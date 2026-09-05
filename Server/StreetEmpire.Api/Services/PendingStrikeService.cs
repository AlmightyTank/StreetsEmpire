using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

/// <summary>
/// Strikes thrown at houses in other towns, from the moment the car leaves to the moment it is back.
///
/// A strike against a neighbour is still instant and still lives in <see cref="StreetStrikeService"/>.
/// This is what a strike becomes once there is a drive in front of it: a commitment now, an outcome
/// later, and a load coming home after that. The rules of what a drive-by does to a guard are not
/// duplicated here - they are the same ones, called at landing with the distance as an argument.
///
/// Nothing about it is scheduled. Due trips are settled by <see cref="ResolveDueAsync"/>, which every
/// busy endpoint calls exactly as it already calls the raid resolver, so the game's own traffic is the
/// clock. A quiet server settles nothing and needs to settle nothing; the first person through the
/// door moves everybody's cars.
/// </summary>
public sealed class PendingStrikeService(
    GameDbContext db,
    IOptionsSnapshot<GameOptions> options,
    IGameRandom random,
    StreetStrikeService strikes,
    HideoutService hideouts,
    TerritoryService territories,
    AllianceService alliances)
{
    private readonly GameOptions _options = options.Value;

    /// <summary>
    /// Whoever is actually standing in a house right now, with whatever they still have to hold it.
    ///
    /// Shared by the instant path and the landing path on purpose. This is the number a strike is
    /// decided against, and two copies of it would be two answers to "who is home" - which is exactly
    /// the sort of thing that stays in step for a month and then quietly does not.
    ///
    /// Crew already out attacking somebody else cannot also be guarding the garage, and neither can the
    /// guns that went with them: a raiding party takes the best of the rack, which is what makes hitting
    /// a player who is mid-raid the opening it should be.
    /// </summary>
    public async Task<StrikeDefence> DefenceForAsync(Player defender, CancellationToken ct)
    {
        // The same query CombatMissionService.ActiveAttackMissions runs, asked directly rather than
        // by dragging the whole raid service in behind it. Three lines against ten dependencies, and
        // this is the only thing that would have been used.
        var committed = await db.CombatMissions.AsNoTracking()
            .Where(x => x.AttackerId == defender.Id && x.Status != "Complete")
            .Select(x => new { x.RemainingAttackers, x.CarriedPistols, x.CarriedShotguns, x.CarriedSmgs, x.CarriedRifles })
            .ToListAsync(ct);
        var away = committed.Aggregate(
            Armoury.Empty,
            (rack, x) => rack + new Armoury(x.CarriedPistols, x.CarriedShotguns, x.CarriedSmgs, x.CarriedRifles));

        // Ground is held in the town the house is in, which is not necessarily the town its owner is
        // standing in. A player who flew to Las Vegas has not taken their alliance's corners with them.
        var cityControl = defender.AllianceId is { } allianceId
            ? await territories.CityControlThugsForAllianceInCityAsync(allianceId, HideoutService.HomeCity(defender), ct)
            : 0;

        return new StrikeDefence(
            Math.Max(0, defender.Thugs - committed.Sum(x => x.RemainingAttackers)) + cityControl,
            defender.Armoury - away);
    }

    /// <summary>How far it is from one house to another, which on this map is a property of the target.</summary>
    public int TravelTurnsBetween(Player attacker, Player defender)
    {
        var from = HideoutService.HomeCity(attacker);
        var to = HideoutService.HomeCity(defender);
        return string.Equals(from, to, StringComparison.OrdinalIgnoreCase)
            ? 0
            : Math.Max(1, _options.CityMarkets.TravelTurns(to));
    }

    /// <summary>
    /// The odds the drive home ends badly, for a haul taken out of this town at this distance.
    ///
    /// The town's own risk is the floor, because you have just done something loud in it and people
    /// are looking; the distance is what is added, because a longer drive is more road to be stopped
    /// on. Never a certainty however far it is - a road that always ends badly is a road nobody takes,
    /// and then a jacking is simply a local verb again.
    ///
    /// Quoted to the attacker before they commit and written on the row when they do, so the number
    /// they are judged by is the number they were shown.
    /// </summary>
    public double ReturnRiskFor(string? targetCity, int travelTurns)
    {
        if (travelTurns <= 0) return 0;
        var distance = _options.Strikes.Distance;
        return Math.Clamp(
            _options.CityMarkets.BustChance(targetCity) + Math.Max(0, distance.ReturnRiskPerTravelTurn) * travelTurns,
            0,
            Math.Clamp(distance.MaxReturnRisk, 0, 1));
    }

    /// <summary>
    /// Puts a strike on the road, taking everything it will need with it.
    ///
    /// The materials leave the house now rather than at landing, for the reason a mule run freezes its
    /// numbers at launch: a crew already driving must not be re-equipped by somebody sitting at home
    /// buying poison. What they do not use comes back with them.
    /// </summary>
    public async Task<PendingStrike> LaunchAsync(
        Player attacker,
        Player defender,
        CombatAttackRequest request,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var distance = _options.Strikes.Distance;
        if (!distance.Allowed)
            throw new GameRuleException(
                $"Your crew do not leave {HideoutService.HomeCity(attacker)}. {defender.Name} runs {HideoutService.HomeCity(defender)}.");

        var method = AttackMethods.Normalize(request.Method);
        var travelTurns = TravelTurnsBetween(attacker, defender);
        var target = HideoutService.HomeCity(defender);

        var inFlight = await db.PendingStrikes
            .CountAsync(x => x.AttackerId == attacker.Id && x.Status != PendingStrikeStatus.Done, ct);
        if (inFlight >= Math.Max(1, distance.MaxInFlight))
            throw new GameRuleException(
                $"You already have {inFlight} crew out on the road, which is all you can keep track of at once.");

        // Every refusal a local strike would give, asked before a penny is spent. The two that can go
        // stale on the road - the shield and the anti-farm band - are asked again on arrival.
        strikes.EnsureCanThrow(attacker, defender, method, request, nowUtc);

        var turnCost = strikes.TurnCostOf(method, travelTurns);
        if (attacker.Turns < turnCost)
            throw new GameRuleException(
                $"A {AttackMethods.Label(method)} in {target} costs {turnCost:N0} turns - {strikes.TurnCostOf(method):N0} for the job and the rest for the drive.");

        var fare = strikes.FareFor(travelTurns);
        if (Capital.Available(attacker) < fare)
            throw new GameRuleException(
                $"Getting a crew to {target} and back costs {fare:C0} in petrol and plates. You have {Capital.Available(attacker):C0}.");

        attacker.Turns -= turnCost;
        Capital.Charge(attacker, fare);

        var legMinutes = Math.Max(1, travelTurns * Math.Max(1, _options.Mules.MinutesPerTravelTurn));
        var strike = new PendingStrike
        {
            AttackerId = attacker.Id,
            Attacker = attacker,
            DefenderId = defender.Id,
            Defender = defender,
            Method = method,
            OriginCity = HideoutService.HomeCity(attacker),
            TargetCity = target,
            TravelTurns = travelTurns,
            LaunchedAtUtc = nowUtc,
            ArrivesAtUtc = nowUtc.AddMinutes(legMinutes),
            ReturnsAtUtc = nowUtc.AddMinutes(legMinutes * 2),
            Status = PendingStrikeStatus.Outbound,
            TurnsSpent = turnCost,
            Fare = fare,
            ReturnRiskPercent = ReturnRiskFor(target, travelTurns) * 100
        };

        Commit(strike, attacker, request);
        strike.Summary =
            $"{attacker.Name}'s crew left for {target}. They arrive in about {legMinutes} minute(s).";
        db.PendingStrikes.Add(strike);
        return strike;
    }

    /// <summary>
    /// Takes what the trip needs out of the house and writes it on the row.
    ///
    /// A jacking is missing on purpose: it is the one strike that costs nothing to throw, since what it
    /// spends is the risk to the thugs who go, and they are not a column.
    /// </summary>
    private void Commit(PendingStrike strike, Player attacker, CombatAttackRequest request)
    {
        switch (strike.Method)
        {
            case AttackMethods.DriveBy:
                attacker.Rides -= 1;
                strike.CommittedRides = 1;
                break;

            case AttackMethods.Infest:
                // Everything they own goes in the boot. An infestation reaches as far as the doses it
                // brought, and deciding how many to carry is not a decision the game asks for - what is
                // left over comes home again.
                strike.CommittedPoison = attacker.Poison;
                attacker.Poison = 0;
                break;

            case AttackMethods.Poach:
                var offer = Math.Min(attacker.Coke, Math.Max(_options.Strikes.Poach.CokePerHoe, request.Coke));
                strike.CommittedCoke = offer;
                strike.CommittedCokePurity = attacker.CokePurity;
                attacker.Coke -= offer;
                break;
        }
    }

    /// <summary>
    /// Settles every trip whose clock has run out: arrivals first, then the ones already driving home.
    ///
    /// Mirrors <see cref="CombatMissionService.ResolveDueAsync"/> in shape and is called from the same
    /// places, so a strike on the road and a raid on the road are moved by exactly the same traffic.
    /// </summary>
    public async Task<int> ResolveDueAsync(DateTime nowUtc, CancellationToken ct)
    {
        var due = await db.PendingStrikes
            .Include(x => x.Attacker).ThenInclude(x => x.Hideout)
            .Include(x => x.Defender).ThenInclude(x => x.Hideout)
            .Include(x => x.Defender).ThenInclude(x => x.Crew)
            .Where(x => x.Status != PendingStrikeStatus.Done
                        && ((x.Status == PendingStrikeStatus.Outbound && x.ArrivesAtUtc <= nowUtc)
                            || (x.Status == PendingStrikeStatus.Returning && x.ReturnsAtUtc <= nowUtc)))
            .OrderBy(x => x.ArrivesAtUtc)
            .Take(50)
            .ToListAsync(ct);
        if (due.Count == 0) return 0;

        var settled = 0;
        foreach (var strike in due)
        {
            if (strike.Status == PendingStrikeStatus.Outbound)
            {
                await LandAsync(strike, nowUtc, ct);
                settled++;
            }

            // A trip that landed on this same pass can come home on it too, when the map is small
            // enough that both clocks have already run out.
            if (strike.Status == PendingStrikeStatus.Returning && strike.ReturnsAtUtc <= nowUtc)
            {
                ComeHome(strike, nowUtc);
                settled++;
            }
        }

        await db.SaveChangesAsync(ct);
        return settled;
    }

    /// <summary>
    /// The crew arrive. Whatever is standing in that house now is what they meet, which is the entire
    /// reason a warning is worth having.
    /// </summary>
    private async Task LandAsync(PendingStrike strike, DateTime nowUtc, CancellationToken ct)
    {
        var attacker = strike.Attacker;
        var defender = strike.Defender;

        // The two rules that can go stale on the road. Neither is the attacker's fault and neither is a
        // bad roll, so the load goes back in the boot and comes home - they kept the turns and the fare,
        // which is what the trip actually cost.
        if (defender.StrikeProtectionUntilUtc is { } shield && shield > nowUtc)
        {
            TurnAround(strike, nowUtc, $"Somebody had already been through {defender.Name}'s place. The crew turned round.");
            return;
        }

        if (defender.CombatProtectionUntilUtc is { } raidShield && raidShield > nowUtc)
        {
            TurnAround(strike, nowUtc, $"{defender.Name}'s house had just been broken open. The crew turned round.");
            return;
        }

        if (await alliances.AreAlliedAsync(attacker, defender, ct))
        {
            TurnAround(strike, nowUtc, $"{defender.Name} runs with your crew now. The car came back.");
            return;
        }

        if (AntiFarm.RejectReason(
                EconomyService.PlunderOf(attacker, _options),
                EconomyService.PlunderOf(defender, _options),
                _options.AntiFarm) is not null)
        {
            TurnAround(strike, nowUtc, $"{defender.Name} is not worth the drive any more. The crew turned round.");
            return;
        }

        // Back into the crew's hands, exactly as they left with it, so the strike consumes what a local
        // one would consume and nothing else. What it does not use is still theirs and drives home.
        attacker.Rides += strike.CommittedRides;
        attacker.Poison += strike.CommittedPoison;
        attacker.AddCoke(strike.CommittedCoke, strike.CommittedCokePurity);

        var ridesBefore = attacker.Rides;
        var poisonBefore = attacker.Poison;
        var cokeBefore = attacker.Coke;
        var hoesBefore = attacker.Hoes;

        var defence = await DefenceForAsync(defender, ct);
        var request = new CombatAttackRequest(defender.Id, Method: strike.Method, Coke: strike.CommittedCoke);
        var result = strikes.ResolveLanded(attacker, defender, request, defence, nowUtc, strike.TravelTurns);

        // Everything the crew still have is on the road, not in the garage. Taken back off the attacker
        // and handed over at the door, which is the only way a jacked car does not teleport home.
        strike.ReturningRides = Math.Max(0, attacker.Rides - (ridesBefore - strike.CommittedRides));
        strike.ReturningPoison = Math.Max(0, attacker.Poison - (poisonBefore - strike.CommittedPoison));
        strike.ReturningCoke = Math.Max(0, attacker.Coke - (cokeBefore - strike.CommittedCoke));
        strike.ReturningCokePurity = attacker.CokePurity;
        strike.ReturningHoes = Math.Max(0, attacker.Hoes - hoesBefore);

        attacker.Rides -= strike.ReturningRides;
        attacker.Poison -= strike.ReturningPoison;
        attacker.Coke -= strike.ReturningCoke;
        attacker.Hoes -= strike.ReturningHoes;

        db.CombatLogs.Add(result.Log);
        strike.Status = PendingStrikeStatus.Returning;
        strike.Outcome = result.Outcome;
        strike.ResolvedAtUtc = nowUtc;
        strike.Summary = result.Log.Summary;
    }

    /// <summary>
    /// A trip that found nothing to do. The load goes back in the boot and the crew drive home, which
    /// is the same journey they were always making - only the middle of it did not happen.
    /// </summary>
    private static void TurnAround(PendingStrike strike, DateTime nowUtc, string why)
    {
        strike.ReturningRides = strike.CommittedRides;
        strike.ReturningPoison = strike.CommittedPoison;
        strike.ReturningCoke = strike.CommittedCoke;
        strike.ReturningCokePurity = strike.CommittedCokePurity;
        strike.Status = PendingStrikeStatus.Returning;
        strike.Outcome = "Aborted";
        strike.ResolvedAtUtc = nowUtc;
        strike.Summary = why;
    }

    /// <summary>
    /// The crew get back. Whatever they are carrying goes where it lives, and what will not fit is
    /// left at the kerb rather than overfilling a room - the same rule a lab and a mule run obey.
    /// </summary>
    private void ComeHome(PendingStrike strike, DateTime nowUtc)
    {
        var attacker = strike.Attacker;
        var capacity = hideouts.CapacityFor(attacker.Hideout);
        // Before a thing is unloaded, because what is taken off them is taken on the road rather than
        // at the door.
        var stopped = RollTheWayHome(strike);

        // What a crew left with is theirs and always comes back. Only what they came home *with* is
        // new, and only new stock has to find room - refusing a player their own car because the garage
        // they took it out of is now full would be the game confiscating it for the crime of being used.
        int Room(int cap, int held, int committed, int returning)
            => Math.Min(returning, Math.Min(returning, committed) + Math.Max(0, cap - held));

        var parked = Room(capacity.MaxRides, attacker.Rides, strike.CommittedRides, strike.ReturningRides);
        attacker.Rides += parked;

        // Hoes are never committed - nobody sends their own house out on a strike - so every one of
        // them is a gain and every one of them needs a bed.
        var housed = Math.Min(strike.ReturningHoes, Math.Max(0, capacity.MaxHoes - attacker.Hoes));
        attacker.Hoes += housed;

        attacker.Poison += Room(capacity.MaxPoison, attacker.Poison, strike.CommittedPoison, strike.ReturningPoison);
        attacker.AddCoke(
            Room(capacity.MaxCoke, attacker.Coke, strike.CommittedCoke, strike.ReturningCoke),
            strike.ReturningCokePurity);

        var short_ = string.Empty;
        if (parked < strike.ReturningRides)
            short_ += $" {strike.ReturningRides - parked:N0} of them had nowhere to park.";
        if (housed < strike.ReturningHoes)
            short_ += $" {strike.ReturningHoes - housed:N0} had nowhere to sleep and walked.";

        strike.Status = PendingStrikeStatus.Done;
        strike.CompletedAtUtc = nowUtc;
        strike.Summary = $"{strike.Summary} The crew are back in {strike.OriginCity}.{stopped}{short_}";
    }

    /// <summary>
    /// The drive home, for a crew carrying something that is not theirs.
    ///
    /// Rolled against the haul and never against the load they set out with. A car you own and a car
    /// you took an hour ago are the same object and completely different journeys: one has plates
    /// nobody is looking for, and the other is the reason anybody is looking. The same is true of the
    /// people in the van - a crew going home is a crew going home, and a crew going home with somebody
    /// else's house in the back is a story.
    ///
    /// One roll for the whole trip, then a share, which is the shape a mule run's seizure already has.
    /// Rolling per unit would turn a wide risk into a narrow average and take the swing out of it.
    /// </summary>
    private string RollTheWayHome(PendingStrike strike)
    {
        // What they took, as against what they left with. A drive-by brings its own car back and has
        // no haul at all, which is why it is never stopped.
        var haulRides = Math.Max(0, strike.ReturningRides - strike.CommittedRides);
        var haulHoes = strike.ReturningHoes;
        if (haulRides + haulHoes <= 0) return string.Empty;
        if (random.NextDouble() >= Math.Clamp(strike.ReturnRiskPercent / 100.0, 0, 1)) return string.Empty;

        var distance = _options.Strikes.Distance;
        var share = Math.Clamp(
            distance.ReturnSeizureMinPercent
                + random.NextDouble() * Math.Max(0, distance.ReturnSeizureMaxPercent - distance.ReturnSeizureMinPercent),
            0,
            1);

        // At least one of whatever there was, so a stop that happens is a stop that cost something.
        strike.SeizedRides = haulRides <= 0 ? 0 : Math.Min(haulRides, Math.Max(1, (int)Math.Round(haulRides * share)));
        strike.SeizedHoes = haulHoes <= 0 ? 0 : Math.Min(haulHoes, Math.Max(1, (int)Math.Round(haulHoes * share)));
        strike.ReturningRides -= strike.SeizedRides;
        strike.ReturningHoes -= strike.SeizedHoes;

        var lost = new List<string>();
        if (strike.SeizedRides > 0) lost.Add($"{strike.SeizedRides:N0} of the cars");
        if (strike.SeizedHoes > 0) lost.Add($"{strike.SeizedHoes:N0} of them");
        return strike.SeizedRides + strike.SeizedHoes >= haulRides + haulHoes
            ? $" They were stopped on the way out of {strike.TargetCity} and lost the lot."
            : $" They were stopped on the way out of {strike.TargetCity} and lost {string.Join(" and ", lost)}.";
    }

    /// <summary>
    /// Whether anything is on its way to this player's door, as far as their lookout can see.
    ///
    /// It answers a yes or a no and nothing else. Not who, not what kind, not how long - the room buys
    /// notice and never detail, which is what keeps the warning worth acting on rather than an
    /// instruction. Medicine, a bigger guard and a better cut are three different purchases, and having
    /// to guess which one is the whole decision.
    ///
    /// The lookout's level is the lead time: past it there is nothing to see, so a crew far enough out
    /// is invisible to everybody and a crew close enough is visible to anyone who paid for eyes.
    /// </summary>
    public async Task<bool> AnythingInboundAsync(Player defender, DateTime nowUtc, CancellationToken ct)
    {
        var notice = WarningMinutesFor(defender.Hideout);
        if (notice <= 0) return false;

        var horizon = nowUtc.AddMinutes(notice);
        return await db.PendingStrikes.AsNoTracking()
            .AnyAsync(x => x.DefenderId == defender.Id
                           && x.Status == PendingStrikeStatus.Outbound
                           && x.ArrivesAtUtc <= horizon, ct);
    }

    /// <summary>
    /// How far down the road this house can see, which is nothing at all without a lookout and nothing
    /// at all while it is through a wall. A wrecked lookout is a blind house, which is most of why a
    /// raider wants to break it.
    /// </summary>
    public int WarningMinutesFor(Hideout? hideout)
    {
        var level = hideout?.WorkingLevel(HideoutRooms.Lookout) ?? 0;
        if (level <= 0) return 0;
        return Math.Max(0, _options.Hideout.Lookout
            .Where(x => x.Level <= level)
            .Select(x => x.WarningMinutes)
            .DefaultIfEmpty(0)
            .Max());
    }

    /// <summary>Trips this player has on the road right now, newest first.</summary>
    public Task<List<PendingStrike>> OutAsync(Guid attackerId, CancellationToken ct)
        => db.PendingStrikes.AsNoTracking()
            .Where(x => x.AttackerId == attackerId && x.Status != PendingStrikeStatus.Done)
            .OrderBy(x => x.ArrivesAtUtc)
            .ToListAsync(ct);
}
