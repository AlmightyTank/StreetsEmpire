using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

/// <summary>
/// Samples where everybody stands, on a timer, so the catch-up digest can say who moved ahead of a
/// player while they were away.
///
/// Sampling everyone at once is the whole point: rank is a comparison, so two readings are only
/// comparable if they were taken at the same instant. Recording each player's position at their own
/// last login would leave an always-active rival with nothing but a fresh reading, which says nothing
/// about where they stood when the player left.
///
/// Ordering and ranking happen in the database, in step with how every other ranking in the game
/// works, so a sample never loads the player table to sort it.
/// </summary>
public sealed class StandingsRecorder(
    GameDbContext db,
    EconomyService economy,
    StandingsSchedule schedule,
    IOptionsSnapshot<GameOptions> options)
{
    private readonly GameOptions _options = options.Value;

    /// <summary>
    /// Takes a sample if one is due. Cheap to call on every request: the gate is a single interlocked
    /// read, and the work only happens once per interval however many players are looking.
    /// </summary>
    public async Task<bool> SampleIfDueAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        var interval = Math.Max(1, _options.WorldNews.StandingsSampleMinutes);
        if (!schedule.TryClaim(nowUtc, TimeSpan.FromMinutes(interval)))
            return false;

        // Ordered by the database, then valued through the one net worth formula rather than a second
        // copy of it in SQL. Loading every player is what the rest of the game avoids, but a sampler
        // writes a row for each of them by definition, and it runs once an interval rather than once a
        // request.
        var ordered = await db.Players.AsNoTracking()
            .OrderByDescending(economy.NetWorthExpression)
            .ThenBy(x => x.CreatedAtUtc)
            .ToListAsync(ct);
        if (ordered.Count == 0)
            return false;

        for (var index = 0; index < ordered.Count; index++)
            db.StandingSnapshots.Add(new StandingSnapshot
            {
                PlayerId = ordered[index].Id,
                Rank = index + 1,
                NetWorth = economy.CalculateNetWorth(ordered[index]),
                TakenAtUtc = nowUtc
            });

        // Samples are only ever read backwards from a player's watermark, so anything older than the
        // longest absence worth reporting on is dead weight.
        var cutoff = nowUtc.AddDays(-Math.Max(1, _options.WorldNews.StandingsRetentionDays));
        await db.StandingSnapshots.Where(x => x.TakenAtUtc < cutoff).ExecuteDeleteAsync(ct);

        await db.SaveChangesAsync(ct);
        return true;
    }
}

/// <summary>
/// Lets one caller at a time take a sample, and only once per interval. Same shape as the combat
/// schedule: concurrent requests must not each write their own sample of the same instant.
/// </summary>
public sealed class StandingsSchedule
{
    private long _nextDueTicks = DateTime.MinValue.Ticks;

    public bool TryClaim(DateTime nowUtc, TimeSpan interval)
    {
        while (true)
        {
            var current = Interlocked.Read(ref _nextDueTicks);
            if (nowUtc.Ticks < current)
                return false;
            var next = nowUtc.Add(interval).Ticks;
            if (Interlocked.CompareExchange(ref _nextDueTicks, next, current) == current)
                return true;
        }
    }
}
