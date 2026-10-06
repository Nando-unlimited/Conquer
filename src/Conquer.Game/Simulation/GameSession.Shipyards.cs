using Conquer.Game.Rules;
using Conquer.Game.Buildings;
using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.World;

namespace Conquer.Game.Simulation;

/// <summary>
/// Shipyards, as in Hearts of Iron: the nation orders ships into one queue, and every port has slips (one, and another
/// with a dry dock) that take the first orders they can build. Each day a slip works on its ship, paying that day's
/// share of what the ship costs, or working only the part it can pay. A finished ship takes its crew from the port and
/// puts to sea as a new fleet.
/// </summary>
public sealed partial class GameSession
{
    private int _nextShipOrderId;

    /// <summary>Slips in a port: one, and one more with a dry dock; none if it is not a port of the nation's.</summary>
    public int Slips(Province p, int playerId) => !IsPort(p, playerId) ? 0 : 1 + (p.Has(BuildingType.DryDock) ? 1 : 0);

    /// <summary>The nation's ports, with how many slips each has.</summary>
    public IEnumerable<(Province Port, int Slips)> Shipyards(Player player) =>
        player.Provinces.Select(id => Map.Provinces[id]).Select(p => (p, Slips(p, player.Id))).Where(x => x.Item2 > 0);

    /// <summary>Whether this port can build the model: it is a port and has the shipyard the model needs.</summary>
    public CommandResult CanBuildShipIn(Province p, int playerId, BattalionInfo model)
    {
        if (!IsPort(p, playerId)) return CommandResult.Fail("Los barcos solo se construyen en ciudades con puerto.");
        if (IsBlockaded(p)) return CommandResult.Fail("El puerto está bloqueado por el enemigo.");
        if (model.Shipyard is BuildingType yard && !p.Has(yard)) return CommandResult.Fail($"Requiere {yard.Info().Name.ToLowerInvariant()} en la ciudad.");
        return CommandResult.Success();
    }

    /// <summary>Whether the nation can order a ship of the line: it knows a model of it and has a port that can build that model.</summary>
    public CommandResult CanOrderShip(Player player, BattalionType type)
    {
        var model = ModelFor(player, type);
        if (!model.Naval) return CommandResult.Fail("No es un barco.");
        var missing = model.Requires.Where(t => !player.Techs.Contains(t)).ToList();
        if (missing.Count > 0) return CommandResult.Fail("Requiere " + string.Join(" y ", missing.Select(t => Science.Techs.Info(t).Name.ToLowerInvariant())) + ".");
        var ports = Shipyards(player).ToList();
        if (ports.Count == 0) return CommandResult.Fail("Necesitas un puerto: constrúyelo en una ciudad con costa.");
        if (!ports.Any(x => CanBuildShipIn(x.Port, player.Id, model).Ok))
            return CommandResult.Fail($"Requiere {model.Shipyard!.Value.Info().Name.ToLowerInvariant()} en uno de tus puertos.");
        return CommandResult.Success();
    }

    /// <summary>
    /// Adds a ship of the newest model of the line to the end of the nation's queue, for any of its ports or, with
    /// <paramref name="portId"/>, preferably that one. Nothing is paid until a slip starts on it.
    /// </summary>
    public CommandResult OrderShip(int playerId, BattalionType type, int? portId = null)
    {
        var player = Players[playerId];
        var check = CanOrderShip(player, type);
        if (!check.Ok) return check;
        var model = ModelFor(player, type);
        if (portId is int id && !CanBuildShipIn(Map.Provinces[id], playerId, model).Ok) portId = null;
        player.ShipOrders.Add(new ShipOrder { Id = _nextShipOrderId++, Type = type, Model = Math.Max(0, type.BestModel(player.Techs)), PreferredPortId = portId });
        return CommandResult.Success($"{model.Name} encargado a los astilleros: {player.ShipOrders.Count}.º de la cola.");
    }

    /// <summary>Moves an order up (negative) or down the queue.</summary>
    public CommandResult MoveShipOrder(int playerId, int orderId, int by)
    {
        var queue = Players[playerId].ShipOrders;
        int index = queue.FindIndex(o => o.Id == orderId);
        if (index < 0) return CommandResult.Fail("Encargo no válido.");
        int to = Math.Clamp(index + by, 0, queue.Count - 1);
        var order = queue[index];
        queue.RemoveAt(index);
        queue.Insert(to, order);
        return CommandResult.Success();
    }

    /// <summary>Takes an order out of the queue; what was spent on it is lost.</summary>
    public CommandResult CancelShipOrder(int playerId, int orderId)
    {
        var queue = Players[playerId].ShipOrders;
        var order = queue.FirstOrDefault(o => o.Id == orderId);
        if (order == null) return CommandResult.Fail("Encargo no válido.");
        queue.Remove(order);
        return CommandResult.Success($"{order.Info.Name} cancelado.");
    }

    /// <summary>Days of work a slip does on a ship of the line each day: one, more with the advances that study it.</summary>
    public static double ShipWorkPerDay(Player player, BattalionType type) => 1 + TrainingSpeed(player, type);

    /// <summary>Days of work left on an order, at the nation's pace.</summary>
    public static double DaysLeft(Player player, ShipOrder order) =>
        Math.Max(0, order.Info.TrainingDays - order.DaysDone) / ShipWorkPerDay(player, order.Type);

    /// <summary>
    /// Every day: orders whose port can no longer build them free their slip (keeping their work); free slips take the
    /// first orders in the queue they can build, the port they were ordered from first; each slip works a day on its
    /// ship, paying that day's share of its cost (or the part the stores allow); finished ships take their crew from the
    /// port and become fleets there, or wait for it.
    /// </summary>
    private void DailyShipyards(Player player)
    {
        var queue = player.ShipOrders;
        if (queue.Count == 0) return;
        foreach (var order in queue.Where(o => o.PortId is int id && !CanBuildShipIn(Map.Provinces[id], player.Id, o.Info).Ok)) order.PortId = null;

        var free = Shipyards(player).ToDictionary(x => x.Port.Id, x => x.Slips - queue.Count(o => o.PortId == x.Port.Id));
        foreach (var order in queue.Where(o => o.PortId == null))
        {
            var candidates = free.Where(f => f.Value > 0 && CanBuildShipIn(Map.Provinces[f.Key], player.Id, order.Info).Ok).Select(f => f.Key).ToList();
            if (candidates.Count == 0) continue;
            int port = order.PreferredPortId is int preferred && candidates.Contains(preferred) ? preferred : candidates[0];
            order.PortId = port;
            free[port]--;
        }

        foreach (var order in queue.Where(o => o.PortId != null).ToList())
        {
            var info = order.Info;
            double work = Math.Min(ShipWorkPerDay(player, order.Type), info.TrainingDays - order.DaysDone);
            if (work > 0)
            {
                var cost = info.Cost.Times(work / info.TrainingDays);
                double share = cost.Items.Length == 0 ? 1 : cost.Items.Min(i => i.Amount <= 0 ? 1 : Math.Min(1, player.Stockpile[i.Type] / i.Amount));
                if (share <= 0) continue;
                foreach (var (type, amount) in cost.Items)
                {
                    player.Stockpile[type] -= amount * share;
                    player.Record(Economy.ResourceFlow.Workshops, type, -amount * share);
                }
                order.DaysDone += work * share;
            }
            if (order.DaysDone < info.TrainingDays - 1e-9) continue;
            Launch(player, order);
        }
    }

    /// <summary>A finished ship takes its crew from its port and the reserve and puts to sea as a new fleet; without them it waits.</summary>
    private void Launch(Player player, ShipOrder order)
    {
        var port = Map.Provinces[order.PortId!.Value];
        var info = order.Info;
        if (port.Population - info.Men < MinimumPopulation(port) || player.Manpower < info.Men)
        {
            if (!order.WaitingForCrew && player.IsHuman)
                Notify(player.Id, $"El {info.Name.ToLowerInvariant()} de {PlaceName(port)} está terminado, pero faltan {info.Men} hombres para su tripulación.");
            order.WaitingForCrew = true;
            return;
        }
        port.Population -= info.Men;
        player.Manpower -= info.Men;
        player.ShipOrders.Remove(order);
        if (order.Convoys)
        {
            player.Convoys += MilitaryRules.ConvoysPerOrder;
            if (player.IsHuman) Notify(player.Id, $"{MilitaryRules.ConvoysPerOrder} convoyes nuevos en {PlaceName(port)}: tienes {player.Convoys:0}.");
            return;
        }
        var fleet = AddUnit(player.Id, UnitType.Fleet, port.Id, 0, NextUnitNumber(player.Id, FleetNumbering));
        fleet.Ships.Add(new Battalion(order.Type, order.Model));
        if (player.IsHuman) Notify(player.Id, $"Nueva unidad en {PlaceName(port)}: {fleet.Name} ({info.Name.ToLowerInvariant()}).");
    }
}
