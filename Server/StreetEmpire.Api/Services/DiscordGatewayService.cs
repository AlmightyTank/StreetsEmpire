using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace StreetEmpire.Api.Services;

public sealed class DiscordGatewayState
{
    private readonly object _gate = new();

    public bool Connected { get; private set; }
    public DateTime? ConnectedAtUtc { get; private set; }
    public DateTime? LastHeartbeatAckAtUtc { get; private set; }
    public string? LastError { get; private set; }

    public void MarkConnected()
    {
        lock (_gate)
        {
            Connected = true;
            ConnectedAtUtc = DateTime.UtcNow;
            LastError = null;
        }
    }

    public void MarkHeartbeatAck()
    {
        lock (_gate)
        {
            LastHeartbeatAckAtUtc = DateTime.UtcNow;
            LastError = null;
        }
    }

    public void MarkOffline(string? error = null)
    {
        lock (_gate)
        {
            Connected = false;
            LastError = error;
        }
    }
}

/// <summary>
/// Keeps the Discord bot visibly online. Slash commands and role sync use HTTP, but Discord only shows
/// a bot as online while it has a Gateway session with working heartbeats.
/// </summary>
public sealed class DiscordGatewayService(
    IServiceScopeFactory scopes,
    DiscordGatewayState state,
    ILogger<DiscordGatewayService> logger) : BackgroundService
{
    private const string GatewayUrl = "wss://gateway.discord.gg/?v=10&encoding=json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            string? token = null;
            try
            {
                using var scope = scopes.CreateScope();
                var integration = scope.ServiceProvider.GetRequiredService<DiscordGuildIntegration>();
                token = await integration.GatewayBotTokenAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not read Discord Gateway settings.");
                state.MarkOffline("Could not read Discord settings.");
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                state.MarkOffline("Waiting for a Discord bot token and guild id.");
                await DelayQuietly(TimeSpan.FromSeconds(60), stoppingToken);
                continue;
            }

            try
            {
                await RunGatewaySessionAsync(token, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Discord Gateway session ended unexpectedly.");
                state.MarkOffline("Discord Gateway connection failed.");
            }

            await DelayQuietly(TimeSpan.FromSeconds(15), stoppingToken);
        }

        state.MarkOffline();
    }

    private async Task RunGatewaySessionAsync(string token, CancellationToken ct)
    {
        using var socket = new ClientWebSocket();
        using var sendLock = new SemaphoreSlim(1, 1);
        socket.Options.SetRequestHeader("User-Agent", "StreetEmpire (https://streetsempire.dev, 1.0)");
        await socket.ConnectAsync(new Uri(GatewayUrl), ct);

        long? sequence = null;
        using var heartbeatStop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        Task? heartbeatTask = null;

        while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            var payload = await ReceiveTextAsync(socket, ct);
            if (payload is null)
                break;

            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (root.TryGetProperty("s", out var sequenceElement) && sequenceElement.ValueKind == JsonValueKind.Number)
                sequence = sequenceElement.GetInt64();

            var op = root.TryGetProperty("op", out var opElement) ? opElement.GetInt32() : -1;
            switch (op)
            {
                case 0:
                    if (root.TryGetProperty("t", out var eventName)
                        && string.Equals(eventName.GetString(), "READY", StringComparison.Ordinal))
                    {
                        logger.LogInformation("Discord Gateway is ready.");
                        state.MarkConnected();
                    }
                    break;
                case 1:
                    await SendHeartbeatAsync(socket, sendLock, sequence, ct);
                    break;
                case 7:
                case 9:
                    state.MarkOffline("Discord asked the Gateway session to reconnect.");
                    heartbeatStop.Cancel();
                    if (heartbeatTask is not null)
                        await SafeWaitAsync(heartbeatTask);
                    return;
                case 10:
                    var interval = root.GetProperty("d").GetProperty("heartbeat_interval").GetInt32();
                    heartbeatTask = HeartbeatLoopAsync(socket, sendLock, () => sequence, interval, heartbeatStop.Token);
                    await SendIdentifyAsync(socket, sendLock, token, ct);
                    break;
                case 11:
                    state.MarkHeartbeatAck();
                    break;
            }
        }

        heartbeatStop.Cancel();
        if (heartbeatTask is not null)
            await SafeWaitAsync(heartbeatTask);
        state.MarkOffline("Discord Gateway disconnected.");
    }

    private async Task HeartbeatLoopAsync(
        ClientWebSocket socket,
        SemaphoreSlim sendLock,
        Func<long?> sequence,
        int intervalMs,
        CancellationToken ct)
    {
        await DelayQuietly(TimeSpan.FromMilliseconds(Math.Max(1000, intervalMs)), ct);
        while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            await SendHeartbeatAsync(socket, sendLock, sequence(), ct);
            await DelayQuietly(TimeSpan.FromMilliseconds(Math.Max(1000, intervalMs)), ct);
        }
    }

    private static Task SendIdentifyAsync(ClientWebSocket socket, SemaphoreSlim sendLock, string token, CancellationToken ct)
        => SendJsonAsync(socket, sendLock, new
        {
            op = 2,
            d = new
            {
                token,
                intents = 0,
                properties = new
                {
                    os = Environment.OSVersion.Platform.ToString(),
                    browser = "StreetEmpire",
                    device = "StreetEmpire"
                },
                presence = new
                {
                    status = "online",
                    afk = false,
                    activities = new[] { new { name = "Street Empire", type = 0 } }
                }
            }
        }, ct);

    private static Task SendHeartbeatAsync(ClientWebSocket socket, SemaphoreSlim sendLock, long? sequence, CancellationToken ct)
        => SendJsonAsync(socket, sendLock, new { op = 1, d = sequence }, ct);

    private static async Task SendJsonAsync(ClientWebSocket socket, SemaphoreSlim sendLock, object payload, CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        await sendLock.WaitAsync(ct);
        try
        {
            await socket.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
        }
        finally
        {
            sendLock.Release();
        }
    }

    private static async Task<string?> ReceiveTextAsync(ClientWebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[8192];
        using var message = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close)
                return null;
            message.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        return Encoding.UTF8.GetString(message.ToArray());
    }

    private static async Task DelayQuietly(TimeSpan delay, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delay, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    private static async Task SafeWaitAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
        }
    }
}
