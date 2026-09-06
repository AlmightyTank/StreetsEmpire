using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

/// <summary>
/// Turns the alerts the game already produces into Discord DMs, for the players who asked for them.
///
/// Built as a sweep rather than as a call at each of fifteen places something interesting happens,
/// because the game does not push alerts - it derives them. A raid writes a CombatLog, a mule writes a
/// GameActionLog, and the bell works out what those mean when somebody opens the page (see
/// <see cref="DefenceAlerts"/>). Adding a TellGameAlertAsync beside every writer would have meant
/// fifteen edits, fifteen chances to load the account wrong, and a second definition of what counts as
/// news that could drift from the one the bell uses. Reading the same two tables through the same
/// classifier means the DM and the bell cannot disagree about what happened or what to call it.
///
/// The cost of that choice is latency: a DM arrives within <see cref="Interval"/> of the event rather
/// than in the same request. For "your mule is back" that is the right trade. Anything that genuinely
/// needs to arrive in the same second should call DiscordDirectMessages directly, as the four existing
/// callers do.
/// </summary>
public sealed class DiscordAlertSweep(
    IServiceProvider services,
    ILogger<DiscordAlertSweep> logger) : BackgroundService
{
    /// <summary>
    /// Often enough that "somebody is hitting your house" is still worth reading when it lands, rarely
    /// enough that a quiet world is not paying for two queries a second to find nothing.
    /// </summary>
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(2);

    /// <summary>Past migrations, the settings load, and the gateway's first connect.</summary>
    private static readonly TimeSpan FirstRun = TimeSpan.FromMinutes(2);

    /// <summary>
    /// The most any one player is told in a single sweep.
    ///
    /// A player who has been raided eleven times in two minutes does not need eleven DMs, and Discord
    /// would rate-limit the bot for trying. They get the newest few and a line saying how many more
    /// there were; the game itself is where the full list lives.
    /// </summary>
    private const int MaxPerPlayerPerSweep = 3;

    /// <summary>
    /// How far back a sweep will ever look, however long the process was down.
    ///
    /// Without this, a server that was off for a weekend would come back and post everybody a weekend
    /// of history. Anything older than this is skipped and the watermark jumps forward: the alerts are
    /// still in the game, which is where somebody catching up should read them.
    /// </summary>
    private static readonly TimeSpan MaxCatchUp = TimeSpan.FromHours(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(FirstRun, stoppingToken);
            using var timer = new PeriodicTimer(Interval);
            do
            {
                await SweepAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Shutdown, not a fault.
        }
    }

    internal async Task SweepAsync(CancellationToken ct)
    {
        try
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
            var dms = scope.ServiceProvider.GetRequiredService<DiscordDirectMessages>();
            var links = scope.ServiceProvider.GetRequiredService<DiscordGuildIntegration>();
            await SweepAsync(db, dms, links.AlertLinkRow, DateTime.UtcNow, ct);
            await AnnounceJackpotAsync(scope.ServiceProvider, db, dms, links, ct);
            await PostCrewReportsAsync(scope.ServiceProvider, db, dms, links, DateTime.UtcNow, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A sweep that throws must not take the loop down with it: the next one is two minutes away
            // and will see the same rows, because nothing has moved the watermark.
            logger.LogWarning(ex, "A Discord alert sweep did not finish.");
        }
    }

    /// <summary>
    /// One pass. Separated from the loop so a test can run exactly one against a known clock.
    /// </summary>
    internal static async Task<int> SweepAsync(
        GameDbContext db,
        DiscordDirectMessages dms,
        Func<string, IReadOnlyList<object>?> linkFor,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var accounts = await db.Accounts
            .Include(x => x.Player)
            .Where(x => !x.IsBot && x.DiscordUserId != null)
            .Where(x => x.DiscordCombatNotices || x.DiscordCrewNotices || x.DiscordMarketNotices || x.DiscordMachineNotices)
            .ToListAsync(ct);
        if (accounts.Count == 0)
            return 0;

        // An account that has never been swept is marked as caught up and told nothing. Linking Discord
        // should not open with a recital of everything that has happened since the account was made.
        var firstTimers = accounts.Where(x => x.DiscordAlertsSentAtUtc is null).ToList();
        foreach (var account in firstTimers)
            account.DiscordAlertsSentAtUtc = nowUtc;

        var due = accounts
            .Where(x => x.DiscordAlertsSentAtUtc is not null && x.Player is not null)
            .Where(x => !firstTimers.Contains(x))
            .ToList();
        if (due.Count == 0)
        {
            await db.SaveChangesAsync(ct);
            return 0;
        }

        var floor = due.Min(x => Watermark(x, nowUtc));
        var playerIds = due.Select(x => x.Player!.Id).ToList();

        var combat = await db.CombatLogs.AsNoTracking()
            .Include(x => x.Attacker)
            .Where(x => playerIds.Contains(x.DefenderId)
                && x.Outcome != "Pending"
                && x.CreatedAtUtc > floor)
            .ToListAsync(ct);
        var logged = await db.ActionLogs.AsNoTracking()
            .Where(x => playerIds.Contains(x.PlayerId) && x.CreatedAtUtc > floor)
            .Where(DefenceAlerts.IsNotificationRow)
            .ToListAsync(ct);

        var byPlayer = due.ToDictionary(x => x.Player!.Id);
        var alerts = new Dictionary<Guid, List<AlertResponse>>();

        foreach (var log in combat)
        {
            if (!byPlayer.ContainsKey(log.DefenderId)) continue;
            Add(alerts, log.DefenderId, DefenceAlerts.ToAlert(DefenceAlerts.Describe(log, null)));
        }

        foreach (var row in logged)
        {
            if (!byPlayer.ContainsKey(row.PlayerId)) continue;
            var alert = DefenceAlerts.ToAlert(row.Id, row.Action, row.Summary, row.CreatedAtUtc, null);
            if (alert is not null) Add(alerts, row.PlayerId, alert);
        }

        var sent = 0;
        foreach (var account in due)
        {
            var since = Watermark(account, nowUtc);
            account.DiscordAlertsSentAtUtc = nowUtc;

            if (!alerts.TryGetValue(account.Player!.Id, out var mine)) continue;

            // Filtered per account, because the switches are per account and the rows above were read
            // once for everybody.
            var wanted = mine
                .Where(x => x.CreatedAtUtc > since)
                .Where(x => DiscordDirectMessages.WantsGameDm(account, DefenceAlerts.CategoryOf(x.Kind)))
                .OrderByDescending(x => x.CreatedAtUtc)
                .ToList();
            if (wanted.Count == 0) continue;

            var telling = wanted.Take(MaxPerPlayerPerSweep).ToList();
            var extra = wanted.Count - telling.Count;

            foreach (var alert in telling)
            {
                var detail = extra > 0 && alert == telling[^1]
                    ? $"{alert.Detail}\n\nAnd {extra:N0} more since you last heard from us."
                    : alert.Detail;
                await dms.TellGameAlertAsync(
                    account,
                    DefenceAlerts.CategoryOf(alert.Kind),
                    alert.Headline,
                    detail,
                    alert.CreatedAtUtc,
                    ct,
                    linkFor(alert.Kind));
                sent++;
            }
        }

        await db.SaveChangesAsync(ct);
        return sent;
    }

    /// <summary>
    /// Tells the floor when the progressive climbs past a notable number.
    ///
    /// A channel post rather than a DM, because this is the one piece of news here that belongs to
    /// nobody in particular - and the reason the casino is worth advertising at all is that people who
    /// are not currently playing see it.
    ///
    /// Announced in steps rather than at one threshold, so a single configured number works for a pot
    /// at five million and the same pot at fifty. The watermark walks back down when the jackpot drops,
    /// so the next climb is announced again rather than being permanently below a high-water mark set
    /// before somebody won it.
    /// </summary>
    private static async Task AnnounceJackpotAsync(
        IServiceProvider scope,
        GameDbContext db,
        DiscordDirectMessages dms,
        DiscordGuildIntegration links,
        CancellationToken ct)
    {
        var settings = scope.GetRequiredService<IOptions<DiscordIntegrationOptions>>().Value;
        var step = settings.JackpotAnnounceStep;
        if (step <= 0 || string.IsNullOrWhiteSpace(settings.AnnounceChannelId))
            return;

        var casino = scope.GetRequiredService<CasinoService>();
        var pots = await casino.PotsAsync(ct);
        if (pots.Count == 0)
            return;

        var best = pots.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.Ordinal).First();
        var milestone = best.Value / step * step;

        var row = await db.GameSettings.SingleOrDefaultAsync(x => x.Id == 1, ct);
        if (row is null)
        {
            row = new GameSetting { Id = 1 };
            db.GameSettings.Add(row);
        }

        var announced = row.JackpotAnnouncedAmount ?? 0;
        if (milestone <= 0 || milestone <= announced)
        {
            // Somebody won it, or the floor was reconfigured downwards. Walk the mark back so the next
            // climb is news again rather than being measured against a pot that no longer exists.
            if (milestone < announced)
            {
                row.JackpotAnnouncedAmount = milestone;
                await db.SaveChangesAsync(ct);
            }
            return;
        }

        var options = scope.GetRequiredService<IOptionsSnapshot<GameOptions>>().Value;
        var name = options.Casino.SlotMachines
            .FirstOrDefault(x => string.Equals(x.Key, best.Key, StringComparison.OrdinalIgnoreCase))?.Name
            ?? best.Key;

        var posted = await dms.PostToChannelAsync(
            settings.AnnounceChannelId,
            $"The {name} progressive just passed {milestone:C0}.",
            links.AlertLinkRow("casino"),
            ct);

        // Only moved when Discord took it. A refused post that advanced the mark would be a jackpot
        // nobody ever hears about until it passes the next step.
        if (posted)
        {
            row.JackpotAnnouncedAmount = milestone;
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// Posts each crew a report of its own week into the room the bot already made for it.
    ///
    /// The only message the bot sends that nobody triggered, which is why it is off until somebody sets
    /// a cadence: everything else here answers a command, or an event the reader's own empire produced.
    /// A room that fills with unasked-for reports gets muted, and a muted crew room takes the alerts
    /// that mattered down with it.
    ///
    /// Only crews with a mapped channel are reported on, and a crew with nobody in it is skipped rather
    /// than posted an empty report.
    /// </summary>
    private static async Task PostCrewReportsAsync(
        IServiceProvider scope,
        GameDbContext db,
        DiscordDirectMessages dms,
        DiscordGuildIntegration links,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var hours = scope.GetRequiredService<IOptions<DiscordIntegrationOptions>>().Value.CrewReportHours;
        if (hours <= 0)
            return;

        var row = await db.GameSettings.SingleOrDefaultAsync(x => x.Id == 1, ct);
        if (row is null)
        {
            row = new GameSetting { Id = 1 };
            db.GameSettings.Add(row);
        }

        // The first run after a cadence is configured records the time and posts nothing, so turning
        // the feature on does not immediately fill every crew room.
        if (row.CrewReportsPostedAtUtc is not { } last)
        {
            row.CrewReportsPostedAtUtc = nowUtc;
            await db.SaveChangesAsync(ct);
            return;
        }

        if (nowUtc - last < TimeSpan.FromHours(hours))
            return;

        var channels = await links.CrewChannelsAsync(ct);
        if (channels.Count == 0)
        {
            // Nothing to post into. The clock still moves, so a map added later does not immediately
            // produce a report covering everything since the feature was switched on.
            row.CrewReportsPostedAtUtc = nowUtc;
            await db.SaveChangesAsync(ct);
            return;
        }

        var economy = scope.GetRequiredService<EconomyService>();
        var members = await db.Players.AsNoTracking()
            .Include(x => x.Alliance)
            .Include(x => x.Hideout)
            .Where(x => x.AllianceId != null)
            .ToListAsync(ct);
        var ground = await db.Territories.AsNoTracking()
            .Include(x => x.Holder)
            .Where(x => x.Holder != null && x.Holder.AllianceId != null)
            .ToListAsync(ct);
        var fights = await db.CombatLogs.AsNoTracking()
            .Where(x => x.Outcome != "Pending" && x.CreatedAtUtc > last)
            .Select(x => new { x.AttackerId, x.DefenderId, x.Outcome })
            .ToListAsync(ct);

        foreach (var crew in members.GroupBy(x => x.AllianceId!.Value))
        {
            var name = crew.First().Alliance!.Name;
            if (!channels.TryGetValue(name, out var channelId))
                continue;

            var ids = crew.Select(x => x.Id).ToHashSet();
            var held = ground.Where(x => x.Holder!.AllianceId == crew.Key).ToList();
            var wentOut = fights.Count(x => ids.Contains(x.AttackerId));
            var landed = fights.Count(x => ids.Contains(x.AttackerId) && x.Outcome == "Victory");
            var cameIn = fights.Count(x => ids.Contains(x.DefenderId) && !ids.Contains(x.AttackerId));
            var thinnest = held.OrderBy(x => x.GarrisonThugs).Take(3).ToList();

            var report =
                $"""
                **{name} - crew report**
                Members {crew.Count():N0} | Worth {crew.Sum(x => economy.CalculateNetWorth(x)):C0}
                Ground held {held.Count:N0} | Attacks out {wentOut:N0} ({landed:N0} landed) | Attacks in {cameIn:N0}
                """;
            if (thinnest.Count > 0)
                report += "\n\nThinnest ground: "
                    + string.Join(", ", thinnest.Select(x => $"{x.Name} ({x.GarrisonThugs:N0})"));

            await dms.PostToChannelAsync(channelId, report, links.AlertLinkRow("crew"), ct);
        }

        row.CrewReportsPostedAtUtc = nowUtc;
        await db.SaveChangesAsync(ct);
    }

    private static void Add(Dictionary<Guid, List<AlertResponse>> alerts, Guid playerId, AlertResponse alert)
    {
        if (!alerts.TryGetValue(playerId, out var list))
            alerts[playerId] = list = [];
        list.Add(alert);
    }

    /// <summary>
    /// How far back to look for this account, never further than <see cref="MaxCatchUp"/>.
    /// </summary>
    private static DateTime Watermark(PlayerAccount account, DateTime nowUtc)
    {
        var floor = nowUtc - MaxCatchUp;
        var mark = account.DiscordAlertsSentAtUtc ?? floor;
        return mark < floor ? floor : mark;
    }
}
