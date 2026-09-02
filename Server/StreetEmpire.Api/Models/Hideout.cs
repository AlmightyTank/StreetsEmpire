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
    /// When each room was put out of action, or null for one that is standing.
    ///
    /// A timestamp rather than a flag because "wrecked" is a thing that happened at a moment and the
    /// player was almost certainly not there for it: the hideout page can say the coke lab has been
    /// down since Tuesday, and the repair bill is arguing with a date rather than with a boolean.
    ///
    /// The level itself is deliberately left alone. What a player paid for is still theirs - the
    /// building is still worth it on the board, the ladder still remembers where they were, and
    /// fixing the room hands back exactly the level that was taken away. A raid that knocked levels
    /// off would be a raid that can un-buy an upgrade, and there is no honest price for that.
    /// </summary>
    public DateTime? WeedLabWreckedAtUtc { get; set; }
    public DateTime? CokeLabWreckedAtUtc { get; set; }
    public DateTime? WorkshopWreckedAtUtc { get; set; }
    public DateTime? LookoutWreckedAtUtc { get; set; }
    public DateTime? IntelligenceWreckedAtUtc { get; set; }

    /// <summary>
    /// The room the builders are in right now, and when they are done. Set together or not at all,
    /// exactly like a tier build.
    ///
    /// One repair at a time, on purpose, and it is the whole reason damage is worth having. Three
    /// wrecked rooms and one crew is a question - the labs that make the money, the lookout that
    /// stops it happening again, or the centre that gets the mules moving - and a player who could
    /// pay for all three at once on a Tuesday evening would never have to answer it.
    /// </summary>
    public string? RepairingRoom { get; set; }
    public DateTime? RepairCompletesAtUtc { get; set; }

    /// <summary>
    /// When passive lab output was last banked. Null means the labs have never run, and accrual starts
    /// from the moment the first one is built rather than from the hideout's creation.
    /// </summary>
    public DateTime? LabsCollectedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>The level on the deeds: what was bought, whether or not it is standing today.</summary>
    public int BuiltLevel(string room) => room switch
    {
        HideoutRooms.Storage => StorageLevel,
        HideoutRooms.Safe => SafeLevel,
        HideoutRooms.WeedLab => WeedLabLevel,
        HideoutRooms.CokeLab => CokeLabLevel,
        HideoutRooms.Workshop => WorkshopLevel,
        HideoutRooms.Lookout => LookoutLevel,
        HideoutRooms.Intelligence => IntelligenceLevel,
        _ => 0
    };

    public DateTime? WreckedAtUtc(string room) => room switch
    {
        HideoutRooms.WeedLab => WeedLabWreckedAtUtc,
        HideoutRooms.CokeLab => CokeLabWreckedAtUtc,
        HideoutRooms.Workshop => WorkshopWreckedAtUtc,
        HideoutRooms.Lookout => LookoutWreckedAtUtc,
        HideoutRooms.Intelligence => IntelligenceWreckedAtUtc,
        _ => null
    };

    public void SetWrecked(string room, DateTime? whenUtc)
    {
        switch (room)
        {
            case HideoutRooms.WeedLab: WeedLabWreckedAtUtc = whenUtc; break;
            case HideoutRooms.CokeLab: CokeLabWreckedAtUtc = whenUtc; break;
            case HideoutRooms.Workshop: WorkshopWreckedAtUtc = whenUtc; break;
            case HideoutRooms.Lookout: LookoutWreckedAtUtc = whenUtc; break;
            case HideoutRooms.Intelligence: IntelligenceWreckedAtUtc = whenUtc; break;
        }
    }

    public bool IsWrecked(string room) => WreckedAtUtc(room) is not null;

    /// <summary>
    /// The level the room actually runs at, which is nothing at all while it is down.
    ///
    /// Every rule that asks what a room does asks this one, and every rule that asks what a player
    /// owns asks <see cref="BuiltLevel"/>. Keeping the two questions apart in the names is what stops
    /// a wrecked lab from quietly costing somebody their place on the leaderboard, or a repaired one
    /// from having to be bought again.
    /// </summary>
    public int WorkingLevel(string room) => IsWrecked(room) ? 0 : BuiltLevel(room);

    /// <summary>Everything that is down, in the order the rooms are listed. A method so EF leaves it alone.</summary>
    public IReadOnlyList<string> WreckedRooms()
        => HideoutRooms.Breakable.Where(IsWrecked).ToList();
}
