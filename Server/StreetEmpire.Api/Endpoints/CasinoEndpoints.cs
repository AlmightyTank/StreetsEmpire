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
                var spin = casino.SpinSlots(player, request.MachineKey, request.Bet, request.Paylines, now);
                var transaction = spin.Transaction;
                var action = transaction.NetResult > 0
                    ? $"Spun {transaction.MachineKey} slots across {transaction.Paylines:N0} lane(s) for {transaction.BetAmount:C0} and won {transaction.PayoutAmount:C0}."
                    : $"Spun {transaction.MachineKey} slots across {transaction.Paylines:N0} lane(s) for {transaction.BetAmount:C0}.";
                AddLog(db, player, before, "CASINO", 0, action, now);
                await db.SaveChangesAsync(ct);

                var response = casino.ToResponse(transaction);
                return Results.Ok(new SlotSpinResponse(
                    response,
                    response.Symbols,
                    player.Cash,
                    player.BankCash,
                    spin.RepEarned,
                    (await casino.BoardAsync(player, ct)).Reputation,
                    await casino.StatsAsync(player.Id, ct)));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();
    }
}
