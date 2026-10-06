using Conquer.Game.Military;
using Conquer.Game.Simulation;

namespace Conquer.Presentation;

/// <summary>The Equipo tab of the nation screen: the stockpile of equipment and what the workshops make.</summary>
public sealed partial class NationScreen
{
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
        foreach (var (key, pieces) in Player.Equipment.Where(e => e.Value >= 1))
            if (Battalions.ByKey(key) is var (type, index) && !models.Any(m => m.Model.Key == key)) models.Add((type, type.Models()[index]));

        // What the nation's battalions wait for: the pieces of their line's newest model, for the men they have left.
        var waiting = new Dictionary<string, double>();
        foreach (var b in Session.Units.Where(u => u.OwnerId == Player.Id && u.IsMilitary).SelectMany(u => u.Battalions))
        {
            int best = b.Type.BestModel(Player.Techs);
            if (best <= b.Model) continue;
            var model = b.Type.Models()[best];
            waiting[model.Key] = waiting.GetValueOrDefault(model.Key) + model.Pieces * b.StrengthShare;
        }

        Column[] columns = [new("Equipo", 300), new("En almacén", 230), new("Producción", 220), new("Para modernizar", 170), new("Coste por batallón", 220)];
        var rows = new List<IReadOnlyList<Cell>>();
        foreach (var (type, model) in models.OrderBy(m => m.Type).ThenByDescending(m => m.Model.Requires.Length))
        {
            double stock = Player.EquipmentOf(model);
            var making = workshops.Where(p => GameSession.ProductionOf(p)?.Key == model.Key).ToList();
            double perDay = making.Sum(p => GameSession.ProductionRate(p, model));
            bool newest = ReferenceEquals(model, type.ModelFor(Player.Techs));
            rows.Add(
            [
                new TextCell(model.Name, newest ? Tone.Normal : Tone.Dim, Bold: newest, Suffix: $" {type.Line().Name.ToLowerInvariant()}" + (newest ? "" : " · antiguo")),
                new TextCell($"{stock:N0} {model.PieceName}", stock >= model.Pieces ? Tone.Normal : Tone.Dim,
                    Suffix: $" · {Math.Floor(stock / model.Pieces):0} bat."),
                new TextCell(perDay > 0 ? $"{perDay:0.#}/día" : "-", perDay > 0 ? Tone.Good : Tone.Dim,
                    Suffix: making.Count > 0 ? $" · {TextFormat.Plural(making.Count, "taller", "talleres")}" : null,
                    Tooltip: making.Count > 0 ? string.Join(", ", making.Select(p => Session.PlaceName(p))) : null),
                new TextCell(waiting.TryGetValue(model.Key, out double wait) ? $"{wait:N0}" : "-", wait > stock ? Tone.Bad : Tone.Dim),
                new TextCell(model.EquipmentCost.Items.Length == 0 ? "gratis" : model.EquipmentCost.ToString(), Tone.Dim, TextSize.Small, Top: 8),
            ]);
        }
        string title = workshops.Count == 0
            ? "No tienes talleres: constrúyelos (pestaña Edificios de una provincia) para fabricar equipo."
            : $"{TextFormat.Plural(workshops.Count, "taller o fábrica", "talleres y fábricas")}" +
              (idle > 0 ? $" · {TextFormat.Plural(idle, "parado", "parados")}: elige qué fabrican en la pestaña Edificios de su provincia" : "") +
              ". Entrenar, reforzar y modernizar batallones gasta este equipo.";
        return new TablePage(new Table(columns, rows, Empty: "No sabes fabricar equipo todavía."), title, idle > 0 || workshops.Count == 0 ? Tone.Accent : Tone.Dim);
    }
}
