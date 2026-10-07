using Conquer.Game.Buildings;
using Conquer.Game.Economy;
using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Science;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Tests;

/// <summary>Air missions: superiority, close support, bombing, naval strikes, paratroopers and the fights in the air.</summary>
[Collection("World")]
public class AirWarTests(WorldFixture world)
{
    private readonly WorldMap _map = world.Map;

    /// <summary>
    /// Two nations at war, both with aviation and an airfield in their capital: the human's in A, the enemy's in B,
    /// a grassland neighbour. No computer rivals.
    /// </summary>
    private (GameSession S, Province A, Province B) War()
    {
        var s = GameSession.Create(_map, 2, seed: 7, computerRivals: false);
        var (a, b) = _map.Provinces.Where(p => p.Biome == Biome.Grassland && p.Neighbors.Length > 3)
            .SelectMany(p => _map.Provinces.Where(q => q.Biome == Biome.Grassland && q.Neighbors.Length > 3 && _map.DistanceKm(p, q) is > 150 and < 300).Take(1).Select(q => (A: p, B: q)))
            .First();
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, a.Id, 300).Id);
        s.FoundCity(1, s.AddUnit(1, UnitType.Settlers, b.Id, 300).Id);
        foreach (var player in s.Players)
        {
            foreach (var t in new[] { Tech.Combustion, Tech.Electricity, Tech.Aviation }) player.Learn(t);
            foreach (var r in Resources.All) player.Stockpile[r] = 5000;
            player.Arm();
            player.Manpower = 5000;
        }
        a.AddBuilding(BuildingType.Airfield);
        b.AddBuilding(BuildingType.Airfield);
        a.Population = b.Population = 5000;
        Assert.Equal((0, 1), (a.OwnerId, b.OwnerId));
        Assert.True(s.DeclareWar(0, 1).Ok);
        return (s, a, b);
    }

    private static void ToMidnight(GameSession s)
    {
        do s.Step(); while (s.Date.Hour != 0);
    }

    [Fact]
    public void AirUnitsFlyOnlyTheirOwnMissionsWithinRange()
    {
        var (s, a, b) = War();
        var fighters = s.AddAirUnit(0, a.Id, BattalionType.Fighters);
        Assert.StartsWith("Los cazas no pueden", s.SetAirMission(0, fighters.Id, AirMission.StrategicBombing, b.Id).Message);
        var far = _map.Provinces.First(p => !p.IsWater && _map.DistanceKm(p, a) > fighters.Info.RangeKm + 50);
        Assert.StartsWith("Está fuera de su alcance", s.SetAirMission(0, fighters.Id, AirMission.AirSuperiority, far.Id).Message);
        Assert.True(s.SetAirMission(0, fighters.Id, AirMission.AirSuperiority, b.Id).Ok);
        Assert.True(s.IsFlying(fighters));
        Assert.True(s.SetAirMission(0, fighters.Id, AirMission.None).Ok);
        Assert.Null(fighters.TargetProvinceId);
    }

    [Fact]
    public void FightersRuleTheSkyAndTroopsFightBetterUnderIt()
    {
        var (s, a, b) = War();
        Assert.Null(s.AirSuperiority(0, b));
        var mine = s.AddAirUnit(0, a.Id, BattalionType.Fighters);
        s.SetAirMission(0, mine.Id, AirMission.AirSuperiority, b.Id);
        Assert.Equal(1, s.AirSuperiority(0, b));
        Assert.True(s.EnemyRulesTheAir(1, b));
        Assert.Equal(1 + MilitaryRules.AirSuperiorityBonus, s.AirSuperiorityMultiplier(0, b), 6);
        Assert.Equal(1 - MilitaryRules.AirSuperiorityBonus, s.AirSuperiorityMultiplier(1, b), 6);

        var theirs = s.AddAirUnit(1, b.Id, BattalionType.Fighters);
        s.SetAirMission(1, theirs.Id, AirMission.AirSuperiority, b.Id);
        Assert.Equal(0.5, s.AirSuperiority(0, b)!.Value, 6);
        Assert.False(s.EnemyRulesTheAir(1, b));

        // A day of fighting in the air costs both sides planes.
        ToMidnight(s);
        Assert.True(mine.PlaneCount < MilitaryRules.PlanesPerFlight);
        Assert.True(theirs.PlaneCount < MilitaryRules.PlanesPerFlight);
        Assert.True(s.PlanesLostLastDay(0) > 0 && s.PlanesDownedLastDay(0) > 0);
    }

    [Fact]
    public void AttackAircraftAddFireToBattlesAndDoLessUnderAnEnemySky()
    {
        var (s, a, b) = War();
        var attack = s.AddAirUnit(0, a.Id, BattalionType.CloseSupport);
        s.SetAirMission(0, attack.Id, AirMission.CloseSupport, b.Id);
        double fire = s.CloseAirSupport(0, b);
        Assert.Equal(attack.Info.Attack * 1, fire, 6);
        var fighters = s.AddAirUnit(1, b.Id, BattalionType.Fighters);
        s.SetAirMission(1, fighters.Id, AirMission.AirSuperiority, b.Id);
        Assert.Equal(fire * MilitaryRules.UnescortedBomberEffect, s.CloseAirSupport(0, b), 6);
    }

    [Fact]
    public void AntiAirShootsDownTheAircraftOverItsProvince()
    {
        var (s, a, b) = War();
        var attack = s.AddAirUnit(0, a.Id, BattalionType.CloseSupport);
        s.SetAirMission(0, attack.Id, AirMission.CloseSupport, b.Id);
        s.AddRegiment(1, b.Id, BattalionType.AntiAir, BattalionType.AntiAir);
        ToMidnight(s);
        Assert.True(attack.PlaneCount < MilitaryRules.PlanesPerFlight);
    }

    [Fact]
    public void BombersHurtTheEnemysProvinceAndDamageItsBuildings()
    {
        var (s, a, b) = War();
        var bombers = s.AddAirUnit(0, a.Id, BattalionType.Bombers, 3);
        s.SetAirMission(0, bombers.Id, AirMission.StrategicBombing, b.Id);
        foreach (var building in b.Buildings.ToList()) b.RemoveBuilding(building);
        b.AddBuilding(BuildingType.Workshop);
        b.Mood = 80;
        ToMidnight(s);
        Assert.True(b.Mood < 80);
        Assert.True(b.WorkshopDamage > 0);
        Assert.Equal(0, a.WorkshopDamage);

        // Left alone, the damage is repaired a little each day.
        double damage = b.WorkshopDamage;
        s.SetAirMission(0, bombers.Id, AirMission.None);
        ToMidnight(s);
        Assert.Equal(damage - MilitaryRules.BuildingRepairPerDay, b.WorkshopDamage, 6);
    }

    [Fact]
    public void NavalAircraftStrikeEnemyFleets()
    {
        var (s, a, _) = War();
        var sea = _map.Provinces.Where(p => p.IsWater).MinBy(p => _map.DistanceKm(p, a))!;
        var naval = s.AddAirUnit(0, a.Id, BattalionType.NavalBombers);
        naval.BaseProvinceId = sea.Neighbors.First(n => !_map.Provinces[n].IsWater); // flown to the coast
        _map.Provinces[naval.BaseProvinceId.Value].AddBuilding(BuildingType.Airfield);
        Assert.True(s.SetAirMission(0, naval.Id, AirMission.NavalStrike, sea.Id).Ok);
        var fleet = s.AddFleet(1, sea.Id, BattalionType.LineShip);
        double crew = fleet.Citizens;
        // Straight to the air phase: the airfield on the coast is not the human's, so the wing would fly home overnight.
        s.DailyAir();
        Assert.True(fleet.Citizens < crew || s.UnitById(fleet.Id) == null);
    }

    [Fact]
    public void ParatroopersAreDroppedFromTheirAirfieldWithinTheTransportsRange()
    {
        var (s, a, b) = War();
        var paras = s.AddRegiment(0, a.Id, BattalionType.Paratroopers);
        Assert.StartsWith("Hacen falta aviones de transporte", s.CanParadrop(paras, b).Message);
        s.AddAirUnit(0, a.Id, BattalionType.AirTransports);
        var guard = s.AddRegiment(1, b.Id, BattalionType.LightInfantry);
        Assert.StartsWith("Hay tropas enemigas", s.CanParadrop(paras, b).Message);
        s.Disband(1, guard.Id);

        Assert.True(s.Paradrop(0, paras.Id, b.Id).Ok);
        Assert.Equal(b.Id, paras.ProvinceId);
        Assert.Equal(0, b.ControllerId); // the enemy's empty capital is taken
        Assert.True(paras.Battalions[0].OrganisationShare <= 0.5);
        Assert.StartsWith("Solo se lanzan", s.CanParadrop(s.AddRegiment(0, a.Id, BattalionType.LightInfantry), b).Message);
    }

    [Fact]
    public void UnderAnEnemySkyTroopsMarchSlowerAndGetLessSupply()
    {
        var (s, a, b) = War();
        var next = a.Neighbors.Select(n => _map.Provinces[n]).First(p => !p.IsWater && p.ControllerId != 1);
        double Step()
        {
            var unit = s.AddRegiment(0, a.Id, BattalionType.LightInfantry);
            Assert.True(s.MoveUnit(0, unit.Id, next.Id).Ok);
            return unit.HoursToNext;
        }
        double clear = Step();
        var fighters = s.AddAirUnit(1, b.Id, BattalionType.Fighters);
        s.SetAirMission(1, fighters.Id, AirMission.AirSuperiority, next.Id);
        Assert.True(s.EnemyRulesTheAir(0, next));
        Assert.Equal(clear * MilitaryRules.UnderEnemyAirSlowdown, Step(), 3);
    }

    [Fact]
    public void MissionsAreSaved()
    {
        var (s, a, b) = War();
        var wing = s.AddAirUnit(0, a.Id, BattalionType.Fighters);
        s.SetAirMission(0, wing.Id, AirMission.AirSuperiority, b.Id);
        var loaded = GameSession.Load(_map, s.ToSave("test"));
        Assert.Equal(1, loaded.AirSuperiority(0, loaded.Map.Provinces[b.Id]));
    }

    [Fact]
    public void TheComputerFliesItsWingsAtWar()
    {
        var (s, a, b) = War();
        var fighters = s.AddAirUnit(1, b.Id, BattalionType.Fighters);
        var bombers = s.AddAirUnit(1, b.Id, BattalionType.Bombers);
        var ai = new Conquer.Game.AI.AiPlayer(s, s.Players[1], 3);
        ai.Think(dailyDecisions: true);
        Assert.Equal((AirMission.StrategicBombing, a.Id), (bombers.Mission, bombers.TargetProvinceId!.Value));
        Assert.Equal(AirMission.AirSuperiority, fighters.Mission);
    }
}
