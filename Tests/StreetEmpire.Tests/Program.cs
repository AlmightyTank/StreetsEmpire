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
        Options.Create(resolved),
        new MinimumRandom(),
        new HideoutService(Options.Create(resolved)),
        new PimpRoster(Options.Create(resolved), new MinimumRandom()));
}

static TurnService CreateTurns(GameOptions? options = null)
{
    var resolved = Resolve(options);
    return new TurnService(Options.Create(resolved), new PimpRoster(Options.Create(resolved), new MinimumRandom()));
}

static PimpRoster CreateRoster(GameOptions? options = null, IGameRandom? random = null)
    => new(Options.Create(Resolve(options)), random ?? new MinimumRandom());

static HideoutService CreateHideouts(GameOptions? options = null)
    => new(Options.Create(Resolve(options)));

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
    => new(Options.Create(options ?? new GameOptions()), new MinimumRandom());

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
