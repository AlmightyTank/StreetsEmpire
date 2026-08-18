namespace StreetEmpire.Api.Models;

/// <summary>
/// A rack of guns, by tier.
///
/// A value type rather than four loose integers because almost nothing in the game wants one tier: it
/// wants "how many guns are there", "how hard do they hit", "take five away". Written out longhand at
/// each of those call sites, a rack would be counted one way in the store, another in a fight and a
/// third when storage overflows, and the three would drift. This is the same reasoning behind
/// <see cref="Player.AddCoke"/>: a pile with a property that a bare increment gets wrong.
///
/// The two operations that matter are opposites, and both are deliberate. A crew arms itself
/// <see cref="Best"/> first, because nobody walks past a rifle to pick up a pistol. Losses and storage
/// overflow come off <see cref="WorstFirst"/>, because the alternative - a lost fight quietly
/// destroying your rifles before your pistols - would make owning good guns a liability.
/// </summary>
public readonly record struct Armoury(int Pistols, int Shotguns, int Smgs, int Rifles)
{
    public static readonly Armoury Empty = new(0, 0, 0, 0);

    public int Total => Pistols + Shotguns + Smgs + Rifles;

    public bool Any => Total > 0;

    public int Of(string tier) => tier switch
    {
        WeaponTiers.Pistol => Pistols,
        WeaponTiers.Shotgun => Shotguns,
        WeaponTiers.Smg => Smgs,
        WeaponTiers.Rifle => Rifles,
        _ => 0
    };

    public Armoury With(string tier, int count) => tier switch
    {
        WeaponTiers.Pistol => this with { Pistols = Math.Max(0, count) },
        WeaponTiers.Shotgun => this with { Shotguns = Math.Max(0, count) },
        WeaponTiers.Smg => this with { Smgs = Math.Max(0, count) },
        WeaponTiers.Rifle => this with { Rifles = Math.Max(0, count) },
        _ => this
    };

    public Armoury Add(string tier, int count)
        => count == 0 ? this : With(tier, Of(tier) + count);

    public static Armoury operator +(Armoury left, Armoury right)
        => new(left.Pistols + right.Pistols, left.Shotguns + right.Shotguns, left.Smgs + right.Smgs, left.Rifles + right.Rifles);

    public static Armoury operator -(Armoury left, Armoury right)
        => new(
            Math.Max(0, left.Pistols - right.Pistols),
            Math.Max(0, left.Shotguns - right.Shotguns),
            Math.Max(0, left.Smgs - right.Smgs),
            Math.Max(0, left.Rifles - right.Rifles));

    /// <summary>
    /// The best <paramref name="count"/> guns on the rack. What a crew actually carries out of the
    /// door, and what firepower is measured over.
    /// </summary>
    public Armoury Best(int count)
    {
        var taken = Empty;
        var left = Math.Max(0, count);
        foreach (var tier in WeaponTiers.BestFirst)
        {
            if (left <= 0) break;
            var take = Math.Min(left, Of(tier));
            taken = taken.With(tier, take);
            left -= take;
        }

        return taken;
    }

    /// <summary>
    /// The cheapest <paramref name="count"/> guns. What a loss or a storage overflow takes, so that
    /// owning good guns is never what makes a bad day worse.
    /// </summary>
    public Armoury WorstFirst(int count)
    {
        var taken = Empty;
        var left = Math.Max(0, count);
        foreach (var tier in WeaponTiers.All)
        {
            if (left <= 0) break;
            var take = Math.Min(left, Of(tier));
            taken = taken.With(tier, take);
            left -= take;
        }

        return taken;
    }

    /// <summary>
    /// How hard this rack hits, in units of one pistol.
    ///
    /// Capped at the crew carrying it, because a gun nobody is holding fights nobody. An all-pistol
    /// rack returns exactly the armed-thug count, which is what the single generic weapon used to
    /// contribute - so a player who has never bought anything better fights precisely as they did
    /// before tiers existed.
    /// </summary>
    public double Firepower(int thugs, IReadOnlyDictionary<string, double> power)
    {
        var carried = Best(Math.Max(0, thugs));
        var total = 0.0;
        foreach (var tier in WeaponTiers.All)
            total += carried.Of(tier) * (power.TryGetValue(tier, out var value) ? value : 1);
        return total;
    }

    /// <summary>Names what is actually on the rack rather than listing the empty shelves.</summary>
    public string Describe()
    {
        var parts = new List<string>();
        foreach (var tier in WeaponTiers.BestFirst)
            if (Of(tier) > 0)
                parts.Add($"{Of(tier):N0} {WeaponTiers.Label(tier).ToLowerInvariant()}");

        return parts.Count switch
        {
            0 => "no weapons",
            1 => parts[0],
            2 => $"{parts[0]} and {parts[1]}",
            _ => $"{string.Join(", ", parts.Take(parts.Count - 1))} and {parts[^1]}"
        };
    }
}
