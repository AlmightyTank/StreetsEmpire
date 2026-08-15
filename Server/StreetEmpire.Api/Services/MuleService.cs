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
    public const string Lost = "Lost";
}

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
