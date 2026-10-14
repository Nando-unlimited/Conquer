using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Science;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Tests;

/// <summary>The migration engine: where a city's emigrants go, and for how long.</summary>
[Collection("World")]
public class MigrationEngineTests(WorldFixture world)
{
    private readonly WorldMap _map = world.Map;

    private GameSession NewSession() => GameSession.Create(_map, 1, seed: 7, computerRivals: false);

    private static void RunHours(GameSession s, int hours)
    {
        for (int i = 0; i < hours; i++) s.Step();
    }

    private static void FoundCity(GameSession s, Province p, double fill)
    {
        Assert.True(s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, p.Id, 300).Id).Ok);
        p.Population = fill * s.CapacityOf(p);
    }

    private static void Claim(GameSession s, Province p, double people)
    {
        var scouts = s.AddRegiment(0, p.Id, BattalionType.Scouts);
        Assert.True(s.Claim(0, scouts.Id).Ok);
        s.Disband(0, scouts.Id);
        p.Population = people;
    }

    /// <summary>A province with two land neighbours that do not touch each other: room for a city on each side.</summary>
    private (Province Near, Province Middle, Province Far) Row()
    {
        foreach (var middle in _map.Provinces.Where(p => p.Biome == Biome.Grassland))
            foreach (int a in middle.Neighbors)
                foreach (int b in middle.Neighbors)
                {
                    var (pa, pb) = (_map.Provinces[a], _map.Provinces[b]);
                    if (a == b || !pa.IsClaimable || !pb.IsClaimable || pa.Neighbors.Contains(b)) continue;
                    if (pa.Neighbors.Any(n => _map.Provinces[n].Neighbors.Contains(b) && n != middle.Id)) continue;
                    return (pa, middle, pb);
                }
        throw new InvalidOperationException("No row of three provinces.");
    }

    [Fact]
    public void TheRulesWeighEmptinessMoreThanDistance()
    {
        // A province wants half its capacity, or as full as its city if that is fuller.
        Assert.Equal(500, MigrationEngine.Wanted(1000, 0.2));
        Assert.Equal(800, MigrationEngine.Wanted(1000, 0.8));
        // Ten days away it pulls half as hard as next door; with nobody living there yet, ten times as hard.
        Assert.Equal(MigrationEngine.Pull(100, 500, 0, false) / 2, MigrationEngine.Pull(100, 500, 24 * GameRules.MigrationDistanceDays, false), 9);
        Assert.Equal(MigrationEngine.Pull(100, 500, 24, false) * GameRules.UnsettledMigrationPull, MigrationEngine.Pull(100, 500, 24, true), 9);
    }

    [Fact]
    public void AProvinceTakesItsMigrantsFromTheNearestCityThatSendsThem()
    {
        var s = NewSession();
        var (near, middle, far) = Row();
        // The near city is new and has room to spare: it sends nobody, so the province takes the far one's people.
        FoundCity(s, near, 0.05);
        FoundCity(s, far, 1);
        Claim(s, middle, 0);
        Assert.False(s.MigrationEngine.IsEmigrating(near));
        Assert.True(s.MigrationEngine.IsEmigrating(far));

        RunHours(s, 24);
        var migration = Assert.Single(s.Migrations);
        Assert.Equal(far.Id, migration.FromProvinceId);
        Assert.Equal(middle.Id, migration.ToProvinceId);
        Assert.Equal(GameRules.MigrationGroup, migration.People);
    }

    [Fact]
    public void IndustryBringsABoomBigCitiesAndAnExodusFromTheCountryside()
    {
        Assert.True(GameRules.EraGrowth(Era.Industrial) > GameRules.EraGrowth(Era.Renaissance));
        Assert.True(GameRules.EraGrowth(Era.Modern) < GameRules.EraGrowth(Era.Industrial));
        Assert.Equal(GameRules.CityCapacityMultiplier, GameRules.CityCapacity(Era.Renaissance));
        Assert.True(GameRules.CityCapacity(Era.Modern) > GameRules.CityCapacity(Era.Industrial));
        // The countryside leaves from the same fill as cities do, and a city wants to be a gap fuller than it, up to a ceiling.
        Assert.Equal(0, GameRules.UrbanizationShareOf(GameRules.EmigrationStartFill));
        Assert.Equal(GameRules.UrbanizationShare, GameRules.UrbanizationShareOf(1));
        Assert.Equal(1000 * (0.4 + GameRules.UrbanizationGap), MigrationEngine.UrbanWanted(1000, 0.4), 9);
        Assert.Equal(1000 * GameRules.UrbanizationCityFill, MigrationEngine.UrbanWanted(1000, 0.9), 9);
    }

    private static void Industrialise(GameSession s)
    {
        foreach (var tech in Techs.All.Where(t => t.Info().Era <= Era.Industrial)) s.Human.Learn(tech);
        Assert.Equal(Era.Industrial, s.Human.Era);
    }

    [Fact]
    public void InAnIndustrialNationTheCountrysideMovesToTheCities()
    {
        var s = NewSession();
        var (city, province, _) = Row();
        FoundCity(s, city, 0.5);
        Claim(s, province, 0);
        Industrialise(s);
        city.Population = 0.5 * s.CapacityOf(city);
        province.Population = 0.8 * s.CapacityOf(province);
        for (int day = 0; day < 60 && s.Migrations.Count == 0; day++) RunHours(s, 24);
        var migration = Assert.Single(s.Migrations);
        Assert.Equal(province.Id, migration.FromProvinceId);
        Assert.Equal(city.Id, migration.ToProvinceId);

        // A full city sends nobody to its empty countryside any more.
        var full = NewSession();
        FoundCity(full, city, 0.9);
        Claim(full, province, 0);
        Industrialise(full);
        city.Population = 0.9 * full.CapacityOf(city);
        province.Population = 0.1 * full.CapacityOf(province);
        RunHours(full, 24 * 30);
        Assert.Equal(0, full.MigrationEngine.NextId);
    }

    [Fact]
    public void MigrationGoesOnWhileTheCityIsFullerThanItsCountryside()
    {
        var s = NewSession();
        var (city, province, _) = Row();
        FoundCity(s, city, 0.9);
        // Past half its capacity, but emptier than the city: it still draws migrants.
        Claim(s, province, s.CapacityOf(province) * 0.6);
        RunHours(s, 24 * 30);
        Assert.True(s.MigrationEngine.NextId > 0, "No group has left for the province.");

        // As full as the city: nobody more comes.
        var calm = NewSession();
        FoundCity(calm, city, 0.9);
        Claim(calm, province, calm.CapacityOf(province) * 0.95);
        RunHours(calm, 24 * 30);
        Assert.Equal(0, calm.MigrationEngine.NextId);
    }
}
