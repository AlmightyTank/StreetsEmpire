namespace StreetEmpire.Api.Models;

/// <summary>
/// A pile of goods somebody can hold, wherever it is standing.
///
/// There are two of these now and there will be more. A player carries one in their pockets and their
/// hideout keeps another on its shelves, and the difference between them is entirely a matter of where
/// they are: what a unit of coke is, what stretching does to it, and which gun comes off the rack
/// first when a shelf overflows are the same questions in both. An interface rather than two copies of
/// the arithmetic, because <see cref="Services.TradeGoods"/> is the one place that knows how a good's
/// key maps to the column holding it, and it has to be able to say that about either pile.
///
/// <see cref="Player"/> implements this with the columns it has always had, so every existing caller
/// that says <c>player.Weed</c> still means what it meant. What changed is that the same code can now
/// be pointed at <see cref="Hideout.Storage"/> instead, which is what makes travel possible at all.
/// </summary>
public interface IStash
{
    int Condoms { get; set; }
    int Beer { get; set; }
    int Medicine { get; set; }
    int Poison { get; set; }
    int Weed { get; set; }
    int Coke { get; set; }
    int Moonshine { get; set; }
    int Cut { get; set; }

    /// <summary>How much of the coke pile is actually coke. See <see cref="Player.CokePurity"/>.</summary>
    double CokePurity { get; set; }

    /// <summary>The gun rack as one value.</summary>
    Armoury Armoury { get; set; }

    /// <summary>How many guns there are, of any kind.</summary>
    int Weapons { get; }

    /// <summary>Blends arriving coke into the pile rather than counting it on to the end.</summary>
    void AddCoke(int units, double purity);

    /// <summary>Puts guns on the rack.</summary>
    void AddWeapons(string tier, int count);

    /// <summary>Takes guns off the rack, cheapest first, and reports what actually went.</summary>
    Armoury RemoveWeapons(int count);
}

/// <summary>
/// Goods sitting somewhere that is not a player's pockets - today, the hideout's storage room.
///
/// An owned type rather than a table of its own because a stash has no life apart from the thing
/// holding it: there is no question anybody asks of a store room that does not start with which
/// hideout it is in, and a row that can only ever be reached through its owner is a column group
/// wearing a primary key.
/// </summary>
public sealed class Stash : IStash
{
    public int Condoms { get; set; }
    public int Beer { get; set; }
    public int Medicine { get; set; }
    public int Poison { get; set; }
    public int Weed { get; set; }
    public int Coke { get; set; }
    public int Moonshine { get; set; }
    public int Cut { get; set; }

    public double CokePurity { get; set; } = 1;

    public int Pistols { get; set; }
    public int Shotguns { get; set; }
    public int Smgs { get; set; }
    public int Rifles { get; set; }

    public Armoury Armoury
    {
        get => new(Pistols, Shotguns, Smgs, Rifles);
        set
        {
            Pistols = Math.Max(0, value.Pistols);
            Shotguns = Math.Max(0, value.Shotguns);
            Smgs = Math.Max(0, value.Smgs);
            Rifles = Math.Max(0, value.Rifles);
        }
    }

    public int Weapons => Pistols + Shotguns + Smgs + Rifles;

    public void AddCoke(int units, double purity)
    {
        if (units <= 0) return;
        var total = Coke + units;
        CokePurity = Math.Clamp((Coke * CokePurity + units * Math.Clamp(purity, 0, 1)) / total, 0, 1);
        Coke = total;
    }

    public void AddWeapons(string tier, int count)
    {
        if (count <= 0) return;
        Armoury = Armoury.Add(tier, count);
    }

    public Armoury RemoveWeapons(int count)
    {
        var taken = Armoury.WorstFirst(count);
        Armoury -= taken;
        return taken;
    }

    /// <summary>Whether there is anything here at all. Read before a raid bothers describing a haul.</summary>
    public bool Any => Condoms > 0 || Beer > 0 || Medicine > 0 || Poison > 0
                       || Weed > 0 || Coke > 0 || Moonshine > 0 || Cut > 0 || Weapons > 0;
}
