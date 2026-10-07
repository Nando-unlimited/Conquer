using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Tests;

/// <summary>Emplacing: a combat unit digs in where it stands, defends better and gets its supplies sooner.</summary>
[Collection("World")]
public class EmplacementTests(WorldFixture world)
{
    private readonly WorldMap _map = world.Map;

    private static void RunDays(GameSession s, int days)
    {
        for (int i = 0; i < days * 24; i++) s.Step();
    }

    private Province Grassland() => _map.Provinces.First(p => p.Biome == Biome.Grassland && p.Neighbors.Any(n => _map.Provinces[n].Biome == Biome.Grassland));

    [Fact]
    public void AnEmplacedUnitDigsInDayByDayAndDefendsBetter()
    {
        var s = GameSession.Create(_map, 1, seed: 7, computerRivals: false);
        var here = Grassland();
        var unit = s.AddRegiment(0, here.Id, BattalionType.LightInfantry);
        Assert.True(s.Emplace(0, unit.Id).Ok);
        Assert.True(GameSession.IsEmplaced(unit));
        Assert.False(s.CanEmplace(unit).Ok);
        RunDays(s, 1);
        Assert.Equal(1 / MilitaryRules.EmplacementDays, unit.Entrenchment, 6);
        RunDays(s, (int)MilitaryRules.EmplacementDays);
        Assert.Equal(1, unit.Entrenchment);

        double Fire(bool attacking) => s.Engage([unit], here, attacking).Sum(e => e.Fire);
        double defending = Fire(false), attacking = Fire(true);
        unit.EmplacedAt = null; // the same men, not dug in
        Assert.Equal(Fire(false) * (1 + MilitaryRules.EmplacementDefense), defending, 6);
        Assert.Equal(Fire(true), attacking, 6); // attacking gets nothing from it
    }

    [Fact]
    public void MovingLiftsTheEmplacement()
    {
        var s = GameSession.Create(_map, 1, seed: 7, computerRivals: false);
        var here = Grassland();
        var unit = s.AddRegiment(0, here.Id, BattalionType.LightInfantry);
        s.Emplace(0, unit.Id);
        RunDays(s, 2);
        Assert.True(unit.Entrenchment > 0);

        var next = here.Neighbors.First(n => _map.Provinces[n].Biome == Biome.Grassland);
        Assert.True(s.MoveUnit(0, unit.Id, next).Ok);
        Assert.False(GameSession.IsEmplaced(unit));
        Assert.Equal(0, unit.Entrenchment);
        Assert.Equal(0, GameSession.EmplacementBonus(unit));
    }

    [Fact]
    public void TheEmplacementIsSaved()
    {
        var s = GameSession.Create(_map, 1, seed: 7, computerRivals: false);
        var unit = s.AddRegiment(0, Grassland().Id, BattalionType.LightInfantry);
        s.Emplace(0, unit.Id);
        RunDays(s, 2);
        var loaded = GameSession.Load(_map, s.ToSave("test")).UnitById(unit.Id)!;
        Assert.True(GameSession.IsEmplaced(loaded));
        Assert.Equal(unit.Entrenchment, loaded.Entrenchment);
    }
}
