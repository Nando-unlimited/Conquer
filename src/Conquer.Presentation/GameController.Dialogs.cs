using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Simulation;

namespace Conquer.Presentation;

/// <summary>An officer's portrait, title and stars, and each trait on its own line (virtues in green, flaws in red).</summary>
public sealed record OfficerCard(string Title, string Stars, string Tooltip, IReadOnlyList<(string Text, Ink Ink)> Traits, Portrait Portrait);

/// <summary>An officer in the reserve: their title, a summary of their traits, and the buttons to assign or retire them.</summary>
public sealed record ReserveOfficer(string Title, string Summary, Ink SummaryInk, string Tooltip, Button Assign, Button Retire, Portrait Portrait);

/// <summary>
/// The officer column of the unit editor: who leads the unit (or why nobody does), the reserve and the button to recruit.
/// <see cref="Hidden"/> says how many more there are when not all of the reserve fits ("{0}" is the number).
/// </summary>
public sealed record OfficerColumn(string Title, OfficerCard? Current, Button? Relieve, string? Nobody, string ReserveTitle, string? EmptyReserve,
    IReadOnlyList<ReserveOfficer> Reserve, string Hidden, Button Recruit);

/// <summary>
/// The window to edit one of the player's units: its name (typed into <see cref="GameController.UnitName"/>), its
/// battalions or ships to split off, the parts of a brigade or division that can leave it (<see cref="Parts"/>), the units it
/// can merge with or take in and, for those with one, its officer.
/// </summary>
public sealed record UnitEditorWindow(string Title, Button Rename, Button? AutomaticName, string? BattalionsTitle, IReadOnlyList<Button> Battalions,
    Button? Split, IReadOnlyList<Button> Merges, OfficerColumn? Officer, IReadOnlyList<Button> Parts);

/// <summary>
/// The window to lay a road or railway: the destinations nearest first (<see cref="Hidden"/> for those that do not fit),
/// what the chosen route costs and joins, and the buttons to build it or cancel.
/// </summary>
public sealed record RoadWindow(string Title, string Intro, string? None, IReadOnlyList<Button> Destinations, string Hidden,
    IReadOnlyList<(string Label, string Value, Ink Ink)> Details, Button Build, Button Cancel);

/// <summary>The unit editor and the road window: their state, their contents and their orders. Time stops while either is open.</summary>
public sealed partial class GameController
{
    private readonly HashSet<int> _splitSelection = [];
    private RoadKind? _roadKind;
    private int _roadFrom;
    private int _roadTarget = -1;
    private List<(int Province, RoadPlan Plan)> _roadOptions = [];

    // ------------------------------------------------------------------ unit editor

    public int? EditingUnitId { get; private set; }
    /// <summary>The name being typed in the unit editor.</summary>
    public string UnitName { get; set; } = "";

    public void OpenUnitEditor(Unit unit)
    {
        EditingUnitId = unit.Id;
        UnitName = unit.Name;
        _splitSelection.Clear();
    }

    public void CloseUnitEditor() => EditingUnitId = null;

    /// <summary>Gives the unit the name typed (an empty one brings back its automatic name).</summary>
    public void RenameEditedUnit()
    {
        if (EditingUnitId is not int id) return;
        var result = Session.RenameUnit(Human.Id, id, UnitName);
        Show(result);
        if (result.Ok && Session.UnitById(id) is { } unit) UnitName = unit.Name;
    }

    /// <summary>The unit editor while it is open; it closes by itself if the unit is gone or no longer the player's.</summary>
    public UnitEditorWindow? UnitEditor()
    {
        if (EditingUnitId is not int id) return null;
        if (Session.UnitById(id) is not { } unit || unit.OwnerId != Human.Id)
        {
            CloseUnitEditor();
            return null;
        }

        var rename = new Button("Renombrar", RenameEditedUnit, UnitName.Trim() != unit.Name, Size: TextSize.Small);
        var automatic = unit.CustomName == null ? null
            : new Button($"Volver al nombre automático ({unit.AutomaticName})", () =>
            {
                UnitName = "";
                RenameEditedUnit();
            }, Tooltip: "El nombre que le corresponde por su número y su tamaño.", Size: TextSize.Small);

        string? battalionsTitle = null;
        var battalions = new List<Button>();
        Button? split = null;
        var merges = new List<Button>();
        if (unit.IsMilitary || unit.IsFleet)
        {
            _splitSelection.RemoveWhere(i => i >= unit.Battalions.Count);
            battalionsTitle = unit.IsFleet ? $"Barcos ({unit.Battalions.Count})" : $"Batallones ({unit.Battalions.Count})";
            for (int i = 0; i < unit.Battalions.Count; i++)
            {
                var b = unit.Battalions[i];
                int index = i;
                bool chosen = _splitSelection.Contains(i);
                battalions.Add(new Button($"{(chosen ? "[x]" : "[  ]")}  {Formations.BattalionName(b.Info)}  ·  {b.Strength:0}/{b.Info.Men}", () =>
                {
                    if (!_splitSelection.Remove(index)) _splitSelection.Add(index);
                }, Active: chosen, Tooltip: "Marca los que quieras separar.", Size: TextSize.Small));
            }
            var can = _splitSelection.Count == 0
                ? CommandResult.Fail(unit.IsFleet ? "Marca los barcos que quieras separar." : "Marca los batallones que quieras separar.")
                : _splitSelection.Count >= unit.Battalions.Count
                ? CommandResult.Fail(unit.IsFleet ? "Debe quedar al menos un barco." : "Debe quedar al menos un batallón.")
                : unit.AttackingProvinceId.HasValue ? CommandResult.Fail("Está atacando.") : CommandResult.Success();
            split = new Button($"Separar los marcados ({_splitSelection.Count})", () =>
            {
                Show(Session.Split(Human.Id, unit.Id, [.. _splitSelection]));
                _splitSelection.Clear();
            }, can.Ok, Tooltip: can.Ok ? "Salen juntos y forman una unidad nueva, sin oficial." : can.Message, Size: TextSize.Small);

            // The player's other units in the province: their battalions can join this regiment (or their ships this fleet),
            // or they can go into this unit, or this one into them, as a part.
            foreach (var other in Session.Units.Where(u => u.IsFleet == unit.IsFleet && (u.IsMilitary || u.IsFleet) && !u.IsAboard
                                                          && u.OwnerId == Human.Id && u.ProvinceId == unit.ProvinceId && u.Id != unit.Id))
            {
                string size = other.IsFleet ? Formations.ShipCount(other.Battalions.Count) : Formations.BattalionCount(other.Battalions.Count);
                string officer = other.Officer is { } o ? $" Su oficial, {o.Title}, " + (unit.Officer == null ? "toma el mando." : "vuelve a la reserva.") : "";
                var canMerge = Session.CanMerge(unit, other);
                if (unit.IsFleet || canMerge.Ok)
                    merges.Add(new Button($"Unir {other.Name} ({size})", () => Show(Session.Merge(Human.Id, unit.Id, other.Id)), canMerge.Ok,
                        Tooltip: canMerge.Ok ? (unit.IsFleet ? "Sus barcos pasan a esta flota." : "Sus batallones pasan a este regimiento.") + officer : canMerge.Message,
                        Size: TextSize.Small));
                if (unit.IsFleet) continue;
                var canIncorporate = Session.CanIncorporate(unit, other);
                bool intoOther = other.Size > unit.Size;
                merges.Add(new Button(intoOther ? $"Incorporarse a {other.Name}" : $"Incorporar {other.Name} ({size})",
                    () => Show(Session.Incorporate(Human.Id, unit.Id, other.Id)), canIncorporate.Ok,
                    Tooltip: canIncorporate.Ok
                        ? (intoOther ? "Esta unidad pasa a ser parte de aquella, con su número y su nombre." : "Pasa a ser parte de esta unidad, con su número y su nombre." + officer)
                        : canIncorporate.Message,
                    Size: TextSize.Small));
            }
        }

        // A brigade's or division's parts, each of which can leave as a unit of its own.
        var parts = new List<Button>();
        if (unit.IsMilitary && unit.Size != Echelon.Regiment)
        {
            var canDetach = Session.CanDetach(unit);
            var list = GameSession.PartsOf(unit);
            for (int i = 0; i < list.Count; i++)
            {
                int index = i;
                var (name, size, count) = list[i];
                parts.Add(new Button($"Separar {name} ({(size == Echelon.Brigade ? "brigada, " : "")}{Formations.BattalionCount(count)})",
                    () => Show(Session.Detach(Human.Id, unit.Id, index)), canDetach.Ok,
                    Tooltip: canDetach.Ok ? "Sale como una unidad propia en la misma provincia, sin oficial." : canDetach.Message, Size: TextSize.Small));
            }
        }
        return new UnitEditorWindow($"Editar {unit.Name}", rename, automatic, battalionsTitle, battalions, split, merges, unit.HasOfficer ? OfficerColumn(unit) : null,
            parts);
    }

    /// <summary>
    /// Who leads the unit, the reserve of its arm to choose a replacement from (the army's, the navy's or the air
    /// force's: only they may lead it) and the button to recruit another of that arm.
    /// </summary>
    private OfficerColumn OfficerColumn(Unit unit)
    {
        var branch = unit.OfficerBranch;
        string role = unit.IsHeadquarters ? "General" : "Oficial";
        string rank = Officer.RankName(unit.RequiredRank, branch).ToLowerInvariant();
        var reserve = Human.OfficerReserve.Where(o => o.Branch == branch).Select(officer => new ReserveOfficer(officer.Title, officer.Summary,
            officer.Traits.Any(Officer.IsFlaw) ? Tone.Dim : Tone.Good, OfficerTooltip(officer),
            new Button("Asignar", () => Show(Session.AssignOfficer(Human.Id, unit.Id, officer.Id)),
                Tooltip: officer.Rank < unit.RequiredRank ? $"Ascenderá a {rank}." : null, Size: TextSize.Small),
            new Button("Retirar", () => Show(Session.RetireOfficer(Human.Id, officer.Id)), Tooltip: "Deja el servicio para siempre.", Size: TextSize.Small),
            PortraitOf(officer))).ToList();
        int others = Human.OfficerReserve.Count(o => o.Branch != branch);
        var can = Session.CanRecruitOfficer(Human, branch);
        string recruit = branch switch
        {
            OfficerBranch.Navy => "Reclutar oficial de marina",
            OfficerBranch.Air => "Reclutar oficial de aviación",
            _ => "Reclutar oficial",
        };
        return new OfficerColumn($"{role} (rango: {rank})",
            unit.Officer is { } current ? OfficerCard(current, LeadsCavalry(unit)) : null,
            unit.Officer is null ? null : new Button("Relevar del mando", () => Show(Session.RelieveOfficer(Human.Id, unit.Id)),
                Tooltip: "Vuelve a la reserva; la unidad se queda sin oficial.", Size: TextSize.Small),
            unit.Officer is null ? "Sin oficial: ni ventajas ni defectos." : null,
            $"Reserva: {Officer.BranchName(branch)} ({reserve.Count})",
            reserve.Count == 0 ? $"No hay oficiales de {(branch switch { OfficerBranch.Navy => "marina", OfficerBranch.Air => "aviación", _ => "ejército" })} en la reserva."
                                 + (others > 0 ? $" ({others} de otras armas no pueden mandarla.)" : "") : null,
            reserve, "y {0} más",
            new Button($"{recruit} ({MilitaryRules.OfficerCost:0} de oro)", () => Show(Session.RecruitOfficer(Human.Id, branch)), can.Ok,
                Tooltip: can.Ok ? "Se une a la reserva con rasgos al azar: una o dos virtudes, y a veces un defecto." : can.Message, Size: TextSize.Small));
    }

    private OfficerCard OfficerCard(Officer officer, bool cavalry) =>
        new(officer.Title, new string('*', officer.Skill), OfficerTooltip(officer),
            officer.Traits.Select(t => ($"{Officer.TraitName(t)}: {Officer.TraitDescription(t)}", (Ink)(Officer.IsFlaw(t) ? Tone.Bad : Tone.Good))).ToList(),
            PortraitOf(officer, cavalry));

    /// <summary>The officer's portrait in the player's colour and era; <paramref name="cavalry"/> if they lead cavalry.</summary>
    public Portrait PortraitOf(Officer officer, bool cavalry = false) => Portrait.Of(officer, Human.Era, Human.Color, cavalry);

    /// <summary>A regiment that is mostly cavalry: its officer's painted portrait is a cavalryman's.</summary>
    private static bool LeadsCavalry(Unit unit) => unit.IsMilitary && unit.Function == UnitFunction.Cavalry;

    // ------------------------------------------------------------------ road window

    public bool RoadWindowOpen => _roadKind.HasValue;

    /// <summary>Opens the window to lay a road of this kind from this province, with the nearest destination that still needs it chosen.</summary>
    public void OpenRoadWindow(int from, RoadKind kind)
    {
        _roadKind = kind;
        _roadFrom = from;
        _roadOptions = [.. Session.RoadHubs(Human.Id).Where(id => id != from)
            .Select(id => (Province: id, Plan: Session.PlanRoad(Human.Id, from, id, kind)))
            .Where(o => o.Plan != null).Select(o => (o.Province, o.Plan!))
            .OrderBy(o => o.Item2.Hours)];
        _roadTarget = _roadOptions.Where(o => o.Plan.NewLinks > 0).Select(o => o.Province).DefaultIfEmpty(-1).First();
    }

    public void CloseRoadWindow() => _roadKind = null;

    /// <summary>The route being chosen, to draw on the map.</summary>
    public IReadOnlyList<int>? PlannedRoute => RoadWindowOpen ? _roadOptions.FirstOrDefault(o => o.Province == _roadTarget).Plan?.Route : null;

    public RoadWindow? RoadWindow()
    {
        if (_roadKind is not RoadKind kind) return null;
        var info = kind.Info();
        var destinations = _roadOptions.Select(o => new Button(o.Plan.NewLinks == 0
                ? $"{HubName(o.Province)}  ·  ya {(info.Feminine ? "unida" : "unido")}"
                : $"{HubName(o.Province)}  ·  {GameSession.FormatHours(o.Plan.Hours)} de marcha  ·  {o.Plan.NewLinks} tramos",
            () => _roadTarget = o.Province, o.Plan.NewLinks > 0, o.Province == _roadTarget, Size: TextSize.Small)).ToList();

        var details = new List<(string, string, Ink)>();
        if (_roadOptions.FirstOrDefault(o => o.Province == _roadTarget).Plan is { } chosen)
        {
            int engineers = Session.EngineersIn(Human.Id, _roadFrom);
            details.Add(("Tramos nuevos", $"{chosen.NewLinks} de {chosen.Route.Count - 1}", Tone.Normal));
            details.Add(("Coste", chosen.Cost.ToString(), Human.Stockpile.Has(chosen.Cost) ? Tone.Normal : Tone.Bad));
            details.Add(("Trabajo", engineers > 1
                ? $"{chosen.WorkDays} días de un batallón: unos {Math.Ceiling(chosen.WorkDays / (double)engineers):0} con los {engineers} que hay aquí"
                : $"{chosen.WorkDays} días con un batallón de ingenieros", Tone.Normal));
            details.Add(("Une también", chosen.CitiesOnTheWay.Count == 0 ? "ninguna otra ciudad" : string.Join(", ", chosen.CitiesOnTheWay.Select(HubName)), Tone.Normal));
        }

        var can = _roadTarget < 0 ? CommandResult.Fail("Elige adónde va.") : Session.CanBuildRoad(Human.Id, _roadFrom, _roadTarget, kind);
        int from = _roadFrom, target = _roadTarget;
        return new RoadWindow($"{(info.Feminine ? "Nueva" : "Nuevo")} {info.Name.ToLowerInvariant()} desde {HubName(_roadFrom)}",
            "¿Qué ciudad o cuartel general quieres unir? La ruta es la que seguiría un ejército, aprovechando las carreteras que ya hay; " +
            "las ciudades por las que pasa quedan unidas también.",
            _roadOptions.Count == 0 ? "No tienes otra ciudad ni cuartel general a los que llegar." : null, destinations, "y {0} más lejos", details,
            new Button("Construir", () =>
            {
                Show(Session.BuildRoad(Human.Id, from, target, kind));
                CloseRoadWindow();
            }, can.Ok, Tooltip: can.Ok ? "Se paga ahora. Los ingenieros que estén en la ruta la construyen tramo a tramo desde aquí; si se van, la obra se para." : can.Message),
            new Button("Cancelar", CloseRoadWindow));
    }
}
