using Conquer.Game.Buildings;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Tests;

/// <summary>Faiths: every nation follows one, its people keep theirs, and those of another convert slowly.</summary>
[Collection("World")]
public class ReligionTests(WorldFixture world)
{
    private readonly WorldMap _map = world.Map;

    /// <summary>The human's capital in A, and B next to it with 1000 people of player 1's other faith, now the human's.</summary>
    private (GameSession S, Province A, Province B) ProvinceOfAnotherFaith()
    {
        var s = GameSession.Create(_map, 2, seed: 7, computerRivals: false);
        s.Players[1].ReligionId = (s.Human.ReligionId + 1) % Religions.Count;
        var (a, b) = _map.Provinces
            .Where(p => p.Biome == Biome.Grassland && p.Neighbors.Length > 3)
            .SelectMany(p => p.Neighbors.Select(n => (A: p, B: _map.Provinces[n])))
            .First(t => t.B.Biome == Biome.Grassland && t.B.Neighbors.Length > 3);
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, a.Id, 300).Id);
        var claimer = s.AddRegiment(1, b.Id, BattalionType.Scouts);
        s.Claim(1, claimer.Id);
        s.Disband(1, claimer.Id);
        b.Population = 1000;
        Assert.Equal(s.Players[1].ReligionId, b.ReligionId);
        s.DeclareWar(0, 1);
        b.ControllerId = 0;
        s.MakePeace(0, 1, PeaceTerms.TakeOccupied);
        Assert.Equal(0, b.OwnerId);
        b.Mood = 50; // conversion at its normal pace
        return (s, a, b);
    }

    [Fact]
    public void EveryNationFollowsAFaithAndItsLandTakesIt()
    {
        var s = GameSession.Create(_map, 4, seed: 7, computerRivals: false);
        Assert.All(s.Players, p => Assert.InRange(p.ReligionId, 0, Religions.Count - 1));
        var settlers = s.Units.First(u => u.OwnerId == 0);
        s.FoundCity(0, settlers.Id);
        Assert.Equal(s.Human.ReligionId, _map.Provinces[s.Cities[0].ProvinceId].ReligionId);
    }

    [Fact]
    public void PeopleOfAnotherFaithAreUnhappyAndConvertFasterWithATemple()
    {
        var (s, a, b) = ProvinceOfAnotherFaith();
        Assert.True(s.HasOtherFaith(b));
        Assert.Contains(s.MoodFactors(b), f => f.Reason.StartsWith("Fe:") && f.Points == -GameRules.OtherFaithMood);
        double pace = s.DailyConversion(b);
        Assert.Equal(1.0 / (GameRules.ConversionYears * 365), pace, 9);
        b.AddBuilding(BuildingType.Temple);
        Assert.Equal(pace * (1 + GameRules.TempleConversion), s.DailyConversion(b), 9);

        b.Conversion = 0.9999;
        for (int h = 0; h < 24; h++) s.Step();
        Assert.False(s.HasOtherFaith(b));
        Assert.Equal(s.Human.ReligionId, b.ReligionId);
        Assert.Contains(s.Notifications, n => n.Text.Contains("abraza la nuestra"));
    }

    [Fact]
    public void FaithsOutlastASaveAndOlderSavesGetOne()
    {
        var (s, _, b) = ProvinceOfAnotherFaith();
        b.Conversion = 0.4;
        var loaded = GameSession.Load(_map, s.ToSave("test"));
        Assert.Equal(s.Players[1].ReligionId, loaded.Map.Provinces[b.Id].ReligionId);
        Assert.Equal(0.4, loaded.Map.Provinces[b.Id].Conversion, 6);
        Assert.Equal(s.Human.ReligionId, loaded.Human.ReligionId);

        var old = s.ToSave("test");
        old = old with
        {
            Players = [.. old.Players.Select(p => p with { ReligionId = null })],
            Provinces = [.. old.Provinces.Select(p => p with { ReligionId = null })],
        };
        var older = GameSession.Load(_map, old);
        Assert.Equal(older.Human.ReligionId, older.Map.Provinces[b.Id].ReligionId);
    }
}
