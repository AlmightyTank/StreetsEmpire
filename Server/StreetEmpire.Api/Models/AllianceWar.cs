namespace StreetEmpire.Api.Models;

/// <summary>
/// One crew declaring on another, for a fixed stretch of hours, with a pot on the table.
///
/// A crew was a reason to exist and no reason to act. Everything it carried was defensive or passive -
/// a truce nobody has to renew, a treasury that only pays out when somebody asks, pacts that create
/// non-aggression and never the other kind, and calls for help that only exist once somebody has
/// already been attacked. Two crews could sit beside each other for a month and never once have a
/// reason to do anything about it.
///
/// A war is the missing half: a clock, a score, and something that changes hands at the end of it.
/// Nothing about it suspends a single protection the rest of the game has - declaring war is exactly
/// the excuse a farmer would want, so the wealth floor, the ratio, the escalating shield and the
/// diminishing loot all still apply. What it changes is that the fights those rules already allow are
/// suddenly worth choosing.
/// </summary>
public sealed class AllianceWar
{
    public long Id { get; set; }

    /// <summary>Who declared, and who was declared on. The distinction outlives the fight: the stake is theirs.</summary>
    public long DeclaringAllianceId { get; set; }
    public Alliance DeclaringAlliance { get; set; } = null!;

    public long TargetAllianceId { get; set; }
    public Alliance TargetAlliance { get; set; } = null!;

    /// <summary>
    /// The member who declared it. Kept because the stake came out of a treasury on their say-so, and
    /// because a war needs one player to hang its public record on - crews do not write to the news
    /// feed, people do.
    /// </summary>
    public Guid DeclaredById { get; set; }
    public Player DeclaredBy { get; set; } = null!;

    public string Status { get; set; } = AllianceWarStatuses.Active;

    /// <summary>
    /// What the declaring crew put up. Taken out of their treasury the moment war is declared, so
    /// declaring is a decision somebody's crew can feel rather than a free insult, and it is the pot
    /// the winner takes at the end - including the crew that never asked for the fight.
    /// </summary>
    public long Stake { get; set; }

    public int DeclaringScore { get; set; }
    public int TargetScore { get; set; }

    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime EndsAtUtc { get; set; }
    public DateTime? SettledAtUtc { get; set; }

    /// <summary>
    /// Null on a war nobody won: level scores, or neither crew doing enough to count. A war that was
    /// declared and never fought has to be able to end in nothing, or declaring on a crew that has
    /// stopped playing would be a way of drawing a wage.
    /// </summary>
    public long? WinnerAllianceId { get; set; }

    /// <summary>What actually left the loser's treasury, over and above the stake.</summary>
    public long Tribute { get; set; }

    /// <summary>The one sentence both crews are told when it ends.</summary>
    public string? Outcome { get; set; }

    public bool IsBetween(long oneAllianceId, long otherAllianceId)
        => (DeclaringAllianceId == oneAllianceId && TargetAllianceId == otherAllianceId)
           || (DeclaringAllianceId == otherAllianceId && TargetAllianceId == oneAllianceId);

    /// <summary>The other crew, from the point of view of one of the two in it.</summary>
    public long OpponentOf(long allianceId)
        => allianceId == DeclaringAllianceId ? TargetAllianceId : DeclaringAllianceId;

    public int ScoreOf(long allianceId)
        => allianceId == DeclaringAllianceId ? DeclaringScore : TargetScore;
}

public static class AllianceWarStatuses
{
    public const string Active = "Active";
    public const string Settled = "Settled";
}
