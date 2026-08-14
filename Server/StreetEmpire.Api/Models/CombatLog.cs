namespace StreetEmpire.Api.Models;

public sealed class CombatLog
{
    public long Id { get; set; }
    public Guid AttackerId { get; set; }
    public Player Attacker { get; set; } = null!;
    public Guid DefenderId { get; set; }
    public Player Defender { get; set; } = null!;

    /// <summary>
    /// Set when the fight was over ground rather than a house. Without it a raid on a corner is
    /// indistinguishable from a raid on the defender's home, so the arrival summary counted one fight
    /// twice and called it an attack on their house.
    /// </summary>
    public long? TerritoryId { get; set; }

    public string Outcome { get; set; } = "Prepared";
    public string Summary { get; set; } = string.Empty;
    public int TurnsSpent { get; set; }
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
    public DateTime? DefenderProtectionUntilUtc { get; set; }
    public DateTime? ResolvesAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
