namespace StreetEmpire.Api.Services;

/// <summary>
/// Where a piece of guidance can point.
///
/// The server names a section rather than a screen, on purpose: it knows a player needs a bigger room
/// and has no business knowing which page or tab the client keeps rooms on. That is the client's answer
/// to give, and it has moved once already - the hideout lived under Business and lives under Crew now.
///
/// Named here rather than typed out at each call because these strings cross a language boundary, and
/// a boundary held together by string literals on both sides is one nothing can check. With a list, a
/// rule test can read this set, read the client's own mapping, and fail when a name is added here that
/// nothing over there answers to - which is the only way that particular mistake gets caught before a
/// player clicks a button that quietly takes them to the Overview.
/// </summary>
public static class GuidancePages
{
    /// <summary>The crew: who you have, their morale, and hiring more of them.</summary>
    public const string Crew = "crew";

    /// <summary>The building and its rooms. Lives under the crew page on the client.</summary>
    public const string Hideout = "hideout";

    /// <summary>The counter: buying, selling, and the bank.</summary>
    public const string Market = "market";

    /// <summary>Working a shift.</summary>
    public const string Street = "street";

    /// <summary>Where somebody with nothing to do should be looking.</summary>
    public const string Overview = "overview";

    /// <summary>Every destination guidance is allowed to name.</summary>
    public static readonly string[] All = [Crew, Hideout, Market, Street, Overview];
}
