namespace StreetEmpire.Api.Models;

public sealed class CombatMission
{
    public long Id { get; set; }
    public Guid AttackerId { get; set; }
    public Player Attacker { get; set; } = null!;
    public Guid DefenderId { get; set; }
    public Player Defender { get; set; } = null!;

    public string Status { get; set; } = "Traveling";
    public string Outcome { get; set; } = "Pending";
    public string Summary { get; set; } = string.Empty;

    public int TurnsSpent { get; set; }
    public int AssignedPimps { get; set; }

    /// <summary>The pimp commanding this attack, kept so the mission can name them.</summary>
    public long? CommanderPimpId { get; set; }
    public Pimp? CommanderPimp { get; set; }
    public string? CommanderName { get; set; }

    /// <summary>Frozen at launch, so a commander dying mid-mission does not change the fight.</summary>
    public int CommanderBonusPercent { get; set; }
    public int AssignedThugs { get; set; }
    public int AssignedWeapons { get; set; }
    public int RemainingAttackers { get; set; }
    public int RemainingWeapons { get; set; }

    public double AttackerMorale { get; set; }
    public double DefenderMorale { get; set; }
    public int CurrentRound { get; set; }
    public int MaxRounds { get; set; }

    public int AttackerPower { get; set; }
    public int DefenderPower { get; set; }
    public long CashStolen { get; set; }
    public int WeedStolen { get; set; }
    public int CokeStolen { get; set; }
    public int AttackerPimpsLost { get; set; }
    public int AttackerHoesLost { get; set; }
    public int AttackerThugsLost { get; set; }
    public int AttackerWeaponsLost { get; set; }
    public int DefenderPimpsLost { get; set; }
    public int DefenderHoesLost { get; set; }
    public int DefenderThugsLost { get; set; }
    public int DefenderWeaponsLost { get; set; }

    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ArrivesAtUtc { get; set; }
    public DateTime? NextRoundAtUtc { get; set; }
    public DateTime? ReturnsAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? DefenderProtectionUntilUtc { get; set; }

    public List<CombatMissionEvent> Events { get; set; } = [];
}
