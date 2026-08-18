namespace StreetEmpire.Api.Models;

/// <summary>
/// Where somebody stands in a crew.
///
/// Four rungs rather than the seven or eight a clan system in a game with thousands of players carries,
/// for the same reason a crew holds six and not twenty: ranks only mean anything if there are enough
/// people for the gaps between them to matter. Four gives a crew of six a boss, a deputy, somebody
/// trusted, and everybody else - which is as much structure as six people have ever needed.
///
/// Ordered, and the order is the whole mechanism. Every rule about who may do what to whom is either
/// "your rank is at least X" or "your rank is above theirs", and both fall straight out of comparing
/// two of these.
/// </summary>
public enum AllianceRank
{
    /// <summary>Where everybody starts. Pays dues, gets the truce, decides nothing.</summary>
    Soldier = 0,

    /// <summary>Trusted with the door and with the crew's men.</summary>
    Enforcer = 1,

    /// <summary>Runs the place when the boss is not looking at it.</summary>
    Underboss = 2,

    /// <summary>Exactly one per crew, and the only rank that can hand itself on.</summary>
    Boss = 3
}

/// <summary>
/// The things a crew can do that not everybody should be able to do.
///
/// Kept as a named set rather than as loose booleans because the boss configures a minimum rank for
/// each one, which is the part of a clan system that actually gets used: two crews with the same ranks
/// and different thresholds run completely differently, and neither of them had to be programmed.
/// </summary>
public enum AlliancePower
{
    /// <summary>Bring somebody in, and answer the people asking to be brought in.</summary>
    Invite,

    /// <summary>Throw somebody out. Never somebody at or above your own rank.</summary>
    Expel,

    /// <summary>Turn the treasury into thugs.</summary>
    SpendTreasury,

    /// <summary>Take offensive thugs out of the pool on a raid.</summary>
    Borrow,

    /// <summary>Stand defensive thugs at your own place.</summary>
    PostDefenders
}

public static class AllianceRanks
{
    public static readonly AllianceRank[] All = [AllianceRank.Soldier, AllianceRank.Enforcer, AllianceRank.Underboss, AllianceRank.Boss];

    public static string Label(AllianceRank rank) => rank switch
    {
        AllianceRank.Boss => "Boss",
        AllianceRank.Underboss => "Underboss",
        AllianceRank.Enforcer => "Enforcer",
        _ => "Soldier"
    };

    /// <summary>
    /// Reads a rank off the wire. An unknown value is a Soldier rather than an error: the safe failure
    /// for a rank is the one that can do the least.
    /// </summary>
    public static AllianceRank Parse(string? value)
        => Enum.TryParse<AllianceRank>(value?.Trim(), ignoreCase: true, out var rank) && All.Contains(rank)
            ? rank
            : AllianceRank.Soldier;

    /// <summary>
    /// Whether one rank may act on another. Strictly above, never equal: two Underbosses throwing each
    /// other out in turn is not a chain of command, it is a fight.
    /// </summary>
    public static bool Outranks(AllianceRank actor, AllianceRank subject) => actor > subject;
}
