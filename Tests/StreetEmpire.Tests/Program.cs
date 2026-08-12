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
    ("combat blocks self attacks", CombatBlocksSelfAttacks),
    ("combat blocks protected defenders", CombatBlocksProtectedDefenders),
    ("combat start creates pending mission", CombatStartCreatesPendingMission),
    ("combat commitment calculates available crew", CombatCommitmentCalculatesAvailableCrew),
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
    var service = new TurnService(Options.Create(new GameOptions
    {
        TurnsPerTick = 2,
        TurnTickMinutes = 10,
        MaxTurns = 20
    }));
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
    var service = new TurnService(Options.Create(new GameOptions
    {
        TurnsPerTick = 2,
        TurnTickMinutes = 10,
        MaxTurns = 20,
        Morale = new MoraleOptions { PassiveRecoveryPerTick = 0.5 }
    }));
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
    => new(Options.Create(options ?? new GameOptions()), new MinimumRandom());

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

sealed class MinimumRandom : IGameRandom
{
    public int NextInclusive(int min, int max) => min;
    public double NextDouble() => 1;
}
