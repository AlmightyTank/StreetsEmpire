using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

/// <summary>
/// What the town's trader is short of, and handing it over.
///
/// Standing could be bought and it could be trickled out of ordinary shopping, and neither of those is
/// playing: one is a wallet and the other is a side effect of restocking. This is the version somebody
/// actually does - the trader wants twelve shotguns by Thursday, you have a workshop, and now the room
/// that was the way around the rep gate is also the way to earn it.
///
/// Generated on read at a pace, the way contracts and ground are, for the same three reasons: a town
/// nobody has visited needs no board, topping up when somebody looks cannot drift out of step with a
/// timer nobody is watching, and a board that refilled on demand would be a rep tap - fill one, look
/// again, repeat until the ladder is climbed in an afternoon.
/// </summary>
public sealed class WantedService(GameDbContext db, IOptionsSnapshot<GameOptions> options, IGameRandom random)
{
    private readonly GameOptions _options = options.Value;

    /// <summary>
    /// What the trader wants asked for, and never anything else.
    ///
    /// Only things a workshop can turn out. That is the whole design of the board rather than a detail
    /// of it: asking for rifles would be asking for the one gun nobody makes, and asking for beer would
    /// be asking somebody to buy at ten and sell at eight, which is not a job, it is a fine. What is
    /// left is exactly the set with a bench behind it, so filling the board is production work and the
    /// margin belongs to whoever did the producing.
    /// </summary>
    public IReadOnlyList<string> Askable()
    {
        var goods = new List<string>();
        foreach (var tier in _options.Weapons.Where(x => x.CanForge).OrderBy(x => x.Price))
            goods.Add(tier.Key);
        foreach (var makeable in _options.Makeables.Where(x => x.CanMake))
            goods.Add(makeable.Key);
        return goods;
    }

    /// <summary>What one of a good costs to make, which is the floor under what the trader will pay.</summary>
    private long MaterialsFor(string good)
        => _options.WeaponTier(good)?.ForgeCost
           ?? _options.Makeables.FirstOrDefault(x => x.Key == good)?.MaterialCost
           ?? 0;

    /// <summary>
    /// The board in a town, topped up first.
    ///
    /// Expired orders are left in the table rather than deleted, like contracts: a player who missed one
    /// should be able to see that they missed it, and the read filters by date anyway.
    /// </summary>
    public async Task<IReadOnlyList<WantedOrder>> BoardAsync(string city, DateTime nowUtc, CancellationToken ct)
    {
        var config = _options.Store.Wanted;
        var open = await db.WantedOrders
            .Where(x => x.City == city && x.FilledAtUtc == null && x.ExpiresAtUtc > nowUtc)
            .OrderBy(x => x.ExpiresAtUtc)
            .ToListAsync(ct);

        var wanted = Math.Max(0, config.OpenPerCity);
        if (open.Count >= wanted)
            return open;

        var askable = Askable();
        if (askable.Count == 0)
            return open;

        var lastPosted = await db.WantedOrders
            .Where(x => x.City == city)
            .OrderByDescending(x => x.PostedAtUtc)
            .Select(x => (DateTime?)x.PostedAtUtc)
            .FirstOrDefaultAsync(ct);
        if (lastPosted is { } posted && posted.AddMinutes(Math.Max(0, config.PostIntervalMinutes)) > nowUtc)
            return open;

        // One at a time once the board has been running, so a stripped board recovers at the trader's
        // pace and whoever cleared it actually took something. A town nobody has visited fills on the
        // first look, which is what makes a counter feel like it was there before you were.
        var toPost = lastPosted is null ? wanted - open.Count : 1;
        for (var i = 0; i < toPost; i++)
        {
            var order = Compose(city, askable, nowUtc);
            db.WantedOrders.Add(order);
            open.Add(order);
        }

        await db.SaveChangesAsync(ct);
        return open.OrderBy(x => x.ExpiresAtUtc).ToList();
    }

    /// <summary>
    /// Hands over as much of an order as the player can manage.
    ///
    /// Cash arrives per instalment and the standing arrives whole at the end, which is the same shape a
    /// contract's premium has and it is there for the same reason: goods handed over are paid for, so
    /// stopping half way leaves nobody out of pocket, and the thing worth finishing for cannot be
    /// farmed a unit at a time.
    /// </summary>
    /// <param name="quantity">
    /// How much to hand over, or null for as much as will go. More than is held is refused rather than
    /// quietly reduced, because a player who typed a number meant it.
    /// </param>
    public WantedFill Deliver(WantedOrder order, Player player, DateTime nowUtc, int? quantity = null)
    {
        TravelGate.EnsureLanded(player);

        var trader = StoreTrader.For(order.City, _options);
        if (!order.IsOpen(nowUtc))
            throw new GameRuleException("That one is gone. Somebody else got there, or the trader gave up on it.");
        if (!string.Equals(order.City, player.City, StringComparison.OrdinalIgnoreCase))
            throw new GameRuleException($"{trader.Name} is in {order.City}. You have to be there.");
        if (!order.CanBeWorkedBy(player.Id))
            throw new GameRuleException("Somebody else is already filling that one.");

        var label = TradeGoods.Label(order.Good).ToLowerInvariant();
        var held = TradeGoods.Held(player, order.Good);
        if (held <= 0)
            throw new GameRuleException($"You have no {label} to hand over.");

        var handing = quantity ?? Math.Min(held, order.Remaining);
        if (handing <= 0)
            throw new GameRuleException("Say how much you are handing over.");
        if (handing > held)
            throw new GameRuleException($"You are handing over {handing:N0} {label} and you have {held:N0}.");
        if (handing > order.Remaining)
            throw new GameRuleException($"They only still want {order.Remaining:N0} {label}.");

        TradeGoods.Add(player, order.Good, -handing);
        order.DeliveredQuantity += handing;
        order.ClaimedById ??= player.Id;

        var paid = handing * order.PricePerUnit;
        player.Cash += paid;

        var completed = order.Remaining == 0;
        var repBefore = player.StoreRep;
        var climbed = false;
        if (completed)
        {
            order.FilledById = player.Id;
            order.FilledBy = player;
            order.FilledAtUtc = nowUtc;
            player.StoreRep = Math.Max(0, player.StoreRep + Math.Max(0, order.Rep));
            climbed = (_options.Store.LevelFor(player.StoreRep)?.Level ?? 1)
                      > (_options.Store.LevelFor(repBefore)?.Level ?? 1);
        }

        var summary = completed
            ? $"Filled {trader.Name}'s order: {order.Quantity:N0} {label} for {order.Payout:C0} and {order.Rep:N0} rep."
              + (climbed ? $" They call you {_options.Store.LevelFor(player.StoreRep)!.Name} now." : string.Empty)
            : $"Ran {handing:N0} {label} to {trader.Name} for {paid:C0}. "
              + $"{order.Remaining:N0} to go, and {order.Rep:N0} rep waiting on the last of it.";

        return new WantedFill(order, paid, handing, completed, completed ? order.Rep : 0, summary);
    }

    /// <summary>
    /// One order, priced over the shelf rather than under it.
    ///
    /// Which is the opposite of how a shop buys, and deliberate. The board used to pay somewhere between
    /// materials and the shelf price, so the only people who could fill an order at a profit were the
    /// ones whose bench was already deep enough to make the thing - everybody else read a list they
    /// could never touch, and a new player's view of the shop's own board was a wall.
    ///
    /// A premium fixes that without giving anything away. Walking to the counter, buying the twenty
    /// shotguns and carrying them back pays: barely, on purpose, because the point of doing it that way
    /// is the standing rather than the money. Making them instead pays several times over, and that gap
    /// is now the whole argument for owning a workshop rather than a rule stopping anybody else from
    /// playing.
    ///
    /// The loop that would normally worry somebody - buy at the shop, sell to the shop, repeat - is shut
    /// by the board rather than by the price. Three orders a town, a fixed quantity each, one more every
    /// seventy minutes: what a player can extract is capped no matter where the goods came from.
    /// </summary>
    private WantedOrder Compose(string city, IReadOnlyList<string> askable, DateTime nowUtc)
    {
        var config = _options.Store.Wanted;
        var good = askable[random.NextInclusive(0, askable.Count - 1)];
        var shelf = Math.Max(1, TradeGoods.ReferencePrice(_options, good, city));
        var premium = 1 + Math.Max(0, config.MinPremiumPercent) / 100.0
                        + random.NextInclusive(0, Math.Max(0, config.PremiumSpreadPercent)) / 100.0;
        // Strictly over the shelf whatever the tuning says, so an order can never be one a player loses
        // money finishing after they have already handed the goods over.
        var pay = Math.Max(shelf + 1, (long)Math.Round(shelf * premium));

        var quantity = random.NextInclusive(Math.Max(1, config.MinQuantity), Math.Max(1, config.MaxQuantity));
        var payout = pay * quantity;

        return new WantedOrder
        {
            City = city,
            Good = good,
            Quantity = quantity,
            PricePerUnit = pay,
            ShopPricePerUnit = shelf,
            Rep = Math.Clamp(
                (int)Math.Round(payout * Math.Max(0, config.RepPerDollar)),
                config.MinRep,
                Math.Max(config.MinRep, config.MaxRep)),
            PostedAtUtc = nowUtc,
            ExpiresAtUtc = nowUtc.AddHours(random.NextInclusive(
                Math.Max(1, config.MinHours),
                Math.Max(1, config.MaxHours)))
        };
    }
}

/// <param name="RepEarned">Nothing until the last unit goes in, then the whole of it at once.</param>
public sealed record WantedFill(
    WantedOrder Order,
    long Paid,
    int Delivered,
    bool Completed,
    int RepEarned,
    string Summary);
