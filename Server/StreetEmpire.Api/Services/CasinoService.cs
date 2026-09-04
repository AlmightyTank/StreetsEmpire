using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

public sealed class CasinoService(
    GameDbContext db,
    IOptionsSnapshot<GameOptions> options,
    IGameRandom random,
    EconomyService economy)
{
    internal const string SlotsGame = "slots";

    private static readonly SlotPaylineResponse[] SlotPaylines =
    [
        new(1, "Top", [0, 1, 2]),
        new(2, "Middle", [3, 4, 5]),
        new(3, "Bottom", [6, 7, 8]),
        new(4, "Down diagonal", [0, 4, 8]),
        new(5, "Up diagonal", [6, 4, 2])
    ];

    private readonly GameOptions _options = options.Value;

    public async Task<CasinoBoardResponse> BoardAsync(Player player, CancellationToken ct)
    {
        var pots = await PotsAsync(ct);
        return new CasinoBoardResponse(
            MachinesFor(player, pots).ToList(),
            SlotPaylines,
            ReputationFor(player),
            await StatsAsync(player.Id, ct),
            (await RecentAsync(player.Id, _options.Casino.HistoryDepth, ct)).ToList(),
            JackpotRules(),
            (await RecentJackpotsAsync(ct)).ToList(),
            Math.Max(0, _options.Casino.SpinTurnCost),
            CompsFor(player));
    }

    /// <summary>
    /// Takes something off the cage in exchange for comps.
    ///
    /// The reward is a row of configuration rather than a branch, so everything on the menu is turns,
    /// cash and heat in some combination and this reads the same for all of them.
    /// </summary>
    public CompClaim ClaimComp(Player player, string? rewardKey)
    {
        TravelGate.EnsureLanded(player);
        var config = _options.Casino;
        if (!config.Enabled)
            throw new GameRuleException("The casino cage is closed right now.");

        var reward = config.Reward(rewardKey)
            ?? throw new GameRuleException("The cage does not do that.");

        var locked = CompLockedReason(player, reward);
        if (locked is not null)
            throw new GameRuleException(locked);

        var turnCap = _options.MaxTurnsFor(player);
        var turns = Math.Max(0, reward.Turns);
        // Refused rather than trimmed. Handing back four turns of a comped room because the bank was
        // nearly full, and charging the whole price for them, is the cage taking a night's play for
        // something the player did not get.
        if (turns > 0 && player.Turns >= turnCap)
            throw new GameRuleException($"Your turn bank is full at {turnCap:N0}. {reward.Name} would be wasted.");

        var cash = Math.Max(0, reward.Cash);
        var heatBefore = player.Heat;
        var granted = turns > 0 ? (int)Math.Min(turns, turnCap - player.Turns) : 0;

        player.CasinoComps = Math.Max(0, player.CasinoComps - reward.Cost);
        player.Turns += granted;
        player.Cash += cash;
        player.Heat = Math.Max(0, player.Heat - Math.Max(0, reward.Heat));
        var heatCleared = Math.Round(heatBefore - player.Heat, 1);

        var parts = new List<string>();
        if (granted > 0) parts.Add($"{granted:N0} turns");
        if (cash > 0) parts.Add($"{cash:C0}");
        if (heatCleared > 0) parts.Add($"{heatCleared:N1} heat off the file");
        var took = parts.Count == 0 ? "nothing anybody could point at" : string.Join(", ", parts);

        return new CompClaim(reward, granted, cash, heatCleared,
            $"Took {reward.Name} off the cage for {reward.Cost:C0} in comps: {took}.");
    }

    /// <summary>
    /// One pull: turns, stake, reels, paytable, and the pot if the grid says so.
    ///
    /// Async because the pot is not a number anybody stores. It is the machine's seed plus a slice of
    /// every wager taken on it since the last time somebody walked off with it, and answering that
    /// means asking the ledger.
    /// </summary>
    public async Task<CasinoSpin> SpinSlotsAsync(Player player, string? machineKey, long bet, int paylines, DateTime nowUtc, CancellationToken ct)
    {
        TravelGate.EnsureLanded(player);
        var config = _options.Casino;
        if (!config.Enabled)
            throw new GameRuleException("The casino cage is closed right now.");

        var machine = config.Machine(machineKey) ?? config.SlotMachines.FirstOrDefault()
            ?? throw new GameRuleException("There are no slot machines on the floor.");

        var locked = LockedReason(player, machine);
        if (locked is not null)
            throw new GameRuleException(locked);
        if (paylines is < 1 || paylines > SlotPaylines.Length)
            throw new GameRuleException($"Slots take between 1 and {SlotPaylines.Length:N0} paylines.");
        if (bet < machine.MinBet || bet > machine.MaxBet)
            throw new GameRuleException($"{machine.Name} takes bets from {machine.MinBet:C0} to {machine.MaxBet:C0}.");
        if (bet > long.MaxValue / paylines)
            throw new GameRuleException("That spin is too large for the cage to write down.");

        // Turns before money, because being out of turns is the answer to the whole request and there
        // is no sense taking a stake off somebody to tell them so.
        var turnCost = Math.Max(0, config.SpinTurnCost);
        if (turnCost > 0 && player.Turns < turnCost)
            throw new GameRuleException($"A pull is {turnCost:N0} turn(s) and you have {player.Turns:N0}.");

        var totalBet = bet * paylines;
        if (player.Cash < totalBet)
            throw new GameRuleException($"You are carrying {player.Cash:C0}.");

        // Read before the stake is taken and before the reels turn, so the figure the pot pays is the
        // one that was standing on the machine when the button went down - this spin's own slice
        // included, the way a real meter ticks up as you play it.
        var pot = await PotAsync(machine, ct) + ContributionFrom(totalBet);

        var repBefore = player.CasinoRep;
        player.Turns -= turnCost;
        player.Cash -= totalBet;
        player.CasinoRep = Math.Max(0, player.CasinoRep + totalBet * Math.Max(0, config.RepPerDollarWagered));
        var compsBefore = player.CasinoComps;
        player.CasinoComps = Math.Max(0, player.CasinoComps + totalBet * Math.Max(0, config.CompsPerDollarWagered));

        var reel = ReelStrip(config);
        var symbols = Enumerable.Range(0, 9).Select(_ => DrawSymbol(reel)).ToArray();
        var result = ScorePaylines(symbols, paylines, bet, machine.MaxWinMultiplier);
        var payout = Math.Min(result.Payout, totalBet * machine.MaxWinMultiplier);

        // The pot is not part of the paytable and so is not held to the paytable's ceiling. Capping it
        // would make the Sidewalk's meter an advertisement for money that machine cannot hand over.
        var jackpot = WinsJackpot(symbols, paylines, machine) ? pot : 0;
        player.Cash += payout + jackpot;

        var transaction = new CasinoTransaction
        {
            PlayerId = player.Id,
            GameType = SlotsGame,
            MachineKey = machine.Key,
            Paylines = paylines,
            WinningPaylines = result.WinningPaylines,
            BetAmount = totalBet,
            PayoutAmount = payout + jackpot,
            NetResult = payout + jackpot - totalBet,
            JackpotAmount = jackpot,
            Outcome = string.Join(",", symbols.Select(x => x.Key)),
            CreatedAtUtc = nowUtc
        };
        db.CasinoTransactions.Add(transaction);

        if (jackpot > 0)
        {
            db.CasinoJackpotDrops.Add(new CasinoJackpotDrop
            {
                MachineKey = machine.Key,
                PlayerId = player.Id,
                Amount = jackpot,
                Transaction = transaction,
                WonAtUtc = nowUtc
            });
        }

        return new CasinoSpin(
            transaction,
            Math.Max(0, (int)Math.Floor(player.CasinoRep) - (int)Math.Floor(repBefore)),
            Math.Max(0, (int)Math.Floor(player.CasinoComps) - (int)Math.Floor(compsBefore)),
            turnCost,
            jackpot);
    }

    public async Task<CasinoStatsResponse> StatsAsync(Guid playerId, CancellationToken ct)
    {
        var stats = await db.CasinoTransactions.AsNoTracking()
            .Where(x => x.PlayerId == playerId)
            .GroupBy(x => x.PlayerId)
            .Select(g => new CasinoStatsResponse(
                g.Count(),
                g.Sum(x => x.BetAmount),
                g.Sum(x => x.PayoutAmount),
                g.Sum(x => x.NetResult)))
            .SingleOrDefaultAsync(ct);
        return stats ?? new CasinoStatsResponse(0, 0, 0, 0);
    }

    /// <summary>
    /// Every machine's pot in one question.
    ///
    /// "No drop on this machine happened at or after this row" is the same statement as "this row is
    /// part of the current pot", and it is one that the database can answer for every machine at once
    /// rather than one round trip per machine on the floor.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, long>> PotsAsync(CancellationToken ct)
    {
        var wagered = await db.CasinoTransactions.AsNoTracking()
            .Where(x => x.GameType == SlotsGame)
            .Where(x => !db.CasinoJackpotDrops.Any(drop => drop.MachineKey == x.MachineKey && drop.WonAtUtc >= x.CreatedAtUtc))
            .GroupBy(x => x.MachineKey)
            .Select(g => new { Machine = g.Key, Total = g.Sum(x => x.BetAmount) })
            .ToListAsync(ct);

        var contributions = wagered.ToDictionary(x => x.Machine, x => ContributionFrom(x.Total), StringComparer.OrdinalIgnoreCase);
        return _options.Casino.SlotMachines.ToDictionary(
            machine => machine.Key,
            machine => SeedFor(machine) + contributions.GetValueOrDefault(machine.Key),
            StringComparer.OrdinalIgnoreCase);
    }

    private async Task<long> PotAsync(SlotMachineOptions machine, CancellationToken ct)
    {
        var wagered = await db.CasinoTransactions.AsNoTracking()
            .Where(x => x.GameType == SlotsGame && x.MachineKey == machine.Key)
            .Where(x => !db.CasinoJackpotDrops.Any(drop => drop.MachineKey == machine.Key && drop.WonAtUtc >= x.CreatedAtUtc))
            .SumAsync(x => (long?)x.BetAmount, ct) ?? 0;
        return SeedFor(machine) + ContributionFrom(wagered);
    }

    public CasinoTransactionResponse ToResponse(CasinoTransaction transaction)
        => ToResponse(transaction, SymbolIndex());

    private CasinoTransactionResponse ToResponse(CasinoTransaction transaction, IReadOnlyDictionary<string, SlotSymbolOptions> index)
    {
        var machine = _options.Casino.Machine(transaction.MachineKey);
        var symbols = SymbolOptionsFrom(transaction.Outcome, index).ToList();
        return new CasinoTransactionResponse(
            transaction.Id,
            transaction.GameType,
            transaction.MachineKey,
            machine?.Name ?? transaction.MachineKey,
            Math.Max(1, transaction.Paylines),
            Math.Max(0, transaction.WinningPaylines),
            transaction.BetAmount,
            transaction.PayoutAmount,
            transaction.NetResult,
            symbols.Select(x => x.Label).ToList(),
            WinningPaylineIndexesFrom(transaction, symbols).ToList(),
            IsTopAward(transaction, symbols, machine),
            transaction.JackpotAmount,
            transaction.CreatedAtUtc);
    }

    private async Task<IReadOnlyList<CasinoTransactionResponse>> RecentAsync(Guid playerId, int take, CancellationToken ct)
    {
        var rows = await db.CasinoTransactions.AsNoTracking()
            .Where(x => x.PlayerId == playerId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(Math.Clamp(take, 1, 50))
            .ToListAsync(ct);

        // Built once for the page rather than once per row. The ledger is eight rows deep and this was
        // eight rebuilds of the same dictionary.
        var index = SymbolIndex();
        return rows.Select(row => ToResponse(row, index)).ToList();
    }

    /// <summary>The last few pots that went, which is the floor's own news and belongs to everybody.</summary>
    private async Task<IReadOnlyList<CasinoJackpotDropResponse>> RecentJackpotsAsync(CancellationToken ct)
    {
        var rows = await db.CasinoJackpotDrops.AsNoTracking()
            .OrderByDescending(x => x.WonAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(5)
            .Select(x => new { x.MachineKey, x.Amount, x.WonAtUtc, PlayerName = x.Player.Name })
            .ToListAsync(ct);

        return rows.Select(x => new CasinoJackpotDropResponse(
            x.MachineKey,
            _options.Casino.Machine(x.MachineKey)?.Name ?? x.MachineKey,
            x.PlayerName,
            x.Amount,
            x.WonAtUtc)).ToList();
    }

    private CasinoCompsResponse CompsFor(Player player)
    {
        var config = _options.Casino;
        var perComp = config.CompsPerDollarWagered <= 0
            ? 0
            : Math.Max(1, (int)Math.Ceiling(1 / config.CompsPerDollarWagered));

        return new CasinoCompsResponse(
            (long)Math.Floor(player.CasinoComps),
            perComp,
            config.CompRewards.Select(reward =>
            {
                var locked = CompLockedReason(player, reward);
                return new CompRewardResponse(
                    reward.Key,
                    reward.Name,
                    reward.Blurb,
                    reward.Cost,
                    Math.Max(0, reward.Turns),
                    Math.Max(0, reward.Cash),
                    Math.Max(0, reward.Heat),
                    Math.Max(1, reward.MinCasinoRepLevel),
                    reward.MinCasinoRepLevel > 1 ? config.LevelName(reward.MinCasinoRepLevel) : null,
                    locked is not null,
                    locked);
            }).ToList());
    }

    /// <summary>
    /// Why the cage will not do this one, or null if it will. Standing is reported before price,
    /// because a room that is not open to you yet is a different answer from one you cannot afford.
    /// </summary>
    private string? CompLockedReason(Player player, CompRewardOptions reward)
    {
        var required = Math.Max(1, reward.MinCasinoRepLevel);
        if (CasinoRep.LevelOf(player, _options) < required)
            return $"{reward.Name} is for {_options.Casino.LevelName(required)} and above.";

        return player.CasinoComps >= reward.Cost
            ? null
            : $"{reward.Cost:C0} in comps. You are holding {(long)Math.Floor(player.CasinoComps):C0}.";
    }

    private CasinoJackpotRulesResponse JackpotRules()
    {
        var jackpot = _options.Casino.Jackpot;
        return new CasinoJackpotRulesResponse(
            jackpot.Enabled,
            JackpotSymbol()?.Label ?? jackpot.Symbol,
            Math.Max(1, jackpot.SymbolsRequired),
            jackpot.RequireAllPaylines,
            Math.Max(0, jackpot.ContributionPercent));
    }

    private IEnumerable<SlotMachineResponse> MachinesFor(Player player, IReadOnlyDictionary<string, long> pots)
        => _options.Casino.SlotMachines.Select(machine =>
        {
            var locked = LockedReason(player, machine);
            return new SlotMachineResponse(
                machine.Key,
                machine.Name,
                machine.Blurb,
                machine.MinBet,
                machine.MaxBet,
                machine.MaxWinMultiplier,
                machine.MaxBet * machine.MaxWinMultiplier,
                pots.GetValueOrDefault(machine.Key, SeedFor(machine)),
                SlotPaylines.Length,
                Math.Max(1, machine.MinCasinoRepLevel),
                machine.MinCasinoRepLevel > 1 ? _options.Casino.LevelName(machine.MinCasinoRepLevel) : null,
                locked is not null,
                locked);
        });

    private string? LockedReason(Player player, SlotMachineOptions machine)
    {
        var requiredRep = Math.Max(1, machine.MinCasinoRepLevel);
        if (CasinoRep.LevelOf(player, _options) < requiredRep)
            return $"{machine.Name} opens at {_options.Casino.LevelName(requiredRep)} on the casino floor.";

        if (machine.MinNetWorth <= 0) return null;

        var worth = economy.CalculateNetWorth(player);
        return worth >= machine.MinNetWorth
            ? null
            : $"{machine.Name} opens at {machine.MinNetWorth:C0} net worth. You are at {worth:C0}.";
    }

    private long SeedFor(SlotMachineOptions machine) => Math.Max(0, machine.JackpotSeed);

    private long ContributionFrom(long wagered)
    {
        var percent = Math.Max(0, _options.Casino.Jackpot.ContributionPercent);
        return percent <= 0 || wagered <= 0 ? 0 : (long)Math.Floor(wagered * percent / 100);
    }

    private SlotSymbolOptions? JackpotSymbol()
        => _options.Casino.SlotSymbols.FirstOrDefault(x =>
            string.Equals(x.Key, _options.Casino.Jackpot.Symbol, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Whether this grid takes the pot: enough of the jackpot symbol anywhere on the nine cells, with
    /// every lane bought if the floor says so.
    ///
    /// Counted across the whole grid rather than read along a lane on purpose. Three of the rarest
    /// symbol in a row is a one in a million spin, and a pot nobody in a world this size will ever
    /// collect is a decoration. Three of them anywhere is roughly one spin in twelve thousand.
    /// </summary>
    private bool WinsJackpot(IReadOnlyList<SlotSymbolOptions> symbols, int paylines, SlotMachineOptions machine)
    {
        var jackpot = _options.Casino.Jackpot;
        if (!jackpot.Enabled || SeedFor(machine) <= 0) return false;
        if (jackpot.RequireAllPaylines && paylines < SlotPaylines.Length) return false;

        var target = JackpotSymbol();
        if (target is null) return false;

        var landed = symbols.Count(x => string.Equals(x.Key, target.Key, StringComparison.OrdinalIgnoreCase));
        return landed >= Math.Max(1, jackpot.SymbolsRequired);
    }

    /// <summary>
    /// The reel, resolved once per spin instead of once per cell. Nine cells meant nine passes over
    /// the symbol list and nine re-additions of the same weights.
    /// </summary>
    private static ReelStripOptions ReelStrip(CasinoOptions config)
    {
        var symbols = config.SlotSymbols.Where(x => x.Weight > 0).ToList();
        if (symbols.Count == 0)
            throw new GameRuleException("The slot reels have no symbols.");

        return new ReelStripOptions(symbols, symbols.Sum(x => Math.Max(0, x.Weight)));
    }

    private SlotSymbolOptions DrawSymbol(ReelStripOptions reel)
    {
        var roll = random.NextDouble() * reel.TotalWeight;
        var running = 0;
        foreach (var symbol in reel.Symbols)
        {
            running += Math.Max(0, symbol.Weight);
            if (roll < running)
                return symbol;
        }

        return reel.Symbols[^1];
    }

    private static int PayoutMultiplier(IReadOnlyList<SlotSymbolOptions> symbols)
    {
        if (symbols.Count < 3)
            return 0;

        var left = symbols[0];
        if (!string.Equals(left.Key, symbols[1].Key, StringComparison.OrdinalIgnoreCase))
            return 0;

        return string.Equals(left.Key, symbols[2].Key, StringComparison.OrdinalIgnoreCase)
            ? Math.Max(0, left.TripleMultiplier)
            : Math.Max(0, left.PairMultiplier);
    }

    private static SlotScore ScorePaylines(IReadOnlyList<SlotSymbolOptions> symbols, int paylines, long bet, int maxWinMultiplier)
    {
        var payout = 0L;
        var winningPaylines = 0;
        foreach (var line in SlotPaylines.Take(paylines))
        {
            var multiplier = PayoutMultiplier(line.Cells.Select(cell => symbols[cell]).ToArray());
            if (multiplier <= 0) continue;

            winningPaylines++;
            payout += bet * Math.Min(multiplier, Math.Max(0, maxWinMultiplier));
        }

        return new SlotScore(payout, winningPaylines);
    }

    private static IEnumerable<int> WinningPaylineIndexesFrom(CasinoTransaction transaction, IReadOnlyList<SlotSymbolOptions> symbols)
    {
        if (symbols.Count < 9)
            yield break;

        foreach (var line in SlotPaylines.Take(Math.Clamp(transaction.Paylines, 1, SlotPaylines.Length)))
            if (PayoutMultiplier(line.Cells.Select(cell => symbols[cell]).ToArray()) > 0)
                yield return line.Index;
    }

    private CasinoRepResponse ReputationFor(Player player)
    {
        var config = _options.Casino;
        var current = config.LevelFor(player.CasinoRep);
        var next = config.NextLevelAfter(player.CasinoRep);
        var floor = current?.Rep ?? 0;
        var ceiling = next?.Rep ?? floor;
        var progress = next is null
            ? 100
            : ceiling <= floor
                ? 0
                : (int)Math.Clamp(Math.Floor((player.CasinoRep - floor) * 100 / (ceiling - floor)), 0, 100);
        var dollarsPerRep = config.RepPerDollarWagered <= 0
            ? 0
            : Math.Max(1, (int)Math.Ceiling(1 / config.RepPerDollarWagered));

        return new CasinoRepResponse(
            (int)Math.Floor(player.CasinoRep),
            current?.Level ?? 1,
            current?.Name ?? "Walk-In",
            next?.Level,
            next?.Name,
            next?.Rep,
            next is null ? 0 : Math.Max(0, next.Rep - (int)Math.Floor(player.CasinoRep)),
            progress,
            dollarsPerRep);
    }

    /// <summary>Whether a lane paid this machine's ceiling, which is the top the paytable can go.</summary>
    private static bool IsTopAward(CasinoTransaction transaction, IReadOnlyList<SlotSymbolOptions> symbols, SlotMachineOptions? machine)
    {
        if (machine is null || machine.MaxWinMultiplier <= 0) return false;
        if (symbols.Count < 9) return false;

        return SlotPaylines
            .Take(Math.Clamp(transaction.Paylines, 1, SlotPaylines.Length))
            .Any(line => PayoutMultiplier(line.Cells.Select(cell => symbols[cell]).ToArray()) >= machine.MaxWinMultiplier);
    }

    private Dictionary<string, SlotSymbolOptions> SymbolIndex()
        => _options.Casino.SlotSymbols.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<SlotSymbolOptions> SymbolOptionsFrom(string outcome, IReadOnlyDictionary<string, SlotSymbolOptions> index)
    {
        foreach (var key in outcome.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (index.TryGetValue(key, out var symbol))
                yield return symbol;
    }
}

public sealed record CasinoSpin(CasinoTransaction Transaction, int RepEarned, int CompsEarned, int TurnsSpent, long JackpotWon);

public sealed record CompClaim(CompRewardOptions Reward, int TurnsGranted, long CashPaid, double HeatCleared, string Summary);

internal sealed record SlotScore(long Payout, int WinningPaylines);

internal sealed record ReelStripOptions(IReadOnlyList<SlotSymbolOptions> Symbols, int TotalWeight);

public static class CasinoRep
{
    public static int LevelOf(Player player, GameOptions options)
        => options.Casino.LevelFor(player.CasinoRep)?.Level ?? 1;
}
