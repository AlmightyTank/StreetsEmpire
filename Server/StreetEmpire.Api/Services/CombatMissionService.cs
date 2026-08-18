using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

public sealed class CombatMissionService(
    GameDbContext db,
    IOptionsSnapshot<GameOptions> options,
    IGameRandom random,
    HideoutService hideout,
    CombatSchedule schedule,
    PimpRoster pimps,
    EconomyService economy,
    TerritoryService territories,
    AllianceService alliances)
{
    // Shared so the cancel path and the lane query cannot drift apart on the spelling.
    private const string CanceledOutcome = "Canceled";

    /// <summary>One pimp commands each attack, like a general. Sending more adds nothing.</summary>
    public const int CommandingPimps = 1;

    private readonly GameOptions _options = options.Value;

    public Task<CombatMission> LaunchAsync(Player attacker, Player defender, CombatAttackRequest request, DateTime nowUtc, CancellationToken cancellationToken)
        => LaunchAsync(attacker, defender, request, null, nowUtc, cancellationToken);

    /// <param name="ground">
    /// Set for a raid on held ground rather than a house. The holder is still the defender and every
    /// rule still applies except the wealth ratio: taking a corner is not robbing anyone, and gating it
    /// by wealth would let a weak player park on good ground permanently.
    /// </param>
    public async Task<CombatMission> LaunchAsync(
        Player attacker,
        Player defender,
        CombatAttackRequest request,
        Territory? ground,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        TravelGate.EnsureLanded(attacker);

        var combat = _options.Combat;
        var activeMissions = await ActiveAttackMissions(attacker.Id)
            .ToListAsync(cancellationToken);
        var committed = CombatCommitment.From(attacker, activeMissions, combat.MaxActiveAttackMissions);
        var laneReadyAt = await LaneReadyAtUtcAsync(attacker.Id, nowUtc, cancellationToken);

        ValidateLaunch(attacker, defender, request, ground, nowUtc, committed, combat, laneReadyAt);

        if (ground is not null)
        {
            if (!TerritoryService.SameCity(attacker, ground))
                throw new GameRuleException($"{ground.Name} is in {ground.City}. You run {attacker.City}.");
            if (ground.HolderId != defender.Id)
                throw new GameRuleException($"{ground.Name} is not held by {defender.Name} any more.");
            if (ground.HolderId == attacker.Id)
                throw new GameRuleException("You already hold that ground.");
            if (ground.ProtectedUntilUtc is { } groundSafeUntil && groundSafeUntil > nowUtc)
            {
                var minutes = Math.Max(1, (int)Math.Ceiling((groundSafeUntil - nowUtc).TotalMinutes));
                throw new GameRuleException($"{ground.Name} has just changed hands. It is settled for another {minutes} minute(s).");
            }
        }
        else
        {
            // Anti-farm gate: a heavyweight cannot pick on a newcomer, and the very new cannot be
            // touched. Skipped for ground, which is contested rather than robbed.
            var mismatch = AntiFarm.RejectReason(
                economy.CalculateNetWorth(attacker),
                economy.CalculateNetWorth(defender),
                _options.AntiFarm);
            if (mismatch is not null)
                throw new GameRuleException(mismatch);
        }

        // Caps a dogpile. Protection only exists once a mission finishes, so without this a crowd can
        // all launch at the same moment and every hit lands unshielded.
        var maxIncoming = Math.Max(1, _options.AntiFarm.MaxIncomingAttacks);
        var saved = await db.CombatMissions.AsNoTracking()
            .CountAsync(x => x.DefenderId == defender.Id && x.Status != "Complete", cancellationToken);
        // Missions added but not yet saved count too. The bot simulator launches many per batch and
        // saves once at the end, so a database-only count let extra attacks slip past the cap.
        var pending = db.ChangeTracker.Entries<CombatMission>()
            .Count(x => x.State == EntityState.Added && x.Entity.DefenderId == defender.Id);
        var incoming = saved + pending;
        if (incoming >= maxIncoming)
            throw new GameRuleException($"{defender.Name} is already fighting off {incoming:N0} attack(s). Wait your turn.");

        // Whoever is already out leading another mission cannot lead this one too, and neither can
        // anyone standing on ground.
        var commanding = activeMissions.Where(x => x.CommanderPimpId is not null).Select(x => x.CommanderPimpId!.Value).ToList();
        commanding.AddRange(await territories.GarrisonedPimpIdsAsync(attacker.Id, cancellationToken));
        var commander = pimps.ChooseCommander(attacker, commanding, request.CommanderPimpId)
            ?? throw new GameRuleException("You need a free pimp to command the attack.");

        // Best guns first. Nobody walks past a rifle to pick up a pistol, and settling the mix here
        // rather than at the first round means it is decided before anything is at stake.
        var carried = committed.AvailableRack.Best(request.Weapons);

        // Borrowed thugs leave the pool now and are not in it for anybody else until this raid comes
        // home. That is the coordination cost the pool exists to create: what you take tonight, your
        // ally does not have.
        var borrowed = 0;
        if (request.AllianceThugs > 0)
        {
            var crew = attacker.Alliance
                ?? throw new GameRuleException("You are not running with a crew.");
            var borrowLimit = alliances.BorrowLimit(request.Thugs);
            if (request.AllianceThugs > borrowLimit)
                throw new GameRuleException(borrowLimit == 0
                    ? "Send thugs of your own and the crew will send the same again."
                    : $"You sent {request.Thugs:N0} of your own, so the crew will send {borrowLimit:N0} at most.");
            if (crew.OffensiveThugs < request.AllianceThugs)
                throw new GameRuleException($"{crew.Name} has {crew.OffensiveThugs:N0} offensive thug(s) spare.");

            borrowed = request.AllianceThugs;
            crew.OffensiveThugs -= borrowed;
        }

        var travelSeconds = RollSeconds(combat.AttackTravelSecondsMin, combat.AttackTravelSecondsMax);
        var arrivesAt = nowUtc.AddSeconds(travelSeconds);
        var attackerMorale = AverageMorale(attacker);
        var defenderMorale = Math.Min(100, AverageMorale(defender) + 5);
        var summary = borrowed > 0
            ? $"{attacker.Name} sent {commander.Name} commanding {request.Thugs:N0} thug(s) and {borrowed:N0} of {attacker.Alliance!.Name}'s, carrying {carried.Describe()}, toward {defender.Name}."
            : $"{attacker.Name} sent {commander.Name} commanding {request.Thugs:N0} thug(s) carrying {carried.Describe()} toward {defender.Name}.";

        attacker.Turns -= combat.AttackTurnCost;
        attacker.LastAttackAtUtc = nowUtc;

        var mission = new CombatMission
        {
            AttackerId = attacker.Id,
            Attacker = attacker,
            DefenderId = defender.Id,
            Defender = defender,
            TerritoryId = ground?.Id,
            Territory = ground,
            Status = "Traveling",
            Outcome = "Pending",
            Summary = summary,
            TurnsSpent = combat.AttackTurnCost,
            AssignedPimps = CommandingPimps,
            CommanderPimp = commander,
            CommanderName = commander.Name,
            CommanderBonusPercent = commander.Specialty == PimpSpecialties.Enforcer ? commander.BonusPercent : 0,
            AssignedThugs = request.Thugs,
            AssignedWeapons = carried.Total,
            RemainingAttackers = request.Thugs,
            // Setting the rack sets RemainingWeapons too, so the count and the four shelves cannot disagree.
            Carried = carried,
            AllianceThugs = borrowed,
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
                    Summary = $"{commander.Name} led the crew out toward {defender.Name}. Arrival in {FormatDuration(travelSeconds)}.",
                    AttackerMorale = attackerMorale,
                    DefenderMorale = defenderMorale,
                    CreatedAtUtc = nowUtc
                }
            ]
        };

        db.CombatMissions.Add(mission);
        // Arrival is sooner than whatever the gate was holding, so bring it forward.
        schedule.NoteUpcoming(arrivesAt);
        return mission;
    }

    public async Task<int> ResolveDueAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var missions = await db.CombatMissions
            .Include(x => x.Attacker).ThenInclude(x => x.Hideout)
            .Include(x => x.Attacker).ThenInclude(x => x.Crew)
            .Include(x => x.Defender).ThenInclude(x => x.Crew)
            .Include(x => x.CommanderPimp)
            .Include(x => x.Events)
            // Tracked, not AsNoTracking: a won raid writes the new holder onto this row. The garrison
            // pimp comes with it, or their bonus silently reads as zero.
            .Include(x => x.Territory).ThenInclude(x => x!.GarrisonPimp)
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
        // Calling it off sends the crew's men home as well. A cancelled raid that kept them would be a
        // way of holding the pool indefinitely for the price of one cancellation.
        ReturnBorrowedThugs(mission);
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
            TerritoryId = mission.TerritoryId,
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
        // This mission is gone, so the cached next-due may now be too early. Harmless: an early gate
        // costs one wasted pass, whereas a late one would stall the remaining missions.
        schedule.Invalidate();
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

    private void ValidateLaunch(Player attacker, Player defender, CombatAttackRequest request, Territory? ground, DateTime nowUtc, CombatCommitment committed, CombatOptions combat, DateTime? laneReadyAt)
    {
        if (attacker.Id == defender.Id)
            throw new GameRuleException("You cannot attack yourself.");
        // The whole of what an alliance buys. Checked here rather than at the endpoint so every way of
        // reaching a raid - the player's, a rival's brain, the admin's directive - runs into it.
        if (AllianceService.AreAllied(attacker, defender))
            throw new GameRuleException($"{defender.Name} runs with your crew.");
        if (attacker.Turns < combat.AttackTurnCost)
            throw new GameRuleException($"You need {combat.AttackTurnCost:N0} turns to attack.");
        if (laneReadyAt is { } readyAt && readyAt > nowUtc)
            throw new GameRuleException($"All {committed.MaxActiveAttackMissions:N0} attack lane(s) are cooling down. The next one frees in {FormatDuration((int)Math.Ceiling((readyAt - nowUtc).TotalSeconds))}.");
        if (committed.ActiveAttackMissions >= committed.MaxActiveAttackMissions)
            throw new GameRuleException($"You already have {committed.ActiveAttackMissions:N0} active attack mission(s).");
        if (request.Thugs < 1)
            throw new GameRuleException("Each attack needs at least one thug.");
        if (request.Weapons < 0 || request.Weapons > request.Thugs)
            throw new GameRuleException("Weapons sent must be between zero and the number of thugs sent.");
        if (committed.AvailablePimps < CommandingPimps)
            throw new GameRuleException("You need a free pimp to command the attack.");
        if (request.Thugs > committed.AvailableThugs)
            throw new GameRuleException("You do not have that many available thugs.");
        if (request.Weapons > committed.AvailableWeapons)
            throw new GameRuleException("You do not have that many available weapons.");
        // House protection shields a player from being robbed. Ground is contested rather than robbed,
        // so it neither blocks a raid for territory nor is granted by one: the ground carries its own
        // settling period instead.
        if (ground is null)
        {
            if (defender.CombatProtectionUntilUtc is { } protectionUntil && protectionUntil > nowUtc)
                throw new GameRuleException($"{defender.Name} is under combat protection.");
            if (defender.Pimps + defender.Hoes + defender.Thugs <= 0 && defender.Cash <= 0 && defender.Weed <= 0 && defender.Coke <= 0)
                throw new GameRuleException($"{defender.Name} has nothing worth attacking right now.");
        }
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
            .Select(x => new { x.AssignedPimps, x.RemainingAttackers, x.RemainingWeapons, x.CommanderPimpId, x.CarriedPistols, x.CarriedShotguns, x.CarriedSmgs, x.CarriedRifles })
            .ToListAsync(cancellationToken);
        var defenderAway = defenderCommitted.Where(x => x.CommanderPimpId != null).Select(x => x.CommanderPimpId!.Value).ToList();
        // A pimp posted to ground is not at home either, so they cannot also be sharpening the house.
        defenderAway.AddRange(await territories.GarrisonedPimpIdsAsync(mission.DefenderId, cancellationToken));
        var defenderHomePimps = Math.Max(0, mission.Defender.Pimps - defenderCommitted.Sum(x => x.AssignedPimps));
        // Whatever the crew has posted to this house stands in it too, and is armed the same way.
        var postedDefenders = Math.Max(0, mission.Defender.AllianceDefenders);
        var defenderHomeThugs = Math.Max(0, mission.Defender.Thugs - defenderCommitted.Sum(x => x.RemainingAttackers)) + postedDefenders;
        var defenderHomeWeapons = Math.Max(0, mission.Defender.Weapons - defenderCommitted.Sum(x => x.RemainingWeapons));
        // The guns still in the house are whatever the defender's own raiding parties did not take.
        var defenderHomeRack = mission.Defender.Armoury - defenderCommitted.Aggregate(
            Armoury.Empty,
            (rack, x) => rack + new Armoury(x.CarriedPistols, x.CarriedShotguns, x.CarriedSmgs, x.CarriedRifles));

        mission.CurrentRound++;
        // Borrowed thugs stand in the line like anybody else, and they arrive armed - at fifteen
        // thousand each they are not turning up empty-handed - so they carry their own firepower on top
        // of whatever the attacker's own crew brought out of the rack.
        var borrowedFirepower = mission.AllianceThugs * Math.Max(0, _options.Alliances.ThugFirepower);
        var attackerPower = AttackPower(
            mission.AssignedPimps,
            mission.RemainingAttackers + mission.AllianceThugs,
            new Firepower(Guns(mission.Carried, mission.RemainingAttackers).InPistols + borrowedFirepower),
            mission.AttackerMorale,
            mission.CommanderBonusPercent);

        // A raid for ground fights the garrison standing on it, not everyone back at the holder's
        // house. Fighting the whole house would make ground effectively untakeable: the garrison is a
        // handful of thugs and the house is the rest of the roster, so the defender would be stronger
        // for having sent fewer people to hold it. No enforcer bonus either; that pimp is at home.
        var defenderPower = mission.Territory is { } ground
            ? DefensePower(
                ground.GarrisonPimpId is null ? 0 : 1,
                ground.GarrisonThugs,
                // Ground is held by bodies. A garrison carries sidearms and nothing heavier, which is
                // exactly what it was worth before tiers existed.
                Firepower.Sidearms(ground.GarrisonThugs, ground.GarrisonThugs),
                mission.DefenderMorale,
                pimps.GarrisonBonusPercent(ground.GarrisonPimp))
            : DefensePower(
                defenderHomePimps,
                defenderHomeThugs,
                new Firepower(
                    Guns(defenderHomeRack, Math.Max(0, defenderHomeThugs - postedDefenders)).InPistols
                    + postedDefenders * Math.Max(0, _options.Alliances.ThugFirepower)),
                mission.DefenderMorale,
                pimps.DefenceBonusPercent(mission.Defender, defenderAway));
        var attackRoll = ApplyPowerVariance(attackerPower, combat.PowerRandomnessPercent);
        var defenseRoll = ApplyPowerVariance(defenderPower, combat.PowerRandomnessPercent);
        var difference = attackRoll - defenseRoll;
        var round = combat.Round;
        var close = Math.Abs(difference) <= Math.Max(round.CloseMinimumGap, Math.Max(attackRoll, defenseRoll) * round.ClosePercent);

        var attackerLosses = 0;
        var defenderLosses = 0;
        var attackerWeaponLosses = 0;
        var defenderWeaponLosses = 0;
        string summary;

        if (close)
        {
            var moraleLoss = random.NextInclusive(round.DrawMoraleLossMin, round.DrawMoraleLossMax);
            mission.AttackerMorale = ClampMorale(mission.AttackerMorale - moraleLoss);
            mission.DefenderMorale = ClampMorale(mission.DefenderMorale - moraleLoss);
            summary = $"Round {mission.CurrentRound}: both crews trade pressure and neither side breaks.";
        }
        else if (difference > 0)
        {
            var defenderMoraleLoss = random.NextInclusive(round.LosingSideMoraleLossMin, round.LosingSideMoraleLossMax);
            var attackerMoraleLoss = random.NextInclusive(round.WinningSideMoraleLossMin, round.WinningSideMoraleLossMax);
            defenderLosses = LossCount(defenderHomeThugs, round.CrewLossRate);
            defenderWeaponLosses = Math.Min(LossCount(defenderHomeWeapons, round.WeaponLossRate), defenderLosses + 1);
            mission.DefenderMorale = ClampMorale(mission.DefenderMorale - defenderMoraleLoss);
            mission.AttackerMorale = ClampMorale(mission.AttackerMorale - attackerMoraleLoss);
            ApplyDefenderLosses(mission, defenderLosses, defenderWeaponLosses);
            summary = $"Round {mission.CurrentRound}: attackers push through. Defender morale drops and {defenderLosses:N0} thug(s) fall.";
        }
        else
        {
            var attackerMoraleLoss = random.NextInclusive(round.LosingSideMoraleLossMin, round.LosingSideMoraleLossMax);
            var defenderMoraleLoss = random.NextInclusive(round.WinningSideMoraleLossMin, round.WinningSideMoraleLossMax);
            attackerLosses = LossCount(mission.RemainingAttackers, round.CrewLossRate);
            attackerWeaponLosses = Math.Min(LossCount(mission.RemainingWeapons, round.WeaponLossRate), attackerLosses + 1);
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
            // Repeat wins against the same victim are worth progressively less, and a defender who has
            // been hit repeatedly by anyone earns a wider shield.
            var windowStart = nowUtc.AddHours(-Math.Max(1, _options.AntiFarm.RepeatWindowHours));
            var priorVictories = await db.CombatLogs.AsNoTracking()
                .CountAsync(x => x.AttackerId == mission.AttackerId
                                 && x.DefenderId == mission.DefenderId
                                 && x.Outcome == "Victory"
                                 && x.CreatedAtUtc >= windowStart, cancellationToken);
            var recentHits = await db.CombatLogs.AsNoTracking()
                .CountAsync(x => x.DefenderId == mission.DefenderId
                                 && x.Outcome == "Victory"
                                 && x.CreatedAtUtc >= windowStart, cancellationToken);
            // A stash house lifts the haul. Folded into the multiplier the mission already carries, so
            // there is still one number deciding what a raid is worth.
            var stashPercent = (await territories.EffectsForAsync(mission.AttackerId, mission.Attacker.City, cancellationToken)).LootPercent;
            mission.LootMultiplierPercent = (int)Math.Round(
                AntiFarm.LootMultiplier(priorVictories, _options.AntiFarm) * 100 * (1 + stashPercent / 100.0));
            mission.DefenderRecentHits = recentHits;

            var lootOverflow = ApplyLoot(mission);
            // The house fell, so a pimp who stayed behind to hold it may not have survived it.
            var defenderCommanding = await ActiveAttackMissions(mission.DefenderId)
                .Where(x => x.CommanderPimpId != null)
                .Select(x => x.CommanderPimpId!.Value)
                .ToListAsync(cancellationToken);
            var defenderFate = pimps.SettleBrokenDefence(mission.Defender, defenderCommanding, nowUtc);
            if (defenderFate.Happened)
                mission.DefenderPimpsLost = 1;

            var fell = defenderFate.Happened ? $" {defenderFate.Name} died holding the house." : string.Empty;
            BeginReturn(mission, "Victory", $"{mission.CommanderName} broke {mission.Defender.Name}'s defense and grabbed ${mission.CashStolen:N0}, {mission.WeedStolen:N0} weed, and {mission.CokeStolen:N0} coke.{fell}{lootOverflow.Describe()}", nowUtc);
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

    /// <summary>
    /// Hands ground over on a win, or leaves the garrison bloodied on a loss.
    ///
    /// Settled when the raid comes home rather than the moment the last round is won, so a player
    /// cannot see ground change hands and then have the crew that took it walk away: the surviving
    /// attackers become the garrison.
    /// </summary>
    /// <summary>
    /// Hands the surviving borrowed thugs back to the pool.
    ///
    /// On the way home rather than at the last round, because they are out until the crew is out: an
    /// ally looking at the pool while a raid is still running should see what is actually available to
    /// them, not what will be available once somebody else's fight finishes.
    /// </summary>
    private void ReturnBorrowedThugs(CombatMission mission)
    {
        if (mission.AllianceThugs <= 0)
            return;

        if (mission.Attacker.Alliance is { } crew)
            crew.OffensiveThugs += mission.AllianceThugs;
        mission.AllianceThugs = 0;
    }

    private void SettleTerritory(CombatMission mission, DateTime nowUtc)
    {
        if (mission.Territory is not { } ground)
            return;

        if (mission.Outcome == "Victory")
        {
            var loser = ground.HolderId;
            territories.Transfer(ground, mission.AttackerId, mission.RemainingAttackers, nowUtc);
            mission.Summary = $"{mission.Attacker.Name} took {ground.Name}.";

            // The loser is told, because losing ground is something done to them and they were very
            // likely not watching. Phrased "from you." so it reads as a notification rather than an
            // action they took, which is what keeps it out of their own activity list.
            if (loser is { } lostBy)
                db.ActionLogs.Add(new GameActionLog
                {
                    PlayerId = lostBy,
                    Action = "GROUND",
                    Summary = $"{mission.Attacker.Name} took {ground.Name} from you.",
                    CreatedAtUtc = nowUtc
                });
            return;
        }

        territories.Bloody(ground, mission.DefenderThugsLost);
        mission.Summary = $"{ground.Name} held against {mission.Attacker.Name}.";

        // A raid that fails still costs the holder: the garrison wore it. Told to them, because a
        // garrison quietly shrinking with no explanation reads as a bug rather than a fight.
        if (ground.HolderId is { } heldBy)
            db.ActionLogs.Add(new GameActionLog
            {
                PlayerId = heldBy,
                Action = "GROUND",
                Summary = mission.DefenderThugsLost > 0
                    ? $"{ground.Name} held against {mission.Attacker.Name}, at the cost of {mission.DefenderThugsLost:N0} thug(s)."
                    : $"{ground.Name} held against {mission.Attacker.Name} without a scratch.",
                ThugsDelta = -mission.DefenderThugsLost,
                CreatedAtUtc = nowUtc
            });
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
        if (mission.TerritoryId is not null)
        {
            // Winning or losing a fight over a corner says nothing about whether the holder's house is
            // being farmed, so it grants no shield there.
            mission.Events.Add(new CombatMissionEvent
            {
                Round = mission.CurrentRound,
                Kind = outcome,
                Summary = summary,
                AttackerMorale = Math.Round(mission.AttackerMorale, 2),
                DefenderMorale = Math.Round(mission.DefenderMorale, 2),
                CreatedAtUtc = nowUtc
            });
            return;
        }

        mission.Defender.LastAttackedAtUtc = nowUtc;
        var protectionMinutes = AntiFarm.ProtectionMinutes(
            Math.Max(0, mission.DefenderRecentHits - 1),
            combat.DefenderProtectionMinutes,
            _options.AntiFarm);
        mission.DefenderProtectionMinutes = protectionMinutes;
        mission.DefenderProtectionUntilUtc = nowUtc.AddMinutes(protectionMinutes);
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
        ReturnBorrowedThugs(mission);

        // The commander's own reckoning: a win lifts them, a beating can cost their life.
        var commanderFate = pimps.SettleMission(mission.Attacker, mission.CommanderPimp, mission.Outcome, nowUtc);
        if (commanderFate.Happened)
            mission.AttackerPimpsLost = 1;

        mission.Status = "Complete";
        mission.CompletedAtUtc = nowUtc;
        mission.Summary = commanderFate.Happened
            ? $"{mission.CommanderName} did not come back from {mission.Defender.Name}: {mission.Outcome}."
            : $"{mission.CommanderName ?? "The crew"} returned from {mission.Defender.Name}: {mission.Outcome}.";
        // After the summary above, which would otherwise overwrite what the raid was actually about.
        SettleTerritory(mission, nowUtc);
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
            TerritoryId = mission.TerritoryId,
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

    private StorageOverflow ApplyLoot(CombatMission mission)
    {
        var combat = _options.Combat;
        var share = Math.Clamp(mission.LootMultiplierPercent, 0, 100) / 100.0;
        mission.CashStolen = Scale(LootCash(mission.Defender.Cash, combat.MinCashLootPercent, combat.MaxCashLootPercent), share);
        mission.WeedStolen = (int)Scale(LootProduct(mission.Defender.Weed, combat.MinProductLootPercent, combat.MaxProductLootPercent), share);
        mission.CokeStolen = (int)Scale(LootProduct(mission.Defender.Coke, combat.MinProductLootPercent, combat.MaxProductLootPercent), share);
        mission.Defender.Cash -= mission.CashStolen;
        mission.Defender.Weed -= mission.WeedStolen;
        mission.Defender.Coke -= mission.CokeStolen;

        // The haul still has to fit at home: cash over the safe banks itself, goods over storage spill.
        var stockBefore = StockLevels.From(mission.Attacker);
        mission.Attacker.Cash += mission.CashStolen;
        mission.Attacker.Weed += mission.WeedStolen;
        mission.Attacker.AddCoke(mission.CokeStolen, mission.Defender.CokePurity);
        return hideout.Settle(mission.Attacker, stockBefore);
    }

    private void ApplyAttackerLosses(CombatMission mission, int thugsLost, int weaponsLost)
    {
        // Split across the whole line rather than off one end of it. Borrowed thugs dying first would
        // empty a pool in a single raid; the attacker's own dying first would make borrowing a way of
        // using somebody else's men as armour.
        var line = mission.RemainingAttackers + mission.AllianceThugs;
        thugsLost = Math.Min(thugsLost, line);
        weaponsLost = Math.Min(weaponsLost, mission.RemainingWeapons);

        var borrowedLost = line <= 0
            ? 0
            : Math.Min(mission.AllianceThugs, (int)Math.Round(thugsLost * (double)mission.AllianceThugs / line));
        mission.AllianceThugs -= borrowedLost;
        mission.AllianceThugsLost += borrowedLost;

        thugsLost = Math.Min(thugsLost - borrowedLost, mission.RemainingAttackers);
        mission.RemainingAttackers -= thugsLost;

        // Which guns were dropped is decided against what the crew is actually carrying, cheapest
        // first, and the same guns then come off the rack at home. Taking a flat count off the rack
        // instead could destroy rifles that never left the house while the crew's pistols came back.
        var dropped = mission.Carried.WorstFirst(weaponsLost);
        mission.Carried = mission.Carried - dropped;
        mission.Attacker.Armoury -= dropped;

        mission.Attacker.Thugs = Math.Max(0, mission.Attacker.Thugs - thugsLost);
        mission.AttackerThugsLost += thugsLost;
        mission.AttackerWeaponsLost += dropped.Total;
    }

    private void ApplyDefenderLosses(CombatMission mission, int thugsLost, int weaponsLost)
    {
        // The same split on the other side. Posted defenders take their share and are gone from the
        // pool for good, which is what makes stationing them a real cost to the crew rather than a free
        // wall around whoever asked first.
        var held = mission.Defender.Thugs + mission.Defender.AllianceDefenders;
        var postedLost = held <= 0
            ? 0
            : Math.Min(mission.Defender.AllianceDefenders, (int)Math.Round(thugsLost * (double)mission.Defender.AllianceDefenders / held));
        mission.Defender.AllianceDefenders -= postedLost;
        thugsLost = Math.Max(0, thugsLost - postedLost);

        mission.Defender.Thugs = Math.Max(0, mission.Defender.Thugs - thugsLost);
        mission.DefenderWeaponsLost += mission.Defender.RemoveWeapons(weaponsLost).Total;
        // Every body that fell here, borrowed or not: the record is of what the house lost.
        mission.DefenderThugsLost += thugsLost + postedLost;
    }

    /// <summary>What a rack is worth to the crew holding it.</summary>
    private Firepower Guns(Armoury rack, int thugs)
        => Firepower.Of(rack, thugs, _options.WeaponFirepower());

    private int AttackPower(int pimps, int thugs, Firepower firepower, double morale, int commanderBonusPercent = 0)
        => CombatPower.Attack(pimps, thugs, firepower, morale, _options.Combat.Power, commanderBonusPercent);

    private int DefensePower(int pimps, int thugs, Firepower firepower, double morale, int enforcerBonusPercent = 0)
        => CombatPower.Defence(pimps, thugs, firepower, morale, _options.Combat.Power, enforcerBonusPercent);

    private static int WithBonus(int power, int bonusPercent)
        => bonusPercent <= 0 ? power : (int)Math.Round(power * (1 + bonusPercent / 100.0), MidpointRounding.AwayFromZero);

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
            if (random.NextDouble() < _options.Combat.Round.LossRollChance) losses++;
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

    /// <summary>Applies the anti-farm share, keeping at least one unit when the base haul was non-zero.</summary>
    private static long Scale(long amount, double share)
        => amount <= 0 ? 0 : Math.Max(1, (long)Math.Floor(amount * share));

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

/// <param name="AvailableRack">
/// Which guns are still on the rack at home, tier by tier. A second raid cannot arm itself from the
/// rifles the first one is already carrying, and a total alone could not tell it that: it would happily
/// hand the same four rifles to both crews and let the loser lose guns the winner was still holding.
/// </param>
public sealed record CombatCommitment(
    int CommittedPimps,
    int CommittedThugs,
    int CommittedWeapons,
    int AvailablePimps,
    int AvailableThugs,
    int AvailableWeapons,
    Armoury AvailableRack,
    int ActiveAttackMissions,
    int MaxActiveAttackMissions)
{
    public static CombatCommitment From(Player player, IReadOnlyCollection<CombatMission> activeMissions, int maxActiveMissions)
    {
        var committedPimps = activeMissions.Sum(x => x.AssignedPimps);
        var committedThugs = activeMissions.Sum(x => x.RemainingAttackers);
        var committedWeapons = activeMissions.Sum(x => x.RemainingWeapons);
        var committedRack = activeMissions.Aggregate(Armoury.Empty, (rack, mission) => rack + mission.Carried);
        return new CombatCommitment(
            committedPimps,
            committedThugs,
            committedWeapons,
            Math.Max(0, player.Pimps - committedPimps),
            Math.Max(0, player.Thugs - committedThugs),
            Math.Max(0, player.Weapons - committedWeapons),
            player.Armoury - committedRack,
            activeMissions.Count,
            Math.Max(1, maxActiveMissions));
    }
}

public sealed record CombatMissionCancelResult(CombatMission Mission, long Cost);
