using StreetEmpire.Api.Services;

namespace StreetEmpire.Api.Contracts;

/// <param name="District">Where to work. Null takes the neutral district.</param>
public sealed record ScoutRequest(int Turns, bool AutoBuySupplies = false, string? District = null);

/// <param name="HoeCutPercent">
/// A cut to price instead of the saved one, so the dial can be moved before it is committed. Null
/// prices the cut the player is actually on.
/// </param>
public sealed record StreetPreviewRequest(int Turns, string? District = null, int? HoeCutPercent = null);

/// <summary>One end of the roll: the gross, who takes what out of it, and what is left.</summary>
public sealed record ShiftMoneyResponse(long Gross, long CrewCut, long Dues, long TakeHome);

/// <summary>
/// A shift priced without being worked.
///
/// The money arrives as two ends rather than one number, because the take is a per-turn roll and an
/// average is a figure half of all shifts come in under - which a player reads as being cheated. What
/// is not rolled is exact.
/// </summary>
public sealed record StreetPreviewResponse(
    int Turns,
    string District,
    int HoeCutPercent,
    int DuesPercent,
    int StreetBonusPercent,
    ShiftMoneyResponse Low,
    ShiftMoneyResponse High,
    int CondomsBurned,
    int BeerBurned,
    double Heat);
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
    IReadOnlyDictionary<string, string>? Powers = null,
    string? Name = null);

public sealed record SetAllianceRankRequest(Guid MemberId, string? Rank);
public sealed record HandOverAllianceRequest(Guid MemberId);
public sealed record InvitePlayerRequest(Guid PlayerId, string? Note);
public sealed record ApplyToAllianceRequest(long AllianceId, string? Note);
public sealed record AnswerAllianceRequest(long RequestId, bool Accept);
public sealed record AllianceTransferRequest(Guid MemberId, string? Item, int Quantity);
public sealed record AlliancePactRequest(long AllianceId);
public sealed record AnswerAlliancePactRequest(long PactId, bool Accept);
public sealed record AllianceAssistRequest(long AssistCallId, int Thugs, int Pistols, int Shotguns, int Smgs, int Rifles);

/// <summary>Asking for back whatever is left of the help you sent. The call knows how much that was.</summary>
public sealed record AllianceAssistRecallRequest(long AssistCallId);

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

public sealed record AlliancePactResponse(
    long Id,
    long RequestingAllianceId,
    string RequestingAllianceName,
    long TargetAllianceId,
    string TargetAllianceName,
    string Status,
    bool YoursToAnswer,
    DateTime CreatedAtUtc);

/// <summary>
/// A war, from the point of view of one of the two crews in it. Everything is stated from that side -
/// "your score", "theirs" - because a crew reading its own war page should never have to work out
/// which of two names it is.
/// </summary>
public sealed record AllianceWarResponse(
    long Id,
    long OpponentAllianceId,
    string OpponentName,
    /// <summary>Whether this crew is the one that declared it, which is whose stake is on the table.</summary>
    bool YouDeclared,
    string DeclaredByName,
    long Stake,
    int YourScore,
    int TheirScore,
    DateTime StartedAtUtc,
    DateTime EndsAtUtc,
    int SecondsRemaining,
    bool Settled,
    /// <summary>Null while it runs, and on a war nobody won.</summary>
    bool? YouWon,
    long Tribute,
    string? Outcome);

/// <summary>What a war costs, runs for and pays, so the page can say so before anybody commits to one.</summary>
public sealed record AllianceWarTermsResponse(
    int DurationHours,
    long Stake,
    int TributePercent,
    long MaxTribute,
    int MinScoreToWin,
    int CooldownHours,
    int PointsForRaidWon,
    int PointsForDefenceHeld,
    int PointsForGroundTaken,
    /// <summary>Whether this viewer's rank lets them declare one at all.</summary>
    bool YouCanDeclare);

public sealed record DeclareWarRequest(long AllianceId);

public sealed record AllianceAssistCallResponse(
    long Id,
    long CombatMissionId,
    long DefenderAllianceId,
    long AllyAllianceId,
    string AttackerName,
    string DefenderName,
    string DefenderAllianceName,
    string AllyAllianceName,
    string MissionStatus,
    string Status,
    int ThugsSent,
    int PistolsSent,
    int ShotgunsSent,
    int SmgsSent,
    int RiflesSent,
    int ThugsReturned,
    int PistolsReturned,
    int ShotgunsReturned,
    int SmgsReturned,
    int RiflesReturned,
    /// <summary>Whoever sent the help. Only they are offered the button to take it back.</summary>
    Guid? RespondedByPlayerId,
    DateTime CreatedAtUtc);

public sealed record AllianceTransferResponse(
    long Id,
    Guid FromPlayerId,
    string FromPlayerName,
    Guid ToPlayerId,
    string ToPlayerName,
    string Item,
    string Label,
    int Quantity,
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
    int CityControlThugs,
    IReadOnlyList<AllianceCityControlResponse> ControlledCities,
    /// <summary>Wars settled in their favour and against them. A crew has a record now.</summary>
    int WarsWon = 0,
    int WarsLost = 0,
    /// <summary>Who they are fighting right now, or null. Nobody may declare on a crew already in one.</summary>
    string? AtWarWith = null,
    int Rank = 0,
    DateTime? NameChangeReadyAtUtc = null,
    int NameChangeReadySeconds = 0);

public sealed record AllianceCityControlResponse(string City, int Territories, int BonusThugs);

/// <summary>
/// The run of the world everybody is currently in, what it has cost so far, and what this player has
/// to show for the ones before it.
/// </summary>
public sealed record SeasonResponse(
    int Number,
    string Name,
    DateTime StartedAtUtc,
    DateTime EndsAtUtc,
    int SecondsRemaining,
    /// <summary>
    /// Whether the clock actually rolls the world when it runs out. Said out loud because a countdown
    /// to nothing is worse than no countdown, and an operator who has not turned seasons on should not
    /// have their players planning around a date that will pass quietly.
    /// </summary>
    bool Enabled,
    int LengthDays,
    /// <summary>What finishing well is worth in the next one, so the climb has a stated prize.</summary>
    long ChampionHeadStart,
    long TopThreeHeadStart,
    long TopTenHeadStart,
    /// <summary>Live table for this season, ranked by raid take.</summary>
    IReadOnlyList<SeasonStandingResponse> CurrentStandings,
    /// <summary>Every season this player has finished, newest first.</summary>
    IReadOnlyList<SeasonHonourResponse> Honours,
    /// <summary>How the last one finished, top first. Empty in a world on its first season.</summary>
    IReadOnlyList<SeasonStandingResponse> LastSeason,
    string? LastSeasonName);

/// <param name="Confirm">
/// The season's own name, typed out. The one thing standing between a mis-click and every empire in
/// the world being deleted, and the reason it is the name rather than a boolean: a true is something
/// a script sends by accident, and a name is something a person has to go and read first.
/// </param>
public sealed record SeasonRollRequest(string? Confirm, string? Reason);

public sealed record SeasonHonourResponse(
    int Number,
    string Name,
    int Rank,
    long NetWorth,
    long RaidScore,
    long RaidCashTaken,
    int RaidWeedTaken,
    int RaidCokeTaken,
    string? Honour,
    DateTime? EndedAtUtc);

public sealed record SeasonStandingResponse(
    /// <summary>Who the row is, rather than what they are currently called. What "is this me?" reads.</summary>
    Guid PlayerId,
    int Rank,
    string PlayerName,
    string City,
    string? CrewName,
    long NetWorth,
    long RaidScore,
    long RaidCashTaken,
    int RaidWeedTaken,
    int RaidCokeTaken,
    string? Honour);

/// <summary>
/// One season on the shelf: what it was called, when it ran, how many were in it, who won it, and
/// where this player came in it.
///
/// The last of those is the field that makes the archive worth opening for somebody who has never
/// finished top ten. A record that only says who won is a record most people appear nowhere in.
/// </summary>
public sealed record SeasonArchiveEntryResponse(
    int Number,
    string Name,
    DateTime StartedAtUtc,
    DateTime EndsAtUtc,
    DateTime? EndedAtUtc,
    /// <summary>Whether this is the one being played. Exactly one season is ever true here.</summary>
    bool Running,
    int Players,
    string? ChampionName,
    string? ChampionCity,
    string? ChampionCrewName,
    long ChampionNetWorth,
    long ChampionRaidScore,
    long ChampionRaidCashTaken,
    int ChampionRaidWeedTaken,
    int ChampionRaidCokeTaken,
    int? YourRank,
    string? YourHonour,
    long? YourNetWorth,
    long? YourRaidScore,
    long? YourRaidCashTaken,
    int? YourRaidWeedTaken,
    int? YourRaidCokeTaken);

/// <summary>
/// How one season finished, in full - or as much of it as a page can hold.
///
/// <paramref name="You"/> is carried beside the table rather than left to be found in it, because a
/// table capped at a hundred is exactly the one somebody's own line is missing from.
/// </summary>
public sealed record SeasonTableResponse(
    int Number,
    string Name,
    DateTime StartedAtUtc,
    DateTime EndsAtUtc,
    DateTime? EndedAtUtc,
    bool Running,
    int Players,
    /// <summary>Empty for the season being played: it has no final table until it has finished.</summary>
    IReadOnlyList<SeasonStandingResponse> Table,
    SeasonStandingResponse? You);

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
    IReadOnlyList<AlliancePactResponse> Pacts,
    /// <summary>The war on right now, or null.</summary>
    AllianceWarResponse? War,
    /// <summary>What this crew has been through. The record everybody else reads them by.</summary>
    IReadOnlyList<AllianceWarResponse> WarHistory,
    AllianceWarTermsResponse WarTerms,
    IReadOnlyList<AllianceAssistCallResponse> AssistCalls,
    IReadOnlyList<AllianceTransferResponse> Transfers,
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
    int HeatPercent,
    double HeatPerTurn);

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

public sealed record CustomTitleCriteriaResponse(
    string Key,
    string Label,
    bool NeedsThreshold,
    bool NeedsText);

public sealed record AdminCustomTitleResponse(
    long Id,
    string Key,
    string Title,
    string Detail,
    string Criteria,
    long Threshold,
    string? TextValue,
    bool IsActive,
    DateTime CreatedAtUtc,
    string CreatedByUsername,
    DateTime? UpdatedAtUtc,
    string? UpdatedByUsername);

public sealed record AdminCustomTitleRequest(
    string? Key,
    string? Title,
    string? Detail,
    string? Criteria,
    long? Threshold,
    string? TextValue,
    bool? IsActive,
    string? Reason);

public sealed record ProfileBadgeResponse(
    string Key,
    string Label,
    string Detail,
    string Tone);

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
/// <summary>Which piece of ground to put the next level of work into. The level is never chosen.</summary>
public sealed record TerritoryDevelopRequest(long TerritoryId);

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
    /// <summary>How far this ground has been worked up, and what that is worth on it.</summary>
    int DevelopmentLevel,
    string DevelopmentName,
    /// <summary>What the work adds to the type's effect and to the garrison, as percentages.</summary>
    int DevelopmentEffectPercent,
    int DevelopmentDefencePercent,
    /// <summary>The next rung, or null at the top of the ladder. Only ever sent for your own ground.</summary>
    TerritoryDevelopmentUpgradeResponse? NextDevelopment,
    /// <summary>Work going on right now. Visible on anybody's ground, because it is a window.</summary>
    TerritoryDevelopmentBuildResponse? Developing,
    bool CanClaim,
    bool CanRaid,
    string? BlockedReason);

/// <summary>The next rung of the development ladder, priced and gated.</summary>
public sealed record TerritoryDevelopmentUpgradeResponse(
    int Level,
    string Name,
    long Cost,
    int Turns,
    int BuildMinutes,
    int EffectPercent,
    int DefencePercent,
    int RequiredTier,
    string RequiredTierName,
    bool TierLocked,
    /// <summary>The type's own effect at this rung, so the page can quote what it actually buys.</summary>
    int EffectNow,
    int EffectAfter);

/// <summary>Work under way on a piece of ground, and when it lands.</summary>
public sealed record TerritoryDevelopmentBuildResponse(
    int Level,
    string Name,
    DateTime CompletesAtUtc,
    int SecondsRemaining);

public sealed record TerritoryBoardResponse(
    string City,
    int Held,
    int HoldingCap,
    int MinimumGarrison,
    int MaxGarrisonThugs,
    int MaxRaidThugs,
    int ClaimTurnCost,
    int FreeThugs,
    TerritoryEffectsResponse Effects,
    AllianceCityControlResponse? AllianceCityControl,
    /// <summary>The whole development ladder, so the page can show what is ahead rather than one rung.</summary>
    IReadOnlyList<TerritoryDevelopmentRungResponse> DevelopmentLadder,
    IReadOnlyList<TerritoryResponse> Territories);

/// <summary>One rung of the ladder as the map page lists it.</summary>
public sealed record TerritoryDevelopmentRungResponse(
    int Level,
    string Name,
    long Cost,
    int Turns,
    int BuildMinutes,
    int EffectPercent,
    int DefencePercent,
    int RequiredTier,
    string RequiredTierName,
    bool Reachable);

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
    /// <summary>What a trip to the bank costs, so the panel can price the button rather than guess.</summary>
    int BankTripTurnCost,
    /// <summary>
    /// While this stands, the player is still at the counter and moves are free. Null when the next
    /// one will be charged, which is most of the time.
    /// </summary>
    DateTime? BankTripFreeUntilUtc,
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
    /// <summary>Empty when the rack has not been scouted, which the intel block distinguishes from bare.</summary>
    IReadOnlyList<WeaponTierResponse> WeaponRack,
    int Medicine,
    /// <summary>Doses on the shelf. What it costs you to infest somebody else's house.</summary>
    int Poison,
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
    /// <summary>Where this player stands with the counter, and what standing is costing or saving them.</summary>
    StoreRepResponse StoreRep,
    /// <summary>The attack menu, priced and gated for this player.</summary>
    IReadOnlyList<AttackMethodResponse> AttackMethods,
    /// <summary>Where a shift can be worked, and what each place is for.</summary>
    IReadOnlyList<StreetDistrictResponse> Districts,
    /// <summary>The crew, or null when running alone.</summary>
    AllianceBriefResponse? Alliance,
    GameUpdatesResponse Updates,
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
    int CondomsNeededPerHour,
    int BeerNeededPerHour,
    int DrugsNeededPerHour,
    double PimpHeat,
    double HoeHeat,
    double ThugHeat,
    double CrewHeat,
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
    /// <summary>What this player pays, after their standing comes off it.</summary>
    int Price,
    string Description,
    /// <summary>The sticker, before standing. Equal to <see cref="Price"/> for anybody with none.</summary>
    int ListPrice,
    /// <summary>The rung needed to be handed this. 1 is everybody.</summary>
    int MinRepLevel,
    /// <summary>What that rung is called, or null when nothing gates the row.</summary>
    string? MinRepLevelName,
    bool Locked,
    /// <summary>Why it is locked, in the same words the refusal would use. Null when it is not.</summary>
    string? LockedReason,
    /// <summary>
    /// How many the counter has left, or null where nothing is counting - the places describing the
    /// shop rather than standing in it.
    /// </summary>
    int? Available = null);

/// <summary>
/// Where a player stands with the store: what they have, what it is worth, what is above it, and what
/// money would buy of it right now.
/// </summary>
public sealed record StoreRepResponse(
    /// <summary>Whose counter this is. The shop stopped being furniture the day it got a name.</summary>
    TraderResponse Trader,
    int Rep,
    int Level,
    string LevelName,
    int DiscountPercent,
    int? NextLevel,
    string? NextLevelName,
    int? NextLevelRep,
    /// <summary>Rep still to find, or 0 at the top.</summary>
    int RepToNextLevel,
    /// <summary>How far through the current rung, for a bar. 100 at the top.</summary>
    int ProgressPercent,
    /// <summary>Dollars of trade that make one point, so the shop can say what a purchase is worth.</summary>
    int DollarsPerRep,
    /// <summary>When the counter will take another investment, and how long that is.</summary>
    DateTime? InvestmentReadyAtUtc,
    int InvestmentReadySeconds,
    IReadOnlyList<StoreInvestmentResponse> Investments);

public sealed record StoreInvestmentResponse(
    string Key,
    string Name,
    string Description,
    long Cost,
    int Rep,
    int CooldownHours,
    int MinLevel,
    string MinLevelName,
    bool Locked,
    string? LockedReason);

public sealed record StoreInvestRequest(string? Key);

/// <summary>Who runs the counter in this town, and what they say to this player.</summary>
public sealed record TraderResponse(
    string Name,
    string City,
    /// <summary>Where they trade from, in a phrase.</summary>
    string Pitch,
    /// <summary>One line in their own voice.</summary>
    string Patter,
    /// <summary>What they say to you specifically, which is what standing feels like before it unlocks anything.</summary>
    string Greeting);

/// <summary>
/// One job on the dealer's board, whoever it is actually for.
///
/// One shape where there were two. A wanted order and a contract carried the same fifteen fields under
/// different names - ShopPricePerUnit and ListPricePerUnit were the same number for the same purpose -
/// and the client drew two nearly identical rows from two nearly identical records.
/// </summary>
public sealed record TraderJobResponse(
    long Id,
    /// <summary>Which of the three slots this is sitting in, so a reroll can name it.</summary>
    int Slot,
    /// <summary>"Supply" when the dealer wants it for their own shelf, "Product" when a buyer does.</summary>
    string Kind,
    /// <summary>Who is asking. Always the town's dealer - every job on the board is theirs.</summary>
    string Buyer,
    /// <summary>Why they are asking, in their own terms. "Covering Duchess Oyelaran in Miami".</summary>
    string Reason,
    string Good,
    string GoodLabel,
    int Quantity,
    long PricePerUnit,
    /// <summary>What the same good goes for ordinarily, so the premium is legible.</summary>
    long ReferencePricePerUnit,
    long Payout,
    /// <summary>What finishing pays on top of the going rate. Never split across instalments.</summary>
    long CompletionBonus,
    int? MinimumPurityPercent,
    /// <summary>Standing for finishing it. Nothing until the last unit goes in.</summary>
    int Rep,
    int MinutesRemaining,
    int Held,
    int Delivered,
    int Remaining,
    /// <summary>How much of the remainder this player could hand over right now.</summary>
    int CanDeliverNow,
    /// <summary>Whether the bench could make this, and the room it needs when it cannot yet.</summary>
    bool CanForge,
    int? WorkshopLevelNeeded,
    /// <summary>True when this player has already put goods in, which pins it in the hand.</summary>
    bool Yours,
    string? BlockedReason);

/// <summary>What asking the dealer to look again would cost, and whether it can be afforded.</summary>
public sealed record TraderJobRerollResponse(
    /// <summary>What the next single swap costs, which is what the first tick box is worth.</summary>
    long NextCash,
    int NextRep,
    /// <summary>The cost of taking the whole hand at once, which is the three next steps together.</summary>
    long AllCash,
    int AllRep,
    /// <summary>How many have been paid for in this cycle, and when the free one comes back.</summary>
    int UsedThisCycle,
    DateTime? FreeAgainAtUtc,
    int FreeAgainSeconds,
    /// <summary>Rep above the current rung: what can be spent here without losing the rung.</summary>
    int SpendableRep);

public sealed record TraderJobBoardResponse(
    string City,
    TraderResponse Trader,
    IReadOnlyList<TraderJobResponse> Jobs,
    /// <summary>How many are going in town altogether, so the page can say how deep the book is.</summary>
    int OpenInTown,
    TraderJobRerollResponse Reroll);

public sealed record DeliverJobRequest(int? Quantity);

/// <summary>Which slots to ask again about. One, two, or the lot.</summary>
public sealed record RerollJobsRequest(IReadOnlyList<int>? Slots);

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
    /// <summary>Beside the name, so the name can be opened. A name with no id is a name that is dead text.</summary>
    Guid PlayerId,
    string PlayerName,
    string? AvatarUrl,
    string? ProfileTagline,
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
    string? AvatarUrl,
    string? ProfileTagline,
    string? ProfilePronouns,
    string? ProfileLocation,
    string ProfileAccent,
    string? PublicDiscordUsername,
    string City,
    bool IsBot,
    string? AiPersonality,
    int Rank,
    long NetWorth,
    int Pimps,
    int Hoes,
    int Thugs,
    int Weapons,
    IReadOnlyList<ProfileBadgeResponse> ProfileBadges,
    /// <summary>Names they have earned today, so the list says who somebody is before you open them.</summary>
    IReadOnlyList<string> Titles,
    /// <summary>Rides parked here, since an unguarded garage is what a jacking is looking for.</summary>
    int Rides,
    double AverageMorale,
    CombatReadinessResponse CombatReadiness,
    CombatStatusResponse CombatStatus,
    bool CanMessage,
    string? MessageBlockedReason);

public sealed record PlayerProfileResponse(
    /// <summary>
    /// Why each strike cannot be thrown at this person, keyed by method, or absent when it can. Worked
    /// out here rather than guessed at by the page, because the menu of methods is built from the
    /// attacker alone and never sees who is being looked at.
    /// </summary>
    IReadOnlyDictionary<string, string> StrikeBlockers,
    Guid PlayerId,
    string Name,
    string? AvatarUrl,
    string? ProfileTagline,
    string? ProfilePronouns,
    string? ProfileLocation,
    string ProfileAccent,
    /// <summary>A preset gradient, named. The stylesheet decides what each one looks like.</summary>
    string ProfileBanner,
    /// <summary>
    /// When they started. Public on purpose and only here, on the profile somebody chose to open,
    /// rather than on every leaderboard row - it says how long somebody has been at this, which is
    /// context for the numbers beside it.
    /// </summary>
    DateTime JoinedAtUtc,
    string? PublicDiscordUsername,
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
    IReadOnlyList<ProfileBadgeResponse> ProfileBadges,
    /// <summary>Names they have earned today. Empty for almost everybody, which is what makes them worth having.</summary>
    IReadOnlyList<string> Titles,
    int? Rides,
    /// <summary>
    /// Their medicine, which decides whether infesting them would achieve anything. Public on purpose:
    /// a strike whose one counter is invisible is a coin flip, and the point of the menu is that each
    /// method is a read of what the target has left uncovered.
    /// </summary>
    int? Medicine,
    int? Weed,
    int? Coke,
    double? HoeHappiness,
    double? ThugHappiness,
    double? AverageMorale,
    /// <summary>Null until somebody has been sent to look. Everything on it is scouted, not published.</summary>
    CombatReadinessResponse? CombatReadiness,
    CombatStatusResponse CombatStatus,
    /// <summary>What this viewer knows about this house, and how they came to know it.</summary>
    IntelResponse Intel,
    bool CanMessage,
    string? MessageBlockedReason,
    IReadOnlyList<ActivityResponse> PublicActivity,
    /// <summary>
    /// True when they have turned it off, as opposed to having done nothing yet. The page needs to tell
    /// those apart: an empty list with no explanation reads as a broken profile, and saying "they keep
    /// this private" gives away nothing they did not choose.
    /// </summary>
    bool ActivityHidden);

public sealed record HideoutUpgradeRequest(string? Room);

/// <summary>Which wrecked room to put a crew into. The same shape as an upgrade, and deliberately so.</summary>
public sealed record HideoutRepairRequest(string? Room);

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
    int SupplyTurns,
    int CondomsNeeded,
    int CondomsUsed,
    int BeerNeeded,
    int BeerUsed,
    int MoonshineUsed,
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

/// <summary>
/// Somebody the law is holding, and what it would take to get them back.
///
/// Carries the odds it happened at as well as the price, because a player who is only told they lost
/// people cannot tell a bad decision from bad luck - the same reason a mule run reports the chances it
/// ran. The seconds are sent alongside the deadline so a panel can count down without trusting the
/// clock on the machine it is drawn on.
/// </summary>
public sealed record ArrestResponse(
    long Id,
    int Hoes,
    int Thugs,
    string? PimpName,
    int Heads,
    long BailAmount,
    bool CanAffordBail,
    string City,
    string District,
    int ChancePercent,
    DateTime ArrestedAtUtc,
    DateTime BailDeadlineUtc,
    int SecondsRemaining);

/// <summary>
/// The cell. Everything being held, and what answering costs.
///
/// Bail draws on the bank first, so what a player can reach is cash and bank together rather than
/// cash on hand - quoting it against the safe would put a bond out of reach of exactly the players who
/// can plainly afford one.
/// </summary>
public sealed record ArrestBoardResponse(
    IReadOnlyList<ArrestResponse> Held,
    long TotalBail,
    long Funds,
    int BailWindowHours);

/// <summary>Who is in a cell and how long there is, for the one row that says so on the front page.</summary>
public sealed record HeldCrew(int Heads, long TotalBail, DateTime SoonestDeadlineUtc);

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
/// <summary>One line in a room, as it is shown.</summary>
public sealed record ChatMessageResponse(
    long Id,
    /// <summary>
    /// Null for anything the game said rather than a player. The room carries both, and only one of
    /// them is somebody you can go and look at.
    /// </summary>
    Guid? AuthorId,
    string Author,
    bool Yours,
    string Body,
    DateTime SentAtUtc);

/// <summary>A room the player can open, and whether they can say anything in it.</summary>
public sealed record ChatChannelResponse(string Channel, string Label, string Detail, bool CanPost, string? BlockedReason);

public sealed record ChatBoardResponse(
    string Channel,
    string Scope,
    IReadOnlyList<ChatChannelResponse> Channels,
    IReadOnlyList<ChatMessageResponse> Messages,
    int MaxLength);

public sealed record PostChatRequest(string? Channel, string? Body);

/// <summary>A conversation in the list: what it is called, who is in it, and where it got to.</summary>
public sealed record ChatConversationSummaryResponse(
    long Id,
    string Name,
    bool IsGroup,
    IReadOnlyList<string> Others,
    string LastBody,
    DateTime SentAtUtc,
    int Unread);

public sealed record ChatConversationListResponse(
    IReadOnlyList<ChatConversationSummaryResponse> Conversations,
    int Unread);

public sealed record ChatConversationResponse(
    long Id,
    string Name,
    bool IsGroup,
    IReadOnlyList<string> Others,
    IReadOnlyList<ChatMessageResponse> Messages,
    int MaxLength);

public sealed record StartGroupRequest(IReadOnlyList<Guid>? PlayerIds, string? Title);

/// <summary>Somebody the picker found.</summary>
public sealed record PersonResponse(Guid PlayerId, string Name, string City);

public sealed record PeopleSearchResponse(IReadOnlyList<PersonResponse> People);

/// <summary>Somebody this player has silenced.</summary>
public sealed record BlockedPlayerResponse(Guid PlayerId, string Name);

public sealed record BlockedListResponse(IReadOnlyList<BlockedPlayerResponse> Blocked);

public sealed record BlockRequest(Guid? PlayerId);

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
    int MaxPoison,
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
    /// <summary>What the building and its rooms are worth on the board: every pound spent on them.</summary>
    long Value,
    HideoutRoomUpgradeResponse? StorageUpgrade,
    HideoutRoomUpgradeResponse? SafeUpgrade,
    HideoutRoomUpgradeResponse? WeedLabUpgrade,
    HideoutRoomUpgradeResponse? CokeLabUpgrade,
    HideoutRoomUpgradeResponse? IntelligenceUpgrade,
    HideoutRoomUpgradeResponse? LookoutUpgrade,
    HideoutTierUpgradeResponse? NextTier,
    HideoutBuildResponse? Building,
    int CraftMinutesPerWork,
    WorkshopCraftResponse? WorkshopCraft,
    IReadOnlyList<ProductionStationResponse> Production,
    IReadOnlyList<HideoutStationResponse> Stations,
    /// <summary>
    /// Every room that is not working, and what it would take to change that. Empty for a house
    /// nobody has been through, which is most of them - the page shows nothing at all rather than a
    /// panel headed "Damage: none", because a heading that is usually empty is a heading that stops
    /// being read.
    /// </summary>
    IReadOnlyList<HideoutDamageResponse> Damage,
    /// <summary>The room the crew are in right now, or null when they are not in one.</summary>
    HideoutRepairResponse? Repair);

/// <summary>
/// A room that has been put out of action, and the bill for putting it back.
///
/// Carries what it stops as well as what it costs, because a level and a price say nothing about why
/// the mules will not leave. The whole point of breaking a room is the thing it was doing.
/// </summary>
public sealed record HideoutDamageResponse(
    string Room,
    string Name,
    string Stops,
    int Level,
    long RepairCost,
    int RepairMinutes,
    DateTime WreckedAtUtc);

/// <summary>A repair in progress. One at a time, so this is one room rather than a list.</summary>
public sealed record HideoutRepairResponse(
    string Room,
    string Name,
    DateTime CompletesAtUtc,
    int SecondsRemaining);

/// <summary>One street product recipe, priced and timed like the workshop craft rows.</summary>
public sealed record ProductionStationResponse(
    string Key,
    string Name,
    int MinPerWork,
    int MaxPerWork,
    long CostPerWork,
    long SellPrice,
    string SellLabel,
    int LabBonusPercent,
    int RequiredWorkshopLevel,
    double HeatPerUnit);

/// <summary>What the workshop has on the bench right now.</summary>
public sealed record WorkshopCraftResponse(
    long Id,
    string Good,
    string Label,
    int Quantity,
    long UnitCost,
    long TotalCost,
    int WorkUnits,
    int WorkshopLevel,
    DateTime StartedAtUtc,
    DateTime CompletesAtUtc,
    int SecondsRemaining);

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
    int RequiredWorkshopLevel,
    HideoutRoomUpgradeResponse? Upgrade);

/// <summary>The next level of a room. Null once the room is maxed out for good.</summary>
public sealed record HideoutRoomUpgradeResponse(
    int Level,
    long Cost,
    int RequiredTier,
    string RequiredTierName,
    bool TierLocked,
    int RequiredWorkshopLevel,
    bool WorkshopLocked,
    /// <summary>
    /// Days of this room's own output at the town's price before the upgrade has paid for itself, or
    /// null for a room that produces nothing to measure.
    ///
    /// Late levels are deliberately a poor return - they exist to absorb money from players who have
    /// run out of things to buy. That is a fine thing for them to be and a bad thing to be quiet
    /// about: a room priced like an investment and sold like one should say what it actually returns,
    /// so somebody buying a trophy knows that is what they are buying.
    /// </summary>
    int? PaybackDays);

public sealed record HideoutTierUpgradeResponse(
    int Level,
    string Name,
    long Cost,
    int Turns,
    int BuildMinutes,
    int MaxPimps,
    int MaxHoes,
    int MaxThugs,
    /// <summary>
    /// The turn bank the building holds. Reported because it is the half of the purchase a player
    /// cannot see anywhere else: crew caps are on the page they are hiring from, and a bank they were
    /// never told about is an upgrade that silently stops throwing their turns away.
    /// </summary>
    int MaxTurns);

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

/// <summary>
/// Why half this card is blank, in the terms the player can do something about.
/// </summary>
/// <param name="Level">What their last look was worth. Zero for never looked, or looked too long ago.</param>
/// <param name="YourCentreLevel">
/// What a fresh scout would be worth now. Said out loud because the gap between this and Level is the
/// whole argument for scouting again, and neither number means anything without the other.
/// </param>
/// <param name="GatheredAtUtc">Null when they have never looked. Stale intel keeps its date and loses its level.</param>
public sealed record IntelResponse(
    int Level,
    int YourCentreLevel,
    DateTime? GatheredAtUtc,
    bool Fresh,
    int ScoutTurnCost,
    int FreshHours);

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

public sealed record PublicStatsResponse(
    DateTime GeneratedAtUtc,
    int Players,
    int Cities,
    int Alliances,
    int TerritoriesHeld,
    int ActiveMissions,
    long TotalNetWorth,
    IReadOnlyList<PublicLeaderResponse> Leaders,
    IReadOnlyList<WorldHeadlineResponse> Headlines);

public sealed record PublicLeaderResponse(
    int Rank,
    string PlayerName,
    string City,
    long NetWorth,
    int Crew);

/// <summary>A standing fact about the world rather than a single event: who leads, who was hit hardest.</summary>
public sealed record WorldHeadlineResponse(
    string Kind,
    string Title,
    string Detail);

public sealed record WorldNewsEntryResponse(
    long Id,
    Guid PlayerId,
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

public sealed record GameAnnouncementResponse(
    long Id,
    string Title,
    string Body,
    string Category,
    string Severity,
    string? Version,
    string? ActionLabel,
    string? ActionUrl,
    bool IsPinned,
    bool ShowOnce,
    DateTime PublishedAtUtc,
    DateTime? ExpiresAtUtc,
    string? Added,
    string? Changed,
    string? Fixed,
    string? KnownIssues,
    bool IsNew);

public sealed record GameUpdatesResponse(
    IReadOnlyList<GameAnnouncementResponse> Updates,
    int UnreadCount,
    DateTime? LastSeenAtUtc);

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

public sealed record AdminBetaKeyResponse(
    Guid Id,
    string Code,
    string DisplayCode,
    string? Label,
    int MaxUses,
    int Uses,
    int UsesLeft,
    string Status,
    Guid? IssuedToAccountId,
    Guid? IssuedToPlayerId,
    string? IssuedToPlayerName,
    string? IssuedToUsername,
    Guid? RedeemedByAccountId,
    Guid? RedeemedByPlayerId,
    string? RedeemedByPlayerName,
    string? RedeemedByUsername,
    DateTime? RedeemedAtUtc,
    DateTime? ExpiresAtUtc,
    DateTime? RevokedAtUtc,
    DateTime CreatedAtUtc);

public sealed record AdminBetaKeysResponse(int Total, IReadOnlyList<AdminBetaKeyResponse> Keys);

public sealed record AdminMintBetaKeysRequest(
    int Count,
    string? Label,
    int? MaxUses,
    DateTime? ExpiresAtUtc,
    Guid? IssuedToAccountId,
    string? Reason);

/// <param name="Email">
/// The address on the account, and whether anybody proved it. A moderator looking at a returning
/// player wants to know what identity they came back on, and until this was here the panel could see a
/// username and nothing else - which is the one field a ban evader changes first.
/// </param>
/// <param name="DiscordUserId">
/// The snowflake rather than the handle. A handle is renamed in a second; the snowflake is the thing
/// that is actually the same person, and it is what a second account would have to reuse.
/// </param>
public sealed record AdminPlayerSummaryResponse(
    Guid PlayerId,
    string Name,
    string Username,
    string? Email,
    bool EmailVerified,
    string? DiscordUsername,
    string? DiscordUserId,
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

public sealed record AnnouncementDeliverySettingsResponse(
    bool DiscordConfigured,
    bool DiscordUsesStoredWebhook,
    string? DiscordWebhookHost,
    string DiscordUsername,
    DateTime UpdatedAtUtc,
    string? UpdatedBy);

public sealed record AnnouncementDeliverySettingsRequest(
    string? DiscordWebhookUrl,
    string? DiscordUsername,
    bool ClearDiscordWebhook,
    string? Reason);

public sealed record DiscordIntegrationSettingsResponse(
    bool BotConfigured,
    bool UsesStoredBotToken,
    bool SlashCommandsConfigured,
    bool RoleSyncConfigured,
    bool GatewayConnected,
    DateTime? GatewayConnectedAtUtc,
    DateTime? GatewayHeartbeatAtUtc,
    string? GatewayError,
    string? ApplicationId,
    string? GuildId,
    bool PublicKeyConfigured,
    string? LinkedRoleId,
    string? TopTenRoleId,
    string? CrewBossRoleId,
    string CityRoleMap,
    string CrewRoleMap,
    string CrewChannelMap,
    string TitleRoleMap,
    DateTime? RolesSyncedAtUtc,
    DateTime? CrewChannelsSyncedAtUtc,
    DateTime? CommandsRegisteredAtUtc,
    DateTime UpdatedAtUtc,
    string? UpdatedBy);

public sealed record DiscordIntegrationSettingsRequest(
    string? BotToken,
    string? ApplicationId,
    string? PublicKey,
    string? GuildId,
    string? LinkedRoleId,
    string? TopTenRoleId,
    string? CrewBossRoleId,
    string? CityRoleMap,
    string? CrewRoleMap,
    string? CrewChannelMap,
    string? TitleRoleMap,
    bool ClearBotToken,
    bool ClearPublicKey,
    string? Reason);

public sealed record DiscordRoleSyncResponse(
    int CheckedPlayers,
    int LinkedPlayers,
    int SyncedPlayers,
    int SkippedPlayers,
    int RolesAdded,
    int RolesRemoved,
    IReadOnlyList<string> Errors,
    DateTime SyncedAtUtc);

public sealed record DiscordRoleEnsureResponse(
    int EnsuredRoles,
    int CreatedRoles,
    int ReusedRoles,
    int CityRoles,
    int CrewRoles,
    int TitleRoles,
    IReadOnlyList<string> Errors,
    DateTime EnsuredAtUtc);

public sealed record DiscordCrewChannelSyncResponse(
    int Crews,
    int Channels,
    int CreatedChannels,
    int ReusedChannels,
    int UpdatedChannels,
    IReadOnlyList<string> Errors,
    DateTime SyncedAtUtc);

public sealed record DiscordCommandRegistrationResponse(
    int Registered,
    DateTime RegisteredAtUtc);

public sealed record AdminGameAnnouncementResponse(
    long Id,
    string Title,
    string Body,
    string Category,
    string Severity,
    string? Version,
    string? ActionLabel,
    string? ActionUrl,
    bool IsDraft,
    bool IsPinned,
    bool ShowOnce,
    bool SendToDiscord,
    DateTime? DiscordSentAtUtc,
    DateTime PublishedAtUtc,
    DateTime? ExpiresAtUtc,
    DateTime? ArchivedAtUtc,
    string? Added,
    string? Changed,
    string? Fixed,
    string? KnownIssues,
    string CreatedByUsername,
    DateTime CreatedAtUtc,
    string? UpdatedByUsername,
    DateTime? UpdatedAtUtc);

public sealed record AdminGameAnnouncementRequest(
    string? Title,
    string? Body,
    string? Category,
    string? Severity,
    string? Version,
    string? ActionLabel,
    string? ActionUrl,
    bool? IsDraft,
    bool? IsPinned,
    bool? ShowOnce,
    bool? SendToDiscord,
    DateTime? PublishedAtUtc,
    DateTime? ExpiresAtUtc,
    string? Added,
    string? Changed,
    string? Fixed,
    string? KnownIssues,
    string? Reason);

public sealed record AdminGameAnnouncementArchiveRequest(bool Archived, string? Reason);

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
