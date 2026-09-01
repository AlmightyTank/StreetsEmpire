namespace StreetEmpire.Api.Models;

/// <summary>
/// Something the town's trader wants brought to them, and what standing it is worth.
///
/// A contract is the city wanting product: somebody at the docks needs sixty of coke by Thursday. This
/// is the other direction and a different person - the trader wants stock for their own shelf, and pays
/// under what they will sell it for, because that is what a shop is.
///
/// Which is what makes it worth doing at all. Buying a shotgun for $2,000 to sell it back at $1,670 is
/// a loss anybody can see; forging one for $1,400 and selling it at $1,670 is a trade. The wanted board
/// only ever asks for things the workshop can turn out, so the room that was the way *around* standing
/// is now also the way to earn it. A player with a bench has something to do with it besides arm
/// themselves, and the guns they make have a buyer who is not another player.
///
/// The standing is the real payment and it is why this exists. Rep could be bought and it could be
/// trickled out of ordinary shopping, and neither of those is *doing* anything: one is a wallet and the
/// other is a side effect. This is the version you play.
/// </summary>
public sealed class WantedOrder
{
    public long Id { get; set; }

    /// <summary>The town whose trader wants it. Orders belong to a counter, not to a player.</summary>
    public string City { get; set; } = string.Empty;

    public string Good { get; set; } = string.Empty;
    public int Quantity { get; set; }

    /// <summary>What they pay a unit. Under the shelf price, because they are going to sell it.</summary>
    public long PricePerUnit { get; set; }

    /// <summary>
    /// What the shop charges for the same thing, frozen when the order was posted. Kept so the board
    /// can show the cut the trader is taking without recomputing a price that may since have moved.
    /// </summary>
    public long ShopPricePerUnit { get; set; }

    /// <summary>
    /// Standing for finishing it, worked out when the order was posted so the board can say the number
    /// rather than make the player derive it.
    /// </summary>
    public int Rep { get; set; }

    public DateTime PostedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>
    /// How much has gone in so far. Instalments for the same reason contracts take them: orders run to
    /// dozens and an early storage room holds twelve guns, so demanding the lot in one movement would
    /// make most of the board unfillable by exactly the players it is meant to give something to do.
    /// </summary>
    public int DeliveredQuantity { get; set; }

    /// <summary>
    /// Who is filling it, set by the first delivery. An order two people are part-filling is one where
    /// somebody's work is going to be wasted, and it would be whoever arrived last.
    /// </summary>
    public Guid? ClaimedById { get; set; }
    public Player? ClaimedBy { get; set; }

    public Guid? FilledById { get; set; }
    public Player? FilledBy { get; set; }
    public DateTime? FilledAtUtc { get; set; }

    public bool IsOpen(DateTime nowUtc) => FilledAtUtc is null && ExpiresAtUtc > nowUtc;

    public int Remaining => Math.Max(0, Quantity - DeliveredQuantity);

    public bool CanBeWorkedBy(Guid playerId) => ClaimedById is null || ClaimedById == playerId;

    /// <summary>What the whole order pays in cash, before the standing on top of it.</summary>
    public long Payout => Quantity * PricePerUnit;

    /// <summary>What the same goods would have cost at the counter, for showing the trader's cut.</summary>
    public long ShopValue => Quantity * ShopPricePerUnit;
}
