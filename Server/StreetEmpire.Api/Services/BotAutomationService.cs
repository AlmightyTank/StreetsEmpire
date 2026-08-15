using Microsoft.Extensions.Options;

namespace StreetEmpire.Api.Services;

// Timing comes from BotAutomationState rather than options, because it is editable at runtime.
public sealed class BotAutomationService(
    IServiceScopeFactory scopeFactory,
    BotAutomationState state,
    ILogger<BotAutomationService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // The tick is how often the world is looked at, not how often a rival plays. A rival acts on
        // roughly every tick while it is in a sitting, and not at all between them.
        logger.LogInformation(
            "AI bot automation started with a {TickSeconds}s tick. Initial state: {State}.",
            state.TickSeconds,
            state.Enabled ? "enabled" : "disabled");

        // The interval is read again every pass rather than baked into a PeriodicTimer at startup, so
        // an admin changing the tick does not have to restart the server for it to take effect.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(state.TickSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            await RunTickAsync(stoppingToken);
        }
    }

    private async Task RunTickAsync(CancellationToken stoppingToken)
    {
        if (!state.Enabled)
            return;

        try
        {
            using var scope = scopeFactory.CreateScope();
            var bots = scope.ServiceProvider.GetRequiredService<BotSimulationService>();
            var result = await bots.RunAsync(state.RoundsPerTick, stoppingToken);

            if (result.Actions > 0)
            {
                logger.LogInformation(
                    "AI bot automation ran {Actions} action(s) across {ActiveBots} active rival(s).",
                    result.Actions,
                    result.ActiveBots);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI bot automation tick failed.");
        }
    }
}

/// <summary>
/// The live automation settings. Seeded from appsettings, replaced by whatever was persisted at
/// startup, and updated by the admin panel. Clamped on the way in so a bad value cannot stall the
/// loop or hammer the database.
/// </summary>
public sealed class BotAutomationState(IOptions<BotAutomationOptions> options)
{
    public const int MinTickSeconds = 15;
    public const int MaxTickSeconds = 3600;
    public const int MinRoundsPerTick = 1;
    public const int MaxRoundsPerTick = 10;

    private volatile bool _enabled = options.Value.Enabled;
    private int _tickSeconds = Math.Clamp(options.Value.TickSeconds, MinTickSeconds, MaxTickSeconds);
    private int _roundsPerTick = Math.Clamp(options.Value.RoundsPerTick, MinRoundsPerTick, MaxRoundsPerTick);

    public bool Enabled => _enabled;
    public int TickSeconds => Volatile.Read(ref _tickSeconds);
    public int RoundsPerTick => Volatile.Read(ref _roundsPerTick);

    /// <summary>The configured defaults, so clearing an override can restore them.</summary>
    public int DefaultTickSeconds => Math.Clamp(options.Value.TickSeconds, MinTickSeconds, MaxTickSeconds);
    public int DefaultRoundsPerTick => Math.Clamp(options.Value.RoundsPerTick, MinRoundsPerTick, MaxRoundsPerTick);

    public void SetEnabled(bool enabled) => _enabled = enabled;

    /// <summary>Null restores the configured default rather than leaving the current value in place.</summary>
    public void SetTiming(int? tickSeconds, int? roundsPerTick)
    {
        Volatile.Write(ref _tickSeconds, Math.Clamp(tickSeconds ?? DefaultTickSeconds, MinTickSeconds, MaxTickSeconds));
        Volatile.Write(ref _roundsPerTick, Math.Clamp(roundsPerTick ?? DefaultRoundsPerTick, MinRoundsPerTick, MaxRoundsPerTick));
    }
}
