namespace StreetEmpire.Api.Models;

/// <summary>
/// A progressive pot that has been taken, and with it the line under the running total.
///
/// The pot itself is not stored anywhere. It is the machine's seed plus a slice of every wager placed
/// on that machine since the last drop, and the wagers are already written down one row at a time in
/// <see cref="CasinoTransaction"/>. Adding a counter beside them would mean every spin in the game
/// reading a shared row, adding to it and writing it back - which is a lost update on the busiest
/// path there is, and two winners at once splitting a pot that pays out twice.
///
/// So the drop is the only thing recorded, and it is recorded by being inserted. The total is derived
/// from the ledger that has to be right anyway.
/// </summary>
public sealed class CasinoJackpotDrop
{
    public long Id { get; set; }
    public string MachineKey { get; set; } = string.Empty;
    public Guid PlayerId { get; set; }
    public Player Player { get; set; } = null!;

    /// <summary>What the pot stood at when it went, which is what the player was paid.</summary>
    public long Amount { get; set; }

    /// <summary>The spin that took it, so the ledger row and the drop can be read against each other.</summary>
    public long CasinoTransactionId { get; set; }

    /// <summary>
    /// Held as a navigation as well as an id because the drop and the spin that won it are written in
    /// the same save, before the spin has an id to point at.
    /// </summary>
    public CasinoTransaction Transaction { get; set; } = null!;

    public DateTime WonAtUtc { get; set; } = DateTime.UtcNow;
}
