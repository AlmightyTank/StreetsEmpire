namespace StreetEmpire.Api.Models;

/// <summary>
/// The four guns, weakest first.
///
/// A weapon does two different jobs, and tiers exist because those two jobs come apart. Any gun covers
/// a thug for morale - a thug with a pistol is as content as a thug with a rifle - but what the thug
/// brings to a fight is the gun, not the thug. That split is the whole mechanic: it makes covering a
/// big crew cheaply and arming a small crew well two different strategies rather than the same purchase
/// at two prices, and it is what turns the hideout's thug cap into the binding constraint. At the Trap
/// House's twenty-five thugs the only way left to get stronger is better guns.
/// </summary>
public static class WeaponTiers
{
    public const string Pistol = "pistols";
    public const string Shotgun = "shotguns";
    public const string Smg = "smgs";
    public const string Rifle = "rifles";

    /// <summary>Weakest to strongest. Order is load-bearing: "best first" reads this backwards.</summary>
    public static readonly string[] All = [Pistol, Shotgun, Smg, Rifle];

    /// <summary>Strongest first, which is the order a crew picks its guns up in.</summary>
    public static readonly string[] BestFirst = [Rifle, Smg, Shotgun, Pistol];

    public static bool IsWeapon(string? key)
        => key is not null && All.Contains(key.Trim().ToLowerInvariant());

    public static string Label(string key) => key switch
    {
        Pistol => "Pistols",
        Shotgun => "Shotguns",
        Smg => "SMGs",
        Rifle => "Rifles",
        _ => key
    };

    /// <summary>Singular, for the sentences that name one.</summary>
    public static string One(string key) => key switch
    {
        Pistol => "pistol",
        Shotgun => "shotgun",
        Smg => "SMG",
        Rifle => "rifle",
        _ => key
    };
}
