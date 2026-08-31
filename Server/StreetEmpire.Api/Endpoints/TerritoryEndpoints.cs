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
            PlayerClock clock,
            IOptionsSnapshot<GameOptions> gameOptions,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var now = DateTime.UtcNow;
            await combatResolver.ResolveDueAsync(now, ct);
            // This page is the one that reads the building to decide how much ground the player may
            // run, and it was the one page that never settled a finished build. A Warehouse paid for
            // and finished still reported a Trap House's single plot until something else happened to
            // advance the clock - so the map refused a second piece the claim endpoint would have
            // allowed, and offered no button to find that out with.
            if ((await clock.AdvanceAsync(player, now, db, ct)).Changed)
                await db.SaveChangesAsync(ct);
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
                Ladder(gameOptions.Value, player),
                all.Select(x => Describe(x, player, territories, pimps, now, mine.Count, cap, free, config, gameOptions.Value)).ToList()));
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


        // Money into the ground rather than into the building. The only sink in the game priced to be
        // months, and the only one somebody else can come and take a share of.
        app.MapPost("/api/game/territories/develop", async (
            TerritoryDevelopRequest request,
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
                var (ground, level, fromBank) = await territories.DevelopAsync(player, request.TerritoryId, now, ct);
                var summary = $"Started working {ground.Name} up to {level.Name} for {level.Cost:C0}.";
                // TERRITORY, not GROUND: this is ground the player acted on, and GROUND is the
                // action reserved for ground news happening to them. Filed under the wrong one it
                // reads back as "You lost ground" over a sentence saying you bought some.
                AddLog(db, player, before, "TERRITORY", level.Turns, summary, now);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new ActionResultResponse(summary, player.Turns, new Dictionary<string, object?>
                {
                    ["territoryId"] = ground.Id,
                    ["level"] = level.Level,
                    ["cost"] = level.Cost,
                    ["paidFromBank"] = fromBank,
                    ["completesAtUtc"] = ground.DevelopmentCompletesAtUtc
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
        TerritoryOptions config,
        GameOptions options)
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

        // Only ever offered on your own ground, and only the rung immediately above what is standing.
        // A ladder shown against somebody else's corner would be a price list for a thing you cannot buy.
        var next = mine ? config.DevelopmentAfter(ground.DevelopmentLevel) : null;
        var tier = player.Hideout?.Tier ?? 1;

        return new TerritoryResponse(
            ground.Id,
            ground.Name,
            ground.City,
            ground.Type,
            type?.Label ?? ground.Type,
            DescribeEffect(type, config.DevelopmentMultiplier(ground.DevelopmentLevel)),
            ground.HolderId,
            ground.Holder?.Name,
            mine,
            ground.GarrisonThugs,
            ground.GarrisonPimp?.Name,
            pimps.GarrisonBonusPercent(ground.GarrisonPimp),
            ground.HeldSinceUtc,
            settled,
            ground.ProtectedUntilUtc,
            ground.DevelopmentLevel,
            territories.DevelopmentName(ground.DevelopmentLevel),
            config.DevelopmentAt(ground.DevelopmentLevel)?.EffectPercent ?? 0,
            config.DevelopmentDefencePercent(ground.DevelopmentLevel),
            next is null
                ? null
                : new TerritoryDevelopmentUpgradeResponse(
                    next.Level,
                    next.Name,
                    next.Cost,
                    next.Turns,
                    next.BuildMinutes,
                    next.EffectPercent,
                    next.DefencePercent,
                    next.MinTier,
                    TierName(options, next.MinTier),
                    next.MinTier > tier,
                    BaseEffect(type, config.DevelopmentMultiplier(ground.DevelopmentLevel)),
                    BaseEffect(type, config.DevelopmentMultiplier(next.Level))),
            ground is { DevelopingToLevel: { } building, DevelopmentCompletesAtUtc: { } due }
                ? new TerritoryDevelopmentBuildResponse(
                    building,
                    territories.DevelopmentName(building),
                    due,
                    Math.Max(0, (int)Math.Ceiling((due - nowUtc).TotalSeconds)))
                : null,
            CanClaim: !mine && ground.HolderId is null && blocked is null,
            CanRaid: !mine && ground.HolderId is not null && blocked is null,
            blocked);
    }

    /// <summary>
    /// The whole ladder, marked with what this player's building can currently reach. Shown in full
    /// rather than one rung at a time because it is the only thing in the game that takes months, and
    /// a months-long climb nobody can see the shape of is not a goal, it is a surprise.
    /// </summary>
    private static List<TerritoryDevelopmentRungResponse> Ladder(GameOptions options, Player player)
    {
        var tier = player.Hideout?.Tier ?? 1;
        return options.Territory.Development
            .OrderBy(x => x.Level)
            .Select(x => new TerritoryDevelopmentRungResponse(
                x.Level,
                x.Name,
                x.Cost,
                x.Turns,
                x.BuildMinutes,
                x.EffectPercent,
                x.DefencePercent,
                x.MinTier,
                TierName(options, x.MinTier),
                x.MinTier <= tier))
            .ToList();
    }

    private static string TierName(GameOptions options, int tier)
        => options.Hideout.Tiers.FirstOrDefault(x => x.Level == tier)?.Name ?? $"tier {tier}";

    /// <summary>The type's own effect, scaled by what has been put into the ground.</summary>
    private static int BaseEffect(TerritoryTypeOptions? type, double multiplier)
    {
        if (type is null) return 0;
        var percent = Math.Max(
            Math.Max(type.StreetIncomePercent, type.ProductionYieldPercent),
            Math.Max(type.MoraleRecoveryPercent, type.LootPercent));
        return (int)Math.Round(percent * multiplier, MidpointRounding.AwayFromZero);
    }

    private static string DescribeEffect(TerritoryTypeOptions? type, double multiplier)
    {
        if (type is null) return "No effect.";
        // Read off the developed number rather than the table's, or a corner somebody has spent
        // months on would advertise the fifteen percent it was worth on the day they took it.
        static int Worked(int percent, double multiplier)
            => (int)Math.Round(percent * multiplier, MidpointRounding.AwayFromZero);
        if (type.StreetIncomePercent > 0) return $"+{Worked(type.StreetIncomePercent, multiplier)}% street income";
        if (type.ProductionYieldPercent > 0) return $"+{Worked(type.ProductionYieldPercent, multiplier)}% production yield";
        if (type.MoraleRecoveryPercent > 0) return $"+{Worked(type.MoraleRecoveryPercent, multiplier)}% passive morale recovery";
        if (type.LootPercent > 0) return $"+{Worked(type.LootPercent, multiplier)}% haul from raids";
        return "No effect.";
    }
}
