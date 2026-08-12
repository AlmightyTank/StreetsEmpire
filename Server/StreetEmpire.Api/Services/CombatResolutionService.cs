using Microsoft.EntityFrameworkCore;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

public sealed class CombatResolutionService(
    GameDbContext db,
    CombatService combat,
    CombatMissionService missions,
    CombatSchedule schedule)
{
    private static readonly SemaphoreSlim ResolutionLock = new(1, 1);

    public async Task<int> ResolveDueAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        // Cheap gate first: most calls arrive while a mission is mid-travel or mid-round with nothing
        // to settle, and those should cost no queries and no time in the lock.
        if (!schedule.MayBeDue(nowUtc))
            return 0;

        await ResolutionLock.WaitAsync(cancellationToken);
        try
        {
            // Re-check inside the lock: a request that queued behind the one that did the work has
            // nothing left to do.
            if (!schedule.MayBeDue(nowUtc))
                return 0;

            var missionUpdates = await missions.ResolveDueAsync(nowUtc, cancellationToken);
            var dueLogs = await db.CombatLogs
                .Include(x => x.Attacker)
                .Include(x => x.Defender)
                .Where(x => x.Outcome == "Pending" && x.ResolvesAtUtc <= nowUtc)
                .OrderBy(x => x.ResolvesAtUtc)
                .ThenBy(x => x.Id)
                .Take(25)
                .ToListAsync(cancellationToken);

            foreach (var log in dueLogs)
            {
                var before = Snapshot(log.Attacker);
                var resolution = combat.ResolveAttack(log, log.Attacker, log.Defender, nowUtc);
                AddLog(log.Attacker, before, "ATTACK", 0, resolution.Summary, nowUtc);
            }

            if (dueLogs.Count > 0)
                await db.SaveChangesAsync(cancellationToken);

            schedule.SetNextDue(await NextDueAtUtcAsync(cancellationToken));
            return dueLogs.Count + missionUpdates;
        }
        finally
        {
            ResolutionLock.Release();
        }
    }

    /// <summary>
    /// The earliest moment combat needs attention again, or null when nothing is outstanding. Reads
    /// only the timestamps, and only after a resolution pass rather than on every request.
    /// </summary>
    private async Task<DateTime?> NextDueAtUtcAsync(CancellationToken cancellationToken)
    {
        var active = await db.CombatMissions.AsNoTracking()
            .Where(x => x.Status != "Complete")
            .Select(x => new { x.Status, x.ArrivesAtUtc, x.NextRoundAtUtc, x.ReturnsAtUtc })
            .ToListAsync(cancellationToken);
        var pendingLog = await db.CombatLogs.AsNoTracking()
            .Where(x => x.Outcome == "Pending" && x.ResolvesAtUtc != null)
            .OrderBy(x => x.ResolvesAtUtc)
            .Select(x => x.ResolvesAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var times = active
            .Select(x => x.Status switch
            {
                "Traveling" => x.ArrivesAtUtc,
                "Fighting" => x.NextRoundAtUtc,
                "Returning" => x.ReturnsAtUtc,
                _ => null
            })
            .Where(x => x is not null)
            .Select(x => x!.Value)
            .ToList();
        if (pendingLog is { } logDue)
            times.Add(logDue);

        return times.Count == 0 ? null : times.Min();
    }

    private void AddLog(
        Player player,
        PlayerSnapshot before,
        string action,
        int turnsSpent,
        string summary,
        DateTime nowUtc)
    {
        db.ActionLogs.Add(new GameActionLog
        {
            PlayerId = player.Id,
            Action = action,
            TurnsSpent = turnsSpent,
            CashDelta = player.Cash - before.Cash,
            BankDelta = player.BankCash - before.BankCash,
            PimpsDelta = player.Pimps - before.Pimps,
            HoesDelta = player.Hoes - before.Hoes,
            ThugsDelta = player.Thugs - before.Thugs,
            CondomsDelta = player.Condoms - before.Condoms,
            BeerDelta = player.Beer - before.Beer,
            WeaponsDelta = player.Weapons - before.Weapons,
            WeedDelta = player.Weed - before.Weed,
            CokeDelta = player.Coke - before.Coke,
            Summary = summary,
            CreatedAtUtc = nowUtc
        });
    }

    private static PlayerSnapshot Snapshot(Player player) => new(
        player.Cash,
        player.BankCash,
        player.Pimps,
        player.Hoes,
        player.Thugs,
        player.Condoms,
        player.Beer,
        player.Weapons,
        player.Weed,
        player.Coke);
}
