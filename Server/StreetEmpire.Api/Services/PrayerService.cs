using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

/// <summary>
/// The shrine. Once a week the pimp gods want something, and giving it to them is answered.
///
/// The source game made this a slot machine: burn whatever you like, roll, maybe something happens.
/// That is a lever rather than a decision, and it sits badly beside everything else here - a game where
/// the lookout tells you what it takes off your odds and a mule quote tells you what a run will clear.
/// So the gods say what they want, and meeting it is answered every time. What stays uncertain is only
/// which blessing lands, and even that is narrowed to the ones that would actually help you.
///
/// The demand is worked out from the week and the player rather than stored, the same trick the rival
/// personalities use: hash the two together and the same demand comes back all week without a row to
/// keep, a job to run, or anything that can drift out of step with itself.
///
/// Nothing here is a way to make money. Every blessing is something money cannot buy at all - notice
/// the law has already taken, a crew's mood, a pimp's faith in you - so a player can never come out of
/// the shrine with more cash than they carried in, however rich they are.
/// </summary>
public sealed class PrayerService(IOptionsSnapshot<GameOptions> options, IGameRandom random, PimpRoster pimps, HideoutService hideout)
{
    private readonly GameOptions _options = options.Value;

    /// <summary>What the gods are asking this player for, this week.</summary>
    public PrayerDemand DemandFor(Player player, DateTime nowUtc)
    {
        var config = _options.Prayer;
        // Player and week together, so every player is asked something different and nobody's demand
        // changes under them because they reloaded the page.
        var seed = SHA256.HashData(Encoding.UTF8.GetBytes($"{player.Id:N}:{WeekOf(nowUtc)}"));

        // Cash is the most common ask because it is the one thing every player certainly has some of.
        // The rest name a good, which turns a week into a small errand: they want coke, and you have
        // until the week turns to go and get some.
        var good = (seed[0] % 10) switch
        {
            <= 3 => "cash",
            4 or 5 => "weed",
            6 or 7 => "coke",
            8 => "moonshine",
            _ => "condoms"
        };

        // Sized against what the player is worth, so the ask means the same thing to a rookie and to an
        // empire. A flat number would be an insult at one end and impossible at the other.
        //
        // Banded rather than taken exactly, and that matters more than it looks. Net worth moves every
        // time anything happens - a sale, a raid, buying the very thing being asked for - so an exact
        // share would quote one number on the shrine and enforce a different one the moment the player
        // clicked. Two significant figures means the ask only moves when a player's wealth moves by
        // about a tenth, which is a week's real progress rather than the noise of playing.
        // What the player can lay hands on rather than what they are worth. The demand is capped at
        // half a shelf, so counting a building would push every established hideout up against that cap
        // at once and the gods would ask the same of a millionaire and of the man who just bought a roof.
        var netWorth = Band(Math.Max(config.MinimumNetWorthForScale, EconomyService.PlunderOf(player, _options)));
        var target = (long)Math.Round(netWorth * Math.Clamp(config.DemandShareOfNetWorth, 0.001, 0.5));

        if (good == "cash")
            return new PrayerDemand("cash", "cash", Math.Max(config.MinimumCashDemand, target), target);

        var unitPrice = Math.Max(1, TradeGoods.ReferencePrice(_options, good, player.City));
        var wanted = Math.Max(1, (long)Math.Round(target / (double)unitPrice));

        // Capped at what the player could physically keep, and at a fraction of it rather than all of
        // it. The share is a value, and a value turns into an enormous pile when the good is cheap: four
        // percent of a mid empire is six hundred bottles of moonshine, and no storage room in the game
        // holds a tenth of that. An ask nobody can meet however hard they work is not an errand, it is a
        // locked door with a candle in front of it.
        //
        // Dividing by the generous multiplier rather than using the whole shelf keeps the other half of
        // the decision alive: an ask that filled the room would make giving twice as much impossible to
        // hold, and generosity is the only choice the shrine actually offers.
        var shelf = Math.Max(1, TradeGoods.Capacity(hideout.CapacityFor(player.Hideout), good));
        var quantity = Math.Min(wanted, Math.Max(1, shelf / Math.Max(1, config.GenerousMultiplier)));

        return new PrayerDemand(good, TradeGoods.Label(good).ToLowerInvariant(), quantity, quantity * unitPrice);
    }

    /// <summary>When this player may next make an offering, or null when the shrine is open now.</summary>
    public DateTime? NextPrayerAtUtc(Player player)
        => player.LastPrayedAtUtc is { } last
            ? last.AddDays(Math.Max(1, _options.Prayer.CooldownDays))
            : null;

    public bool CanPray(Player player, DateTime nowUtc)
        => NextPrayerAtUtc(player) is not { } next || next <= nowUtc;

    /// <summary>
    /// Makes the offering.
    ///
    /// <paramref name="offered"/> is what the player is putting on the altar, which may be more than
    /// was asked. It is never less: an offering short of the demand is refused before anything is taken,
    /// because taking a player's coke and telling them the gods were unmoved is the kind of mechanic
    /// that teaches people not to touch a thing again.
    /// </summary>
    public PrayerResult Offer(Player player, long offered, DateTime nowUtc)
    {
        TravelGate.EnsureLanded(player);

        var config = _options.Prayer;
        if (!CanPray(player, nowUtc))
        {
            var days = Math.Max(1, (int)Math.Ceiling((NextPrayerAtUtc(player)!.Value - nowUtc).TotalDays));
            throw new GameRuleException($"The gods have heard from you this week. Come back in {days} day(s).");
        }

        var demand = DemandFor(player, nowUtc);
        if (offered < demand.Quantity)
            throw new GameRuleException($"They asked for {demand.Quantity:N0} {demand.Label}. Anything less is not an offering.");

        var held = demand.Good == "cash" ? player.Cash : TradeGoods.Held(player, demand.Good);
        if (held < offered)
            throw new GameRuleException(demand.Good == "cash"
                ? $"You do not have {offered:N0} in cash on hand."
                : $"You only hold {held:N0} {demand.Label}.");

        // Generosity is the only thing the player controls here, and it decides which shelf the
        // blessing comes off rather than rolling for one.
        var generous = offered >= demand.Quantity * Math.Max(1, config.GenerousMultiplier);

        if (demand.Good == "cash") player.Cash -= offered;
        else TradeGoods.Add(player, demand.Good, (int)-Math.Min(int.MaxValue, offered));

        player.LastPrayedAtUtc = nowUtc;

        var blessing = Bless(player, generous, nowUtc);
        var summary = $"You laid {offered:N0} {demand.Label} at the shrine. {blessing.Summary}";

        return new PrayerResult(demand, offered, generous, blessing, summary);
    }

    /// <summary>
    /// Picks a blessing, from the ones that would actually do this player some good.
    ///
    /// Narrowed rather than rolled blind, because a blessing that lands on something you do not need is
    /// indistinguishable from nothing happening - being told the law has lost interest when nobody was
    /// looking for you is a dud, and a weekly ritual that produces duds is a weekly ritual nobody keeps.
    /// </summary>
    private PrayerBlessing Bless(Player player, bool generous, DateTime nowUtc)
    {
        var config = _options.Prayer;
        var candidates = new List<PrayerBlessing>();

        var heat = hideout.HeatFor(player);
        if (heat >= config.HeatWorthClearing)
            candidates.Add(new PrayerBlessing(
                "quiet",
                "The law loses interest",
                $"Whatever they had on you is gone. {heat:N0} heat, forgotten."));

        var morale = Math.Min(player.HoeHappiness, player.ThugHappiness);
        if (morale <= config.MoraleWorthLifting)
            candidates.Add(new PrayerBlessing(
                "morale",
                "The house is lifted",
                "Something goes round the crew and everybody stands a little straighter."));

        var shaky = pimps.Active(player).Any(x => x.Loyalty < config.LoyaltyWorthRestoring);
        if (shaky)
            candidates.Add(new PrayerBlessing(
                "loyalty",
                "Your pimps remember why they follow you",
                "Whatever doubts were going round the crew are not going round it any more."));

        // Turns are the one blessing that cannot be justified by need - everybody always wants more -
        // so they are the reward for generosity rather than a thing the gods offer freely. It is also
        // the only blessing that touches the rate the whole game runs at, which is why it is rationed
        // to a player who gave twice what was asked.
        if (generous)
            candidates.Add(new PrayerBlessing(
                "turns",
                "The week opens up",
                $"You find {config.TurnsBlessing:N0} more turns in the day than there should be."));

        // Nothing wrong with you and nothing extra given: the gods take the offering and say a kind
        // word about the crew, which is the smallest thing they can do rather than nothing at all.
        if (candidates.Count == 0)
            candidates.Add(new PrayerBlessing(
                "morale",
                "The house is lifted",
                "Nothing was wrong, so they settle for making the crew glad to be there."));

        var chosen = candidates[random.NextInclusive(0, candidates.Count - 1)];
        Apply(player, chosen.Kind, generous, nowUtc);
        return chosen with { Summary = $"{chosen.Headline}. {chosen.Detail}" };
    }

    private void Apply(Player player, string kind, bool generous, DateTime nowUtc)
    {
        var config = _options.Prayer;
        var scale = generous ? Math.Max(1, config.GenerousBlessingMultiplier) : 1;

        switch (kind)
        {
            case "quiet":
                // Earned heat only. What a player is sitting on goes on drawing notice, because the
                // gods can make the law forget you, not make a store of coke stop being a store of coke.
                player.Heat = 0;
                break;

            case "morale":
                player.HoeHappiness = Math.Clamp(player.HoeHappiness + config.MoraleBlessing * scale, 0, 100);
                player.ThugHappiness = Math.Clamp(player.ThugHappiness + config.MoraleBlessing * scale, 0, 100);
                break;

            case "loyalty":
                pimps.Recover(player, config.LoyaltyBlessing * scale);
                break;

            case "turns":
                player.Turns = Math.Min(_options.MaxTurns, player.Turns + config.TurnsBlessing);
                break;
        }
    }

    /// <summary>
    /// Rounds a fortune down to two significant figures, so ordinary play does not move the ask.
    /// </summary>
    private static long Band(long netWorth)
    {
        if (netWorth < 100) return netWorth;
        var step = (long)Math.Pow(10, Math.Floor(Math.Log10(netWorth)) - 1);
        return netWorth / step * step;
    }

    /// <summary>
    /// The week a moment falls in, as a stable string. Weeks rather than days so the demand is worth
    /// going and getting: a player asked for coke they do not hold has until the week turns to find it.
    /// </summary>
    private static string WeekOf(DateTime nowUtc)
        => $"{ISOWeek.GetYear(nowUtc)}-{ISOWeek.GetWeekOfYear(nowUtc):00}";
}

/// <summary>What the gods are asking for, and roughly what giving it costs at the counter.</summary>
public sealed record PrayerDemand(string Good, string Label, long Quantity, long ApproximateValue);

public sealed record PrayerBlessing(string Kind, string Headline, string Detail, string Summary = "");

public sealed record PrayerResult(PrayerDemand Demand, long Offered, bool Generous, PrayerBlessing Blessing, string Summary);
