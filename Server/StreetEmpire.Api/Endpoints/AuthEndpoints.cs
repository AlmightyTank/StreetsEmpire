using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;
using StreetEmpire.Api.Services;
using StreetEmpire.Api.Support;

namespace StreetEmpire.Api.Endpoints;

/// <summary>Registration, login, logout, and the round trip through Discord.</summary>
internal static class AuthEndpoints
{
    internal static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {

        // Public: the register form needs the town list before anybody has an account.
        app.MapGet("/api/auth/cities", (IOptionsSnapshot<GameOptions> gameOptions) =>
        {
            var options = gameOptions.Value;
            options.Territory.ApplyDefaultsWhereEmpty();
            return Results.Ok(options.Territory.Cities());
        });

        // Public, and the reason the Discord button is never drawn on a server that cannot honour it.
        app.MapGet("/api/auth/providers", (DiscordAuthService discord) =>
            Results.Ok(new AuthProvidersResponse(discord.Options.IsConfigured)));

        app.MapPost("/api/auth/register", async (
            RegisterRequest request,
            GameDbContext db,
            IPasswordHasher<PlayerAccount> passwordHasher,
            IOptionsSnapshot<GameOptions> gameOptions,
            PimpRoster pimps,
            EmailVerificationService verification,
            HttpContext http,
            CancellationToken ct) =>
        {
            var username = request.Username?.Trim() ?? string.Empty;
            var playerName = request.PlayerName?.Trim() ?? string.Empty;

            if (username.Length is < 3 or > 32)
                return Results.BadRequest(new { error = "Username must be 3-32 characters." });
            if (playerName.Length is < 3 or > 32)
                return Results.BadRequest(new { error = "Player name must be 3-32 characters." });
            if (string.IsNullOrEmpty(request.Password) || request.Password.Length < 8)
                return Results.BadRequest(new { error = "Password must be at least 8 characters." });

            // Required here, and only here.
            //
            // An account made on this door has exactly one way in - the password - and one way back if
            // that goes, which is a code to a confirmed address. Without an address there is no second
            // thing, and the account is one forgotten password from being unreachable for good. The
            // other door carries its own way back in, so it asks and does not insist.
            var email = AccountSetup.NormalizeEmail(request.Email);
            if (email is null)
                return Results.BadRequest(new
                {
                    error = "An email address is needed - it is the only way back in if you forget your password. Or sign up with Discord instead."
                });
            if (!AccountSetup.LooksLikeAnEmail(email))
                return Results.BadRequest(new { error = "That does not look like an email address." });
            if (AccountSetup.LooksLikeAnAttemptAtEmail(username))
                return Results.BadRequest(new { error = "Keep the @ out of your username - the email box is below it." });

            // Registration used to ignore the city entirely, so every player defaulted to New York no
            // matter what they picked. Now that ground is contested inside a town, that would have put
            // everybody in one map and left the other four empty.
            var opts0 = gameOptions.Value;
            opts0.Territory.ApplyDefaultsWhereEmpty();
            var cities = opts0.Territory.Cities();
            var city = cities.FirstOrDefault(x => string.Equals(x, request.City?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(request.City) && city is null)
                return Results.BadRequest(new { error = $"Pick one of: {string.Join(", ", cities)}." });

            if (await db.Accounts.AnyAsync(x => x.Username == username, ct))
                return Results.Conflict(new { error = "Username is already taken." });
            if (await db.Accounts.AnyAsync(x => x.Email == email, ct))
                return Results.Conflict(new { error = "That email is already on an account." });
            if (await db.Players.AnyAsync(x => x.Name == playerName, ct))
                return Results.Conflict(new { error = "Player name is already taken." });

            var opts = gameOptions.Value;
            var isFirstAccount = !await db.Accounts.AnyAsync(ct);
            var account = new PlayerAccount { Username = username, IsAdmin = isFirstAccount };
            account.SetEmail(email);
            account.PasswordHash = passwordHasher.HashPassword(account, request.Password);
            var (player, log) = AccountSetup.NewPlayer(account, playerName, city ?? cities.FirstOrDefault() ?? "New York", opts, pimps);

            db.Accounts.Add(account);
            db.Players.Add(player);
            db.ActionLogs.Add(log);
            await db.SaveChangesAsync(ct);

            // Verification starts at sign-up rather than waiting to be asked for, which is the only
            // moment a player is already thinking about the address they just typed. It is never fatal:
            // an account with an unconfirmed address is a working account that cannot yet be signed
            // into by address, and the account page has the button to finish it.
            if (verification.Options.SendOnSignUp)
                await verification.SendAsync(account, VerificationPurpose.ConfirmAddress, ct);

            await SignInAsync(http, account);
            return Results.Ok(new AuthResponse(player.Id, player.Name, account.Username));
        }).RequireRateLimiting("sign-in");


        app.MapPost("/api/auth/login", async (
            LoginRequest request,
            GameDbContext db,
            IPasswordHasher<PlayerAccount> passwordHasher,
            HttpContext http,
            CancellationToken ct) =>
        {
            var identifier = request.Username?.Trim() ?? string.Empty;
            var password = request.Password ?? string.Empty;

            // One box, two kinds of name. Which one it is decided by the @ rather than by trying both:
            // an address can never be a username, so a lookup against both columns would only ever be
            // one wasted comparison and one more way for two rows to answer the same string.
            var email = AccountSetup.NormalizeEmail(identifier);
            var byEmail = email is not null && AccountSetup.LooksLikeAnAttemptAtEmail(email);
            var account = byEmail
                ? await db.Accounts.Include(x => x.Player).SingleOrDefaultAsync(x => x.Email == email, ct)
                : await db.Accounts.Include(x => x.Player).SingleOrDefaultAsync(x => x.Username == identifier, ct);

            // An account made through Discord has no password to check, and an empty one must never
            // verify - PasswordHasher would simply say no, but saying it here says why it never could.
            if (account is null || account.IsBot || !account.HasPassword
                || passwordHasher.VerifyHashedPassword(account, account.PasswordHash, password) == PasswordVerificationResult.Failed)
                return Results.Unauthorized();

            // An unconfirmed address is not a way in. This is what makes the tick mean something and
            // what makes typing somebody else's address pointless: it holds the address against every
            // other account, and opens nothing.
            //
            // Said out loud rather than folded into the same blank 401, because the password has just
            // been proved - there is nothing left to give away, and a player who is told only "no"
            // when their password is right has no way to work out what to do about it.
            if (byEmail && !account.EmailVerified)
                return Results.Json(
                    new { error = "Confirm that address before signing in with it. Your username still works." },
                    statusCode: StatusCodes.Status403Forbidden);

            var nowUtc = DateTime.UtcNow;
            if (account.IsLockedOut(nowUtc))
                return Results.Json(new { error = account.LockoutMessage(nowUtc) }, statusCode: StatusCodes.Status403Forbidden);

            await SignInAsync(http, account);
            return Results.Ok(new AuthResponse(account.Player!.Id, account.Player.Name, account.Username));
        }).RequireRateLimiting("sign-in");


        app.MapPost("/api/auth/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        }).RequireAuthorization();


        // ---- Discord -------------------------------------------------------------------------
        //
        // Three legs. /start sends the browser to Discord with a signed note saying where it came
        // from; /callback is where Discord sends it back and is the only leg that decides anything;
        // /complete finishes the half of a sign-up Discord cannot answer, which is what the player
        // wants to be called and which town they are setting up in.
        //
        // The same callback serves signing in, signing up, and connecting Discord to an account that
        // already exists, because from Discord's side all three are the same round trip and the only
        // thing that tells them apart is what the server finds when it looks the identity up.

        app.MapGet("/api/auth/discord/start", (
            HttpContext http,
            DiscordAuthService discord,
            DiscordTickets tickets,
            DiscordReturnUrls returnUrls) =>
        {
            if (!discord.Options.IsConfigured)
                return Results.NotFound(new { error = "Discord sign-in is not set up on this server." });

            var returnUrl = returnUrls.Resolve(http.Request.Query["return"]);
            var nonce = DiscordTickets.NewNonce();
            http.Response.Cookies.Append(DiscordTickets.StateCookie, nonce, StateCookieOptions(http));
            return Results.Redirect(discord.AuthorizeUrl(tickets.ProtectState(nonce, returnUrl)));
        });

        app.MapGet("/api/auth/discord/callback", async (
            HttpContext http,
            GameDbContext db,
            DiscordAuthService discord,
            DiscordTickets tickets,
            DiscordReturnUrls returnUrls,
            AccountNotices notices,
            CancellationToken ct) =>
        {
            var state = tickets.ReadState(http.Request.Query["state"]);
            var nonce = http.Request.Cookies[DiscordTickets.StateCookie];
            // Whatever happens next, this note has been spent.
            http.Response.Cookies.Delete(DiscordTickets.StateCookie, StateCookieOptions(http));

            var returnUrl = state?.ReturnUrl ?? returnUrls.Resolve(null);
            if (!discord.Options.IsConfigured)
                return Results.Redirect(DiscordReturnUrls.WithOutcome(returnUrl, "unavailable"));

            // Declining on Discord's screen is a decision, not a failure, and the player should land
            // back where they were without being told something went wrong.
            if (!string.IsNullOrEmpty(http.Request.Query["error"]))
                return Results.Redirect(DiscordReturnUrls.WithOutcome(returnUrl, "cancelled"));

            if (state is null || string.IsNullOrEmpty(nonce) || !CryptographicEquals(state.Value.Nonce, nonce))
                return Results.Redirect(DiscordReturnUrls.WithOutcome(returnUrl, "failed"));

            var code = http.Request.Query["code"].ToString();
            if (string.IsNullOrEmpty(code))
                return Results.Redirect(DiscordReturnUrls.WithOutcome(returnUrl, "failed"));

            var profile = await discord.ExchangeCodeAsync(code, ct);
            if (profile is null)
                return Results.Redirect(DiscordReturnUrls.WithOutcome(returnUrl, "failed"));

            var linked = await db.Accounts.Include(x => x.Player)
                .SingleOrDefaultAsync(x => x.DiscordUserId == profile.Id, ct);

            // Already somebody's: this is a login, and it does not matter who was signed in before.
            if (linked is not null)
            {
                if (linked.IsBot || linked.Player is null)
                    return Results.Redirect(DiscordReturnUrls.WithOutcome(returnUrl, "failed"));
                if (linked.IsLockedOut(DateTime.UtcNow))
                    return Results.Redirect(DiscordReturnUrls.WithOutcome(returnUrl, "locked"));

                // The handle is refreshed on the way through, so the settings page does not go on
                // showing a name its owner stopped using months ago.
                if (linked.DiscordUsername != profile.DisplayName)
                {
                    linked.DiscordUsername = profile.DisplayName;
                    await db.SaveChangesAsync(ct);
                }

                await SignInAsync(http, linked);

                // The only notice that is not about a setting. A connected Discord signs in without a
                // password, so this is the single event that tells somebody another person is in their
                // account - and nothing else would ever mention it.
                await notices.TellAccountAsync(linked, AccountChange.SignedInWithDiscord, profile.DisplayName, ct);

                return Results.Redirect(DiscordReturnUrls.WithOutcome(returnUrl, "signed-in"));
            }

            // Nobody's yet, and somebody is signed in: this is the connect button on the account page.
            if (Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var accountId))
            {
                var current = await db.Accounts.SingleOrDefaultAsync(x => x.Id == accountId, ct);
                if (current is null)
                    return Results.Redirect(DiscordReturnUrls.WithOutcome(returnUrl, "failed"));
                if (current.DiscordUserId is not null)
                    return Results.Redirect(DiscordReturnUrls.WithOutcome(returnUrl, "already-connected"));

                current.DiscordUserId = profile.Id;
                current.DiscordUsername = profile.DisplayName;
                current.DiscordLinkedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);

                // A connection made by somebody else is a permanent way in that needs no password, so
                // this is worth a notice even though the player is standing right here having asked
                // for it. The account has to be loaded with its player for the notice to have a name.
                await db.Entry(current).Reference(x => x.Player).LoadAsync(ct);
                await notices.TellAccountAsync(current, AccountChange.DiscordConnected, profile.DisplayName, ct);

                return Results.Redirect(DiscordReturnUrls.WithOutcome(returnUrl, "connected"));
            }

            // Nobody's, and nobody signed in: a new player who still has to name themselves. The
            // identity is parked in a signed cookie rather than handed to the client, so the finish
            // form cannot claim to be a Discord account it is not.
            http.Response.Cookies.Append(DiscordTickets.SignUpCookie, tickets.ProtectSignUp(profile), SignUpCookieOptions(http));
            return Results.Redirect(DiscordReturnUrls.WithOutcome(returnUrl, "sign-up"));
        }).RequireRateLimiting("sign-in");

        // What the finish-signing-up form needs to draw itself. 404 is the honest answer to a browser
        // that has no ticket: there is nothing half-finished here.
        app.MapGet("/api/auth/discord/ticket", (HttpContext http, DiscordTickets tickets) =>
        {
            var profile = tickets.ReadSignUp(http.Request.Cookies[DiscordTickets.SignUpCookie]);
            return profile is null
                ? Results.NotFound(new { error = "That Discord sign-up has expired. Start again." })
                : Results.Ok(new DiscordSignUpTicketResponse(AccountSetup.SuggestUsername(profile.Username), profile.DisplayName));
        });

        app.MapDelete("/api/auth/discord/ticket", (HttpContext http) =>
        {
            http.Response.Cookies.Delete(DiscordTickets.SignUpCookie, SignUpCookieOptions(http));
            return Results.NoContent();
        });

        app.MapPost("/api/auth/discord/complete", async (
            CompleteDiscordSignUpRequest request,
            HttpContext http,
            GameDbContext db,
            DiscordTickets tickets,
            IOptionsSnapshot<GameOptions> gameOptions,
            PimpRoster pimps,
            EmailVerificationService verification,
            CancellationToken ct) =>
        {
            var profile = tickets.ReadSignUp(http.Request.Cookies[DiscordTickets.SignUpCookie]);
            if (profile is null)
                return Results.BadRequest(new { error = "That Discord sign-up has expired. Start again." });

            var playerName = request.PlayerName?.Trim() ?? string.Empty;
            var username = request.Username?.Trim() ?? string.Empty;
            if (username.Length is < 3 or > 32)
                return Results.BadRequest(new { error = "Username must be 3-32 characters." });
            if (AccountSetup.LooksLikeAnAttemptAtEmail(username))
                return Results.BadRequest(new { error = "Keep the @ out of your username." });
            if (playerName.Length is < 3 or > 32)
                return Results.BadRequest(new { error = "Player name must be 3-32 characters." });

            // Optional, and the same rules the register form applies, because it is the same column.
            var email = AccountSetup.NormalizeEmail(request.Email);
            if (email is not null && !AccountSetup.LooksLikeAnEmail(email))
                return Results.BadRequest(new { error = "That does not look like an email address." });

            var opts = gameOptions.Value;
            opts.Territory.ApplyDefaultsWhereEmpty();
            var cities = opts.Territory.Cities();
            var city = cities.FirstOrDefault(x => string.Equals(x, request.City?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(request.City) && city is null)
                return Results.BadRequest(new { error = $"Pick one of: {string.Join(", ", cities)}." });

            // Checked again rather than trusted from the callback: minutes have passed, and in those
            // minutes the same Discord account could have finished this form in another tab.
            if (await db.Accounts.AnyAsync(x => x.DiscordUserId == profile.Id, ct))
                return Results.Conflict(new { error = "That Discord account is already on an empire. Sign in with it instead." });
            if (await db.Accounts.AnyAsync(x => x.Username == username, ct))
                return Results.Conflict(new { error = "Username is already taken." });
            if (email is not null && await db.Accounts.AnyAsync(x => x.Email == email, ct))
                return Results.Conflict(new { error = "That email is already on an account." });
            if (await db.Players.AnyAsync(x => x.Name == playerName, ct))
                return Results.Conflict(new { error = "Player name is already taken." });

            var isFirstAccount = !await db.Accounts.AnyAsync(ct);
            var account = new PlayerAccount
            {
                Username = username,
                // No password at all, rather than a random one nobody knows. The account page can add
                // one later, and until it does Discord is the only door - which the page says plainly.
                PasswordHash = string.Empty,
                IsAdmin = isFirstAccount,
                DiscordUserId = profile.Id,
                DiscordUsername = profile.DisplayName,
                DiscordLinkedAtUtc = DateTime.UtcNow,
            };
            account.SetEmail(email);
            var (player, log) = AccountSetup.NewPlayer(account, playerName, city ?? cities.FirstOrDefault() ?? "New York", opts, pimps);

            db.Accounts.Add(account);
            db.Players.Add(player);
            db.ActionLogs.Add(log);
            await db.SaveChangesAsync(ct);

            // Same as the register form: the code goes out at the one moment the player is thinking
            // about the address they just typed.
            if (email is not null && verification.Options.SendOnSignUp)
                await verification.SendAsync(account, VerificationPurpose.ConfirmAddress, ct);

            http.Response.Cookies.Delete(DiscordTickets.SignUpCookie, SignUpCookieOptions(http));
            await SignInAsync(http, account);
            return Results.Ok(new AuthResponse(player.Id, player.Name, account.Username));
        }).RequireRateLimiting("sign-in");
    }

    /// <summary>
    /// The moment to stamp both a sessions-valid-after watermark and the cookie that has to survive it:
    /// the current second, floored, plus one.
    ///
    /// Two things are going on here, and both come from the same fact - a cookie ticket writes its
    /// issued-at through the round-trip string format, which keeps whole seconds and throws the
    /// fraction away.
    ///
    /// The flooring is what stops a watermark written from an unrounded clock sitting a few hundred
    /// microseconds ahead of the cookie issued in the same breath, which used to sign a player out of
    /// their own password change.
    ///
    /// The extra second is what closes the window that flooring alone left open. Stamped with the same
    /// floored second, a session that signed in earlier *in that second* compares equal to the
    /// watermark rather than before it, and survives being revoked - so somebody who got in moments
    /// before a reset kept their session. Moving both a second forward makes every session issued in
    /// or before this second strictly earlier than the watermark, while the one re-issued here still
    /// matches it exactly. The cost is a cookie stamped up to a second in the future, which nothing
    /// reads except this comparison.
    /// </summary>
    internal static DateTime ToSessionMoment(DateTime utc)
        => new DateTime(utc.Ticks - (utc.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Utc).AddSeconds(1);

    /// <summary>
    /// Issues the session cookie.
    /// </summary>
    /// <param name="issuedUtc">
    /// Set explicitly by the one caller that has just moved this account's session watermark forward.
    /// Changing a password ends every other session, and the watermark cannot tell this session from
    /// the others except by the moment it was issued - so that moment is stated rather than left to
    /// whatever the clock reads a few milliseconds later.
    /// </param>
    internal static async Task SignInAsync(HttpContext http, PlayerAccount account, DateTimeOffset? issuedUtc = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new(ClaimTypes.Name, account.Username),
            new("is_admin", account.IsAdmin ? "true" : "false")
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await http.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true, IssuedUtc = issuedUtc });
    }

    /// <summary>
    /// Lax rather than Strict: the whole point of these two is to survive a navigation that starts on
    /// discord.com, and Strict would drop them on exactly that hop.
    /// </summary>
    private static CookieOptions StateCookieOptions(HttpContext http) => new()
    {
        HttpOnly = true,
        IsEssential = true,
        SameSite = SameSiteMode.Lax,
        Secure = http.Request.IsHttps,
        Path = "/api/auth/discord",
        MaxAge = TimeSpan.FromMinutes(15),
    };

    private static CookieOptions SignUpCookieOptions(HttpContext http) => new()
    {
        HttpOnly = true,
        IsEssential = true,
        SameSite = SameSiteMode.Lax,
        Secure = http.Request.IsHttps,
        Path = "/api/auth/discord",
        MaxAge = TimeSpan.FromMinutes(20),
    };

    /// <summary>Compared in fixed time, because the nonce is a secret being checked against a guess.</summary>
    private static bool CryptographicEquals(string left, string right)
        => System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(left),
            System.Text.Encoding.UTF8.GetBytes(right));
}
