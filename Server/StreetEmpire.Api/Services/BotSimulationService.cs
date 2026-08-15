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
    CombatMissionService missions,
    TerritoryService territories,
    PimpRoster pimps,
    MarketService market,
    IOptions<BotAutomationOptions> botOptions)
{
    private readonly GameOptions _options = options.Value;

    // Bound from its own section rather than from GameOptions, so it is read separately here.
    private readonly BotAutomationOptions _bots = botOptions.Value;

    public Task<BotSimulationResult> RunAsync(int requestedRounds, CancellationToken ct)
        => RunAsync(requestedRounds, null, ct);

    /// <param name="onlyPlayerId">
    /// Runs a single rival whether or not it is at the screen, for the admin's "act now" button: it
    /// opens a sitting rather than waiting for one. A paused rival stays paused even then, because
    /// pausing is a statement about the rival rather than about this run.
    /// </param>
    public async Task<BotSimulationResult> RunAsync(int requestedRounds, Guid? onlyPlayerId, CancellationToken ct)
    {
        var rounds = Math.Clamp(requestedRounds, 1, 10);
        var bots = await db.Players
            .Include(x => x.Account)
            .Include(x => x.Hideout)
            .Include(x => x.Crew)
            .Where(x => x.Account.IsBot && !x.Account.IsBotPaused)
            .Where(x => onlyPlayerId == null || x.Id == onlyPlayerId)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(ct);
        var actions = 0;
        var activeBotRounds = 0;
        var activeBotIds = new HashSet<Guid>();

        for (var round = 0; round < rounds; round++)
        {
            foreach (var bot in bots)
            {
                var nowUtc = DateTime.UtcNow;
                var brain = BotBrain.For(bot);
                // A targeted run is an explicit instruction, so it opens a session rather than waiting
                // for one. The gate exists to make rivals behave like people, not to gate the admin.
                if (!OpenSessionIfDue(bot, brain, nowUtc, force: onlyPlayerId is not null))
                    continue;

                // Reading the screen, changing your mind, going to make tea. Costs the session a slot,
                // so a hesitant rival gets through less in a sitting, exactly as a distracted one does.
                if (onlyPlayerId is null && random.NextDouble() < Math.Clamp(_bots.HesitationChance, 0, 0.9))
                {
                    SpendSessionAction(bot, brain, nowUtc);
                    continue;
                }

                await clock.AdvanceAsync(bot, nowUtc, ct: ct);
                var botActions = await TryMarketAsync(bot, brain, nowUtc, ct);
                if (botActions == 0)
                    botActions = await TryTerritoryAsync(bot, brain, nowUtc, ct);
                if (botActions == 0)
                    botActions = await TryAttackAsync(bot, brain, nowUtc, ct);
                if (botActions == 0)
                    botActions = RunBotRound(bot, brain, nowUtc);

                SpendSessionAction(bot, brain, nowUtc);
                if (botActions > 0)
                {
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
    /// Whether this rival is at the screen, opening a sitting if one is due.
    ///
    /// A player is away for hours while turns pile up, then sits down and spends the lot. So a rival
    /// is either in a session, acting on every tick, or logged off entirely. How big a sitting is
    /// follows from what there is to spend: a rival coming back to a full bank has a long evening
    /// ahead of it, and one that just played has almost nothing to do.
    /// </summary>
    private bool OpenSessionIfDue(Player bot, BotBrain brain, DateTime nowUtc, bool force)
    {
        var account = bot.Account;
        if (account.IsBotInSession(nowUtc)) return true;

        // The session that just ended has to be closed out before the next can be scheduled, or a
        // rival whose actions ran out would sit at zero and act again on the very next tick.
        if (account.BotSessionEndsAtUtc is not null)
            CloseSession(bot, brain, nowUtc);

        if (!force)
        {
            if (account.BotNextSessionAtUtc is { } due && due > nowUtc) return false;

            var schedule = BotSchedule.For(bot, brain, _bots);
            // Being due is not enough: a rival that keeps hours does not play outside them, and the
            // next-session time is only ever a lower bound.
            if (!schedule.IsAwake(nowUtc))
            {
                account.BotNextSessionAtUtc = schedule.NextSessionStart(nowUtc, random);
                return false;
            }
        }

        var config = _bots;
        var reserve = Math.Max(0, config.SessionTurnReserve);
        var spendable = Math.Max(0, bot.Turns - reserve);
        // Turns are the length of the evening. Sized off the smallest real turn action rather than an
        // average, so the cap is a ceiling the rival rarely reaches instead of one it always hits.
        var affordable = spendable / 2;
        var slots = Math.Clamp(affordable, 1, Math.Max(1, config.MaxActionsPerSession));

        account.BotSessionActionsLeft = force ? Math.Max(1, slots) : slots;
        account.BotSessionEndsAtUtc = nowUtc.AddMinutes(Math.Max(1, config.MaxSessionMinutes));
        account.BotNextSessionAtUtc = null;
        return true;
    }

    /// <summary>Burns one slot, and ends the sitting when the rival is out of them or out of turns.</summary>
    private void SpendSessionAction(Player bot, BotBrain brain, DateTime nowUtc)
    {
        var account = bot.Account;
        account.BotSessionActionsLeft = Math.Max(0, account.BotSessionActionsLeft - 1);
        // Nobody plays their bank to exactly zero, and a rival that did would have nothing in hand to
        // answer a raid with.
        if (account.BotSessionActionsLeft <= 0 || bot.Turns <= Math.Max(0, _bots.SessionTurnReserve))
            CloseSession(bot, brain, nowUtc);
    }

    private void CloseSession(Player bot, BotBrain brain, DateTime nowUtc)
    {
        var account = bot.Account;
        account.BotSessionActionsLeft = 0;
        account.BotSessionEndsAtUtc = null;
        account.BotNextSessionAtUtc = BotSchedule.For(bot, brain, _bots).NextSessionStart(nowUtc, random);
    }

    /// <summary>
    /// Puts a rival through an action the admin chose instead of one its brain chose.
    ///
    /// Every action goes through the same service a real player's would, so the rules still apply and a
    /// refusal is a real refusal rather than a special admin path that behaves differently from the
    /// game. That is the whole point: it is for testing what the game does, so it has to be the game
    /// doing it. The rival's hours are ignored, since this is an explicit instruction.
    /// </summary>
    public async Task<ActionResultResponse> DirectAsync(Player bot, AdminBotActionRequest request, DateTime nowUtc, CancellationToken ct)
    {
        var action = request.Action?.Trim().ToLowerInvariant() ?? string.Empty;
        await clock.AdvanceAsync(bot, nowUtc, ct: ct);
        var before = Snapshot(bot);

        var (logAction, turnsSpent, result) = action switch
        {
            "street" => ("STREET", Turns(request), economy.Scout(bot, Turns(request), autoBuySupplies: true)),
            "produce" => ("PRODUCTION", Turns(request), economy.Produce(bot, request.Product, Turns(request))),
            "sell" => ("SALE", 0, economy.SellProduct(bot, request.Product, Quantity(request))),
            "buy" => ("STORE", 0, economy.BuyStoreItem(bot, request.Item, Quantity(request))),
            "hire" => ("CREW", 0, economy.HireCrew(bot, request.Role, Quantity(request))),
            "fire" => ("CREW", 0, economy.FireCrew(bot, request.Role, Quantity(request))),
            "deposit" => ("BANK", 0, economy.Deposit(bot, request.Amount ?? 0)),
            "withdraw" => ("BANK", 0, economy.Withdraw(bot, request.Amount ?? 0)),
            "recover" => ("HIDEOUT", 0, economy.RecoverCrewMorale(bot, request.Strategy)),
            "upgrade" => ("HIDEOUT", 0, hideouts.Upgrade(bot, request.Room, nowUtc)),
            "attack" => ("ATTACK", 0, await AttackAsync(bot, request, nowUtc, ct)),
            _ => throw new GameRuleException(
                "Action must be street, produce, sell, buy, hire, fire, deposit, withdraw, recover, upgrade, or attack.")
        };

        // Marked as directed rather than "AI:", so the activity trail does not claim the brain chose it.
        AddLog(bot, before, logAction, turnsSpent, nowUtc, $"AI (directed): {result.Summary}");
        await db.SaveChangesAsync(ct);
        return result;

        static int Turns(AdminBotActionRequest request) => Math.Max(1, request.Turns ?? 1);
        static int Quantity(AdminBotActionRequest request) => Math.Max(1, request.Quantity ?? 1);
    }

    private async Task<ActionResultResponse> AttackAsync(Player bot, AdminBotActionRequest request, DateTime nowUtc, CancellationToken ct)
    {
        if (request.DefenderId is not { } defenderId)
            throw new GameRuleException("Pick who the rival should attack.");

        var defender = await db.Players
            .Include(x => x.Account)
            .Include(x => x.Crew)
            .SingleOrDefaultAsync(x => x.Id == defenderId, ct)
            ?? throw new GameRuleException("That target does not exist.");

        var thugs = Math.Max(1, request.Thugs ?? 1);
        var mission = await missions.LaunchAsync(
            bot,
            defender,
            new CombatAttackRequest(defenderId, thugs, Math.Min(thugs, Math.Max(0, request.Weapons ?? thugs))),
            nowUtc,
            ct);
        return new ActionResultResponse(mission.Summary, bot.Turns, new Dictionary<string, object?>
        {
            ["missionId"] = mission.Id,
            ["defender"] = defender.Name
        });
    }

    /// <summary>
    /// Buys from the board and puts surplus on it, so the market is not a room only players stand in.
    ///
    /// Buying is judged against the shop, never against the other listings: a rival that simply took
    /// the cheapest thing on the board would bid against itself and drain every listing regardless of
    /// whether the price was any good.
    /// </summary>
    private async Task<int> TryMarketAsync(Player bot, BotBrain brain, DateTime nowUtc, CancellationToken ct)
    {
        var report = economy.GetCrewReport(bot);
        var capacity = hideouts.CapacityFor(bot.Hideout);

        // Weapons first, because uncovered thugs are the gap a rival most needs to close and the one
        // good a player is likely to be selling.
        if (report.UncoveredThugs > 0)
        {
            var offer = await db.MarketListings
                .Include(x => x.Seller)
                .Where(x => x.CancelledAtUtc == null && x.Quantity > 0 && x.Item == "weapons" && x.SellerId != bot.Id)
                .Where(x => x.PricePerUnit < _options.WeaponPrice)
                .OrderBy(x => x.PricePerUnit)
                .FirstOrDefaultAsync(ct);
            if (offer is not null)
            {
                var room = Math.Max(0, capacity.MaxWeapons - bot.Weapons);
                var affordable = offer.PricePerUnit <= 0 ? 0 : (int)((bot.Cash - CashReserve(bot, brain)) / offer.PricePerUnit);
                var want = Math.Min(Math.Min(report.UncoveredThugs, offer.Quantity), Math.Min(room, affordable));
                if (want > 0)
                {
                    var before = Snapshot(bot);
                    try
                    {
                        var purchase = await market.BuyAsync(bot, offer.Id, want, ct);
                        AddLog(bot, before, "MARKET", 0, nowUtc,
                            $"AI: Bought {purchase.Quantity:N0} weapons off {purchase.Listing.Seller.Name} for {purchase.Cost:C0}.");
                        return 1;
                    }
                    catch (GameRuleException)
                    {
                        return 0;
                    }
                }
            }
        }

        // Then sell what it is sitting on and cannot use. Priced under the shop, or nobody would buy.
        var spare = Math.Max(0, bot.Weapons - bot.Thugs - 5);
        if (spare >= 5 && hideouts.StationFor(bot.Hideout, "workshop") is not null)
        {
            var before = Snapshot(bot);
            try
            {
                var price = (long)Math.Round(_options.WeaponPrice * 0.85);
                var listing = await market.ListAsync(bot, "weapons", spare, price, nowUtc, ct);
                AddLog(bot, before, "MARKET", 0, nowUtc,
                    $"AI: Listed {listing.Quantity:N0} weapons at {listing.PricePerUnit:C0} each.");
                return 1;
            }
            catch (GameRuleException)
            {
                return 0;
            }
        }

        return 0;
    }

    /// <summary>
    /// Considers taking ground: claiming what nobody holds, or raiding a garrison thin enough to beat.
    ///
    /// Rivals have to contest the map or players own it uncontested within a day, which is the same
    /// lesson as rivals not upgrading their hideouts. Ground is checked before a house raid because
    /// both use a lane, and a bot that always robbed houses would never take any.
    /// </summary>
    private async Task<int> TryTerritoryAsync(Player bot, BotBrain brain, DateTime nowUtc, CancellationToken ct)
    {
        var config = _options.Territory;
        var cap = territories.HoldingCapFor(bot.Hideout);
        var held = await db.Territories.CountAsync(x => x.HolderId == bot.Id, ct);
        if (held >= cap)
            return 0;

        var free = await territories.FreeThugsAsync(bot, ct);
        if (free < config.MinimumGarrison)
            return 0;

        // Never garrison the whole roster. A bot that empties its house to hold a corner is free loot,
        // so it commits the same share it would send on a raid.
        var profile = BotAttackProfile.For(brain.Focus);
        var commit = Math.Clamp((int)Math.Round(free * profile.ThugCommitShare), config.MinimumGarrison, free);

        var open = await db.Territories
            .Where(x => x.City == bot.City)
            .Where(x => x.HolderId == null && (x.ProtectedUntilUtc == null || x.ProtectedUntilUtc <= nowUtc))
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(ct);
        if (open is not null && bot.Turns >= config.ClaimTurnCost)
        {
            var before = Snapshot(bot);
            try
            {
                // Rivals post an Enforcer too, picking their best free one, or the ground is defended
                // by thugs alone and every player walks over it.
                var garrisonPimp = pimps.ChooseCommander(bot, await territories.GarrisonedPimpIdsAsync(bot.Id, ct));
                var claimed = await territories.ClaimAsync(bot, open.Id, commit, garrisonPimp?.Id, nowUtc, ct);
                AddLog(bot, before, "TERRITORY", 0, nowUtc, $"AI: Took over {claimed.Name} with {claimed.GarrisonThugs:N0} thug(s).");
                return 1;
            }
            catch (GameRuleException)
            {
                return 0;
            }
        }

        // Only ground it should actually beat. Judging with the force it will send, not the roster,
        // is the same correction the house-raid path needed.
        if (random.NextDouble() >= profile.AttackChance)
            return 0;
        var attackPower = AttackPower(CombatMissionService.CommandingPimps, commit, Math.Min(commit, free), (bot.HoeHappiness + bot.ThugHappiness) / 2);
        var candidates = await db.Territories
            .Include(x => x.Holder)
            .Where(x => x.City == bot.City)
            .Where(x => x.HolderId != null && x.HolderId != bot.Id)
            .Where(x => x.ProtectedUntilUtc == null || x.ProtectedUntilUtc <= nowUtc)
            .OrderBy(x => x.GarrisonThugs)
            .Take(5)
            .ToListAsync(ct);

        foreach (var ground in candidates)
        {
            // The holder's morale, not the attacker's. Judging a garrison by how the raider feels is
            // nonsense, and it flattered thin garrisons held by a demoralised crew.
            var holderMorale = ground.Holder is { } owner ? (owner.HoeHappiness + owner.ThugHappiness) / 2 : 100;
            var defence = DefensePower(0, ground.GarrisonThugs, ground.GarrisonThugs, holderMorale);
            if (attackPower < defence * profile.WinMargin)
                continue;

            var holder = await db.Players.Include(x => x.Account).Include(x => x.Crew)
                .SingleOrDefaultAsync(x => x.Id == ground.HolderId, ct);
            if (holder is null)
                continue;

            var before = Snapshot(bot);
            try
            {
                var mission = await missions.LaunchAsync(
                    bot,
                    holder,
                    new CombatAttackRequest(holder.Id, commit, Math.Min(commit, free)),
                    ground,
                    nowUtc,
                    ct);
                AddLog(bot, before, "ATTACK", mission.TurnsSpent, nowUtc, $"AI: Moved on {ground.Name}. {mission.Summary}");
                return 1;
            }
            catch (GameRuleException)
            {
                return 0;
            }
        }

        return 0;
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
            HoeMoraleBefore = before.HoeMorale,
            ThugMoraleBefore = before.ThugMorale,
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
        player.Coke,
        player.HoeHappiness,
        player.ThugHappiness);

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
        int Coke,
        double HoeMorale,
        double ThugMorale);

}

public sealed record BotSimulationResult(int TotalBots, int ActiveBots, int ActiveBotRounds, int Actions, int Rounds);
