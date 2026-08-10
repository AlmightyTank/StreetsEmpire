using Microsoft.Extensions.Options;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

public sealed class EconomyService(IOptions<GameOptions> options, IGameRandom random)
{
    private readonly GameOptions _options = options.Value;

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
            crew.MinThugMoraleToHire);
    }

    public ActionResultResponse Scout(Player player, int turns)
    {
        ValidateTurns(player, turns, _options.MaxActionTurns, "Work the streets");

        var street = _options.StreetAction;
        var morale = _options.Morale;
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

        var crewPayout = (long)Math.Round(gross * (player.HoeCutPercent / 100.0), MidpointRounding.AwayFromZero);
        var playerProfit = Math.Max(0, gross - crewPayout);

        player.Turns -= turns;
        player.Cash += playerProfit;
        player.Pimps += recruitedPimps;
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

        var summary = $"Worked the streets for {turns} turn{Plural(turns)}. Grossed ${gross:N0}; crew cut was ${crewPayout:N0}; you kept ${playerProfit:N0}.";
        if (recruitedPimps + recruitedHoes + recruitedThugs > 0)
            summary += $" Recruited {recruitedPimps} pimp(s), {recruitedHoes} hoe(s), and {recruitedThugs} thug(s).";
        if (condomsFound + beerFound + weedFound + cokeFound > 0)
            summary += $" Found {condomsFound} condoms, {beerFound} beer, {weedFound} weed, and {cokeFound} coke.";
        if (condomShortage > 0) summary += $" Condom shortage: {condomShortage}.";
        if (beerShortage > 0) summary += $" Beer shortage: {beerShortage}.";
        if (unmanagedHoes > 0) summary += $" {unmanagedHoes} hoe(s) are beyond your pimp management capacity.";
        if (uncoveredThugs > 0) summary += $" {uncoveredThugs} thug(s) do not have weapons.";
        if (hoeDeserters > 0 || thugDeserters > 0)
            summary += $" {hoeDeserters} hoe(s) and {thugDeserters} thug(s) walked out due to low morale.";

        return new ActionResultResponse(summary, player.Turns, new Dictionary<string, object?>
        {
            ["turnsSpent"] = turns,
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

        var produced = 0;
        for (var i = 0; i < turns; i++)
            produced += random.NextInclusive(production.UnitsMin, production.UnitsMax);

        var totalCost = (long)production.CostPerTurn * turns;
        if (player.Cash < totalCost)
            throw new GameRuleException($"You need ${totalCost:N0} cash on hand for production materials.");

        player.Cash -= totalCost;
        player.Turns -= turns;
        if (key == "weed") player.Weed += produced;
        else player.Coke += produced;

        return new ActionResultResponse(
            $"Produced {produced:N0} {key} using {turns} turn{Plural(turns)} and ${totalCost:N0} in materials.",
            player.Turns,
            new Dictionary<string, object?>
            {
                ["product"] = key,
                ["turnsSpent"] = turns,
                ["unitsProduced"] = produced,
                ["costPerTurn"] = production.CostPerTurn,
                ["totalCost"] = totalCost
            });
    }

    public ActionResultResponse SellProduct(Player player, string? product, int quantity)
    {
        if (quantity is < 1 or > 100_000)
            throw new GameRuleException("Quantity must be between 1 and 100,000.");

        var key = NormalizeProduct(product);
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
        return new ActionResultResponse(
            $"Sold {quantity:N0} {key} for ${total:N0} cash.",
            player.Turns,
            new Dictionary<string, object?>
            {
                ["product"] = key,
                ["quantity"] = quantity,
                ["unitPrice"] = price,
                ["total"] = total
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

        player.Cash -= total;
        switch (item.Key)
        {
            case "condoms": player.Condoms += quantity; break;
            case "beer": player.Beer += quantity; break;
            case "weapons": player.Weapons += quantity; break;
            default: throw new GameRuleException("Store item is not implemented.");
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

        var totalCost = (long)unitCost * quantity;
        if (player.Cash < totalCost)
            throw new GameRuleException($"You need ${totalCost:N0} cash on hand to hire that crew.");

        player.Cash -= totalCost;
        switch (normalizedRole)
        {
            case "pimps": player.Pimps += quantity; break;
            case "hoes": player.Hoes += quantity; break;
            case "thugs": player.Thugs += quantity; break;
        }

        return new ActionResultResponse(
            $"Hired {quantity:N0} {CrewLabel(normalizedRole, quantity)} for ${totalCost:N0}.",
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

        switch (normalizedRole)
        {
            case "pimps":
                if (player.Pimps - quantity < 1)
                    throw new GameRuleException("You must keep at least one pimp managing the operation.");
                player.Pimps -= quantity;
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
            $"Fired {quantity:N0} {CrewLabel(normalizedRole, quantity)}.",
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

    private static string Plural(int value) => value == 1 ? string.Empty : "s";
}

public sealed class GameRuleException(string message) : Exception(message);
