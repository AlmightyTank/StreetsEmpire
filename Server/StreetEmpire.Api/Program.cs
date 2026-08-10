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
builder.Services.Configure<BotAutomationOptions>(builder.Configuration.GetSection("Bots"));
builder.Services.AddDbContext<GameDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("GameDatabase")));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentPlayerService>();
builder.Services.AddScoped<TurnService>();
builder.Services.AddScoped<EconomyService>();
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

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", version = "0.1.10" }));

app.MapPost("/api/auth/register", async (
    RegisterRequest request,
    GameDbContext db,
    IPasswordHasher<PlayerAccount> passwordHasher,
    IOptions<GameOptions> gameOptions,
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

app.MapGet("/api/world/news", async (
    GameDbContext db,
    CancellationToken ct) =>
{
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
    IOptions<GameOptions> gameOptions,
    IOptions<BotAutomationOptions> botOptions,
    BotAutomationState botAutomation,
    CancellationToken ct) =>
{
    var admin = await current.GetAsync(ct);
    if (admin is null) return Results.Unauthorized();
    if (!admin.Account.IsAdmin) return Results.Forbid();

    var players = await db.Players.AsNoTracking().ToListAsync(ct);
    var totalAccounts = await db.Accounts.AsNoTracking().CountAsync(ct);
    var adminAccounts = await db.Accounts.AsNoTracking().CountAsync(x => x.IsAdmin, ct);
    var botAccounts = await db.Accounts.AsNoTracking().CountAsync(x => x.IsBot, ct);
    var totalNetWorth = players.Sum(economy.CalculateNetWorth);

    return Results.Ok(new AdminOverviewResponse(
        DateTime.UtcNow,
        totalAccounts,
        adminAccounts,
        botAccounts,
        players.Count,
        players.Sum(x => x.Cash),
        players.Sum(x => x.BankCash),
        players.Sum(x => x.Cash + x.BankCash),
        totalNetWorth,
        players.Sum(x => x.Turns),
        players.Count == 0 ? 0 : Math.Round(players.Average(x => x.HoeHappiness), 2),
        players.Count == 0 ? 0 : Math.Round(players.Average(x => x.ThugHappiness), 2),
        new BotAutomationStatusResponse(
            botAutomation.Enabled,
            Math.Clamp(botOptions.Value.TickSeconds, 15, 3600),
            Math.Clamp(botOptions.Value.RoundsPerTick, 1, 10)),
        gameOptions.Value));
}).RequireAuthorization();

app.MapPost("/api/admin/cheats", async (
    AdminCheatRequest request,
    CurrentPlayerService current,
    GameDbContext db,
    IOptions<GameOptions> gameOptions,
    CancellationToken ct) =>
{
    var player = await current.GetAsync(ct);
    if (player is null) return Results.Unauthorized();
    if (!player.Account.IsAdmin) return Results.Forbid();

    var before = Snapshot(player);
    try
    {
        var summary = ApplyAdminCheat(player, request, gameOptions.Value);
        AddLog(db, player, before, "ADMIN", 0, summary);
        await db.SaveChangesAsync(ct);
        return Results.Ok(new ActionResultResponse(summary, player.Turns, new Dictionary<string, object?>
        {
            ["cheat"] = request.Cheat?.Trim().ToLowerInvariant(),
            ["amount"] = request.Amount
        }));
    }
    catch (GameRuleException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (OverflowException)
    {
        return Results.BadRequest(new { error = "Cheat amount would exceed the resource limit." });
    }
}).RequireAuthorization();

app.MapPost("/api/admin/bots/seed", async (
    AdminSeedBotsRequest request,
    CurrentPlayerService current,
    GameDbContext db,
    IOptions<GameOptions> gameOptions,
    CancellationToken ct) =>
{
    var admin = await current.GetAsync(ct);
    if (admin is null) return Results.Unauthorized();
    if (!admin.Account.IsAdmin) return Results.Forbid();

    var templates = BotTemplates();
    var count = Math.Clamp(request.Count, 1, templates.Count);
    var now = DateTime.UtcNow;
    var existingUsernames = (await db.Accounts.AsNoTracking().Select(x => x.Username).ToListAsync(ct))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    var existingNames = (await db.Players.AsNoTracking().Select(x => x.Name).ToListAsync(ct))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    var created = 0;

    foreach (var template in templates)
    {
        if (created >= count) break;
        if (existingUsernames.Contains(template.Username) || existingNames.Contains(template.Name))
            continue;

        var player = CreateBotPlayer(template, gameOptions.Value, now);
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

static string ApplyAdminCheat(Player player, AdminCheatRequest request, GameOptions options)
{
    var cheat = request.Cheat?.Trim().ToLowerInvariant() ?? string.Empty;
    var amount = request.Amount;
    if (amount <= 0)
        throw new GameRuleException("Cheat amount must be positive.");
    if (amount > 1_000_000_000)
        throw new GameRuleException("Cheat amount is too large.");

    switch (cheat)
    {
        case "cash":
            player.Cash = checked(player.Cash + amount);
            return $"Admin cheat granted ${amount:N0} cash.";
        case "bank":
        case "bankcash":
            player.BankCash = checked(player.BankCash + amount);
            return $"Admin cheat granted ${amount:N0} bank cash.";
        case "turns":
            player.Turns = Math.Min(options.MaxTurns, checked(player.Turns + ToInt(amount)));
            return $"Admin cheat granted up to {amount:N0} turns.";
        case "pimps":
            player.Pimps = checked(player.Pimps + ToInt(amount));
            return $"Admin cheat granted {amount:N0} pimps.";
        case "hoes":
            player.Hoes = checked(player.Hoes + ToInt(amount));
            return $"Admin cheat granted {amount:N0} hoes.";
        case "thugs":
            player.Thugs = checked(player.Thugs + ToInt(amount));
            return $"Admin cheat granted {amount:N0} thugs.";
        case "condoms":
            player.Condoms = checked(player.Condoms + ToInt(amount));
            return $"Admin cheat granted {amount:N0} condoms.";
        case "beer":
            player.Beer = checked(player.Beer + ToInt(amount));
            return $"Admin cheat granted {amount:N0} beer.";
        case "weapons":
            player.Weapons = checked(player.Weapons + ToInt(amount));
            return $"Admin cheat granted {amount:N0} weapons.";
        case "weed":
            player.Weed = checked(player.Weed + ToInt(amount));
            return $"Admin cheat granted {amount:N0} weed.";
        case "coke":
            player.Coke = checked(player.Coke + ToInt(amount));
            return $"Admin cheat granted {amount:N0} coke.";
        case "morale":
            var morale = Math.Clamp(amount, 0, 100);
            player.HoeHappiness = morale;
            player.ThugHappiness = morale;
            return $"Admin cheat set crew morale to {morale:N0}%.";
        default:
            throw new GameRuleException("Unknown admin cheat.");
    }
}

static int ToInt(long value)
{
    if (value > int.MaxValue)
        throw new GameRuleException("Cheat amount is too large for this resource.");
    return (int)value;
}

static Player CreateBotPlayer(BotTemplate template, GameOptions options, DateTime createdAtUtc)
{
    var account = new PlayerAccount
    {
        Username = template.Username,
        PasswordHash = "BOT_ACCOUNT_DISABLED",
        IsBot = true,
        CreatedAtUtc = createdAtUtc
    };

    return new Player
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
