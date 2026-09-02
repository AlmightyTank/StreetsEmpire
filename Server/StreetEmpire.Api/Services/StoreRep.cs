using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

/// <summary>
/// Standing at the counter, and everything that reads it.
///
/// Money was the only thing the store ever asked for, which made the gun rack a price list rather than
/// a ladder: a player who came into $5,500 on their first evening walked out holding the best weapon in
/// the game, and a player who had traded there every day for a month was no more welcome than one who
/// had never been through the door. That is not a shop, it is a vending machine.
///
/// Rep is what the counter remembers about you. It is earned by trading - every dollar that crosses it
/// counts, so the beer and condoms an ordinary week already buys are building it whether or not anybody
/// is thinking about it - and it is bought outright by investing, which is money for standing and
/// nothing else, held down by a clock so that being rich shortens the climb rather than skipping it.
///
/// What it buys is the guns above a pistol and a cut off every price on the board. The discount is
/// there because the top of the ladder has to be worth reaching after the last gun is already unlocked;
/// without it, standing would stop mattering the day somebody bought their first rifle.
/// </summary>
public static class StoreRep
{
    /// <summary>The rung a player stands on. 1 when the ladder is empty, so nothing is ever gated off.</summary>
    public static int LevelOf(Player player, GameOptions options)
        => options.Store.LevelFor(player.StoreRep)?.Level ?? 1;

    /// <summary>What this player's standing takes off a price, as a share.</summary>
    public static double Discount(Player player, GameOptions options)
        => Math.Clamp((options.Store.LevelFor(player.StoreRep)?.DiscountPercent ?? 0) / 100d, 0, 0.9);

    /// <summary>
    /// What this player actually pays for something listed at <paramref name="listPrice"/>. Rounded
    /// down and floored at a dollar: a discount that made anything free would make rep a printer, since
    /// rep is earned on what is handed over.
    /// </summary>
    public static int Price(Player player, int listPrice, GameOptions options)
        => listPrice <= 0 ? listPrice : Math.Max(1, (int)Math.Floor(listPrice * (1 - Discount(player, options))));

    /// <summary>Credits a spend at the counter. Every dollar of it, whatever was bought.</summary>
    public static void Credit(Player player, long dollarsSpent, GameOptions options)
    {
        if (dollarsSpent <= 0) return;
        player.StoreRep = Math.Max(0, player.StoreRep + dollarsSpent * Math.Max(0, options.Store.RepPerDollarSpent));
    }

    /// <summary>
    /// Takes back the standing a refund undoes. Floored at nothing rather than allowed to go negative,
    /// so selling a jacked car to the chop shop can cost a player credit they earned but never a debt
    /// they cannot pay off.
    /// </summary>
    public static void Debit(Player player, long dollarsRefunded, GameOptions options)
    {
        if (dollarsRefunded <= 0) return;
        player.StoreRep = Math.Max(0, player.StoreRep - dollarsRefunded * Math.Max(0, options.Store.RepPerDollarRefunded));
    }

    /// <summary>The rung a gun needs. 1 - anybody - for a tier configuration never gated.</summary>
    public static int RequiredLevel(GameOptions options, string? weaponKey)
        => Math.Max(1, options.WeaponTier(weaponKey)?.MinRepLevel ?? 1);

    /// <summary>Whether this player has the standing to be handed this gun.</summary>
    public static bool CanHold(Player player, GameOptions options, string? weaponKey)
        => !WeaponTiers.IsWeapon(weaponKey) || LevelOf(player, options) >= RequiredLevel(options, weaponKey);

    /// <summary>
    /// The refusal, worded the same wherever a gun changes hands. Named rather than numbered, because
    /// "you need level 4" tells a player nothing they can go and do about it.
    /// </summary>
    public static string Refusal(GameOptions options, string weaponKey, int required)
        => $"Nobody hands {WeaponTiers.Label(weaponKey).ToLowerInvariant()} to somebody with no standing. "
           + $"That takes {options.Store.LevelName(required)} at the store.";

    /// <summary>Refuses the handover, or does nothing. The one gate every gun counter calls.</summary>
    public static void EnsureCanHold(Player player, GameOptions options, string? weaponKey)
    {
        if (!WeaponTiers.IsWeapon(weaponKey)) return;
        var key = weaponKey!.Trim().ToLowerInvariant();
        var required = RequiredLevel(options, key);
        if (LevelOf(player, options) < required)
            throw new GameRuleException(Refusal(options, key, required));
    }

    /// <summary>When the counter will take another favour, or null when it will take one now.</summary>
    public static DateTime? InvestmentReadyAt(Player player, DateTime nowUtc)
        => player.StoreInvestmentReadyAtUtc is { } ready && ready > nowUtc ? ready : null;
}

/// <summary>
/// How standing is earned, what each rung of it is called, and what money can buy of it. Lives beside
/// the rules that read it rather than in the options file, the way the intel centre and the title
/// ladder do, because a rung means nothing without the thing that reads it.
/// </summary>
public sealed class StoreOptions
{
    /// <summary>
    /// Rep per dollar spent at the counter. A hundredth, so a rifle is worth 55 and a shift's supplies
    /// are worth a handful - deliberately a trickle, because trade is the floor under the climb rather
    /// than the way up it. Paid on cash actually handed over, so a discounted price earns less.
    /// </summary>
    public double RepPerDollarSpent { get; set; } = 0.01;

    /// <summary>
    /// What the chop shop's buy-back takes off again, per dollar returned. Equal to the earning rate on
    /// purpose: without it, buying a ride for $25,000 and selling it back for $15,000 would be 250 rep
    /// for a net $10,000 - two and a half times the going rate, uncapped, and repeatable all evening.
    /// Reversing it at the same rate leaves a round trip worth exactly the money that stayed spent.
    /// </summary>
    public double RepPerDollarRefunded { get; set; } = 0.01;

    /// <summary>
    /// The ladder, lowest first. Empty here like the gun rack and the storage tables: the binder appends
    /// to a pre-populated list rather than replacing it, so shipping defaults in the initializer would
    /// merge them with appsettings and let a stale rung win the lookup.
    /// </summary>
    public List<StoreRepLevelOptions> Levels { get; set; } = [];

    /// <summary>Money for standing and nothing else. Empty for the same reason.</summary>
    public List<StoreInvestmentOptions> Investments { get; set; } = [];

    /// <summary>Who runs the counter in each town. Empty for the same reason.</summary>
    public List<StoreTraderOptions> Traders { get; set; } = [];

    /// <summary>What the trader asks people to bring them, and what it is worth.</summary>
    public TraderJobOptions Jobs { get; set; } = new();

    /// <summary>What is on the counter, and the two clocks that put it there.</summary>
    public TraderShelfOptions Shelf { get; set; } = new();

    public void ApplyDefaultsWhereEmpty()
    {
        Jobs.Reroll.ApplyDefaultsWhereEmpty();

        if (Levels.Count == 0)
            Levels =
            [
                // Rep is a hundredth of a dollar through the counter, so these rungs read as $30,000,
                // $300,000, $1.5M and $5M of trade.
                //
                // The gaps widen on purpose, and by a lot. A ladder with even rungs is one you stop
                // noticing after the first, and the guns hanging off these are not evenly spaced either:
                // the step from a pistol to a shotgun is pocket money and the step to a rifle is an
                // empire's worth of trading. The first rung is an evening, which is the whole of what it
                // needs to be - a new player wanting a shotgun should not be told to come back in a week.
                // The last is a month of being a regular, and it should be.
                //
                // These were tripled and then some when the guns above the pistol got dearer, because the
                // rise had quietly outrun them: at $5,000 an SMG, twenty of them was $100,000, which was
                // the whole of the old third rung. A ladder the purchases it gates can climb by
                // themselves is not gating anything.
                new StoreRepLevelOptions { Level = 1, Name = "Nobody", Rep = 0, DiscountPercent = 0 },
                new StoreRepLevelOptions { Level = 2, Name = "Regular", Rep = 300, DiscountPercent = 2 },
                new StoreRepLevelOptions { Level = 3, Name = "Trusted", Rep = 3_000, DiscountPercent = 4 },
                new StoreRepLevelOptions { Level = 4, Name = "Connected", Rep = 15_000, DiscountPercent = 6 },
                // Nothing new on the rack here. What it is for is the last two points off every price in
                // the shop, for a player who already owns the best gun there is.
                new StoreRepLevelOptions { Level = 5, Name = "Made", Rep = 50_000, DiscountPercent = 8 }
            ];

        if (Investments.Count == 0)
            Investments =
            [
                // Each buys rep at a better rate than trading does, and each shuts the counter for
                // longer than the last. That pairing is the whole shape of it: money shortens the climb
                // and the clock stops it from erasing the climb. A player taking the biggest favour they
                // can reach every time it comes back is spending $250,000 a day for 7,500 rep, which is
                // three weeks of ordinary trading bought in one.
                //
                // These were raised with the ladder but deliberately by less than it, which is what makes
                // the wider gaps wider in days and not only in dollars. Scaling both together would have
                // moved every number on the page and changed nothing anybody could feel.
                new StoreInvestmentOptions
                {
                    Key = "tab",
                    Name = "Cover the counter's tab",
                    Cost = 5_000,
                    Rep = 100,
                    CooldownHours = 6,
                    MinLevel = 1,
                    Description = "A night's drinking for the people who work the counter. Cheap, quick, and remembered."
                },
                new StoreInvestmentOptions
                {
                    Key = "shipment",
                    Name = "Stake the next shipment",
                    Cost = 50_000,
                    Rep = 1_200,
                    CooldownHours = 12,
                    MinLevel = 2,
                    Description = "Your money fronts the crates. You never see one of them, and everyone hears whose money it was."
                },
                new StoreInvestmentOptions
                {
                    Key = "block",
                    Name = "Buy into the block",
                    Cost = 250_000,
                    Rep = 7_500,
                    CooldownHours = 24,
                    MinLevel = 3,
                    Description = "What the street around the shop costs to keep quiet. The counter stays open because you paid for it."
                }
            ];

        if (Traders.Count == 0)
            Traders =
            [
                // One to a town, fixed rather than rolled, so a player's dealer stays their dealer.
                // Each works out of somewhere that is plainly the wrong end of that city, because the
                // whole conceit is that this is the person the legitimate trade does not serve.
                // Price and breadth against the patter each of them already had, because the line
                // under their name was doing this job in prose while the shelf sold the same nine things
                // at the same price in every town in the country.
                //
                // The trade runs one way down the list: the cheap shops are the narrow ones. Nobody is
                // strictly better than anybody, and which town you are standing in is finally a question
                // about buying as well as selling. Rifles exist in three towns out of eight, so the top
                // of the standing ladder is a thing you travel for.
                //
                // Every one of them carries condoms, beer and pistols whatever this says. See
                // StoreTrader.Always.
                new StoreTraderOptions
                {
                    City = "Chicago", Name = "Auntie Vasska",
                    Pitch = "a butcher's cold room on the South Side",
                    Patter = "You want it cheap, you want it now, or you want it clean. Pick two.",
                    // She says it herself. The cheapest counter in the game and very nearly the emptiest.
                    PricePercent = 84,
                    Stocks = ["medicine"],
                },
                new StoreTraderOptions
                {
                    City = "Detroit", Name = "Sallow Pete",
                    Pitch = "a garage under the Fisher with the lights off",
                    Patter = "Everything here fell off something. Do not ask off what.",
                    // Wide and cheap because none of it was bought. No rifles: the one gun that does not
                    // fall off the back of anything.
                    PricePercent = 90,
                    Stocks = ["medicine", "poison", "shotguns", "smgs", "rides"],
                },
                new StoreTraderOptions
                {
                    City = "Atlanta", Name = "Miss Ondine",
                    Pitch = "a hair shop on Bankhead that never has anybody in the chairs",
                    Patter = "Sit down. Talk quiet. Pay before you stand up.",
                    PricePercent = 95,
                    Stocks = ["medicine", "shotguns"],
                },
                new StoreTraderOptions
                {
                    City = "Houston", Name = "Bayou Lacroix",
                    Pitch = "a bait shop on the ship channel",
                    Patter = "Ice chest on the left is fish. Do not open the one on the right.",
                    // What comes off a boat: the ugly end of the shelf, and no medicine at all.
                    PricePercent = 96,
                    Stocks = ["poison", "shotguns", "rides"],
                },
                new StoreTraderOptions
                {
                    City = "New York", Name = "Rat-Face Ochoa",
                    Pitch = "a lock-up behind a shuttered laundromat",
                    Patter = "You again. Good. The last man who came twice is dead, so.",
                    // The baseline the rest of the country is read against: the whole shelf, at exactly
                    // the list price. Somewhere has to be par, and the biggest city is the one that can
                    // get hold of anything without either wanting a favour for it or charging for the
                    // trouble. Every other counter is a trade against this one.
                    PricePercent = 100,
                    Stocks = ["medicine", "poison", "shotguns", "smgs", "rifles", "rides"],
                },
                new StoreTraderOptions
                {
                    City = "Miami", Name = "Duchess Oyelaran",
                    Pitch = "the back of a boat hire that owns no boats",
                    Patter = "Money first. Questions never. We will be friends.",
                    PricePercent = 108,
                    Stocks = ["medicine", "poison", "shotguns", "smgs", "rifles", "rides"],
                },
                new StoreTraderOptions
                {
                    City = "Los Angeles", Name = "Sunny Delgado",
                    Pitch = "a storage unit off Alameda with the number filed off",
                    Patter = "I sell to everybody. That is not a promise, it is a warning.",
                    PricePercent = 112,
                    Stocks = ["medicine", "poison", "shotguns", "smgs", "rifles", "rides"],
                },
                new StoreTraderOptions
                {
                    City = "Las Vegas", Name = "Half-Deck Mo",
                    Pitch = "a laundry chute at a motel that burned down twice",
                    Patter = "House always wins. I am not the house. I am the man out back.",
                    // The dearest counter in the country, and it carries the whole illegal end of the
                    // shelf. A town built on people who will pay anything at four in the morning, run by
                    // the man out the back of it - so everything is here and none of it is a bargain.
                    PricePercent = 130,
                    Stocks = ["medicine", "poison", "shotguns", "smgs", "rifles", "rides"],
                }
            ];
    }

    /// <summary>The ladder in order, lowest first, whatever order configuration listed it in.</summary>
    public IReadOnlyList<StoreRepLevelOptions> Ladder()
        => Levels.OrderBy(x => x.Rep).ThenBy(x => x.Level).ToList();

    /// <summary>The rung this much rep stands on, or null when nothing is configured.</summary>
    public StoreRepLevelOptions? LevelFor(double rep)
        => Ladder().LastOrDefault(x => rep >= x.Rep);

    /// <summary>The rung above, or null at the top of the ladder.</summary>
    public StoreRepLevelOptions? NextLevelAfter(double rep)
        => Ladder().FirstOrDefault(x => rep < x.Rep);

    public StoreRepLevelOptions? Level(int level)
        => Levels.FirstOrDefault(x => x.Level == level);

    /// <summary>What a rung is called, for the sentences naming one that is out of reach.</summary>
    public string LevelName(int level)
        => Level(level)?.Name ?? $"level {level}";

    public StoreInvestmentOptions? Investment(string? key)
        => Investments.FirstOrDefault(x => string.Equals(x.Key, key?.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// One rung. The discount is what standing here is worth on every price in the shop; what it unlocks is
/// written on the guns rather than here, so adding a gun is one line of configuration rather than two
/// lists that have to be kept agreeing with each other.
/// </summary>
public sealed class StoreRepLevelOptions
{
    public int Level { get; set; } = 1;
    public string Name { get; set; } = string.Empty;

    /// <summary>Rep needed to stand here.</summary>
    public int Rep { get; set; }

    /// <summary>Off every price at the counter, at this rung and no lower.</summary>
    public int DiscountPercent { get; set; }
}

/// <summary>
/// Money handed over for standing and nothing else.
///
/// Deliberately a worse deal in every way but the two that matter: it is cheaper per point than trading
/// is, and it needs nothing you have to find a use for afterwards. What stops it being the only thing
/// anybody does is the cooldown, which is one clock across all of them - the counter takes one favour at
/// a time, and a big favour is remembered longer than a small one.
/// </summary>
public sealed class StoreInvestmentOptions
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public long Cost { get; set; }
    public int Rep { get; set; }

    /// <summary>How long the counter takes nothing else after this one.</summary>
    public int CooldownHours { get; set; } = 6;

    /// <summary>The rung you have to already stand on to be offered it.</summary>
    public int MinLevel { get; set; } = 1;
}

