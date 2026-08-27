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
            await db.SaveChangesAsync(ct);

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
            current.DiscordLinkedAtUtc = null;
            await db.SaveChangesAsync(ct);
            await notices.TellAccountAsync(current, AccountChange.DiscordDisconnected, wasConnectedTo, ct);
            return Results.Ok(await DescribeAsync(current, discord, verification, ct));
        });
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
            account.DiscordLinkedAtUtc,
            discord.Options.IsConfigured,
            account.CreatedAtUtc);
    }
}
