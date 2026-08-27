using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;
using StreetEmpire.Api.Services;
using static StreetEmpire.Api.Support.ActionLogging;

namespace StreetEmpire.Api.Endpoints;

/// <summary>The map: who holds what, claiming empty ground, garrisons, and raids on held ground.</summary>
internal static class TerritoryEndpoints
{
    internal static void MapTerritoryEndpoints(this IEndpointRouteBuilder app)
    {

        app.MapGet("/api/game/territories", async (
            CurrentPlayerService current,
            GameDbContext db,
            TerritoryService territories,
            PimpRoster pimps,
            CombatResolutionService combatResolver,
            IOptionsSnapshot<GameOptions> gameOptions,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var now = DateTime.UtcNow;
            await combatResolver.ResolveDueAsync(now, ct);
            await territories.SeedAsync(ct);

            // Your town and nowhere else. The other cities exist and rivals hold ground in them, but
            // they are not yours to fight over, so showing them would only be a list of buttons you
            // cannot press.
            var all = await db.Territories.AsNoTracking()
                .Include(x => x.Holder)
                .Include(x => x.GarrisonPimp)
                .Where(x => x.City == player.City)
                .OrderBy(x => x.Name)
                .ToListAsync(ct);

            var config = gameOptions.Value.Territory;
            var mine = all.Where(x => x.HolderId == player.Id).ToList();
            var cap = territories.HoldingCapFor(player.Hideout);
            var free = await territories.FreeThugsAsync(player, ct);
            var effects = territories.EffectsFor(mine);
            var cityControl = player.AllianceId is { } allianceId
                ? (await territories.ControlledCitiesForAllianceAsync(allianceId, ct))
                    .FirstOrDefault(x => string.Equals(x.City, player.City, StringComparison.OrdinalIgnoreCase))
                : null;

            return Results.Ok(new TerritoryBoardResponse(
                player.City,
                mine.Count,
                cap,
                config.MinimumGarrison,
                config.MaxGarrisonThugs,
                config.MaxRaidThugs,
                config.ClaimTurnCost,
                free,
                new TerritoryEffectsResponse(
                    effects.StreetIncomePercent,
                    effects.ProductionYieldPercent,
                    effects.MoraleRecoveryPercent,
                    effects.LootPercent),
                cityControl is null
                    ? null
                    : new AllianceCityControlResponse(cityControl.City, cityControl.Territories, cityControl.BonusThugs),
                all.Select(x => Describe(x, player, territories, pimps, now, mine.Count, cap, free, config)).ToList()));
        }).RequireAuthorization();


        app.MapPost("/api/game/territories/claim", async (
            TerritoryClaimRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            TerritoryService territories,
            PlayerClock clock,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var now = DateTime.UtcNow;
            await clock.AdvanceAsync(player, now, db, ct);
            var before = Snapshot(player);
            try
            {
                var ground = await territories.ClaimAsync(player, request.TerritoryId, request.Thugs, request.PimpId, now, ct);
                var summary = $"Took over {ground.Name} with {ground.GarrisonThugs:N0} thug(s) standing on it.";
                AddLog(db, player, before, "TERRITORY", 0, summary, now);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new ActionResultResponse(summary, player.Turns, new Dictionary<string, object?>
                {
                    ["territoryId"] = ground.Id,
                    ["garrison"] = ground.GarrisonThugs
                }));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();


        app.MapPost("/api/game/territories/garrison", async (
            TerritoryGarrisonRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            TerritoryService territories,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var before = Snapshot(player);
            try
            {
                var (ground, gaveUp) = await territories.SetGarrisonAsync(player, request.TerritoryId, request.Thugs, request.PimpId, ct);
                var summary = gaveUp
                    ? $"Pulled off {ground.Name} entirely. It is anyone's now."
                    : $"{ground.Name} is now held by {ground.GarrisonThugs:N0} thug(s).";
                AddLog(db, player, before, "TERRITORY", 0, summary, DateTime.UtcNow);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new ActionResultResponse(summary, player.Turns, new Dictionary<string, object?>
                {
                    ["territoryId"] = ground.Id,
                    ["garrison"] = ground.GarrisonThugs,
                    ["gaveUp"] = gaveUp
                }));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();


        // Held ground is taken with a mission, using one of the two attack lanes, so taking ground
        // competes with raiding a house rather than being a free extra.
        app.MapPost("/api/game/territories/raid", async (
            TerritoryRaidRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            CombatMissionService missions,
            CombatResolutionService combatResolver,
            PlayerClock clock,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var now = DateTime.UtcNow;
            await combatResolver.ResolveDueAsync(now, ct);

            var ground = await db.Territories.Include(x => x.Holder).SingleOrDefaultAsync(x => x.Id == request.TerritoryId, ct);
            if (ground is null) return Results.NotFound(new { error = "That ground does not exist." });
            if (ground.HolderId is null) return Results.BadRequest(new { error = $"Nobody holds {ground.Name}. Claim it instead." });

            var holder = await db.Players
                .Include(x => x.Account)
                .Include(x => x.Crew)
                .SingleAsync(x => x.Id == ground.HolderId, ct);

            await clock.AdvanceAsync(player, now, db, ct);
            var before = Snapshot(player);
            try
            {
                var mission = await missions.LaunchAsync(
                    player,
                    holder,
                    new CombatAttackRequest(holder.Id, request.Thugs, request.Weapons, request.CommanderPimpId),
                    ground,
                    now,
                    ct);
                var summary = $"Moved on {ground.Name}: {mission.Summary}";
                AddLog(db, player, before, "ATTACK", mission.TurnsSpent, summary, now);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new ActionResultResponse(summary, player.Turns, new Dictionary<string, object?>
                {
                    ["missionId"] = mission.Id,
                    ["territoryId"] = ground.Id
                }));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();
    }

    /// <summary>
    /// Says not only what the ground is but whether this player can do anything about it, and why not
    /// when they cannot. A greyed button with no reason is the thing players come back to ask about.
    /// </summary>
    private static TerritoryResponse Describe(
        Territory ground,
        Player player,
        TerritoryService territories,
        PimpRoster pimps,
        DateTime nowUtc,
        int held,
        int cap,
        int freeThugs,
        TerritoryOptions config)
    {
        var type = territories.TypeOf(ground.Type);
        var mine = ground.HolderId == player.Id;
        var settled = ground.ProtectedUntilUtc is { } until && until > nowUtc;

        string? blocked = null;
        if (mine) blocked = null;
        else if (settled) blocked = "Just changed hands. Settled for now.";
        else if (ground.HolderId is null && held >= cap) blocked = $"You already run {held} of {cap} pieces of ground.";
        else if (ground.HolderId is null && freeThugs < config.MinimumGarrison) blocked = $"You need {config.MinimumGarrison} free thugs to hold it.";
        else if (ground.HolderId is null && player.Turns < config.ClaimTurnCost) blocked = $"Claiming takes {config.ClaimTurnCost} turns.";
        else if (ground.HolderId is not null && held >= cap) blocked = $"You already run {held} of {cap} pieces of ground.";

        return new TerritoryResponse(
            ground.Id,
            ground.Name,
            ground.City,
            ground.Type,
            type?.Label ?? ground.Type,
            DescribeEffect(type),
            ground.HolderId,
            ground.Holder?.Name,
            mine,
            ground.GarrisonThugs,
            ground.GarrisonPimp?.Name,
            pimps.GarrisonBonusPercent(ground.GarrisonPimp),
            ground.HeldSinceUtc,
            settled,
            ground.ProtectedUntilUtc,
            CanClaim: !mine && ground.HolderId is null && blocked is null,
            CanRaid: !mine && ground.HolderId is not null && blocked is null,
            blocked);
    }

    private static string DescribeEffect(TerritoryTypeOptions? type)
    {
        if (type is null) return "No effect.";
        if (type.StreetIncomePercent > 0) return $"+{type.StreetIncomePercent}% street income";
        if (type.ProductionYieldPercent > 0) return $"+{type.ProductionYieldPercent}% production yield";
        if (type.MoraleRecoveryPercent > 0) return $"+{type.MoraleRecoveryPercent}% passive morale recovery";
        if (type.LootPercent > 0) return $"+{type.LootPercent}% haul from raids";
        return "No effect.";
    }
}
