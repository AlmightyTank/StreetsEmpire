using System.Text.Json;

namespace StreetEmpire.Api.Services;

/// <summary>
/// The typing-to-values layer: bot tokens, snowflakes, public keys and the role maps an admin writes
/// by hand in a textarea.
///
/// All of it is static and none of it touches the database, Discord or the clock, which is why it is
/// worth having apart from the rest - these are the members with the most callers and the fewest
/// reasons to change, and they were previously buried between two sets of network calls.
///
/// The maps are stored as JSON and edited as lines of 'Name=123456789', so every one of these pairs a
/// parser with a writer. Refusing bad input here is the whole point: a role id that is not a number
/// fails in the admin panel with a sentence, rather than at 3am as a Discord 400 nobody sees.
/// </summary>
public sealed partial class DiscordGuildIntegration
{
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

    /// <summary>
    /// The address the game answers on, as a bare origin the bot can hang a path off.
    ///
    /// Refused rather than repaired when it is not an absolute http(s) address, because the failure
    /// this is guarding against is silent: a bad address still renders as a button, and a button that
    /// goes nowhere is worse than no button at all. The trailing slash comes off here so the link
    /// builder never has to decide whether it is looking at one.
    /// </summary>
    public static string? NormalizePublicUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim().TrimEnd('/');
        if (trimmed.Length is 0 or > 256
            || !Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new GameRuleException("The public address must be the full address of the game, like https://streetempire.example.");
        return trimmed;
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
        => ParseNamedRoleMap(value, "City", "City roles use one mapping per line, like Chicago=123456789.");

    public static Dictionary<string, string> ParseCrewRoleMap(string? value)
        => ParseNamedRoleMap(value, "Crew", "Crew roles use one mapping per line, like The Eastside Table=123456789.");

    public static Dictionary<string, string> ParseCrewChannelMap(string? value)
        => ParseNamedRoleMap(value, "Crew channel", "Crew channels use one mapping per line, like The Eastside Table=123456789.");

    public static Dictionary<string, string> ParseTitleRoleMap(string? value)
        => ParseNamedRoleMap(value, "Title", "Title roles use one mapping per line, like killer=123456789.");

    private static Dictionary<string, string> ParseNamedRoleMap(string? value, string label, string formatError)
    {
        var roles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(value))
            return roles;

        var trimmed = value.Trim();
        if (trimmed.StartsWith("{", StringComparison.Ordinal))
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(trimmed, JsonOptions) ?? [];
            foreach (var pair in parsed)
                AddNamedRole(roles, pair.Key, pair.Value, label);
            return roles;
        }

        foreach (var line in trimmed.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var clean = line.Trim();
            if (clean.Length == 0) continue;
            var separator = clean.IndexOf('=');
            if (separator < 0) separator = clean.IndexOf(':');
            if (separator < 0)
                throw new GameRuleException(formatError);
            AddNamedRole(roles, clean[..separator], clean[(separator + 1)..], label);
        }

        return roles;
    }

    public static string CityRoleMapJson(string? value)
        => RoleMapJson(ParseCityRoleMap(value));

    public static string CrewRoleMapJson(string? value)
        => RoleMapJson(ParseCrewRoleMap(value));

    public static string CrewChannelMapJson(string? value)
        => RoleMapJson(ParseCrewChannelMap(value));

    public static string TitleRoleMapJson(string? value)
        => RoleMapJson(ParseTitleRoleMap(value));

    private static string RoleMapJson(IReadOnlyDictionary<string, string> roles)
        => roles.Count == 0 ? string.Empty : JsonSerializer.Serialize(roles, JsonOptions);

    public static string CityRoleMapText(IReadOnlyDictionary<string, string> roles)
        => RoleMapText(roles);

    public static string RoleMapText(IReadOnlyDictionary<string, string> roles)
        => string.Join('\n', roles.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).Select(x => $"{x.Key}={x.Value}"));

    private static void AddNamedRole(Dictionary<string, string> roles, string nameValue, string roleValue, string label)
    {
        var name = nameValue.Trim();
        if (name.Length is < 2 or > 64)
            throw new GameRuleException($"{label} names in the Discord role map must be 2-64 characters.");
        roles[name] = NormalizeSnowflake(roleValue) ?? throw new GameRuleException($"Every {label.ToLowerInvariant()} role needs a Discord role id.");
    }
}
