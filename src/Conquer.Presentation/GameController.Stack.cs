using System.Numerics;
using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Simulation;

namespace Conquer.Presentation;

/// <summary>
/// One unit in a stack's list: its line (as in the battle window, its officer in the tooltip), whether it is the selected one, the button that
/// selects it and, for the player's own combat units, fleets and HQs, the one that opens its editor.
/// </summary>
public sealed record StackRow(UnitEntry Entry, bool Selected, Button Select, Button? Edit);

/// <summary>
/// The list of the units in a stack, opened by clicking it, beside it on the map (<see cref="Anchor"/> is the stack's
/// place on screen): pick one to select it, to move it or to edit it.
/// </summary>
public sealed record StackWindow(string Title, Vector2 Anchor, IReadOnlyList<StackRow> Units, Button Close);

/// <summary>The window listing a stack's units.</summary>
public sealed partial class GameController
{
    /// <summary>The units of the stack whose list is open, top one first; null when closed.</summary>
    private List<int>? _stackList;

    public bool StackListOpen => _stackList != null;

    /// <summary>
    /// Clicking a stack: a lone unit is selected; a stack has its top unit selected (or keeps the selected one, if it is
    /// in it) and opens the list of its units.
    /// </summary>
    public void ClickStack(IReadOnlyList<int> stack)
    {
        if (stack.Count == 0) return;
        if (SelectedUnitId is not int id || !stack.Contains(id)) SelectUnit(stack[0]);
        _stackList = stack.Count > 1 ? [.. stack] : null;
    }

    public void CloseStackList() => _stackList = null;

    /// <summary>
    /// The open stack's list, with the units still standing where it was (those that marched off, were lost or
    /// boarded drop out); it closes by itself when fewer than two are left.
    /// </summary>
    public StackWindow? StackList()
    {
        if (_stackList is not { } ids) return null;
        var first = ids.Select(Session.UnitById).OfType<Unit>().FirstOrDefault();
        var units = first == null ? [] : ids.Select(Session.UnitById).OfType<Unit>()
            .Where(u => u.ProvinceId == first.ProvinceId && u.OwnerId == first.OwnerId && !u.IsAboard && (!u.IsMoving || u.AttackingProvinceId.HasValue)
                        && Session.CanSee(Human.Id, u))
            .ToList();
        if (units.Count < 2)
        {
            CloseStackList();
            return null;
        }

        var rows = new List<StackRow>();
        foreach (var unit in units)
        {
            bool selected = unit.Id == SelectedUnitId, mine = unit.OwnerId == Human.Id;
            int id = unit.Id;
            bool bars = unit.IsMilitary || unit.IsFleet;
            var entry = new UnitEntry(unit.Name, selected ? Tone.Accent : Tone.Normal, "", Tone.Dim,
                StackLine(unit), Tone.Dim, bars ? unit.StrengthShare : 1, bars ? unit.OrganisationShare : 1,
                (unit.Officer?.Title ?? "Sin oficial") + (bars ? $"\nHombres {unit.StrengthShare:P0} · organización {unit.OrganisationShare:P0}" : ""));
            var select = new Button(selected ? "Seleccionada" : "Seleccionar", () => SelectUnit(id), Active: selected,
                Tooltip: mine ? "Selecciónala para moverla (clic derecho en el mapa) o darle órdenes en su panel." : "Selecciónala para ver su panel.",
                Size: TextSize.Small);
            var edit = mine && (unit.IsMilitary || unit.IsFleet || unit.IsHeadquarters)
                ? new Button("Editar", () =>
                {
                    SelectUnit(id);
                    OpenUnitEditor(unit);
                }, Tooltip: "Renombrar, separar y unir tropas, y elegir su oficial.", Size: TextSize.Small)
                : null;
            rows.Add(new StackRow(entry, selected, select, edit));
        }
        var owner = Session.Players[first!.OwnerId];
        string title = $"{units.Count} unidades en {Session.PlaceName(Map.Provinces[first.ProvinceId])}" + (owner.Id == Human.Id ? "" : $" · {owner.Name}");
        // Beside the stack: its counter stands on the province's centre (or over its city when close in).
        var anchor = Camera.MapToScreen(Center(first.ProvinceId));
        return new StackWindow(title, anchor, rows, new Button("x", CloseStackList, Tooltip: "Cerrar (Esc)", Size: TextSize.Small));
    }

    /// <summary>What a unit in a stack's list is: its make-up and men, as in its panel.</summary>
    private static string StackLine(Unit unit) => unit.Type switch
    {
        UnitType.Regiment => $"{Composition(unit)} · {unit.Citizens:N0} hombres",
        UnitType.Headquarters => $"Cuartel general de {Formations.LevelName(unit.HeadquartersLevel).ToLowerInvariant()}",
        UnitType.Fleet => $"Flota de {Formations.ShipCount(unit.Battalions.Count)}",
        _ => $"{unit.Citizens:N0} colonos",
    };
}
