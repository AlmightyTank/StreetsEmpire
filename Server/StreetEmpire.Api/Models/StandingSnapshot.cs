namespace StreetEmpire.Api.Models;

/// <summary>
/// Where one player stood at one moment.
///
/// Rank is a comparison between players, so answering "who moved ahead of me while I was away" needs
/// everyone's position at the same past instant, not each player's position at their own last login.
/// A rival that acts constantly would otherwise only ever have a fresh reading, which says nothing
/// about where they were when the player left.
///
/// Rows are written for every player at once on a timer, so any two rows sharing a
/// <see cref="TakenAtUtc"/> are directly comparable.
/// </summary>
public sealed class StandingSnapshot
{
    public long Id { get; set; }
    public Guid PlayerId { get; set; }
    public Player Player { get; set; } = null!;

    public int Rank { get; set; }
    public long NetWorth { get; set; }

    /// <summary>Shared by every row in the same sample, which is what makes them comparable.</summary>
    public DateTime TakenAtUtc { get; set; }
}
