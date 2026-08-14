namespace StreetEmpire.Api.Models;

/// <summary>
/// Live operations state, held as a single row. Persisted rather than kept in memory so maintenance
/// mode and the announcement survive a restart: a deploy is exactly when you need the game still
/// locked and the notice still showing.
/// </summary>
public sealed class GameSetting
{
    /// <summary>Fixed key so there is only ever one row.</summary>
    public int Id { get; set; } = 1;

    /// <summary>Blocks gameplay for everyone except admins.</summary>
    public bool MaintenanceMode { get; set; }

    /// <summary>Shown to players while maintenance mode is on.</summary>
    public string? MaintenanceMessage { get; set; }

    /// <summary>Site-wide banner, shown whether or not maintenance is on.</summary>
    public string? Announcement { get; set; }

    /// <summary>
    /// Tuning overrides as a JSON object of dotted path to value, layered over appsettings at runtime.
    /// Stored as one blob rather than a row per key: it is always read and written whole.
    /// </summary>
    public string? ConfigOverridesJson { get; set; }

    /// <summary>
    /// Automatic AI, persisted for the same reason maintenance mode is: an admin who turns the rivals
    /// off before a deploy expects them to still be off afterwards. Held in memory alone it silently
    /// reverted to the appsettings default on every restart.
    ///
    /// Null timings mean "use the configured default", so clearing an override is a real operation
    /// rather than having to remember what the default was.
    /// </summary>
    public bool BotAutomationEnabled { get; set; }
    public int? BotTickSeconds { get; set; }
    public int? BotRoundsPerTick { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? UpdatedBy { get; set; }
}
