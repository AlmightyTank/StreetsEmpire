using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Configuration;
using StreetEmpire.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Models;
using StreetEmpire.Api.Endpoints;
using StreetEmpire.Api.Services;
using StreetEmpire.Api.Support;
using static StreetEmpire.Api.Mapping.ResponseMappers;
using static StreetEmpire.Api.Support.BotSeeding;

var tests = new (string Name, Action Test)[]
{
    ("net worth includes all liquid and inventory value", NetWorthIncludesAllValue),
    ("net worth expression agrees with the net worth calculation", NetWorthExpressionAgreesWithCalculation),
    ("a hideout is worth every pound that built it", AHideoutIsWorthWhatItCost),
    ("both worth expressions survive the trip to sql", WorthExpressionsTranslateToSql),
    ("building is free on the board and invisible to a raid", BuildingMovesNeitherRankNorTheOddsOfAFight),
    ("ranking breaks net worth ties by oldest player", RankingBreaksTiesByOldestPlayer),
    ("ranks-above predicate agrees with in-memory ranking", RanksAbovePredicateAgreesWithInMemoryRanking),
    ("turn refresh catches up without exceeding cap", TurnRefreshCatchesUp),
    ("turn refresh passively recovers morale", TurnRefreshPassivelyRecoversMorale),
    ("street action returns deterministic tuned breakdown", StreetActionBreakdownIsDeterministic),
    ("production uses configured tables", ProductionUsesConfiguredTables),
    ("invalid product is a rule error", InvalidProductIsRuleError),
    ("crew report calculates operating requirements", CrewReportCalculatesRequirements),
    ("hire crew spends cash and respects morale gates", HireCrewSpendsCashAndChecksMorale),
    ("fire crew updates counts and morale", FireCrewUpdatesCountsAndMorale),
    ("trap house recovery spends resources and boosts morale", TrapHouseRecoverySpendsResourcesAndBoostsMorale),
    ("street auto-buy tops up upkeep within cash and storage", StreetAutoBuyToppsUpWithinLimits),
    ("supply shortage costs a share of upkeep, not a flat charge per unit", ShortagePenaltyScalesWithTheShareMissed),
    ("crew report names the storage level a crew actually needs", CrewReportNamesTheStorageLevelNeeded),
    ("pimp roster stays in step with the pimp counter", PimpRosterStaysInStepWithCounter),
    ("pimp specialties bonus the right activity", PimpSpecialtiesBonusTheRightActivity),
    ("pimp commander selection honours the request", PimpCommanderSelectionHonoursRequest),
    ("pimp commander dies on a bad defeat", PimpCommanderDiesOnDefeat),
    ("pimp walks out when loyalty bottoms out", PimpWalksOutWhenLoyaltyBottomsOut),
    ("hideout caps crew hiring at the tier limit", HideoutCapsCrewHiring),
    ("hideout blocks store buys that would overflow storage", HideoutBlocksOverflowingStoreBuys),
    ("everything on the counter can actually be bought", EverythingOnTheCounterCanBeBought),
    ("everything you can hold is worth something", EverythingYouCanHoldIsWorthSomething),
    ("the bench never makes an attack cheaper than its answer", DefenceIsNeverDearerThanAttack),
    ("hideout banks cash over the safe and spills goods", HideoutBanksCashOverSafeAndSpillsGoods),
    ("city markets change product sale prices", CityMarketsChangeProductSalePrices),
    ("travel changes city and spends the town's distance", TravelChangesCityAndSpendsTheTownsDistance),
    ("a stopped run takes a share of the load but never the bank", StoppedRunTakesAShareOfTheLoadButNeverTheBank),
    ("a small load is not worth stopping", SmallLoadIsNotWorthStopping),
    ("break-even seizure is priced against what the player carries", BreakEvenSeizureIsPricedAgainstWhatThePlayerCarries),
    ("hideout grandfathers stock a player already held", HideoutGrandfathersExistingStock),
    ("hideout lab raises production yield", HideoutLabRaisesProductionYield),
    ("trade goods map keys to the piles they move", TradeGoodsMapKeysToPiles),
    ("workshop makes weapons under the store price", WorkshopMakesWeaponsUnderStorePrice),
    ("moonshine drinks like beer and cut stretches coke", ContrabandGoodsDoTheirJob),
    ("heat comes from what you hold and what you do", HeatDrivesTheBust),
    ("stepping on coke turns cut into more of it", CuttingStretchesWhatYouAlreadyHold),
    ("purity makes stretching a trade rather than a printer", PurityStopsTheCokePrinter),
    ("guidance names the move and the ladder reads the world", GuidancePointsAtTheGame),
    ("turns come back faster while you are small", EarlyGameTurnsTaper),
    ("the first tier always has something worth saving for", TheFirstTierHasNoDeadZone),
    ("a crew too big for a full shift is told the shorter one", ShortShiftsAreASupplyAnswer),
    ("territory effects add up across the ground held", TerritoryEffectsAddUp),
    ("a pimp posted to ground only helps if they fight", GarrisonPimpBonusOnlyForEnforcers),
    ("ground bonuses reach the activities they boost", TerritoryBonusesReachTheirActivities),
    ("hideout tier build charges up front and lands on time", HideoutTierBuildChargesUpFrontAndLandsOnTime),
    ("hideout tier gates the rooms it is too small to hold", HideoutTierGatesDeeperRooms),
    ("storage levels hold a full action at the crew caps they unlock", StorageLevelsMatchTheCrewCapsTheyUnlock),
    ("a crew is capped by the store as well as the building", CrewIsCappedByWhicheverRunsOutFirst),
    ("the settings the server actually ships obey the same rules", ShippedSettingsObeyTheSameRules),
    ("every test written is a test that runs", EveryTestWrittenIsATestThatRuns),
    ("every hideout upgrade in the shipped tables can be paid for", EveryHideoutUpgradeIsReachable),
    ("labs produce while away, bounded by storage and the offline ceiling", LabsProduceWhileAway),
    ("labs start their clock when built rather than backdating", LabsStartTheirClockWhenBuilt),
    ("world news keeps fights and drops routine noise", WorldNewsKeepsFightsAndDropsNoise),
    ("morale trend reports direction and admits when it cannot", MoraleTrendReportsDirection),
    ("account lockout blocks banned and suspended players", AccountLockoutBlocksBannedAndSuspended),
    ("wealth stats describe the distribution", WealthStatsDescribeTheDistribution),
    ("option paths discover and write scalar tuning", OptionPathsDiscoverAndWriteScalars),
    ("option overrides layer over appsettings values", OptionOverridesLayerOverAppsettings),
    ("anti-farm refuses mismatched fights", AntiFarmRefusesMismatchedFights),
    ("anti-farm decays loot for repeat victories", AntiFarmDecaysRepeatLoot),
    ("anti-farm widens protection under repeated hits", AntiFarmWidensProtection),
    ("bot targeting picks the richest beatable target", BotTargetingPicksRichestBeatable),
    ("bot attack profiles scale with personality", BotAttackProfilesScaleWithPersonality),
    ("bot mule appetite follows what each rival is for", BotMuleProfilesScaleWithPersonality),
    ("rivals remember who robbed them", BotsHoldGrudges),
    ("the news names who started the feud", FeudsNameTheAggressor),
    ("every town has ground, a price and a name of its own", EveryCityIsRealAndDistinct),
    ("a watchful town notices the same operation sooner", CityRiskReachesTheDailyLoop),
    ("a buyer with a deadline pays over the counter", ContractsAreDemandWithAShape),
    ("a room only ever fails towards the loudest one", ChatFailsTowardsTheOpenRoom),
    ("a direct message is addressed, never posted", ADirectMessageIsAddressedNeverPosted),
    ("blocking silences somebody without shielding you from them", BlockingIsChatAndNotCover),
    ("a conversation is who is in it, whether that is two or twelve", AConversationIsWhoIsInIt),
    ("an order goes in as fast as the room allows", AnOrderGoesInAsFastAsTheRoomAllows),
    ("rivals keep their own hours and play in sittings", BotSchedulesLookLikePeople),
    ("a mule run is gated, priced and frozen at launch", MuleRunsArePricedAndFrozen),
    ("a mule run settles three ways and never twice", MuleRunsSettleThreeWays),
    ("a player in the air cannot act", TravelIsAFlightYouCannotActFrom),
    ("defence alerts flip the outcome to the defender's view", DefenceAlertsFlipPerspective),
    ("catch-up reports what happened while away and stays quiet otherwise", CatchUpReportsWhatHappenedWhileAway),
    ("catch-up reports rank moves and who changed places with you", CatchUpReportsRankAndRivals),
    ("defence alerts count only what is unread", DefenceAlertsCountUnread),
    ("combat power keeps a defender edge without killing attacks", CombatPowerBalanceTarget),
    ("combat blocks self attacks", CombatBlocksSelfAttacks),
    ("combat blocks protected defenders", CombatBlocksProtectedDefenders),
    ("combat start creates pending mission", CombatStartCreatesPendingMission),
    ("combat commitment calculates available crew", CombatCommitmentCalculatesAvailableCrew),
    ("combat schedule gate never runs late", CombatScheduleGateNeverRunsLate),
    ("combat mission cancel price scales by status", CombatMissionCancelPriceScalesByStatus),
    ("combat mission launch respects the attacker cooldown", CombatMissionLaunchRespectsAttackerCooldown),
    ("combat victory steals cash and product without touching bank", CombatVictoryStealsCashAndProductWithoutTouchingBank),
    ("combat attack spends turns and creates log", CombatAttackSpendsTurnsAndCreatesLog),
    ("the attack menu prices every method and says what is missing", AttackMenuPricesEveryMethod),
    ("a drive-by thins the guard and risks the car", DriveByThinsTheGuard),
    ("a jacking is decided by the guard on the garage", JackingIsDecidedByTheGuard),
    ("a jacking reads the guards' guns as well as their number", JackingReadsTheGuardsGunsAsWellAsTheirNumber),
    ("a drive-by weighs bodies for the hit and guns for the car", DriveByWeighsBodiesAndGunsDifferently),
    ("the gods ask for something specific and the ask holds all week", TheGodsAskForSomethingSpecific),
    ("praying is answered, and can never pay", PrayingIsAnsweredAndNeverPays),
    ("titles name the leader of each category, both ways round", TitlesNameLeadersBothWaysRound),
    ("every district is worth going to for something", DistrictsAreWorthChoosingBetween),
    ("a crew is people who have agreed not to rob each other", AllianceIsATruce),
    ("dues come off the gross beside the crew cut, and never compound", DuesComeOffTheGross),
    ("the pool amplifies a crew rather than replacing one", PoolAmplifiesRatherThanReplaces),
    ("rank decides what a member may do, and to whom", RanksGatePowersAndPeople),
    ("a boss draws the lines every other rank runs under", BossDrawsTheLines),
    ("the door is one setting with three states", TheDoorIsOneSettingWithThreeStates),
    ("medicine is the answer to an infestation", MedicineAnswersAnInfestation),
    ("poison is what an infestation costs to throw", PoisonIsWhatAnInfestationCosts),
    ("a well-paid house cannot be poached at any price", PayoutAnswersPoaching),
    ("the two shields keep their own clocks", StrikeAndRaidShieldsAreSeparate),
    ("the chop shop sells rides and buys them back for less", ChopShopBuysBackUnderTheSticker),
    ("defence alerts name the strike that hit you", DefenceAlertsNameTheStrike),
    ("a strike says no before the click, not after it", AStrikeRefusesBeforeTheClick),
    ("a pistol fights exactly as the one weapon used to", PistolsReproduceTheOldWeapon),
    ("any gun covers a thug, but only the good ones fight", CoverageAndFirepowerComeApart),
    ("a crew carries the best guns and drops the worst", CrewsCarryTheBestAndDropTheWorst),
    ("one shelf holds every gun", OneShelfHoldsEveryGun),
    ("better guns cost more per point than more thugs", TradingUpIsForWhenTheHouseIsFull),
    ("an email is a second name, not a message", AnEmailIsASecondNameNotAMessage),
    ("both doors put down exactly the same player", BothDoorsPutDownTheSamePlayer),
    ("an account can never end up with no way in", AnAccountAlwaysKeepsAWayIn),
    ("a Discord handle always suggests a usable username", ADiscordHandleAlwaysSuggestsAUsableUsername),
    ("a Discord ticket cannot be read, forged, or replayed elsewhere", ADiscordTicketCannotBeForged),
    ("Discord is off until it is configured", DiscordIsOffUntilItIsConfigured),
    ("the browser only ever comes back somewhere already trusted", ReturnUrlsAreOnlyEverOnesAlreadyTrusted),
    ("a session watermark is measured in whole seconds, like the cookie it judges", SessionWatermarksAreMeasuredInWholeSeconds),
    ("an address and its tick always move together", AnAddressAndItsTickMoveTogether),
    ("a verification code lives minutes, not hours", AVerificationCodeLivesMinutesNotHours),
    ("mail is off until it is configured, and says so rather than pretending", MailIsOffUntilItIsConfigured),
    ("the code email carries the code and escapes the name", TheCodeEmailCarriesTheCodeAndEscapesTheName),
    ("an email is only a second name for the password door", AnEmailIsOnlyASecondNameForThePasswordDoor),
    ("the resend cooldown guards an inbox, not an account", TheResendCooldownGuardsAnInboxNotAnAccount),
    ("a .env file reads the way every other one does", ADotEnvFileReadsTheWayEveryOtherOneDoes),
    ("the real environment always beats the file", TheRealEnvironmentAlwaysBeatsTheFile),
    ("the committed example holds no secrets and no surprises", TheCommittedExampleHoldsNoSecrets),
    ("a code is only ever good for the thing it was sent for", ACodeIsOnlyGoodForWhatItWasSentFor),
    ("a reset needs an address somebody actually proved", AResetNeedsAProvenAddress),
    ("an account made through Discord is a whole account", ADiscordSignUpIsAWholeAccount),
    ("every account change worth a notice has copy of its own", EveryAccountChangeHasCopyOfItsOwn),
    ("a notice says what happened and never what it was", ANoticeSaysWhatHappenedAndNeverWhatItWas),
    ("notices only ever go to an address somebody proved they own", NoticesOnlyGoToProvenAddresses),
    ("the address being left behind is the one that gets told", TheAddressBeingLeftBehindGetsTold)
};

var failed = 0;
foreach (var (name, test) in tests)
{
    try
    {
        test();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.Error.WriteLine($"FAIL {name}: {ex.Message}");
    }
}

if (failed > 0)
    Environment.Exit(1);

static void NetWorthIncludesAllValue()
{
    var service = CreateEconomy(new GameOptions
    {
        CondomPrice = 10,
        BeerPrice = 15,
        WeaponPrice = 500,
        PimpNetWorth = 1_000,
        HoeNetWorth = 550,
        ThugNetWorth = 1_250,
        WeedNetWorth = 30,
        CokeNetWorth = 120,
        // A ride counts at what the chop shop pays, not what it cost, or buying one would be a free
        // climb up the board.
        RideNetWorth = 15_000,
        MedicineNetWorth = 250,
        // A rack is worth the shop price of each gun on it, so the sum has to know the tiers.
        Weapons =
        [
            new WeaponTierOptions { Key = WeaponTiers.Pistol, Price = 250, Firepower = 1 },
            new WeaponTierOptions { Key = WeaponTiers.Rifle, Price = 5_500, Firepower = 2.5 }
        ]
    });
    var player = new Player
    {
        Cash = 100,
        BankCash = 200,
        Pimps = 1,
        Hoes = 2,
        Thugs = 3,
        Condoms = 4,
        Beer = 5,
        Pistols = 6,
        Rifles = 2,
        Medicine = 2,
        Rides = 1,
        Weed = 7,
        Coke = 8
    };

    // Six pistols at $250 and two rifles at $5,500: a rack is worth what is on it, not what it counts.
    AssertEqual(35_435, service.CalculateNetWorth(player));
}

// The database sorts and counts by the expression while the API reports the method's value, so a
// change to one that misses the other would silently disagree with the leaderboard.
static void NetWorthExpressionAgreesWithCalculation()
{
    var service = CreateEconomy();
    var players = new[]
    {
        new Player(),
        new Player { Cash = 5_000, Pimps = 1, Hoes = 3, Thugs = 1, Condoms = 25, Beer = 12, Pistols = 1 },
        new Player
        {
            Cash = 1_234,
            BankCash = 98_765,
            Pimps = 7,
            Hoes = 41,
            Thugs = 19,
            Condoms = 310,
            Beer = 225,
            Pistols = 17,
            Weed = 88,
            Coke = 46
        }
    };

    var compiled = service.NetWorthExpression.Compile();
    foreach (var player in players)
        AssertEqual(service.CalculateNetWorth(player), compiled(player));

    // Every one of those has no hideout, so the building half of the sum was riding along untested.
    // Both halves have to agree, at every shape of house, or the board ranks by one number while the
    // player is shown another.
    var housed = new[]
    {
        new Player { Cash = 1_000, Hideout = new Hideout() },
        new Player { Cash = 1_000, Hideout = new Hideout { Tier = 2, StorageLevel = 3, SafeLevel = 2 } },
        new Player { Cash = 1_000, Hideout = new Hideout { Tier = 1, UpgradingToTier = 2 } },
        new Player
        {
            Cash = 9_999,
            Hoes = 12,
            Hideout = new Hideout
            {
                Tier = 4, StorageLevel = 6, SafeLevel = 5, WeedLabLevel = 3, CokeLabLevel = 2,
                WorkshopLevel = 2, IntelligenceLevel = 2, LookoutLevel = 2
            }
        }
    };

    var plunder = service.PlunderExpression.Compile();
    foreach (var player in housed)
    {
        AssertEqual(service.CalculateNetWorth(player), compiled(player));
        AssertEqual(service.CalculatePlunder(player), plunder(player));
        AssertTrue(service.CalculateNetWorth(player) >= service.CalculatePlunder(player),
            "a building can only ever add");
    }
}

// The building was the one thing a player owned that counted for nothing, so the single largest
// investment in the game made your standing worse the moment you made it.
// Every other test here compiles the expression and runs it in memory, which proves the arithmetic and
// nothing about the half that matters: these two sums exist as expression trees so the database can
// rank by them. A tree that will not translate does not fail quietly - it throws on the leaderboard,
// for every player, the first time anybody looks at it. Translating without connecting is enough to
// catch that, and keeps this project free of a live database.
static void WorthExpressionsTranslateToSql()
{
    var options = new DbContextOptionsBuilder<GameDbContext>()
        .UseNpgsql("Host=localhost;Database=translation_only;Username=none")
        .Options;
    using var db = new GameDbContext(options);
    var economy = CreateEconomy();

    // Ordering by it is what the leaderboard does; filtering by it is what target-finding does.
    var ranked = db.Players.OrderByDescending(economy.NetWorthExpression).ToQueryString();
    var filtered = db.Players.Where(economy.NetWorthAtLeast(25_000)).ToQueryString();
    var plunder = db.Players.OrderByDescending(economy.PlunderExpression).ToQueryString();
    var plunderFiltered = db.Players.Where(economy.PlunderAtLeast(25_000)).ToQueryString();

    AssertTrue(ranked.Length > 0 && filtered.Length > 0, "the ranking sums translate");
    AssertTrue(plunder.Length > 0 && plunderFiltered.Length > 0, "the raid sums translate");

    // The building half has to reach the database as a join onto the hideout, not as something quietly
    // evaluated on the client - which would rank the whole table in memory.
    AssertTrue(ranked.Contains("Hideouts", StringComparison.OrdinalIgnoreCase),
        "net worth ranks on the hideout in the database");

    // And the raid sum must not touch it at all, or every target query pays for a join it never reads.
    AssertTrue(!plunder.Contains("Hideouts", StringComparison.OrdinalIgnoreCase),
        "what a raid can take owes nothing to the building");
}

static void AHideoutIsWorthWhatItCost()
{
    var options = Resolve(null);
    var config = options.Hideout;

    // A hideout nobody has spent anything on is worth nothing, so a new player is not handed a number.
    AssertEqual(0L, HideoutValue.Of(new Hideout(), options));
    AssertEqual(0L, HideoutValue.Of(null, options));

    // Worth exactly the upgrades bought, at cost.
    var moved = new Hideout { Tier = 2, StorageLevel = 3, SafeLevel = 1 };
    var expected = config.Tiers.Where(x => x.Level <= 2).Sum(x => x.UpgradeCost)
                 + config.Storage.Where(x => x.Level <= 3).Sum(x => x.UpgradeCost);
    AssertEqual(expected, HideoutValue.Of(moved, options));

    // A tier being built is a tier already paid for, so it counts from the moment the money goes.
    // Otherwise a player drops down the board for the length of the build and climbs back afterwards.
    var building = new Hideout { Tier = 1, UpgradingToTier = 2 };
    AssertEqual(config.Tiers.Single(x => x.Level == 2).UpgradeCost, HideoutValue.Of(building, options));

    // Every room in the price list is counted. Reflected over rather than listed, because the rooms
    // are enumerated by hand in half a dozen switches and three of them were missed on the first pass
    // here - a room added later would be missed again, and the only sign would be a quietly cheap
    // hideout. Maxing everything has to come to every pound the config can charge.
    var maxed = new Hideout
    {
        Tier = config.Tiers.Max(x => x.Level),
        StorageLevel = config.Storage.Max(x => x.Level),
        SafeLevel = config.Safe.Max(x => x.Level),
        WeedLabLevel = config.WeedLab.Max(x => x.Level),
        CokeLabLevel = config.CokeLab.Max(x => x.Level),
        WorkshopLevel = config.Workshop.Max(x => x.Level),
        IntelligenceLevel = config.Intelligence.Max(x => x.Level),
        LookoutLevel = config.Lookout.Max(x => x.Level)
    };

    var everyPound = 0L;
    var roomLists = 0;
    foreach (var property in typeof(HideoutOptions).GetProperties())
    {
        if (property.GetValue(config) is not System.Collections.IEnumerable rows || rows is string) continue;
        var counted = false;
        foreach (var row in rows)
        {
            var cost = row.GetType().GetProperty("UpgradeCost");
            if (cost is null) break;
            everyPound += Convert.ToInt64(cost.GetValue(row));
            counted = true;
        }
        if (counted) roomLists++;
    }

    // Eight since the still and the mix house were folded into the workshop. The number is here to
    // catch reflection finding nothing at all, not to pin the room count.
    AssertTrue(roomLists >= 8, $"every room list should be found, saw {roomLists}");
    AssertEqual(everyPound, HideoutValue.Of(maxed, options));
}

// Two promises at once: buying a room does not move you on the board, and it does not change who you
// are allowed to fight.
static void BuildingMovesNeitherRankNorTheOddsOfAFight()
{
    var options = Resolve(null);
    var economy = CreateEconomy(options);
    var hideouts = CreateHideouts(options);
    var tier2 = options.Hideout.Tiers.Single(x => x.Level == 2);

    var player = new Player
    {
        Cash = tier2.UpgradeCost + 50_000,
        Turns = tier2.UpgradeTurns + 5,
        Hoes = 4,
        Hideout = new Hideout { StorageLevel = 2 }
    };

    var worthBefore = economy.CalculateNetWorth(player);
    var plunderBefore = economy.CalculatePlunder(player);
    var buildingBefore = HideoutValue.Of(player.Hideout, options);

    hideouts.Upgrade(player, "tier", DateTime.UtcNow);

    // The cash is gone and the building is worth what it cost, so the two cancel exactly. Upgrading is
    // not a way up the board, and it is no longer a way down it either.
    AssertEqual(worthBefore, economy.CalculateNetWorth(player));
    AssertEqual(buildingBefore + tier2.UpgradeCost, HideoutValue.Of(player.Hideout, options));

    // A raid cannot carry off a building, so what a fight weighs went down by exactly the cash spent.
    // Anything else would let a player change who may attack them by buying rooms.
    AssertEqual(plunderBefore - tier2.UpgradeCost, economy.CalculatePlunder(player));
    AssertTrue(economy.CalculatePlunder(player) < economy.CalculateNetWorth(player),
        "the building counts for the board and not for the fight");

    // And the gate itself reads the takeable figure, so a mansion cannot make a pauper untouchable.
    var mansion = new Player { Cash = 30_000, Hideout = new Hideout { Tier = 4, StorageLevel = 6, SafeLevel = 5 } };
    var neighbour = new Player { Cash = 30_000, Hideout = new Hideout() };
    AssertEqual(EconomyService.PlunderOf(mansion, options), EconomyService.PlunderOf(neighbour, options));
    AssertTrue(EconomyService.NetWorthOf(mansion, options) > EconomyService.NetWorthOf(neighbour, options),
        "the board still knows which of the two built something");
    AssertEqual(
        AntiFarm.RejectReason(EconomyService.PlunderOf(neighbour, options), EconomyService.PlunderOf(mansion, options), options.AntiFarm),
        AntiFarm.RejectReason(EconomyService.PlunderOf(neighbour, options), EconomyService.PlunderOf(neighbour, options), options.AntiFarm));
}


static void RankingBreaksTiesByOldestPlayer()
{
    var richer = new PlayerStanding(9_000, new DateTime(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc));
    var older = new PlayerStanding(5_000, new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));
    var newer = new PlayerStanding(5_000, new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc));
    var contenders = new[] { richer, older, newer };

    AssertEqual(1, EconomyService.RankOf(richer, contenders));
    AssertEqual(2, EconomyService.RankOf(older, contenders));
    AssertEqual(3, EconomyService.RankOf(newer, contenders));

    // A page member is never counted as outranking itself.
    AssertEqual(1, EconomyService.RankOf(richer, [richer]));
}

static void RanksAbovePredicateAgreesWithInMemoryRanking()
{
    var service = CreateEconomy();
    var created = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc);
    var subject = new Player { Cash = 5_000, Thugs = 2, CreatedAtUtc = created };
    var standing = new PlayerStanding(service.CalculateNetWorth(subject), created);
    var predicate = service.RanksAbove(standing.NetWorth, standing.CreatedAtUtc).Compile();
    var candidates = new[]
    {
        subject,
        new Player { Cash = 50_000, CreatedAtUtc = created.AddDays(5) },
        new Player { Cash = 10, CreatedAtUtc = created.AddDays(-5) },
        new Player { Cash = 5_000, Thugs = 2, CreatedAtUtc = created.AddMinutes(-1) },
        new Player { Cash = 5_000, Thugs = 2, CreatedAtUtc = created.AddMinutes(1) }
    };

    foreach (var candidate in candidates)
    {
        var contender = new PlayerStanding(service.CalculateNetWorth(candidate), candidate.CreatedAtUtc);
        AssertEqual(EconomyService.Outranks(contender, standing), predicate(candidate));
    }
}

static void TurnRefreshCatchesUp()
{
    var service = CreateTurns(new GameOptions
    {
        TurnsPerTick = 2,
        TurnTickMinutes = 10,
        MaxTurns = 20,
        // Held flat: this is about catching up whole ticks and stopping at the cap, and the
        // early-game taper is covered on its own elsewhere.
        EarlyGameTurnBoost = 1
    });
    var player = new Player
    {
        Turns = 10,
        LastTurnUpdateUtc = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc)
    };

    var changed = service.Refresh(player, new DateTime(2026, 8, 10, 0, 31, 0, DateTimeKind.Utc));

    AssertTrue(changed, "turn refresh should report a change");
    AssertEqual(16, player.Turns);
    AssertEqual(new DateTime(2026, 8, 10, 0, 30, 0, DateTimeKind.Utc), player.LastTurnUpdateUtc);
}

static void TurnRefreshPassivelyRecoversMorale()
{
    var service = CreateTurns(new GameOptions
    {
        TurnsPerTick = 2,
        TurnTickMinutes = 10,
        MaxTurns = 20,
        Morale = new MoraleOptions { PassiveRecoveryPerTick = 0.5 }
    });
    var player = new Player
    {
        Turns = 20,
        HoeHappiness = 40,
        ThugHappiness = 50,
        LastTurnUpdateUtc = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc)
    };

    var changed = service.Refresh(player, new DateTime(2026, 8, 10, 0, 30, 0, DateTimeKind.Utc));

    AssertTrue(changed, "capped players should still recover morale over time");
    AssertEqual(20, player.Turns);
    AssertEqual(41.5, player.HoeHappiness);
    AssertEqual(51.5, player.ThugHappiness);
}

static void StreetActionBreakdownIsDeterministic()
{
    var service = CreateEconomy(new GameOptions
    {
        MaxActionTurns = 20,
        StreetAction = new StreetActionOptions
        {
            BaseGrossPerTurn = 10,
            HoeGrossPerTurn = new RangeOptions(2, 2),
            PimpGrossPerTurn = new RangeOptions(1, 1),
            PimpRecruitChance = 0,
            HoeRecruitChance = 0,
            ThugRecruitChance = 0,
            Finds = NoFinds()
        },
        Morale = new MoraleOptions
        {
            HoesManagedPerPimp = 10,
            TurnsPerCondom = 1,
            TurnsPerBeer = 1,
            HoeStreetWorkGainPerTurn = 0,
            ThugStreetWorkGainPerTurn = 0,
            HoeCutMoraleScalePerTurn = 0,
            CondomShortagePenalty = 0,
            BeerShortagePenalty = 0,
            UnmanagedHoePenalty = 0,
            UncoveredThugPenalty = 0,
            DesertionThreshold = 0,
            MaxDesertionChance = 0
        }
    });
    var player = new Player
    {
        Turns = 10,
        Cash = 100,
        Pimps = 1,
        Hoes = 2,
        Thugs = 1,
        Condoms = 10,
        Beer = 10,
        Pistols = 1,
        HoeCutPercent = 25,
        HoeHappiness = 50,
        ThugHappiness = 50
    };

    var result = service.Scout(player, 2);
    var breakdown = RequiredBreakdown(result);

    AssertEqual(8, player.Turns);
    AssertEqual(122, player.Cash);
    AssertEqual(6, player.Condoms);
    AssertEqual(8, player.Beer);
    AssertEqual(30L, Value<long>(breakdown, "gross"));
    AssertEqual(8L, Value<long>(breakdown, "crewPayout"));
    AssertEqual(22L, Value<long>(breakdown, "playerProfit"));
    AssertEqual(4, Value<int>(breakdown, "condomsUsed"));
    AssertEqual(2, Value<int>(breakdown, "beerUsed"));
}

static void ProductionUsesConfiguredTables()
{
    var service = CreateEconomy(new GameOptions
    {
        MaxActionTurns = 20,
        Production = new ProductionOptions
        {
            Weed = new ProductProductionOptions(7, 4, 4)
        }
    });
    var player = new Player { Turns = 5, Cash = 100 };

    var result = service.Produce(player, "weed", 3);
    var breakdown = RequiredBreakdown(result);

    AssertEqual(2, player.Turns);
    AssertEqual(79, player.Cash);
    AssertEqual(12, player.Weed);
    AssertEqual(12, Value<int>(breakdown, "unitsProduced"));
    AssertEqual(21L, Value<long>(breakdown, "totalCost"));

    var queued = new Player { Turns = 5, Cash = 100, Hideout = new Hideout { StorageLevel = 2, WorkshopLevel = 1 } };
    var queuedAt = new DateTime(2026, 8, 24, 2, 0, 0, DateTimeKind.Utc);
    var craft = service.StartProductionCraft(queued, "weed", 3, null, queuedAt);
    AssertEqual(2, queued.Turns);
    AssertEqual(79, queued.Cash);
    AssertEqual(0, queued.Weed);
    AssertEqual(12, craft.Quantity);
    AssertEqual(queuedAt.AddMinutes(3 * craft.WorkUnits), craft.CompletesAtUtc);
    service.CompleteCraft(queued, craft, craft.CompletesAtUtc);
    AssertEqual(12, queued.Weed);
    AssertRuleError(() => service.StartProductionCraft(new Player { Turns = 5, Cash = 100, Hideout = new Hideout { StorageLevel = 2 } }, "weed", 3, null, queuedAt),
        "queued production without a workshop");
    AssertRuleError(() => service.StartProductionCraft(new Player { Turns = 2, Cash = 100, Hideout = new Hideout { StorageLevel = 2 } }, "weed", 3, null, queuedAt),
        "queued production without enough turns");
}

static void InvalidProductIsRuleError()
{
    var service = CreateEconomy();
    var player = new Player { Turns = 5, Cash = 100 };

    try
    {
        service.Produce(player, null, 1);
    }
    catch (GameRuleException)
    {
        return;
    }

    throw new InvalidOperationException("Expected GameRuleException.");
}

static void CrewReportCalculatesRequirements()
{
    var service = CreateEconomy(new GameOptions
    {
        MaxActionTurns = 20,
        CondomPrice = 10,
        BeerPrice = 15,
        Morale = new MoraleOptions
        {
            HoesManagedPerPimp = 10,
            TurnsPerCondom = 10,
            TurnsPerBeer = 5
        },
        Crew = new CrewOptions
        {
            HirePimpCost = 2_500,
            HireHoeCost = 750,
            HireThugCost = 1_500,
            MinHoeMoraleToHire = 35,
            MinThugMoraleToHire = 40
        }
    });
    var player = new Player { Pimps = 2, Hoes = 23, Thugs = 7, Pistols = 5 };

    var report = service.GetCrewReport(player);

    AssertEqual(20, report.ManagementCapacity);
    AssertEqual(3, report.UnmanagedHoes);
    AssertEqual(5, report.ArmedThugs);
    AssertEqual(2, report.UncoveredThugs);
    AssertEqual(46, report.CondomsNeededForMaxStreetAction);
    AssertEqual(28, report.BeerNeededForMaxStreetAction);
    AssertEqual(880L, report.SupplyCostForMaxStreetAction);
}

static void HireCrewSpendsCashAndChecksMorale()
{
    var service = CreateEconomy(new GameOptions
    {
        Crew = new CrewOptions
        {
            HireHoeCost = 100,
            MinHoeMoraleToHire = 35
        }
    });
    var player = new Player { Cash = 500, Turns = 5, HoeHappiness = 40 };

    var result = service.HireCrew(player, "hoes", 3);
    var breakdown = RequiredBreakdown(result);

    AssertEqual(200, player.Cash);
    AssertEqual(3, player.Hoes);
    AssertEqual(300L, Value<long>(breakdown, "totalCost"));

    player.HoeHappiness = 20;
    try
    {
        service.HireCrew(player, "hoes", 1);
    }
    catch (GameRuleException)
    {
        return;
    }

    throw new InvalidOperationException("Expected low-morale hire failure.");
}

static void FireCrewUpdatesCountsAndMorale()
{
    var service = CreateEconomy(new GameOptions
    {
        Crew = new CrewOptions
        {
            FireThugMoralePenalty = 2,
            MaxFireMoralePenalty = 25
        }
    });
    var player = new Player { Pimps = 1, Thugs = 5, ThugHappiness = 80 };

    service.FireCrew(player, "thugs", 3);

    AssertEqual(2, player.Thugs);
    AssertEqual(74.0, player.ThugHappiness);

    try
    {
        service.FireCrew(player, "pimps", 1);
    }
    catch (GameRuleException)
    {
        return;
    }

    throw new InvalidOperationException("Expected last-pimp fire failure.");
}

static void TrapHouseRecoverySpendsResourcesAndBoostsMorale()
{
    var service = CreateEconomy(new GameOptions
    {
        Morale = new MoraleOptions
        {
            HqPartyTurnCost = 2,
            HqPartyCashPerCrew = 10,
            HqPartyBeerPerThug = 2,
            HqPartyWeedPerHoes = 4,
            HqPartyHoeMoraleGain = 12,
            HqPartyThugMoraleGain = 10
        }
    });
    var player = new Player
    {
        Turns = 5,
        Cash = 1_000,
        Pimps = 1,
        Hoes = 8,
        Thugs = 4,
        Beer = 5,
        Weed = 3,
        HoeHappiness = 40,
        ThugHappiness = 30
    };

    var result = service.RecoverCrewMorale(player, "party");
    var breakdown = RequiredBreakdown(result);

    AssertEqual(3, player.Turns);
    AssertEqual(870L, player.Cash);
    AssertEqual(3, player.Beer);
    AssertEqual(1, player.Weed);
    AssertEqual(52.0, player.HoeHappiness);
    AssertEqual(40.0, player.ThugHappiness);
    AssertEqual(130L, Value<long>(breakdown, "cashCost"));
    AssertEqual(2, Value<int>(breakdown, "beerCost"));
    AssertEqual(2, Value<int>(breakdown, "weedCost"));
}

static void StreetAutoBuyToppsUpWithinLimits()
{
    var options = new GameOptions
    {
        MaxActionTurns = 20,
        CondomPrice = 10,
        BeerPrice = 15,
        StreetAction = new StreetActionOptions
        {
            BaseGrossPerTurn = 0,
            HoeGrossPerTurn = new RangeOptions(0, 0),
            PimpGrossPerTurn = new RangeOptions(0, 0),
            PimpRecruitChance = 0,
            HoeRecruitChance = 0,
            ThugRecruitChance = 0,
            Finds = NoFinds()
        },
        Morale = new MoraleOptions { TurnsPerCondom = 10, TurnsPerBeer = 10 }
    };
    var service = CreateEconomy(options);

    // 10 hoes over 10 turns needs 10 condoms; 5 thugs needs 5 beer. Holding none of either.
    var player = new Player { Turns = 20, Cash = 10_000, Hoes = 10, Thugs = 5, Hideout = new Hideout { StorageLevel = 3 } };
    var breakdown = RequiredBreakdown(service.Scout(player, 10, autoBuySupplies: true));

    AssertEqual(10, Value<int>(breakdown, "autoBoughtCondoms"));
    AssertEqual(5, Value<int>(breakdown, "autoBoughtBeer"));
    AssertEqual(175L, Value<long>(breakdown, "autoBuyCost"));
    AssertEqual(0, Value<int>(breakdown, "condomShortage"));
    AssertEqual(0, Value<int>(breakdown, "beerShortage"));

    // Cash only stretches to two condoms, and the action still runs on a partial restock.
    var broke = new Player { Turns = 20, Cash = 20, Hoes = 10, Thugs = 5, Hideout = new Hideout { StorageLevel = 3 } };
    var brokeBreakdown = RequiredBreakdown(service.Scout(broke, 10, autoBuySupplies: true));

    AssertEqual(2, Value<int>(brokeBreakdown, "autoBoughtCondoms"));
    AssertEqual(0, Value<int>(brokeBreakdown, "autoBoughtBeer"));
    AssertEqual(8, Value<int>(brokeBreakdown, "condomShortage"));
    AssertEqual(10, Value<int>(brokeBreakdown, "turnsSpent"));

    // Storage is the other ceiling: a level 1 room caps the top-up at what fits. Fifty hoes on a level
    // 1 room is a state the crew caps now stop anybody reaching by hiring, but the clamp still has to
    // hold for a player who had the crew before the caps were tied to the store.
    var cramped = new Player { Turns = 20, Cash = 10_000, Hoes = 50, Thugs = 5, Hideout = new Hideout { StorageLevel = 1 } };
    var crampedBreakdown = RequiredBreakdown(service.Scout(cramped, 20, autoBuySupplies: true));

    AssertEqual(42, Value<int>(crampedBreakdown, "autoBoughtCondoms"));

    // Left off, nothing is bought at all.
    var manual = new Player { Turns = 20, Cash = 10_000, Hoes = 10, Thugs = 5, Hideout = new Hideout { StorageLevel = 3 } };
    var manualBreakdown = RequiredBreakdown(service.Scout(manual, 10));

    AssertEqual(0, Value<int>(manualBreakdown, "autoBoughtCondoms"));
    AssertEqual(10_000L, manual.Cash);
}

// Player.Pimps is what the economy and the leaderboard's net worth expression read, so it must
// always equal the number of living pimps on the roster.
/// <summary>
/// Reported from a live game: morale fell about 29 points per shift while the summary said the crew
/// had been auto-supplied. The crew had outgrown the storage room, auto-buy topped up to the room
/// rather than to the requirement, and the missing condoms were charged at a flat rate each.
///
/// The penalty is now a share of the upkeep missed, so it means the same thing at any crew size.
/// </summary>
static void ShortagePenaltyScalesWithTheShareMissed()
{
    static GameOptions Tuning() => new()
    {
        MaxActionTurns = 20,
        StreetAction = new StreetActionOptions
        {
            BaseGrossPerTurn = 0,
            HoeGrossPerTurn = new RangeOptions(0, 0),
            PimpGrossPerTurn = new RangeOptions(0, 0),
            PimpRecruitChance = 0,
            HoeRecruitChance = 0,
            ThugRecruitChance = 0,
            Finds = NoFinds()
        },
        Morale = new MoraleOptions
        {
            HoesManagedPerPimp = 10,
            TurnsPerCondom = 12,
            HoeStreetWorkGainPerTurn = 0.14,
            HoeCutMoraleScalePerTurn = 0,
            CondomShortagePenalty = 2.25,
            UnmanagedHoePenalty = 0,
            UncoveredThugPenalty = 0,
            DesertionThreshold = 0,
            MaxDesertionChance = 0
        }
    };

    // The reported case: 59 hoes need 99 condoms for a 20 turn shift, a level 3 room holds 84.
    var reported = new Player { Turns = 20, Pimps = 9, Hoes = 59, Condoms = 84, HoeHappiness = 93.8, Hideout = new Hideout { StorageLevel = 3 } };
    CreateEconomy(Tuning()).Scout(reported, 20, false);
    // 15 of 99 missing is 15% of a 45 point full-shortage penalty, against the +2.8 the shift earns.
    AssertTrue(reported.HoeHappiness > 89 && reported.HoeHappiness < 91,
        $"a 14% shortfall should cost a few points, not thirty. Was {reported.HoeHappiness:F1}");

    // Going out with nothing still craters morale, which is the pressure worth keeping.
    var unsupplied = new Player { Turns = 20, Pimps = 9, Hoes = 59, Condoms = 0, HoeHappiness = 93.8, Hideout = new Hideout { StorageLevel = 3 } };
    CreateEconomy(Tuning()).Scout(unsupplied, 20, false);
    AssertTrue(unsupplied.HoeHappiness > 50 && unsupplied.HoeHappiness < 53,
        $"an entirely unsupplied shift should cost about 45. Was {unsupplied.HoeHappiness:F1}");

    // Same share missed, very different crew sizes: the cost has to be the same, which is the whole
    // point of charging a share rather than a count.
    var small = new Player { Turns = 20, Pimps = 9, Hoes = 24, Condoms = 20, HoeHappiness = 80, Hideout = new Hideout { StorageLevel = 6 } };
    var large = new Player { Turns = 20, Pimps = 20, Hoes = 192, Condoms = 160, HoeHappiness = 80, Hideout = new Hideout { StorageLevel = 6 } };
    CreateEconomy(Tuning()).Scout(small, 20, false);
    CreateEconomy(Tuning()).Scout(large, 20, false);
    AssertTrue(Math.Abs(small.HoeHappiness - large.HoeHappiness) < 0.5,
        $"half the crew size should not change what being equally short costs. {small.HoeHappiness:F1} vs {large.HoeHappiness:F1}");

    // A fully supplied shift still earns morale.
    var supplied = new Player { Turns = 20, Pimps = 9, Hoes = 59, Condoms = 99, HoeHappiness = 50, Hideout = new Hideout { StorageLevel = 6 } };
    CreateEconomy(Tuning()).Scout(supplied, 20, false);
    AssertEqual(52.8, Math.Round(supplied.HoeHappiness, 1));
}

/// <summary>
/// A full storage room is a harder limit than what a player currently holds: past it there is nothing
/// left to buy and every shift runs a shortage. The report has to state that limit and name the room
/// that would fix it, because the trap is invisible otherwise.
/// </summary>
static void CrewReportNamesTheStorageLevelNeeded()
{
    var service = CreateEconomy();

    // A level 2 room holds 84 condoms, which carries 50 hoes through a 20 turn shift and no more.
    var supplied = new Player { Pimps = 6, Hoes = 50, Thugs = 25, Hideout = new Hideout { StorageLevel = 2 } };
    var fine = service.GetCrewReport(supplied);
    AssertEqual(50, fine.HoesStorageCanSupply);
    AssertEqual(25, fine.ThugsStorageCanSupply);
    AssertTrue(fine.StorageLevelToSupplyCrew is null, "a room that already covers the crew needs no upgrade named");

    // One hoe past it and the room is the constraint, not the stock on the shelf.
    var stretched = new Player { Pimps = 6, Hoes = 59, Thugs = 25, Condoms = 84, Hideout = new Hideout { StorageLevel = 2 } };
    var warned = service.GetCrewReport(stretched);
    AssertEqual(50, warned.HoesStorageCanSupply);
    AssertEqual(3, warned.StorageLevelToSupplyCrew ?? 0);

    // Nothing in the table carries a crew past the top room, and that has to be said rather than
    // pointing at a level that does not exist.
    var enormous = new Player { Pimps = 22, Hoes = 500, Thugs = 110, Hideout = new Hideout { StorageLevel = 6 } };
    AssertTrue(service.GetCrewReport(enormous).StorageLevelToSupplyCrew is null, "no room covers a crew this size");
}

static void PimpRosterStaysInStepWithCounter()
{
    var service = CreateEconomy();
    var roster = CreateRoster();
    var now = new DateTime(2026, 8, 12, 0, 0, 0, DateTimeKind.Utc);
    var player = new Player { Cash = 1_000_000, Pimps = 0, HoeHappiness = 100, ThugHappiness = 100 };

    // A counter set directly, as admin cheats and bot seeding do, is reconciled into named crew.
    player.Pimps = 3;
    roster.Reconcile(player, now);
    AssertEqual(3, player.Pimps);
    AssertEqual(3, roster.Active(player).Count);
    AssertEqual(3, roster.Active(player).Select(x => x.Name).Distinct().Count());

    service.HireCrew(player, "pimps", 2);
    AssertEqual(5, player.Pimps);
    AssertEqual(5, roster.Active(player).Count);

    service.FireCrew(player, "pimps", 2);
    AssertEqual(3, player.Pimps);
    AssertEqual(3, roster.Active(player).Count);
    // The fired pair stay on the books as history rather than vanishing.
    AssertEqual(2, roster.Fallen(player).Count);

    // Reconcile also trims when the counter drops beneath the roster.
    player.Pimps = 1;
    roster.Reconcile(player, now);
    AssertEqual(1, player.Pimps);
    AssertEqual(1, roster.Active(player).Count);
}

static void PimpSpecialtiesBonusTheRightActivity()
{
    var options = new GameOptions { Pimps = new PimpOptions { MaxStreetBonusPercent = 20, MaxDefenceBonusPercent = 20 } };
    var roster = CreateRoster(options);
    var now = new DateTime(2026, 8, 12, 0, 0, 0, DateTimeKind.Utc);
    var player = new Player { Pimps = 3 };
    roster.Reconcile(player, now);
    var crew = roster.Active(player);
    // Unsaved rows all carry Id 0, which the "already commanding" filter keys on.
    for (var i = 0; i < crew.Count; i++) crew[i].Id = i + 1;
    crew[0].Specialty = PimpSpecialties.Hustler; crew[0].BonusPercent = 5;
    crew[1].Specialty = PimpSpecialties.Hustler; crew[1].BonusPercent = 4;
    crew[2].Specialty = PimpSpecialties.Enforcer; crew[2].BonusPercent = 6;

    // Each specialty only counts toward its own activity.
    AssertEqual(9, roster.StreetBonusPercent(player, []));
    AssertEqual(6, roster.DefenceBonusPercent(player, []));

    // A pimp away commanding is not home to help either one.
    AssertEqual(4, roster.StreetBonusPercent(player, [crew[0].Id]));
    AssertEqual(0, roster.DefenceBonusPercent(player, [crew[2].Id]));

    // Stacked bonuses are capped so a full roster is not a huge swing.
    foreach (var pimp in crew) { pimp.Specialty = PimpSpecialties.Hustler; pimp.BonusPercent = 40; }
    AssertEqual(20, roster.StreetBonusPercent(player, []));
}

static void PimpCommanderSelectionHonoursRequest()
{
    var roster = CreateRoster();
    var now = new DateTime(2026, 8, 12, 0, 0, 0, DateTimeKind.Utc);
    var player = new Player { Pimps = 3 };
    roster.Reconcile(player, now);
    var crew = roster.Active(player);
    for (var i = 0; i < crew.Count; i++) crew[i].Id = i + 1;
    crew[0].Specialty = PimpSpecialties.Hustler; crew[0].BonusPercent = 8;
    crew[1].Specialty = PimpSpecialties.Enforcer; crew[1].BonusPercent = 3;
    crew[2].Specialty = PimpSpecialties.Enforcer; crew[2].BonusPercent = 7;

    // Asking for someone specific gets exactly them, even a weaker choice.
    AssertEqual(crew[0].Id, roster.ChooseCommander(player, [], crew[0].Id)!.Id);

    // With no request the strongest Enforcer leads.
    AssertEqual(crew[2].Id, roster.ChooseCommander(player, [])!.Id);

    // Someone already out cannot lead a second attack.
    AssertRuleError(() => roster.ChooseCommander(player, [crew[0].Id], crew[0].Id), "requesting a pimp already commanding");
    AssertEqual(crew[2].Id, roster.ChooseCommander(player, [crew[1].Id])!.Id);

    // Nor can a pimp who belongs to nobody here.
    AssertRuleError(() => roster.ChooseCommander(player, [], 999_999), "requesting a pimp that is not yours");
}

static void PimpCommanderDiesOnDefeat()
{
    var options = new GameOptions { Pimps = new PimpOptions { CommanderDeathChanceOnDefeat = 1 } };
    var roster = CreateRoster(options, new AlwaysRandom());
    var now = new DateTime(2026, 8, 12, 0, 0, 0, DateTimeKind.Utc);
    var player = new Player { Pimps = 2 };
    roster.Reconcile(player, now);
    var commander = roster.Active(player)[0];

    var fate = roster.SettleMission(player, commander, "Defeat", now);

    AssertTrue(fate.Happened, "a certain-death defeat should kill the commander");
    AssertEqual("Killed in action", fate.Reason);
    AssertEqual(1, player.Pimps);
    AssertEqual(1, roster.Active(player).Count);
    AssertEqual(1, roster.Fallen(player).Count);

    // A win instead builds their record and leaves them standing.
    var survivor = roster.Active(player)[0];
    var won = roster.SettleMission(player, survivor, "Victory", now);
    AssertTrue(!won.Happened, "a victory should not cost the commander");
    AssertEqual(1, survivor.Victories);
    AssertEqual(1, survivor.MissionsLed);
}

static void PimpWalksOutWhenLoyaltyBottomsOut()
{
    var options = new GameOptions { Pimps = new PimpOptions { WalkOutThreshold = 90, MaxWalkOutChance = 1 } };
    var roster = CreateRoster(options, new AlwaysRandom());
    var now = new DateTime(2026, 8, 12, 0, 0, 0, DateTimeKind.Utc);
    var player = new Player { Pimps = 3 };
    roster.Reconcile(player, now);
    foreach (var pimp in roster.Active(player))
        pimp.Loyalty = 5;

    var walked = roster.SettleStreetWork(player, turns: 5, crewMorale: 100, nowUtc: now);

    // Never the last one: a player with no pimps could never command an attack again.
    AssertEqual(2, walked.Count);
    AssertEqual(1, player.Pimps);
    AssertEqual(1, roster.Active(player).Count);
    AssertEqual("Walked out", roster.Fallen(player)[0].LostReason);
}

static void HideoutCapsCrewHiring()
{
    var service = CreateEconomy();
    var player = new Player
    {
        Cash = 1_000_000,
        Pimps = 1,
        Hoes = 48,
        Thugs = 1,
        HoeHappiness = 100,
        ThugHappiness = 100,
        // A level 2 room supplies a full Trap House, so the building is the cap being tested here.
        Hideout = new Hideout { StorageLevel = 2 }
    };

    // Trap House holds 50 hoes, so only two more fit.
    service.HireCrew(player, "hoes", 2);
    AssertEqual(50, player.Hoes);

    AssertRuleError(() => service.HireCrew(player, "hoes", 1), "hiring past the hideout cap");
    AssertEqual(50, player.Hoes);

    // On the starting room the store is the cap instead, and the refusal has to say so rather than
    // blaming the building. The two are fixed by completely different purchases, and a player sent to
    // buy a bigger house for a room-sized problem has been sent to waste their money.
    var starting = new Player
    {
        Cash = 1_000_000,
        Hoes = 25,
        HoeHappiness = 100,
        Hideout = new Hideout { StorageLevel = 1 }
    };
    AssertRuleError(() => service.HireCrew(starting, "hoes", 1), "storage room");
    AssertEqual(25, starting.Hoes);
}

// The counter and the shelf are two lists that have to agree, and nothing was making them. Poison went
// on sale with no case in the capacity switch behind it, so buying any fell through to a developer note
// - "Store item is not implemented" - and the good was unobtainable by the only route that sold it.
//
// Walks the shop rather than naming its stock, so the next thing added to it is covered the day it is
// added rather than the day somebody tries to buy one.
// Moonshine and cut were held, shelved, taxed in heat and paid for in turns, and counted for nothing
// on the board - so brewing a full still lowered your standing by whatever the materials cost. The same
// trap the hideout was in, in a different currency, and nothing was watching for it.
//
// Walks the goods the game knows about rather than naming them, so the next one added is covered the
// day it is added rather than whenever somebody notices their net worth going the wrong way.
// Poison was put on the bench without the medicine that answers it, so for a while the game sold the
// attack at a third of the counter price and left the defence at full. Whoever built the deeper shop
// got to poison houses cheaply while the houses could only buy their way out.
//
// The rule this pins is small and worth keeping: for any pair where one thing exists to beat another,
// the bench must not reach the attacking end first, nor make it the better bargain.
static void DefenceIsNeverDearerThanAttack()
{
    var options = Resolve(new GameOptions());

    var poison = options.Makeables.Single(x => x.Key == "poison");
    var medicine = options.Makeables.Single(x => x.Key == "medicine");

    // Reachable no later than the thing it answers.
    AssertTrue(medicine.MinWorkshopLevel <= poison.MinWorkshopLevel,
        $"medicine at level {medicine.MinWorkshopLevel} must not come after poison at {poison.MinWorkshopLevel}");

    // And no worse a saving against the counter, or buying the cure is the mug's game.
    var poisonSaving = 1 - poison.MaterialCost / (double)options.PoisonPrice;
    var medicineSaving = 1 - medicine.MaterialCost / (double)options.MedicinePrice;
    AssertTrue(medicineSaving >= poisonSaving - 0.05,
        $"medicine saves {medicineSaving:P0} against poison's {poisonSaving:P0}");

    // Every recipe undercuts what it stands in for, or there is no reason for the bench to exist.
    foreach (var recipe in options.Makeables.Where(x => x.CanMake))
    {
        var counter = TradeGoods.ReferencePrice(options, recipe.Key, "Detroit");
        AssertTrue(counter > 0, $"{recipe.Key} should have a price to be judged against");
        AssertTrue(recipe.MaterialCost < counter,
            $"making {recipe.Key} costs {recipe.MaterialCost} against a counter price of {counter}");
    }

    AssertTrue(!options.Makeables.Any(x => x.Key == "condoms" && x.CanMake),
        "condoms stay a store supply, not a workshop craft");

    var levelThreeRecipes = options.Makeables.Where(x => x.CanMake && x.MinWorkshopLevel == 3).Select(x => x.Key).OrderBy(x => x).ToArray();
    AssertTrue(levelThreeRecipes.Length > 0, "workshop level three needs at least one craft unlock");
}

static void EverythingYouCanHoldIsWorthSomething()
{
    var options = Resolve(new GameOptions());
    var service = CreateEconomy(options);

    // Every key TradeGoods will store against a player, which is the definition of a thing you can hold.
    var goods = new[] { "condoms", "beer", "medicine", "poison", "weed", "coke", "moonshine", "cut" };

    foreach (var good in goods)
    {
        var empty = new Player();
        var holding = new Player { CokePurity = 1 };
        TradeGoods.Add(holding, good, 10);

        AssertEqual(10, TradeGoods.Held(holding, good));
        var gain = service.CalculateNetWorth(holding) - service.CalculateNetWorth(empty);
        AssertTrue(gain > 0, $"holding 10 {good} should be worth something, moved net worth by {gain}");
    }

    // Guns are held through the rack rather than a plain counter, and are worth what the shop charges.
    foreach (var tier in WeaponTiers.All)
    {
        var armed = new Player();
        TradeGoods.Add(armed, tier, 5);
        AssertTrue(service.CalculateNetWorth(armed) > 0, $"a rack of {tier} should be worth something");
    }

    // And the two forms of the sum still agree with each other once all of it is in play.
    var stocked = new Player
    {
        Cash = 1_000, Weed = 12, Coke = 8, CokePurity = 0.9, Moonshine = 20, Cut = 30,
        Medicine = 3, Poison = 4, Condoms = 40, Beer = 25, Rides = 1, Pistols = 6,
        Hideout = new Hideout { Tier = 2, StorageLevel = 3 }
    };
    AssertEqual(service.CalculateNetWorth(stocked), service.NetWorthExpression.Compile()(stocked));
    AssertEqual(service.CalculatePlunder(stocked), service.PlunderExpression.Compile()(stocked));
}

static void EverythingOnTheCounterCanBeBought()
{
    var options = Resolve(new GameOptions());
    var service = CreateEconomy(options);

    foreach (var item in service.GetStore())
    {
        var buyer = new Player
        {
            Cash = 100_000_000,
            Hideout = new Hideout { Tier = 4, StorageLevel = 6, SafeLevel = 5 }
        };

        // One of each. A price the player can plainly afford and a room with space, so anything that
        // refuses here is refusing on the shape of the code rather than on the state of the player.
        var bought = service.BuyStoreItem(buyer, item.Key, 1);
        AssertTrue(bought is not null, $"{item.Key} should be buyable");
        AssertTrue(TradeGoods.Held(buyer, item.Key) > 0 || item.Key == "rides",
            $"buying {item.Key} should leave the player holding one");
    }
}

static void HideoutBlocksOverflowingStoreBuys()
{
    var service = CreateEconomy(StorageCapOptions(condoms: 10));
    var player = new Player { Cash = 1_000_000, Condoms = 8, Hideout = new Hideout() };

    AssertRuleError(() => service.BuyStoreItem(player, "condoms", 5), "buying past storage capacity");
    AssertEqual(8, player.Condoms);
    AssertEqual(1_000_000L, player.Cash);

    service.BuyStoreItem(player, "condoms", 2);
    AssertEqual(10, player.Condoms);
}

static void HideoutBanksCashOverSafeAndSpillsGoods()
{
    var options = new GameOptions { WeedSellPrice = 40 };
    var service = CreateEconomy(options);
    var player = new Player
    {
        Cash = 49_000,
        BankCash = 0,
        Weed = 60,
        Hideout = new Hideout { SafeLevel = 1, StorageLevel = 3 }
    };

    // 60 weed at $40 is $2,400, which pushes cash past the level 1 safe's $50,000.
    var result = service.SellProduct(player, "weed", 60);

    AssertEqual(50_000L, player.Cash);
    AssertEqual(1_400L, player.BankCash);
    AssertEqual(1_400L, Value<long>(RequiredBreakdown(result), "cashBankedByOverflow"));
    AssertTrue(result.Summary.Contains("safe was full"), "the summary should explain the transfer");
}

static void CityMarketsChangeProductSalePrices()
{
    var service = CreateEconomy(new GameOptions { WeedSellPrice = 40, CokeSellPrice = 150 });
    var player = new Player
    {
        City = "Chicago",
        Weed = 2,
        Coke = 1,
        Hideout = new Hideout { SafeLevel = 1, StorageLevel = 3 }
    };

    var weed = service.SellProduct(player, "weed", 2);
    AssertEqual(100L, player.Cash);
    AssertEqual(50, Value<int>(RequiredBreakdown(weed), "unitPrice"));

    player.City = "Detroit";
    var coke = service.SellProduct(player, "coke", 1);
    AssertEqual(250L, player.Cash);
    AssertEqual(150, Value<int>(RequiredBreakdown(coke), "unitPrice"));
}

static void TravelChangesCityAndSpendsTheTownsDistance()
{
    var options = Resolve(null);
    var service = CreateEconomy(options);
    var player = new Player { City = "Detroit", Turns = 10 };

    var result = service.Travel(player, "Chicago");

    AssertEqual("Chicago", player.City);
    AssertEqual(7, player.Turns);
    AssertEqual(3, Value<int>(RequiredBreakdown(result), "turnsSpent"));
    AssertEqual("High", Value<string>(RequiredBreakdown(result), "risk"));

    // Distance and danger are separate numbers: Chicago is the short run into the bad town, Los
    // Angeles the long run into the calmer one. Reading either off the other would fail here.
    AssertEqual(3, options.CityMarkets.TravelTurns("Chicago"));
    AssertEqual(6, options.CityMarkets.TravelTurns("Los Angeles"));
    AssertEqual(22, options.CityMarkets.BustChancePercent("Chicago"));
    AssertEqual(12, options.CityMarkets.BustChancePercent("Los Angeles"));

    AssertRuleError(() => service.Travel(player, "Chicago"), "traveling to the current city");
    AssertRuleError(() => service.Travel(player, "Atlantis"), "traveling to an unknown city");
}

static void StoppedRunTakesAShareOfTheLoadButNeverTheBank()
{
    var service = CreateEconomy(null, new AlwaysRandom());
    var player = new Player { City = "Detroit", Turns = 10, Cash = 10_000, BankCash = 50_000, Weed = 100, Coke = 40 };

    var result = service.Travel(player, "Chicago");
    var breakdown = RequiredBreakdown(result);

    // The trip is already paid for, so a stopped run still arrives, just lighter.
    AssertEqual("Chicago", player.City);
    AssertEqual(7, player.Turns);
    AssertTrue(Value<bool>(breakdown, "busted"), "the run should have been stopped");

    AssertEqual(50_000L, player.BankCash);
    AssertEqual(8_000L, player.Cash);
    AssertEqual(80, player.Weed);
    AssertEqual(32, player.Coke);
    AssertEqual(2_000L, Value<long>(breakdown, "cashSeized"));
    AssertEqual(20, Value<int>(breakdown, "weedSeized"));
    AssertEqual(8, Value<int>(breakdown, "cokeSeized"));
    AssertTrue(result.Summary.Contains("got stopped"), "the summary should say the run was stopped");
}

static void BreakEvenSeizureIsPricedAgainstWhatThePlayerCarries()
{
    var options = Resolve(null);
    var cokeRunner = new Player { City = "Detroit", Coke = 100 };
    var markets = ToCityMarkets(options, cokeRunner);
    int? BreakEven(IEnumerable<CityMarketResponse> board, string city)
        => board.Single(x => x.City == city).BreakEvenSeizurePercent;

    // Coke out of Detroit at 150: Miami pays 225, New York 188, Chicago 150, Los Angeles 113. The
    // share is how much of the load a stop can take before staying home would have paid better.
    AssertEqual<int?>(33, BreakEven(markets, "Miami"));
    AssertEqual<int?>(20, BreakEven(markets, "New York"));
    AssertEqual<int?>(0, BreakEven(markets, "Chicago"));
    AssertEqual<int?>(0, BreakEven(markets, "Los Angeles"));
    AssertEqual<int?>(null, BreakEven(markets, "Detroit"));

    // The same map reads differently for a weed load: the number belongs to what is being carried,
    // not to the route, which is the whole reason it is worth showing.
    var weedRunner = new Player { City = "Detroit", Weed = 400 };
    AssertEqual<int?>(40, BreakEven(ToCityMarkets(options, weedRunner), "Chicago"));

    AssertEqual<int?>(null, BreakEven(ToCityMarkets(options, new Player { City = "Detroit" }), "Miami"));
}

static void SmallLoadIsNotWorthStopping()
{
    var service = CreateEconomy(null, new AlwaysRandom());
    var player = new Player { City = "Detroit", Turns = 10, Cash = 100 };

    var result = service.Travel(player, "Chicago");

    AssertTrue(!Value<bool>(RequiredBreakdown(result), "busted"), "pocket change should not be worth a stop");
    AssertEqual(100L, player.Cash);
    AssertEqual("Chicago", player.City);
}

static void HideoutGrandfathersExistingStock()
{
    var hideouts = CreateHideouts(StorageCapOptions(condoms: 10));
    // The room holds 10, but this player already had 68 from before caps existed.
    var player = new Player { Condoms = 68, Hideout = new Hideout() };
    var before = StockLevels.From(player);

    var noGain = hideouts.Settle(player, before);
    AssertEqual(68, player.Condoms);
    AssertTrue(!noGain.Any, "held stock should never be taken away");

    // Gains on top of a grandfathered amount still spill.
    player.Condoms += 5;
    var overflow = hideouts.Settle(player, before);
    AssertEqual(68, player.Condoms);
    AssertEqual(5, overflow.CondomsLost);

    // Once spent back under the cap, the ceiling follows them down.
    player.Condoms = 4;
    var settled = StockLevels.From(player);
    player.Condoms = 12;
    AssertEqual(2, hideouts.Settle(player, settled).CondomsLost);
    AssertEqual(10, player.Condoms);
}

static void HideoutLabRaisesProductionYield()
{
    var options = new GameOptions
    {
        Production = new ProductionOptions { Weed = new ProductProductionOptions(25, 4, 4) }
    };
    var service = CreateEconomy(options);
    var withoutLab = new Player { Cash = 10_000, Turns = 20, Hideout = new Hideout() };
    var withLab = new Player { Cash = 10_000, Turns = 20, Hideout = new Hideout { StorageLevel = 3, WeedLabLevel = 3, WorkshopLevel = 2 } };
    var underBenched = new Player { Cash = 10_000, Turns = 20, Hideout = new Hideout { StorageLevel = 3, WeedLabLevel = 3, WorkshopLevel = 1 } };

    var plain = service.Produce(withoutLab, "weed", 5);
    var boosted = service.Produce(withLab, "weed", 5);
    var capped = service.Produce(underBenched, "weed", 5);

    // MinimumRandom always rolls the low end, so five turns is a flat 20 units before the lab.
    AssertEqual(20, Value<int>(RequiredBreakdown(plain), "baseUnits"));
    AssertEqual(20, Value<int>(RequiredBreakdown(boosted), "baseUnits"));
    AssertEqual(110, Value<int>(RequiredBreakdown(boosted), "labBonusPercent"));
    AssertEqual(42, Value<int>(RequiredBreakdown(boosted), "unitsProduced"));
    AssertEqual(60, Value<int>(RequiredBreakdown(capped), "labBonusPercent"));
    AssertEqual(32, Value<int>(RequiredBreakdown(capped), "unitsProduced"));
}

static void HideoutTierBuildChargesUpFrontAndLandsOnTime()
{
    var options = Resolve(null);
    var hideouts = CreateHideouts(options);
    var tier2 = options.Hideout.Tiers.Single(x => x.Level == 2);
    var start = new DateTime(2026, 8, 13, 9, 0, 0, DateTimeKind.Utc);
    // Cash and bank together, and the bank pays first. A tier costs more than the safe below it holds,
    // so charging cash on hand alone would put every tier permanently out of reach.
    var player = new Player
    {
        Cash = 30_000,
        BankCash = tier2.UpgradeCost - 25_000,
        Turns = tier2.UpgradeTurns + 3,
        // A level 2 room supplies a full Trap House, so the tier is the only cap in play here.
        Hideout = new Hideout { StorageLevel = 2 }
    };

    AssertEqual(tier2.UpgradeCost + 5_000, player.Cash + player.BankCash);
    hideouts.Upgrade(player, "tier", start);

    AssertEqual(0L, player.BankCash);
    AssertEqual(5_000L, player.Cash);
    AssertEqual(3, player.Turns);
    // Paid for, but not yet built: the caps are what a player would otherwise buy their way past.
    AssertEqual(1, player.Hideout!.Tier);
    AssertEqual(2, player.Hideout.UpgradingToTier ?? 0);
    AssertEqual(50, hideouts.CapacityFor(player.Hideout).MaxHoes);

    AssertRuleError(() => hideouts.Upgrade(player, "tier", start), "starting a second build while one runs");

    AssertTrue(!hideouts.CompleteBuild(player.Hideout, start.AddMinutes(tier2.BuildMinutes - 1)), "an unfinished build does not land");
    AssertEqual(1, player.Hideout.Tier);

    AssertTrue(hideouts.CompleteBuild(player.Hideout, start.AddMinutes(tier2.BuildMinutes)), "a due build lands");
    AssertEqual(2, player.Hideout.Tier);
    AssertTrue(player.Hideout.UpgradingToTier is null, "the pending tier is cleared once it lands");

    // The new building does not hand over a bigger crew on its own, because the level 2 room behind it
    // still only supplies fifty. Moving somewhere with more space does not put more food in the
    // cupboard, and the cap is honest about which of the two you have run out of.
    AssertEqual(50, hideouts.CapacityFor(player.Hideout).MaxHoes);

    // Deepening the store is what actually collects the tier's room, and now there is a building big
    // enough to hold the deeper room.
    player.Hideout.StorageLevel = 3;
    AssertEqual(tier2.MaxHoes, hideouts.CapacityFor(player.Hideout).MaxHoes);
    AssertEqual(tier2.MaxThugs, hideouts.CapacityFor(player.Hideout).MaxThugs);
}

static void HideoutTierGatesDeeperRooms()
{
    var hideouts = CreateHideouts();
    var player = new Player { Cash = 5_000_000, Hideout = new Hideout { StorageLevel = 2 } };

    var locked = hideouts.NextUpgrade(player.Hideout, "storage");
    AssertEqual(3, locked!.Level);
    AssertTrue(locked.TierLocked, "a level 3 storage room needs a bigger building");
    AssertRuleError(() => hideouts.Upgrade(player, "storage", DateTime.UtcNow), "upgrading a room past the tier");
    AssertEqual(2, player.Hideout!.StorageLevel);
    AssertEqual(5_000_000L, player.Cash);

    player.Hideout.Tier = 2;
    AssertTrue(!hideouts.NextUpgrade(player.Hideout, "storage")!.Locked, "the second tier holds a level 3 room");
    hideouts.Upgrade(player, "storage", DateTime.UtcNow);
    AssertEqual(3, player.Hideout.StorageLevel);

    var lab = new Player { Cash = 5_000_000, Hideout = new Hideout { Tier = 2, WeedLabLevel = 1 } };
    var labLocked = hideouts.NextUpgrade(lab.Hideout, "weedlab");
    AssertEqual(2, labLocked!.Level);
    AssertTrue(labLocked.WorkshopLocked, "a level 2 lab needs the level 1 workshop that supports it");
    AssertRuleError(() => hideouts.Upgrade(lab, "weedlab", DateTime.UtcNow), "upgrading a lab past the workshop");
    lab.Hideout!.WorkshopLevel = 1;
    AssertTrue(!hideouts.NextUpgrade(lab.Hideout, "weedlab")!.Locked, "the workshop unlocks the next lab bonus");
    hideouts.Upgrade(lab, "weedlab", DateTime.UtcNow);
    AssertEqual(2, lab.Hideout.WeedLabLevel);
}

/// <summary>
/// The rule the storage table is built on: every level that a tier unlocks holds exactly what a
/// full-length street action consumes at that tier's crew caps. Without this the tables drift apart
/// silently and a maxed player finds they cannot supply the crew their building allows.
/// </summary>
// The building used to promise a crew the store could not begin to feed: a Trap House offered room for
// fifty hoes while the room behind it held four turns of condoms for them. Every shift then charged the
// player morale for a shortfall the game itself had invited, which is a punishment for believing the
// hideout page.
// Everything else here builds its options from the code defaults, which is exactly half the config.
// The lists in appsettings.json win whenever they are present - ApplyDefaultsWhereEmpty only fills a
// list nobody has filled already - so every tuning value the server actually runs on was untested, and
// a change to the defaults could look green here while the game went on using the old numbers.
//
// It had. The storage ladder was rebuilt in code and appsettings went on shipping the old one, so the
// running game had the new rule capping crew at the old room sizes: a starting player was held to ten
// hoes rather than the twenty-five intended, which is worse than the fifty they had before anybody
// touched it. Tests cannot only read the half of the config that is convenient.
// A test that is written but never listed in the manifest above does not fail - it simply is not run,
// and the suite reports all green while the thing it was written to guard goes uncovered. That has
// happened twice: once to a test of instalment deliveries, once to a headline nobody had ever asserted
// on, and both times the only symptom was a total that did not go up by one.
//
// Reads this file rather than reflecting over the assembly, because top-level statements compile the
// manifest into a method body where reflection cannot see it.
static void AnEmailIsASecondNameNotAMessage()
{
    // Folded, trimmed, and blank collapsed to null. Without the fold, signing up as Sam@example.com
    // and coming back as sam@example.com is two different accounts as far as the unique index cares.
    AssertEqual("sam@example.com", AccountSetup.NormalizeEmail("  Sam@Example.COM  "));
    AssertEqual(null, AccountSetup.NormalizeEmail("   "));
    AssertEqual(null, AccountSetup.NormalizeEmail(null));

    // Loose on purpose - nothing is ever sent here - but strict about the parts that make it a key.
    foreach (var good in new[] { "a@b.co", "first.last@sub.domain.example", "someone+tag@example.org" })
        AssertTrue(AccountSetup.LooksLikeAnEmail(good), $"{good} should be usable as a sign-in name");
    foreach (var bad in new[] { "nobody", "@example.com", "someone@", "someone@localhost", "two @example.com", "a@b@c.com" })
        AssertTrue(!AccountSetup.LooksLikeAnEmail(bad), $"{bad} should not be usable as a sign-in name");

    // 254 is the cap the column carries, so anything longer would be a truncation rather than a save.
    AssertTrue(!AccountSetup.LooksLikeAnEmail(new string('a', 250) + "@example.com"), "an over-long address should be refused");

    // The @ is the whole of how the login box tells the two kinds of name apart. A username holding
    // one would be looked up against the email column for ever and never found, so it is refused at
    // the point it is chosen rather than becoming an account nobody can sign into.
    AssertTrue(AccountSetup.LooksLikeAnAttemptAtEmail("sam@example.com"), "an address should read as an address");
    AssertTrue(AccountSetup.LooksLikeAnAttemptAtEmail("sam@"), "a half-typed address should still read as one");
    AssertTrue(!AccountSetup.LooksLikeAnAttemptAtEmail("sam"), "a plain username should not read as an address");
}

static void BothDoorsPutDownTheSamePlayer()
{
    // The reason the starting player was pulled out of the register endpoint: there are two ways in
    // now, and the failure this guards against is quiet - a Discord sign-up handing out a different
    // amount of money, or no hideout, because somebody edited one copy of the setup and not the other.
    var options = Resolve(new GameOptions
    {
        StartingCash = 5_000,
        StartingBankCash = 100,
        StartingTurns = 200,
        StartingPimps = 1,
        StartingHoes = 3,
        StartingThugs = 1,
        StartingCondoms = 17,
        StartingBeer = 10,
        StartingWeapons = 1,
        StartingHoeCutPercent = 30,
    });

    var withPassword = new PlayerAccount { Username = "sam", PasswordHash = "hashed" };
    var withDiscord = new PlayerAccount { Username = "alex", DiscordUserId = "1234567890" };
    var (one, oneLog) = AccountSetup.NewPlayer(withPassword, "Sam", "Chicago", options, CreateRoster(options));
    var (two, _) = AccountSetup.NewPlayer(withDiscord, "Alex", "Chicago", options, CreateRoster(options));

    AssertEqual(one.Cash, two.Cash);
    AssertEqual(one.BankCash, two.BankCash);
    AssertEqual(one.Turns, two.Turns);
    AssertEqual(one.Pimps, two.Pimps);
    AssertEqual(one.Hoes, two.Hoes);
    AssertEqual(one.Thugs, two.Thugs);
    AssertEqual(one.Condoms, two.Condoms);
    AssertEqual(one.Beer, two.Beer);
    AssertEqual(one.Pistols, two.Pistols);
    AssertEqual(one.HoeCutPercent, two.HoeCutPercent);
    AssertEqual(one.City, two.City);

    // The three things a new player is useless without, and the one line that says they arrived.
    AssertTrue(one.Hideout is not null, "a new player should have a hideout");
    AssertEqual(options.StartingPimps, one.Crew.Count);
    AssertEqual("START", oneLog.Action);
    AssertTrue(oneLog.Summary.Contains("Chicago"), "the opening line should name the town");
}

static void AnAccountAlwaysKeepsAWayIn()
{
    // The whole point of the check the account endpoints run before taking a door away. Get this
    // wrong and a player removes their password on Monday, unlinks Discord on Tuesday, and owns an
    // empire that can never be reached again.
    var passwordOnly = new PlayerAccount { PasswordHash = "hashed" };
    var discordOnly = new PlayerAccount { DiscordUserId = "1234567890" };
    var both = new PlayerAccount { PasswordHash = "hashed", DiscordUserId = "1234567890" };

    AssertTrue(passwordOnly.HasPassword, "a hash should count as a password");
    AssertTrue(!discordOnly.HasPassword, "an empty hash should never count as a password");

    // Unlinking Discord: allowed when a password is left behind, refused when it is the only door.
    AssertTrue(both.HasAnotherWayIn(withoutDiscord: true), "an account with a password can drop Discord");
    AssertTrue(!discordOnly.HasAnotherWayIn(withoutDiscord: true), "Discord alone cannot be dropped");

    // And the same question the other way round, which is what a password endpoint would ask.
    AssertTrue(both.HasAnotherWayIn(withoutPassword: true), "an account with Discord can drop its password");
    AssertTrue(!passwordOnly.HasAnotherWayIn(withoutPassword: true), "a password alone cannot be dropped");
}

static void ADiscordHandleAlwaysSuggestsAUsableUsername()
{
    // Only a suggestion - the finish form lets it be typed over - but a suggestion that the register
    // rules would refuse is worse than none, because it fails on submit and reads as the game's fault.
    foreach (var handle in new[] { "sam", "Sam.Smith", "sam_smith_99", "\u2728\u2728", "", "x", new string('a', 60) })
    {
        var suggested = AccountSetup.SuggestUsername(handle);
        AssertTrue(suggested.Length is >= 3 and <= 32, $"'{handle}' suggested '{suggested}', which the register rules would refuse");
        AssertTrue(!AccountSetup.LooksLikeAnAttemptAtEmail(suggested), $"'{suggested}' should not read as an address");
    }

    AssertEqual("SamSmith", AccountSetup.SuggestUsername("Sam.Smith"));
}

static void ADiscordTicketCannotBeForged()
{
    var tickets = new DiscordTickets(DataProtectionProvider.Create("StreetEmpire.Tests"));
    var profile = new DiscordProfile("1234567890", "sam", "Sam");

    // What goes round the loop comes back unchanged, which is the only reason a signed cookie can
    // stand in for the server remembering anything across a trip through somebody else's site.
    var ticket = tickets.ProtectSignUp(profile);
    var read = tickets.ReadSignUp(ticket);
    AssertTrue(read is not null, "a ticket this server wrote should be readable by it");
    AssertEqual(profile.Id, read!.Id);
    AssertEqual("Sam", read.DisplayName);

    // The identity is not in the browser's hands: a ticket is opaque, and one bad character voids it.
    AssertTrue(!ticket.Contains("1234567890"), "the Discord id should not be sitting in the cookie in the clear");
    AssertEqual(null, tickets.ReadSignUp(ticket[..^4] + "AAAA"));
    AssertEqual(null, tickets.ReadSignUp("not a ticket at all"));
    AssertEqual(null, tickets.ReadSignUp(null));

    // A different server cannot read this one's notes, which is what stops a ticket minted anywhere
    // else from being spent here.
    var elsewhere = new DiscordTickets(DataProtectionProvider.Create("SomebodyElse"));
    AssertEqual(null, elsewhere.ReadSignUp(ticket));

    // The state note carries the nonce that proves the round trip started in this same browser.
    var state = tickets.ProtectState("cafef00d", "http://localhost:5173/");
    var readState = tickets.ReadState(state);
    AssertTrue(readState is not null, "a state note this server wrote should be readable by it");
    AssertEqual("cafef00d", readState!.Value.Nonce);
    AssertEqual("http://localhost:5173/", readState.Value.ReturnUrl);
    AssertEqual(null, tickets.ReadState(state[..^4] + "AAAA"));

    // Two nonces are never the same one, or the check they exist for proves nothing.
    AssertTrue(DiscordTickets.NewNonce() != DiscordTickets.NewNonce(), "nonces should not repeat");
}

static void DiscordIsOffUntilItIsConfigured()
{
    // Half-configured is off. A button that appears because somebody set an id and forgot the secret
    // is a button that sends the player to Discord and fails on the way back.
    AssertTrue(!new DiscordOptions().IsConfigured, "an empty configuration should not offer Discord");
    AssertTrue(!new DiscordOptions { ClientId = "an-id" }.IsConfigured, "an id with no secret should not offer Discord");
    AssertTrue(!new DiscordOptions { ClientSecret = "a-secret" }.IsConfigured, "a secret with no id should not offer Discord");
    AssertTrue(new DiscordOptions { ClientId = "an-id", ClientSecret = "a-secret" }.IsConfigured, "both should offer Discord");

    // And the shipped file must never carry either of them, because the shipped file is public.
    var root = new DirectoryInfo(AppContext.BaseDirectory);
    while (root is not null && !File.Exists(Path.Combine(root.FullName, "StreetEmpire.sln")))
        root = root.Parent;
    AssertTrue(root is not null, "the solution root should be findable from the test binary");

    var shipped = new ConfigurationBuilder()
        .AddJsonFile(Path.Combine(root!.FullName, "Server", "StreetEmpire.Api", "appsettings.json"))
        .Build();
    var options = new DiscordOptions();
    shipped.GetSection("Auth:Discord").Bind(options);
    AssertTrue(!options.IsConfigured, "appsettings.json should never carry a Discord client secret");
    AssertTrue(!string.IsNullOrWhiteSpace(options.RedirectUri), "the shipped file should still say where Discord sends the browser back to");
}

static void ReturnUrlsAreOnlyEverOnesAlreadyTrusted()
{
    // The client names the origin it wants to be returned to, because in development that origin is
    // on a port nobody could have written into a config file. An origin the caller names and the
    // server obeys is an open redirect unless it is checked, so this is the check.
    var configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins:0"] = "http://localhost:5173",
            ["Cors:AllowedOrigins:1"] = "https://play.example.com",
        })
        .Build();
    var options = new OptionsSnapshotStub<DiscordOptions>(new DiscordOptions { ReturnUrl = "http://localhost:5173/" });

    var live = new DiscordReturnUrls(configuration, new HostingStub("Production"), options);
    AssertEqual("https://play.example.com/", live.Resolve("https://play.example.com/anywhere"));
    AssertEqual("http://localhost:5173/", live.Resolve("https://evil.example.net/"));
    AssertEqual("http://localhost:5173/", live.Resolve("javascript:alert(1)"));
    AssertEqual("http://localhost:5173/", live.Resolve("//evil.example.net"));
    AssertEqual("http://localhost:5173/", live.Resolve(null));
    // Not in the allowlist, and a live server has no reason to send anybody to a machine-local port.
    AssertEqual("http://localhost:5173/", live.Resolve("http://localhost:61234/"));

    // In development it does, because that is exactly where the dev server ends up.
    var dev = new DiscordReturnUrls(configuration, new HostingStub("Development"), options);
    AssertEqual("http://localhost:61234/", dev.Resolve("http://localhost:61234/"));
    AssertEqual("http://localhost:5173/", dev.Resolve("https://evil.example.net/"));
}

static void SessionWatermarksAreMeasuredInWholeSeconds()
{
    // Changing a password ends every other session by writing a watermark and re-issuing this one.
    // The two are compared through a cookie ticket, which keeps whole seconds and throws the fraction
    // away - so an unrounded watermark sits a few hundred microseconds ahead of the cookie written in
    // the same breath, and the first session it signs out is the one that just changed the password.
    // That is not a hypothetical: it is what happened, and it is what this floor is for.
    var fractional = new DateTime(2026, 8, 26, 13, 24, 18, 390, DateTimeKind.Utc).AddTicks(5_490);
    var floored = AuthEndpoints.ToSessionMoment(fractional);

    // The current second, floored, plus one.
    AssertEqual(new DateTime(2026, 8, 26, 13, 24, 19, DateTimeKind.Utc), floored);
    AssertEqual(DateTimeKind.Utc, floored.Kind);
    AssertEqual(0, floored.Ticks % TimeSpan.TicksPerSecond);

    // The mechanism itself, rather than a restatement of the flooring. A cookie ticket writes its
    // issued-at through the RFC1123 round-trip format, and whatever survives that trip is what the
    // watermark is later compared against.
    static DateTime ThroughACookieTicket(DateTime issued) => DateTime.ParseExact(
        new DateTimeOffset(issued, TimeSpan.Zero).ToString("r", System.Globalization.CultureInfo.InvariantCulture),
        "r",
        System.Globalization.CultureInfo.InvariantCulture,
        System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal);

    // Write the watermark from an unrounded clock and issue the cookie at the same instant, and the
    // cookie comes back from that trip fractionally earlier than the watermark. That is the whole bug:
    // the session that just changed its own password is the first one the watermark throws out.
    AssertTrue(ThroughACookieTicket(fractional) < fractional,
        "a cookie ticket should lose the fraction, which is what made an unrounded watermark a trap");

    // The session re-issued alongside the watermark survives it. This is the one that must not break.
    AssertTrue(!(ThroughACookieTicket(floored) < floored),
        "the session issued at the watermark must survive it");

    // And every session that already existed does not - including one signed in earlier in the very
    // same second, which flooring alone let through. Somebody who got in moments before a reset kept
    // their session, which is precisely who a reset is aimed at.
    var sameSecond = ThroughACookieTicket(new DateTime(2026, 8, 26, 13, 24, 18, 100, DateTimeKind.Utc));
    var secondsBefore = ThroughACookieTicket(new DateTime(2026, 8, 26, 13, 24, 11, DateTimeKind.Utc));
    AssertTrue(sameSecond < floored, "a session from the same second must still be revoked");
    AssertTrue(secondsBefore < floored, "an older session must be revoked");
}

static void AnAddressAndItsTickMoveTogether()
{
    // The one failure worth a test of its own: an address that changed while the tick stayed put is a
    // verified address nobody verified, and it would let whoever typed it sign in as somebody else.
    var account = new PlayerAccount { Username = "sam" };
    account.SetEmail("sam@example.com");
    account.EmailVerified = true;
    account.EmailVerifiedAtUtc = new DateTime(2026, 8, 26, 0, 0, 0, DateTimeKind.Utc);

    account.SetEmail("someone.else@example.com");
    AssertEqual("someone.else@example.com", account.Email);
    AssertTrue(!account.EmailVerified, "changing the address must take the tick with it");
    AssertEqual(null, account.EmailVerifiedAtUtc);

    // Clearing it is the same move, and must not leave a tick behind either.
    account.EmailVerified = true;
    account.SetEmail(null);
    AssertEqual(null, account.Email);
    AssertTrue(!account.EmailVerified, "removing the address must take the tick with it");
}

static void AVerificationCodeLivesMinutesNotHours()
{
    // Six digits is a million possibilities. That is only safe because of the three numbers around it,
    // so those numbers are the test: a short window, a hard attempt cap, and a wait between sends.
    var options = new EmailOptions();
    AssertTrue(options.CodeLifetimeMinutes is > 0 and <= 30,
        $"a six-digit code held open for {options.CodeLifetimeMinutes} minutes is a long time to guess a million numbers in");
    AssertTrue(options.MaxAttempts is > 0 and <= 10, "a code needs a hard cap on guesses");
    AssertTrue(options.ResendCooldownSeconds >= 30, "without a cooldown the resend button is a mail cannon");

    var now = new DateTime(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);
    var code = new EmailVerification
    {
        ExpiresAtUtc = now.AddMinutes(options.CodeLifetimeMinutes),
        Attempts = 0,
    };

    AssertTrue(code.IsLive(now, options.MaxAttempts), "a fresh code should be worth typing");
    // Out of time.
    AssertTrue(!code.IsLive(code.ExpiresAtUtc.AddSeconds(1), options.MaxAttempts), "an expired code is dead");
    // Out of guesses, with time to spare - which is the case the clock alone would have missed.
    code.Attempts = options.MaxAttempts;
    AssertTrue(!code.IsLive(now, options.MaxAttempts), "a code guessed at too many times is dead");
    // Already spent. A code that worked once must never work twice.
    code.Attempts = 0;
    code.ConsumedAtUtc = now;
    AssertTrue(!code.IsLive(now, options.MaxAttempts), "a spent code is dead");
}

static void MailIsOffUntilItIsConfigured()
{
    AssertTrue(!new EmailOptions().IsConfigured, "no key should mean no delivery");
    AssertTrue(new EmailOptions { ApiKey = "re_something" }.IsConfigured, "a key should mean delivery");

    // The shipped file must carry no key, for the same reason it carries no Discord secret: it is
    // public. And it must still carry the numbers, because those are the safety and not the secret.
    var root = new DirectoryInfo(AppContext.BaseDirectory);
    while (root is not null && !File.Exists(Path.Combine(root.FullName, "StreetEmpire.sln")))
        root = root.Parent;
    AssertTrue(root is not null, "the solution root should be findable from the test binary");

    var shipped = new ConfigurationBuilder()
        .AddJsonFile(Path.Combine(root!.FullName, "Server", "StreetEmpire.Api", "appsettings.json"))
        .Build();
    var options = new EmailOptions();
    shipped.GetSection("Auth:Email").Bind(options);

    AssertTrue(!options.IsConfigured, "appsettings.json should never carry an email API key");
    AssertTrue(options.CodeLifetimeMinutes is > 0 and <= 30, "the shipped code window should still be short");
    AssertTrue(options.MaxAttempts is > 0 and <= 10, "the shipped attempt cap should still be a cap");
    AssertTrue(options.ResendCooldownSeconds >= 30, "the shipped cooldown should still be a cooldown");
    AssertTrue(!string.IsNullOrWhiteSpace(options.FromAddress), "the shipped file should still name a sender");
}

static void TheCodeEmailCarriesTheCodeAndEscapesTheName()
{
    // A player name is chosen by the player, so it reaches the HTML body as markup unless something
    // stops it. Nobody reading their own mail is the victim here - but the same copy is one refactor
    // away from being shown somewhere else, and escaping it costs nothing.
    var message = CodeEmail.Build(
        "sam@example.com", "<script>alert(1)</script>", "042137",
        VerificationPurpose.ConfirmAddress, 15, "Street Empire");

    AssertEqual("sam@example.com", message.To);
    AssertTrue(message.Subject.Contains("042137"), "the subject should carry the code, so it reads from a notification");
    AssertTrue(message.Text.Contains("042137"), "the text part should carry the code");
    AssertTrue(message.Html.Contains("042137"), "the html part should carry the code");
    AssertTrue(message.Text.Contains("15 minutes"), "the mail should say how long the code is good for");
    AssertTrue(!message.Html.Contains("<script>"), "a player name must never reach the body as markup");
    AssertTrue(message.Html.Contains("&lt;script&gt;"), "the name should still be shown, escaped");

    // Both flows use one builder, so both have to end up saying which of the two they are - a reset
    // mail that reads like a confirmation is a reset nobody realises they did not ask for.
    var reset = CodeEmail.Build(
        "sam@example.com", "Sam", "042137", VerificationPurpose.ResetPassword, 15, "Street Empire");

    AssertTrue(reset.Subject != message.Subject, "a reset should not look like a confirmation in an inbox");
    AssertTrue(reset.Subject.Contains("reset"), "the subject should say what the code is for");
    AssertTrue(reset.Text.Contains("new password"), "the body should say what the code is for");
    // The one line that matters to somebody who did not ask: a code alone changes nothing.
    AssertTrue(reset.Text.Contains("your password has not"), "an unasked-for reset mail should say nothing has happened yet");
}

static void AnEmailIsOnlyASecondNameForThePasswordDoor()
{
    // A confirmed address is a name you can type, not a key. It is the password beside it that opens
    // anything - so an account with an address and no password has no way in, and the check that
    // guards the last door must not be fooled into counting the address as one.
    var addressOnly = new PlayerAccount { Username = "sam", EmailVerified = true };
    addressOnly.Email = "sam@example.com";

    AssertTrue(!addressOnly.HasPassword, "an address is not a password");
    AssertTrue(!addressOnly.HasAnotherWayIn(), "an address alone is not a way in");

    // With a password it is a second name for the same door - and dropping the password drops both.
    var withPassword = new PlayerAccount { Username = "sam", PasswordHash = "hashed", EmailVerified = true };
    withPassword.Email = "sam@example.com";
    AssertTrue(withPassword.HasAnotherWayIn(), "a password is a way in");
    AssertTrue(!withPassword.HasAnotherWayIn(withoutPassword: true),
        "an address must not stand in for the password it is typed beside");
}

static void TheResendCooldownGuardsAnInboxNotAnAccount()
{
    // The cooldown exists to stop one inbox being hammered, so it is measured against the address the
    // code would go to. Measured against the account instead, a player who changed their address inside
    // the first minute was silently refused a code - left holding an address they had no way to confirm
    // and no explanation of why. That is the bug this is here for.
    var now = new DateTime(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);
    var sent = new EmailVerification { Email = "sam@example.com", CreatedAtUtc = now };
    const int cooldown = 60;

    // Same inbox, seconds later: wait.
    AssertTrue(EmailVerificationService.IsTooSoon(sent, "sam@example.com", now.AddSeconds(5), cooldown),
        "asking again for the same address inside the cooldown should wait");
    // Same inbox, after the minute: go.
    AssertTrue(!EmailVerificationService.IsTooSoon(sent, "sam@example.com", now.AddSeconds(61), cooldown),
        "the cooldown should end");
    // A different inbox is not the one being protected, whatever the clock says.
    AssertTrue(!EmailVerificationService.IsTooSoon(sent, "moved@example.com", now.AddSeconds(5), cooldown),
        "changing address should not be held up by a code sent somewhere else");
    // And nothing sent yet is never too soon.
    AssertTrue(!EmailVerificationService.IsTooSoon(null, "sam@example.com", now, cooldown),
        "a first code should never be held up");
}

static void ADotEnvFileReadsTheWayEveryOtherOneDoes()
{
    // The format is a convention rather than a specification, so what it accepts is worth pinning down:
    // somebody will paste a line out of a shell script or a password manager and expect it to work.
    var parsed = DotEnv.Parse([
        "# a comment",
        "",
        "   ",
        "Auth__Email__ApiKey=re_plain",
        "  Auth__Discord__ClientId = 12345  ",
        "export Auth__Discord__ClientSecret=exported",
        "Quoted=\"a value\"",
        "Single='literal'",
        "WithHash=before # after",
        "HashInsideQuotes=\"keeps#this\"",
        "Escaped=\"one\\ntwo\"",
        "Empty=",
        "Colon:Style:Key=works",
        "novalueline",
        "=novalue",
    ]).ToDictionary(x => x.Key, x => x.Value);

    AssertEqual("re_plain", parsed["Auth__Email__ApiKey"]);
    // Whitespace around both halves goes, because a lined-up file is a normal thing to write.
    AssertEqual("12345", parsed["Auth__Discord__ClientId"]);
    AssertEqual("exported", parsed["Auth__Discord__ClientSecret"]);
    AssertEqual("a value", parsed["Quoted"]);
    AssertEqual("literal", parsed["Single"]);
    // A trailing comment ends an unquoted value, and does not touch a quoted one - which is what lets
    // a secret with a # in it survive being written down.
    AssertEqual("before", parsed["WithHash"]);
    AssertEqual("keeps#this", parsed["HashInsideQuotes"]);
    AssertEqual("one\ntwo", parsed["Escaped"]);
    AssertEqual("", parsed["Empty"]);
    AssertEqual("works", parsed["Colon:Style:Key"]);

    // A line that is not a setting is skipped rather than thrown over. One bad line in a config file
    // must never be the reason a server will not boot.
    AssertTrue(!parsed.ContainsKey("novalueline"), "a line with no = is not a setting");
    AssertTrue(!parsed.ContainsKey(""), "a line with no name is not a setting");
    AssertEqual(10, parsed.Count);
}

static void TheRealEnvironmentAlwaysBeatsTheFile()
{
    // The rule that matters in production: a platform injecting a secret as an environment variable
    // must win over a .env that found its way into an image. Every dotenv implementation works this
    // way round, and getting it backwards would be a silent downgrade to whatever was committed.
    var directory = Directory.CreateTempSubdirectory("street-empire-dotenv");
    var alreadySet = "STREET_EMPIRE_TEST_ALREADY_SET";
    var fromFile = "STREET_EMPIRE_TEST_FROM_FILE";
    try
    {
        File.WriteAllLines(Path.Combine(directory.FullName, ".env"),
        [
            $"{alreadySet}=from-the-file",
            $"{fromFile}=from-the-file",
        ]);

        Environment.SetEnvironmentVariable(alreadySet, "from-the-environment");
        Environment.SetEnvironmentVariable(fromFile, null);

        var previous = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(directory.FullName);
            var result = DotEnv.Load();

            AssertTrue(result.Found, "a .env in the working directory should be found");
            AssertEqual(1, result.Applied);
            AssertEqual(1, result.SkippedBecauseAlreadySet);
            AssertEqual("from-the-environment", Environment.GetEnvironmentVariable(alreadySet));
            AssertEqual("from-the-file", Environment.GetEnvironmentVariable(fromFile));
        }
        finally
        {
            Directory.SetCurrentDirectory(previous);
        }
    }
    finally
    {
        Environment.SetEnvironmentVariable(alreadySet, null);
        Environment.SetEnvironmentVariable(fromFile, null);
        directory.Delete(recursive: true);
    }
}

static void TheCommittedExampleHoldsNoSecrets()
{
    var root = new DirectoryInfo(AppContext.BaseDirectory);
    while (root is not null && !File.Exists(Path.Combine(root.FullName, "StreetEmpire.sln")))
        root = root.Parent;
    AssertTrue(root is not null, "the solution root should be findable from the test binary");

    // .env is where the secrets go, so it must never be committable. This is the one line standing
    // between a working setup and a published API key.
    var ignore = File.ReadAllLines(Path.Combine(root!.FullName, ".gitignore")).Select(x => x.Trim());
    AssertTrue(ignore.Contains(".env"), ".gitignore must ignore .env");

    var examplePath = Path.Combine(root.FullName, ".env.example");
    AssertTrue(File.Exists(examplePath), ".env.example should be committed so there is something to copy");

    // Every key the example sets a value for must be empty. A template that ships a filled-in secret
    // is the exact failure the template exists to prevent, and it would be copied into every .env.
    foreach (var (key, value) in DotEnv.Parse(File.ReadAllLines(examplePath)))
        AssertTrue(value.Length == 0, $".env.example should leave {key} empty, not carry '{value}'");

    // And the names in it have to be real, or somebody fills the file in and nothing happens. Checked
    // against the options themselves rather than a list written out twice.
    var text = File.ReadAllText(examplePath);
    foreach (var expected in new[]
    {
        $"Auth__Discord__{nameof(DiscordOptions.ClientId)}",
        $"Auth__Discord__{nameof(DiscordOptions.ClientSecret)}",
        $"Auth__Discord__{nameof(DiscordOptions.RedirectUri)}",
        $"Auth__Email__{nameof(EmailOptions.ApiKey)}",
        $"Auth__Email__{nameof(EmailOptions.FromAddress)}",
        $"Auth__Email__{nameof(EmailOptions.CodeLifetimeMinutes)}",
    })
        AssertTrue(text.Contains(expected), $".env.example should name {expected}");
}

static void EveryAccountChangeHasCopyOfItsOwn()
{
    // The guard for the next one somebody adds. A new enum value with no arm written for it falls
    // through to "Something on your account changed", which is a notice that tells the reader nothing
    // and would ship without anybody noticing.
    var subjects = new List<string>();
    foreach (var change in Enum.GetValues<AccountChange>())
    {
        var message = AccountNoticeEmail.Build("sam@example.com", "Sam", change, null, DateTime.UtcNow, "Street Empire");

        AssertTrue(!message.Subject.Contains("your account changed"),
            $"{change} has no copy of its own and fell through to the generic subject");
        AssertTrue(!message.Text.Contains("Something on your account changed"),
            $"{change} has no copy of its own and fell through to the generic sentence");
        AssertTrue(message.Subject.StartsWith("Street Empire:", StringComparison.Ordinal),
            $"{change} should say who is writing");
        subjects.Add(message.Subject);
    }

    // Distinct, because two changes sharing a subject line is two changes one of them cannot be told
    // apart from in an inbox.
    AssertEqual(subjects.Count, subjects.Distinct().Count());
    AssertTrue(subjects.Count >= 9, "every way in should have a notice behind it");
}

static void ANoticeSaysWhatHappenedAndNeverWhatItWas()
{
    // The rule the whole file is written to. A mailbox is not a secure channel, so a notice reports a
    // change and never carries the change: no new password, no verification code, no token. A notice
    // that leaked what it reported would be worse than no notice.
    var message = AccountNoticeEmail.Build(
        "sam@example.com", "Sam", AccountChange.PasswordChanged, null,
        new DateTime(2026, 8, 26, 14, 5, 0, DateTimeKind.Utc), "Street Empire");

    AssertTrue(message.Text.Contains("14:05 UTC"), "a notice should say when, or it cannot be checked against memory");
    AssertTrue(message.Text.Contains("Every other session was signed out."), "it should say what else the change did");
    // The advice has to work for the reader who has already lost the account, which is who most needs
    // it. "Sign in and change your password" is useless to somebody whose password was just changed
    // out from under them, so the route named has to be the one that works without it.
    AssertTrue(message.Text.Contains("reset your password from the sign-in screen"),
        "the advice should name the route that works when you are already locked out");
    AssertTrue(message.Text.Contains("Discord connection you do not recognise"),
        "it should also point at the other way in somebody could have left behind");

    // The detail is somebody else's text - a Discord handle is chosen by its owner - so it never
    // reaches the body as markup.
    var hostile = AccountNoticeEmail.Build(
        "sam@example.com", "Sam", AccountChange.DiscordConnected, "<script>alert(1)</script>",
        DateTime.UtcNow, "Street Empire");

    AssertTrue(hostile.Text.Contains("<script>"), "the text part is not markup and needs no escaping");
    AssertTrue(!hostile.Html.Contains("<script>"), "a handle must never reach the html body as markup");
    AssertTrue(hostile.Html.Contains("&lt;script&gt;"), "the handle should still be shown, escaped");

    // Naming the address it moved to is the point of that notice: it is how somebody sees where their
    // account went.
    var moved = AccountNoticeEmail.Build(
        "old@example.com", "Sam", AccountChange.EmailChanged, "new@example.com", DateTime.UtcNow, "Street Empire");
    AssertTrue(moved.Text.Contains("new@example.com"), "the notice should name where the account moved to");
}

static void NoticesOnlyGoToProvenAddresses()
{
    // An unconfirmed address may belong to a stranger who was typed in by mistake or on purpose.
    // Mailing them about somebody else's account is a nuisance to them and a spam complaint against
    // the sending domain - which would eventually stop verification codes arriving for everybody.
    var sent = new RecordingEmailSender();
    var notices = new AccountNotices(sent, Options(new EmailOptions()), NullLogger<AccountNotices>.Instance);

    var unconfirmed = new PlayerAccount { Username = "sam" };
    unconfirmed.SetEmail("sam@example.com");
    notices.TellAccountAsync(unconfirmed, AccountChange.PasswordChanged, null, default).GetAwaiter().GetResult();
    AssertEqual(0, sent.Messages.Count);

    var noAddress = new PlayerAccount { Username = "sam" };
    notices.TellAccountAsync(noAddress, AccountChange.PasswordChanged, null, default).GetAwaiter().GetResult();
    AssertEqual(0, sent.Messages.Count);

    var confirmed = new PlayerAccount { Username = "sam", EmailVerified = true };
    confirmed.Email = "sam@example.com";
    notices.TellAccountAsync(confirmed, AccountChange.PasswordChanged, null, default).GetAwaiter().GetResult();
    AssertEqual(1, sent.Messages.Count);
    AssertEqual("sam@example.com", sent.Messages[0].To);

    // The switch exists for a load test against a real provider, not for ordinary use.
    var quiet = new AccountNotices(sent, Options(new EmailOptions { SendSecurityNotices = false }), NullLogger<AccountNotices>.Instance);
    quiet.TellAccountAsync(confirmed, AccountChange.PasswordChanged, null, default).GetAwaiter().GetResult();
    AssertEqual(1, sent.Messages.Count);
}

static void TheAddressBeingLeftBehindGetsTold()
{
    // The most important notice in the set, and the one easiest to get backwards. Changing the address
    // is how somebody who has taken an account keeps it: the owner is cut off and never hears. Telling
    // only the new address would be telling the thief.
    var sent = new RecordingEmailSender();
    var notices = new AccountNotices(sent, Options(new EmailOptions()), NullLogger<AccountNotices>.Instance);

    notices.TellFormerAddressAsync("old@example.com", "Sam", AccountChange.EmailChanged, "new@example.com", default)
        .GetAwaiter().GetResult();

    AssertEqual(1, sent.Messages.Count);
    AssertEqual("old@example.com", sent.Messages[0].To);
    AssertTrue(sent.Messages[0].Text.Contains("new@example.com"), "it should name where the account went");

    // A send that fails must never throw, because the change it reports has already happened and been
    // saved - a provider being down cannot be allowed to answer an error for a password that really did
    // change.
    var broken = new AccountNotices(new ThrowingEmailSender(), Options(new EmailOptions()), NullLogger<AccountNotices>.Instance);
    broken.TellFormerAddressAsync("old@example.com", "Sam", AccountChange.EmailRemoved, null, default)
        .GetAwaiter().GetResult();
}

static void ACodeIsOnlyGoodForWhatItWasSentFor()
{
    // Confirming an address and resetting a password share a table, and the purpose column is the only
    // thing keeping them apart. Without it a code mailed to confirm an address - which the mail calls
    // harmless - could be typed into the reset form and become a new password.
    AssertEqual(2, Enum.GetValues<VerificationPurpose>().Length);

    var confirm = new EmailVerification { Purpose = VerificationPurpose.ConfirmAddress };
    var reset = new EmailVerification { Purpose = VerificationPurpose.ResetPassword };
    AssertTrue(confirm.Purpose != reset.Purpose, "the two purposes must be distinguishable on the row");

    // Existing rows predate the column, and every one of them was a confirmation. The migration has to
    // say so, because the empty string EF would otherwise write is not a name the enum reads back from.
    var root = new DirectoryInfo(AppContext.BaseDirectory);
    while (root is not null && !File.Exists(Path.Combine(root.FullName, "StreetEmpire.sln")))
        root = root.Parent;
    AssertTrue(root is not null, "the solution root should be findable from the test binary");

    var migration = Directory
        .GetFiles(Path.Combine(root!.FullName, "Server", "StreetEmpire.Api", "Migrations"), "*_PasswordResetCodes.cs")
        .Single(x => !x.EndsWith(".Designer.cs", StringComparison.Ordinal));
    var text = File.ReadAllText(migration);
    AssertTrue(text.Contains($"defaultValue: \"{nameof(VerificationPurpose.ConfirmAddress)}\""),
        "the purpose column must backfill existing rows with a name the enum can be read back from");
}

static void AResetNeedsAProvenAddress()
{
    // The two flows have exactly opposite preconditions, and getting either backwards is a hole:
    // confirming an address that is already confirmed is pointless, and resetting against an address
    // nobody proved would mail a way into the account to whoever typed the address in.
    var unproven = new PlayerAccount { Username = "sam" };
    unproven.SetEmail("sam@example.com");

    var proven = new PlayerAccount { Username = "sam", EmailVerified = true };
    proven.Email = "sam@example.com";

    var noAddress = new PlayerAccount { Username = "sam" };

    AssertEqual(SendCodeResult.AddressNotConfirmed, Precondition(unproven, VerificationPurpose.ResetPassword));
    AssertEqual(SendCodeResult.Sent, Precondition(proven, VerificationPurpose.ResetPassword));
    AssertEqual(SendCodeResult.Sent, Precondition(unproven, VerificationPurpose.ConfirmAddress));
    AssertEqual(SendCodeResult.AlreadyVerified, Precondition(proven, VerificationPurpose.ConfirmAddress));
    AssertEqual(SendCodeResult.NoAddress, Precondition(noAddress, VerificationPurpose.ResetPassword));
    AssertEqual(SendCodeResult.NoAddress, Precondition(noAddress, VerificationPurpose.ConfirmAddress));

    // Mirrors the gate at the top of SendAsync. Kept here rather than reaching for a database, because
    // the decision is about the account alone and this suite runs without one.
    static SendCodeResult Precondition(PlayerAccount account, VerificationPurpose purpose)
    {
        if (account.Email is null) return SendCodeResult.NoAddress;
        return purpose switch
        {
            VerificationPurpose.ConfirmAddress when account.EmailVerified => SendCodeResult.AlreadyVerified,
            VerificationPurpose.ResetPassword when !account.EmailVerified => SendCodeResult.AddressNotConfirmed,
            _ => SendCodeResult.Sent,
        };
    }
}

static void ADiscordSignUpIsAWholeAccount()
{
    // The second door into the game, and the one that is easiest to leave half-built: Discord answers
    // who somebody is and has no opinion about what they want to be called, which town they set up in,
    // or how they get back in if they lose the Discord. All three are the game's to ask.
    var options = Resolve(new GameOptions { StartingCash = 5_000, StartingPimps = 1, StartingTurns = 200 });

    var account = new PlayerAccount
    {
        Username = "streetking",
        PasswordHash = string.Empty,
        DiscordUserId = "555000111222",
        DiscordUsername = "StreetKing",
        DiscordLinkedAtUtc = DateTime.UtcNow,
    };
    account.SetEmail("street.king@example.com");
    var (player, log) = AccountSetup.NewPlayer(account, "Street King", "Las Vegas", options, CreateRoster(options));

    // A whole player, not a stub: the same starting hand the register form deals.
    AssertEqual(options.StartingCash, player.Cash);
    AssertEqual(options.StartingTurns, player.Turns);
    AssertEqual("Las Vegas", player.City);
    AssertTrue(player.Hideout is not null, "a Discord sign-up should get a hideout like anybody else");
    AssertEqual(options.StartingPimps, player.Crew.Count);
    AssertEqual("START", log.Action);

    // No password, on purpose - nobody chose one - and an address that is not confirmed yet, because
    // typing it is not proving it. Both are true of a fresh Discord account and both are what the
    // account page is then arranged around.
    AssertTrue(!account.HasPassword, "a Discord sign-up has not chosen a password");
    AssertTrue(!account.EmailVerified, "an address typed at sign-up is not a proved one");

    // Discord is the only thing holding it, so it cannot be taken away. This is the rule that stops an
    // account made this way being closed by the one control on the page that would close it.
    AssertTrue(!account.HasAnotherWayIn(withoutDiscord: true),
        "Discord must not be removable while it is the only way in");

    // And the address is what makes that recoverable: confirmed, it can be reset from, which is the
    // whole reason it is asked for at sign-up rather than left to a page nobody visits.
    account.EmailVerified = true;
    AssertTrue(account.Email is not null && account.EmailVerified,
        "a confirmed address is the second way back into a Discord-made account");
}

static void EveryTestWrittenIsATestThatRuns()
{
    var root = new DirectoryInfo(AppContext.BaseDirectory);
    while (root is not null && !File.Exists(Path.Combine(root.FullName, "StreetEmpire.sln")))
        root = root.Parent;
    AssertTrue(root is not null, "the solution root should be findable from the test binary");

    var path = Path.Combine(root!.FullName, "Tests", "StreetEmpire.Tests", "Program.cs");
    AssertTrue(File.Exists(path), $"this suite should be able to read itself at {path}");
    var source = File.ReadAllText(path);

    var manifest = Regex.Match(source, @"var tests = new \(string Name, Action Test\)\[\]\s*\{(.*?)\n\};", RegexOptions.Singleline);
    AssertTrue(manifest.Success, "the manifest should be findable");

    var registered = Regex.Matches(manifest.Groups[1].Value, @",\s*(\w+)\s*\)")
        .Select(x => x.Groups[1].Value)
        .ToList();

    // Nothing listed twice: a duplicate is a test counted twice and a name somebody meant to change.
    var duplicated = registered.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
    AssertTrue(duplicated.Count == 0, $"listed more than once: {string.Join(", ", duplicated)}");

    var listed = registered.ToHashSet();
    var orphans = Regex.Matches(source, @"^static void (\w+)\(\)", RegexOptions.Multiline)
        .Select(x => x.Groups[1].Value)
        .Where(name => !listed.Contains(name))
        // A body nothing lists but something calls is a helper, not a test nobody runs.
        .Where(name => Regex.Matches(source, $@"\b{name}\b").Count <= 1)
        .ToList();

    AssertTrue(orphans.Count == 0, $"written but never run: {string.Join(", ", orphans)}");
    AssertEqual(registered.Count, listed.Count);
}

static void ShippedSettingsObeyTheSameRules()
{
    // Read the file in the server project, never the copy sitting beside this test binary. The build
    // drops one there, and a test that reads it passes against whatever was last compiled - which is
    // precisely how a stale copy convinced this suite the ladder had been updated when it had not.
    var root = new DirectoryInfo(AppContext.BaseDirectory);
    while (root is not null && !File.Exists(Path.Combine(root.FullName, "StreetEmpire.sln")))
        root = root.Parent;
    AssertTrue(root is not null, "the solution root should be findable from the test binary");

    var path = Path.Combine(root!.FullName, "Server", "StreetEmpire.Api", "appsettings.json");
    AssertTrue(File.Exists(path), $"the server's own appsettings.json should be readable at {path}");

    var shipped = new ConfigurationBuilder().AddJsonFile(path).Build();
    var options = new GameOptions();
    shipped.GetSection("Game").Bind(options);
    options.ApplyWeaponDefaultsWhereEmpty();
    options.StreetAction.ApplyDistrictDefaultsWhereEmpty();
    options.Alliances.ApplyDefaultsWhereEmpty();
    options.Hideout.ApplyDefaultsWhereEmpty();
    options.Territory.ApplyDefaultsWhereEmpty();
    options.CityMarkets.ApplyDefaultsWhereEmpty(options.Territory.Cities());

    // The file is meant to be carrying real values, not to have quietly emptied out.
    AssertTrue(options.Hideout.Storage.Count > 0, "the shipped settings carry a storage ladder");
    AssertTrue(options.Hideout.Tiers.Count > 0, "the shipped settings carry building tiers");

    var hideouts = new HideoutService(Snapshot(options));
    var morale = options.Morale;

    // The promise: every crew the game allows is a crew it can supply for a full shift. Checked here
    // against the numbers the server will actually boot with.
    foreach (var tier in options.Hideout.Tiers)
    foreach (var storage in options.Hideout.Storage.Where(x => x.MinTier <= tier.Level))
    {
        var capacity = hideouts.CapacityFor(new Hideout { Tier = tier.Level, StorageLevel = storage.Level });
        var condomsNeeded = (int)Math.Ceiling(capacity.MaxHoes * options.MaxActionTurns / morale.TurnsPerCondom);
        var beerNeeded = (int)Math.Ceiling(capacity.MaxThugs * options.MaxActionTurns / morale.TurnsPerBeer);
        AssertTrue(condomsNeeded <= storage.Condoms,
            $"shipped: tier {tier.Level} store {storage.Level} allows {capacity.MaxHoes} hoes needing {condomsNeeded} condoms, room holds {storage.Condoms}");
        AssertTrue(beerNeeded <= storage.Beer,
            $"shipped: tier {tier.Level} store {storage.Level} allows {capacity.MaxThugs} thugs needing {beerNeeded} beer, room holds {storage.Beer}");
    }

    // A starting player gets a crew worth running rather than a room that supplies a fifth of a shift.
    var starting = hideouts.CapacityFor(new Hideout { Tier = 1, StorageLevel = 1 });
    AssertTrue(starting.MaxHoes >= 25, $"a starting house supports a real crew, saw {starting.MaxHoes} hoes");

    // Every rung has to be reachable: a room gated behind a building nobody can be standing in is a
    // dead end, and a level whose cost nobody can pay is the same thing with extra steps.
    var topTier = options.Hideout.Tiers.Max(x => x.Level);
    foreach (var storage in options.Hideout.Storage)
        AssertTrue(storage.MinTier <= topTier,
            $"shipped: storage {storage.Level} needs tier {storage.MinTier} and the game stops at {topTier}");

    // And the two halves of the config should not have drifted apart at all. Where the code carries a
    // default and the file carries a value, they are two statements of the same intent, and whichever
    // of them is wrong the players are getting the file. Compared over every room by reflection rather
    // than over the one that happened to break, because the next drift will be in a different list.
    var defaults = Resolve(null);
    var lists = 0;
    foreach (var property in typeof(HideoutOptions).GetProperties())
    {
        if (property.GetValue(defaults.Hideout) is not System.Collections.IList fromCode) continue;
        if (property.GetValue(options.Hideout) is not System.Collections.IList fromFile) continue;
        if (fromCode.Count == 0) continue;

        AssertEqual(fromCode.Count, fromFile.Count);
        for (var i = 0; i < fromCode.Count; i++)
        {
            var code = fromCode[i]!;
            var file = fromFile[i]!;
            foreach (var field in code.GetType().GetProperties())
            {
                var expected = field.GetValue(code);
                var actual = field.GetValue(file);
                AssertTrue(Equals(expected, actual),
                    $"{property.Name}[{i}].{field.Name}: code says {expected}, appsettings ships {actual}");
            }
        }
        lists++;
    }

    AssertTrue(lists >= 8, $"every hideout list should be compared, saw {lists}");
}

static void CrewIsCappedByWhicheverRunsOutFirst()
{
    var options = Resolve(null);
    var hideouts = CreateHideouts(options);
    var morale = options.Morale;
    var tier1 = options.Hideout.Tiers.Single(x => x.Level == 1);

    int Supported(int shelf, double per) => (int)Math.Floor(shelf * per / options.MaxActionTurns);

    // The store is the binding constraint while it is the smaller of the two, and it is what the
    // player is actually told.
    var starting = hideouts.CapacityFor(new Hideout { Tier = 1, StorageLevel = 1 });
    var store1 = options.Hideout.Storage.Single(x => x.Level == 1);
    AssertEqual(Supported(store1.Condoms, morale.TurnsPerCondom), starting.MaxHoes);
    AssertEqual(Supported(store1.Beer, morale.TurnsPerBeer), starting.MaxThugs);
    AssertEqual(25, starting.MaxHoes);
    AssertTrue(starting.MaxHoes < tier1.MaxHoes, "a starting store cannot fill a starting house");

    // Deepening the store raises the crew, which is the whole point of the coupling.
    var deeper = hideouts.CapacityFor(new Hideout { Tier = 1, StorageLevel = 2 });
    AssertTrue(deeper.MaxHoes > starting.MaxHoes, "a bigger room is a bigger crew");
    AssertEqual(tier1.MaxHoes, deeper.MaxHoes);
    AssertEqual(tier1.MaxThugs, deeper.MaxThugs);

    // And the building takes over as the ceiling the moment the store passes it, so no amount of
    // shelving fits more people into a Trap House than it has room for.
    foreach (var storage in options.Hideout.Storage)
    {
        var capacity = hideouts.CapacityFor(new Hideout { Tier = 1, StorageLevel = storage.Level });
        AssertTrue(capacity.MaxHoes <= tier1.MaxHoes, $"store {storage.Level} cannot outgrow the building");
        AssertTrue(capacity.MaxThugs <= tier1.MaxThugs, $"store {storage.Level} cannot outgrow the building on thugs");
    }

    // Every crew the game allows is a crew it can supply for a full action, at every combination of
    // building and room. This is the promise the whole change exists to keep.
    foreach (var tier in options.Hideout.Tiers)
    foreach (var storage in options.Hideout.Storage.Where(x => x.MinTier <= tier.Level))
    {
        var capacity = hideouts.CapacityFor(new Hideout { Tier = tier.Level, StorageLevel = storage.Level });
        var condomsNeeded = (int)Math.Ceiling(capacity.MaxHoes * options.MaxActionTurns / morale.TurnsPerCondom);
        var beerNeeded = (int)Math.Ceiling(capacity.MaxThugs * options.MaxActionTurns / morale.TurnsPerBeer);
        AssertTrue(condomsNeeded <= storage.Condoms,
            $"tier {tier.Level} store {storage.Level}: {capacity.MaxHoes} hoes need {condomsNeeded} condoms, room holds {storage.Condoms}");
        AssertTrue(beerNeeded <= storage.Beer,
            $"tier {tier.Level} store {storage.Level}: {capacity.MaxThugs} thugs need {beerNeeded} beer, room holds {storage.Beer}");
    }

    // Pimps are the exception, and deliberately: nothing supplies a pimp, so only the building can run
    // out of room for one.
    foreach (var storage in options.Hideout.Storage)
        AssertEqual(tier1.MaxPimps, hideouts.CapacityFor(new Hideout { Tier = 1, StorageLevel = storage.Level }).MaxPimps);
}

static void StorageLevelsMatchTheCrewCapsTheyUnlock()
{
    var options = Resolve(null);
    var morale = options.Morale;

    foreach (var tier in options.Hideout.Tiers)
    {
        var unlocked = options.Hideout.Storage.Where(x => x.MinTier == tier.Level).MaxBy(x => x.Level);
        AssertTrue(unlocked is not null, $"tier {tier.Level} should unlock a storage level");

        var condomsNeeded = (int)Math.Ceiling(tier.MaxHoes * options.MaxActionTurns / morale.TurnsPerCondom);
        var beerNeeded = (int)Math.Ceiling(tier.MaxThugs * options.MaxActionTurns / morale.TurnsPerBeer);

        AssertEqual(condomsNeeded, unlocked!.Condoms);
        AssertEqual(beerNeeded, unlocked.Beer);
        AssertEqual(tier.MaxThugs, unlocked.Weapons);
    }
}

/// <summary>
/// Walks the whole hideout ladder with the money in the bank, buying every tier and every room level
/// in order. Nothing may refuse.
///
/// This exists because prices and the safe that holds them are tuned in the same table, and it is easy
/// to price a room above the safe one level below it. A level 3 safe cost $120,000 against a level 2
/// safe holding $100,000, and a level 3 coke lab cost $150,000 against the same $100,000, so both were
/// unbuyable and everything gated behind the safe was unreachable. Charging the bank fixed it; this
/// test is what stops it coming back.
/// </summary>
static void EveryHideoutUpgradeIsReachable()
{
    var options = Resolve(null);
    var hideouts = CreateHideouts(options);
    var now = new DateTime(2026, 8, 14, 0, 0, 0, DateTimeKind.Utc);
    var player = new Player { BankCash = 100_000_000, Turns = 200, Hideout = new Hideout() };

    var rooms = new[] { "storage", "safe", "workshop", "weedlab", "cokelab" };
    var topTier = options.Hideout.Tiers.Max(x => x.Level);

    for (var tier = 1; ; tier++)
    {
        foreach (var room in rooms)
            while (hideouts.NextUpgrade(player.Hideout, room) is { Locked: false })
            {
                var before = hideouts.NextUpgrade(player.Hideout, room)!.Level;
                hideouts.Upgrade(player, room, now);
                AssertTrue(
                    hideouts.NextUpgrade(player.Hideout, room)?.Level != before,
                    $"buying {room} level {before} should move it along");
            }

        if (tier >= topTier) break;

        player.Turns = 200;
        hideouts.Upgrade(player, "tier", now);
        AssertTrue(hideouts.CompleteBuild(player.Hideout, now.AddDays(1)), $"the tier {tier + 1} build should land");
        AssertEqual(tier + 1, player.Hideout!.Tier);
    }

    // Everything in the tables is now owned, which is the point: no level is stranded.
    AssertEqual(topTier, player.Hideout!.Tier);
    AssertEqual(options.Hideout.Storage.Max(x => x.Level), player.Hideout.StorageLevel);
    AssertEqual(options.Hideout.Workshop.Max(x => x.Level), player.Hideout.WorkshopLevel);
    AssertEqual(options.Hideout.Safe.Max(x => x.Level), player.Hideout.SafeLevel);
    AssertEqual(options.Hideout.WeedLab.Max(x => x.Level), player.Hideout.WeedLabLevel);
    AssertEqual(options.Hideout.CokeLab.Max(x => x.Level), player.Hideout.CokeLabLevel);
    AssertTrue(player.Cash >= 0 && player.BankCash >= 0, "nothing should have been bought on credit");
}

static void LabsProduceWhileAway()
{
    var options = Resolve(null);
    options.Hideout.MaxOfflineProductionHours = 12;
    var hideouts = CreateHideouts(options);
    var start = new DateTime(2026, 8, 13, 0, 0, 0, DateTimeKind.Utc);
    // Level 1 weed lab makes 2 an hour; storage level 2 holds 100 weed.
    var player = new Player
    {
        Hideout = new Hideout { StorageLevel = 2, WeedLabLevel = 1, LabsCollectedAtUtc = start }
    };

    // Part of an hour pays nothing and leaves the clock alone, so the remainder is not thrown away.
    AssertTrue(!hideouts.AccrueLabs(player, start.AddMinutes(59)).ClockMoved, "a partial hour is not banked");
    AssertEqual(0, player.Weed);

    var threeHours = hideouts.AccrueLabs(player, start.AddHours(3));
    AssertEqual(6, threeHours.Weed);
    AssertEqual(6, player.Weed);

    // The same instant a second time pays nothing: the clock moved with the first run.
    AssertEqual(0, hideouts.AccrueLabs(player, start.AddHours(3)).Weed);
    AssertEqual(6, player.Weed);

    // A week away is charged at the ceiling, and the leftover days are not owed later.
    var week = hideouts.AccrueLabs(player, start.AddDays(7));
    AssertEqual(24, week.Weed);
    AssertTrue(week.HitOfflineCeiling, "the offline ceiling should be reported");
    AssertEqual(0, hideouts.AccrueLabs(player, start.AddDays(7)).Weed);

    // Storage is a wall, not a spill: a full room stops production instead of destroying stock.
    player.Weed = 99;
    var full = hideouts.AccrueLabs(player, start.AddDays(8));
    AssertEqual(1, full.Weed);
    AssertEqual(100, player.Weed);
}

static void LabsStartTheirClockWhenBuilt()
{
    var hideouts = CreateHideouts();
    var built = new DateTime(2026, 8, 13, 6, 0, 0, DateTimeKind.Utc);
    // A hideout founded long ago that only just built a lab.
    var player = new Player { Hideout = new Hideout { StorageLevel = 3, WeedLabLevel = 3, WorkshopLevel = 2, CreatedAtUtc = built.AddDays(-30) } };

    var first = hideouts.AccrueLabs(player, built);
    AssertEqual(0, first.Weed);
    AssertTrue(first.ClockMoved, "the first run starts the clock and has to be saved");
    AssertEqual(built, player.Hideout!.LabsCollectedAtUtc);

    AssertEqual(7, hideouts.AccrueLabs(player, built.AddHours(1)).Weed);
}

static void MoraleTrendReportsDirection()
{
    var options = new MoraleOptions { TrendWindowHours = 3, TrendFlatBand = 0.25 };
    var player = new Player { HoeHappiness = 72, ThugHappiness = 44 };

    var rising = ToMoraleTrend(player, 60, 50, options);
    AssertEqual(12.0, rising.HoeDelta ?? 0);
    AssertEqual("up", rising.HoeDirection);
    AssertEqual(-6.0, rising.ThugDelta ?? 0);
    AssertEqual("down", rising.ThugDirection);
    AssertEqual(3, rising.WindowHours);

    // Inside the flat band the arrow reads steady, so ordinary drift does not make it flicker.
    var drift = ToMoraleTrend(player, 71.9, 43.9, options);
    AssertEqual("steady", drift.HoeDirection);
    AssertEqual("steady", drift.ThugDirection);

    // Exactly on the band counts as movement, so the band is a floor rather than a dead zone.
    AssertEqual("up", ToMoraleTrend(player, 71.75, 44, options).HoeDirection);
    AssertEqual("down", ToMoraleTrend(player, 72.25, 45, options).HoeDirection);

    // Reported from a live game: a crew recovering 0.7 an action read as steady on a one point band,
    // so the arrow sat still while morale visibly climbed.
    AssertEqual("up", ToMoraleTrend(new Player { HoeHappiness = 47.9 }, 47.2, 47.2, options).HoeDirection);

    // No baseline is not the same as steady, and must not be dressed up as one.
    var unknown = ToMoraleTrend(player, null, null, options);
    AssertEqual("unknown", unknown.HoeDirection);
    AssertEqual("unknown", unknown.ThugDirection);
    AssertTrue(unknown.HoeDelta is null && unknown.ThugDelta is null, "an unknown trend carries no delta");
}

static void WorldNewsKeepsFightsAndDropsNoise()
{
    var options = new WorldNewsOptions { MinCashSwing = 25_000, MinCrewChange = 5 };
    var since = new DateTime(2026, 8, 12, 0, 0, 0, DateTimeKind.Utc);
    var newsworthy = WorldNews.IsNewsworthy(options, since).Compile();
    var now = since.AddHours(1);

    AssertTrue(newsworthy(new GameActionLog { Action = "ATTACK", CreatedAtUtc = now }), "fights are always news");
    AssertTrue(newsworthy(new GameActionLog { Action = "HIDEOUT", CreatedAtUtc = now }), "buildings are always news");
    AssertTrue(!newsworthy(new GameActionLog { Action = "ATTACK", CreatedAtUtc = since.AddHours(-1) }), "old rows fall out of the window");
    AssertTrue(!newsworthy(new GameActionLog { Action = "STREET", CreatedAtUtc = now, CashDelta = 900 }), "an ordinary shift is not news");
    AssertTrue(newsworthy(new GameActionLog { Action = "SALE", CreatedAtUtc = now, CashDelta = 40_000 }), "a big score is news");
    AssertTrue(newsworthy(new GameActionLog { Action = "STREET", CreatedAtUtc = now, CashDelta = -30_000 }), "a big loss is news too");

    // A deposit moves cash into the bank and nets zero. Moving your own money is not a story.
    AssertTrue(!newsworthy(new GameActionLog { Action = "BANK", CreatedAtUtc = now, CashDelta = -80_000, BankDelta = 80_000 }), "a deposit is bookkeeping");

    AssertTrue(newsworthy(new GameActionLog { Action = "CREW", CreatedAtUtc = now, HoesDelta = 6 }), "a real hiring run is news");
    AssertTrue(!newsworthy(new GameActionLog { Action = "CREW", CreatedAtUtc = now, HoesDelta = 2 }), "hiring two is not");
    AssertTrue(newsworthy(new GameActionLog { Action = "CREW", CreatedAtUtc = now, PimpsDelta = -1 }), "a named pimp leaving is news");
    AssertTrue(!newsworthy(new GameActionLog { Action = "LAB", CreatedAtUtc = now, WeedDelta = 84 }), "passive lab output is not news");
    AssertTrue(!newsworthy(new GameActionLog { Action = "ADMIN", CreatedAtUtc = now, CashDelta = 500_000 }), "admin action never reaches the feed");

    AssertTrue(newsworthy(new GameActionLog { Action = "TERRITORY", CreatedAtUtc = now, Summary = "Took over Hunts Point." }), "ground changing hands is news");
    AssertTrue(!newsworthy(new GameActionLog { Action = "GROUND", CreatedAtUtc = now, Summary = "X took Y from you." }),
        "a notice written to one player is not published to everyone");
    AssertEqual("ground", WorldNews.Category("TERRITORY"));
    AssertEqual("combat", WorldNews.Category("ATTACK"));
    AssertEqual("build", WorldNews.Category("HIDEOUT"));
    AssertEqual("money", WorldNews.Category("SALE"));
}

/// <summary>
/// Every type is a percentage on an activity the player still spends turns on, and holding two of a
/// kind is worth twice as much. Storage was the original fourth type and became a stash house instead:
/// capacity is read in seventeen places that must agree, and two authorities disagreeing about a cap
/// is how the hideout bugs happened.
/// </summary>
/// <summary>
/// The store, production, admin adjustments and the market all move the same piles. One mapping, or a
/// good lands in one place and not another and the market can take what it cannot give back.
/// </summary>
static void TradeGoodsMapKeysToPiles()
{
    var player = new Player { Condoms = 1, Beer = 2, Pistols = 3, Weed = 4, Coke = 5 };
    var capacity = CreateHideouts().CapacityFor(new Hideout { StorageLevel = 3 });

    foreach (var key in TradeGoods.Keys)
    {
        AssertTrue(TradeGoods.IsTradeable(key), $"{key} is listed as tradeable");
        AssertTrue(TradeGoods.Capacity(capacity, key) > 0, $"{key} has a storage cap");
        var before = TradeGoods.Held(player, key);
        TradeGoods.Add(player, key, 7);
        AssertEqual(before + 7, TradeGoods.Held(player, key));
        TradeGoods.Add(player, key, -7);
        AssertEqual(before, TradeGoods.Held(player, key));
    }

    // Crew is not goods. Listing a pimp would be selling a person out of the roster the game tracks by
    // name, and nothing here knows how to move one.
    AssertTrue(!TradeGoods.IsTradeable("pimps"), "crew is not tradeable");
    AssertTrue(!TradeGoods.IsTradeable("turns"), "turns are not tradeable");
    AssertTrue(!TradeGoods.IsTradeable(null), "nothing is not tradeable");
}

/// <summary>
/// The workshop exists to give the board something worth trading, which only works if a maker can
/// undercut the shop. If materials ever cost more than the store price there is nothing to sell.
/// </summary>
static void WorkshopMakesWeaponsUnderStorePrice()
{
    var options = Resolve(null);
    // A maker who cannot undercut the shop has nothing to sell, so every gun it can forge has to
    // cost less to make than to buy. The cost belongs to the gun now rather than to the room.
    foreach (var tier in options.Weapons.Where(x => x.CanForge))
        AssertTrue(tier.ForgeCost < tier.Price,
            $"{tier.Key} cost {tier.ForgeCost} to make against a store price of {tier.Price}");

    // And the rifle is the one gun nobody makes in a back room, which is what stops the workshop
    // from eventually replacing the shop.
    AssertTrue(!options.WeaponTier(WeaponTiers.Rifle)!.CanForge, "rifles are bought, never made");

    var service = CreateEconomy(options);
    AssertRuleError(() => service.Forge(new Player { Turns = 20, Cash = 100_000, Hideout = new Hideout { StorageLevel = 2, WorkshopLevel = 1 } }, 5),
        "forging before level two");

    var maker = new Player { Turns = 20, Cash = 100_000, Hideout = new Hideout { StorageLevel = 2, WorkshopLevel = 2 } };
    var made = service.Forge(maker, 5);
    AssertEqual(10, Value<int>(RequiredBreakdown(made), "weaponsMade"));
    AssertEqual(10, maker.Weapons);
    AssertEqual(15, maker.Turns);

    // Left to itself a shop makes the best gun it has unlocked, and a level 2 shop tops out at
    // shotguns. Asking for what it cannot reach is refused by name rather than quietly downgraded.
    AssertEqual(WeaponTiers.Shotgun, Value<string>(RequiredBreakdown(made), "good"));
    AssertEqual(10, maker.Shotguns);
    AssertRuleError(() => service.Forge(maker, 1, WeaponTiers.Smg), "forging above the workshop level");
    AssertRuleError(() => service.Forge(maker, 1, WeaponTiers.Rifle), "forging a gun nobody makes");

    // A shop can always make anything below what it has unlocked, so an upgrade only ever adds.
    var pistols = service.Forge(maker, 2, WeaponTiers.Pistol);
    AssertEqual(WeaponTiers.Pistol, Value<string>(RequiredBreakdown(pistols), "good"));
    AssertTrue(maker.Pistols > 0, "a shop that has moved on can still make the cheap ones");

    // The queue version pays turns and materials up front, then delivers only when the clock clears it.
    var queued = new Player { Turns = 20, Cash = 100_000, Hideout = new Hideout { StorageLevel = 2, WorkshopLevel = 2 } };
    var queuedAt = new DateTime(2026, 8, 24, 1, 0, 0, DateTimeKind.Utc);
    var craft = service.StartCraft(queued, 5, WeaponTiers.Pistol, queuedAt);
    AssertEqual(0, queued.Weapons);
    AssertEqual(15, queued.Turns);
    AssertEqual(100_000L - craft.TotalCost, queued.Cash);
    AssertEqual(queuedAt.AddMinutes(options.WorkshopCraftMinutesPerTurn * craft.WorkUnits), craft.CompletesAtUtc);
    service.CompleteCraft(queued, craft, craft.CompletesAtUtc);
    AssertEqual(craft.Quantity, queued.Pistols);
    AssertEqual(craft.CompletesAtUtc, craft.CompletedAtUtc!.Value);
    AssertRuleError(() => service.StartCraft(new Player { Turns = 2, Cash = 100_000, Hideout = new Hideout { StorageLevel = 2, WorkshopLevel = 2 } }, 5, WeaponTiers.Pistol, queuedAt),
        "queued workshop craft without enough turns");

    // Bounded by the room up front rather than made and spilled, so nobody pays for nothing.
    // Twenty-four guns already on a shelf that holds twenty-five, whatever kind they are.
    var cramped = new Player { Turns = 20, Cash = 100_000, Pistols = 24, Hideout = new Hideout { StorageLevel = 2, WorkshopLevel = 2 } };
    var partial = service.Forge(cramped, 10);
    AssertEqual(1, Value<int>(RequiredBreakdown(partial), "weaponsMade"));
    AssertEqual(25, cramped.Weapons);
    AssertTrue(partial.Summary.Contains("Storage filled up"), "a short run says why");

    // No workshop, no weapons.
    AssertRuleError(() => service.Forge(new Player { Turns = 20, Cash = 100_000, Hideout = new Hideout() }, 5),
        "forging without a workshop");

    // One bench now. What used to be a still and a mix house are recipes on it, and how far up the
    // list a shop reaches is the level rather than a separate room somebody had to remember to build.
    var hideouts = CreateHideouts(options);
    AssertTrue(hideouts.WorkshopRequiredTier() is null, "the workshop is open from the start");

    var moonshine = options.Makeables.Single(x => x.Key == "moonshine");
    var cut = options.Makeables.Single(x => x.Key == "cut");
    var medicine = options.Makeables.Single(x => x.Key == "medicine");
    var poison = options.Makeables.Single(x => x.Key == "poison");
    AssertTrue(moonshine.MinWorkshopLevel < cut.MinWorkshopLevel, "moonshine comes before cut");
    AssertEqual(3, cut.MinWorkshopLevel);
    AssertEqual(3, medicine.MinWorkshopLevel);
    AssertTrue(cut.MinWorkshopLevel < poison.MinWorkshopLevel, "poison stays after the utility tier");

    // A shop that cannot reach a recipe says so by name rather than making the wrong thing.
    var firstBench = new Player { Turns = 20, Cash = 100_000, Hideout = new Hideout { Tier = 2, StorageLevel = 4, WorkshopLevel = 1 } };
    AssertRuleError(() => service.Make(firstBench, 5, "moonshine"), "workshop makes moonshine");
    AssertRuleError(() => service.Make(firstBench, 5, "cut"), "workshop makes cut");
    AssertRuleError(() => service.Make(firstBench, 5, "poison"), "workshop makes poison");

    var secondBench = new Player { Turns = 20, Cash = 100_000, Hideout = new Hideout { Tier = 2, StorageLevel = 4, WorkshopLevel = 2 } };
    AssertTrue(Value<int>(RequiredBreakdown(service.Make(secondBench, 5, "moonshine")), "unitsMade") > 0, "level two reaches moonshine");
    AssertRuleError(() => service.Make(secondBench, 5, "cut"), "level two workshop makes cut");

    var thirdBench = new Player { Turns = 20, Cash = 500_000, Hideout = new Hideout { Tier = 3, StorageLevel = 5, WorkshopLevel = 3 } };
    AssertTrue(Value<int>(RequiredBreakdown(service.Make(thirdBench, 5, "cut")), "unitsMade") > 0, "level three reaches cut");
    AssertTrue(Value<int>(RequiredBreakdown(service.Make(thirdBench, 5, "medicine")), "unitsMade") > 0, "level three reaches medicine");
    AssertRuleError(() => service.Make(thirdBench, 5, "poison"), "level three workshop makes poison");

    // And the deepest one reaches the dangerous craft.
    var deeper = new Player { Turns = 20, Cash = 500_000, Hideout = new Hideout { Tier = 3, StorageLevel = 5, WorkshopLevel = 4 } };
    AssertTrue(Value<int>(RequiredBreakdown(service.Make(deeper, 5, "poison")), "unitsMade") > 0, "level four reaches poison");
}

/// <summary>
/// Each new good earns its place by doing something, not by having a price. Moonshine substitutes for
/// the beer thugs drink; cut stretches coke and is worth nothing otherwise.
/// </summary>
static void ContrabandGoodsDoTheirJob()
{
    var options = new GameOptions
    {
        MaxActionTurns = 20,
        StreetAction = new StreetActionOptions
        {
            BaseGrossPerTurn = 0,
            HoeGrossPerTurn = new RangeOptions(0, 0),
            PimpGrossPerTurn = new RangeOptions(0, 0),
            PimpRecruitChance = 0, HoeRecruitChance = 0, ThugRecruitChance = 0,
            Finds = NoFinds()
        },
        Production = new ProductionOptions { Coke = new ProductProductionOptions(0, 4, 4) },
        Morale = new MoraleOptions { TurnsPerBeer = 1, TurnsPerCondom = 1000, DesertionThreshold = 0, MaxDesertionChance = 0 }
    };

    // Ten thugs over ten turns need ten beer. Six bought, four moonshine, so nothing runs short.
    var stocked = new Player { Turns = 20, Thugs = 10, Beer = 6, Moonshine = 4, ThugHappiness = 80, Hideout = new Hideout { StorageLevel = 3 } };
    CreateEconomy(options).Scout(stocked, 10);
    AssertEqual(0, stocked.Beer);
    AssertEqual(0, stocked.Moonshine);

    // The bought beer goes first, so a player is never quietly spending contraband while legal stock
    // sits next to it.
    var mixed = new Player { Turns = 20, Thugs = 5, Beer = 20, Moonshine = 10, ThugHappiness = 80, Hideout = new Hideout { StorageLevel = 3 } };
    CreateEconomy(options).Scout(mixed, 4);
    AssertEqual(10, mixed.Moonshine);

    // Production no longer touches cut. It used to be spent silently by any coke run, which meant a
    // player saving it for a batch watched it vanish into something they had not connected it to.
    var mixer = new Player { Turns = 20, Cash = 100_000, Cut = 3, Hideout = new Hideout { StorageLevel = 3 } };
    var run = CreateEconomy(options).Produce(mixer, "coke", 5);
    AssertEqual(3, mixer.Cut);
    AssertEqual(20, Value<int>(RequiredBreakdown(run), "unitsProduced"));

    var weeder = new Player { Turns = 20, Cash = 100_000, Cut = 3, Hideout = new Hideout { StorageLevel = 3 } };
    CreateEconomy(options).Produce(weeder, "weed", 5);
    AssertEqual(3, weeder.Cut);
}

/// <summary>
/// Cut used to be a free doubling: a unit of filler became a unit of product at full price, so the
/// mix house was a cheaper and faster source of coke than producing coke was, without limit. Purity
/// is what makes it a trade - more units, each worth less - and what makes the printer stop.
/// </summary>
/// <summary>
/// A new player used to finish their whole first session having clicked one button five times, with
/// the best purchase available to them unmentioned. Guidance ranks what is actually worth doing, and
/// the opening ladder is read from the world rather than stored, so it cannot drift out of step.
/// </summary>
/// <summary>
/// A flat turn rate is a wall that falls hardest on the people least able to take it: twelve an hour
/// meant a new player who spent their bank waited most of a day to play again, at exactly the point
/// they had the least reason to come back. The help tapers with net worth and ends entirely.
/// </summary>
/// <summary>
/// A tier with a hole in its ladder is a tier where earning stops meaning anything. Everything a Trap
/// House could buy landed between ten and seventy-five thousand, and then nothing until a hundred and
/// fifty: a session and a half with nothing to want. The lookout fills it, and this keeps it filled.
/// </summary>
/// <summary>
/// Outgrowing a storage room has two answers, and the warning only ever gave one. Buying a bigger room
/// costs money the player may not have; working a shorter shift costs nothing and is available now.
/// </summary>
static void ShortShiftsAreASupplyAnswer()
{
    var options = Resolve(new GameOptions());
    var economy = CreateEconomy(options);

    // Twenty-six hoes need forty-four condoms for a full shift, and a level one room holds forty-two.
    // There is nothing to buy: the room is the limit. But it does cover a shorter shift.
    //
    // Hiring can no longer put a player here, because the caps see to that. A crew that predates the
    // caps still can be, and the report has to answer them honestly.
    var stretched = new Player { Hoes = 26, Thugs = 1, Hideout = new Hideout { Tier = 1, StorageLevel = 1 } };
    var report = economy.GetCrewReport(stretched);
    AssertEqual(25, report.HoesStorageCanSupply);
    AssertEqual(2, report.StorageLevelToSupplyCrew);
    AssertTrue(report.SuppliedStreetActionTurns > 0 && report.SuppliedStreetActionTurns < options.MaxActionTurns,
        $"a shorter shift is supplied ({report.SuppliedStreetActionTurns} turns)");

    // And the number is true: a shift that length needs no more than the room holds.
    var capacity = CreateHideouts(options).CapacityFor(stretched.Hideout);
    var needed = (int)Math.Ceiling(stretched.Hoes * report.SuppliedStreetActionTurns / options.Morale.TurnsPerCondom);
    AssertTrue(needed <= capacity.MaxCondoms,
        $"{report.SuppliedStreetActionTurns} turns needs {needed} condoms and the room holds {capacity.MaxCondoms}");
    // One turn longer would not be, or the answer is needlessly short.
    var oneMore = (int)Math.Ceiling(stretched.Hoes * (report.SuppliedStreetActionTurns + 1) / options.Morale.TurnsPerCondom);
    AssertTrue(oneMore > capacity.MaxCondoms, "and it is the longest shift that fits, not merely a safe one");

    // A crew the room comfortably covers is not limited at all.
    var comfortable = new Player { Hoes = 3, Thugs = 1, Hideout = new Hideout { Tier = 1, StorageLevel = 1 } };
    AssertEqual(options.MaxActionTurns, economy.GetCrewReport(comfortable).SuppliedStreetActionTurns);

    // Neither is an empire with nobody in it, which would otherwise divide by a crew of zero.
    var empty = new Player { Hoes = 0, Thugs = 0, Hideout = new Hideout { Tier = 1, StorageLevel = 1 } };
    AssertEqual(options.MaxActionTurns, economy.GetCrewReport(empty).SuppliedStreetActionTurns);
}

static void TheFirstTierHasNoDeadZone()
{
    var options = Resolve(new GameOptions());
    var config = options.Hideout;

    var ladder = new List<long>();
    foreach (var levels in new IEnumerable<(int Level, int MinTier, long Cost)>[]
             {
                 config.Storage.Select(x => (x.Level, x.MinTier, x.UpgradeCost)),
                 config.Safe.Select(x => (x.Level, x.MinTier, x.UpgradeCost)),
                 config.WeedLab.Select(x => (x.Level, x.MinTier, x.UpgradeCost)),
                 config.CokeLab.Select(x => (x.Level, x.MinTier, x.UpgradeCost)),
                 config.Workshop.Select(x => (x.Level, x.MinTier, x.UpgradeCost)),
                 config.Lookout.Select(x => (x.Level, x.MinTier, x.UpgradeCost))
             })
        ladder.AddRange(levels.Where(x => x.MinTier <= 1 && x.Cost > 0).Select(x => x.Cost));

    var warehouse = config.Tiers.Single(x => x.Level == 2).UpgradeCost;
    ladder.Add(warehouse);
    ladder.Sort();

    // Roughly what a full bank of turns earns, so a gap wider than this is a session with nothing
    // to aim at. Two of them is the hole this exists to stop coming back.
    const long sessionEarnings = 50_000;
    for (var i = 1; i < ladder.Count; i++)
        AssertTrue(ladder[i] - ladder[i - 1] <= sessionEarnings * 2,
            $"nothing to save for between {ladder[i - 1]:C0} and {ladder[i]:C0}");

    AssertTrue(ladder[0] <= 15_000, "and something is reachable in the first session");

    // The lookout is a real answer to heat rather than a bigger number, and it is reachable at the
    // first tier: it is the only thing in the tier that is not more of something already owned.
    var hideouts = CreateHideouts(options);
    AssertEqual(0.0, hideouts.BustRiskReduction(null));
    var watched = new Hideout { Tier = 1, LookoutLevel = 1 };
    AssertTrue(hideouts.BustRiskReduction(watched) > 0, "a lookout lowers the odds");
    AssertTrue(hideouts.BustRiskReduction(watched) < 1, "but never to nothing, or holding would be free");

    // And it actually reaches the roll rather than only reading well in the options.
    var exposed = new Player { City = "Atlanta", Coke = 400, Cash = 50_000, Hideout = new Hideout { Tier = 1, StorageLevel = 6 } };
    var guarded = new Player { City = "Atlanta", Coke = 400, Cash = 50_000, Hideout = watched };
    var risky = Resolve(new GameOptions());
    risky.Hideout.HeatDecayPerHour = 0;
    var service = CreateHideouts(risky);
    // A roll that lands just under the unguarded chance but over the guarded one: the same night
    // takes the exposed player and misses the one with somebody on the corner.
    var unguardedChance = (400 * risky.Hideout.CokeHeatPerUnit - risky.Hideout.HeatBustFloor) * risky.Hideout.BustChancePerHeat;
    var roll = unguardedChance * 0.9;
    AssertTrue(service.RollBust(exposed, 1, new FixedRandom(roll)).Happened, "an unwatched stash is taken");
    AssertTrue(!service.RollBust(guarded, 1, new FixedRandom(roll)).Happened, "a watched one is not");
}

static void EarlyGameTurnsTaper()
{
    var options = Resolve(new GameOptions());

    // A new player earns several times the base rate.
    var rookie = Rookie(options);
    var opening = options.TurnsPerTickFor(rookie);
    AssertTrue(opening > options.TurnsPerTick, $"a new player is helped along ({opening} against {options.TurnsPerTick})");

    // It fades as they grow rather than switching off at a line.
    var halfway = Rookie(options);
    halfway.BankCash = options.EarlyGameNetWorthCeiling / 2;
    var middling = options.TurnsPerTickFor(halfway);
    AssertTrue(middling < opening, "the help shrinks as the empire grows");
    AssertTrue(middling >= options.TurnsPerTick, "but never drops below the normal rate");

    // And an established empire is left exactly as it was.
    var veteran = Rookie(options);
    veteran.BankCash = options.EarlyGameNetWorthCeiling * 4;
    AssertEqual(options.TurnsPerTick, options.TurnsPerTickFor(veteran));

    // The opening bank is the whole cap, so the first sitting is not five clicks.
    AssertEqual(options.MaxTurns, options.StartingTurns);
    AssertTrue(options.StartingTurns / options.MaxActionTurns >= 10, "a first session is ten shifts, not five");

    // The boost is what a turn refresh actually pays out, not just what the option reports.
    var clock = new TurnService(Snapshot(options), new PimpRoster(Snapshot(options), new MinimumRandom()));
    var earning = Rookie(options);
    earning.Turns = 0;
    earning.LastTurnUpdateUtc = DateTime.UtcNow.AddMinutes(-options.TurnTickMinutes);
    clock.Refresh(earning, DateTime.UtcNow);
    AssertEqual(opening, earning.Turns);
}

static void GuidancePointsAtTheGame()
{
    var options = Resolve(new GameOptions());
    var guidance = CreateGuidance(options);

    // Day one: turns and nothing wrong, so the advice is simply to go and earn.
    AssertTrue(guidance.NextMoves(Rookie(options), 0).Any(x => x.Label.Contains("Work the streets")),
        "a fresh player is told where the money is");

    // Things actively costing money outrank things that merely would pay. An unarmed crew bleeds
    // morale every shift; a lab is only an opportunity.
    var bleeding = Rookie(options);
    bleeding.Thugs = 6;
    bleeding.Pistols = 0;
    bleeding.Cash = 50_000;
    var advice = guidance.NextMoves(bleeding, 0).ToList();
    var arm = advice.FindIndex(x => x.Label.StartsWith("Arm "));
    var lab = advice.FindIndex(x => x.Label.Contains("weed lab"));
    AssertTrue(arm >= 0, "an unarmed crew is called out");
    AssertTrue(lab >= 0, "and the lab is still offered");
    AssertTrue(arm < lab, "but the bleeding comes first");
    AssertTrue(advice[arm].Urgent, "and it is marked urgent");
    AssertEqual(6 * (long)options.WeaponPrice, advice[arm].Cost);

    // The single best early purchase, which the old panel never once mentioned.
    var flush = Rookie(options);
    flush.Cash = 50_000;
    AssertTrue(guidance.NextMoves(flush, 0).Any(x => x.Label.Contains("weed lab")),
        "a player who can afford the lab is told about the lab");
    AssertTrue(!guidance.NextMoves(Rookie(options), 0).Any(x => x.Label.Contains("weed lab")),
        "and a broke one is not sold what they cannot buy");

    // Never a wall of text. Four at most, or the ranking was pointless.
    var swamped = Rookie(options);
    swamped.Thugs = 9;
    swamped.Pistols = 0;
    swamped.Hoes = 90;
    swamped.Condoms = 0;
    swamped.Beer = 0;
    swamped.HoeHappiness = 10;
    swamped.Cash = 900_000;
    AssertTrue(guidance.NextMoves(swamped, 500).Count <= 4, "advice is ranked, not dumped");

    // The ladder is read from the world: a lab built is a rung ticked, with nothing stored anywhere.
    var climber = Rookie(options);
    AssertTrue(!guidance.Objectives(climber, []).Single(x => x.Label.Contains("weed lab")).Done, "no lab, no tick");
    climber.Hideout!.WeedLabLevel = 1;
    AssertTrue(guidance.Objectives(climber, []).Single(x => x.Label.Contains("weed lab")).Done, "a lab in the world is a lab on the ladder");

    // And from history, for the verbs that leave no mark on the player themselves.
    AssertTrue(!guidance.Objectives(climber, []).Single(x => x.Label.Contains("Sell")).Done, "nothing sold yet");
    AssertTrue(guidance.Objectives(climber, ["SALE"]).Single(x => x.Label.Contains("Sell")).Done, "a sale in the log counts");

    // A grown empire has a finished ladder, which is how the panel knows to get out of the way.
    var veteran = Rookie(options);
    veteran.Pimps = 4;
    veteran.Thugs = 2;
    veteran.Pistols = 8;
    veteran.Hideout = new Hideout { Tier = 2, StorageLevel = 3, WeedLabLevel = 2 };
    var finished = guidance.Objectives(veteran, ["STREET", "BANK", "PRODUCTION", "SALE"]);
    AssertTrue(finished.All(x => x.Done),
        $"nothing left to tell a grown empire: {string.Join(", ", finished.Where(x => !x.Done).Select(x => x.Label))}");

    // Every rung sends the player to a tab, and nothing checked that the tab it names is one the client
    // has, let alone that the thing it is asking for lives there. "Run a production shift" pointed at
    // the street for as long as the ladder had existed, and there is no production on the street at
    // all - so a new player following the game's own instructions arrived somewhere with nothing to do.
    //
    // The page names have to match the client's own keys, which are duplicated here on purpose: they
    // cross a wire, and the only way a rename gets caught is if both ends are written down.
    var pages = new[] { "overview", "street", "crew", "hideout", "territory", "market", "mules", "recon", "alliance" };
    var ladder = guidance.Objectives(Rookie(options), []);
    AssertTrue(ladder.Count > 0, "the ladder should have rungs");

    foreach (var rung in ladder)
    {
        AssertTrue(pages.Contains(rung.Page), $"\"{rung.Label}\" points at \"{rung.Page}\", which is not a page");
        AssertTrue(!string.IsNullOrWhiteSpace(rung.Why), $"\"{rung.Label}\" should say why it is worth doing");
    }

    // The lab and the shift that uses it belong on one page: the step before this one builds the lab in
    // the hideout, and sending the player elsewhere to use it is a tab change bought for nothing.
    var production = ladder.FirstOrDefault(x => x.Label.Contains("production"));
    AssertTrue(production is not null, "the ladder should still teach production");
    AssertEqual("hideout", production!.Page);

}

static Player Rookie(GameOptions options) => new()
{
    City = "Detroit",
    Cash = options.StartingCash,
    Turns = options.StartingTurns,
    Pimps = options.StartingPimps,
    Hoes = options.StartingHoes,
    Thugs = options.StartingThugs,
    Condoms = options.StartingCondoms,
    Beer = options.StartingBeer,
    Pistols = options.StartingWeapons,
    HoeHappiness = 100,
    ThugHappiness = 100,
    Hideout = new Hideout { Tier = 1, StorageLevel = 1, SafeLevel = 1 }
};

static GuidanceService CreateGuidance(GameOptions options)
    => new(Snapshot(options), CreateHideouts(options), CreateEconomy(options));

static void PurityStopsTheCokePrinter()
{
    var options = Resolve(new GameOptions());
    var economy = CreateEconomy(options);

    // Blending is a weighted average, so filler drags the whole pile down with it.
    var pile = new Player { Coke = 100, CokePurity = 1 };
    pile.AddCoke(100, 0);
    AssertEqual(200, pile.Coke);
    AssertEqual(0.5, pile.CokePurity);
    pile.AddCoke(200, 1);
    AssertEqual(0.75, pile.CokePurity);

    // Taking coke away leaves the mixture as it was: a share of a blend is the same blend.
    pile.Coke -= 100;
    AssertEqual(0.75, pile.CokePurity);

    // The price falls slower than proportionally, or nobody would ever stretch anything.
    AssertEqual(1.0, options.PurityMultiplier(1));
    AssertTrue(options.PurityMultiplier(0.5) > 0.5, "halving purity costs less than half the price");
    AssertTrue(options.PurityMultiplier(0.5) < 1, "but it does cost something");
    AssertTrue(options.PurityMultiplier(0.25) < options.PurityMultiplier(0.5), "and it keeps falling");

    // No floor, which is the point. A floor would make total value climb with unit count forever,
    // which is the printer wearing a different hat.
    AssertTrue(options.PurityMultiplier(0.01) < 0.2, "very cut product is very nearly worthless");

    // The printer is dead: stretching a pile raises what it is worth by less than doubling it.
    var pureValue = 200 * options.PurityMultiplier(1);
    var stretchedValue = 400 * options.PurityMultiplier(0.5);
    AssertTrue(stretchedValue > pureValue, "stretching still pays, or the mix house is pointless");
    AssertTrue(stretchedValue < pureValue * 2, "but it never pays like free coke did");

    // Selling reads the pile's own strength rather than the list price.
    var seller = new Player { Turns = 50, Coke = 100, CokePurity = 0.25, City = "New York", Hideout = new Hideout { StorageLevel = 6, SafeLevel = 6 } };
    var list = economy.ProductSellPrice(seller.City, "coke");
    var sale = economy.SellProduct(seller, "coke", 100);
    var paid = Value<long>(RequiredBreakdown(sale), "unitPrice");
    AssertTrue(paid < list, $"cut coke fetches less than clean ({paid} against {list})");
    AssertTrue(sale.Summary.Contains("25% pure"), $"and the notice says why: {sale.Summary}");

    // Net worth values it the same way, in memory and in the database expression alike.
    var cut = new Player { Coke = 100, CokePurity = 0.25 };
    var clean = new Player { Coke = 100, CokePurity = 1 };
    AssertTrue(economy.CalculateNetWorth(cut) < economy.CalculateNetWorth(clean),
        "a cut pile is worth less on the ladder too");
    AssertEqual(economy.CalculateNetWorth(cut), economy.NetWorthExpression.Compile()(cut));
}

/// <summary>
/// Cut is worth nothing on its own; it is worth whatever the coke it becomes is worth. This is the
/// step that turns one into the other, and the coke it works on is coke you already hold, however it
/// got there - which is the whole reason it is an action rather than a bonus on production.
/// </summary>
static void CuttingStretchesWhatYouAlreadyHold()
{
    var options = Resolve(new GameOptions());
    var economy = CreateEconomy(options);
    var perTurn = options.Hideout.CutPerTurnPerMixLevel;

    // A bench deep enough to make cut is required - the same level the recipe itself asks for - and so
    // is something at both ends of the mix.
    var roomless = Stocked(workshop: 0, coke: 50, cut: 50);
    AssertRuleError(() => economy.CutCoke(roomless, 5), "workshop");
    AssertRuleError(() => economy.CutCoke(Stocked(workshop: 3, coke: 50, cut: 0), 5), "no cut to work with");
    AssertRuleError(() => economy.CutCoke(Stocked(workshop: 3, coke: 0, cut: 50), 5), "no coke to step on");

    // One cut makes one coke. The cut is spent, the pile grows by the same.
    var player = Stocked(workshop: 3, coke: 60, cut: 40);
    var result = economy.CutCoke(player, 4);
    AssertEqual(100, player.Coke);
    AssertEqual(0, player.Cut);
    // Sixty clean plus forty of filler is sixty percent product, and that is what it now sells as.
    AssertEqual(0.6, Math.Round(player.CokePurity, 4));
    AssertTrue(result.Summary.Contains("Stepped on 40 coke"), $"the notice says what happened: {result.Summary}");

    // Only the turns the batch actually needed. Asking for ten on a two-turn batch should not cost
    // eight turns of standing about.
    var quick = Stocked(workshop: 3, coke: 100, cut: perTurn);
    var turnsBefore = quick.Turns;
    economy.CutCoke(quick, 10);
    AssertEqual(turnsBefore - 1, quick.Turns);

    // The top bench still works faster, which gives level four value even after cut opens at three.
    var mid = Stocked(workshop: 3, coke: 100, cut: 500);
    var deep = Stocked(workshop: 4, coke: 100, cut: 500);
    economy.CutCoke(mid, 1);
    economy.CutCoke(deep, 1);
    AssertEqual(perTurn * 3, 500 - mid.Cut);
    AssertEqual(perTurn * 4, 500 - deep.Cut);

    // Never past the walls. Cutting into a full store would destroy cut already paid for, so the
    // batch stops at the room instead of spilling.
    var cramped = Stocked(workshop: 3, coke: 60, cut: 200, storage: 1);
    var capacity = CreateHideouts(options).CapacityFor(cramped.Hideout).MaxCoke;
    cramped.Coke = capacity - 3;
    economy.CutCoke(cramped, 20);
    AssertEqual(capacity, cramped.Coke);
    AssertEqual(197, cramped.Cut);

    AssertRuleError(() => economy.CutCoke(Full(options), 5), "no space for more coke");

    static Player Stocked(int workshop, int coke, int cut, int storage = 6) => new()
    {
        Turns = 100,
        Coke = coke,
        Cut = cut,
        Hideout = new Hideout { Tier = 3, StorageLevel = storage, WorkshopLevel = workshop }
    };

    Player Full(GameOptions opts)
    {
        var player = Stocked(workshop: 3, coke: 0, cut: 50, storage: 1);
        player.Coke = CreateHideouts(opts).CapacityFor(player.Hideout).MaxCoke;
        return player;
    }
}

/// <summary>
/// Everything in this game is illegal, so being illegal distinguishes nothing. Heat is what differs:
/// weighted by how much notice a good draws, earned by working, and cooling on its own.
/// </summary>
static void HeatDrivesTheBust()
{
    var options = Resolve(new GameOptions());
    var config = options.Hideout;
    config.HeatDecayPerHour = 0;
    config.HeatBustFloor = 20;
    config.BustChancePerHeat = 1;
    config.SeizedPercent = 0.5;
    config.FinePerSeizedUnit = 40;
    var hideouts = CreateHideouts(options);

    // Coke draws the most notice per unit and cut the least, despite where cut is made. Measured in
    // an ordinary town: how hard a town looks is its own thing, and is tested on its own.
    const string ordinary = "Atlanta";
    AssertEqual(1.0, options.CityMarkets.HeatMultiplier(ordinary));
    AssertEqual(35.0, hideouts.HeatFor(new Player { City = ordinary, Coke = 100 }));
    AssertEqual(25.0, hideouts.HeatFor(new Player { City = ordinary, Moonshine = 100 }));
    AssertEqual(10.0, hideouts.HeatFor(new Player { City = ordinary, Weed = 100 }));
    AssertEqual(3.0, hideouts.HeatFor(new Player { City = ordinary, Cut = 100 }));

    // Sized against the rooms the game ships. A full Warehouse store of coke is worth watching; a
    // whole evening's work by someone holding nothing is not, and it fades before the next one.
    var warehouseStore = hideouts.HeatFor(new Player { City = ordinary, Coke = 85 });
    AssertTrue(warehouseStore > config.HeatBustFloor && warehouseStore < config.HeatBustFloor * 2,
        $"a full Warehouse store of coke is Noticed, not Hunted ({warehouseStore})");
    // Read from a untouched copy: this test bends decay and the floor for the roll assertions below.
    var shipped = new GameOptions().Hideout;
    var fullBank = new GameOptions().MaxTurns * shipped.HeatPerStreetTurn;
    AssertTrue(fullBank < shipped.HeatBustFloor * 2,
        $"working the whole turn bank does not on its own pass Noticed ({fullBank})");
    AssertTrue(fullBank / shipped.HeatDecayPerHour < 12,
        "and a night of laying low clears what a day of work earned");

    // Working the streets counts even with nothing held, which is the point: the core loop is illegal.
    AssertEqual(25.0, hideouts.HeatFor(new Player { Heat = 25 }));

    // Under the floor nobody is looking, however long they sit there.
    var quiet = new Player { City = ordinary, Weed = 20, Cash = 10_000 };
    AssertTrue(!hideouts.RollBust(quiet, 24, new AlwaysRandom()).Happened, "a small stash draws nobody");
    AssertEqual(20, quiet.Weed);

    // Over it, a raid takes a share of every pile and fines them for the lot. This stash alone sits
    // just under the floor now, so it takes a day's work on top to draw anyone: which is the point.
    var loaded = new Player { City = ordinary, Coke = 40, Weed = 20, Moonshine = 10, Cut = 8, Heat = 20, Cash = 10_000 };
    AssertTrue(hideouts.HeatFor(new Player { City = ordinary, Coke = 40, Weed = 20, Moonshine = 10, Cut = 8 }) < config.HeatBustFloor,
        "a working stash on its own stays under the floor");
    var bust = hideouts.RollBust(loaded, 1, new AlwaysRandom());
    AssertEqual(20, bust.Coke);
    AssertEqual(10, bust.Weed);
    AssertEqual(5, bust.Moonshine);
    AssertEqual(4, bust.Cut);
    AssertEqual(39 * 40L, bust.Fine);
    AssertEqual(20, loaded.Coke);
    AssertTrue(bust.Describe().Contains("20 coke"), $"the notice names what went: {bust.Describe()}");

    // The fine never reaches past cash on hand.
    var broke = new Player { City = ordinary, Coke = 100, Cash = 500 };
    AssertEqual(500L, hideouts.RollBust(broke, 1, new AlwaysRandom()).Fine);
    AssertEqual(0L, broke.Cash);

    // A raid clears the attention it was drawn by, or one bust guarantees the next.
    var raided = new Player { City = ordinary, Coke = 100, Heat = 90, Cash = 10_000 };
    hideouts.RollBust(raided, 1, new AlwaysRandom());
    AssertEqual(0.0, raided.Heat);

    // Earned heat cools on its own, which is what makes laying low work.
    var cooling = new Player { Heat = 30 };
    var decaying = Resolve(new GameOptions());
    decaying.Hideout.HeatDecayPerHour = 3;
    decaying.Hideout.BustChancePerHeat = 0;
    CreateHideouts(decaying).RollBust(cooling, 5, new AlwaysRandom());
    AssertEqual(15.0, cooling.Heat);
}

static void TerritoryEffectsAddUp()
{
    var options = new GameOptions();
    options.Territory.ApplyDefaultsWhereEmpty();
    var service = CreateTerritories(options);

    Territory Ground(string type) => new() { Type = type };

    AssertEqual(0, service.EffectsFor([]).StreetIncomePercent);
    AssertTrue(!service.EffectsFor([]).Any, "holding nothing amplifies nothing");

    AssertEqual(15, service.EffectsFor([Ground("corner")]).StreetIncomePercent);
    AssertEqual(30, service.EffectsFor([Ground("corner"), Ground("corner")]).StreetIncomePercent);
    AssertEqual(20, service.EffectsFor([Ground("dock")]).ProductionYieldPercent);
    AssertEqual(50, service.EffectsFor([Ground("club")]).MoraleRecoveryPercent);
    AssertEqual(20, service.EffectsFor([Ground("stash")]).LootPercent);

    // A mixed hand contributes to each line separately rather than to one pooled number.
    var mixed = service.EffectsFor([Ground("corner"), Ground("dock"), Ground("stash")]);
    AssertEqual(15, mixed.StreetIncomePercent);
    AssertEqual(20, mixed.ProductionYieldPercent);
    AssertEqual(20, mixed.LootPercent);
    AssertEqual(0, mixed.MoraleRecoveryPercent);

    // Ground of a type nobody configured is worth nothing rather than throwing.
    AssertTrue(!service.EffectsFor([Ground("racetrack")]).Any, "an unknown type is inert");

    // Every town carries all four types, so nowhere is starved of an effect, and the town list is
    // derived from the map rather than kept beside it.
    var cities = options.Territory.Cities();
    AssertTrue(cities.Count >= 5, $"every town needs a map: {cities.Count} found");
    foreach (var city in cities)
    {
        var inTown = options.Territory.Map.Where(x => string.Equals(x.City, city, StringComparison.OrdinalIgnoreCase)).ToList();
        AssertTrue(inTown.Count >= 4, $"{city} has only {inTown.Count} pieces, which is not a map worth fighting over");
        foreach (var type in new[] { "corner", "dock", "club", "stash" })
            AssertTrue(inTown.Any(x => x.Type == type), $"{city} has no {type}, so that effect is unreachable there");
    }

    // Ground is contested inside a town, and the rule lives in one place so claiming and raiding agree.
    var local = new Player { City = "Detroit" };
    AssertTrue(TerritoryService.SameCity(local, new Territory { City = "Detroit" }), "your own town is contestable");
    AssertTrue(TerritoryService.SameCity(local, new Territory { City = "detroit" }), "case does not decide who runs a town");
    AssertTrue(!TerritoryService.SameCity(local, new Territory { City = "Miami" }), "somebody else's town is not");

    // The tier ladder gains a second meaning: how much ground you may run at once.
    AssertEqual(1, service.HoldingCapFor(new Hideout { Tier = 1 }));
    AssertEqual(4, service.HoldingCapFor(new Hideout { Tier = 4 }));
}

/// <summary>
/// The bonuses have to arrive where they were promised. Each one lives at a single seam, and a seam
/// that silently stops passing them through is the failure this pins down.
/// </summary>
/// <summary>
/// A garrison is a handful of thugs, so who runs it matters more there than at home. Only an Enforcer
/// helps hold ground, which is the same division the rest of the game uses: Enforcers fight, Hustlers
/// earn.
/// </summary>
static void GarrisonPimpBonusOnlyForEnforcers()
{
    var roster = CreatePimps(new GameOptions { Pimps = new PimpOptions { MaxGarrisonBonusPercent = 30 } });

    AssertEqual(0, roster.GarrisonBonusPercent(null));
    AssertEqual(7, roster.GarrisonBonusPercent(new Pimp { Specialty = PimpSpecialties.Enforcer, BonusPercent = 7 }));
    AssertEqual(0, roster.GarrisonBonusPercent(new Pimp { Specialty = PimpSpecialties.Hustler, BonusPercent = 7 }));

    // The garrison cap is its own number, not the house one, because the same percentage is worth far
    // less over five thugs than over a full roster.
    AssertEqual(30, roster.GarrisonBonusPercent(new Pimp { Specialty = PimpSpecialties.Enforcer, BonusPercent = 99 }));

    // A pimp who is gone cannot be running anything.
    AssertEqual(0, roster.GarrisonBonusPercent(new Pimp
    {
        Specialty = PimpSpecialties.Enforcer,
        BonusPercent = 7,
        LostAtUtc = new DateTime(2026, 8, 14, 0, 0, 0, DateTimeKind.Utc)
    }));

    // Posted away, so they are not also sharpening the house or lifting street income.
    var player = new Player();
    var enforcer = new Pimp { Id = 1, Specialty = PimpSpecialties.Enforcer, BonusPercent = 6 };
    var hustler = new Pimp { Id = 2, Specialty = PimpSpecialties.Hustler, BonusPercent = 5 };
    player.Crew.Add(enforcer);
    player.Crew.Add(hustler);
    AssertEqual(6, roster.DefenceBonusPercent(player, []));
    AssertEqual(0, roster.DefenceBonusPercent(player, [1L]));
    AssertEqual(5, roster.StreetBonusPercent(player, []));
    AssertEqual(0, roster.StreetBonusPercent(player, [2L]));
}

static void TerritoryBonusesReachTheirActivities()
{
    var options = new GameOptions
    {
        MaxActionTurns = 20,
        StreetAction = new StreetActionOptions
        {
            BaseGrossPerTurn = 100,
            HoeGrossPerTurn = new RangeOptions(0, 0),
            PimpGrossPerTurn = new RangeOptions(0, 0),
            PimpRecruitChance = 0,
            HoeRecruitChance = 0,
            ThugRecruitChance = 0,
            Finds = NoFinds()
        },
        Production = new ProductionOptions { Weed = new ProductProductionOptions(0, 4, 4) },
        Morale = new MoraleOptions { PassiveRecoveryPerTick = 1, TurnsPerCondom = 1000, TurnsPerBeer = 1000 }
    };

    // A corner lifts the take. Ten turns at a flat 100 gross is 1,000 before any cut.
    var plain = new Player { Turns = 20, Hoes = 1, HoeCutPercent = 0, Hideout = new Hideout() };
    var withCorner = new Player { Turns = 20, Hoes = 1, HoeCutPercent = 0, Hideout = new Hideout() };
    AssertEqual(1000L, Value<long>(RequiredBreakdown(CreateEconomy(options).Scout(plain, 10)), "gross"));
    AssertEqual(1150L, Value<long>(RequiredBreakdown(
        CreateEconomy(options).Scout(withCorner, 10, false, new TerritoryEffects(15, 0, 0, 0))), "gross"));

    // Docks lift the yield, stacking on whatever the lab already gives.
    var producer = new Player { Turns = 20, Cash = 10_000, Hideout = new Hideout { StorageLevel = 3 } };
    AssertEqual(24, Value<int>(RequiredBreakdown(
        CreateEconomy(options).Produce(producer, "weed", 5, new TerritoryEffects(0, 20, 0, 0))), "unitsProduced"));

    // A club speeds passive recovery, which is a percentage on the existing rate rather than a
    // second trickle, so morale still recovers in exactly one place.
    var resting = new Player { HoeHappiness = 50, ThugHappiness = 50, LastTurnUpdateUtc = new DateTime(2026, 8, 14, 0, 0, 0, DateTimeKind.Utc) };
    CreateTurns(options).Refresh(resting, resting.LastTurnUpdateUtc.AddMinutes(options.TurnTickMinutes * 4), 50);
    AssertEqual(56.0, Math.Round(resting.HoeHappiness, 2));
}

// The lockout check is what a ban actually means, so it has to be exact about the boundaries.
static void AccountLockoutBlocksBannedAndSuspended()
{
    var now = new DateTime(2026, 8, 12, 12, 0, 0, DateTimeKind.Utc);

    var clean = new PlayerAccount { Username = "clean" };
    AssertTrue(!clean.IsLockedOut(now), "an untouched account is not locked out");
    AssertEqual(string.Empty, clean.LockoutMessage(now));

    var banned = new PlayerAccount { Username = "banned", IsBanned = true, EnforcementReason = "Exploiting" };
    AssertTrue(banned.IsLockedOut(now), "a banned account is locked out");
    AssertTrue(banned.LockoutMessage(now).Contains("banned"), "the message should say it is a ban");
    AssertTrue(banned.LockoutMessage(now).Contains("Exploiting"), "the reason should reach the player");

    // A suspension is a deadline, not a state: it has to expire on its own.
    var suspended = new PlayerAccount { Username = "suspended", SuspendedUntilUtc = now.AddHours(1) };
    AssertTrue(suspended.IsLockedOut(now), "a live suspension locks the account");
    AssertTrue(!suspended.IsLockedOut(now.AddHours(1)), "a suspension expires at its deadline");
    AssertTrue(!suspended.IsLockedOut(now.AddHours(2)), "a lapsed suspension stays lifted");
    AssertTrue(suspended.LockoutMessage(now).Contains("suspended"), "the message should say it is a suspension");

    // A ban with no reason recorded still has to produce a usable message.
    var quiet = new PlayerAccount { Username = "quiet", IsBanned = true };
    AssertTrue(quiet.LockoutMessage(now).Contains("No reason recorded"), "a reasonless ban still explains itself");
}

static void WealthStatsDescribeTheDistribution()
{
    AssertEqual(0L, WealthStats.Median([]));
    AssertEqual(5L, WealthStats.Median([5]));
    AssertEqual(3L, WealthStats.Median([1, 5]));
    AssertEqual(5L, WealthStats.Median([1, 5, 9]));

    // Perfect equality is 0; one player holding everything approaches 100.
    AssertEqual(0d, WealthStats.GiniPercent([100, 100, 100, 100]));
    AssertEqual(0d, WealthStats.GiniPercent([]));
    AssertEqual(0d, WealthStats.GiniPercent([0, 0]));
    AssertTrue(WealthStats.GiniPercent([0, 0, 0, 1000]) > 70, "one holder of everything is severely unequal");
    AssertTrue(
        WealthStats.GiniPercent([100, 200, 300]) < WealthStats.GiniPercent([1, 1, 1000]),
        "a spread economy must score lower than a concentrated one");

    // Bands partition without overlap: every player lands in exactly one.
    var bands = WealthStats.WealthBands([10_000, 60_000, 300_000, 5_000_000, 20_000]);
    AssertEqual(4, bands.Count);
    AssertEqual(2, bands[0].Players);
    AssertEqual(1, bands[1].Players);
    AssertEqual(1, bands[2].Players);
    AssertEqual(1, bands[3].Players);
    AssertEqual(5, bands.Sum(x => x.Players));
    AssertEqual(5_390_000L, bands.Sum(x => x.TotalNetWorth));

    // Boundaries belong to the upper band, so nobody is counted twice.
    AssertEqual(1, WealthStats.WealthBands([50_000])[1].Players);
    AssertEqual(1, WealthStats.WealthBands([1_000_000])[3].Players);
}

// This reflection is what makes runtime tuning possible, so its edges need pinning down.
static void OptionPathsDiscoverAndWriteScalars()
{
    var options = new GameOptions();
    var paths = GameOptionPaths.Describe(options);
    var byPath = paths.ToDictionary(x => x.Path, StringComparer.OrdinalIgnoreCase);

    // Nested scalars are reachable at every depth.
    AssertTrue(byPath.ContainsKey("MaxTurns"), "top-level scalars are editable");
    AssertTrue(byPath.ContainsKey("Combat.AttackTurnCost"), "one level down is editable");
    AssertTrue(byPath.ContainsKey("StreetAction.Finds.Coke.Chance"), "deeply nested scalars are editable");
    AssertTrue(byPath.ContainsKey("Morale.TurnsPerCondom"), "morale tuning is editable");

    // List-shaped config is deliberately out of scope.
    AssertTrue(!paths.Any(x => x.Path.StartsWith("Hideout.Storage", StringComparison.OrdinalIgnoreCase)),
        "table-shaped settings must not be exposed as scalars");

    // Types are reported so the UI can validate before submitting.
    AssertEqual("whole number", byPath["Combat.AttackTurnCost"].Type);
    AssertEqual("decimal", byPath["Morale.TurnsPerCondom"].Type);

    // Writing goes through parsing and lands on the real property.
    AssertTrue(GameOptionPaths.TryApply(options, "Combat.AttackTurnCost", "7", out _), "a valid int is accepted");
    AssertEqual(7, options.Combat.AttackTurnCost);
    AssertTrue(GameOptionPaths.TryApply(options, "morale.turnspercondom", "18.5", out _), "paths are case-insensitive");
    AssertEqual(18.5, options.Morale.TurnsPerCondom);

    // Failures report a reason instead of throwing, since the values come from an admin form.
    AssertTrue(!GameOptionPaths.TryApply(options, "Combat.AttackTurnCost", "abc", out var parseError), "text is rejected for a number");
    AssertTrue(parseError is not null && parseError.Contains("whole number"), "the error names the expected type");
    AssertTrue(!GameOptionPaths.TryApply(options, "Combat.AttackTurnCost", "-3", out var negativeError), "negatives are rejected");
    AssertTrue(negativeError is not null, "a negative reports why");
    AssertTrue(!GameOptionPaths.TryApply(options, "Nope.NotReal", "1", out var unknownError), "unknown paths are rejected");
    AssertTrue(unknownError is not null, "an unknown path reports why");
    AssertTrue(!GameOptionPaths.TryApply(options, "Hideout.Storage", "1", out _), "a list cannot be set as a scalar");

    // The value written is the value read back.
    AssertEqual("7", GameOptionPaths.Read(options, "Combat.AttackTurnCost"));
    AssertTrue(GameOptionPaths.Read(options, "Nope.NotReal") is null, "reading an unknown path yields null");
}

static void OptionOverridesLayerOverAppsettings()
{
    var overrides = new GameOptionOverrides();
    AssertEqual(0, overrides.Snapshot().Count);

    overrides.Replace(new Dictionary<string, string>
    {
        ["Combat.AttackTurnCost"] = "4",
        ["Morale.TurnsPerCondom"] = "25",
        // A stale or bogus entry must be skipped, not crash every request that binds options.
        ["Removed.Setting"] = "1",
        ["Combat.AttackCooldownMinutes"] = "not a number"
    });

    var options = new GameOptions();
    overrides.Apply(options);

    AssertEqual(4, options.Combat.AttackTurnCost);
    AssertEqual(25.0, options.Morale.TurnsPerCondom);
    // Untouched by the bad entry: the shipped default survives.
    AssertEqual(30, options.Combat.AttackCooldownMinutes);

    // Replacing swaps the whole map and bumps the version consumers watch.
    var versionBefore = overrides.Version;
    overrides.Replace(new Dictionary<string, string>());
    AssertTrue(overrides.Version > versionBefore, "replacing bumps the version");
    var reset = new GameOptions();
    overrides.Apply(reset);
    AssertEqual(10, reset.Combat.AttackTurnCost);
}

static void AntiFarmRefusesMismatchedFights()
{
    var options = new AntiFarmOptions { MinDefenderNetWorth = 25_000, MaxNetWorthRatio = 5 };

    // A fair fight passes. The ratio boundary is tested above the floor, since the floor is checked
    // first and would otherwise mask it.
    AssertTrue(AntiFarm.RejectReason(100_000, 50_000, options) is null, "similar sizes may fight");
    AssertTrue(AntiFarm.RejectReason(150_000, 30_000, options) is null, "exactly at the ratio is allowed");

    // Too small to be touched at all.
    var floored = AntiFarm.RejectReason(100_000, 24_999, options);
    AssertTrue(floored is not null && floored.Contains("floor"), "a target under the floor is protected");
    // The floor applies even between two small players.
    AssertTrue(AntiFarm.RejectReason(9_830, 9_830, options) is not null, "brand new players cannot be farmed");

    // Outweighing the target by more than the ratio.
    var outmatched = AntiFarm.RejectReason(150_001, 30_000, options);
    AssertTrue(outmatched is not null && outmatched.Contains("outweigh"), "a heavyweight cannot pick on the weak");

    // Punching up is always fine.
    AssertTrue(AntiFarm.RejectReason(30_000, 5_000_000, options) is null, "the weak may attack the strong");
}

static void AntiFarmDecaysRepeatLoot()
{
    var options = new AntiFarmOptions { LootDecayPerRepeat = 0.4, MinLootMultiplier = 0.1 };

    AssertEqual(1d, AntiFarm.LootMultiplier(0, options));
    AssertEqual(0.6, Math.Round(AntiFarm.LootMultiplier(1, options), 4));
    AssertEqual(0.36, Math.Round(AntiFarm.LootMultiplier(2, options), 4));

    // Never reaches zero: a repeat attack becomes pointless, not forbidden.
    AssertEqual(0.1, Math.Round(AntiFarm.LootMultiplier(20, options), 4));
    AssertTrue(AntiFarm.LootMultiplier(100, options) >= options.MinLootMultiplier, "the floor always holds");

    // Decay is monotonic, so there is never an incentive to hit again sooner.
    var previous = 1d;
    for (var wins = 1; wins <= 6; wins++)
    {
        var current = AntiFarm.LootMultiplier(wins, options);
        AssertTrue(current <= previous, "each repeat is worth no more than the last");
        previous = current;
    }
}

static void AntiFarmWidensProtection()
{
    var options = new AntiFarmOptions { ProtectionEscalationPerHit = 0.5, MaxProtectionMinutes = 360 };

    // A first hit earns the plain window.
    AssertEqual(60, AntiFarm.ProtectionMinutes(0, 60, options));
    AssertEqual(90, AntiFarm.ProtectionMinutes(1, 60, options));
    AssertEqual(120, AntiFarm.ProtectionMinutes(2, 60, options));

    // Capped, so a heavily farmed player is not made permanently untouchable.
    AssertEqual(360, AntiFarm.ProtectionMinutes(100, 60, options));

    // A base longer than the cap still wins, so lowering the cap cannot shorten the base window.
    AssertEqual(600, AntiFarm.ProtectionMinutes(0, 600, options));
    AssertEqual(600, AntiFarm.ProtectionMinutes(5, 600, options));
}

static void BotTargetingPicksRichestBeatable()
{
    var antiFarm = new AntiFarmOptions { MinDefenderNetWorth = 25_000, MaxNetWorthRatio = 5 };
    var attackerNetWorth = 200_000L;
    var attackPower = 500;

    var weakAndPoor = new BotTarget(Guid.NewGuid(), "Poor", 40_000, 100, false, 0);
    var richAndBeatable = new BotTarget(Guid.NewGuid(), "Rich", 180_000, 300, false, 0);
    var richButStrong = new BotTarget(Guid.NewGuid(), "Fortress", 400_000, 900, false, 0);
    var protectedTarget = new BotTarget(Guid.NewGuid(), "Shielded", 300_000, 100, true, 0);
    var belowFloor = new BotTarget(Guid.NewGuid(), "Newbie", 9_830, 10, false, 0);

    var chosen = BotTargeting.Choose(
        [weakAndPoor, richAndBeatable, richButStrong, protectedTarget, belowFloor],
        attackerNetWorth, attackPower, antiFarm, winMargin: 1.25);

    // Richest of the ones it can actually beat and is allowed to hit.
    AssertTrue(chosen is not null, "a beatable target should be found");
    AssertEqual("Rich", chosen!.Name);

    // Each exclusion holds on its own.
    AssertTrue(BotTargeting.Choose([protectedTarget], attackerNetWorth, attackPower, antiFarm, 1.25) is null,
        "protected targets are skipped");
    AssertTrue(BotTargeting.Choose([belowFloor], attackerNetWorth, attackPower, antiFarm, 1.25) is null,
        "targets under the anti-farm floor are skipped");
    AssertTrue(BotTargeting.Choose([richButStrong], attackerNetWorth, attackPower, antiFarm, 1.25) is null,
        "a fight it would lose is skipped");
    AssertTrue(BotTargeting.Choose([weakAndPoor], 5_000_000, attackPower, antiFarm, 1.25) is null,
        "a target it outweighs past the ratio is skipped");
    AssertTrue(BotTargeting.Choose([], attackerNetWorth, attackPower, antiFarm, 1.25) is null,
        "an empty ladder yields nothing");

    // Already being swarmed: piling on is exactly what the incoming cap prevents.
    var swarmed = new BotTarget(Guid.NewGuid(), "Swarmed", 180_000, 300, false, antiFarm.MaxIncomingAttacks);
    AssertTrue(BotTargeting.Choose([swarmed], attackerNetWorth, attackPower, antiFarm, 1.25) is null,
        "a target at the incoming cap is skipped");
    var oneIncoming = new BotTarget(Guid.NewGuid(), "Busy", 180_000, 300, false, antiFarm.MaxIncomingAttacks - 1);
    AssertTrue(BotTargeting.Choose([oneIncoming], attackerNetWorth, attackPower, antiFarm, 1.25) is not null,
        "a target below the cap is still fair game");

    // A larger win margin makes the bot pickier, never bolder.
    AssertTrue(BotTargeting.Choose([richAndBeatable], attackerNetWorth, attackPower, antiFarm, 2.0) is null,
        "a cautious bot passes on a fight a reckless one takes");
}

static void BotAttackProfilesScaleWithPersonality()
{
    var reckless = BotAttackProfile.For(BotBrainFocus.MoraleNeglecter);
    var banker = BotAttackProfile.For(BotBrainFocus.Banker);

    AssertTrue(reckless.AttackChance > banker.AttackChance, "hard chargers fight more than bankers");
    AssertTrue(reckless.WinMargin < banker.WinMargin, "hard chargers accept thinner odds");
    AssertTrue(reckless.ThugCommitShare > banker.ThugCommitShare, "hard chargers commit more crew");

    // Every personality is sane: it fights sometimes, never commits everything, and wants an edge.
    foreach (var focus in Enum.GetValues<BotBrainFocus>())
    {
        var profile = BotAttackProfile.For(focus);
        AssertTrue(profile.AttackChance is > 0 and <= 1, $"{focus} has a usable attack chance");
        AssertTrue(profile.ThugCommitShare is > 0 and < 1, $"{focus} keeps some crew at home");
        AssertTrue(profile.WinMargin >= 1, $"{focus} does not seek fights it loses");
        AssertTrue(profile.MinThugsToAttack > 0, $"{focus} needs a crew to attack");
    }
}

/// <summary>
/// A rival that acts once every twenty minutes forever is not doing what a player does. A player is
/// away while turns bank up, then sits down and spends the lot, at their own hours. This pins the
/// shape of that: habits fixed to the rival, hours that actually exclude something, and a next
/// sitting that always lands inside them.
/// </summary>
/// <summary>
/// A run is capital tied up in a plane, so who sends one falls out of what each personality is for.
/// The runner moves goods for a living; the banker wants the money where it can see it; the hard
/// charger wants a fight rather than a wait.
/// </summary>
/// <summary>
/// A rival that forgets an attack the moment it lands goes back to picking whoever is richest, so
/// nothing between two of them ever becomes a story and the world reads as weather rather than
/// people. A grudge decides between fights they were already willing to take - never more than that.
/// </summary>
/// <summary>
/// The first version grouped a pair by comparing their ids and then read the aggressor off that
/// ordering, so a one-sided feud credited whichever name happened to sort first: it called the victim
/// the one kicking the door in about half the time. Ids group the two directions; they say nothing
/// about who started it.
/// </summary>
/// <summary>
/// A town is only a town if it has ground to fight over and prices of its own. Both are easy to get
/// silently wrong: the city list is derived from the map, so a town with no ground simply is not
/// there, and the market fills any gap with a bland Medium/Medium profile rather than complaining.
/// </summary>
/// <summary>
/// Risk used to describe only the way into a town: whether a run was stopped at the door, and nothing
/// about living there. Two players running identical operations in Detroit and New York were in
/// identical danger, which made the choice of town a price list rather than a place.
/// </summary>
/// <summary>
/// The game had one buyer before this: the city itself, fixed price, any amount, any hour. That is a
/// price list rather than a market, and it made producing a routine. An order has a shape - an
/// amount, a deadline, sometimes a condition - and every refusal has to be a real one.
/// </summary>
// The one rule in chat that cannot be got wrong quietly.
//
// Everywhere else in this game an unknown value fails to the most restrictive option - a door that
// cannot be read is shut, because handing somebody a crew by accident is worse than refusing one. A
// channel is the other way round, and deliberately: a line that lands somewhere more public than it
// was meant to is a mistake the person who typed it can see and answer for, while one that quietly
// goes to a crew it was not meant for cannot be taken back by anybody.
//
// So the failure is Global, and every private room has to be asked for by name.
// The rule that has to hold for direct messages: Direct is not a room, and nothing that turns a string
// into a channel can ever produce one.
//
// Parse falls to Global on anything it does not recognise, which is right for a room - a line landing
// somewhere more public than intended can at least be seen and answered for. That same fallback would
// be a disaster in the other direction, so the guard is that Parse cannot return Direct at all: a
// direct message is sent through its own path, with a recipient, or it is not sent.
// Blocking is a chat setting and has to stay one.
//
// The moment it also stops somebody raiding your house, it stops being a way to deal with an unpleasant
// person and becomes a move: block the strongest player on the board and sit behind it. So the guard is
// that nothing in combat reads the block table at all, and this test says so by walking the whole
// attack path with a block in place and watching it go through exactly as before.
// Direct messages were built as a pair - a message with a recipient, a thread worked out by folding
// those together. That shape held for two people and not for three: nothing to fold, no way to say who
// is in a conversation before anybody has spoken, nowhere to hang a name.
//
// So membership is the model, and a direct message is a conversation with two people in it. One
// mechanism rather than two that drift apart, and this pins the properties that depend on that.
static void AConversationIsWhoIsInIt()
{
    var options = Resolve(new GameOptions());

    // A pair carries no title and is named after whoever else is in it; a group carries its own.
    var pair = new Conversation
    {
        IsGroup = false,
        Members = [new ConversationMember { PlayerId = Guid.NewGuid() }, new ConversationMember { PlayerId = Guid.NewGuid() }]
    };
    AssertTrue(pair.Title is null, "a pair has no name of its own");
    AssertEqual(2, pair.Members.Count);

    var group = new Conversation
    {
        IsGroup = true,
        Title = "The Causeway job",
        Members = Enumerable.Range(0, 5).Select(_ => new ConversationMember { PlayerId = Guid.NewGuid() }).ToList()
    };
    AssertEqual(5, group.Members.Count);
    AssertTrue(group.Title is not null, "a group can be named");

    // A group has a ceiling, or it stops being a conversation and becomes a broadcast.
    AssertTrue(options.Chat.MaxGroupMembers >= 3, "a group holds more than a pair");
    AssertTrue(options.Chat.MaxGroupMembers <= 50, $"and not a crowd: {options.Chat.MaxGroupMembers}");

    // A message belongs to a conversation rather than to a person, which is what let the third person
    // exist at all. No room scope either: a conversation is not a place.
    var said = new ChatMessage
    {
        Channel = ChatChannel.Direct,
        ConversationId = 7,
        AuthorName = "You",
        Body = "meet at the docks"
    };
    AssertEqual(7L, said.ConversationId ?? 0);
    AssertTrue(said.City is null && said.AllianceId is null, "a conversation belongs to no town and no crew");

    // The read watermark is a real position rather than the guess the pair version used, which counted
    // anything newer than your own last reply and so called a conversation unread forever if you never
    // answered it.
    var member = new ConversationMember { PlayerId = Guid.NewGuid(), LastReadMessageId = 42 };
    AssertEqual(42L, member.LastReadMessageId);
}

static void BlockingIsChatAndNotCover()
{
    var options = Resolve(new GameOptions());
    var strikes = CreateStrikes(options);

    var attacker = new Player
    {
        Id = Guid.NewGuid(), Name = "Blocked", City = "Detroit", Turns = 40,
        Thugs = 4, Pistols = 4, Poison = 10, Hideout = new Hideout()
    };
    var defender = new Player
    {
        Id = Guid.NewGuid(), Name = "Blocker", City = "Detroit",
        Cash = 200_000, Rides = 1, Hoes = 6
    };

    // A block exists between them - the defender wants nothing to do with the attacker.
    var block = new PlayerBlock { BlockerId = defender.Id, BlockedId = attacker.Id };
    AssertEqual(defender.Id, block.BlockerId);
    AssertEqual(attacker.Id, block.BlockedId);

    // And it changes nothing about whether the attack is allowed. Every strike answers the same way it
    // would to a stranger, because none of this consults who is talking to whom.
    AssertTrue(strikes.WhyNot(AttackMethods.Jack, attacker, defender) is null, "a block does not save the cars");
    AssertTrue(strikes.WhyNot(AttackMethods.Infest, attacker, defender) is null, "nor the house");
    AssertTrue(strikes.WhyNot(AttackMethods.DriveBy, attacker, defender) is not null, "and the ordinary rules still apply");

    // The refusal a blocked person meets when they write is about messages and nothing else, and it is
    // the same sentence whichever direction the block runs, so it never reveals who blocked whom.
    var message = new ChatMessage
    {
        Channel = ChatChannel.Direct,
        ConversationId = 1,
        AuthorId = attacker.Id,
        AuthorName = attacker.Name,
        Body = "let me explain"
    };
    AssertEqual(ChatChannel.Direct, message.Channel);
    AssertTrue(message.City is null && message.AllianceId is null, "a direct message has no room scope");
}

static void ADirectMessageIsAddressedNeverPosted()
{
    // The word itself does not open the private channel.
    AssertTrue(ChatChannels.Parse("Direct") != ChatChannel.Direct, "Parse must never hand back Direct");
    AssertTrue(ChatChannels.Parse("direct") != ChatChannel.Direct, "in any case");
    AssertEqual(ChatChannel.Global, ChatChannels.Parse("Direct"));

    // And it is not one of the rooms anything enumerates.
    AssertTrue(!ChatChannels.All.Contains(ChatChannel.Direct), "Direct is not a room");
    AssertEqual(3, ChatChannels.All.Length);

    // A direct message carries both ends and no room scope, which is what makes a thread a pair rather
    // than a place: neither the town nor the crew has any bearing on who can read it.
    var sent = new ChatMessage
    {
        Channel = ChatChannel.Direct,
        ConversationId = 3,
        AuthorId = Guid.NewGuid(),
        AuthorName = "You",
        Body = "meet me at the docks"
    };
    AssertTrue(sent.City is null, "a direct message belongs to no town");
    AssertTrue(sent.AllianceId is null, "and to no crew");
    AssertTrue(sent.ConversationId is not null, "and always belongs to a conversation");

    // A room message is the mirror of that: a scope and nobody addressed.
    var posted = new ChatMessage
    {
        Channel = ChatChannel.City,
        City = "Detroit",
        AuthorName = "You",
        Body = "anybody about"
    };
    AssertTrue(posted.ConversationId is null, "a room message belongs to no conversation");
}

static void ChatFailsTowardsTheOpenRoom()
{
    // Nonsense, blank and missing all land in the open.
    AssertEqual(ChatChannel.Global, ChatChannels.Parse(null));
    AssertEqual(ChatChannel.Global, ChatChannels.Parse(""));
    AssertEqual(ChatChannel.Global, ChatChannels.Parse("   "));
    AssertEqual(ChatChannel.Global, ChatChannels.Parse("crew"));
    AssertEqual(ChatChannel.Global, ChatChannels.Parse("alliance-ish"));

    // The private rooms answer only to their own names, in any case.
    AssertEqual(ChatChannel.Alliance, ChatChannels.Parse("Alliance"));
    AssertEqual(ChatChannel.Alliance, ChatChannels.Parse("alliance"));
    AssertEqual(ChatChannel.City, ChatChannels.Parse("city"));

    // Three rooms, each named and described once.
    AssertEqual(3, ChatChannels.All.Length);
    AssertEqual(3, ChatChannels.All.Select(ChatChannels.Label).Distinct().Count());
    AssertEqual(3, ChatChannels.All.Select(ChatChannels.Describe).Distinct().Count());

    // And the scope is stored on the line rather than read off the author later, which is what keeps a
    // Detroit message a Detroit message after its author has moved to Miami.
    var said = new ChatMessage
    {
        Channel = ChatChannel.City,
        City = "Detroit",
        AuthorName = "Somebody",
        Body = "meet me at the docks"
    };
    AssertEqual("Detroit", said.City);
    AssertTrue(said.AllianceId is null, "a city line belongs to no crew");
}

static void ContractsAreDemandWithAShape()
{
    var options = Resolve(new GameOptions());
    var config = options.Contracts;

    // A buyer always beats the counter, or there is no reason to hold stock for a deadline.
    AssertTrue(config.MinPremiumPercent > 0, "an order pays over the counter price");
    // ...but never so far over that nothing else is worth doing.
    AssertTrue(config.MinPremiumPercent + config.PremiumSpreadPercent + config.PurityPremiumPercent < 100,
        "and never so far over that the rest of the game stops mattering");

    // A purity floor sometimes rather than always: on every order it would make stretching pointless
    // rather than a trade, which is the whole thing purity exists to be.
    AssertTrue(config.PurityConditionChance is > 0 and < 1, "some buyers care about strength, not all");
    // And a town leans towards what it values without ever ruling the other product out.
    AssertTrue(config.FavouredGoodPercent is >= 50 and < 100, "a town has a taste, not a single note");

    var order = new Contract
    {
        City = "Las Vegas",
        Buyer = "The Sands Room",
        Good = "coke",
        Quantity = 20,
        PricePerUnit = 300,
        ListPricePerUnit = 225,
        MinimumPurityPercent = 60,
        ExpiresAtUtc = Landing().AddHours(4)
    };
    AssertEqual(6_000L, order.Payout);
    AssertEqual(1_500L, order.Payout - order.FlatValue);

    // Open until it is filled or it runs out, and never both.
    AssertTrue(order.IsOpen(Landing()), "an order stands until its deadline");
    AssertTrue(!order.IsOpen(Landing().AddHours(5)), "and not past it");

    var service = CreateContracts(options);
    var seller = new Player { City = "Las Vegas", Coke = 50, CokePurity = 0.9, Cash = 0 };

    // Every refusal is a real one, against the same stock the rest of the game moves.
    AssertRuleError(() => service.Deliver(order, new Player { City = "Las Vegas", Coke = 0 }, Landing()), "no coke");
    AssertRuleError(() => service.Deliver(order, new Player { City = "Las Vegas", Coke = 50, CokePurity = 0.3 }, Landing()),
        "at least 60% pure");
    AssertRuleError(() => service.Deliver(order, new Player { City = "Detroit", Coke = 50, CokePurity = 0.9 }, Landing()),
        "You have to be there");
    // Asking to hand over more than is held is refused rather than quietly reduced.
    AssertRuleError(() => service.Deliver(order, new Player { City = "Las Vegas", Coke = 5, CokePurity = 0.9 }, Landing(), 9),
        "and you have 5");

    var fill = service.Deliver(order, seller, Landing());
    AssertEqual(6_000L, seller.Cash);
    AssertEqual(30, seller.Coke);
    AssertEqual(1_500L, fill.Premium);
    AssertTrue(fill.Completed, "handing over the whole order at once still finishes it");
    // Selling a share of a blend leaves the blend alone, exactly as selling flat does.
    AssertEqual(0.9, seller.CokePurity);

    // Filled once, and then it is gone rather than a repeatable source of money.
    AssertTrue(!order.IsOpen(Landing()), "a filled order is closed");
    AssertRuleError(() => service.Deliver(order, seller, Landing()), "already gone");
}

// The rooms this order is aimed at cannot hold it. A first storage room holds five weapons and ten of
// coke against orders that run to sixty, so insisting on one movement made most of the board
// unfillable for exactly the players it was meant to give something to aim at.
static void AnOrderGoesInAsFastAsTheRoomAllows()
{
    var options = Resolve(new GameOptions());
    var service = CreateContracts(options);

    Contract Order() => new()
    {
        City = "Las Vegas",
        Buyer = "The Sands Room",
        Good = "coke",
        Quantity = 20,
        PricePerUnit = 300,
        ListPricePerUnit = 225,
        ExpiresAtUtc = Landing().AddHours(4)
    };

    // A room that holds ten works a twenty order in two trips.
    var order = Order();
    var small = new Player { Id = Guid.NewGuid(), City = "Las Vegas", Coke = 10, Cash = 0 };

    var first = service.Deliver(order, small, Landing());
    AssertEqual(10, first.Delivered);
    AssertTrue(!first.Completed, "half an order is not a finished one");
    // Paid the town's ordinary rate, and not a penny of the premium yet.
    AssertEqual(2_250L, small.Cash);
    AssertEqual(0L, first.Premium);
    AssertEqual(10, order.Remaining);
    AssertEqual(0, small.Coke);

    small.Coke = 10;
    var second = service.Deliver(order, small, Landing());
    AssertTrue(second.Completed, "the last unit finishes it");
    AssertEqual(0, order.Remaining);
    // The premium is never split, so two trips pay exactly what one would have.
    AssertEqual(1_500L, second.Premium);
    AssertEqual(6_000L, small.Cash);
    AssertEqual(Order().Payout, small.Cash);

    // Stopping half way leaves a player exactly where selling flat would have: the premium is what
    // finishing buys, so instalments are never free money.
    var abandoned = Order();
    var quitter = new Player { Id = Guid.NewGuid(), City = "Las Vegas", Coke = 8, Cash = 0 };
    service.Deliver(abandoned, quitter, Landing());
    AssertEqual(8 * 225L, quitter.Cash);

    // An order somebody has started is theirs. Without this the player who worked hardest and arrived
    // last would simply have wasted the goods.
    var rival = new Player { Id = Guid.NewGuid(), City = "Las Vegas", Coke = 20 };
    AssertRuleError(() => service.Deliver(abandoned, rival, Landing()), "Somebody else");
    AssertEqual(20, rival.Coke);

    // And the buyer never takes more than they asked for.
    var nearly = Order();
    var seller = new Player { Id = Guid.NewGuid(), City = "Las Vegas", Coke = 60 };
    service.Deliver(nearly, seller, Landing(), 18);
    AssertRuleError(() => service.Deliver(nearly, seller, Landing(), 5), "only still want 2");
    service.Deliver(nearly, seller, Landing());
    AssertEqual(20, nearly.DeliveredQuantity);
    AssertEqual(40, seller.Coke);
}

static ContractService CreateContracts(GameOptions options)
    => new(null!, Snapshot(options), new MinimumRandom());

static void CityRiskReachesTheDailyLoop()
{
    var options = Resolve(new GameOptions());
    var markets = options.CityMarkets;
    var hideouts = CreateHideouts(options);

    // Detroit is the quiet town and New York the watchful one, so the same stash reads differently.
    AssertTrue(markets.HeatMultiplier("Detroit") < markets.HeatMultiplier("Atlanta"), "a quiet town looks less hard");
    AssertTrue(markets.HeatMultiplier("New York") > markets.HeatMultiplier("Atlanta"), "a watchful one looks harder");

    var quiet = new Player { City = "Detroit", Coke = 100 };
    var watchful = new Player { City = "New York", Coke = 100 };
    AssertTrue(hideouts.HeatFor(watchful) > hideouts.HeatFor(quiet),
        $"the same stash is hotter in New York ({hideouts.HeatFor(watchful)}) than Detroit ({hideouts.HeatFor(quiet)})");

    // Earned heat is banked points and is not re-scaled by where it was earned, or moving town would
    // silently rewrite a player's history rather than change what happens next.
    var carried = new Player { City = "New York", Heat = 40 };
    AssertEqual(40.0, hideouts.HeatFor(carried));

    // A shift in a watchful town earns more notice than the same shift in a quiet one.
    var economy = CreateEconomy(options);
    var inDetroit = Working("Detroit");
    var inNewYork = Working("New York");
    economy.Scout(inDetroit, 10);
    economy.Scout(inNewYork, 10);
    AssertTrue(inNewYork.Heat > inDetroit.Heat,
        $"ten turns draws more notice in New York ({inNewYork.Heat:F1}) than Detroit ({inDetroit.Heat:F1})");

    // Risk pairs with reward, or it is only a penalty for living in the wrong place. The towns that
    // watch hardest are the ones that pay best.
    var cities = options.Territory.Cities();
    var watched = cities.Where(c => markets.HeatMultiplier(c) > 1).ToList();
    AssertTrue(watched.Count > 0, "somewhere watches harder than average");
    var bestPaying = cities.Max(c => markets.ProductPrice(c, "coke", 100));
    AssertTrue(watched.Any(c => markets.ProductPrice(c, "coke", 100) == bestPaying),
        "the best coke price in the game is in a town that watches");

    static Player Working(string city) => new()
    {
        City = city,
        Turns = 100,
        Pimps = 1,
        Hoes = 6,
        Thugs = 2,
        Condoms = 500,
        Beer = 500,
        Pistols = 2,
        HoeHappiness = 90,
        ThugHappiness = 90,
        HoeCutPercent = 30,
        Hideout = new Hideout { Tier = 1, StorageLevel = 3, SafeLevel = 3 }
    };
}

static void EveryCityIsRealAndDistinct()
{
    var options = Resolve(new GameOptions());
    var territory = options.Territory;
    var cities = territory.Cities();

    string[] expected = ["Atlanta", "Chicago", "Detroit", "Houston", "Las Vegas", "Los Angeles", "Miami", "New York"];
    AssertEqual(expected.Length, cities.Count);
    foreach (var city in expected)
        AssertTrue(cities.Contains(city), $"{city} is a town you can set up in");

    foreach (var city in cities)
    {
        // Ground, or the territory page is an empty room. That every town carries all four types is
        // checked elsewhere, and matters because a town is chosen at sign-up knowing nothing: one
        // missing an effect would punish a blind choice for as long as the player stayed there.
        var ground = territory.Map.Count(x => string.Equals(x.City, city, StringComparison.OrdinalIgnoreCase));
        AssertTrue(ground >= 4, $"{city} has {ground} piece(s) of ground to fight over");

        // A profile of its own. Without one the market quietly invents Medium/Medium, and a town that
        // prices everything the same as everywhere else is a town with no reason to travel to it.
        var profile = options.CityMarkets.Profiles
            .SingleOrDefault(x => string.Equals(x.City, city, StringComparison.OrdinalIgnoreCase));
        AssertTrue(profile is not null, $"{city} has its own market profile");
        AssertTrue(profile!.TravelTurns is > 0, $"{city} is a real distance away");
    }

    // Ground names are the seeding key, so a duplicate would silently swallow a second town's piece.
    var duplicated = territory.Map.GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
    AssertTrue(duplicated.Count == 0, $"every piece of ground is named once: {string.Join(", ", duplicated)}");

    // Every town needs rivals in it. One with none has an empty leaderboard and nobody to fight, so
    // putting it on the map only looks like a choice at sign-up.
    var templates = BotTemplates();
    foreach (var city in cities)
        AssertTrue(templates.Count(x => string.Equals(x.City, city, StringComparison.OrdinalIgnoreCase)) >= 3,
            $"{city} has rivals living in it");

    // The map only pays if towns actually differ, so somewhere has to buy cheap and somewhere dear.
    foreach (var good in new[] { "weed", "coke" })
    {
        var prices = cities.Select(c => options.CityMarkets.ProductPrice(c, good, 100)).Distinct().ToList();
        AssertTrue(prices.Count >= 3, $"{good} is priced at least three different ways across the map");
    }
    AssertTrue(cities.Any(c => options.CityMarkets.ProductPrice(c, "coke", 100) < 100)
               && cities.Any(c => options.CityMarkets.ProductPrice(c, "coke", 100) > 100),
        "there is somewhere to buy coke cheap and somewhere to sell it dear");
}

static void FeudsNameTheAggressor()
{
    // Deliberately the id that sorts second, since sorting is exactly what the bug relied on.
    var bully = new Guid("ffffffff-0000-0000-0000-000000000000");
    var victim = new Guid("00000000-0000-0000-0000-000000000001");
    var oneSided = Enumerable.Repeat(new FeudRound(bully, victim, "Grit Baron", "Velvet Bishop"), 3).ToList();

    var feud = WorldFeuds.Pick(oneSided)!;
    AssertEqual("Grit Baron", feud.Aggressor);
    AssertEqual("Velvet Bishop", feud.Victim);
    AssertTrue(!feud.BothWays, "one name doing all the hitting is not a mutual quarrel");
    AssertTrue(WorldFeuds.Describe(feud).StartsWith("Grit Baron has been through Velvet Bishop"),
        $"the news names the right aggressor: {WorldFeuds.Describe(feud)}");

    // Blows in both directions are one quarrel, counted together and reported as mutual.
    var mutual = oneSided.Concat([new FeudRound(victim, bully, "Velvet Bishop", "Grit Baron")]).ToList();
    var traded = WorldFeuds.Pick(mutual)!;
    AssertEqual(4, traded.Rounds);
    AssertTrue(traded.BothWays, "hits in both directions are a mutual quarrel");
    AssertTrue(WorldFeuds.Describe(traded).Contains("both have been on the receiving end"), "and it reads that way");

    // A mutual quarrel outranks a longer one-sided beating, because it is the better story.
    var third = new Guid("88888888-0000-0000-0000-000000000000");
    var mixed = mutual
        .Concat(Enumerable.Repeat(new FeudRound(third, victim, "Switch Lane", "Velvet Bishop"), 9))
        .ToList();
    AssertTrue(WorldFeuds.Pick(mixed)!.BothWays, "a feud beats a beating");

    // One or two scraps are not a feud, or every fight in the world becomes a headline.
    AssertTrue(WorldFeuds.Pick(oneSided.Take(2).ToList()) is null, "two raids is not yet a story");
    AssertTrue(WorldFeuds.Pick([]) is null, "and a quiet week has no headline at all");
}

static void BotsHoldGrudges()
{
    var antiFarm = new AntiFarmOptions { MinDefenderNetWorth = 0, MaxNetWorthRatio = 100, MaxIncomingAttacks = 2 };
    var rich = new BotTarget(Guid.NewGuid(), "Fat Wallet", 500_000, 10, false, 0);
    var enemy = new BotTarget(Guid.NewGuid(), "The One Who Robbed Me", 300_000, 10, false, 0);
    var field = new[] { rich, enemy };

    // With no memory, the fattest target wins every time. That was the whole behaviour before.
    AssertEqual(rich.Name, BotTargeting.Choose(field, 400_000, 100, antiFarm, 1)!.Name);

    // A rival who holds grudges goes after the one who robbed them instead, even though it pays less.
    var grudges = new Dictionary<Guid, int> { [enemy.PlayerId] = 2 };
    AssertEqual(enemy.Name, BotTargeting.Choose(field, 400_000, 100, antiFarm, 1, grudges, 0.9)!.Name);

    // One who does not still takes the better deal, which is what keeps the personalities distinct.
    AssertEqual(rich.Name, BotTargeting.Choose(field, 400_000, 100, antiFarm, 1, grudges, 0.1)!.Name);

    // A grudge never makes them reckless: a target they cannot beat is still refused, however personal.
    var untouchable = new BotTarget(Guid.NewGuid(), "Too Strong", 900_000, 5_000, false, 0);
    var hopeless = new Dictionary<Guid, int> { [untouchable.PlayerId] = 9 };
    AssertTrue(BotTargeting.Choose([untouchable], 400_000, 100, antiFarm, 1, hopeless, 0.9) is null,
        "a score to settle is not a reason to lose");
    // Nor does it reach past a shield or through the incoming cap.
    var shielded = enemy with { IsProtected = true };
    AssertEqual(rich.Name, BotTargeting.Choose([rich, shielded], 400_000, 100, antiFarm, 1, grudges, 0.9)!.Name);

    // Who carries it follows from character: the hard charger takes it personally, the banker does not.
    var charger = BotGrudgeProfile.For(BotBrainFocus.MoraleNeglecter);
    var banker = BotGrudgeProfile.For(BotBrainFocus.Banker);
    AssertTrue(charger.Weight > banker.Weight, "hard chargers take it personally");
    AssertTrue(charger.MemoryHours > banker.MemoryHours, "and hold it for longer");
    foreach (var focus in Enum.GetValues<BotBrainFocus>())
    {
        var profile = BotGrudgeProfile.For(focus);
        AssertTrue(profile.Weight is > 0 and <= 1, $"{focus} carries a usable grudge");
        AssertTrue(profile.MemoryHours > 0, $"{focus} remembers for some length of time");
    }
}

static void BotMuleProfilesScaleWithPersonality()
{
    var runner = BotMuleProfile.For(BotBrainFocus.ProductRunner);
    var banker = BotMuleProfile.For(BotBrainFocus.Banker);
    var charger = BotMuleProfile.For(BotBrainFocus.MoraleNeglecter);

    AssertTrue(runner.RunChance > banker.RunChance, "product runners run mules more than bankers");
    AssertTrue(runner.RunChance > charger.RunChance, "and more than hard chargers, who would rather fight");
    AssertTrue(runner.CashShare > banker.CashShare, "and commit more of the purse to it");
    AssertTrue(banker.MinimumProfit > runner.MinimumProfit, "a banker wants a wider margin before it bothers");

    // Every personality is sane: it sometimes runs, never sends the whole purse, and never sends a
    // run it expects to lose on.
    foreach (var focus in Enum.GetValues<BotBrainFocus>())
    {
        var profile = BotMuleProfile.For(focus);
        AssertTrue(profile.RunChance is > 0 and <= 1, $"{focus} has a usable run chance");
        AssertTrue(profile.CashShare is > 0 and < 1, $"{focus} keeps some money at home");
        AssertTrue(profile.MaxHoes is > 0 and <= 6, $"{focus} sends a sane number of hoes");
        AssertTrue(profile.MinimumProfit > 0, $"{focus} does not send runs it expects to lose on");
    }
}

static void BotSchedulesLookLikePeople()
{
    var options = new BotAutomationOptions();
    var random = new AlwaysRandom();

    // Habits belong to the rival, not to the roll: the same rival is the same person every time.
    var bot = new Player { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), AccountId = Guid.Parse("22222222-2222-2222-2222-222222222222"), Name = "Repeat" };
    var brain = BotBrain.For(bot);
    var once = BotSchedule.For(bot, brain, options);
    AssertEqual(once, BotSchedule.For(bot, brain, options));

    // Eager personalities play more often than patient ones, read off the character rather than a
    // second dial that could disagree with the first.
    var charger = ScheduleFor(BotBrainFocus.MoraleNeglecter, options);
    var banker = ScheduleFor(BotBrainFocus.Banker, options);
    AssertTrue(charger.SessionsPerDay >= banker.SessionsPerDay,
        $"hard chargers play at least as often as bankers ({charger.SessionsPerDay} vs {banker.SessionsPerDay})");

    // A rival that keeps hours is genuinely absent outside them, or "hours" means nothing.
    var sleeper = new BotSchedule(4, PeakHourUtc: 20, WindowHours: 8, NeverSleeps: false);
    AssertTrue(sleeper.IsAwake(new DateTime(2026, 1, 1, 20, 0, 0)), "a sleeper is up at its peak");
    AssertTrue(!sleeper.IsAwake(new DateTime(2026, 1, 1, 4, 0, 0)), "a sleeper is away at four in the morning");

    // The window wraps midnight rather than clipping at it, which a plain subtraction would get wrong.
    var nightOwl = new BotSchedule(4, PeakHourUtc: 0, WindowHours: 8, NeverSleeps: false);
    AssertTrue(nightOwl.IsAwake(new DateTime(2026, 1, 1, 23, 0, 0)), "an hour before midnight is inside a midnight window");
    AssertTrue(nightOwl.IsAwake(new DateTime(2026, 1, 1, 3, 0, 0)), "and so is three hours after it");
    AssertTrue(!nightOwl.IsAwake(new DateTime(2026, 1, 1, 12, 0, 0)), "noon is not");

    // The next sitting is always inside the rival's hours, whenever it is asked from. Asked at four in
    // the morning it must skip forward to the evening rather than schedule a session nobody plays.
    for (var hour = 0; hour < 24; hour++)
    {
        var next = sleeper.NextSessionStart(new DateTime(2026, 1, 1, hour, 0, 0), random);
        AssertTrue(next > new DateTime(2026, 1, 1, hour, 0, 0), $"the next sitting is in the future from {hour}:00");
        AssertTrue(sleeper.IsAwake(next), $"the sitting booked from {hour}:00 lands inside its hours, not at {next:HH:mm}");
    }

    // A rival with no hours is available at every one of them, which is what keeps the board alive
    // for anyone playing at an odd time.
    var always = new BotSchedule(4, PeakHourUtc: 20, WindowHours: 8, NeverSleeps: true);
    for (var hour = 0; hour < 24; hour++)
        AssertTrue(always.IsAwake(new DateTime(2026, 1, 1, hour, 0, 0)), $"an always-on rival is up at {hour}:00");

    // Sessions per day stay inside the configured band for every personality, so no rival plays
    // ninety times a day or once a week.
    foreach (var focus in Enum.GetValues<BotBrainFocus>())
    {
        var schedule = ScheduleFor(focus, options);
        AssertTrue(schedule.SessionsPerDay >= options.MinSessionsPerDay && schedule.SessionsPerDay <= options.MaxSessionsPerDay,
            $"{focus} plays {schedule.SessionsPerDay}x a day, inside the configured band");
    }
}

/// <summary>Finds a rival whose seed lands on the wanted personality, so a focus can be tested directly.</summary>
static BotSchedule ScheduleFor(BotBrainFocus focus, BotAutomationOptions options)
{
    for (var seed = 0; seed < 4096; seed++)
    {
        var id = new Guid(seed, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0]);
        var bot = new Player { Id = id, AccountId = id, Name = $"Seed {seed}" };
        var brain = BotBrain.For(bot);
        if (brain.Focus == focus)
            return BotSchedule.For(bot, brain, options);
    }

    throw new InvalidOperationException($"No seed produced a {focus} rival.");
}

/// <summary>
/// A run buys time and presence with crew and money. This pins the price of that: fewer turns than
/// travelling yourself, real cash before anybody leaves, crew off the books while they are gone, and
/// every number the outcome will depend on written down at launch rather than read again later.
/// </summary>
static void MuleRunsArePricedAndFrozen()
{
    var options = Resolve(new GameOptions());
    var mules = CreateMules(options);
    var hideouts = CreateHideouts(options);

    // Los Angeles is six turns out on the shipped map; Detroit is two.
    var player = new Player { City = "Los Angeles", Cash = 200_000, Turns = 100, Hoes = 20 };
    player.Hideout = new Hideout { Tier = 2, IntelligenceLevel = 1 };

    // Without the room there are no runs at all: the intelligence centre is the gate, not a discount.
    var roomless = new Player { City = "Los Angeles", Cash = 200_000, Turns = 100, Hoes = 20, Hideout = new Hideout { Tier = 2 } };
    AssertEqual(0, hideouts.ConcurrentRunCap(roomless.Hideout));
    AssertRuleError(
        () => mules.Launch(roomless, Pimp(roomless, "Vic", 100), "Detroit", "weed", 2, 10_000, 0, DateTime.UtcNow),
        "You need an intelligence centre before you can run mules.");

    // A run is cheaper in turns than going yourself, which costs the distance each way.
    var quote = mules.Quote(player, "Detroit", "weed", 3, 30_000);
    AssertEqual(2, quote.TravelTurns);
    AssertEqual(1, quote.Turns);
    AssertTrue(quote.Turns < quote.TravelTurns * 2, "a run costs fewer turns than the round trip it replaces");

    // Three hoes carry one hundred thirty-five units, and the fare and upkeep are charged for four heads.
    AssertEqual(135, quote.Capacity);
    AssertEqual(4 * 2 * 60L, quote.Fare);
    AssertEqual(34, quote.TripMinutes);
    AssertEqual(quote.CashSent + quote.Fare + quote.Upkeep, quote.TotalCost);
    AssertTrue(quote.Upkeep > 0, "keeping crew away costs something");
    AssertEqual(mules.BustChancePercent(player, "Detroit", 3), quote.BustChancePercent);

    // They buy at the destination's price, not at ours, which is the entire reason to go.
    AssertEqual(options.CityMarkets.ProductPrice("Detroit", "weed", options.WeedSellPrice), quote.UnitPriceThere);
    AssertTrue(quote.UnitPriceThere < options.CityMarkets.ProductPrice("Los Angeles", "weed", options.WeedSellPrice),
        "there is no point running to a town that is dearer than home");

    // A run has to go somewhere else, and only carries what is worth a plane ticket.
    AssertRuleError(() => mules.Quote(player, "Nowhere", "weed", 1, 1_000), "Pick one of");
    AssertRuleError(() => mules.Quote(player, "Detroit", "beer", 1, 1_000), "A mule run carries weed or coke.");
    AssertRuleError(
        () => mules.Launch(player, Pimp(player, "Vic", 100), "Los Angeles", "weed", 1, 1_000, 0, DateTime.UtcNow),
        "A mule run has to go somewhere else.");

    // The cap is the room's, and it is counted against what is already in the air.
    AssertRuleError(
        () => mules.Launch(player, Pimp(player, "Vic", 100), "Detroit", "weed", 3, 30_000, 1, DateTime.UtcNow),
        "can only run 1 at a time");

    var launchedAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    var run = mules.Launch(player, Pimp(player, "Vic", 100), "Detroit", "weed", 3, 30_000, 0, launchedAt);

    // Paid for before anybody leaves, and the hoes are off the books while they are gone: counting
    // them at home would have them working the streets and carrying cargo at the same time.
    AssertEqual(200_000L - quote.TotalCost, player.Cash);
    AssertEqual(99, player.Turns);
    AssertEqual(17, player.Hoes);

    // Both legs are booked at launch, so the run is a flight rather than a teleport.
    AssertEqual(launchedAt.AddMinutes(12), run.ArrivesAtUtc);
    AssertEqual(launchedAt.AddMinutes(34), run.ReturnsAtUtc);
    AssertEqual(MuleRunStatus.Outbound, mules.StatusAt(run, launchedAt.AddMinutes(5)));
    AssertEqual(MuleRunStatus.Inbound, mules.StatusAt(run, launchedAt.AddMinutes(20)));
    AssertEqual(MuleRunStatus.Done, mules.StatusAt(run, launchedAt.AddMinutes(40)));
    AssertTrue(run.IsOut, "a run is out until it is settled");

    // Frozen at launch. A pimp whose loyalty slips mid-flight must not change a run already in the air.
    AssertEqual(100.0, run.PimpLoyaltyAtLaunch);
    AssertEqual(135, run.Capacity);
    AssertEqual(30_000L, run.CashSent);

    // A loyal pimp does not walk; a wavering one is likelier to the further he is sent.
    AssertEqual(0, mules.DefectChancePercent(player, Pimp(player, "Loyal", 100), "Detroit"));
    var wavering = mules.DefectChancePercent(player, Pimp(player, "Shaky", 20), "Detroit");
    AssertTrue(wavering > 0, "a pimp well below the threshold might not come back");
    AssertTrue(mules.DefectChancePercent(player, Pimp(player, "Shaky", 20), "Los Angeles") > wavering,
        "distance makes walking away easier");

    // Knowing the route takes a share off the risk, but never all of it.
    var blind = mules.BustChancePercent(roomless, "New York", 1);
    var briefed = mules.BustChancePercent(player, "New York", 1);
    AssertTrue(briefed < blind, $"an intelligence centre lowers the risk ({briefed} vs {blind})");
    AssertTrue(briefed > 0, "a briefing is not a guarantee");
    AssertTrue(mules.BustChancePercent(player, "New York", 6) > briefed, "more bodies are easier to notice");

    // Sending crew you do not have, or money you cannot cover, is refused rather than run on credit.
    var thin = new Player { City = "Los Angeles", Cash = 200_000, Turns = 100, Hoes = 1, Hideout = new Hideout { Tier = 2, IntelligenceLevel = 1 } };
    AssertRuleError(
        () => mules.Launch(thin, Pimp(thin, "Vic", 100), "Detroit", "weed", 4, 30_000, 0, launchedAt),
        "hoe(s) to send");
    AssertRuleError(
        () => mules.Launch(player, Pimp(player, "Vic", 100), "Detroit", "weed", 1, 0, 0, launchedAt),
        "Send them with something to buy with.");
    AssertRuleError(
        () => mules.Launch(player, Pimp(player, "Vic", 100), "Detroit", "weed", 1, 5_000_000, 0, launchedAt),
        "costs");
}

/// <summary>
/// The three ways a run ends. Buying happens at settlement rather than at launch, because they buy at
/// the destination's price and the only moment that price is real is when they are standing in it.
/// </summary>
static void MuleRunsSettleThreeWays()
{
    var options = Resolve(new GameOptions());
    var mules = CreateMules(options);
    var price = options.CityMarkets.ProductPrice("Detroit", "weed", options.WeedSellPrice);

    // Delivered: cargo home, and cash they never spent comes back with them.
    var lucky = Loaded();
    var run = Out(lucky, cash: 30_000, hoes: 3);
    run.BustChancePercent = 0;
    run.DefectChancePercent = 0;
    var settled = mules.Settle(run, lucky, Pimp(lucky, "Vic", 100), new MinimumRandom(), Landing());

    AssertEqual(MuleRunOutcome.Delivered, run.Outcome);
    // One hundred thirty-five is what three hoes carry, so the load binds long before the money does.
    AssertEqual(135, run.UnitsBought);
    AssertEqual(30_000L - 135 * price, run.CashReturned);
    AssertEqual(135, lucky.Weed);
    AssertEqual(20, lucky.Hoes);
    AssertEqual(30_000L - 135 * price, lucky.Cash);
    AssertEqual(135, settled.UnitsDelivered);
    AssertTrue(!run.IsOut, "a settled run is no longer out");

    // Seized: a share of the load goes, the unspent cash goes with it because it was in the room when
    // the door came in, and the heat lands on the player who sent them.
    var stopped = Loaded();
    var seizedRun = Out(stopped, cash: 30_000, hoes: 3);
    seizedRun.BustChancePercent = 100;
    seizedRun.DefectChancePercent = 0;
    mules.Settle(seizedRun, stopped, Pimp(stopped, "Vic", 100), new AlwaysRandom(), Landing());

    AssertEqual(MuleRunOutcome.Seized, seizedRun.Outcome);
    AssertEqual(0L, seizedRun.CashReturned);
    AssertEqual(0L, stopped.Cash);
    AssertTrue(seizedRun.SeizedUnits > 0, "a stop takes something");
    AssertEqual(135 - seizedRun.SeizedUnits, stopped.Weed);
    AssertEqual(seizedRun.SeizedUnits * options.Mules.HeatPerSeizedUnit, stopped.Heat);
    AssertEqual(20, stopped.Hoes);
    AssertTrue(seizedRun.Summary.Contains("was stopped"), $"the notice says what happened: {seizedRun.Summary}");

    // Defected: he keeps the money, the goods and the crew, and comes off the payroll.
    var robbed = Loaded();
    var gone = Out(robbed, cash: 30_000, hoes: 3);
    gone.BustChancePercent = 0;
    gone.DefectChancePercent = 100;
    var pimp = Pimp(robbed, "Vic", 20);
    mules.Settle(gone, robbed, pimp, new AlwaysRandom(), Landing());

    AssertEqual(MuleRunOutcome.Defected, gone.Outcome);
    AssertEqual(0, robbed.Weed);
    AssertEqual(0L, robbed.Cash);
    AssertEqual(17, robbed.Hoes);
    AssertEqual(3, gone.HoesLost);
    AssertTrue(gone.PimpLost && pimp.LostAtUtc is not null, "a pimp who runs is off the payroll");

    // Settling stamps the run, which is what stops the clock paying the same run out twice.
    AssertEqual(Landing(), run.SettledAtUtc);
    AssertEqual(MuleRunStatus.Done, mules.StatusAt(run, Landing().AddMinutes(-99)));

    // A load bigger than the room is left behind rather than overfilling it, as a lab would be.
    var cramped = Loaded();
    cramped.Hideout = new Hideout { Tier = 1, StorageLevel = 1, IntelligenceLevel = 1 };
    var overflowing = Out(cramped, cash: 30_000, hoes: 3);
    overflowing.BustChancePercent = 0;
    overflowing.DefectChancePercent = 0;
    var tight = mules.Settle(overflowing, cramped, Pimp(cramped, "Vic", 100), new MinimumRandom(), Landing());
    var room = CreateHideouts(options).CapacityFor(cramped.Hideout).MaxWeed;
    AssertEqual(room, cramped.Weed);
    AssertEqual(room, tight.UnitsDelivered);
    // The player paid for all 135. A run that quietly dropped the rest would read as the price being
    // wrong rather than the room being full, so the notice has to say so.
    AssertEqual(135, overflowing.UnitsBought);
    AssertTrue(overflowing.Summary.Contains($"{135 - room:N0} weed was dumped"),
        $"a short delivery says why: {overflowing.Summary}");

    static Player Loaded() => new()
    {
        City = "Los Angeles",
        Cash = 0,
        Hoes = 17,
        Hideout = new Hideout { Tier = 2, StorageLevel = 6, IntelligenceLevel = 1 }
    };
}

/// <summary>
/// Travel used to be instant, which made a town's distance a pure turn cost: you were somewhere else
/// the moment you decided to be. Now the distance is time too, and there is nothing to do from a plane.
/// </summary>
static void TravelIsAFlightYouCannotActFrom()
{
    var flyer = new Player { City = "Los Angeles", TravelArrivesAtUtc = DateTime.UtcNow.AddMinutes(10) };
    AssertTrue(flyer.IsInTransit(DateTime.UtcNow), "a player mid-flight is in transit");
    AssertRuleError(() => TravelGate.EnsureLanded(flyer), "You are in the air");

    // Every way of acting runs through a service that checks this, so none of them work in the air.
    var economy = CreateEconomy();
    AssertRuleError(() => economy.Scout(flyer, 1), "You are in the air");
    AssertRuleError(() => economy.Deposit(flyer, 1), "You are in the air");
    AssertRuleError(() => economy.Travel(flyer, "Detroit"), "You are in the air");

    // Landed is landed, whether the clock cleared it or the moment simply passed.
    var landed = new Player { City = "Detroit", TravelArrivesAtUtc = DateTime.UtcNow.AddMinutes(-1) };
    AssertTrue(!landed.IsInTransit(DateTime.UtcNow), "a flight in the past is over");
    TravelGate.EnsureLanded(landed);
}

/// <summary>When the plane is due. A method, since top-level statements have no fields.</summary>
static DateTime Landing() => new(2026, 1, 1, 13, 0, 0, DateTimeKind.Utc);

/// <summary>A run already in the air, so settlement can be tested without replaying a launch.</summary>
static MuleRun Out(Player player, long cash, int hoes) => new()
{
    PlayerId = player.Id,
    OriginCity = player.City,
    DestinationCity = "Detroit",
    Good = "weed",
    Status = MuleRunStatus.Inbound,
    Outcome = MuleRunOutcome.Pending,
    PimpId = 1,
    PimpName = "Vic",
    PimpLoyaltyAtLaunch = 100,
    AssignedHoes = hoes,
    Capacity = hoes * 45,
    CashSent = cash,
    DepartedAtUtc = Landing().AddHours(-1),
    ArrivesAtUtc = Landing().AddMinutes(-30),
    ReturnsAtUtc = Landing()
};

static Pimp Pimp(Player owner, string name, double loyalty)
    => new() { Id = 1, PlayerId = owner.Id, Name = name, Loyalty = loyalty };

static MuleService CreateMules(GameOptions options)
    => new(Snapshot(options), CreateHideouts(options));

// A CombatLog stores the attacker's outcome, so telling the defender "Victory" would say they won a
// fight they lost. This is the flip, and it is the whole reason the describer exists.
static void DefenceAlertsFlipPerspective()
{
    var attacker = new Player { Name = "Lucky Voss" };
    var at = new DateTime(2026, 8, 12, 12, 0, 0, DateTimeKind.Utc);

    var robbed = DefenceAlerts.Describe(new CombatLog
    {
        Id = 1, Attacker = attacker, Outcome = "Victory", CreatedAtUtc = at,
        CashStolen = 12_500, WeedStolen = 8, DefenderThugsLost = 3, DefenderPimpsLost = 1
    }, seenAtUtc: null);

    AssertTrue(!robbed.HeldTheHouse, "an attacker victory means the defender did not hold");
    AssertTrue(robbed.Headline.Contains("broke through"), "the headline reads from the defender's side");
    AssertTrue(robbed.Headline.Contains("Lucky Voss"), "the attacker is named");
    AssertTrue(robbed.Detail.Contains("$12,500"), "stolen cash is reported");
    AssertTrue(robbed.Detail.Contains("1 pimp"), "a lost pimp is called out");
    AssertEqual(12_500L, robbed.CashLost);

    // The attacker losing is good news for the reader.
    var held = DefenceAlerts.Describe(new CombatLog
    {
        Id = 2, Attacker = attacker, Outcome = "Defeat", CreatedAtUtc = at
    }, seenAtUtc: null);
    AssertTrue(held.HeldTheHouse, "an attacker defeat means the defender held");
    AssertTrue(held.Headline.Contains("held"), "the headline says they held");
    AssertTrue(held.Detail.Contains("Nothing was taken"), "a clean defence says so");

    var standstill = DefenceAlerts.Describe(new CombatLog { Id = 3, Attacker = attacker, Outcome = "Standstill", CreatedAtUtc = at }, null);
    AssertTrue(standstill.HeldTheHouse, "a standstill is not a loss");

    var called = DefenceAlerts.Describe(new CombatLog { Id = 4, Attacker = attacker, Outcome = "Canceled", CreatedAtUtc = at }, null);
    AssertTrue(called.Headline.Contains("called off"), "a cancelled raid is described as such");

    // A missing attacker row must not blow up the alert.
    var orphan = DefenceAlerts.Describe(new CombatLog { Id = 5, Outcome = "Victory", CreatedAtUtc = at }, null);
    AssertTrue(orphan.AttackerName == "Someone", "an unnamed attacker still produces an alert");
}

static void CatchUpReportsWhatHappenedWhileAway()
{
    var since = new DateTime(2026, 8, 13, 6, 0, 0, DateTimeKind.Utc);
    var now = since.AddHours(9);
    static CatchUpFacts Quiet(DateTime since, DateTime now)
        => new(since, now, 0, 0, 0, 0, 0, 0, 0, 0, 0, [], 40, 200, null);

    // Nothing happened, so there is nothing to interrupt anyone with.
    var quiet = CatchUp.Build(Quiet(since, now));
    AssertTrue(!quiet.HasNews, "a quiet spell raises no popup");
    AssertEqual(0, quiet.Items.Count);
    AssertEqual(540, quiet.AwayMinutes);

    // Robbed twice, held once.
    var raided = CatchUp.Build(Quiet(since, now) with
    {
        AttacksAgainstYou = 3,
        AttacksHeld = 1,
        CashStolen = 12_400,
        ThugsLost = 4
    });
    AssertTrue(raided.HasNews, "being attacked is news");
    var attack = raided.Items.Single(x => x.Kind == "attacks");
    AssertEqual("bad", attack.Tone);
    AssertTrue(attack.Headline.Contains("attacked 3 times"), $"headline should count the attacks: {attack.Headline}");
    AssertTrue(attack.Detail.Contains("held 1"), $"detail should credit the one that held: {attack.Detail}");
    AssertTrue(attack.Detail.Contains("$12,400") && attack.Detail.Contains("4 thug"), $"detail should list the losses: {attack.Detail}");

    // Attacked but nothing lost reads as a win, not a warning.
    var repelled = CatchUp.Build(Quiet(since, now) with { AttacksAgainstYou = 2, AttacksHeld = 2 });
    AssertEqual("good", repelled.Items.Single(x => x.Kind == "attacks").Tone);
    AssertTrue(repelled.Items[0].Headline.Contains("held off 2"), $"headline should say they held: {repelled.Items[0].Headline}");

    // Labs, a finished build, and a capped turn meter each earn their own line.
    var busy = CatchUp.Build(Quiet(since, now) with
    {
        LabWeed = 84,
        LabCoke = 24,
        HideoutBuilds = ["The Warehouse is finished."],
        TurnsNow = 200
    });
    AssertEqual(3, busy.Items.Count);
    AssertTrue(busy.Items.Single(x => x.Kind == "labs").Detail.Contains("84 weed and 24 coke"), "labs should report both products");
    AssertEqual("The Warehouse is finished.", busy.Items.Single(x => x.Kind == "hideout").Detail);
    AssertTrue(busy.Items.Any(x => x.Kind == "turns"), "a capped turn meter is worth saying");

    // Below the cap the turn meter is not worth a line, since nothing is being wasted.
    AssertTrue(!CatchUp.Build(Quiet(since, now) with { TurnsNow = 199 }).Items.Any(x => x.Kind == "turns"),
        "turns below the cap are not news");

    // Ground taken off you leads over ground you took, since it is the thing to react to.
    var ground = CatchUp.Build(Quiet(since, now) with
    {
        GroundLostNames = ["Red Hook Docks"],
        GroundTakenNames = ["Hunts Point"]
    });
    var lost = ground.Items.Single(x => x.Kind == "ground" && x.Tone == "bad");
    AssertTrue(lost.Headline.Contains("You lost Red Hook Docks"), $"a single name reads better than a count: {lost.Headline}");
    AssertTrue(ground.Items.Any(x => x.Kind == "ground" && x.Tone == "good"), "ground you took is worth a line too");
    var groundKinds = ground.Items.Where(x => x.Kind == "ground").Select(x => x.Tone).ToList();
    AssertEqual("bad", groundKinds[0]);
    AssertEqual("good", groundKinds[1]);

    // A raid you beat off still cost the garrison, which is why it is worth a line: a garrison that
    // shrank with no explanation reads as a bug rather than a fight.
    var held = CatchUp.Build(Quiet(since, now) with { GroundHeldNames = ["Hunts Point"], GarrisonThugsLost = 3 });
    var line = held.Items.Single(x => x.Kind == "ground");
    AssertEqual("good", line.Tone);
    AssertTrue(line.Detail.Contains("3 thug"), $"the cost of holding is the point: {line.Detail}");
    AssertTrue(CatchUp.Build(Quiet(since, now) with { GroundHeldNames = ["Hunts Point"] })
        .Items.Single(x => x.Kind == "ground").Detail.Contains("without a scratch"), "a clean hold reads differently");

    // Protection still running is worth knowing; protection that has lapsed is not.
    AssertTrue(CatchUp.Build(Quiet(since, now) with { ProtectedUntilUtc = now.AddMinutes(41) }).Items.Any(x => x.Kind == "protection"),
        "live protection is worth saying");
    AssertTrue(!CatchUp.Build(Quiet(since, now) with { ProtectedUntilUtc = now.AddMinutes(-1) }).Items.Any(x => x.Kind == "protection"),
        "lapsed protection is not");
}

static void CatchUpReportsRankAndRivals()
{
    var since = new DateTime(2026, 8, 13, 6, 0, 0, DateTimeKind.Utc);
    var now = since.AddHours(9);
    static CatchUpFacts Quiet(DateTime since, DateTime now)
        => new(since, now, 0, 0, 0, 0, 0, 0, 0, 0, 0, [], 40, 200, null);

    // No standings sample covering the absence means no claim at all, which is not the same as
    // claiming nothing changed.
    var blind = CatchUp.Build(Quiet(since, now));
    AssertTrue(!blind.Items.Any(x => x.Kind == "rank"), "no baseline means no rank line");
    AssertTrue(!blind.Items.Any(x => x.Kind == "rivals"), "no baseline means no rivals line");

    // Rank rises as the number falls, so the wording has to be careful about which way is good.
    var climbed = CatchUp.Build(Quiet(since, now) with { RankBefore = 7, RankNow = 4 });
    var up = climbed.Items.Single(x => x.Kind == "rank");
    AssertEqual("good", up.Tone);
    AssertTrue(up.Headline.Contains("climbed to #4"), $"headline should name the new rank: {up.Headline}");
    AssertTrue(up.Detail.Contains("#7"), $"detail should name the old rank: {up.Detail}");

    var slipped = CatchUp.Build(Quiet(since, now) with { RankBefore = 3, RankNow = 5 });
    AssertEqual("bad", slipped.Items.Single(x => x.Kind == "rank").Tone);
    AssertTrue(slipped.Items.Single(x => x.Kind == "rank").Headline.Contains("slipped to #5"), "a drop should read as a drop");

    // Holding the same rank is not news.
    AssertTrue(!CatchUp.Build(Quiet(since, now) with { RankBefore = 4, RankNow = 4 }).Items.Any(x => x.Kind == "rank"),
        "an unchanged rank is not worth a line");

    // Being overtaken leads, because it is the thing to react to.
    var overtaken = CatchUp.Build(Quiet(since, now) with
    {
        RankBefore = 3,
        RankNow = 5,
        OvertookYouNames = ["Lucky Voss", "Grit Baron"],
        YouOvertookNames = ["Silk Ledger"]
    });
    var rivals = overtaken.Items.Single(x => x.Kind == "rivals");
    AssertEqual("bad", rivals.Tone);
    AssertTrue(rivals.Headline.Contains("2 rivals moved ahead"), $"headline should count them: {rivals.Headline}");
    AssertTrue(rivals.Detail.Contains("Lucky Voss and Grit Baron"), $"detail should name them: {rivals.Detail}");
    AssertTrue(rivals.Detail.Contains("Silk Ledger"), $"detail should still credit the one passed: {rivals.Detail}");

    // Passing people with nobody passing you reads as a win.
    var gained = CatchUp.Build(Quiet(since, now) with { RankBefore = 6, RankNow = 4, YouOvertookNames = ["Silk Ledger"] });
    var won = gained.Items.Single(x => x.Kind == "rivals");
    AssertEqual("good", won.Tone);
    AssertTrue(won.Headline.Contains("You passed Silk Ledger"), $"a single name reads better than a count: {won.Headline}");

    // A long list turns into a count rather than a wall of names.
    var swarm = CatchUp.Build(Quiet(since, now) with
    {
        RankBefore = 2,
        RankNow = 9,
        OvertookYouNames = ["A", "B", "C", "D", "E", "F"]
    });
    AssertTrue(swarm.Items.Single(x => x.Kind == "rivals").Detail.Contains("and 3 others"), "six names should collapse");
}

static void DefenceAlertsCountUnread()
{
    var attacker = new Player { Name = "Brass Knox" };
    var seen = new DateTime(2026, 8, 12, 12, 0, 0, DateTimeKind.Utc);
    CombatLog At(long id, DateTime when) => new() { Id = id, Attacker = attacker, Outcome = "Victory", CreatedAtUtc = when };

    var older = DefenceAlerts.Describe(At(1, seen.AddMinutes(-5)), seen);
    var exactlyAtWatermark = DefenceAlerts.Describe(At(2, seen), seen);
    var newer = DefenceAlerts.Describe(At(3, seen.AddMinutes(5)), seen);

    AssertTrue(!older.IsUnread, "anything before the watermark is read");
    AssertTrue(!exactlyAtWatermark.IsUnread, "the watermark moment itself is read");
    AssertTrue(newer.IsUnread, "anything after the watermark is unread");
    AssertEqual(1, DefenceAlerts.UnreadCount(new[] { older, exactlyAtWatermark, newer }.Select(DefenceAlerts.ToAlert)));

    // A player who has never opened their alerts sees all of them as unread.
    var neverLooked = new[] { At(1, seen.AddDays(-9)), At(2, seen) }
        .Select(x => DefenceAlerts.ToAlert(DefenceAlerts.Describe(x, null)))
        .ToList();
    AssertEqual(2, DefenceAlerts.UnreadCount(neverLooked));
    AssertEqual(0, DefenceAlerts.UnreadCount([]));

    // Non-combat notices share the bell and the same watermark. Only the kinds that are genuinely
    // things done to the player belong there: starting a build is an action, it finishing is not.
    AssertEqual("labs", DefenceAlerts.ToAlert(9, "LAB", "Your labs made 40 weed.", seen.AddMinutes(5), seen)!.Kind);
    AssertTrue(DefenceAlerts.ToAlert(9, "LAB", "x", seen.AddMinutes(5), seen)!.IsUnread, "a notice after the watermark is unread");
    AssertTrue(!DefenceAlerts.ToAlert(9, "LAB", "x", seen.AddMinutes(-5), seen)!.IsUnread, "a notice before it is read");
    AssertEqual("hideout", DefenceAlerts.ToAlert(9, "HIDEOUT", "The Warehouse is finished.", seen, seen)!.Kind);
    AssertTrue(DefenceAlerts.ToAlert(9, "HIDEOUT", "Started building the Warehouse for $200,000.", seen, seen) is null,
        "starting a build is an action, not a notification");
    AssertTrue(DefenceAlerts.ToAlert(9, "STREET", "Worked the streets.", seen, seen) is null, "ordinary actions are not alerts");

    // The filter the activity list uses has to agree with the one the bell uses, or a row lands in
    // both places or neither.
    AssertTrue(DefenceAlerts.IsNotification("LAB", "anything"), "lab output is a notification");
    AssertTrue(DefenceAlerts.IsNotification("HIDEOUT", "The Warehouse is finished."), "a finished build is");
    AssertTrue(!DefenceAlerts.IsNotification("HIDEOUT", "Upgraded the safe to level 3."), "a room upgrade is not");
    AssertTrue(!DefenceAlerts.IsNotification("STREET", "Worked the streets."), "street work is not");
    AssertTrue(DefenceAlerts.IsNotification("GROUND", "Brass Knox took Hunts Point from you."), "losing ground is");
    AssertTrue(DefenceAlerts.IsNotification("GROUND", "Hunts Point held against Brass Knox."), "a raid you held off is");
    AssertTrue(!DefenceAlerts.IsNotification("TERRITORY", "Took over Hunts Point with 6 thug(s)."), "claiming ground is an action");

    // The activity list uses the negation of this rule, derived from it rather than written out again.
    var rows = new[]
    {
        new GameActionLog { Action = "LAB", Summary = "made weed" },
        new GameActionLog { Action = "GROUND", Summary = "X took Y from you." },
        new GameActionLog { Action = "TERRITORY", Summary = "Took over Y." },
        new GameActionLog { Action = "STREET", Summary = "worked" }
    };
    var isNotification = DefenceAlerts.IsNotificationRow.Compile();
    var isAction = DefenceAlerts.IsActionRow.Compile();
    foreach (var row in rows)
        AssertTrue(isNotification(row) != isAction(row), $"every row is one or the other, never both: {row.Action}");
    AssertEqual(2, rows.Count(isNotification));
}

// The balance target, stated as a test so retuning cannot quietly break it: a defender holds at equal
// armed crew, and an attacker needs a modest edge rather than an overwhelming one.
static void CombatPowerBalanceTarget()
{
    var power = new CombatPowerOptions();
    const double morale = 100;

    foreach (var (thugs, pimps) in new[] { (5, 2), (10, 3), (20, 5), (25, 6) })
    {
        var attackAtParity = CombatPower.Attack(1, thugs, Firepower.Sidearms(thugs, thugs), morale, power);
        var defence = CombatPower.Defence(pimps, thugs, Firepower.Sidearms(thugs, thugs), morale, power);
        AssertTrue(attackAtParity < defence,
            $"at {thugs} armed each, the defender should hold ({attackAtParity} vs {defence})");

        // The edge required stays modest across the whole scale, so attacking is viable.
        var needed = CombatPower.ThugsNeededToMatch(thugs, pimps, morale, power);
        var edge = (needed - thugs) / (double)thugs;
        AssertTrue(edge > 0, $"matching {thugs} defenders needs more than {thugs} attackers");
        AssertTrue(edge <= 0.30,
            $"matching {thugs} defenders should need no more than 30% extra crew, needed {needed} ({edge:P0})");
    }

    // Weapons matter to both sides, and unarmed crew is worth strictly less.
    AssertTrue(CombatPower.Attack(1, 10, Firepower.Sidearms(10, 10), morale, power) > CombatPower.Attack(1, 10, Firepower.Sidearms(10, 0), morale, power),
        "arming the raid helps");
    AssertTrue(CombatPower.Defence(3, 10, Firepower.Sidearms(10, 10), morale, power) > CombatPower.Defence(3, 10, Firepower.Sidearms(10, 0), morale, power),
        "arming the house helps");

    // Morale counts for both, and more for the defender.
    AssertTrue(CombatPower.Attack(1, 10, Firepower.Sidearms(10, 10), 100, power) > CombatPower.Attack(1, 10, Firepower.Sidearms(10, 10), 0, power),
        "morale lifts an attack");
    AssertTrue(
        CombatPower.Defence(3, 10, Firepower.Sidearms(10, 10), 100, power) - CombatPower.Defence(3, 10, Firepower.Sidearms(10, 10), 0, power)
        > CombatPower.Attack(1, 10, Firepower.Sidearms(10, 10), 100, power) - CombatPower.Attack(1, 10, Firepower.Sidearms(10, 10), 0, power),
        "morale is worth more at home than on the road");

    // The commander bonus scales the whole figure and never drops it below one.
    AssertEqual(CombatPower.Attack(1, 10, Firepower.Sidearms(10, 10), morale, power) * 2,
        CombatPower.Attack(1, 10, Firepower.Sidearms(10, 10), morale, power, bonusPercent: 100));
    AssertTrue(CombatPower.Attack(0, 0, Firepower.Sidearms(0, 0), 0, power) >= 1, "power never falls below one");

    // The ceiling matchup. Under the previous weights a maxed defender needed 34 attacking thugs to
    // crack while the crew cap was 25, so a fully built house was literally unbeatable. Now brute force
    // alone still falls short, and the counterplay is a top Enforcer commanding or catching the crew away.
    var tier = new GameOptions().Hideout;
    tier.ApplyDefaultsWhereEmpty();
    var maxThugs = tier.Tiers[0].MaxThugs;
    var maxPimps = tier.Tiers[0].MaxPimps;
    var bestBonus = new PimpOptions().MaxBonusPercent;

    var fortress = CombatPower.Defence(maxPimps, maxThugs, Firepower.Sidearms(maxThugs, maxThugs), morale, power);
    var maxedRaid = CombatPower.Attack(1, maxThugs, Firepower.Sidearms(maxThugs, maxThugs), morale, power);
    AssertTrue(maxedRaid < fortress, "a full raid alone should not crack a fully built house");
    AssertTrue(CombatPower.Attack(1, maxThugs, Firepower.Sidearms(maxThugs, maxThugs), morale, power, bestBonus) >= fortress,
        "a top Enforcer commander should bring a full raid level with a fully built house");

    // And a house with crew out attacking is beatable without any commander bonus at all.
    var stretched = CombatPower.Defence(maxPimps, maxThugs - 5, Firepower.Sidearms(maxThugs - 5, maxThugs - 5), morale, power);
    AssertTrue(maxedRaid > stretched, "a house with its crew away is exposed");
}

static void CombatBlocksSelfAttacks()
{
    var service = CreateCombat();
    var player = new Player { Id = Guid.NewGuid(), Turns = 20, Pimps = 1 };

    try
    {
        service.Attack(player, player, new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc));
    }
    catch (GameRuleException)
    {
        return;
    }

    throw new InvalidOperationException("Expected self-attack failure.");
}

static void CombatBlocksProtectedDefenders()
{
    var service = CreateCombat(new GameOptions
    {
        Combat = new CombatOptions { AttackTurnCost = 10, AttackCooldownMinutes = 30 }
    });
    var now = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc);
    var attacker = new Player { Id = Guid.NewGuid(), Turns = 20, Pimps = 1, Thugs = 5 };
    var defender = new Player
    {
        Id = Guid.NewGuid(),
        Name = "Protected Target",
        Cash = 1_000,
        Pimps = 1,
        CombatProtectionUntilUtc = now.AddMinutes(5)
    };

    try
    {
        service.Attack(attacker, defender, now);
    }
    catch (GameRuleException)
    {
        return;
    }

    throw new InvalidOperationException("Expected protected-defender failure.");
}

static void CombatStartCreatesPendingMission()
{
    var service = CreateCombat(new GameOptions
    {
        Combat = new CombatOptions
        {
            AttackTurnCost = 6,
            AttackTravelSecondsMin = 90,
            AttackTravelSecondsMax = 180,
            DefenderProtectionMinutes = 30
        }
    });
    var now = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc);
    var attacker = new Player { Id = Guid.NewGuid(), Name = "Attacker", Turns = 20, Pimps = 1, Thugs = 3 };
    var defender = new Player { Id = Guid.NewGuid(), Name = "Defender", Cash = 1_000, Pimps = 1, Thugs = 1 };

    var resolution = service.StartAttack(attacker, defender, now);

    AssertEqual("Pending", resolution.Outcome);
    AssertEqual(14, attacker.Turns);
    AssertEqual(now, attacker.LastAttackAtUtc);
    AssertEqual(now.AddSeconds(90), resolution.Log.ResolvesAtUtc);
    AssertEqual(now.AddSeconds(90).AddMinutes(30), defender.CombatProtectionUntilUtc);
    AssertEqual(1_000L, defender.Cash);
    AssertEqual(0L, attacker.Cash);
}

static void CombatCommitmentCalculatesAvailableCrew()
{
    var player = new Player { Pimps = 3, Thugs = 20, Pistols = 15 };
    var active = new[]
    {
        new CombatMission { AssignedPimps = 1, RemainingAttackers = 8, RemainingWeapons = 6 },
        new CombatMission { AssignedPimps = 1, RemainingAttackers = 5, RemainingWeapons = 4 }
    };

    var commitment = CombatCommitment.From(player, active, 2);

    AssertEqual(2, commitment.CommittedPimps);
    AssertEqual(13, commitment.CommittedThugs);
    AssertEqual(10, commitment.CommittedWeapons);
    AssertEqual(1, commitment.AvailablePimps);
    AssertEqual(7, commitment.AvailableThugs);
    AssertEqual(5, commitment.AvailableWeapons);
    AssertEqual(2, commitment.ActiveAttackMissions);
}

// The gate may fire early and waste a pass, but must never fire late: a late gate stalls combat.
static void CombatScheduleGateNeverRunsLate()
{
    var now = new DateTime(2026, 8, 12, 0, 0, 0, DateTimeKind.Utc);
    var schedule = new CombatSchedule();

    AssertTrue(schedule.MayBeDue(now), "a cold schedule must take the slow path");

    schedule.SetNextDue(now.AddSeconds(30));
    AssertTrue(!schedule.MayBeDue(now), "nothing is due 30 seconds early");
    AssertTrue(schedule.MayBeDue(now.AddSeconds(30)), "the exact due moment counts as due");
    AssertTrue(schedule.MayBeDue(now.AddMinutes(5)), "past due still counts as due");

    // A launch landing sooner than the cached time must bring the gate forward.
    schedule.SetNextDue(now.AddMinutes(10));
    schedule.NoteUpcoming(now.AddSeconds(5));
    AssertTrue(schedule.MayBeDue(now.AddSeconds(5)), "an earlier arrival must open the gate");

    // A later event must not push the gate out past work already waiting.
    schedule.SetNextDue(now.AddSeconds(5));
    schedule.NoteUpcoming(now.AddHours(1));
    AssertTrue(schedule.MayBeDue(now.AddSeconds(5)), "a later event must not delay the gate");

    schedule.SetNextDue(null);
    AssertTrue(!schedule.MayBeDue(now.AddYears(50)), "an empty schedule stays shut");
    schedule.Invalidate();
    AssertTrue(schedule.MayBeDue(now), "invalidating forces the slow path");
}

static void CombatMissionCancelPriceScalesByStatus()
{
    var mission = new CombatMission
    {
        Status = "Traveling",
        AssignedPimps = 1,
        RemainingAttackers = 4,
        RemainingWeapons = 2
    };

    AssertEqual(1_250L, CombatMissionService.CancelCashCost(mission));
    AssertTrue(CombatMissionService.CanCancel(mission), "traveling missions should be cancelable");

    mission.Status = "Fighting";
    AssertEqual(2_500L, CombatMissionService.CancelCashCost(mission));
    AssertTrue(CombatMissionService.CanCancel(mission), "fighting missions should be cancelable");

    mission.Status = "Returning";
    AssertTrue(!CombatMissionService.CanCancel(mission), "returning missions should not be cancelable");
}

static void CombatMissionLaunchRespectsAttackerCooldown()
{
    var now = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc);

    AssertTrue(
        CombatMissionService.LaneReadyAtUtc([], 2, 30) is null,
        "an attacker who has never launched should have both lanes free");
    AssertTrue(
        CombatMissionService.LaneReadyAtUtc([now], 2, 30) is null,
        "one launch should leave the second lane free");

    // Both lanes used: the older launch frees its lane first.
    AssertEqual(
        now.AddMinutes(30),
        CombatMissionService.LaneReadyAtUtc([now.AddMinutes(12), now], 2, 30));

    // A lowered lane count still frees the lane that expires soonest.
    AssertEqual(
        now.AddMinutes(42),
        CombatMissionService.LaneReadyAtUtc([now.AddMinutes(20), now.AddMinutes(12), now], 2, 30));

    AssertTrue(
        CombatMissionService.LaneReadyAtUtc([now, now], 2, 0) is null,
        "a zero-minute cooldown should never hold a lane");
    AssertTrue(
        CombatMissionService.LaneReadyAtUtc([now], 1, 30) == now.AddMinutes(30),
        "a single-lane config should cool down on every launch");
}

static void CombatVictoryStealsCashAndProductWithoutTouchingBank()
{
    var service = CreateCombat(new GameOptions
    {
        Combat = new CombatOptions
        {
            AttackTurnCost = 10,
            AttackCooldownMinutes = 30,
            DefenderProtectionMinutes = 60,
            PowerRandomnessPercent = 0,
            MinCashLootPercent = 0.10,
            MaxCashLootPercent = 0.10,
            MinProductLootPercent = 0.20,
            MaxProductLootPercent = 0.20,
            WinnerCrewLossPercent = 0,
            LoserCrewLossPercent = 0,
            WeaponLossPercent = 0
        }
    });
    var now = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc);
    var attacker = new Player
    {
        Id = Guid.NewGuid(),
        Name = "Attacker",
        Turns = 30,
        Pimps = 3,
        Thugs = 20,
        Pistols = 20,
        HoeHappiness = 100,
        ThugHappiness = 100
    };
    var defender = new Player
    {
        Id = Guid.NewGuid(),
        Name = "Defender",
        Cash = 10_000,
        BankCash = 50_000,
        Pimps = 1,
        Thugs = 1,
        Pistols = 1,
        Weed = 100,
        Coke = 50,
        HoeHappiness = 50,
        ThugHappiness = 50
    };

    var resolution = service.Attack(attacker, defender, now);
    var breakdown = RequiredBreakdown(resolution.Result);

    AssertEqual("Victory", resolution.Outcome);
    AssertEqual(20, attacker.Turns);
    AssertEqual(1_000L, Value<long>(breakdown, "cashStolen"));
    AssertEqual(20, Value<int>(breakdown, "weedStolen"));
    AssertEqual(10, Value<int>(breakdown, "cokeStolen"));
    AssertEqual(1_000L, attacker.Cash);
    AssertEqual(9_000L, defender.Cash);
    AssertEqual(50_000L, defender.BankCash);
    AssertEqual(20, attacker.Weed);
    AssertEqual(80, defender.Weed);
    AssertEqual(10, attacker.Coke);
    AssertEqual(40, defender.Coke);
}

static void CombatAttackSpendsTurnsAndCreatesLog()
{
    var service = CreateCombat(new GameOptions
    {
        Combat = new CombatOptions
        {
            AttackTurnCost = 7,
            AttackCooldownMinutes = 30,
            DefenderProtectionMinutes = 45,
            PowerRandomnessPercent = 0,
            MinCashLootPercent = 0,
            MaxCashLootPercent = 0,
            MinProductLootPercent = 0,
            MaxProductLootPercent = 0,
            WinnerCrewLossPercent = 0,
            LoserCrewLossPercent = 0,
            WeaponLossPercent = 0
        }
    });
    var now = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc);
    var attacker = new Player { Id = Guid.NewGuid(), Name = "Attacker", Turns = 10, Pimps = 1, Thugs = 5 };
    var defender = new Player { Id = Guid.NewGuid(), Name = "Defender", Cash = 100, Pimps = 1, Thugs = 1 };

    var resolution = service.Attack(attacker, defender, now);

    AssertEqual(3, attacker.Turns);
    AssertEqual(now, attacker.LastAttackAtUtc);
    AssertEqual(now, defender.LastAttackedAtUtc);
    AssertEqual(now.AddMinutes(45), defender.CombatProtectionUntilUtc);
    AssertEqual(7, resolution.Log.TurnsSpent);
    AssertEqual(attacker.Id, resolution.Log.AttackerId);
    AssertEqual(defender.Id, resolution.Log.DefenderId);
    AssertTrue(resolution.Log.Summary.Contains("Defender"), "combat summary should name the defender");
}

// The menu is built on the server so the client never has to know a rule. What matters here is that a
// method a player cannot use arrives already carrying the reason, rather than being silently absent or
// silently clickable.
static void AttackMenuPricesEveryMethod()
{
    var options = Resolve(new GameOptions());
    var strikes = CreateStrikes(options);

    var broke = Rookie(options);
    var menu = strikes.MethodsFor(broke);
    AssertEqual(5, menu.Count);
    AssertTrue(menu.All(x => x.TurnCost > 0), "every method costs turns");

    // A raid is the heavyweight and should cost more than any of the cheap shots.
    var raid = menu.Single(x => x.Key == AttackMethods.Raid);
    AssertTrue(menu.Where(x => x.Key != AttackMethods.Raid).All(x => x.TurnCost < raid.TurnCost),
        "a strike is cheaper than an operation");

    // With no car and no product, two of the four are shut, and each says so itself.
    AssertTrue(menu.Single(x => x.Key == AttackMethods.DriveBy).BlockedReason is not null, "no ride, no drive-by");
    AssertTrue(menu.Single(x => x.Key == AttackMethods.Poach).BlockedReason is not null, "no coke, no poaching");
    AssertTrue(menu.Single(x => x.Key == AttackMethods.Infest).BlockedReason is null, "infesting needs nothing of yours");

    // Buy the car and the reason goes away.
    broke.Rides = 1;
    AssertTrue(strikes.MethodsFor(broke).Single(x => x.Key == AttackMethods.DriveBy).BlockedReason is null,
        "a ride opens the drive-by");

    // An unknown or missing method is a raid, which is what every caller written before the menu
    // existed was asking for.
    AssertEqual(AttackMethods.Raid, AttackMethods.Normalize(null));
    AssertEqual(AttackMethods.Raid, AttackMethods.Normalize("nonsense"));
    AssertTrue(!AttackMethods.IsStrike(AttackMethods.Raid), "a raid is not a strike");
    AssertTrue(AttackMethods.Strikes.All(AttackMethods.IsStrike), "and all four strikes are");
}

// A drive-by takes nothing, which is the whole reason it is cheap: it is how a player who cannot yet win
// a raid makes one winnable. What it must never be is free.
static void DriveByThinsTheGuard()
{
    var options = Resolve(new GameOptions());
    var now = new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc);

    // AlwaysRandom rolls zero, which lands every chance check and takes the minimum of every range.
    var strikes = CreateStrikes(options, new AlwaysRandom());
    var attacker = Attacker(options, rides: 1);
    var defender = Defender(options);
    var thugsBefore = defender.Thugs;
    var moraleBefore = defender.ThugHappiness;

    var result = strikes.Resolve(attacker, defender, Strike(defender, AttackMethods.DriveBy), StrikeDefence.Everyone(defender), now);

    AssertEqual("Victory", result.Outcome);
    AssertTrue(defender.Thugs < thugsBefore, "thugs go down");
    AssertTrue(defender.ThugHappiness < moraleBefore, "and the survivors like it less");
    AssertEqual(0L, result.Log.CashStolen);
    AssertEqual(0, result.Log.DefenderHoesLost);
    AssertEqual(options.Strikes.DriveBy.TurnCost, result.Log.TurnsSpent);
    AssertTrue(attacker.Heat > 0, "shooting up a street gets you noticed");
    // Zero rolls also land the return-fire check, so the car is gone.
    AssertEqual(0, attacker.Rides);

    // MinimumRandom rolls one, which fails every chance check: the pass finds nobody and the car comes
    // home. A miss is a real outcome, or a full turn bank would grind any rival to nothing for free.
    var unlucky = CreateStrikes(options, new MinimumRandom());
    var misser = Attacker(options, rides: 1);
    var untouched = Defender(options);
    var missed = unlucky.Resolve(misser, untouched, Strike(untouched, AttackMethods.DriveBy), StrikeDefence.Everyone(untouched), now);
    AssertEqual("Defeat", missed.Outcome);
    AssertEqual(Defender(options).Thugs, untouched.Thugs);
    AssertEqual(1, misser.Rides);

    // The better armed the street, the worse the odds of getting away clean.
    var config = options.Strikes.DriveBy;
    var quiet = Math.Clamp(config.RideLossChance, 0, config.MaxRideLossChance);
    var busy = Math.Clamp(config.RideLossChance + 20 * config.RideLossChancePerArmedThug, 0, config.MaxRideLossChance);
    AssertTrue(busy > quiet, "a defended street shoots back harder");
}

// The strike whose odds are almost entirely the defender's own doing. A garage behind a full armed crew
// should be close to untouchable, and one behind nobody should be a car park with the keys in.
static void JackingIsDecidedByTheGuard()
{
    var options = Resolve(new GameOptions());
    var now = new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc);
    var strikes = CreateStrikes(options, new AlwaysRandom());

    var attacker = Attacker(options);
    var defender = Defender(options);
    defender.Rides = 3;

    // Nobody home: the cars leave, bounded by the attacker's own garage rather than by their nerve.
    var open = strikes.Resolve(attacker, defender, Strike(defender, AttackMethods.Jack), Nobody(), now);
    AssertEqual("Victory", open.Outcome);
    AssertTrue(open.Log.RidesTaken > 0, "an unguarded garage is emptied");
    AssertEqual(open.Log.RidesTaken, attacker.Rides);
    AssertEqual(3 - open.Log.RidesTaken, defender.Rides);

    // A full armed crew standing in it turns the odds the other way. MinimumRandom rolls one, which
    // fails against any chance below certainty.
    var guarded = CreateStrikes(options, new MinimumRandom());
    var thief = Attacker(options);
    var held = Defender(options);
    held.Rides = 3;
    var caught = guarded.Resolve(thief, held, Strike(held, AttackMethods.Jack), Guard(40, WeaponTiers.Pistol), now);
    AssertEqual("Defeat", caught.Outcome);
    AssertEqual(3, held.Rides);
    AssertEqual(0, thief.Rides);

    // A ride with nowhere to park is a ride left behind, which is the same rule the chop shop refuses a
    // purchase under.
    var full = Attacker(options);
    full.Rides = CreateHideouts(options).CapacityFor(full.Hideout).MaxRides;
    AssertRuleError(
        () => strikes.Resolve(full, Defender(options, rides: 2), Strike(Defender(options), AttackMethods.Jack), Nobody(), now),
        "jacking with a full garage");
}

// Bodies are eyes on the door; guns are what happens once you are seen. Both have to count, or a garage
// held by riflemen is exactly as easy to rob as the same garage held by the same number of pistols.
static void JackingReadsTheGuardsGunsAsWellAsTheirNumber()
{
    var options = Resolve(new GameOptions());
    var now = new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc);

    // FixedRandom rolls exactly what it is told, so a roll lands only against a chance above it. Six
    // guards with pistols leave the odds well above this; the same six with rifles do not.
    const double roll = 0.5;
    var attacker = Attacker(options);
    var withPistols = Defender(options, rides: 2);
    var withRifles = Defender(options, rides: 2);

    var soft = CreateStrikes(options, new FixedRandom(roll))
        .Resolve(attacker, withPistols, Strike(withPistols, AttackMethods.Jack), Guard(6, WeaponTiers.Pistol), now);
    AssertEqual("Victory", soft.Outcome);

    var hard = CreateStrikes(options, new FixedRandom(roll))
        .Resolve(Attacker(options), withRifles, Strike(withRifles, AttackMethods.Jack), Guard(6, WeaponTiers.Rifle), now);
    AssertEqual("Defeat", hard.Outcome);
    AssertEqual(2, withRifles.Rides);

    // The odds themselves, reported so a player can see which half of the garage beat them.
    var softOdds = Value<int>(RequiredBreakdown(soft.Result), "successChancePercent");
    var hardOdds = Value<int>(RequiredBreakdown(hard.Result), "successChancePercent");
    AssertTrue(hardOdds < softOdds, $"rifles guard better than pistols ({hardOdds}% against {softOdds}%)");
    AssertEqual(6, Value<int>(RequiredBreakdown(hard.Result), "guardArmedThugs"));
    AssertEqual(15.0, Value<double>(RequiredBreakdown(hard.Result), "guardFirepower"));

    // Only the firepower over one pistol each counts, so the two terms never describe the same thug
    // twice - and a pistol guard is worth exactly what it was before guns had tiers at all.
    AssertEqual(0.0, Value<double>(RequiredBreakdown(soft.Result), "guardFirepowerOverSidearms"));
    AssertEqual(9.0, Value<double>(RequiredBreakdown(hard.Result), "guardFirepowerOverSidearms"));

    var jack = options.Strikes.Jack;
    AssertEqual(
        (int)Math.Round((jack.BaseChance - 6 * jack.ChancePerArmedThug) * 100),
        softOdds);

    // Guns nobody is holding guard nothing. A rack of forty rifles behind two thugs is two riflemen.
    var thin = new StrikeDefence(2, new Armoury(0, 0, 0, 40));
    AssertEqual(2, thin.ArmedThugs);
    AssertEqual(5.0, thin.Guns(options.WeaponFirepower()).InPistols);
    AssertEqual(3.0, thin.FirepowerOverSidearms(options.WeaponFirepower()));

    // And a body with no gun at all guards by standing there, not by shooting.
    var unarmed = new StrikeDefence(6, Armoury.Empty);
    AssertEqual(0, unarmed.ArmedThugs);
    AssertEqual(0.0, unarmed.FirepowerOverSidearms(options.WeaponFirepower()));
}

/// <summary>An empty garage, for the strikes that want nobody standing in the way.</summary>
static StrikeDefence Nobody() => new(0, Armoury.Empty);

/// <summary>A guard of this many bodies, each carrying one gun of the given kind.</summary>
static StrikeDefence Guard(int thugs, string tier)
    => new(thugs, Armoury.Empty.With(tier, thugs));

// The only attack in the game answered by a purchase rather than by crew or morale. Medicine sits on a
// shelf doing nothing, costing money, until the day it is the only thing between a rival and the house.
// Infesting was the only strike that took nothing to throw. A drive-by risks the car, a jacking needs
// a thug and somewhere to park what it takes, a poach spends coke a head - and poisoning somebody's
// house was free, which made it the obvious opening move against anybody at any time.
//
// Poison is the defender's own problem handed back in reverse. A crate of medicine treats three hoes;
// a dose of poison reaches three. Covering a big house costs real money at either end.
static void PoisonIsWhatAnInfestationCosts()
{
    var options = Resolve(new GameOptions());
    var now = new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc);
    var strikes = CreateStrikes(options, new AlwaysRandom());
    var perDose = options.Strikes.Infest.HoesHitPerDose;

    // Turning up with one dose reaches what one dose reaches, whatever the roll wanted.
    var stingy = Attacker(options);
    stingy.Poison = 1;
    var big = Defender(options);
    big.Hoes = 60;
    big.Medicine = 0;

    var thin = strikes.Resolve(stingy, big, Strike(big, AttackMethods.Infest), StrikeDefence.Everyone(big), now);
    AssertTrue(thin.Log.DefenderHoesLost <= perDose, $"one dose cannot reach more than {perDose}, hit {thin.Log.DefenderHoesLost}");
    AssertEqual(0, stingy.Poison);

    // Carrying enough, the roll is what limits it rather than the shelf.
    var stocked = Attacker(options);
    stocked.Poison = 40;
    var house = Defender(options);
    house.Hoes = 60;
    house.Medicine = 0;

    var full = strikes.Resolve(stocked, house, Strike(house, AttackMethods.Infest), StrikeDefence.Everyone(house), now);
    AssertTrue(full.Log.DefenderHoesLost > thin.Log.DefenderHoesLost, "more poison reaches further");
    AssertTrue(stocked.Poison < 40, "and it is spent doing it");

    // A part-used dose is a used dose, exactly as a part-used crate of medicine is. Anything else lets
    // one dose cover a house forever by never quite finishing.
    var used = 40 - stocked.Poison;
    AssertEqual((int)Math.Ceiling(full.Log.DefenderHoesLost / (double)perDose), used);

    // The shelf holds it like anything else, and it is worth what it cost on the books.
    var hideouts = CreateHideouts(options);
    var capacity = hideouts.CapacityFor(new Hideout { StorageLevel = 1 });
    AssertTrue(capacity.MaxPoison > 0, "a starting room holds some");
    AssertEqual(capacity.MaxMedicine, capacity.MaxPoison);

    var holder = new Player { Poison = 4 };
    AssertEqual(4L * options.PoisonNetWorth, EconomyService.NetWorthOf(holder, options));
}

static void MedicineAnswersAnInfestation()
{
    var options = Resolve(new GameOptions());
    var now = new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc);
    var strikes = CreateStrikes(options, new AlwaysRandom());
    var perCrate = options.Strikes.Infest.HoesCuredPerCrate;

    // Poisoning a house costs poison, so an attacker in this test has to be carrying enough to reach
    // the whole of a forty-hoe house. Stocked deliberately rather than in the shared helper, so the
    // other tests keep measuring an attacker who is carrying nothing unusual.
    Player Poisoner() { var who = Attacker(options); who.Poison = 40; return who; }

    // No medicine: whoever is exposed is gone.
    var attacker = Poisoner();
    var bare = Defender(options);
    bare.Hoes = 40;
    bare.Medicine = 0;
    var landed = strikes.Resolve(attacker, bare, Strike(bare, AttackMethods.Infest), StrikeDefence.Everyone(bare), now);
    AssertEqual("Victory", landed.Outcome);
    AssertTrue(landed.Log.DefenderHoesLost > 0, "hoes are lost");
    AssertEqual(40 - landed.Log.DefenderHoesLost, bare.Hoes);
    // Nothing changed hands: an infestation kills, it does not recruit.
    AssertEqual(0, landed.Log.HoesTaken);
    AssertEqual(Attacker(options).Hoes, attacker.Hoes);

    // Enough medicine to cover the whole house: the attack achieves nothing but a bad evening.
    var stocked = Defender(options);
    stocked.Hoes = 40;
    stocked.Medicine = (int)Math.Ceiling(40.0 / perCrate);
    var moraleBefore = stocked.HoeHappiness;
    var held = strikes.Resolve(Poisoner(), stocked, Strike(stocked, AttackMethods.Infest), StrikeDefence.Everyone(stocked), now);
    AssertEqual("Defeat", held.Outcome);
    AssertEqual(40, stocked.Hoes);
    AssertTrue(stocked.Medicine < (int)Math.Ceiling(40.0 / perCrate), "crates are used up treating them");
    AssertTrue(stocked.HoeHappiness < moraleBefore, "being infested is still unpleasant");

    // A part-used crate is a used crate. Keeping it would make one crate cover a house forever.
    var thin = Defender(options);
    thin.Hoes = 40;
    thin.Medicine = 1;
    strikes.Resolve(Poisoner(), thin, Strike(thin, AttackMethods.Infest), StrikeDefence.Everyone(thin), now);
    AssertEqual(0, thin.Medicine);

    // And a house with no hoes is not a target at all.
    var empty = Defender(options);
    empty.Hoes = 0;
    AssertRuleError(
        () => strikes.Resolve(Poisoner(), empty, Strike(empty, AttackMethods.Infest), StrikeDefence.Everyone(empty), now),
        "infesting a house with no hoes");
}

// The reason the payout slider is a decision rather than a dial nobody touches. A house paid enough to
// be entirely happy cannot be poached at any price; one squeezed for every dollar can be emptied.
static void PayoutAnswersPoaching()
{
    var options = Resolve(new GameOptions());
    var now = new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc);
    var strikes = CreateStrikes(options, new AlwaysRandom());
    var perHoe = options.Strikes.Poach.CokePerHoe;
    var stake = perHoe * 8;

    // Fully happy: nobody goes, and the coke is spent anyway. That is the risk the move carries.
    var buyer = Attacker(options, coke: 500);
    var loyal = Defender(options);
    loyal.Hoes = 30;
    loyal.HoeHappiness = 100;
    var refused = strikes.Resolve(buyer, loyal, Strike(loyal, AttackMethods.Poach, stake), StrikeDefence.Everyone(loyal), now);
    AssertEqual("Defeat", refused.Outcome);
    AssertEqual(30, loyal.Hoes);
    AssertEqual(500 - stake, buyer.Coke);
    AssertTrue(refused.Summary.Contains("paid too well"), $"and it says why: {refused.Summary}");

    // Squeezed: the same pile walks people out of the house.
    var raider = Attacker(options, coke: 500);
    var squeezed = Defender(options);
    squeezed.Hoes = 30;
    squeezed.HoeHappiness = 20;
    var taken = strikes.Resolve(raider, squeezed, Strike(squeezed, AttackMethods.Poach, stake), StrikeDefence.Everyone(squeezed), now);
    AssertEqual("Victory", taken.Outcome);
    AssertTrue(taken.Log.HoesTaken > 0, "underpaid hoes are temptable");
    // They changed hands rather than died, so both sides move by the same number.
    AssertEqual(30 - taken.Log.HoesTaken, squeezed.Hoes);
    AssertEqual(Attacker(options).Hoes + taken.Log.HoesTaken, raider.Hoes);

    // Stepped-on product tempts fewer people, through the same multiplier the market prices it by.
    var cutBuyer = Attacker(options, coke: 500);
    cutBuyer.CokePurity = 0.25;
    var alsoSqueezed = Defender(options);
    alsoSqueezed.Hoes = 30;
    alsoSqueezed.HoeHappiness = 20;
    var weaker = strikes.Resolve(cutBuyer, alsoSqueezed, Strike(alsoSqueezed, AttackMethods.Poach, stake), StrikeDefence.Everyone(alsoSqueezed), now);
    AssertTrue(weaker.Log.HoesTaken < taken.Log.HoesTaken,
        $"cut coke is worse at this too ({weaker.Log.HoesTaken} against {taken.Log.HoesTaken})");

    // Poaching more than you hold is refused rather than quietly trimmed.
    AssertRuleError(
        () => strikes.Resolve(Attacker(options, coke: 5), squeezed, Strike(squeezed, AttackMethods.Poach, stake), StrikeDefence.Everyone(squeezed), now),
        "staking coke you do not have");
}

// Two shields, two clocks. One column for both would let either loop lock the other out: a four-turn
// drive-by must never buy its victim an hour of immunity from the raid that was actually coming.
static void StrikeAndRaidShieldsAreSeparate()
{
    var options = Resolve(new GameOptions());
    var now = new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc);
    var strikes = CreateStrikes(options, new AlwaysRandom());

    var attacker = Attacker(options, rides: 4);
    var defender = Defender(options);
    strikes.Resolve(attacker, defender, Strike(defender, AttackMethods.DriveBy), StrikeDefence.Everyone(defender), now);

    // The strike set its own clock and left the raid shield alone.
    AssertTrue(defender.StrikeProtectionUntilUtc > now, "a strike shelters against more strikes");
    AssertTrue(defender.CombatProtectionUntilUtc is null, "but grants no shelter from a raid");
    AssertRuleError(
        () => strikes.Resolve(attacker, defender, Strike(defender, AttackMethods.DriveBy), StrikeDefence.Everyone(defender), now),
        "striking someone who was just struck");

    // It expires, and the same target is open again.
    var later = defender.StrikeProtectionUntilUtc!.Value.AddMinutes(1);
    strikes.Resolve(attacker, defender, Strike(defender, AttackMethods.DriveBy), StrikeDefence.Everyone(defender), later);

    // The raid shield, in the other direction, covers everything. Walking in behind somebody else's
    // victory to finish the job is the dogpile the protection exists for.
    var broken = Defender(options);
    broken.CombatProtectionUntilUtc = now.AddHours(1);
    AssertRuleError(
        () => strikes.Resolve(attacker, broken, Strike(broken, AttackMethods.DriveBy), StrikeDefence.Everyone(broken), now),
        "striking a house that has just been raided");

    // And the anti-farm rules apply to strikes exactly as they do to raids.
    var newcomer = Rookie(options);
    AssertRuleError(
        () => strikes.Resolve(attacker, newcomer, Strike(newcomer, AttackMethods.DriveBy), StrikeDefence.Everyone(newcomer), now),
        "striking a target under the net worth floor");
}

// The one counter in the game that buys as well as sells, so a garage is not a one-way purchase.
static void ChopShopBuysBackUnderTheSticker()
{
    var options = Resolve(new GameOptions());
    var economy = CreateEconomy(options);
    var hideouts = CreateHideouts(options);
    var garage = hideouts.CapacityFor(new Hideout { Tier = 1 }).MaxRides;

    var player = new Player
    {
        Cash = options.RidePrice * (garage + 2L),
        Hideout = new Hideout { Tier = 1, StorageLevel = 3, SafeLevel = 5 }
    };

    economy.BuyStoreItem(player, "rides", garage);
    AssertEqual(garage, player.Rides);

    // Refused rather than clamped, and the refusal names the garage rather than sending them off to
    // buy a bigger shelf.
    AssertRuleError(() => economy.BuyStoreItem(player, "rides", 1), "buying past the garage");

    var cashBefore = player.Cash + player.BankCash;
    var sale = economy.SellRides(player, garage);
    AssertEqual(0, player.Rides);
    var proceeds = player.Cash + player.BankCash - cashBefore;
    AssertEqual(options.RideSalePrice * (long)garage, proceeds);
    AssertTrue(options.RideSalePrice < options.RidePrice, "a fleet held only to be resold loses money");
    AssertTrue(sale.Summary.Contains("chop shop"), $"and the notice says where it went: {sale.Summary}");

    AssertRuleError(() => economy.SellRides(player, 1), "selling a ride you do not own");
}

// A CombatLog records the outcome from the attacker's point of view. "Broke through your defence" is
// true of a raid and absurd of a drive-by, and a defender told only that they lost has no idea whether
// to buy medicine, move the cars, or pay the house better.
// The menu of strikes is built from the attacker alone - their thugs, their garage, their coke - and
// has never seen who is being looked at. So the target's half of every rule had nowhere to be said, and
// a player could sit reading "nothing parked there to take" underneath a live button offering to take
// it, learning the rule only by spending the click and being refused.
//
// The same function answers both now, which is the point: the sentence under a dead button has to be
// the sentence the launch would have thrown, or they will drift and one of them will be a lie.
static void AStrikeRefusesBeforeTheClick()
{
    var options = Resolve(new GameOptions());
    var strikes = CreateStrikes(options);

    // Turns are checked before any of this, so give them enough that the ride is what refuses.
    var attacker = new Player { Name = "You", City = "Detroit", Turns = 40, Thugs = 4, Pistols = 4, Coke = 500, CokePurity = 1, Poison = 10, Hideout = new Hideout() };

    // Nothing parked: the jacking is refused, and it is refused by name.
    // Rich enough to be worth attacking at all: the anti-farm floor is checked before any of this,
    // and a pauper is refused for being a pauper long before the garage is looked at.
    var empty = new Player { Name = "Skint", City = "Detroit", Cash = 200_000, Rides = 0, Hoes = 3 };
    var why = strikes.WhyNot(AttackMethods.Jack, attacker, empty);
    AssertTrue(why is not null, "a jacking against an empty garage is refused");
    AssertTrue(why!.Contains("does not own a ride"), $"and says why: {why}");

    // One parked and it is on.
    var owner = new Player { Name = "Parked", City = "Detroit", Cash = 200_000, Rides = 1, Hoes = 3 };
    AssertTrue(strikes.WhyNot(AttackMethods.Jack, attacker, owner) is null, "one ride is enough to be worth taking");

    // The other two strikes that need something on the far end are gated the same way, because the gap
    // was never about rides - it was about the menu not knowing who it was pointed at.
    var noHoes = new Player { Name = "Alone", City = "Detroit", Cash = 200_000, Rides = 2, Hoes = 0 };
    AssertTrue(strikes.WhyNot(AttackMethods.Infest, attacker, noHoes) is not null, "nothing to infest");
    AssertTrue(strikes.WhyNot(AttackMethods.Poach, attacker, noHoes, options.Strikes.Poach.CokePerHoe) is not null, "nobody to poach");
    AssertTrue(strikes.WhyNot(AttackMethods.Infest, attacker, owner) is null, "a house with hoes can be infested");

    // And an attacker with no poison is refused, which is the whole of the new cost: infesting was the
    // one strike that took nothing to throw.
    var empty_handed = new Player { Name = "Broke", City = "Detroit", Turns = 40, Thugs = 4, Poison = 0, Hideout = new Hideout() };
    var noDose = strikes.WhyNot(AttackMethods.Infest, empty_handed, owner);
    AssertTrue(noDose is not null && noDose.Contains("no poison"), $"no poison, no infestation: {noDose}");

    // Your own half still answers: a full garage stops a jacking whatever they have parked.
    var stuffed = new Player { Name = "You", City = "Detroit", Turns = 40, Thugs = 4, Rides = 99, Hideout = new Hideout() };
    var full = strikes.WhyNot(AttackMethods.Jack, stuffed, owner);
    AssertTrue(full is not null && full.Contains("garage"), $"a full garage is still a reason: {full}");

    // And the two answers agree. Asking before the click has to give the sentence the launch throws,
    // or the button and the refusal are two different rules wearing the same words.
    var thrown = string.Empty;
    try
    {
        strikes.Resolve(
            attacker,
            empty,
            new CombatAttackRequest(empty.Id, Method: AttackMethods.Jack),
            StrikeDefence.Everyone(empty),
            Landing());
    }
    catch (GameRuleException error)
    {
        thrown = error.Message;
    }
    AssertEqual(why, thrown);
}

static void DefenceAlertsNameTheStrike()
{
    var attacker = new Player { Name = "Brass Knox" };
    var seen = new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc);

    var jacked = DefenceAlerts.Describe(
        new CombatLog { Attacker = attacker, Method = AttackMethods.Jack, Outcome = "Victory", RidesTaken = 2, CreatedAtUtc = seen.AddMinutes(1) },
        seen);
    AssertTrue(!jacked.HeldTheHouse, "the attacker's victory is the defender's loss");
    AssertTrue(jacked.Headline.Contains("drove off"), $"a jacking reads as a jacking: {jacked.Headline}");
    AssertTrue(jacked.Detail.Contains("2 ride(s)"), $"and names what left: {jacked.Detail}");
    AssertEqual(2, jacked.RidesLost);
    AssertTrue(jacked.IsUnread, "anything after the watermark is unread");

    // A strike that failed is a win for the defender, and should read like one.
    var treated = DefenceAlerts.Describe(
        new CombatLog { Attacker = attacker, Method = AttackMethods.Infest, Outcome = "Defeat", CreatedAtUtc = seen.AddMinutes(1) },
        seen);
    AssertTrue(treated.HeldTheHouse, "their medicine held");
    AssertTrue(treated.Headline.Contains("medicine"), $"and the alert says so: {treated.Headline}");

    // The successful infest was the one headline in this family nothing covered, and the one that had
    // gone wrong: it said somebody "put something through your house", which names neither what was
    // done nor what it was done to. A defender's first line has to stand on its own.
    var infested = DefenceAlerts.Describe(
        new CombatLog { Attacker = attacker, Method = AttackMethods.Infest, Outcome = "Victory", DefenderHoesLost = 5, CreatedAtUtc = seen.AddMinutes(1) },
        seen);
    AssertTrue(!infested.HeldTheHouse, "the medicine did not hold");
    AssertTrue(infested.Headline.Contains("poisoned"), $"a poisoning says so plainly: {infested.Headline}");
    AssertTrue(!infested.Headline.Contains("something"), $"rather than hinting at it: {infested.Headline}");
    AssertTrue(infested.Detail.Contains("5 hoe(s)"), $"and the house is told what it cost: {infested.Detail}");

    // Hoes were missing from the loss list even before the strikes existed, which understated every
    // raid that took any. Two of the four strikes take nothing else.
    var poached = DefenceAlerts.Describe(
        new CombatLog { Attacker = attacker, Method = AttackMethods.Poach, Outcome = "Victory", DefenderHoesLost = 4, HoesTaken = 4, CreatedAtUtc = seen.AddMinutes(1) },
        seen);
    AssertTrue(poached.Detail.Contains("4 hoe(s)"), $"a poached house is told how many went: {poached.Detail}");

    // A row written before the menu existed, or by any caller that names no method, is a raid.
    var legacy = DefenceAlerts.Describe(
        new CombatLog { Attacker = attacker, Method = string.Empty, Outcome = "Victory", CashStolen = 500, CreatedAtUtc = seen.AddMinutes(1) },
        seen);
    AssertTrue(legacy.Headline.Contains("broke through"), $"history still reads as raids: {legacy.Headline}");
}

/// <summary>
/// A striker built without a database. Every rule in the service runs against two plain players: what
/// the defender has standing at home is handed in rather than looked up, which is what keeps it testable.
/// </summary>
static StreetStrikeService CreateStrikes(GameOptions? options = null, IGameRandom? random = null)
{
    var resolved = Resolve(options);
    return new StreetStrikeService(Snapshot(resolved), random ?? new MinimumRandom(), new HideoutService(Snapshot(resolved)));
}

/// <summary>An established player, well clear of the anti-farm floor so strikes are legal both ways.</summary>
static Player Attacker(GameOptions options, int rides = 0, int coke = 0) => new()
{
    Id = Guid.NewGuid(),
    Name = "Attacker",
    City = "Detroit",
    Turns = options.MaxTurns,
    Cash = 40_000,
    Pimps = 2,
    Hoes = 10,
    Thugs = 10,
    Pistols = 10,
    Rides = rides,
    Coke = coke,
    HoeHappiness = 100,
    ThugHappiness = 100,
    Hideout = new Hideout { Tier = 2, StorageLevel = 4, SafeLevel = 3 }
};

static Player Defender(GameOptions options, int rides = 0) => new()
{
    Id = Guid.NewGuid(),
    Name = "Defender",
    City = "Detroit",
    Cash = 40_000,
    Pimps = 2,
    Hoes = 20,
    Thugs = 10,
    Pistols = 10,
    Rides = rides,
    HoeHappiness = 70,
    ThugHappiness = 70,
    Hideout = new Hideout { Tier = 2, StorageLevel = 4, SafeLevel = 3 }
};

static CombatAttackRequest Strike(Player defender, string method, int coke = 0)
    => new(defender.Id, Method: method, Coke: coke);

// The property the whole migration rests on. Every weapon that existed before tiers became a pistol,
// so a player who has never traded up has to fight with precisely the numbers they had the day before.
static void PistolsReproduceTheOldWeapon()
{
    var options = Resolve(new GameOptions());
    var power = options.Combat.Power;
    var firepower = options.WeaponFirepower();

    AssertEqual(1.0, options.WeaponTier(WeaponTiers.Pistol)!.Firepower);

    // A rack of pistols is worth exactly its armed count, which is what the old weapon column was.
    var pistols = new Armoury(Pistols: 10, Shotguns: 0, Smgs: 0, Rifles: 0);
    AssertEqual(10.0, pistols.Firepower(10, firepower));
    // And guns nobody is holding are worth nothing, which the old Math.Min did inside the calculation.
    AssertEqual(6.0, pistols.Firepower(6, firepower));
    AssertEqual(0.0, pistols.Firepower(0, firepower));

    // So the power figures are identical to what the bare count produced.
    AssertEqual(
        CombatPower.Attack(1, 10, Firepower.Sidearms(10, 10), 80, power),
        CombatPower.Attack(1, 10, Firepower.Of(pistols, 10, firepower), 80, power));
    AssertEqual(
        CombatPower.Defence(2, 10, Firepower.Sidearms(10, 10), 80, power),
        CombatPower.Defence(2, 10, Firepower.Of(pistols, 10, firepower), 80, power));

    // The balance target the fight is tuned around is stated in pistols on both sides, so it is
    // untouched by tiers existing.
    var needed = CombatPower.ThugsNeededToMatch(20, 2, 80, power);
    AssertTrue(needed > 20, "a defender still holds at equal armed crew");
    AssertTrue(needed <= 25, $"and an attacker still only needs a modest edge, not {needed} against 20");
}

// The split that makes tiers a decision rather than a bigger number. It is also the whole of the source
// game's "pistol packing": cover the crew with the cheap guns, and fight with the good ones.
static void CoverageAndFirepowerComeApart()
{
    var options = Resolve(new GameOptions());
    var economy = CreateEconomy(options);
    var firepower = options.WeaponFirepower();

    var cheap = new Player { Thugs = 10, Pistols = 10 };
    var dear = new Player { Thugs = 10, Rifles = 10 };

    // Both crews are fully covered, so morale reads the same for each: a thug with a pistol is exactly
    // as content as a thug with a rifle, and that is what makes the cheap gun worth buying at all.
    AssertEqual(cheap.Weapons, dear.Weapons);
    AssertEqual(0, Math.Max(0, cheap.Thugs - cheap.Weapons));
    AssertEqual(0, Math.Max(0, dear.Thugs - dear.Weapons));

    // But they do not fight the same.
    AssertTrue(economy.FirepowerOf(dear) > economy.FirepowerOf(cheap) * 2,
        "ten rifles hit far harder than ten pistols");

    // A gun beyond the crew is a gun nobody is holding, whatever it cost. This is the reason the cap
    // lives inside the rack rather than being left to each caller to remember.
    var hoarder = new Player { Thugs = 2, Rifles = 40 };
    AssertEqual(economy.FirepowerOf(new Player { Thugs = 2, Rifles = 2 }), economy.FirepowerOf(hoarder));

    // And a crew picks up the best of what is on the rack, not a sample of it.
    var mixed = new Armoury(Pistols: 20, Shotguns: 0, Smgs: 0, Rifles: 3);
    var carried = mixed.Best(5);
    AssertEqual(3, carried.Rifles);
    AssertEqual(2, carried.Pistols);
    AssertEqual(5.0 + 3 * (options.WeaponTier(WeaponTiers.Rifle)!.Firepower - 1) - 0, Math.Round(mixed.Firepower(5, firepower) + 0, 10));
}

// Two opposite rules, and both matter. A crew arms itself with the best guns; a bad day takes the
// cheapest ones. The alternative - losses coming off the top - would make owning rifles a liability.
static void CrewsCarryTheBestAndDropTheWorst()
{
    var rack = new Armoury(Pistols: 10, Shotguns: 4, Smgs: 2, Rifles: 1);
    AssertEqual(17, rack.Total);

    // Best first, down through the tiers.
    var carried = rack.Best(5);
    AssertEqual(1, carried.Rifles);
    AssertEqual(2, carried.Smgs);
    AssertEqual(2, carried.Shotguns);
    AssertEqual(0, carried.Pistols);

    // Worst first, up through them.
    var lost = rack.WorstFirst(12);
    AssertEqual(10, lost.Pistols);
    AssertEqual(2, lost.Shotguns);
    AssertEqual(0, lost.Smgs);
    AssertEqual(0, lost.Rifles);

    // Asking for more than there is gives everything, and never a negative shelf.
    AssertEqual(rack, rack.Best(500));
    AssertEqual(rack, rack.WorstFirst(500));
    AssertEqual(Armoury.Empty, rack.Best(0));

    // Every loss in the game goes through the player, cheapest first, and reports what actually went.
    var player = new Player { Pistols = 3, Rifles = 2 };
    var taken = player.RemoveWeapons(4);
    AssertEqual(3, taken.Pistols);
    AssertEqual(1, taken.Rifles);
    AssertEqual(0, player.Pistols);
    AssertEqual(1, player.Rifles);
    AssertEqual(1, player.Weapons);

    // Taking more than is there empties the rack rather than going negative.
    player.RemoveWeapons(99);
    AssertEqual(0, player.Weapons);
    AssertEqual(Armoury.Empty, player.Armoury);
}

// The storage room holds one weapons count across all four tiers. Subtracting only the tier being
// bought from that shared cap would let a player fill the shelf four times over, once per gun.
static void OneShelfHoldsEveryGun()
{
    var options = Resolve(StorageCapOptions(condoms: 10));
    var economy = CreateEconomy(options);
    var hideouts = CreateHideouts(options);
    var capacity = hideouts.CapacityFor(new Hideout());
    AssertEqual(5, capacity.MaxWeapons);

    var player = new Player { Cash = 1_000_000, Pistols = 3, Hideout = new Hideout() };

    // Three pistols on a five-gun shelf leaves room for two more guns of any kind.
    AssertEqual(2, TradeGoods.Room(player, capacity, WeaponTiers.Rifle));
    AssertEqual(2, TradeGoods.Room(player, capacity, WeaponTiers.Pistol));

    economy.BuyStoreItem(player, WeaponTiers.Rifle, 2);
    AssertEqual(5, player.Weapons);
    AssertEqual(0, TradeGoods.Room(player, capacity, WeaponTiers.Shotgun));

    // The shelf is full, whatever kind is asked for next.
    AssertRuleError(() => economy.BuyStoreItem(player, WeaponTiers.Shotgun, 1), "buying past a full gun shelf");
    AssertRuleError(() => economy.BuyStoreItem(player, WeaponTiers.Pistol, 1), "buying past a full gun shelf");

    // Holdings stay per tier even though the room is shared: the market lists rifles, not "weapons".
    AssertEqual(3, TradeGoods.Held(player, WeaponTiers.Pistol));
    AssertEqual(2, TradeGoods.Held(player, WeaponTiers.Rifle));
    AssertTrue(TradeGoods.Keys.Contains(WeaponTiers.Rifle), "each gun trades on its own");

    // Overflow takes the cheap ones, so a shrinking room never eats the rifles first.
    var squeezed = new Player { Pistols = 4, Rifles = 2, Hideout = new Hideout() };
    hideouts.ClampToCapacity(squeezed);
    AssertEqual(5, squeezed.Weapons);
    AssertEqual(2, squeezed.Rifles);
    AssertEqual(3, squeezed.Pistols);
}

// Better guns are steeply worse value per point of firepower, and that is deliberate. Trading up has to
// be the thing you do when the hideout will not hold another thug, not the efficient way to spend money.
static void TradingUpIsForWhenTheHouseIsFull()
{
    var options = Resolve(new GameOptions());
    var tiers = options.Weapons.OrderBy(x => x.Price).ToList();
    AssertEqual(4, tiers.Count);

    // Prices climb, firepower climbs, and cost per point of firepower climbs fastest.
    for (var i = 1; i < tiers.Count; i++)
    {
        AssertTrue(tiers[i].Price > tiers[i - 1].Price, $"{tiers[i].Key} costs more than {tiers[i - 1].Key}");
        AssertTrue(tiers[i].Firepower > tiers[i - 1].Firepower, $"{tiers[i].Key} hits harder than {tiers[i - 1].Key}");
        AssertTrue(tiers[i].Price / tiers[i].Firepower > tiers[i - 1].Price / tiers[i - 1].Firepower,
            $"{tiers[i].Key} is worse value per point than {tiers[i - 1].Key}, or nobody would ever buy the cheap ones");
    }

    // So at the first tier's thug cap, filling the crew with more bodies beats upgrading their guns for
    // the same money - right up until there is nowhere to put another body.
    var options2 = Resolve(new GameOptions());
    var crewCap = options2.Hideout.Tiers.Single(x => x.Level == 1).MaxThugs;
    var rifle = options2.WeaponTier(WeaponTiers.Rifle)!;
    var pistol = options2.WeaponTier(WeaponTiers.Pistol)!;
    var armThemAll = crewCap * (long)pistol.Price;
    var upgradeThemAll = crewCap * (long)rifle.Price;
    AssertTrue(upgradeThemAll > armThemAll * 10,
        "arming a whole crew is cheap; re-arming it with rifles is an entirely different kind of purchase");

    // And the cap is what makes it worth doing at all: past it, guns are the only thing left to buy.
    AssertTrue(rifle.Firepower > pistol.Firepower, "which only pays because a full house cannot grow");
}

// Both of a drive-by's rolls read the guard, and they weight it differently on purpose. Finding somebody
// in the open is mostly about how many were watching the road; losing the car is mostly about what they
// were holding, because a pistol rarely stops a moving car and a rifle very often does.
static void DriveByWeighsBodiesAndGunsDifferently()
{
    var options = Resolve(new GameOptions());
    var config = options.Strikes.DriveBy;
    var now = new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc);

    // The weights themselves, since the asymmetry is the design rather than an accident of tuning.
    AssertTrue(config.HitChancePerArmedThug > config.HitChancePerGuardFirepower,
        "bodies decide whether the pass finds anybody");
    AssertTrue(config.RideLossChancePerGuardFirepower > config.RideLossChancePerArmedThug,
        "guns decide whether the car comes back");

    // Six bodies either way. Only what they are carrying changes.
    const double roll = 0.2;
    var withPistols = Defender(options);
    var withRifles = Defender(options);

    var softDriver = Attacker(options, rides: 1);
    var hardDriver = Attacker(options, rides: 1);
    var soft = CreateStrikes(options, new FixedRandom(roll))
        .Resolve(softDriver, withPistols, Strike(withPistols, AttackMethods.DriveBy), Guard(6, WeaponTiers.Pistol), now);
    var hard = CreateStrikes(options, new FixedRandom(roll))
        .Resolve(hardDriver, withRifles, Strike(withRifles, AttackMethods.DriveBy), Guard(6, WeaponTiers.Rifle), now);

    var softOdds = RequiredBreakdown(soft.Result);
    var hardOdds = RequiredBreakdown(hard.Result);

    // An all-pistol street is worth exactly what it was before guns had tiers: the second term is zero.
    AssertEqual(0.0, Value<double>(softOdds, "guardFirepowerOverSidearms"));
    AssertEqual(
        (int)Math.Round((config.BaseHitChance - 6 * config.HitChancePerArmedThug) * 100),
        Value<int>(softOdds, "hitChancePercent"));
    AssertEqual(
        (int)Math.Round((config.RideLossChance + 6 * config.RideLossChancePerArmedThug) * 100),
        Value<int>(softOdds, "rideLossChancePercent"));

    // Rifles make the street harder to shoot up and much more likely to keep your car.
    AssertEqual(9.0, Value<double>(hardOdds, "guardFirepowerOverSidearms"));
    AssertTrue(Value<int>(hardOdds, "hitChancePercent") < Value<int>(softOdds, "hitChancePercent"),
        "a rifle street is harder to catch anybody on");
    AssertTrue(Value<int>(hardOdds, "rideLossChancePercent") > Value<int>(softOdds, "rideLossChancePercent"),
        "and far likelier to take the car");

    // Which is the difference that actually lands. Identical roll, identical six guards: the pistol
    // street lets the car go home and the rifle street keeps it, in the garage as well as in the log.
    AssertEqual(0, Value<int>(softOdds, "ridesLost"));
    AssertEqual(1, softDriver.Rides);
    AssertEqual(1, Value<int>(hardOdds, "ridesLost"));
    AssertEqual(0, hardDriver.Rides);

    // A street nobody is standing on is the cheapest pass there is, whatever is in the gun cabinet.
    var empty = CreateStrikes(options, new FixedRandom(roll))
        .Resolve(Attacker(options, rides: 1), Defender(options), Strike(Defender(options), AttackMethods.DriveBy), new StrikeDefence(0, new Armoury(0, 0, 0, 40)), now);
    var emptyOdds = RequiredBreakdown(empty.Result);
    AssertEqual(0, Value<int>(emptyOdds, "guardArmedThugs"));
    AssertEqual(0.0, Value<double>(emptyOdds, "guardFirepowerOverSidearms"));
    AssertEqual((int)Math.Round(config.BaseHitChance * 100), Value<int>(emptyOdds, "hitChancePercent"));
}

// The demand is worked out from the player and the week rather than stored, so it has to come back the
// same all week and differ between players. If it moved when the page was reloaded it would not be a
// demand at all, it would be a slot machine with extra steps.
static void TheGodsAskForSomethingSpecific()
{
    var options = Resolve(new GameOptions());
    var prayer = CreatePrayer(options);
    var monday = new DateTime(2026, 8, 17, 9, 0, 0, DateTimeKind.Utc);
    var friday = new DateTime(2026, 8, 21, 23, 0, 0, DateTimeKind.Utc);
    var nextWeek = new DateTime(2026, 8, 25, 9, 0, 0, DateTimeKind.Utc);

    var faithful = Rookie(options);
    faithful.Id = Guid.Parse("11111111-1111-1111-1111-111111111111");

    var asked = prayer.DemandFor(faithful, monday);
    AssertTrue(asked.Quantity > 0, "they always want something");
    AssertEqual(asked.Good, prayer.DemandFor(faithful, friday).Good);
    AssertEqual(asked.Quantity, prayer.DemandFor(faithful, friday).Quantity);

    // A different player is asked a different thing, and the same player a different thing next week.
    var other = Rookie(options);
    other.Id = Guid.Parse("22222222-2222-2222-2222-222222222222");
    var differs = prayer.DemandFor(other, monday).Good != asked.Good
        || prayer.DemandFor(faithful, nextWeek).Good != asked.Good
        || prayer.DemandFor(faithful, nextWeek).Quantity != asked.Quantity;
    AssertTrue(differs, "the ask is not the same thing for everybody forever");

    // Scaled to what the player is worth, so it means the same to a rookie and to an empire. Measured
    // with a room big enough that the shelf is not what is deciding the number, since the two limits
    // would otherwise be tested at once and neither of them properly.
    var small = Rookie(options);
    small.Id = faithful.Id;
    small.Hideout = new Hideout { Tier = 4, StorageLevel = 6, SafeLevel = 5 };
    var empire = Rookie(options);
    empire.Id = faithful.Id;
    empire.Hideout = new Hideout { Tier = 4, StorageLevel = 6, SafeLevel = 5 };
    empire.BankCash = 5_000_000;

    var smallAsk = prayer.DemandFor(small, monday);
    var bigAsk = prayer.DemandFor(empire, monday);
    AssertEqual(smallAsk.Good, bigAsk.Good);
    AssertTrue(bigAsk.Quantity > smallAsk.Quantity, "a bigger empire is asked for more");

    // And never more than the player could physically keep. A share is a value, and a value in a cheap
    // good is an enormous pile: four percent of a mid empire is hundreds of bottles of moonshine, which
    // no storage room in the game holds. Generously is still reachable too, or the only choice the
    // shrine offers would be closed for every good with a shelf.
    var hideouts = CreateHideouts(options);
    foreach (var tier in new[] { 1, 2, 4 })
    {
        var stocked = Rookie(options);
        stocked.BankCash = 20_000_000;
        stocked.Hideout = new Hideout { Tier = tier, StorageLevel = tier, SafeLevel = 1 };
        var capacity = hideouts.CapacityFor(stocked.Hideout);
        for (var day = 0; day < 40; day++)
        {
            var ask = prayer.DemandFor(stocked, monday.AddDays(day * 7));
            if (ask.Good == "cash") continue;
            var shelf = TradeGoods.Capacity(capacity, ask.Good);
            AssertTrue(ask.Quantity <= shelf,
                $"they ask for {ask.Quantity:N0} {ask.Label} against a shelf holding {shelf:N0}");
            AssertTrue(ask.Quantity * options.Prayer.GenerousMultiplier <= shelf,
                $"and generosity in {ask.Label} still fits the room");
        }
    }

    // But banded, so ordinary play does not move it. Net worth changes every time anything happens, and
    // an ask that tracked it exactly would quote one number on the shrine and enforce another the moment
    // the player clicked - including when the thing they are buying to offer is what moves it.
    var earner = Rookie(options);
    earner.Id = faithful.Id;
    earner.Hideout = new Hideout { Tier = 4, StorageLevel = 6, SafeLevel = 5 };
    earner.BankCash = 5_000_000;
    var beforeEarning = prayer.DemandFor(earner, monday).Quantity;
    earner.BankCash += 20_000;
    AssertEqual(beforeEarning, prayer.DemandFor(earner, monday).Quantity);
}

// Meeting the demand is answered every time, and none of the answers is money. A shrine a player can
// farm is a printer with a candle in front of it.
static void PrayingIsAnsweredAndNeverPays()
{
    var options = Resolve(new GameOptions());
    var prayer = CreatePrayer(options);
    var now = new DateTime(2026, 8, 17, 9, 0, 0, DateTimeKind.Utc);

    // Stocked first, then asked. Reading the demand and then changing what the player owns would be
    // asking about a different player than the one who turns up at the shrine.
    var player = Worshipper(options, now);
    Stock(player, prayer.DemandFor(player, now).Good, 10_000_000);
    var demand = prayer.DemandFor(player, now);

    var cashBefore = player.Cash + player.BankCash;
    var heldBefore = Held(player, demand.Good);

    // Short of the ask is refused before anything is taken. Taking the offering and reporting that the
    // gods were unmoved is the kind of mechanic that teaches people never to touch a thing again.
    AssertRuleError(() => prayer.Offer(player, demand.Quantity - 1, now), "offering less than was asked");
    AssertEqual(heldBefore, Held(player, demand.Good));
    AssertTrue(player.LastPrayedAtUtc is null, "and a refused offering is not a prayer");

    var result = prayer.Offer(player, demand.Quantity, now);
    AssertEqual(demand.Quantity, result.Offered);
    AssertEqual(heldBefore - demand.Quantity, Held(player, demand.Good));
    AssertTrue(result.Summary.Length > 0, "something is always said back");
    AssertEqual(now, player.LastPrayedAtUtc);

    // Whatever landed, it was not money. This is the property that makes the shrine safe to keep.
    var cashAfter = player.Cash + player.BankCash;
    AssertTrue(cashAfter <= cashBefore, $"the shrine never pays out ({cashBefore} in, {cashAfter} out)");

    // Once a week, and the refusal says when.
    var soon = now.AddDays(options.Prayer.CooldownDays - 1);
    AssertTrue(!prayer.CanPray(player, soon), "the shrine is shut for the week");
    AssertRuleError(() => prayer.Offer(player, demand.Quantity, soon), "praying twice in a week");

    var later = now.AddDays(options.Prayer.CooldownDays);
    AssertTrue(prayer.CanPray(player, later), "and open again when the week turns");

    // Generosity is the only thing the player controls, and it is what buys the rationed blessing.
    var generous = Worshipper(options, later);
    Stock(generous, prayer.DemandFor(generous, later).Good, 10_000_000);
    var generousDemand = prayer.DemandFor(generous, later);
    generous.Turns = 0;
    var big = prayer.Offer(generous, generousDemand.Quantity * options.Prayer.GenerousMultiplier, later);
    AssertTrue(big.Generous, "twice the ask is a generous offering");

    // Offering more than is held is refused rather than quietly trimmed.
    var broke = Worshipper(options, later);
    var brokeDemand = prayer.DemandFor(broke, later);
    AssertRuleError(() => prayer.Offer(broke, brokeDemand.Quantity * 1000, later), "offering what you do not have");

    static long Held(Player player, string good)
        => good == "cash" ? player.Cash : TradeGoods.Held(player, good);

    static void Stock(Player player, string good, long amount)
    {
        if (good == "cash") player.Cash = amount;
        else TradeGoods.Add(player, good, (int)amount);
    }
}

// Titles are read out of the fights that happened rather than kept as counters, and half of them are
// for things done to a player rather than by them - which is the source game's own reading of what a
// stat board is for, and the half that makes it funny.
static void TitlesNameLeadersBothWaysRound()
{
    var options = Resolve(new GameOptions());
    var minimum = options.Titles.MinimumToHold;
    var winner = Guid.NewGuid();
    var loser = Guid.NewGuid();
    var nobody = Guid.NewGuid();

    // Every category is named, and each says which end of the fight it reads.
    AssertEqual(7, TitleCategories.All.Count);
    AssertTrue(TitleCategories.All.Any(x => x.FromTheAttackersSide), "some titles are for what you did");
    AssertTrue(TitleCategories.All.Any(x => !x.FromTheAttackersSide), "and some for what was done to you");
    AssertEqual(TitleCategories.All.Count, TitleCategories.All.Select(x => x.Key).Distinct().Count());
    AssertEqual(TitleCategories.All.Count, TitleCategories.All.Select(x => x.Title).Distinct().Count());

    // A player carrying a category leads it; one under the floor carries nothing.
    var poaching = TitleCategories.All.Single(x => x.Key == "poacher");
    AssertEqual(minimum + 5, poaching.Measure(new TitleTally(winner, "Winner", minimum + 5, 0, 0, 0, 0)));

    // The floor is what stops every category always having a holder in a quiet world.
    AssertTrue(minimum > 1, "one of anything should not earn a name");

    // The same column read from the other side is a different title, which is the whole design: the
    // hoes one player walked off with are the hoes another player lost.
    var poached = TitleCategories.All.Single(x => x.Key == "poached");
    AssertTrue(poaching.FromTheAttackersSide && !poached.FromTheAttackersSide,
        "the same number is a boast on one side and a bruise on the other");
    AssertEqual(poaching.Measure(new TitleTally(loser, "Loser", 9, 0, 0, 0, 0)), poached.Measure(new TitleTally(loser, "Loser", 9, 0, 0, 0, 0)));

    // Reading a player's own titles off a board is a filter, not a query.
    var board = new List<PlayerTitleResponse>
    {
        new("poacher", "Silver Tongue", winner, "Winner", 9, "took nine"),
        new("killer", "Body Count", winner, "Winner", 12, "killed twelve"),
        new("onfoot", "On Foot", loser, "Loser", 4, "lost four")
    };
    AssertEqual(2, TitleService.For(winner, board).Count);
    AssertTrue(TitleService.For(winner, board).Contains("Body Count"), "a player can hold more than one");
    AssertEqual(1, TitleService.For(loser, board).Count);
    AssertEqual(0, TitleService.For(nobody, board).Count);
}

static PrayerService CreatePrayer(GameOptions? options = null, IGameRandom? random = null)
{
    var resolved = Resolve(options);
    return new PrayerService(
        Snapshot(resolved),
        random ?? new MinimumRandom(),
        new PimpRoster(Snapshot(resolved), new MinimumRandom()),
        new HideoutService(Snapshot(resolved)));
}

/// <summary>A player established enough that the shrine asks them for something real.</summary>
static Player Worshipper(GameOptions options, DateTime nowUtc) => new()
{
    Id = Guid.NewGuid(),
    Name = "Faithful",
    City = "Detroit",
    Turns = options.MaxTurns,
    Cash = 250_000,
    Pimps = 2,
    Hoes = 20,
    Thugs = 10,
    Pistols = 10,
    HoeHappiness = 70,
    ThugHappiness = 70,
    Hideout = new Hideout { Tier = 2, StorageLevel = 6, SafeLevel = 5 },
    CreatedAtUtc = nowUtc.AddMonths(-1)
};

// The source game had five districts and its own guide admits it never found a difference between any
// of them. Five names on a dropdown that all do the same thing is a wasted click, so the only version
// worth building is one where each is worth going to for something and costs something to go to.
static void DistrictsAreWorthChoosingBetween()
{
    var options = Resolve(new GameOptions());
    var districts = options.StreetAction.Districts;
    AssertEqual(5, districts.Count);
    AssertEqual(5, districts.Select(x => x.Key).Distinct().Count());
    AssertEqual(1, districts.Count(x => x.IsDefault));

    // The default is the neutral one, at exactly the base numbers, so a player who never touches the
    // picker works precisely the shift they always did and nothing about the old balance moved.
    var neutral = options.StreetAction.DefaultDistrict();
    AssertEqual("lowrent", neutral.Key);
    AssertEqual(100, neutral.GrossPercent);
    AssertEqual(100, neutral.HoeRecruitPercent);
    AssertEqual(100, neutral.ThugRecruitPercent);
    AssertEqual(100, neutral.PimpRecruitPercent);
    AssertEqual(100, neutral.FindPercent);
    AssertEqual(100, neutral.HeatPercent);

    // Every other district leads at something. A district nobody would ever pick is decoration.
    foreach (var district in districts.Where(x => !x.IsDefault))
    {
        var best = new[]
        {
            district.GrossPercent,
            district.HoeRecruitPercent,
            district.ThugRecruitPercent,
            district.PimpRecruitPercent,
            district.FindPercent
        }.Max();
        AssertTrue(best > 100, $"{district.Key} is not the best place for anything");

        // And costs something to go to, in what it gives up or in what it draws.
        var gives = new[]
        {
            district.GrossPercent,
            district.HoeRecruitPercent,
            district.ThugRecruitPercent,
            district.PimpRecruitPercent,
            district.FindPercent
        }.Min();
        AssertTrue(gives < 100 || district.HeatPercent > 100,
            $"{district.Key} is better at something and worse at nothing");
    }

    // The two that pay best are the two the law is already watching, which is the trade the whole set
    // is built on: what you go home with against how much notice you drew getting it.
    var casino = districts.Single(x => x.Key == "casino");
    var slums = districts.Single(x => x.Key == "winos");
    AssertTrue(casino.GrossPercent > neutral.GrossPercent && casino.HeatPercent > neutral.HeatPercent,
        "the rich district pays more and is watched harder");
    AssertTrue(slums.GrossPercent < neutral.GrossPercent && slums.HeatPercent < neutral.HeatPercent,
        "and the poor one pays less and is watched less");
    AssertTrue(slums.ThugRecruitPercent > casino.ThugRecruitPercent,
        "which is why you go to the slums for men rather than for money");

    // It reaches the shift, rather than reading well in the options and doing nothing. AlwaysRandom
    // rolls zero, so every chance lands and the difference is entirely the multipliers.
    var tuned = Resolve(Tuned());
    var takings = new Dictionary<string, long>();
    var heat = new Dictionary<string, double>();
    foreach (var key in new[] { "lowrent", "casino", "winos" })
    {
        var worker = Rookie(tuned);
        worker.Hoes = 20;
        worker.Thugs = 10;
        worker.Condoms = 500;
        worker.Beer = 500;
        var shift = CreateEconomy(tuned, new AlwaysRandom()).Scout(worker, 10, district: key);
        takings[key] = Value<long>(RequiredBreakdown(shift), "gross");
        heat[key] = worker.Heat;
        AssertEqual(key, Value<string>(RequiredBreakdown(shift), "district"));
        AssertTrue(shift.Summary.Contains(tuned.StreetAction.District(key)!.Name),
            $"the notice says where they worked: {shift.Summary}");
    }

    AssertTrue(takings["casino"] > takings["lowrent"], "the casino pays better");
    AssertTrue(takings["winos"] < takings["lowrent"], "and the slums pay worse");
    AssertTrue(heat["casino"] > heat["lowrent"] && heat["lowrent"] > heat["winos"],
        "and notice follows the money in the same order");

    // A district nobody has heard of is a caller getting it wrong, not a reason to work somewhere else.
    AssertRuleError(
        () => CreateEconomy(tuned).Scout(Rookie(tuned), 1, district: "nowhere"),
        "working a district that does not exist");

    // Naming nothing is the neutral district, which is what every caller written before there was a
    // choice was always asking for.
    var plain = CreateEconomy(tuned, new AlwaysRandom()).Scout(Rookie(tuned), 1);
    AssertEqual("lowrent", Value<string>(RequiredBreakdown(plain), "district"));

    static GameOptions Tuned() => new()
    {
        StreetAction = new StreetActionOptions
        {
            // No finds, so the takings compared above are the district's doing and nothing else.
            Finds = NoFinds()
        }
    };
}

// The truce is the whole of what an alliance is for, and it has to hold on every way of moving on
// somebody. Enforced in the rules rather than socially, which is the half the source game left to the
// message board and a rule nobody enforces is not a rule.
static void AllianceIsATruce()
{
    var options = Resolve(new GameOptions());
    var now = new DateTime(2026, 8, 18, 9, 0, 0, DateTimeKind.Utc);

    var ally = Attacker(options, rides: 2, coke: 500);
    var friend = Defender(options, rides: 2);
    var stranger = Defender(options, rides: 2);

    // Unaligned is not allied. Two people with nothing between them are not on the same side.
    AssertTrue(!AllianceService.AreAllied(ally, friend), "no crew is not the same crew");

    ally.AllianceId = 7;
    AssertTrue(!AllianceService.AreAllied(ally, friend), "one of them in a crew is still not a truce");

    friend.AllianceId = 7;
    AssertTrue(AllianceService.AreAllied(ally, friend), "the same crew is");
    stranger.AllianceId = 9;
    AssertTrue(!AllianceService.AreAllied(ally, stranger), "a different one is not");

    // Every strike refuses an ally, and the refusal names the reason rather than a generic block.
    var strikes = CreateStrikes(options, new AlwaysRandom());
    foreach (var method in AttackMethods.Strikes)
    {
        AssertRuleError(
            () => strikes.Resolve(ally, friend, Strike(friend, method, 200), StrikeDefence.Everyone(friend), now),
            $"{method} against your own crew");
    }

    // And the same player, out of the crew, is a target again. The truce is membership, not friendship.
    friend.AllianceId = null;
    var landed = strikes.Resolve(ally, friend, Strike(friend, AttackMethods.DriveBy), StrikeDefence.Everyone(friend), now);
    AssertTrue(landed.Log.TurnsSpent > 0, "leaving the crew ends the truce");

    // The legacy instant path holds it too, so no route into a fight can miss it.
    var combat = CreateCombat(options);
    var aligned = new Player { Id = Guid.NewGuid(), Name = "One", Turns = 50, Pimps = 1, Thugs = 5, AllianceId = 3 };
    var alsoAligned = new Player { Id = Guid.NewGuid(), Name = "Two", Cash = 5_000, Pimps = 1, Thugs = 1, AllianceId = 3 };
    AssertRuleError(() => combat.Attack(aligned, alsoAligned, now), "attacking your own crew");
}

// Dues sit beside the hoe cut because they are the same kind of thing: a share of what the shift
// grossed, gone before the money reaches you. Off the gross rather than off what is left, or the second
// rate would quietly mean something different depending on the first.
static void DuesComeOffTheGross()
{
    var options = Resolve(new GameOptions());
    options.StreetAction.Finds = NoFinds();
    var economy = CreateEconomy(options, new AlwaysRandom());

    var crew = new Alliance { Id = 1, Name = "The Table", DuesPercent = 20, FounderId = Guid.NewGuid() };

    var loyal = Rookie(options);
    loyal.Hoes = 20;
    loyal.Thugs = 5;
    loyal.Condoms = 500;
    loyal.Beer = 500;
    loyal.HoeCutPercent = 40;
    loyal.Alliance = crew;
    loyal.AllianceId = crew.Id;

    var result = economy.Scout(loyal, 10);
    var breakdown = RequiredBreakdown(result);
    var gross = Value<long>(breakdown, "gross");
    var crewCut = Value<long>(breakdown, "crewPayout");
    var dues = Value<long>(breakdown, "allianceDues");
    var kept = Value<long>(breakdown, "playerProfit");

    // Both cuts are taken off the same number, so 40% and 20% give up 60% of the shift rather than 52%.
    AssertEqual((long)Math.Round(gross * 0.40, MidpointRounding.AwayFromZero), crewCut);
    AssertEqual((long)Math.Round(gross * 0.20, MidpointRounding.AwayFromZero), dues);
    AssertEqual(gross - crewCut - dues, kept);

    // The money is in the treasury rather than nowhere, and it is the same money.
    AssertEqual(dues, crew.Treasury);
    AssertTrue(dues > 0, "a crew with a rate actually collects");
    AssertTrue(result.Summary.Contains("The Table"), $"and the shift says who took it: {result.Summary}");

    // A player with no crew pays nothing and is told nothing about dues.
    var alone = Rookie(options);
    alone.Hoes = 20;
    alone.Thugs = 5;
    alone.Condoms = 500;
    alone.Beer = 500;
    alone.HoeCutPercent = 40;
    var solo = economy.Scout(alone, 10);
    AssertEqual(0L, Value<long>(RequiredBreakdown(solo), "allianceDues"));
    AssertTrue(!solo.Summary.Contains("dues"), $"and nothing is said about them: {solo.Summary}");

    // Between them a crew and a house can never take more than the shift actually made.
    var squeezed = Rookie(options);
    squeezed.Hoes = 20;
    squeezed.Thugs = 5;
    squeezed.Condoms = 500;
    squeezed.Beer = 500;
    squeezed.HoeCutPercent = 95;
    squeezed.Alliance = new Alliance { Id = 2, Name = "The Vice", DuesPercent = 20, FounderId = Guid.NewGuid() };
    squeezed.AllianceId = 2;
    var thin = economy.Scout(squeezed, 10);
    var thinBreakdown = RequiredBreakdown(thin);
    AssertTrue(Value<long>(thinBreakdown, "playerProfit") >= 0, "a shift never pays out less than nothing");
    AssertTrue(
        Value<long>(thinBreakdown, "crewPayout") + Value<long>(thinBreakdown, "allianceDues")
        <= Value<long>(thinBreakdown, "gross"),
        "and the two cuts together never exceed what was earned");

    // The ceiling on a founder's rate is a real rule, not advice.
    AssertTrue(options.Alliances.MaxDuesPercent < 100, "a founder cannot take everything");
    // And a crew is sized against this world rather than the one the source game had.
    AssertTrue(options.Alliances.MaxMembers < 20, "six-ish, not twenty: this world is two dozen rivals");
    AssertEqual(3, options.Alliances.RivalCrews.Count);
}

// The one rule keeping the shared pool from breaking the fight. Alliance thugs ignore the hideout's
// thug cap, which is the constraint every combat number is measured against - so without a limit a Trap
// House with a rich crew behind it could field a Penthouse army and the whole ladder would stop meaning
// anything. Tied to the member's own crew, the pool doubles you rather than replacing you.
static void PoolAmplifiesRatherThanReplaces()
{
    var options = Resolve(new GameOptions());
    var alliances = CreateAlliances(options);

    // You may bring as many as you brought yourself, and no more.
    AssertEqual(0, alliances.BorrowLimit(0));
    AssertEqual(10, alliances.BorrowLimit(10));
    AssertEqual(45, alliances.BorrowLimit(45));

    // Which means a tier still decides a ceiling: the pool moves it by a factor, never past it.
    var trapHouse = options.Hideout.Tiers.Single(x => x.Level == 1).MaxThugs;
    var penthouse = options.Hideout.Tiers.Single(x => x.Level == 4).MaxThugs;
    AssertTrue(trapHouse + alliances.BorrowLimit(trapHouse) < penthouse,
        "a first-tier player with the whole crew behind them still cannot field a top-tier army");

    // Posting defenders runs under the same rule, so an empty house cannot be made a fortress with
    // borrowed men.
    var crew = new Alliance { Id = 1, Name = "The Table", FounderId = Guid.NewGuid(), DefensiveThugs = 50 };
    var thin = Worshipper(options, DateTime.UtcNow);
    thin.Thugs = 4;
    thin.Alliance = crew;
    thin.AllianceId = crew.Id;
    thin.AllianceRank = AllianceRank.Soldier;

    AssertEqual(4, alliances.BorrowLimit(thin.Thugs));

    // They come out of the pool and stand somewhere specific, rather than defending everybody at once.
    var posted = alliances.PostDefenders(thin, crew, 4);
    AssertEqual(4, posted);
    AssertEqual(4, thin.AllianceDefenders);
    AssertEqual(46, crew.DefensiveThugs);

    // Past the limit is refused, and what is already standing there counts towards it.
    AssertRuleError(() => alliances.PostDefenders(thin, crew, 1),
        "posting more of the crew's thugs than you have of your own");

    // Sending them back returns them to the pool exactly.
    var released = alliances.PostDefenders(thin, crew, -3);
    AssertEqual(-3, released);
    AssertEqual(1, thin.AllianceDefenders);
    AssertEqual(49, crew.DefensiveThugs);

    // Buying is gated by rank, because it is the crew's money and somebody has to answer for it.
    var member = Worshipper(options, DateTime.UtcNow);
    member.Alliance = crew;
    member.AllianceId = crew.Id;
    member.AllianceRank = AllianceRank.Soldier;
    crew.Treasury = 100_000;
    AssertRuleError(() => alliances.BuyThugs(member, crew, "offensive", 1),
        "a member spending the crew's money");

    // And it is bounded by the treasury rather than by optimism.
    var founder = Worshipper(options, DateTime.UtcNow);
    founder.Alliance = crew;
    founder.AllianceId = crew.Id;
    founder.AllianceRank = AllianceRank.Boss;
    crew.FounderId = founder.Id;
    AssertRuleError(() => alliances.BuyThugs(founder, crew, "offensive", 1_000),
        "buying more than the treasury holds");

    var cost = alliances.BuyThugs(founder, crew, "offensive", 4);
    AssertEqual(options.Alliances.OffensiveThugCost * 4, cost);
    AssertEqual(100_000 - cost, crew.Treasury);
    AssertEqual(4, crew.OffensiveThugs);

    // A pool is a long project rather than a purchase: a hundred of them is many millions of dues.
    AssertTrue(options.Alliances.OffensiveThugCost * 100 > 1_000_000,
        "a hundred borrowed thugs is a crew-sized undertaking");

    // And one of them is worth exactly one armed thug, not a second combat system.
    AssertEqual(1.0, options.Alliances.ThugFirepower);
}

/// <summary>
/// An alliance service without a database. Every rule tested here decides what a member may do with the
/// pool, and none of them reads a row: the crew is handed in on the player.
/// </summary>
static AllianceService CreateAlliances(GameOptions? options = null)
{
    var resolved = Resolve(options);
    return new AllianceService(null!, Snapshot(resolved), CreateEconomy(resolved));
}

// Ranks on their own are decoration - words next to names. What makes them a system is that every
// power is gated by one, and that acting on a person needs you to be above them rather than merely
// entitled to the verb.
static void RanksGatePowersAndPeople()
{
    var options = Resolve(new GameOptions());
    var crew = new Alliance { Id = 1, Name = "The Table", FounderId = Guid.NewGuid(), DefensiveThugs = 40, Treasury = 500_000 };

    var boss = Member(options, crew, AllianceRank.Boss);
    var under = Member(options, crew, AllianceRank.Underboss);
    var enforcer = Member(options, crew, AllianceRank.Enforcer);
    var soldier = Member(options, crew, AllianceRank.Soldier);

    // The ladder is ordered, and that order is the whole mechanism.
    AssertTrue(AllianceRank.Boss > AllianceRank.Underboss, "the ladder is ordered");
    AssertTrue(AllianceRanks.Outranks(AllianceRank.Underboss, AllianceRank.Enforcer), "above is above");
    AssertTrue(!AllianceRanks.Outranks(AllianceRank.Enforcer, AllianceRank.Enforcer),
        "equal is not above: two of the same rank throwing each other out is a fight, not a chain of command");

    // Defaults: the door at Enforcer, throwing people out at Underboss, the treasury at Underboss.
    AssertTrue(AllianceService.Can(enforcer, crew, AlliancePower.Invite), "an enforcer opens the door");
    AssertTrue(!AllianceService.Can(soldier, crew, AlliancePower.Invite), "a soldier does not");
    AssertTrue(AllianceService.Can(under, crew, AlliancePower.Expel), "an underboss throws people out");
    AssertTrue(!AllianceService.Can(enforcer, crew, AlliancePower.Expel), "an enforcer does not");
    AssertTrue(AllianceService.Can(soldier, crew, AlliancePower.PostDefenders), "everybody can stand men at their own door");

    // Somebody outside the crew has no powers in it whatever their rank says.
    var outsider = Member(options, crew, AllianceRank.Boss);
    outsider.AllianceId = 99;
    AssertTrue(!AllianceService.Can(outsider, crew, AlliancePower.Invite), "rank in one crew is nothing in another");

    var alliances = CreateAlliances(options);

    // Spending is gated, and the gate is the rank rather than who founded it.
    AssertRuleError(() => alliances.BuyThugs(soldier, crew, "offensive", 1), "a soldier spending the treasury");
    AssertTrue(alliances.BuyThugs(under, crew, "offensive", 1) > 0, "an underboss can");

    // Posting is gated on the way out and deliberately not on the way back: somebody demoted while
    // holding the crew's men must still be able to hand them over.
    crew.SetMinRankFor(AlliancePower.PostDefenders, AllianceRank.Underboss);
    soldier.Thugs = 10;
    AssertRuleError(() => alliances.PostDefenders(soldier, crew, 2), "posting below the threshold");
    soldier.AllianceDefenders = 3;
    AssertEqual(-3, alliances.PostDefenders(soldier, crew, -3));
    AssertEqual(0, soldier.AllianceDefenders);
}

// The boss configures where every line is drawn, which is the part of a clan system that actually gets
// used: two crews with identical ranks and different thresholds run completely differently, and neither
// of them had to be programmed.
static void BossDrawsTheLines()
{
    var options = Resolve(new GameOptions());
    var crew = new Alliance { Id = 1, Name = "The Table", FounderId = Guid.NewGuid() };

    // Every power has a line, and every line is readable and settable.
    foreach (var power in Enum.GetValues<AlliancePower>())
    {
        crew.SetMinRankFor(power, AllianceRank.Boss);
        AssertEqual(AllianceRank.Boss, crew.MinRankFor(power));
        crew.SetMinRankFor(power, AllianceRank.Soldier);
        AssertEqual(AllianceRank.Soldier, crew.MinRankFor(power));
    }

    // Moving a line moves who can do the thing, with nothing else changing.
    var enforcer = Member(options, crew, AllianceRank.Enforcer);
    crew.SetMinRankFor(AlliancePower.Expel, AllianceRank.Underboss);
    AssertTrue(!AllianceService.Can(enforcer, crew, AlliancePower.Expel), "below the line");
    crew.SetMinRankFor(AlliancePower.Expel, AllianceRank.Enforcer);
    AssertTrue(AllianceService.Can(enforcer, crew, AlliancePower.Expel), "and on it");

    // An unknown rank on the wire is the one that can do the least, which is the safe way to fail.
    AssertEqual(AllianceRank.Soldier, AllianceRanks.Parse("nonsense"));
    AssertEqual(AllianceRank.Soldier, AllianceRanks.Parse(null));
    AssertEqual(AllianceRank.Underboss, AllianceRanks.Parse("underboss"));
    AssertEqual(AllianceRank.Boss, AllianceRanks.Parse("Boss"));

    // Four rungs, each named once. A crew of six does not need more structure than this.
    AssertEqual(4, AllianceRanks.All.Length);
    AssertEqual(4, AllianceRanks.All.Select(AllianceRanks.Label).Distinct().Count());
    AssertTrue(AllianceRanks.All.Length < options.Alliances.MaxMembers,
        "fewer rungs than members, or the ladder is longer than the crew standing on it");
}

/// <summary>A member of a crew at a given rank, for the rules that are about standing rather than money.</summary>
static Player Member(GameOptions options, Alliance crew, AllianceRank rank)
{
    var player = Worshipper(options, DateTime.UtcNow);
    player.Alliance = crew;
    player.AllianceId = crew.Id;
    player.AllianceRank = rank;
    return player;
}

// One setting with three states rather than a boolean with two paths always open underneath it. The
// old shape said "open or not" and quietly accepted applications either way, so a crew that had shut
// its door was still fielding requests it had no way to stop.
static void TheDoorIsOneSettingWithThreeStates()
{
    var options = Resolve(new GameOptions());

    // Three, and exactly the three things an outsider can do on their own: walk in, ask, or wait.
    AssertEqual(3, AllianceDoors.All.Length);
    AssertEqual(3, AllianceDoors.All.Select(AllianceDoors.Label).Distinct().Count());
    AssertEqual(3, AllianceDoors.All.Select(AllianceDoors.Describe).Distinct().Count());

    // A crew opens by default: a new one nobody can reach is a worse starting state than one anybody
    // can walk into, since the boss can shut it the moment they want to.
    AssertEqual(AllianceDoor.Open, new Alliance().Door);

    // An unknown value off the wire is the most restrictive state. A door that failed open would hand
    // a crew to anybody who sent a malformed setting.
    AssertEqual(AllianceDoor.InviteOnly, AllianceDoors.Parse("nonsense"));
    AssertEqual(AllianceDoor.InviteOnly, AllianceDoors.Parse(null));
    AssertEqual(AllianceDoor.Open, AllianceDoors.Parse("open"));
    AssertEqual(AllianceDoor.Application, AllianceDoors.Parse("Application"));

    // The world ships one of each, so a player meets all three on their first look at the board rather
    // than discovering two of them only after building a crew of their own.
    var doors = options.Alliances.RivalCrews.Select(x => AllianceDoors.Parse(x.Door)).ToList();
    AssertEqual(3, doors.Distinct().Count());
    foreach (var door in AllianceDoors.All)
        AssertTrue(doors.Contains(door), $"the world ships a crew that is {AllianceDoors.Label(door)}");
}

static EconomyService CreateEconomy(GameOptions? options = null, IGameRandom? random = null)
{
    var resolved = Resolve(options);
    return new EconomyService(
        Snapshot(resolved),
        random ?? new MinimumRandom(),
        new HideoutService(Snapshot(resolved)),
        new PimpRoster(Snapshot(resolved), new MinimumRandom()));
}

/// <summary>
/// Built without a database on purpose. The methods under test here decide what held ground is worth
/// and how much of it a tier may run, and neither reads a row: the caller hands them the ground.
/// </summary>
static TerritoryService CreateTerritories(GameOptions options)
    => new(null!, Snapshot(options));

static PimpRoster CreatePimps(GameOptions? options = null)
{
    var resolved = Resolve(options);
    return new PimpRoster(Snapshot(resolved), new MinimumRandom());
}

static TurnService CreateTurns(GameOptions? options = null)
{
    var resolved = Resolve(options);
    return new TurnService(Snapshot(resolved), new PimpRoster(Snapshot(resolved), new MinimumRandom()));
}

static PimpRoster CreateRoster(GameOptions? options = null, IGameRandom? random = null)
    => new(Snapshot(Resolve(options)), random ?? new MinimumRandom());

static HideoutService CreateHideouts(GameOptions? options = null)
    => new(Snapshot(Resolve(options)));

/// <summary>Mirrors the API's PostConfigure step, which fills the tables config left empty.</summary>
static GameOptions Resolve(GameOptions? options)
{
    var resolved = options ?? new GameOptions();
    resolved.ApplyWeaponDefaultsWhereEmpty();
    resolved.StreetAction.ApplyDistrictDefaultsWhereEmpty();
    resolved.Alliances.ApplyDefaultsWhereEmpty();
    resolved.Hideout.ApplyDefaultsWhereEmpty();
    resolved.Territory.ApplyDefaultsWhereEmpty();
    resolved.CityMarkets.ApplyDefaultsWhereEmpty(resolved.Territory.Cities());
    return resolved;
}

/// <summary>
/// Options with a single, explicitly sized storage room, so capacity rule tests do not break every
/// time the shipped storage table is retuned.
/// </summary>
static GameOptions StorageCapOptions(int condoms)
    => new()
    {
        Hideout = new HideoutOptions
        {
            Storage = [new StorageLevelOptions { Level = 1, Condoms = condoms, Beer = 10, Weapons = 5, Weed = 25, Coke = 10 }]
        }
    };

static IOptionsSnapshot<GameOptions> Snapshot(GameOptions options) => new OptionsSnapshotStub<GameOptions>(options);

static IOptions<T> Options<T>(T value) where T : class => new OptionsSnapshotStub<T>(value);

static void AssertRuleError(Action action, string expectation)
{
    try
    {
        action();
    }
    catch (GameRuleException)
    {
        return;
    }

    throw new InvalidOperationException($"Expected a rule error when {expectation}.");
}

static CombatService CreateCombat(GameOptions? options = null)
    => new(Snapshot(options ?? new GameOptions()), new MinimumRandom());

static FindTableOptions NoFinds() => new()
{
    Condoms = new FindOptions(0, 1, 1),
    Beer = new FindOptions(0, 1, 1),
    Weed = new FindOptions(0, 1, 1),
    Coke = new FindOptions(0, 1, 1)
};

static IReadOnlyDictionary<string, object?> RequiredBreakdown(ActionResultResponse result)
    => result.Breakdown ?? throw new InvalidOperationException("Expected an action breakdown.");

static T Value<T>(IReadOnlyDictionary<string, object?> values, string key)
{
    if (!values.TryGetValue(key, out var value) || value is null)
        throw new InvalidOperationException($"Expected breakdown value '{key}'.");

    return (T)Convert.ChangeType(value, typeof(T))!;
}

static void AssertEqual<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"Expected {expected}, got {actual}.");
}

static void AssertTrue(bool value, string message)
{
    if (!value)
        throw new InvalidOperationException(message);
}

/// <summary>
/// Just enough of a hosting environment to answer IsDevelopment(), which is the one question the
/// return-url guard asks it.
/// </summary>
sealed class HostingStub(string environmentName) : IWebHostEnvironment
{
    public string EnvironmentName { get; set; } = environmentName;
    public string ApplicationName { get; set; } = "StreetEmpire.Tests";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public string WebRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
}

/// <summary>Keeps what it was given instead of sending it.</summary>
sealed class RecordingEmailSender : IEmailSender
{
    public List<EmailMessage> Messages { get; } = [];
    public bool Delivers => true;

    public Task<bool> SendAsync(EmailMessage message, CancellationToken ct)
    {
        Messages.Add(message);
        return Task.FromResult(true);
    }
}

/// <summary>A provider having a bad day, which must never become the caller's problem.</summary>
sealed class ThrowingEmailSender : IEmailSender
{
    public bool Delivers => true;
    public Task<bool> SendAsync(EmailMessage message, CancellationToken ct)
        => throw new HttpRequestException("the provider is down");
}

/// <summary>Stands in for the scoped IOptionsSnapshot the services now take.</summary>
sealed class OptionsSnapshotStub<T>(T value) : IOptionsSnapshot<T> where T : class
{
    public T Value => value;
    public T Get(string? name) => value;
}

sealed class AlwaysRandom : IGameRandom
{
    public int NextInclusive(int min, int max) => min;
    public double NextDouble() => 0;
}

/// <summary>Rolls exactly what it is told, for testing a threshold from both sides.</summary>
sealed class FixedRandom(double value) : IGameRandom
{
    public int NextInclusive(int min, int max) => min;
    public double NextDouble() => value;
}

sealed class MinimumRandom : IGameRandom
{
    public int NextInclusive(int min, int max) => min;
    public double NextDouble() => 1;
}
