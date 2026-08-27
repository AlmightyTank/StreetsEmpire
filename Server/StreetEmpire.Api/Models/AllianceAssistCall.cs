namespace StreetEmpire.Api.Models;

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

    public Guid? RespondedById { get; set; }
    public Player? RespondedBy { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAtUtc { get; set; }
}

public static class AllianceAssistStatuses
{
    public const string Open = "Open";
    public const string Answered = "Answered";
    public const string Closed = "Closed";
}
