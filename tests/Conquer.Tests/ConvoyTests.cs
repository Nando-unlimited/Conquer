using Conquer.Game.Buildings;
using Conquer.Game.Economy;
using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Science;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Tests;

/// <summary>Convoys over the sea, and the fleets' missions: patrol, escort, raid and blockade.</summary>
[Collection("World")]
public class ConvoyTests(WorldFixture world)
{
    private readonly WorldMap _map = world.Map;

    /// <summary>A coastal province for a port, a coastal sea next to it and a coast across that sea, not joined by land.</summary>
    private (Province Port, Province Sea, Province Landing) Coast()
    {
        var land = new int[_map.Provinces.Count];
        int mass = 0;
        foreach (var start in _map.Provinces.Where(p => !p.IsWater))
        {
            if (land[start.Id] != 0) continue;
            mass++;
            var stack = new Stack<int>([start.Id]);
            land[start.Id] = mass;
            while (stack.TryPop(out int id))
                foreach (int n in _map.Provinces[id].Neighbors)
                    if (!_map.Provinces[n].IsWater && land[n] == 0) { land[n] = mass; stack.Push(n); }
        }
        return _map.Provinces.Where(p => p.IsClaimable && p.Neighbors.Length > 2)
            .SelectMany(p => p.Neighbors.Select(n => _map.Provinces[n]).Where(s => s.Biome == Biome.ShallowSea)
                .SelectMany(s => s.Neighbors.Select(l => _map.Provinces[l])
                    .Where(l => l.IsClaimable && land[l.Id] != land[p.Id]).Select(l => (p, s, l))))
            .First();
    }

    /// <summary>
    /// The human's capital is a port; a second city across the sea keeps a regiment there in supply. Player 1 is at war
    /// with the human. No computer rivals.
    /// </summary>
    private (GameSession S, Province Port, Province Sea, Province Landing, Unit Regiment) Overseas()
    {
        var (port, sea, landing) = Coast();
        var s = GameSession.Create(_map, 2, seed: 7, computerRivals: false);
        s.Human.Arm();
        s.Human.Learn(Tech.Navigation);
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, port.Id, 300).Id);
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, landing.Id, 300).Id);
        Assert.Equal(_map.Provinces[s.Human.CapitalCityId is int c ? s.CityById(c)!.ProvinceId : -1], port);
        port.Population = 5000;
        s.Human.Manpower = 5000;
        foreach (var r in Resources.All) s.Human.Stockpile[r] = 5000;
        port.AddBuilding(BuildingType.Port);
        var regiment = s.AddRegiment(0, landing.Id, BattalionType.LightInfantry, BattalionType.LightInfantry);
        foreach (var b in regiment.Battalions) b.Strength = 50;
        Assert.True(s.DeclareWar(0, 1).Ok);
        return (s, port, sea, landing, regiment);
    }

    private static void ToMidnight(GameSession s)
    {
        do s.Step(); while (s.Date.Hour != 0);
    }

    [Fact]
    public void ShipmentsOverTheSeaNeedConvoys()
    {
        var (s, _, sea, _, regiment) = Overseas();
        ToMidnight(s);
        Assert.Empty(s.ShipmentsTo(regiment));
        Assert.True(s.Human.CargoLeftForWantOfConvoys > 0);

        s.Human.Convoys = 10;
        ToMidnight(s);
        var shipment = Assert.Single(s.ShipmentsTo(regiment));
        Assert.Contains(sea.Id, shipment.SeaRoute);
        Assert.Equal(GameSession.Cargo(shipment) / MilitaryRules.ConvoyCapacity, shipment.Convoys, 6);
        Assert.Equal(10 - shipment.Convoys, s.FreeConvoys(s.Human), 6);
        Assert.Equal(0, s.Human.CargoLeftForWantOfConvoys);
    }

    [Fact]
    public void RaidersSinkConvoysAndEscortsProtectThem()
    {
        double Loss(bool escorted)
        {
            var (s, port, sea, _, regiment) = Overseas();
            s.Human.Convoys = 10;
            var raider = s.AddFleet(1, sea.Id, BattalionType.LineShip);
            Assert.True(s.SetFleetMission(1, raider.Id, FleetMission.Raid).Ok);
            if (escorted)
            {
                var escort = s.AddFleet(0, sea.Id, BattalionType.LineShip, BattalionType.LineShip);
                s.SetFleetMission(0, escort.Id, FleetMission.Escort);
                // Not meeting in battle: the escort keeps to a neighbouring sea.
                escort.ProvinceId = sea.Neighbors.First(n => _map.Provinces[n].IsWater && n != raider.ProvinceId);
            }
            raider.ProvinceId = sea.Neighbors.First(n => _map.Provinces[n].IsWater);
            ToMidnight(s);
            var shipment = s.ShipmentsTo(regiment).Single();
            double convoys = shipment.Convoys;
            ToMidnight(s);
            return 1 - shipment.Convoys / convoys;
        }
        double alone = Loss(false), escorted = Loss(true);
        Assert.True(alone > 0.1, $"{alone}");
        Assert.True(escorted < alone / 2, $"{escorted} vs {alone}");
    }

    [Fact]
    public void ABlockadedPortBuildsNothingSendsNothingBySeaAndLosesItsSeaTrade()
    {
        var (s, port, sea, _, regiment) = Overseas();
        s.Human.Convoys = 10;
        double taxes = s.BonusesOf(port).Taxes;
        var blockade = s.AddFleet(1, sea.Id, BattalionType.LineShip);
        Assert.True(s.SetFleetMission(1, blockade.Id, FleetMission.Blockade).Ok);
        Assert.True(s.IsBlockaded(port));
        Assert.Equal([port], s.Blockading(blockade));
        Assert.Equal(taxes - BuildingType.Port.Info().Effects.Taxes, s.BonusesOf(port).Taxes, 6);
        Assert.Equal("El puerto está bloqueado por el enemigo.", s.CanBuildShipIn(port, 0, BattalionType.Transport.First()).Message);

        // Their battle aside, nothing leaves the port for the troops across the sea.
        blockade.ProvinceId = sea.Id;
        ToMidnight(s);
        Assert.Empty(s.ShipmentsTo(regiment));
    }

    [Fact]
    public void PatrolsGoAfterEnemyFleetsInTheNeighbouringSeas()
    {
        var (s, _, sea, _, _) = Overseas();
        var patrol = s.AddFleet(0, sea.Id, BattalionType.LineShip);
        s.SetFleetMission(0, patrol.Id, FleetMission.Patrol);
        int next = sea.Neighbors.First(n => _map.Provinces[n].IsWater && GameSession.CanSail(s.Human, _map.Provinces[n]));
        s.AddFleet(1, next, BattalionType.Transport);
        s.Step();
        Assert.True(patrol.IsMoving || patrol.ProvinceId == next);
    }

    [Fact]
    public void ConvoysAreBuiltInTheShipyards()
    {
        var (s, _, _, _, _) = Overseas();
        Assert.True(s.OrderConvoys(0).Ok);
        Assert.True(s.Human.ShipOrders.Single().Convoys);
        for (int d = 0; d <= Battalions.ConvoyBatch.TrainingDays; d++) ToMidnight(s);
        Assert.Equal(MilitaryRules.ConvoysPerOrder, s.Human.Convoys);
        Assert.DoesNotContain(s.Units, u => u.IsFleet);
    }

    [Fact]
    public void ConvoysMissionsAndRoutesAreSaved()
    {
        var (s, _, sea, _, regiment) = Overseas();
        s.Human.Convoys = 10;
        var fleet = s.AddFleet(0, sea.Id, BattalionType.LineShip);
        s.SetFleetMission(0, fleet.Id, FleetMission.Escort);
        s.OrderConvoys(0);
        ToMidnight(s);
        var loaded = GameSession.Load(_map, s.ToSave("test"));
        Assert.Equal(10, loaded.Human.Convoys);
        Assert.Equal(FleetMission.Escort, loaded.UnitById(fleet.Id)!.Mission);
        Assert.True(loaded.Human.ShipOrders.Single().Convoys);
        var shipment = loaded.Shipments.Single(x => x.UnitId == regiment.Id);
        Assert.Equal(s.ShipmentsTo(regiment).Single().SeaRoute, shipment.SeaRoute);
        Assert.Equal(s.ShipmentsTo(regiment).Single().Convoys, shipment.Convoys, 6);

        // A save from before convoys gives a nation with a port some.
        var old = s.ToSave("test");
        old = old with { Players = [.. old.Players.Select(p => p with { Convoys = null })] };
        Assert.Equal(MilitaryRules.ConvoysInOldSaves, GameSession.Load(_map, old).Human.Convoys);
    }
}
