namespace StreetEmpire.Api.Models;

/// <summary>
/// A piece of ground somebody holds.
///
/// Territory exists so combat is about position rather than a one-off withdrawal: a raid pays once and
/// changes nothing, while ground can be lost while its holder is asleep. Holding it costs garrisoned
/// thugs who are unavailable at home, which is the whole design in one line: attack, defend, or occupy,
/// pick two.
///
/// The rows are seeded rather than created by players. The map is fixed and scarce on purpose, because
/// contested ground is the point and ground nobody wants is just scenery.
/// </summary>
public sealed class Territory
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The town this ground is in, and the boundary of who may fight over it. You contest your own
    /// city and nowhere else, which is what makes the map local rather than a shared list.
    /// </summary>
    public string City { get; set; } = string.Empty;

    /// <summary>corner, dock, club, or lot. Decides what holding it is worth.</summary>
    public string Type { get; set; } = "corner";

    public Guid? HolderId { get; set; }
    public Player? Holder { get; set; }

    /// <summary>Thugs standing on it. They count as away from home for as long as they are here.</summary>
    public int GarrisonThugs { get; set; }

    /// <summary>
    /// The pimp running this ground. Standing here is a posting, not a visit: they are away from home
    /// for as long as it lasts, so they do not sharpen the house's defence, do not lift street income,
    /// and cannot command a raid. An Enforcer's bonus applies to the garrison instead.
    /// </summary>
    public long? GarrisonPimpId { get; set; }
    public Pimp? GarrisonPimp { get; set; }

    public DateTime? HeldSinceUtc { get; set; }

    /// <summary>
    /// Set when the ground changes hands. Without it two players trade the same corner every time
    /// their lanes come free, which is a different problem from the wealth farming anti-farm covers.
    /// </summary>
    public DateTime? ProtectedUntilUtc { get; set; }
}
