namespace Conquer.Game.World.Generation;

/// <summary>
/// Assigns a biome to every pixel from a simple climate model: temperature falls with latitude
/// and altitude; moisture follows the global circulation bands (wet equator, dry subtropics,
/// wet mid-latitudes, dry poles) and drops with distance from the ocean.
/// </summary>
internal static class ClimateGenerator
{
    public static Biome[] Assign(Terrain terrain, int width, int height, int seed, bool isEarth)
    {
        var elevation = terrain.Elevation;
        var oceanDistanceKm = DistanceToOceanKm(terrain, width, height);
        var tempNoise = new Noise(seed + 10);
        var moistNoise = new Noise(seed + 11);
        var wetlandNoise = new Noise(seed + 12);
        var biomes = new Biome[width * height];
        // Earth's ice sheets come from real data, so the temperature rule only adds a little high-Arctic ice.
        double iceBelow = isEarth ? -17 : -13;

        Parallel.For(0, height, y =>
        {
            double lat = 90.0 - (y + 0.5) / height * 180.0;
            double absLat = Math.Abs(lat);
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                short e = elevation[i];

                if (terrain.Lake[i]) { biomes[i] = Biome.Lake; continue; }
                if (e < 0)
                {
                    biomes[i] = e > -200 ? Biome.ShallowSea : e > -3000 ? Biome.Ocean : Biome.DeepOcean;
                    continue;
                }

                var (px, py, pz) = Sphere.Point(x, y, width, height);
                double temperature = 28 - 52 * Math.Pow(absLat / 90, 1.5) - 6.5 * e / 1000.0
                                     + tempNoise.Fractal(px * 4, py * 4, pz * 4, 3) * 3;
                if (terrain.Glacier[i] || temperature < iceBelow) { biomes[i] = Biome.PolarIce; continue; }

                double moisture = LatitudeMoisture(absLat);
                double inland = oceanDistanceKm[i];
                moisture *= absLat < 12 ? 0.8 + 0.2 * Math.Exp(-inland / 1500) : 0.45 + 0.55 * Math.Exp(-inland / 1100);
                moisture += moistNoise.Fractal(px * 3, py * 3, pz * 3, 4) * 0.18;

                if (e >= 3800) { biomes[i] = Biome.HighMountains; continue; }
                if (e >= 2200) { biomes[i] = Biome.Mountains; continue; }
                if (e >= 900 && Slope(elevation, width, height, x, y) > 160) { biomes[i] = Biome.Hills; continue; }

                if (e < 60 && moisture > 0.55 && temperature > 0 && wetlandNoise.Fractal(px * 20, py * 20, pz * 20, 2) > 0.25)
                {
                    biomes[i] = Biome.Wetland;
                    continue;
                }

                biomes[i] = temperature switch
                {
                    < -3 => Biome.Tundra,
                    < 4 => moisture > 0.3 ? Biome.Taiga : Biome.Tundra,
                    < 19 => moisture switch
                    {
                        < 0.17 => Biome.Desert,
                        < 0.33 => Biome.Steppe,
                        < 0.52 => Biome.Grassland,
                        _ => Biome.TemperateForest,
                    },
                    _ => moisture switch
                    {
                        < 0.2 => Biome.Desert,
                        < 0.3 => Biome.Steppe,
                        < 0.6 => Biome.Savanna,
                        _ => Biome.TropicalForest,
                    },
                };
            }
        });
        return biomes;
    }

    private static double LatitudeMoisture(double absLat) => absLat switch
    {
        < 10 => 0.95,
        < 20 => 0.95 - (absLat - 10) / 10 * 0.83, // falls into the dry subtropics
        < 28 => 0.12,
        < 42 => 0.12 + (absLat - 28) / 14 * 0.58, // rises into the stormy mid-latitudes
        < 60 => 0.7,
        _ => 0.7 - (absLat - 60) / 30 * 0.4,
    };

    /// <summary>Largest elevation difference to a 4-neighbour, in metres.</summary>
    private static int Slope(short[] elevation, int width, int height, int x, int y)
    {
        int e = elevation[y * width + x], max = 0;
        max = Math.Max(max, Math.Abs(e - elevation[y * width + (x + 1) % width]));
        max = Math.Max(max, Math.Abs(e - elevation[y * width + (x + width - 1) % width]));
        if (y > 0) max = Math.Max(max, Math.Abs(e - elevation[(y - 1) * width + x]));
        if (y < height - 1) max = Math.Max(max, Math.Abs(e - elevation[(y + 1) * width + x]));
        return max;
    }

    /// <summary>Approximate distance in km from each pixel to the nearest ocean pixel (breadth-first, 8-connected).</summary>
    private static float[] DistanceToOceanKm(Terrain terrain, int width, int height)
    {
        int n = width * height;
        var steps = new int[n];
        Array.Fill(steps, -1);
        var queue = new Queue<int>();
        for (int i = 0; i < n; i++)
            if (terrain.Elevation[i] < 0 && !terrain.Lake[i]) { steps[i] = 0; queue.Enqueue(i); }

        while (queue.Count > 0)
        {
            int i = queue.Dequeue();
            int x = i % width, y = i / width;
            for (int dy = -1; dy <= 1; dy++)
            {
                int ny = y + dy;
                if (ny < 0 || ny >= height) continue;
                for (int dx = -1; dx <= 1; dx++)
                {
                    int j = ny * width + (x + dx + width) % width;
                    if (steps[j] >= 0) continue;
                    steps[j] = steps[i] + 1;
                    queue.Enqueue(j);
                }
            }
        }

        double kmPerStep = Math.PI * WorldMap.EarthRadiusKm / height;
        var km = new float[n];
        for (int i = 0; i < n; i++) km[i] = steps[i] < 0 ? 5000 : (float)(steps[i] * kmPerStep);
        return km;
    }
}
