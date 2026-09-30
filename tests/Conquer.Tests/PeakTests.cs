using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Tests;

/// <summary>The Earth as the latest generator makes it, with peaks.</summary>
public sealed class PeakEarthFixture
{
    public WorldMap Map { get; } = WorldGenerator.Generate(new WorldSettings(MapKind.Earth, 1, Generator: WorldGenerator.LatestGenerator));
}

public class PeakTests(PeakEarthFixture earth) : IClassFixture<PeakEarthFixture>
{
    private readonly WorldMap _map = earth.Map;

    /// <summary>The peaks province with the most pixels: the Tibetan plateau and the Himalaya.</summary>
    private Province Tibet => _map.Provinces.Where(p => p.Biome == Biome.Peaks).MaxBy(p => p.PixelCount)!;

    [Fact]
    public void EachRangeAboveFiveThousandMetresIsOneProvince()
    {
        var tibet = Tibet;
        Assert.InRange(tibet.Latitude, 25, 40);
        Assert.InRange(tibet.Longitude, 75, 100);
        // Every peaks pixel of the range belongs to that one province, and none of them is low.
        for (int i = 0; i < _map.ProvinceIds.Length; i++)
        {
            if (_map.Biomes[i] != Biome.Peaks) continue;
            Assert.True(_map.Elevation[i] >= GameRules.PeakElevation);
            Assert.Equal(Biome.Peaks, _map.Provinces[_map.ProvinceIds[i]].Biome);
        }
        Assert.All(_map.Provinces.Where(p => p.Biome == Biome.Peaks), p => Assert.True(p.PixelCount >= GameRules.MinPeakPixels));
        // Antarctica's plateau is ice, not peaks.
        Assert.DoesNotContain(_map.Provinces, p => p.Biome == Biome.Peaks && Math.Abs(p.Latitude) > 60);
    }

    [Fact]
    public void PeaksCannotBeClaimedButTroopsCrossThem()
    {
        var s = GameSession.Create(_map, 1, seed: 7, computerRivals: false);
        var tibet = Tibet;
        var unit = s.AddRegiment(0, tibet.Id, BattalionType.Warriors);
        Assert.False(tibet.IsClaimable);
        Assert.Equal(WorldGenerator.LatestGenerator, s.ToSave("test").World.Generator); // so loading it makes the same map
        var claim = s.Claim(0, unit.Id);
        Assert.False(claim.Ok);
        Assert.Contains("cumbres", claim.Message);

        // A regiment on one side can march through the range to the other.
        var west = _map.Provinces[tibet.Neighbors.Where(n => !_map.Provinces[n].IsWater).MinBy(n => _map.Provinces[n].Longitude)];
        var east = _map.Provinces[tibet.Neighbors.Where(n => !_map.Provinces[n].IsWater).MaxBy(n => _map.Provinces[n].Longitude)];
        Assert.True(s.CanUnitEnter(unit, tibet.Id));
        var route = s.Pathfinder.FindPath(west.Id, east.Id, id => id == tibet.Id || id == west.Id || id == east.Id);
        Assert.NotNull(route);
        Assert.True(route.Value.Hours > 0);
        Assert.Contains(tibet.Id, route.Value.Path);
    }

    [Fact]
    public void OlderMapsHaveNoPeaks()
    {
        Assert.Equal(1, new WorldSettings(MapKind.Earth, 1).Generator);
        var older = WorldGenerator.Generate(new WorldSettings(MapKind.Earth, 1));
        Assert.DoesNotContain(older.Provinces, p => p.Biome == Biome.Peaks);
        Assert.DoesNotContain(older.Biomes, b => b == Biome.Peaks);
    }
}
