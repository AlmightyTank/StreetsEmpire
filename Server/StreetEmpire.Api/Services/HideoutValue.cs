using System.Linq.Expressions;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

/// <summary>
/// What a hideout is worth: every pound spent getting it to the state it is in.
///
/// The building was the one thing a player owned that counted for nothing. Everything portable was on
/// the books - cash, crew, guns, product, even the beer - while up to 13.4 million pounds of building
/// was invisible, so the single largest investment in the game made your standing worse the moment you
/// made it. A player who put a Penthouse over their head dropped down the board for doing it.
///
/// Valued at cost rather than at some fraction of it, which makes an upgrade neutral: cash becomes a
/// building of the same worth and the leaderboard does not move. Buying rooms is not a way up the
/// board, and it is no longer a way down it either.
///
/// Derived from the levels rather than stored as a running total, so it cannot drift out of step with
/// the price list, and re-tuning an upgrade cost re-values every hideout that bought it. The cost of
/// that choice is this file: the sum has to exist twice, once for memory and once as an expression the
/// database can run, exactly as net worth itself already does.
/// </summary>
public static class HideoutValue
{
    /// <summary>What every upgrade up to and including this level cost, added up.</summary>
    private static long CumulativeCost<T>(IEnumerable<T> levels, int level, Func<T, int> levelOf, Func<T, long> costOf)
        => levels.Where(x => levelOf(x) <= level).Sum(costOf);

    /// <summary>
    /// What one room cost to reach the level it is standing at.
    ///
    /// The same sum as the whole-building total, taken one room at a time, because a repair bill has
    /// to be priced against the room that was broken rather than the house it is in. Sharing the
    /// arithmetic is the point: re-tune a lab's ladder and the cost of putting one back moves with it,
    /// exactly as the leaderboard value does.
    /// </summary>
    public static long OfRoom(HideoutOptions config, string room, int level) => room switch
    {
        HideoutRooms.Storage => CumulativeCost(config.Storage, level, x => x.Level, x => x.UpgradeCost),
        HideoutRooms.Safe => CumulativeCost(config.Safe, level, x => x.Level, x => x.UpgradeCost),
        HideoutRooms.WeedLab => CumulativeCost(config.WeedLab, level, x => x.Level, x => x.UpgradeCost),
        HideoutRooms.CokeLab => CumulativeCost(config.CokeLab, level, x => x.Level, x => x.UpgradeCost),
        HideoutRooms.Workshop => CumulativeCost(config.Workshop, level, x => x.Level, x => x.UpgradeCost),
        HideoutRooms.Intelligence => CumulativeCost(config.Intelligence, level, x => x.Level, x => x.UpgradeCost),
        HideoutRooms.Lookout => CumulativeCost(config.Lookout, level, x => x.Level, x => x.UpgradeCost),
        _ => 0
    };

    public static long Of(Hideout? hideout, GameOptions options)
    {
        if (hideout is null) return 0;
        var config = options.Hideout;

        // A tier being built is a tier already paid for. Leaving it out would drop a player down the
        // board for the length of the build and put them back afterwards, which is the same penalty
        // this file exists to remove, just on a timer.
        var tier = hideout.UpgradingToTier ?? hideout.Tier;

        return CumulativeCost(config.Tiers, tier, x => x.Level, x => x.UpgradeCost)
             + CumulativeCost(config.Storage, hideout.StorageLevel, x => x.Level, x => x.UpgradeCost)
             + CumulativeCost(config.Safe, hideout.SafeLevel, x => x.Level, x => x.UpgradeCost)
             + CumulativeCost(config.WeedLab, hideout.WeedLabLevel, x => x.Level, x => x.UpgradeCost)
             + CumulativeCost(config.CokeLab, hideout.CokeLabLevel, x => x.Level, x => x.UpgradeCost)
             + CumulativeCost(config.Workshop, hideout.WorkshopLevel, x => x.Level, x => x.UpgradeCost)
             + CumulativeCost(config.Intelligence, hideout.IntelligenceLevel, x => x.Level, x => x.UpgradeCost)
             + CumulativeCost(config.Lookout, hideout.LookoutLevel, x => x.Level, x => x.UpgradeCost);
    }

    /// <summary>
    /// The same sum as something the database can rank by, so the leaderboard is still one query rather
    /// than every player pulled into memory and added up.
    ///
    /// Built as a ladder of comparisons on the level column - worth this much at level four, this much
    /// at three - which Npgsql turns into a plain CASE. The alternative, a lookup keyed on the level,
    /// has nothing to translate to.
    /// </summary>
    public static Expression Build(Expression hideout, GameOptions options)
    {
        var config = options.Hideout;

        // UpgradingToTier ?? Tier, so a build in flight counts from the moment it is paid for.
        var tier = Expression.Coalesce(
            Expression.Property(hideout, nameof(Hideout.UpgradingToTier)),
            Expression.Property(hideout, nameof(Hideout.Tier)));

        return Add(
            Ladder(tier, config.Tiers.Select(x => (x.Level, x.UpgradeCost))),
            Ladder(Expression.Property(hideout, nameof(Hideout.StorageLevel)), config.Storage.Select(x => (x.Level, x.UpgradeCost))),
            Ladder(Expression.Property(hideout, nameof(Hideout.SafeLevel)), config.Safe.Select(x => (x.Level, x.UpgradeCost))),
            Ladder(Expression.Property(hideout, nameof(Hideout.WeedLabLevel)), config.WeedLab.Select(x => (x.Level, x.UpgradeCost))),
            Ladder(Expression.Property(hideout, nameof(Hideout.CokeLabLevel)), config.CokeLab.Select(x => (x.Level, x.UpgradeCost))),
            Ladder(Expression.Property(hideout, nameof(Hideout.WorkshopLevel)), config.Workshop.Select(x => (x.Level, x.UpgradeCost))),
            Ladder(Expression.Property(hideout, nameof(Hideout.IntelligenceLevel)), config.Intelligence.Select(x => (x.Level, x.UpgradeCost))),
            Ladder(Expression.Property(hideout, nameof(Hideout.LookoutLevel)), config.Lookout.Select(x => (x.Level, x.UpgradeCost))));
    }

    private static Expression Add(params Expression[] parts)
        => parts.Aggregate((left, right) => Expression.Add(left, right));

    /// <summary>
    /// level >= n ? cumulative(n) : (level >= n-1 ? cumulative(n-1) : ... : 0), highest rung first.
    /// </summary>
    private static Expression Ladder(Expression column, IEnumerable<(int Level, long Cost)> levels)
    {
        var ordered = levels.OrderBy(x => x.Level).ToList();

        Expression result = Expression.Constant(0L);
        long running = 0;
        foreach (var (level, cost) in ordered)
        {
            running += cost;
            if (running == 0) continue;
            result = Expression.Condition(
                Expression.GreaterThanOrEqual(column, Expression.Constant(level)),
                Expression.Constant(running),
                result);
        }

        return result;
    }
}
