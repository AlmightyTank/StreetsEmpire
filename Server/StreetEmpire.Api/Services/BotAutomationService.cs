using Microsoft.Extensions.Options;

namespace StreetEmpire.Api.Services;

public sealed class BotAutomationService(
    IServiceScopeFactory scopeFactory,
    BotAutomationState state,
    IOptions<BotAutomationOptions> options,
    ILogger<BotAutomationService> logger) : BackgroundService
{
    private readonly BotAutomationOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tickSeconds = Math.Clamp(_options.TickSeconds, 15, 3600);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(tickSeconds));

        logger.LogInformation(
            "AI bot automation started with a {TickSeconds}s tick. Initial state: {State}.",
            tickSeconds,
            state.Enabled ? "enabled" : "disabled");

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
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
            var result = await bots.RunAsync(Math.Clamp(_options.RoundsPerTick, 1, 10), stoppingToken);

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

public sealed class BotAutomationState(IOptions<BotAutomationOptions> options)
{
    private volatile bool _enabled = options.Value.Enabled;

    public bool Enabled => _enabled;

    public void SetEnabled(bool enabled) => _enabled = enabled;
}
