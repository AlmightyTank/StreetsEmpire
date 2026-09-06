using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Support;

internal sealed record PlayerSnapshot(
    long Cash,
    long BankCash,
    int Pimps,
    int Hoes,
    int Thugs,
    int Condoms,
    int Beer,
    int Weapons,
    int Weed,
    int Coke,
    double HoeMorale,
    double ThugMorale);
/// <summary>
/// A player, what they are worth, and where that puts them. Plunder is carried alongside because
/// the board shows net worth while the anti-farm gate weighs what could actually be taken, and a
/// row that knew only one of the two would have to guess at the other.
/// </summary>
internal sealed record RankedPlayer(Player Player, long NetWorth, long Plunder, int Rank);
internal sealed record PlayerStandingRow(Guid PlayerId, long NetWorth, DateTime CreatedAtUtc);
internal sealed record BotTemplate(
    string Username,
    string Name,
    string City,
    long CashBonus,
    long BankCash,
    int TurnBonus,
    int Pimps,
    int Hoes,
    int Thugs,
    int Condoms,
    int Beer,
    int Weapons,
    int Weed,
    int Coke,
    int HoeCutPercent,
    double HoeHappiness,
    double ThugHappiness,

    /// <summary>
    /// The personality to give this rival, or null to let it take one from its name.
    ///
    /// Named for the seeds whose whole reason for existing is how they behave: a house dealt a fat
    /// safe and a rack of pistols is only a target if it also has the temperament to leave both alone.
    /// </summary>
    string? Focus = null);
