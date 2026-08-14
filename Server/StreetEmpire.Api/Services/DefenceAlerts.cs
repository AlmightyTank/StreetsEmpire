using System.Linq.Expressions;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

/// <summary>
/// Turns combat history into what the defender should be told.
///
/// A CombatLog records the outcome from the attacker's point of view, so "Victory" means the person
/// reading the alert was robbed. Describing these without flipping perspective would tell players they
/// won fights they lost, so the flip lives here in one tested place rather than in the UI.
/// </summary>
public static class DefenceAlerts
{
    public static DefenceAlertResponse Describe(CombatLog log, DateTime? seenAtUtc)
    {
        var unread = seenAtUtc is null || log.CreatedAtUtc > seenAtUtc;
        var attacker = log.Attacker?.Name ?? "Someone";

        var (headline, held) = log.Outcome switch
        {
            // The attacker won, so the defender is the one who lost something.
            "Victory" => ($"{attacker} broke through your defence.", false),
            "Defeat" => ($"You held {attacker} off.", true),
            "Standstill" => ($"You fought {attacker} to a standstill.", true),
            "Canceled" => ($"{attacker} called off an attack on you.", true),
            _ => ($"{attacker} attacked you.", false)
        };

        var losses = new List<string>();
        if (log.CashStolen > 0) losses.Add($"${log.CashStolen:N0}");
        if (log.WeedStolen > 0) losses.Add($"{log.WeedStolen:N0} weed");
        if (log.CokeStolen > 0) losses.Add($"{log.CokeStolen:N0} coke");
        if (log.DefenderThugsLost > 0) losses.Add($"{log.DefenderThugsLost:N0} thug(s)");
        if (log.DefenderWeaponsLost > 0) losses.Add($"{log.DefenderWeaponsLost:N0} weapon(s)");
        if (log.DefenderPimpsLost > 0) losses.Add($"{log.DefenderPimpsLost:N0} pimp(s)");

        var detail = losses.Count == 0
            ? held ? "Nothing was taken." : "No losses recorded."
            : $"Lost {string.Join(", ", losses)}.";

        return new DefenceAlertResponse(
            log.Id,
            attacker,
            log.Outcome,
            held,
            headline,
            detail,
            log.CashStolen,
            log.WeedStolen,
            log.CokeStolen,
            log.DefenderThugsLost,
            log.DefenderPimpsLost,
            unread,
            log.CreatedAtUtc);
    }

    /// <summary>How many of these the player has not seen yet.</summary>
    public static int UnreadCount(IEnumerable<AlertResponse> alerts)
        => alerts.Count(x => x.IsUnread);

    /// <summary>A fight as a general alert, so it can sit in one list with the non-combat notices.</summary>
    public static AlertResponse ToAlert(DefenceAlertResponse defence)
        => new(
            $"combat-{defence.Id}",
            "attack",
            defence.Headline,
            defence.Detail,
            defence.HeldTheHouse ? "good" : "bad",
            defence.IsUnread,
            defence.CreatedAtUtc);

    /// <summary>
    /// A logged event the player did not cause. Only the kinds that are genuinely notifications belong
    /// here: a build being started is an action, a build finishing while they were out is not.
    /// </summary>
    public static AlertResponse? ToAlert(long logId, string action, string summary, DateTime createdAtUtc, DateTime? seenAtUtc)
    {
        var unread = seenAtUtc is null || createdAtUtc > seenAtUtc;
        return action switch
        {
            "LAB" => new AlertResponse($"log-{logId}", "labs", "Your labs kept working", summary, "good", unread, createdAtUtc),
            "HIDEOUT" when summary.EndsWith(" is finished.", StringComparison.Ordinal)
                => new AlertResponse($"log-{logId}", "hideout", "Building finished", summary, "good", unread, createdAtUtc),
            "TERRITORY" when summary.EndsWith(" from you.", StringComparison.Ordinal)
                => new AlertResponse($"log-{logId}", "ground", "You lost ground", summary, "bad", unread, createdAtUtc),
            _ => null
        };
    }

    /// <summary>
    /// The one definition of which log rows are notifications rather than actions.
    ///
    /// Shared as an expression because three queries depend on it: the alert list, the unread count,
    /// and the activity list that excludes exactly these rows. Written out separately in each, a new
    /// kind lands in both places or neither.
    /// </summary>
    public static Expression<Func<GameActionLog, bool>> IsNotificationRow { get; } =
        log => log.Action == "LAB"
               || (log.Action == "HIDEOUT" && log.Summary.EndsWith(" is finished."))
               // Ground being taken off you. Claiming ground yourself is an action and stays in activity.
               || (log.Action == "TERRITORY" && log.Summary.EndsWith(" from you."));

    /// <summary>The same rule negated, for the activity list. Derived so the two cannot disagree.</summary>
    public static Expression<Func<GameActionLog, bool>> IsActionRow { get; } =
        Expression.Lambda<Func<GameActionLog, bool>>(
            Expression.Not(IsNotificationRow.Body),
            IsNotificationRow.Parameters);

    /// <summary>The in-memory twin, kept in step by a test that runs both over the same rows.</summary>
    public static bool IsNotification(string action, string summary)
        => IsNotificationRow.Compile()(new GameActionLog { Action = action, Summary = summary });
}
