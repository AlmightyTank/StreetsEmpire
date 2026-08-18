namespace StreetEmpire.Api.Models;

/// <summary>
/// How a crew takes people on.
///
/// One setting with three states rather than a boolean and two paths that were always open underneath
/// it. The old shape said "open or not" and quietly accepted applications either way, which meant a
/// crew that had shut its door was still fielding requests it never asked for and had no way to stop.
///
/// The three states are the three things an outsider can do on their own initiative, which is the only
/// axis a door actually has: walk in, ask, or wait to be asked. Invitations are deliberately not on
/// that axis - they are the crew reaching out rather than somebody arriving, they work in every mode,
/// and a crew that could not invite while set to invite-only would be a contradiction.
/// </summary>
public enum AllianceDoor
{
    /// <summary>Anybody may walk in. Asking would be a formality, so applications are turned away.</summary>
    Open = 0,

    /// <summary>Outsiders may ask, and somebody with the door power answers.</summary>
    Application = 1,

    /// <summary>Nobody gets in on their own. The crew asks, or nothing happens.</summary>
    InviteOnly = 2
}

public static class AllianceDoors
{
    public static readonly AllianceDoor[] All = [AllianceDoor.Open, AllianceDoor.Application, AllianceDoor.InviteOnly];

    public static string Label(AllianceDoor door) => door switch
    {
        AllianceDoor.Open => "Open to anyone",
        AllianceDoor.Application => "By application",
        _ => "Invitation only"
    };

    /// <summary>What the setting means, in the words the board shows an outsider.</summary>
    public static string Describe(AllianceDoor door) => door switch
    {
        AllianceDoor.Open => "Anybody can walk in.",
        AllianceDoor.Application => "Ask, and somebody will answer.",
        _ => "They take people they have asked for, and nobody else."
    };

    /// <summary>An unknown value is the most restrictive one: the safe failure for a door is shut.</summary>
    public static AllianceDoor Parse(string? value)
        => Enum.TryParse<AllianceDoor>(value?.Trim(), ignoreCase: true, out var door) && All.Contains(door)
            ? door
            : AllianceDoor.InviteOnly;
}
