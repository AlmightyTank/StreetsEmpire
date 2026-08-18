namespace StreetEmpire.Api.Models;

/// <summary>
/// A crew of players who have agreed not to rob each other.
///
/// That is the whole of what an alliance is for, and everything else it carries follows from it. The
/// source game enforced the interesting half of this socially - "don't form super alliances, it's
/// against the rules" - which is a rule that only works while somebody is reading the message board.
/// Here it is mechanical: members cannot attack each other by any method, and that immunity is the
/// thing being bought with dues.
///
/// The treasury and the thug pool are shared and finite on purpose. A bonus that simply applied to
/// every member would make an alliance a switch you flip once, and the more members the better; a pot
/// that empties when somebody uses it makes joining one an arrangement between people who have to
/// decide together what it is spent on.
/// </summary>
public sealed class Alliance
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>What the founder wants said on the recruitment board.</summary>
    public string? Motto { get; set; }

    /// <summary>
    /// Whoever founded it. Kept as a plain id rather than a navigation both ways: the founder is also a
    /// member, and a required relationship in both directions makes deleting either one a puzzle.
    /// </summary>
    public Guid FounderId { get; set; }

    /// <summary>
    /// The cut of every member's shift that goes to the treasury. Set by the founder, which is the whole
    /// of their authority over anybody else's money - they cannot reach into a member's cash, only take
    /// a share of what a shift grosses before it lands.
    /// </summary>
    public int DuesPercent { get; set; } = 5;

    public long Treasury { get; set; }

    /// <summary>
    /// The shared thug pool. Offensive thugs ride along on a member's raid; defensive ones are posted to
    /// a member's house and stand in it until they are released or killed. Both are finite, which is
    /// what makes spending them a decision rather than a formality.
    /// </summary>
    public int OffensiveThugs { get; set; }
    public int DefensiveThugs { get; set; }

    /// <summary>
    /// How this crew takes people on. Every crew still appears on the board whatever it is set to -
    /// being able to see who is winning and not being able to join them is most of what makes a board
    /// worth reading.
    /// </summary>
    public AllianceDoor Door { get; set; } = AllianceDoor.Open;

    /// <summary>
    /// The rank each power needs, set by the boss.
    ///
    /// This is the part of a rank system that actually gets used. Ranks on their own are decoration -
    /// a list of words next to names - and what makes two crews with identical ranks run completely
    /// differently is where their boss drew these lines. One hands the door to anybody who has been
    /// around a week; another keeps every one of them at the top and runs the crew personally.
    ///
    /// Stored as the underlying number so the whole set can be read and written without four separate
    /// conversions, and so a rank added later does not need a migration to be selectable here.
    /// </summary>
    public int MinRankToInvite { get; set; } = (int)AllianceRank.Enforcer;
    public int MinRankToExpel { get; set; } = (int)AllianceRank.Underboss;
    public int MinRankToSpendTreasury { get; set; } = (int)AllianceRank.Underboss;
    public int MinRankToBorrow { get; set; } = (int)AllianceRank.Enforcer;
    public int MinRankToPostDefenders { get; set; } = (int)AllianceRank.Soldier;

    public List<AllianceRequest> Requests { get; set; } = [];

    /// <summary>The rank a power needs here.</summary>
    public AllianceRank MinRankFor(AlliancePower power) => (AllianceRank)(power switch
    {
        AlliancePower.Invite => MinRankToInvite,
        AlliancePower.Expel => MinRankToExpel,
        AlliancePower.SpendTreasury => MinRankToSpendTreasury,
        AlliancePower.Borrow => MinRankToBorrow,
        _ => MinRankToPostDefenders
    });

    public void SetMinRankFor(AlliancePower power, AllianceRank rank)
    {
        switch (power)
        {
            case AlliancePower.Invite: MinRankToInvite = (int)rank; break;
            case AlliancePower.Expel: MinRankToExpel = (int)rank; break;
            case AlliancePower.SpendTreasury: MinRankToSpendTreasury = (int)rank; break;
            case AlliancePower.Borrow: MinRankToBorrow = (int)rank; break;
            default: MinRankToPostDefenders = (int)rank; break;
        }
    }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<Player> Members { get; set; } = [];
}
