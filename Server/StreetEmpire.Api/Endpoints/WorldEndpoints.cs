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
            CancellationToken ct) =>
        {
            // Ordered and capped by the database: rank is the row's position in the global order.
            var top = await db.Players.AsNoTracking()
                .OrderByDescending(economy.NetWorthExpression)
                .ThenBy(x => x.CreatedAtUtc)
                .Take(50)
                .ToListAsync(ct);
            var result = top
                .Select((x, index) => new LeaderboardEntryResponse(
                    index + 1,
                    x.Name,
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
    /// The standing state of the world, as four separate top-one queries. Each is ordered and cut by
    /// the database; none of them pulls a page of rows back to pick a winner in memory.
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
