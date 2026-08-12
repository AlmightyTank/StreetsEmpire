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
            CombatResolutionService combatResolver,
            CancellationToken ct) =>
        {
            await combatResolver.ResolveDueAsync(DateTime.UtcNow, ct);
            var result = await db.ActionLogs.AsNoTracking()
                .Where(x => x.Action != "ADMIN" && x.Action != "STORE")
                .OrderByDescending(x => x.CreatedAtUtc)
                .ThenByDescending(x => x.Id)
                .Take(30)
                .Select(x => new WorldNewsEntryResponse(
                    x.Id,
                    x.Player.Name,
                    x.Player.City,
                    x.Action,
                    x.Summary,
                    x.TurnsSpent,
                    x.CreatedAtUtc))
                .ToListAsync(ct);

            return Results.Ok(result);
        }).RequireAuthorization();
    }
}
