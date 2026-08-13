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
    // Starting supplies fill a level 1 storage room exactly, so a new player is never over capacity.
    public int StartingCondoms { get; set; } = 17;
    public int StartingBeer { get; set; } = 10;
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
    public CombatOptions Combat { get; set; } = new();
    public HideoutOptions Hideout { get; set; } = new();
    public PimpOptions Pimps { get; set; } = new();
    public AntiFarmOptions AntiFarm { get; set; } = new();
    public WorldNewsOptions WorldNews { get; set; } = new();
}

/// <summary>
/// Weights behind attack and defence strength. Tuned so a defender holds at equal armed crew while an
/// attacker with roughly 12-20% more armed thugs gets through: wide enough that defence is worth
/// investing in, narrow enough that attacking is a real option rather than a losing bet.
///
/// Before this pass, defence earned 24 per armed thug against attack's 20 and counted morale twice as
/// heavily, so an attacker needed about 1.4x the crew and bots correctly refused nearly every fight.
/// </summary>
/// <summary>
/// What happens inside a fight round. These were hardcoded, which made the round outcome impossible to
/// tune and hid two problems: a 10% band counted as a draw, so a modest edge produced six drawn rounds
/// and no result, and 12-22 morale damage barely broke a 95-morale defender inside the round cap. Both
/// meant an attacker could win on paper and still come home with nothing.
/// </summary>
public sealed class CombatRoundOptions
{
    /// <summary>Within this fraction of each other, a round is a draw and neither side breaks.</summary>
    public double ClosePercent { get; set; } = 0.06;
    public int CloseMinimumGap { get; set; } = 8;

    /// <summary>Morale both sides lose in a drawn round.</summary>
    public int DrawMoraleLossMin { get; set; } = 6;
    public int DrawMoraleLossMax { get; set; } = 12;

    /// <summary>
    /// Morale the losing side of a round gives up. Left at the original range on purpose: raising it to
    /// 16-26 alongside the narrower draw band turned every simulated fight into an attacker victory,
    /// because a side that starts losing rounds also loses crew and morale and cannot recover. The band
    /// was the real cause of drawn-out stalemates, so only the band moved.
    /// </summary>
    public int LosingSideMoraleLossMin { get; set; } = 12;
    public int LosingSideMoraleLossMax { get; set; } = 22;

    /// <summary>Morale the winning side of a round still gives up.</summary>
    public int WinningSideMoraleLossMin { get; set; } = 4;
    public int WinningSideMoraleLossMax { get; set; } = 9;

    public double CrewLossRate { get; set; } = 0.06;
    public double WeaponLossRate { get; set; } = 0.04;
    public double LossRollChance { get; set; } = 0.55;
}

public sealed class CombatPowerOptions
{
    public int ThugAttack { get; set; } = 13;
    public int ArmedThugAttack { get; set; } = 9;
    public int PimpAttack { get; set; } = 2;
    /// <summary>
    /// Close to the defender's weight on purpose. A crew's morale matters wherever it fights, and a
    /// large gap here swamps small crews: at 0.75 an attacker needed 40% more thugs to match five
    /// defenders, because the flat morale difference dwarfed a five-thug base.
    /// </summary>
    public double MoraleAttackWeight { get; set; } = 0.9;

    public int ThugDefence { get; set; } = 14;
    public int ArmedThugDefence { get; set; } = 9;
    public int PimpDefence { get; set; } = 3;
    public double MoraleDefenceWeight { get; set; } = 1;
}

public sealed class AntiFarmOptions
{
    /// <summary>
    /// Below this net worth a player cannot be attacked at all. Self-limiting as a shield: bank cash
    /// counts toward net worth, so a player cannot hide wealth to stay permanently untouchable.
    /// </summary>
    public long MinDefenderNetWorth { get; set; } = 25_000;

    /// <summary>An attacker may not hit a target worth less than their own net worth over this ratio.</summary>
    public double MaxNetWorthRatio { get; set; } = 5;

    /// <summary>How far back repeat victories and hits are counted.</summary>
    public int RepeatWindowHours { get; set; } = 24;

    /// <summary>Share of the haul lost per prior victory against the same defender in the window.</summary>
    public double LootDecayPerRepeat { get; set; } = 0.4;

    /// <summary>Floor on the decay, so a repeat attack is pointless rather than forbidden.</summary>
    public double MinLootMultiplier { get; set; } = 0.1;

    /// <summary>Extra protection per hit the defender already took in the window, as a multiple of the base.</summary>
    public double ProtectionEscalationPerHit { get; set; } = 0.5;

    public int MaxProtectionMinutes { get; set; } = 360;

    /// <summary>
    /// How many attacks may be in flight against one defender at once. Escalating protection is
    /// reactive: it is set when a mission finishes, so without this cap any number of attackers can
    /// launch simultaneously and every one lands before the first shield exists.
    /// </summary>
    public int MaxIncomingAttacks { get; set; } = 2;
}

public sealed class PimpOptions
{
    public double StartingLoyalty { get; set; } = 100;

    /// <summary>Loyalty lost by the pimp who led a defeat, and gained by one who led a win.</summary>
    public double DefeatLoyaltyPenalty { get; set; } = 12;
    public double StandstillLoyaltyPenalty { get; set; } = 4;
    public double VictoryLoyaltyGain { get; set; } = 6;

    /// <summary>Per turn of street work, applied while crew morale sits below the threshold.</summary>
    public double LowMoraleLoyaltyPenaltyPerTurn { get; set; } = 0.35;
    public double LowMoraleThreshold { get; set; } = 45;
    public double PassiveRecoveryPerTick { get; set; } = 0.5;
    public double RestRecovery { get; set; } = 6;
    public double PartyRecovery { get; set; } = 9;

    /// <summary>Below this loyalty a pimp may walk out after street work.</summary>
    public double WalkOutThreshold { get; set; } = 25;
    public double MaxWalkOutChance { get; set; } = 0.15;

    /// <summary>Chance the commanding pimp dies when the attack is beaten.</summary>
    public double CommanderDeathChanceOnDefeat { get; set; } = 0.20;

    /// <summary>Chance a pimp at home dies when a defence is broken.</summary>
    public double DefenderDeathChanceOnLoss { get; set; } = 0.15;

    /// <summary>Per-pimp bonus rolled at hire, in percent.</summary>
    public int MinBonusPercent { get; set; } = 3;
    public int MaxBonusPercent { get; set; } = 8;

    /// <summary>
    /// Ceilings on the stacked bonus from pimps at home. Six Hustlers at 8% would otherwise be a 48%
    /// income swing, which is no longer a small bonus.
    /// </summary>
    public int MaxStreetBonusPercent { get; set; } = 20;
    public int MaxDefenceBonusPercent { get; set; } = 20;
}

/// <summary>
/// Hideout tuning tables. These start empty on purpose: the configuration binder appends to a
/// pre-populated <see cref="List{T}"/> instead of replacing it, so shipping defaults in the
/// initializers would merge them with appsettings and let the stale default win the level lookup.
/// Call <see cref="ApplyDefaultsWhereEmpty"/> after binding to fill in whatever config omitted.
/// </summary>
public sealed class HideoutOptions
{
    public List<HideoutTierOptions> Tiers { get; set; } = [];
    public List<StorageLevelOptions> Storage { get; set; } = [];
    public List<SafeLevelOptions> Safe { get; set; } = [];
    public List<LabLevelOptions> WeedLab { get; set; } = [];
    public List<LabLevelOptions> CokeLab { get; set; } = [];

    /// <summary>
    /// How much passive lab output can pile up while a player is away. Past this the labs sit idle, so
    /// the hideout is a reason to come back rather than a reason to stay gone.
    /// </summary>
    public int MaxOfflineProductionHours { get; set; } = 12;

    public void ApplyDefaultsWhereEmpty()
    {
        // Each tier's crew caps are what the storage level it unlocks is sized against, so a full-length
        // action is always exactly supplyable at the top of a tier and never more than that.
        if (Tiers.Count == 0)
            Tiers =
            [
                new HideoutTierOptions { Level = 1, Name = "Trap House", MaxPimps = 6, MaxHoes = 50, MaxThugs = 25 },
                new HideoutTierOptions { Level = 2, Name = "Row House", MaxPimps = 10, MaxHoes = 85, MaxThugs = 45, UpgradeCost = 200_000, UpgradeTurns = 40, BuildMinutes = 30 },
                new HideoutTierOptions { Level = 3, Name = "Corner Club", MaxPimps = 15, MaxHoes = 130, MaxThugs = 70, UpgradeCost = 600_000, UpgradeTurns = 80, BuildMinutes = 120 },
                new HideoutTierOptions { Level = 4, Name = "Penthouse", MaxPimps = 22, MaxHoes = 200, MaxThugs = 110, UpgradeCost = 1_800_000, UpgradeTurns = 120, BuildMinutes = 360 }
            ];

        // Condoms hold a full 20-turn action at the tier's hoe cap (one per 12 turns each), beer the same
        // for thugs (one per 10), and weapons cover every thug. Weed and coke stay at 2x and 1x the hoe cap.
        if (Storage.Count == 0)
            Storage =
            [
                // Level 1 supplies a fifth of a full-length action at the crew caps: 4 turns of both.
                new StorageLevelOptions { Level = 1, Condoms = 17, Beer = 10, Weapons = 5, Weed = 25, Coke = 10 },
                // Level 2 supplies exactly half a full-length action: 10 turns of both.
                new StorageLevelOptions { Level = 2, Condoms = 42, Beer = 25, Weapons = 12, Weed = 50, Coke = 25, UpgradeCost = 15_000 },
                // Level 3 holds exactly what a full-length action consumes: 84 condoms for 50 hoes at
                // 12 turns each, 50 beer for 25 thugs at 10. It drains the room dry each time.
                new StorageLevelOptions { Level = 3, Condoms = 84, Beer = 50, Weapons = 25, Weed = 100, Coke = 50, UpgradeCost = 50_000 },
                new StorageLevelOptions { Level = 4, MinTier = 2, Condoms = 142, Beer = 90, Weapons = 45, Weed = 170, Coke = 85, UpgradeCost = 150_000 },
                new StorageLevelOptions { Level = 5, MinTier = 3, Condoms = 217, Beer = 140, Weapons = 70, Weed = 260, Coke = 130, UpgradeCost = 400_000 },
                new StorageLevelOptions { Level = 6, MinTier = 4, Condoms = 334, Beer = 220, Weapons = 110, Weed = 400, Coke = 200, UpgradeCost = 1_000_000 }
            ];

        if (Safe.Count == 0)
            Safe =
            [
                new SafeLevelOptions { Level = 1, MaxCash = 50_000 },
                new SafeLevelOptions { Level = 2, MaxCash = 100_000, UpgradeCost = 40_000 },
                new SafeLevelOptions { Level = 3, MinTier = 2, MaxCash = 350_000, UpgradeCost = 120_000 },
                new SafeLevelOptions { Level = 4, MinTier = 3, MaxCash = 1_000_000, UpgradeCost = 300_000 },
                new SafeLevelOptions { Level = 5, MinTier = 4, MaxCash = 3_000_000, UpgradeCost = 900_000 }
            ];

        // PassivePerHour is deliberately below what the same lab yields through production turns: about
        // half a day of accrual matches one full-length production run, so being away is worth something
        // without being worth more than playing.
        if (WeedLab.Count == 0)
            WeedLab =
            [
                new LabLevelOptions { Level = 1, YieldBonusPercent = 25, PassivePerHour = 2, UpgradeCost = 10_000 },
                new LabLevelOptions { Level = 2, YieldBonusPercent = 60, PassivePerHour = 4, UpgradeCost = 30_000 },
                new LabLevelOptions { Level = 3, YieldBonusPercent = 110, PassivePerHour = 7, UpgradeCost = 75_000 },
                new LabLevelOptions { Level = 4, MinTier = 3, YieldBonusPercent = 170, PassivePerHour = 11, UpgradeCost = 250_000 },
                new LabLevelOptions { Level = 5, MinTier = 4, YieldBonusPercent = 240, PassivePerHour = 16, UpgradeCost = 700_000 }
            ];

        if (CokeLab.Count == 0)
            CokeLab =
            [
                new LabLevelOptions { Level = 1, YieldBonusPercent = 25, PassivePerHour = 1, UpgradeCost = 25_000 },
                new LabLevelOptions { Level = 2, YieldBonusPercent = 60, PassivePerHour = 2, UpgradeCost = 60_000 },
                new LabLevelOptions { Level = 3, YieldBonusPercent = 110, PassivePerHour = 3, UpgradeCost = 150_000 },
                new LabLevelOptions { Level = 4, MinTier = 3, YieldBonusPercent = 170, PassivePerHour = 5, UpgradeCost = 450_000 },
                new LabLevelOptions { Level = 5, MinTier = 4, YieldBonusPercent = 240, PassivePerHour = 7, UpgradeCost = 1_200_000 }
            ];
    }
}

public sealed class HideoutTierOptions
{
    public int Level { get; set; } = 1;
    public string Name { get; set; } = "Trap House";
    public int MaxPimps { get; set; }
    public int MaxHoes { get; set; }
    public int MaxThugs { get; set; }

    /// <summary>What moving up to this tier costs. Tier 1 is where everyone starts, so it costs nothing.</summary>
    public long UpgradeCost { get; set; }
    public int UpgradeTurns { get; set; }

    /// <summary>How long the build takes once paid for. The hideout keeps its old caps until it finishes.</summary>
    public int BuildMinutes { get; set; }
}

public sealed class StorageLevelOptions
{
    public int Level { get; set; }
    /// <summary>The hideout tier this level needs. A bigger room needs a bigger building to put it in.</summary>
    public int MinTier { get; set; } = 1;
    public int Condoms { get; set; }
    public int Beer { get; set; }
    public int Weapons { get; set; }
    public int Weed { get; set; }
    public int Coke { get; set; }
    public long UpgradeCost { get; set; }
}

public sealed class SafeLevelOptions
{
    public int Level { get; set; }
    public int MinTier { get; set; } = 1;
    public long MaxCash { get; set; }
    public long UpgradeCost { get; set; }
}

public sealed class LabLevelOptions
{
    public int Level { get; set; }
    public int MinTier { get; set; } = 1;
    public int YieldBonusPercent { get; set; }

    /// <summary>Units the lab makes on its own each hour, capped by storage and by offline hours.</summary>
    public int PassivePerHour { get; set; }
    public long UpgradeCost { get; set; }
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

    /// <summary>
    /// How far back the morale trend arrow looks. Long enough that a single action does not define the
    /// direction, short enough that yesterday's slump is not still showing.
    /// </summary>
    public int TrendWindowHours { get; set; } = 3;

    /// <summary>Movement smaller than this reads as steady, so the arrow does not flicker on drift.</summary>
    public double TrendFlatBand { get; set; } = 1;

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
    public double PassiveRecoveryPerTick { get; set; } = 0.35;
    public int HqRestTurnCost { get; set; } = 4;
    public long HqRestCashPerCrew { get; set; } = 75;
    public double HqRestMoraleGain { get; set; } = 8;
    public int HqPartyTurnCost { get; set; } = 2;
    public long HqPartyCashPerCrew { get; set; } = 45;
    public int HqPartyBeerPerThug { get; set; } = 5;
    public int HqPartyWeedPerHoes { get; set; } = 10;
    public double HqPartyHoeMoraleGain { get; set; } = 12;
    public double HqPartyThugMoraleGain { get; set; } = 10;
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

public sealed class CombatOptions
{
    public CombatPowerOptions Power { get; set; } = new();
    public CombatRoundOptions Round { get; set; } = new();

    public int AttackTurnCost { get; set; } = 10;
    public int AttackCooldownMinutes { get; set; } = 30;
    public int AttackTravelSecondsMin { get; set; } = 75;
    public int AttackTravelSecondsMax { get; set; } = 180;
    public int ReturnTravelSecondsMin { get; set; } = 60;
    public int ReturnTravelSecondsMax { get; set; } = 150;
    public int FightRoundSeconds { get; set; } = 20;
    public int MaxFightRounds { get; set; } = 6;
    public int MaxActiveAttackMissions { get; set; } = 2;
    public double MoraleBreakThreshold { get; set; } = 5;
    public int DefenderProtectionMinutes { get; set; } = 60;
    public double PowerRandomnessPercent { get; set; } = 0.15;
    public double MinCashLootPercent { get; set; } = 0.05;
    public double MaxCashLootPercent { get; set; } = 0.20;
    public double MinProductLootPercent { get; set; } = 0.05;
    public double MaxProductLootPercent { get; set; } = 0.15;
    public double WinnerCrewLossPercent { get; set; } = 0.03;
    public double LoserCrewLossPercent { get; set; } = 0.10;
    public double WeaponLossPercent { get; set; } = 0.08;
    public double AttackerDefeatThugMoralePenalty { get; set; } = 8;
    public double AttackerDefeatHoeMoralePenalty { get; set; } = 3;
    public double AttackerStandstillThugMoralePenalty { get; set; } = 3;
    public double AttackerVictoryThugMoraleGain { get; set; } = 4;
    public double AttackerVictoryHoeMoraleGain { get; set; } = 1.5;
    public double DefenderDefeatThugMoralePenalty { get; set; } = 6;
    public double DefenderDefeatHoeMoralePenalty { get; set; } = 4;
    public double DefenderVictoryThugMoraleGain { get; set; } = 3;
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
