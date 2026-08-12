namespace StreetEmpire.Api.Models;

public sealed class CombatMissionEvent
{
    public long Id { get; set; }
    public long CombatMissionId { get; set; }
    public CombatMission CombatMission { get; set; } = null!;

    public int Round { get; set; }
    public string Kind { get; set; } = "Update";
    public string Summary { get; set; } = string.Empty;
    public double AttackRoll { get; set; }
    public double DefenseRoll { get; set; }
    public double AttackerMorale { get; set; }
    public double DefenderMorale { get; set; }
    public int AttackerThugsLost { get; set; }
    public int DefenderThugsLost { get; set; }
    public int AttackerWeaponsLost { get; set; }
    public int DefenderWeaponsLost { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
