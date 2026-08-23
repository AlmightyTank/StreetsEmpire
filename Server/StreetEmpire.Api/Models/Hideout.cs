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
    /// The bench. Guns, moonshine, cut and poison all come off it, and the level buys how fast it works
    /// and how far up the list it reaches.
    ///
    /// There were three of these - a workshop, a still and a mix house - which were the same room with
    /// different signs on the door, and two of them dead-ended at the second building with two levels
    /// each. What a thing costs to make belongs to the thing now, so the room is just the room.
    /// </summary>
    public int WorkshopLevel { get; set; }

    /// <summary>
    /// Eyes on the street. The only answer a first-tier player has to heat besides selling everything
    /// and waiting: it does not stop them noticing you, it buys the warning that keeps the door shut.
    /// </summary>
    public int LookoutLevel { get; set; }

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
