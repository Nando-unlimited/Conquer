namespace Conquer.Game.World.Generation;

/// <summary>
/// Splits the raster into provinces. Seeds are scattered with a density that depends on the biome
/// (deserts, ice and oceans get far fewer, so their provinces are much larger) and on the real
/// area of each pixel, then grown outwards with slightly noisy costs so borders look organic.
/// A province never mixes sea, lake, ice and habitable land.
/// </summary>
internal static class ProvinceGenerator
{
    private const int CellSize = 5;
    private const int MinComponentPixels = 3;

    public static (int[] Ids, List<Province> Provinces) Generate(short[] elevation, Biome[] biomes, int width, int height, int seed, int targetCount)
    {
        var random = new Random(seed + 20);
        var category = new byte[biomes.Length];
        for (int i = 0; i < biomes.Length; i++) category[i] = Category(biomes[i]);
        var stepCost = StepCosts(width, height, seed);

        var seeds = PlaceSeeds(biomes, width, height, targetCount, random);
        var ids = Grow(seeds, category, stepCost, width, height);
        seeds = Relax(seeds, ids, width, height);
        ids = Grow(seeds, category, stepCost, width, height);
        FillLeftovers(ids, seeds, category, stepCost, width, height);

        var provinces = BuildProvinces(ids, seeds, elevation, biomes, width, height);
        return (ids, provinces);
    }

    private static byte Category(Biome b) => b switch
    {
        Biome.Lake => 1,
        Biome.PolarIce => 2,
        _ when b.Info().IsWater => 0,
        _ => 3,
    };

    private static byte[] StepCosts(int width, int height, int seed)
    {
        var noise = new Noise(seed + 21);
        var cost = new byte[width * height];
        Parallel.For(0, height, y =>
        {
            for (int x = 0; x < width; x++)
            {
                var (px, py, pz) = Sphere.Point(x, y, width, height);
                cost[y * width + x] = (byte)Math.Clamp(10 + noise.Fractal(px * 40, py * 40, pz * 40, 3) * 9, 5, 15);
            }
        });
        return cost;
    }

    private static List<int> PlaceSeeds(Biome[] biomes, int width, int height, int targetCount, Random random)
    {
        var rowWeight = new double[height];
        for (int y = 0; y < height; y++) rowWeight[y] = Math.Max(0.0, Math.Cos((0.5 - (y + 0.5) / height) * Math.PI));

        double total = 0;
        for (int i = 0; i < biomes.Length; i++) total += biomes[i].Info().ProvinceDensity * rowWeight[i / width];
        double scale = targetCount / total;

        var seeds = new List<int>(targetCount + 1000);
        for (int cy = 0; cy < height; cy += CellSize)
        for (int cx = 0; cx < width; cx += CellSize)
        {
            int h = Math.Min(CellSize, height - cy), w = Math.Min(CellSize, width - cx);
            double expected = 0, maxDensity = 0;
            for (int y = cy; y < cy + h; y++)
            for (int x = cx; x < cx + w; x++)
            {
                double d = biomes[y * width + x].Info().ProvinceDensity;
                expected += d * rowWeight[y];
                maxDensity = Math.Max(maxDensity, d);
            }
            expected *= scale;
            int count = (int)expected + (random.NextDouble() < expected - (int)expected ? 1 : 0);
            for (int k = 0; k < count; k++)
            {
                int pick = -1;
                for (int tries = 0; tries < 16; tries++)
                {
                    int i = (cy + random.Next(h)) * width + cx + random.Next(w);
                    pick = i;
                    if (random.NextDouble() * maxDensity <= biomes[i].Info().ProvinceDensity) break;
                }
                seeds.Add(pick);
            }
        }
        return seeds.Distinct().ToList();
    }

    /// <summary>
    /// Multi-source shortest-path growth (Dial's bucket queue). Each seed claims the pixels of its own
    /// category that it reaches first.
    /// </summary>
    private static int[] Grow(List<int> seeds, byte[] category, byte[] stepCost, int width, int height)
    {
        int n = category.Length;
        var ids = new int[n];
        Array.Fill(ids, -1);
        var dist = new int[n];
        Array.Fill(dist, int.MaxValue);
        const int Buckets = 64;
        var buckets = new Queue<int>[Buckets];
        for (int b = 0; b < Buckets; b++) buckets[b] = new Queue<int>();
        int pending = 0;

        for (int s = 0; s < seeds.Count; s++)
        {
            int p = seeds[s];
            if (ids[p] >= 0 && ids[p] != s) continue;
            ids[p] = s;
            dist[p] = 0;
            buckets[0].Enqueue(p);
            pending++;
        }

        for (int d = 0; pending > 0; d++)
        {
            var bucket = buckets[d % Buckets];
            while (bucket.Count > 0)
            {
                int i = bucket.Dequeue();
                pending--;
                if (dist[i] != d) continue;
                int x = i % width, y = i / width, id = ids[i];
                byte cat = category[i];
                for (int dy = -1; dy <= 1; dy++)
                {
                    int ny = y + dy;
                    if (ny < 0 || ny >= height) continue;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int j = ny * width + (x + dx + width) % width;
                        if (category[j] != cat) continue;
                        int cost = stepCost[j] * (dx != 0 && dy != 0 ? 14 : 10) / 10;
                        int nd = d + cost;
                        if (nd >= dist[j]) continue;
                        dist[j] = nd;
                        ids[j] = id;
                        buckets[nd % Buckets].Enqueue(j);
                        pending++;
                    }
                }
            }
        }
        return ids;
    }

    /// <summary>One Lloyd step: move each seed to its region's centroid when that point lies inside the region.</summary>
    private static List<int> Relax(List<int> seeds, int[] ids, int width, int height)
    {
        int count = seeds.Count;
        var sumCos = new double[count];
        var sumSin = new double[count];
        var sumY = new double[count];
        var n = new int[count];
        for (int i = 0; i < ids.Length; i++)
        {
            int id = ids[i];
            if (id < 0) continue;
            double angle = (i % width) * 2 * Math.PI / width;
            sumCos[id] += Math.Cos(angle);
            sumSin[id] += Math.Sin(angle);
            sumY[id] += i / width;
            n[id]++;
        }
        var result = new List<int>(count);
        for (int s = 0; s < count; s++)
        {
            if (n[s] == 0) { result.Add(seeds[s]); continue; }
            int cx = CircularMeanX(sumCos[s], sumSin[s], width);
            int cy = Math.Clamp((int)Math.Round(sumY[s] / n[s]), 0, height - 1);
            int c = cy * width + cx;
            result.Add(ids[c] == s ? c : seeds[s]);
        }
        return result;
    }

    private static int CircularMeanX(double sumCos, double sumSin, int width)
    {
        double angle = Math.Atan2(sumSin, sumCos);
        if (angle < 0) angle += 2 * Math.PI;
        return Math.Clamp((int)Math.Round(angle / (2 * Math.PI) * width), 0, width - 1) % width;
    }

    /// <summary>
    /// Areas no seed could reach (islands, isolated lakes). Tiny ones join a neighbouring province;
    /// the rest become provinces of their own.
    /// </summary>
    private static void FillLeftovers(int[] ids, List<int> seeds, byte[] category, byte[] stepCost, int width, int height)
    {
        var stack = new Stack<int>();
        var component = new List<int>();
        var visited = new bool[ids.Length];
        for (int start = 0; start < ids.Length; start++)
        {
            if (ids[start] >= 0 || visited[start]) continue;
            component.Clear();
            stack.Push(start);
            visited[start] = true;
            while (stack.Count > 0)
            {
                int i = stack.Pop();
                component.Add(i);
                int x = i % width, y = i / width;
                foreach (int j in Neighbours4(x, y, width, height))
                {
                    if (visited[j] || ids[j] >= 0 || category[j] != category[i]) continue;
                    visited[j] = true;
                    stack.Push(j);
                }
            }

            if (component.Count < MinComponentPixels)
            {
                int neighbour = -1;
                foreach (int i in component)
                foreach (int j in Neighbours4(i % width, i / width, width, height))
                    if (ids[j] >= 0) neighbour = ids[j];
                if (neighbour >= 0)
                {
                    foreach (int i in component) ids[i] = neighbour;
                    continue;
                }
            }

            int id = seeds.Count;
            seeds.Add(component[component.Count / 2]);
            foreach (int i in component) ids[i] = id;
        }
    }

    private static IEnumerable<int> Neighbours4(int x, int y, int width, int height)
    {
        yield return y * width + (x + 1) % width;
        yield return y * width + (x + width - 1) % width;
        if (y > 0) yield return (y - 1) * width + x;
        if (y < height - 1) yield return (y + 1) * width + x;
    }

    private static List<Province> BuildProvinces(int[] ids, List<int> seeds, short[] elevation, Biome[] biomes, int width, int height)
    {
        // Seeds whose region vanished (swallowed by a neighbour) leave gaps; compact the ids.
        var remap = new int[seeds.Count];
        Array.Fill(remap, -1);
        int next = 0;
        for (int i = 0; i < ids.Length; i++)
            if (remap[ids[i]] < 0) remap[ids[i]] = next++;
        for (int i = 0; i < ids.Length; i++) ids[i] = remap[ids[i]];

        int count = next;
        var biomeCount = new int[count, Enum.GetValues<Biome>().Length];
        var area = new double[count];
        var pixels = new int[count];
        var elevationSum = new double[count];
        var sumCos = new double[count];
        var sumSin = new double[count];
        var sumY = new double[count];
        var pixelArea = new double[height];
        var probe = new WorldMap(width, height, MapKind.Random, 0, elevation, biomes, ids, []);
        for (int y = 0; y < height; y++) pixelArea[y] = probe.PixelAreaKm2(y);

        var edges = new HashSet<long>();
        for (int i = 0; i < ids.Length; i++)
        {
            int id = ids[i], x = i % width, y = i / width;
            biomeCount[id, (int)biomes[i]]++;
            area[id] += pixelArea[y];
            pixels[id]++;
            elevationSum[id] += elevation[i];
            double angle = x * 2 * Math.PI / width;
            sumCos[id] += Math.Cos(angle);
            sumSin[id] += Math.Sin(angle);
            sumY[id] += y;

            int right = ids[y * width + (x + 1) % width];
            if (right != id) edges.Add(Edge(id, right));
            if (y < height - 1)
            {
                int down = ids[(y + 1) * width + x];
                if (down != id) edges.Add(Edge(id, down));
            }
        }

        var seedOf = new int[count];
        for (int s = 0; s < seeds.Count; s++)
            if (remap[s] >= 0 && ids[seeds[s]] == remap[s]) seedOf[remap[s]] = seeds[s] + 1;

        var neighbours = new List<int>[count];
        for (int i = 0; i < count; i++) neighbours[i] = [];
        foreach (long e in edges)
        {
            int a = (int)(e >> 32), b = (int)(e & 0xFFFFFFFF);
            neighbours[a].Add(b);
            neighbours[b].Add(a);
        }

        var provinces = new List<Province>(count);
        for (int id = 0; id < count; id++)
        {
            var p = new Province(id);
            int best = 0;
            for (int b = 1; b < biomeCount.GetLength(1); b++)
                if (biomeCount[id, b] > biomeCount[id, best]) best = b;
            p.Biome = (Biome)best;
            p.PixelCount = pixels[id];
            p.AreaKm2 = Math.Max(area[id], 1);
            p.MeanElevation = (float)(elevationSum[id] / pixels[id]);
            p.Neighbors = neighbours[id].ToArray();

            int cx = CircularMeanX(sumCos[id], sumSin[id], width);
            int cy = Math.Clamp((int)Math.Round(sumY[id] / pixels[id]), 0, height - 1);
            if (ids[cy * width + cx] != id)
            {
                int s = seedOf[id] - 1;
                if (s < 0) s = Array.IndexOf(ids, id);
                cx = s % width;
                cy = s / width;
            }
            p.CenterX = cx;
            p.CenterY = cy;
            p.Latitude = probe.Latitude(cy);
            p.Longitude = probe.Longitude(cx);
            provinces.Add(p);
        }
        return provinces;
    }

    private static long Edge(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
}
