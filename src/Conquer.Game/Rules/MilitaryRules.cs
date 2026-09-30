using Conquer.Game.World;

namespace Conquer.Game.Rules;

/// <summary>Tuning numbers for armies, combat, supply and command. Combat rates are per hour; the rest per day.</summary>
public static class MilitaryRules
{
    /// <summary>Battalions in one combat unit at most: a division.</summary>
    public const int MaxBattalionsPerUnit = 12;
    /// <summary>Ships in one fleet at most.</summary>
    public const int MaxShipsPerFleet = 10;
    /// <summary>A fleet in a port with a dry dock recovers organisation and crews this many times faster.</summary>
    public const double DryDockRepair = 2;
    /// <summary>HQs travel on horseback, faster than infantry.</summary>
    public const double HeadquartersSpeed = 1.5;

    // Combat, every hour
    /// <summary>Organisation a side loses per point of enemy fire, shared among its battalions.</summary>
    public const double OrganisationDamage = 0.3;
    /// <summary>Men a side loses per point of enemy fire, shared among its battalions.</summary>
    public const double StrengthDamage = 0.1;
    /// <summary>A regiment breaks and leaves the fight below this share of its organisation.</summary>
    public const double BreakingOrganisation = 0.1;
    /// <summary>Fire of each side varies this much each hour, up and down.</summary>
    public const double CombatRandomness = 0.2;
    /// <summary>Horses and chariots attack at this share of their strength in rough terrain.</summary>
    public const double MountedRoughTerrainAttack = 0.5;

    /// <summary>Extra fire of a fully experienced (elite) battalion.</summary>
    public const double ExperienceBonus = 0.5;
    /// <summary>Experience a battalion gains each hour it fights, less as it nears the top.</summary>
    public const double ExperiencePerBattleHour = 0.003;
    /// <summary>Artillery and aircraft take this share of the fire the front line takes.</summary>
    public const double SupportExposure = 0.25;
    /// <summary>Extra fire for each kind of troop beyond the first among those fighting (infantry, cavalry, artillery, armour, aircraft)...</summary>
    public const double CombinedArmsBonus = 0.1;
    /// <summary>...up to this much.</summary>
    public const double MaxCombinedArmsBonus = 0.3;

    /// <summary>
    /// Battalions of each side that fit in the front line of a province: the rest wait in reserve, out of the
    /// fight. Artillery and aircraft fire from behind, up to half as many again.
    /// </summary>
    public static int FrontWidth(Biome biome) => biome switch
    {
        Biome.HighMountains => 4,
        Biome.Mountains => 6,
        Biome.TemperateForest or Biome.Taiga or Biome.TropicalForest or Biome.Wetland => 8,
        Biome.Hills => 9,
        _ => 12,
    };

    /// <summary>Defenders' fire is multiplied by this in hills, mountains, forests and marshes.</summary>
    public static double TerrainDefense(Biome biome) => biome switch
    {
        Biome.Hills => 1.25,
        Biome.Mountains or Biome.HighMountains => 1.5,
        Biome.TemperateForest or Biome.Taiga or Biome.TropicalForest or Biome.Wetland => 1.2,
        _ => 1,
    };

    public static bool IsRough(Biome biome) => TerrainDefense(biome) > 1;

    /// <summary>Defenders of a province with a river fire this much harder: the attackers have to cross it.</summary>
    public const double RiverDefense = 1.25;

    /// <summary>How much harder a province is to take: its terrain, and its river if it has one.</summary>
    public static double DefenseMultiplier(Province province) => TerrainDefense(province.Biome) * (province.HasRiver ? RiverDefense : 1);

    // Chain of command
    /// <summary>Combat and recovery bonus of a regiment whose own HQ is within range.</summary>
    public const double CommandBonus = 0.1;
    /// <summary>Extra bonus for each higher HQ in an unbroken chain above it.</summary>
    public const double HigherCommandBonus = 0.05;

    // Officers
    /// <summary>Gold it costs to recruit an officer into the reserve.</summary>
    public const double OfficerCost = 40;
    /// <summary>Longest name a player may give a unit.</summary>
    public const int MaxUnitNameLength = 30;

    // Supply and recovery, every day
    /// <summary>Supply reaches this many hours of marching from a city through territory the nation controls.</summary>
    public const double SupplyRangeHours = 24 * 15;
    public const double OutOfSupplyEfficiency = 0.75;
    /// <summary>Share of their full organisation regiments regain each day in supply and out of combat.</summary>
    public const double OrganisationRecovery = 0.2;
    /// <summary>Share of full organisation lost each day without supply.</summary>
    public const double OutOfSupplyOrganisationLoss = 0.05;
    /// <summary>Share of full strength lost each day without supply (hunger, desertion).</summary>
    public const double OutOfSupplyAttrition = 0.01;
    /// <summary>Share of full strength a battalion in supply gets back each day, taken from the capital's people.</summary>
    public const double ReinforcementRate = 0.05;

    // Upkeep, every day
    /// <summary>Share of its gold cost each battalion, ship or HQ costs every day.</summary>
    public const double UpkeepGoldShare = 0.02;
    /// <summary>Share of its other materials (iron, coal, oil…; not wood) it uses up every day.</summary>
    public const double UpkeepResourceShare = 0.01;
    /// <summary>Share of full organisation lost each day the army goes unpaid...</summary>
    public const double UnpaidOrganisationLoss = 0.1;
    /// <summary>...and share of the men who desert.</summary>
    public const double UnpaidDesertion = 0.02;

    /// <summary>Mood of a province while an enemy occupies it.</summary>
    public const double OccupiedMood = -30;
}
