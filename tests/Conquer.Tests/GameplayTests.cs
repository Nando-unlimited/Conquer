using Conquer.Game.Buildings;
using Conquer.Game.Economy;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Science;
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
            Assert.Equal(GameRules.StartingCitizens, unit.Citizens);
            Assert.True(_map.Provinces[unit.ProvinceId].IsClaimable);
            Assert.Equal(GameRules.StartingFood, player.Stockpile[ResourceType.Food]);
            Assert.Equal(GameRules.StartingGold, player.Stockpile[ResourceType.Gold]);
            Assert.Equal(GameRules.StartingWood, player.Stockpile[ResourceType.Wood]);
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
        Assert.Equal(GameRules.StartingCitizens, province.Population);
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
        var warriors = s.AddRegiment(0, ice.Id, BattalionType.Warriors);

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
        var warriors = s.AddRegiment(0, coast.Id, BattalionType.Warriors);

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
        s.Claim(0, s.AddRegiment(0, b.Id, BattalionType.Scouts).Id);

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
    public void OnlyUnitsWithScoutsClaimFreeLand()
    {
        var s = NewSession();
        var (a, _) = GrasslandPair();
        var settlers = s.AddUnit(0, UnitType.Settlers, a.Id, 300);
        var warriors = s.AddRegiment(0, a.Id, BattalionType.Warriors, BattalionType.Warriors);
        var mixed = s.AddRegiment(0, a.Id, BattalionType.Warriors, BattalionType.Scouts);

        Assert.False(s.Claim(0, settlers.Id).Ok);
        var refused = s.Claim(0, warriors.Id);
        Assert.False(refused.Ok);
        Assert.Equal("Solo reclaman territorio las unidades con exploradores.", refused.Message);
        Assert.False(a.IsOwned);

        Assert.True(s.Claim(0, mixed.Id).Ok);
        Assert.Equal(0, a.OwnerId);
        Assert.False(s.Claim(0, mixed.Id).Ok);
    }

    [Fact]
    public void UnitsWalkAtTenKilometresAnHourOnOpenGroundSlowedBySnow()
    {
        var s = NewSession();
        var (a, b) = GrasslandPair();
        var unit = s.AddRegiment(0, a.Id, BattalionType.Warriors);
        double expected = _map.DistanceKm(a, b) / GameRules.CitizenSpeedKmh;

        Assert.Equal(expected, s.Pathfinder.StepHours(a.Id, b.Id), 6);
        expected *= s.SeasonSlowdown(b); // snow or mud, depending on where the pair lies
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
        a.Population = 20000; // enough for a few emigrants a day
        var scouts = s.AddRegiment(0, b.Id, BattalionType.Scouts);
        Assert.True(s.Claim(0, scouts.Id).Ok);

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
        s.Claim(0, s.AddRegiment(0, b.Id, BattalionType.Scouts).Id);
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
        Assert.Equal(GameRules.StartingFood * (1 - GameRules.FoodSpoilage) - GameRules.StartingCitizens * GameRules.FoodPerCitizen, s.Human.Stockpile[ResourceType.Food], 6);
    }

    [Fact]
    public void TheCapitalGrowsHappierAndMoreFertile()
    {
        var s = NewSession();
        var (a, _) = GrasslandPair();
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, a.Id, 300).Id);
        s.Human.Stockpile[ResourceType.Food] = 1e6; // the starting settlers still waiting eat too
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
        // Everyone eats: the city and the starting settlers still waiting.
        s.Human.Stockpile[ResourceType.Food] = (300 + GameRules.StartingCitizens) * GameRules.FoodPerCitizen * GameRules.FoodReserveFullDays * 2;

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
        s.Claim(0, s.AddRegiment(0, b.Id, BattalionType.Scouts).Id);
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
    public void EveryPopulatedProvinceHasBirthsByItsOwnFertility()
    {
        var s = NewSession();
        var (a, b) = GrasslandPair();
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, a.Id, 300).Id);
        b.Population = 20;

        b.Fertility = 1;
        double normal = s.DailyBirths(b, starving: false);
        b.Fertility = 1.5;
        double fertile = s.DailyBirths(b, starving: false);

        // A thinly populated countryside still grows by its land, beyond its few citizens' own share.
        Assert.True(normal > 20 * GameRules.GrowthRate, $"births {normal}");
        Assert.Equal(1.5 * normal, fertile, 6);
        Assert.Equal(0, s.DailyBirths(b, starving: true));
        Assert.Equal(s.DailyBirths(a, starving: false), s.Stats(s.Human).DailyBirths, 6);
    }

    [Fact]
    public void NationStatsAddUpPeopleWhereverTheyAre()
    {
        var s = NewSession();
        var (a, b) = GrasslandPair();
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, a.Id, 300).Id);
        s.Claim(0, s.AddRegiment(0, b.Id, BattalionType.Scouts).Id);
        a.Population = 1000;
        a.Mood = 80;
        b.Population = 500;
        b.Mood = 20;
        s.ForceMigration(0, a.Id, b.Id, 100);

        var stats = s.Stats(s.Human);
        Assert.Equal(1400, stats.Settled);
        Assert.Equal(GameRules.StartingCitizens + 50, stats.InUnits); // the starting settlers are still waiting, plus the scouts
        Assert.Equal(100, stats.Migrating);
        Assert.Equal(1400 + GameRules.StartingCitizens + 50 + 100, stats.Total);
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
    public void ManyProvincesHoldDepositsAndSomeSeveral()
    {
        var land = _map.Provinces.Where(p => p.IsClaimable).ToList();
        int DepositsIn(Province p) => Resources.Deposits.Count(r => p.Deposits[(int)r] > 0);

        // On normal difficulty about three in ten have a deposit, and about one in twenty two or more.
        Assert.InRange(land.Count(p => DepositsIn(p) > 0), land.Count * 25 / 100, land.Count * 40 / 100);
        Assert.InRange(land.Count(p => DepositsIn(p) > 1), land.Count * 3 / 100, land.Count * 8 / 100);
    }

    [Fact]
    public void DepositsRunDryAndStopProducing()
    {
        var s = NewSession();
        var p = _map.Provinces.First(p => p.IsClaimable && p.Neighbors.Length > 3 && p.HasDeposit(ResourceType.Iron));
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, p.Id, 300).Id);
        s.Human.Learn(Tech.IronWorking);
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
    public void CitiesProduceScienceThatDiscoversAdvances()
    {
        var s = NewSession();
        var (a, _) = GrasslandPair();
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, a.Id, 300).Id);
        Assert.True(s.SciencePerDay(s.Human) > GameRules.ScienceBasePerCity);

        Assert.True(s.Research(0, Tech.Agriculture).Ok);
        for (int day = 0; day < 365 * 3 && !s.Human.Techs.Contains(Tech.Agriculture); day++) RunHours(s, 24);

        Assert.Contains(Tech.Agriculture, s.Human.Techs);
        Assert.Null(s.Human.Researching[(int)TechBranch.Economy]);
        Assert.Equal(0.2, s.Human.Bonuses.Food);
        Assert.Contains(s.Notifications, n => n.Text.StartsWith("Descubrimiento: Agricultura"));
        Assert.False(s.Research(0, Tech.Agriculture).Ok); // already known
    }

    [Fact]
    public void LevelsOpenWithOneAdvanceOfTheLevelBelow()
    {
        var s = NewSession();
        Assert.False(s.Research(0, Tech.Mining).Ok); // level 2 is closed
        s.Human.Learn(Tech.Carpentry);
        Assert.True(s.Research(0, Tech.Mining).Ok);
        Assert.False(s.Research(0, Tech.Irrigation).Ok); // its level is open, but it needs agriculture
        Assert.False(s.Research(0, Tech.Currency).Ok);   // level 3 needs mining or irrigation

        // Choosing another advance of the branch replaces the one it was researching.
        Assert.True(s.Research(0, Tech.Agriculture).Ok);
        Assert.Equal(Tech.Agriculture, s.Human.Researching[(int)TechBranch.Economy]);
    }

    [Fact]
    public void ScienceIsSharedByPriorityAmongTheBranchesResearching()
    {
        var s = NewSession();
        var (a, _) = GrasslandPair();
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, a.Id, 300).Id);
        s.Research(0, Tech.Agriculture);
        s.Research(0, Tech.Writing);
        s.SetResearchPriority(0, TechBranch.Economy, 3);
        Assert.False(s.SetResearchPriority(0, TechBranch.Military, GameRules.MaxResearchPriority + 1).Ok);

        RunHours(s, 24 * 3);

        // Nothing chosen in the military: its share goes to the other two, three to one.
        Assert.Equal(s.Human.ResearchProgress[(int)Tech.Writing] * 3, s.Human.ResearchProgress[(int)Tech.Agriculture], 6);
        Assert.Equal(0, s.Human.SpareScience);
    }

    [Fact]
    public void ScienceIsSavedWhileNothingIsResearched()
    {
        var s = NewSession();
        var (a, _) = GrasslandPair();
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, a.Id, 300).Id);
        RunHours(s, 24 * 5);
        double saved = s.Human.SpareScience;

        Assert.True(saved > 0);
        s.Research(0, Tech.Carpentry);
        Assert.Equal(saved, s.Human.ResearchProgress[(int)Tech.Carpentry]);
        Assert.Equal(0, s.Human.SpareScience);
    }

    [Fact]
    public void NeighboursWhoKnowAnAdvanceMakeItCheaper()
    {
        var s = GameSession.Create(_map, 2, seed: 7, computerRivals: false);
        var (a, b) = GrasslandPair();
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, a.Id, 300).Id);
        double cost = Tech.Agriculture.Info().Cost * GameRules.ResearchCostMultiplier;
        Assert.Equal(cost, s.ResearchCost(s.Human, Tech.Agriculture));

        s.Claim(1, s.AddRegiment(1, b.Id, BattalionType.Scouts).Id);
        s.Players[1].Learn(Tech.Agriculture);

        Assert.Contains(1, s.NeighbourNations(s.Human));
        Assert.Equal(cost * (1 - GameRules.NeighbourResearchDiscount), s.ResearchCost(s.Human, Tech.Agriculture), 6);
        Assert.Equal(Tech.Carpentry.Info().Cost * GameRules.ResearchCostMultiplier, s.ResearchCost(s.Human, Tech.Carpentry)); // the neighbour does not know it
    }

    [Fact]
    public void TheLandFeedsMorePeopleInEachAge()
    {
        var s = NewSession();
        var (a, _) = GrasslandPair();
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, a.Id, 300).Id);
        double ancient = s.CapacityOf(a);
        Assert.Equal(a.Capacity * GameRules.CityCapacityMultiplier, ancient, 6);

        var medieval = Techs.All.First(t => t.Info().Era == Era.Medieval && t.Info().Effects.Capacity == 0);
        s.Human.Learn(medieval);
        Assert.Equal(Era.Medieval, s.Human.Era);
        Assert.Equal(ancient * GameRules.EraCapacity(Era.Medieval), s.CapacityOf(a), 6);
    }

    [Fact]
    public void EmptyLandDoesNotFillByItself()
    {
        var s = NewSession();
        var (a, b) = GrasslandPair();
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, a.Id, 300).Id);
        s.Claim(0, s.AddRegiment(0, b.Id, BattalionType.Scouts).Id);
        b.Population = 10;
        // Births in a village do not depend on how much land it has: a handful of people grow slowly.
        Assert.InRange(s.DailyBirths(b, starving: false), 0, (10 * GameRules.GrowthRate + GameRules.BaseBirthsPerProvince) * b.Fertility);
    }

    [Fact]
    public void AdvancesImproveTheEconomy()
    {
        (double Food, double Capacity) OneDay(params Tech[] techs)
        {
            var s = NewSession();
            var (a, _) = GrasslandPair();
            s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, a.Id, 300).Id);
            foreach (var t in techs) s.Human.Learn(t);
            RunHours(s, 24);
            // Harvest = balance + what was eaten (300 in the city and the starting settlers still waiting) + what spoiled.
            return (s.Human.LastDayNet[(int)ResourceType.Food] + GameRules.FoodPerCitizen * (300 + GameRules.StartingCitizens)
                    + GameRules.StartingFood * GameRules.FoodSpoilage, s.CapacityOf(a));
        }

        var plain = OneDay();
        var advanced = OneDay(Tech.Agriculture, Tech.Irrigation);
        Assert.Equal(plain.Food * 1.2, advanced.Food, 3);
        Assert.Equal(plain.Capacity * 1.25, advanced.Capacity, 3);
    }

    [Fact]
    public void BuildingsCostResourcesAndTakeDaysToBuild()
    {
        var s = NewSession();
        var (a, _) = GrasslandPair();
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, a.Id, 300).Id);
        var farm = BuildingType.Farm.Info();

        Assert.True(s.Build(0, a.Id, BuildingType.Farm).Ok);
        Assert.Equal(GameRules.StartingWood - 40, s.Human.Stockpile[ResourceType.Wood]);
        Assert.Equal(BuildingType.Farm, a.Constructing);
        Assert.False(s.Build(0, a.Id, BuildingType.Sawmill).Ok); // one construction at a time

        RunHours(s, 24 * (farm.Days - 1));
        Assert.DoesNotContain(BuildingType.Farm, a.Buildings);
        RunHours(s, 24);
        Assert.Contains(BuildingType.Farm, a.Buildings);
        Assert.Null(a.Constructing);
        Assert.Contains(s.Notifications, n => n.Text.StartsWith("Terminada la obra: Granja"));
        Assert.False(s.Build(0, a.Id, BuildingType.Farm).Ok); // one of each
    }

    [Fact]
    public void BuildingsHaveTheirRequirements()
    {
        var s = NewSession();
        var (a, b) = GrasslandPair();
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, a.Id, 300).Id);
        s.Claim(0, s.AddRegiment(0, b.Id, BattalionType.Scouts).Id);
        s.Human.Stockpile[ResourceType.Wood] = s.Human.Stockpile[ResourceType.Gold] = 1000;

        Assert.False(s.Build(0, a.Id, BuildingType.Library).Ok); // needs writing
        s.Human.Learn(Tech.Writing);
        Assert.False(s.Build(0, b.Id, BuildingType.Library).Ok); // needs a city
        Assert.False(s.Build(0, b.Id, BuildingType.Farm).Ok);    // nobody lives there to build it
        Assert.True(s.Build(0, a.Id, BuildingType.Library).Ok);

        s.Human.Learn(Tech.Mining);
        var bare = _map.Provinces.First(p => p.IsClaimable && !p.IsOwned && !Resources.Deposits.Any(p.HasDeposit));
        s.Claim(0, s.AddRegiment(0, bare.Id, BattalionType.Scouts).Id);
        Assert.False(s.IsBuildingAvailable(bare, BuildingType.Mine).Ok); // no deposit to mine
    }

    [Fact]
    public void BuildingsImproveTheirOwnProvince()
    {
        var s = NewSession();
        var (a, _) = GrasslandPair();
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, a.Id, 300).Id);
        double target = s.TargetMood(a), capacity = s.CapacityOf(a), science = s.SciencePerDay(s.Human);

        a.AddBuilding(BuildingType.Temple);
        a.AddBuilding(BuildingType.Aqueduct);
        a.AddBuilding(BuildingType.Library);

        Assert.Contains(s.MoodFactors(a), f => f.Reason == "Templo" && f.Points == 10);
        Assert.Equal(Math.Min(100, target + 10), s.TargetMood(a));
        Assert.Equal(capacity * 1.25, s.CapacityOf(a), 6);
        Assert.Equal(science * 1.5, s.SciencePerDay(s.Human), 6);
    }

    [Fact]
    public void OnlyAncientResourcesAreKnownAtTheStart()
    {
        var s = NewSession();
        Assert.True(s.Human.Knows(ResourceType.Copper));
        Assert.True(s.Human.Knows(ResourceType.Gold));
        Assert.True(s.Human.Knows(ResourceType.Silver));
        foreach (var hidden in new[] { ResourceType.Coal, ResourceType.Iron, ResourceType.Oil, ResourceType.Rubber, ResourceType.Aluminium, ResourceType.Silicon })
            Assert.False(s.Human.Knows(hidden), $"{hidden} should start hidden");

        s.Human.Learn(Tech.Mining);
        Assert.True(s.Human.Knows(ResourceType.Coal));
        s.Human.Learn(Tech.IronWorking);
        Assert.True(s.Human.Knows(ResourceType.Iron));
    }

    [Fact]
    public void UnknownResourcesAreNotMined()
    {
        var s = NewSession();
        var p = _map.Provinces.First(p => p.IsClaimable && p.Neighbors.Length > 3 && p.HasDeposit(ResourceType.Iron));
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, p.Id, 300).Id);
        p.Population = GameRules.DepositFullWorkers;
        double full = p.Reserves[(int)ResourceType.Iron];

        RunHours(s, 24);
        Assert.Equal(0, s.Human.LastDayNet[(int)ResourceType.Iron]);
        Assert.Equal(full, p.Reserves[(int)ResourceType.Iron]);

        s.Human.Learn(Tech.IronWorking);
        RunHours(s, 24);
        Assert.True(s.Human.LastDayNet[(int)ResourceType.Iron] > 0);
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
            Assert.True(ai.Techs.Count > 0 || ai.ResearchProgress.Any(p => p > 0), $"{ai.Name} is not researching");
        }
    }

    [Fact]
    public void ComputerRivalsBuildBarracksToTrainTheirArmy()
    {
        var s = NewSession(players: 3);
        RunHours(s, 24 * 365);

        foreach (var ai in s.Players.Where(p => !p.IsHuman))
        {
            var capital = _map.Provinces[s.CityById(ai.CapitalCityId!.Value)!.ProvinceId];
            Assert.Contains(BuildingType.Barracks, capital.Buildings);
            var combat = s.Units.Where(u => u.OwnerId == ai.Id && u.IsMilitary).SelectMany(u => u.Battalions).Count(b => b.Type.TrainingBuilding() != null)
                         + ai.Provinces.Sum(id => _map.Provinces[id].Training.Count(o => o.TemplateBattalions.Count > 0 || o.Battalion is { } t && t.TrainingBuilding() != null));
            Assert.True(combat > 0, $"{ai.Name} has no combat troops");
        }
    }
}
