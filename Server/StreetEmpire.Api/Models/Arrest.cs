namespace StreetEmpire.Api.Models;

/// <summary>
/// Crew picked up working the streets, sitting in a cell with a deadline on them.
///
/// The decision it exists to create is bail or leave them. Bail is cash; leaving them is paid in the
/// morale of everybody still out, in the loyalty of every pimp who watched it happen, and - if the one
/// left inside had little reason to keep quiet - in the heat of whatever they told the law.
///
/// Everything the outcome depends on is frozen here at the arrest, for the same reason a mule run
/// freezes its odds: a player told only that they lost cannot tell a bad decision from bad luck, and a
/// price that moves overnight must not rewrite a bail that was already quoted.
/// </summary>
public sealed class Arrest
{
    public long Id { get; set; }
    public Guid PlayerId { get; set; }
    public Player Player { get; set; } = null!;

    public int Hoes { get; set; }
    public int Thugs { get; set; }

    /// <summary>
    /// The named one, when a sweep took one. Kept by id for the release and by name for the record,
    /// since the row outlives the pimp when nobody comes for them.
    /// </summary>
    public long? PimpId { get; set; }
    public Pimp? Pimp { get; set; }
    public string? PimpName { get; set; }

    /// <summary>What they had to lose by talking, read at the moment the door shut.</summary>
    public double PimpLoyaltyAtArrest { get; set; }

    /// <summary>Quoted once. What it cost when it happened is what it costs.</summary>
    public long BailAmount { get; set; }

    public string City { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;

    /// <summary>The odds this shift ran, so the sweep can be explained after the fact.</summary>
    public double HeatAtArrest { get; set; }
    public int ChancePercent { get; set; }

    public DateTime ArrestedAtUtc { get; set; }

    /// <summary>After this they are gone. Bail before it, or do not.</summary>
    public DateTime BailDeadlineUtc { get; set; }

    /// <summary>Set when it is answered either way, so nothing is settled twice.</summary>
    public DateTime? SettledAtUtc { get; set; }

    /// <summary>Pending until answered: Bailed or Abandoned.</summary>
    public string Outcome { get; set; } = "Pending";

    public bool IsHeld => SettledAtUtc is null;

    /// <summary>
    /// Heads in the cell. Bail is priced per head and the morale cost is counted per head.
    ///
    /// Counted off the name rather than off the id, because the id is filled in on save and one taken
    /// during the shift that recruited them has not got one yet. The name is written at the arrest and
    /// never moves.
    /// </summary>
    public int Heads => Hoes + Thugs + (PimpName is null ? 0 : 1);
}
