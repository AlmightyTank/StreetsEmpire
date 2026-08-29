namespace StreetEmpire.Api.Models;

/// <summary>
/// A named pimp. Pimps are the only crew tracked individually: they are capped at six, they persist
/// (hoes and thugs churn constantly), and one commands each attack, so a name has something to stick
/// to. Lost pimps are kept as rows rather than deleted so the player has a roll of the fallen.
/// </summary>
public sealed class Pimp
{
    public long Id { get; set; }
    public Guid PlayerId { get; set; }
    public Player Player { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Enforcer or Hustler. An Enforcer sharpens the attack they command and the defence of the house
    /// while they are home; a Hustler lifts street income while they are home. Since street work is
    /// blocked while any mission is out, the two never apply at the same time.
    /// </summary>
    public string Specialty { get; set; } = PimpSpecialties.Hustler;

    /// <summary>A few percent, rolled at hire.</summary>
    public int BonusPercent { get; set; }

    /// <summary>Drops on defeats and while the crew is miserable; a low value risks a walk-out.</summary>
    public double Loyalty { get; set; } = 100;

    public int MissionsLed { get; set; }
    public int Victories { get; set; }

    public DateTime HiredAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Null while still on the payroll.</summary>
    public DateTime? LostAtUtc { get; set; }

    /// <summary>How they left: Fired, Killed in action, Killed defending, Walked out, or Left in County.</summary>
    public string? LostReason { get; set; }

    /// <summary>
    /// Null unless they are sitting in a cell.
    ///
    /// A third state, and not the same as being lost. Lost is for ever and is what the fallen list is
    /// made of; jail is a held position somebody can still be bought out of. Kept separate rather than
    /// borrowing LostAtUtc because clearing that on a bail would be a resurrection, and because a
    /// reason of "Arrested" sitting in the fallen list would be reporting a death that has not
    /// happened.
    ///
    /// Left set when an abandoned pimp finally becomes lost, so the record still says where they went.
    /// </summary>
    public DateTime? JailedAtUtc { get; set; }

    /// <summary>
    /// On the payroll and available. Excluding the jailed here is what makes the rest of the game
    /// handle them for free: they stop earning, stop counting toward management capacity and cannot be
    /// picked to lead, at every call site, without any of those sites knowing jail exists.
    /// </summary>
    public bool IsActive => LostAtUtc is null && JailedAtUtc is null;

    /// <summary>Inside, and still gettable. Not lost, and deliberately not in the fallen list.</summary>
    public bool IsJailed => LostAtUtc is null && JailedAtUtc is not null;
}

public static class PimpSpecialties
{
    public const string Enforcer = "Enforcer";
    public const string Hustler = "Hustler";
}
