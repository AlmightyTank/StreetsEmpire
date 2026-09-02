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

        // Read off the house as it stands rather than off what happened while they were gone, which is
        // the one item here that is deliberately not a report of the absence. A room that was already
        // dark when they left is still dark now and still costing them every hour, and a digest that
        // only mentioned rooms broken since Tuesday would go quiet on the second morning - which is
        // exactly the morning somebody needs telling.
        if (facts.WreckedRooms.Count > 0)
            items.Add(new CatchUpItemResponse(
                "hideout",
                facts.WreckedRooms.Count == 1
                    ? $"Your {facts.WreckedRooms[0]} is wrecked"
                    : $"{facts.WreckedRooms.Count:N0} of your rooms are wrecked",
                facts.WreckedRooms.Count == 1
                    ? $"Your {facts.WreckedRooms[0]} does nothing until it is repaired."
                    : $"{Names(facts.WreckedRooms)} do nothing until they are repaired.",
                "bad"));

        if (facts.GroundLost.Count > 0)
            items.Add(new CatchUpItemResponse(
                "ground",
                facts.GroundLost.Count == 1 ? $"You lost {facts.GroundLost[0]}" : $"You lost {facts.GroundLost.Count:N0} pieces of ground",
                $"{Names(facts.GroundLost)} was taken off you while you were out.",
                "bad"));

        if (facts.GroundHeld.Count > 0)
            items.Add(new CatchUpItemResponse(
                "ground",
                facts.GroundHeld.Count == 1 ? $"{facts.GroundHeld[0]} held" : $"{facts.GroundHeld.Count:N0} pieces held",
                facts.GarrisonThugsLost > 0
                    ? $"{Names(facts.GroundHeld)} was attacked and held. The garrison lost {facts.GarrisonThugsLost:N0} thug(s)."
                    : $"{Names(facts.GroundHeld)} was attacked and held without a scratch.",
                "good"));

        if (facts.GroundTaken.Count > 0)
            items.Add(new CatchUpItemResponse(
                "ground",
                facts.GroundTaken.Count == 1 ? $"You took {facts.GroundTaken[0]}" : $"You took {facts.GroundTaken.Count:N0} pieces of ground",
                $"{Names(facts.GroundTaken)} is yours now.",
                "good"));

        if (RankItem(facts) is { } rank)
            items.Add(rank);
        if (PassedItem(facts) is { } passed)
            items.Add(passed);

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
    /// Where the player finished up against where they started. Rank rises as the number falls, which
    /// is worth being careful about in the wording: "moved up to #3" reads better than any arrow.
    /// </summary>
    private static CatchUpItemResponse? RankItem(CatchUpFacts facts)
    {
        if (facts.RankBefore is not { } before || facts.RankNow is not { } now || before == now)
            return null;

        var climbed = now < before;
        return new CatchUpItemResponse(
            "rank",
            climbed ? $"You climbed to #{now:N0}" : $"You slipped to #{now:N0}",
            climbed
                ? $"Up from #{before:N0} while you were out."
                : $"Down from #{before:N0} while you were out.",
            climbed ? "good" : "bad");
    }

    /// <summary>
    /// Who changed places with the player. Both directions are worth a line: being overtaken is the
    /// thing to react to, and overtaking someone is the thing worth noticing.
    /// </summary>
    private static CatchUpItemResponse? PassedItem(CatchUpFacts facts)
    {
        if (facts.OvertookYou.Count == 0 && facts.YouOvertook.Count == 0)
            return null;

        if (facts.OvertookYou.Count > 0)
            return new CatchUpItemResponse(
                "rivals",
                facts.OvertookYou.Count == 1 ? $"{facts.OvertookYou[0]} moved ahead of you" : $"{facts.OvertookYou.Count:N0} rivals moved ahead of you",
                facts.YouOvertook.Count > 0
                    ? $"{Names(facts.OvertookYou)} got past you, though you left {Names(facts.YouOvertook)} behind."
                    : $"{Names(facts.OvertookYou)} got past you.",
                "bad");

        return new CatchUpItemResponse(
            "rivals",
            facts.YouOvertook.Count == 1 ? $"You passed {facts.YouOvertook[0]}" : $"You passed {facts.YouOvertook.Count:N0} rivals",
            $"{Names(facts.YouOvertook)} are behind you now.",
            "good");
    }

    /// <summary>Names read better than a count until there are too many to list.</summary>
    private static string Names(IReadOnlyList<string> names)
    {
        if (names.Count == 1) return names[0];
        if (names.Count == 2) return $"{names[0]} and {names[1]}";
        if (names.Count <= 4) return $"{string.Join(", ", names.Take(names.Count - 1))}, and {names[^1]}";
        return $"{string.Join(", ", names.Take(3))}, and {names.Count - 3:N0} others";
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
    DateTime? ProtectedUntilUtc,
    /// <summary>Null when no standings sample covers the absence, which is not the same as no change.</summary>
    int? RankBefore = null,
    int? RankNow = null,
    IReadOnlyList<string>? OvertookYouNames = null,
    IReadOnlyList<string>? YouOvertookNames = null,
    IReadOnlyList<string>? GroundLostNames = null,
    IReadOnlyList<string>? GroundTakenNames = null,
    IReadOnlyList<string>? GroundHeldNames = null,
    int GarrisonThugsLost = 0,
    /// <summary>Rooms that are not working right now, named the way the hideout page names them.</summary>
    IReadOnlyList<string>? WreckedRoomNames = null)
{
    public IReadOnlyList<string> WreckedRooms => WreckedRoomNames ?? [];
    public IReadOnlyList<string> OvertookYou => OvertookYouNames ?? [];
    public IReadOnlyList<string> YouOvertook => YouOvertookNames ?? [];
    public IReadOnlyList<string> GroundLost => GroundLostNames ?? [];
    public IReadOnlyList<string> GroundTaken => GroundTakenNames ?? [];
    public IReadOnlyList<string> GroundHeld => GroundHeldNames ?? [];
}
