using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
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
    options.Hideout.ApplyDefaultsWhereEmpty();
    options.Territory.ApplyDefaultsWhereEmpty();
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
builder.Services.AddSingleton<StandingsSchedule>();
builder.Services.AddScoped<PimpRoster>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<EconomyService>();
builder.Services.AddScoped<CombatService>();
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
builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy
        .WithOrigins("http://localhost:5173")
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

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", version = "0.2.4" }));

app.MapAuthEndpoints();
app.MapGameEndpoints();
app.MapCombatEndpoints();
app.MapWorldEndpoints();
app.MapTerritoryEndpoints();
app.MapAdminPlayerEndpoints();
app.MapAdminOpsEndpoints();

app.Run();
