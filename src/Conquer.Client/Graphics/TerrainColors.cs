using Conquer.Game.World;

namespace Conquer.Client.Graphics;

/// <summary>Biome colours with hill shading on land and depth shading at sea, as RGBA bytes.</summary>
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
                    // Deeper water is darker.
                    float depth = Math.Clamp(-e / 6000f, 0, 1);
                    float f = 1.15f - depth * 0.45f;
                    r *= f; g *= f; b *= f;
                }
                else if (!biome.Info().IsWater)
                {
                    // Light from the north-west.
                    int west = elevation[y * w + (x + w - 1) % w];
                    int north = y > 0 ? elevation[(y - 1) * w + x] : e;
                    float slope = ((e - west) + (e - north)) / 400f;
                    float shade = Math.Clamp(1f + slope * 0.35f, 0.65f, 1.3f);
                    r *= shade; g *= shade; b *= shade;
                }

                data[i * 4] = (byte)Math.Clamp(r, 0, 255);
                data[i * 4 + 1] = (byte)Math.Clamp(g, 0, 255);
                data[i * 4 + 2] = (byte)Math.Clamp(b, 0, 255);
                data[i * 4 + 3] = 255;
            }
        });
        return data;
    }
}
