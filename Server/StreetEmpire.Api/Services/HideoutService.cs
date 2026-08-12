using Microsoft.Extensions.Options;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

/// <summary>
/// Owns hideout capacity. Crew comes from the tier, goods from the storage room, cash on hand from
/// the safe, and production yield from the labs.
/// </summary>
public sealed class HideoutService(IOptions<GameOptions> options)
{
    private readonly GameOptions _options = options.Value;

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
            storage.Coke);
    }

    /// <summary>
    /// Extra production units per turn, as a percentage, from the lab for this product.
    /// </summary>
    public int ProductionYieldBonusPercent(Hideout? hideout, string product)
    {
        var config = _options.Hideout;
        var (levels, level) = product == "coke"
            ? (config.CokeLab, hideout?.CokeLabLevel ?? 0)
            : (config.WeedLab, hideout?.WeedLabLevel ?? 0);
        return level <= 0 ? 0 : Level(levels, level, x => x.Level)?.YieldBonusPercent ?? 0;
    }

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

    public ActionResultResponse Upgrade(Player player, string? room)
    {
        var hideout = player.Hideout ?? throw new GameRuleException("Your hideout is not set up yet.");
        var key = room?.Trim().ToLowerInvariant() ?? string.Empty;
        var config = _options.Hideout;

        return key switch
        {
            "storage" => ApplyUpgrade(player, config.Storage, hideout.StorageLevel, x => x.Level, x => x.UpgradeCost,
                level => hideout.StorageLevel = level, "storage room"),
            "safe" => ApplyUpgrade(player, config.Safe, hideout.SafeLevel, x => x.Level, x => x.UpgradeCost,
                level => hideout.SafeLevel = level, "safe"),
            "weedlab" => ApplyUpgrade(player, config.WeedLab, hideout.WeedLabLevel, x => x.Level, x => x.UpgradeCost,
                level => hideout.WeedLabLevel = level, "weed lab"),
            "cokelab" => ApplyUpgrade(player, config.CokeLab, hideout.CokeLabLevel, x => x.Level, x => x.UpgradeCost,
                level => hideout.CokeLabLevel = level, "coke lab"),
            _ => throw new GameRuleException("Room must be 'storage', 'safe', 'weedlab', or 'cokelab'.")
        };
    }

    /// <summary>The cost of the next level for a room, or null when it is already maxed.</summary>
    public long? NextUpgradeCost(Hideout? hideout, string room)
    {
        var config = _options.Hideout;
        return room switch
        {
            "storage" => NextCost(config.Storage, hideout?.StorageLevel ?? 1, x => x.Level, x => x.UpgradeCost),
            "safe" => NextCost(config.Safe, hideout?.SafeLevel ?? 1, x => x.Level, x => x.UpgradeCost),
            "weedlab" => NextCost(config.WeedLab, hideout?.WeedLabLevel ?? 0, x => x.Level, x => x.UpgradeCost),
            "cokelab" => NextCost(config.CokeLab, hideout?.CokeLabLevel ?? 0, x => x.Level, x => x.UpgradeCost),
            _ => null
        };
    }

    private ActionResultResponse ApplyUpgrade<T>(
        Player player,
        List<T> levels,
        int currentLevel,
        Func<T, int> levelOf,
        Func<T, long> costOf,
        Action<int> setLevel,
        string label)
    {
        var next = levels
            .Where(x => levelOf(x) == currentLevel + 1)
            .Select(x => (Level: levelOf(x), Cost: costOf(x)))
            .FirstOrDefault();
        if (next.Level == 0)
            throw new GameRuleException($"Your {label} is already at its highest level.");
        if (player.Cash < next.Cost)
            throw new GameRuleException($"You need {next.Cost:C0} cash on hand to upgrade the {label}.");

        player.Cash -= next.Cost;
        setLevel(next.Level);

        return new ActionResultResponse(
            $"Upgraded the {label} to level {next.Level} for {next.Cost:C0}.",
            player.Turns,
            new Dictionary<string, object?>
            {
                ["room"] = label,
                ["level"] = next.Level,
                ["cost"] = next.Cost,
                ["cashRemaining"] = player.Cash
            });
    }

    private static long? NextCost<T>(List<T> levels, int currentLevel, Func<T, int> levelOf, Func<T, long> costOf)
    {
        foreach (var level in levels)
            if (levelOf(level) == currentLevel + 1)
                return costOf(level);
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
    int MaxCoke);

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
