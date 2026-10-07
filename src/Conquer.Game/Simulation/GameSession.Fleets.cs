using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.World;

namespace Conquer.Game.Simulation;

/// <summary>
/// The navy's chain of command and its fuel. Fleets grow from flotillas (up to 3 ships) into escuadras (9) and fuerzas
/// (27); Flotas command the fleets near their port and the Armada the flotas, each with an admiral. Steam ships burn coal
/// and modern ones oil every day at sea; without it a fleet crawls and fights at half strength. Aircraft burn oil on
/// the days they fly.
/// </summary>
public sealed partial class GameSession
{
    private readonly List<NavalHeadquarters> _navalHeadquarters = [];
    private int _nextNavalHeadquartersId;

    public IReadOnlyList<NavalHeadquarters> NavalHeadquarters => _navalHeadquarters;

    public NavalHeadquarters? NavalHeadquartersById(int id) => _navalHeadquarters.FirstOrDefault(h => h.Id == id);

    /// <summary>The numbering key for fleets of each size, apart from the units' (1.ª Flotilla, 1.ª Escuadra…).</summary>
    private static int FleetNumbering(NavalEchelon size) => -30 - (int)size;

    /// <summary>A new number when a fleet changes size, as land units do when they grow into a brigade.</summary>
    private void RenumberFleet(Unit fleet, NavalEchelon before)
    {
        var now = NavalEchelons.Of(fleet.Ships.Count);
        if (now != before) fleet.Number = NextUnitNumber(fleet.OwnerId, FleetNumbering(now));
    }

    /// <summary>The nation's Armada, if it has one.</summary>
    public NavalHeadquarters? NavyOf(Player player) => _navalHeadquarters.FirstOrDefault(h => h.OwnerId == player.Id && h.IsNavy);

    public NavalHeadquarters? FlotaOf(Unit fleet) => fleet.FleetCommanderId is int id ? NavalHeadquartersById(id) : null;

    public IEnumerable<Unit> FleetsOf(NavalHeadquarters flota) => Units.Where(u => u.IsFleet && u.FleetCommanderId == flota.Id);

    public static Economy.ResourceCost NavalHeadquartersCost(int level) => new((Economy.ResourceType.Gold, level == 2 ? 300 : 150));

    /// <summary>Whether a naval HQ can be formed at the province: one of the nation's ports with the staff to spare, and the gold; the Armada only once.</summary>
    public CommandResult CanRaiseNavalHeadquarters(Province p, int level)
    {
        if (p.OwnerId < 0 || !IsPort(p, p.OwnerId)) return CommandResult.Fail("Se forma en uno de tus puertos.");
        var player = Players[p.OwnerId];
        if (level == 2 && NavyOf(player) != null) return CommandResult.Fail("Ya tienes una Armada.");
        if (p.Population - MilitaryRules.NavalHeadquartersStaff < MinimumPopulation(p)) return CommandResult.Fail($"Hacen falta {MilitaryRules.NavalHeadquartersStaff} hombres de la provincia.");
        if (LacksManpower(player, MilitaryRules.NavalHeadquartersStaff) is { } lack) return lack;
        var cost = NavalHeadquartersCost(level);
        if (!player.Stockpile.Has(cost)) return CommandResult.Fail($"Cuesta {cost}.");
        return CommandResult.Success();
    }

    /// <summary>Forms a Flota (level 1) or the Armada (2) in a port, with an admiral.</summary>
    public CommandResult RaiseNavalHeadquarters(int playerId, int provinceId, int level)
    {
        var p = Map.Provinces[provinceId];
        if (p.OwnerId != playerId) return CommandResult.Fail("La provincia no es tuya.");
        var check = CanRaiseNavalHeadquarters(p, level);
        if (!check.Ok) return check;
        var player = Players[playerId];
        player.Stockpile.TrySpend(NavalHeadquartersCost(level));
        p.Population -= MilitaryRules.NavalHeadquartersStaff;
        player.Manpower -= MilitaryRules.NavalHeadquartersStaff;
        var hq = new NavalHeadquarters(_nextNavalHeadquartersId++, player, level, level == 2 ? 1 : NextUnitNumber(playerId, FleetNumbering(NavalEchelon.Force) - 10), provinceId)
        {
            Officer = NewOfficer(player, level == 2 ? OfficerRank.Marshal : OfficerRank.LieutenantGeneral, OfficerBranch.Navy),
        };
        _navalHeadquarters.Add(hq);
        return CommandResult.Success($"{hq.Name} formada en {PlaceName(p)}.");
    }

    /// <summary>Puts a fleet under a Flota of the nation's (with room for it), or takes it out with null.</summary>
    public CommandResult AttachFleet(int playerId, int fleetId, int? flotaId)
    {
        if (UnitById(fleetId) is not { } fleet || fleet.OwnerId != playerId || !fleet.IsFleet) return CommandResult.Fail("Flota no válida.");
        if (flotaId is int id)
        {
            if (NavalHeadquartersById(id) is not { } hq || hq.OwnerId != playerId || hq.IsNavy) return CommandResult.Fail("Flota (cuartel general) no válida.");
            if (FleetsOf(hq).Count() >= MilitaryRules.MaxFleetsPerFlota && fleet.FleetCommanderId != id)
                return CommandResult.Fail($"Una flota manda {MilitaryRules.MaxFleetsPerFlota} agrupaciones como mucho.");
            fleet.FleetCommanderId = id;
            return CommandResult.Success($"{fleet.Name} pasa a la {hq.Name}.");
        }
        fleet.FleetCommanderId = null;
        return CommandResult.Success($"{fleet.Name} sin flota.");
    }

    /// <summary>Disbands a naval HQ: its fleets are left unattached, its staff go back to the reserve and its admiral to the officers' reserve.</summary>
    public CommandResult DisbandNavalHeadquarters(int playerId, int hqId)
    {
        if (NavalHeadquartersById(hqId) is not { } hq || hq.OwnerId != playerId) return CommandResult.Fail("Cuartel general no válido.");
        foreach (var fleet in FleetsOf(hq)) fleet.FleetCommanderId = null;
        ReturnToReserve(hq.Owner, MilitaryRules.NavalHeadquartersStaff);
        if (hq.Officer is { } officer) hq.Owner.OfficerReserve.Add(officer);
        _navalHeadquarters.Remove(hq);
        return CommandResult.Success($"{hq.Name} disuelta.");
    }

    /// <summary>Whether a fleet's Flota commands it: the fleet within its range of the Flota's port.</summary>
    public bool InFleetCommand(Unit fleet) =>
        FlotaOf(fleet) is { } hq && Map.DistanceKm(Map.Provinces[fleet.ProvinceId], Map.Provinces[hq.BaseProvinceId]) <= MilitaryRules.FleetCommandRangeKm;

    /// <summary>What a fleet gets from its chain of command: its Flota in range and its admiral, and the Armada if within reach of the Flota.</summary>
    public double FleetCommandBonus(Unit fleet)
    {
        if (!InFleetCommand(fleet)) return 0;
        var flota = FlotaOf(fleet)!;
        double bonus = MilitaryRules.FleetCommandBonus + (flota.Officer?.NavalFireBonus ?? 0);
        if (NavyOf(fleet.Owner) is { } navy
            && Map.DistanceKm(Map.Provinces[flota.BaseProvinceId], Map.Provinces[navy.BaseProvinceId]) <= MilitaryRules.NavyCommandRangeKm)
            bonus += MilitaryRules.HigherFleetCommandBonus;
        return bonus;
    }

    /// <summary>
    /// Every day each fleet at sea burns its ships' coal and oil; one the stores cannot pay is out of fuel until it gets
    /// some (in port it always refuels). Naval HQs whose port is lost move to the nation's nearest port, or are disbanded.
    /// </summary>
    private void DailyFleets(Player player)
    {
        foreach (var fleet in Units.Where(u => u.OwnerId == player.Id && u.IsFleet))
        {
            if (IsPort(Map.Provinces[fleet.ProvinceId], player.Id))
            {
                fleet.OutOfFuel = false;
                continue;
            }
            var need = fleet.Ships.Where(s => s.Info.Fuel != null).GroupBy(s => s.Info.Fuel!.Value)
                .Select(g => (Type: g.Key, Amount: g.Sum(s => s.Info.FuelPerDay * s.StrengthShare))).ToList();
            fleet.OutOfFuel = need.Any(n => player.Stockpile[n.Type] < n.Amount);
            if (fleet.OutOfFuel) continue;
            foreach (var (type, amount) in need)
            {
                player.Stockpile[type] -= amount;
                player.Record(Economy.ResourceFlow.Upkeep, type, -amount);
            }
        }
        foreach (var hq in _navalHeadquarters.Where(h => h.OwnerId == player.Id && !IsPort(Map.Provinces[h.BaseProvinceId], player.Id)).ToList())
        {
            var port = player.Provinces.Select(id => Map.Provinces[id]).Where(p => IsPort(p, player.Id)).MinBy(p => Map.DistanceKm(p, Map.Provinces[hq.BaseProvinceId]));
            if (port != null) hq.BaseProvinceId = port.Id;
            else DisbandNavalHeadquarters(player.Id, hq.Id);
        }
    }

    /// <summary>The air units with a mission burn their oil for the day; those the stores cannot pay stay on the ground.</summary>
    private void FuelAircraft()
    {
        foreach (var unit in _airUnits)
        {
            unit.Grounded = false;
            if (unit.Mission is AirMission.None or AirMission.Paradrop) continue;
            double oil = MilitaryRules.OilPerFlightDay * unit.Flights.Count;
            var stock = unit.Owner.Stockpile;
            if (stock[Economy.ResourceType.Oil] < oil)
            {
                unit.Grounded = true;
                continue;
            }
            stock[Economy.ResourceType.Oil] -= oil;
            unit.Owner.Record(Economy.ResourceFlow.Upkeep, Economy.ResourceType.Oil, -oil);
        }
        _flyingAt = -1;
    }
}
