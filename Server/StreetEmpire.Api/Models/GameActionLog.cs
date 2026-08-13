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
    /// <summary>
    /// Crew morale as it stood going into this action, so the dashboard can say which way morale is
    /// trending rather than only what it is right now.
    ///
    /// Recorded before rather than after on purpose. Taken after, the oldest row inside the trend
    /// window already contains the damage its own action did, so a player who crashes morale in one
    /// action and looks straight away is told it is steady.
    ///
    /// Nullable because rows written before this existed have no honest value to give: their morale is
    /// genuinely unknown, and stamping today's figure on them would invent a trend that never happened.
    /// </summary>
    public double? HoeMoraleBefore { get; set; }
    public double? ThugMoraleBefore { get; set; }

    public string Summary { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
