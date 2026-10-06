using Conquer.Game.Buildings;
using Conquer.Game.Economy;
using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Science;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Tests;

/// <summary>Equipment: made in workshops into the nation's stockpile, taken to train, modernise and reinforce battalions.</summary>
[Collection("World")]
public class EquipmentTests(WorldFixture world)
{
    private readonly WorldMap _map = world.Map;

    private static readonly BattalionInfo Warriors = BattalionType.LightInfantry.First();
    private static readonly BattalionInfo Velites = BattalionType.LightInfantry.Models()[1];

    /// <summary>The human's capital with barracks and a workshop, plenty of resources and people, and no equipment at all.</summary>
    private (GameSession S, Province Capital) Game()
    {
        var s = GameSession.Create(_map, 1, seed: 7, computerRivals: false);
        var settlers = s.Units.First();
        var capital = _map.Provinces[settlers.ProvinceId];
        s.FoundCity(0, settlers.Id);
        capital.Population = 5000;
        capital.AddBuilding(BuildingType.Barracks);
        capital.AddBuilding(BuildingType.Workshop);
        foreach (var r in Resources.All) s.Human.Stockpile[r] = 10_000;
        s.Human.Equipment.Clear();
        return (s, capital);
    }

    private static void RunDays(GameSession s, int days)
    {
        for (int h = 0; h < 24 * days; h++) s.Step();
    }

    [Fact]
    public void EachNationStartsWithEquipmentForItsFirstWarriorsAndScouts()
    {
        var s = GameSession.Create(_map, 2, seed: 7, computerRivals: false);
        Assert.All(s.Players, p =>
        {
            Assert.Equal(MilitaryRules.StartingWarriorKits * Warriors.Pieces, p.EquipmentOf(Warriors));
            Assert.Equal(MilitaryRules.StartingScoutKits * BattalionType.Scouts.First().Pieces, p.EquipmentOf(BattalionType.Scouts.First()));
        });
        Assert.Null(BuildingType.Workshop.Info().RequiresTech);
    }

    [Fact]
    public void AWorkshopMakesTheEquipmentItIsSetToForItsResources()
    {
        var (s, capital) = Game();
        Assert.True(s.SetProduction(0, capital.Id, Warriors.Key).Ok);
        double rate = GameSession.ProductionRate(capital, Warriors);
        Assert.Equal(Warriors.Pieces / (double)Warriors.TrainingDays, rate, 6);
        RunDays(s, 1);
        Assert.Equal(rate, s.Human.EquipmentOf(Warriors), 6);

        // Without the iron it needs (and the nation mines none yet), it makes nothing.
        var legionaries = BattalionType.HeavyInfantry.Models()[2];
        s.Human.Learn(Tech.Drill);
        Assert.True(s.SetProduction(0, capital.Id, legionaries.Key).Ok);
        s.Human.Stockpile[ResourceType.Iron] = 0;
        RunDays(s, 1);
        Assert.Equal(0, s.Human.EquipmentOf(legionaries));

        // A factory makes twice as much; nothing is made without a workshop, nor equipment for what is not known.
        capital.AddBuilding(BuildingType.Factory);
        Assert.Equal(rate * MilitaryRules.FactoryOutput, GameSession.ProductionRate(capital, Warriors), 6);
        Assert.False(s.SetProduction(0, capital.Id, Velites.Key).Ok);
        Assert.False(s.CanProduce(_map.Provinces.First(p => p.IsClaimable && !p.IsOwned), Warriors).Ok);
        Assert.True(s.SetProduction(0, capital.Id, null).Ok);
        Assert.Null(capital.Production);
    }

    [Fact]
    public void TrainingTakesTheNewestModelThereIsEquipmentFor()
    {
        var (s, capital) = Game();
        var refused = s.CanTrain(capital, BattalionType.LightInfantry);
        Assert.False(refused.Ok);
        Assert.Contains("Falta equipo", refused.Message);

        s.Human.AddEquipment(Warriors, 100);
        s.Human.Learn(Tech.MilitaryTactics);
        Assert.Equal(Warriors, GameSession.TrainedModel(s.Human, BattalionType.LightInfantry)); // no velites' weapons yet
        Assert.True(s.Train(0, capital.Id, BattalionType.LightInfantry).Ok);
        Assert.Equal(0, s.Human.EquipmentOf(Warriors));
        RunDays(s, Warriors.TrainingDays);
        var regiment = s.Units.Single(u => u.IsMilitary);
        Assert.Equal("Guerreros", regiment.Battalions[0].Info.Name);
    }

    [Fact]
    public void BattalionsTakeUpANewModelWhenItsEquipmentReachesThem()
    {
        var (s, capital) = Game();
        var regiment = s.AddRegiment(0, capital.Id, BattalionType.LightInfantry);
        s.Human.Learn(Tech.MilitaryTactics);
        RunDays(s, 1);
        Assert.Equal("Guerreros", regiment.Battalions[0].Info.Name); // no velites' weapons

        s.Human.AddEquipment(Velites, Velites.Pieces);
        RunDays(s, 1);
        Assert.Equal("Vélites", regiment.Battalions[0].Info.Name);
        Assert.Equal(0, s.Human.EquipmentOf(Velites));
        Assert.Equal(Warriors.Pieces, s.Human.EquipmentOf(Warriors)); // the old weapons go back into store
    }

    [Fact]
    public void ReinforcementsNeedTheirEquipment()
    {
        var (s, capital) = Game();
        var regiment = s.AddRegiment(0, capital.Id, BattalionType.LightInfantry);
        var b = regiment.Battalions[0];
        b.Strength = 50;
        RunDays(s, 2);
        Assert.Equal(50, b.Strength, 6); // men there are, but no weapons for them

        s.Human.AddEquipment(Warriors, 10);
        RunDays(s, 3);
        Assert.Equal(60, b.Strength, 6);
        Assert.Equal(0, s.Human.EquipmentOf(Warriors), 6);
    }

    [Fact]
    public void EquipmentAndProductionAreSaved()
    {
        var (s, capital) = Game();
        s.Human.AddEquipment(Warriors, 123);
        Assert.True(s.SetProduction(0, capital.Id, Warriors.Key).Ok);
        var loaded = GameSession.Load(_map, s.ToSave("test"));
        Assert.Equal(123, loaded.Human.EquipmentOf(Warriors), 6);
        Assert.Equal(Warriors.Key, _map.Provinces[capital.Id].Production);
    }

    [Fact]
    public void ArtilleryAndTanksAreAFewPiecesWithTheirCrews()
    {
        var catapults = BattalionType.Artillery.First();
        Assert.Equal((5, 50), (catapults.Pieces, catapults.Men));
        var tanks = BattalionType.Armour.Models()[1];
        Assert.Equal((10, 50), (tanks.Pieces, tanks.Men));
        Assert.Equal("5 catapultas", catapults.PiecesText(5));
        Assert.Equal("100 armas de guerreros", Warriors.PiecesText(100));
        // Training costs the gold; the rest of the cost is the equipment's.
        Assert.All(catapults.TrainingCost.Items, i => Assert.Equal(ResourceType.Gold, i.Type));
        Assert.DoesNotContain(catapults.EquipmentCost.Items, i => i.Type == ResourceType.Gold);
        Assert.False(BattalionType.Trireme.First().NeedsEquipment);
    }

    [Fact]
    public void WorkshopsAreSetBySupplyAndScoutsEngineersAndMedicsShareTheirs()
    {
        var (s, capital) = Game();
        s.Human.Learn(Tech.Engineering);
        s.Human.Learn(Tech.Medicine);
        s.Human.Learn(Tech.SiegeEngines);
        var producible = GameSession.ProducibleModels(s.Human).Select(x => x.Model.SupplyName).ToList();
        Assert.Contains("Armas (guerreros)", producible);
        Assert.Contains("Catapultas", producible);
        Assert.Single(producible, "Suministros");

        var scouts = BattalionType.Scouts.First();
        var engineers = BattalionType.Engineers.First();
        var medics = BattalionType.Medics.First();
        Assert.True(s.SetProduction(0, capital.Id, scouts.Key).Ok);
        RunDays(s, 1);
        double made = GameSession.ProductionRate(capital, scouts);
        Assert.Equal(made, s.Human.EquipmentOf(engineers), 6);
        Assert.Equal(made, s.Human.EquipmentOf(medics), 6);
        // Each piece costs the same whoever takes it.
        Assert.Equal(scouts.EquipmentCost.Items.Single().Amount / scouts.Pieces, engineers.EquipmentCost.Items.Single().Amount / engineers.Pieces, 6);
        Assert.Equal("50 suministros", medics.PiecesText(50));
    }

    [Fact]
    public void WhatTheWorkshopsUseCountsInTheDaysBalance()
    {
        var (s, capital) = Game();
        Assert.True(s.SetProduction(0, capital.Id, Warriors.Key).Ok);
        RunDays(s, 1);
        double wood = Warriors.EquipmentCost.Items.Single(i => i.Type == ResourceType.Wood).Amount * GameSession.ProductionRate(capital, Warriors) / Warriors.Pieces;
        Assert.Equal(-wood, s.Human.LastDayFlows[(int)ResourceFlow.Workshops][(int)ResourceType.Wood], 6);
        Assert.Equal(s.Human.LastDayNet[(int)ResourceType.Wood], s.Human.LastDayFlows.Sum(f => f[(int)ResourceType.Wood]), 6);
    }

    [Fact]
    public void OldSavesPutTheSupportTroopsSuppliesTogether()
    {
        var (s, _) = Game();
        var save = s.ToSave("test");
        var human = save.Players[0] with { Equipment = new() { ["scouts"] = 30, ["medics"] = 20 } };
        save = save with { Players = [human, .. save.Players.Skip(1)] };
        var loaded = GameSession.Load(_map, save);
        Assert.Equal(50, loaded.Human.EquipmentOf(BattalionType.Engineers.First()), 6);
    }
}
