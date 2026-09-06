namespace StreetEmpire.Api.Models;

public sealed class Player : IStash
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public PlayerAccount Account { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public DateTime? NameChangedAtUtc { get; set; }
    /// <summary>
    /// The town the player is physically standing in.
    ///
    /// Deliberately not the same thing as <see cref="Models.Hideout.City"/>, and the whole point of
    /// this pairing. Travel moves this and nothing else: the house, the shelves, the safe and the crew
    /// stay where they were built, which is what makes going somewhere a decision rather than a
    /// teleport for an entire operation. Anything the player physically walks up to - the counter, the
    /// casino floor, the street, a trader's job book - reads this. Anything the house does on its own
    /// reads the hideout's town instead.
    /// </summary>
    public string City { get; set; } = "New York";

    /// <summary>
    /// The gun on the player's hip, as a weapon tier key, or null for somebody carrying nothing.
    ///
    /// A tier rather than a count because this is not a shelf: it is which of the guns in
    /// <see cref="Armoury"/> is the one to hand. Kept on the player rather than the rack so it travels
    /// with them, and swapping it for something out of hideout storage needs a trip home.
    /// </summary>
    public string? EquippedWeapon { get; set; }

    // Money
    /// <summary>
    /// Cash in the player's pocket. It goes where they go, it is what the street, the store and the
    /// casino floor are paid out of, and it is the only money a stop on the way into town can take.
    ///
    /// Uncapped on purpose. The safe used to be the ceiling on this, back when cash on hand and the
    /// safe were the same pile; now that <see cref="Models.Hideout.SafeCash"/> is a real place with a
    /// real door, the ceiling belongs to it. Walking around with a fortune is allowed and is meant to
    /// be a bad idea.
    /// </summary>
    public long Cash { get; set; }
    public long BankCash { get; set; }

    /// <summary>
    /// When this player last paid for a trip to the bank, or null for one who has never been.
    ///
    /// Moves within the grace window after it are free, so one visit is one charge however many times
    /// money changes hands during it. Only a paid trip writes this: refreshing it on the free moves
    /// inside the window would turn a single payment into permanent free banking for anyone willing
    /// to move money every few minutes.
    /// </summary>
    public DateTime? LastBankedAtUtc { get; set; }

    /// <summary>
    /// Start of the current counter-pricing day. Null means the next paid visit starts a new one.
    ///
    /// The grace window says whether this physical trip is still open; this says what the next new
    /// trip costs. Keeping them separate lets typo fixes stay free without letting them reset the day.
    /// </summary>
    public DateTime? BankTripWindowStartedAtUtc { get; set; }

    /// <summary>
    /// Paid counter visits taken in the current pricing day. Free moves inside a paid visit do not
    /// touch this, because they are the same visit rather than another turn of the ladder.
    /// </summary>
    public int BankTripsInWindow { get; set; }

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

    // Inventory - the hideout's shelves. See Carried for the other pile.
    //
    // Every good below sits in the hideout, in Hideout.City, and does not move when the player does.
    // These are the columns the storage room caps, the labs fill, the crew eat, the thugs arm
    // themselves from, and a raid carries out of the door - which is why they stayed here rather than
    // being moved on to the Hideout row when the two piles were split. What a player physically has on
    // them is Carried, and it is the new half.
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
    /// Low-riders, parked in the garage at <see cref="Models.Hideout.City"/>.
    ///
    /// A ride is what a drive-by is fired from and what a jacking takes, so it is the one asset that is
    /// both a tool and a target: parking a fleet outside a thin guard is an invitation.
    ///
    /// It is the hideout's, and it is the only good here whose home is the building rather than the
    /// storage room - the garage is bought with the tier, not with shelves. Cars do not get on planes
    /// with their owner, and there is deliberately no way to carry one: a ride is a thing you drive out
    /// of a garage and back into it, so it is in exactly one town and that town is the hideout's.
    ///
    /// When a base can be relocated, the fleet does not follow it for free either - moving cars between
    /// towns is a flatbed and a bill, and that price belongs to the relocation rule rather than to
    /// anything here. This column simply never changes because the player went somewhere.
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
    /// Standing at the street store, earned by trading there and bought by investing in it.
    ///
    /// A double rather than a counter because it accrues off cash: at a hundredth of a point per dollar
    /// an integer would round seventeen condoms down to nothing and hand the same player their rep back
    /// for buying a thousand at once. Nothing else in the game is priced in it, so there is no
    /// arithmetic here that a fraction can spoil. Heat is the same shape for the same reason.
    ///
    /// It is an empire rather than a person, so a season takes it. See <see cref="Services.StoreRep"/>.
    /// </summary>
    public double StoreRep { get; set; }

    /// <summary>
    /// Standing on the casino floor, earned by putting cash through the slots.
    ///
    /// It is separate from store standing because the two rooms remember different things: the counter
    /// trusts steady trade, while the casino trusts action. A player who gambles heavily should be
    /// known by the cage without making the gun dealer any friendlier, and a trader with a clean store
    /// record should not walk straight into the private machines.
    /// </summary>
    public double CasinoRep { get; set; }

    /// <summary>
    /// What the cage owes this player back, in dollars of comps.
    ///
    /// Separate from standing on purpose, because the two answer different questions and a real floor
    /// keeps them apart for the same reason: standing is who you are to the house and decides which
    /// room will take your money, while comps are what the house owes you for having played and are
    /// spent down to nothing every time you collect. One is a rank and the other is a balance, so a
    /// player can be a House Name with nothing to claim, or a Walk-In holding a night's worth.
    ///
    /// Held in dollars rather than in points because every one of them is redeemed for something with
    /// a price, and a currency that has to be mentally converted before it means anything is a
    /// currency nobody spends.
    ///
    /// Kept in cents, as a whole number, for the reason <see cref="Cash"/> is a long: this is money,
    /// and money in a double is money that drifts. It is added to a few cents at a time - a hundredth
    /// of every wager - so a season of play is tens of thousands of additions, each one landing on a
    /// binary fraction that cannot represent a tenth of a cent exactly. The error is invisible per
    /// spin and cumulative by construction, and it lands on a balance players spend.
    ///
    /// Cents rather than whole dollars because the accrual is inherently smaller than a dollar: at a
    /// hundredth of the stake, a fifty dollar hand earns fifty cents, and a currency that rounded
    /// that to nothing would pay out only to people betting in hundreds. The cage still talks in
    /// whole dollars - see CasinoService.CompsFor - because that is what the rewards are priced in.
    /// </summary>
    public long CasinoCompsCents { get; set; }

    /// <summary>
    /// Spins the house owes this player, and the ticket they are owed on.
    ///
    /// The ticket is held with the count rather than taken from whatever is on screen when they are
    /// spent. Free spins that played at the current stake would make the way to use them obvious and
    /// stupid: win them on the cheapest pull the machine takes, then set the stake to the maximum and
    /// collect at a hundred times the price of what earned them. They replay the spin that won them,
    /// which is also what a real floor does with them.
    /// </summary>
    public int CasinoFreeSpins { get; set; }

    public string? CasinoFreeSpinMachine { get; set; }
    public long CasinoFreeSpinBet { get; set; }
    public int CasinoFreeSpinLanes { get; set; }

    /// <summary>
    /// When the counter will take another investment. Null means now.
    ///
    /// One clock across every favour rather than one per favour, because what is being modelled is the
    /// counter's patience and not the player's: a table of per-investment cooldowns would let somebody
    /// take all three in the same minute, which is exactly the "buy the whole ladder this afternoon"
    /// the clock exists to prevent.
    /// </summary>
    public DateTime? StoreInvestmentReadyAtUtc { get; set; }

    /// <summary>
    /// How many jobs this player has asked the dealer to swap out in the current cycle.
    ///
    /// The count rather than the cost, because what a reroll charges is a ladder and the ladder is
    /// configuration: storing the price would freeze last week's tuning into a player who has not
    /// pressed the button since.
    /// </summary>
    public int JobRerollsUsed { get; set; }

    /// <summary>
    /// When the free one comes back and the count above goes to nothing. Null for somebody who has
    /// never asked, which is the same thing as a cycle that has already turned over.
    /// </summary>
    public DateTime? JobRerollsResetAtUtc { get; set; }

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
    /// Crew upkeep runs on whole hours, separate from the turn clock for the same reason heat does:
    /// checking in every few minutes must not dodge the bill, and the remainder has to stay on the
    /// clock until it becomes a real hour.
    /// </summary>
    public DateTime LastUpkeepUtc { get; set; } = DateTime.UtcNow;

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
    /// When this player finished, or dismissed, the opening walkthrough. Null means they have not.
    ///
    /// On the row rather than in browser storage, because "runs once, after the account is made" is a
    /// fact about the person and not about the browser they happened to sign up in. Kept in local
    /// storage it was neither: somebody who signed up on a phone got the whole thing again on their
    /// laptop, and anybody who cleared their browser got it again on a fortnight-old empire.
    ///
    /// A timestamp rather than a flag for the reason every other watermark here is one - it costs the
    /// same and it answers "when", which a bare true never can.
    /// </summary>
    public DateTime? WalkthroughSeenAtUtc { get; set; }

    /// <summary>
    /// Opening cash the next season owes this player, built up across consecutive top-ten finishes.
    ///
    /// A head start used to be worth one season and never compounded, on the stated grounds that
    /// winning a season by having won the one before it is the failure a seasonal game has to avoid.
    /// It is deliberate that this now does compound: the game is meant to have high highs, and a run
    /// of good seasons is the clearest one it can offer - the thing you are protecting when you play
    /// the last week of a season you have already won.
    ///
    /// What keeps it from being a permanent aristocracy is that it is a streak and not a balance. One
    /// season outside the top ten does not reduce it, it empties it, and the climb starts again from
    /// the bottom.
    ///
    /// Money rather than a multiplier on the streak, because what each season adds depends on where
    /// you came in it - a champion year and a tenth-place year are not worth the same, and totalling
    /// the cash is the only version of this that keeps that true.
    ///
    /// A person and not an empire, so a roll does not take it. See <see cref="Support.StartingState"/>.
    /// </summary>
    public long SeasonHeadStart { get; set; }

    /// <summary>
    /// How many seasons running this player has finished in the top ten. Zero the moment they do not.
    ///
    /// Kept beside the money because it is the half worth reading: the cash says what the run is worth
    /// and this says what the run is, which is the part somebody tells other people about.
    /// </summary>
    public int SeasonTopTenStreak { get; set; }

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

    /// <summary>
    /// What the player physically has on them. It goes where they go.
    ///
    /// The other half of the split, and the new one. Everything else on this row that looks like stock
    /// is the hideout's - see the inventory block above - and the difference between the two is
    /// entirely a matter of where it is standing when something happens to it. A stop on the road into
    /// town takes from here; a raid on the house cannot reach here at all. Product bought at a counter
    /// lands here, and putting it somewhere safer is a decision the player makes at their own front
    /// door.
    ///
    /// An owned type rather than nine more loose columns, so that it is one thing the rules can be
    /// handed rather than nine the next rule has to remember all of. Capped by
    /// <see cref="Services.HideoutService.CarryCapacityFor"/>, which is where bags, cars, escorts and
    /// skills will eventually be read.
    /// </summary>
    public Stash Carried { get; set; } = new();

    /// <summary>
    /// The hideout's shelves, as one value rather than as a dozen columns.
    ///
    /// It is <c>this</c>, because the player row is where the store has always been kept. The property
    /// exists so that code written from here on can say which of the two piles it means instead of
    /// leaving the reader to know, and so that the day the store does move to a table of its own, the
    /// rules that read it do not have to move with it.
    /// </summary>
    public IStash Stored => this;

    /// <summary>Named pimps, active and fallen. <see cref="Pimps"/> counts the active ones.</summary>
    public List<Pimp> Crew { get; set; } = [];

    /// <summary>
    /// Optimistic concurrency token, in the same spirit as <see cref="BetaKey.Version"/> and for a
    /// much larger reason.
    ///
    /// Nearly every rule in this game is written as read the player, check they can afford it, take
    /// it, save. Two requests that overlap each get their own DbContext and their own copy of this
    /// row, so both read the same cash, both agree the purchase is affordable, and both write - and
    /// the second write is computed from a balance that no longer exists. The money comes off once
    /// and the goods arrive twice. Double-clicking will not do it; two requests genuinely in flight
    /// together will, and the casino was the worst of it, where two winning spins could each read the
    /// same progressive meter and walk off with the whole of it.
    ///
    /// Stamped in <see cref="Data.GameDbContext.SaveChangesAsync(bool, CancellationToken)"/> rather
    /// than at the hundred-odd places that spend something, because a rule that has to be remembered
    /// at every call site is a rule that holds until somebody adds the hundred-and-first.
    /// </summary>
    public int Version { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<GameActionLog> ActionLogs { get; set; } = [];
    public List<CombatLog> AttacksMade { get; set; } = [];
    public List<CombatLog> Defenses { get; set; } = [];
    public List<CombatMission> MissionsStarted { get; set; } = [];
    public List<CombatMission> MissionsDefended { get; set; } = [];
}
