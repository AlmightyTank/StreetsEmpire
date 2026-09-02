namespace StreetEmpire.Api.Contracts;

/// <param name="City">The town to set up in. Ground is contested inside a town, so this is the map
/// the player will actually be playing on. Omitted falls back to the first configured city.</param>
/// <param name="Email">
/// Required on this door, because it is the only way back into an account made through it. The other
/// door - Discord - carries its own way back in, so the address is optional there and demanded here.
/// Every account therefore has at least one identity that can be recovered, which is the whole point
/// of asking.
/// </param>
public sealed record RegisterRequest(
    string? Username,
    string? Password,
    string? PlayerName,
    string? City = null,
    string? Email = null,
    string? BetaKey = null);

/// <param name="Username">A username or an email address. The field kept its name because the login
/// box has always sent it, and both are looked up the same way.</param>
public sealed record LoginRequest(string? Username, string? Password);
public sealed record AuthResponse(Guid PlayerId, string PlayerName, string Username);

/// <summary>Which ways in this server can actually offer, so the login box only shows doors that open.</summary>
public sealed record AuthProvidersResponse(bool Discord, bool BetaKeyRequired);

public sealed record BetaKeyCheckResponse(bool Required, bool Valid, string? Error);

public sealed record AccountInviteKeyResponse(
    Guid Id,
    string Code,
    string DisplayCode,
    string? Label,
    int MaxUses,
    int Uses,
    int UsesLeft,
    string Status,
    Guid? RedeemedByPlayerId,
    string? RedeemedByPlayerName,
    DateTime? RedeemedAtUtc,
    DateTime? ExpiresAtUtc,
    DateTime? RevokedAtUtc,
    DateTime CreatedAtUtc);

public sealed record AccountInvitesResponse(IReadOnlyList<AccountInviteKeyResponse> Keys);

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
    string? DiscordAvatarUrl,
    DateTime? DiscordLinkedAtUtc,
    DateTime? DiscordSyncedAtUtc,
    string AvatarSource,
    string? AvatarUrl,
    string? CustomAvatarUrl,
    string? ProfileTagline,
    string? ProfilePronouns,
    string? ProfileLocation,
    string ProfileAccent,
    string ProfileBanner,
    IReadOnlyList<ProfileBadgeResponse> ProfileBadges,
    /// <summary>
    /// The key they chose, held or not - the picker shows it selected either way, because a title lost
    /// this afternoon is one they may hold again tomorrow. What they currently hold is a live question
    /// with a service behind it and is asked separately, at /api/account/titles.
    /// </summary>
    string? FeaturedTitle,
    bool ShowDiscordOnProfile,
    bool ShowActivityOnProfile,
    string DirectMessagePolicy,
    bool SyncDiscordAvatar,
    bool NoticeCombat,
    bool NoticeCrew,
    bool NoticeMarket,
    bool EmailSecurityNotices,
    bool EmailCombatNotices,
    bool EmailAllianceNotices,
    bool DiscordSecurityNotices,
    bool DiscordCombatNotices,
    bool DiscordCrewNotices,
    bool DiscordMarketNotices,
    bool DiscordConfigured,
    DateTime? DiscordLinkRewardClaimedAtUtc,
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

/// <summary>
/// One signed-in browser, as the account page shows it.
/// </summary>
/// <param name="IsCurrent">
/// The one asking. Named rather than left to be worked out, because "which of these is me" is the first
/// thing anybody wants from this list and the client cannot tell from an address - two tabs on one
/// machine share it.
/// </param>
/// <param name="UserAgent">
/// Raw, as the browser sent it. Attacker-controlled and rendered as text, never as markup.
/// </param>
public sealed record SessionResponse(
    Guid Id,
    string? IpAddress,
    string? UserAgent,
    DateTime CreatedAtUtc,
    DateTime LastSeenAtUtc,
    bool IsCurrent);

/// <param name="CurrentPassword">
/// Required by an account that has one. Ending sessions is how somebody who has stolen one locks the
/// owner out, so it costs the password - which a stolen cookie does not carry.
/// </param>
public sealed record RevokeSessionsRequest(string? CurrentPassword);

/// <param name="Codes">
/// In the clear, and for the only time they ever will be. What the server keeps is a hash, so this
/// response is the codes - there is no second chance to read them and no endpoint that will say them
/// again.
/// </param>
public sealed record RecoveryCodesResponse(IReadOnlyList<string> Codes);

/// <param name="Code">One of the ten. Case and the dash do not matter; it is read the way it looks.</param>
public sealed record UseRecoveryCodeRequest(string? Identifier, string? Code, string? NewPassword);

public sealed record ChangeAvatarRequest(string? Source);
/// <param name="FeaturedTitle">A title key, or an empty string to lead with whatever the board hands you.</param>
public sealed record ChangeProfileRequest(
    string? Tagline, string? Pronouns, string? Location, string? Accent, string? Banner = null,
    string? FeaturedTitle = null);
public sealed record ChangePrivacyRequest(
    bool? ShowDiscordOnProfile,
    string? DirectMessagePolicy,
    bool? ShowActivityOnProfile = null);

/// <param name="NoticeCombat">The bell, not the inbox. The email switches below are a separate channel.</param>
public sealed record ChangeNotificationPreferencesRequest(
    bool? SyncDiscordAvatar,
    bool? EmailSecurityNotices,
    bool? EmailCombatNotices,
    bool? EmailAllianceNotices,
    bool? DiscordSecurityNotices,
    bool? DiscordCombatNotices,
    bool? DiscordCrewNotices,
    bool? DiscordMarketNotices,
    bool? NoticeCombat = null,
    bool? NoticeCrew = null,
    bool? NoticeMarket = null);

/// <summary>The half of a Discord sign-up the game needs and Discord cannot answer.</summary>
/// <param name="Email">
/// Optional, and asked for here rather than left to the account page, because an account made this way
/// has no password and no address - Discord is the only way in, and losing it loses the empire. This is
/// the one moment the player is already filling in a form, so it is the cheapest moment to offer them a
/// second way back. Confirmed the usual way, by a code, like any other address.
/// </param>
public sealed record CompleteDiscordSignUpRequest(
    string? PlayerName,
    string? City,
    string? Username,
    string? Email = null,
    string? BetaKey = null);

/// <summary>
/// Handed to the client when a Discord login turns out to belong to nobody yet. Carries the handle so
/// the finish-signing-up form can offer it as the name, and nothing that could be trusted as identity -
/// the identity itself stays server-side in the signed ticket cookie.
/// </summary>
public sealed record DiscordSignUpTicketResponse(string SuggestedUsername, string DiscordUsername);
