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
    public void TheSameSettingsGenerateTheSameMap()
    {
        var again = WorldGenerator.Generate(new WorldSettings(MapKind.Random, 42));
        Assert.Equal(GameSession.Fingerprint(_map), GameSession.Fingerprint(again));
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
