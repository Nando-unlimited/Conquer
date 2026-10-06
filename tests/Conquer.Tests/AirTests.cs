using Conquer.Game.Buildings;
using Conquer.Game.Economy;
using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Science;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Tests;

/// <summary>The air force: wings of aircraft based at airfields and carriers.</summary>
[Collection("World")]
public class AirTests(WorldFixture world)
{
    private readonly WorldMap _map = world.Map;

    /// <summary>A human capital with an airfield, aviation, plenty of everything and the planes in store.</summary>
    private (GameSession Session, Province Capital) WithAirfield()
    {
        var s = GameSession.Create(_map, 2, seed: 7, computerRivals: false);
        var capital = _map.Provinces.First(p => p.Biome == Biome.Grassland && p.Neighbors.Length > 3 && !p.IsOwned);
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, capital.Id, 300).Id);
        capital.Population = 5000;
        s.Human.Manpower = 5000;
        foreach (var t in new[] { Tech.Combustion, Tech.Electricity, Tech.Aviation }) s.Human.Learn(t);
        foreach (var r in Resources.All) s.Human.Stockpile[r] = 5000;
        s.Human.Arm();
        capital.AddBuilding(BuildingType.Airfield);
        return (s, capital);
    }

    private static void ToMidnight(GameSession s)
    {
        do s.Step(); while (s.Date.Hour != 0);
    }

    [Fact]
    public void EachKindOfAircraftHasAModelPerAge()
    {
        var air = Battalions.All.Where(t => t.Line().Group == BattalionGroup.Air).ToList();
        Assert.Equal([BattalionType.Bombers, BattalionType.Fighters, BattalionType.CloseSupport, BattalionType.NavalBombers, BattalionType.AirTransports], air);
        foreach (var type in air)
        {
            Assert.Equal([[Tech.Aviation], [Tech.Radar], [Tech.JetEngine]], type.Models().Select(m => m.Requires));
            Assert.All(type.Models(), m => Assert.True(m.Flies && m.Pieces == MilitaryRules.PlanesPerWing && m.RangeKm > 0));
            // Each age flies further.
            Assert.Equal(type.Models().Select(m => m.RangeKm).Order(), type.Models().Select(m => m.RangeKm));
        }
        Assert.True(BattalionType.Fighters.First().AirAttack > BattalionType.Bombers.First().AirAttack);
        Assert.True(BattalionType.AirTransports.First().Capacity > 0);
    }

    [Fact]
    public void WingsAreFormedAtAirfieldsUpToTheirRoom()
    {
        var (s, capital) = WithAirfield();
        var elsewhere = _map.Provinces.First(p => p.IsClaimable && !p.IsOwned && _map.DistanceKm(p, capital) > 300);
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, elsewhere.Id, 300).Id);
        elsewhere.Population = 3000;
        Assert.Equal("Requiere un aeródromo en la provincia.", s.CanTrain(elsewhere, BattalionType.Fighters).Message);

        for (int i = 0; i < MilitaryRules.WingsPerAirfield; i++) Assert.True(s.Train(0, capital.Id, BattalionType.Fighters).Ok);
        Assert.StartsWith("El aeródromo está lleno", s.CanTrain(capital, BattalionType.Fighters).Message);
        for (int d = 0; d <= BattalionType.Fighters.First().TrainingDays; d++) ToMidnight(s);
        Assert.Equal(MilitaryRules.WingsPerAirfield, s.WingsAt(capital).Count());
        var wing = s.WingsAt(capital).First();
        Assert.Equal("1.ª Ala de cazas", wing.Name);
        Assert.Equal(MilitaryRules.PlanesPerWing, wing.PlaneCount, 6);
        Assert.DoesNotContain(s.Units, u => u.Flies);
        Assert.False(s.CanAddToTemplate(s.Human, s.Human.Templates[0], BattalionType.Fighters).Ok);
    }

    [Fact]
    public void WingsRebaseWithinTwiceTheirRangeAndOnlySomeFlyFromCarriers()
    {
        var (s, capital) = WithAirfield();
        var fighters = s.AddWing(0, capital.Id, BattalionType.Fighters);
        var bombers = s.AddWing(0, capital.Id, BattalionType.Bombers);
        var near = _map.Provinces.First(p => p.IsClaimable && !p.IsOwned && _map.DistanceKm(p, capital) is > 100 and < 500);
        s.Claim(0, s.AddRegiment(0, near.Id, BattalionType.Scouts).Id);
        Assert.False(s.Rebase(0, fighters.Id, near.Id).Ok); // no airfield there
        near.AddBuilding(BuildingType.Airfield);
        Assert.True(s.Rebase(0, fighters.Id, near.Id).Ok);
        Assert.Equal(near.Id, fighters.BaseProvinceId);

        var far = _map.Provinces.First(p => p.IsClaimable && !p.IsOwned && _map.DistanceKm(p, near) > 2 * fighters.Info.RangeKm + 100);
        s.Claim(0, s.AddRegiment(0, far.Id, BattalionType.Scouts).Id);
        far.AddBuilding(BuildingType.Airfield);
        Assert.StartsWith("Está a más de", s.Rebase(0, fighters.Id, far.Id).Message);

        var sea = _map.Provinces.Where(p => p.IsWater).MinBy(p => _map.DistanceKm(p, capital))!;
        s.Human.Learn(Tech.NavalEngineering);
        var fleet = s.AddFleet(0, sea.Id, BattalionType.AircraftCarrier);
        Assert.Equal(MilitaryRules.WingsPerCarrier, s.CarrierRoom(fleet));
        Assert.StartsWith("Solo cazas", s.Rebase(0, bombers.Id, 0, fleet.Id).Message);
        Assert.True(s.Rebase(0, fighters.Id, 0, fleet.Id).Ok);
        Assert.Equal(fleet.Id, fighters.CarrierId);
        Assert.Equal(sea.Id, s.BaseOf(fighters).Id);
    }

    [Fact]
    public void AWingThatLosesItsBaseFliesToAnotherOrIsLost()
    {
        var (s, capital) = WithAirfield();
        var wing = s.AddWing(0, capital.Id, BattalionType.CloseSupport);
        var other = _map.Provinces.First(p => p.IsClaimable && !p.IsOwned && _map.DistanceKm(p, capital) > 300);
        s.Claim(0, s.AddRegiment(0, other.Id, BattalionType.Scouts).Id);
        other.AddBuilding(BuildingType.Airfield);
        capital.RemoveBuilding(BuildingType.Airfield);
        ToMidnight(s);
        Assert.Equal(other.Id, wing.BaseProvinceId);

        other.RemoveBuilding(BuildingType.Airfield);
        ToMidnight(s);
        Assert.Empty(s.Wings);
    }

    [Fact]
    public void WingsAreRepairedAtTheirBaseAndDisbandingReturnsTheirPlanes()
    {
        var (s, capital) = WithAirfield();
        var wing = s.AddWing(0, capital.Id, BattalionType.Fighters);
        wing.Planes.Strength = wing.Info.Men / 2.0;
        wing.Planes.Organisation = 0;
        double planes = s.Human.EquipmentOf(wing.Info);
        ToMidnight(s);
        Assert.True(wing.Planes.Strength > wing.Info.Men / 2.0);
        Assert.True(wing.Planes.Organisation > 0);
        Assert.True(s.Human.EquipmentOf(wing.Info) < planes);

        planes = s.Human.EquipmentOf(wing.Info);
        double count = wing.PlaneCount;
        Assert.True(s.DisbandWing(0, wing.Id).Ok);
        Assert.Empty(s.Wings);
        Assert.Equal(planes + count, s.Human.EquipmentOf(wing.Info), 6);
    }

    [Fact]
    public void ACarrierSunkTakesItsWingsDown()
    {
        var (s, capital) = WithAirfield();
        s.Human.Learn(Tech.NavalEngineering);
        var sea = _map.Provinces.Where(p => p.IsWater).MinBy(p => _map.DistanceKm(p, capital))!;
        var fleet = s.AddFleet(0, sea.Id, BattalionType.AircraftCarrier);
        var wing = s.AddWing(0, capital.Id, BattalionType.NavalBombers);
        wing.BaseProvinceId = sea.Neighbors.First(n => !_map.Provinces[n].IsWater); // flown to the coast first
        Assert.True(s.Rebase(0, wing.Id, 0, fleet.Id).Ok);
        foreach (var ship in fleet.Ships) ship.Strength = 0;
        s.AddFleet(1, sea.Id, BattalionType.LineShip);
        s.DeclareWar(0, 1);
        for (int h = 0; h < 48 && s.UnitById(fleet.Id) != null; h++) s.Step();
        Assert.Null(s.UnitById(fleet.Id));
        Assert.Empty(s.Wings);
    }

    [Fact]
    public void WingsAreSaved()
    {
        var (s, capital) = WithAirfield();
        var wing = s.AddWing(0, capital.Id, BattalionType.Bombers);
        wing.Planes.Strength = 20;
        wing.Mission = AirMission.StrategicBombing;
        wing.TargetProvinceId = capital.Neighbors[0];
        var loaded = GameSession.Load(_map, s.ToSave("test"));
        var again = Assert.Single(loaded.Wings);
        Assert.Equal((wing.Id, wing.Name, wing.BaseProvinceId, wing.Mission, wing.TargetProvinceId, 20.0),
            (again.Id, again.Name, again.BaseProvinceId, again.Mission, again.TargetProvinceId, again.Planes.Strength));
        Assert.Equal("2.ª Ala de cazas", loaded.AddWing(0, capital.Id, BattalionType.Fighters).Name);
    }
}
