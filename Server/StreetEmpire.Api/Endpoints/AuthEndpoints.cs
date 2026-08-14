using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Mapping;
using StreetEmpire.Api.Models;
using StreetEmpire.Api.Services;
using static StreetEmpire.Api.Mapping.ResponseMappers;
using static StreetEmpire.Api.Support.ActionLogging;
using static StreetEmpire.Api.Support.BotSeeding;
using static StreetEmpire.Api.Support.Formatting;
using static StreetEmpire.Api.Support.LiveOpsStore;
using static StreetEmpire.Api.Support.PlayerRanking;
using StreetEmpire.Api.Support;

namespace StreetEmpire.Api.Endpoints;

/// <summary>Registration, login, and logout.</summary>
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
                City = city ?? cities.FirstOrDefault() ?? "New York",
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
                Summary = $"{playerName} started an operation in {player.City} with ${opts.StartingCash:N0}, {opts.StartingPimps} pimp(s), {opts.StartingHoes} hoe(s), and {opts.StartingThugs} thug(s).",
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
    }
}
