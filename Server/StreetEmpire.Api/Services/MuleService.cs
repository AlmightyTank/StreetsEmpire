using Microsoft.Extensions.Options;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

/// <summary>
/// Sends crew to another town to buy cheap and carry it home.
///
/// This is the launch half: validating the run, taking what it costs, and freezing everything the
/// outcome will depend on. Nothing here decides whether the run succeeds. That is deliberate — a run
/// resolves in real time, long after the request that started it has gone, so the only honest place
/// to settle it is the clock.
/// </summary>
public sealed class MuleService(IOptionsSnapshot<GameOptions> options, HideoutService hideouts)
{
    private readonly GameOptions _options = options.Value;

    /// <summary>What a run would cost and face, before anybody commits to it.</summary>
    public MuleQuote Quote(Player player, string? city, string? good, int hoes, long cashToSend)
    {
        var destination = ResolveDestination(player, city);
        var product = ResolveGood(good);
        var crew = Math.Clamp(hoes, 1, Math.Max(1, _options.Mules.MaxHoesPerRun));
        var mules = _options.Mules;

        var travelTurns = _options.CityMarkets.TravelTurns(destination);
        var legMinutes = Math.Max(1, travelTurns * Math.Max(1, mules.MinutesPerTravelTurn));
        var tripMinutes = legMinutes * 2 + Math.Max(0, mules.BuyingMinutes);

        // One pimp leads, the hoes carry. Both are away and earning nothing, which is the real price.
        var heads = 1 + crew;
        var fare = heads * travelTurns * Math.Max(0, mules.FarePerHeadPerTravelTurn);
        var upkeep = (long)Math.Round(heads * Math.Max(0, mules.UpkeepPerHeadPerHour) * (tripMinutes / 60.0));
        var turns = Math.Max(
            Math.Max(1, mules.MinTurnCost),
            (int)Math.Ceiling(travelTurns * Math.Max(0, mules.TurnCostPerTravelTurn)));

        var capacity = crew * Math.Max(1, mules.HoeCarryCapacity);
        var unitPrice = TradeGoods.ReferencePrice(_options, product, destination);
        var cash = Math.Max(0, cashToSend);

        return new MuleQuote(
            destination,
            product,
            crew,
            capacity,
            travelTurns,
            legMinutes,
            tripMinutes,
            turns,
            fare,
            upkeep,
            cash,
            fare + upkeep + cash,
            unitPrice,
            unitPrice <= 0 ? 0 : (int)Math.Min(capacity, cash / unitPrice),
            BustChancePercent(player, destination, crew),
            DefectChancePercent(player, null, destination));
    }

    /// <summary>
    /// Commits the run. Everything the outcome depends on is written down now, because a pimp whose
    /// loyalty slips while the plane is in the air must not change a run that is already out.
    /// </summary>
    public MuleRun Launch(Player player, Pimp pimp, string? city, string? good, int hoes, long cashToSend, int runsAlreadyOut, DateTime nowUtc)
    {
        TravelGate.EnsureLanded(player);

        var cap = hideouts.ConcurrentRunCap(player.Hideout);
        if (cap <= 0)
            throw new GameRuleException("You need an intelligence centre before you can run mules.");
        if (runsAlreadyOut >= cap)
            throw new GameRuleException($"Your intelligence centre can only run {cap} at a time. {runsAlreadyOut} already out.");

        if (pimp.PlayerId != player.Id || pimp.LostAtUtc is not null)
            throw new GameRuleException("That pimp is not on your payroll.");

        var quote = Quote(player, city, good, hoes, cashToSend);
        if (string.Equals(quote.DestinationCity, player.City, StringComparison.OrdinalIgnoreCase))
            throw new GameRuleException("A mule run has to go somewhere else.");
        if (player.Hoes < quote.Hoes)
            throw new GameRuleException($"You need {quote.Hoes} hoe(s) to send. You have {player.Hoes}.");
        if (player.Turns < quote.Turns)
            throw new GameRuleException($"Briefing a run to {quote.DestinationCity} takes {quote.Turns} turn(s).");
        if (quote.CashSent <= 0)
            throw new GameRuleException("Send them with something to buy with.");
        if (player.Cash + player.BankCash < quote.TotalCost)
            throw new GameRuleException(
                $"A run to {quote.DestinationCity} costs {quote.TotalCost:C0}: {quote.CashSent:C0} to buy with, {quote.Fare:C0} in fares and {quote.Upkeep:C0} to keep them while they are gone.");

        // Bank first, same as a hideout upgrade: money is money, and making a player withdraw by hand
        // before every run would be a chore rather than a decision.
        var fromBank = Math.Min(player.BankCash, quote.TotalCost);
        player.BankCash -= fromBank;
        player.Cash -= quote.TotalCost - fromBank;
        player.Turns -= quote.Turns;
        // The hoes go with them, so they are off the books until they come back. Counting them at home
        // would have them earning on the streets and carrying cargo at the same time.
        player.Hoes -= quote.Hoes;

        return new MuleRun
        {
            PlayerId = player.Id,
            OriginCity = player.City,
            DestinationCity = quote.DestinationCity,
            Good = quote.Good,
            Status = MuleRunStatus.Outbound,
            Outcome = MuleRunOutcome.Pending,
            PimpId = pimp.Id,
            PimpName = pimp.Name,
            PimpLoyaltyAtLaunch = pimp.Loyalty,
            AssignedHoes = quote.Hoes,
            Capacity = quote.Capacity,
            CashSent = quote.CashSent,
            TravelCost = quote.Fare,
            UpkeepCost = quote.Upkeep,
            TurnsSpent = quote.Turns,
            BustChancePercent = quote.BustChancePercent,
            DefectChancePercent = DefectChancePercent(player, pimp, quote.DestinationCity),
            DepartedAtUtc = nowUtc,
            ArrivesAtUtc = nowUtc.AddMinutes(quote.LegMinutes),
            ReturnsAtUtc = nowUtc.AddMinutes(quote.TripMinutes),
            Summary = $"{pimp.Name} and {quote.Hoes} hoe(s) left for {quote.DestinationCity}."
        };
    }

    /// <summary>Where a run is right now, from the clock alone. No state to drift out of step.</summary>
    public string StatusAt(MuleRun run, DateTime nowUtc)
    {
        if (run.SettledAtUtc is not null) return MuleRunStatus.Done;
        if (nowUtc >= run.ReturnsAtUtc) return MuleRunStatus.Done;
        if (nowUtc >= run.ArrivesAtUtc) return MuleRunStatus.Inbound;
        return MuleRunStatus.Outbound;
    }

    /// <summary>
    /// A mule is sloppier than the player making the same trip, and every extra body is another thing
    /// to notice. Knowing the route takes a share off, but never all of it.
    /// </summary>
    public int BustChancePercent(Player player, string destination, int hoes)
    {
        var mules = _options.Mules;
        var chance = _options.CityMarkets.BustChance(destination) * Math.Max(0, mules.BustChanceMultiplier)
                     + Math.Max(0, hoes - 1) * Math.Max(0, mules.BustChancePerExtraHoe);
        chance *= 1 - hideouts.RouteRiskReduction(player.Hideout);
        return (int)Math.Round(Math.Clamp(chance, 0, Math.Clamp(mules.MaxBustChance, 0, 1)) * 100, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// A pimp far from home with your money and little reason to come back. Scales with how far below
    /// the threshold their loyalty sits and with how far away they are, because distance is what makes
    /// walking away easy.
    /// </summary>
    public int DefectChancePercent(Player player, Pimp? pimp, string destination)
    {
        var mules = _options.Mules;
        var loyalty = pimp?.Loyalty ?? _options.Pimps.StartingLoyalty;
        var threshold = Math.Max(0, mules.DefectLoyaltyThreshold);
        if (threshold <= 0 || loyalty >= threshold) return 0;

        var shortfall = (threshold - loyalty) / threshold;
        var distance = _options.CityMarkets.TravelTurns(destination) / 6.0;
        var chance = Math.Clamp(mules.MaxDefectChance, 0, 1) * shortfall * Math.Clamp(0.5 + distance, 0.5, 1.5);
        return (int)Math.Round(Math.Clamp(chance, 0, 1) * 100, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Settles a run that is home, or that never will be.
    ///
    /// Buying happens here rather than at launch because they buy at the destination's price, and the
    /// only moment that price is real is when they are standing in it. Everything else the outcome
    /// turns on was frozen when they left, so a player who changed nothing while the plane was in the
    /// air gets the run they actually booked.
    /// </summary>
    public MuleSettlement Settle(MuleRun run, Player player, Pimp? pimp, IGameRandom random, DateTime nowUtc)
    {
        var mules = _options.Mules;
        var price = Math.Max(1, TradeGoods.ReferencePrice(_options, run.Good, run.DestinationCity));
        var units = (int)Math.Min(run.Capacity, run.CashSent / price);
        var unspent = run.CashSent - units * price;

        run.UnitPricePaid = price;
        run.UnitsBought = units;
        run.SettledAtUtc = nowUtc;
        run.Status = MuleRunStatus.Done;

        // A pimp far from home with your money and nothing to come back for. Rolled before the law,
        // because a man who has already gone was never carrying anything to be caught with.
        if (random.NextDouble() < run.DefectChancePercent / 100.0)
        {
            run.Outcome = MuleRunOutcome.Defected;
            run.PimpLost = true;
            run.HoesLost = run.AssignedHoes;
            if (pimp is not null) pimp.LostAtUtc = nowUtc;
            run.Summary = $"{run.PimpName} never came back from {run.DestinationCity}. He kept {run.CashSent:C0}, the {run.AssignedHoes} hoe(s), and whatever he bought with it.";
            return new MuleSettlement(run, 0, 0, 0, 0);
        }

        if (random.NextDouble() < run.BustChancePercent / 100.0)
        {
            var share = Math.Clamp(
                mules.SeizureMinPercent + random.NextDouble() * Math.Max(0, mules.SeizureMaxPercent - mules.SeizureMinPercent),
                0,
                1);
            var seized = Math.Min(units, (int)Math.Ceiling(units * share));
            var kept = units - seized;
            run.Outcome = MuleRunOutcome.Seized;
            run.SeizedUnits = seized;
            run.HeatAdded = seized * Math.Max(0, mules.HeatPerSeizedUnit);
            // Cash they had not spent yet goes with them. It was in the room when the door came in.
            run.CashReturned = 0;
            player.Heat += run.HeatAdded;
            var (delivered, seizedSpill) = Deliver(player, run.Good, kept);
            var hoes = ReturnHoes(player, run);
            run.Summary = seized >= units && units > 0
                ? $"{run.PimpName} was stopped coming back from {run.DestinationCity}. They took all {seized:N0} {run.Good} and the {unspent:C0} he had left."
                : $"{run.PimpName} was stopped coming back from {run.DestinationCity}. They took {seized:N0} {run.Good} and the {unspent:C0} he had left; {delivered:N0} got through.{Spill(seizedSpill, run.Good)}";
            return new MuleSettlement(run, delivered, 0, hoes, run.HeatAdded);
        }

        run.Outcome = MuleRunOutcome.Delivered;
        run.CashReturned = unspent;
        player.Cash += unspent;
        var (landed, landedSpill) = Deliver(player, run.Good, units);
        var home = ReturnHoes(player, run);
        run.Summary = unspent > 0
            ? $"{run.PimpName} is back from {run.DestinationCity} with {landed:N0} {run.Good} at {price:C0} each, and {unspent:C0} unspent.{Spill(landedSpill, run.Good)}"
            : $"{run.PimpName} is back from {run.DestinationCity} with {landed:N0} {run.Good} at {price:C0} each.{Spill(landedSpill, run.Good)}";
        return new MuleSettlement(run, landed, unspent, home, 0);
    }

    /// <summary>
    /// Puts the load away, up to what the storage room holds. Cargo that will not fit is left behind
    /// rather than overfilling the room, the same way a lab stops at the walls. What was left is
    /// returned as well as what was stored, because the player paid for both and a run that quietly
    /// dropped a third of the load would read as the price being wrong.
    /// </summary>
    private (int Stored, int Spilled) Deliver(Player player, string good, int units)
    {
        if (units <= 0) return (0, 0);
        var capacity = TradeGoods.Capacity(hideouts.CapacityFor(player.Hideout), good);
        var room = Math.Max(0, capacity - TradeGoods.Held(player, good));
        var stored = Math.Min(units, room);
        TradeGoods.Add(player, good, stored, 1);
        return (stored, units - stored);
    }

    /// <summary>Says what would not fit, so a short delivery is never silent.</summary>
    private static string Spill(int spilled, string good)
        => spilled <= 0 ? string.Empty : $" {spilled:N0} {good} was dumped: your storage room is full.";

    /// <summary>
    /// Brings the crew back onto the books, up to the hideout's cap. They were taken off at launch, so
    /// this can only bind if the player hired replacements while they were gone.
    /// </summary>
    private int ReturnHoes(Player player, MuleRun run)
    {
        var cap = hideouts.CapacityFor(player.Hideout).MaxHoes;
        var room = Math.Max(0, cap - player.Hoes);
        var home = Math.Min(run.AssignedHoes, room);
        player.Hoes += home;
        run.HoesLost = run.AssignedHoes - home;
        return home;
    }

    private string ResolveDestination(Player player, string? city)
        => _options.CityMarkets.ResolveCity(city)
           ?? throw new GameRuleException($"Pick one of: {string.Join(", ", _options.CityMarkets.Profiles.Where(x => !string.Equals(x.City, player.City, StringComparison.OrdinalIgnoreCase)).Select(x => x.City))}.");

    /// <summary>
    /// Only what is worth a plane ticket. Condoms and beer are the same price everywhere, so a run for
    /// them would be a pure loss dressed up as a decision.
    /// </summary>
    private static string ResolveGood(string? good)
    {
        var key = good?.Trim().ToLowerInvariant();
        return key is "weed" or "coke"
            ? key
            : throw new GameRuleException("A mule run carries weed or coke.");
    }
}

public static class MuleRunStatus
{
    public const string Outbound = "Outbound";
    public const string Inbound = "Inbound";
    public const string Done = "Done";
}

public static class MuleRunOutcome
{
    public const string Pending = "Pending";
    public const string Delivered = "Delivered";
    public const string Seized = "Seized";
    public const string Defected = "Defected";
}

/// <summary>What a settled run actually returned, for the row the player reads afterwards.</summary>
public sealed record MuleSettlement(MuleRun Run, int UnitsDelivered, long CashReturned, int HoesHome, double HeatAdded);

/// <summary>What a run would cost and face. Shown before committing, so a run is never a surprise.</summary>
public sealed record MuleQuote(
    string DestinationCity,
    string Good,
    int Hoes,
    int Capacity,
    int TravelTurns,
    int LegMinutes,
    int TripMinutes,
    int Turns,
    long Fare,
    long Upkeep,
    long CashSent,
    long TotalCost,
    long UnitPriceThere,
    int UnitsAffordable,
    int BustChancePercent,
    int DefectChancePercent);
