namespace StreetEmpire.Api.Models;

/// <summary>
/// An immutable record of one administrative action. Deliberately holds no foreign keys and copies the
/// actor and target names in full: an audit trail has to survive whatever happens to the rows it
/// describes, and has to stay readable when the same admin acts on many players.
/// </summary>
public sealed class AdminAuditLog
{
    public long Id { get; set; }

    public Guid ActorAccountId { get; set; }
    public string ActorUsername { get; set; } = string.Empty;

    /// <summary>What was done, e.g. Adjust, Ban, Unban, Suspend, PromoteAdmin, Announce.</summary>
    public string Action { get; set; } = string.Empty;

    public Guid? TargetPlayerId { get; set; }
    public string? TargetName { get; set; }

    /// <summary>Human-readable account of the change, including before and after values.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Why the admin says they did it. Required for account actions.</summary>
    public string? Reason { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
