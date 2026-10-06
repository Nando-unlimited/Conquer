using Conquer.Game.Buildings;
using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.World;

namespace Conquer.Game.Simulation;

/// <summary>
/// Fleets: ships built in ports that sail the sea as far as their nation can navigate, carry troops in
/// their transports and fight the fleets of enemies they meet, hour by hour, until one side breaks.
/// </summary>
public sealed partial class GameSession
{
    /// <summary>The numbering key for fleets, apart from the levels of the chain of command.</summary>
    private const int FleetNumbering = -2;

    /// <summary>Puts a new fleet of the given ships on the map, fully crewed (tests and training).</summary>
    internal Unit AddFleet(int ownerId, int provinceId, params BattalionType[] ships)
    {
        var fleet = AddUnit(ownerId, UnitType.Fleet, provinceId, 0, NextUnitNumber(ownerId, FleetNumbering));
        foreach (var type in ships) fleet.Ships.Add(NewBattalion(Players[ownerId], type));
        return fleet;
    }

    /// <summary>The units a fleet is carrying.</summary>
    public IEnumerable<Unit> CargoOf(Unit fleet) => Units.Where(u => u.CarrierId == fleet.Id);

    /// <summary>Men aboard a fleet, out of its <see cref="Unit.Capacity"/>.</summary>
    public int CargoMen(Unit fleet) => CargoOf(fleet).Sum(u => u.Citizens);

    /// <summary>A city of the player's with a port building, not held by the enemy: where its ships are built, anchor and are repaired.</summary>
    public bool IsPort(Province p, int playerId) =>
        p.Buildings.Contains(BuildingType.Port) && p.CityId.HasValue && p.OwnerId == playerId && !p.IsOccupied;

    /// <summary>Where a fleet may go: the sea its nation can navigate, and its own ports.</summary>
    private bool CanFleetEnter(Unit fleet, Province p) => p.IsWater ? CanSail(fleet.Owner, p) : IsPort(p, fleet.OwnerId);

    /// <summary>Everything aboard goes wherever the fleet is.</summary>
    private void SyncCargo(Unit fleet)
    {
        foreach (var unit in CargoOf(fleet)) unit.ProvinceId = fleet.ProvinceId;
    }

    // ------------------------------------------------------------------ embarking

    public CommandResult CanEmbark(Unit unit, Unit fleet)
    {
        if (unit.IsFleet || unit.IsAboard) return CommandResult.Fail("Esta unidad no puede embarcar.");
        if (!fleet.IsFleet || fleet.OwnerId != unit.OwnerId) return CommandResult.Fail("Flota no válida.");
        if (fleet.Capacity == 0) return CommandResult.Fail($"{fleet.Name} no tiene barcos que lleven tropas.");
        if (unit.AttackingProvinceId.HasValue || InBattle(unit)) return CommandResult.Fail("Está combatiendo.");
        bool nextTo = fleet.ProvinceId == unit.ProvinceId || Map.Provinces[unit.ProvinceId].Neighbors.Contains(fleet.ProvinceId);
        if (!nextTo || fleet.IsMoving) return CommandResult.Fail($"{fleet.Name} tiene que estar quieta en esta provincia o en el mar de al lado.");
        int room = fleet.Capacity - CargoMen(fleet);
        if (unit.Citizens > room) return CommandResult.Fail($"No cabe: {fleet.Name} tiene sitio para {room:N0} hombres más.");
        return CommandResult.Success();
    }

    /// <summary>The unit goes aboard the fleet and will travel with it.</summary>
    public CommandResult Embark(int playerId, int unitId, int fleetId)
    {
        if (UnitById(unitId) is not { } unit || unit.OwnerId != playerId || UnitById(fleetId) is not { } fleet) return CommandResult.Fail("Unidad no válida.");
        var check = CanEmbark(unit, fleet);
        if (!check.Ok) return check;
        unit.Path.Clear();
        unit.StepHours = unit.HoursToNext = 0;
        unit.CarrierId = fleet.Id;
        unit.ProvinceId = fleet.ProvinceId;
        return CommandResult.Success($"{unit.Name} embarca en {fleet.Name}.");
    }

    /// <summary>
    /// A unit aboard lands in the fleet's port or on a coast next to the fleet. Enemy land without troops
    /// is occupied at once; a coast held by enemy troops cannot be landed on.
    /// </summary>
    public CommandResult Disembark(int playerId, int unitId, int provinceId)
    {
        if (UnitById(unitId) is not { } unit || unit.OwnerId != playerId || unit.CarrierId is not int carrier || UnitById(carrier) is not { } fleet)
            return CommandResult.Fail("Unidad no válida.");
        var p = Map.Provinces[provinceId];
        if (p.IsWater) return CommandResult.Fail("Solo se desembarca en tierra.");
        if (provinceId != fleet.ProvinceId && !Map.Provinces[fleet.ProvinceId].Neighbors.Contains(provinceId))
            return CommandResult.Fail($"Solo en la costa junto a {fleet.Name}.");
        if (fleet.IsMoving) return CommandResult.Fail($"Espera a que {fleet.Name} se detenga.");
        if (EnemyRegimentsIn(provinceId, playerId).Any()) return CommandResult.Fail("Hay tropas enemigas en esa costa: desembarca en otra provincia.");
        unit.CarrierId = null;
        if (!CanUnitEnter(unit, provinceId))
        {
            unit.CarrierId = fleet.Id;
            return CommandResult.Fail(unit.IsMilitary ? $"No estás en guerra con {Players[p.ControllerId].Name}." : $"No puede entrar en tierras de {Players[p.ControllerId].Name}.");
        }
        EnterProvince(unit, provinceId);
        return CommandResult.Success($"{unit.Name} desembarca en {PlaceName(p)}.");
    }

    // ------------------------------------------------------------------ battles at sea

    public IEnumerable<Unit> EnemyFleetsIn(int provinceId, int playerId) =>
        Units.Where(u => u.IsFleet && u.ProvinceId == provinceId && AtWar(u.OwnerId, playerId));

    /// <summary>Sea provinces where fleets of nations at war meet: they fight there every hour.</summary>
    public IEnumerable<int> NavalBattleProvinces() =>
        Units.Where(u => u.IsFleet && Map.Provinces[u.ProvinceId].IsWater).GroupBy(u => u.ProvinceId)
            .Where(g => g.Any(a => g.Any(b => AtWar(a.OwnerId, b.OwnerId)))).Select(g => g.Key);

    /// <summary>
    /// One hour of every battle at sea. Each nation's ships fire on all its enemies' ships there, with
    /// all the fire worked out before any lands; broken fleets flee to open water or sink.
    /// </summary>
    private void ResolveNavalBattles()
    {
        foreach (int provinceId in NavalBattleProvinces().ToList())
        {
            var fleets = Units.Where(u => u.IsFleet && u.ProvinceId == provinceId).ToList();
            var fire = fleets.GroupBy(f => f.OwnerId).ToDictionary(g => g.Key, g => g.Sum(NavalFire));
            foreach (var (owner, shots) in fire)
                Damage([.. fleets.Where(f => AtWar(f.OwnerId, owner))], shots);
            foreach (var fleet in fleets.Where(Broken))
            {
                var victors = fleets.Where(f => AtWar(f.OwnerId, fleet.OwnerId) && !Broken(f)).ToList();
                foreach (var officer in victors.Select(f => f.Officer).OfType<Officer>()) officer.Victories++;
                foreach (int winner in victors.Select(f => f.OwnerId).Distinct())
                    RecordVictory(winner, fleet.OwnerId);
                FleeOrSink(fleet);
            }
        }
    }

    /// <summary>A fleet's fire in an hour: its ships' guns, scaled by their crews and organisation, and a little luck.</summary>
    private double NavalFire(Unit fleet) => ExpectedNavalFire(fleet) * (1 + (_random.NextDouble() * 2 - 1) * MilitaryRules.CombatRandomness);

    /// <summary>A fleet's fire in an hour before luck: its ships' guns, scaled by their crews and organisation, and by its officer's skill and traits.</summary>
    public static double ExpectedNavalFire(Unit fleet) =>
        fleet.Battalions.Sum(b => b.Info.Attack * b.StrengthShare * (0.5 + 0.5 * b.OrganisationShare)) * Math.Max(0, 1 + (fleet.Officer?.NavalFireBonus ?? 0));

    /// <summary>
    /// The fleet runs to a neighbouring sea (or its own port) with no enemy ships; with nowhere to go, or no
    /// crew left, it sinks with everything aboard.
    /// </summary>
    private void FleeOrSink(Unit fleet)
    {
        fleet.Path.Clear();
        fleet.StepHours = fleet.HoursToNext = 0;
        var refuge = Map.Provinces[fleet.ProvinceId].Neighbors
            .Where(n => CanUnitEnter(fleet, n) && !EnemyFleetsIn(n, fleet.OwnerId).Any())
            .Select(n => (int?)n).FirstOrDefault();
        if (refuge is int to && fleet.Citizens >= 1)
        {
            fleet.ProvinceId = to;
            SyncCargo(fleet);
            if (fleet.OwnerId == HumanPlayerId) Notify(HumanPlayerId, $"{fleet.Name} se retira a {Map.Provinces[to].DisplayName}.");
            return;
        }
        Sink(fleet);
    }

    /// <summary>The fleet goes down, and the troops aboard with it.</summary>
    private void Sink(Unit fleet)
    {
        foreach (var unit in CargoOf(fleet).ToList()) Destroy(unit, $"hundida con {fleet.Name}");
        Destroy(fleet, "hundida");
    }

    /// <summary>Tells the human when their fleet runs into an enemy one, or an enemy fleet into theirs.</summary>
    private void NotifyNavalEncounter(Unit fleet, int provinceId)
    {
        var enemies = EnemyFleetsIn(provinceId, fleet.OwnerId).ToList();
        if (enemies.Count == 0) return;
        string place = Map.Provinces[provinceId].DisplayName;
        if (fleet.OwnerId == HumanPlayerId) Notify(HumanPlayerId, $"{fleet.Name} se enfrenta a la flota de {enemies[0].Owner.Name} en {place}.");
        else if (enemies.Any(e => e.OwnerId == HumanPlayerId)) Notify(HumanPlayerId, $"La flota de {fleet.Owner.Name} ataca a la nuestra en {place}.");
    }
}
