using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Simulation;

namespace Conquer.Presentation;

/// <summary>The Marina tab of the nation screen: its fleets, its shipyards, the ships under construction and what it can order.</summary>
public sealed partial class NationScreen
{
    private TablesPage Navy() => new([Fleets(), Slips(), ShipQueue(), ShipOrders()]);

    /// <summary>Each fleet: where it is, its ships, crews and organisation, whether in port and what it does.</summary>
    private TablePage Fleets()
    {
        Column[] columns = [new("Flota", 180), new("Ubicación", 170), new("Barcos", 300), new("Tripulación", 130), new("Organiz.", 100), new("Misión", 160), new("Estado", 200), new("", 60)];
        var rows = new List<IReadOnlyList<Cell>>();
        foreach (var fleet in Session.Units.Where(u => u.OwnerId == Player.Id && u.IsFleet).OrderBy(u => u.Name))
        {
            var p = Map.Provinces[fleet.ProvinceId];
            var ships = fleet.Ships.GroupBy(s => s.Info.Name).Select(g => g.Count() > 1 ? $"{g.Count()} × {g.Key}" : g.Key);
            int cargo = Session.CargoMen(fleet);
            rows.Add(
            [
                new TextCell(fleet.Name, Bold: true),
                new TextCell(Session.PlaceName(p), Tone.Dim),
                new TextCell(string.Join(", ", ships), Tone.Normal, TextSize.Small, Top: 8, Tooltip: string.Join("\n", fleet.Ships.Select(s =>
                    $"{s.Info.Name} ({s.Type.Line().Name.ToLowerInvariant()}): fuego {s.Info.Attack:0}, tripulación {s.Strength:0}/{s.Info.Men}"))),
                new TextCell($"{fleet.Citizens:N0}", fleet.StrengthShare < 0.5 ? Tone.Bad : Tone.Normal, Bar: new CellBar(fleet.StrengthShare, Tone.Strength, 26, 3, 20)),
                new TextCell("", Bar: new CellBar(fleet.OrganisationShare, Tone.Organisation, 13, 8, 20)),
                new TextCell(GameSession.MissionName(fleet.Mission), fleet.Mission == Conquer.Game.Rules.FleetMission.None ? Tone.Dim : Tone.Good, TextSize.Small, Top: 8,
                    Tooltip: GameSession.MissionDescription(fleet.Mission)),
                new TextCell((Session.IsPort(p, Player.Id) ? "En puerto" : Session.EnemyFleetsIn(p.Id, Player.Id).Any() ? "Combatiendo" : "En el mar")
                             + (fleet.Capacity > 0 ? $" · lleva {cargo:N0}/{fleet.Capacity:N0}" : ""),
                    Session.EnemyFleetsIn(p.Id, Player.Id).Any() ? Tone.Bad : Tone.Dim, TextSize.Small),
                new ButtonsCell([new Button("Ver", () => ViewUnit(fleet.Id), Tooltip: "Seleccionar en el mapa", Size: TextSize.Small)]),
            ]);
        }
        return new TablePage(new Table(columns, rows, Empty: "No tienes flotas. Encarga barcos abajo cuando tengas un puerto."), "Flotas", Tone.Accent);
    }

    /// <summary>Each port: its slips and what each is building.</summary>
    private TablePage Slips()
    {
        Column[] columns = [new("Astillero", 220), new("Gradas", 120), new("En construcción", 600)];
        var rows = new List<IReadOnlyList<Cell>>();
        foreach (var (port, slips) in Session.Shipyards(Player).OrderBy(x => Session.PlaceName(x.Port)))
        {
            var building = Player.ShipOrders.Where(o => o.PortId == port.Id).ToList();
            rows.Add(
            [
                new TextCell(Session.PlaceName(port), Bold: true),
                new TextCell($"{building.Count}/{slips}", building.Count < slips ? Tone.Accent : Tone.Normal,
                    Tooltip: "Un puerto tiene una grada, y otra más con dique seco. Cada grada construye un barco a la vez."),
                Session.IsBlockaded(port)
                    ? new TextCell("Bloqueado por el enemigo: no construye", Tone.Bad, Tooltip: "Una flota enemiga bloquea el puerto: sus gradas paran y lo que construían espera otra grada.")
                    : new TextCell(building.Count == 0 ? "Libre" : string.Join(", ", building.Select(o => $"{o.Info.Name} ({o.Progress:P0})")),
                        building.Count == 0 ? Tone.Dim : Tone.Normal),
            ]);
        }
        return new TablePage(new Table(columns, rows, Empty: "No tienes astilleros: construye un Puerto (Navegación a vela) en una ciudad con costa."),
            "Astilleros", Tone.Accent);
    }

    /// <summary>
    /// The queue: each order in the order the slips take them, with its shipyard, progress, days left and what a day
    /// of work costs, and buttons to move it up or down or cancel it.
    /// </summary>
    private TablePage ShipQueue()
    {
        Column[] columns = [new("Barco", 230), new("Astillero", 190), new("Progreso", 170), new("Quedan", 110), new("Coste al día", 300), new("", 160)];
        var rows = new List<IReadOnlyList<Cell>>();
        var queue = Player.ShipOrders;
        for (int i = 0; i < queue.Count; i++)
        {
            var order = queue[i];
            var info = order.Info;
            double perDay = GameSession.ShipWorkPerDay(Player, order.Type) / info.TrainingDays;
            string where = order.PortId is int id ? Session.PlaceName(Map.Provinces[id])
                : order.PreferredPortId is int preferred ? $"Espera grada ({Session.PlaceName(Map.Provinces[preferred])})" : "Espera grada";
            rows.Add(
            [
                new TextCell($"{i + 1}. {info.Name}", Bold: order.PortId != null, Suffix: $" {order.Type.Line().Name.ToLowerInvariant()}"),
                new TextCell(where, order.PortId == null ? Tone.Accent : Tone.Dim, TextSize.Small, Top: 8),
                new TextCell(order.WaitingForCrew ? "Falta tripulación" : $"{order.Progress:P0}", order.WaitingForCrew ? Tone.Bad : Tone.Normal,
                    Tooltip: order.WaitingForCrew ? $"Terminado: espera {info.Men} hombres del puerto y de la reserva de reclutas." : null,
                    Bar: new CellBar(order.Progress, Tone.Accent, 26, 3, 20)),
                new TextCell(order.PortId == null ? "-" : $"{Math.Ceiling(GameSession.DaysLeft(Player, order)):0} días", Tone.Dim),
                new TextCell(info.Cost.Times(perDay).ToString(), Tone.Dim, TextSize.Small, Top: 8,
                    Tooltip: $"Lo que gasta una grada cada día de trabajo; si no hay bastante, avanza solo lo que puede pagar. Total: {info.Cost}."),
                new ButtonsCell(
                [
                    new Button("Subir", () => Show(Session.MoveShipOrder(Player.Id, order.Id, -1)), i > 0, Tooltip: "Adelantar en la cola", Size: TextSize.Small),
                    new Button("Bajar", () => Show(Session.MoveShipOrder(Player.Id, order.Id, 1)), i < queue.Count - 1, Tooltip: "Retrasar en la cola", Size: TextSize.Small),
                    new Button("x", () => Show(Session.CancelShipOrder(Player.Id, order.Id)),
                        Tooltip: "Cancelar el encargo. Lo ya gastado en él se pierde.", Size: TextSize.Small),
                ]),
            ]);
        }
        return new TablePage(new Table(columns, rows, Empty: "No hay barcos encargados."), "Cola de construcción: las gradas libres toman los primeros que pueden construir.", Tone.Accent);
    }

    /// <summary>A row per line of ships: the model the nation would order now, its figures and cost, and the button to order it.</summary>
    private TablePage ShipOrders()
    {
        Column[] columns = [new("Línea", 170), new("Modelo", 290), new("Fuego", 80), new("Lleva", 90), new("Tripulación", 110), new("Días", 70), new("Coste", 290), new("", 110)];
        var rows = new List<IReadOnlyList<Cell>>();
        foreach (var type in Battalions.All.Where(t => t.Line().Group == BattalionGroup.Navy))
        {
            var model = GameSession.ModelFor(Player, type);
            bool known = type.BestModel(Player.Techs) >= 0;
            var can = Session.CanOrderShip(Player, type);
            rows.Add(
            [
                new TextCell(type.Line().Name, Bold: true),
                new TextCell(model.Name, known ? Tone.Normal : Tone.Dim, Suffix: known ? null : " · por descubrir"),
                new TextCell($"{model.Attack:0}", Tone.Dim),
                new TextCell(model.Capacity > 0 ? $"{model.Capacity:N0}" : "-", Tone.Dim),
                new TextCell($"{model.Men:N0}", Tone.Dim),
                new TextCell($"{Math.Ceiling(model.TrainingDays / GameSession.ShipWorkPerDay(Player, type)):0}", Tone.Dim),
                new TextCell(model.Cost.ToString(), Tone.Dim, TextSize.Small, Top: 8),
                new ButtonsCell([new Button("Encargar", () => Show(Session.OrderShip(Player.Id, type)), can.Ok,
                    Tooltip: can.Ok ? $"Añade un {model.Name.ToLowerInvariant()} al final de la cola. Se paga mientras se construye y, al terminar, toma {model.Men} hombres del puerto."
                        : can.Message, Size: TextSize.Small)]),
            ]);
        }
        // Convoys: merchant ships in batches, which carry the shipments over the sea.
        var batch = Battalions.ConvoyBatch;
        var convoys = Session.CanOrderConvoys(Player);
        rows.Add(
        [
            new TextCell("Convoyes", Bold: true),
            new TextCell($"Lote de {MilitaryRules.ConvoysPerOrder}", Tone.Normal),
            new TextCell("-", Tone.Dim),
            new TextCell($"{MilitaryRules.ConvoyCapacity * MilitaryRules.ConvoysPerOrder:N0}", Tone.Dim, Tooltip: $"Cada convoy lleva {MilitaryRules.ConvoyCapacity:0} hombres, piezas o suministros."),
            new TextCell($"{batch.Men:N0}", Tone.Dim),
            new TextCell($"{batch.TrainingDays}", Tone.Dim),
            new TextCell(batch.Cost.ToString(), Tone.Dim, TextSize.Small, Top: 8),
            new ButtonsCell([new Button("Encargar", () => Show(Session.OrderConvoys(Player.Id)), convoys.Ok,
                Tooltip: convoys.Ok ? $"Añade {MilitaryRules.ConvoysPerOrder} convoyes al final de la cola." : convoys.Message, Size: TextSize.Small)]),
        ]);
        double free = Session.FreeConvoys(Player), inUse = Session.ConvoysInUse(Player);
        string title = $"Encargar barcos · Convoyes: {Player.Convoys:0.#} ({inUse:0.#} en el mar, {free:0.#} en puerto)" +
                       (Player.CargoLeftForWantOfConvoys >= 0.5 ? $" · faltan para {Player.CargoLeftForWantOfConvoys:N0} de carga" : "");
        return new TablePage(new Table(columns, rows), title, Player.CargoLeftForWantOfConvoys >= 0.5 ? Tone.Bad : Tone.Accent);
    }
}
