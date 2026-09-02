namespace StreetEmpire.Api.Models;

/// <summary>
/// A run of the world, from everybody starting level to everybody starting level again.
///
/// The last thing worth adding rather than the first, and only because there is finally enough to
/// climb: a turn bank that grows with the building, four tiers of house, ground that takes months to
/// work up, and a war to fight over the top of it. A reset before any of that existed would have taken
/// away an afternoon and given back an afternoon, which is not a season, it is a punishment on a timer.
///
/// What a season resets is the empire. What it never resets is the person: the account, the name, the
/// crew they run with, and every honour they have ever won. That split is the whole design - the thing
/// being taken away is the thing that stops being fun once it is finished, and the thing being kept is
/// the only proof anybody has that they did it.
/// </summary>
public sealed class Season
{
    public long Id { get; set; }

    /// <summary>One, two, three. What everybody calls it.</summary>
    public int Number { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Status { get; set; } = SeasonStatuses.Running;

    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When it is due to end. A date rather than a duration so that everybody can see the same clock
    /// and plan against it - a season whose end nobody can name is just a rumour that the world might
    /// be deleted.
    /// </summary>
    public DateTime EndsAtUtc { get; set; }

    public DateTime? EndedAtUtc { get; set; }

    /// <summary>How many players were still standing at the end, for the record page.</summary>
    public int Players { get; set; }
}

/// <summary>
/// Where one player finished one season. The permanent half of the game.
///
/// Written for everybody rather than only the top, because a season a player finished fortieth in is
/// still a season they played, and a record that only remembers winners is a record most people have
/// no reason to look at.
/// </summary>
public sealed class SeasonResult
{
    public long Id { get; set; }

    public long SeasonId { get; set; }
    public Season Season { get; set; } = null!;

    public Guid PlayerId { get; set; }
    public Player Player { get; set; } = null!;

    /// <summary>Copied rather than joined: the name and town at the finish are facts about that season.</summary>
    public string PlayerName { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string? CrewName { get; set; }

    public int Rank { get; set; }
    public long NetWorth { get; set; }

    /// <summary>
    /// The Season 1 score: cash taken in raids, plus the configured value of weed and coke taken in
    /// those raids. Stored with the result because combat logs are wiped when the world rolls.
    /// </summary>
    public long RaidScore { get; set; }
    public long RaidCashTaken { get; set; }
    public int RaidWeedTaken { get; set; }
    public int RaidCokeTaken { get; set; }

    /// <summary>What they came away with, or null for a finish that was not one of the three.</summary>
    public string? Honour { get; set; }
}

public static class SeasonStatuses
{
    public const string Running = "Running";
    public const string Ended = "Ended";
}

/// <summary>
/// The three finishes worth a name. Kept short and few on purpose: an honour everybody has is a
/// participation sticker, and the point of these is that they are the only thing that survives a reset.
/// </summary>
public static class SeasonHonours
{
    public const string Champion = "Champion";
    public const string TopThree = "Top Three";
    public const string TopTen = "Top Ten";

    /// <summary>What a finishing position is worth, or null for one that is worth nothing but the memory.</summary>
    public static string? For(int rank) => rank switch
    {
        1 => Champion,
        2 or 3 => TopThree,
        <= 10 and > 0 => TopTen,
        _ => null
    };
}
