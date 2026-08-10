using System.Security.Claims;
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

builder.Services.Configure<GameOptions>(builder.Configuration.GetSection("Game"));
builder.Services.AddDbContext<GameDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("GameDatabase")));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentPlayerService>();
builder.Services.AddScoped<TurnService>();
builder.Services.AddScoped<EconomyService>();
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

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", version = "0.1.1" }));

app.MapPost("/api/auth/register", async (
    RegisterRequest request,
    GameDbContext db,
    IPasswordHasher<PlayerAccount> passwordHasher,
    IOptions<GameOptions> gameOptions,
    HttpContext http,
    CancellationToken ct) =>
{
    var username = request.Username.Trim();
    var playerName = request.PlayerName.Trim();

    if (username.Length is < 3 or > 32)
        return Results.BadRequest(new { error = "Username must be 3-32 characters." });
    if (playerName.Length is < 3 or > 32)
        return Results.BadRequest(new { error = "Player name must be 3-32 characters." });
    if (request.Password.Length < 8)
        return Results.BadRequest(new { error = "Password must be at least 8 characters." });

    if (await db.Accounts.AnyAsync(x => x.Username == username, ct))
        return Results.Conflict(new { error = "Username is already taken." });
    if (await db.Players.AnyAsync(x => x.Name == playerName, ct))
        return Results.Conflict(new { error = "Player name is already taken." });

    var opts = gameOptions.Value;
    var account = new PlayerAccount { Username = username };
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
    var username = request.Username.Trim();
    var account = await db.Accounts.Include(x => x.Player)
        .SingleOrDefaultAsync(x => x.Username == username, ct);

    if (account is null || passwordHasher.VerifyHashedPassword(account, account.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
        return Results.Unauthorized();

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
    IOptions<GameOptions> gameOptions,
    CancellationToken ct) =>
{
    var player = await current.GetAsync(ct);
    if (player is null) return Results.Unauthorized();

    var now = DateTime.UtcNow;
    if (turns.Refresh(player, now))
        await db.SaveChangesAsync(ct);

    var netWorth = economy.CalculateNetWorth(player);
    var allPlayers = await db.Players.AsNoTracking().ToListAsync(ct);
    var rank = allPlayers.Count(x => economy.CalculateNetWorth(x) > netWorth) + 1;
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
        player.City,
        player.Cash,
        player.BankCash,
        netWorth,
        rank,
        player.Turns,
        opts.MaxTurns,
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
        economy.GetStore(),
        activity));
}).RequireAuthorization();

app.MapPost("/api/game/street", async (
    ScoutRequest request,
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
        var result = economy.Scout(player, request.Turns);
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
    CancellationToken ct) =>
{
    var player = await current.GetAsync(ct);
    if (player is null) return Results.Unauthorized();

    turns.Refresh(player, DateTime.UtcNow);
    var before = Snapshot(player);
    try
    {
        var result = economy.Scout(player, request.Turns);
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

app.MapGet("/api/game/leaderboard", async (
    GameDbContext db,
    EconomyService economy,
    CancellationToken ct) =>
{
    var players = await db.Players.AsNoTracking().ToListAsync(ct);
    var result = players
        .Select(x => new { Player = x, NetWorth = economy.CalculateNetWorth(x) })
        .OrderByDescending(x => x.NetWorth)
        .ThenBy(x => x.Player.CreatedAtUtc)
        .Take(50)
        .Select((x, index) => new LeaderboardEntryResponse(
            index + 1,
            x.Player.Name,
            x.Player.City,
            x.NetWorth,
            x.Player.Cash,
            x.Player.BankCash,
            x.Player.Pimps,
            x.Player.Hoes,
            x.Player.Thugs))
        .ToList();
    return Results.Ok(result);
}).RequireAuthorization();

app.Run();

static async Task SignInAsync(HttpContext http, PlayerAccount account)
{
    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, account.Id.ToString()),
        new(ClaimTypes.Name, account.Username)
    };
    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await http.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(identity),
        new AuthenticationProperties { IsPersistent = true });
}

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
