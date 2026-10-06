using Conquer.Game.Buildings;
using Conquer.Game.Economy;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Tests;

/// <summary>The reserve of recruits: what it holds, what draws on it and how it refills.</summary>
[Collection("World")]
public class ManpowerTests(WorldFixture world)
{
    private readonly WorldMap _map = world.Map;

    /// <summary>The human's capital with 2,000 people, barracks and plenty to pay with.</summary>
    private (GameSession S, Province Capital) Capital()
    {
        var s = GameSession.Create(_map, 1, seed: 7, computerRivals: false);
        var settlers = s.Units.Single();
        Assert.True(s.FoundCity(0, settlers.Id).Ok);
        var capital = _map.Provinces[s.Cities.Single().ProvinceId];
        capital.Population = 2000;
        capital.AddBuilding(BuildingType.Barracks);
        foreach (var r in Resources.All) s.Human.Stockpile[r] = 10_000;
        return (s, capital);
    }

    [Fact]
    public void TheReserveHoldsABasePlusAShareOfThePeople()
    {
        var (s, _) = Capital();
        Assert.Equal(GameRules.BaseManpower, s.Human.Manpower);
        Assert.Equal(GameRules.BaseManpower + 2000 * GameRules.ManpowerShare, s.ManpowerCapacity(s.Human), 6);
    }

    [Fact]
    public void TrainingDrawsOnTheReserveUntilItRunsOut()
    {
        var (s, capital) = Capital();
        s.Human.Manpower = 150;
        Assert.True(s.Train(0, capital.Id, BattalionType.LightInfantry).Ok);
        Assert.Equal(50, s.Human.Manpower, 6);
        var lack = s.Train(0, capital.Id, BattalionType.LightInfantry);
        Assert.False(lack.Ok);
        Assert.StartsWith("Faltan reclutas", lack.Message);
        Assert.True(s.Train(0, capital.Id, BattalionType.Scouts).Ok); // 50 men still fit
        Assert.False(s.CanRaiseHeadquarters(capital, 1).Ok);
    }

    [Fact]
    public void TheReserveRefillsAndDisbandedMenComeBack()
    {
        var (s, capital) = Capital();
        s.Human.Manpower = 0;
        double perDay = s.ManpowerPerDay(s.Human);
        for (int h = 0; h < 24; h++) s.Step();
        Assert.InRange(s.Human.Manpower, perDay * 0.9, perDay * 1.1);

        double before = s.Human.Manpower;
        var regiment = s.AddRegiment(0, capital.Id, BattalionType.LightInfantry);
        Assert.True(s.Disband(0, regiment.Id).Ok);
        Assert.Equal(Math.Min(s.ManpowerCapacity(s.Human), before + 100), s.Human.Manpower, 6);

        var loaded = GameSession.Load(_map, s.ToSave("test"));
        Assert.Equal(s.Human.Manpower, loaded.Human.Manpower, 6);
        // Saves from before the reserve start with it full.
        var old = s.ToSave("test");
        old = old with { Players = [.. old.Players.Select(p => p with { Manpower = null })] };
        var full = GameSession.Load(_map, old);
        Assert.Equal(full.ManpowerCapacity(full.Human), full.Human.Manpower, 6);
    }

    [Fact]
    public void ReinforcementsNeedRecruits()
    {
        var (s, capital) = Capital();
        var regiment = s.AddRegiment(0, capital.Id, BattalionType.LightInfantry);
        regiment.Battalions[0].Strength = 50;
        s.Human.Manpower = 0;
        for (int h = 0; h < 24; h++) s.Step();
        Assert.InRange(regiment.Battalions[0].Strength, 50, 50 + s.ManpowerPerDay(s.Human) * 1.05); // only what the reserve gained that day
    }
}
