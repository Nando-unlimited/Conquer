using Conquer.Client.Graphics;
using Conquer.Client.UI;
using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Simulation;
using Conquer.Presentation;

namespace Conquer.Client.Screens;

/// <summary>
/// The window to edit one of the player's units: rename it, split off several battalions at once, merge it with the
/// others in its province and choose its officer from the nation's reserve (or recruit a new one). Time stops while it is open.
/// </summary>
public sealed partial class GameScreen
{
    private int? _editingUnitId;
    private string _unitName = "";
    private readonly HashSet<int> _splitSelection = [];

    private void OpenUnitEditor(Unit unit)
    {
        _editingUnitId = unit.Id;
        _unitName = unit.Name;
        _splitSelection.Clear();
    }

    private void CloseUnitEditor() => _editingUnitId = null;

    /// <summary>For <c>--panel edit</c>: recruits four officers, puts the first at the head of the unit and opens its editor.</summary>
    private void ShowSampleOfficers(int unitId)
    {
        Human.Stockpile[Game.Economy.ResourceType.Gold] += 4 * MilitaryRules.OfficerCost;
        for (int i = 0; i < 4; i++) _session.RecruitOfficer(Human.Id);
        _session.AssignOfficer(Human.Id, unitId, Human.OfficerReserve[0].Id);
        if (_session.UnitById(unitId) is { } unit) OpenUnitEditor(unit);
    }

    private void RenameEditedUnit()
    {
        if (_editingUnitId is not int id) return;
        var result = _session.RenameUnit(Human.Id, id, _unitName);
        _game.Show(result);
        if (result.Ok && _session.UnitById(id) is { } unit) _unitName = unit.Name;
    }

    private void DrawUnitEditor()
    {
        if (_editingUnitId is not int id || _session.UnitById(id) is not { } unit || unit.OwnerId != Human.Id)
        {
            CloseUnitEditor();
            return;
        }
        var s = _app.ScreenSize;
        Batch.Rect(0, 0, s.X, s.Y, Rgba.Black.WithAlpha(0.45f));
        Ui.Block(new Rect(0, 0, s.X, s.Y));
        float width = Math.Min(860, s.X - 32), height = Math.Min(620, s.Y - 100);
        var panel = new Rect(s.X / 2 - width / 2, s.Y / 2 - height / 2, width, height);
        Ui.Panel(panel);
        float x = panel.X + 24, y = panel.Y + 20;
        Ui.Text(x, y, $"Editar {unit.Name}", Theme.Accent, FontSize.Large, bold: true);
        y += 40;

        bool twoColumns = unit.HasOfficer;
        float column = twoColumns ? (panel.W - 48 - 24) / 2 : panel.W - 48;
        float bottom = panel.Bottom - 64;
        float left = y;
        NameSection(unit, x, ref left, column);
        if (unit.IsMilitary || unit.IsFleet)
        {
            BattalionSection(unit, x, ref left, column, bottom);
            MergeSection(unit, x, ref left, column, bottom);
        }
        if (twoColumns)
        {
            float right = y;
            OfficerSection(unit, x + column + 24, ref right, column, bottom);
        }

        if (Ui.Button(new Rect(panel.Right - 24 - 160, panel.Bottom - 52, 160, 36), "Cerrar")) CloseUnitEditor();
    }

    private void NameSection(Unit unit, float x, ref float y, float w)
    {
        Ui.Text(x, y, "Nombre", Theme.Text, bold: true);
        y += 26;
        _unitName = Ui.TextField(new Rect(x, y, w - 118, 34), _unitName, MilitaryRules.MaxUnitNameLength);
        if (Ui.Button(new Rect(x + w - 110, y, 110, 34), "Renombrar", _unitName.Trim() != unit.Name, size: FontSize.Small)) RenameEditedUnit();
        y += 40;
        if (unit.CustomName != null)
        {
            if (Ui.Button(new Rect(x, y, w, 26), $"Volver al nombre automático ({unit.AutomaticName})", size: FontSize.Small,
                    tooltip: "El nombre que le corresponde por su número y su tamaño."))
            {
                _unitName = "";
                RenameEditedUnit();
            }
            y += 32;
        }
        y += 8;
    }

    /// <summary>The battalions (or ships), each a toggle; the chosen ones leave together as a new unit.</summary>
    private void BattalionSection(Unit unit, float x, ref float y, float w, float bottom)
    {
        _splitSelection.RemoveWhere(i => i >= unit.Battalions.Count);
        Ui.Text(x, y, unit.IsFleet ? $"Barcos ({unit.Battalions.Count})" : $"Batallones ({unit.Battalions.Count})", Theme.Text, bold: true);
        y += 26;
        for (int i = 0; i < unit.Battalions.Count && y + 26 < bottom - 80; i++)
        {
            var b = unit.Battalions[i];
            bool chosen = _splitSelection.Contains(i);
            string label = $"{(chosen ? "[x]" : "[  ]")}  {Formations.BattalionName(b.Info)}  ·  {b.Strength:0}/{b.Info.Men}";
            if (Ui.Button(new Rect(x, y, w, 24), label, active: chosen, size: FontSize.Small, tooltip: "Marca los que quieras separar."))
            {
                if (!_splitSelection.Remove(i)) _splitSelection.Add(i);
            }
            y += 28;
        }
        var can = _splitSelection.Count == 0
            ? CommandResult.Fail(unit.IsFleet ? "Marca los barcos que quieras separar." : "Marca los batallones que quieras separar.")
            : _splitSelection.Count >= unit.Battalions.Count
            ? CommandResult.Fail(unit.IsFleet ? "Debe quedar al menos un barco." : "Debe quedar al menos un batallón.")
            : unit.AttackingProvinceId.HasValue ? CommandResult.Fail("Está atacando.") : CommandResult.Success();
        if (Ui.Button(new Rect(x, y, w, 30), $"Separar los marcados ({_splitSelection.Count})", can.Ok, size: FontSize.Small,
                tooltip: can.Ok ? "Salen juntos y forman una unidad nueva, sin oficial." : can.Message))
        {
            _game.Show(_session.Split(Human.Id, unit.Id, [.. _splitSelection]));
            _splitSelection.Clear();
        }
        y += 44;
    }

    /// <summary>The player's other units in the province that can join this one.</summary>
    private void MergeSection(Unit unit, float x, ref float y, float w, float bottom)
    {
        var others = _session.Units.Where(u => u.IsFleet == unit.IsFleet && (u.IsMilitary || u.IsFleet) && !u.IsAboard
                                               && u.OwnerId == Human.Id && u.ProvinceId == unit.ProvinceId && u.Id != unit.Id).ToList();
        if (others.Count == 0) return;
        Ui.Text(x, y, "Unir con esta unidad", Theme.Text, bold: true);
        y += 26;
        foreach (var other in others)
        {
            if (y + 28 > bottom) break;
            var can = _session.CanMerge(unit, other);
            string size = other.IsFleet ? Formations.ShipCount(other.Battalions.Count) : Formations.BattalionCount(other.Battalions.Count);
            string tip = can.Ok
                ? "Sus tropas pasan a esta unidad." + (other.Officer is { } o ? $" Su oficial, {o.Title}, " + (unit.Officer == null ? "toma el mando." : "vuelve a la reserva.") : "")
                : can.Message;
            if (Ui.Button(new Rect(x, y, w, 26), $"Unir {other.Name} ({size})", can.Ok, tooltip: tip, size: FontSize.Small))
                _game.Show(_session.Merge(Human.Id, unit.Id, other.Id));
            y += 30;
        }
    }

    /// <summary>Who leads the unit, the reserve to choose a replacement from and the button to recruit another.</summary>
    private void OfficerSection(Unit unit, float x, ref float y, float w, float bottom)
    {
        string role = unit.IsHeadquarters ? "General" : "Oficial";
        Ui.Text(x, y, $"{role} (rango: {Officer.RankName(unit.RequiredRank).ToLowerInvariant()})", Theme.Text, bold: true);
        y += 26;
        if (unit.Officer is { } current)
        {
            OfficerCard(current, x, ref y, w);
            if (Ui.Button(new Rect(x, y, w, 28), "Relevar del mando", size: FontSize.Small, tooltip: "Vuelve a la reserva; la unidad se queda sin oficial."))
                _game.Show(_session.RelieveOfficer(Human.Id, unit.Id));
            y += 36;
        }
        else
        {
            Ui.Text(x, y, "Sin oficial: ni ventajas ni defectos.", Theme.TextDim, FontSize.Small);
            y += 28;
        }

        var reserve = Human.OfficerReserve;
        Ui.Text(x, y, $"Reserva ({reserve.Count})", Theme.Text, bold: true);
        y += 26;
        if (reserve.Count == 0)
        {
            Ui.Text(x, y, "No hay oficiales en la reserva.", Theme.TextDim, FontSize.Small);
            y += 24;
        }
        int shown = 0;
        foreach (var officer in reserve.ToList())
        {
            if (y + 34 > bottom - 44) break;
            shown++;
            var row = new Rect(x, y, w - 150, 30);
            Ui.Text(x, y, officer.Title, Theme.Text, FontSize.Small, bold: true);
            Ui.Text(x, y + 15, officer.Summary, officer.Traits.Any(Officer.IsFlaw) ? Theme.TextDim : Theme.Good, FontSize.Small);
            if (Ui.Hover(row)) Ui.Tooltip(GameController.OfficerTooltip(officer));
            if (Ui.Button(new Rect(x + w - 144, y + 2, 84, 26), "Asignar", size: FontSize.Small,
                    tooltip: officer.Rank < unit.RequiredRank ? $"Ascenderá a {Officer.RankName(unit.RequiredRank).ToLowerInvariant()}." : null))
                _game.Show(_session.AssignOfficer(Human.Id, unit.Id, officer.Id));
            if (Ui.Button(new Rect(x + w - 54, y + 2, 54, 26), "Retirar", size: FontSize.Small, tooltip: "Deja el ejército para siempre."))
                _game.Show(_session.RetireOfficer(Human.Id, officer.Id));
            y += 36;
        }
        if (shown < reserve.Count)
        {
            Ui.Text(x, y, $"y {reserve.Count - shown} más", Theme.TextDim, FontSize.Small);
            y += 22;
        }
        var can = _session.CanRecruitOfficer(Human);
        if (Ui.Button(new Rect(x, Math.Max(y, bottom - 36), w, 32), $"Reclutar oficial ({MilitaryRules.OfficerCost:0} de oro)", can.Ok, size: FontSize.Small,
                tooltip: can.Ok ? "Se une a la reserva con rasgos al azar: una o dos virtudes, y a veces un defecto." : can.Message))
            _game.Show(_session.RecruitOfficer(Human.Id));
    }

    /// <summary>An officer's title, stars and each trait on its own line, virtues in green and flaws in red.</summary>
    private void OfficerCard(Officer officer, float x, ref float y, float w)
    {
        Ui.Text(x, y, officer.Title, Theme.Accent, bold: true);
        string stars = new('*', officer.Skill);
        Ui.Text(x + w - Ui.Font.Measure(stars, FontSize.Normal, true), y, stars, Theme.Accent, bold: true);
        if (Ui.Hover(new Rect(x, y, w, 22))) Ui.Tooltip(GameController.OfficerTooltip(officer));
        y += 24;
        foreach (var trait in officer.Traits)
        {
            bool flaw = Officer.IsFlaw(trait);
            Ui.Text(x + 8, y, $"{Officer.TraitName(trait)}: {Officer.TraitDescription(trait)}", flaw ? Theme.Bad : Theme.Good, FontSize.Small);
            y += 18;
        }
        y += 8;
    }
}
