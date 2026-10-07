using Conquer.Game.Buildings;
using Conquer.Game.Economy;
using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Science;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Tests;

/// <summary>The air force: escuadrillas joined into escuadrones, grupos and alas, based at airfields and carriers, under air HQs.</summary>
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
    public void EachKindOfAircraftFliesInEscuadrillasOfSixWithItsCrewsPerPlane()
    {
        var air = Battalions.All.Where(t => t.Line().Group == BattalionGroup.Air).ToList();
        Assert.Equal([BattalionType.Bombers, BattalionType.Fighters, BattalionType.CloseSupport, BattalionType.TacticalBombers, BattalionType.NavalBombers,
            BattalionType.AirTransports], air);
        var crews = new Dictionary<BattalionType, int>
        {
            [BattalionType.Fighters] = 1, [BattalionType.CloseSupport] = 2, [BattalionType.TacticalBombers] = 4, [BattalionType.Bombers] = 10,
            [BattalionType.NavalBombers] = 2, [BattalionType.AirTransports] = 4,
        };
        foreach (var type in air)
        {
            Assert.Equal([[Tech.Aviation], [Tech.Radar], [Tech.JetEngine]], type.Models().Select(m => m.Requires));
            Assert.All(type.Models(), m => Assert.Equal((MilitaryRules.PlanesPerFlight, crews[type] * MilitaryRules.PlanesPerFlight), (m.Pieces, m.Men)));
            Assert.Equal(type.Models().Select(m => m.RangeKm).Order(), type.Models().Select(m => m.RangeKm));
        }
        Assert.Equal([AirMission.CloseSupport, AirMission.StrategicBombing], GameSession.MissionsFor(BattalionType.TacticalBombers));
    }

    [Fact]
    public void EscuadrillasAreFormedAtAirfieldsUpToTheirRoom()
    {
        var (s, capital) = WithAirfield();
        var elsewhere = _map.Provinces.First(p => p.IsClaimable && !p.IsOwned && _map.DistanceKm(p, capital) > 300);
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, elsewhere.Id, 300).Id);
        elsewhere.Population = 3000;
        Assert.Equal("Requiere un aeródromo en la provincia.", s.CanTrain(elsewhere, BattalionType.Fighters).Message);

        Assert.True(s.Train(0, capital.Id, BattalionType.Fighters).Ok);
        for (int d = 0; d <= BattalionType.Fighters.First().TrainingDays; d++) ToMidnight(s);
        var unit = Assert.Single(s.AirUnitsAt(capital));
        Assert.Equal("1.ª Escuadrilla de cazas", unit.Name);
        Assert.Equal(MilitaryRules.PlanesPerFlight, unit.PlaneCount, 6);
        Assert.DoesNotContain(s.Units, u => u.Flies);
        Assert.False(s.CanAddToTemplate(s.Human, s.Human.Templates[0], BattalionType.Fighters).Ok);

        s.AddAirUnit(0, capital.Id, BattalionType.Bombers, MilitaryRules.FlightsPerAirfield - 1);
        Assert.Equal(0, s.AirfieldRoom(capital, 0));
        Assert.StartsWith("El aeródromo está lleno", s.CanTrain(capital, BattalionType.Fighters).Message);
    }

    [Fact]
    public void EscuadrillasJoinIntoEscuadronesGruposAndAlasAndSplit()
    {
        var (s, capital) = WithAirfield();
        var a = s.AddAirUnit(0, capital.Id, BattalionType.Fighters);
        var b = s.AddAirUnit(0, capital.Id, BattalionType.Fighters);
        var bombers = s.AddAirUnit(0, capital.Id, BattalionType.Bombers);
        Assert.Equal("Solo se unen aviones del mismo tipo.", s.CanJoin(a, bombers).Message);
        Assert.True(s.JoinAirUnits(0, a.Id, b.Id).Ok);
        Assert.Equal((AirEchelon.Squadron, "1.er Escuadrón de cazas"), (a.Size, a.Name));
        Assert.Null(s.AirUnitById(b.Id));

        var more = s.AddAirUnit(0, capital.Id, BattalionType.Fighters, 2);
        Assert.True(s.JoinAirUnits(0, a.Id, more.Id).Ok);
        Assert.Equal(AirEchelon.Group, a.Size);
        Assert.Equal(AirEchelon.Wing, AirEchelons.Of(AirEchelons.FlightsPerGroup + 1));
        Assert.Equal("1.ª Ala", AirEchelon.Wing.Numbered(1));

        Assert.True(s.SplitAirUnit(0, a.Id, 1).Ok);
        Assert.Equal(3, a.Flights.Count);
        Assert.Equal(AirEchelon.Squadron, a.Size);
        Assert.Contains(s.AirUnits, u => u != a && u.Type == BattalionType.Fighters && u.Size == AirEchelon.Flight);
        Assert.False(s.SplitAirUnit(0, bombers.Id, 1).Ok);
    }

    [Fact]
    public void AirDivisionsCommandTheUnitsNearThemAndTheAirCommandTheDivisions()
    {
        var (s, capital) = WithAirfield();
        var unit = s.AddAirUnit(0, capital.Id, BattalionType.Fighters);
        Assert.Equal(0, s.AirCommandBonus(unit));
        Assert.True(s.RaiseAirHeadquarters(0, capital.Id, 1).Ok);
        var division = s.AirHeadquarters.Single();
        Assert.Equal("1.ª División aérea", division.Name);
        Assert.Equal(OfficerBranch.Air, division.Officer!.Branch);
        Assert.True(s.AttachAirUnit(0, unit.Id, division.Id).Ok);
        Assert.True(s.InAirCommand(unit));
        double bonus = s.AirCommandBonus(unit);
        Assert.Equal(MilitaryRules.AirCommandBonus + division.Officer.NavalFireBonus, bonus, 6);

        Assert.True(s.RaiseAirHeadquarters(0, capital.Id, 2).Ok);
        Assert.Equal("Ya tienes un Mando aéreo.", s.CanRaiseAirHeadquarters(capital, 2).Message);
        Assert.Equal(bonus + MilitaryRules.HigherAirCommandBonus, s.AirCommandBonus(unit), 6);

        for (int i = 1; i < MilitaryRules.MaxUnitsPerAirDivision; i++) s.AttachAirUnit(0, s.AddAirUnit(0, capital.Id, BattalionType.Bombers).Id, division.Id);
        Assert.StartsWith("Una división aérea manda", s.AttachAirUnit(0, s.AddAirUnit(0, capital.Id, BattalionType.Bombers).Id, division.Id).Message);
        Assert.True(s.DisbandAirHeadquarters(0, division.Id).Ok);
        Assert.Null(unit.CommanderId);
    }

    [Fact]
    public void UnitsRebaseWithinTwiceTheirRangeAndOnlySomeFlyFromCarriers()
    {
        var (s, capital) = WithAirfield();
        var fighters = s.AddAirUnit(0, capital.Id, BattalionType.Fighters);
        var bombers = s.AddAirUnit(0, capital.Id, BattalionType.Bombers);
        var near = _map.Provinces.First(p => p.IsClaimable && !p.IsOwned && _map.DistanceKm(p, capital) is > 100 and < 500);
        s.Claim(0, s.AddRegiment(0, near.Id, BattalionType.Scouts).Id);
        Assert.False(s.Rebase(0, fighters.Id, near.Id).Ok); // no airfield there
        near.AddBuilding(BuildingType.Airfield);
        Assert.True(s.Rebase(0, fighters.Id, near.Id).Ok);

        var far = _map.Provinces.First(p => p.IsClaimable && !p.IsOwned && _map.DistanceKm(p, near) > 2 * fighters.Info.RangeKm + 100);
        s.Claim(0, s.AddRegiment(0, far.Id, BattalionType.Scouts).Id);
        far.AddBuilding(BuildingType.Airfield);
        Assert.StartsWith("Está a más de", s.Rebase(0, fighters.Id, far.Id).Message);

        var sea = _map.Provinces.Where(p => p.IsWater).MinBy(p => _map.DistanceKm(p, capital))!;
        s.Human.Learn(Tech.NavalEngineering);
        var fleet = s.AddFleet(0, sea.Id, BattalionType.AircraftCarrier);
        Assert.Equal(MilitaryRules.FlightsPerCarrier, s.CarrierRoom(fleet));
        Assert.StartsWith("Solo cazas", s.Rebase(0, bombers.Id, 0, fleet.Id).Message);
        fighters.BaseProvinceId = sea.Neighbors.First(n => !_map.Provinces[n].IsWater); // flown to the coast first
        Assert.True(s.Rebase(0, fighters.Id, 0, fleet.Id).Ok);
        Assert.Equal(sea.Id, s.BaseOf(fighters).Id);
    }

    [Fact]
    public void AUnitThatLosesItsBaseFliesToAnotherOrIsLost()
    {
        var (s, capital) = WithAirfield();
        var unit = s.AddAirUnit(0, capital.Id, BattalionType.CloseSupport);
        var other = _map.Provinces.First(p => p.IsClaimable && !p.IsOwned && _map.DistanceKm(p, capital) > 300);
        s.Claim(0, s.AddRegiment(0, other.Id, BattalionType.Scouts).Id);
        other.AddBuilding(BuildingType.Airfield);
        capital.RemoveBuilding(BuildingType.Airfield);
        ToMidnight(s);
        Assert.Equal(other.Id, unit.BaseProvinceId);

        other.RemoveBuilding(BuildingType.Airfield);
        ToMidnight(s);
        Assert.Empty(s.AirUnits);
    }

    [Fact]
    public void EscuadrillasAreRepairedAtTheirBaseAndDisbandingReturnsTheirPlanes()
    {
        var (s, capital) = WithAirfield();
        var unit = s.AddAirUnit(0, capital.Id, BattalionType.Fighters, 2);
        foreach (var f in unit.Flights)
        {
            f.Strength = f.Info.Men / 2.0;
            f.Organisation = 0;
        }
        double planes = s.Human.EquipmentOf(unit.Info);
        ToMidnight(s);
        Assert.True(unit.StrengthShare > 0.5);
        Assert.True(unit.OrganisationShare > 0);
        Assert.True(s.Human.EquipmentOf(unit.Info) < planes);

        planes = s.Human.EquipmentOf(unit.Info);
        double count = unit.PlaneCount;
        Assert.True(s.DisbandAirUnit(0, unit.Id).Ok);
        Assert.Empty(s.AirUnits);
        Assert.Equal(planes + count, s.Human.EquipmentOf(BattalionType.Fighters.First()), 6);
    }

    [Fact]
    public void ACarrierSunkTakesItsAircraftDown()
    {
        var (s, capital) = WithAirfield();
        s.Human.Learn(Tech.NavalEngineering);
        var sea = _map.Provinces.Where(p => p.IsWater).MinBy(p => _map.DistanceKm(p, capital))!;
        var fleet = s.AddFleet(0, sea.Id, BattalionType.AircraftCarrier);
        var unit = s.AddAirUnit(0, capital.Id, BattalionType.NavalBombers);
        unit.BaseProvinceId = sea.Neighbors.First(n => !_map.Provinces[n].IsWater); // flown to the coast first
        Assert.True(s.Rebase(0, unit.Id, 0, fleet.Id).Ok);
        foreach (var ship in fleet.Ships) ship.Strength = 0;
        s.AddFleet(1, sea.Id, BattalionType.LineShip);
        s.DeclareWar(0, 1);
        for (int h = 0; h < 48 && s.UnitById(fleet.Id) != null; h++) s.Step();
        Assert.Null(s.UnitById(fleet.Id));
        Assert.Empty(s.AirUnits);
    }

    [Fact]
    public void AirUnitsHeadquartersAndDamageAreSaved()
    {
        var (s, capital) = WithAirfield();
        var unit = s.AddAirUnit(0, capital.Id, BattalionType.Bombers, 3);
        unit.Flights[0].Strength = 20;
        unit.Mission = AirMission.StrategicBombing;
        unit.TargetProvinceId = capital.Neighbors[0];
        s.RaiseAirHeadquarters(0, capital.Id, 1);
        s.AttachAirUnit(0, unit.Id, s.AirHeadquarters[0].Id);
        capital.SetDamage(BuildingType.Airfield, 0.4);
        var loaded = GameSession.Load(_map, s.ToSave("test"));
        var again = Assert.Single(loaded.AirUnits);
        Assert.Equal((unit.Id, unit.Name, unit.BaseProvinceId, unit.Mission, unit.TargetProvinceId, unit.CommanderId, 3, 20.0),
            (again.Id, again.Name, again.BaseProvinceId, again.Mission, again.TargetProvinceId, again.CommanderId, again.Flights.Count, again.Flights[0].Strength));
        Assert.Equal(s.AirHeadquarters[0].Officer!.Name, loaded.AirHeadquarters.Single().Officer!.Name);
        Assert.Equal(0.4, loaded.Map.Provinces[capital.Id].DamageOf(BuildingType.Airfield), 6);
        Assert.Equal("1.ª Escuadrilla de cazas", loaded.AddAirUnit(0, capital.Id, BattalionType.Fighters).Name);
    }

    [Fact]
    public void BomberRegimentsOfOldSavesBecomeEscuadrillas()
    {
        var (s, capital) = WithAirfield();
        capital.RemoveBuilding(BuildingType.Airfield);
        var mixed = s.AddRegiment(0, capital.Id, BattalionType.LightInfantry, BattalionType.Bombers);
        var bombers = s.AddRegiment(0, capital.Id, BattalionType.Bombers);
        var loaded = GameSession.Load(_map, s.ToSave("test"));
        Assert.True(loaded.Map.Provinces[capital.Id].Has(BuildingType.Airfield)); // the capital gets one
        Assert.Equal(2, loaded.AirUnits.Count);
        Assert.All(loaded.AirUnits, u => Assert.Equal((capital.Id, AirEchelon.Flight), (u.BaseProvinceId!.Value, u.Size)));
        Assert.Equal([BattalionType.LightInfantry], loaded.UnitById(mixed.Id)!.Battalions.Select(b => b.Type));
        Assert.Null(loaded.UnitById(bombers.Id));
    }

    [Fact]
    public void WingsOfEarlierSavesBecomeEscuadrones()
    {
        var (s, capital) = WithAirfield();
        var save = s.ToSave("test") with
        {
            Wings = [new WingSave(0, 0, new BattalionSave(BattalionType.Fighters, 10, 40), 1, capital.Id, null, AirMission.None, null)],
        };
        var loaded = GameSession.Load(_map, save);
        var unit = Assert.Single(loaded.AirUnits);
        Assert.Equal((AirEchelon.Squadron, 12.0), (unit.Size, unit.PlaneCount));
    }

    [Fact]
    public void BombedBuildingsProduceLessAndAreRepaired()
    {
        var (s, capital) = WithAirfield();
        capital.AddBuilding(BuildingType.Market);
        double taxes = s.BonusesOf(capital).Taxes;
        capital.SetDamage(BuildingType.Market, 0.5);
        Assert.Equal(taxes - BuildingType.Market.Info().Effects.Taxes / 2, s.BonusesOf(capital).Taxes, 6);
        capital.AddBuilding(BuildingType.Workshop);
        capital.SetDamage(BuildingType.Workshop, 0.3);
        Assert.Equal(0.3, capital.WorkshopDamage, 6);
        capital.RemoveBuilding(BuildingType.Market);
        Assert.Equal(0, capital.DamageOf(BuildingType.Market));
    }

    [Fact]
    public void TheAirForceTabListsUnitsAndHeadquartersAndFormsEscuadrillas()
    {
        var (s, capital) = WithAirfield();
        s.AddAirUnit(0, capital.Id, BattalionType.Fighters);
        var game = new Conquer.Presentation.GameController(s);
        game.Nation.Visible = true;
        game.Nation.Tab = Conquer.Presentation.NationTab.AirForce;
        var page = Assert.IsType<Conquer.Presentation.TablesPage>(game.Nation.Page());
        Assert.StartsWith("Unidades aéreas", page.Tables[0].Title);
        Assert.Single(page.Tables[0].Table.Rows);
        var buttons = page.Tables.SelectMany(t => t.Table.Rows).SelectMany(r => r).OfType<Conquer.Presentation.ButtonsCell>().SelectMany(c => c.Buttons).ToList();
        buttons.First(b => b.Text == "Formar" && b.Enabled && b.Tooltip!.Contains("general")).Press();
        Assert.Single(s.AirHeadquarters);
        var form = Assert.IsType<Conquer.Presentation.TablesPage>(game.Nation.Page()).Tables[3].Table.Rows.SelectMany(r => r)
            .OfType<Conquer.Presentation.ButtonsCell>().SelectMany(c => c.Buttons).First(b => b.Text == "Formar" && b.Enabled);
        form.Press();
        Assert.Single(capital.Training);
    }
}
