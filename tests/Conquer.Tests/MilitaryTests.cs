using Conquer.Game.Buildings;
using Conquer.Game.Economy;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Science;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Tests;

[Collection("World")]
public class MilitaryTests(WorldFixture world)
{
    private readonly WorldMap _map = world.Map;

    /// <summary>A grassland province and a grassland neighbour, both with several neighbours.</summary>
    private (Province A, Province B) Pair() =>
        _map.Provinces
            .Where(p => p.Biome == Biome.Grassland && p.Neighbors.Length > 3)
            .SelectMany(p => p.Neighbors.Select(n => (A: p, B: _map.Provinces[n])))
            .First(t => t.B.Biome == Biome.Grassland && t.B.Neighbors.Length > 3);

    /// <summary>
    /// Player 0 (human) has its capital in A, with barracks; player 1 owns the neighbouring B. No computer rivals, so
    /// only the test moves units.
    /// </summary>
    private (GameSession S, Province A, Province B) TwoNations()
    {
        var s = GameSession.Create(_map, 2, seed: 7, computerRivals: false);
        s.Human.Arm();
        var (a, b) = Pair();
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, a.Id, 300).Id);
        a.AddBuilding(BuildingType.Barracks);
        var claimer = s.AddRegiment(1, b.Id, BattalionType.Scouts);
        s.Claim(1, claimer.Id);
        s.Disband(1, claimer.Id);
        return (s, a, b);
    }

    private static void RunHours(GameSession s, int hours)
    {
        for (int i = 0; i < hours; i++) s.Step();
    }

    private static void RunUntil(GameSession s, Func<bool> done, int maxHours)
    {
        for (int i = 0; i < maxHours && !done(); i++) s.Step();
    }

    [Fact]
    public void UnitsShowTheNatoSymbolOfTheirSizeAndArm()
    {
        Assert.Equal("III", Formations.CombatEchelon(Echelon.Regiment));
        Assert.Equal("X", Formations.CombatEchelon(Echelon.Brigade));
        Assert.Equal("XX", Formations.CombatEchelon(Echelon.Division));
        Assert.Equal(["XXX", "XXXX", "XXXXX"], CommandLevels.All.Select(l => l.Symbol));

        Assert.Equal(UnitFunction.Infantry, Formations.Function([BattalionType.LightInfantry, BattalionType.RangedInfantry]));
        Assert.Equal(UnitFunction.Cavalry, Formations.Function([BattalionType.Cavalry, BattalionType.Cavalry, BattalionType.LightInfantry]));
        Assert.Equal(UnitFunction.Cavalry, Formations.Function([BattalionType.Scouts]));
        Assert.Equal(UnitFunction.Armour, Formations.Function([BattalionType.Armour]));
        Assert.Equal(UnitFunction.Mechanised, Formations.Function([BattalionType.Armour, BattalionType.Cavalry]));
        Assert.Equal(UnitFunction.Mountain, Formations.Function([BattalionType.MountainInfantry]));
        Assert.Equal(UnitFunction.Airborne, Formations.Function([BattalionType.Paratroopers]));
        Assert.Equal(UnitFunction.AntiAir, Formations.Function([BattalionType.AntiAir]));
        Assert.Equal(UnitFunction.Medical, Formations.Function([BattalionType.Medics]));
        // Horse-drawn chariots are cavalry; tanks are armour.
        Assert.Equal(UnitFunction.Cavalry, Formations.Function([new Battalion(BattalionType.Armour, 0)]));
        Assert.Equal(UnitFunction.Armour, Formations.Function([new Battalion(BattalionType.Armour, 1)]));
        Assert.Equal(UnitFunction.Artillery, Formations.Function([BattalionType.Artillery, BattalionType.Artillery, BattalionType.RangedInfantry]));
        Assert.Equal(UnitFunction.Engineers, Formations.Function([BattalionType.Engineers]));
        Assert.Equal(UnitFunction.Air, Formations.Function([BattalionType.Bombers]));
        // A tie goes to the front line.
        Assert.Equal(UnitFunction.Infantry, Formations.Function([BattalionType.Artillery, BattalionType.LightInfantry]));
    }

    [Fact]
    public void BattalionsTakeMenResourcesAndDaysToTrain()
    {
        var (s, a, _) = TwoNations();
        var city = s.CityIn(a)!;
        a.Population = 1000;
        s.Human.Stockpile[ResourceType.Wood] = s.Human.Stockpile[ResourceType.Gold] = 500;
        var info = BattalionType.LightInfantry.First();

        Assert.True(s.Train(0, city.ProvinceId, BattalionType.LightInfantry).Ok);
        Assert.Equal(1000 - info.Men, a.Population);
        Assert.Equal(500, s.Human.Stockpile[ResourceType.Wood]); // the wood went into the weapons, made apart
        Assert.Equal(500 - 15, s.Human.Stockpile[ResourceType.Gold]);
        Assert.Equal(100_000 - info.Pieces, s.Human.EquipmentOf(info));
        Assert.Single(a.Training);

        RunHours(s, 24 * info.TrainingDays);
        Assert.Empty(a.Training);
        var regiment = Assert.Single(s.Units, u => u.IsMilitary && u.OwnerId == 0);
        Assert.Equal(a.Id, regiment.ProvinceId);
        Assert.Equal(info.Men, regiment.Citizens);
        Assert.Equal("1.er Regimiento", regiment.Name);
    }

    [Fact]
    public void AdvancedBattalionsNeedTheirAdvance()
    {
        var (s, a, _) = TwoNations();
        var city = s.CityIn(a)!;
        a.Population = 1000;
        s.Human.Stockpile[ResourceType.Wood] = s.Human.Stockpile[ResourceType.Gold] = 500;
        s.Human.Stockpile[ResourceType.Copper] = 100;

        Assert.True(s.Train(0, city.ProvinceId, BattalionType.LightInfantry).Ok); // warriors need nothing
        Assert.False(s.Train(0, city.ProvinceId, BattalionType.RangedInfantry).Ok);
        s.Human.Learn(Tech.Archery);
        Assert.True(s.Train(0, city.ProvinceId, BattalionType.RangedInfantry).Ok);

        Assert.False(s.Train(0, city.ProvinceId, BattalionType.Cavalry).Ok);
        s.Human.Learn(Tech.HorsebackRiding);
        Assert.True(s.Train(0, city.ProvinceId, BattalionType.Cavalry).Ok);
        Assert.False(s.Train(0, city.ProvinceId, BattalionType.Armour).Ok); // needs the wheel

        s.Human.Learn(Tech.TheWheel);
        Assert.True(s.Train(0, city.ProvinceId, BattalionType.Armour).Ok);
        s.Human.Techs.Remove(Tech.TheWheel);
        var noWheel = s.CanTrain(a, BattalionType.Armour);
        Assert.False(noWheel.Ok);
        Assert.Contains("la rueda", noWheel.Message);
    }

    [Fact]
    public void ClassicalBattalionsNeedTheirAdvances()
    {
        var (s, a, _) = TwoNations();
        var city = s.CityIn(a)!;
        a.Population = 2000;
        foreach (var r in new[] { ResourceType.Wood, ResourceType.Gold, ResourceType.Copper, ResourceType.Iron }) s.Human.Stockpile[r] = 1000;

        Assert.False(s.Train(0, city.ProvinceId, BattalionType.HeavyInfantry).Ok);
        s.Human.Learn(Tech.MilitaryTactics);
        Assert.True(s.Train(0, city.ProvinceId, BattalionType.HeavyInfantry).Ok);

        a.AddBuilding(BuildingType.Workshop);
        Assert.False(s.Train(0, city.ProvinceId, BattalionType.Artillery).Ok);
        s.Human.Learn(Tech.SiegeEngines);
        Assert.True(s.Train(0, city.ProvinceId, BattalionType.Artillery).Ok);

        Assert.False(s.Train(0, city.ProvinceId, BattalionType.Cavalry).Ok);
        s.Human.Learn(Tech.HeavyCavalry);
        Assert.True(s.Train(0, city.ProvinceId, BattalionType.Cavalry).Ok);
    }

    [Fact]
    public void DivisionsMergeSplitAndMarchAtTheSlowestPace()
    {
        var (s, a, _) = TwoNations();
        var riders = s.AddRegiment(0, a.Id, BattalionType.Cavalry);
        var foot = s.AddRegiment(0, a.Id, BattalionType.LightInfantry, BattalionType.RangedInfantry);
        Assert.Equal(1.8, riders.Speed);

        Assert.True(s.Merge(0, riders.Id, foot.Id).Ok);
        Assert.Null(s.UnitById(foot.Id));
        Assert.Equal(3, riders.Battalions.Count);
        Assert.Equal(1, riders.Speed);

        var more = s.AddRegiment(0, a.Id, [.. Enumerable.Repeat(BattalionType.LightInfantry, 10)]);
        Assert.False(s.Merge(0, riders.Id, more.Id).Ok); // thirteen battalions is too many

        Assert.True(s.Split(0, riders.Id, 0).Ok);
        Assert.Equal(2, riders.Battalions.Count);
        Assert.Contains(s.Units, u => u.IsMilitary && u.Battalions.Count == 1 && u.Battalions[0].Type == BattalionType.Cavalry);
    }

    [Fact]
    public void ArmiesOnlyEnterTheLandOfNationsAtWar()
    {
        var (s, a, b) = TwoNations();
        var regiment = s.AddRegiment(0, a.Id, BattalionType.LightInfantry);

        Assert.False(s.MoveUnit(0, regiment.Id, b.Id).Ok);
        var settlers = s.AddUnit(0, UnitType.Settlers, a.Id, 300);
        Assert.True(s.DeclareWar(0, 1).Ok);
        Assert.True(s.AtWar(1, 0));
        Assert.True(s.MoveUnit(0, regiment.Id, b.Id).Ok);
        Assert.False(s.MoveUnit(0, settlers.Id, b.Id).Ok); // only armies go into enemy land
    }

    [Fact]
    public void MarchingIntoUndefendedEnemyLandOccupiesIt()
    {
        var (s, a, b) = TwoNations();
        var regiment = s.AddRegiment(0, a.Id, BattalionType.LightInfantry);
        s.DeclareWar(0, 1);
        s.MoveUnit(0, regiment.Id, b.Id);

        RunUntil(s, () => regiment.ProvinceId == b.Id, 24 * 5);
        Assert.Equal(b.Id, regiment.ProvinceId);
        Assert.Equal(0, b.ControllerId);
        Assert.Equal(1, b.OwnerId);
        Assert.True(b.IsOccupied);
        Assert.Contains(s.MoodFactors(b), f => f.Points == MilitaryRules.OccupiedMood);
    }

    [Fact]
    public void AStrongAttackWinsTheProvince()
    {
        var (s, a, b) = TwoNations();
        var defender = s.AddRegiment(1, b.Id, BattalionType.LightInfantry);
        var attackers = Enumerable.Range(0, 3)
            .Select(_ => s.AddRegiment(0, a.Id, BattalionType.HeavyInfantry, BattalionType.HeavyInfantry, BattalionType.HeavyInfantry, BattalionType.HeavyInfantry))
            .ToList();
        s.DeclareWar(0, 1);
        foreach (var unit in attackers) s.MoveUnit(0, unit.Id, b.Id);

        RunUntil(s, () => s.BattleIn(b.Id) != null, 24 * 5);
        Assert.NotNull(s.BattleIn(b.Id));
        Assert.All(attackers, u => Assert.Equal(a.Id, u.ProvinceId)); // attackers wait at the border

        RunUntil(s, () => s.BattleIn(b.Id) == null, 24 * 10);
        Assert.Null(s.BattleIn(b.Id));
        Assert.All(attackers, u => Assert.Equal(b.Id, u.ProvinceId));
        Assert.Equal(0, b.ControllerId);
        Assert.True(s.UnitById(defender.Id) is null || s.UnitById(defender.Id)!.ProvinceId != b.Id);
    }

    [Fact]
    public void ScoutsAreCheapFastWeakAndClaimLand()
    {
        var (s, a, _) = TwoNations();
        var city = s.CityIn(a)!;
        a.Population = 1000;
        s.Human.Stockpile[ResourceType.Wood] = s.Human.Stockpile[ResourceType.Gold] = 500;
        var scouts = BattalionType.Scouts.First();
        var warriors = BattalionType.LightInfantry.First();
        Assert.Empty(scouts.Requires);
        Assert.True(scouts.Cost.Items.Sum(i => i.Amount) < warriors.Cost.Items.Sum(i => i.Amount) / 2);
        Assert.True(scouts.Speed > warriors.Speed);
        Assert.True(scouts.Attack < warriors.Attack && scouts.Defense < warriors.Defense);
        Assert.True(s.Train(0, city.ProvinceId, BattalionType.Scouts).Ok);

        var free = _map.Provinces.First(p => p.IsClaimable && !p.IsOwned);
        var unit = s.AddRegiment(0, free.Id, BattalionType.Scouts);
        Assert.True(s.Claim(0, unit.Id).Ok);
        Assert.Equal(0, free.OwnerId);
    }

    [Fact]
    public void EngineersNeedEngineeringAndBluntTheDefendersTerrain()
    {
        var (s, a, b) = TwoNations();
        var city = s.CityIn(a)!;
        a.Population = 1000;
        s.Human.Stockpile[ResourceType.Wood] = s.Human.Stockpile[ResourceType.Gold] = 500;
        Assert.False(s.Train(0, city.ProvinceId, BattalionType.Engineers).Ok);
        s.Human.Learn(Tech.Engineering);
        Assert.True(s.Train(0, city.ProvinceId, BattalionType.Engineers).Ok);
        Assert.Equal(BattalionRole.Engineers, BattalionType.Engineers.Role());

        var hillsWithRiver = _map.Provinces.First(p => p.Biome == Biome.Hills && p.HasRiver);
        Assert.Equal(1.25 * MilitaryRules.RiverDefense, MilitaryRules.DefenseMultiplier(hillsWithRiver), 6);
        Assert.Equal(1 + 0.25 * MilitaryRules.EngineeredTerrainDefense, MilitaryRules.DefenseMultiplier(hillsWithRiver, engineers: true), 6);
        var plain = _map.Provinces.First(p => p.Biome == Biome.Grassland && !p.HasRiver);
        Assert.Equal(1, MilitaryRules.DefenseMultiplier(plain, engineers: true), 6);

        var defenders = new List<Conquer.Game.Entities.Unit> { s.AddRegiment(1, hillsWithRiver.Id, BattalionType.LightInfantry) };
        double normal = GameSession.ExpectedFire(s.Engage(defenders, hillsWithRiver, attacking: false));
        double engineered = GameSession.ExpectedFire(s.Engage(defenders, hillsWithRiver, attacking: false, enemyEngineers: true));
        Assert.Equal(normal * MilitaryRules.DefenseMultiplier(hillsWithRiver, true) / MilitaryRules.DefenseMultiplier(hillsWithRiver), engineered, 6);

        var sappers = s.AddRegiment(0, a.Id, BattalionType.LightInfantry, BattalionType.Engineers);
        Assert.True(GameSession.HasEngineers([sappers]));
        var engaged = s.Engage([sappers], a, attacking: true);
        Assert.Equal(MilitaryRules.SupportExposure, engaged.Single(e => e.Role == BattalionRole.Engineers).Exposure);
    }

    /// <summary>A province a few stretches from <paramref name="from"/> through land player 0 may build roads on.</summary>
    private int FewStretchesAway(GameSession s, Province from)
    {
        bool Allowed(int id) => !_map.Provinces[id].IsWater && _map.Provinces[id].OwnerId != 1;
        return _map.Provinces.Where(p => Allowed(p.Id) && _map.DistanceKm(from, p) < 800)
            .Select(p => (p.Id, Path: s.Pathfinder.FindPath(from.Id, p.Id, Allowed)))
            .First(t => t.Path is { } path && path.Path.Count is >= 3 and <= 5).Id;
    }

    [Fact]
    public void EngineersLayRoadsFromACityOrHqToAnotherAlongTheMarchingRoute()
    {
        var (s, a, _) = TwoNations();
        s.Human.Stockpile[ResourceType.Wood] = s.Human.Stockpile[ResourceType.Gold] = 1000;
        int far = FewStretchesAway(s, a);
        var hq = s.AddUnit(0, UnitType.Headquarters, far, 50, headquartersLevel: 1);
        Assert.Equal(new[] { a.Id, far }.Order(), s.RoadHubs(0).Order());
        Assert.True(s.IsRoadHub(0, hq.ProvinceId));

        Assert.StartsWith("Requiere", s.CanBuildRoad(0, a.Id, far, RoadKind.Road).Message);
        s.Human.Learn(Tech.Engineering);
        Assert.Equal("Hacen falta ingenieros en la provincia.", s.CanBuildRoad(0, a.Id, far, RoadKind.Road).Message);

        // Only from a city or HQ.
        int between = s.PlanRoad(0, a.Id, far, RoadKind.Road)!.Route[1];
        s.AddRegiment(0, between, BattalionType.Engineers);
        Assert.StartsWith("Solo desde", s.CanBuildRoad(0, between, far, RoadKind.Road).Message);

        s.AddRegiment(0, a.Id, BattalionType.Engineers, BattalionType.Engineers);
        var plan = s.PlanRoad(0, a.Id, far, RoadKind.Road)!;
        Assert.Equal(s.Pathfinder.FindPath(a.Id, far, id => !_map.Provinces[id].IsWater && _map.Provinces[id].OwnerId != 1)!.Value.Path, plan.Route.Skip(1));
        Assert.Equal(plan.Route.Count - 1, plan.NewLinks);
        Assert.True(s.BuildRoad(0, a.Id, far, RoadKind.Road).Ok);
        Assert.Equal(1000 - 20 * plan.NewLinks, s.Human.Stockpile[ResourceType.Wood], 6);
        Assert.Equal("Ya están unidas por carretera (o lo estarán con las obras en curso).", s.CanBuildRoad(0, a.Id, far, RoadKind.Road).Message);

        // Three battalions along the route: three days of work a day, stretch after stretch from the start.
        var work = Assert.Single(s.RoadProjects);
        RunHours(s, 24);
        Assert.Equal(work.DaysPerLink - 3, work.WorkLeft, 6);
        RunUntil(s, () => s.RoadProjects.Count == 0, 24 * 60);
        Assert.Empty(s.RoadProjects);
        Assert.True(s.Connected(a.Id, far, RoadKind.Road));
        Assert.False(s.Connected(a.Id, far, RoadKind.Railway));
        Assert.Contains(s.Notifications, n => n.Text.StartsWith("Terminada la carretera"));

        // Marching along it is faster.
        double hours = s.Pathfinder.FindPath(a.Id, far)!.Value.Hours;
        s.Roads.Clear();
        Assert.True(s.Pathfinder.FindPath(a.Id, far)!.Value.Hours > hours * 1.4);
    }

    [Fact]
    public void RoadWorkStopsWithoutEngineersAndCancellingGivesBackTheRest()
    {
        var (s, a, _) = TwoNations();
        s.Human.Stockpile[ResourceType.Wood] = s.Human.Stockpile[ResourceType.Gold] = 1000;
        s.Human.Learn(Tech.Engineering);
        int far = FewStretchesAway(s, a);
        s.AddUnit(0, UnitType.Headquarters, far, 50, headquartersLevel: 1);
        var engineers = s.AddRegiment(0, a.Id, BattalionType.Engineers);
        Assert.True(s.BuildRoad(0, a.Id, far, RoadKind.Road).Ok);
        var work = s.RoadProjects[0];

        s.Disband(0, engineers.Id);
        RunHours(s, 24 * 3);
        Assert.Equal(work.DaysPerLink, work.WorkLeft, 6);

        double wood = s.Human.Stockpile[ResourceType.Wood];
        int left = s.LinksLeft(work);
        Assert.True(s.CancelRoad(0, work.Id).Ok);
        Assert.Empty(s.RoadProjects);
        Assert.Equal(wood + 20 * left, s.Human.Stockpile[ResourceType.Wood], 6);
    }

    [Fact]
    public void SupplyTravelsAlongRoadsBeyondItsRange()
    {
        var (s, a, _) = TwoNations();
        bool Allowed(int id) => !_map.Provinces[id].IsWater && _map.Provinces[id].OwnerId != 1;
        var (hours, _) = s.Pathfinder.FromSources([a.Id], canEnter: Allowed);
        int far = Enumerable.Range(0, hours.Length).First(id => hours[id] > MilitaryRules.SupplyRangeHours * 3 && hours[id] < MilitaryRules.SupplyRangeHours * 5);
        RunHours(s, 24);
        Assert.False(s.IsSupplied(0, far));

        var route = s.Pathfinder.FindPath(a.Id, far, Allowed)!.Value.Path.Prepend(a.Id).ToList();
        for (int i = 0; i + 1 < route.Count; i++) s.Roads.Lay(route[i], route[i + 1], RoadKind.Road);
        RunHours(s, 24);
        Assert.True(s.IsSupplied(0, far));
    }

    [Fact]
    public void OldSavesTurnRoadBuildingsIntoRoadsBetweenNeighbours()
    {
        var (s, a, b) = TwoNations();
        var save = s.ToSave("test");
        save = save with
        {
            Roads = null,
            Provinces = [.. save.Provinces.Select(p => p.Id == a.Id || p.Id == b.Id ? p with { Buildings = [.. p.Buildings, BuildingType.Road] } : p)],
        };
        var loaded = GameSession.Load(_map, save);
        Assert.Equal(RoadKind.Road, loaded.Roads.Between(a.Id, b.Id));
        Assert.Equal(1, loaded.Roads.Count);
        Assert.DoesNotContain(BuildingType.Road, a.Buildings);
    }

    [Fact]
    public void BattlesRecordLossesHourByHourAndHowTheyEnded()
    {
        var (s, a, b) = TwoNations();
        var defender = s.AddRegiment(1, b.Id, BattalionType.LightInfantry, BattalionType.LightInfantry);
        var attacker = s.AddRegiment(0, a.Id, BattalionType.HeavyInfantry, BattalionType.HeavyInfantry, BattalionType.HeavyInfantry);
        s.DeclareWar(0, 1);
        s.MoveUnit(0, attacker.Id, b.Id);
        RunUntil(s, () => s.BattleIn(b.Id) != null, 24 * 5);
        var battle = s.BattleIn(b.Id)!;
        int recorded = battle.History.Count;
        RunHours(s, 3);

        Assert.Equal(recorded + 3, battle.History.Count);
        Assert.True(battle.AttackerLosses > 0 && battle.DefenderLosses > 0);
        Assert.Equal(attacker.Citizens, battle.History[^1].AttackerMen, 0.5);
        Assert.Equal(defender.OrganisationShare, battle.History[^1].DefenderOrganisation, 6);
        Assert.True(battle.History[^1].AttackerFire > 0);
        Assert.Null(battle.AttackersWon);

        var loaded = GameSession.Load(_map, s.ToSave("test")).BattleIn(b.Id)!;
        Assert.Equal(battle.AttackerLosses, loaded.AttackerLosses);
        Assert.Equal(battle.DefenderLosses, loaded.DefenderLosses);

        RunUntil(s, () => s.BattleIn(b.Id) == null, 24 * 10);
        Assert.True(battle.AttackersWon);
        Assert.Equal(s.Date.Hours, battle.EndHours);
    }

    [Fact]
    public void AWeakAttackBreaksAndGivesUp()
    {
        var (s, a, b) = TwoNations();
        s.AddRegiment(1, b.Id, BattalionType.HeavyInfantry, BattalionType.HeavyInfantry, BattalionType.HeavyInfantry, BattalionType.HeavyInfantry);
        var attacker = s.AddRegiment(0, a.Id, BattalionType.RangedInfantry);
        s.DeclareWar(0, 1);
        s.MoveUnit(0, attacker.Id, b.Id);

        RunUntil(s, () => s.BattleIn(b.Id) != null, 24 * 5);
        RunUntil(s, () => s.BattleIn(b.Id) == null, 24 * 10);
        Assert.Equal(a.Id, attacker.ProvinceId);
        Assert.Null(attacker.AttackingProvinceId);
        Assert.True(attacker.OrganisationShare < MilitaryRules.BreakingOrganisation);
        Assert.Equal(1, b.ControllerId);
    }

    [Fact]
    public void SurroundedDefendersAreDestroyed()
    {
        var (s, a, b) = TwoNations();
        // Player 0 holds every land neighbour of B, so there is nowhere to retreat to.
        foreach (int n in b.Neighbors.Where(n => !_map.Provinces[n].IsWater && !_map.Provinces[n].IsOwned))
            s.Claim(0, s.AddRegiment(0, n, BattalionType.Scouts).Id);
        var defender = s.AddRegiment(1, b.Id, BattalionType.LightInfantry);
        s.DeclareWar(0, 1);
        foreach (int n in b.Neighbors.Where(n => _map.Provinces[n].ControllerId == 0 && !_map.Provinces[n].IsOccupied))
        {
            var unit = s.AddRegiment(0, n, BattalionType.HeavyInfantry, BattalionType.HeavyInfantry, BattalionType.HeavyInfantry);
            s.MoveUnit(0, unit.Id, b.Id);
        }

        RunUntil(s, () => s.UnitById(defender.Id) is null, 24 * 10);
        Assert.Null(s.UnitById(defender.Id));
        RunHours(s, 2); // the attackers march in once the battle is over
        Assert.Equal(0, b.ControllerId);
    }

    [Fact]
    public void PeaceReturnsOccupiedLandAndSendsArmiesHome()
    {
        var (s, a, b) = TwoNations();
        var regiment = s.AddRegiment(0, a.Id, BattalionType.LightInfantry);
        s.DeclareWar(0, 1);
        s.MoveUnit(0, regiment.Id, b.Id);
        RunUntil(s, () => b.IsOccupied, 24 * 5);
        Assert.True(b.IsOccupied);

        s.MakePeace(0, 1);
        Assert.False(s.AtWar(0, 1));
        Assert.False(b.IsOccupied);
        Assert.Equal(0, _map.Provinces[regiment.ProvinceId].ControllerId);
    }

    [Fact]
    public void WallsHaveToBeBesiegedAndFallInTime()
    {
        var (s, a, b) = TwoNations();
        b.AddBuilding(BuildingType.Walls);
        var regiment = s.AddRegiment(0, a.Id, BattalionType.LightInfantry);
        s.DeclareWar(0, 1);
        s.MoveUnit(0, regiment.Id, b.Id);
        RunUntil(s, () => regiment.ProvinceId == b.Id, 24 * 5);

        // Marching in does not take it: the regiment lays siege, and stays in supply from home next door.
        Assert.False(b.IsOccupied);
        var siege = Assert.IsType<Siege>(s.SiegeAt(b.Id));
        Assert.Equal(0, siege.AttackerId);
        Assert.True(s.IsBesieging(regiment));
        Assert.True(s.IsInSupply(regiment));
        Assert.Equal(1, s.DailySiegeWork(siege), 6);

        RunHours(s, 24 * (int)(GameSession.SiegeDays(b) - 3));
        Assert.False(b.IsOccupied);
        RunHours(s, 24 * 5);
        Assert.True(b.IsOccupied);
        Assert.Null(s.SiegeAt(b.Id));
    }

    [Fact]
    public void ArtilleryShortensTheSiegeAndLeavingLiftsIt()
    {
        var (s, a, b) = TwoNations();
        b.AddBuilding(BuildingType.Castle);
        var regiment = s.AddRegiment(0, a.Id, BattalionType.LightInfantry, BattalionType.Artillery);
        s.DeclareWar(0, 1);
        s.MoveUnit(0, regiment.Id, b.Id);
        RunUntil(s, () => s.SiegeAt(b.Id) != null, 24 * 5);
        Assert.Equal(1 + regiment.Battalions[1].Info.Attack / MilitaryRules.SiegeAttackPerDay, s.DailySiegeWork(s.SiegeAt(b.Id)!), 6);

        s.MoveUnit(0, regiment.Id, a.Id);
        RunUntil(s, () => regiment.ProvinceId == a.Id, 24 * 5);
        RunHours(s, 24);
        Assert.Null(s.SiegeAt(b.Id));
        Assert.False(b.IsOccupied);
    }

    [Fact]
    public void PeaceStartsATruceThatOutlastsASave()
    {
        var (s, _, _) = TwoNations();
        s.DeclareWar(0, 1);
        s.MakePeace(0, 1);
        Assert.Equal(GameRules.TruceDays, s.TruceDaysLeft(1, 0), 6);
        Assert.False(s.CanDeclareWar(1, 0).Ok);

        RunHours(s, 24 * 10);
        var loaded = GameSession.Load(_map, s.ToSave("test"));
        Assert.Equal(GameRules.TruceDays - 10, loaded.TruceDaysLeft(0, 1), 6);
        Assert.False(loaded.CanDeclareWar(0, 1).Ok);
    }

    [Fact]
    public void TheTruceEnds()
    {
        var (s, _, _) = TwoNations();
        s.DeclareWar(0, 1);
        s.MakePeace(0, 1);
        RunHours(s, 24 * GameRules.TruceDays);
        Assert.Equal(0, s.TruceDaysLeft(0, 1));
        Assert.True(s.CanDeclareWar(0, 1).Ok);
    }

    [Fact]
    public void TreatyKeepsTheOccupiedLandWithItsCity()
    {
        var (s, a, b) = TwoNations();
        // The rival's capital one province beyond B, too far from ours to be "too close".
        var c = b.Neighbors.Select(n => _map.Provinces[n])
            .First(p => p.IsClaimable && !p.IsOwned && p.Neighbors.All(n => n != a.Id && !_map.Provinces[n].CityId.HasValue));
        Assert.True(s.FoundCity(1, s.AddUnit(1, UnitType.Settlers, c.Id, 300).Id).Ok);
        var city = s.CityIn(c)!;
        var rival = s.Players[1];
        Assert.Equal(city.Id, rival.CapitalCityId);
        var regiment = s.AddRegiment(0, a.Id, BattalionType.LightInfantry);
        s.DeclareWar(0, 1);
        Assert.False(s.CanProposePeace(0, 1, PeaceTerms.TakeOccupied).Ok);
        s.MoveUnit(0, regiment.Id, c.Id);
        RunUntil(s, () => c.IsOccupied, 24 * 10);

        // All their land, capital included: the whole of their nation.
        Assert.True(b.IsOccupied && c.IsOccupied);
        Assert.Equal(100, s.WarScore(0, 1), 6);
        Assert.Equal(-100, s.WarScore(1, 0), 6);
        Assert.Equal(100, s.PeaceCost(0, 1, PeaceTerms.TakeOccupied), 6);
        Assert.True(s.CanProposePeace(0, 1, PeaceTerms.TakeOccupied).Ok);

        Assert.Equal((2, 0), s.MakePeace(0, 1, PeaceTerms.TakeOccupied));
        Assert.False(s.AtWar(0, 1));
        Assert.Equal(0, c.OwnerId);
        Assert.False(c.IsOccupied);
        Assert.Contains(b.Id, s.Human.Provinces);
        Assert.Empty(rival.Provinces);
        Assert.Equal(0, city.OwnerId);
        Assert.Null(rival.CapitalCityId);
        Assert.Equal(c.Id, regiment.ProvinceId);
    }

    [Fact]
    public void TreatyCanHandOverOurOccupiedLand()
    {
        var (s, a, b) = TwoNations();
        b.Population = 500;
        var regiment = s.AddRegiment(1, b.Id, BattalionType.LightInfantry);
        s.DeclareWar(1, 0);
        s.MoveUnit(1, regiment.Id, a.Id);
        RunUntil(s, () => a.IsOccupied, 24 * 5);
        Assert.True(a.IsOccupied);
        double mood = a.Mood;

        Assert.Equal((0, 1), s.MakePeace(0, 1, PeaceTerms.CedeOccupied));
        Assert.Equal(1, a.OwnerId);
        Assert.Equal(1, s.CityIn(a)!.OwnerId);
        Assert.Null(s.Human.CapitalCityId);
        Assert.Equal(s.CityIn(a)!.Id, s.Players[1].CapitalCityId);
        Assert.Equal(mood - GameRules.CededMoodPenalty, a.Mood, 6);
    }

    [Fact]
    public void BattlesWonAddToTheWarScore()
    {
        var (s, a, b) = TwoNations();
        var attacker = s.AddRegiment(1, b.Id, BattalionType.LightInfantry);
        s.AddRegiment(0, a.Id, [.. Enumerable.Repeat(BattalionType.LightInfantry, 6)]);
        s.DeclareWar(1, 0);
        s.MoveUnit(1, attacker.Id, a.Id);
        RunUntil(s, () => s.WarVictories(0, 1) > 0, 24 * 10);

        Assert.Equal(1, s.WarVictories(0, 1));
        Assert.False(a.IsOccupied);
        Assert.Equal(GameRules.WarScorePerVictory, s.WarScore(0, 1), 6);
    }

    [Fact]
    public void ComputerRivalsOnlyAcceptPeaceAfterAWhile()
    {
        var s = GameSession.Create(_map, 2, seed: 7);
        // A human army at least as strong as the rival's, so the rival is not winning.
        var home = s.Units.First(u => u.OwnerId == 0).ProvinceId;
        s.Human.Learn(Tech.Armouries); // heavy infantry, not swordsmen
        s.AddRegiment(0, home, [.. Enumerable.Repeat(BattalionType.HeavyInfantry, 12)]);
        s.Human.Stockpile[ResourceType.Gold] = s.Human.Stockpile[ResourceType.Iron] = 1000; // to pay for it
        s.DeclareWar(0, 1);
        Assert.False(s.ProposePeace(0, 1).Ok);
        RunHours(s, 24 * 61);
        Assert.True(s.ProposePeace(0, 1).Ok);
        Assert.False(s.AtWar(0, 1));
    }

    [Fact]
    public void DivisionsWitherWithoutSupply()
    {
        var (s, a, _) = TwoNations();
        var home = s.AddRegiment(0, a.Id, BattalionType.LightInfantry);
        var far = _map.Provinces.First(p => p.IsClaimable && _map.DistanceKm(p, a) > 6000);
        var lost = s.AddRegiment(0, far.Id, BattalionType.LightInfantry);

        RunHours(s, 24);
        Assert.True(s.IsInSupply(home));
        Assert.False(s.IsInSupply(lost));
        Assert.Equal(1, home.OrganisationShare, 6);
        Assert.True(lost.OrganisationShare < 1);
        Assert.True(lost.Citizens < BattalionType.LightInfantry.First().Men);
    }

    [Fact]
    public void DivisionsInSupplyRecoverAndAreReinforcedFromTheCapital()
    {
        var (s, a, _) = TwoNations();
        a.Population = 1000;
        var regiment = s.AddRegiment(0, a.Id, BattalionType.LightInfantry);
        var battalion = regiment.Battalions[0];
        battalion.Organisation = 0;
        battalion.Strength = 50;

        RunHours(s, 25); // the recruits leave the capital at midnight and arrive an hour later
        Assert.Equal(battalion.Info.MaxOrganisation * MilitaryRules.OrganisationRecovery, battalion.Organisation, 6);
        double reinforcement = battalion.Info.Men * MilitaryRules.ReinforcementRate;
        Assert.Equal(50 + reinforcement, battalion.Strength, 6);
    }

    [Fact]
    public void HeadquartersInRangeGiveABonusUpTheChain()
    {
        var (s, a, _) = TwoNations();
        var regiment = s.AddRegiment(0, a.Id, BattalionType.LightInfantry);
        var corps = s.AddHeadquarters(0, a.Id, 1);
        var army = s.AddHeadquarters(0, a.Id, 2);
        Assert.Equal("I Cuerpo", corps.Name);
        Assert.Equal("1.er Ejército", army.Name);

        Assert.False(s.Attach(0, regiment.Id, army.Id).Ok); // combat units report to a corps
        Assert.True(s.Attach(0, regiment.Id, corps.Id).Ok);
        Assert.Equal(MilitaryRules.CommandBonus, s.CommandBonus(regiment), 6);
        Assert.True(s.Attach(0, corps.Id, army.Id).Ok);
        Assert.Equal(MilitaryRules.CommandBonus + MilitaryRules.HigherCommandBonus, s.CommandBonus(regiment), 6);

        corps.ProvinceId = _map.Provinces.First(p => p.IsClaimable && _map.DistanceKm(p, a) > 2000).Id;
        Assert.Equal(0, s.CommandBonus(regiment));
    }

    [Fact]
    public void EveryNationStartsWithATemplateOfTwoWarriors()
    {
        var s = GameSession.Create(_map, 3, seed: 7, computerRivals: false);
        Assert.All(s.Players, p =>
        {
            var template = Assert.Single(p.Templates);
            Assert.Equal("Plantilla I", template.Name);
            Assert.Equal([BattalionType.LightInfantry, BattalionType.LightInfantry], template.Battalions);
        });
    }

    [Fact]
    public void TemplatesAreEditedWithinTheirLimits()
    {
        var (s, _, _) = TwoNations();
        Assert.True(s.CreateTemplate(0).Ok);
        var template = s.Human.Templates[^1];
        Assert.Equal("Plantilla II", template.Name);
        Assert.Equal([BattalionType.LightInfantry], template.Battalions);

        Assert.False(s.AddToTemplate(0, template.Id, BattalionType.RangedInfantry).Ok); // archery not known yet
        s.Human.Learn(Tech.Archery);
        for (int i = 1; i < MilitaryRules.MaxBattalionsPerRegiment; i++) Assert.True(s.AddToTemplate(0, template.Id, BattalionType.RangedInfantry).Ok);
        Assert.False(s.AddToTemplate(0, template.Id, BattalionType.LightInfantry).Ok); // full

        Assert.True(s.DuplicateTemplate(0, template.Id).Ok);
        Assert.Equal(template.Battalions, s.Human.Templates[^1].Battalions);
        for (int i = 0; i < MilitaryRules.MaxBattalionsPerRegiment - 1; i++) Assert.True(s.RemoveFromTemplate(0, template.Id, 0).Ok);
        Assert.False(s.RemoveFromTemplate(0, template.Id, 0).Ok); // at least one battalion

        foreach (var t in s.Human.Templates.Skip(1).ToList()) Assert.True(s.DeleteTemplate(0, t.Id).Ok);
        Assert.False(s.DeleteTemplate(0, s.Human.Templates[0].Id).Ok); // at least one template
    }

    [Fact]
    public void ATemplateTrainsAWholeRegimentAtOnce()
    {
        var (s, a, _) = TwoNations();
        var city = s.CityIn(a)!;
        a.Population = 2000;
        s.Human.Stockpile[ResourceType.Wood] = s.Human.Stockpile[ResourceType.Gold] = 500;
        s.Human.Learn(Tech.Archery);
        var template = s.Human.Templates[0];
        s.AddToTemplate(0, template.Id, BattalionType.RangedInfantry);

        Assert.Equal(20, template.TrainingDays(s.Human.Techs)); // the archers are the slowest
        Assert.True(s.TrainTemplate(0, city.ProvinceId, template.Id).Ok);
        Assert.Equal(2000 - 300, a.Population);
        Assert.Equal(500, s.Human.Stockpile[ResourceType.Wood]);
        // Warriors and archers share the ancient weapons.
        Assert.Equal(100_000 - 300, s.Human.EquipmentOf(BattalionType.LightInfantry.First()));
        Assert.Equal(100_000 - 300, s.Human.EquipmentOf(BattalionType.RangedInfantry.First()));
        Assert.Equal(500 - 50, s.Human.Stockpile[ResourceType.Gold]);

        RunHours(s, 24 * 20);
        var regiment = Assert.Single(s.Units, u => u.IsMilitary && u.OwnerId == 0);
        Assert.Equal([BattalionType.LightInfantry, BattalionType.LightInfantry, BattalionType.RangedInfantry], regiment.Battalions.Select(b => b.Type));
        Assert.Equal(300, regiment.Citizens);
    }

    [Fact]
    public void OnlyProvincesWithBarracksTrainCombatTroops()
    {
        var (s, a, _) = TwoNations();
        a.ClearBuildings();
        a.Population = 3000;
        foreach (var r in Resources.All) s.Human.Stockpile[r] = 5000;
        s.Human.Learn(Tech.Engineering);
        s.Human.Learn(Tech.Navigation);

        Assert.Equal("Requiere un cuartel en la provincia.", s.CanTrain(a, BattalionType.LightInfantry).Message);
        Assert.False(s.CanTrainTemplate(a, s.Human.Templates[0]).Ok);
        // Scouts, engineers and HQs need no barracks in a city; ships need a port instead.
        Assert.True(s.Train(0, a.Id, BattalionType.Scouts).Ok);
        Assert.True(s.Train(0, a.Id, BattalionType.Engineers).Ok);
        Assert.True(s.RaiseHeadquarters(0, a.Id, 1).Ok);
        var scoutsOnly = s.AddTemplate(s.Human, [BattalionType.Scouts, BattalionType.Engineers]);
        Assert.True(s.CanTrainTemplate(a, scoutsOnly).Ok);
        Assert.Null(BattalionType.LineShip.First().TrainingBuilding(BattalionType.LineShip));

        // Barracks need no advance, and once built the city trains combat troops.
        Assert.True(s.Build(0, a.Id, BuildingType.Barracks).Ok);
        Assert.False(s.CanTrain(a, BattalionType.LightInfantry).Ok); // still under construction
        RunHours(s, 24 * BuildingType.Barracks.Info().Days);
        Assert.Contains(BuildingType.Barracks, a.Buildings);
        Assert.True(s.Train(0, a.Id, BattalionType.LightInfantry).Ok);
        Assert.True(s.TrainTemplate(0, a.Id, s.Human.Templates[0].Id).Ok);
    }

    [Fact]
    public void BarracksTrainTroopsInAProvinceWithoutACity()
    {
        var (s, a, b) = TwoNations();
        foreach (var r in Resources.All) s.Human.Stockpile[r] = 5000;
        var field = _map.Provinces[a.Neighbors.First(n => n != b.Id && _map.Provinces[n].IsClaimable && !_map.Provinces[n].IsOwned)];
        var claimer = s.AddRegiment(0, field.Id, BattalionType.Scouts);
        Assert.True(s.Claim(0, claimer.Id).Ok);
        s.Disband(0, claimer.Id);
        field.Population = 500;

        // Without a city or barracks the province trains nothing.
        Assert.Equal("Hace falta una ciudad, un cuartel o un taller en la provincia.", s.CanTrain(field, BattalionType.Scouts).Message);
        Assert.Equal("Hace falta una ciudad, un cuartel o un taller en la provincia.", s.CanRaiseHeadquarters(field, 1).Message);
        Assert.True(s.IsBuildingAvailable(field, BuildingType.Barracks).Ok);
        Assert.True(s.Build(0, field.Id, BuildingType.Barracks).Ok);
        RunHours(s, 24 * BuildingType.Barracks.Info().Days);
        Assert.Null(field.CityId);

        // With them it trains like a city, keeping enough people to stay settled; ships still need a port.
        Assert.False(s.CanTrain(field, BattalionType.LineShip).Ok);
        Assert.True(s.Train(0, field.Id, BattalionType.LightInfantry).Ok);
        Assert.Single(field.Training);
        field.Population = 100;
        Assert.Equal($"Hacen falta {100 + GameRules.SettledPopulation} habitantes.", s.CanTrain(field, BattalionType.LightInfantry).Message);

        // What it trains is saved with the province.
        var loaded = GameSession.Load(_map, s.ToSave("test"));
        Assert.Single(_map.Provinces[field.Id].Training);
        RunHours(loaded, 24 * GameSession.TrainingDays(loaded.Human, BattalionType.LightInfantry));
        Assert.Empty(_map.Provinces[field.Id].Training);
        Assert.Contains(loaded.Units, u => u.OwnerId == 0 && u.ProvinceId == field.Id && u.Battalions.Any(x => x.Type == BattalionType.LightInfantry));
    }

    [Fact]
    public void OnlyProvincesWithAWorkshopBuildWarMachines()
    {
        var (s, a, _) = TwoNations();
        a.Population = 3000;
        foreach (var r in Resources.All) s.Human.Stockpile[r] = 5000;
        Assert.Equal(
            [BattalionType.Armour, BattalionType.Artillery, BattalionType.AntiAir],
            Battalions.All.Where(t => t.Models().Any(m => m.TrainingBuilding(t) == BuildingType.Workshop)));

        // The workshop can be built from the start; war machines need their advance.
        Assert.True(s.IsBuildingAvailable(a, BuildingType.Workshop).Ok);
        Assert.Equal("Requiere maquinaria de asedio.", s.CanTrain(a, BattalionType.Artillery).Message);
        s.Human.Learn(Tech.SiegeEngines);
        Assert.Equal("Requiere un taller en la provincia.", s.CanTrain(a, BattalionType.Artillery).Message);
        var siege = s.AddTemplate(s.Human, [BattalionType.LightInfantry, BattalionType.Artillery]);
        Assert.Equal("Requiere un taller en la provincia.", s.CanTrainTemplate(a, siege).Message);
        a.RemoveBuilding(BuildingType.Barracks);
        Assert.Equal("Requiere un cuartel y un taller en la provincia.", s.CanTrainTemplate(a, siege).Message);

        Assert.True(s.Build(0, a.Id, BuildingType.Workshop).Ok);
        RunHours(s, 24 * BuildingType.Workshop.Info().Days);
        Assert.Contains(BuildingType.Workshop, a.Buildings);
        // A workshop builds war machines but drills no soldiers: that is still the barracks' job.
        Assert.True(s.Train(0, a.Id, BattalionType.Artillery).Ok);
        Assert.Equal("Requiere un cuartel en la provincia.", s.CanTrain(a, BattalionType.LightInfantry).Message);
        a.AddBuilding(BuildingType.Barracks);
        Assert.True(s.TrainTemplate(0, a.Id, siege.Id).Ok);
    }

    [Fact]
    public void WorkshopsBecomeFactoriesWithIndustrialization()
    {
        var (s, a, _) = TwoNations();
        a.Population = 3000;
        foreach (var r in Resources.All) s.Human.Stockpile[r] = 5000;
        s.Human.Learn(Tech.SiegeEngines);
        s.Human.Learn(Tech.Metallurgy);
        a.AddBuilding(BuildingType.Workshop);

        s.Human.Learn(Tech.Industrialization);
        // From then on factories are built instead of workshops, also where there is no city.
        Assert.Equal("Ahora se construye una fábrica.", s.IsBuildingAvailable(a, BuildingType.Workshop).Message);
        Assert.False(BuildingType.Factory.Info().CityOnly);
        RunHours(s, 24);
        Assert.DoesNotContain(BuildingType.Workshop, a.Buildings);
        Assert.Contains(BuildingType.Factory, a.Buildings);
        Assert.Equal(0.5, a.BuildingBonuses.Deposits);
        Assert.Contains(s.Notifications, n => n.Text == "Industrialización: tu taller pasa a ser una fábrica.");

        // The factory builds the war machines as the workshop did.
        Assert.True(s.Train(0, a.Id, BattalionType.Artillery).Ok);
        a.RemoveBuilding(BuildingType.Factory);
        Assert.Equal("Requiere una fábrica en la provincia.", s.CanTrain(a, BattalionType.Artillery).Message);
        Assert.Equal(0, a.BuildingBonuses.Deposits);
    }

    [Fact]
    public void MilitaryAdvancesTrainTheBattalionsTheyStudyFaster()
    {
        var (s, a, _) = TwoNations();
        var city = s.CityIn(a)!;
        a.Population = 3000;
        foreach (var r in Resources.All) s.Human.Stockpile[r] = 5000;
        s.Human.Learn(Tech.Archery);
        Assert.Equal(20, GameSession.TrainingDays(s.Human, BattalionType.RangedInfantry));

        // Improved bows speed up the archers only.
        s.Human.Learn(Tech.ImprovedBows);
        Assert.Equal(16, GameSession.TrainingDays(s.Human, BattalionType.RangedInfantry)); // 20 / 1.25
        Assert.Equal(15, GameSession.TrainingDays(s.Human, BattalionType.LightInfantry));
        Assert.True(s.Train(0, city.ProvinceId, BattalionType.RangedInfantry).Ok);
        Assert.Equal(16, a.Training[^1].TotalDays);

        // A template waits for its slowest battalion, each at its own pace.
        var template = s.AddTemplate(s.Human, [BattalionType.LightInfantry, BattalionType.RangedInfantry]);
        Assert.Equal(20, template.TrainingDays(s.Human.Techs));
        Assert.Equal(16, GameSession.TrainingDays(s.Human, template));
        s.Human.Learn(Tech.Drill);
        Assert.Equal(12, GameSession.TrainingDays(s.Human, BattalionType.LightInfantry)); // 15 / 1.25
        Assert.Equal(16, GameSession.TrainingDays(s.Human, template));

        // Advances add up: knights are studied by horse breeding and armouries.
        s.Human.Learn(Tech.Stirrup);
        s.Human.Learn(Tech.HorseBreeding);
        s.Human.Learn(Tech.Armouries);
        Assert.Equal(30, GameSession.TrainingDays(s.Human, BattalionType.Cavalry)); // 45 / 1.5
    }

    [Fact]
    public void AdvancesThatSpeedUpTrainingAreMilitaryAndStudyCombatTroops()
    {
        var faster = Techs.All.Where(t => t.Info().FasterTraining.Length > 0).ToList();
        Assert.Equal(9, faster.Count);
        foreach (var tech in faster)
        {
            Assert.Equal(TechBranch.Military, tech.Info().Branch);
            Assert.All(tech.Info().FasterTraining, type => Assert.Contains(type.Models(), m => m.TrainingBuilding(type) != null));
        }
        // Every combat battalion has some advance that speeds it up.
        Assert.All(Battalions.All.Where(t => t.Models().Any(m => m.TrainingBuilding(t) != null)), type => Assert.Contains(faster, t => t.Info().FasterTraining.Contains(type)));
    }

    [Fact]
    public void CombatUnitsAreNamedByTheirSize()
    {
        Assert.Equal("3.er Regimiento", Formations.CombatUnitName(3, Echelon.Regiment));
        Assert.Equal("12.º Regimiento", Formations.CombatUnitName(12, Echelon.Regiment));
        Assert.Equal("3.ª Brigada", Formations.CombatUnitName(3, Echelon.Brigade));
        Assert.Equal("3.ª División", Formations.CombatUnitName(3, Echelon.Division));
        Assert.Equal("IV Cuerpo", Formations.HeadquartersName(1, 4));
        Assert.Equal("1.er Ejército", Formations.HeadquartersName(2, 1));
        Assert.Equal("2.º Grupo de ejércitos", Formations.HeadquartersName(3, 2));
        Assert.Equal("Batallón de arqueros", Formations.BattalionName(BattalionType.RangedInfantry.First()));
        Assert.Equal("Trirreme", Formations.BattalionName(BattalionType.LineShip.First()));
    }

    [Fact]
    public void MountainTroopsFightBetterInRoughTerrain()
    {
        var (s, a, _) = TwoNations();
        s.Human.Learn(Tech.MilitaryTactics);
        var mountaineers = s.AddRegiment(0, a.Id, BattalionType.MountainInfantry);
        var phalanx = s.AddRegiment(0, a.Id, BattalionType.HeavyInfantry);
        var rough = _map.Provinces.First(p => !p.IsWater && MilitaryRules.IsRough(p.Biome));
        var open = _map.Provinces.First(p => !p.IsWater && !MilitaryRules.IsRough(p.Biome));
        double Fire(Conquer.Game.Entities.Unit u, Province p) => s.Engage([u], p, attacking: true).Sum(e => e.Fire);

        Assert.Equal(Fire(mountaineers, open) * MilitaryRules.MountainTroopsRoughTerrain, Fire(mountaineers, rough), 6);
        Assert.Equal(Fire(phalanx, open), Fire(phalanx, rough), 6);
    }

    [Fact]
    public void MedicsDoNotFightNorCountAsAnotherKindOfTroop()
    {
        var (s, a, _) = TwoNations();
        var unit = s.AddRegiment(0, a.Id, BattalionType.LightInfantry, BattalionType.Medics);
        Assert.True(GameSession.HasMedics(unit));
        var engaged = s.Engage([unit], a, attacking: true);
        Assert.DoesNotContain(engaged, e => e.Battalion.Type == BattalionType.Medics);
        Assert.Equal(0, GameSession.CombinedArms([BattalionRole.Infantry, BattalionRole.Support]));
        Assert.False(GameSession.HasMedics(s.AddRegiment(0, a.Id, BattalionType.LightInfantry)));
    }

    [Fact]
    public void AntiAirFiresHarderAtAircraftAndBluntsTheirFire()
    {
        var (s, a, b) = TwoNations();
        s.Human.Learn(Tech.Aviation);
        s.Players[1].Learn(Tech.Aviation);
        var bombers = s.Engage([s.AddRegiment(0, a.Id, BattalionType.Bombers)], b, attacking: true);
        var guns = s.Engage([s.AddRegiment(1, b.Id, BattalionType.AntiAir, BattalionType.AntiAir)], b, attacking: false);
        var infantry = s.Engage([s.AddRegiment(1, b.Id, BattalionType.LightInfantry)], b, attacking: false);

        var (air, ground) = GameSession.FaceEachOther(bombers, guns);
        Assert.Equal(bombers.Sum(e => e.Fire) * (1 - 2 * MilitaryRules.AntiAirShield), air.Sum(e => e.Fire), 6);
        Assert.Equal(guns.Sum(e => e.Fire) * MilitaryRules.AntiAirAgainstAircraft, ground.Sum(e => e.Fire), 6);
        // Without aircraft to shoot at, the guns fire as they are; without guns, the aircraft too.
        var (_, alone) = GameSession.FaceEachOther(infantry, guns);
        Assert.Equal(guns.Sum(e => e.Fire), alone.Sum(e => e.Fire), 6);
        Assert.Equal(bombers.Sum(e => e.Fire), GameSession.FaceEachOther(bombers, infantry).Attacking.Sum(e => e.Fire), 6);
    }

    [Fact]
    public void ForcesCostUpkeepEveryDay()
    {
        var (s, a, _) = TwoNations();
        s.Human.Learn(Tech.Armouries); // heavy infantry: 50 gold and medieval weapons, 20 iron
        s.AddRegiment(0, a.Id, BattalionType.LightInfantry, BattalionType.HeavyInfantry);
        s.AddHeadquarters(0, a.Id, 1);
        var upkeep = s.Upkeep(s.Human);

        double gold = (15 + 50 + 60) * MilitaryRules.UpkeepGoldShare; // two battalions and a corps
        Assert.Equal(gold, upkeep[(int)ResourceType.Gold], 6);
        Assert.Equal(20 * MilitaryRules.UpkeepResourceShare, upkeep[(int)ResourceType.Iron], 6);
        Assert.Equal(0, upkeep[(int)ResourceType.Wood]); // wood only goes into raising them

        s.Human.Stockpile[ResourceType.Gold] = s.Human.Stockpile[ResourceType.Iron] = 1000;
        RunHours(s, 24);
        Assert.False(s.Human.ArmyUnpaid);
        Assert.Equal(1000 - 20 * MilitaryRules.UpkeepResourceShare, s.Human.Stockpile[ResourceType.Iron], 6);
    }

    [Fact]
    public void AnUnpaidArmyLosesHeartAndMen()
    {
        var (s, a, _) = TwoNations();
        s.Human.Learn(Tech.Armouries); // heavy infantry, with iron weapons
        var unit = s.AddRegiment(0, a.Id, BattalionType.HeavyInfantry);
        s.Human.Stockpile[ResourceType.Iron] = 0; // nothing to keep its iron weapons
        var b = unit.Battalions[0];

        RunHours(s, 24);

        Assert.True(s.Human.ArmyUnpaid);
        Assert.Equal(0, s.Human.Stockpile[ResourceType.Iron]);
        Assert.True(b.Strength < b.Info.Men);
        Assert.Contains(s.Notifications, n => n.Text.StartsWith("No hay con qué pagar al ejército"));
    }

    [Fact]
    public void ACorpsCommandsAtMostFiveUnits()
    {
        var (s, a, _) = TwoNations();
        var corps = s.AddHeadquarters(0, a.Id, 1);
        for (int i = 0; i < 5; i++) Assert.True(s.Attach(0, s.AddRegiment(0, a.Id, BattalionType.LightInfantry).Id, corps.Id).Ok);
        Assert.False(s.Attach(0, s.AddRegiment(0, a.Id, BattalionType.LightInfantry).Id, corps.Id).Ok);
    }

    [Fact]
    public void EveryHeadquartersHasAGeneralOfItsRankWhoLeadsTheUnitsInRange()
    {
        var (s, a, _) = TwoNations();
        var corps = s.AddHeadquarters(0, a.Id, 1);
        var unit = s.AddRegiment(0, a.Id, BattalionType.LightInfantry);
        Assert.NotNull(corps.Officer);
        Assert.Equal(OfficerRank.LieutenantGeneral, corps.Officer!.Rank);
        Assert.InRange(corps.Officer.Skill, 1, 3);
        Assert.Null(s.GeneralOf(unit));
        Assert.True(s.Attach(0, unit.Id, corps.Id).Ok);
        Assert.Same(corps.Officer, s.GeneralOf(unit));
    }

    [Fact]
    public void OfficersEarnStarsWithVictoriesAndHelpByTheirVirtues()
    {
        var general = new Officer(0, "Álvaro Castro", [OfficerTrait.Offensive], 1);
        general.Victories = Officer.VictoriesPerStar;
        Assert.Equal(2, general.Skill);
        general.Victories = 100;
        Assert.Equal(Officer.MaxSkill, general.Skill);
        Assert.Equal(Officer.AttackPerStar * Officer.MaxSkill, general.FireBonus(attacking: true), 6);
        Assert.Equal(0, general.FireBonus(attacking: false));
        Assert.Equal(0, general.OrganisationLoss);
        Assert.Equal(-Officer.MaxShield, new Officer(1, "Inés Lara", [OfficerTrait.Tactician], 5).OrganisationLoss, 6);
    }

    [Fact]
    public void BattlesGiveExperienceAndTheWinnersGeneralAVictory()
    {
        var (s, a, b) = TwoNations();
        s.AddRegiment(1, b.Id, BattalionType.LightInfantry);
        var attacker = s.AddRegiment(0, a.Id, [.. Enumerable.Repeat(BattalionType.HeavyInfantry, 6)]);
        var corps = s.AddHeadquarters(0, a.Id, 1);
        s.Attach(0, attacker.Id, corps.Id);
        int victories = corps.Officer!.Victories;
        s.DeclareWar(0, 1);
        s.MoveUnit(0, attacker.Id, b.Id);

        RunUntil(s, () => s.BattleIn(b.Id) != null, 24 * 5);
        RunUntil(s, () => s.BattleIn(b.Id) == null, 24 * 10);
        Assert.Equal(0, b.ControllerId);
        Assert.All(attacker.Battalions, x => Assert.True(x.Experience > 0));
        Assert.Equal(victories + 1, corps.Officer.Victories);
    }

    [Fact]
    public void RecruitsWaterDownExperience()
    {
        var (s, a, _) = TwoNations();
        a.Population = 1000;
        var battalion = s.AddRegiment(0, a.Id, BattalionType.LightInfantry).Battalions[0];
        battalion.Strength = 50;
        battalion.Experience = 0.5;

        RunHours(s, 24);
        Assert.Equal(0.5 * 50 / battalion.Strength, battalion.Experience, 6);
    }

    [Fact]
    public void OnlyAFrontsWorthOfBattalionsFightWithSupportBehind()
    {
        var (s, a, _) = TwoNations();
        var infantry = s.AddRegiment(0, a.Id, [.. Enumerable.Repeat(BattalionType.HeavyInfantry, 12)]);
        var more = s.AddRegiment(0, a.Id, [.. Enumerable.Repeat(BattalionType.HeavyInfantry, 6)]);
        var siege = s.AddRegiment(0, a.Id, [.. Enumerable.Repeat(BattalionType.Artillery, 12)]);
        var engaged = s.Engage([infantry, more, siege], a, attacking: true);
        int width = MilitaryRules.FrontWidth(a.Biome);
        Assert.Equal(width, engaged.Count(e => e.Role == BattalionRole.Infantry));
        Assert.Equal(width / 2, engaged.Count(e => e.Role == BattalionRole.Artillery));
        Assert.All(engaged.Where(e => e.Role == BattalionRole.Artillery), e => Assert.Equal(MilitaryRules.SupportExposure, e.Exposure));

        var peaks = _map.Provinces.First(p => p.Biome == Biome.HighMountains);
        Assert.True(MilitaryRules.FrontWidth(peaks.Biome) < width);
        Assert.Equal(MilitaryRules.FrontWidth(peaks.Biome), s.Engage([infantry], peaks, attacking: false).Count);
    }

    [Fact]
    public void MixingKindsOfTroopsHitsHarder()
    {
        Assert.Equal(0, GameSession.CombinedArms([BattalionRole.Infantry, BattalionRole.Infantry]));
        Assert.Equal(MilitaryRules.CombinedArmsBonus, GameSession.CombinedArms([BattalionRole.Infantry, BattalionRole.Cavalry]), 6);
        Assert.Equal(MilitaryRules.MaxCombinedArmsBonus, GameSession.CombinedArms(Enum.GetValues<BattalionRole>()), 6);
    }

    [Fact]
    public void GeneralsAndExperienceAreSavedAndOldHeadquartersGetAGeneral()
    {
        var (s, a, _) = TwoNations();
        var corps = s.AddHeadquarters(0, a.Id, 1);
        var unit = s.AddRegiment(0, a.Id, BattalionType.LightInfantry);
        unit.Battalions[0].Experience = 0.4;
        corps.Officer!.Victories = 4;

        var save = s.ToSave("test");
        var loaded = GameSession.Load(_map, save);
        Assert.Equal(0.4, loaded.UnitById(unit.Id)!.Battalions[0].Experience, 6);
        var general = loaded.UnitById(corps.Id)!.Officer!;
        Assert.Equal((corps.Officer.Name, corps.Officer.Skill, corps.Officer.Rank), (general.Name, general.Skill, general.Rank));
        Assert.Equal(corps.Officer.Traits, general.Traits);

        // A general as saves before officers wrote it becomes an officer of the HQ's rank.
        int corpsIndex = save.Units.FindIndex(u => u.Id == corps.Id);
        save.Units[corpsIndex] = save.Units[corpsIndex] with { Officer = null, General = new GeneralSave("Olga Haro", OfficerTrait.Organiser, 2, 1) };
        var old = GameSession.Load(_map, save).UnitById(corps.Id)!.Officer!;
        Assert.Equal(("Olga Haro", OfficerTrait.Organiser, OfficerRank.LieutenantGeneral), (old.Name, old.Traits.Single(), old.Rank));

        for (int i = 0; i < save.Units.Count; i++) save.Units[i] = save.Units[i] with { Officer = null, General = null };
        Assert.NotNull(GameSession.Load(_map, save).UnitById(corps.Id)!.Officer);
    }
}
