namespace StreetEmpire.Api.Contracts;

/// <param name="City">The town to set up in. Ground is contested inside a town, so this is the map
/// the player will actually be playing on. Omitted falls back to the first configured city.</param>
/// <param name="Email">
/// Required on this door, because it is the only way back into an account made through it. The other
/// door - Discord - carries its own way back in, so the address is optional there and demanded here.
/// Every account therefore has at least one identity that can be recovered, which is the whole point
/// of asking.
/// </param>
public sealed record RegisterRequest(string? Username, string? Password, string? PlayerName, string? City = null, string? Email = null);

/// <param name="Username">A username or an email address. The field kept its name because the login
/// box has always sent it, and both are looked up the same way.</param>
public sealed record LoginRequest(string? Username, string? Password);
public sealed record AuthResponse(Guid PlayerId, string PlayerName, string Username);

/// <summary>Which ways in this server can actually offer, so the login box only shows doors that open.</summary>
public sealed record AuthProvidersResponse(bool Discord);

/// <summary>
/// What the account page shows. The password is described rather than sent: whether one exists is the
/// only fact about it a client has any business knowing, and the same goes for the outstanding code.
/// </summary>
/// <param name="EmailVerified">Whether the address has been proved. Unverified, it cannot be signed in with.</param>
/// <param name="Verification">The outstanding code, if one is in flight. Null when there is nothing to type.</param>
/// <param name="EmailDelivers">
/// False when no provider is configured and mail is written to the server log instead of sent. Said out
/// loud rather than hidden, because a code sitting in a log is fine on a laptop and a quiet disaster
/// anywhere else.
/// </param>
public sealed record AccountResponse(
    string Username,
    string PlayerName,
    string? Email,
    bool EmailVerified,
    DateTime? EmailVerifiedAtUtc,
    EmailVerificationState? Verification,
    bool EmailDelivers,
    bool HasPassword,
    bool DiscordConnected,
    string? DiscordUsername,
    DateTime? DiscordLinkedAtUtc,
    bool DiscordConfigured,
    DateTime CreatedAtUtc);

/// <summary>
/// Everything about the code in flight except the code. Enough for the page to run a clock and say how
/// many guesses are left, and not one digit more.
/// </summary>
public sealed record EmailVerificationState(
    string SentTo,
    DateTime ExpiresAtUtc,
    int AttemptsRemaining,
    DateTime? ResendableAtUtc);

public sealed record ConfirmEmailRequest(string? Code);

/// <param name="Identifier">A username or a confirmed email address - whichever the player remembers.</param>
public sealed record StartPasswordResetRequest(string? Identifier);

/// <param name="Identifier">
/// Sent again rather than remembered server-side. The first leg is deliberately stateless: holding a
/// "reset in progress" for an unauthenticated caller would be a thing anybody could create for anybody.
/// </param>
public sealed record ConfirmPasswordResetRequest(string? Identifier, string? Code, string? NewPassword);

/// <param name="Email">Null or empty removes the address. </param>
/// <param name="CurrentPassword">Required when the account has a password. Changing where a sign-in
/// can come from is exactly the kind of change that should cost the current password.</param>
public sealed record ChangeEmailRequest(string? Email, string? CurrentPassword);

/// <param name="CurrentPassword">Not required by an account that has never set one - a Discord
/// sign-up has nothing to prove with, and is already proving itself with the session cookie.</param>
public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);

/// <summary>The half of a Discord sign-up the game needs and Discord cannot answer.</summary>
/// <param name="Email">
/// Optional, and asked for here rather than left to the account page, because an account made this way
/// has no password and no address - Discord is the only way in, and losing it loses the empire. This is
/// the one moment the player is already filling in a form, so it is the cheapest moment to offer them a
/// second way back. Confirmed the usual way, by a code, like any other address.
/// </param>
public sealed record CompleteDiscordSignUpRequest(string? PlayerName, string? City, string? Username, string? Email = null);

/// <summary>
/// Handed to the client when a Discord login turns out to belong to nobody yet. Carries the handle so
/// the finish-signing-up form can offer it as the name, and nothing that could be trusted as identity -
/// the identity itself stays server-side in the signed ticket cookie.
/// </summary>
public sealed record DiscordSignUpTicketResponse(string SuggestedUsername, string DiscordUsername);
