using Conquer.Game.Buildings;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Science;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Tests;

/// <summary>Epidemics: a sick province loses people and mood, spreads the sickness, and is immune once it has passed.</summary>
[Collection("World")]
public class PlagueTests(WorldFixture world)
{
    private readonly WorldMap _map = world.Map;

    private static void RunHours(GameSession s, int hours)
    {
        for (int i = 0; i < hours; i++) s.Step();
    }

    /// <summary>The human's capital on grassland, with plenty of people.</summary>
    private (GameSession S, Province Capital) Capital()
    {
        var s = GameSession.Create(_map, 2, seed: 7, computerRivals: false);
        var a = _map.Provinces.First(p => p.Biome == Biome.Grassland && p.Neighbors.Length > 3);
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, a.Id, 300).Id);
        a.Population = 50000;
        return (s, a);
    }

    [Fact]
    public void ASickProvinceLosesPeopleAndMood()
    {
        var (s, a) = Capital();
        double healthyMood = s.TargetMood(a);
        s.StartPlague(a);
        Assert.True(GameSession.IsSick(a));
        Assert.Equal(GameRules.PlagueDays, a.PlagueDaysLeft);
        Assert.Equal(healthyMood - GameRules.PlagueMood, s.TargetMood(a), 3);
        Assert.Contains(s.MoodFactors(a), f => f.Reason == "Epidemia");
        double deaths = a.Population * s.PlagueDeaths(a);
        Assert.True(deaths > 0);
        double before = a.Population;
        RunHours(s, 24);
        Assert.True(a.Population < before);
        Assert.Equal(GameRules.PlagueDays - 1, a.PlagueDaysLeft);
    }

    [Fact]
    public void MedicineAndHospitalsResistIt()
    {
        var (s, a) = Capital();
        double plain = s.PlagueDeaths(a);
        Assert.Equal(GameRules.PlagueDeathRate, plain, 9);
        s.Human.Learn(Tech.Medicine);
        Assert.Equal(GameRules.PlagueDeathRate * 0.75, s.PlagueDeaths(a), 9);
        a.AddBuilding(BuildingType.Hospital);
        Assert.Equal(GameRules.PlagueDeathRate * 0.35, s.PlagueDeaths(a), 9);
        foreach (var tech in Techs.All) s.Human.Learn(tech);
        a.AddBuilding(BuildingType.HerbalistHut);
        Assert.Equal(GameRules.MaxPlagueResistance, s.PlagueResistance(a), 9);
    }

    [Fact]
    public void ItEndsAndLeavesTheProvinceImmune()
    {
        var (s, a) = Capital();
        s.StartPlague(a);
        RunHours(s, 24 * GameRules.PlagueDays);
        Assert.False(GameSession.IsSick(a));
        Assert.True(a.PlagueImmuneUntil > s.Date.Hours);
        Assert.Contains(s.Notifications, n => n.Text.Contains("ha terminado"));
    }

    [Fact]
    public void ItSpreadsToNeighbours()
    {
        var (s, a) = Capital();
        foreach (int n in a.Neighbors) _map.Provinces[n].Population = 1000;
        s.StartPlague(a);
        RunHours(s, 24 * GameRules.PlagueDays);
        // Each neighbour has a 1-in-200 chance a day for 90 days; with several neighbours one almost surely falls ill.
        Assert.Contains(a.Neighbors, n => GameSession.IsSick(_map.Provinces[n]) || _map.Provinces[n].PlagueImmuneUntil > 0);
    }

    [Fact]
    public void ItIsSaved()
    {
        var (s, a) = Capital();
        s.StartPlague(a);
        RunHours(s, 24 * 5);
        var loaded = GameSession.Load(_map, s.ToSave("test"));
        var b = loaded.Map.Provinces[a.Id];
        Assert.Equal(a.PlagueDaysLeft, b.PlagueDaysLeft);
        RunHours(s, 24 * GameRules.PlagueDays);
        loaded = GameSession.Load(_map, s.ToSave("test"));
        Assert.Equal(a.PlagueImmuneUntil, loaded.Map.Provinces[a.Id].PlagueImmuneUntil);
    }
}
