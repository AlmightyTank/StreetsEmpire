using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

public sealed class CombatMissionService(GameDbContext db, IOptions<GameOptions> options, IGameRandom random)
{
    // Shared so the cancel path and the lane query cannot drift apart on the spelling.
    private const string CanceledOutcome = "Canceled";

    private readonly GameOptions _options = options.Value;

    public async Task<CombatMission> LaunchAsync(Player attacker, Player defender, CombatAttackRequest request, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var combat = _options.Combat;
        var activeMissions = await ActiveAttackMissions(attacker.Id)
            .ToListAsync(cancellationToken);
        var committed = CombatCommitment.From(attacker, activeMissions, combat.MaxActiveAttackMissions);
        var laneReadyAt = await LaneReadyAtUtcAsync(attacker.Id, nowUtc, cancellationToken);

        ValidateLaunch(attacker, defender, request, nowUtc, committed, combat, laneReadyAt);

        var travelSeconds = RollSeconds(combat.AttackTravelSecondsMin, combat.AttackTravelSecondsMax);
        var arrivesAt = nowUtc.AddSeconds(travelSeconds);
        var attackerMorale = AverageMorale(attacker);
        var defenderMorale = Math.Min(100, AverageMorale(defender) + 5);
        var summary = $"{attacker.Name} sent {request.Pimps:N0} pimp(s), {request.Thugs:N0} thug(s), and {request.Weapons:N0} weapon(s) toward {defender.Name}.";

        attacker.Turns -= combat.AttackTurnCost;
        attacker.LastAttackAtUtc = nowUtc;

        var mission = new CombatMission
        {
            AttackerId = attacker.Id,
            Attacker = attacker,
            DefenderId = defender.Id,
            Defender = defender,
            Status = "Traveling",
            Outcome = "Pending",
            Summary = summary,
            TurnsSpent = combat.AttackTurnCost,
            AssignedPimps = request.Pimps,
            AssignedThugs = request.Thugs,
            AssignedWeapons = request.Weapons,
            RemainingAttackers = request.Thugs,
            RemainingWeapons = request.Weapons,
            AttackerMorale = attackerMorale,
            DefenderMorale = defenderMorale,
            MaxRounds = Math.Clamp(combat.MaxFightRounds, 1, 20),
            StartedAtUtc = nowUtc,
            ArrivesAtUtc = arrivesAt,
            Events =
            [
                new CombatMissionEvent
                {
                    Kind = "Launch",
                    Summary = $"{attacker.Name}'s crew left for {defender.Name}. Arrival in {FormatDuration(travelSeconds)}.",
                    AttackerMorale = attackerMorale,
                    DefenderMorale = defenderMorale,
                    CreatedAtUtc = nowUtc
                }
            ]
        };

        db.CombatMissions.Add(mission);
        return mission;
    }

    public async Task<int> ResolveDueAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var missions = await db.CombatMissions
            .Include(x => x.Attacker)
            .Include(x => x.Defender)
            .Include(x => x.Events)
            .Where(x => x.Status != "Complete")
            .OrderBy(x => x.ArrivesAtUtc)
            .ThenBy(x => x.Id)
            .Take(50)
            .ToListAsync(cancellationToken);

        var updates = 0;
        foreach (var mission in missions)
        {
            if (mission.Status == "Traveling" && mission.ArrivesAtUtc <= nowUtc)
            {
                Arrive(mission, nowUtc);
                updates++;
            }

            var roundsThisPass = 0;
            while (mission.Status == "Fighting" && mission.NextRoundAtUtc <= nowUtc && roundsThisPass < 6)
            {
                await ResolveRoundAsync(mission, nowUtc, cancellationToken);
                roundsThisPass++;
                updates++;
            }

            if (mission.Status == "Returning" && mission.ReturnsAtUtc <= nowUtc)
            {
                Complete(mission, nowUtc);
                updates++;
            }
        }

        if (updates > 0)
            await db.SaveChangesAsync(cancellationToken);

        return updates;
    }

    public IQueryable<CombatMission> VisibleMissions(Guid playerId)
        => db.CombatMissions.AsNoTracking()
            .Include(x => x.Attacker)
            .Include(x => x.Defender)
            .Include(x => x.Events.OrderByDescending(e => e.CreatedAtUtc).ThenByDescending(e => e.Id))
            .Where(x => x.AttackerId == playerId || x.DefenderId == playerId)
            .OrderBy(x => x.Status == "Complete")
            .ThenByDescending(x => x.StartedAtUtc)
            .Take(30);

    public async Task<CombatCommitment> CommitmentAsync(Player player, CancellationToken cancellationToken)
    {
        var activeMissions = await ActiveAttackMissions(player.Id).ToListAsync(cancellationToken);
        return CombatCommitment.From(player, activeMissions, _options.Combat.MaxActiveAttackMissions);
    }

    public IQueryable<CombatMission> ActiveAttackMissions(Guid playerId)
        => db.CombatMissions.AsNoTracking()
            .Where(x => x.AttackerId == playerId && x.Status != "Complete");

    public async Task<CombatMissionCancelResult> CancelAsync(Player attacker, long missionId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var mission = await db.CombatMissions
            .Include(x => x.Attacker)
            .Include(x => x.Defender)
            .Include(x => x.Events)
            .SingleOrDefaultAsync(x => x.Id == missionId, cancellationToken);

        if (mission is null)
            throw new GameRuleException("Mission not found.");
        if (mission.AttackerId != attacker.Id)
            throw new GameRuleException("You can only cancel your own attack missions.");
        if (mission.Status == "Complete")
            throw new GameRuleException("That mission is already complete.");
        if (mission.Status == "Returning")
            throw new GameRuleException("That fight is already over and the crew is returning.");

        var cost = CancelCashCost(mission);
        if (attacker.Cash < cost)
            throw new GameRuleException($"You need ${cost:N0} cash on hand to cancel this mission.");

        attacker.Cash -= cost;
        mission.Status = "Complete";
        mission.Outcome = CanceledOutcome;
        mission.CompletedAtUtc = nowUtc;
        mission.ReturnsAtUtc = nowUtc;
        mission.NextRoundAtUtc = null;
        mission.Summary = $"{mission.Attacker.Name} paid ${cost:N0} to call off the attack on {mission.Defender.Name}.";

        if (mission.CurrentRound > 0)
        {
            var combat = _options.Combat;
            mission.Defender.LastAttackedAtUtc = nowUtc;
            mission.DefenderProtectionUntilUtc = nowUtc.AddMinutes(Math.Max(1, combat.DefenderProtectionMinutes / 2));
            mission.Defender.CombatProtectionUntilUtc = mission.DefenderProtectionUntilUtc;
        }

        mission.Events.Add(new CombatMissionEvent
        {
            Round = mission.CurrentRound,
            Kind = "Cancel",
            Summary = mission.Summary,
            AttackerMorale = Math.Round(mission.AttackerMorale, 2),
            DefenderMorale = Math.Round(mission.DefenderMorale, 2),
            CreatedAtUtc = nowUtc
        });

        db.CombatLogs.Add(new CombatLog
        {
            AttackerId = mission.AttackerId,
            DefenderId = mission.DefenderId,
            Outcome = mission.Outcome,
            Summary = mission.Summary,
            TurnsSpent = mission.TurnsSpent,
            AttackerPower = mission.AttackerPower,
            DefenderPower = mission.DefenderPower,
            CashStolen = mission.CashStolen,
            WeedStolen = mission.WeedStolen,
            CokeStolen = mission.CokeStolen,
            AttackerPimpsLost = mission.AttackerPimpsLost,
            AttackerHoesLost = mission.AttackerHoesLost,
            AttackerThugsLost = mission.AttackerThugsLost,
            AttackerWeaponsLost = mission.AttackerWeaponsLost,
            DefenderPimpsLost = mission.DefenderPimpsLost,
            DefenderHoesLost = mission.DefenderHoesLost,
            DefenderThugsLost = mission.DefenderThugsLost,
            DefenderWeaponsLost = mission.DefenderWeaponsLost,
            DefenderProtectionUntilUtc = mission.DefenderProtectionUntilUtc,
            ResolvesAtUtc = nowUtc,
            ResolvedAtUtc = nowUtc,
            CreatedAtUtc = nowUtc
        });

        db.ActionLogs.Add(new GameActionLog
        {
            PlayerId = mission.AttackerId,
            Action = "ATTACK",
            TurnsSpent = 0,
            CashDelta = -cost,
            BankDelta = 0,
            Summary = mission.Summary,
            CreatedAtUtc = nowUtc
        });

        await db.SaveChangesAsync(cancellationToken);
        return new CombatMissionCancelResult(mission, cost);
    }

    public static long CancelCashCost(CombatMission mission)
    {
        var baseCost = Math.Max(1_000L, mission.AssignedPimps * 500L + mission.RemainingAttackers * 150L + mission.RemainingWeapons * 75L);
        return mission.Status == "Fighting" ? baseCost * 2 : baseCost;
    }

    public static bool CanCancel(CombatMission mission)
        => mission.Status is "Traveling" or "Fighting";

    /// <summary>
    /// When an attack lane next frees up, or null if one is free now. Each launch listed holds one
    /// of the attacker's lanes for the whole cooldown window.
    /// </summary>
    public static DateTime? LaneReadyAtUtc(IReadOnlyList<DateTime> launchesNewestFirst, int lanes, int cooldownMinutes)
    {
        var laneCount = Math.Max(1, lanes);
        var cooldown = Math.Max(0, cooldownMinutes);
        return cooldown > 0 && launchesNewestFirst.Count >= laneCount
            ? launchesNewestFirst[laneCount - 1].AddMinutes(cooldown)
            : null;
    }

    /// <summary>
    /// Reads the attacker's launches inside the current cooldown window to find the next free lane.
    /// Canceled missions release their lane, so they are not counted.
    /// </summary>
    public async Task<DateTime?> LaneReadyAtUtcAsync(Guid attackerId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var combat = _options.Combat;
        var lanes = Math.Max(1, combat.MaxActiveAttackMissions);
        var cooldown = Math.Max(0, combat.AttackCooldownMinutes);
        if (cooldown == 0)
            return null;

        var windowStart = nowUtc.AddMinutes(-cooldown);
        var launches = await db.CombatMissions.AsNoTracking()
            .Where(x => x.AttackerId == attackerId && x.StartedAtUtc > windowStart && x.Outcome != CanceledOutcome)
            .OrderByDescending(x => x.StartedAtUtc)
            .Take(lanes)
            .Select(x => x.StartedAtUtc)
            .ToListAsync(cancellationToken);

        return LaneReadyAtUtc(launches, lanes, cooldown);
    }

    private void ValidateLaunch(Player attacker, Player defender, CombatAttackRequest request, DateTime nowUtc, CombatCommitment committed, CombatOptions combat, DateTime? laneReadyAt)
    {
        if (attacker.Id == defender.Id)
            throw new GameRuleException("You cannot attack yourself.");
        if (attacker.Turns < combat.AttackTurnCost)
            throw new GameRuleException($"You need {combat.AttackTurnCost:N0} turns to attack.");
        if (laneReadyAt is { } readyAt && readyAt > nowUtc)
            throw new GameRuleException($"All {committed.MaxActiveAttackMissions:N0} attack lane(s) are cooling down. The next one frees in {FormatDuration((int)Math.Ceiling((readyAt - nowUtc).TotalSeconds))}.");
        if (committed.ActiveAttackMissions >= committed.MaxActiveAttackMissions)
            throw new GameRuleException($"You already have {committed.ActiveAttackMissions:N0} active attack mission(s).");
        if (request.Pimps < 1)
            throw new GameRuleException("Each attack needs at least one pimp in control.");
        if (request.Thugs < 1)
            throw new GameRuleException("Each attack needs at least one thug.");
        if (request.Weapons < 0 || request.Weapons > request.Thugs)
            throw new GameRuleException("Weapons sent must be between zero and the number of thugs sent.");
        if (request.Pimps > committed.AvailablePimps)
            throw new GameRuleException("You do not have that many available pimps.");
        if (request.Thugs > committed.AvailableThugs)
            throw new GameRuleException("You do not have that many available thugs.");
        if (request.Weapons > committed.AvailableWeapons)
            throw new GameRuleException("You do not have that many available weapons.");
        if (defender.CombatProtectionUntilUtc is { } protectionUntil && protectionUntil > nowUtc)
            throw new GameRuleException($"{defender.Name} is under combat protection.");
        if (defender.Pimps + defender.Hoes + defender.Thugs <= 0 && defender.Cash <= 0 && defender.Weed <= 0 && defender.Coke <= 0)
            throw new GameRuleException($"{defender.Name} has nothing worth attacking right now.");
    }

    private void Arrive(CombatMission mission, DateTime nowUtc)
    {
        mission.Status = "Fighting";
        mission.NextRoundAtUtc = nowUtc;
        mission.Summary = $"{mission.Attacker.Name}'s crew reached {mission.Defender.Name}. The fight is starting.";
        mission.Events.Add(new CombatMissionEvent
        {
            Kind = "Arrive",
            Summary = mission.Summary,
            AttackerMorale = mission.AttackerMorale,
            DefenderMorale = mission.DefenderMorale,
            CreatedAtUtc = nowUtc
        });
    }

    private async Task ResolveRoundAsync(CombatMission mission, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var combat = _options.Combat;
        var defenderCommitted = await ActiveAttackMissions(mission.DefenderId)
            .Select(x => new { x.AssignedPimps, x.RemainingAttackers, x.RemainingWeapons })
            .ToListAsync(cancellationToken);
        var defenderHomePimps = Math.Max(0, mission.Defender.Pimps - defenderCommitted.Sum(x => x.AssignedPimps));
        var defenderHomeThugs = Math.Max(0, mission.Defender.Thugs - defenderCommitted.Sum(x => x.RemainingAttackers));
        var defenderHomeWeapons = Math.Max(0, mission.Defender.Weapons - defenderCommitted.Sum(x => x.RemainingWeapons));

        mission.CurrentRound++;
        var attackerPower = AttackPower(mission.AssignedPimps, mission.RemainingAttackers, mission.RemainingWeapons, mission.AttackerMorale);
        var defenderPower = DefensePower(defenderHomePimps, defenderHomeThugs, defenderHomeWeapons, mission.DefenderMorale);
        var attackRoll = ApplyPowerVariance(attackerPower, combat.PowerRandomnessPercent);
        var defenseRoll = ApplyPowerVariance(defenderPower, combat.PowerRandomnessPercent);
        var difference = attackRoll - defenseRoll;
        var close = Math.Abs(difference) <= Math.Max(8, Math.Max(attackRoll, defenseRoll) * 0.10);

        var attackerLosses = 0;
        var defenderLosses = 0;
        var attackerWeaponLosses = 0;
        var defenderWeaponLosses = 0;
        string summary;

        if (close)
        {
            var moraleLoss = random.NextInclusive(6, 12);
            mission.AttackerMorale = ClampMorale(mission.AttackerMorale - moraleLoss);
            mission.DefenderMorale = ClampMorale(mission.DefenderMorale - moraleLoss);
            summary = $"Round {mission.CurrentRound}: both crews trade pressure and neither side breaks.";
        }
        else if (difference > 0)
        {
            var defenderMoraleLoss = random.NextInclusive(12, 22);
            var attackerMoraleLoss = random.NextInclusive(4, 9);
            defenderLosses = LossCount(defenderHomeThugs, 0.06);
            defenderWeaponLosses = Math.Min(LossCount(defenderHomeWeapons, 0.04), defenderLosses + 1);
            mission.DefenderMorale = ClampMorale(mission.DefenderMorale - defenderMoraleLoss);
            mission.AttackerMorale = ClampMorale(mission.AttackerMorale - attackerMoraleLoss);
            ApplyDefenderLosses(mission, defenderLosses, defenderWeaponLosses);
            summary = $"Round {mission.CurrentRound}: attackers push through. Defender morale drops and {defenderLosses:N0} thug(s) fall.";
        }
        else
        {
            var attackerMoraleLoss = random.NextInclusive(12, 22);
            var defenderMoraleLoss = random.NextInclusive(4, 9);
            attackerLosses = LossCount(mission.RemainingAttackers, 0.06);
            attackerWeaponLosses = Math.Min(LossCount(mission.RemainingWeapons, 0.04), attackerLosses + 1);
            mission.AttackerMorale = ClampMorale(mission.AttackerMorale - attackerMoraleLoss);
            mission.DefenderMorale = ClampMorale(mission.DefenderMorale - defenderMoraleLoss);
            ApplyAttackerLosses(mission, attackerLosses, attackerWeaponLosses);
            summary = $"Round {mission.CurrentRound}: defenders hold hard. Attacker morale drops and {attackerLosses:N0} thug(s) fall.";
        }

        mission.AttackerPower = attackerPower;
        mission.DefenderPower = defenderPower;
        mission.Events.Add(new CombatMissionEvent
        {
            Round = mission.CurrentRound,
            Kind = "Round",
            Summary = summary,
            AttackRoll = Math.Round(attackRoll, 2),
            DefenseRoll = Math.Round(defenseRoll, 2),
            AttackerMorale = Math.Round(mission.AttackerMorale, 2),
            DefenderMorale = Math.Round(mission.DefenderMorale, 2),
            AttackerThugsLost = attackerLosses,
            DefenderThugsLost = defenderLosses,
            AttackerWeaponsLost = attackerWeaponLosses,
            DefenderWeaponsLost = defenderWeaponLosses,
            CreatedAtUtc = nowUtc
        });

        if (mission.AttackerMorale <= combat.MoraleBreakThreshold || mission.RemainingAttackers <= 0)
        {
            BeginReturn(mission, "Defeat", $"{mission.Attacker.Name}'s crew broke and started heading home.", nowUtc);
        }
        else if (mission.DefenderMorale <= combat.MoraleBreakThreshold || defenderHomeThugs <= defenderLosses)
        {
            ApplyLoot(mission);
            BeginReturn(mission, "Victory", $"{mission.Attacker.Name}'s crew broke {mission.Defender.Name}'s defense and grabbed ${mission.CashStolen:N0}, {mission.WeedStolen:N0} weed, and {mission.CokeStolen:N0} coke.", nowUtc);
        }
        else if (mission.CurrentRound >= mission.MaxRounds)
        {
            BeginReturn(mission, "Standstill", $"{mission.Attacker.Name}'s attack stalled after {mission.CurrentRound:N0} round(s). Both sides disengaged.", nowUtc);
        }
        else
        {
            mission.NextRoundAtUtc = nowUtc.AddSeconds(Math.Clamp(combat.FightRoundSeconds, 5, 300));
            mission.Summary = summary;
        }
    }

    private void BeginReturn(CombatMission mission, string outcome, string summary, DateTime nowUtc)
    {
        var combat = _options.Combat;
        var returnSeconds = RollSeconds(combat.ReturnTravelSecondsMin, combat.ReturnTravelSecondsMax);
        mission.Status = "Returning";
        mission.Outcome = outcome;
        mission.Summary = summary;
        mission.NextRoundAtUtc = null;
        mission.ReturnsAtUtc = nowUtc.AddSeconds(returnSeconds);
        mission.Defender.LastAttackedAtUtc = nowUtc;
        mission.DefenderProtectionUntilUtc = nowUtc.AddMinutes(Math.Max(1, combat.DefenderProtectionMinutes));
        mission.Defender.CombatProtectionUntilUtc = mission.DefenderProtectionUntilUtc;
        mission.Events.Add(new CombatMissionEvent
        {
            Round = mission.CurrentRound,
            Kind = outcome,
            Summary = $"{summary} Return time: {FormatDuration(returnSeconds)}.",
            AttackerMorale = Math.Round(mission.AttackerMorale, 2),
            DefenderMorale = Math.Round(mission.DefenderMorale, 2),
            CreatedAtUtc = nowUtc
        });
    }

    private void Complete(CombatMission mission, DateTime nowUtc)
    {
        ApplyMoraleAftermath(mission);
        mission.Status = "Complete";
        mission.CompletedAtUtc = nowUtc;
        mission.Summary = $"{mission.Attacker.Name}'s crew returned from {mission.Defender.Name}: {mission.Outcome}.";
        mission.Events.Add(new CombatMissionEvent
        {
            Round = mission.CurrentRound,
            Kind = "Return",
            Summary = mission.Summary,
            AttackerMorale = Math.Round(mission.AttackerMorale, 2),
            DefenderMorale = Math.Round(mission.DefenderMorale, 2),
            CreatedAtUtc = nowUtc
        });

        db.CombatLogs.Add(new CombatLog
        {
            AttackerId = mission.AttackerId,
            DefenderId = mission.DefenderId,
            Outcome = mission.Outcome,
            Summary = mission.Summary,
            TurnsSpent = mission.TurnsSpent,
            AttackerPower = mission.AttackerPower,
            DefenderPower = mission.DefenderPower,
            CashStolen = mission.CashStolen,
            WeedStolen = mission.WeedStolen,
            CokeStolen = mission.CokeStolen,
            AttackerPimpsLost = mission.AttackerPimpsLost,
            AttackerHoesLost = mission.AttackerHoesLost,
            AttackerThugsLost = mission.AttackerThugsLost,
            AttackerWeaponsLost = mission.AttackerWeaponsLost,
            DefenderPimpsLost = mission.DefenderPimpsLost,
            DefenderHoesLost = mission.DefenderHoesLost,
            DefenderThugsLost = mission.DefenderThugsLost,
            DefenderWeaponsLost = mission.DefenderWeaponsLost,
            DefenderProtectionUntilUtc = mission.DefenderProtectionUntilUtc,
            ResolvesAtUtc = mission.ReturnsAtUtc,
            ResolvedAtUtc = nowUtc,
            CreatedAtUtc = nowUtc
        });

        AddActionLog(mission, mission.Outcome == "Victory"
            ? $"{mission.Attacker.Name}'s crew returned from {mission.Defender.Name} with ${mission.CashStolen:N0}, {mission.WeedStolen:N0} weed, and {mission.CokeStolen:N0} coke."
            : mission.Summary,
            nowUtc);
    }

    private void ApplyMoraleAftermath(CombatMission mission)
    {
        var combat = _options.Combat;
        switch (mission.Outcome)
        {
            case "Victory":
                mission.Attacker.ThugHappiness = ClampMorale(mission.Attacker.ThugHappiness + combat.AttackerVictoryThugMoraleGain);
                mission.Attacker.HoeHappiness = ClampMorale(mission.Attacker.HoeHappiness + combat.AttackerVictoryHoeMoraleGain);
                mission.Defender.ThugHappiness = ClampMorale(Math.Min(
                    mission.Defender.ThugHappiness - combat.DefenderDefeatThugMoralePenalty,
                    mission.DefenderMorale));
                mission.Defender.HoeHappiness = ClampMorale(mission.Defender.HoeHappiness - combat.DefenderDefeatHoeMoralePenalty);
                break;
            case "Defeat":
                mission.Attacker.ThugHappiness = ClampMorale(Math.Min(
                    mission.Attacker.ThugHappiness - combat.AttackerDefeatThugMoralePenalty,
                    mission.AttackerMorale));
                mission.Attacker.HoeHappiness = ClampMorale(mission.Attacker.HoeHappiness - combat.AttackerDefeatHoeMoralePenalty);
                mission.Defender.ThugHappiness = ClampMorale(mission.Defender.ThugHappiness + combat.DefenderVictoryThugMoraleGain);
                break;
            case "Standstill":
                mission.Attacker.ThugHappiness = ClampMorale(Math.Min(
                    mission.Attacker.ThugHappiness - combat.AttackerStandstillThugMoralePenalty,
                    mission.AttackerMorale + 10));
                mission.Defender.ThugHappiness = ClampMorale(Math.Min(
                    mission.Defender.ThugHappiness - 1,
                    mission.DefenderMorale + 10));
                break;
        }
    }

    private void AddActionLog(CombatMission mission, string summary, DateTime nowUtc)
    {
        db.ActionLogs.Add(new GameActionLog
        {
            PlayerId = mission.AttackerId,
            Action = "ATTACK",
            TurnsSpent = 0,
            CashDelta = mission.CashStolen,
            BankDelta = 0,
            PimpsDelta = 0,
            HoesDelta = 0,
            ThugsDelta = -mission.AttackerThugsLost,
            WeaponsDelta = -mission.AttackerWeaponsLost,
            WeedDelta = mission.WeedStolen,
            CokeDelta = mission.CokeStolen,
            Summary = summary,
            CreatedAtUtc = nowUtc
        });
    }

    private void ApplyLoot(CombatMission mission)
    {
        var combat = _options.Combat;
        mission.CashStolen = LootCash(mission.Defender.Cash, combat.MinCashLootPercent, combat.MaxCashLootPercent);
        mission.WeedStolen = LootProduct(mission.Defender.Weed, combat.MinProductLootPercent, combat.MaxProductLootPercent);
        mission.CokeStolen = LootProduct(mission.Defender.Coke, combat.MinProductLootPercent, combat.MaxProductLootPercent);
        mission.Defender.Cash -= mission.CashStolen;
        mission.Attacker.Cash += mission.CashStolen;
        mission.Defender.Weed -= mission.WeedStolen;
        mission.Attacker.Weed += mission.WeedStolen;
        mission.Defender.Coke -= mission.CokeStolen;
        mission.Attacker.Coke += mission.CokeStolen;
    }

    private void ApplyAttackerLosses(CombatMission mission, int thugsLost, int weaponsLost)
    {
        thugsLost = Math.Min(thugsLost, mission.RemainingAttackers);
        weaponsLost = Math.Min(weaponsLost, mission.RemainingWeapons);
        mission.RemainingAttackers -= thugsLost;
        mission.RemainingWeapons -= weaponsLost;
        mission.Attacker.Thugs = Math.Max(0, mission.Attacker.Thugs - thugsLost);
        mission.Attacker.Weapons = Math.Max(0, mission.Attacker.Weapons - weaponsLost);
        mission.AttackerThugsLost += thugsLost;
        mission.AttackerWeaponsLost += weaponsLost;
    }

    private void ApplyDefenderLosses(CombatMission mission, int thugsLost, int weaponsLost)
    {
        mission.Defender.Thugs = Math.Max(0, mission.Defender.Thugs - thugsLost);
        mission.Defender.Weapons = Math.Max(0, mission.Defender.Weapons - weaponsLost);
        mission.DefenderThugsLost += thugsLost;
        mission.DefenderWeaponsLost += weaponsLost;
    }

    private int AttackPower(int pimps, int thugs, int weapons, double morale)
    {
        var armedThugs = Math.Min(weapons, thugs);
        return Math.Max(1, thugs * 12 + armedThugs * 8 + pimps * 2 + (int)Math.Round(morale / 2));
    }

    private int DefensePower(int pimps, int thugs, int weapons, double morale)
    {
        var armedThugs = Math.Min(weapons, thugs);
        return Math.Max(1, thugs * 14 + armedThugs * 10 + pimps * 3 + (int)Math.Round(morale));
    }

    private double ApplyPowerVariance(int power, double randomnessPercent)
    {
        var variance = Math.Clamp(randomnessPercent, 0, 0.5);
        var multiplier = 1 - variance + random.NextDouble() * variance * 2;
        return Math.Max(1, power * multiplier);
    }

    private int LossCount(int count, double rate)
    {
        if (count <= 0) return 0;
        var max = Math.Max(1, (int)Math.Ceiling(count * rate));
        var losses = 0;
        for (var i = 0; i < max; i++)
            if (random.NextDouble() < 0.55) losses++;
        return Math.Min(count, losses);
    }

    private long LootCash(long cash, double minPercent, double maxPercent)
    {
        if (cash <= 0) return 0;
        var percent = RollPercent(minPercent, maxPercent);
        if (percent <= 0) return 0;
        return Math.Min(cash, Math.Max(1, (long)Math.Floor(cash * percent)));
    }

    private int LootProduct(int product, double minPercent, double maxPercent)
    {
        if (product <= 0) return 0;
        var percent = RollPercent(minPercent, maxPercent);
        if (percent <= 0) return 0;
        return Math.Min(product, Math.Max(1, (int)Math.Floor(product * percent)));
    }

    private double RollPercent(double minPercent, double maxPercent)
    {
        var min = Math.Clamp(minPercent, 0, 1);
        var max = Math.Clamp(maxPercent, min, 1);
        return min + random.NextDouble() * (max - min);
    }

    private int RollSeconds(int minSeconds, int maxSeconds)
    {
        var min = Math.Clamp(minSeconds, 10, 3600);
        var max = Math.Clamp(Math.Max(minSeconds, maxSeconds), 10, 3600);
        return random.NextInclusive(min, max);
    }

    private static double AverageMorale(Player player)
        => Math.Round((player.HoeHappiness + player.ThugHappiness) / 2, 2);

    private static double ClampMorale(double value)
        => Math.Round(Math.Clamp(value, 0, 100), 2);

    private static string FormatDuration(int seconds)
    {
        var minutes = seconds / 60;
        var remainder = seconds % 60;
        return minutes <= 0
            ? $"{seconds} second{(seconds == 1 ? string.Empty : "s")}"
            : $"{minutes}m {remainder:00}s";
    }
}

public sealed record CombatCommitment(
    int CommittedPimps,
    int CommittedThugs,
    int CommittedWeapons,
    int AvailablePimps,
    int AvailableThugs,
    int AvailableWeapons,
    int ActiveAttackMissions,
    int MaxActiveAttackMissions)
{
    public static CombatCommitment From(Player player, IReadOnlyCollection<CombatMission> activeMissions, int maxActiveMissions)
    {
        var committedPimps = activeMissions.Sum(x => x.AssignedPimps);
        var committedThugs = activeMissions.Sum(x => x.RemainingAttackers);
        var committedWeapons = activeMissions.Sum(x => x.RemainingWeapons);
        return new CombatCommitment(
            committedPimps,
            committedThugs,
            committedWeapons,
            Math.Max(0, player.Pimps - committedPimps),
            Math.Max(0, player.Thugs - committedThugs),
            Math.Max(0, player.Weapons - committedWeapons),
            activeMissions.Count,
            Math.Max(1, maxActiveMissions));
    }
}

public sealed record CombatMissionCancelResult(CombatMission Mission, long Cost);
