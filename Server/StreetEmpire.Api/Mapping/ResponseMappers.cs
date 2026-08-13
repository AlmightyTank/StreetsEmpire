using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Mapping;
using StreetEmpire.Api.Models;
using StreetEmpire.Api.Services;
using static StreetEmpire.Api.Support.Formatting;
using StreetEmpire.Api.Support;

namespace StreetEmpire.Api.Mapping;

/// <summary>Turns entities into the response contracts the browser client consumes.</summary>
internal static class ResponseMappers
{

    internal static AdminPlayerSummaryResponse ToAdminSummary(Player player, EconomyService economy)
        => new(
            player.Id,
            player.Name,
            player.Account.Username,
            player.City,
            player.Account.IsBot,
            player.Account.IsAdmin,
            player.Account.IsBanned,
            player.Account.SuspendedUntilUtc,
            player.Account.EnforcementReason,
            economy.CalculateNetWorth(player),
            player.Cash,
            player.BankCash,
            player.Turns,
            player.Pimps,
            player.Hoes,
            player.Thugs,
            player.CreatedAtUtc);

    internal static PlayerTargetResponse ToTargetResponse(RankedPlayer ranked, DateTime nowUtc, Player? viewer, GameOptions options, int recentAttacksMade = 0, int recentDefenses = 0, DateTime? viewerLaneReadyAtUtc = null, long viewerNetWorth = 0)
    {
        var player = ranked.Player;
        var mismatch = viewer is null || viewer.Id == player.Id
            ? null
            : AntiFarm.RejectReason(viewerNetWorth, ranked.NetWorth, options.AntiFarm);
        return new PlayerTargetResponse(
            player.Id,
            player.Name,
            player.City,
            player.Account.IsBot,
            player.Account.IsBot ? BotBrain.For(player).Name : null,
            ranked.Rank,
            ranked.NetWorth,
            player.Pimps,
            player.Hoes,
            player.Thugs,
            player.Weapons,
            AverageMorale(player),
            ToCombatReadiness(player, options),
            ToCombatStatus(player, nowUtc, viewer, options, recentAttacksMade, recentDefenses, viewerLaneReadyAtUtc, mismatch));
    }

    internal static PlayerProfileResponse ToProfileResponse(
        RankedPlayer ranked,
        IReadOnlyList<ActivityResponse> publicActivity,
        DateTime nowUtc,
        Player? viewer,
        GameOptions options,
        int recentAttacksMade,
        int recentDefenses,
        DateTime? viewerLaneReadyAtUtc,
        long viewerNetWorth = 0)
    {
        var player = ranked.Player;
        var mismatch = viewer is null || viewer.Id == player.Id
            ? null
            : AntiFarm.RejectReason(viewerNetWorth, ranked.NetWorth, options.AntiFarm);
        return new PlayerProfileResponse(
            player.Id,
            player.Name,
            player.City,
            player.Account.IsBot,
            player.Account.IsBot ? BotBrain.For(player).Name : null,
            ranked.Rank,
            ranked.NetWorth,
            player.Cash,
            player.BankCash,
            player.Pimps,
            player.Hoes,
            player.Thugs,
            player.Weapons,
            player.Weed,
            player.Coke,
            Math.Round(player.HoeHappiness, 2),
            Math.Round(player.ThugHappiness, 2),
            AverageMorale(player),
            ToCombatReadiness(player, options),
            ToCombatStatus(player, nowUtc, viewer, options, recentAttacksMade, recentDefenses, viewerLaneReadyAtUtc, mismatch),
            publicActivity);
    }

    internal static CombatReadinessResponse ToCombatReadiness(Player player, GameOptions options)
    {
        var armedThugs = Math.Min(player.Weapons, player.Thugs);
        var uncoveredThugs = Math.Max(0, player.Thugs - player.Weapons);
        var weaponCoverage = player.Thugs == 0 ? 100 : Math.Round(armedThugs * 100.0 / player.Thugs, 2);
        var averageMorale = AverageMorale(player);
        var power = options.Combat.Power;
        var attackPower = CombatPower.Attack(player.Pimps, player.Thugs, player.Weapons, averageMorale, power);
        var defensePower = CombatPower.Defence(player.Pimps, player.Thugs, player.Weapons, averageMorale, power);
        var riskBand = (averageMorale, weaponCoverage, uncoveredThugs) switch
        {
            (< 35, _, _) => "Fragile",
            (_, < 50, _) => "Exposed",
            (_, _, > 0) => "Underarmed",
            (>= 80, >= 90, 0) => "Ready",
            _ => "Mixed"
        };

        return new CombatReadinessResponse(
            attackPower,
            defensePower,
            armedThugs,
            uncoveredThugs,
            weaponCoverage,
            averageMorale,
            riskBand);
    }

    /// <summary>
    /// Turns a morale baseline into a direction. A null baseline means nothing recent to compare
    /// against, which is reported as "unknown" rather than dressed up as steady: a flat arrow on a
    /// player who has not acted in hours would be a claim the server cannot support.
    /// </summary>
    internal static MoraleTrendResponse ToMoraleTrend(Player player, double? hoeBaseline, double? thugBaseline, MoraleOptions options)
    {
        // Classified on the raw movement and only rounded for display. Rounding first decided direction
        // by the display format: a delta of exactly the band rounded to a tenth and fell under it.
        var hoeDelta = hoeBaseline is null ? (double?)null : player.HoeHappiness - hoeBaseline.Value;
        var thugDelta = thugBaseline is null ? (double?)null : player.ThugHappiness - thugBaseline.Value;
        return new MoraleTrendResponse(
            hoeDelta is null ? null : Math.Round(hoeDelta.Value, 1),
            thugDelta is null ? null : Math.Round(thugDelta.Value, 1),
            Direction(hoeDelta, options.TrendFlatBand),
            Direction(thugDelta, options.TrendFlatBand),
            options.TrendWindowHours);

        static string Direction(double? delta, double flatBand) => delta switch
        {
            null => "unknown",
            var value when value >= flatBand => "up",
            var value when value <= -flatBand => "down",
            _ => "steady"
        };
    }

    internal static HideoutResponse ToHideoutResponse(Player player, HideoutService hideouts, DateTime nowUtc, GameOptions options)
    {
        var capacity = hideouts.CapacityFor(player.Hideout);
        var nextTier = hideouts.NextTier(player.Hideout);
        var building = player.Hideout is { UpgradingToTier: { } tier, UpgradeCompletesAtUtc: { } due }
            ? new HideoutBuildResponse(
                tier,
                hideouts.TierName(tier),
                due,
                Math.Max(0, (int)Math.Ceiling((due - nowUtc).TotalSeconds)))
            : null;

        return new HideoutResponse(
            capacity.TierName,
            capacity.Tier,
            capacity.StorageLevel,
            capacity.SafeLevel,
            capacity.WeedLabLevel,
            capacity.CokeLabLevel,
            capacity.MaxPimps,
            capacity.MaxHoes,
            capacity.MaxThugs,
            capacity.MaxCash,
            capacity.MaxCondoms,
            capacity.MaxBeer,
            capacity.MaxWeapons,
            capacity.MaxWeed,
            capacity.MaxCoke,
            hideouts.ProductionYieldBonusPercent(player.Hideout, "weed"),
            hideouts.ProductionYieldBonusPercent(player.Hideout, "coke"),
            hideouts.PassivePerHour(player.Hideout, "weed"),
            hideouts.PassivePerHour(player.Hideout, "coke"),
            options.Hideout.MaxOfflineProductionHours,
            ToRoomUpgrade(hideouts, player.Hideout, "storage"),
            ToRoomUpgrade(hideouts, player.Hideout, "safe"),
            ToRoomUpgrade(hideouts, player.Hideout, "weedlab"),
            ToRoomUpgrade(hideouts, player.Hideout, "cokelab"),
            nextTier is null
                ? null
                : new HideoutTierUpgradeResponse(
                    nextTier.Level,
                    nextTier.Name,
                    nextTier.UpgradeCost,
                    nextTier.UpgradeTurns,
                    nextTier.BuildMinutes,
                    nextTier.MaxPimps,
                    nextTier.MaxHoes,
                    nextTier.MaxThugs),
            building);
    }

    private static HideoutRoomUpgradeResponse? ToRoomUpgrade(HideoutService hideouts, Hideout? hideout, string room)
        => hideouts.NextUpgrade(hideout, room) is { } next
            ? new HideoutRoomUpgradeResponse(next.Level, next.Cost, next.RequiredTier, hideouts.TierName(next.RequiredTier), next.TierLocked)
            : null;

    internal static PimpResponse ToPimpResponse(Pimp pimp, IReadOnlyCollection<long> commandingPimpIds)
        => new(
            pimp.Id,
            pimp.Name,
            pimp.Specialty,
            pimp.BonusPercent,
            Math.Round(pimp.Loyalty, 2),
            pimp.MissionsLed,
            pimp.Victories,
            commandingPimpIds.Contains(pimp.Id),
            pimp.HiredAtUtc,
            pimp.LostAtUtc,
            pimp.LostReason);

    internal static CombatCrewResponse ToCombatCrewResponse(CombatCommitment commitment)
        => new(
            commitment.CommittedPimps,
            commitment.CommittedThugs,
            commitment.CommittedWeapons,
            commitment.AvailablePimps,
            commitment.AvailableThugs,
            commitment.AvailableWeapons,
            commitment.ActiveAttackMissions,
            commitment.MaxActiveAttackMissions);

    internal static CombatStatusResponse ToCombatStatus(
        Player player,
        DateTime nowUtc,
        Player? viewer,
        GameOptions options,
        int recentAttacksMade = 0,
        int recentDefenses = 0,
        DateTime? viewerLaneReadyAtUtc = null,
        string? mismatchReason = null)
    {
        var combat = options.Combat;
        var isProtected = player.CombatProtectionUntilUtc is { } protectionUntil && protectionUntil > nowUtc;
        var isOnCooldown = viewerLaneReadyAtUtc is { } readyAt && readyAt > nowUtc;
        var hasViewer = viewer is not null;
        var isSelf = viewer?.Id == player.Id;
        var hasTurns = viewer?.Turns >= combat.AttackTurnCost;
        var eligibility = isSelf
            ? "Self"
            : isProtected
                ? "Protected"
                : mismatchReason is not null
                    ? "Mismatched"
                    : isOnCooldown
                        ? "Cooldown"
                        : !hasTurns
                            ? "Need Turns"
                            : "Eligible";

        return new CombatStatusResponse(
            isProtected,
            player.CombatProtectionUntilUtc,
            player.LastAttackAtUtc,
            player.LastAttackedAtUtc,
            viewerLaneReadyAtUtc,
            hasViewer && !isSelf && !isProtected && !isOnCooldown && hasTurns && mismatchReason is null,
            combat.AttackTurnCost,
            recentAttacksMade,
            recentDefenses,
            eligibility,
            mismatchReason);
    }

    internal static CombatLogResponse ToCombatLogResponse(CombatLog log)
        => new(
            log.Id,
            log.AttackerId,
            log.Attacker.Name,
            log.DefenderId,
            log.Defender.Name,
            log.Outcome,
            log.Summary,
            log.TurnsSpent,
            log.AttackerPower,
            log.DefenderPower,
            log.CashStolen,
            log.WeedStolen,
            log.CokeStolen,
            log.AttackerPimpsLost,
            log.AttackerHoesLost,
            log.AttackerThugsLost,
            log.AttackerWeaponsLost,
            log.DefenderPimpsLost,
            log.DefenderHoesLost,
            log.DefenderThugsLost,
            log.DefenderWeaponsLost,
            log.DefenderProtectionUntilUtc,
            log.ResolvesAtUtc,
            log.ResolvedAtUtc,
            log.CreatedAtUtc);

    internal static CombatMissionResponse ToCombatMissionResponse(CombatMission mission)
        => new(
            mission.Id,
            mission.AttackerId,
            mission.Attacker.Name,
            mission.DefenderId,
            mission.Defender.Name,
            mission.Status,
            mission.Outcome,
            mission.Summary,
            mission.TurnsSpent,
            mission.AssignedPimps,
            mission.CommanderName,
            mission.CommanderBonusPercent,
            mission.AssignedThugs,
            mission.AssignedWeapons,
            mission.RemainingAttackers,
            mission.RemainingWeapons,
            Math.Round(mission.AttackerMorale, 2),
            Math.Round(mission.DefenderMorale, 2),
            mission.CurrentRound,
            mission.MaxRounds,
            mission.AttackerPower,
            mission.DefenderPower,
            mission.LootMultiplierPercent,
            mission.DefenderRecentHits,
            mission.DefenderProtectionMinutes,
            mission.CashStolen,
            mission.WeedStolen,
            mission.CokeStolen,
            mission.StartedAtUtc,
            mission.ArrivesAtUtc,
            mission.NextRoundAtUtc,
            mission.ReturnsAtUtc,
            mission.CompletedAtUtc,
            mission.DefenderProtectionUntilUtc,
            CombatMissionService.CanCancel(mission),
            CombatMissionService.CancelCashCost(mission),
            mission.Events
                .OrderByDescending(x => x.CreatedAtUtc)
                .ThenByDescending(x => x.Id)
                .DistinctBy(CombatMissionEventDedupeKey)
                .Take(8)
                .Select(ToCombatMissionEventResponse)
                .ToList());

    internal static (Guid AttackerId, Guid DefenderId, string Outcome, string Summary, DateTime CreatedAtUtc) CombatLogDedupeKey(CombatLog log)
        => (log.AttackerId, log.DefenderId, log.Outcome, log.Summary, log.CreatedAtUtc);

    internal static (int Round, string Kind, string Summary, DateTime CreatedAtUtc) CombatMissionEventDedupeKey(CombatMissionEvent entry)
        => (entry.Round, entry.Kind, entry.Summary, entry.CreatedAtUtc);

    internal static CombatMissionEventResponse ToCombatMissionEventResponse(CombatMissionEvent entry)
        => new(
            entry.Id,
            entry.Round,
            entry.Kind,
            entry.Summary,
            Math.Round(entry.AttackRoll, 2),
            Math.Round(entry.DefenseRoll, 2),
            Math.Round(entry.AttackerMorale, 2),
            Math.Round(entry.DefenderMorale, 2),
            entry.AttackerThugsLost,
            entry.DefenderThugsLost,
            entry.AttackerWeaponsLost,
            entry.DefenderWeaponsLost,
            entry.CreatedAtUtc);
}
