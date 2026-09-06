using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Models;
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

    /// <summary>
    /// What the bot says when it is not saying anything else. Sent with the IDENTIFY and returned to
    /// between every rotated line, so it is written once here rather than in both places.
    /// </summary>
    internal const string DefaultActivity = "Street Empire";
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
        Task? presenceTask = null;

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
                        // Started here rather than beside the heartbeat, because a presence update sent
                        // before READY is one Discord has nowhere to put yet.
                        presenceTask ??= PresenceLoopAsync(socket, sendLock, heartbeatStop.Token);
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
                    if (presenceTask is not null)
                        await SafeWaitAsync(presenceTask);
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
        if (presenceTask is not null)
            await SafeWaitAsync(presenceTask);
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
                    activities = new[] { new { name = DefaultActivity, type = 0 } }
                }
            }
        }, ct);

    private static Task SendHeartbeatAsync(ClientWebSocket socket, SemaphoreSlim sendLock, long? sequence, CancellationToken ct)
        => SendJsonAsync(socket, sendLock, new { op = 1, d = sequence }, ct);

    /// <summary>
    /// Walks the status through what the world is doing, and back to the game's own name between each.
    ///
    /// The default line is put back between every other one rather than taken as one entry in a ring,
    /// because the status is the bot's name badge before it is a dashboard: somebody glancing at the
    /// member list should read what this is, not catch it mid-sentence about crew wars.
    ///
    /// Does nothing at all unless a rotation is configured. An IDENTIFY already carries the default, so
    /// a server that never sets this has a correct status and no queries running behind it.
    /// </summary>
    private async Task PresenceLoopAsync(ClientWebSocket socket, SemaphoreSlim sendLock, CancellationToken ct)
    {
        var configured = 0;
        try
        {
            using var scope = scopes.CreateScope();
            configured = scope.ServiceProvider
                .GetRequiredService<IOptions<DiscordIntegrationOptions>>()
                .Value.PresenceRotateSeconds;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not read the Discord presence settings.");
            return;
        }

        if (configured <= 0)
            return;

        // Discord allows a handful of presence updates per twenty seconds and drops the session for
        // more. The floor is the bot's protection against a configuration typo, not a preference.
        var every = TimeSpan.FromSeconds(Math.Max(15, configured));

        // What was last sent, so a world with nothing to report is not told the same thing every
        // fifteen seconds for the rest of the day. Discord counts an unchanged presence against the
        // rate limit exactly as it counts a new one.
        string? showing = null;

        while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            foreach (var line in await PresenceLinesAsync(ct))
            {
                if (ct.IsCancellationRequested || socket.State != WebSocketState.Open)
                    return;

                if (line.Name != showing)
                {
                    await SendPresenceAsync(socket, sendLock, line.Name, line.Type, ct);
                    showing = line.Name;
                }

                await DelayQuietly(every, ct);
            }
        }
    }

    private static Task SendPresenceAsync(
        ClientWebSocket socket,
        SemaphoreSlim sendLock,
        string name,
        int type,
        CancellationToken ct)
        => SendJsonAsync(socket, sendLock, new
        {
            op = 3,
            d = new
            {
                since = (long?)null,
                status = "online",
                afk = false,
                activities = new[] { new { name, type } }
            }
        }, ct);

    /// <summary>
    /// What there is to say right now, with the game's own name between each of them.
    ///
    /// Anything the world cannot answer is simply left out rather than shown as a zero: "Watching 0
    /// crew wars" is worse than not mentioning crew wars, and a quiet server should read as a game
    /// rather than as an empty one.
    /// </summary>
    private async Task<IReadOnlyList<(string Name, int Type)>> PresenceLinesAsync(CancellationToken ct)
    {
        var lines = new List<(string Name, int Type)>();
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<Data.GameDbContext>();

            var since = DateTime.UtcNow.AddMinutes(-DiscordGuildIntegration.OnlineWindowMinutes);
            var online = await db.Sessions.AsNoTracking()
                .Where(x => x.RevokedAtUtc == null && x.LastSeenAtUtc >= since && !x.Account.IsBot)
                .Select(x => x.AccountId)
                .Distinct()
                .CountAsync(ct);
            if (online > 0)
                lines.Add(($"{online:N0} player(s) building an empire", 3));

            var pots = await scope.ServiceProvider.GetRequiredService<CasinoService>().PotsAsync(ct);
            if (pots.Count > 0)
            {
                var best = pots.Values.Max();
                if (best > 0)
                    lines.Add(($"the {Compact(best)} jackpot", 3));
            }

            var wars = await db.AllianceWars.AsNoTracking()
                .CountAsync(x => x.Status == AllianceWarStatuses.Active, ct);
            if (wars > 0)
                lines.Add(($"{wars:N0} crew war(s)", 3));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A status is not worth a stack trace an hour. The default line below still goes out, so a
            // failure here shows as a bot that stopped rotating rather than one that went quiet.
            logger.LogDebug(ex, "Could not read what the Discord status should say.");
        }

        // Interleaved rather than appended: the name badge comes back between every line.
        var rotation = new List<(string Name, int Type)> { (DefaultActivity, 0) };
        foreach (var line in lines)
        {
            rotation.Add(line);
            rotation.Add((DefaultActivity, 0));
        }

        return rotation;
    }

    /// <summary>Money as somebody would say it out loud, because a status bar has no room for commas.</summary>
    internal static string Compact(long amount) => amount switch
    {
        >= 1_000_000_000 => $"${amount / 1_000_000_000d:0.#}B",
        >= 1_000_000 => $"${amount / 1_000_000d:0.#}M",
        >= 1_000 => $"${amount / 1_000d:0.#}K",
        _ => $"${amount:N0}"
    };

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
