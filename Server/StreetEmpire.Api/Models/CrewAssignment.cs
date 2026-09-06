namespace StreetEmpire.Api.Models;

/// <summary>
/// Where one of the player's people actually is.
///
/// Crew used to exist wherever the player was standing, which was fine for exactly as long as those
/// were the same place. Now that a player can be in Las Vegas while their house is in New York, "my
/// thugs" is a question that needs an answer, and the honest answer is different for each of them: the
/// ones on the door are at the house, the ones on a corner are on that corner, and the ones on a mule
/// run are somewhere over Nebraska.
///
/// Only <see cref="Pimp"/> carries this today, because pimps are the only crew tracked individually -
/// hoes and thugs are counts, and a count cannot be in two places. The values that a count cannot yet
/// express are here anyway rather than being added one at a time later, because they are what the rest
/// of the design has to be shaped around: garrisons, mule teams and raid parties already exist as
/// separate rows elsewhere, and this is the vocabulary that will let them be read as one roster.
/// </summary>
public static class CrewAssignment
{
    /// <summary>On the payroll, at the house, available. The default and the fallback.</summary>
    public const string AtHideout = "hideout";

    /// <summary>Standing on a piece of ground the player holds.</summary>
    public const string Garrison = "garrison";

    /// <summary>Out on a raid, and not back yet.</summary>
    public const string Raid = "raid";

    /// <summary>Running a load between towns.</summary>
    public const string MuleRun = "mule";

    /// <summary>Between two towns for any other reason.</summary>
    public const string Travelling = "travelling";

    /// <summary>Hurt, and no use to anybody until they are not.</summary>
    public const string Injured = "injured";

    /// <summary>Inside. Still gettable - see <see cref="Pimp.JailedAtUtc"/>.</summary>
    public const string Jail = "jail";

    /// <summary>On some other job the player has put them on.</summary>
    public const string Job = "job";

    /// <summary>
    /// With the player, wherever the player is. Nothing writes this yet: it is what an entourage will
    /// set when a player is allowed to take people with them, and it is the only value here that makes
    /// a crew member's city follow <see cref="Player.City"/> rather than sit still.
    /// </summary>
    public const string WithPlayer = "with-player";

    public static readonly IReadOnlyList<string> All =
    [
        AtHideout, Garrison, Raid, MuleRun, Travelling, Injured, Jail, Job, WithPlayer
    ];

    /// <summary>The assignment as it is stored: trimmed, lowercased, and at the house when unrecognised.</summary>
    public static string Normalize(string? assignment)
    {
        var key = assignment?.Trim().ToLowerInvariant();
        return key is not null && All.Contains(key) ? key : AtHideout;
    }

    /// <summary>How the roster says it.</summary>
    public static string Label(string assignment) => Normalize(assignment) switch
    {
        Garrison => "On a corner",
        Raid => "Out on a raid",
        MuleRun => "Running a load",
        Travelling => "Travelling",
        Injured => "Laid up",
        Jail => "Inside",
        Job => "On a job",
        WithPlayer => "With you",
        _ => "At the hideout"
    };

    /// <summary>
    /// Whether somebody on this assignment is standing at the house.
    ///
    /// The question every hideout action asks, and the reason the constants are worth having: a raid
    /// party and a garrison are not at the house, so they do not defend it and they cannot be fired
    /// from it, and neither of those rules has to know what a garrison is.
    /// </summary>
    public static bool IsHome(string? assignment) => Normalize(assignment) == AtHideout;
}
