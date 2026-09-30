namespace Conquer.Game.World;

public enum Biome : byte
{
    DeepOcean,
    Ocean,
    ShallowSea,
    Lake,
    PolarIce,
    Tundra,
    Taiga,
    TemperateForest,
    Grassland,
    Steppe,
    Desert,
    Savanna,
    TropicalForest,
    Wetland,
    Hills,
    Mountains,
    HighMountains,
    /// <summary>Land above <see cref="Rules.GameRules.PeakElevation"/> (maps since 1.33.0): one province per range, crossable but never claimed. Last, so the others keep their numbers.</summary>
    Peaks,
}

/// <param name="Habitable">Whether the province can be claimed, settled and populated.</param>
/// <param name="ProvinceDensity">Relative number of provinces per km²: low values make large provinces.</param>
/// <param name="FoodYield">Multiplier on the food each worker grows (<c>GameRules.FoodPerWorker</c>).</param>
/// <param name="WoodYield">Wood produced per 1000 workers per day.</param>
/// <param name="Carrying">Citizens per km² the land can feed at the start of history.</param>
/// <param name="MoveSpeed">Travel speed multiplier across this terrain.</param>
public sealed record BiomeInfo(
    string Name,
    bool IsWater,
    bool Habitable,
    float ProvinceDensity,
    float FoodYield,
    float WoodYield,
    float Carrying,
    float MoveSpeed,
    uint Color);

public static class Biomes
{
    private static readonly BiomeInfo[] Table =
    [
        new("Océano profundo", true, false, 0.035f, 0, 0, 0, 1.0f, 0xFF1B3A6B),
        new("Océano", true, false, 0.05f, 0, 0, 0, 1.0f, 0xFF24508A),
        new("Mar costero", true, false, 0.12f, 0, 0, 0, 1.0f, 0xFF3A6EA5),
        new("Lago", true, false, 0.15f, 0, 0, 0, 1.0f, 0xFF4A86C0),
        new("Hielo polar", false, false, 0.05f, 0, 0, 0, 0.4f, 0xFFEEF3F7),
        new("Tundra", false, true, 0.45f, 0.6f, 0.2f, 1.0f, 0.7f, 0xFF9DA890),
        new("Taiga", false, true, 0.8f, 0.8f, 3.0f, 3.0f, 0.7f, 0xFF3F6B4A),
        new("Bosque templado", false, true, 1.0f, 1.2f, 2.5f, 12f, 0.8f, 0xFF3E8A3A),
        new("Pradera", false, true, 1.0f, 1.6f, 0.4f, 18f, 1.0f, 0xFF8DBA4E),
        new("Estepa", false, true, 0.7f, 1.1f, 0.2f, 6f, 1.0f, 0xFFB8B56A),
        new("Desierto", false, true, 0.2f, 0.4f, 0.0f, 0.5f, 0.8f, 0xFFE3CF8E),
        new("Sabana", false, true, 0.8f, 1.3f, 0.6f, 10f, 1.0f, 0xFFBFAE4E),
        new("Selva tropical", false, true, 0.8f, 1.1f, 3.5f, 8f, 0.6f, 0xFF1F6B2A),
        new("Humedal", false, true, 0.8f, 1.3f, 1.0f, 10f, 0.5f, 0xFF4F8A78),
        new("Colinas", false, true, 0.9f, 1.0f, 1.2f, 8f, 0.7f, 0xFF9C9460),
        new("Montañas", false, true, 0.6f, 0.6f, 0.8f, 3f, 0.5f, 0xFF8A7B6A),
        new("Alta montaña", false, true, 0.3f, 0.3f, 0.1f, 0.5f, 0.35f, 0xFFC8C3BE),
        new("Cumbres", false, false, 0.02f, 0, 0, 0, 0.4f, 0xFFDEDAD6),
    ];

    public static BiomeInfo Info(this Biome biome) => Table[(int)biome];
}
