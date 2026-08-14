using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

/// <summary>
/// The player-to-player board: listing goods, buying them, and pulling a listing back.
///
/// Everything here is about not creating or destroying goods by accident. Stock is escrowed out of the
/// seller the moment it is listed, cash moves in one step with the goods, and anything that cannot be
/// delivered is refused before either side is touched.
/// </summary>
public sealed class MarketService(GameDbContext db, HideoutService hideouts, IOptionsSnapshot<GameOptions> options)
{
    private readonly GameOptions _options = options.Value;

    /// <summary>
    /// Puts goods up for sale. The stock leaves storage now rather than on sale: left with the seller
    /// it could be listed twice, spent, or stolen out from under a buyer.
    /// </summary>
    public async Task<MarketListing> ListAsync(Player seller, string? item, int quantity, long pricePerUnit, DateTime nowUtc, CancellationToken ct)
    {
        var config = _options.Market;
        var key = TradeGoods.Normalise(item);
        if (!TradeGoods.IsTradeable(key))
            throw new GameRuleException($"You can only trade {string.Join(", ", TradeGoods.Keys)}.");
        if (quantity < 1)
            throw new GameRuleException("List at least one.");
        if (quantity > config.MaxQuantityPerListing)
            throw new GameRuleException($"One listing tops out at {config.MaxQuantityPerListing:N0}.");
        if (pricePerUnit < 1)
            throw new GameRuleException("Name a price above zero.");

        var reference = TradeGoods.ReferencePrice(_options, key);
        var floor = (long)Math.Floor(reference * config.MinPriceMultiplier);
        var ceiling = (long)Math.Ceiling(reference * config.MaxPriceMultiplier);
        if (reference > 0 && (pricePerUnit < floor || pricePerUnit > ceiling))
            throw new GameRuleException($"{TradeGoods.Label(key)} has to be priced between {floor:C0} and {ceiling:C0} each.");

        var open = await db.MarketListings.CountAsync(x => x.SellerId == seller.Id && x.CancelledAtUtc == null && x.Quantity > 0, ct);
        if (open >= config.MaxListingsPerPlayer)
            throw new GameRuleException($"You already have {open:N0} listings up. Pull one first.");

        var held = TradeGoods.Held(seller, key);
        if (held < quantity)
            throw new GameRuleException($"You only have {held:N0} {TradeGoods.Label(key).ToLowerInvariant()}.");

        TradeGoods.Add(seller, key, -quantity);
        var listing = new MarketListing
        {
            SellerId = seller.Id,
            Seller = seller,
            Item = key,
            Quantity = quantity,
            OriginalQuantity = quantity,
            PricePerUnit = pricePerUnit,
            CreatedAtUtc = nowUtc
        };
        db.MarketListings.Add(listing);
        return listing;
    }

    /// <summary>
    /// Buys some or all of a listing. Partial fills are allowed because a listing of two hundred
    /// weapons that only one buyer in the game can afford is a listing nobody buys.
    /// </summary>
    public async Task<MarketPurchase> BuyAsync(Player buyer, long listingId, int quantity, CancellationToken ct)
    {
        var listing = await db.MarketListings
            .Include(x => x.Seller)
            .SingleOrDefaultAsync(x => x.Id == listingId, ct)
            ?? throw new GameRuleException("That listing is gone.");

        if (!listing.IsOpen)
            throw new GameRuleException("That listing has already been taken.");
        if (listing.SellerId == buyer.Id)
            throw new GameRuleException("You cannot buy your own listing.");
        if (quantity < 1)
            throw new GameRuleException("Buy at least one.");
        if (quantity > listing.Quantity)
            throw new GameRuleException($"Only {listing.Quantity:N0} left in that listing.");

        var cost = listing.PricePerUnit * quantity;
        if (buyer.Cash < cost)
            throw new GameRuleException($"That comes to {cost:C0} and you have {buyer.Cash:C0} on hand.");

        // Refused up front rather than delivered and spilled, the same way a store purchase is. Goods
        // paid for and then lost to an overflowing room would be the worst of both.
        var capacity = hideouts.CapacityFor(buyer.Hideout);
        var room = TradeGoods.Capacity(capacity, listing.Item) - TradeGoods.Held(buyer, listing.Item);
        if (quantity > room)
            throw new GameRuleException($"Your storage has room for {Math.Max(0, room):N0} more {TradeGoods.Label(listing.Item).ToLowerInvariant()}.");

        var cut = (long)Math.Round(cost * (Math.Clamp(_options.Market.HouseCutPercent, 0, 100) / 100.0), MidpointRounding.AwayFromZero);
        var payout = cost - cut;

        buyer.Cash -= cost;
        TradeGoods.Add(buyer, listing.Item, quantity);
        listing.Quantity -= quantity;

        // Straight to the seller's bank. Cash on hand is capped by their safe and stealable, and a sale
        // that overflowed into nothing while they were offline would be a hole in the economy.
        listing.Seller.BankCash += payout;

        return new MarketPurchase(listing, quantity, cost, cut, payout);
    }

    /// <summary>
    /// Pulls a listing and hands the stock back. Anything that will not fit stays on the board rather
    /// than evaporating, so a full room cannot cost the seller their goods.
    /// </summary>
    public async Task<MarketWithdrawal> CancelAsync(Player seller, long listingId, DateTime nowUtc, CancellationToken ct)
    {
        var listing = await db.MarketListings.SingleOrDefaultAsync(x => x.Id == listingId, ct)
            ?? throw new GameRuleException("That listing is gone.");
        if (listing.SellerId != seller.Id)
            throw new GameRuleException("That is not your listing.");
        if (!listing.IsOpen)
            throw new GameRuleException("That listing is already closed.");

        var capacity = hideouts.CapacityFor(seller.Hideout);
        var room = Math.Max(0, TradeGoods.Capacity(capacity, listing.Item) - TradeGoods.Held(seller, listing.Item));
        var returned = Math.Min(listing.Quantity, room);

        TradeGoods.Add(seller, listing.Item, returned);
        listing.Quantity -= returned;
        if (listing.Quantity == 0)
            listing.CancelledAtUtc = nowUtc;

        return new MarketWithdrawal(listing, returned, listing.Quantity);
    }
}

public sealed record MarketPurchase(MarketListing Listing, int Quantity, long Cost, long HouseCut, long SellerPayout);

/// <param name="LeftOnBoard">Stock that would not fit back in storage, so the listing stays up for it.</param>
public sealed record MarketWithdrawal(MarketListing Listing, int Returned, int LeftOnBoard);
