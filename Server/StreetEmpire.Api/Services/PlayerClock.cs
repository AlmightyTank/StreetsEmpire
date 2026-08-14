using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;
using static StreetEmpire.Api.Support.ActionLogging;

namespace StreetEmpire.Api.Services;

/// <summary>
/// Everything a player is owed for time passing, in one place: accrued turns and morale, a finished
/// hideout build, and whatever the labs made on their own.
///
/// These used to be one call to <see cref="TurnService.Refresh"/> scattered across seven endpoints.
/// Wall-clock earnings have to happen wherever a player is loaded, not only on the dashboard, or the
/// same hour pays out twice depending on which page they opened first.
/// </summary>
public sealed class PlayerClock(TurnService turns, HideoutService hideouts)
{
    /// <summary>
    /// Brings a player up to date. The caller saves when <see cref="PlayerTick.Changed"/> is set, and
    /// passes <paramref name="db"/> when the run should leave a record the player can read later.
    /// </summary>
    public PlayerTick Advance(Player player, DateTime nowUtc, GameDbContext? db = null)
    {
        var built = hideouts.CompleteBuild(player.Hideout, nowUtc);
        if (db is not null && built)
            AddLog(db, player, Snapshot(player), "HIDEOUT", 0, $"The {hideouts.TierName(player.Hideout!.Tier)} is finished.", nowUtc);

        // Snapshotted again on purpose: sharing one snapshot with the build above would stamp the lab's
        // haul onto the build's log row as well, and both rows would claim the same weed.
        var beforeLabs = Snapshot(player);
        var labs = hideouts.AccrueLabs(player, nowUtc);
        if (db is not null && labs.Any)
            AddLog(db, player, beforeLabs, "LAB", 0, labs.Describe(), nowUtc);

        var turnsMoved = turns.Refresh(player, nowUtc);
        return new PlayerTick(turnsMoved || built || labs.ClockMoved, built, labs);
    }

    public int SecondsUntilNextTick(Player player, DateTime nowUtc)
        => turns.SecondsUntilNextTick(player, nowUtc);
}

/// <param name="Changed">Whether anything was written to the player and needs saving.</param>
public sealed record PlayerTick(bool Changed, bool HideoutFinished, LabYield Labs);
