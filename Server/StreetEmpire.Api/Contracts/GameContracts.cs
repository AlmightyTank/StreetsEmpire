using StreetEmpire.Api.Services;

namespace StreetEmpire.Api.Contracts;

public sealed record ScoutRequest(int Turns);
public sealed record ProduceRequest(string? Product, int Turns);
public sealed record SellProductRequest(string? Product, int Quantity);
public sealed record StoreBuyRequest(string? ItemKey, int Quantity);
public sealed record BankRequest(long Amount);
public sealed record UpdateCrewSettingsRequest(int HoeCutPercent);
public sealed record CrewRequest(string? Role, int Quantity);
public sealed record AdminCheatRequest(string? Cheat, long Amount);
public sealed record AdminSeedBotsRequest(int Count);
public sealed record AdminRunBotsRequest(int Rounds);
public sealed record AdminBotAutomationRequest(bool Enabled);

public sealed record DashboardResponse(
    Guid PlayerId,
    string Name,
    bool IsAdmin,
    string City,
    long Cash,
    long BankCash,
    long NetWorth,
    int Rank,
    int Turns,
    int MaxTurns,
    int MaxActionTurns,
    int TurnsPerTick,
    int TurnTickMinutes,
    int SecondsUntilNextTurnTick,
    int Pimps,
    int Hoes,
    int Thugs,
    int HoeCutPercent,
    double HoeHappiness,
    double ThugHappiness,
    int Condoms,
    int Beer,
    int Weapons,
    int Weed,
    int Coke,
    int WeedSellPrice,
    int CokeSellPrice,
    CrewReportResponse CrewReport,
    IReadOnlyList<StoreItemResponse> Store,
    IReadOnlyList<ActivityResponse> RecentActivity);

public sealed record CrewReportResponse(
    int ManagementCapacity,
    int UnmanagedHoes,
    int ArmedThugs,
    int UncoveredThugs,
    int CondomsNeededForMaxStreetAction,
    int BeerNeededForMaxStreetAction,
    long CondomCostForMaxStreetAction,
    long BeerCostForMaxStreetAction,
    long SupplyCostForMaxStreetAction,
    int HirePimpCost,
    int HireHoeCost,
    int HireThugCost,
    double MinHoeMoraleToHire,
    double MinThugMoraleToHire);

public sealed record StoreItemResponse(
    string Key,
    string Name,
    string Category,
    int Price,
    string Description);

public sealed record ActivityResponse(
    long Id,
    string Action,
    string Summary,
    int TurnsSpent,
    long CashDelta,
    long BankDelta,
    DateTime CreatedAtUtc);

public sealed record LeaderboardEntryResponse(
    int Rank,
    string PlayerName,
    string City,
    long NetWorth,
    long Cash,
    long BankCash,
    int Pimps,
    int Hoes,
    int Thugs);

public sealed record WorldNewsEntryResponse(
    long Id,
    string PlayerName,
    string City,
    string Action,
    string Summary,
    int TurnsSpent,
    DateTime CreatedAtUtc);

public sealed record ActionResultResponse(
    string Summary,
    int TurnsRemaining,
    IReadOnlyDictionary<string, object?>? Breakdown = null);

public sealed record AdminOverviewResponse(
    DateTime GeneratedAtUtc,
    int TotalAccounts,
    int AdminAccounts,
    int BotAccounts,
    int TotalPlayers,
    long TotalCashOnHand,
    long TotalBankCash,
    long TotalLiquidCash,
    long TotalNetWorth,
    int TotalTurnsBanked,
    double AverageHoeMorale,
    double AverageThugMorale,
    BotAutomationStatusResponse BotAutomation,
    GameOptions Economy);

public sealed record BotAutomationStatusResponse(
    bool Enabled,
    int TickSeconds,
    int RoundsPerTick);
