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

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<GameActionLog> ActionLogs { get; set; } = [];
}
