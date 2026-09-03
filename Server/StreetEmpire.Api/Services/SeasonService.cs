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
        {
            if (AlignOpeningSeasonLength(running))
                await db.SaveChangesAsync(ct);
            return running;
        }

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

        // Read through CurrentAsync rather than straight off the table, so the decision is made against
        // the dates the configuration actually names. Season one's end is derived, and the row can be
        // holding an older answer - a world opened on the thirty-day clock has an end date two months
        // behind the ninety-day one replacing it. Deciding off the row would roll the world on the
        // first request after the deploy that turned seasons on, deleting every empire in it against a
        // deadline the configuration had already moved and nobody was playing to.
        var current = await CurrentAsync(nowUtc, ct);
        if (current.EndsAtUtc > nowUtc)
            return null;

        return await RollAsync(nowUtc, ct);
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

        // Season 1 is a raid race: the table is ordered by what an empire took off other players, not
        // by what it managed to keep. Net worth is still stored beside the finish for context, but the
        // honour is earned by the take.
        var standing = await RaidStandingsForAsync(season, take: 0, ct);
        var players = standing.Select(x => x.Player).ToList();

        var results = new List<SeasonResult>();
        for (var index = 0; index < standing.Count; index++)
        {
            var row = standing[index];
            var player = row.Player;
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
                NetWorth = row.NetWorth,
                RaidScore = row.RaidScore,
                RaidCashTaken = row.RaidCashTaken,
                RaidWeedTaken = row.RaidWeedTaken,
                RaidCokeTaken = row.RaidCokeTaken,
                Honour = SeasonHonours.For(rank)
            });
        }

        db.SeasonResults.AddRange(results);
        season.Status = SeasonStatuses.Ended;
        season.EndedAtUtc = nowUtc;
        season.Players = standing.Count;

        await WipeTheWorldAsync(nowUtc, players, ct);

        // What this season alone was worth, read off the results just written.
        var earned = results.ToDictionary(x => x.PlayerId, x => _options.Seasons.HeadStartFor(x.Honour));
        var next = Open(season.Number + 1, nowUtc);

        foreach (var player in players)
        {
            /*
              A top-ten finish adds to the pile; anything less empties it.

              This is the one place in the game that compounds, and it is meant to. A season used to
              end and hand its winner a leg up worth an hour, which made the last fortnight of a season
              you had already won worth nothing to play - there was no run to protect. Now there is:
              the streak is the high, and the whole of it is on the table every season.

              Emptied rather than reduced on a bad year, and emptied for eleventh place exactly as hard
              as for last. That is what stops it becoming an aristocracy - the pile is only ever one
              ordinary season away from nothing, and everyone who has one knows it.

              Everybody in the world is in `players`, because the roll ranks the whole board. So a
              player who spent the season doing nothing is not skipped here, they are reset - which is
              what "miss a season and you lose it" has to mean in a world where standing still is
              itself a finish outside the top ten.
            */
            var won = earned.GetValueOrDefault(player.Id);
            if (won > 0)
            {
                player.SeasonHeadStart += won;
                player.SeasonTopTenStreak++;
            }
            else
            {
                player.SeasonHeadStart = 0;
                player.SeasonTopTenStreak = 0;
            }

            StartingState.Apply(player, _options, nowUtc, player.SeasonHeadStart);
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
                    + (player.SeasonHeadStart > 0
                        ? $", with {player.SeasonHeadStart:C0} on account of {player.SeasonTopTenStreak:N0}"
                          + $" season{(player.SeasonTopTenStreak == 1 ? string.Empty : "s")} running in the top ten."
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

    /// <summary>Live standings for the season being played, ranked by raid take.</summary>
    public async Task<IReadOnlyList<SeasonStanding>> CurrentTableForAsync(Season season, int take, CancellationToken ct = default)
        => (await RaidStandingsForAsync(season, take, ct))
            .Select((x, index) => new SeasonStanding(
                x.Player.Id,
                index + 1,
                x.Player.Name,
                x.Player.City,
                x.Player.Alliance?.Name,
                x.NetWorth,
                x.RaidScore,
                x.RaidCashTaken,
                x.RaidWeedTaken,
                x.RaidCokeTaken,
                SeasonHonours.For(index + 1)))
            .ToList();

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
        var startedAt = number == 1 ? OpeningStart(nowUtc) : nowUtc;
        var season = new Season
        {
            Number = number,
            Name = string.Format(_options.Seasons.NameFormat, number),
            Status = SeasonStatuses.Running,
            StartedAtUtc = startedAt,
            EndsAtUtc = startedAt.AddDays(Math.Max(1, _options.Seasons.LengthDays))
        };
        db.Seasons.Add(season);
        schedule.NoteNextDue(season.EndsAtUtc);
        return season;
    }

    /// <summary>
    /// When season one began, as a date somebody chose rather than a moment that happened to somebody.
    ///
    /// Unset means "whenever the first request landed", which is fine for a world nobody is watching
    /// and no good for one being launched: the ninety days would start on a deploy-time health check
    /// or a bot's first tick, and the answer to "when does this end" would be a row in the database
    /// nobody could have predicted. Season two onwards needs none of this - a season starts when the
    /// one before it ended, and that is a date the world watched happen.
    /// </summary>
    private DateTime OpeningStart(DateTime nowUtc)
    {
        if (_options.Seasons.StartsAtUtc is not { } configured)
            return nowUtc;

        // Every kind handled rather than the one the binder is assumed to hand over. A date in a config
        // file goes through DateTime.Parse, which reads a trailing Z by converting to the machine's own
        // zone and labelling the result Local - so a server in Chicago would otherwise open the season
        // five hours out, and only a server sitting in UTC would ever prove it right. Unspecified is
        // taken at its word, because the setting says UTC in its name.
        return configured.Kind switch
        {
            DateTimeKind.Utc => configured,
            DateTimeKind.Local => configured.ToUniversalTime(),
            _ => DateTime.SpecifyKind(configured, DateTimeKind.Utc)
        };
    }

    /// <summary>
    /// Pulls season one onto the dates the configuration names, for a world that opened it before they
    /// were set - which is every world running before the ninety-day race was decided on.
    ///
    /// Only ever season one, and only while it is still running. A finished season is a record, and a
    /// record that rewrote itself when somebody edited a config file would not be one.
    /// </summary>
    private bool AlignOpeningSeasonLength(Season season)
    {
        if (season.Number != 1 || season.Status != SeasonStatuses.Running)
            return false;

        var expectedStart = OpeningStart(season.StartedAtUtc);
        var expectedEnd = expectedStart.AddDays(Math.Max(1, _options.Seasons.LengthDays));
        if (season.StartedAtUtc == expectedStart && season.EndsAtUtc == expectedEnd)
        {
            schedule.NoteNextDue(season.EndsAtUtc);
            return false;
        }

        season.StartedAtUtc = expectedStart;
        season.EndsAtUtc = expectedEnd;
        schedule.NoteNextDue(season.EndsAtUtc);
        return true;
    }

    private async Task<List<SeasonContestant>> RaidStandingsForAsync(Season season, int take, CancellationToken ct)
    {
        // No upper bound on purpose. Every log in the table belongs to the season being played, because
        // the last roll wiped the ones before it - and a season that stopped counting on its end date
        // would throw away every raid landed between that date and the request that actually notices
        // the clock has run out, which on a quiet night is a night's play scored at nothing.
        var raidTakes = await db.CombatLogs.AsNoTracking()
            .Where(x => x.CreatedAtUtc >= season.StartedAtUtc
                        && x.Method == AttackMethods.Raid
                        && x.Outcome == "Victory")
            .GroupBy(x => x.AttackerId)
            .Select(g => new
            {
                PlayerId = g.Key,
                Cash = g.Sum(x => x.CashStolen),
                Weed = g.Sum(x => x.WeedStolen),
                Coke = g.Sum(x => x.CokeStolen)
            })
            .ToDictionaryAsync(x => x.PlayerId, ct);

        // The roll asks for everybody, because a season's record is written for everybody who was in it.
        // A page asks for a board, and a board of raid takes has nobody on it who has not raided - so
        // reading every empire in the world to print fifty rows would be loading the whole table to
        // throw all but the raiders away. Untracked with it: the board is a read, and the roll is the
        // only caller that goes on to change any of the players it asks for.
        var whole = take <= 0;
        var scorers = raidTakes.Keys.ToList();
        var contestants = whole
            ? await db.Players.Include(x => x.Alliance).ToListAsync(ct)
            : await db.Players.AsNoTracking()
                .Include(x => x.Alliance)
                .Where(x => scorers.Contains(x.Id))
                .ToListAsync(ct);

        var ranked = contestants
            .Select(player =>
            {
                raidTakes.TryGetValue(player.Id, out var loot);
                var cash = loot?.Cash ?? 0;
                var weed = loot?.Weed ?? 0;
                var coke = loot?.Coke ?? 0;
                return new SeasonContestant(
                    player,
                    economy.CalculateNetWorth(player),
                    RaidScore(cash, weed, coke),
                    cash,
                    weed,
                    coke);
            })
            .OrderByDescending(x => x.RaidScore)
            .ThenBy(x => x.Player.CreatedAtUtc)
            .ToList();

        return whole ? ranked : ranked.Take(take).ToList();
    }

    private long RaidScore(long cash, int weed, int coke)
        => Math.Max(0, cash)
           + Math.Max(0, (long)weed) * Math.Max(0, _options.WeedNetWorth)
           + Math.Max(0, (long)coke) * Math.Max(0, _options.CokeNetWorth);

    private sealed record SeasonContestant(
        Player Player,
        long NetWorth,
        long RaidScore,
        long RaidCashTaken,
        int RaidWeedTaken,
        int RaidCokeTaken);

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
        // The town's book, and with it every hand dealt out of it. Standing resets with the empire, so
        // a job left standing would be paying last season's rep into this one.
        db.TraderJobs.RemoveRange(await db.TraderJobs.ToListAsync(ct));
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

public sealed record SeasonStanding(
    Guid PlayerId,
    int Rank,
    string PlayerName,
    string City,
    string? CrewName,
    long NetWorth,
    long RaidScore,
    long RaidCashTaken,
    int RaidWeedTaken,
    int RaidCokeTaken,
    string? Honour);

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
