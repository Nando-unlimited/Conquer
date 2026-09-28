using Conquer.Game.Rules;

namespace Conquer.Game.World.Generation;

/// <summary>
/// Rivers from the relief. On a half-resolution grid, a priority flood from every sea and lake finds
/// where each piece of land drains (crossing hollows as a lake would, once filled); rain then runs down
/// that tree, each cell adding runoff by its climate. Cells that gather enough water are rivers. A few
/// metres of noise on the relief make them meander over flat land, and their courses are smoothed.
/// </summary>
internal static class RiverGenerator
{
    /// <summary>Grid cells are this many map pixels on a side.</summary>
    private const int Step = 2;

    public static List<RiverSegment> Trace(short[] elevation, Biome[] biomes, int width, int height, int seed)
    {
        var wobble = new Noise(seed + 40);
        int w = width / Step, h = height / Step, n = w * h;
        var elev = new int[n];
        var water = new bool[n];
        var runoff = new float[n];
        for (int y = 0; y < h; y++)
        {
            double cosLat = Math.Cos((90.0 - (y + 0.5) / h * 180.0) * Math.PI / 180);
            for (int x = 0; x < w; x++)
            {
                // A cell is water if most of its pixels are; its height is the lowest land pixel.
                int wet = 0, low = int.MaxValue;
                double rain = 0;
                for (int dy = 0; dy < Step; dy++)
                for (int dx = 0; dx < Step; dx++)
                {
                    int i = (y * Step + dy) * width + x * Step + dx;
                    var biome = biomes[i];
                    if (biome.Info().IsWater) wet++;
                    else low = Math.Min(low, elevation[i]);
                    rain += Runoff(biome);
                }
                int c = y * w + x;
                water[c] = wet * 2 > Step * Step;
                var (px, py, pz) = Sphere.Point(x, y, w, h);
                // Only to steer the water: without it, flat land drains in straight lines.
                elev[c] = water[c] ? 0 : low + (int)(wobble.Fractal(px * 300, py * 300, pz * 300, 3) * 30);
                runoff[c] = (float)(rain / (Step * Step) * cosLat);
            }
        }

        // Priority flood: each land cell drains into the neighbour that reached it first from the water.
        var downstream = new int[n];
        Array.Fill(downstream, -1);
        var reached = new bool[n];
        var level = new int[n];
        var order = new List<int>(n);
        var open = new PriorityQueue<int, int>();
        for (int c = 0; c < n; c++)
            if (water[c])
            {
                reached[c] = true;
                open.Enqueue(c, 0);
            }
        while (open.TryDequeue(out int c, out int lvl))
        {
            if (!water[c]) order.Add(c);
            int cx = c % w, cy = c / w;
            for (int k = 0; k < 8; k++)
            {
                int nx = (cx + Dx[k] + w) % w, ny = cy + Dy[k];
                if (ny < 0 || ny >= h) continue;
                int next = ny * w + nx;
                if (reached[next]) continue;
                reached[next] = true;
                downstream[next] = c;
                level[next] = Math.Max(lvl, elev[next]);
                open.Enqueue(next, level[next]);
            }
        }

        // Rain runs down the tree, from the farthest cells towards the sea.
        var flow = new float[n];
        for (int k = order.Count - 1; k >= 0; k--)
        {
            int c = order[k];
            flow[c] += runoff[c];
            if (downstream[c] >= 0 && !water[downstream[c]]) flow[downstream[c]] += flow[c];
        }

        return Smooth(order, downstream, water, flow, w, width);
    }

    /// <summary>
    /// Links the river cells into stretches from a source or a confluence to the next confluence or the
    /// sea, rounds each stretch off (Chaikin, keeping its ends so stretches still meet) and cuts it into
    /// short segments for drawing.
    /// </summary>
    private static List<RiverSegment> Smooth(List<int> order, int[] downstream, bool[] water, float[] flow, int w, int width)
    {
        bool IsRiver(int c) => !water[c] && flow[c] >= GameRules.MinRiverFlow && downstream[c] >= 0;
        var inflows = new Dictionary<int, int>();
        foreach (int c in order)
            if (IsRiver(c)) inflows[downstream[c]] = inflows.GetValueOrDefault(downstream[c]) + 1;

        var rivers = new List<RiverSegment>();
        var points = new List<(float X, float Y, float Flow)>();
        foreach (int start in order)
        {
            // A stretch starts at a source or just below a confluence.
            if (!IsRiver(start) || inflows.GetValueOrDefault(start) == 1) continue;
            points.Clear();
            int c = start;
            while (true)
            {
                points.Add(((c % w + 0.5f + Jitter(c, 1)) * Step, (c / w + 0.5f + Jitter(c, 2)) * Step, flow[c]));
                int next = downstream[c];
                if (next < 0) break;
                if (!IsRiver(next) || inflows.GetValueOrDefault(next) != 1)
                {
                    points.Add(((next % w + 0.5f) * Step, (next / w + 0.5f) * Step, IsRiver(next) ? flow[next] : flow[c]));
                    break;
                }
                c = next;
            }
            Unwrap(points, width);
            var smooth = Chaikin(Chaikin(Chaikin(points)));
            for (int i = 0; i + 1 < smooth.Count; i++)
                rivers.Add(new RiverSegment(Wrap(smooth[i].X, width), smooth[i].Y, Wrap(smooth[i].X, width) + smooth[i + 1].X - smooth[i].X, smooth[i + 1].Y, smooth[i].Flow));
        }
        return rivers;
    }

    /// <summary>A fixed pseudo-random nudge of up to a third of a cell, so courses do not follow the grid.</summary>
    private static float Jitter(int cell, int axis)
    {
        uint hash = (uint)cell * 2654435761u ^ (uint)axis * 40503u;
        hash ^= hash >> 15;
        hash *= 2246822519u;
        hash ^= hash >> 13;
        return (hash % 1000) / 1000f * 0.66f - 0.33f;
    }

    /// <summary>Makes x continuous along a stretch that crosses the date line, so smoothing does not jump.</summary>
    private static void Unwrap(List<(float X, float Y, float Flow)> points, int width)
    {
        for (int i = 1; i < points.Count; i++)
        {
            float dx = points[i].X - points[i - 1].X;
            if (Math.Abs(dx) > width / 2f) points[i] = (points[i].X - MathF.Sign(dx) * width, points[i].Y, points[i].Flow);
        }
    }

    private static float Wrap(float x, int width) => ((x % width) + width) % width;

    /// <summary>One round of Chaikin corner cutting; the first and last points stay put.</summary>
    private static List<(float X, float Y, float Flow)> Chaikin(List<(float X, float Y, float Flow)> p)
    {
        if (p.Count < 3) return [.. p];
        var result = new List<(float X, float Y, float Flow)>(p.Count * 2) { p[0] };
        for (int i = 0; i + 1 < p.Count; i++)
        {
            var (a, b) = (p[i], p[i + 1]);
            result.Add((0.75f * a.X + 0.25f * b.X, 0.75f * a.Y + 0.25f * b.Y, a.Flow));
            result.Add((0.25f * a.X + 0.75f * b.X, 0.25f * a.Y + 0.75f * b.Y, b.Flow));
        }
        result.Add(p[^1]);
        return result;
    }

    private static readonly int[] Dx = [1, -1, 0, 0, 1, 1, -1, -1];
    private static readonly int[] Dy = [0, 0, 1, -1, 1, -1, 1, -1];

    /// <summary>Water each land pixel feeds its river: wet forests and mountains a lot, deserts and ice little.</summary>
    private static double Runoff(Biome biome) => biome switch
    {
        Biome.TropicalForest => 1.5,
        Biome.Wetland => 1.2,
        Biome.TemperateForest or Biome.Hills or Biome.Mountains => 1.0,
        Biome.HighMountains or Biome.Taiga => 0.8,
        Biome.Grassland or Biome.Savanna => 0.6,
        Biome.Tundra => 0.4,
        Biome.Steppe => 0.25,
        Biome.PolarIce => 0.15,
        Biome.Desert => 0.05,
        _ => 0,
    };
}
