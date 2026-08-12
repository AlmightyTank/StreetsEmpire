using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

public sealed class BotSimulationService(
    GameDbContext db,
    EconomyService economy,
    TurnService turns,
    IGameRandom random,
    IOptions<GameOptions> options)
{
    private readonly GameOptions _options = options.Value;

    public async Task<BotSimulationResult> RunAsync(int requestedRounds, CancellationToken ct)
    {
        var rounds = Math.Clamp(requestedRounds, 1, 10);
        var bots = await db.Players
            .Include(x => x.Account)
            .Where(x => x.Account.IsBot)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(ct);
        var botIds = bots.Select(x => x.Id).ToList();
        var lastBotActivityTimes = await db.ActionLogs.AsNoTracking()
            .Where(x => botIds.Contains(x.PlayerId))
            .GroupBy(x => x.PlayerId)
            .Select(x => new { PlayerId = x.Key, LastActionAtUtc = x.Max(a => a.CreatedAtUtc) })
            .ToDictionaryAsync(x => x.PlayerId, x => x.LastActionAtUtc, ct);
        var actions = 0;
        var activeBotRounds = 0;
        var activeBotIds = new HashSet<Guid>();

        for (var round = 0; round < rounds; round++)
        {
            foreach (var bot in bots)
            {
                var nowUtc = DateTime.UtcNow;
                var brain = BotBrain.For(bot);
                var lastActivityAtUtc = lastBotActivityTimes.TryGetValue(bot.Id, out var lastActionAtUtc)
                    ? lastActionAtUtc
                    : bot.CreatedAtUtc;
                var nextEligibleAtUtc = lastActivityAtUtc.AddMinutes(random.NextInclusive(brain.MinCooldownMinutes, brain.MaxCooldownMinutes));
                if (nextEligibleAtUtc > nowUtc)
                    continue;

                turns.Refresh(bot, nowUtc);
                var botActions = RunBotRound(bot, brain, nowUtc);
                if (botActions > 0)
                {
                    lastBotActivityTimes[bot.Id] = nowUtc;
                    activeBotIds.Add(bot.Id);
                    activeBotRounds++;
                    actions += botActions;
                }
            }
        }

        await db.SaveChangesAsync(ct);
        return new BotSimulationResult(bots.Count, activeBotIds.Count, activeBotRounds, actions, rounds);
    }

    private int RunBotRound(Player bot, BotBrain brain, DateTime actionTimeUtc)
    {
        if (random.NextDouble() < brain.IdleChance)
            return 0;

        var action = TrySellProduct(bot, brain, actionTimeUtc);
        if (action > 0) return action;

        return brain.Focus switch
        {
            BotBrainFocus.ResourceManager => FirstAction(
                () => TryManageCrewMorale(bot, brain, actionTimeUtc),
                () => TryBuySupplies(bot, brain, actionTimeUtc),
                () => TryDeposit(bot, brain, actionTimeUtc),
                () => TryHireCrew(bot, brain, actionTimeUtc),
                () => TryMajorTurnAction(bot, brain, actionTimeUtc)),
            BotBrainFocus.BigSpender => FirstAction(
                () => TryHireCrew(bot, brain, actionTimeUtc),
                () => TryMajorTurnAction(bot, brain, actionTimeUtc),
                () => TryBuySupplies(bot, brain, actionTimeUtc),
                () => TryManageCrewMorale(bot, brain, actionTimeUtc),
                () => TryDeposit(bot, brain, actionTimeUtc)),
            BotBrainFocus.MoraleNeglecter => FirstAction(
                () => TryMajorTurnAction(bot, brain, actionTimeUtc),
                () => TryHireCrew(bot, brain, actionTimeUtc),
                () => TryBuySupplies(bot, brain, actionTimeUtc),
                () => TryManageCrewMorale(bot, brain, actionTimeUtc),
                () => TryDeposit(bot, brain, actionTimeUtc)),
            BotBrainFocus.ProductRunner => FirstAction(
                () => TryProduce(bot, brain, actionTimeUtc),
                () => TryBuySupplies(bot, brain, actionTimeUtc),
                () => TryMajorTurnAction(bot, brain, actionTimeUtc),
                () => TryManageCrewMorale(bot, brain, actionTimeUtc),
                () => TryDeposit(bot, brain, actionTimeUtc),
                () => TryHireCrew(bot, brain, actionTimeUtc)),
            BotBrainFocus.CrewBuilder => FirstAction(
                () => TryHireCrew(bot, brain, actionTimeUtc),
                () => TryBuySupplies(bot, brain, actionTimeUtc),
                () => TryManageCrewMorale(bot, brain, actionTimeUtc),
                () => TryMajorTurnAction(bot, brain, actionTimeUtc),
                () => TryDeposit(bot, brain, actionTimeUtc)),
            BotBrainFocus.Banker => FirstAction(
                () => TryDeposit(bot, brain, actionTimeUtc),
                () => TrySellProduct(bot, brain, actionTimeUtc),
                () => TryBuySupplies(bot, brain, actionTimeUtc),
                () => TryManageCrewMorale(bot, brain, actionTimeUtc),
                () => TryMajorTurnAction(bot, brain, actionTimeUtc),
                () => TryHireCrew(bot, brain, actionTimeUtc)),
            _ => FirstAction(
                () => TryManageCrewMorale(bot, brain, actionTimeUtc),
                () => TryBuySupplies(bot, brain, actionTimeUtc),
                () => TryHireCrew(bot, brain, actionTimeUtc),
                () => TryMajorTurnAction(bot, brain, actionTimeUtc),
                () => TryDeposit(bot, brain, actionTimeUtc))
        };
    }

    private static int FirstAction(params Func<int>[] actions)
    {
        foreach (var action in actions)
        {
            var result = action();
            if (result > 0)
                return result;
        }

        return 0;
    }

    private int TrySellProduct(Player bot, BotBrain brain, DateTime actionTimeUtc)
    {
        var cokeThreshold = Math.Max(1, (int)Math.Round(10 * brain.ProductSellThresholdMultiplier));
        var weedThreshold = Math.Max(1, (int)Math.Round(25 * brain.ProductSellThresholdMultiplier));
        if (bot.Coke >= cokeThreshold || (bot.Cash < brain.EmergencyCashThreshold && bot.Coke > 0))
            return TryAction(bot, "SALE", 0, actionTimeUtc, () => economy.SellProduct(bot, "coke", Math.Min(bot.Coke, brain.MaxCokeSaleQuantity)));

        if (bot.Weed >= weedThreshold || (bot.Cash < brain.EmergencyCashThreshold && bot.Weed > 0))
            return TryAction(bot, "SALE", 0, actionTimeUtc, () => economy.SellProduct(bot, "weed", Math.Min(bot.Weed, brain.MaxWeedSaleQuantity)));

        return 0;
    }

    private int TryManageCrewMorale(Player bot, BotBrain brain, DateTime actionTimeUtc)
    {
        if (random.NextDouble() > brain.MoraleActionChance)
            return 0;

        var crew = _options.Crew;
        var report = economy.GetCrewReport(bot);

        if (bot.HoeHappiness < brain.RaiseHoeCutBelow && bot.HoeCutPercent < brain.HighHoeCutPercent)
        {
            var hoeCutPercent = Math.Min(brain.MaxHoeCutPercent, Math.Max(brain.HighHoeCutPercent, bot.HoeCutPercent + brain.HoeCutStep));
            return TryAction(bot, "CREW", 0, actionTimeUtc, () => economy.UpdateCrewSettings(bot, hoeCutPercent));
        }

        if (bot.HoeHappiness > brain.LowerHoeCutAbove && bot.HoeCutPercent > brain.LowHoeCutPercent && bot.Condoms >= report.CondomsNeededForMaxStreetAction)
        {
            var hoeCutPercent = Math.Max(brain.LowHoeCutPercent, bot.HoeCutPercent - brain.HoeCutStep);
            return TryAction(bot, "CREW", 0, actionTimeUtc, () => economy.UpdateCrewSettings(bot, hoeCutPercent));
        }

        if (report.UnmanagedHoes > brain.UnmanagedHoeTolerance && bot.Cash >= crew.HirePimpCost + CashReserve(bot, brain))
            return TryAction(bot, "CREW", 0, actionTimeUtc, () => economy.HireCrew(bot, "pimps", 1));

        if (bot.HoeHappiness < brain.SupplyMoraleThreshold && bot.Condoms < report.CondomsNeededForMaxStreetAction * brain.RecoverySupplyMultiplier)
        {
            var quantity = AffordableQuantity(
                bot,
                report.CondomsNeededForMaxStreetAction * brain.RecoverySupplyMultiplier - bot.Condoms,
                _options.CondomPrice,
                random.NextInclusive(brain.MinSupplyBuy, brain.MaxSupplyBuy),
                brain);
            if (quantity > 0)
                return TryAction(bot, "STORE", 0, actionTimeUtc, () => economy.BuyStoreItem(bot, "condoms", quantity));
        }

        if (bot.ThugHappiness < brain.SupplyMoraleThreshold && bot.Beer < report.BeerNeededForMaxStreetAction * brain.RecoverySupplyMultiplier)
        {
            var quantity = AffordableQuantity(
                bot,
                report.BeerNeededForMaxStreetAction * brain.RecoverySupplyMultiplier - bot.Beer,
                _options.BeerPrice,
                random.NextInclusive(brain.MinSupplyBuy, brain.MaxSupplyBuy),
                brain);
            if (quantity > 0)
                return TryAction(bot, "STORE", 0, actionTimeUtc, () => economy.BuyStoreItem(bot, "beer", quantity));
        }

        if ((bot.ThugHappiness < brain.WeaponMoraleThreshold || report.UncoveredThugs > brain.UncoveredThugTolerance) && report.UncoveredThugs > 0)
        {
            var quantity = AffordableQuantity(bot, report.UncoveredThugs, _options.WeaponPrice, random.NextInclusive(1, brain.MaxWeaponBuy), brain);
            if (quantity > 0)
                return TryAction(bot, "STORE", 0, actionTimeUtc, () => economy.BuyStoreItem(bot, "weapons", quantity));
        }

        return 0;
    }

    private int TryBuySupplies(Player bot, BotBrain brain, DateTime actionTimeUtc)
    {
        var report = economy.GetCrewReport(bot);
        var targetCondoms = (int)Math.Ceiling(report.CondomsNeededForMaxStreetAction * brain.SupplyTargetMultiplier);
        var targetBeer = (int)Math.Ceiling(report.BeerNeededForMaxStreetAction * brain.SupplyTargetMultiplier);

        if (bot.Condoms < targetCondoms)
        {
            var quantity = AffordableQuantity(bot, targetCondoms - bot.Condoms, _options.CondomPrice, random.NextInclusive(brain.MinSupplyBuy, brain.MaxSupplyBuy), brain);
            return TryAction(bot, "STORE", 0, actionTimeUtc, () => economy.BuyStoreItem(bot, "condoms", quantity));
        }

        if (bot.Beer < targetBeer)
        {
            var quantity = AffordableQuantity(bot, targetBeer - bot.Beer, _options.BeerPrice, random.NextInclusive(brain.MinSupplyBuy, brain.MaxSupplyBuy), brain);
            return TryAction(bot, "STORE", 0, actionTimeUtc, () => economy.BuyStoreItem(bot, "beer", quantity));
        }

        var targetWeapons = Math.Max(0, (int)Math.Ceiling(bot.Thugs * brain.WeaponCoverageTarget));
        if (bot.Weapons < targetWeapons)
        {
            var quantity = AffordableQuantity(bot, targetWeapons - bot.Weapons, _options.WeaponPrice, random.NextInclusive(1, brain.MaxWeaponBuy), brain);
            return TryAction(bot, "STORE", 0, actionTimeUtc, () => economy.BuyStoreItem(bot, "weapons", quantity));
        }

        return 0;
    }

    private int TryHireCrew(Player bot, BotBrain brain, DateTime actionTimeUtc)
    {
        var actions = 0;
        var crew = _options.Crew;
        var report = economy.GetCrewReport(bot);

        if (bot.HoeHappiness < brain.HireMoraleThreshold || bot.ThugHappiness < brain.HireMoraleThreshold)
            return 0;

        if (bot.Hoes >= report.ManagementCapacity - brain.ManagementBuffer && bot.Cash >= crew.HirePimpCost * brain.HireCashMultiplier)
            return TryAction(bot, "CREW", 0, actionTimeUtc, () => economy.HireCrew(bot, "pimps", 1));

        if (bot.HoeHappiness >= crew.MinHoeMoraleToHire && bot.Cash >= crew.HireHoeCost * brain.HireCashMultiplier)
        {
            var room = Math.Max(1, economy.GetCrewReport(bot).ManagementCapacity - bot.Hoes);
            var quantity = Math.Clamp(Math.Min(room, random.NextInclusive(1, brain.MaxHoeHireBatch)), 1, brain.MaxHoeHireBatch);
            return TryAction(bot, "CREW", 0, actionTimeUtc, () => economy.HireCrew(bot, "hoes", quantity));
        }

        var targetThugs = Math.Max(brain.MinThugs, bot.Hoes / brain.HoesPerThugTarget);
        if (bot.ThugHappiness >= crew.MinThugMoraleToHire && bot.Cash >= crew.HireThugCost * brain.HireCashMultiplier && bot.Thugs < targetThugs)
        {
            var quantity = Math.Clamp(targetThugs - bot.Thugs, 1, brain.MaxThugHireBatch);
            return TryAction(bot, "CREW", 0, actionTimeUtc, () => economy.HireCrew(bot, "thugs", quantity));
        }

        return actions;
    }

    private int TryMajorTurnAction(Player bot, BotBrain brain, DateTime actionTimeUtc)
    {
        if (AvailableTurnBudget(bot, brain) <= 0 || random.NextDouble() < brain.TurnActionSkipChance)
            return 0;
        if (NeedsMoraleRecovery(bot, brain))
            return 0;

        var wantsProduction = bot.Cash > brain.ProductionCashMinimum
            && (bot.Weed + bot.Coke < 60 || bot.Cash > 15_000)
            && random.NextDouble() < brain.ProductionChance;

        if (wantsProduction)
        {
            var produced = TryProduce(bot, brain, actionTimeUtc);
            if (produced > 0)
                return produced;
        }

        return TryWorkStreet(bot, brain, actionTimeUtc);
    }

    private int TryProduce(Player bot, BotBrain brain, DateTime actionTimeUtc)
    {
        var availableTurns = AvailableTurnBudget(bot, brain);
        if (availableTurns <= 0 || bot.Cash < brain.ProductionCashMinimum)
            return 0;

        var product = bot.Cash > brain.CokeCashThreshold && random.NextDouble() < brain.CokePreference ? "coke" : "weed";
        var costPerTurn = product == "coke"
            ? _options.Production.Coke.CostPerTurn
            : _options.Production.Weed.CostPerTurn;
        var turnsToSpend = Math.Min(availableTurns, random.NextInclusive(brain.MinProductionTurns, brain.MaxProductionTurns));
        turnsToSpend = Math.Min(turnsToSpend, (int)Math.Max(0, (bot.Cash - CashReserve(bot, brain)) / costPerTurn));
        if (turnsToSpend <= 0)
            return 0;

        return TryAction(bot, "PRODUCTION", turnsToSpend, actionTimeUtc, () => economy.Produce(bot, product, turnsToSpend));
    }

    private int TryWorkStreet(Player bot, BotBrain brain, DateTime actionTimeUtc)
    {
        var availableTurns = AvailableTurnBudget(bot, brain);
        if (availableTurns <= 0)
            return 0;

        var turnsToSpend = Math.Min(availableTurns, random.NextInclusive(brain.MinStreetTurns, brain.MaxStreetTurns));
        if (bot.HoeHappiness < brain.LowMoraleStreetThreshold || bot.ThugHappiness < brain.LowMoraleStreetThreshold)
            turnsToSpend = Math.Min(turnsToSpend, random.NextInclusive(1, brain.LowMoraleMaxStreetTurns));

        return TryAction(bot, "STREET", turnsToSpend, actionTimeUtc, () => economy.Scout(bot, turnsToSpend));
    }

    private int TryDeposit(Player bot, BotBrain brain, DateTime actionTimeUtc)
    {
        var reserve = Math.Max(3_000, (long)(bot.Hoes + bot.Thugs) * 250);
        var excess = bot.Cash - reserve;
        if (excess < brain.DepositMinimumExcess)
            return 0;

        return TryAction(bot, "BANK", 0, actionTimeUtc, () => economy.Deposit(bot, Math.Max(1, (long)Math.Round(excess * brain.DepositShare))));
    }

    private int AvailableTurnBudget(Player bot, BotBrain brain)
        => Math.Min(_options.MaxActionTurns, Math.Max(0, bot.Turns - TurnReserve(brain)));

    private int TurnReserve(BotBrain brain)
        => Math.Clamp((int)Math.Round(Math.Clamp(_options.MaxTurns / 5, 20, 50) * brain.TurnReserveMultiplier), 5, _options.MaxTurns);

    private long CashReserve(Player bot, BotBrain brain)
        => Math.Max(500, (long)Math.Round(Math.Max(2_500, (long)(bot.Hoes + bot.Thugs) * 250) * brain.CashReserveMultiplier));

    private bool NeedsMoraleRecovery(Player bot, BotBrain brain)
    {
        var report = economy.GetCrewReport(bot);
        return bot.HoeHappiness < brain.MoraleRecoveryThreshold
            || bot.ThugHappiness < brain.MoraleRecoveryThreshold
            || report.UnmanagedHoes > brain.UnmanagedHoeTolerance
            || report.UncoveredThugs > brain.UncoveredThugTolerance
            || bot.Condoms < report.CondomsNeededForMaxStreetAction * brain.MinimumSupplyCoverage
            || bot.Beer < report.BeerNeededForMaxStreetAction * brain.MinimumSupplyCoverage;
    }

    private int AffordableQuantity(Player bot, int requested, int unitPrice, int cap, BotBrain brain)
    {
        if (requested <= 0 || unitPrice <= 0)
            return 0;

        var affordable = (int)Math.Min(int.MaxValue, Math.Max(0, bot.Cash - CashReserve(bot, brain)) / unitPrice);
        return Math.Min(requested, Math.Min(cap, affordable));
    }

    private int TryAction(Player bot, string action, int turnsSpent, DateTime actionTimeUtc, Func<ActionResultResponse> resolve)
    {
        var before = Snapshot(bot);
        try
        {
            var result = resolve();
            AddLog(bot, before, action, turnsSpent, actionTimeUtc, $"AI: {result.Summary}");
            return 1;
        }
        catch (GameRuleException)
        {
            return 0;
        }
    }

    private void AddLog(Player bot, PlayerSnapshot before, string action, int turnsSpent, DateTime actionTimeUtc, string summary)
    {
        db.ActionLogs.Add(new GameActionLog
        {
            Player = bot,
            Action = action,
            TurnsSpent = turnsSpent,
            CashDelta = bot.Cash - before.Cash,
            BankDelta = bot.BankCash - before.BankCash,
            PimpsDelta = bot.Pimps - before.Pimps,
            HoesDelta = bot.Hoes - before.Hoes,
            ThugsDelta = bot.Thugs - before.Thugs,
            CondomsDelta = bot.Condoms - before.Condoms,
            BeerDelta = bot.Beer - before.Beer,
            WeaponsDelta = bot.Weapons - before.Weapons,
            WeedDelta = bot.Weed - before.Weed,
            CokeDelta = bot.Coke - before.Coke,
            CreatedAtUtc = actionTimeUtc,
            Summary = summary
        });
    }

    private static PlayerSnapshot Snapshot(Player player) => new(
        player.Cash,
        player.BankCash,
        player.Pimps,
        player.Hoes,
        player.Thugs,
        player.Condoms,
        player.Beer,
        player.Weapons,
        player.Weed,
        player.Coke);

    private sealed record PlayerSnapshot(
        long Cash,
        long BankCash,
        int Pimps,
        int Hoes,
        int Thugs,
        int Condoms,
        int Beer,
        int Weapons,
        int Weed,
        int Coke);

}

public sealed record BotSimulationResult(int TotalBots, int ActiveBots, int ActiveBotRounds, int Actions, int Rounds);
