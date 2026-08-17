using Microsoft.Extensions.Options;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Models;
using StreetEmpire.Api.Services;
using static StreetEmpire.Api.Mapping.ResponseMappers;

var tests = new (string Name, Action Test)[]
{
    ("net worth includes all liquid and inventory value", NetWorthIncludesAllValue),
    ("net worth expression agrees with the net worth calculation", NetWorthExpressionAgreesWithCalculation),
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
    ("territory effects add up across the ground held", TerritoryEffectsAddUp),
    ("a pimp posted to ground only helps if they fight", GarrisonPimpBonusOnlyForEnforcers),
    ("ground bonuses reach the activities they boost", TerritoryBonusesReachTheirActivities),
    ("hideout tier build charges up front and lands on time", HideoutTierBuildChargesUpFrontAndLandsOnTime),
    ("hideout tier gates the rooms it is too small to hold", HideoutTierGatesDeeperRooms),
    ("storage levels hold a full action at the crew caps they unlock", StorageLevelsMatchTheCrewCapsTheyUnlock),
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
    ("combat attack spends turns and creates log", CombatAttackSpendsTurnsAndCreatesLog)
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
        CokeNetWorth = 120
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
        Weapons = 6,
        Weed = 7,
        Coke = 8
    };

    AssertEqual(10_435, service.CalculateNetWorth(player));
}

// The database sorts and counts by the expression while the API reports the method's value, so a
// change to one that misses the other would silently disagree with the leaderboard.
static void NetWorthExpressionAgreesWithCalculation()
{
    var service = CreateEconomy();
    var players = new[]
    {
        new Player(),
        new Player { Cash = 5_000, Pimps = 1, Hoes = 3, Thugs = 1, Condoms = 25, Beer = 12, Weapons = 1 },
        new Player
        {
            Cash = 1_234,
            BankCash = 98_765,
            Pimps = 7,
            Hoes = 41,
            Thugs = 19,
            Condoms = 310,
            Beer = 225,
            Weapons = 17,
            Weed = 88,
            Coke = 46
        }
    };

    var compiled = service.NetWorthExpression.Compile();
    foreach (var player in players)
        AssertEqual(service.CalculateNetWorth(player), compiled(player));
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
        Weapons = 1,
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
    var player = new Player { Pimps = 2, Hoes = 23, Thugs = 7, Weapons = 5 };

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

    // Storage is the other ceiling: a level 1 room caps the top-up at what fits.
    var cramped = new Player { Turns = 20, Cash = 10_000, Hoes = 50, Thugs = 5, Hideout = new Hideout { StorageLevel = 1 } };
    var crampedBreakdown = RequiredBreakdown(service.Scout(cramped, 20, autoBuySupplies: true));

    AssertEqual(17, Value<int>(crampedBreakdown, "autoBoughtCondoms"));

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

    // A level 3 room holds 84 condoms, which carries 50 hoes through a 20 turn shift and no more.
    var supplied = new Player { Pimps = 6, Hoes = 50, Thugs = 25, Hideout = new Hideout { StorageLevel = 3 } };
    var fine = service.GetCrewReport(supplied);
    AssertEqual(50, fine.HoesStorageCanSupply);
    AssertEqual(25, fine.ThugsStorageCanSupply);
    AssertTrue(fine.StorageLevelToSupplyCrew is null, "a room that already covers the crew needs no upgrade named");

    // One hoe past it and the room is the constraint, not the stock on the shelf.
    var stretched = new Player { Pimps = 6, Hoes = 59, Thugs = 25, Condoms = 84, Hideout = new Hideout { StorageLevel = 3 } };
    var warned = service.GetCrewReport(stretched);
    AssertEqual(50, warned.HoesStorageCanSupply);
    AssertEqual(4, warned.StorageLevelToSupplyCrew ?? 0);

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
        Hideout = new Hideout()
    };

    // Trap House holds 50 hoes, so only two more fit.
    service.HireCrew(player, "hoes", 2);
    AssertEqual(50, player.Hoes);

    AssertRuleError(() => service.HireCrew(player, "hoes", 1), "hiring past the hideout cap");
    AssertEqual(50, player.Hoes);
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
    var withLab = new Player { Cash = 10_000, Turns = 20, Hideout = new Hideout { StorageLevel = 3, WeedLabLevel = 3 } };

    var plain = service.Produce(withoutLab, "weed", 5);
    var boosted = service.Produce(withLab, "weed", 5);

    // MinimumRandom always rolls the low end, so five turns is a flat 20 units before the lab.
    AssertEqual(20, Value<int>(RequiredBreakdown(plain), "baseUnits"));
    AssertEqual(20, Value<int>(RequiredBreakdown(boosted), "baseUnits"));
    AssertEqual(110, Value<int>(RequiredBreakdown(boosted), "labBonusPercent"));
    AssertEqual(42, Value<int>(RequiredBreakdown(boosted), "unitsProduced"));
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
        Hideout = new Hideout()
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
    AssertEqual(tier2.MaxHoes, hideouts.CapacityFor(player.Hideout).MaxHoes);
}

static void HideoutTierGatesDeeperRooms()
{
    var hideouts = CreateHideouts();
    var player = new Player { Cash = 5_000_000, Hideout = new Hideout { StorageLevel = 3 } };

    var locked = hideouts.NextUpgrade(player.Hideout, "storage");
    AssertEqual(4, locked!.Level);
    AssertTrue(locked.TierLocked, "a level 4 storage room needs a bigger building");
    AssertRuleError(() => hideouts.Upgrade(player, "storage", DateTime.UtcNow), "upgrading a room past the tier");
    AssertEqual(3, player.Hideout!.StorageLevel);
    AssertEqual(5_000_000L, player.Cash);

    player.Hideout.Tier = 2;
    AssertTrue(!hideouts.NextUpgrade(player.Hideout, "storage")!.TierLocked, "the second tier holds a level 4 room");
    hideouts.Upgrade(player, "storage", DateTime.UtcNow);
    AssertEqual(4, player.Hideout.StorageLevel);
}

/// <summary>
/// The rule the storage table is built on: every level that a tier unlocks holds exactly what a
/// full-length street action consumes at that tier's crew caps. Without this the tables drift apart
/// silently and a maxed player finds they cannot supply the crew their building allows.
/// </summary>
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

    var rooms = new[] { "storage", "safe", "weedlab", "cokelab" };
    var topTier = options.Hideout.Tiers.Max(x => x.Level);

    for (var tier = 1; ; tier++)
    {
        foreach (var room in rooms)
            while (hideouts.NextUpgrade(player.Hideout, room) is { TierLocked: false })
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
    // Level 1 weed lab makes 2 an hour; storage level 3 holds 100 weed.
    var player = new Player
    {
        Hideout = new Hideout { StorageLevel = 3, WeedLabLevel = 1, LabsCollectedAtUtc = start }
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
    var player = new Player { Hideout = new Hideout { StorageLevel = 3, WeedLabLevel = 3, CreatedAtUtc = built.AddDays(-30) } };

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
    var player = new Player { Condoms = 1, Beer = 2, Weapons = 3, Weed = 4, Coke = 5 };
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
    foreach (var level in options.Hideout.Workshop)
        AssertTrue(level.CostPerWeapon < options.WeaponPrice,
            $"workshop level {level.Level} costs {level.CostPerWeapon} against a store price of {options.WeaponPrice}");

    var service = CreateEconomy(options);
    var maker = new Player { Turns = 20, Cash = 100_000, Hideout = new Hideout { StorageLevel = 3, WorkshopLevel = 1 } };
    var made = service.Forge(maker, 5);
    AssertEqual(5, Value<int>(RequiredBreakdown(made), "weaponsMade"));
    AssertEqual(5, maker.Weapons);
    AssertEqual(15, maker.Turns);

    // Bounded by the room up front rather than made and spilled, so nobody pays for nothing.
    var cramped = new Player { Turns = 20, Cash = 100_000, Weapons = 24, Hideout = new Hideout { StorageLevel = 3, WorkshopLevel = 1 } };
    var partial = service.Forge(cramped, 10);
    AssertEqual(1, Value<int>(RequiredBreakdown(partial), "weaponsMade"));
    AssertEqual(25, cramped.Weapons);
    AssertTrue(partial.Summary.Contains("Storage filled up"), "a short run says why");

    // No workshop, no weapons.
    AssertRuleError(() => service.Forge(new Player { Turns = 20, Cash = 100_000, Hideout = new Hideout() }, 5),
        "forging without a workshop");

    // A still and a mix house need the second tier, and the gate holds when making as well as when
    // building: a station built before the gate existed would otherwise keep running under it.
    var hideouts = CreateHideouts(options);
    AssertEqual(2, hideouts.StationRequiredTier("still") ?? 0);
    AssertEqual(2, hideouts.StationRequiredTier("mix") ?? 0);
    AssertTrue(hideouts.StationRequiredTier("workshop") is null, "the workshop is open from the start");

    var trapHouse = new Player { Turns = 20, Cash = 100_000, Hideout = new Hideout { Tier = 1, StorageLevel = 3, StillLevel = 1, MixLevel = 1 } };
    AssertRuleError(() => service.Make(trapHouse, "still", 5), "brewing in a Trap House");
    AssertRuleError(() => service.Make(trapHouse, "mix", 5), "mixing in a Trap House");

    var warehouse = new Player { Turns = 20, Cash = 100_000, Hideout = new Hideout { Tier = 2, StorageLevel = 3, StillLevel = 1, MixLevel = 1 } };
    AssertEqual(20, Value<int>(RequiredBreakdown(service.Make(warehouse, "still", 5)), "unitsMade"));
    AssertEqual(15, Value<int>(RequiredBreakdown(service.Make(warehouse, "mix", 5)), "unitsMade"));
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
    bleeding.Weapons = 0;
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
    swamped.Weapons = 0;
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
    veteran.Weapons = 8;
    veteran.Hideout = new Hideout { Tier = 2, StorageLevel = 3, WeedLabLevel = 2 };
    var finished = guidance.Objectives(veteran, ["STREET", "BANK", "PRODUCTION", "SALE"]);
    AssertTrue(finished.All(x => x.Done),
        $"nothing left to tell a grown empire: {string.Join(", ", finished.Where(x => !x.Done).Select(x => x.Label))}");
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
    Weapons = options.StartingWeapons,
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

    // A mix house is required, and so is something at both ends of the mix.
    var roomless = Stocked(mix: 0, coke: 50, cut: 50);
    AssertRuleError(() => economy.CutCoke(roomless, 5), "You need a mix house to step on it.");
    AssertRuleError(() => economy.CutCoke(Stocked(mix: 1, coke: 50, cut: 0), 5), "no cut to work with");
    AssertRuleError(() => economy.CutCoke(Stocked(mix: 1, coke: 0, cut: 50), 5), "no coke to step on");

    // One cut makes one coke. The cut is spent, the pile grows by the same.
    var player = Stocked(mix: 1, coke: 60, cut: 40);
    var result = economy.CutCoke(player, 4);
    AssertEqual(100, player.Coke);
    AssertEqual(0, player.Cut);
    // Sixty clean plus forty of filler is sixty percent product, and that is what it now sells as.
    AssertEqual(0.6, Math.Round(player.CokePurity, 4));
    AssertTrue(result.Summary.Contains("Stepped on 40 coke"), $"the notice says what happened: {result.Summary}");

    // Only the turns the batch actually needed. Asking for ten on a two-turn batch should not cost
    // eight turns of standing about.
    var quick = Stocked(mix: 1, coke: 100, cut: perTurn);
    var turnsBefore = quick.Turns;
    economy.CutCoke(quick, 10);
    AssertEqual(turnsBefore - 1, quick.Turns);

    // A better mix house works faster, which is the room's second reason to exist.
    var basic = Stocked(mix: 1, coke: 100, cut: 500);
    var better = Stocked(mix: 2, coke: 100, cut: 500);
    economy.CutCoke(basic, 1);
    economy.CutCoke(better, 1);
    AssertEqual(perTurn, 500 - basic.Cut);
    AssertEqual(perTurn * 2, 500 - better.Cut);

    // Never past the walls. Cutting into a full store would destroy cut already paid for, so the
    // batch stops at the room instead of spilling.
    var cramped = Stocked(mix: 1, coke: 60, cut: 200, storage: 1);
    var capacity = CreateHideouts(options).CapacityFor(cramped.Hideout).MaxCoke;
    cramped.Coke = capacity - 3;
    economy.CutCoke(cramped, 20);
    AssertEqual(capacity, cramped.Coke);
    AssertEqual(197, cramped.Cut);

    AssertRuleError(() => economy.CutCoke(Full(options), 5), "no space for more coke");

    static Player Stocked(int mix, int coke, int cut, int storage = 6) => new()
    {
        Turns = 100,
        Coke = coke,
        Cut = cut,
        Hideout = new Hideout { Tier = 2, StorageLevel = storage, MixLevel = mix }
    };

    Player Full(GameOptions opts)
    {
        var player = Stocked(mix: 1, coke: 0, cut: 50, storage: 1);
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

    // Coke draws the most notice per unit and cut the least, despite where cut is made.
    AssertEqual(35.0, hideouts.HeatFor(new Player { Coke = 100 }));
    AssertEqual(25.0, hideouts.HeatFor(new Player { Moonshine = 100 }));
    AssertEqual(10.0, hideouts.HeatFor(new Player { Weed = 100 }));
    AssertEqual(3.0, hideouts.HeatFor(new Player { Cut = 100 }));

    // Sized against the rooms the game ships. A full Warehouse store of coke is worth watching; a
    // whole evening's work by someone holding nothing is not, and it fades before the next one.
    var warehouseStore = hideouts.HeatFor(new Player { Coke = 85 });
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
    var quiet = new Player { Weed = 20, Cash = 10_000 };
    AssertTrue(!hideouts.RollBust(quiet, 24, new AlwaysRandom()).Happened, "a small stash draws nobody");
    AssertEqual(20, quiet.Weed);

    // Over it, a raid takes a share of every pile and fines them for the lot. This stash alone sits
    // just under the floor now, so it takes a day's work on top to draw anyone: which is the point.
    var loaded = new Player { Coke = 40, Weed = 20, Moonshine = 10, Cut = 8, Heat = 20, Cash = 10_000 };
    AssertTrue(hideouts.HeatFor(new Player { Coke = 40, Weed = 20, Moonshine = 10, Cut = 8 }) < config.HeatBustFloor,
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
    var broke = new Player { Coke = 100, Cash = 500 };
    AssertEqual(500L, hideouts.RollBust(broke, 1, new AlwaysRandom()).Fine);
    AssertEqual(0L, broke.Cash);

    // A raid clears the attention it was drawn by, or one bust guarantees the next.
    var raided = new Player { Coke = 100, Heat = 90, Cash = 10_000 };
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

    // Three hoes carry ninety units, and the fare and upkeep are charged for four heads.
    AssertEqual(90, quote.Capacity);
    AssertEqual(4 * 2 * 60L, quote.Fare);
    AssertEqual(34, quote.TripMinutes);
    AssertEqual(quote.CashSent + quote.Fare + quote.Upkeep, quote.TotalCost);
    AssertTrue(quote.Upkeep > 0, "keeping crew away costs something");

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
    AssertEqual(90, run.Capacity);
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
    // Ninety is what three hoes carry, so the load binds long before the money does.
    AssertEqual(90, run.UnitsBought);
    AssertEqual(30_000L - 90 * price, run.CashReturned);
    AssertEqual(90, lucky.Weed);
    AssertEqual(20, lucky.Hoes);
    AssertEqual(30_000L - 90 * price, lucky.Cash);
    AssertEqual(90, settled.UnitsDelivered);
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
    AssertEqual(90 - seizedRun.SeizedUnits, stopped.Weed);
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
    // The player paid for all 90. A run that quietly dropped the rest would read as the price being
    // wrong rather than the room being full, so the notice has to say so.
    AssertEqual(90, overflowing.UnitsBought);
    AssertTrue(overflowing.Summary.Contains($"{90 - room:N0} weed was dumped"),
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
    Capacity = hoes * 30,
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
        var attackAtParity = CombatPower.Attack(1, thugs, thugs, morale, power);
        var defence = CombatPower.Defence(pimps, thugs, thugs, morale, power);
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
    AssertTrue(CombatPower.Attack(1, 10, 10, morale, power) > CombatPower.Attack(1, 10, 0, morale, power),
        "arming the raid helps");
    AssertTrue(CombatPower.Defence(3, 10, 10, morale, power) > CombatPower.Defence(3, 10, 0, morale, power),
        "arming the house helps");

    // Morale counts for both, and more for the defender.
    AssertTrue(CombatPower.Attack(1, 10, 10, 100, power) > CombatPower.Attack(1, 10, 10, 0, power),
        "morale lifts an attack");
    AssertTrue(
        CombatPower.Defence(3, 10, 10, 100, power) - CombatPower.Defence(3, 10, 10, 0, power)
        > CombatPower.Attack(1, 10, 10, 100, power) - CombatPower.Attack(1, 10, 10, 0, power),
        "morale is worth more at home than on the road");

    // The commander bonus scales the whole figure and never drops it below one.
    AssertEqual(CombatPower.Attack(1, 10, 10, morale, power) * 2,
        CombatPower.Attack(1, 10, 10, morale, power, bonusPercent: 100));
    AssertTrue(CombatPower.Attack(0, 0, 0, 0, power) >= 1, "power never falls below one");

    // The ceiling matchup. Under the previous weights a maxed defender needed 34 attacking thugs to
    // crack while the crew cap was 25, so a fully built house was literally unbeatable. Now brute force
    // alone still falls short, and the counterplay is a top Enforcer commanding or catching the crew away.
    var tier = new GameOptions().Hideout;
    tier.ApplyDefaultsWhereEmpty();
    var maxThugs = tier.Tiers[0].MaxThugs;
    var maxPimps = tier.Tiers[0].MaxPimps;
    var bestBonus = new PimpOptions().MaxBonusPercent;

    var fortress = CombatPower.Defence(maxPimps, maxThugs, maxThugs, morale, power);
    var maxedRaid = CombatPower.Attack(1, maxThugs, maxThugs, morale, power);
    AssertTrue(maxedRaid < fortress, "a full raid alone should not crack a fully built house");
    AssertTrue(CombatPower.Attack(1, maxThugs, maxThugs, morale, power, bestBonus) >= fortress,
        "a top Enforcer commander should bring a full raid level with a fully built house");

    // And a house with crew out attacking is beatable without any commander bonus at all.
    var stretched = CombatPower.Defence(maxPimps, maxThugs - 5, maxThugs - 5, morale, power);
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
    var player = new Player { Pimps = 3, Thugs = 20, Weapons = 15 };
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
        Weapons = 20,
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
        Weapons = 1,
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

/// <summary>Mirrors the API's PostConfigure step, which fills hideout tables config left empty.</summary>
static GameOptions Resolve(GameOptions? options)
{
    var resolved = options ?? new GameOptions();
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

sealed class MinimumRandom : IGameRandom
{
    public int NextInclusive(int min, int max) => min;
    public double NextDouble() => 1;
}
