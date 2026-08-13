namespace StreetEmpire.Api.Models;

public sealed class Player
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public PlayerAccount Account { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = "New York";

    // Money
    public long Cash { get; set; }
    public long BankCash { get; set; }

    // Turn bank
    public int Turns { get; set; }
    public DateTime LastTurnUpdateUtc { get; set; } = DateTime.UtcNow;

    // Crew
    public int Pimps { get; set; }
    public int Hoes { get; set; }
    public int Thugs { get; set; }
    public int HoeCutPercent { get; set; } = 30;
    public double HoeHappiness { get; set; } = 100;
    public double ThugHappiness { get; set; } = 100;

    // Inventory
    public int Condoms { get; set; }
    public int Beer { get; set; }
    public int Weapons { get; set; }
    public int Weed { get; set; }
    public int Coke { get; set; }

    // Combat pacing fields written by the attack flow.
    public DateTime? CombatProtectionUntilUtc { get; set; }
    public DateTime? LastAttackAtUtc { get; set; }
    public DateTime? LastAttackedAtUtc { get; set; }

    /// <summary>
    /// Watermark for defence alerts: anything that happened to this player after it is unread. A single
    /// column rather than a notifications table, because the events already exist in CombatLogs and only
    /// the read position is missing.
    /// </summary>
    public DateTime? CombatAlertsSeenAtUtc { get; set; }

    public Hideout? Hideout { get; set; }

    /// <summary>Named pimps, active and fallen. <see cref="Pimps"/> counts the active ones.</summary>
    public List<Pimp> Crew { get; set; } = [];

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<GameActionLog> ActionLogs { get; set; } = [];
    public List<CombatLog> AttacksMade { get; set; } = [];
    public List<CombatLog> Defenses { get; set; } = [];
    public List<CombatMission> MissionsStarted { get; set; } = [];
    public List<CombatMission> MissionsDefended { get; set; } = [];
}
