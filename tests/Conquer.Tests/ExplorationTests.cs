using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Tests;

[Collection("World")]
public class ExplorationTests(WorldFixture world)
{
    private readonly WorldMap _map = world.Map;

    private (GameSession Session, Province Capital) WithCapital()
    {
        var s = GameSession.Create(_map, 1, seed: 7, computerRivals: false);
        var capital = _map.Provinces.First(p => p.Biome == Biome.Grassland && p.Neighbors.Count(n => _map.Provinces[n].IsClaimable) > 3);
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, capital.Id, 300).Id, "Roma");
        return (s, capital);
    }

    [Fact]
    public void ExploringScoutsClaimFreeLandAlongTheBordersOnTheirOwn()
    {
        var (s, capital) = WithCapital();
        var scouts = s.AddRegiment(0, capital.Id, BattalionType.Scouts);
        int before = s.Human.Provinces.Count;

        var result = s.SetScoutOrders(0, scouts.Id, ScoutOrders.Claim);
        Assert.True(result.Ok, result.Message);
        Assert.True(scouts.IsMoving);
        int target = scouts.Destination!.Value;
        Assert.Contains(target, capital.Neighbors);

        for (int h = 0; h < 24 * 20; h++) s.Step();

        Assert.Equal(0, _map.Provinces[target].OwnerId);
        Assert.True(s.Human.Provinces.Count >= before + 2);
        Assert.Equal(ScoutOrders.Claim, scouts.ScoutOrders);
        // Every province it claimed touches land the nation already had.
        Assert.All(s.Human.Provinces, id => Assert.True(id == capital.Id || _map.Provinces[id].Neighbors.Any(n => _map.Provinces[n].OwnerId == 0)));
    }

    [Fact]
    public void ScoutsExploringOnlyDiscoverUnknownLandWithoutClaimingIt()
    {
        var (s, capital) = WithCapital();
        var scouts = s.AddRegiment(0, capital.Id, BattalionType.Scouts);
        s.VisibleProvinces(0);
        int provinces = s.Human.Provinces.Count, explored = s.Human.Explored.Count;

        var result = s.SetScoutOrders(0, scouts.Id, ScoutOrders.Explore);
        Assert.True(result.Ok, result.Message);
        Assert.True(scouts.IsMoving);
        Assert.DoesNotContain(scouts.Destination!.Value, s.Human.Explored);

        for (int h = 0; h < 24 * 20; h++) s.Step();

        Assert.Equal(provinces, s.Human.Provinces.Count);
        Assert.True(s.Human.Explored.Count > explored + 10);
        Assert.Equal(ScoutOrders.Explore, scouts.ScoutOrders);
        Assert.True(scouts.IsMoving);
        Assert.Equal(ScoutOrders.Explore, GameSession.Load(_map, s.ToSave("test")).UnitById(scouts.Id)!.ScoutOrders);
    }

    [Fact]
    public void OnlyScoutsExploreAndTheOptionIsSaved()
    {
        var (s, capital) = WithCapital();
        var warriors = s.AddRegiment(0, capital.Id, BattalionType.LightInfantry);
        Assert.False(s.SetScoutOrders(0, warriors.Id, ScoutOrders.Claim).Ok);
        Assert.Equal(ScoutOrders.None, warriors.ScoutOrders);

        var scouts = s.AddRegiment(0, capital.Id, BattalionType.Scouts);
        Assert.True(s.SetScoutOrders(0, scouts.Id, ScoutOrders.Claim).Ok);
        var loaded = GameSession.Load(_map, s.ToSave("test"));
        Assert.Equal(ScoutOrders.Claim, loaded.UnitById(scouts.Id)!.ScoutOrders);

        Assert.True(s.SetScoutOrders(0, scouts.Id, ScoutOrders.None).Ok);
        Assert.Equal(ScoutOrders.None, scouts.ScoutOrders);
    }
}
