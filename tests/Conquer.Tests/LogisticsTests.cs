using Conquer.Game.Entities;
using Conquer.Game.Buildings;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Tests;

[Collection("World")]
public class LogisticsTests(WorldFixture world)
{
    private readonly WorldMap _map = world.Map;

    /// <summary>A capital on grassland with plenty of people, plenty of equipment and no computer rivals.</summary>
    private (GameSession S, Province Capital) Game()
    {
        var s = GameSession.Create(_map, 2, seed: 7, computerRivals: false);
        s.Human.Arm();
        var capital = _map.Provinces.First(p => p.Biome == Biome.Grassland && p.Neighbors.Length > 3 && !p.IsOwned);
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, capital.Id, 300).Id);
        capital.Population = 5000;
        s.Human.Manpower = 5000;
        return (s, capital);
    }

    /// <summary>A province in supply, two to six days' march from the capital over free land.</summary>
    private static Province FarAway(GameSession s, Province capital)
    {
        var (hours, _) = s.Pathfinder.FromSources([capital.Id], canEnter: id => !s.Map.Provinces[id].IsWater && !s.Map.Provinces[id].IsOwned || id == capital.Id);
        return s.Map.Provinces.First(p => hours[p.Id] is > 48 and < 144 && s.IsSupplied(0, p.Id));
    }

    /// <summary>Runs until the next day's shipments have left (midnight).</summary>
    private static void ToMidnight(GameSession s)
    {
        do s.Step(); while (s.Date.Hour != 0);
    }

    [Fact]
    public void ReinforcementsTakeTheWayFromTheCapital()
    {
        var (s, capital) = Game();
        var far = FarAway(s, capital);
        var unit = s.AddRegiment(0, far.Id, BattalionType.LightInfantry);
        var corps = s.AddHeadquarters(0, far.Id, 1);
        Assert.True(s.Attach(0, unit.Id, corps.Id).Ok);
        var b = unit.Battalions[0];
        b.Strength = 50;
        double stock = s.Human.EquipmentOf(b.Info);

        ToMidnight(s);
        var shipment = Assert.Single(s.ShipmentsTo(unit));
        double men = b.Info.Men * MilitaryRules.ReinforcementRate;
        Assert.Equal(men, shipment.Men, 6);
        Assert.Equal(stock - men / b.Info.Men * b.Info.Pieces, s.Human.EquipmentOf(b.Info), 6); // out of the stockpile already
        Assert.True(shipment.ArriveHours - s.Date.Hours > 24); // days on the road

        while (s.Date.Hours < shipment.ArriveHours - 1) s.Step();
        double before = b.Strength; // not there yet (the cold may have taken some on the way)
        Assert.True(before <= 50);
        s.Step();
        Assert.InRange(b.Strength, before + men - 1, before + men + 1e-6); // less any cold at midnight
    }

    [Fact]
    public void HighPriorityHeadquartersAreServedFirstAndUnitsWithoutOneLastAndSlower()
    {
        var (s, capital) = Game();
        var far = FarAway(s, capital);
        var units = Enumerable.Range(0, 3).Select(_ => s.AddRegiment(0, far.Id, BattalionType.LightInfantry)).ToList();
        foreach (var u in units) u.Battalions[0].Strength = 50;
        var low = s.AddHeadquarters(0, far.Id, 1);
        var high = s.AddHeadquarters(0, far.Id, 1);
        Assert.True(s.Attach(0, units[0].Id, low.Id).Ok);
        Assert.True(s.Attach(0, units[1].Id, high.Id).Ok);
        Assert.True(s.SetSupplyPriority(0, low.Id, SupplyPriority.Low).Ok);
        Assert.True(s.SetSupplyPriority(0, high.Id, SupplyPriority.High).Ok);
        Assert.False(s.SetSupplyPriority(0, units[2].Id, SupplyPriority.High).Ok); // only HQs have one

        // Weapons for one day's recruits of a single battalion.
        var info = units[0].Battalions[0].Info;
        s.Human.Equipment[info.SupplyKey] = info.Pieces * MilitaryRules.ReinforcementRate;
        ToMidnight(s);
        Assert.Single(s.ShipmentsTo(units[1]));
        Assert.Empty(s.ShipmentsTo(units[0]));
        Assert.Empty(s.ShipmentsTo(units[2]));

        // With plenty, everyone gets theirs; the unit without an HQ, twice as late.
        s.Human.Arm();
        ToMidnight(s);
        long Trip(Unit u) => s.ShipmentsTo(u).Max(x => x.ArriveHours) - s.Date.Hours;
        Assert.Equal(Trip(units[0]), Trip(units[1]));
        Assert.InRange(Trip(units[2]), 2 * Trip(units[0]) - 1, 2 * Trip(units[0]));
    }

    [Fact]
    public void AnHeadquartersCutOffLeavesItsUnitsWithoutShipments()
    {
        var (s, capital) = Game();
        // The open sea nearest the capital that supply does not reach, and the land in supply nearest to it.
        var sea = _map.Provinces.Where(p => p.IsWater && !s.IsSupplied(0, p.Id)).MinBy(p => _map.DistanceKm(p, capital))!;
        var coast = _map.Provinces.Where(p => !p.IsWater && s.IsSupplied(0, p.Id)).MinBy(p => _map.DistanceKm(p, sea))!;
        var unit = s.AddRegiment(0, coast.Id, BattalionType.LightInfantry);
        unit.Battalions[0].Strength = 50;
        var corps = s.AddHeadquarters(0, coast.Id, 1);
        Assert.True(s.Attach(0, unit.Id, corps.Id).Ok);
        Assert.Equal(((int)SupplyPriority.Normal, 1.0), s.ShipmentTerms(unit));

        // Sent off over the sea, the HQ is out of supply.
        corps.ProvinceId = sea.Id;
        Assert.True(s.InCommandRange(unit));
        Assert.Null(s.ShipmentTerms(unit));
        Assert.Equal("Su cuartel general está aislado.", s.NoShipmentsReason(unit));
        ToMidnight(s);
        Assert.Empty(s.ShipmentsTo(unit));
    }

    [Fact]
    public void AShipmentWhoseUnitIsCutOffGoesBack()
    {
        var (s, capital) = Game();
        var far = FarAway(s, capital);
        var unit = s.AddRegiment(0, far.Id, BattalionType.LightInfantry);
        unit.Battalions[0].Strength = 50;
        var info = unit.Battalions[0].Info;
        ToMidnight(s);
        var shipment = Assert.Single(s.ShipmentsTo(unit));
        double stock = s.Human.EquipmentOf(info), manpower = s.Human.Manpower;

        // Before it gets there, the unit is carried off beyond any supply.
        unit.ProvinceId = _map.Provinces.Where(p => !p.IsWater).MaxBy(p => _map.DistanceKm(p, capital))!.Id;
        Assert.Equal("Está sin suministro.", s.NoShipmentsReason(unit));
        while (s.Date.Hours < shipment.ArriveHours) s.Step();
        Assert.Empty(s.ShipmentsTo(unit));
        Assert.True(unit.Battalions[0].Strength <= 50); // nothing came: it only lost men out of supply
        Assert.True(s.Human.EquipmentOf(info) >= stock + shipment.Pieces[info.SupplyKey] - 1e-6);
        Assert.True(s.Human.Manpower >= manpower + shipment.Men - 1e-6);
    }

    [Fact]
    public void FightingSpendsAmmunitionAndWithoutItTroopsFightAtHalfStrength()
    {
        var (s, capital) = Game();
        var unit = s.AddRegiment(0, capital.Id, BattalionType.LightInfantry, BattalionType.LightInfantry, BattalionType.Medics);
        // A day of fighting for the two battalions; medics do not fight.
        Assert.Equal(2 * 100 / 100.0 * MilitaryRules.AmmoPerHundredMenHour * MilitaryRules.AmmoHours, GameSession.AmmoCapacity(unit), 6);
        Assert.Equal(1, GameSession.AmmoEfficiency(unit));
        double full = GameSession.ExpectedFire(s.Engage([unit], capital, attacking: true));

        unit.AmmoSpent = GameSession.AmmoCapacity(unit);
        Assert.Equal(MilitaryRules.OutOfAmmoEfficiency, GameSession.AmmoEfficiency(unit), 6);
        Assert.Equal(full * MilitaryRules.OutOfAmmoEfficiency, GameSession.ExpectedFire(s.Engage([unit], capital, attacking: true)), 6);

        // The capital sends the suministros back.
        double stock = s.Human.EquipmentOf(Supplies.General.Key);
        double spent = unit.AmmoSpent;
        ToMidnight(s);
        s.Step();
        Assert.Equal(0, unit.AmmoSpent, 6);
        Assert.Equal(stock - spent, s.Human.EquipmentOf(Supplies.General.Key), 6);
    }

    [Fact]
    public void BattlesUseUpAmmunition()
    {
        var s = GameSession.Create(_map, 2, seed: 7, computerRivals: false);
        s.Human.Arm();
        var (a, b) = _map.Provinces.Where(p => p.Biome == Biome.Grassland && p.Neighbors.Length > 3)
            .SelectMany(p => p.Neighbors.Select(n => (A: p, B: _map.Provinces[n]))).First(t => t.B.Biome == Biome.Grassland && !t.B.IsWater);
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, a.Id, 300).Id);
        var claimer = s.AddRegiment(1, b.Id, BattalionType.Scouts);
        s.Claim(1, claimer.Id);
        s.Disband(1, claimer.Id);
        var attacker = s.AddRegiment(0, a.Id, BattalionType.LightInfantry, BattalionType.LightInfantry);
        var defender = s.AddRegiment(1, b.Id, BattalionType.LightInfantry, BattalionType.LightInfantry);
        Assert.True(s.DeclareWar(0, 1).Ok);
        Assert.True(s.MoveUnit(0, attacker.Id, b.Id).Ok);
        for (int i = 0; i < 48 && attacker.AttackingProvinceId == null; i++) s.Step();
        s.Step();
        Assert.True(attacker.AmmoSpent > 0);
        Assert.True(defender.AmmoSpent > 0);
    }

    [Fact]
    public void ShipmentsAmmunitionAndPrioritiesAreSaved()
    {
        var (s, capital) = Game();
        var far = FarAway(s, capital);
        var unit = s.AddRegiment(0, far.Id, BattalionType.LightInfantry);
        unit.Battalions[0].Strength = 50;
        unit.AmmoSpent = 2;
        var corps = s.AddHeadquarters(0, far.Id, 1);
        s.SetSupplyPriority(0, corps.Id, SupplyPriority.High);
        ToMidnight(s);
        var shipment = Assert.Single(s.ShipmentsTo(unit));

        var loaded = GameSession.Load(_map, s.ToSave("test"));
        var again = Assert.Single(loaded.Shipments);
        Assert.Equal((shipment.UnitId, shipment.Men, shipment.Ammo, shipment.ArriveHours), (again.UnitId, again.Men, again.Ammo, again.ArriveHours));
        Assert.Equal(shipment.Pieces, again.Pieces);
        Assert.Equal(SupplyPriority.High, loaded.UnitById(corps.Id)!.SupplyPriority);
        Assert.Equal(unit.AmmoSpent, loaded.UnitById(unit.Id)!.AmmoSpent, 6);
    }
}
