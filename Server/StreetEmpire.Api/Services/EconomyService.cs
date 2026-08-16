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
    public Expression<Func<Player, long>> NetWorthExpression { get; } = BuildNetWorthExpression(options.Value);

    private static Expression<Func<Player, long>> BuildNetWorthExpression(GameOptions options)
        => player => player.Cash
                     + player.BankCash
                     + (long)player.Pimps * options.PimpNetWorth
                     + (long)player.Hoes * options.HoeNetWorth
                     + (long)player.Thugs * options.ThugNetWorth
                     + (long)player.Condoms * options.CondomPrice
                     + (long)player.Beer * options.BeerPrice
                     + (long)player.Weapons * options.WeaponPrice
                     + (long)player.Weed * options.WeedNetWorth
                     + (long)player.Coke * options.CokeNetWorth;

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

    public IReadOnlyList<StoreItemResponse> GetStore() =>
    [
        new("condoms", "Condoms", "Crew Supplies", _options.CondomPrice, "Consumed while your hoes work the streets."),
        new("beer", "Beer", "Crew Supplies", _options.BeerPrice, "Consumed by thugs during street operations."),
        new("weapons", "Weapons", "Security", _options.WeaponPrice, "Permanent security equipment. One weapon covers one thug.")
    ];

    public long CalculateNetWorth(Player player)
        => player.Cash
           + player.BankCash
           + (long)player.Pimps * _options.PimpNetWorth
           + (long)player.Hoes * _options.HoeNetWorth
           + (long)player.Thugs * _options.ThugNetWorth
           + (long)player.Condoms * _options.CondomPrice
           + (long)player.Beer * _options.BeerPrice
           + (long)player.Weapons * _options.WeaponPrice
           + (long)player.Weed * _options.WeedNetWorth
           + (long)player.Coke * _options.CokeNetWorth;

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
    public ActionResultResponse Scout(Player player, int turns, bool autoBuySupplies = false, TerritoryEffects? territory = null, IReadOnlyCollection<long>? awayPimpIds = null)
    {
        TravelGate.EnsureLanded(player);
        ValidateTurns(player, turns, _options.MaxActionTurns, "Work the streets");

        var street = _options.StreetAction;
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

            if (RollChance(street.PimpRecruitChance)) recruitedPimps++;
            if (RollChance(street.HoeRecruitChance)) recruitedHoes++;
            if (RollChance(street.ThugRecruitChance)) recruitedThugs++;

            condomsFound += RollFind(street.Finds.Condoms);
            beerFound += RollFind(street.Finds.Beer);
            weedFound += RollFind(street.Finds.Weed);
            cokeFound += RollFind(street.Finds.Coke);
        }

        // The hideout only has so many beds, so recruits beyond it walk away.
        var recruitsOffered = recruitedPimps + recruitedHoes + recruitedThugs;
        recruitedPimps = Math.Min(recruitedPimps, hideout.CrewRoom(player, "pimps"));
        recruitedHoes = Math.Min(recruitedHoes, hideout.CrewRoom(player, "hoes"));
        recruitedThugs = Math.Min(recruitedThugs, hideout.CrewRoom(player, "thugs"));
        var recruitsTurnedAway = recruitsOffered - (recruitedPimps + recruitedHoes + recruitedThugs);

        // Hustlers at home lift the take. Street work is blocked while a mission is out, so nobody
        // is away commanding at this point.
        var streetBonusPercent = pimps.StreetBonusPercent(player, awayPimpIds ?? []) + (territory?.StreetIncomePercent ?? 0);
        var grossBeforeBonus = gross;
        gross += (long)Math.Round(gross * (streetBonusPercent / 100.0), MidpointRounding.AwayFromZero);

        var crewPayout = (long)Math.Round(gross * (player.HoeCutPercent / 100.0), MidpointRounding.AwayFromZero);
        var playerProfit = Math.Max(0, gross - crewPayout);

        player.Turns -= turns;
        player.Cash += playerProfit;
        // Working the streets is illegal too, so it draws attention whether or not anything is held.
        player.Heat += Math.Max(0, _options.Hideout.HeatPerStreetTurn) * turns;
        // Recruited pimps arrive as named crew, which also moves the counter.
        var recruitedPimpNames = pimps.Hire(player, recruitedPimps, DateTime.UtcNow).Select(x => x.Name).ToList();
        player.Hoes += recruitedHoes;
        player.Thugs += recruitedThugs;
        player.Condoms += condomsFound;
        player.Beer += beerFound;
        player.Weed += weedFound;
        player.Coke += cokeFound;

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
        summary += $"Worked the streets for {turns} turn{Plural(turns)}. Grossed ${gross:N0}; crew cut was ${crewPayout:N0}; you kept ${playerProfit:N0}.";
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
        // Cut stretches coke and nothing else. Spent to the extent there is any, one unit per unit of
        // coke, so it is worth exactly what the coke it makes is worth in this town.
        var cutUsed = key == "coke" ? Math.Min(player.Cut, baseUnits) : 0;
        player.Cut -= cutUsed;
        var produced = (int)Math.Round(baseUnits * (1 + labBonusPercent / 100.0), MidpointRounding.AwayFromZero) + cutUsed;

        var totalCost = (long)production.CostPerTurn * turns;
        if (player.Cash < totalCost)
            throw new GameRuleException($"You need ${totalCost:N0} cash on hand for production materials.");

        player.Cash -= totalCost;
        player.Turns -= turns;
        if (key == "weed") player.Weed += produced;
        else player.Coke += produced;
        var overflow = hideout.Settle(player, stockBefore);

        var summary = $"Produced {produced:N0} {key} using {turns} turn{Plural(turns)} and ${totalCost:N0} in materials.";
        if (labBonusPercent > 0)
            summary += $" The {key} lab added {labBonusPercent:N0}% yield.";
        if (cutUsed > 0)
            summary += $" {cutUsed:N0} cut stretched it by the same again.";
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
                ["cutUsed"] = cutUsed,
                ["costPerTurn"] = production.CostPerTurn,
                ["totalCost"] = totalCost,
                ["weedLostToStorage"] = overflow.WeedLost,
                ["cokeLostToStorage"] = overflow.CokeLost
            });
    }

    /// <summary>
    /// Turns and materials into weapons. Separate from Produce because weapons are not a product you
    /// sell to the game at a fixed price: they are a supply everyone burns, which is what makes them
    /// worth putting on the board.
    /// </summary>
    public ActionResultResponse Forge(Player player, int turns) => Make(player, "workshop", turns);

    /// <summary>
    /// Turns and materials into one good. The workshop, the still and the mix house are the same shape,
    /// so they share a path rather than three near-copies that can drift apart on the storage rule.
    /// </summary>
    public ActionResultResponse Make(Player player, string station, int turns)
    {
        TravelGate.EnsureLanded(player);
        if (turns < 1 || turns > _options.MaxActionTurns)
            throw new GameRuleException($"Work between 1 and {_options.MaxActionTurns} turns.");
        if (player.Turns < turns)
            throw new GameRuleException("You do not have that many turns.");

        var (good, label, refusal) = station switch
        {
            "still" => ("moonshine", "moonshine", "You need a still before you can brew moonshine."),
            "mix" => ("cut", "cut", "You need a mix house before you can make cut."),
            _ => ("weapons", "weapon(s)", "You need a workshop before you can make weapons.")
        };
        var workshop = hideout.StationFor(player.Hideout, station) ?? throw new GameRuleException(refusal);
        if (hideout.StationRequiredTier(station) is { } needed && (player.Hideout?.Tier ?? 1) < needed)
            throw new GameRuleException($"Making {good} needs the {hideout.TierName(needed)} or better.");

        var capacity = hideout.CapacityFor(player.Hideout);
        var room = Math.Max(0, TradeGoods.Capacity(capacity, good) - TradeGoods.Held(player, good));
        if (room == 0)
            throw new GameRuleException($"Your storage has no room for more {good}.");

        // Bounded by the room up front rather than made and spilled, so nobody pays for materials that
        // turn into nothing.
        var wanted = workshop.WeaponsPerTurn * turns;
        var made = Math.Min(wanted, room);
        var turnsUsed = (int)Math.Ceiling((double)made / Math.Max(1, workshop.WeaponsPerTurn));
        var cost = workshop.CostPerWeapon * made;
        if (player.Cash < cost)
            throw new GameRuleException($"Materials for {made:N0} weapon(s) cost {cost:C0}.");

        player.Turns -= turnsUsed;
        player.Cash -= cost;
        TradeGoods.Add(player, good, made);

        var summary = $"Turned out {made:N0} {label} over {turnsUsed} turn{Plural(turnsUsed)} for {cost:C0} in materials.";
        if (made < wanted)
            summary += " Storage filled up before the run finished.";

        return new ActionResultResponse(summary, player.Turns, new Dictionary<string, object?>
        {
            ["good"] = good,
            ["weaponsMade"] = made,
            ["unitsMade"] = made,
            ["turnsSpent"] = turnsUsed,
            ["costPerWeapon"] = workshop.CostPerWeapon,
            ["totalCost"] = cost,
            ["storePrice"] = _options.WeaponPrice
        });
    }

    public ActionResultResponse SellProduct(Player player, string? product, int quantity)
    {
        TravelGate.EnsureLanded(player);
        if (quantity is < 1 or > 100_000)
            throw new GameRuleException("Quantity must be between 1 and 100,000.");

        var key = NormalizeProduct(product);
        var stockBefore = StockLevels.From(player);
        var price = ProductSellPrice(player.City, key);
        if (key == "weed")
        {
            if (player.Weed < quantity) throw new GameRuleException("You do not have enough weed.");
            player.Weed -= quantity;
        }
        else
        {
            if (player.Coke < quantity) throw new GameRuleException("You do not have enough coke.");
            player.Coke -= quantity;
        }

        var total = (long)quantity * price;
        player.Cash += total;
        var overflow = hideout.Settle(player, stockBefore);
        return new ActionResultResponse(
            $"Sold {quantity:N0} {key} for ${total:N0} cash.{overflow.Describe()}",
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
            _ => throw new GameRuleException("Product must be 'weed' or 'coke'.")
        };

    public ActionResultResponse BuyStoreItem(Player player, string? itemKey, int quantity)
    {
        TravelGate.EnsureLanded(player);
        if (quantity is < 1 or > 10_000)
            throw new GameRuleException("Quantity must be between 1 and 10,000.");

        var item = GetStore().SingleOrDefault(x => string.Equals(x.Key, itemKey?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (item is null)
            throw new GameRuleException("Unknown store item.");

        var total = (long)item.Price * quantity;
        if (player.Cash < total)
            throw new GameRuleException("You do not have enough cash on hand.");

        // Purchases are refused rather than clamped: losing goods you paid for is worse than a refusal.
        var capacity = hideout.CapacityFor(player.Hideout);
        var (held, cap) = item.Key switch
        {
            "condoms" => (player.Condoms, capacity.MaxCondoms),
            "beer" => (player.Beer, capacity.MaxBeer),
            "weapons" => (player.Weapons, capacity.MaxWeapons),
            _ => throw new GameRuleException("Store item is not implemented.")
        };
        var room = Math.Max(0, cap - held);
        if (quantity > room)
            throw new GameRuleException(room == 0
                ? $"Your storage room is full at {cap:N0} {item.Name.ToLowerInvariant()}. Upgrade it to hold more."
                : $"Your storage room only has space for {room:N0} more {item.Name.ToLowerInvariant()}.");

        player.Cash -= total;
        switch (item.Key)
        {
            case "condoms": player.Condoms += quantity; break;
            case "beer": player.Beer += quantity; break;
            case "weapons": player.Weapons += quantity; break;
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

    public ActionResultResponse Deposit(Player player, long amount)
    {
        TravelGate.EnsureLanded(player);
        ValidateMoneyAmount(amount);
        if (player.Cash < amount) throw new GameRuleException("You do not have that much cash on hand.");
        player.Cash -= amount;
        player.BankCash += amount;
        return new ActionResultResponse(
            $"Deposited ${amount:N0} into the bank.",
            player.Turns,
            new Dictionary<string, object?> { ["amount"] = amount, ["direction"] = "deposit" });
    }

    public ActionResultResponse Withdraw(Player player, long amount)
    {
        TravelGate.EnsureLanded(player);
        ValidateMoneyAmount(amount);
        if (player.BankCash < amount) throw new GameRuleException("You do not have that much money in the bank.");

        // Refused rather than clamped, since clamping would bounce the cash straight back to the bank.
        var safeCap = hideout.CapacityFor(player.Hideout).MaxCash;
        var room = Math.Max(0, safeCap - player.Cash);
        if (amount > room)
            throw new GameRuleException(room == 0
                ? $"Your safe is full at ${safeCap:N0} cash on hand. Upgrade it to hold more."
                : $"Your safe only has room for ${room:N0} more cash on hand.");

        player.BankCash -= amount;
        player.Cash += amount;
        return new ActionResultResponse(
            $"Withdrew ${amount:N0} from the bank.",
            player.Turns,
            new Dictionary<string, object?> { ["amount"] = amount, ["direction"] = "withdraw" });
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
            _ => throw new GameRuleException("Crew role must be 'pimps', 'hoes', or 'thugs'.")
        };

        if (normalizedRole == "hoes" && player.HoeHappiness < crew.MinHoeMoraleToHire)
            throw new GameRuleException($"Hoe morale must be at least {crew.MinHoeMoraleToHire:N0}% before you can hire more hoes.");
        if (normalizedRole == "thugs" && player.ThugHappiness < crew.MinThugMoraleToHire)
            throw new GameRuleException($"Thug morale must be at least {crew.MinThugMoraleToHire:N0}% before you can hire more thugs.");

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
            throw new GameRuleException(room == 0
                ? $"Your {capacity.TierName} holds {cap:N0} {normalizedRole} and is full."
                : $"Your {capacity.TierName} only has room for {room:N0} more {CrewLabel(normalizedRole, room)}.");
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
                    throw new GameRuleException("You do not have that many hoes.");
                player.Hoes -= quantity;
                player.HoeHappiness = ClampHappiness(player.HoeHappiness - FirePenalty(quantity, crew.FireHoeMoralePenalty, crew));
                break;
            case "thugs":
                if (player.Thugs < quantity)
                    throw new GameRuleException("You do not have that many thugs.");
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
            throw new GameRuleException("Hoe cut must be between 10% and 80%.");
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

        throw new GameRuleException("Morale recovery strategy must be 'rest' or 'party'.");
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
            _ => throw new GameRuleException("Product must be 'weed' or 'coke'.")
        };

    private static void ValidateTurns(Player player, int turns, int max, string action)
    {
        if (turns is < 1 || turns > max)
            throw new GameRuleException($"{action} must use between 1 and {max} turns.");
        if (player.Turns < turns)
            throw new GameRuleException("You do not have enough turns.");
    }

    private static void ValidateMoneyAmount(long amount)
    {
        if (amount is < 1 or > 1_000_000_000_000)
            throw new GameRuleException("Amount must be between $1 and $1,000,000,000,000.");
    }

    private static string NormalizeProduct(string? product)
    {
        var key = product?.Trim().ToLowerInvariant();
        if (key is "weed" or "coke") return key;
        throw new GameRuleException("Product must be 'weed' or 'coke'.");
    }

    private static string NormalizeCrewRole(string? role)
    {
        var key = role?.Trim().ToLowerInvariant();
        return key switch
        {
            "pimp" or "pimps" => "pimps",
            "hoe" or "hoes" => "hoes",
            "thug" or "thugs" => "thugs",
            _ => throw new GameRuleException("Crew role must be 'pimps', 'hoes', or 'thugs'.")
        };
    }

    private static void ValidateCrewQuantity(int quantity, CrewOptions crew)
    {
        if (quantity < 1 || quantity > crew.MaxCrewTransactionQuantity)
            throw new GameRuleException($"Quantity must be between 1 and {crew.MaxCrewTransactionQuantity:N0}.");
    }

    private static double FirePenalty(int quantity, double penaltyPerCrew, CrewOptions crew)
        => Math.Min(crew.MaxFireMoralePenalty, quantity * penaltyPerCrew);

    private static string CrewLabel(string role, int quantity)
        => quantity == 1 ? role.TrimEnd('s') : role;

    private int Roll(RangeOptions range) => random.NextInclusive(range.Min, range.Max);

    private bool RollChance(double chance) => random.NextDouble() < Math.Clamp(chance, 0, 1);

    private int RollFind(FindOptions find)
        => RollChance(find.Chance) ? random.NextInclusive(find.Min, find.Max) : 0;

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
