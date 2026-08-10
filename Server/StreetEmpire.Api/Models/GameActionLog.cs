namespace StreetEmpire.Api.Models;

public sealed class GameActionLog
{
    public long Id { get; set; }
    public Guid PlayerId { get; set; }
    public Player Player { get; set; } = null!;
    public string Action { get; set; } = string.Empty;
    public int TurnsSpent { get; set; }
    public long CashDelta { get; set; }
    public long BankDelta { get; set; }
    public int PimpsDelta { get; set; }
    public int HoesDelta { get; set; }
    public int ThugsDelta { get; set; }
    public int CondomsDelta { get; set; }
    public int BeerDelta { get; set; }
    public int WeaponsDelta { get; set; }
    public int WeedDelta { get; set; }
    public int CokeDelta { get; set; }
    public string Summary { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
