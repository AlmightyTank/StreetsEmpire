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
    public static BotTarget? Choose(
        IReadOnlyList<BotTarget> candidates,
        long attackerNetWorth,
        int attackerPower,
        AntiFarmOptions antiFarm,
        double winMargin)
    {
        BotTarget? best = null;
        foreach (var candidate in candidates)
        {
            if (candidate.IsProtected)
                continue;
            // Already being swarmed: launching would be refused, and piling on is the behaviour the
            // incoming cap exists to prevent.
            if (candidate.IncomingAttacks >= Math.Max(1, antiFarm.MaxIncomingAttacks))
                continue;
            if (AntiFarm.RejectReason(attackerNetWorth, candidate.NetWorth, antiFarm) is not null)
                continue;
            // Only pick fights it should win. Defence power already folds in weapons and morale.
            if (attackerPower < candidate.DefensePower * Math.Max(1, winMargin))
                continue;

            // Richest beatable target: the haul scales with what they are holding.
            if (best is null || candidate.NetWorth > best.NetWorth)
                best = candidate;
        }

        return best;
    }
}

/// <summary>A candidate defender, flattened so targeting never touches the database.</summary>
public sealed record BotTarget(Guid PlayerId, string Name, long NetWorth, int DefensePower, bool IsProtected, int IncomingAttacks);
