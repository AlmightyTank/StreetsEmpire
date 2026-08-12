using StreetEmpire.Api.Models;
using System.Security.Cryptography;
using System.Text;

namespace StreetEmpire.Api.Services;

internal sealed record BotBrain(
    string Name,
    BotBrainFocus Focus,
    int MinCooldownMinutes,
    int MaxCooldownMinutes,
    double IdleChance,
    double MoraleActionChance,
    double CashReserveMultiplier,
    double TurnReserveMultiplier,
    double SupplyTargetMultiplier,
    int RecoverySupplyMultiplier,
    double MinimumSupplyCoverage,
    double WeaponCoverageTarget,
    double ProductSellThresholdMultiplier,
    int EmergencyCashThreshold,
    int MaxCokeSaleQuantity,
    int MaxWeedSaleQuantity,
    int RaiseHoeCutBelow,
    int LowerHoeCutAbove,
    int LowHoeCutPercent,
    int HighHoeCutPercent,
    int MaxHoeCutPercent,
    int HoeCutStep,
    int SupplyMoraleThreshold,
    int WeaponMoraleThreshold,
    int MoraleRecoveryThreshold,
    int LowMoraleStreetThreshold,
    int LowMoraleMaxStreetTurns,
    int HireMoraleThreshold,
    int ManagementBuffer,
    int UnmanagedHoeTolerance,
    int UncoveredThugTolerance,
    double HireCashMultiplier,
    int MaxHoeHireBatch,
    int MaxThugHireBatch,
    int MinThugs,
    int HoesPerThugTarget,
    double ProductionChance,
    int ProductionCashMinimum,
    int CokeCashThreshold,
    double CokePreference,
    int MinProductionTurns,
    int MaxProductionTurns,
    int MinStreetTurns,
    int MaxStreetTurns,
    double TurnActionSkipChance,
    int MinSupplyBuy,
    int MaxSupplyBuy,
    int MaxWeaponBuy,
    long DepositMinimumExcess,
    double DepositShare)
{
    public static BotBrain For(Player bot)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{bot.AccountId:N}:{bot.Id:N}:{bot.Name}"));
        var focus = (BotBrainFocus)(bytes[0] % Enum.GetValues<BotBrainFocus>().Length);
        var patience = bytes[1] / 255.0;
        var risk = bytes[2] / 255.0;
        var appetite = bytes[3] / 255.0;

        return focus switch
        {
            BotBrainFocus.ResourceManager => new BotBrain(
                "Resource Manager", focus, 18 + (int)(patience * 8), 44 + (int)(patience * 10), 0.15, 0.95,
                1.35, 1.18, 2.8, 4, 1.0, 1.05, 0.9, 2_500, 80, 220,
                65, 90, 40, 65, 80, 10, 78, 82, 55, 42, 2,
                72, 1, 0, 0, 4.5, 2, 1, 3, 4,
                0.24, 6_000, 14_000, 0.55, 2, 3, 2, 5, 0.24,
                18, 60, 2, 2_000, 0.65),
            BotBrainFocus.BigSpender => new BotBrain(
                "Big Spender", focus, 10, 32 + (int)(risk * 8), 0.07, 0.55,
                0.45, 0.55, 1.05, 2, 0.45, 0.75, 0.65, 900, 140, 320,
                42, 84, 25, 55, 70, 15, 55, 58, 28, 24, 4,
                42, -3, 3, 3, 1.5, 4, 2, 2, 2,
                0.42, 2_500, 8_000, 0.72, 2, 5, 3, 8, 0.08,
                10, 75, 4, 6_000, 0.25),
            BotBrainFocus.MoraleNeglecter => new BotBrain(
                "Hard Charger", focus, 12, 36 + (int)(risk * 10), 0.06, 0.28,
                0.65, 0.62, 0.85, 1, 0.25, 0.55, 0.75, 1_000, 120, 280,
                30, 92, 20, 50, 60, 10, 38, 42, 18, 18, 2,
                30, -6, 8, 5, 2.0, 3, 2, 2, 2,
                0.32, 3_000, 9_000, 0.6, 1, 4, 3, 9, 0.06,
                8, 40, 2, 7_000, 0.2),
            BotBrainFocus.ProductRunner => new BotBrain(
                "Product Runner", focus, 14, 40 + (int)(appetite * 8), 0.11, 0.7,
                0.95, 0.8, 1.6, 3, 0.75, 0.9, 1.7, 1_500, 180, 420,
                52, 88, 35, 62, 78, 10, 68, 72, 40, 32, 2,
                58, 0, 1, 1, 3.2, 2, 1, 3, 3,
                0.78, 3_200, 7_000, 0.84, 3, 7, 2, 5, 0.14,
                12, 45, 2, 3_500, 0.45),
            BotBrainFocus.CrewBuilder => new BotBrain(
                "Crew Builder", focus, 13, 38 + (int)(appetite * 8), 0.1, 0.72,
                0.8, 0.72, 1.5, 3, 0.7, 0.85, 0.8, 1_400, 100, 260,
                54, 86, 35, 62, 78, 10, 68, 72, 38, 30, 2,
                58, 2, 1, 2, 2.1, 5, 2, 4, 2,
                0.28, 4_000, 10_000, 0.55, 2, 4, 2, 6, 0.12,
                14, 55, 3, 4_500, 0.35),
            BotBrainFocus.Banker => new BotBrain(
                "Banker", focus, 20 + (int)(patience * 10), 50 + (int)(patience * 14), 0.18, 0.78,
                1.15, 1.3, 1.7, 3, 0.85, 0.95, 0.55, 2_000, 70, 180,
                58, 88, 35, 65, 80, 10, 70, 76, 48, 36, 2,
                64, 1, 0, 0, 4.0, 2, 1, 4, 4,
                0.2, 5_000, 13_000, 0.48, 2, 3, 2, 5, 0.22,
                12, 45, 2, 1_200, 0.82),
            _ => new BotBrain(
                "Balanced Operator", focus, 15 + (int)(patience * 8), 42 + (int)(patience * 6), 0.12, 0.8,
                1.0, 1.0, 2.0, 3, 0.8, 1.0, 1.0, 2_000, 100, 250,
                55, 88, 35, 65, 80, 10, 70, 75, 45, 35, 2,
                65, 0, 0, 0, 4.0, 3, 1, 3, 3,
                0.35, 5_000, 12_000, 0.65, 2, 4, 2, 6, 0.18,
                12, 40, 3, 2_500, 0.5)
        };
    }
}

internal enum BotBrainFocus
{
    BalancedOperator,
    ResourceManager,
    BigSpender,
    MoraleNeglecter,
    ProductRunner,
    CrewBuilder,
    Banker
}
