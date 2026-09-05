using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

/// <summary>
/// The only game on this floor where the player can be wrong.
///
/// Slots and the wheel take a stake and answer; blackjack asks a question back, and the answer changes
/// what the game returns. Played well it hands back a shade under 100%, which makes it far and away
/// the best thing in the casino and is exactly why it is the last room to open.
///
/// A hand is a row in the database for as long as it is live. Everything that decides it - the shoe
/// and the dealer's face-down card - is kept there and trimmed out of every response, because a game
/// of decisions is only a game if the person deciding cannot see the answer.
/// </summary>
public sealed class BlackjackService(
    GameDbContext db,
    IOptionsSnapshot<GameOptions> options,
    IGameRandom random,
    EconomyService economy)
{
    private const string Ranks = "A23456789TJQK";
    private const string Suits = "SHDC";

    private readonly GameOptions _options = options.Value;

    public async Task<BlackjackBoardResponse> BoardAsync(Player player, CancellationToken ct)
    {
        var live = await LiveHandAsync(player.Id, ct);
        return new BlackjackBoardResponse(
            _options.Casino.Blackjack.Enabled,
            TablesFor(player).ToList(),
            Math.Max(0, _options.Casino.Blackjack.SpinTurnCost),
            _options.Casino.Blackjack.DealerHitsSoft17,
            _options.Casino.Blackjack.BlackjackPaysNumerator,
            _options.Casino.Blackjack.BlackjackPaysDenominator,
            live is null ? null : ViewOf(live),
            (await RecentAsync(player.Id, _options.Casino.HistoryDepth, ct)).ToList());
    }

    /// <summary>Deals a hand, taking the stake and the turn. Refuses if one is already live.</summary>
    public async Task<BlackjackHand> DealAsync(Player player, string? tableKey, long bet, DateTime nowUtc, CancellationToken ct)
    {
        TravelGate.EnsureLanded(player);
        var config = _options.Casino.Blackjack;
        if (!config.Enabled)
            throw new GameRuleException("The blackjack pit is shut.");

        if (await LiveHandAsync(player.Id, ct) is not null)
            throw new GameRuleException("You are already in a hand. Finish it first.");

        var table = config.Table(tableKey) ?? config.Tables.FirstOrDefault()
            ?? throw new GameRuleException("There are no blackjack tables open.");

        var locked = LockedReason(player, table);
        if (locked is not null)
            throw new GameRuleException(locked);
        if (bet < table.MinBet || bet > table.MaxBet)
            throw new GameRuleException($"{table.Name} takes bets from {table.MinBet:C0} to {table.MaxBet:C0}.");

        var turnCost = Math.Max(0, config.SpinTurnCost);
        if (turnCost > 0 && player.Turns < turnCost)
            throw new GameRuleException($"A hand is {turnCost:N0} turn(s) and you have {player.Turns:N0}.");
        // The stake has to cover a double as well, or the decision is not really on the table.
        if (player.Cash < bet)
            throw new GameRuleException($"You are carrying {player.Cash:C0}.");

        player.Turns -= turnCost;
        player.Cash -= bet;
        player.CasinoRep = Math.Max(0, player.CasinoRep + RepFor(table, bet));
        player.CasinoComps = Math.Max(0, player.CasinoComps + bet * Math.Max(0, _options.Casino.CompsPerDollarWagered));

        var deck = Shuffle(BuildShoe(config.Decks));
        // Player, dealer, player, dealer, which is the order it comes off a real shoe. With the shoe
        // shuffled fresh it changes no odds - but a deal that dealt two to one hand and then two to the
        // other would be a different thing wearing the same name, and the hole card would be the third
        // card off the shoe rather than the fourth.
        var playerCards = new List<string>();
        var dealerCards = new List<string>();
        playerCards.Add(Draw(deck));
        dealerCards.Add(Draw(deck));
        playerCards.Add(Draw(deck));
        dealerCards.Add(Draw(deck));

        var hand = new BlackjackHand
        {
            PlayerId = player.Id,
            TableKey = table.Key,
            Bet = bet,
            DeckJson = JsonSerializer.Serialize(deck),
            PlayerCardsJson = JsonSerializer.Serialize(playerCards),
            DealerCardsJson = JsonSerializer.Serialize(dealerCards),
            Status = BlackjackStatus.Playing,
            CreatedAtUtc = nowUtc
        };

        // A natural settles itself: nobody is asked whether they want another card on twenty-one.
        if (Best(playerCards) == 21 || Best(dealerCards) == 21)
            Settle(player, hand, playerCards, dealerCards, nowUtc);

        db.BlackjackHands.Add(hand);
        return hand;
    }

    /// <summary>Another card. Busting settles the hand where it stands.</summary>
    public async Task<BlackjackHand> HitAsync(Player player, DateTime nowUtc, CancellationToken ct)
    {
        var hand = await LiveHandAsync(player.Id, ct) ?? throw new GameRuleException("There is no hand in front of you.");
        var deck = Read(hand.DeckJson);
        var playerCards = Read(hand.PlayerCardsJson);

        playerCards.Add(Draw(deck));
        hand.DeckJson = JsonSerializer.Serialize(deck);
        hand.PlayerCardsJson = JsonSerializer.Serialize(playerCards);

        if (Best(playerCards) > 21)
            Settle(player, hand, playerCards, Read(hand.DealerCardsJson), nowUtc);

        return hand;
    }

    /// <summary>Doubles the stake for exactly one more card, which is the whole of the bet's point.</summary>
    public async Task<BlackjackHand> DoubleAsync(Player player, DateTime nowUtc, CancellationToken ct)
    {
        var hand = await LiveHandAsync(player.Id, ct) ?? throw new GameRuleException("There is no hand in front of you.");
        var playerCards = Read(hand.PlayerCardsJson);

        if (hand.Doubled)
            throw new GameRuleException("You have already doubled on this hand.");
        if (playerCards.Count != 2)
            throw new GameRuleException("Doubling is for the first two cards.");
        if (player.Cash < hand.Bet)
            throw new GameRuleException($"Doubling is another {hand.Bet:C0} and you are carrying {player.Cash:C0}.");

        var table = _options.Casino.Blackjack.Table(hand.TableKey);
        player.Cash -= hand.Bet;
        if (table is not null)
        {
            player.CasinoRep = Math.Max(0, player.CasinoRep + RepFor(table, hand.Bet));
            player.CasinoComps = Math.Max(0, player.CasinoComps + hand.Bet * Math.Max(0, _options.Casino.CompsPerDollarWagered));
        }

        hand.Bet *= 2;
        hand.Doubled = true;

        var deck = Read(hand.DeckJson);
        playerCards.Add(Draw(deck));
        hand.DeckJson = JsonSerializer.Serialize(deck);
        hand.PlayerCardsJson = JsonSerializer.Serialize(playerCards);

        // One card and no more, so the hand goes straight to the dealer either way.
        if (Best(playerCards) > 21) Settle(player, hand, playerCards, Read(hand.DealerCardsJson), nowUtc);
        else await StandAsync(player, nowUtc, ct);

        return hand;
    }

    /// <summary>Turns the hand over. The dealer draws to whatever the house rule says and it settles.</summary>
    public async Task<BlackjackHand> StandAsync(Player player, DateTime nowUtc, CancellationToken ct)
    {
        var hand = await LiveHandAsync(player.Id, ct) ?? throw new GameRuleException("There is no hand in front of you.");
        var deck = Read(hand.DeckJson);
        var playerCards = Read(hand.PlayerCardsJson);
        var dealerCards = Read(hand.DealerCardsJson);

        // The dealer has no decisions. That is the trade for seeing one of their cards all along.
        while (DealerDraws(dealerCards))
            dealerCards.Add(Draw(deck));

        hand.DeckJson = JsonSerializer.Serialize(deck);
        hand.DealerCardsJson = JsonSerializer.Serialize(dealerCards);
        Settle(player, hand, playerCards, dealerCards, nowUtc);
        return hand;
    }

    private bool DealerDraws(IReadOnlyList<string> dealer)
    {
        var best = Best(dealer);
        if (best > 17) return false;
        if (best < 17) return true;
        // Seventeen with an ace still counted as eleven is a soft seventeen, and some houses hit it.
        return _options.Casino.Blackjack.DealerHitsSoft17 && IsSoft(dealer);
    }

    private void Settle(Player player, BlackjackHand hand, List<string> playerCards, List<string> dealerCards, DateTime nowUtc)
    {
        var config = _options.Casino.Blackjack;
        var playerBest = Best(playerCards);
        var dealerBest = Best(dealerCards);
        var playerNatural = playerCards.Count == 2 && playerBest == 21;
        var dealerNatural = dealerCards.Count == 2 && dealerBest == 21;

        long payout;
        string status;
        if (playerNatural && dealerNatural) { status = BlackjackStatus.Push; payout = hand.Bet; }
        else if (playerNatural)
        {
            status = BlackjackStatus.PlayerBlackjack;
            // Stake back plus the house's odds on a natural, which is why it is worth its own case.
            payout = hand.Bet + hand.Bet * Math.Max(1, config.BlackjackPaysNumerator) / Math.Max(1, config.BlackjackPaysDenominator);
        }
        else if (dealerNatural) { status = BlackjackStatus.DealerWin; payout = 0; }
        else if (playerBest > 21) { status = BlackjackStatus.PlayerBust; payout = 0; }
        else if (dealerBest > 21) { status = BlackjackStatus.DealerBust; payout = hand.Bet * 2; }
        else if (playerBest > dealerBest) { status = BlackjackStatus.PlayerWin; payout = hand.Bet * 2; }
        else if (playerBest < dealerBest) { status = BlackjackStatus.DealerWin; payout = 0; }
        else { status = BlackjackStatus.Push; payout = hand.Bet; }

        hand.Status = status;
        hand.Payout = payout;
        hand.SettledAtUtc = nowUtc;
        player.Cash += payout;

        db.CasinoTransactions.Add(new CasinoTransaction
        {
            PlayerId = player.Id,
            GameType = "blackjack",
            MachineKey = hand.TableKey,
            Paylines = 1,
            WinningPaylines = payout > hand.Bet ? 1 : 0,
            BetAmount = hand.Bet,
            PayoutAmount = payout,
            NetResult = payout - hand.Bet,
            Outcome = status,
            DetailJson = JsonSerializer.Serialize(new StoredHand(playerCards, dealerCards, playerBest, dealerBest)),
            CreatedAtUtc = nowUtc
        });
    }

    // -- The cards themselves -------------------------------------------------------------------------

    private static List<string> BuildShoe(int decks)
    {
        var shoe = new List<string>(52 * Math.Max(1, decks));
        for (var deck = 0; deck < Math.Max(1, decks); deck++)
            foreach (var suit in Suits)
                foreach (var rank in Ranks)
                    shoe.Add($"{rank}{suit}");
        return shoe;
    }

    private List<string> Shuffle(List<string> cards)
    {
        // Fisher-Yates, walked from the back. A shoe is shuffled fresh for every hand, so nothing
        // carries between them and counting cards is not a thing anybody can do here.
        for (var i = cards.Count - 1; i > 0; i--)
        {
            var j = random.NextInclusive(0, i);
            (cards[i], cards[j]) = (cards[j], cards[i]);
        }

        return cards;
    }

    private static string Draw(List<string> deck)
    {
        if (deck.Count == 0) throw new GameRuleException("The shoe is empty.");
        var card = deck[0];
        deck.RemoveAt(0);
        return card;
    }

    /// <summary>What a card is worth with its ace counted low. Faces are ten, an ace is one.</summary>
    internal static int Value(string card) => card[0] switch
    {
        'A' => 1,
        'T' or 'J' or 'Q' or 'K' => 10,
        _ => card[0] - '0'
    };

    /// <summary>
    /// The best a hand can be without going over, which is the only total that ever matters.
    ///
    /// An ace is one or eleven and never both, and a hand holding two of them can only ever use one
    /// as eleven - two would be twenty-two before anything else was counted.
    /// </summary>
    internal static int Best(IReadOnlyList<string> cards)
    {
        var total = cards.Sum(Value);
        return cards.Any(x => x[0] == 'A') && total + 10 <= 21 ? total + 10 : total;
    }

    /// <summary>Whether an ace in this hand is still being counted as eleven.</summary>
    internal static bool IsSoft(IReadOnlyList<string> cards)
        => cards.Any(x => x[0] == 'A') && cards.Sum(Value) + 10 <= 21;

    // -- What the player is allowed to see ------------------------------------------------------------

    /// <summary>
    /// A hand as the table shows it.
    ///
    /// The shoe never appears, and while the hand is live neither does the dealer's second card or any
    /// total that would give it away. Once it is over there is nothing left to protect and the whole
    /// thing is turned face up.
    /// </summary>
    private BlackjackHandView ViewOf(BlackjackHand hand)
    {
        var playerCards = Read(hand.PlayerCardsJson);
        var dealerCards = Read(hand.DealerCardsJson);
        var over = BlackjackStatus.IsOver(hand.Status);
        var shown = over ? dealerCards : dealerCards.Take(1).ToList();

        return new BlackjackHandView(
            hand.Id,
            hand.TableKey,
            hand.Bet,
            playerCards,
            Best(playerCards),
            IsSoft(playerCards),
            shown,
            over ? Best(dealerCards) : Best(shown),
            !over,
            hand.Status,
            hand.Payout,
            hand.Payout - hand.Bet,
            // Doubling is a first-two-cards decision and needs the money for it to be a decision.
            !over && !hand.Doubled && playerCards.Count == 2);
    }

    public BlackjackHandView View(BlackjackHand hand) => ViewOf(hand);

    private Task<BlackjackHand?> LiveHandAsync(Guid playerId, CancellationToken ct)
        => db.BlackjackHands
            .Where(x => x.PlayerId == playerId && x.Status == BlackjackStatus.Playing)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);

    private static List<string> Read(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    private IEnumerable<BlackjackTableResponse> TablesFor(Player player)
        => _options.Casino.Blackjack.Tables.Select(table =>
        {
            var locked = LockedReason(player, table);
            return new BlackjackTableResponse(
                table.Key,
                table.Name,
                table.Blurb,
                table.MinBet,
                table.MaxBet,
                Math.Max(1, table.MinCasinoRepLevel),
                table.MinCasinoRepLevel > 1 ? _options.Casino.LevelName(table.MinCasinoRepLevel) : null,
                locked is not null,
                locked);
        });

    private string? LockedReason(Player player, BlackjackTableOptions table)
    {
        var required = Math.Max(1, table.MinCasinoRepLevel);
        if (CasinoRep.LevelOf(player, _options) < required)
            return $"{table.Name} opens at {_options.Casino.LevelName(required)} on the casino floor.";

        if (table.MinNetWorth <= 0) return null;

        var worth = economy.CalculateNetWorth(player);
        return worth >= table.MinNetWorth
            ? null
            : $"{table.Name} opens at {table.MinNetWorth:C0} net worth. You are at {worth:C0}.";
    }

    private double RepFor(BlackjackTableOptions table, long staked)
        => Math.Max(0, _options.Casino.RepPerMaxBetSpin) * staked / Math.Max(1, table.MaxBet);

    private async Task<IReadOnlyList<BlackjackRowResponse>> RecentAsync(Guid playerId, int take, CancellationToken ct)
    {
        var rows = await db.BlackjackHands.AsNoTracking()
            .Where(x => x.PlayerId == playerId && x.Status != BlackjackStatus.Playing)
            .OrderByDescending(x => x.SettledAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(Math.Clamp(take, 1, 50))
            .ToListAsync(ct);

        return rows.Select(hand =>
        {
            var playerCards = Read(hand.PlayerCardsJson);
            var dealerCards = Read(hand.DealerCardsJson);
            return new BlackjackRowResponse(
                hand.Id,
                hand.TableKey,
                _options.Casino.Blackjack.Table(hand.TableKey)?.Name ?? hand.TableKey,
                playerCards,
                Best(playerCards),
                dealerCards,
                Best(dealerCards),
                hand.Status,
                hand.Bet,
                hand.Payout,
                hand.Payout - hand.Bet,
                hand.SettledAtUtc ?? hand.CreatedAtUtc);
        }).ToList();
    }

    private sealed record StoredHand(List<string> Player, List<string> Dealer, int PlayerBest, int DealerBest);
}
