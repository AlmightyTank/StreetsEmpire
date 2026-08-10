using Microsoft.Extensions.Options;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

public sealed class TurnService(IOptions<GameOptions> options)
{
    private readonly GameOptions _options = options.Value;

    public bool Refresh(Player player, DateTime nowUtc)
    {
        if (player.Turns >= _options.MaxTurns)
        {
            // Reset the clock while capped so old elapsed time cannot be banked.
            player.LastTurnUpdateUtc = nowUtc;
            return false;
        }

        var tick = TimeSpan.FromMinutes(_options.TurnTickMinutes);
        var elapsed = nowUtc - player.LastTurnUpdateUtc;
        if (elapsed < tick)
            return false;

        var completedTicks = (int)Math.Floor(elapsed.TotalMinutes / _options.TurnTickMinutes);
        if (completedTicks <= 0)
            return false;

        var turnsToAdd = completedTicks * _options.TurnsPerTick;
        player.Turns = Math.Min(_options.MaxTurns, player.Turns + turnsToAdd);
        player.LastTurnUpdateUtc = player.Turns >= _options.MaxTurns
            ? nowUtc
            : player.LastTurnUpdateUtc.AddMinutes(completedTicks * _options.TurnTickMinutes);
        return true;
    }

    public int SecondsUntilNextTick(Player player, DateTime nowUtc)
    {
        if (player.Turns >= _options.MaxTurns)
            return 0;

        var next = player.LastTurnUpdateUtc.AddMinutes(_options.TurnTickMinutes);
        return Math.Max(0, (int)Math.Ceiling((next - nowUtc).TotalSeconds));
    }
}
