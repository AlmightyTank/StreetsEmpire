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
                var lastActivityAtUtc = lastBotActivityTimes.TryGetValue(bot.Id, out var lastActionAtUtc)
                    ? lastActionAtUtc
                    : bot.CreatedAtUtc;
                var nextEligibleAtUtc = lastActivityAtUtc.AddMinutes(random.NextInclusive(10, 45));
                if (nextEligibleAtUtc > nowUtc)
                    continue;

                turns.Refresh(bot, nowUtc);
                var botActions = RunBotRound(bot, nowUtc);
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

    private int RunBotRound(Player bot, DateTime actionTimeUtc)
    {
        if (random.NextDouble() < 0.12)
            return 0;

        var action = TrySellProduct(bot, actionTimeUtc);
        if (action > 0) return action;

        action = TryManageCrewMorale(bot, actionTimeUtc);
        if (action > 0) return action;

        action = TryBuySupplies(bot, actionTimeUtc);
        if (action > 0) return action;

        action = TryHireCrew(bot, actionTimeUtc);
        if (action > 0) return action;

        action = TryMajorTurnAction(bot, actionTimeUtc);
        if (action > 0) return action;

        return TryDeposit(bot, actionTimeUtc);
    }

    private int TrySellProduct(Player bot, DateTime actionTimeUtc)
    {
        if (bot.Coke >= 10 || (bot.Cash < 2_000 && bot.Coke > 0))
            return TryAction(bot, "SALE", 0, actionTimeUtc, () => economy.SellProduct(bot, "coke", Math.Min(bot.Coke, 100)));

        if (bot.Weed >= 25 || (bot.Cash < 2_000 && bot.Weed > 0))
            return TryAction(bot, "SALE", 0, actionTimeUtc, () => economy.SellProduct(bot, "weed", Math.Min(bot.Weed, 250)));

        return 0;
    }

    private int TryManageCrewMorale(Player bot, DateTime actionTimeUtc)
    {
        var crew = _options.Crew;
        var report = economy.GetCrewReport(bot);

        if (bot.HoeHappiness < 55 && bot.HoeCutPercent < 65)
        {
            var hoeCutPercent = Math.Min(80, Math.Max(65, bot.HoeCutPercent + 15));
            return TryAction(bot, "CREW", 0, actionTimeUtc, () => economy.UpdateCrewSettings(bot, hoeCutPercent));
        }

        if (bot.HoeHappiness > 88 && bot.HoeCutPercent > 35 && bot.Condoms >= report.CondomsNeededForMaxStreetAction)
        {
            var hoeCutPercent = Math.Max(35, bot.HoeCutPercent - 10);
            return TryAction(bot, "CREW", 0, actionTimeUtc, () => economy.UpdateCrewSettings(bot, hoeCutPercent));
        }

        if (report.UnmanagedHoes > 0 && bot.Cash >= crew.HirePimpCost + CashReserve(bot))
            return TryAction(bot, "CREW", 0, actionTimeUtc, () => economy.HireCrew(bot, "pimps", 1));

        if (bot.HoeHappiness < 70 && bot.Condoms < report.CondomsNeededForMaxStreetAction * 3)
        {
            var quantity = AffordableQuantity(
                bot,
                report.CondomsNeededForMaxStreetAction * 3 - bot.Condoms,
                _options.CondomPrice,
                random.NextInclusive(20, 60));
            if (quantity > 0)
                return TryAction(bot, "STORE", 0, actionTimeUtc, () => economy.BuyStoreItem(bot, "condoms", quantity));
        }

        if (bot.ThugHappiness < 70 && bot.Beer < report.BeerNeededForMaxStreetAction * 3)
        {
            var quantity = AffordableQuantity(
                bot,
                report.BeerNeededForMaxStreetAction * 3 - bot.Beer,
                _options.BeerPrice,
                random.NextInclusive(15, 45));
            if (quantity > 0)
                return TryAction(bot, "STORE", 0, actionTimeUtc, () => economy.BuyStoreItem(bot, "beer", quantity));
        }

        if ((bot.ThugHappiness < 75 || report.UncoveredThugs > 0) && report.UncoveredThugs > 0)
        {
            var quantity = AffordableQuantity(bot, report.UncoveredThugs, _options.WeaponPrice, random.NextInclusive(1, 2));
            if (quantity > 0)
                return TryAction(bot, "STORE", 0, actionTimeUtc, () => economy.BuyStoreItem(bot, "weapons", quantity));
        }

        return 0;
    }

    private int TryBuySupplies(Player bot, DateTime actionTimeUtc)
    {
        var report = economy.GetCrewReport(bot);
        var targetCondoms = report.CondomsNeededForMaxStreetAction * 2;
        var targetBeer = report.BeerNeededForMaxStreetAction * 2;

        if (bot.Condoms < targetCondoms)
        {
            var quantity = AffordableQuantity(bot, targetCondoms - bot.Condoms, _options.CondomPrice, random.NextInclusive(12, 40));
            return TryAction(bot, "STORE", 0, actionTimeUtc, () => economy.BuyStoreItem(bot, "condoms", quantity));
        }

        if (bot.Beer < targetBeer)
        {
            var quantity = AffordableQuantity(bot, targetBeer - bot.Beer, _options.BeerPrice, random.NextInclusive(8, 30));
            return TryAction(bot, "STORE", 0, actionTimeUtc, () => economy.BuyStoreItem(bot, "beer", quantity));
        }

        if (bot.Weapons < bot.Thugs)
        {
            var quantity = AffordableQuantity(bot, bot.Thugs - bot.Weapons, _options.WeaponPrice, random.NextInclusive(1, 3));
            return TryAction(bot, "STORE", 0, actionTimeUtc, () => economy.BuyStoreItem(bot, "weapons", quantity));
        }

        return 0;
    }

    private int TryHireCrew(Player bot, DateTime actionTimeUtc)
    {
        var actions = 0;
        var crew = _options.Crew;
        var report = economy.GetCrewReport(bot);

        if (bot.HoeHappiness < 65 || bot.ThugHappiness < 65)
            return 0;

        if (bot.Hoes >= report.ManagementCapacity && bot.Cash >= crew.HirePimpCost * 2L)
            return TryAction(bot, "CREW", 0, actionTimeUtc, () => economy.HireCrew(bot, "pimps", 1));

        if (bot.HoeHappiness >= crew.MinHoeMoraleToHire && bot.Cash >= crew.HireHoeCost * 4L)
        {
            var room = Math.Max(1, economy.GetCrewReport(bot).ManagementCapacity - bot.Hoes);
            var quantity = Math.Clamp(Math.Min(room, random.NextInclusive(1, 3)), 1, 3);
            return TryAction(bot, "CREW", 0, actionTimeUtc, () => economy.HireCrew(bot, "hoes", quantity));
        }

        if (bot.ThugHappiness >= crew.MinThugMoraleToHire && bot.Cash >= crew.HireThugCost * 3L && bot.Thugs < Math.Max(3, bot.Hoes / 3))
            return TryAction(bot, "CREW", 0, actionTimeUtc, () => economy.HireCrew(bot, "thugs", 1));

        return actions;
    }

    private int TryMajorTurnAction(Player bot, DateTime actionTimeUtc)
    {
        if (AvailableTurnBudget(bot) <= 0 || random.NextDouble() < 0.18)
            return 0;
        if (NeedsMoraleRecovery(bot))
            return 0;

        var wantsProduction = bot.Cash > 5_000
            && (bot.Weed + bot.Coke < 60 || bot.Cash > 15_000)
            && random.NextDouble() < 0.35;

        if (wantsProduction)
        {
            var produced = TryProduce(bot, actionTimeUtc);
            if (produced > 0)
                return produced;
        }

        return TryWorkStreet(bot, actionTimeUtc);
    }

    private int TryProduce(Player bot, DateTime actionTimeUtc)
    {
        var availableTurns = AvailableTurnBudget(bot);
        if (availableTurns <= 0 || bot.Cash < 3_000)
            return 0;

        var product = bot.Cash > 12_000 && random.NextDouble() < 0.65 ? "coke" : "weed";
        var costPerTurn = product == "coke"
            ? _options.Production.Coke.CostPerTurn
            : _options.Production.Weed.CostPerTurn;
        var turnsToSpend = Math.Min(availableTurns, random.NextInclusive(2, 4));
        turnsToSpend = Math.Min(turnsToSpend, (int)Math.Max(0, (bot.Cash - CashReserve(bot)) / costPerTurn));
        if (turnsToSpend <= 0)
            return 0;

        return TryAction(bot, "PRODUCTION", turnsToSpend, actionTimeUtc, () => economy.Produce(bot, product, turnsToSpend));
    }

    private int TryWorkStreet(Player bot, DateTime actionTimeUtc)
    {
        var availableTurns = AvailableTurnBudget(bot);
        if (availableTurns <= 0)
            return 0;

        var turnsToSpend = Math.Min(availableTurns, random.NextInclusive(2, 6));
        if (bot.HoeHappiness < 35 || bot.ThugHappiness < 35)
            turnsToSpend = Math.Min(turnsToSpend, random.NextInclusive(1, 2));

        return TryAction(bot, "STREET", turnsToSpend, actionTimeUtc, () => economy.Scout(bot, turnsToSpend));
    }

    private int TryDeposit(Player bot, DateTime actionTimeUtc)
    {
        var reserve = Math.Max(3_000, (long)(bot.Hoes + bot.Thugs) * 250);
        var excess = bot.Cash - reserve;
        if (excess < 2_500)
            return 0;

        return TryAction(bot, "BANK", 0, actionTimeUtc, () => economy.Deposit(bot, excess / 2));
    }

    private int AvailableTurnBudget(Player bot)
        => Math.Min(_options.MaxActionTurns, Math.Max(0, bot.Turns - TurnReserve()));

    private int TurnReserve()
        => Math.Clamp(_options.MaxTurns / 5, 20, 50);

    private long CashReserve(Player bot)
        => Math.Max(2_500, (long)(bot.Hoes + bot.Thugs) * 250);

    private bool NeedsMoraleRecovery(Player bot)
    {
        var report = economy.GetCrewReport(bot);
        return bot.HoeHappiness < 45
            || bot.ThugHappiness < 45
            || report.UnmanagedHoes > 0
            || report.UncoveredThugs > 0
            || bot.Condoms < report.CondomsNeededForMaxStreetAction
            || bot.Beer < report.BeerNeededForMaxStreetAction;
    }

    private int AffordableQuantity(Player bot, int requested, int unitPrice, int cap)
    {
        if (requested <= 0 || unitPrice <= 0)
            return 0;

        var affordable = (int)Math.Min(int.MaxValue, Math.Max(0, bot.Cash - CashReserve(bot)) / unitPrice);
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
