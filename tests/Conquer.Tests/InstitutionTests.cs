using Conquer.Game.Economy;
using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Science;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Tests;

[Collection("World")]
public class InstitutionTests(WorldFixture world)
{
    private readonly WorldMap _map = world.Map;

    private (Province A, Province B) GrasslandPair() =>
        _map.Provinces
            .Where(p => p.Biome == Biome.Grassland)
            .SelectMany(p => p.Neighbors.Select(n => (A: p, B: _map.Provinces[n])))
            .First(t => t.B.Biome == Biome.Grassland && t.A.Neighbors.Length > 3);

    private static void RunDays(GameSession s, int days)
    {
        for (int i = 0; i < days * 24; i++) s.Step();
    }

    /// <summary>A human capital on <paramref name="a"/> big enough for urbanism to be born in it.</summary>
    private static GameSession WithBigCapital(WorldMap map, Province a, int players = 1)
    {
        var s = GameSession.Create(map, players, seed: 7, computerRivals: false);
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, a.Id, 300).Id);
        a.Population = GameRules.UrbanismBirthPopulation;
        return s;
    }

    [Fact]
    public void UrbanismIsBornInTheFirstBigCity()
    {
        var (a, _) = GrasslandPair();
        var s = GameSession.Create(_map, 1, seed: 7, computerRivals: false);
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, a.Id, 300).Id);
        RunDays(s, 1);
        Assert.False(s.IsBorn(Institution.Urbanism));

        a.Population = GameRules.UrbanismBirthPopulation;
        RunDays(s, 1);

        Assert.True(s.IsBorn(Institution.Urbanism));
        Assert.Equal(a.Id, s.BirthplaceOf(Institution.Urbanism));
        Assert.Contains(Institution.Urbanism, a.Institutions);
        Assert.Contains(s.Notifications, n => n.Text.StartsWith("Nace el Urbanismo"));
    }

    [Fact]
    public void InstitutionsSpreadToSettledNeighboursOnly()
    {
        var (a, b) = GrasslandPair();
        var s = WithBigCapital(_map, a);
        s.Claim(0, s.AddRegiment(0, b.Id, BattalionType.Warriors).Id);
        b.Population = 100;
        var empty = a.Neighbors.Select(n => _map.Provinces[n]).First(p => p.IsClaimable && p.Id != b.Id && p.Population == 0);

        for (int day = 0; day < 1000 && !b.Institutions.Contains(Institution.Urbanism); day++)
        {
            RunDays(s, 1);
            empty.Population = 0; // keep the migrants out of it
        }

        Assert.Contains(Institution.Urbanism, b.Institutions);
        Assert.DoesNotContain(Institution.Urbanism, empty.Institutions);
    }

    [Fact]
    public void NationsAdoptAnInstitutionOnceHalfTheirPeopleHaveIt()
    {
        var (a, _) = GrasslandPair();
        var s = WithBigCapital(_map, a);
        double science = s.Human.Bonuses.Science;

        RunDays(s, 1); // born in the capital, where nearly everyone lives

        Assert.Contains(Institution.Urbanism, s.Human.Institutions);
        Assert.Equal(science + 0.1, s.Human.Bonuses.Science, 6);
        Assert.Contains(s.Notifications, n => n.Text.StartsWith("Tu nación adopta el Urbanismo"));
    }

    [Fact]
    public void AnInstitutionCanBeAdoptedEarlierForGold()
    {
        var (a, b) = GrasslandPair();
        var s = GameSession.Create(_map, 2, seed: 7, computerRivals: false);
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, a.Id, 300).Id);
        a.Population = 1000;
        // Born in the rival's city, far away.
        var far = _map.Provinces.First(p => p.IsClaimable && p.Biome == Biome.Grassland && Math.Abs(p.Latitude - a.Latitude) > 30);
        s.FoundCity(1, s.AddUnit(1, UnitType.Settlers, far.Id, 300).Id);
        far.Population = GameRules.UrbanismBirthPopulation;
        RunDays(s, 1);
        Assert.StartsWith("Aún no ha llegado", s.CanAdopt(s.Human, Institution.Urbanism).Message);

        s.Claim(0, s.AddRegiment(0, b.Id, BattalionType.Warriors).Id);
        b.Population = 100;
        b.Institutions.Add(Institution.Urbanism);
        double cost = s.AdoptionCost(s.Human, Institution.Urbanism);
        Assert.Equal(Math.Round(a.Population * GameRules.InstitutionGoldPerCitizen), cost);
        s.Human.Stockpile[ResourceType.Gold] = cost - 1;
        Assert.False(s.Adopt(0, Institution.Urbanism).Ok);

        s.Human.Stockpile[ResourceType.Gold] = cost + 10;
        Assert.True(s.Adopt(0, Institution.Urbanism).Ok);
        Assert.Contains(Institution.Urbanism, s.Human.Institutions);
        Assert.Equal(10, s.Human.Stockpile[ResourceType.Gold], 6);
    }

    [Fact]
    public void AdvancesOfAnAgeCostMoreUntilItsInstitutionIsAdopted()
    {
        var player = new Player(0, "Prueba", 0, isHuman: true);
        Assert.Equal(1, GameSession.EraCostMultiplier(player, Era.Ancient));
        Assert.Equal(1 + GameRules.InstitutionPenalty, GameSession.EraCostMultiplier(player, Era.Classical));

        player.Adopt(Institution.Urbanism);
        Assert.Equal(1, GameSession.EraCostMultiplier(player, Era.Classical));
    }

    [Fact]
    public void ClassicalAdvancesCostMoreUntilUrbanismIsAdopted()
    {
        var s = GameSession.Create(_map, 1, seed: 7, computerRivals: false);
        foreach (var tech in Techs.All.Where(t => t.Info().Era == Era.Ancient)) s.Human.Learn(tech);
        double cost = Tech.Trade.Info().Cost;

        Assert.Equal(Era.Classical, Tech.Trade.Info().Era);
        Assert.Equal(cost * (1 + GameRules.InstitutionPenalty), s.ResearchCost(s.Human, Tech.Trade), 6);
        s.Human.Adopt(Institution.Urbanism);
        Assert.Equal(cost, s.ResearchCost(s.Human, Tech.Trade), 6);
    }

    [Fact]
    public void ClassicalLevelsFollowTheAncientOnes()
    {
        var s = GameSession.Create(_map, 1, seed: 7, computerRivals: false);
        Assert.False(s.Research(0, Tech.MilitaryTactics).Ok); // level 4 needs level 3
        foreach (var tech in Techs.All.Where(t => t.Info().Era == Era.Ancient)) s.Human.Learn(tech);

        Assert.True(s.Research(0, Tech.MilitaryTactics).Ok);
        Assert.False(s.Research(0, Tech.SiegeEngines).Ok);  // needs mathematics
        Assert.False(s.Research(0, Tech.HeavyCavalry).Ok);  // level 5 needs one of level 4
        s.Human.Learn(Tech.MilitaryTactics);
        Assert.True(s.Research(0, Tech.HeavyCavalry).Ok);
    }

    [Fact]
    public void ConstructionBringsAmphitheatres()
    {
        var (a, _) = GrasslandPair();
        var s = GameSession.Create(_map, 1, seed: 7, computerRivals: false);
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, a.Id, 300).Id);

        Assert.False(s.IsBuildingAvailable(a, Conquer.Game.Buildings.BuildingType.Amphitheatre).Ok);
        s.Human.Learn(Tech.Construction);
        Assert.True(s.IsBuildingAvailable(a, Conquer.Game.Buildings.BuildingType.Amphitheatre).Ok);
    }

    [Fact]
    public void InstitutionsAreSaved()
    {
        var (a, _) = GrasslandPair();
        var s = WithBigCapital(_map, a);
        RunDays(s, 1);

        var loaded = GameSession.Load(_map, s.ToSave("test"));

        Assert.Equal(a.Id, loaded.BirthplaceOf(Institution.Urbanism));
        Assert.Contains(Institution.Urbanism, a.Institutions);
        Assert.Contains(Institution.Urbanism, loaded.Human.Institutions);
    }
}
