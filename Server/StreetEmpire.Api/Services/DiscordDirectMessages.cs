using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

/// <summary>
/// Sends opt-in player DMs from the bot account.
///
/// Discord warns against opening lots of DMs without a user action, so this sender is deliberately
/// narrow: a linked account must turn the switch on, and only alert-worthy gameplay rows use it.
/// </summary>
public sealed class DiscordDirectMessages(
    HttpClient http,
    GameDbContext db,
    IOptions<DiscordIntegrationOptions> options,
    ILogger<DiscordDirectMessages> logger)
{
    private const string ApiRoot = "https://discord.com/api/v10";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task TellAccountAsync(PlayerAccount account, AccountChange change, string? detail, CancellationToken ct)
    {
        if (!WantsAccountDm(account)) return;

        var playerName = account.Player?.Name ?? account.Username;
        var (_, happened) = AccountNoticeEmail.Describe(change, detail);
        var text =
            $"""
            Street Empire account notice for {playerName}

            {happened}

            {DateTime.UtcNow:HH:mm 'UTC' on d MMMM yyyy}
            """;
        await SendAsync(account.DiscordUserId!, text, ct);
    }

    public async Task TellGameAlertAsync(PlayerAccount account, AlertCategory category, string headline, string detail, DateTime whenUtc, CancellationToken ct)
    {
        if (!WantsGameDm(account, category)) return;

        var playerName = account.Player?.Name ?? account.Username;
        var text =
            $"""
            Street Empire alert for {playerName}

            {headline}
            {detail}

            {whenUtc:HH:mm 'UTC' on d MMMM yyyy}
            """;
        await SendAsync(account.DiscordUserId!, text, ct);
    }

    internal static bool WantsAccountDm(PlayerAccount account)
        => account is { IsBot: false, DiscordSecurityNotices: true, DiscordUserId: not null };

    internal static bool WantsGameDm(PlayerAccount account, AlertCategory category)
    {
        if (account is not { IsBot: false, DiscordUserId: not null })
            return false;

        return category switch
        {
            AlertCategory.Combat => account.DiscordCombatNotices,
            AlertCategory.Crew => account.DiscordCrewNotices,
            AlertCategory.Market => account.DiscordMarketNotices,
            _ => false
        };
    }

    private async Task SendAsync(string discordUserId, string text, CancellationToken ct)
    {
        var token = await BotTokenAsync(ct);
        if (string.IsNullOrWhiteSpace(token)) return;

        try
        {
            var channelId = await OpenDmChannelAsync(token, discordUserId, ct);
            if (string.IsNullOrWhiteSpace(channelId)) return;

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiRoot}/channels/{Uri.EscapeDataString(channelId)}/messages");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bot", token);
            request.Content = JsonContent.Create(new
            {
                content = OneLine(text, 1900),
                allowed_mentions = new { parse = Array.Empty<string>() }
            }, options: JsonOptions);

            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                logger.LogWarning("Discord refused a DM to {DiscordUserId} with {Status} {Reason}.", discordUserId, (int)response.StatusCode, response.ReasonPhrase);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "Could not send Discord DM to {DiscordUserId}.", discordUserId);
        }
    }

    private async Task<string?> OpenDmChannelAsync(string token, string discordUserId, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiRoot}/users/@me/channels");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bot", token);
        request.Content = JsonContent.Create(new { recipient_id = discordUserId }, options: JsonOptions);

        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Discord refused to open DM channel for {DiscordUserId} with {Status} {Reason}.", discordUserId, (int)response.StatusCode, response.ReasonPhrase);
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return document.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;
    }

    private async Task<string?> BotTokenAsync(CancellationToken ct)
    {
        var stored = await db.GameSettings.AsNoTracking()
            .Where(x => x.Id == 1)
            .Select(x => x.DiscordBotToken)
            .SingleOrDefaultAsync(ct);
        return First(stored, options.Value.BotToken);
    }

    private static string? First(string? stored, string? configured)
        => !string.IsNullOrWhiteSpace(stored) ? stored.Trim()
            : !string.IsNullOrWhiteSpace(configured) ? configured.Trim()
            : null;

    private static string OneLine(string value, int max)
    {
        var clean = value.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        return clean.Length <= max ? clean : clean[..Math.Max(0, max - 3)] + "...";
    }
}
