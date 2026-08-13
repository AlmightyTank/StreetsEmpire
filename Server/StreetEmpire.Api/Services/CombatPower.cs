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
    public static int Attack(int pimps, int thugs, int weapons, double morale, CombatPowerOptions options, int bonusPercent = 0)
    {
        var armed = Math.Min(weapons, thugs);
        var raw = thugs * options.ThugAttack
                  + armed * options.ArmedThugAttack
                  + pimps * options.PimpAttack
                  + (int)Math.Round(morale * options.MoraleAttackWeight);
        return WithBonus(raw, bonusPercent);
    }

    public static int Defence(int pimps, int thugs, int weapons, double morale, CombatPowerOptions options, int bonusPercent = 0)
    {
        var armed = Math.Min(weapons, thugs);
        var raw = thugs * options.ThugDefence
                  + armed * options.ArmedThugDefence
                  + pimps * options.PimpDefence
                  + (int)Math.Round(morale * options.MoraleDefenceWeight);
        return WithBonus(raw, bonusPercent);
    }

    /// <summary>
    /// How many armed thugs an attacker needs to match a defender of this size, for balance tests and
    /// for explaining the matchup. Never less than one.
    /// </summary>
    public static int ThugsNeededToMatch(int defenderThugs, int defenderPimps, double morale, CombatPowerOptions options)
    {
        var target = Defence(defenderPimps, defenderThugs, defenderThugs, morale, options);
        for (var thugs = 1; thugs <= defenderThugs * 5 + 10; thugs++)
            if (Attack(1, thugs, thugs, morale, options) >= target)
                return thugs;
        return defenderThugs * 5 + 10;
    }

    private static int WithBonus(int raw, int bonusPercent)
        => Math.Max(1, bonusPercent <= 0 ? raw : (int)Math.Round(raw * (1 + bonusPercent / 100.0)));
}
