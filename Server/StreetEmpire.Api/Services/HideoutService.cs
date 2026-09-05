using Microsoft.Extensions.Options;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

/// <summary>
/// Owns hideout capacity. Crew comes from the tier, goods from the storage room, cash on hand from
/// the safe, and production yield from the labs.
/// </summary>
public sealed class HideoutService(IOptionsSnapshot<GameOptions> options)
{
    private readonly GameOptions _options = options.Value;

    /// <summary>
    /// Lands a finished tier build. Called wherever a player is refreshed, so the new caps appear the
    /// first time they look rather than waiting for them to take an action.
    /// </summary>
    public bool CompleteBuild(Hideout? hideout, DateTime nowUtc)
    {
        if (hideout?.UpgradingToTier is not { } tier || hideout.UpgradeCompletesAtUtc is not { } due)
            return false;
        if (nowUtc < due)
            return false;

        hideout.Tier = tier;
        hideout.UpgradingToTier = null;
        hideout.UpgradeCompletesAtUtc = null;
        return true;
    }

    /// <summary>
    /// Banks what the labs made while nobody was looking. Output stops at the storage cap rather than
    /// spilling, so time away can never destroy stock a player already had, and stops again at the
    /// offline ceiling. Whole hours only: the remainder stays on the clock and is paid next time.
    /// </summary>
    public LabYield AccrueLabs(Player player, DateTime nowUtc)
    {
        var hideout = player.Hideout;
        // A still counts as a reason to run the clock even though it makes nothing passively: the
        // hours it reports are what the contraband risk is rolled over, and a brewer with no lab would
        // otherwise never be at risk at all.
        //
        // Read off what was built rather than off what is standing, which matters now that a room can
        // be down. A house whose labs have all been wrecked still runs this clock and still banks
        // nothing from it, so the hours a raid cost are hours that are genuinely gone. Gating the
        // clock on working rooms would hold the whole outage in credit and pay it out the moment the
        // repair landed, which is a raid that costs its victim nothing at all.
        if (hideout is null || (hideout.WeedLabLevel <= 0 && hideout.CokeLabLevel <= 0 && hideout.WorkshopLevel <= 0))
            return LabYield.None;

        // A lab built just now starts its clock now, so building one never pays out for the hours
        // before it existed.
        if (hideout.LabsCollectedAtUtc is not { } since)
        {
            hideout.LabsCollectedAtUtc = nowUtc;
            return LabYield.None with { ClockMoved = true };
        }

        var hours = (int)Math.Floor((nowUtc - since).TotalHours);
        if (hours <= 0)
            return LabYield.None;

        var chargedHours = Math.Min(hours, Math.Max(0, _options.Hideout.MaxOfflineProductionHours));
        // Advance by every elapsed hour, not just the charged ones. Otherwise a player who stays away
        // for a week banks the ceiling and still has six days of credit waiting behind it.
        hideout.LabsCollectedAtUtc = since.AddHours(hours);
        if (chargedHours <= 0)
            return LabYield.None with { ClockMoved = true };

        var capacity = CapacityFor(hideout);
        // A lab that has been switched off makes nothing, and the hours it was off are gone rather
        // than owed. Held in credit they would pay out in a lump the moment it came back on, which
        // would make switching off free and the switch pointless.
        var weedMade = hideout.WeedLabRunning
            ? PassivePerHour(hideout, "weed") * chargedHours
            : 0;
        var cokeMade = hideout.CokeLabRunning
            ? PassivePerHour(hideout, "coke") * chargedHours
            : 0;

        // Sold before the store is consulted, so a full shelf is no reason for a selling lab to stop:
        // that is most of what the upgrade buys. What is shelved is still capped by the room.
        //
        // Priced at the house's own town rather than at whichever one the player is standing in. The
        // labs are in that town, the buyer is in that town, and nobody in the fiction is flying a
        // week's output out to wherever the boss happens to be having dinner. It is also the rule that
        // stops travel from being an economic lever: a player could otherwise fly to the dearest
        // market on the board and leave their labs selling into it from a thousand miles away.
        var weedSold = SellsItsOwn(hideout, "weed") ? weedMade : 0;
        var cokeSold = SellsItsOwn(hideout, "coke") ? cokeMade : 0;
        var earned = (long)weedSold * ProductPrice(hideout.City, "weed")
                     + (long)cokeSold * ProductPrice(hideout.City, "coke");
        // Into the safe rather than into the player's pocket, because the player may not be there and
        // money does not fly. Whatever the safe cannot hold goes to the bank rather than evaporating.
        IntoSafe(player, earned);

        var weed = Produce(player.Weed, capacity.MaxWeed, weedMade - weedSold);
        var coke = Produce(player.Coke, capacity.MaxCoke, cokeMade - cokeSold);
        player.Weed += weed;
        // Fresh off the bench and uncut, like anything else a lab makes.
        player.AddCoke(coke, 1);

        return new LabYield(weed, coke, weedSold, cokeSold, earned, chargedHours, hours > chargedHours, true);
    }

    /// <summary>
    /// Total attention on a player: what they are sitting on, plus what they have earned working.
    ///
    /// Weighted per good rather than flat, because everything here is illegal and so being illegal
    /// distinguishes nothing. What differs is how much notice a thing draws.
    /// </summary>
    public double HeatFor(Player player)
    {
        return EarnedHeatFor(player) + HeldGoodsHeatFor(player) + CrewHeatFor(player);
    }

    public double EarnedHeatFor(Player player)
        => Math.Max(0, player.Heat);

    /// <summary>
    /// Attention drawn by contraband, from both piles at once and each in its own town.
    ///
    /// Two multipliers rather than one because the two piles are in two places. What is on the shelves
    /// draws notice where the house is; what is in the player's pockets draws it wherever they are
    /// standing. A player who empties their store into a bag and flies somewhere quiet genuinely has
    /// cooled their house down, and is now the most interesting person at the airport - which is the
    /// trade this is meant to offer.
    /// </summary>
    public double HeldGoodsHeatFor(Player player)
        => CarriedGoodsHeatFor(player) + StoredGoodsHeatFor(player);

    /// <summary>What the player is walking around with, at the rate of the town they are walking in.</summary>
    public double CarriedGoodsHeatFor(Player player)
        => GoodsHeat(player.Carried) * _options.CityMarkets.HeatMultiplier(player.City);

    /// <summary>What is on the shelves, at the rate of the town the shelves are in.</summary>
    public double StoredGoodsHeatFor(Player player)
        => GoodsHeat(player.Stored) * _options.CityMarkets.HeatMultiplier(HomeCity(player));

    private double GoodsHeat(IStash stash)
    {
        var config = _options.Hideout;
        return stash.Coke * config.CokeHeatPerUnit
               + stash.Moonshine * config.MoonshineHeatPerUnit
               + stash.Weed * config.WeedHeatPerUnit
               + stash.Cut * config.CutHeatPerUnit;
    }

    /// <summary>
    /// Attention drawn by the people on the payroll, at the rate of the town they are in - which is
    /// the hideout's, because crew do not get on the plane with the player.
    /// </summary>
    public double CrewHeatFor(Player player)
    {
        var config = _options.Hideout;
        return HomeHeatMultiplier(player) * (player.Pimps * config.PimpHeat
               + player.Hoes * config.HoeHeat
               + player.Thugs * config.ThugHeat);
    }

    /// <summary>The heat rate of the town the operation lives in, which is not necessarily the player's.</summary>
    private double HomeHeatMultiplier(Player player)
        => _options.CityMarkets.HeatMultiplier(HomeCity(player));

    /// <summary>
    /// The town the operation is run from. The hideout's, falling back to the player's for anybody who
    /// has not got one yet - a house that does not exist is wherever its owner is standing.
    /// </summary>
    public static string HomeCity(Player player) => player.Hideout?.City ?? player.City;

    /// <summary>
    /// Whether the player is standing at their own front door.
    ///
    /// The question behind every physical hideout action: opening the safe, moving stock on or off a
    /// shelf, paying a builder, taking a gun off the rack. Somebody with no hideout at all is treated
    /// as home, because there is no door for them to be away from and refusing them would lock a brand
    /// new player out of their own first purchase.
    /// </summary>
    public static bool IsAtHideout(Player player)
        => player.Hideout is not { } hideout
           || string.Equals(player.City, hideout.City, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Cools earned heat and then rolls for the law turning up, once per elapsed hour.
    ///
    /// Rolled on the clock rather than on an action so it costs a player who stockpiles whether or not
    /// they are at the screen, and costs one who sells and lays low almost nothing. A bust takes a
    /// share of every contraband pile and fines them, and the fine stops at cash on hand: one that
    /// could push a player into debt is a different and much nastier mechanic than losing the stash.
    ///
    /// What band the heat was in when they came through decides the rest: how much of the stash goes,
    /// and whether the place is left standing. Under Watched they take stock and go, which is the bill
    /// for holding it. At Watched and above they take the house apart on the way out, and that is the
    /// part still costing the player tomorrow - the labs are dark, the bench is cold, nobody is on the
    /// corner watching for the next one, and the mules are going nowhere until it is paid for.
    /// </summary>
    public ContrabandBust RollBust(Player player, int hours, IGameRandom random, DateTime nowUtc)
    {
        if (hours <= 0)
            return ContrabandBust.None;

        var config = _options.Hideout;
        player.Heat = Math.Max(0, player.Heat - Math.Max(0, config.HeatDecayPerHour) * hours);

        var heat = HeatFor(player);
        if (heat <= config.HeatBustFloor)
            return ContrabandBust.None;

        var chance = Math.Clamp((heat - config.HeatBustFloor) * config.BustChancePerHeat, 0, Math.Clamp(config.MaxBustChancePerHour, 0, 1));
        // Someone watching the street. It never takes the risk away, or holding would be free.
        chance *= 1 - BustRiskReduction(player.Hideout);
        var caught = false;
        for (var hour = 0; hour < hours && !caught; hour++)
            caught = random.NextDouble() < chance;
        if (!caught)
            return ContrabandBust.None;

        // Read before anything is taken. Seizing first would drop the held-goods half of the number
        // and quietly demote a Hunted house to a Watched one mid-raid, so the stash somebody was
        // caught with would decide how hard they were hit only until it was carried out of the door.
        var band = HeatBands.Of(heat, config);
        // One roll for the whole raid: the band decides how prepared they came through the door, and
        // this decides how much of the house they actually turned over once they were in.
        var share = HeatBands.SeizedPercent(band, config, random.NextDouble());

        // A raid is on the house. It takes what is on the shelves and what is in the safe, and it can
        // only take what the player is carrying if the player is there to be caught with it - which is
        // the other half of the rule that stops a hideout raid reaching into another town's pockets.
        // Somebody who flew out this morning with the coke in a bag keeps the coke and loses whatever
        // they left behind, and that is a decision they made rather than a loophole.
        var caughtHere = IsAtHideout(player);
        int Take(string good)
            => Seize(player.Stored, good, share)
               + (caughtHere ? Seize(player.Carried, good, share) : 0);

        var weed = Take("weed");
        var coke = Take("coke");
        var moonshine = Take("moonshine");
        var cut = Take("cut");

        var units = weed + coke + moonshine + cut;
        // The safe pays first and the player's pockets only if they are standing in front of it. A
        // fine that could reach into another town would be the same reach the seizure is denied.
        var fine = FromSafeThenPocket(player, (long)Math.Round(units * Math.Max(0, config.FinePerSeizedUnit)), caughtHere);
        var wrecked = Wreck(player.Hideout, HeatBands.RoomsWrecked(band, config), random, nowUtc);
        // A raid resets the attention it was drawn by. Leaving it high would mean one bust guarantees
        // the next, which is a spiral rather than a risk.
        player.Heat = 0;

        return new ContrabandBust(weed, coke, moonshine, cut, fine, Math.Round(heat, 1), HeatBands.Label(band), wrecked);
    }

    /// <summary>
    /// Puts rooms out of action and says which ones went.
    ///
    /// Picked at random from what is actually standing, and never more than there is to break. Which
    /// room goes is not a decision anybody in the fiction makes carefully - the law goes through the
    /// door it is nearest to, and a raiding crew has minutes rather than a survey - and a weighting
    /// table here would only be the designer picking the same room every time with extra steps. The
    /// decision this creates is the one that comes after: three dark rooms and one repair crew.
    ///
    /// A room already down cannot be broken again, which is what stops a second raid from silently
    /// resetting a repair that is halfway through.
    /// </summary>
    public IReadOnlyList<string> Wreck(Hideout? hideout, int count, IGameRandom random, DateTime nowUtc)
    {
        if (hideout is null || count <= 0)
            return [];

        var standing = HideoutRooms.Breakable
            .Where(room => hideout.BuiltLevel(room) > 0 && !hideout.IsWrecked(room))
            .ToList();

        var broken = new List<string>();
        while (broken.Count < count && standing.Count > 0)
        {
            var pick = standing[random.NextInclusive(0, standing.Count - 1)];
            standing.Remove(pick);
            hideout.SetWrecked(pick, nowUtc);
            broken.Add(pick);
        }

        return broken;
    }

    /// <summary>Takes a share of one pile, through the same mapping every other mover of goods uses.</summary>
    private static int Seize(IStash stash, string good, double share)
    {
        var held = TradeGoods.Held(stash, good);
        if (held <= 0) return 0;
        var taken = Math.Min(held, Math.Max(1, (int)Math.Round(held * share)));
        TradeGoods.Add(stash, good, -taken);
        return taken;
    }

    /// <summary>
    /// Charges a bill against the safe first and then the money in the player's hand, and says what
    /// was actually paid. Never past what is there: a fine that could push somebody into debt is a
    /// much nastier mechanic than losing the stash, and it is not the one being built.
    /// </summary>
    private static long FromSafeThenPocket(Player player, long amount, bool reachPocket)
    {
        var owed = Math.Max(0, amount);
        var paid = 0L;
        if (player.Hideout is { } hideout && hideout.SafeCash > 0)
        {
            var fromSafe = Math.Min(hideout.SafeCash, owed);
            hideout.SafeCash -= fromSafe;
            paid += fromSafe;
            owed -= fromSafe;
        }

        if (reachPocket && owed > 0)
        {
            var fromHand = Math.Min(player.Cash, owed);
            player.Cash -= fromHand;
            paid += fromHand;
        }

        return paid;
    }

    /// <summary>
    /// Puts money in the safe, up to what the room holds, and banks whatever will not fit.
    ///
    /// Everything the house earns on its own comes through here, because the player may be a thousand
    /// miles away when it does and cash cannot fly to them. Overflow goes to the bank rather than
    /// being lost: a full safe is a reason to buy a bigger one, not a reason for a night's production
    /// to disappear.
    /// </summary>
    public long IntoSafe(Player player, long amount)
    {
        if (amount <= 0) return 0;
        if (player.Hideout is not { } hideout)
        {
            player.BankCash += amount;
            return 0;
        }

        var stored = Math.Min(Math.Max(0, CapacityFor(hideout).MaxCash - hideout.SafeCash), amount);
        hideout.SafeCash += stored;
        player.BankCash += amount - stored;
        return stored;
    }

    /// <summary>
    /// A making station's level, or null when none is built. One lookup for all three because they are
    /// the same shape: turns and materials in, one good out.
    /// </summary>
    public WorkshopLevelOptions? WorkshopFor(Hideout? hideout)
    {
        var level = hideout?.WorkingLevel(HideoutRooms.Workshop) ?? 0;
        return level <= 0 ? null : Level(_options.Hideout.Workshop, level, x => x.Level);
    }

    /// <summary>
    /// The share of an hour's raid chance a lookout takes off. Capped below one on purpose: a room
    /// that made a stash safe would end the decision heat exists to create.
    /// </summary>
    public double BustRiskReduction(Hideout? hideout)
    {
        var level = hideout?.WorkingLevel(HideoutRooms.Lookout) ?? 0;
        if (level <= 0) return 0;
        var percent = Level(_options.Hideout.Lookout, level, x => x.Level)?.BustChanceReductionPercent ?? 0;
        return Math.Clamp(percent / 100.0, 0, 0.85);
    }

    /// <summary>
    /// How many mule runs may be in the air at once. Zero without the room, which is what makes the
    /// intelligence centre the gate on mule running rather than a discount on it.
    /// </summary>
    public int ConcurrentRunCap(Hideout? hideout)
    {
        var level = hideout?.WorkingLevel(HideoutRooms.Intelligence) ?? 0;
        if (level <= 0) return Math.Max(0, _options.Mules.BaseConcurrentRuns);
        return Level(_options.Hideout.Intelligence, level, x => x.Level)?.ConcurrentRuns
               ?? Math.Max(0, _options.Mules.BaseConcurrentRuns);
    }

    /// <summary>
    /// The share of a route's risk that knowing the route takes off. Never all of it: a briefing is
    /// not a guarantee, and a room that removed risk entirely would end the decision it exists for.
    /// </summary>
    public double RouteRiskReduction(Hideout? hideout)
    {
        var level = hideout?.WorkingLevel(HideoutRooms.Intelligence) ?? 0;
        if (level <= 0) return 0;
        var percent = Level(_options.Hideout.Intelligence, level, x => x.Level)?.RiskReductionPercent ?? 0;
        return Math.Clamp(percent / 100.0, 0, 0.9);
    }

    /// <summary>
    /// The tier a station needs, or null when it has no gate. Checked when making as well as when
    /// building: buying is not the only way to end up with one, since a station built before a gate
    /// existed would otherwise keep running under it forever.
    /// </summary>
    public int? WorkshopRequiredTier()
    {
        var first = _options.Hideout.Workshop.OrderBy(x => x.Level).FirstOrDefault();
        return first is null || first.MinTier <= 1 ? null : first.MinTier;
    }

    /// <summary>What a lab makes per hour on its own, before storage limits.</summary>
    /// <summary>
    /// Whether this lab is set to move its own output, and is big enough to be allowed to.
    ///
    /// Both halves every time it is asked, rather than trusting the switch on its own: a player who
    /// set it at level three and was then knocked back down - a season roll, a lab sold - would
    /// otherwise still be selling from a cupboard.
    /// </summary>
    public bool SellsItsOwn(Hideout? hideout, string product)
    {
        if (hideout is null) return false;
        var (on, level) = product == "coke"
            ? (hideout.CokeLabAutoSell, hideout.CokeLabLevel)
            : (hideout.WeedLabAutoSell, hideout.WeedLabLevel);
        return on && level >= Math.Max(1, _options.Hideout.MinLabLevelForAutoSell);
    }

    /// <summary>
    /// What a unit fetches in this town. The same figure the counter quotes, read from the same place,
    /// so a lab never sells at a price nobody could have got standing there themselves.
    /// </summary>
    private int ProductPrice(string? city, string product)
        => product == "coke"
            ? _options.CityMarkets.ProductPrice(city, "coke", _options.CokeSellPrice)
            : _options.CityMarkets.ProductPrice(city, "weed", _options.WeedSellPrice);

    public int PassivePerHour(Hideout? hideout, string product)
    {
        var (levels, level) = EffectiveLabFor(hideout, product);
        return level <= 0 ? 0 : Level(levels, level, x => x.Level)?.PassivePerHour ?? 0;
    }


    /// <summary>
    /// Whether the store rather than the building is what is holding a role down.
    ///
    /// Worth knowing because the two are fixed by completely different purchases, and a player told
    /// the wrong one will buy the wrong thing: moving to a bigger house to raise a cap the storage
    /// room is setting is an expensive way to change nothing.
    /// </summary>
    public bool StoreCapsCrew(Hideout? hideout, string role)
    {
        var config = _options.Hideout;
        var tier = Level(config.Tiers, hideout?.Tier ?? 1, x => x.Level);
        var storage = Level(config.Storage, hideout?.StorageLevel ?? 1, x => x.Level);
        if (tier is null || storage is null) return false;

        return role switch
        {
            "hoes" => Supported(storage.Condoms, _options.Morale.TurnsPerCondom) < tier.MaxHoes,
            "thugs" => Supported(storage.Beer, _options.Morale.TurnsPerBeer) < tier.MaxThugs,
            // Nothing supplies a pimp, so the building is the only thing that can be the limit.
            _ => false
        };
    }

    /// <summary>
    /// How large a crew a shelf of supplies covers for one full-length action. The exact inverse of
    /// the upkeep the action charges, floored rather than rounded, because a crew the store can only
    /// three-quarters feed is a crew the store cannot feed.
    /// </summary>
    private int Supported(int shelf, double turnsPerSupply)
    {
        if (turnsPerSupply <= 0) return int.MaxValue;
        var turns = Math.Max(1, _options.MaxActionTurns);
        return (int)Math.Floor(shelf * turnsPerSupply / turns);
    }

    public HideoutCapacity CapacityFor(Hideout? hideout)
    {
        var config = _options.Hideout;
        var tier = Level(config.Tiers, hideout?.Tier ?? 1, x => x.Level)
            ?? new HideoutTierOptions { MaxPimps = int.MaxValue, MaxHoes = int.MaxValue, MaxThugs = int.MaxValue, MaxRides = int.MaxValue };
        var storage = Level(config.Storage, hideout?.StorageLevel ?? 1, x => x.Level)
            ?? new StorageLevelOptions { Condoms = int.MaxValue, Beer = int.MaxValue, Weapons = int.MaxValue, Weed = int.MaxValue, Coke = int.MaxValue, Medicine = int.MaxValue };
        var safe = Level(config.Safe, hideout?.SafeLevel ?? 1, x => x.Level)
            ?? new SafeLevelOptions { MaxCash = long.MaxValue };

        // A crew is capped by whichever runs out first: the room the building has for them, or the
        // supplies the store can put behind them for a full action. Hiring past the store was the
        // game handing a player fifty hoes and four turns of condoms, then charging them morale for
        // the shortfall every shift - a punishment for taking the hideout page at its word.
        //
        // Pimps are not on this list because nothing supplies a pimp. They eat no condoms and drink
        // no beer, so the building is the only thing that can run out of room for them.
        var maxHoes = Math.Min(tier.MaxHoes, Supported(storage.Condoms, _options.Morale.TurnsPerCondom));
        var maxThugs = Math.Min(tier.MaxThugs, Supported(storage.Beer, _options.Morale.TurnsPerBeer));

        return new HideoutCapacity(
            tier.Name,
            hideout?.Tier ?? 1,
            hideout?.StorageLevel ?? 1,
            hideout?.SafeLevel ?? 1,
            hideout?.WeedLabLevel ?? 0,
            hideout?.CokeLabLevel ?? 0,
            tier.MaxPimps,
            maxHoes,
            maxThugs,
            tier.MaxRides,
            safe.MaxCash,
            storage.Condoms,
            storage.Beer,
            storage.Weapons,
            storage.Weed,
            storage.Coke,
            storage.Moonshine,
            storage.Cut,
            storage.Medicine,
            storage.Poison);
    }

    /// <summary>
    /// Extra production units per turn, as a percentage, from the lab for this product.
    /// </summary>
    public int ProductionYieldBonusPercent(Hideout? hideout, string product)
    {
        var (levels, level) = EffectiveLabFor(hideout, product);
        return level <= 0 ? 0 : Level(levels, level, x => x.Level)?.YieldBonusPercent ?? 0;
    }

    private (List<LabLevelOptions> Levels, int Level) EffectiveLabFor(Hideout? hideout, string product)
    {
        var (levels, level) = LabFor(hideout, product);
        if (level <= 0)
            return (levels, 0);

        // A lab level upgrades the workshop level below it: level 1 can stand alone, level 2 needs a
        // level 1 workshop, and so on. Existing saves keep their purchased lab level, but output waits
        // for the bench that can actually support it.
        //
        // The bench that has to be standing, not the one that was paid for: a wrecked workshop drags
        // every lab above the first rung down with it, which is the knock-on that makes the bench the
        // room a raider actually wants and the one a player fixes first.
        var workshopReach = Math.Max(1, (hideout?.WorkingLevel(HideoutRooms.Workshop) ?? 0) + 1);
        return (levels, Math.Min(level, workshopReach));
    }

    /// <summary>
    /// The lab table and the level it is running at, which is nothing while the lab is down. Every
    /// yield and every passive hour is read through here, so one wrecked room silences all of them.
    /// </summary>
    private (List<LabLevelOptions> Levels, int Level) LabFor(Hideout? hideout, string product)
        => product == "coke"
            ? (_options.Hideout.CokeLab, hideout?.WorkingLevel(HideoutRooms.CokeLab) ?? 0)
            : (_options.Hideout.WeedLab, hideout?.WorkingLevel(HideoutRooms.WeedLab) ?? 0);

    private static int RequiredWorkshopForLab(int labLevel) => Math.Max(0, labLevel - 1);

    /// <summary>How much of a passive run actually fits, never taking away what is already held.</summary>
    private static int Produce(int held, int cap, int produced)
        => Math.Max(0, Math.Min(produced, cap - held));

    /// <summary>
    /// How many more of a crew role the hideout has room for. Zero once the cap is reached, and zero
    /// for grandfathered players who are already over it.
    /// </summary>
    public int CrewRoom(Player player, string role)
    {
        var capacity = CapacityFor(player.Hideout);
        return role switch
        {
            "pimps" => Math.Max(0, capacity.MaxPimps - player.Pimps),
            "hoes" => Math.Max(0, capacity.MaxHoes - player.Hoes),
            "thugs" => Math.Max(0, capacity.MaxThugs - player.Thugs),
            _ => 0
        };
    }

    /// <summary>
    /// Spaces left in the garage. Read by the chop shop before a purchase and by a jacking before it
    /// drives anything away, so a ride can never arrive somewhere with nowhere to put it.
    /// </summary>
    public int RideRoom(Player player)
        => Math.Max(0, CapacityFor(player.Hideout).MaxRides - player.Rides);

    /// <summary>
    /// What this player can physically carry, before anything is on them.
    ///
    /// The single place every carry modifier the design wants later has to land - bags, a car, an
    /// escort, a skill, a perk, a bigger house. Only the tier bonus exists today and it defaults to
    /// nothing, so this is currently the configured table read straight through; the point of the
    /// method is that adding the next one changes this function and nothing else.
    /// </summary>
    public CarryCapacity CarryCapacityFor(Player player)
    {
        var carry = _options.Carry;
        if (!carry.Enforce)
            return CarryCapacity.Unlimited;

        var tiersAbove = Math.Max(0, (player.Hideout?.Tier ?? 1) - 1);
        var scale = 1 + Math.Max(0, carry.PerTierBonusPercent) / 100.0 * tiersAbove;
        int Room(int slots) => slots <= 0 ? 0 : Math.Max(1, (int)Math.Round(slots * scale));

        return new CarryCapacity(
            true,
            Room(carry.Condoms),
            Room(carry.Beer),
            Room(carry.Weapons),
            Room(carry.Weed),
            Room(carry.Coke),
            Room(carry.Moonshine),
            Room(carry.Cut),
            Room(carry.Medicine),
            Room(carry.Poison));
    }

    /// <summary>
    /// Settles a finished action against what the player can carry, putting the surplus on the shelves
    /// and losing whatever will not fit there either.
    ///
    /// Two ceilings now instead of one, because a shift ends with the takings in somebody's hands and
    /// a store room down the hall. What a player walked in holding is never taken off them, so
    /// grandfathered amounts survive and drain down through upkeep, exactly as before.
    ///
    /// Cash is not on this list any more. It used to be capped by the safe, back when cash on hand and
    /// the safe were the same pile; now that the safe is a place with a door, walking around with a
    /// fortune is allowed and is meant to be a bad idea rather than an impossible one.
    /// </summary>
    public StorageOverflow Settle(Player player, StockLevels before)
    {
        var storage = CapacityFor(player.Hideout);
        var carry = CarryCapacityFor(player);
        var bag = player.Carried;
        // A shelf a thousand miles away catches nothing that falls out of a pocket in another town.
        // Every action that settles requires being at the house today, so this is a guard rather than
        // a branch anybody reaches - but the day one of them does not, the surplus has to go into the
        // gutter rather than teleport home.
        var canShelve = IsAtHideout(player);

        var shelved = 0;
        var lost = new Dictionary<string, int>();
        foreach (var key in Spillable)
        {
            // The shelves first, exactly as they always were: what a shift or a haul leaves over the
            // storage room is gone. What a player walked in holding is never taken off them.
            var onShelf = key == Rack ? player.Weapons : TradeGoods.Held(player, key);
            var overShelf = Spill(onShelf, TradeGoods.Capacity(storage, key), before.Of(key));
            if (overShelf > 0)
            {
                if (key == Rack) player.RemoveWeapons(overShelf);
                else TradeGoods.Add(player, key, -overShelf);
                lost[key] = overShelf;
            }

            // Then the bag, which spills on to the shelves before it spills into the street.
            var inBag = key == Rack ? bag.Weapons : TradeGoods.Held(bag, key);
            var overBag = Spill(inBag, carry.Of(key), 0);
            if (overBag <= 0) continue;

            var moved = 0;
            if (canShelve)
                moved = key == Rack
                    ? ShelveWeapons(bag, player, overBag, storage.MaxWeapons)
                    : TradeGoods.Move(bag, player, key, overBag, TradeGoods.Room(player, storage, key));

            shelved += moved;
            var dropped = overBag - moved;
            if (dropped <= 0) continue;
            if (key == Rack) bag.RemoveWeapons(dropped);
            else TradeGoods.Add(bag, key, -dropped);
            lost[key] = Lost(key) + dropped;
        }

        int Lost(string key) => lost.TryGetValue(key, out var value) ? value : 0;
        return new StorageOverflow(
            Lost("condoms"), Lost("beer"), Lost(Rack), Lost("weed"),
            Lost("coke"), Lost("medicine"), Lost("poison"), Lost("moonshine"), Lost("cut"), shelved);
    }

    /// <summary>
    /// The whole gun rack as one key. Weapons are the odd good out: four columns sharing a single
    /// ceiling, so they are settled and clamped as one pile rather than tier by tier.
    /// </summary>
    private const string Rack = "weapons";

    /// <summary>
    /// The goods a finished action can leave somebody over-loaded with, in the order the overflow
    /// sentence names them.
    /// </summary>
    private static readonly IReadOnlyList<string> Spillable =
        ["condoms", "beer", Rack, "weed", "coke", "medicine", "poison", "moonshine", "cut"];

    /// <summary>
    /// Moves guns from a rack to a shelf, cheapest first, and says how many went. Not a
    /// <see cref="TradeGoods.Move"/> because the rack is four piles under one cap, and moving a count
    /// rather than named tiers is what keeps the good ones on the hip.
    /// </summary>
    private static int ShelveWeapons(IStash from, IStash to, int count, int cap)
    {
        var room = Math.Max(0, cap - to.Weapons);
        var taken = from.RemoveWeapons(Math.Min(count, room));
        foreach (var tier in WeaponTiers.All)
            to.AddWeapons(tier, taken.Of(tier));
        return taken.Total;
    }

    /// <summary>
    /// Hard-clamps a player to capacity with no grandfathering. Used when seeding rivals, who must play
    /// by the same limits as players.
    ///
    /// Both ceilings, because a seeded rival has to be a legal position and not just a plausible one:
    /// pockets down to what a person can carry, shelves down to what the room holds, and cash over the
    /// safe into the bank rather than destroyed.
    /// </summary>
    public void ClampToCapacity(Player player)
    {
        var capacity = CapacityFor(player.Hideout);
        player.Pimps = Math.Min(player.Pimps, capacity.MaxPimps);
        player.Hoes = Math.Min(player.Hoes, capacity.MaxHoes);
        player.Thugs = Math.Min(player.Thugs, capacity.MaxThugs);
        player.Rides = Math.Min(player.Rides, capacity.MaxRides);

        var carry = CarryCapacityFor(player);
        var bag = player.Carried;
        foreach (var key in Spillable)
        {
            if (key == Rack)
            {
                player.RemoveWeapons(Math.Max(0, player.Weapons - capacity.MaxWeapons));
                bag.RemoveWeapons(Math.Max(0, bag.Weapons - carry.MaxWeapons));
                continue;
            }

            TradeGoods.Add(player, key, -Math.Max(0, TradeGoods.Held(player, key) - TradeGoods.Capacity(capacity, key)));
            TradeGoods.Add(bag, key, -Math.Max(0, TradeGoods.Held(bag, key) - carry.Of(key)));
        }

        if (player.Hideout is not { } hideout) return;

        var overSafe = hideout.SafeCash - capacity.MaxCash;
        if (overSafe > 0)
        {
            hideout.SafeCash -= overSafe;
            player.BankCash += overSafe;
        }
    }

    public ActionResultResponse Upgrade(Player player, string? room, DateTime nowUtc)
    {
        var hideout = player.Hideout ?? throw new GameRuleException("Your hideout is not set up yet.");
        // Building is done on site. There is no remote unlock for this one and there should not be:
        // signing off a wall is not a phone call, and a hideout that can be rebuilt from anywhere is a
        // hideout with no location worth having.
        EnsureAtHideout(player, "Building");
        var key = HideoutRooms.Normalize(room);
        var config = _options.Hideout;

        // Nothing gets built on top of a wreck. Allowing it would let a player skip the repair bill by
        // buying the next level instead - the same room, standing again, for a price that has nothing
        // to do with the damage - and it would make being raided a discount for anybody who was going
        // to upgrade that room anyway.
        if (HideoutRooms.CanBreak(key) && hideout.IsWrecked(key))
            throw new GameRuleException(
                $"Your {HideoutRooms.Name(key)} is wrecked. Nobody is building on top of it until it is put back.");

        return key switch
        {
            "tier" => StartTierUpgrade(player, hideout, nowUtc),
            "storage" => ApplyUpgrade(player, hideout, config.Storage, hideout.StorageLevel, x => x.Level, x => x.UpgradeCost, x => x.MinTier,
                level => hideout.StorageLevel = level, "storage room"),
            "safe" => ApplyUpgrade(player, hideout, config.Safe, hideout.SafeLevel, x => x.Level, x => x.UpgradeCost, x => x.MinTier,
                level => hideout.SafeLevel = level, "safe"),
            "weedlab" => ApplyUpgrade(player, hideout, config.WeedLab, hideout.WeedLabLevel, x => x.Level, x => x.UpgradeCost, x => x.MinTier,
                level => BuildLab(hideout, nowUtc, () => hideout.WeedLabLevel = level), "weed lab", RequiredWorkshopForLab),
            "cokelab" => ApplyUpgrade(player, hideout, config.CokeLab, hideout.CokeLabLevel, x => x.Level, x => x.UpgradeCost, x => x.MinTier,
                level => BuildLab(hideout, nowUtc, () => hideout.CokeLabLevel = level), "coke lab", RequiredWorkshopForLab),
            "workshop" => ApplyUpgrade(player, hideout, config.Workshop, hideout.WorkshopLevel, x => x.Level, x => x.UpgradeCost, x => x.MinTier,
                level => hideout.WorkshopLevel = level, "workshop"),
            "intelligence" => ApplyUpgrade(player, hideout, config.Intelligence, hideout.IntelligenceLevel, x => x.Level, x => x.UpgradeCost, x => x.MinTier,
                level => hideout.IntelligenceLevel = level, "intelligence centre"),
            "lookout" => ApplyUpgrade(player, hideout, config.Lookout, hideout.LookoutLevel, x => x.Level, x => x.UpgradeCost, x => x.MinTier,
                level => hideout.LookoutLevel = level, "lookout"),
            _ => throw new GameRuleException(
                "Room must be 'tier', 'storage', 'safe', 'weedlab', 'cokelab', 'workshop', 'still', 'mix', 'intelligence', or 'lookout'.")
        };
    }

    /// <summary>
    /// Pays for the next tier and starts the clock. The cash and turns go now; the caps arrive when the
    /// build finishes, which is what keeps a rich player from buying a bigger crew mid-fight.
    /// </summary>
    private ActionResultResponse StartTierUpgrade(Player player, Hideout hideout, DateTime nowUtc)
    {
        if (hideout.UpgradingToTier is { } pending)
        {
            var name = TierName(pending);
            var minutes = Math.Max(1, (int)Math.Ceiling(((hideout.UpgradeCompletesAtUtc ?? nowUtc) - nowUtc).TotalMinutes));
            throw new GameRuleException($"The {name} is already being built. It is ready in about {minutes} minute(s).");
        }

        var next = Level(_options.Hideout.Tiers, hideout.Tier + 1, x => x.Level)
            ?? throw new GameRuleException("Your hideout is already the biggest there is.");
        if (Capital.Available(player) < next.UpgradeCost)
            throw new GameRuleException($"Moving up to the {next.Name} costs {next.UpgradeCost:C0} across your cash, safe and bank.");
        if (player.Turns < next.UpgradeTurns)
            throw new GameRuleException($"Moving up to the {next.Name} takes {next.UpgradeTurns} turns of work.");

        var fromBank = ChargeCapital(player, next.UpgradeCost);
        player.Turns -= next.UpgradeTurns;
        hideout.UpgradingToTier = next.Level;
        hideout.UpgradeCompletesAtUtc = nowUtc.AddMinutes(next.BuildMinutes);

        return new ActionResultResponse(
            $"Started building the {next.Name} for {next.UpgradeCost:C0} and {next.UpgradeTurns} turns. It is ready in {next.BuildMinutes} minute(s).",
            player.Turns,
            new Dictionary<string, object?>
            {
                ["room"] = "hideout",
                ["tier"] = next.Level,
                ["tierName"] = next.Name,
                ["cost"] = next.UpgradeCost,
                ["turns"] = next.UpgradeTurns,
                ["paidFromBank"] = fromBank,
                ["readyAtUtc"] = hideout.UpgradeCompletesAtUtc,
                ["cashRemaining"] = player.Cash,
                ["bankRemaining"] = player.BankCash
            });
    }

    /// <summary>
    /// What putting a room back costs: a share of every pound that got it to the level it is.
    ///
    /// Priced off the built level rather than a flat fee, so the bill is proportionate to what was
    /// taken away. Somebody whose first-rung lookout went through a window is out pocket money, and
    /// somebody whose maxed coke lab went is out something they will feel - which is correct, because
    /// that is also the difference in what the room was earning them while it stood.
    /// </summary>
    public long RepairCost(Hideout? hideout, string room)
    {
        var level = hideout?.BuiltLevel(room) ?? 0;
        if (level <= 0) return 0;
        var built = HideoutValue.OfRoom(_options.Hideout, room, level);
        return Math.Max(1, (long)Math.Round(built * Math.Clamp(_options.Hideout.RepairCostPercent, 0, 1)));
    }

    /// <summary>How long the crew are in there. Longer for a deeper room, and never instant.</summary>
    public int RepairMinutes(Hideout? hideout, string room)
    {
        var level = hideout?.BuiltLevel(room) ?? 0;
        if (level <= 0) return 0;
        return Math.Max(
            Math.Max(1, _options.Hideout.MinRepairMinutes),
            level * Math.Max(1, _options.Hideout.RepairMinutesPerLevel));
    }

    /// <summary>
    /// Puts a crew into one wrecked room and starts the clock.
    ///
    /// Paid for up front and one room at a time, which is the whole shape of the mechanic: the money
    /// goes now, the room comes back later, and a house with three dark rooms has to decide which one
    /// it wants working tonight. Paid from the bank first like every other hideout bill, for the reason
    /// <see cref="ChargeCapital"/> gives - the safe is one of the things a raid can leave you short of,
    /// and a repair nobody can afford because their own safe is too small is a dead end.
    ///
    /// No turns. A raid is something done to a player rather than something they chose, and charging
    /// the scarcest resource in the game to undo somebody else's decision is where a setback turns into
    /// a reason to stop playing. Cash and hours are the price; the turns stay theirs.
    /// </summary>
    public ActionResultResponse Repair(Player player, string? room, DateTime nowUtc)
    {
        var hideout = player.Hideout ?? throw new GameRuleException("Your hideout is not set up yet.");
        // A repair can be ordered from another town, but only by somebody with a centre big enough to
        // order it: the room that exists to know things is where remote management is bought.
        if (!CanRepairRemotely(hideout))
            EnsureAtHideout(player, _options.Hideout.RemoteRepairLevel > 0
                ? $"Starting a repair from another town needs a level {_options.Hideout.RemoteRepairLevel:N0} intelligence centre, so this"
                : "Starting a repair");
        var key = HideoutRooms.Normalize(room);
        if (!HideoutRooms.CanBreak(key))
            throw new GameRuleException($"Room must be one of {string.Join(", ", HideoutRooms.Breakable)}.");

        // Land a repair that is already due before deciding whether the crew are free. The clock does
        // this too and gets here first on every real request, but only here is it load-bearing: without
        // it, a call that arrived at the exact moment the last one finished would overwrite the room
        // being repaired and leave the finished one wrecked for ever, with the money already spent.
        CompleteRepair(hideout, nowUtc);

        if (hideout.RepairingRoom is { } busy && hideout.RepairCompletesAtUtc is { } due && due > nowUtc)
        {
            var left = Math.Max(1, (int)Math.Ceiling((due - nowUtc).TotalMinutes));
            throw new GameRuleException(
                $"The crew are still in the {HideoutRooms.Name(busy)}. They are out in about {left} minute(s).");
        }

        if (!hideout.IsWrecked(key))
            throw new GameRuleException($"Your {HideoutRooms.Name(key)} is not broken.");

        var cost = RepairCost(hideout, key);
        if (Capital.Available(player) < cost)
            throw new GameRuleException(
                $"Putting the {HideoutRooms.Name(key)} back costs {cost:C0} across your cash, safe and bank. You have {Capital.Available(player):C0}.");

        var fromBank = ChargeCapital(player, cost);
        var minutes = RepairMinutes(hideout, key);
        hideout.RepairingRoom = key;
        hideout.RepairCompletesAtUtc = nowUtc.AddMinutes(minutes);

        return new ActionResultResponse(
            $"Put a crew on the {HideoutRooms.Name(key)} for {cost:C0}. It is working again in {minutes} minute(s).",
            player.Turns,
            new Dictionary<string, object?>
            {
                ["room"] = key,
                ["cost"] = cost,
                ["minutes"] = minutes,
                ["paidFromBank"] = fromBank,
                ["readyAtUtc"] = hideout.RepairCompletesAtUtc,
                ["cashRemaining"] = player.Cash,
                ["bankRemaining"] = player.BankCash
            });
    }

    /// <summary>
    /// Lands a finished repair. Called wherever a player is refreshed, exactly like a finished tier
    /// build, so a room that came back while nobody was looking is working the first time they look.
    /// </summary>
    public string? CompleteRepair(Hideout? hideout, DateTime nowUtc)
    {
        if (hideout?.RepairingRoom is not { } room || hideout.RepairCompletesAtUtc is not { } due)
            return null;
        if (nowUtc < due)
            return null;

        hideout.SetWrecked(room, null);
        hideout.RepairingRoom = null;
        hideout.RepairCompletesAtUtc = null;
        return room;
    }

    /// <summary>The next level available for a room, whether or not the tier allows it yet.</summary>
    public NextRoomUpgrade? NextUpgrade(Hideout? hideout, string room)
    {
        var config = _options.Hideout;
        var currentTier = hideout?.Tier ?? 1;
        return room switch
        {
            "storage" => Next(config.Storage, hideout?.StorageLevel ?? 1, x => x.Level, x => x.UpgradeCost, x => x.MinTier, currentTier),
            "safe" => Next(config.Safe, hideout?.SafeLevel ?? 1, x => x.Level, x => x.UpgradeCost, x => x.MinTier, currentTier),
            "weedlab" => Next(config.WeedLab, hideout?.WeedLabLevel ?? 0, x => x.Level, x => x.UpgradeCost, x => x.MinTier, currentTier, hideout?.WorkshopLevel ?? 0, RequiredWorkshopForLab),
            "cokelab" => Next(config.CokeLab, hideout?.CokeLabLevel ?? 0, x => x.Level, x => x.UpgradeCost, x => x.MinTier, currentTier, hideout?.WorkshopLevel ?? 0, RequiredWorkshopForLab),
            "workshop" => Next(config.Workshop, hideout?.WorkshopLevel ?? 0, x => x.Level, x => x.UpgradeCost, x => x.MinTier, currentTier),
            "intelligence" => Next(config.Intelligence, hideout?.IntelligenceLevel ?? 0, x => x.Level, x => x.UpgradeCost, x => x.MinTier, currentTier),
            "lookout" => Next(config.Lookout, hideout?.LookoutLevel ?? 0, x => x.Level, x => x.UpgradeCost, x => x.MinTier, currentTier),
            _ => null
        };
    }

    /// <summary>
    /// The deepest level of a room a tier is allowed to hold. Seeded rivals use this so they start
    /// inside the same rules a player builds under: before the tier gates existed, seeding simply took
    /// the highest level in the table, which now means a Trap House with a Penthouse-sized safe.
    /// </summary>
    public int HighestLevelForTier(string room, int tier)
    {
        var config = _options.Hideout;
        return room switch
        {
            "storage" => Highest(config.Storage.Where(x => x.MinTier <= tier).Select(x => x.Level)),
            "safe" => Highest(config.Safe.Where(x => x.MinTier <= tier).Select(x => x.Level)),
            "weedlab" => Highest(config.WeedLab.Where(x => x.MinTier <= tier).Select(x => x.Level)),
            "cokelab" => Highest(config.CokeLab.Where(x => x.MinTier <= tier).Select(x => x.Level)),
            _ => 1
        };

        static int Highest(IEnumerable<int> levels)
        {
            var highest = 1;
            foreach (var level in levels)
                if (level > highest)
                    highest = level;
            return highest;
        }
    }

    /// <summary>
    /// The lowest storage level that would hold this much upkeep, or null when no room in the table is
    /// big enough. Used to tell a player which room they need rather than only that they are short.
    /// </summary>
    public int? StorageLevelThatHolds(int condoms, int beer)
    {
        int? best = null;
        foreach (var level in _options.Hideout.Storage)
            // Beer rather than beer-and-moonshine: the room being recommended has to hold the drink the
            // player can go out and buy. A level picked because its moonshine shelf made up the
            // difference is a level they upgrade to and are still short in, since the counter sells no
            // moonshine to put on that shelf.
            if (level.Condoms >= condoms && level.Beer >= beer && (best is null || level.Level < best))
                best = level.Level;
        return best;
    }

    /// <summary>The tier a hideout can move up to next, or null once it is the biggest there is.</summary>
    public HideoutTierOptions? NextTier(Hideout? hideout)
        => Level(_options.Hideout.Tiers, (hideout?.Tier ?? 1) + 1, x => x.Level);

    public string TierName(int tier)
        => Level(_options.Hideout.Tiers, tier, x => x.Level)?.Name ?? $"Tier {tier}";

    /// <summary>
    /// Starts the lab clock when the first lab goes up, so accrual runs from the build rather than
    /// paying out for every hour since the hideout was founded.
    /// </summary>
    private static void BuildLab(Hideout hideout, DateTime nowUtc, Action setLevel)
    {
        setLevel();
        hideout.LabsCollectedAtUtc ??= nowUtc;
    }

    private ActionResultResponse ApplyUpgrade<T>(
        Player player,
        Hideout hideout,
        List<T> levels,
        int currentLevel,
        Func<T, int> levelOf,
        Func<T, long> costOf,
        Func<T, int> minTierOf,
        Action<int> setLevel,
        string label,
        Func<int, int>? requiredWorkshopOf = null)
    {
        var next = Next(levels, currentLevel, levelOf, costOf, minTierOf, hideout.Tier, hideout.WorkshopLevel, requiredWorkshopOf)
            ?? throw new GameRuleException($"Your {label} is already at its highest level.");
        if (next.TierLocked)
            throw new GameRuleException($"A level {next.Level} {label} needs the {TierName(next.RequiredTier)} or better.");
        if (next.WorkshopLocked)
            throw new GameRuleException($"A level {next.Level} {label} needs a level {next.RequiredWorkshopLevel} workshop.");
        if (Capital.Available(player) < next.Cost)
            throw new GameRuleException($"You need {next.Cost:C0} across your cash and bank to upgrade the {label}.");

        var fromBank = ChargeCapital(player, next.Cost);
        setLevel(next.Level);

        return new ActionResultResponse(
            $"Upgraded the {label} to level {next.Level} for {next.Cost:C0}.",
            player.Turns,
            new Dictionary<string, object?>
            {
                ["room"] = label,
                ["level"] = next.Level,
                ["cost"] = next.Cost,
                ["paidFromBank"] = fromBank,
                ["cashRemaining"] = player.Cash,
                ["bankRemaining"] = player.BankCash
            });
    }

    /// <summary>
    /// Takes the price of an upgrade out of the bank first, then cash on hand, and returns how much the
    /// bank covered. The caller checks the combined total first.
    ///
    /// Every hideout price has to be paid this way, because the safe is itself one of the things being
    /// bought. Charging cash on hand would cap what a player can spend at the safe they already own,
    /// and several upgrades cost more than the safe one level below them holds: a level 3 safe costs
    /// $120,000 against a level 2 safe that holds $100,000, so it could never be bought, and everything
    /// gated behind it was unreachable too. Earnings over the safe are swept into the bank anyway, so
    /// the bank is where the money for a large purchase actually is.
    ///
    /// The till itself lives in <see cref="Capital"/>, because working ground up is the second thing in
    /// the game priced past what a safe holds and both have to charge it the same way.
    /// </summary>
    private static long ChargeCapital(Player player, long cost)
        => Capital.Charge(player, cost);

    private static NextRoomUpgrade? Next<T>(
        List<T> levels,
        int currentLevel,
        Func<T, int> levelOf,
        Func<T, long> costOf,
        Func<T, int> minTierOf,
        int currentTier,
        int currentWorkshop = int.MaxValue,
        Func<int, int>? requiredWorkshopOf = null)
    {
        foreach (var level in levels)
            if (levelOf(level) == currentLevel + 1)
            {
                var nextLevel = levelOf(level);
                var requiredWorkshop = requiredWorkshopOf?.Invoke(nextLevel) ?? 0;
                return new NextRoomUpgrade(
                    nextLevel,
                    costOf(level),
                    minTierOf(level),
                    minTierOf(level) > currentTier,
                    requiredWorkshop,
                    requiredWorkshop > currentWorkshop);
            }
        return null;
    }

    private static T? Level<T>(List<T> levels, int level, Func<T, int> levelOf) where T : class
    {
        foreach (var candidate in levels)
            if (levelOf(candidate) == level)
                return candidate;
        return null;
    }

    /// <summary>
    /// Refuses an action that needs the player to be standing in their own hideout, naming where it is.
    ///
    /// The counterpart to <see cref="TravelGate.EnsureLanded"/>, and it exists for the same reason:
    /// there is one rule here and two dozen places that would otherwise have to remember it. Being able
    /// to see the hideout page from another town is not the same as being able to reach into it, and
    /// this is the line between those two.
    /// </summary>
    public static void EnsureAtHideout(Player player, string what)
    {
        if (IsAtHideout(player)) return;
        var hideout = player.Hideout!;
        throw new GameRuleException(
            $"Your hideout is in {hideout.City} and you are in {player.City}. {what} needs you there.");
    }

    /// <summary>
    /// Whether the labs can be switched, and set to sell, from another town.
    ///
    /// The first thing the intelligence centre buys back once a player can be somewhere else. A switch
    /// is a phone call, so it is the cheapest remote control there is - but it is still bought rather
    /// than free, because being away from the house is supposed to cost something.
    /// </summary>
    public bool CanControlLabsRemotely(Hideout? hideout)
        => Reaches(hideout, _options.Hideout.RemoteLabControlLevel);

    /// <summary>
    /// Whether a repair can be started from another town. Dearer than a switch, because this one spends
    /// money and puts a crew in a room.
    /// </summary>
    public bool CanRepairRemotely(Hideout? hideout)
        => Reaches(hideout, _options.Hideout.RemoteRepairLevel);

    /// <summary>
    /// The intelligence level a remote control needs, against the one that is actually standing. A
    /// wrecked centre reaches nothing, which is the whole point of breaking it.
    /// </summary>
    private static bool Reaches(Hideout? hideout, int required)
        => required > 0 && (hideout?.WorkingLevel(HideoutRooms.Intelligence) ?? 0) >= required;

    /// <summary>
    /// Puts goods on the shelves, or takes them off, for a player standing in front of them.
    ///
    /// One method for both directions because they are one rule read in two directions: something can
    /// only move if the place it is going has room for it. Splitting them was the first draft, and the
    /// two halves immediately disagreed about guns - a rack is four piles under one ceiling, and the
    /// version that forgot that let a player shelve rifles into a shelf that was already full.
    /// </summary>
    public ActionResultResponse MoveStock(Player player, string? item, int quantity, bool depositing)
    {
        TravelGate.EnsureLanded(player);
        var hideout = player.Hideout ?? throw new GameRuleException("Your hideout is not set up yet.");
        EnsureAtHideout(player, depositing ? "Storing something" : "Taking something out");

        var key = TradeGoods.Normalise(item);
        if (!TradeGoods.IsStorable(key))
            throw new GameRuleException($"Store one of: {string.Join(", ", TradeGoods.Storable)}.");
        if (quantity <= 0)
            throw new GameRuleException("Move at least one.");

        var (from, to) = depositing ? ((IStash)player.Carried, player.Stored) : (player.Stored, player.Carried);
        var held = TradeGoods.Held(from, key);
        if (held <= 0)
            throw new GameRuleException(depositing
                ? $"You are not carrying any {TradeGoods.Label(key).ToLowerInvariant()}."
                : $"There is no {TradeGoods.Label(key).ToLowerInvariant()} on the shelves.");

        var room = depositing
            ? TradeGoods.Room(player.Stored, CapacityFor(hideout), key)
            : TradeGoods.Room(player.Carried, CarryCapacityFor(player).Of(key), key);
        if (room <= 0)
            throw new GameRuleException(depositing
                ? $"Your storage room is full of {TradeGoods.Label(key).ToLowerInvariant()}."
                : $"You cannot carry any more {TradeGoods.Label(key).ToLowerInvariant()}.");

        var moved = TradeGoods.Move(from, to, key, quantity, room);
        var label = TradeGoods.Label(key).ToLowerInvariant();
        var short_ = moved < quantity
            ? depositing
                ? $" The shelf only had room for {moved:N0}."
                : $" You could only carry {moved:N0}."
            : string.Empty;

        return new ActionResultResponse(
            depositing
                ? $"Put {moved:N0} {label} into storage in {hideout.City}.{short_}"
                : $"Took {moved:N0} {label} off the shelf in {hideout.City}.{short_}",
            player.Turns,
            new Dictionary<string, object?>
            {
                ["item"] = key,
                ["quantity"] = moved,
                ["direction"] = depositing ? "deposit" : "withdraw",
                ["carried"] = TradeGoods.Held(player.Carried, key),
                ["stored"] = TradeGoods.Held(player.Stored, key)
            });
    }

    /// <summary>
    /// Moves money between the player's pocket and the safe.
    ///
    /// Free, unlike the bank, and that is the trade the two offer against each other: the safe costs no
    /// turns but has to be walked to and can be carried out of the door by a raid, while the bank costs
    /// a trip and cannot be reached by anybody. A player in another town has neither - which is what
    /// makes deciding what to take with them a decision at all.
    /// </summary>
    public ActionResultResponse MoveCash(Player player, long amount, bool depositing)
    {
        TravelGate.EnsureLanded(player);
        var hideout = player.Hideout ?? throw new GameRuleException("Your hideout is not set up yet.");
        EnsureAtHideout(player, depositing ? "Opening the safe" : "Opening the safe");

        if (amount <= 0)
            throw new GameRuleException("Move at least a dollar.");

        long moved;
        if (depositing)
        {
            if (player.Cash < amount)
                throw new GameRuleException($"You are carrying {player.Cash:C0}.");
            var room = Math.Max(0, CapacityFor(hideout).MaxCash - hideout.SafeCash);
            if (room <= 0)
                throw new GameRuleException($"Your safe is full at {CapacityFor(hideout).MaxCash:C0}. Upgrade it to hold more.");
            moved = Math.Min(amount, room);
            player.Cash -= moved;
            hideout.SafeCash += moved;
        }
        else
        {
            if (hideout.SafeCash < amount)
                throw new GameRuleException($"The safe is holding {hideout.SafeCash:C0}.");
            moved = amount;
            hideout.SafeCash -= moved;
            player.Cash += moved;
        }

        var short_ = moved < amount ? $" The safe only had room for {moved:C0}." : string.Empty;
        return new ActionResultResponse(
            depositing
                ? $"Put {moved:C0} in the safe.{short_}"
                : $"Took {moved:C0} out of the safe.",
            player.Turns,
            new Dictionary<string, object?>
            {
                ["amount"] = moved,
                ["direction"] = depositing ? "deposit" : "withdraw",
                ["cash"] = player.Cash,
                ["safeCash"] = hideout.SafeCash
            });
    }

    /// <summary>
    /// Puts a gun on the player's hip, or takes the one that is there off.
    ///
    /// Only out of what they are carrying, which is what makes the rack worth splitting in the first
    /// place: a player with eight rifles on a shelf in New York and a pistol in their pocket in Las
    /// Vegas is carrying a pistol, and no amount of owning rifles changes that.
    /// </summary>
    public ActionResultResponse Equip(Player player, string? weapon)
    {
        TravelGate.EnsureLanded(player);
        var key = TradeGoods.Normalise(weapon);
        if (key.Length == 0)
        {
            player.EquippedWeapon = null;
            return new ActionResultResponse("You are carrying nothing on your hip.", player.Turns,
                new Dictionary<string, object?> { ["equipped"] = null });
        }

        if (!WeaponTiers.IsWeapon(key))
            throw new GameRuleException($"Carry one of: {string.Join(", ", WeaponTiers.All)}.");
        if (player.Carried.Armoury.Of(key) <= 0)
            throw new GameRuleException(
                $"You are not carrying a {WeaponTiers.One(key)}. Take one out of storage while you are at the hideout.");

        player.EquippedWeapon = key;
        return new ActionResultResponse($"You are carrying a {WeaponTiers.One(key)}.", player.Turns,
            new Dictionary<string, object?> { ["equipped"] = key });
    }

    /// <summary>
    /// Drops an equipped gun that is no longer being carried. Called wherever a player is refreshed,
    /// because a rack can empty in a dozen ways - a fight, a stop on the road, a deposit - and none of
    /// them should have to remember that a hip exists.
    /// </summary>
    public static void SettleEquipped(Player player)
    {
        if (player.EquippedWeapon is { } tier && player.Carried.Armoury.Of(tier) <= 0)
            player.EquippedWeapon = null;
    }

    /// <summary>How much of an amount does not fit, never dipping below what was already held.</summary>
    private static int Spill(int amount, int cap, int before)
        => Math.Max(0, amount - Math.Max(cap, before));
}

/// <summary>The next level of a room, and whether the hideout is big enough to hold it yet.</summary>
public sealed record NextRoomUpgrade(
    int Level,
    long Cost,
    int RequiredTier,
    bool TierLocked,
    int RequiredWorkshopLevel = 0,
    bool WorkshopLocked = false)
{
    public bool Locked => TierLocked || WorkshopLocked;
}

/// <summary>What the labs banked on their own, and whether the offline ceiling cut it short.</summary>
/// <param name="ClockMoved">
/// True when the lab clock itself was written, which happens even on a run that produced nothing.
/// The caller has to save in that case or the same hours are paid for twice.
/// </param>
/// <param name="Weed">What was shelved, which is what was made less anything sold on the spot.</param>
/// <param name="WeedSold">What went straight out of the door, for somebody whose lab moves its own.</param>
public sealed record LabYield(
    int Weed,
    int Coke,
    int WeedSold,
    int CokeSold,
    long Earned,
    int Hours,
    bool HitOfflineCeiling,
    bool ClockMoved)
{
    public static readonly LabYield None = new(0, 0, 0, 0, 0, 0, false, false);

    public bool Any => Weed > 0 || Coke > 0 || WeedSold > 0 || CokeSold > 0;

    /// <summary>A sentence for the dashboard, or empty when the labs made nothing worth mentioning.</summary>
    public string Describe()
    {
        if (!Any)
            return string.Empty;

        var made = new List<string>();
        if (Weed > 0) made.Add($"{Weed:N0} weed");
        if (Coke > 0) made.Add($"{Coke:N0} coke");
        // Said as a separate clause rather than folded into the total, because a player who set a lab
        // to sell wants to know it did - and a player who did not needs to notice that it is.
        var sold = new List<string>();
        if (WeedSold > 0) sold.Add($"{WeedSold:N0} weed");
        if (CokeSold > 0) sold.Add($"{CokeSold:N0} coke");

        var shelved = made.Count > 0 ? $"Your labs made {string.Join(" and ", made)} while you were away." : string.Empty;
        var moved = sold.Count > 0
            ? $"{(shelved.Length > 0 ? " They also sold" : "Your labs sold")} {string.Join(" and ", sold)}"
              + $" as it was made, for {Earned:C0}."
            : string.Empty;
        var ceiling = HitOfflineCeiling ? $" They only stack up {Hours} hour(s) of work, so the rest of your time away was idle." : string.Empty;
        return $"{shelved}{moved}{ceiling}".TrimStart();
    }
}

/// <summary>
/// What a raid took, and what it left broken.
///
/// The band is carried rather than worked out again later, because the heat it was read off is gone by
/// the time anybody describes this: the raid took the stash that was half of it and reset the rest to
/// nothing, so a report that recomputed the band afterwards would tell every player they had been
/// Quiet at the time.
/// </summary>
public sealed record ContrabandBust(
    int Weed,
    int Coke,
    int Moonshine,
    int Cut,
    long Fine,
    double HeatAtBust,
    string Band = "Quiet",
    IReadOnlyList<string>? Wrecked = null)
{
    public static readonly ContrabandBust None = new(0, 0, 0, 0, 0, 0);

    public IReadOnlyList<string> WreckedRooms => Wrecked ?? [];

    public int Units => Weed + Coke + Moonshine + Cut;

    /// <summary>
    /// Whether there is anything to report. Rooms count, and have to: somebody with a lot of earned
    /// heat and an empty store can be raided, and before rooms could break, that raid took nothing,
    /// wrote no log and told nobody it had happened.
    /// </summary>
    public bool Happened => Units > 0 || WreckedRooms.Count > 0;

    /// <summary>Names what was actually taken rather than listing every pile including the empty ones.</summary>
    public string Describe()
    {
        var taken = new List<string>();
        if (Coke > 0) taken.Add($"{Coke:N0} coke");
        if (Moonshine > 0) taken.Add($"{Moonshine:N0} moonshine");
        if (Weed > 0) taken.Add($"{Weed:N0} weed");
        if (Cut > 0) taken.Add($"{Cut:N0} cut");
        var haul = taken.Count == 0 ? "nothing worth naming" : string.Join(", ", taken);
        var opening = Units == 0
            ? "The law came through and found nothing worth carrying out."
            : Fine > 0
                ? $"The law came through. They took {haul} and fined you {Fine:C0}."
                : $"The law came through and took {haul}.";
        if (WreckedRooms.Count == 0)
            return opening;

        var rooms = string.Join(" and ", WreckedRooms.Select(HideoutRooms.Name));
        // Two rooms is the ordinary case at the top band, so the plural is not a nicety here.
        var tail = WreckedRooms.Count == 1
            ? "and it stays down until you pay to have it put back"
            : "and they stay down until you pay to have them put back";
        return $"{opening} They wrecked your {rooms} on the way out, {tail}.";
    }
}

public sealed record HideoutCapacity(
    string TierName,
    int Tier,
    int StorageLevel,
    int SafeLevel,
    int WeedLabLevel,
    int CokeLabLevel,
    int MaxPimps,
    int MaxHoes,
    int MaxThugs,
    int MaxRides,
    long MaxCash,
    int MaxCondoms,
    int MaxBeer,
    int MaxWeapons,
    int MaxWeed,
    int MaxCoke,
    int MaxMoonshine,
    int MaxCut,
    int MaxMedicine,
    int MaxPoison);

/// <summary>
/// What a player can physically carry, once every modifier has been applied.
///
/// The same shape as <see cref="HideoutCapacity"/> for the goods the two have in common, because they
/// are the same question asked of two different places: one is a shelf and the other is a pair of
/// pockets, and the rules that move goods between them should not have to care which they are looking
/// at.
/// </summary>
public sealed record CarryCapacity(
    bool Enforced,
    int MaxCondoms,
    int MaxBeer,
    int MaxWeapons,
    int MaxWeed,
    int MaxCoke,
    int MaxMoonshine,
    int MaxCut,
    int MaxMedicine,
    int MaxPoison)
{
    /// <summary>
    /// No limit at all, for a configuration with carry limits switched off. Everything is int.MaxValue
    /// rather than zero, so the switch reads as "carry what you like" at every call site without any
    /// of them needing to check <see cref="Enforced"/> first.
    /// </summary>
    public static readonly CarryCapacity Unlimited = new(
        false, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue,
        int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue);

    public int Of(string key) => key switch
    {
        "condoms" => MaxCondoms,
        "beer" => MaxBeer,
        "medicine" => MaxMedicine,
        "poison" => MaxPoison,
        "weed" => MaxWeed,
        "coke" => MaxCoke,
        "moonshine" => MaxMoonshine,
        "cut" => MaxCut,
        "weapons" => MaxWeapons,
        _ => WeaponTiers.IsWeapon(key) ? MaxWeapons : 0
    };
}

/// <summary>The stock a player held before an action, used as the floor for grandfathered amounts.</summary>
public sealed record StockLevels(
    long Cash,
    int Condoms,
    int Beer,
    int Weapons,
    int Weed,
    int Coke,
    int Medicine,
    int Poison,
    int Moonshine = 0,
    int Cut = 0)
{
    public static StockLevels From(Player player)
        => new(player.Cash, player.Condoms, player.Beer, player.Weapons, player.Weed, player.Coke,
            player.Medicine, player.Poison, player.Moonshine, player.Cut);

    /// <summary>The floor for one good, by the key the settling loop walks.</summary>
    public int Of(string key) => key switch
    {
        "condoms" => Condoms,
        "beer" => Beer,
        "medicine" => Medicine,
        "poison" => Poison,
        "weed" => Weed,
        "coke" => Coke,
        "moonshine" => Moonshine,
        "cut" => Cut,
        "weapons" => Weapons,
        _ => WeaponTiers.IsWeapon(key) ? Weapons : 0
    };
}

/// <summary>
/// What would not fit when an action finished: what went on the shelves, and what was dropped.
///
/// The cash half is gone. It used to say "your safe was full so the money went to the bank", which was
/// true while the safe was the ceiling on cash on hand; now that the safe is somewhere the player has
/// to walk to, cash in a pocket has no ceiling to overflow.
/// </summary>
public sealed record StorageOverflow(
    int CondomsLost,
    int BeerLost,
    int WeaponsLost,
    int WeedLost,
    int CokeLost,
    int MedicineLost,
    int PoisonLost = 0,
    int MoonshineLost = 0,
    int CutLost = 0,
    /// <summary>How much of the surplus made it on to a shelf rather than into the gutter.</summary>
    int Stored = 0)
{
    public int TotalLost => CondomsLost + BeerLost + WeaponsLost + WeedLost + CokeLost
                            + MedicineLost + PoisonLost + MoonshineLost + CutLost;

    public bool Any => TotalLost > 0 || Stored > 0;

    /// <summary>A sentence to append to an action summary, or empty when nothing overflowed.</summary>
    public string Describe()
    {
        var sentences = new List<string>();
        if (Stored > 0)
            sentences.Add($"You could not carry it all, so {Stored:N0} went into hideout storage.");

        var lost = new List<string>();
        if (CondomsLost > 0) lost.Add($"{CondomsLost:N0} condoms");
        if (BeerLost > 0) lost.Add($"{BeerLost:N0} beer");
        if (WeaponsLost > 0) lost.Add($"{WeaponsLost:N0} weapons");
        if (WeedLost > 0) lost.Add($"{WeedLost:N0} weed");
        if (CokeLost > 0) lost.Add($"{CokeLost:N0} coke");
        if (MedicineLost > 0) lost.Add($"{MedicineLost:N0} medicine");
        if (PoisonLost > 0) lost.Add($"{PoisonLost:N0} poison");
        if (MoonshineLost > 0) lost.Add($"{MoonshineLost:N0} moonshine");
        if (CutLost > 0) lost.Add($"{CutLost:N0} cut");
        if (lost.Count > 0)
            sentences.Add($"Your hands and your storage were both full, so you lost {string.Join(", ", lost)}.");

        return sentences.Count == 0 ? string.Empty : $" {string.Join(" ", sentences)}";
    }
}
