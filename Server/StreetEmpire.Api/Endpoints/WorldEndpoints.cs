using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Mapping;
using StreetEmpire.Api.Models;
using StreetEmpire.Api.Services;
using static StreetEmpire.Api.Mapping.ResponseMappers;
using static StreetEmpire.Api.Support.ActionLogging;
using static StreetEmpire.Api.Support.BotSeeding;
using static StreetEmpire.Api.Support.Formatting;
using static StreetEmpire.Api.Support.LiveOpsStore;
using static StreetEmpire.Api.Support.PlayerRanking;
using StreetEmpire.Api.Support;

namespace StreetEmpire.Api.Endpoints;

/// <summary>Leaderboard and the global activity feed.</summary>
internal static class WorldEndpoints
{
    internal static void MapWorldEndpoints(this IEndpointRouteBuilder app)
    {

        app.MapGet("/api/game/leaderboard", async (
            GameDbContext db,
            EconomyService economy,
            IOptionsSnapshot<GameOptions> gameOptions,
            string? city,
            CancellationToken ct) =>
        {
            // A town's own ladder. Eight cities on one global board means most players never appear on
            // it and never will, so the town they chose is the one place their standing is legible.
            var options = gameOptions.Value;
            options.Territory.ApplyDefaultsWhereEmpty();
            var scope = options.CityMarkets.ResolveCity(city);
            if (city is not null && scope is null)
                return Results.BadRequest(new { error = $"Pick one of: {string.Join(", ", options.Territory.Cities())}." });

            // Ordered and capped by the database: rank is the row's position within whatever is asked
            // for, so a city board reads 1..n for that town rather than showing global positions.
            var top = await db.Players.AsNoTracking()
                .Include(x => x.Account)
                .Where(x => scope == null || x.City == scope)
                .OrderByDescending(economy.NetWorthExpression)
                .ThenBy(x => x.CreatedAtUtc)
                .Take(50)
                .ToListAsync(ct);
            var result = top
                .Select((x, index) => new LeaderboardEntryResponse(
                    index + 1,
                    x.Name,
                    AvatarUrl(x.Account),
                    x.Account.ProfileTagline,
                    x.City,
                    economy.CalculateNetWorth(x),
                    x.Cash,
                    x.BankCash,
                    x.Pimps,
                    x.Hoes,
                    x.Thugs))
                .ToList();
            return Results.Ok(result);
        }).RequireAuthorization();


        // Who leads at what today. Its own route rather than part of the news feed: the feed is a list of
        // things that happened, and this is a standing fact about who people are this week.
        app.MapGet("/api/world/titles", async (
            TitleService titles,
            CombatResolutionService combatResolver,
            CancellationToken ct) =>
        {
            var now = DateTime.UtcNow;
            await combatResolver.ResolveDueAsync(now, ct);
            return Results.Ok(await titles.BoardAsync(now, ct));
        }).RequireAuthorization();


        app.MapGet("/api/game/prayer", async (
            CurrentPlayerService current,
            PrayerService prayer,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var now = DateTime.UtcNow;
            return Results.Ok(ToPrayerBoard(player, prayer, now));
        }).RequireAuthorization();


        app.MapPost("/api/game/prayer", async (
            PrayerRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            PlayerClock clock,
            PrayerService prayer,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var now = DateTime.UtcNow;
            await clock.AdvanceAsync(player, now, db, ct);
            var before = Snapshot(player);
            try
            {
                var result = prayer.Offer(player, request.Offered, now);
                AddLog(db, player, before, "PRAYER", 0, result.Summary, now);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new ActionResultResponse(result.Summary, player.Turns, new Dictionary<string, object?>
                {
                    ["good"] = result.Demand.Good,
                    ["asked"] = result.Demand.Quantity,
                    ["offered"] = result.Offered,
                    ["generous"] = result.Generous,
                    ["blessing"] = result.Blessing.Kind,
                    ["nextPrayerAtUtc"] = prayer.NextPrayerAtUtc(player)
                }));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();


        app.MapGet("/api/world/news", async (
            GameDbContext db,
            EconomyService economy,
            CombatResolutionService combatResolver,
            IOptionsSnapshot<GameOptions> gameOptions,
            CancellationToken ct) =>
        {
            var now = DateTime.UtcNow;
            await combatResolver.ResolveDueAsync(now, ct);

            var options = gameOptions.Value.WorldNews;
            var since = now.AddHours(-Math.Max(1, options.WindowHours));

            var feed = await db.ActionLogs.AsNoTracking()
                .Where(WorldNews.IsNewsworthy(options, since))
                .OrderByDescending(x => x.CreatedAtUtc)
                .ThenByDescending(x => x.Id)
                .Take(Math.Max(1, options.FeedSize))
                .Select(x => new { x.Id, x.Player.Name, x.Player.City, x.Action, x.Summary, x.TurnsSpent, x.CreatedAtUtc })
                .ToListAsync(ct);

            return Results.Ok(new WorldNewsResponse(
                await HeadlinesAsync(db, economy, since, ct),
                feed
                    .Select(x => new WorldNewsEntryResponse(
                        x.Id, x.Name, x.City, x.Action, WorldNews.Category(x.Action), x.Summary, x.TurnsSpent, x.CreatedAtUtc))
                    .ToList()));
        }).RequireAuthorization();
    }

    /// <summary>
    /// The shrine as the player sees it, including why it is shut when it is shut.
    /// </summary>
    private static PrayerBoardResponse ToPrayerBoard(Player player, PrayerService prayer, DateTime nowUtc)
    {
        var demand = prayer.DemandFor(player, nowUtc);
        var held = demand.Good == "cash" ? player.Cash : TradeGoods.Held(player, demand.Good);
        var canPray = prayer.CanPray(player, nowUtc);
        var next = prayer.NextPrayerAtUtc(player);

        var blocked = !canPray && next is { } due
            ? $"The gods have heard from you. Come back in {Math.Max(1, (int)Math.Ceiling((due - nowUtc).TotalDays))} day(s)."
            : held < demand.Quantity
                ? $"They want {demand.Quantity:N0} {demand.Label} and you have {held:N0}."
                : null;

        return new PrayerBoardResponse(
            canPray,
            next,
            demand.Good,
            demand.Label,
            demand.Quantity,
            demand.ApproximateValue,
            held,
            demand.Quantity * 2,
            blocked);
    }

    /// <summary>
    /// The standing state of the world, as separate top-one queries. Each is ordered and cut by the
    /// database; none of them pulls a page of rows back to pick a winner in memory.
    /// </summary>
    private static async Task<List<WorldHeadlineResponse>> HeadlinesAsync(
        GameDbContext db,
        EconomyService economy,
        DateTime sinceUtc,
        CancellationToken ct)
    {
        var headlines = new List<WorldHeadlineResponse>();

        var leader = await db.Players.AsNoTracking()
            .OrderByDescending(economy.NetWorthExpression)
            .ThenBy(x => x.CreatedAtUtc)
            .Select(x => new { x.Name, x.City, x.Pimps, x.Hoes, x.Thugs })
            .FirstOrDefaultAsync(ct);
        if (leader is not null)
            headlines.Add(new WorldHeadlineResponse(
                "leader",
                $"{leader.Name} runs the city",
                $"Out of {leader.City} with {leader.Pimps + leader.Hoes + leader.Thugs:N0} crew."));

        var robbery = await db.CombatLogs.AsNoTracking()
            .Where(x => x.CreatedAtUtc >= sinceUtc && x.CashStolen > 0)
            .OrderByDescending(x => x.CashStolen)
            .Select(x => new { Attacker = x.Attacker.Name, Defender = x.Defender.Name, x.CashStolen })
            .FirstOrDefaultAsync(ct);
        if (robbery is not null)
            headlines.Add(new WorldHeadlineResponse(
                "robbery",
                "Biggest take",
                $"{robbery.Attacker} walked out of {robbery.Defender}'s place with {robbery.CashStolen:C0}."));

        var score = await db.ActionLogs.AsNoTracking()
            // START carries the whole starting stake as a cash delta, which would crown every new
            // player the best earner of the day for doing nothing.
            .Where(x => x.CreatedAtUtc >= sinceUtc && x.Action != "ADMIN" && x.Action != "ATTACK" && x.Action != "START")
            .OrderByDescending(x => x.CashDelta + x.BankDelta)
            .Select(x => new { x.Player.Name, Earned = x.CashDelta + x.BankDelta })
            .FirstOrDefaultAsync(ct);
        if (score is { Earned: > 0 })
            headlines.Add(new WorldHeadlineResponse(
                "score",
                "Best day on the street",
                $"{score.Name} cleared {score.Earned:C0} in one go."));

        // Who runs the most ground. Counted and ordered by the database rather than reading the map
        // back to tally it.
        var landlord = await db.Territories.AsNoTracking()
            .Where(x => x.HolderId != null)
            .GroupBy(x => new { x.HolderId, Name = x.Holder!.Name, x.City })
            .Select(g => new { g.Key.Name, g.Key.City, Pieces = g.Count(), Thugs = g.Sum(x => x.GarrisonThugs) })
            .OrderByDescending(x => x.Pieces)
            .ThenByDescending(x => x.Thugs)
            .FirstOrDefaultAsync(ct);
        if (landlord is { Pieces: > 0 })
            headlines.Add(new WorldHeadlineResponse(
                "ground",
                landlord.Pieces == 1 ? $"{landlord.Name} holds ground" : $"{landlord.Name} runs {landlord.Pieces:N0} pieces",
                $"{landlord.Thugs:N0} thug(s) standing on it across {landlord.City}."));

        // A feud: two names that keep turning up on opposite sides of the same fight. This is the one
        // headline that is about the world rather than about a single best-of, and the whole point of
        // rivals holding grudges - a story running whether or not the player is in it.
        var quarrels = await db.CombatLogs.AsNoTracking()
            .Where(x => x.CreatedAtUtc >= sinceUtc && x.Outcome != "Pending" && x.Outcome != "Canceled")
            .Select(x => new FeudRound(x.AttackerId, x.DefenderId, x.Attacker.Name, x.Defender.Name))
            .ToListAsync(ct);
        // The one headline that is about the world rather than a single best-of, and the whole point
        // of rivals holding grudges: a story running whether or not the player is in it.
        if (WorldFeuds.Pick(quarrels) is { } feud)
            headlines.Add(new WorldHeadlineResponse(
                "feud",
                $"{feud.Aggressor} and {feud.Victim} are at each other",
                WorldFeuds.Describe(feud)));

        var arrival = await db.Players.AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new { x.Name, x.City, x.CreatedAtUtc })
            .FirstOrDefaultAsync(ct);
        if (arrival is not null && arrival.CreatedAtUtc >= sinceUtc)
            headlines.Add(new WorldHeadlineResponse(
                "arrival",
                "New name in town",
                $"{arrival.Name} set up in {arrival.City}."));

        return headlines;
    }
}
