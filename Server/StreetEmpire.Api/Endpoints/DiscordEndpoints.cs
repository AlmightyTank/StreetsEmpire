using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Services;
using static StreetEmpire.Api.Support.LiveOpsStore;

namespace StreetEmpire.Api.Endpoints;

/// <summary>Discord bot configuration, role sync, and signed slash-command callbacks.</summary>
internal static class DiscordEndpoints
{
    internal static void MapDiscordEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/discord", async (
            CurrentPlayerService current,
            DiscordGuildIntegration discord,
            CancellationToken ct) =>
        {
            var admin = await current.GetAsync(ct);
            if (admin is null) return Results.Unauthorized();
            if (!admin.Account.IsAdmin) return Results.Forbid();

            return Results.Ok(await discord.SettingsResponseAsync(ct));
        }).RequireAuthorization();

        app.MapPut("/api/admin/discord", async (
            DiscordIntegrationSettingsRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            AdminService admins,
            DiscordGuildIntegration discord,
            CancellationToken ct) =>
        {
            var admin = await current.GetAsync(ct);
            if (admin is null) return Results.Unauthorized();
            if (!admin.Account.IsAdmin) return Results.Forbid();

            var settings = await LiveOpsAsync(db, ct);
            var changes = new List<string>();
            var now = DateTime.UtcNow;

            try
            {
                if (request.ClearBotToken)
                {
                    settings.DiscordBotToken = null;
                    changes.Add("Discord bot token cleared");
                }
                else if (request.BotToken is not null)
                {
                    settings.DiscordBotToken = DiscordGuildIntegration.NormalizeBotToken(request.BotToken);
                    changes.Add(settings.DiscordBotToken is null ? "Discord bot token cleared" : "Discord bot token updated");
                }

                if (request.ClearPublicKey)
                {
                    settings.DiscordPublicKey = null;
                    changes.Add("Discord public key cleared");
                }
                else if (request.PublicKey is not null)
                {
                    settings.DiscordPublicKey = DiscordGuildIntegration.NormalizePublicKey(request.PublicKey);
                    changes.Add(settings.DiscordPublicKey is null ? "Discord public key cleared" : "Discord public key updated");
                }

                if (request.ApplicationId is not null)
                {
                    settings.DiscordApplicationId = DiscordGuildIntegration.NormalizeSnowflake(request.ApplicationId);
                    changes.Add("Discord application id updated");
                }
                if (request.GuildId is not null)
                {
                    settings.DiscordGuildId = DiscordGuildIntegration.NormalizeSnowflake(request.GuildId);
                    changes.Add("Discord guild id updated");
                }
                if (request.LinkedRoleId is not null)
                {
                    settings.DiscordLinkedRoleId = DiscordGuildIntegration.NormalizeSnowflake(request.LinkedRoleId);
                    changes.Add("Discord linked role updated");
                }
                if (request.TopTenRoleId is not null)
                {
                    settings.DiscordTopTenRoleId = DiscordGuildIntegration.NormalizeSnowflake(request.TopTenRoleId);
                    changes.Add("Discord top-ten role updated");
                }
                if (request.CrewBossRoleId is not null)
                {
                    settings.DiscordCrewBossRoleId = DiscordGuildIntegration.NormalizeSnowflake(request.CrewBossRoleId);
                    changes.Add("Discord crew-boss role updated");
                }
                if (request.CityRoleMap is not null)
                {
                    settings.DiscordCityRoleMapJson = DiscordGuildIntegration.CityRoleMapJson(request.CityRoleMap);
                    changes.Add("Discord city role map updated");
                }
                if (request.CrewRoleMap is not null)
                {
                    settings.DiscordCrewRoleMapJson = DiscordGuildIntegration.CrewRoleMapJson(request.CrewRoleMap);
                    changes.Add("Discord crew role map updated");
                }
                if (request.CrewChannelMap is not null)
                {
                    settings.DiscordCrewChannelMapJson = DiscordGuildIntegration.CrewChannelMapJson(request.CrewChannelMap);
                    changes.Add("Discord crew channel map updated");
                }
                if (request.TitleRoleMap is not null)
                {
                    settings.DiscordTitleRoleMapJson = DiscordGuildIntegration.TitleRoleMapJson(request.TitleRoleMap);
                    changes.Add("Discord title role map updated");
                }
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }

            if (changes.Count == 0)
                return Results.BadRequest(new { error = "Nothing to change." });

            settings.UpdatedAtUtc = now;
            settings.UpdatedBy = admin.Account.Username;
            admins.Record(admin.Account, "DiscordIntegration", null, string.Join("; ", changes), request.Reason, now);
            await db.SaveChangesAsync(ct);

            return Results.Ok(await discord.SettingsResponseAsync(ct));
        }).RequireAuthorization();

        app.MapPost("/api/admin/discord/ensure-roles", async (
            CurrentPlayerService current,
            GameDbContext db,
            AdminService admins,
            DiscordGuildIntegration discord,
            CancellationToken ct) =>
        {
            var admin = await current.GetAsync(ct);
            if (admin is null) return Results.Unauthorized();
            if (!admin.Account.IsAdmin) return Results.Forbid();

            try
            {
                var result = await discord.EnsureRoleMapsAsync(admin.Account.Username, ct);
                admins.Record(admin.Account, "DiscordRoles", null,
                    $"ensured {result.EnsuredRoles:N0} Discord role map entrie(s), created {result.CreatedRoles:N0}, reused {result.ReusedRoles:N0}",
                    null, result.EnsuredAtUtc);
                await db.SaveChangesAsync(ct);
                return Results.Ok(result);
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();

        app.MapPost("/api/admin/discord/sync-crew-channels", async (
            CurrentPlayerService current,
            GameDbContext db,
            AdminService admins,
            DiscordGuildIntegration discord,
            CancellationToken ct) =>
        {
            var admin = await current.GetAsync(ct);
            if (admin is null) return Results.Unauthorized();
            if (!admin.Account.IsAdmin) return Results.Forbid();

            try
            {
                var result = await discord.SyncCrewChannelsAsync(admin.Account.Username, ct);
                admins.Record(admin.Account, "DiscordCrewChannels", null,
                    $"synced {result.Channels:N0} Discord crew channel(s), created {result.CreatedChannels:N0}, reused {result.ReusedChannels:N0}, updated {result.UpdatedChannels:N0}",
                    null, result.SyncedAtUtc);
                await db.SaveChangesAsync(ct);
                return Results.Ok(result);
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();

        app.MapPost("/api/admin/discord/sync-roles", async (
            CurrentPlayerService current,
            GameDbContext db,
            AdminService admins,
            DiscordGuildIntegration discord,
            CancellationToken ct) =>
        {
            var admin = await current.GetAsync(ct);
            if (admin is null) return Results.Unauthorized();
            if (!admin.Account.IsAdmin) return Results.Forbid();

            try
            {
                var result = await discord.SyncRolesAsync(ct);
                admins.Record(admin.Account, "DiscordRoleSync", null,
                    $"synced {result.SyncedPlayers:N0} Discord member(s), added {result.RolesAdded:N0} role(s), removed {result.RolesRemoved:N0}",
                    null, result.SyncedAtUtc);
                await db.SaveChangesAsync(ct);
                return Results.Ok(result);
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();

        app.MapPost("/api/admin/discord/register-commands", async (
            CurrentPlayerService current,
            GameDbContext db,
            AdminService admins,
            DiscordGuildIntegration discord,
            CancellationToken ct) =>
        {
            var admin = await current.GetAsync(ct);
            if (admin is null) return Results.Unauthorized();
            if (!admin.Account.IsAdmin) return Results.Forbid();

            try
            {
                var result = await discord.RegisterSlashCommandsAsync(ct);
                admins.Record(admin.Account, "DiscordSlashCommands", null,
                    $"registered {result.Registered:N0} Discord slash command(s)",
                    null, result.RegisteredAtUtc);
                await db.SaveChangesAsync(ct);
                return Results.Ok(result);
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();

        app.MapPost("/api/discord/interactions", async (
            HttpRequest request,
            DiscordGuildIntegration discord,
            IServiceScopeFactory scopes,
            ILoggerFactory loggers,
            CancellationToken ct) =>
        {
            using var reader = new StreamReader(request.Body);
            var body = await reader.ReadToEndAsync(ct);
            if (!await discord.VerifyInteractionSignatureAsync(request, body, ct))
                return Results.Unauthorized();

            using var document = JsonDocument.Parse(body);
            if (!DiscordGuildIntegration.TryReadInteractionCallback(document, out var type, out var applicationId, out var token))
                return Results.Json(await discord.HandleInteractionAsync(document, ct));

            if (type == 1)
                return Results.Json(DiscordGuildIntegration.PongInteractionResponse());

            if (type != 2)
                return Results.Json(await discord.HandleInteractionAsync(document, ct));

            var logger = loggers.CreateLogger("StreetEmpire.DiscordInteractions");
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    var worker = scope.ServiceProvider.GetRequiredService<DiscordGuildIntegration>();
                    using var backgroundDocument = JsonDocument.Parse(body);
                    var response = await worker.HandleInteractionAsync(backgroundDocument, CancellationToken.None);
                    await worker.EditOriginalInteractionResponseAsync(applicationId, token, response, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Could not finish deferred Discord interaction.");
                }
            }, CancellationToken.None);

            return Results.Json(DiscordGuildIntegration.DeferredInteractionResponse());
        }).DisableRateLimiting();
    }
}
