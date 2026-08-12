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
    int Coke);
internal sealed record RankedPlayer(Player Player, long NetWorth, int Rank);
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
    double ThugHappiness);
