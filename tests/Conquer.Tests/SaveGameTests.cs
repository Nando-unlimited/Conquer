using Conquer.Game.Buildings;
using Conquer.Game.Economy;
using Conquer.Game.Rules;
using Conquer.Game.Science;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Tests;

[Collection("World")]
public class SaveGameTests(WorldFixture world)
{
    private readonly WorldMap _map = world.Map;

    /// <summary>Four nations, the human's capital founded and researching, a war on, half a year played.</summary>
    private GameSession PlayedGame()
    {
        var s = GameSession.Create(_map, 4, seed: 7);
        s.FoundCity(s.Human.Id, s.Units.First(u => u.OwnerId == s.Human.Id).Id);
        s.Research(s.Human.Id, Tech.Agriculture);
        s.SetResearchPriority(s.Human.Id, TechBranch.Economy, 3);
        s.DeclareWar(s.Human.Id, 1);
        for (int h = 0; h < 24 * 180; h++) s.Step();
        return s;
    }

    private static byte[] Bytes(SaveGame save)
    {
        using var stream = new MemoryStream();
        (save with { SavedAtUtc = default }).Write(stream);
        return stream.ToArray();
    }

    [Fact]
    public void LoadingAndSavingAgainGivesTheSameSave()
    {
        var original = PlayedGame();
        var save = original.ToSave("test");
        Assert.NotEmpty(save.Cities);
        Assert.NotEmpty(save.Wars);
        Assert.NotEmpty(save.Ais);

        using var stream = new MemoryStream(Bytes(save));
        var loaded = GameSession.Load(_map, SaveGame.Read(stream));

        Assert.Equal(Bytes(save), Bytes(loaded.ToSave("test")));
        Assert.Equal(original.Date, loaded.Date);
        Assert.Equal(original.Human.Provinces.Order(), loaded.Human.Provinces.Order());
        Assert.Equal(original.Human.Bonuses, loaded.Human.Bonuses);
    }

    [Fact]
    public void LoadedGameKeepsPlaying()
    {
        var save = PlayedGame().ToSave("test");
        var loaded = GameSession.Load(_map, save);
        double people = loaded.Stats(loaded.Human).Total;

        for (int h = 0; h < 24 * 60; h++) loaded.Step();

        Assert.True(loaded.Stats(loaded.Human).Total > people * 0.5);
        Assert.Contains(loaded.Cities, c => c.OwnerId != loaded.Human.Id);
    }

    [Fact]
    public void OlderSavesKeepOnlyThePeopleTheLandFeeds()
    {
        var s = GameSession.Create(_map, 2, seed: 7);
        s.FoundCity(s.Human.Id, s.Units.First(u => u.OwnerId == s.Human.Id).Id);
        var capital = _map.Provinces[s.CityById(s.Human.CapitalCityId!.Value)!.ProvinceId];
        capital.Population = 50_000_000;
        var save = s.ToSave("test");

        var loaded = GameSession.Load(_map, save);
        Assert.Equal(50_000_000, capital.Population);
        loaded = GameSession.Load(_map, save with { RealisticPopulation = false });
        Assert.Equal(loaded.CapacityOf(capital), capital.Population, 6);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OlderSavesGetFullExtraDeposits(bool extraDeposits)
    {
        var s = GameSession.Create(_map, 2, seed: 7);
        var p = _map.Provinces.First(p => p.IsClaimable && p.HasDeposit(ResourceType.Iron));
        p.Reserves[(int)ResourceType.Iron] = 0;
        var save = s.ToSave("test") with { ExtraDeposits = extraDeposits };

        GameSession.Load(_map, save);

        // A current save keeps the pocket exhausted; an older one never had it, so it starts full.
        Assert.Equal(extraDeposits ? 0 : p.DepositSizes[(int)ResourceType.Iron] * GameRules.DepositSizeMultiplier,
            p.Reserves[(int)ResourceType.Iron]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OlderSavesGiveEveryCityBarracks(bool barracks)
    {
        var s = PlayedGame();
        var save = s.ToSave("test") with { Barracks = barracks };

        var loaded = GameSession.Load(_map, save);

        // Every city trained troops before barracks, so in an older save each gets one; a current save keeps what it had.
        var withBarracks = loaded.Cities.Where(c => _map.Provinces[c.ProvinceId].Buildings.Contains(BuildingType.Barracks));
        if (barracks) Assert.Equal(save.Provinces.Count(p => p.Buildings.Contains(BuildingType.Barracks)), withBarracks.Count());
        else Assert.Equal(loaded.Cities.Count, withBarracks.Count());
    }

    [Fact]
    public void OlderSavesGiveAWorkshopToBarracksThatBuiltWarMachines()
    {
        var s = PlayedGame();
        s.Human.Learn(Tech.SiegeEngines);
        _map.Provinces[s.Cities.First(c => c.OwnerId == s.Human.Id).ProvinceId].AddBuilding(BuildingType.Barracks);
        var save = s.ToSave("test") with { Workshops = false };

        var loaded = GameSession.Load(_map, save);

        // The barracks built catapults before workshops: where the owner knew how, they get a workshop to go on doing so.
        var barracks = _map.Provinces.Where(p => p.OwnerId >= 0 && p.Buildings.Contains(BuildingType.Barracks)).ToList();
        Assert.Contains(barracks, p => p.OwnerId == loaded.Human.Id);
        Assert.All(barracks, p => Assert.Equal(loaded.Players[p.OwnerId].Techs.Contains(Tech.SiegeEngines), p.Buildings.Contains(BuildingType.Workshop)));
    }

    [Fact]
    public void TheSameSettingsGenerateTheSameMap()
    {
        var again = WorldGenerator.Generate(new WorldSettings(MapKind.Random, 42));
        Assert.Equal(GameSession.Fingerprint(_map), GameSession.Fingerprint(again));
    }

    /// <summary>
    /// The generator must keep making the map older saves were played on. These are the fingerprints of the test
    /// world as 1.30.1 made it, and as 1.31.0 and 1.32.0 recorded it (they took each province's river from the rivers
    /// as drawn); a change to the generator that breaks this breaks every save.
    /// </summary>
    [Theory]
    [InlineData(-7352737610506749324)]
    [InlineData(-3434530806986038682)]
    public void SavesFromEarlierVersionsOfTheMapStillLoad(long fingerprint)
    {
        Assert.Equal(-7352737610506749324, GameSession.Fingerprint(_map));
        var save = GameSession.Create(_map, 2, seed: 7).ToSave("test") with { MapFingerprint = fingerprint };
        Assert.NotNull(GameSession.Load(_map, save));
    }

    [Fact]
    public void ASaveFromAnotherMapIsRefused()
    {
        var save = GameSession.Create(_map, 2, seed: 7).ToSave("test") with { MapFingerprint = 1 };
        Assert.Throws<InvalidDataException>(() => GameSession.Load(_map, save));
    }

    [Fact]
    public void DamagedSaveIsRefused()
    {
        using var stream = new MemoryStream([1, 2, 3, 4]);
        Assert.Throws<InvalidDataException>(() => SaveGame.Read(stream));
    }
}
