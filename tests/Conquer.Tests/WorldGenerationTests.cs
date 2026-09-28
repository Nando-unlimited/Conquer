using System.Diagnostics;
using Conquer.Game.Rules;
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

    [Theory]
    [InlineData(MapKind.Random)]
    [InlineData(MapKind.Earth)]
    public void TracesRivers(MapKind kind)
    {
        var sw = Stopwatch.StartNew();
        var map = WorldGenerator.Generate(new WorldSettings(kind, 1234));
        var land = map.Provinces.Where(p => !p.IsWater).ToList();
        int streams = land.Count(p => p.RiverFlow > 0), great = land.Count(p => p.HasRiver);
        output.WriteLine($"{kind}: {map.Rivers.Count} segments, max flow {map.Rivers.Max(r => r.Flow):0}, " +
                         $"{streams} of {land.Count} land provinces with a stream, {great} with a great river ({sw.ElapsedMilliseconds} ms)");

        Assert.NotEmpty(map.Rivers);
        Assert.All(map.Rivers, r => Assert.True(r.Flow >= GameRules.MinRiverFlow));
        Assert.All(map.Rivers, r => Assert.InRange(r.Y1, 0, map.Height));
        Assert.All(map.Rivers, r => Assert.InRange(r.X1, 0, map.Width));
        // Great rivers are notable but not everywhere.
        Assert.InRange(great, land.Count / 50, land.Count / 4);
        Assert.DoesNotContain(map.Provinces, p => p.IsWater && p.RiverFlow > 0);
    }
}
