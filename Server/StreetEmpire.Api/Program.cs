using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
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
using static StreetEmpire.Api.Support.ActionLogging;
using static StreetEmpire.Api.Support.BotSeeding;
using static StreetEmpire.Api.Support.Formatting;
using static StreetEmpire.Api.Support.LiveOpsStore;
using static StreetEmpire.Api.Support.PlayerRanking;

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

            if (lockedOut || staleSession)
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

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", version = "0.2.6" })).DisableRateLimiting();

app.MapAuthEndpoints();
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

app.Run();
