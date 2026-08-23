using Microsoft.Extensions.Options;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

/// <summary>
/// The four quick strikes: a drive-by, a jacking, an infestation, a poaching run.
///
/// Deliberately not missions. A raid takes ten turns, holds one of two lanes for half an hour, travels,
/// fights over rounds and comes home; that machinery is what makes a raid feel like an operation, and it
/// is exactly what makes a raid the wrong shape for everything else. Wanting to thin a rival's guard
/// before a real attack, or to walk off with the two cars they left unguarded, should not cost the same
/// as robbing their house. So these settle in one call, cost turns rather than lanes, and each touches
/// exactly one thing the defender owns.
///
/// Nothing here reads the database. What the house has standing in it is handed in, because that number
/// depends on which of the defender's crew are out on missions of their own, and the caller already has
/// to know that. It keeps every rule in here testable against two plain players.
/// </summary>
public sealed class StreetStrikeService(IOptionsSnapshot<GameOptions> options, IGameRandom random, HideoutService hideout)
{
    private readonly GameOptions _options = options.Value;

    /// <summary>
    /// What each method costs and needs, for a menu that can be priced without the client knowing any
    /// of the rules. Reported for a specific player, so "you have no ride" is a fact about them rather
    /// than a note in the description.
    /// </summary>
    public IReadOnlyList<AttackMethodResponse> MethodsFor(Player player)
    {
        var strikes = _options.Strikes;
        var raidTurns = _options.Combat.AttackTurnCost;
        return
        [
            new AttackMethodResponse(
                AttackMethods.Raid,
                "Raid",
                raidTurns,
                "Send a crew to break the house and take cash, weed and coke. Travels, fights in rounds, and holds an attack lane.",
                null),
            new AttackMethodResponse(
                AttackMethods.DriveBy,
                "Drive-by",
                strikes.DriveBy.TurnCost,
                "Shoot up the street from a moving car. Kills thugs and dents their morale, takes nothing, and may cost you the car.",
                player.Rides < 1 ? "You need a ride." : null),
            new AttackMethodResponse(
                AttackMethods.Jack,
                "Jack a ride",
                strikes.Jack.TurnCost,
                "Drive their low-riders away. Easy against a thin guard, close to hopeless against a full one.",
                player.Thugs < 1
                    ? "You need a thug to drive."
                    : hideout.RideRoom(player) < 1 ? "Your garage is full." : null),
            new AttackMethodResponse(
                AttackMethods.Infest,
                "Infest their hoes",
                strikes.Infest.TurnCost,
                "Poison their house. Their medicine treats who it can; the rest are lost.",
                null),
            new AttackMethodResponse(
                AttackMethods.Poach,
                "Poach with coke",
                strikes.Poach.TurnCost,
                $"Buy their hoes away, {strikes.Poach.CokePerHoe:N0} coke a head at full purity. Happy hoes will not go at any price.",
                player.Coke < strikes.Poach.CokePerHoe
                    ? $"You need at least {strikes.Poach.CokePerHoe:N0} coke."
                    : hideout.CrewRoom(player, "hoes") < 1 ? "Your hideout has no room for more hoes." : null)
        ];
    }

    public int TurnCostOf(string method) => AttackMethods.Normalize(method) switch
    {
        AttackMethods.DriveBy => Math.Max(1, _options.Strikes.DriveBy.TurnCost),
        AttackMethods.Jack => Math.Max(1, _options.Strikes.Jack.TurnCost),
        AttackMethods.Infest => Math.Max(1, _options.Strikes.Infest.TurnCost),
        AttackMethods.Poach => Math.Max(1, _options.Strikes.Poach.TurnCost),
        _ => Math.Max(1, _options.Combat.AttackTurnCost)
    };

    /// <param name="defence">
    /// The crew actually standing in the defender's house right now, with whatever they are holding.
    /// Crew away on missions of their own cannot also be guarding the garage.
    /// </param>
    public StrikeResult Resolve(
        Player attacker,
        Player defender,
        CombatAttackRequest request,
        StrikeDefence defence,
        DateTime nowUtc)
    {
        var method = AttackMethods.Normalize(request.Method);
        if (!AttackMethods.IsStrike(method))
            throw new GameRuleException($"{AttackMethods.Label(method)} is not a strike.");

        Validate(attacker, defender, method, request, nowUtc);

        var strike = method switch
        {
            AttackMethods.DriveBy => DriveBy(attacker, defender, defence),
            AttackMethods.Jack => Jack(attacker, defender, defence),
            AttackMethods.Infest => Infest(attacker, defender),
            _ => Poach(attacker, defender, request)
        };

        var turnCost = TurnCostOf(method);
        attacker.Turns -= turnCost;
        // Loud crimes in public, against people with their own reasons to talk to the law. Charged at the
        // town's own rate, like every other way of drawing notice here.
        attacker.Heat += Math.Max(0, HeatOf(method)) * _options.CityMarkets.HeatMultiplier(attacker.City);

        defender.HoeHappiness = ClampMorale(defender.HoeHappiness - strike.HoeMoraleHit);
        defender.ThugHappiness = ClampMorale(defender.ThugHappiness - strike.ThugMoraleHit);
        defender.LastAttackedAtUtc = nowUtc;
        // Only the strike clock. A raid's shield covers strikes as well, but a four-turn drive-by must
        // never buy its victim an hour of immunity from the raid that was actually coming.
        defender.StrikeProtectionUntilUtc = nowUtc.AddMinutes(Math.Max(1, _options.Strikes.ShieldMinutes));

        var outcome = strike.Landed ? "Victory" : "Defeat";
        var log = new CombatLog
        {
            AttackerId = attacker.Id,
            Attacker = attacker,
            DefenderId = defender.Id,
            Defender = defender,
            Method = method,
            Outcome = outcome,
            Summary = strike.Summary,
            TurnsSpent = turnCost,
            AttackerThugsLost = strike.AttackerThugsLost,
            DefenderHoesLost = strike.DefenderHoesLost,
            DefenderThugsLost = strike.DefenderThugsLost,
            HoesTaken = strike.HoesTaken,
            RidesTaken = strike.RidesTaken,
            DefenderProtectionUntilUtc = defender.StrikeProtectionUntilUtc,
            ResolvesAtUtc = nowUtc,
            ResolvedAtUtc = nowUtc,
            CreatedAtUtc = nowUtc
        };

        var breakdown = new Dictionary<string, object?>
        {
            ["method"] = method,
            ["outcome"] = outcome,
            ["turnsSpent"] = turnCost,
            ["defenderThugsLost"] = strike.DefenderThugsLost,
            ["defenderHoesLost"] = strike.DefenderHoesLost,
            ["hoesTaken"] = strike.HoesTaken,
            ["ridesTaken"] = strike.RidesTaken,
            ["attackerThugsLost"] = strike.AttackerThugsLost,
            ["ridesLost"] = strike.AttackerRidesLost,
            ["cokeSpent"] = strike.CokeSpent,
            ["strikeProtectionUntilUtc"] = defender.StrikeProtectionUntilUtc
        };
        foreach (var (key, value) in strike.Detail)
            breakdown[key] = value;

        return new StrikeResult(method, outcome, log, new ActionResultResponse(strike.Summary, attacker.Turns, breakdown));
    }

    private void Validate(Player attacker, Player defender, string method, CombatAttackRequest request, DateTime nowUtc)
    {
        TravelGate.EnsureLanded(attacker);
        if (attacker.Id == defender.Id)
            throw new GameRuleException("You cannot attack yourself.");
        if (AllianceService.AreAllied(attacker, defender))
            throw new GameRuleException($"{defender.Name} runs with your crew.");

        var turnCost = TurnCostOf(method);
        if (attacker.Turns < turnCost)
            throw new GameRuleException($"A {AttackMethods.Label(method)} costs {turnCost:N0} turns.");

        // A house that has just been broken open is sheltered from everything, not only from more raids:
        // walking in behind someone else's victory to finish the job is the dogpile protection exists for.
        if (defender.CombatProtectionUntilUtc is { } raidShield && raidShield > nowUtc)
            throw new GameRuleException($"{defender.Name} is under combat protection.");
        if (defender.StrikeProtectionUntilUtc is { } strikeShield && strikeShield > nowUtc)
        {
            var minutes = Math.Max(1, (int)Math.Ceiling((strikeShield - nowUtc).TotalMinutes));
            throw new GameRuleException($"{defender.Name} has just been hit and is watching the street. Try again in {minutes} minute(s).");
        }

        // What could be carried off, not what the pair are worth: see CombatMissionService.
        var mismatch = AntiFarm.RejectReason(
            EconomyService.PlunderOf(attacker, _options),
            EconomyService.PlunderOf(defender, _options),
            _options.AntiFarm);
        if (mismatch is not null)
            throw new GameRuleException(mismatch);

        if (WhyNot(method, attacker, defender, request.Coke) is { } refusal)
            throw new GameRuleException(refusal);
    }

    /// <summary>
    /// Why this strike cannot be thrown at this person, or null when it can.
    ///
    /// Returns the reason rather than throwing it so the same sentence can be shown before the click as
    /// is thrown after it. The menu of methods is built from the attacker alone - it never sees who is
    /// being looked at - so the target's half of the rule had nowhere to be said, and a player could sit
    /// reading "nothing parked there to take" under a live button offering to take it.
    ///
    /// One function for both, because a rule written twice is a rule that will disagree with itself.
    /// </summary>
    public string? WhyNot(string method, Player attacker, Player defender, int coke = 0)
    {
        switch (AttackMethods.Normalize(method))
        {
            case AttackMethods.DriveBy:
                if (attacker.Rides < 1)
                    return "A drive-by needs a ride. The chop shop sells them.";
                break;

            case AttackMethods.Jack:
                if (attacker.Thugs < 1)
                    return "You need a thug to drive it away.";
                if (defender.Rides < 1)
                    return $"{defender.Name} does not own a ride.";
                if (hideout.RideRoom(attacker) < 1)
                    return "Your garage is full. A bigger hideout parks more.";
                break;

            case AttackMethods.Infest:
                if (defender.Hoes < 1)
                    return $"{defender.Name} has no hoes to infest.";
                if (attacker.Poison < 1)
                    return "You have no poison. The counter sells it, and a mix house makes it cheaper.";
                break;

            case AttackMethods.Poach:
                var perHoe = Math.Max(1, _options.Strikes.Poach.CokePerHoe);
                if (defender.Hoes < 1)
                    return $"{defender.Name} has no hoes to poach.";
                if (coke < perHoe)
                    return $"Tempting one hoe away takes {perHoe:N0} coke at full purity.";
                if (attacker.Coke < coke)
                    return $"You only hold {attacker.Coke:N0} coke.";
                if (hideout.CrewRoom(attacker, "hoes") < 1)
                    return "Your hideout has no room for more hoes.";
                break;
        }

        return null;
    }

    /// <summary>
    /// Shoot up the street. Takes nothing, which is what makes it cheap: it is how a player who cannot
    /// yet win a raid makes the raid winnable, one guard at a time.
    ///
    /// Both of its rolls read the guard the way a jacking does - how many of them, and what they are
    /// carrying over and above sidearms - but they weight the two differently, because they are asking
    /// different questions. Whether the pass finds anybody leans on bodies: a crowded street is a street
    /// where somebody sees you coming and everyone is behind a wall before you arrive. Whether the car
    /// comes back leans on guns: a pistol rarely stops a moving car and a rifle very often does.
    /// </summary>
    private Strike DriveBy(Player attacker, Player defender, StrikeDefence defence)
    {
        var config = _options.Strikes.DriveBy;
        var guard = defence.ArmedThugs;
        var extraFirepower = defence.FirepowerOverSidearms(_options.WeaponFirepower());
        var hitChance = Math.Clamp(
            config.BaseHitChance
                - guard * config.HitChancePerArmedThug
                - extraFirepower * config.HitChancePerGuardFirepower,
            Math.Clamp(config.MinHitChance, 0, 1),
            1);

        var landed = random.NextDouble() < hitChance;
        var kills = landed
            ? Math.Min(defender.Thugs, random.NextInclusive(
                Math.Max(0, config.ThugKillsMin),
                Math.Max(Math.Max(0, config.ThugKillsMin), config.ThugKillsMax)))
            : 0;
        defender.Thugs -= kills;

        // Return fire. Rolled whether or not the pass landed: driving into a defended street is the risk,
        // and hitting nobody does not mean nobody was shooting back. This is the one roll in the game
        // where what the guard is holding outweighs how many of them there are.
        var rideLossChance = Math.Clamp(
            config.RideLossChance
                + guard * config.RideLossChancePerArmedThug
                + extraFirepower * config.RideLossChancePerGuardFirepower,
            0,
            Math.Clamp(config.MaxRideLossChance, 0, 1));
        var ridesLost = random.NextDouble() < rideLossChance ? 1 : 0;
        attacker.Rides -= ridesLost;

        var summary = kills > 0
            ? $"{attacker.Name} shot up {defender.Name}'s street and left {kills:N0} thug(s) down."
            : $"{attacker.Name} shot up {defender.Name}'s street and hit nobody.";
        if (ridesLost > 0)
            summary += " The car did not come back.";

        return new Strike(
            kills > 0,
            summary,
            DefenderThugsLost: kills,
            AttackerRidesLost: ridesLost,
            ThugMoraleHit: kills > 0 ? config.ThugMoraleHit : config.ThugMoraleHit / 2,
            Detail: new Dictionary<string, object?>
            {
                ["hitChancePercent"] = (int)Math.Round(hitChance * 100),
                ["rideLossChancePercent"] = (int)Math.Round(rideLossChance * 100),
                ["guardArmedThugs"] = guard,
                // Both halves of the street, so a player who lost the car can see whether it was the
                // number of them or what they were carrying.
                ["guardFirepower"] = Math.Round(defence.Guns(_options.WeaponFirepower()).InPistols, 1),
                ["guardFirepowerOverSidearms"] = Math.Round(extraFirepower, 1)
            });
    }

    /// <summary>
    /// Take their cars. The one strike whose odds are almost entirely the defender's own doing: a garage
    /// behind a full armed crew is close to untouchable, and a garage behind nobody is a car park.
    ///
    /// Two things stop you, and they are counted separately because they stop you in different ways.
    /// Bodies are eyes: however lightly armed, more of them means more chance somebody is looking at the
    /// door you came in through. Guns are what happens once you are seen, and a crew carrying rifles is a
    /// far worse answer to being seen than the same crew carrying sidearms.
    ///
    /// Only the firepower *above* one pistol each counts in the second term, which keeps the two from
    /// double-counting the same guard - and means a garage held by a pistol crew has exactly the odds it
    /// had before guns had tiers at all.
    /// </summary>
    private Strike Jack(Player attacker, Player defender, StrikeDefence defence)
    {
        var config = _options.Strikes.Jack;
        var guard = defence.ArmedThugs;
        var extraFirepower = defence.FirepowerOverSidearms(_options.WeaponFirepower());
        var chance = Math.Clamp(
            config.BaseChance
                - guard * config.ChancePerArmedThug
                - extraFirepower * config.ChancePerGuardFirepower,
            Math.Clamp(config.MinChance, 0, 1),
            1);

        if (random.NextDouble() >= chance)
        {
            var thugsLost = Math.Min(attacker.Thugs, random.NextInclusive(
                Math.Max(0, config.FailedThugLossesMin),
                Math.Max(Math.Max(0, config.FailedThugLossesMin), config.FailedThugLossesMax)));
            attacker.Thugs -= thugsLost;
            var caught = thugsLost > 0
                ? $"{attacker.Name} went for {defender.Name}'s garage and left {thugsLost:N0} thug(s) in it."
                : $"{attacker.Name} went for {defender.Name}'s garage and got run off empty-handed.";
            return new Strike(false, caught, AttackerThugsLost: thugsLost, Detail: Odds(chance, guard));
        }

        // Bounded by their garage and by yours. A ride with nowhere to park is a ride left behind, which
        // is the same rule the chop shop refuses a purchase under.
        var taken = Math.Min(
            Math.Min(defender.Rides, hideout.RideRoom(attacker)),
            random.NextInclusive(1, Math.Max(1, config.MaxRides)));
        defender.Rides -= taken;
        attacker.Rides += taken;

        return new Strike(
            taken > 0,
            $"{attacker.Name} drove {taken:N0} of {defender.Name}'s ride(s) out of the garage.",
            RidesTaken: taken,
            Detail: Odds(chance, guard));

        Dictionary<string, object?> Odds(double success, int armed) => new()
        {
            ["successChancePercent"] = (int)Math.Round(success * 100),
            ["guardArmedThugs"] = armed,
            // Reported apart so a player can see which half of the garage beat them: too many of them,
            // or too well armed.
            ["guardFirepower"] = Math.Round(defence.Guns(_options.WeaponFirepower()).InPistols, 1),
            ["guardFirepowerOverSidearms"] = Math.Round(extraFirepower, 1)
        };
    }

    /// <summary>
    /// Put something through their house, and find out whether they bought medicine.
    ///
    /// The only attack in the game answered by a purchase rather than by crew or morale, which is what
    /// makes medicine interesting to own: it sits on a shelf doing nothing, costing money, until the day
    /// it is the only thing between a rival and a third of your hoes.
    /// </summary>
    private Strike Infest(Player attacker, Player defender)
    {
        var config = _options.Strikes.Infest;
        var perCrate = Math.Max(1, config.HoesCuredPerCrate);
        var share = Math.Clamp(
            config.MinSharePercent + random.NextDouble() * Math.Max(0, config.MaxSharePercent - config.MinSharePercent),
            0,
            100);
        var exposed = Math.Min(defender.Hoes, Math.Max(1, (int)Math.Round(defender.Hoes * share / 100.0)));

        // You reach as far as you brought poison for. This is the defender's own problem handed back to
        // them in reverse: covering a big house costs real money, and turning up short against one only
        // buys you the hoes your doses could reach.
        var perDose = Math.Max(1, config.HoesHitPerDose);
        exposed = Math.Min(exposed, attacker.Poison * perDose);

        // A part-used dose is a used dose, exactly as a part-used crate of medicine is.
        var dosesUsed = (int)Math.Ceiling(exposed / (double)perDose);
        attacker.Poison -= dosesUsed;

        var cured = Math.Min(exposed, defender.Medicine * perCrate);
        // A part-used crate is a used crate, so this rounds up. Treating three hoes out of a crate of
        // three and keeping the crate would make one crate cover a house forever.
        var cratesUsed = (int)Math.Ceiling(cured / (double)perCrate);
        defender.Medicine -= cratesUsed;

        var killed = exposed - cured;
        defender.Hoes -= killed;

        var summary = killed > 0
            ? cured > 0
                ? $"{attacker.Name} infested {defender.Name}'s house. Medicine saved {cured:N0}, but {killed:N0} hoe(s) were lost."
                : $"{attacker.Name} infested {defender.Name}'s house and {killed:N0} hoe(s) were lost."
            : $"{attacker.Name} infested {defender.Name}'s house, but their medicine treated all {cured:N0} of them.";

        return new Strike(
            killed > 0,
            summary,
            DefenderHoesLost: killed,
            HoeMoraleHit: killed > 0 ? config.HoeMoraleHit : config.CuredHoeMoraleHit,
            Detail: new Dictionary<string, object?>
            {
                ["hoesExposed"] = exposed,
                ["hoesCured"] = cured,
                ["medicineUsed"] = cratesUsed,
                ["poisonUsed"] = dosesUsed,
                ["sharePercent"] = Math.Round(share, 1)
            });
    }

    /// <summary>
    /// Buy their hoes away with product.
    ///
    /// This is the attack the payout slider answers, and the reason that slider is a decision rather than
    /// a dial nobody touches. A house paid enough to be entirely happy cannot be poached at any price;
    /// one squeezed for every dollar can be emptied by a rival with a lab. Purity matters too: stepped-on
    /// coke tempts fewer people, through the same multiplier the market prices it by.
    /// </summary>
    private Strike Poach(Player attacker, Player defender, CombatAttackRequest request)
    {
        var config = _options.Strikes.Poach;
        var perHoe = Math.Max(1, config.CokePerHoe);
        var spent = Math.Min(attacker.Coke, Math.Max(perHoe, request.Coke));

        var offered = spent * _options.PurityMultiplier(attacker.CokePurity) / perHoe;
        var resistance = Math.Clamp(defender.HoeHappiness / 100 * Math.Max(0, config.MoraleResistance), 0, 1);
        var tempted = (int)Math.Floor(offered * (1 - resistance));
        var taken = Math.Max(0, Math.Min(
            Math.Min(tempted, Math.Max(0, config.MaxHoes)),
            Math.Min(defender.Hoes, hideout.CrewRoom(attacker, "hoes"))));

        // Spent either way. The product went out on the street and got handed to people who then stayed
        // where they were, which is the risk the move carries: it is the only strike that can cost real
        // money and return nothing at all.
        attacker.Coke -= spent;
        defender.Hoes -= taken;
        attacker.Hoes += taken;

        var summary = taken > 0
            ? $"{attacker.Name} put {spent:N0} coke on the street and walked {taken:N0} of {defender.Name}'s hoes home."
            : $"{attacker.Name} put {spent:N0} coke on the street outside {defender.Name}'s place and nobody went with them.";
        if (taken == 0 && defender.HoeHappiness >= 90)
            summary += " They are paid too well to be tempted.";
        else if (taken < tempted)
            summary += " More would have come, but there was nowhere to put them.";

        return new Strike(
            taken > 0,
            summary,
            DefenderHoesLost: taken,
            HoesTaken: taken,
            CokeSpent: spent,
            HoeMoraleHit: taken > 0 ? config.HoeMoraleHit : 0,
            Detail: new Dictionary<string, object?>
            {
                ["cokePurityPercent"] = (int)Math.Round(attacker.CokePurity * 100),
                ["hoesTempted"] = tempted,
                ["defenderHoeMorale"] = Math.Round(defender.HoeHappiness, 1)
            });
    }

    private double HeatOf(string method) => method switch
    {
        AttackMethods.DriveBy => _options.Strikes.DriveBy.HeatPerStrike,
        AttackMethods.Jack => _options.Strikes.Jack.HeatPerStrike,
        AttackMethods.Infest => _options.Strikes.Infest.HeatPerStrike,
        AttackMethods.Poach => _options.Strikes.Poach.HeatPerStrike,
        _ => 0
    };

    private static double ClampMorale(double value) => Math.Clamp(value, 0, 100);

    /// <summary>What one strike did, before it is turned into a log row and a summary.</summary>
    private sealed record Strike(
        bool Landed,
        string Summary,
        int DefenderThugsLost = 0,
        int DefenderHoesLost = 0,
        int HoesTaken = 0,
        int RidesTaken = 0,
        int AttackerThugsLost = 0,
        int AttackerRidesLost = 0,
        int CokeSpent = 0,
        double HoeMoraleHit = 0,
        double ThugMoraleHit = 0,
        IReadOnlyDictionary<string, object?>? Detail = null)
    {
        public IReadOnlyDictionary<string, object?> Detail { get; init; } = Detail ?? new Dictionary<string, object?>();
    }
}

/// <summary>
/// Who is actually home, and what they are holding. Committed crew is subtracted by the caller, because
/// a player whose thugs are out raiding somebody else is not also guarding their own garage with them -
/// and neither are the guns that went with them.
/// </summary>
public sealed record StrikeDefence(int HomeThugs, Armoury HomeRack)
{
    public int HomeWeapons => HomeRack.Total;

    /// <summary>How many of them are armed at all. One gun covers one body, whatever kind it is.</summary>
    public int ArmedThugs => Math.Max(0, Math.Min(HomeThugs, HomeRack.Total));

    /// <summary>What those guns are worth, in pistols, in the hands of the crew holding them.</summary>
    public Firepower Guns(IReadOnlyDictionary<string, double> power)
        => Firepower.Of(HomeRack, HomeThugs, power);

    /// <summary>
    /// How much better than sidearms the guard is armed: the firepower they have over and above one
    /// pistol each. Zero for a crew carrying nothing but pistols, which is the whole point - it is what
    /// lets a rule read "bodies, and then what those bodies are holding" as two separate terms.
    /// </summary>
    public double FirepowerOverSidearms(IReadOnlyDictionary<string, double> power)
        => Math.Max(0, Guns(power).InPistols - ArmedThugs);

    /// <summary>The whole house, for callers with no missions to account for.</summary>
    public static StrikeDefence Everyone(Player player) => new(player.Thugs, player.Armoury);
}

public sealed record StrikeResult(string Method, string Outcome, CombatLog Log, ActionResultResponse Result)
{
    /// <summary>The sentence describing what happened. The same one on the log and in the response.</summary>
    public string Summary => Log.Summary;
}
