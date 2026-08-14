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
    public static readonly IReadOnlyList<string> Keys = ["condoms", "beer", "weapons", "weed", "coke"];

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
        _ => key
    };

    public static int Held(Player player, string key) => key switch
    {
        "condoms" => player.Condoms,
        "beer" => player.Beer,
        "weapons" => player.Weapons,
        "weed" => player.Weed,
        "coke" => player.Coke,
        _ => 0
    };

    public static void Add(Player player, string key, int amount)
    {
        switch (key)
        {
            case "condoms": player.Condoms += amount; break;
            case "beer": player.Beer += amount; break;
            case "weapons": player.Weapons += amount; break;
            case "weed": player.Weed += amount; break;
            case "coke": player.Coke += amount; break;
        }
    }

    public static int Capacity(HideoutCapacity capacity, string key) => key switch
    {
        "condoms" => capacity.MaxCondoms,
        "beer" => capacity.MaxBeer,
        "weapons" => capacity.MaxWeapons,
        "weed" => capacity.MaxWeed,
        "coke" => capacity.MaxCoke,
        _ => 0
    };

    /// <summary>
    /// What the game itself pays or charges, as the reference a listing is judged against. Used only to
    /// keep listings inside a sane band so a fat-fingered price cannot poison the board.
    /// </summary>
    public static long ReferencePrice(GameOptions options, string key) => key switch
    {
        "condoms" => options.CondomPrice,
        "beer" => options.BeerPrice,
        "weapons" => options.WeaponPrice,
        "weed" => options.WeedSellPrice,
        "coke" => options.CokeSellPrice,
        _ => 0
    };
}
