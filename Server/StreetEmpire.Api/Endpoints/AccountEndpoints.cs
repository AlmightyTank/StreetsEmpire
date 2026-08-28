using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;
using StreetEmpire.Api.Services;
using StreetEmpire.Api.Support;

namespace StreetEmpire.Api.Endpoints;

/// <summary>
/// The account settings page: what you sign in with, what is connected to you, and what is proved.
///
/// The rule running through all of it is that an account must always keep at least one way in. Every
/// endpoint here that can take a door away checks for another one first, because the alternative is a
/// player who removes their password on Monday, unlinks Discord on Tuesday, and owns an empire nobody
/// can ever reach again.
/// </summary>
internal static class AccountEndpoints
{
    private const long MaxCustomAvatarBytes = 1_000_000;
    private const int MaxTaglineLength = 140;
    private const int MaxPronounsLength = 64;
    private const int MaxLocationLength = 64;

    internal static void MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var account = app.MapGroup("/api/account").RequireAuthorization();

        account.MapGet("", async (
            HttpContext http,
            GameDbContext db,
            DiscordAuthService discord,
            EmailVerificationService verification,
            CancellationToken ct) =>
        {
            var current = await LoadAsync(http, db, ct);
            return current is null ? Results.Unauthorized() : Results.Ok(await DescribeAsync(current, discord, verification, ct));
        });

        // Setting, changing, and clearing an address are one endpoint because they are one decision:
        // which address, if any, this account answers to.
        account.MapPut("/email", async (
            ChangeEmailRequest request,
            HttpContext http,
            GameDbContext db,
            IPasswordHasher<PlayerAccount> passwordHasher,
            DiscordAuthService discord,
            EmailVerificationService verification,
            AccountNotices notices,
            CancellationToken ct) =>
        {
            var current = await LoadAsync(http, db, ct);
            if (current is null) return Results.Unauthorized();

            // Anyone holding a borrowed session could otherwise point the account at an address they
            // own, which is the first half of taking it. The password is the thing they would not have.
            if (current.HasPassword && !Verifies(passwordHasher, current, request.CurrentPassword))
                return Results.BadRequest(new { error = "That is not your current password." });

            var email = AccountSetup.NormalizeEmail(request.Email);
            if (email is not null && !AccountSetup.LooksLikeAnEmail(email))
                return Results.BadRequest(new { error = "That does not look like an email address." });
            if (email == current.Email)
                return Results.Ok(await DescribeAsync(current, discord, verification, ct));

            if (email is not null && await db.Accounts.AnyAsync(x => x.Email == email && x.Id != current.Id, ct))
                return Results.Conflict(new { error = "That email is already on an account." });

            // Half of the rule that stops somebody keeping an empire they cannot recover. Signing up
            // demands an address or a Discord; taking the address off a minute later would put them
            // straight back where the sign-up rule refused to let them start.
            //
            // Only removal is refused. Changing to a new address leaves one there to confirm, and a
            // code is already on its way to it.
            if (email is null && !current.HasAnotherWayBackIn(withoutEmail: true))
                return Results.BadRequest(new
                {
                    error = "Connect Discord first - this address is the only way back into your account if you forget your password."
                });

            // Captured before the change, because in a moment the account will not point here any more
            // and this is the one notice that has to reach where it used to point.
            var leaving = current is { EmailVerified: true, Email: not null } ? current.Email : null;
            var playerName = current.Player?.Name ?? current.Username;

            // Address and tick move together, always. See PlayerAccount.SetEmail.
            current.SetEmail(email);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (DatabaseErrors.DescribeUniqueViolation(ex) is { } taken)
            {
                // The check above and this line are two moments, and an address can be claimed between
                // them. The unique index is the thing that actually decides; this is how it says so.
                return Results.Conflict(new { error = taken });
            }

            // The most important notice in the whole set. Moving the address is how somebody who has
            // taken an account keeps it - the owner is cut off and never told - so the old address is
            // told at the moment it stops being the account's. The new one is not told separately,
            // because a code is already on its way there.
            if (leaving is not null)
                await notices.TellFormerAddressAsync(
                    leaving,
                    playerName,
                    email is null ? AccountChange.EmailRemoved : AccountChange.EmailChanged,
                    email,
                    ct);

            // A new address with no code in the post is a dead end: the player would have to go and
            // find the button themselves to finish what they just started.
            if (email is not null && verification.Options.SendOnSignUp)
                await verification.SendAsync(current, VerificationPurpose.ConfirmAddress, ct);

            return Results.Ok(await DescribeAsync(current, discord, verification, ct));
        }).RequireRateLimiting("sign-in");

        // ---- Proving the address --------------------------------------------------------------
        //
        // Six digits, fifteen minutes, five guesses, one code a minute. The short secret is only safe
        // because of the three numbers after it; EmailVerificationService says why that trade was made
        // rather than the long token a link would have carried.

        account.MapPost("/email/verify/send", async (
            HttpContext http,
            GameDbContext db,
            EmailVerificationService verification,
            DiscordAuthService discord,
            CancellationToken ct) =>
        {
            var current = await LoadAsync(http, db, ct);
            if (current is null) return Results.Unauthorized();

            var outcome = await verification.SendAsync(current, VerificationPurpose.ConfirmAddress, ct);
            var described = await DescribeAsync(current, discord, verification, ct);
            return outcome switch
            {
                SendCodeResult.Sent => Results.Ok(described),
                SendCodeResult.NoAddress => Results.BadRequest(new { error = "Add an email address first." }),
                SendCodeResult.AlreadyVerified => Results.BadRequest(new { error = "That address is already confirmed." }),
                SendCodeResult.TooSoon => Results.BadRequest(new { error = "A code has just gone out. Give it a minute before asking for another." }),
                SendCodeResult.TooMany => Results.BadRequest(new { error = "That address has had a lot of codes today. Try again tomorrow." }),
                // The code is real and the message is not. Said plainly rather than as a success,
                // because a player waiting for mail that will never arrive cannot work that out alone.
                SendCodeResult.NotDelivered => Results.Json(
                    new { error = "The code was made but could not be sent. Try again shortly." },
                    statusCode: StatusCodes.Status502BadGateway),
                _ => Results.BadRequest(new { error = "Could not send a code." }),
            };
        }).RequireRateLimiting("sign-in");

        account.MapPost("/email/verify", async (
            ConfirmEmailRequest request,
            HttpContext http,
            GameDbContext db,
            EmailVerificationService verification,
            DiscordAuthService discord,
            CancellationToken ct) =>
        {
            var current = await LoadAsync(http, db, ct);
            if (current is null) return Results.Unauthorized();

            var outcome = await verification.ConfirmAsync(current, VerificationPurpose.ConfirmAddress, request.Code, ct);
            var described = await DescribeAsync(current, discord, verification, ct);
            return outcome switch
            {
                ConfirmCodeResult.Verified or ConfirmCodeResult.AlreadyVerified => Results.Ok(described),
                // How many guesses are left comes back in the state beside this, so the message does
                // not have to carry a number that would then be in two places.
                ConfirmCodeResult.Wrong => Results.BadRequest(new { error = "That code is not right." }),
                ConfirmCodeResult.Expired => Results.BadRequest(new { error = "That code has run out. Ask for a new one." }),
                _ => Results.BadRequest(new { error = "There is no code waiting to be confirmed." }),
            };
        }).RequireRateLimiting("sign-in");

        // Setting a first password and changing an existing one are the same endpoint; only whether a
        // current password is demanded differs, and that is decided by whether there is one to demand.
        account.MapPut("/password", async (
            ChangePasswordRequest request,
            HttpContext http,
            GameDbContext db,
            IPasswordHasher<PlayerAccount> passwordHasher,
            DiscordAuthService discord,
            EmailVerificationService verification,
            AccountNotices notices,
            CancellationToken ct) =>
        {
            var current = await LoadAsync(http, db, ct);
            if (current is null) return Results.Unauthorized();

            if (string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length < 8)
                return Results.BadRequest(new { error = "Password must be at least 8 characters." });
            if (current.HasPassword && !Verifies(passwordHasher, current, request.CurrentPassword))
                return Results.BadRequest(new { error = "That is not your current password." });

            // Which of the two it was has to be read before the hash is written over.
            var isFirstPassword = !current.HasPassword;
            current.PasswordHash = passwordHasher.HashPassword(current, request.NewPassword);
            await EndOtherSessionsAsync(http, db, current, ct);
            await notices.TellAccountAsync(
                current,
                isFirstPassword ? AccountChange.PasswordSet : AccountChange.PasswordChanged,
                null,
                ct);

            return Results.Ok(await DescribeAsync(current, discord, verification, ct));
        }).RequireRateLimiting("sign-in");

        // The same machinery as a password change, offered on its own. Somebody who left themselves
        // signed in on a machine they no longer have should not have to change their password to get
        // out of it.
        account.MapPost("/sessions/revoke", async (
            HttpContext http,
            GameDbContext db,
            DiscordAuthService discord,
            EmailVerificationService verification,
            AccountNotices notices,
            CancellationToken ct) =>
        {
            var current = await LoadAsync(http, db, ct);
            if (current is null) return Results.Unauthorized();

            await EndOtherSessionsAsync(http, db, current, ct);
            await notices.TellAccountAsync(current, AccountChange.SessionsSignedOut, null, ct);
            return Results.Ok(await DescribeAsync(current, discord, verification, ct));
        });

        account.MapPut("/avatar", async (
            ChangeAvatarRequest request,
            HttpContext http,
            GameDbContext db,
            DiscordAuthService discord,
            EmailVerificationService verification,
            CancellationToken ct) =>
        {
            var current = await LoadAsync(http, db, ct);
            if (current is null) return Results.Unauthorized();

            var source = request.Source?.Trim().ToLowerInvariant() switch
            {
                null or "" or "none" => AccountAvatarSource.None,
                "discord" => AccountAvatarSource.Discord,
                "custom" => AccountAvatarSource.Custom,
                _ => (AccountAvatarSource?)null,
            };
            if (source is null)
                return Results.BadRequest(new { error = "Pick one of: none, discord, custom." });
            if (source == AccountAvatarSource.Discord && current.DiscordUserId is null)
                return Results.BadRequest(new { error = "Connect Discord before using its avatar." });
            if (source == AccountAvatarSource.Discord && DiscordAuthService.AvatarUrl(current.DiscordUserId, current.DiscordAvatarHash) is null)
                return Results.BadRequest(new { error = "That Discord account does not have a custom avatar to use." });
            if (source == AccountAvatarSource.Custom && current.CustomAvatar is null)
                return Results.BadRequest(new { error = "Upload a custom avatar before using it." });

            current.AvatarSource = source.Value;
            await db.SaveChangesAsync(ct);
            return Results.Ok(await DescribeAsync(current, discord, verification, ct));
        });

        account.MapPut("/profile", async (
            ChangeProfileRequest request,
            HttpContext http,
            GameDbContext db,
            DiscordAuthService discord,
            EmailVerificationService verification,
            CancellationToken ct) =>
        {
            var current = await LoadAsync(http, db, ct);
            if (current is null) return Results.Unauthorized();

            var tagline = NormalizeProfileText(request.Tagline);
            var pronouns = NormalizeProfileText(request.Pronouns);
            var location = NormalizeProfileText(request.Location);
            var accent = ParseAccent(request.Accent);
            var banner = ParseBanner(request.Banner);
            if (tagline?.Length > MaxTaglineLength)
                return Results.BadRequest(new { error = $"Tagline must be {MaxTaglineLength} characters or less." });
            if (pronouns?.Length > MaxPronounsLength)
                return Results.BadRequest(new { error = $"Pronouns must be {MaxPronounsLength} characters or less." });
            if (location?.Length > MaxLocationLength)
                return Results.BadRequest(new { error = $"Profile location must be {MaxLocationLength} characters or less." });
            if (!string.IsNullOrWhiteSpace(request.Accent) && accent is null)
                return Results.BadRequest(new { error = "Pick one of: gold, teal, rose, steel." });
            if (!string.IsNullOrWhiteSpace(request.Banner) && banner is null)
                return Results.BadRequest(new { error = "Pick one of: none, neon, smoke, chrome, rust, velvet." });

            current.ProfileTagline = tagline;
            current.ProfilePronouns = pronouns;
            current.ProfileLocation = location;
            if (accent is { } selectedAccent)
                current.ProfileAccent = selectedAccent;
            if (banner is { } selectedBanner)
                current.ProfileBanner = selectedBanner;
            await db.SaveChangesAsync(ct);
            return Results.Ok(await DescribeAsync(current, discord, verification, ct));
        });

        account.MapPut("/privacy", async (
            ChangePrivacyRequest request,
            HttpContext http,
            GameDbContext db,
            DiscordAuthService discord,
            EmailVerificationService verification,
            CancellationToken ct) =>
        {
            var current = await LoadAsync(http, db, ct);
            if (current is null) return Results.Unauthorized();

            if (request.ShowDiscordOnProfile is { } showDiscord)
                current.ShowDiscordOnProfile = showDiscord && current.DiscordUserId is not null;
            if (request.ShowActivityOnProfile is { } showActivity)
                current.ShowActivityOnProfile = showActivity;

            var policy = request.DirectMessagePolicy?.Trim().ToLowerInvariant() switch
            {
                null or "" => (DirectMessagePolicy?)null,
                "everyone" => DirectMessagePolicy.Everyone,
                "alliance" => DirectMessagePolicy.Alliance,
                "allianceandpacts" or "alliance-and-pacts" or "pacts" => DirectMessagePolicy.AllianceAndPacts,
                "nobody" => DirectMessagePolicy.Nobody,
                _ => (DirectMessagePolicy?)null,
            };
            if (!string.IsNullOrWhiteSpace(request.DirectMessagePolicy) && policy is null)
                return Results.BadRequest(new { error = "Pick one of: everyone, alliance, pacts, nobody." });
            if (policy is { } selected)
                current.DirectMessagePolicy = selected;

            await db.SaveChangesAsync(ct);
            return Results.Ok(await DescribeAsync(current, discord, verification, ct));
        });

        account.MapPut("/notifications", async (
            ChangeNotificationPreferencesRequest request,
            HttpContext http,
            GameDbContext db,
            DiscordAuthService discord,
            EmailVerificationService verification,
            CancellationToken ct) =>
        {
            var current = await LoadAsync(http, db, ct);
            if (current is null) return Results.Unauthorized();

            if (request.SyncDiscordAvatar is { } syncDiscordAvatar)
            {
                current.SyncDiscordAvatar = syncDiscordAvatar;
                if (syncDiscordAvatar
                    && current.DiscordUserId is not null
                    && DiscordAuthService.AvatarUrl(current.DiscordUserId, current.DiscordAvatarHash) is not null)
                    current.AvatarSource = AccountAvatarSource.Discord;
            }
            if (request.EmailSecurityNotices is { } security)
                current.EmailSecurityNotices = security;
            if (request.EmailCombatNotices is { } combat)
                current.EmailCombatNotices = combat;
            if (request.EmailAllianceNotices is { } alliance)
                current.EmailAllianceNotices = alliance;

            if (request.NoticeCombat is { } noticeCombat)
                current.NoticeCombat = noticeCombat;
            if (request.NoticeCrew is { } noticeCrew)
                current.NoticeCrew = noticeCrew;
            if (request.NoticeMarket is { } noticeMarket)
                current.NoticeMarket = noticeMarket;

            await db.SaveChangesAsync(ct);
            return Results.Ok(await DescribeAsync(current, discord, verification, ct));
        });

        account.MapPost("/avatar/custom", async (
            HttpContext http,
            GameDbContext db,
            DiscordAuthService discord,
            EmailVerificationService verification,
            CancellationToken ct) =>
        {
            var current = await LoadAsync(http, db, ct);
            if (current is null) return Results.Unauthorized();
            if (!http.Request.HasFormContentType)
                return Results.BadRequest(new { error = "Upload an image file." });

            var form = await http.Request.ReadFormAsync(ct);
            var file = form.Files.GetFile("avatar") ?? form.Files.FirstOrDefault();
            if (file is null || file.Length == 0)
                return Results.BadRequest(new { error = "Choose an image file first." });
            if (file.Length > MaxCustomAvatarBytes)
                return Results.BadRequest(new { error = "Avatar image must be 1 MB or smaller." });

            await using var stream = file.OpenReadStream();
            using var bytes = new MemoryStream();
            await stream.CopyToAsync(bytes, ct);
            var data = bytes.ToArray();
            var contentType = ImageContentType(data);
            if (contentType is null)
                return Results.BadRequest(new { error = "Avatar must be a PNG, JPG, GIF, or WebP image." });

            current.CustomAvatar = data;
            current.CustomAvatarContentType = contentType;
            current.CustomAvatarUpdatedAtUtc = DateTime.UtcNow;
            current.AvatarSource = AccountAvatarSource.Custom;
            await db.SaveChangesAsync(ct);
            return Results.Ok(await DescribeAsync(current, discord, verification, ct));
        }).DisableAntiforgery();

        account.MapDelete("/avatar/custom", async (
            HttpContext http,
            GameDbContext db,
            DiscordAuthService discord,
            EmailVerificationService verification,
            CancellationToken ct) =>
        {
            var current = await LoadAsync(http, db, ct);
            if (current is null) return Results.Unauthorized();

            current.CustomAvatar = null;
            current.CustomAvatarContentType = null;
            current.CustomAvatarUpdatedAtUtc = null;
            if (current.AvatarSource == AccountAvatarSource.Custom)
                current.AvatarSource = AccountAvatarSource.None;
            await db.SaveChangesAsync(ct);
            return Results.Ok(await DescribeAsync(current, discord, verification, ct));
        });

        account.MapDelete("/discord", async (
            HttpContext http,
            GameDbContext db,
            DiscordAuthService discord,
            EmailVerificationService verification,
            AccountNotices notices,
            CancellationToken ct) =>
        {
            var current = await LoadAsync(http, db, ct);
            if (current is null) return Results.Unauthorized();
            if (current.DiscordUserId is null)
                return Results.BadRequest(new { error = "There is no Discord account connected." });
            if (!current.HasAnotherWayIn(withoutDiscord: true))
                return Results.BadRequest(new { error = "Set a password first - Discord is the only way into this account." });

            // The other half, and the one that makes the pair a rule rather than a speed bump. Without
            // it the hole simply moves: connect Discord, drop the address because Discord now covers
            // it, then drop Discord because the password now covers it - and the account ends up with
            // a password and no way to recover it, one step at a time, each step allowed.
            if (!current.HasAnotherWayBackIn(withoutDiscord: true))
                return Results.BadRequest(new
                {
                    error = "Confirm an email address first - Discord is the only way back into this account if you forget your password."
                });

            // Read before it is cleared, so the notice can name which account was taken off.
            var wasConnectedTo = current.DiscordUsername;
            current.DiscordUserId = null;
            current.DiscordUsername = null;
            current.DiscordAvatarHash = null;
            current.ShowDiscordOnProfile = false;
            if (current.AvatarSource == AccountAvatarSource.Discord)
                current.AvatarSource = AccountAvatarSource.None;
            current.DiscordLinkedAtUtc = null;
            await db.SaveChangesAsync(ct);
            await notices.TellAccountAsync(current, AccountChange.DiscordDisconnected, wasConnectedTo, ct);
            return Results.Ok(await DescribeAsync(current, discord, verification, ct));
        });

        app.MapGet("/api/game/players/{playerId:guid}/avatar", async (
            Guid playerId,
            HttpContext http,
            GameDbContext db,
            CancellationToken ct) =>
        {
            var subject = await db.Players
                .Include(x => x.Account)
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == playerId, ct);
            if (subject?.Account is not { AvatarSource: AccountAvatarSource.Custom, CustomAvatar: not null } accountWithAvatar
                || string.IsNullOrWhiteSpace(accountWithAvatar.CustomAvatarContentType))
                return Results.NotFound();

            http.Response.Headers.CacheControl = "private, max-age=86400";
            return Results.File(accountWithAvatar.CustomAvatar, accountWithAvatar.CustomAvatarContentType);
        }).RequireAuthorization();
    }

    /// <summary>
    /// Rejects every session but this one.
    ///
    /// The watermark and the re-issued cookie are both floored to the second, because they are compared
    /// through a cookie ticket that only remembers whole seconds - an unrounded watermark signs out the
    /// very session that asked for this.
    /// </summary>
    private static async Task EndOtherSessionsAsync(HttpContext http, GameDbContext db, PlayerAccount account, CancellationToken ct)
    {
        var now = AuthEndpoints.ToSessionMoment(DateTime.UtcNow);
        account.SessionsValidAfterUtc = now;
        await db.SaveChangesAsync(ct);
        await AuthEndpoints.SignInAsync(http, account, now);
    }

    private static async Task<PlayerAccount?> LoadAsync(HttpContext http, GameDbContext db, CancellationToken ct)
        => Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? await db.Accounts.Include(x => x.Player).SingleOrDefaultAsync(x => x.Id == id, ct)
            : null;

    private static bool Verifies(IPasswordHasher<PlayerAccount> hasher, PlayerAccount account, string? password)
        => !string.IsNullOrEmpty(password)
            && hasher.VerifyHashedPassword(account, account.PasswordHash, password) != PasswordVerificationResult.Failed;

    private static string? NormalizeProfileText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var parts = value.Trim().Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', parts);
    }

    private static ProfileAccent? ParseAccent(string? value)
        => value?.Trim().ToLowerInvariant() switch
        {
            null or "" => null,
            "gold" => ProfileAccent.Gold,
            "teal" => ProfileAccent.Teal,
            "rose" => ProfileAccent.Rose,
            "steel" => ProfileAccent.Steel,
            _ => null,
        };

    private static ProfileBanner? ParseBanner(string? value)
        => value?.Trim().ToLowerInvariant() switch
        {
            null or "" => null,
            "none" => ProfileBanner.None,
            "neon" => ProfileBanner.Neon,
            "smoke" => ProfileBanner.Smoke,
            "chrome" => ProfileBanner.Chrome,
            "rust" => ProfileBanner.Rust,
            "velvet" => ProfileBanner.Velvet,
            _ => null,
        };

    private static string? ImageContentType(byte[] bytes)
    {
        if (bytes.Length >= 8
            && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47
            && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
            return "image/png";

        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return "image/jpeg";

        if (StartsWithAscii(bytes, "GIF87a") || StartsWithAscii(bytes, "GIF89a"))
            return "image/gif";

        if (bytes.Length >= 12 && StartsWithAscii(bytes, "RIFF", 0) && StartsWithAscii(bytes, "WEBP", 8))
            return "image/webp";

        return null;
    }

    private static bool StartsWithAscii(byte[] bytes, string value, int offset = 0)
    {
        if (bytes.Length < offset + value.Length) return false;
        for (var i = 0; i < value.Length; i++)
        {
            if (bytes[offset + i] != (byte)value[i]) return false;
        }
        return true;
    }

    private static async Task<AccountResponse> DescribeAsync(
        PlayerAccount account,
        DiscordAuthService discord,
        EmailVerificationService verification,
        CancellationToken ct)
    {
        var pending = await verification.PendingAsync(account.Id, VerificationPurpose.ConfirmAddress, ct);
        var state = pending is null
            ? null
            : new EmailVerificationState(
                pending.Email,
                pending.ExpiresAtUtc,
                Math.Max(0, verification.Options.MaxAttempts - pending.Attempts),
                await verification.ResendableAtAsync(account.Id, VerificationPurpose.ConfirmAddress, account.Email, ct));

        var discordAvatarUrl = DiscordAuthService.AvatarUrl(account.DiscordUserId, account.DiscordAvatarHash);
        var avatarUrl = StreetEmpire.Api.Mapping.ResponseMappers.AvatarUrl(account);
        var customAvatarUrl = account.Player is not null && account.CustomAvatarUpdatedAtUtc is { } updated
            ? $"/api/game/players/{account.Player.Id}/avatar?v={new DateTimeOffset(updated).ToUnixTimeMilliseconds()}"
            : null;

        return new AccountResponse(
            account.Username,
            account.Player?.Name ?? account.Username,
            account.Email,
            account.EmailVerified,
            account.EmailVerifiedAtUtc,
            state,
            verification.Delivers,
            account.HasPassword,
            account.DiscordUserId is not null,
            account.DiscordUsername,
            discordAvatarUrl,
            account.DiscordLinkedAtUtc,
            account.DiscordSyncedAtUtc,
            account.AvatarSource.ToString(),
            avatarUrl,
            customAvatarUrl,
            account.ProfileTagline,
            account.ProfilePronouns,
            account.ProfileLocation,
            account.ProfileAccent.ToString(),
            account.ProfileBanner.ToString(),
            account.ShowDiscordOnProfile,
            account.ShowActivityOnProfile,
            account.DirectMessagePolicy.ToString(),
            account.SyncDiscordAvatar,
            account.NoticeCombat,
            account.NoticeCrew,
            account.NoticeMarket,
            account.EmailSecurityNotices,
            account.EmailCombatNotices,
            account.EmailAllianceNotices,
            discord.Options.IsConfigured,
            account.CreatedAtUtc);
    }
}
