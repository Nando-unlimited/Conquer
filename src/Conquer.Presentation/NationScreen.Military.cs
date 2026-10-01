using Conquer.Game.Economy;
using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Simulation;

namespace Conquer.Presentation;

/// <summary>The Ejército (order of battle), Plantillas and Diplomacia tabs of the nation screen.</summary>
public sealed partial class NationScreen
{
    private int _selectedTemplateId = -1;

    // ------------------------------------------------------------------ army

    /// <summary>
    /// The order of battle: every HQ with the units under it, as a tree, then the regiments without an
    /// HQ and the fleets. Each row shows where the unit is, its men, organisation, supply and what it is doing.
    /// </summary>
    private TablePage Army()
    {
        var mine = Session.Units.Where(u => u.OwnerId == Player.Id && (u.CommandLevel >= 0 || u.IsFleet)).ToList();
        var regiments = mine.Where(u => u.IsMilitary).ToList();
        string title = $"{TextFormat.Plural(regiments.Count, "unidad de combate", Formations.CombatPlural)} · {Formations.BattalionCount(regiments.Sum(u => u.Battalions.Count))} · {regiments.Sum(u => u.Citizens):N0} hombres · " +
                       $"poder militar {Session.MilitaryPower(Player.Id):0} · mantenimiento {Session.Upkeep(Player)[(int)ResourceType.Gold]:0.#} de oro/día" +
                       (Player.ArmyUnpaid ? " (sin pagar)" : "");

        var units = new List<(Unit Unit, int Depth)>();
        void AddTree(Unit unit, int depth)
        {
            units.Add((unit, depth));
            foreach (var sub in Session.SubordinatesOf(unit).OrderByDescending(u => u.CommandLevel).ThenBy(u => u.Name)) AddTree(sub, depth + 1);
        }
        foreach (var top in mine.Where(u => u.IsHeadquarters && Session.CommanderOf(u) is null).OrderByDescending(u => u.HeadquartersLevel).ThenBy(u => u.Name))
            AddTree(top, 0);
        foreach (var loose in regiments.Where(u => Session.CommanderOf(u) is null).OrderBy(u => u.Name)) units.Add((loose, 0));
        foreach (var fleet in mine.Where(u => u.IsFleet).OrderBy(u => u.Name)) units.Add((fleet, 0));

        Column[] columns = [new("Unidad", 290), new("Ubicación", 170), new("Hombres", 100), new("Organización", 130), new("Suministro", 110), new("Estado", 170), new("", 60)];
        var rows = new List<IReadOnlyList<Cell>>();
        foreach (var (unit, depth) in units)
        {
            Ink nameInk = unit.CommanderId.HasValue && !Session.InCommandRange(unit) ? Tone.Bad : unit.IsHeadquarters ? Tone.Accent : Tone.Normal;
            var p = Map.Provinces[unit.ProvinceId];
            var cells = new List<Cell>
            {
                new TextCell((depth > 0 ? "· " : "") + unit.Name, nameInk, Bold: unit.IsHeadquarters, Indent: depth * 18),
                new TextCell(Session.PlaceName(p), Tone.Dim),
            };
            if (unit.IsMilitary || unit.IsFleet)
            {
                cells.Add(new TextCell($"{unit.Citizens:N0}", unit.StrengthShare < 0.5 ? Tone.Bad : Tone.Normal, Bar: new CellBar(unit.StrengthShare, Tone.Strength, 26, 3, 20)));
                cells.Add(new TextCell("", Bar: new CellBar(unit.OrganisationShare, Tone.Organisation, 13, 8, 20)));
                bool supplied = unit.IsAboard || Session.IsInSupply(unit);
                cells.Add(unit.IsFleet
                    ? new TextCell(Session.IsPort(p, unit.OwnerId) ? "Puerto" : "En el mar", Tone.Dim)
                    : new TextCell(supplied ? "Sí" : "No", supplied ? Tone.Good : Tone.Bad));
            }
            else
            {
                int subs = Session.SubordinatesOf(unit).Count();
                cells.Add(new TextCell($"{subs}/{CommandLevels.Info(unit.HeadquartersLevel).MaxSubordinates} al mando", Tone.Dim, TextSize.Small));
                cells.Add(new TextCell(""));
                cells.Add(new TextCell(""));
            }
            cells.Add(new TextCell(UnitActivity(unit), Session.InBattle(unit) ? Tone.Bad : Tone.Dim, TextSize.Small));
            cells.Add(new ButtonsCell([new Button("Ver", () => ViewUnit(unit.Id), Tooltip: "Seleccionar en el mapa", Size: TextSize.Small)]));
            rows.Add(cells);
        }
        return new TablePage(new Table(columns, rows, Empty: "No tienes ejército. Entrena batallones en la pestaña Ejército de tus ciudades."),
            title, Player.ArmyUnpaid ? Tone.Bad : Tone.Normal);
    }

    private string UnitActivity(Unit unit)
    {
        if (unit.CarrierId is int carrier && Session.UnitById(carrier) is { } fleet) return $"A bordo de {fleet.Name}";
        if (unit.AttackingProvinceId is int target) return $"Atacando {Session.PlaceName(Map.Provinces[target])}";
        if (Session.InBattle(unit)) return "Defendiendo";
        if (unit.IsMoving && unit.Destination is int dest) return $"Hacia {Session.PlaceName(Map.Provinces[dest])}";
        return "En reserva";
    }

    // ------------------------------------------------------------------ templates

    /// <summary>
    /// The unit designer: the nation's templates (new, duplicate, delete); the chosen one's battalions, buttons to add
    /// the battalions it knows, and what a unit of that design costs and how it fights.
    /// </summary>
    private TemplatesPage Templates()
    {
        if (Session.TemplateById(Player, _selectedTemplateId) is not { } template)
        {
            template = Player.Templates[0];
            _selectedTemplateId = template.Id;
        }

        var list = Player.Templates.Select(t => new Button($"{t.Name}  ({Formations.BattalionCount(t.Battalions.Count)})", () => _selectedTemplateId = t.Id,
            Active: t.Id == template.Id, Size: TextSize.Small)).ToList();
        Button[] actions =
        [
            new("Nueva plantilla", () =>
            {
                Show(Session.CreateTemplate(Player.Id));
                _selectedTemplateId = Player.Templates[^1].Id;
            }, Size: TextSize.Small),
            new("Duplicar", () =>
            {
                Show(Session.DuplicateTemplate(Player.Id, template.Id));
                _selectedTemplateId = Player.Templates[^1].Id;
            }, Size: TextSize.Small),
            new("Borrar", () => Show(Session.DeleteTemplate(Player.Id, template.Id)), Player.Templates.Count > 1, Tooltip: "Hace falta al menos una plantilla.", Size: TextSize.Small),
        ];

        var slots = new List<TemplateSlot>();
        for (int i = 0; i < MilitaryRules.MaxBattalionsPerUnit; i++)
        {
            if (i >= template.Battalions.Count)
            {
                slots.Add(new TemplateSlot(null, "hueco libre", "", null));
                continue;
            }
            var info = template.Battalions[i].Info();
            int index = i;
            slots.Add(new TemplateSlot(template.Battalions[i], Formations.BattalionName(info), $"A {info.Attack:0.#} · D {info.Defense:0.#} · {info.Men} h",
                new Button("Quitar", () => Show(Session.RemoveFromTemplate(Player.Id, template.Id, index)), template.Battalions.Count > 1, Size: TextSize.Small)));
        }

        // Ships are built one by one in ports, never from templates.
        var add = Battalions.All.Where(t => !t.Info().Naval && t.Info().Requires.All(Player.Techs.Contains)).Select(type =>
        {
            var can = Session.CanAddToTemplate(Player, template, type);
            return new Button("+ " + type.Info().Name, () => Show(Session.AddToTemplate(Player.Id, template.Id, type)), can.Ok,
                Tooltip: can.Ok ? Formations.BattalionName(type.Info()) : can.Message, Size: TextSize.Small, Icon: new BattalionIcon(type));
        }).ToList();

        // What a unit of this design is like.
        var details = new Document();
        details.Add(new Heading(Formations.CombatName(template.Battalions.Count), Tone.Accent, Height: 28));
        details.Add(new Pair("Hombres", $"{template.Men:N0}"));
        details.Add(new Pair("Instrucción", $"{GameSession.TrainingDays(Player, template)} días"));
        details.Add(new Pair("Ataque", $"{template.Attack:0.#}"));
        details.Add(new Pair("Defensa", $"{template.Defense:0.#}"));
        details.Add(new Pair("Organización", $"{template.MaxOrganisation:0}"));
        details.Add(new Pair("Velocidad", $"{template.Speed * GameRules.CitizenSpeedKmh:0.#} km/h"));
        details.Add(new Pair("Mantenimiento", TextFormat.UpkeepText(template.Battalions.Select(b => b.Info().Cost))));
        details.Add(new Space(6));
        details.Add(new Label("Coste", Tone.Dim, TextSize.Normal));
        foreach (var (type, amount) in template.Cost.Items) details.Add(new Pair(type.Name(), $"{amount:0}", Indent: 12));
        details.Add(new Space(10));
        details.Add(new Paragraph("Se entrena entero en la pestaña Ejército de tus ciudades; sus batallones se instruyen a la vez.", Tone.Dim));
        if (template.AnyMounted) details.Add(new Paragraph("Los montados atacan a la mitad en bosques, pantanos y montañas.", Tone.Dim));

        return new TemplatesPage(list, actions, template.Name,
            $"{Formations.CombatName(template.Battalions.Count)} de {Formations.BattalionCount(template.Battalions.Count)}", slots, add, details);
    }

    // ------------------------------------------------------------------ diplomacy

    /// <summary>Every other nation: at peace or at war, its army against ours, what each holds of the other, and war or peace.</summary>
    private TablePage Diplomacy()
    {
        double ours = Session.MilitaryPower(Player.Id);
        Column[] columns = [new("Nación", 230), new("Relación", 180), new("Poder militar", 170), new("Provincias", 110), new("Ocupación", 190), new("", 150)];
        var rows = new List<IReadOnlyList<Cell>>();
        foreach (var other in Session.Players.Where(p => p.Id != Player.Id))
        {
            bool war = Session.AtWar(Player.Id, other.Id);
            double theirs = Session.MilitaryPower(other.Id);
            string ratio = ours <= 0 && theirs <= 0 ? "igual" : theirs <= 0 ? "sin ejército" : ours / theirs >= 1.2 ? "más débil que tú" : ours / theirs <= 0.8 ? "más fuerte que tú" : "parecido al tuyo";
            int taken = Map.Provinces.Count(p => p.OwnerId == other.Id && p.ControllerId == Player.Id);
            int lost = Map.Provinces.Count(p => p.OwnerId == Player.Id && p.ControllerId == other.Id);
            rows.Add(
            [
                new TextCell(other.Name, Bold: true, Swatch: other.Color),
                new TextCell(war ? $"En guerra ({Session.WarDays(Player.Id, other.Id):0} días)" : "En paz", war ? Tone.Bad : Tone.Good),
                new TextCell($"{theirs:0} ({ratio})", theirs > ours * 1.2 ? Tone.Bad : Tone.Normal),
                new TextCell($"{other.Provinces.Count:N0}", Tone.Dim),
                new TextCell(war || taken + lost > 0 ? $"tomadas {taken} · perdidas {lost}" : "-", lost > taken ? Tone.Bad : Tone.Dim, TextSize.Small),
                new ButtonsCell([war
                    ? new Button("Proponer la paz", () => Show(Session.ProposePeace(Player.Id, other.Id)),
                        Tooltip: "Las provincias ocupadas vuelven a sus dueños y los ejércitos regresan a casa. La IA solo acepta si la guerra le va mal o se alarga.", Size: TextSize.Small)
                    : new Button("Declarar la guerra", () => Show(Session.DeclareWar(Player.Id, other.Id)),
                        Tooltip: "Tus ejércitos podrán entrar en sus tierras, atacar sus tropas y ocupar sus provincias.", Size: TextSize.Small)]),
            ]);
        }
        return new TablePage(new Table(columns, rows));
    }
}
