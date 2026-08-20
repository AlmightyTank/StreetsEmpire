using StreetEmpire.Api.Services;

namespace StreetEmpire.Api.Contracts;

/// <param name="District">Where to work. Null takes the neutral district.</param>
public sealed record ScoutRequest(int Turns, bool AutoBuySupplies = false, string? District = null);
public sealed record ProduceRequest(string? Product, int Turns);
public sealed record SellProductRequest(string? Product, int Quantity);
public sealed record TravelRequest(string? City);
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
    int? Weapons = null,
    /// <summary>Which attack to drive them through. Null is a raid, as it was before the menu existed.</summary>
    string? Method = null,
    int? Coke = null);
/// <summary>Null timings mean "leave as they are"; the reset flag restores the configured defaults.</summary>
public sealed record AdminBotAutomationRequest(bool Enabled, int? TickSeconds = null, int? RoundsPerTick = null, bool ResetTiming = false);
/// <summary>
/// An attack carries exactly one commanding pimp, so there is no count to send. Naming a pimp picks
/// the commander; leaving it null lets the server field the best Enforcer available.
/// </summary>
/// <param name="Method">
/// Which attack this is: a raid, or one of the quick strikes. Defaults to a raid, so every caller
/// written before the menu existed keeps asking for exactly what it always got.
/// </param>
/// <param name="Coke">Product staked on a poaching run. Ignored by every other method.</param>
public sealed record CombatAttackRequest(
    Guid DefenderId,
    int Thugs = 1,
    int Weapons = 0,
    long? CommanderPimpId = null,
    string? Method = null,
    int Coke = 0,
    /// <summary>Borrowed thugs to bring. Capped at the size of your own crew on the raid.</summary>
    int AllianceThugs = 0);

/// <summary>
/// One entry on the attack menu, priced and gated for the player looking at it. Sent from the server so
/// the client never has to know a rule: a method it cannot use arrives already carrying the reason.
/// </summary>
public sealed record AttackMethodResponse(
    string Key,
    string Label,
    int TurnCost,
    string Description,
    string? BlockedReason);

public sealed record StoreSellRequest(string? ItemKey, int Quantity);

public sealed record PrayerRequest(long Offered);

public sealed record FoundAllianceRequest(string? Name, string? Motto);
public sealed record JoinAllianceRequest(long AllianceId);
public sealed record ExpelMemberRequest(Guid MemberId);
/// <param name="Powers">
/// Minimum rank per power, keyed by power name. Partial: only the ones named are changed, so a boss
/// adjusting one line does not have to restate the other four.
/// </param>
public sealed record UpdateAllianceRequest(
    int? DuesPercent,
    string? Door,
    string? Motto,
    IReadOnlyDictionary<string, string>? Powers = null);

public sealed record SetAllianceRankRequest(Guid MemberId, string? Rank);
public sealed record HandOverAllianceRequest(Guid MemberId);
public sealed record InvitePlayerRequest(Guid PlayerId, string? Note);
public sealed record ApplyToAllianceRequest(long AllianceId, string? Note);
public sealed record AnswerAllianceRequest(long RequestId, bool Accept);

/// <summary>One outstanding ask, from whichever side it came.</summary>
public sealed record AllianceRequestResponse(
    long Id,
    string Kind,
    long AllianceId,
    string AllianceName,
    Guid PlayerId,
    string PlayerName,
    string? Note,
    /// <summary>Whether this viewer is the one who has to answer it.</summary>
    bool YoursToAnswer,
    DateTime CreatedAtUtc);

/// <summary>One of the three ways a crew can take people on.</summary>
public sealed record AllianceDoorResponse(string Door, string Label, string Detail);

/// <summary>A power, the rank it needs here, and whether the viewer has it.</summary>
public sealed record AlliancePowerResponse(string Power, string Label, string MinRank, bool YouHaveIt);
public sealed record BuyAllianceThugsRequest(string? Kind, int Quantity);

/// <summary>
/// Just enough of the crew for the pages that are not about the crew: the raid screen needs to know
/// what it can borrow, and nothing else about who you run with.
/// </summary>
public sealed record AllianceBriefResponse(
    long Id,
    string Name,
    int OffensiveThugs,
    int DefensiveThugs,
    int BorrowLimit,
    int YourDefenders);
/// <param name="Quantity">Negative sends them back to the pool.</param>
public sealed record PostDefendersRequest(int Quantity);

/// <summary>
/// One crew on the board. Worth the sum of what its members are worth, which is the source game's own
/// definition and the only one that cannot disagree with the individual leaderboard.
/// </summary>
public sealed record AllianceSummaryResponse(
    long Id,
    string Name,
    string? Motto,
    int Members,
    int MaxMembers,
    long NetWorth,
    int DuesPercent,
    int OffensiveThugs,
    int DefensiveThugs,
    /// <summary>How they take people on, and what that means in the words the board shows.</summary>
    string Door,
    string DoorLabel,
    string DoorDetail,
    bool Yours,
    bool YouFounded,
    int Rank = 0);

/// <summary>One member, as their own crew sees them.</summary>
public sealed record AllianceMemberResponse(
    Guid PlayerId,
    string Name,
    string City,
    long NetWorth,
    int Pimps,
    int Hoes,
    int Thugs,
    bool IsFounder,
    bool IsYou,
    /// <summary>Where they stand, and whether the viewer is above them.</summary>
    string Rank,
    string RankLabel,
    bool YouOutrankThem,
    /// <summary>Alliance thugs posted to their house, which came out of the shared pool.</summary>
    int Defenders,
    DateTime? JoinedAtUtc);

/// <summary>The crew page: who is in it, what it holds, and what it costs to be here.</summary>
public sealed record AllianceBoardResponse(
    AllianceSummaryResponse? Yours,
    IReadOnlyList<AllianceMemberResponse> Members,
    long Treasury,
    long FoundingCost,
    int MaxDuesPercent,
    long OffensiveThugCost,
    long DefensiveThugCost,
    /// <summary>Borrowed thugs this player may field, which is the size of their own crew.</summary>
    int BorrowLimit,
    int YourDefenders,
    /// <summary>Where the viewer stands, and what that lets them do here.</summary>
    string YourRank,
    IReadOnlyList<AlliancePowerResponse> Powers,
    IReadOnlyList<string> Ranks,
    /// <summary>The three doors a boss can pick between.</summary>
    IReadOnlyList<AllianceDoorResponse> Doors,
    /// <summary>Asks waiting on somebody: invitations to the viewer, applications to their crew.</summary>
    IReadOnlyList<AllianceRequestResponse> Requests,
    IReadOnlyList<AllianceSummaryResponse> Board);

/// <summary>
/// One place to work a shift, as the difference it makes rather than as raw numbers. The source
/// game had five of these and its own guide admits it never found a difference between any of them,
/// so what a district is worth is stated outright instead of left to be discovered.
/// </summary>
public sealed record StreetDistrictResponse(
    string Key,
    string Name,
    string Blurb,
    bool IsDefault,
    int GrossPercent,
    int HoeRecruitPercent,
    int ThugRecruitPercent,
    int PimpRecruitPercent,
    int FindPercent,
    int HeatPercent);

/// <summary>
/// The shrine as the player sees it: what the gods want this week, whether they will hear you, and what
/// giving generously would mean.
/// </summary>
public sealed record PrayerBoardResponse(
    bool CanPray,
    DateTime? NextPrayerAtUtc,
    string Good,
    string Label,
    long Quantity,
    long ApproximateValue,
    /// <summary>What the player actually holds of it, so the ask can be read against the shelf.</summary>
    long Held,
    /// <summary>Giving this much counts as generous, which is what the rarer blessings cost.</summary>
    long GenerousQuantity,
    string? BlockedReason);

/// <summary>
/// A name somebody has earned today, and what earned it. Half of these are for things done to a player
/// rather than by them, which is the source game's own reading of what a stat board is for.
/// </summary>
public sealed record PlayerTitleResponse(
    string Key,
    string Title,
    Guid PlayerId,
    string PlayerName,
    long Value,
    string Detail);

/// <summary>
/// One shelf of the gun rack, as the player sees it. Carries what a gun is worth in a fight as well as
/// what it costs, because that ratio is the entire decision and working it out from two other panels is
/// not something anyone should have to do.
/// </summary>
public sealed record WeaponTierResponse(
    string Key,
    string Label,
    int Held,
    int Price,
    double Firepower,
    /// <summary>What making one costs, or null for a gun nobody makes in a back room.</summary>
    long? ForgeCost,
    int? MinWorkshopLevel);

public sealed record MarketListRequest(string? Item, int Quantity, long PricePerUnit);
public sealed record MarketBuyRequest(long ListingId, int Quantity);
public sealed record MarketCancelRequest(long ListingId);
/// <param name="Weapon">Which gun to make. Null asks for the best the workshop can manage.</param>
public sealed record ForgeRequest(int Turns, string? Station = null, string? Weapon = null);

public sealed record MarketListingResponse(
    long Id,
    string Item,
    string ItemLabel,
    int Quantity,
    int OriginalQuantity,
    long PricePerUnit,
    string SellerName,
    bool Yours,
    long ReferencePrice,
    DateTime CreatedAtUtc);

public sealed record MarketBoardResponse(
    int HouseCutPercent,
    int MaxListingsPerPlayer,
    int YourOpenListings,
    IReadOnlyList<MarketGoodResponse> Goods,
    IReadOnlyList<MarketListingResponse> Listings);

/// <param name="Held">What the viewer has of it, so they can see what is worth listing.</param>
/// <param name="Room">Storage left, since a purchase past it is refused.</param>
public sealed record MarketGoodResponse(
    string Item,
    string Label,
    long ReferencePrice,
    int Held,
    int Room,
    long? BestPrice);

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
    CityMarketResponse CurrentMarket,
    IReadOnlyList<CityMarketResponse> CityMarkets,
    TravelStatusResponse Travel,
    long Cash,
    long BankCash,
    long NetWorth,
    int Rank,
    /// <summary>Standing among the players in your own town, and how many that is.</summary>
    int CityRank,
    int CityPlayers,
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
    /// <summary>Guns of every kind, which is the coverage number: one gun covers one thug.</summary>
    int Weapons,
    /// <summary>And which guns they are, since that is what decides a fight.</summary>
    IReadOnlyList<WeaponTierResponse> WeaponRack,
    int Medicine,
    int Rides,
    int Weed,
    int Coke,
    int Moonshine,
    int Cut,
    int WeedSellPrice,
    int CokeSellPrice,
    /// <summary>How clean the coke pile is, and what that does to its price here.</summary>
    int CokePurityPercent,
    int CokeSellPriceAtPurity,
    CrewReportResponse CrewReport,
    GuidanceResponse Guidance,
    HideoutResponse Hideout,
    IReadOnlyList<PimpResponse> Crew,
    IReadOnlyList<PimpResponse> FallenCrew,
    CombatCrewResponse CombatCrew,
    CombatStatusResponse CombatStatus,
    int UnreadDefenceAlerts,
    IReadOnlyList<StoreItemResponse> Store,
    /// <summary>The attack menu, priced and gated for this player.</summary>
    IReadOnlyList<AttackMethodResponse> AttackMethods,
    /// <summary>Where a shift can be worked, and what each place is for.</summary>
    IReadOnlyList<StreetDistrictResponse> Districts,
    /// <summary>The crew, or null when running alone.</summary>
    AllianceBriefResponse? Alliance,
    IReadOnlyList<ActivityResponse> RecentActivity);

public sealed record CityMarketResponse(
    string City,
    string Weed,
    string Coke,
    string Risk,
    int BustChancePercent,
    /// <summary>
    /// The seizure share at which this run stops being worth taking, for the load the player is
    /// actually carrying. Null when they carry nothing, or for the town they are standing in.
    /// </summary>
    int? BreakEvenSeizurePercent,
    int WeedSellPrice,
    int CokeSellPrice,
    int TravelTurns,
    bool Current);

/// <summary>
/// Travel facts that belong to the player rather than to any one town. The seizure range rides along
/// because a break-even share means nothing without knowing what a stop can actually take.
/// </summary>
public sealed record TravelStatusResponse(
    string? BlockedReason,
    long CarriedValue,
    int SeizureMinPercent,
    int SeizureMaxPercent);

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
    /// <summary>
    /// The longest shift a completely full room can supply this crew through. The second answer to
    /// outgrowing a storage room, and the one that costs nothing: a crew too big for a full-length
    /// action is usually fine on a shorter one.
    /// </summary>
    int SuppliedStreetActionTurns,
    /// <summary>
    /// What letting crew go costs in morale, per head and in total. Quoted because firing fifteen hoes
    /// is a severe hit that the button gave no hint of until after it landed.
    /// </summary>
    double FireHoeMoralePenalty,
    double FireThugMoralePenalty,
    double MaxFireMoralePenalty,
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
    /// <summary>Names they have earned today, so the list says who somebody is before you open them.</summary>
    IReadOnlyList<string> Titles,
    /// <summary>Rides parked here, since an unguarded garage is what a jacking is looking for.</summary>
    int Rides,
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
    /// <summary>What they are armed with, not merely how many. A house of rifles is a different fight.</summary>
    IReadOnlyList<WeaponTierResponse> WeaponRack,
    /// <summary>Names they have earned today. Empty for almost everybody, which is what makes them worth having.</summary>
    IReadOnlyList<string> Titles,
    int Rides,
    /// <summary>
    /// Their medicine, which decides whether infesting them would achieve anything. Public on purpose:
    /// a strike whose one counter is invisible is a coin flip, and the point of the menu is that each
    /// method is a read of what the target has left uncovered.
    /// </summary>
    int Medicine,
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

public sealed record MuleLaunchRequest(string? City, string? Good, int Hoes, long Cash, long PimpId);

public sealed record MuleQuoteRequest(string? City, string? Good, int Hoes, long Cash);

/// <summary>A run in the air, or one that has just come home. The same row serves both.</summary>
public sealed record MuleRunResponse(
    long Id,
    string DestinationCity,
    string Good,
    string Status,
    string Outcome,
    string PimpName,
    int Hoes,
    int Capacity,
    long CashSent,
    int UnitsBought,
    int SeizedUnits,
    long CashReturned,
    int BustChancePercent,
    int DefectChancePercent,
    DateTime ArrivesAtUtc,
    DateTime ReturnsAtUtc,
    int SecondsRemaining,
    string Summary);

/// <summary>A town a run could go to, priced and timed so the choice can be made without guessing.</summary>
public sealed record MuleDestinationResponse(
    string City,
    string Risk,
    int TravelTurns,
    int FlightMinutes,
    long WeedPrice,
    long CokePrice,
    int BustChancePercent);

/// <summary>A pimp who could lead a run, and what sending them would risk.</summary>
public sealed record MuleCandidateResponse(
    long Id,
    string Name,
    string Specialty,
    int Loyalty,
    bool IsAway,
    string? AwayReason);

public sealed record MuleQuoteResponse(
    string DestinationCity,
    string Good,
    int Hoes,
    int Capacity,
    int Turns,
    int FlightMinutes,
    int TripMinutes,
    long Fare,
    long Upkeep,
    long CashSent,
    long TotalCost,
    long UnitPriceThere,
    int UnitsAffordable,
    long HomePrice,
    long ProjectedGross,
    long ProjectedSpend,
    long ProjectedProfit,
    int BustChancePercent,
    int DefectChancePercent);

public sealed record MuleBoardResponse(
    int ConcurrentRunCap,
    int RunsOut,
    int IntelligenceLevel,
    int HoesAvailable,
    int MaxHoesPerRun,
    int HoeCarryCapacity,
    IReadOnlyList<MuleDestinationResponse> Destinations,
    IReadOnlyList<MuleCandidateResponse> Pimps,
    IReadOnlyList<MuleRunResponse> Runs);

/// <summary>One move worth making now, with what it costs and why it is worth it.</summary>
public sealed record NextMoveResponse(string Label, string Why, string Page, long Cost, bool Urgent);

/// <summary>A rung on the opening ladder. Done is read from the world, never stored.</summary>
public sealed record ObjectiveResponse(string Label, string Why, string Page, bool Done);

public sealed record GuidanceResponse(
    IReadOnlyList<NextMoveResponse> Moves,
    IReadOnlyList<ObjectiveResponse> Objectives,
    int ObjectivesDone,
    int ObjectivesTotal);

/// <summary>One order on a town's board, with why it cannot be filled if it cannot.</summary>
public sealed record ContractResponse(
    long Id,
    string Buyer,
    string Good,
    int Quantity,
    long PricePerUnit,
    long ListPricePerUnit,
    long Payout,
    long PremiumOverFlat,
    int? MinimumPurityPercent,
    int MinutesRemaining,
    int Held,
    /// <summary>How much is already in, how much the buyer still wants, and what finishing pays.</summary>
    int Delivered,
    int Remaining,
    long CompletionBonus,
    /// <summary>How much of the remainder this player could hand over right now.</summary>
    int CanDeliverNow,
    /// <summary>True when the player has started this one, so the board can say so.</summary>
    bool Yours,
    string? BlockedReason);

public sealed record ContractBoardResponse(string City, IReadOnlyList<ContractResponse> Contracts);

/// <summary>How much of an order to hand over. Null means as much as will go.</summary>
public sealed record DeliverContractRequest(int? Quantity);

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
    /// <summary>Garage spaces. Held by the building rather than the storage room.</summary>
    int MaxRides,
    long MaxCash,
    int MaxCondoms,
    int MaxBeer,
    int MaxWeapons,
    int MaxWeed,
    int MaxCoke,
    int MaxMoonshine,
    int MaxCut,
    int MaxMedicine,
    int WeedLabYieldBonusPercent,
    int CokeLabYieldBonusPercent,
    int WeedLabPassivePerHour,
    int CokeLabPassivePerHour,
    int MaxOfflineProductionHours,
    int IntelligenceLevel,
    int ConcurrentRunCap,
    int LookoutLevel,
    int BustRiskReductionPercent,
    double Heat,
    string HeatLabel,
    string HeatDetail,
    string HeatNote,
    HideoutRoomUpgradeResponse? StorageUpgrade,
    HideoutRoomUpgradeResponse? SafeUpgrade,
    HideoutRoomUpgradeResponse? WeedLabUpgrade,
    HideoutRoomUpgradeResponse? CokeLabUpgrade,
    HideoutRoomUpgradeResponse? IntelligenceUpgrade,
    HideoutRoomUpgradeResponse? LookoutUpgrade,
    HideoutTierUpgradeResponse? NextTier,
    HideoutBuildResponse? Building,
    IReadOnlyList<HideoutStationResponse> Stations);

/// <summary>
/// A making station: turns and materials in, one good out. Reported together because they are the
/// same shape, so the page can list them without knowing which is which.
/// </summary>
public sealed record HideoutStationResponse(
    string Key,
    string Name,
    string Good,
    int Level,
    int PerTurn,
    long CostPerUnit,
    long ComparePrice,
    string CompareLabel,
    double HeatPerUnit,
    HideoutRoomUpgradeResponse? Upgrade);

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
    /// <summary>
    /// What the guns actually carried are worth, in pistols. Shown next to the armed count because the
    /// two answer different questions: how many thugs are covered, and how hard they hit.
    /// </summary>
    double Firepower,
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
    /// <summary>
    /// The lighter shield the quick strikes set and respect. Separate from the raid shield because a
    /// player can be open to a raid and closed to another drive-by at the same time, and the recon page
    /// has to be able to say which.
    /// </summary>
    bool IsStrikeProtected,
    DateTime? StrikeProtectionUntilUtc,
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
    /// <summary>Which attack this was, so the history can say "jacking" rather than only "Victory".</summary>
    string Method,
    string MethodLabel,
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
    int HoesTaken,
    int RidesTaken,
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
    int Medicine,
    int Rides,
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
    bool IsPaused,
    bool IsInSession,
    int SessionActionsLeft,
    DateTime? NextSessionAtUtc,
    string Habits);

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
    string Method,
    string MethodLabel,
    string Outcome,
    bool HeldTheHouse,
    string Headline,
    string Detail,
    long CashLost,
    int WeedLost,
    int CokeLost,
    int ThugsLost,
    int HoesLost,
    int RidesLost,
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
