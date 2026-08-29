namespace StreetEmpire.Api.Models;

/// <summary>
/// One single-use way back into an account, for somebody who has lost both the others.
///
/// A recovery code is a password. It is stored the way one is - hashed, never in a form this server can
/// read back - and shown exactly once, at the moment it is made. Losing the sheet means making a new
/// set, which is the same trade every service that does this makes, and the alternative is a column
/// that hands an attacker with database access a way into every account in the game.
///
/// Deliberately not counted as a way *back* in by <see cref="PlayerAccount.HasAnotherWayBackIn"/>.
/// Holding codes does not let a player remove their address or disconnect their Discord: codes are a
/// spare set of keys rather than a replacement for the door, and the whole point of the two-door rule is
/// that the second one is something the player cannot mislay in a drawer. Being additive is what makes
/// them safe to add at all.
/// </summary>
public sealed class RecoveryCode
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AccountId { get; set; }
    public PlayerAccount Account { get; set; } = null!;

    /// <summary>
    /// Hashed with the same hasher the passwords use, so a code gets whatever that is upgraded to next
    /// without a second decision being made about it here.
    /// </summary>
    public string CodeHash { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Set rather than deleted, so the page can say how many are left and how many have been spent
    /// without the two being the same number by construction.
    /// </summary>
    public DateTime? UsedAtUtc { get; set; }
}
