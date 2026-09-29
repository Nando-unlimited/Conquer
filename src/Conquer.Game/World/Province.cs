using Conquer.Game.Buildings;
using Conquer.Game.Economy;
using Conquer.Game.Rules;
using Conquer.Game.Science;

namespace Conquer.Game.World;

public sealed class Province
{
    public int Id { get; }
    /// <summary>
    /// Its own name, given by the first nation to claim it and kept whoever holds it later; a city founded
    /// in it does not change it. Empty until then, and always for oceans and poles.
    /// </summary>
    public string Name { get; internal set; } = "";
    public Biome Biome { get; internal set; }
    /// <summary>A pixel inside the province used to place labels, units and cities.</summary>
    public int CenterX { get; internal set; }
    public int CenterY { get; internal set; }
    public double Latitude { get; internal set; }
    public double Longitude { get; internal set; }
    public int PixelCount { get; internal set; }
    public double AreaKm2 { get; internal set; }
    public float MeanElevation { get; internal set; }
    public int[] Neighbors { get; internal set; } = [];
    /// <summary>Water carried by the biggest river or stream through the province (0 without one).</summary>
    public float RiverFlow { get; internal set; }

    /// <summary>Daily output of each deposit when the province is fully worked; zero when absent.</summary>
    public float[] Deposits { get; } = new float[Resources.All.Length];
    /// <summary>Total amount each deposit holds on a map of normal size; a game scales it (see <see cref="GameRules.DepositSizeMultiplier"/>).</summary>
    public float[] DepositSizes { get; } = new float[Resources.All.Length];
    /// <summary>What is left in each deposit in the current game; it stops producing at zero.</summary>
    public double[] Reserves { get; } = new double[Resources.All.Length];

    /// <summary>-1 while nobody owns the province.</summary>
    public int OwnerId { get; set; } = -1;
    /// <summary>Who holds it: the owner, or an enemy occupying it in a war (-1 while unowned).</summary>
    public int ControllerId { get; set; } = -1;
    public double Population { get; set; }
    public int? CityId { get; set; }
    /// <summary>0 (furious) to 100 (delighted); drifts each day toward what the province's conditions call for.</summary>
    public double Mood { get; set; } = GameRules.StartingMood;
    /// <summary>Birth-rate multiplier (1 = normal); follows mood and food slowly.</summary>
    public double Fertility { get; set; } = 1;
    /// <summary>Institutions that have reached the province.</summary>
    public HashSet<Institution> Institutions { get; } = [];

    /// <summary>Finished buildings.</summary>
    public HashSet<BuildingType> Buildings { get; } = [];
    /// <summary>The effects of every finished building, added up.</summary>
    public Modifiers BuildingBonuses { get; private set; } = Modifiers.None;
    /// <summary>The building under construction, if any, and the days of work it still needs.</summary>
    public BuildingType? Constructing { get; set; }
    public int ConstructionDaysLeft { get; set; }
    /// <summary>The name of the city being built here, if any; it takes <see cref="ConstructionDaysLeft"/> days like a building.</summary>
    public string? PlannedCityName { get; set; }

    public void AddBuilding(BuildingType type)
    {
        if (Buildings.Add(type)) BuildingBonuses += type.Info().Effects;
    }

    /// <summary>Knocks down every building and stops any construction (for a new game).</summary>
    public void ClearBuildings()
    {
        Buildings.Clear();
        BuildingBonuses = Modifiers.None;
        Constructing = null;
        ConstructionDaysLeft = 0;
        PlannedCityName = null;
    }

    public Province(int id) => Id = id;

    public BiomeInfo Info => Biome.Info();
    /// <summary>Its name, or its biome's for oceans and poles.</summary>
    public string DisplayName => Name.Length > 0 ? Name : Info.Name;
    public bool IsWater => Info.IsWater;
    public bool IsClaimable => Info.Habitable;
    public bool IsOwned => OwnerId >= 0;
    public bool IsOccupied => OwnerId >= 0 && ControllerId != OwnerId;

    /// <summary>A great river runs through it (streams are drawn but change nothing).</summary>
    public bool HasRiver => RiverFlow >= GameRules.GreatRiverFlow;

    /// <summary>Food a worker grows here compared with average land: its biome's, more on a river's floodplain.</summary>
    public double FoodYield => Info.FoodYield * (HasRiver ? GameRules.RiverFertility : 1);

    /// <summary>Citizens the province's land can feed; rivers make it feed more.</summary>
    public double Capacity => AreaKm2 * Info.Carrying * (HasRiver ? GameRules.RiverFertility : 1);

    /// <summary>The province has this deposit and it is not yet exhausted.</summary>
    public bool HasDeposit(ResourceType r) => Deposits[(int)r] > 0 && Reserves[(int)r] > 0;
}
