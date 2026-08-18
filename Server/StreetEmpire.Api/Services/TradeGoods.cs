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

    public static string Normalise(string? key)
        => key?.Trim().ToLowerInvariant() ?? string.Empty;

    public static string Label(string key) => key switch
    {
        "condoms" => "Condoms",
        "beer" => "Beer",
        "medicine" => "Medicine",
        "weed" => "Weed",
        "coke" => "Coke",
        "moonshine" => "Moonshine",
        "cut" => "Cut",
        _ => WeaponTiers.IsWeapon(key) ? WeaponTiers.Label(key) : key
    };

    public static int Held(Player player, string key) => key switch
    {
        "condoms" => player.Condoms,
        "beer" => player.Beer,
        "medicine" => player.Medicine,
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
    public static void Add(Player player, string key, int amount, double purity = 1)
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
            case "weed": player.Weed += amount; break;
            case "coke": player.Coke += amount; break;
            case "moonshine": player.Moonshine += amount; break;
            case "cut": player.Cut += amount; break;
        }
    }

    public static int Capacity(HideoutCapacity capacity, string key) => key switch
    {
        "condoms" => capacity.MaxCondoms,
        "beer" => capacity.MaxBeer,
        "medicine" => capacity.MaxMedicine,
        "weed" => capacity.MaxWeed,
        "coke" => capacity.MaxCoke,
        "moonshine" => capacity.MaxMoonshine,
        "cut" => capacity.MaxCut,
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
    public static int Room(Player player, HideoutCapacity capacity, string key)
    {
        var occupied = WeaponTiers.IsWeapon(key) ? player.Weapons : Held(player, key);
        return Math.Max(0, Capacity(capacity, key) - occupied);
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
