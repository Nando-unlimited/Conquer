using Conquer.Game.Buildings;
using Conquer.Game.Military;
using Conquer.Game.Science;

namespace Conquer.Presentation;

/// <summary>The Unidades tab of the nation screen: every kind of battalion and ship, with its figures and the equipment it needs.</summary>
public sealed partial class NationScreen
{
    /// <summary>
    /// The army and the navy group by group, line by line and model by model, from the oldest to the newest: its men,
    /// attack, defence, organisation, speed, training days, the equipment a battalion needs and what it all costs. The
    /// model the nation trains now stands out; the older ones and those still to discover are dimmed.
    /// </summary>
    private TablePage Units()
    {
        Column[] columns = [new("Tipo", 260), new("Hombres", 80), new("Ataque", 70), new("Defensa", 75), new("Organiz.", 80), new("Velocidad", 85),
            new("Instrucción", 95), new("Equipo por batallón", 230), new("Coste total", 220)];
        var rows = new List<IReadOnlyList<Cell>>();
        foreach (var group in Battalions.All.GroupBy(t => t.Line().Group))
        {
            rows.Add([new TextCell(group.Key.Name(), Tone.Accent, TextSize.Large, Bold: true, Top: 4)]);
            foreach (var type in group)
            {
                var line = type.Line();
                // The heavy infantry's last model is the light infantry's: it is listed once, with its own line.
                var models = line.Models.Where(m => Battalions.ByKey(m.Key)?.Type == type).ToList();
                bool titled = models.Count > 1;
                if (titled) rows.Add([new TextCell(line.Name, Bold: true, Indent: 8)]);
                int best = type.BestModel(Player.Techs);
                foreach (var model in models)
                {
                    int index = Array.IndexOf(line.Models, model);
                    bool current = index == best, known = index <= best;
                    var ink = current ? Tone.Normal : Tone.Dim;
                    rows.Add(
                    [
                        new TextCell(model.Name, ink, Bold: current, Indent: titled ? 22 : 8, Suffix: current ? null : known ? " · antiguo" : " · por descubrir",
                            Tooltip: Description(type, model, known)),
                        new TextCell($"{model.Men:N0}", ink),
                        new TextCell($"{model.Attack:0.#}", ink),
                        new TextCell($"{model.Defense:0.#}", ink),
                        new TextCell($"{model.MaxOrganisation:0}", ink),
                        new TextCell($"×{model.Speed:0.0#}", ink),
                        new TextCell($"{model.TrainingDays} días", ink),
                        new TextCell(model.NeedsEquipment ? model.PiecesText(model.Pieces) : "Se construye entero", ink, TextSize.Small, Top: 8),
                        new TextCell(model.Cost.ToString(), Tone.Dim, TextSize.Small, Top: 8,
                            Tooltip: model.NeedsEquipment ? $"Instrucción: {model.TrainingCost}. Equipo: {model.EquipmentCost}." : null),
                    ]);
                }
            }
        }
        return new TablePage(new Table(columns, rows),
            "Tipos de batallón y de barco. Ataque y defensa: daño por hora; velocidad: respecto a un ciudadano a pie. Pasa el ratón por un tipo para ver más.", Tone.Dim);
    }

    /// <summary>What sets the model apart, where it trains and, if the nation does not know it yet, the advances it needs.</summary>
    private static string Description(BattalionType type, BattalionInfo model, bool known)
    {
        var lines = new List<string> { $"{model.Name} ({type.Line().Name.ToLowerInvariant()}, {type.Role().Name().ToLowerInvariant()})." };
        if (GameController.LineNote(type) is { } note) lines.Add(note);
        if (model.Mounted) lines.Add("Lucha peor en bosques, pantanos y montañas.");
        if (model.Flies) lines.Add("Vuela: puede cruzar el mar.");
        if (model.Capacity > 0) lines.Add($"Lleva hasta {model.Capacity:N0} hombres.");
        lines.Add(model.Naval ? "Se construye en un puerto" + (model.Shipyard is BuildingType yard ? $" con {yard.Info().Name.ToLowerInvariant()}." : ".")
            : model.TrainingBuilding(type) is BuildingType building ? $"Se instruye en {(building == BuildingType.Workshop ? "un taller" : "un cuartel")}."
            : "Se instruye en cualquier ciudad.");
        if (model.NeedsEquipment) lines.Add($"Equipo: {model.PiecesText(model.Pieces)} ({model.EquipmentCost} en un taller).");
        if (!known && model.Requires.Length > 0) lines.Add("Requiere " + string.Join(" y ", model.Requires.Select(t => t.Info().Name.ToLowerInvariant())) + ".");
        return string.Join("\n", lines);
    }
}
