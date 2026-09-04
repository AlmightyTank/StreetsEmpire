using System.Linq.Expressions;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

/// <summary>
/// What counts as news.
///
/// The feed used to be every action log that was not an admin action or a store purchase, newest
/// first. With rivals acting on a timer that meant thirty rows of somebody buying condoms, and the
/// one attack that actually mattered was pushed off the page within a minute. This decides what is
/// worth reporting, and the decision runs in the database rather than over a page of rows pulled back
/// and thrown away.
/// </summary>
public static class WorldNews
{
    /// <summary>
    /// Fights, buildings, and arrivals are news whatever their size. Everything else has to move
    /// real money or real crew to earn a place.
    ///
    /// Money is judged on cash and bank together: a deposit is a large negative cash swing and an
    /// equally large positive bank swing, and moving your own money between two pockets is not news.
    /// </summary>
    public static Expression<Func<GameActionLog, bool>> IsNewsworthy(WorldNewsOptions options, DateTime sinceUtc)
        => log => log.CreatedAtUtc >= sinceUtc
                  && log.Action != "ADMIN"
                  && log.Action != "LAB"
                  // GROUND rows are written to one player in the second person. The raider's own row
                  // reports the same event publicly, so publishing these would put "took it from you"
                  // in front of everybody.
                  && log.Action != "GROUND"
                  && log.Action != "BUST"
                  && (log.Action == "ATTACK"
                      // A progressive that drops is news at any size. It is fed by everybody who
                      // played that machine, so the one person who walked off with it owes the rest of
                      // the floor an explanation, and the cash swing rule would silently swallow the
                      // cheapest pots - which are exactly the ones most players are playing for.
                      || log.Action == "JACKPOT"
                      // One crew declaring on another is the largest thing that happens in this world
                      // and the only one that involves a dozen people at once. It is written once, to
                      // the player who declared it, precisely so it can be published here.
                      || log.Action == "WAR"
                      || log.Action == "HIDEOUT"
                      || log.Action == "TERRITORY"
                      || log.Action == "START"
                      || log.CashDelta + log.BankDelta >= options.MinCashSwing
                      || log.CashDelta + log.BankDelta <= -options.MinCashSwing
                      || log.HoesDelta >= options.MinCrewChange
                      || log.HoesDelta <= -options.MinCrewChange
                      || log.ThugsDelta >= options.MinCrewChange
                      || log.ThugsDelta <= -options.MinCrewChange
                      // Pimps are named and few, so any of them leaving is worth a line.
                      || log.PimpsDelta <= -1);

    /// <summary>The kind of story a row is, so the feed can be read at a glance.</summary>
    public static string Category(string action) => action switch
    {
        "ATTACK" => "combat",
        "TERRITORY" => "ground",
        "WAR" => "crew",
        "HIDEOUT" => "build",
        "START" => "arrival",
        "CREW" => "crew",
        "JACKPOT" => "casino",
        "CASINO" => "casino",
        _ => "money"
    };
}

public sealed class WorldNewsOptions
{
    public int FeedSize { get; set; } = 30;

    /// <summary>How far back the feed and its headlines look.</summary>
    public int WindowHours { get; set; } = 48;

    /// <summary>Cash and bank movement, taken together, that makes an ordinary action newsworthy.</summary>
    public long MinCashSwing { get; set; } = 25_000;

    public int MinCrewChange { get; set; } = 5;

    /// <summary>
    /// How often everybody's standing is sampled. Finer sampling pins down more precisely who moved
    /// ahead of whom, at the cost of one row per player per sample.
    /// </summary>
    public int StandingsSampleMinutes { get; set; } = 15;

    /// <summary>How long samples are kept. Nothing reads back further than the longest absence.</summary>
    public int StandingsRetentionDays { get; set; } = 14;
}
