using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;
using StreetEmpire.Api.Services;
using static StreetEmpire.Api.Support.ActionLogging;

namespace StreetEmpire.Api.Endpoints;

/// <summary>
/// The trader's own board: what the counter in this town is short of, and handing it over.
///
/// Its own route rather than more fields on the dashboard, like the contract board beside it. The
/// dashboard is fetched on every visit and this is a list that tops itself up when somebody reads it -
/// posting orders as a side effect of loading the overview would fill every town in the game for a
/// player who never goes near a shop.
/// </summary>
internal static class WantedEndpoints
{
    internal static void MapWantedEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/game/store/wanted", async (
            CurrentPlayerService current,
            WantedService wanted,
            HideoutService hideouts,
            IOptionsSnapshot<GameOptions> gameOptions,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var opts = gameOptions.Value;
            var now = DateTime.UtcNow;
            var board = await wanted.BoardAsync(player.City, now, ct);
            var trader = StoreTrader.For(player.City, opts);

            return Results.Ok(new WantedBoardResponse(
                player.City,
                new TraderResponse(
                    trader.Name,
                    player.City,
                    trader.Pitch,
                    trader.Patter,
                    StoreTrader.Greeting(player, opts)),
                board.Select(x => ToResponse(x, player, opts, hideouts, now)).ToList()));
        }).RequireAuthorization();


        app.MapPost("/api/game/store/wanted/{id:long}/fill", async (
            long id,
            DeliverWantedRequest? request,
            CurrentPlayerService current,
            GameDbContext db,
            PlayerClock clock,
            WantedService wanted,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var now = DateTime.UtcNow;
            await clock.AdvanceAsync(player, now, db, ct);

            var order = await db.WantedOrders.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (order is null) return Results.NotFound(new { error = "No such order." });

            var before = Snapshot(player);
            try
            {
                var fill = wanted.Deliver(order, player, now, request?.Quantity);
                AddLog(db, player, before, "WANTED", 0, fill.Summary, now);
                await db.SaveChangesAsync(ct);

                return Results.Ok(new ActionResultResponse(fill.Summary, player.Turns, new Dictionary<string, object?>
                {
                    ["good"] = order.Good,
                    ["handedOver"] = fill.Delivered,
                    ["stillWanted"] = order.Remaining,
                    ["pricePerUnit"] = order.PricePerUnit,
                    ["paid"] = fill.Paid,
                    ["repEarned"] = fill.RepEarned,
                    ["rep"] = (int)Math.Floor(player.StoreRep)
                }));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();
    }

    /// <summary>
    /// One order as the board shows it, including whether this player's bench could actually make the
    /// thing. An order for SMGs is a different proposition to somebody with a level 4 workshop than to
    /// somebody with none, and the row is the only place that difference can be said.
    /// </summary>
    private static WantedOrderResponse ToResponse(
        WantedOrder order,
        Player player,
        GameOptions options,
        HideoutService hideouts,
        DateTime nowUtc)
    {
        var held = TradeGoods.Held(player, order.Good);
        var workshop = player.Hideout?.WorkshopLevel ?? 0;
        var needed = options.WeaponTier(order.Good) is { CanForge: true } tier
            ? tier.MinWorkshopLevel
            : options.Makeables.FirstOrDefault(x => x.Key == order.Good && x.CanMake)?.MinWorkshopLevel;

        return new WantedOrderResponse(
            order.Id,
            order.Good,
            TradeGoods.Label(order.Good),
            order.Quantity,
            order.PricePerUnit,
            order.ShopPricePerUnit,
            order.Payout,
            order.Rep,
            Math.Max(0, (int)Math.Ceiling((order.ExpiresAtUtc - nowUtc).TotalMinutes)),
            held,
            order.DeliveredQuantity,
            order.Remaining,
            Math.Min(held, order.Remaining),
            needed is not null && workshop >= needed,
            needed is not null && workshop < needed ? needed : null,
            order.ClaimedById == player.Id,
            Blocked(order, player, held));
    }

    /// <summary>
    /// Why the row cannot be acted on, in the words the refusal would use. Null when it can, because a
    /// button that is grey for a reason nobody prints is the complaint this game has already answered
    /// once for the morale controls.
    /// </summary>
    private static string? Blocked(WantedOrder order, Player player, int held)
    {
        if (!order.CanBeWorkedBy(player.Id)) return "Somebody else is filling this one.";
        if (held <= 0) return $"You have no {TradeGoods.Label(order.Good).ToLowerInvariant()} to hand over.";
        return null;
    }
}
