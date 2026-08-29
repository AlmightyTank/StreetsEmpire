namespace StreetEmpire.Api.Models;

/// <summary>
/// A durable note about the game itself: patch notes, events, balance changes and maintenance aftercare.
/// LiveOps.Announcement is the loud banner for right now; this is the history players can come back to.
/// </summary>
public sealed class GameAnnouncement
{
    public long Id { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    /// <summary>Patch, Event, Balance, Maintenance, or Info. Kept as text so a post is readable in SQL.</summary>
    public string Category { get; set; } = "Info";

    /// <summary>Info, Warning, Event, or Maintenance. Drives urgency without changing the archive bucket.</summary>
    public string Severity { get; set; } = "Info";

    public string? Version { get; set; }

    public string? ActionLabel { get; set; }
    public string? ActionUrl { get; set; }

    public bool IsDraft { get; set; }
    public bool IsPinned { get; set; }
    public bool ShowOnce { get; set; }
    public bool SendToDiscord { get; set; }
    public DateTime PublishedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime? ArchivedAtUtc { get; set; }
    public DateTime? DiscordSentAtUtc { get; set; }

    public string? Added { get; set; }
    public string? Changed { get; set; }
    public string? Fixed { get; set; }
    public string? KnownIssues { get; set; }

    public Guid CreatedByAccountId { get; set; }
    public string CreatedByUsername { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public string? UpdatedByUsername { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}
