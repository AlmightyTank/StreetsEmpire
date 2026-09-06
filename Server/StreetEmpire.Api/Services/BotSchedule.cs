using StreetEmpire.Api.Models;
using System.Security.Cryptography;
using System.Text;

namespace StreetEmpire.Api.Services;

/// <summary>
/// When a rival is at the screen.
///
/// The old model gave every rival one action every twenty-odd minutes, evenly, forever. Nothing about
/// that is what a player does. A player is away for hours while turns pile up, then sits down and
/// spends the lot in one go, then leaves again. They also do it at their own hours, so a world of them
/// is busy in the evening and quiet at four in the morning.
///
/// So a rival now has sessions rather than a cooldown, and each rival keeps its own hours. Both come
/// from the same seed the personality does, which means a rival's habits are as fixed as its
/// character: the Banker who plays at eight is always the Banker who plays at eight.
/// </summary>
internal sealed record BotSchedule(
    int SessionsPerDay,
    int PeakHourUtc,
    int WindowHours,
    bool NeverSleeps)
{
    /// <summary>
    /// The band the brain's old cooldown is read against. A Hard Charger averaged around 24 minutes
    /// and a Banker around 44, so those are the ends: eager personalities play more often.
    /// </summary>
    private const double EagerCooldownMinutes = 22;
    private const double PatientCooldownMinutes = 46;

    internal static BotSchedule For(Player bot, BotBrain brain, BotAutomationOptions options)
    {
        // Salted differently from the brain's hash so that habits and character are independent: a
        // rival's hours should not be readable off its personality.
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"schedule:{bot.AccountId:N}:{bot.Id:N}"));

        var minSessions = Math.Max(1, options.MinSessionsPerDay);
        var maxSessions = Math.Max(minSessions, options.MaxSessionsPerDay);

        // How often a rival plays comes from its character, not from a second dial that could disagree
        // with the first. The cooldown these are read from is what used to pace the loop directly.
        var averageCooldown = (brain.MinCooldownMinutes + brain.MaxCooldownMinutes) / 2.0;
        var patience = Math.Clamp(
            (averageCooldown - EagerCooldownMinutes) / (PatientCooldownMinutes - EagerCooldownMinutes),
            0,
            1);
        var fromCharacter = maxSessions - patience * (maxSessions - minSessions);
        // A point of jitter either way, so two rivals of one personality are not identical people.
        var sessions = (int)Math.Clamp(Math.Round(fromCharacter) + (bytes[0] % 3) - 1, minSessions, maxSessions);

        // A share of rivals keep no hours at all. Without them the board is dead for anyone who plays
        // at an odd hour, and the point of rivals is that the world is moving whether you are there or
        // not.
        var neverSleeps = bytes[3] / 255.0 < Math.Clamp(options.NeverSleepsShare, 0, 1);

        // Anything the account was given outright wins over the draw, field by field: an admin who
        // pinned an hour should not also have to pin how often, and one who set a rival to never sleep
        // should keep the sessions its character earned.
        // The band above shapes the draw; it does not overrule somebody who picked a number. An admin
        // who asks for nine sittings against a house maximum of six wants the rival they asked for, and
        // silently handing back six would look like the field had not saved. Still bounded, because the
        // column is an int and the endpoint is not the only thing that can write one.
        var account = bot.Account;
        return new BotSchedule(
            account?.BotSessionsPerDay is { } chosen
                ? Math.Clamp(chosen, 1, 48)
                : Math.Clamp(sessions, minSessions, maxSessions),
            ((account?.BotPeakHourUtc ?? bytes[1] % 24) % 24 + 24) % 24,
            Math.Clamp(options.ActiveWindowHours, 1, 24),
            account?.BotNeverSleeps ?? neverSleeps);
    }

    /// <summary>Whether this rival keeps hours at all, and whether the given moment is inside them.</summary>
    internal bool IsAwake(DateTime utc)
    {
        if (NeverSleeps || WindowHours >= 24) return true;
        var distance = HoursFromPeak(utc.Hour);
        return distance * 2 <= WindowHours;
    }

    /// <summary>
    /// When the next session may start. The gap is the day divided by how many sessions this rival
    /// plays, jittered hard so rivals do not fall into lockstep, and then pushed into this rival's
    /// hours if it keeps any. Pushing rather than waiting matters: a rival whose gap lands at 4am
    /// should play when it wakes, not skip the session entirely.
    /// </summary>
    internal DateTime NextSessionStart(DateTime nowUtc, IGameRandom random)
    {
        var averageGapMinutes = 24 * 60 / (double)Math.Max(1, SessionsPerDay);
        var jitter = 0.55 + random.NextDouble() * 0.9;
        var candidate = nowUtc.AddMinutes(averageGapMinutes * jitter);
        if (IsAwake(candidate)) return candidate;

        // Land somewhere inside the window rather than exactly on its edge, or every sleeper in the
        // world would wake at the same minute.
        var start = candidate.Date.AddHours(PeakHourUtc - WindowHours / 2.0);
        while (start < candidate) start = start.AddDays(1);
        return start.AddMinutes(random.NextInclusive(0, WindowHours * 60));
    }

    /// <summary>Shortest way round the clock face, so a window straddling midnight still works.</summary>
    private int HoursFromPeak(int hour)
    {
        var raw = Math.Abs(hour - PeakHourUtc);
        return Math.Min(raw, 24 - raw);
    }

    /// <summary>A readable summary of this rival's habits, for the admin's AI tab.</summary>
    internal string Describe()
    {
        if (NeverSleeps || WindowHours >= 24)
            return $"{SessionsPerDay}x a day, any hour";
        var from = ((PeakHourUtc - WindowHours / 2) % 24 + 24) % 24;
        var to = ((PeakHourUtc + WindowHours / 2) % 24 + 24) % 24;
        return $"{SessionsPerDay}x a day, {from:00}:00-{to:00}:00 UTC";
    }
}
