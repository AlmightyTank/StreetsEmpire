using Microsoft.Extensions.Options;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Models;
using StreetEmpire.Api.Services;

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
    ("pimp roster stays in step with the pimp counter", PimpRosterStaysInStepWithCounter),
    ("pimp specialties bonus the right activity", PimpSpecialtiesBonusTheRightActivity),
    ("pimp commander selection honours the request", PimpCommanderSelectionHonoursRequest),
    ("pimp commander dies on a bad defeat", PimpCommanderDiesOnDefeat),
    ("pimp walks out when loyalty bottoms out", PimpWalksOutWhenLoyaltyBottomsOut),
    ("hideout caps crew hiring at the tier limit", HideoutCapsCrewHiring),
    ("hideout blocks store buys that would overflow storage", HideoutBlocksOverflowingStoreBuys),
    ("hideout banks cash over the safe and spills goods", HideoutBanksCashOverSafeAndSpillsGoods),
    ("hideout grandfathers stock a player already held", HideoutGrandfathersExistingStock),
    ("hideout lab raises production yield", HideoutLabRaisesProductionYield),
    ("hideout tier build charges up front and lands on time", HideoutTierBuildChargesUpFrontAndLandsOnTime),
    ("hideout tier gates the rooms it is too small to hold", HideoutTierGatesDeeperRooms),
    ("storage levels hold a full action at the crew caps they unlock", StorageLevelsMatchTheCrewCapsTheyUnlock),
    ("labs produce while away, bounded by storage and the offline ceiling", LabsProduceWhileAway),
    ("labs start their clock when built rather than backdating", LabsStartTheirClockWhenBuilt),
    ("world news keeps fights and drops routine noise", WorldNewsKeepsFightsAndDropsNoise),
    ("account lockout blocks banned and suspended players", AccountLockoutBlocksBannedAndSuspended),
    ("wealth stats describe the distribution", WealthStatsDescribeTheDistribution),
    ("option paths discover and write scalar tuning", OptionPathsDiscoverAndWriteScalars),
    ("option overrides layer over appsettings values", OptionOverridesLayerOverAppsettings),
    ("anti-farm refuses mismatched fights", AntiFarmRefusesMismatchedFights),
    ("anti-farm decays loot for repeat victories", AntiFarmDecaysRepeatLoot),
    ("anti-farm widens protection under repeated hits", AntiFarmWidensProtection),
    ("bot targeting picks the richest beatable target", BotTargetingPicksRichestBeatable),
    ("bot attack profiles scale with personality", BotAttackProfilesScaleWithPersonality),
    ("defence alerts flip the outcome to the defender's view", DefenceAlertsFlipPerspective),
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
        MaxTurns = 20
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
    AssertTrue(!hideouts.NextUpgrade(player.Hideout, "storage")!.TierLocked, "the Row House holds a level 4 room");
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

    AssertEqual("combat", WorldNews.Category("ATTACK"));
    AssertEqual("build", WorldNews.Category("HIDEOUT"));
    AssertEqual("money", WorldNews.Category("SALE"));
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
    AssertEqual(1, DefenceAlerts.UnreadCount([older, exactlyAtWatermark, newer]));

    // A player who has never opened their alerts sees all of them as unread.
    var neverLooked = new[] { At(1, seen.AddDays(-9)), At(2, seen) }
        .Select(x => DefenceAlerts.Describe(x, null))
        .ToList();
    AssertEqual(2, DefenceAlerts.UnreadCount(neverLooked));
    AssertEqual(0, DefenceAlerts.UnreadCount([]));
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

static EconomyService CreateEconomy(GameOptions? options = null)
{
    var resolved = Resolve(options);
    return new EconomyService(
        Snapshot(resolved),
        new MinimumRandom(),
        new HideoutService(Snapshot(resolved)),
        new PimpRoster(Snapshot(resolved), new MinimumRandom()));
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
