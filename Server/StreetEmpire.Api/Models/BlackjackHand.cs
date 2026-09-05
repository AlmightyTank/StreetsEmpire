namespace StreetEmpire.Api.Models;

/// <summary>
/// A round of blackjack, from the deal to the settle.
///
/// A round rather than a hand, because splitting turns one into several and they are all still the
/// same visit to the table: one shoe, one dealer hand, one turn paid for. The player's side is a list
/// held in <see cref="HandsJson"/> and it usually has one thing in it.
///
/// It lives in the database rather than in the request because blackjack is the one game on this floor
/// that takes more than a single exchange: a player draws, looks, draws again. Everything that decides
/// the outcome - the shoe and the card the dealer has face down - has to sit somewhere the player
/// cannot read, and the only such place is here.
/// </summary>
public sealed class BlackjackHand
{
    public long Id { get; set; }
    public Guid PlayerId { get; set; }
    public Player Player { get; set; } = null!;
    public string TableKey { get; set; } = string.Empty;

    /// <summary>
    /// Everything staked on the round across every hand in it. Splitting and doubling both add to it,
    /// so this is what the round cost rather than what one hand did.
    /// </summary>
    public long Bet { get; set; }

    /// <summary>
    /// The shoe, in the order it will come out, minus what has already been dealt.
    ///
    /// Never leaves the server. It is the answer to every question the player is being asked, so
    /// putting it anywhere near a response would be handing them the paper.
    /// </summary>
    public string DeckJson { get; set; } = string.Empty;

    /// <summary>The player's hands, in the order they are played. One unless somebody splits.</summary>
    public string HandsJson { get; set; } = string.Empty;

    /// <summary>Which of them is being played. Past the end means the player is done deciding.</summary>
    public int ActiveHand { get; set; }

    /// <summary>
    /// Both of the dealer's cards from the moment of the deal, including the one that is face down.
    /// What the player is shown is trimmed on the way out rather than stored short: the hole card is
    /// dealt when the round is dealt, the way it is at a table, and hiding it is a rule about telling
    /// rather than a rule about dealing.
    /// </summary>
    public string DealerCardsJson { get; set; } = string.Empty;

    /// <summary>Where the round is up to. Anything other than "playing" means it is over.</summary>
    public string Status { get; set; } = BlackjackStatus.Playing;

    /// <summary>Paid on settling across every hand, stakes included, so a push shows the stake back.</summary>
    public long Payout { get; set; }

    /// <summary>How many times the player has split in this round, against the house limit.</summary>
    public int Splits { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? SettledAtUtc { get; set; }
}

public static class BlackjackStatus
{
    public const string Playing = "playing";
    public const string PlayerBlackjack = "blackjack";
    public const string PlayerBust = "bust";
    public const string DealerBust = "dealer_bust";
    public const string PlayerWin = "won";
    public const string DealerWin = "lost";
    public const string Push = "push";

    /// <summary>A hand the player has finished with but the dealer has not answered yet.</summary>
    public const string Stood = "stood";

    /// <summary>A round with more than one hand in it, which cannot be described by any single one.</summary>
    public const string Split = "split";

    public static bool IsOver(string status) => status != Playing;
}
