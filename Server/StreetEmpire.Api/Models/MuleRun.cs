namespace StreetEmpire.Api.Models;

/// <summary>
/// Crew sent to another town to buy cheap and carry it home.
///
/// The point of it is the trade it forces. Travelling yourself costs turns each way and puts you in
/// the wrong town; a run costs fewer turns but takes real time, locks up a pimp and hoes who are not
/// earning while they are gone, and is paid for in cash before anybody leaves. You are buying time
/// and presence with crew and money.
///
/// Everything the outcome depends on is frozen here at launch. A pimp whose loyalty slips while the
/// plane is in the air should not change a run that is already out, and a price that moves should not
/// retroactively rewrite what was paid.
/// </summary>
public sealed class MuleRun
{
    public long Id { get; set; }
    public Guid PlayerId { get; set; }
    public Player Player { get; set; } = null!;

    public string OriginCity { get; set; } = string.Empty;
    public string DestinationCity { get; set; } = string.Empty;

    /// <summary>What they are sent to buy. One good a run: a mule is not a shopping trip.</summary>
    public string Good { get; set; } = "weed";

    /// <summary>Outbound, Buying, Inbound, then Done. Never rolled back.</summary>
    public string Status { get; set; } = "Outbound";

    /// <summary>Pending until it lands: Delivered, Seized, Defected or Lost.</summary>
    public string Outcome { get; set; } = "Pending";
    public string Summary { get; set; } = string.Empty;

    public long? PimpId { get; set; }
    public Pimp? Pimp { get; set; }
    public string PimpName { get; set; } = string.Empty;

    /// <summary>
    /// Frozen at launch, and the reason a run is a decision rather than a dice roll. A pimp sent far
    /// away with your money and nothing to come back for is the interesting way this goes wrong.
    /// </summary>
    public double PimpLoyaltyAtLaunch { get; set; }

    public int AssignedHoes { get; set; }

    /// <summary>How much they can carry, from the hoes sent. This is the greed dial.</summary>
    public int Capacity { get; set; }

    /// <summary>Cash handed over to buy with. They buy at the destination's price, not at ours.</summary>
    public long CashSent { get; set; }

    /// <summary>Fares out and back, plus what it costs to keep them while they are gone.</summary>
    public long TravelCost { get; set; }
    public long UpkeepCost { get; set; }

    public int TurnsSpent { get; set; }

    public int UnitsBought { get; set; }
    public long UnitPricePaid { get; set; }
    public long CashReturned { get; set; }

    public int SeizedUnits { get; set; }
    public double HeatAdded { get; set; }
    public bool PimpLost { get; set; }
    public int HoesLost { get; set; }

    /// <summary>
    /// The odds this run faced, frozen so the outcome can be explained after the fact. A player told
    /// only that they lost cannot tell a bad decision from bad luck.
    /// </summary>
    public int BustChancePercent { get; set; }
    public int DefectChancePercent { get; set; }

    public DateTime DepartedAtUtc { get; set; }

    /// <summary>When they touch down and start buying, and when they are home. Both set at launch.</summary>
    public DateTime ArrivesAtUtc { get; set; }
    public DateTime ReturnsAtUtc { get; set; }

    /// <summary>Set when the run is settled, so a resolved run is never resolved twice.</summary>
    public DateTime? SettledAtUtc { get; set; }

    public bool IsOut => SettledAtUtc is null;
}
