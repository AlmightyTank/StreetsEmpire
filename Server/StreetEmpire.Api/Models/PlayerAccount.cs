namespace StreetEmpire.Api.Models;

public sealed class PlayerAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public bool IsBot { get; set; }

    /// <summary>
    /// A second name to sign in under, stored folded to lower case so that the unique index is what
    /// decides whether two people have the same address rather than how they happened to type it.
    /// Optional, and null rather than empty when absent: Postgres lets any number of rows hold null in
    /// a unique index, which is what allows every account without an address to coexist.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Whether the person holding this account has proved they can read mail at that address.
    ///
    /// It is not decoration: an unverified address cannot be signed in with. The unique index still
    /// holds the address against every other account the moment it is typed, which stops two people
    /// claiming one - and means somebody who types an address they do not own has blocked it without
    /// gaining anything, because the door it would have opened stays shut until they prove it.
    ///
    /// Reset to false by any change of address. A tick that survived being pointed somewhere new would
    /// be a tick against an address nobody ever checked.
    /// </summary>
    public bool EmailVerified { get; set; }

    public DateTime? EmailVerifiedAtUtc { get; set; }

    /// <summary>Codes issued to this account, newest last. Only ever read one at a time.</summary>
    public ICollection<EmailVerification> EmailVerifications { get; set; } = [];

    /// <summary>
    /// Points the account at a new address, or at none, and takes the tick off. One method rather than
    /// two assignments at four call sites, because the pair coming apart is the whole failure: an
    /// address that changed while the tick stayed put is a verified address nobody verified.
    /// </summary>
    public void SetEmail(string? normalizedEmail)
    {
        Email = normalizedEmail;
        EmailVerified = false;
        EmailVerifiedAtUtc = null;
    }

    /// <summary>
    /// Discord's snowflake for the linked user. This, not the handle, is the identity: a handle can be
    /// changed by its owner at any time and would hand somebody else's account over on the next login.
    /// </summary>
    public string? DiscordUserId { get; set; }

    /// <summary>What to show on the settings page so a player can tell which Discord this is. Display only.</summary>
    public string? DiscordUsername { get; set; }

    public DateTime? DiscordLinkedAtUtc { get; set; }

    /// <summary>
    /// An account made through Discord has never chosen a password, and one that has unlinked Discord
    /// may have nothing else left. Both endpoints that can take a way in away check this first, so an
    /// account can never end up with no door.
    /// </summary>
    public bool HasPassword => !string.IsNullOrEmpty(PasswordHash);

    /// <summary>Whether this account can still be signed into if the named way in were removed.</summary>
    public bool HasAnotherWayIn(bool withoutPassword = false, bool withoutDiscord = false)
        => (!withoutPassword && HasPassword) || (!withoutDiscord && DiscordUserId is not null);

    /// <summary>
    /// Whether this account could still be got back into if the named thing were removed.
    ///
    /// A different question from <see cref="HasAnotherWayIn"/>, and the difference is the whole point.
    /// A password is a way in and is not a way back in: forget it and there is nothing left to prove
    /// the account was ever yours. Only two things answer this one - a confirmed address, which can be
    /// sent a reset code, and a Discord account, which signs in without needing the password at all.
    ///
    /// Unconfirmed does not count. An address nobody has proved cannot be sent a reset, so treating it
    /// as a way back would be counting a door that does not open.
    /// </summary>
    public bool HasAnotherWayBackIn(bool withoutEmail = false, bool withoutDiscord = false)
        => (!withoutEmail && EmailVerified) || (!withoutDiscord && DiscordUserId is not null);

    /// <summary>
    /// A paused rival is skipped by the automatic loop and by manual runs, but is otherwise a normal
    /// player: still rankable, still attackable, still holding whatever it had. Useful for freezing one
    /// rival as a fixed target while the rest of the world keeps moving.
    /// </summary>
    public bool IsBotPaused { get; set; }

    /// <summary>
    /// When this rival's current sitting runs out. Null between sessions, which is what "logged off"
    /// means here. Kept on the account rather than the player because it is a fact about the thing
    /// driving the player, not about the empire.
    /// </summary>
    public DateTime? BotSessionEndsAtUtc { get; set; }

    /// <summary>When the rival next sits down. Null means it may start as soon as it is looked at.</summary>
    public DateTime? BotNextSessionAtUtc { get; set; }

    /// <summary>What is left of this sitting. Counts down as the rival acts, and ends the session at zero.</summary>
    public int BotSessionActionsLeft { get; set; }

    /// <summary>Whether this rival is at the screen right now.</summary>
    public bool IsBotInSession(DateTime nowUtc)
        => BotSessionEndsAtUtc is { } ends && ends > nowUtc && BotSessionActionsLeft > 0;

    /// <summary>Blocked indefinitely until an admin lifts it.</summary>
    public bool IsBanned { get; set; }

    /// <summary>Blocked until this moment passes. Null when not suspended.</summary>
    public DateTime? SuspendedUntilUtc { get; set; }

    /// <summary>Shown to the player when they are turned away, so a ban is never silent.</summary>
    public string? EnforcementReason { get; set; }

    /// <summary>
    /// Sessions issued before this are rejected. Cookie auth cannot otherwise be revoked server-side,
    /// so this is what makes a ban or a force-logout take effect on an already signed-in player.
    /// </summary>
    public DateTime? SessionsValidAfterUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Player? Player { get; set; }

    /// <summary>Whether the account is currently barred, and why.</summary>
    public bool IsLockedOut(DateTime nowUtc)
        => IsBanned || (SuspendedUntilUtc is { } until && until > nowUtc);

    public string LockoutMessage(DateTime nowUtc)
    {
        var reason = string.IsNullOrWhiteSpace(EnforcementReason) ? "No reason recorded." : EnforcementReason;
        if (IsBanned)
            return $"This account is banned. {reason}";
        return SuspendedUntilUtc is { } until && until > nowUtc
            ? $"This account is suspended until {until:u}. {reason}"
            : string.Empty;
    }
}
