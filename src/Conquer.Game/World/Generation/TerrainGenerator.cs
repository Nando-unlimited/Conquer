namespace Conquer.Game.World.Generation;

/// <summary>Surface elevation plus the few facts the Earth data knows better than the climate model.</summary>
internal sealed class Terrain
{
    public required short[] Elevation { get; init; }
    public required bool[] Lake { get; init; }
    /// <summary>Known permanent ice (Earth only); random maps derive ice from temperature.</summary>
    public required bool[] Glacier { get; init; }
}

internal static class TerrainGenerator
{
    public static Terrain Earth()
    {
        var data = EarthData.LoadEmbedded();
        int n = data.Flags.Length;
        var lake = new bool[n];
        var glacier = new bool[n];
        for (int i = 0; i < n; i++)
        {
            lake[i] = (data.Flags[i] & EarthData.FlagLake) != 0;
            glacier[i] = (data.Flags[i] & EarthData.FlagGlacier) != 0;
        }
        return new Terrain { Elevation = data.Elevation, Lake = lake, Glacier = glacier };
    }

    /// <summary>Continents from warped fractal noise on the sphere, with ridged mountain ranges.</summary>
    public static Terrain Random(int width, int height, int seed, double landFraction = 0.3)
    {
        var continents = new Noise(seed);
        var warp = new Noise(seed + 1);
        var ridges = new Noise(seed + 2);
        var detail = new Noise(seed + 3);
        var mountainMask = new Noise(seed + 4);
        var raw = new float[width * height];
        var ridge = new float[width * height];

        Parallel.For(0, height, y =>
        {
            for (int x = 0; x < width; x++)
            {
                var (px, py, pz) = Sphere.Point(x, y, width, height);
                double wx = warp.Fractal(px * 1.5 + 11, py * 1.5, pz * 1.5, 3) * 0.35;
                double wy = warp.Fractal(px * 1.5, py * 1.5 + 23, pz * 1.5, 3) * 0.35;
                double wz = warp.Fractal(px * 1.5, py * 1.5, pz * 1.5 + 37, 3) * 0.35;
                double h = continents.Fractal((px + wx) * 1.6, (py + wy) * 1.6, (pz + wz) * 1.6, 6, 0.55);
                h += detail.Fractal(px * 12, py * 12, pz * 12, 3) * 0.04;
                raw[y * width + x] = (float)h;
                ridge[y * width + x] = (float)ridges.Ridged(px * 3.5 + wx, py * 3.5 + wy, pz * 3.5 + wz, 5);
            }
        });

        float sea = AreaPercentile(raw, height, 1 - landFraction);
        float max = raw.Max(), min = raw.Min();
        var elevation = new short[width * height];

        Parallel.For(0, height, y =>
        {
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                float h = raw[i];
                if (h >= sea)
                {
                    double t = (h - sea) / (max - sea);
                    var (px, py, pz) = Sphere.Point(x, y, width, height);
                    double mask = Math.Clamp(mountainMask.Fractal(px * 2.2, py * 2.2, pz * 2.2, 3) * 2.2 + 0.2, 0, 1);
                    double r = Math.Pow(ridge[i], 2.2);
                    double m = 20 + Math.Pow(t, 1.3) * 2500 + r * mask * 5200 * Math.Min(1, t * 8);
                    elevation[i] = (short)Math.Min(8800, m);
                }
                else
                {
                    double t = (sea - h) / (sea - min);
                    // A shallow continental shelf, then a slope down to the abyssal plains.
                    double depth = t < 0.06 ? 20 + t / 0.06 * 180 : 200 + Math.Pow((t - 0.06) / 0.94, 0.5) * 6800;
                    elevation[i] = (short)-depth;
                }
            }
        });

        return new Terrain { Elevation = elevation, Lake = new bool[width * height], Glacier = new bool[width * height] };
    }

    /// <summary>Value below which the given fraction of the planet's surface lies (pixels weighted by cos(latitude)).</summary>
    private static float AreaPercentile(float[] values, int height, double fraction)
    {
        const int Buckets = 4096;
        int width = values.Length / height;
        float min = values.Min(), max = values.Max();
        var weights = new double[Buckets];
        double total = 0;
        for (int y = 0; y < height; y++)
        {
            double w = Math.Cos((0.5 - (y + 0.5) / height) * Math.PI);
            for (int x = 0; x < width; x++)
            {
                int b = (int)((values[y * width + x] - min) / (max - min) * (Buckets - 1));
                weights[b] += w;
                total += w;
            }
        }
        double acc = 0;
        for (int b = 0; b < Buckets; b++)
        {
            acc += weights[b];
            if (acc >= total * fraction) return min + (max - min) * b / (Buckets - 1);
        }
        return max;
    }
}
