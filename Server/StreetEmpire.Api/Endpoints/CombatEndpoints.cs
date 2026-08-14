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

        // Fetched once on arrival. Reading it advances the watermark, so a refresh does not replay a
        // digest the player has already seen.
        app.MapGet("/api/game/catch-up", async (
            CurrentPlayerService current,
            GameDbContext db,
            PlayerClock clock,
            CombatResolutionService combatResolver,
            IOptionsSnapshot<GameOptions> gameOptions,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var now = DateTime.UtcNow;
            await combatResolver.ResolveDueAsync(now, ct);
            // Settle first, and save before reading. The labs and any finished build are usually
            // settled by this very request, and the queries below go to the database: without the save
            // they would miss rows sitting unsaved in the change tracker and the player would be told
            // about their own lab run one visit late.
            if (clock.Advance(player, now, db).Changed)
                await db.SaveChangesAsync(ct);

            // A player who has never had a digest has no "away" to report. Start their watermark and
            // say nothing rather than summarising their whole history at them.
            if (player.CatchUpSeenAtUtc is not { } since)
            {
                player.CatchUpSeenAtUtc = now;
                await db.SaveChangesAsync(ct);
                return Results.Ok(new CatchUpResponse(now, 0, false, []));
            }

            var defences = await db.CombatLogs.AsNoTracking()
                .Where(x => x.DefenderId == player.Id && x.Outcome != "Pending" && x.CreatedAtUtc > since)
                .Select(x => new
                {
                    x.Outcome,
                    x.CashStolen,
                    x.WeedStolen,
                    x.CokeStolen,
                    x.DefenderThugsLost,
                    x.DefenderPimpsLost
                })
                .ToListAsync(ct);

            var passive = await db.ActionLogs.AsNoTracking()
                .Where(x => x.PlayerId == player.Id && x.Action == "LAB" && x.CreatedAtUtc > since)
                .GroupBy(x => 1)
                .Select(g => new { Weed = g.Sum(x => x.WeedDelta), Coke = g.Sum(x => x.CokeDelta) })
                .FirstOrDefaultAsync(ct);

            var builds = await db.ActionLogs.AsNoTracking()
                .Where(x => x.PlayerId == player.Id && x.Action == "HIDEOUT" && x.CreatedAtUtc > since && x.Summary.EndsWith(" is finished."))
                .OrderBy(x => x.CreatedAtUtc)
                .Select(x => x.Summary)
                .ToListAsync(ct);

            // Both readings come from standings samples, never from a live figure. Two samples share a
            // ranking of the same players at the same instants, so crossings between them are real.
            //
            // It also stops the digest repeating itself. Compared against a live rank, the baseline
            // stays the nearest sample even after the watermark advances, so the same "you climbed to
            // #3" reappeared on every refresh. Once the watermark passes the newest sample the two
            // readings are the same row and there is nothing left to report.
            var baselineAt = await db.StandingSnapshots.AsNoTracking()
                .Where(x => x.PlayerId == player.Id && x.TakenAtUtc <= since)
                .OrderByDescending(x => x.TakenAtUtc)
                .Select(x => (DateTime?)x.TakenAtUtc)
                .FirstOrDefaultAsync(ct);
            var latestAt = await db.StandingSnapshots.AsNoTracking()
                .Where(x => x.PlayerId == player.Id)
                .OrderByDescending(x => x.TakenAtUtc)
                .Select(x => (DateTime?)x.TakenAtUtc)
                .FirstOrDefaultAsync(ct);

            int? rankBefore = null;
            int? rankNow = null;
            var overtookYou = new List<string>();
            var youOvertook = new List<string>();
            if (baselineAt is { } thenAt && latestAt is { } nowAt && thenAt < nowAt)
            {
                var samples = await db.StandingSnapshots.AsNoTracking()
                    .Where(x => x.TakenAtUtc == thenAt || x.TakenAtUtc == nowAt)
                    .Select(x => new { x.PlayerId, x.Rank, x.TakenAtUtc, Name = x.Player.Name })
                    .ToListAsync(ct);

                var before = samples.Where(x => x.TakenAtUtc == thenAt).ToList();
                var after = samples.Where(x => x.TakenAtUtc == nowAt).ToList();
                rankBefore = before.SingleOrDefault(x => x.PlayerId == player.Id)?.Rank;
                rankNow = after.SingleOrDefault(x => x.PlayerId == player.Id)?.Rank;

                if (rankBefore is { } wasRanked && rankNow is { } isRanked)
                {
                    var aheadThen = before.Where(x => x.Rank < wasRanked).Select(x => x.PlayerId).ToHashSet();
                    var aheadNow = after.Where(x => x.Rank < isRanked).ToList();
                    var nameOf = before.ToDictionary(x => x.PlayerId, x => x.Name);

                    // Ahead now but not ahead then, and the reverse. Anyone missing from the older
                    // sample is skipped rather than guessed at: a player who did not exist then cannot
                    // have overtaken anybody.
                    overtookYou = aheadNow.Where(x => !aheadThen.Contains(x.PlayerId) && nameOf.ContainsKey(x.PlayerId)).Select(x => x.Name).ToList();
                    var aheadNowIds = aheadNow.Select(x => x.PlayerId).ToHashSet();
                    youOvertook = aheadThen.Where(id => !aheadNowIds.Contains(id)).Select(id => nameOf[id]).ToList();
                }
            }

            var digest = CatchUp.Build(new CatchUpFacts(
                since,
                now,
                defences.Count,
                defences.Count(x => x.Outcome != "Victory"),
                defences.Sum(x => x.CashStolen),
                defences.Sum(x => x.WeedStolen),
                defences.Sum(x => x.CokeStolen),
                defences.Sum(x => x.DefenderThugsLost),
                defences.Sum(x => x.DefenderPimpsLost),
                passive?.Weed ?? 0,
                passive?.Coke ?? 0,
                builds,
                player.Turns,
                gameOptions.Value.MaxTurns,
                player.CombatProtectionUntilUtc,
                rankBefore,
                rankNow,
                overtookYou,
                youOvertook));

            player.CatchUpSeenAtUtc = now;
            await db.SaveChangesAsync(ct);
            return Results.Ok(digest);
        }).RequireAuthorization();

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
            PlayerClock clock,
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

            clock.Advance(attacker, now, db);
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
