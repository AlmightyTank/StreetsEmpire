namespace StreetEmpire.Api.Models;

/// <summary>
/// Somebody in a town who wants a quantity of something by a deadline, and pays over the market for
/// it.
///
/// The game had exactly one buyer before this: the city itself, at a fixed price, for any amount, at
/// any hour. That is a price list rather than a market, and it made producing a routine rather than a
/// decision - there was never a reason to make one thing over another, or to make it by Tuesday.
///
/// A contract is demand with a shape: an amount, a deadline, sometimes a condition. It is offered to
/// the town rather than to a player, and the buyer is a real place on that town's map, so the people
/// wanting things are the same places the player already fights over.
/// </summary>
public sealed class Contract
{
    public long Id { get; set; }

    public string City { get; set; } = string.Empty;

    /// <summary>A place on this town's map, so the buyer is somewhere the player already knows.</summary>
    public string Buyer { get; set; } = string.Empty;

    public string Good { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public long PricePerUnit { get; set; }

    /// <summary>
    /// What the town pays for this good ordinarily, frozen when the contract was posted. Kept so the
    /// board can show what the premium actually is without recomputing a price that may since have
    /// moved, and so a filled contract still explains itself afterwards.
    /// </summary>
    public long ListPricePerUnit { get; set; }

    /// <summary>
    /// A purity floor, for coke only. Null when the buyer does not care. This is what makes a stretched
    /// pile a decision rather than free money: the cheap buyers take anything, the good ones do not.
    /// </summary>
    public int? MinimumPurityPercent { get; set; }

    public DateTime PostedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>
    /// How much of the order has been handed over so far.
    ///
    /// Orders run to sixty units and a first storage room holds five weapons or ten of coke, so
    /// insisting on the whole amount in one movement made most of the board unfillable for exactly the
    /// players it was meant to give something to aim at. Goods go in a bit at a time and the buyer
    /// keeps a tally.
    /// </summary>
    public int DeliveredQuantity { get; set; }

    /// <summary>
    /// Who is filling it. Set by the first delivery, because an order two people are part-filling is
    /// one where somebody's goods are going to be wasted - and it would be whoever worked hardest and
    /// arrived last. A claim is not forever: the deadline frees an order nobody finishes.
    /// </summary>
    public Guid? ClaimedById { get; set; }
    public Player? ClaimedBy { get; set; }

    /// <summary>Set when the last unit goes in. A contract is completed once and then it is gone.</summary>
    public Guid? FilledById { get; set; }
    public Player? FilledBy { get; set; }
    public DateTime? FilledAtUtc { get; set; }

    public bool IsOpen(DateTime nowUtc) => FilledAtUtc is null && ExpiresAtUtc > nowUtc;

    /// <summary>How much the buyer is still waiting on.</summary>
    public int Remaining => Math.Max(0, Quantity - DeliveredQuantity);

    /// <summary>Whether this player may put goods into it - unclaimed, or already theirs.</summary>
    public bool CanBeWorkedBy(Guid playerId) => ClaimedById is null || ClaimedById == playerId;

    /// <summary>What the whole job pays, and what the same goods would fetch sold flat.</summary>
    public long Payout => Quantity * PricePerUnit;
    public long FlatValue => Quantity * ListPricePerUnit;

    /// <summary>
    /// The part of the payout that is not simply the market price, handed over when the order is
    /// completed. Deliveries pay the town's ordinary rate as they happen, so a part-filled order is
    /// worth exactly what selling those goods flat would have been: the premium is what finishing buys,
    /// and the whole of it survives being delivered in pieces because it is never split.
    /// </summary>
    public long CompletionBonus => Payout - FlatValue;
}
