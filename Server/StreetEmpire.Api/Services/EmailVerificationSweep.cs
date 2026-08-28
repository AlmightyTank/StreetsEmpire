using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

/// <summary>
/// Throws away rows that can never be used again.
///
/// Nothing deleted from this table until now, so every code ever issued stayed in it - spent secrets
/// with no reason to exist, in a table that only grows. They are worth a few days ("did a code actually
/// go out on Tuesday" is a real question when somebody says they never got one) and worth nothing after
/// that.
///
/// Age is the only test, deliberately, rather than "spent or expired". A row younger than the retention
/// window is left alone whatever state it is in, so the sweep can never race the flow that is using one:
/// the worst it can do is leave something a day longer than it had to.
/// </summary>
public sealed class EmailVerificationSweep(
    IServiceProvider services,
    IOptions<EmailOptions> options,
    ILogger<EmailVerificationSweep> logger) : BackgroundService
{
    /// <summary>
    /// Daily. There is nothing time-critical here - the rows are inert, and the only cost of keeping
    /// one an extra hour is the row. An hourly sweep would be a query an hour to delete nothing.
    /// </summary>
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    /// <summary>
    /// Long enough after start that it never competes with migrations, the settings load, or the first
    /// players arriving.
    /// </summary>
    private static readonly TimeSpan FirstRun = TimeSpan.FromMinutes(5);

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
            // Shutdown. Not a fault, and not worth a line in the log that looks like one.
        }
    }

    /// <summary>
    /// Longer than the fourteen days a session cookie lives, so a live session is never swept out from
    /// under a player who is still using it. Shorter than for ever, because these rows hold an address.
    /// </summary>
    private const int SessionRetentionDays = 30;

    private async Task SweepAsync(CancellationToken ct)
    {
        try
        {
            // A scope of its own: this is a background loop, and the database context is scoped.
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();

            var days = Math.Max(1, options.Value.CodeRetentionDays);
            var cutoff = DateTime.UtcNow.AddDays(-days);
            var codes = await db.EmailVerifications
                .Where(x => x.CreatedAtUtc < cutoff)
                .ExecuteDeleteAsync(ct);

            // Assist calls go the same way and for the same reason: one is raised for every allied crew
            // every time somebody is raided, and a closed one is a finished conversation about a fight
            // that finished. Only closed ones - an open or answered call is still somebody's business,
            // however old the row is, because an answered one is a claim on thugs that have not come
            // home yet.
            var calls = await db.AllianceAssistCalls
                .Where(x => x.CreatedAtUtc < cutoff && x.Status == AllianceAssistStatuses.Closed)
                .ExecuteDeleteAsync(ct);

            // Transfers are an audit trail rather than working state, so they are kept far longer -
            // "where did the crew's guns go" is a question asked weeks later. Long enough to answer it,
            // not for ever.
            var transferCutoff = DateTime.UtcNow.AddDays(-Math.Max(days, options.Value.TransferRetentionDays));
            var transfers = await db.AllianceTransfers
                .Where(x => x.CreatedAtUtc < transferCutoff)
                .ExecuteDeleteAsync(ct);

            // Sessions, which are the only rows here holding personal data - an address and a browser
            // string. Two cutoffs rather than one: a revoked session is finished business and goes on the
            // short clock, while a live one is only removed once it is older than the cookie it belongs
            // to, since a row that outlives its ticket is a session nobody can see or end.
            var sessionCutoff = DateTime.UtcNow.AddDays(-Math.Max(days, SessionRetentionDays));
            var sessions = await db.Sessions
                .Where(x => (x.RevokedAtUtc != null && x.RevokedAtUtc < cutoff)
                            || x.LastSeenAtUtc < sessionCutoff)
                .ExecuteDeleteAsync(ct);

            // Pacts are deliberately not swept. A pact is state rather than history: an active one is
            // a truce being relied on right now, and a closed one is the record of a crew having broken
            // one, which is exactly the thing somebody will want to look up.

            // Only said when it did something. A daily line reporting zero is a line that trains
            // everybody to stop reading the log.
            if (codes + calls + transfers + sessions > 0)
                logger.LogInformation(
                    "Swept {Codes} verification code(s), {Calls} closed assist call(s), {Transfers} transfer record(s) "
                    + "and {Sessions} session(s).",
                    codes, calls, transfers, sessions);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A failed sweep is a table that stays big for another day, which is not worth stopping a
            // game server over.
            logger.LogWarning(ex, "Could not sweep old verification codes.");
        }
    }
}
