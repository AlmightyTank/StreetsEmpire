using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

/// <summary>
/// What one player is allowed to know about another's house.
///
/// The rule in one place, because it is asked from two directions - the endpoint deciding what to put
/// in a response, and the page deciding what to draw - and two copies of a disclosure rule is one that
/// eventually tells somebody something the other half thought was hidden.
/// </summary>
public sealed class IntelService(GameDbContext db, IOptionsSnapshot<GameOptions> options)
{
    private readonly GameOptions _options = options.Value;

    /// <summary>
    /// What a viewer currently knows about a subject: the level their last scout was run at, or zero.
    ///
    /// Zero for looking at a stranger, for intelligence that has gone stale, and for a player with no
    /// intelligence centre - the three are the same answer from the reader's side, which is what makes
    /// this one number rather than three flags.
    /// </summary>
    public async Task<int> KnownLevelAsync(Player viewer, Guid subjectId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        // Your own house needs no scouting. Everything is known, whatever the building says.
        if (viewer.Id == subjectId) return IntelLevels.Everything;

        var intel = await db.HideoutIntel.AsNoTracking()
            .SingleOrDefaultAsync(x => x.ViewerId == viewer.Id && x.SubjectId == subjectId, cancellationToken);

        if (intel is null) return 0;
        return IsFresh(intel, nowUtc, _options.Hideout.Intel) ? intel.Level : 0;
    }

    /// <summary>
    /// When they last looked, fresh or not. Kept separate from the level so the page can say "you looked
    /// on Tuesday and it has gone cold" rather than "you have never looked", which are different
    /// problems with different answers.
    /// </summary>
    public Task<DateTime?> LastLookedAtUtcAsync(Guid viewerId, Guid subjectId, CancellationToken cancellationToken)
        => db.HideoutIntel.AsNoTracking()
            .Where(x => x.ViewerId == viewerId && x.SubjectId == subjectId)
            .Select(x => (DateTime?)x.GatheredAtUtc)
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>When this intelligence stops being intelligence.</summary>
    public static bool IsFresh(HideoutIntel intel, DateTime nowUtc, IntelOptions options)
        => intel.GatheredAtUtc.AddHours(Math.Max(1, options.FreshHours)) > nowUtc;

    /// <summary>
    /// Runs a scout, or says why not.
    ///
    /// Turns are spent whether or not anything is learned that the player did not already have, because
    /// the cost is sending people to look rather than a payment for news.
    /// </summary>
    public async Task<string?> ScoutAsync(Player viewer, Player subject, DateTime nowUtc, CancellationToken cancellationToken)
    {
        if (viewer.Id == subject.Id) return "You know what is in your own house.";

        var level = _options.Hideout.LevelOfIntelligence(viewer.Hideout);
        if (level < 1)
            return "Build an intelligence centre before sending anybody to look at somebody else's house.";

        var cost = Math.Max(0, _options.Hideout.Intel.ScoutTurnCost);
        if (viewer.Turns < cost)
            return $"Scouting a house costs {cost} turn{(cost == 1 ? string.Empty : "s")}.";

        viewer.Turns -= cost;

        var existing = await db.HideoutIntel
            .SingleOrDefaultAsync(x => x.ViewerId == viewer.Id && x.SubjectId == subject.Id, cancellationToken);

        if (existing is null)
        {
            db.HideoutIntel.Add(new HideoutIntel
            {
                ViewerId = viewer.Id,
                SubjectId = subject.Id,
                GatheredAtUtc = nowUtc,
                Level = level,
            });
        }
        else
        {
            existing.GatheredAtUtc = nowUtc;
            existing.Level = level;
        }

        return null;
    }
}

/// <summary>
/// The ladder, named. Each rung is what an intelligence centre of that level brings back, and every
/// rung includes the ones below it.
///
/// Read as a story rather than a table: how hard they hit, then what they hit with, then what is worth
/// taking off them, then where they are soft. The last one is last on purpose - morale is what a poach
/// is aimed at, so knowing it is the sharpest thing on the card.
/// </summary>
public static class IntelLevels
{
    /// <summary>Attack, defence, the risk band, how many are armed, and the coverage.</summary>
    public const int FightingWeight = 1;

    /// <summary>The gun rack itself, firepower, protection, and what they have been doing for a day.</summary>
    public const int Armoury = 2;

    /// <summary>Rides in the garage, medicine on the shelf, product in the house.</summary>
    public const int Stock = 3;

    /// <summary>Hoe and thug morale.</summary>
    public const int Morale = 4;

    /// <summary>What a player always knows about their own house.</summary>
    public const int Everything = int.MaxValue;
}

public sealed class IntelOptions
{
    /// <summary>
    /// How long a scout is worth anything. Long enough that scouting before a raid is a decision rather
    /// than a chore, short enough that a card cannot be read off a week-old visit.
    /// </summary>
    public int FreshHours { get; set; } = 6;

    /// <summary>Turns to send somebody to look.</summary>
    public int ScoutTurnCost { get; set; } = 2;
}
