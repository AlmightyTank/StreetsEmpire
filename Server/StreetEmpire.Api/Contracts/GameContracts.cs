namespace StreetEmpire.Api.Contracts;

public sealed record ScoutRequest(int Turns);
public sealed record ProduceRequest(string? Product, int Turns);
public sealed record SellProductRequest(string? Product, int Quantity);
public sealed record StoreBuyRequest(string? ItemKey, int Quantity);
public sealed record BankRequest(long Amount);
public sealed record UpdateCrewSettingsRequest(int HoeCutPercent);

public sealed record DashboardResponse(
    Guid PlayerId,
    string Name,
    string City,
    long Cash,
    long BankCash,
    long NetWorth,
    int Rank,
    int Turns,
    int MaxTurns,
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
    IReadOnlyList<StoreItemResponse> Store,
    IReadOnlyList<ActivityResponse> RecentActivity);

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

public sealed record ActionResultResponse(
    string Summary,
    int TurnsRemaining,
    IReadOnlyDictionary<string, object?>? Breakdown = null);
