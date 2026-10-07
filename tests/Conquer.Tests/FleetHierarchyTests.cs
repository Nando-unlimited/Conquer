using Conquer.Game.Buildings;
using Conquer.Game.Economy;
using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Science;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Tests;

/// <summary>The navy as the spec asked: flotillas, escuadras and fuerzas under Flotas and the Armada, the agreed crews, and fuel.</summary>
[Collection("World")]
public class FleetHierarchyTests(WorldFixture world)
{
    private readonly WorldMap _map = world.Map;

    /// <summary>A human capital in a port with navigation, plenty of everything and no computer rivals; and a sea next to it.</summary>
    private (GameSession Session, Province Port, Province Sea) WithPort()
    {
        var port = _map.Provinces.First(p => p.IsClaimable && p.Neighbors.Length > 2 && p.Neighbors.Any(n => _map.Provinces[n].Biome == Biome.ShallowSea));
        var s = GameSession.Create(_map, 2, seed: 7, computerRivals: false);
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, port.Id, 300).Id);
        port.Population = 5000;
        s.Human.Manpower = 5000;
        foreach (var t in new[] { Tech.Navigation, Tech.SteamEngine }) s.Human.Learn(t);
        foreach (var r in Resources.All) s.Human.Stockpile[r] = 5000;
        port.AddBuilding(BuildingType.Port);
        return (s, port, _map.Provinces[port.Neighbors.First(n => _map.Provinces[n].Biome == Biome.ShallowSea)]);
    }

    private static void ToMidnight(GameSession s)
    {
        do s.Step(); while (s.Date.Hour != 0);
    }

    [Fact]
    public void ShipsCarryTheAgreedCrewsAndTheCorbetaIsAnEscort()
    {
        int Crew(BattalionType line, string name) => line.Models().Single(m => m.Name == name).Men;
        Assert.Equal(200, Crew(BattalionType.LineShip, "Trirreme"));
        Assert.Equal(25, Crew(BattalionType.Escort, "Carabela"));
        Assert.Equal(120, Crew(BattalionType.Escort, "Corbeta"));
        Assert.Equal(250, Crew(BattalionType.LineShip, "Galeón"));
        Assert.Equal(250, Crew(BattalionType.Escort, "Fragata"));
        Assert.Equal(700, Crew(BattalionType.LineShip, "Navío de línea"));
        Assert.Equal(1000, Crew(BattalionType.LineShip, "Acorazado"));
        Assert.Equal(700, Crew(BattalionType.Escort, "Crucero"));
        Assert.Equal(300, Crew(BattalionType.Escort, "Destructor"));
        Assert.Equal(60, Crew(BattalionType.Submarine, "Submarino"));
        Assert.Equal(2000, Crew(BattalionType.AircraftCarrier, "Portaaviones"));
        // Sail burns nothing; steam, coal; modern ships, oil.
        Assert.Null(BattalionType.LineShip.Models()[2].Fuel);
        Assert.Equal(ResourceType.Coal, BattalionType.LineShip.Models()[3].Fuel);
        Assert.Equal(ResourceType.Oil, BattalionType.Escort.Models()[5].Fuel);
    }

    [Fact]
    public void FleetsGrowFromFlotillasIntoEscuadrasAndFuerzas()
    {
        var (s, port, _) = WithPort();
        var a = s.AddFleet(0, port.Id, BattalionType.LineShip, BattalionType.LineShip);
        Assert.Equal("1.ª Flotilla", a.Name);
        Assert.Equal(OfficerRank.Colonel, a.RequiredRank);
        var b = s.AddFleet(0, port.Id, BattalionType.Escort, BattalionType.Escort);
        Assert.True(s.Merge(0, a.Id, b.Id).Ok);
        Assert.Equal("1.ª Escuadra", a.Name);
        Assert.Equal(OfficerRank.Brigadier, a.RequiredRank);
        var c = s.AddFleet(0, port.Id, [.. Enumerable.Repeat(BattalionType.LineShip, 8)]);
        Assert.True(s.Merge(0, a.Id, c.Id).Ok);
        Assert.Equal("1.ª Fuerza", a.Name);
        var d = s.AddFleet(0, port.Id, [.. Enumerable.Repeat(BattalionType.LineShip, 16)]);
        Assert.StartsWith("Una fuerza tiene como mucho", s.CanMerge(a, d).Message);

        Assert.True(s.Split(0, a.Id, [0, 1, 2, 3, 4, 5, 6, 7]).Ok);
        Assert.Equal(NavalEchelon.Squadron, NavalEchelons.Of(a.Ships.Count));
        Assert.EndsWith("Escuadra", a.Name);
    }

    [Fact]
    public void FlotasCommandTheFleetsNearTheirPortAndTheArmadaTheFlotas()
    {
        var (s, port, sea) = WithPort();
        var fleet = s.AddFleet(0, sea.Id, BattalionType.LineShip);
        Assert.Equal(0, s.FleetCommandBonus(fleet));
        Assert.True(s.RaiseNavalHeadquarters(0, port.Id, 1).Ok);
        var flota = s.NavalHeadquarters.Single();
        Assert.Equal("1.ª Flota", flota.Name);
        Assert.Equal(OfficerBranch.Navy, flota.Officer!.Branch);
        Assert.True(s.AttachFleet(0, fleet.Id, flota.Id).Ok);
        Assert.Equal(MilitaryRules.FleetCommandBonus + flota.Officer.NavalFireBonus, s.FleetCommandBonus(fleet), 6);
        Assert.True(s.RaiseNavalHeadquarters(0, port.Id, 2).Ok);
        Assert.Equal("Armada", s.NavalHeadquarters.Single(h => h.IsNavy).Name);
        Assert.Equal("Ya tienes una Armada.", s.CanRaiseNavalHeadquarters(port, 2).Message);
        Assert.Equal(MilitaryRules.FleetCommandBonus + flota.Officer.NavalFireBonus + MilitaryRules.HigherFleetCommandBonus, s.FleetCommandBonus(fleet), 6);

        // Far from its Flota's port, the fleet is out of its command.
        fleet.ProvinceId = _map.Provinces.Where(p => p.IsWater).MaxBy(p => _map.DistanceKm(p, port))!.Id;
        Assert.False(s.InFleetCommand(fleet));
        Assert.True(s.DisbandNavalHeadquarters(0, flota.Id).Ok);
        Assert.Null(fleet.FleetCommanderId);
    }

    [Fact]
    public void SteamShipsBurnCoalAtSeaAndWithoutItCrawlAndFightAtHalf()
    {
        var (s, port, sea) = WithPort();
        s.Human.Learn(Tech.Metallurgy);
        s.Human.Learn(Tech.Steel);
        var fleet = s.AddFleet(0, sea.Id, BattalionType.LineShip); // an ironclad
        Assert.Equal("Acorazado", fleet.Ships[0].Info.Name);
        double coal = s.Human.Stockpile[ResourceType.Coal];
        ToMidnight(s);
        Assert.True(s.Human.Stockpile[ResourceType.Coal] < coal);
        Assert.False(fleet.OutOfFuel);

        s.Human.Stockpile[ResourceType.Coal] = 0;
        ToMidnight(s);
        Assert.True(fleet.OutOfFuel);
        // In port it refuels.
        fleet.ProvinceId = port.Id;
        ToMidnight(s);
        Assert.False(fleet.OutOfFuel);
    }

    [Fact]
    public void AircraftWithoutOilStayOnTheGroundAndTheirCrewsEat()
    {
        var (s, port, _) = WithPort();
        foreach (var t in new[] { Tech.Combustion, Tech.Electricity, Tech.Aviation }) s.Human.Learn(t);
        port.AddBuilding(BuildingType.Airfield);
        var unit = s.AddAirUnit(0, port.Id, BattalionType.Fighters, 3);
        Assert.True(s.SetAirMission(0, unit.Id, AirMission.AirSuperiority, port.Id).Ok);
        // Straight to the air phase, so the day's oil does not come in first.
        s.Human.Stockpile[ResourceType.Oil] = 0;
        s.DailyAir();
        Assert.True(unit.Grounded);
        Assert.False(s.IsFlying(unit));
        s.Human.Stockpile[ResourceType.Oil] = 100;
        s.DailyAir();
        Assert.False(unit.Grounded);
        Assert.Equal(100 - MilitaryRules.OilPerFlightDay * 3, s.Human.Stockpile[ResourceType.Oil], 6);

        // The crews eat with everyone else.
        ToMidnight(s);
        double eaten = -s.Human.LastDayFlows[(int)ResourceFlow.Consumption][(int)ResourceType.Food];
        s.DisbandAirUnit(0, unit.Id);
        ToMidnight(s);
        Assert.True(-s.Human.LastDayFlows[(int)ResourceFlow.Consumption][(int)ResourceType.Food] < eaten);
    }

    [Fact]
    public void HeadquartersFuelAndTheCorbetaAreSaved()
    {
        var (s, port, sea) = WithPort();
        s.Human.Learn(Tech.Metallurgy);
        var fleet = s.AddFleet(0, sea.Id, BattalionType.Escort); // a fragata
        Assert.Equal("Fragata", fleet.Ships[0].Info.Name);
        fleet.OutOfFuel = true;
        s.RaiseNavalHeadquarters(0, port.Id, 1);
        s.AttachFleet(0, fleet.Id, s.NavalHeadquarters[0].Id);
        var loaded = GameSession.Load(_map, s.ToSave("test"));
        var again = loaded.UnitById(fleet.Id)!;
        Assert.Equal((true, s.NavalHeadquarters[0].Id, "Fragata"), (again.OutOfFuel, again.FleetCommanderId!.Value, again.Ships[0].Info.Name));
        Assert.Equal(s.NavalHeadquarters[0].Officer!.Name, loaded.NavalHeadquarters.Single().Officer!.Name);

        // A save from before the corbeta had the fragata one place lower: it is moved up.
        fleet.Ships[0] = new Battalion(BattalionType.Escort, 2);
        var old = GameSession.Load(_map, s.ToSave("test") with { Corvettes = false });
        Assert.Equal("Fragata", old.UnitById(fleet.Id)!.Ships[0].Info.Name);
    }
}
