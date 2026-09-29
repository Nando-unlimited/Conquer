using Conquer.Game.Buildings;
using Conquer.Game.Economy;
using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Science;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Tests;

/// <summary>Fleets: built in ports, sailing as far as their nation can navigate, carrying troops and fighting at sea.</summary>
[Collection("World")]
public class NavalTests(WorldFixture world)
{
    private readonly WorldMap _map = world.Map;

    /// <summary>A coastal province for a port, a coastal sea next to it and another coast across that sea.</summary>
    private (Province Port, Province Sea, Province Landing) Coast() =>
        _map.Provinces.Where(p => p.IsClaimable && p.Neighbors.Length > 2)
            .SelectMany(p => p.Neighbors.Select(n => _map.Provinces[n]).Where(s => s.Biome == Biome.ShallowSea)
                .SelectMany(s => s.Neighbors.Select(l => _map.Provinces[l])
                    .Where(l => l.IsClaimable && l.Id != p.Id && !p.Neighbors.Contains(l.Id)).Select(l => (p, s, l))))
            .First();

    /// <summary>A human capital in the port, with navigation and plenty of everything.</summary>
    private (GameSession Session, Province Port, Province Sea, Province Landing) WithPort(int players = 1)
    {
        var (port, sea, landing) = Coast();
        var s = GameSession.Create(_map, players, seed: 7, computerRivals: false);
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, port.Id, 300).Id);
        port.Population = 3000;
        s.Human.Learn(Tech.Navigation);
        foreach (var r in Resources.All) s.Human.Stockpile[r] = 5000;
        port.AddBuilding(BuildingType.Port);
        return (s, port, sea, landing);
    }

    private static void RunUntil(GameSession s, Func<bool> done, int maxHours = 24 * 60)
    {
        for (int h = 0; h < maxHours && !done(); h++) s.Step();
    }

    [Fact]
    public void TroopsNeedShipsToCrossTheSea()
    {
        var (s, port, sea, _) = WithPort();
        var regiment = s.AddRegiment(0, port.Id, BattalionType.Warriors);
        var bombers = s.AddRegiment(0, port.Id, BattalionType.Bombers);

        Assert.False(s.CanUnitEnter(regiment, sea.Id));
        Assert.StartsWith("Para cruzar el mar hay que embarcar", s.MoveUnit(0, regiment.Id, sea.Id).Message);
        Assert.True(s.CanUnitEnter(bombers, sea.Id));
    }

    [Fact]
    public void ShipsAreBuiltInPortsAndFormFleets()
    {
        var (s, port, _, _) = WithPort();
        var inland = _map.Provinces.First(p => p.IsClaimable && !p.IsOwned && p.Neighbors.All(n => !_map.Provinces[n].IsWater)
                                               && _map.DistanceKm(p, port) > 400);
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, inland.Id, 300).Id);
        inland.Population = 3000;

        Assert.Equal("Los barcos solo se construyen en ciudades con puerto.", s.CanTrain(s.CityIn(inland)!, BattalionType.Trireme).Message);
        Assert.True(s.Train(0, s.CityIn(port)!.Id, BattalionType.Trireme).Ok);
        RunUntil(s, () => s.Units.Any(u => u.IsFleet));

        var fleet = s.Units.Single(u => u.IsFleet);
        Assert.Equal(port.Id, fleet.ProvinceId);
        Assert.Equal(BattalionType.Trireme, fleet.Battalions.Single().Type);
        Assert.False(s.CanAddToTemplate(s.Human, s.Human.Templates[0], BattalionType.Trireme).Ok);
    }

    [Fact]
    public void FleetsSailAsFarAsTheirNationCanNavigate()
    {
        var (s, port, sea, landing) = WithPort();
        var fleet = s.AddFleet(0, port.Id, BattalionType.Trireme);
        var ocean = _map.Provinces.First(p => p.Biome == Biome.Ocean);

        Assert.True(s.CanUnitEnter(fleet, sea.Id));
        Assert.False(s.CanUnitEnter(fleet, landing.Id)); // land that is not one of its ports
        Assert.False(s.CanUnitEnter(fleet, ocean.Id));
        s.Human.Learn(Tech.Cartography);
        Assert.True(s.CanUnitEnter(fleet, ocean.Id));
    }

    [Fact]
    public void TransportsCarryTroopsAcrossTheSea()
    {
        var (s, port, sea, landing) = WithPort();
        var fleet = s.AddFleet(0, port.Id, BattalionType.Transport);
        var regiment = s.AddRegiment(0, port.Id, BattalionType.Warriors, BattalionType.Warriors);
        var tooBig = s.AddRegiment(0, port.Id, [.. Enumerable.Repeat(BattalionType.Warriors, 6)]);

        Assert.True(s.Embark(0, regiment.Id, fleet.Id).Ok);
        Assert.True(regiment.IsAboard);
        Assert.StartsWith("No cabe", s.Embark(0, tooBig.Id, fleet.Id).Message);

        Assert.True(s.MoveUnit(0, fleet.Id, sea.Id).Ok);
        RunUntil(s, () => fleet.ProvinceId == sea.Id);
        Assert.Equal(sea.Id, regiment.ProvinceId);

        Assert.True(s.MoveUnit(0, regiment.Id, landing.Id).Ok);
        Assert.False(regiment.IsAboard);
        Assert.Equal(landing.Id, regiment.ProvinceId);
    }

    [Fact]
    public void ARightClickOnAFleetAtSeaBoardsIt()
    {
        var (s, port, sea, _) = WithPort();
        var fleet = s.AddFleet(0, sea.Id, BattalionType.Transport);
        var settlers = s.AddUnit(0, UnitType.Settlers, port.Id, 300);

        Assert.True(s.MoveUnit(0, settlers.Id, sea.Id).Ok);

        Assert.Equal(fleet.Id, settlers.CarrierId);
    }

    [Fact]
    public void WarshipsSinkAnEnemyFleetAndWhatItCarries()
    {
        var (s, _, sea, _) = WithPort(players: 2);
        s.DeclareWar(0, 1);
        var ours = s.AddFleet(0, sea.Id, BattalionType.Trireme, BattalionType.Trireme, BattalionType.Trireme);
        var theirs = s.AddFleet(1, sea.Id, BattalionType.Transport);
        var aboard = s.AddRegiment(1, sea.Id, BattalionType.Warriors);
        aboard.CarrierId = theirs.Id;

        Assert.Contains(sea.Id, s.NavalBattleProvinces());
        RunUntil(s, () => s.UnitById(theirs.Id) is null, 24 * 5);

        // Their nation cannot sail, so the broken fleet has nowhere to flee.
        Assert.Null(s.UnitById(theirs.Id));
        Assert.Null(s.UnitById(aboard.Id));
        Assert.NotNull(s.UnitById(ours.Id));
    }

    [Fact]
    public void MergingFleetsKeepsTheirCargo()
    {
        var (s, port, _, _) = WithPort();
        var a = s.AddFleet(0, port.Id, BattalionType.Trireme);
        var b = s.AddFleet(0, port.Id, BattalionType.Transport);
        var regiment = s.AddRegiment(0, port.Id, BattalionType.Warriors);
        s.Embark(0, regiment.Id, b.Id);

        Assert.True(s.Merge(0, a.Id, b.Id).Ok);

        Assert.Equal(a.Id, regiment.CarrierId);
        Assert.False(s.Split(0, a.Id, 1).Ok); // the transport cannot leave with the regiment still aboard
    }

    [Fact]
    public void FleetsAndCargoAreSaved()
    {
        var (s, port, _, _) = WithPort();
        var fleet = s.AddFleet(0, port.Id, BattalionType.Transport);
        var regiment = s.AddRegiment(0, port.Id, BattalionType.Warriors);
        s.Embark(0, regiment.Id, fleet.Id);

        var loaded = GameSession.Load(_map, s.ToSave("test"));

        Assert.True(loaded.UnitById(fleet.Id)!.IsFleet);
        Assert.Equal(fleet.Id, loaded.UnitById(regiment.Id)!.CarrierId);
    }

    [Fact]
    public void ShipsSailFasterThanPeopleWalk()
    {
        var s = GameSession.Create(_map, 1, seed: 7, computerRivals: false);
        var (sea, next) = _map.Provinces.Where(p => p.IsWater)
            .SelectMany(p => p.Neighbors.Select(n => (p, _map.Provinces[n]))).First(t => t.Item2.IsWater);

        Assert.Equal(_map.DistanceKm(sea, next) / (GameRules.CitizenSpeedKmh * GameRules.SailingSpeed), s.Pathfinder.StepHours(sea.Id, next.Id), 6);
    }

    [Fact]
    public void APortNeedsTheCoastAndNavigation()
    {
        var (s, port, _, _) = WithPort();
        var inland = _map.Provinces.First(p => p.IsClaimable && !p.IsOwned && p.Neighbors.All(n => !_map.Provinces[n].IsWater)
                                               && _map.DistanceKm(p, port) > 400);
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, inland.Id, 300).Id);

        Assert.Equal("Solo en provincias con costa.", s.IsBuildingAvailable(inland, BuildingType.Port).Message);
        s.Human.Techs.Remove(Tech.Navigation);
        Assert.False(s.IsBuildingAvailable(port, BuildingType.Port).Ok);
    }

    [Fact]
    public void WithoutAPortBuildingACityBuildsNoShipsAndShelters()
    {
        var (s, port, _, _) = WithPort();
        var fleet = s.AddFleet(0, port.Id, BattalionType.Trireme);
        port.ClearBuildings();

        Assert.False(s.IsPort(port, 0));
        Assert.False(s.CanTrain(s.CityIn(port)!, BattalionType.Trireme).Ok);
        Assert.False(s.CanUnitEnter(fleet, port.Id));
    }

    [Fact]
    public void AdvancedShipsNeedADryDock()
    {
        var (s, port, _, _) = WithPort();
        var city = s.CityIn(port)!;
        s.Human.Learn(Tech.NavalEngineering);

        Assert.StartsWith("Requiere dique seco", s.CanTrain(city, BattalionType.Destroyer).Message);
        Assert.True(s.IsBuildingAvailable(port, BuildingType.DryDock).Ok);
        port.AddBuilding(BuildingType.DryDock);
        Assert.True(s.CanTrain(city, BattalionType.Destroyer).Ok);
        Assert.False(s.CanTrain(city, BattalionType.AircraftCarrier).Ok); // also needs aviation
    }

    [Fact]
    public void ADryDockNeedsAPortFirst()
    {
        var (s, port, _, _) = WithPort();
        s.Human.Learn(Tech.NavalEngineering);
        port.ClearBuildings();
        Assert.Equal("Requiere puerto.", s.IsBuildingAvailable(port, BuildingType.DryDock).Message);
    }

    [Fact]
    public void ADryDockRepairsFleetsTwiceAsFast()
    {
        double Repaired(bool dock)
        {
            var (s, port, _, _) = WithPort();
            if (dock) port.AddBuilding(BuildingType.DryDock);
            var fleet = s.AddFleet(0, port.Id, BattalionType.Trireme);
            fleet.Battalions[0].Organisation = 0;
            for (int h = 0; h < 24; h++) s.Step();
            return fleet.Battalions[0].Organisation;
        }

        Assert.Equal(Repaired(dock: false) * MilitaryRules.DryDockRepair, Repaired(dock: true), 6);
    }
}
