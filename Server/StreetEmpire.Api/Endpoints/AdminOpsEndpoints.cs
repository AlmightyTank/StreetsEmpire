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

/// <summary>Overview, oversight, audit trail, live operations, runtime tuning, and AI controls.</summary>
internal static class AdminOpsEndpoints
{
    internal static void MapAdminOpsEndpoints(this IEndpointRouteBuilder app)
    {

        app.MapGet("/api/admin/overview", async (
            CurrentPlayerService current,
            GameDbContext db,
            EconomyService economy,
            IOptionsSnapshot<GameOptions> gameOptions,
            IOptions<BotAutomationOptions> botOptions,
            BotAutomationState botAutomation,
            CancellationToken ct) =>
        {
            var admin = await current.GetAsync(ct);
            if (admin is null) return Results.Unauthorized();
            if (!admin.Account.IsAdmin) return Results.Forbid();

            var totalAccounts = await db.Accounts.AsNoTracking().CountAsync(ct);
            var adminAccounts = await db.Accounts.AsNoTracking().CountAsync(x => x.IsAdmin, ct);
            var botAccounts = await db.Accounts.AsNoTracking().CountAsync(x => x.IsBot, ct);
            // Totalled by the database rather than by loading every player row.
            var totals = await db.Players.AsNoTracking()
                .GroupBy(x => 1)
                .Select(g => new
                {
                    Players = g.Count(),
                    Cash = g.Sum(x => x.Cash),
                    BankCash = g.Sum(x => x.BankCash),
                    Turns = g.Sum(x => x.Turns),
                    HoeMorale = g.Average(x => x.HoeHappiness),
                    ThugMorale = g.Average(x => x.ThugHappiness)
                })
                .SingleOrDefaultAsync(ct);
            var totalNetWorth = await db.Players.AsNoTracking().SumAsync(economy.NetWorthExpression, ct);

            return Results.Ok(new AdminOverviewResponse(
                DateTime.UtcNow,
                totalAccounts,
                adminAccounts,
                botAccounts,
                totals?.Players ?? 0,
                totals?.Cash ?? 0,
                totals?.BankCash ?? 0,
                (totals?.Cash ?? 0) + (totals?.BankCash ?? 0),
                totalNetWorth,
                totals?.Turns ?? 0,
                totals is null ? 0 : Math.Round(totals.HoeMorale, 2),
                totals is null ? 0 : Math.Round(totals.ThugMorale, 2),
                new BotAutomationStatusResponse(
                    botAutomation.Enabled,
                    Math.Clamp(botOptions.Value.TickSeconds, 15, 3600),
                    Math.Clamp(botOptions.Value.RoundsPerTick, 1, 10)),
                gameOptions.Value));
        }).RequireAuthorization();


        app.MapPost("/api/admin/bots/seed", async (
            AdminSeedBotsRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            IOptionsSnapshot<GameOptions> gameOptions,
            HideoutService hideouts,
            PimpRoster pimps,
            CancellationToken ct) =>
        {
            var admin = await current.GetAsync(ct);
            if (admin is null) return Results.Unauthorized();
            if (!admin.Account.IsAdmin) return Results.Forbid();

            var templates = BotTemplates();
            var hideoutConfig = gameOptions.Value.Hideout;
            var maxStorageLevel = hideoutConfig.Storage.Count == 0 ? 1 : hideoutConfig.Storage.Max(x => x.Level);
            var maxSafeLevel = hideoutConfig.Safe.Count == 0 ? 1 : hideoutConfig.Safe.Max(x => x.Level);
            var count = Math.Clamp(request.Count, 1, templates.Count);
            var now = DateTime.UtcNow;
            // Only the seed templates can collide, so ask about those names instead of reading every
            // account and player. lower() keeps the match case-insensitive, as the hash sets below are.
            var templateUsernames = templates.Select(x => x.Username.ToLowerInvariant()).ToList();
            var templateNames = templates.Select(x => x.Name.ToLowerInvariant()).ToList();
            var existingUsernames = (await db.Accounts.AsNoTracking()
                    .Where(x => templateUsernames.Contains(x.Username.ToLower()))
                    .Select(x => x.Username)
                    .ToListAsync(ct))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var existingNames = (await db.Players.AsNoTracking()
                    .Where(x => templateNames.Contains(x.Name.ToLower()))
                    .Select(x => x.Name)
                    .ToListAsync(ct))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var created = 0;

            foreach (var template in templates)
            {
                if (created >= count) break;
                if (existingUsernames.Contains(template.Username) || existingNames.Contains(template.Name))
                    continue;

                var player = CreateBotPlayer(template, gameOptions.Value, now, maxStorageLevel, maxSafeLevel);
                hideouts.ClampToCapacity(player);
                pimps.Reconcile(player, now);
                db.Accounts.Add(player.Account);
                db.Players.Add(player);
                db.ActionLogs.Add(new GameActionLog
                {
                    Player = player,
                    Action = "START",
                    Summary = $"{player.Name} surfaced in {player.City} with a crew of {player.Pimps} pimp(s), {player.Hoes} hoe(s), and {player.Thugs} thug(s).",
                    CashDelta = player.Cash,
                    BankDelta = player.BankCash,
                    PimpsDelta = player.Pimps,
                    HoesDelta = player.Hoes,
                    ThugsDelta = player.Thugs,
                    CondomsDelta = player.Condoms,
                    BeerDelta = player.Beer,
                    WeaponsDelta = player.Weapons,
                    WeedDelta = player.Weed,
                    CokeDelta = player.Coke,
                    CreatedAtUtc = now
                });

                existingUsernames.Add(template.Username);
                existingNames.Add(template.Name);
                created++;
            }

            await db.SaveChangesAsync(ct);
            var summary = created == 0
                ? "No new AI rivals were available to seed."
                : $"Seeded {created:N0} AI rival{(created == 1 ? string.Empty : "s")} for 0.2.0 testing.";

            return Results.Ok(new ActionResultResponse(summary, admin.Turns, new Dictionary<string, object?>
            {
                ["requested"] = request.Count,
                ["target"] = count,
                ["created"] = created
            }));
        }).RequireAuthorization();


        app.MapPost("/api/admin/bots/run", async (
            AdminRunBotsRequest request,
            CurrentPlayerService current,
            BotSimulationService bots,
            CancellationToken ct) =>
        {
            var admin = await current.GetAsync(ct);
            if (admin is null) return Results.Unauthorized();
            if (!admin.Account.IsAdmin) return Results.Forbid();

            var result = await bots.RunAsync(request.Rounds, ct);
            var summary = result.TotalBots == 0
                ? "No AI rivals exist yet. Seed AI players first."
                : $"Ran {result.Rounds:N0} AI round{(result.Rounds == 1 ? string.Empty : "s")}: {result.Actions:N0} action{(result.Actions == 1 ? string.Empty : "s")} across {result.ActiveBots:N0} active rival{(result.ActiveBots == 1 ? string.Empty : "s")}.";

            return Results.Ok(new ActionResultResponse(summary, admin.Turns, new Dictionary<string, object?>
            {
                ["totalBots"] = result.TotalBots,
                ["activeBots"] = result.ActiveBots,
                ["activeBotRounds"] = result.ActiveBotRounds,
                ["actions"] = result.Actions,
                ["rounds"] = result.Rounds
            }));
        }).RequireAuthorization();


        app.MapPut("/api/admin/bots/automation", async (
            AdminBotAutomationRequest request,
            CurrentPlayerService current,
            BotAutomationState botAutomation,
            IOptions<BotAutomationOptions> botOptions,
            CancellationToken ct) =>
        {
            var admin = await current.GetAsync(ct);
            if (admin is null) return Results.Unauthorized();
            if (!admin.Account.IsAdmin) return Results.Forbid();

            botAutomation.SetEnabled(request.Enabled);
            var summary = request.Enabled ? "Automatic AI is now on." : "Automatic AI is now off.";

            return Results.Ok(new ActionResultResponse(summary, admin.Turns, new Dictionary<string, object?>
            {
                ["enabled"] = botAutomation.Enabled,
                ["tickSeconds"] = Math.Clamp(botOptions.Value.TickSeconds, 15, 3600),
                ["roundsPerTick"] = Math.Clamp(botOptions.Value.RoundsPerTick, 1, 10)
            }));
        }).RequireAuthorization();


        app.MapPut("/api/admin/live-ops", async (
            AdminLiveOpsRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            AdminService admins,
            CancellationToken ct) =>
        {
            var admin = await current.GetAsync(ct);
            if (admin is null) return Results.Unauthorized();
            if (!admin.Account.IsAdmin) return Results.Forbid();

            var settings = await LiveOpsAsync(db, ct);
            var now = DateTime.UtcNow;
            var changes = new List<string>();

            if (request.MaintenanceMode is { } maintenance && maintenance != settings.MaintenanceMode)
            {
                settings.MaintenanceMode = maintenance;
                changes.Add(maintenance ? "maintenance on" : "maintenance off");
            }

            if (request.MaintenanceMessage is not null)
            {
                settings.MaintenanceMessage = Blank(request.MaintenanceMessage);
                changes.Add("maintenance message updated");
            }

            if (request.Announcement is not null)
            {
                settings.Announcement = Blank(request.Announcement);
                changes.Add(settings.Announcement is null ? "announcement cleared" : "announcement updated");
            }

            if (changes.Count == 0)
                return Results.BadRequest(new { error = "Nothing to change." });

            settings.UpdatedAtUtc = now;
            settings.UpdatedBy = admin.Account.Username;
            admins.Record(admin.Account, "LiveOps", null, string.Join("; ", changes), request.Reason, now);
            await db.SaveChangesAsync(ct);

            return Results.Ok(ToLiveOpsResponse(settings));
        }).RequireAuthorization();


        app.MapGet("/api/admin/config", async (
            CurrentPlayerService current,
            IOptionsSnapshot<GameOptions> gameOptions,
            GameOptionOverrides overrides,
            CancellationToken ct) =>
        {
            var admin = await current.GetAsync(ct);
            if (admin is null) return Results.Unauthorized();
            if (!admin.Account.IsAdmin) return Results.Forbid();

            // The snapshot already has overrides layered on, so these are the values the game is really using.
            var active = overrides.Snapshot();
            var settings = GameOptionPaths.Describe(gameOptions.Value)
                .Select(x => new AdminConfigEntryResponse(
                    x.Path,
                    x.Type,
                    x.CurrentValue,
                    active.TryGetValue(x.Path, out var value) ? value : null,
                    active.ContainsKey(x.Path)))
                .ToList();

            return Results.Ok(new AdminConfigResponse(overrides.Version, active.Count, settings));
        }).RequireAuthorization();


        app.MapPut("/api/admin/config", async (
            AdminConfigChangeRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            AdminService admins,
            IOptionsSnapshot<GameOptions> gameOptions,
            GameOptionOverrides overrides,
            CancellationToken ct) =>
        {
            var admin = await current.GetAsync(ct);
            if (admin is null) return Results.Unauthorized();
            if (!admin.Account.IsAdmin) return Results.Forbid();

            var path = request.Path?.Trim() ?? string.Empty;
            if (path.Length == 0)
                return Results.BadRequest(new { error = "Which setting?" });

            // Validate against a throwaway copy so a bad value never reaches the live options.
            var probe = new GameOptions();
            if (!GameOptionPaths.IsKnownPath(probe, path))
                return Results.BadRequest(new { error = $"'{path}' is not an editable setting." });

            var current_ = overrides.Snapshot().ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
            var now = DateTime.UtcNow;
            var before = GameOptionPaths.Read(gameOptions.Value, path) ?? "?";
            string summary;

            if (string.IsNullOrWhiteSpace(request.Value))
            {
                if (!current_.Remove(path))
                    return Results.BadRequest(new { error = $"'{path}' is not overridden." });
                summary = $"{path}: override cleared (was {before})";
            }
            else
            {
                if (!GameOptionPaths.TryApply(probe, path, request.Value, out var error))
                    return Results.BadRequest(new { error });
                current_[path] = request.Value.Trim();
                summary = $"{path}: {before} -> {request.Value.Trim()}";
            }

            var settings = await LiveOpsAsync(db, ct);
            settings.ConfigOverridesJson = current_.Count == 0 ? null : JsonSerializer.Serialize(current_);
            settings.UpdatedAtUtc = now;
            settings.UpdatedBy = admin.Account.Username;
            admins.Record(admin.Account, "Config", null, summary, request.Reason, now);
            await db.SaveChangesAsync(ct);

            // Swapping the map is what makes the change live: the next request rebinds through PostConfigure.
            overrides.Replace(current_);
            return Results.Ok(new ActionResultResponse(summary, admin.Turns));
        }).RequireAuthorization();


        app.MapGet("/api/admin/oversight", async (
            CurrentPlayerService current,
            GameDbContext db,
            EconomyService economy,
            CombatMissionService combatMissions,
            IOptionsSnapshot<GameOptions> gameOptions,
            CancellationToken ct) =>
        {
            var admin = await current.GetAsync(ct);
            if (admin is null) return Results.Unauthorized();
            if (!admin.Account.IsAdmin) return Results.Forbid();

            var now = DateTime.UtcNow;
            var since = now.AddHours(-24);

            // Net worth is a computed expression, so the distribution is read as bare standings.
            var standings = await db.Players.AsNoTracking()
                .Select(economy.StandingExpression())
                .ToListAsync(ct);
            var worths = standings.Select(x => x.NetWorth).OrderBy(x => x).ToList();

            // Growth is approximated from logged cash deltas: the game keeps no net worth history to diff.
            var movement = await db.ActionLogs.AsNoTracking()
                .Where(x => x.CreatedAtUtc >= since)
                .GroupBy(x => x.PlayerId)
                .Select(g => new { PlayerId = g.Key, Gained = g.Sum(x => x.CashDelta + x.BankDelta), Actions = g.Count() })
                .OrderByDescending(x => x.Gained)
                .Take(8)
                .ToListAsync(ct);
            var moverIds = movement.Select(x => x.PlayerId).ToList();
            var moverPlayers = await db.Players.AsNoTracking()
                .Include(x => x.Account)
                .Where(x => moverIds.Contains(x.Id))
                .ToListAsync(ct);
            var movers = movement
                .Join(moverPlayers, m => m.PlayerId, p => p.Id, (m, p) => new AdminMoverResponse(
                    p.Id, p.Name, p.Account.IsBot, economy.CalculateNetWorth(p), m.Gained, m.Actions))
                .ToList();

            var missions = await db.CombatMissions.AsNoTracking()
                .Include(x => x.Attacker)
                .Include(x => x.Defender)
                .Where(x => x.Status != "Complete")
                .OrderBy(x => x.StartedAtUtc)
                .Take(50)
                .ToListAsync(ct);
            var activeMissions = missions.Select(mission =>
            {
                var nextAt = mission.Status switch
                {
                    "Traveling" => mission.ArrivesAtUtc,
                    "Fighting" => mission.NextRoundAtUtc,
                    "Returning" => mission.ReturnsAtUtc,
                    _ => null
                };
                // Anything more than five minutes past its next event is stuck, not in flight.
                var overdue = nextAt is { } due && due < now.AddMinutes(-5);
                return new AdminMissionResponse(
                    mission.Id,
                    mission.Attacker.Name,
                    mission.Defender.Name,
                    mission.CommanderName,
                    mission.Status,
                    mission.Outcome,
                    mission.CurrentRound,
                    mission.MaxRounds,
                    mission.StartedAtUtc,
                    nextAt,
                    overdue);
            }).ToList();

            var bots = await db.Players.AsNoTracking()
                .Include(x => x.Account)
                .Where(x => x.Account.IsBot)
                .ToListAsync(ct);
            var botIds = bots.Select(x => x.Id).ToList();
            var lastActions = await db.ActionLogs.AsNoTracking()
                .Where(x => botIds.Contains(x.PlayerId))
                .GroupBy(x => x.PlayerId)
                .Select(g => new { PlayerId = g.Key, Last = g.Max(x => x.CreatedAtUtc) })
                .ToDictionaryAsync(x => x.PlayerId, x => x.Last, ct);
            var botHealth = bots
                .Select(bot =>
                {
                    var last = lastActions.TryGetValue(bot.Id, out var at) ? at : (DateTime?)null;
                    return new AdminBotHealthResponse(
                        bot.Id,
                        bot.Name,
                        BotBrain.For(bot).Name,
                        economy.CalculateNetWorth(bot),
                        last,
                        last is { } value ? (int)Math.Max(0, (now - value).TotalMinutes) : int.MaxValue);
                })
                .OrderByDescending(x => x.MinutesIdle)
                .ToList();

            return Results.Ok(new AdminOversightResponse(
                WealthStats.Median(worths),
                worths.Count == 0 ? 0 : worths[^1],
                WealthStats.GiniPercent(worths),
                WealthStats.WealthBands(worths),
                movers,
                activeMissions,
                botHealth));
        }).RequireAuthorization();


        app.MapPost("/api/admin/missions/{missionId:long}/force-resolve", async (
            long missionId,
            CurrentPlayerService current,
            GameDbContext db,
            AdminService admins,
            CombatSchedule schedule,
            CombatResolutionService combatResolver,
            CancellationToken ct) =>
        {
            var admin = await current.GetAsync(ct);
            if (admin is null) return Results.Unauthorized();
            if (!admin.Account.IsAdmin) return Results.Forbid();

            var mission = await db.CombatMissions
                .Include(x => x.Attacker)
                .Include(x => x.Defender)
                .SingleOrDefaultAsync(x => x.Id == missionId, ct);
            if (mission is null) return Results.NotFound(new { error = "Mission not found." });
            if (mission.Status == "Complete") return Results.BadRequest(new { error = "That mission is already complete." });

            // Pulls every timer into the past, then lets the normal resolver finish it through the usual rules
            // rather than hand-writing an outcome.
            var now = DateTime.UtcNow;
            if (mission.Status == "Traveling") mission.ArrivesAtUtc = now;
            if (mission.NextRoundAtUtc is not null) mission.NextRoundAtUtc = now;
            if (mission.ReturnsAtUtc is not null) mission.ReturnsAtUtc = now;
            admins.Record(admin.Account, "ForceResolve", mission.Attacker,
                $"mission {mission.Id} ({mission.Status}) against {mission.Defender.Name} pushed to resolve", null, now);
            await db.SaveChangesAsync(ct);

            schedule.Invalidate();
            var updates = await combatResolver.ResolveDueAsync(DateTime.UtcNow, ct);
            return Results.Ok(new ActionResultResponse($"Pushed mission {missionId} through the resolver ({updates:N0} update(s)).", admin.Turns));
        }).RequireAuthorization();


        app.MapGet("/api/admin/audit", async (
            CurrentPlayerService current,
            AdminService admins,
            CancellationToken ct) =>
        {
            var admin = await current.GetAsync(ct);
            if (admin is null) return Results.Unauthorized();
            if (!admin.Account.IsAdmin) return Results.Forbid();

            var entries = await admins.AuditTrail()
                .Take(100)
                .Select(x => new AdminAuditEntryResponse(x.Id, x.ActorUsername, x.Action, x.TargetPlayerId, x.TargetName, x.Summary, x.Reason, x.CreatedAtUtc))
                .ToListAsync(ct);
            return Results.Ok(entries);
        }).RequireAuthorization();
    }
}
