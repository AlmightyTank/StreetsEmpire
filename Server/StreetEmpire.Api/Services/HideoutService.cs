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
        if (hideout is null || (hideout.WeedLabLevel <= 0 && hideout.CokeLabLevel <= 0 && hideout.StillLevel <= 0))
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
        player.Coke += coke;

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
        return Math.Max(0, player.Heat)
               + player.Coke * config.CokeHeatPerUnit
               + player.Moonshine * config.MoonshineHeatPerUnit
               + player.Weed * config.WeedHeatPerUnit
               + player.Cut * config.CutHeatPerUnit;
    }

    /// <summary>
    /// Cools earned heat and then rolls for the law turning up, once per elapsed hour.
    ///
    /// Rolled on the clock rather than on an action so it costs a player who stockpiles whether or not
    /// they are at the screen, and costs one who sells and lays low almost nothing. A bust takes a
    /// share of every contraband pile and fines them, and the fine stops at cash on hand: one that
    /// could push a player into debt is a different and much nastier mechanic than losing the stash.
    /// </summary>
    public ContrabandBust RollBust(Player player, int hours, IGameRandom random)
    {
        if (hours <= 0)
            return ContrabandBust.None;

        var config = _options.Hideout;
        player.Heat = Math.Max(0, player.Heat - Math.Max(0, config.HeatDecayPerHour) * hours);

        var heat = HeatFor(player);
        if (heat <= config.HeatBustFloor)
            return ContrabandBust.None;

        var chance = Math.Clamp((heat - config.HeatBustFloor) * config.BustChancePerHeat, 0, Math.Clamp(config.MaxBustChancePerHour, 0, 1));
        var caught = false;
        for (var hour = 0; hour < hours && !caught; hour++)
            caught = random.NextDouble() < chance;
        if (!caught)
            return ContrabandBust.None;

        var share = Math.Clamp(config.SeizedPercent, 0, 1);
        var weed = Seize(player, "weed", share);
        var coke = Seize(player, "coke", share);
        var moonshine = Seize(player, "moonshine", share);
        var cut = Seize(player, "cut", share);

        var units = weed + coke + moonshine + cut;
        var fine = Math.Min(player.Cash, (long)Math.Round(units * Math.Max(0, config.FinePerSeizedUnit)));
        player.Cash -= fine;
        // A raid resets the attention it was drawn by. Leaving it high would mean one bust guarantees
        // the next, which is a spiral rather than a risk.
        player.Heat = 0;

        return new ContrabandBust(weed, coke, moonshine, cut, fine, Math.Round(heat, 1));
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
    public WorkshopLevelOptions? StationFor(Hideout? hideout, string station)
    {
        var (levels, level) = station switch
        {
            "still" => (_options.Hideout.Still, hideout?.StillLevel ?? 0),
            "mix" => (_options.Hideout.Mix, hideout?.MixLevel ?? 0),
            _ => (_options.Hideout.Workshop, hideout?.WorkshopLevel ?? 0)
        };
        return level <= 0 ? null : Level(levels, level, x => x.Level);
    }

    public WorkshopLevelOptions? WorkshopFor(Hideout? hideout) => StationFor(hideout, "workshop");

    /// <summary>
    /// How many mule runs may be in the air at once. Zero without the room, which is what makes the
    /// intelligence centre the gate on mule running rather than a discount on it.
    /// </summary>
    public int ConcurrentRunCap(Hideout? hideout)
    {
        var level = hideout?.IntelligenceLevel ?? 0;
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
        var level = hideout?.IntelligenceLevel ?? 0;
        if (level <= 0) return 0;
        var percent = Level(_options.Hideout.Intelligence, level, x => x.Level)?.RiskReductionPercent ?? 0;
        return Math.Clamp(percent / 100.0, 0, 0.9);
    }

    /// <summary>
    /// The tier a station needs, or null when it has no gate. Checked when making as well as when
    /// building: buying is not the only way to end up with one, since a station built before a gate
    /// existed would otherwise keep running under it forever.
    /// </summary>
    public int? StationRequiredTier(string station)
    {
        var levels = station switch
        {
            "still" => _options.Hideout.Still,
            "mix" => _options.Hideout.Mix,
            _ => _options.Hideout.Workshop
        };
        var first = levels.OrderBy(x => x.Level).FirstOrDefault();
        return first is null || first.MinTier <= 1 ? null : first.MinTier;
    }

    /// <summary>What a lab makes per hour on its own, before storage limits.</summary>
    public int PassivePerHour(Hideout? hideout, string product)
    {
        var (levels, level) = LabFor(hideout, product);
        return level <= 0 ? 0 : Level(levels, level, x => x.Level)?.PassivePerHour ?? 0;
    }

    public HideoutCapacity CapacityFor(Hideout? hideout)
    {
        var config = _options.Hideout;
        var tier = Level(config.Tiers, hideout?.Tier ?? 1, x => x.Level)
            ?? new HideoutTierOptions { MaxPimps = int.MaxValue, MaxHoes = int.MaxValue, MaxThugs = int.MaxValue };
        var storage = Level(config.Storage, hideout?.StorageLevel ?? 1, x => x.Level)
            ?? new StorageLevelOptions { Condoms = int.MaxValue, Beer = int.MaxValue, Weapons = int.MaxValue, Weed = int.MaxValue, Coke = int.MaxValue };
        var safe = Level(config.Safe, hideout?.SafeLevel ?? 1, x => x.Level)
            ?? new SafeLevelOptions { MaxCash = long.MaxValue };

        return new HideoutCapacity(
            tier.Name,
            hideout?.Tier ?? 1,
            hideout?.StorageLevel ?? 1,
            hideout?.SafeLevel ?? 1,
            hideout?.WeedLabLevel ?? 0,
            hideout?.CokeLabLevel ?? 0,
            tier.MaxPimps,
            tier.MaxHoes,
            tier.MaxThugs,
            safe.MaxCash,
            storage.Condoms,
            storage.Beer,
            storage.Weapons,
            storage.Weed,
            storage.Coke,
            storage.Moonshine,
            storage.Cut);
    }

    /// <summary>
    /// Extra production units per turn, as a percentage, from the lab for this product.
    /// </summary>
    public int ProductionYieldBonusPercent(Hideout? hideout, string product)
    {
        var (levels, level) = LabFor(hideout, product);
        return level <= 0 ? 0 : Level(levels, level, x => x.Level)?.YieldBonusPercent ?? 0;
    }

    private (List<LabLevelOptions> Levels, int Level) LabFor(Hideout? hideout, string product)
        => product == "coke"
            ? (_options.Hideout.CokeLab, hideout?.CokeLabLevel ?? 0)
            : (_options.Hideout.WeedLab, hideout?.WeedLabLevel ?? 0);

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
        player.Condoms -= condoms;
        player.Beer -= beer;
        player.Weapons -= weapons;
        player.Weed -= weed;
        player.Coke -= coke;

        return new StorageOverflow(banked, condoms, beer, weapons, weed, coke);
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
        player.Weapons = Math.Min(player.Weapons, capacity.MaxWeapons);
        player.Weed = Math.Min(player.Weed, capacity.MaxWeed);
        player.Coke = Math.Min(player.Coke, capacity.MaxCoke);

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
        var key = room?.Trim().ToLowerInvariant() ?? string.Empty;
        var config = _options.Hideout;

        return key switch
        {
            "tier" => StartTierUpgrade(player, hideout, nowUtc),
            "storage" => ApplyUpgrade(player, hideout, config.Storage, hideout.StorageLevel, x => x.Level, x => x.UpgradeCost, x => x.MinTier,
                level => hideout.StorageLevel = level, "storage room"),
            "safe" => ApplyUpgrade(player, hideout, config.Safe, hideout.SafeLevel, x => x.Level, x => x.UpgradeCost, x => x.MinTier,
                level => hideout.SafeLevel = level, "safe"),
            "weedlab" => ApplyUpgrade(player, hideout, config.WeedLab, hideout.WeedLabLevel, x => x.Level, x => x.UpgradeCost, x => x.MinTier,
                level => BuildLab(hideout, nowUtc, () => hideout.WeedLabLevel = level), "weed lab"),
            "cokelab" => ApplyUpgrade(player, hideout, config.CokeLab, hideout.CokeLabLevel, x => x.Level, x => x.UpgradeCost, x => x.MinTier,
                level => BuildLab(hideout, nowUtc, () => hideout.CokeLabLevel = level), "coke lab"),
            "workshop" => ApplyUpgrade(player, hideout, config.Workshop, hideout.WorkshopLevel, x => x.Level, x => x.UpgradeCost, x => x.MinTier,
                level => hideout.WorkshopLevel = level, "workshop"),
            "still" => ApplyUpgrade(player, hideout, config.Still, hideout.StillLevel, x => x.Level, x => x.UpgradeCost, x => x.MinTier,
                level => hideout.StillLevel = level, "still"),
            "mix" => ApplyUpgrade(player, hideout, config.Mix, hideout.MixLevel, x => x.Level, x => x.UpgradeCost, x => x.MinTier,
                level => hideout.MixLevel = level, "mix house"),
            "intelligence" => ApplyUpgrade(player, hideout, config.Intelligence, hideout.IntelligenceLevel, x => x.Level, x => x.UpgradeCost, x => x.MinTier,
                level => hideout.IntelligenceLevel = level, "intelligence centre"),
            _ => throw new GameRuleException(
                "Room must be 'tier', 'storage', 'safe', 'weedlab', 'cokelab', 'workshop', 'still', 'mix', or 'intelligence'.")
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

    /// <summary>The next level available for a room, whether or not the tier allows it yet.</summary>
    public NextRoomUpgrade? NextUpgrade(Hideout? hideout, string room)
    {
        var config = _options.Hideout;
        var currentTier = hideout?.Tier ?? 1;
        return room switch
        {
            "storage" => Next(config.Storage, hideout?.StorageLevel ?? 1, x => x.Level, x => x.UpgradeCost, x => x.MinTier, currentTier),
            "safe" => Next(config.Safe, hideout?.SafeLevel ?? 1, x => x.Level, x => x.UpgradeCost, x => x.MinTier, currentTier),
            "weedlab" => Next(config.WeedLab, hideout?.WeedLabLevel ?? 0, x => x.Level, x => x.UpgradeCost, x => x.MinTier, currentTier),
            "cokelab" => Next(config.CokeLab, hideout?.CokeLabLevel ?? 0, x => x.Level, x => x.UpgradeCost, x => x.MinTier, currentTier),
            "workshop" => Next(config.Workshop, hideout?.WorkshopLevel ?? 0, x => x.Level, x => x.UpgradeCost, x => x.MinTier, currentTier),
            "still" => Next(config.Still, hideout?.StillLevel ?? 0, x => x.Level, x => x.UpgradeCost, x => x.MinTier, currentTier),
            "mix" => Next(config.Mix, hideout?.MixLevel ?? 0, x => x.Level, x => x.UpgradeCost, x => x.MinTier, currentTier),
            "intelligence" => Next(config.Intelligence, hideout?.IntelligenceLevel ?? 0, x => x.Level, x => x.UpgradeCost, x => x.MinTier, currentTier),
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
        string label)
    {
        var next = Next(levels, currentLevel, levelOf, costOf, minTierOf, hideout.Tier)
            ?? throw new GameRuleException($"Your {label} is already at its highest level.");
        if (next.TierLocked)
            throw new GameRuleException($"A level {next.Level} {label} needs the {TierName(next.RequiredTier)} or better.");
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
    /// </summary>
    private static long ChargeCapital(Player player, long cost)
    {
        var fromBank = Math.Min(player.BankCash, cost);
        player.BankCash -= fromBank;
        player.Cash -= cost - fromBank;
        return fromBank;
    }

    private static NextRoomUpgrade? Next<T>(
        List<T> levels,
        int currentLevel,
        Func<T, int> levelOf,
        Func<T, long> costOf,
        Func<T, int> minTierOf,
        int currentTier)
    {
        foreach (var level in levels)
            if (levelOf(level) == currentLevel + 1)
                return new NextRoomUpgrade(levelOf(level), costOf(level), minTierOf(level), minTierOf(level) > currentTier);
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
public sealed record NextRoomUpgrade(int Level, long Cost, int RequiredTier, bool TierLocked);

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

/// <summary>What a raid took. Zero units means nothing happened.</summary>
public sealed record ContrabandBust(int Weed, int Coke, int Moonshine, int Cut, long Fine, double HeatAtBust)
{
    public static readonly ContrabandBust None = new(0, 0, 0, 0, 0, 0);

    public int Units => Weed + Coke + Moonshine + Cut;
    public bool Happened => Units > 0;

    /// <summary>Names what was actually taken rather than listing every pile including the empty ones.</summary>
    public string Describe()
    {
        var taken = new List<string>();
        if (Coke > 0) taken.Add($"{Coke:N0} coke");
        if (Moonshine > 0) taken.Add($"{Moonshine:N0} moonshine");
        if (Weed > 0) taken.Add($"{Weed:N0} weed");
        if (Cut > 0) taken.Add($"{Cut:N0} cut");
        var haul = taken.Count == 0 ? "nothing worth naming" : string.Join(", ", taken);
        return Fine > 0
            ? $"The law came through. They took {haul} and fined you {Fine:C0}."
            : $"The law came through and took {haul}.";
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
    long MaxCash,
    int MaxCondoms,
    int MaxBeer,
    int MaxWeapons,
    int MaxWeed,
    int MaxCoke,
    int MaxMoonshine,
    int MaxCut);

/// <summary>The stock a player held before an action, used as the floor for grandfathered amounts.</summary>
public sealed record StockLevels(long Cash, int Condoms, int Beer, int Weapons, int Weed, int Coke)
{
    public static StockLevels From(Player player)
        => new(player.Cash, player.Condoms, player.Beer, player.Weapons, player.Weed, player.Coke);
}

public sealed record StorageOverflow(
    long CashBanked,
    int CondomsLost,
    int BeerLost,
    int WeaponsLost,
    int WeedLost,
    int CokeLost)
{
    public bool Any => CashBanked > 0 || CondomsLost > 0 || BeerLost > 0 || WeaponsLost > 0 || WeedLost > 0 || CokeLost > 0;

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
        if (lost.Count > 0)
            sentences.Add($"Storage overflowed and you lost {string.Join(", ", lost)}.");

        return sentences.Count == 0 ? string.Empty : $" {string.Join(" ", sentences)}";
    }
}
