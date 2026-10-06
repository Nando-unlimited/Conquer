using Conquer.Game.Buildings;
using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.World;

namespace Conquer.Game.Simulation;

/// <summary>
/// The air force, as in Hearts of Iron: wings of ten planes based at airfields (up to <see cref="MilitaryRules.WingsPerAirfield"/>)
/// and aircraft carriers (<see cref="MilitaryRules.WingsPerCarrier"/> each). They are formed at an airfield like a
/// battalion (planes from the workshops, crews and gold), are repaired at their base, and fly their missions as far as
/// their range. A wing whose base is lost flies to the nearest airfield with room, or is lost.
/// </summary>
public sealed partial class GameSession
{
    /// <summary>The numbering key for wings, apart from the units'.</summary>
    private const int WingNumbering = -3;

    private readonly List<AirWing> _wings = [];
    private int _nextWingId;

    public IReadOnlyList<AirWing> Wings => _wings;

    public AirWing? WingById(int id) => _wings.FirstOrDefault(w => w.Id == id);

    public IEnumerable<AirWing> WingsAt(Province airfield) => _wings.Where(w => w.BaseProvinceId == airfield.Id);

    public IEnumerable<AirWing> WingsOn(Unit fleet) => _wings.Where(w => w.CarrierId == fleet.Id);

    /// <summary>Where a wing is now: its airfield, or the sea or port its carrier is in.</summary>
    public Province BaseOf(AirWing wing) =>
        Map.Provinces[wing.BaseProvinceId ?? (UnitById(wing.CarrierId ?? -1)?.ProvinceId ?? 0)];

    /// <summary>Wings an airfield of the nation's can take: none if it is not one.</summary>
    public int AirfieldRoom(Province p, int playerId) =>
        !p.Has(BuildingType.Airfield) || p.OwnerId != playerId || p.IsOccupied ? 0
        : MilitaryRules.WingsPerAirfield - WingsAt(p).Count() - p.Training.Count(o => o.Battalion is BattalionType t && t.First().Flies);

    /// <summary>Wings a fleet's aircraft carriers can take.</summary>
    public int CarrierRoom(Unit fleet) =>
        fleet.Ships.Count(s => s.Type == BattalionType.AircraftCarrier) * MilitaryRules.WingsPerCarrier - WingsOn(fleet).Count();

    /// <summary>Puts a new wing of the model at an airfield (tests and training).</summary>
    internal AirWing AddWing(int ownerId, int provinceId, BattalionType type, int? model = null)
    {
        var owner = Players[ownerId];
        var wing = new AirWing(_nextWingId++, owner, new Battalion(type, model ?? Math.Max(0, type.BestModel(owner.Techs))), NextUnitNumber(ownerId, WingNumbering))
        {
            BaseProvinceId = provinceId,
        };
        _wings.Add(wing);
        return wing;
    }

    /// <summary>
    /// Moves a wing to another airfield of the nation's with room, or onto a fleet with an aircraft carrier with room
    /// (only fighters, attack and naval aircraft), as far as twice its range from where it is.
    /// </summary>
    public CommandResult Rebase(int playerId, int wingId, int provinceId, int? carrierId = null)
    {
        if (WingById(wingId) is not { } wing || wing.OwnerId != playerId) return CommandResult.Fail("Ala no válida.");
        Province target;
        if (carrierId is int id)
        {
            if (UnitById(id) is not { } fleet || fleet.OwnerId != playerId || !fleet.IsFleet) return CommandResult.Fail("Flota no válida.");
            if (!wing.FitsOnCarrier) return CommandResult.Fail("Solo cazas, aviones de ataque y aviación naval operan desde portaaviones.");
            if (CarrierRoom(fleet) <= 0) return CommandResult.Fail("No hay sitio en sus portaaviones.");
            target = Map.Provinces[fleet.ProvinceId];
        }
        else
        {
            target = Map.Provinces[provinceId];
            if (AirfieldRoom(target, playerId) <= 0) return CommandResult.Fail("Hace falta un aeródromo tuyo con sitio.");
        }
        if (Map.DistanceKm(BaseOf(wing), target) > 2 * wing.Info.RangeKm) return CommandResult.Fail($"Está a más de {2 * wing.Info.RangeKm:N0} km.");
        wing.BaseProvinceId = carrierId is null ? target.Id : null;
        wing.CarrierId = carrierId;
        // A new base: the old mission may be out of reach.
        if (wing.TargetProvinceId is int t && !InRange(wing, Map.Provinces[t])) wing.TargetProvinceId = null;
        return CommandResult.Success($"{wing.Name} se traslada a {(carrierId is int c ? UnitById(c)!.Name : PlaceName(target))}.");
    }

    /// <summary>Whether a province is within the wing's range of its base.</summary>
    public bool InRange(AirWing wing, Province p) => Map.DistanceKm(BaseOf(wing), p) <= wing.Info.RangeKm;

    /// <summary>The wing is stood down: its crews go back to the reserve and its planes to the stockpile.</summary>
    public CommandResult DisbandWing(int playerId, int wingId)
    {
        if (WingById(wingId) is not { } wing || wing.OwnerId != playerId) return CommandResult.Fail("Ala no válida.");
        ReturnToReserve(wing.Owner, (int)Math.Round(wing.Planes.Strength));
        wing.Owner.AddEquipment(wing.Info, wing.PlaneCount);
        _wings.Remove(wing);
        return CommandResult.Success($"{wing.Name} disuelta.");
    }

    /// <summary>
    /// Every day: wings whose base is lost fly to the nation's nearest airfield with room or are lost; at a base in
    /// supply (a carrier, in port) they recover organisation and get back planes and crews from the stockpile and the reserve.
    /// </summary>
    private void DailyWings(Player player)
    {
        foreach (var wing in _wings.Where(w => w.OwnerId == player.Id).ToList())
        {
            bool based = wing.CarrierId is int c
                ? UnitById(c) is { } fleet && fleet.OwnerId == player.Id && CarrierRoom(fleet) >= 0 && fleet.Ships.Any(s => s.Type == BattalionType.AircraftCarrier)
                : AirfieldRoom(Map.Provinces[wing.BaseProvinceId!.Value], player.Id) >= 0 && Map.Provinces[wing.BaseProvinceId.Value].Has(BuildingType.Airfield)
                  && Map.Provinces[wing.BaseProvinceId.Value].OwnerId == player.Id && !Map.Provinces[wing.BaseProvinceId.Value].IsOccupied;
            if (!based && !Relocate(wing)) continue;

            var at = BaseOf(wing);
            bool repairs = wing.CarrierId is int carrier ? IsPort(Map.Provinces[UnitById(carrier)!.ProvinceId], player.Id) : IsSupplied(player.Id, at.Id);
            if (!repairs) continue;
            var b = wing.Planes;
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

    /// <summary>A wing that lost its base flies to the nation's nearest airfield with room; with none, it is lost. False if lost.</summary>
    private bool Relocate(AirWing wing)
    {
        var from = BaseOf(wing);
        var airfield = wing.Owner.Provinces.Select(id => Map.Provinces[id]).Where(p => AirfieldRoom(p, wing.OwnerId) > 0)
            .MinBy(p => Map.DistanceKm(p, from));
        if (airfield == null)
        {
            _wings.Remove(wing);
            if (wing.Owner.IsHuman) Notify(wing.OwnerId, $"{wing.Name} se ha perdido: no le quedaba dónde aterrizar.");
            return false;
        }
        wing.BaseProvinceId = airfield.Id;
        wing.CarrierId = null;
        if (wing.Owner.IsHuman) Notify(wing.OwnerId, $"{wing.Name} ha perdido su base y aterriza en {PlaceName(airfield)}.");
        return true;
    }

    /// <summary>The wings carried by a fleet go down with it.</summary>
    private void LoseCarrierWings(Unit fleet) => _wings.RemoveAll(w => w.CarrierId == fleet.Id);
}
