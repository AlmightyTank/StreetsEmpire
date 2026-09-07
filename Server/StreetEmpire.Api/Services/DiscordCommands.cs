using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Chaos.NaCl;
using Microsoft.EntityFrameworkCore;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

/// <summary>
/// The half of the bot a player actually talks to: what the slash commands are, who is allowed to see
/// each answer, and how an answer is shaped on its way back to Discord.
///
/// Split from the guild half rather than from the class. Role sync and command answers share the bot
/// token, the settings row and the HTTP client, and separating them into two objects would have meant
/// threading all three through a constructor to buy nothing; separating the files buys the thing that
/// was actually wrong, which is that both used to be read past to reach the other.
///
/// Nothing here changes game state. Every command is a read, and the only component ever written is a
/// link button, which Discord renders without an interaction handler and which leads out to the site
/// rather than back to this server - so no message the bot writes can spend anything by being clicked.
/// </summary>
public sealed partial class DiscordGuildIntegration
{
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
            new
            {
                name = "play",
                type = 1,
                description = "Open Street Empire.",
                options = Array.Empty<object>()
            },
            new
            {
                name = "me",
                type = 1,
                description = "Your cash, bank, worth, city, heat, crew and rank.",
                options = Array.Empty<object>()
            },
            new
            {
                name = "online",
                type = 1,
                description = "How many players have been on recently.",
                options = Array.Empty<object>()
            },
            new
            {
                name = "crew",
                type = 1,
                description = "Your crew, in as much detail as you want.",
                options = new[]
                {
                    SubCommand("status", "Size, worth, treasury and ground.", CrewOption()),
                    SubCommand("members", "Who is in it, by rank.", CrewOption()),
                    SubCommand("territories", "The ground it holds and how well it is held.", CrewOption()),
                    SubCommand("wars", "Wars it is currently in.", CrewOption()),
                    SubCommand("leaderboard", "Every crew, richest first."),
                    SubCommand("activity", "The fights its members have been in.", CrewOption()),
                }
            },
            new
            {
                name = "leaderboard",
                type = 1,
                description = "The richest empires in the game.",
                options = Array.Empty<object>()
            },
            new
            {
                name = "casino",
                type = 1,
                description = "Your casino standing, comps and free spins.",
                options = Array.Empty<object>()
            },
            new
            {
                name = "jackpot",
                type = 1,
                description = "What the progressive is sitting at.",
                options = Array.Empty<object>()
            },
            new
            {
                name = "travel",
                type = 1,
                description = "Where you are and what it costs to be somewhere else.",
                options = Array.Empty<object>()
            },
            new
            {
                name = "season",
                type = 1,
                description = "The season clock and who is winning it.",
                options = Array.Empty<object>()
            },
            new
            {
                name = "wanted",
                type = 1,
                description = "What the dealer is asking for, and what it pays.",
                options = new[] { StringOption("city", "City name. Leave blank to use your own.", required: false) }
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
            return InteractionResponse(Say("Street Empire did not understand that Discord interaction."), ephemeral: true);

        var name = data.GetProperty("name").GetString()?.Trim().ToLowerInvariant();
        var discordUserId = DiscordUserId(root);
        await LoadPublicAddressAsync(ct);
        // Named apart from the injected options rather than shadowing them, so a command body below can
        // still reach the bot's own settings - which is how any of them get a link to the game.
        var (sub, commandOptions) = Arguments(data);
        var content = name switch
        {
            "profile" => await ProfileCommandAsync(commandOptions.GetValueOrDefault("player"), discordUserId, ct),
            "rank" => await RankCommandAsync(commandOptions.GetValueOrDefault("player"), discordUserId, ct),
            "market" => await MarketCommandAsync(commandOptions.GetValueOrDefault("city"), discordUserId, ct),
            "streetwire" => await StreetWireCommandAsync(ct),
            "play" => PlayCommand(),
            "me" => await MeCommandAsync(discordUserId, ct),
            "online" => await OnlineCommandAsync(ct),
            "crew" => await CrewCommandAsync(sub, commandOptions.GetValueOrDefault("crew"), discordUserId, ct),
            "leaderboard" => await LeaderboardCommandAsync(ct),
            "casino" => await CasinoCommandAsync(discordUserId, ct),
            "jackpot" => await JackpotCommandAsync(ct),
            "travel" => await TravelCommandAsync(discordUserId, ct),
            "season" => await SeasonCommandAsync(ct),
            "wanted" => await WantedCommandAsync(commandOptions.GetValueOrDefault("city"), discordUserId, ct),
            _ => Say("Street Empire does not have that command yet.")
        };

        return InteractionResponse(content, AnswersEphemerally(sub is null ? name : $"{name} {sub}"));
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
    /// The holding answer, and the only place a slash command's audience is ever decided.
    ///
    /// Discord fixes ephemerality here: the follow-up edit can neither widen nor narrow it afterwards.
    /// This used to be hardcoded private because the command name had not been parsed yet when the
    /// deferral went out - which was true, and meant the Public/Ephemeral choice the command bodies
    /// were making got dropped on the floor for every real interaction. The name is cheap to read off
    /// the callback before deferring, so the caller reads it and passes the answer in.
    /// </summary>
    public static object DeferredInteractionResponse(bool ephemeral)
        => new
        {
            type = 5,
            data = new
            {
                flags = ephemeral ? 64 : 0,
                allowed_mentions = new { parse = Array.Empty<string>() }
            }
        };

    /// <summary>
    /// Which commands answer the caller alone.
    ///
    /// Private by default, and deliberately a list of the public ones rather than the private ones: a
    /// command missing from here is one nobody has thought about yet, and showing somebody's bank
    /// balance to a channel is the worse of the two mistakes to make by omission.
    /// </summary>
    private static readonly HashSet<string> PublicCommands = new(StringComparer.Ordinal)
    {
        "profile",
        "rank",
        "market",
        "streetwire",
        // Advertising and scoreboards. These are the answers somebody would screenshot into the channel
        // anyway, and the whole point of /play and /jackpot is that other people see them.
        "play",
        "online",
        "leaderboard",
        "jackpot",
        "season",
        "wanted",
        // A board of crews is public the way the player board is. Everything else under /crew - the
        // treasury, who is in it, where its ground is thin - is what a rival would pay for.
        "crew leaderboard",
    };

    /// <summary>
    /// How recently a session must have been seen for its owner to count as around.
    ///
    /// Nothing in the game reports being online - there is only the last time each session was used -
    /// so this is a window rather than a state, and the answer says so rather than claiming a count of
    /// people who are looking at it right now.
    /// </summary>
    internal const int OnlineWindowMinutes = 15;

    internal static bool AnswersEphemerally(string? commandName)
        => string.IsNullOrWhiteSpace(commandName) || !PublicCommands.Contains(commandName);

    /// <summary>
    /// The slash command a callback is asking for, lowercased, or null when it names none.
    ///
    /// A subcommand is part of the name here - "crew leaderboard" rather than "crew" - because who may
    /// see the answer differs between them: a crew board is a board, and a crew treasury is not. One
    /// key for the whole thing means the visibility table cannot be read one way at the deferral and
    /// another way at the dispatch.
    /// </summary>
    public static string? CommandName(JsonDocument document)
    {
        var root = document.RootElement;
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            return null;
        if (!data.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String)
            return null;

        var value = name.GetString()?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var (sub, _) = Arguments(data);
        return sub is null ? value : $"{value} {sub}";
    }

    /// <summary>
    /// A command's subcommand, if it has one, and the options belonging to whichever level is in play.
    ///
    /// Discord nests a subcommand's own options inside it rather than beside the command's, so reading
    /// the top level for both would find the subcommand sitting where a value was expected and nothing
    /// at all where the arguments were.
    /// </summary>
    private static (string? Sub, Dictionary<string, string> Options) Arguments(JsonElement data)
    {
        if (data.TryGetProperty("options", out var options)
            && options.ValueKind == JsonValueKind.Array
            && options.GetArrayLength() > 0)
        {
            var first = options[0];
            var isSub = first.TryGetProperty("type", out var type)
                && type.ValueKind == JsonValueKind.Number
                && type.GetInt32() == 1;
            if (isSub)
            {
                var name = first.TryGetProperty("name", out var subName) ? subName.GetString()?.Trim().ToLowerInvariant() : null;
                return (string.IsNullOrWhiteSpace(name) ? null : name, CommandOptions(first));
            }
        }

        return (null, CommandOptions(data));
    }

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
        return Say($"{player.Name} runs {player.City}. Rank #{rank:N0}, worth {netWorth:C0}. Crew: {crew}.", LinkRow(("Open Street Empire", "overview")));
    }

    private async Task<DiscordCommandText> RankCommandAsync(string? playerName, string? discordUserId, CancellationToken ct)
    {
        var player = await ResolvePlayerAsync(playerName, discordUserId, ct);
        if (player is null)
            return NeedPlayer(playerName);

        var rank = await RankOfAsync(player, ct);
        return Say($"{player.Name} is #{rank:N0} in Street Empire with {economy.CalculateNetWorth(player):C0} net worth.");
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
        return Say($"{resolved} market: weed {weed:C0}, coke {coke:C0}. Travel {markets.TravelTurns(resolved):N0} turn(s), bust risk {markets.BustChancePercent(resolved)}%.", LinkRow(("Open the market", "market")));
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
            return Say("Street Wire is quiet right now.");

        var version = string.IsNullOrWhiteSpace(post.Version) ? string.Empty : $" [{post.Version}]";
        return Say($"{post.Title}{version}: {OneLine(post.Body, 280)}", LinkRow(("Read the full update", "updates")));
    }

    /// <summary>
    /// A way in, and nothing else.
    ///
    /// The cheapest command in the set and the one most worth having: a link button needs no handler
    /// behind it, so this is a whole feature that cannot break at three in the morning.
    /// </summary>
    private DiscordCommandText PlayCommand()
    {
        var row = LinkRow(("Play Street Empire", null), ("The casino floor", "casino"));
        return row is null
            ? Say("Street Empire has not been told its own address yet, so there is no link to hand you. An admin can set it under Admin -> Discord.")
            : Say("Build your empire on the streets.", row);
    }

    /// <summary>
    /// The whole empire in four lines, for the player who does not want to remember nine commands.
    ///
    /// Answers the caller alone, and it is the reason the private default exists: this is the one that
    /// reads out a bank balance, and a channel is the last place that belongs.
    /// </summary>
    private async Task<DiscordCommandText> MeCommandAsync(string? discordUserId, CancellationToken ct)
    {
        var player = await ResolvePlayerAsync(null, discordUserId, ct);
        if (player is null)
            return NeedPlayer(null);

        var now = DateTime.UtcNow;
        var rank = await RankOfAsync(player, ct);
        var netWorth = economy.CalculateNetWorth(player);
        var heat = HeatBands.Label(HeatBands.Of(player.Heat, gameOptions.Value.Hideout));
        var crew = player.Alliance is null
            ? "Independent"
            : $"{player.Alliance.Name} ({AllianceRanks.Label(player.AllianceRank)})";
        var where = player.IsInTransit(now)
            ? $"In the air, landing in {MinutesOut(player, now):N0} minute(s)"
            : player.City;

        return Say(
            $"""
            **{player.Name}** - rank #{rank:N0}
            Cash {player.Cash:C0} | Bank {player.BankCash:C0} | Worth {netWorth:C0}
            {where} | Heat: {heat} | Turns: {player.Turns:N0}
            Crew: {crew}
            """,
            LinkRow(("Open your empire", "overview"), ("Your crew", "alliance")));
    }

    /// <summary>
    /// How busy the game is, said as the window it actually measures.
    ///
    /// Nothing records being online; there is only the last time each session was used. So this counts
    /// accounts seen inside <see cref="OnlineWindowMinutes"/> and says so, rather than reporting a
    /// number of people currently looking at the game that it has no way to know.
    ///
    /// Bots are excluded. The world runs a simulation of them and counting those would turn this into
    /// a number that is always large and never means anything.
    /// </summary>
    private async Task<DiscordCommandText> OnlineCommandAsync(CancellationToken ct)
    {
        var since = DateTime.UtcNow.AddMinutes(-OnlineWindowMinutes);
        var online = await db.Sessions.AsNoTracking()
            .Where(x => x.RevokedAtUtc == null && x.LastSeenAtUtc >= since && !x.Account.IsBot)
            .Select(x => x.AccountId)
            .Distinct()
            .CountAsync(ct);

        return Say(
            online == 0
                ? $"Nobody has been on in the last {OnlineWindowMinutes:N0} minutes. The streets are yours."
                : $"{online:N0} player(s) have been on in the last {OnlineWindowMinutes:N0} minutes.",
            LinkRow(("Play Street Empire", null)));
    }

    /// <summary>
    /// What a crew is worth and how much ground it is standing on.
    ///
    /// Answers the caller alone even though a crew is a public thing, because the treasury is not: a
    /// rival reading how much a crew can spend on defence is intelligence the game charges for.
    /// Phase 3 posts crew reports into the crew's own room, which is where the treasury belongs.
    /// </summary>
    private static object CrewOption()
        => StringOption("crew", "Crew name. Leave blank to use your own.", required: false);

    /// <summary>
    /// Everything under /crew. The subcommand picks the question; resolving whose crew is being asked
    /// about is the same job in every one of them, so it happens once here.
    /// </summary>
    private async Task<DiscordCommandText> CrewCommandAsync(string? sub, string? crewName, string? discordUserId, CancellationToken ct)
    {
        // The one question that is not about a particular crew.
        if (sub == "leaderboard")
            return await CrewLeaderboardCommandAsync(discordUserId, ct);

        var crew = await ResolveCrewAsync(crewName, discordUserId, ct);
        if (crew is null)
            return Say(string.IsNullOrWhiteSpace(crewName)
                ? "You are not in a crew, so name one and I will look it up."
                : $"No Street Empire crew named {crewName.Trim()}.");

        return sub switch
        {
            "members" => await CrewMembersAsync(crew, ct),
            "territories" => await CrewTerritoriesAsync(crew, ct),
            "wars" => await CrewWarsAsync(crew, ct),
            "activity" => await CrewActivityAsync(crew, ct),
            _ => await CrewStatusAsync(crew, ct),
        };
    }

    private async Task<DiscordCommandText> CrewStatusAsync(Alliance crew, CancellationToken ct)
    {
        var members = await MembersOfAsync(crew, ct);
        var ground = await db.Territories.AsNoTracking()
            .CountAsync(x => x.Holder != null && x.Holder.AllianceId == crew.Id, ct);
        var worth = members.Sum(x => economy.CalculateNetWorth(x));
        var boss = members
            .OrderByDescending(x => x.AllianceRank)
            .ThenBy(x => x.AllianceJoinedAtUtc)
            .FirstOrDefault();

        return Say(
            $"""
            **{crew.Name}** - {members.Count:N0} member(s)
            Worth {worth:C0} | Treasury {crew.Treasury:C0}
            Ground held: {ground:N0} | Thugs {crew.OffensiveThugs + crew.DefensiveThugs:N0}
            Top of the table: {boss?.Name ?? "nobody yet"}
            """,
            LinkRow(("Open the crew", "alliance")));
    }

    private async Task<DiscordCommandText> CrewMembersAsync(Alliance crew, CancellationToken ct)
    {
        var members = await MembersOfAsync(crew, ct);
        if (members.Count == 0)
            return Say($"{crew.Name} has nobody in it.");

        var rows = members
            .OrderByDescending(x => x.AllianceRank)
            .ThenByDescending(x => economy.CalculateNetWorth(x))
            .Take(15)
            .Select(x => $"{x.Name} - {AllianceRanks.Label(x.AllianceRank)}, {economy.CalculateNetWorth(x):C0} ({x.City})");
        var more = members.Count > 15 ? $"\nAnd {members.Count - 15:N0} more." : string.Empty;

        return Say(
            $"**{crew.Name}**\n" + string.Join('\n', rows) + more,
            LinkRow(("Open the crew", "alliance")));
    }

    /// <summary>
    /// The ground the crew is standing on, thinnest garrison first.
    ///
    /// Ordered that way because the useful question is never "what do we hold" - the crew knows - but
    /// "which of it would fall tonight", and that is the row at the top rather than one buried in a
    /// list sorted by name.
    /// </summary>
    private async Task<DiscordCommandText> CrewTerritoriesAsync(Alliance crew, CancellationToken ct)
    {
        var ground = await db.Territories.AsNoTracking()
            .Include(x => x.Holder)
            .Where(x => x.Holder != null && x.Holder.AllianceId == crew.Id)
            .OrderBy(x => x.GarrisonThugs)
            .Take(10)
            .ToListAsync(ct);
        if (ground.Count == 0)
            return Say($"{crew.Name} holds no ground.");

        var rows = ground.Select(x =>
            $"{x.Name} ({x.City}) - {x.GarrisonThugs:N0} thug(s), held by {x.Holder!.Name}");
        return Say(
            $"**{crew.Name} ground, thinnest first**\n" + string.Join('\n', rows),
            LinkRow(("Open the map", "recon")));
    }

    private async Task<DiscordCommandText> CrewWarsAsync(Alliance crew, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var wars = await db.AllianceWars.AsNoTracking()
            .Include(x => x.DeclaringAlliance)
            .Include(x => x.TargetAlliance)
            .Where(x => x.Status == AllianceWarStatuses.Active
                && (x.DeclaringAllianceId == crew.Id || x.TargetAllianceId == crew.Id))
            .OrderBy(x => x.EndsAtUtc)
            .ToListAsync(ct);
        if (wars.Count == 0)
            return Say($"{crew.Name} is not at war with anybody.");

        var rows = wars.Select(war =>
        {
            var us = war.DeclaringAllianceId == crew.Id;
            var them = us ? war.TargetAlliance.Name : war.DeclaringAlliance.Name;
            var ourScore = us ? war.DeclaringScore : war.TargetScore;
            var theirScore = us ? war.TargetScore : war.DeclaringScore;
            var left = war.EndsAtUtc - now;
            var clock = left <= TimeSpan.Zero
                ? "settling now"
                : left.TotalHours >= 1 ? $"{left.TotalHours:N0}h left" : $"{Math.Max(1, left.TotalMinutes):N0}m left";
            return $"{them} - {ourScore:N0} to {theirScore:N0}, {clock}, {war.Stake:C0} on it";
        });

        return Say(
            $"**{crew.Name} at war**\n" + string.Join('\n', rows),
            LinkRow(("Open the crew", "alliance")));
    }

    /// <summary>
    /// The fights the crew has been in lately, from either side of them.
    ///
    /// Both sides on purpose: a crew whose members are all being hit reads identically to one whose
    /// members are all out hitting people, if you only count the rows on one side.
    /// </summary>
    private async Task<DiscordCommandText> CrewActivityAsync(Alliance crew, CancellationToken ct)
    {
        var memberIds = await db.Players.AsNoTracking()
            .Where(x => x.AllianceId == crew.Id)
            .Select(x => x.Id)
            .ToListAsync(ct);
        if (memberIds.Count == 0)
            return Say($"{crew.Name} has nobody in it.");

        var fights = await db.CombatLogs.AsNoTracking()
            .Include(x => x.Attacker)
            .Include(x => x.Defender)
            .Where(x => x.Outcome != "Pending"
                && (memberIds.Contains(x.AttackerId) || memberIds.Contains(x.DefenderId)))
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(6)
            .ToListAsync(ct);
        if (fights.Count == 0)
            return Say($"Nobody in {crew.Name} has been in a fight lately.");

        var rows = fights.Select(x =>
        {
            var ours = memberIds.Contains(x.AttackerId);
            var verb = x.Outcome == "Victory" ? "took" : "came off worse against";
            return ours
                ? $"{x.Attacker.Name} {verb} {x.Defender?.Name ?? "somebody"}"
                : $"{x.Attacker.Name} hit {x.Defender?.Name ?? "somebody"} and {(x.Outcome == "Victory" ? "got through" : "did not")}";
        });

        return Say(
            $"**{crew.Name} lately**\n" + string.Join('\n', rows),
            LinkRow(("Open the map", "recon")));
    }

    /// <summary>
    /// Every crew by what its members are worth between them, which is the same total the game's own
    /// crew board is built from.
    /// </summary>
    private async Task<DiscordCommandText> CrewLeaderboardCommandAsync(string? discordUserId, CancellationToken ct)
    {
        var players = await db.Players.AsNoTracking()
            .Include(x => x.Alliance)
            .Include(x => x.Hideout)
            .Where(x => x.AllianceId != null)
            .ToListAsync(ct);
        if (players.Count == 0)
            return Say("Nobody has started a crew yet.");

        var mine = string.IsNullOrWhiteSpace(discordUserId)
            ? null
            : await db.Players.AsNoTracking()
                .Where(x => x.Account.DiscordUserId == discordUserId)
                .Select(x => x.AllianceId)
                .SingleOrDefaultAsync(ct);

        var board = players
            .GroupBy(x => x.AllianceId!.Value)
            .Select(g => new
            {
                Id = g.Key,
                Name = g.First().Alliance!.Name,
                Worth = g.Sum(x => economy.CalculateNetWorth(x)),
                Members = g.Count()
            })
            .OrderByDescending(x => x.Worth)
            .ThenBy(x => x.Id)
            .Take(10)
            .Select((x, index) =>
                $"{index + 1:N0}. {x.Name} - {x.Worth:C0} ({x.Members:N0} member(s)){(x.Id == mine ? "  <- you" : string.Empty)}");

        return Say(
            $"**The crew board**\n" + string.Join('\n', board),
            LinkRow(("Open the crew", "alliance")));
    }

    private async Task<List<Player>> MembersOfAsync(Alliance crew, CancellationToken ct)
        => await db.Players.AsNoTracking()
            .Include(x => x.Hideout)
            .Where(x => x.AllianceId == crew.Id)
            .ToListAsync(ct);

    /// <summary>
    /// The richest empires, counted the same way <c>/rank</c> counts them.
    ///
    /// Bots included, deliberately. They are ranked in game and a board that quietly dropped them would
    /// disagree with the number <c>/me</c> reports for the same player.
    /// </summary>
    private async Task<DiscordCommandText> LeaderboardCommandAsync(CancellationToken ct)
    {
        var top = await db.Players.AsNoTracking()
            .Include(x => x.Alliance)
            .Include(x => x.Hideout)
            .OrderByDescending(economy.NetWorthExpression)
            .ThenBy(x => x.CreatedAtUtc)
            .Take(10)
            .ToListAsync(ct);
        if (top.Count == 0)
            return Say("Nobody has built anything yet.");

        var rows = top.Select((player, index) =>
            $"{index + 1:N0}. {player.Name} - {economy.CalculateNetWorth(player):C0} ({player.City})");
        return Say(
            "**The richest in Street Empire**\n" + string.Join('\n', rows),
            LinkRow(("Take a run at them", null)));
    }

    /// <summary>
    /// What the house owes the caller, and what it is currently holding for everybody.
    ///
    /// Private: comps, free spins and reputation are the caller's own balance sheet. The jackpot half
    /// of it is public and has its own command.
    /// </summary>
    private async Task<DiscordCommandText> CasinoCommandAsync(string? discordUserId, CancellationToken ct)
    {
        var player = await ResolvePlayerAsync(null, discordUserId, ct);
        if (player is null)
            return NeedPlayer(null);

        var board = await casino.BoardAsync(player, ct);
        var spins = board.FreeSpins.Enabled && board.FreeSpins.Owed > 0
            ? $"{board.FreeSpins.Owed:N0} free spin(s) on {board.FreeSpins.MachineName}"
            : "no free spins";
        // The board already carries every machine and its progressive, so the pot comes off that rather
        // than through BiggestPotAsync - which would compute the whole floor a second time.
        var richest = board.SlotMachines
            .OrderByDescending(x => x.Progressive)
            .ThenBy(x => x.Key, StringComparer.Ordinal)
            .FirstOrDefault();

        return Say(
            $"""
            **The Street Empire floor**
            Reputation: {board.Reputation.LevelName} ({board.Reputation.Rep:N0} rep)
            Comps {board.Comps.Balance:N0} | {spins}
            {(richest is null ? "No progressive is running." : $"Biggest progressive: {richest.Name} at {richest.Progressive:C0}")}
            """,
            LinkRow(("Enter the casino", "casino")));
    }

    /// <summary>The progressive, for the channel rather than for one player.</summary>
    private async Task<DiscordCommandText> JackpotCommandAsync(CancellationToken ct)
    {
        var (machine, pot) = await BiggestPotAsync(ct);
        return Say(
            machine is null
                ? "No progressive is running right now."
                : $"The {machine} progressive is sitting at {pot:C0}.",
            LinkRow(("Try your luck", "casino")));
    }

    /// <summary>
    /// Where the caller is, and what every other town costs to reach.
    ///
    /// Private, because "in the air, landing in nine minutes" is a sentence a raider would pay for -
    /// it names a house whose owner cannot answer the door.
    /// </summary>
    private async Task<DiscordCommandText> TravelCommandAsync(string? discordUserId, CancellationToken ct)
    {
        var player = await ResolvePlayerAsync(null, discordUserId, ct);
        if (player is null)
            return NeedPlayer(null);

        var now = DateTime.UtcNow;
        var markets = gameOptions.Value.CityMarkets;
        gameOptions.Value.Territory.ApplyDefaultsWhereEmpty();
        markets.ApplyDefaultsWhereEmpty(gameOptions.Value.Territory.Cities());

        var here = player.IsInTransit(now)
            ? $"In the air, landing in {MinutesOut(player, now):N0} minute(s). Next stop {player.City}."
            : $"You are in {player.City}.";
        var elsewhere = gameOptions.Value.Territory.Cities()
            .Where(city => !string.Equals(city, player.City, StringComparison.OrdinalIgnoreCase))
            .Select(city => $"{city} - {markets.TravelTurns(city):N0} turn(s), bust risk {markets.BustChancePercent(city)}%")
            .ToList();

        return Say(
            elsewhere.Count == 0
                ? here
                : here + "\n" + string.Join('\n', elsewhere),
            LinkRow(("Book a flight", "street")));
    }

    /// <summary>
    /// The season clock and the top of its table.
    ///
    /// Reads the running season straight out of the table rather than through SeasonService.CurrentAsync,
    /// which opens a season when a world has never had one. Somebody typing a slash command should never
    /// be the reason season one starts.
    /// </summary>
    private async Task<DiscordCommandText> SeasonCommandAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var season = await db.Seasons.AsNoTracking()
            .Where(x => x.Status == SeasonStatuses.Running)
            .OrderByDescending(x => x.Number)
            .FirstOrDefaultAsync(ct);
        if (season is null)
            return Say("No season is running right now.");

        var left = season.EndsAtUtc - now;
        var clock = left <= TimeSpan.Zero
            ? "closing now"
            : left.TotalDays >= 1
                ? $"{left.TotalDays:N0} day(s) left"
                : $"{left.TotalHours:N0} hour(s) left";
        var table = await seasons.CurrentTableForAsync(season, 3, ct);
        var leaders = table.Count == 0
            ? "Nobody has taken anything off anybody yet."
            : string.Join(" | ", table.Select(x => $"{x.Rank:N0}. {x.PlayerName}"));

        return Say(
            $"""
            **{season.Name}** - {clock}
            {season.Players:N0} player(s) running.
            {leaders}
            """,
            LinkRow(("See the whole table", "seasons")));
    }

    /// <summary>
    /// The button under an alert DM: the room in the game where that news can be acted on.
    ///
    /// Public because the sweep that sends the DMs lives outside this class, and the map belongs beside
    /// the other link building rather than beside the sweep - a raid alert and /me should never end up
    /// pointing at two different ideas of where the game is.
    ///
    /// The kinds are the ones DefenceAlerts produces. Anything unrecognised gets the overview, which is
    /// always somewhere useful, rather than no button at all.
    /// </summary>
    public IReadOnlyList<object>? AlertLinkRow(string kind) => kind switch
    {
        "attack" or "bust" or "ground" or "groundwork" => LinkRow(("Defend your empire", "recon")),
        "crew" => LinkRow(("Open the crew", "alliance")),
        "arrest" => LinkRow(("Back to the street", "street")),
        "sale" => LinkRow(("Open the market", "market")),
        "labs" or "hideout" or "workshop" => LinkRow(("Open the hideout", "crew")),
        "mule" or "traderjob" => LinkRow(("Open the business", "market")),
        "travel" => LinkRow(("Back on the street", "street")),
        "title" => LinkRow(("See the board", "seasons")),
        _ => LinkRow(("Open Street Empire", "overview")),
    };

    /// <summary>
    /// What the dealer in a town is asking for right now.
    ///
    /// Reads the open rows rather than going through TraderJobService.BookAsync, which tops the book up
    /// when it is thin - that is the right behaviour for a player walking into the shop and the wrong
    /// behaviour here, where somebody typing a slash command would be quietly writing jobs into a town
    /// they may not even be standing in. Same reasoning as /season not opening a season.
    ///
    /// Public, because a board is a board: anybody in that town can already read it, and a job somebody
    /// else fills is a job that was going to be filled anyway.
    /// </summary>
    private async Task<DiscordCommandText> WantedCommandAsync(string? requestedCity, string? discordUserId, CancellationToken ct)
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

        var now = DateTime.UtcNow;
        var open = await db.TraderJobs.AsNoTracking()
            .Where(x => x.City == resolved && x.FilledAtUtc == null && x.ExpiresAtUtc > now)
            .OrderBy(x => x.ExpiresAtUtc)
            .Take(5)
            .ToListAsync(ct);
        if (open.Count == 0)
            return Say($"The book is empty in {resolved} right now.", LinkRow(("Open the business", "market")));

        var rows = open.Select(job =>
        {
            var left = job.ExpiresAtUtc - now;
            var clock = left.TotalHours >= 1
                ? $"{left.TotalHours:N0}h left"
                : $"{Math.Max(1, left.TotalMinutes):N0}m left";
            // The shelf gap is the one job on the board with a consequence for nobody doing it, so it is
            // the one worth marking out from the other three reasons.
            var why = job.Reason == TraderJobReason.ShelfGap ? " (counter is dry)" : string.Empty;
            return $"{job.Remaining:N0} {TradeGoods.Label(job.Good).ToLowerInvariant()} at {job.PricePerUnit:C0} each - {clock}{why}";
        });

        return Say(
            $"**The book in {resolved}**\n" + string.Join('\n', rows),
            LinkRow(("Take the work", "market")));
    }

    /// <summary>The crew a command means: the one that was named, or the caller's own.</summary>
    private async Task<Alliance?> ResolveCrewAsync(string? crewName, string? discordUserId, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(crewName))
        {
            var wanted = crewName.Trim().ToLowerInvariant();
            return await db.Alliances.AsNoTracking().SingleOrDefaultAsync(x => x.Name.ToLower() == wanted, ct);
        }

        if (string.IsNullOrWhiteSpace(discordUserId))
            return null;
        return await db.Players.AsNoTracking()
            .Where(x => x.Account.DiscordUserId == discordUserId && x.Alliance != null)
            .Select(x => x.Alliance!)
            .SingleOrDefaultAsync(ct);
    }

    /// <summary>
    /// The fattest progressive on the floor and the machine holding it.
    ///
    /// The pots come from CasinoService rather than being totalled here, because a jackpot is a seed
    /// plus everything wagered since it last dropped - arithmetic that must not exist in two places.
    /// </summary>
    private async Task<(string? Machine, long Pot)> BiggestPotAsync(CancellationToken ct)
    {
        var pots = await casino.PotsAsync(ct);
        var best = pots.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.Ordinal).FirstOrDefault();
        if (best.Key is null)
            return (null, 0);

        var name = gameOptions.Value.Casino.SlotMachines
            .FirstOrDefault(x => string.Equals(x.Key, best.Key, StringComparison.OrdinalIgnoreCase))?.Name;
        return (string.IsNullOrWhiteSpace(name) ? best.Key : name, best.Value);
    }

    private static double MinutesOut(Player player, DateTime nowUtc)
        => Math.Max(1, Math.Ceiling((player.TravelArrivesAtUtc!.Value - nowUtc).TotalMinutes));

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
            ? Say("Link Discord from your Street Empire account, or pass a player name.")
            : Say($"No Street Empire player named {playerName.Trim()}.");

    private static DiscordCommandText Say(string text, IReadOnlyList<object>? components = null) => new(text, components);

    private static object InteractionResponse(DiscordCommandText content, bool ephemeral)
        => new
        {
            type = 4,
            data = new
            {
                content = content.Text,
                flags = ephemeral ? 64 : 0,
                components = content.Components ?? [],
                allowed_mentions = new { parse = Array.Empty<string>() }
            }
        };

    /// <summary>
    /// The same answer again, shaped for the follow-up edit rather than the first reply.
    ///
    /// It goes back through JSON because the answer is an anonymous type by the time it arrives, and
    /// this used to lift out the content and nothing else - which silently dropped any buttons on the
    /// way to the only path real traffic takes. Flags genuinely are dropped: Discord settled those at
    /// the deferral and will not move them here.
    /// </summary>
    internal static object InteractionMessagePayload(object interactionResponse)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(interactionResponse, JsonOptions));
        if (document.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
        {
            var content = data.TryGetProperty("content", out var contentElement) ? contentElement.GetString() ?? string.Empty : string.Empty;
            var components = data.TryGetProperty("components", out var componentElement) && componentElement.ValueKind == JsonValueKind.Array
                ? (object)componentElement.Clone()
                : Array.Empty<object>();
            return new
            {
                content,
                components,
                allowed_mentions = new { parse = Array.Empty<string>() }
            };
        }

        return new
        {
            content = "Street Empire could not answer that command.",
            components = Array.Empty<object>(),
            allowed_mentions = new { parse = Array.Empty<string>() }
        };
    }

    private string? _publicAddress;
    private bool _publicAddressKnown;

    /// <summary>
    /// Read where the game lives, once, so the link builders below can stay synchronous.
    ///
    /// The address is a stored setting with configuration underneath it, which makes finding it a
    /// database read - and it is wanted at the bottom of twenty command bodies that have no business
    /// knowing that. Every entry point that can produce a button calls this first; DeepLink reads what
    /// it left behind, and falls back to configuration if nobody did.
    ///
    /// Public because the alert sweep needs it too: the sweep hands AlertLinkRow around as a plain
    /// delegate, which cannot go and fetch anything on its own.
    /// </summary>
    public async Task LoadPublicAddressAsync(CancellationToken ct)
    {
        if (_publicAddressKnown) return;
        _publicAddress = Effective(await SettingsRowAsync(ct)).PublicUrl;
        _publicAddressKnown = true;
    }

    /// <summary>
    /// A link into the running game, or null when nobody has said where the game is.
    ///
    /// The client routes on the hash (see Client/src/route.ts), so a page can be named here without the
    /// server owning a route for it - '#/casino' is read by the app at boot and never reaches the host.
    /// The page names are the keys of pageMeta in Client/src/pagecontext.ts: overview, street, crew,
    /// market, casino, recon, seasons, updates, alliance, account.
    /// </summary>
    private string? DeepLink(string? page = null)
    {
        var root = (_publicAddressKnown ? _publicAddress : options.Value.PublicUrl)?.Trim();
        if (string.IsNullOrWhiteSpace(root)
            || !Uri.TryCreate(root, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return null;

        var trimmed = root.TrimEnd('/');
        return string.IsNullOrWhiteSpace(page) ? $"{trimmed}/" : $"{trimmed}/#/{page.Trim().ToLowerInvariant()}";
    }

    /// <summary>
    /// A row of link buttons, or null when there is no public address to point them at.
    ///
    /// Link buttons are the one component Discord renders with nothing behind them, which is what makes
    /// them safe to scatter through read-only answers: a click leaves for the website and never comes
    /// back to this server, so no button written here can be a way to spend somebody's money by mistake.
    ///
    /// Null rather than an empty row when there is no address, because Discord rejects a row holding no
    /// components and would refuse the whole message over a decoration.
    /// </summary>
    private IReadOnlyList<object>? LinkRow(params (string Label, string? Page)[] links)
    {
        var buttons = links
            .Select(link => new { link.Label, Url = DeepLink(link.Page) })
            .Where(link => link.Url is not null)
            .Take(5)
            .Select(link => (object)new { type = 2, style = 5, label = link.Label, url = link.Url })
            .ToList();
        return buttons.Count == 0 ? null : [new { type = 1, components = buttons }];
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

    private static object StringOption(string name, string description, bool required)
        => new { name, description, type = 3, required };

    /// <summary>One subcommand, in the shape Discord wants it nested inside its parent.</summary>
    private static object SubCommand(string name, string description, params object[] options)
        => new { name, description, type = 1, options };

    private static string OneLine(string value, int max)
    {
        var clean = string.Join(' ', value.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
        return clean.Length <= max ? clean : clean[..Math.Max(0, max - 1)] + "...";
    }

    /// <summary>
    /// One command's answer: the sentence, and any buttons under it.
    ///
    /// No ephemerality here on purpose. Who sees the answer is a fact about the command, settled at the
    /// deferral before this record exists - see <see cref="DeferredInteractionResponse"/>.
    /// </summary>
    private sealed record DiscordCommandText(string Text, IReadOnlyList<object>? Components = null);
}
