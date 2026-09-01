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
        var named = Config(city, options);
        return named is null
            ? new TraderPersona("The Counter", "the back of a shop with no sign on it", "Cash on the counter.")
            : new TraderPersona(named.Name, named.Pitch, named.Patter);
    }

    /// <summary>The configured row for a town, or null for one nobody has written a trader for.</summary>
    public static StoreTraderOptions? Config(string? city, GameOptions options)
        => options.Store.Traders
            .FirstOrDefault(x => string.Equals(x.City, city?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The three lines every counter in the game carries, and which never go dry.
    ///
    /// A floor rather than a rule about breadth, and it exists for one reason: a crew that cannot buy
    /// condoms, beer or a pistol is a crew that cannot work at all. Everything else about a trader is a
    /// choice the player can travel around; this is the part that would just be a town nobody can play
    /// in. Pistols are on it because arming a thug is upkeep rather than an upgrade - the gun that keeps
    /// a crew content, not the gun that wins a fight.
    /// </summary>
    public static readonly string[] Always = ["condoms", "beer", WeaponTiers.Pistol];

    /// <summary>
    /// Whether this town's counter carries a thing at all.
    ///
    /// Breadth is what makes a town somewhere rather than a backdrop. Every shop sold every line at the
    /// same price everywhere, so which town you stood in decided what your product *sold* for and
    /// nothing at all about what you could *buy* - a whole half of a place's character that the map was
    /// not using. Auntie Vasska is cheap and carries almost nothing; Half-Deck Mo carries everything and
    /// charges for it. Travel is a buying decision now, not only a selling one.
    ///
    /// An unconfigured town carries the lot, so adding a ninth city is a row rather than a shop that
    /// silently sells nothing.
    /// </summary>
    public static bool Carries(string? city, GameOptions options, string good)
    {
        var key = good?.Trim().ToLowerInvariant() ?? string.Empty;
        if (Always.Contains(key)) return true;
        var named = Config(city, options);
        return named is null || named.Stocks.Count == 0
            || named.Stocks.Any(x => string.Equals(x, key, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// What this town's counter charges for something listed at <paramref name="listPrice"/>, before
    /// standing comes off it.
    ///
    /// A share rather than a per-good table, because the thing being modelled is a person and not a
    /// spreadsheet: a trader is dear or they are cheap, and it is the same trader on every line. What
    /// stops the cheap one simply being better is what they carry - the narrow shops are the cheap ones
    /// and the shop that has everything is the one that charges for it.
    /// </summary>
    public static int Price(string? city, GameOptions options, int listPrice)
    {
        if (listPrice <= 0) return listPrice;
        var percent = Math.Clamp(Config(city, options)?.PricePercent ?? 100, 1, 1000);
        return Math.Max(1, (int)Math.Round(listPrice * percent / 100.0));
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

    /// <summary>
    /// What they charge against the list price, as a percentage. A hundred is the price everywhere else
    /// in the game; below it is a cheap shop and above it is a dear one.
    /// </summary>
    public int PricePercent { get; set; } = 100;

    /// <summary>
    /// What they carry beyond the three lines every counter has. Empty means everything, so a town
    /// nobody has written a stock list for is a shop rather than an empty room.
    /// </summary>
    public List<string> Stocks { get; set; } = [];
}
