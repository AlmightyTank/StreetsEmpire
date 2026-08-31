using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;
using StreetEmpire.Api.Support;

namespace StreetEmpire.Api.Services;

/// <summary>
/// Starts the world over, and remembers who did what before it did.
///
/// A reset is the most destructive thing this game can do to somebody, so the rule it is built on is
/// stated once here and then followed everywhere: <b>the empire goes and the person stays</b>. The
/// account, the name, the town, the crew they run with and every honour they have ever won all survive
/// a roll untouched. Cash, crew, buildings, stock, ground and every clock a fight left behind do not.
///
/// It reads as a gift rather than a theft for exactly one reason, and it is a reason that did not
/// exist a month ago: there is finally enough to climb. A turn bank that grows with the building, four
/// tiers of house, ground priced in months, and a war to fight over the top of it. Handing that back to
/// somebody who has finished it is giving them the good part again. Handing it back to somebody who
/// had barely started is taking nothing they will miss.
/// </summary>
public sealed class SeasonService(
    GameDbContext db,
    IOptionsSnapshot<GameOptions> options,
    EconomyService economy,
    PimpRoster pimps,
    SeasonSchedule schedule)
{
    private readonly GameOptions _options = options.Value;

    /// <summary>
    /// The season being played, opening the first one if the world has never had a season before.
    ///
    /// Opened lazily rather than seeded at deploy so that a world which has been running for months
    /// gets a season one that starts today, rather than one that has apparently been running since the
    /// database was created and is therefore already over.
    /// </summary>
    public async Task<Season> CurrentAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        var running = await db.Seasons
            .Where(x => x.Status == SeasonStatuses.Running)
            .OrderByDescending(x => x.Number)
            .FirstOrDefaultAsync(ct);
        if (running is not null)
            return running;

        var last = await db.Seasons.OrderByDescending(x => x.Number).FirstOrDefaultAsync(ct);
        var opened = Open((last?.Number ?? 0) + 1, nowUtc);
        await db.SaveChangesAsync(ct);
        return opened;
    }

    /// <summary>
    /// Rolls the world if the clock has run out and the operator has turned seasons on.
    ///
    /// Cheap to call on every request: a single interlocked read holds the gate shut, and the work
    /// happens once however many people are looking at the moment it comes due.
    /// </summary>
    public async Task<SeasonRoll?> RollIfDueAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        if (!_options.Seasons.Enabled)
            return null;
        if (!schedule.TryClaim(nowUtc))
            return null;

        var current = await db.Seasons
            .Where(x => x.Status == SeasonStatuses.Running && x.EndsAtUtc <= nowUtc)
            .OrderBy(x => x.Number)
            .FirstOrDefaultAsync(ct);
        return current is null ? null : await RollAsync(nowUtc, ct);
    }

    /// <summary>
    /// Ends the season being played, writes down where everybody finished, puts the world back to day
    /// one, and opens the next.
    ///
    /// One call rather than four, and no way to do half of it: a world where the results were recorded
    /// and the reset failed would hand out honours for a season nobody had finished, and a world reset
    /// without the results would delete every empire in it and remember nothing about any of them.
    /// </summary>
    public async Task<SeasonRoll> RollAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        var season = await CurrentAsync(nowUtc, ct);

        // Ranked by the same net worth expression the leaderboard uses, in the database, so a season's
        // final table can never disagree with the board people watched all month.
        var standing = await db.Players
            .Include(x => x.Alliance)
            .OrderByDescending(economy.NetWorthExpression)
            .ThenBy(x => x.CreatedAtUtc)
            .ToListAsync(ct);

        var results = new List<SeasonResult>();
        for (var index = 0; index < standing.Count; index++)
        {
            var player = standing[index];
            var rank = index + 1;
            results.Add(new SeasonResult
            {
                SeasonId = season.Id,
                Season = season,
                PlayerId = player.Id,
                Player = player,
                PlayerName = player.Name,
                City = player.City,
                CrewName = player.Alliance?.Name,
                Rank = rank,
                NetWorth = economy.CalculateNetWorth(player),
                Honour = SeasonHonours.For(rank)
            });
        }

        db.SeasonResults.AddRange(results);
        season.Status = SeasonStatuses.Ended;
        season.EndedAtUtc = nowUtc;
        season.Players = standing.Count;

        await WipeTheWorldAsync(nowUtc, standing, ct);

        // Applied off the results just written rather than off anything stored on the player, which is
        // what keeps a head start to one season. Winning twice running is worth two trophies and one
        // leg up, never a compounding one.
        var headStarts = results.ToDictionary(x => x.PlayerId, x => _options.Seasons.HeadStartFor(x.Honour));
        var next = Open(season.Number + 1, nowUtc);

        foreach (var player in standing)
        {
            StartingState.Apply(player, _options, nowUtc, headStarts.GetValueOrDefault(player.Id));
            if (player.Hideout is not null)
                StartingState.Apply(player.Hideout, nowUtc);
            // The named crew is rebuilt from the starting pimp count, so nobody carries a specialist
            // they spent a season earning into a world where nobody else has one.
            pimps.Reconcile(player, nowUtc);

            db.ActionLogs.Add(new GameActionLog
            {
                PlayerId = player.Id,
                Action = "START",
                Summary = $"{next.Name} opened. {player.Name} starts again in {player.City}"
                    + (headStarts.GetValueOrDefault(player.Id) is var head && head > 0
                        ? $", with {head:C0} on account of last season."
                        : "."),
                CashDelta = player.Cash,
                PimpsDelta = player.Pimps,
                HoesDelta = player.Hoes,
                ThugsDelta = player.Thugs,
                CreatedAtUtc = nowUtc
            });
        }

        await db.SaveChangesAsync(ct);
        return new SeasonRoll(season, next, results.Count);
    }

    /// <summary>Every season this player has finished, newest first. The half of the game that lasts.</summary>
    public async Task<IReadOnlyList<SeasonResult>> HonoursForAsync(Guid playerId, CancellationToken ct = default)
        => await db.SeasonResults.AsNoTracking()
            .Include(x => x.Season)
            .Where(x => x.PlayerId == playerId)
            .OrderByDescending(x => x.Season.Number)
            .ToListAsync(ct);

    /// <summary>How a season finished, top first. Read from the record rather than recomputed.</summary>
    public async Task<IReadOnlyList<SeasonResult>> TableForAsync(long seasonId, int take, CancellationToken ct = default)
        => await db.SeasonResults.AsNoTracking()
            .Where(x => x.SeasonId == seasonId)
            .OrderBy(x => x.Rank)
            .Take(take)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Season>> PastSeasonsAsync(int take, CancellationToken ct = default)
        => await db.Seasons.AsNoTracking()
            .Where(x => x.Status == SeasonStatuses.Ended)
            .OrderByDescending(x => x.Number)
            .Take(take)
            .ToListAsync(ct);

    /// <summary>One season by the number people call it, running or long finished.</summary>
    public async Task<Season?> ByNumberAsync(int number, CancellationToken ct = default)
        => await db.Seasons.AsNoTracking().FirstOrDefaultAsync(x => x.Number == number, ct);

    /// <summary>
    /// Who won each of these seasons, in one query rather than one per season.
    ///
    /// The archive is a shelf that grows by a row a month, and a query a season would make reading it
    /// cost more every month it is worth reading.
    /// </summary>
    public async Task<Dictionary<long, SeasonResult>> ChampionsForAsync(
        IEnumerable<long> seasonIds, CancellationToken ct = default)
    {
        var ids = seasonIds.ToList();
        return await db.SeasonResults.AsNoTracking()
            .Where(x => ids.Contains(x.SeasonId) && x.Rank == 1)
            .ToDictionaryAsync(x => x.SeasonId, ct);
    }

    /// <summary>
    /// Where one player finished one season - the row a table capped at a hundred is most likely to
    /// have left out.
    ///
    /// The record is written for everybody rather than only the top, and that is worth nothing if the
    /// only way to read your own line in it is to have come in the top hundred.
    /// </summary>
    public async Task<SeasonResult?> FinishForAsync(long seasonId, Guid playerId, CancellationToken ct = default)
        => await db.SeasonResults.AsNoTracking()
            .FirstOrDefaultAsync(x => x.SeasonId == seasonId && x.PlayerId == playerId, ct);

    /// <summary>
    /// How many empires are in the world right now.
    ///
    /// A finished season carries its own count, written down at the roll. The one being played has
    /// nothing written down yet, and "how many am I climbing against" is the question that makes a
    /// position on the board mean anything at all.
    /// </summary>
    public async Task<int> PlayersNowAsync(CancellationToken ct = default)
        => await db.Players.CountAsync(ct);

    private Season Open(int number, DateTime nowUtc)
    {
        var season = new Season
        {
            Number = number,
            Name = string.Format(_options.Seasons.NameFormat, number),
            Status = SeasonStatuses.Running,
            StartedAtUtc = nowUtc,
            EndsAtUtc = nowUtc.AddDays(Math.Max(1, _options.Seasons.LengthDays))
        };
        db.Seasons.Add(season);
        schedule.NoteNextDue(season.EndsAtUtc);
        return season;
    }

    /// <summary>
    /// Everything that was an empire rather than a person.
    ///
    /// Ground is emptied rather than deleted, because the map is seeded and fixed and a season that
    /// deleted it would come back with no map at all. Crews are kept and stripped: the people who
    /// organised themselves stay organised, and everything they had saved goes, because a treasury
    /// carried into a fresh world is one crew starting the season already finished.
    ///
    /// Loaded and removed rather than deleted in bulk, which is not the fast way and is the correct
    /// one. A bulk delete leaves the change tracker holding entities for rows that no longer exist -
    /// the roster in particular, which the caller rebuilds a moment later off a navigation property
    /// that would still be full of ghosts. This runs once a month; the tracker being right afterwards
    /// matters considerably more than the round trips.
    /// </summary>
    private async Task WipeTheWorldAsync(DateTime nowUtc, List<Player> players, CancellationToken ct)
    {
        // Written down and then thrown away: last season's fights, runs, orders, listings and rows are
        // about a world that no longer exists, and a news feed reporting them on day one of a new
        // season would be reporting a fiction.
        db.CombatMissionEvents.RemoveRange(await db.CombatMissionEvents.ToListAsync(ct));
        db.CombatMissions.RemoveRange(await db.CombatMissions.ToListAsync(ct));
        db.AllianceAssistCalls.RemoveRange(await db.AllianceAssistCalls.ToListAsync(ct));
        db.CombatLogs.RemoveRange(await db.CombatLogs.ToListAsync(ct));
        db.MarketListings.RemoveRange(await db.MarketListings.ToListAsync(ct));
        db.MuleRuns.RemoveRange(await db.MuleRuns.ToListAsync(ct));
        db.WorkshopCrafts.RemoveRange(await db.WorkshopCrafts.ToListAsync(ct));
        db.Contracts.RemoveRange(await db.Contracts.ToListAsync(ct));
        db.Arrests.RemoveRange(await db.Arrests.ToListAsync(ct));
        db.HideoutIntel.RemoveRange(await db.HideoutIntel.ToListAsync(ct));
        db.StandingSnapshots.RemoveRange(await db.StandingSnapshots.ToListAsync(ct));
        db.AllianceTransfers.RemoveRange(await db.AllianceTransfers.ToListAsync(ct));
        db.ActionLogs.RemoveRange(await db.ActionLogs.ToListAsync(ct));

        // A roster is the deepest thing a player builds - names, specialties, loyalty histories - and
        // keeping it would be keeping most of an empire. The navigation is emptied alongside the rows
        // so the caller rebuilds a starting crew rather than reconciling against people who are gone.
        db.Pimps.RemoveRange(await db.Pimps.ToListAsync(ct));
        foreach (var player in players)
            player.Crew.Clear();

        // A war in progress is over, unpaid. There is nothing to pay it out of and nothing left to
        // fight over, and a war whose clock outlived its world would settle in week two of the next
        // season over fights that happened in the last one. Settled wars stay: a crew's record is the
        // same kind of thing as a player's honours, and neither of them is an asset.
        foreach (var war in await db.AllianceWars.Where(x => x.Status == AllianceWarStatuses.Active).ToListAsync(ct))
        {
            war.Status = AllianceWarStatuses.Settled;
            war.SettledAtUtc = nowUtc;
            war.Outcome = "The season ended before the war did.";
        }

        foreach (var ground in await db.Territories.ToListAsync(ct))
        {
            ground.HolderId = null;
            ground.GarrisonThugs = 0;
            ground.GarrisonPimpId = null;
            ground.HeldSinceUtc = null;
            ground.ProtectedUntilUtc = null;
            ground.DevelopmentLevel = 0;
            ground.DevelopingToLevel = null;
            ground.DevelopmentCompletesAtUtc = null;
        }

        foreach (var crew in await db.Alliances.ToListAsync(ct))
        {
            crew.Treasury = 0;
            crew.OffensiveThugs = 0;
            crew.DefensiveThugs = 0;
        }
    }
}

/// <summary>What one roll did: the season that ended, the one that opened, and how many were in it.</summary>
public sealed record SeasonRoll(Season Ended, Season Opened, int Players);

/// <summary>
/// Holds the gate shut between rolls, so a burst of requests arriving in the same second cannot each
/// decide the season is over. Registered as a singleton, like the standings sampler's own gate.
/// </summary>
public sealed class SeasonSchedule
{
    private long _nextDueTicks = DateTime.MinValue.Ticks;

    /// <summary>
    /// True for exactly one caller once the clock has run out. Pushed forward by an hour on a claim
    /// rather than to the next season's end, because the claimer may yet find nothing to do - the
    /// season may have been rolled by hand a second earlier - and a gate that shut for a month on a
    /// wasted claim would stop the next roll from ever happening.
    /// </summary>
    public bool TryClaim(DateTime nowUtc)
    {
        while (true)
        {
            var current = Interlocked.Read(ref _nextDueTicks);
            if (nowUtc.Ticks < current)
                return false;
            var next = nowUtc.AddHours(1).Ticks;
            if (Interlocked.CompareExchange(ref _nextDueTicks, next, current) == current)
                return true;
        }
    }

    /// <summary>Shuts the gate until a known end date, so an open season costs nothing to have.</summary>
    public void NoteNextDue(DateTime dueUtc)
        => Interlocked.Exchange(ref _nextDueTicks, dueUtc.Ticks);
}
