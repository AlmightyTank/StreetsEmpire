namespace StreetEmpire.Api.Services;

/// <summary>
/// Rules that stop a strong player farming a weak one. Three separate levers, because each closes a
/// different hole:
///
/// - A net worth floor and ratio decide whether an attack may happen at all, so heavyweights cannot
///   pick on newcomers who have no realistic defence.
/// - Diminishing loot removes the reward for hitting the same victim over and over, without banning it.
/// - Escalating protection widens the gap between hits on someone who is being dogpiled.
///
/// The maths lives here as pure functions so it can be tested without a database or a live fight.
/// </summary>
public static class AntiFarm
{
    /// <summary>Why an attack is not allowed, or null when it is.</summary>
    public static string? RejectReason(long attackerNetWorth, long defenderNetWorth, AntiFarmOptions options)
    {
        if (defenderNetWorth < options.MinDefenderNetWorth)
            return $"{FormatMoney(options.MinDefenderNetWorth)} net worth is the floor for being attacked. This target is still under it.";

        // Only guards the strong hitting the weak. Punching up is always allowed.
        var ratio = Math.Max(1, options.MaxNetWorthRatio);
        if (defenderNetWorth > 0 && attackerNetWorth > defenderNetWorth * ratio)
            return $"You outweigh this target by more than {ratio:N1}x. Pick on someone your own size.";

        return null;
    }

    /// <summary>
    /// What fraction of the normal haul a victory earns, given how many times this attacker has already
    /// beaten this defender inside the window. Decays per prior win and never reaches zero, so a repeat
    /// attack is pointless rather than forbidden.
    /// </summary>
    public static double LootMultiplier(int priorVictories, AntiFarmOptions options)
    {
        if (priorVictories <= 0)
            return 1;

        var decay = Math.Clamp(options.LootDecayPerRepeat, 0, 1);
        var floor = Math.Clamp(options.MinLootMultiplier, 0, 1);
        var multiplier = Math.Pow(1 - decay, priorVictories);
        return Math.Max(floor, multiplier);
    }

    /// <summary>
    /// How long the defender is shielded, widening with each hit they have already taken in the window
    /// from anyone. Being dogpiled by several attackers escalates as fast as being farmed by one.
    /// </summary>
    public static int ProtectionMinutes(int recentHits, int baseMinutes, AntiFarmOptions options)
    {
        var floor = Math.Max(1, baseMinutes);
        if (recentHits <= 0)
            return floor;

        var escalated = floor * (1 + Math.Max(0, options.ProtectionEscalationPerHit) * recentHits);
        return (int)Math.Round(Math.Min(escalated, Math.Max(floor, options.MaxProtectionMinutes)));
    }

    private static string FormatMoney(long value) => $"${value:N0}";
}
