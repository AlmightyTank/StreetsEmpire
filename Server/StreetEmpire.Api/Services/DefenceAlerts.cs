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
    public static int UnreadCount(IEnumerable<DefenceAlertResponse> alerts)
        => alerts.Count(x => x.IsUnread);
}
