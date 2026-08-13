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

/// <summary>Finding and acting on individual players. Every write is audited.</summary>
internal static class AdminPlayerEndpoints
{
    internal static void MapAdminPlayerEndpoints(this IEndpointRouteBuilder app)
    {

        // ----- Admin: player administration -----

        app.MapGet("/api/admin/players", async (
            string? query,
            CurrentPlayerService current,
            AdminService admins,
            EconomyService economy,
            CancellationToken ct) =>
        {
            var admin = await current.GetAsync(ct);
            if (admin is null) return Results.Unauthorized();
            if (!admin.Account.IsAdmin) return Results.Forbid();

            // Capped: an admin needs the first page of matches, not the whole table.
            var matches = await admins.SearchPlayers(query)
                .OrderBy(x => x.Name)
                .Take(50)
                .ToListAsync(ct);
            return Results.Ok(matches.Select(x => ToAdminSummary(x, economy)).ToList());
        }).RequireAuthorization();


        app.MapGet("/api/admin/players/{playerId:guid}", async (
            Guid playerId,
            CurrentPlayerService current,
            GameDbContext db,
            AdminService admins,
            EconomyService economy,
            HideoutService hideouts,
            PimpRoster pimps,
            IOptionsSnapshot<GameOptions> gameOptions,
            CancellationToken ct) =>
        {
            var admin = await current.GetAsync(ct);
            if (admin is null) return Results.Unauthorized();
            if (!admin.Account.IsAdmin) return Results.Forbid();

            var target = await admins.FindPlayerAsync(playerId, ct);
            if (target is null) return Results.NotFound(new { error = "Player not found." });

            var activity = await db.ActionLogs.AsNoTracking()
                .Where(x => x.PlayerId == playerId)
                .OrderByDescending(x => x.CreatedAtUtc)
                .ThenByDescending(x => x.Id)
                .Take(20)
                .Select(x => new ActivityResponse(x.Id, x.Action, x.Summary, x.TurnsSpent, x.CashDelta, x.BankDelta, x.CreatedAtUtc))
                .ToListAsync(ct);
            var audit = await admins.AuditTrail()
                .Where(x => x.TargetPlayerId == playerId)
                .Take(20)
                .Select(x => new AdminAuditEntryResponse(x.Id, x.ActorUsername, x.Action, x.TargetPlayerId, x.TargetName, x.Summary, x.Reason, x.CreatedAtUtc))
                .ToListAsync(ct);

            return Results.Ok(new AdminPlayerDetailResponse(
                ToAdminSummary(target, economy),
                target.Condoms,
                target.Beer,
                target.Weapons,
                target.Weed,
                target.Coke,
                Math.Round(target.HoeHappiness, 2),
                Math.Round(target.ThugHappiness, 2),
                target.HoeCutPercent,
                target.LastAttackAtUtc,
                target.LastAttackedAtUtc,
                target.CombatProtectionUntilUtc,
                ToHideoutResponse(target, hideouts, DateTime.UtcNow, gameOptions.Value),
                pimps.Active(target).Select(x => ToPimpResponse(x, [])).ToList(),
                activity,
                audit,
                AdminService.AdjustableResources.ToList()));
        }).RequireAuthorization();


        app.MapPost("/api/admin/players/{playerId:guid}/adjust", (Guid playerId, AdminAdjustRequest request, HttpContext http) =>
            AdminAction(http, playerId, (admins, actor, target, now) =>
                admins.AdjustResource(actor, target, request.Resource, request.Delta, request.Reason, now)))
            .RequireAuthorization();


        app.MapPost("/api/admin/players/{playerId:guid}/morale", (Guid playerId, AdminMoraleRequest request, HttpContext http) =>
            AdminAction(http, playerId, (admins, actor, target, now) =>
                admins.SetMorale(actor, target, request.Morale, request.Reason, now)))
            .RequireAuthorization();


        app.MapPost("/api/admin/players/{playerId:guid}/enforcement", (Guid playerId, AdminEnforcementRequest request, HttpContext http) =>
            AdminAction(http, playerId, (admins, actor, target, now) =>
                admins.SetEnforcement(actor, target, request.Action, request.UntilUtc, request.Reason, now)))
            .RequireAuthorization();


        app.MapPost("/api/admin/players/{playerId:guid}/force-logout", (Guid playerId, AdminReasonRequest request, HttpContext http) =>
            AdminAction(http, playerId, (admins, actor, target, now) =>
                admins.ForceLogout(actor, target, request.Reason, now)))
            .RequireAuthorization();


        app.MapPost("/api/admin/players/{playerId:guid}/rename", (Guid playerId, AdminRenameRequest request, HttpContext http) =>
            AdminAction(http, playerId, (admins, actor, target, now) =>
                admins.Rename(actor, target, request.Name, request.Reason, now)))
            .RequireAuthorization();


        app.MapPost("/api/admin/players/{playerId:guid}/admin-rights", async (
            Guid playerId,
            AdminSetAdminRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            AdminService admins,
            CancellationToken ct) =>
        {
            var admin = await current.GetAsync(ct);
            if (admin is null) return Results.Unauthorized();
            if (!admin.Account.IsAdmin) return Results.Forbid();

            var target = await admins.FindPlayerAsync(playerId, ct);
            if (target is null) return Results.NotFound(new { error = "Player not found." });

            try
            {
                var summary = await admins.SetAdminAsync(admin.Account, target, request.IsAdmin, request.Reason, DateTime.UtcNow, ct);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new ActionResultResponse(summary, admin.Turns));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();


        /// <summary>
        /// Shared shape for the admin write endpoints: authorise, resolve the target, run the action, save, and
        /// turn a rule violation into a 400. Keeps AdminService the only place the logic and the audit live.
        /// </summary>
        static async Task<IResult> AdminAction(
            HttpContext http,
            Guid playerId,
            Func<AdminService, PlayerAccount, Player, DateTime, string> action)
        {
            var services = http.RequestServices;
            var current = services.GetRequiredService<CurrentPlayerService>();
            var admins = services.GetRequiredService<AdminService>();
            var db = services.GetRequiredService<GameDbContext>();

            var admin = await current.GetAsync(http.RequestAborted);
            if (admin is null) return Results.Unauthorized();
            if (!admin.Account.IsAdmin) return Results.Forbid();

            var target = await admins.FindPlayerAsync(playerId, http.RequestAborted);
            if (target is null) return Results.NotFound(new { error = "Player not found." });

            try
            {
                var summary = action(admins, admin.Account, target, DateTime.UtcNow);
                await db.SaveChangesAsync(http.RequestAborted);
                return Results.Ok(new ActionResultResponse(summary, admin.Turns));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }
    }
}
