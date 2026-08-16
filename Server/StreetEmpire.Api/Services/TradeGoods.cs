using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

/// <summary>
/// The one place that knows how a good's key maps to the column holding it and the cap on that column.
///
/// The store, production, admin adjustments and now the market all move the same five piles around.
/// Written out separately in each, a good lands in one place and not another, and a market that can
/// take goods it cannot give back is worse than no market.
/// </summary>
public static class TradeGoods
{
    public static readonly IReadOnlyList<string> Keys = ["condoms", "beer", "weapons", "weed", "coke", "moonshine", "cut"];

    public static bool IsTradeable(string? key)
        => key is not null && Keys.Contains(key.Trim().ToLowerInvariant());

    public static string Normalise(string? key)
        => key?.Trim().ToLowerInvariant() ?? string.Empty;

    public static string Label(string key) => key switch
    {
        "condoms" => "Condoms",
        "beer" => "Beer",
        "weapons" => "Weapons",
        "weed" => "Weed",
        "coke" => "Coke",
        "moonshine" => "Moonshine",
        "cut" => "Cut",
        _ => key
    };

    public static int Held(Player player, string key) => key switch
    {
        "condoms" => player.Condoms,
        "beer" => player.Beer,
        "weapons" => player.Weapons,
        "weed" => player.Weed,
        "coke" => player.Coke,
        "moonshine" => player.Moonshine,
        "cut" => player.Cut,
        _ => 0
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

        switch (key)
        {
            case "condoms": player.Condoms += amount; break;
            case "beer": player.Beer += amount; break;
            case "weapons": player.Weapons += amount; break;
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
        "weapons" => capacity.MaxWeapons,
        "weed" => capacity.MaxWeed,
        "coke" => capacity.MaxCoke,
        "moonshine" => capacity.MaxMoonshine,
        "cut" => capacity.MaxCut,
        _ => 0
    };

    /// <summary>
    /// What the game itself pays or charges, as the reference a listing is judged against. Used only to
    /// keep listings inside a sane band so a fat-fingered price cannot poison the board.
    /// </summary>
    public static long ReferencePrice(GameOptions options, string key, string? city = null) => key switch
    {
        "condoms" => options.CondomPrice,
        "beer" => options.BeerPrice,
        "weapons" => options.WeaponPrice,
        "weed" => options.CityMarkets.ProductPrice(city, "weed", options.WeedSellPrice),
        "coke" => options.CityMarkets.ProductPrice(city, "coke", options.CokeSellPrice),
        // Moonshine is judged against the shop beer it replaces, and that price is the same everywhere,
        // so it does not move with the town.
        "moonshine" => options.BeerPrice,
        // Cut is worth nothing on its own; it is worth what it stretches. Pricing it off the local coke
        // makes it follow the town without needing a band of its own.
        "cut" => Math.Max(1, options.CityMarkets.ProductPrice(city, "coke", options.CokeSellPrice) / 4),
        _ => 0
    };
}
