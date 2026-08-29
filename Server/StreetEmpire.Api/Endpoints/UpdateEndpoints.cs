using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;
using StreetEmpire.Api.Services;
using static StreetEmpire.Api.Support.LiveOpsStore;

namespace StreetEmpire.Api.Endpoints;

/// <summary>Durable game updates: the history behind the temporary Live Ops banner.</summary>
internal static class UpdateEndpoints
{
    private static readonly HashSet<string> Categories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Info",
        "Patch",
        "Balance",
        "Event",
        "Maintenance",
    };

    private static readonly HashSet<string> Severities = new(StringComparer.OrdinalIgnoreCase)
    {
        "Info",
        "Warning",
        "Event",
        "Maintenance",
    };

    internal static void MapUpdateEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/game/updates", async (
            CurrentPlayerService current,
            GameDbContext db,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            return Results.Ok(await UpdatesForAsync(db, player.Account.LastSeenAnnouncementAtUtc, DateTime.UtcNow, 50, ct));
        }).RequireAuthorization();

        app.MapPost("/api/game/updates/seen", async (
            CurrentPlayerService current,
            GameDbContext db,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var now = DateTime.UtcNow;
            var latest = await Visible(db.GameAnnouncements.AsNoTracking(), now)
                .OrderByDescending(x => x.PublishedAtUtc)
                .Select(x => (DateTime?)x.PublishedAtUtc)
                .FirstOrDefaultAsync(ct);
            player.Account.LastSeenAnnouncementAtUtc = latest ?? now;
            await db.SaveChangesAsync(ct);

            return Results.Ok(await UpdatesForAsync(db, player.Account.LastSeenAnnouncementAtUtc, now, 50, ct));
        }).RequireAuthorization();

        app.MapGet("/api/admin/updates", async (
            bool? includeArchived,
            CurrentPlayerService current,
            GameDbContext db,
            CancellationToken ct) =>
        {
            var admin = await current.GetAsync(ct);
            if (admin is null) return Results.Unauthorized();
            if (!admin.Account.IsAdmin) return Results.Forbid();

            var query = db.GameAnnouncements.AsNoTracking();
            if (includeArchived != true)
                query = query.Where(x => x.ArchivedAtUtc == null);

            var posts = await query
                .OrderBy(x => x.IsDraft)
                .ThenByDescending(x => x.IsPinned)
                .ThenByDescending(x => x.PublishedAtUtc)
                .ThenByDescending(x => x.Id)
                .Take(100)
                .Select(x => ToAdminResponse(x))
                .ToListAsync(ct);
            return Results.Ok(posts);
        }).RequireAuthorization();

        app.MapGet("/api/admin/updates/delivery", async (
            CurrentPlayerService current,
            GameDbContext db,
            IOptions<AnnouncementOptions> options,
            CancellationToken ct) =>
        {
            var admin = await current.GetAsync(ct);
            if (admin is null) return Results.Unauthorized();
            if (!admin.Account.IsAdmin) return Results.Forbid();

            return Results.Ok(await DiscordAnnouncementSender.SettingsResponseAsync(db, options.Value, ct));
        }).RequireAuthorization();

        app.MapPut("/api/admin/updates/delivery", async (
            AnnouncementDeliverySettingsRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            AdminService admins,
            IOptions<AnnouncementOptions> options,
            CancellationToken ct) =>
        {
            var admin = await current.GetAsync(ct);
            if (admin is null) return Results.Unauthorized();
            if (!admin.Account.IsAdmin) return Results.Forbid();

            var settings = await LiveOpsAsync(db, ct);
            var changes = new List<string>();
            var now = DateTime.UtcNow;

            if (request.ClearDiscordWebhook)
            {
                settings.DiscordAnnouncementWebhookUrl = null;
                changes.Add("Discord webhook cleared");
            }
            else if (request.DiscordWebhookUrl is not null)
            {
                var webhook = request.DiscordWebhookUrl.Trim();
                if (webhook.Length == 0)
                {
                    settings.DiscordAnnouncementWebhookUrl = null;
                    changes.Add("Discord webhook cleared");
                }
                else
                {
                    if (webhook.Length > 512)
                        return Results.BadRequest(new { error = "Discord webhook URL must be 512 characters or less." });
                    if (!DiscordAnnouncementSender.IsAllowedWebhookUrl(webhook))
                        return Results.BadRequest(new { error = "Discord webhook must be an https://discord.com or https://discordapp.com webhook URL." });
                    settings.DiscordAnnouncementWebhookUrl = webhook;
                    changes.Add("Discord webhook updated");
                }
            }

            if (request.DiscordUsername is not null)
            {
                var username = request.DiscordUsername.Trim();
                if (username.Length > 80)
                    return Results.BadRequest(new { error = "Discord webhook name must be 80 characters or less." });
                settings.DiscordAnnouncementUsername = username.Length == 0 ? null : username;
                changes.Add("Discord webhook name updated");
            }

            if (changes.Count == 0)
                return Results.BadRequest(new { error = "Nothing to change." });

            settings.UpdatedAtUtc = now;
            settings.UpdatedBy = admin.Account.Username;
            admins.Record(admin.Account, "AnnouncementDelivery", null, string.Join("; ", changes), request.Reason, now);
            await db.SaveChangesAsync(ct);

            return Results.Ok(await DiscordAnnouncementSender.SettingsResponseAsync(db, options.Value, ct));
        }).RequireAuthorization();

        app.MapPost("/api/admin/updates", async (
            AdminGameAnnouncementRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            AdminService admins,
            DiscordAnnouncementSender discord,
            CancellationToken ct) =>
        {
            var admin = await current.GetAsync(ct);
            if (admin is null) return Results.Unauthorized();
            if (!admin.Account.IsAdmin) return Results.Forbid();

            try
            {
                var now = DateTime.UtcNow;
                var post = Build(request, admin.Account, now);
                db.GameAnnouncements.Add(post);
                admins.Record(
                    admin.Account,
                    post.IsDraft ? "DraftUpdate" : "PublishUpdate",
                    null,
                    $"{(post.IsDraft ? "drafted" : "published")} update: {post.Title}",
                    request.Reason,
                    now);
                await db.SaveChangesAsync(ct);
                await SendDiscordIfNeededAsync(post, discord, db, ct);
                return Results.Ok(ToAdminResponse(post));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();

        app.MapPut("/api/admin/updates/{id:long}", async (
            long id,
            AdminGameAnnouncementRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            AdminService admins,
            DiscordAnnouncementSender discord,
            CancellationToken ct) =>
        {
            var admin = await current.GetAsync(ct);
            if (admin is null) return Results.Unauthorized();
            if (!admin.Account.IsAdmin) return Results.Forbid();

            var post = await db.GameAnnouncements.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (post is null) return Results.NotFound(new { error = "Update not found." });

            try
            {
                var now = DateTime.UtcNow;
                Apply(post, request, admin.Account, now);
                admins.Record(
                    admin.Account,
                    post.IsDraft ? "EditUpdateDraft" : "EditUpdate",
                    null,
                    $"{(post.IsDraft ? "edited draft" : "edited update")}: {post.Title}",
                    request.Reason,
                    now);
                await db.SaveChangesAsync(ct);
                await SendDiscordIfNeededAsync(post, discord, db, ct);
                return Results.Ok(ToAdminResponse(post));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();

        app.MapPost("/api/admin/updates/{id:long}/archive", async (
            long id,
            AdminGameAnnouncementArchiveRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            AdminService admins,
            CancellationToken ct) =>
        {
            var admin = await current.GetAsync(ct);
            if (admin is null) return Results.Unauthorized();
            if (!admin.Account.IsAdmin) return Results.Forbid();

            var post = await db.GameAnnouncements.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (post is null) return Results.NotFound(new { error = "Update not found." });

            var now = DateTime.UtcNow;
            post.ArchivedAtUtc = request.Archived ? now : null;
            post.UpdatedAtUtc = now;
            post.UpdatedByUsername = admin.Account.Username;
            admins.Record(
                admin.Account,
                request.Archived ? "ArchiveUpdate" : "RestoreUpdate",
                null,
                $"{(request.Archived ? "archived" : "restored")} update: {post.Title}",
                request.Reason,
                now);
            await db.SaveChangesAsync(ct);
            return Results.Ok(ToAdminResponse(post));
        }).RequireAuthorization();
    }

    internal static Task<GameUpdatesResponse> UpdatesForAsync(
        GameDbContext db,
        DateTime? lastSeenAtUtc,
        DateTime nowUtc,
        int limit,
        CancellationToken ct)
        => BuildUpdatesAsync(Visible(db.GameAnnouncements.AsNoTracking(), nowUtc), lastSeenAtUtc, limit, ct);

    private static IQueryable<GameAnnouncement> Visible(IQueryable<GameAnnouncement> query, DateTime nowUtc)
        => query.Where(x => !x.IsDraft
            && x.ArchivedAtUtc == null
            && x.PublishedAtUtc <= nowUtc
            && (x.ExpiresAtUtc == null || x.ExpiresAtUtc > nowUtc));

    private static async Task<GameUpdatesResponse> BuildUpdatesAsync(
        IQueryable<GameAnnouncement> visible,
        DateTime? lastSeenAtUtc,
        int limit,
        CancellationToken ct)
    {
        var unread = await visible.CountAsync(x => lastSeenAtUtc == null || x.PublishedAtUtc > lastSeenAtUtc, ct);
        var posts = await visible
            .OrderByDescending(x => x.IsPinned)
            .ThenByDescending(x => x.PublishedAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(Math.Clamp(limit, 1, 100))
            .Select(x => ToPlayerResponse(x, lastSeenAtUtc))
            .ToListAsync(ct);
        return new GameUpdatesResponse(posts, unread, lastSeenAtUtc);
    }

    private static async Task SendDiscordIfNeededAsync(
        GameAnnouncement post,
        DiscordAnnouncementSender discord,
        GameDbContext db,
        CancellationToken ct)
    {
        if (post.IsDraft || post.ArchivedAtUtc is not null || !post.SendToDiscord || post.DiscordSentAtUtc is not null)
            return;

        if (!await discord.SendAsync(post, ct))
            return;

        post.DiscordSentAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private static GameAnnouncement Build(AdminGameAnnouncementRequest request, PlayerAccount actor, DateTime nowUtc)
    {
        var post = new GameAnnouncement
        {
            CreatedByAccountId = actor.Id,
            CreatedByUsername = actor.Username,
            CreatedAtUtc = nowUtc,
        };
        Apply(post, request, actor, nowUtc, creating: true);
        return post;
    }

    private static void Apply(GameAnnouncement post, AdminGameAnnouncementRequest request, PlayerAccount actor, DateTime nowUtc, bool creating = false)
    {
        var title = request.Title?.Trim() ?? string.Empty;
        if (title.Length is < 3 or > 96)
            throw new GameRuleException("Update title must be 3-96 characters.");

        var body = NormalizeBody(request.Body);
        if (body.Length is < 10 or > 4_000)
            throw new GameRuleException("Update body must be 10-4000 characters.");

        var category = NormalizeCategory(request.Category);
        var severity = NormalizeSeverity(request.Severity);
        var version = NormalizeOptional(request.Version, 32);
        var actionLabel = NormalizeOptional(request.ActionLabel, 40);
        var actionUrl = NormalizeOptional(request.ActionUrl, 240);
        var added = NormalizeSection(request.Added);
        var changed = NormalizeSection(request.Changed);
        var fixedItems = NormalizeSection(request.Fixed);
        var knownIssues = NormalizeSection(request.KnownIssues);
        if (actionLabel is not null && actionUrl is null)
            throw new GameRuleException("An action label needs an action URL.");
        if (actionUrl is not null && !IsAllowedActionUrl(actionUrl))
            throw new GameRuleException("Action URL must be a site path or an http(s) URL.");
        var isDraft = request.IsDraft ?? post.IsDraft;
        var publishedAt = request.PublishedAtUtc ?? (creating ? nowUtc : post.PublishedAtUtc);
        if (!isDraft && publishedAt > nowUtc)
            throw new GameRuleException("Published updates cannot have a future publish time. Save it as a draft first.");
        if (request.ExpiresAtUtc is { } expires && expires <= publishedAt)
            throw new GameRuleException("Expiry must be after the publish time.");

        post.Title = title;
        post.Body = body;
        post.Category = category;
        post.Severity = severity;
        post.Version = version;
        post.ActionLabel = actionLabel;
        post.ActionUrl = actionUrl;
        post.IsDraft = isDraft;
        post.IsPinned = request.IsPinned ?? post.IsPinned;
        post.ShowOnce = request.ShowOnce ?? post.ShowOnce;
        post.SendToDiscord = request.SendToDiscord ?? post.SendToDiscord;
        post.PublishedAtUtc = publishedAt;
        post.ExpiresAtUtc = request.ExpiresAtUtc;
        post.Added = added;
        post.Changed = changed;
        post.Fixed = fixedItems;
        post.KnownIssues = knownIssues;
        post.UpdatedAtUtc = creating ? null : nowUtc;
        post.UpdatedByUsername = creating ? null : actor.Username;
    }

    private static string NormalizeBody(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var lines = value.Replace("\r\n", "\n").Replace('\r', '\n')
            .Split('\n')
            .Select(line => string.Join(' ', line.Trim().Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries)));
        return string.Join('\n', lines).Trim();
    }

    private static string NormalizeCategory(string? value)
    {
        var trimmed = value?.Trim() ?? "Info";
        var category = Categories.FirstOrDefault(x => string.Equals(x, trimmed, StringComparison.OrdinalIgnoreCase));
        if (category is null)
            throw new GameRuleException($"Update category must be one of: {string.Join(", ", Categories.Order())}.");
        return category;
    }

    private static string NormalizeSeverity(string? value)
    {
        var trimmed = value?.Trim() ?? "Info";
        var severity = Severities.FirstOrDefault(x => string.Equals(x, trimmed, StringComparison.OrdinalIgnoreCase));
        if (severity is null)
            throw new GameRuleException($"Update severity must be one of: {string.Join(", ", Severities.Order())}.");
        return severity;
    }

    private static string? NormalizeOptional(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > max)
            throw new GameRuleException($"Keep optional fields under {max} characters.");
        return trimmed;
    }

    private static string? NormalizeSection(string? value)
    {
        var section = NormalizeBody(value);
        if (section.Length == 0) return null;
        if (section.Length > 2_000)
            throw new GameRuleException("Patch note sections must be 2000 characters or less.");
        return section;
    }

    private static bool IsAllowedActionUrl(string value)
        => value.StartsWith("/", StringComparison.Ordinal)
            || Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static GameAnnouncementResponse ToPlayerResponse(GameAnnouncement post, DateTime? lastSeenAtUtc)
        => new(
            post.Id,
            post.Title,
            post.Body,
            post.Category,
            post.Severity,
            post.Version,
            post.ActionLabel,
            post.ActionUrl,
            post.IsPinned,
            post.ShowOnce,
            post.PublishedAtUtc,
            post.ExpiresAtUtc,
            post.Added,
            post.Changed,
            post.Fixed,
            post.KnownIssues,
            lastSeenAtUtc == null || post.PublishedAtUtc > lastSeenAtUtc);

    private static AdminGameAnnouncementResponse ToAdminResponse(GameAnnouncement post)
        => new(
            post.Id,
            post.Title,
            post.Body,
            post.Category,
            post.Severity,
            post.Version,
            post.ActionLabel,
            post.ActionUrl,
            post.IsDraft,
            post.IsPinned,
            post.ShowOnce,
            post.SendToDiscord,
            post.DiscordSentAtUtc,
            post.PublishedAtUtc,
            post.ExpiresAtUtc,
            post.ArchivedAtUtc,
            post.Added,
            post.Changed,
            post.Fixed,
            post.KnownIssues,
            post.CreatedByUsername,
            post.CreatedAtUtc,
            post.UpdatedByUsername,
            post.UpdatedAtUtc);
}
