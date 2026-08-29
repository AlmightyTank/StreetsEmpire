using System.Globalization;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Endpoints;
using StreetEmpire.Api.Models;
using StreetEmpire.Api.Services;
using static StreetEmpire.Api.Mapping.ResponseMappers;
using StreetEmpire.Api.Support;
using static StreetEmpire.Api.Support.ActionLogging;
using static StreetEmpire.Api.Support.BotSeeding;
using static StreetEmpire.Api.Support.Formatting;
using static StreetEmpire.Api.Support.LiveOpsStore;
using static StreetEmpire.Api.Support.PlayerRanking;

// First line of the program, because every string this game writes is formatted against it and the
// container it runs in sets no LANG - which would otherwise leave the invariant culture in charge and
// print prices as ¤94. See GameCulture; the wiring is checked by a test, since nothing here fails.
GameCulture.Apply();

// Before anything else, because CreateBuilder reads the environment as it goes and a value that
// arrives afterwards arrives too late. Values already in the real environment are left alone; see
// DotEnv for why that way round.
var dotEnv = DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<GameOptionOverrides>();
builder.Services.Configure<GameOptions>(builder.Configuration.GetSection("Game"));
// Runs per scope for IOptionsSnapshot, so admin overrides take effect on the next request rather than
// at the next restart. Order matters: overrides land on the bound values, then hideout tables are
// filled if config left them out.
builder.Services.AddOptions<GameOptions>().PostConfigure<GameOptionOverrides>((options, overrides) =>
{
    overrides.Apply(options);
    options.ApplyWeaponDefaultsWhereEmpty();
    options.StreetAction.ApplyDistrictDefaultsWhereEmpty();
    options.Alliances.ApplyDefaultsWhereEmpty();
    options.Hideout.ApplyDefaultsWhereEmpty();
    options.Territory.ApplyDefaultsWhereEmpty();
    options.CityMarkets.ApplyDefaultsWhereEmpty(options.Territory.Cities());
});
builder.Services.Configure<BotAutomationOptions>(builder.Configuration.GetSection("Bots"));
builder.Services.AddDbContext<GameDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("GameDatabase")));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentPlayerService>();
builder.Services.AddScoped<TurnService>();
builder.Services.AddScoped<HideoutService>();
builder.Services.AddScoped<PlayerClock>();
builder.Services.AddScoped<StandingsRecorder>();
builder.Services.AddScoped<TerritoryService>();
builder.Services.AddScoped<MarketService>();
builder.Services.AddScoped<MuleService>();
builder.Services.AddScoped<GuidanceService>();
builder.Services.AddScoped<ContractService>();
builder.Services.AddScoped<ChatService>();
builder.Services.AddSingleton<StandingsSchedule>();
builder.Services.AddScoped<PimpRoster>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<EconomyService>();
builder.Services.AddScoped<CombatService>();
builder.Services.AddScoped<StreetStrikeService>();
builder.Services.AddScoped<PrayerService>();
builder.Services.AddScoped<TitleService>();
builder.Services.AddScoped<AllianceService>();
builder.Services.AddSingleton<CombatSchedule>();
builder.Services.AddScoped<CombatMissionService>();
builder.Services.AddScoped<CombatResolutionService>();
builder.Services.AddScoped<BotSimulationService>();
builder.Services.AddSingleton<BotAutomationState>();
builder.Services.AddHostedService<BotAutomationService>();
builder.Services.AddSingleton<IGameRandom, GameRandom>();
builder.Services.AddScoped<IPasswordHasher<PlayerAccount>, PasswordHasher<PlayerAccount>>();

// Discord sign-in. Registered unconditionally so the endpoints exist and can say "not set up" for
// themselves; whether the button is ever shown is decided by DiscordOptions.IsConfigured, which is
// false until a client id and secret arrive from user-secrets or the environment.
builder.Services.AddScoped<RecoveryCodes>();
builder.Services.AddScoped<IntelService>();
builder.Services.Configure<DiscordOptions>(builder.Configuration.GetSection("Auth:Discord"));
builder.Services.AddHttpClient<DiscordAuthService>(client => client.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddScoped<DiscordTickets>();
builder.Services.AddScoped<DiscordReturnUrls>();

// Transactional email, for confirming an address.
//
// The sender is chosen once, here, by whether a Resend key exists. With one, mail goes over Resend's
// HTTP API - not SMTP of our own, because running a mail server means owning deliverability and the
// reward for getting it wrong is verification mail that lands in spam. Without one, the message is
// written to the log instead, which keeps the whole flow clickable on a laptop with no account
// anywhere. The account page is told which of the two is running rather than left to imply the mail
// was sent.
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Auth:Email"));
builder.Services.AddHttpClient<ResendEmailSender>(client => client.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddScoped<IEmailSender>(services =>
    services.GetRequiredService<IOptions<EmailOptions>>().Value.IsConfigured
        ? services.GetRequiredService<ResendEmailSender>()
        : ActivatorUtilities.CreateInstance<LoggedEmailSender>(services));
builder.Services.AddScoped<EmailVerificationService>();
builder.Services.AddScoped<AccountNotices>();
builder.Services.AddHostedService<EmailVerificationSweep>();

// Where the data protection key ring lives.
//
// Unset, ASP.NET keeps it somewhere that belongs to the machine and the user, which is fine on a
// laptop and quietly ruinous in a container: the keys go when the container does. Everything sealed
// with them stops being readable on the next deploy - every session cookie, so every player is signed
// out; every outstanding verification and reset code, which simply stop matching; every half-finished
// Discord sign-up. None of it errors. It all just silently stops working, once, at deploy time.
//
// So in a container this points at a mounted volume. Left unset it behaves exactly as before, which is
// what keeps development unchanged.
var keyRingPath = builder.Configuration["DataProtection:KeyPath"];
if (!string.IsNullOrWhiteSpace(keyRingPath))
{
    Directory.CreateDirectory(keyRingPath);
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath))
        // Named explicitly, because the default is derived from the content root path - which changes
        // between a container and a laptop, and would make keys written by one unreadable by the other.
        .SetApplicationName("StreetEmpire");
}

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "street_empire_session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.Events.OnRedirectToLogin = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        // Without this, Results.Forbid() redirects to an access-denied page and the API answers a
        // non-admin with a 302 to HTML instead of a status the browser client can act on.
        options.Events.OnRedirectToAccessDenied = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
        // A ban, suspension, or force-logout has to end sessions that are already signed in.
        options.Events.OnValidatePrincipal = async ctx =>
        {
            var accountId = ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(accountId, out var id))
                return;

            var db = ctx.HttpContext.RequestServices.GetRequiredService<GameDbContext>();
            var account = await db.Accounts.AsNoTracking()
                .Select(x => new { x.Id, x.IsBanned, x.SuspendedUntilUtc, x.SessionsValidAfterUtc })
                .SingleOrDefaultAsync(x => x.Id == id);

            var now = DateTime.UtcNow;
            var lockedOut = account is null
                || account.IsBanned
                || (account.SuspendedUntilUtc is { } until && until > now);
            var staleSession = account?.SessionsValidAfterUtc is { } validAfter
                && ctx.Properties.IssuedUtc is { } issued
                && issued.UtcDateTime < validAfter;

            // The named session, if this ticket carries one. Tickets issued before sessions were
            // recorded do not, and are deliberately left alone rather than rejected: turning that into
            // a sign-out would sign out every player in the game on the deploy that shipped it, to no
            // benefit. They simply are not listed, and the next sign-in writes a row like any other.
            var revoked = false;
            if (Guid.TryParse(ctx.Principal?.FindFirstValue(AuthEndpoints.SessionClaim), out var sessionId))
            {
                var session = await db.Sessions
                    .SingleOrDefaultAsync(x => x.Id == sessionId && x.AccountId == id);

                // Gone as well as revoked. A row the sweep has removed is a session nobody can see or
                // end any more, and a ticket outliving its record should not be the way back in.
                revoked = session is null || session.RevokedAtUtc is not null;

                // Moved at most every five minutes. Honest to the second would be a write per session
                // per poll, which on a game that asks every five seconds makes this the busiest table
                // in the database to answer a question asked once a month.
                if (!revoked && session is not null && session.LastSeenAtUtc < now.AddMinutes(-5))
                {
                    session.LastSeenAtUtc = now;
                    await db.SaveChangesAsync();
                }
            }

            if (lockedOut || staleSession || revoked)
            {
                ctx.RejectPrincipal();
                await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });
// Rate limiting.
//
// The game is played by polling: a client asks for missions every five seconds, chat every eight, and
// fires roughly seven calls to refresh after every action. Steady play is twenty to ninety requests a
// minute per open tab, so the ceiling has to clear that comfortably or the limiter becomes a bug that
// only appears when somebody is playing well.
//
// Partitioned by player rather than by address, because a household or an office behind one address
// is several players, and throttling them together would punish them for their router. Anonymous
// traffic has no player to name and falls back to the address.
const int requestsPerMinute = 300;
const int signInAttemptsPerMinute = 10;

builder.Services.AddRateLimiter(limiter =>
{
    limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var player = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var partition = player ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(partition, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = requestsPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });
    });

    // Signing in is the one place where a low ceiling is the point rather than a safety margin: it is
    // what turns a stolen password list from a script into a wait. Keyed on the address, because the
    // whole problem is a caller who has not proved who they are.
    limiter.AddPolicy("sign-in", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = signInAttemptsPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));

    limiter.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        // Say when to come back rather than only that they cannot. Without this a client has no way
        // to behave well, and the only strategy left to it is to keep trying.
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { error = "Too many requests. Slow down and try again shortly." }, token);
    };
});

// Believing the proxy about who asked, and how.
//
// Behind TLS termination the app sees a plain HTTP request from a container on the bridge network, and
// two things silently go wrong if it takes that at face value.
//
// The session cookie is issued with CookieSecurePolicy.SameAsRequest, so it would go out without the
// Secure flag while the browser is on HTTPS - TLS on the wire and a cookie that would happily be sent
// over plaintext the first time anything links to http://.
//
// And the sign-in rate limiter partitions anonymous traffic by remote address, which behind a proxy is
// the proxy for everybody. Ten attempts a minute stops being per person and becomes ten a minute for
// the entire game, so players lock each other out.
//
// The known-proxy lists are cleared rather than enumerated because the proxy's address on a Docker
// bridge is not knowable in advance. That is only safe while nothing can reach this app except through
// the proxy - the compose file publishes no host port for it, and this switch stays off by default so
// that a directly exposed instance never trusts a header anybody could set.
var trustProxyHeaders = builder.Configuration.GetValue("Proxy:TrustForwardedHeaders", false);
if (trustProxyHeaders)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

builder.Services.AddAuthorization();

// The allowed origins are configuration, not a constant.
//
// This named one port, 5173, which stopped being the port the client runs on the moment the dev
// server was allowed to pick its own. Nothing broke, because the Vite proxy makes the browser's calls
// same-origin and CORS never gets a say - which is precisely why it went unnoticed and would have
// stayed unnoticed until the day the client was served from its own origin and every request failed.
//
// Credentials are allowed, so this can never become AllowAnyOrigin: the two are mutually exclusive by
// spec, and the session cookie is the whole reason the policy exists.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173", "http://localhost:3000"];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

var app = builder.Build();

// Said now rather than at load time, because at load time there was no logger. Worth a line either
// way: "the key I put in .env is not being read" is otherwise a silent, unfalsifiable guess.
if (dotEnv.Found)
    app.Logger.LogInformation(
        "Read {Applied} setting(s) from {Path}. {Skipped} were left alone because the environment already set them.",
        dotEnv.Applied, dotEnv.Path, dotEnv.SkippedBecauseAlreadySet);
else
    app.Logger.LogInformation("No .env file found. Configuration is coming from appsettings and the environment.");

// Said at startup because the alternative is finding out from Discord.
//
// A redirect that is not registered on the Discord application - or registered and not saved - fails
// on Discord's own page, which means the browser never comes back and nothing on this side ever runs.
// There is no error for the server to catch and nothing in its log to read. Printing the exact string
// that has to be in the Discord dashboard turns "Invalid OAuth2 redirect_uri" from a guess into a
// comparison of two lines of text.
var discordAtStartup = app.Services.GetRequiredService<IOptions<DiscordOptions>>().Value;
if (discordAtStartup.IsConfigured)
    app.Logger.LogInformation(
        "Discord sign-in is on. This exact string must be registered and saved under OAuth2 > Redirects: {RedirectUri}",
        discordAtStartup.RedirectUri);

// Mail that goes nowhere, said loudly, and only where it would be a surprise.
//
// With no provider key the verification code is written to this log instead of sent. On a laptop that
// is the point - the whole flow is clickable without an account anywhere. Anywhere else it is a quiet
// disaster: every player is stuck at an unconfirmed address, nobody can reset a password, and the only
// hint is one line on a settings page. A warning here is the difference between finding that out at
// startup and finding it out from a player.
var emailAtStartup = app.Services.GetRequiredService<IOptions<EmailOptions>>().Value;
if (emailAtStartup.IsConfigured)
    app.Logger.LogInformation("Email is on, sending as {From}.", emailAtStartup.FromAddress);
else if (app.Environment.IsDevelopment())
    app.Logger.LogInformation(
        "No email provider is configured. Verification codes and account notices will be written to this log instead of sent.");
else
    app.Logger.LogWarning(
        "NO EMAIL PROVIDER IS CONFIGURED and this is not a development environment. Verification codes "
        + "and account notices are being written to this log instead of sent, which means no player can "
        + "confirm an address or reset a password. Set Auth__Email__ApiKey.");

// Overrides live in the database, so they have to be in memory before the first request binds options.
using (var startupScope = app.Services.CreateScope())
{
    var startupDb = startupScope.ServiceProvider.GetRequiredService<GameDbContext>();
    var startupOverrides = startupScope.ServiceProvider.GetRequiredService<GameOptionOverrides>();

    // Pending migrations are applied before anything reads a table.
    //
    // This existed nowhere until now, which meant a schema change only reached a database when
    // somebody remembered to run `dotnet ef database update` by hand. That is exactly how the
    // workshop queue shipped against a table that happened to already be there - the migration had
    // been applied out of band, and nothing in the application would have noticed if it had not.
    //
    // The honest caveat: this races if two instances start at once, because both would try to
    // migrate. That is a real problem for a rolling deploy and not one this game has yet - it runs
    // as a single process, and the background bot service already assumes that. When a second
    // instance becomes possible, this moves to a deploy step and the bot service needs a lock;
    // until then, a schema that arrives on its own beats a schema that arrives when remembered.
    if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
    {
        try
        {
            var pending = (await startupDb.Database.GetPendingMigrationsAsync()).ToList();
            if (pending.Count > 0)
            {
                app.Logger.LogInformation(
                    "Applying {Count} pending migration(s): {Migrations}",
                    pending.Count, string.Join(", ", pending));
                await startupDb.Database.MigrateAsync();
            }
        }
        catch (Exception ex)
        {
            // Loud and fatal. A server that keeps running against a schema it cannot read will fail
            // one endpoint at a time, in ways that look like unrelated bugs.
            app.Logger.LogCritical(ex, "Could not apply database migrations. Refusing to start.");
            throw;
        }
    }

    try
    {
        var stored = await startupDb.GameSettings
            .AsNoTracking()
            .Where(x => x.Id == 1)
            .Select(x => new { x.ConfigOverridesJson, x.BotAutomationEnabled, x.BotTickSeconds, x.BotRoundsPerTick })
            .SingleOrDefaultAsync();
        if (!string.IsNullOrWhiteSpace(stored?.ConfigOverridesJson))
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(stored.ConfigOverridesJson);
            if (parsed is not null)
                startupOverrides.Replace(parsed);
        }
        if (stored is not null)
        {
            // Automatic AI is deliberately restored before the background service starts, so a restart
            // does not quietly revert an admin's decision to the appsettings default.
            var startupAutomation = startupScope.ServiceProvider.GetRequiredService<BotAutomationState>();
            startupAutomation.SetEnabled(stored.BotAutomationEnabled);
            startupAutomation.SetTiming(stored.BotTickSeconds, stored.BotRoundsPerTick);
        }
    }
    catch (Exception ex)
    {
        // A missing or unreadable settings row must not stop the game booting on appsettings alone.
        app.Logger.LogWarning(ex, "Could not load stored settings; running on appsettings values.");
    }
}

// Anything that reaches here uncaught, outside development.
//
// A bug used to answer a bare 500 with whatever the framework felt like putting in the body - in
// development, a full stack trace, sent to whoever asked. The developer page is worth keeping where it
// helps and worth nowhere near production, so it keeps development and this takes everywhere else: one
// sentence in the shape every other error in this API uses, and the detail in the log where it belongs.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(handler => handler.Run(async context =>
    {
        var thrown = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        app.Logger.LogError(thrown, "Unhandled exception answering {Method} {Path}.",
            context.Request.Method, context.Request.Path);

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error = "Something went wrong. Try again shortly." });
    }));
}

// First in the pipeline, because everything after it asks either what scheme the caller used or where
// they came from, and both are wrong until this has run.
if (trustProxyHeaders)
    app.UseForwardedHeaders();

// The built client, when there is one beside us.
//
// In development the client is served by Vite on its own port and talks here through a proxy. In a
// container the two ship together and this serves it, which is worth more than the tidiness: one
// origin means CORS has nothing to do, cookies are plainly first-party, and there is exactly one
// address to register with Discord as a callback rather than one that moves.
var clientRoot = app.Environment.WebRootPath;
var servingClient = !string.IsNullOrWhiteSpace(clientRoot) && File.Exists(Path.Combine(clientRoot, "index.html"));
if (servingClient)
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.Logger.LogInformation(
    trustProxyHeaders
        ? "Trusting X-Forwarded-* headers: expecting to sit behind a proxy that terminates TLS."
        : "Not trusting proxy headers: expecting to be reached directly.");

app.Logger.LogInformation(
    servingClient ? "Serving the built client from {Root}." : "API only - no built client alongside ({Root}).",
    clientRoot ?? "(none)");

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
// After authentication, so a request can be counted against the player who made it.
app.UseRateLimiter();

// Maintenance gate. Written as middleware rather than a per-endpoint check so a new gameplay endpoint
// cannot forget it. Reads stay open so a locked-out player still sees their empire and the notice.
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;
    var isWrite = !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method);
    var isGameplay = path.StartsWith("/api/game/", StringComparison.OrdinalIgnoreCase);
    if (!isWrite || !isGameplay)
    {
        await next(context);
        return;
    }

    var db = context.RequestServices.GetRequiredService<GameDbContext>();
    var settings = await LiveOpsAsync(db, context.RequestAborted);
    if (!settings.MaintenanceMode)
    {
        await next(context);
        return;
    }

    // Admins keep playing through maintenance so they can verify a deploy.
    var current = context.RequestServices.GetRequiredService<CurrentPlayerService>();
    var player = await current.GetAsync(context.RequestAborted);
    if (player?.Account.IsAdmin == true)
    {
        await next(context);
        return;
    }

    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
    await context.Response.WriteAsJsonAsync(new
    {
        error = string.IsNullOrWhiteSpace(settings.MaintenanceMessage)
            ? "The game is down for maintenance. Try again shortly."
            : settings.MaintenanceMessage
    }, context.RequestAborted);
});

// Read off the assembly, which took it from the VERSION file at build time. It used to be a string
// typed in here, which agreed with the other four copies of the number right up until it would not.
//
// The build is reported alongside it because the informational version carries the commit it was built
// from, and "is the thing I just deployed actually the thing running" is the question a health endpoint
// is asked from a server more than any other.
var buildVersion = typeof(Program).Assembly
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
    ?? "unknown";
var releaseVersion = buildVersion.Split('+')[0];

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    version = releaseVersion,
    build = buildVersion,
})).DisableRateLimiting();

app.MapAuthEndpoints();
app.MapAccountEndpoints();
app.MapPasswordResetEndpoints();
app.MapGameEndpoints();
app.MapCombatEndpoints();
app.MapWorldEndpoints();
app.MapAllianceEndpoints();
app.MapTerritoryEndpoints();
app.MapMarketEndpoints();
app.MapMuleEndpoints();
app.MapContractEndpoints();
app.MapChatEndpoints();
app.MapAdminPlayerEndpoints();
app.MapAdminOpsEndpoints();

// Anything that is not an API route and not a file on disk is the client's own routing to resolve, so
// it gets the shell and works it out in the browser.
//
// API paths are deliberately excluded rather than swept up with everything else: a mistyped endpoint
// answering 200 and a page of HTML is a much harder thing to debug than one answering 404.
if (servingClient)
{
    app.MapFallback(async context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(new { error = "No such endpoint." });
            return;
        }

        context.Response.ContentType = "text/html";
        await context.Response.SendFileAsync(Path.Combine(clientRoot!, "index.html"));
    });
}

app.Run();
