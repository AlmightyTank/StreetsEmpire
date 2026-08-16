namespace StreetEmpire.Api.Models;

/// <summary>
/// Goods one player is offering to another.
///
/// The stock leaves the seller's storage the moment it is listed and lives here until it sells or the
/// listing is pulled. Escrow rather than a promise: otherwise the same fifty weapons could be listed
/// twice, or sold after the seller had already spent them.
/// </summary>
public sealed class MarketListing
{
    public long Id { get; set; }

    public Guid SellerId { get; set; }
    public Player Seller { get; set; } = null!;

    /// <summary>Matches the store and inventory keys: condoms, beer, weapons, weed, coke.</summary>
    public string Item { get; set; } = string.Empty;

    /// <summary>What is still for sale. A listing is closed when this reaches zero.</summary>
    public int Quantity { get; set; }

    /// <summary>What the seller originally put up, so a part-sold listing still reads honestly.</summary>
    public int OriginalQuantity { get; set; }

    public long PricePerUnit { get; set; }

    /// <summary>
    /// The purity of the coke in this listing, frozen when it went up. Carried on the listing rather
    /// than read from the seller, because otherwise the board launders: stretch a pile to a tenth,
    /// list it, and the buyer receives clean product while the seller keeps the difference. What was
    /// escrowed is what gets delivered. Ignored for every other good, which is interchangeable.
    /// </summary>
    public double Purity { get; set; } = 1;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Set when the seller pulls it. Sold-out listings simply have no quantity left.</summary>
    public DateTime? CancelledAtUtc { get; set; }

    public bool IsOpen => CancelledAtUtc is null && Quantity > 0;
}
