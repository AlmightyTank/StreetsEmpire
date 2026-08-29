using System.Linq.Expressions;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

public sealed class EconomyService(IOptionsSnapshot<GameOptions> options, IGameRandom random, HideoutService hideout, PimpRoster pimps)
{
    private readonly GameOptions _options = options.Value;

    /// <summary>
    /// The net worth formula as an expression tree so the database can sort, count, and total by it
    /// instead of the API loading every player to rank them. Kept in step with
    /// <see cref="CalculateNetWorth"/> by a rule test that compares the two.
    /// </summary>
    /// <summary>
    /// Everything a player is worth, hideout included. This is the number the board ranks by.
    /// </summary>
    public Expression<Func<Player, long>> NetWorthExpression { get; } = BuildNetWorthExpression(options.Value, withHideout: true);

    /// <summary>
    /// Everything a player is worth that somebody could actually carry off.
    ///
    /// The anti-farm rules exist to stop the strong robbing the weak, and they answer that by weighing
    /// the two sides against each other. A hideout is the one thing in the game nobody can take, so
    /// counting it there would decide fights on the strength of a building: a well-built player would
    /// be locked out of attacking anyone near their own size, and left open to heavyweights who would
    /// turn up to find nothing worth the trip.
    /// </summary>
    public Expression<Func<Player, long>> PlunderExpression { get; } = BuildNetWorthExpression(options.Value, withHideout: false);

    private static Expression<Func<Player, long>> BuildNetWorthExpression(GameOptions options, bool withHideout)
    {
        // Pulled out as locals so the expression tree closes over four plain numbers rather than over a
        // list it would have to walk in the database. A rack is worth what the shop charges for the guns
        // on it, tier by tier, which is why this cannot be a count times one price any more.
        var pistol = PriceOf(options, WeaponTiers.Pistol);
        var shotgun = PriceOf(options, WeaponTiers.Shotgun);
        var smg = PriceOf(options, WeaponTiers.Smg);
        var rifle = PriceOf(options, WeaponTiers.Rifle);

        Expression<Func<Player, long>> portable = player => player.Cash
                     + player.BankCash
                     + (long)player.Pimps * options.PimpNetWorth
                     + (long)player.Hoes * options.HoeNetWorth
                     + (long)player.Thugs * options.ThugNetWorth
                     + (long)player.Condoms * options.CondomPrice
                     + (long)player.Beer * options.BeerPrice
                     + (long)player.Pistols * pistol
                     + (long)player.Shotguns * shotgun
                     + (long)player.Smgs * smg
                     + (long)player.Rifles * rifle
                     + (long)player.Medicine * options.MedicineNetWorth
                     + (long)player.Poison * options.PoisonNetWorth
                     + (long)player.Rides * options.RideNetWorth
                     + (long)player.Weed * options.WeedNetWorth
                     + (long)player.Moonshine * options.MoonshineNetWorth
                     + (long)player.Cut * options.CutNetWorth
                     // Coke is worth what it is, not what it weighs. Math.Pow translates to the
                     // database's own power(), so ranking still happens there rather than in memory.
                     + (long)(player.Coke * options.CokeNetWorth
                              * Math.Pow(player.CokePurity, options.CokePurityPricePower));

        if (!withHideout) return portable;

        // A player with no hideout row is worth no building, and the null check has to be in the tree
        // rather than around it: this runs in the database, where the join may find nothing.
        var self = portable.Parameters[0];
        var hideout = Expression.Property(self, nameof(Player.Hideout));
        var value = Expression.Condition(
            Expression.Equal(hideout, Expression.Constant(null, typeof(Models.Hideout))),
            Expression.Constant(0L),
            HideoutValue.Build(hideout, options));

        return Expression.Lambda<Func<Player, long>>(Expression.Add(portable.Body, value), self);
    }

    /// <summary>A tier's shop price, or the legacy single-weapon price if the table has no such gun.</summary>
    private static int PriceOf(GameOptions options, string tier)
        => options.WeaponTier(tier)?.Price ?? options.WeaponPrice;

    /// <summary>
    /// Players ranked above the given standing, for turning a net worth into a rank without
    /// materializing the player table. Mirrors the leaderboard's net worth then oldest-first order.
    /// </summary>
    public Expression<Func<Player, bool>> RanksAbove(long netWorth, DateTime createdAtUtc)
    {
        var player = NetWorthExpression.Parameters[0];
        var theirNetWorth = NetWorthExpression.Body;
        var standing = Expression.Constant(netWorth);
        var outranked = Expression.OrElse(
            Expression.GreaterThan(theirNetWorth, standing),
            Expression.AndAlso(
                Expression.Equal(theirNetWorth, standing),
                Expression.LessThan(
                    Expression.Property(player, nameof(Player.CreatedAtUtc)),
                    Expression.Constant(createdAtUtc))));

        return Expression.Lambda<Func<Player, bool>>(outranked, player);
    }

    /// <summary>
    /// Players worth at least a threshold, so a caller can pull a band of the ladder without reading
    /// the whole table. Built off the same expression body as everything else here.
    /// </summary>
    public Expression<Func<Player, bool>> NetWorthAtLeast(long threshold)
    {
        var player = NetWorthExpression.Parameters[0];
        var body = Expression.GreaterThanOrEqual(NetWorthExpression.Body, Expression.Constant(threshold));
        return Expression.Lambda<Func<Player, bool>>(body, player);
    }

    /// <summary>The same filter over what could be taken, for the places choosing who to fight.</summary>
    public Expression<Func<Player, bool>> PlunderAtLeast(long threshold)
    {
        var player = PlunderExpression.Parameters[0];
        var body = Expression.GreaterThanOrEqual(PlunderExpression.Body, Expression.Constant(threshold));
        return Expression.Lambda<Func<Player, bool>>(body, player);
    }

    /// <summary>
    /// Projects a player down to just their position in the net worth order, so ranking a page does
    /// not drag whole player rows back with it.
    /// </summary>
    public Expression<Func<Player, PlayerStanding>> StandingExpression()
    {
        var player = NetWorthExpression.Parameters[0];
        var standing = Expression.New(
            typeof(PlayerStanding).GetConstructor([typeof(long), typeof(DateTime)])!,
            NetWorthExpression.Body,
            Expression.Property(player, nameof(Player.CreatedAtUtc)));

        return Expression.Lambda<Func<Player, PlayerStanding>>(standing, player);
    }

    /// <summary>
    /// Projects each player to the crew they run with and what they are worth.
    ///
    /// Built by composition for the same reason <see cref="StandingExpression"/> is: the net worth sum
    /// is one expression tree and every reader of it has to be the same sum. EF cannot take a
    /// pre-built expression as the selector of a grouped Sum, so the grouping happens in memory over
    /// the aligned players alone - which is dozens of rows, and every one of their net worths was still
    /// worked out by the database.
    /// </summary>
    public Expression<Func<Player, AllianceStanding>> AllianceStandingExpression()
    {
        var player = NetWorthExpression.Parameters[0];
        var standing = Expression.New(
            typeof(AllianceStanding).GetConstructor([typeof(long?), typeof(long)])!,
            Expression.Property(player, nameof(Player.AllianceId)),
            NetWorthExpression.Body);

        return Expression.Lambda<Func<Player, AllianceStanding>>(standing, player);
    }

    /// <summary>
    /// The in-memory twin of <see cref="RanksAbove"/>, for ranking a page of players against the
    /// contenders already fetched for it. A rule test keeps the two in agreement.
    /// </summary>
    public static bool Outranks(PlayerStanding contender, PlayerStanding standing)
        => contender.NetWorth > standing.NetWorth
           || (contender.NetWorth == standing.NetWorth && contender.CreatedAtUtc < standing.CreatedAtUtc);

    /// <summary>
    /// Ranks one standing against every player known to outrank the weakest member of its page.
    /// That set is a superset of the players outranking this standing, so the count is the true
    /// global rank.
    /// </summary>
    public static int RankOf(PlayerStanding standing, IReadOnlyList<PlayerStanding> contenders)
        => contenders.Count(x => Outranks(x, standing)) + 1;

    public IReadOnlyList<StoreItemResponse> GetStore()
    {
        var store = new List<StoreItemResponse>
        {
            new("condoms", "Condoms", "Crew Supplies", _options.CondomPrice, "Consumed while your hoes work the streets."),
            new("beer", "Beer", "Crew Supplies", _options.BeerPrice, "Consumed by thugs during street operations."),
            new("medicine", "Medicine", "Crew Supplies", _options.MedicinePrice, $"Treats {Math.Max(1, _options.Strikes.Infest.HoesCuredPerCrate)} hoes when a rival infests your house. Does nothing until then."),
            new("poison", "Poison", "Crew Supplies", _options.PoisonPrice, $"One dose reaches {Math.Max(1, _options.Strikes.Infest.HoesHitPerDose)} of somebody else's hoes. The other end of medicine, and the only way to infest a house.")
        };

        // The gun counter, cheapest first. Every row says the same two things because they are the two
        // that decide the purchase: any gun covers one thug for morale, and what separates them is what
        // that thug is worth in a fight.
        foreach (var tier in _options.Weapons.OrderBy(x => x.Price))
            store.Add(new StoreItemResponse(
                tier.Key,
                WeaponTiers.Label(tier.Key),
                "Security",
                tier.Price,
                tier.Firepower <= 1
                    ? "Covers one thug. The cheapest way to keep a big crew content."
                    : $"Covers one thug like any gun, and fights {tier.Firepower:0.#}x as hard as a pistol."));

        store.Add(new StoreItemResponse("rides", "Low-Rider", "Chop Shop", _options.RidePrice, $"Needed for a drive-by, and worth jacking. The shop buys them back at ${_options.RideSalePrice:N0}."));
        return store;
    }

    /// <summary>
    /// What this player's rack is worth in a fight, in units of one pistol. Capped by the crew that
    /// would carry it, so buying guns nobody can hold buys nothing.
    /// </summary>
    public double FirepowerOf(Player player, int? thugs = null)
        => player.Armoury.Firepower(thugs ?? player.Thugs, _options.WeaponFirepower());

    public long CalculateNetWorth(Player player) => NetWorthOf(player, _options);

    /// <summary>What of this player's worth could actually be carried off. See PlunderExpression.</summary>
    public long CalculatePlunder(Player player) => PlunderOf(player, _options);

    /// <summary>
    /// Net worth without needing the service, for the places that measure how established a player is
    /// rather than rank them. Delegated to rather than copied, so there are still only two forms of
    /// this sum - this one and the expression tree - and the test that compares them still guards it.
    /// </summary>
    public static long NetWorthOf(Player player, GameOptions options)
        => PlunderOf(player, options) + HideoutValue.Of(player.Hideout, options);

    /// <summary>
    /// The portable half of the sum: everything a raid could take. Net worth is this plus the building.
    /// </summary>
    public static long PlunderOf(Player player, GameOptions options)
        => player.Cash
           + player.BankCash
           + (long)player.Pimps * options.PimpNetWorth
           + (long)player.Hoes * options.HoeNetWorth
           + (long)player.Thugs * options.ThugNetWorth
           + (long)player.Condoms * options.CondomPrice
           + (long)player.Beer * options.BeerPrice
           + options.WeaponValue(player.Armoury)
           + (long)player.Medicine * options.MedicineNetWorth
           + (long)player.Poison * options.PoisonNetWorth
           + (long)player.Rides * options.RideNetWorth
           + (long)player.Weed * options.WeedNetWorth
           + (long)player.Moonshine * options.MoonshineNetWorth
           + (long)player.Cut * options.CutNetWorth
           + (long)(player.Coke * options.CokeNetWorth * options.PurityMultiplier(player.CokePurity));

    public CrewReportResponse GetCrewReport(Player player)
    {
        var morale = _options.Morale;
        var crew = _options.Crew;
        var managementCapacity = Math.Max(1, player.Pimps) * morale.HoesManagedPerPimp;
        var unmanagedHoes = Math.Max(0, player.Hoes - managementCapacity);
        var armedThugs = Math.Min(player.Weapons, player.Thugs);
        var uncoveredThugs = Math.Max(0, player.Thugs - player.Weapons);
        var condomsNeeded = RequiredUpkeep(player.Hoes, _options.MaxActionTurns, morale.TurnsPerCondom);
        var beerNeeded = RequiredUpkeep(player.Thugs, _options.MaxActionTurns, morale.TurnsPerBeer);
        var condomCost = (long)condomsNeeded * _options.CondomPrice;
        var beerCost = (long)beerNeeded * _options.BeerPrice;
        var totalCrew = TotalCrew(player);
        var hqRestCashCost = HqCashCost(totalCrew, morale.HqRestCashPerCrew);
        var hqPartyCashCost = HqCashCost(totalCrew, morale.HqPartyCashPerCrew);

        // What the room could carry if it were completely full, which is the limit a player cannot buy
        // their way past. Beyond it every full-length shift runs a shortage until the room is bigger.
        var capacity = hideout.CapacityFor(player.Hideout);
        var hoesStorageCanSupply = SupportableCrew(capacity.MaxCondoms, _options.MaxActionTurns, morale.TurnsPerCondom);
        var thugsStorageCanSupply = SupportableCrew(capacity.MaxBeer, _options.MaxActionTurns, morale.TurnsPerBeer);
        var storageLevelToSupplyCrew = player.Hoes <= hoesStorageCanSupply && player.Thugs <= thugsStorageCanSupply
            ? null
            : hideout.StorageLevelThatHolds(condomsNeeded, beerNeeded);
        // Whichever of the two runs out first decides how long a shift can actually be supplied.
        var suppliedTurns = Math.Clamp(
            Math.Min(
                SuppliedTurns(capacity.MaxCondoms, player.Hoes, morale.TurnsPerCondom, _options.MaxActionTurns),
                SuppliedTurns(capacity.MaxBeer, player.Thugs, morale.TurnsPerBeer, _options.MaxActionTurns)),
            0,
            _options.MaxActionTurns);

        return new CrewReportResponse(
            managementCapacity,
            unmanagedHoes,
            armedThugs,
            uncoveredThugs,
            condomsNeeded,
            beerNeeded,
            hoesStorageCanSupply,
            thugsStorageCanSupply,
            storageLevelToSupplyCrew,
            suppliedTurns,
            crew.FireHoeMoralePenalty,
            crew.FireThugMoralePenalty,
            crew.MaxFireMoralePenalty,
            condomCost,
            beerCost,
            condomCost + beerCost,
            crew.HirePimpCost,
            crew.HireHoeCost,
            crew.HireThugCost,
            crew.MinHoeMoraleToHire,
            crew.MinThugMoraleToHire,
            morale.HqRestTurnCost,
            hqRestCashCost,
            morale.HqRestMoraleGain,
            morale.HqPartyTurnCost,
            hqPartyCashCost,
            RequiredRecoverySupply(player.Thugs, morale.HqPartyBeerPerThug),
            RequiredRecoverySupply(player.Hoes, morale.HqPartyWeedPerHoes),
            morale.HqPartyHoeMoraleGain,
            morale.HqPartyThugMoraleGain);
    }

    /// <param name="territory">
    /// What the player's ground adds to the take. Passed in rather than looked up, because this runs
    /// synchronously inside an action that already has the player loaded.
    /// </param>
    /// <param name="awayPimpIds">
    /// Pimps who are not at home. Street work is blocked while a mission is out, so this used to be
    /// empty by definition, but a pimp posted to ground is away while the player carries on working.
    /// </param>
    /// <param name="district">
    /// Where the crew is working. Null takes the neutral district, which is exactly the base numbers, so
    /// every caller written before there was anywhere to choose keeps working the shift it always did.
    /// </param>
    public ActionResultResponse Scout(Player player, int turns, bool autoBuySupplies = false, TerritoryEffects? territory = null, IReadOnlyCollection<long>? awayPimpIds = null, string? district = null)
    {
        TravelGate.EnsureLanded(player);
        ValidateTurns(player, turns, _options.MaxActionTurns, "Work the streets");

        var street = _options.StreetAction;
        // Named rather than accepted quietly: asking for a district that does not exist is a caller
        // getting it wrong, and silently working somewhere else would hide that from them.
        var where = district is null
            ? street.DefaultDistrict()
            : street.District(district)
              ?? throw new GameRuleException($"Work one of: {string.Join(", ", street.Districts.Select(x => x.Name))}.");
        var morale = _options.Morale;
        var stockBefore = StockLevels.From(player);
        var restock = autoBuySupplies ? RestockForAction(player, turns) : Restock.None;
        var hoesBefore = player.Hoes;
        var thugsBefore = player.Thugs;
        var hoeHappinessBefore = player.HoeHappiness;
        var thugHappinessBefore = player.ThugHappiness;

        long gross = 0;
        var recruitedPimps = 0;
        var recruitedHoes = 0;
        var recruitedThugs = 0;
        var condomsFound = 0;
        var beerFound = 0;
        var weedFound = 0;
        var cokeFound = 0;

        for (var i = 0; i < turns; i++)
        {
            gross += street.BaseGrossPerTurn
                + player.Hoes * Roll(street.HoeGrossPerTurn)
                + player.Pimps * Roll(street.PimpGrossPerTurn);

            if (RollChance(street.PimpRecruitChance * where.Scale(where.PimpRecruitPercent))) recruitedPimps++;
            if (RollChance(street.HoeRecruitChance * where.Scale(where.HoeRecruitPercent))) recruitedHoes++;
            if (RollChance(street.ThugRecruitChance * where.Scale(where.ThugRecruitPercent))) recruitedThugs++;

            var finds = where.Scale(where.FindPercent);
            condomsFound += RollFind(street.Finds.Condoms, finds);
            beerFound += RollFind(street.Finds.Beer, finds);
            weedFound += RollFind(street.Finds.Weed, finds);
            cokeFound += RollFind(street.Finds.Coke, finds);
        }

        // The hideout only has so many beds, so recruits beyond it walk away.
        var recruitsOffered = recruitedPimps + recruitedHoes + recruitedThugs;
        recruitedPimps = Math.Min(recruitedPimps, hideout.CrewRoom(player, "pimps"));
        recruitedHoes = Math.Min(recruitedHoes, hideout.CrewRoom(player, "hoes"));
        recruitedThugs = Math.Min(recruitedThugs, hideout.CrewRoom(player, "thugs"));
        var recruitsTurnedAway = recruitsOffered - (recruitedPimps + recruitedHoes + recruitedThugs);

        // Where they worked decides what the take was worth before anybody's cut of it. Applied to the
        // whole shift rather than per turn so the rounding happens once.
        gross = (long)Math.Round(gross * where.Scale(where.GrossPercent), MidpointRounding.AwayFromZero);

        // Hustlers at home lift the take. Street work is blocked while a mission is out, so nobody
        // is away commanding at this point.
        var streetBonusPercent = pimps.StreetBonusPercent(player, awayPimpIds ?? []) + (territory?.StreetIncomePercent ?? 0);
        var grossBeforeBonus = gross;
        gross += (long)Math.Round(gross * (streetBonusPercent / 100.0), MidpointRounding.AwayFromZero);

        var crewPayout = (long)Math.Round(gross * (player.HoeCutPercent / 100.0), MidpointRounding.AwayFromZero);

        // The crew's cut comes off first and the crew's crew comes off next. Dues are taken here, beside
        // the hoe cut, because it is the same kind of thing and reads in the same sentence: a share of
        // what the shift grossed, gone before the money reaches you.
        //
        // Taken off the gross rather than off what is left, so the two cuts do not compound - a house
        // paying 40% and dues of 20% gives up 60% of a shift, not 52%. Compounding would make the second
        // rate quietly mean something different depending on the first.
        var duesPercent = player.Alliance is { } crew ? Math.Clamp(crew.DuesPercent, 0, 100) : 0;
        var dues = (long)Math.Round(gross * (duesPercent / 100.0), MidpointRounding.AwayFromZero);
        var playerProfit = Math.Max(0, gross - crewPayout - dues);
        // Never more than there was. A crew and a house between them cannot take more than the shift
        // made, whatever the two rates add up to.
        dues = Math.Min(dues, Math.Max(0, gross - crewPayout));
        if (player.Alliance is { } paid && dues > 0)
            paid.Treasury += dues;

        player.Turns -= turns;
        player.Cash += playerProfit;
        // Working the streets is illegal too, so it draws attention whether or not anything is held,
        // and a watchful town notices a shift sooner than a quiet one does.
        player.Heat += Math.Max(0, _options.Hideout.HeatPerStreetTurn) * turns
                       * _options.CityMarkets.HeatMultiplier(player.City)
                       * where.Scale(where.HeatPercent);
        // Recruited pimps arrive as named crew, which also moves the counter.
        var recruitedPimpNames = pimps.Hire(player, recruitedPimps, DateTime.UtcNow).Select(x => x.Name).ToList();
        player.Hoes += recruitedHoes;
        player.Thugs += recruitedThugs;
        player.Condoms += condomsFound;
        player.Beer += beerFound;
        player.Weed += weedFound;
        // Found on the street, so it is whatever it is: treated as clean.
        player.AddCoke(cokeFound, 1);

        var condomsNeeded = RequiredUpkeep(hoesBefore, turns, morale.TurnsPerCondom);
        var condomsUsed = Math.Min(player.Condoms, condomsNeeded);
        var condomShortage = condomsNeeded - condomsUsed;
        player.Condoms -= condomsUsed;

        var beerNeeded = RequiredUpkeep(thugsBefore, turns, morale.TurnsPerBeer);
        var beerUsed = Math.Min(player.Beer, beerNeeded);
        player.Beer -= beerUsed;
        // Moonshine drinks the same. Poured only once the bought beer is gone, so a player is never
        // quietly spending contraband while a legal barrel sits next to it.
        var moonshineUsed = Math.Min(player.Moonshine, beerNeeded - beerUsed);
        player.Moonshine -= moonshineUsed;
        var beerShortage = beerNeeded - beerUsed - moonshineUsed;

        var managementCapacity = Math.Max(1, player.Pimps) * morale.HoesManagedPerPimp;
        var unmanagedHoes = Math.Max(0, player.Hoes - managementCapacity);
        var uncoveredThugs = Math.Max(0, player.Thugs - player.Weapons);

        var cutEffect = (player.HoeCutPercent - morale.BaselineHoeCutPercent) * turns * morale.HoeCutMoraleScalePerTurn;
        var hoeDelta = morale.HoeStreetWorkGainPerTurn * turns
            + cutEffect
            - ShortagePenalty(condomShortage, condomsNeeded, turns, morale.CondomShortagePenalty)
            - unmanagedHoes * morale.UnmanagedHoePenalty;
        var thugDelta = morale.ThugStreetWorkGainPerTurn * turns
            - ShortagePenalty(beerShortage, beerNeeded, turns, morale.BeerShortagePenalty)
            - uncoveredThugs * morale.UncoveredThugPenalty;

        player.HoeHappiness = ClampHappiness(player.HoeHappiness + hoeDelta);
        player.ThugHappiness = ClampHappiness(player.ThugHappiness + thugDelta);

        var hoeDeserters = RollDeserters(player.Hoes, player.HoeHappiness, morale);
        var thugDeserters = RollDeserters(player.Thugs, player.ThugHappiness, morale);
        player.Hoes -= hoeDeserters;
        player.Thugs -= thugDeserters;
        var walkouts = pimps.SettleStreetWork(player, turns, (player.HoeHappiness + player.ThugHappiness) / 2, DateTime.UtcNow);
        var overflow = hideout.Settle(player, stockBefore);

        var summary = string.Empty;
        if (restock.Any)
            summary += $"Auto-bought {restock.Describe()} for ${restock.Cost:N0}. ";
        summary += $"Worked the {where.Name} for {turns} turn{Plural(turns)}. Grossed ${gross:N0}; crew cut was ${crewPayout:N0}; you kept ${playerProfit:N0}.";
        if (dues > 0)
            summary += $" {player.Alliance!.Name} took ${dues:N0} in dues.";
        if (recruitedPimps + recruitedHoes + recruitedThugs > 0)
            summary += $" Recruited {recruitedPimps} pimp(s), {recruitedHoes} hoe(s), and {recruitedThugs} thug(s).";
        if (recruitedPimpNames.Count > 0)
            summary += $" {string.Join(" and ", recruitedPimpNames)} signed on.";
        var groundBonusPercent = territory?.StreetIncomePercent ?? 0;
        if (streetBonusPercent > 0)
            summary += groundBonusPercent > 0 && streetBonusPercent > groundBonusPercent
                ? $" Your hustlers and your corners added {streetBonusPercent}% to the take."
                : groundBonusPercent == streetBonusPercent
                    ? $" Your corners added {streetBonusPercent}% to the take."
                    : $" Your hustlers added {streetBonusPercent}% to the take.";
        if (recruitsTurnedAway > 0)
            summary += $" {recruitsTurnedAway} recruit(s) walked because your hideout is full.";
        if (condomsFound + beerFound + weedFound + cokeFound > 0)
            summary += $" Found {condomsFound} condoms, {beerFound} beer, {weedFound} weed, and {cokeFound} coke.";
        if (condomShortage > 0) summary += $" Condom shortage: {condomShortage}.";
        if (beerShortage > 0) summary += $" Beer shortage: {beerShortage}.";
        if (unmanagedHoes > 0) summary += $" {unmanagedHoes} hoe(s) are beyond your pimp management capacity.";
        if (uncoveredThugs > 0) summary += $" {uncoveredThugs} thug(s) do not have weapons.";
        if (hoeDeserters > 0 || thugDeserters > 0)
            summary += $" {hoeDeserters} hoe(s) and {thugDeserters} thug(s) walked out due to low morale.";
        foreach (var walkout in walkouts)
            summary += $" {walkout.Name} had enough and walked out on you.";
        summary += overflow.Describe();

        return new ActionResultResponse(summary, player.Turns, new Dictionary<string, object?>
        {
            ["turnsSpent"] = turns,
            ["district"] = where.Key,
            ["districtName"] = where.Name,
            ["hustlerBonusPercent"] = streetBonusPercent,
            ["grossBeforeHustlers"] = grossBeforeBonus,
            ["autoBoughtCondoms"] = restock.Condoms,
            ["autoBoughtBeer"] = restock.Beer,
            ["autoBuyCost"] = restock.Cost,
            ["recruitsTurnedAway"] = recruitsTurnedAway,
            ["cashBankedByOverflow"] = overflow.CashBanked,
            ["condomsLostToStorage"] = overflow.CondomsLost,
            ["beerLostToStorage"] = overflow.BeerLost,
            ["weedLostToStorage"] = overflow.WeedLost,
            ["cokeLostToStorage"] = overflow.CokeLost,
            ["gross"] = gross,
            ["crewPayout"] = crewPayout,
            ["allianceDues"] = dues,
            ["allianceDuesPercent"] = duesPercent,
            ["playerProfit"] = playerProfit,
            ["recruitedPimps"] = recruitedPimps,
            ["recruitedHoes"] = recruitedHoes,
            ["recruitedThugs"] = recruitedThugs,
            ["condomsFound"] = condomsFound,
            ["beerFound"] = beerFound,
            ["weedFound"] = weedFound,
            ["cokeFound"] = cokeFound,
            ["condomsNeeded"] = condomsNeeded,
            ["condomsUsed"] = condomsUsed,
            ["condomShortage"] = condomShortage,
            ["beerNeeded"] = beerNeeded,
            ["beerUsed"] = beerUsed,
            ["beerShortage"] = beerShortage,
            ["managementCapacity"] = managementCapacity,
            ["unmanagedHoes"] = unmanagedHoes,
            ["uncoveredThugs"] = uncoveredThugs,
            ["hoeHappinessBefore"] = Math.Round(hoeHappinessBefore, 2),
            ["hoeHappinessAfter"] = Math.Round(player.HoeHappiness, 2),
            ["hoeHappinessDelta"] = Math.Round(player.HoeHappiness - hoeHappinessBefore, 2),
            ["thugHappinessBefore"] = Math.Round(thugHappinessBefore, 2),
            ["thugHappinessAfter"] = Math.Round(player.ThugHappiness, 2),
            ["thugHappinessDelta"] = Math.Round(player.ThugHappiness - thugHappinessBefore, 2),
            ["hoeDeserters"] = hoeDeserters,
            ["thugDeserters"] = thugDeserters
        });
    }

    public ActionResultResponse Produce(Player player, string? product, int turns, TerritoryEffects? territory = null)
    {
        TravelGate.EnsureLanded(player);
        ValidateTurns(player, turns, _options.MaxActionTurns, "Production");
        var key = NormalizeProduct(product);
        var production = GetProduction(key);
        var stockBefore = StockLevels.From(player);

        var baseUnits = 0;
        for (var i = 0; i < turns; i++)
            baseUnits += random.NextInclusive(production.UnitsMin, production.UnitsMax);

        // The lab is turn-fed: it raises what each production turn yields rather than running itself.
        var labBonusPercent = hideout.ProductionYieldBonusPercent(player.Hideout, key) + (territory?.ProductionYieldPercent ?? 0);
        // Cut is no longer spent here. It used to be consumed silently by any coke run, which meant a
        // player saving it for a batch watched it disappear into production they had not connected it
        // to, and cut could never touch coke that arrived any other way - off a plane, off the board,
        // out of a lab. Stepping on it is its own action now, so the player decides when.
        var produced = (int)Math.Round(baseUnits * (1 + labBonusPercent / 100.0), MidpointRounding.AwayFromZero);

        var totalCost = (long)production.CostPerTurn * turns;
        if (player.Cash < totalCost)
            throw new GameRuleException($"You need ${totalCost:N0} cash on hand for production materials.");

        player.Cash -= totalCost;
        player.Turns -= turns;
        if (key == "weed") player.Weed += produced;
        else player.AddCoke(produced, 1);
        var overflow = hideout.Settle(player, stockBefore);

        var summary = $"Produced {produced:N0} {key} using {turns} turn{Plural(turns)} and ${totalCost:N0} in materials.";
        if (labBonusPercent > 0)
            summary += $" The {key} lab added {labBonusPercent:N0}% yield.";
        summary += overflow.Describe();

        return new ActionResultResponse(
            summary,
            player.Turns,
            new Dictionary<string, object?>
            {
                ["product"] = key,
                ["turnsSpent"] = turns,
                ["unitsProduced"] = produced,
                ["baseUnits"] = baseUnits,
                ["labBonusPercent"] = labBonusPercent,
                ["costPerTurn"] = production.CostPerTurn,
                ["totalCost"] = totalCost,
                ["weedLostToStorage"] = overflow.WeedLost,
                ["cokeLostToStorage"] = overflow.CokeLost
            });
    }

    public WorkshopCraft StartProductionCraft(Player player, string? product, int workUnits, TerritoryEffects? territory, DateTime nowUtc)
    {
        TravelGate.EnsureLanded(player);
        if (workUnits < 1 || workUnits > _options.MaxActionTurns)
            throw new GameRuleException($"Work between 1 and {_options.MaxActionTurns} turns.");
        if (player.Turns < workUnits)
            throw new GameRuleException($"That is {workUnits:N0} turns and you have {player.Turns:N0}.");

        var key = NormalizeProduct(product);
        var production = GetProduction(key);
        var workshopLevel = player.Hideout?.WorkshopLevel ?? 0;
        if (workshopLevel < production.MinWorkshopLevel)
            throw new GameRuleException($"{TradeGoods.Label(key)} needs a level {production.MinWorkshopLevel} workshop. Yours is level {workshopLevel}.");

        var baseUnits = 0;
        for (var i = 0; i < workUnits; i++)
            baseUnits += random.NextInclusive(production.UnitsMin, production.UnitsMax);

        var labBonusPercent = hideout.ProductionYieldBonusPercent(player.Hideout, key) + (territory?.ProductionYieldPercent ?? 0);
        var wanted = (int)Math.Round(baseUnits * (1 + labBonusPercent / 100.0), MidpointRounding.AwayFromZero);
        var capacity = hideout.CapacityFor(player.Hideout);
        var room = TradeGoods.Room(player, capacity, key);
        if (room <= 0)
            throw new GameRuleException($"Your storage has no room for more {key}.");

        var produced = Math.Min(wanted, room);
        var totalCost = (long)production.CostPerTurn * workUnits;
        if (player.Cash < totalCost)
            throw new GameRuleException($"Materials for {workUnits:N0} work unit{Plural(workUnits)} of {key} cost {totalCost:C0}.");

        player.Cash -= totalCost;
        player.Turns -= workUnits;
        var minutes = Math.Max(1, _options.WorkshopCraftMinutesPerTurn) * workUnits;
        var label = TradeGoods.Label(key);
        var summary = $"Producing {produced:N0} {label.ToLowerInvariant()}.";
        if (produced < wanted)
            summary += " Storage capped the batch.";
        if (labBonusPercent > 0)
            summary += $" Lab and territory bonuses added {labBonusPercent:N0}% yield.";

        return new WorkshopCraft
        {
            PlayerId = player.Id,
            Good = key,
            Label = label,
            Quantity = produced,
            UnitCost = produced <= 0 ? 0 : Math.Max(1, totalCost / produced),
            TotalCost = totalCost,
            WorkUnits = workUnits,
            WorkshopLevel = workshopLevel,
            StartedAtUtc = nowUtc,
            CompletesAtUtc = nowUtc.AddMinutes(minutes),
            Summary = summary
        };
    }

    /// <summary>
    /// Steps on the coke you already hold: cut goes in, and the pile comes out bigger.
    ///
    /// Its own action rather than a silent bonus on production, because the coke worth stretching is
    /// usually coke that was never produced. A run comes off a plane with eighty units, the board
    /// sells a hundred, a lab turns some out overnight - none of that used to be reachable by cut,
    /// which only ever applied to units made in the same breath as it was spent.
    ///
    /// One unit of cut makes one unit of coke, so the cut is worth exactly what the coke it becomes is
    /// worth here. What it costs is turns, storage, and notice: coke draws more heat per unit than
    /// anything else, so a stretched pile is a hotter pile.
    /// </summary>
    public ActionResultResponse CutCoke(Player player, int turns)
    {
        TravelGate.EnsureLanded(player);
        ValidateTurns(player, turns, _options.MaxActionTurns, "Cutting coke");

        // Stepping on coke needs a bench that can make the cut it is stepped on with, which is the
        // same level the recipe asks for rather than a second number that could drift away from it.
        var needsLevel = _options.Makeables.FirstOrDefault(x => x.Key == "cut")?.MinWorkshopLevel ?? 1;
        var mixLevel = player.Hideout?.WorkshopLevel ?? 0;
        if (mixLevel < needsLevel)
            throw new GameRuleException($"Stepping on coke needs a level {needsLevel} workshop.");
        if (hideout.WorkshopRequiredTier() is { } needed && (player.Hideout?.Tier ?? 1) < needed)
            throw new GameRuleException($"Stepping on it needs the {hideout.TierName(needed)} or better.");
        if (player.Cut <= 0)
            throw new GameRuleException("You have no cut to work with.");
        if (player.Coke <= 0)
            throw new GameRuleException("You have no coke to step on.");

        var capacity = hideout.CapacityFor(player.Hideout);
        var room = Math.Max(0, capacity.MaxCoke - player.Coke);
        if (room <= 0)
            throw new GameRuleException("Your storage room has no space for more coke.");

        // Bounded by every real limit at once, and capped by the room rather than allowed to overflow:
        // cutting into a full store would destroy cut the player had already paid to make.
        var perTurn = Math.Max(1, _options.Hideout.CutPerTurnPerMixLevel) * mixLevel;
        // Held before the mix, so the summary below can say which of these actually bound the batch.
        var cutAvailable = player.Cut;
        var cokeAvailable = player.Coke;
        var stretched = Math.Min(Math.Min(turns * perTurn, cutAvailable), Math.Min(cokeAvailable, room));
        if (stretched <= 0)
            throw new GameRuleException("There is nothing to gain from a batch this size.");

        // Only the turns the batch actually needed. A player who asks for ten turns on a batch that
        // takes two should not be charged for eight turns of standing about.
        var turnsUsed = Math.Max(1, (int)Math.Ceiling(stretched / (double)perTurn));

        player.Cut -= stretched;
        // Filler is filler. Blending it in is the whole cost of the move: the pile grows and weakens.
        player.AddCoke(stretched, 0);
        player.Turns -= turnsUsed;

        var summary = $"Stepped on {stretched:N0} coke with {stretched:N0} cut, using {turnsUsed} turn{Plural(turnsUsed)}. You now hold {player.Coke:N0} coke.";
        // Name the limit that actually bound the batch. "Something stopped it" is the kind of notice
        // that leaves a player guessing whether to buy cut, sell coke, or build a bigger room.
        if (stretched < turns * perTurn)
            summary += cutAvailable <= stretched
                ? " That was all the cut you had."
                : cokeAvailable <= stretched
                    ? " That was all the coke you had to step on."
                    : " Your storage room would not hold any more.";

        return new ActionResultResponse(
            summary,
            player.Turns,
            new Dictionary<string, object?>
            {
                ["cutUsed"] = stretched,
                ["cokeGained"] = stretched,
                ["turnsSpent"] = turnsUsed,
                ["perTurn"] = perTurn,
                ["mixLevel"] = mixLevel,
                ["cokeHeld"] = player.Coke,
                ["cutHeld"] = player.Cut
            });
    }

    /// <summary>
    /// Turns and materials into weapons. Separate from Produce because weapons are not a product you
    /// sell to the game at a fixed price: they are a supply everyone burns, which is what makes them
    /// worth putting on the board.
    /// </summary>
    public ActionResultResponse Forge(Player player, int turns, string? weapon = null) => Make(player, turns, weapon);

    /// <summary>
    /// Turns and materials into one good.
    ///
    /// One room now. The workshop, the still and the mix house were the same shape wearing three signs,
    /// and what a unit costs to make belongs to the thing being made rather than to the room it happens
    /// in - which is what the guns had been saying since they were given a forge cost of their own.
    /// </summary>
    /// <param name="good">
    /// What to turn out. Null means the best gun the shop can reach, which is what asking a workshop
    /// for nothing in particular has always meant.
    /// </param>
    public ActionResultResponse Make(Player player, int turns, string? good = null)
    {
        var plan = PlanCraft(player, turns, good, requireTurns: true);

        player.Turns -= plan.WorkUnits;
        player.Cash -= plan.TotalCost;
        TradeGoods.Add(player, plan.Good, plan.Quantity);

        var summary = $"Turned out {plan.Quantity:N0} {plan.Label} over {plan.WorkUnits} turn{Plural(plan.WorkUnits)} for {plan.TotalCost:C0} in materials.";
        if (plan.Quantity < plan.Wanted)
            summary += " Storage filled up before the run finished.";

        return new ActionResultResponse(summary, player.Turns, new Dictionary<string, object?>
        {
            ["good"] = plan.Good,
            ["weaponsMade"] = plan.Quantity,
            ["unitsMade"] = plan.Quantity,
            ["turnsSpent"] = plan.WorkUnits,
            ["costPerWeapon"] = plan.UnitCost,
            ["totalCost"] = plan.TotalCost,
            ["storePrice"] = TradeGoods.ReferencePrice(_options, plan.Good, player.City)
        });
    }

    public WorkshopCraft StartCraft(Player player, int workUnits, string? good, DateTime nowUtc)
    {
        var plan = PlanCraft(player, workUnits, good, requireTurns: true);

        player.Turns -= plan.WorkUnits;
        player.Cash -= plan.TotalCost;
        var minutes = Math.Max(1, _options.WorkshopCraftMinutesPerTurn) * plan.WorkUnits;
        return new WorkshopCraft
        {
            PlayerId = player.Id,
            Good = plan.Good,
            Label = TradeGoods.Label(plan.Good),
            Quantity = plan.Quantity,
            UnitCost = plan.UnitCost,
            TotalCost = plan.TotalCost,
            WorkUnits = plan.WorkUnits,
            WorkshopLevel = plan.WorkshopLevel,
            StartedAtUtc = nowUtc,
            CompletesAtUtc = nowUtc.AddMinutes(minutes),
            Summary = $"Crafting {plan.Quantity:N0} {plan.Label}."
        };
    }

    public ActionResultResponse CompleteCraft(Player player, WorkshopCraft craft, DateTime nowUtc)
    {
        if (craft.CompletedAtUtc is not null)
            return new ActionResultResponse(craft.Summary, player.Turns);

        var capacity = hideout.CapacityFor(player.Hideout);
        var room = TradeGoods.Room(player, capacity, craft.Good);
        var delivered = Math.Min(craft.Quantity, room);
        var spilled = craft.Quantity - delivered;
        TradeGoods.Add(player, craft.Good, delivered);

        craft.CompletedAtUtc = nowUtc;
        craft.Delivered = delivered;
        craft.Spilled = spilled;
        var source = craft.Good is "weed" or "coke" ? "craft queue" : "workshop";
        craft.Summary = $"Finished {craft.Quantity:N0} {craft.Label.ToLowerInvariant()} from the {source}.";
        if (spilled > 0)
            craft.Summary += $" {spilled:N0} would not fit and was dumped.";

        return new ActionResultResponse(craft.Summary, player.Turns, new Dictionary<string, object?>
        {
            ["good"] = craft.Good,
            ["unitsMade"] = delivered,
            ["unitsSpilled"] = spilled,
            ["totalCost"] = craft.TotalCost,
            ["completedCraftId"] = craft.Id
        });
    }

    private WorkshopCraftPlan PlanCraft(Player player, int workUnits, string? good, bool requireTurns)
    {
        TravelGate.EnsureLanded(player);
        if (workUnits < 1 || workUnits > _options.MaxActionTurns)
            throw new GameRuleException($"Work between 1 and {_options.MaxActionTurns} turns.");
        if (requireTurns && player.Turns < workUnits)
            throw new GameRuleException($"That is {workUnits:N0} turns and you have {player.Turns:N0}.");

        var workshop = hideout.WorkshopFor(player.Hideout)
            ?? throw new GameRuleException("You need a workshop before you can make anything.");

        // A recipe first, since anything that is not one is a gun, and the guns carry their own.
        var recipe = _options.Makeables.FirstOrDefault(x =>
            string.Equals(x.Key, good, StringComparison.OrdinalIgnoreCase));

        string key;
        string label;
        long unitCost;
        int perTurn;

        if (recipe is not null)
        {
            if (!recipe.CanMake)
                throw new GameRuleException($"Nobody makes {TradeGoods.Label(recipe.Key).ToLowerInvariant()}. You buy it.");
            if (workshop.Level < recipe.MinWorkshopLevel)
                throw new GameRuleException(
                    $"A level {recipe.MinWorkshopLevel} workshop makes {TradeGoods.Label(recipe.Key).ToLowerInvariant()}. Yours is level {workshop.Level}.");

            key = recipe.Key;
            label = TradeGoods.Label(recipe.Key).ToLowerInvariant();
            unitCost = recipe.MaterialCost;
            perTurn = Math.Max(1, recipe.PerTurn);
        }
        else
        {
            (key, label, unitCost) = ForgeTarget(workshop, good);
            // Guns are the slow end of the bench: one a turn before the room speeds it up.
            perTurn = 1;
        }

        if (hideout.WorkshopRequiredTier() is { } needed && (player.Hideout?.Tier ?? 1) < needed)
            throw new GameRuleException($"Making {label} needs the {hideout.TierName(needed)} or better.");

        var capacity = hideout.CapacityFor(player.Hideout);
        var room = TradeGoods.Room(player, capacity, key);
        if (room == 0)
            throw new GameRuleException($"Your storage has no room for more {label}.");

        // What the room does is make the same bench go faster, whatever is on it.
        var rate = Math.Max(1, perTurn * Math.Max(1, workshop.Throughput));

        // Bounded by the room up front rather than made and spilled, so nobody pays for materials that
        // turn into nothing.
        var wanted = rate * workUnits;
        var made = Math.Min(wanted, room);
        var turnsUsed = (int)Math.Ceiling((double)made / rate);
        var cost = unitCost * made;
        if (player.Cash < cost)
            throw new GameRuleException($"Materials for {made:N0} {label} cost {cost:C0}.");

        return new WorkshopCraftPlan(key, label, made, wanted, unitCost, cost, turnsUsed, workshop.Level);
    }

    /// <summary>
    /// Which gun this workshop is making, and what one costs it.
    ///
    /// A shop can always make anything below what it has unlocked, so an upgrade never takes an option
    /// away - it only adds a better one. Asking for a gun the shop cannot reach is refused by name
    /// rather than quietly downgraded, because a player who asked for rifles and silently received
    /// pistols would have paid for a decision they did not make.
    /// </summary>
    private (string Good, string Label, long UnitCost) ForgeTarget(WorkshopLevelOptions workshop, string? weapon)
    {
        var buildable = _options.Weapons
            .Where(x => x.CanForge && x.MinWorkshopLevel <= workshop.Level)
            .OrderByDescending(x => x.Price)
            .ToList();
        if (buildable.Count == 0)
            throw new GameRuleException("Your workshop cannot make any weapon yet.");

        // Null means "the best you can", which is what a caller written before the shop made more than
        // one kind of gun was always asking for.
        if (string.IsNullOrWhiteSpace(weapon))
        {
            var best = buildable[0];
            return (best.Key, WeaponTiers.Label(best.Key).ToLowerInvariant(), best.ForgeCost);
        }

        var key = weapon.Trim().ToLowerInvariant();
        if (!WeaponTiers.IsWeapon(key))
            throw new GameRuleException($"A workshop makes {string.Join(", ", buildable.Select(x => WeaponTiers.Label(x.Key).ToLowerInvariant()))}.");

        var wanted = _options.WeaponTier(key);
        if (wanted is null || !wanted.CanForge)
            throw new GameRuleException($"Nobody makes {WeaponTiers.Label(key).ToLowerInvariant()} in a back room. You buy those.");
        if (wanted.MinWorkshopLevel > workshop.Level)
            throw new GameRuleException($"{WeaponTiers.Label(key)} need a level {wanted.MinWorkshopLevel} workshop. Yours is level {workshop.Level}.");

        return (wanted.Key, WeaponTiers.Label(wanted.Key).ToLowerInvariant(), wanted.ForgeCost);
    }

    private sealed record WorkshopCraftPlan(
        string Good,
        string Label,
        int Quantity,
        int Wanted,
        long UnitCost,
        long TotalCost,
        int WorkUnits,
        int WorkshopLevel);

    public ActionResultResponse SellProduct(Player player, string? product, int quantity)
    {
        TravelGate.EnsureLanded(player);
        if (quantity is < 1 or > 100_000)
            throw new GameRuleException("Move between 1 and 100,000 at a time.");

        var key = NormalizeProduct(product);
        var stockBefore = StockLevels.From(player);
        var listPrice = ProductSellPrice(player.City, key);
        // Coke is priced on what it actually is. Stretching gains units and loses strength, and the
        // buyer is paying for the strength: without this the mix house is simply a cheaper coke lab.
        var purity = key == "coke" ? player.CokePurity : 1;
        var price = key == "coke"
            ? Math.Max(1, (long)Math.Round(listPrice * _options.PurityMultiplier(purity)))
            : listPrice;
        if (key == "weed")
        {
            if (player.Weed < quantity) throw new GameRuleException($"You only hold {player.Weed:N0} weed.");
            player.Weed -= quantity;
        }
        else
        {
            if (player.Coke < quantity) throw new GameRuleException($"You only hold {player.Coke:N0} coke.");
            // Selling a share of a mixture leaves the mixture as it was.
            player.Coke -= quantity;
        }

        var total = (long)quantity * price;
        player.Cash += total;
        var overflow = hideout.Settle(player, stockBefore);
        var weakened = key == "coke" && price < listPrice
            ? $" It is {purity:P0} pure, so it fetched {price:C0} against {listPrice:C0} for clean."
            : string.Empty;
        return new ActionResultResponse(
            $"Sold {quantity:N0} {key} for ${total:N0} cash.{weakened}{overflow.Describe()}",
            player.Turns,
            new Dictionary<string, object?>
            {
                ["product"] = key,
                ["quantity"] = quantity,
                ["unitPrice"] = price,
                ["total"] = total,
                ["cashBankedByOverflow"] = overflow.CashBanked
            });
    }

    public ActionResultResponse Travel(Player player, string? city)
    {
        TravelGate.EnsureLanded(player);
        var destination = _options.CityMarkets.ResolveCity(city)
            ?? throw new GameRuleException($"Pick one of: {string.Join(", ", _options.CityMarkets.Profiles.Select(x => x.City))}.");
        if (string.Equals(player.City, destination, StringComparison.OrdinalIgnoreCase))
            throw new GameRuleException($"You are already in {destination}.");

        var turns = _options.CityMarkets.TravelTurns(destination);
        if (player.Turns < turns)
            throw new GameRuleException($"Travel to {destination} takes {turns:N0} turns.");

        var from = player.City;
        var profile = _options.CityMarkets.ProfileFor(destination);
        player.Turns -= turns;
        player.City = destination;

        // The flight is the distance in time as well as in turns. Committed on departure rather than
        // on arrival, so the town you are standing in is the one you paid to reach, and the clock is
        // what says whether you are there yet.
        var nowUtc = DateTime.UtcNow;
        var flightMinutes = Math.Max(1, turns * Math.Max(1, _options.Mules.MinutesPerTravelTurn));
        player.TravelArrivesAtUtc = nowUtc.AddMinutes(flightMinutes);

        // The trip is already paid for in turns, so a bad roll lightens the load rather than turning
        // the player back. Losing the turns and the ground would be two punishments for one roll.
        var seizure = RollTravelSeizure(player, destination);
        var prices = $"Weed is {profile.Weed.ToLowerInvariant()}, coke is {profile.Coke.ToLowerInvariant()}.";
        var landing = $"You land in {flightMinutes} minute(s).";
        var summary = seizure.Busted
            ? $"Left {from} for {destination}, but got stopped on the way in. {SeizureSummary(seizure)} {prices} {landing}"
            : $"Left {from} for {destination} clean. {prices} {landing}";

        return new ActionResultResponse(
            summary,
            player.Turns,
            new Dictionary<string, object?>
            {
                ["from"] = from,
                ["to"] = destination,
                ["turnsSpent"] = turns,
                ["flightMinutes"] = flightMinutes,
                ["arrivesAtUtc"] = player.TravelArrivesAtUtc,
                ["risk"] = profile.Risk,
                ["bustChancePercent"] = _options.CityMarkets.BustChancePercent(destination),
                ["busted"] = seizure.Busted,
                ["cashSeized"] = seizure.Cash,
                ["weedSeized"] = seizure.Weed,
                ["cokeSeized"] = seizure.Coke,
                ["weedSellPrice"] = ProductSellPrice(destination, "weed"),
                ["cokeSellPrice"] = ProductSellPrice(destination, "coke")
            });
    }

    /// <summary>
    /// What a player has on them, valued the way net worth values it. Cash in the bank is deliberately
    /// absent: it is the one place a load is safe, and that is what makes banking before a run a move.
    /// </summary>
    public long CarriedValue(Player player)
        => player.Cash
           + (long)player.Weed * _options.WeedNetWorth
           + (long)player.Coke * _options.CokeNetWorth;

    /// <summary>
    /// Rolled once per trip rather than per turn: a run is one event, and per-turn rolls would make
    /// the far towns punishing for their distance instead of their danger.
    /// </summary>
    private TravelSeizure RollTravelSeizure(Player player, string destination)
    {
        var markets = _options.CityMarkets;
        if (CarriedValue(player) < markets.MinimumCarriedValueToBust) return TravelSeizure.None;
        if (!RollChance(markets.BustChance(destination))) return TravelSeizure.None;

        var share = Math.Clamp(
            markets.SeizureMinPercent + random.NextDouble() * (markets.SeizureMaxPercent - markets.SeizureMinPercent),
            0,
            1);
        var seizure = new TravelSeizure(
            true,
            SeizeCash(player.Cash, share),
            SeizeUnits(player.Weed, share),
            SeizeUnits(player.Coke, share));

        player.Cash -= seizure.Cash;
        player.Weed -= seizure.Weed;
        player.Coke -= seizure.Coke;
        return seizure;
    }

    private static long SeizeCash(long held, double share)
        => held <= 0 ? 0 : (long)Math.Floor(held * share);

    /// <summary>Keeps at least one unit when there was anything to take, as combat loot does.</summary>
    private static int SeizeUnits(int held, double share)
        => held <= 0 ? 0 : Math.Max(1, (int)Math.Floor(held * share));

    private static string SeizureSummary(TravelSeizure seizure)
    {
        var lost = new List<string>();
        if (seizure.Cash > 0) lost.Add($"${seizure.Cash:N0}");
        if (seizure.Weed > 0) lost.Add($"{seizure.Weed:N0} weed");
        if (seizure.Coke > 0) lost.Add($"{seizure.Coke:N0} coke");

        return lost.Count switch
        {
            0 => "Nothing on you was worth taking.",
            1 => $"They took {lost[0]}.",
            2 => $"They took {lost[0]} and {lost[1]}.",
            _ => $"They took {string.Join(", ", lost.Take(lost.Count - 1))} and {lost[^1]}."
        };
    }

    private readonly record struct TravelSeizure(bool Busted, long Cash, int Weed, int Coke)
    {
        public static readonly TravelSeizure None = new(false, 0, 0, 0);
    }

    public int ProductSellPrice(string? city, string product)
        => product.Trim().ToLowerInvariant() switch
        {
            "weed" => _options.CityMarkets.ProductPrice(city, "weed", _options.WeedSellPrice),
            "coke" => _options.CityMarkets.ProductPrice(city, "coke", _options.CokeSellPrice),
            _ => throw new GameRuleException("Weed or coke.")
        };

    public ActionResultResponse BuyStoreItem(Player player, string? itemKey, int quantity)
    {
        TravelGate.EnsureLanded(player);
        if (quantity is < 1 or > 10_000)
            throw new GameRuleException("Move between 1 and 10,000 at a time.");

        var item = GetStore().SingleOrDefault(x => string.Equals(x.Key, itemKey?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (item is null)
            throw new GameRuleException("Unknown store item.");

        var total = (long)item.Price * quantity;
        if (player.Cash < total)
            throw new GameRuleException($"That comes to {total:C0} and you are carrying {player.Cash:C0}.");

        // Purchases are refused rather than clamped: losing goods you paid for is worse than a refusal.
        var capacity = hideout.CapacityFor(player.Hideout);
        // Rides are the one purchase held by the building rather than the storage room, so the refusal
        // has to name the right thing to upgrade: telling someone to buy a bigger shelf for a car would
        // send them to spend money that cannot help.
        var (held, cap, roomName) = item.Key switch
        {
            "condoms" => (player.Condoms, capacity.MaxCondoms, "storage room"),
            "beer" => (player.Beer, capacity.MaxBeer, "storage room"),
            "medicine" => (player.Medicine, capacity.MaxMedicine, "storage room"),
            "poison" => (player.Poison, capacity.MaxPoison, "storage room"),
            "rides" => (player.Rides, capacity.MaxRides, "garage"),
            // Guns share one shelf whatever kind they are, so what is already on it is the whole
            // rack. Counting only the tier being bought would let a player fill the room four times.
            _ when WeaponTiers.IsWeapon(item.Key) => (player.Weapons, capacity.MaxWeapons, "storage room"),
            _ => throw new GameRuleException($"The counter does not stock {item.Name.ToLowerInvariant()}.")
        };
        var room = Math.Max(0, cap - held);
        if (quantity > room)
            throw new GameRuleException(room == 0
                ? $"Your {roomName} is full at {cap:N0} {item.Name.ToLowerInvariant()}. {(roomName == "garage" ? "A bigger hideout parks more." : "Upgrade it to hold more.")}"
                : $"Your {roomName} only has space for {room:N0} more {item.Name.ToLowerInvariant()}.");

        player.Cash -= total;
        switch (item.Key)
        {
            case "condoms": player.Condoms += quantity; break;
            case "beer": player.Beer += quantity; break;
            case "medicine": player.Medicine += quantity; break;
            case "poison": player.Poison += quantity; break;
            case "rides": player.Rides += quantity; break;
            default:
                // Every remaining store key should be a gun. Checked rather than assumed: this arm
                // used to take anything it did not recognise and put it on the weapon rack, so a new
                // good added to the shop and forgotten here was bought, paid for, and quietly filed
                // as a gun. A refusal is a bug report; a silent wrong shelf is a mystery.
                if (!WeaponTiers.IsWeapon(item.Key))
                    throw new GameRuleException($"The counter cannot hand over {item.Name.ToLowerInvariant()}.");
                player.AddWeapons(item.Key, quantity);
                break;
        }

        return new ActionResultResponse(
            $"Bought {quantity:N0} {item.Name.ToLowerInvariant()} for ${total:N0}.",
            player.Turns,
            new Dictionary<string, object?>
            {
                ["itemKey"] = item.Key,
                ["quantity"] = quantity,
                ["unitPrice"] = item.Price,
                ["total"] = total
            });
    }

    /// <summary>
    /// Sells rides back to the chop shop.
    ///
    /// Only rides, because they are the only store item worth anything second hand: supplies are
    /// consumed and weapons already have a player market that pays better than any shop would. The
    /// buy-back exists so a garage is not a one-way purchase - a player who has decided they are done
    /// with drive-bys, or who needs the cash tonight, can get most of it back.
    /// </summary>
    public ActionResultResponse SellRides(Player player, int quantity)
    {
        TravelGate.EnsureLanded(player);
        if (quantity is < 1 or > 10_000)
            throw new GameRuleException("Move between 1 and 10,000 at a time.");
        if (player.Rides < quantity)
            throw new GameRuleException(player.Rides == 0
                ? "You do not own a ride."
                : $"You only own {player.Rides:N0} ride(s).");

        var unitPrice = Math.Max(0, _options.RideSalePrice);
        var total = (long)unitPrice * quantity;
        player.Rides -= quantity;
        player.Cash += total;
        // The safe still binds what can sit at the house, and a fleet is worth more than an early safe
        // holds, so a big sale would otherwise vanish at the next settle.
        var overflow = hideout.Settle(player, StockLevels.From(player) with { Cash = player.Cash - total });

        return new ActionResultResponse(
            $"Sold {quantity:N0} ride(s) to the chop shop for ${total:N0}.{overflow.Describe()}",
            player.Turns,
            new Dictionary<string, object?>
            {
                ["itemKey"] = "rides",
                ["quantity"] = quantity,
                ["unitPrice"] = unitPrice,
                ["total"] = total,
                ["ridesRemaining"] = player.Rides,
                ["cashBankedByOverflow"] = overflow.CashBanked
            });
    }

    public ActionResultResponse Deposit(Player player, long amount, DateTime nowUtc)
    {
        TravelGate.EnsureLanded(player);
        ValidateMoneyAmount(amount);
        if (player.Cash < amount) throw new GameRuleException($"You are carrying {player.Cash:C0}.");

        // Charged after the money has been checked and before any of it moves, so a refused amount
        // costs nothing and a charged trip always did something.
        var trip = ChargeBankTrip(player, nowUtc);
        player.Cash -= amount;
        player.BankCash += amount;
        return new ActionResultResponse(
            TripSummary($"Deposited ${amount:N0} into the bank.", trip),
            player.Turns,
            new Dictionary<string, object?>
            {
                ["amount"] = amount,
                ["direction"] = "deposit",
                ["turnsSpent"] = trip,
                ["freeTrip"] = trip == 0
            });
    }

    public ActionResultResponse Withdraw(Player player, long amount, DateTime nowUtc)
    {
        TravelGate.EnsureLanded(player);
        ValidateMoneyAmount(amount);
        if (player.BankCash < amount) throw new GameRuleException($"The bank is holding {player.BankCash:C0}.");

        // Refused rather than clamped, since clamping would bounce the cash straight back to the bank.
        var safeCap = hideout.CapacityFor(player.Hideout).MaxCash;
        var room = Math.Max(0, safeCap - player.Cash);
        if (amount > room)
            throw new GameRuleException(room == 0
                ? $"Your safe is full at ${safeCap:N0} cash on hand. Upgrade it to hold more."
                : $"Your safe only has room for ${room:N0} more cash on hand.");

        var trip = ChargeBankTrip(player, nowUtc);
        player.BankCash -= amount;
        player.Cash += amount;
        return new ActionResultResponse(
            TripSummary($"Withdrew ${amount:N0} from the bank.", trip),
            player.Turns,
            new Dictionary<string, object?>
            {
                ["amount"] = amount,
                ["direction"] = "withdraw",
                ["turnsSpent"] = trip,
                ["freeTrip"] = trip == 0
            });
    }

    public ActionResultResponse HireCrew(Player player, string? role, int quantity)
    {
        TravelGate.EnsureLanded(player);
        var crew = _options.Crew;
        var normalizedRole = NormalizeCrewRole(role);
        ValidateCrewQuantity(quantity, crew);

        var unitCost = normalizedRole switch
        {
            "pimps" => crew.HirePimpCost,
            "hoes" => crew.HireHoeCost,
            "thugs" => crew.HireThugCost,
            _ => throw new GameRuleException("Pimps, hoes or thugs.")
        };

        if (normalizedRole == "hoes" && player.HoeHappiness < crew.MinHoeMoraleToHire)
            throw new GameRuleException($"The house is at {player.HoeHappiness:N0}% and nobody new signs on under {crew.MinHoeMoraleToHire:N0}%.");
        if (normalizedRole == "thugs" && player.ThugHappiness < crew.MinThugMoraleToHire)
            throw new GameRuleException($"Your thugs are at {player.ThugHappiness:N0}% and nobody new signs on under {crew.MinThugMoraleToHire:N0}%.");

        var room = hideout.CrewRoom(player, normalizedRole);
        if (quantity > room)
        {
            var capacity = hideout.CapacityFor(player.Hideout);
            var cap = normalizedRole switch
            {
                "pimps" => capacity.MaxPimps,
                "hoes" => capacity.MaxHoes,
                _ => capacity.MaxThugs
            };
            // Name whichever of the two is actually the limit. Blaming the building for a ceiling the
            // storage room is setting sends a player off to buy a bigger house, which will not move
            // the number by one.
            var store = hideout.StoreCapsCrew(player.Hideout, normalizedRole);
            var holder = store ? "storage room" : $"{capacity.TierName}";
            var verb = store ? "supplies" : "holds";
            throw new GameRuleException(room == 0
                ? $"Your {holder} {verb} {cap:N0} {normalizedRole} and is full."
                  + (store ? " Deepen the store to run more." : string.Empty)
                : $"Your {holder} only has room for {room:N0} more {CrewLabel(normalizedRole, room)}.");
        }

        var totalCost = (long)unitCost * quantity;
        if (player.Cash < totalCost)
            throw new GameRuleException($"You need ${totalCost:N0} cash on hand to hire that crew.");

        player.Cash -= totalCost;
        var hiredNames = string.Empty;
        switch (normalizedRole)
        {
            case "pimps":
                var hired = pimps.Hire(player, quantity, DateTime.UtcNow);
                hiredNames = string.Join(", ", hired.Select(x => x.Name));
                break;
            case "hoes": player.Hoes += quantity; break;
            case "thugs": player.Thugs += quantity; break;
        }

        return new ActionResultResponse(
            normalizedRole == "pimps"
                ? $"Hired {hiredNames} for ${totalCost:N0}."
                : $"Hired {quantity:N0} {CrewLabel(normalizedRole, quantity)} for ${totalCost:N0}.",
            player.Turns,
            new Dictionary<string, object?>
            {
                ["role"] = normalizedRole,
                ["quantity"] = quantity,
                ["unitCost"] = unitCost,
                ["totalCost"] = totalCost,
                ["cashRemaining"] = player.Cash
            });
    }

    public ActionResultResponse FireCrew(Player player, string? role, int quantity)
    {
        TravelGate.EnsureLanded(player);
        var crew = _options.Crew;
        var normalizedRole = NormalizeCrewRole(role);
        ValidateCrewQuantity(quantity, crew);
        var firedNames = string.Empty;

        switch (normalizedRole)
        {
            case "pimps":
                if (player.Pimps - quantity < 1)
                    throw new GameRuleException("You must keep at least one pimp managing the operation.");
                firedNames = string.Join(", ", pimps.Release(player, quantity, "Fired", DateTime.UtcNow).Select(x => x.Name));
                player.HoeHappiness = ClampHappiness(player.HoeHappiness - FirePenalty(quantity, crew.FirePimpHoeMoralePenalty, crew));
                break;
            case "hoes":
                if (player.Hoes < quantity)
                    throw new GameRuleException($"You have {player.Hoes:N0} hoes on the books.");
                player.Hoes -= quantity;
                player.HoeHappiness = ClampHappiness(player.HoeHappiness - FirePenalty(quantity, crew.FireHoeMoralePenalty, crew));
                break;
            case "thugs":
                if (player.Thugs < quantity)
                    throw new GameRuleException($"You have {player.Thugs:N0} thugs on the books.");
                player.Thugs -= quantity;
                player.ThugHappiness = ClampHappiness(player.ThugHappiness - FirePenalty(quantity, crew.FireThugMoralePenalty, crew));
                break;
        }

        return new ActionResultResponse(
            normalizedRole == "pimps"
                ? $"Let {firedNames} go."
                : $"Fired {quantity:N0} {CrewLabel(normalizedRole, quantity)}.",
            player.Turns,
            new Dictionary<string, object?>
            {
                ["role"] = normalizedRole,
                ["quantity"] = quantity,
                ["pimps"] = player.Pimps,
                ["hoes"] = player.Hoes,
                ["thugs"] = player.Thugs,
                ["hoeHappiness"] = Math.Round(player.HoeHappiness, 2),
                ["thugHappiness"] = Math.Round(player.ThugHappiness, 2)
            });
    }

    public ActionResultResponse UpdateCrewSettings(Player player, int hoeCutPercent)
    {
        if (hoeCutPercent is < 10 or > 80)
            throw new GameRuleException("The house cut runs from 10% to 80%.");
        player.HoeCutPercent = hoeCutPercent;
        return new ActionResultResponse(
            $"Set the hoe payout to {hoeCutPercent}% of street gross.",
            player.Turns,
            new Dictionary<string, object?> { ["hoeCutPercent"] = hoeCutPercent });
    }

    public ActionResultResponse RecoverCrewMorale(Player player, string? strategy)
    {
        TravelGate.EnsureLanded(player);
        var morale = _options.Morale;
        var key = strategy?.Trim().ToLowerInvariant() ?? "rest";
        var hoeBefore = player.HoeHappiness;
        var thugBefore = player.ThugHappiness;
        var totalCrew = TotalCrew(player);
        // Named from the tier rather than hardcoded, so a player who moved up is not still being told
        // about a Trap House they left behind.
        var hideoutName = hideout.TierName(player.Hideout?.Tier ?? 1);

        if (key == "rest")
        {
            ValidateTurns(player, morale.HqRestTurnCost, _options.MaxActionTurns, "HQ rest");
            var cashCost = HqCashCost(totalCrew, morale.HqRestCashPerCrew);
            if (player.Cash < cashCost)
                throw new GameRuleException($"You need ${cashCost:N0} cash on hand to rest the crew.");

            player.Turns -= morale.HqRestTurnCost;
            player.Cash -= cashCost;
            player.HoeHappiness = ClampHappiness(player.HoeHappiness + morale.HqRestMoraleGain);
            player.ThugHappiness = ClampHappiness(player.ThugHappiness + morale.HqRestMoraleGain);
            pimps.Recover(player, pimps.RestRecovery);

            return new ActionResultResponse(
                $"Opened the {hideoutName} for crew downtime. Morale rose by up to {morale.HqRestMoraleGain:N0}%.",
                player.Turns,
                MoraleBreakdown(key, morale.HqRestTurnCost, cashCost, 0, 0, hoeBefore, thugBefore, player));
        }

        if (key == "party")
        {
            ValidateTurns(player, morale.HqPartyTurnCost, _options.MaxActionTurns, "HQ party");
            var cashCost = HqCashCost(totalCrew, morale.HqPartyCashPerCrew);
            var beerCost = RequiredRecoverySupply(player.Thugs, morale.HqPartyBeerPerThug);
            var weedCost = RequiredRecoverySupply(player.Hoes, morale.HqPartyWeedPerHoes);
            if (player.Cash < cashCost)
                throw new GameRuleException($"You need ${cashCost:N0} cash on hand to throw a {hideoutName} party.");
            if (player.Beer < beerCost)
                throw new GameRuleException($"You need {beerCost:N0} beer for the thugs.");
            if (player.Weed < weedCost)
                throw new GameRuleException($"You need {weedCost:N0} weed for the crew.");

            player.Turns -= morale.HqPartyTurnCost;
            player.Cash -= cashCost;
            player.Beer -= beerCost;
            player.Weed -= weedCost;
            player.HoeHappiness = ClampHappiness(player.HoeHappiness + morale.HqPartyHoeMoraleGain);
            player.ThugHappiness = ClampHappiness(player.ThugHappiness + morale.HqPartyThugMoraleGain);
            pimps.Recover(player, pimps.PartyRecovery);

            return new ActionResultResponse(
                $"Threw a {hideoutName} party and let the crew burn off pressure. Hoe morale rose by up to {morale.HqPartyHoeMoraleGain:N0}%; thug morale rose by up to {morale.HqPartyThugMoraleGain:N0}%.",
                player.Turns,
                MoraleBreakdown(key, morale.HqPartyTurnCost, cashCost, beerCost, weedCost, hoeBefore, thugBefore, player));
        }

        throw new GameRuleException("Rest them or throw them a party.");
    }

    /// <summary>
    /// Tops up the consumables this action will burn, spending cash on hand only. Buys as much of the
    /// shortfall as the storage room and the wallet allow rather than refusing outright, so a thin
    /// wallet still gets a partial restock and the action still runs. Weapons are left alone: they are
    /// permanent coverage, not upkeep, and quietly spending hundreds on them would be a nasty surprise.
    /// Condoms come first because hoes drive the gross.
    /// </summary>
    private Restock RestockForAction(Player player, int turns)
    {
        var morale = _options.Morale;
        var capacity = hideout.CapacityFor(player.Hideout);
        var condomsNeeded = RequiredUpkeep(player.Hoes, turns, morale.TurnsPerCondom);
        var beerNeeded = RequiredUpkeep(player.Thugs, turns, morale.TurnsPerBeer);

        var condoms = BuyUpTo(player, condomsNeeded - player.Condoms, capacity.MaxCondoms - player.Condoms, _options.CondomPrice);
        player.Condoms += condoms.Quantity;
        var beer = BuyUpTo(player, beerNeeded - player.Beer, capacity.MaxBeer - player.Beer, _options.BeerPrice);
        player.Beer += beer.Quantity;

        return new Restock(condoms.Quantity, beer.Quantity, condoms.Cost + beer.Cost);
    }

    private static (int Quantity, long Cost) BuyUpTo(Player player, int shortfall, int room, int unitPrice)
    {
        if (shortfall <= 0 || room <= 0 || unitPrice <= 0)
            return (0, 0);

        var affordable = (int)Math.Min(int.MaxValue, player.Cash / unitPrice);
        var quantity = Math.Min(shortfall, Math.Min(room, affordable));
        if (quantity <= 0)
            return (0, 0);

        var cost = (long)quantity * unitPrice;
        player.Cash -= cost;
        return (quantity, cost);
    }

    private ProductProductionOptions GetProduction(string product)
        => product switch
        {
            "weed" => _options.Production.Weed,
            "coke" => _options.Production.Coke,
            _ => throw new GameRuleException("Weed or coke.")
        };

    private static void ValidateTurns(Player player, int turns, int max, string action)
    {
        if (turns is < 1 || turns > max)
            throw new GameRuleException($"{action} runs from 1 to {max} turns.");
        if (player.Turns < turns)
            throw new GameRuleException($"That is {turns:N0} turns and you have {player.Turns:N0}.");
    }

    /// <summary>
    /// Charges a trip to the bank, unless the player is still at the counter from the last one, and
    /// returns what it cost so the caller can log and report it.
    ///
    /// Only a paid trip writes the stamp. Refreshing it on the free moves inside the window would let
    /// anyone willing to move money every few minutes hold one payment open forever, which is the
    /// difference between a grace window and no charge at all.
    /// </summary>
    private int ChargeBankTrip(Player player, DateTime nowUtc)
    {
        var bank = _options.Bank;
        var cost = Math.Max(0, bank.TripTurnCost);
        if (cost == 0) return 0;
        if (player.LastBankedAtUtc is { } last
            && last.AddMinutes(Math.Max(0, bank.TripGraceMinutes)) > nowUtc)
            return 0;

        ValidateTurns(player, cost, _options.MaxActionTurns, "A trip to the bank");
        player.Turns -= cost;
        player.LastBankedAtUtc = nowUtc;
        return cost;
    }

    /// <summary>
    /// Names a free trip and says nothing about a charged one. The turn counter already reports what
    /// a charge cost, but nothing would tell a player the window exists, and a rule nobody can see is
    /// a rule that reads as the game losing count of their turns.
    /// </summary>
    private static string TripSummary(string summary, int trip)
        => trip == 0 ? $"{summary} You were still at the bank, so the trip was free." : summary;

    private static void ValidateMoneyAmount(long amount)
    {
        if (amount is < 1 or > 1_000_000_000_000)
            throw new GameRuleException("Move between $1 and $1,000,000,000,000.");
    }

    private static string NormalizeProduct(string? product)
    {
        var key = product?.Trim().ToLowerInvariant();
        if (key is "weed" or "coke") return key;
        throw new GameRuleException("Weed or coke.");
    }

    private static string NormalizeCrewRole(string? role)
    {
        var key = role?.Trim().ToLowerInvariant();
        return key switch
        {
            "pimp" or "pimps" => "pimps",
            "hoe" or "hoes" => "hoes",
            "thug" or "thugs" => "thugs",
            _ => throw new GameRuleException("Pimps, hoes or thugs.")
        };
    }

    private static void ValidateCrewQuantity(int quantity, CrewOptions crew)
    {
        if (quantity < 1 || quantity > crew.MaxCrewTransactionQuantity)
            throw new GameRuleException($"Take on between 1 and {crew.MaxCrewTransactionQuantity:N0} at a time.");
    }

    private static double FirePenalty(int quantity, double penaltyPerCrew, CrewOptions crew)
        => Math.Min(crew.MaxFireMoralePenalty, quantity * penaltyPerCrew);

    private static string CrewLabel(string role, int quantity)
        => quantity == 1 ? role.TrimEnd('s') : role;

    private int Roll(RangeOptions range) => random.NextInclusive(range.Min, range.Max);

    private bool RollChance(double chance) => random.NextDouble() < Math.Clamp(chance, 0, 1);

    /// <param name="scale">
    /// What the district does to the odds of finding anything. Applied to the chance rather than to
    /// the amount, so a rich district turns things up more often rather than turning up impossible
    /// quantities of them at the same rate.
    /// </param>
    private int RollFind(FindOptions find, double scale = 1)
        => RollChance(find.Chance * Math.Max(0, scale)) ? random.NextInclusive(find.Min, find.Max) : 0;

    private int RollDeserters(int crewCount, double happiness, MoraleOptions morale)
    {
        if (crewCount <= 0 || happiness >= morale.DesertionThreshold) return 0;

        var chance = Math.Min(morale.MaxDesertionChance, (morale.DesertionThreshold - happiness) / 100.0);
        var deserters = 0;
        for (var i = 0; i < crewCount; i++)
            if (RollChance(chance)) deserters++;
        return deserters;
    }

    /// <summary>
    /// What running short on upkeep costs, as a share of the upkeep that was missed rather than a flat
    /// charge per missing unit.
    ///
    /// Charged per unit, the penalty grew with the crew while the morale a shift earns did not, so the
    /// two came apart as a player got bigger. A crew of 59 needs 98 condoms for a full shift and a
    /// level 3 storage room holds 84, which is a 14 unit shortfall: at 2.25 each that was -31.5 morale
    /// against the +2.8 the same shift earned, so being 14% under-supplied cost eleven shifts of
    /// progress and the crew walked out inside four actions. Proportional, the same 14% costs -6.4 and
    /// reads as a slide the player can correct, while going out with nothing still craters morale.
    ///
    /// The coefficient is now "morale lost per turn when wholly unsupplied", so a full 20 turn shift
    /// with no condoms at all costs 45. It behaves the same at any crew size, which is the point.
    /// </summary>
    private static double ShortagePenalty(int shortage, int needed, int turns, double penaltyPerTurn)
        => shortage <= 0 || needed <= 0 ? 0 : penaltyPerTurn * turns * ((double)shortage / needed);

    /// <summary>How much crew a given stock of upkeep carries through an action of this length.</summary>
    /// <summary>
    /// The longest shift this much supply covers this much crew for. A crew with nobody in it is not
    /// limited by supply at all, so it gets the full length rather than nothing.
    /// </summary>
    private static int SuppliedTurns(int supplyHeld, int crewCount, double turnsPerSupply, int maxTurns)
        => crewCount <= 0 || turnsPerSupply <= 0
            ? maxTurns
            : (int)Math.Floor(supplyHeld * turnsPerSupply / crewCount);

    private static int SupportableCrew(int supplyHeld, int turns, double turnsPerSupply)
        => turns <= 0 ? 0 : (int)Math.Floor(supplyHeld * turnsPerSupply / turns);

    private static int RequiredUpkeep(int crewCount, int turns, double turnsPerSupply)
    {
        if (turnsPerSupply <= 0) return 0;
        return Math.Max(0, (int)Math.Ceiling(crewCount * turns / turnsPerSupply));
    }

    private static double ClampHappiness(double value) => Math.Clamp(value, 0, 100);

    private static int TotalCrew(Player player)
        => Math.Max(1, player.Pimps + player.Hoes + player.Thugs);

    private static long HqCashCost(int totalCrew, long cashPerCrew)
        => Math.Max(0, totalCrew * Math.Max(0, cashPerCrew));

    private static int RequiredRecoverySupply(int crewCount, int crewPerSupply)
        => crewCount <= 0 || crewPerSupply <= 0 ? 0 : (int)Math.Ceiling(crewCount / (double)crewPerSupply);

    private static Dictionary<string, object?> MoraleBreakdown(
        string strategy,
        int turnsSpent,
        long cashCost,
        int beerCost,
        int weedCost,
        double hoeBefore,
        double thugBefore,
        Player player)
        => new()
        {
            ["strategy"] = strategy,
            ["turnsSpent"] = turnsSpent,
            ["cashCost"] = cashCost,
            ["beerCost"] = beerCost,
            ["weedCost"] = weedCost,
            ["hoeHappinessBefore"] = Math.Round(hoeBefore, 2),
            ["hoeHappinessAfter"] = Math.Round(player.HoeHappiness, 2),
            ["hoeHappinessDelta"] = Math.Round(player.HoeHappiness - hoeBefore, 2),
            ["thugHappinessBefore"] = Math.Round(thugBefore, 2),
            ["thugHappinessAfter"] = Math.Round(player.ThugHappiness, 2),
            ["thugHappinessDelta"] = Math.Round(player.ThugHappiness - thugBefore, 2)
        };

    private static string Plural(int value) => value == 1 ? string.Empty : "s";
}

public sealed class GameRuleException(string message) : Exception(message);

/// <summary>
/// The one rule about being on a plane: you cannot do anything from it.
///
/// Checked in the services rather than at each endpoint, because there are two dozen ways to act and
/// only one set of places where acting actually happens. A guard the endpoints have to remember is a
/// guard that will eventually be forgotten.
/// </summary>
public static class TravelGate
{
    public static void EnsureLanded(Player player)
    {
        var nowUtc = DateTime.UtcNow;
        if (!player.IsInTransit(nowUtc)) return;
        var minutes = Math.Max(1, (int)Math.Ceiling((player.TravelArrivesAtUtc!.Value - nowUtc).TotalMinutes));
        throw new GameRuleException($"You are in the air, landing in {minutes} minute(s). Nothing to do but wait.");
    }
}

/// <summary>What an auto-buy topped up before an action ran.</summary>
public sealed record Restock(int Condoms, int Beer, long Cost)
{
    public static readonly Restock None = new(0, 0, 0);

    public bool Any => Condoms > 0 || Beer > 0;

    public string Describe()
    {
        var parts = new List<string>();
        if (Condoms > 0) parts.Add($"{Condoms:N0} condoms");
        if (Beer > 0) parts.Add($"{Beer:N0} beer");
        return string.Join(" and ", parts);
    }
}

/// <summary>A player's position in the net worth order, without the rest of the player row.</summary>
public sealed record PlayerStanding(long NetWorth, DateTime CreatedAtUtc);

/// <summary>One player's crew and what they are worth, for totalling a board of crews.</summary>
public sealed record AllianceStanding(long? AllianceId, long NetWorth);
