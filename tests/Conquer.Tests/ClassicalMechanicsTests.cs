using Conquer.Game.Buildings;
using Conquer.Game.Entities;
using Conquer.Game.Rules;
using Conquer.Game.Science;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Tests;

/// <summary>Walls, roads, faster building and administration.</summary>
[Collection("World")]
public class ClassicalMechanicsTests(WorldFixture world)
{
    private readonly WorldMap _map = world.Map;

    private (Province A, Province B) GrasslandPair() =>
        _map.Provinces
            .Where(p => p.Biome == Biome.Grassland)
            .SelectMany(p => p.Neighbors.Select(n => (A: p, B: _map.Provinces[n])))
            .First(t => t.B.Biome == Biome.Grassland && t.A.Neighbors.Length > 3);

    private GameSession WithCapital(Province a)
    {
        var s = GameSession.Create(_map, 1, seed: 7, computerRivals: false);
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, a.Id, 300).Id);
        return s;
    }

    [Fact]
    public void WallsMakeDefendersHitHarder()
    {
        var (a, _) = GrasslandPair();
        WithCapital(a);
        double open = GameSession.DefenseMultiplier(a);

        a.AddBuilding(BuildingType.Walls);

        Assert.Equal(open * 1.5, GameSession.DefenseMultiplier(a), 6);
    }

    [Fact]
    public void RoadsShortenTheMarch()
    {
        var (a, b) = GrasslandPair();
        var s = WithCapital(a);
        double hours = s.Pathfinder.StepHours(a.Id, b.Id);

        s.Roads.Lay(a.Id, b.Id, RoadKind.Road);

        Assert.Equal(hours / 1.5, s.Pathfinder.StepHours(a.Id, b.Id), 6);
        Assert.Equal(s.Pathfinder.StepHours(a.Id, b.Id), s.Pathfinder.FindPath(a.Id, b.Id)!.Value.Hours, 6);
    }

    [Fact]
    public void ConstructionSpeedsUpBuilding()
    {
        var (a, _) = GrasslandPair();
        var s = WithCapital(a);
        s.Human.Stockpile[Conquer.Game.Economy.ResourceType.Wood] = 1000;
        s.Human.Learn(Tech.Construction);

        Assert.True(s.Build(0, a.Id, BuildingType.Farm).Ok);

        Assert.Equal((int)Math.Ceiling(BuildingType.Farm.Info().Days / 1.25), a.ConstructionDaysLeft);
    }

    [Fact]
    public void AdministrationHalvesTheMoodLostToDistance()
    {
        var (a, _) = GrasslandPair();
        var s = WithCapital(a);
        var far = _map.Provinces.First(p => p.IsClaimable && !p.IsOwned && _map.DistanceKm(a, p) > 600);
        s.Claim(0, s.AddRegiment(0, far.Id, Conquer.Game.Military.BattalionType.Warriors).Id);
        double Distance() => s.MoodFactors(far).Single(f => f.Reason == "Lejos de la capital").Points;
        double before = Distance();

        s.Human.Learn(Tech.Administration);

        Assert.Equal(-GameRules.MaxDistanceMoodPenalty, before);
        Assert.Equal(before / 2, Distance(), 6);
    }
}
