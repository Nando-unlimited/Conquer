using Conquer.Game.Economy;
using Conquer.Game.Rules;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Tests;

/// <summary>The same map as <see cref="WorldFixture"/>, on the hardest difficulty.</summary>
public sealed class VeryHardWorldFixture
{
    public WorldMap Map { get; } = WorldGenerator.Generate(new WorldSettings(MapKind.Random, 42, Difficulty: Difficulty.VeryHard));
}

[Collection("World")]
public class DifficultyTests(WorldFixture world, VeryHardWorldFixture veryHard) : IClassFixture<VeryHardWorldFixture>
{
    private readonly WorldMap _normal = world.Map, _veryHard = veryHard.Map;

    private static IEnumerable<(Province P, ResourceType R)> DepositsOf(WorldMap map) =>
        map.Provinces.SelectMany(p => Resources.Deposits.Where(r => p.Deposits[(int)r] > 0).Select(r => (p, r)));

    [Fact]
    public void HarderMapsHaveFewerAndSmallerDeposits()
    {
        var hard = DepositsOf(_veryHard).ToList();

        Assert.True(hard.Count < DepositsOf(_normal).Count() * 0.7);
        // The hardest map keeps only the first rolls, which every difficulty shares, with half-sized pockets.
        Assert.All(hard, d =>
        {
            var p = _normal.Provinces[d.P.Id];
            Assert.Equal(p.Deposits[(int)d.R], d.P.Deposits[(int)d.R]);
            Assert.InRange(d.P.DepositSizes[(int)d.R], p.DepositSizes[(int)d.R] * 0.5 - 10, p.DepositSizes[(int)d.R] * 0.5 + 10);
        });
    }

    [Fact]
    public void TheHumanStartsWithWhatTheDifficultySays()
    {
        var normal = GameSession.Create(_normal, 2, seed: 7);
        Assert.Equal(GameRules.StartingFood, normal.Human.Stockpile[ResourceType.Food]);

        var s = GameSession.Create(_veryHard, 2, seed: 7);
        Assert.Equal(Difficulty.VeryHard, s.Difficulty);
        Assert.Equal(GameRules.StartingFood * 0.5, s.Human.Stockpile[ResourceType.Food]);
        Assert.Equal(GameRules.StartingGold * 0.5, s.Human.Stockpile[ResourceType.Gold]);
        Assert.Equal(GameRules.StartingWood * 0.5, s.Human.Stockpile[ResourceType.Wood]);
        // Rivals always start with the standard stockpile.
        Assert.Equal(GameRules.StartingFood, s.Players[1].Stockpile[ResourceType.Food]);
    }

    [Fact]
    public void ComputerRivalsProduceMoreOnHarderDifficulties()
    {
        var s = GameSession.Create(_veryHard, 2, seed: 7);
        foreach (var player in s.Players) s.FoundCity(player.Id, s.Units.First(u => u.OwnerId == player.Id).Id);
        foreach (var city in s.Cities)
        {
            var p = _veryHard.Provinces[city.ProvinceId];
            p.Population = 1000;
            p.Mood = 60;
        }

        // The same city makes half as much science again for a rival.
        Assert.Equal(s.SciencePerDay(s.Human) * 1.5, s.SciencePerDay(s.Players[1]), 6);
    }

    [Fact]
    public void TheDifficultyIsSavedWithTheMap()
    {
        var save = GameSession.Create(_veryHard, 2, seed: 7).ToSave("test");
        using var stream = new MemoryStream();
        save.Write(stream);
        stream.Position = 0;

        Assert.Equal(Difficulty.VeryHard, SaveGame.Read(stream).World.Difficulty);
    }
}
