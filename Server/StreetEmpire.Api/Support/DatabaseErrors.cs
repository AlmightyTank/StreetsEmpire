using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace StreetEmpire.Api.Support;

/// <summary>
/// Turning a lost race into an answer rather than a crash.
///
/// Every place that takes a name checks whether it is free and then saves, and those are two moments
/// with a gap between them. Two people registering the same username in that gap both pass the check,
/// and the second save hits the unique index - which arrives as a DbUpdateException, which nothing
/// caught, which meant a 500 and (in development) a stack trace sent to whoever asked.
///
/// The check stays, because it is what produces a decent message almost every time. This is for the
/// almost never.
/// </summary>
internal static class DatabaseErrors
{
    /// <summary>Postgres' code for a unique index refusing a row.</summary>
    private const string UniqueViolation = "23505";

    /// <summary>
    /// The message to answer with, or null if this was not a lost race and the caller should let the
    /// exception carry on being an exception. Named by constraint, because "already taken" is no use
    /// to somebody who cannot tell which of the three things they typed was the problem.
    /// </summary>
    /// <param name="oneName">
    /// True when the caller asked for a name once and let the player name follow it, which is what the
    /// sign-up forms do. Both name constraints then answer with the one message, because naming the
    /// column that lost would name a box the player never filled in.
    /// </param>
    internal static string? DescribeUniqueViolation(Exception exception, bool oneName = false)
        => exception is DbUpdateException { InnerException: PostgresException { SqlState: UniqueViolation } inner }
            ? DescribeConstraint(inner.ConstraintName, oneName)
            : null;

    /// <summary>
    /// Which name was taken, from the index that refused the row.
    ///
    /// Split out from the exception it usually arrives in so that the interesting half - the mapping -
    /// can be tested without building an Npgsql exception, which cannot be constructed with a
    /// constraint name on it from outside the library.
    ///
    /// Matched loosely on purpose. A renamed index would otherwise fall back to a 500 silently, and the
    /// fallback still says something true.
    /// </summary>
    internal static string DescribeConstraint(string? constraintName, bool oneName = false)
    {
        var constraint = constraintName ?? string.Empty;
        if (constraint.Contains("Username", StringComparison.OrdinalIgnoreCase))
            return oneName ? AccountSetup.NameTaken : "Username is already taken.";
        if (constraint.Contains("Email", StringComparison.OrdinalIgnoreCase))
            return "That email is already on an account.";
        if (constraint.Contains("DiscordUserId", StringComparison.OrdinalIgnoreCase))
            return "That Discord account is already on an empire. Sign in with it instead.";
        if (constraint.Contains("Players_Name", StringComparison.OrdinalIgnoreCase))
            return oneName ? AccountSetup.NameTaken : "Player name is already taken.";
        if (constraint.Contains("Alliances_Name", StringComparison.OrdinalIgnoreCase))
            return "Crew name is already taken.";

        return "Somebody just took one of those names. Try again.";
    }
}
