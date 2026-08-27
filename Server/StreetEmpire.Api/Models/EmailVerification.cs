namespace StreetEmpire.Api.Models;

/// <summary>
/// One issued verification code.
///
/// A row rather than a column on the account, because a code is an event with a life of its own: it
/// was sent to a particular address at a particular moment, it can be got wrong a few times, and it
/// stops being worth anything a quarter of an hour later. Keeping the history also means the resend
/// cooldown and the attempt count have somewhere to live that is not the account row.
/// </summary>
public sealed class EmailVerification
{
    public int Id { get; set; }
    public Guid AccountId { get; set; }
    public PlayerAccount? Account { get; set; }

    /// <summary>
    /// The address this code was actually sent to.
    ///
    /// Recorded rather than read back off the account, because the two can disagree: a player can
    /// change their address while a code is in flight, and a code that proves control of the old one
    /// must not be allowed to mark the new one verified.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// The code, sealed by the data protection key ring rather than stored as it was sent.
    ///
    /// Six digits is a million possibilities, which any hash gives up in seconds to somebody holding
    /// the table. Sealing it means a database read alone is not enough - the key ring lives outside
    /// the database - and it costs nothing, because the code is only ever compared against one row.
    /// </summary>
    public string SealedCode { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Short on purpose. Six digits held open for a day is a day to guess a million numbers in.</summary>
    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>Set the moment it is spent, so a code that worked once can never work twice.</summary>
    public DateTime? ConsumedAtUtc { get; set; }

    /// <summary>
    /// Wrong guesses. The short window is what makes six digits safe against a patient attacker; this
    /// is what makes it safe against a fast one.
    /// </summary>
    public int Attempts { get; set; }

    /// <summary>Whether this code is still worth checking a guess against.</summary>
    public bool IsLive(DateTime nowUtc, int maxAttempts)
        => ConsumedAtUtc is null && ExpiresAtUtc > nowUtc && Attempts < maxAttempts;
}
