namespace StreetEmpire.Api.Support;

/// <summary>
/// Reads a <c>.env</c> file into the process environment before configuration is built.
///
/// .NET has no notion of a .env file. What it does have is an environment-variable configuration
/// provider that is already in the chain and already knows how to turn <c>Auth__Email__ApiKey</c> into
/// <c>Auth:Email:ApiKey</c> - so rather than writing a configuration source and arguing about where it
/// sits in the order, this puts the values where that provider will find them and gets out of the way.
/// Every setting in appsettings.json can be overridden this way, not just the secrets.
///
/// A value already present in the real environment is never overwritten. That is the rule every dotenv
/// implementation follows and the one that matters in production: a platform that injects a secret as
/// an environment variable must win over a .env file that got copied into an image by accident.
/// </summary>
internal static class DotEnv
{
    /// <summary>What happened, so the server can say it out loud once it has a logger to say it with.</summary>
    internal sealed record LoadResult(string? Path, int Applied, int SkippedBecauseAlreadySet)
    {
        internal bool Found => Path is not null;
    }

    /// <summary>
    /// Finds the nearest .env and applies it. Never throws: a malformed line is skipped and a missing
    /// file is the normal case, because the game is meant to run with no secrets at all.
    /// </summary>
    /// <param name="fileName">Overridable so the tests can point at a fixture.</param>
    internal static LoadResult Load(string fileName = ".env")
    {
        var path = Find(fileName);
        if (path is null) return new LoadResult(null, 0, 0);

        var applied = 0;
        var skipped = 0;
        foreach (var (key, value) in Parse(File.ReadAllLines(path)))
        {
            // Set, not overwritten. The real environment is the more authoritative of the two.
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
            {
                skipped++;
                continue;
            }

            Environment.SetEnvironmentVariable(key, value);
            applied++;
        }

        return new LoadResult(path, applied, skipped);
    }

    /// <summary>
    /// Walks up from the working directory looking for the file.
    ///
    /// Up rather than in one fixed place, because where the server is started from is not one thing:
    /// `dotnet run --project Server/...` from the repository root and `dotnet run` from inside the
    /// project are both normal, and they have different working directories. One .env at the root
    /// serves both.
    /// </summary>
    private static string? Find(string fileName)
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, fileName);
                if (File.Exists(candidate)) return candidate;
                directory = directory.Parent;
            }
        }

        return null;
    }

    /// <summary>
    /// The format, which is a convention rather than a specification: KEY=VALUE a line at a time,
    /// <c>#</c> comments, blank lines ignored, an optional <c>export</c> in front, and quotes stripped
    /// if they wrap the whole value.
    /// </summary>
    internal static IEnumerable<(string Key, string Value)> Parse(IEnumerable<string> lines)
    {
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;

            // `export FOO=bar` is what somebody pastes out of a shell script.
            if (line.StartsWith("export ", StringComparison.Ordinal)) line = line[7..].TrimStart();

            var split = line.IndexOf('=');
            // No separator is not a key, and an empty name is not one either. Both are skipped rather
            // than thrown over: one bad line should not stop a server booting.
            if (split <= 0) continue;

            var key = line[..split].Trim();
            var value = line[(split + 1)..].Trim();
            if (key.Length == 0) continue;

            if (IsWrappedIn(value, '"') || IsWrappedIn(value, '\''))
            {
                var quote = value[0];
                value = value[1..^1];
                // Escapes are only honoured inside double quotes, which is the shell's rule and the one
                // every .env reader copies. A single-quoted value is taken literally.
                if (quote == '"') value = value.Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\\"", "\"");
            }
            else
            {
                // An unquoted value ends at a trailing comment: `KEY=value # why`. Inside quotes a #
                // is just a character, which is how a secret containing one survives.
                var comment = value.IndexOf(" #", StringComparison.Ordinal);
                if (comment >= 0) value = value[..comment].TrimEnd();
            }

            yield return (key, value);
        }
    }

    private static bool IsWrappedIn(string value, char quote)
        => value.Length >= 2 && value[0] == quote && value[^1] == quote;
}
