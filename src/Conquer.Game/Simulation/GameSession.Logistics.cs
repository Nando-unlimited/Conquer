using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.World;

namespace Conquer.Game.Simulation;

/// <summary>
/// Something on its way from the capital to a combat unit: recruits, the pieces of equipment they and the battalions
/// taking up a newer model need (by <see cref="BattalionInfo.SupplyKey"/>) and ammunition (suministros).
/// </summary>
public sealed class Shipment
{
    public required int OwnerId { get; init; }
    public required int UnitId { get; init; }
    public double Men { get; set; }
    public Dictionary<string, double> Pieces { get; init; } = [];
    public double Ammo { get; set; }
    public required long ArriveHours { get; init; }
    /// <summary>The seas it crosses, in order; empty by land.</summary>
    public List<int> SeaRoute { get; init; } = [];
    /// <summary>Convoys carrying it over the sea, kept from other shipments until it arrives.</summary>
    public double Convoys { get; set; }
}

/// <summary>
/// Logistics, as in Hearts of Iron: the nation's stockpile stands at its capital, and what its combat units need reaches
/// them through the supply network as shipments that take their time, sent once a day. The chain of command sets who
/// is served first: the units of HQs of high priority, then normal, then low, and last, twice as slowly, the units
/// with no HQ in range; an HQ cut off from supply leaves its units without any. In battle every battalion that fights
/// spends ammunition, and without it fights at half strength.
/// </summary>
public sealed partial class GameSession
{
    private readonly List<Shipment> _shipments = [];

    /// <summary>Shipments on their way, the oldest first.</summary>
    public IReadOnlyList<Shipment> Shipments => _shipments;

    public IEnumerable<Shipment> ShipmentsTo(Unit unit) => _shipments.Where(s => s.UnitId == unit.Id);

    /// <summary>The suministros a combat unit carries when full: a day of fighting for each battalion but its medics.</summary>
    public static double AmmoCapacity(Unit unit) => unit.IsMilitary
        ? unit.Battalions.Where(b => b.Type.Role() != BattalionRole.Support).Sum(b => b.Info.Men) / 100.0 * MilitaryRules.AmmoPerHundredMenHour * MilitaryRules.AmmoHours
        : 0;

    /// <summary>The suministros a combat unit carries now.</summary>
    public static double Ammo(Unit unit) => Math.Max(0, AmmoCapacity(unit) - unit.AmmoSpent);

    /// <summary>What a battalion spends in an hour of fighting.</summary>
    public static double AmmoPerHour(Battalion b) => b.Strength / 100 * MilitaryRules.AmmoPerHundredMenHour;

    /// <summary>
    /// The share of their fire the unit's battalions keep for want of ammunition this hour: all of it while it has the
    /// hour's worth, down to <see cref="MilitaryRules.OutOfAmmoEfficiency"/> with none.
    /// </summary>
    public static double AmmoEfficiency(Unit unit)
    {
        double need = unit.Battalions.Where(b => b.Type.Role() != BattalionRole.Support).Sum(AmmoPerHour);
        if (need <= 0 || !unit.IsMilitary) return 1;
        double share = Math.Min(1, Ammo(unit) / need);
        return MilitaryRules.OutOfAmmoEfficiency + (1 - MilitaryRules.OutOfAmmoEfficiency) * share;
    }

    /// <summary>The fighting battalions spend this hour's ammunition, as far as they have it.</summary>
    private static void SpendAmmo(IEnumerable<Engaged> engaged)
    {
        foreach (var e in engaged)
            e.Unit.AmmoSpent = Math.Min(AmmoCapacity(e.Unit), e.Unit.AmmoSpent + AmmoPerHour(e.Battalion));
    }

    public CommandResult SetSupplyPriority(int playerId, int hqId, SupplyPriority priority)
    {
        if (UnitById(hqId) is not { } hq || hq.OwnerId != playerId || !hq.IsHeadquarters) return CommandResult.Fail("Solo los cuarteles generales tienen prioridad de suministro.");
        hq.SupplyPriority = priority;
        return CommandResult.Success($"{hq.Name}: prioridad de suministro {PriorityName(priority).ToLowerInvariant()}.");
    }

    public static string PriorityName(SupplyPriority priority) => priority switch
    {
        SupplyPriority.High => "Alta",
        SupplyPriority.Low => "Baja",
        _ => "Normal",
    };

    /// <summary>
    /// Where a unit stands in the queue for shipments (lower first: its HQ's priority, or after them all without one)
    /// and how many times slower they reach it; null when none can: it is aboard, out of supply or its HQ is cut off.
    /// </summary>
    public (int Rank, double Slowdown)? ShipmentTerms(Unit unit)
    {
        if (!unit.IsMilitary || unit.IsAboard || !IsInSupply(unit)) return null;
        if (!InCommandRange(unit)) return ((int)SupplyPriority.Low + 1, MilitaryRules.UnattachedShipmentSlowdown);
        var hq = CommanderOf(unit)!;
        if (!IsSupplied(unit.OwnerId, hq.ProvinceId)) return null;
        return ((int)hq.SupplyPriority, 1);
    }

    /// <summary>Why a unit gets no shipments, or null if it does.</summary>
    public string? NoShipmentsReason(Unit unit) =>
        !unit.IsMilitary ? "No es una unidad de combate."
        : unit.IsAboard ? "Va embarcada."
        : !IsInSupply(unit) ? "Está sin suministro."
        : InCommandRange(unit) && !IsSupplied(unit.OwnerId, CommanderOf(unit)!.ProvinceId) ? "Su cuartel general está aislado."
        : CapitalProvince(Players[unit.OwnerId]) is null ? "La nación no tiene capital."
        : null;

    /// <summary>The province of the nation's capital, unless the enemy holds it.</summary>
    private Province? CapitalProvince(Player player) =>
        player.CapitalCityId is int c && CityById(c) is { } city && !Map.Provinces[city.ProvinceId].IsOccupied ? Map.Provinces[city.ProvinceId] : null;

    /// <summary>
    /// Once a day the capital sends each combat unit in reach what it lacks, in the order of <see cref="ShipmentTerms"/>
    /// and as far as the stockpile, the reserve of recruits and the capital's people go: recruits for a day's worth of
    /// reinforcements with their equipment, the pieces of the newest model for its battalions to modernise and the
    /// ammunition it has spent, less what is already on its way. Each shipment takes the way there along the supply
    /// network (by land from the capital, or by sea from the nearest city), slower without an HQ and faster to an emplaced unit.
    /// </summary>
    private void DailyShipments(Player player)
    {
        if (CapitalProvince(player) is not { } capital) return;
        var served = new List<(Unit Unit, int Rank, double Slowdown)>();
        foreach (var unit in Units.Where(u => u.OwnerId == player.Id && u.IsMilitary))
        {
            // A unit that lost battalions (split off, or detached) carries less.
            unit.AmmoSpent = Math.Min(unit.AmmoSpent, AmmoCapacity(unit));
            if (ShipmentTerms(unit) is var (rank, slowdown)) served.Add((unit, rank, slowdown));
        }
        if (served.Count == 0) return;

        // The way from the capital, by land and over the sea, searched only as far as the units it serves.
        var (routeHours, previous) = SupplyRoutes(player, capital.Id, served.Select(s => s.Unit.ProvinceId).ToHashSet());
        var queue = new List<(Unit Unit, int Rank, double Hours)>();
        foreach (var (unit, rank, slowdown) in served)
            if (!double.IsPositiveInfinity(routeHours[unit.ProvinceId]))
                queue.Add((unit, rank, routeHours[unit.ProvinceId] * slowdown
                    * (EnemyRulesTheAir(player.Id, Map.Provinces[unit.ProvinceId]) ? MilitaryRules.UnderEnemyAirSlowdown : 1)
                    * (IsEmplaced(unit) ? MilitaryRules.EmplacedShipmentTime : 1)));

        player.CargoLeftForWantOfConvoys = 0;
        foreach (var (unit, _, hours) in queue.OrderBy(q => q.Rank).ThenBy(q => q.Hours))
        {
            var onTheWay = ShipmentsTo(unit).ToList();
            var shipment = new Shipment
            {
                OwnerId = player.Id, UnitId = unit.Id, ArriveHours = Date.Hours + Math.Max(1, (long)Math.Ceiling(hours)),
                SeaRoute = SeaLegs(previous, unit.ProvinceId),
            };

            // The pieces of the newest model of each line, for the men its battalions have left.
            var upgrades = new Dictionary<string, double>();
            foreach (var b in unit.Battalions)
                if (Upgrade(player, b) is { } model && model.SupplyKey != b.Info.SupplyKey)
                    upgrades[model.SupplyKey] = upgrades.GetValueOrDefault(model.SupplyKey) + model.Pieces * b.StrengthShare;
            foreach (var (key, need) in upgrades)
                Load(player, shipment, key, need - onTheWay.Sum(s => s.Pieces.GetValueOrDefault(key)));

            // A day's worth of recruits for each battalion short of men, with the equipment that arms them.
            double menLeft = unit.Battalions.Sum(b => b.Info.Men - b.Strength) - onTheWay.Sum(s => s.Men);
            foreach (var b in unit.Battalions)
            {
                double men = Math.Min(Math.Min(b.Info.Men - b.Strength, b.Info.Men * MilitaryRules.ReinforcementRate), menLeft);
                men = Math.Min(men, Math.Min(player.Manpower, capital.Population - GameRules.MinCityPopulation));
                if (b.Info.NeedsEquipment) men = Math.Min(men, player.EquipmentOf(b.Info) / b.Info.Pieces * b.Info.Men);
                if (men <= 0) continue;
                if (b.Info.NeedsEquipment) Load(player, shipment, b.Info.SupplyKey, men / b.Info.Men * b.Info.Pieces);
                shipment.Men += men;
                menLeft -= men;
                player.Manpower -= men;
                capital.Population -= men;
            }

            double ammo = Math.Min(unit.AmmoSpent - onTheWay.Sum(s => s.Ammo), player.EquipmentOf(Supplies.General.Key));
            if (ammo > 0)
            {
                player.AddEquipment(Supplies.General.Key, -ammo);
                shipment.Ammo = ammo;
            }
            // Under a sky the enemy rules, part of it cannot get through.
            if (EnemyRulesTheAir(player.Id, Map.Provinces[unit.ProvinceId])) Unload(player, shipment, MilitaryRules.UnderEnemyAirSupplyLoss);
            // Over the sea it needs convoys for its cargo; what they cannot carry stays behind.
            if (shipment.SeaRoute.Count > 0 && Cargo(shipment) > 0)
            {
                double room = FreeConvoys(player) * MilitaryRules.ConvoyCapacity, cargo = Cargo(shipment);
                if (cargo > room)
                {
                    player.CargoLeftForWantOfConvoys += cargo - room;
                    Unload(player, shipment, 1 - room / cargo);
                }
                shipment.Convoys = Cargo(shipment) / MilitaryRules.ConvoyCapacity;
            }
            if (Cargo(shipment) > 1e-6) _shipments.Add(shipment);
        }
    }

    /// <summary>Takes up to <paramref name="pieces"/> of a supply from the stockpile into the shipment.</summary>
    private static void Load(Player player, Shipment shipment, string key, double pieces)
    {
        pieces = Math.Min(pieces, player.EquipmentOf(key));
        if (pieces <= 0) return;
        player.AddEquipment(key, -pieces);
        shipment.Pieces[key] = shipment.Pieces.GetValueOrDefault(key) + pieces;
    }

    /// <summary>The newer model of its line a battalion would take up, if the nation knows one.</summary>
    private static BattalionInfo? Upgrade(Player player, Battalion b)
    {
        int best = b.Type.BestModel(player.Techs);
        return best > b.Model ? b.Type.Models()[best] : null;
    }

    /// <summary>
    /// Battalions in supply and out of battle whose newest model uses the same equipment as theirs (phalanx and
    /// legionaries) take it up at once; the rest wait for its pieces to come (<see cref="DailyShipments"/>).
    /// </summary>
    private void ModerniseInPlace(Player player)
    {
        foreach (var unit in Units.Where(u => u.OwnerId == player.Id && u.IsMilitary && !u.IsAboard && IsInSupply(u) && !InBattle(u)))
            foreach (var b in unit.Battalions)
                if (Upgrade(player, b) is { } model && model.SupplyKey == b.Info.SupplyKey) b.Modernise(b.Type.BestModel(player.Techs));
    }

    /// <summary>
    /// Every hour, the shipments due reach their units, if they are still in reach; if not, or the unit is gone, what
    /// they carry goes back to the stockpile, the recruits to the reserve and the capital.
    /// </summary>
    private void ArriveShipments()
    {
        for (int i = 0; i < _shipments.Count; i++)
        {
            var s = _shipments[i];
            if (s.ArriveHours > Date.Hours) continue;
            _shipments.RemoveAt(i--);
            var player = Players[s.OwnerId];
            if (UnitById(s.UnitId) is { } unit && unit.OwnerId == s.OwnerId && unit.IsMilitary && !unit.IsAboard && IsInSupply(unit))
                Deliver(player, unit, s);
            Return(player, s);
        }
    }

    /// <summary>
    /// Hands the unit what the shipment carries: ammunition, then the pieces for its battalions to take up their newest
    /// model (their old pieces go back to the stockpile), then the recruits with their equipment. What is not needed stays in it.
    /// </summary>
    private static void Deliver(Player player, Unit unit, Shipment s)
    {
        double ammo = Math.Min(s.Ammo, unit.AmmoSpent);
        unit.AmmoSpent -= ammo;
        s.Ammo -= ammo;

        foreach (var b in unit.Battalions)
        {
            if (Upgrade(player, b) is not { } model) continue;
            double share = b.StrengthShare, need = model.Pieces * share;
            if (model.SupplyKey != b.Info.SupplyKey)
            {
                if (s.Pieces.GetValueOrDefault(model.SupplyKey) < need - 1e-9) continue;
                s.Pieces[model.SupplyKey] -= need;
                player.AddEquipment(b.Info, b.Info.Pieces * share);
            }
            b.Modernise(b.Type.BestModel(player.Techs));
        }

        foreach (var b in unit.Battalions)
        {
            double men = Math.Min(b.Info.Men - b.Strength, s.Men);
            if (b.Info.NeedsEquipment) men = Math.Min(men, s.Pieces.GetValueOrDefault(b.Info.SupplyKey) / b.Info.Pieces * b.Info.Men);
            if (men <= 0) continue;
            if (b.Info.NeedsEquipment) s.Pieces[b.Info.SupplyKey] -= men / b.Info.Men * b.Info.Pieces;
            // Recruits are green: they water down the battalion's experience.
            b.Experience = b.Experience * b.Strength / (b.Strength + men);
            b.Strength += men;
            s.Men -= men;
        }
    }

    /// <summary>Men, pieces and ammunition a shipment carries, each counting one against a convoy's room.</summary>
    public static double Cargo(Shipment s) => s.Men + s.Pieces.Values.Sum() + s.Ammo;

    /// <summary>Takes this share of what a shipment carries back where it came from (see <see cref="Return"/>).</summary>
    private void Unload(Player player, Shipment s, double share)
    {
        var back = new Shipment { OwnerId = s.OwnerId, UnitId = s.UnitId, ArriveHours = s.ArriveHours, Men = s.Men * share, Ammo = s.Ammo * share,
            Pieces = s.Pieces.ToDictionary(p => p.Key, p => p.Value * share) };
        s.Men -= back.Men;
        s.Ammo -= back.Ammo;
        foreach (var key in s.Pieces.Keys.ToList()) s.Pieces[key] -= back.Pieces[key];
        Return(player, back);
    }

    /// <summary>
    /// Travel hours from the capital to every province by land through the nation's own or free land and over the sea it
    /// can sail, boarding only at its ports that are not blockaded and landing on any coast; and the province each was
    /// reached from. The search stops once the <paramref name="targets"/> are reached.
    /// </summary>
    private (double[] Hours, int[] Previous) SupplyRoutes(Player player, int capital, HashSet<int> targets)
    {
        int n = Map.Provinces.Count;
        var hours = new double[n];
        var previous = new int[n];
        Array.Fill(hours, double.PositiveInfinity);
        Array.Fill(previous, -1);
        var open = new PriorityQueue<int, double>();
        hours[capital] = 0;
        open.Enqueue(capital, 0);
        int left = targets.Count;
        bool Land(Province p) => !p.IsWater && (!p.IsOwned || p.ControllerId == player.Id);
        while (open.TryDequeue(out int id, out double at))
        {
            if (at > hours[id]) continue;
            if (targets.Contains(id) && --left == 0) break;
            var from = Map.Provinces[id];
            foreach (int next in from.Neighbors)
            {
                var to = Map.Provinces[next];
                bool can = to.IsWater
                    ? CanSail(player, to) && (from.IsWater || IsPort(from, player.Id) && !IsBlockaded(from))
                    : Land(to);
                if (!can) continue;
                double h = at + Pathfinder.StepHours(id, next);
                if (h >= hours[next]) continue;
                hours[next] = h;
                previous[next] = id;
                open.Enqueue(next, h);
            }
        }
        return (hours, previous);
    }

    /// <summary>The seas on the way to a province, in order, from <see cref="SupplyRoutes"/>.</summary>
    private List<int> SeaLegs(int[] previous, int provinceId)
    {
        var seas = new List<int>();
        for (int id = provinceId; id >= 0; id = previous[id])
            if (Map.Provinces[id].IsWater) seas.Add(id);
        seas.Reverse();
        return seas;
    }

    /// <summary>What a shipment still carries goes back: pieces and ammunition to the stockpile, recruits to the reserve and the capital.</summary>
    private void Return(Player player, Shipment s)
    {
        foreach (var (key, pieces) in s.Pieces) if (pieces > 0) player.AddEquipment(key, pieces);
        if (s.Ammo > 0) player.AddEquipment(Supplies.General.Key, s.Ammo);
        if (s.Men <= 0) return;
        player.Manpower += s.Men;
        if (CapitalProvince(player) is { } capital) capital.Population += s.Men;
    }
}
