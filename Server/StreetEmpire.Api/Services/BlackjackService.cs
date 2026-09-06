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
/// A round is a row in the database for as long as it is live. Everything that decides it - the shoe
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
        var config = _options.Casino.Blackjack;
        var live = await LiveRoundAsync(player.Id, ct);
        return new BlackjackBoardResponse(
            config.Enabled,
            TablesFor(player).ToList(),
            Math.Max(0, config.SpinTurnCost),
            config.DealerHitsSoft17,
            config.BlackjackPaysNumerator,
            config.BlackjackPaysDenominator,
            Math.Max(0, config.MaxSplits),
            config.InsuranceEnabled,
            live is null ? null : ViewOf(player, live),
            (await RecentAsync(player.Id, _options.Casino.HistoryDepth, ct)).ToList());
    }

    /// <summary>Deals a round, taking the stake and the turn. Refuses if one is already live.</summary>
    public async Task<BlackjackHand> DealAsync(Player player, string? tableKey, long bet, DateTime nowUtc, CancellationToken ct)
    {
        TravelGate.EnsureLanded(player);
        var config = _options.Casino.Blackjack;
        if (!config.Enabled)
            throw new GameRuleException("The blackjack pit is shut.");

        if (await LiveRoundAsync(player.Id, ct) is not null)
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
        if (player.Cash < bet)
            throw new GameRuleException($"You are carrying {player.Cash:C0}.");

        player.Turns -= turnCost;
        Stake(player, table, bet);

        var deck = Shuffle(BuildShoe(config.Decks));
        // Player, dealer, player, dealer, which is the order it comes off a real shoe. With the shoe
        // shuffled fresh it changes no odds - but a deal that dealt two to one hand and then two to the
        // other would be a different thing wearing the same name, and the hole card would be the third
        // card off the shoe rather than the fourth.
        var first = new List<string>();
        var dealerCards = new List<string>();
        first.Add(Draw(deck));
        dealerCards.Add(Draw(deck));
        first.Add(Draw(deck));
        dealerCards.Add(Draw(deck));

        var round = new BlackjackHand
        {
            PlayerId = player.Id,
            TableKey = table.Key,
            Bet = bet,
            DeckJson = Write(deck),
            HandsJson = WriteHands([new PlayerHand(first, bet, false, BlackjackStatus.Playing, 0)]),
            DealerCardsJson = Write(dealerCards),
            ActiveHand = 0,
            Status = BlackjackStatus.Playing,
            CreatedAtUtc = nowUtc
        };

        // Insurance is offered before anybody looks at the hole card, so a round that is asking has to
        // hold still - no natural settles itself until the question has been answered, because settling
        // it would be looking. It is only asked when the player could actually cover it: a question
        // with one possible answer is not a question, it is a click.
        var insuranceCost = bet / 2;
        if (config.InsuranceEnabled && dealerCards[0][0] == 'A' && insuranceCost > 0 && player.Cash >= insuranceCost)
            round.InsuranceOffered = true;
        // A natural settles itself: nobody is asked whether they want another card on twenty-one.
        else if (Best(first) == 21 || Best(dealerCards) == 21)
            Finish(player, round, nowUtc);

        db.BlackjackHands.Add(round);
        return round;
    }

    /// <summary>Another card on the hand being played. Busting closes that hand and moves along.</summary>
    public async Task<BlackjackHand> HitAsync(Player player, DateTime nowUtc, CancellationToken ct)
    {
        var round = await LiveRoundAsync(player.Id, ct) ?? throw new GameRuleException("There is no hand in front of you.");
        EnsureInsuranceAnswered(round);
        var deck = Read(round.DeckJson);
        var hands = ReadHands(round.HandsJson);
        var hand = Active(round, hands);

        hand.Cards.Add(Draw(deck));
        if (Best(hand.Cards) > 21) hand.Status = BlackjackStatus.PlayerBust;

        round.DeckJson = Write(deck);
        round.HandsJson = WriteHands(hands);
        if (hand.Status != BlackjackStatus.Playing) Advance(player, round, hands, nowUtc);
        return round;
    }

    /// <summary>Done with this hand. Moves to the next, or turns the dealer over if there is none.</summary>
    public async Task<BlackjackHand> StandAsync(Player player, DateTime nowUtc, CancellationToken ct)
    {
        var round = await LiveRoundAsync(player.Id, ct) ?? throw new GameRuleException("There is no hand in front of you.");
        EnsureInsuranceAnswered(round);
        var hands = ReadHands(round.HandsJson);
        Active(round, hands).Status = BlackjackStatus.Stood;
        round.HandsJson = WriteHands(hands);
        Advance(player, round, hands, nowUtc);
        return round;
    }

    /// <summary>Doubles this hand's stake for exactly one more card, which is the whole of the bet.</summary>
    public async Task<BlackjackHand> DoubleAsync(Player player, DateTime nowUtc, CancellationToken ct)
    {
        var round = await LiveRoundAsync(player.Id, ct) ?? throw new GameRuleException("There is no hand in front of you.");
        EnsureInsuranceAnswered(round);
        var hands = ReadHands(round.HandsJson);
        var hand = Active(round, hands);

        if (hand.Doubled)
            throw new GameRuleException("You have already doubled on this hand.");
        if (hand.Cards.Count != 2)
            throw new GameRuleException("Doubling is for the first two cards.");
        if (player.Cash < hand.Bet)
            throw new GameRuleException($"Doubling is another {hand.Bet:C0} and you are carrying {player.Cash:C0}.");

        var table = _options.Casino.Blackjack.Table(round.TableKey);
        if (table is not null) Stake(player, table, hand.Bet);
        else player.Cash -= hand.Bet;
        round.Bet += hand.Bet;

        var deck = Read(round.DeckJson);
        hand.Cards.Add(Draw(deck));
        hand.Bet *= 2;
        hand.Doubled = true;
        // One card and no more, whichever way it lands.
        hand.Status = Best(hand.Cards) > 21 ? BlackjackStatus.PlayerBust : BlackjackStatus.Stood;

        round.DeckJson = Write(deck);
        round.HandsJson = WriteHands(hands);
        Advance(player, round, hands, nowUtc);
        return round;
    }

    /// <summary>
    /// Splits a pair into two hands, each getting a second card and its own stake.
    ///
    /// Aces are the exception every house makes: one card each and no more asked, because a pair of
    /// aces resplit and drawn freely is the best hand in the game by a distance.
    /// </summary>
    public async Task<BlackjackHand> SplitAsync(Player player, DateTime nowUtc, CancellationToken ct)
    {
        var round = await LiveRoundAsync(player.Id, ct) ?? throw new GameRuleException("There is no hand in front of you.");
        EnsureInsuranceAnswered(round);
        var config = _options.Casino.Blackjack;
        var hands = ReadHands(round.HandsJson);
        var hand = Active(round, hands);

        if (hand.Cards.Count != 2)
            throw new GameRuleException("Splitting is for the first two cards.");
        if (Value(hand.Cards[0]) != Value(hand.Cards[1]))
            throw new GameRuleException("Splitting takes two cards of the same value.");
        if (round.Splits >= Math.Max(0, config.MaxSplits))
            throw new GameRuleException($"This table allows {Math.Max(0, config.MaxSplits):N0} split(s) a round.");
        if (player.Cash < hand.Bet)
            throw new GameRuleException($"Splitting is another {hand.Bet:C0} and you are carrying {player.Cash:C0}.");

        var table = config.Table(round.TableKey);
        if (table is not null) Stake(player, table, hand.Bet);
        else player.Cash -= hand.Bet;
        round.Bet += hand.Bet;
        round.Splits++;

        var deck = Read(round.DeckJson);
        var moved = hand.Cards[1];
        hand.Cards.RemoveAt(1);
        hand.Cards.Add(Draw(deck));

        var second = new PlayerHand([moved, Draw(deck)], hand.Bet, false, BlackjackStatus.Playing, 0);
        hands.Insert(round.ActiveHand + 1, second);

        // Split aces take one card each and that is the hand.
        if (moved[0] == 'A' && config.OneCardOnSplitAces)
        {
            hand.Status = BlackjackStatus.Stood;
            second.Status = BlackjackStatus.Stood;
        }

        round.DeckJson = Write(deck);
        round.HandsJson = WriteHands(hands);
        if (hand.Status != BlackjackStatus.Playing) Advance(player, round, hands, nowUtc);
        return round;
    }

    /// <summary>
    /// Answers the insurance question, which is also the moment the dealer looks at the hole card.
    ///
    /// Insurance is a side bet of half the stake that the hole card is worth ten, and it pays two to
    /// one. Two to one is a fair price on one card in three; the hole card is a ten four times in
    /// thirteen. The house holds about seven percent of everything put up here, which is more than
    /// twice what it holds on the hand itself, and that gap is the whole reason the bet exists.
    /// </summary>
    public async Task<BlackjackHand> InsuranceAsync(Player player, bool take, DateTime nowUtc, CancellationToken ct)
    {
        var round = await LiveRoundAsync(player.Id, ct) ?? throw new GameRuleException("There is no hand in front of you.");
        if (!AwaitingInsurance(round))
            throw new GameRuleException("Nobody has offered you insurance.");

        var hands = ReadHands(round.HandsJson);
        var cost = InsuranceCost(hands);

        if (take)
        {
            if (player.Cash < cost)
                throw new GameRuleException($"Insurance is {cost:C0} and you are carrying {player.Cash:C0}.");

            var table = _options.Casino.Blackjack.Table(round.TableKey);
            if (table is not null) Stake(player, table, cost);
            else player.Cash -= cost;
            round.InsuranceBet = cost;
            round.Bet += cost;
        }

        round.InsuranceAnswered = true;

        // The dealer looks now. Insurance is decided on what is under there and nothing that happens
        // afterwards can change it, so it is settled here and carried to the payout.
        var dealerCards = Read(round.DealerCardsJson);
        var dealerNatural = Best(dealerCards) == 21;
        if (dealerNatural) round.InsurancePayout = round.InsuranceBet * 3;

        // And now the settle the question was holding back.
        if (dealerNatural || Best(hands[0].Cards) == 21) Finish(player, round, nowUtc);
        return round;
    }

    /// <summary>
    /// Gives the hand up for half the stake, before the dealer draws anything.
    ///
    /// Late surrender: by the time it can be asked for, the dealer has already been shown not to be
    /// holding a natural, because a natural settles the round where it stands. It is the only move in
    /// the game that ends a hand without playing it, so it is the first two cards only, never after a
    /// split, and only at a table that offers it.
    /// </summary>
    public async Task<BlackjackHand> SurrenderAsync(Player player, DateTime nowUtc, CancellationToken ct)
    {
        var round = await LiveRoundAsync(player.Id, ct) ?? throw new GameRuleException("There is no hand in front of you.");
        EnsureInsuranceAnswered(round);

        var table = _options.Casino.Blackjack.Table(round.TableKey);
        if (table is null || !table.AllowsSurrender)
            throw new GameRuleException($"{table?.Name ?? "This table"} does not take surrenders.");

        var hands = ReadHands(round.HandsJson);
        var hand = Active(round, hands);

        if (round.Splits > 0 || hands.Count > 1)
            throw new GameRuleException("A hand that has been split has to be played out.");
        if (hand.Cards.Count != 2)
            throw new GameRuleException("Surrender is for the first two cards.");

        hand.Status = BlackjackStatus.Surrendered;
        // Half back, and the house keeps the odd dollar on anything odd. Every table here is priced in
        // whole hundreds, so on anything anybody can actually bet this is exact.
        hand.Payout = hand.Bet / 2;

        round.HandsJson = WriteHands(hands);
        Advance(player, round, hands, nowUtc);
        return round;
    }

    /// <summary>Whether the table is waiting on an insurance answer and nothing else.</summary>
    private static bool AwaitingInsurance(BlackjackHand round) => round.InsuranceOffered && !round.InsuranceAnswered;

    private static void EnsureInsuranceAnswered(BlackjackHand round)
    {
        if (AwaitingInsurance(round))
            throw new GameRuleException("The dealer is showing an ace and is waiting on insurance.");
    }

    /// <summary>
    /// Half the stake, which is what insurance costs wherever it is offered.
    ///
    /// Read off the first hand rather than off the round, because the round's total is what insurance
    /// is about to be added to and taking half of that would price the bet off itself.
    /// </summary>
    private static long InsuranceCost(List<PlayerHand> hands) => hands.Count == 0 ? 0 : hands[0].Bet / 2;

    /// <summary>
    /// Moves to the next hand that still wants a decision, or ends the player's side of the round.
    ///
    /// A hand can arrive already finished - split aces, or a split that made twenty-one - so this
    /// walks rather than stepping once.
    /// </summary>
    private void Advance(Player player, BlackjackHand round, List<PlayerHand> hands, DateTime nowUtc)
    {
        var next = round.ActiveHand;
        while (next < hands.Count && hands[next].Status != BlackjackStatus.Playing) next++;
        round.ActiveHand = next;

        if (next >= hands.Count) Finish(player, round, nowUtc);
    }

    /// <summary>
    /// The dealer plays once and every hand is answered against them.
    ///
    /// Once, however many hands the player has, which is the thing that makes splitting a real decision
    /// rather than free rolls: the same dealer hand beats or loses to all of them together.
    /// </summary>
    private void Finish(Player player, BlackjackHand round, DateTime nowUtc)
    {
        var config = _options.Casino.Blackjack;
        var deck = Read(round.DeckJson);
        var hands = ReadHands(round.HandsJson);
        var dealerCards = Read(round.DealerCardsJson);
        var dealerNatural = dealerCards.Count == 2 && Best(dealerCards) == 21;

        // No reason to draw if every hand has gone over or been given up: there is nothing left for the
        // dealer to beat and the cards would only be burned.
        if (hands.Any(x => x.Status != BlackjackStatus.Surrendered && Best(x.Cards) <= 21) && !dealerNatural)
            while (DealerDraws(dealerCards))
                dealerCards.Add(Draw(deck));

        var dealerBest = Best(dealerCards);
        var dealerBust = dealerBest > 21;
        long payout = 0;

        foreach (var hand in hands)
        {
            // A hand that was given up settled itself at half the stake and is not played against.
            if (hand.Status == BlackjackStatus.Surrendered)
            {
                payout += hand.Payout;
                continue;
            }

            var best = Best(hand.Cards);
            // A natural is two cards on the hand as dealt. Twenty-one made after a split is twenty-one
            // and is paid like it, which is the rule every house in the world keeps.
            var natural = hand.Cards.Count == 2 && best == 21 && hands.Count == 1;

            if (natural && dealerNatural) { hand.Status = BlackjackStatus.Push; hand.Payout = hand.Bet; }
            else if (natural)
            {
                hand.Status = BlackjackStatus.PlayerBlackjack;
                hand.Payout = hand.Bet + hand.Bet * Math.Max(1, config.BlackjackPaysNumerator) / Math.Max(1, config.BlackjackPaysDenominator);
            }
            else if (best > 21) { hand.Status = BlackjackStatus.PlayerBust; hand.Payout = 0; }
            else if (dealerNatural) { hand.Status = BlackjackStatus.DealerWin; hand.Payout = 0; }
            else if (dealerBust) { hand.Status = BlackjackStatus.DealerBust; hand.Payout = hand.Bet * 2; }
            else if (best > dealerBest) { hand.Status = BlackjackStatus.PlayerWin; hand.Payout = hand.Bet * 2; }
            else if (best < dealerBest) { hand.Status = BlackjackStatus.DealerWin; hand.Payout = 0; }
            else { hand.Status = BlackjackStatus.Push; hand.Payout = hand.Bet; }

            payout += hand.Payout;
        }

        // Insurance was decided when the dealer looked and rides along to the same settle.
        payout += round.InsurancePayout;

        round.DeckJson = Write(deck);
        round.DealerCardsJson = Write(dealerCards);
        round.HandsJson = WriteHands(hands);
        round.Payout = payout;
        round.SettledAtUtc = nowUtc;
        round.ActiveHand = hands.Count;
        // One hand can name itself; several cannot, so a split round says so and the hands carry the detail.
        round.Status = hands.Count == 1 ? hands[0].Status : BlackjackStatus.Split;
        player.Cash += payout;

        db.CasinoTransactions.Add(new CasinoTransaction
        {
            PlayerId = player.Id,
            GameType = "blackjack",
            MachineKey = round.TableKey,
            Paylines = hands.Count,
            WinningPaylines = hands.Count(x => x.Payout > x.Bet),
            BetAmount = round.Bet,
            PayoutAmount = payout,
            NetResult = payout - round.Bet,
            Outcome = round.Status,
            DetailJson = Write(new StoredRound(hands.Select(x => x.Cards).ToList(), dealerCards, dealerBest)),
            CreatedAtUtc = nowUtc
        });
    }

    private void Stake(Player player, BlackjackTableOptions table, long amount)
    {
        player.Cash -= amount;
        player.CasinoRep = Math.Max(0, player.CasinoRep + RepFor(table, amount));
        player.CasinoCompsCents = Math.Max(0, player.CasinoCompsCents + _options.Casino.CompsCentsFor(amount));
    }

    private static PlayerHand Active(BlackjackHand round, List<PlayerHand> hands)
    {
        if (round.ActiveHand < 0 || round.ActiveHand >= hands.Count)
            throw new GameRuleException("There is no hand waiting on you.");
        var hand = hands[round.ActiveHand];
        if (hand.Status != BlackjackStatus.Playing)
            throw new GameRuleException("That hand is already finished.");
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
        // Fisher-Yates, walked from the back. A shoe is shuffled fresh for every round, so nothing
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
    /// A round as the table shows it.
    ///
    /// The shoe never appears, and while the round is live neither does the dealer's second card or any
    /// total that would give it away. Once it is over there is nothing left to protect and the whole
    /// thing is turned face up.
    /// </summary>
    private BlackjackRoundView ViewOf(Player player, BlackjackHand round)
    {
        var config = _options.Casino.Blackjack;
        var hands = ReadHands(round.HandsJson);
        var dealerCards = Read(round.DealerCardsJson);
        var over = BlackjackStatus.IsOver(round.Status);
        var shown = over ? dealerCards : dealerCards.Take(1).ToList();
        var active = round.ActiveHand;
        var awaiting = AwaitingInsurance(round);
        var insuranceCost = InsuranceCost(hands);
        var surrenders = config.Table(round.TableKey)?.AllowsSurrender == true;

        return new BlackjackRoundView(
            round.Id,
            round.TableKey,
            round.Bet,
            hands.Select((hand, index) =>
            {
                var live = !over && index == active && hand.Status == BlackjackStatus.Playing;
                // Nothing about the hand is decidable while the table is waiting on insurance.
                var decidable = live && !awaiting;
                return new BlackjackHandView(
                    index,
                    hand.Cards,
                    Best(hand.Cards),
                    IsSoft(hand.Cards),
                    hand.Bet,
                    hand.Status,
                    hand.Payout,
                    hand.Payout - hand.Bet,
                    live,
                    // Doubling and splitting are both first-two-cards decisions, and both need the money.
                    decidable && !hand.Doubled && hand.Cards.Count == 2 && player.Cash >= hand.Bet,
                    decidable
                        && hand.Cards.Count == 2
                        && Value(hand.Cards[0]) == Value(hand.Cards[1])
                        && round.Splits < Math.Max(0, config.MaxSplits)
                        && player.Cash >= hand.Bet,
                    // Surrender hands money back rather than taking it, so it needs no cash check -
                    // only a table that takes it and a hand nobody has touched yet.
                    decidable && surrenders && hand.Cards.Count == 2 && round.Splits == 0 && hands.Count == 1);
            }).ToList(),
            shown,
            over ? Best(dealerCards) : Best(shown),
            !over,
            round.Status,
            round.Payout,
            round.Payout - round.Bet,
            awaiting,
            insuranceCost,
            awaiting && player.Cash >= insuranceCost,
            round.InsuranceBet,
            round.InsurancePayout);
    }

    public BlackjackRoundView View(Player player, BlackjackHand round) => ViewOf(player, round);

    private Task<BlackjackHand?> LiveRoundAsync(Guid playerId, CancellationToken ct)
        => db.BlackjackHands
            .Where(x => x.PlayerId == playerId && x.Status == BlackjackStatus.Playing)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);

    private static string Write<T>(T value) => JsonSerializer.Serialize(value);

    private static List<string> Read(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    private static string WriteHands(List<PlayerHand> hands) => JsonSerializer.Serialize(hands);

    private static List<PlayerHand> ReadHands(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<PlayerHand>>(json) ?? []; }
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
                table.AllowsSurrender,
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

        return rows.Select(round =>
        {
            var hands = ReadHands(round.HandsJson);
            var dealerCards = Read(round.DealerCardsJson);
            return new BlackjackRowResponse(
                round.Id,
                round.TableKey,
                _options.Casino.Blackjack.Table(round.TableKey)?.Name ?? round.TableKey,
                hands.Select(x => new BlackjackRowHand(x.Cards, Best(x.Cards), x.Bet, x.Status, x.Payout - x.Bet)).ToList(),
                dealerCards,
                Best(dealerCards),
                round.Status,
                round.Bet,
                round.Payout,
                round.Payout - round.Bet,
                round.SettledAtUtc ?? round.CreatedAtUtc);
        }).ToList();
    }

    /// <summary>One of the player's hands. Mutable because a round is played by changing them.</summary>
    private sealed class PlayerHand(List<string> cards, long bet, bool doubled, string status, long payout)
    {
        public List<string> Cards { get; set; } = cards;
        public long Bet { get; set; } = bet;
        public bool Doubled { get; set; } = doubled;
        public string Status { get; set; } = status;
        public long Payout { get; set; } = payout;
    }

    private sealed record StoredRound(List<List<string>> Hands, List<string> Dealer, int DealerBest);
}
