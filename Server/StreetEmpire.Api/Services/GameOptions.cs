using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

public sealed class GameOptions
{
    public int TurnsPerTick { get; set; } = 2;
    public int TurnTickMinutes { get; set; } = 10;

    /// <summary>
    /// The bank a player starts life with, and the floor under every building above it. What a
    /// particular player can actually hold is <see cref="MaxTurnsFor"/>, because the building raises it.
    /// </summary>
    public int MaxTurns { get; set; } = 200;
    /// <summary>
    /// The opening bank. Half of the cap read as a courtesy and played as a wall: a hundred turns at
    /// twenty a shift is five clicks, and then eight hours of nothing. A full bank makes the first
    /// sitting long enough to buy the first lab and still have turns left to watch it work.
    /// </summary>
    public int StartingTurns { get; set; } = 200;
    public int MaxActionTurns { get; set; } = 20;

    /// <summary>
    /// How much faster turns come back while a player is still small, and the net worth at which that
    /// help has entirely faded.
    ///
    /// A flat rate is a wall that falls hardest on the people least able to take it: twelve turns an
    /// hour means a new player who spends their bank waits most of a day to play again, at exactly the
    /// point they have the least reason to come back. This tapers with net worth rather than switching
    /// off at a line, and the ceiling sits just past the Warehouse, so the help ends as the first real
    /// milestone comes into reach. An established empire is untouched.
    /// </summary>
    public double EarlyGameTurnBoost { get; set; } = 3;
    public long EarlyGameNetWorthCeiling { get; set; } = 250_000;

    /// <summary>Turns a tick is worth for this player, after the early-game taper.</summary>
    public int TurnsPerTickFor(Player player)
    {
        var ceiling = Math.Max(1, EarlyGameNetWorthCeiling);
        var boost = Math.Max(1, EarlyGameTurnBoost);
        // Portable wealth, not net worth: a player who spends their first 200,000 on a building is
        // not established, they are broke with somewhere to live. Tapering on the hideout would take
        // the beginner's turn boost away as a fee for buying the upgrade the game just recommended.
        var room = Math.Clamp(1 - EconomyService.PlunderOf(player, this) / (double)ceiling, 0, 1);
        return Math.Max(TurnsPerTick, (int)Math.Round(TurnsPerTick * (1 + (boost - 1) * room)));
    }

    /// <summary>
    /// The turn bank this player's building holds.
    ///
    /// The rate is deliberately not on this ladder. Everything in the game is priced per turn - the
    /// gross of a shift, the heat it draws, what a drive-by costs, what a tier costs - so paying a
    /// bigger player more turns an hour would inflate every one of those numbers and need all of them
    /// retuned. A bigger bank changes none of it: income per hour is untouched and a day is still
    /// worth the same 288 turns. What it changes is how much of that a player who is not at the screen
    /// actually keeps, and how long one sitting is allowed to be.
    ///
    /// Which is the thing worth buying. At 200 the bank fills in under seventeen hours, so anybody who
    /// sleeps and then works throws away most of a third of what the game owes them every day, and
    /// there was nothing on any shop page that could stop it happening. Now the building can.
    /// </summary>
    public int MaxTurnsFor(Player player) => Hideout.TurnBankFor(player.Hideout, MaxTurns);

    public long StartingCash { get; set; } = 5_000;
    public long StartingBankCash { get; set; } = 0;
    public int StartingPimps { get; set; } = 1;
    public int StartingHoes { get; set; } = 3;
    public int StartingThugs { get; set; } = 1;
    // Starting supplies fill a level 1 storage room exactly, so a new player is never over capacity.
    public int StartingCondoms { get; set; } = 17;
    public int StartingBeer { get; set; } = 10;
    public int StartingWeapons { get; set; } = 1;
    public int StartingHoeCutPercent { get; set; } = 30;

    public int CondomPrice { get; set; } = 10;
    public int BeerPrice { get; set; } = 15;
    public int WorkshopCraftMinutesPerTurn { get; set; } = 3;

    /// <summary>
    /// What the single generic weapon used to cost, kept only for the places that want one number to
    /// stand for "a gun": the market's reference band and the guidance that says arming a crew is
    /// worth doing. Everything that actually buys, fights with or values a weapon reads the tier table.
    /// </summary>
    public int WeaponPrice { get; set; } = 250;

    public int WeedSellPrice { get; set; } = 40;
    public int CokeSellPrice { get; set; } = 150;

    /// <summary>
    /// A crate of medicine, and a low-rider off the lot.
    ///
    /// Medicine is priced so that stocking against an infestation is an easy decision and stocking
    /// against ten is not: a crate covers several hoes, so covering a whole house costs real money that
    /// is doing nothing until someone attacks. The ride is priced against the first tier's ladder - it
    /// sits between the workshop and the lookout - because a drive-by is the cheapest way into the
    /// attack menu and should be reachable in a first tier that can already afford a lab.
    /// </summary>
    public int MedicinePrice { get; set; } = 250;

    /// <summary>
    /// What a dose costs over the counter. Dearer than the medicine that answers it, because the
    /// house being poisoned only has to cover its own hoes while the attacker is choosing to spend
    /// this, and a cheap attack on somebody else's crew is one nobody has to think about.
    /// </summary>
    public int PoisonPrice { get; set; } = 400;
    public int RidePrice { get; set; } = 25_000;

    /// <summary>
    /// What the chop shop gives you back for one. Well under the sticker, like every other second-hand
    /// price here: rides are bought to use, and a fleet held only to be sold again should lose money.
    /// </summary>
    public int RideSalePrice { get; set; } = 15_000;

    /// <summary>
    /// How hard a sale price follows purity, as an exponent.
    ///
    /// It has to fall slower than proportionally or stretching gains nothing and nobody would ever do
    /// it; it has to fall at all or stretching is free money. A square root does both: halving purity
    /// costs about 29% of the unit price while doubling the units, so a stretch pays, and each further
    /// round needs twice the cut for the same proportional gain until the cut costs more than it makes.
    /// A floor here would be a mistake - it would make total value climb with unit count forever,
    /// which is the printer all over again.
    /// </summary>
    public double CokePurityPricePower { get; set; } = 0.5;

    /// <summary>What a pile of this purity fetches, as a share of the list price.</summary>
    public double PurityMultiplier(double purity)
        => Math.Pow(Math.Clamp(purity, 0, 1), Math.Clamp(CokePurityPricePower, 0.05, 1));

    public int PimpNetWorth { get; set; } = 1_000;
    public int HoeNetWorth { get; set; } = 550;
    public int ThugNetWorth { get; set; } = 1_250;
    public int WeedNetWorth { get; set; } = 30;
    public int CokeNetWorth { get; set; } = 120;

    /// <summary>
    /// Moonshine and cut were the two things a player could hold that counted for nothing. Both take a
    /// shelf, both cost money and turns to make, both draw heat - and brewing a full still dropped your
    /// standing by whatever the materials cost, which is the same trap the hideout used to be in.
    ///
    /// Not invented: the game already says what they are worth when it prices a contract. Moonshine
    /// stands in for beer and is priced as beer; cut is priced at a quarter of coke, so its worth is a
    /// quarter of coke's. Changing either of those should change these.
    /// </summary>
    public int MoonshineNetWorth { get; set; } = 15;
    public int CutNetWorth { get; set; } = 30;

    /// <summary>
    /// A ride counts at what the chop shop would actually pay for it, not at what it cost. Net worth is
    /// what you could liquidate, and valuing a fleet at the sticker price would make buying rides a way
    /// to climb the board for free.
    /// </summary>
    public int RideNetWorth { get; set; } = 15_000;
    public int MedicineNetWorth { get; set; } = 250;
    public int PoisonNetWorth { get; set; } = 300;

    /// <summary>
    /// The gun rack. Empty here on purpose, like the hideout tables: the configuration binder appends to
    /// a pre-populated list rather than replacing it, so shipping defaults in the initializer would merge
    /// them with appsettings and let a stale row win the lookup.
    /// </summary>
    public List<WeaponTierOptions> Weapons { get; set; } = [];

    /// <summary>
    /// Everything else the workshop can turn out. Guns live in Weapons because they carry a price and a
    /// firepower as well as a recipe; these carry only the recipe.
    /// </summary>
    public List<MakeableOptions> Makeables { get; set; } = [];

    /// <summary>Firepower by tier, in units of one pistol, for the rack to measure itself against.</summary>
    public IReadOnlyDictionary<string, double> WeaponFirepower()
        => Weapons.ToDictionary(x => x.Key, x => x.Firepower);

    public WeaponTierOptions? WeaponTier(string? key)
        => Weapons.FirstOrDefault(x => string.Equals(x.Key, key?.Trim().ToLowerInvariant(), StringComparison.Ordinal));

    /// <summary>What a rack is worth, at what the shop charges for each gun on it.</summary>
    public long WeaponValue(Armoury armoury)
    {
        var total = 0L;
        foreach (var tier in Weapons)
            total += (long)armoury.Of(tier.Key) * tier.Price;
        return total;
    }

    public void ApplyWeaponDefaultsWhereEmpty()
    {
        // The recipes for everything the workshop turns out that is not a gun. Rates are what the old
        // rooms managed at their opening level, so a first workshop makes moonshine as fast as a first
        // still did, and the poison the mix house used to handle now needs a deeper room to reach.
        if (Makeables.Count == 0)
            Makeables =
            [
                new MakeableOptions { Key = "moonshine", PerTurn = 4, MaterialCost = 6, MinWorkshopLevel = 2 },
                new MakeableOptions { Key = "cut", PerTurn = 3, MaterialCost = 20, MinWorkshopLevel = 3 },
                // Medicine before poison, deliberately. They are the two ends of one mechanic and the
                // bench reached the attacking end first, which quietly made it cheaper to poison a
                // house than to protect your own - a player could buy the attack at a third of the
                // price while the answer to it stayed full price at the counter. Defence comes first
                // now: you can look after your own house a level before you can go after anybody's.
                new MakeableOptions { Key = "medicine", PerTurn = 2, MaterialCost = 90, MinWorkshopLevel = 3 },
                new MakeableOptions { Key = "poison", PerTurn = 1, MaterialCost = 140, MinWorkshopLevel = 4 }
            ];

        if (Weapons.Count > 0)
            return;

        // The firepower curve is the answer to what the prices buy. It falls away steeply against price -
        // a pistol is $250 a point, a rifle $7,200 - so trading up is never the efficient way to spend
        // money and always the only way left once the hideout's thug cap is full. That is the trade the
        // tiers exist to create: more bodies while you have room for them, better guns once you do not.
        //
        // Everything above the pistol was raised once standing began gating it, because the two are one
        // decision. A shotgun at $1,250 was an afternoon's takings, which made the rung in front of it
        // the only thing anybody had to think about and the price a formality - and a ladder whose rungs
        // are free is a waiting room. The pistol did not move: it is what a new player arms a crew with
        // on day one, and it is the one gun the ladder was never going to gate.
        //
        // A pistol is exactly 1, which is what the old single weapon contributed, so nobody's fighting
        // strength moved when tiers arrived - only what their rack is worth on paper.
        //
        // The rep rungs are the other half of that trade. Money alone used to decide the whole rack,
        // which meant one good night on the street put the best gun in the game in the hands of somebody
        // who had never been in the shop before. Now the price is what a gun costs and the standing is
        // whether anybody will sell you one.
        // Materials are deliberately well under the shelf now - about two fifths of it, in line with what
        // the other made goods have always cost against the thing they stand in for. Making a gun used to
        // save you thirty percent, which is a discount rather than a trade, and it left the bench as
        // something you built because you could not be sold the good ones rather than because it earned.
        // The trader's board is what that change is for: an order filled out of the shop pays a little,
        // and the same order filled off your own bench pays several times over.
        Weapons =
        [
            new WeaponTierOptions { Key = WeaponTiers.Pistol, Price = 250, Firepower = 1.0, ForgeCost = 100, MinWorkshopLevel = 2, MinRepLevel = 1 },
            new WeaponTierOptions { Key = WeaponTiers.Shotgun, Price = 2_000, Firepower = 1.4, ForgeCost = 800, MinWorkshopLevel = 2, MinRepLevel = 2 },
            new WeaponTierOptions { Key = WeaponTiers.Smg, Price = 5_000, Firepower = 1.9, ForgeCost = 2_000, MinWorkshopLevel = 4, MinRepLevel = 3 },
            // No forge cost and no workshop level: a rifle is the one gun nobody makes in a back room,
            // which is what stops the workshop from eventually replacing the shop entirely - and what
            // makes the top rung of standing the only door to it. It is priced as the thing at the end
            // of both ladders rather than as the last step of one.
            new WeaponTierOptions { Key = WeaponTiers.Rifle, Price = 18_000, Firepower = 2.5, MinRepLevel = 4 }
        ];
    }

    public StreetActionOptions StreetAction { get; set; } = new();
    public ProductionOptions Production { get; set; } = new();
    public MoraleOptions Morale { get; set; } = new();
    public CrewOptions Crew { get; set; } = new();
    public CombatOptions Combat { get; set; } = new();
    public StrikeOptions Strikes { get; set; } = new();
    public PrayerOptions Prayer { get; set; } = new();
    public AllianceOptions Alliances { get; set; } = new();
    public TitleOptions Titles { get; set; } = new();
    public HideoutOptions Hideout { get; set; } = new();
    public PimpOptions Pimps { get; set; } = new();
    public AntiFarmOptions AntiFarm { get; set; } = new();
    public WorldNewsOptions WorldNews { get; set; } = new();
    public ChatOptions Chat { get; set; } = new();
    public HistoryOptions History { get; set; } = new();
    public TerritoryOptions Territory { get; set; } = new();
    public MarketOptions Market { get; set; } = new();
    public CityMarketOptions CityMarkets { get; set; } = new();
    public MuleOptions Mules { get; set; } = new();
    public BankOptions Bank { get; set; } = new();
    public ArrestOptions Arrests { get; set; } = new();
    public SeasonOptions Seasons { get; set; } = new();
    public StoreOptions Store { get; set; } = new();
    public BetaOptions Beta { get; set; } = new();
}

public sealed class BetaOptions
{
    /// <summary>
    /// When on, every new non-first-player account must spend a beta key on either sign-up door.
    /// Left off by default so local development and the current test harness can still create players
    /// without first minting invites.
    /// </summary>
    public bool RequireKey { get; set; }

    /// <summary>How many keys a migration/backfill or future grant gives a player by default.</summary>
    public int KeysPerPlayer { get; set; } = 1;

    /// <summary>Zero means no automatic expiry.</summary>
    public int KeyExpiryDays { get; set; }
}

/// <summary>
/// Weights behind attack and defence strength. Tuned so a defender holds at equal armed crew while an
/// attacker with roughly 12-20% more armed thugs gets through: wide enough that defence is worth
/// investing in, narrow enough that attacking is a real option rather than a losing bet.
///
/// Before this pass, defence earned 24 per armed thug against attack's 20 and counted morale twice as
/// heavily, so an attacker needed about 1.4x the crew and bots correctly refused nearly every fight.
/// </summary>
/// <summary>
/// What happens inside a fight round. These were hardcoded, which made the round outcome impossible to
/// tune and hid two problems: a 10% band counted as a draw, so a modest edge produced six drawn rounds
/// and no result, and 12-22 morale damage barely broke a 95-morale defender inside the round cap. Both
/// meant an attacker could win on paper and still come home with nothing.
/// </summary>
public sealed class CombatRoundOptions
{
    /// <summary>Within this fraction of each other, a round is a draw and neither side breaks.</summary>
    public double ClosePercent { get; set; } = 0.06;
    public int CloseMinimumGap { get; set; } = 8;

    /// <summary>Morale both sides lose in a drawn round.</summary>
    public int DrawMoraleLossMin { get; set; } = 6;
    public int DrawMoraleLossMax { get; set; } = 12;

    /// <summary>
    /// Morale the losing side of a round gives up. Left at the original range on purpose: raising it to
    /// 16-26 alongside the narrower draw band turned every simulated fight into an attacker victory,
    /// because a side that starts losing rounds also loses crew and morale and cannot recover. The band
    /// was the real cause of drawn-out stalemates, so only the band moved.
    /// </summary>
    public int LosingSideMoraleLossMin { get; set; } = 12;
    public int LosingSideMoraleLossMax { get; set; } = 22;

    /// <summary>Morale the winning side of a round still gives up.</summary>
    public int WinningSideMoraleLossMin { get; set; } = 4;
    public int WinningSideMoraleLossMax { get; set; } = 9;

    public double CrewLossRate { get; set; } = 0.06;
    public double WeaponLossRate { get; set; } = 0.04;
    public double LossRollChance { get; set; } = 0.55;
}

public sealed class CombatPowerOptions
{
    public int ThugAttack { get; set; } = 13;
    public int ArmedThugAttack { get; set; } = 9;
    public int PimpAttack { get; set; } = 2;
    /// <summary>
    /// Close to the defender's weight on purpose. A crew's morale matters wherever it fights, and a
    /// large gap here swamps small crews: at 0.75 an attacker needed 40% more thugs to match five
    /// defenders, because the flat morale difference dwarfed a five-thug base.
    /// </summary>
    public double MoraleAttackWeight { get; set; } = 0.9;

    public int ThugDefence { get; set; } = 14;
    public int ArmedThugDefence { get; set; } = 9;
    public int PimpDefence { get; set; } = 3;
    public double MoraleDefenceWeight { get; set; } = 1;
}

/// <summary>
/// The quick strikes: drive-by, jacking, infestation, poaching.
///
/// Tuned against the turn bank rather than against each other, because turns are the only thing
/// limiting them. A raid is rationed by lanes and a thirty-minute cooldown; a strike is rationed by
/// what it costs, exactly as street work is, which is why the costs here are the whole balance. A full
/// two-hundred turn bank buys about fifty drive-bys or twenty-five poachings, and every one of those
/// turns is a turn not spent earning - so a player who spends an evening harassing rivals has spent an
/// evening not making money, and that is the trade the prices are for.
///
/// Each strike also draws heat. They are loud, public crimes committed against people who have their
/// own reasons to talk to the law, so a night of them puts a player on the radar the same way a night
/// of working the streets does, only faster.
/// </summary>
public sealed class StrikeOptions
{
    /// <summary>
    /// How long a strike shelters its victim from further strikes. Short by design: this is here to stop
    /// a crowd emptying one player's garage in a minute, not to make anyone safe for an evening.
    /// </summary>
    public int ShieldMinutes { get; set; } = 20;

    public DriveByOptions DriveBy { get; set; } = new();
    public JackOptions Jack { get; set; } = new();
    public InfestOptions Infest { get; set; } = new();
    public PoachOptions Poach { get; set; } = new();
}

public sealed class DriveByOptions
{
    /// <summary>
    /// The cheapest way into the attack menu, and the only one that needs no crew commitment beyond a
    /// car and a driver. It takes nothing, which is what makes it affordable: it is how a player softens
    /// a target they cannot yet raid.
    /// </summary>
    public int TurnCost { get; set; } = 4;
    public double HeatPerStrike { get; set; } = 4;

    /// <summary>
    /// Odds the pass finds anybody: against an empty street, then per armed body on it, then per point
    /// of firepower those bodies carry over a pistol each. A drive-by is barely contested compared with
    /// a jacking, which is the point of it - but it cannot be a certainty, or a player with one car and
    /// a full turn bank could grind any rival's crew to nothing for free.
    ///
    /// Bodies weigh more than guns here. Whether you find anybody in the open is mostly about how many
    /// people were watching the road, not what they were holding when they got behind a wall.
    /// </summary>
    public double BaseHitChance { get; set; } = 0.9;
    public double HitChancePerArmedThug { get; set; } = 0.02;
    public double HitChancePerGuardFirepower { get; set; } = 0.015;
    public double MinHitChance { get; set; } = 0.25;

    public int ThugKillsMin { get; set; } = 1;
    public int ThugKillsMax { get; set; } = 3;

    /// <summary>Morale the defender's thugs lose from being shot at in the street.</summary>
    public double ThugMoraleHit { get; set; } = 6;

    /// <summary>
    /// The odds of driving into return fire and losing the car, rising with the guard on the street and
    /// with what that guard is carrying. A floor above zero on purpose: even an undefended drive-by
    /// should sometimes cost the ride, or the move becomes free and there is no reason to stop doing it.
    ///
    /// This is the one roll in the game where the guns outweigh the bodies, and it is the honest
    /// reading: a pistol rarely stops a moving car, and a rifle very often does. It is also what gives
    /// the drive-by a ceiling - past a certain quality of guard, shooting up a street costs more cars
    /// than it is worth, whoever is driving.
    /// </summary>
    public double RideLossChance { get; set; } = 0.05;
    public double RideLossChancePerArmedThug { get; set; } = 0.01;
    public double RideLossChancePerGuardFirepower { get; set; } = 0.015;
    public double MaxRideLossChance { get; set; } = 0.45;
}

public sealed class JackOptions
{
    public int TurnCost { get; set; } = 6;
    public double HeatPerStrike { get; set; } = 3;

    /// <summary>Most rides one jacking can drive away, however thin the guard.</summary>
    public int MaxRides { get; set; } = 2;

    /// <summary>
    /// Odds of getting away with it: against a bare garage, then per armed body standing in it, then per
    /// point of firepower those bodies carry over and above a pistol each.
    ///
    /// This is the strike most sensitive to the defender's guard, and the two terms are what it is
    /// sensitive to. Bodies are eyes on the door; guns are what happens once you are seen. A body is
    /// worth slightly more than the marginal firepower of upgrading one, so hiring a guard is never
    /// worse than re-arming the guard you have - but a rifle crew still shuts a garage that the same
    /// number of pistols would only make risky.
    ///
    /// Counting only the firepower above sidearms is what stops the two terms describing the same thug
    /// twice, and it means an all-pistol guard has precisely the odds it had before guns had tiers.
    /// </summary>
    public double BaseChance { get; set; } = 0.8;
    public double ChancePerArmedThug { get; set; } = 0.035;
    public double ChancePerGuardFirepower { get; set; } = 0.03;
    public double MinChance { get; set; } = 0.05;

    /// <summary>Thugs the attacker leaves behind when the garage crew catches them.</summary>
    public int FailedThugLossesMin { get; set; }
    public int FailedThugLossesMax { get; set; } = 2;
}

public sealed class InfestOptions
{
    public int TurnCost { get; set; } = 6;
    public double HeatPerStrike { get; set; } = 2;

    /// <summary>Share of the defender's hoes exposed, before medicine. Always at least one.</summary>
    public double MinSharePercent { get; set; } = 6;
    public double MaxSharePercent { get; set; } = 14;

    /// <summary>
    /// Hoes one crate treats. This is what makes medicine a real stock decision rather than a switch:
    /// covering a big house costs a lot of money that does nothing until somebody attacks.
    /// </summary>
    public int HoesCuredPerCrate { get; set; } = 3;

    /// <summary>
    /// Hoes one dose reaches, mirroring the crate that treats them. The attacker's problem is now
    /// the defender's in reverse: covering a big house costs real money, and turning up short means
    /// only as many hoes as you brought poison for.
    /// </summary>
    public int HoesHitPerDose { get; set; } = 3;

    /// <summary>Morale hit when it lands, and the smaller one for a house whose medicine held.</summary>
    public double HoeMoraleHit { get; set; } = 8;
    public double CuredHoeMoraleHit { get; set; } = 2;
}

public sealed class PoachOptions
{
    public int TurnCost { get; set; } = 8;
    public double HeatPerStrike { get; set; } = 2;

    /// <summary>
    /// Coke it takes to tempt one hoe away, at full purity. Stepped-on product tempts fewer, through the
    /// same purity multiplier the market prices with, so a stretched pile is worse at this too.
    ///
    /// Priced at roughly twice what hiring the same hoe off the street costs. It has to carry a premium
    /// or nobody would ever hire again, and it has to stay within reach or the move is decoration: what
    /// the premium buys is a hoe who arrives regardless of your own crew's morale, out of somebody
    /// else's house.
    /// </summary>
    public int CokePerHoe { get; set; } = 10;

    /// <summary>Most hoes one run can walk away with, whatever the pile spent.</summary>
    public int MaxHoes { get; set; } = 8;

    /// <summary>
    /// How hard the defender's own morale resists. At 1 a fully happy house loses nobody at any price,
    /// which is the whole point of the move: poaching is the attack the payout slider answers, and the
    /// slider has to be able to answer it completely or nobody would ever touch it.
    /// </summary>
    public double MoraleResistance { get; set; } = 1;

    /// <summary>Morale the house loses watching people leave.</summary>
    public double HoeMoraleHit { get; set; } = 5;
}

/// <summary>
/// The shrine. What the gods ask for, and what they are worth answering.
///
/// The whole table is sized so that praying can never be a way of making money. What goes on the altar
/// is priced against net worth; what comes back is notice, mood and faith - none of which has a price
/// anywhere else in the game. A player who prays every week for a year is no richer for it, only
/// harder to raid and harder to demoralise.
/// </summary>
public sealed class PrayerOptions
{
    public int CooldownDays { get; set; } = 7;

    /// <summary>
    /// What the gods ask for, as a share of what the player is worth. Four percent is a real cost - a
    /// week's coke for a mid empire - without ever being the difference between playing and not.
    /// </summary>
    public double DemandShareOfNetWorth { get; set; } = 0.04;

    /// <summary>
    /// A floor under the scaling, so the very first prayer is a real offering rather than a token. Below
    /// this the ask would round to nothing and the ritual would be free.
    /// </summary>
    public long MinimumNetWorthForScale { get; set; } = 25_000;
    public long MinimumCashDemand { get; set; } = 500;

    /// <summary>Giving this many times what was asked opens the blessings that generosity buys.</summary>
    public int GenerousMultiplier { get; set; } = 2;
    public int GenerousBlessingMultiplier { get; set; } = 2;

    /// <summary>
    /// Thresholds for whether a blessing would actually help. A blessing landing on something the player
    /// did not need reads as nothing happening, and a weekly ritual that mostly produces nothing is a
    /// weekly ritual nobody keeps.
    /// </summary>
    public double HeatWorthClearing { get; set; } = 15;
    public double MoraleWorthLifting { get; set; } = 85;
    public double LoyaltyWorthRestoring { get; set; } = 80;

    public double MoraleBlessing { get; set; } = 8;
    public double LoyaltyBlessing { get; set; } = 12;

    /// <summary>
    /// Turns, and the one blessing rationed behind generosity. Everybody always wants more of these and
    /// nothing else in the game grants them, so they cannot be something the gods hand out for meeting
    /// a demand - they are what giving twice as much buys.
    /// </summary>
    public int TurnsBlessing { get; set; } = 25;
}

/// <summary>
/// What a crew costs and how big it may get.
/// </summary>
public sealed class AllianceOptions
{
    /// <summary>
    /// Members per crew.
    ///
    /// The source game said twenty, but it was a game with thousands of players signing up every month.
    /// This world is two dozen rivals and a handful of people, so twenty would not be an alliance, it
    /// would be everybody against nobody - and the one thing an alliance must not become is the whole
    /// board agreeing to stop playing. Sized against the population it actually has.
    /// </summary>
    public int MaxMembers { get; set; } = 6;

    /// <summary>
    /// What starting one costs. High enough that founding a crew is a decision an established player
    /// makes rather than the first thing anybody does, and it is paid once by one person.
    /// </summary>
    public long FoundingCost { get; set; } = 150_000;

    public int DefaultDuesPercent { get; set; } = 5;

    /// <summary>
    /// The ceiling on the founder's cut. A founder who could set it to everything would be running a
    /// scheme rather than a crew, and the members paying it cannot vote.
    /// </summary>
    public int MaxDuesPercent { get; set; } = 20;

    /// <summary>
    /// What a thug costs the treasury. The source game's price, and it is the first thing keeping the
    /// pool honest: at eight percent of a shift, a hundred of them is somewhere near twenty million
    /// dollars of gross street work by the whole crew. A pool is a long project, not a purchase.
    /// </summary>
    public long OffensiveThugCost { get; set; } = 15_000;
    public long DefensiveThugCost { get; set; } = 15_000;

    /// <summary>
    /// What one thug out of the pool is worth in a fight, in pistols. They arrive armed - at fifteen
    /// thousand each they are not turning up with their hands empty - so one of them is exactly an armed
    /// thug and nothing more exotic. Anything cleverer would make the pool a second combat system.
    /// </summary>
    public double ThugFirepower { get; set; } = 1;

    /// <summary>
    /// How many borrowed thugs a member may field, as a multiple of their own.
    ///
    /// This is the rule that keeps the pool from breaking the game. Alliance thugs ignore the hideout's
    /// thug cap entirely, which is the constraint every fight is balanced against - without a limit,
    /// a Trap House with a rich crew behind it could field a Penthouse army and the whole hideout ladder
    /// would stop meaning anything.
    ///
    /// Tying the limit to the member's own crew makes the pool amplify rather than substitute: you may
    /// bring as many friends as you brought yourself, so your tier still sets your ceiling and the crew
    /// only doubles it. It also means the pool is worth most to the players who have already built
    /// something, which is the right way round for a thing a crew pays for together.
    /// </summary>
    public double MaxBorrowedPerOwnThug { get; set; } = 1;

    /// <summary>What a declared war costs, runs for, scores, and pays out.</summary>
    public WarOptions War { get; set; } = new();

    /// <summary>
    /// Crews the world already has. Seeded around towns on first read, because that is the alliance a
    /// world would actually make - the people working the same streets - and it gives a player an
    /// obvious door to knock on in their own city. Three towns rather than eight on purpose: a map where
    /// everybody has agreed not to rob each other has nothing left to do.
    /// </summary>
    public List<RivalCrewOptions> RivalCrews { get; set; } = [];

    public void ApplyDefaultsWhereEmpty()
    {
        if (RivalCrews.Count > 0)
            return;

        RivalCrews =
        [
            new RivalCrewOptions
            {
                Name = "The Eastside Table",
                City = "New York",
                Motto = "We eat first and we eat together.",
                DuesPercent = 8,
                Door = "Application"
            },
            new RivalCrewOptions
            {
                Name = "Riverworks",
                City = "Detroit",
                Motto = "Nobody here is asking anybody for anything.",
                DuesPercent = 5
            },
            new RivalCrewOptions
            {
                Name = "The Causeway Set",
                City = "Miami",
                Motto = "Everything moves through us on the way to somewhere else.",
                DuesPercent = 12,
                Door = "InviteOnly"
            }
        ];
    }
}

/// <summary>A crew the world starts with, formed from the rivals already working that town.</summary>
/// <summary>
/// A crew war: a clock, a score and a pot.
///
/// Sized against what a crew can actually do inside the window rather than picked round. Two days is
/// long enough that both sides get a full evening at the screen whatever timezone they are in, and
/// short enough that a war is an event rather than a condition. A player has two attack lanes on a
/// thirty-minute cooldown, so a crew of six can mount a few dozen fights in that stretch and a score
/// in the twenties is a hard-fought one.
/// </summary>
public sealed class WarOptions
{
    /// <summary>How long a war runs once declared.</summary>
    public int DurationHours { get; set; } = 48;

    /// <summary>
    /// What the declaring crew puts on the table, out of the treasury, the moment they declare.
    ///
    /// Priced against the founding cost rather than against income: starting a crew is $150,000 and is
    /// meant to be a decision an established player makes, and picking a fight with another crew should
    /// cost about as much thought. It is also the whole of what the crew being declared on is
    /// guaranteed to win, which is why it cannot be nominal - a free declaration is an insult, and an
    /// insult is not a war.
    /// </summary>
    public long Stake { get; set; } = 250_000;

    /// <summary>
    /// What the loser's treasury pays the winner on top of the stake, and the ceiling on it.
    ///
    /// A share rather than a number, so a war between two poor crews is fought over the stake and a
    /// war between two rich ones is fought over something worth having. Capped because a crew that has
    /// been saving for a year should not be emptied by two days of raids.
    /// </summary>
    public double TributePercent { get; set; } = 15;
    public long MaxTribute { get; set; } = 5_000_000;

    /// <summary>
    /// The score it takes to win anything at all.
    ///
    /// Without it, declaring on a crew that has stopped playing is a wage: one raid nobody contests
    /// wins the war and takes a cut of whatever they had saved. A war has to be fought to be won, and
    /// this is the line under "fought".
    /// </summary>
    public int MinScoreToWin { get; set; } = 6;

    /// <summary>
    /// How long the same two crews must wait before doing it again. The second half of the answer to
    /// farming a dormant crew: even a war that is worth winning cannot be re-declared every other day.
    /// </summary>
    public int CooldownHours { get; set; } = 72;

    /// <summary>
    /// What the things a crew already does are worth once there is a war on.
    ///
    /// Nothing new is scored. A raid, a defence and a piece of ground are the three outcomes the combat
    /// system already produces, and a war is only a reason to go and produce them against one
    /// particular crew. Ground is worth the most because it is the hardest and it lasts; a defence is
    /// worth something real because a crew that only ever attacks should not beat a crew that turns
    /// every raid away.
    /// </summary>
    public int PointsForRaidWon { get; set; } = 3;
    public int PointsForDefenceHeld { get; set; } = 2;
    public int PointsForGroundTaken { get; set; } = 5;
}

public sealed class RivalCrewOptions
{
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string? Motto { get; set; }
    public int DuesPercent { get; set; } = 5;

    /// <summary>How they take people on: Open, Application, or InviteOnly.</summary>
    public string Door { get; set; } = "Open";
}

/// <summary>
/// How long a run of the world lasts, and what finishing it well is worth in the next one.
///
/// Off by default, and that is not timidity - a world already being played would otherwise wake up one
/// morning to find every empire in it deleted by a date somebody committed months earlier. Turning
/// seasons on is a decision an operator makes with their hand on the switch, and once it is on the
/// clock is public, because a season whose end nobody can name is only a rumour that the world might
/// be deleted.
/// </summary>
public sealed class SeasonOptions
{
    /// <summary>Whether the clock actually rolls the world when it runs out.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// How long a run lasts. Season 1 is a raid race, so ninety days gives people time to build a crew,
    /// pick fights, answer retaliation, and still have the winner decided by what they took.
    /// </summary>
    public int LengthDays { get; set; } = 90;

    /// <summary>
    /// When season one starts, as an absolute UTC instant. Unset means it starts whenever the first
    /// request to ask about it happens to land, which is how a world ends up counting ninety days from
    /// a deploy-time health check.
    ///
    /// Set it and the dates become something that can be announced: the start is fixed, the end is the
    /// start plus <see cref="LengthDays"/>, and both survive a restart because neither was ever a
    /// property of the process. Only season one reads it - every season after it starts when the one
    /// before it ended, which is a date the world watched happen.
    ///
    /// A date in the future is allowed and means what it says: the season exists, its clock has not
    /// started, and raids landed before it score nothing.
    /// </summary>
    public DateTime? StartsAtUtc { get; set; }

    /// <summary>
    /// Opening cash earned by last season's finish, and only last season's - it never stacks and never
    /// compounds. Paid in the one currency that stops mattering fastest: against the $5,000 everybody
    /// else opens with it is a real leg up through the first hour, and against a Warehouse it is a
    /// rounding error. A head start that lasted would be a way of winning a season by having won the
    /// one before it, which is the failure mode every seasonal game has to avoid.
    /// </summary>
    public long ChampionHeadStart { get; set; } = 50_000;
    public long TopThreeHeadStart { get; set; } = 25_000;
    public long TopTenHeadStart { get; set; } = 10_000;

    /// <summary>What a run is called before anybody names it.</summary>
    public string NameFormat { get; set; } = "Season {0}";

    /// <summary>What the head start is worth to somebody who finished here.</summary>
    public long HeadStartFor(string? honour) => honour switch
    {
        SeasonHonours.Champion => Math.Max(0, ChampionHeadStart),
        SeasonHonours.TopThree => Math.Max(0, TopThreeHeadStart),
        SeasonHonours.TopTen => Math.Max(0, TopTenHeadStart),
        _ => 0
    };
}

public sealed class AntiFarmOptions
{
    /// <summary>
    /// Below this net worth a player cannot be attacked at all. Self-limiting as a shield: bank cash
    /// counts toward net worth, so a player cannot hide wealth to stay permanently untouchable.
    /// </summary>
    public long MinDefenderNetWorth { get; set; } = 25_000;

    /// <summary>An attacker may not hit a target worth less than their own net worth over this ratio.</summary>
    public double MaxNetWorthRatio { get; set; } = 5;

    /// <summary>How far back repeat victories and hits are counted.</summary>
    public int RepeatWindowHours { get; set; } = 24;

    /// <summary>Share of the haul lost per prior victory against the same defender in the window.</summary>
    public double LootDecayPerRepeat { get; set; } = 0.4;

    /// <summary>Floor on the decay, so a repeat attack is pointless rather than forbidden.</summary>
    public double MinLootMultiplier { get; set; } = 0.1;

    /// <summary>Extra protection per hit the defender already took in the window, as a multiple of the base.</summary>
    public double ProtectionEscalationPerHit { get; set; } = 0.5;

    public int MaxProtectionMinutes { get; set; } = 360;

    /// <summary>
    /// How many attacks may be in flight against one defender at once. Escalating protection is
    /// reactive: it is set when a mission finishes, so without this cap any number of attackers can
    /// launch simultaneously and every one lands before the first shield exists.
    /// </summary>
    public int MaxIncomingAttacks { get; set; } = 2;
}

public sealed class PimpOptions
{
    public double StartingLoyalty { get; set; } = 100;

    /// <summary>Loyalty lost by the pimp who led a defeat, and gained by one who led a win.</summary>
    public double DefeatLoyaltyPenalty { get; set; } = 12;
    public double StandstillLoyaltyPenalty { get; set; } = 4;
    public double VictoryLoyaltyGain { get; set; } = 6;

    /// <summary>Per turn of street work, applied while crew morale sits below the threshold.</summary>
    public double LowMoraleLoyaltyPenaltyPerTurn { get; set; } = 0.35;
    public double LowMoraleThreshold { get; set; } = 45;
    public double PassiveRecoveryPerTick { get; set; } = 0.5;
    public double RestRecovery { get; set; } = 6;
    public double PartyRecovery { get; set; } = 9;

    /// <summary>Below this loyalty a pimp may walk out after street work.</summary>
    public double WalkOutThreshold { get; set; } = 25;
    public double MaxWalkOutChance { get; set; } = 0.15;

    /// <summary>Chance the commanding pimp dies when the attack is beaten.</summary>
    public double CommanderDeathChanceOnDefeat { get; set; } = 0.20;

    /// <summary>Chance a pimp at home dies when a defence is broken.</summary>
    public double DefenderDeathChanceOnLoss { get; set; } = 0.15;

    /// <summary>Per-pimp bonus rolled at hire, in percent.</summary>
    public int MinBonusPercent { get; set; } = 3;
    public int MaxBonusPercent { get; set; } = 8;

    /// <summary>
    /// Ceilings on the stacked bonus from pimps at home. Six Hustlers at 8% would otherwise be a 48%
    /// income swing, which is no longer a small bonus.
    /// </summary>
    public int MaxStreetBonusPercent { get; set; } = 20;
    public int MaxDefenceBonusPercent { get; set; } = 20;

    /// <summary>
    /// Cap on what an Enforcer adds to ground they are posted to. Its own number rather than the house
    /// cap: a garrison is a handful of thugs, so the same percentage is worth far less in absolute
    /// terms, and holding ground should be worth putting a good pimp on.
    /// </summary>
    public int MaxGarrisonBonusPercent { get; set; } = 85;
}

/// <summary>
/// Hideout tuning tables. These start empty on purpose: the configuration binder appends to a
/// pre-populated <see cref="List{T}"/> instead of replacing it, so shipping defaults in the
/// initializers would merge them with appsettings and let the stale default win the level lookup.
/// Call <see cref="ApplyDefaultsWhereEmpty"/> after binding to fill in whatever config omitted.
/// </summary>
public sealed class HideoutOptions
{
    public List<HideoutTierOptions> Tiers { get; set; } = [];
    public List<StorageLevelOptions> Storage { get; set; } = [];
    public List<SafeLevelOptions> Safe { get; set; } = [];
    public List<LabLevelOptions> WeedLab { get; set; } = [];
    public List<LabLevelOptions> CokeLab { get; set; } = [];
    public List<WorkshopLevelOptions> Workshop { get; set; } = [];
    public List<IntelligenceLevelOptions> Intelligence { get; set; } = [];
    public List<LookoutLevelOptions> Lookout { get; set; } = [];

    /// <summary>What scouting somebody else's house costs, and how long the answer is worth having.</summary>
    public IntelOptions Intel { get; set; } = new();

    /// <summary>
    /// The intelligence centre level, or zero. Here rather than in the service that asks, because the
    /// question "how much of a building does this player have" is the options' to answer.
    ///
    /// What is standing rather than what was bought. Scouting is people going and looking, and there
    /// is nobody to send out of a room that has been put through a wall.
    /// </summary>
    public int LevelOfIntelligence(Hideout? hideout) => hideout?.WorkingLevel(HideoutRooms.Intelligence) ?? 0;

    /// <summary>
    /// The turn bank a building of this size holds, never below the opening one.
    ///
    /// Read as the best of every tier at or below the one standing rather than the row that matches
    /// it, so the ladder can only ever climb. A table that left a middle row's number out would
    /// otherwise take a bank away from somebody for upgrading, which is the one thing an upgrade must
    /// never do.
    /// </summary>
    public int TurnBankFor(Hideout? hideout, int opening)
        => TurnBankAtTier(hideout?.Tier ?? 1, opening);

    /// <summary>The same answer for a building nobody has bought yet, so an upgrade can be advertised.</summary>
    public int TurnBankAtTier(int tier, int opening)
    {
        var held = opening;
        foreach (var candidate in Tiers)
            if (candidate.Level <= tier && candidate.MaxTurns > held)
                held = candidate.MaxTurns;
        return held;
    }

    /// <summary>
    /// How much notice each contraband good draws per unit held. Weighted rather than flat because
    /// they are not equally incriminating: a coke lab's output is the worst thing to be found with,
    /// while cut is mostly baking soda and barely registers despite where it is made.
    ///
    /// Sized against the storage rooms the game actually ships rather than against nothing. At the
    /// first tuning coke drew a point a unit, so filling a Warehouse store with 85 coke put a player
    /// at 85 heat - Hunted - for doing nothing but using the room they had bought. A full store should
    /// be worth watching, not a death sentence: a Warehouse of coke now reads around 30, and only
    /// hoarding a maxed Penthouse store of everything reaches Hunted on stock alone.
    /// </summary>
    public double CokeHeatPerUnit { get; set; } = 0.35;
    public double MoonshineHeatPerUnit { get; set; } = 0.25;
    public double WeedHeatPerUnit { get; set; } = 0.1;
    public double CutHeatPerUnit { get; set; } = 0.03;

    /// <summary>
    /// Standing crew heat. Bodies in the house are a footprint even before a shift starts: one new
    /// player stays quiet, while a packed Penthouse is itself enough for the street to know.
    /// </summary>
    public double PimpHeat { get; set; } = 0.4;
    public double HoeHeat { get; set; } = 0.03;
    public double ThugHeat { get; set; } = 0.08;

    /// <summary>
    /// Units of coke a turn of stepping on it can stretch, per level of the mix house.
    ///
    /// Cutting is mixing rather than manufacturing, so it goes far faster than making the cut did: a
    /// run of coke off a plane should be stretchable in an evening, not over days. The mix house level
    /// scales it, which gives that room a second reason to exist beyond making the cut in the first
    /// place.
    /// </summary>
    public int CutPerTurnPerMixLevel { get; set; } = 10;


    /// <summary>
    /// Working the streets draws attention of its own, whether or not anything is held.
    ///
    /// Sized against the turn bank, which is the thing it is actually charged against. At half a point
    /// a turn a full 200-turn bank earned 100 heat in one sitting, so an ordinary evening of work took
    /// a player who held nothing at all from Quiet to Hunted, and decay of three an hour could never
    /// catch up. A heavy day should get you noticed and then fade overnight, which is what this does:
    /// the whole bank is about 30, and ten quiet hours clears it.
    /// </summary>
    public double HeatPerStreetTurn { get; set; } = 0.15;

    /// <summary>Earned heat cools on its own, which is what makes laying low a real option.</summary>
    public double HeatDecayPerHour { get; set; } = 3;

    /// <summary>Below this nobody is looking. Above it, every hour is a roll.</summary>
    public double HeatBustFloor { get; set; } = 20;

    /// <summary>
    /// Chance per hour per point of heat over the floor, and the ceiling on that chance.
    ///
    /// At the old rate a Hunted house sat at about a one-in-six hour, which is four or five hours of
    /// play before the door goes in - long enough that the sensible move was to ignore the number and
    /// keep working. Doubled, a hundred heat is roughly a one-in-three hour and the ceiling is a coin
    /// flip: being Hunted is now a thing you deal with tonight rather than a warning you outrun.
    /// </summary>
    public double BustChancePerHeat { get; set; } = 0.004;
    public double MaxBustChancePerHour { get; set; } = 0.5;

    /// <summary>
    /// Where the bands sit, as multiples of the floor. Noticed starts at the floor, Watched at twice
    /// it, Hunted at four times. Read by <see cref="HeatBands"/> rather than written out again in the
    /// mapper that prints the word, which is where they used to live as two literals.
    /// </summary>
    public double WatchedHeatMultiple { get; set; } = 2;
    public double HuntedHeatMultiple { get; set; } = 4;

    /// <summary>Share of every contraband pile taken when it happens, and the fine per unit lost.</summary>
    public double SeizedPercent { get; set; } = 0.65;

    /// <summary>What they take instead from a house they have been watching all week.</summary>
    public double SeizedPercentWhenHunted { get; set; } = 0.85;

    /// <summary>
    /// How far one raid's luck moves that share, down and up.
    ///
    /// The share used to be exactly what the band said, so every raid at a given band was the same
    /// raid and the only question heat ever asked was whether tonight was the night. A raid is a crew
    /// going through a house in a hurry: sometimes they find the floorboard and sometimes they walk
    /// past it. Skewed upward on purpose - a bad night takes a little less, a good one takes the lot,
    /// and at Hunted the top of the range is everything you were holding.
    /// </summary>
    public double SeizedRollDown { get; set; } = 0.15;
    public double SeizedRollUp { get; set; } = 0.35;

    /// <summary>
    /// Charged per unit carried out of the door, on top of losing the unit.
    /// </summary>
    /// <remarks>
    /// Forty a unit made the fine the small half of a raid - a stash worth thousands cost a few
    /// hundred, so the loss was the goods and the court was an afterthought. At a hundred the fine is
    /// the part that hurts a player who was holding cheap volume, which is the hole the old number
    /// left: weed was near enough free to sit on.
    /// </remarks>
    public double FinePerSeizedUnit { get; set; } = 100;

    /// <summary>
    /// Rooms a raid puts out of action, by band. Nothing at all below Watched: see
    /// <see cref="HeatBands.RoomsWrecked"/> for why the low bands stay a matter of stock and fines.
    /// </summary>
    public int RoomsWreckedWhenWatched { get; set; } = 1;
    public int RoomsWreckedWhenHunted { get; set; } = 2;

    /// <summary>
    /// What putting a room back costs, as a share of every pound that built it to the level it is.
    ///
    /// A share rather than a price list, because a repair bill has to track the room. A third of a
    /// maxed coke lab is millions and a third of a first-rung lookout is pocket money, and both of
    /// those are the right answer: the bigger the thing that was taken away, the more it was worth
    /// per hour and the more it is worth paying to get back.
    /// </summary>
    public double RepairCostPercent { get; set; } = 0.35;

    /// <summary>
    /// How long the crew are in there, per level of the room, and the least it can ever take.
    ///
    /// Deliberately measured in an evening rather than in days. The cost of a wrecked room is the
    /// hours it was not running plus the money, and stretching the clock past that turns one bad
    /// night into a weekend somebody spends locked out of their own hideout - which is how a setback
    /// becomes a reason to stop playing.
    /// </summary>
    public int RepairMinutesPerLevel { get; set; } = 20;
    public int MinRepairMinutes { get; set; } = 15;

    /// <summary>
    /// How much passive lab output can pile up while a player is away. Past this the labs sit idle, so
    /// the hideout is a reason to come back rather than a reason to stay gone.
    /// </summary>
    public int MaxOfflineProductionHours { get; set; } = 12;

    /// <summary>
    /// The lab level at which it will move its own output instead of shelving it.
    ///
    /// Gated rather than free because selling without you is a real convenience: it turns the one
    /// asset a raid can always take into the one it can never touch. A first-rung lab is a grow in a
    /// cupboard and it has nobody to sell to; by the third it is an operation with a buyer.
    /// </summary>
    public int MinLabLevelForAutoSell { get; set; } = 3;

    public void ApplyDefaultsWhereEmpty()
    {
        // Each tier's crew caps are what the storage level it unlocks is sized against, so a full-length
        // action is always exactly supplyable at the top of a tier and never more than that.
        //
        // These are what the building has room for. What the crew can actually be is the smaller of this
        // and what the storage room supplies - see HideoutService.CapacityFor - so a tier's numbers are a
        // ceiling to work towards rather than a promise made on the day you move in.
        if (Tiers.Count == 0)
            Tiers =
            [
                // The turn banks are hours away from the screen, not round numbers. At twelve turns an
                // hour the opening 200 is gone in under seventeen, which is a night's sleep and a
                // morning; 300 is a whole day, 450 is a day and a half, and 650 is a weekend. Each one
                // is what that building is actually promising: that being away from it costs nothing.
                new HideoutTierOptions { Level = 1, Name = "Trap House", MaxPimps = 6, MaxHoes = 50, MaxThugs = 25, MaxRides = 2 },
                new HideoutTierOptions { Level = 2, Name = "Warehouse", MaxTurns = 300, MaxPimps = 10, MaxHoes = 85, MaxThugs = 45, MaxRides = 5, UpgradeCost = 300_000, UpgradeTurns = 40, BuildMinutes = 30 },
                new HideoutTierOptions { Level = 3, Name = "Nightclub", MaxTurns = 450, MaxPimps = 15, MaxHoes = 130, MaxThugs = 70, MaxRides = 9, UpgradeCost = 1_500_000, UpgradeTurns = 80, BuildMinutes = 120 },
                new HideoutTierOptions { Level = 4, Name = "Penthouse", MaxTurns = 650, MaxPimps = 22, MaxHoes = 200, MaxThugs = 110, MaxRides = 15, UpgradeCost = 7_200_000, UpgradeTurns = 120, BuildMinutes = 360 }
            ];

        // Every level holds a full 20-turn action for the crew it supports: condoms at one per 12 turns
        // each, beer at one per 10, weapons covering every thug, and weed and coke at 2x and 1x the hoes.
        //
        // The ladder is the crew ladder now, because the store is what decides how big a crew can be. It
        // used to open at a room that supplied four turns of a crew the building would happily let you
        // hire - fifty hoes fed by seventeen condoms - which is a room you have outgrown before you have
        // understood what it was for. It opens at a working crew of 25 and climbs to the biggest house.
        if (Storage.Count == 0)
            Storage =
            [
                // 25 hoes and 12 thugs, a full action each: the smallest crew worth calling a crew.
                new StorageLevelOptions { Level = 1, Condoms = 42, Beer = 25, Weapons = 12, Weed = 50, Coke = 25, Moonshine = 25, Cut = 25, Medicine = 9, Poison = 9 },
                // 50 and 25, which is everything a Trap House has room for. The building is the ceiling
                // from here rather than the room, and moving out is the only way up.
                new StorageLevelOptions { Level = 2, Condoms = 84, Beer = 50, Weapons = 25, Weed = 100, Coke = 50, Moonshine = 50, Cut = 50, Medicine = 17, Poison = 17, UpgradeCost = 22_000 },
                new StorageLevelOptions { Level = 3, MinTier = 2, Condoms = 142, Beer = 90, Weapons = 45, Weed = 170, Coke = 85, Moonshine = 90, Cut = 85, Medicine = 29, Poison = 29, UpgradeCost = 125_000 },
                new StorageLevelOptions { Level = 4, MinTier = 3, Condoms = 217, Beer = 140, Weapons = 70, Weed = 260, Coke = 130, Moonshine = 140, Cut = 130, Medicine = 44, Poison = 44, UpgradeCost = 600_000 },
                new StorageLevelOptions { Level = 5, MinTier = 4, Condoms = 334, Beer = 220, Weapons = 110, Weed = 400, Coke = 200, Moonshine = 220, Cut = 200, Medicine = 67, Poison = 67, UpgradeCost = 2_200_000 },
                // Nothing above supplies a bigger crew, because no building holds one. What the last
                // upgrade buys is room for product, which is the only thing left to want.
                new StorageLevelOptions { Level = 6, MinTier = 4, Condoms = 334, Beer = 220, Weapons = 110, Weed = 600, Coke = 300, Moonshine = 330, Cut = 300, Medicine = 67, Poison = 67, UpgradeCost = 7_000_000 }
            ];

        if (Safe.Count == 0)
            Safe =
            [
                new SafeLevelOptions { Level = 1, MaxCash = 50_000 },
                new SafeLevelOptions { Level = 2, MaxCash = 100_000, UpgradeCost = 60_000 },
                new SafeLevelOptions { Level = 3, MinTier = 2, MaxCash = 350_000, UpgradeCost = 300_000 },
                new SafeLevelOptions { Level = 4, MinTier = 3, MaxCash = 1_000_000, UpgradeCost = 1_200_000 },
                new SafeLevelOptions { Level = 5, MinTier = 4, MaxCash = 3_000_000, UpgradeCost = 4_950_000 }
            ];

        // PassivePerHour is deliberately below what the same lab yields through production turns: about
        // half a day of accrual matches one full-length production run, so being away is worth something
        // without being worth more than playing.
        if (WeedLab.Count == 0)
            WeedLab =
            [
                new LabLevelOptions { Level = 1, YieldBonusPercent = 25, PassivePerHour = 2, UpgradeCost = 10_000 },
                new LabLevelOptions { Level = 2, YieldBonusPercent = 60, PassivePerHour = 4, UpgradeCost = 45_000 },
                new LabLevelOptions { Level = 3, YieldBonusPercent = 110, PassivePerHour = 7, UpgradeCost = 210_000 },
                new LabLevelOptions { Level = 4, MinTier = 3, YieldBonusPercent = 170, PassivePerHour = 11, UpgradeCost = 1_000_000 },
                new LabLevelOptions { Level = 5, MinTier = 4, YieldBonusPercent = 240, PassivePerHour = 16, UpgradeCost = 3_850_000 }
            ];

        // Throughput and nothing else. What a gun costs to make belongs to the gun, not to the room, so
        // a level buys guns per turn and which guns are unlocked - the prices live on the tier table,
        // each set under what the shop charges, because a maker who cannot undercut the shop has nothing
        // to sell and the whole point of the workshop is to give the market a good with real demand.
        // One making room instead of three. The workshop, the still and the mix house were the same
        // room wearing different signs - turns and materials in, one good out - and two of them
        // dead-ended at the second building with two levels each, maxed in an afternoon and never
        // thought about again. The room now buys throughput and reach, and what a thing costs to make
        // belongs to the thing, which is what the guns had been saying all along.
        //
        // Priced to absorb what all three used to cost together, so nobody who had built them is out
        // of pocket and nobody who had not gets a discount.
        if (Workshop.Count == 0)
            Workshop =
            [
                new WorkshopLevelOptions { Level = 1, Throughput = 1, UpgradeCost = 40_000 },
                new WorkshopLevelOptions { Level = 2, Throughput = 2, UpgradeCost = 165_000 },
                new WorkshopLevelOptions { Level = 3, MinTier = 2, Throughput = 3, UpgradeCost = 750_000 },
                new WorkshopLevelOptions { Level = 4, MinTier = 3, Throughput = 4, UpgradeCost = 2_200_000 }
            ];


        // The lookout fills the one hole in the first tier's ladder. Everything else a Trap House can
        // buy is more of something it already has; this is the only answer it has to heat.
        if (Lookout.Count == 0)
            Lookout =
            [
                new LookoutLevelOptions { Level = 1, MinTier = 1, BustChanceReductionPercent = 25, UpgradeCost = 100_000 },
                new LookoutLevelOptions { Level = 2, MinTier = 2, BustChanceReductionPercent = 45, UpgradeCost = 390_000 },
                new LookoutLevelOptions { Level = 3, MinTier = 3, BustChanceReductionPercent = 60, UpgradeCost = 1_750_000 }
            ];

        if (Intelligence.Count == 0)
            Intelligence =
            [
                new IntelligenceLevelOptions { Level = 1, MinTier = 2, ConcurrentRuns = 1, RiskReductionPercent = 10, UpgradeCost = 120_000 },
                new IntelligenceLevelOptions { Level = 2, MinTier = 2, ConcurrentRuns = 2, RiskReductionPercent = 20, UpgradeCost = 480_000 },
                new IntelligenceLevelOptions { Level = 3, MinTier = 3, ConcurrentRuns = 3, RiskReductionPercent = 30, UpgradeCost = 1_875_000 },
                new IntelligenceLevelOptions { Level = 4, MinTier = 4, ConcurrentRuns = 5, RiskReductionPercent = 40, UpgradeCost = 6_400_000 }
            ];

        if (CokeLab.Count == 0)
            CokeLab =
            [
                new LabLevelOptions { Level = 1, YieldBonusPercent = 25, PassivePerHour = 1, UpgradeCost = 25_000 },
                new LabLevelOptions { Level = 2, YieldBonusPercent = 60, PassivePerHour = 2, UpgradeCost = 90_000 },
                new LabLevelOptions { Level = 3, YieldBonusPercent = 110, PassivePerHour = 3, UpgradeCost = 375_000 },
                new LabLevelOptions { Level = 4, MinTier = 3, YieldBonusPercent = 170, PassivePerHour = 5, UpgradeCost = 1_800_000 },
                new LabLevelOptions { Level = 5, MinTier = 4, YieldBonusPercent = 240, PassivePerHour = 7, UpgradeCost = 6_600_000 }
            ];
    }
}

/// <summary>
/// What ground is worth and what it costs to sit on. The map itself starts empty for the same reason
/// the hideout tables do: the configuration binder appends to a pre-populated list rather than
/// replacing it, so shipping defaults in the initialiser would merge them with appsettings.
/// </summary>
/// <summary>
/// The player-to-player board. It exists because turns are scarcer than cash: somebody with turns and
/// no money makes goods, somebody with money and no turns buys them.
/// </summary>
public sealed class MarketOptions
{
    /// <summary>
    /// The house's cut of every sale. A money sink the game otherwise lacks, and a reason not to churn
    /// stock back and forth through the board for the sake of it.
    /// </summary>
    public int HouseCutPercent { get; set; } = 5;

    /// <summary>Stops one player papering the board over.</summary>
    public int MaxListingsPerPlayer { get; set; } = 10;

    /// <summary>
    /// How far a price may sit from what the game itself pays, as a multiple. A wide band on purpose:
    /// it is there to stop a mistyped price poisoning the board, not to set the price.
    /// </summary>
    public double MinPriceMultiplier { get; set; } = 0.25;
    public double MaxPriceMultiplier { get; set; } = 4;

    public int MaxQuantityPerListing { get; set; } = 10_000;
}

public sealed class CityMarketOptions
{
    public List<CityMarketProfileOptions> Profiles { get; set; } = [];
    public double CheapMultiplier { get; set; } = 0.75;
    public double MediumMultiplier { get; set; } = 1.0;
    public double HighMultiplier { get; set; } = 1.25;
    public double ExpensiveMultiplier { get; set; } = 1.5;
    /// <summary>
    /// Trip length for a profile that does not set its own <see cref="CityMarketProfileOptions.TravelTurns"/>.
    /// Still keyed by risk so older configuration keeps working, but distance and danger are separate
    /// numbers now: a short run into a bad town should cost few turns and still be the one that hurts.
    /// </summary>
    public int LowRiskTravelTurns { get; set; } = 2;
    public int MediumRiskTravelTurns { get; set; } = 4;
    public int HighRiskTravelTurns { get; set; } = 6;

    /// <summary>Chance a run into the town is stopped, by risk band.</summary>
    public double LowRiskBustChance { get; set; } = 0.05;
    public double MediumRiskBustChance { get; set; } = 0.12;
    public double HighRiskBustChance { get; set; } = 0.22;

    /// <summary>
    /// How much notice a town takes of you, by risk band.
    ///
    /// Risk used to describe only the way into a town: it decided whether a run was stopped at the
    /// door and nothing at all about living there. So a player in Detroit and a player in New York ran
    /// exactly the same daily operation at exactly the same danger, and the choice of town was a price
    /// list rather than a place. This is what makes it a place: the same stash and the same shift draw
    /// more attention in a watchful town than a quiet one.
    ///
    /// It pairs with what a town pays. The high-risk towns are the ones that sell dear - New York,
    /// Chicago, Las Vegas - so the trade is legible: earn more per unit, get noticed faster.
    /// </summary>
    public double LowRiskHeatMultiplier { get; set; } = 0.7;
    public double MediumRiskHeatMultiplier { get; set; } = 1.0;
    public double HighRiskHeatMultiplier { get; set; } = 1.4;

    /// <summary>How hard this town looks at you, as a multiple of the ordinary rate.</summary>
    public double HeatMultiplier(string? city)
        => ProfileFor(city).Risk?.Trim().ToLowerInvariant() switch
        {
            "low" => LowRiskHeatMultiplier,
            "high" => HighRiskHeatMultiplier,
            _ => MediumRiskHeatMultiplier
        };

    /// <summary>
    /// Share of the load taken when a run is stopped, rolled per trip. The top of the range has to
    /// clear a route's break-even share (1 - homePrice/destPrice, up to 40% on the shipped map) or a
    /// stop on the best runs costs less than staying home would have, and risk stops meaning anything.
    /// </summary>
    public double SeizureMinPercent { get; set; } = 0.20;
    public double SeizureMaxPercent { get; set; } = 0.60;

    /// <summary>
    /// Below this carried value a stop takes nothing. A player moving pocket change is not worth
    /// searching, and it keeps a first haul from being wiped before there is anything to bank.
    /// </summary>
    public long MinimumCarriedValueToBust { get; set; } = 2_000;

    public void ApplyDefaultsWhereEmpty(IReadOnlyList<string> cities)
    {
        if (Profiles.Count == 0)
            Profiles =
            [
                new CityMarketProfileOptions { City = "Miami", Weed = "Cheap", Coke = "Expensive", Risk = "Medium", TravelTurns = 5 },
                new CityMarketProfileOptions { City = "New York", Weed = "Medium", Coke = "High", Risk = "High", TravelTurns = 4 },
                new CityMarketProfileOptions { City = "Detroit", Weed = "Cheap", Coke = "Medium", Risk = "Low", TravelTurns = 2 },
                new CityMarketProfileOptions { City = "Los Angeles", Weed = "Medium", Coke = "Cheap", Risk = "Medium", TravelTurns = 6 },
                new CityMarketProfileOptions { City = "Chicago", Weed = "High", Coke = "Medium", Risk = "High", TravelTurns = 3 },
                // Vegas is where product is spent rather than made: tourist money, and eyes everywhere.
                new CityMarketProfileOptions { City = "Las Vegas", Weed = "Medium", Coke = "Expensive", Risk = "High", TravelTurns = 5 },
                // Atlanta is a distribution town, so weed is cheap and it is close to everything.
                new CityMarketProfileOptions { City = "Atlanta", Weed = "Cheap", Coke = "Medium", Risk = "Medium", TravelTurns = 3 },
                // Houston takes it off the water, which makes it the second place coke is cheap.
                new CityMarketProfileOptions { City = "Houston", Weed = "Medium", Coke = "Cheap", Risk = "Medium", TravelTurns = 4 }
            ];

        foreach (var city in cities)
            if (!Profiles.Any(x => string.Equals(x.City, city, StringComparison.OrdinalIgnoreCase)))
                Profiles.Add(new CityMarketProfileOptions { City = city, Weed = "Medium", Coke = "Medium", Risk = "Medium" });
    }

    public CityMarketProfileOptions ProfileFor(string? city)
        => Profiles.FirstOrDefault(x => string.Equals(x.City, city?.Trim(), StringComparison.OrdinalIgnoreCase))
           ?? Profiles.FirstOrDefault()
           ?? new CityMarketProfileOptions { City = city?.Trim() ?? "New York", Weed = "Medium", Coke = "Medium", Risk = "Medium" };

    public string? ResolveCity(string? city)
        => Profiles.FirstOrDefault(x => string.Equals(x.City, city?.Trim(), StringComparison.OrdinalIgnoreCase))?.City;

    public int ProductPrice(string? city, string product, int basePrice)
    {
        var profile = ProfileFor(city);
        var band = product.Trim().ToLowerInvariant() == "weed" ? profile.Weed : profile.Coke;
        return Math.Max(1, (int)Math.Round(basePrice * MultiplierFor(band), MidpointRounding.AwayFromZero));
    }

    public int TravelTurns(string? city)
    {
        var profile = ProfileFor(city);
        return Math.Max(1, profile.TravelTurns ?? RiskTravelTurns(profile.Risk));
    }

    public double BustChance(string? city)
        => RiskBustChance(ProfileFor(city).Risk);

    public int BustChancePercent(string? city)
        => (int)Math.Round(BustChance(city) * 100, MidpointRounding.AwayFromZero);

    private double MultiplierFor(string? band)
        => band?.Trim().ToLowerInvariant() switch
        {
            "cheap" => CheapMultiplier,
            "medium" => MediumMultiplier,
            "high" => HighMultiplier,
            "expensive" => ExpensiveMultiplier,
            _ => MediumMultiplier
        };

    private int RiskTravelTurns(string? risk)
        => risk?.Trim().ToLowerInvariant() switch
        {
            "low" => LowRiskTravelTurns,
            "high" => HighRiskTravelTurns,
            _ => MediumRiskTravelTurns
        };

    private double RiskBustChance(string? risk)
        => Math.Clamp(risk?.Trim().ToLowerInvariant() switch
        {
            "low" => LowRiskBustChance,
            "high" => HighRiskBustChance,
            _ => MediumRiskBustChance
        }, 0, 1);
}

public sealed class CityMarketProfileOptions
{
    public string City { get; set; } = string.Empty;
    public string Weed { get; set; } = "Medium";
    public string Coke { get; set; } = "Medium";

    /// <summary>How likely a run into this town is stopped. No longer decides the trip length.</summary>
    public string Risk { get; set; } = "Medium";

    /// <summary>How far the town is, in turns. Falls back to the risk-keyed default when unset.</summary>
    public int? TravelTurns { get; set; }
}

public sealed class TerritoryOptions
{
    /// <summary>
    /// The towns a player can set up in. Derived from the map so the two cannot drift: a city with no
    /// ground would be a town where the territory page is empty.
    /// </summary>
    public IReadOnlyList<string> Cities()
        => Map.Select(x => x.City).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();

    /// <summary>
    /// Where somebody starts when they did not pick.
    ///
    /// It used to be whichever town the alphabet put first, which is not a decision anybody made - and
    /// it landed new players in Atlanta, at a narrow counter, on their first evening. New York is the
    /// one shop in the country that carries the whole shelf at exactly the list price, so the town a
    /// player learns the game in is the one where nothing about the shop is a special case yet.
    /// </summary>
    public string StartingCity { get; set; } = "New York";

    /// <summary>The starting town if the map has it, and the first town on the map if it does not.</summary>
    public string StartingCityOrFirst()
    {
        var cities = Cities();
        return cities.FirstOrDefault(x => string.Equals(x, StartingCity, StringComparison.OrdinalIgnoreCase))
               ?? cities.FirstOrDefault()
               ?? "New York";
    }

    /// <summary>Thugs needed to hold anything at all. Below this the ground is given up.</summary>
    public int MinimumGarrison { get; set; } = 5;

    /// <summary>Most thugs one piece of ground can hold.</summary>
    public int MaxGarrisonThugs { get; set; } = 50;

    /// <summary>Most thugs one territory raid can send at a garrison.</summary>
    public int MaxRaidThugs { get; set; } = 100;

    /// <summary>Turns spent claiming ground nobody holds. Taking it off somebody costs a mission.</summary>
    public int ClaimTurnCost { get; set; } = 5;

    /// <summary>
    /// How long ground is safe after changing hands. Anti-farm's wealth rules do not apply here,
    /// because taking a corner is not robbing anyone, so this is the only thing stopping two players
    /// trading the same ground every time their lanes come free.
    /// </summary>
    public int HoldCooldownMinutes { get; set; } = 60;

    /// <summary>How many pieces of ground each hideout tier can hold at once.</summary>
    public List<TerritoryTierCapOptions> TierCaps { get; set; } = [];

    /// <summary>
    /// The ladder a piece of ground can be worked up. Empty here for the same reason every other table
    /// is: the binder appends rather than replaces.
    /// </summary>
    public List<TerritoryDevelopmentOptions> Development { get; set; } = [];

    /// <summary>The rung this ground is standing on, or null for ground nobody has put anything into.</summary>
    public TerritoryDevelopmentOptions? DevelopmentAt(int level)
        => level <= 0 ? null : Development.FirstOrDefault(x => x.Level == level);

    /// <summary>The next rung up, or null at the top of the ladder.</summary>
    public TerritoryDevelopmentOptions? DevelopmentAfter(int level)
        => Development.FirstOrDefault(x => x.Level == level + 1);

    /// <summary>
    /// The highest rung a building of this size is allowed to run, which is what a captured piece of
    /// ground is cut down to when the winner's house is smaller than the loser's was.
    /// </summary>
    public int MaxDevelopmentForTier(int tier)
    {
        var best = 0;
        foreach (var level in Development)
            if (level.MinTier <= tier && level.Level > best)
                best = level.Level;
        return best;
    }

    /// <summary>
    /// What this ground multiplies its type's effect by. One for bare ground, so every piece on the
    /// map reads exactly as it did before anybody spent anything.
    /// </summary>
    public double DevelopmentMultiplier(int level)
        => 1 + Math.Max(0, DevelopmentAt(level)?.EffectPercent ?? 0) / 100.0;

    /// <summary>What the work adds to the garrison standing on it, as a percentage of their strength.</summary>
    public int DevelopmentDefencePercent(int level)
        => Math.Max(0, DevelopmentAt(level)?.DefencePercent ?? 0);

    public List<TerritoryTypeOptions> Types { get; set; } = [];
    public List<TerritoryCityControlOptions> CityControl { get; set; } = [];
    public List<TerritorySeedOptions> Map { get; set; } = [];

    public void ApplyDefaultsWhereEmpty()
    {
        if (TierCaps.Count == 0)
            TierCaps =
            [
                new TerritoryTierCapOptions { Tier = 1, MaxTerritories = 1 },
                new TerritoryTierCapOptions { Tier = 2, MaxTerritories = 2 },
                new TerritoryTierCapOptions { Tier = 3, MaxTerritories = 3 },
                new TerritoryTierCapOptions { Tier = 4, MaxTerritories = 4 }
            ];

        // The ladder a piece of ground is worked up, and the only thing in the game priced to be
        // months rather than days.
        //
        // A corner was worth the same fifteen percent on the day it was taken as it was a season
        // later, and a player at the top of the tier ladder held their four pieces and was finished
        // with the map for good. There was nothing to put money into and nothing to come and take.
        //
        // The prices are deliberately steep at the top and the return there is deliberately poor -
        // the same thing the late hideout rooms are, and said out loud there too: they exist to absorb
        // money from players who have run out of things to buy. Forty-two million for one maxed piece
        // against seven for the biggest building in the game, and the whole ladder doubles what the
        // ground is worth rather than multiplying it out of sight.
        //
        // The defence percentages are what stops all of that being a target painted on a player's own
        // back. Money in the ground buys some of the reason you get to keep it, and a fully worked
        // piece fights at half again what bare ground does.
        //
        // Tier-gated like the hideout rooms, so the map's depth opens at the same pace as everything
        // else, and a building that could never have built a level is never left holding one.
        if (Development.Count == 0)
            Development =
            [
                new TerritoryDevelopmentOptions { Level = 1, Name = "Staked Out", MinTier = 1, Cost = 150_000, Turns = 10, BuildMinutes = 30, EffectPercent = 20, DefencePercent = 8 },
                new TerritoryDevelopmentOptions { Level = 2, Name = "Established", MinTier = 2, Cost = 600_000, Turns = 20, BuildMinutes = 120, EffectPercent = 40, DefencePercent = 16 },
                new TerritoryDevelopmentOptions { Level = 3, Name = "Entrenched", MinTier = 3, Cost = 2_400_000, Turns = 40, BuildMinutes = 360, EffectPercent = 60, DefencePercent = 24 },
                new TerritoryDevelopmentOptions { Level = 4, Name = "Locked Down", MinTier = 4, Cost = 9_000_000, Turns = 60, BuildMinutes = 720, EffectPercent = 80, DefencePercent = 32 },
                new TerritoryDevelopmentOptions { Level = 5, Name = "Untouchable", MinTier = 4, Cost = 30_000_000, Turns = 90, BuildMinutes = 1_440, EffectPercent = 100, DefencePercent = 40 }
            ];

        // Every effect is a percentage on an activity the player still spends turns on. Nothing here
        // pays out on its own: the labs already fill that role and needed two separate bounds to stay
        // sane, and a second idle earner would be one more thing to hold rather than a reason to play.
        if (Types.Count == 0)
            Types =
            [
                new TerritoryTypeOptions { Type = "corner", Label = "Corner", StreetIncomePercent = 15 },
                new TerritoryTypeOptions { Type = "dock", Label = "Docks", ProductionYieldPercent = 20 },
                new TerritoryTypeOptions { Type = "club", Label = "Club", MoraleRecoveryPercent = 50 },
                new TerritoryTypeOptions { Type = "stash", Label = "Stash House", LootPercent = 20 }
            ];

        if (CityControl.Count == 0)
            CityControl =
            [
                new TerritoryCityControlOptions { City = "Detroit", BonusThugs = 10 },
                new TerritoryCityControlOptions { City = "Atlanta", BonusThugs = 12 },
                new TerritoryCityControlOptions { City = "Houston", BonusThugs = 14 },
                new TerritoryCityControlOptions { City = "Chicago", BonusThugs = 16 },
                new TerritoryCityControlOptions { City = "Miami", BonusThugs = 18 },
                new TerritoryCityControlOptions { City = "Los Angeles", BonusThugs = 20 },
                new TerritoryCityControlOptions { City = "Las Vegas", BonusThugs = 22 },
                new TerritoryCityControlOptions { City = "New York", BonusThugs = 24 }
            ];

        // Six pieces per city, and every city carries all four types so nowhere is starved of an
        // effect. Ground is contested inside a city only, so a thin map would mean a town where
        // nothing is worth fighting over.
        if (Map.Count == 0)
            Map =
            [
                new TerritorySeedOptions { Name = "Hunts Point", City = "New York", Type = "corner" },
                new TerritorySeedOptions { Name = "Bed-Stuy Blocks", City = "New York", Type = "corner" },
                new TerritorySeedOptions { Name = "Red Hook Docks", City = "New York", Type = "dock" },
                new TerritorySeedOptions { Name = "Sunset Pier", City = "New York", Type = "dock" },
                new TerritorySeedOptions { Name = "The Deuce", City = "New York", Type = "club" },
                new TerritorySeedOptions { Name = "Fulton Stash", City = "New York", Type = "stash" },

                new TerritorySeedOptions { Name = "Eight Mile Strip", City = "Detroit", Type = "corner" },
                new TerritorySeedOptions { Name = "Cass Corridor", City = "Detroit", Type = "corner" },
                new TerritorySeedOptions { Name = "Delray Docks", City = "Detroit", Type = "dock" },
                new TerritorySeedOptions { Name = "The Grande", City = "Detroit", Type = "club" },
                new TerritorySeedOptions { Name = "Riverside Yard", City = "Detroit", Type = "stash" },
                new TerritorySeedOptions { Name = "Packard Lot", City = "Detroit", Type = "stash" },

                new TerritorySeedOptions { Name = "Southside Blocks", City = "Chicago", Type = "corner" },
                new TerritorySeedOptions { Name = "Cabrini Corner", City = "Chicago", Type = "corner" },
                new TerritorySeedOptions { Name = "Calumet Docks", City = "Chicago", Type = "dock" },
                new TerritorySeedOptions { Name = "Navy Pier Yard", City = "Chicago", Type = "dock" },
                new TerritorySeedOptions { Name = "The Green Mill", City = "Chicago", Type = "club" },
                new TerritorySeedOptions { Name = "Stony Island Stash", City = "Chicago", Type = "stash" },

                new TerritorySeedOptions { Name = "Crenshaw Corner", City = "Los Angeles", Type = "corner" },
                new TerritorySeedOptions { Name = "Skid Row Blocks", City = "Los Angeles", Type = "corner" },
                new TerritorySeedOptions { Name = "Harbor Wharf", City = "Los Angeles", Type = "dock" },
                new TerritorySeedOptions { Name = "Long Beach Docks", City = "Los Angeles", Type = "dock" },
                new TerritorySeedOptions { Name = "Sunset Room", City = "Los Angeles", Type = "club" },
                new TerritorySeedOptions { Name = "Boyle Stash", City = "Los Angeles", Type = "stash" },

                new TerritorySeedOptions { Name = "Liberty Corner", City = "Miami", Type = "corner" },
                new TerritorySeedOptions { Name = "Little Havana Blocks", City = "Miami", Type = "corner" },
                new TerritorySeedOptions { Name = "Biscayne Docks", City = "Miami", Type = "dock" },
                new TerritorySeedOptions { Name = "Ocean Drive Room", City = "Miami", Type = "club" },
                new TerritorySeedOptions { Name = "Star Island Room", City = "Miami", Type = "club" },
                new TerritorySeedOptions { Name = "Port Stash", City = "Miami", Type = "stash" },

                // Vegas leans on its rooms, but still carries one of each type. A player picks their
                // town at sign-up knowing nothing, so a town missing an effect entirely would punish a
                // blind choice forever: the character is in the mix, never in leaving a gap.
                new TerritorySeedOptions { Name = "Fremont Corner", City = "Las Vegas", Type = "corner" },
                new TerritorySeedOptions { Name = "Naked City Blocks", City = "Las Vegas", Type = "corner" },
                new TerritorySeedOptions { Name = "Union Pacific Yard", City = "Las Vegas", Type = "dock" },
                new TerritorySeedOptions { Name = "The Sands Room", City = "Las Vegas", Type = "club" },
                new TerritorySeedOptions { Name = "Glitter Gulch Room", City = "Las Vegas", Type = "club" },
                new TerritorySeedOptions { Name = "Desert Stash", City = "Las Vegas", Type = "stash" },

                new TerritorySeedOptions { Name = "Bankhead Corner", City = "Atlanta", Type = "corner" },
                new TerritorySeedOptions { Name = "Bluff Blocks", City = "Atlanta", Type = "corner" },
                new TerritorySeedOptions { Name = "Inman Yard", City = "Atlanta", Type = "dock" },
                new TerritorySeedOptions { Name = "Hartsfield Freight", City = "Atlanta", Type = "dock" },
                new TerritorySeedOptions { Name = "Peachtree Room", City = "Atlanta", Type = "club" },
                new TerritorySeedOptions { Name = "Westside Stash", City = "Atlanta", Type = "stash" },

                new TerritorySeedOptions { Name = "Third Ward Corner", City = "Houston", Type = "corner" },
                new TerritorySeedOptions { Name = "Sunnyside Blocks", City = "Houston", Type = "corner" },
                new TerritorySeedOptions { Name = "Ship Channel Docks", City = "Houston", Type = "dock" },
                new TerritorySeedOptions { Name = "Galveston Wharf", City = "Houston", Type = "dock" },
                new TerritorySeedOptions { Name = "Montrose Room", City = "Houston", Type = "club" },
                new TerritorySeedOptions { Name = "Acres Home Stash", City = "Houston", Type = "stash" }
            ];
    }
}

/// <summary>
/// One rung of the development ladder. The percentages are what the ground is worth standing on this
/// rung rather than what this rung adds, so a level reads on its own without summing the ones below.
/// </summary>
public sealed class TerritoryDevelopmentOptions
{
    public int Level { get; set; }

    /// <summary>What this rung is called, which is what the map shows instead of a number.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The hideout tier it takes to build it, and to be left holding it after a raid.</summary>
    public int MinTier { get; set; } = 1;

    public long Cost { get; set; }
    public int Turns { get; set; }

    /// <summary>How long the work takes. The ground is worth what it was worth until it lands.</summary>
    public int BuildMinutes { get; set; }

    /// <summary>What the ground adds to its type's effect at this level, as a percentage of it.</summary>
    public int EffectPercent { get; set; }

    /// <summary>What it adds to the garrison defending it.</summary>
    public int DefencePercent { get; set; }
}

public sealed class TerritoryTierCapOptions
{
    public int Tier { get; set; }
    public int MaxTerritories { get; set; }
}

public sealed class TerritoryTypeOptions
{
    public string Type { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int StreetIncomePercent { get; set; }
    public int ProductionYieldPercent { get; set; }
    public int MoraleRecoveryPercent { get; set; }

    /// <summary>
    /// Extra haul from a won raid. Storage was the original plan here, but capacity is consulted in
    /// seventeen places that all have to agree, and two authorities disagreeing about a cap is exactly
    /// how the hideout bugs happened. Loot rides on the multiplier the mission already carries.
    /// </summary>
    public int LootPercent { get; set; }
}

public sealed class TerritoryCityControlOptions
{
    public string City { get; set; } = string.Empty;
    public int BonusThugs { get; set; }
}

public sealed class TerritorySeedOptions
{
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public sealed class HideoutTierOptions
{
    public int Level { get; set; } = 1;
    public string Name { get; set; } = "Trap House";
    public int MaxPimps { get; set; }
    public int MaxHoes { get; set; }
    public int MaxThugs { get; set; }

    /// <summary>
    /// Rides the place has room for. A garage rather than a storage shelf, because a car is not stock:
    /// it does not get consumed, it does not spoil, and there is nowhere to put a fleet of them but the
    /// building itself. It is also what stops a rich player from parking twenty rides behind a Trap
    /// House guard and treating the jacking strike as somebody else's problem.
    /// </summary>
    public int MaxRides { get; set; } = 2;

    /// <summary>
    /// Turns the building can hold at once. Zero means it adds nothing to the opening bank, which is
    /// what the first tier is: the Trap House leaves <see cref="GameOptions.MaxTurns"/> as it found it,
    /// so the one number that decides a new player's bank stays the one number in config.
    ///
    /// The first thing a tier sells that is not room for people. A player whose crew is held down by
    /// their storage room rather than their building had no reason at all to want the next one.
    /// </summary>
    public int MaxTurns { get; set; }

    /// <summary>What moving up to this tier costs. Tier 1 is where everyone starts, so it costs nothing.</summary>
    public long UpgradeCost { get; set; }
    public int UpgradeTurns { get; set; }

    /// <summary>How long the build takes once paid for. The hideout keeps its old caps until it finishes.</summary>
    public int BuildMinutes { get; set; }
}

public sealed class StorageLevelOptions
{
    public int Level { get; set; }
    /// <summary>The hideout tier this level needs. A bigger room needs a bigger building to put it in.</summary>
    public int MinTier { get; set; } = 1;
    public int Condoms { get; set; }
    public int Beer { get; set; }
    public int Weapons { get; set; }
    public int Weed { get; set; }
    public int Coke { get; set; }
    public int Moonshine { get; set; }
    public int Cut { get; set; }

    /// <summary>
    /// Crates of medicine. Sized so that the room which supplies a tier's crew through a full shift also
    /// holds enough medicine to treat that whole crew once, which makes the ceiling legible: a full
    /// shelf survives one catastrophic infestation, not a campaign of them.
    /// </summary>
    public int Medicine { get; set; }

    /// <summary>
    /// Doses of poison. Sized like the medicine beside it, so a room that can treat its own house once
    /// can also mount one attack on a house its own size.
    /// </summary>
    public int Poison { get; set; }

    public long UpgradeCost { get; set; }
}

public sealed class SafeLevelOptions
{
    public int Level { get; set; }
    public int MinTier { get; set; } = 1;
    public long MaxCash { get; set; }
    public long UpgradeCost { get; set; }
}

/// <summary>
/// A level of the workshop: how much of anything a turn produces, and how far up the list it reaches.
/// One room now rather than three - see MakeableOptions.
/// </summary>
public sealed class WorkshopLevelOptions
{
    public int Level { get; set; }
    public int MinTier { get; set; } = 1;

    /// <summary>
    /// How many turns' worth of output one turn produces. A good says what it makes per turn in a room
    /// that can make it at all; this is how much faster a deeper room does the same work.
    /// </summary>
    public int Throughput { get; set; } = 1;

    public long UpgradeCost { get; set; }

    /// <summary>
    /// Kept because the shipped settings still carry it and the binder would otherwise drop the value
    /// on the floor. Nothing reads it: what a unit costs belongs to the thing being made.
    /// </summary>
    public long CostPerWeapon { get; set; }

    /// <summary>Retired with the still and the mix house. See Throughput.</summary>
    public int WeaponsPerTurn { get; set; }
}

/// <summary>
/// One gun. Price is the shop's; firepower is what carrying it is worth in a fight, in units of one
/// pistol; the forge fields are what making it takes, and are absent for a gun nobody makes.
/// </summary>
/// <summary>
/// Something the workshop can turn out that is not a gun.
///
/// Guns have carried their own forging cost and the workshop level that unlocks them since the tiers
/// were added, and every other made good was described instead by a room of its own - a still that made
/// moonshine, a mix house that made cut. Three rooms with one shape between them, two of which dead-
/// ended at the second building and were never thought about again.
///
/// So the good carries the recipe and the workshop is simply the room it happens in, which is what the
/// guns were already saying.
/// </summary>
public sealed class MakeableOptions
{
    public string Key { get; set; } = string.Empty;

    /// <summary>Units one turn produces at the first workshop that can make it, before the room scales it.</summary>
    public int PerTurn { get; set; } = 1;

    /// <summary>Materials for one unit. Zero means it cannot be made at all, only bought.</summary>
    public long MaterialCost { get; set; }

    public int MinWorkshopLevel { get; set; } = 1;

    public bool CanMake => MaterialCost > 0 && MinWorkshopLevel > 0;
}

public sealed class WeaponTierOptions
{
    public string Key { get; set; } = WeaponTiers.Pistol;
    public int Price { get; set; }
    public double Firepower { get; set; } = 1;

    /// <summary>Materials to forge one, and the workshop that can. Zero means it cannot be made at all.</summary>
    public long ForgeCost { get; set; }
    public int MinWorkshopLevel { get; set; }

    /// <summary>
    /// The rung of store standing anybody has to be on before this gun will be handed over, at the
    /// counter or off another player's listing. 1 is everybody.
    ///
    /// Deliberately not enforced on the workshop. Forging is you making it yourself in your own back
    /// room, which is the alternative route the gate exists to leave open - it needs a deep building
    /// instead of a reputation, it is slow, and it can never turn out a rifle. Standing is what
    /// somebody else's willingness to arm you is made of, and nobody has to be willing to arm you in
    /// your own basement.
    /// </summary>
    public int MinRepLevel { get; set; } = 1;

    public bool CanForge => ForgeCost > 0 && MinWorkshopLevel > 0;
}

/// <summary>
/// A level of the intelligence centre. Shaped on its own rather than reusing the making stations,
/// because it produces nothing: what it buys is how many runs can be in the air and how much of the
/// route is known before anybody leaves.
/// </summary>
/// <summary>
/// Sending crew to another town to buy cheap and carry it home.
///
/// Travelling yourself costs turns each way and leaves you standing in the wrong town. A run costs
/// fewer turns, but it takes real time, it locks up crew who earn nothing while they are gone, and it
/// is paid for in cash before anybody leaves. Neither is strictly better, which is the whole point.
/// </summary>

/// <summary>
/// What it costs to go and move money.
///
/// The bank used to be free, which made it strictly better than the safe in every case: cash on hand
/// is what a raid and a roadblock take, bank cash is what neither can, and moving between them cost
/// nothing at all. A disciplined player therefore never carried anything, the safe never held a risk
/// worth pricing, and the top level of it was five million dollars for a convenience.
///
/// The charge is on the visit rather than on the amount or the direction. Pricing the amount would
/// tax being rich, which the turn bank already does by being the same size for everybody. Pricing the
/// direction would mean picking which half to break: charging withdrawals alone leaves depositing
/// free, so nobody ever carries and the risk still never bites, while charging deposits alone taxes
/// the careful move and reads as a punishment for playing well. Charging the trip does both at once -
/// banking after every shift is expensive, banking at the end of a session is not - and it is what
/// gives the safe ladder something to sell, since a withdrawal is capped by the safe and a small safe
/// therefore means more trips to fund the same buy.
/// </summary>
public sealed class BankOptions
{
    /// <summary>
    /// Turns for one trip. Two against a twenty-turn shift is roughly a tenth on top of banking after
    /// every action and nothing at all on banking twice a night, which is the difference the charge
    /// exists to create.
    /// </summary>
    public int TripTurnCost { get; set; } = 2;

    /// <summary>
    /// How long you are still counted as standing at the counter. Without it, depositing and then
    /// realising you overshot costs two trips, and the game charges a player for a typo.
    ///
    /// The window is fixed rather than sliding: it opens when a trip is paid for and is not pushed
    /// along by the free moves inside it. A sliding one would turn a single payment into permanent
    /// free banking for anyone willing to move money every few minutes.
    /// </summary>
    public int TripGraceMinutes { get; set; } = 5;
}

/// <summary>
/// Getting swept up working the streets, and what it costs to get people back.
///
/// The street had no downside event at all. It draws heat, but heat only ever answered for what was
/// held, so a house holding nothing worked for ever at no risk - and because recruits and finds are
/// both flat per turn, the shift was pure upside that quietly stopped mattering as the crew grew. A
/// sweep that scales with the crew on the street is the counterweight: it turns a flat trickle of
/// recruits back into churn, and gives a grown empire something to spend money on.
///
/// The choice it creates is the point. Bail is priced above what the same head costs to hire, so for
/// anonymous crew it is deliberately the worse deal in cash - you pay it to keep the morale, not to
/// save money. For a named pimp it is plainly worth paying, because another pimp is not that pimp.
/// </summary>
public sealed class ArrestOptions
{
    /// <summary>
    /// Crew on the street below which nobody is ever taken.
    ///
    /// The same idea as the heat floor, and for the same stated reason: a floor is what makes a small
    /// operation safe and stops the game punishing a player for existing. A Trap House holds fifty
    /// hoes, so a house working its opening crew is never swept.
    /// </summary>
    public int FreeCrewOnStreet { get; set; } = 20;

    /// <summary>
    /// How fast the odds climb with each exposed head. Feeds a curve rather than a straight line, so
    /// the ceiling below is approached rather than reached: a flat line hit the cap by the second tier
    /// and made every size and district above it identical.
    /// </summary>
    public double ChancePerCrewPerShift { get; set; } = 0.004;

    /// <summary>The most any one shift can risk, before the lookout takes its share off.</summary>
    public double MaxChancePerShift { get; set; } = 0.5;

    /// <summary>Heat lifts the odds rather than gating them: the law is already looking, not newly told.</summary>
    public double HeatScaleDivisor { get; set; } = 100;

    /// <summary>How much of the crew on the street a sweep takes. A share, so a big house loses more.</summary>
    public double MinTakenPercent { get; set; } = 0.01;
    public double MaxTakenPercent { get; set; } = 0.03;

    /// <summary>The chance a sweep also picks up a named pimp, who is the decision worth having.</summary>
    public double PimpTakenChance { get; set; } = 0.12;

    /// <summary>
    /// What a sweep adds to the attention on the house: a flat charge for being on a report at all,
    /// then a share per head taken, and more again for a named pimp.
    ///
    /// Without this a sweep quietly made a house cooler, because the crew heat of the people taken
    /// left with them - so the worst night the law could give you also lowered your odds of the next
    /// one. It should read the other way round: a house that just had people taken off its corner is
    /// a house with a file open on it. The pimp is worth the most of the three because he is the one
    /// who knows the addresses.
    ///
    /// Distinct from <see cref="TalkHeat"/>, which is a later and separate event - the pimp you chose
    /// to leave inside giving you up. This is charged on the sweep itself and whatever you do next.
    ///
    /// Earned heat rather than derived, so it decays like everything else a player did - a sweep
    /// makes the next few hours dearer and then fades, which keeps laying low the answer to it.
    /// </summary>
    public double HeatPerArrest { get; set; } = 6;
    public double HeatPerArrestedCrew { get; set; } = 1.5;
    public double HeatPerArrestedPimp { get; set; } = 10;

    public int BailWindowHours { get; set; } = 6;

    /// <summary>
    /// What a bond costs, per head.
    ///
    /// Priced when a shift was worth a great deal less than it is now. A sweep taking three hoes and a
    /// named pimp came to under nine thousand, which a house big enough to be swept in the first place
    /// earns back inside a single shift - so the decision the bail window exists to create, pay or
    /// leave them, was not a decision at all. At these prices the same sweep costs about half a shift,
    /// which is enough to be worth thinking about and not enough to end anybody.
    ///
    /// The pimp is the one that matters: he is a named person with loyalty, he is the one who talks if
    /// you leave him, and he should be the reason you find the money.
    /// </summary>
    public long BailPerHoe { get; set; } = 3_000;
    public long BailPerThug { get; set; } = 6_000;
    public long BailPerPimp { get; set; } = 25_000;

    /// <summary>
    /// What leaving people inside costs the ones who are still out. Capped, because the hiring floor
    /// sits at 35 morale: an uncapped penalty could drop a player below the line that lets them
    /// replace the crew they just lost, which is a hole rather than a consequence.
    /// </summary>
    public double AbandonMoralePerHead { get; set; } = 0.8;
    public double MaxAbandonMoralePenalty { get; set; } = 20;
    public double AbandonLoyaltyPenalty { get; set; } = 6;

    /// <summary>
    /// Whether the one you left talks. Loyalty is frozen at the arrest and decides it, which is the
    /// only place in the game where loyalty you never spent buys you something.
    /// </summary>
    public double TalkLoyaltyThreshold { get; set; } = 50;
    public double TalkChance { get; set; } = 0.4;
    public double TalkHeat { get; set; } = 12;
}

public sealed class MuleOptions
{
    /// <summary>
    /// How long a leg takes per turn of distance. This is what makes a run feel like a flight rather
    /// than a teleport: at six minutes a turn the shipped map runs twelve to thirty-six minutes each
    /// way, so a round trip is a decent chunk of an evening.
    /// </summary>
    public int MinutesPerTravelTurn { get; set; } = 6;

    /// <summary>How long they spend on the ground finding a seller and buying.</summary>
    public int BuyingMinutes { get; set; } = 10;

    /// <summary>
    /// Turns to brief and dispatch a run, per turn of distance. Below 1 on purpose: the run is meant
    /// to be cheaper in turns than going yourself, which costs the distance twice over.
    /// </summary>
    public double TurnCostPerTravelTurn { get; set; } = 0.5;
    public int MinTurnCost { get; set; } = 1;

    /// <summary>
    /// Units one hoe can carry, and how many may go. The number of hoes sent is the player's greed
    /// dial. Sized against the storage room rather than against nothing: six hoes at forty-five apiece
    /// can outrun a shallow store, so a big run makes the room matter instead of vanishing into it. It
    /// also has to clear what flying her costs, or every extra body is a loss.
    /// </summary>
    public int HoeCarryCapacity { get; set; } = 45;
    public int MaxHoesPerRun { get; set; } = 6;

    /// <summary>
    /// Fare per head per turn of distance, charged both ways at launch. Deliberately smaller than the
    /// margin a hoe can carry: at 220 a head cost more to fly than she could earn on any route in the
    /// game, so every run lost money and the whole mechanic was dead on arrival.
    /// </summary>
    public long FarePerHeadPerTravelTurn { get; set; } = 60;

    /// <summary>
    /// Rooms and meals per head per hour away, charged up front for the whole trip. Prepaid rather
    /// than billed hourly because crew who ran out of money mid-flight would need a debt system, and
    /// a run that quietly becomes a loan is a nastier mechanic than one that is simply expensive.
    /// </summary>
    public long UpkeepPerHeadPerHour { get; set; } = 60;

    /// <summary>
    /// A mule is sloppier than you are, so a route's own bust chance is worse for them than for a
    /// player making the same trip.
    /// </summary>
    public double BustChanceMultiplier { get; set; } = 1.4;

    /// <summary>Extra chance per hoe beyond the first: more bodies, more to notice.</summary>
    public double BustChancePerExtraHoe { get; set; } = 0.02;
    public double MaxBustChance { get; set; } = 0.6;

    /// <summary>Share of the load taken when a run is stopped.</summary>
    public double SeizureMinPercent { get; set; } = 0.35;
    public double SeizureMaxPercent { get; set; } = 1.0;

    /// <summary>Heat earned per unit seized, because crew who are caught talk.</summary>
    public double HeatPerSeizedUnit { get; set; } = 0.8;

    /// <summary>
    /// Below this loyalty a pimp sent far away with your money may simply not come back. This is what
    /// makes who you send a real question rather than picking whoever is spare.
    /// </summary>
    public double DefectLoyaltyThreshold { get; set; } = 45;
    public double MaxDefectChance { get; set; } = 0.3;

    /// <summary>Runs allowed out with no intelligence centre. Zero: the room is what unlocks them.</summary>
    public int BaseConcurrentRuns { get; set; } = 0;
}

/// <summary>
/// A level of the lookout. Buys warning rather than output: someone watching the street means the
/// stash is moved and the door is shut before anyone reaches it.
/// </summary>
public sealed class LookoutLevelOptions
{
    public int Level { get; set; }
    public int MinTier { get; set; } = 1;

    /// <summary>How much of an hour's raid chance the warning takes off. Never all of it.</summary>
    public int BustChanceReductionPercent { get; set; }

    public long UpgradeCost { get; set; }
}

public sealed class IntelligenceLevelOptions
{
    public int Level { get; set; }
    public int MinTier { get; set; } = 2;

    /// <summary>Mule runs allowed out at once.</summary>
    public int ConcurrentRuns { get; set; } = 1;

    /// <summary>How much of a route's risk is taken off by knowing it, as a percent of the base chance.</summary>
    public int RiskReductionPercent { get; set; }

    public long UpgradeCost { get; set; }
}

public sealed class LabLevelOptions
{
    public int Level { get; set; }
    public int MinTier { get; set; } = 1;
    public int YieldBonusPercent { get; set; }

    /// <summary>Units the lab makes on its own each hour, capped by storage and by offline hours.</summary>
    public int PassivePerHour { get; set; }
    public long UpgradeCost { get; set; }
}

public sealed class BotAutomationOptions
{
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// How often the loop looks at the world. This is the rate a rival acts at while it is *in* a
    /// session, not how often it plays: a player at the screen fires an action every minute or so, and
    /// then is gone for hours.
    /// </summary>
    public int TickSeconds { get; set; } = 60;
    public int RoundsPerTick { get; set; } = 1;

    /// <summary>How many times a day a rival sits down to play. Each one draws its own from this band.</summary>
    public int MinSessionsPerDay { get; set; } = 2;
    public int MaxSessionsPerDay { get; set; } = 6;

    /// <summary>
    /// How wide a rival's playing hours are, centred on its own peak hour. Narrow enough that the world
    /// has a rush hour, wide enough that it is not a single spike.
    /// </summary>
    public int ActiveWindowHours { get; set; } = 8;

    /// <summary>
    /// The share of rivals who keep no hours. Without a few of these the board is dead for anyone who
    /// plays at an odd hour.
    /// </summary>
    public double NeverSleepsShare { get; set; } = 0.2;

    /// <summary>
    /// The ceiling on one sitting, in actions and in minutes. Both are backstops: a session is meant
    /// to end when the turn bank runs dry, which is what a real one ends on. Sized so that a rival
    /// returning to a full bank can actually spend it, since a cap that binds first would leave every
    /// rival permanently sitting on turns it never uses.
    /// </summary>
    public int MaxActionsPerSession { get; set; } = 60;
    public int MaxSessionMinutes { get; set; } = 90;

    /// <summary>
    /// Turns a rival leaves on the table before calling it a night. Nobody plays their bank to exactly
    /// zero, and a rival that did would never have anything in hand to answer a raid with.
    /// </summary>
    public int SessionTurnReserve { get; set; } = 12;

    /// <summary>
    /// Chance of doing nothing on a given tick mid-session. This is reading the screen, changing your
    /// mind, going to make tea: without it a session is a machine gun of evenly spaced actions.
    /// </summary>
    public double HesitationChance { get; set; } = 0.25;
}

public sealed class StreetActionOptions
{
    public int BaseGrossPerTurn { get; set; } = 35;
    public RangeOptions HoeGrossPerTurn { get; set; } = new(18, 30);
    public RangeOptions PimpGrossPerTurn { get; set; } = new(4, 10);
    /// <summary>
    /// What a turn of work turns up, before the district and before word of mouth.
    ///
    /// Set below the far end of the scale below rather than at the near one, so the scale can never
    /// lift anybody past what the flat rates used to give. Left where they were, a reach running to
    /// 2.5x would not have redistributed what the street finds, it would have printed more of it: the
    /// top of the curve found two and a half times what anybody had ever found before, which is a
    /// maxed Penthouse recruited from empty in three days of turns.
    ///
    /// Halved again on top of that, because free crew that fills a building is free crew that makes
    /// the crew shop decorative. Capacity is what actually gates income here - the shift is worth what
    /// the hideout has room for - so a street that tops a house up to its ceiling on its own is the
    /// game handing out the only thing it charges 300,000 dollars a tier for. At full reach a house
    /// now finds half of what every house used to find, and a small one finds a fifth of it: enough
    /// that a shift occasionally turns somebody up, not enough to be a supply line.
    /// </summary>
    public double PimpRecruitChance { get; set; } = 0.0024;
    public double HoeRecruitChance { get; set; } = 0.024;
    public double ThugRecruitChance { get; set; } = 0.008;
    public FindTableOptions Finds { get; set; } = new();

    /// <summary>
    /// How many heads on the street it takes to double the odds of picking somebody up, and the
    /// ceiling on that.
    ///
    /// The arrest rules already say why this is here: recruits were flat per turn, so a shift was pure
    /// upside that quietly stopped mattering as the house grew. The answer there was a risk that
    /// scales with the crew - which is half a fix, because it takes the flat trickle away without
    /// giving the growing house anything back, and it left the district table as a plain gross ladder:
    /// every recruiting district is worth the same few thousand a shift for ever while the Casino's cut
    /// of the take grows with every hoe.
    ///
    /// Word of mouth is the other half. A bigger operation is more visible to the people who might join
    /// it, so the same shift turns up more of them, and the districts that pay in crew stay worth
    /// choosing right up to the point the hideout is full - which is exactly when they should stop
    /// mattering, because there is nowhere left to put anybody.
    ///
    /// Capped, and the cap is load-bearing twice over: recruits feed the crew that sets this
    /// multiplier, so an uncapped version is a growth loop that outruns every building in the game -
    /// and the base rates above are priced against the cap rather than against the floor, so this
    /// decides who gets the trickle rather than how big the trickle is.
    ///
    /// The step is sized against the crew ladder rather than picked round. At sixty the cap landed at
    /// ninety heads, which a full Trap House of seventy-five very nearly reaches and a Warehouse of a
    /// hundred and thirty blows straight past - so the multiplier saturated at the first tier and
    /// stopped telling the buildings apart, which is the one thing it exists to do. At a hundred it
    /// lands at a hundred and fifty and every tier sits somewhere different on the curve: a Trap House
    /// at 1.75, a Warehouse at 2.3, a Nightclub and a Penthouse at the cap.
    /// </summary>
    public int RecruitCrewPerStep { get; set; } = 100;
    public double MaxRecruitCrewScale { get; set; } = 2.5;

    /// <summary>What word of mouth is worth to a house this size, as a multiplier on every recruit roll.</summary>
    public double RecruitScaleFor(int crewOnStreet)
        => Math.Clamp(
            1 + Math.Max(0, crewOnStreet) / (double)Math.Max(1, RecruitCrewPerStep),
            1,
            Math.Max(1, MaxRecruitCrewScale));

    /// <summary>
    /// Where the crew works. Empty here for the same reason the hideout tables are: the binder appends
    /// to a pre-populated list rather than replacing it.
    /// </summary>
    public List<StreetDistrictOptions> Districts { get; set; } = [];

    public StreetDistrictOptions? District(string? key)
        => Districts.FirstOrDefault(x => string.Equals(x.Key, key?.Trim().ToLowerInvariant(), StringComparison.Ordinal));

    /// <summary>The one every shift falls back to: the neutral district, at exactly the base numbers.</summary>
    public StreetDistrictOptions DefaultDistrict()
        => Districts.FirstOrDefault(x => x.IsDefault) ?? Districts.FirstOrDefault() ?? new StreetDistrictOptions();

    public void ApplyDistrictDefaultsWhereEmpty()
    {
        if (Districts.Count > 0)
            return;

        // The source game had five districts and its own guide admits it never found a difference
        // between any of them - which makes them five names on a dropdown and a wasted click. So each
        // one here changes what a shift is actually for, and the trade is always the same shape: what
        // you go home with against how much notice you drew getting it.
        //
        // Low Rent is the neutral one and the default, at exactly the base numbers, so a player who
        // never touches the picker works precisely the shift they always did.
        //
        // The gross column is the only one that scales with the house, so it is also the only one that
        // can be allowed a wide spread. At the first tuning the Nightclub paid 115 against Low Rent's
        // 100 and gave up nothing that costs real money, which made the neutral district - the default,
        // the one most shifts are worked in - strictly the worse choice at every crew size, and turned
        // the whole picker into a gross ladder read top-down. The districts that pay in crew now pay
        // for it out of the take, so choosing one is choosing what the shift was for.
        Districts =
        [
            new StreetDistrictOptions
            {
                Key = "casino",
                Name = "Casino District",
                Blurb = "Money everywhere and somebody watching all of it.",
                GrossPercent = 130,
                HoeRecruitPercent = 60,
                ThugRecruitPercent = 40,
                PimpRecruitPercent = 100,
                FindPercent = 40,
                HeatPercent = 200
            },
            new StreetDistrictOptions
            {
                Key = "winos",
                Name = "Wino Slums",
                Blurb = "Nothing to earn and nobody to stop you. Men who will take any work going.",
                GrossPercent = 65,
                HoeRecruitPercent = 70,
                ThugRecruitPercent = 220,
                PimpRecruitPercent = 40,
                FindPercent = 90,
                HeatPercent = 45
            },
            new StreetDistrictOptions
            {
                Key = "lowrent",
                Name = "Low Rent District",
                Blurb = "Nothing special in either direction, which is its own kind of useful.",
                IsDefault = true
            },
            new StreetDistrictOptions
            {
                Key = "nightclub",
                Name = "Nightclub District",
                Blurb = "Where the work finds you. Hoes and the people who manage them.",
                GrossPercent = 90,
                HoeRecruitPercent = 185,
                ThugRecruitPercent = 60,
                PimpRecruitPercent = 200,
                FindPercent = 80,
                HeatPercent = 130
            },
            new StreetDistrictOptions
            {
                Key = "ghetto",
                Name = "Urban Ghetto",
                Blurb = "Product changes hands on every corner, and the law knows it.",
                GrossPercent = 85,
                HoeRecruitPercent = 90,
                ThugRecruitPercent = 130,
                PimpRecruitPercent = 60,
                FindPercent = 230,
                HeatPercent = 150
            }
        ];
    }
}

/// <summary>
/// One place to work a shift, as a set of multipliers over the base numbers.
///
/// Percentages rather than absolute figures on purpose: a district says how somewhere differs from an
/// ordinary street, so retuning what a shift is worth retunes every district at once and none of them
/// can silently drift away from the baseline it is supposed to be a variation on.
/// </summary>
public sealed class StreetDistrictOptions
{
    public string Key { get; set; } = "lowrent";
    public string Name { get; set; } = "Low Rent District";
    public string Blurb { get; set; } = string.Empty;

    /// <summary>The district a shift with no district named works. Exactly one should carry it.</summary>
    public bool IsDefault { get; set; }

    public int GrossPercent { get; set; } = 100;
    public int HoeRecruitPercent { get; set; } = 100;
    public int ThugRecruitPercent { get; set; } = 100;
    public int PimpRecruitPercent { get; set; } = 100;

    /// <summary>What turns up on the ground: condoms, beer, weed and coke together.</summary>
    public int FindPercent { get; set; } = 100;

    /// <summary>
    /// How much notice a turn of work draws here. The counterweight on every other number: the two
    /// districts worth going out of your way for are also the two the law is already looking at.
    /// </summary>
    public int HeatPercent { get; set; } = 100;

    public double Scale(int percent) => Math.Max(0, percent) / 100.0;
}

public sealed class FindTableOptions
{
    public FindOptions Condoms { get; set; } = new(0.06, 1, 3);
    public FindOptions Beer { get; set; } = new(0.05, 1, 2);
    public FindOptions Weed { get; set; } = new(0.07, 1, 3);
    public FindOptions Coke { get; set; } = new(0.018, 1, 1);
}

public sealed class ProductionOptions
{
    public ProductProductionOptions Weed { get; set; } = new(25, 3, 6);
    public ProductProductionOptions Coke { get; set; } = new(80, 1, 3);
}

public sealed class MoraleOptions
{
    public int HoesManagedPerPimp { get; set; } = 10;

/// <summary>
    /// How stale a trend baseline may be before the arrow is withheld. It is not the measurement
    /// period: the arrow reads from the most recent action, not across the whole window.
    /// </summary>
    public int TrendWindowHours { get; set; } = 3;

    /// <summary>
    /// Movement smaller than this reads as steady, so the arrow does not flicker on drift. Sized for
    /// the change across one action, which is often well under a point: at a full point a crew
    /// climbing 0.7 a shift was reported as steady while it visibly recovered.
    /// </summary>
    public double TrendFlatBand { get; set; } = 0.25;

    public double TurnsPerCondom { get; set; } = 12;
    public double TurnsPerBeer { get; set; } = 10;
    public double HoeStreetWorkGainPerTurn { get; set; } = 0.14;
    public double ThugStreetWorkGainPerTurn { get; set; } = 0.12;
    public double HoeCutMoraleScalePerTurn { get; set; } = 0.025;
    public double BaselineHoeCutPercent { get; set; } = 30;
    /// <summary>
    /// Morale lost per turn by a crew sent out wholly unsupplied, scaled by the share of upkeep that
    /// was actually missing. These used to be charged per missing unit, which grew with the crew while
    /// the morale a shift earns did not, so a mid-sized crew a little short lost more morale in one
    /// action than ten good ones earned.
    /// </summary>
    public double CondomShortagePenalty { get; set; } = 2.25;
    public double BeerShortagePenalty { get; set; } = 2.0;

    /// <summary>
    /// Morale lost per turn by a crew nobody is managing and a crew nobody armed, scaled by the share
    /// of them in that state - the same shape as the shortage penalties above, and charged for the same
    /// reason.
    ///
    /// These were the last two per-head charges left, and they had both faults the shortage rates were
    /// fixed for and one of their own. They grew with the crew while the morale a shift earns did not,
    /// so twenty unmanaged hoes cost the same whether they were twenty of twenty-five or twenty of two
    /// hundred; and being flat per shift rather than per turn, they were the only part of a shift that
    /// did not care how long it was, which quietly priced a one-turn look at the street the same as a
    /// full twenty-turn night.
    ///
    /// The coefficient is now "morale lost per turn when the whole crew is in that state", so a full
    /// twenty-turn shift with no pimps at all costs 14 and one with no weapons at all costs 20. A house
    /// running a third of its hoes unmanaged pays a third of that. Both sit well under a full supply
    /// shortage, because going out unsupplied is worse than going out badly organised.
    /// </summary>
    public double UnmanagedHoePenalty { get; set; } = 0.7;
    public double UncoveredThugPenalty { get; set; } = 1.0;
    public double DesertionThreshold { get; set; } = 25;
    public double MaxDesertionChance { get; set; } = 0.20;
    public double PassiveRecoveryPerTick { get; set; } = 0.35;
    /// <summary>
    /// What sitting still costs, per head, in hours per unit.
    ///
    /// Passive upkeep used to charge an hour as though it were a turn of street work - it read
    /// <see cref="TurnsPerCondom"/> and <see cref="TurnsPerBeer"/> straight off - which meant an hour
    /// asleep and an hour on the corner cost a crew exactly the same. That is the wrong shape: the
    /// shift is the thing being paid for, and the standing charge is what it costs to keep people
    /// around between shifts. Its own rates, at half the working burn, so a night away is a bill a
    /// player can come back to rather than a reason not to log off.
    ///
    /// Separate knobs rather than a multiplier on the working rate, because the two numbers stopped
    /// being the same question the moment one of them was halved - and a storage room sized against
    /// the working rate must not move when the standing charge is tuned.
    /// </summary>
    public double HoursPerCondomUpkeep { get; set; } = 24;
    public double HoursPerBeerUpkeep { get; set; } = 20;

    /// <summary>
    /// General crew upkeep: weed first, then coke. Set to zero to turn the drug part off without
    /// touching condoms and beer.
    /// </summary>
    public double HoursPerDrugUpkeep { get; set; } = 48;
    public double PassiveUpkeepMoralePenaltyPerHour { get; set; } = 3;
    public double PassiveUpkeepLoyaltyPenaltyPerHour { get; set; } = 2;
    public int HqRestTurnCost { get; set; } = 4;
    public long HqRestCashPerCrew { get; set; } = 75;
    public double HqRestMoraleGain { get; set; } = 8;
    public int HqPartyTurnCost { get; set; } = 2;
    public long HqPartyCashPerCrew { get; set; } = 45;
    public int HqPartyBeerPerThug { get; set; } = 5;
    public int HqPartyWeedPerHoes { get; set; } = 10;
    public double HqPartyHoeMoraleGain { get; set; } = 12;
    public double HqPartyThugMoraleGain { get; set; } = 10;
}

public sealed class CrewOptions
{
    public int MaxCrewTransactionQuantity { get; set; } = 1_000;
    public int HirePimpCost { get; set; } = 2_500;
    public int HireHoeCost { get; set; } = 750;
    public int HireThugCost { get; set; } = 1_500;
    public double MinHoeMoraleToHire { get; set; } = 35;
    public double MinThugMoraleToHire { get; set; } = 35;
    public double FireHoeMoralePenalty { get; set; } = 1.5;
    public double FireThugMoralePenalty { get; set; } = 1.25;
    public double FirePimpHoeMoralePenalty { get; set; } = 2.0;
    public double MaxFireMoralePenalty { get; set; } = 25;
}

public sealed class CombatOptions
{
    public CombatPowerOptions Power { get; set; } = new();
    public CombatRoundOptions Round { get; set; } = new();

    public int AttackTurnCost { get; set; } = 10;
    public int AttackCooldownMinutes { get; set; } = 30;
    public int AttackTravelSecondsMin { get; set; } = 75;
    public int AttackTravelSecondsMax { get; set; } = 180;
    public int ReturnTravelSecondsMin { get; set; } = 60;
    public int ReturnTravelSecondsMax { get; set; } = 150;
    public int FightRoundSeconds { get; set; } = 20;
    public int MaxFightRounds { get; set; } = 6;
    public int MaxActiveAttackMissions { get; set; } = 2;
    public double MoraleBreakThreshold { get; set; } = 5;
    public int DefenderProtectionMinutes { get; set; } = 60;
    public double PowerRandomnessPercent { get; set; } = 0.15;
    /// <summary>
    /// What a won raid carries off, as a share of what the loser had on them.
    ///
    /// A fifth of the cash at the very best was a raid the loser could shrug at - by the size of house
    /// that can mount one, it was a fraction of a single shift, so being raided cost less than not
    /// working for an hour and there was nothing to be afraid of. At the top of this range a raid takes
    /// more than half of what somebody was carrying, which is the point: it should be the worst thing
    /// that happens to you in a day.
    ///
    /// Cash comes off what is on hand and never out of the bank, so none of this touches savings. It
    /// makes banking the answer instead - the habit the game has been trying to teach all along, and
    /// now the one that decides whether a raid ruins an evening or barely registers. Product has no
    /// bank, which is why the ceiling matters more there: the only defence is not sitting on a pile.
    ///
    /// The repeat decay below is what keeps this from becoming a way to farm one person - the same
    /// target pays less every time, down to a tenth.
    /// </summary>
    public double MinCashLootPercent { get; set; } = 0.15;
    public double MaxCashLootPercent { get; set; } = 0.55;
    public double MinProductLootPercent { get; set; } = 0.15;
    public double MaxProductLootPercent { get; set; } = 0.55;
    public double WinnerCrewLossPercent { get; set; } = 0.03;
    public double LoserCrewLossPercent { get; set; } = 0.10;
    public double WeaponLossPercent { get; set; } = 0.08;

    /// <summary>
    /// Rooms a won raid leaves broken behind it. One, and only on a house.
    ///
    /// Everything a raid used to take grew back by morning: cash comes off a shift, product comes off
    /// a lab, and crew are a hiring away. That made losing a raid an expensive evening rather than
    /// something that happened to your empire, and it made winning one a withdrawal rather than a
    /// blow. A room is the part still costing the loser tomorrow, and the part the winner can point
    /// at. Ground gets none of this: a corner is contested rather than robbed, and nobody's house was
    /// ever inside it.
    /// </summary>
    public int RoomsWreckedOnRaidLoss { get; set; } = 1;
    public double AttackerDefeatThugMoralePenalty { get; set; } = 8;
    public double AttackerDefeatHoeMoralePenalty { get; set; } = 3;
    public double AttackerStandstillThugMoralePenalty { get; set; } = 3;
    public double AttackerVictoryThugMoraleGain { get; set; } = 4;
    public double AttackerVictoryHoeMoraleGain { get; set; } = 1.5;
    public double DefenderDefeatThugMoralePenalty { get; set; } = 6;
    public double DefenderDefeatHoeMoralePenalty { get; set; } = 4;
    public double DefenderVictoryThugMoraleGain { get; set; } = 3;
}

public sealed class RangeOptions
{
    public RangeOptions()
    {
    }

    public RangeOptions(int min, int max)
    {
        Min = min;
        Max = max;
    }

    public int Min { get; set; }
    public int Max { get; set; }
}

public sealed class FindOptions
{
    public FindOptions()
    {
    }

    public FindOptions(double chance, int min, int max)
    {
        Chance = chance;
        Min = min;
        Max = max;
    }

    public double Chance { get; set; }
    public int Min { get; set; }
    public int Max { get; set; }
}

public sealed class ProductProductionOptions
{
    public ProductProductionOptions()
    {
    }

    public ProductProductionOptions(int costPerTurn, int unitsMin, int unitsMax)
    {
        CostPerTurn = costPerTurn;
        UnitsMin = unitsMin;
        UnitsMax = unitsMax;
    }

    public int CostPerTurn { get; set; }
    public int UnitsMin { get; set; }
    public int UnitsMax { get; set; }
    public int MinWorkshopLevel { get; set; } = 1;
}

/// <summary>
/// Talking. The limits here are the only thing standing between a chat panel and somebody who has
/// worked out they can type faster than everybody else can read.
/// </summary>
/// <summary>
/// How long the game keeps its own history.
///
/// Every other table here that grows was already swept - chat, verification codes, closed assist calls,
/// transfer records, standing snapshots, sessions. These two were not, and they are the two that grow
/// fastest: every action by every player and every bot, and every fight.
///
/// Nothing reads either of them over all time. The pages take a recent handful, the admin oversight
/// aggregates over the last day, and the world feed and the away digest work from a timestamp - so what
/// a retention throws away is history that no query could reach, on a table read on the dashboard, on
/// every profile, and by the alert bell.
///
/// Ninety days rather than a fortnight, because the one thing that can genuinely reach back is a player
/// returning from a long absence, and the away digest should not be the feature that discovers the
/// retention.
/// </summary>
public sealed class HistoryOptions
{
    public int ActionLogRetentionDays { get; set; } = 90;

    /// <summary>
    /// Fights are kept as long as actions. A raid is the thing players argue about afterwards, and the
    /// combat log is the only record of who did what to whom.
    /// </summary>
    public int CombatLogRetentionDays { get; set; } = 90;
}

public sealed class ChatOptions
{
    /// <summary>Lines kept in a room's history. Enough to catch up on, few enough to load in one go.</summary>
    public int HistoryDepth { get; set; } = 60;

    /// <summary>
    /// Characters in one message. Long enough to say something, short enough that nobody can push the
    /// rest of the room off the screen with a single paste.
    /// </summary>
    public int MaxLength { get; set; } = 280;

    /// <summary>
    /// The quiet moment between one message and the next from the same person. Not a punishment - it
    /// is the difference between a conversation and a wall.
    /// </summary>
    public int SecondsBetweenMessages { get; set; } = 3;

    /// <summary>
    /// How long a line survives. Chat is the one table that grows with talking rather than playing, so
    /// it is the one that needs sweeping; nobody scrolls back a fortnight.
    /// </summary>
    public int RetentionDays { get; set; } = 14;

    /// <summary>
    /// People in one group, counting whoever started it. Small enough that a group is a table rather
    /// than a broadcast: past a dozen, nobody is talking to anybody.
    /// </summary>
    public int MaxGroupMembers { get; set; } = 12;
}
