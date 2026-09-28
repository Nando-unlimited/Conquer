namespace Conquer.Game.World;

public enum MapKind
{
    Random,
    Earth,
}

/// <summary>A stretch of river between two map points, with the water it carries (grows downstream).</summary>
public readonly record struct RiverSegment(float X1, float Y1, float X2, float Y2, float Flow);

/// <summary>
/// An equirectangular raster of the planet split into provinces. X wraps around (longitude),
/// Y runs from the north pole (row 0) to the south pole.
/// </summary>
public sealed class WorldMap
{
    public const double EarthRadiusKm = 6371.0;

    public int Width { get; }
    public int Height { get; }
    public MapKind Kind { get; }
    public int Seed { get; }
    public short[] Elevation { get; }
    public Biome[] Biomes { get; }
    public int[] ProvinceIds { get; }
    public IReadOnlyList<Province> Provinces { get; }
    /// <summary>Every river on the map, as short segments from each point to the next one downstream.</summary>
    public IReadOnlyList<RiverSegment> Rivers { get; }

    public WorldMap(int width, int height, MapKind kind, int seed, short[] elevation, Biome[] biomes, int[] provinceIds,
        IReadOnlyList<Province> provinces, IReadOnlyList<RiverSegment>? rivers = null)
    {
        Rivers = rivers ?? [];
        Width = width;
        Height = height;
        Kind = kind;
        Seed = seed;
        Elevation = elevation;
        Biomes = biomes;
        ProvinceIds = provinceIds;
        Provinces = provinces;
    }

    public double Latitude(double y) => 90.0 - (y + 0.5) / Height * 180.0;
    public double Longitude(double x) => (x + 0.5) / Width * 360.0 - 180.0;

    public int WrapX(int x) => ((x % Width) + Width) % Width;

    /// <summary>Province at a pixel, or null outside the map vertically.</summary>
    public Province? ProvinceAt(int x, int y)
    {
        if (y < 0 || y >= Height) return null;
        return Provinces[ProvinceIds[y * Width + WrapX(x)]];
    }

    /// <summary>Area of one pixel on the given row in km².</summary>
    public double PixelAreaKm2(int y)
    {
        double degree = Math.PI / 180.0;
        double h = 180.0 / Height * degree * EarthRadiusKm;
        double w = 360.0 / Width * degree * EarthRadiusKm * Math.Cos(Latitude(y) * degree);
        return h * Math.Max(w, 0.0);
    }

    /// <summary>Great-circle distance in km.</summary>
    public static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        double r = Math.PI / 180.0;
        double dLat = (lat2 - lat1) * r, dLon = (lon2 - lon1) * r;
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(lat1 * r) * Math.Cos(lat2 * r) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * EarthRadiusKm * Math.Asin(Math.Min(1.0, Math.Sqrt(a)));
    }

    public double DistanceKm(Province a, Province b) => DistanceKm(a.Latitude, a.Longitude, b.Latitude, b.Longitude);
}
