using Conquer.Game.Buildings;
using Conquer.Game.Economy;
using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Science;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Tests;

/// <summary>Stone, for the stone buildings, and sulfur, for gunpowder.</summary>
[Collection("World")]
public class StoneAndSulfurTests(WorldFixture world)
{
    private readonly WorldMap _map = world.Map;

    private int Count(ResourceType r, Func<Province, bool> where) => _map.Provinces.Count(p => p.Deposits[(int)r] > 0 && where(p));

    [Fact]
    public void QuarriesAreCommonInTheHillsAndSulfurIsScarcer()
    {
        bool Rugged(Province p) => p.Biome is Biome.Hills or Biome.Mountains or Biome.HighMountains;
        int rugged = _map.Provinces.Count(p => p.IsClaimable && Rugged(p));
        Assert.True(Count(ResourceType.Stone, Rugged) > rugged / 5);
        Assert.True(Count(ResourceType.Sulfur, _ => true) > 0);
        Assert.True(Count(ResourceType.Sulfur, _ => true) < Count(ResourceType.Stone, _ => true) / 2);
    }

    [Fact]
    public void StoneIsKnownFromTheStartAndGunpowderRevealsSulfur()
    {
        var s = GameSession.Create(_map, 1, seed: 7, computerRivals: false);
        Assert.True(s.Human.Knows(ResourceType.Stone));
        Assert.Equal(GameRules.StartingStone * s.Difficulty.Info().StartingResources, s.Human.Stockpile[ResourceType.Stone], 6);
        Assert.False(s.Human.Knows(ResourceType.Sulfur));
        s.Human.Learn(Tech.Gunpowder);
        Assert.True(s.Human.Knows(ResourceType.Sulfur));
    }

    [Fact]
    public void WallsAreOfStoneAndGunpowderWeaponsTakeSulfur()
    {
        Assert.Contains(BuildingType.Walls.Info().Cost.Items, i => i.Type == ResourceType.Stone);
        Assert.Contains(BuildingType.Castle.Info().Cost.Items, i => i.Type == ResourceType.Stone);
        Assert.Contains(Supplies.Firearms.PieceCost.Items, i => i.Type == ResourceType.Sulfur);
        Assert.Contains(BattalionType.Artillery.Models()[2].EquipmentCost.Items, i => i.Type == ResourceType.Sulfur); // cannons
    }

    [Fact]
    public void OlderSavesGetTheirStartingStoneAndFullNewDeposits()
    {
        var s = GameSession.Create(_map, 1, seed: 7, computerRivals: false);
        var save = s.ToSave("test");
        int old = (int)ResourceType.Stone;
        save = save with
        {
            Players = [.. save.Players.Select(p => p with { Stockpile = p.Stockpile[..old], LastDayNet = p.LastDayNet[..old] })],
            Provinces = [.. save.Provinces.Select(p => p with { Reserves = p.Reserves[..old] })],
        };
        var loaded = GameSession.Load(_map, save);
        Assert.Equal(GameRules.StartingStone, loaded.Human.Stockpile[ResourceType.Stone]);
        Assert.All(_map.Provinces.Where(p => p.Deposits[(int)ResourceType.Stone] > 0),
            p => Assert.Equal(p.DepositSizes[(int)ResourceType.Stone] * GameRules.DepositSizeMultiplier, p.Reserves[(int)ResourceType.Stone], 3));
    }
}

/// <summary>Saltpeter, for gunpowder with the sulfur, and horses, for the cavalry.</summary>
[Collection("World")]
public class SaltpeterAndHorsesTests(WorldFixture world)
{
    private readonly WorldMap _map = world.Map;

    [Fact]
    public void HerdsGrazeTheGrasslandsAndNeverRunOut()
    {
        Assert.Contains(_map.Provinces, p => p.Deposits[(int)ResourceType.Horses] > 0 && p.Biome is Biome.Grassland or Biome.Steppe);
        Assert.Contains(_map.Provinces, p => p.Deposits[(int)ResourceType.Saltpeter] > 0);

        var s = GameSession.Create(_map, 1, seed: 7, computerRivals: false);
        Assert.True(s.Human.Knows(ResourceType.Horses));
        s.Human.Learn(Tech.Gunpowder);
        Assert.True(s.Human.Knows(ResourceType.Saltpeter));
        var p = _map.Provinces.First(p => p.IsClaimable && p.Neighbors.Length > 3 && p.Deposits[(int)ResourceType.Horses] > 0);
        double before = p.Reserves[(int)ResourceType.Horses];
        Assert.True(s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, p.Id, 300).Id).Ok);
        p.Population = 2000;
        for (int h = 0; h < 48; h++) s.Step();
        Assert.Equal(before, p.Reserves[(int)ResourceType.Horses]);
        Assert.True(s.Human.Stockpile[ResourceType.Horses] > 0);
    }

    [Fact]
    public void GunpowderTakesSaltpeterAndCavalryTakesHorses()
    {
        Assert.Contains(Supplies.Firearms.PieceCost.Items, i => i.Type == ResourceType.Saltpeter);
        Assert.Equal(100, BattalionType.Cavalry.First().EquipmentCost.Items.Single(i => i.Type == ResourceType.Horses).Amount);
        Assert.Contains(BattalionType.Armour.First().EquipmentCost.Items, i => i.Type == ResourceType.Horses);
    }
}
