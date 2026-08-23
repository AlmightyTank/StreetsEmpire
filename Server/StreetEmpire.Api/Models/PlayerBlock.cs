namespace StreetEmpire.Api.Models;

/// <summary>
/// One person refusing to hear from another.
///
/// Deliberately narrow: this silences somebody, it does not shield you from them. Blocking a player
/// does not stop them raiding your house, jacking your cars or poisoning your crew, and it never
/// will - the moment a social setting can be used to duck a fight, it stops being a way to deal with
/// somebody unpleasant and becomes a move. The game is the game; this is only about talking.
///
/// It cuts both ways. Somebody you have blocked cannot write to you and does not appear in your
/// rooms, and you do not appear in theirs either, because a block that let the blocker keep reading
/// would be surveillance rather than a refusal.
/// </summary>
public sealed class PlayerBlock
{
    public long Id { get; set; }

    /// <summary>The one who does not want to hear it.</summary>
    public Guid BlockerId { get; set; }
    public Player? Blocker { get; set; }

    /// <summary>The one they do not want to hear from.</summary>
    public Guid BlockedId { get; set; }
    public Player? Blocked { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
