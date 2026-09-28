using Conquer.Game.Economy;
using Conquer.Game.Rules;

namespace Conquer.Game.World;

public sealed class Province
{
    public int Id { get; }
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

    /// <summary>Daily output of each deposit when the province is fully worked; zero when absent.</summary>
    public float[] Deposits { get; } = new float[Resources.All.Length];

    /// <summary>-1 while nobody owns the province.</summary>
    public int OwnerId { get; set; } = -1;
    public double Population { get; set; }
    public int? CityId { get; set; }
    /// <summary>0 (furious) to 100 (delighted); drifts each day toward what the province's conditions call for.</summary>
    public double Mood { get; set; } = GameRules.StartingMood;
    /// <summary>Birth-rate multiplier (1 = normal); follows mood and food slowly.</summary>
    public double Fertility { get; set; } = 1;

    public Province(int id) => Id = id;

    public BiomeInfo Info => Biome.Info();
    public bool IsWater => Info.IsWater;
    public bool IsClaimable => Info.Habitable;
    public bool IsOwned => OwnerId >= 0;

    /// <summary>Citizens the province's land can feed.</summary>
    public double Capacity => AreaKm2 * Info.Carrying;
}
