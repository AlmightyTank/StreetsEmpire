using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;
using StreetEmpire.Api.Services;
using static StreetEmpire.Api.Support.ActionLogging;

namespace StreetEmpire.Api.Endpoints;

/// <summary>Founding, joining and running a crew, and the board of everybody else's.</summary>
internal static class AllianceEndpoints
{
    internal static void MapAllianceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/game/alliances", async (
            CurrentPlayerService current,
            GameDbContext db,
            EconomyService economy,
            AllianceService alliances,
            TerritoryService territories,
            IOptionsSnapshot<GameOptions> gameOptions,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            // Crews seed themselves into an existing world the first time anybody looks, the same way
            // ground does. A board showing only the crew the player just made is not a board.
            await alliances.SeedRivalCrewsAsync(DateTime.UtcNow, ct);
            await territories.SeedAsync(ct);

            var board = await alliances.BoardAsync(player, ct);
            var config = gameOptions.Value.Alliances;
            var yours = board.FirstOrDefault(x => x.Yours);

            var members = new List<AllianceMemberResponse>();
            var pacts = new List<AlliancePactResponse>();
            var assistCalls = new List<AllianceAssistCallResponse>();
            var transfers = new List<AllianceTransferResponse>();
            long treasury = 0;
            if (player.AllianceId is { } id)
            {
                var crew = await db.Alliances.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Id == id, ct);
                treasury = crew?.Treasury ?? 0;

                var roster = await db.Players.AsNoTracking()
                    .Where(x => x.AllianceId == id)
                    .ToListAsync(ct);
                members = roster
                    .Select(x => new AllianceMemberResponse(
                        x.Id,
                        x.Name,
                        x.City,
                        economy.CalculateNetWorth(x),
                        x.Pimps,
                        x.Hoes,
                        x.Thugs,
                        crew is not null && crew.FounderId == x.Id,
                        x.Id == player.Id,
                        x.AllianceRank.ToString(),
                        AllianceRanks.Label(x.AllianceRank),
                        AllianceRanks.Outranks(player.AllianceRank, x.AllianceRank),
                        x.AllianceDefenders,
                        x.AllianceJoinedAtUtc))
                    .OrderByDescending(x => x.NetWorth)
                    .ToList();

                var canAnswerPacts = crew is not null && AllianceService.Can(player, crew, AlliancePower.Invite);
                var pactRows = await db.AlliancePacts.AsNoTracking()
                    .Include(x => x.RequestingAlliance)
                    .Include(x => x.TargetAlliance)
                    .Where(x => (x.RequestingAllianceId == id || x.TargetAllianceId == id)
                                && (x.Status == AlliancePactStatuses.Pending || x.Status == AlliancePactStatuses.Active))
                    .OrderByDescending(x => x.CreatedAtUtc)
                    .Take(20)
                    .ToListAsync(ct);
                pacts = pactRows.Select(x => ToPactResponse(x, id, canAnswerPacts)).ToList();

                var callRows = await db.AllianceAssistCalls.AsNoTracking()
                    .Include(x => x.AllyAlliance)
                    .Include(x => x.DefenderAlliance)
                    .Include(x => x.CombatMission).ThenInclude(x => x.Attacker)
                    .Include(x => x.CombatMission).ThenInclude(x => x.Defender)
                    .Where(x => x.AllyAllianceId == id || x.DefenderAllianceId == id)
                    .OrderByDescending(x => x.Status == AllianceAssistStatuses.Open)
                    .ThenByDescending(x => x.CreatedAtUtc)
                    .Take(20)
                    .ToListAsync(ct);
                assistCalls = callRows.Select(ToAssistCallResponse).ToList();

                var transferRows = await db.AllianceTransfers.AsNoTracking()
                    .Include(x => x.FromPlayer)
                    .Include(x => x.ToPlayer)
                    .Where(x => x.AllianceId == id)
                    .OrderByDescending(x => x.CreatedAtUtc)
                    .Take(20)
                    .ToListAsync(ct);
                transfers = transferRows.Select(ToTransferResponse).ToList();
            }

            // Everything this player has a part in: invitations waiting on them, applications waiting on
            // their crew, and invitations their crew has sent and is still waiting to hear about.
            //
            // That last kind is easy to leave out - nobody is waiting on you for it - and leaving it out
            // means a boss can neither see who has been asked nor take an ask back, which is how a crew
            // ends up with invitations outstanding to people who left the game months ago.
            var pending = await db.AllianceRequests.AsNoTracking()
                .Include(x => x.Alliance)
                .Include(x => x.Player)
                .Where(x => (x.Kind == AllianceRequestKind.Invitation && x.PlayerId == player.Id)
                            || (x.AllianceId == player.AllianceId))
                .OrderByDescending(x => x.CreatedAtUtc)
                .Take(30)
                .ToListAsync(ct);

            var crewForPowers = player.Alliance;
            var powers = crewForPowers is null
                ? []
                : Enum.GetValues<AlliancePower>()
                    .Select(x => new AlliancePowerResponse(
                        x.ToString(),
                        PowerLabel(x),
                        AllianceRanks.Label(crewForPowers.MinRankFor(x)),
                        AllianceService.Can(player, crewForPowers, x)))
                    .ToList();

            return Results.Ok(new AllianceBoardResponse(
                yours,
                members,
                treasury,
                config.FoundingCost,
                config.MaxDuesPercent,
                config.OffensiveThugCost,
                config.DefensiveThugCost,
                alliances.BorrowLimit(player.Thugs),
                player.AllianceDefenders,
                AllianceRanks.Label(player.AllianceRank),
                powers,
                AllianceRanks.All.Select(AllianceRanks.Label).ToList(),
                AllianceDoors.All
                    .Select(x => new AllianceDoorResponse(x.ToString(), AllianceDoors.Label(x), AllianceDoors.Describe(x)))
                    .ToList(),
                pending.Select(x => ToRequestResponse(x, player)).ToList(),
                pacts,
                assistCalls,
                transfers,
                board));
        }).RequireAuthorization();


        app.MapPost("/api/game/alliances", async (
            FoundAllianceRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            AllianceService alliances,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var before = Snapshot(player);
            try
            {
                var now = DateTime.UtcNow;
                var alliance = await alliances.FoundAsync(player, request.Name, request.Motto, now, ct);
                var summary = $"{player.Name} founded {alliance.Name}.";
                AddLog(db, player, before, "ALLIANCE", 0, summary, now);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new ActionResultResponse(summary, player.Turns, new Dictionary<string, object?>
                {
                    ["allianceId"] = alliance.Id,
                    ["name"] = alliance.Name,
                    ["duesPercent"] = alliance.DuesPercent
                }));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();


        app.MapPost("/api/game/alliances/join", async (
            JoinAllianceRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            AllianceService alliances,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var before = Snapshot(player);
            try
            {
                var now = DateTime.UtcNow;
                var alliance = await alliances.JoinAsync(player, request.AllianceId, now, ct);
                var summary = $"{player.Name} started running with {alliance.Name}.";
                AddLog(db, player, before, "ALLIANCE", 0, summary, now);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new ActionResultResponse(summary, player.Turns, new Dictionary<string, object?>
                {
                    ["allianceId"] = alliance.Id,
                    ["name"] = alliance.Name,
                    ["duesPercent"] = alliance.DuesPercent
                }));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();


        app.MapPost("/api/game/alliances/leave", async (
            CurrentPlayerService current,
            GameDbContext db,
            AllianceService alliances,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var before = Snapshot(player);
            try
            {
                var now = DateTime.UtcNow;
                var alliance = await alliances.LeaveAsync(player, now, ct);
                var summary = $"{player.Name} walked out on {alliance.Name}.";
                AddLog(db, player, before, "ALLIANCE", 0, summary, now);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new ActionResultResponse(summary, player.Turns));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();


        app.MapPost("/api/game/alliances/expel", async (
            ExpelMemberRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            AllianceService alliances,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var before = Snapshot(player);
            try
            {
                var now = DateTime.UtcNow;
                var alliance = await alliances.ExpelAsync(player, request.MemberId, ct);
                var summary = $"Somebody was thrown out of {alliance.Name}.";
                AddLog(db, player, before, "ALLIANCE", 0, summary, now);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new ActionResultResponse(summary, player.Turns));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();


        app.MapPost("/api/game/alliances/thugs", async (
            BuyAllianceThugsRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            AllianceService alliances,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var before = Snapshot(player);
            try
            {
                var now = DateTime.UtcNow;
                var (alliance, bought, cost) = await alliances.BuyThugsAsync(player, request.Kind, request.Quantity, ct);
                var kind = AllianceService.IsOffensive(request.Kind) ? "offensive" : "defensive";
                var summary = $"{alliance.Name} took on {bought:N0} {kind} thug(s) for {cost:C0}.";
                AddLog(db, player, before, "ALLIANCE", 0, summary, now);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new ActionResultResponse(summary, player.Turns, new Dictionary<string, object?>
                {
                    ["kind"] = kind,
                    ["bought"] = bought,
                    ["cost"] = cost,
                    ["treasury"] = alliance.Treasury,
                    ["offensiveThugs"] = alliance.OffensiveThugs,
                    ["defensiveThugs"] = alliance.DefensiveThugs
                }));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();


        // Positive posts them to this member's house; negative sends them back to the pool.
        app.MapPost("/api/game/alliances/defenders", async (
            PostDefendersRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            AllianceService alliances,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var before = Snapshot(player);
            try
            {
                var now = DateTime.UtcNow;
                var (alliance, posted) = await alliances.PostDefendersAsync(player, request.Quantity, ct);
                var summary = posted > 0
                    ? $"{posted:N0} of {alliance.Name}'s thugs are standing at your place."
                    : $"{-posted:N0} of {alliance.Name}'s thugs went back to the crew.";
                AddLog(db, player, before, "ALLIANCE", 0, summary, now);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new ActionResultResponse(summary, player.Turns, new Dictionary<string, object?>
                {
                    ["posted"] = posted,
                    ["yourDefenders"] = player.AllianceDefenders,
                    ["defensiveThugs"] = alliance.DefensiveThugs
                }));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();


        app.MapPost("/api/game/alliances/rank", async (
            SetAllianceRankRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            AllianceService alliances,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var before = Snapshot(player);
            try
            {
                var now = DateTime.UtcNow;
                var (alliance, member, rank) = await alliances.SetRankAsync(player, request.MemberId, request.Rank, ct);
                var summary = $"{member.Name} is now {AllianceRanks.Label(rank)} in {alliance.Name}.";
                AddLog(db, player, before, "ALLIANCE", 0, summary, now);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new ActionResultResponse(summary, player.Turns));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();


        app.MapPost("/api/game/alliances/hand-over", async (
            HandOverAllianceRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            AllianceService alliances,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var before = Snapshot(player);
            try
            {
                var now = DateTime.UtcNow;
                var (alliance, successor) = await alliances.HandOverAsync(player, request.MemberId, ct);
                var summary = $"{successor.Name} runs {alliance.Name} now.";
                AddLog(db, player, before, "ALLIANCE", 0, summary, now);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new ActionResultResponse(summary, player.Turns));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();


        app.MapPost("/api/game/alliances/invite", async (
            InvitePlayerRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            AllianceService alliances,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            try
            {
                var now = DateTime.UtcNow;
                var made = await alliances.RequestAsync(player, request.PlayerId, null, AllianceRequestKind.Invitation, request.Note, now, ct);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new ActionResultResponse($"{made.Player.Name} has been asked to run with {made.Alliance.Name}.", player.Turns));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();


        app.MapPost("/api/game/alliances/apply", async (
            ApplyToAllianceRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            AllianceService alliances,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            try
            {
                var now = DateTime.UtcNow;
                var made = await alliances.RequestAsync(player, null, request.AllianceId, AllianceRequestKind.Application, request.Note, now, ct);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new ActionResultResponse($"You have asked {made.Alliance.Name} for a place.", player.Turns));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();


        app.MapPost("/api/game/alliances/answer", async (
            AnswerAllianceRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            AllianceService alliances,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var before = Snapshot(player);
            try
            {
                var now = DateTime.UtcNow;
                var (answered, joined) = await alliances.AnswerAsync(player, request.RequestId, request.Accept, now, ct);
                var summary = joined
                    ? $"{answered.Player.Name} runs with {answered.Alliance.Name} now."
                    : "Turned down.";
                if (joined)
                    AddLog(db, player, before, "ALLIANCE", 0, summary, now);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new ActionResultResponse(summary, player.Turns));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();


        app.MapPost("/api/game/alliances/withdraw", async (
            AnswerAllianceRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            AllianceService alliances,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            try
            {
                await alliances.WithdrawAsync(player, request.RequestId, ct);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new ActionResultResponse("Taken back.", player.Turns));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();


        app.MapPost("/api/game/alliances/transfer", async (
            AllianceTransferRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            AllianceService alliances,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var before = Snapshot(player);
            try
            {
                var now = DateTime.UtcNow;
                var transfer = await alliances.SendResourceAsync(player, request.MemberId, request.Item, request.Quantity, now, ct);
                var summary = $"{player.Name} sent {transfer.Quantity:N0} {ResourceLabel(transfer.Item).ToLowerInvariant()} to {transfer.ToPlayer.Name}.";
                AddLog(db, player, before, "ALLIANCE", 0, summary, now);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new ActionResultResponse(summary, player.Turns));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();


        app.MapPost("/api/game/alliances/pacts", async (
            AlliancePactRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            AllianceService alliances,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            try
            {
                var now = DateTime.UtcNow;
                var pact = await alliances.RequestPactAsync(player, request.AllianceId, now, ct);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new ActionResultResponse($"{pact.TargetAlliance.Name} got your alliance pact offer.", player.Turns));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();


        app.MapPost("/api/game/alliances/pacts/answer", async (
            AnswerAlliancePactRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            AllianceService alliances,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            try
            {
                var now = DateTime.UtcNow;
                var pact = await alliances.AnswerPactAsync(player, request.PactId, request.Accept, now, ct);
                var summary = request.Accept
                    ? $"{pact.TargetAlliance.Name} and {pact.RequestingAlliance.Name} are allies now."
                    : "Pact refused.";
                await db.SaveChangesAsync(ct);
                return Results.Ok(new ActionResultResponse(summary, player.Turns));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();


        app.MapPost("/api/game/alliances/pacts/cancel", async (
            AnswerAlliancePactRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            AllianceService alliances,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            try
            {
                var now = DateTime.UtcNow;
                var pact = await alliances.CancelPactAsync(player, request.PactId, now, ct);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new ActionResultResponse($"Pact with {OtherCrew(pact, player.AllianceId)} closed.", player.Turns));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();


        // Taking back what is left of it, once the fight is over. Its own endpoint rather than a flag
        // on the one above, because it is the opposite direction and answers to different rules.
        app.MapPost("/api/game/alliances/assist/recall", async (
            AllianceAssistRecallRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            AllianceService alliances,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var before = Snapshot(player);
            try
            {
                var now = DateTime.UtcNow;
                var call = await alliances.RecallAssistAsync(player, request.AssistCallId, now, ct);
                var guns = new Armoury(call.PistolsReturned, call.ShotgunsReturned, call.SmgsReturned, call.RiflesReturned);

                var came = new List<string>();
                if (call.ThugsReturned > 0) came.Add($"{call.ThugsReturned:N0} thug(s)");
                if (guns.Total > 0) came.Add(guns.Describe());
                // Nothing coming home is a real outcome rather than a failure, and saying so plainly is
                // the only way somebody learns what sending help actually costs.
                var summary = came.Count == 0
                    ? $"Nothing came back from {call.CombatMission.Defender.Name} - what you sent did not survive the fight."
                    : $"{player.Name} took {string.Join(" and ", came)} back from {call.CombatMission.Defender.Name}.";

                AddLog(db, player, before, "ALLIANCE", 0, summary, now);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new ActionResultResponse(summary, player.Turns));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();

        app.MapPost("/api/game/alliances/assist", async (
            AllianceAssistRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            AllianceService alliances,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var before = Snapshot(player);
            try
            {
                var now = DateTime.UtcNow;
                var weapons = new Armoury(request.Pistols, request.Shotguns, request.Smgs, request.Rifles);
                var call = await alliances.AnswerAssistCallAsync(player, request.AssistCallId, request.Thugs, weapons, now, ct);
                var sent = new List<string>();
                if (request.Thugs > 0) sent.Add($"{request.Thugs:N0} thug(s)");
                if (weapons.Total > 0) sent.Add(weapons.Describe());
                var summary = $"{player.Name} sent {string.Join(" and ", sent)} to help {call.CombatMission.Defender.Name}.";
                AddLog(db, player, before, "ALLIANCE", 0, summary, now);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new ActionResultResponse(summary, player.Turns));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();


        app.MapPut("/api/game/alliances", async (
            UpdateAllianceRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            AllianceService alliances,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            try
            {
                var alliance = await alliances.UpdateAsync(player, request.DuesPercent, request.Door, request.Motto, request.Powers, ct);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new ActionResultResponse(
                    $"{alliance.Name} takes {alliance.DuesPercent}% and is {AllianceDoors.Label(alliance.Door).ToLowerInvariant()}.",
                    player.Turns,
                    new Dictionary<string, object?>
                    {
                        ["duesPercent"] = alliance.DuesPercent,
                        ["door"] = alliance.Door.ToString()
                    }));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();
    }

    private static AllianceRequestResponse ToRequestResponse(AllianceRequest request, Player viewer)
        => new(
            request.Id,
            request.Kind.ToString(),
            request.AllianceId,
            request.Alliance.Name,
            request.PlayerId,
            request.Player.Name,
            request.Note,
            // An application is only yours to answer if you can actually open the door. Being in the crew
            // is not enough: a soldier shown Accept and Refuse would be shown two buttons that refuse
            // them, which teaches a rule by failing rather than by saying it.
            request.Kind == AllianceRequestKind.Invitation
                ? request.PlayerId == viewer.Id
                : viewer.AllianceId == request.AllianceId
                  && AllianceService.Can(viewer, request.Alliance, AlliancePower.Invite),
            request.CreatedAtUtc);

    private static AlliancePactResponse ToPactResponse(AlliancePact pact, long viewerAllianceId, bool canAnswer)
        => new(
            pact.Id,
            pact.RequestingAllianceId,
            pact.RequestingAlliance.Name,
            pact.TargetAllianceId,
            pact.TargetAlliance.Name,
            pact.Status,
            pact.Status == AlliancePactStatuses.Pending && pact.TargetAllianceId == viewerAllianceId && canAnswer,
            pact.CreatedAtUtc);

    private static AllianceAssistCallResponse ToAssistCallResponse(AllianceAssistCall call)
        => new(
            call.Id,
            call.CombatMissionId,
            call.DefenderAllianceId,
            call.AllyAllianceId,
            call.CombatMission.Attacker.Name,
            call.CombatMission.Defender.Name,
            call.DefenderAlliance.Name,
            call.AllyAlliance.Name,
            call.CombatMission.Status,
            call.Status,
            call.ThugsSent,
            call.PistolsSent,
            call.ShotgunsSent,
            call.SmgsSent,
            call.RiflesSent,
            call.ThugsReturned,
            call.PistolsReturned,
            call.ShotgunsReturned,
            call.SmgsReturned,
            call.RiflesReturned,
            call.RespondedById,
            call.CreatedAtUtc);

    private static AllianceTransferResponse ToTransferResponse(AllianceTransfer transfer)
        => new(
            transfer.Id,
            transfer.FromPlayer.Name,
            transfer.ToPlayer.Name,
            transfer.Item,
            ResourceLabel(transfer.Item),
            transfer.Quantity,
            transfer.CreatedAtUtc);

    private static string ResourceLabel(string key)
        => key switch
        {
            "cash" => "Cash",
            "thugs" => "Thugs",
            _ => TradeGoods.Label(key)
        };

    private static string OtherCrew(AlliancePact pact, long? viewerAllianceId)
        => pact.RequestingAllianceId == viewerAllianceId ? pact.TargetAlliance.Name : pact.RequestingAlliance.Name;

    /// <summary>What a power is called on the settings panel, in the words the game uses elsewhere.</summary>
    private static string PowerLabel(AlliancePower power) => power switch
    {
        AlliancePower.Invite => "Open the door",
        AlliancePower.Expel => "Throw people out",
        AlliancePower.SpendTreasury => "Spend the treasury",
        AlliancePower.Borrow => "Take thugs on a raid",
        _ => "Post defenders at home"
    };
}
