namespace Conquer.Game.World.Generation;

/// <summary>Seeded 3D gradient (Perlin) noise. Sampling it on the unit sphere gives seamless world maps.</summary>
public sealed class Noise
{
    private readonly int[] _perm = new int[512];

    public Noise(int seed)
    {
        var p = Enumerable.Range(0, 256).ToArray();
        var random = new Random(seed);
        for (int i = 255; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (p[i], p[j]) = (p[j], p[i]);
        }
        for (int i = 0; i < 512; i++) _perm[i] = p[i & 255];
    }

    /// <summary>Roughly in [-1, 1].</summary>
    public double Sample(double x, double y, double z)
    {
        int xi = (int)Math.Floor(x), yi = (int)Math.Floor(y), zi = (int)Math.Floor(z);
        double xf = x - xi, yf = y - yi, zf = z - zi;
        xi &= 255; yi &= 255; zi &= 255;
        double u = Fade(xf), v = Fade(yf), w = Fade(zf);

        int a = _perm[xi] + yi, aa = _perm[a] + zi, ab = _perm[a + 1] + zi;
        int b = _perm[xi + 1] + yi, ba = _perm[b] + zi, bb = _perm[b + 1] + zi;

        return Lerp(w,
            Lerp(v, Lerp(u, Grad(_perm[aa], xf, yf, zf), Grad(_perm[ba], xf - 1, yf, zf)),
                    Lerp(u, Grad(_perm[ab], xf, yf - 1, zf), Grad(_perm[bb], xf - 1, yf - 1, zf))),
            Lerp(v, Lerp(u, Grad(_perm[aa + 1], xf, yf, zf - 1), Grad(_perm[ba + 1], xf - 1, yf, zf - 1)),
                    Lerp(u, Grad(_perm[ab + 1], xf, yf - 1, zf - 1), Grad(_perm[bb + 1], xf - 1, yf - 1, zf - 1))));
    }

    /// <summary>Fractal sum of octaves, normalised to roughly [-1, 1].</summary>
    public double Fractal(double x, double y, double z, int octaves, double persistence = 0.5, double lacunarity = 2.0)
    {
        double sum = 0, amplitude = 1, total = 0, frequency = 1;
        for (int i = 0; i < octaves; i++)
        {
            sum += Sample(x * frequency, y * frequency, z * frequency) * amplitude;
            total += amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
        }
        return sum / total;
    }

    /// <summary>Ridged fractal noise in [0, 1] with sharp crests, used for mountain ranges.</summary>
    public double Ridged(double x, double y, double z, int octaves)
    {
        double sum = 0, amplitude = 1, total = 0, frequency = 1;
        for (int i = 0; i < octaves; i++)
        {
            double n = 1 - Math.Abs(Sample(x * frequency, y * frequency, z * frequency));
            sum += n * n * amplitude;
            total += amplitude;
            amplitude *= 0.5;
            frequency *= 2.0;
        }
        return sum / total;
    }

    private static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);
    private static double Lerp(double t, double a, double b) => a + t * (b - a);

    private static double Grad(int hash, double x, double y, double z)
    {
        int h = hash & 15;
        double u = h < 8 ? x : y;
        double v = h < 4 ? y : h is 12 or 14 ? x : z;
        return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
    }
}

internal static class Sphere
{
    /// <summary>Unit-sphere position of a raster pixel.</summary>
    public static (double X, double Y, double Z) Point(int x, int y, int width, int height)
    {
        double lat = (0.5 - (y + 0.5) / height) * Math.PI;
        double lon = ((x + 0.5) / width * 2 - 1) * Math.PI;
        double c = Math.Cos(lat);
        return (c * Math.Cos(lon), Math.Sin(lat), c * Math.Sin(lon));
    }
}
