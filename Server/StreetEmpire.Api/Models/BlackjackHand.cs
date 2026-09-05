namespace StreetEmpire.Api.Models;

/// <summary>
/// A hand of blackjack, from the deal to the settle.
///
/// It lives in the database rather than in the request because blackjack is the first game on this
/// floor that takes more than one turn of conversation: a player draws, looks, draws again. Everything
/// that decides the outcome - the shoe and the card the dealer has face down - has to sit somewhere
/// the player cannot read, and the only such place is here.
/// </summary>
public sealed class BlackjackHand
{
    public long Id { get; set; }
    public Guid PlayerId { get; set; }
    public Player Player { get; set; } = null!;
    public string TableKey { get; set; } = string.Empty;

    /// <summary>What is riding on it. Doubling puts the same amount up again and this grows.</summary>
    public long Bet { get; set; }

    /// <summary>
    /// The shoe, in the order it will come out, minus what has already been dealt.
    ///
    /// Never leaves the server. It is the answer to every question the player is being asked, so
    /// putting it anywhere near a response would be handing them the paper.
    /// </summary>
    public string DeckJson { get; set; } = string.Empty;

    public string PlayerCardsJson { get; set; } = string.Empty;

    /// <summary>
    /// Both of the dealer's cards from the moment of the deal, including the one that is face down.
    /// What the player is shown is trimmed on the way out rather than stored short: the hole card is
    /// dealt when the hand is dealt, the way it is at a table, and hiding it is a rule about telling
    /// rather than a rule about dealing.
    /// </summary>
    public string DealerCardsJson { get; set; } = string.Empty;

    /// <summary>Where the hand is up to. Anything other than "playing" means it is over.</summary>
    public string Status { get; set; } = BlackjackStatus.Playing;

    /// <summary>Paid on settling, stake included, so a push shows the stake coming back.</summary>
    public long Payout { get; set; }

    /// <summary>Whether the player has already put a second stake up, so they cannot do it twice.</summary>
    public bool Doubled { get; set; }

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

    public static bool IsOver(string status) => status != Playing;
}
