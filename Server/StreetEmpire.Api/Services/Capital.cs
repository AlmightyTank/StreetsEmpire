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
    /// Everything a large purchase can be paid out of, from here.
    ///
    /// The bank always, cash on hand always, and the safe only when the player is standing in front of
    /// it. The safe is money the player owns and cannot spend from another town, so a purchase that
    /// counted it while they were away would be quoting them a total they cannot actually reach.
    /// </summary>
    public static long Available(Player player)
        => player.BankCash + player.Cash + Reachable(player);

    /// <summary>
    /// Takes a price out of the bank first, then the safe, then cash on hand, and returns how much the
    /// bank covered. The caller checks <see cref="Available"/> first, because refusing a purchase is a
    /// rule and this is only the till.
    ///
    /// The order is cheapest-to-reach first in the player's terms: the bank has already been swept
    /// into and is the least useful money to hold, the safe is the next most idle, and cash on hand is
    /// what they need for the street. Spending it in that order is what a player would do.
    /// </summary>
    public static long Charge(Player player, long cost)
    {
        var owed = Math.Max(0, cost);
        var fromBank = Math.Min(player.BankCash, owed);
        player.BankCash -= fromBank;
        owed -= fromBank;

        if (owed > 0 && player.Hideout is { } hideout && Reachable(player) > 0)
        {
            var fromSafe = Math.Min(hideout.SafeCash, owed);
            hideout.SafeCash -= fromSafe;
            owed -= fromSafe;
        }

        player.Cash -= owed;
        return fromBank;
    }

    /// <summary>What is in the safe, or nothing at all when the player is not there to open it.</summary>
    private static long Reachable(Player player)
        => player.Hideout is { } hideout && HideoutService.IsAtHideout(player) ? hideout.SafeCash : 0;
}
