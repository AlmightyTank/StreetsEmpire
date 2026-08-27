using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Data;

namespace StreetEmpire.Api.Services;

/// <summary>
/// Throws away codes that can never be used again.
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

    private async Task SweepAsync(CancellationToken ct)
    {
        try
        {
            // A scope of its own: this is a background loop, and the database context is scoped.
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();

            var days = Math.Max(1, options.Value.CodeRetentionDays);
            var cutoff = DateTime.UtcNow.AddDays(-days);
            var removed = await db.EmailVerifications
                .Where(x => x.CreatedAtUtc < cutoff)
                .ExecuteDeleteAsync(ct);

            // Only said when it did something. A daily line reporting zero is a line that trains
            // everybody to stop reading the log.
            if (removed > 0)
                logger.LogInformation("Swept {Count} verification code(s) older than {Days} day(s).", removed, days);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A failed sweep is a table that stays big for another day, which is not worth stopping a
            // game server over.
            logger.LogWarning(ex, "Could not sweep old verification codes.");
        }
    }
}
