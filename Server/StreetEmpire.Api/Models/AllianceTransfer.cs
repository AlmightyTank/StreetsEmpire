namespace StreetEmpire.Api.Models;

public sealed class AllianceTransfer
{
    public long Id { get; set; }

    public long AllianceId { get; set; }
    public Alliance Alliance { get; set; } = null!;

    public Guid FromPlayerId { get; set; }
    public Player FromPlayer { get; set; } = null!;

    public Guid ToPlayerId { get; set; }
    public Player ToPlayer { get; set; } = null!;

    public string Item { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
