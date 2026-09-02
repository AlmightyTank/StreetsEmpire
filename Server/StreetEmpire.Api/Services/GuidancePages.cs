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
    /// <summary>The crew you have, and how they are doing.</summary>
    public const string Crew = "crew";

    /// <summary>Hiring and letting go.</summary>
    public const string CrewHiring = "crew-hiring";

    /// <summary>The cells, and what a bond costs.</summary>
    public const string Arrests = "arrests";

    /// <summary>The rooms of the building, and what upgrading one buys.</summary>
    public const string Hideout = "hideout";

    /// <summary>Standing the crew down, which is what a house is for when nobody is working.</summary>
    public const string Recovery = "recovery";

    /// <summary>The bench: making, producing, and selling what came off it.</summary>
    public const string Production = "production";

    /// <summary>The counter, where stock is bought.</summary>
    public const string Store = "store";

    /// <summary>The safe and what is in it.</summary>
    public const string Bank = "bank";

    /// <summary>Buying and selling generally, when nothing more exact is meant.</summary>
    public const string Market = "market";

    /// <summary>Working a shift.</summary>
    public const string Street = "street";

    /// <summary>Where somebody with nothing to do should be looking.</summary>
    public const string Overview = "overview";

    /// <summary>
    /// Every destination guidance is allowed to name.
    ///
    /// These got finer once the client could scroll to a panel rather than only open a page. A name
    /// like "market" was honest while all it could promise was a page; against a panel it is a shrug,
    /// and the two moves that used it for selling were pointing at a screen with nothing to sell on.
    /// </summary>
    public static readonly string[] All =
        [Crew, CrewHiring, Arrests, Hideout, Recovery, Production, Store, Bank, Market, Street, Overview];
}
