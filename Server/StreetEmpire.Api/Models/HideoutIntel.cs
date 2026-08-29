namespace StreetEmpire.Api.Models;

/// <summary>
/// What one player found out about another's house, and when.
///
/// Before this, every number on a rival's card was simply true and free: their guns, their morale, what
/// was in the safe. A raid was arithmetic anybody could do from the target list without spending
/// anything, which made the intelligence centre a building that improved mule runs and nothing else.
///
/// One row per pair, overwritten each time. There is no history worth keeping - what somebody had a
/// fortnight ago is not intelligence, it is a rumour, and the whole point is that it goes stale.
/// </summary>
public sealed class HideoutIntel
{
    public long Id { get; set; }

    /// <summary>Who looked.</summary>
    public Guid ViewerId { get; set; }
    public Player Viewer { get; set; } = null!;

    /// <summary>Who was looked at.</summary>
    public Guid SubjectId { get; set; }
    public Player Subject { get; set; } = null!;

    public DateTime GatheredAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The intelligence centre level at the moment of looking, not now.
    ///
    /// Stamped rather than read back live so that upgrading the building does not retroactively sharpen
    /// a scout somebody ran last week. You learn what your people could learn when you sent them.
    /// </summary>
    public int Level { get; set; }
}
