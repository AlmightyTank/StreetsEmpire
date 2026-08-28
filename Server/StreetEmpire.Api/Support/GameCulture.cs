using System.Globalization;

namespace StreetEmpire.Api.Support;

/// <summary>
/// The culture every number in this game is written in.
///
/// Thirty-eight player-facing strings format money with <c>:C0</c>, which asks the ambient culture what
/// a currency looks like. On a developer's Windows machine that culture is American and the answer is
/// "$1,234". In a container it is nothing at all: the runtime images set no LANG, so CurrentCulture
/// falls back to the invariant culture, whose currency symbol is the generic sign - and the game tells
/// a player they sold their coke for ¤94.
///
/// It surfaced as a red test on Linux CI rather than in the game, which was luck. The other thirty-seven
/// places had no test looking at them and would have reached players.
///
/// So the culture is decided here rather than inherited, once, at the top of Program.cs. Fixing the
/// thirty-eight call sites instead would have been thirty-eight chances to miss one, and the thirty-ninth
/// would have been written next week. Nothing about this game is localised - the prose is English and the
/// money is dollars - so there is one right answer and this is the place to say it.
/// </summary>
internal static class GameCulture
{
    /// <summary>
    /// American English, because the money is dollars. Resolved through ICU, which the runtime image
    /// carries; a build with InvariantGlobalization turned on would silently hand back the invariant
    /// culture again, which is what the test in the suite is there to catch.
    /// </summary>
    internal static readonly CultureInfo Formatting = CultureInfo.GetCultureInfo("en-US");

    /// <summary>
    /// Call before anything formats anything. The defaults are what every thread gets, including the
    /// request threads that produce these strings and the background services that write to the log.
    /// </summary>
    internal static void Apply()
    {
        CultureInfo.DefaultThreadCurrentCulture = Formatting;
        CultureInfo.DefaultThreadCurrentUICulture = Formatting;
    }
}
