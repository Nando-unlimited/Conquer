using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Science;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Tests;

/// <summary>Crossing the sea: by ship with navigation and cartography, or by air.</summary>
[Collection("World")]
public class NavalTests(WorldFixture world)
{
    private readonly WorldMap _map = world.Map;

    /// <summary>A coastal land province with a coastal sea next to it, and an ocean province somewhere.</summary>
    private (Province Coast, Province Sea, Province Ocean) Coast()
    {
        var coast = _map.Provinces.First(p => p.IsClaimable && p.Neighbors.Any(n => _map.Provinces[n].Biome == Biome.ShallowSea));
        var sea = _map.Provinces[coast.Neighbors.First(n => _map.Provinces[n].Biome == Biome.ShallowSea)];
        var ocean = _map.Provinces.First(p => p.Biome == Biome.Ocean);
        return (coast, sea, ocean);
    }

    [Fact]
    public void NavigationOpensCoastalSeasAndCartographyTheOcean()
    {
        var (coast, sea, ocean) = Coast();
        var s = GameSession.Create(_map, 1, seed: 7, computerRivals: false);
        var unit = s.AddRegiment(0, coast.Id, BattalionType.Warriors);

        Assert.False(s.CanUnitEnter(unit, sea.Id));
        Assert.StartsWith("Hace falta la navegación a vela", s.MoveUnit(0, unit.Id, sea.Id).Message);

        s.Human.Learn(Tech.Navigation);
        Assert.True(s.CanUnitEnter(unit, sea.Id));
        Assert.True(s.MoveUnit(0, unit.Id, sea.Id).Ok);
        Assert.False(s.CanUnitEnter(unit, ocean.Id));

        s.Human.Learn(Tech.Cartography);
        Assert.True(s.CanUnitEnter(unit, ocean.Id));
    }

    [Fact]
    public void ShipsSailFasterThanPeopleWalk()
    {
        var s = GameSession.Create(_map, 1, seed: 7, computerRivals: false);
        var (sea, next) = _map.Provinces.Where(p => p.IsWater)
            .SelectMany(p => p.Neighbors.Select(n => (p, _map.Provinces[n]))).First(t => t.Item2.IsWater);

        double hours = s.Pathfinder.StepHours(sea.Id, next.Id);

        Assert.Equal(_map.DistanceKm(sea, next) / (GameRules.CitizenSpeedKmh * GameRules.SailingSpeed), hours, 6);
    }

    [Fact]
    public void MigrantsAndSupplyStayOnLand()
    {
        var (coast, sea, _) = Coast();
        var s = GameSession.Create(_map, 1, seed: 7, computerRivals: false);
        s.Human.Learn(Tech.Navigation);

        Assert.False(s.Pathfinder.CanEnter(sea.Id));
        Assert.Null(s.Pathfinder.FindPath(coast.Id, sea.Id));
    }

    [Fact]
    public void BombersFlyOverTheSeaWithoutShips()
    {
        var (coast, _, ocean) = Coast();
        var s = GameSession.Create(_map, 1, seed: 7, computerRivals: false);
        var bombers = s.AddRegiment(0, coast.Id, BattalionType.Bombers);
        var infantry = s.AddRegiment(0, coast.Id, BattalionType.Warriors);

        Assert.True(bombers.Flies);
        Assert.True(s.CanUnitEnter(bombers, ocean.Id));
        Assert.False(infantry.Flies);
        Assert.False(s.CanUnitEnter(infantry, ocean.Id));
    }

    [Fact]
    public void AviationBringsBombers()
    {
        Assert.Contains(Tech.Aviation, BattalionType.Bombers.Info().Requires);
        Assert.True(BattalionType.Bombers.Info().Flies);
        Assert.Equal(Era.Modern, Tech.Aviation.Info().Era);
    }
}
