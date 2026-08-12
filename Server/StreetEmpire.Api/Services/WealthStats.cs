using StreetEmpire.Api.Contracts;

namespace StreetEmpire.Api.Services;

/// <summary>
/// Distribution maths for the admin oversight view. Totals alone hide the shape of an economy: an
/// admin needs to see whether wealth is spread or concentrated. Lives here rather than in Program so
/// the formulas are covered by rule tests.
/// </summary>
public static class WealthStats
{
    /// <summary>Expects an ascending list. Even counts average the middle pair.</summary>
    public static long Median(IReadOnlyList<long> ascending)
        => ascending.Count == 0
            ? 0
            : ascending.Count % 2 == 1
                ? ascending[ascending.Count / 2]
                : (ascending[ascending.Count / 2 - 1] + ascending[ascending.Count / 2]) / 2;

    /// <summary>
    /// Gini as a percentage over an ascending list: 0 means everyone holds the same, 100 means one
    /// player holds everything. Uses the sum of (2i - n - 1) * value, normalised by n * total.
    /// </summary>
    public static double GiniPercent(IReadOnlyList<long> ascending)
    {
        var total = ascending.Sum();
        if (ascending.Count < 2 || total <= 0)
            return 0;

        var weighted = 0d;
        for (var i = 0; i < ascending.Count; i++)
            weighted += (2d * (i + 1) - ascending.Count - 1) * ascending[i];

        return Math.Round(Math.Clamp(weighted / (ascending.Count * (double)total), 0, 1) * 100, 1);
    }

    private static readonly (string Label, long Floor)[] Bands =
    [
        ("Under $50k", 0),
        ("$50k - $250k", 50_000),
        ("$250k - $1M", 250_000),
        ("$1M and up", 1_000_000)
    ];

    public static List<AdminWealthBandResponse> WealthBands(IReadOnlyList<long> ascending)
        => Bands
            .Select((band, index) =>
            {
                var ceiling = index + 1 < Bands.Length ? Bands[index + 1].Floor : long.MaxValue;
                var inBand = ascending.Where(x => x >= band.Floor && x < ceiling).ToList();
                return new AdminWealthBandResponse(band.Label, inBand.Count, inBand.Sum());
            })
            .ToList();
}
