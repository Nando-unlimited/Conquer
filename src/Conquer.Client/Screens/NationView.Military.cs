using Conquer.Client.Graphics;
using Conquer.Client.UI;
using Conquer.Game.Entities;
using Conquer.Game.Economy;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Simulation;

namespace Conquer.Client.Screens;

/// <summary>The Ejército (order of battle) and Diplomacia tabs of the nation screen.</summary>
public sealed partial class NationView
{
    private static readonly Rgba StrengthBar = new(0xFF6FBF5A);
    private static readonly Rgba OrganisationBar = new(0xFFE0B656);

    // ------------------------------------------------------------------ army

    /// <summary>
    /// The order of battle: every HQ with the units under it, as a tree, then the regiments without an
    /// HQ. Each row shows where the unit is, its men, organisation, supply and what it is doing.
    /// </summary>
    private void Army(Ui ui, Rect r)
    {
        var mine = _session.Units.Where(u => u.OwnerId == _player.Id && (u.CommandLevel >= 0 || u.IsFleet)).ToList();
        var regiments = mine.Where(u => u.IsMilitary).ToList();
        ui.Text(r.X, r.Y, $"{Plural(regiments.Count, "unidad de combate", Formations.CombatPlural)} · {Formations.BattalionCount(regiments.Sum(u => u.Battalions.Count))} · {regiments.Sum(u => u.Citizens):N0} hombres · " +
                          $"poder militar {_session.MilitaryPower(_player.Id):0} · mantenimiento {_session.Upkeep(_player)[(int)ResourceType.Gold]:0.#} de oro/día" +
                          (_player.ArmyUnpaid ? " (sin pagar)" : ""), _player.ArmyUnpaid ? Theme.Bad : Theme.Text, bold: true);
        var body = new Rect(r.X, r.Y + 34, r.W, r.H - 34);
        if (mine.Count == 0)
        {
            ui.Text(body.X, body.Y, $"No tienes ejército. Entrena batallones en la pestaña Ejército de tus ciudades.", Theme.TextDim);
            return;
        }

        var rows = new List<(Unit Unit, int Depth)>();
        void AddTree(Unit unit, int depth)
        {
            rows.Add((unit, depth));
            foreach (var sub in _session.SubordinatesOf(unit).OrderByDescending(u => u.CommandLevel).ThenBy(u => u.Name)) AddTree(sub, depth + 1);
        }
        foreach (var top in mine.Where(u => u.IsHeadquarters && _session.CommanderOf(u) is null).OrderByDescending(u => u.HeadquartersLevel).ThenBy(u => u.Name))
            AddTree(top, 0);
        foreach (var loose in regiments.Where(u => _session.CommanderOf(u) is null).OrderBy(u => u.Name)) rows.Add((loose, 0));
        foreach (var fleet in mine.Where(u => u.IsFleet).OrderBy(u => u.Name)) rows.Add((fleet, 0));

        (string Title, float Width)[] columns = [("Unidad", 290), ("Ubicación", 170), ("Hombres", 100), ("Organización", 130), ("Suministro", 110), ("Estado", 170), ("", 60)];
        float x0 = body.X;
        foreach (var (title, width) in columns)
        {
            ui.Text(x0 + 8, body.Y + 5, title, Theme.TextDim, FontSize.Small);
            x0 += width;
        }
        foreach (var ((unit, depth), rowY) in Rows(ui, body, body.Y + 30, rows))
        {
            float x = body.X + 8;
            string name = (depth > 0 ? "· " : "") + unit.Name;
            var nameColor = unit.IsHeadquarters ? Theme.Accent : Theme.Text;
            if (unit.CommanderId.HasValue && !_session.InCommandRange(unit)) nameColor = Theme.Bad;
            ui.Text(x + depth * 18, rowY + 6, name, nameColor, bold: unit.IsHeadquarters);
            x += columns[0].Width;

            var p = _session.Map.Provinces[unit.ProvinceId];
            ui.Text(x, rowY + 6, _session.PlaceName(p), Theme.TextDim);
            x += columns[1].Width;

            if (unit.IsMilitary || unit.IsFleet)
            {
                ui.Text(x, rowY + 6, $"{unit.Citizens:N0}", unit.StrengthShare < 0.5 ? Theme.Bad : Theme.Text);
                ui.Batch.Rect(x, rowY + 26, columns[2].Width - 20, 3, Theme.ButtonDisabled);
                ui.Batch.Rect(x, rowY + 26, (columns[2].Width - 20) * (float)unit.StrengthShare, 3, StrengthBar);
                x += columns[2].Width;
                ProgressBar(ui, new Rect(x, rowY + 13, columns[3].Width - 20, 8), unit.OrganisationShare, OrganisationBar);
                x += columns[3].Width;
                bool supplied = unit.IsAboard || _session.IsInSupply(unit);
                if (unit.IsFleet) ui.Text(x, rowY + 6, _session.IsPort(p, unit.OwnerId) ? "Puerto" : "En el mar", Theme.TextDim);
                else ui.Text(x, rowY + 6, supplied ? "Sí" : "No", supplied ? Theme.Good : Theme.Bad);
            }
            else
            {
                int subs = _session.SubordinatesOf(unit).Count();
                ui.Text(x, rowY + 6, $"{subs}/{CommandLevels.Info(unit.HeadquartersLevel).MaxSubordinates} al mando", Theme.TextDim, FontSize.Small);
                x += columns[2].Width + columns[3].Width;
            }
            x = body.X + 8 + columns.Take(5).Sum(c => c.Width);
            ui.Text(x, rowY + 6, UnitActivity(unit), _session.InBattle(unit) ? Theme.Bad : Theme.TextDim, FontSize.Small);
            x += columns[5].Width;
            if (ui.Button(new Rect(x, rowY + 3, columns[6].Width - 6, RowHeight - 6), "Ver", tooltip: "Seleccionar en el mapa", size: FontSize.Small))
            {
                Visible = false;
                _viewUnit(unit.Id);
            }
        }
    }

    private static string Plural(int n, string one, string many) => $"{n:N0} {(n == 1 ? one : many)}";

    /// <summary>Daily upkeep in words: "0,3 oro/día" or "1,2 oro, 0,4 hierro/día".</summary>
    public static string UpkeepText(IEnumerable<ResourceCost> costs)
    {
        var upkeep = new double[Resources.All.Length];
        foreach (var cost in costs) GameSession.AddUpkeep(upkeep, cost);
        var parts = Resources.All.Where(r => upkeep[(int)r] > 0).Select(r => $"{upkeep[(int)r]:0.##} {r.Name().ToLowerInvariant()}").ToList();
        return parts.Count == 0 ? "nada" : string.Join(", ", parts) + "/día";
    }

    /// <summary>"Tarda 16 días (20 sin tus avances militares)." or, with none that speed it up, "Tarda 20 días."</summary>
    public static string TrainingDaysText(int days, int baseDays) =>
        days < baseDays ? $"Tarda {days} días ({baseDays} sin tus avances militares)." : $"Tarda {days} días.";

    private string UnitActivity(Unit unit)
    {
        if (unit.CarrierId is int carrier && _session.UnitById(carrier) is { } fleet) return $"A bordo de {fleet.Name}";
        if (unit.AttackingProvinceId is int target) return $"Atacando {_session.PlaceName(_session.Map.Provinces[target])}";
        if (_session.InBattle(unit)) return "Defendiendo";
        if (unit.IsMoving && unit.Destination is int dest) return $"Hacia {_session.PlaceName(_session.Map.Provinces[dest])}";
        return "En reserva";
    }

    // ------------------------------------------------------------------ templates

    private int _selectedTemplateId = -1;

    /// <summary>
    /// The regiment designer: the nation's templates on the left (new, duplicate, delete); on the right
    /// the chosen one's battalions (up to six), buttons to add the battalions it knows, and what a
    /// regiment of that design costs and how it fights.
    /// </summary>
    private void Templates(Ui ui, Rect r)
    {
        if (_session.TemplateById(_player, _selectedTemplateId) is not { } template)
        {
            template = _player.Templates[0];
            _selectedTemplateId = template.Id;
        }

        const float ListW = 240;
        float y = r.Y;
        ui.Text(r.X, y, "Plantillas", Theme.Text, bold: true);
        y += 28;
        foreach (var t in _player.Templates)
        {
            if (y > r.Bottom - 130) break;
            if (ui.Button(new Rect(r.X, y, ListW, 30), $"{t.Name}  ({Formations.BattalionCount(t.Battalions.Count)})", active: t.Id == template.Id, size: FontSize.Small))
                _selectedTemplateId = t.Id;
            y += 34;
        }
        y += 8;
        if (ui.Button(new Rect(r.X, y, ListW, 30), "Nueva plantilla", size: FontSize.Small))
        {
            _show(_session.CreateTemplate(_player.Id));
            _selectedTemplateId = _player.Templates[^1].Id;
        }
        y += 34;
        if (ui.Button(new Rect(r.X, y, ListW, 30), "Duplicar", size: FontSize.Small))
        {
            _show(_session.DuplicateTemplate(_player.Id, template.Id));
            _selectedTemplateId = _player.Templates[^1].Id;
        }
        y += 34;
        if (ui.Button(new Rect(r.X, y, ListW, 30), "Borrar", _player.Templates.Count > 1, tooltip: "Hace falta al menos una plantilla.", size: FontSize.Small))
            _show(_session.DeleteTemplate(_player.Id, template.Id));

        // The chosen template.
        float x = r.X + ListW + 30, w = r.Right - x;
        y = r.Y;
        ui.Text(x, y, template.Name, Theme.Accent, FontSize.Large, bold: true);
        ui.Text(x + ui.Font.Measure(template.Name, FontSize.Large, true) + 16, y + 8,
            $"{Formations.CombatName(template.Battalions.Count)} de {Formations.BattalionCount(template.Battalions.Count)}", Theme.TextDim, FontSize.Small);
        y += 40;
        for (int i = 0; i < MilitaryRules.MaxBattalionsPerUnit; i++)
        {
            var slot = new Rect(x, y, w * 0.6f, 32);
            ui.Batch.Rect(slot.X, slot.Y, slot.W, slot.H, Theme.Button.WithAlpha(0.35f));
            if (i < template.Battalions.Count)
            {
                var info = template.Battalions[i].Info();
                ui.Text(slot.X + 10, slot.Y + 6, Formations.BattalionName(info), Theme.Text);
                string stats = $"A {info.Attack:0.#} · D {info.Defense:0.#} · {info.Men} h";
                ui.Text(slot.Right - 90 - ui.Font.Measure(stats, FontSize.Small), slot.Y + 9, stats, Theme.TextDim, FontSize.Small);
                if (ui.Button(new Rect(slot.Right - 80, slot.Y + 4, 74, 24), "Quitar", template.Battalions.Count > 1, size: FontSize.Small))
                    _show(_session.RemoveFromTemplate(_player.Id, template.Id, i));
            }
            else ui.Text(slot.X + 10, slot.Y + 8, "hueco libre", Theme.TextDisabled, FontSize.Small);
            y += 36;
        }

        y += 10;
        ui.Text(x, y, "Añadir", Theme.Text, bold: true);
        y += 26;
        // Ships are built one by one in ports, never from templates.
        var known = Battalions.All.Where(t => !t.Info().Naval && t.Info().Requires.All(_player.Techs.Contains)).ToList();
        float bw = (w * 0.6f - 12) / 3;
        for (int i = 0; i < known.Count; i++)
        {
            var type = known[i];
            var can = _session.CanAddToTemplate(_player, template, type);
            var button = new Rect(x + i % 3 * (bw + 6), y + i / 3 * 34, bw, 30);
            if (ui.Button(button, "+ " + type.Info().Name, can.Ok, tooltip: can.Ok ? Formations.BattalionName(type.Info()) : can.Message, size: FontSize.Small))
                _show(_session.AddToTemplate(_player.Id, template.Id, type));
        }

        // What a regiment of this design is like.
        float sx = x + w * 0.6f + 30, sw = r.Right - sx;
        float sy = r.Y + 40;
        ui.Text(sx, sy, Formations.CombatName(template.Battalions.Count), Theme.Accent, bold: true);
        sy += 28;
        Row(ui, sx, ref sy, sw, "Hombres", $"{template.Men:N0}");
        Row(ui, sx, ref sy, sw, "Instrucción", $"{GameSession.TrainingDays(_player, template)} días");
        Row(ui, sx, ref sy, sw, "Ataque", $"{template.Attack:0.#}");
        Row(ui, sx, ref sy, sw, "Defensa", $"{template.Defense:0.#}");
        Row(ui, sx, ref sy, sw, "Organización", $"{template.MaxOrganisation:0}");
        Row(ui, sx, ref sy, sw, "Velocidad", $"{template.Speed * GameRules.CitizenSpeedKmh:0.#} km/h");
        Row(ui, sx, ref sy, sw, "Mantenimiento", UpkeepText(template.Battalions.Select(b => b.Info().Cost)));
        sy += 6;
        ui.Text(sx, sy, "Coste", Theme.TextDim);
        sy += 22;
        foreach (var (type, amount) in template.Cost.Items)
            Row(ui, sx + 12, ref sy, sw - 12, type.Name(), $"{amount:0}");
        sy += 10;
        var notes = new List<string> { $"Se entrena entero en la pestaña Ejército de tus ciudades; sus batallones se instruyen a la vez." };
        if (template.AnyMounted) notes.Add("Los montados atacan a la mitad en bosques, pantanos y montañas.");
        foreach (var note in notes)
            foreach (var line in ui.Font.Wrap(note, sw, FontSize.Small))
            {
                ui.Text(sx, sy, line, Theme.TextDim, FontSize.Small);
                sy += ui.Font.LineHeight(FontSize.Small);
            }
    }

    // ------------------------------------------------------------------ diplomacy

    /// <summary>Every other nation: at peace or at war, its army against ours, what each holds of the other, and war or peace.</summary>
    private void Diplomacy(Ui ui, Rect r)
    {
        double ours = _session.MilitaryPower(_player.Id);
        (string Title, float Width)[] columns = [("Nación", 230), ("Relación", 180), ("Poder militar", 170), ("Provincias", 110), ("Ocupación", 190), ("", 150)];
        float x0 = r.X;
        foreach (var (title, width) in columns)
        {
            ui.Text(x0 + 8, r.Y + 5, title, Theme.TextDim, FontSize.Small);
            x0 += width;
        }
        var others = _session.Players.Where(p => p.Id != _player.Id).ToList();
        foreach (var (other, rowY) in Rows(ui, r, r.Y + 30, others))
        {
            float x = r.X + 8;
            ui.Batch.Rect(x, rowY + 9, 16, 16, Rgba.Black);
            ui.Batch.Rect(x + 2, rowY + 11, 12, 12, new Rgba(other.Color));
            ui.Text(x + 24, rowY + 6, other.Name, Theme.Text, bold: true);
            x += columns[0].Width;

            bool war = _session.AtWar(_player.Id, other.Id);
            ui.Text(x, rowY + 6, war ? $"En guerra ({_session.WarDays(_player.Id, other.Id):0} días)" : "En paz", war ? Theme.Bad : Theme.Good);
            x += columns[1].Width;

            double theirs = _session.MilitaryPower(other.Id);
            string ratio = ours <= 0 && theirs <= 0 ? "igual" : theirs <= 0 ? "sin ejército" : ours / theirs >= 1.2 ? "más débil que tú" : ours / theirs <= 0.8 ? "más fuerte que tú" : "parecido al tuyo";
            ui.Text(x, rowY + 6, $"{theirs:0} ({ratio})", theirs > ours * 1.2 ? Theme.Bad : Theme.Text);
            x += columns[2].Width;

            ui.Text(x, rowY + 6, $"{other.Provinces.Count:N0}", Theme.TextDim);
            x += columns[3].Width;

            var map = _session.Map.Provinces;
            int taken = map.Count(p => p.OwnerId == other.Id && p.ControllerId == _player.Id);
            int lost = map.Count(p => p.OwnerId == _player.Id && p.ControllerId == other.Id);
            ui.Text(x, rowY + 6, war || taken + lost > 0 ? $"tomadas {taken} · perdidas {lost}" : "-", lost > taken ? Theme.Bad : Theme.TextDim, FontSize.Small);
            x += columns[4].Width;

            var button = new Rect(x, rowY + 3, columns[5].Width - 6, RowHeight - 6);
            if (war)
            {
                if (ui.Button(button, "Proponer la paz", tooltip: "Las provincias ocupadas vuelven a sus dueños y los ejércitos regresan a casa. La IA solo acepta si la guerra le va mal o se alarga.", size: FontSize.Small))
                    _show(_session.ProposePeace(_player.Id, other.Id));
            }
            else if (ui.Button(button, "Declarar la guerra", tooltip: "Tus ejércitos podrán entrar en sus tierras, atacar sus tropas y ocupar sus provincias.", size: FontSize.Small))
                _show(_session.DeclareWar(_player.Id, other.Id));
        }
    }
}
