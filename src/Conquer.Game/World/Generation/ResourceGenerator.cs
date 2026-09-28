using Conquer.Game.Economy;

namespace Conquer.Game.World.Generation;

/// <summary>
/// Scatters deposits over habitable provinces. Each resource favours certain terrain and is
/// clustered by regional noise, so some regions are rich in it and others lack it entirely.
/// Every deposit is a finite pocket: it has a daily output and a total size.
/// </summary>
internal static class ResourceGenerator
{
    private const double MinDepositYears = 10, MaxDepositYears = 50;

    public static void Place(IReadOnlyList<Province> provinces, int seed)
    {
        var random = new Random(seed + 30);
        // Pocket sizes draw from their own generator so a seed places the same deposits as before they had a size.
        var sizes = new Random(seed + 29);
        var regional = Resources.Deposits.ToDictionary(r => r, r => new Noise(seed + 31 + (int)r));

        foreach (var p in provinces)
        {
            if (!p.IsClaimable) continue;
            double lat = p.Latitude * Math.PI / 180, lon = p.Longitude * Math.PI / 180;
            double sx = Math.Cos(lat) * Math.Cos(lon), sy = Math.Sin(lat), sz = Math.Cos(lat) * Math.Sin(lon);

            foreach (var resource in Resources.Deposits)
            {
                double chance = Chance(resource, p.Biome, Math.Abs(p.Latitude));
                if (chance <= 0) continue;
                double cluster = Math.Clamp(regional[resource].Fractal(sx * 3, sy * 3, sz * 3, 3) * 1.8 + 0.6, 0, 2);
                if (random.NextDouble() < chance * cluster)
                {
                    float output = (float)Math.Round(Richness(resource) * (0.5 + random.NextDouble()), 1);
                    p.Deposits[(int)resource] = output;
                    // A pocket lasts 10 to 50 years at full output; small and large ones alike.
                    double years = MinDepositYears + (MaxDepositYears - MinDepositYears) * sizes.NextDouble();
                    p.DepositSizes[(int)resource] = (float)(Math.Round(output * 365 * years / 10) * 10);
                }
            }
        }
    }

    private static double Chance(ResourceType resource, Biome biome, double absLat)
    {
        bool rugged = biome is Biome.Hills or Biome.Mountains or Biome.HighMountains;
        bool tropical = absLat < 23;
        return resource switch
        {
            ResourceType.Coal => biome is Biome.TemperateForest or Biome.Taiga or Biome.Hills or Biome.Grassland or Biome.Steppe ? 0.08 : 0.02,
            ResourceType.Iron => rugged ? 0.12 : 0.04,
            ResourceType.Copper => rugged || biome == Biome.Desert ? 0.08 : 0.02,
            ResourceType.Silicon => biome == Biome.Desert ? 0.12 : 0.03,
            ResourceType.Oil => biome is Biome.Desert or Biome.Steppe or Biome.Tundra or Biome.Wetland ? 0.07 : 0.015,
            ResourceType.Aluminium => tropical && biome is Biome.Savanna or Biome.TropicalForest ? 0.08 : rugged ? 0.02 : 0.01,
            ResourceType.Rubber => tropical && biome is Biome.TropicalForest or Biome.Wetland ? 0.3 : 0,
            ResourceType.Gold => rugged ? 0.04 : 0.008,
            ResourceType.Silver => rugged ? 0.04 : 0.005,
            _ => 0,
        };
    }

    /// <summary>Typical daily output of a fully worked deposit.</summary>
    private static double Richness(ResourceType resource) => resource switch
    {
        ResourceType.Gold or ResourceType.Silver => 1,
        ResourceType.Rubber => 3,
        _ => 4,
    };
}
