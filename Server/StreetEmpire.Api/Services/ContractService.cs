using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

/// <summary>
/// The people in a town who want things.
///
/// Generated on read rather than by a background service, the way ground is seeded: a town nobody has
/// visited needs no contracts, and topping the board up when somebody looks at it costs one query and
/// cannot drift out of step with a timer nobody is watching.
/// </summary>
public sealed class ContractService(GameDbContext db, IOptionsSnapshot<GameOptions> options, IGameRandom random)
{
    private readonly GameOptions _options = options.Value;

    /// <summary>
    /// What is on offer in a town right now, topping the board up first.
    ///
    /// Expired contracts are left in the table rather than deleted: a player who missed one should be
    /// able to see that they missed it, and the board reads the open ones by date anyway.
    /// </summary>
    public async Task<IReadOnlyList<Contract>> BoardAsync(string city, DateTime nowUtc, CancellationToken ct)
    {
        var config = _options.Contracts;
        var open = await db.Contracts
            .Where(x => x.City == city && x.FilledAtUtc == null && x.ExpiresAtUtc > nowUtc)
            .OrderBy(x => x.ExpiresAtUtc)
            .ToListAsync(ct);

        var wanted = Math.Max(0, config.OpenPerCity);
        if (open.Count >= wanted)
            return open;

        // A town posts orders at a pace rather than on demand. Without this the board refilled the
        // instant anybody looked, so a player could fill one, look again for a fresh one, and keep
        // going: the counter price would never be worth taking again and every sale in the game would
        // quietly be worth a third more. Filling one now means waiting for the next.
        var lastPosted = await db.Contracts
            .Where(x => x.City == city)
            .OrderByDescending(x => x.PostedAtUtc)
            .Select(x => (DateTime?)x.PostedAtUtc)
            .FirstOrDefaultAsync(ct);
        if (lastPosted is { } posted && posted.AddMinutes(Math.Max(0, config.PostIntervalMinutes)) > nowUtc)
            return open;

        var places = _options.Territory.Map
            .Where(x => string.Equals(x.City, city, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Name)
            .ToList();
        if (places.Count == 0)
            return open;

        // One at a time, so a stripped board recovers at the town's pace rather than all at once.
        // A town nobody has visited fills up on the first look, which is what makes it feel settled
        // rather than empty.
        var toPost = lastPosted is null ? wanted - open.Count : 1;
        for (var i = 0; i < toPost; i++)
        {
            var contract = Compose(city, places, nowUtc);
            db.Contracts.Add(contract);
            open.Add(contract);
        }

        await db.SaveChangesAsync(ct);
        return open.OrderBy(x => x.ExpiresAtUtc).ToList();
    }

    /// <summary>
    /// Hands the goods over and takes the money.
    ///
    /// Every refusal is a real one: this is the same stock the rest of the game moves, so a contract
    /// cannot be filled with product the player does not have, or with coke too weak for a buyer who
    /// asked for strength.
    /// </summary>
    public ContractFill Fill(Contract contract, Player player, DateTime nowUtc)
    {
        TravelGate.EnsureLanded(player);

        if (!contract.IsOpen(nowUtc))
            throw new GameRuleException("That job is already gone.");
        if (!string.Equals(contract.City, player.City, StringComparison.OrdinalIgnoreCase))
            throw new GameRuleException($"That buyer is in {contract.City}. You have to be there.");

        var held = TradeGoods.Held(player, contract.Good);
        if (held < contract.Quantity)
            throw new GameRuleException(
                $"They want {contract.Quantity:N0} {TradeGoods.Label(contract.Good).ToLowerInvariant()} and you have {held:N0}.");

        if (contract.MinimumPurityPercent is { } floor)
        {
            var purity = (int)Math.Round(player.CokePurity * 100);
            if (purity < floor)
                throw new GameRuleException($"They want it at least {floor}% pure. Yours is {purity}%.");
        }

        TradeGoods.Add(player, contract.Good, -contract.Quantity);
        player.Cash += contract.Payout;
        contract.FilledById = player.Id;
        contract.FilledBy = player;
        contract.FilledAtUtc = nowUtc;

        var premium = contract.Payout - contract.FlatValue;
        return new ContractFill(
            contract,
            contract.Payout,
            premium,
            $"Filled {contract.Buyer}'s order: {contract.Quantity:N0} {contract.Good} for {contract.Payout:C0}"
            + (premium > 0 ? $", {premium:C0} more than selling it flat." : "."));
    }

    /// <summary>
    /// One order, drawn from what the town is like. A place that sells coke dearest asks for coke; a
    /// town with cheap weed wants it moved in bulk. The character of a city decides what it wants, so
    /// the board reads as somewhere rather than as a random number generator.
    /// </summary>
    private Contract Compose(string city, IReadOnlyList<string> places, DateTime nowUtc)
    {
        var config = _options.Contracts;
        var good = ChooseGood(city);
        var list = TradeGoods.ReferencePrice(_options, good, city);
        var premium = 1 + config.MinPremiumPercent / 100.0
                        + random.NextInclusive(0, Math.Max(0, config.PremiumSpreadPercent)) / 100.0;

        // Purity floors only mean anything for coke, and only sometimes: a buyer who always demanded
        // strength would make stretching pointless rather than a trade.
        int? purityFloor = good == "coke" && random.NextDouble() < config.PurityConditionChance
            ? config.MinimumPurityFloorPercent
            : null;
        // Somebody paying for strength pays for it, or the condition is only a penalty.
        if (purityFloor is not null)
            premium += config.PurityPremiumPercent / 100.0;

        return new Contract
        {
            City = city,
            Buyer = places[random.NextInclusive(0, places.Count - 1)],
            Good = good,
            Quantity = random.NextInclusive(Math.Max(1, config.MinQuantity), Math.Max(1, config.MaxQuantity)),
            ListPricePerUnit = list,
            PricePerUnit = Math.Max(list + 1, (long)Math.Round(list * premium)),
            MinimumPurityPercent = purityFloor,
            PostedAtUtc = nowUtc,
            ExpiresAtUtc = nowUtc.AddHours(random.NextInclusive(
                Math.Max(1, config.MinLifetimeHours),
                Math.Max(1, config.MaxLifetimeHours)))
        };
    }

    /// <summary>
    /// What a town asks for. Weighted towards what it pays most for, because a buyer wanting the thing
    /// nobody there can shift would be a job posted by nobody in particular.
    /// </summary>
    private string ChooseGood(string city)
    {
        var config = _options.Contracts;
        var roll = random.NextInclusive(0, 99);
        // Weapons and moonshine are the standing minority: everybody needs weapons and moonshine is
        // always worth something, but a town's identity is in its weed and its coke.
        if (roll < config.WeaponsPercent) return "weapons";
        if (roll < config.WeaponsPercent + config.MoonshinePercent) return "moonshine";

        // Between the two products, lean towards whichever this town values more without ever ruling
        // the other out. Always asking for the dearer one made a town a one-note board: Las Vegas
        // would have wanted coke and only coke, for as long as it existed.
        var weed = TradeGoods.ReferencePrice(_options, "weed", city) / (double)Math.Max(1, _options.WeedSellPrice);
        var coke = TradeGoods.ReferencePrice(_options, "coke", city) / (double)Math.Max(1, _options.CokeSellPrice);
        var favoured = coke >= weed ? "coke" : "weed";
        var other = favoured == "coke" ? "weed" : "coke";
        return random.NextInclusive(0, 99) < Math.Clamp(config.FavouredGoodPercent, 50, 100) ? favoured : other;
    }
}

/// <summary>What filling an order actually paid, and what it beat.</summary>
public sealed record ContractFill(Contract Contract, long Paid, long Premium, string Summary);
