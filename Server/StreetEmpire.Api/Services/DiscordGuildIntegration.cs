using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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
    public string CrewRoleMap { get; set; } = string.Empty;
    public string CrewChannelMap { get; set; } = string.Empty;
    public string TitleRoleMap { get; set; } = string.Empty;

    /// <summary>
    /// Where the game itself lives, so a Discord message can offer a way back into it.
    ///
    /// Every link the bot writes is built from this, and nothing else on the server knows the public
    /// address. The OAuth ReturnUrl is the closest thing and it is the wrong answer: that is where a
    /// sign-in is put down, not where the game is, and the two stop agreeing the moment sign-in moves.
    ///
    /// Blank means the bot writes no buttons at all. A button pointing at somebody else's localhost is
    /// worse than no button, because it looks like it works.
    /// </summary>
    public string PublicUrl { get; set; } = string.Empty;

    /// <summary>
    /// The channel a jackpot climbing past a notable number is announced in. Blank means it is not.
    ///
    /// Config only for now, unlike the role maps: this is one id that changes when a server is set up
    /// and never again, and putting it in the admin panel would mean a column, a contract and a form
    /// field for a value nobody edits twice.
    /// </summary>
    public string AnnounceChannelId { get; set; } = string.Empty;

    /// <summary>
    /// How much the progressive has to climb before the floor is told again, in whole dollars.
    ///
    /// A step rather than a single threshold, so one number covers a jackpot at five million and the
    /// same jackpot at fifty. Zero - the default - means the bot never announces a jackpot at all,
    /// which is the right behaviour for a server that has not asked for it.
    /// </summary>
    public long JackpotAnnounceStep { get; set; }

    /// <summary>
    /// How often a crew report is posted into each crew's own channel, in hours. Zero - the default -
    /// means never.
    ///
    /// Off unless asked for, because this is the only thing the bot does that nobody triggered: every
    /// other message answers a command or an event the reader's own empire produced. A room that fills
    /// up with unasked-for reports is a room people mute, and a muted crew room takes the alerts that
    /// mattered down with it.
    /// </summary>
    public int CrewReportHours { get; set; }

    /// <summary>
    /// How long each line of the bot's status is shown before the next, in seconds. Zero - the default
    /// - leaves it on "Playing Street Empire" and never changes it.
    ///
    /// Clamped to a floor by the gateway whatever is set here: Discord rate-limits presence updates,
    /// and a status flicking every second would cost the session rather than the status.
    /// </summary>
    public int PresenceRotateSeconds { get; set; }
}

/// <summary>
/// Everything the bot does against one guild: reading the settings row, keeping roles and crew rooms
/// in step with the game, and every raw call to Discord.
///
/// Partial across three files, because one file had grown to the point where the role sync and the
/// slash commands were each something you scrolled past to reach the other:
///
/// <list type="bullet">
///   <item>this file - settings, role sync, crew channels, and the HTTP underneath them;</item>
///   <item><c>DiscordCommands.cs</c> - what the slash commands are and how an answer is shaped;</item>
///   <item><c>DiscordSettingsText.cs</c> - the static parsers turning admin typing into ids and maps.</item>
/// </list>
///
/// One class rather than three, because all of it needs the same bot token, the same settings row and
/// the same HttpClient. Splitting the type would have meant threading those through three constructors
/// to buy nothing the file split does not already give.
/// </summary>
public sealed partial class DiscordGuildIntegration(
    HttpClient http,
    GameDbContext db,
    IOptions<DiscordIntegrationOptions> options,
    IOptionsSnapshot<GameOptions> gameOptions,
    EconomyService economy,
    TitleService titles,
    CasinoService casino,
    SeasonService seasons,
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
            RoleMapText(effective.CrewRoles),
            RoleMapText(effective.CrewChannels),
            RoleMapText(effective.TitleRoles),
            row.DiscordRolesSyncedAtUtc,
            row.DiscordCrewChannelsSyncedAtUtc,
            row.DiscordCommandsRegisteredAtUtc,
            row.UpdatedAtUtc,
            row.UpdatedBy);
    }

    /// <summary>
    /// The Discord channel each crew has, by crew name.
    ///
    /// Exposed so the sweep can post into these rooms without learning how the settings row layers
    /// over configuration - which is this class's job and nobody else's.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string>> CrewChannelsAsync(CancellationToken ct)
        => Effective(await SettingsRowAsync(ct)).CrewChannels;

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
        var titleBoard = settings.TitleRoles.Count == 0
            ? []
            : await titles.BoardAsync(DateTime.UtcNow, ct);

        var added = 0;
        var removed = 0;
        var synced = 0;
        var skipped = 0;
        var errors = new List<string>();
        var managedRoles = ManagedRoleIds(settings).ToList();

        for (var index = 0; index < players.Count; index++)
        {
            var player = players[index];
            var desired = DesiredRoleIds(settings, player, index + 1, titleBoard).ToHashSet(StringComparer.Ordinal);
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

    public async Task<DiscordRoleEnsureResponse> EnsureRoleMapsAsync(string? updatedBy, CancellationToken ct)
    {
        var row = await SettingsRowAsync(ct);
        var settings = Effective(row);
        if (!settings.BotConfigured)
            throw new GameRuleException("Add a bot token and guild id before creating Discord roles.");

        var existingRoles = await GuildRolesAsync(settings, ct);
        var rolesById = existingRoles.ToDictionary(x => x.Id, StringComparer.Ordinal);
        var rolesByName = existingRoles
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        var cityMap = settings.CityRoles.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        var crewMap = settings.CrewRoles.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        var titleMap = settings.TitleRoles.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);

        gameOptions.Value.Territory.ApplyDefaultsWhereEmpty();
        var crews = await db.Alliances.AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => x.Name)
            .ToListAsync(ct);

        var customTitles = await db.CustomTitles.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Title)
            .Select(x => new KeyValuePair<string, string>(x.Key, x.Title))
            .ToListAsync(ct);
        var created = 0;
        var reused = 0;
        var errors = new List<string>();
        foreach (var target in AutomaticRoleTargets(gameOptions.Value, crews, customTitles))
        {
            var map = target.Kind switch
            {
                "city" => cityMap,
                "crew" => crewMap,
                _ => titleMap,
            };

            if (map.TryGetValue(target.Key, out var mappedRoleId) && rolesById.ContainsKey(mappedRoleId))
                continue;

            if (rolesByName.TryGetValue(target.RoleName, out var existing))
            {
                map[target.Key] = existing.Id;
                reused++;
                continue;
            }

            var made = await CreateGuildRoleAsync(settings, target.RoleName, target.Color, ct);
            if (made is null)
            {
                errors.Add($"Could not create role {target.RoleName}.");
                continue;
            }

            map[target.Key] = made.Id;
            rolesById[made.Id] = made;
            rolesByName.TryAdd(made.Name, made);
            created++;
        }

        row.DiscordCityRoleMapJson = RoleMapJson(cityMap);
        row.DiscordCrewRoleMapJson = RoleMapJson(crewMap);
        row.DiscordTitleRoleMapJson = RoleMapJson(titleMap);
        var now = DateTime.UtcNow;
        row.UpdatedAtUtc = now;
        row.UpdatedBy = updatedBy;
        await db.SaveChangesAsync(ct);

        return new DiscordRoleEnsureResponse(
            cityMap.Count + crewMap.Count + titleMap.Count,
            created,
            reused,
            cityMap.Count,
            crewMap.Count,
            titleMap.Count,
            errors,
            now);
    }

    public async Task<DiscordCrewChannelSyncResponse> SyncCrewChannelsAsync(string? updatedBy, CancellationToken ct)
    {
        var row = await SettingsRowAsync(ct);
        var settings = Effective(row);
        if (!settings.BotConfigured)
            throw new GameRuleException("Add a bot token and guild id before syncing Discord crew channels.");

        var crews = await db.Alliances.AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => x.Name)
            .ToListAsync(ct);
        if (crews.Count == 0)
            throw new GameRuleException("There are no crews to mirror into Discord yet.");

        var guildChannels = await GuildChannelsAsync(settings, ct);
        var channelsById = guildChannels.ToDictionary(x => x.Id, StringComparer.Ordinal);
        var channelsByName = guildChannels
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        var channelMap = settings.CrewChannels.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);

        var created = 0;
        var reused = 0;
        var updated = 0;
        var errors = new List<string>();
        foreach (var crew in crews)
        {
            if (!settings.CrewRoles.TryGetValue(crew, out var crewRoleId))
            {
                errors.Add($"{crew}: create a crew role map before syncing a private channel.");
                continue;
            }

            var name = CrewChannelName(crew);
            var topic = $"Private Street Empire crew room for {crew}.";
            DiscordGuildChannel? channel = null;
            if (channelMap.TryGetValue(crew, out var mappedChannelId) && channelsById.TryGetValue(mappedChannelId, out var mapped))
            {
                channel = mapped;
            }
            else if (channelsByName.TryGetValue(name, out var existing))
            {
                channel = existing;
                channelMap[crew] = existing.Id;
                reused++;
            }

            if (channel is null)
            {
                var made = await CreateCrewChannelAsync(settings, crew, crewRoleId, ct);
                if (made is null)
                {
                    errors.Add($"{crew}: Discord refused channel creation.");
                    continue;
                }

                channelMap[crew] = made.Id;
                channelsById[made.Id] = made;
                channelsByName.TryAdd(made.Name, made);
                created++;
                continue;
            }

            if (await UpdateCrewChannelAsync(settings, channel.Id, name, topic, crewRoleId, ct))
                updated++;
            else
                errors.Add($"{crew}: Discord refused the private channel update.");
        }

        row.DiscordCrewChannelMapJson = RoleMapJson(channelMap);
        var now = DateTime.UtcNow;
        row.DiscordCrewChannelsSyncedAtUtc = now;
        row.UpdatedAtUtc = now;
        row.UpdatedBy = updatedBy;
        await db.SaveChangesAsync(ct);

        return new DiscordCrewChannelSyncResponse(
            crews.Count,
            channelMap.Count,
            created,
            reused,
            updated,
            errors.Take(20).ToList(),
            now);
    }

    internal static IReadOnlyList<AutomaticRoleTarget> AutomaticRoleTargets(
        GameOptions options,
        IReadOnlyList<string> crewNames,
        IReadOnlyList<KeyValuePair<string, string>>? customTitles = null)
    {
        options.Territory.ApplyDefaultsWhereEmpty();
        var targets = new List<AutomaticRoleTarget>();
        targets.AddRange(options.Territory.Cities()
            .Select(city => new AutomaticRoleTarget("city", city, RoleName("City", city), 0x1f8b4c)));
        targets.AddRange(crewNames
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Select(crew => new AutomaticRoleTarget("crew", crew, RoleName("Crew", crew), 0xd4af37)));
        targets.AddRange(TitleCategories.All
            .Select(title => new AutomaticRoleTarget("title", title.Key, RoleName("Title", title.Title), 0xc0392b)));
        targets.Add(new AutomaticRoleTarget("title", TitleService.DiscordConnectedKey, RoleName("Title", TitleService.DiscordConnectedTitle), 0x5865f2));
        targets.AddRange((customTitles ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x.Key) && !string.IsNullOrWhiteSpace(x.Value))
            .DistinctBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(title => new AutomaticRoleTarget("title", title.Key, RoleName("Title", title.Value), 0x8e44ad)));
        return targets;
    }

    private static string RoleName(string group, string value)
    {
        var clean = string.Join(' ', value.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
        var name = $"StreetEmpire {group} - {clean}";
        return name.Length <= 100 ? name : name[..100];
    }

    private static string CrewChannelName(string crew)
    {
        var builder = new StringBuilder("street-crew-");
        var lastWasDash = false;
        foreach (var ch in crew.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
                lastWasDash = false;
            }
            else if (!lastWasDash)
            {
                builder.Append('-');
                lastWasDash = true;
            }
        }

        var name = builder.ToString().TrimEnd('-');
        return name.Length <= 90 ? name : name[..90].TrimEnd('-');
    }

    private static IEnumerable<string> ManagedRoleIds(EffectiveDiscordSettings settings)
        => new[] { settings.LinkedRoleId, settings.TopTenRoleId, settings.CrewBossRoleId }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Concat(settings.CityRoles.Values)
            .Concat(settings.CrewRoles.Values)
            .Concat(settings.TitleRoles.Values)
            .Distinct(StringComparer.Ordinal);

    internal static IEnumerable<string> DesiredRoleIds(
        EffectiveDiscordSettings settings,
        Player player,
        int rank,
        IReadOnlyList<PlayerTitleResponse> titleBoard)
    {
        if (!string.IsNullOrWhiteSpace(settings.LinkedRoleId))
            yield return settings.LinkedRoleId;
        if (rank <= 10 && !string.IsNullOrWhiteSpace(settings.TopTenRoleId))
            yield return settings.TopTenRoleId;
        if (player.Alliance?.FounderId == player.Id && !string.IsNullOrWhiteSpace(settings.CrewBossRoleId))
            yield return settings.CrewBossRoleId;
        if (settings.CityRoles.TryGetValue(player.City, out var cityRole))
            yield return cityRole;
        if (player.Alliance is not null && settings.CrewRoles.TryGetValue(player.Alliance.Name, out var crewRole))
            yield return crewRole;
        foreach (var title in TitleService.AccountTitles(player.Account)
                     .Concat(titleBoard.Where(x => x.PlayerId == player.Id)))
        {
            if (settings.TitleRoles.TryGetValue(title.Key, out var titleRole))
                yield return titleRole;
        }
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

    private async Task<IReadOnlyList<DiscordGuildRole>> GuildRolesAsync(EffectiveDiscordSettings settings, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiRoot}/guilds/{settings.GuildId}/roles");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bot", settings.BotToken);
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new GameRuleException($"Discord refused to list guild roles with {(int)response.StatusCode} {response.ReasonPhrase}.");

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            return [];

        return document.RootElement.EnumerateArray()
            .Select(role => new DiscordGuildRole(
                role.TryGetProperty("id", out var id) ? id.GetString() ?? string.Empty : string.Empty,
                role.TryGetProperty("name", out var name) ? name.GetString() ?? string.Empty : string.Empty))
            .Where(x => !string.IsNullOrWhiteSpace(x.Id) && !string.IsNullOrWhiteSpace(x.Name))
            .ToList();
    }

    private async Task<IReadOnlyList<DiscordGuildChannel>> GuildChannelsAsync(EffectiveDiscordSettings settings, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiRoot}/guilds/{settings.GuildId}/channels");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bot", settings.BotToken);
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new GameRuleException($"Discord refused to list guild channels with {(int)response.StatusCode} {response.ReasonPhrase}.");

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            return [];

        return document.RootElement.EnumerateArray()
            .Select(channel => new DiscordGuildChannel(
                channel.TryGetProperty("id", out var id) ? id.GetString() ?? string.Empty : string.Empty,
                channel.TryGetProperty("name", out var name) ? name.GetString() ?? string.Empty : string.Empty,
                channel.TryGetProperty("type", out var type) && type.TryGetInt32(out var parsedType) ? parsedType : -1))
            .Where(x => x.Type == 0 && !string.IsNullOrWhiteSpace(x.Id) && !string.IsNullOrWhiteSpace(x.Name))
            .ToList();
    }

    private async Task<DiscordGuildChannel?> CreateCrewChannelAsync(EffectiveDiscordSettings settings, string crew, string crewRoleId, CancellationToken ct)
    {
        var name = CrewChannelName(crew);
        try
        {
            using var response = await SendBotJsonAsync(settings, HttpMethod.Post, $"/guilds/{settings.GuildId}/channels", new
            {
                name,
                type = 0,
                topic = $"Private Street Empire crew room for {crew}.",
                permission_overwrites = CrewChannelPermissions(settings.GuildId!, crewRoleId)
            }, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Discord refused channel creation for {CrewName} with {Status} {Reason}.", crew, (int)response.StatusCode, response.ReasonPhrase);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = document.RootElement;
            var id = root.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
            var channelName = root.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : name;
            return string.IsNullOrWhiteSpace(id) ? null : new DiscordGuildChannel(id, channelName ?? name, 0);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "Could not create Discord crew channel for {CrewName}.", crew);
            return null;
        }
    }

    private async Task<bool> UpdateCrewChannelAsync(EffectiveDiscordSettings settings, string channelId, string name, string topic, string crewRoleId, CancellationToken ct)
    {
        try
        {
            using var response = await SendBotJsonAsync(settings, new HttpMethod("PATCH"), $"/channels/{channelId}", new
            {
                name,
                topic,
                permission_overwrites = CrewChannelPermissions(settings.GuildId!, crewRoleId)
            }, ct);
            if (response.IsSuccessStatusCode)
                return true;

            logger.LogWarning("Discord refused channel update for {ChannelId} with {Status} {Reason}.", channelId, (int)response.StatusCode, response.ReasonPhrase);
            return false;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Could not update Discord crew channel {ChannelId}.", channelId);
            return false;
        }
    }

    private static object[] CrewChannelPermissions(string guildId, string crewRoleId)
        =>
        [
            new { id = guildId, type = 0, allow = "0", deny = "1024" },
            new { id = crewRoleId, type = 0, allow = "68608", deny = "0" }
        ];

    private async Task<DiscordGuildRole?> CreateGuildRoleAsync(EffectiveDiscordSettings settings, string name, int color, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiRoot}/guilds/{settings.GuildId}/roles");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bot", settings.BotToken);
        request.Headers.TryAddWithoutValidation("X-Audit-Log-Reason", Uri.EscapeDataString("Street Empire role auto-create"));
        request.Content = JsonContent.Create(new
        {
            name,
            color,
            hoist = false,
            mentionable = false
        }, options: JsonOptions);

        try
        {
            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Discord refused role creation for {RoleName} with {Status} {Reason}.", name, (int)response.StatusCode, response.ReasonPhrase);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = document.RootElement;
            var id = root.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
            var roleName = root.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : name;
            return string.IsNullOrWhiteSpace(id) ? null : new DiscordGuildRole(id, roleName ?? name);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "Could not create Discord role {RoleName}.", name);
            return null;
        }
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
        var crewMap = string.IsNullOrWhiteSpace(row.DiscordCrewRoleMapJson)
            ? fallback.CrewRoleMap
            : row.DiscordCrewRoleMapJson;
        var titleMap = string.IsNullOrWhiteSpace(row.DiscordTitleRoleMapJson)
            ? fallback.TitleRoleMap
            : row.DiscordTitleRoleMapJson;
        var crewChannelMap = string.IsNullOrWhiteSpace(row.DiscordCrewChannelMapJson)
            ? fallback.CrewChannelMap
            : row.DiscordCrewChannelMapJson;
        return new EffectiveDiscordSettings(
            First(row.DiscordBotToken, fallback.BotToken),
            First(row.DiscordApplicationId, fallback.ApplicationId),
            First(row.DiscordPublicKey, fallback.PublicKey),
            First(row.DiscordGuildId, fallback.GuildId),
            First(row.DiscordLinkedRoleId, fallback.LinkedRoleId),
            First(row.DiscordTopTenRoleId, fallback.TopTenRoleId),
            First(row.DiscordCrewBossRoleId, fallback.CrewBossRoleId),
            ParseCityRoleMap(cityMap),
            ParseCrewRoleMap(crewMap),
            ParseCrewChannelMap(crewChannelMap),
            ParseTitleRoleMap(titleMap));
    }

    private static string? First(string? stored, string? configured)
        => !string.IsNullOrWhiteSpace(stored) ? stored.Trim()
            : !string.IsNullOrWhiteSpace(configured) ? configured.Trim()
            : null;

    internal sealed record EffectiveDiscordSettings(
        string? BotToken,
        string? ApplicationId,
        string? PublicKey,
        string? GuildId,
        string? LinkedRoleId,
        string? TopTenRoleId,
        string? CrewBossRoleId,
        IReadOnlyDictionary<string, string> CityRoles,
        IReadOnlyDictionary<string, string> CrewRoles,
        IReadOnlyDictionary<string, string> CrewChannels,
        IReadOnlyDictionary<string, string> TitleRoles)
    {
        public bool BotConfigured => !string.IsNullOrWhiteSpace(BotToken) && !string.IsNullOrWhiteSpace(GuildId);
        public bool SlashCommandsConfigured => BotConfigured && !string.IsNullOrWhiteSpace(ApplicationId) && !string.IsNullOrWhiteSpace(PublicKey);
        public bool RoleSyncConfigured => BotConfigured
            && (!string.IsNullOrWhiteSpace(LinkedRoleId)
                || !string.IsNullOrWhiteSpace(TopTenRoleId)
                || !string.IsNullOrWhiteSpace(CrewBossRoleId)
                || CityRoles.Count > 0
                || CrewRoles.Count > 0
                || TitleRoles.Count > 0);
    }

    private sealed record DiscordRoleResult(bool Success, bool NotInGuild, string? Error)
    {
        public static readonly DiscordRoleResult Ok = new(true, false, null);
    }

    private sealed record DiscordMemberRolesResult(bool Success, bool NotInGuild, IReadOnlyList<string> Roles, string? Error)
    {
        public static readonly DiscordMemberRolesResult MemberMissing = new(false, true, [], null);
    }

    private sealed record DiscordGuildRole(string Id, string Name);
    private sealed record DiscordGuildChannel(string Id, string Name, int Type);
}

internal sealed record AutomaticRoleTarget(string Kind, string Key, string RoleName, int Color);
