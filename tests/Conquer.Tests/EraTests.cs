using Conquer.Game.Buildings;
using Conquer.Game.Economy;
using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Science;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Tests;

/// <summary>The ages after the ancient one: their advances, institutions, buildings and battalions.</summary>
[Collection("World")]
public class EraTests(WorldFixture world)
{
    private readonly WorldMap _map = world.Map;

    private (Province A, Province B) GrasslandPair() =>
        _map.Provinces
            .Where(p => p.Biome == Biome.Grassland)
            .SelectMany(p => p.Neighbors.Select(n => (A: p, B: _map.Provinces[n])))
            .First(t => t.B.Biome == Biome.Grassland && t.A.Neighbors.Length > 3);

    private (GameSession Session, Province Capital) WithCapital()
    {
        var (a, _) = GrasslandPair();
        var s = GameSession.Create(_map, 1, seed: 7, computerRivals: false);
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, a.Id, 300).Id);
        a.Population = 3000;
        foreach (var r in Resources.All) s.Human.Stockpile[r] = 5000;
        return (s, a);
    }

    private static void LearnAgesBefore(Player player, Era era)
    {
        foreach (var tech in Techs.All.Where(t => t.Info().Era < era)) player.Learn(tech);
    }

    [Theory]
    [InlineData(Era.Classical)]
    [InlineData(Era.Medieval)]
    [InlineData(Era.Renaissance)]
    public void EachAgeCostsMoreUntilItsInstitutionIsAdopted(Era era)
    {
        var (s, _) = WithCapital();
        var tech = Techs.All.First(t => t.Info().Era == era);
        var institution = Institutions.All.Single(i => i.Info().Opens == era);

        Assert.Equal(tech.Info().Cost * (1 + GameRules.InstitutionPenalty), s.ResearchCost(s.Human, tech), 6);
        s.Human.Adopt(institution);
        Assert.Equal(tech.Info().Cost, s.ResearchCost(s.Human, tech), 6);
    }

    [Theory]
    [InlineData(Era.Classical)]
    [InlineData(Era.Medieval)]
    [InlineData(Era.Renaissance)]
    public void AnAgeOpensOnceTheOneBeforeIsKnown(Era era)
    {
        var (s, _) = WithCapital();
        // An advance of its first level whose requirements all come from earlier ages.
        var first = Techs.All.Where(t => t.Info().Era == era && t.Info().Requires.All(r => r.Info().Era < era)).MinBy(t => t.Info().Level);
        Assert.False(s.Research(0, first).Ok);

        LearnAgesBefore(s.Human, era);

        Assert.True(s.Research(0, first).Ok);
    }

    [Fact]
    public void FeudalismIsBornInTheCapitalOfTheFirstNationWithEightCities()
    {
        var (s, capital) = WithCapital();
        foreach (var site in _map.Provinces.Where(p => p.Biome == Biome.Grassland && p.Id != capital.Id))
        {
            if (s.Cities.Count >= GameRules.FeudalismBirthCities) break;
            // Sites next to another city are refused; the settlers left there do not matter.
            s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, site.Id, 300).Id);
        }
        Assert.Equal(GameRules.FeudalismBirthCities, s.Cities.Count);

        for (int h = 0; h < 24; h++) s.Step();

        Assert.Equal(capital.Id, s.BirthplaceOf(Institution.Feudalism));
    }

    [Theory]
    [InlineData(BuildingType.University, Tech.Education)]
    [InlineData(BuildingType.Bank, Tech.Banking)]
    [InlineData(BuildingType.Castle, Tech.Castles)]
    public void BuildingsNeedTheirAdvance(BuildingType type, Tech tech)
    {
        var (s, a) = WithCapital();
        Assert.False(s.IsBuildingAvailable(a, type).Ok);
        s.Human.Learn(tech);
        Assert.True(s.IsBuildingAvailable(a, type).Ok);
    }

    [Fact]
    public void CastlesDoubleTheDefendersDamage()
    {
        var (_, a) = WithCapital();
        double open = GameSession.DefenseMultiplier(a);
        a.AddBuilding(BuildingType.Castle);
        Assert.Equal(open * 2, GameSession.DefenseMultiplier(a), 6);
    }

    [Theory]
    [InlineData(BattalionType.Knights, Tech.Stirrup)]
    [InlineData(BattalionType.Crossbowmen, Tech.Machinery)]
    [InlineData(BattalionType.Arquebusiers, Tech.Gunpowder)]
    [InlineData(BattalionType.Cannons, Tech.Metallurgy)]
    [InlineData(BattalionType.Musketeers, Tech.MilitaryScience)]
    public void BattalionsNeedTheirAdvance(BattalionType type, Tech tech)
    {
        var (s, a) = WithCapital();
        var city = s.CityIn(a)!;
        Assert.False(s.Train(0, city.Id, type).Ok);
        s.Human.Learn(tech);
        Assert.True(s.Train(0, city.Id, type).Ok);
    }

    [Fact]
    public void HumanismIsBornInTheBiggestCityWithAUniversity()
    {
        var (s, a) = WithCapital();
        for (int h = 0; h < 24; h++) s.Step();
        Assert.False(s.IsBorn(Institution.Humanism));

        a.AddBuilding(BuildingType.University);
        for (int h = 0; h < 24; h++) s.Step();

        Assert.Equal(a.Id, s.BirthplaceOf(Institution.Humanism));
    }

    [Fact]
    public void MilitaryScienceBringsModernNames()
    {
        var (s, _) = WithCapital();
        Assert.Equal(ArmyEra.Classical, s.Human.ArmyEra);
        s.Human.Learn(Tech.MilitaryScience);
        Assert.Equal(ArmyEra.Modern, s.Human.ArmyEra);
    }
}
