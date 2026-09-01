using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

/// <summary>
/// The town's book of work, and the three of it a player is holding.
///
/// Generated on read rather than by a background service, the way ground and contracts always were: a
/// town nobody has visited needs no jobs, and topping the book up when somebody looks at it costs one
/// query and cannot drift out of step with a timer nobody is watching.
///
/// Two ideas, kept apart on purpose:
///
/// The <b>book</b> is the town's. Sixteen to eighteen jobs live at once, the same set for everybody in
/// that city, and a rival finishing one takes it off the board for the rest of the world. That is what
/// makes the work finite and worth getting to first.
///
/// The <b>hand</b> is the player's - three jobs out of the book, remembered, so that a job is still
/// there when you come back with the goods you went away to make. Without the memory the board would
/// deal a fresh three on every refresh and nothing on it would ever be worth starting.
/// </summary>
public sealed class TraderJobService(GameDbContext db, IOptionsSnapshot<GameOptions> options, IGameRandom random)
{
    private readonly GameOptions _options = options.Value;

    /// <summary>Everything the dealer can be asked to take for their own shelf: whatever a bench makes.</summary>
    public IReadOnlyList<string> SupplyGoods()
    {
        var goods = new List<string>();
        foreach (var tier in _options.Weapons.Where(x => x.CanForge).OrderBy(x => x.Price))
            goods.Add(tier.Key);
        foreach (var makeable in _options.Makeables.Where(x => x.CanMake))
            goods.Add(makeable.Key);
        return goods;
    }

    /// <summary>
    /// What is going in a town right now, topping the book up first.
    ///
    /// Expired jobs are left in the table rather than deleted: a player who missed one should be able to
    /// see that they missed it, and the book reads the open ones by date anyway.
    /// </summary>
    public async Task<IReadOnlyList<TraderJob>> BookAsync(string city, DateTime nowUtc, CancellationToken ct)
    {
        var config = _options.Store.Jobs;
        var open = await db.TraderJobs
            .Where(x => x.City == city && x.FilledAtUtc == null && x.ExpiresAtUtc > nowUtc)
            .OrderBy(x => x.ExpiresAtUtc)
            .ToListAsync(ct);

        var target = random.NextInclusive(
            Math.Max(1, Math.Min(config.BookMin, config.BookMax)),
            Math.Max(1, Math.Max(config.BookMin, config.BookMax)));
        if (open.Count >= target)
            return open;

        var lastPosted = await db.TraderJobs
            .Where(x => x.City == city)
            .OrderByDescending(x => x.PostedAtUtc)
            .Select(x => (DateTime?)x.PostedAtUtc)
            .FirstOrDefaultAsync(ct);
        if (lastPosted is { } posted && posted.AddMinutes(Math.Max(0, config.PostIntervalMinutes)) > nowUtc)
            return open;

        // A town nobody has visited fills up on the first look, which is what makes it feel settled
        // rather than empty. After that it is one at a time, so a stripped book recovers at the town's
        // pace and whoever cleared it actually took something.
        var toPost = lastPosted is null ? target - open.Count : 1;
        for (var i = 0; i < toPost; i++)
        {
            var job = Compose(city, nowUtc, open);
            if (job is null) break;
            db.TraderJobs.Add(job);
            open.Add(job);
        }

        await db.SaveChangesAsync(ct);
        return open.OrderBy(x => x.ExpiresAtUtc).ToList();
    }

    /// <summary>
    /// The three jobs this player is being told about in the town they are standing in, dealing into any
    /// slot that has come empty.
    ///
    /// A slot empties on its own when the job in it is finished - by them or by anybody - or when it
    /// expires. Refilling those is free and automatic: it is the book moving on, not the player asking
    /// for something better, and charging for it would mean a job somebody else took cost you money.
    /// </summary>
    public async Task<IReadOnlyList<TraderJobLead>> HandAsync(Player player, DateTime nowUtc, CancellationToken ct)
    {
        var config = _options.Store.Jobs;
        var book = await BookAsync(player.City, nowUtc, ct);
        var size = Math.Max(1, config.HandSize);

        var held = await db.TraderJobLeads
            .Include(x => x.Job)
            .Where(x => x.PlayerId == player.Id && x.City == player.City)
            .ToListAsync(ct);

        // Anything dead, and anything the slot table no longer has room for, goes back.
        var stale = held.Where(x => x.Slot >= size || !x.Job.IsOpen(nowUtc)).ToList();
        if (stale.Count > 0)
        {
            db.TraderJobLeads.RemoveRange(stale);
            held = held.Except(stale).ToList();
        }

        var dealt = false;
        for (var slot = 0; slot < size; slot++)
        {
            if (held.Any(x => x.Slot == slot)) continue;
            var lead = Deal(player, book, held, slot, nowUtc);
            if (lead is null) continue;
            db.TraderJobLeads.Add(lead);
            held.Add(lead);
            dealt = true;
        }

        if (stale.Count > 0 || dealt)
            await db.SaveChangesAsync(ct);

        return held.OrderBy(x => x.Slot).ToList();
    }

    /// <summary>
    /// Swaps the named slots for something else out of the book.
    ///
    /// Charged per slot rather than per press, counting on from whatever has already been paid for this
    /// cycle: taking all three at once is three draws and costs three. Per press, rerolling the whole
    /// hand would always be the only sensible way to touch the button.
    /// </summary>
    public async Task<TraderJobReroll> RerollAsync(Player player, IReadOnlyList<int> slots, DateTime nowUtc, CancellationToken ct)
    {
        TravelGate.EnsureLanded(player);
        var config = _options.Store.Jobs;
        var size = Math.Max(1, config.HandSize);

        var wanted = slots.Distinct().OrderBy(x => x).ToList();
        if (wanted.Count == 0)
            throw new GameRuleException("Say which of them to ask about.");
        if (wanted.Any(x => x < 0 || x >= size))
            throw new GameRuleException("That is not one of the jobs you are holding.");

        var hand = (await HandAsync(player, nowUtc, ct)).ToList();
        var book = await BookAsync(player.City, nowUtc, ct);

        // A job with goods already in it is not rerollable, at any price. Swapping it out would take
        // back an unfinished job somebody has stock sunk into and the premium riding on - the goods are
        // gone, the completion bonus is not paid until the last unit, and the slot would come back
        // holding something else entirely. Refused by name rather than quietly skipped, because a
        // player who ticked three boxes and paid for three draws should be told which one did not go.
        var started = hand
            .Where(x => wanted.Contains(x.Slot) && x.Job.DeliveredQuantity > 0 && x.Job.ClaimedById == player.Id)
            .ToList();
        if (started.Count > 0)
        {
            var job = started[0].Job;
            throw new GameRuleException(
                $"You have {job.DeliveredQuantity:N0} {TradeGoods.Label(job.Good).ToLowerInvariant()} in "
                + $"{StoreTrader.For(job.City, _options).Name}'s job already. Finish it or let it run out.");
        }

        // The cycle turns over before anything is counted, so a player coming back after the clock has
        // run gets their free one whether or not they ever spent the last cycle's.
        if (player.JobRerollsResetAtUtc is not { } resetAt || resetAt <= nowUtc)
        {
            player.JobRerollsUsed = 0;
            player.JobRerollsResetAtUtc = nowUtc.AddHours(Math.Max(1, config.Reroll.FreeEveryHours));
        }

        var steps = new List<TraderJobRerollStepOptions>();
        for (var i = 0; i < wanted.Count; i++)
            steps.Add(config.Reroll.Step(player.JobRerollsUsed + i));
        var cash = steps.Sum(x => Math.Max(0, x.Cash));
        var rep = steps.Sum(x => Math.Max(0, x.Rep));

        if (player.Cash < cash)
            throw new GameRuleException($"Asking about {Describe(wanted.Count)} comes to {cash:C0} and you are carrying {player.Cash:C0}.");

        // Standing is spendable here and nowhere else, and it is what the gun counter reads - so the
        // one thing this must never do is quietly shut a shelf somebody was already buying from. You
        // can spend what you carry above your rung and not a point more, and being told so is better
        // than finding out at the counter.
        var floor = _options.Store.LevelFor(player.StoreRep)?.Rep ?? 0;
        var spendable = Math.Max(0, (int)Math.Floor(player.StoreRep) - floor);
        if (rep > spendable)
        {
            var level = _options.Store.LevelFor(player.StoreRep);
            throw new GameRuleException(
                $"That costs {rep:N0} rep and you are only {spendable:N0} clear of {level?.Name ?? "your standing"}. "
                + "Asking again would cost you the rung.");
        }

        player.Cash -= cash;
        player.StoreRep = Math.Max(0, player.StoreRep - rep);
        player.JobRerollsUsed += wanted.Count;

        var taken = hand.Where(x => wanted.Contains(x.Slot)).ToList();
        db.TraderJobLeads.RemoveRange(taken);
        var kept = hand.Except(taken).ToList();

        var replaced = 0;
        foreach (var slot in wanted)
        {
            var lead = Deal(player, book, kept, slot, nowUtc);
            if (lead is null) continue;
            db.TraderJobLeads.Add(lead);
            kept.Add(lead);
            replaced++;
        }

        await db.SaveChangesAsync(ct);

        // Said plainly when the book could not answer, because an empty slot after paying looks like a
        // bug and is not: the town is out of that kind of work and will post more.
        var summary = replaced == wanted.Count
            ? $"Asked about {Describe(wanted.Count)}."
              + (cash > 0 || rep > 0 ? $" It cost {cash:C0} and {rep:N0} rep." : " That one was free.")
            : $"Asked about {Describe(wanted.Count)} and only {replaced:N0} came back - the book is thin in {player.City} right now."
              + (cash > 0 || rep > 0 ? $" It cost {cash:C0} and {rep:N0} rep." : string.Empty);

        return new TraderJobReroll(kept.OrderBy(x => x.Slot).ToList(), cash, rep, replaced, summary);
    }

    private static string Describe(int count) => count == 1 ? "one job" : count == 2 ? "two jobs" : $"{count:N0} jobs";

    /// <summary>
    /// Draws one job for a slot: the right kind for it, not already in the hand, and not one somebody
    /// else has a claim on. Null when the book has nothing to offer, which is a real answer.
    /// </summary>
    private TraderJobLead? Deal(Player player, IReadOnlyList<TraderJob> book, IReadOnlyList<TraderJobLead> hand, int slot, DateTime nowUtc)
    {
        var kind = _options.Store.Jobs.SlotKind(slot);
        var holding = hand.Select(x => x.JobId).ToHashSet();
        var candidates = book
            .Where(x => !holding.Contains(x.Id))
            .Where(x => x.CanBeWorkedBy(player.Id))
            .Where(x => kind is null || x.Kind == kind)
            .ToList();

        // A reserved slot falls back to anything rather than sitting empty. A town that happens to have
        // no supply work going should still deal three jobs; the reservation is there to stop the hand
        // being one-note, not to punish a thin book by leaving a hole in it.
        if (candidates.Count == 0 && kind is not null)
            candidates = book
                .Where(x => !holding.Contains(x.Id))
                .Where(x => x.CanBeWorkedBy(player.Id))
                .ToList();
        if (candidates.Count == 0)
            return null;

        var job = candidates[random.NextInclusive(0, candidates.Count - 1)];
        return new TraderJobLead
        {
            PlayerId = player.Id,
            JobId = job.Id,
            Job = job,
            City = job.City,
            Slot = slot,
            DealtAtUtc = nowUtc,
        };
    }

    /// <summary>
    /// Hands over as much of a job as the player can manage, and takes the money for it.
    ///
    /// Deliveries pay the going rate the moment they are made, and the premium arrives whole when the
    /// last unit goes in. That is what lets a small room work a big job without the instalments becoming
    /// free money: stopping half way leaves a player exactly where selling the same goods flat would
    /// have, so the only thing an abandoned job costs is the chance at the premium.
    ///
    /// Every refusal is a real one: this is the same stock the rest of the game moves, so a job cannot
    /// be fed product the player does not have, or coke too weak for a buyer who asked for strength.
    /// </summary>
    /// <param name="quantity">
    /// How much to hand over, or null for as much as will go. More than is held is refused rather than
    /// quietly reduced, because a player who typed a number meant it.
    /// </param>
    public TraderJobFill Deliver(TraderJob job, Player player, DateTime nowUtc, int? quantity = null)
    {
        TravelGate.EnsureLanded(player);
        // Everything on the board is the dealer's. Who they are doing it for is a sentence about the
        // job rather than somebody the player hands goods to.
        var trader = StoreTrader.For(job.City, _options);
        var asking = trader.Name;

        if (!job.IsOpen(nowUtc))
            throw new GameRuleException("That one is gone. Somebody else got there, or it ran out of time.");
        if (!string.Equals(job.City, player.City, StringComparison.OrdinalIgnoreCase))
            throw new GameRuleException($"{asking} is in {job.City}. You have to be there.");
        if (!job.CanBeWorkedBy(player.Id))
            throw new GameRuleException("Somebody else is already filling that one.");

        var label = TradeGoods.Label(job.Good).ToLowerInvariant();
        var held = TradeGoods.Held(player, job.Good);
        if (held <= 0)
            throw new GameRuleException($"You have no {label} to hand over.");

        // Purity belongs to the pile rather than to the units leaving it, so it is checked on every
        // delivery: a buyer who accepted a strong first instalment has not agreed to a weak second.
        if (job.MinimumPurityPercent is { } floor)
        {
            var purity = (int)Math.Round(player.CokePurity * 100);
            if (purity < floor)
                throw new GameRuleException($"They want it at least {floor}% pure. Yours is {purity}%.");
        }

        var handing = quantity ?? Math.Min(held, job.Remaining);
        if (handing <= 0)
            throw new GameRuleException("Say how much you are handing over.");
        if (handing > held)
            throw new GameRuleException($"You are handing over {handing:N0} {label} and you have {held:N0}.");
        if (handing > job.Remaining)
            throw new GameRuleException($"They only still want {job.Remaining:N0} {label}.");

        TradeGoods.Add(player, job.Good, -handing);
        job.DeliveredQuantity += handing;
        job.ClaimedById ??= player.Id;

        var paid = handing * job.ReferencePricePerUnit;
        var completed = job.Remaining == 0;
        var rep = 0;
        var climbed = false;
        if (completed)
        {
            paid += job.CompletionBonus;
            job.FilledById = player.Id;
            job.FilledBy = player;
            job.FilledAtUtc = nowUtc;

            var before = player.StoreRep;
            rep = job.Rep;
            player.StoreRep = Math.Max(0, player.StoreRep + rep);
            climbed = (_options.Store.LevelFor(player.StoreRep)?.Level ?? 1)
                      > (_options.Store.LevelFor(before)?.Level ?? 1);
        }

        player.Cash += paid;

        var summary = completed
            ? $"Finished {asking}'s job: {job.Quantity:N0} {label} for {job.Payout:C0}"
              + (job.CompletionBonus > 0 ? $", {job.CompletionBonus:C0} more than selling it flat." : ".")
              + $" {trader.Name} hears about it: +{rep:N0} rep."
              + (climbed ? $" They call you {_options.Store.LevelFor(player.StoreRep)!.Name} now." : string.Empty)
            : $"Ran {handing:N0} {label} to {asking} for {paid:C0}. "
              + $"{job.Remaining:N0} to go, and {job.CompletionBonus:C0} waiting on the last of it.";

        return new TraderJobFill(job, paid, completed ? job.CompletionBonus : 0, handing, completed, rep, summary);
    }

    /// <summary>
    /// One job, drawn from what the town is like and which half of the game it speaks to.
    ///
    /// Null when there is nothing to compose - a town with no places on the map and a shop with no
    /// forgeable goods - which is a configuration problem rather than a state the board should invent
    /// its way around.
    /// </summary>
    private TraderJob? Compose(string city, DateTime nowUtc, IReadOnlyList<TraderJob> open)
    {
        var config = _options.Store.Jobs;
        // Supply and product in the ratio the hand asks for them, so the book holds roughly what the
        // slots will want. A book that was nine-tenths product would leave the reserved supply slot
        // dealing the same two jobs to everybody in town.
        var kind = random.NextInclusive(0, 2) == 0 ? TraderJobKind.Supply : TraderJobKind.Product;
        // How many lines this counter is already out of. A gap shuts a line until somebody fills it, so
        // left uncapped a run of them would close most of a shop at once - and a town where half the
        // shelf is dark is not a town with a problem to solve, it is a town that is shut.
        var gaps = open.Count(x => x.Reason == TraderJobReason.ShelfGap);
        return kind == TraderJobKind.Supply
            ? ComposeSupply(city, nowUtc, gaps) ?? ComposeProduct(city, nowUtc)
            : ComposeProduct(city, nowUtc) ?? ComposeSupply(city, nowUtc, gaps);
    }

    private TraderJob? ComposeSupply(string city, DateTime nowUtc, int openGaps)
    {
        var config = _options.Store.Jobs;
        var family = config.Supply;
        var askable = SupplyGoods();
        if (askable.Count == 0) return null;

        var good = askable[random.NextInclusive(0, askable.Count - 1)];
        // At this town's own price, not the national one, so "against $X on the shelf" is a sentence
        // about the shelf the player is standing in front of.
        var shelf = (long)Math.Max(1, StoreTrader.Price(city, _options, (int)Math.Min(int.MaxValue, TradeGoods.ReferencePrice(_options, good, city))));
        var premium = Premium(family);
        // Strictly over the going rate whatever the tuning says, so a job can never be one a player
        // loses money finishing after they have already handed the goods over.
        var pay = Math.Max(shelf + 1, (long)Math.Round(shelf * premium));

        var (reason, behalf) = SupplyReason(city, good, openGaps);
        return Finish(new TraderJob
        {
            City = city,
            Kind = TraderJobKind.Supply,
            Reason = reason,
            OnBehalfOf = behalf,
            Good = good,
            Quantity = random.NextInclusive(Math.Max(1, family.MinQuantity), Math.Max(1, family.MaxQuantity)),
            PricePerUnit = pay,
            ReferencePricePerUnit = shelf,
        }, family, nowUtc);
    }

    private TraderJob? ComposeProduct(string city, DateTime nowUtc)
    {
        var config = _options.Store.Jobs;
        var family = config.Product;
        var places = _options.Territory.Map
            .Where(x => string.Equals(x.City, city, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Name)
            .ToList();
        if (places.Count == 0) return null;

        var good = ChooseProductGood(city);
        var list = Math.Max(1, TradeGoods.ReferencePrice(_options, good, city));
        var premium = Premium(family);

        int? purityFloor = good == "coke" && random.NextDouble() < config.PurityConditionChance
            ? config.MinimumPurityFloorPercent
            : null;
        // Somebody paying for strength pays for it, or the condition is only a penalty.
        if (purityFloor is not null)
            premium += config.PurityPremiumPercent / 100.0;

        // A town job is a favour or a deal, and either way it is the dealer asking. The place is who
        // they are doing it for rather than who the player hands the goods to.
        var place = places[random.NextInclusive(0, places.Count - 1)];
        return Finish(new TraderJob
        {
            City = city,
            Kind = TraderJobKind.Product,
            Reason = random.NextInclusive(0, 1) == 0 ? TraderJobReason.Favour : TraderJobReason.Deal,
            OnBehalfOf = place,
            Good = good,
            Quantity = random.NextInclusive(Math.Max(1, family.MinQuantity), Math.Max(1, family.MaxQuantity)),
            PricePerUnit = Math.Max(list + 1, (long)Math.Round(list * premium)),
            ReferencePricePerUnit = list,
            MinimumPurityPercent = purityFloor,
        }, family, nowUtc);
    }

    /// <summary>
    /// Why the dealer wants something for their own end of the shelf.
    ///
    /// A shelf gap only where it could actually be a gap: a line this counter carries, and never one of
    /// the three every counter always has. Everything else becomes covering another town, which is the
    /// reason that ties the map together - the Duchess is dry in Miami and your dealer said they would
    /// see to it.
    /// </summary>
    private (TraderJobReason Reason, string? OnBehalfOf) SupplyReason(string city, string good, int openGaps)
    {
        if (openGaps < Math.Max(0, _options.Store.Jobs.MaxShelfGapsPerCity)
            && StoreTrader.Carries(city, _options, good)
            && !StoreTrader.Always.Contains(good)
            && random.NextInclusive(0, 1) == 0)
            return (TraderJobReason.ShelfGap, null);

        // Somebody else's counter, and only one that actually stocks the thing - covering a shop for
        // something it has never sold would be a favour nobody asked for.
        var elsewhere = _options.Store.Traders
            .Where(x => !string.Equals(x.City, city, StringComparison.OrdinalIgnoreCase))
            .Where(x => StoreTrader.Carries(x.City, _options, good))
            .ToList();
        if (elsewhere.Count == 0)
            return (TraderJobReason.Deal, null);

        var mate = elsewhere[random.NextInclusive(0, elsewhere.Count - 1)];
        return (TraderJobReason.CoveringTrader, $"{mate.Name} in {mate.City}");
    }

    /// <summary>
    /// The lines a town's counter is out of: every open shelf-gap job in it.
    ///
    /// The job is the gap. There is no stock table and no depleting shelf, deliberately - a shelf that
    /// emptied because somebody bought it out would be a shared resource players could strip to close a
    /// town, and a race on every purchase. A gap is rolled, it shuts the line while it stands, and
    /// filling it is what puts the line back.
    /// </summary>
    public async Task<HashSet<string>> DryGoodsAsync(string city, DateTime nowUtc, CancellationToken ct)
    {
        var gaps = await db.TraderJobs
            .Where(x => x.City == city && x.FilledAtUtc == null && x.ExpiresAtUtc > nowUtc)
            .Where(x => x.Reason == TraderJobReason.ShelfGap)
            .Select(x => x.Good)
            .ToListAsync(ct);

        // Filtered on the way out as well as on the way in. The rule that condoms, beer and a pistol are
        // always on the shelf is what stops a town becoming one nobody can play in, and a rule that
        // important should not depend on every writer of a row having remembered it - an old job, a
        // retuned stock list or a migration's guess would all otherwise close a counter for good.
        return gaps
            .Where(x => !StoreTrader.Always.Contains(x))
            .Where(x => StoreTrader.Carries(city, _options, x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private double Premium(TraderJobFamilyOptions family)
        => 1 + Math.Max(0, family.MinPremiumPercent) / 100.0
             + random.NextInclusive(0, Math.Max(0, family.PremiumSpreadPercent)) / 100.0;

    /// <summary>The parts every job has however it was composed: the clock, and what it is worth.</summary>
    private TraderJob Finish(TraderJob job, TraderJobFamilyOptions family, DateTime nowUtc)
    {
        var config = _options.Store.Jobs;
        job.PostedAtUtc = nowUtc;
        job.ExpiresAtUtc = nowUtc.AddHours(random.NextInclusive(
            Math.Max(1, config.MinLifetimeHours),
            Math.Max(1, config.MaxLifetimeHours)));
        job.Rep = Math.Clamp(
            (int)Math.Round(job.Payout * Math.Max(0, family.RepPerDollar)),
            family.MinRep,
            Math.Max(family.MinRep, family.MaxRep));
        return job;
    }

    /// <summary>
    /// What a town's buyers ask for. Weighted towards what it pays most for, because a buyer wanting the
    /// thing nobody there can shift would be a job posted by nobody in particular.
    /// </summary>
    private string ChooseProductGood(string city)
    {
        var config = _options.Store.Jobs;
        var roll = random.NextInclusive(0, 99);
        // A gun job names the gun. "Forty weapons" would let a player fill a rifle-priced job with
        // pistols, and the whole point of a job is that the buyer knows what they want.
        if (roll < config.WeaponsPercent)
            return WeaponTiers.All[random.NextInclusive(0, WeaponTiers.All.Length - 1)];
        if (roll < config.WeaponsPercent + config.MoonshinePercent) return "moonshine";

        var weed = TradeGoods.ReferencePrice(_options, "weed", city) / (double)Math.Max(1, _options.WeedSellPrice);
        var coke = TradeGoods.ReferencePrice(_options, "coke", city) / (double)Math.Max(1, _options.CokeSellPrice);
        var favoured = coke >= weed ? "coke" : "weed";
        var other = favoured == "coke" ? "weed" : "coke";
        return random.NextInclusive(0, 99) < Math.Clamp(config.FavouredGoodPercent, 50, 100) ? favoured : other;
    }
}

/// <summary>What a delivery paid, how much went in, and whether that was the last of it.</summary>
/// <param name="RepEarned">Nothing until the last unit goes in, like the premium.</param>
public sealed record TraderJobFill(
    TraderJob Job,
    long Paid,
    long Premium,
    int Delivered,
    bool Completed,
    int RepEarned,
    string Summary);

/// <summary>What asking again cost, and what came back.</summary>
public sealed record TraderJobReroll(
    IReadOnlyList<TraderJobLead> Hand,
    long Cash,
    int Rep,
    int Replaced,
    string Summary);
