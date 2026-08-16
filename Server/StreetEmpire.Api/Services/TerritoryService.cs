using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

/// <summary>
/// Who holds what, what it is worth, and what it costs to sit on.
///
/// The effects are all percentages on activities the player still spends turns to perform, so holding
/// ground amplifies play rather than replacing it. Nothing here pays out on its own.
/// </summary>
public sealed class TerritoryService(GameDbContext db, IOptionsSnapshot<GameOptions> options)
{
    private readonly GameOptions _options = options.Value;

    /// <summary>Ensures the fixed map exists. Idempotent, so it is safe to call on any read.</summary>
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var configured = _options.Territory.Map;
        if (configured.Count == 0)
            return;

        var existing = await db.Territories.AsNoTracking().Select(x => x.Name).ToListAsync(ct);
        var known = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = false;
        foreach (var seed in configured)
        {
            if (known.Contains(seed.Name))
                continue;
            db.Territories.Add(new Territory { Name = seed.Name, City = seed.City, Type = seed.Type });
            added = true;
        }

        if (added)
            await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// The bonuses a player's holdings add up to. Read on every street action and production run, so it
    /// takes the garrison rows it is given rather than querying for them itself.
    /// </summary>
    public TerritoryEffects EffectsFor(IEnumerable<Territory> held)
    {
        var street = 0;
        var production = 0;
        var morale = 0;
        var loot = 0;
        foreach (var territory in held)
        {
            var type = TypeOf(territory.Type);
            if (type is null)
                continue;
            street += type.StreetIncomePercent;
            production += type.ProductionYieldPercent;
            morale += type.MoraleRecoveryPercent;
            loot += type.LootPercent;
        }

        return new TerritoryEffects(street, production, morale, loot);
    }

    public async Task<TerritoryEffects> EffectsForAsync(Guid playerId, string? city = null, CancellationToken ct = default)
        => EffectsFor((await HeldByAsync(playerId, ct))
            .Where(x => city is null || string.Equals(x.City, city, StringComparison.OrdinalIgnoreCase)));

    public async Task<List<Territory>> HeldByAsync(Guid playerId, CancellationToken ct = default)
        => await db.Territories.AsNoTracking().Where(x => x.HolderId == playerId).ToListAsync(ct);

    /// <summary>
    /// Ground is contested inside a town only. Checked in the service rather than the endpoint so the
    /// claim path, the raid path, and the rivals all answer to the same rule.
    /// </summary>
    public static bool SameCity(Player player, Territory territory)
        => string.Equals(player.City, territory.City, StringComparison.OrdinalIgnoreCase);

    public TerritoryTypeOptions? TypeOf(string type)
        => _options.Territory.Types.FirstOrDefault(x => string.Equals(x.Type, type, StringComparison.OrdinalIgnoreCase));

    /// <summary>How many pieces of ground this hideout tier is allowed to hold at once.</summary>
    public int HoldingCapFor(Hideout? hideout)
    {
        var tier = hideout?.Tier ?? 1;
        var configured = _options.Territory.TierCaps.FirstOrDefault(x => x.Tier == tier);
        return configured?.MaxTerritories ?? 1;
    }

    /// <summary>
    /// Claims ground nobody holds. Taking it off somebody is a mission, not this: the point of the
    /// system is that held ground has to be fought for.
    /// </summary>
    public async Task<Territory> ClaimAsync(Player player, long territoryId, int thugs, long? pimpId, DateTime nowUtc, CancellationToken ct)
    {
        TravelGate.EnsureLanded(player);
        var config = _options.Territory;
        var territory = await db.Territories.SingleOrDefaultAsync(x => x.Id == territoryId, ct)
            ?? throw new GameRuleException("That ground does not exist.");
        if (!SameCity(player, territory))
            throw new GameRuleException($"{territory.Name} is in {territory.City}. You run {player.City}.");
        if (territory.HolderId is not null)
            throw new GameRuleException($"{territory.Name} is already held. You will have to take it.");
        if (territory.ProtectedUntilUtc is { } until && until > nowUtc)
            throw new GameRuleException($"{territory.Name} has just changed hands and is not up for grabs yet.");

        var held = await db.Territories.CountAsync(x => x.HolderId == player.Id, ct);
        var cap = HoldingCapFor(player.Hideout);
        if (held >= cap)
            throw new GameRuleException($"A {TierName(player)} can only run {cap} piece(s) of ground at once.");

        if (thugs < config.MinimumGarrison)
            throw new GameRuleException($"It takes {config.MinimumGarrison} thugs to hold ground.");
        if (player.Turns < config.ClaimTurnCost)
            throw new GameRuleException($"Claiming ground takes {config.ClaimTurnCost} turns.");

        var free = await FreeThugsAsync(player, ct);
        if (thugs > free)
            throw new GameRuleException($"You only have {free:N0} thug(s) free. The rest are out or already on the ground.");

        territory.GarrisonPimpId = await ResolveGarrisonPimpAsync(player, pimpId, territoryId, ct);
        player.Turns -= config.ClaimTurnCost;
        territory.HolderId = player.Id;
        territory.GarrisonThugs = thugs;
        territory.HeldSinceUtc = nowUtc;
        territory.ProtectedUntilUtc = nowUtc.AddMinutes(config.HoldCooldownMinutes);
        return territory;
    }

    /// <summary>
    /// Moves thugs on or off ground already held. Dropping below the minimum gives the ground up rather
    /// than leaving it held by nobody, so a garrison always means what it says.
    /// </summary>
    public async Task<(Territory Territory, bool GaveUp)> SetGarrisonAsync(Player player, long territoryId, int thugs, long? pimpId, CancellationToken ct)
    {
        TravelGate.EnsureLanded(player);
        var config = _options.Territory;
        var territory = await db.Territories.SingleOrDefaultAsync(x => x.Id == territoryId, ct)
            ?? throw new GameRuleException("That ground does not exist.");
        if (territory.HolderId != player.Id)
            throw new GameRuleException("You do not hold that ground.");
        if (thugs < 0)
            throw new GameRuleException("A garrison cannot be negative.");

        if (thugs < config.MinimumGarrison)
        {
            territory.HolderId = null;
            territory.GarrisonThugs = 0;
            territory.GarrisonPimpId = null;
            territory.HeldSinceUtc = null;
            return (territory, true);
        }

        var free = await FreeThugsAsync(player, ct) + territory.GarrisonThugs;
        if (thugs > free)
            throw new GameRuleException($"You only have {free:N0} thug(s) available for that ground.");

        territory.GarrisonPimpId = await ResolveGarrisonPimpAsync(player, pimpId, territoryId, ct);
        territory.GarrisonThugs = thugs;
        return (territory, false);
    }

    /// <summary>
    /// Thugs at home and not spoken for. Garrisons count as away for the same reason a raiding party
    /// does: they cannot be in two places, and pretending otherwise would make holding ground free.
    /// </summary>
    public async Task<int> FreeThugsAsync(Player player, CancellationToken ct = default)
    {
        var garrisoned = await GarrisonedThugsAsync(player.Id, ct);
        var onMissions = await db.CombatMissions.AsNoTracking()
            .Where(x => x.AttackerId == player.Id && x.Status != "Complete")
            .SumAsync(x => (int?)x.RemainingAttackers, ct) ?? 0;
        return Math.Max(0, player.Thugs - garrisoned - onMissions);
    }

    /// <summary>
    /// Pimps posted to ground. They are away for every purpose a mission commander is: no house
    /// defence bonus, no street income bonus, and not available to command a raid.
    /// </summary>
    public async Task<List<long>> GarrisonedPimpIdsAsync(Guid playerId, CancellationToken ct = default)
        => await db.Territories.AsNoTracking()
            .Where(x => x.HolderId == playerId && x.GarrisonPimpId != null)
            .Select(x => x.GarrisonPimpId!.Value)
            .ToListAsync(ct);

    public async Task<int> GarrisonedThugsAsync(Guid playerId, CancellationToken ct = default)
        => await db.Territories.AsNoTracking()
            .Where(x => x.HolderId == playerId)
            .SumAsync(x => (int?)x.GarrisonThugs, ct) ?? 0;

    /// <summary>
    /// Validates the pimp being posted. Somebody already out commanding a raid or standing on other
    /// ground cannot also run this one, which is the same rule a mission commander answers to.
    /// </summary>
    private async Task<long?> ResolveGarrisonPimpAsync(Player player, long? pimpId, long territoryId, CancellationToken ct)
    {
        if (pimpId is not { } id)
            return null;

        var pimp = player.Crew.FirstOrDefault(x => x.Id == id && x.LostAtUtc is null)
            ?? throw new GameRuleException("That pimp is not on your roster.");

        var commanding = await db.CombatMissions.AsNoTracking()
            .AnyAsync(x => x.AttackerId == player.Id && x.Status != "Complete" && x.CommanderPimpId == id, ct);
        if (commanding)
            throw new GameRuleException($"{pimp.Name} is out commanding a raid.");

        var elsewhere = await db.Territories.AsNoTracking()
            .AnyAsync(x => x.HolderId == player.Id && x.Id != territoryId && x.GarrisonPimpId == id, ct);
        if (elsewhere)
            throw new GameRuleException($"{pimp.Name} is already running other ground.");

        return id;
    }

    /// <summary>
    /// Hands ground to its new holder after a won raid. The winning force stays as the garrison, which
    /// is why taking ground and then walking away is not an option.
    /// </summary>
    public void Transfer(Territory territory, Guid newHolderId, int garrison, DateTime nowUtc)
    {
        territory.HolderId = newHolderId;
        // The beaten pimp does not stay on to run it for the winner.
        territory.GarrisonPimpId = null;
        territory.GarrisonThugs = Math.Max(_options.Territory.MinimumGarrison, garrison);
        territory.HeldSinceUtc = nowUtc;
        territory.ProtectedUntilUtc = nowUtc.AddMinutes(_options.Territory.HoldCooldownMinutes);
    }

    /// <summary>A failed raid still costs the defender: the garrison wore the damage.</summary>
    public void Bloody(Territory territory, int losses)
        => territory.GarrisonThugs = Math.Max(0, territory.GarrisonThugs - Math.Max(0, losses));

    private string TierName(Player player)
        => _options.Hideout.Tiers.FirstOrDefault(x => x.Level == (player.Hideout?.Tier ?? 1))?.Name ?? "hideout";
}

/// <summary>Percentages a player's holdings add to activities they still spend turns on.</summary>
public sealed record TerritoryEffects(
    int StreetIncomePercent,
    int ProductionYieldPercent,
    int MoraleRecoveryPercent,
    int LootPercent)
{
    public static readonly TerritoryEffects None = new(0, 0, 0, 0);

    public bool Any => StreetIncomePercent > 0 || ProductionYieldPercent > 0 || MoraleRecoveryPercent > 0 || LootPercent > 0;
}
