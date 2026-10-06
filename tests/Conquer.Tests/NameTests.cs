using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Tests;

[Collection("World")]
public class NameTests(WorldFixture world)
{
    private readonly WorldMap _map = world.Map;

    [Fact]
    public void EveryRealNameIsUniqueShortAndDrawable()
    {
        var names = Countries.All.Select(c => c.Name).Concat(Countries.All.SelectMany(c => c.Cities.Concat(c.Regions))).ToList();
        Assert.Empty(names.GroupBy(n => n).Where(g => g.Count() > 1 && !Countries.All.Any(c => c.Name == g.Key)).Select(g => g.Key));
        Assert.All(names, n => Assert.InRange(n.Length, 2, GameRules.MaxCityNameLength));
        Assert.All(names, n => Assert.All(n, ch => Assert.InRange(ch, ' ', (char)255)));
        Assert.All(Countries.All, c => Assert.True(c.Cities.Length >= 15 && c.Regions.Length >= 10, c.Name));
    }

    [Fact]
    public void EveryNationHasItsOwnColour()
    {
        var colours = Enumerable.Range(0, Countries.MaxNations).Select(Countries.Color).ToList();
        Assert.Equal(colours.Count, colours.Distinct().Count());
        Assert.All(colours, c => Assert.Equal(0xFFu, c >> 24));
    }

    [Fact]
    public void NationsAreRealCountriesThatNameTheirCitiesAndProvincesAfterTheirOwn()
    {
        var s = GameSession.Create(_map, Countries.MaxNations, seed: 3);
        Assert.Equal(Countries.MaxNations, s.Players.Select(p => p.Name).Distinct().Count());
        var country = Countries.ByName(s.Human.Name)!;

        var settlers = s.Units.First(u => u.OwnerId == 0 && u.Type == UnitType.Settlers);
        Assert.Equal(country.Cities[0], s.SuggestCityName(0));
        Assert.Equal(country.Cities[1], s.SuggestCityName(0, country.Cities[0]));
        Assert.True(s.FoundCity(0, settlers.Id).Ok);
        var capital = s.Cities.Single(c => c.OwnerId == 0);
        Assert.Equal(country.Cities[0], capital.Name);
        Assert.Equal(country.Regions[0], _map.Provinces[capital.ProvinceId].Name);
        Assert.Equal(country.Cities[1], s.SuggestCityName(0));
    }

    [Fact]
    public void ANationThatRunsOutOfItsOwnNamesBorrowsRealOnes()
    {
        var s = GameSession.Create(_map, 2, seed: 3);
        var country = Countries.ByName(s.Human.Name)!;
        var free = _map.Provinces.Where(p => p.IsClaimable && !p.IsOwned).Take(country.Regions.Length + 5).ToList();
        foreach (var p in free) s.Claim(0, s.AddRegiment(0, p.Id, BattalionType.Scouts).Id);
        var named = free.Select(p => p.Name).ToList();
        Assert.Equal(country.Regions, named.Take(country.Regions.Length));
        var others = Countries.All.Where(c => c != country).SelectMany(c => c.Regions).ToHashSet();
        Assert.All(named.Skip(country.Regions.Length), n => Assert.Contains(n, others));
    }
}
