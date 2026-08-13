using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

public sealed class BotSimulationService(
    GameDbContext db,
    EconomyService economy,
    PlayerClock clock,
    IGameRandom random,
    IOptionsSnapshot<GameOptions> options,
    HideoutService hideouts,
    CombatMissionService missions)
{
    private readonly GameOptions _options = options.Value;

    public async Task<BotSimulationResult> RunAsync(int requestedRounds, CancellationToken ct)
    {
        var rounds = Math.Clamp(requestedRounds, 1, 10);
        var bots = await db.Players
            .Include(x => x.Account)
            .Include(x => x.Hideout)
            .Include(x => x.Crew)
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

                clock.Advance(bot, nowUtc);
                var botActions = await TryAttackAsync(bot, brain, nowUtc, ct);
                if (botActions == 0)
                    botActions = RunBotRound(bot, brain, nowUtc);
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

    /// <summary>
    /// Considers starting a fight. Separate from the synchronous action chain because launching a
    /// mission needs the database, and an attack replaces the round's other action rather than adding
    /// to it. Every rule still applies: LaunchAsync validates turns, lanes, crew, and the anti-farm
    /// matchup, and a refusal is swallowed the same way the other bot actions swallow theirs.
    /// </summary>
    private async Task<int> TryAttackAsync(Player bot, BotBrain brain, DateTime nowUtc, CancellationToken ct)
    {
        var profile = BotAttackProfile.For(brain.Focus);
        if (random.NextDouble() >= profile.AttackChance)
            return 0;

        var combat = _options.Combat;
        if (bot.Turns < combat.AttackTurnCost)
            return 0;

        var committed = await missions.CommitmentAsync(bot, ct);
        if (committed.AvailablePimps < 1 || committed.AvailableThugs < profile.MinThugsToAttack)
            return 0;
        if (committed.ActiveAttackMissions >= committed.MaxActiveAttackMissions)
            return 0;
        if (await missions.LaneReadyAtUtcAsync(bot.Id, nowUtc, ct) is { } readyAt && readyAt > nowUtc)
            return 0;

        var botNetWorth = economy.CalculateNetWorth(bot);
        var antiFarm = _options.AntiFarm;
        // Only pull the band the anti-farm ratio actually permits, rather than the whole table.
        var floor = Math.Max(antiFarm.MinDefenderNetWorth, (long)(botNetWorth / Math.Max(1, antiFarm.MaxNetWorthRatio)));
        // Whole rows rather than a projection: net worth spans ten columns, and rebuilding a partial
        // Player to compute it would understate every target and skew both the anti-farm check and
        // the choice of who is worth hitting. Bounded to 25, so the cost is trivial.
        var candidates = await db.Players.AsNoTracking()
            .Where(x => x.Id != bot.Id)
            .Where(economy.NetWorthAtLeast(floor))
            .OrderByDescending(economy.NetWorthExpression)
            .Take(25)
            .ToListAsync(ct);

        // One grouped read of who is already under attack, so bots spread out instead of all choosing
        // the same victim and having every launch after the second refused.
        var candidateIds = candidates.Select(x => x.Id).ToList();
        var incoming = await db.CombatMissions.AsNoTracking()
            .Where(x => candidateIds.Contains(x.DefenderId) && x.Status != "Complete")
            .GroupBy(x => x.DefenderId)
            .Select(g => new { DefenderId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DefenderId, x => x.Count, ct);

        var targets = candidates
            .Select(x => new BotTarget(
                x.Id,
                x.Name,
                economy.CalculateNetWorth(x),
                DefensePower(x.Pimps, x.Thugs, x.Weapons, (x.HoeHappiness + x.ThugHappiness) / 2),
                x.CombatProtectionUntilUtc is { } until && until > nowUtc,
                incoming.TryGetValue(x.Id, out var count) ? count : 0))
            .ToList();

        // Decide the force first, then judge the fight with it. Sizing the raid after choosing a target
        // meant the bot compared its whole roster against the defence and then attacked with a fraction
        // of it, so it reliably picked fights it could not win.
        var thugs = Math.Clamp((int)Math.Round(committed.AvailableThugs * profile.ThugCommitShare), profile.MinThugsToAttack, committed.AvailableThugs);
        var weapons = Math.Min(thugs, committed.AvailableWeapons);
        var attackPower = AttackPower(CombatMissionService.CommandingPimps, thugs, weapons, (bot.HoeHappiness + bot.ThugHappiness) / 2);

        var target = BotTargeting.Choose(targets, botNetWorth, attackPower, antiFarm, profile.WinMargin);
        if (target is null)
            return 0;

        var defender = await db.Players
            .Include(x => x.Account)
            .Include(x => x.Crew)
            .SingleOrDefaultAsync(x => x.Id == target.PlayerId, ct);
        if (defender is null)
            return 0;

        var before = Snapshot(bot);
        try
        {
            var mission = await missions.LaunchAsync(bot, defender, new CombatAttackRequest(defender.Id, thugs, weapons), nowUtc, ct);
            AddLog(bot, before, "ATTACK", mission.TurnsSpent, nowUtc, $"AI: {mission.Summary}");
            return 1;
        }
        catch (GameRuleException)
        {
            return 0;
        }
    }

    private int AttackPower(int pimps, int thugs, int weapons, double morale)
        => CombatPower.Attack(pimps, thugs, weapons, morale, _options.Combat.Power);

    private int DefensePower(int pimps, int thugs, int weapons, double morale)
        => CombatPower.Defence(pimps, thugs, weapons, morale, _options.Combat.Power);

    private int RunBotRound(Player bot, BotBrain brain, DateTime actionTimeUtc)
    {
        if (random.NextDouble() < brain.IdleChance)
            return 0;

        var action = TrySellProduct(bot, brain, actionTimeUtc);
        if (action > 0) return action;

        action = TryUpgradeHideout(bot, brain, actionTimeUtc);
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
        var capacity = hideouts.CapacityFor(bot.Hideout);
        // Recovery stockpiles are capped by storage for the same reason the buy targets are.
        var condomStockpile = Math.Min(capacity.MaxCondoms, report.CondomsNeededForMaxStreetAction * brain.RecoverySupplyMultiplier);
        var beerStockpile = Math.Min(capacity.MaxBeer, report.BeerNeededForMaxStreetAction * brain.RecoverySupplyMultiplier);

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

        if (bot.HoeHappiness < brain.SupplyMoraleThreshold && bot.Condoms < condomStockpile)
        {
            var quantity = AffordableQuantity(
                bot,
                condomStockpile - bot.Condoms,
                _options.CondomPrice,
                random.NextInclusive(brain.MinSupplyBuy, brain.MaxSupplyBuy),
                brain);
            if (quantity > 0)
                return TryAction(bot, "STORE", 0, actionTimeUtc, () => economy.BuyStoreItem(bot, "condoms", quantity));
        }

        if (bot.ThugHappiness < brain.SupplyMoraleThreshold && bot.Beer < beerStockpile)
        {
            var quantity = AffordableQuantity(
                bot,
                beerStockpile - bot.Beer,
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

    /// <summary>
    /// Rivals grow their base the way a player does. Without this they sit at the Trap House caps
    /// forever: rich enough on paper to be worth raiding, capped too low to put up a fight, and
    /// eventually walled off by the anti-farm net-worth ratio, which leaves a maxed player with
    /// nobody left to attack.
    ///
    /// Every branch is gated on the room already being the constraint, so a bot never spends on a
    /// bigger safe it has no cash to fill.
    /// </summary>
    private int TryUpgradeHideout(Player bot, BotBrain brain, DateTime actionTimeUtc)
    {
        var hideout = bot.Hideout;
        if (hideout is null || hideout.UpgradingToTier is not null)
            return 0;

        var reserve = CashReserve(bot, brain);
        var capacity = hideouts.CapacityFor(hideout);

        // The safe first. Cash over it is swept into the bank, and a bot that cannot hold cash on hand
        // can never save up for anything larger.
        if (hideouts.NextUpgrade(hideout, "safe") is { TierLocked: false } safe
            && bot.Cash + bot.BankCash >= safe.Cost + reserve
            && bot.Cash >= capacity.MaxCash * 3 / 4)
            return TryAction(bot, "HIDEOUT", 0, actionTimeUtc, () => hideouts.Upgrade(bot, "safe", actionTimeUtc));

        var report = economy.GetCrewReport(bot);
        if (hideouts.NextUpgrade(hideout, "storage") is { TierLocked: false } storage
            && bot.Cash + bot.BankCash >= storage.Cost + reserve
            && (report.CondomsNeededForMaxStreetAction > capacity.MaxCondoms || bot.Condoms >= capacity.MaxCondoms))
            return TryAction(bot, "HIDEOUT", 0, actionTimeUtc, () => hideouts.Upgrade(bot, "storage", actionTimeUtc));

        // Then the building itself, once the crew is pressed against its caps and there are turns to
        // spare. The turn reserve is what stops a bot from building instead of earning.
        if ((bot.Hoes >= capacity.MaxHoes || bot.Thugs >= capacity.MaxThugs)
            && hideouts.NextTier(hideout) is { } tier
            // Cash and bank together, because that is how the build is paid for.
            && bot.Cash + bot.BankCash >= tier.UpgradeCost + reserve
            && bot.Turns >= tier.UpgradeTurns + TurnReserve(brain))
            return TryAction(bot, "HIDEOUT", 0, actionTimeUtc, () => hideouts.Upgrade(bot, "tier", actionTimeUtc));

        // Labs last, and only for the brain that actually runs product.
        if (brain.Focus == BotBrainFocus.ProductRunner)
            foreach (var lab in new[] { "weedlab", "cokelab" })
                if (hideouts.NextUpgrade(hideout, lab) is { TierLocked: false } next && bot.Cash + bot.BankCash >= next.Cost + reserve * 2)
                    return TryAction(bot, "HIDEOUT", 0, actionTimeUtc, () => hideouts.Upgrade(bot, lab, actionTimeUtc));

        return 0;
    }

    private int TryBuySupplies(Player bot, BotBrain brain, DateTime actionTimeUtc)
    {
        var report = economy.GetCrewReport(bot);
        var capacity = hideouts.CapacityFor(bot.Hideout);
        // Targets are capped by the storage room. Without this a bot aims past what it can hold, the
        // store refuses the buy, and the swallowed rule error leaves it silently unable to restock.
        var targetCondoms = Math.Min(capacity.MaxCondoms, (int)Math.Ceiling(report.CondomsNeededForMaxStreetAction * brain.SupplyTargetMultiplier));
        var targetBeer = Math.Min(capacity.MaxBeer, (int)Math.Ceiling(report.BeerNeededForMaxStreetAction * brain.SupplyTargetMultiplier));

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

        var targetWeapons = Math.Min(capacity.MaxWeapons, Math.Max(0, (int)Math.Ceiling(bot.Thugs * brain.WeaponCoverageTarget)));
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
