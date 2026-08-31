using Microsoft.Extensions.Options;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

public sealed class TurnService(IOptionsSnapshot<GameOptions> options, PimpRoster pimps)
{
    private readonly GameOptions _options = options.Value;

    /// <param name="moraleRecoveryPercent">
    /// What the player's clubs add to passive recovery. A percentage on the existing rate rather than a
    /// separate trickle, so there is still only one place morale recovers.
    /// </param>
    public bool Refresh(Player player, DateTime nowUtc, int moraleRecoveryPercent = 0)
    {
        var tick = TimeSpan.FromMinutes(_options.TurnTickMinutes);
        var elapsed = nowUtc - player.LastTurnUpdateUtc;
        if (elapsed < tick)
            return false;

        var completedTicks = (int)Math.Floor(elapsed.TotalMinutes / _options.TurnTickMinutes);
        if (completedTicks <= 0)
            return false;

        // Faster while they are small, tapering to the normal rate as the empire grows. Read per
        // refresh rather than stored, so it follows the player rather than needing to be recalculated.
        var turnsToAdd = completedTicks * _options.TurnsPerTickFor(player);
        var moraleRecovery = completedTicks
            * Math.Max(0, _options.Morale.PassiveRecoveryPerTick)
            * (1 + Math.Max(0, moraleRecoveryPercent) / 100.0);
        // What this player's building holds, not what the game opens at. Read per refresh for the
        // same reason the rate is: it follows the player rather than needing recalculating when they
        // move up, and a build that lands mid-session raises the ceiling on the very next tick.
        var maxTurns = _options.MaxTurnsFor(player);
        var turnsBefore = player.Turns;
        var hoeBefore = player.HoeHappiness;
        var thugBefore = player.ThugHappiness;
        var clockBefore = player.LastTurnUpdateUtc;
        player.Turns = Math.Min(maxTurns, player.Turns + turnsToAdd);
        player.HoeHappiness = RecoverMorale(player.HoeHappiness, moraleRecovery);
        player.ThugHappiness = RecoverMorale(player.ThugHappiness, moraleRecovery);
        // Pimps cool off over the same ticks, so loyalty is not a one-way ratchet.
        pimps.Recover(player, completedTicks * pimps.PassiveRecoveryPerTick);
        player.LastTurnUpdateUtc = player.Turns >= maxTurns
            ? nowUtc
            : player.LastTurnUpdateUtc.AddMinutes(completedTicks * _options.TurnTickMinutes);
        return turnsBefore != player.Turns
            || !DoubleEquals(hoeBefore, player.HoeHappiness)
            || !DoubleEquals(thugBefore, player.ThugHappiness)
            || clockBefore != player.LastTurnUpdateUtc;
    }

    public int SecondsUntilNextTick(Player player, DateTime nowUtc)
    {
        if (player.Turns >= _options.MaxTurnsFor(player))
            return 0;

        var next = player.LastTurnUpdateUtc.AddMinutes(_options.TurnTickMinutes);
        return Math.Max(0, (int)Math.Ceiling((next - nowUtc).TotalSeconds));
    }

    private static double RecoverMorale(double current, double amount)
        => Math.Round(Math.Clamp(current + amount, 0, 100), 2);

    private static bool DoubleEquals(double left, double right)
        => Math.Abs(left - right) < 0.001;
}
