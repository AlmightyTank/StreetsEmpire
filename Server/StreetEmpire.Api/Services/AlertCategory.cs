namespace StreetEmpire.Api.Services;

/// <summary>
/// The switches on the account page that an alert can belong to.
///
/// Separate from the email preferences on purpose: somebody who wants no mail at all still wants the
/// bell, and somebody who wants mail about a raid does not necessarily want it about a sale. The two
/// sets of switches govern different channels and are stored as different columns.
/// </summary>
public enum AlertCategory
{
    /// <summary>Not governed by any switch. Your own machinery reporting in - labs, builds, mules.</summary>
    Always,
    Combat,
    Crew,
    Market,
}
