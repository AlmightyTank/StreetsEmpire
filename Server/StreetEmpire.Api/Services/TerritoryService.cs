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
            // What the ground is worth is the type's number times what has been put into the ground.
            // Bare ground multiplies by one, so a map nobody has developed reads exactly as it did.
            var worked = _options.Territory.DevelopmentMultiplier(territory.DevelopmentLevel);
            street += Scale(type.StreetIncomePercent, worked);
            production += Scale(type.ProductionYieldPercent, worked);
            morale += Scale(type.MoraleRecoveryPercent, worked);
            loot += Scale(type.LootPercent, worked);
        }

        return new TerritoryEffects(street, production, morale, loot);
    }

    public async Task<TerritoryEffects> EffectsForAsync(Guid playerId, string? city = null, CancellationToken ct = default)
        => EffectsFor((await HeldByAsync(playerId, ct))
            .Where(x => city is null || string.Equals(x.City, city, StringComparison.OrdinalIgnoreCase)));

    public async Task<List<Territory>> HeldByAsync(Guid playerId, CancellationToken ct = default)
        => await db.Territories.AsNoTracking().Where(x => x.HolderId == playerId).ToListAsync(ct);

    public async Task<IReadOnlyDictionary<long, IReadOnlyList<AllianceCityControl>>> ControlledCitiesByAllianceAsync(CancellationToken ct = default)
    {
        var territories = await db.Territories.AsNoTracking()
            .Include(x => x.Holder)
            .ToListAsync(ct);

        return territories
            .GroupBy(x => x.City, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var allianceIds = group.Select(x => x.Holder?.AllianceId).Distinct().ToList();
                var allianceId = allianceIds.Count == 1 ? allianceIds[0] : null;
                if (allianceId is null || group.Any(x => x.HolderId is null || x.Holder?.AllianceId != allianceId))
                    return null;
                var city = group.First().City;
                var bonus = CityControlBonusThugs(city);
                return bonus <= 0 ? null : new AllianceCityControl(allianceId.Value, city, group.Count(), bonus);
            })
            .Where(x => x is not null)
            .Select(x => x!)
            .GroupBy(x => x.AllianceId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<AllianceCityControl>)group.OrderBy(x => x.City, StringComparer.Ordinal).ToList());
    }

    public async Task<IReadOnlyList<AllianceCityControl>> ControlledCitiesForAllianceAsync(long allianceId, CancellationToken ct = default)
        => (await ControlledCitiesByAllianceAsync(ct)).TryGetValue(allianceId, out var controls) ? controls : [];

    public async Task<int> CityControlThugsForAllianceInCityAsync(long allianceId, string? city, CancellationToken ct = default)
    {
        var wanted = city?.Trim();
        if (string.IsNullOrWhiteSpace(wanted))
            return 0;

        var controls = await ControlledCitiesForAllianceAsync(allianceId, ct);
        return controls.FirstOrDefault(x => string.Equals(x.City, wanted, StringComparison.OrdinalIgnoreCase))?.BonusThugs ?? 0;
    }

    public int CityControlBonusThugs(string? city)
        => Math.Max(0, _options.Territory.CityControl
            .FirstOrDefault(x => string.Equals(x.City, city?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?.BonusThugs ?? 0);

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

    private static int Scale(int percent, double multiplier)
        => percent <= 0 ? 0 : (int)Math.Round(percent * multiplier, MidpointRounding.AwayFromZero);

    /// <summary>What this ground is worth against a bare piece of the same type, as a multiplier.</summary>
    public double DevelopmentMultiplier(Territory ground)
        => _options.Territory.DevelopmentMultiplier(ground.DevelopmentLevel);

    /// <summary>
    /// What the work adds to the garrison standing on it. Passed to the raid as a bonus percentage
    /// alongside an enforcer's, so money in the ground buys some of the reason you keep it rather than
    /// only painting a target on it.
    /// </summary>
    public int DevelopmentDefencePercent(Territory ground)
        => _options.Territory.DevelopmentDefencePercent(ground.DevelopmentLevel);

    public string DevelopmentName(int level)
        => _options.Territory.DevelopmentAt(level)?.Name ?? "Bare";

    /// <summary>
    /// Starts the next level of work on ground the player already holds.
    ///
    /// The money and the turns go now and the ground is worth what it was worth until the build lands,
    /// which is the same bargain a hideout tier makes. What that buys the rest of the town is a window:
    /// a piece being worked up is a piece whose holder has just spent everything, and it can be taken
    /// off them before it is finished.
    /// </summary>
    public async Task<(Territory Ground, TerritoryDevelopmentOptions Level, long FromBank)> DevelopAsync(
        Player player,
        long territoryId,
        DateTime nowUtc,
        CancellationToken ct)
    {
        TravelGate.EnsureLanded(player);
        var config = _options.Territory;
        var ground = await db.Territories.SingleOrDefaultAsync(x => x.Id == territoryId, ct)
            ?? throw new GameRuleException("That ground does not exist.");
        if (ground.HolderId != player.Id)
            throw new GameRuleException("You can only work up ground you hold.");
        if (ground.DevelopingToLevel is not null)
            throw new GameRuleException($"Work is already going on at {ground.Name}.");

        var next = config.DevelopmentAfter(ground.DevelopmentLevel)
            ?? throw new GameRuleException($"{ground.Name} is as worked up as ground gets.");

        var tier = player.Hideout?.Tier ?? 1;
        if (next.MinTier > tier)
            throw new GameRuleException($"Running {next.Name} ground takes the {TierNameAt(next.MinTier)} or better.");
        if (player.Turns < next.Turns)
            throw new GameRuleException($"Working {ground.Name} up to {next.Name} takes {next.Turns} turns.");
        if (player.Cash + player.BankCash < next.Cost)
            throw new GameRuleException($"You need {next.Cost:C0} across your cash and bank to work {ground.Name} up to {next.Name}.");

        var fromBank = Capital.Charge(player, next.Cost);
        player.Turns -= next.Turns;
        ground.DevelopingToLevel = next.Level;
        ground.DevelopmentCompletesAtUtc = nowUtc.AddMinutes(Math.Max(0, next.BuildMinutes));
        return (ground, next, fromBank);
    }

    /// <summary>
    /// Lands any work whose timer has run out. Settled on the holder's clock rather than a background
    /// sweep for the same reason a mule run is: the ground belongs to one empire, and the moment that
    /// matters is the moment that empire is next looked at.
    /// </summary>
    public static bool CompleteDevelopment(Territory ground, DateTime nowUtc)
    {
        if (ground.DevelopingToLevel is not { } level || ground.DevelopmentCompletesAtUtc is not { } due || due > nowUtc)
            return false;

        ground.DevelopmentLevel = Math.Max(ground.DevelopmentLevel, level);
        ground.DevelopingToLevel = null;
        ground.DevelopmentCompletesAtUtc = null;
        return true;
    }

    /// <summary>
    /// Ground goes back to being ground. Used wherever a holder stops holding it without losing a
    /// fight - walking away, or being pulled off by dropping the garrison under the minimum.
    ///
    /// Everything put into it is lost rather than left standing for the next person to claim, because
    /// ground that keeps its development while nobody holds it is a way to hand an empire's money to
    /// somebody else without either of them fighting for it.
    /// </summary>
    private static void Raze(Territory ground)
    {
        ground.DevelopmentLevel = 0;
        ground.DevelopingToLevel = null;
        ground.DevelopmentCompletesAtUtc = null;
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
        if (thugs > config.MaxGarrisonThugs)
            throw new GameRuleException($"One piece of ground can hold {config.MaxGarrisonThugs:N0} defender(s).");
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
            throw new GameRuleException("You cannot post fewer than nobody.");

        if (thugs < config.MinimumGarrison)
        {
            territory.HolderId = null;
            territory.GarrisonThugs = 0;
            territory.GarrisonPimpId = null;
            territory.HeldSinceUtc = null;
            // Walking away is walking away from what was put into it, half-finished work included.
            Raze(territory);
            return (territory, true);
        }
        if (thugs > config.MaxGarrisonThugs)
            throw new GameRuleException($"One piece of ground can hold {config.MaxGarrisonThugs:N0} defender(s).");

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
    public void Transfer(Territory territory, Guid newHolderId, int garrison, DateTime nowUtc, Hideout? winnersHideout = null)
    {
        // Half, rounded down, and never past what the winner's own building could have built.
        //
        // Whole would make taking ground strictly cheaper than working it up, and nobody would ever
        // build anything. Nothing at all would mean contested ground is never developed either - a
        // player would only invest where they were already safe, and one lost raid would wipe out
        // months. Half is the shape that leaves both worth doing: the attacker gets a head start on a
        // ladder they still have to climb, and the loser loses enough that defending it mattered.
        var inherited = Math.Min(
            territory.DevelopmentLevel / 2,
            _options.Territory.MaxDevelopmentForTier(winnersHideout?.Tier ?? 1));
        territory.DevelopmentLevel = Math.Max(0, inherited);
        // Work in progress does not survive the ground changing hands. The money went when it started.
        territory.DevelopingToLevel = null;
        territory.DevelopmentCompletesAtUtc = null;

        territory.HolderId = newHolderId;
        // The beaten pimp does not stay on to run it for the winner.
        territory.GarrisonPimpId = null;
        territory.GarrisonThugs = Math.Min(
            Math.Max(_options.Territory.MinimumGarrison, garrison),
            Math.Max(_options.Territory.MinimumGarrison, _options.Territory.MaxGarrisonThugs));
        territory.HeldSinceUtc = nowUtc;
        territory.ProtectedUntilUtc = nowUtc.AddMinutes(_options.Territory.HoldCooldownMinutes);
    }

    /// <summary>A failed raid still costs the defender: the garrison wore the damage.</summary>
    public void Bloody(Territory territory, int losses)
        => territory.GarrisonThugs = Math.Max(0, territory.GarrisonThugs - Math.Max(0, losses));

    private string TierName(Player player)
        => TierNameAt(player.Hideout?.Tier ?? 1);

    private string TierNameAt(int tier)
        => _options.Hideout.Tiers.FirstOrDefault(x => x.Level == tier)?.Name ?? "hideout";
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

public sealed record AllianceCityControl(long AllianceId, string City, int Territories, int BonusThugs);
