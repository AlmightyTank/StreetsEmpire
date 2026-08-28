using Microsoft.EntityFrameworkCore;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Support;

/// <summary>
/// Who may write to whom, in one place.
///
/// It was in two: ChatService refused the send, and ResponseMappers decided whether the button was
/// drawn, and each carried its own copy of the same switch. They agreed, which is what two copies of a
/// rule do until one of them is taught something. Drift between these two in particular is worse than
/// most - one says the door is open while the other holds it shut, so the player is told they can write
/// and then told they cannot.
///
/// The pact case is why they had to be joined rather than kept in step. Answering it needs the crews
/// this player's crew has a standing pact with, which is a database question, and the display copy had
/// no database to ask.
/// </summary>
internal static class DirectMessages
{
    /// <summary>
    /// The crews this alliance has an active pact with. Empty for a player in no crew, which is the
    /// answer that makes the Pacts policy behave like Alliance for them rather than throwing.
    ///
    /// Loaded once per request by the caller and handed to every comparison, because the alternative is
    /// a query per row on a list of fifty targets.
    /// </summary>
    internal static async Task<HashSet<long>> PactAlliesAsync(
        GameDbContext db, long? allianceId, CancellationToken cancellationToken)
    {
        if (allianceId is not { } id) return [];

        var allies = await db.AlliancePacts.AsNoTracking()
            .Where(x => x.Status == AlliancePactStatuses.Active
                        && (x.RequestingAllianceId == id || x.TargetAllianceId == id))
            .Select(x => x.RequestingAllianceId == id ? x.TargetAllianceId : x.RequestingAllianceId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return [.. allies];
    }

    /// <summary>
    /// Why this message cannot be sent, or null when it can.
    ///
    /// <paramref name="senderPactAllies"/> is what PactAlliesAsync returned for the sender's crew. It is
    /// only ever consulted for the one policy that needs it, so passing an empty set is correct for
    /// every other case rather than merely harmless.
    /// </summary>
    internal static string? BlockedReason(Player? sender, Player recipient, IReadOnlySet<long> senderPactAllies)
    {
        if (sender is null) return "Sign in to send messages.";
        if (sender.Id == recipient.Id) return "You are already talking to yourself.";

        return recipient.Account.DirectMessagePolicy switch
        {
            DirectMessagePolicy.Nobody => "They are not taking direct messages.",

            DirectMessagePolicy.Alliance when !SameCrew(sender, recipient) =>
                "They are only taking messages from their crew.",

            // Their crew, or a crew theirs has a pact with. The pact is read from the sender's side and
            // the recipient's alliance is looked for in it, which is the same set either way round - a
            // pact is mutual, and both rows of it name the pair.
            DirectMessagePolicy.AllianceAndPacts when !SameCrew(sender, recipient)
                && (recipient.AllianceId is null || !senderPactAllies.Contains(recipient.AllianceId.Value)) =>
                "They are only taking messages from their crew and their allies.",

            _ => null,
        };
    }

    /// <summary>Crewless is not a crew: two players in no alliance are not in the same one.</summary>
    private static bool SameCrew(Player sender, Player recipient)
        => sender.AllianceId is not null && sender.AllianceId == recipient.AllianceId;
}
