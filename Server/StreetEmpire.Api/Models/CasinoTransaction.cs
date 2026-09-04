namespace StreetEmpire.Api.Models;

public sealed class CasinoTransaction
{
    public long Id { get; set; }
    public Guid PlayerId { get; set; }
    public Player Player { get; set; } = null!;
    public string GameType { get; set; } = "slots";
    public string MachineKey { get; set; } = string.Empty;
    public int Paylines { get; set; } = 1;
    public int WinningPaylines { get; set; }
    public long BetAmount { get; set; }
    public long PayoutAmount { get; set; }
    public long NetResult { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
