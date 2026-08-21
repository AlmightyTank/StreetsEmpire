namespace StreetEmpire.Api.Services;

/// <summary>
/// How an AI rival picks a fight. Kept separate from the simulator and free of database types so the
/// judgement can be tested directly: a bot that picks badly is worse than one that never attacks,
/// because it feeds free loot and protection windows to whoever it loses against.
/// </summary>
public static class BotTargeting
{
    /// <summary>
    /// The most attractive target a bot can legally hit, or null if none qualify. Prefers the weakest
    /// defence it still comfortably beats: bots should look opportunistic, not suicidal, and the
    /// anti-farm rules already stop them punching too far down.
    /// </summary>
    /// <param name="grudges">
    /// Who has hit this rival lately, and how often. Without it a rival picks the richest beatable
    /// target every time and forgets being robbed the moment it happens, so nothing between two of
    /// them ever becomes a story: the world reads as weather rather than as people.
    /// </param>
    /// <param name="grudgeWeight">
    /// How much a grudge is worth against a fatter target, as a share of the haul added per hit
    /// taken. Personality decides it, so a hard charger chases the man who robbed him while a banker
    /// goes on taking the best deal available.
    /// </param>
    public static BotTarget? Choose(
        IReadOnlyList<BotTarget> candidates,
        long attackerPlunder,
        int attackerPower,
        AntiFarmOptions antiFarm,
        double winMargin,
        IReadOnlyDictionary<Guid, int>? grudges = null,
        double grudgeWeight = 0)
    {
        BotTarget? best = null;
        var bestAppeal = long.MinValue;
        foreach (var candidate in candidates)
        {
            if (candidate.IsProtected)
                continue;
            // Already being swarmed: launching would be refused, and piling on is the behaviour the
            // incoming cap exists to prevent.
            if (candidate.IncomingAttacks >= Math.Max(1, antiFarm.MaxIncomingAttacks))
                continue;
            if (AntiFarm.RejectReason(attackerPlunder, candidate.Plunder, antiFarm) is not null)
                continue;
            // Only pick fights it should win. Defence power already folds in weapons and morale.
            if (attackerPower < candidate.DefensePower * Math.Max(1, winMargin))
                continue;

            // Richest beatable target, plus whatever settling a score is worth to this personality.
            // A grudge does not make a rival reckless: everything above still applies, so it only ever
            // decides between fights it was already willing to take.
            var hits = grudges is not null && grudges.TryGetValue(candidate.PlayerId, out var taken) ? taken : 0;
            var appeal = candidate.Plunder + (long)(candidate.Plunder * Math.Max(0, grudgeWeight) * hits);
            if (best is null || appeal > bestAppeal)
            {
                best = candidate;
                bestAppeal = appeal;
            }
        }

        return best;
    }
}

/// <summary>A candidate defender, flattened so targeting never touches the database.</summary>
/// <summary>A possible victim, weighed by what is actually on the table rather than what they are worth.</summary>
public sealed record BotTarget(Guid PlayerId, string Name, long Plunder, int DefensePower, bool IsProtected, int IncomingAttacks);
