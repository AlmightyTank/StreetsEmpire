using StreetEmpire.Api.Models;
using StreetEmpire.Api.Services;

namespace StreetEmpire.Api.Support;

/// <summary>
/// What a player is on their first day, in one place.
///
/// There are two ways to arrive at it now - signing up, and a season starting over - and they have to
/// be the same thing or the two quietly drift into giving people different amounts of money for the
/// same position. Signing up builds a player around this; a reset puts an existing one back to it and
/// keeps everything that is not an empire: who they are, what they are called, who they run with, and
/// every honour they have ever won.
/// </summary>
internal static class StartingState
{
    /// <summary>
    /// Puts a player back to day one. Their identity, town, crew membership and account are left alone;
    /// everything an empire is made of is not.
    /// </summary>
    /// <param name="headStart">
    /// Extra opening cash, earned by last season's finish. Deliberately paid in the one currency that
    /// stops mattering fastest: against a $5,000 opening it is a real leg up through the first hour,
    /// and against a Warehouse it is a rounding error. A head start that lasted would be a way of
    /// winning a season by having won the previous one.
    /// </param>
    internal static void Apply(Player player, GameOptions options, DateTime nowUtc, long headStart = 0)
    {
        player.Cash = options.StartingCash + Math.Max(0, headStart);
        player.BankCash = options.StartingBankCash;
        player.LastBankedAtUtc = null;
        player.Turns = options.StartingTurns;
        player.LastTurnUpdateUtc = nowUtc;

        player.Pimps = options.StartingPimps;
        player.Hoes = options.StartingHoes;
        player.Thugs = options.StartingThugs;
        player.HoeCutPercent = options.StartingHoeCutPercent;
        player.HoeHappiness = 100;
        player.ThugHappiness = 100;

        player.Condoms = options.StartingCondoms;
        player.Beer = options.StartingBeer;
        // Everyone starts with the cheapest gun there is, and nothing above it.
        player.Armoury = new Armoury(options.StartingWeapons, 0, 0, 0);
        player.Weed = 0;
        player.Coke = 0;
        player.CokePurity = 1;
        player.Moonshine = 0;
        player.Cut = 0;
        player.Medicine = 0;
        player.Poison = 0;
        player.Rides = 0;

        // Standing is an empire and not a person. It was earned by an empire's worth of trading, it
        // unlocks the guns that empire fought with, and carrying it through a roll would open a new
        // season with the rifle counter already unlocked - which is the one thing a season is for.
        player.StoreRep = 0;
        player.StoreInvestmentReadyAtUtc = null;
        // The hand itself goes with the book, which the roll empties. This is only the clock that
        // decides what looking again costs, and it has to open at free like everybody else's.
        player.JobRerollsUsed = 0;
        player.JobRerollsResetAtUtc = null;

        player.Heat = 0;
        player.LastHeatRollUtc = nowUtc;
        player.TravelArrivesAtUtc = null;

        // Every clock and shield a fight leaves behind. A new season that opened with somebody still
        // protected, or still on cooldown, would be a season that started at different times for
        // different people.
        player.CombatProtectionUntilUtc = null;
        player.StrikeProtectionUntilUtc = null;
        player.LastAttackAtUtc = null;
        player.LastAttackedAtUtc = null;
        player.CombatAlertsSeenAtUtc = null;
        player.CatchUpSeenAtUtc = null;
        player.LastPrayedAtUtc = null;

        // Thugs the crew posted here came out of a pool that is itself being emptied.
        player.AllianceDefenders = 0;
    }

    /// <summary>
    /// A hideout back to the one everybody starts in. The row is kept rather than replaced so nothing
    /// holding a reference to it is left pointing at a building that no longer exists.
    /// </summary>
    internal static void Apply(Hideout hideout, DateTime nowUtc)
    {
        hideout.Tier = 1;
        hideout.UpgradingToTier = null;
        hideout.UpgradeCompletesAtUtc = null;
        hideout.StorageLevel = 1;
        hideout.SafeLevel = 1;
        hideout.WeedLabLevel = 0;
        hideout.CokeLabLevel = 0;
        hideout.WorkshopLevel = 0;
        hideout.LookoutLevel = 0;
        hideout.IntelligenceLevel = 0;
        // Nothing carries damage across a roll. A season that opened with somebody's coke lab still
        // wrecked would be charging them a repair bill for a lab that no longer exists, in an empire
        // that is not the one it was broken in.
        foreach (var room in HideoutRooms.Breakable)
            hideout.SetWrecked(room, null);
        hideout.RepairingRoom = null;
        hideout.RepairCompletesAtUtc = null;
        hideout.LabsCollectedAtUtc = null;
        hideout.CreatedAtUtc = nowUtc;
    }
}
