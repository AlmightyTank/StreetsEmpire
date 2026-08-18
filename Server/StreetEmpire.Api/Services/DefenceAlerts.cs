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

        // Each method needs its own sentence. "Broke through your defence" is true of a raid and absurd
        // of a drive-by, which never went inside, and a defender told only that they won or lost has no
        // idea whether to buy medicine, park the cars somewhere else, or pay the house better.
        var (headline, held) = AttackMethods.Normalize(log.Method) switch
        {
            AttackMethods.DriveBy => log.Outcome == "Victory"
                ? ($"{attacker} shot up your street.", false)
                : ($"{attacker} shot up your street and hit nobody.", true),
            AttackMethods.Jack => log.Outcome == "Victory"
                ? ($"{attacker} drove off with your rides.", false)
                : ($"Your crew ran {attacker} out of your garage.", true),
            AttackMethods.Infest => log.Outcome == "Victory"
                ? ($"{attacker} put something through your house.", false)
                : ($"{attacker} tried to infest your house. Your medicine held.", true),
            AttackMethods.Poach => log.Outcome == "Victory"
                ? ($"{attacker} bought your hoes away.", false)
                : ($"{attacker} came for your hoes and nobody went with them.", true),
            _ => log.Outcome switch
            {
                // The attacker won, so the defender is the one who lost something.
                "Victory" => ($"{attacker} broke through your defence.", false),
                "Defeat" => ($"You held {attacker} off.", true),
                "Standstill" => ($"You fought {attacker} to a standstill.", true),
                "Canceled" => ($"{attacker} called off an attack on you.", true),
                _ => ($"{attacker} attacked you.", false)
            }
        };

        var losses = new List<string>();
        if (log.CashStolen > 0) losses.Add($"${log.CashStolen:N0}");
        if (log.WeedStolen > 0) losses.Add($"{log.WeedStolen:N0} weed");
        if (log.CokeStolen > 0) losses.Add($"{log.CokeStolen:N0} coke");
        if (log.DefenderThugsLost > 0) losses.Add($"{log.DefenderThugsLost:N0} thug(s)");
        // Hoes were missing here even before the strikes existed, which understated every raid that took
        // any. Two of the four strikes take nothing else, so it can no longer be left out.
        if (log.DefenderHoesLost > 0) losses.Add($"{log.DefenderHoesLost:N0} hoe(s)");
        if (log.RidesTaken > 0) losses.Add($"{log.RidesTaken:N0} ride(s)");
        if (log.DefenderWeaponsLost > 0) losses.Add($"{log.DefenderWeaponsLost:N0} weapon(s)");
        if (log.DefenderPimpsLost > 0) losses.Add($"{log.DefenderPimpsLost:N0} pimp(s)");

        var detail = losses.Count == 0
            ? held ? "Nothing was taken." : "No losses recorded."
            : $"Lost {string.Join(", ", losses)}.";

        return new DefenceAlertResponse(
            log.Id,
            attacker,
            AttackMethods.Normalize(log.Method),
            AttackMethods.Label(log.Method),
            log.Outcome,
            held,
            headline,
            detail,
            log.CashStolen,
            log.WeedStolen,
            log.CokeStolen,
            log.DefenderThugsLost,
            log.DefenderHoesLost,
            log.RidesTaken,
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
            "BUST" => new AlertResponse($"log-{logId}", "bust", "Raided", summary, "bad", unread, createdAtUtc),
            // A run lands whether or not anyone is watching, so how it went has to come find them.
            "MULE" when summary.Contains("never came back", StringComparison.Ordinal)
                => new AlertResponse($"log-{logId}", "mule", "Your pimp ran", summary, "bad", unread, createdAtUtc),
            "MULE" when summary.Contains("was stopped", StringComparison.Ordinal)
                => new AlertResponse($"log-{logId}", "mule", "Your mule was stopped", summary, "bad", unread, createdAtUtc),
            "MULE" => new AlertResponse($"log-{logId}", "mule", "Your mule is back", summary, "good", unread, createdAtUtc),
            "GROUND" when summary.Contains("held", StringComparison.OrdinalIgnoreCase)
                => new AlertResponse($"log-{logId}", "ground", "Your ground held", summary, "good", unread, createdAtUtc),
            "GROUND"
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
               // GROUND is ground news happening to you; TERRITORY is ground you acted on yourself and
               // belongs in activity. A separate action rather than matching how the sentence ends,
               // which broke the moment a second kind of ground notice existed.
               || log.Action == "GROUND"
               || log.Action == "BUST"
               // A run settles on the clock rather than on a request, so it is news, not activity.
               || log.Action == "MULE";

    /// <summary>The same rule negated, for the activity list. Derived so the two cannot disagree.</summary>
    public static Expression<Func<GameActionLog, bool>> IsActionRow { get; } =
        Expression.Lambda<Func<GameActionLog, bool>>(
            Expression.Not(IsNotificationRow.Body),
            IsNotificationRow.Parameters);

    /// <summary>The in-memory twin, kept in step by a test that runs both over the same rows.</summary>
    public static bool IsNotification(string action, string summary)
        => IsNotificationRow.Compile()(new GameActionLog { Action = action, Summary = summary });
}
