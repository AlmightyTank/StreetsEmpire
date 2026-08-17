using Microsoft.Extensions.Options;
using StreetEmpire.Api.Contracts;
using StreetEmpire.Api.Models;

namespace StreetEmpire.Api.Services;

/// <summary>
/// What to do next, and what the game has not shown you yet.
///
/// The old Next Moves panel was a status readout wearing an advice label: four fixed rows that said
/// the same thing on day one and day one hundred, and never once named a move. A new player finished
/// their first hundred turns having clicked one button five times, never having seen production, a
/// lab, the market or heat, with the best purchase available to them - a ten thousand dollar weed lab
/// - sitting unmentioned in a room they had no reason to open.
///
/// So this ranks real moves against the state the player is actually in, and says what each one costs
/// and why it is worth doing. Nothing here changes the game; it only points at it.
/// </summary>
public sealed class GuidanceService(IOptionsSnapshot<GameOptions> options, HideoutService hideouts, EconomyService economy)
{
    private readonly GameOptions _options = options.Value;

    /// <summary>
    /// The handful of moves worth making right now, most pressing first.
    ///
    /// Ranked rather than listed, because a player who is shown everything is shown nothing. Urgency
    /// is what is actively costing them - morale bleeding, product they cannot store, a stash drawing
    /// the law - ahead of what merely would pay.
    /// </summary>
    public IReadOnlyList<NextMoveResponse> NextMoves(Player player, double heat)
    {
        var moves = new List<(int Rank, NextMoveResponse Move)>();
        var report = economy.GetCrewReport(player);
        var capacity = hideouts.CapacityFor(player.Hideout);
        var funds = player.Cash + player.BankCash;

        void Add(int rank, string label, string why, string page, long cost = 0)
            => moves.Add((rank, new NextMoveResponse(label, why, page, cost, rank <= 20)));

        // Bleeding first. These are not opportunities, they are things quietly taking money off the
        // table every shift the player works.
        var unarmed = Math.Max(0, player.Thugs - player.Weapons);
        if (unarmed > 0)
        {
            var cost = (long)unarmed * _options.WeaponPrice;
            Add(10, $"Arm {unarmed:N0} thug{(unarmed == 1 ? string.Empty : "s")}",
                "Thugs without weapons drag morale down every shift, and a low-morale crew earns less and starts walking out.",
                "market", cost);
        }

        var unmanaged = report.UnmanagedHoes;
        if (unmanaged > 0)
        {
            Add(11, "Hire a pimp",
                $"{unmanaged:N0} hoe{(unmanaged == 1 ? " is" : "s are")} beyond what your pimps can manage, which costs morale every shift until it is fixed.",
                "crew", _options.Crew.HirePimpCost);
        }

        if (player.Condoms < report.CondomsNeededForMaxStreetAction || player.Beer < report.BeerNeededForMaxStreetAction)
        {
            Add(12, "Restock supplies",
                $"A full shift needs {report.CondomsNeededForMaxStreetAction:N0} condoms and {report.BeerNeededForMaxStreetAction:N0} beer. Working short of them costs morale.",
                "market", report.SupplyCostForMaxStreetAction);
        }

        var morale = Math.Min(player.HoeHappiness, player.ThugHappiness);
        if (morale < _options.Morale.DesertionThreshold + 15)
        {
            Add(13, "Give the crew a night off",
                $"Morale is down to {morale:N0}. Below {_options.Morale.DesertionThreshold:N0} they start leaving.",
                "crew", _options.Morale.HqRestCashPerCrew * (player.Pimps + player.Hoes + player.Thugs));
        }

        if (heat > _options.Hideout.HeatBustFloor)
        {
            Add(14, "Sell down, or lie low",
                $"You are drawing enough notice to be raided. Heat falls {_options.Hideout.HeatDecayPerHour:N0} an hour on its own, and selling stock takes it off faster.",
                "market");
        }

        // Then what is going to waste: a full room earns nothing, and turns evaporate at the cap.
        if (player.Weed >= capacity.MaxWeed || player.Coke >= capacity.MaxCoke)
        {
            Add(20, "Sell product",
                "Your store is full, so anything else you make or bring home is dumped on arrival.",
                "market");
        }

        if (player.Turns >= _options.MaxTurns)
        {
            Add(21, "Spend your turns",
                "Your bank is full, so every turn you earn from here is thrown away until you spend some.",
                "street");
        }

        // Then what would pay. The first lab is the single best purchase in the early game and the one
        // nothing in the old panel ever mentioned.
        if ((player.Hideout?.WeedLabLevel ?? 0) == 0
            && hideouts.NextUpgrade(player.Hideout, "weedlab") is { TierLocked: false } lab
            && funds >= lab.Cost)
        {
            Add(30, "Build the weed lab",
                "It lifts what every production turn yields and makes weed on its own while you are away. The cheapest thing that earns without you.",
                "hideout", lab.Cost);
        }

        if (player.Cash > capacity.MaxCash * 3 / 4 && player.Cash > 0)
        {
            Add(31, "Bank your cash",
                "Cash on hand is what a raid takes. Money in the bank cannot be stolen.",
                "market");
        }

        if (player.Turns >= _options.MaxActionTurns)
        {
            Add(32, "Work the streets",
                $"{player.Turns:N0} turns ready. This is where the money comes from early on.",
                "street");
        }

        var next = NextAffordableRoom(player, funds);
        if (next is not null)
            Add(33, $"Upgrade the {next.Value.Label}", next.Value.Why, "hideout", next.Value.Cost);

        if (player.Turns <= 0)
        {
            var rate = _options.TurnsPerTickFor(player);
            var boosted = rate > _options.TurnsPerTick
                ? " You are still small, so they come back faster than they will later."
                : string.Empty;
            Add(90, "Wait for turns",
                $"You earn {rate:N0} every {_options.TurnTickMinutes:N0} minutes whether you are here or not, and labs and ground keep working.{boosted}",
                "overview");
        }

        return moves.OrderBy(x => x.Rank).Select(x => x.Move).Take(4).ToList();
    }

    /// <summary>
    /// The opening ladder, each rung a verb the game never otherwise introduces.
    ///
    /// Read from state and history rather than stored as flags. A player who built a lab has a lab,
    /// and a player who has sold something has a SALE in their log: asking the world what happened
    /// cannot drift out of step with it the way a checklist column would, and needs no migration.
    /// </summary>
    public IReadOnlyList<ObjectiveResponse> Objectives(Player player, IReadOnlyCollection<string> actionsTaken)
    {
        var hideout = player.Hideout;
        var tierTwo = _options.Hideout.Tiers.FirstOrDefault(x => x.Level == 2);

        return
        [
            Step("Work the streets", "Spend turns for cash, and pick up crew while you are out.",
                "street", actionsTaken.Contains("STREET")),
            Step("Bank what you make", "Cash on hand is stolen in a raid. Cash in the bank is not.",
                "market", actionsTaken.Contains("BANK")),
            Step("Build the weed lab", "The cheapest thing you can own that earns while you are away.",
                "hideout", (hideout?.WeedLabLevel ?? 0) > 0),
            Step("Run a production shift", "Turns and materials into product, at better odds than the street.",
                "street", actionsTaken.Contains("PRODUCTION")),
            Step("Sell what you made", "Product is only worth something once somebody buys it.",
                "market", actionsTaken.Contains("SALE")),
            Step("Arm every thug", "An unarmed thug is a morale leak and a lost fight.",
                "market", player.Thugs > 0 && player.Weapons >= player.Thugs),
            Step("Hire a second pimp", "Each one manages ten hoes. Past that the whole crew sours.",
                "crew", player.Pimps >= 2),
            Step("Deepen the store", "A full room throws away everything you make after it.",
                "hideout", (hideout?.StorageLevel ?? 1) > 1),
            Step($"Move up to the {tierTwo?.Name ?? "Warehouse"}", "Stills, mix houses and mule runs all live behind it.",
                "hideout", (hideout?.Tier ?? 1) >= 2)
        ];

        static ObjectiveResponse Step(string label, string why, string page, bool done)
            => new(label, why, page, done);
    }

    /// <summary>The cheapest room they could buy right now, and why it is worth the money.</summary>
    private (string Label, string Why, long Cost)? NextAffordableRoom(Player player, long funds)
    {
        (string Room, string Label, string Why)[] rooms =
        [
            ("storage", "storage room", "A deeper room is what lets a lab run all night without spilling."),
            ("safe", "safe", "A bigger safe is more cash a raid cannot reach."),
            ("cokelab", "coke lab", "Coke is worth several times what weed is, and the lab makes it while you are away.")
        ];

        foreach (var (room, label, why) in rooms)
            if (hideouts.NextUpgrade(player.Hideout, room) is { TierLocked: false } next && funds >= next.Cost)
                return (label, why, next.Cost);

        return null;
    }
}
