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
        => new(
            MachinesFor(player).ToList(),
            SlotPaylines,
            ReputationFor(player),
            await StatsAsync(player.Id, ct),
            (await RecentAsync(player.Id, _options.Casino.HistoryDepth, ct)).ToList());

    public CasinoSpin SpinSlots(Player player, string? machineKey, long bet, int paylines, DateTime nowUtc)
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

        var totalBet = bet * paylines;
        if (player.Cash < totalBet)
            throw new GameRuleException($"You are carrying {player.Cash:C0}.");

        var repBefore = player.CasinoRep;
        player.Cash -= totalBet;
        player.CasinoRep = Math.Max(0, player.CasinoRep + totalBet * Math.Max(0, config.RepPerDollarWagered));
        var symbols = Enumerable.Range(0, 9).Select(_ => DrawSymbol(config)).ToArray();
        var result = ScorePaylines(symbols, paylines, bet, machine.MaxWinMultiplier);
        var payout = Math.Min(result.Payout, totalBet * machine.MaxWinMultiplier);
        player.Cash += payout;

        var transaction = new CasinoTransaction
        {
            PlayerId = player.Id,
            GameType = "slots",
            MachineKey = machine.Key,
            Paylines = paylines,
            WinningPaylines = result.WinningPaylines,
            BetAmount = totalBet,
            PayoutAmount = payout,
            NetResult = payout - totalBet,
            Outcome = string.Join(",", symbols.Select(x => x.Key)),
            CreatedAtUtc = nowUtc
        };
        db.CasinoTransactions.Add(transaction);
        return new CasinoSpin(transaction, Math.Max(0, (int)Math.Floor(player.CasinoRep) - (int)Math.Floor(repBefore)));
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

    public CasinoTransactionResponse ToResponse(CasinoTransaction transaction)
    {
        var machine = _options.Casino.Machine(transaction.MachineKey);
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
            SymbolsFrom(transaction.Outcome).ToList(),
            WinningPaylineIndexesFrom(transaction).ToList(),
            IsJackpot(transaction, machine),
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
        return rows.Select(ToResponse).ToList();
    }

    private IEnumerable<SlotMachineResponse> MachinesFor(Player player)
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
                machine.MaxBet * SlotPaylines.Length * machine.MaxWinMultiplier,
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

    private SlotSymbolOptions DrawSymbol(CasinoOptions config)
    {
        var symbols = config.SlotSymbols.Where(x => x.Weight > 0).ToList();
        if (symbols.Count == 0)
            throw new GameRuleException("The slot reels have no symbols.");

        var total = symbols.Sum(x => Math.Max(0, x.Weight));
        var roll = random.NextDouble() * total;
        var running = 0;
        foreach (var symbol in symbols)
        {
            running += Math.Max(0, symbol.Weight);
            if (roll < running)
                return symbol;
        }

        return symbols[^1];
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

    private IEnumerable<int> WinningPaylineIndexesFrom(CasinoTransaction transaction)
    {
        var symbols = SymbolOptionsFrom(transaction.Outcome).ToList();
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

    private bool IsJackpot(CasinoTransaction transaction, SlotMachineOptions? machine)
    {
        if (machine is null || machine.MaxWinMultiplier <= 0) return false;

        var symbols = SymbolOptionsFrom(transaction.Outcome).ToList();
        if (symbols.Count < 9) return false;

        return SlotPaylines
            .Take(Math.Clamp(transaction.Paylines, 1, SlotPaylines.Length))
            .Any(line => PayoutMultiplier(line.Cells.Select(cell => symbols[cell]).ToArray()) >= machine.MaxWinMultiplier);
    }

    private IEnumerable<string> SymbolsFrom(string outcome)
    {
        var labels = _options.Casino.SlotSymbols.ToDictionary(x => x.Key, x => x.Label);
        foreach (var key in outcome.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            yield return labels.TryGetValue(key, out var label) ? label : key;
    }

    private IEnumerable<SlotSymbolOptions> SymbolOptionsFrom(string outcome)
    {
        var symbols = _options.Casino.SlotSymbols.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
        foreach (var key in outcome.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (symbols.TryGetValue(key, out var symbol))
                yield return symbol;
    }
}

public sealed record CasinoSpin(CasinoTransaction Transaction, int RepEarned);

internal sealed record SlotScore(long Payout, int WinningPaylines);

public static class CasinoRep
{
    public static int LevelOf(Player player, GameOptions options)
        => options.Casino.LevelFor(player.CasinoRep)?.Level ?? 1;
}
