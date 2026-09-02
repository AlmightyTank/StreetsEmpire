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
            TitleService titles,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var normalizedQuery = query?.Trim() ?? string.Empty;
            var now = DateTime.UtcNow;
            await combatResolver.ResolveDueAsync(now, ct);
            var laneReadyAt = await combatMissions.LaneReadyAtUtcAsync(player.Id, now, ct);
            // What this viewer could carry off, which is what the target list gates on.
            var viewerPlunder = economy.CalculatePlunder(player);
            // Read once for the whole page. A title lookup per row would be twenty queries to answer
            // one question about the same twenty-four hours.
            var board = await titles.BoardAsync(now, ct);

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
            // Once for the page rather than once per row: every row is judged against the same sender.
            var pactAllies = await DirectMessages.PactAlliesAsync(db, player.AllianceId, ct);
            var targets = ranked
                .Select(x => ToTargetResponse(x, now, player, gameOptions.Value, pactAllies, viewerLaneReadyAtUtc: laneReadyAt, viewerPlunder: viewerPlunder, titles: board))
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
            StreetStrikeService strikes,
            TitleService titles,
            IntelService intel,
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

            var subjectStanding = await db.Players.AsNoTracking()
                .Where(x => x.Id == playerId)
                .Select(economy.StandingRowExpression())
                .SingleAsync(ct);
            var subjectRank = await db.Players.AsNoTracking()
                .CountAsync(economy.RanksAbove(subjectStanding.NetWorth, subjectStanding.CreatedAtUtc), ct) + 1;
            var target = new RankedPlayer(subject, subjectStanding.NetWorth, economy.CalculatePlunder(subject), subjectRank);

            // Not fetched at all when it is not going to be shown. Filtering after the query would
            // read the same rows out of the database and then decide not to use them, which is the
            // shape of privacy that leaks the first time somebody adds a field to the response.
            var showActivity = subject.Account.ShowActivityOnProfile;
            var activity = !showActivity ? [] : await db.ActionLogs.AsNoTracking()
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

            // Asked of the same function the launch will ask, so the sentence under a dead button is
            // the sentence the server would have thrown had it been pressed.
            var blockers = new Dictionary<string, string>();
            if (viewer is not null)
            {
                foreach (var method in AttackMethods.All.Where(AttackMethods.IsStrike))
                {
                    // Poach is asked about at its cheapest, since the amount is chosen after this: the
                    // question here is whether the strike is possible at all, not whether one slider is.
                    var coke = method == AttackMethods.Poach ? gameOptions.Value.Strikes.Poach.CokePerHoe : 0;
                    if (strikes.WhyNot(method, viewer, target.Player, coke) is { } why)
                        blockers[method] = why;
                }
            }

            return Results.Ok(ToProfileResponse(
                target, activity, now, viewer, gameOptions.Value,
                await DirectMessages.PactAlliesAsync(db, viewer?.AllianceId, ct),
                await DescribeIntelAsync(intel, gameOptions.Value, viewer, playerId, now, ct),
                recentAttacksMade, recentDefenses, laneReadyAt,
                // Plunder, not net worth: this is the anti-farm gate, and it weighs what can be taken.
                // It said CalculateNetWorth here, so a profile judged the viewer on a sum that included
                // their buildings while the target list beside it judged them on one that did not - the
                // same rule giving two answers depending on which screen you were looking at.
                // Nobody signed in has nothing to weigh, and the gate reads zero as "no opinion".
                viewer is null ? 0 : economy.CalculatePlunder(viewer),
                await titles.BoardAsync(now, ct),
                blockers));
        }).RequireAuthorization();


        // Sending somebody to look at a house. Costs turns, and what comes back is decided by the
        // intelligence centre at the moment of looking - see IntelService.
        app.MapPost("/api/game/players/{playerId:guid}/scout", async (
            Guid playerId,
            CurrentPlayerService current,
            GameDbContext db,
            PlayerClock clock,
            IntelService intel,
            CancellationToken ct) =>
        {
            var viewer = await current.GetAsync(ct);
            if (viewer is null) return Results.Unauthorized();

            var now = DateTime.UtcNow;
            // Turns accrue on a clock, so they are brought up to date before any are spent.
            await clock.AdvanceAsync(viewer, now, db, ct);

            var subject = await db.Players.AsNoTracking().SingleOrDefaultAsync(x => x.Id == playerId, ct);
            if (subject is null) return Results.NotFound(new { error = "Player not found." });

            if (await intel.ScoutAsync(viewer, subject, now, ct) is { } refusal)
                return Results.BadRequest(new { error = refusal });

            await db.SaveChangesAsync(ct);
            return Results.Ok(new ActionResultResponse(
                $"Your people had a look at {subject.Name}'s place.", viewer.Turns, null));
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
            if ((await clock.AdvanceAsync(player, now, db, ct)).Changed)
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
                // House raids only. A fight over ground is reported by its own lines, and counting it
                // here as well told the player about one raid twice.
                .Where(x => x.DefenderId == player.Id && x.TerritoryId == null && x.Outcome != "Pending" && x.CreatedAtUtc > since)
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

            // Read off finished territory raids rather than a new store: the mission already records
            // who fought over what and when it landed.
            var groundFights = await db.CombatMissions.AsNoTracking()
                .Where(x => x.TerritoryId != null
                            && x.Status == "Complete"
                            && x.CompletedAtUtc != null
                            && x.CompletedAtUtc > since
                            && (x.AttackerId == player.Id || x.DefenderId == player.Id))
                .Select(x => new { x.AttackerId, x.DefenderId, x.Outcome, x.DefenderThugsLost, Name = x.Territory!.Name })
                .ToListAsync(ct);
            var groundLost = groundFights
                .Where(x => x.DefenderId == player.Id && x.Outcome == "Victory")
                .Select(x => x.Name).ToList();
            var groundTaken = groundFights
                .Where(x => x.AttackerId == player.Id && x.Outcome == "Victory")
                .Select(x => x.Name).ToList();
            // Held is worth reporting too: the garrison paid for it, and a garrison that shrank with
            // no explanation reads as a bug.
            var defended = groundFights.Where(x => x.DefenderId == player.Id && x.Outcome != "Victory").ToList();

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
                gameOptions.Value.MaxTurnsFor(player),
                player.CombatProtectionUntilUtc,
                rankBefore,
                rankNow,
                overtookYou,
                youOvertook,
                groundLost,
                groundTaken,
                defended.Select(x => x.Name).Distinct().ToList(),
                defended.Sum(x => x.DefenderThugsLost),
                (player.Hideout?.WreckedRooms() ?? []).Select(HideoutRooms.Name).ToList()));

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

            // Non-combat notices come from the action log, which is where they are already recorded.
            // Matching the notification kinds in the database keeps the page small rather than pulling
            // a player's whole history back to filter it here.
            var notices = await db.ActionLogs.AsNoTracking()
                .Where(x => x.PlayerId == player.Id)
                .Where(DefenceAlerts.IsNotificationRow)
                .OrderByDescending(x => x.CreatedAtUtc)
                .ThenByDescending(x => x.Id)
                .Take(25)
                .Select(x => new { x.Id, x.Action, x.Summary, x.CreatedAtUtc })
                .ToListAsync(ct);

            // Filtered here rather than in the two queries above, because the switch is per category and
            // a category is a property of the alert the classifier produces rather than of the row it
            // came from. Turning one off hides it from the count as well as the list: an unread badge
            // over something you asked not to be told about is the notification you switched off.
            var wanted = (AlertCategory category) => category switch
            {
                AlertCategory.Combat => player.Account.NoticeCombat,
                AlertCategory.Crew => player.Account.NoticeCrew,
                AlertCategory.Market => player.Account.NoticeMarket,
                _ => true,
            };

            var alerts = logs
                .Select(x => DefenceAlerts.ToAlert(DefenceAlerts.Describe(x, player.CombatAlertsSeenAtUtc)))
                .Concat(notices
                    .Select(x => DefenceAlerts.ToAlert(x.Id, x.Action, x.Summary, x.CreatedAtUtc, player.CombatAlertsSeenAtUtc))
                    .Where(x => x is not null)
                    .Select(x => x!))
                .Where(x => wanted(DefenceAlerts.CategoryOf(x.Kind)))
                .OrderByDescending(x => x.CreatedAtUtc)
                .Take(25)
                .ToList();

            return Results.Ok(new AlertsResponse(
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
            return Results.Ok(new AlertsResponse(0, player.CombatAlertsSeenAtUtc, []));
        }).RequireAuthorization();

        // One endpoint for the whole attack menu. A raid launches a travelling mission; the four strikes
        // settle here and now. Keeping them behind one route means the client sends the same shape
        // whichever it picked, and a caller that names no method still gets the raid it always got.
        app.MapPost("/api/game/combat/attack", async (
            CombatAttackRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            PlayerClock clock,
            CombatMissionService combatMissions,
            CombatResolutionService combatResolver,
            AllianceService alliances,
            TerritoryService territories,
            StreetStrikeService strikes,
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

            await clock.AdvanceAsync(attacker, now, db, ct);
            var before = Snapshot(attacker);
            try
            {
                if (AttackMethods.IsStrike(request.Method))
                {
                    if (await alliances.AreAlliedAsync(attacker, defender, ct))
                        throw new GameRuleException($"{defender.Name} is allied with your crew.");

                    // Whoever the defender still has at home, and what they still have to hold. Crew
                    // already out attacking someone else cannot also be guarding the garage - and
                    // neither can the guns that went with them, which is what makes striking a player
                    // who is mid-raid the opening it should be. Their best guns are precisely the ones
                    // a raiding party takes.
                    var committed = await combatMissions.ActiveAttackMissions(defender.Id)
                        .Select(x => new { x.RemainingAttackers, x.CarriedPistols, x.CarriedShotguns, x.CarriedSmgs, x.CarriedRifles })
                        .ToListAsync(ct);
                    var away = committed.Aggregate(
                        Armoury.Empty,
                        (rack, x) => rack + new Armoury(x.CarriedPistols, x.CarriedShotguns, x.CarriedSmgs, x.CarriedRifles));
                    var cityControlDefenders = defender.AllianceId is { } defenderAllianceId
                        ? await territories.CityControlThugsForAllianceInCityAsync(defenderAllianceId, defender.City, ct)
                        : 0;
                    var defence = new StrikeDefence(
                        Math.Max(0, defender.Thugs - committed.Sum(x => x.RemainingAttackers)) + cityControlDefenders,
                        defender.Armoury - away);

                    var strike = strikes.Resolve(attacker, defender, request, defence, now);
                    db.CombatLogs.Add(strike.Log);
                    AddLog(db, attacker, before, "ATTACK", strike.Log.TurnsSpent, strike.Log.Summary);
                    await db.SaveChangesAsync(ct);
                    return Results.Ok(strike.Result);
                }

                var mission = await combatMissions.LaunchAsync(attacker, defender, request, now, ct);
                var calls = await alliances.CreateAssistCallsForAsync(mission, now, ct);
                AddLog(db, attacker, before, "ATTACK", mission.TurnsSpent, mission.Summary);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new ActionResultResponse(mission.Summary, attacker.Turns, new Dictionary<string, object?>
                {
                    ["method"] = AttackMethods.Raid,
                    ["missionId"] = mission.Id,
                    ["status"] = mission.Status,
                    ["assignedPimps"] = mission.AssignedPimps,
                    ["assignedThugs"] = mission.AssignedThugs,
                    ["assignedWeapons"] = mission.AssignedWeapons,
                    ["assistCalls"] = calls.Count,
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

    /// <summary>
    /// What this viewer knows, and what a fresh look would be worth. Both numbers, because the gap
    /// between them is the entire argument for spending the turns on another one.
    /// </summary>
    private static async Task<IntelResponse> DescribeIntelAsync(
        IntelService intel, GameOptions options, Player? viewer, Guid subjectId, DateTime nowUtc, CancellationToken ct)
    {
        var cost = options.Hideout.Intel.ScoutTurnCost;
        var hours = options.Hideout.Intel.FreshHours;
        if (viewer is null) return new IntelResponse(0, 0, null, false, cost, hours);

        var known = await intel.KnownLevelAsync(viewer, subjectId, nowUtc, ct);
        return new IntelResponse(
            known,
            options.Hideout.LevelOfIntelligence(viewer.Hideout),
            await intel.LastLookedAtUtcAsync(viewer.Id, subjectId, ct),
            known > 0,
            cost,
            hours);
    }
}

