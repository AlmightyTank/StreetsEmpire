namespace StreetEmpire.Api.Models;

public sealed class PlayerAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public bool IsBot { get; set; }

    /// <summary>
    /// A paused rival is skipped by the automatic loop and by manual runs, but is otherwise a normal
    /// player: still rankable, still attackable, still holding whatever it had. Useful for freezing one
    /// rival as a fixed target while the rest of the world keeps moving.
    /// </summary>
    public bool IsBotPaused { get; set; }

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
