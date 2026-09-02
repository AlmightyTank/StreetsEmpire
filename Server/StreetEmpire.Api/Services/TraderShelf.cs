using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

/// <summary>
/// What is actually on a counter right now: which lines they got hold of today, and how many are left.
///
/// Two clocks, because they answer two different questions and a shop that ran both on one would be
/// either boring or unusable. What a trader *sells* turns over at midnight - a whole day of the same
/// shop, so "Pete has SMGs this week" is a thing worth knowing and worth travelling on. How many they
/// *have* comes back every two hours, so a counter somebody cleaned out is an inconvenience rather than
/// a town shut for the evening.
///
/// The range is rolled rather than stored: it is a pure function of the town, the line and the day, so
/// every player in a city sees the same shop on the same date without a row anywhere, and a shop cannot
/// drift out of step with itself between two requests. Quantity has to be stored, because the whole
/// point of it is that buying takes it away.
/// </summary>
public sealed class TraderShelfService(GameDbContext db, IOptionsSnapshot<GameOptions> options)
{
    private readonly GameOptions _options = options.Value;

    /// <summary>
    /// The day a moment falls in, in the trade's own time.
    ///
    /// Central time because that is where the game's clock has always been set, and the whole country
    /// restocking at once matters more than any one player's midnight: a shop that turned over at each
    /// player's local midnight would mean two people standing in the same town seeing different shelves.
    ///
    /// The real zone rather than a fixed offset, so the hour does not walk an hour out of true for half
    /// the year - with a fixed fallback for a container built without a timezone database, which is a
    /// thing that happens and should not take the shop down.
    /// </summary>
    public static DateOnly TradingDay(DateTime nowUtc)
    {
        var central = Central();
        var local = central is null
            ? nowUtc.AddHours(-6)
            : TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), central);
        return DateOnly.FromDateTime(local);
    }

    private static TimeZoneInfo? Central()
    {
        foreach (var id in new[] { "America/Chicago", "Central Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return null;
    }

    /// <summary>
    /// The delivery window a moment falls in, aligned to the clock rather than to whoever last bought
    /// something. Everybody's shop fills at the same minute, which is a thing players can learn and plan
    /// around; a window that started at each player's first purchase would be unknowable.
    /// </summary>
    public DateTime WindowStart(DateTime nowUtc)
    {
        var hours = Math.Clamp(_options.Store.Shelf.RestockHours, 1, 24);
        var day = nowUtc.Date;
        var elapsed = (int)(nowUtc - day).TotalHours;
        return DateTime.SpecifyKind(day.AddHours(elapsed / hours * hours), DateTimeKind.Utc);
    }

    /// <summary>
    /// Whether this town's counter got hold of a line today.
    ///
    /// Their configured list is what they *can* get; this is what actually turned up. A shop whose range
    /// never moved was a price list with a name on it - the reason to walk in on a Tuesday was the same
    /// as the reason on Monday, which is no reason at all.
    ///
    /// The three lines every counter always carries are never rolled for. See StoreTrader.Always.
    /// </summary>
    public bool CarriesToday(string? city, string good, DateTime nowUtc)
    {
        var key = good.Trim().ToLowerInvariant();
        if (StoreTrader.Always.Contains(key)) return true;
        if (!StoreTrader.Carries(city, _options, key)) return false;

        var chance = Math.Clamp(_options.Store.Shelf.LineInStockPercent, 1, 100);
        return Roll(city, key, TradingDay(nowUtc).DayNumber, 100) < chance;
    }

    /// <summary>
    /// How many of a line a full delivery brings.
    ///
    /// A trader stocks roughly the same money in every line rather than the same count, which is what
    /// makes a shelf read like a shop: condoms by the hundred and rifles in ones, without a table
    /// anybody has to keep in step with the prices. Rolled off the window so a quiet line is quiet all
    /// window and not per request.
    /// </summary>
    public int FullStock(string? city, string good, int listPrice, DateTime nowUtc)
    {
        var config = _options.Store.Shelf;
        var shelf = Math.Max(1, StoreTrader.Price(city, _options, Math.Max(1, listPrice)));
        var typical = (int)Math.Clamp(config.ValuePerLine / shelf, config.MinPerLine, config.MaxPerLine);
        // A quarter either way, so two towns are not carrying identical numbers of everything.
        var swing = Math.Max(0, typical / 4);
        var window = WindowStart(nowUtc);
        var rolled = typical - swing + Roll(city, good, window.GetHashCode(), swing * 2 + 1);
        // Clamped after the roll as well as before it. Clamping only the middle let the swing carry a
        // cheap line straight back out through the ceiling - four hundred condoms became five hundred -
        // which is exactly the sort of bound that is never noticed because nothing about it looks wrong.
        return (int)Math.Clamp(rolled, Math.Max(1, config.MinPerLine), Math.Max(1, config.MaxPerLine));
    }

    /// <summary>
    /// What is left on the counter, line by line. Lines the trader did not get hold of today are absent
    /// rather than zero: not carrying a thing and having sold out of it are different sentences.
    /// </summary>
    public async Task<Dictionary<string, int>> RemainingAsync(string city, IReadOnlyList<StoreItemResponse> lines, DateTime nowUtc, CancellationToken ct)
    {
        var window = WindowStart(nowUtc);
        var rows = await db.TraderStocks.Where(x => x.City == city).ToListAsync(ct);
        var shelf = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var touched = false;

        foreach (var line in lines)
        {
            if (!CarriesToday(city, line.Key, nowUtc)) continue;

            var row = rows.FirstOrDefault(x => string.Equals(x.Good, line.Key, StringComparison.OrdinalIgnoreCase));
            if (row is null)
            {
                row = new TraderStock { City = city, Good = line.Key, WindowStartUtc = window, Remaining = FullStock(city, line.Key, line.ListPrice, nowUtc) };
                db.TraderStocks.Add(row);
                touched = true;
            }
            else if (row.WindowStartUtc < window)
            {
                // A delivery came. Whatever was left is replaced rather than added to, so a line nobody
                // touched does not quietly pile up into a warehouse.
                row.WindowStartUtc = window;
                row.Remaining = FullStock(city, line.Key, line.ListPrice, nowUtc);
                touched = true;
            }

            shelf[line.Key] = Math.Max(0, row.Remaining);
        }

        if (touched) await db.SaveChangesAsync(ct);
        return shelf;
    }

    /// <summary>Takes goods off the counter. Called after a purchase has already been allowed.</summary>
    public async Task TakeAsync(string city, string good, int quantity, DateTime nowUtc, CancellationToken ct)
    {
        if (quantity <= 0) return;
        var row = await db.TraderStocks.FirstOrDefaultAsync(x => x.City == city && x.Good == good, ct);
        if (row is null) return;
        row.Remaining = Math.Max(0, row.Remaining - quantity);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Puts goods back on it, which is what finishing a shelf-gap job is for: the counter is short, you
    /// bring them some, and the line is on sale again before the next delivery would have brought it.
    /// </summary>
    public async Task RestockAsync(string city, string good, int quantity, DateTime nowUtc, CancellationToken ct)
    {
        if (quantity <= 0) return;
        var row = await db.TraderStocks.FirstOrDefaultAsync(x => x.City == city && x.Good == good, ct);
        if (row is null)
        {
            db.TraderStocks.Add(new TraderStock { City = city, Good = good, WindowStartUtc = WindowStart(nowUtc), Remaining = quantity });
            return;
        }
        row.Remaining += quantity;
    }

    /// <summary>
    /// A stable number from a few strings and an integer, for the things that have to be the same for
    /// everybody looking at the same shop on the same day without being written down anywhere.
    /// </summary>
    private static int Roll(string? city, string good, int salt, int range)
    {
        if (range <= 1) return 0;
        var hash = 17;
        unchecked
        {
            foreach (var c in (city ?? string.Empty).ToLowerInvariant()) hash = hash * 31 + c;
            foreach (var c in good.ToLowerInvariant()) hash = hash * 31 + c;
            hash = hash * 31 + salt;
        }
        return Math.Abs(hash % range);
    }
}

/// <summary>
/// The two clocks a counter runs on, and how deep it stocks.
/// </summary>
public sealed class TraderShelfOptions
{
    /// <summary>
    /// How often a delivery comes. Two hours, so a shop somebody cleaned out is an inconvenience rather
    /// than a town closed for the evening - which is the thing that makes it safe for buying to take
    /// stock away at all.
    /// </summary>
    public int RestockHours { get; set; } = 2;

    /// <summary>
    /// The odds a line the trader can get hold of actually turned up today. Not certainty, because a
    /// range that never moved is a price list: the reason to walk in on a Tuesday should not be the same
    /// as Monday's.
    /// </summary>
    public int LineInStockPercent { get; set; } = 75;

    /// <summary>
    /// Roughly the money a trader keeps in each line. Value rather than count, which is what makes a
    /// shelf read like a shop without a table anybody has to keep in step with the prices: condoms by
    /// the hundred, rifles in ones.
    /// </summary>
    public long ValuePerLine { get; set; } = 60_000;

    public int MinPerLine { get; set; } = 2;
    public int MaxPerLine { get; set; } = 400;
}
