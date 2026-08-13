using StreetEmpire.Api.Contracts;

namespace StreetEmpire.Api.Services;

/// <summary>
/// What a player missed while they were gone.
///
/// The game keeps running when nobody is watching: rivals attack, labs produce, hideout builds land,
/// turns pile up. All of it was already recorded, but a returning player had to piece it together from
/// the activity list and a bell badge. This assembles it into one thing to read on arrival.
///
/// Deliberately a pure function over gathered facts rather than something that runs its own queries,
/// so the wording and the decisions about what is worth mentioning can be tested without a database.
/// </summary>
public static class CatchUp
{
    public static CatchUpResponse Build(CatchUpFacts facts)
    {
        var items = new List<CatchUpItemResponse>();
        var awayMinutes = Math.Max(0, (int)Math.Round((facts.NowUtc - facts.SinceUtc).TotalMinutes));

        if (facts.AttacksAgainstYou > 0)
            items.Add(AttackItem(facts));

        if (facts.LabWeed > 0 || facts.LabCoke > 0)
        {
            var made = new List<string>();
            if (facts.LabWeed > 0) made.Add($"{facts.LabWeed:N0} weed");
            if (facts.LabCoke > 0) made.Add($"{facts.LabCoke:N0} coke");
            items.Add(new CatchUpItemResponse(
                "labs",
                "Your labs kept working",
                $"They made {string.Join(" and ", made)} while you were out.",
                "good"));
        }

        foreach (var build in facts.HideoutBuilds)
            items.Add(new CatchUpItemResponse("hideout", "Building finished", build, "good"));

        // Only worth saying once the meter is full, because that is the point at which waiting longer
        // costs the player something.
        if (facts.MaxTurns > 0 && facts.TurnsNow >= facts.MaxTurns)
            items.Add(new CatchUpItemResponse(
                "turns",
                "Your turns are capped",
                $"You are sitting on all {facts.MaxTurns:N0} turns, so no more are accruing. Spend some.",
                "neutral"));

        if (facts.ProtectedUntilUtc is { } until && until > facts.NowUtc)
        {
            var minutes = Math.Max(1, (int)Math.Ceiling((until - facts.NowUtc).TotalMinutes));
            items.Add(new CatchUpItemResponse(
                "protection",
                "You are under protection",
                $"Nobody can attack you for another {minutes} minute(s).",
                "neutral"));
        }

        return new CatchUpResponse(facts.SinceUtc, awayMinutes, items.Count > 0, items);
    }

    /// <summary>
    /// One line for the whole raid, not one per attack. The bell already lists them individually, and a
    /// returning player wants the damage before the detail.
    /// </summary>
    private static CatchUpItemResponse AttackItem(CatchUpFacts facts)
    {
        var attacks = facts.AttacksAgainstYou;
        var breached = attacks - facts.AttacksHeld;
        var headline = attacks == 1
            ? breached == 0 ? "You were attacked and held" : "Someone got into your house"
            : breached == 0 ? $"You held off {attacks:N0} attacks" : $"You were attacked {attacks:N0} times";

        var lost = new List<string>();
        if (facts.CashStolen > 0) lost.Add($"${facts.CashStolen:N0}");
        if (facts.WeedStolen > 0) lost.Add($"{facts.WeedStolen:N0} weed");
        if (facts.CokeStolen > 0) lost.Add($"{facts.CokeStolen:N0} coke");
        if (facts.ThugsLost > 0) lost.Add($"{facts.ThugsLost:N0} thug(s)");
        if (facts.PimpsLost > 0) lost.Add($"{facts.PimpsLost:N0} pimp(s)");

        var detail = lost.Count == 0
            ? breached == 0
                ? "Nothing was taken."
                : "They got through, but took nothing worth naming."
            : $"They took {string.Join(", ", lost)}.";
        if (attacks > 1 && breached > 0 && facts.AttacksHeld > 0)
            detail = $"You held {facts.AttacksHeld:N0} of them. {detail}";

        return new CatchUpItemResponse("attacks", headline, detail, lost.Count == 0 && breached == 0 ? "good" : "bad");
    }
}

/// <summary>Everything the digest is built from, gathered by the endpoint in one pass.</summary>
public sealed record CatchUpFacts(
    DateTime SinceUtc,
    DateTime NowUtc,
    int AttacksAgainstYou,
    int AttacksHeld,
    long CashStolen,
    int WeedStolen,
    int CokeStolen,
    int ThugsLost,
    int PimpsLost,
    int LabWeed,
    int LabCoke,
    IReadOnlyList<string> HideoutBuilds,
    int TurnsNow,
    int MaxTurns,
    DateTime? ProtectedUntilUtc);
