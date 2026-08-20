using Microsoft.EntityFrameworkCore;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Services;
using static StreetEmpire.Api.Support.ActionLogging;

namespace StreetEmpire.Api.Endpoints;

/// <summary>
/// The people in a town who want things: what is on offer where the player is standing, and filling
/// one. Orders belong to the town rather than to the player, so this only ever shows the town they
/// are actually in.
/// </summary>
internal static class ContractEndpoints
{
    internal static void MapContractEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/game/contracts", async (
            CurrentPlayerService current,
            ContractService contracts,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var now = DateTime.UtcNow;
            var board = await contracts.BoardAsync(player.City, now, ct);
            return Results.Ok(new ContractBoardResponse(
                player.City,
                board.Select(x => ToResponse(x, player, now)).ToList()));
        }).RequireAuthorization();

        app.MapPost("/api/game/contracts/{id:long}/fill", async (
            long id,
            DeliverContractRequest? request,
            CurrentPlayerService current,
            GameDbContext db,
            PlayerClock clock,
            ContractService contracts,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var now = DateTime.UtcNow;
            await clock.AdvanceAsync(player, now, db, ct);

            var contract = await db.Contracts.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (contract is null) return Results.NotFound(new { error = "No such job." });

            var before = Snapshot(player);
            try
            {
                var fill = contracts.Deliver(contract, player, now, request?.Quantity);
                AddLog(db, player, before, "CONTRACT", 0, fill.Summary, now);
                await db.SaveChangesAsync(ct);

                return Results.Ok(new ActionResultResponse(fill.Summary, player.Turns, new Dictionary<string, object?>
                {
                    ["buyer"] = contract.Buyer,
                    ["good"] = contract.Good,
                    ["handedOver"] = fill.Delivered,
                    ["stillWanted"] = contract.Remaining,
                    ["pricePerUnit"] = contract.PricePerUnit,
                    ["paid"] = fill.Paid,
                    ["premiumOverFlat"] = fill.Premium
                }));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();
    }

    private static ContractResponse ToResponse(Models.Contract contract, Models.Player player, DateTime nowUtc)
    {
        // Why the player cannot fill it, worked out once here rather than guessed at by the page. A
        // button that looks live and then refuses is worse than one that says what is missing.
        var held = TradeGoods.Held(player, contract.Good);
        var purity = (int)Math.Round(player.CokePurity * 100);
        var canDeliverNow = Math.Min(held, contract.Remaining);
        // An order only has to be startable now, not finishable now - that is the whole point of
        // handing it over in instalments. What blocks it is having nothing to put in, or somebody
        // else already working it.
        var blocked = !contract.CanBeWorkedBy(player.Id)
            ? "Somebody else is filling this one"
            : contract.MinimumPurityPercent is { } floor && purity < floor
                ? $"Yours is {purity}% pure, they want {floor}%"
                : held <= 0
                    ? $"You have no {TradeGoods.Label(contract.Good).ToLowerInvariant()}"
                    : null;

        return new ContractResponse(
            contract.Id,
            contract.Buyer,
            contract.Good,
            contract.Quantity,
            contract.PricePerUnit,
            contract.ListPricePerUnit,
            contract.Payout,
            contract.Payout - contract.FlatValue,
            contract.MinimumPurityPercent,
            Math.Max(0, (int)Math.Ceiling((contract.ExpiresAtUtc - nowUtc).TotalMinutes)),
            held,
            contract.DeliveredQuantity,
            contract.Remaining,
            contract.CompletionBonus,
            canDeliverNow,
            contract.ClaimedById == player.Id,
            blocked);
    }
}
