namespace StreetEmpire.Api.Models;

/// <summary>
/// Which half of the game a job speaks to.
///
/// Not two boards any more, but still two different propositions, and a board of three that could roll
/// three of the same kind would regularly have nothing to say to half of what a player owns. So the
/// hand reserves a slot for each and lets the third go where it likes.
///
/// Kept underneath rather than shown. A player sees who is asking and why; this is only how the job was
/// priced and what it is worth in standing.
/// </summary>
public enum TraderJobKind
{
    /// <summary>The dealer wants stock for their own shelf: guns, moonshine, cut, medicine, poison.</summary>
    Supply = 0,

    /// <summary>Somebody in town wants product by a deadline, and the dealer passes it on.</summary>
    Product = 1,
}

/// <summary>
/// Why the dealer is asking.
///
/// Every job on the board is theirs now. It used to be half theirs and half the town's - a row headed
/// "Crenshaw Corner" sitting under a panel headed "Sunny Delgado", with nothing saying what the two had
/// to do with each other - and the answer was always the same one: a stranger does not hear about work
/// in this town except through the person who knows everybody. So the place is still named, as who your
/// dealer is doing it for, and the row says which of the four this is.
/// </summary>
public enum TraderJobReason
{
    /// <summary>
    /// Their own shelf is empty. The one reason that closes a line at the counter until somebody fills
    /// it, which is what makes it the only job on the board with a consequence for not doing it.
    /// </summary>
    ShelfGap = 0,

    /// <summary>Somebody they owe, somewhere on the town's map, needs it.</summary>
    Favour = 1,

    /// <summary>They have promised it to somebody by a date and come up short.</summary>
    Deal = 2,

    /// <summary>Another town's counter is dry and they said they would cover it.</summary>
    CoveringTrader = 3,
}

/// <summary>
/// One job going in a town: what somebody wants, how much, by when, and what finishing it is worth.
///
/// This is the trader's board and the town's contracts as one thing, which is what they always were in
/// the fiction and never were in the code. Two tables, two services, two clocks, two premiums, two rep
/// scales and two panels stacked one above the other, both headed with the same dealer's name and both
/// opening with a paragraph explaining the same arrangement. A player looking at six open jobs under two
/// headings was being asked to learn a distinction the game never had a use for.
///
/// What survives the merge is the only difference that was ever load-bearing: who is asking, and for
/// what. <see cref="Buyer"/> null means the dealer wants it themselves.
/// </summary>
public sealed class TraderJob
{
    public long Id { get; set; }

    /// <summary>The town it is going in. A job belongs to a place, never to a player.</summary>
    public string City { get; set; } = string.Empty;

    public TraderJobKind Kind { get; set; }

    public TraderJobReason Reason { get; set; }

    /// <summary>
    /// Who the dealer is doing it for - a place on the town's map, or another town's trader by name -
    /// and null when it is for their own shelf.
    ///
    /// Never the counterparty. The player is dealing with their dealer either way; this is the sentence
    /// that says why the dealer cares, which is the thing the old board was missing entirely.
    /// </summary>
    public string? OnBehalfOf { get; set; }

    public string Good { get; set; } = string.Empty;
    public int Quantity { get; set; }

    /// <summary>What the finished job pays a unit, premium included.</summary>
    public long PricePerUnit { get; set; }

    /// <summary>
    /// What the same good goes for ordinarily - the shelf price for a supply job, the town's own price
    /// for a product one - frozen when the job was posted.
    ///
    /// Frozen because a job has to keep explaining itself after the world moves. A shop price that
    /// changed underneath an open job would turn a premium into a loss, and the row would go on quoting
    /// a comparison that had stopped being true.
    /// </summary>
    public long ReferencePricePerUnit { get; set; }

    /// <summary>
    /// A purity floor, for coke only, and only sometimes. Null when the buyer does not care. It is what
    /// makes a stretched pile a decision rather than free money: the cheap buyers take anything.
    /// </summary>
    public int? MinimumPurityPercent { get; set; }

    /// <summary>
    /// Standing for finishing it, worked out when the job was posted so the board can say the number
    /// rather than make a player derive it, and so the row cannot disagree with the delivery.
    /// </summary>
    public int Rep { get; set; }

    public DateTime PostedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>
    /// How much has gone in so far. Instalments because jobs run to sixty units and a first storage
    /// room holds ten of coke, so demanding the lot in one movement would make most of the book
    /// unfillable by exactly the players it exists to give something to aim at.
    /// </summary>
    public int DeliveredQuantity { get; set; }

    /// <summary>
    /// Who is filling it, set by the first delivery. A job two people are part-filling is one where
    /// somebody's goods are going to be wasted, and it would be whoever worked hardest and arrived
    /// last. The claim is not forever: the deadline frees a job nobody finishes.
    /// </summary>
    public Guid? ClaimedById { get; set; }
    public Player? ClaimedBy { get; set; }

    public Guid? FilledById { get; set; }
    public Player? FilledBy { get; set; }
    public DateTime? FilledAtUtc { get; set; }

    /// <summary>Who is currently holding this job in their hand. See <see cref="TraderJobLead"/>.</summary>
    public List<TraderJobLead> Leads { get; set; } = [];

    public bool IsOpen(DateTime nowUtc) => FilledAtUtc is null && ExpiresAtUtc > nowUtc;

    public int Remaining => Math.Max(0, Quantity - DeliveredQuantity);

    /// <summary>Whether this player may put goods into it - unclaimed, or already theirs.</summary>
    public bool CanBeWorkedBy(Guid playerId) => ClaimedById is null || ClaimedById == playerId;

    /// <summary>What the whole job pays, and what the same goods fetch at the ordinary rate.</summary>
    public long Payout => Quantity * PricePerUnit;
    public long FlatValue => Quantity * ReferencePricePerUnit;

    /// <summary>
    /// The part of the payout that is not simply the going rate, handed over when the last unit goes in.
    ///
    /// One rule for both kinds now. The dealer's own orders used to pay their premium per instalment,
    /// which meant a part-filled order was already in profit and the thing worth finishing for could be
    /// farmed a unit at a time. Deliveries pay the ordinary rate as they happen and the premium is never
    /// split, so stopping half way leaves a player exactly where selling those goods flat would have -
    /// the only thing an abandoned job costs is the chance at the premium.
    /// </summary>
    public long CompletionBonus => Payout - FlatValue;
}

/// <summary>
/// One of the three jobs a player is currently being told about.
///
/// The book is the town's - sixteen to eighteen jobs live at once, the same book for everybody, and a
/// rival finishing one takes it off the board for the rest of the world. What a player gets is a hand
/// of three dealt out of it, which is the shape the fiction always described: nobody sees a noticeboard,
/// they hear about what their dealer chooses to mention.
///
/// The hand is why this row exists at all. A board that showed the whole book would be seventeen rows of
/// homework; a board that showed three of them at random with no memory would deal a different three
/// every time the page refreshed, and no job would ever be worth going away and making something for.
/// </summary>
public sealed class TraderJobLead
{
    public long Id { get; set; }

    public Guid PlayerId { get; set; }
    public Player Player { get; set; } = null!;

    public long JobId { get; set; }
    public TraderJob Job { get; set; } = null!;

    /// <summary>
    /// The town the hand was dealt in, copied off the job so a player's hands in different towns can
    /// sit side by side without a join to tell them apart. Travelling does not throw your hand away.
    /// </summary>
    public string City { get; set; } = string.Empty;

    /// <summary>
    /// Which of the three this is, and what kind of job belongs in it: the first is always a supply
    /// job, the second always a product one, the third whatever comes up. Held rather than derived so
    /// that rerolling one slot cannot quietly change what the other two are for.
    /// </summary>
    public int Slot { get; set; }

    public DateTime DealtAtUtc { get; set; } = DateTime.UtcNow;
}
