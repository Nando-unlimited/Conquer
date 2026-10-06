using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Tests;

[Collection("World")]
public class SeasonTests(WorldFixture world)
{
    private readonly WorldMap _map = world.Map;

    private Province Land(Func<Province, bool> where) =>
        _map.Provinces.First(p => p.IsClaimable && p.Biome != Biome.Desert && where(p));

    [Fact]
    public void WinterInTheNorthIsSummerInTheSouth()
    {
        // Games start on 1 January.
        var s = GameSession.Create(_map, 1, seed: 7, computerRivals: false);
        var north = Land(p => p.Latitude > 50);
        var south = Land(p => p.Latitude < -40);
        var tropics = Land(p => Math.Abs(p.Latitude) < 15);

        Assert.Equal(Season.Winter, s.SeasonOf(north));
        Assert.Equal(Season.Summer, s.SeasonOf(south));
        Assert.True(s.WinterSeverity(north) > 0);
        Assert.Equal(0, s.WinterSeverity(south));
        // The calendar still says winter in the northern tropics, but there is no snow.
        Assert.Equal(0, s.WinterSeverity(tropics));
        Assert.Equal(1 + GameRules.WinterSlowdown * s.WinterSeverity(north), s.SeasonSlowdown(north), 6);
        Assert.Equal(1, s.SeasonSlowdown(tropics));
    }

    [Fact]
    public void TheColdWearsDownArmiesOutOfTheirCities()
    {
        var s = GameSession.Create(_map, 1, seed: 7, computerRivals: false);
        var cold = Land(p => p.Latitude > GameRules.HarshWinterLatitude);
        var regiment = s.AddRegiment(0, cold.Id, BattalionType.LightInfantry);
        double severity = s.WinterSeverity(cold);
        Assert.Equal(1, severity, 6);
        Assert.Equal(GameRules.WinterAttrition, s.DailyAttrition(regiment), 6);
        Assert.NotNull(s.SeasonEffect(cold));

        double men = regiment.Citizens;
        for (int h = 0; h < 24; h++) s.Step();
        Assert.True(regiment.Citizens <= men * (1 - GameRules.WinterAttrition) + 1e-6);

        // Sheltered in a city of its own, the cold does not touch it.
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, cold.Id, 300).Id);
        Assert.Equal(0, s.DailyAttrition(regiment));
    }

    [Fact]
    public void TheDesertWearsDownArmiesAllYear()
    {
        var s = GameSession.Create(_map, 1, seed: 7, computerRivals: false);
        var desert = _map.Provinces.First(p => p.Biome == Biome.Desert && Math.Abs(p.Latitude) < GameRules.MildWinterLatitude);
        var regiment = s.AddRegiment(0, desert.Id, BattalionType.LightInfantry);
        Assert.Equal(GameRules.DesertAttrition, s.DailyAttrition(regiment), 6);
    }

    [Fact]
    public void ThePeaksAndTheHighMountainsWearDownArmiesAllYear()
    {
        var s = GameSession.Create(_map, 1, seed: 7, computerRivals: false);
        var peak = _map.Provinces.First(p => p.Biome is Biome.Peaks or Biome.PolarIce); // the test map may have no peaks
        var regiment = s.AddRegiment(0, peak.Id, BattalionType.LightInfantry);
        Assert.Equal(GameRules.PeakAttrition, GameSession.HeightAttrition(peak), 6);
        Assert.True(s.DailyAttrition(regiment) >= GameRules.PeakAttrition);
        Assert.Contains("aire enrarecido", s.SeasonEffect(peak));
        Assert.Equal(GameRules.HighMountainAttrition, GameSession.HeightAttrition(_map.Provinces.First(p => p.Biome == Biome.HighMountains)), 6);
    }
}
