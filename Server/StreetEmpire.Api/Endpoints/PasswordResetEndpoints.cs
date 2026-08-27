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
/// Getting back into an account whose password is gone.
///
/// Everything else on the account page assumes you are already in. This is the one flow that has to
/// work for somebody who is not, which changes what it is allowed to say: it is unauthenticated, so
/// every answer it gives is an answer to a stranger.
///
/// The rule that shapes all of it is that it must never say whether an account exists. Starting a reset
/// answers exactly the same way for a real username, a real address, a typo and a fishing expedition -
/// otherwise the form becomes a way to test whether somebody plays this game, and then a way to test
/// which of a leaked address list does.
///
/// A reset needs a confirmed address, which is what that confirmation was always for. An unconfirmed
/// one is refused - the same silent way as everything else here - because sending a reset code to an
/// address nobody proved would hand the account to whoever typed the address in.
/// </summary>
internal static class PasswordResetEndpoints
{
    /// <summary>
    /// One sentence for every outcome, including the ones that did nothing at all. It has to be true in
    /// all of them, so it promises no more than "if there was anything to send to, it has gone".
    /// </summary>
    private const string SameAnswerForEverybody =
        "If that account exists and has a confirmed email address, a code is on its way to it.";

    internal static void MapPasswordResetEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/reset/start", async (
            StartPasswordResetRequest request,
            GameDbContext db,
            EmailVerificationService verification,
            ILoggerFactory logs,
            CancellationToken ct) =>
        {
            var account = await FindAsync(db, request.Identifier, ct);

            // Bots are accounts too, as far as the schema is concerned, and none of them has a mailbox.
            if (account is not null && !account.IsBot && !account.IsLockedOut(DateTime.UtcNow))
            {
                var outcome = await verification.SendAsync(account, VerificationPurpose.ResetPassword, ct);

                // Told to the log and never to the caller. An operator has to be able to tell "nobody
                // asked" from "somebody asked and the mail did not go", and the sentence below cannot
                // say which without answering the question this endpoint exists not to answer.
                if (outcome is not (SendCodeResult.Sent or SendCodeResult.TooSoon or SendCodeResult.TooMany))
                    logs.CreateLogger("StreetEmpire.PasswordReset").LogWarning(
                        "A reset was asked for on account {Account} and no code went out: {Outcome}.",
                        account.Id, outcome);
            }

            return Results.Ok(new { message = SameAnswerForEverybody });
        }).RequireRateLimiting("sign-in");

        app.MapPost("/api/auth/reset/confirm", async (
            ConfirmPasswordResetRequest request,
            GameDbContext db,
            IPasswordHasher<PlayerAccount> passwordHasher,
            EmailVerificationService verification,
            AccountNotices notices,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length < 8)
                return Results.BadRequest(new { error = "Password must be at least 8 characters." });

            var account = await FindAsync(db, request.Identifier, ct);

            // A missing account and a wrong code answer identically, and both cost an attempt against
            // the sign-in limiter. Anything else would turn this endpoint into the enumeration oracle
            // that /start refuses to be.
            if (account is null || account.IsBot || account.IsLockedOut(DateTime.UtcNow))
                return Results.BadRequest(new { error = "That code is not right, or it has run out." });

            var outcome = await verification.ConfirmAsync(account, VerificationPurpose.ResetPassword, request.Code, ct);
            if (outcome != ConfirmCodeResult.Verified)
                return Results.BadRequest(new { error = "That code is not right, or it has run out." });

            // The three things a reset is, done together in one place so they cannot come apart. The
            // sessions matter most: whoever took the account is signed in right now, and a new password
            // that left them there would have changed nothing.
            var now = AuthEndpoints.ToSessionMoment(DateTime.UtcNow);
            account.PasswordHash = passwordHasher.HashPassword(account, request.NewPassword);
            account.SessionsValidAfterUtc = now;
            await db.SaveChangesAsync(ct);
            await notices.TellAccountAsync(account, AccountChange.PasswordReset, null, ct);

            // Signed in on the way out. The code already proved control of the mailbox, and the password
            // is one they just chose, so there is nothing left for a second sign-in to establish.
            await AuthEndpoints.SignInAsync(http, account, now);
            return Results.Ok(new AuthResponse(account.Player!.Id, account.Player.Name, account.Username));
        }).RequireRateLimiting("sign-in");
    }

    /// <summary>
    /// Finds an account by whichever kind of name was typed, using the same @ rule the login box does.
    ///
    /// Only a confirmed address matches. An unconfirmed one is not a name this game answers to, and
    /// letting it find an account here would be letting somebody who typed a stranger's address ask for
    /// a code to be sent to it.
    /// </summary>
    private static async Task<PlayerAccount?> FindAsync(GameDbContext db, string? identifier, CancellationToken ct)
    {
        var typed = identifier?.Trim() ?? string.Empty;
        if (typed.Length == 0) return null;

        var email = AccountSetup.NormalizeEmail(typed);
        return email is not null && AccountSetup.LooksLikeAnAttemptAtEmail(email)
            ? await db.Accounts.Include(x => x.Player).SingleOrDefaultAsync(x => x.Email == email && x.EmailVerified, ct)
            : await db.Accounts.Include(x => x.Player).SingleOrDefaultAsync(x => x.Username == typed, ct);
    }
}
