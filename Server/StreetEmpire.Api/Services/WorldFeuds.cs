namespace StreetEmpire.Api.Services;

/// <summary>
/// Picking the quarrel worth reporting out of a pile of fights.
///
/// Separated from the endpoint and free of database types so it can be tested directly, which it
/// needed to be: the first version grouped each pair by comparing their ids and then read the
/// aggressor off that ordering, so every one-sided feud credited whichever name happened to sort
/// first. It reported the victim as the one kicking the door in about half the time.
/// </summary>
public static class WorldFeuds
{
    /// <summary>
    /// The loudest quarrel: mutual before one-sided, then by how many times they have gone at it.
    /// A quarrel is a pair rather than a direction, so blows both ways are one story.
    /// </summary>
    public static Feud? Pick(IReadOnlyList<FeudRound> rounds, int minimumRounds = 3)
        => rounds
            .GroupBy(x => x.AttackerId.CompareTo(x.DefenderId) < 0
                ? (First: x.AttackerId, Second: x.DefenderId)
                : (First: x.DefenderId, Second: x.AttackerId))
            .Select(group => new Feud(
                // Named off an actual fight. The id ordering above only groups the two directions
                // together and says nothing about who started it.
                group.First().Attacker,
                group.First().Defender,
                group.Count(),
                group.Select(x => x.AttackerId).Distinct().Count() > 1))
            .Where(x => x.Rounds >= minimumRounds)
            .OrderByDescending(x => x.BothWays)
            .ThenByDescending(x => x.Rounds)
            .FirstOrDefault();

    /// <summary>How it reads in the news.</summary>
    public static string Describe(Feud feud)
        => feud.BothWays
            ? $"{feud.Rounds:N0} raids between them, and both have been on the receiving end."
            : $"{feud.Aggressor} has been through {feud.Victim}'s door {feud.Rounds:N0} times.";
}

/// <summary>One fight, flattened so the pick never touches the database.</summary>
public sealed record FeudRound(Guid AttackerId, Guid DefenderId, string Attacker, string Defender);

public sealed record Feud(string Aggressor, string Victim, int Rounds, bool BothWays);
