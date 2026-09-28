// Builds src/Conquer.Game/Assets/earth.gz, the real-Earth map used by "Tierra real" games.
//
// Inputs (public domain):
//   NASA Visible Earth / GEBCO   gebco_08_rev_elev_21600x10800.png   land elevation, 0..255 ~ 0..8800 m
//                                gebco_08_rev_bath_21600x10800.png   ocean depth, 255 = land, 0 ~ -9000 m
//   Natural Earth 1:50m          ne_50m_land / ne_50m_lakes / ne_50m_glaciated_areas .geojson
//
// Usage: dotnet run --project tools/Conquer.EarthData -c Release -- <input dir> <output file>
using System.IO.Compression;
using System.Text.Json;
using Conquer.Game.World;
using StbImageSharp;

if (args.Length < 2)
{
    Console.Error.WriteLine("usage: Conquer.EarthData <input dir> <output file>");
    return 1;
}

var dir = args[0];
const int W = EarthData.Width, H = EarthData.Height;

var landElevation = Downsample(Load(Path.Combine(dir, "gebco_08_rev_elev_21600x10800.png")));
var bathymetry = Downsample(Load(Path.Combine(dir, "gebco_08_rev_bath_21600x10800.png")));
var land = Rasterize(Path.Combine(dir, "ne_50m_land.geojson"));
var lakes = Rasterize(Path.Combine(dir, "ne_50m_lakes.geojson"));
var glaciers = Rasterize(Path.Combine(dir, "ne_50m_glaciated_areas.geojson"));

var elevation = new short[W * H];
var flags = new byte[W * H];
for (int i = 0; i < W * H; i++)
{
    bool isLand = land[i];
    if (isLand)
    {
        elevation[i] = (short)Math.Max(2, MathF.Round(landElevation[i] / 255f * 8800f / 20f) * 20);
        flags[i] |= EarthData.FlagLand;
        if (lakes[i]) flags[i] |= EarthData.FlagLake;
        if (glaciers[i]) flags[i] |= EarthData.FlagGlacier;
    }
    else
    {
        elevation[i] = (short)Math.Min(-5, MathF.Round(-(255f - bathymetry[i]) / 255f * 9000f / 100f) * 100);
    }
}

EarthData.Write(args[1], elevation, flags);
Console.WriteLine($"wrote {args[1]} ({new FileInfo(args[1]).Length / 1024} KiB)");
return 0;

static ImageResult Load(string path)
{
    using var s = File.OpenRead(path);
    return ImageResult.FromStream(s, ColorComponents.Grey);
}

static float[] Downsample(ImageResult img)
{
    int f = img.Width / W;
    var result = new float[W * H];
    Parallel.For(0, H, y =>
    {
        for (int x = 0; x < W; x++)
        {
            int sum = 0;
            for (int dy = 0; dy < f; dy++)
            {
                int row = (y * f + dy) * img.Width + x * f;
                for (int dx = 0; dx < f; dx++) sum += img.Data[row + dx];
            }
            result[y * W + x] = sum / (float)(f * f);
        }
    });
    return result;
}

// Scanline-fills every polygon of a GeoJSON file (even-odd rule, so holes work) with 3x3
// supersampling; a pixel is set when most of its samples are inside.
static bool[] Rasterize(string path)
{
    const int S = 3;
    var counts = new byte[W * H];
    using var doc = JsonDocument.Parse(File.ReadAllText(path));
    foreach (var feature in doc.RootElement.GetProperty("features").EnumerateArray())
    {
        var geometry = feature.GetProperty("geometry");
        var type = geometry.GetProperty("type").GetString();
        var coords = geometry.GetProperty("coordinates");
        if (type == "Polygon") FillPolygon(coords, counts);
        else if (type == "MultiPolygon")
            foreach (var polygon in coords.EnumerateArray()) FillPolygon(polygon, counts);
    }
    return counts.Select(c => c > S * S / 2).ToArray();

    static void FillPolygon(JsonElement rings, byte[] counts)
    {
        var edges = new List<(double x0, double y0, double x1, double y1)>();
        double minY = double.MaxValue, maxY = double.MinValue;
        foreach (var ring in rings.EnumerateArray())
        {
            var pts = ring.EnumerateArray()
                .Select(p => ((p[0].GetDouble() + 180) / 360 * W * S, (90 - p[1].GetDouble()) / 180 * H * S))
                .ToList();
            for (int i = 0; i < pts.Count; i++)
            {
                var a = pts[i];
                var b = pts[(i + 1) % pts.Count];
                edges.Add((a.Item1, a.Item2, b.Item1, b.Item2));
                minY = Math.Min(minY, a.Item2);
                maxY = Math.Max(maxY, a.Item2);
            }
        }

        var xs = new List<double>();
        for (int sy = Math.Max(0, (int)minY); sy <= Math.Min(H * S - 1, (int)maxY + 1); sy++)
        {
            double cy = sy + 0.5;
            xs.Clear();
            foreach (var (x0, y0, x1, y1) in edges)
                if ((y0 <= cy && y1 > cy) || (y1 <= cy && y0 > cy))
                    xs.Add(x0 + (cy - y0) / (y1 - y0) * (x1 - x0));
            xs.Sort();
            for (int k = 0; k + 1 < xs.Count; k += 2)
            {
                int from = Math.Max(0, (int)Math.Ceiling(xs[k] - 0.5));
                int to = Math.Min(W * S - 1, (int)Math.Floor(xs[k + 1] - 0.5));
                for (int sx = from; sx <= to; sx++)
                {
                    int idx = sy / S * W + sx / S;
                    if (counts[idx] < 255) counts[idx]++;
                }
            }
        }
    }
}
