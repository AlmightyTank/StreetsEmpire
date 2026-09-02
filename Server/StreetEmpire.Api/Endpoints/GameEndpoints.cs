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
    /// <summary>
    /// The sentence a sweep adds to the shift that caused it, or nothing at all.
    ///
    /// Says what it costs to answer and how long there is to answer it, because those are the two
    /// numbers the decision needs and a player who has to go and look them up has been told they lost
    /// people rather than that they have a choice to make.
    /// </summary>
    private static string Describe(Arrest? arrest)
    {
        if (arrest is null) return string.Empty;

        var taken = new List<string>();
        if (arrest.Hoes > 0) taken.Add($"{arrest.Hoes:N0} hoe(s)");
        if (arrest.Thugs > 0) taken.Add($"{arrest.Thugs:N0} thug(s)");
        if (arrest.PimpName is not null) taken.Add(arrest.PimpName);
        var hours = Math.Max(1, (int)Math.Round((arrest.BailDeadlineUtc - arrest.ArrestedAtUtc).TotalHours));
        return $" The law swept up {string.Join(" and ", taken)}."
               + $" Bail is ${arrest.BailAmount:N0}, and you have {hours} hour(s) to pay it.";
    }

    internal static void MapGameEndpoints(this IEndpointRouteBuilder app)
    {

        app.MapGet("/api/game/dashboard", async (
            CurrentPlayerService current,
            GameDbContext db,
            PlayerClock clock,
            EconomyService economy,
            GuidanceService guidance,
            IOptionsSnapshot<GameOptions> gameOptions,
            HideoutService hideouts,
            PimpRoster pimps,
            CombatMissionService combatMissions,
            CombatResolutionService combatResolver,
            StreetStrikeService strikes,
            AllianceService allianceRules,
            StandingsRecorder standings,
            SeasonService seasons,
            TraderShelfService shelves,
            CancellationToken ct) =>
        {
            // Before the player is loaded, not after. The world may be due to start over, and a
            // dashboard landing in the same second as a roll should show the new season rather than
            // the last minute of the old one. Does nothing at all unless an operator has turned
            // seasons on and the clock has actually run out.
            await seasons.RollIfDueAsync(DateTime.UtcNow, ct);

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
            // The same count narrowed to one town, so both ranks come from one definition of who
            // outranks whom rather than from two that could disagree.
            var cityRank = await db.Players.AsNoTracking()
                .Where(x => x.City == player.City)
                .CountAsync(economy.RanksAbove(netWorth, player.CreatedAtUtc), ct) + 1;
            var cityPlayers = await db.Players.AsNoTracking().CountAsync(x => x.City == player.City, ct);
            // Baseline is the morale going into the player's most recent action, so the arrow reports
            // the direction morale is moving now: the last action's own effect plus whatever has
            // recovered since. Measured from the oldest row in the window instead, the arrow kept
            // reporting a crash for hours after it was over, pointing down while morale climbed.
            //
            // The window is now only a staleness bound. A baseline older than it says nothing useful
            // about the present, so the arrow is withheld rather than guessed.
            var opts = gameOptions.Value;
            var cityMarkets = ToCityMarkets(opts, player);
            var currentMarket = cityMarkets.First(x => x.Current);
            // Reported up front so the travel panel can say why it is closed. Without it the buttons
            // render live and the player only learns about the garrison or the mission by clicking.
            var travel = new TravelStatusResponse(
                await TravelBlockedReasonAsync(db, player.Id, ct),
                economy.CarriedValue(player),
                (int)Math.Round(opts.CityMarkets.SeizureMinPercent * 100, MidpointRounding.AwayFromZero),
                (int)Math.Round(opts.CityMarkets.SeizureMaxPercent * 100, MidpointRounding.AwayFromZero));
            var trendSince = now.AddHours(-Math.Max(1, opts.Morale.TrendWindowHours));
            var moraleBaseline = await db.ActionLogs.AsNoTracking()
                .Where(x => x.PlayerId == player.Id && x.CreatedAtUtc >= trendSince && x.HoeMoraleBefore != null)
                .OrderByDescending(x => x.CreatedAtUtc)
                .ThenByDescending(x => x.Id)
                .Select(x => new { Hoe = x.HoeMoraleBefore, Thug = x.ThugMoraleBefore })
                .FirstOrDefaultAsync(ct);
            var moraleTrend = ToMoraleTrend(player, moraleBaseline?.Hoe, moraleBaseline?.Thug, opts.Morale);

            // Which verbs this player has ever used, so the opening ladder can be read from what
            // actually happened rather than from a checklist column that could drift out of step.
            var actionsTaken = await db.ActionLogs.AsNoTracking()
                .Where(x => x.PlayerId == player.Id)
                .Select(x => x.Action)
                .Distinct()
                .ToListAsync(ct);
            var objectives = guidance.Objectives(player, actionsTaken);
            // One row rather than the cell itself: the front page says somebody is being held and what
            // it would take, and the crew page is where they are actually answered.
            var held = await db.Arrests.AsNoTracking()
                .Where(x => x.PlayerId == player.Id && x.SettledAtUtc == null)
                .GroupBy(x => x.PlayerId)
                .Select(g => new HeldCrew(
                    g.Sum(x => x.Hoes + x.Thugs + (x.PimpName == null ? 0 : 1)),
                    g.Sum(x => x.BailAmount),
                    g.Min(x => x.BailDeadlineUtc)))
                .SingleOrDefaultAsync(ct);
            var activeCraft = await db.WorkshopCrafts.AsNoTracking()
                .Where(x => x.PlayerId == player.Id && x.CompletedAtUtc == null)
                .OrderBy(x => x.CompletesAtUtc)
                .FirstOrDefaultAsync(ct);

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
                currentMarket,
                cityMarkets,
                travel,
                player.Cash,
                player.BankCash,
                opts.Bank.TripTurnCost,
                // Only sent while it is still standing. A window that has already closed is not a fact
                // about the player any more, and the panel would have to do the comparison anyway.
                player.LastBankedAtUtc?.AddMinutes(Math.Max(0, opts.Bank.TripGraceMinutes)) is { } freeUntil
                    && freeUntil > now
                        ? freeUntil
                        : null,
                netWorth,
                rank,
                cityRank,
                cityPlayers,
                player.Turns,
                opts.MaxTurnsFor(player),
                opts.MaxActionTurns,
                // The rate this player actually earns at, not the base one. Reporting the base while
                // paying the boosted rate would make the strip quietly wrong for every new player.
                opts.TurnsPerTickFor(player),
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
                ToWeaponRack(player, opts),
                player.Medicine,
                player.Poison,
                player.Rides,
                player.Weed,
                player.Coke,
                player.Moonshine,
                player.Cut,
                economy.ProductSellPrice(player.City, "weed"),
                economy.ProductSellPrice(player.City, "coke"),
                (int)Math.Round(player.CokePurity * 100),
                Math.Max(1, (int)Math.Round(economy.ProductSellPrice(player.City, "coke") * opts.PurityMultiplier(player.CokePurity))),
                economy.GetCrewReport(player),
                new GuidanceResponse(
                    guidance.NextMoves(player, hideouts.HeatFor(player), held),
                    objectives,
                    objectives.Count(x => x.Done),
                    objectives.Count),
                ToHideoutResponse(player, hideouts, now, opts, activeCraft),
                pimps.Active(player).Select(x => ToPimpResponse(x, commandingPimpIds)).ToList(),
                pimps.Fallen(player).Take(12).Select(x => ToPimpResponse(x, commandingPimpIds)).ToList(),
                ToCombatCrewResponse(combatCrew),
                ToCombatStatus(player, now, player, opts, recentAttacksMade, recentDefenses, laneReadyAt),
                unreadAlerts,
                economy.GetStore(player, await shelves.RemainingAsync(player.City, economy.GetStore(player), now, ct)),
                ToStoreRep(player, opts, now),
                strikes.MethodsFor(player),
                ToDistricts(opts),
                player.Alliance is { } crew
                    ? new AllianceBriefResponse(crew.Id, crew.Name, crew.OffensiveThugs, crew.DefensiveThugs, allianceRules.BorrowLimit(player.Thugs), player.AllianceDefenders)
                    : null,
                await UpdateEndpoints.UpdatesForAsync(db, player.Account.LastSeenAnnouncementAtUtc, now, 3, ct),
                activity));
        }).RequireAuthorization();


        app.MapPost("/api/game/travel", async (
            TravelRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            PlayerClock clock,
            EconomyService economy,
            CombatResolutionService combatResolver,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var now = DateTime.UtcNow;
            await combatResolver.ResolveDueAsync(now, ct);
            var blockedReason = await TravelBlockedReasonAsync(db, player.Id, ct);
            if (blockedReason is not null)
                return Results.BadRequest(new { error = blockedReason });

            await clock.AdvanceAsync(player, now, db, ct);
            var before = Snapshot(player);
            try
            {
                var result = economy.Travel(player, request.City);
                AddLog(db, player, before, "TRAVEL", TurnsSpentIn(result), result.Summary, now);
                await db.SaveChangesAsync(ct);
                return Results.Ok(result);
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();


        app.MapPost("/api/game/street", async (
            ScoutRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            PlayerClock clock,
            EconomyService economy,
            ArrestService arrests,
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
                var result = economy.Scout(player, request.Turns, request.AutoBuySupplies, await territories.EffectsForAsync(player.Id, player.City, ct), await territories.GarrisonedPimpIdsAsync(player.Id, ct), request.District);
                // Rolled here rather than inside the shift because it writes a row, and the economy
                // service has no database by design. Logged as one sentence with the shift that caused
                // it: being swept up is part of what happened out there, not a separate event.
                var swept = arrests.RollForShift(player, request.Turns, request.District, now);
                AddLog(db, player, before, "STREET", request.Turns, result.Summary + Describe(swept));
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
            ArrestService arrests,
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
                var result = economy.Scout(player, request.Turns, request.AutoBuySupplies, await territories.EffectsForAsync(player.Id, player.City, ct), await territories.GarrisonedPimpIdsAsync(player.Id, ct), request.District);
                // Rolled here rather than inside the shift because it writes a row, and the economy
                // service has no database by design. Logged as one sentence with the shift that caused
                // it: being swept up is part of what happened out there, not a separate event.
                var swept = arrests.RollForShift(player, request.Turns, request.District, now);
                AddLog(db, player, before, "STREET", request.Turns, result.Summary + Describe(swept));
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

            var now = DateTime.UtcNow;
            var tick = await clock.AdvanceAsync(player, now, db, ct);
            var before = Snapshot(player);
            try
            {
                var active = await db.WorkshopCrafts
                    .AnyAsync(x => x.PlayerId == player.Id && x.CompletedAtUtc == null, ct);
                if (active)
                {
                    if (tick.Changed)
                        await db.SaveChangesAsync(ct);
                    return Results.BadRequest(new { error = "The craft queue is already running something." });
                }

                var craft = economy.StartProductionCraft(player, request.Product, request.Turns, await territories.EffectsForAsync(player.Id, player.City, ct), now);
                db.WorkshopCrafts.Add(craft);

                var minutes = Math.Max(1, (int)Math.Ceiling((craft.CompletesAtUtc - now).TotalMinutes));
                var summary = $"Queued {craft.Quantity:N0} {craft.Label.ToLowerInvariant()} with {craft.WorkUnits:N0} turn{(craft.WorkUnits == 1 ? string.Empty : "s")} and {craft.TotalCost:C0}. Ready in {minutes:N0} minute{(minutes == 1 ? string.Empty : "s")}.";
                AddLog(db, player, before, "PRODUCTION", craft.WorkUnits, summary, now);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new ActionResultResponse(summary, player.Turns, new Dictionary<string, object?>
                {
                    ["craftId"] = craft.Id,
                    ["product"] = craft.Good,
                    ["unitsQueued"] = craft.Quantity,
                    ["workUnits"] = craft.WorkUnits,
                    ["turnsSpent"] = craft.WorkUnits,
                    ["totalCost"] = craft.TotalCost,
                    ["completesAtUtc"] = craft.CompletesAtUtc
                }));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();


        // Stepping on coke you already hold, wherever it came from. Separate from production because
        // the coke worth stretching is usually coke that was never produced.
        app.MapPost("/api/game/cut", async (
            ProduceRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            PlayerClock clock,
            EconomyService economy,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            await clock.AdvanceAsync(player, DateTime.UtcNow, db, ct);
            var before = Snapshot(player);
            try
            {
                var result = economy.CutCoke(player, request.Turns);
                AddLog(db, player, before, "CUT", request.Turns, result.Summary);
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


        // The clock everybody is playing against, and the only part of the game that survives it.
        app.MapGet("/api/game/season", async (
            CurrentPlayerService current,
            SeasonService seasons,
            IOptionsSnapshot<GameOptions> gameOptions,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var now = DateTime.UtcNow;
            var season = await seasons.CurrentAsync(now, ct);
            var config = gameOptions.Value.Seasons;
            var honours = await seasons.HonoursForAsync(player.Id, ct);
            var previous = (await seasons.PastSeasonsAsync(1, ct)).FirstOrDefault();
            var table = previous is null
                ? new List<SeasonResult>()
                : (await seasons.TableForAsync(previous.Id, 10, ct)).ToList();

            return Results.Ok(new SeasonResponse(
                season.Number,
                season.Name,
                season.StartedAtUtc,
                season.EndsAtUtc,
                Math.Max(0, (int)Math.Ceiling((season.EndsAtUtc - now).TotalSeconds)),
                config.Enabled,
                config.LengthDays,
                config.ChampionHeadStart,
                config.TopThreeHeadStart,
                config.TopTenHeadStart,
                honours.Select(x => new SeasonHonourResponse(
                    x.Season.Number,
                    x.Season.Name,
                    x.Rank,
                    x.NetWorth,
                    x.Honour,
                    x.Season.EndedAtUtc)).ToList(),
                table.Select(Standing).ToList(),
                previous?.Name));
        }).RequireAuthorization();


        // The whole shelf, newest first: every season the world has had, who won it, and where this
        // player came in it.
        //
        // Its own route rather than more fields on /api/game/season. That one is the panel on the
        // dashboard and is fetched on every visit, and a list that grows by a row a month has no
        // business riding along with a countdown.
        app.MapGet("/api/game/seasons", async (
            CurrentPlayerService current,
            SeasonService seasons,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var now = DateTime.UtcNow;
            var shelf = new List<Season> { await seasons.CurrentAsync(now, ct) };
            shelf.AddRange(await seasons.PastSeasonsAsync(24, ct));

            // Two queries for the whole shelf rather than two per season: who won each, and where this
            // player finished each.
            var champions = await seasons.ChampionsForAsync(shelf.Select(x => x.Id), ct);
            var mine = (await seasons.HonoursForAsync(player.Id, ct)).ToDictionary(x => x.SeasonId);
            var playingNow = await seasons.PlayersNowAsync(ct);

            return Results.Ok(shelf.Select(season =>
            {
                var running = season.Status == SeasonStatuses.Running;
                var champion = champions.GetValueOrDefault(season.Id);
                var yours = mine.GetValueOrDefault(season.Id);
                return new SeasonArchiveEntryResponse(
                    season.Number,
                    season.Name,
                    season.StartedAtUtc,
                    season.EndsAtUtc,
                    season.EndedAtUtc,
                    running,
                    // A finished season counted itself at the roll. The one being played has nothing
                    // written down yet, so it is counted now.
                    running ? playingNow : season.Players,
                    champion?.PlayerName,
                    champion?.City,
                    champion?.CrewName,
                    champion?.NetWorth ?? 0,
                    yours?.Rank,
                    yours?.Honour,
                    yours?.NetWorth);
            }).ToList());
        }).RequireAuthorization();


        // How one season finished, read from the record rather than recomputed.
        app.MapGet("/api/game/seasons/{number:int}", async (
            int number,
            CurrentPlayerService current,
            SeasonService seasons,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var season = await seasons.ByNumberAsync(number, ct);
            if (season is null)
                return Results.NotFound(new { error = $"There has never been a season {number}." });

            // The season being played has no final table and will not have one until it is over. The
            // live board is the answer to that question and already has a route of its own.
            var running = season.Status == SeasonStatuses.Running;
            var table = running
                ? new List<SeasonResult>()
                : (await seasons.TableForAsync(season.Id, 100, ct)).ToList();

            // Carried beside the table rather than left to be found in it. A hundred rows is the page's
            // cap and not the record's, and the line most people want is the one it cuts off.
            var yours = running ? null : await seasons.FinishForAsync(season.Id, player.Id, ct);

            return Results.Ok(new SeasonTableResponse(
                season.Number,
                season.Name,
                season.StartedAtUtc,
                season.EndsAtUtc,
                season.EndedAtUtc,
                running,
                running ? await seasons.PlayersNowAsync(ct) : season.Players,
                table.Select(Standing).ToList(),
                yours is null ? null : Standing(yours)));
        }).RequireAuthorization();


        // Priced for whoever is asking: the shelf is the same for everybody and the prices are not.
        app.MapGet("/api/game/store", async (
            CurrentPlayerService current,
            EconomyService economy,
            TraderShelfService shelves,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();
            // The shelf is read once and priced twice: the first call is the list of lines this shop
            // could have, which is what the stock lookup needs to know what to count.
            var now = DateTime.UtcNow;
            var stock = await shelves.RemainingAsync(player.City, economy.GetStore(player), now, ct);
            return Results.Ok(economy.GetStore(player, stock));
        }).RequireAuthorization();


        // Money for standing and nothing else. No turns: this is a payment, not an errand, and the
        // thing holding it back is the counter's own clock rather than the player's.
        app.MapPost("/api/game/store/invest", async (
            StoreInvestRequest request,
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
                var result = economy.Invest(player, request.Key, DateTime.UtcNow);
                AddLog(db, player, before, "STORE", 0, result.Summary);
                await db.SaveChangesAsync(ct);
                return Results.Ok(result);
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();


        app.MapPost("/api/game/store/buy", async (
            StoreBuyRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            EconomyService economy,
            TraderShelfService shelves,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var now = DateTime.UtcNow;
            var stock = await shelves.RemainingAsync(player.City, economy.GetStore(player), now, ct);
            var before = Snapshot(player);
            try
            {
                var result = economy.BuyStoreItem(player, request.ItemKey, request.Quantity, stock);
                // Off the counter after the purchase is allowed, never before: a refused buy must not
                // quietly cost the town its stock.
                if (result.Breakdown?.TryGetValue("good", out var bought) == true && bought is string good)
                    await shelves.TakeAsync(player.City, good, request.Quantity, now, ct);
                AddLog(db, player, before, "STORE", 0, result.Summary);
                await db.SaveChangesAsync(ct);
                return Results.Ok(result);
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();


        // Only rides come back here. Supplies are consumed and weapons have a player market that pays
        // better than any shop, so the chop shop is the one counter in the game that buys as well as sells.
        app.MapPost("/api/game/store/sell", async (
            StoreSellRequest request,
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
                var key = request.ItemKey?.Trim().ToLowerInvariant();
                if (key is not ("rides" or "ride"))
                    return Results.BadRequest(new { error = "The only thing anyone buys back is a ride." });

                var result = economy.SellRides(player, request.Quantity);
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
            PlayerClock clock,
            EconomyService economy,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            // A trip to the bank costs turns, so the clock has to be brought forward first. Without
            // this the player is refused for turns they have already earned and cannot see.
            var now = DateTime.UtcNow;
            await clock.AdvanceAsync(player, now, db, ct);
            var before = Snapshot(player);
            try
            {
                var result = economy.Deposit(player, request.Amount, now);
                AddLog(db, player, before, "BANK", TurnsSpentIn(result), result.Summary, now);
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
            PlayerClock clock,
            EconomyService economy,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var now = DateTime.UtcNow;
            await clock.AdvanceAsync(player, now, db, ct);
            var before = Snapshot(player);
            try
            {
                var result = economy.Withdraw(player, request.Amount, now);
                AddLog(db, player, before, "BANK", TurnsSpentIn(result), result.Summary, now);
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

        // Neither blocker is per-city, so travel is either open or shut for the whole panel. Shared by
        // the dashboard, which reports the reason, and the travel post, which enforces it.
        static async Task<string?> TravelBlockedReasonAsync(GameDbContext db, Guid playerId, CancellationToken cancellationToken)
        {
            var pendingAttack = await ActiveOutgoingMissionAsync(db, playerId, cancellationToken);
            if (pendingAttack is not null) return PendingAttackMessage(pendingAttack);

            return await db.Territories.AsNoTracking().AnyAsync(x => x.HolderId == playerId, cancellationToken)
                ? "Pull your garrisons off your ground before leaving town."
                : null;
        }

        // One finish, as the shelf and the table both show it. Shared so the dashboard panel and the
        // archive can never disagree about what a row of a season's table is.
        static SeasonStandingResponse Standing(SeasonResult result)
            => new(result.Rank, result.PlayerName, result.City, result.CrewName, result.NetWorth, result.Honour);

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
