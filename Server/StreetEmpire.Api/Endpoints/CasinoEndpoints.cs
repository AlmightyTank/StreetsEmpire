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
                var onTheHouse = spin.WasFreeSpin ? " on the house" : string.Empty;
                var summary = spin.JackpotWon > 0
                    ? $"Took the {transaction.MachineKey} progressive for {spin.JackpotWon:C0} on a {transaction.BetAmount:C0} pull{onTheHouse}."
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
                    spin.CompsEarned,
                    board.Reputation,
                    board.Stats,
                    spin.WasFreeSpin,
                    spin.FreeSpinsAwarded,
                    spin.FreeSpinsLeft,
                    board));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();

        app.MapGet("/api/game/casino/blackjack", async (
            CurrentPlayerService current,
            BlackjackService blackjack,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            return player is null
                ? Results.Unauthorized()
                : Results.Ok(await blackjack.BoardAsync(player, ct));
        }).RequireAuthorization();

        // Deal, hit, stand and double all end the same way: save, then hand back the hand as the table
        // shows it. Only the deal takes a stake and a turn, so only it needs the clock advanced first.
        app.MapPost("/api/game/casino/blackjack/deal", async (
            BlackjackDealRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            PlayerClock clock,
            BlackjackService blackjack,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var now = DateTime.UtcNow;
            await clock.AdvanceAsync(player, now, db, ct);
            var before = Snapshot(player);
            try
            {
                var round = await blackjack.DealAsync(player, request.TableKey, request.Bet, now, ct);
                AddLog(db, player, before, "CASINO", 0, $"Sat down at {round.TableKey} blackjack for {request.Bet:C0}.", now);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new BlackjackActionResponse(
                    blackjack.View(player, round), player.Cash, player.Turns, await blackjack.BoardAsync(player, ct)));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();

        foreach (var move in new[] { "hit", "stand", "double", "split" })
        {
            var chosen = move;
            app.MapPost($"/api/game/casino/blackjack/{chosen}", async (
                CurrentPlayerService current,
                GameDbContext db,
                BlackjackService blackjack,
                CancellationToken ct) =>
            {
                var player = await current.GetAsync(ct);
                if (player is null) return Results.Unauthorized();

                var now = DateTime.UtcNow;
                var before = Snapshot(player);
                try
                {
                    var round = chosen switch
                    {
                        "hit" => await blackjack.HitAsync(player, now, ct),
                        "double" => await blackjack.DoubleAsync(player, now, ct),
                        "split" => await blackjack.SplitAsync(player, now, ct),
                        _ => await blackjack.StandAsync(player, now, ct)
                    };

                    // Only worth a line in the log once it is settled - a card at a time is not news.
                    if (round.SettledAtUtc is not null)
                    {
                        var summary = round.Payout > round.Bet
                            ? $"Played a {round.TableKey} blackjack hand for {round.Bet:C0} and took {round.Payout:C0}."
                            : $"Played a {round.TableKey} blackjack hand for {round.Bet:C0} and lost it.";
                        AddLog(db, player, before, "CASINO", 0, summary, now);
                    }

                    await db.SaveChangesAsync(ct);
                    return Results.Ok(new BlackjackActionResponse(
                        blackjack.View(player, round), player.Cash, player.Turns, await blackjack.BoardAsync(player, ct)));
                }
                catch (GameRuleException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            }).RequireAuthorization();
        }

        app.MapGet("/api/game/casino/roulette", async (
            CurrentPlayerService current,
            RouletteService roulette,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            return player is null
                ? Results.Unauthorized()
                : Results.Ok(await roulette.BoardAsync(player, ct));
        }).RequireAuthorization();

        app.MapPost("/api/game/casino/roulette/spin", async (
            RouletteSpinRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            PlayerClock clock,
            RouletteService roulette,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var now = DateTime.UtcNow;
            await clock.AdvanceAsync(player, now, db, ct);
            var before = Snapshot(player);
            try
            {
                var spin = roulette.Spin(player, request.TableKey, request.Bets ?? [], now);
                var transaction = spin.Transaction;
                var won = transaction.NetResult > 0;
                var summary = won
                    ? $"Backed {transaction.Paylines:N0} bet(s) for {transaction.BetAmount:C0} on {transaction.MachineKey} roulette; {spin.Pocket} {spin.Colour} paid {transaction.PayoutAmount:C0}."
                    : $"Backed {transaction.Paylines:N0} bet(s) for {transaction.BetAmount:C0} on {transaction.MachineKey} roulette and the ball found {spin.Pocket} {spin.Colour}.";
                AddLog(db, player, before, "CASINO", spin.TurnsSpent, summary, now);
                await db.SaveChangesAsync(ct);

                return Results.Ok(new RouletteSpinResponse(
                    roulette.ToResponse(transaction),
                    spin.Pocket,
                    spin.Colour,
                    player.Cash,
                    player.Turns,
                    spin.TurnsSpent,
                    spin.RepEarned,
                    spin.CompsEarned,
                    await roulette.BoardAsync(player, ct)));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();

        app.MapPost("/api/game/casino/comps/claim", async (
            ClaimCompRequest request,
            CurrentPlayerService current,
            GameDbContext db,
            PlayerClock clock,
            CasinoService casino,
            CancellationToken ct) =>
        {
            var player = await current.GetAsync(ct);
            if (player is null) return Results.Unauthorized();

            var now = DateTime.UtcNow;
            // Advanced first so a comped room lands on top of whatever the clock had already given
            // back. Granting turns against a stale bank would either overshoot the cap or quietly
            // hand back fewer than the menu promised.
            await clock.AdvanceAsync(player, now, db, ct);
            var before = Snapshot(player);
            try
            {
                var claim = casino.ClaimComp(player, request.RewardKey);
                // No turns spent: this is the house paying out, and the reward itself is often turns.
                AddLog(db, player, before, "COMP", 0, claim.Summary, now);
                await db.SaveChangesAsync(ct);

                return Results.Ok(new ClaimCompResponse(
                    claim.Summary,
                    claim.TurnsGranted,
                    claim.CashPaid,
                    claim.HeatCleared,
                    player.Turns,
                    player.Cash,
                    Math.Round(player.Heat, 1),
                    await casino.BoardAsync(player, ct)));
            }
            catch (GameRuleException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization();
    }
}
