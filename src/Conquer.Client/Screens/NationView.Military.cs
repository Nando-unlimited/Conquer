using Conquer.Client.Graphics;
using Conquer.Client.UI;
using Conquer.Game.Entities;
using Conquer.Game.Military;
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
        var mine = _session.Units.Where(u => u.OwnerId == _player.Id && u.CommandLevel >= 0).ToList();
        var regiments = mine.Where(u => u.IsMilitary).ToList();
        var era = _player.ArmyEra;
        ui.Text(r.X, r.Y, $"{Plural(regiments.Count, Formations.LevelName(CommandLevels.Regiment, era).ToLowerInvariant(), Formations.LevelPlural(CommandLevels.Regiment, era))} · {Formations.BattalionCount(regiments.Sum(u => u.Battalions.Count), era)} · {regiments.Sum(u => u.Citizens):N0} hombres · " +
                          $"poder militar {_session.MilitaryPower(_player.Id):0}", Theme.Text, bold: true);
        var body = new Rect(r.X, r.Y + 34, r.W, r.H - 34);
        if (mine.Count == 0)
        {
            ui.Text(body.X, body.Y, $"No tienes ejército. Entrena {Formations.BattalionPlural(era)} en la pestaña Ejército de tus ciudades.", Theme.TextDim);
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
            ui.Text(x, rowY + 6, _session.CityIn(p)?.Name ?? p.Info.Name, Theme.TextDim);
            x += columns[1].Width;

            if (unit.IsMilitary)
            {
                ui.Text(x, rowY + 6, $"{unit.Citizens:N0}", unit.StrengthShare < 0.5 ? Theme.Bad : Theme.Text);
                ui.Batch.Rect(x, rowY + 26, columns[2].Width - 20, 3, Theme.ButtonDisabled);
                ui.Batch.Rect(x, rowY + 26, (columns[2].Width - 20) * (float)unit.StrengthShare, 3, StrengthBar);
                x += columns[2].Width;
                ProgressBar(ui, new Rect(x, rowY + 13, columns[3].Width - 20, 8), unit.OrganisationShare, OrganisationBar);
                x += columns[3].Width;
                bool supplied = _session.IsInSupply(unit);
                ui.Text(x, rowY + 6, supplied ? "Sí" : "No", supplied ? Theme.Good : Theme.Bad);
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

    private string UnitActivity(Unit unit)
    {
        if (unit.AttackingProvinceId is int target) return $"Atacando {_session.CityIn(_session.Map.Provinces[target])?.Name ?? _session.Map.Provinces[target].Info.Name}";
        if (_session.InBattle(unit)) return "Defendiendo";
        if (unit.IsMoving && unit.Destination is int dest) return $"Hacia {_session.CityIn(_session.Map.Provinces[dest])?.Name ?? _session.Map.Provinces[dest].Info.Name}";
        return "En reserva";
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
