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
            _ => null
        };
    }

    /// <summary>The log rows that are notifications rather than actions, for filtering both ways.</summary>
    public static bool IsNotification(string action, string summary)
        => action == "LAB" || (action == "HIDEOUT" && summary.EndsWith(" is finished.", StringComparison.Ordinal));
}
