namespace StreetEmpire.Api.Models;

public sealed class CustomTitle
{
    public long Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string Criteria { get; set; } = CustomTitleCriteria.NetWorthAtLeast;
    public long Threshold { get; set; }
    public string? TextValue { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedByUsername { get; set; } = string.Empty;
    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedByUsername { get; set; }
}

public static class CustomTitleCriteria
{
    public const string NetWorthAtLeast = "net-worth-at-least";
    public const string CashAtLeast = "cash-at-least";
    public const string BankCashAtLeast = "bank-cash-at-least";
    public const string PimpsAtLeast = "pimps-at-least";
    public const string HoesAtLeast = "hoes-at-least";
    public const string ThugsAtLeast = "thugs-at-least";
    public const string RidesAtLeast = "rides-at-least";
    public const string WeaponsAtLeast = "weapons-at-least";
    public const string CityIs = "city-is";
    public const string CrewIs = "crew-is";
    public const string CrewBoss = "crew-boss";
    public const string TopTen = "top-ten";
    public const string DiscordConnected = "discord-connected";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        NetWorthAtLeast,
        CashAtLeast,
        BankCashAtLeast,
        PimpsAtLeast,
        HoesAtLeast,
        ThugsAtLeast,
        RidesAtLeast,
        WeaponsAtLeast,
        CityIs,
        CrewIs,
        CrewBoss,
        TopTen,
        DiscordConnected
    };
}
