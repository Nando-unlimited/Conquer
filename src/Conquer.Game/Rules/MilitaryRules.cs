using Conquer.Game.World;

namespace Conquer.Game.Rules;

/// <summary>Tuning numbers for armies, combat, supply and command. Combat rates are per hour; the rest per day.</summary>
public static class MilitaryRules
{
    /// <summary>Battalions in a regiment at most; a template designs one regiment.</summary>
    public const int MaxBattalionsPerRegiment = 5;
    /// <summary>Regiments in a brigade at most.</summary>
    public const int MaxRegimentsPerBrigade = 4;
    /// <summary>Brigades and regiments in a division at most...</summary>
    public const int MaxDivisionParts = 5;
    /// <summary>...and its men at full strength.</summary>
    public const int MaxDivisionMen = 10_000;
    /// <summary>Ships in one fleet at most.</summary>
    public const int MaxShipsPerFleet = Military.NavalEchelons.ShipsPerForce;
    /// <summary>A Flota commands fleets this close to its port, and the Armada flotas this close to its own.</summary>
    public const double FleetCommandRangeKm = 2000, NavyCommandRangeKm = 6000;
    /// <summary>Fleets a Flota commands at most.</summary>
    public const int MaxFleetsPerFlota = 5;
    /// <summary>What a fleet gets from its Flota in range, and from the Armada above it.</summary>
    public const double FleetCommandBonus = 0.1, HigherFleetCommandBonus = 0.05;
    /// <summary>Staff of a naval HQ.</summary>
    public const int NavalHeadquartersStaff = 50;
    /// <summary>How much slower a fleet without fuel sails, and the share of its fire it keeps.</summary>
    public const double OutOfFuelSlowdown = 4, OutOfFuelEfficiency = 0.5;
    /// <summary>Oil an escuadrilla burns each day it flies its mission.</summary>
    public const double OilPerFlightDay = 1;
    /// <summary>Planes in an escuadrilla, the basic air unit, and the pieces of equipment it needs.</summary>
    public const int PlanesPerFlight = 6;
    /// <summary>Escuadrillas an airfield holds (a grupo), and each aircraft carrier.</summary>
    public const int FlightsPerAirfield = 18, FlightsPerCarrier = 6;
    /// <summary>A División aérea commands air units based this close to it; the Mando aéreo, divisions this close.</summary>
    public const double AirDivisionRangeKm = 1500, AirCommandRangeKm = 4000;
    /// <summary>Air units its División aérea commands at most.</summary>
    public const int MaxUnitsPerAirDivision = 6;
    /// <summary>What an air unit gets from its División aérea in range, and from the Mando aéreo above it.</summary>
    public const double AirCommandBonus = 0.1, HigherAirCommandBonus = 0.05;
    /// <summary>Staff of an air HQ, from its province and the reserve.</summary>
    public const int AirHeadquartersStaff = 30;
    /// <summary>Damage a bombed building takes per point of harm; at 1 it falls.</summary>
    public const double BuildingDamagePerHarm = 1.0 / 300;
    /// <summary>Share of its damage a building not bombed that day has repaired.</summary>
    public const double BuildingRepairPerDay = 0.02;
    /// <summary>An air mission covers the provinces this close to its target.</summary>
    public const double MissionRadiusKm = 300;
    /// <summary>Ground troops fight up to this much better with the sky theirs, and as much worse with it the enemy's.</summary>
    public const double AirSuperiorityBonus = 0.15;
    /// <summary>The share of the fighters over a province a side needs to rule its sky.</summary>
    public const double AirRuleShare = 0.6;
    /// <summary>Under a sky the enemy rules, troops march this many times slower and their shipments take as much longer.</summary>
    public const double UnderEnemyAirSlowdown = 1.5;
    /// <summary>Share of what is shipped to troops under a sky the enemy rules that does not get through (it goes back).</summary>
    public const double UnderEnemyAirSupplyLoss = 0.25;
    /// <summary>Bombers and attack aircraft under a sky the enemy rules do this share of their harm.</summary>
    public const double UnescortedBomberEffect = 0.5;
    /// <summary>
    /// A wing loses each day incoming / (incoming + AirDefenseWeight * defence * planes share + AirEvasion) of its planes, at most
    /// MaxDailyAirLoss, where incoming is the fire of the enemy wings and anti-air in its area.
    /// </summary>
    public const double AirDefenseWeight = 5, AirEvasion = 20, MaxDailyAirLoss = 0.4;
    /// <summary>A wing with less organisation than this share stays on the ground.</summary>
    public const double MinFlyingOrganisation = 0.2;
    /// <summary>Hours of fire a naval strike deals once a day.</summary>
    public const double NavalStrikeHours = 4;
    /// <summary>A fleet in a port with a dry dock recovers organisation and crews this many times faster.</summary>
    public const double DryDockRepair = 2;
    /// <summary>Share of the newer model's cost a ship's refit costs, in one of the nation's ports.</summary>
    public const double RefitCostShare = 0.5;
    /// <summary>Each advance that studies a battalion (<see cref="Science.TechInfo.FasterTraining"/>) has the barracks train it this much faster.</summary>
    public const double TechTrainingSpeed = 0.25;
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
    /// <summary>Mountain troops fire this many times harder, attacking or defending, in rough terrain.</summary>
    public const double MountainTroopsRoughTerrain = 1.5;
    /// <summary>Share of the men a unit with medics would lose in battle that they save.</summary>
    public const double MedicsSaving = 0.3;
    /// <summary>Anti-air fires this many times harder when the enemy has aircraft in the fight...</summary>
    public const double AntiAirAgainstAircraft = 2;
    /// <summary>...and each of its battalions takes this share off the enemy aircraft's fire...</summary>
    public const double AntiAirShield = 0.25;
    /// <summary>...up to this much.</summary>
    public const double MaxAntiAirShield = 0.75;

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
        Biome.HighMountains or Biome.Peaks => 4,
        Biome.Mountains => 6,
        Biome.TemperateForest or Biome.Taiga or Biome.TropicalForest or Biome.Wetland => 8,
        Biome.Hills => 9,
        _ => 12,
    };

    /// <summary>Defenders' fire is multiplied by this in hills, mountains, forests and marshes.</summary>
    public static double TerrainDefense(Biome biome) => biome switch
    {
        Biome.Hills => 1.25,
        Biome.Mountains or Biome.HighMountains or Biome.Peaks => 1.5,
        Biome.TemperateForest or Biome.Taiga or Biome.TropicalForest or Biome.Wetland => 1.2,
        _ => 1,
    };

    public static bool IsRough(Biome biome) => TerrainDefense(biome) > 1;

    /// <summary>Defenders of a province with a river fire this much harder: the attackers have to cross it.</summary>
    public const double RiverDefense = 1.25;

    /// <summary>Days of work each battalion of engineers puts into a road or railway every day.</summary>
    public const int EngineerWorkDays = 1;

    /// <summary>Share of the terrain's defence bonus left to defenders facing engineers; they bridge rivers outright.</summary>
    public const double EngineeredTerrainDefense = 0.5;

    /// <summary>
    /// How much harder a province is to take: its terrain, and its river if it has one. Attackers with
    /// <paramref name="engineers"/> halve the terrain's bonus and cross the river as if it were not there.
    /// </summary>
    public static double DefenseMultiplier(Province province, bool engineers = false)
    {
        double terrain = TerrainDefense(province.Biome);
        if (!engineers) return terrain * (province.HasRiver ? RiverDefense : 1);
        return 1 + (terrain - 1) * EngineeredTerrainDefense;
    }

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

    // Equipment, every day
    /// <summary>A factory makes this many times what a workshop does.</summary>
    public const double FactoryOutput = 2;
    /// <summary>Battalions of warriors and of scouts each nation starts with the equipment for.</summary>
    public const int StartingWarriorKits = 4, StartingScoutKits = 2;

    // Supply and recovery, every day
    /// <summary>Supply reaches this many hours of marching through territory the nation controls from a city, or from its roads and railways joined to one.</summary>
    public const double SupplyRangeHours = 24 * 10;
    public const double OutOfSupplyEfficiency = 0.75;
    /// <summary>Share of their full organisation regiments regain each day in supply and out of combat.</summary>
    public const double OrganisationRecovery = 0.2;
    // Emplacement: a combat unit digs in where it stands
    /// <summary>Days an emplaced unit takes to dig in fully.</summary>
    public const double EmplacementDays = 5;
    /// <summary>Extra defence of a fully dug-in unit (in proportion until then).</summary>
    public const double EmplacementDefense = 0.25;
    /// <summary>An emplaced unit regains organisation this many times as fast...</summary>
    public const double EmplacedRecovery = 1.5;
    /// <summary>...and its shipments take this share of the time to reach it (it waits for them at a known place).</summary>
    public const double EmplacedShipmentTime = 0.5;
    /// <summary>Share of full organisation lost each day without supply.</summary>
    public const double OutOfSupplyOrganisationLoss = 0.05;
    /// <summary>Share of full strength lost each day without supply (hunger, desertion).</summary>
    public const double OutOfSupplyAttrition = 0.01;
    /// <summary>Share of full strength a battalion in supply gets back each day, taken from the capital's people.</summary>
    public const double ReinforcementRate = 0.05;

    // Logistics: shipments from the capital, and ammunition in battle
    /// <summary>Units without an HQ in range get their shipments this many times slower (and after everyone else).</summary>
    public const double UnattachedShipmentSlowdown = 2;
    /// <summary>Men, pieces of equipment or suministros a convoy carries over the sea.</summary>
    public const double ConvoyCapacity = 100;
    /// <summary>Convoys a shipyard order builds at once.</summary>
    public const int ConvoysPerOrder = 5;
    /// <summary>Convoys a nation with a port gets when a save from before convoys is loaded.</summary>
    public const int ConvoysInOldSaves = 10;
    /// <summary>
    /// A shipment at sea loses each day raid / (raid + EscortWeight * escort + ConvoyEvasion) of its convoys and cargo, at
    /// most MaxDailyConvoyLoss, where raid and escort are the fire of the raiders and escorts on its route.
    /// </summary>
    public const double EscortWeight = 2, ConvoyEvasion = 30, MaxDailyConvoyLoss = 0.6;
    /// <summary>Submarines hunt convoys this many times better than their fire says.</summary>
    public const double SubmarineRaidBonus = 2;
    /// <summary>Suministros a battalion of 100 men spends each hour it fights (artillery and aircraft as well; medics do not fight).</summary>
    public const double AmmoPerHundredMenHour = 0.25;
    /// <summary>Hours of fighting a unit carries ammunition for.</summary>
    public const double AmmoHours = 24;
    /// <summary>A battalion out of ammunition fights at this share of its fire.</summary>
    public const double OutOfAmmoEfficiency = 0.5;

    // Sieges
    /// <summary>Days to take a fortified province per point of its defence: 30 for walls (+50 %), 60 for a castle (+100 %).</summary>
    public const double SiegeDaysPerDefense = 60;
    /// <summary>A full artillery battalion adds a day of siege work per day for every this much attack (catapults: one).</summary>
    public const double SiegeAttackPerDay = 12;

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
