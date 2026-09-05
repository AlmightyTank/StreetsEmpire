namespace StreetEmpire.Api.Models;

/// <summary>
/// A strike thrown at a house in another town, while it is still on the road.
///
/// Strikes are the instant verb and that is the whole point of them - but instant only makes sense
/// against somebody down the street. Now that a hideout has a town, hitting one on the other side of
/// the country is a drive, and a drive takes time. This is the row that time lives in.
///
/// Everything the outcome depends on that belongs to the attacker is written down here at launch, for
/// the reason <see cref="MuleRun"/> gives: a crew already on the road must not be re-equipped by
/// somebody sitting at home buying poison. The defender is deliberately <em>not</em> frozen. What is
/// standing in that house when the car pulls up is what the crew meet, which is the entire reason a
/// warning is worth anything.
///
/// A same-town strike never creates one of these. It still resolves on the spot, which is the
/// asymmetry the whole mechanic rests on: near neighbours cannot be answered, distant ones can.
/// </summary>
public sealed class PendingStrike
{
    public long Id { get; set; }

    public Guid AttackerId { get; set; }
    public Player Attacker { get; set; } = null!;

    public Guid DefenderId { get; set; }
    public Player Defender { get; set; } = null!;

    /// <summary>One of <see cref="AttackMethods.Strikes"/>. A raid is a mission and never this.</summary>
    public string Method { get; set; } = AttackMethods.DriveBy;

    /// <summary>The house it left, and the house it is going to. Both hideout towns, never player towns.</summary>
    public string OriginCity { get; set; } = string.Empty;
    public string TargetCity { get; set; } = string.Empty;

    /// <summary>
    /// How far it is, in the same travel turns everything else on this map is measured in. Written down
    /// rather than recomputed so that re-tuning a town's distance cannot change a trip already in the
    /// air.
    /// </summary>
    public int TravelTurns { get; set; }

    public DateTime LaunchedAtUtc { get; set; }

    /// <summary>When the crew reach the target and the strike actually happens.</summary>
    public DateTime ArrivesAtUtc { get; set; }

    /// <summary>
    /// When they are back at their own door with whatever survived.
    ///
    /// A second settlement rather than one, because a jacking that put stolen cars in your garage the
    /// instant it landed would be the teleport the whole location split exists to close. What was taken
    /// has to be driven home, and until it is, it is neither here nor there.
    /// </summary>
    public DateTime ReturnsAtUtc { get; set; }

    public string Status { get; set; } = PendingStrikeStatus.Outbound;

    /// <summary>What was paid to put it on the road. Kept for the log and for a refund that is not owed.</summary>
    public int TurnsSpent { get; set; }
    public long Fare { get; set; }

    /// <summary>
    /// The odds the drive home goes wrong, as a percentage, frozen at launch like everything else here.
    ///
    /// Written down rather than worked out at the door for the reason a mule run writes its own down:
    /// a trip already on the road must not be re-priced by somebody re-tuning a table while it drives.
    /// It is also the number the attacker was shown before they committed, and being judged by a
    /// different one than you were quoted is the worst thing a risk can do.
    ///
    /// It reads on the haul only. What a crew left with is theirs and comes back; what they took is
    /// hot, and hot is what gets stopped.
    /// </summary>
    public double ReturnRiskPercent { get; set; }

    /// <summary>What the road took off them on the way back, in whatever they were carrying.</summary>
    public int SeizedRides { get; set; }
    public int SeizedHoes { get; set; }

    // What went with them, taken off the attacker at launch and handed back to the crew on arrival.
    // A car for a drive-by, doses for an infestation, product for a poach; a jacking spends nothing to
    // throw, which is why there is no column for it.
    public int CommittedRides { get; set; }
    public int CommittedPoison { get; set; }
    public int CommittedCoke { get; set; }
    public double CommittedCokePurity { get; set; } = 1;

    // What is coming back, written at arrival and delivered at the door. Separate from the committed
    // columns because they are different questions: one is what a crew left with, the other is what
    // they still have.
    public int ReturningRides { get; set; }
    public int ReturningPoison { get; set; }
    public int ReturningCoke { get; set; }
    public double ReturningCokePurity { get; set; } = 1;
    public int ReturningHoes { get; set; }

    /// <summary>Victory, Defeat, or one of the ways a trip can come to nothing. Null until it lands.</summary>
    public string? Outcome { get; set; }

    /// <summary>What the attacker is told when they next look. Written at each settlement.</summary>
    public string Summary { get; set; } = string.Empty;

    public DateTime? ResolvedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    /// <summary>Still on the road towards somebody. The state a lookout can see.</summary>
    public bool IsInbound => Status == PendingStrikeStatus.Outbound;

    /// <summary>Out of the attacker's hands one way or another, and not yet back.</summary>
    public bool IsOut => Status is PendingStrikeStatus.Outbound or PendingStrikeStatus.Returning;
}

public static class PendingStrikeStatus
{
    /// <summary>On the way there. Nothing has happened to anybody yet.</summary>
    public const string Outbound = "Outbound";

    /// <summary>It happened, and the crew are driving home with whatever they still have.</summary>
    public const string Returning = "Returning";

    /// <summary>Home. The row is history from here.</summary>
    public const string Done = "Done";
}
