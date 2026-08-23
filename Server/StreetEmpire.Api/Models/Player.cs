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

    // The gun rack, weakest to strongest. Four columns rather than one because a weapon does two jobs
    // that come apart: any gun covers a thug for morale, but what it contributes to a fight is the gun.
    public int Pistols { get; set; }
    public int Shotguns { get; set; }
    public int Smgs { get; set; }
    public int Rifles { get; set; }

    public int Weed { get; set; }
    public int Coke { get; set; }

    /// <summary>
    /// The rack as one value, for everything that wants to reason about it rather than about a column.
    /// </summary>
    public Armoury Armoury
    {
        get => new(Pistols, Shotguns, Smgs, Rifles);
        set
        {
            Pistols = Math.Max(0, value.Pistols);
            Shotguns = Math.Max(0, value.Shotguns);
            Smgs = Math.Max(0, value.Smgs);
            Rifles = Math.Max(0, value.Rifles);
        }
    }

    /// <summary>
    /// How many guns there are, of any kind. This is the coverage number: one gun covers one thug, and
    /// a thug with a pistol is exactly as content as a thug with a rifle.
    ///
    /// Deliberately read-only. It used to be the column, and making it derived is what forced every
    /// place that used to add or subtract weapons to say which ones - which is the only way a rack
    /// cannot quietly lose its rifles to a rule that was written when there was only one kind of gun.
    /// </summary>
    public int Weapons => Pistols + Shotguns + Smgs + Rifles;

    /// <summary>Puts guns on the rack.</summary>
    public void AddWeapons(string tier, int count)
    {
        if (count <= 0) return;
        Armoury = Armoury.Add(tier, count);
    }

    /// <summary>
    /// Takes guns off the rack, cheapest first, and reports what actually went. Every loss in the game
    /// runs through here - fights, storage overflow, a jacking gone wrong - so a bad day can never cost
    /// a player their rifles while the pistols sit untouched.
    /// </summary>
    public Armoury RemoveWeapons(int count)
    {
        var taken = Armoury.WorstFirst(count);
        Armoury -= taken;
        return taken;
    }

    /// <summary>
    /// Treats a sick crew. It does nothing at all until somebody infects your hoes, which is the point:
    /// it is the only stock in the game bought purely against something another player might do to you,
    /// and a crate sitting unused is a bet that did not have to be called.
    /// </summary>
    public int Medicine { get; set; }

    /// <summary>
    /// Doses for infesting somebody else's house. The other half of the medicine pairing: one is what
    /// you keep in case it happens to you, the other is what it costs to do it to somebody. Infesting
    /// was the only strike that took nothing to throw - a drive-by risks the car, a jacking needs a
    /// thug and a space to park, a poach spends coke, and poisoning a house was free.
    /// </summary>
    public int Poison { get; set; }

    /// <summary>
    /// Low-riders. A ride is what a drive-by is fired from and what a jacking takes, so it is the one
    /// asset that is both a tool and a target: parking a fleet outside a thin guard is an invitation.
    /// </summary>
    public int Rides { get; set; }

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
    /// Shelter from the quick strikes - drive-bys, jackings, infestations, poaching - kept apart from
    /// the shield a broken raid earns.
    ///
    /// One column for both would let either loop lock the other out. A player could fire a four-turn
    /// drive-by at a rival to buy them an hour of immunity from the raid that was actually coming, and a
    /// raid that took everything would also make its victim un-harassable. They are different scales of
    /// violence and they cool down at different speeds, so they get different clocks.
    /// </summary>
    public DateTime? StrikeProtectionUntilUtc { get; set; }

    /// <summary>
    /// Watermark for defence alerts: anything that happened to this player after it is unread. A single
    /// column rather than a notifications table, because the events already exist in CombatLogs and only
    /// the read position is missing.
    /// </summary>
    public DateTime? CombatAlertsSeenAtUtc { get; set; }

    /// <summary>
    /// When this player last made an offering. Null means never. One column rather than a table of
    /// prayers because only the most recent one gates anything: what the gods asked for is worked out
    /// from the week rather than stored, so there is no history to keep.
    /// </summary>
    public DateTime? LastPrayedAtUtc { get; set; }

    /// <summary>
    /// Watermark for the catch-up digest shown on arrival. Kept separate from the alert watermark on
    /// purpose: reading the bell should not silently swallow the summary of what happened while the
    /// player was away, and seeing that summary should not mark every attack as read.
    /// </summary>
    public DateTime? CatchUpSeenAtUtc { get; set; }

    /// <summary>
    /// The crew this player runs with, if any. One at a time: the point of an alliance is who you have
    /// agreed not to rob, and a player in two of them would be quietly holding a truce with everybody.
    /// </summary>
    public long? AllianceId { get; set; }
    public Alliance? Alliance { get; set; }
    public DateTime? AllianceJoinedAtUtc { get; set; }

    /// <summary>
    /// Where they stand in it. Meaningless without a crew, and deliberately not nullable: a member is
    /// always some rank, and the lowest one is a real answer rather than an absent one.
    /// </summary>
    public AllianceRank AllianceRank { get; set; } = AllianceRank.Soldier;

    /// <summary>
    /// Alliance thugs posted to this house, drawn out of the shared pool and standing here until they
    /// are released or killed. Held on the member rather than the alliance because they are somewhere
    /// specific: a pool that defended every member at once would be twenty houses guarded by one set of
    /// men, which is the opposite of finite.
    /// </summary>
    public int AllianceDefenders { get; set; }

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
