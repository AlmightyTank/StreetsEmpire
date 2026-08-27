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
    MuleService mules,
    ContractService contracts,
    StreetStrikeService strikes,
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
            // Rivals pay dues out of the same shift a player does, and the tithe is written onto this
            // navigation. Without it a seeded crew's treasury would never move.
            .Include(x => x.Alliance)
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
                // Before any other way of selling: a contract pays over the counter for stock a rival
                // already holds, so a rival that ignored one would be turning down the better price.
                var botActions = await TryFillContractAsync(bot, nowUtc, ct);
                if (botActions == 0)
                    botActions = await TryMarketAsync(bot, brain, nowUtc, ct);
                if (botActions == 0)
                    botActions = await TryMuleRunAsync(bot, brain, nowUtc, ct);
                if (botActions == 0)
                    botActions = await TryTerritoryAsync(bot, brain, nowUtc, ct);
                if (botActions == 0)
                    botActions = await TryAttackAsync(bot, brain, nowUtc, ct);
                // After the raid, because a rival that can afford an operation should run the operation.
                // A strike is what it does when it cannot: no lane free, not enough crew to commit, or
                // nobody on the board worth a full raid but somebody with a car left out.
                if (botActions == 0)
                    botActions = await TryStrikeAsync(bot, brain, nowUtc, ct);
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

        // The admin can drive a rival through any of the five, which is the only way to watch a specific
        // strike land against a specific target on demand rather than waiting for a personality to pick it.
        if (AttackMethods.IsStrike(request.Method))
        {
            var strike = strikes.Resolve(
                bot,
                defender,
                new CombatAttackRequest(defenderId, Method: request.Method, Coke: Math.Max(0, request.Coke ?? PoachStake(bot))),
                StrikeDefence.Everyone(defender),
                nowUtc);
            db.CombatLogs.Add(strike.Log);
            return strike.Result;
        }

        var thugs = Math.Max(1, request.Thugs ?? 1);
        var mission = await missions.LaunchAsync(
            bot,
            defender,
            new CombatAttackRequest(defenderId, thugs, Math.Min(thugs, Math.Max(0, request.Weapons ?? thugs))),
            nowUtc,
            ct);
        return new ActionResultResponse(mission.Summary, bot.Turns, new Dictionary<string, object?>
        {
            ["method"] = AttackMethods.Raid,
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
        var weaponKeys = WeaponTiers.All;
        if (report.UncoveredThugs > 0)
        {
            var offer = await db.MarketListings
                .Include(x => x.Seller)
                // Any gun covers a thug, so a rival shopping for coverage takes the cheapest one on
                // the board rather than holding out for a particular kind.
                .Where(x => x.CancelledAtUtc == null && x.Quantity > 0 && weaponKeys.Contains(x.Item) && x.SellerId != bot.Id)
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
                            $"AI: Bought {purchase.Quantity:N0} {TradeGoods.Label(offer.Item).ToLowerInvariant()} off {purchase.Listing.Seller.Name} for {purchase.Cost:C0}.");
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
        // The guns a full crew is not carrying, which are always the cheapest ones on the rack.
        var spare = Math.Max(0, bot.Weapons - bot.Thugs - 5);
        if (spare >= 5 && hideouts.WorkshopFor(bot.Hideout) is not null)
        {
            var before = Snapshot(bot);
            try
            {
                var surplus = bot.Armoury.WorstFirst(spare);
                var tier = WeaponTiers.All.First(x => surplus.Of(x) > 0);
                var price = (long)Math.Round(TradeGoods.ReferencePrice(_options, tier, bot.City) * 0.85);
                var listing = await market.ListAsync(bot, tier, surplus.Of(tier), price, nowUtc, ct);
                AddLog(bot, before, "MARKET", 0, nowUtc,
                    $"AI: Listed {listing.Quantity:N0} {TradeGoods.Label(tier).ToLowerInvariant()} at {listing.PricePerUnit:C0} each.");
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
    /// <summary>
    /// Takes an order off the town's board when the stock is already in the room.
    ///
    /// No dice roll and no personality dial: a contract pays over the counter for goods a rival is
    /// holding anyway, so every one of them should take it, and a rival that did not would simply be
    /// selling at a worse price. What decides whether they get there first is whose hours fall when,
    /// which is the competition the board is supposed to create.
    /// </summary>
    private async Task<int> TryFillContractAsync(Player bot, DateTime nowUtc, CancellationToken ct)
    {
        var open = await db.Contracts
            .Where(x => x.City == bot.City && x.FilledAtUtc == null && x.ExpiresAtUtc > nowUtc)
            .ToListAsync(ct);
        if (open.Count == 0) return 0;

        var purity = (int)Math.Round(bot.CokePurity * 100);
        // Best premium first, and only ones they can actually satisfy. Filtered here rather than by
        // catching the refusals, so a rival is not walking through exceptions to find its own stock.
        var fillable = open
            // A rival works an order in instalments the same way a player does, so it goes after ones
            // it can start rather than only ones it could finish in a single movement. It still will
            // not touch an order somebody else has begun.
            .Where(x => x.CanBeWorkedBy(bot.Id))
            .Where(x => TradeGoods.Held(bot, x.Good) > 0)
            .Where(x => x.MinimumPurityPercent is not { } floor || purity >= floor)
            .OrderByDescending(x => x.Payout - x.FlatValue)
            .ToList();

        foreach (var contract in fillable)
        {
            var filled = TryAction(bot, "CONTRACT", 0, nowUtc, () =>
            {
                var fill = contracts.Deliver(contract, bot, nowUtc);
                return new ActionResultResponse(fill.Summary, bot.Turns, new Dictionary<string, object?>());
            });
            if (filled > 0) return filled;
        }

        return 0;
    }

    /// <summary>
    /// Sends crew out of town when a route is worth it.
    ///
    /// A rival picks the route the same way a player should: by what the run actually clears after
    /// fares, not by the widest spread. Rivals sit in different towns, so what is worth running
    /// differs per rival without any of them being told so.
    /// </summary>
    private async Task<int> TryMuleRunAsync(Player bot, BotBrain brain, DateTime nowUtc, CancellationToken ct)
    {
        var profile = BotMuleProfile.For(brain.Focus);
        if (random.NextDouble() >= profile.RunChance)
            return 0;

        var cap = hideouts.ConcurrentRunCap(bot.Hideout);
        if (cap <= 0) return 0;

        // Saved runs plus ones launched earlier in this same batch. A whole round of rivals is written
        // in one SaveChanges at the end, so a query alone cannot see what has just been sent and the
        // cap would let a rival put its entire roster in the air at once.
        var out_ = await db.MuleRuns.CountAsync(x => x.PlayerId == bot.Id && x.SettledAtUtc == null, ct)
                   + db.ChangeTracker.Entries<MuleRun>()
                       .Count(x => x.State == EntityState.Added
                                   && x.Entity.PlayerId == bot.Id
                                   && x.Entity.SettledAtUtc is null);
        if (out_ >= cap) return 0;

        // A pimp on ground or already on a plane is not available, and neither is one leading a raid.
        var busy = new HashSet<long>(await territories.GarrisonedPimpIdsAsync(bot.Id, ct));
        foreach (var id in await db.MuleRuns
                     .Where(x => x.PlayerId == bot.Id && x.SettledAtUtc == null && x.PimpId != null)
                     .Select(x => x.PimpId!.Value)
                     .ToListAsync(ct))
            busy.Add(id);

        var pimp = bot.Crew.FirstOrDefault(x => x.LostAtUtc is null && !busy.Contains(x.Id));
        if (pimp is null) return 0;

        // Never send the whole house. Hoes on a plane are hoes not working the streets, and a rival
        // that emptied its roster onto one run would stop earning the moment it left.
        var spare = Math.Min(profile.MaxHoes, bot.Hoes / 3);
        if (spare < 1) return 0;

        var purse = (long)Math.Round((bot.Cash + bot.BankCash - CashReserve(bot, brain)) * profile.CashShare);
        if (purse <= 0) return 0;

        // Room already spoken for by cargo in the air. Without this a rival with two runs allowed out
        // sizes both against the same empty shelf, and the second one lands and is dumped.
        var inbound = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var pending in await db.MuleRuns
                     .Where(x => x.PlayerId == bot.Id && x.SettledAtUtc == null)
                     .Select(x => new { x.Good, x.Capacity })
                     .ToListAsync(ct))
            inbound[pending.Good] = inbound.GetValueOrDefault(pending.Good) + pending.Capacity;
        foreach (var entry in db.ChangeTracker.Entries<MuleRun>()
                     .Where(x => x.State == EntityState.Added && x.Entity.PlayerId == bot.Id && x.Entity.SettledAtUtc is null))
            inbound[entry.Entity.Good] = inbound.GetValueOrDefault(entry.Entity.Good) + entry.Entity.Capacity;

        var best = BestRoute(bot, spare, purse, profile.MinimumProfit, inbound);
        if (best is null) return 0;

        var run = default(MuleRun);
        var actions = TryAction(bot, "MULE_SENT", 0, nowUtc, () =>
        {
            run = mules.Launch(bot, pimp, best.Value.City, best.Value.Good, spare, best.Value.Cash, out_, nowUtc);
            return new ActionResultResponse(run.Summary, bot.Turns, new Dictionary<string, object?>());
        });

        if (actions > 0 && run is not null)
            db.MuleRuns.Add(run);
        return actions;
    }

    /// <summary>
    /// The best route on offer, judged on what it clears rather than on the spread. Returns nothing
    /// when no route pays, which is the correct answer for a rival sitting in the cheapest town.
    /// </summary>
    private (string City, string Good, long Cash)? BestRoute(Player bot, int hoes, long purse, long minimumProfit, IReadOnlyDictionary<string, int> inbound)
    {
        (string City, string Good, long Cash)? best = null;
        var bestProfit = minimumProfit;
        var storage = hideouts.CapacityFor(bot.Hideout);

        foreach (var profileCity in _options.CityMarkets.Profiles)
        {
            if (string.Equals(profileCity.City, bot.City, StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var good in new[] { "weed", "coke" })
            {
                MuleQuote quote;
                try
                {
                    quote = mules.Quote(bot, profileCity.City, good, hoes, purse);
                }
                catch (GameRuleException)
                {
                    continue;
                }

                // Only what there is somewhere to put. Cargo that will not fit is dumped on arrival,
                // so buying past the room is buying nothing: a rival that ignored this spent thousands
                // on coke and stored none of it.
                var room = Math.Max(0, TradeGoods.Room(bot, storage, good) - inbound.GetValueOrDefault(good));
                var units = Math.Min(quote.UnitsAffordable, room);
                if (units <= 0) continue;

                var home = TradeGoods.ReferencePrice(_options, good, bot.City);
                var spend = units * quote.UnitPriceThere;
                var profit = units * home - (quote.Fare + quote.Upkeep + spend);
                if (profit <= bestProfit) continue;

                bestProfit = profit;
                // Buying money only. Fares are charged on top of this, and anything sent beyond what
                // the load costs just rides along to be taken if the run is stopped.
                best = (profileCity.City, good, spend);
            }
        }

        return best;
    }

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
        var claimCommit = Math.Clamp(
            (int)Math.Round(free * profile.ThugCommitShare),
            config.MinimumGarrison,
            Math.Min(free, Math.Max(config.MinimumGarrison, config.MaxGarrisonThugs)));
        var attackCommit = Math.Clamp(
            (int)Math.Round(free * profile.ThugCommitShare),
            config.MinimumGarrison,
            Math.Min(free, Math.Max(config.MinimumGarrison, config.MaxRaidThugs)));

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
                var claimed = await territories.ClaimAsync(bot, open.Id, claimCommit, garrisonPimp?.Id, nowUtc, ct);
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
        var attackPower = AttackPower(CombatMissionService.CommandingPimps, attackCommit, bot.Armoury.Best(Math.Min(attackCommit, free)), (bot.HoeHappiness + bot.ThugHappiness) / 2);
        var candidates = await db.Territories
            .Include(x => x.Holder)
            .Where(x => x.City == bot.City)
            .Where(x => x.HolderId != null && x.HolderId != bot.Id)
            .Where(x => bot.AllianceId == null || x.Holder!.AllianceId != bot.AllianceId)
            .Where(x => x.ProtectedUntilUtc == null || x.ProtectedUntilUtc <= nowUtc)
            .OrderBy(x => x.GarrisonThugs)
            .Take(5)
            .ToListAsync(ct);

        foreach (var ground in candidates)
        {
            // The holder's morale, not the attacker's. Judging a garrison by how the raider feels is
            // nonsense, and it flattered thin garrisons held by a demoralised crew.
            var holderMorale = ground.Holder is { } owner ? (owner.HoeHappiness + owner.ThugHappiness) / 2 : 100;
            var defence = GarrisonDefence(ground.GarrisonThugs, holderMorale);
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
                    new CombatAttackRequest(holder.Id, attackCommit, Math.Min(attackCommit, free)),
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

        var botPlunder = economy.CalculatePlunder(bot);
        var antiFarm = _options.AntiFarm;
        // Only pull the band the anti-farm ratio actually permits, rather than the whole table. Weighed
        // on what is takeable, matching the gate that will judge the launch.
        var floor = Math.Max(antiFarm.MinDefenderNetWorth, (long)(botPlunder / Math.Max(1, antiFarm.MaxNetWorthRatio)));
        // Whole rows rather than a projection: net worth spans ten columns, and rebuilding a partial
        // Player to compute it would understate every target and skew both the anti-farm check and
        // the choice of who is worth hitting. Bounded to 25, so the cost is trivial.
        var candidates = await db.Players.AsNoTracking()
            .Where(x => x.Id != bot.Id)
            // Never its own crew. The truce is the whole of what a rival gets for paying dues.
            .Where(x => bot.AllianceId == null || x.AllianceId != bot.AllianceId)
            .Where(economy.PlunderAtLeast(floor))
            .OrderByDescending(economy.PlunderExpression)
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
                DefensePower(x.Pimps, x.Thugs, x.Armoury, (x.HoeHappiness + x.ThugHappiness) / 2),
                x.CombatProtectionUntilUtc is { } until && until > nowUtc,
                incoming.TryGetValue(x.Id, out var count) ? count : 0))
            .ToList();

        // Decide the force first, then judge the fight with it. Sizing the raid after choosing a target
        // meant the bot compared its whole roster against the defence and then attacked with a fraction
        // of it, so it reliably picked fights it could not win.
        var thugs = Math.Clamp((int)Math.Round(committed.AvailableThugs * profile.ThugCommitShare), profile.MinThugsToAttack, committed.AvailableThugs);
        var weapons = Math.Min(thugs, committed.AvailableWeapons);
        // Judged on the guns the crew would actually carry, which is the best of what is left on the
        // rack after any raid already out. A rival that valued its rifles twice would pick fights on
        // the strength of a crew it cannot field.
        var attackPower = AttackPower(CombatMissionService.CommandingPimps, thugs, committed.AvailableRack.Best(weapons), (bot.HoeHappiness + bot.ThugHappiness) / 2);

        // Who has hit this rival lately. Read from the fights that actually happened rather than kept
        // as a score, so a grudge is exactly as old as the last punch and nothing has to be pruned.
        var grudge = BotGrudgeProfile.For(brain.Focus);
        var since = nowUtc.AddHours(-Math.Max(1, grudge.MemoryHours));
        var grudges = await db.CombatLogs.AsNoTracking()
            .Where(x => x.DefenderId == bot.Id && x.CreatedAtUtc >= since && x.Outcome == "Victory")
            .GroupBy(x => x.AttackerId)
            .Select(g => new { Attacker = g.Key, Hits = g.Count() })
            .ToDictionaryAsync(x => x.Attacker, x => x.Hits, ct);

        var target = BotTargeting.Choose(targets, botPlunder, attackPower, antiFarm, profile.WinMargin, grudges, grudge.Weight);
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

    /// <summary>
    /// Takes a cheap shot at somebody who left something uncovered.
    ///
    /// Its own path rather than a branch inside the raid, because a strike judges a target on entirely
    /// different grounds. A raid needs a power comparison and a win margin; a strike needs to know
    /// whether they own a car, whether their hoes are underpaid, and nothing else. Sharing the raid's
    /// candidate query would mean loading and scoring defences that no strike ever consults.
    ///
    /// Every rule still applies: <see cref="StreetStrikeService.Resolve"/> validates turns, shields, the
    /// anti-farm matchup and each method's own requirements, and a refusal is swallowed the same way the
    /// other rival actions swallow theirs.
    /// </summary>
    private async Task<int> TryStrikeAsync(Player bot, BotBrain brain, DateTime nowUtc, CancellationToken ct)
    {
        var profile = BotStrikeProfile.For(brain.Focus);
        if (random.NextDouble() >= profile.StrikeChance)
            return 0;

        // The cheapest thing on its list. No point reading the board for a rival that cannot pay for the
        // one strike it would actually choose.
        var affordable = profile.Preference.Where(x => strikes.TurnCostOf(x) <= bot.Turns).ToList();
        if (affordable.Count == 0)
            return 0;

        var botPlunder = economy.CalculatePlunder(bot);
        var antiFarm = _options.AntiFarm;
        var floor = Math.Max(antiFarm.MinDefenderNetWorth, (long)(botPlunder / Math.Max(1, antiFarm.MaxNetWorthRatio)));
        // Tracked rather than AsNoTracking: a strike writes losses onto whichever of these it lands on,
        // and the run's single SaveChanges is what persists them. No hideout join - every limit a strike
        // reads belongs to the attacker's house, never the target's.
        var candidates = await db.Players
            .Where(x => x.Id != bot.Id)
            .Where(x => bot.AllianceId == null || x.AllianceId != bot.AllianceId)
            .Where(economy.PlunderAtLeast(floor))
            .Where(x => x.CombatProtectionUntilUtc == null || x.CombatProtectionUntilUtc <= nowUtc)
            .Where(x => x.StrikeProtectionUntilUtc == null || x.StrikeProtectionUntilUtc <= nowUtc)
            .OrderByDescending(economy.PlunderExpression)
            .Take(15)
            .ToListAsync(ct);
        if (candidates.Count == 0)
            return 0;

        // First method on the personality's list that some reachable target is actually exposed to.
        // Ordered this way round - method first, then target - so a Banker looking for a car does not
        // settle for infesting the richest player on the board just because they came up first.
        foreach (var method in affordable)
        {
            var target = candidates.FirstOrDefault(x => Exposed(bot, x, method));
            if (target is null)
                continue;

            var before = Snapshot(bot);
            try
            {
                var request = new CombatAttackRequest(
                    target.Id,
                    Method: method,
                    Coke: method == AttackMethods.Poach ? PoachStake(bot) : 0);
                var strike = strikes.Resolve(bot, target, request, StrikeDefence.Everyone(target), nowUtc);
                db.CombatLogs.Add(strike.Log);
                AddLog(bot, before, "ATTACK", strike.Log.TurnsSpent, nowUtc, $"AI: {strike.Log.Summary}");
                return 1;
            }
            catch (GameRuleException)
            {
                // Refused for a reason the cheap check above could not see. Try the next method rather
                // than the next target: whatever blocked this one is very likely about the rival itself.
            }
        }

        return 0;
    }

    /// <summary>
    /// Whether this target has left the thing a given strike is for uncovered. A cheap read, so the
    /// rival never spends its one action of the tick on a strike that was always going to do nothing.
    /// </summary>
    private bool Exposed(Player bot, Player target, string method) => method switch
    {
        AttackMethods.DriveBy => bot.Rides > 0 && target.Thugs > 0,
        AttackMethods.Jack => bot.Thugs > 0 && target.Rides > 0 && hideouts.RideRoom(bot) > 0,
        // Nothing to gain from infesting a house whose medicine already covers everyone in it.
        AttackMethods.Infest => target.Hoes > 0 && target.Medicine * Math.Max(1, _options.Strikes.Infest.HoesCuredPerCrate) < target.Hoes,
        // A well-paid house cannot be poached at any price, so a rival should not spend the coke finding out.
        AttackMethods.Poach => target.Hoes > 0
                               && target.HoeHappiness < 85
                               && hideouts.CrewRoom(bot, "hoes") > 0
                               && bot.Coke >= _options.Strikes.Poach.CokePerHoe,
        _ => false
    };

    /// <summary>
    /// What a rival is willing to put on the street for a poaching run: enough for the cap, but never
    /// more than a third of the pile. A rival that emptied its store to buy hoes would have nothing left
    /// to sell, and the poach would cost it the week.
    /// </summary>
    private int PoachStake(Player bot)
    {
        var poach = _options.Strikes.Poach;
        var forCap = Math.Max(1, poach.MaxHoes) * Math.Max(1, poach.CokePerHoe);
        return Math.Max(Math.Max(1, poach.CokePerHoe), Math.Min(forCap, bot.Coke / 3));
    }

    private int AttackPower(int pimps, int thugs, Armoury rack, double morale)
        => CombatPower.Attack(pimps, thugs, Firepower.Of(rack, thugs, _options.WeaponFirepower()), morale, _options.Combat.Power);

    /// <summary>What a garrison is worth: bodies with sidearms, never the holder's rack at home.</summary>
    private int GarrisonDefence(int thugs, double morale)
        => CombatPower.Defence(0, thugs, Firepower.Sidearms(thugs, thugs), morale, _options.Combat.Power);

    private int DefensePower(int pimps, int thugs, Armoury rack, double morale)
        => CombatPower.Defence(pimps, thugs, Firepower.Of(rack, thugs, _options.WeaponFirepower()), morale, _options.Combat.Power);

    private int RunBotRound(Player bot, BotBrain brain, DateTime actionTimeUtc)
    {
        if (random.NextDouble() < brain.IdleChance)
            return 0;

        // Before selling: cut sitting next to coke is money not yet made, and a rival that never
        // stepped on it would let the mix house pile up product it had already paid to produce.
        var action = TryCutCoke(bot, actionTimeUtc);
        if (action > 0) return action;

        action = TrySellProduct(bot, brain, actionTimeUtc);
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
    /// <summary>Steps on the coke, when there is cut, coke and somewhere to put the result.</summary>
    private int TryCutCoke(Player bot, DateTime actionTimeUtc)
    {
        if (bot.Cut <= 0 || bot.Coke <= 0 || (bot.Hideout?.WorkshopLevel ?? 0) <= 0)
            return 0;
        if (hideouts.CapacityFor(bot.Hideout).MaxCoke - bot.Coke <= 0)
            return 0;

        var perTurn = Math.Max(1, _options.Hideout.CutPerTurnPerMixLevel) * (bot.Hideout?.WorkshopLevel ?? 1);
        var turns = Math.Clamp((int)Math.Ceiling(bot.Cut / (double)perTurn), 1, _options.MaxActionTurns);
        return TryAction(bot, "CUT", turns, actionTimeUtc, () => economy.CutCoke(bot, turns));
    }

    private int TryUpgradeHideout(Player bot, BotBrain brain, DateTime actionTimeUtc)
    {
        var hideout = bot.Hideout;
        if (hideout is null || hideout.UpgradingToTier is not null)
            return 0;

        var reserve = CashReserve(bot, brain);
        var capacity = hideouts.CapacityFor(hideout);

        // The safe first. Cash over it is swept into the bank, and a bot that cannot hold cash on hand
        // can never save up for anything larger.
        if (hideouts.NextUpgrade(hideout, "safe") is { Locked: false } safe
            && bot.Cash + bot.BankCash >= safe.Cost + reserve
            && bot.Cash >= capacity.MaxCash * 3 / 4)
            return TryAction(bot, "HIDEOUT", 0, actionTimeUtc, () => hideouts.Upgrade(bot, "safe", actionTimeUtc));

        var report = economy.GetCrewReport(bot);
        if (hideouts.NextUpgrade(hideout, "storage") is { Locked: false } storage
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

        // The intelligence centre, for the personalities that would use it. Ahead of the labs because
        // a room that unlocks a whole way of earning is worth more than a percentage on one that is
        // already running, and without it a rival can never run a mule at all.
        var muleProfile = BotMuleProfile.For(brain.Focus);
        if (muleProfile.RunChance >= 0.2
            && hideouts.NextUpgrade(hideout, "intelligence") is { Locked: false } intel
            && bot.Cash + bot.BankCash >= intel.Cost + reserve * 2)
            return TryAction(bot, "HIDEOUT", 0, actionTimeUtc, () => hideouts.Upgrade(bot, "intelligence", actionTimeUtc));

        // Labs last, and only for the brain that actually runs product.
        if (brain.Focus == BotBrainFocus.ProductRunner)
            foreach (var lab in new[] { "weedlab", "cokelab" })
                if (hideouts.NextUpgrade(hideout, lab) is { Locked: false } next && bot.Cash + bot.BankCash >= next.Cost + reserve * 2)
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
            return TryAction(bot, "STORE", 0, actionTimeUtc, () => economy.BuyStoreItem(bot, WeaponFor(bot, brain), quantity));
        }

        // Medicine, once somebody has actually been infesting them.
        //
        // Rivals restocking on their own matters more here than for any other good, because an
        // infestation is only answered by a purchase. A field that never buys medicine is a field where
        // the strike is a one-way ratchet: every rival who reaches for it lands it at full effect
        // forever, and the whole AI population bleeds hoes with no counterplay. Driven by having been
        // hit rather than bought up front on principle, so it stays a reaction to the world - a rival
        // nobody bothers infesting goes on spending its money on crew, exactly as it should.
        var perCrate = Math.Max(1, _options.Strikes.Infest.HoesCuredPerCrate);
        var targetMedicine = Math.Min(capacity.MaxMedicine, (int)Math.Ceiling(bot.Hoes / (double)perCrate));
        if (bot.Medicine < targetMedicine && WasRecentlyInfested(bot, actionTimeUtc))
        {
            var quantity = AffordableQuantity(bot, targetMedicine - bot.Medicine, _options.MedicinePrice, random.NextInclusive(1, brain.MaxWeaponBuy), brain);
            return TryAction(bot, "STORE", 0, actionTimeUtc, () => economy.BuyStoreItem(bot, "medicine", quantity));
        }

        return 0;
    }

    /// <summary>
    /// Which gun a rival reaches for.
    ///
    /// Coverage first, always: an uncovered thug is a morale leak whatever it is holding, and the
    /// cheapest gun closes it. Only once the whole crew is armed does the question become what they are
    /// armed with, and then it is a question about money - the rival buys the best gun it can pay for
    /// out of spare cash, which is what makes a rich rival's house genuinely harder to break than a poor
    /// one of the same size.
    /// </summary>
    private string WeaponFor(Player bot, BotBrain brain)
    {
        if (bot.Weapons < bot.Thugs)
            return WeaponTiers.Pistol;

        var spare = Math.Max(0, bot.Cash - CashReserve(bot, brain));
        var best = WeaponTiers.Pistol;
        foreach (var tier in _options.Weapons.OrderBy(x => x.Price))
            if (spare >= tier.Price * Math.Max(1, brain.MaxWeaponBuy))
                best = tier.Key;
        return best;
    }

    /// <summary>
    /// Whether anyone has put something through this rival's house lately. Read off the fights that
    /// actually happened, like grudges are, so nothing has to be stored or pruned.
    /// </summary>
    private bool WasRecentlyInfested(Player bot, DateTime nowUtc)
    {
        var since = nowUtc.AddHours(-Math.Max(1, _options.AntiFarm.RepeatWindowHours));
        return db.CombatLogs.Any(x => x.DefenderId == bot.Id
                                      && x.Method == AttackMethods.Infest
                                      && x.CreatedAtUtc >= since);
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

        return TryAction(bot, "STREET", turnsToSpend, actionTimeUtc, () => economy.Scout(bot, turnsToSpend, district: DistrictFor(bot, brain)));
    }

    /// <summary>
    /// Where a rival works its shift.
    ///
    /// Read off what the rival is short of rather than fixed to its personality, because that is how a
    /// district is actually chosen: a crew builder with no thugs wants the slums today whatever it
    /// usually prefers. Personality only decides the tie, which is what stops every rival converging on
    /// the same corner and gives each of them a place they are usually found.
    /// </summary>
    private string DistrictFor(Player bot, BotBrain brain)
    {
        // Uncovered thugs and unmanaged hoes are the two shortages that actually cost a rival money
        // every shift, so they outrank a preference.
        if (bot.Thugs < Math.Max(1, brain.MinThugs))
            return "winos";
        if (bot.Hoes < bot.Thugs * Math.Max(1, brain.HoesPerThugTarget))
            return "nightclub";

        return brain.Focus switch
        {
            BotBrainFocus.Banker or BotBrainFocus.ResourceManager => "casino",
            BotBrainFocus.ProductRunner => "ghetto",
            BotBrainFocus.CrewBuilder => "nightclub",
            BotBrainFocus.MoraleNeglecter => "winos",
            _ => "lowrent"
        };
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
