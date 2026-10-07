using Conquer.Game.Buildings;
using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.World;

namespace Conquer.Game.Simulation;

/// <summary>
/// The air force: escuadrillas of six planes, formed at airfields, that join into escuadrones, grupos and alas
/// (<see cref="AirEchelon"/>). Air units are based at airfields (<see cref="MilitaryRules.FlightsPerAirfield"/>
/// escuadrillas) and on aircraft carriers, are repaired at their base and fly their missions as far as their range.
/// A unit whose base is lost flies to the nearest airfield with room, or is lost. Divisiones aéreas command the units
/// based near them, and the Mando aéreo the divisions.
/// </summary>
public sealed partial class GameSession
{
    private readonly List<AirUnit> _airUnits = [];
    private readonly List<AirHeadquarters> _airHeadquarters = [];
    private int _nextAirUnitId, _nextAirHeadquartersId;

    public IReadOnlyList<AirUnit> AirUnits => _airUnits;
    public IReadOnlyList<AirHeadquarters> AirHeadquarters => _airHeadquarters;

    public AirUnit? AirUnitById(int id) => _airUnits.FirstOrDefault(u => u.Id == id);
    public AirHeadquarters? AirHeadquartersById(int id) => _airHeadquarters.FirstOrDefault(h => h.Id == id);

    public IEnumerable<AirUnit> AirUnitsAt(Province airfield) => _airUnits.Where(u => u.BaseProvinceId == airfield.Id);

    public IEnumerable<AirUnit> AirUnitsOn(Unit fleet) => _airUnits.Where(u => u.CarrierId == fleet.Id);

    /// <summary>Numbering keys for air units of each size and air HQs, apart from the units'.</summary>
    private static int AirNumbering(AirEchelon size) => -10 - (int)size;
    private const int AirDivisionNumbering = -20;

    /// <summary>Where an air unit is now: its airfield, or the sea or port its carrier is in.</summary>
    public Province BaseOf(AirUnit unit) =>
        Map.Provinces[unit.BaseProvinceId ?? (UnitById(unit.CarrierId ?? -1)?.ProvinceId ?? 0)];

    /// <summary>Escuadrillas an airfield of the nation's can still take, counting those being formed; none if it is not one.</summary>
    public int AirfieldRoom(Province p, int playerId) =>
        !p.Has(BuildingType.Airfield) || p.OwnerId != playerId || p.IsOccupied ? 0
        : MilitaryRules.FlightsPerAirfield - AirUnitsAt(p).Sum(u => u.Flights.Count) - p.Training.Count(o => o.Battalion is BattalionType t && t.First().Flies);

    /// <summary>Escuadrillas a fleet's aircraft carriers can still take.</summary>
    public int CarrierRoom(Unit fleet) =>
        fleet.Ships.Count(s => s.Type == BattalionType.AircraftCarrier) * MilitaryRules.FlightsPerCarrier - AirUnitsOn(fleet).Sum(u => u.Flights.Count);

    /// <summary>Puts a new air unit of the given escuadrillas of a kind at an airfield (tests and training).</summary>
    internal AirUnit AddAirUnit(int ownerId, int provinceId, BattalionType type, int flights = 1, int? model = null)
    {
        var owner = Players[ownerId];
        int m = model ?? Math.Max(0, type.BestModel(owner.Techs));
        var unit = new AirUnit(_nextAirUnitId++, owner, Enumerable.Range(0, flights).Select(_ => new Battalion(type, m)),
            NextUnitNumber(ownerId, AirNumbering(AirEchelons.Of(flights)))) { BaseProvinceId = provinceId };
        _airUnits.Add(unit);
        return unit;
    }

    /// <summary>A new number when an air unit changes size, as land units do when they grow into a brigade.</summary>
    private void Renumber(AirUnit unit, AirEchelon before)
    {
        if (unit.Size != before) unit.Number = NextUnitNumber(unit.OwnerId, AirNumbering(unit.Size));
    }

    /// <summary>
    /// Moves an air unit to another airfield of the nation's with room, or onto a fleet with aircraft carriers with room
    /// (only fighters, dive bombers and naval aircraft), as far as twice its range from where it is.
    /// </summary>
    public CommandResult Rebase(int playerId, int unitId, int provinceId, int? carrierId = null)
    {
        if (AirUnitById(unitId) is not { } unit || unit.OwnerId != playerId) return CommandResult.Fail("Unidad aérea no válida.");
        Province target;
        if (carrierId is int id)
        {
            if (UnitById(id) is not { } fleet || fleet.OwnerId != playerId || !fleet.IsFleet) return CommandResult.Fail("Flota no válida.");
            if (!unit.FitsOnCarrier) return CommandResult.Fail("Solo cazas, bombarderos en picado y aviación naval operan desde portaaviones.");
            if (CarrierRoom(fleet) < unit.Flights.Count) return CommandResult.Fail("No hay sitio en sus portaaviones.");
            target = Map.Provinces[fleet.ProvinceId];
        }
        else
        {
            target = Map.Provinces[provinceId];
            if (AirfieldRoom(target, playerId) < unit.Flights.Count) return CommandResult.Fail("Hace falta un aeródromo tuyo con sitio.");
        }
        if (Map.DistanceKm(BaseOf(unit), target) > 2 * unit.Info.RangeKm) return CommandResult.Fail($"Está a más de {2 * unit.Info.RangeKm:N0} km.");
        unit.BaseProvinceId = carrierId is null ? target.Id : null;
        unit.CarrierId = carrierId;
        _flyingAt = -1;
        // A new base: the old mission may be out of reach.
        if (unit.TargetProvinceId is int t && !InRange(unit, Map.Provinces[t])) unit.TargetProvinceId = null;
        return CommandResult.Success($"{unit.Name} se traslada a {(carrierId is int c ? UnitById(c)!.Name : PlaceName(target))}.");
    }

    /// <summary>Whether a province is within the unit's range of its base (that of its shortest-ranged escuadrilla).</summary>
    public bool InRange(AirUnit unit, Province p) => Map.DistanceKm(BaseOf(unit), p) <= unit.Flights.Min(f => f.Info.RangeKm);

    /// <summary>Whether two air units can join: the same nation, kind and base, and no more than an ala together.</summary>
    public CommandResult CanJoin(AirUnit unit, AirUnit other)
    {
        if (unit == other || unit.OwnerId != other.OwnerId) return CommandResult.Fail("Unidad aérea no válida.");
        if (unit.Type != other.Type) return CommandResult.Fail("Solo se unen aviones del mismo tipo.");
        if (unit.BaseProvinceId != other.BaseProvinceId || unit.CarrierId != other.CarrierId) return CommandResult.Fail("Tienen que estar en la misma base.");
        if (unit.Flights.Count + other.Flights.Count > AirEchelons.FlightsPerWing)
            return CommandResult.Fail($"Un ala tiene como mucho {AirEchelons.FlightsPerWing} escuadrillas.");
        return CommandResult.Success();
    }

    /// <summary>The other unit's escuadrillas join this one, which grows into an escuadrón, a grupo or an ala, and the other is gone.</summary>
    public CommandResult JoinAirUnits(int playerId, int unitId, int otherId)
    {
        if (AirUnitById(unitId) is not { } unit || unit.OwnerId != playerId || AirUnitById(otherId) is not { } other) return CommandResult.Fail("Unidad aérea no válida.");
        var check = CanJoin(unit, other);
        if (!check.Ok) return check;
        var before = unit.Size;
        unit.Flights.AddRange(other.Flights);
        unit.CommanderId ??= other.CommanderId;
        _airUnits.Remove(other);
        Renumber(unit, before);
        _flyingAt = -1;
        return CommandResult.Success($"{other.Name} se une: ahora es la unidad {unit.Name}.");
    }

    /// <summary>Takes this many escuadrillas out of a unit as a new one at the same base, with the same mission.</summary>
    public CommandResult SplitAirUnit(int playerId, int unitId, int flights)
    {
        if (AirUnitById(unitId) is not { } unit || unit.OwnerId != playerId) return CommandResult.Fail("Unidad aérea no válida.");
        if (flights < 1 || flights >= unit.Flights.Count) return CommandResult.Fail("Tiene que quedar al menos una escuadrilla en cada parte.");
        var before = unit.Size;
        var leaving = unit.Flights.Skip(unit.Flights.Count - flights).ToList();
        unit.Flights.RemoveRange(unit.Flights.Count - flights, flights);
        var part = new AirUnit(_nextAirUnitId++, unit.Owner, leaving, NextUnitNumber(playerId, AirNumbering(AirEchelons.Of(flights))))
        {
            BaseProvinceId = unit.BaseProvinceId, CarrierId = unit.CarrierId, Mission = unit.Mission, TargetProvinceId = unit.TargetProvinceId,
        };
        _airUnits.Add(part);
        Renumber(unit, before);
        _flyingAt = -1;
        return CommandResult.Success($"{part.Name} se separa de {unit.Name}.");
    }

    /// <summary>The unit is stood down: its crews go back to the reserve and its planes to the stockpile.</summary>
    public CommandResult DisbandAirUnit(int playerId, int unitId)
    {
        if (AirUnitById(unitId) is not { } unit || unit.OwnerId != playerId) return CommandResult.Fail("Unidad aérea no válida.");
        ReturnToReserve(unit.Owner, unit.Crews);
        foreach (var f in unit.Flights) unit.Owner.AddEquipment(f.Info, f.StrengthShare * f.Info.Pieces);
        _airUnits.Remove(unit);
        _flyingAt = -1;
        return CommandResult.Success($"{unit.Name} disuelta.");
    }

    // ------------------------------------------------------------------ air HQs

    /// <summary>The nation's Mando aéreo, if it has one.</summary>
    public AirHeadquarters? AirCommandOf(Player player) => _airHeadquarters.FirstOrDefault(h => h.OwnerId == player.Id && h.IsCommand);

    public AirHeadquarters? DivisionOf(AirUnit unit) => unit.CommanderId is int id ? AirHeadquartersById(id) : null;

    public IEnumerable<AirUnit> UnitsOf(AirHeadquarters division) => _airUnits.Where(u => u.CommanderId == division.Id);

    /// <summary>
    /// Whether an air HQ can be formed at the province: an airfield of the nation's with the staff to spare, and the
    /// gold; the Mando aéreo only once.
    /// </summary>
    public CommandResult CanRaiseAirHeadquarters(Province p, int level)
    {
        var player = Players[p.OwnerId < 0 ? 0 : p.OwnerId];
        if (!p.Has(BuildingType.Airfield) || p.OwnerId < 0 || p.IsOccupied) return CommandResult.Fail("Se forma en uno de tus aeródromos.");
        if (level == 2 && AirCommandOf(player) != null) return CommandResult.Fail("Ya tienes un Mando aéreo.");
        if (p.Population - MilitaryRules.AirHeadquartersStaff < MinimumPopulation(p)) return CommandResult.Fail($"Hacen falta {MilitaryRules.AirHeadquartersStaff} hombres de la provincia.");
        if (LacksManpower(player, MilitaryRules.AirHeadquartersStaff) is { } lack) return lack;
        var cost = AirHeadquartersCost(level);
        if (!player.Stockpile.Has(cost)) return CommandResult.Fail($"Cuesta {cost}.");
        return CommandResult.Success();
    }

    public static Economy.ResourceCost AirHeadquartersCost(int level) => new((Economy.ResourceType.Gold, level == 2 ? 200 : 100));

    /// <summary>Forms a División aérea (level 1) or the Mando aéreo (2) at an airfield, with a general of the air force.</summary>
    public CommandResult RaiseAirHeadquarters(int playerId, int provinceId, int level)
    {
        var p = Map.Provinces[provinceId];
        if (p.OwnerId != playerId) return CommandResult.Fail("La provincia no es tuya.");
        var check = CanRaiseAirHeadquarters(p, level);
        if (!check.Ok) return check;
        var player = Players[playerId];
        player.Stockpile.TrySpend(AirHeadquartersCost(level));
        p.Population -= MilitaryRules.AirHeadquartersStaff;
        player.Manpower -= MilitaryRules.AirHeadquartersStaff;
        var hq = new AirHeadquarters(_nextAirHeadquartersId++, player, level, level == 2 ? 1 : NextUnitNumber(playerId, AirDivisionNumbering), provinceId)
        {
            Officer = NewOfficer(player, level == 2 ? OfficerRank.Marshal : OfficerRank.LieutenantGeneral, OfficerBranch.Air),
        };
        _airHeadquarters.Add(hq);
        return CommandResult.Success($"{hq.Name} formada en {PlaceName(p)}.");
    }

    /// <summary>Puts an air unit under a División aérea of the nation's (with room for it), or takes it out of its own with null.</summary>
    public CommandResult AttachAirUnit(int playerId, int unitId, int? divisionId)
    {
        if (AirUnitById(unitId) is not { } unit || unit.OwnerId != playerId) return CommandResult.Fail("Unidad aérea no válida.");
        if (divisionId is int id)
        {
            if (AirHeadquartersById(id) is not { } hq || hq.OwnerId != playerId || hq.IsCommand) return CommandResult.Fail("División aérea no válida.");
            if (UnitsOf(hq).Count() >= MilitaryRules.MaxUnitsPerAirDivision && unit.CommanderId != id)
                return CommandResult.Fail($"Una división aérea manda {MilitaryRules.MaxUnitsPerAirDivision} unidades como mucho.");
            unit.CommanderId = id;
            return CommandResult.Success($"{unit.Name} pasa a la {hq.Name}.");
        }
        unit.CommanderId = null;
        return CommandResult.Success($"{unit.Name} sin división aérea.");
    }

    /// <summary>Disbands an air HQ: its units are left unattached, its staff go back to the reserve and its general to the officers' reserve.</summary>
    public CommandResult DisbandAirHeadquarters(int playerId, int hqId)
    {
        if (AirHeadquartersById(hqId) is not { } hq || hq.OwnerId != playerId) return CommandResult.Fail("Cuartel general no válido.");
        foreach (var unit in UnitsOf(hq)) unit.CommanderId = null;
        ReturnToReserve(hq.Owner, MilitaryRules.AirHeadquartersStaff);
        if (hq.Officer is { } officer) hq.Owner.OfficerReserve.Add(officer);
        _airHeadquarters.Remove(hq);
        return CommandResult.Success($"{hq.Name} disuelta.");
    }

    /// <summary>Whether an air unit's División aérea commands it: based within its range.</summary>
    public bool InAirCommand(AirUnit unit) =>
        DivisionOf(unit) is { } hq && Map.DistanceKm(BaseOf(unit), Map.Provinces[hq.BaseProvinceId]) <= MilitaryRules.AirDivisionRangeKm;

    /// <summary>
    /// What an air unit gets from its chain of command: its División aérea in range and its general, and the Mando aéreo
    /// if it is within reach of the division.
    /// </summary>
    public double AirCommandBonus(AirUnit unit)
    {
        if (!InAirCommand(unit)) return 0;
        var division = DivisionOf(unit)!;
        double bonus = MilitaryRules.AirCommandBonus + (division.Officer?.NavalFireBonus ?? 0);
        if (AirCommandOf(unit.Owner) is { } command
            && Map.DistanceKm(Map.Provinces[division.BaseProvinceId], Map.Provinces[command.BaseProvinceId]) <= MilitaryRules.AirCommandRangeKm)
            bonus += MilitaryRules.HigherAirCommandBonus;
        return bonus;
    }

    // ------------------------------------------------------------------ every day

    /// <summary>
    /// Every day: air units and HQs whose base is lost fly to the nation's nearest airfield with room (or are lost); at a
    /// base in supply (a carrier, in port) the escuadrillas recover organisation and get back planes and crews from the
    /// stockpile and the reserve.
    /// </summary>
    private void DailyAirUnits(Player player)
    {
        foreach (var unit in _airUnits.Where(u => u.OwnerId == player.Id).ToList())
        {
            bool based = unit.CarrierId is int c
                ? UnitById(c) is { } fleet && fleet.OwnerId == player.Id && fleet.Ships.Any(s => s.Type == BattalionType.AircraftCarrier) && CarrierRoom(fleet) >= 0
                : IsOwnAirfield(Map.Provinces[unit.BaseProvinceId!.Value], player.Id) && AirfieldRoom(Map.Provinces[unit.BaseProvinceId.Value], player.Id) >= 0;
            if (!based && !Relocate(unit)) continue;

            var at = BaseOf(unit);
            bool repairs = unit.CarrierId is int carrier ? IsPort(Map.Provinces[UnitById(carrier)!.ProvinceId], player.Id) : IsSupplied(player.Id, at.Id);
            if (!repairs) continue;
            foreach (var b in unit.Flights)
            {
                b.Organisation = Math.Min(b.Info.MaxOrganisation, b.Organisation + b.Info.MaxOrganisation * MilitaryRules.OrganisationRecovery);
                double men = Math.Min(Math.Min(b.Info.Men - b.Strength, b.Info.Men * MilitaryRules.ReinforcementRate * 2), player.Manpower);
                men = Math.Min(men, player.EquipmentOf(b.Info) / b.Info.Pieces * b.Info.Men);
                if (men <= 0) continue;
                player.AddEquipment(b.Info, -men / b.Info.Men * b.Info.Pieces);
                player.Manpower -= men;
                b.Experience = b.Experience * b.Strength / (b.Strength + men);
                b.Strength += men;
            }
        }
        foreach (var hq in _airHeadquarters.Where(h => h.OwnerId == player.Id && !IsOwnAirfield(Map.Provinces[h.BaseProvinceId], player.Id)).ToList())
        {
            var airfield = player.Provinces.Select(id => Map.Provinces[id]).Where(p => IsOwnAirfield(p, player.Id))
                .MinBy(p => Map.DistanceKm(p, Map.Provinces[hq.BaseProvinceId]));
            if (airfield != null) hq.BaseProvinceId = airfield.Id;
            else DisbandAirHeadquarters(player.Id, hq.Id);
        }
    }

    private static bool IsOwnAirfield(Province p, int playerId) => p.Has(BuildingType.Airfield) && p.OwnerId == playerId && !p.IsOccupied;

    /// <summary>A unit that lost its base flies to the nation's nearest airfield with room for it; with none, it is lost. False if lost.</summary>
    private bool Relocate(AirUnit unit)
    {
        var from = BaseOf(unit);
        var airfield = unit.Owner.Provinces.Select(id => Map.Provinces[id]).Where(p => AirfieldRoom(p, unit.OwnerId) >= unit.Flights.Count && p.Id != unit.BaseProvinceId)
            .MinBy(p => Map.DistanceKm(p, from));
        _flyingAt = -1;
        if (airfield == null)
        {
            _airUnits.Remove(unit);
            if (unit.Owner.IsHuman) Notify(unit.OwnerId, $"{unit.Name} se ha perdido: no le quedaba dónde aterrizar.");
            return false;
        }
        unit.BaseProvinceId = airfield.Id;
        unit.CarrierId = null;
        if (unit.Owner.IsHuman) Notify(unit.OwnerId, $"{unit.Name} ha perdido su base y aterriza en {PlaceName(airfield)}.");
        return true;
    }

    /// <summary>The air units carried by a fleet go down with it.</summary>
    private void LoseCarrierAirUnits(Unit fleet)
    {
        _airUnits.RemoveAll(u => u.CarrierId == fleet.Id);
        _flyingAt = -1;
    }

    /// <summary>
    /// Saves from before the air force kept aircraft in regiments: each such battalion becomes an escuadrilla at its
    /// nation's nearest airfield with room (the capital gets one if the nation has none); with no room anywhere its planes
    /// go to the stockpile and its crews to the reserve. Regiments left empty are gone.
    /// </summary>
    private void TurnFlyingBattalionsIntoAirUnits()
    {
        foreach (var unit in Units.Where(u => u.IsMilitary && u.Battalions.Any(b => b.Info.Flies)).ToList())
        {
            var owner = unit.Owner;
            var from = Map.Provinces[unit.ProvinceId];
            foreach (var regiment in unit.Regiments.Concat(unit.Brigades.SelectMany(b => b.Regiments)))
                foreach (var b in regiment.Battalions.Where(b => b.Info.Flies).ToList())
                {
                    regiment.Battalions.Remove(b);
                    if (!owner.Provinces.Any(id => Map.Provinces[id].Has(BuildingType.Airfield))
                        && CapitalProvince(owner) is { } capital) capital.AddBuilding(BuildingType.Airfield);
                    var airfield = owner.Provinces.Select(id => Map.Provinces[id]).Where(p => AirfieldRoom(p, owner.Id) > 0).MinBy(p => Map.DistanceKm(p, from));
                    if (airfield == null)
                    {
                        owner.AddEquipment(b.Info, b.StrengthShare * b.Info.Pieces);
                        ReturnToReserve(owner, b.Strength);
                        continue;
                    }
                    var air = new AirUnit(_nextAirUnitId++, owner, [b], NextUnitNumber(owner.Id, AirNumbering(AirEchelon.Flight))) { BaseProvinceId = airfield.Id };
                    _airUnits.Add(air);
                    if (owner.IsHuman) Notify(owner.Id, $"Tus bombarderos de {unit.Name} forman ahora la {air.Name} en {PlaceName(airfield)}.");
                }
            foreach (var brigade in unit.Brigades) brigade.Regiments.RemoveAll(r => r.Battalions.Count == 0);
            unit.Brigades.RemoveAll(b => b.Regiments.Count == 0);
            if (unit.Regiments.Count > 1) unit.Regiments.RemoveAll(r => r.Battalions.Count == 0);
            if (unit.Battalions.Count == 0) RemoveUnit(unit);
        }
    }
}
