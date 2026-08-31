using System.Text.RegularExpressions;
using StreetEmpire.Api.Models;
using StreetEmpire.Api.Services;

namespace StreetEmpire.Api.Support;

/// <summary>
/// The parts of signing up that more than one door needs.
///
/// There are two ways to become a player now - a username and password, or a Discord account - and
/// both end in exactly the same place: an account, a player standing in a town with the starting
/// resources, a hideout, a named crew, and the opening line in the log. That was written once, inline
/// in the register endpoint, which is fine until there is a second door and the two quietly drift into
/// giving new players different amounts of money.
/// </summary>
internal static partial class AccountSetup
{
    /// <summary>
    /// Deliberately loose. This is a second name to sign in under, not a channel anything is sent
    /// down, so the only things worth refusing are the ones that would make it a bad key: no address
    /// part, no domain part, whitespace in the middle, or long enough to be a paste accident.
    /// </summary>
    [GeneratedRegex(@"^[^@\s]+@[^@\s.]+(\.[^@\s.]+)+$")]
    private static partial Regex EmailShape();

    /// <summary>
    /// Folded to lower case, because a unique index compares bytes and a player who signs up as
    /// Sam@example.com will type sam@example.com the next time and expect to be let in. Blank comes
    /// back as null so that "no address" is one value rather than two.
    /// </summary>
    internal static string? NormalizeEmail(string? email)
    {
        var trimmed = email?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed.ToLowerInvariant();
    }

    internal static bool LooksLikeAnEmail(string email)
        => email.Length <= 254 && EmailShape().IsMatch(email);

    /// <summary>Anything with an @ in it was meant as an address, however badly it came out.</summary>
    internal static bool LooksLikeAnAttemptAtEmail(string identifier)
        => identifier.Contains('@');

    /// <summary>
    /// Builds the player, the hideout, the crew and the opening log line for a brand new account. The
    /// caller is left to add the account and the player to the context and save, because only it knows
    /// what else belongs in the same transaction.
    /// </summary>
    internal static (Player Player, GameActionLog Log) NewPlayer(
        PlayerAccount account,
        string playerName,
        string city,
        GameOptions options,
        PimpRoster pimps)
    {
        var now = DateTime.UtcNow;
        var player = new Player
        {
            Account = account,
            Name = playerName,
            City = city
        };
        // What a first day is, from the one place that says so. A season starting over puts an
        // existing player back through the same call, and two copies of "what a new player has" is
        // exactly how the two doors end up handing out different amounts of money.
        StartingState.Apply(player, options, now);
        player.Hideout = new Hideout { Player = player };
        StartingState.Apply(player.Hideout, now);
        // Turns the starting pimp count into named crew.
        pimps.Reconcile(player, DateTime.UtcNow);

        var log = new GameActionLog
        {
            Player = player,
            Action = "START",
            Summary = $"{playerName} started an operation in {player.City} with ${options.StartingCash:N0}, {options.StartingPimps} pimp(s), {options.StartingHoes} hoe(s), and {options.StartingThugs} thug(s).",
            CashDelta = options.StartingCash,
            PimpsDelta = options.StartingPimps,
            HoesDelta = options.StartingHoes,
            ThugsDelta = options.StartingThugs,
            CondomsDelta = options.StartingCondoms,
            BeerDelta = options.StartingBeer,
            WeaponsDelta = options.StartingWeapons
        };

        return (player, log);
    }

    /// <summary>
    /// What a collision on either name is called now that the forms ask for one.
    /// </summary>
    internal const string NameTaken = "That name is already taken.";

    /// <summary>
    /// The player name a sign-up ends up with, and whether it came from the username.
    ///
    /// Both doors used to ask twice - a username and a player name, 3-32 characters each, with no word
    /// anywhere saying how they differed. Every account ever made here answered both with the same
    /// string, which is what people do when asked the same question twice. So the forms ask once and
    /// send it as the username, and the player name follows.
    ///
    /// The two columns stay separate underneath, because they are not the same thing: an admin rename
    /// moves the player name and leaves the sign-in name alone, and nothing renames a username at all.
    /// A caller that wants them apart still can, by sending a player name of its own.
    ///
    /// The flag comes back because it decides what a collision can be told. "Player name is already
    /// taken" names a box that is no longer on the form.
    /// </summary>
    internal static (string Name, bool FromUsername) PlayerNameFor(string username, string? requested)
        => string.IsNullOrWhiteSpace(requested) ? (username, true) : (requested.Trim(), false);

    /// <summary>
    /// Turns a Discord handle into something the username rules will accept: 3-32 characters of the
    /// alphabet the register form allows, with a number stuck on the end if the plain form is taken.
    /// A suggestion only - the finish-signing-up form shows it and the player can type over it.
    /// </summary>
    internal static string SuggestUsername(string handle)
    {
        var cleaned = new string(handle.Where(char.IsLetterOrDigit).ToArray());
        if (cleaned.Length > 32) cleaned = cleaned[..32];
        // A handle can be entirely emoji or punctuation, and "" is not a username.
        return cleaned.Length >= 3 ? cleaned : "player";
    }
}
