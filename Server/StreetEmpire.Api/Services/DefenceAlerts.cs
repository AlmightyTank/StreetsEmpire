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
                // Named rather than hinted at. "Put something through your house" is what this used to
                // say, borrowed from the attack menu - but there a sentence about medicine follows it
                // straight away and makes the euphemism land. Alone at the top of an alert it named
                // neither what was done nor what it was done to.
                ? ($"{attacker} poisoned your house.", false)
                : ($"{attacker} tried to poison your house. Your medicine held.", true),
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

        // Its own sentence rather than another item on the list, because it is the only loss on this
        // row that is still costing the reader tomorrow. Everything above it is a number that a shift
        // or a hiring puts back; this one has a bill and a clock, and it is worth saying so plainly.
        if (Wrecked(log.DefenderRoomWrecked) is { Count: > 0 } rooms)
            detail = $"{detail} They wrecked your {string.Join(" and ", rooms.Select(HideoutRooms.Name))}. "
                     + (rooms.Count == 1
                         ? "It does nothing until you pay to have it put back."
                         : "They do nothing until you pay to have them put back.");

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

    /// <summary>
    /// The rooms a stored receipt names. Empty for the null every attack that broke nothing carries,
    /// which is nearly all of them - only a raid that took a house can break anything.
    /// </summary>
    public static IReadOnlyList<string> Wrecked(string? rooms)
        => string.IsNullOrWhiteSpace(rooms)
            ? []
            : rooms.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

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
            // A repair landing while the player was away, which is the ordinary case: they paid for it
            // and left. The same reasoning as a finished build - starting one is an action, and the
            // hour it comes back in is something that happened to them.
            "HIDEOUT" when summary.EndsWith(" is working again.", StringComparison.Ordinal)
                => new AlertResponse($"log-{logId}", "hideout", "A room is back", summary, "good", unread, createdAtUtc),
            "BUST" => new AlertResponse($"log-{logId}", "bust", "Raided", summary, "bad", unread, createdAtUtc),
            // A run lands whether or not anyone is watching, so how it went has to come find them.
            "MULE" when summary.Contains("never came back", StringComparison.Ordinal)
                => new AlertResponse($"log-{logId}", "mule", "Your pimp ran", summary, "bad", unread, createdAtUtc),
            "MULE" when summary.Contains("was stopped", StringComparison.Ordinal)
                => new AlertResponse($"log-{logId}", "mule", "Your mule was stopped", summary, "bad", unread, createdAtUtc),
            "MULE" => new AlertResponse($"log-{logId}", "mule", "Your mule is back", summary, "good", unread, createdAtUtc),
            // Work landing on a corner while the player was away. Its own action rather than a
            // fourth reading of GROUND, for the reason the comment on IsNotificationRow already
            // gives: telling these apart by how the sentence ends breaks the moment there is
            // another kind of ground notice, and there now is.
            "GROUNDWORK" => new AlertResponse($"log-{logId}", "groundwork", "Your ground is worked up", summary, "good", unread, createdAtUtc),
            "GROUND" when summary.Contains("held", StringComparison.OrdinalIgnoreCase)
                => new AlertResponse($"log-{logId}", "ground", "Your ground held", summary, "good", unread, createdAtUtc),
            "GROUND"
                => new AlertResponse($"log-{logId}", "ground", "You lost ground", summary, "bad", unread, createdAtUtc),
            // A sale happens to the seller rather than because of them - the row already said so in its
            // own comment where it is written - but it was never in this list, so it sat in the seller's
            // activity looking like something they did while they were asleep.
            "SALE" => new AlertResponse($"log-{logId}", "sale", "Something of yours sold", summary, "good", unread, createdAtUtc),
            // Raised by a crew you have a pact with, at the moment they are being raided. Worth waking
            // somebody for: an assist call nobody sees is a call that expires unanswered.
            "CREW" => new AlertResponse($"log-{logId}", "crew", "Your allies need help", summary, "bad", unread, createdAtUtc),
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
               // A repair landing on the clock, which is the ordinary case: it is paid for and then
               // waited out, usually somewhere else. Its own ending rather than a share of the build's,
               // because starting either one is an action and only the landing is news.
               || (log.Action == "HIDEOUT" && log.Summary.EndsWith(" is working again."))
               // GROUND is ground news happening to you; TERRITORY is ground you acted on yourself and
               // belongs in activity. A separate action rather than matching how the sentence ends,
               // which broke the moment a second kind of ground notice existed.
               || log.Action == "GROUND"
               // Ground finishes being worked up on the holder's clock, which means it lands while
               // they are somewhere else. That is the definition above, so it is news rather than
               // activity - and unlike GROUND it is good news.
               || log.Action == "GROUNDWORK"
               || log.Action == "BUST"
               // A run settles on the clock rather than on a request, so it is news, not activity.
               || log.Action == "MULE"
               // The arrest itself is reported in the shift that caused it, which is activity. This is
               // the deadline running out while nobody was looking, which is the definition above.
               || log.Action == "ARREST"
               // Written to the seller by the buyer's request, and to a crew by their ally's attacker.
               // Neither player was here when it happened, which is the whole definition above.
               || log.Action == "SALE"
               || log.Action == "CREW";

    /// <summary>The same rule negated, for the activity list. Derived so the two cannot disagree.</summary>
    public static Expression<Func<GameActionLog, bool>> IsActionRow { get; } =
        Expression.Lambda<Func<GameActionLog, bool>>(
            Expression.Not(IsNotificationRow.Body),
            IsNotificationRow.Parameters);

    /// <summary>
    /// Which switch on the account page governs an alert.
    ///
    /// Kept beside the kinds rather than in the endpoint that filters, because the failure mode is an
    /// alert kind that belongs to no category and is therefore unfilterable - it would simply always
    /// show, and nothing would say so. Everything not named here is deliberately uncategorised: labs,
    /// builds, mules and busts are your own machinery reporting in, and there is no switch for them.
    /// </summary>
    public static AlertCategory CategoryOf(string kind) => kind switch
    {
        "attack" or "bust" or "ground" => AlertCategory.Combat,
        "crew" or "arrest" => AlertCategory.Crew,
        "sale" => AlertCategory.Market,
        _ => AlertCategory.Always,
    };

    /// <summary>The in-memory twin, kept in step by a test that runs both over the same rows.</summary>
    public static bool IsNotification(string action, string summary)
        => IsNotificationRow.Compile()(new GameActionLog { Action = action, Summary = summary });
}
