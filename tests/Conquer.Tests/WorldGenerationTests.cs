using System.Diagnostics;
using Conquer.Game.World;
using Xunit.Abstractions;

namespace Conquer.Tests;

public class WorldGenerationTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(MapKind.Random)]
    [InlineData(MapKind.Earth)]
    public void GeneratesAboutTwentyFiveThousandProvinces(MapKind kind)
    {
        var sw = Stopwatch.StartNew();
        var map = WorldGenerator.Generate(new WorldSettings(kind, 1234));
        output.WriteLine($"{kind}: {map.Provinces.Count} provinces in {sw.ElapsedMilliseconds} ms");
        foreach (var g in map.Provinces.GroupBy(p => p.Biome).OrderBy(g => g.Key))
            output.WriteLine($"  {g.Key,-16} {g.Count(),6} provinces, avg {g.Average(p => p.AreaKm2),9:0} km2");

        Assert.InRange(map.Provinces.Count, 22000, 28000);
        Assert.DoesNotContain(map.ProvinceIds, id => id < 0 || id >= map.Provinces.Count);
    }
}
