using Conquer.Game.World;

namespace Conquer.Client.Graphics;

/// <summary>
/// Biome colours as RGBA bytes, with relief on land (lit from the north-west, heights a little paler) and,
/// at sea, deeper water darker and a lighter band of shallows along the coast.
/// </summary>
public static class TerrainColors
{
    public static byte[] Build(WorldMap map)
    {
        int w = map.Width, h = map.Height;
        var data = new byte[w * h * 4];
        var elevation = map.Elevation;
        bool IsSea(int x, int y) => y >= 0 && y < h && map.Biomes[y * w + (x % w + w) % w].Info().IsWater;

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
                    // Deeper water is darker; water touching land catches the light.
                    float depth = Math.Clamp(-e / 6000f, 0, 1);
                    float f = 1.15f - depth * 0.45f;
                    bool shore = !IsSea(x - 1, y) || !IsSea(x + 1, y) || !IsSea(x, y - 1) || !IsSea(x, y + 1);
                    if (shore) f *= 1.22f;
                    r *= f; g *= f; b *= f;
                }
                else if (!biome.Info().IsWater)
                {
                    // Light from the north-west, measured over two pixels so the relief reads at every zoom.
                    int west = elevation[y * w + (x + w - 2) % w];
                    int north = y > 1 ? elevation[(y - 2) * w + x] : e;
                    float slope = ((e - west) + (e - north)) / 500f;
                    float shade = Math.Clamp(1f + slope * 0.55f, 0.55f, 1.4f);
                    // High ground fades a little towards grey-white, like thinner vegetation.
                    float height = Math.Clamp(e / 4000f, 0, 1) * 0.25f;
                    r = (r + (225 - r) * height) * shade;
                    g = (g + (222 - g) * height) * shade;
                    b = (b + (215 - b) * height) * shade;
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
