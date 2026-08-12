namespace StreetEmpire.Api.Models;

/// <summary>
/// A player's base. The tier caps crew, the storage room caps goods, the safe caps cash on hand,
/// and the labs raise what each production turn yields.
/// </summary>
public sealed class Hideout
{
    public long Id { get; set; }
    public Guid PlayerId { get; set; }
    public Player Player { get; set; } = null!;

    /// <summary>Tier 1 is the Trap House. Higher tiers are reserved for later versions.</summary>
    public int Tier { get; set; } = 1;

    public int StorageLevel { get; set; } = 1;
    public int SafeLevel { get; set; } = 1;

    /// <summary>Level 0 means the lab has not been built yet.</summary>
    public int WeedLabLevel { get; set; }
    public int CokeLabLevel { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
