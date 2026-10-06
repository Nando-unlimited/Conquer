using Conquer.Game.Buildings;
using Conquer.Game.Economy;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Science;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Tests;

[Collection("World")]
public class CityTests(WorldFixture world)
{
    private readonly WorldMap _map = world.Map;

    private static void RunHours(GameSession s, int hours)
    {
        for (int i = 0; i < hours; i++) s.Step();
    }

    /// <summary>A capital on grassland, and another grassland province of the player's two provinces away, with people in it.</summary>
    private (GameSession Session, Province Capital, Province Site) CapitalAndSite(int people = 600)
    {
        var s = GameSession.Create(_map, 1, seed: 7);
        var capital = _map.Provinces.First(p => p.Biome == Biome.Grassland && p.Neighbors.Length > 3);
        var near = capital.Neighbors.Append(capital.Id).ToHashSet();
        var site = _map.Provinces.First(p => p.Biome == Biome.Grassland && !near.Contains(p.Id) && p.Neighbors.All(n => !near.Contains(n)));
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, capital.Id, 300).Id, "Roma");
        s.Claim(0, s.AddRegiment(0, site.Id, BattalionType.Scouts).Id);
        site.Population = people;
        s.Human.Stockpile[ResourceType.Wood] = s.Human.Stockpile[ResourceType.Gold] = 1000;
        return (s, capital, site);
    }

    [Fact]
    public void OnlyLandWorksAndMilitaryBuildingsGoWithoutACity()
    {
        Assert.Equal([BuildingType.Farm, BuildingType.Granary, BuildingType.Sawmill, BuildingType.Mine, BuildingType.Barracks, BuildingType.Factory, BuildingType.Workshop, BuildingType.Airfield],
            Buildings.All.Where(b => !b.Info().CityOnly));

        var (s, _, site) = CapitalAndSite();
        foreach (var tech in Techs.All) s.Human.Learn(tech);
        Assert.Equal("Solo en provincias con ciudad.", s.CanBuild(0, site, BuildingType.Temple).Message);
        Assert.True(s.CanBuild(0, site, BuildingType.Granary).Ok);
    }

    [Fact]
    public void CitizensBuildACityWithTheNameTheyChoose()
    {
        var (s, _, site) = CapitalAndSite();
        string province = site.Name;

        var result = s.BuildCity(0, site.Id, "  Nueva Roma ");
        Assert.True(result.Ok, result.Message);
        Assert.Equal(1000 - GameRules.CityCost.Items.First(i => i.Type == ResourceType.Wood).Amount, s.Human.Stockpile[ResourceType.Wood]);
        Assert.Equal("Nueva Roma", site.PlannedCityName);
        Assert.False(s.Build(0, site.Id, BuildingType.Farm).Ok); // one construction at a time

        RunHours(s, 24 * (GameRules.CityBuildingDays - 1));
        Assert.Null(site.CityId);
        RunHours(s, 24);

        var city = s.CityIn(site)!;
        Assert.Equal("Nueva Roma", city.Name);
        Assert.Equal(province, site.Name); // the province keeps its own name
        Assert.NotEqual(city.Name, site.Name);
        Assert.Null(site.PlannedCityName);
        Assert.NotEqual(city.Id, s.Human.CapitalCityId);
        Assert.Contains(s.Notifications, n => n.Text == $"Fundada la ciudad de Nueva Roma en {province}.");
    }

    [Fact]
    public void ACityNeedsPeopleRoomAndAFreeName()
    {
        var (s, capital, site) = CapitalAndSite(people: GameRules.CityBuildingPopulation - 1);
        Assert.False(s.CanBuildCity(0, site).Ok); // too few people
        site.Population = GameRules.CityBuildingPopulation;
        Assert.True(s.CanBuildCity(0, site).Ok);

        Assert.False(s.BuildCity(0, site.Id, "Roma").Ok);            // taken by the capital
        Assert.False(s.BuildCity(0, site.Id, "   ").Ok);             // no name
        Assert.False(s.BuildCity(0, site.Id, new string('a', 30)).Ok); // too long
        Assert.True(s.BuildCity(0, site.Id, "Ostia").Ok);

        var next = _map.Provinces[site.Neighbors[0]];
        Assert.False(s.IsCitySite(0, next).Ok); // next to a city being built
        Assert.False(s.CheckCityName("ostia").Ok);
        Assert.Equal("Ya hay una ciudad aquí.", s.CanBuildCity(0, capital).Message);
    }

    [Fact]
    public void AGranarySparesHalfOfThoseWhoWouldStarve()
    {
        // Only the capital, so no citizens leave it for other provinces while it starves.
        double Deaths(bool granary)
        {
            var s = GameSession.Create(_map, 1, seed: 7);
            var capital = _map.Provinces.First(p => p.Biome == Biome.Grassland && p.Neighbors.Length > 3);
            s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, capital.Id, 300).Id);
            if (granary) capital.AddBuilding(BuildingType.Granary);
            capital.Population = 10 * s.CapacityOf(capital);
            s.Human.Stockpile[ResourceType.Food] = 0;
            RunHours(s, 24);
            double before = capital.Population;
            RunHours(s, 24);
            Assert.True(s.Human.IsStarving);
            return (before - capital.Population) / before;
        }

        Assert.Equal(Deaths(granary: false) / 2, Deaths(granary: true), 4);
    }

    [Fact]
    public void ACityUnderConstructionIsSavedAndFinishedAfterLoading()
    {
        var (s, _, site) = CapitalAndSite();
        Assert.True(s.BuildCity(0, site.Id, "Cartago Nova").Ok);
        RunHours(s, 24 * 10);

        var loaded = GameSession.Load(_map, s.ToSave("test"));
        Assert.Equal("Cartago Nova", site.PlannedCityName);
        RunHours(loaded, 24 * (GameRules.CityBuildingDays - 10));
        Assert.Equal("Cartago Nova", loaded.CityIn(site)?.Name);
    }

    [Fact]
    public void ProvincesAreNamedWhenFirstClaimedAndKeepTheirNames()
    {
        var s = GameSession.Create(_map, 2, seed: 7, computerRivals: false);
        Assert.All(_map.Provinces, p => Assert.Equal("", p.Name)); // nobody has claimed anything yet
        Assert.All(_map.Provinces, p => Assert.Equal(p.Info.Name, p.DisplayName));

        var sites = _map.Provinces.Where(p => p.IsClaimable && p.Neighbors.Length > 3).Take(40).ToList();
        foreach (var p in sites) s.Claim(0, s.AddRegiment(0, p.Id, BattalionType.Scouts).Id);
        var claimed = sites.Where(p => p.OwnerId == 0).ToList();
        Assert.NotEmpty(claimed);
        Assert.All(claimed, p => Assert.NotEqual("", p.Name));
        Assert.Equal(claimed.Count, claimed.Select(p => p.Name).Distinct().Count());
        Assert.Contains(s.Notifications, n => n.Text.StartsWith($"Reclamas la provincia y la llamas {claimed[^1].Name}"));

        // The name is saved with the game (loading resets the map first).
        string name = claimed[0].Name;
        GameSession.Load(_map, s.ToSave("test"));
        Assert.Equal(name, claimed[0].Name);
    }
}
