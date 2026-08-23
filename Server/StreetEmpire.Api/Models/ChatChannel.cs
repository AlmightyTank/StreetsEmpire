namespace StreetEmpire.Api.Models;

/// <summary>
/// Where a message is said.
///
/// Three, and each one is a different room rather than the same room with a filter on it: the whole
/// board, the town you are standing in, and the crew you run with. A message belongs to exactly one of
/// them and cannot be moved, because a thing said to your own crew was said on the understanding that
/// it stayed there.
/// </summary>
public enum ChatChannel
{
    /// <summary>Everybody, everywhere.</summary>
    Global = 0,

    /// <summary>Whoever is standing in the same town. Follows you when you travel.</summary>
    City = 1,

    /// <summary>Your crew, and nobody else.</summary>
    Alliance = 2
}

public static class ChatChannels
{
    public static readonly ChatChannel[] All = [ChatChannel.Global, ChatChannel.City, ChatChannel.Alliance];

    public static string Label(ChatChannel channel) => channel switch
    {
        ChatChannel.Global => "Global",
        ChatChannel.City => "City",
        _ => "Crew"
    };

    public static string Describe(ChatChannel channel) => channel switch
    {
        ChatChannel.Global => "Everybody on the board.",
        ChatChannel.City => "Whoever is in this town right now.",
        _ => "Your crew, and nobody else."
    };

    /// <summary>
    /// An unknown value is the loudest room rather than the quietest one, which is the wrong way round
    /// for a door but the right way round for a channel: a message that lands somewhere more public
    /// than intended is a mistake anybody can see and correct, while one that silently goes to a crew
    /// it was not meant for cannot be taken back.
    ///
    /// So the failure is Global, and every private channel has to be asked for by name.
    /// </summary>
    public static ChatChannel Parse(string? value)
        => Enum.TryParse<ChatChannel>(value?.Trim(), ignoreCase: true, out var channel) && All.Contains(channel)
            ? channel
            : ChatChannel.Global;
}
