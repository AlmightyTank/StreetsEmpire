namespace StreetEmpire.Api.Models;

/// <summary>
/// One workshop order on the bench. Only one may be active for a player at a time; it is paid for up
/// front and delivered by the clock when its finish time passes.
/// </summary>
public sealed class WorkshopCraft
{
    public long Id { get; set; }
    public Guid PlayerId { get; set; }
    public Player Player { get; set; } = null!;

    public string Good { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public long UnitCost { get; set; }
    public long TotalCost { get; set; }
    public int WorkUnits { get; set; }
    public int WorkshopLevel { get; set; }

    public DateTime StartedAtUtc { get; set; }
    public DateTime CompletesAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    public int Delivered { get; set; }
    public int Spilled { get; set; }
    public string Summary { get; set; } = string.Empty;

    public bool IsActive => CompletedAtUtc is null;
}
