namespace StreetEmpire.Api.Models;

/// <summary>
/// One invite into the beta.
///
/// Unlike a recovery code, this is deliberately stored in the clear. A recovery code is a credential
/// to an existing account, so the server must never be able to show it again. A beta key is the thing
/// a player is meant to hand to somebody else next week; being able to read and copy it later is the
/// feature, not an accident.
/// </summary>
public sealed class BetaKey
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Uppercase and stripped of dashes/spaces. The display format is rebuilt from this.</summary>
    public string Code { get; set; } = string.Empty;

    public Guid? IssuedToAccountId { get; set; }
    public PlayerAccount? IssuedToAccount { get; set; }

    public string? Label { get; set; }

    public int MaxUses { get; set; } = 1;
    public int Uses { get; set; }

    public Guid? RedeemedByAccountId { get; set; }
    public PlayerAccount? RedeemedByAccount { get; set; }
    public DateTime? RedeemedAtUtc { get; set; }

    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Explicit optimistic concurrency token. Two sign-ups can read the same unused key, but only one
    /// update can be written from the version they both saw, so the account insert and key spend stand
    /// or fall together.
    /// </summary>
    public int Version { get; set; }
}
