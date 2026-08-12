using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;
using StreetEmpire.Api.Services;

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
});
builder.Services.Configure<BotAutomationOptions>(builder.Configuration.GetSection("Bots"));
builder.Services.AddDbContext<GameDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("GameDatabase")));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentPlayerService>();
builder.Services.AddScoped<TurnService>();
builder.Services.AddScoped<HideoutService>();
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
            .Select(x => x.ConfigOverridesJson)
            .SingleOrDefaultAsync();
        if (!string.IsNullOrWhiteSpace(stored))
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(stored);
            if (parsed is not null)
                startupOverrides.Replace(parsed);
        }
    }
    catch (Exception ex)
    {
        // A missing or unreadable settings row must not stop the game booting on appsettings alone.
        app.Logger.LogWarning(ex, "Could not load tuning overrides; running on appsettings values.");
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

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", version = "0.2.1" }));

app.MapPost("/api/auth/register", async (
    RegisterRequest request,
    GameDbContext db,
    IPasswordHasher<PlayerAccount> passwordHasher,
    IOptionsSnapshot<GameOptions> gameOptions,
    PimpRoster pimps,
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

    if (await db.Accounts.AnyAsync(x => x.Username == username, ct))
        return Results.Conflict(new { error = "Username is already taken." });
    if (await db.Players.AnyAsync(x => x.Name == playerName, ct))
        return Results.Conflict(new { error = "Player name is already taken." });

    var opts = gameOptions.Value;
    var isFirstAccount = !await db.Accounts.AnyAsync(ct);
    var account = new PlayerAccount { Username = username, IsAdmin = isFirstAccount };
    account.PasswordHash = passwordHasher.HashPassword(account, request.Password);
    var player = new Player
    {
        Account = account,
        Name = playerName,
        Cash = opts.StartingCash,
        BankCash = opts.StartingBankCash,
        Turns = opts.StartingTurns,
        Pimps = opts.StartingPimps,
        Hoes = opts.StartingHoes,
        Thugs = opts.StartingThugs,
        Condoms = opts.StartingCondoms,
        Beer = opts.StartingBeer,
        Weapons = opts.StartingWeapons,
        HoeCutPercent = opts.StartingHoeCutPercent,
        HoeHappiness = 100,
        ThugHappiness = 100,
        LastTurnUpdateUtc = DateTime.UtcNow
    };
    player.Hideout = new Hideout { Player = player };
    // Turns the starting pimp count into named crew.
    pimps.Reconcile(player, DateTime.UtcNow);

    db.Accounts.Add(account);
    db.Players.Add(player);
    db.ActionLogs.Add(new GameActionLog
    {
        Player = player,
        Action = "START",
        Summary = $"{playerName} started an operation in New York with ${opts.StartingCash:N0}, {opts.StartingPimps} pimp(s), {opts.StartingHoes} hoe(s), and {opts.StartingThugs} thug(s).",
        CashDelta = opts.StartingCash,
        PimpsDelta = opts.StartingPimps,
        HoesDelta = opts.StartingHoes,
        ThugsDelta = opts.StartingThugs,
        CondomsDelta = opts.StartingCondoms,
        BeerDelta = opts.StartingBeer,
        WeaponsDelta = opts.StartingWeapons
    });
    await db.SaveChangesAsync(ct);

    await SignInAsync(http, account);
    return Results.Ok(new AuthResponse(player.Id, player.Name, account.Username));
});

app.MapPost("/api/auth/login", async (
    LoginRequest request,
    GameDbContext db,
    IPasswordHasher<PlayerAccount> passwordHasher,
    HttpContext http,
    CancellationToken ct) =>
{
    var username = request.Username?.Trim() ?? string.Empty;
    var password = request.Password ?? string.Empty;
    var account = await db.Accounts.Include(x => x.Player)
        .SingleOrDefaultAsync(x => x.Username == username, ct);

    if (account is null || account.IsBot || passwordHasher.VerifyHashedPassword(account, account.PasswordHash, password) == PasswordVerificationResult.Failed)
        return Results.Unauthorized();

    var nowUtc = DateTime.UtcNow;
    if (account.IsLockedOut(nowUtc))
        return Results.Json(new { error = account.LockoutMessage(nowUtc) }, statusCode: StatusCodes.Status403Forbidden);

    await SignInAsync(http, account);
    return Results.Ok(new AuthResponse(account.Player!.Id, account.Player.Name, account.Username));
});

app.MapPost("/api/auth/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.NoContent();
}).RequireAuthorization();

app.MapGet("/api/game/dashboard", async (
    CurrentPlayerService current,
    GameDbContext db,
    TurnService turns,
    EconomyService economy,
    IOptionsSnapshot<GameOptions> gameOptions,
    HideoutService hideouts,
    PimpRoster pimps,
    CombatMissionService combatMissions,
    CombatResolutionService combatResolver,
    CancellationToken ct) =>
{
    var player = await current.GetAsync(ct);
    if (player is null) return Results.Unauthorized();

    var now = DateTime.UtcNow;
    await combatResolver.ResolveDueAsync(now, ct);
    if (turns.Refresh(player, now))
        await db.SaveChangesAsync(ct);

    var netWorth = economy.CalculateNetWorth(player);
    var combatSince = now.AddDays(-1);
    var recentAttacksMade = await db.CombatLogs.AsNoTracking()
        .CountAsync(x => x.AttackerId == player.Id && x.CreatedAtUtc >= combatSince, ct);
    var recentDefenses = await db.CombatLogs.AsNoTracking()
        .CountAsync(x => x.DefenderId == player.Id && x.CreatedAtUtc >= combatSince, ct);
    var combatCrew = await combatMissions.CommitmentAsync(player, ct);
    var laneReadyAt = await combatMissions.LaneReadyAtUtcAsync(player.Id, now, ct);
    var commandingPimpIds = await combatMissions.ActiveAttackMissions(player.Id)
        .Where(x => x.CommanderPimpId != null)
        .Select(x => x.CommanderPimpId!.Value)
        .ToListAsync(ct);
    var rank = await db.Players.AsNoTracking()
        .CountAsync(economy.RanksAbove(netWorth, player.CreatedAtUtc), ct) + 1;
    var activity = await db.ActionLogs.AsNoTracking()
        .Where(x => x.PlayerId == player.Id)
        .OrderByDescending(x => x.CreatedAtUtc)
        .Take(12)
        .Select(x => new ActivityResponse(
            x.Id, x.Action, x.Summary, x.TurnsSpent, x.CashDelta, x.BankDelta, x.CreatedAtUtc))
        .ToListAsync(ct);

    var opts = gameOptions.Value;
    return Results.Ok(new DashboardResponse(
        player.Id,
        player.Name,
        player.Account.IsAdmin,
        player.City,
        player.Cash,
        player.BankCash,
        netWorth,
        rank,
        player.Turns,
        opts.MaxTurns,
        opts.MaxActionTurns,
        opts.TurnsPerTick,
        opts.TurnTickMinutes,
        turns.SecondsUntilNextTick(player, now),
        player.Pimps,
        player.Hoes,
        player.Thugs,
        player.HoeCutPercent,
        Math.Round(player.HoeHappiness, 2),
        Math.Round(player.ThugHappiness, 2),
        player.Condoms,
        player.Beer,
        player.Weapons,
        player.Weed,
        player.Coke,
        opts.WeedSellPrice,
        opts.CokeSellPrice,
        economy.GetCrewReport(player),
        ToHideoutResponse(player, hideouts),
        pimps.Active(player).Select(x => ToPimpResponse(x, commandingPimpIds)).ToList(),
        pimps.Fallen(player).Take(12).Select(x => ToPimpResponse(x, commandingPimpIds)).ToList(),
        ToCombatCrewResponse(combatCrew),
        ToCombatStatus(player, now, player, opts, recentAttacksMade, recentDefenses, laneReadyAt),
        economy.GetStore(),
        activity));
}).RequireAuthorization();

app.MapPost("/api/game/street", async (
    ScoutRequest request,
    CurrentPlayerService current,
    GameDbContext db,
    TurnService turns,
    EconomyService economy,
    CombatResolutionService combatResolver,
    CancellationToken ct) =>
{
    var player = await current.GetAsync(ct);
    if (player is null) return Results.Unauthorized();

    var now = DateTime.UtcNow;
    await combatResolver.ResolveDueAsync(now, ct);
    var pendingAttack = await ActiveOutgoingMissionAsync(db, player.Id, ct);
    if (pendingAttack is not null)
        return Results.BadRequest(new { error = PendingAttackMessage(pendingAttack) });

    turns.Refresh(player, now);
    var before = Snapshot(player);
    try
    {
        var result = economy.Scout(player, request.Turns, request.AutoBuySupplies);
        AddLog(db, player, before, "STREET", request.Turns, result.Summary);
        await db.SaveChangesAsync(ct);
        return Results.Ok(result);
    }
    catch (GameRuleException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).RequireAuthorization();

// Keep the 0.1.0 route as a compatibility alias while the UI moves to /street.
app.MapPost("/api/game/scout", async (
    ScoutRequest request,
    CurrentPlayerService current,
    GameDbContext db,
    TurnService turns,
    EconomyService economy,
    CombatResolutionService combatResolver,
    CancellationToken ct) =>
{
    var player = await current.GetAsync(ct);
    if (player is null) return Results.Unauthorized();

    var now = DateTime.UtcNow;
    await combatResolver.ResolveDueAsync(now, ct);
    var pendingAttack = await ActiveOutgoingMissionAsync(db, player.Id, ct);
    if (pendingAttack is not null)
        return Results.BadRequest(new { error = PendingAttackMessage(pendingAttack) });

    turns.Refresh(player, now);
    var before = Snapshot(player);
    try
    {
        var result = economy.Scout(player, request.Turns, request.AutoBuySupplies);
        AddLog(db, player, before, "STREET", request.Turns, result.Summary);
        await db.SaveChangesAsync(ct);
        return Results.Ok(result);
    }
    catch (GameRuleException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).RequireAuthorization();

app.MapPost("/api/game/production", async (
    ProduceRequest request,
    CurrentPlayerService current,
    GameDbContext db,
    TurnService turns,
    EconomyService economy,
    CancellationToken ct) =>
{
    var player = await current.GetAsync(ct);
    if (player is null) return Results.Unauthorized();

    turns.Refresh(player, DateTime.UtcNow);
    var before = Snapshot(player);
    try
    {
        var result = economy.Produce(player, request.Product, request.Turns);
        AddLog(db, player, before, "PRODUCTION", request.Turns, result.Summary);
        await db.SaveChangesAsync(ct);
        return Results.Ok(result);
    }
    catch (GameRuleException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).RequireAuthorization();

app.MapPost("/api/game/product/sell", async (
    SellProductRequest request,
    CurrentPlayerService current,
    GameDbContext db,
    EconomyService economy,
    CancellationToken ct) =>
{
    var player = await current.GetAsync(ct);
    if (player is null) return Results.Unauthorized();

    var before = Snapshot(player);
    try
    {
        var result = economy.SellProduct(player, request.Product, request.Quantity);
        AddLog(db, player, before, "SALE", 0, result.Summary);
        await db.SaveChangesAsync(ct);
        return Results.Ok(result);
    }
    catch (GameRuleException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).RequireAuthorization();

app.MapGet("/api/game/store", (EconomyService economy) => Results.Ok(economy.GetStore()))
    .RequireAuthorization();

app.MapPost("/api/game/store/buy", async (
    StoreBuyRequest request,
    CurrentPlayerService current,
    GameDbContext db,
    EconomyService economy,
    CancellationToken ct) =>
{
    var player = await current.GetAsync(ct);
    if (player is null) return Results.Unauthorized();

    var before = Snapshot(player);
    try
    {
        var result = economy.BuyStoreItem(player, request.ItemKey, request.Quantity);
        AddLog(db, player, before, "STORE", 0, result.Summary);
        await db.SaveChangesAsync(ct);
        return Results.Ok(result);
    }
    catch (GameRuleException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).RequireAuthorization();

app.MapPost("/api/game/bank/deposit", async (
    BankRequest request,
    CurrentPlayerService current,
    GameDbContext db,
    EconomyService economy,
    CancellationToken ct) =>
{
    var player = await current.GetAsync(ct);
    if (player is null) return Results.Unauthorized();

    var before = Snapshot(player);
    try
    {
        var result = economy.Deposit(player, request.Amount);
        AddLog(db, player, before, "BANK", 0, result.Summary);
        await db.SaveChangesAsync(ct);
        return Results.Ok(result);
    }
    catch (GameRuleException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).RequireAuthorization();

app.MapPost("/api/game/bank/withdraw", async (
    BankRequest request,
    CurrentPlayerService current,
    GameDbContext db,
    EconomyService economy,
    CancellationToken ct) =>
{
    var player = await current.GetAsync(ct);
    if (player is null) return Results.Unauthorized();

    var before = Snapshot(player);
    try
    {
        var result = economy.Withdraw(player, request.Amount);
        AddLog(db, player, before, "BANK", 0, result.Summary);
        await db.SaveChangesAsync(ct);
        return Results.Ok(result);
    }
    catch (GameRuleException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).RequireAuthorization();

app.MapPut("/api/game/crew/settings", async (
    UpdateCrewSettingsRequest request,
    CurrentPlayerService current,
    GameDbContext db,
    EconomyService economy,
    CancellationToken ct) =>
{
    var player = await current.GetAsync(ct);
    if (player is null) return Results.Unauthorized();

    var before = Snapshot(player);
    try
    {
        var result = economy.UpdateCrewSettings(player, request.HoeCutPercent);
        AddLog(db, player, before, "CREW", 0, result.Summary);
        await db.SaveChangesAsync(ct);
        return Results.Ok(result);
    }
    catch (GameRuleException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).RequireAuthorization();

app.MapPost("/api/game/crew/hire", async (
    CrewRequest request,
    CurrentPlayerService current,
    GameDbContext db,
    EconomyService economy,
    CancellationToken ct) =>
{
    var player = await current.GetAsync(ct);
    if (player is null) return Results.Unauthorized();

    var before = Snapshot(player);
    try
    {
        var result = economy.HireCrew(player, request.Role, request.Quantity);
        AddLog(db, player, before, "CREW", 0, result.Summary);
        await db.SaveChangesAsync(ct);
        return Results.Ok(result);
    }
    catch (GameRuleException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).RequireAuthorization();

app.MapPost("/api/game/crew/fire", async (
    CrewRequest request,
    CurrentPlayerService current,
    GameDbContext db,
    EconomyService economy,
    CancellationToken ct) =>
{
    var player = await current.GetAsync(ct);
    if (player is null) return Results.Unauthorized();

    var before = Snapshot(player);
    try
    {
        var result = economy.FireCrew(player, request.Role, request.Quantity);
        AddLog(db, player, before, "CREW", 0, result.Summary);
        await db.SaveChangesAsync(ct);
        return Results.Ok(result);
    }
    catch (GameRuleException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).RequireAuthorization();

app.MapPost("/api/game/hideout/recover", async (
    MoraleRecoveryRequest request,
    CurrentPlayerService current,
    GameDbContext db,
    TurnService turns,
    EconomyService economy,
    CancellationToken ct) =>
{
    var player = await current.GetAsync(ct);
    if (player is null) return Results.Unauthorized();

    var now = DateTime.UtcNow;
    turns.Refresh(player, now);
    var before = Snapshot(player);
    try
    {
        var result = economy.RecoverCrewMorale(player, request.Strategy);
        AddLog(db, player, before, "HIDEOUT", result.Breakdown is not null && result.Breakdown.TryGetValue("turnsSpent", out var turnsSpent)
            ? Convert.ToInt32(turnsSpent)
            : 0,
            result.Summary);
        await db.SaveChangesAsync(ct);
        return Results.Ok(result);
    }
    catch (GameRuleException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).RequireAuthorization();

app.MapPost("/api/game/hideout/upgrade", async (
    HideoutUpgradeRequest request,
    CurrentPlayerService current,
    GameDbContext db,
    HideoutService hideouts,
    CancellationToken ct) =>
{
    var player = await current.GetAsync(ct);
    if (player is null) return Results.Unauthorized();

    var before = Snapshot(player);
    try
    {
        var result = hideouts.Upgrade(player, request.Room);
        AddLog(db, player, before, "HIDEOUT", 0, result.Summary);
        await db.SaveChangesAsync(ct);
        return Results.Ok(result);
    }
    catch (GameRuleException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).RequireAuthorization();

app.MapGet("/api/game/leaderboard", async (
    GameDbContext db,
    EconomyService economy,
    CancellationToken ct) =>
{
    // Ordered and capped by the database: rank is the row's position in the global order.
    var top = await db.Players.AsNoTracking()
        .OrderByDescending(economy.NetWorthExpression)
        .ThenBy(x => x.CreatedAtUtc)
        .Take(50)
        .ToListAsync(ct);
    var result = top
        .Select((x, index) => new LeaderboardEntryResponse(
            index + 1,
            x.Name,
            x.City,
            economy.CalculateNetWorth(x),
            x.Cash,
            x.BankCash,
            x.Pimps,
            x.Hoes,
            x.Thugs))
        .ToList();
    return Results.Ok(result);
}).RequireAuthorization();

app.MapGet("/api/game/targets", async (
    string? query,
    CurrentPlayerService current,
    GameDbContext db,
    EconomyService economy,
    IOptionsSnapshot<GameOptions> gameOptions,
    CombatMissionService combatMissions,
    CombatResolutionService combatResolver,
    CancellationToken ct) =>
{
    var player = await current.GetAsync(ct);
    if (player is null) return Results.Unauthorized();

    var normalizedQuery = query?.Trim() ?? string.Empty;
    var now = DateTime.UtcNow;
    await combatResolver.ResolveDueAsync(now, ct);
    var laneReadyAt = await combatMissions.LaneReadyAtUtcAsync(player.Id, now, ct);

    var candidates = db.Players
        .Include(x => x.Account)
        .AsNoTracking()
        .Where(x => x.Id != player.Id);
    if (normalizedQuery.Length > 0)
    {
        // ILIKE keeps the search case-insensitive the way the old in-memory Contains was.
        var pattern = ToLikePattern(normalizedQuery);
        candidates = candidates.Where(x =>
            EF.Functions.ILike(x.Name, pattern, "\\")
            || EF.Functions.ILike(x.City, pattern, "\\"));
    }

    var page = await candidates
        .OrderByDescending(economy.NetWorthExpression)
        .ThenBy(x => x.CreatedAtUtc)
        .Take(20)
        .ToListAsync(ct);
    var ranked = await RankPageAsync(page, db, economy, ct);
    var targets = ranked
        .Select(x => ToTargetResponse(x, now, player, gameOptions.Value, viewerLaneReadyAtUtc: laneReadyAt))
        .ToList();

    return Results.Ok(targets);
}).RequireAuthorization();

app.MapGet("/api/game/players/{playerId:guid}/profile", async (
    Guid playerId,
    CurrentPlayerService current,
    GameDbContext db,
    EconomyService economy,
    IOptionsSnapshot<GameOptions> gameOptions,
    CombatMissionService combatMissions,
    CombatResolutionService combatResolver,
    CancellationToken ct) =>
{
    var viewer = await current.GetAsync(ct);
    if (viewer is null) return Results.Unauthorized();

    var now = DateTime.UtcNow;
    await combatResolver.ResolveDueAsync(now, ct);
    var laneReadyAt = await combatMissions.LaneReadyAtUtcAsync(viewer.Id, now, ct);
    var subject = await db.Players
        .Include(x => x.Account)
        .AsNoTracking()
        .SingleOrDefaultAsync(x => x.Id == playerId, ct);
    if (subject is null) return Results.NotFound(new { error = "Player not found." });

    var subjectNetWorth = economy.CalculateNetWorth(subject);
    var subjectRank = await db.Players.AsNoTracking()
        .CountAsync(economy.RanksAbove(subjectNetWorth, subject.CreatedAtUtc), ct) + 1;
    var target = new RankedPlayer(subject, subjectNetWorth, subjectRank);

    var activity = await db.ActionLogs.AsNoTracking()
        .Where(x => x.PlayerId == playerId && x.Action != "ADMIN" && x.Action != "STORE")
        .OrderByDescending(x => x.CreatedAtUtc)
        .ThenByDescending(x => x.Id)
        .Take(8)
        .Select(x => new ActivityResponse(
            x.Id,
            x.Action,
            x.Summary,
            x.TurnsSpent,
            x.CashDelta,
            x.BankDelta,
            x.CreatedAtUtc))
        .ToListAsync(ct);
    var combatSince = now.AddDays(-1);
    var recentAttacksMade = await db.CombatLogs.AsNoTracking()
        .CountAsync(x => x.AttackerId == playerId && x.CreatedAtUtc >= combatSince, ct);
    var recentDefenses = await db.CombatLogs.AsNoTracking()
        .CountAsync(x => x.DefenderId == playerId && x.CreatedAtUtc >= combatSince, ct);

    return Results.Ok(ToProfileResponse(target, activity, now, viewer, gameOptions.Value, recentAttacksMade, recentDefenses, laneReadyAt));
}).RequireAuthorization();

app.MapGet("/api/game/combat/logs", async (
    CurrentPlayerService current,
    GameDbContext db,
    CombatResolutionService combatResolver,
    CancellationToken ct) =>
{
    var player = await current.GetAsync(ct);
    if (player is null) return Results.Unauthorized();

    await combatResolver.ResolveDueAsync(DateTime.UtcNow, ct);
    var combatLogs = await db.CombatLogs.AsNoTracking()
        .Include(x => x.Attacker)
        .Include(x => x.Defender)
        .Where(x => x.AttackerId == player.Id || x.DefenderId == player.Id)
        .OrderByDescending(x => x.CreatedAtUtc)
        .ThenByDescending(x => x.Id)
        .Take(100)
        .ToListAsync(ct);

    var logs = combatLogs
        .DistinctBy(CombatLogDedupeKey)
        .Take(30)
        .Select(ToCombatLogResponse)
        .ToList();
    return Results.Ok(logs);
}).RequireAuthorization();

app.MapGet("/api/game/combat/missions", async (
    CurrentPlayerService current,
    CombatMissionService combatMissions,
    CombatResolutionService combatResolver,
    CancellationToken ct) =>
{
    var player = await current.GetAsync(ct);
    if (player is null) return Results.Unauthorized();

    await combatResolver.ResolveDueAsync(DateTime.UtcNow, ct);
    var missions = await combatMissions.VisibleMissions(player.Id).ToListAsync(ct);
    return Results.Ok(missions.Select(ToCombatMissionResponse).ToList());
}).RequireAuthorization();

app.MapPost("/api/game/combat/attack", async (
    CombatAttackRequest request,
    CurrentPlayerService current,
    GameDbContext db,
    TurnService turns,
    CombatMissionService combatMissions,
    CombatResolutionService combatResolver,
    CancellationToken ct) =>
{
    var attacker = await current.GetAsync(ct);
    if (attacker is null) return Results.Unauthorized();

    var now = DateTime.UtcNow;
    await combatResolver.ResolveDueAsync(now, ct);

    var defender = await db.Players
        .Include(x => x.Account)
        .SingleOrDefaultAsync(x => x.Id == request.DefenderId, ct);
    if (defender is null) return Results.NotFound(new { error = "Target not found." });

    turns.Refresh(attacker, now);
    var before = Snapshot(attacker);
    try
    {
        var mission = await combatMissions.LaunchAsync(attacker, defender, request, now, ct);
        AddLog(db, attacker, before, "ATTACK", mission.TurnsSpent, mission.Summary);
        await db.SaveChangesAsync(ct);
        return Results.Ok(new ActionResultResponse(mission.Summary, attacker.Turns, new Dictionary<string, object?>
        {
            ["missionId"] = mission.Id,
            ["status"] = mission.Status,
            ["assignedPimps"] = mission.AssignedPimps,
            ["assignedThugs"] = mission.AssignedThugs,
            ["assignedWeapons"] = mission.AssignedWeapons,
            ["arrivesAtUtc"] = mission.ArrivesAtUtc
        }));
    }
    catch (GameRuleException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).RequireAuthorization();

app.MapPost("/api/game/combat/missions/{missionId:long}/cancel", async (
    long missionId,
    CurrentPlayerService current,
    CombatMissionService combatMissions,
    CombatResolutionService combatResolver,
    CancellationToken ct) =>
{
    var attacker = await current.GetAsync(ct);
    if (attacker is null) return Results.Unauthorized();

    var now = DateTime.UtcNow;
    await combatResolver.ResolveDueAsync(now, ct);

    try
    {
        var result = await combatMissions.CancelAsync(attacker, missionId, now, ct);
        var mission = result.Mission;
        return Results.Ok(new ActionResultResponse(mission.Summary, attacker.Turns, new Dictionary<string, object?>
        {
            ["missionId"] = mission.Id,
            ["status"] = mission.Status,
            ["outcome"] = mission.Outcome,
            ["cancelCashCost"] = result.Cost
        }));
    }
    catch (GameRuleException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).RequireAuthorization();

app.MapGet("/api/world/news", async (
    GameDbContext db,
    CombatResolutionService combatResolver,
    CancellationToken ct) =>
{
    await combatResolver.ResolveDueAsync(DateTime.UtcNow, ct);
    var result = await db.ActionLogs.AsNoTracking()
        .Where(x => x.Action != "ADMIN" && x.Action != "STORE")
        .OrderByDescending(x => x.CreatedAtUtc)
        .ThenByDescending(x => x.Id)
        .Take(30)
        .Select(x => new WorldNewsEntryResponse(
            x.Id,
            x.Player.Name,
            x.Player.City,
            x.Action,
            x.Summary,
            x.TurnsSpent,
            x.CreatedAtUtc))
        .ToListAsync(ct);

    return Results.Ok(result);
}).RequireAuthorization();

app.MapGet("/api/admin/overview", async (
    CurrentPlayerService current,
    GameDbContext db,
    EconomyService economy,
    IOptionsSnapshot<GameOptions> gameOptions,
    IOptions<BotAutomationOptions> botOptions,
    BotAutomationState botAutomation,
    CancellationToken ct) =>
{
    var admin = await current.GetAsync(ct);
    if (admin is null) return Results.Unauthorized();
    if (!admin.Account.IsAdmin) return Results.Forbid();

    var totalAccounts = await db.Accounts.AsNoTracking().CountAsync(ct);
    var adminAccounts = await db.Accounts.AsNoTracking().CountAsync(x => x.IsAdmin, ct);
    var botAccounts = await db.Accounts.AsNoTracking().CountAsync(x => x.IsBot, ct);
    // Totalled by the database rather than by loading every player row.
    var totals = await db.Players.AsNoTracking()
        .GroupBy(x => 1)
        .Select(g => new
        {
            Players = g.Count(),
            Cash = g.Sum(x => x.Cash),
            BankCash = g.Sum(x => x.BankCash),
            Turns = g.Sum(x => x.Turns),
            HoeMorale = g.Average(x => x.HoeHappiness),
            ThugMorale = g.Average(x => x.ThugHappiness)
        })
        .SingleOrDefaultAsync(ct);
    var totalNetWorth = await db.Players.AsNoTracking().SumAsync(economy.NetWorthExpression, ct);

    return Results.Ok(new AdminOverviewResponse(
        DateTime.UtcNow,
        totalAccounts,
        adminAccounts,
        botAccounts,
        totals?.Players ?? 0,
        totals?.Cash ?? 0,
        totals?.BankCash ?? 0,
        (totals?.Cash ?? 0) + (totals?.BankCash ?? 0),
        totalNetWorth,
        totals?.Turns ?? 0,
        totals is null ? 0 : Math.Round(totals.HoeMorale, 2),
        totals is null ? 0 : Math.Round(totals.ThugMorale, 2),
        new BotAutomationStatusResponse(
            botAutomation.Enabled,
            Math.Clamp(botOptions.Value.TickSeconds, 15, 3600),
            Math.Clamp(botOptions.Value.RoundsPerTick, 1, 10)),
        gameOptions.Value));
}).RequireAuthorization();

app.MapPost("/api/admin/bots/seed", async (
    AdminSeedBotsRequest request,
    CurrentPlayerService current,
    GameDbContext db,
    IOptionsSnapshot<GameOptions> gameOptions,
    HideoutService hideouts,
    PimpRoster pimps,
    CancellationToken ct) =>
{
    var admin = await current.GetAsync(ct);
    if (admin is null) return Results.Unauthorized();
    if (!admin.Account.IsAdmin) return Results.Forbid();

    var templates = BotTemplates();
    var hideoutConfig = gameOptions.Value.Hideout;
    var maxStorageLevel = hideoutConfig.Storage.Count == 0 ? 1 : hideoutConfig.Storage.Max(x => x.Level);
    var maxSafeLevel = hideoutConfig.Safe.Count == 0 ? 1 : hideoutConfig.Safe.Max(x => x.Level);
    var count = Math.Clamp(request.Count, 1, templates.Count);
    var now = DateTime.UtcNow;
    // Only the seed templates can collide, so ask about those names instead of reading every
    // account and player. lower() keeps the match case-insensitive, as the hash sets below are.
    var templateUsernames = templates.Select(x => x.Username.ToLowerInvariant()).ToList();
    var templateNames = templates.Select(x => x.Name.ToLowerInvariant()).ToList();
    var existingUsernames = (await db.Accounts.AsNoTracking()
            .Where(x => templateUsernames.Contains(x.Username.ToLower()))
            .Select(x => x.Username)
            .ToListAsync(ct))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    var existingNames = (await db.Players.AsNoTracking()
            .Where(x => templateNames.Contains(x.Name.ToLower()))
            .Select(x => x.Name)
            .ToListAsync(ct))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    var created = 0;

    foreach (var template in templates)
    {
        if (created >= count) break;
        if (existingUsernames.Contains(template.Username) || existingNames.Contains(template.Name))
            continue;

        var player = CreateBotPlayer(template, gameOptions.Value, now, maxStorageLevel, maxSafeLevel);
        hideouts.ClampToCapacity(player);
        pimps.Reconcile(player, now);
        db.Accounts.Add(player.Account);
        db.Players.Add(player);
        db.ActionLogs.Add(new GameActionLog
        {
            Player = player,
            Action = "START",
            Summary = $"{player.Name} surfaced in {player.City} with a crew of {player.Pimps} pimp(s), {player.Hoes} hoe(s), and {player.Thugs} thug(s).",
            CashDelta = player.Cash,
            BankDelta = player.BankCash,
            PimpsDelta = player.Pimps,
            HoesDelta = player.Hoes,
            ThugsDelta = player.Thugs,
            CondomsDelta = player.Condoms,
            BeerDelta = player.Beer,
            WeaponsDelta = player.Weapons,
            WeedDelta = player.Weed,
            CokeDelta = player.Coke,
            CreatedAtUtc = now
        });

        existingUsernames.Add(template.Username);
        existingNames.Add(template.Name);
        created++;
    }

    await db.SaveChangesAsync(ct);
    var summary = created == 0
        ? "No new AI rivals were available to seed."
        : $"Seeded {created:N0} AI rival{(created == 1 ? string.Empty : "s")} for 0.2.0 testing.";

    return Results.Ok(new ActionResultResponse(summary, admin.Turns, new Dictionary<string, object?>
    {
        ["requested"] = request.Count,
        ["target"] = count,
        ["created"] = created
    }));
}).RequireAuthorization();

app.MapPost("/api/admin/bots/run", async (
    AdminRunBotsRequest request,
    CurrentPlayerService current,
    BotSimulationService bots,
    CancellationToken ct) =>
{
    var admin = await current.GetAsync(ct);
    if (admin is null) return Results.Unauthorized();
    if (!admin.Account.IsAdmin) return Results.Forbid();

    var result = await bots.RunAsync(request.Rounds, ct);
    var summary = result.TotalBots == 0
        ? "No AI rivals exist yet. Seed AI players first."
        : $"Ran {result.Rounds:N0} AI round{(result.Rounds == 1 ? string.Empty : "s")}: {result.Actions:N0} action{(result.Actions == 1 ? string.Empty : "s")} across {result.ActiveBots:N0} active rival{(result.ActiveBots == 1 ? string.Empty : "s")}.";

    return Results.Ok(new ActionResultResponse(summary, admin.Turns, new Dictionary<string, object?>
    {
        ["totalBots"] = result.TotalBots,
        ["activeBots"] = result.ActiveBots,
        ["activeBotRounds"] = result.ActiveBotRounds,
        ["actions"] = result.Actions,
        ["rounds"] = result.Rounds
    }));
}).RequireAuthorization();

app.MapPut("/api/admin/bots/automation", async (
    AdminBotAutomationRequest request,
    CurrentPlayerService current,
    BotAutomationState botAutomation,
    IOptions<BotAutomationOptions> botOptions,
    CancellationToken ct) =>
{
    var admin = await current.GetAsync(ct);
    if (admin is null) return Results.Unauthorized();
    if (!admin.Account.IsAdmin) return Results.Forbid();

    botAutomation.SetEnabled(request.Enabled);
    var summary = request.Enabled ? "Automatic AI is now on." : "Automatic AI is now off.";

    return Results.Ok(new ActionResultResponse(summary, admin.Turns, new Dictionary<string, object?>
    {
        ["enabled"] = botAutomation.Enabled,
        ["tickSeconds"] = Math.Clamp(botOptions.Value.TickSeconds, 15, 3600),
        ["roundsPerTick"] = Math.Clamp(botOptions.Value.RoundsPerTick, 1, 10)
    }));
}).RequireAuthorization();

// ----- Admin: player administration -----

app.MapGet("/api/admin/players", async (
    string? query,
    CurrentPlayerService current,
    AdminService admins,
    EconomyService economy,
    CancellationToken ct) =>
{
    var admin = await current.GetAsync(ct);
    if (admin is null) return Results.Unauthorized();
    if (!admin.Account.IsAdmin) return Results.Forbid();

    // Capped: an admin needs the first page of matches, not the whole table.
    var matches = await admins.SearchPlayers(query)
        .OrderBy(x => x.Name)
        .Take(50)
        .ToListAsync(ct);
    return Results.Ok(matches.Select(x => ToAdminSummary(x, economy)).ToList());
}).RequireAuthorization();

app.MapGet("/api/admin/players/{playerId:guid}", async (
    Guid playerId,
    CurrentPlayerService current,
    GameDbContext db,
    AdminService admins,
    EconomyService economy,
    HideoutService hideouts,
    PimpRoster pimps,
    CancellationToken ct) =>
{
    var admin = await current.GetAsync(ct);
    if (admin is null) return Results.Unauthorized();
    if (!admin.Account.IsAdmin) return Results.Forbid();

    var target = await admins.FindPlayerAsync(playerId, ct);
    if (target is null) return Results.NotFound(new { error = "Player not found." });

    var activity = await db.ActionLogs.AsNoTracking()
        .Where(x => x.PlayerId == playerId)
        .OrderByDescending(x => x.CreatedAtUtc)
        .ThenByDescending(x => x.Id)
        .Take(20)
        .Select(x => new ActivityResponse(x.Id, x.Action, x.Summary, x.TurnsSpent, x.CashDelta, x.BankDelta, x.CreatedAtUtc))
        .ToListAsync(ct);
    var audit = await admins.AuditTrail()
        .Where(x => x.TargetPlayerId == playerId)
        .Take(20)
        .Select(x => new AdminAuditEntryResponse(x.Id, x.ActorUsername, x.Action, x.TargetPlayerId, x.TargetName, x.Summary, x.Reason, x.CreatedAtUtc))
        .ToListAsync(ct);

    return Results.Ok(new AdminPlayerDetailResponse(
        ToAdminSummary(target, economy),
        target.Condoms,
        target.Beer,
        target.Weapons,
        target.Weed,
        target.Coke,
        Math.Round(target.HoeHappiness, 2),
        Math.Round(target.ThugHappiness, 2),
        target.HoeCutPercent,
        target.LastAttackAtUtc,
        target.LastAttackedAtUtc,
        target.CombatProtectionUntilUtc,
        ToHideoutResponse(target, hideouts),
        pimps.Active(target).Select(x => ToPimpResponse(x, [])).ToList(),
        activity,
        audit,
        AdminService.AdjustableResources.ToList()));
}).RequireAuthorization();

app.MapPost("/api/admin/players/{playerId:guid}/adjust", (Guid playerId, AdminAdjustRequest request, HttpContext http) =>
    AdminAction(http, playerId, (admins, actor, target, now) =>
        admins.AdjustResource(actor, target, request.Resource, request.Delta, request.Reason, now)))
    .RequireAuthorization();

app.MapPost("/api/admin/players/{playerId:guid}/morale", (Guid playerId, AdminMoraleRequest request, HttpContext http) =>
    AdminAction(http, playerId, (admins, actor, target, now) =>
        admins.SetMorale(actor, target, request.Morale, request.Reason, now)))
    .RequireAuthorization();

app.MapPost("/api/admin/players/{playerId:guid}/enforcement", (Guid playerId, AdminEnforcementRequest request, HttpContext http) =>
    AdminAction(http, playerId, (admins, actor, target, now) =>
        admins.SetEnforcement(actor, target, request.Action, request.UntilUtc, request.Reason, now)))
    .RequireAuthorization();

app.MapPost("/api/admin/players/{playerId:guid}/force-logout", (Guid playerId, AdminReasonRequest request, HttpContext http) =>
    AdminAction(http, playerId, (admins, actor, target, now) =>
        admins.ForceLogout(actor, target, request.Reason, now)))
    .RequireAuthorization();

app.MapPost("/api/admin/players/{playerId:guid}/rename", (Guid playerId, AdminRenameRequest request, HttpContext http) =>
    AdminAction(http, playerId, (admins, actor, target, now) =>
        admins.Rename(actor, target, request.Name, request.Reason, now)))
    .RequireAuthorization();

app.MapPost("/api/admin/players/{playerId:guid}/admin-rights", async (
    Guid playerId,
    AdminSetAdminRequest request,
    CurrentPlayerService current,
    GameDbContext db,
    AdminService admins,
    CancellationToken ct) =>
{
    var admin = await current.GetAsync(ct);
    if (admin is null) return Results.Unauthorized();
    if (!admin.Account.IsAdmin) return Results.Forbid();

    var target = await admins.FindPlayerAsync(playerId, ct);
    if (target is null) return Results.NotFound(new { error = "Player not found." });

    try
    {
        var summary = await admins.SetAdminAsync(admin.Account, target, request.IsAdmin, request.Reason, DateTime.UtcNow, ct);
        await db.SaveChangesAsync(ct);
        return Results.Ok(new ActionResultResponse(summary, admin.Turns));
    }
    catch (GameRuleException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).RequireAuthorization();

// ----- Live operations -----

// Readable by any signed-in player: the client needs the banner and the maintenance notice.
app.MapGet("/api/game/live-ops", async (GameDbContext db, CancellationToken ct) =>
    Results.Ok(ToLiveOpsResponse(await LiveOpsAsync(db, ct)))).RequireAuthorization();

app.MapPut("/api/admin/live-ops", async (
    AdminLiveOpsRequest request,
    CurrentPlayerService current,
    GameDbContext db,
    AdminService admins,
    CancellationToken ct) =>
{
    var admin = await current.GetAsync(ct);
    if (admin is null) return Results.Unauthorized();
    if (!admin.Account.IsAdmin) return Results.Forbid();

    var settings = await LiveOpsAsync(db, ct);
    var now = DateTime.UtcNow;
    var changes = new List<string>();

    if (request.MaintenanceMode is { } maintenance && maintenance != settings.MaintenanceMode)
    {
        settings.MaintenanceMode = maintenance;
        changes.Add(maintenance ? "maintenance on" : "maintenance off");
    }

    if (request.MaintenanceMessage is not null)
    {
        settings.MaintenanceMessage = Blank(request.MaintenanceMessage);
        changes.Add("maintenance message updated");
    }

    if (request.Announcement is not null)
    {
        settings.Announcement = Blank(request.Announcement);
        changes.Add(settings.Announcement is null ? "announcement cleared" : "announcement updated");
    }

    if (changes.Count == 0)
        return Results.BadRequest(new { error = "Nothing to change." });

    settings.UpdatedAtUtc = now;
    settings.UpdatedBy = admin.Account.Username;
    admins.Record(admin.Account, "LiveOps", null, string.Join("; ", changes), request.Reason, now);
    await db.SaveChangesAsync(ct);

    return Results.Ok(ToLiveOpsResponse(settings));
}).RequireAuthorization();

app.MapGet("/api/admin/config", async (
    CurrentPlayerService current,
    IOptionsSnapshot<GameOptions> gameOptions,
    GameOptionOverrides overrides,
    CancellationToken ct) =>
{
    var admin = await current.GetAsync(ct);
    if (admin is null) return Results.Unauthorized();
    if (!admin.Account.IsAdmin) return Results.Forbid();

    // The snapshot already has overrides layered on, so these are the values the game is really using.
    var active = overrides.Snapshot();
    var settings = GameOptionPaths.Describe(gameOptions.Value)
        .Select(x => new AdminConfigEntryResponse(
            x.Path,
            x.Type,
            x.CurrentValue,
            active.TryGetValue(x.Path, out var value) ? value : null,
            active.ContainsKey(x.Path)))
        .ToList();

    return Results.Ok(new AdminConfigResponse(overrides.Version, active.Count, settings));
}).RequireAuthorization();

app.MapPut("/api/admin/config", async (
    AdminConfigChangeRequest request,
    CurrentPlayerService current,
    GameDbContext db,
    AdminService admins,
    IOptionsSnapshot<GameOptions> gameOptions,
    GameOptionOverrides overrides,
    CancellationToken ct) =>
{
    var admin = await current.GetAsync(ct);
    if (admin is null) return Results.Unauthorized();
    if (!admin.Account.IsAdmin) return Results.Forbid();

    var path = request.Path?.Trim() ?? string.Empty;
    if (path.Length == 0)
        return Results.BadRequest(new { error = "Which setting?" });

    // Validate against a throwaway copy so a bad value never reaches the live options.
    var probe = new GameOptions();
    if (!GameOptionPaths.IsKnownPath(probe, path))
        return Results.BadRequest(new { error = $"'{path}' is not an editable setting." });

    var current_ = overrides.Snapshot().ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
    var now = DateTime.UtcNow;
    var before = GameOptionPaths.Read(gameOptions.Value, path) ?? "?";
    string summary;

    if (string.IsNullOrWhiteSpace(request.Value))
    {
        if (!current_.Remove(path))
            return Results.BadRequest(new { error = $"'{path}' is not overridden." });
        summary = $"{path}: override cleared (was {before})";
    }
    else
    {
        if (!GameOptionPaths.TryApply(probe, path, request.Value, out var error))
            return Results.BadRequest(new { error });
        current_[path] = request.Value.Trim();
        summary = $"{path}: {before} -> {request.Value.Trim()}";
    }

    var settings = await LiveOpsAsync(db, ct);
    settings.ConfigOverridesJson = current_.Count == 0 ? null : JsonSerializer.Serialize(current_);
    settings.UpdatedAtUtc = now;
    settings.UpdatedBy = admin.Account.Username;
    admins.Record(admin.Account, "Config", null, summary, request.Reason, now);
    await db.SaveChangesAsync(ct);

    // Swapping the map is what makes the change live: the next request rebinds through PostConfigure.
    overrides.Replace(current_);
    return Results.Ok(new ActionResultResponse(summary, admin.Turns));
}).RequireAuthorization();

app.MapGet("/api/admin/oversight", async (
    CurrentPlayerService current,
    GameDbContext db,
    EconomyService economy,
    CombatMissionService combatMissions,
    IOptionsSnapshot<GameOptions> gameOptions,
    CancellationToken ct) =>
{
    var admin = await current.GetAsync(ct);
    if (admin is null) return Results.Unauthorized();
    if (!admin.Account.IsAdmin) return Results.Forbid();

    var now = DateTime.UtcNow;
    var since = now.AddHours(-24);

    // Net worth is a computed expression, so the distribution is read as bare standings.
    var standings = await db.Players.AsNoTracking()
        .Select(economy.StandingExpression())
        .ToListAsync(ct);
    var worths = standings.Select(x => x.NetWorth).OrderBy(x => x).ToList();

    // Growth is approximated from logged cash deltas: the game keeps no net worth history to diff.
    var movement = await db.ActionLogs.AsNoTracking()
        .Where(x => x.CreatedAtUtc >= since)
        .GroupBy(x => x.PlayerId)
        .Select(g => new { PlayerId = g.Key, Gained = g.Sum(x => x.CashDelta + x.BankDelta), Actions = g.Count() })
        .OrderByDescending(x => x.Gained)
        .Take(8)
        .ToListAsync(ct);
    var moverIds = movement.Select(x => x.PlayerId).ToList();
    var moverPlayers = await db.Players.AsNoTracking()
        .Include(x => x.Account)
        .Where(x => moverIds.Contains(x.Id))
        .ToListAsync(ct);
    var movers = movement
        .Join(moverPlayers, m => m.PlayerId, p => p.Id, (m, p) => new AdminMoverResponse(
            p.Id, p.Name, p.Account.IsBot, economy.CalculateNetWorth(p), m.Gained, m.Actions))
        .ToList();

    var missions = await db.CombatMissions.AsNoTracking()
        .Include(x => x.Attacker)
        .Include(x => x.Defender)
        .Where(x => x.Status != "Complete")
        .OrderBy(x => x.StartedAtUtc)
        .Take(50)
        .ToListAsync(ct);
    var activeMissions = missions.Select(mission =>
    {
        var nextAt = mission.Status switch
        {
            "Traveling" => mission.ArrivesAtUtc,
            "Fighting" => mission.NextRoundAtUtc,
            "Returning" => mission.ReturnsAtUtc,
            _ => null
        };
        // Anything more than five minutes past its next event is stuck, not in flight.
        var overdue = nextAt is { } due && due < now.AddMinutes(-5);
        return new AdminMissionResponse(
            mission.Id,
            mission.Attacker.Name,
            mission.Defender.Name,
            mission.CommanderName,
            mission.Status,
            mission.Outcome,
            mission.CurrentRound,
            mission.MaxRounds,
            mission.StartedAtUtc,
            nextAt,
            overdue);
    }).ToList();

    var bots = await db.Players.AsNoTracking()
        .Include(x => x.Account)
        .Where(x => x.Account.IsBot)
        .ToListAsync(ct);
    var botIds = bots.Select(x => x.Id).ToList();
    var lastActions = await db.ActionLogs.AsNoTracking()
        .Where(x => botIds.Contains(x.PlayerId))
        .GroupBy(x => x.PlayerId)
        .Select(g => new { PlayerId = g.Key, Last = g.Max(x => x.CreatedAtUtc) })
        .ToDictionaryAsync(x => x.PlayerId, x => x.Last, ct);
    var botHealth = bots
        .Select(bot =>
        {
            var last = lastActions.TryGetValue(bot.Id, out var at) ? at : (DateTime?)null;
            return new AdminBotHealthResponse(
                bot.Id,
                bot.Name,
                BotBrain.For(bot).Name,
                economy.CalculateNetWorth(bot),
                last,
                last is { } value ? (int)Math.Max(0, (now - value).TotalMinutes) : int.MaxValue);
        })
        .OrderByDescending(x => x.MinutesIdle)
        .ToList();

    return Results.Ok(new AdminOversightResponse(
        WealthStats.Median(worths),
        worths.Count == 0 ? 0 : worths[^1],
        WealthStats.GiniPercent(worths),
        WealthStats.WealthBands(worths),
        movers,
        activeMissions,
        botHealth));
}).RequireAuthorization();

app.MapPost("/api/admin/missions/{missionId:long}/force-resolve", async (
    long missionId,
    CurrentPlayerService current,
    GameDbContext db,
    AdminService admins,
    CombatSchedule schedule,
    CombatResolutionService combatResolver,
    CancellationToken ct) =>
{
    var admin = await current.GetAsync(ct);
    if (admin is null) return Results.Unauthorized();
    if (!admin.Account.IsAdmin) return Results.Forbid();

    var mission = await db.CombatMissions
        .Include(x => x.Attacker)
        .Include(x => x.Defender)
        .SingleOrDefaultAsync(x => x.Id == missionId, ct);
    if (mission is null) return Results.NotFound(new { error = "Mission not found." });
    if (mission.Status == "Complete") return Results.BadRequest(new { error = "That mission is already complete." });

    // Pulls every timer into the past, then lets the normal resolver finish it through the usual rules
    // rather than hand-writing an outcome.
    var now = DateTime.UtcNow;
    if (mission.Status == "Traveling") mission.ArrivesAtUtc = now;
    if (mission.NextRoundAtUtc is not null) mission.NextRoundAtUtc = now;
    if (mission.ReturnsAtUtc is not null) mission.ReturnsAtUtc = now;
    admins.Record(admin.Account, "ForceResolve", mission.Attacker,
        $"mission {mission.Id} ({mission.Status}) against {mission.Defender.Name} pushed to resolve", null, now);
    await db.SaveChangesAsync(ct);

    schedule.Invalidate();
    var updates = await combatResolver.ResolveDueAsync(DateTime.UtcNow, ct);
    return Results.Ok(new ActionResultResponse($"Pushed mission {missionId} through the resolver ({updates:N0} update(s)).", admin.Turns));
}).RequireAuthorization();

app.MapGet("/api/admin/audit", async (
    CurrentPlayerService current,
    AdminService admins,
    CancellationToken ct) =>
{
    var admin = await current.GetAsync(ct);
    if (admin is null) return Results.Unauthorized();
    if (!admin.Account.IsAdmin) return Results.Forbid();

    var entries = await admins.AuditTrail()
        .Take(100)
        .Select(x => new AdminAuditEntryResponse(x.Id, x.ActorUsername, x.Action, x.TargetPlayerId, x.TargetName, x.Summary, x.Reason, x.CreatedAtUtc))
        .ToListAsync(ct);
    return Results.Ok(entries);
}).RequireAuthorization();

app.Run();

static async Task SignInAsync(HttpContext http, PlayerAccount account)
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
        new AuthenticationProperties { IsPersistent = true });
}

/// <summary>
/// Shared shape for the admin write endpoints: authorise, resolve the target, run the action, save, and
/// turn a rule violation into a 400. Keeps AdminService the only place the logic and the audit live.
/// </summary>
static async Task<IResult> AdminAction(
    HttpContext http,
    Guid playerId,
    Func<AdminService, PlayerAccount, Player, DateTime, string> action)
{
    var services = http.RequestServices;
    var current = services.GetRequiredService<CurrentPlayerService>();
    var admins = services.GetRequiredService<AdminService>();
    var db = services.GetRequiredService<GameDbContext>();

    var admin = await current.GetAsync(http.RequestAborted);
    if (admin is null) return Results.Unauthorized();
    if (!admin.Account.IsAdmin) return Results.Forbid();

    var target = await admins.FindPlayerAsync(playerId, http.RequestAborted);
    if (target is null) return Results.NotFound(new { error = "Player not found." });

    try
    {
        var summary = action(admins, admin.Account, target, DateTime.UtcNow);
        await db.SaveChangesAsync(http.RequestAborted);
        return Results.Ok(new ActionResultResponse(summary, admin.Turns));
    }
    catch (GameRuleException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}

/// <summary>
/// The single live-ops row, created on demand so a database that predates the seed still works.
/// </summary>
static async Task<GameSetting> LiveOpsAsync(GameDbContext db, CancellationToken cancellationToken)
{
    var settings = await db.GameSettings.SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);
    if (settings is not null)
        return settings;

    settings = new GameSetting { Id = 1 };
    db.GameSettings.Add(settings);
    await db.SaveChangesAsync(cancellationToken);
    return settings;
}

static LiveOpsResponse ToLiveOpsResponse(GameSetting settings)
    => new(
        settings.MaintenanceMode,
        settings.MaintenanceMessage,
        settings.Announcement,
        settings.UpdatedAtUtc,
        settings.UpdatedBy);

static string? Blank(string? value)
    => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

static AdminPlayerSummaryResponse ToAdminSummary(Player player, EconomyService economy)
    => new(
        player.Id,
        player.Name,
        player.Account.Username,
        player.City,
        player.Account.IsBot,
        player.Account.IsAdmin,
        player.Account.IsBanned,
        player.Account.SuspendedUntilUtc,
        player.Account.EnforcementReason,
        economy.CalculateNetWorth(player),
        player.Cash,
        player.BankCash,
        player.Turns,
        player.Pimps,
        player.Hoes,
        player.Thugs,
        player.CreatedAtUtc);

static PlayerSnapshot Snapshot(Player player) => new(
    player.Cash,
    player.BankCash,
    player.Pimps,
    player.Hoes,
    player.Thugs,
    player.Condoms,
    player.Beer,
    player.Weapons,
    player.Weed,
    player.Coke);

static void AddLog(
    GameDbContext db,
    Player player,
    PlayerSnapshot before,
    string action,
    int turnsSpent,
    string summary)
{
    db.ActionLogs.Add(new GameActionLog
    {
        PlayerId = player.Id,
        Action = action,
        TurnsSpent = turnsSpent,
        CashDelta = player.Cash - before.Cash,
        BankDelta = player.BankCash - before.BankCash,
        PimpsDelta = player.Pimps - before.Pimps,
        HoesDelta = player.Hoes - before.Hoes,
        ThugsDelta = player.Thugs - before.Thugs,
        CondomsDelta = player.Condoms - before.Condoms,
        BeerDelta = player.Beer - before.Beer,
        WeaponsDelta = player.Weapons - before.Weapons,
        WeedDelta = player.Weed - before.Weed,
        CokeDelta = player.Coke - before.Coke,
        Summary = summary
    });
}

static Task<CombatMission?> ActiveOutgoingMissionAsync(GameDbContext db, Guid playerId, CancellationToken cancellationToken)
    => db.CombatMissions.AsNoTracking()
        .Where(x => x.AttackerId == playerId && x.Status != "Complete")
        .OrderBy(x => x.ReturnsAtUtc ?? x.NextRoundAtUtc ?? x.ArrivesAtUtc)
        .ThenBy(x => x.Id)
        .FirstOrDefaultAsync(cancellationToken);

static string PendingAttackMessage(CombatMission mission)
{
    var nextAt = mission.Status switch
    {
        "Traveling" => mission.ArrivesAtUtc,
        "Fighting" => mission.NextRoundAtUtc,
        "Returning" => mission.ReturnsAtUtc,
        _ => null
    };
    return nextAt is { } value
        ? $"Your crew is out on an attack mission. Next update in {FormatDuration(Math.Max(0, (int)Math.Ceiling((value - DateTime.UtcNow).TotalSeconds)))}."
        : "Your crew is out on an attack mission.";
}

static string FormatDuration(int seconds)
{
    var minutes = seconds / 60;
    var remainder = seconds % 60;
    return minutes <= 0
        ? $"{seconds} second{(seconds == 1 ? string.Empty : "s")}"
        : $"{minutes}m {remainder:00}s";
}

/// <summary>
/// Assigns global ranks to an already ordered and capped page of players. Only the players who
/// outrank the weakest row on the page are read back, and only as bare standings, so an unfiltered
/// page costs about as many rows as the page itself.
/// </summary>
static async Task<List<RankedPlayer>> RankPageAsync(
    List<Player> page,
    GameDbContext db,
    EconomyService economy,
    CancellationToken cancellationToken)
{
    if (page.Count == 0)
        return [];

    var standings = page
        .Select(x => new PlayerStanding(economy.CalculateNetWorth(x), x.CreatedAtUtc))
        .ToList();
    var weakest = standings[^1];
    var contenders = await db.Players.AsNoTracking()
        .Where(economy.RanksAbove(weakest.NetWorth, weakest.CreatedAtUtc))
        .Select(economy.StandingExpression())
        .ToListAsync(cancellationToken);

    return page
        .Select((x, index) => new RankedPlayer(
            x,
            standings[index].NetWorth,
            EconomyService.RankOf(standings[index], contenders)))
        .ToList();
}

// Escapes a search term so ILIKE treats its wildcards as literal characters. Callers pass "\" as
// the ESCAPE character to match.
static string ToLikePattern(string query)
    => $"%{query.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";

static PlayerTargetResponse ToTargetResponse(RankedPlayer ranked, DateTime nowUtc, Player? viewer, GameOptions options, int recentAttacksMade = 0, int recentDefenses = 0, DateTime? viewerLaneReadyAtUtc = null)
{
    var player = ranked.Player;
    return new PlayerTargetResponse(
        player.Id,
        player.Name,
        player.City,
        player.Account.IsBot,
        player.Account.IsBot ? BotBrain.For(player).Name : null,
        ranked.Rank,
        ranked.NetWorth,
        player.Pimps,
        player.Hoes,
        player.Thugs,
        player.Weapons,
        AverageMorale(player),
        ToCombatReadiness(player),
        ToCombatStatus(player, nowUtc, viewer, options, recentAttacksMade, recentDefenses, viewerLaneReadyAtUtc));
}

static PlayerProfileResponse ToProfileResponse(
    RankedPlayer ranked,
    IReadOnlyList<ActivityResponse> publicActivity,
    DateTime nowUtc,
    Player? viewer,
    GameOptions options,
    int recentAttacksMade,
    int recentDefenses,
    DateTime? viewerLaneReadyAtUtc)
{
    var player = ranked.Player;
    return new PlayerProfileResponse(
        player.Id,
        player.Name,
        player.City,
        player.Account.IsBot,
        player.Account.IsBot ? BotBrain.For(player).Name : null,
        ranked.Rank,
        ranked.NetWorth,
        player.Cash,
        player.BankCash,
        player.Pimps,
        player.Hoes,
        player.Thugs,
        player.Weapons,
        player.Weed,
        player.Coke,
        Math.Round(player.HoeHappiness, 2),
        Math.Round(player.ThugHappiness, 2),
        AverageMorale(player),
        ToCombatReadiness(player),
        ToCombatStatus(player, nowUtc, viewer, options, recentAttacksMade, recentDefenses, viewerLaneReadyAtUtc),
        publicActivity);
}

static CombatReadinessResponse ToCombatReadiness(Player player)
{
    var armedThugs = Math.Min(player.Weapons, player.Thugs);
    var uncoveredThugs = Math.Max(0, player.Thugs - player.Weapons);
    var weaponCoverage = player.Thugs == 0 ? 100 : Math.Round(armedThugs * 100.0 / player.Thugs, 2);
    var averageMorale = AverageMorale(player);
    var attackPower = Math.Max(1, player.Thugs * 12 + armedThugs * 8 + player.Pimps * 2 + (int)Math.Round(averageMorale / 2));
    var defensePower = Math.Max(1, player.Thugs * 14 + armedThugs * 10 + player.Pimps * 3 + (int)Math.Round(averageMorale));
    var riskBand = (averageMorale, weaponCoverage, uncoveredThugs) switch
    {
        (< 35, _, _) => "Fragile",
        (_, < 50, _) => "Exposed",
        (_, _, > 0) => "Underarmed",
        (>= 80, >= 90, 0) => "Ready",
        _ => "Mixed"
    };

    return new CombatReadinessResponse(
        attackPower,
        defensePower,
        armedThugs,
        uncoveredThugs,
        weaponCoverage,
        averageMorale,
        riskBand);
}

static HideoutResponse ToHideoutResponse(Player player, HideoutService hideouts)
{
    var capacity = hideouts.CapacityFor(player.Hideout);
    return new HideoutResponse(
        capacity.TierName,
        capacity.Tier,
        capacity.StorageLevel,
        capacity.SafeLevel,
        capacity.WeedLabLevel,
        capacity.CokeLabLevel,
        capacity.MaxPimps,
        capacity.MaxHoes,
        capacity.MaxThugs,
        capacity.MaxCash,
        capacity.MaxCondoms,
        capacity.MaxBeer,
        capacity.MaxWeapons,
        capacity.MaxWeed,
        capacity.MaxCoke,
        hideouts.ProductionYieldBonusPercent(player.Hideout, "weed"),
        hideouts.ProductionYieldBonusPercent(player.Hideout, "coke"),
        hideouts.NextUpgradeCost(player.Hideout, "storage"),
        hideouts.NextUpgradeCost(player.Hideout, "safe"),
        hideouts.NextUpgradeCost(player.Hideout, "weedlab"),
        hideouts.NextUpgradeCost(player.Hideout, "cokelab"));
}

static PimpResponse ToPimpResponse(Pimp pimp, IReadOnlyCollection<long> commandingPimpIds)
    => new(
        pimp.Id,
        pimp.Name,
        pimp.Specialty,
        pimp.BonusPercent,
        Math.Round(pimp.Loyalty, 2),
        pimp.MissionsLed,
        pimp.Victories,
        commandingPimpIds.Contains(pimp.Id),
        pimp.HiredAtUtc,
        pimp.LostAtUtc,
        pimp.LostReason);

static CombatCrewResponse ToCombatCrewResponse(CombatCommitment commitment)
    => new(
        commitment.CommittedPimps,
        commitment.CommittedThugs,
        commitment.CommittedWeapons,
        commitment.AvailablePimps,
        commitment.AvailableThugs,
        commitment.AvailableWeapons,
        commitment.ActiveAttackMissions,
        commitment.MaxActiveAttackMissions);

static double AverageMorale(Player player)
    => Math.Round((player.HoeHappiness + player.ThugHappiness) / 2, 2);

static CombatStatusResponse ToCombatStatus(
    Player player,
    DateTime nowUtc,
    Player? viewer,
    GameOptions options,
    int recentAttacksMade = 0,
    int recentDefenses = 0,
    DateTime? viewerLaneReadyAtUtc = null)
{
    var combat = options.Combat;
    var isProtected = player.CombatProtectionUntilUtc is { } protectionUntil && protectionUntil > nowUtc;
    var isOnCooldown = viewerLaneReadyAtUtc is { } readyAt && readyAt > nowUtc;
    var hasViewer = viewer is not null;
    var isSelf = viewer?.Id == player.Id;
    var hasTurns = viewer?.Turns >= combat.AttackTurnCost;
    var eligibility = isSelf
        ? "Self"
        : isProtected
            ? "Protected"
            : isOnCooldown
                ? "Cooldown"
                : !hasTurns
                    ? "Need Turns"
                    : "Eligible";

    return new CombatStatusResponse(
        isProtected,
        player.CombatProtectionUntilUtc,
        player.LastAttackAtUtc,
        player.LastAttackedAtUtc,
        viewerLaneReadyAtUtc,
        hasViewer && !isSelf && !isProtected && !isOnCooldown && hasTurns,
        combat.AttackTurnCost,
        recentAttacksMade,
        recentDefenses,
        eligibility);
}

static CombatLogResponse ToCombatLogResponse(CombatLog log)
    => new(
        log.Id,
        log.AttackerId,
        log.Attacker.Name,
        log.DefenderId,
        log.Defender.Name,
        log.Outcome,
        log.Summary,
        log.TurnsSpent,
        log.AttackerPower,
        log.DefenderPower,
        log.CashStolen,
        log.WeedStolen,
        log.CokeStolen,
        log.AttackerPimpsLost,
        log.AttackerHoesLost,
        log.AttackerThugsLost,
        log.AttackerWeaponsLost,
        log.DefenderPimpsLost,
        log.DefenderHoesLost,
        log.DefenderThugsLost,
        log.DefenderWeaponsLost,
        log.DefenderProtectionUntilUtc,
        log.ResolvesAtUtc,
        log.ResolvedAtUtc,
        log.CreatedAtUtc);

static CombatMissionResponse ToCombatMissionResponse(CombatMission mission)
    => new(
        mission.Id,
        mission.AttackerId,
        mission.Attacker.Name,
        mission.DefenderId,
        mission.Defender.Name,
        mission.Status,
        mission.Outcome,
        mission.Summary,
        mission.TurnsSpent,
        mission.AssignedPimps,
        mission.CommanderName,
        mission.CommanderBonusPercent,
        mission.AssignedThugs,
        mission.AssignedWeapons,
        mission.RemainingAttackers,
        mission.RemainingWeapons,
        Math.Round(mission.AttackerMorale, 2),
        Math.Round(mission.DefenderMorale, 2),
        mission.CurrentRound,
        mission.MaxRounds,
        mission.AttackerPower,
        mission.DefenderPower,
        mission.CashStolen,
        mission.WeedStolen,
        mission.CokeStolen,
        mission.StartedAtUtc,
        mission.ArrivesAtUtc,
        mission.NextRoundAtUtc,
        mission.ReturnsAtUtc,
        mission.CompletedAtUtc,
        mission.DefenderProtectionUntilUtc,
        CombatMissionService.CanCancel(mission),
        CombatMissionService.CancelCashCost(mission),
        mission.Events
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .DistinctBy(CombatMissionEventDedupeKey)
            .Take(8)
            .Select(ToCombatMissionEventResponse)
            .ToList());

static (Guid AttackerId, Guid DefenderId, string Outcome, string Summary, DateTime CreatedAtUtc) CombatLogDedupeKey(CombatLog log)
    => (log.AttackerId, log.DefenderId, log.Outcome, log.Summary, log.CreatedAtUtc);

static (int Round, string Kind, string Summary, DateTime CreatedAtUtc) CombatMissionEventDedupeKey(CombatMissionEvent entry)
    => (entry.Round, entry.Kind, entry.Summary, entry.CreatedAtUtc);

static CombatMissionEventResponse ToCombatMissionEventResponse(CombatMissionEvent entry)
    => new(
        entry.Id,
        entry.Round,
        entry.Kind,
        entry.Summary,
        Math.Round(entry.AttackRoll, 2),
        Math.Round(entry.DefenseRoll, 2),
        Math.Round(entry.AttackerMorale, 2),
        Math.Round(entry.DefenderMorale, 2),
        entry.AttackerThugsLost,
        entry.DefenderThugsLost,
        entry.AttackerWeaponsLost,
        entry.DefenderWeaponsLost,
        entry.CreatedAtUtc);

static Player CreateBotPlayer(BotTemplate template, GameOptions options, DateTime createdAtUtc, int maxStorageLevel, int maxSafeLevel)
{
    var account = new PlayerAccount
    {
        Username = template.Username,
        PasswordHash = "BOT_ACCOUNT_DISABLED",
        IsBot = true,
        CreatedAtUtc = createdAtUtc
    };

    var player = new Player
    {
        Account = account,
        Name = template.Name,
        City = template.City,
        Cash = options.StartingCash + template.CashBonus,
        BankCash = options.StartingBankCash + template.BankCash,
        Turns = Math.Min(options.MaxTurns, options.StartingTurns + template.TurnBonus),
        Pimps = options.StartingPimps + template.Pimps,
        Hoes = options.StartingHoes + template.Hoes,
        Thugs = options.StartingThugs + template.Thugs,
        HoeCutPercent = template.HoeCutPercent,
        HoeHappiness = template.HoeHappiness,
        ThugHappiness = template.ThugHappiness,
        Condoms = options.StartingCondoms + template.Condoms,
        Beer = options.StartingBeer + template.Beer,
        Weapons = options.StartingWeapons + template.Weapons,
        Weed = template.Weed,
        Coke = template.Coke,
        LastTurnUpdateUtc = createdAtUtc,
        CreatedAtUtc = createdAtUtc
    };
    // Rivals are seeded as established operations, then clamped so they obey the same limits players do.
    player.Hideout = new Hideout
    {
        Player = player,
        StorageLevel = maxStorageLevel,
        SafeLevel = maxSafeLevel,
        CreatedAtUtc = createdAtUtc
    };
    return player;
}

static IReadOnlyList<BotTemplate> BotTemplates() =>
[
    new("ai_silk_ledger", "Silk Ledger", "New York", 12_000, 6_000, 40, 2, 24, 8, 180, 120, 10, 100, 35, 35, 92, 88),
    new("ai_brass_knox", "Brass Knox", "Chicago", 9_000, 8_500, 25, 3, 18, 12, 140, 160, 16, 55, 20, 30, 86, 94),
    new("ai_velvet_bishop", "Velvet Bishop", "Miami", 16_000, 2_000, 55, 1, 32, 6, 230, 90, 8, 145, 42, 40, 96, 82),
    new("ai_night_audit", "Night Audit", "Detroit", 7_500, 11_000, 20, 4, 14, 16, 110, 210, 20, 40, 18, 25, 80, 91),
    new("ai_lucky_voss", "Lucky Voss", "Los Angeles", 20_000, 5_000, 70, 2, 28, 10, 220, 150, 13, 180, 60, 35, 89, 87),
    new("ai_ruby_ledger", "Ruby Ledger", "New York", 5_500, 18_000, 15, 5, 20, 18, 160, 250, 24, 35, 12, 30, 84, 95),
    new("ai_metro_saint", "Metro Saint", "Chicago", 11_000, 4_500, 35, 2, 22, 9, 170, 130, 12, 95, 28, 35, 90, 86),
    new("ai_grit_baron", "Grit Baron", "Detroit", 4_000, 22_000, 10, 6, 16, 22, 120, 280, 28, 20, 8, 30, 78, 96),
    new("ai_crown_vale", "Crown Vale", "Miami", 24_000, 1_500, 80, 1, 36, 7, 260, 110, 9, 220, 75, 45, 98, 84),
    new("ai_switch_lane", "Switch Lane", "Los Angeles", 8_000, 7_000, 30, 3, 19, 11, 150, 145, 14, 85, 25, 30, 87, 89),
    new("ai_ace_borough", "Ace Borough", "New York", 15_000, 10_000, 45, 4, 25, 14, 210, 200, 18, 120, 44, 35, 91, 92),
    new("ai_dollar_wren", "Dollar Wren", "Chicago", 6_500, 14_000, 18, 2, 17, 13, 130, 175, 17, 48, 16, 30, 82, 90),
    new("ai_queen_mercer", "Queen Mercer", "Miami", 18_000, 9_000, 60, 3, 30, 12, 240, 190, 16, 165, 58, 40, 95, 90),
    new("ai_brick_falcon", "Brick Falcon", "Detroit", 10_000, 12_000, 28, 5, 21, 20, 170, 260, 26, 70, 30, 30, 83, 97),
    new("ai_halo_vice", "Halo Vice", "Los Angeles", 22_000, 3_000, 75, 2, 34, 9, 255, 135, 12, 205, 70, 45, 97, 86)
];

internal sealed record PlayerSnapshot(
    long Cash,
    long BankCash,
    int Pimps,
    int Hoes,
    int Thugs,
    int Condoms,
    int Beer,
    int Weapons,
    int Weed,
    int Coke);

internal sealed record RankedPlayer(Player Player, long NetWorth, int Rank);

internal sealed record BotTemplate(
    string Username,
    string Name,
    string City,
    long CashBonus,
    long BankCash,
    int TurnBonus,
    int Pimps,
    int Hoes,
    int Thugs,
    int Condoms,
    int Beer,
    int Weapons,
    int Weed,
    int Coke,
    int HoeCutPercent,
    double HoeHappiness,
    double ThugHappiness);
