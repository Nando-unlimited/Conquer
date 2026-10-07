using Conquer.Game.Economy;
using Conquer.Game.Military;
using Conquer.Game.Simulation;

namespace Conquer.Presentation;

/// <summary>
/// The Almacén tab of the nation screen: the resources in store with what came in and went out the last day, and the
/// stockpile of equipment with what the workshops make.
/// </summary>
public sealed partial class NationScreen
{
    private TablesPage Store() => new([Stock(), Equipment()]);

    /// <summary>
    /// One row per known resource: what is in store, what the last day brought in and took out by where it came from
    /// or went to (<see cref="ResourceFlow"/>), the balance and, when it falls, how many days the store lasts.
    /// </summary>
    private TablePage Stock()
    {
        Column[] columns = [new("Recurso", 150), new("En almacén", 130), new("Producción", 130), new("Comercio", 120), new("Consumo", 120),
            new("Ejército", 120), new("Talleres", 120), new("Balance al día", 140), new("Se agota en", 120),
            new("En bolsas", 110)];
        var reserves = Session.Stats(Player).Reserves;
        var rows = new List<IReadOnlyList<Cell>>();
        foreach (var r in Resources.All.Where(Player.Knows))
        {
            double stock = Player.Stockpile[r], net = Player.LastDayNet[(int)r];
            TextCell Flow(ResourceFlow flow, string tooltip)
            {
                double amount = Player.LastDayFlows[(int)flow][(int)r];
                return Math.Abs(amount) < 0.005 ? new TextCell("-", Tone.Dim)
                    : new TextCell($"{amount:+#,0.##;-#,0.##}", amount > 0 ? Tone.Good : Tone.Bad, Tooltip: tooltip);
            }
            rows.Add(
            [
                new TextCell(r.Name(), Bold: true),
                new TextCell($"{stock:N0}", r == ResourceType.Food && Player.IsStarving ? Tone.Bad : Tone.Normal),
                Flow(ResourceFlow.Production, r switch
                {
                    ResourceType.Food => "Las cosechas de tus provincias.",
                    ResourceType.Wood => "La madera que cortan tus provincias.",
                    ResourceType.Gold => "Los impuestos de tus provincias y el oro de sus minas.",
                    _ => "Lo que sacan los yacimientos de tus provincias.",
                }),
                Flow(ResourceFlow.Exchange, "Acuerdos comerciales, tributos y reparaciones: lo que entra y lo que sale."),
                Flow(ResourceFlow.Consumption, "Lo que come tu gente (también la de tus unidades y la que migra) y la comida que se estropea."),
                Flow(ResourceFlow.Upkeep, "El mantenimiento de tus unidades."),
                Flow(ResourceFlow.Workshops, "Lo que gastan tus talleres y fábricas en fabricar equipo."),
                new TextCell(Math.Abs(net) < 0.005 ? "-" : $"{net:+#,0.##;-#,0.##}", net > 0 ? Tone.Good : net < 0 ? Tone.Bad : Tone.Dim, Bold: true),
                new TextCell(net < -0.005 ? stock / -net < 1 ? "Hoy" : $"{stock / -net:N0} días" : "-", net < -0.005 && stock / -net < 30 ? Tone.Bad : Tone.Dim),
                Resources.Deposits.Contains(r) && !r.IsRenewable()
                    ? new TextCell(TextFormat.Compact(reserves[(int)r]), reserves[(int)r] > 0 ? Tone.Normal : Tone.Dim,
                        Tooltip: "Lo que queda en los yacimientos de tus provincias. Cada bolsa se agota al explotarla.")
                    : new TextCell("-", Tone.Dim),
            ]);
        }
        return new TablePage(new Table(columns, rows), "Recursos: lo que hay y lo que entró y salió en el último día.", Tone.Dim);
    }

    /// <summary>
    /// One row per model the nation can make (the newest of each line) or still keeps pieces of: the pieces in store and
    /// the battalions they arm, what the workshops and factories make a day, the pieces its battalions wait for to take
    /// up a newer model, and what a battalion's worth costs.
    /// </summary>
    private TablePage Equipment()
    {
        var workshops = Player.Provinces.Select(id => Map.Provinces[id]).Where(GameSession.HasWorkshop).ToList();
        int idle = workshops.Count(p => GameSession.ProductionOf(p) is null);
        var models = GameSession.ProducibleModels(Player).Select(x => (x.Type, x.Model)).ToList();
        foreach (string key in Player.Equipment.Where(e => e.Value >= 1).Select(e => e.Key))
            if (!models.Any(m => m.Model.SupplyKey == key) && Battalions.BySupply(key) is var (type, model)) models.Add((type, model));

        // What the nation's battalions wait for: the pieces of their line's newest model, for the men they have left.
        var waiting = new Dictionary<string, double>();
        foreach (var b in Session.Units.Where(u => u.OwnerId == Player.Id && u.IsMilitary).SelectMany(u => u.Battalions))
        {
            int best = b.Type.BestModel(Player.Techs);
            if (best <= b.Model) continue;
            var model = b.Type.Models()[best];
            waiting[model.SupplyKey] = waiting.GetValueOrDefault(model.SupplyKey) + model.Pieces * b.StrengthShare;
        }

        // What is on its way to the troops: pieces for recruits and new models, and ammunition (suministros).
        var onTheWay = new Dictionary<string, double>();
        foreach (var s in Session.Shipments.Where(s => s.OwnerId == Player.Id))
        {
            foreach (var (key, pieces) in s.Pieces) onTheWay[key] = onTheWay.GetValueOrDefault(key) + pieces;
            onTheWay[Supplies.General.Key] = onTheWay.GetValueOrDefault(Supplies.General.Key) + s.Ammo;
        }

        Column[] columns = [new("Suministro", 300), new("En almacén", 230), new("Producción", 220), new("Para modernizar", 170), new("En camino", 140), new("Coste por batallón", 220)];
        var rows = new List<IReadOnlyList<Cell>>();
        foreach (var (type, model) in models.OrderBy(m => m.Type).ThenByDescending(m => m.Model.Requires.Length))
        {
            double stock = Player.EquipmentOf(model);
            var making = workshops.Where(p => GameSession.ProductionOf(p)?.SupplyKey == model.SupplyKey).ToList();
            double perDay = making.Sum(p => GameSession.ProductionRate(p, GameSession.ProductionOf(p)!));
            // A shared supply is current while some line's newest model takes it.
            bool newest = Battalions.All.Any(t => t.BestModel(Player.Techs) >= 0 && t.ModelFor(Player.Techs).SupplyKey == model.SupplyKey);
            string users = model.Supply != null ? TextFormat.List(Battalions.UsersOf(model.Supply).Select(m => m.Name.ToLowerInvariant())) : model.Name.ToLowerInvariant();
            rows.Add(
            [
                new TextCell(model.SupplyName, newest ? Tone.Normal : Tone.Dim, Bold: newest, Suffix: newest ? null : " · antiguo",
                    Tooltip: $"Para {users}: {(model.Supply != null ? "una pieza por hombre" : model.PiecesText(model.Pieces) + " por batallón")}."
                        + (model.SupplyKey == Supplies.General.Key ? " También es la munición de todas tus tropas en combate." : "")),
                new TextCell($"{stock:N0}", stock >= model.Pieces ? Tone.Normal : Tone.Dim,
                    Suffix: model.Supply != null ? $" · {Math.Floor(stock / 100):0} bat. de 100" : $" · {Math.Floor(stock / model.Pieces):0} bat."),
                new TextCell(perDay > 0 ? $"{perDay:0.#}/día" : "-", perDay > 0 ? Tone.Good : Tone.Dim,
                    Suffix: making.Count > 0 ? $" · {TextFormat.Plural(making.Count, "taller", "talleres")}" : null,
                    Tooltip: making.Count > 0 ? string.Join(", ", making.Select(p => Session.PlaceName(p))) : null),
                new TextCell(waiting.TryGetValue(model.SupplyKey, out double wait) ? $"{wait:N0}" : "-", wait > stock ? Tone.Bad : Tone.Dim),
                new TextCell(onTheWay.GetValueOrDefault(model.SupplyKey) >= 0.5 ? $"{onTheWay[model.SupplyKey]:N0}" : "-", Tone.Dim,
                    Tooltip: "Salió de la capital hacia tus tropas y aún no ha llegado. Si una unidad queda aislada, vuelve al almacén."),
                new TextCell(model.EquipmentCost.Items.Length == 0 ? "gratis"
                    : model.Supply != null ? $"{model.Supply.PieceCost.Times(100)} por 100" : model.EquipmentCost.ToString(), Tone.Dim, TextSize.Small, Top: 8),
            ]);
        }
        string title = workshops.Count == 0
            ? "No tienes talleres: constrúyelos (pestaña Edificios de una provincia) para fabricar equipo."
            : $"{TextFormat.Plural(workshops.Count, "taller o fábrica", "talleres y fábricas")}" +
              (idle > 0 ? $" · {TextFormat.Plural(idle, "parado", "parados")}: elige qué fabrican en la pestaña Edificios de su provincia" : "") +
              ". Entrenar, reforzar y modernizar batallones gasta este equipo, que sale de la capital hacia las tropas en envíos; los suministros son además su munición.";
        return new TablePage(new Table(columns, rows, Empty: "No sabes fabricar equipo todavía."), "Equipo · " + title, idle > 0 || workshops.Count == 0 ? Tone.Accent : Tone.Dim);
    }
}
