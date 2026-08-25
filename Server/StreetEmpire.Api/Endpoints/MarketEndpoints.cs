using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Services;
using static StreetEmpire.Api.Support.ActionLogging;

namespace StreetEmpire.Api.Endpoints;

/// <summary>The player-to-player board: what is for sale, listing, buying, and pulling a listing.</summary>
internal static class MarketEndpoints
{
    internal static void MapMarketEndpoints(this IEndpointRouteBuilder app)
    {

        app.MapGet("/api/game/market", async (
            CurrentPlayerService current,
            GameDbContext db,
            HideoutService hideouts,
            IOptionsSnapshot<GameOptions> gameOptions,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var options = gameOptions.Value;
            var listings = await db.MarketListings.AsNoTracking()
                .Include(x => x.Seller)
                .Where(x => x.CancelledAtUtc == null && x.Quantity > 0)
                .OrderBy(x => x.Item)
                .ThenBy(x => x.PricePerUnit)
                .ThenBy(x => x.CreatedAtUtc)
                .Take(200)
                .ToListAsync(ct);

            var capacity = hideouts.CapacityFor(player.Hideout);
            var goods = TradeGoods.Keys
                .Select(key => new MarketGoodResponse(
                    key,
                    TradeGoods.Label(key),
                    TradeGoods.ReferencePrice(options, key, player.City),
                    TradeGoods.Held(player, key),
                    TradeGoods.Room(player, capacity, key),
                    listings.Where(x => x.Item == key).Select(x => (long?)x.PricePerUnit).Min()))
                .ToList();

            return Results.Ok(new MarketBoardResponse(
                options.Market.HouseCutPercent,
                options.Market.MaxListingsPerPlayer,
                listings.Count(x => x.SellerId == player.Id),
                goods,
                listings.Select(x => new MarketListingResponse(
                    x.Id,
                    x.Item,
                    TradeGoods.Label(x.Item),
                    x.Quantity,
                    x.OriginalQuantity,
                    x.PricePerUnit,
                    x.Seller.Name,
                    x.SellerId == player.Id,
                    TradeGoods.ReferencePrice(options, x.Item, player.City),
                    x.CreatedAtUtc)).ToList()));
        }).RequireAuthorization();


        app.MapPost("/api/game/market/list", async (
            MarketListRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            MarketService market,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var now = DateTime.UtcNow;
            var before = Snapshot(player);
            try
            {
                var listing = await market.ListAsync(player, request.Item, request.Quantity, request.PricePerUnit, now, ct);
                var summary = $"Put {listing.Quantity:N0} {TradeGoods.Label(listing.Item).ToLowerInvariant()} on the market at {listing.PricePerUnit:C0} each.";
                AddLog(db, player, before, "MARKET", 0, summary, now);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new ActionResultResponse(summary, player.Turns, new Dictionary<string, object?>
                {
                    ["listingId"] = listing.Id,
                    ["item"] = listing.Item,
                    ["quantity"] = listing.Quantity,
                    ["pricePerUnit"] = listing.PricePerUnit
                }));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();


        app.MapPost("/api/game/market/buy", async (
            MarketBuyRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            MarketService market,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var now = DateTime.UtcNow;
            var before = Snapshot(player);
            try
            {
                var purchase = await market.BuyAsync(player, request.ListingId, request.Quantity, ct);
                var item = TradeGoods.Label(purchase.Listing.Item).ToLowerInvariant();
                var summary = $"Bought {purchase.Quantity:N0} {item} from {purchase.Listing.Seller.Name} for {purchase.Cost:C0}.";
                AddLog(db, player, before, "MARKET", 0, summary, now);
                // The seller is told, because a sale happens to them rather than because of them.
                db.ActionLogs.Add(new Models.GameActionLog
                {
                    PlayerId = purchase.Listing.SellerId,
                    Action = "SALE",
                    Summary = $"{player.Name} bought {purchase.Quantity:N0} {item} off you for {purchase.SellerPayout:C0} after the house took {purchase.HouseCut:C0}.",
                    BankDelta = purchase.SellerPayout,
                    CreatedAtUtc = now
                });
                await db.SaveChangesAsync(ct);
                return Results.Ok(new ActionResultResponse(summary, player.Turns, new Dictionary<string, object?>
                {
                    ["quantity"] = purchase.Quantity,
                    ["cost"] = purchase.Cost,
                    ["houseCut"] = purchase.HouseCut,
                    ["sellerPayout"] = purchase.SellerPayout
                }));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();


        app.MapPost("/api/game/market/cancel", async (
            MarketCancelRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            MarketService market,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var now = DateTime.UtcNow;
            var before = Snapshot(player);
            try
            {
                var result = await market.CancelAsync(player, request.ListingId, now, ct);
                var item = TradeGoods.Label(result.Listing.Item).ToLowerInvariant();
                var summary = result.LeftOnBoard > 0
                    ? $"Took {result.Returned:N0} {item} back. {result.LeftOnBoard:N0} stayed listed; your storage is full."
                    : $"Pulled {result.Returned:N0} {item} back off the market.";
                AddLog(db, player, before, "MARKET", 0, summary, now);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new ActionResultResponse(summary, player.Turns, new Dictionary<string, object?>
                {
                    ["returned"] = result.Returned,
                    ["leftOnBoard"] = result.LeftOnBoard
                }));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();


        // Weapons are the one thing a player can make that everybody needs, which is what gives the
        // board something worth trading.
        app.MapPost("/api/game/workshop/forge", async (
            ForgeRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            EconomyService economy,
            PlayerClock clock,
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
                    return Results.BadRequest(new { error = "The workshop is already crafting something." });
                }

                // The station used to say which room; there is one room now, so what arrives is what to
                // make. Either name is accepted: an old client asking for "still" wants moonshine.
                var asked = request.Weapon ?? request.Station?.Trim().ToLowerInvariant() switch
                {
                    "still" => "moonshine",
                    "mix" => "cut",
                    "workshop" => null,
                    { Length: > 0 } station => station,
                    _ => null
                };
                var craft = economy.StartCraft(player, request.Turns, asked, now);
                db.WorkshopCrafts.Add(craft);

                var minutes = Math.Max(1, (int)Math.Ceiling((craft.CompletesAtUtc - now).TotalMinutes));
                var summary = $"Started crafting {craft.Quantity:N0} {craft.Label.ToLowerInvariant()} with {craft.WorkUnits:N0} turn{(craft.WorkUnits == 1 ? string.Empty : "s")} and {craft.TotalCost:C0}. Ready in {minutes:N0} minute{(minutes == 1 ? string.Empty : "s")}.";
                AddLog(db, player, before, "WORKSHOP", craft.WorkUnits, summary, now);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new ActionResultResponse(summary, player.Turns, new Dictionary<string, object?>
                {
                    ["craftId"] = craft.Id,
                    ["good"] = craft.Good,
                    ["unitsQueued"] = craft.Quantity,
                    ["workUnits"] = craft.WorkUnits,
                    ["turnsSpent"] = craft.WorkUnits,
                    ["unitCost"] = craft.UnitCost,
                    ["totalCost"] = craft.TotalCost,
                    ["completesAtUtc"] = craft.CompletesAtUtc
                }));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();
    }
}
