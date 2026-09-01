using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

/// <summary>
/// The book, the hand, and what looking again costs.
///
/// One set of numbers where there were two. The trader's wanted board and the town's contracts were
/// tuned separately and drifted exactly as far apart as two things with no reason to agree ever do -
/// different quantities, different premiums, different lifetimes, different rep scales and two refill
/// clocks that between them put six jobs in a town nobody could read in one sitting.
/// </summary>
public sealed class TraderJobOptions
{
    /// <summary>
    /// How many jobs a town keeps going at once.
    ///
    /// Deep, and deliberately far deeper than anybody sees. A book of three would make a reroll a
    /// reshuffle of the same three cards; at seventeen, asking your dealer what else is going is a real
    /// question with a real answer, and it is still a finite one - clear enough of the book and looking
    /// again stops helping, which is what stops the button being a slot machine.
    /// </summary>
    public int BookMin { get; set; } = 16;
    public int BookMax { get; set; } = 18;

    /// <summary>
    /// How often a town posts one more, once its book is thin.
    ///
    /// This is the thing that makes the book a supply rather than a tap. Fast, because there are
    /// seventeen slots behind it rather than three: a town stripped bare still takes a couple of hours
    /// to come back, and a town nobody has touched sits full.
    /// </summary>
    public int PostIntervalMinutes { get; set; } = 8;

    /// <summary>
    /// How many a player is told about at once. Three for everybody, whatever their standing.
    ///
    /// Standing used to decide this - one job at the bottom rung, one more per rung after it - and it
    /// was the neatest thing rep bought anywhere in the game. It is a flat three now because the reroll
    /// took the job over: what standing is worth on this board is being able to afford to look again,
    /// which is a thing a player does rather than a number that happens to them.
    /// </summary>
    public int HandSize { get; set; } = 3;

    /// <summary>What the dealer wants for their own shelf, and what a town's buyers want.</summary>
    public TraderJobFamilyOptions Supply { get; set; } = new()
    {
        MinQuantity = 6,
        MaxQuantity = 30,
        // Pocket money on purpose. A player without a bench can buy twenty shotguns at the counter and
        // carry them back for six to sixteen percent, which is a way in rather than a living; somebody
        // who makes their own takes several times the margin on the same job.
        MinPremiumPercent = 6,
        PremiumSpreadPercent = 10,
        // Raised with the merge. Two boards refilling on two clocks put about 2.2 jobs an hour into a
        // town; one book feeding a hand of three puts through fewer, and the rate a player can earn
        // standing at should not have quietly fallen by half because two panels became one.
        RepPerDollar = 0.011,
        MinRep = 85,
        MaxRep = 840,
    };

    public TraderJobFamilyOptions Product { get; set; } = new()
    {
        MinQuantity = 15,
        MaxQuantity = 60,
        // Bigger, because it has to clear the effort of holding stock to a deadline and stay under what
        // a good mule route makes, or jobs become the only thing worth doing.
        MinPremiumPercent = 20,
        PremiumSpreadPercent = 35,
        // Under the dealer's own rate, and that gap is the point of keeping the two families apart.
        // Bringing them stock is doing them a favour and is worth real credit; finishing a job they put
        // you onto is doing yourself a favour with their contact, and is worth being remembered for.
        RepPerDollar = 0.0056,
        MinRep = 55,
        MaxRep = 420,
    };

    /// <summary>
    /// How often a coke buyer cares about strength, what they insist on, and what they pay for it.
    /// Sometimes rather than always: a floor on every job would make stretching pointless rather than a
    /// trade.
    /// </summary>
    public double PurityConditionChance { get; set; } = 0.4;
    public int MinimumPurityFloorPercent { get; set; } = 60;
    public int PurityPremiumPercent { get; set; } = 25;

    /// <summary>
    /// What a town's buyers ask for. Weapons and moonshine are the standing minority, and between weed
    /// and coke a town leans towards whatever it values most without ever ruling the other out: asking
    /// only for the dearer one made every town a one-note board.
    /// </summary>
    public int WeaponsPercent { get; set; } = 20;
    public int MoonshinePercent { get; set; } = 10;
    public int FavouredGoodPercent { get; set; } = 70;

    /// <summary>
    /// How many lines one town's counter can be out of at once.
    ///
    /// A gap shuts that line until somebody fills it, which is the whole reason it is worth doing - and
    /// the whole reason it needs a ceiling. Six of the nine lines can go dry, so a book that rolled gaps
    /// freely would regularly leave a town with most of its shelf dark, which stops being a job to do
    /// and starts being a shop that is closed.
    /// </summary>
    public int MaxShelfGapsPerCity { get; set; } = 2;

    /// <summary>How long a job stands. Long enough to go and make the goods, short enough to matter.</summary>
    public int MinLifetimeHours { get; set; } = 4;
    public int MaxLifetimeHours { get; set; } = 14;

    public TraderJobRerollOptions Reroll { get; set; } = new();

    public TraderJobFamilyOptions Family(TraderJobKind kind)
        => kind == TraderJobKind.Supply ? Supply : Product;

    /// <summary>
    /// What belongs in each slot of the hand, in order. Supply, then product, then whatever comes up.
    ///
    /// Reserved rather than rolled because the hand is three cards wide. Left to chance, better than
    /// one evening in seven deals three product jobs to a player whose whole question that evening is
    /// what to do with a workshop.
    /// </summary>
    public TraderJobKind? SlotKind(int slot) => slot switch
    {
        0 => TraderJobKind.Supply,
        1 => TraderJobKind.Product,
        _ => null,
    };
}

/// <summary>One of the two kinds of job, and the numbers that differ between them.</summary>
public sealed class TraderJobFamilyOptions
{
    public int MinQuantity { get; set; } = 6;
    public int MaxQuantity { get; set; } = 30;

    /// <summary>What the job pays over the going rate, as a percentage.</summary>
    public int MinPremiumPercent { get; set; } = 6;
    public int PremiumSpreadPercent { get; set; } = 10;

    /// <summary>Standing for finishing one, per dollar it pays.</summary>
    public double RepPerDollar { get; set; } = 0.008;

    /// <summary>The least a job can be worth in standing, so a small one is still worth bending for.</summary>
    public int MinRep { get; set; } = 60;

    /// <summary>
    /// The most. Rep scaling with what a job pays is right in the small - a harder job is worth more -
    /// and wrong in the tail, where the dearest good at the biggest quantity would be most of a rung in
    /// one delivery.
    /// </summary>
    public int MaxRep { get; set; } = 600;
}

/// <summary>
/// Asking the dealer what else is going.
///
/// The first one in a cycle is free, and after that it costs money and standing together. Money alone
/// would make it a rich player's button; standing alone would make it a tax on the one number the whole
/// shop is built around. Both means a reroll is worth something to everybody and cheap to nobody.
/// </summary>
public sealed class TraderJobRerollOptions
{
    /// <summary>
    /// How often the free one comes back. The same clock family as everything else here - the
    /// investment counter, the bank's grace window, an offering to the gods.
    /// </summary>
    public int FreeEveryHours { get; set; } = 6;

    /// <summary>
    /// What each reroll in a cycle costs after the free one, in order, and the last entry over again
    /// for anybody who keeps going.
    ///
    /// Cheap once, silly by the fourth, which is the shape a "look again" button should have. Charged
    /// per slot rather than per press: rerolling all three at once is three draws and pays for three,
    /// or taking the whole hand would always be the only sensible way to press it.
    /// </summary>
    public List<TraderJobRerollStepOptions> Steps { get; set; } = [];

    public void ApplyDefaultsWhereEmpty()
    {
        if (Steps.Count > 0) return;
        Steps =
        [
            new TraderJobRerollStepOptions { Cash = 0, Rep = 0 },
            new TraderJobRerollStepOptions { Cash = 5_000, Rep = 25 },
            new TraderJobRerollStepOptions { Cash = 15_000, Rep = 75 },
            new TraderJobRerollStepOptions { Cash = 45_000, Rep = 225 },
        ];
    }

    /// <summary>What the nth reroll of a cycle costs, counting from zero. Past the end, the last step.</summary>
    public TraderJobRerollStepOptions Step(int used)
    {
        if (Steps.Count == 0) return new TraderJobRerollStepOptions();
        return Steps[Math.Clamp(used, 0, Steps.Count - 1)];
    }
}

public sealed class TraderJobRerollStepOptions
{
    public long Cash { get; set; }
    public int Rep { get; set; }

    public bool IsFree => Cash <= 0 && Rep <= 0;
}
