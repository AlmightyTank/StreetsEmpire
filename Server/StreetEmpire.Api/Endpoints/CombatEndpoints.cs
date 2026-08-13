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

/// <summary>Target recon, public profiles, and combat missions.</summary>
internal static class CombatEndpoints
{
    internal static void MapCombatEndpoints(this IEndpointRouteBuilder app)
    {

        app.MapGet("/api/game/targets", async (
            string? query,
            CurrentPlayerService current,
            GameDbContext db,
            EconomyService economy,
            IOptionsSnapshot<GameOptions> gameOptions,
            CombatMissionService combatMissions,
            CombatResolutionService combatResolver,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var normalizedQuery = query?.Trim() ?? string.Empty;
            var now = DateTime.UtcNow;
            await combatResolver.ResolveDueAsync(now, ct);
            var laneReadyAt = await combatMissions.LaneReadyAtUtcAsync(player.Id, now, ct);
            var viewerNetWorth = economy.CalculateNetWorth(player);

            var candidates = db.Players
                .Include(x => x.Account)
                .AsNoTracking()
                .Where(x => x.Id != player.Id);
            if (normalizedQuery.Length > 0)
            {
                // ILIKE keeps the search case-insensitive the way the old in-memory Contains was.
                var pattern = ToLikePattern(normalizedQuery);
                candidates = candidates.Where(x =>
                    EF.Functions.ILike(x.Name, pattern, "\\")
                    || EF.Functions.ILike(x.City, pattern, "\\"));
            }

            var page = await candidates
                .OrderByDescending(economy.NetWorthExpression)
                .ThenBy(x => x.CreatedAtUtc)
                .Take(20)
                .ToListAsync(ct);
            var ranked = await RankPageAsync(page, db, economy, ct);
            var targets = ranked
                .Select(x => ToTargetResponse(x, now, player, gameOptions.Value, viewerLaneReadyAtUtc: laneReadyAt, viewerNetWorth: viewerNetWorth))
                .ToList();

            return Results.Ok(targets);
        }).RequireAuthorization();


        app.MapGet("/api/game/players/{playerId:guid}/profile", async (
            Guid playerId,
            CurrentPlayerService current,
            GameDbContext db,
            EconomyService economy,
            IOptionsSnapshot<GameOptions> gameOptions,
            CombatMissionService combatMissions,
            CombatResolutionService combatResolver,
            CancellationToken ct) =>
        {
            var viewer = await current.GetAsync(ct);
            if (viewer is null) return Results.Unauthorized();

            var now = DateTime.UtcNow;
            await combatResolver.ResolveDueAsync(now, ct);
            var laneReadyAt = await combatMissions.LaneReadyAtUtcAsync(viewer.Id, now, ct);
            var subject = await db.Players
                .Include(x => x.Account)
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == playerId, ct);
            if (subject is null) return Results.NotFound(new { error = "Player not found." });

            var subjectNetWorth = economy.CalculateNetWorth(subject);
            var subjectRank = await db.Players.AsNoTracking()
                .CountAsync(economy.RanksAbove(subjectNetWorth, subject.CreatedAtUtc), ct) + 1;
            var target = new RankedPlayer(subject, subjectNetWorth, subjectRank);

            var activity = await db.ActionLogs.AsNoTracking()
                .Where(x => x.PlayerId == playerId && x.Action != "ADMIN" && x.Action != "STORE")
                .OrderByDescending(x => x.CreatedAtUtc)
                .ThenByDescending(x => x.Id)
                .Take(8)
                .Select(x => new ActivityResponse(
                    x.Id,
                    x.Action,
                    x.Summary,
                    x.TurnsSpent,
                    x.CashDelta,
                    x.BankDelta,
                    x.CreatedAtUtc))
                .ToListAsync(ct);
            var combatSince = now.AddDays(-1);
            var recentAttacksMade = await db.CombatLogs.AsNoTracking()
                .CountAsync(x => x.AttackerId == playerId && x.CreatedAtUtc >= combatSince, ct);
            var recentDefenses = await db.CombatLogs.AsNoTracking()
                .CountAsync(x => x.DefenderId == playerId && x.CreatedAtUtc >= combatSince, ct);

            return Results.Ok(ToProfileResponse(target, activity, now, viewer, gameOptions.Value, recentAttacksMade, recentDefenses, laneReadyAt, economy.CalculateNetWorth(viewer)));
        }).RequireAuthorization();


        app.MapGet("/api/game/combat/logs", async (
            CurrentPlayerService current,
            GameDbContext db,
            CombatResolutionService combatResolver,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            await combatResolver.ResolveDueAsync(DateTime.UtcNow, ct);
            var combatLogs = await db.CombatLogs.AsNoTracking()
                .Include(x => x.Attacker)
                .Include(x => x.Defender)
                .Where(x => x.AttackerId == player.Id || x.DefenderId == player.Id)
                .OrderByDescending(x => x.CreatedAtUtc)
                .ThenByDescending(x => x.Id)
                .Take(100)
                .ToListAsync(ct);

            var logs = combatLogs
                .DistinctBy(CombatLogDedupeKey)
                .Take(30)
                .Select(ToCombatLogResponse)
                .ToList();
            return Results.Ok(logs);
        }).RequireAuthorization();


        app.MapGet("/api/game/combat/missions", async (
            CurrentPlayerService current,
            CombatMissionService combatMissions,
            CombatResolutionService combatResolver,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            await combatResolver.ResolveDueAsync(DateTime.UtcNow, ct);
            var missions = await combatMissions.VisibleMissions(player.Id).ToListAsync(ct);
            return Results.Ok(missions.Select(ToCombatMissionResponse).ToList());
        }).RequireAuthorization();


        // ----- Defender alerts -----

        app.MapGet("/api/game/alerts", async (
            CurrentPlayerService current,
            GameDbContext db,
            CombatResolutionService combatResolver,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            await combatResolver.ResolveDueAsync(DateTime.UtcNow, ct);
            var logs = await db.CombatLogs.AsNoTracking()
                .Include(x => x.Attacker)
                .Where(x => x.DefenderId == player.Id && x.Outcome != "Pending")
                .OrderByDescending(x => x.CreatedAtUtc)
                .ThenByDescending(x => x.Id)
                .Take(25)
                .ToListAsync(ct);

            var alerts = logs.Select(x => DefenceAlerts.Describe(x, player.CombatAlertsSeenAtUtc)).ToList();
            return Results.Ok(new DefenceAlertsResponse(
                DefenceAlerts.UnreadCount(alerts),
                player.CombatAlertsSeenAtUtc,
                alerts));
        }).RequireAuthorization();

        app.MapPost("/api/game/alerts/seen", async (
            CurrentPlayerService current,
            GameDbContext db,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            // Moving the watermark forward is all "mark read" means.
            player.CombatAlertsSeenAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new DefenceAlertsResponse(0, player.CombatAlertsSeenAtUtc, []));
        }).RequireAuthorization();

        app.MapPost("/api/game/combat/attack", async (
            CombatAttackRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            TurnService turns,
            CombatMissionService combatMissions,
            CombatResolutionService combatResolver,
            CancellationToken ct) =>
        {
            var attacker = await current.GetAsync(ct);
            if (attacker is null) return Results.Unauthorized();

            var now = DateTime.UtcNow;
            await combatResolver.ResolveDueAsync(now, ct);

            var defender = await db.Players
                .Include(x => x.Account)
                .SingleOrDefaultAsync(x => x.Id == request.DefenderId, ct);
            if (defender is null) return Results.NotFound(new { error = "Target not found." });

            turns.Refresh(attacker, now);
            var before = Snapshot(attacker);
            try
            {
                var mission = await combatMissions.LaunchAsync(attacker, defender, request, now, ct);
                AddLog(db, attacker, before, "ATTACK", mission.TurnsSpent, mission.Summary);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new ActionResultResponse(mission.Summary, attacker.Turns, new Dictionary<string, object?>
                {
                    ["missionId"] = mission.Id,
                    ["status"] = mission.Status,
                    ["assignedPimps"] = mission.AssignedPimps,
                    ["assignedThugs"] = mission.AssignedThugs,
                    ["assignedWeapons"] = mission.AssignedWeapons,
                    ["arrivesAtUtc"] = mission.ArrivesAtUtc
                }));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();


        app.MapPost("/api/game/combat/missions/{missionId:long}/cancel", async (
            long missionId,
            CurrentPlayerService current,
            CombatMissionService combatMissions,
            CombatResolutionService combatResolver,
            CancellationToken ct) =>
        {
            var attacker = await current.GetAsync(ct);
            if (attacker is null) return Results.Unauthorized();

            var now = DateTime.UtcNow;
            await combatResolver.ResolveDueAsync(now, ct);

            try
            {
                var result = await combatMissions.CancelAsync(attacker, missionId, now, ct);
                var mission = result.Mission;
                return Results.Ok(new ActionResultResponse(mission.Summary, attacker.Turns, new Dictionary<string, object?>
                {
                    ["missionId"] = mission.Id,
                    ["status"] = mission.Status,
                    ["outcome"] = mission.Outcome,
                    ["cancelCashCost"] = result.Cost
                }));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();
    }
}
