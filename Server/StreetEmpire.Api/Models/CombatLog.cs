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

    /// <summary>
    /// Which of the attack methods this was: a raid, or one of the quick strikes. Stored rather than
    /// inferred from what was taken, because a strike that failed took nothing and would otherwise be
    /// indistinguishable from any other loss in the history and the defence alerts.
    /// </summary>
    public string Method { get; set; } = AttackMethods.Raid;

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

    /// <summary>
    /// Crew and rides that changed hands rather than simply died. A poached hoe walks into someone
    /// else's house and a jacked ride is parked in someone else's garage, which is a different event
    /// from the same hoe catching a disease, even though both read as a loss to the defender.
    /// </summary>
    public int HoesTaken { get; set; }
    public int RidesTaken { get; set; }

    public DateTime? DefenderProtectionUntilUtc { get; set; }
    public DateTime? ResolvesAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
