using Conquer.Game.World.Generation;

namespace Conquer.Game.World;

public sealed record WorldSettings(MapKind Kind, int Seed, int ProvinceCount = 25000);

public static class WorldGenerator
{
    public const int Width = EarthData.Width;
    public const int Height = EarthData.Height;

    /// <param name="progress">Receives a short description of each stage as it starts.</param>
    public static WorldMap Generate(WorldSettings settings, Action<string>? progress = null)
    {
        progress?.Invoke(settings.Kind == MapKind.Earth ? "Cargando la Tierra..." : "Levantando continentes...");
        var terrain = settings.Kind == MapKind.Earth
            ? TerrainGenerator.Earth()
            : TerrainGenerator.Random(Width, Height, settings.Seed);

        progress?.Invoke("Calculando clima y biomas...");
        var biomes = ClimateGenerator.Assign(terrain, Width, Height, settings.Seed, settings.Kind == MapKind.Earth);

        progress?.Invoke("Trazando provincias...");
        var (ids, provinces) = ProvinceGenerator.Generate(terrain.Elevation, biomes, Width, Height, settings.Seed, settings.ProvinceCount);

        progress?.Invoke("Repartiendo recursos...");
        ResourceGenerator.Place(provinces, settings.Seed);

        progress?.Invoke("Trazando ríos...");
        var rivers = RiverGenerator.Trace(terrain.Elevation, biomes, Width, Height, settings.Seed);
        // Each province remembers the biggest river that runs through it.
        foreach (var r in rivers)
        {
            var p = provinces[ids[(int)r.Y1 * Width + (int)r.X1]];
            if (!p.IsWater) p.RiverFlow = Math.Max(p.RiverFlow, r.Flow);
        }

        return new WorldMap(Width, Height, settings.Kind, settings.Seed, terrain.Elevation, biomes, ids, provinces, rivers);
    }
}
