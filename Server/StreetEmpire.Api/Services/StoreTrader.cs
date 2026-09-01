using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

/// <summary>
/// The person behind the counter.
///
/// The shop was a price list with a title on it. Everything else in this game is somebody - a pimp has
/// a name and a loyalty, a buyer is a place on the map you already fight over, a rival is an empire
/// with a house - and the one counter every player visits every single day was furniture. Standing was
/// the first half of fixing that: rep only means anything if there is somebody to have it with.
///
/// A name in each town rather than one dealer in eight places at once, because a slum trader who
/// operated nationwide would not be a slum trader. Standing is still one number, and that is the point
/// the greeting is written to carry: the trade is small and word travels, so the woman in Miami has
/// heard about you before you walk in.
///
/// The names are fixed to the town rather than rolled, so a player's dealer is their dealer for as long
/// as they play there. That is most of what makes it a relationship rather than a label.
/// </summary>
public static class StoreTrader
{
    /// <summary>
    /// Who runs the counter in a town, and where they run it from.
    ///
    /// Falls back rather than throwing for a town the table has never heard of: the map is
    /// configuration, somebody will add a ninth city, and a shop with no shopkeeper would be a crash
    /// on the busiest page in the game.
    /// </summary>
    public static TraderPersona For(string? city, GameOptions options)
    {
        var named = options.Store.Traders
            .FirstOrDefault(x => string.Equals(x.City, city?.Trim(), StringComparison.OrdinalIgnoreCase));
        return named is null
            ? new TraderPersona("The Counter", "the back of a shop with no sign on it", "Cash on the counter.")
            : new TraderPersona(named.Name, named.Pitch, named.Patter);
    }

    /// <summary>
    /// What they say when you walk in, which is the whole of what standing feels like before it unlocks
    /// anything.
    ///
    /// Read off the rung rather than off the points, so it changes exactly when the thing it is
    /// describing changes. A player who has just climbed hears it the next time they open the shop,
    /// which is the cheapest possible way to tell somebody their standing moved.
    /// </summary>
    public static string Greeting(Player player, GameOptions options)
    {
        var trader = For(player.City, options);
        var level = StoreRep.LevelOf(player, options);
        var top = options.Store.Ladder().LastOrDefault()?.Level ?? 1;

        if (level >= top && top > 1)
            return $"{trader.Name} does not look up. \"Whatever you need. It is already yours.\"";
        return level switch
        {
            1 => $"{trader.Name} keeps one hand under the counter. \"Never seen you. Pistols and powder, that is your lot.\"",
            2 => $"{trader.Name} nods you in. \"You have been good for it so far. Do not make me wrong.\"",
            3 => $"{trader.Name} moves a crate off the stool for you. \"Sit. I keep the good stuff for people who come back.\"",
            _ => $"{trader.Name} shuts the door behind you. \"Anything on the shelf. You have earned the back room.\""
        };
    }
}

/// <summary>
/// One trader, as the client shows them: who they are, where they work out of, and a line of patter.
/// </summary>
/// <param name="Name">What they are called. Fixed to the town.</param>
/// <param name="Pitch">Where they trade from, in a phrase. "a lock-up under the freeway".</param>
/// <param name="Patter">One line in their own voice, for under their name.</param>
public sealed record TraderPersona(string Name, string Pitch, string Patter);

/// <summary>Configuration for one town's trader, so a new city is a row rather than a code change.</summary>
public sealed class StoreTraderOptions
{
    public string City { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Pitch { get; set; } = string.Empty;
    public string Patter { get; set; } = string.Empty;
}
