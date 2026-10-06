using Conquer.Game.Buildings;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Tests;

[Collection("World")]
public class CultureTests(WorldFixture world)
{
    private readonly WorldMap _map = world.Map;

    /// <summary>
    /// Player 0 (human) has its capital in A; player 1 owns the neighbouring B and another province C beyond it. A
    /// war and a treaty hand B, with 1000 of player 1's people, to player 0. No computer rivals.
    /// </summary>
    private (GameSession S, Province A, Province B, Province C) ConqueredProvince()
    {
        var s = GameSession.Create(_map, 2, seed: 7, computerRivals: false);
        var (a, b) = _map.Provinces
            .Where(p => p.Biome == Biome.Grassland && p.Neighbors.Length > 3)
            .SelectMany(p => p.Neighbors.Select(n => (A: p, B: _map.Provinces[n])))
            .First(t => t.B.Biome == Biome.Grassland && t.B.Neighbors.Length > 3);
        var c = b.Neighbors.Select(n => _map.Provinces[n]).First(p => p.IsClaimable && p.Id != a.Id && !a.Neighbors.Contains(p.Id));
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, a.Id, 300).Id);
        foreach (var province in new[] { b, c })
        {
            var claimer = s.AddRegiment(1, province.Id, BattalionType.Scouts);
            s.Claim(1, claimer.Id);
            s.Disband(1, claimer.Id);
        }
        b.Population = 1000;
        Assert.Equal(1, b.CultureId);

        var regiment = s.AddRegiment(0, a.Id, BattalionType.LightInfantry);
        s.DeclareWar(0, 1);
        s.MoveUnit(0, regiment.Id, b.Id);
        for (int h = 0; h < 24 * 5 && !b.IsOccupied; h++) s.Step();
        s.MakePeace(0, 1, PeaceTerms.TakeOccupied);
        s.Disband(0, regiment.Id);
        Assert.Equal(0, b.OwnerId);
        return (s, a, b, c);
    }

    private static void RunDay(GameSession s)
    {
        for (int h = 0; h < 24; h++) s.Step();
    }

    [Fact]
    public void ConqueredPeopleKeepTheirCultureAndResentIt()
    {
        var (s, a, b, _) = ConqueredProvince();
        Assert.Equal(0, a.CultureId);
        Assert.Equal(1, b.CultureId);
        Assert.True(GameSession.HasForeignCulture(b));
        Assert.Contains(s.MoodFactors(b), f => f.Reason == $"Cultura de {s.Players[1].Name}" && f.Points == -GameRules.ForeignCultureMood);

        b.Assimilation = 0.5;
        Assert.Equal(-GameRules.ForeignCultureMood / 2, GameSession.ForeignCultureMood(b), 6);
    }

    [Fact]
    public void AssimilatedPeopleAdoptTheirRulersCulture()
    {
        var (s, _, b, _) = ConqueredProvince();
        b.Assimilation = 1 - GameSession.DailyAssimilation(b) / 2;
        RunDay(s);
        Assert.Equal(0, b.CultureId);
        Assert.False(GameSession.HasForeignCulture(b));
    }

    [Fact]
    public void ForeignProvinceInUnrestRejoinsTheNationOfItsCulture()
    {
        var (s, _, b, c) = ConqueredProvince();
        Assert.True(s.WouldSecede(b));
        b.Mood = 0;
        b.RevoltProgress = GameRules.RevoltDays - 1;
        RunDay(s);

        Assert.Equal(1, b.OwnerId);
        Assert.Contains(b.Id, s.Players[1].Provinces);
        Assert.True(b.Mood >= GameRules.LiberatedMood);
        Assert.Equal(0, b.RevoltProgress);
        Assert.Equal(1, c.OwnerId);
    }

    [Fact]
    public void AGarrisonHoldsTheRevoltBack()
    {
        var (s, _, b, _) = ConqueredProvince();
        s.AddRegiment(0, b.Id, BattalionType.LightInfantry);
        Assert.True(s.IsGarrisoned(b));
        b.Mood = 0;
        b.RevoltProgress = GameRules.RevoltDays - 1;
        RunDay(s);

        Assert.Equal(0, b.OwnerId);
        Assert.Equal(GameRules.RevoltDays - 1, b.RevoltProgress);
    }

    [Fact]
    public void OurOwnPeopleRiotInstead()
    {
        var (s, a, _, _) = ConqueredProvince();
        a.Population = 2000;
        a.AddBuilding(BuildingType.Granary);
        int buildings = a.Buildings.Count;
        Assert.False(s.WouldSecede(a));
        a.Mood = 0;
        a.RevoltProgress = GameRules.RevoltDays - 1;
        RunDay(s);

        Assert.Equal(0, a.OwnerId);
        Assert.Equal(buildings - 1, a.Buildings.Count);
        Assert.True(a.Population < 2000 * 0.95);
        Assert.True(a.Mood >= GameRules.AfterRiotMood);
    }

    [Fact]
    public void CalmProvincesForgetTheirAnger()
    {
        var (s, a, _, _) = ConqueredProvince();
        a.Mood = 80;
        a.RevoltProgress = 10;
        RunDay(s);
        Assert.Equal(10 - GameRules.RevoltCalmingPerDay, a.RevoltProgress, 6);
    }
}
