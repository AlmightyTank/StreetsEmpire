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
public sealed class PlayerClock(TurnService turns, HideoutService hideouts, GameDbContext territoryDb, IGameRandom random, MuleService mules, EconomyService economy, ArrestService arrests, TerritoryService territories)
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
        var bust = hideouts.RollBust(player, ClaimHeatHours(player, nowUtc), random);
        if (db is not null && bust.Happened)
            AddLog(db, player, beforeBust, "BUST", 0, bust.Describe(), nowUtc);

        // A flight that has landed is over. Cleared here rather than checked everywhere, so nothing
        // downstream has to reason about a arrival time that is already in the past.
        var landed = false;
        if (player.TravelArrivesAtUtc is { } arrival && arrival <= nowUtc)
        {
            player.TravelArrivesAtUtc = null;
            landed = true;
        }

        var groundWorked = await CompleteGroundWorkAsync(player, nowUtc, db, ct);
        var runsHome = await SettleMuleRunsAsync(player, nowUtc, db, ct);
        var craftsDone = await CompleteWorkshopCraftsAsync(player, nowUtc, db, ct);
        // Before the turn refresh, because leaving crew inside costs morale and the refresh is what
        // recovers it. Settled the other way round, an abandonment would be partly healed in the same
        // pass that applied it.
        var writtenOff = await SettleArrestsAsync(player, nowUtc, db, ct);

        var turnsMoved = turns.Refresh(player, nowUtc, MoraleRecoveryPercentFor(moraleBonus));
        return new PlayerTick(turnsMoved || built || labs.ClockMoved || bust.Happened || landed || runsHome || craftsDone || writtenOff || groundWorked, built, labs);
    }

    /// <summary>
    /// Brings home any mule run whose plane has landed.
    ///
    /// Settled on the clock rather than on a timer because a run is owed to a player, not to the
    /// world: the crew, the cargo and the cash all belong to one empire, and the moment that matters
    /// is the moment that empire is next looked at. Bots go through this same call when they play, so
    /// a rival's runs land on the same rules a player's do.
    /// </summary>
    private async Task<bool> SettleMuleRunsAsync(Player player, DateTime nowUtc, GameDbContext? db, CancellationToken ct)
    {
        var due = await territoryDb.MuleRuns
            .Where(x => x.PlayerId == player.Id && x.SettledAtUtc == null && x.ReturnsAtUtc <= nowUtc)
            .OrderBy(x => x.ReturnsAtUtc)
            .ToListAsync(ct);
        if (due.Count == 0) return false;

        var pimpIds = due.Where(x => x.PimpId is not null).Select(x => x.PimpId!.Value).ToList();
        var crew = await territoryDb.Pimps.Where(x => pimpIds.Contains(x.Id)).ToListAsync(ct);

        foreach (var run in due)
        {
            // Snapshotted per run: two runs landing together must not both claim the same delivery.
            var before = Snapshot(player);
            mules.Settle(run, player, crew.FirstOrDefault(x => x.Id == run.PimpId), random, nowUtc);
            if (db is not null)
                AddLog(db, player, before, "MULE", 0, run.Summary, nowUtc);
        }

        return true;
    }

    /// <summary>
    /// Lands work on any ground of theirs whose timer has run out.
    ///
    /// Settled on the holder's clock rather than a sweep, like everything else here: the ground belongs
    /// to one empire and the moment that matters is the moment that empire is next looked at. A holder
    /// who never comes back simply has a corner that stays half-built, which is the honest reading of
    /// having walked away from it.
    /// </summary>
    private async Task<bool> CompleteGroundWorkAsync(Player player, DateTime nowUtc, GameDbContext? db, CancellationToken ct)
    {
        var due = await territoryDb.Territories
            .Where(x => x.HolderId == player.Id && x.DevelopingToLevel != null && x.DevelopmentCompletesAtUtc <= nowUtc)
            .OrderBy(x => x.DevelopmentCompletesAtUtc)
            .ToListAsync(ct);
        if (due.Count == 0) return false;

        var worked = false;
        foreach (var ground in due)
        {
            var level = ground.DevelopingToLevel;
            if (!TerritoryService.CompleteDevelopment(ground, nowUtc))
                continue;
            worked = true;
            if (db is not null)
                AddLog(db, player, Snapshot(player), "GROUND", 0,
                    $"The work at {ground.Name} is finished. It runs as {territories.DevelopmentName(level ?? ground.DevelopmentLevel)} ground now.",
                    nowUtc);
        }

        return worked;
    }

    /// <summary>
    /// Writes off anybody whose bail window has run out.
    ///
    /// Settled here rather than on a timer for the same reason a mule run is: the crew belong to one
    /// empire, and the moment that matters is the moment that empire is next looked at. It does mean a
    /// player who never logs in never loses anybody, which is correct - the choice was theirs to make
    /// and the game should not make it while they are gone, only once they are back to be told.
    /// </summary>
    private async Task<bool> SettleArrestsAsync(Player player, DateTime nowUtc, GameDbContext? db, CancellationToken ct)
    {
        var expired = await territoryDb.Arrests
            .Include(x => x.Pimp)
            .Where(x => x.PlayerId == player.Id && x.SettledAtUtc == null && x.BailDeadlineUtc <= nowUtc)
            .OrderBy(x => x.BailDeadlineUtc)
            .ToListAsync(ct);
        if (expired.Count == 0) return false;

        foreach (var arrest in expired)
        {
            // Snapshotted per arrest: two windows closing together must not both claim the same loss.
            var before = Snapshot(player);
            var gone = arrests.Abandon(player, arrest, nowUtc);
            if (db is not null)
                AddLog(db, player, before, "ARREST", 0, gone.Describe(), nowUtc);
        }

        return true;
    }

    /// <summary>
    /// Pays out workshop orders whose timers have finished. Starting a craft pays for materials, but
    /// the goods do not hit storage until this clock pass.
    /// </summary>
    private async Task<bool> CompleteWorkshopCraftsAsync(Player player, DateTime nowUtc, GameDbContext? db, CancellationToken ct)
    {
        var due = await territoryDb.WorkshopCrafts
            .Where(x => x.PlayerId == player.Id && x.CompletedAtUtc == null && x.CompletesAtUtc <= nowUtc)
            .OrderBy(x => x.CompletesAtUtc)
            .ToListAsync(ct);
        if (due.Count == 0) return false;

        foreach (var craft in due)
        {
            var before = Snapshot(player);
            economy.CompleteCraft(player, craft, nowUtc);
            if (db is not null)
                AddLog(db, player, before, "WORKSHOP", 0, craft.Summary, nowUtc);
        }

        return true;
    }

    public int SecondsUntilNextTick(Player player, DateTime nowUtc)
        => turns.SecondsUntilNextTick(player, nowUtc);

    /// <summary>
    /// Reads the club bonus off the held types directly. Deliberately not routed through
    /// TerritoryService: this runs on every player refresh, and a list of type strings is all it needs.
    /// </summary>
    /// <summary>
    /// Takes the whole hours owed to heat and leaves the remainder on the clock. Whole hours only,
    /// because a raid rolled per partial visit would punish checking in.
    /// </summary>
    private static int ClaimHeatHours(Player player, DateTime nowUtc)
    {
        if (player.LastHeatRollUtc > nowUtc)
        {
            player.LastHeatRollUtc = nowUtc;
            return 0;
        }

        var hours = (int)Math.Min(int.MaxValue, Math.Floor((nowUtc - player.LastHeatRollUtc).TotalHours));
        if (hours <= 0) return 0;
        player.LastHeatRollUtc = player.LastHeatRollUtc.AddHours(hours);
        return hours;
    }

    private int MoraleRecoveryPercentFor(IEnumerable<string> heldTypes)
        => heldTypes.Count(x => string.Equals(x, "club", StringComparison.OrdinalIgnoreCase)) * ClubMoraleRecoveryPercent;

    private const int ClubMoraleRecoveryPercent = 50;
}

/// <param name="Changed">Whether anything was written to the player and needs saving.</param>
public sealed record PlayerTick(bool Changed, bool HideoutFinished, LabYield Labs);
