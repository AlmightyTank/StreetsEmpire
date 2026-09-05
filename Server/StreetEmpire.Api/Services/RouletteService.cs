using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

/// <summary>
/// The wheel.
///
/// Roulette needs none of the tuning the slots needed, and that is the interesting thing about it.
/// Every bet on the table is priced as though the zeroes were not there - a straight number pays
/// thirty-five to one, which is correct for a thirty-six pocket wheel - so the house's whole take is
/// the zeroes and nothing else. One zero is 2.70% and two is 5.26%, on every bet the table offers,
/// which is why a player can pick any of them without picking wrongly.
/// </summary>
public sealed class RouletteService(
    GameDbContext db,
    IOptionsSnapshot<GameOptions> options,
    IGameRandom random,
    EconomyService economy)
{
    internal const string RouletteGame = "roulette";

    /// <summary>The double zero, which only the two-zero wheel carries.</summary>
    internal const string DoubleZero = "00";

    /// <summary>
    /// The red pockets, in the arrangement every wheel in the world uses. Not derivable from the
    /// number - the colours alternate along the wheel rather than along the board - so it is a list.
    /// </summary>
    private static readonly HashSet<int> RedPockets =
        [1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36];

    private readonly GameOptions _options = options.Value;

    public async Task<RouletteBoardResponse> BoardAsync(Player player, CancellationToken ct)
        => new(
            _options.Casino.Roulette.Enabled,
            TablesFor(player).ToList(),
            BetKinds().ToList(),
            RedPockets.OrderBy(x => x).ToList(),
            Math.Max(0, _options.Casino.Roulette.SpinTurnCost),
            Math.Max(1, _options.Casino.Roulette.MaxBetsPerSpin),
            (await RecentAsync(player.Id, _options.Casino.HistoryDepth, ct)).ToList());

    /// <summary>
    /// One spin: stake, wheel, and whatever the bets on the cloth say about where it stopped.
    ///
    /// Every bet is settled against the same pocket, so a player covering half the board is not
    /// getting several rolls of a die - they are getting one, which is what makes covering the board
    /// a losing idea rather than a clever one.
    /// </summary>
    public RouletteSpin Spin(Player player, string? tableKey, IReadOnlyList<RouletteBetRequest> bets, DateTime nowUtc)
    {
        TravelGate.EnsureLanded(player);
        var config = _options.Casino.Roulette;
        if (!config.Enabled)
            throw new GameRuleException("The wheel is covered for the night.");

        var table = config.Table(tableKey) ?? config.Tables.FirstOrDefault()
            ?? throw new GameRuleException("There is no wheel on the floor.");

        var locked = LockedReason(player, table);
        if (locked is not null)
            throw new GameRuleException(locked);

        var placed = ReadBets(table, bets, config.MaxBetsPerSpin);
        var staked = placed.Sum(x => x.Amount);

        var turnCost = Math.Max(0, config.SpinTurnCost);
        if (turnCost > 0 && player.Turns < turnCost)
            throw new GameRuleException($"A spin is {turnCost:N0} turn(s) and you have {player.Turns:N0}.");
        if (player.Cash < staked)
            throw new GameRuleException($"That is {staked:C0} on the cloth and you are carrying {player.Cash:C0}.");

        var repBefore = player.CasinoRep;
        var compsBefore = player.CasinoComps;
        player.Turns -= turnCost;
        player.Cash -= staked;
        player.CasinoRep = Math.Max(0, player.CasinoRep + RepFor(table, staked));
        player.CasinoComps = Math.Max(0, player.CasinoComps + staked * Math.Max(0, _options.Casino.CompsPerDollarWagered));

        var pocket = SpinWheel(table);
        var settled = placed.Select(bet => Settle(bet, pocket)).ToList();
        var payout = settled.Sum(x => x.Payout);
        player.Cash += payout;

        var transaction = new CasinoTransaction
        {
            PlayerId = player.Id,
            GameType = RouletteGame,
            MachineKey = table.Key,
            // The cloth is not lanes, but the count of bets is the nearest true thing these two columns
            // can say, and the ledger reads better for having them than for leaving them at zero.
            Paylines = settled.Count,
            WinningPaylines = settled.Count(x => x.Payout > 0),
            BetAmount = staked,
            PayoutAmount = payout,
            NetResult = payout - staked,
            Outcome = pocket,
            DetailJson = JsonSerializer.Serialize(settled.Select(x => new StoredBet(x.Kind, x.Value, x.Amount, x.Payout))),
            CreatedAtUtc = nowUtc
        };
        db.CasinoTransactions.Add(transaction);

        return new RouletteSpin(
            transaction,
            pocket,
            ColourOf(pocket),
            settled,
            Math.Max(0, (int)Math.Floor(player.CasinoRep) - (int)Math.Floor(repBefore)),
            Math.Max(0, (int)Math.Floor(player.CasinoComps) - (int)Math.Floor(compsBefore)),
            turnCost);
    }

    /// <summary>
    /// Reads what was put on the cloth, and refuses anything the table would not take.
    ///
    /// Each bet is checked against the table minimum on its own rather than against the total. A
    /// hundred chips of a dollar is not a hundred-dollar bet, and a table with a minimum has one so
    /// that it is not asked to settle a hundred separate dollar bets.
    /// </summary>
    private static List<PlacedBet> ReadBets(RouletteTableOptions table, IReadOnlyList<RouletteBetRequest> bets, int maxBets)
    {
        if (bets.Count == 0)
            throw new GameRuleException("There is nothing on the cloth.");
        if (bets.Count > maxBets)
            throw new GameRuleException($"The croupier will take {maxBets:N0} bets on one spin.");

        var placed = new List<PlacedBet>();
        var total = 0L;
        foreach (var bet in bets)
        {
            var kind = RouletteBetKind.Parse(bet.Kind)
                ?? throw new GameRuleException($"There is no bet called \"{bet.Kind}\" on this cloth.");

            var value = kind.NormaliseValue(bet.Value, table)
                ?? throw new GameRuleException($"{kind.Name} does not take \"{bet.Value}\".");

            if (bet.Amount < table.MinBet || bet.Amount > table.MaxBet)
                throw new GameRuleException($"{table.Name} takes bets from {table.MinBet:C0} to {table.MaxBet:C0}.");

            if (total > long.MaxValue - bet.Amount)
                throw new GameRuleException("That is more than the cage can write down.");
            total += bet.Amount;

            placed.Add(new PlacedBet(kind.Key, value, bet.Amount, 0));
        }

        return placed;
    }

    /// <summary>Where the ball stopped. Every pocket is equally likely, which is the whole game.</summary>
    private string SpinWheel(RouletteTableOptions table)
    {
        var pockets = 37 + (table.Zeroes >= 2 ? 1 : 0);
        var index = random.NextInclusive(0, pockets - 1);
        return index == 37 ? DoubleZero : index.ToString();
    }

    private static PlacedBet Settle(PlacedBet bet, string pocket)
    {
        var kind = RouletteBetKind.Parse(bet.Kind);
        if (kind is null || !kind.Wins(bet.Value, pocket)) return bet;

        // Stake back plus the odds, the way it is paid across a real cloth.
        return bet with { Payout = bet.Amount + bet.Amount * kind.Odds };
    }

    internal static string ColourOf(string pocket)
    {
        if (pocket == DoubleZero || pocket == "0") return "green";
        return int.TryParse(pocket, out var n) && RedPockets.Contains(n) ? "red" : "black";
    }

    private IEnumerable<RouletteTableResponse> TablesFor(Player player)
        => _options.Casino.Roulette.Tables.Select(table =>
        {
            var locked = LockedReason(player, table);
            var pockets = 37 + (table.Zeroes >= 2 ? 1 : 0);
            return new RouletteTableResponse(
                table.Key,
                table.Name,
                table.Blurb,
                Math.Clamp(table.Zeroes, 1, 2),
                pockets,
                // Exact rather than measured: a straight number pays 35 to 1 on a wheel with one more
                // pocket than that, and the difference is the house's whole take.
                Math.Round(36.0 / pockets * 100, 2),
                table.MinBet,
                table.MaxBet,
                Math.Max(1, table.MinCasinoRepLevel),
                table.MinCasinoRepLevel > 1 ? _options.Casino.LevelName(table.MinCasinoRepLevel) : null,
                locked is not null,
                locked);
        });

    private string? LockedReason(Player player, RouletteTableOptions table)
    {
        var required = Math.Max(1, table.MinCasinoRepLevel);
        if (CasinoRep.LevelOf(player, _options) < required)
            return $"{table.Name} opens at {_options.Casino.LevelName(required)} on the casino floor.";

        if (table.MinNetWorth <= 0) return null;

        var worth = economy.CalculateNetWorth(player);
        return worth >= table.MinNetWorth
            ? null
            : $"{table.Name} opens at {table.MinNetWorth:C0} net worth. You are at {worth:C0}.";
    }

    /// <summary>Standing off a table's own maximum, the way the slots take it off a full ticket.</summary>
    private double RepFor(RouletteTableOptions table, long staked)
        => Math.Max(0, _options.Casino.RepPerMaxBetSpin) * staked / Math.Max(1, table.MaxBet);

    private static IEnumerable<RouletteBetKindResponse> BetKinds()
        => RouletteBetKind.All.Select(x => new RouletteBetKindResponse(x.Key, x.Name, x.Odds, x.Blurb, x.TakesNumber));

    private async Task<IReadOnlyList<RouletteSpinRowResponse>> RecentAsync(Guid playerId, int take, CancellationToken ct)
    {
        var rows = await db.CasinoTransactions.AsNoTracking()
            .Where(x => x.PlayerId == playerId && x.GameType == RouletteGame)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(Math.Clamp(take, 1, 50))
            .ToListAsync(ct);

        return rows.Select(ToResponse).ToList();
    }

    public RouletteSpinRowResponse ToResponse(CasinoTransaction transaction)
    {
        var table = _options.Casino.Roulette.Table(transaction.MachineKey);
        var bets = ReadStored(transaction.DetailJson);
        return new RouletteSpinRowResponse(
            transaction.Id,
            transaction.MachineKey,
            table?.Name ?? transaction.MachineKey,
            transaction.Outcome,
            ColourOf(transaction.Outcome),
            bets.Select(x => new RouletteSettledBetResponse(
                x.Kind,
                RouletteBetKind.Parse(x.Kind)?.Describe(x.Value) ?? x.Kind,
                x.Value,
                x.Amount,
                x.Payout)).ToList(),
            transaction.BetAmount,
            transaction.PayoutAmount,
            transaction.NetResult,
            transaction.CreatedAtUtc);
    }

    private static List<StoredBet> ReadStored(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<StoredBet>>(json) ?? [];
        }
        catch (JsonException)
        {
            // A row whose detail cannot be read still has its money, and the money is the part that
            // matters. Better a ledger line with no breakdown than a page that will not load.
            return [];
        }
    }

    private sealed record StoredBet(string Kind, string Value, long Amount, long Payout);
}

public sealed record PlacedBet(string Kind, string Value, long Amount, long Payout);

public sealed record RouletteSpin(
    CasinoTransaction Transaction,
    string Pocket,
    string Colour,
    IReadOnlyList<PlacedBet> Bets,
    int RepEarned,
    int CompsEarned,
    int TurnsSpent);

/// <summary>
/// The bets a cloth takes, and what each one covers.
///
/// Held as a table rather than as a switch because every one of them is the same shape - a name, some
/// odds, and a question about where the ball stopped - and a new one should be a row here rather than
/// another branch in three different places.
/// </summary>
public sealed class RouletteBetKind
{
    private RouletteBetKind(string key, string name, int odds, string blurb, bool takesNumber, Func<string, string, bool> wins)
    {
        Key = key;
        Name = name;
        Odds = odds;
        Blurb = blurb;
        TakesNumber = takesNumber;
        _wins = wins;
    }

    private readonly Func<string, string, bool> _wins;

    public string Key { get; }
    public string Name { get; }

    /// <summary>What it pays to one, on top of the stake coming back.</summary>
    public int Odds { get; }

    public string Blurb { get; }

    /// <summary>Whether the bet needs a value alongside it - a pocket, a dozen, a column.</summary>
    public bool TakesNumber { get; }

    public static readonly IReadOnlyList<RouletteBetKind> All =
    [
        new("straight", "Straight up", 35, "One pocket, and nothing else on the wheel.", true,
            (value, pocket) => value == pocket),
        new("red", "Red", 1, "Eighteen pockets, and neither zero.", false,
            (_, pocket) => RouletteService.ColourOf(pocket) == "red"),
        new("black", "Black", 1, "The other eighteen.", false,
            (_, pocket) => RouletteService.ColourOf(pocket) == "black"),
        new("odd", "Odd", 1, "Every odd number on the cloth.", false,
            (_, pocket) => Number(pocket) is { } n && n % 2 == 1),
        new("even", "Even", 1, "Every even one.", false,
            (_, pocket) => Number(pocket) is { } n && n % 2 == 0),
        new("low", "Low (1-18)", 1, "The bottom half.", false,
            (_, pocket) => Number(pocket) is >= 1 and <= 18),
        new("high", "High (19-36)", 1, "The top half.", false,
            (_, pocket) => Number(pocket) is >= 19 and <= 36),
        new("dozen", "Dozen", 2, "Twelve numbers in a block: 1-12, 13-24 or 25-36.", true,
            (value, pocket) => Number(pocket) is { } n && n >= 1 && int.TryParse(value, out var d) && (n - 1) / 12 == d - 1),
        new("column", "Column", 2, "Twelve numbers down the cloth.", true,
            (value, pocket) => Number(pocket) is { } n && n >= 1 && int.TryParse(value, out var c) && (n - 1) % 3 == c - 1)
    ];

    public static RouletteBetKind? Parse(string? key)
        => All.FirstOrDefault(x => string.Equals(x.Key, key?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The zeroes are numbers on the wheel and nothing on the cloth: they are not red, not black, not
    /// odd, not in a dozen. That is the house's entire edge, so it is worth being exact about.
    /// </summary>
    private static int? Number(string pocket)
        => pocket != RouletteService.DoubleZero && int.TryParse(pocket, out var n) && n >= 1 ? n : null;

    public bool Wins(string value, string pocket) => _wins(value, pocket);

    /// <summary>What a bet's value has to look like, or null if it does not look like anything.</summary>
    public string? NormaliseValue(string? value, RouletteTableOptions table)
    {
        if (!TakesNumber) return string.Empty;

        var trimmed = value?.Trim() ?? string.Empty;
        if (Key == "straight")
        {
            if (trimmed == RouletteService.DoubleZero)
                return table.Zeroes >= 2 ? trimmed : null;
            return int.TryParse(trimmed, out var pocket) && pocket is >= 0 and <= 36 ? pocket.ToString() : null;
        }

        return int.TryParse(trimmed, out var block) && block is >= 1 and <= 3 ? block.ToString() : null;
    }

    public string Describe(string value) => TakesNumber ? $"{Name} {value}" : Name;
}
