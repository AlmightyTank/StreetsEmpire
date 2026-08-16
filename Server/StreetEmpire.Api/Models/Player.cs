namespace StreetEmpire.Api.Models;

public sealed class Player
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public PlayerAccount Account { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = "New York";

    // Money
    public long Cash { get; set; }
    public long BankCash { get; set; }

    // Turn bank
    public int Turns { get; set; }
    public DateTime LastTurnUpdateUtc { get; set; } = DateTime.UtcNow;

    // Crew
    public int Pimps { get; set; }
    public int Hoes { get; set; }
    public int Thugs { get; set; }
    public int HoeCutPercent { get; set; } = 30;
    public double HoeHappiness { get; set; } = 100;
    public double ThugHappiness { get; set; } = 100;

    // Inventory
    public int Condoms { get; set; }
    public int Beer { get; set; }
    public int Weapons { get; set; }
    public int Weed { get; set; }
    public int Coke { get; set; }

    /// <summary>
    /// Home-brewed beer. Cheaper than the shop and it keeps thugs going the same way, but it is
    /// contraband: holding it is what the law comes for.
    /// </summary>
    public int Moonshine { get; set; }

    /// <summary>Stretches coke. Worthless on its own, which is why it is priced off the local coke.</summary>
    public int Cut { get; set; }

    /// <summary>
    /// Attention earned rather than held. Everything in this game is illegal, so being illegal is not
    /// what distinguishes anything: what differs is how much notice a thing draws. This is the part
    /// that accumulates from working, and it decays on its own, which is why laying low works.
    /// </summary>
    public double Heat { get; set; }

    /// <summary>
    /// Heat runs on its own clock rather than the turn clock. The turn clock is dragged forward every
    /// few minutes by anyone at the screen, so a player who checked in often would never accumulate a
    /// whole hour, and would never be raided or cool down. Whole hours are consumed here and the
    /// remainder is left behind, so twelve five-minute visits still add up to an hour.
    /// </summary>
    public DateTime LastHeatRollUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When a flight lands. Null on the ground. Travel used to be instant, which made a town's distance
    /// a pure turn cost and nothing else: you could be somewhere else the moment you decided to be.
    /// Now the distance is time as well, and while it is running you are on a plane and cannot act.
    /// </summary>
    public DateTime? TravelArrivesAtUtc { get; set; }

    /// <summary>Whether this player is in the air right now.</summary>
    public bool IsInTransit(DateTime nowUtc)
        => TravelArrivesAtUtc is { } landing && landing > nowUtc;

    /// <summary>
    /// How much of the coke pile is actually coke, from 1 down towards nothing.
    ///
    /// Cut used to be a free doubling: a unit of filler became a unit of product at full price, which
    /// made the mix house a cheaper and faster source of coke than producing coke was, with no limit
    /// on it. Purity is what turns stretching into a trade rather than a printer - more units, each
    /// worth less - and it is why a batch of pure product is worth going out of your way for.
    /// </summary>
    public double CokePurity { get; set; } = 1;

    /// <summary>
    /// Adds coke of a known purity, blending it into whatever is already in the room.
    ///
    /// A method rather than a bare increment, because purity belongs to the pile and not to the
    /// delivery. Coke arrives produced, found, stolen, bought, flown in, or stretched with filler, and
    /// every one of those has to end up mixed into the same number. One place to do it is the only
    /// arrangement that stays true as more ways of arriving get added.
    /// </summary>
    public void AddCoke(int units, double purity)
    {
        if (units <= 0) return;
        var total = Coke + units;
        CokePurity = Math.Clamp((Coke * CokePurity + units * Math.Clamp(purity, 0, 1)) / total, 0, 1);
        Coke = total;
    }

    // Combat pacing fields written by the attack flow.
    public DateTime? CombatProtectionUntilUtc { get; set; }
    public DateTime? LastAttackAtUtc { get; set; }
    public DateTime? LastAttackedAtUtc { get; set; }

    /// <summary>
    /// Watermark for defence alerts: anything that happened to this player after it is unread. A single
    /// column rather than a notifications table, because the events already exist in CombatLogs and only
    /// the read position is missing.
    /// </summary>
    public DateTime? CombatAlertsSeenAtUtc { get; set; }

    /// <summary>
    /// Watermark for the catch-up digest shown on arrival. Kept separate from the alert watermark on
    /// purpose: reading the bell should not silently swallow the summary of what happened while the
    /// player was away, and seeing that summary should not mark every attack as read.
    /// </summary>
    public DateTime? CatchUpSeenAtUtc { get; set; }

    public Hideout? Hideout { get; set; }

    /// <summary>Named pimps, active and fallen. <see cref="Pimps"/> counts the active ones.</summary>
    public List<Pimp> Crew { get; set; } = [];

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<GameActionLog> ActionLogs { get; set; } = [];
    public List<CombatLog> AttacksMade { get; set; } = [];
    public List<CombatLog> Defenses { get; set; } = [];
    public List<CombatMission> MissionsStarted { get; set; } = [];
    public List<CombatMission> MissionsDefended { get; set; } = [];
}
