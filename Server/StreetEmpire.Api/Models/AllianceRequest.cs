namespace StreetEmpire.Api.Models;

/// <summary>
/// Somebody asking to join a crew, or a crew asking somebody to.
///
/// One table for both directions rather than two, because they are the same row read from opposite
/// ends: a name, a crew, and whoever has to say yes. Splitting them would mean two sets of rules for
/// expiry, duplicates and cleanup, which would drift apart the first time either was changed.
///
/// The row is the whole of the state. There is no "pending" flag to get stuck: a request exists until
/// it is accepted, refused, or withdrawn, and then it is gone.
/// </summary>
public sealed class AllianceRequest
{
    public long Id { get; set; }

    public long AllianceId { get; set; }
    public Alliance Alliance { get; set; } = null!;

    public Guid PlayerId { get; set; }
    public Player Player { get; set; } = null!;

    /// <summary>
    /// Which way round it goes. An invitation is answered by the player; an application is answered by
    /// anybody in the crew who is allowed to open the door.
    /// </summary>
    public AllianceRequestKind Kind { get; set; }

    /// <summary>
    /// Who sent it, for an invitation. Null on an application, where the player is the sender and is
    /// already named above.
    /// </summary>
    public Guid? SentById { get; set; }

    /// <summary>What the sender wanted to say about it.</summary>
    public string? Note { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public enum AllianceRequestKind
{
    /// <summary>The crew asked the player. The player answers.</summary>
    Invitation = 0,

    /// <summary>The player asked the crew. The crew answers.</summary>
    Application = 1
}
