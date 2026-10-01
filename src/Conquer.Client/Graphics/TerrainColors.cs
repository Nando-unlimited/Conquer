using Conquer.Game.World;

namespace Conquer.Client.Graphics;

/// <summary>
/// The textures the map shader paints the land with: biome colours (heights a little paler, deeper water darker) and,
/// per map pixel, the height and what the ground is made of. The shader lights the relief and adds detail from them;
/// the shallows along the coast follow the smooth coastline it draws.
/// </summary>
public static class TerrainColors
{
    public static byte[] Build(WorldMap map)
    {
        int w = map.Width, h = map.Height;
        var data = new byte[w * h * 4];
        var elevation = map.Elevation;

        Parallel.For(0, h, y =>
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                var biome = map.Biomes[i];
                var c = new Rgba(biome.Info().Color);
                float r = c.R, g = c.G, b = c.B;
                short e = elevation[i];

                if (biome.Info().IsWater && biome != Biome.Lake)
                {
                    // Deeper water is darker (the lighter shallows along the coast are drawn by the map shader).
                    float depth = Math.Clamp(-e / 6000f, 0, 1);
                    float f = 1.15f - depth * 0.45f;
                    r *= f; g *= f; b *= f;
                }
                else if (!biome.Info().IsWater)
                {
                    // High ground fades a little towards grey-white, like thinner vegetation.
                    float height = Math.Clamp(e / 4000f, 0, 1) * 0.25f;
                    r += (225 - r) * height;
                    g += (222 - g) * height;
                    b += (215 - b) * height;
                }

                data[i * 4] = (byte)Math.Clamp(r, 0, 255);
                data[i * 4 + 1] = (byte)Math.Clamp(g, 0, 255);
                data[i * 4 + 2] = (byte)Math.Clamp(b, 0, 255);
                data[i * 4 + 3] = 255;
            }
        });
        return data;
    }

    /// <summary>Metres of land height the detail texture's red channel spans (square-root scale, finer low down).</summary>
    public const float MaxHeight = 9000;

    /// <summary>
    /// Per map pixel: land height (red, square-root scale up to <see cref="MaxHeight"/>; 0 at sea) and how much of the
    /// ground is forest (green), sand (blue) and bare rock (alpha). Filtered smoothly, neighbouring biomes blend.
    /// </summary>
    public static byte[] BuildDetail(WorldMap map)
    {
        int w = map.Width, h = map.Height;
        var data = new byte[w * h * 4];
        var elevation = map.Elevation;

        Parallel.For(0, h, y =>
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                var biome = map.Biomes[i];
                bool water = biome.Info().IsWater;
                var (forest, sand, rock) = Ground(biome);
                data[i * 4] = water ? (byte)0 : (byte)(MathF.Sqrt(Math.Clamp(elevation[i] / MaxHeight, 0, 1)) * 255);
                data[i * 4 + 1] = forest;
                data[i * 4 + 2] = sand;
                data[i * 4 + 3] = rock;
            }
        });
        return data;
    }

    /// <summary>How much of a biome's ground is forest, sand and bare rock (0 to 255 each).</summary>
    private static (byte Forest, byte Sand, byte Rock) Ground(Biome biome) => biome switch
    {
        Biome.TemperateForest or Biome.TropicalForest => (255, 0, 0),
        Biome.Taiga => (230, 0, 0),
        Biome.Wetland => (120, 0, 0),
        Biome.Savanna => (50, 80, 0),
        Biome.Steppe => (0, 110, 0),
        Biome.Desert => (0, 255, 0),
        Biome.Tundra => (0, 40, 50),
        Biome.Hills => (60, 0, 90),
        Biome.Mountains => (0, 0, 200),
        Biome.HighMountains or Biome.Peaks => (0, 0, 255),
        _ => (0, 0, 0),
    };
}
