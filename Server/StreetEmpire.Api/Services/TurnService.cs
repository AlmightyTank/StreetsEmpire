using Microsoft.Extensions.Options;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

public sealed class TurnService(IOptionsSnapshot<GameOptions> options, PimpRoster pimps)
{
    private readonly GameOptions _options = options.Value;

    /// <summary>
    /// Charges whole-hour crew upkeep: condoms for hoes, beer or moonshine for pimps and thugs, and
    /// weed or coke for everybody. Shortages press morale and pimp loyalty, but never invent debt.
    /// </summary>
    public CrewUpkeep ChargeHourlyUpkeep(Player player, int hours, int prepaidPimps = 0)
    {
        if (hours <= 0)
            return CrewUpkeep.None;

        var morale = _options.Morale;
        var activePimps = Math.Max(0, player.Pimps - Math.Max(0, prepaidPimps));
        var hoes = Math.Max(0, player.Hoes);
        var thugs = Math.Max(0, player.Thugs);
        var beerCrew = activePimps + thugs;
        var totalCrew = activePimps + hoes + thugs;
        // Kept for the receipt, which reports the hours this charge covered. It stopped being a count
        // of turns when the standing charge got rates of its own: an hour is no longer a turn's worth
        // of upkeep, it is an hour's.
        var upkeepTurns = hours;

        var condomsNeeded = RequiredHourlyUpkeep(hoes, hours, morale.HoursPerCondomUpkeep);
        var beerNeeded = RequiredHourlyUpkeep(beerCrew, hours, morale.HoursPerBeerUpkeep);
        var drugsNeeded = RequiredHourlyUpkeep(totalCrew, hours, morale.HoursPerDrugUpkeep);

        var condomsUsed = Take(player.Condoms, condomsNeeded);
        player.Condoms -= condomsUsed;
        var beerUsed = Take(player.Beer, beerNeeded);
        player.Beer -= beerUsed;
        var moonshineUsed = Take(player.Moonshine, beerNeeded - beerUsed);
        player.Moonshine -= moonshineUsed;
        var weedUsed = Take(player.Weed, drugsNeeded);
        player.Weed -= weedUsed;
        var cokeUsed = Take(player.Coke, drugsNeeded - weedUsed);
        player.Coke -= cokeUsed;

        var condomShortage = Math.Max(0, condomsNeeded - condomsUsed);
        var beerShortage = Math.Max(0, beerNeeded - beerUsed - moonshineUsed);
        var drugShortage = Math.Max(0, drugsNeeded - weedUsed - cokeUsed);

        var moralePenalty = Math.Max(0, morale.PassiveUpkeepMoralePenaltyPerHour);
        var loyaltyPenalty = Math.Max(0, morale.PassiveUpkeepLoyaltyPenaltyPerHour);
        var drugShare = ShortageShare(drugShortage, drugsNeeded);
        var hoePenalty = hours * moralePenalty * (ShortageShare(condomShortage, condomsNeeded) + drugShare);
        var thugPenalty = hours * moralePenalty * (ShortageShare(beerShortage, beerNeeded) + drugShare);
        var pimpPenalty = hours * loyaltyPenalty * (ShortageShare(beerShortage, beerNeeded) + drugShare);

        player.HoeHappiness = RecoverMorale(player.HoeHappiness, -hoePenalty);
        player.ThugHappiness = RecoverMorale(player.ThugHappiness, -thugPenalty);
        pimps.Pressure(player, pimpPenalty);

        return new CrewUpkeep(
            hours,
            upkeepTurns,
            activePimps,
            hoes,
            thugs,
            condomsNeeded,
            condomsUsed,
            beerNeeded,
            beerUsed,
            moonshineUsed,
            drugsNeeded,
            weedUsed,
            cokeUsed,
            Math.Round(hoePenalty, 2),
            Math.Round(thugPenalty, 2),
            Math.Round(pimpPenalty, 2));
    }

    /// <param name="moraleRecoveryPercent">
    /// What the player's clubs add to passive recovery. A percentage on the existing rate rather than a
    /// separate trickle, so there is still only one place morale recovers.
    /// </param>
    public bool Refresh(Player player, DateTime nowUtc, int moraleRecoveryPercent = 0)
    {
        var tick = TimeSpan.FromMinutes(_options.TurnTickMinutes);
        var elapsed = nowUtc - player.LastTurnUpdateUtc;
        if (elapsed < tick)
            return false;

        var completedTicks = (int)Math.Floor(elapsed.TotalMinutes / _options.TurnTickMinutes);
        if (completedTicks <= 0)
            return false;

        // Faster while they are small, tapering to the normal rate as the empire grows. Read per
        // refresh rather than stored, so it follows the player rather than needing to be recalculated.
        var turnsToAdd = completedTicks * _options.TurnsPerTickFor(player);
        var moraleRecovery = completedTicks
            * Math.Max(0, _options.Morale.PassiveRecoveryPerTick)
            * (1 + Math.Max(0, moraleRecoveryPercent) / 100.0);
        // What this player's building holds, not what the game opens at. Read per refresh for the
        // same reason the rate is: it follows the player rather than needing recalculating when they
        // move up, and a build that lands mid-session raises the ceiling on the very next tick.
        var maxTurns = _options.MaxTurnsFor(player);
        var turnsBefore = player.Turns;
        var hoeBefore = player.HoeHappiness;
        var thugBefore = player.ThugHappiness;
        var clockBefore = player.LastTurnUpdateUtc;
        player.Turns = Math.Min(maxTurns, player.Turns + turnsToAdd);
        player.HoeHappiness = RecoverMorale(player.HoeHappiness, moraleRecovery);
        player.ThugHappiness = RecoverMorale(player.ThugHappiness, moraleRecovery);
        // Pimps cool off over the same ticks, so loyalty is not a one-way ratchet.
        pimps.Recover(player, completedTicks * pimps.PassiveRecoveryPerTick);
        player.LastTurnUpdateUtc = player.Turns >= maxTurns
            ? nowUtc
            : player.LastTurnUpdateUtc.AddMinutes(completedTicks * _options.TurnTickMinutes);
        return turnsBefore != player.Turns
            || !DoubleEquals(hoeBefore, player.HoeHappiness)
            || !DoubleEquals(thugBefore, player.ThugHappiness)
            || clockBefore != player.LastTurnUpdateUtc;
    }

    public int SecondsUntilNextTick(Player player, DateTime nowUtc)
    {
        if (player.Turns >= _options.MaxTurnsFor(player))
            return 0;

        var next = player.LastTurnUpdateUtc.AddMinutes(_options.TurnTickMinutes);
        return Math.Max(0, (int)Math.Ceiling((next - nowUtc).TotalSeconds));
    }

    private static double RecoverMorale(double current, double amount)
        => Math.Round(Math.Clamp(current + amount, 0, 100), 2);

    private static bool DoubleEquals(double left, double right)
        => Math.Abs(left - right) < 0.001;

    private static int RequiredHourlyUpkeep(int crewCount, int hours, double hoursPerSupply)
    {
        if (crewCount <= 0 || hours <= 0 || hoursPerSupply <= 0) return 0;
        return Math.Max(0, (int)Math.Ceiling(crewCount * hours / hoursPerSupply));
    }

    private static int Take(int held, int needed)
        => Math.Min(Math.Max(0, held), Math.Max(0, needed));

    private static double ShortageShare(int shortage, int needed)
        => shortage <= 0 || needed <= 0 ? 0 : (double)shortage / needed;
}

public sealed record CrewUpkeep(
    int Hours,
    int UpkeepTurns,
    int Pimps,
    int Hoes,
    int Thugs,
    int CondomsNeeded,
    int CondomsUsed,
    int BeerNeeded,
    int BeerUsed,
    int MoonshineUsed,
    int DrugsNeeded,
    int WeedUsed,
    int CokeUsed,
    double HoeMoralePenalty,
    double ThugMoralePenalty,
    double PimpLoyaltyPenalty)
{
    public static readonly CrewUpkeep None = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    public bool Any => CondomsUsed > 0 || BeerUsed > 0 || MoonshineUsed > 0 || WeedUsed > 0 || CokeUsed > 0
                       || CondomShortage > 0 || BeerShortage > 0 || DrugShortage > 0;
    public int CondomShortage => Math.Max(0, CondomsNeeded - CondomsUsed);
    public int BeerShortage => Math.Max(0, BeerNeeded - BeerUsed - MoonshineUsed);
    public int DrugShortage => Math.Max(0, DrugsNeeded - WeedUsed - CokeUsed);

    public string Describe()
    {
        var summary = $"Crew upkeep ran for {Hours:N0} hour{(Hours == 1 ? string.Empty : "s")}.";
        var spent = SupplyList(
            (CondomsUsed, "condom", "condoms"),
            (BeerUsed, "beer", "beer"),
            (MoonshineUsed, "moonshine", "moonshine"),
            (WeedUsed, "weed", "weed"),
            (CokeUsed, "coke", "coke"));
        if (spent.Length > 0)
            summary += $" Spent {spent}.";

        var shorted = SupplyList(
            (CondomShortage, "condom", "condoms"),
            (BeerShortage, "beer or moonshine", "beer or moonshine"),
            (DrugShortage, "weed or coke", "weed or coke"));
        if (shorted.Length > 0)
            summary += $" Ran short {shorted}.";
        return summary;
    }

    private static string SupplyList(params (int Count, string One, string Many)[] items)
    {
        var parts = items
            .Where(x => x.Count > 0)
            .Select(x => $"{x.Count:N0} {(x.Count == 1 ? x.One : x.Many)}")
            .ToList();
        return parts.Count switch
        {
            0 => string.Empty,
            1 => parts[0],
            2 => $"{parts[0]} and {parts[1]}",
            _ => $"{string.Join(", ", parts.Take(parts.Count - 1))}, and {parts[^1]}"
        };
    }
}
