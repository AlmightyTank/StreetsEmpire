using Microsoft.Extensions.Options;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

public sealed class CombatService(IOptionsSnapshot<GameOptions> options, IGameRandom random)
{
    private readonly GameOptions _options = options.Value;

    public CombatResolution Attack(Player attacker, Player defender, DateTime nowUtc)
    {
        var pending = StartAttack(attacker, defender, nowUtc);
        pending.Log.ResolvesAtUtc = nowUtc;
        return ResolveAttack(pending.Log, attacker, defender, nowUtc);
    }

    public CombatResolution StartAttack(Player attacker, Player defender, DateTime nowUtc)
    {
        var combat = _options.Combat;
        ValidateAttack(attacker, defender, nowUtc, combat);

        var travelSeconds = random.NextInclusive(
            Math.Clamp(combat.AttackTravelSecondsMin, 10, 3600),
            Math.Clamp(Math.Max(combat.AttackTravelSecondsMin, combat.AttackTravelSecondsMax), 10, 3600));
        var resolvesAt = nowUtc.AddSeconds(travelSeconds);
        var protectionUntil = resolvesAt.AddMinutes(Math.Max(1, combat.DefenderProtectionMinutes));

        attacker.Turns -= combat.AttackTurnCost;
        attacker.LastAttackAtUtc = nowUtc;
        defender.CombatProtectionUntilUtc = protectionUntil;

        var summary = $"{attacker.Name} sent a crew toward {defender.Name}. The attack will resolve in {FormatDuration(travelSeconds)}.";
        var log = new CombatLog
        {
            AttackerId = attacker.Id,
            Attacker = attacker,
            DefenderId = defender.Id,
            Defender = defender,
            Outcome = "Pending",
            Summary = summary,
            TurnsSpent = combat.AttackTurnCost,
            AttackerPower = AttackPower(attacker),
            DefenderPower = DefensePower(defender),
            DefenderProtectionUntilUtc = protectionUntil,
            ResolvesAtUtc = resolvesAt,
            CreatedAtUtc = nowUtc
        };

        return new CombatResolution(summary, "Pending", log, new ActionResultResponse(summary, attacker.Turns, new Dictionary<string, object?>
        {
            ["outcome"] = "Pending",
            ["turnsSpent"] = combat.AttackTurnCost,
            ["resolvesAtUtc"] = resolvesAt,
            ["travelSeconds"] = travelSeconds,
            ["defenderProtectionUntilUtc"] = protectionUntil
        }));
    }

    public CombatResolution ResolveAttack(CombatLog log, Player attacker, Player defender, DateTime nowUtc)
    {
        if (!string.Equals(log.Outcome, "Pending", StringComparison.OrdinalIgnoreCase))
            return new CombatResolution(log.Summary, log.Outcome, log, new ActionResultResponse(log.Summary, attacker.Turns));
        if (log.ResolvesAtUtc is { } resolvesAt && resolvesAt > nowUtc)
            throw new GameRuleException("This attack is still in progress.");

        var combat = _options.Combat;
        var attackerBefore = CombatantSnapshot.From(attacker);
        var defenderBefore = CombatantSnapshot.From(defender);
        var attackerPower = AttackPower(attacker);
        var defenderPower = DefensePower(defender);
        var attackRoll = ApplyPowerVariance(attackerPower, combat.PowerRandomnessPercent);
        var defenseRoll = ApplyPowerVariance(defenderPower, combat.PowerRandomnessPercent);
        var victory = attackRoll >= defenseRoll;

        defender.LastAttackedAtUtc = nowUtc;
        defender.CombatProtectionUntilUtc = nowUtc.AddMinutes(Math.Max(1, combat.DefenderProtectionMinutes));

        var cashStolen = 0L;
        var weedStolen = 0;
        var cokeStolen = 0;

        if (victory)
        {
            cashStolen = LootCash(defender.Cash, combat.MinCashLootPercent, combat.MaxCashLootPercent);
            weedStolen = LootProduct(defender.Weed, combat.MinProductLootPercent, combat.MaxProductLootPercent);
            cokeStolen = LootProduct(defender.Coke, combat.MinProductLootPercent, combat.MaxProductLootPercent);

            defender.Cash -= cashStolen;
            attacker.Cash += cashStolen;
            defender.Weed -= weedStolen;
            attacker.Weed += weedStolen;
            defender.Coke -= cokeStolen;
            attacker.Coke += cokeStolen;

            ApplyLosses(defender, combat.LoserCrewLossPercent, combat.WeaponLossPercent);
            ApplyLosses(attacker, combat.WinnerCrewLossPercent, combat.WeaponLossPercent / 2);
        }
        else
        {
            ApplyLosses(attacker, combat.LoserCrewLossPercent, combat.WeaponLossPercent);
            ApplyLosses(defender, combat.WinnerCrewLossPercent, combat.WeaponLossPercent / 2);
        }

        var attackerAfter = CombatantSnapshot.From(attacker);
        var defenderAfter = CombatantSnapshot.From(defender);
        var outcome = victory ? "Victory" : "Defeat";
        var summary = victory
            ? $"{attacker.Name} hit {defender.Name} and won, stealing ${cashStolen:N0}, {weedStolen:N0} weed, and {cokeStolen:N0} coke."
            : $"{attacker.Name} attacked {defender.Name} and got pushed back.";

        log.Outcome = outcome;
        log.Summary = summary;
        log.AttackerPower = attackerPower;
        log.DefenderPower = defenderPower;
        log.CashStolen = cashStolen;
        log.WeedStolen = weedStolen;
        log.CokeStolen = cokeStolen;
        log.AttackerPimpsLost = Math.Max(0, attackerBefore.Pimps - attackerAfter.Pimps);
        log.AttackerHoesLost = Math.Max(0, attackerBefore.Hoes - attackerAfter.Hoes);
        log.AttackerThugsLost = Math.Max(0, attackerBefore.Thugs - attackerAfter.Thugs);
        log.AttackerWeaponsLost = Math.Max(0, attackerBefore.Weapons - attackerAfter.Weapons);
        log.DefenderPimpsLost = Math.Max(0, defenderBefore.Pimps - defenderAfter.Pimps);
        log.DefenderHoesLost = Math.Max(0, defenderBefore.Hoes - defenderAfter.Hoes);
        log.DefenderThugsLost = Math.Max(0, defenderBefore.Thugs - defenderAfter.Thugs);
        log.DefenderWeaponsLost = Math.Max(0, defenderBefore.Weapons - defenderAfter.Weapons);
        log.DefenderProtectionUntilUtc = defender.CombatProtectionUntilUtc;
        log.ResolvedAtUtc = nowUtc;

        return new CombatResolution(summary, outcome, log, new ActionResultResponse(summary, attacker.Turns, new Dictionary<string, object?>
        {
            ["outcome"] = outcome,
            ["attackerPower"] = attackerPower,
            ["defenderPower"] = defenderPower,
            ["attackRoll"] = Math.Round(attackRoll, 2),
            ["defenseRoll"] = Math.Round(defenseRoll, 2),
            ["cashStolen"] = cashStolen,
            ["weedStolen"] = weedStolen,
            ["cokeStolen"] = cokeStolen,
            ["attackerCrewLost"] = log.AttackerPimpsLost + log.AttackerHoesLost + log.AttackerThugsLost,
            ["defenderCrewLost"] = log.DefenderPimpsLost + log.DefenderHoesLost + log.DefenderThugsLost,
            ["defenderProtectionUntilUtc"] = defender.CombatProtectionUntilUtc
        }));
    }

    public int AttackPower(Player player)
    {
        var armedThugs = Math.Min(player.Weapons, player.Thugs);
        var averageMorale = AverageMorale(player);
        return Math.Max(1, player.Thugs * 12 + armedThugs * 8 + player.Pimps * 2 + (int)Math.Round(averageMorale / 2));
    }

    public int DefensePower(Player player)
    {
        var armedThugs = Math.Min(player.Weapons, player.Thugs);
        var averageMorale = AverageMorale(player);
        return Math.Max(1, player.Thugs * 14 + armedThugs * 10 + player.Pimps * 3 + (int)Math.Round(averageMorale));
    }

    private void ValidateAttack(Player attacker, Player defender, DateTime nowUtc, CombatOptions combat)
    {
        if (attacker.Id == defender.Id)
            throw new GameRuleException("You cannot attack yourself.");
        if (attacker.Turns < combat.AttackTurnCost)
            throw new GameRuleException($"You need {combat.AttackTurnCost:N0} turns to attack.");
        if (attacker.LastAttackAtUtc is { } lastAttack && lastAttack.AddMinutes(combat.AttackCooldownMinutes) > nowUtc)
            throw new GameRuleException("You need to wait before attacking again.");
        if (defender.CombatProtectionUntilUtc is { } protectionUntil && protectionUntil > nowUtc)
            throw new GameRuleException($"{defender.Name} is under combat protection.");
        if (defender.Pimps + defender.Hoes + defender.Thugs <= 0 && defender.Cash <= 0 && defender.Weed <= 0 && defender.Coke <= 0)
            throw new GameRuleException($"{defender.Name} has nothing worth attacking right now.");
    }

    private double ApplyPowerVariance(int power, double randomnessPercent)
    {
        var variance = Math.Clamp(randomnessPercent, 0, 0.5);
        var multiplier = 1 - variance + random.NextDouble() * variance * 2;
        return Math.Max(1, power * multiplier);
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

    private void ApplyLosses(Player player, double crewLossPercent, double weaponLossPercent)
    {
        player.Thugs = Math.Max(0, player.Thugs - Losses(player.Thugs, crewLossPercent));
        player.Hoes = Math.Max(0, player.Hoes - Losses(player.Hoes, crewLossPercent / 2));
        player.Pimps = Math.Max(1, player.Pimps - Losses(Math.Max(0, player.Pimps - 1), crewLossPercent / 3));
        player.Weapons = Math.Max(0, player.Weapons - Losses(player.Weapons, weaponLossPercent));
    }

    private int Losses(int count, double percent)
    {
        if (count <= 0 || percent <= 0) return 0;
        var maxLosses = Math.Max(0, (int)Math.Ceiling(count * percent));
        if (maxLosses <= 0) return 0;
        var losses = 0;
        for (var i = 0; i < maxLosses; i++)
            if (random.NextDouble() < 0.65) losses++;
        return Math.Min(count, losses);
    }

    private static double AverageMorale(Player player)
        => Math.Round((player.HoeHappiness + player.ThugHappiness) / 2, 2);

    private static string FormatDuration(int seconds)
    {
        var minutes = seconds / 60;
        var remainder = seconds % 60;
        return minutes <= 0
            ? $"{seconds} second{(seconds == 1 ? string.Empty : "s")}"
            : $"{minutes}m {remainder:00}s";
    }

    private sealed record CombatantSnapshot(int Pimps, int Hoes, int Thugs, int Weapons)
    {
        public static CombatantSnapshot From(Player player)
            => new(player.Pimps, player.Hoes, player.Thugs, player.Weapons);
    }
}

public sealed record CombatResolution(
    string Summary,
    string Outcome,
    CombatLog Log,
    ActionResultResponse Result);
