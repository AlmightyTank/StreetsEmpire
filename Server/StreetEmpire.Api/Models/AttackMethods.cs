namespace StreetEmpire.Api.Models;

/// <summary>
/// The ways one player can move on another.
///
/// A raid is the heavyweight: a crew leaves the house, travels, fights over rounds, and comes home with
/// whatever it could carry. The four strikes are the opposite of that in every respect - cheap, instant,
/// narrow, and each aimed at exactly one thing the target owns. They exist because a single attack verb
/// makes the whole of a player's holdings one undifferentiated pile of loot: with only a raid, a garage
/// of rides and a hundred hoes are just numbers feeding the same defence roll, and no decision a
/// defender makes about them matters. A strike per asset is what makes each one worth guarding.
/// </summary>
public static class AttackMethods
{
    /// <summary>Break the door down and take what is inside. The multi-round mission.</summary>
    public const string Raid = "raid";

    /// <summary>Shoot up the street from a moving car. Cheap, loud, takes nothing, thins their guard.</summary>
    public const string DriveBy = "driveby";

    /// <summary>Take their rides. Contested by whoever is standing in the garage, not by the whole house.</summary>
    public const string Jack = "jack";

    /// <summary>Put something through their hoes. Medicine is the answer; running out of it is the cost.</summary>
    public const string Infest = "infest";

    /// <summary>Buy their hoes away with product. Answered by paying them enough that they will not go.</summary>
    public const string Poach = "poach";

    public static readonly string[] All = [Raid, DriveBy, Jack, Infest, Poach];

    /// <summary>Everything that resolves on the spot rather than as a travelling mission.</summary>
    public static readonly string[] Strikes = [DriveBy, Jack, Infest, Poach];

    public static bool IsStrike(string? method)
        => Strikes.Contains(Normalize(method));

    /// <summary>
    /// Reads a method off a request. An empty or unknown method is a raid, which is what every caller
    /// written before the menu existed was asking for.
    /// </summary>
    public static string Normalize(string? method)
    {
        var key = method?.Trim().ToLowerInvariant();
        return string.IsNullOrEmpty(key) ? Raid : All.Contains(key) ? key : Raid;
    }

    /// <summary>How a summary or a log row names it.</summary>
    public static string Label(string? method) => Normalize(method) switch
    {
        DriveBy => "drive-by",
        Jack => "jacking",
        Infest => "infestation",
        Poach => "poaching",
        _ => "raid"
    };
}
