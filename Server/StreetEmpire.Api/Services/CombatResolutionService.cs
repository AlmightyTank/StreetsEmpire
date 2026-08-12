using Microsoft.EntityFrameworkCore;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

public sealed class CombatResolutionService(GameDbContext db, CombatService combat, CombatMissionService missions)
{
    private static readonly SemaphoreSlim ResolutionLock = new(1, 1);

    public async Task<int> ResolveDueAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        await ResolutionLock.WaitAsync(cancellationToken);
        try
        {
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

            return dueLogs.Count + missionUpdates;
        }
        finally
        {
            ResolutionLock.Release();
        }
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
