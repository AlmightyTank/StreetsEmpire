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

    /// <summary>Tier 1 is the Trap House. Each tier above raises crew caps and unlocks deeper rooms.</summary>
    public int Tier { get; set; } = 1;

    /// <summary>
    /// The tier being built, and when it lands. Set together or not at all: a build is paid for up
    /// front but the old caps hold until it finishes, so nobody buys their way past a cap instantly.
    /// </summary>
    public int? UpgradingToTier { get; set; }
    public DateTime? UpgradeCompletesAtUtc { get; set; }

    public int StorageLevel { get; set; } = 1;
    public int SafeLevel { get; set; } = 1;

    /// <summary>Level 0 means the lab has not been built yet.</summary>
    public int WeedLabLevel { get; set; }
    public int CokeLabLevel { get; set; }

    /// <summary>
    /// Turns cash and turns into weapons. The one thing a player can make that everybody needs and
    /// nobody else can undercut, which is what gives the market something worth trading.
    /// </summary>
    public int WorkshopLevel { get; set; }

    /// <summary>Brews moonshine. Illegal to hold, so running one is a standing risk.</summary>
    public int StillLevel { get; set; }

    /// <summary>Mixes cut, which stretches coke rather than being worth anything itself.</summary>
    public int MixLevel { get; set; }

    /// <summary>
    /// Runs the routes. Unlike the other stations it makes nothing: it decides how many mule runs can
    /// be out at once, and how well briefed they are when they go. A room that buys capacity rather
    /// than output, which is what stops mule running from being free once you can afford one pimp.
    /// </summary>
    public int IntelligenceLevel { get; set; }

    /// <summary>
    /// When passive lab output was last banked. Null means the labs have never run, and accrual starts
    /// from the moment the first one is built rather than from the hideout's creation.
    /// </summary>
    public DateTime? LabsCollectedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
