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

        return new CrewReportResponse(
            managementCapacity,
            unmanagedHoes,
            armedThugs,
            uncoveredThugs,
            condomsNeeded,
            beerNeeded,
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

    public ActionResultResponse Scout(Player player, int turns, bool autoBuySupplies = false)
    {
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
        var streetBonusPercent = pimps.StreetBonusPercent(player, []);
        var grossBeforeBonus = gross;
        gross += (long)Math.Round(gross * (streetBonusPercent / 100.0), MidpointRounding.AwayFromZero);

        var crewPayout = (long)Math.Round(gross * (player.HoeCutPercent / 100.0), MidpointRounding.AwayFromZero);
        var playerProfit = Math.Max(0, gross - crewPayout);

        player.Turns -= turns;
        player.Cash += playerProfit;
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
        var beerShortage = beerNeeded - beerUsed;
        player.Beer -= beerUsed;

        var managementCapacity = Math.Max(1, player.Pimps) * morale.HoesManagedPerPimp;
        var unmanagedHoes = Math.Max(0, player.Hoes - managementCapacity);
        var uncoveredThugs = Math.Max(0, player.Thugs - player.Weapons);

        var cutEffect = (player.HoeCutPercent - morale.BaselineHoeCutPercent) * turns * morale.HoeCutMoraleScalePerTurn;
        var hoeDelta = morale.HoeStreetWorkGainPerTurn * turns
            + cutEffect
            - condomShortage * morale.CondomShortagePenalty
            - unmanagedHoes * morale.UnmanagedHoePenalty;
        var thugDelta = morale.ThugStreetWorkGainPerTurn * turns
            - beerShortage * morale.BeerShortagePenalty
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
        if (streetBonusPercent > 0)
            summary += $" Your hustlers added {streetBonusPercent}% to the take.";
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

    public ActionResultResponse Produce(Player player, string? product, int turns)
    {
        ValidateTurns(player, turns, _options.MaxActionTurns, "Production");
        var key = NormalizeProduct(product);
        var production = GetProduction(key);
        var stockBefore = StockLevels.From(player);

        var baseUnits = 0;
        for (var i = 0; i < turns; i++)
            baseUnits += random.NextInclusive(production.UnitsMin, production.UnitsMax);

        // The lab is turn-fed: it raises what each production turn yields rather than running itself.
        var labBonusPercent = hideout.ProductionYieldBonusPercent(player.Hideout, key);
        var produced = (int)Math.Round(baseUnits * (1 + labBonusPercent / 100.0), MidpointRounding.AwayFromZero);

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

    public ActionResultResponse SellProduct(Player player, string? product, int quantity)
    {
        if (quantity is < 1 or > 100_000)
            throw new GameRuleException("Quantity must be between 1 and 100,000.");

        var key = NormalizeProduct(product);
        var stockBefore = StockLevels.From(player);
        var price = key == "weed" ? _options.WeedSellPrice : _options.CokeSellPrice;
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

    public ActionResultResponse BuyStoreItem(Player player, string? itemKey, int quantity)
    {
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
