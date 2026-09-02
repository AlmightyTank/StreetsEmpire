namespace StreetEmpire.Api.Models;

/// <summary>
/// The rooms, by the keys the upgrade endpoint, the responses and the logs have always spelled them
/// with. Constants rather than the literals that were scattered across eight files, because damage
/// gave the keys a second job: a room is now something that can be named in a police report, a raid
/// summary and a repair bill, and a typo in any one of those is a room that can never be fixed.
/// </summary>
public static class HideoutRooms
{
    public const string Tier = "tier";
    public const string Storage = "storage";
    public const string Safe = "safe";
    public const string WeedLab = "weedlab";
    public const string CokeLab = "cokelab";
    public const string Workshop = "workshop";
    public const string Lookout = "lookout";
    public const string Intelligence = "intelligence";

    /// <summary>
    /// The rooms a raid can put out of action, and deliberately not all of them.
    ///
    /// Every room here is a function or a bonus: break it and something stops, and the player is out
    /// exactly what that thing was worth per hour until they pay to have it put back. The store and
    /// the safe are not on the list because they are capacity rather than function, and wrecking
    /// capacity does not stop a buff - it destroys stock and cash that are already sitting in the
    /// room. That is a different and much nastier mechanic than the one being built here, and it
    /// would arrive as "the law took half your weed and then the rest of it spilled into the street".
    /// </summary>
    public static readonly IReadOnlyList<string> Breakable =
    [
        WeedLab,
        CokeLab,
        Workshop,
        Lookout,
        Intelligence
    ];

    public static bool CanBreak(string room) => Breakable.Contains(room);

    /// <summary>The room in the words the hideout page prints on its own row.</summary>
    public static string Name(string room) => room switch
    {
        Tier => "building",
        Storage => "storage room",
        Safe => "safe",
        WeedLab => "weed lab",
        CokeLab => "coke lab",
        Workshop => "workshop",
        Lookout => "lookout",
        Intelligence => "intelligence centre",
        _ => "room"
    };

    /// <summary>
    /// What stops while it is down. The whole point of breaking a room is the thing it was doing, so
    /// every message about damage says it rather than leaving a player to work out from a level of
    /// zero why their mules will not leave.
    /// </summary>
    public static string Stops(string room) => room switch
    {
        WeedLab => "nothing grows and shifts lose the lab's yield",
        CokeLab => "nothing cooks and shifts lose the lab's yield",
        Workshop => "nothing comes off the bench, and every lab above the first rung is capped with it",
        Lookout => "nobody is watching the street and the odds of the next raid go back to full",
        Intelligence => "no mule runs leave and nobody can be scouted",
        _ => "the room does nothing"
    };

    /// <summary>The key as the endpoints hand it over: trimmed, lowercased, never null.</summary>
    public static string Normalize(string? room) => room?.Trim().ToLowerInvariant() ?? string.Empty;
}
