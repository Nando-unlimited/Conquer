using Conquer.Game.Economy;
using Conquer.Game.Military;
using Conquer.Game.Simulation;
using Conquer.Game.World;
using Conquer.Presentation;

namespace Conquer.Tests;

/// <summary>The fog of war: what each nation sees, and the counters it hides.</summary>
[Collection("World")]
public class VisionTests(WorldFixture world)
{
    private readonly WorldMap _map = world.Map;

    [Fact]
    public void ANationSeesAroundItsUnitsAndScoutsSeeFurther()
    {
        var s = GameSession.Create(_map, 2, seed: 7, computerRivals: false);
        var home = _map.Provinces[s.Units.First(u => u.OwnerId == 0).ProvinceId];
        var seen = s.VisibleProvinces(0);
        Assert.Contains(home.Id, seen);
        Assert.All(home.Neighbors, n => Assert.Contains(n, seen));
        var twoAway = home.Neighbors.SelectMany(n => _map.Provinces[n].Neighbors).First(id => id != home.Id && !home.Neighbors.Contains(id));
        Assert.DoesNotContain(twoAway, seen);

        s.AddRegiment(0, home.Id, BattalionType.Scouts);
        Assert.Contains(twoAway, s.VisibleProvinces(0));
    }

    [Fact]
    public void WhatANationHasSeenStaysExploredAndIsSaved()
    {
        var s = GameSession.Create(_map, 2, seed: 7, computerRivals: false);
        var home = _map.Provinces[s.Units.First(u => u.OwnerId == 0).ProvinceId];
        var twoAway = home.Neighbors.SelectMany(n => _map.Provinces[n].Neighbors).First(id => id != home.Id && !home.Neighbors.Contains(id));
        s.VisibleProvinces(0);
        Assert.True(s.HasExplored(0, home.Id));
        Assert.False(s.HasExplored(0, twoAway));

        var scouts = s.AddRegiment(0, home.Id, BattalionType.Scouts);
        s.Step();
        Assert.True(s.HasExplored(0, twoAway));
        Assert.True(s.Claim(0, scouts.Id).Ok); // they may only be sent home on their own land
        Assert.True(s.Disband(0, scouts.Id).Ok);
        s.Step();
        Assert.DoesNotContain(twoAway, s.VisibleProvinces(0));
        Assert.True(s.HasExplored(0, twoAway));

        var loaded = GameSession.Load(_map, s.ToSave("test"));
        Assert.Equal(s.Human.Explored.Order(), loaded.Human.Explored.Order());
    }

    [Fact]
    public void UnexploredLandShowsNothing()
    {
        var game = new GameController(GameSession.Create(_map, 2, seed: 7, computerRivals: false));
        var s = game.Session;
        var rival = s.Units.First(u => u.OwnerId == 1 && u.CanFoundCity);
        Assert.True(s.FoundCity(1, rival.Id).Ok);
        var city = s.Cities.Single(c => c.OwnerId == 1);
        Assert.False(game.ExploredProvinces.Contains(city.ProvinceId));

        game.ViewProvince(city.ProvinceId); // on screen, but never seen
        Assert.Equal(-1, game.SelectedProvince);
        Assert.DoesNotContain(game.Markers().Cities, c => c.Id == city.Id);
        game.HoverProvince = city.ProvinceId;
        Assert.Equal("Tierra inexplorada", game.MapTooltip());
    }

    [Fact]
    public void EnemyUnitsOutOfSightAreHiddenUnlessAnAllySeesThem()
    {
        var game = new GameController(GameSession.Create(_map, 3, seed: 7));
        var s = game.Session;
        var home = _map.Provinces[s.Units.First(u => u.OwnerId == 0).ProvinceId];
        var far = _map.Provinces.First(p => p.IsClaimable && _map.DistanceKm(p, home) > 6000);
        var stranger = s.AddRegiment(1, far.Id, BattalionType.LightInfantry);

        Assert.False(s.CanSee(0, stranger));
        game.ViewProvince(far.Id); // on screen, so only the fog hides it
        Assert.DoesNotContain(game.Markers().Units, c => c.UnitId == stranger.Id);
        game.SelectUnit(stranger.Id);
        Assert.Null(game.SelectedUnit);

        // Rival 2 stands beside it and shares what it sees with its ally.
        s.AddRegiment(2, far.Id, BattalionType.LightInfantry);
        foreach (var p in s.Players) p.Stockpile[ResourceType.Gold] = 10_000;
        while (s.Opinion(2, 0) < Conquer.Game.Rules.GameRules.AllianceAcceptOpinion) Assert.True(s.SendGift(0, 2).Ok);
        Assert.True(s.ProposeAlliance(0, 2).Ok);
        s.Step();
        Assert.True(s.CanSee(0, stranger));
        Assert.Contains(game.Markers().Units, c => c.UnitId == stranger.Id);
    }
}
