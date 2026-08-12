using Microsoft.Extensions.Options;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

/// <summary>
/// Owns the named-pimp roster. <see cref="Player.Pimps"/> stays the authoritative count for the
/// economy and for the net worth expression the database sorts by, so every method here keeps that
/// counter in step with the active rows. A rule test asserts the two never drift.
/// </summary>
public sealed class PimpRoster(IOptionsSnapshot<GameOptions> options, IGameRandom random)
{
    private readonly PimpOptions _options = options.Value.Pimps;

    public IReadOnlyList<Pimp> Active(Player player)
        => player.Crew.Where(x => x.IsActive).OrderBy(x => x.HiredAtUtc).ThenBy(x => x.Id).ToList();

    public IReadOnlyList<Pimp> Fallen(Player player)
        => player.Crew.Where(x => !x.IsActive).OrderByDescending(x => x.LostAtUtc).ToList();

    /// <summary>Adds named pimps and moves the counter with them.</summary>
    public IReadOnlyList<Pimp> Hire(Player player, int quantity, DateTime nowUtc)
    {
        var hired = new List<Pimp>();
        for (var i = 0; i < quantity; i++)
        {
            var pimp = new Pimp
            {
                Player = player,
                PlayerId = player.Id,
                Name = NextName(player),
                Specialty = random.NextDouble() < 0.5 ? PimpSpecialties.Enforcer : PimpSpecialties.Hustler,
                BonusPercent = random.NextInclusive(
                    Math.Max(0, _options.MinBonusPercent),
                    Math.Max(Math.Max(0, _options.MinBonusPercent), _options.MaxBonusPercent)),
                Loyalty = _options.StartingLoyalty,
                HiredAtUtc = nowUtc
            };
            player.Crew.Add(pimp);
            hired.Add(pimp);
        }

        player.Pimps = Active(player).Count;
        return hired;
    }

    /// <summary>Retires pimps, newest first, so the crew you have held longest survives a cutback.</summary>
    public IReadOnlyList<Pimp> Release(Player player, int quantity, string reason, DateTime nowUtc)
    {
        var released = Active(player)
            .OrderByDescending(x => x.HiredAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(Math.Max(0, quantity))
            .ToList();

        foreach (var pimp in released)
        {
            pimp.LostAtUtc = nowUtc;
            pimp.LostReason = reason;
        }

        player.Pimps = Active(player).Count;
        return released;
    }

    /// <summary>Pimps free to lead an attack right now.</summary>
    public IReadOnlyList<Pimp> AvailableCommanders(Player player, IReadOnlyCollection<long> alreadyCommanding)
        => Active(player).Where(x => !alreadyCommanding.Contains(x.Id)).ToList();

    /// <summary>
    /// Resolves who leads an attack. A requested pimp must be theirs, alive, and not already out;
    /// with no request the best Enforcer leads, falling back to the steadiest hand.
    /// </summary>
    public Pimp? ChooseCommander(Player player, IReadOnlyCollection<long> alreadyCommanding, long? requestedPimpId = null)
    {
        var available = AvailableCommanders(player, alreadyCommanding);
        if (requestedPimpId is { } requested)
            return available.FirstOrDefault(x => x.Id == requested)
                ?? throw new GameRuleException("That pimp is not available to command this attack.");

        return available
            .OrderByDescending(x => x.Specialty == PimpSpecialties.Enforcer)
            .ThenByDescending(x => x.BonusPercent)
            .ThenByDescending(x => x.Loyalty)
            .FirstOrDefault();
    }

    /// <summary>Stacked Hustler bonus from pimps at home, as a percent added to street gross.</summary>
    public int StreetBonusPercent(Player player, IReadOnlyCollection<long> away)
        => Math.Min(
            Math.Max(0, _options.MaxStreetBonusPercent),
            Active(player).Where(x => x.Specialty == PimpSpecialties.Hustler && !away.Contains(x.Id)).Sum(x => x.BonusPercent));

    /// <summary>Stacked Enforcer bonus from pimps at home, as a percent added to defence power.</summary>
    public int DefenceBonusPercent(Player player, IReadOnlyCollection<long> away)
        => Math.Min(
            Math.Max(0, _options.MaxDefenceBonusPercent),
            Active(player).Where(x => x.Specialty == PimpSpecialties.Enforcer && !away.Contains(x.Id)).Sum(x => x.BonusPercent));

    /// <summary>Settles a mission's effect on its commander, killing them off on a bad enough day.</summary>
    public PimpOutcome SettleMission(Player attacker, Pimp? commander, string outcome, DateTime nowUtc)
    {
        if (commander is null || !commander.IsActive)
            return PimpOutcome.None;

        commander.MissionsLed++;
        switch (outcome)
        {
            case "Victory":
                commander.Victories++;
                commander.Loyalty = Clamp(commander.Loyalty + _options.VictoryLoyaltyGain);
                return PimpOutcome.None;
            case "Defeat":
                commander.Loyalty = Clamp(commander.Loyalty - _options.DefeatLoyaltyPenalty);
                if (random.NextDouble() < Math.Clamp(_options.CommanderDeathChanceOnDefeat, 0, 1))
                {
                    Kill(attacker, commander, "Killed in action", nowUtc);
                    return new PimpOutcome(commander.Name, "Killed in action");
                }

                return PimpOutcome.None;
            case "Standstill":
                commander.Loyalty = Clamp(commander.Loyalty - _options.StandstillLoyaltyPenalty);
                return PimpOutcome.None;
            default:
                return PimpOutcome.None;
        }
    }

    /// <summary>A broken defence can cost a pimp who was at home holding the house.</summary>
    public PimpOutcome SettleBrokenDefence(Player defender, IReadOnlyCollection<long> awayCommanding, DateTime nowUtc)
    {
        if (random.NextDouble() >= Math.Clamp(_options.DefenderDeathChanceOnLoss, 0, 1))
            return PimpOutcome.None;

        // Never take the last pimp: a player with none could not command an attack again.
        var atHome = Active(defender).Where(x => !awayCommanding.Contains(x.Id)).ToList();
        if (atHome.Count == 0 || Active(defender).Count <= 1)
            return PimpOutcome.None;

        var lost = atHome[random.NextInclusive(0, atHome.Count - 1)];
        Kill(defender, lost, "Killed defending", nowUtc);
        return new PimpOutcome(lost.Name, "Killed defending");
    }

    /// <summary>
    /// Street work grinds on loyalty while the crew is unhappy, and an unhappy pimp may walk. Never
    /// takes the last one, so the player is not locked out of attacking entirely.
    /// </summary>
    public IReadOnlyList<PimpOutcome> SettleStreetWork(Player player, int turns, double crewMorale, DateTime nowUtc)
    {
        if (crewMorale < _options.LowMoraleThreshold)
        {
            var drop = _options.LowMoraleLoyaltyPenaltyPerTurn * turns;
            foreach (var pimp in Active(player))
                pimp.Loyalty = Clamp(pimp.Loyalty - drop);
        }

        var walked = new List<PimpOutcome>();
        foreach (var pimp in Active(player))
        {
            if (Active(player).Count <= 1)
                break;
            if (pimp.Loyalty >= _options.WalkOutThreshold)
                continue;

            var chance = Math.Min(
                Math.Clamp(_options.MaxWalkOutChance, 0, 1),
                (_options.WalkOutThreshold - pimp.Loyalty) / 100.0);
            if (random.NextDouble() >= chance)
                continue;

            pimp.LostAtUtc = nowUtc;
            pimp.LostReason = "Walked out";
            walked.Add(new PimpOutcome(pimp.Name, "Walked out"));
        }

        player.Pimps = Active(player).Count;
        return walked;
    }

    public void Recover(Player player, double amount)
    {
        foreach (var pimp in Active(player))
            pimp.Loyalty = Clamp(pimp.Loyalty + amount);
    }

    public double PassiveRecoveryPerTick => Math.Max(0, _options.PassiveRecoveryPerTick);
    public double RestRecovery => _options.RestRecovery;
    public double PartyRecovery => _options.PartyRecovery;

    /// <summary>Brings the roster in line with a counter that was changed directly, e.g. admin cheats.</summary>
    public void Reconcile(Player player, DateTime nowUtc)
    {
        var active = Active(player).Count;
        if (player.Pimps > active)
            Hire(player, player.Pimps - active, nowUtc);
        else if (player.Pimps < active)
            Release(player, active - player.Pimps, "Moved on", nowUtc);
    }

    private void Kill(Player player, Pimp pimp, string reason, DateTime nowUtc)
    {
        pimp.LostAtUtc = nowUtc;
        pimp.LostReason = reason;
        player.Pimps = Active(player).Count;
    }

    private string NextName(Player player)
    {
        var taken = player.Crew.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var pool = StreetNames.Where(x => !taken.Contains(x)).ToList();
        if (pool.Count > 0)
            return pool[random.NextInclusive(0, pool.Count - 1)];

        // Every name is spoken for, including by the fallen, so number a repeat rather than collide.
        var fallback = StreetNames[random.NextInclusive(0, StreetNames.Count - 1)];
        for (var suffix = 2; ; suffix++)
        {
            var candidate = $"{fallback} {suffix}";
            if (!taken.Contains(candidate))
                return candidate;
        }
    }

    private static double Clamp(double value) => Math.Round(Math.Clamp(value, 0, 100), 2);

    private static readonly IReadOnlyList<string> StreetNames =
    [
        "Lil Daddy", "Silk Reno", "Big Cassius", "Papa Lux", "Slim Osgood", "Duke Mercer",
        "Fat Solomon", "Smooth Ellis", "Baby Vaughn", "Sweet Lorenzo", "King Ambrose", "Cool Rufus",
        "Sly Beaumont", "Money Grant", "Velvet Otis", "Ace Dupree", "Prince Hollis", "Slick Barnaby",
        "Deacon Ray", "Cadillac Moss", "Gator Pike", "Bishop Vane", "Fingers Malone", "Diamond Tate",
        "Choppa Reed", "Nickel Ford", "Preacher Boyd", "Tiny Marcel", "Loud Winston", "Cousin Hark"
    ];
}

/// <summary>A pimp leaving the roster, for reporting in an action summary.</summary>
public sealed record PimpOutcome(string? Name, string? Reason)
{
    public static readonly PimpOutcome None = new(null, null);

    public bool Happened => Name is not null;
}
