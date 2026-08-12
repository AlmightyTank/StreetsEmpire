using StreetEmpire.Api.Services;

namespace StreetEmpire.Api.Contracts;

public sealed record ScoutRequest(int Turns);
public sealed record ProduceRequest(string? Product, int Turns);
public sealed record SellProductRequest(string? Product, int Quantity);
public sealed record StoreBuyRequest(string? ItemKey, int Quantity);
public sealed record BankRequest(long Amount);
public sealed record UpdateCrewSettingsRequest(int HoeCutPercent);
public sealed record CrewRequest(string? Role, int Quantity);
public sealed record MoraleRecoveryRequest(string? Strategy);
public sealed record AdminCheatRequest(string? Cheat, long Amount);
public sealed record AdminSeedBotsRequest(int Count);
public sealed record AdminRunBotsRequest(int Rounds);
public sealed record AdminBotAutomationRequest(bool Enabled);
public sealed record CombatAttackRequest(Guid DefenderId, int Pimps = 1, int Thugs = 1, int Weapons = 0);

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
    CombatCrewResponse CombatCrew,
    CombatStatusResponse CombatStatus,
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
    double MinThugMoraleToHire,
    int HqRestTurnCost,
    long HqRestCashCost,
    double HqRestMoraleGain,
    int HqPartyTurnCost,
    long HqPartyCashCost,
    int HqPartyBeerCost,
    int HqPartyWeedCost,
    double HqPartyHoeMoraleGain,
    double HqPartyThugMoraleGain);

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

public sealed record PlayerTargetResponse(
    Guid PlayerId,
    string Name,
    string City,
    bool IsBot,
    string? AiPersonality,
    int Rank,
    long NetWorth,
    int Pimps,
    int Hoes,
    int Thugs,
    int Weapons,
    double AverageMorale,
    CombatReadinessResponse CombatReadiness,
    CombatStatusResponse CombatStatus);

public sealed record PlayerProfileResponse(
    Guid PlayerId,
    string Name,
    string City,
    bool IsBot,
    string? AiPersonality,
    int Rank,
    long NetWorth,
    long Cash,
    long BankCash,
    int Pimps,
    int Hoes,
    int Thugs,
    int Weapons,
    int Weed,
    int Coke,
    double HoeHappiness,
    double ThugHappiness,
    double AverageMorale,
    CombatReadinessResponse CombatReadiness,
    CombatStatusResponse CombatStatus,
    IReadOnlyList<ActivityResponse> PublicActivity);

public sealed record CombatReadinessResponse(
    int AttackPower,
    int DefensePower,
    int ArmedThugs,
    int UncoveredThugs,
    double WeaponCoveragePercent,
    double AverageMorale,
    string RiskBand);

public sealed record CombatCrewResponse(
    int CommittedPimps,
    int CommittedThugs,
    int CommittedWeapons,
    int AvailablePimps,
    int AvailableThugs,
    int AvailableWeapons,
    int ActiveAttackMissions,
    int MaxActiveAttackMissions);

public sealed record CombatStatusResponse(
    bool IsProtected,
    DateTime? ProtectionUntilUtc,
    DateTime? LastAttackAtUtc,
    DateTime? LastAttackedAtUtc,
    DateTime? AttackCooldownUntilUtc,
    bool CanAttackNow,
    int AttackTurnCost,
    int RecentAttacksMade,
    int RecentDefenses,
    string Eligibility);

public sealed record CombatLogResponse(
    long Id,
    Guid AttackerId,
    string AttackerName,
    Guid DefenderId,
    string DefenderName,
    string Outcome,
    string Summary,
    int TurnsSpent,
    int AttackerPower,
    int DefenderPower,
    long CashStolen,
    int WeedStolen,
    int CokeStolen,
    int AttackerPimpsLost,
    int AttackerHoesLost,
    int AttackerThugsLost,
    int AttackerWeaponsLost,
    int DefenderPimpsLost,
    int DefenderHoesLost,
    int DefenderThugsLost,
    int DefenderWeaponsLost,
    DateTime? DefenderProtectionUntilUtc,
    DateTime? ResolvesAtUtc,
    DateTime? ResolvedAtUtc,
    DateTime CreatedAtUtc);

public sealed record CombatMissionResponse(
    long Id,
    Guid AttackerId,
    string AttackerName,
    Guid DefenderId,
    string DefenderName,
    string Status,
    string Outcome,
    string Summary,
    int TurnsSpent,
    int AssignedPimps,
    int AssignedThugs,
    int AssignedWeapons,
    int RemainingAttackers,
    int RemainingWeapons,
    double AttackerMorale,
    double DefenderMorale,
    int CurrentRound,
    int MaxRounds,
    int AttackerPower,
    int DefenderPower,
    long CashStolen,
    int WeedStolen,
    int CokeStolen,
    DateTime StartedAtUtc,
    DateTime ArrivesAtUtc,
    DateTime? NextRoundAtUtc,
    DateTime? ReturnsAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? DefenderProtectionUntilUtc,
    bool CanCancel,
    long CancelCashCost,
    IReadOnlyList<CombatMissionEventResponse> Events);

public sealed record CombatMissionEventResponse(
    long Id,
    int Round,
    string Kind,
    string Summary,
    double AttackRoll,
    double DefenseRoll,
    double AttackerMorale,
    double DefenderMorale,
    int AttackerThugsLost,
    int DefenderThugsLost,
    int AttackerWeaponsLost,
    int DefenderWeaponsLost,
    DateTime CreatedAtUtc);

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
