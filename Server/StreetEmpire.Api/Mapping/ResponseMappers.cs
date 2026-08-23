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

    internal static List<CityMarketResponse> ToCityMarkets(GameOptions options, Player player)
    {
        var homeValue = LoadValue(options, player, player.City);
        return options.CityMarkets.Profiles
            .OrderBy(x => x.City)
            .Select(x =>
            {
                var current = string.Equals(x.City, player.City, StringComparison.OrdinalIgnoreCase);
                return new CityMarketResponse(
                    x.City,
                    x.Weed,
                    x.Coke,
                    x.Risk,
                    options.CityMarkets.BustChancePercent(x.City),
                    current ? null : BreakEvenSeizurePercent(options, player, x.City, homeValue),
                    options.CityMarkets.ProductPrice(x.City, "weed", options.WeedSellPrice),
                    options.CityMarkets.ProductPrice(x.City, "coke", options.CokeSellPrice),
                    options.CityMarkets.TravelTurns(x.City),
                    current);
            })
            .ToList();
    }

    /// <summary>What the carried product would fetch in a given town, at that town's prices.</summary>
    private static long LoadValue(GameOptions options, Player player, string city)
        => (long)player.Weed * options.CityMarkets.ProductPrice(city, "weed", options.WeedSellPrice)
           + (long)player.Coke * options.CityMarkets.ProductPrice(city, "coke", options.CokeSellPrice);

    /// <summary>
    /// A stop takes a share of the load, so the run beats staying home only while
    /// (1 - share) * thereValue > hereValue. Solving for the share gives the point where the trip
    /// stops paying for itself, which is the number that says how much a stop here actually costs.
    /// </summary>
    private static int? BreakEvenSeizurePercent(GameOptions options, Player player, string city, long homeValue)
    {
        var destinationValue = LoadValue(options, player, city);
        if (destinationValue <= 0) return null;

        var share = 1 - (double)homeValue / destinationValue;
        return Math.Max(0, (int)Math.Round(share * 100, MidpointRounding.AwayFromZero));
    }

    internal static PlayerTargetResponse ToTargetResponse(RankedPlayer ranked, DateTime nowUtc, Player? viewer, GameOptions options, int recentAttacksMade = 0, int recentDefenses = 0, DateTime? viewerLaneReadyAtUtc = null, long viewerPlunder = 0, IReadOnlyList<PlayerTitleResponse>? titles = null)
    {
        var player = ranked.Player;
        var mismatch = viewer is null || viewer.Id == player.Id
            ? null
            // What a raid could carry off on both sides. The row still shows net worth; it is only
            // the question of who may fight whom that a building has no business answering.
            : AntiFarm.RejectReason(viewerPlunder, ranked.Plunder, options.AntiFarm);
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
            TitleService.For(player.Id, titles ?? []),
            player.Rides,
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
        long viewerPlunder = 0,
        IReadOnlyList<PlayerTitleResponse>? titles = null,
        IReadOnlyDictionary<string, string>? strikeBlockers = null)
    {
        var player = ranked.Player;
        var mismatch = viewer is null || viewer.Id == player.Id
            ? null
            : AntiFarm.RejectReason(viewerPlunder, ranked.Plunder, options.AntiFarm);
        return new PlayerProfileResponse(
            strikeBlockers ?? new Dictionary<string, string>(),
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
            ToWeaponRack(player, options),
            TitleService.For(player.Id, titles ?? []),
            player.Rides,
            player.Medicine,
            player.Weed,
            player.Coke,
            Math.Round(player.HoeHappiness, 2),
            Math.Round(player.ThugHappiness, 2),
            AverageMorale(player),
            ToCombatReadiness(player, options),
            ToCombatStatus(player, nowUtc, viewer, options, recentAttacksMade, recentDefenses, viewerLaneReadyAtUtc, mismatch),
            publicActivity);
    }

    /// <summary>Where a shift can be worked, and what each place is actually for.</summary>
    internal static List<StreetDistrictResponse> ToDistricts(GameOptions options)
        => options.StreetAction.Districts
            .Select(x => new StreetDistrictResponse(
                x.Key,
                x.Name,
                x.Blurb,
                x.IsDefault,
                x.GrossPercent,
                x.HoeRecruitPercent,
                x.ThugRecruitPercent,
                x.PimpRecruitPercent,
                x.FindPercent,
                x.HeatPercent))
            .ToList();

    /// <summary>The gun rack as the client sees it: what is held, what it costs, what it is worth.</summary>
    internal static List<WeaponTierResponse> ToWeaponRack(Player player, GameOptions options)
        => options.Weapons
            .OrderBy(x => x.Price)
            .Select(x => new WeaponTierResponse(
                x.Key,
                WeaponTiers.Label(x.Key),
                player.Armoury.Of(x.Key),
                x.Price,
                x.Firepower,
                x.CanForge ? x.ForgeCost : null,
                x.CanForge ? x.MinWorkshopLevel : null))
            .ToList();

    internal static CombatReadinessResponse ToCombatReadiness(Player player, GameOptions options)
    {
        var armedThugs = Math.Min(player.Weapons, player.Thugs);
        var uncoveredThugs = Math.Max(0, player.Thugs - player.Weapons);
        var weaponCoverage = player.Thugs == 0 ? 100 : Math.Round(armedThugs * 100.0 / player.Thugs, 2);
        var averageMorale = AverageMorale(player);
        var power = options.Combat.Power;
        var firepower = Firepower.Of(player.Armoury, player.Thugs, options.WeaponFirepower());
        var attackPower = CombatPower.Attack(player.Pimps, player.Thugs, firepower, averageMorale, power);
        var defensePower = CombatPower.Defence(player.Pimps, player.Thugs, firepower, averageMorale, power);
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
            Math.Round(firepower.InPistols, 1),
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
        var heat = hideouts.HeatFor(player);
        var nextTier = hideouts.NextTier(player.Hideout);
        var building = player.Hideout is { UpgradingToTier: { } tier, UpgradeCompletesAtUtc: { } due }
            ? new HideoutBuildResponse(
                tier,
                hideouts.TierName(tier),
                due,
                Math.Max(0, (int)Math.Ceiling((due - nowUtc).TotalSeconds)))
            : null;

        var buildingValue = HideoutValue.Of(player.Hideout, options);

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
            capacity.MaxRides,
            capacity.MaxCash,
            capacity.MaxCondoms,
            capacity.MaxBeer,
            capacity.MaxWeapons,
            capacity.MaxWeed,
            capacity.MaxCoke,
            capacity.MaxMoonshine,
            capacity.MaxCut,
            capacity.MaxMedicine,
            capacity.MaxPoison,
            hideouts.ProductionYieldBonusPercent(player.Hideout, "weed"),
            hideouts.ProductionYieldBonusPercent(player.Hideout, "coke"),
            hideouts.PassivePerHour(player.Hideout, "weed"),
            hideouts.PassivePerHour(player.Hideout, "coke"),
            options.Hideout.MaxOfflineProductionHours,
            player.Hideout?.IntelligenceLevel ?? 0,
            hideouts.ConcurrentRunCap(player.Hideout),
            player.Hideout?.LookoutLevel ?? 0,
            (int)Math.Round(hideouts.BustRiskReduction(player.Hideout) * 100),
            Math.Round(heat, 1),
            HeatLabel(heat, options),
            HeatDetail(heat, options),
            HeatNote(heat, options, player.City),
            buildingValue,
            ToRoomUpgrade(hideouts, player.Hideout, "storage"),
            ToRoomUpgrade(hideouts, player.Hideout, "safe"),
            ToRoomUpgrade(hideouts, player.Hideout, "weedlab", options, player.City),
            ToRoomUpgrade(hideouts, player.Hideout, "cokelab", options, player.City),
            ToRoomUpgrade(hideouts, player.Hideout, "intelligence"),
            ToRoomUpgrade(hideouts, player.Hideout, "lookout"),
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
            building,
            Stations(player, hideouts, options));
    }

    /// <summary>
    /// The three making stations, each next to the price it is meant to beat. A station whose output
    /// costs more than the thing it replaces has no reason to exist, so the comparison is shown rather
    /// than left for the player to work out.
    /// </summary>
    private static List<HideoutStationResponse> Stations(Player player, HideoutService hideouts, GameOptions options)
    {
        var cokePrice = options.CityMarkets.ProductPrice(player.City, "coke", options.CokeSellPrice);
        // What the workshop is currently turning out: the best gun its level has unlocked. That decides
        // both the price it is meant to beat and the cost it is quoted at, so an upgrade shows up here as
        // a different product rather than as the same one slightly cheaper.
        var workshopLevel = hideouts.WorkshopFor(player.Hideout);
        // An unbuilt shop advertises what building one would actually get you, which is the first
        // level's reach rather than the cheapest gun in the game. The panel's whole job is to say what
        // the money buys, and "makes pistols" undersells a shop that turns out shotguns on day one.
        var reach = Math.Max(1, workshopLevel?.Level ?? 1);
        var forging = options.Weapons
            .Where(x => x.CanForge && x.MinWorkshopLevel <= reach)
            .OrderByDescending(x => x.Price)
            .FirstOrDefault();
        var forgeGood = forging?.Key ?? WeaponTiers.Pistol;

        // One bench, and everything it can currently turn out. The still and the mix house used to be
        // rooms of their own; they are recipes now, and a recipe the shop cannot reach yet is still
        // listed so the player can see what the next level buys rather than discovering it afterwards.
        var level = player.Hideout?.WorkshopLevel ?? 0;
        var throughput = workshopLevel?.Throughput ?? 0;
        var upgrade = ToRoomUpgrade(hideouts, player.Hideout, "workshop");

        var rows = new List<HideoutStationResponse>
        {
            new(
                "workshop",
                "Workshop",
                forgeGood,
                level,
                throughput,
                forging?.ForgeCost ?? 0,
                forging?.Price ?? options.WeaponPrice,
                $"store {WeaponTiers.Label(forgeGood).ToLowerInvariant()}",
                HeatPerUnit(options, forgeGood),
                upgrade)
        };

        foreach (var recipe in options.Makeables.Where(x => x.CanMake).OrderBy(x => x.MinWorkshopLevel))
        {
            // What the thing is measured against: moonshine stands in for beer, cut for a share of the
            // coke it stretches. The same comparisons the separate rooms used to draw.
            var (compare, compareLabel) = recipe.Key switch
            {
                "moonshine" => ((long)options.BeerPrice, "store beer"),
                "cut" => (Math.Max(1, cokePrice / 4), "what it stretches"),
                "condoms" => ((long)options.CondomPrice, "store condoms"),
                "medicine" => ((long)options.MedicinePrice, "store medicine"),
                _ => ((long)options.PoisonPrice, "store poison")
            };

            rows.Add(new HideoutStationResponse(
                recipe.Key,
                TradeGoods.Label(recipe.Key),
                recipe.Key,
                // A recipe has no level of its own any more. What it reports is the shop that makes it,
                // or nothing when the shop cannot reach it yet.
                level >= recipe.MinWorkshopLevel ? level : 0,
                level >= recipe.MinWorkshopLevel ? recipe.PerTurn * Math.Max(1, throughput) : 0,
                recipe.MaterialCost,
                compare,
                compareLabel,
                HeatPerUnit(options, recipe.Key),
                upgrade));
        }

        return rows;
    }

    /// <summary>
    /// Heat in words, because the number on its own says nothing about whether tonight is the night.
    /// The floor is the honest dividing line: under it nobody is looking, however long you sit there.
    /// </summary>
    private static string HeatLabel(double heat, GameOptions options)
    {
        var floor = options.Hideout.HeatBustFloor;
        return heat switch
        {
            var value when value <= floor => "Quiet",
            var value when value <= floor * 2 => "Noticed",
            var value when value <= floor * 4 => "Watched",
            _ => "Hunted"
        };
    }

    /// <summary>
    /// The same reading in a few words, for the status strip. The strip is on every page, so it gets
    /// the number and the odds; the sentence explaining what to do about it is the tooltip.
    /// </summary>
    private static string HeatDetail(double heat, GameOptions options)
    {
        var rounded = Math.Round(heat);
        return heat <= options.Hideout.HeatBustFloor
            ? $"{rounded:N0} heat, nobody looking"
            : $"{rounded:N0} heat, {RaidChance(heat, options):P0} an hour";
    }

    private static double RaidChance(double heat, GameOptions options)
    {
        var config = options.Hideout;
        return Math.Clamp((heat - config.HeatBustFloor) * config.BustChancePerHeat, 0, Math.Clamp(config.MaxBustChancePerHour, 0, 1));
    }

    private static string HeatNote(double heat, GameOptions options, string city)
    {
        var config = options.Hideout;
        // Named, because a player who moves and watches their heat jump deserves to know it was the
        // town rather than something they did.
        var town = options.CityMarkets.HeatMultiplier(city) switch
        {
            > 1.05 => $" {city} watches harder than most towns.",
            < 0.95 => $" {city} pays less attention than most towns.",
            _ => string.Empty
        };
        if (heat <= config.HeatBustFloor)
            return $"Nobody is looking your way. Nothing you hold is worth a door being kicked in yet.{town}";
        return $"Roughly a {RaidChance(heat, options):P0} chance an hour of a raid. Sell down, or lie low: heat falls {config.HeatDecayPerHour:N0} an hour on its own.{town}";
    }

    /// <summary>
    /// How much notice one unit of a good draws while it is held. Every station here makes something
    /// illegal, so a plain legal/illegal flag told the player nothing; this is the part that differs.
    /// </summary>
    private static double HeatPerUnit(GameOptions options, string good) => good switch
    {
        "coke" => options.Hideout.CokeHeatPerUnit,
        "moonshine" => options.Hideout.MoonshineHeatPerUnit,
        "weed" => options.Hideout.WeedHeatPerUnit,
        "cut" => options.Hideout.CutHeatPerUnit,
        _ => 0
    };

    private static HideoutRoomUpgradeResponse? ToRoomUpgrade(
        HideoutService hideouts,
        Hideout? hideout,
        string room,
        GameOptions? options = null,
        string? city = null)
        => hideouts.NextUpgrade(hideout, room) is { } next
            ? new HideoutRoomUpgradeResponse(
                next.Level,
                next.Cost,
                next.RequiredTier,
                hideouts.TierName(next.RequiredTier),
                next.TierLocked,
                PaybackDays(options, city, room, hideout, next.Level, next.Cost))
            : null;

    /// <summary>
    /// How long the room's own output takes to cover what the upgrade costs, in days.
    ///
    /// Only the labs produce anything on their own, so only the labs can answer. The later levels are
    /// deliberately a bad return - a sink for money with nowhere else to go - and this is what stops
    /// that from being a secret: the number gets worse as you climb, and a player can see it getting
    /// worse before they spend rather than afterwards.
    /// </summary>
    private static int? PaybackDays(
        GameOptions? options,
        string? city,
        string room,
        Hideout? hideout,
        int nextLevel,
        long cost)
    {
        if (options is null || cost <= 0) return null;

        var (levels, good) = room switch
        {
            "weedlab" => (options.Hideout.WeedLab, "weed"),
            "cokelab" => (options.Hideout.CokeLab, "coke"),
            _ => (null, string.Empty)
        };
        if (levels is null) return null;

        var now = levels.SingleOrDefault(x => x.Level == (room == "weedlab" ? hideout?.WeedLabLevel ?? 0 : hideout?.CokeLabLevel ?? 0));
        var then = levels.SingleOrDefault(x => x.Level == nextLevel);
        if (then is null) return null;

        // What the upgrade adds rather than what the room makes: the level below was already paid for.
        var gained = then.PassivePerHour - (now?.PassivePerHour ?? 0);
        if (gained <= 0) return null;

        var price = TradeGoods.ReferencePrice(options, good, city ?? string.Empty);
        var perDay = (long)gained * 24 * price;
        return perDay <= 0 ? null : (int)Math.Ceiling(cost / (double)perDay);
    }

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
        var isStrikeProtected = player.StrikeProtectionUntilUtc is { } strikeUntil && strikeUntil > nowUtc;
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
            isStrikeProtected,
            player.StrikeProtectionUntilUtc,
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
            log.Method,
            AttackMethods.Label(log.Method),
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
            log.HoesTaken,
            log.RidesTaken,
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
