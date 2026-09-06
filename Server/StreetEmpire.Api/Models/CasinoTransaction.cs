namespace StreetEmpire.Api.Models;

public sealed class CasinoTransaction
{
    public long Id { get; set; }
    public Guid PlayerId { get; set; }
    public Player Player { get; set; } = null!;
    public string GameType { get; set; } = "slots";
    public string MachineKey { get; set; } = string.Empty;
    public int Paylines { get; set; } = 1;
    public int WinningPaylines { get; set; }
    public long BetAmount { get; set; }
    public long PayoutAmount { get; set; }
    public long NetResult { get; set; }

    /// <summary>
    /// The progressive pot this spin took, or zero. Kept beside the payout rather than folded into it
    /// so the ledger can still say which part of a very large number was the paytable and which part
    /// was the pot - they are paid for differently and they cap differently.
    /// </summary>
    public long JackpotAmount { get; set; }

    /// <summary>
    /// Whether the house paid for this pull rather than the player.
    ///
    /// The stake is still written down, because it is what the paytable multiplied and what the row
    /// has to show to make sense. What it is not is money anybody put in, so it feeds neither the
    /// progressive nor standing nor comps - all of which are paid for out of what players actually
    /// stake, and would otherwise be paid for twice.
    /// </summary>
    public bool IsFreeSpin { get; set; }

    /// <summary>
    /// What the pull or the spin was, in whatever shape that game needs: the nine or fifteen symbol
    /// keys for a machine, the pocket the ball stopped in for the wheel.
    /// </summary>
    public string Outcome { get; set; } = string.Empty;

    /// <summary>
    /// Anything the game needs written down that does not fit a column, as JSON. Roulette keeps the
    /// bets that were on the cloth here.
    ///
    /// A blob rather than a table because nothing ever queries across it - it is read back with the
    /// row it belongs to and never on its own - and a child table for something only ever fetched by
    /// its parent is a join bought for nothing.
    /// </summary>
    public string? DetailJson { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
