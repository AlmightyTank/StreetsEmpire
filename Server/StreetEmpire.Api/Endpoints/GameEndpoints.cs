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

/// <summary>The core economy loop: dashboard, street work, production, store, bank, crew, hideout.</summary>
internal static class GameEndpoints
{
    internal static void MapGameEndpoints(this IEndpointRouteBuilder app)
    {

        app.MapGet("/api/game/dashboard", async (
            CurrentPlayerService current,
            GameDbContext db,
            PlayerClock clock,
            EconomyService economy,
            IOptionsSnapshot<GameOptions> gameOptions,
            HideoutService hideouts,
            PimpRoster pimps,
            CombatMissionService combatMissions,
            CombatResolutionService combatResolver,
            StandingsRecorder standings,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var now = DateTime.UtcNow;
            await combatResolver.ResolveDueAsync(now, ct);
            if ((await clock.AdvanceAsync(player, now, db, ct)).Changed)
                await db.SaveChangesAsync(ct);
            // Sampled from the busiest read in the game, behind a timer, so standings history builds up
            // as a side effect of anyone playing rather than needing a background service of its own.
            await standings.SampleIfDueAsync(now, ct);

            var netWorth = economy.CalculateNetWorth(player);
            var combatSince = now.AddDays(-1);
            var recentAttacksMade = await db.CombatLogs.AsNoTracking()
                .CountAsync(x => x.AttackerId == player.Id && x.CreatedAtUtc >= combatSince, ct);
            var recentDefenses = await db.CombatLogs.AsNoTracking()
                .CountAsync(x => x.DefenderId == player.Id && x.CreatedAtUtc >= combatSince, ct);
            var unreadAlerts = await db.CombatLogs.AsNoTracking()
                .CountAsync(x => x.DefenderId == player.Id
                                 && x.Outcome != "Pending"
                                 && (player.CombatAlertsSeenAtUtc == null || x.CreatedAtUtc > player.CombatAlertsSeenAtUtc), ct)
                + await db.ActionLogs.AsNoTracking()
                    .Where(DefenceAlerts.IsNotificationRow)
                    .CountAsync(x => x.PlayerId == player.Id
                                     && (player.CombatAlertsSeenAtUtc == null || x.CreatedAtUtc > player.CombatAlertsSeenAtUtc), ct);
            var combatCrew = await combatMissions.CommitmentAsync(player, ct);
            var laneReadyAt = await combatMissions.LaneReadyAtUtcAsync(player.Id, now, ct);
            var commandingPimpIds = await combatMissions.ActiveAttackMissions(player.Id)
                .Where(x => x.CommanderPimpId != null)
                .Select(x => x.CommanderPimpId!.Value)
                .ToListAsync(ct);
            var rank = await db.Players.AsNoTracking()
                .CountAsync(economy.RanksAbove(netWorth, player.CreatedAtUtc), ct) + 1;
            // Baseline is the morale going into the player's most recent action, so the arrow reports
            // the direction morale is moving now: the last action's own effect plus whatever has
            // recovered since. Measured from the oldest row in the window instead, the arrow kept
            // reporting a crash for hours after it was over, pointing down while morale climbed.
            //
            // The window is now only a staleness bound. A baseline older than it says nothing useful
            // about the present, so the arrow is withheld rather than guessed.
            var opts = gameOptions.Value;
            var trendSince = now.AddHours(-Math.Max(1, opts.Morale.TrendWindowHours));
            var moraleBaseline = await db.ActionLogs.AsNoTracking()
                .Where(x => x.PlayerId == player.Id && x.CreatedAtUtc >= trendSince && x.HoeMoraleBefore != null)
                .OrderByDescending(x => x.CreatedAtUtc)
                .ThenByDescending(x => x.Id)
                .Select(x => new { Hoe = x.HoeMoraleBefore, Thug = x.ThugMoraleBefore })
                .FirstOrDefaultAsync(ct);
            var moraleTrend = ToMoraleTrend(player, moraleBaseline?.Hoe, moraleBaseline?.Thug, opts.Morale);

            // Notifications are excluded: this list is what the player did, and a lab payout they had
            // no hand in reads as an action they took. They surface in the alert bell instead.
            var activity = await db.ActionLogs.AsNoTracking()
                .Where(x => x.PlayerId == player.Id)
                .Where(DefenceAlerts.IsActionRow)
                .OrderByDescending(x => x.CreatedAtUtc)
                .Take(12)
                .Select(x => new ActivityResponse(
                    x.Id, x.Action, x.Summary, x.TurnsSpent, x.CashDelta, x.BankDelta, x.CreatedAtUtc))
                .ToListAsync(ct);

            return Results.Ok(new DashboardResponse(
                player.Id,
                player.Name,
                player.Account.IsAdmin,
                player.City,
                player.Cash,
                player.BankCash,
                netWorth,
                rank,
                player.Turns,
                opts.MaxTurns,
                opts.MaxActionTurns,
                opts.TurnsPerTick,
                opts.TurnTickMinutes,
                clock.SecondsUntilNextTick(player, now),
                player.Pimps,
                player.Hoes,
                player.Thugs,
                player.HoeCutPercent,
                Math.Round(player.HoeHappiness, 2),
                Math.Round(player.ThugHappiness, 2),
                moraleTrend,
                player.Condoms,
                player.Beer,
                player.Weapons,
                player.Weed,
                player.Coke,
                opts.WeedSellPrice,
                opts.CokeSellPrice,
                economy.GetCrewReport(player),
                ToHideoutResponse(player, hideouts, now, opts),
                pimps.Active(player).Select(x => ToPimpResponse(x, commandingPimpIds)).ToList(),
                pimps.Fallen(player).Take(12).Select(x => ToPimpResponse(x, commandingPimpIds)).ToList(),
                ToCombatCrewResponse(combatCrew),
                ToCombatStatus(player, now, player, opts, recentAttacksMade, recentDefenses, laneReadyAt),
                unreadAlerts,
                economy.GetStore(),
                activity));
        }).RequireAuthorization();


        app.MapPost("/api/game/street", async (
            ScoutRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            PlayerClock clock,
            EconomyService economy,
            TerritoryService territories,
            CombatResolutionService combatResolver,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var now = DateTime.UtcNow;
            await combatResolver.ResolveDueAsync(now, ct);
            var pendingAttack = await ActiveOutgoingMissionAsync(db, player.Id, ct);
            if (pendingAttack is not null)
                return Results.BadRequest(new { error = PendingAttackMessage(pendingAttack) });

            await clock.AdvanceAsync(player, now, db, ct);
            var before = Snapshot(player);
            try
            {
                var result = economy.Scout(player, request.Turns, request.AutoBuySupplies, await territories.EffectsForAsync(player.Id, ct), await territories.GarrisonedPimpIdsAsync(player.Id, ct));
                AddLog(db, player, before, "STREET", request.Turns, result.Summary);
                await db.SaveChangesAsync(ct);
                return Results.Ok(result);
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();


        // Keep the 0.1.0 route as a compatibility alias while the UI moves to /street.
        app.MapPost("/api/game/scout", async (
            ScoutRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            PlayerClock clock,
            EconomyService economy,
            TerritoryService territories,
            CombatResolutionService combatResolver,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var now = DateTime.UtcNow;
            await combatResolver.ResolveDueAsync(now, ct);
            var pendingAttack = await ActiveOutgoingMissionAsync(db, player.Id, ct);
            if (pendingAttack is not null)
                return Results.BadRequest(new { error = PendingAttackMessage(pendingAttack) });

            await clock.AdvanceAsync(player, now, db, ct);
            var before = Snapshot(player);
            try
            {
                var result = economy.Scout(player, request.Turns, request.AutoBuySupplies, await territories.EffectsForAsync(player.Id, ct), await territories.GarrisonedPimpIdsAsync(player.Id, ct));
                AddLog(db, player, before, "STREET", request.Turns, result.Summary);
                await db.SaveChangesAsync(ct);
                return Results.Ok(result);
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();


        app.MapPost("/api/game/production", async (
            ProduceRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            PlayerClock clock,
            EconomyService economy,
            TerritoryService territories,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            await clock.AdvanceAsync(player, DateTime.UtcNow, db, ct);
            var before = Snapshot(player);
            try
            {
                var result = economy.Produce(player, request.Product, request.Turns, await territories.EffectsForAsync(player.Id, ct));
                AddLog(db, player, before, "PRODUCTION", request.Turns, result.Summary);
                await db.SaveChangesAsync(ct);
                return Results.Ok(result);
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();


        app.MapPost("/api/game/product/sell", async (
            SellProductRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            EconomyService economy,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var before = Snapshot(player);
            try
            {
                var result = economy.SellProduct(player, request.Product, request.Quantity);
                AddLog(db, player, before, "SALE", 0, result.Summary);
                await db.SaveChangesAsync(ct);
                return Results.Ok(result);
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();


        app.MapGet("/api/game/store", (EconomyService economy) => Results.Ok(economy.GetStore()))
            .RequireAuthorization();


        app.MapPost("/api/game/store/buy", async (
            StoreBuyRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            EconomyService economy,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var before = Snapshot(player);
            try
            {
                var result = economy.BuyStoreItem(player, request.ItemKey, request.Quantity);
                AddLog(db, player, before, "STORE", 0, result.Summary);
                await db.SaveChangesAsync(ct);
                return Results.Ok(result);
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();


        app.MapPost("/api/game/bank/deposit", async (
            BankRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            EconomyService economy,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var before = Snapshot(player);
            try
            {
                var result = economy.Deposit(player, request.Amount);
                AddLog(db, player, before, "BANK", 0, result.Summary);
                await db.SaveChangesAsync(ct);
                return Results.Ok(result);
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();


        app.MapPost("/api/game/bank/withdraw", async (
            BankRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            EconomyService economy,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var before = Snapshot(player);
            try
            {
                var result = economy.Withdraw(player, request.Amount);
                AddLog(db, player, before, "BANK", 0, result.Summary);
                await db.SaveChangesAsync(ct);
                return Results.Ok(result);
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();


        app.MapPut("/api/game/crew/settings", async (
            UpdateCrewSettingsRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            EconomyService economy,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var before = Snapshot(player);
            try
            {
                var result = economy.UpdateCrewSettings(player, request.HoeCutPercent);
                AddLog(db, player, before, "CREW", 0, result.Summary);
                await db.SaveChangesAsync(ct);
                return Results.Ok(result);
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();


        app.MapPost("/api/game/crew/hire", async (
            CrewRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            EconomyService economy,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var before = Snapshot(player);
            try
            {
                var result = economy.HireCrew(player, request.Role, request.Quantity);
                AddLog(db, player, before, "CREW", 0, result.Summary);
                await db.SaveChangesAsync(ct);
                return Results.Ok(result);
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();


        app.MapPost("/api/game/crew/fire", async (
            CrewRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            EconomyService economy,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var before = Snapshot(player);
            try
            {
                var result = economy.FireCrew(player, request.Role, request.Quantity);
                AddLog(db, player, before, "CREW", 0, result.Summary);
                await db.SaveChangesAsync(ct);
                return Results.Ok(result);
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();


        app.MapPost("/api/game/hideout/recover", async (
            MoraleRecoveryRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            PlayerClock clock,
            EconomyService economy,
            TerritoryService territories,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var now = DateTime.UtcNow;
            await clock.AdvanceAsync(player, now, db, ct);
            var before = Snapshot(player);
            try
            {
                var result = economy.RecoverCrewMorale(player, request.Strategy);
                AddLog(db, player, before, "HIDEOUT", result.Breakdown is not null && result.Breakdown.TryGetValue("turnsSpent", out var turnsSpent)
                    ? Convert.ToInt32(turnsSpent)
                    : 0,
                    result.Summary);
                await db.SaveChangesAsync(ct);
                return Results.Ok(result);
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();


        app.MapPost("/api/game/hideout/upgrade", async (
            HideoutUpgradeRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            HideoutService hideouts,
            PlayerClock clock,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            // Moving up a tier is paid for in turns, so bring the accrued ones in before charging.
            var now = DateTime.UtcNow;
            await clock.AdvanceAsync(player, now, db, ct);
            var before = Snapshot(player);
            try
            {
                var result = hideouts.Upgrade(player, request.Room, now);
                AddLog(db, player, before, "HIDEOUT", 0, result.Summary);
                await db.SaveChangesAsync(ct);
                return Results.Ok(result);
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();


        // ----- Live operations -----

        // Readable by any signed-in player: the client needs the banner and the maintenance notice.
        app.MapGet("/api/game/live-ops", async (GameDbContext db, CancellationToken ct) =>
            Results.Ok(ToLiveOpsResponse(await LiveOpsAsync(db, ct)))).RequireAuthorization();


        static Task<CombatMission?> ActiveOutgoingMissionAsync(GameDbContext db, Guid playerId, CancellationToken cancellationToken)
            => db.CombatMissions.AsNoTracking()
                .Where(x => x.AttackerId == playerId && x.Status != "Complete")
                .OrderBy(x => x.ReturnsAtUtc ?? x.NextRoundAtUtc ?? x.ArrivesAtUtc)
                .ThenBy(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);

        static string PendingAttackMessage(CombatMission mission)
        {
            var nextAt = mission.Status switch
            {
                "Traveling" => mission.ArrivesAtUtc,
                "Fighting" => mission.NextRoundAtUtc,
                "Returning" => mission.ReturnsAtUtc,
                _ => null
            };
            return nextAt is { } value
                ? $"Your crew is out on an attack mission. Next update in {FormatDuration(Math.Max(0, (int)Math.Ceiling((value - DateTime.UtcNow).TotalSeconds)))}."
                : "Your crew is out on an attack mission.";
        }
    }
}
