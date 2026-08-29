using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

/// <summary>
/// Who leads at what, and the name it earns them.
///
/// Every one of these is read out of the fights that actually happened rather than kept as a counter on
/// the player. The source game held eight running totals and a button to clear them, which is two
/// problems: a stored tally can drift out of step with the history it claims to summarise, and a tally
/// you can wipe is not a record of anything. A window over the combat log cannot drift, needs no
/// migration, and expires on its own - yesterday's body count stops being today's.
///
/// Half the titles are for things done to you. That is deliberate and it is the source game's own
/// reading: it tracked hoes stolen from you beside hoes stolen by you. A board of nothing but winners
/// says only who is winning, which the leaderboard already says. Being publicly the man everybody is
/// robbing is a different fact about the world, and a funnier one.
/// </summary>
public sealed class TitleService(GameDbContext db, IOptionsSnapshot<GameOptions> options)
{
    private readonly GameOptions _options = options.Value;

    /// <summary>
    /// The board: one holder per category, or no holder where nothing happened.
    ///
    /// Two grouped queries rather than one per category. Seven top-one queries would be seven round
    /// trips to answer a question about a few dozen rows, and the aggregate has to be grouped by player
    /// either way - so the database groups once per side and the leaders are picked from that.
    /// </summary>
    public async Task<IReadOnlyList<PlayerTitleResponse>> BoardAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var config = _options.Titles;
        var since = nowUtc.AddHours(-Math.Max(1, config.WindowHours));

        // Pending rows are fights that have not happened yet, and canceled ones are fights that never
        // did. Neither belongs in a record of what was done.
        var settled = db.CombatLogs.AsNoTracking()
            .Where(x => x.CreatedAtUtc >= since && x.Outcome != "Pending" && x.Outcome != "Canceled");

        var byAttacker = await settled
            .GroupBy(x => new { x.AttackerId, x.Attacker.Name })
            .Select(g => new TitleTally(
                g.Key.AttackerId,
                g.Key.Name,
                g.Sum(x => x.HoesTaken),
                g.Sum(x => x.RidesTaken),
                g.Sum(x => x.DefenderThugsLost),
                g.Sum(x => x.CashStolen),
                g.Sum(x => x.DefenderHoesLost)))
            .ToListAsync(cancellationToken);

        var byDefender = await settled
            .GroupBy(x => new { x.DefenderId, x.Defender.Name })
            .Select(g => new TitleTally(
                g.Key.DefenderId,
                g.Key.Name,
                g.Sum(x => x.HoesTaken),
                g.Sum(x => x.RidesTaken),
                g.Sum(x => x.DefenderThugsLost),
                g.Sum(x => x.CashStolen),
                g.Sum(x => x.DefenderHoesLost)))
            .ToListAsync(cancellationToken);

        var board = new List<PlayerTitleResponse>();
        foreach (var category in TitleCategories.All)
        {
            var tallies = category.FromTheAttackersSide ? byAttacker : byDefender;
            if (Leader(tallies, category, config.MinimumToHold) is { } holder)
                board.Add(holder);
        }

        return board;
    }

    /// <summary>
    /// The titles one player holds, from a board already read. Callers with a page of players to
    /// decorate read the board once and ask this per name, rather than a query each.
    /// </summary>
    public static IReadOnlyList<string> For(Guid playerId, IReadOnlyList<PlayerTitleResponse> board)
        => board.Where(x => x.PlayerId == playerId).Select(x => x.Title).ToList();

    /// <summary>
    /// The same list with one title pulled to the front - the one this player chose to lead with, when
    /// they still hold it.
    ///
    /// Pulled forward rather than shown alone. Holding four titles and displaying one would be hiding
    /// three things the player earned today, and the board is small enough that all of them fit.
    /// </summary>
    public static IReadOnlyList<string> For(Guid playerId, IReadOnlyList<PlayerTitleResponse> board, string? featuredKey)
    {
        var held = board.Where(x => x.PlayerId == playerId).ToList();
        if (featuredKey is null) return [.. held.Select(x => x.Title)];

        return
        [
            .. held.Where(x => x.Key == featuredKey).Select(x => x.Title),
            .. held.Where(x => x.Key != featuredKey).Select(x => x.Title),
        ];
    }

    /// <summary>Whether a key is one this game hands out at all, for the endpoint that takes it.</summary>
    public static bool IsTitleKey(string key)
        => TitleCategories.All.Any(x => x.Key == key);

    /// <summary>
    /// Whoever leads a category, if anybody does by enough to be worth naming.
    ///
    /// Ties break on the name rather than the value, which is arbitrary but stable: two players level on
    /// body count would otherwise swap the title every time the page was drawn.
    /// </summary>
    private static PlayerTitleResponse? Leader(IReadOnlyList<TitleTally> tallies, TitleCategory category, int minimum)
    {
        var best = tallies
            .Select(x => new { Tally = x, Value = category.Measure(x) })
            .Where(x => x.Value >= Math.Max(1, minimum))
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Tally.Name, StringComparer.Ordinal)
            .FirstOrDefault();

        return best is null
            ? null
            : new PlayerTitleResponse(
                category.Key,
                category.Title,
                best.Tally.PlayerId,
                best.Tally.Name,
                best.Value,
                category.Describe(best.Value));
    }
}

/// <summary>One player's totals over the window, from one side of the fight.</summary>
public sealed record TitleTally(
    Guid PlayerId,
    string Name,
    int Hoes,
    int Rides,
    int Thugs,
    long Cash,
    int HoesLost);

/// <param name="FromTheAttackersSide">
/// Which end of the fight the total is read from. A category is otherwise identical whichever side it
/// counts - the same column, the same sum - and only this says whether it is a boast or a bruise.
/// </param>
public sealed record TitleCategory(
    string Key,
    string Title,
    bool FromTheAttackersSide,
    Func<TitleTally, long> Measure,
    Func<long, string> Describe);

public static class TitleCategories
{
    public static readonly IReadOnlyList<TitleCategory> All =
    [
        new("poacher", "Silver Tongue", true,
            x => x.Hoes,
            v => $"Walked {v:N0} hoe(s) out of somebody else's house."),
        new("wheelman", "Wheelman", true,
            x => x.Rides,
            v => $"Drove off in {v:N0} car(s) that were not theirs."),
        new("killer", "Body Count", true,
            x => x.Thugs,
            v => $"Put {v:N0} thug(s) in the ground."),
        new("robber", "Second Storey", true,
            x => x.Cash,
            v => $"Carried {v:C0} out of other people's houses."),

        // The other half of the board: not what they did, what was done to them.
        new("poached", "Picked Clean", false,
            x => x.Hoes,
            v => $"Lost {v:N0} hoe(s) to somebody with better product."),
        new("onfoot", "On Foot", false,
            x => x.Rides,
            v => $"Had {v:N0} car(s) driven out of the garage."),
        new("mourner", "Fresh Graves", false,
            x => x.Thugs,
            v => $"Buried {v:N0} of their own.")
    ];
}

public sealed class TitleOptions
{
    /// <summary>
    /// How far back a title looks. A day, because the point of these is that they turn over: a name
    /// earned last week that never moves is a statue rather than a title.
    /// </summary>
    public int WindowHours { get; set; } = 24;

    /// <summary>
    /// The least a category needs before anyone wears its name. Being Body Count for killing one thug
    /// makes the whole board a joke, and in a quiet world every category would always have a holder.
    /// </summary>
    public int MinimumToHold { get; set; } = 3;
}
