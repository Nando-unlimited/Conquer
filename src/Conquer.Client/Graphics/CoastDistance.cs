using Conquer.Game.World;

namespace Conquer.Client.Graphics;

/// <summary>
/// For each map pixel, how far it is to the nearest land, in map pixels (0 on land), up to <see cref="MaxDistance"/>.
/// The map shader paints the sea with it: turquoise shallows along the coasts, waves rolling in to the shore and surf.
/// An exact Euclidean distance transform (Felzenszwalb and Huttenlocher), rows then columns, across the seam where the
/// map wraps round.
/// </summary>
public static class CoastDistance
{
    /// <summary>Distances are stored up to this many map pixels (a byte of 255 is this far or more), as COAST_MAX in the shader.</summary>
    public const float MaxDistance = 32f;

    private const double Far = 1e9;
    private const int Padding = 40; // more than MaxDistance, so pixels by the seam measure to land across it

    public static byte[] Build(WorldMap map)
    {
        int width = map.Width, height = map.Height, padded = width + 2 * Padding;
        var squared = new double[padded * height];
        Parallel.For(0, height, y =>
        {
            for (int x = 0; x < padded; x++)
                squared[y * padded + x] = map.Provinces[map.ProvinceIds[y * width + map.WrapX(x - Padding)]].IsWater ? Far : 0;
        });
        Parallel.For(0, height, () => new Buffers(Math.Max(padded, height)), (y, _, b) =>
        {
            squared.AsSpan(y * padded, padded).CopyTo(b.Line);
            Transform(b, padded);
            b.Result.AsSpan(0, padded).CopyTo(squared.AsSpan(y * padded, padded));
            return b;
        }, _ => { });
        Parallel.For(0, padded, () => new Buffers(Math.Max(padded, height)), (x, _, b) =>
        {
            for (int y = 0; y < height; y++) b.Line[y] = squared[y * padded + x];
            Transform(b, height);
            for (int y = 0; y < height; y++) squared[y * padded + x] = b.Result[y];
            return b;
        }, _ => { });

        var bytes = new byte[width * height];
        Parallel.For(0, height, y =>
        {
            for (int x = 0; x < width; x++)
                bytes[y * width + x] = (byte)Math.Min(255.0, Math.Round(Math.Sqrt(squared[y * padded + x + Padding]) * 255.0 / MaxDistance));
        });
        return bytes;
    }

    /// <summary>One dimension of the transform: the lower envelope of the parabolas rooted at each sample.</summary>
    private static void Transform(Buffers b, int n)
    {
        double[] f = b.Line, d = b.Result, bounds = b.Bounds;
        int[] hull = b.Hull;
        int k = 0;
        hull[0] = 0;
        bounds[0] = double.NegativeInfinity;
        bounds[1] = double.PositiveInfinity;
        for (int q = 1; q < n; q++)
        {
            double s = Intersection(f, q, hull[k]);
            while (s <= bounds[k]) s = Intersection(f, q, hull[--k]);
            hull[++k] = q;
            bounds[k] = s;
            bounds[k + 1] = double.PositiveInfinity;
        }
        k = 0;
        for (int q = 0; q < n; q++)
        {
            while (bounds[k + 1] < q) k++;
            int p = hull[k];
            d[q] = (double)(q - p) * (q - p) + f[p];
        }
    }

    private static double Intersection(double[] f, int q, int p) => (f[q] + (double)q * q - (f[p] + (double)p * p)) / (2.0 * q - 2.0 * p);

    private sealed class Buffers(int length)
    {
        public readonly double[] Line = new double[length];
        public readonly double[] Result = new double[length];
        public readonly int[] Hull = new int[length];
        public readonly double[] Bounds = new double[length + 1];
    }
}
