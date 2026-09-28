using Conquer.Game.World;

namespace Conquer.Game.Rules;

/// <summary>Tuning numbers for armies, combat, supply and command. Combat rates are per hour; the rest per day.</summary>
public static class MilitaryRules
{
    public const int MaxBrigadesPerDivision = 4;
    /// <summary>HQs travel on horseback, faster than infantry.</summary>
    public const double HeadquartersSpeed = 1.5;

    // Combat, every hour
    /// <summary>Organisation a side loses per point of enemy fire, shared among its brigades.</summary>
    public const double OrganisationDamage = 0.3;
    /// <summary>Men a side loses per point of enemy fire, shared among its brigades.</summary>
    public const double StrengthDamage = 0.1;
    /// <summary>A division breaks and leaves the fight below this share of its organisation.</summary>
    public const double BreakingOrganisation = 0.1;
    /// <summary>Fire of each side varies this much each hour, up and down.</summary>
    public const double CombatRandomness = 0.2;
    /// <summary>Horses and chariots attack at this share of their strength in rough terrain.</summary>
    public const double MountedRoughTerrainAttack = 0.5;

    /// <summary>Defenders' fire is multiplied by this in hills, mountains, forests and marshes.</summary>
    public static double TerrainDefense(Biome biome) => biome switch
    {
        Biome.Hills => 1.25,
        Biome.Mountains or Biome.HighMountains => 1.5,
        Biome.TemperateForest or Biome.Taiga or Biome.TropicalForest or Biome.Wetland => 1.2,
        _ => 1,
    };

    public static bool IsRough(Biome biome) => TerrainDefense(biome) > 1;

    // Chain of command
    /// <summary>Combat and recovery bonus of a division whose own HQ is within range.</summary>
    public const double CommandBonus = 0.1;
    /// <summary>Extra bonus for each higher HQ in an unbroken chain above it.</summary>
    public const double HigherCommandBonus = 0.05;

    // Supply and recovery, every day
    /// <summary>Supply reaches this many hours of marching from a city through territory the nation controls.</summary>
    public const double SupplyRangeHours = 24 * 15;
    public const double OutOfSupplyEfficiency = 0.75;
    /// <summary>Share of their full organisation divisions regain each day in supply and out of combat.</summary>
    public const double OrganisationRecovery = 0.2;
    /// <summary>Share of full organisation lost each day without supply.</summary>
    public const double OutOfSupplyOrganisationLoss = 0.05;
    /// <summary>Share of full strength lost each day without supply (hunger, desertion).</summary>
    public const double OutOfSupplyAttrition = 0.01;
    /// <summary>Share of full strength a brigade in supply gets back each day, taken from the capital's people.</summary>
    public const double ReinforcementRate = 0.05;

    /// <summary>Mood of a province while an enemy occupies it.</summary>
    public const double OccupiedMood = -30;
}
