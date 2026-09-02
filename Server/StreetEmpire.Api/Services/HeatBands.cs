namespace StreetEmpire.Api.Services;

/// <summary>How much attention is on a house, in the four words the game already used for it.</summary>
public enum HeatBand
{
    Quiet,
    Noticed,
    Watched,
    Hunted
}

/// <summary>
/// Where a heat number sits, and what that costs beyond the odds.
///
/// The bands existed already, as a switch in the response mapper that turned a double into a word for
/// the status strip - and nothing at all read them, so "Hunted" was a label rather than a state. The
/// number decided one thing: the chance of a raid per hour. Everything else about being the most
/// watched house in town was identical to being the quietest one that happened to be unlucky.
///
/// They are here now because more than the label reads them: how much of a stash a raid walks off
/// with, and how many rooms it puts through a wall on the way out. Same thresholds, one authority, so
/// the word on the strip and the size of the boot through the door can never disagree.
/// </summary>
public static class HeatBands
{
    public static HeatBand Of(double heat, HideoutOptions options)
    {
        var floor = Math.Max(1, options.HeatBustFloor);
        return heat switch
        {
            var value when value <= floor => HeatBand.Quiet,
            var value when value <= floor * Math.Max(1, options.WatchedHeatMultiple) => HeatBand.Noticed,
            var value when value <= floor * Math.Max(1, options.HuntedHeatMultiple) => HeatBand.Watched,
            _ => HeatBand.Hunted
        };
    }

    public static string Label(HeatBand band) => band switch
    {
        HeatBand.Quiet => "Quiet",
        HeatBand.Noticed => "Noticed",
        HeatBand.Watched => "Watched",
        _ => "Hunted"
    };

    public static string Label(double heat, HideoutOptions options) => Label(Of(heat, options));

    /// <summary>
    /// The share of every contraband pile a raid at this band walks off with.
    ///
    /// A flat half was the same raid whether the law had noticed you last night or had been parked
    /// outside for a week, which is the version of heat where the number only ever bought a dice
    /// roll. They come better prepared for a house they have been watching.
    /// </summary>
    public static double SeizedPercent(HeatBand band, HideoutOptions options)
        => Math.Clamp(band == HeatBand.Hunted ? options.SeizedPercentWhenHunted : options.SeizedPercent, 0, 1);

    /// <summary>
    /// How many rooms a raid at this band puts out of action.
    ///
    /// Nothing under Watched. A player who has been noticed once loses stock and a fine, which is the
    /// risk they took by holding it; the room-breaking is reserved for the band a player has to work
    /// at to reach, because it is the part that is still costing them tomorrow.
    /// </summary>
    public static int RoomsWrecked(HeatBand band, HideoutOptions options) => band switch
    {
        HeatBand.Watched => Math.Max(0, options.RoomsWreckedWhenWatched),
        HeatBand.Hunted => Math.Max(0, options.RoomsWreckedWhenHunted),
        _ => 0
    };
}
