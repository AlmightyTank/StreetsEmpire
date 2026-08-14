using StreetEmpire.Api.Services;

namespace StreetEmpire.Api.Contracts;

public sealed record ScoutRequest(int Turns, bool AutoBuySupplies = false);
public sealed record ProduceRequest(string? Product, int Turns);
public sealed record SellProductRequest(string? Product, int Quantity);
public sealed record StoreBuyRequest(string? ItemKey, int Quantity);
public sealed record BankRequest(long Amount);
public sealed record UpdateCrewSettingsRequest(int HoeCutPercent);
public sealed record CrewRequest(string? Role, int Quantity);
public sealed record MoraleRecoveryRequest(string? Strategy);
public sealed record AdminSeedBotsRequest(int Count);
public sealed record AdminRunBotsRequest(int Rounds);
public sealed record AdminBotPauseRequest(bool Paused);

/// <summary>
/// Drives one rival through a chosen action rather than letting its brain pick. Every field is
/// optional because each action needs a different few; the endpoint validates what its action needs.
/// </summary>
public sealed record AdminBotActionRequest(
    string? Action,
    int? Turns = null,
    string? Product = null,
    string? Item = null,
    string? Role = null,
    int? Quantity = null,
    long? Amount = null,
    string? Strategy = null,
    string? Room = null,
    Guid? DefenderId = null,
    int? Thugs = null,
    int? Weapons = null);
/// <summary>Null timings mean "leave as they are"; the reset flag restores the configured defaults.</summary>
public sealed record AdminBotAutomationRequest(bool Enabled, int? TickSeconds = null, int? RoundsPerTick = null, bool ResetTiming = false);
/// <summary>
/// An attack carries exactly one commanding pimp, so there is no count to send. Naming a pimp picks
/// the commander; leaving it null lets the server field the best Enforcer available.
/// </summary>
public sealed record CombatAttackRequest(Guid DefenderId, int Thugs = 1, int Weapons = 0, long? CommanderPimpId = null);

public sealed record TerritoryClaimRequest(long TerritoryId, int Thugs, long? PimpId = null);
public sealed record TerritoryGarrisonRequest(long TerritoryId, int Thugs, long? PimpId = null);
public sealed record TerritoryRaidRequest(long TerritoryId, int Thugs, int Weapons = 0, long? CommanderPimpId = null);

/// <summary>One piece of ground, as the map page shows it.</summary>
public sealed record TerritoryResponse(
    long Id,
    string Name,
    string City,
    string Type,
    string TypeLabel,
    string Effect,
    Guid? HolderId,
    string? HolderName,
    bool HeldByYou,
    int GarrisonThugs,
    string? GarrisonPimpName,
    int GarrisonBonusPercent,
    DateTime? HeldSinceUtc,
    bool IsProtected,
    DateTime? ProtectedUntilUtc,
    bool CanClaim,
    bool CanRaid,
    string? BlockedReason);

public sealed record TerritoryBoardResponse(
    string City,
    int Held,
    int HoldingCap,
    int MinimumGarrison,
    int ClaimTurnCost,
    int FreeThugs,
    TerritoryEffectsResponse Effects,
    IReadOnlyList<TerritoryResponse> Territories);

public sealed record TerritoryEffectsResponse(
    int StreetIncomePercent,
    int ProductionYieldPercent,
    int MoraleRecoveryPercent,
    int LootPercent);

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
    MoraleTrendResponse MoraleTrend,
    int Condoms,
    int Beer,
    int Weapons,
    int Weed,
    int Coke,
    int WeedSellPrice,
    int CokeSellPrice,
    CrewReportResponse CrewReport,
    HideoutResponse Hideout,
    IReadOnlyList<PimpResponse> Crew,
    IReadOnlyList<PimpResponse> FallenCrew,
    CombatCrewResponse CombatCrew,
    CombatStatusResponse CombatStatus,
    int UnreadDefenceAlerts,
    IReadOnlyList<StoreItemResponse> Store,
    IReadOnlyList<ActivityResponse> RecentActivity);

public sealed record CrewReportResponse(
    int ManagementCapacity,
    int UnmanagedHoes,
    int ArmedThugs,
    int UncoveredThugs,
    int CondomsNeededForMaxStreetAction,
    int BeerNeededForMaxStreetAction,
    /// <summary>
    /// How much crew a completely full storage room can carry through a full-length action. This is a
    /// harder limit than what a player currently holds: past it they cannot buy their way out, and
    /// every shift runs a shortage until the room itself is bigger.
    /// </summary>
    int HoesStorageCanSupply,
    int ThugsStorageCanSupply,
    /// <summary>The storage level that would cover the crew, or null when the room already does.</summary>
    int? StorageLevelToSupplyCrew,
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

public sealed record HideoutUpgradeRequest(string? Room);

public sealed record PimpResponse(
    long Id,
    string Name,
    string Specialty,
    int BonusPercent,
    double Loyalty,
    int MissionsLed,
    int Victories,
    bool IsCommanding,
    DateTime HiredAtUtc,
    DateTime? LostAtUtc,
    string? LostReason);

public sealed record HideoutResponse(
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
    int WeedLabYieldBonusPercent,
    int CokeLabYieldBonusPercent,
    int WeedLabPassivePerHour,
    int CokeLabPassivePerHour,
    int MaxOfflineProductionHours,
    HideoutRoomUpgradeResponse? StorageUpgrade,
    HideoutRoomUpgradeResponse? SafeUpgrade,
    HideoutRoomUpgradeResponse? WeedLabUpgrade,
    HideoutRoomUpgradeResponse? CokeLabUpgrade,
    HideoutTierUpgradeResponse? NextTier,
    HideoutBuildResponse? Building);

/// <summary>The next level of a room. Null once the room is maxed out for good.</summary>
public sealed record HideoutRoomUpgradeResponse(
    int Level,
    long Cost,
    int RequiredTier,
    string RequiredTierName,
    bool TierLocked);

public sealed record HideoutTierUpgradeResponse(
    int Level,
    string Name,
    long Cost,
    int Turns,
    int BuildMinutes,
    int MaxPimps,
    int MaxHoes,
    int MaxThugs);

/// <summary>A tier build in progress. The hideout keeps its old caps until this lands.</summary>
public sealed record HideoutBuildResponse(
    int Tier,
    string Name,
    DateTime CompletesAtUtc,
    int SecondsRemaining);

/// <summary>
/// Which way crew morale is moving, measured from the player's most recent action to now. Null means
/// there is nothing recent enough to compare against, which is a different thing from steady and is
/// shown differently.
/// </summary>
public sealed record MoraleTrendResponse(
    double? HoeDelta,
    double? ThugDelta,
    string HoeDirection,
    string ThugDirection,
    int WindowHours);

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
    string Eligibility,
    string? MismatchReason);

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
    string? CommanderName,
    int CommanderBonusPercent,
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
    int LootMultiplierPercent,
    int DefenderRecentHits,
    int DefenderProtectionMinutes,
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

/// <summary>
/// What happened while the player was away. <see cref="HasNews"/> is what the client gates the popup
/// on: a summary that says nothing happened is not worth interrupting anyone for.
/// </summary>
public sealed record CatchUpResponse(
    DateTime SinceUtc,
    int AwayMinutes,
    bool HasNews,
    IReadOnlyList<CatchUpItemResponse> Items);

/// <param name="Tone">good, bad, or neutral. Styling only; the wording carries the meaning.</param>
public sealed record CatchUpItemResponse(
    string Kind,
    string Headline,
    string Detail,
    string Tone);

public sealed record WorldNewsResponse(
    IReadOnlyList<WorldHeadlineResponse> Headlines,
    IReadOnlyList<WorldNewsEntryResponse> Feed);

/// <summary>A standing fact about the world rather than a single event: who leads, who was hit hardest.</summary>
public sealed record WorldHeadlineResponse(
    string Kind,
    string Title,
    string Detail);

public sealed record WorldNewsEntryResponse(
    long Id,
    string PlayerName,
    string City,
    string Action,
    string Category,
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
    int RoundsPerTick,
    int DefaultTickSeconds,
    int DefaultRoundsPerTick,
    int MinTickSeconds,
    int MaxTickSeconds,
    int MinRoundsPerTick,
    int MaxRoundsPerTick);

// ----- Admin panel -----

public sealed record AdminAdjustRequest(string? Resource, long Delta, string? Reason);
public sealed record AdminMoraleRequest(double Morale, string? Reason);
public sealed record AdminEnforcementRequest(string? Action, DateTime? UntilUtc, string? Reason);
public sealed record AdminSetAdminRequest(bool IsAdmin, string? Reason);
public sealed record AdminRenameRequest(string? Name, string? Reason);
public sealed record AdminReasonRequest(string? Reason);

public sealed record AdminPlayerSummaryResponse(
    Guid PlayerId,
    string Name,
    string Username,
    string City,
    bool IsBot,
    bool IsAdmin,
    bool IsBanned,
    DateTime? SuspendedUntilUtc,
    string? EnforcementReason,
    long NetWorth,
    long Cash,
    long BankCash,
    int Turns,
    int Pimps,
    int Hoes,
    int Thugs,
    DateTime CreatedAtUtc);

public sealed record AdminPlayerDetailResponse(
    AdminPlayerSummaryResponse Summary,
    int Condoms,
    int Beer,
    int Weapons,
    int Weed,
    int Coke,
    double HoeHappiness,
    double ThugHappiness,
    int HoeCutPercent,
    DateTime? LastAttackAtUtc,
    DateTime? LastAttackedAtUtc,
    DateTime? CombatProtectionUntilUtc,
    HideoutResponse Hideout,
    IReadOnlyList<PimpResponse> Crew,
    IReadOnlyList<ActivityResponse> RecentActivity,
    IReadOnlyList<AdminAuditEntryResponse> AuditTrail,
    IReadOnlyList<string> AdjustableResources);

public sealed record AdminAuditEntryResponse(
    long Id,
    string ActorUsername,
    string Action,
    Guid? TargetPlayerId,
    string? TargetName,
    string Summary,
    string? Reason,
    DateTime CreatedAtUtc);

public sealed record AdminWealthBandResponse(string Label, int Players, long TotalNetWorth);

public sealed record AdminMoverResponse(
    Guid PlayerId,
    string Name,
    bool IsBot,
    long NetWorth,
    long CashGained24h,
    int ActionsLast24h);

public sealed record AdminMissionResponse(
    long MissionId,
    string AttackerName,
    string DefenderName,
    string? CommanderName,
    string Status,
    string Outcome,
    int CurrentRound,
    int MaxRounds,
    DateTime StartedAtUtc,
    DateTime? NextEventAtUtc,
    bool IsOverdue);

public sealed record AdminBotHealthResponse(
    Guid PlayerId,
    string Name,
    string Personality,
    long NetWorth,
    DateTime? LastActionAtUtc,
    int MinutesIdle,
    bool IsPaused);

public sealed record AdminOversightResponse(
    long MedianNetWorth,
    long TopNetWorth,
    double GiniPercent,
    IReadOnlyList<AdminWealthBandResponse> WealthBands,
    IReadOnlyList<AdminMoverResponse> FastestMovers,
    IReadOnlyList<AdminMissionResponse> ActiveMissions,
    IReadOnlyList<AdminBotHealthResponse> Bots);

public sealed record AdminLiveOpsRequest(bool? MaintenanceMode, string? MaintenanceMessage, string? Announcement, string? Reason);

public sealed record LiveOpsResponse(
    bool MaintenanceMode,
    string? MaintenanceMessage,
    string? Announcement,
    DateTime UpdatedAtUtc,
    string? UpdatedBy);

public sealed record AdminConfigChangeRequest(string? Path, string? Value, string? Reason);

public sealed record AdminConfigEntryResponse(
    string Path,
    string Type,
    string EffectiveValue,
    string? OverrideValue,
    bool IsOverridden);

public sealed record AdminConfigResponse(
    int Version,
    int OverrideCount,
    IReadOnlyList<AdminConfigEntryResponse> Settings);

public sealed record DefenceAlertResponse(
    long Id,
    string AttackerName,
    string Outcome,
    bool HeldTheHouse,
    string Headline,
    string Detail,
    long CashLost,
    int WeedLost,
    int CokeLost,
    int ThugsLost,
    int PimpsLost,
    bool IsUnread,
    DateTime CreatedAtUtc);

/// <summary>
/// Anything that happened to a player rather than because of them: raids, lab output, a build landing.
/// Passive lab output used to sit in the activity list, which is a record of what the player did, so a
/// payout they had no hand in read as an action they took.
/// </summary>
/// <param name="Id">Namespaced by source, since combat logs and action logs number independently.</param>
public sealed record AlertResponse(
    string Id,
    string Kind,
    string Headline,
    string Detail,
    string Tone,
    bool IsUnread,
    DateTime CreatedAtUtc);

public sealed record AlertsResponse(
    int UnreadCount,
    DateTime? LastSeenAtUtc,
    IReadOnlyList<AlertResponse> Alerts);
