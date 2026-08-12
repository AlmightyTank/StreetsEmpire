namespace StreetEmpire.Api.Services;

/// <summary>
/// The live set of tuning overrides, layered on top of appsettings.
///
/// Held as a singleton and applied through a PostConfigure step. Because consumers take
/// <c>IOptionsSnapshot&lt;GameOptions&gt;</c> — which re-runs configuration per scope — swapping the map
/// here means the next request already sees the new numbers, with no restart and no writing to
/// appsettings.json. Persistence lives in the single GameSettings row so overrides survive a restart.
/// </summary>
public sealed class GameOptionOverrides
{
    private readonly Lock _gate = new();
    private Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Bumped on every change, so callers can tell whether they are looking at stale numbers.</summary>
    public int Version { get; private set; }

    public IReadOnlyDictionary<string, string> Snapshot()
    {
        lock (_gate)
            return new Dictionary<string, string>(_values, StringComparer.OrdinalIgnoreCase);
    }

    public void Replace(IReadOnlyDictionary<string, string> values)
    {
        lock (_gate)
        {
            _values = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
            Version++;
        }
    }

    /// <summary>
    /// Writes every override onto a freshly bound options instance. Unknown or unparsable entries are
    /// skipped rather than thrown: a bad row left in the database must not take the whole game down.
    /// </summary>
    public void Apply(GameOptions options)
    {
        foreach (var (path, value) in Snapshot())
            GameOptionPaths.TryApply(options, path, value, out _);
    }
}
