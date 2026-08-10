namespace StreetEmpire.Api.Services;

public sealed class GameOptions
{
    public int TurnsPerTick { get; set; } = 2;
    public int TurnTickMinutes { get; set; } = 10;
    public int MaxTurns { get; set; } = 200;
    public int StartingTurns { get; set; } = 100;
    public int MaxActionTurns { get; set; } = 20;

    public long StartingCash { get; set; } = 5_000;
    public long StartingBankCash { get; set; } = 0;
    public int StartingPimps { get; set; } = 1;
    public int StartingHoes { get; set; } = 3;
    public int StartingThugs { get; set; } = 1;
    public int StartingCondoms { get; set; } = 25;
    public int StartingBeer { get; set; } = 12;
    public int StartingWeapons { get; set; } = 1;
    public int StartingHoeCutPercent { get; set; } = 30;

    public int CondomPrice { get; set; } = 10;
    public int BeerPrice { get; set; } = 15;
    public int WeaponPrice { get; set; } = 500;
    public int WeedSellPrice { get; set; } = 40;
    public int CokeSellPrice { get; set; } = 150;

    public int PimpNetWorth { get; set; } = 1_000;
    public int HoeNetWorth { get; set; } = 550;
    public int ThugNetWorth { get; set; } = 1_250;
    public int WeedNetWorth { get; set; } = 30;
    public int CokeNetWorth { get; set; } = 120;

    public StreetActionOptions StreetAction { get; set; } = new();
    public ProductionOptions Production { get; set; } = new();
    public MoraleOptions Morale { get; set; } = new();
    public CrewOptions Crew { get; set; } = new();
}

public sealed class BotAutomationOptions
{
    public bool Enabled { get; set; } = false;
    public int TickSeconds { get; set; } = 60;
    public int RoundsPerTick { get; set; } = 1;
}

public sealed class StreetActionOptions
{
    public int BaseGrossPerTurn { get; set; } = 35;
    public RangeOptions HoeGrossPerTurn { get; set; } = new(18, 30);
    public RangeOptions PimpGrossPerTurn { get; set; } = new(4, 10);
    public double PimpRecruitChance { get; set; } = 0.012;
    public double HoeRecruitChance { get; set; } = 0.12;
    public double ThugRecruitChance { get; set; } = 0.04;
    public FindTableOptions Finds { get; set; } = new();
}

public sealed class FindTableOptions
{
    public FindOptions Condoms { get; set; } = new(0.06, 1, 3);
    public FindOptions Beer { get; set; } = new(0.05, 1, 2);
    public FindOptions Weed { get; set; } = new(0.07, 1, 3);
    public FindOptions Coke { get; set; } = new(0.018, 1, 1);
}

public sealed class ProductionOptions
{
    public ProductProductionOptions Weed { get; set; } = new(25, 3, 6);
    public ProductProductionOptions Coke { get; set; } = new(80, 1, 3);
}

public sealed class MoraleOptions
{
    public int HoesManagedPerPimp { get; set; } = 10;
    public double TurnsPerCondom { get; set; } = 12;
    public double TurnsPerBeer { get; set; } = 10;
    public double HoeStreetWorkGainPerTurn { get; set; } = 0.14;
    public double ThugStreetWorkGainPerTurn { get; set; } = 0.12;
    public double HoeCutMoraleScalePerTurn { get; set; } = 0.025;
    public double BaselineHoeCutPercent { get; set; } = 30;
    public double CondomShortagePenalty { get; set; } = 2.25;
    public double BeerShortagePenalty { get; set; } = 2.0;
    public double UnmanagedHoePenalty { get; set; } = 0.20;
    public double UncoveredThugPenalty { get; set; } = 0.35;
    public double DesertionThreshold { get; set; } = 25;
    public double MaxDesertionChance { get; set; } = 0.20;
}

public sealed class CrewOptions
{
    public int MaxCrewTransactionQuantity { get; set; } = 1_000;
    public int HirePimpCost { get; set; } = 2_500;
    public int HireHoeCost { get; set; } = 750;
    public int HireThugCost { get; set; } = 1_500;
    public double MinHoeMoraleToHire { get; set; } = 35;
    public double MinThugMoraleToHire { get; set; } = 35;
    public double FireHoeMoralePenalty { get; set; } = 1.5;
    public double FireThugMoralePenalty { get; set; } = 1.25;
    public double FirePimpHoeMoralePenalty { get; set; } = 2.0;
    public double MaxFireMoralePenalty { get; set; } = 25;
}

public sealed class RangeOptions
{
    public RangeOptions()
    {
    }

    public RangeOptions(int min, int max)
    {
        Min = min;
        Max = max;
    }

    public int Min { get; set; }
    public int Max { get; set; }
}

public sealed class FindOptions
{
    public FindOptions()
    {
    }

    public FindOptions(double chance, int min, int max)
    {
        Chance = chance;
        Min = min;
        Max = max;
    }

    public double Chance { get; set; }
    public int Min { get; set; }
    public int Max { get; set; }
}

public sealed class ProductProductionOptions
{
    public ProductProductionOptions()
    {
    }

    public ProductProductionOptions(int costPerTurn, int unitsMin, int unitsMax)
    {
        CostPerTurn = costPerTurn;
        UnitsMin = unitsMin;
        UnitsMax = unitsMax;
    }

    public int CostPerTurn { get; set; }
    public int UnitsMin { get; set; }
    public int UnitsMax { get; set; }
}
