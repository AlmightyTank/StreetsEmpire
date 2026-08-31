using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

/// <summary>
/// How a large purchase is paid for: out of the bank first, then whatever is on the table.
///
/// Shared rather than owned by the hideout because the ground is now the second thing in the game that
/// costs more than a safe holds. Charging cash on hand would cap what a player can spend at the size of
/// the safe they happen to own, and earnings over that safe are swept into the bank anyway - so the
/// bank is simply where the money for anything expensive actually is.
/// </summary>
public static class Capital
{
    /// <summary>
    /// Takes a price out of the bank first, then cash on hand, and returns how much the bank covered.
    /// The caller checks the combined total first, because refusing a purchase is a rule and this is
    /// only the till.
    /// </summary>
    public static long Charge(Player player, long cost)
    {
        var fromBank = Math.Min(player.BankCash, cost);
        player.BankCash -= fromBank;
        player.Cash -= cost - fromBank;
        return fromBank;
    }
}
