using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;
using StreetEmpire.Api.Services;
using static StreetEmpire.Api.Support.ActionLogging;

namespace StreetEmpire.Api.Endpoints;

/// <summary>
/// Sending crew to another town to buy cheap and carry it home: what a run would cost, and starting
/// one. Runs are never settled here. They come home on the clock, whether or not anybody asked.
/// </summary>
internal static class MuleEndpoints
{
    internal static void MapMuleEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/game/mules", async (
            CurrentPlayerService current,
            GameDbContext db,
            PlayerClock clock,
            MuleService mules,
            HideoutService hideouts,
            TerritoryService territories,
            IOptionsSnapshot<GameOptions> gameOptions,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var now = DateTime.UtcNow;
            // Advancing first means a run that landed while they were away is already home and in the
            // list below as a result, rather than showing as still in the air on the page that exists
            // to tell them where it is.
            var tick = await clock.AdvanceAsync(player, now, db, ct);
            if (tick.Changed) await db.SaveChangesAsync(ct);

            return Results.Ok(await BoardAsync(player, db, mules, hideouts, territories, gameOptions.Value, now, ct));
        }).RequireAuthorization();

        app.MapPost("/api/game/mules/quote", async (
            MuleQuoteRequest request,
            CurrentPlayerService current,
            MuleService mules,
            IOptionsSnapshot<GameOptions> gameOptions,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            try
            {
                var quote = mules.Quote(player, request.City, request.Good, request.Hoes, request.Cash);
                var options = gameOptions.Value;
                var home = options.CityMarkets.ProductPrice(player.City, quote.Good, BasePrice(options, quote.Good));
                // What the run actually costs is the fares plus what they really spend, not the whole
                // purse: cash they cannot carry goods for comes home again. Quoting the purse as the
                // cost made every run look like a loss and hid the one figure that decides it.
                var spend = quote.UnitsAffordable * quote.UnitPriceThere;
                var profit = quote.UnitsAffordable * home - (quote.Fare + quote.Upkeep + spend);
                return Results.Ok(new MuleQuoteResponse(
                    quote.DestinationCity,
                    quote.Good,
                    quote.Hoes,
                    quote.Capacity,
                    quote.Turns,
                    quote.LegMinutes,
                    quote.TripMinutes,
                    quote.Fare,
                    quote.Upkeep,
                    quote.CashSent,
                    quote.TotalCost,
                    quote.UnitPriceThere,
                    quote.UnitsAffordable,
                    home,
                    quote.UnitsAffordable * home,
                    spend,
                    profit,
                    quote.SupplyTurns,
                    quote.CondomsNeeded,
                    quote.CondomsUsed,
                    quote.BeerNeeded,
                    quote.BeerUsed,
                    quote.MoonshineUsed,
                    quote.BustChancePercent,
                    quote.DefectChancePercent));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();

        app.MapPost("/api/game/mules/launch", async (
            MuleLaunchRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            PlayerClock clock,
            MuleService mules,
            TerritoryService territories,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var now = DateTime.UtcNow;
            await clock.AdvanceAsync(player, now, db, ct);

            var pimp = await db.Pimps
                .SingleOrDefaultAsync(x => x.Id == request.PimpId && x.PlayerId == player.Id && x.LostAtUtc == null, ct);
            if (pimp is null) return Results.BadRequest(new { error = "Pick a pimp to lead the run." });

            // A pimp holding ground is not standing around waiting for a plane. Checked here rather
            // than in the service because who is away is a fact about the world, not about the run.
            var garrisoned = await territories.GarrisonedPimpIdsAsync(player.Id, ct);
            if (garrisoned.Contains(pimp.Id))
                return Results.BadRequest(new { error = $"{pimp.Name} is holding ground. Pull them off it first." });

            var out_ = await db.MuleRuns.CountAsync(x => x.PlayerId == player.Id && x.SettledAtUtc == null, ct);
            var before = Snapshot(player);
            try
            {
                var quote = mules.Quote(player, request.City, request.Good, request.Hoes, request.Cash);
                var run = mules.Launch(player, pimp, request.City, request.Good, request.Hoes, request.Cash, out_, now);
                db.MuleRuns.Add(run);
                AddLog(db, player, before, "MULE_SENT", run.TurnsSpent, run.Summary, now);
                await db.SaveChangesAsync(ct);

                return Results.Ok(new ActionResultResponse(
                    $"{run.Summary} They are back in {(int)Math.Ceiling((run.ReturnsAtUtc - now).TotalMinutes)} minute(s).",
                    player.Turns,
                    new Dictionary<string, object?>
                    {
                        ["runId"] = run.Id,
                        ["destination"] = run.DestinationCity,
                        ["good"] = run.Good,
                        ["capacity"] = run.Capacity,
                        ["cashSent"] = run.CashSent,
                        ["fare"] = run.TravelCost,
                        ["upkeep"] = run.UpkeepCost,
                        ["supplyTurns"] = quote.SupplyTurns,
                        ["condomsUsed"] = quote.CondomsUsed,
                        ["beerUsed"] = quote.BeerUsed,
                        ["moonshineUsed"] = quote.MoonshineUsed,
                        ["turnsSpent"] = run.TurnsSpent,
                        ["bustChancePercent"] = run.BustChancePercent,
                        ["defectChancePercent"] = run.DefectChancePercent,
                        ["returnsAtUtc"] = run.ReturnsAtUtc
                    }));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();
    }

    private static async Task<MuleBoardResponse> BoardAsync(
        Player player,
        GameDbContext db,
        MuleService mules,
        HideoutService hideouts,
        TerritoryService territories,
        GameOptions options,
        DateTime nowUtc,
        CancellationToken ct)
    {
        // Everything out, plus what came home recently: a run the player never saw land would
        // otherwise vanish between one visit and the next.
        var runs = await db.MuleRuns.AsNoTracking()
            .Where(x => x.PlayerId == player.Id)
            .Where(x => x.SettledAtUtc == null || x.SettledAtUtc >= nowUtc.AddHours(-12))
            .OrderByDescending(x => x.DepartedAtUtc)
            .Take(20)
            .ToListAsync(ct);

        var away = await territories.GarrisonedPimpIdsAsync(player.Id, ct);
        var onRuns = runs.Where(x => x.SettledAtUtc == null && x.PimpId is not null)
            .Select(x => x.PimpId!.Value)
            .ToHashSet();

        var pimps = await db.Pimps.AsNoTracking()
            .Where(x => x.PlayerId == player.Id && x.LostAtUtc == null)
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

        var destinations = options.CityMarkets.Profiles
            .Where(x => !string.Equals(x.City, player.City, StringComparison.OrdinalIgnoreCase))
            .Select(profile => new MuleDestinationResponse(
                profile.City,
                profile.Risk,
                options.CityMarkets.TravelTurns(profile.City),
                options.CityMarkets.TravelTurns(profile.City) * Math.Max(1, options.Mules.MinutesPerTravelTurn),
                options.CityMarkets.ProductPrice(profile.City, "weed", options.WeedSellPrice),
                options.CityMarkets.ProductPrice(profile.City, "coke", options.CokeSellPrice),
                mules.BustChancePercent(player, profile.City, 1)))
            .ToList();

        return new MuleBoardResponse(
            hideouts.ConcurrentRunCap(player.Hideout),
            runs.Count(x => x.SettledAtUtc == null),
            player.Hideout?.IntelligenceLevel ?? 0,
            player.Hoes,
            options.Mules.MaxHoesPerRun,
            options.Mules.HoeCarryCapacity,
            destinations,
            pimps.Select(pimp => new MuleCandidateResponse(
                pimp.Id,
                pimp.Name,
                pimp.Specialty,
                (int)Math.Round(pimp.Loyalty),
                away.Contains(pimp.Id) || onRuns.Contains(pimp.Id),
                onRuns.Contains(pimp.Id) ? "On a run" : away.Contains(pimp.Id) ? "Holding ground" : null)).ToList(),
            runs.Select(run => ToResponse(run, mules, nowUtc)).ToList());
    }

    private static MuleRunResponse ToResponse(MuleRun run, MuleService mules, DateTime nowUtc)
        => new(
            run.Id,
            run.DestinationCity,
            run.Good,
            mules.StatusAt(run, nowUtc),
            run.Outcome,
            run.PimpName,
            run.AssignedHoes,
            run.Capacity,
            run.CashSent,
            run.UnitsBought,
            run.SeizedUnits,
            run.CashReturned,
            run.BustChancePercent,
            run.DefectChancePercent,
            run.ArrivesAtUtc,
            run.ReturnsAtUtc,
            run.SettledAtUtc is not null ? 0 : Math.Max(0, (int)Math.Ceiling((run.ReturnsAtUtc - nowUtc).TotalSeconds)),
            run.Summary);

    private static int BasePrice(GameOptions options, string good)
        => good == "coke" ? options.CokeSellPrice : options.WeedSellPrice;
}
