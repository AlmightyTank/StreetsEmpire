using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;
using StreetEmpire.Api.Services;
using static StreetEmpire.Api.Support.ActionLogging;

namespace StreetEmpire.Api.Endpoints;

/// <summary>
/// The cell, and the two ways out of it.
///
/// Nobody is ever arrested here - that happens on the shift that caused it. What is here is the answer
/// to it, which is the only part that is a decision: pay the bond, or say out loud that you are not
/// going to and take what that costs.
///
/// Every route advances the clock first. A window that ran out while the player was away has already
/// been settled by the time the list is drawn, so the page can never offer a bond on somebody who is
/// long gone.
/// </summary>
internal static class ArrestEndpoints
{
    internal static void MapArrestEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/game/arrests", async (
            CurrentPlayerService current,
            GameDbContext db,
            PlayerClock clock,
            IOptionsSnapshot<GameOptions> gameOptions,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var now = DateTime.UtcNow;
            var tick = await clock.AdvanceAsync(player, now, db, ct);
            if (tick.Changed) await db.SaveChangesAsync(ct);

            return Results.Ok(await BoardAsync(player, db, gameOptions.Value, now, ct));
        }).RequireAuthorization();

        app.MapPost("/api/game/arrests/{id:long}/bail", async (
            long id,
            CurrentPlayerService current,
            GameDbContext db,
            PlayerClock clock,
            ArrestService arrests,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var now = DateTime.UtcNow;
            await clock.AdvanceAsync(player, now, db, ct);

            var arrest = await HeldAsync(db, player.Id, id, ct);
            if (arrest is null)
                return Results.BadRequest(new { error = "Nobody is being held on that." });

            var before = Snapshot(player);
            try
            {
                var summary = arrests.Bail(player, arrest, now);
                // BAIL rather than ARREST: this is the player answering, which belongs in their own
                // activity, while ARREST is the deadline running out while nobody was looking and
                // belongs in the bell. One string for both would have filed a button they just
                // pressed as news, which is the same trap GROUND and TERRITORY are kept apart for.
                AddLog(db, player, before, "BAIL", 0, summary, now);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new ActionResultResponse(summary, player.Turns, new Dictionary<string, object?>
                {
                    ["arrestId"] = arrest.Id,
                    ["bailPaid"] = arrest.BailAmount,
                    ["hoes"] = arrest.Hoes,
                    ["thugs"] = arrest.Thugs,
                    ["pimpName"] = arrest.PimpName
                }));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();

        app.MapPost("/api/game/arrests/{id:long}/abandon", async (
            long id,
            CurrentPlayerService current,
            GameDbContext db,
            PlayerClock clock,
            ArrestService arrests,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var now = DateTime.UtcNow;
            await clock.AdvanceAsync(player, now, db, ct);

            var arrest = await HeldAsync(db, player.Id, id, ct);
            if (arrest is null)
                return Results.BadRequest(new { error = "Nobody is being held on that." });

            // Deliberately allowed rather than made to wait the window out. The cost is identical
            // either way, so refusing would only mean a player who has decided still has the row on
            // their page for six hours, and a decision the game will not let you act on is not one.
            var before = Snapshot(player);
            var gone = arrests.Abandon(player, arrest, now);
            var said = gone.Describe(deliberate: true);
            AddLog(db, player, before, "BAIL", 0, said, now);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new ActionResultResponse(said, player.Turns, new Dictionary<string, object?>
            {
                ["arrestId"] = arrest.Id,
                ["hoes"] = gone.Hoes,
                ["thugs"] = gone.Thugs,
                ["pimpName"] = gone.PimpName,
                ["moralePenalty"] = Math.Round(gone.MoralePenalty, 1),
                ["talked"] = gone.Talked
            }));
        }).RequireAuthorization();
    }

    /// <summary>
    /// One row this player is actually being held on.
    ///
    /// Scoped to the player in the query rather than checked afterwards, so an id belonging to somebody
    /// else is indistinguishable from one that does not exist - guessing at numbers should not be able
    /// to tell you whether a rival is holding anybody.
    /// </summary>
    private static Task<Arrest?> HeldAsync(GameDbContext db, Guid playerId, long id, CancellationToken ct)
        => db.Arrests
            .Include(x => x.Pimp)
            .SingleOrDefaultAsync(x => x.Id == id && x.PlayerId == playerId && x.SettledAtUtc == null, ct);

    private static async Task<ArrestBoardResponse> BoardAsync(
        Player player,
        GameDbContext db,
        GameOptions options,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var held = await db.Arrests.AsNoTracking()
            .Where(x => x.PlayerId == player.Id && x.SettledAtUtc == null)
            .OrderBy(x => x.BailDeadlineUtc)
            .ToListAsync(ct);

        var funds = player.Cash + player.BankCash;
        return new ArrestBoardResponse(
            held.Select(x => new ArrestResponse(
                x.Id,
                x.Hoes,
                x.Thugs,
                x.PimpName,
                x.Heads,
                x.BailAmount,
                funds >= x.BailAmount,
                x.City,
                x.District,
                x.ChancePercent,
                x.ArrestedAtUtc,
                x.BailDeadlineUtc,
                Math.Max(0, (int)Math.Ceiling((x.BailDeadlineUtc - nowUtc).TotalSeconds)))).ToList(),
            held.Sum(x => x.BailAmount),
            funds,
            Math.Max(1, options.Arrests.BailWindowHours));
    }

}
