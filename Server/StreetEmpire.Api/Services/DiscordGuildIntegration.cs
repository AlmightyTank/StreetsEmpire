using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Chaos.NaCl;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

/// <summary>Discord bot settings that may come from config, with admin-stored values layered on top.</summary>
public sealed class DiscordIntegrationOptions
{
    public string BotToken { get; set; } = string.Empty;
    public string ApplicationId { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string GuildId { get; set; } = string.Empty;
    public string LinkedRoleId { get; set; } = string.Empty;
    public string TopTenRoleId { get; set; } = string.Empty;
    public string CrewBossRoleId { get; set; } = string.Empty;
    public string CityRoleMap { get; set; } = string.Empty;
}

public sealed class DiscordGuildIntegration(
    HttpClient http,
    GameDbContext db,
    IOptions<DiscordIntegrationOptions> options,
    IOptionsSnapshot<GameOptions> gameOptions,
    EconomyService economy,
    DiscordGatewayState gatewayState,
    ILogger<DiscordGuildIntegration> logger)
{
    private const string ApiRoot = "https://discord.com/api/v10";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<DiscordIntegrationSettingsResponse> SettingsResponseAsync(CancellationToken ct)
    {
        var row = await SettingsRowAsync(ct);
        var effective = Effective(row);
        return new DiscordIntegrationSettingsResponse(
            effective.BotConfigured,
            !string.IsNullOrWhiteSpace(row.DiscordBotToken),
            effective.SlashCommandsConfigured,
            effective.RoleSyncConfigured,
            gatewayState.Connected,
            gatewayState.ConnectedAtUtc,
            gatewayState.LastHeartbeatAckAtUtc,
            gatewayState.LastError,
            effective.ApplicationId,
            effective.GuildId,
            !string.IsNullOrWhiteSpace(effective.PublicKey),
            effective.LinkedRoleId,
            effective.TopTenRoleId,
            effective.CrewBossRoleId,
            CityRoleMapText(effective.CityRoles),
            row.DiscordRolesSyncedAtUtc,
            row.DiscordCommandsRegisteredAtUtc,
            row.UpdatedAtUtc,
            row.UpdatedBy);
    }

    public async Task<string?> GatewayBotTokenAsync(CancellationToken ct)
    {
        var effective = Effective(await SettingsRowAsync(ct));
        return effective.BotConfigured ? effective.BotToken : null;
    }

    public async Task<DiscordRoleSyncResponse> SyncRolesAsync(CancellationToken ct)
    {
        var row = await SettingsRowAsync(ct);
        var settings = Effective(row);
        if (!settings.RoleSyncConfigured)
            throw new GameRuleException("Add a bot token, guild id, and at least one managed role before syncing roles.");

        var players = await db.Players
            .Include(x => x.Account)
            .Include(x => x.Alliance)
            .Where(x => !x.Account.IsBot && x.Account.DiscordUserId != null)
            .OrderByDescending(economy.NetWorthExpression)
            .ThenBy(x => x.CreatedAtUtc)
            .ToListAsync(ct);

        var added = 0;
        var removed = 0;
        var synced = 0;
        var skipped = 0;
        var errors = new List<string>();
        var managedRoles = ManagedRoleIds(settings).ToList();

        for (var index = 0; index < players.Count; index++)
        {
            var player = players[index];
            var desired = DesiredRoleIds(settings, player, index + 1).ToHashSet(StringComparer.Ordinal);
            var current = await MemberRoleIdsAsync(settings, player.Account.DiscordUserId!, ct);
            if (!current.Success)
            {
                skipped++;
                errors.Add(current.NotInGuild
                    ? $"{player.Name}: Discord member was not found in the guild."
                    : $"{player.Name}: {current.Error ?? "Discord refused the member read."}");
                continue;
            }

            var currentRoles = current.Roles.ToHashSet(StringComparer.Ordinal);
            var addRoles = managedRoles.Where(roleId => desired.Contains(roleId) && !currentRoles.Contains(roleId)).ToList();
            var removeRoles = managedRoles.Where(roleId => !desired.Contains(roleId) && currentRoles.Contains(roleId)).ToList();

            foreach (var roleId in addRoles.Concat(removeRoles))
            {
                var want = addRoles.Contains(roleId);
                var result = await SetMemberRoleAsync(settings, player.Account.DiscordUserId!, roleId, want, ct);
                if (result.Success)
                {
                    if (want) added++;
                    else removed++;
                    continue;
                }

                errors.Add($"{player.Name}: {result.Error ?? "Discord refused the role update."}");
            }

            if (addRoles.Count > 0 || removeRoles.Count > 0)
                synced++;
        }

        var now = DateTime.UtcNow;
        row.DiscordRolesSyncedAtUtc = now;
        row.UpdatedAtUtc = now;
        await db.SaveChangesAsync(ct);
        return new DiscordRoleSyncResponse(players.Count, players.Count, synced, skipped, added, removed, errors.Take(20).ToList(), now);
    }

    public async Task<DiscordCommandRegistrationResponse> RegisterSlashCommandsAsync(CancellationToken ct)
    {
        var row = await SettingsRowAsync(ct);
        var settings = Effective(row);
        if (!settings.BotConfigured || string.IsNullOrWhiteSpace(settings.ApplicationId))
            throw new GameRuleException("Add a bot token, application id, and guild id before registering slash commands.");

        var commands = new[]
        {
            new
            {
                name = "profile",
                type = 1,
                description = "Look up a Street Empire profile.",
                options = new[] { StringOption("player", "Player name. Leave blank to use your linked empire.", required: false) }
            },
            new
            {
                name = "rank",
                type = 1,
                description = "Show a Street Empire rank and net worth.",
                options = new[] { StringOption("player", "Player name. Leave blank to use your linked empire.", required: false) }
            },
            new
            {
                name = "market",
                type = 1,
                description = "Show city market prices and travel risk.",
                options = new[] { StringOption("city", "City name. Leave blank to use your linked city.", required: false) }
            },
            new
            {
                name = "streetwire",
                type = 1,
                description = "Show the latest Street Empire update.",
                options = Array.Empty<object>()
            },
        };

        foreach (var command in commands)
        {
            var response = await SendBotJsonAsync(
                settings,
                HttpMethod.Post,
                $"/applications/{settings.ApplicationId}/guilds/{settings.GuildId}/commands",
                command,
                ct);
            if (!response.IsSuccessStatusCode)
                throw new GameRuleException($"Discord refused slash command registration with {(int)response.StatusCode} {response.ReasonPhrase}.");
        }

        var now = DateTime.UtcNow;
        row.DiscordCommandsRegisteredAtUtc = now;
        row.UpdatedAtUtc = now;
        await db.SaveChangesAsync(ct);
        return new DiscordCommandRegistrationResponse(commands.Length, now);
    }

    public async Task<bool> VerifyInteractionSignatureAsync(HttpRequest request, string body, CancellationToken ct)
    {
        var settings = Effective(await SettingsRowAsync(ct));
        if (string.IsNullOrWhiteSpace(settings.PublicKey))
            return false;

        var signatureHeader = request.Headers["X-Signature-Ed25519"].ToString();
        var timestamp = request.Headers["X-Signature-Timestamp"].ToString();
        if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(timestamp))
            return false;

        try
        {
            var signature = Convert.FromHexString(signatureHeader);
            var publicKey = Convert.FromHexString(settings.PublicKey);
            var signed = Encoding.UTF8.GetBytes(timestamp + body);
            return signature.Length == Ed25519.SignatureSizeInBytes
                && publicKey.Length == Ed25519.PublicKeySizeInBytes
                && Ed25519.Verify(signature, signed, publicKey);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public async Task<object> HandleInteractionAsync(JsonDocument document, CancellationToken ct)
    {
        var root = document.RootElement;
        var type = root.TryGetProperty("type", out var typeElement) ? typeElement.GetInt32() : 0;
        if (type == 1)
            return PongInteractionResponse();

        if (type != 2 || !root.TryGetProperty("data", out var data))
            return InteractionResponse(Ephemeral("Street Empire did not understand that Discord interaction."));

        var name = data.GetProperty("name").GetString()?.Trim().ToLowerInvariant();
        var discordUserId = DiscordUserId(root);
        var options = CommandOptions(data);
        var content = name switch
        {
            "profile" => await ProfileCommandAsync(options.GetValueOrDefault("player"), discordUserId, ct),
            "rank" => await RankCommandAsync(options.GetValueOrDefault("player"), discordUserId, ct),
            "market" => await MarketCommandAsync(options.GetValueOrDefault("city"), discordUserId, ct),
            "streetwire" => await StreetWireCommandAsync(ct),
            _ => Ephemeral("Street Empire does not have that command yet.")
        };

        return InteractionResponse(content);
    }

    public async Task EditOriginalInteractionResponseAsync(string applicationId, string interactionToken, object interactionResponse, CancellationToken ct)
    {
        var content = InteractionMessagePayload(interactionResponse);
        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"{ApiRoot}/webhooks/{Uri.EscapeDataString(applicationId)}/{Uri.EscapeDataString(interactionToken)}/messages/@original")
        {
            Content = JsonContent.Create(content, options: JsonOptions)
        };

        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            logger.LogWarning("Discord refused original interaction edit with {Status} {Reason}.", (int)response.StatusCode, response.ReasonPhrase);
    }

    public static object PongInteractionResponse() => new { type = 1 };

    /// <summary>
    /// Always ephemeral. The deferral is sent before the command name is parsed, so there is no way to
    /// know yet whether the answer wants to be public - and a private reply shown to the whole channel is
    /// the worse of the two mistakes. Discord fixes ephemerality at the deferral: the follow-up edit
    /// cannot widen it later, so every slash command answers the caller alone.
    /// </summary>
    public static object DeferredInteractionResponse()
        => new
        {
            type = 5,
            data = new
            {
                flags = 64,
                allowed_mentions = new { parse = Array.Empty<string>() }
            }
        };

    public static bool TryReadInteractionCallback(JsonDocument document, out int type, out string applicationId, out string token)
    {
        var root = document.RootElement;
        type = root.TryGetProperty("type", out var typeElement) && typeElement.ValueKind == JsonValueKind.Number
            ? typeElement.GetInt32()
            : 0;
        applicationId = root.TryGetProperty("application_id", out var appElement) ? appElement.GetString() ?? string.Empty : string.Empty;
        token = root.TryGetProperty("token", out var tokenElement) ? tokenElement.GetString() ?? string.Empty : string.Empty;
        return !string.IsNullOrWhiteSpace(applicationId) && !string.IsNullOrWhiteSpace(token);
    }

    public static string? NormalizeSnowflake(string? value, int max = 32)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > max || trimmed.Any(ch => ch is < '0' or > '9'))
            throw new GameRuleException("Discord ids must be numbers copied from Discord developer mode.");
        return trimmed;
    }

    public static string? NormalizePublicKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length != 64 || trimmed.Any(ch => !Uri.IsHexDigit(ch)))
            throw new GameRuleException("Discord public key must be the 64-character hex key from the application page.");
        return trimmed.ToLowerInvariant();
    }

    public static string? NormalizeBotToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length is < 32 or > 256)
            throw new GameRuleException("Discord bot token length does not look right.");
        return trimmed;
    }

    public static Dictionary<string, string> ParseCityRoleMap(string? value)
    {
        var roles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(value))
            return roles;

        var trimmed = value.Trim();
        if (trimmed.StartsWith("{", StringComparison.Ordinal))
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(trimmed, JsonOptions) ?? [];
            foreach (var pair in parsed)
                AddCityRole(roles, pair.Key, pair.Value);
            return roles;
        }

        foreach (var line in trimmed.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var clean = line.Trim();
            if (clean.Length == 0) continue;
            var separator = clean.IndexOf('=');
            if (separator < 0) separator = clean.IndexOf(':');
            if (separator < 0)
                throw new GameRuleException("City roles use one mapping per line, like Chicago=123456789.");
            AddCityRole(roles, clean[..separator], clean[(separator + 1)..]);
        }

        return roles;
    }

    public static string CityRoleMapJson(string? value)
    {
        var roles = ParseCityRoleMap(value);
        return roles.Count == 0 ? string.Empty : JsonSerializer.Serialize(roles, JsonOptions);
    }

    public static string CityRoleMapText(IReadOnlyDictionary<string, string> roles)
        => string.Join('\n', roles.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).Select(x => $"{x.Key}={x.Value}"));

    private static void AddCityRole(Dictionary<string, string> roles, string cityValue, string roleValue)
    {
        var city = cityValue.Trim();
        if (city.Length is < 2 or > 64)
            throw new GameRuleException("City names in the Discord role map must be 2-64 characters.");
        roles[city] = NormalizeSnowflake(roleValue) ?? throw new GameRuleException("Every city role needs a Discord role id.");
    }

    private async Task<DiscordCommandText> ProfileCommandAsync(string? playerName, string? discordUserId, CancellationToken ct)
    {
        var player = await ResolvePlayerAsync(playerName, discordUserId, ct);
        if (player is null)
            return NeedPlayer(playerName);

        var rank = await RankOfAsync(player, ct);
        var netWorth = economy.CalculateNetWorth(player);
        var crew = player.Alliance is null
            ? "Independent"
            : $"{player.Alliance.Name} ({AllianceRanks.Label(player.AllianceRank)})";
        return Public($"{player.Name} runs {player.City}. Rank #{rank:N0}, worth {netWorth:C0}. Crew: {crew}.");
    }

    private async Task<DiscordCommandText> RankCommandAsync(string? playerName, string? discordUserId, CancellationToken ct)
    {
        var player = await ResolvePlayerAsync(playerName, discordUserId, ct);
        if (player is null)
            return NeedPlayer(playerName);

        var rank = await RankOfAsync(player, ct);
        return Public($"{player.Name} is #{rank:N0} in Street Empire with {economy.CalculateNetWorth(player):C0} net worth.");
    }

    private async Task<DiscordCommandText> MarketCommandAsync(string? requestedCity, string? discordUserId, CancellationToken ct)
    {
        var city = requestedCity;
        if (string.IsNullOrWhiteSpace(city) && !string.IsNullOrWhiteSpace(discordUserId))
            city = await db.Players.AsNoTracking()
                .Where(x => x.Account.DiscordUserId == discordUserId)
                .Select(x => x.City)
                .SingleOrDefaultAsync(ct);

        var markets = gameOptions.Value.CityMarkets;
        gameOptions.Value.Territory.ApplyDefaultsWhereEmpty();
        markets.ApplyDefaultsWhereEmpty(gameOptions.Value.Territory.Cities());
        var resolved = markets.ResolveCity(city) ?? markets.ProfileFor(null).City;
        var weed = markets.ProductPrice(resolved, "weed", gameOptions.Value.WeedSellPrice);
        var coke = markets.ProductPrice(resolved, "coke", gameOptions.Value.CokeSellPrice);
        return Public($"{resolved} market: weed {weed:C0}, coke {coke:C0}. Travel {markets.TravelTurns(resolved):N0} turn(s), bust risk {markets.BustChancePercent(resolved)}%.");
    }

    private async Task<DiscordCommandText> StreetWireCommandAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var post = await db.GameAnnouncements.AsNoTracking()
            .Where(x => !x.IsDraft
                && x.ArchivedAtUtc == null
                && x.PublishedAtUtc <= now
                && (x.ExpiresAtUtc == null || x.ExpiresAtUtc > now))
            .OrderByDescending(x => x.IsPinned)
            .ThenByDescending(x => x.PublishedAtUtc)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);
        if (post is null)
            return Public("Street Wire is quiet right now.");

        var version = string.IsNullOrWhiteSpace(post.Version) ? string.Empty : $" [{post.Version}]";
        return Public($"{post.Title}{version}: {OneLine(post.Body, 280)}");
    }

    private async Task<Player?> ResolvePlayerAsync(string? playerName, string? discordUserId, CancellationToken ct)
    {
        var query = db.Players.AsNoTracking()
            .Include(x => x.Account)
            .Include(x => x.Alliance)
            .Include(x => x.Hideout);
        if (!string.IsNullOrWhiteSpace(playerName))
        {
            var wanted = playerName.Trim();
            return await query.SingleOrDefaultAsync(x => x.Name.ToLower() == wanted.ToLowerInvariant(), ct);
        }

        if (string.IsNullOrWhiteSpace(discordUserId))
            return null;
        return await query.SingleOrDefaultAsync(x => x.Account.DiscordUserId == discordUserId, ct);
    }

    private async Task<int> RankOfAsync(Player player, CancellationToken ct)
    {
        var netWorth = economy.CalculateNetWorth(player);
        var contenders = await db.Players.AsNoTracking()
            .Where(economy.RanksAbove(netWorth, player.CreatedAtUtc))
            .Select(economy.StandingExpression())
            .ToListAsync(ct);
        return EconomyService.RankOf(new PlayerStanding(netWorth, player.CreatedAtUtc), contenders);
    }

    private static DiscordCommandText NeedPlayer(string? playerName)
        => string.IsNullOrWhiteSpace(playerName)
            ? Ephemeral("Link Discord from your Street Empire account, or pass a player name.")
            : Ephemeral($"No Street Empire player named {playerName.Trim()}.");

    private static DiscordCommandText Public(string text) => new(text, false);
    private static DiscordCommandText Ephemeral(string text) => new(text, true);

    private static object InteractionResponse(DiscordCommandText content)
        => new
        {
            type = 4,
            data = new
            {
                content = content.Text,
                flags = content.Ephemeral ? 64 : 0,
                allowed_mentions = new { parse = Array.Empty<string>() }
            }
        };

    private static object InteractionMessagePayload(object interactionResponse)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(interactionResponse, JsonOptions));
        if (document.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
        {
            var content = data.TryGetProperty("content", out var contentElement) ? contentElement.GetString() ?? string.Empty : string.Empty;
            return new
            {
                content,
                allowed_mentions = new { parse = Array.Empty<string>() }
            };
        }

        return new
        {
            content = "Street Empire could not answer that command.",
            allowed_mentions = new { parse = Array.Empty<string>() }
        };
    }

    private static Dictionary<string, string> CommandOptions(JsonElement data)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!data.TryGetProperty("options", out var options) || options.ValueKind != JsonValueKind.Array)
            return values;

        foreach (var option in options.EnumerateArray())
        {
            var name = option.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
            if (string.IsNullOrWhiteSpace(name) || !option.TryGetProperty("value", out var valueElement))
                continue;
            values[name] = valueElement.ValueKind == JsonValueKind.String
                ? valueElement.GetString() ?? string.Empty
                : valueElement.ToString();
        }

        return values;
    }

    private static string? DiscordUserId(JsonElement root)
    {
        if (root.TryGetProperty("member", out var member)
            && member.TryGetProperty("user", out var memberUser)
            && memberUser.TryGetProperty("id", out var memberUserId))
            return memberUserId.GetString();
        if (root.TryGetProperty("user", out var user)
            && user.TryGetProperty("id", out var userId))
            return userId.GetString();
        return null;
    }

    private static IEnumerable<string> ManagedRoleIds(EffectiveDiscordSettings settings)
    {
        foreach (var role in new[] { settings.LinkedRoleId, settings.TopTenRoleId, settings.CrewBossRoleId }
                     .Where(x => !string.IsNullOrWhiteSpace(x)))
            yield return role!;
        foreach (var role in settings.CityRoles.Values)
            yield return role;
    }

    private static IEnumerable<string> DesiredRoleIds(EffectiveDiscordSettings settings, Player player, int rank)
    {
        if (!string.IsNullOrWhiteSpace(settings.LinkedRoleId))
            yield return settings.LinkedRoleId;
        if (rank <= 10 && !string.IsNullOrWhiteSpace(settings.TopTenRoleId))
            yield return settings.TopTenRoleId;
        if (player.Alliance?.FounderId == player.Id && !string.IsNullOrWhiteSpace(settings.CrewBossRoleId))
            yield return settings.CrewBossRoleId;
        if (settings.CityRoles.TryGetValue(player.City, out var cityRole))
            yield return cityRole;
    }

    private async Task<DiscordRoleResult> SetMemberRoleAsync(EffectiveDiscordSettings settings, string userId, string roleId, bool add, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(
            add ? HttpMethod.Put : HttpMethod.Delete,
            $"{ApiRoot}/guilds/{settings.GuildId}/members/{userId}/roles/{roleId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bot", settings.BotToken);
        request.Headers.TryAddWithoutValidation("X-Audit-Log-Reason", Uri.EscapeDataString("Street Empire role sync"));
        try
        {
            using var response = await http.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
                return DiscordRoleResult.Ok;
            return new DiscordRoleResult(false, false, $"{(int)response.StatusCode} {response.ReasonPhrase}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Could not sync Discord role {RoleId} for {DiscordUserId}.", roleId, userId);
            return new DiscordRoleResult(false, false, "Discord API request failed.");
        }
    }

    private async Task<DiscordMemberRolesResult> MemberRoleIdsAsync(EffectiveDiscordSettings settings, string userId, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiRoot}/guilds/{settings.GuildId}/members/{userId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bot", settings.BotToken);
        try
        {
            using var response = await http.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return DiscordMemberRolesResult.MemberMissing;
            if (!response.IsSuccessStatusCode)
                return new DiscordMemberRolesResult(false, false, [], $"{(int)response.StatusCode} {response.ReasonPhrase}");

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var roles = document.RootElement.TryGetProperty("roles", out var rolesElement) && rolesElement.ValueKind == JsonValueKind.Array
                ? rolesElement.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).ToList()
                : [];
            return new DiscordMemberRolesResult(true, false, roles, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "Could not read Discord member {DiscordUserId}.", userId);
            return new DiscordMemberRolesResult(false, false, [], "Discord member read failed.");
        }
    }

    private async Task<HttpResponseMessage> SendBotJsonAsync(EffectiveDiscordSettings settings, HttpMethod method, string path, object body, CancellationToken ct)
    {
        var request = new HttpRequestMessage(method, $"{ApiRoot}{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bot", settings.BotToken);
        request.Content = JsonContent.Create(body, options: JsonOptions);
        return await http.SendAsync(request, ct);
    }

    private async Task<GameSetting> SettingsRowAsync(CancellationToken ct)
    {
        var row = await db.GameSettings.SingleOrDefaultAsync(x => x.Id == 1, ct);
        if (row is not null) return row;

        row = new GameSetting { Id = 1 };
        db.GameSettings.Add(row);
        await db.SaveChangesAsync(ct);
        return row;
    }

    private EffectiveDiscordSettings Effective(GameSetting row)
    {
        var fallback = options.Value;
        var cityMap = string.IsNullOrWhiteSpace(row.DiscordCityRoleMapJson)
            ? fallback.CityRoleMap
            : row.DiscordCityRoleMapJson;
        return new EffectiveDiscordSettings(
            First(row.DiscordBotToken, fallback.BotToken),
            First(row.DiscordApplicationId, fallback.ApplicationId),
            First(row.DiscordPublicKey, fallback.PublicKey),
            First(row.DiscordGuildId, fallback.GuildId),
            First(row.DiscordLinkedRoleId, fallback.LinkedRoleId),
            First(row.DiscordTopTenRoleId, fallback.TopTenRoleId),
            First(row.DiscordCrewBossRoleId, fallback.CrewBossRoleId),
            ParseCityRoleMap(cityMap));
    }

    private static object StringOption(string name, string description, bool required)
        => new { name, description, type = 3, required };

    private static string? First(string? stored, string? configured)
        => !string.IsNullOrWhiteSpace(stored) ? stored.Trim()
            : !string.IsNullOrWhiteSpace(configured) ? configured.Trim()
            : null;

    private static string OneLine(string value, int max)
    {
        var clean = string.Join(' ', value.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
        return clean.Length <= max ? clean : clean[..Math.Max(0, max - 1)] + "...";
    }

    private sealed record EffectiveDiscordSettings(
        string? BotToken,
        string? ApplicationId,
        string? PublicKey,
        string? GuildId,
        string? LinkedRoleId,
        string? TopTenRoleId,
        string? CrewBossRoleId,
        IReadOnlyDictionary<string, string> CityRoles)
    {
        public bool BotConfigured => !string.IsNullOrWhiteSpace(BotToken) && !string.IsNullOrWhiteSpace(GuildId);
        public bool SlashCommandsConfigured => BotConfigured && !string.IsNullOrWhiteSpace(ApplicationId) && !string.IsNullOrWhiteSpace(PublicKey);
        public bool RoleSyncConfigured => BotConfigured
            && (!string.IsNullOrWhiteSpace(LinkedRoleId)
                || !string.IsNullOrWhiteSpace(TopTenRoleId)
                || !string.IsNullOrWhiteSpace(CrewBossRoleId)
                || CityRoles.Count > 0);
    }

    private sealed record DiscordRoleResult(bool Success, bool NotInGuild, string? Error)
    {
        public static readonly DiscordRoleResult Ok = new(true, false, null);
    }

    private sealed record DiscordMemberRolesResult(bool Success, bool NotInGuild, IReadOnlyList<string> Roles, string? Error)
    {
        public static readonly DiscordMemberRolesResult MemberMissing = new(false, true, [], null);
    }

    private sealed record DiscordCommandText(string Text, bool Ephemeral);
}
