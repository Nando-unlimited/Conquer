using Conquer.Game.Economy;
using Conquer.Game.Rules;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Tests;

/// <summary>Generates one random world shared by every test in the "World" collection.</summary>
public sealed class WorldFixture
{
    public WorldMap Map { get; } = WorldGenerator.Generate(new WorldSettings(MapKind.Random, 42));
}

[CollectionDefinition("World")]
public sealed class WorldCollection : ICollectionFixture<WorldFixture>;

[Collection("World")]
public class GameplayTests(WorldFixture world)
{
    private readonly WorldMap _map = world.Map;

    private GameSession NewSession(int players = 1) => GameSession.Create(_map, players, seed: 7);

    /// <summary>A fertile province with a fertile neighbour, both without neighbouring cities.</summary>
    private (Province A, Province B) GrasslandPair() =>
        _map.Provinces
            .Where(p => p.Biome == Biome.Grassland)
            .SelectMany(p => p.Neighbors.Select(n => (A: p, B: _map.Provinces[n])))
            .First(t => t.B.Biome == Biome.Grassland && t.A.Neighbors.Length > 3);

    private static void RunHours(GameSession s, int hours)
    {
        for (int i = 0; i < hours; i++) s.Step();
    }

    [Fact]
    public void GameStartsWithNoTerritoryAndOneBandOfSettlersEach()
    {
        var s = NewSession(players: 4);

        Assert.All(_map.Provinces, p => Assert.False(p.IsOwned));
        Assert.Equal(4, s.Units.Count);
        foreach (var player in s.Players)
        {
            var unit = Assert.Single(s.Units, u => u.OwnerId == player.Id);
            Assert.Equal(UnitType.Settlers, unit.Type);
            Assert.Equal(300, unit.Citizens);
            Assert.True(_map.Provinces[unit.ProvinceId].IsClaimable);
            Assert.Equal(600, player.Stockpile[ResourceType.Food]);
            Assert.Equal(50, player.Stockpile[ResourceType.Gold]);
            Assert.Equal(100, player.Stockpile[ResourceType.Wood]);
        }
        Assert.Equal("1 ene 4000 a.C., 00:00", s.Date.ToString());
    }

    [Fact]
    public void SettlersClaimTheirProvinceAndFoundTheCapital()
    {
        var s = NewSession();
        var settlers = s.Units.Single();
        var province = _map.Provinces[settlers.ProvinceId];

        var result = s.FoundCity(0, settlers.Id);

        Assert.True(result.Ok, result.Message);
        Assert.Empty(s.Units);
        Assert.Equal(0, province.OwnerId);
        Assert.Equal(300, province.Population);
        var city = Assert.Single(s.Cities);
        Assert.Equal(city.Id, s.Human.CapitalCityId);
        Assert.Contains(province.Id, s.Human.Provinces);
    }

    [Fact]
    public void OceansAndPolesCannotBeClaimed()
    {
        var s = NewSession();
        var ocean = _map.Provinces.First(p => p.Biome == Biome.Ocean);
        var ice = _map.Provinces.First(p => p.Biome == Biome.PolarIce);
        var settlers = s.AddUnit(0, UnitType.Settlers, ocean.Id, 300);
        var warriors = s.AddUnit(0, UnitType.Warriors, ice.Id, 100);

        Assert.False(s.FoundCity(0, settlers.Id).Ok);
        Assert.False(s.Claim(0, warriors.Id).Ok);
        Assert.False(ocean.IsOwned);
        Assert.False(ice.IsOwned);
    }

    [Fact]
    public void LandUnitsCannotEnterTheSeaButCanCrossPolarIce()
    {
        var s = NewSession();
        var coast = _map.Provinces.First(p => p.IsClaimable && p.Neighbors.Any(n => _map.Provinces[n].Biome == Biome.ShallowSea));
        var sea = coast.Neighbors.First(n => _map.Provinces[n].IsWater);
        var warriors = s.AddUnit(0, UnitType.Warriors, coast.Id, 100);

        Assert.False(s.MoveUnit(0, warriors.Id, sea).Ok);
        Assert.False(warriors.IsMoving);

        var ice = _map.Provinces.First(p => p.Biome == Biome.PolarIce && p.Neighbors.Any(n => _map.Provinces[n].IsClaimable));
        var iceShore = ice.Neighbors.First(n => _map.Provinces[n].IsClaimable);
        Assert.NotNull(s.Pathfinder.FindPath(iceShore, ice.Id));
    }

    /// <summary>Two claimable provinces on land masses that only the sea connects.</summary>
    private (Province A, Province B) ProvincesAcrossTheSea()
    {
        var reachable = Reach(_map.Provinces.First(p => p.Biome == Biome.Grassland).Id);
        var a = _map.Provinces.First(p => reachable.Contains(p.Id) && p.Biome == Biome.Grassland);
        var b = _map.Provinces.First(p => p.IsClaimable && p.Info.Carrying > 1 && !reachable.Contains(p.Id));
        return (a, b);
    }

    private HashSet<int> Reach(int start)
    {
        var seen = new HashSet<int> { start };
        var stack = new Stack<int>([start]);
        while (stack.Count > 0)
            foreach (int n in _map.Provinces[stack.Pop()].Neighbors)
                if (!_map.Provinces[n].IsWater && seen.Add(n)) stack.Push(n);
        return seen;
    }

    [Fact]
    public void NothingWalksAcrossTheSea()
    {
        var s = NewSession();
        var (a, b) = ProvincesAcrossTheSea();
        var settlers = s.AddUnit(0, UnitType.Settlers, a.Id, 300);
        s.FoundCity(0, settlers.Id);
        a.Population = 5000;
        s.Claim(0, s.AddUnit(0, UnitType.Warriors, b.Id, 100).Id);

        Assert.Null(s.Pathfinder.FindPath(a.Id, b.Id));
        Assert.False(s.ForceMigration(0, a.Id, b.Id, 100).Ok);
        RunHours(s, 24 * 5);
        Assert.DoesNotContain(s.Migrations, m => m.ToProvinceId == b.Id);
    }

    [Fact]
    public void DesertsPolesAndOceansHaveLargerProvinces()
    {
        double Average(Biome b) => _map.Provinces.Where(p => p.Biome == b).Average(p => p.AreaKm2);
        double grassland = Average(Biome.Grassland);
        Assert.True(Average(Biome.Desert) > 2 * grassland);
        Assert.True(Average(Biome.Ocean) > 5 * grassland);
        Assert.True(Average(Biome.PolarIce) > 5 * grassland);
    }

    [Fact]
    public void OnlyMilitaryUnitsClaimFreeLand()
    {
        var s = NewSession();
        var (a, _) = GrasslandPair();
        var settlers = s.AddUnit(0, UnitType.Settlers, a.Id, 300);
        var warriors = s.AddUnit(0, UnitType.Warriors, a.Id, 100);

        Assert.False(s.Claim(0, settlers.Id).Ok);
        Assert.True(s.Claim(0, warriors.Id).Ok);
        Assert.Equal(0, a.OwnerId);
        Assert.False(s.Claim(0, warriors.Id).Ok);
    }

    [Fact]
    public void UnitsWalkAtTenKilometresAnHourOnOpenGround()
    {
        var s = NewSession();
        var (a, b) = GrasslandPair();
        var unit = s.AddUnit(0, UnitType.Warriors, a.Id, 100);
        double expected = _map.DistanceKm(a, b) / GameRules.CitizenSpeedKmh;

        Assert.Equal(expected, s.Pathfinder.StepHours(a.Id, b.Id), 6);
        Assert.True(s.MoveUnit(0, unit.Id, b.Id).Ok);
        RunHours(s, (int)Math.Floor(expected) - 1);
        Assert.Equal(a.Id, unit.ProvinceId);
        RunHours(s, 2);
        Assert.Equal(b.Id, unit.ProvinceId);
        Assert.False(unit.IsMoving);
    }

    [Fact]
    public void CitiesSendMigrantsDailyToClaimedTerritory()
    {
        var s = NewSession();
        var (a, b) = GrasslandPair();
        var settlers = s.AddUnit(0, UnitType.Settlers, a.Id, 300);
        Assert.True(s.FoundCity(0, settlers.Id).Ok);
        a.Population = 5000;
        var warriors = s.AddUnit(0, UnitType.Warriors, b.Id, 100);
        Assert.True(s.Claim(0, warriors.Id).Ok);

        RunHours(s, 24); // reaches midnight: the day's migrants leave
        var migration = Assert.Single(s.Migrations);
        Assert.Equal(a.Id, migration.FromProvinceId);
        Assert.Equal(b.Id, migration.ToProvinceId);
        Assert.Equal(Math.Ceiling(s.Pathfinder.StepHours(a.Id, b.Id)), migration.ArriveHours - migration.DepartHours);

        long arrivalIn = migration.ArriveHours - s.Date.Hours;
        RunHours(s, (int)arrivalIn);
        Assert.InRange(b.Population, migration.People, migration.People * 1.01);
    }

    [Fact]
    public void ForcedMigrationCostsGoldAndTakesTheTravelTime()
    {
        var s = NewSession();
        var (a, b) = GrasslandPair();
        var settlers = s.AddUnit(0, UnitType.Settlers, a.Id, 300);
        s.FoundCity(0, settlers.Id);
        s.Claim(0, s.AddUnit(0, UnitType.Warriors, b.Id, 100).Id);
        double gold = s.Human.Stockpile[ResourceType.Gold];

        Assert.False(s.ForceMigration(0, a.Id, b.Id, 250).Ok); // the city must keep 100 people
        var result = s.ForceMigration(0, a.Id, b.Id, 150);

        Assert.True(result.Ok, result.Message);
        Assert.Equal(gold - 15, s.Human.Stockpile[ResourceType.Gold]);
        Assert.Equal(150, a.Population);
        var m = Assert.Single(s.Migrations);
        Assert.True(m.Forced);
        RunHours(s, (int)(m.ArriveHours - s.Date.Hours));
        Assert.True(b.Population >= 150);
    }

    [Fact]
    public void SettlersEatFromTheStartingFood()
    {
        var s = NewSession();
        RunHours(s, 24);
        Assert.Equal(600 - 300 * GameRules.FoodPerCitizen, s.Human.Stockpile[ResourceType.Food], 6);
    }

    [Fact]
    public void TheCapitalGrowsHappierAndMoreFertile()
    {
        var s = NewSession();
        var (a, _) = GrasslandPair();
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, a.Id, 300).Id);
        double target = GameRules.BaseMood + GameRules.CityMood + GameRules.CapitalMood;

        Assert.Equal(target, s.TargetMood(a));
        RunHours(s, 24 * 60);
        Assert.True(a.Mood > target - 1, $"mood {a.Mood}");
        Assert.InRange(a.Mood, s.TargetMood(a) - 2, s.TargetMood(a) + 2);
        Assert.True(a.Fertility > 1.05, $"fertility {a.Fertility}");
    }

    [Fact]
    public void FoodReservesCheerPeopleUp()
    {
        var s = NewSession();
        var (a, _) = GrasslandPair();
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, a.Id, 300).Id);
        s.Human.Stockpile[ResourceType.Food] = 300 * GameRules.FoodPerCitizen * GameRules.FoodReserveFullDays * 2;

        RunHours(s, 24);
        Assert.True(s.Human.FoodReserveDays > GameRules.FoodReserveFullDays);
        Assert.Contains(s.MoodFactors(a), f => f.Reason == "Reservas de comida" && f.Points == GameRules.FoodReserveMood);
    }

    [Fact]
    public void FestivalsCostGoldAndLiftTheCityMoodForAMonth()
    {
        var s = NewSession();
        var (a, _) = GrasslandPair();
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, a.Id, 300).Id);
        var city = s.CityIn(a)!;
        double gold = s.Human.Stockpile[ResourceType.Gold];
        double before = s.TargetMood(a);

        Assert.True(s.HoldFestival(0, city.Id).Ok);
        Assert.Equal(gold - GameRules.FestivalCost(300), s.Human.Stockpile[ResourceType.Gold]);
        Assert.False(s.HoldFestival(0, city.Id).Ok); // one at a time
        Assert.Contains(s.MoodFactors(a), f => f.Reason == "Fiestas");
        Assert.True(s.TargetMood(a) > before);

        RunHours(s, 24 * GameRules.FestivalDays + 1);
        Assert.DoesNotContain(s.MoodFactors(a), f => f.Reason == "Fiestas");
        Assert.True(s.CanHoldFestival(city).Ok || s.Human.Stockpile[ResourceType.Gold] < GameRules.FestivalCost(a.Population));
    }

    [Fact]
    public void HungerMakesPeopleUnhappyAndLessFertile()
    {
        var s = NewSession();
        var (a, _) = GrasslandPair();
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, a.Id, 300).Id);
        a.Population = 10 * s.CapacityOf(a); // far more mouths than the land feeds
        s.Human.Stockpile[ResourceType.Food] = 0;

        RunHours(s, 24 * 30);
        Assert.True(s.Human.IsStarving);
        Assert.Contains(s.MoodFactors(a), f => f.Reason == "Hambre");
        Assert.True(a.Mood < GameRules.UnrestMood, $"mood {a.Mood}");
        Assert.True(a.Fertility < 1, $"fertility {a.Fertility}");
    }

    [Fact]
    public void ForcedMigrantsArriveUnhappy()
    {
        var s = NewSession();
        var (a, b) = GrasslandPair();
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, a.Id, 300).Id);
        s.Claim(0, s.AddUnit(0, UnitType.Warriors, b.Id, 100).Id);
        double originMood = a.Mood;

        Assert.True(s.ForceMigration(0, a.Id, b.Id, 150).Ok);
        var m = Assert.Single(s.Migrations);
        Assert.Equal(originMood - GameRules.ForcedMigrantMoodPenalty, m.Mood);
        RunHours(s, (int)(m.ArriveHours - s.Date.Hours));
        Assert.True(b.Mood < originMood - GameRules.ForcedMigrantMoodPenalty / 2, $"mood {b.Mood}");
    }

    [Fact]
    public void ProvincesInUnrestPayNoTaxes()
    {
        var s = NewSession();
        var (a, _) = GrasslandPair();
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, a.Id, 300).Id);
        RunHours(s, 24);
        Assert.True(s.Human.LastDayNet[(int)ResourceType.Gold] > 0);

        a.Mood = GameRules.UnrestMood - 10;
        RunHours(s, 24);
        Assert.Equal(0, s.Human.LastDayNet[(int)ResourceType.Gold]);
    }

    [Fact]
    public void FertilityDrivesPopulationGrowth()
    {
        double GrowthOverADay(double fertility)
        {
            var s = NewSession();
            var (a, _) = GrasslandPair();
            s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, a.Id, 300).Id);
            a.Fertility = fertility;
            RunHours(s, 24);
            return a.Population - 300;
        }

        Assert.True(GrowthOverADay(1.5) > 2 * GrowthOverADay(0.5));
    }

    [Fact]
    public void NationStatsAddUpPeopleWhereverTheyAre()
    {
        var s = NewSession();
        var (a, b) = GrasslandPair();
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, a.Id, 300).Id);
        s.Claim(0, s.AddUnit(0, UnitType.Warriors, b.Id, 100).Id);
        a.Population = 1000;
        a.Mood = 80;
        b.Population = 500;
        b.Mood = 20;
        s.ForceMigration(0, a.Id, b.Id, 100);

        var stats = s.Stats(s.Human);
        Assert.Equal(1400, stats.Settled);
        Assert.Equal(300 + 100, stats.InUnits); // the starting settlers are still waiting, plus the warriors
        Assert.Equal(100, stats.Migrating);
        Assert.Equal(1900, stats.Total);
        Assert.Equal((2, 1, 2), (stats.Provinces, stats.Cities, stats.Units));
        Assert.Equal((900 * 80 + 500 * 20) / 1400.0, stats.AverageMood, 6);
        Assert.Equal(500, stats.PopulationByMood[0]); // unrest
        Assert.Equal(900, stats.PopulationByMood[3]); // content
    }

    [Fact]
    public void EveryDepositIsAFinitePocket()
    {
        NewSession();
        var deposits = _map.Provinces.SelectMany(p => Resources.Deposits.Where(r => p.Deposits[(int)r] > 0).Select(r => (p, r))).ToList();

        Assert.NotEmpty(deposits);
        Assert.All(deposits, d =>
        {
            float size = d.p.DepositSizes[(int)d.r];
            // Between 10 and 50 years of full output, and the game starts with the whole pocket.
            Assert.InRange(size, d.p.Deposits[(int)d.r] * 365 * 10 - 10, d.p.Deposits[(int)d.r] * 365 * 50 + 10);
            Assert.Equal(size * GameRules.DepositSizeMultiplier, d.p.Reserves[(int)d.r]);
        });
    }

    [Fact]
    public void DepositsRunDryAndStopProducing()
    {
        var s = NewSession();
        var p = _map.Provinces.First(p => p.IsClaimable && p.Neighbors.Length > 3 && p.HasDeposit(ResourceType.Iron));
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, p.Id, 300).Id);
        p.Population = GameRules.DepositFullWorkers;
        p.Reserves[(int)ResourceType.Iron] = 1.5; // less than a day's output

        RunHours(s, 24);
        Assert.Equal(1.5, s.Human.LastDayNet[(int)ResourceType.Iron], 6);
        Assert.False(p.HasDeposit(ResourceType.Iron));
        Assert.Contains(s.Notifications, n => n.Text.Contains("agotado"));

        RunHours(s, 24);
        Assert.Equal(0, s.Human.LastDayNet[(int)ResourceType.Iron]);
        Assert.Equal(1.5, s.Human.Stockpile[ResourceType.Iron], 6);
    }

    [Fact]
    public void ComputerRivalsFoundCitiesAndExpand()
    {
        var s = NewSession(players: 4);
        RunHours(s, 24 * 120);

        foreach (var ai in s.Players.Where(p => !p.IsHuman))
        {
            Assert.NotNull(ai.CapitalCityId);
            Assert.True(ai.Provinces.Count > 1, $"{ai.Name} owns {ai.Provinces.Count} provinces");
        }
    }
}
