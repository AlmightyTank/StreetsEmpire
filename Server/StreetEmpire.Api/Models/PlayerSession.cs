namespace StreetEmpire.Api.Models;

/// <summary>
/// One signed-in browser, written down.
///
/// The session cookie is stateless: it is a sealed ticket, and the server has never had to remember
/// anything for it to work. That is a good property and it is why signing out everywhere was built as a
/// watermark - a timestamp on the account that every older ticket is compared against - rather than as
/// a list of things to delete.
///
/// A watermark cannot answer "where am I signed in", and it cannot end one session without ending them
/// all. So the sessions are recorded, and the ticket carries the row's id.
///
/// What that costs is one lookup per request. It costs nothing extra here, because the cookie handler
/// already reads this account on every request to check for a ban, a suspension and that watermark -
/// this rides along in the same query.
///
/// What it stores is an address and a browser string, which is personal data and is why these rows are
/// pruned rather than kept: see the sweep. Nothing here is required for the game to run - a row that
/// has aged out simply stops being listed.
/// </summary>
public sealed class PlayerSession
{
    /// <summary>Stamped into the cookie as a claim, which is the only way back to this row.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AccountId { get; set; }
    public PlayerAccount Account { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Moved at most once every few minutes rather than on every request. The game polls every five
    /// seconds, so an honest last-seen would be a write per session per five seconds and would make
    /// this table the busiest thing in the database to answer a question nobody asks that often.
    /// </summary>
    public DateTime LastSeenAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Set rather than deleted, so a revoked session stays visible for a while on the page that
    /// revoked it. The sweep is what eventually removes it.
    /// </summary>
    public DateTime? RevokedAtUtc { get; set; }

    /// <summary>
    /// As the app saw it, which behind the proxy means the real caller - the forwarded headers are
    /// trusted in production, which is the same switch that makes the rate limiter count people rather
    /// than counting Caddy. 45 characters holds an IPv6 address with room to spare.
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Truncated, because a user agent is attacker-controlled and unbounded, and this one is shown back
    /// to the player on a page. Stored raw and read as a name in the client, never as markup.
    /// </summary>
    public string? UserAgent { get; set; }

    public bool IsActive(DateTime nowUtc) => RevokedAtUtc is null;
}
