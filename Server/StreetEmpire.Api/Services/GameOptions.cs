using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

public sealed class GameOptions
{
    public int TurnsPerTick { get; set; } = 2;
    public int TurnTickMinutes { get; set; } = 10;
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
    /// A ride counts at what the chop shop would actually pay for it, not at what it cost. Net worth is
    /// what you could liquidate, and valuing a fleet at the sticker price would make buying rides a way
    /// to climb the board for free.
    /// </summary>
    public int RideNetWorth { get; set; } = 15_000;
    public int MedicineNetWorth { get; set; } = 250;

    /// <summary>
    /// The gun rack. Empty here on purpose, like the hideout tables: the configuration binder appends to
    /// a pre-populated list rather than replacing it, so shipping defaults in the initializer would merge
    /// them with appsettings and let a stale row win the lookup.
    /// </summary>
    public List<WeaponTierOptions> Weapons { get; set; } = [];

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
        if (Weapons.Count > 0)
            return;

        // Prices are the source game's, and the firepower curve is the answer to what they buy. It falls
        // away steeply against price - a pistol is $250 a point, a rifle $2,200 - so trading up is never
        // the efficient way to spend money and always the only way left once the hideout's thug cap is
        // full. That is the trade the tiers exist to create: more bodies while you have room for them,
        // better guns once you do not.
        //
        // A pistol is exactly 1, which is what the old single weapon contributed, so nobody's fighting
        // strength moved when tiers arrived - only what their rack is worth on paper.
        Weapons =
        [
            new WeaponTierOptions { Key = WeaponTiers.Pistol, Price = 250, Firepower = 1.0, ForgeCost = 170, MinWorkshopLevel = 1 },
            new WeaponTierOptions { Key = WeaponTiers.Shotgun, Price = 1_250, Firepower = 1.4, ForgeCost = 880, MinWorkshopLevel = 1 },
            new WeaponTierOptions { Key = WeaponTiers.Smg, Price = 2_500, Firepower = 1.9, ForgeCost = 1_800, MinWorkshopLevel = 2 },
            // No forge cost and no workshop level: a rifle is the one gun nobody makes in a back room,
            // which is what stops the workshop from eventually replacing the shop entirely.
            new WeaponTierOptions { Key = WeaponTiers.Rifle, Price = 5_500, Firepower = 2.5 }
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
    public TerritoryOptions Territory { get; set; } = new();
    public MarketOptions Market { get; set; } = new();
    public CityMarketOptions CityMarkets { get; set; } = new();
    public MuleOptions Mules { get; set; } = new();
    public ContractOptions Contracts { get; set; } = new();
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
public sealed class RivalCrewOptions
{
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string? Motto { get; set; }
    public int DuesPercent { get; set; } = 5;

    /// <summary>How they take people on: Open, Application, or InviteOnly.</summary>
    public string Door { get; set; } = "Open";
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
    public int MaxGarrisonBonusPercent { get; set; } = 30;
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
    public List<WorkshopLevelOptions> Still { get; set; } = [];
    public List<WorkshopLevelOptions> Mix { get; set; } = [];
    public List<IntelligenceLevelOptions> Intelligence { get; set; } = [];
    public List<LookoutLevelOptions> Lookout { get; set; } = [];

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

    /// <summary>Chance per hour per point of heat over the floor, and the ceiling on that chance.</summary>
    public double BustChancePerHeat { get; set; } = 0.002;
    public double MaxBustChancePerHour { get; set; } = 0.35;

    /// <summary>Share of every contraband pile taken when it happens, and the fine per unit lost.</summary>
    public double SeizedPercent { get; set; } = 0.5;
    public double FinePerSeizedUnit { get; set; } = 40;

    /// <summary>
    /// How much passive lab output can pile up while a player is away. Past this the labs sit idle, so
    /// the hideout is a reason to come back rather than a reason to stay gone.
    /// </summary>
    public int MaxOfflineProductionHours { get; set; } = 12;

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
                new HideoutTierOptions { Level = 1, Name = "Trap House", MaxPimps = 6, MaxHoes = 50, MaxThugs = 25, MaxRides = 2 },
                new HideoutTierOptions { Level = 2, Name = "Warehouse", MaxPimps = 10, MaxHoes = 85, MaxThugs = 45, MaxRides = 5, UpgradeCost = 200_000, UpgradeTurns = 40, BuildMinutes = 30 },
                new HideoutTierOptions { Level = 3, Name = "Nightclub", MaxPimps = 15, MaxHoes = 130, MaxThugs = 70, MaxRides = 9, UpgradeCost = 600_000, UpgradeTurns = 80, BuildMinutes = 120 },
                new HideoutTierOptions { Level = 4, Name = "Penthouse", MaxPimps = 22, MaxHoes = 200, MaxThugs = 110, MaxRides = 15, UpgradeCost = 1_800_000, UpgradeTurns = 120, BuildMinutes = 360 }
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
                new StorageLevelOptions { Level = 1, Condoms = 42, Beer = 25, Weapons = 12, Weed = 50, Coke = 25, Moonshine = 25, Cut = 25, Medicine = 9 },
                // 50 and 25, which is everything a Trap House has room for. The building is the ceiling
                // from here rather than the room, and moving out is the only way up.
                new StorageLevelOptions { Level = 2, Condoms = 84, Beer = 50, Weapons = 25, Weed = 100, Coke = 50, Moonshine = 50, Cut = 50, Medicine = 17, UpgradeCost = 15_000 },
                new StorageLevelOptions { Level = 3, MinTier = 2, Condoms = 142, Beer = 90, Weapons = 45, Weed = 170, Coke = 85, Moonshine = 90, Cut = 85, Medicine = 29, UpgradeCost = 50_000 },
                new StorageLevelOptions { Level = 4, MinTier = 3, Condoms = 217, Beer = 140, Weapons = 70, Weed = 260, Coke = 130, Moonshine = 140, Cut = 130, Medicine = 44, UpgradeCost = 150_000 },
                new StorageLevelOptions { Level = 5, MinTier = 4, Condoms = 334, Beer = 220, Weapons = 110, Weed = 400, Coke = 200, Moonshine = 220, Cut = 200, Medicine = 67, UpgradeCost = 400_000 },
                // Nothing above supplies a bigger crew, because no building holds one. What the last
                // upgrade buys is room for product, which is the only thing left to want.
                new StorageLevelOptions { Level = 6, MinTier = 4, Condoms = 334, Beer = 220, Weapons = 110, Weed = 600, Coke = 300, Moonshine = 330, Cut = 300, Medicine = 67, UpgradeCost = 1_000_000 }
            ];

        if (Safe.Count == 0)
            Safe =
            [
                new SafeLevelOptions { Level = 1, MaxCash = 50_000 },
                new SafeLevelOptions { Level = 2, MaxCash = 100_000, UpgradeCost = 40_000 },
                new SafeLevelOptions { Level = 3, MinTier = 2, MaxCash = 350_000, UpgradeCost = 120_000 },
                new SafeLevelOptions { Level = 4, MinTier = 3, MaxCash = 1_000_000, UpgradeCost = 300_000 },
                new SafeLevelOptions { Level = 5, MinTier = 4, MaxCash = 3_000_000, UpgradeCost = 900_000 }
            ];

        // PassivePerHour is deliberately below what the same lab yields through production turns: about
        // half a day of accrual matches one full-length production run, so being away is worth something
        // without being worth more than playing.
        if (WeedLab.Count == 0)
            WeedLab =
            [
                new LabLevelOptions { Level = 1, YieldBonusPercent = 25, PassivePerHour = 2, UpgradeCost = 10_000 },
                new LabLevelOptions { Level = 2, YieldBonusPercent = 60, PassivePerHour = 4, UpgradeCost = 30_000 },
                new LabLevelOptions { Level = 3, YieldBonusPercent = 110, PassivePerHour = 7, UpgradeCost = 75_000 },
                new LabLevelOptions { Level = 4, MinTier = 3, YieldBonusPercent = 170, PassivePerHour = 11, UpgradeCost = 250_000 },
                new LabLevelOptions { Level = 5, MinTier = 4, YieldBonusPercent = 240, PassivePerHour = 16, UpgradeCost = 700_000 }
            ];

        // Throughput and nothing else. What a gun costs to make belongs to the gun, not to the room, so
        // a level buys guns per turn and which guns are unlocked - the prices live on the tier table,
        // each set under what the shop charges, because a maker who cannot undercut the shop has nothing
        // to sell and the whole point of the workshop is to give the market a good with real demand.
        if (Workshop.Count == 0)
            Workshop =
            [
                new WorkshopLevelOptions { Level = 1, WeaponsPerTurn = 1, UpgradeCost = 60_000 },
                new WorkshopLevelOptions { Level = 2, WeaponsPerTurn = 2, UpgradeCost = 180_000 },
                new WorkshopLevelOptions { Level = 3, MinTier = 3, WeaponsPerTurn = 3, UpgradeCost = 500_000 }
            ];

        // Moonshine undercuts the shop's beer, which is the only reason to run the risk of holding it.
        // Both of these need the second tier: a Trap House is not somewhere you hide a still, and
        // gating them there keeps the first tier about learning the game rather than running a lab.
        if (Still.Count == 0)
            Still =
            [
                new WorkshopLevelOptions { Level = 1, MinTier = 2, WeaponsPerTurn = 4, CostPerWeapon = 6, UpgradeCost = 25_000 },
                new WorkshopLevelOptions { Level = 2, MinTier = 2, WeaponsPerTurn = 7, CostPerWeapon = 5, UpgradeCost = 80_000 }
            ];

        // The lookout fills the one hole in the first tier's ladder. Everything else a Trap House can
        // buy lands between ten and seventy-five thousand, and then there is nothing until a hundred
        // and fifty: a session and a half of earning with nothing to want. It is also the only new
        // verb in the tier after the workshop, and the only answer to heat besides selling down.
        if (Lookout.Count == 0)
            Lookout =
            [
                new LookoutLevelOptions { Level = 1, MinTier = 1, BustChanceReductionPercent = 25, UpgradeCost = 100_000 },
                new LookoutLevelOptions { Level = 2, MinTier = 2, BustChanceReductionPercent = 45, UpgradeCost = 260_000 },
                new LookoutLevelOptions { Level = 3, MinTier = 3, BustChanceReductionPercent = 60, UpgradeCost = 700_000 }
            ];

        // The intelligence centre buys capacity, not output: how many runs can be out at once, and how
        // much of the route's risk is already known before anybody leaves. Gated at the Warehouse for
        // the same reason the still is, and the first level is deliberately expensive relative to what
        // one run earns, so mule running is something an empire grows into rather than opens with.
        if (Intelligence.Count == 0)
            Intelligence =
            [
                new IntelligenceLevelOptions { Level = 1, MinTier = 2, ConcurrentRuns = 1, RiskReductionPercent = 10, UpgradeCost = 120_000 },
                new IntelligenceLevelOptions { Level = 2, MinTier = 2, ConcurrentRuns = 2, RiskReductionPercent = 20, UpgradeCost = 320_000 },
                new IntelligenceLevelOptions { Level = 3, MinTier = 3, ConcurrentRuns = 3, RiskReductionPercent = 30, UpgradeCost = 750_000 },
                new IntelligenceLevelOptions { Level = 4, MinTier = 4, ConcurrentRuns = 5, RiskReductionPercent = 40, UpgradeCost = 1_600_000 }
            ];

        if (Mix.Count == 0)
            Mix =
            [
                new WorkshopLevelOptions { Level = 1, MinTier = 2, WeaponsPerTurn = 3, CostPerWeapon = 20, UpgradeCost = 40_000 },
                new WorkshopLevelOptions { Level = 2, MinTier = 2, WeaponsPerTurn = 5, CostPerWeapon = 18, UpgradeCost = 120_000 }
            ];

        if (CokeLab.Count == 0)
            CokeLab =
            [
                new LabLevelOptions { Level = 1, YieldBonusPercent = 25, PassivePerHour = 1, UpgradeCost = 25_000 },
                new LabLevelOptions { Level = 2, YieldBonusPercent = 60, PassivePerHour = 2, UpgradeCost = 60_000 },
                new LabLevelOptions { Level = 3, YieldBonusPercent = 110, PassivePerHour = 3, UpgradeCost = 150_000 },
                new LabLevelOptions { Level = 4, MinTier = 3, YieldBonusPercent = 170, PassivePerHour = 5, UpgradeCost = 450_000 },
                new LabLevelOptions { Level = 5, MinTier = 4, YieldBonusPercent = 240, PassivePerHour = 7, UpgradeCost = 1_200_000 }
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

    /// <summary>Thugs needed to hold anything at all. Below this the ground is given up.</summary>
    public int MinimumGarrison { get; set; } = 5;

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

    public List<TerritoryTypeOptions> Types { get; set; } = [];
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

    public long UpgradeCost { get; set; }
}

public sealed class SafeLevelOptions
{
    public int Level { get; set; }
    public int MinTier { get; set; } = 1;
    public long MaxCash { get; set; }
    public long UpgradeCost { get; set; }
}

public sealed class WorkshopLevelOptions
{
    public int Level { get; set; }
    public int MinTier { get; set; } = 1;

    /// <summary>Units a turn of work turns out.</summary>
    public int WeaponsPerTurn { get; set; }

    /// <summary>
    /// Materials per unit, below the shop price or there is nothing to sell. Read by the still and the
    /// mix house, which each make one thing. The workshop makes four, so its costs live on the weapon
    /// tiers instead: a level is throughput and which guns are unlocked, not a single price.
    /// </summary>
    public long CostPerWeapon { get; set; }
    public long UpgradeCost { get; set; }
}

/// <summary>
/// One gun. Price is the shop's; firepower is what carrying it is worth in a fight, in units of one
/// pistol; the forge fields are what making it takes, and are absent for a gun nobody makes.
/// </summary>
public sealed class WeaponTierOptions
{
    public string Key { get; set; } = WeaponTiers.Pistol;
    public int Price { get; set; }
    public double Firepower { get; set; } = 1;

    /// <summary>Materials to forge one, and the workshop that can. Zero means it cannot be made at all.</summary>
    public long ForgeCost { get; set; }
    public int MinWorkshopLevel { get; set; }

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
/// Buyers with a shape. The game had one buyer before this - the city itself, fixed price, any
/// amount, any hour - which is a price list rather than a market.
/// </summary>
public sealed class ContractOptions
{
    /// <summary>How many stand open in a town at once. Enough to choose between, few enough to read.</summary>
    public int OpenPerCity { get; set; } = 3;

    /// <summary>
    /// How often a town posts another order once its board has been thinned.
    ///
    /// This is what makes the board a limited supply rather than a tap. Refilled on demand, a player
    /// could fill an order, look again for a fresh one and repeat until their stock ran out, which
    /// would make the counter price never worth taking and quietly raise the value of every sale in
    /// the game. It is also what gives rivals taking orders a consequence: a stripped board stays
    /// thin, and whoever got there first actually took something.
    /// </summary>
    public int PostIntervalMinutes { get; set; } = 45;

    public int MinQuantity { get; set; } = 15;
    public int MaxQuantity { get; set; } = 60;

    /// <summary>
    /// What a buyer pays over the town's own price, as a percentage. It has to clear the effort of
    /// holding stock for a deadline, and stay under what a good mule route makes, or contracts become
    /// the only thing worth doing.
    /// </summary>
    public int MinPremiumPercent { get; set; } = 20;
    public int PremiumSpreadPercent { get; set; } = 35;

    /// <summary>
    /// How often a coke buyer cares about strength, what they insist on, and what they pay extra for
    /// it. Sometimes rather than always: a floor on every order would make stretching pointless rather
    /// than a trade.
    /// </summary>
    public double PurityConditionChance { get; set; } = 0.4;
    public int MinimumPurityFloorPercent { get; set; } = 60;
    public int PurityPremiumPercent { get; set; } = 25;

    /// <summary>
    /// What gets asked for. Weapons and moonshine are the standing minority, and between weed and coke
    /// a town leans towards whatever it values most without ever ruling the other out: asking only for
    /// the dearer one made every town a one-note board.
    /// </summary>
    public int WeaponsPercent { get; set; } = 20;
    public int MoonshinePercent { get; set; } = 10;
    public int FavouredGoodPercent { get; set; } = 70;

    /// <summary>How long an order stands. Long enough to go and make the goods, short enough to matter.</summary>
    public int MinLifetimeHours { get; set; } = 4;
    public int MaxLifetimeHours { get; set; } = 14;
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
    /// dial. Sized against the storage room rather than against nothing: six hoes at thirty apiece is
    /// close to what a deep store holds, so a big run makes the room matter instead of vanishing into
    /// it. It also has to clear what flying her costs, or every extra body is a loss.
    /// </summary>
    public int HoeCarryCapacity { get; set; } = 30;
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
    public double PimpRecruitChance { get; set; } = 0.012;
    public double HoeRecruitChance { get; set; } = 0.12;
    public double ThugRecruitChance { get; set; } = 0.04;
    public FindTableOptions Finds { get; set; } = new();

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
        Districts =
        [
            new StreetDistrictOptions
            {
                Key = "casino",
                Name = "Casino District",
                Blurb = "Money everywhere and somebody watching all of it.",
                GrossPercent = 145,
                HoeRecruitPercent = 60,
                ThugRecruitPercent = 40,
                PimpRecruitPercent = 100,
                FindPercent = 40,
                HeatPercent = 175
            },
            new StreetDistrictOptions
            {
                Key = "winos",
                Name = "Wino Slums",
                Blurb = "Nothing to earn and nobody to stop you. Men who will take any work going.",
                GrossPercent = 55,
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
                GrossPercent = 115,
                HoeRecruitPercent = 185,
                ThugRecruitPercent = 60,
                PimpRecruitPercent = 200,
                FindPercent = 80,
                HeatPercent = 120
            },
            new StreetDistrictOptions
            {
                Key = "ghetto",
                Name = "Urban Ghetto",
                Blurb = "Product changes hands on every corner, and the law knows it.",
                GrossPercent = 80,
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
    public double UnmanagedHoePenalty { get; set; } = 0.20;
    public double UncoveredThugPenalty { get; set; } = 0.35;
    public double DesertionThreshold { get; set; } = 25;
    public double MaxDesertionChance { get; set; } = 0.20;
    public double PassiveRecoveryPerTick { get; set; } = 0.35;
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
    public double MinCashLootPercent { get; set; } = 0.05;
    public double MaxCashLootPercent { get; set; } = 0.20;
    public double MinProductLootPercent { get; set; } = 0.05;
    public double MaxProductLootPercent { get; set; } = 0.15;
    public double WinnerCrewLossPercent { get; set; } = 0.03;
    public double LoserCrewLossPercent { get; set; } = 0.10;
    public double WeaponLossPercent { get; set; } = 0.08;
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
}
