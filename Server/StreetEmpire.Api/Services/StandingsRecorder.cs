using System.Text.Json;
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
    TitleService titles,
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

        // Two standings that are derived rather than stored, turned into news the same way: work out
        // what is true now, compare it with what was true at the last sample, and write a log row for
        // whoever it changed for. The bell and the Discord DMs both read those rows, so neither has to
        // know that titles and crew ranks are computed rather than awarded.
        // Fetched once and handed to both. A query does not see an entity that has been Added but not
        // yet saved, so two callers each asking for the settings row would each decide it was missing
        // and add their own - two rows claiming Id 1, and a save that fails on the way out.
        var settings = await SettingsRowAsync(ct);
        await RecordTitleChangesAsync(settings, nowUtc, ct);
        await RecordCrewRankChangesAsync(settings, ordered, nowUtc, ct);

        // Samples are only ever read backwards from a player's watermark, so anything older than the
        // longest absence worth reporting on is dead weight.
        var cutoff = nowUtc.AddDays(-Math.Max(1, _options.WorldNews.StandingsRetentionDays));
        await db.StandingSnapshots.Where(x => x.TakenAtUtc < cutoff).ExecuteDeleteAsync(ct);

        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Tells a player they have taken a title, and the person they took it from that it is gone.
    ///
    /// Both halves, because losing one is news by exactly the same argument that gaining one is: it
    /// happened to you, on somebody else's week, while you were not looking.
    /// </summary>
    private async Task RecordTitleChangesAsync(GameSetting row, DateTime nowUtc, CancellationToken ct)
    {
        var board = await titles.BoardAsync(nowUtc, ct);
        var now = board.ToDictionary(x => x.Key, x => x.PlayerId, StringComparer.Ordinal);

        // The first sample records what is true and says nothing, the same rule the Discord sweep uses
        // for an account it has never seen. Without it, the deploy that adds this feature announces
        // every title in the game to whoever happens to hold it that minute.
        if (string.IsNullOrWhiteSpace(row.TitleHoldersJson))
        {
            row.TitleHoldersJson = Write(now);
            return;
        }

        var before = Read<string, Guid>(row.TitleHoldersJson);

        foreach (var held in board)
        {
            if (before.TryGetValue(held.Key, out var previous) && previous == held.PlayerId)
                continue;

            db.ActionLogs.Add(Row(held.PlayerId, "TITLE", $"You are now {held.Title}.", nowUtc));
            // Only when somebody actually lost it. A title with no previous holder is one nobody had.
            if (previous != Guid.Empty && previous != held.PlayerId && before.ContainsKey(held.Key))
                db.ActionLogs.Add(Row(previous, "TITLE", $"{held.PlayerName} took {held.Title} off you.", nowUtc));
        }

        row.TitleHoldersJson = Write(now);
    }

    /// <summary>
    /// Tells every member of a crew when the crew's place on the board changes.
    ///
    /// Written to each member rather than to the crew, because a crew has no inbox: the bell and the
    /// DMs are both per player, and the news belongs to all of them.
    /// </summary>
    private async Task RecordCrewRankChangesAsync(GameSetting row, IReadOnlyList<Player> ordered, DateTime nowUtc, CancellationToken ct)
    {
        var worth = new Dictionary<long, long>();
        foreach (var player in ordered.Where(x => x.AllianceId is not null))
            worth[player.AllianceId!.Value] = worth.GetValueOrDefault(player.AllianceId!.Value) + economy.CalculateNetWorth(player);
        if (worth.Count == 0)
            return;

        var names = await db.Alliances.AsNoTracking()
            .Where(x => worth.Keys.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        var ranked = worth
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Key)
            .Select((x, index) => (Alliance: x.Key, Rank: index + 1))
            .ToList();

        // Only crews that were on the board last time can have moved on it, which makes the first
        // sample silent without needing a rule of its own.
        var before = Read<long, int>(row.CrewRanksJson);
        var moved = ranked
            .Where(x => before.TryGetValue(x.Alliance, out var was) && was != x.Rank)
            .ToList();

        if (moved.Count > 0)
        {
            var members = await db.Players.AsNoTracking()
                .Where(x => x.AllianceId != null && moved.Select(m => m.Alliance).Contains(x.AllianceId!.Value))
                .Select(x => new { x.Id, AllianceId = x.AllianceId!.Value })
                .ToListAsync(ct);

            foreach (var (alliance, rank) in moved)
            {
                var was = before[alliance];
                var name = names.GetValueOrDefault(alliance, "Your crew");
                var summary = rank < was
                    ? $"{name} climbed to #{rank:N0} on the crew board."
                    : $"{name} slipped to #{rank:N0} on the crew board.";
                foreach (var member in members.Where(x => x.AllianceId == alliance))
                    db.ActionLogs.Add(Row(member.Id, "CREWNOTICE", summary, nowUtc));
            }
        }

        row.CrewRanksJson = Write(ranked.ToDictionary(x => x.Alliance, x => x.Rank));
    }

    private async Task<GameSetting> SettingsRowAsync(CancellationToken ct)
    {
        var row = await db.GameSettings.SingleOrDefaultAsync(x => x.Id == 1, ct);
        if (row is not null) return row;

        row = new GameSetting { Id = 1 };
        db.GameSettings.Add(row);
        return row;
    }

    private static GameActionLog Row(Guid playerId, string action, string summary, DateTime nowUtc)
        => new() { PlayerId = playerId, Action = action, Summary = summary, CreatedAtUtc = nowUtc };

    private static Dictionary<TKey, TValue> Read<TKey, TValue>(string? json) where TKey : notnull
        => string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<Dictionary<TKey, TValue>>(json) ?? [];

    private static string Write<TKey, TValue>(Dictionary<TKey, TValue> value) where TKey : notnull
        => JsonSerializer.Serialize(value);
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
