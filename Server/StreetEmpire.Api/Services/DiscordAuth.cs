using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace StreetEmpire.Api.Services;

/// <summary>
/// What the server needs before it can offer a Discord button at all.
///
/// None of it ships with a value. A client secret in appsettings.json is a client secret in the
/// repository, so these arrive from the gitignored <c>.env</c> file at the repository root, or from
/// the real environment in production. Until they do, <see cref="IsConfigured"/> is false and the
/// button is never shown - which is the point: a door that is drawn but cannot open is worse than no
/// door. See <c>.env.example</c> for the names.
/// </summary>
public sealed class DiscordOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Where Discord sends the browser back to, and it has to be character-for-character what is
    /// registered in the Discord application - Discord compares the string, not the destination.
    ///
    /// This points at the API rather than the client on purpose. The client's dev port moves (Vite is
    /// handed a free one when several sessions run at once) and a moving port cannot be registered
    /// anywhere. Cookies ignore the port, so a session cookie set here on localhost is still sent by
    /// the browser once it is back on the client's port a moment later.
    /// </summary>
    public string RedirectUri { get; set; } = "http://localhost:5080/api/auth/discord/callback";

    /// <summary>
    /// Where the player is put down afterwards, used when the client did not ask for somewhere. The
    /// client normally does ask, by handing its own origin to /start, so this is the fallback for a
    /// callback that arrives with nothing to go on.
    /// </summary>
    public string ReturnUrl { get; set; } = "http://localhost:5173/";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}

/// <summary>The only three facts the game takes from Discord.</summary>
public sealed record DiscordProfile(string Id, string Username, string? GlobalName)
{
    /// <summary>The handle as a person would recognise it, preferring the display name they set.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(GlobalName) ? Username : GlobalName!;
}

/// <summary>
/// The two calls that turn a returned code into a Discord identity. Nothing here touches the database
/// or the session; deciding what an identity means is the endpoint's job.
/// </summary>
public sealed class DiscordAuthService(HttpClient http, IOptions<DiscordOptions> options, ILogger<DiscordAuthService> logger)
{
    private const string AuthorizeEndpoint = "https://discord.com/oauth2/authorize";
    private const string TokenEndpoint = "https://discord.com/api/oauth2/token";
    private const string CurrentUserEndpoint = "https://discord.com/api/users/@me";

    public DiscordOptions Options => options.Value;

    /// <summary>
    /// Only <c>identify</c> is asked for. Discord will hand over an email address for the asking, and
    /// the game has no use for one it did not verify itself, so it does not ask.
    /// </summary>
    public string AuthorizeUrl(string state)
    {
        var query = new Dictionary<string, string?>
        {
            ["client_id"] = Options.ClientId,
            ["redirect_uri"] = Options.RedirectUri,
            ["response_type"] = "code",
            ["scope"] = "identify",
            ["state"] = state,
        };
        return QueryHelpers.AddQueryString(AuthorizeEndpoint, query);
    }

    /// <summary>Null on any failure, because there is nothing a caller could usefully do with the why.</summary>
    public async Task<DiscordProfile?> ExchangeCodeAsync(string code, CancellationToken ct)
    {
        try
        {
            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = Options.ClientId,
                ["client_secret"] = Options.ClientSecret,
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = Options.RedirectUri,
            });

            using var tokenResponse = await http.PostAsync(TokenEndpoint, form, ct);
            if (!tokenResponse.IsSuccessStatusCode)
            {
                logger.LogWarning("Discord refused the code exchange with {Status}.", tokenResponse.StatusCode);
                return null;
            }

            using var tokenBody = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync(ct));
            if (!tokenBody.RootElement.TryGetProperty("access_token", out var accessToken))
                return null;

            using var userRequest = new HttpRequestMessage(HttpMethod.Get, CurrentUserEndpoint);
            userRequest.Headers.Authorization = new("Bearer", accessToken.GetString());
            using var userResponse = await http.SendAsync(userRequest, ct);
            if (!userResponse.IsSuccessStatusCode)
            {
                logger.LogWarning("Discord refused the profile read with {Status}.", userResponse.StatusCode);
                return null;
            }

            using var user = JsonDocument.Parse(await userResponse.Content.ReadAsStringAsync(ct));
            var id = user.RootElement.TryGetProperty("id", out var idValue) ? idValue.GetString() : null;
            var username = user.RootElement.TryGetProperty("username", out var nameValue) ? nameValue.GetString() : null;
            var globalName = user.RootElement.TryGetProperty("global_name", out var globalValue) && globalValue.ValueKind == JsonValueKind.String
                ? globalValue.GetString()
                : null;

            return string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(username)
                ? null
                : new DiscordProfile(id!, username!, globalName);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Could not complete the Discord exchange.");
            return null;
        }
    }
}

/// <summary>
/// The two short-lived, signed notes the round trip needs.
///
/// A trip through somebody else's site cannot keep anything in memory, so both halves are signed blobs
/// the server hands to the browser and refuses to believe on the way back unless the signature and the
/// clock agree. Data protection does the signing, which means neither can be read or forged by the
/// browser holding it, and neither is worth stealing once its few minutes are up.
/// </summary>
public sealed class DiscordTickets(IDataProtectionProvider provider)
{
    /// <summary>Long enough to get through Discord's consent screen, short enough to be worthless later.</summary>
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(15);

    /// <summary>Long enough to pick a name and a town without being rushed.</summary>
    private static readonly TimeSpan SignUpLifetime = TimeSpan.FromMinutes(20);

    private readonly ITimeLimitedDataProtector _state =
        provider.CreateProtector("StreetEmpire.Discord.State").ToTimeLimitedDataProtector();
    private readonly ITimeLimitedDataProtector _signUp =
        provider.CreateProtector("StreetEmpire.Discord.SignUp").ToTimeLimitedDataProtector();

    public const string StateCookie = "street_empire_discord_state";
    public const string SignUpCookie = "street_empire_discord_signup";

    /// <param name="nonce">Also written to a cookie, and compared on the way back. The signature proves
    /// the state came from this server; the nonce proves it came from this browser, which is what stops
    /// a login somebody else finished from being replayed into your session.</param>
    public string ProtectState(string nonce, string returnUrl)
        => _state.Protect($"{nonce}\n{returnUrl}", StateLifetime);

    public (string Nonce, string ReturnUrl)? ReadState(string? protectedState)
    {
        if (string.IsNullOrWhiteSpace(protectedState)) return null;
        try
        {
            var parts = _state.Unprotect(protectedState).Split('\n');
            return parts.Length == 2 ? (parts[0], parts[1]) : null;
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            // Tampered with, issued under a different key ring, or simply too old. All the same answer.
            return null;
        }
    }

    public string ProtectSignUp(DiscordProfile profile)
        => _signUp.Protect($"{profile.Id}\n{profile.Username}\n{profile.GlobalName ?? string.Empty}", SignUpLifetime);

    public DiscordProfile? ReadSignUp(string? protectedTicket)
    {
        if (string.IsNullOrWhiteSpace(protectedTicket)) return null;
        try
        {
            var parts = _signUp.Unprotect(protectedTicket).Split('\n');
            return parts.Length == 3
                ? new DiscordProfile(parts[0], parts[1], string.IsNullOrEmpty(parts[2]) ? null : parts[2])
                : null;
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }

    public static string NewNonce()
        => Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
}

/// <summary>
/// Where the browser may be sent when the round trip finishes.
///
/// The client asks to be returned to its own origin, because in development that origin sits on a port
/// nobody can predict. An origin the caller named and the server obeys is an open redirect unless it is
/// checked, so it is checked: it has to be one of the origins CORS already trusts, or - in development
/// only - some port on this machine.
/// </summary>
public sealed class DiscordReturnUrls(IConfiguration configuration, IWebHostEnvironment environment, IOptions<DiscordOptions> options)
{
    public string Resolve(string? requested)
    {
        var fallback = options.Value.ReturnUrl;
        if (string.IsNullOrWhiteSpace(requested)) return fallback;
        if (!Uri.TryCreate(requested, UriKind.Absolute, out var uri)) return fallback;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return fallback;

        var origin = uri.GetLeftPart(UriPartial.Authority);
        var allowed = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (allowed.Any(x => string.Equals(x.TrimEnd('/'), origin, StringComparison.OrdinalIgnoreCase)))
            return origin + "/";

        if (environment.IsDevelopment() && uri.IsLoopback)
            return origin + "/";

        return fallback;
    }

    /// <summary>Adds the one-word outcome the client reads on arrival, keeping whatever query is there.</summary>
    public static string WithOutcome(string returnUrl, string outcome)
        => QueryHelpers.AddQueryString(returnUrl, "discord", outcome);
}
