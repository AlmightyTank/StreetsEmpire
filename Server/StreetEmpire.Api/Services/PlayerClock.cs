using Microsoft.EntityFrameworkCore;
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
public sealed class PlayerClock(TurnService turns, HideoutService hideouts, GameDbContext territoryDb, IGameRandom random)
{
    /// <summary>
    /// Brings a player up to date. The caller saves when <see cref="PlayerTick.Changed"/> is set, and
    /// passes <paramref name="db"/> when the run should leave a record the player can read later.
    /// </summary>
    /// <summary>
    /// Async because a club's morale bonus depends on the ground the player holds, and resolving that
    /// here keeps one authority over recovery rather than asking every caller to remember to pass it.
    /// </summary>
    public async Task<PlayerTick> AdvanceAsync(Player player, DateTime nowUtc, GameDbContext? db = null, CancellationToken ct = default)
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

        var moraleBonus = await territoryDb.Territories.AsNoTracking()
            .Where(x => x.HolderId == player.Id)
            .Select(x => x.Type)
            .ToListAsync(ct);
        // Holding contraband is a standing risk, rolled over the same hours the labs ran for. Done
        // here rather than on an action so it costs a player who stockpiles it whether or not they are
        // at the screen, which is the whole point of it being illegal to hold.
        var beforeBust = Snapshot(player);
        var bust = hideouts.RollMoonshineBust(player, labs.Hours, random);
        if (db is not null && bust.Happened)
            AddLog(db, player, beforeBust, "BUST", 0,
                bust.Fine > 0
                    ? $"The law found the still. They took {bust.Seized:N0} moonshine and fined you {bust.Fine:C0}."
                    : $"The law found the still and took {bust.Seized:N0} moonshine.",
                nowUtc);

        var turnsMoved = turns.Refresh(player, nowUtc, MoraleRecoveryPercentFor(moraleBonus));
        return new PlayerTick(turnsMoved || built || labs.ClockMoved || bust.Happened, built, labs);
    }

    public int SecondsUntilNextTick(Player player, DateTime nowUtc)
        => turns.SecondsUntilNextTick(player, nowUtc);

    /// <summary>
    /// Reads the club bonus off the held types directly. Deliberately not routed through
    /// TerritoryService: this runs on every player refresh, and a list of type strings is all it needs.
    /// </summary>
    private int MoraleRecoveryPercentFor(IEnumerable<string> heldTypes)
        => heldTypes.Count(x => string.Equals(x, "club", StringComparison.OrdinalIgnoreCase)) * ClubMoraleRecoveryPercent;

    private const int ClubMoraleRecoveryPercent = 50;
}

/// <param name="Changed">Whether anything was written to the player and needs saving.</param>
public sealed record PlayerTick(bool Changed, bool HideoutFinished, LabYield Labs);
