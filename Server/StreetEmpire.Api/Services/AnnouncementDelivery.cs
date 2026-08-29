using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

/// <summary>Optional broadcast settings for game updates.</summary>
public sealed class AnnouncementOptions
{
    /// <summary>A Discord channel webhook URL. Blank means updates stay in-game only.</summary>
    public string DiscordWebhookUrl { get; set; } = string.Empty;

    /// <summary>The webhook display name Discord shows beside the broadcast.</summary>
    public string DiscordUsername { get; set; } = "Street Empire";

    public bool DiscordConfigured => !string.IsNullOrWhiteSpace(DiscordWebhookUrl);
}

public sealed class DiscordAnnouncementSender(
    HttpClient http,
    GameDbContext db,
    IOptions<AnnouncementOptions> options,
    ILogger<DiscordAnnouncementSender> logger)
{
    public bool Delivers => options.Value.DiscordConfigured;

    public async Task<bool> SendAsync(GameAnnouncement post, CancellationToken ct)
    {
        var settings = await EffectiveSettingsAsync(db, options.Value, ct);
        if (!settings.DiscordConfigured)
        {
            logger.LogInformation(
                "Announcement '{Title}' asked for Discord broadcast, but no webhook is configured.",
                post.Title);
            return false;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, settings.WebhookUrl);
            request.Content = new StringContent(
                JsonSerializer.Serialize(BuildPayload(post, settings.Username)),
                Encoding.UTF8,
                "application/json");

            using var response = await http.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
                return true;

            logger.LogWarning(
                "Discord refused announcement '{Title}' with {Status}: {Body}",
                post.Title,
                response.StatusCode,
                await response.Content.ReadAsStringAsync(ct));
            return false;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Could not reach Discord to broadcast announcement '{Title}'.", post.Title);
            return false;
        }
    }

    internal static async Task<AnnouncementDeliverySettingsResponse> SettingsResponseAsync(
        GameDbContext db,
        AnnouncementOptions fallback,
        CancellationToken ct)
    {
        var row = await db.GameSettings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == 1, ct);
        var settings = EffectiveSettings(row, fallback);
        return new AnnouncementDeliverySettingsResponse(
            settings.DiscordConfigured,
            settings.UsesStoredWebhook,
            Host(settings.WebhookUrl),
            settings.Username,
            row?.UpdatedAtUtc ?? DateTime.UtcNow,
            row?.UpdatedBy);
    }

    internal static bool IsAllowedWebhookUrl(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && (uri.Host.Equals("discord.com", StringComparison.OrdinalIgnoreCase)
                || uri.Host.Equals("discordapp.com", StringComparison.OrdinalIgnoreCase)
                || uri.Host.EndsWith(".discord.com", StringComparison.OrdinalIgnoreCase)
                || uri.Host.EndsWith(".discordapp.com", StringComparison.OrdinalIgnoreCase));

    internal static object BuildPayload(GameAnnouncement post, string username)
    {
        var fields = PatchSections(post)
            .Select(section => new { name = section.Name, value = Limit(section.Value, 1_000), inline = false })
            .ToArray();

        return new
        {
            username = string.IsNullOrWhiteSpace(username) ? "Street Empire" : username.Trim(),
            embeds = new[]
            {
                new
                {
                    title = post.Version is null ? post.Title : $"{post.Title} ({post.Version})",
                    description = Limit(post.Body, 3_500),
                    color = SeverityColor(post.Severity),
                    fields,
                    timestamp = post.PublishedAtUtc,
                },
            },
        };
    }

    private static IEnumerable<(string Name, string Value)> PatchSections(GameAnnouncement post)
    {
        if (!string.IsNullOrWhiteSpace(post.Added)) yield return ("Added", post.Added);
        if (!string.IsNullOrWhiteSpace(post.Changed)) yield return ("Changed", post.Changed);
        if (!string.IsNullOrWhiteSpace(post.Fixed)) yield return ("Fixed", post.Fixed);
        if (!string.IsNullOrWhiteSpace(post.KnownIssues)) yield return ("Known issues", post.KnownIssues);
    }

    private static int SeverityColor(string severity)
        => severity.Equals("Maintenance", StringComparison.OrdinalIgnoreCase) ? 0xdc3545
            : severity.Equals("Warning", StringComparison.OrdinalIgnoreCase) ? 0xffc107
            : severity.Equals("Event", StringComparison.OrdinalIgnoreCase) ? 0x198754
            : 0x0d6efd;

    private static string Limit(string value, int max)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : $"{trimmed[..Math.Max(0, max - 3)].TrimEnd()}...";
    }

    private static async Task<EffectiveAnnouncementSettings> EffectiveSettingsAsync(
        GameDbContext db,
        AnnouncementOptions fallback,
        CancellationToken ct)
        => EffectiveSettings(await db.GameSettings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == 1, ct), fallback);

    private static EffectiveAnnouncementSettings EffectiveSettings(GameSetting? row, AnnouncementOptions fallback)
    {
        var storedWebhook = row?.DiscordAnnouncementWebhookUrl?.Trim();
        var fallbackWebhook = fallback.DiscordWebhookUrl.Trim();
        var webhook = !string.IsNullOrWhiteSpace(storedWebhook) ? storedWebhook : fallbackWebhook;
        var username = row?.DiscordAnnouncementUsername?.Trim();
        if (string.IsNullOrWhiteSpace(username))
            username = string.IsNullOrWhiteSpace(fallback.DiscordUsername) ? "Street Empire" : fallback.DiscordUsername.Trim();

        return new EffectiveAnnouncementSettings(
            webhook,
            !string.IsNullOrWhiteSpace(webhook),
            !string.IsNullOrWhiteSpace(storedWebhook),
            username);
    }

    private static string? Host(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri.Host : null;

    private sealed record EffectiveAnnouncementSettings(
        string? WebhookUrl,
        bool DiscordConfigured,
        bool UsesStoredWebhook,
        string Username);
}
