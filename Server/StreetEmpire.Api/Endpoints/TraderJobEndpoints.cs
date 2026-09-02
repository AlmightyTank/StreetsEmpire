using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;
using StreetEmpire.Api.Services;
using static StreetEmpire.Api.Support.ActionLogging;

namespace StreetEmpire.Api.Endpoints;

/// <summary>
/// The dealer's board: the three jobs you are holding, handing goods into one, and asking about others.
///
/// One route group where there were two, which is the whole point of the merge - the wanted board and
/// the contract board were separate endpoints returning separate shapes that the client drew as two
/// panels stacked under one another, both headed with the same person's name.
/// </summary>
internal static class TraderJobEndpoints
{
    internal static void MapTraderJobEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/game/store/jobs", async (
            CurrentPlayerService current,
            TraderJobService jobs,
            IOptionsSnapshot<GameOptions> gameOptions,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var now = DateTime.UtcNow;
            var opts = gameOptions.Value;
            var hand = await jobs.HandAsync(player, now, ct);
            var book = await jobs.BookAsync(player.City, now, ct);
            var trader = StoreTrader.For(player.City, opts);

            return Results.Ok(new TraderJobBoardResponse(
                player.City,
                new TraderResponse(
                    trader.Name,
                    player.City,
                    trader.Pitch,
                    trader.Patter,
                    StoreTrader.Greeting(player, opts)),
                hand.Select(x => ToResponse(x.Job, x.Slot, player, opts, now)).ToList(),
                book.Count,
                RerollState(player, opts, now)));
        }).RequireAuthorization();


        app.MapPost("/api/game/store/jobs/{id:long}/fill", async (
            long id,
            DeliverJobRequest? request,
            CurrentPlayerService current,
            GameDbContext db,
            TraderJobService jobs,
            TraderShelfService shelves,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var job = await db.TraderJobs.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (job is null) return Results.NotFound(new { error = "That job is gone." });

            var before = Snapshot(player);
            try
            {
                var now = DateTime.UtcNow;
                var result = jobs.Deliver(job, player, now, request?.Quantity);
                // Goods brought in for a gap on their own shelf go onto that shelf. This is the whole
                // point of the reason existing: the counter was short, you filled it, and the line is on
                // sale again before the next delivery would have brought it.
                if (job.Reason == TraderJobReason.ShelfGap)
                    await shelves.RestockAsync(job.City, job.Good, result.Delivered, now, ct);
                AddLog(db, player, before, "JOB", 0, result.Summary);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new ActionResultResponse(
                    result.Summary,
                    player.Turns,
                    new Dictionary<string, object?>
                    {
                        ["jobId"] = job.Id,
                        ["good"] = job.Good,
                        ["delivered"] = result.Delivered,
                        ["paid"] = result.Paid,
                        ["premium"] = result.Premium,
                        ["completed"] = result.Completed,
                        ["repEarned"] = result.RepEarned,
                        ["remaining"] = job.Remaining,
                    }));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();


        // No turns. Asking somebody what else is going is a conversation, and what holds it back is the
        // money, the standing, and the dealer's patience - which is the cycle clock, not the turn bank.
        app.MapPost("/api/game/store/jobs/reroll", async (
            RerollJobsRequest? request,
            CurrentPlayerService current,
            GameDbContext db,
            TraderJobService jobs,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var before = Snapshot(player);
            try
            {
                var result = await jobs.RerollAsync(player, request?.Slots ?? [], DateTime.UtcNow, ct);
                AddLog(db, player, before, "JOB", 0, result.Summary);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new ActionResultResponse(
                    result.Summary,
                    player.Turns,
                    new Dictionary<string, object?>
                    {
                        ["cash"] = result.Cash,
                        ["rep"] = result.Rep,
                        ["replaced"] = result.Replaced,
                    }));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();
    }

    /// <summary>
    /// What the button costs right now, and what it would cost to take the whole hand at once.
    ///
    /// Both, because they are different decisions and the page should not make anybody multiply. The
    /// cycle is rolled over here for reading purposes only - a player whose clock has run should be
    /// quoted free before they press anything, not told the old price and then charged the new one.
    /// </summary>
    private static TraderJobRerollResponse RerollState(Player player, GameOptions options, DateTime nowUtc)
    {
        var config = options.Store.Jobs;
        var reroll = config.Reroll;
        var expired = player.JobRerollsResetAtUtc is not { } reset || reset <= nowUtc;
        var used = expired ? 0 : player.JobRerollsUsed;
        var freeAgain = expired ? null : player.JobRerollsResetAtUtc;

        var next = reroll.Step(used);
        var all = Enumerable.Range(0, Math.Max(1, config.HandSize)).Select(i => reroll.Step(used + i)).ToList();
        var floor = options.Store.LevelFor(player.StoreRep)?.Rep ?? 0;

        return new TraderJobRerollResponse(
            next.Cash,
            next.Rep,
            all.Sum(x => x.Cash),
            all.Sum(x => x.Rep),
            used,
            freeAgain,
            freeAgain is null ? 0 : Math.Max(0, (int)Math.Ceiling((freeAgain.Value - nowUtc).TotalSeconds)),
            Math.Max(0, (int)Math.Floor(player.StoreRep) - floor));
    }

    /// <summary>
    /// One row, answered for the player looking at it: what they hold, what they could hand over now,
    /// and why they cannot when they cannot.
    /// </summary>
    /// <summary>
    /// Why the dealer is asking, in their own terms.
    ///
    /// Written here rather than stored, so retuning what the four reasons sound like is a text change
    /// rather than a migration over every open job in the world.
    /// </summary>
    private static string ReasonLine(TraderJob job, GameOptions options)
    {
        var good = TradeGoods.Label(job.Good).ToLowerInvariant();
        // A gap that could not really be a gap does not get to claim it is one. The shelf already
        // ignores those - a line on the floor is never dry - and a row still saying "out of pistols"
        // beside a counter selling pistols would be the interface disagreeing with itself in front of
        // the player. Legacy rows and retuned stock lists both land here.
        var couldBeDry = !StoreTrader.Always.Contains(job.Good)
                         && StoreTrader.Carries(job.City, options, job.Good);
        return job.Reason switch
        {
            TraderJobReason.ShelfGap when couldBeDry => $"Out of {good}, and people keep asking",
            TraderJobReason.ShelfGap => $"Wants {good} put by, and is short",
            TraderJobReason.CoveringTrader => $"Covering {job.OnBehalfOf ?? "another counter"}",
            TraderJobReason.Favour => $"Doing {job.OnBehalfOf ?? "somebody"} a favour",
            _ => job.OnBehalfOf is null
                ? "Promised it to somebody and came up short"
                : $"Promised it to {job.OnBehalfOf} and came up short",
        };
    }

    private static TraderJobResponse ToResponse(TraderJob job, int slot, Player player, GameOptions options, DateTime nowUtc)
    {
        var held = TradeGoods.Held(player, job.Good);
        var canDeliver = Math.Min(held, job.Remaining);
        var tier = options.WeaponTier(job.Good);
        var workshop = player.Hideout?.WorkshopLevel ?? 0;
        var makeable = options.Makeables.FirstOrDefault(x => x.Key == job.Good);
        var levelNeeded = tier?.CanForge == true ? tier.MinWorkshopLevel
            : makeable?.CanMake == true ? makeable.MinWorkshopLevel
            : (int?)null;

        string? blocked = null;
        if (!string.Equals(job.City, player.City, StringComparison.OrdinalIgnoreCase))
            blocked = $"This one is in {job.City}.";
        else if (!job.CanBeWorkedBy(player.Id))
            blocked = "Somebody else is filling that one.";
        else if (job.MinimumPurityPercent is { } floor && (int)Math.Round(player.CokePurity * 100) < floor)
            blocked = $"They want it at least {floor}% pure. Yours is {(int)Math.Round(player.CokePurity * 100)}%.";
        else if (held <= 0)
            blocked = $"You have no {TradeGoods.Label(job.Good).ToLowerInvariant()} to hand over.";

        return new TraderJobResponse(
            job.Id,
            slot,
            job.Kind.ToString(),
            StoreTrader.For(job.City, options).Name,
            ReasonLine(job, options),
            job.Good,
            TradeGoods.Label(job.Good),
            job.Quantity,
            job.PricePerUnit,
            job.ReferencePricePerUnit,
            job.Payout,
            job.CompletionBonus,
            job.MinimumPurityPercent,
            job.Rep,
            Math.Max(0, (int)Math.Ceiling((job.ExpiresAtUtc - nowUtc).TotalMinutes)),
            held,
            job.DeliveredQuantity,
            job.Remaining,
            canDeliver,
            levelNeeded is not null && workshop >= levelNeeded,
            levelNeeded is not null && workshop < levelNeeded ? levelNeeded : null,
            job.DeliveredQuantity > 0 && job.ClaimedById == player.Id,
            blocked);
    }
}
