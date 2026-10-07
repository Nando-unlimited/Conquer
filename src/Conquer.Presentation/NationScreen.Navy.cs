using Conquer.Game.Economy;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Simulation;

namespace Conquer.Presentation;

/// <summary>The Marina tab of the nation screen: its fleets, its shipyards, the ships under construction and what it can order.</summary>
public sealed partial class NationScreen
{
    private TablesPage Navy() => new([Fleets(), NavalCommand(), Slips(), ShipQueue(), ShipOrders()]);

    /// <summary>Each fleet: its size and where it is, its ships, crews and organisation, its Flota, mission and state, fuel included.</summary>
    private TablePage Fleets()
    {
        Column[] columns = [new("Agrupación", 180), new("Ubicación", 160), new("Barcos", 280), new("Tripulación", 120), new("Organiz.", 90), new("Flota", 120),
            new("Misión", 140), new("Estado", 200), new("", 60)];
        var rows = new List<IReadOnlyList<Cell>>();
        foreach (var fleet in Session.Units.Where(u => u.OwnerId == Player.Id && u.IsFleet).OrderByDescending(u => u.Ships.Count).ThenBy(u => u.Name))
        {
            var p = Map.Provinces[fleet.ProvinceId];
            var ships = fleet.Ships.GroupBy(s => s.Info.Name).Select(g => g.Count() > 1 ? $"{g.Count()} × {g.Key}" : g.Key);
            int cargo = Session.CargoMen(fleet);
            var flota = Session.FlotaOf(fleet);
            bool fighting = Session.EnemyFleetsIn(p.Id, Player.Id).Any();
            rows.Add(
            [
                new TextCell(fleet.Name, Bold: true),
                new TextCell(Session.PlaceName(p), Tone.Dim),
                new TextCell(string.Join(", ", ships), Tone.Normal, TextSize.Small, Top: 8, Tooltip: string.Join("\n", fleet.Ships.Select(s =>
                    $"{s.Info.Name} ({s.Type.Line().Name.ToLowerInvariant()}): fuego {s.Info.Attack:0}, tripulación {s.Strength:0}/{s.Info.Men}" +
                    (s.Info.Fuel is { } fuel ? $", {s.Info.FuelPerDay:0.#} de {fuel.Name().ToLowerInvariant()} al día en el mar" : "")))),
                new TextCell($"{fleet.Citizens:N0}", fleet.StrengthShare < 0.5 ? Tone.Bad : Tone.Normal, Bar: new CellBar(fleet.StrengthShare, Tone.Strength, 26, 3, 20)),
                new TextCell("", Bar: new CellBar(fleet.OrganisationShare, Tone.Organisation, 13, 8, 20)),
                new TextCell(flota?.Name ?? "-", flota == null ? Tone.Dim : Session.InFleetCommand(fleet) ? Tone.Good : Tone.Bad, TextSize.Small, Top: 8),
                new TextCell(GameSession.MissionName(fleet.Mission), fleet.Mission == FleetMission.None ? Tone.Dim : Tone.Good, TextSize.Small, Top: 8,
                    Tooltip: GameSession.MissionDescription(fleet.Mission)),
                new TextCell((Session.IsPort(p, Player.Id) ? "En puerto" : fighting ? "Combatiendo" : "En el mar") + (fleet.OutOfFuel ? " · sin combustible" : "")
                             + (fleet.Capacity > 0 ? $" · lleva {cargo:N0}/{fleet.Capacity:N0}" : ""),
                    fighting || fleet.OutOfFuel ? Tone.Bad : Tone.Dim, TextSize.Small),
                new ButtonsCell([new Button("Ver", () => ViewUnit(fleet.Id), Tooltip: "Seleccionar en el mapa", Size: TextSize.Small)]),
            ]);
        }
        return new TablePage(new Table(columns, rows, Empty: "No tienes barcos. Encarga barcos abajo cuando tengas un puerto."),
            "Agrupaciones · flotilla de hasta 3 buques, escuadra de 3 flotillas (9), fuerza de 3 escuadras (27)", Tone.Accent);
    }

    /// <summary>
    /// The Armada and each Flota: port, admiral and fleets, with a button to disband each, and buttons to form a Flota or the
    /// Armada in the nation's biggest port.
    /// </summary>
    private TablePage NavalCommand()
    {
        Column[] columns = [new("Cuartel general", 200), new("Puerto", 190), new("Almirante", 280), new("Manda", 300), new("", 110)];
        var rows = new List<IReadOnlyList<Cell>>();
        foreach (var hq in Session.NavalHeadquarters.Where(h => h.OwnerId == Player.Id).OrderByDescending(h => h.Level).ThenBy(h => h.Number))
        {
            string commands = hq.IsNavy
                ? $"{Session.NavalHeadquarters.Count(h => h.OwnerId == Player.Id && !h.IsNavy)} flotas"
                : $"{Session.FleetsOf(hq).Count()}/{MilitaryRules.MaxFleetsPerFlota} agrupaciones";
            rows.Add(
            [
                new TextCell(hq.Name, hq.IsNavy ? Tone.Accent : Tone.Normal, Bold: true),
                new TextCell(Session.PlaceName(Map.Provinces[hq.BaseProvinceId]), Tone.Dim),
                new TextCell(hq.Officer?.Title ?? "Ninguno", hq.Officer == null ? Tone.Dim : Tone.Normal, TextSize.Small, Top: 8,
                    Tooltip: hq.Officer is { } g ? $"{g.Summary}: suma su habilidad al fuego de los barcos de su flota." : null),
                new TextCell(commands, Tone.Dim, TextSize.Small, Top: 8,
                    Tooltip: hq.IsNavy
                        ? $"Las flotas a menos de {MilitaryRules.NavyCommandRangeKm:N0} km dan +{MilitaryRules.HigherFleetCommandBonus:P0} más a sus barcos."
                        : $"Manda las agrupaciones a menos de {MilitaryRules.FleetCommandRangeKm:N0} km de su puerto: +{MilitaryRules.FleetCommandBonus:P0} de fuego y la habilidad de su almirante."),
                new ButtonsCell([new Button("Disolver", () => Show(Session.DisbandNavalHeadquarters(Player.Id, hq.Id)),
                    Tooltip: "Sus agrupaciones quedan sin flota; su plana mayor vuelve a la reserva y su almirante a los oficiales sin destino.", Size: TextSize.Small)]),
            ]);
        }
        var port = Player.Provinces.Select(id => Map.Provinces[id]).Where(p => Session.IsPort(p, Player.Id)).MaxBy(p => p.Population);
        foreach (int level in new[] { 1, 2 })
        {
            var can = port == null ? CommandResult.Fail("Se forma en uno de tus puertos.") : Session.CanRaiseNavalHeadquarters(port, level);
            rows.Add(
            [
                new TextCell(level == 2 ? "Armada" : "Flota", Tone.Dim),
                new TextCell(port == null ? "-" : Session.PlaceName(port), Tone.Dim),
                new TextCell($"Coste: {GameSession.NavalHeadquartersCost(level)}, {MilitaryRules.NavalHeadquartersStaff} hombres", Tone.Dim, TextSize.Small, Top: 8),
                new TextCell(level == 2 ? "Una por nación: manda las flotas" : "Manda agrupaciones cercanas a su puerto", Tone.Dim, TextSize.Small, Top: 8),
                new ButtonsCell([new Button("Formar", () => Show(Session.RaiseNavalHeadquarters(Player.Id, port!.Id, level)), can.Ok,
                    Tooltip: can.Ok ? $"Se forma en {Session.PlaceName(port!)} con un almirante." : can.Message, Size: TextSize.Small)]),
            ]);
        }
        return new TablePage(new Table(columns, rows), "Mando naval · asigna cada agrupación a su flota desde su panel", Tone.Accent);
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
