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
        var weed = Produce(player.Weed, capacity.MaxWeed, PassivePerHour(hideout, "weed") * chargedHours);
        var coke = Produce(player.Coke, capacity.MaxCoke, PassivePerHour(hideout, "coke") * chargedHours);
        player.Weed += weed;
        player.AddCoke(coke, 1);

        return new LabYield(weed, coke, chargedHours, hours > chargedHours, true);
    }

    /// <summary>
    /// Total attention on a player: what they are sitting on, plus what they have earned working.
    ///
    /// Weighted per good rather than flat, because everything here is illegal and so being illegal
    /// distinguishes nothing. What differs is how much notice a thing draws.
    /// </summary>
    public double HeatFor(Player player)
    {
        var config = _options.Hideout;
        // What is held draws notice at the town's own rate: the same stash is more conspicuous in a
        // watchful city than a quiet one, which is what makes where you live a standing decision.
        var town = _options.CityMarkets.HeatMultiplier(player.City);
        return Math.Max(0, player.Heat)
               + town * (player.Coke * config.CokeHeatPerUnit
               + player.Moonshine * config.MoonshineHeatPerUnit
               + player.Weed * config.WeedHeatPerUnit
               + player.Cut * config.CutHeatPerUnit);
    }

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
        var share = HeatBands.SeizedPercent(band, config);
        var weed = Seize(player, "weed", share);
        var coke = Seize(player, "coke", share);
        var moonshine = Seize(player, "moonshine", share);
        var cut = Seize(player, "cut", share);

        var units = weed + coke + moonshine + cut;
        var fine = Math.Min(player.Cash, (long)Math.Round(units * Math.Max(0, config.FinePerSeizedUnit)));
        player.Cash -= fine;
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
    private static int Seize(Player player, string good, double share)
    {
        var held = TradeGoods.Held(player, good);
        if (held <= 0) return 0;
        var taken = Math.Min(held, Math.Max(1, (int)Math.Round(held * share)));
        TradeGoods.Add(player, good, -taken);
        return taken;
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
    /// Settles a finished action against the hideout's limits. Cash over the safe is moved to the
    /// bank; goods over storage are lost. Stock a player already held is never taken away, so
    /// grandfathered amounts survive and drain down naturally through upkeep instead.
    /// </summary>
    public StorageOverflow Settle(Player player, StockLevels before)
    {
        var capacity = CapacityFor(player.Hideout);

        var cashCeiling = Math.Max(capacity.MaxCash, before.Cash);
        var banked = 0L;
        if (player.Cash > cashCeiling)
        {
            banked = player.Cash - cashCeiling;
            player.Cash = cashCeiling;
            player.BankCash += banked;
        }

        var condoms = Spill(player.Condoms, capacity.MaxCondoms, before.Condoms);
        var beer = Spill(player.Beer, capacity.MaxBeer, before.Beer);
        var weapons = Spill(player.Weapons, capacity.MaxWeapons, before.Weapons);
        var weed = Spill(player.Weed, capacity.MaxWeed, before.Weed);
        var coke = Spill(player.Coke, capacity.MaxCoke, before.Coke);
        var medicine = Spill(player.Medicine, capacity.MaxMedicine, before.Medicine);
        var poison = Spill(player.Poison, capacity.MaxPoison, before.Poison);
        player.Condoms -= condoms;
        player.Beer -= beer;
        // Cheapest guns first. A full rack losing its rifles to an overflowing shelf would make owning
        // good ones a hazard, which is the opposite of what a gun rack is for.
        player.RemoveWeapons(weapons);
        player.Weed -= weed;
        player.Coke -= coke;
        player.Medicine -= medicine;
        player.Poison -= poison;

        return new StorageOverflow(banked, condoms, beer, weapons, weed, coke, medicine);
    }

    /// <summary>
    /// Hard-clamps a player to capacity with no grandfathering, moving excess cash to the bank rather
    /// than destroying it. Used when seeding rivals, who must play by the same limits as players.
    /// </summary>
    public void ClampToCapacity(Player player)
    {
        var capacity = CapacityFor(player.Hideout);
        player.Pimps = Math.Min(player.Pimps, capacity.MaxPimps);
        player.Hoes = Math.Min(player.Hoes, capacity.MaxHoes);
        player.Thugs = Math.Min(player.Thugs, capacity.MaxThugs);
        player.Condoms = Math.Min(player.Condoms, capacity.MaxCondoms);
        player.Beer = Math.Min(player.Beer, capacity.MaxBeer);
        player.RemoveWeapons(Math.Max(0, player.Weapons - capacity.MaxWeapons));
        player.Weed = Math.Min(player.Weed, capacity.MaxWeed);
        player.Coke = Math.Min(player.Coke, capacity.MaxCoke);
        player.Medicine = Math.Min(player.Medicine, capacity.MaxMedicine);
        player.Poison = Math.Min(player.Poison, capacity.MaxPoison);
        player.Rides = Math.Min(player.Rides, capacity.MaxRides);

        var overSafe = player.Cash - capacity.MaxCash;
        if (overSafe > 0)
        {
            player.Cash -= overSafe;
            player.BankCash += overSafe;
        }
    }

    public ActionResultResponse Upgrade(Player player, string? room, DateTime nowUtc)
    {
        var hideout = player.Hideout ?? throw new GameRuleException("Your hideout is not set up yet.");
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
        if (player.Cash + player.BankCash < next.UpgradeCost)
            throw new GameRuleException($"Moving up to the {next.Name} costs {next.UpgradeCost:C0} across your cash and bank.");
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
        if (player.Cash + player.BankCash < cost)
            throw new GameRuleException(
                $"Putting the {HideoutRooms.Name(key)} back costs {cost:C0} across your cash and bank. You have {player.Cash + player.BankCash:C0}.");

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
        if (player.Cash + player.BankCash < next.Cost)
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
public sealed record LabYield(int Weed, int Coke, int Hours, bool HitOfflineCeiling, bool ClockMoved)
{
    public static readonly LabYield None = new(0, 0, 0, false, false);

    public bool Any => Weed > 0 || Coke > 0;

    /// <summary>A sentence for the dashboard, or empty when the labs made nothing worth mentioning.</summary>
    public string Describe()
    {
        if (!Any)
            return string.Empty;

        var made = new List<string>();
        if (Weed > 0) made.Add($"{Weed:N0} weed");
        if (Coke > 0) made.Add($"{Coke:N0} coke");
        var ceiling = HitOfflineCeiling ? $" They only stack up {Hours} hour(s) of work, so the rest of your time away was idle." : string.Empty;
        return $"Your labs made {string.Join(" and ", made)} while you were away.{ceiling}";
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

/// <summary>The stock a player held before an action, used as the floor for grandfathered amounts.</summary>
public sealed record StockLevels(long Cash, int Condoms, int Beer, int Weapons, int Weed, int Coke, int Medicine, int Poison)
{
    public static StockLevels From(Player player)
        => new(player.Cash, player.Condoms, player.Beer, player.Weapons, player.Weed, player.Coke, player.Medicine, player.Poison);
}

public sealed record StorageOverflow(
    long CashBanked,
    int CondomsLost,
    int BeerLost,
    int WeaponsLost,
    int WeedLost,
    int CokeLost,
    int MedicineLost)
{
    public bool Any => CashBanked > 0 || CondomsLost > 0 || BeerLost > 0 || WeaponsLost > 0 || WeedLost > 0 || CokeLost > 0 || MedicineLost > 0;

    /// <summary>A sentence to append to an action summary, or empty when nothing overflowed.</summary>
    public string Describe()
    {
        var sentences = new List<string>();
        if (CashBanked > 0)
            sentences.Add($"Your safe was full, so ${CashBanked:N0} went to the bank.");

        var lost = new List<string>();
        if (CondomsLost > 0) lost.Add($"{CondomsLost:N0} condoms");
        if (BeerLost > 0) lost.Add($"{BeerLost:N0} beer");
        if (WeaponsLost > 0) lost.Add($"{WeaponsLost:N0} weapons");
        if (WeedLost > 0) lost.Add($"{WeedLost:N0} weed");
        if (CokeLost > 0) lost.Add($"{CokeLost:N0} coke");
        if (MedicineLost > 0) lost.Add($"{MedicineLost:N0} medicine");
        if (lost.Count > 0)
            sentences.Add($"Storage overflowed and you lost {string.Join(", ", lost)}.");

        return sentences.Count == 0 ? string.Empty : $" {string.Join(" ", sentences)}";
    }
}
