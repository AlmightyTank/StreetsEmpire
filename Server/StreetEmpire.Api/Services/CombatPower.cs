using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

/// <summary>
/// The one place combat strength is calculated.
///
/// These numbers previously lived hardcoded in four places: the mission resolver, the readiness figures
/// shown to the player, the bot's targeting, and the legacy combat path. Retuning one without the others
/// meant the power a player was shown was not the power their fight used, and bots decided with a third
/// set. Any balance change now happens here, driven by configuration.
///
/// Balance target: at equal armed crew and morale the defender holds, and an attacker needs roughly
/// 12-20% more armed thugs to win, tighter at scale. A rule test pins that down.
/// </summary>
public static class CombatPower
{
    public static int Attack(int pimps, int thugs, Firepower firepower, double morale, CombatPowerOptions options, int bonusPercent = 0)
    {
        var raw = thugs * options.ThugAttack
                  + (int)Math.Round(firepower.InPistols * options.ArmedThugAttack)
                  + pimps * options.PimpAttack
                  + (int)Math.Round(morale * options.MoraleAttackWeight);
        return WithBonus(raw, bonusPercent);
    }

    public static int Defence(int pimps, int thugs, Firepower firepower, double morale, CombatPowerOptions options, int bonusPercent = 0)
    {
        var raw = thugs * options.ThugDefence
                  + (int)Math.Round(firepower.InPistols * options.ArmedThugDefence)
                  + pimps * options.PimpDefence
                  + (int)Math.Round(morale * options.MoraleDefenceWeight);
        return WithBonus(raw, bonusPercent);
    }

    /// <summary>
    /// How many pistol-armed thugs an attacker needs to match a pistol-armed defender of this size, for
    /// balance tests and for explaining the matchup. Never less than one.
    ///
    /// Deliberately measured in pistols on both sides: it answers what the crew ratio has to be when
    /// nobody has an equipment advantage, which is the balance the fight is tuned around. What better
    /// guns are worth on top of that is the tier table's business.
    /// </summary>
    public static int ThugsNeededToMatch(int defenderThugs, int defenderPimps, double morale, CombatPowerOptions options)
    {
        var target = Defence(defenderPimps, defenderThugs, Firepower.Sidearms(defenderThugs, defenderThugs), morale, options);
        for (var thugs = 1; thugs <= defenderThugs * 5 + 10; thugs++)
            if (Attack(1, thugs, Firepower.Sidearms(thugs, thugs), morale, options) >= target)
                return thugs;
        return defenderThugs * 5 + 10;
    }

    private static int WithBonus(int raw, int bonusPercent)
        => Math.Max(1, bonusPercent <= 0 ? raw : (int)Math.Round(raw * (1 + bonusPercent / 100.0)));
}

/// <summary>
/// What a crew's guns are worth in a fight, measured in pistols, already capped at the crew carrying
/// them.
///
/// A type of its own rather than a bare double, and that is the point of it. This parameter used to be
/// a weapon count, and every number that could be passed to it - a rack total, a storage cap, a stock
/// figure - is still an int that converts to double without a murmur. A caller handing over a raw count
/// would be crediting a player for guns nobody is holding, in the one calculation nobody watches closely
/// enough to notice. So the count cannot be passed at all: it has to be turned into one of these first,
/// by a named method that says which reading of the rack it is.
/// </summary>
public readonly record struct Firepower(double InPistols)
{
    public static readonly Firepower None = new(0);

    /// <summary>What a rack is worth in the hands of this many thugs.</summary>
    public static Firepower Of(Armoury rack, int thugs, IReadOnlyDictionary<string, double> power)
        => new(rack.Firepower(thugs, power));

    /// <summary>
    /// A crew where every gun is a pistol. What a bare weapon count always meant, and still the honest
    /// reading for a garrison, which holds ground with bodies and sidearms rather than a rack of its own.
    /// </summary>
    public static Firepower Sidearms(int thugs, int weapons) => new(Math.Max(0, Math.Min(thugs, weapons)));
}
