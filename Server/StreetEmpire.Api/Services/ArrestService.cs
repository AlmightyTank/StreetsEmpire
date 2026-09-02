using Microsoft.Extensions.Options;
using StreetEmpire.Api.Data;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

/// <summary>
/// The law taking people off the street, and what it costs to get them back.
///
/// Rolled on the shift rather than on the clock, which is the opposite of the contraband bust and for
/// the opposite reason: holding is a standing risk and is charged for whether or not anybody is at the
/// screen, while a sweep is something that happens to a crew who are out working. Rolling this on the
/// clock would arrest people who had been at home all day.
///
/// It sits outside the economy service because it has to write a row, and that service has no database
/// by design. The caller runs it straight after a shift and appends what it says to the shift's log.
/// </summary>
public sealed class ArrestService(
    GameDbContext db,
    IOptionsSnapshot<GameOptions> options,
    IGameRandom random,
    HideoutService hideouts,
    PimpRoster pimps)
{
    private readonly GameOptions _options = options.Value;

    /// <summary>
    /// Rolls one shift's sweep and takes whoever it took.
    ///
    /// Priced on the crew actually on the street, which is the whole reason it exists: recruits and
    /// finds are both flat per turn, so a shift was pure upside that quietly stopped mattering as the
    /// house grew. A risk that scales with the crew is the counterweight, and it turns a flat trickle
    /// of recruits back into churn at the size where it had become nothing.
    /// </summary>
    public Arrest? RollForShift(Player player, int turns, string? district, DateTime nowUtc)
    {
        var config = _options.Arrests;
        var onStreet = player.Hoes + player.Thugs;
        var exposed = onStreet - Math.Max(0, config.FreeCrewOnStreet);
        // Under the allowance nobody is looking, however long they work. The same floor the heat rules
        // keep, and for the reason stated there: a floor is what makes a small operation safe and stops
        // the game punishing a player for existing.
        if (exposed <= 0 || turns <= 0)
            return null;

        var where = district is null
            ? _options.StreetAction.DefaultDistrict()
            : _options.StreetAction.District(district);
        var shift = Math.Max(0, turns / (double)Math.Max(1, _options.MaxActionTurns));

        // How much of a target the shift was. Crew on the street, how long they were out, where they
        // worked, and how much notice the house was already drawing - heat lifts the odds rather than
        // gating them, because the law being interested is a multiplier on being seen rather than a
        // separate door that has to be opened first.
        var exposure = exposed
                       * Math.Max(0, config.ChancePerCrewPerShift)
                       * shift
                       * (where?.Scale(where.HeatPercent) ?? 1)
                       * (1 + Math.Max(0, hideouts.HeatFor(player)) / Math.Max(1, config.HeatScaleDivisor));

        // Approaches the ceiling rather than marching into it. Read straight off the exposure this was
        // a flat line that hit the cap by the second tier, which flattened every size and every
        // district above it into the same number and made the whole table stop meaning anything - and
        // because the cap was read before the multipliers, a big house in the casino came out at 86%
        // a shift, which is not a risk, it is a tax. A curve keeps the ceiling honest and leaves the
        // district and the heat deciding something at every size.
        var chance = Math.Clamp(config.MaxChancePerShift, 0, 1) * (1 - Math.Exp(-Math.Max(0, exposure)));
        // Somebody watching the street sees them coming. Applied last so the room is worth the same
        // fraction wherever a player sits on that curve, and never stops being worth building.
        chance *= 1 - hideouts.BustRiskReduction(player.Hideout);
        chance = Math.Clamp(chance, 0, 1);

        if (random.NextDouble() >= chance)
            return null;

        // A share of the crew rather than a flat count, so a bigger house loses more and the sweep
        // stays proportionate at every size.
        var share = Math.Clamp(config.MinTakenPercent, 0, 1)
                    + random.NextDouble() * Math.Max(0, config.MaxTakenPercent - config.MinTakenPercent);
        var taken = Math.Min(onStreet, Math.Max(1, (int)Math.Round(onStreet * share)));

        // Split in the proportion they were out in, so a house of mostly hoes loses mostly hoes.
        var hoes = Math.Clamp((int)Math.Round(taken * (player.Hoes / (double)onStreet)), 0, player.Hoes);
        var thugs = Math.Clamp(taken - hoes, 0, player.Thugs);
        if (hoes + thugs == 0)
            return null;

        var pimp = random.NextDouble() < Math.Clamp(config.PimpTakenChance, 0, 1)
            ? pimps.Jail(player, nowUtc)
            : null;

        player.Hoes -= hoes;
        player.Thugs -= thugs;

        var arrest = new Arrest
        {
            Player = player,
            PlayerId = player.Id,
            Hoes = hoes,
            Thugs = thugs,
            // Set through the navigation only. A pimp recruited during this very shift has not been
            // saved yet and still carries an id of zero, and writing that here would store a foreign
            // key pointing at nothing. Entity Framework fills the column from the reference on save.
            Pimp = pimp,
            PimpName = pimp?.Name,
            PimpLoyaltyAtArrest = pimp?.Loyalty ?? 0,
            BailAmount = BailFor(hoes, thugs, pimp is not null),
            City = player.City,
            District = where?.Key ?? string.Empty,
            HeatAtArrest = Math.Round(hideouts.HeatFor(player), 1),
            ChancePercent = (int)Math.Round(chance * 100),
            ArrestedAtUtc = nowUtc,
            BailDeadlineUtc = nowUtc.AddHours(Math.Max(1, config.BailWindowHours))
        };

        db.Arrests.Add(arrest);

        // Added after the record is written, so HeatAtArrest stays the heat that drew the sweep rather
        // than the heat the sweep caused. Earned heat, so it decays: the law has your name for a few
        // hours and then the file goes cold.
        player.Heat += HeatFromArrest(hoes + thugs, pimp is not null);
        return arrest;
    }

    /// <summary>What one sweep puts on the house, before any of it decays.</summary>
    public double HeatFromArrest(int crew, bool pimp)
    {
        var config = _options.Arrests;
        return Math.Max(0, config.HeatPerArrest)
               + Math.Max(0, crew) * Math.Max(0, config.HeatPerArrestedCrew)
               + (pimp ? Math.Max(0, config.HeatPerArrestedPimp) : 0);
    }

    public long BailFor(int hoes, int thugs, bool pimp)
    {
        var config = _options.Arrests;
        return hoes * Math.Max(0, config.BailPerHoe)
               + thugs * Math.Max(0, config.BailPerThug)
               + (pimp ? Math.Max(0, config.BailPerPimp) : 0);
    }

    /// <summary>
    /// Pays it and brings them home.
    ///
    /// Charged from the bank first and cash second, the way every other invoice in the game is. A bond
    /// is settling a bill rather than going to fetch money, so it is not a trip to the bank and is not
    /// charged a fare for one - and pricing it against cash on hand alone would put it out of reach of
    /// exactly the players who can plainly afford it, since a bail worth paying costs more than most
    /// safes hold.
    /// </summary>
    /// <returns>What happened, in the one sentence both a player and a rival are reported with.</returns>
    public string Bail(Player player, Arrest arrest, DateTime nowUtc)
    {
        TravelGate.EnsureLanded(player);
        if (!arrest.IsHeld)
            throw new GameRuleException("That has already been settled.");
        if (arrest.BailDeadlineUtc <= nowUtc)
            throw new GameRuleException("They have been moved on already. Nobody is holding them now.");
        if (player.Cash + player.BankCash < arrest.BailAmount)
            throw new GameRuleException($"Bail is {arrest.BailAmount:C0} and you have {player.Cash + player.BankCash:C0}.");

        var fromBank = Math.Min(player.BankCash, arrest.BailAmount);
        player.BankCash -= fromBank;
        player.Cash -= arrest.BailAmount - fromBank;

        player.Hoes += arrest.Hoes;
        player.Thugs += arrest.Thugs;
        if (arrest.Pimp is { IsJailed: true } pimp)
            pimps.Bail(player, pimp);

        arrest.Outcome = "Bailed";
        arrest.SettledAtUtc = nowUtc;

        var who = new List<string>();
        if (arrest.Hoes > 0) who.Add($"{arrest.Hoes:N0} hoe(s)");
        if (arrest.Thugs > 0) who.Add($"{arrest.Thugs:N0} thug(s)");
        if (arrest.PimpName is not null) who.Add(arrest.PimpName);
        return $"Bailed out {string.Join(" and ", who)} for ${arrest.BailAmount:N0}.";
    }

    /// <summary>
    /// Nobody came, either because the clock ran out or because the player said so.
    ///
    /// The cost is paid by the people still outside. Morale, because a crew who watched you leave
    /// somebody inside works differently afterwards; loyalty, for the same reason, on the pimps who
    /// have names to lose; and heat, if the one left had little enough reason to keep quiet. That last
    /// is the only place loyalty a player never spent buys them anything.
    /// </summary>
    public AbandonedCrew Abandon(Player player, Arrest arrest, DateTime nowUtc)
    {
        var config = _options.Arrests;

        // Capped, and the cap is load-bearing rather than tidy: the hiring floor sits at 35 morale, so
        // an uncapped penalty could drop a player under the line that lets them replace the very crew
        // they have just lost. Broke, short-handed and locked out of the fix is a hole, not a
        // consequence.
        var penalty = Math.Min(
            Math.Max(0, config.MaxAbandonMoralePenalty),
            arrest.Heads * Math.Max(0, config.AbandonMoralePerHead));
        player.HoeHappiness = Math.Clamp(player.HoeHappiness - penalty, 0, 100);
        player.ThugHappiness = Math.Clamp(player.ThugHappiness - penalty, 0, 100);

        foreach (var crew in pimps.Active(player))
            crew.Loyalty = Math.Clamp(crew.Loyalty - Math.Max(0, config.AbandonLoyaltyPenalty), 0, 100);

        var talked = false;
        if (arrest.Pimp is { IsJailed: true } pimp)
        {
            talked = arrest.PimpLoyaltyAtArrest < config.TalkLoyaltyThreshold
                     && random.NextDouble() < Math.Clamp(config.TalkChance, 0, 1);
            pimps.LeaveInside(player, pimp, nowUtc);
            if (talked)
                player.Heat += Math.Max(0, config.TalkHeat);
        }

        arrest.Outcome = "Abandoned";
        arrest.SettledAtUtc = nowUtc;
        return new AbandonedCrew(arrest.Hoes, arrest.Thugs, arrest.PimpName, penalty, talked);
    }
}

/// <summary>What leaving them cost, for the sentence that reports it.</summary>
public sealed record AbandonedCrew(int Hoes, int Thugs, string? PimpName, double MoralePenalty, bool Talked)
{
    /// <param name="deliberate">
    /// Whether the player said it out loud or the clock said it for them. The same thing happens either
    /// way, but "nobody came for them" is not true of somebody who stood there and decided, and telling
    /// a player they were absent from their own decision reads as the game losing track of them.
    /// </param>
    public string Describe(bool deliberate = false)
    {
        var lost = new List<string>();
        if (Hoes > 0) lost.Add($"{Hoes:N0} hoe(s)");
        if (Thugs > 0) lost.Add($"{Thugs:N0} thug(s)");
        if (PimpName is not null) lost.Add(PimpName);
        var who = lost.Count == 0 ? "your crew" : string.Join(" and ", lost);
        var said = Talked
            ? $" {PimpName} talked on the way in, and the law is paying attention now."
            : string.Empty;
        return deliberate
            ? $"You left {who} inside. The ones still out noticed.{said}"
            : $"Nobody came for {who}. The ones still out noticed.{said}";
    }
}
