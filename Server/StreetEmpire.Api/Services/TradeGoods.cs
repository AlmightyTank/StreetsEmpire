using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

/// <summary>
/// The one place that knows how a good's key maps to the column holding it and the cap on that column.
///
/// The store, production, admin adjustments and the market all move the same piles around.
/// Written out separately in each, a good lands in one place and not another, and a market that can
/// take goods it cannot give back is worse than no market.
/// </summary>
public static class TradeGoods
{
    // Medicine trades for the same reason guns do: it is bought against something another player might
    // do, and a house that has just been infested wants it now rather than next payday. The four guns
    // trade separately because a rack of pistols and a rack of rifles are not the same offer, and a board
    // that listed both as "weapons" would price them as if they were.
    public static readonly IReadOnlyList<string> Keys =
        ["condoms", "beer", "medicine", .. WeaponTiers.All, "weed", "coke", "moonshine", "cut"];

    public static bool IsTradeable(string? key)
        => key is not null && Keys.Contains(key.Trim().ToLowerInvariant());

    /// <summary>
    /// Everything that occupies a shelf, which is not the same list as everything players sell each
    /// other. Poison is the difference: the counter stocks it, the storage room caps it and a raid
    /// seizes it, but it is deliberately not on the player market - the one thing you cannot do with a
    /// dose is sell it to the person it is for.
    ///
    /// Its own list rather than a special case at each call site, because "can this be put down
    /// somewhere" and "can this be listed for sale" are two questions, and the code that moves goods
    /// between a bag and a shelf was asking the wrong one - which is how the counter came to refuse to
    /// hand over a good it was advertising two lines above.
    /// </summary>
    public static readonly IReadOnlyList<string> Storable = [.. Keys, "poison"];

    public static bool IsStorable(string? key)
        => key is not null && Storable.Contains(key.Trim().ToLowerInvariant());

    public static string Normalise(string? key)
        => key?.Trim().ToLowerInvariant() ?? string.Empty;

    public static string Label(string key) => key switch
    {
        "condoms" => "Condoms",
        "beer" => "Beer",
        "medicine" => "Medicine",
        "poison" => "Poison",
        "weed" => "Weed",
        "coke" => "Coke",
        "moonshine" => "Moonshine",
        "cut" => "Cut",
        _ => WeaponTiers.IsWeapon(key) ? WeaponTiers.Label(key) : key
    };

    public static int Held(IStash player, string key) => key switch
    {
        "condoms" => player.Condoms,
        "beer" => player.Beer,
        "medicine" => player.Medicine,
        "poison" => player.Poison,
        "weed" => player.Weed,
        "coke" => player.Coke,
        "moonshine" => player.Moonshine,
        "cut" => player.Cut,
        _ => WeaponTiers.IsWeapon(key) ? player.Armoury.Of(key) : 0
    };

    /// <param name="purity">
    /// Only read for coke, and only when adding. Coke is the one good that is not interchangeable with
    /// itself: a unit is worth what it is cut with, so arriving stock has to be blended into the pile
    /// rather than counted onto it. Taking coke away leaves purity alone, since removing a share of a
    /// mixture does not change the mixture.
    /// </param>
    public static void Add(IStash player, string key, int amount, double purity = 1)
    {
        if (key == "coke" && amount > 0)
        {
            player.AddCoke(amount, purity);
            return;
        }

        // A gun goes on its own shelf, and only ever comes off the one it went on: a listing for rifles
        // that gave back pistols when it was cancelled would be a way to launder a rack downwards.
        if (WeaponTiers.IsWeapon(key))
        {
            if (amount >= 0) player.AddWeapons(key, amount);
            else player.Armoury = player.Armoury.With(key, Math.Max(0, player.Armoury.Of(key) + amount));
            return;
        }

        switch (key)
        {
            case "condoms": player.Condoms += amount; break;
            case "beer": player.Beer += amount; break;
            case "medicine": player.Medicine += amount; break;
            case "poison": player.Poison += amount; break;
            case "weed": player.Weed += amount; break;
            case "coke": player.Coke += amount; break;
            case "moonshine": player.Moonshine += amount; break;
            case "cut": player.Cut += amount; break;
        }
    }

    /// <summary>
    /// What a shelf in the hideout store holds. The player's pockets are a different and much smaller
    /// question - see <see cref="CarryCapacity"/>.
    /// </summary>
    public static int Capacity(HideoutCapacity capacity, string key) => key switch
    {
        "condoms" => capacity.MaxCondoms,
        "beer" => capacity.MaxBeer,
        "medicine" => capacity.MaxMedicine,
        "poison" => capacity.MaxPoison,
        "weed" => capacity.MaxWeed,
        "coke" => capacity.MaxCoke,
        "moonshine" => capacity.MaxMoonshine,
        "cut" => capacity.MaxCut,
        // The rack answers to its own name as well as to each gun's. Four tiers share one ceiling, so
        // there are rules - settling an overflow, clamping a seeded rival - that want to ask about the
        // whole rack at once, and without this arm they were told the shelf holds no guns at all.
        "weapons" => capacity.MaxWeapons,
        _ => WeaponTiers.IsWeapon(key) ? capacity.MaxWeapons : 0
    };

    /// <summary>
    /// How much more of a good will fit.
    ///
    /// Its own function rather than capacity-minus-held at each call site, because for guns those two
    /// numbers count different things. "How many rifles do I have" is one shelf; "how much room is there
    /// for rifles" is the whole rack, since the storage room holds one weapons count across all four
    /// tiers. Subtracting the rifles alone from the shared cap would let a player fill the shelf four
    /// times over, once per tier.
    /// </summary>
    public static int Room(IStash player, HideoutCapacity capacity, string key)
        => Room(player, Capacity(capacity, key), key);

    /// <summary>
    /// The same question against a bare ceiling, for the piles that are not the hideout store - a
    /// player's pockets, and whatever else ends up holding goods.
    /// </summary>
    public static int Room(IStash stash, int cap, string key)
    {
        var occupied = WeaponTiers.IsWeapon(key) ? stash.Weapons : Held(stash, key);
        return Math.Max(0, cap - occupied);
    }

    /// <summary>
    /// Moves as much of one good as will fit from one pile to another, and says how much went.
    ///
    /// One function because every way goods change place is this: a deposit, a withdrawal, a lab
    /// filling a shelf, a bag overflowing into the store room. Written out at each of those, the coke
    /// purity gets blended in three of them and forgotten in the fourth.
    /// </summary>
    public static int Move(IStash from, IStash to, string key, int wanted, int room)
    {
        var moved = Math.Min(Math.Min(Math.Max(0, wanted), Held(from, key)), Math.Max(0, room));
        if (moved <= 0) return 0;
        var purity = key == "coke" ? from.CokePurity : 1;
        Add(from, key, -moved);
        Add(to, key, moved, purity);
        return moved;
    }

    /// <summary>
    /// What the game itself pays or charges, as the reference a listing is judged against. Used only to
    /// keep listings inside a sane band so a fat-fingered price cannot poison the board.
    /// </summary>
    public static long ReferencePrice(GameOptions options, string key, string? city = null) => key switch
    {
        "condoms" => options.CondomPrice,
        "beer" => options.BeerPrice,
        "medicine" => options.MedicinePrice,
        "poison" => options.PoisonPrice,
        "weed" => options.CityMarkets.ProductPrice(city, "weed", options.WeedSellPrice),
        "coke" => options.CityMarkets.ProductPrice(city, "coke", options.CokeSellPrice),
        // Moonshine is judged against the shop beer it replaces, and that price is the same everywhere,
        // so it does not move with the town.
        "moonshine" => options.BeerPrice,
        // Cut is worth nothing on its own; it is worth what it stretches. Pricing it off the local coke
        // makes it follow the town without needing a band of its own.
        "cut" => Math.Max(1, options.CityMarkets.ProductPrice(city, "coke", options.CokeSellPrice) / 4),
        _ => options.WeaponTier(key)?.Price ?? 0
    };
}
