using Conquer.Game.Rules;
using Conquer.Game.World.Generation;

namespace Conquer.Game.World;

/// <summary>
/// What a map is generated from. The difficulty changes how many deposits it has and how big they are. <paramref name="Generator"/>
/// is the version of the generator that made it: saves keep theirs, so a game carries on on the map it began on, and
/// new games use <see cref="WorldGenerator.LatestGenerator"/>. It is 1 in saves from before it was written.
/// </summary>
/// <param name="Size">
/// How big a random world is (<see cref="MapSizes"/>): its share of land and how many provinces the whole map is split into.
/// The Earth is always <see cref="MapSize.Large"/>.
/// </param>
public sealed record WorldSettings(MapKind Kind, int Seed, int ProvinceCount = 25000, Difficulty Difficulty = Difficulty.Normal, int Generator = 1,
    MapSize Size = MapSize.Large)
{
    /// <summary>A new game's settings: the latest generator, and the province count of the world's size.</summary>
    public static WorldSettings New(MapKind kind, int seed, Difficulty difficulty, MapSize size = MapSize.Large)
    {
        if (kind == MapKind.Earth) size = MapSize.Large;
        return new(kind, seed, size.Info().ProvinceCount, difficulty, WorldGenerator.LatestGenerator, size);
    }
}

/// <summary>How big a random world is.</summary>
public enum MapSize
{
    Small,
    Medium,
    Large,
}

/// <param name="LandFraction">Share of the planet that is land.</param>
/// <param name="ProvinceCount">Provinces the whole map is split into, sea included: fewer for the land makes them bigger.</param>
public sealed record MapSizeInfo(string Name, string Description, double LandFraction, int ProvinceCount);

public static class MapSizes
{
    public static readonly MapSize[] All = Enum.GetValues<MapSize>();

    private static readonly Dictionary<MapSize, MapSizeInfo> Table = new()
    {
        [MapSize.Small] = new("Pequeño", "Poca tierra, en provincias más grandes: unas tres veces menos provincias que el grande.", 0.18, 9300),
        [MapSize.Medium] = new("Mediano", "Menos tierra y provincias algo más grandes: unos dos tercios de las provincias del grande.", 0.24, 17500),
        [MapSize.Large] = new("Grande", "Un 30 % de tierra en provincias del tamaño de siempre.", 0.3, 25000),
    };

    public static MapSizeInfo Info(this MapSize size) => Table[size];
}

public static class WorldGenerator
{
    public const int Width = EarthData.Width;
    public const int Height = EarthData.Height;

    /// <summary>The generator new games use. 2 (1.33.0): land above <see cref="GameRules.PeakElevation"/> becomes peaks, one province per range.</summary>
    public const int LatestGenerator = 2;

    /// <param name="progress">Receives a short description of each stage as it starts.</param>
    public static WorldMap Generate(WorldSettings settings, Action<string>? progress = null)
    {
        progress?.Invoke(settings.Kind == MapKind.Earth ? "Cargando la Tierra..." : "Levantando continentes...");
        var terrain = settings.Kind == MapKind.Earth
            ? TerrainGenerator.Earth()
            : TerrainGenerator.Random(Width, Height, settings.Seed, settings.Size.Info().LandFraction);

        progress?.Invoke("Calculando clima y biomas...");
        var biomes = ClimateGenerator.Assign(terrain, Width, Height, settings.Seed, settings.Kind == MapKind.Earth, peaks: settings.Generator >= 2);

        progress?.Invoke("Trazando provincias...");
        var (ids, provinces) = ProvinceGenerator.Generate(terrain.Elevation, biomes, Width, Height, settings.Seed, settings.ProvinceCount);

        progress?.Invoke("Repartiendo recursos...");
        ResourceGenerator.Place(provinces, settings.Seed, settings.Difficulty.Info());

        progress?.Invoke("Trazando ríos...");
        var (rivers, flowSegments) = RiverGenerator.Trace(terrain.Elevation, biomes, Width, Height, settings.Seed);
        // Each province remembers the biggest river that runs through it (from the rivers as traced up to 1.30.1).
        foreach (var r in flowSegments)
        {
            var p = provinces[ids[(int)r.Y1 * Width + (int)r.X1]];
            if (!p.IsWater) p.RiverFlow = Math.Max(p.RiverFlow, r.Flow);
        }

        return new WorldMap(Width, Height, settings.Kind, settings.Seed, terrain.Elevation, biomes, ids, provinces, rivers) { Settings = settings };
    }
}
