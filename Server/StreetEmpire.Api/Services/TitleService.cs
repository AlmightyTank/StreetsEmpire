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
public sealed class TitleService(GameDbContext db, IOptionsSnapshot<GameOptions> options, EconomyService economy)
{
    public const string DiscordConnectedKey = "discord-connected";
    public const string DiscordConnectedTitle = "Discord Connected";

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

        board.AddRange(await CustomBoardAsync(cancellationToken));
        return board;
    }

    public async Task<IReadOnlyList<AdminCustomTitleResponse>> CustomTitlesAsync(CancellationToken cancellationToken)
        => await db.CustomTitles.AsNoTracking()
            .OrderBy(x => x.Title)
            .ThenBy(x => x.Key)
            .Select(x => ToAdminResponse(x))
            .ToListAsync(cancellationToken);

    public static IReadOnlyList<CustomTitleCriteriaResponse> CriteriaCatalog()
        =>
        [
            new(CustomTitleCriteria.NetWorthAtLeast, "Net worth at least", true, false),
            new(CustomTitleCriteria.CashAtLeast, "Cash at least", true, false),
            new(CustomTitleCriteria.BankCashAtLeast, "Bank cash at least", true, false),
            new(CustomTitleCriteria.PimpsAtLeast, "Pimps at least", true, false),
            new(CustomTitleCriteria.HoesAtLeast, "Hoes at least", true, false),
            new(CustomTitleCriteria.ThugsAtLeast, "Thugs at least", true, false),
            new(CustomTitleCriteria.RidesAtLeast, "Rides at least", true, false),
            new(CustomTitleCriteria.WeaponsAtLeast, "Weapons at least", true, false),
            new(CustomTitleCriteria.CityIs, "City is", false, true),
            new(CustomTitleCriteria.CrewIs, "Crew is", false, true),
            new(CustomTitleCriteria.CrewBoss, "Crew boss", false, false),
            new(CustomTitleCriteria.TopTen, "Top ten", false, false),
            new(CustomTitleCriteria.DiscordConnected, "Discord connected", false, false)
        ];

    public async Task<CustomTitle> CreateCustomTitleAsync(
        AdminCustomTitleRequest request,
        PlayerAccount actor,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var title = BuildCustomTitle(request, actor, nowUtc);
        if (await db.CustomTitles.AnyAsync(x => x.Key == title.Key, cancellationToken))
            throw new GameRuleException("That title key is already in use.");

        db.CustomTitles.Add(title);
        return title;
    }

    public async Task<CustomTitle?> UpdateCustomTitleAsync(
        long id,
        AdminCustomTitleRequest request,
        PlayerAccount actor,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var title = await db.CustomTitles.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (title is null) return null;

        ApplyCustomTitle(title, request, actor, nowUtc, creating: false);
        if (await db.CustomTitles.AnyAsync(x => x.Id != id && x.Key == title.Key, cancellationToken))
            throw new GameRuleException("That title key is already in use.");

        return title;
    }

    public async Task<bool> IsTitleKeyAsync(string key, CancellationToken cancellationToken)
        => IsTitleKey(key) || await db.CustomTitles.AnyAsync(x => x.IsActive && x.Key == key, cancellationToken);

    /// <summary>
    /// The titles one player holds, from a board already read. Callers with a page of players to
    /// decorate read the board once and ask this per name, rather than a query each.
    /// </summary>
    public static IReadOnlyList<string> For(Guid playerId, IReadOnlyList<PlayerTitleResponse> board)
        => board.Where(x => x.PlayerId == playerId).Select(x => x.Title).ToList();

    /// <summary>
    /// Permanent titles earned by account choices rather than a day's fighting. They are shaped like
    /// board rows so the picker and profile can treat them exactly like any other title.
    /// </summary>
    public static IReadOnlyList<PlayerTitleResponse> AccountTitles(PlayerAccount account)
        => account.Player is null || account.DiscordUserId is null
            ? []
            :
            [
                new PlayerTitleResponse(
                    DiscordConnectedKey,
                    DiscordConnectedTitle,
                    account.Player.Id,
                    account.Player.Name,
                    1,
                    "Linked a Discord account to this empire.")
            ];

    public static IReadOnlyList<string> For(PlayerAccount account, IReadOnlyList<PlayerTitleResponse> board)
        => For(account, board, account.FeaturedTitle);

    public static IReadOnlyList<string> For(PlayerAccount account, IReadOnlyList<PlayerTitleResponse> board, bool publicOnly)
        => For(account, board, account.FeaturedTitle, publicOnly);

    public static IReadOnlyList<string> For(PlayerAccount account, IReadOnlyList<PlayerTitleResponse> board, string? featuredKey)
        => For(account, board, featuredKey, publicOnly: false);

    public static IReadOnlyList<string> For(
        PlayerAccount account,
        IReadOnlyList<PlayerTitleResponse> board,
        string? featuredKey,
        bool publicOnly)
    {
        if (account.Player is null) return [];
        IReadOnlyList<PlayerTitleResponse> accountTitles = publicOnly && !account.ShowDiscordOnProfile ? [] : AccountTitles(account);
        return For(account.Player.Id, [.. board, .. accountTitles], featuredKey);
    }

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
        => string.Equals(key, DiscordConnectedKey, StringComparison.OrdinalIgnoreCase)
           || TitleCategories.All.Any(x => x.Key == key);

    internal async Task<IReadOnlyList<PlayerTitleResponse>> CustomBoardAsync(CancellationToken cancellationToken)
    {
        var definitions = await db.CustomTitles.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Title)
            .ThenBy(x => x.Key)
            .ToListAsync(cancellationToken);
        if (definitions.Count == 0) return [];

        var players = await db.Players.AsNoTracking()
            .Include(x => x.Account)
            .Include(x => x.Alliance)
            .Include(x => x.Hideout)
            .OrderByDescending(economy.NetWorthExpression)
            .ThenBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        var ranks = players
            .Select((player, index) => new { player.Id, Rank = index + 1 })
            .ToDictionary(x => x.Id, x => x.Rank);

        var rows = new List<PlayerTitleResponse>();
        foreach (var definition in definitions)
        foreach (var player in players)
        {
            var value = CriteriaValue(definition, player, ranks[player.Id]);
            if (value is null) continue;

            rows.Add(new PlayerTitleResponse(
                definition.Key,
                definition.Title,
                player.Id,
                player.Name,
                value.Value,
                string.IsNullOrWhiteSpace(definition.Detail)
                    ? EarnedDetail(definition, value.Value)
                    : definition.Detail));
        }

        return rows;
    }

    private static CustomTitle BuildCustomTitle(AdminCustomTitleRequest request, PlayerAccount actor, DateTime nowUtc)
    {
        var title = new CustomTitle
        {
            CreatedAtUtc = nowUtc,
            CreatedByUsername = actor.Username
        };
        ApplyCustomTitle(title, request, actor, nowUtc, creating: true);
        return title;
    }

    private static void ApplyCustomTitle(CustomTitle title, AdminCustomTitleRequest request, PlayerAccount actor, DateTime nowUtc, bool creating)
    {
        var key = NormalizeKey(request.Key);
        if (key is null)
            throw new GameRuleException("Title key must be 2-32 lowercase letters, numbers, or dashes.");
        if (IsTitleKey(key))
            throw new GameRuleException("That title key is reserved by a built-in title.");

        var name = NormalizeText(request.Title, 64);
        if (name is null)
            throw new GameRuleException("Title name is required.");

        var criteria = request.Criteria?.Trim().ToLowerInvariant();
        if (criteria is null || !CustomTitleCriteria.All.Contains(criteria))
            throw new GameRuleException("Pick a valid way to earn this title.");

        var threshold = request.Threshold ?? 0;
        var text = NormalizeText(request.TextValue, 64);
        var needsThreshold = CriteriaCatalog().Single(x => x.Key == criteria).NeedsThreshold;
        var needsText = CriteriaCatalog().Single(x => x.Key == criteria).NeedsText;
        if (needsThreshold && threshold <= 0)
            throw new GameRuleException("This title needs a positive threshold.");
        if (needsText && text is null)
            throw new GameRuleException("This title needs a city or crew name.");

        title.Key = key;
        title.Title = name;
        title.Detail = NormalizeText(request.Detail, 240) ?? string.Empty;
        title.Criteria = criteria;
        title.Threshold = needsThreshold ? threshold : 0;
        title.TextValue = needsText ? text : null;
        title.IsActive = request.IsActive ?? title.IsActive;
        if (!creating)
        {
            title.UpdatedAtUtc = nowUtc;
            title.UpdatedByUsername = actor.Username;
        }
    }

    private long? CriteriaValue(CustomTitle title, Player player, int rank)
        => title.Criteria switch
        {
            CustomTitleCriteria.NetWorthAtLeast => Qualify(economy.CalculateNetWorth(player), title.Threshold),
            CustomTitleCriteria.CashAtLeast => Qualify(player.Cash, title.Threshold),
            CustomTitleCriteria.BankCashAtLeast => Qualify(player.BankCash, title.Threshold),
            CustomTitleCriteria.PimpsAtLeast => Qualify(player.Pimps, title.Threshold),
            CustomTitleCriteria.HoesAtLeast => Qualify(player.Hoes, title.Threshold),
            CustomTitleCriteria.ThugsAtLeast => Qualify(player.Thugs, title.Threshold),
            CustomTitleCriteria.RidesAtLeast => Qualify(player.Rides, title.Threshold),
            CustomTitleCriteria.WeaponsAtLeast => Qualify(player.Weapons, title.Threshold),
            CustomTitleCriteria.CityIs when Same(player.City, title.TextValue) => 1,
            CustomTitleCriteria.CrewIs when Same(player.Alliance?.Name, title.TextValue) => 1,
            CustomTitleCriteria.CrewBoss when player.Alliance?.FounderId == player.Id => 1,
            CustomTitleCriteria.TopTen when rank <= 10 => rank,
            CustomTitleCriteria.DiscordConnected when player.Account.DiscordUserId is not null => 1,
            _ => null
        };

    private static long? Qualify(long value, long threshold)
        => value >= threshold ? value : null;

    private static bool Same(string? left, string? right)
        => string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string EarnedDetail(CustomTitle title, long value)
        => title.Criteria switch
        {
            CustomTitleCriteria.NetWorthAtLeast => $"Reached {value:C0} net worth.",
            CustomTitleCriteria.CashAtLeast => $"Held {value:C0} cash.",
            CustomTitleCriteria.BankCashAtLeast => $"Banked {value:C0}.",
            CustomTitleCriteria.PimpsAtLeast => $"Ran {value:N0} pimp(s).",
            CustomTitleCriteria.HoesAtLeast => $"Ran {value:N0} hoe(s).",
            CustomTitleCriteria.ThugsAtLeast => $"Kept {value:N0} thug(s).",
            CustomTitleCriteria.RidesAtLeast => $"Parked {value:N0} ride(s).",
            CustomTitleCriteria.WeaponsAtLeast => $"Armed the house with {value:N0} weapon(s).",
            CustomTitleCriteria.CityIs => $"Set up in {title.TextValue}.",
            CustomTitleCriteria.CrewIs => $"Runs with {title.TextValue}.",
            CustomTitleCriteria.CrewBoss => "Founded a crew.",
            CustomTitleCriteria.TopTen => $"Reached rank #{value:N0}.",
            CustomTitleCriteria.DiscordConnected => "Linked Discord.",
            _ => "Earned this title."
        };

    internal static AdminCustomTitleResponse ToAdminResponse(CustomTitle title)
        => new(
            title.Id,
            title.Key,
            title.Title,
            title.Detail,
            title.Criteria,
            title.Threshold,
            title.TextValue,
            title.IsActive,
            title.CreatedAtUtc,
            title.CreatedByUsername,
            title.UpdatedAtUtc,
            title.UpdatedByUsername);

    private static string? NormalizeText(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var clean = string.Join(' ', value.Trim().Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
        return clean.Length <= max ? clean : clean[..max];
    }

    private static string? NormalizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var key = value.Trim().ToLowerInvariant();
        return key.Length is < 2 or > 32 || key.Any(ch => ch is not (>= 'a' and <= 'z') and not (>= '0' and <= '9') and not '-')
            ? null
            : key;
    }

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
