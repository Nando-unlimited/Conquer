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

        var result = s.SetAutoClaim(0, scouts.Id, true);
        Assert.True(result.Ok, result.Message);
        Assert.True(scouts.IsMoving);
        int target = scouts.Destination!.Value;
        Assert.Contains(target, capital.Neighbors);

        for (int h = 0; h < 24 * 20; h++) s.Step();

        Assert.Equal(0, _map.Provinces[target].OwnerId);
        Assert.True(s.Human.Provinces.Count >= before + 2);
        Assert.True(scouts.AutoClaim);
        // Every province it claimed touches land the nation already had.
        Assert.All(s.Human.Provinces, id => Assert.True(id == capital.Id || _map.Provinces[id].Neighbors.Any(n => _map.Provinces[n].OwnerId == 0)));
    }

    [Fact]
    public void OnlyScoutsExploreAndTheOptionIsSaved()
    {
        var (s, capital) = WithCapital();
        var warriors = s.AddRegiment(0, capital.Id, BattalionType.Warriors);
        Assert.False(s.SetAutoClaim(0, warriors.Id, true).Ok);
        Assert.False(warriors.AutoClaim);

        var scouts = s.AddRegiment(0, capital.Id, BattalionType.Scouts);
        Assert.True(s.SetAutoClaim(0, scouts.Id, true).Ok);
        var loaded = GameSession.Load(_map, s.ToSave("test"));
        Assert.True(loaded.UnitById(scouts.Id)!.AutoClaim);

        Assert.True(s.SetAutoClaim(0, scouts.Id, false).Ok);
        Assert.False(scouts.AutoClaim);
    }
}
