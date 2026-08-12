using Microsoft.Extensions.Options;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

public sealed class TurnService(IOptions<GameOptions> options)
{
    private readonly GameOptions _options = options.Value;

    public bool Refresh(Player player, DateTime nowUtc)
    {
        var tick = TimeSpan.FromMinutes(_options.TurnTickMinutes);
        var elapsed = nowUtc - player.LastTurnUpdateUtc;
        if (elapsed < tick)
            return false;

        var completedTicks = (int)Math.Floor(elapsed.TotalMinutes / _options.TurnTickMinutes);
        if (completedTicks <= 0)
            return false;

        var turnsToAdd = completedTicks * _options.TurnsPerTick;
        var moraleRecovery = completedTicks * Math.Max(0, _options.Morale.PassiveRecoveryPerTick);
        var turnsBefore = player.Turns;
        var hoeBefore = player.HoeHappiness;
        var thugBefore = player.ThugHappiness;
        var clockBefore = player.LastTurnUpdateUtc;
        player.Turns = Math.Min(_options.MaxTurns, player.Turns + turnsToAdd);
        player.HoeHappiness = RecoverMorale(player.HoeHappiness, moraleRecovery);
        player.ThugHappiness = RecoverMorale(player.ThugHappiness, moraleRecovery);
        player.LastTurnUpdateUtc = player.Turns >= _options.MaxTurns
            ? nowUtc
            : player.LastTurnUpdateUtc.AddMinutes(completedTicks * _options.TurnTickMinutes);
        return turnsBefore != player.Turns
            || !DoubleEquals(hoeBefore, player.HoeHappiness)
            || !DoubleEquals(thugBefore, player.ThugHappiness)
            || clockBefore != player.LastTurnUpdateUtc;
    }

    public int SecondsUntilNextTick(Player player, DateTime nowUtc)
    {
        if (player.Turns >= _options.MaxTurns)
            return 0;

        var next = player.LastTurnUpdateUtc.AddMinutes(_options.TurnTickMinutes);
        return Math.Max(0, (int)Math.Ceiling((next - nowUtc).TotalSeconds));
    }

    private static double RecoverMorale(double current, double amount)
        => Math.Round(Math.Clamp(current + amount, 0, 100), 2);

    private static bool DoubleEquals(double left, double right)
        => Math.Abs(left - right) < 0.001;
}
