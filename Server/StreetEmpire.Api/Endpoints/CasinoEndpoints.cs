using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Services;
using static StreetEmpire.Api.Support.ActionLogging;

namespace StreetEmpire.Api.Endpoints;

internal static class CasinoEndpoints
{
    internal static void MapCasinoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/game/casino", async (
            CurrentPlayerService current,
            CasinoService casino,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            return player is null
                ? Results.Unauthorized()
                : Results.Ok(await casino.BoardAsync(player, ct));
        }).RequireAuthorization();

        app.MapPost("/api/game/casino/slots/spin", async (
            SlotSpinRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            PlayerClock clock,
            CasinoService casino,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var now = DateTime.UtcNow;
            await clock.AdvanceAsync(player, now, db, ct);
            var before = Snapshot(player);
            try
            {
                var spin = await casino.SpinSlotsAsync(player, request.MachineKey, request.Bet, request.Paylines, now, ct);
                var transaction = spin.Transaction;

                // A dropped pot is written as its own kind of row rather than as a large CASINO one, so
                // the feed can always carry it. An ordinary win only reaches the world when it clears
                // the newsworthy cash swing, which is right for a win and wrong for a jackpot: the pot
                // on the Sidewalk is the smallest on the floor and still the story of the night.
                var action = spin.JackpotWon > 0 ? "JACKPOT" : "CASINO";
                var summary = spin.JackpotWon > 0
                    ? $"Took the {transaction.MachineKey} progressive for {spin.JackpotWon:C0} on a {transaction.BetAmount:C0} pull."
                    : transaction.NetResult > 0
                        ? $"Spun {transaction.MachineKey} slots across {transaction.Paylines:N0} lane(s) for {transaction.BetAmount:C0} and won {transaction.PayoutAmount:C0}."
                        : $"Spun {transaction.MachineKey} slots across {transaction.Paylines:N0} lane(s) for {transaction.BetAmount:C0}.";
                AddLog(db, player, before, action, spin.TurnsSpent, summary, now);
                await db.SaveChangesAsync(ct);

                var response = casino.ToResponse(transaction);
                var board = await casino.BoardAsync(player, ct);
                return Results.Ok(new SlotSpinResponse(
                    response,
                    response.Symbols,
                    player.Cash,
                    player.BankCash,
                    player.Turns,
                    spin.TurnsSpent,
                    spin.RepEarned,
                    board.Reputation,
                    board.Stats,
                    board));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();
    }
}
