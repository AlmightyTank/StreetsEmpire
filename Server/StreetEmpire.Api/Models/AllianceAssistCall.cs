namespace StreetEmpire.Api.Models;

/// <summary>
/// One crew asking another for help with a fight, and what came of it.
///
/// Raised automatically for every crew the defender holds an active pact with, the moment a raid is
/// launched at one of their members. Nobody is obliged to answer; a call that is never answered simply
/// closes when the fight does.
///
/// What an ally sends genuinely leaves them and lands with the defender, because the fight reads the
/// defender's own numbers and nothing else. That is also why the sending is recorded rather than only
/// the fact of it: once the thugs are in the defender's pile they are indistinguishable from their own,
/// and this row is the only account of whose they were.
/// </summary>
public sealed class AllianceAssistCall
{
    public long Id { get; set; }

    public long CombatMissionId { get; set; }
    public CombatMission CombatMission { get; set; } = null!;

    public long DefenderAllianceId { get; set; }
    public Alliance DefenderAlliance { get; set; } = null!;

    public long AllyAllianceId { get; set; }
    public Alliance AllyAlliance { get; set; } = null!;

    public string Status { get; set; } = AllianceAssistStatuses.Open;
    public int ThugsSent { get; set; }
    public int PistolsSent { get; set; }
    public int ShotgunsSent { get; set; }
    public int SmgsSent { get; set; }
    public int RiflesSent { get; set; }

    /// <summary>
    /// What came home, once the fight was over and the ally asked for it back.
    ///
    /// Never more than was sent, and never more than the defender still has standing free - some of it
    /// will have died in the fight, and what died is not owed back by anybody. Recording it separately
    /// from what was sent is what makes the difference between the two readable: a crew that sent ten
    /// and got six back can see the four it cost them.
    /// </summary>
    public int ThugsReturned { get; set; }
    public int PistolsReturned { get; set; }
    public int ShotgunsReturned { get; set; }
    public int SmgsReturned { get; set; }
    public int RiflesReturned { get; set; }

    public DateTime? RecalledAtUtc { get; set; }

    public Guid? RespondedById { get; set; }
    public Player? RespondedBy { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAtUtc { get; set; }
}

public static class AllianceAssistStatuses
{
    /// <summary>Raised, and nobody has sent anything yet. Only an open call can be answered.</summary>
    public const string Open = "Open";

    /// <summary>Somebody sent help. It is with the defender, and can be asked for back once the fight ends.</summary>
    public const string Answered = "Answered";

    /// <summary>
    /// Finished with, and the one state that means the row can be swept.
    ///
    /// Two ways in: an open call closes on its own when the fight ends, because help that arrives after
    /// the shooting is not help; an answered one closes when the ally takes back what is left. Nothing
    /// was ever setting this before, so every call ever raised stayed on the alliance page for good.
    /// </summary>
    public const string Closed = "Closed";
}
