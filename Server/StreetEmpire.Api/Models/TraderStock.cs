namespace StreetEmpire.Api.Models;

/// <summary>
/// How many of one line one town's counter has left, and which delivery it belongs to.
///
/// The only part of a shelf that is written down. What a trader carries is rolled from the town, the
/// line and the date - the same answer for everybody, needing no row - but a count has to be stored,
/// because the whole point of it is that buying takes it away.
///
/// One row per line per town, replaced rather than added to when a delivery comes: a line nobody touched
/// all week should be a full shelf, not a warehouse.
/// </summary>
public sealed class TraderStock
{
    public long Id { get; set; }

    public string City { get; set; } = string.Empty;
    public string Good { get; set; } = string.Empty;

    /// <summary>The delivery this count belongs to. Older than the current one means it is stale.</summary>
    public DateTime WindowStartUtc { get; set; }

    public int Remaining { get; set; }
}
