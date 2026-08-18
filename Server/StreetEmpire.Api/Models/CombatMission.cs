namespace StreetEmpire.Api.Models;

public sealed class CombatMission
{
    public long Id { get; set; }
    public Guid AttackerId { get; set; }
    public Player Attacker { get; set; } = null!;
    public Guid DefenderId { get; set; }

    /// <summary>
    /// Set when the raid is for ground rather than a house. The holder is still the defender, so this
    /// does not make DefenderId nullable: unheld ground is claimed without a fight, never raided.
    /// </summary>
    public long? TerritoryId { get; set; }
    public Territory? Territory { get; set; }
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

    /// <summary>
    /// Guns still out with this crew, counted whole. Kept as a column rather than derived from the four
    /// below because the commitment query and the defender's home-crew arithmetic both sum it across
    /// every live mission, and doing that over four columns would cost four sums to answer one question.
    /// It is maintained in step with them: <see cref="CarriedRifles"/> and friends are the same guns.
    /// </summary>
    public int RemainingWeapons { get; set; }

    /// <summary>
    /// Which guns went, and which are still out. A crew arms itself from the best of the rack, so a raid
    /// is not "twenty weapons" - it is four rifles and sixteen pistols, and losing five of them has to
    /// take the right five off the right shelves back home.
    ///
    /// Decremented as the fight goes, so these are the survivors rather than the manifest; the manifest
    /// is <see cref="AssignedWeapons"/>, frozen at launch.
    /// </summary>
    public int CarriedPistols { get; set; }
    public int CarriedShotguns { get; set; }
    public int CarriedSmgs { get; set; }
    public int CarriedRifles { get; set; }

    /// <summary>
    /// Borrowed thugs out with this crew, and how many of them did not come back.
    ///
    /// Counted apart from the attacker's own because they belong to somebody else: what survives goes
    /// back to the pool when the raid comes home, and what dies is gone from it for good. Folded into
    /// RemainingAttackers they would return as the attacker's own men, which is a way of stealing from
    /// the people you agreed not to steal from.
    /// </summary>
    public int AllianceThugs { get; set; }
    public int AllianceThugsLost { get; set; }

    /// <summary>The guns still out, as one value.</summary>
    public Armoury Carried
    {
        get => new(CarriedPistols, CarriedShotguns, CarriedSmgs, CarriedRifles);
        set
        {
            CarriedPistols = Math.Max(0, value.Pistols);
            CarriedShotguns = Math.Max(0, value.Shotguns);
            CarriedSmgs = Math.Max(0, value.Smgs);
            CarriedRifles = Math.Max(0, value.Rifles);
            RemainingWeapons = value.Total;
        }
    }

    public double AttackerMorale { get; set; }
    public double DefenderMorale { get; set; }
    public int CurrentRound { get; set; }
    public int MaxRounds { get; set; }

    public int AttackerPower { get; set; }
    public int DefenderPower { get; set; }
    /// <summary>Anti-farm share of the haul this mission earned, as a percent. 100 means a first hit.</summary>
    public int LootMultiplierPercent { get; set; } = 100;

    /// <summary>Hits the defender had taken in the window when this mission landed.</summary>
    public int DefenderRecentHits { get; set; }

    /// <summary>Minutes of protection the defender earned, after escalation.</summary>
    public int DefenderProtectionMinutes { get; set; }

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
