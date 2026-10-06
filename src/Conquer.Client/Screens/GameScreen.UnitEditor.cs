using Conquer.Client.Graphics;
using Conquer.Client.UI;
using Conquer.Game.Rules;
using Conquer.Presentation;

namespace Conquer.Client.Screens;

/// <summary>
/// Draws the window to edit one of the player's units (<see cref="GameController.UnitEditor"/>): its name, the
/// battalions to split off, the units to merge with and, for those with one, the officer and the reserve. Lists stop
/// where the window ends.
/// </summary>
public sealed partial class GameScreen
{
    /// <summary>For <c>--panel edit</c>: recruits four officers, puts the first at the head of the unit and opens its editor.</summary>
    private void ShowSampleOfficers(int unitId)
    {
        Human.Stockpile[Game.Economy.ResourceType.Gold] += 4 * MilitaryRules.OfficerCost;
        for (int i = 0; i < 4; i++) _session.RecruitOfficer(Human.Id);
        _session.AssignOfficer(Human.Id, unitId, Human.OfficerReserve[0].Id);
        if (_session.UnitById(unitId) is { } unit) _game.OpenUnitEditor(unit);
    }

    private void DrawUnitEditor()
    {
        if (_game.UnitEditor() is not { } editor) return;
        var s = _app.ScreenSize;
        Batch.Rect(0, 0, s.X, s.Y, Rgba.Black.WithAlpha(0.45f));
        Ui.Block(new Rect(0, 0, s.X, s.Y));
        float width = Math.Min(860, s.X - 32), height = Math.Min(620, s.Y - 100);
        var panel = new Rect(s.X / 2 - width / 2, s.Y / 2 - height / 2, width, height);
        Ui.Panel(panel);
        float x = panel.X + 24, y = panel.Y + 20;
        Ui.Text(x, y, editor.Title, Theme.Accent, FontSize.Large, bold: true);
        y += 40;

        float column = editor.Officer != null ? (panel.W - 48 - 24) / 2 : panel.W - 48;
        float bottom = panel.Bottom - 64;
        float left = y;
        NameSection(editor, x, ref left, column);
        if (editor.BattalionsTitle != null)
        {
            // Parts and organising first: a division's battalions can run past the window.
            PartsSection(editor, x, ref left, column, bottom);
            MergeSection(editor, x, ref left, column, bottom);
            BattalionSection(editor, x, ref left, column, bottom);
        }
        if (editor.Officer != null)
        {
            float right = y;
            OfficerSection(editor.Officer, x + column + 24, ref right, column, bottom);
        }

        if (Ui.Button(new Rect(panel.Right - 24 - 160, panel.Bottom - 52, 160, 36), "Cerrar")) _game.CloseUnitEditor();
    }

    private void NameSection(UnitEditorWindow editor, float x, ref float y, float w)
    {
        Ui.Text(x, y, "Nombre", Theme.Text, bold: true);
        y += 26;
        _game.UnitName = Ui.TextField(new Rect(x, y, w - 118, 34), _game.UnitName, MilitaryRules.MaxUnitNameLength);
        DocumentView.Press(Ui, editor.Rename, new Rect(x + w - 110, y, 110, 34));
        y += 40;
        if (editor.AutomaticName != null)
        {
            DocumentView.Press(Ui, editor.AutomaticName, new Rect(x, y, w, 26));
            y += 32;
        }
        y += 8;
    }

    /// <summary>The battalions (or ships), each a toggle; the chosen ones leave together as a new unit.</summary>
    private void BattalionSection(UnitEditorWindow editor, float x, ref float y, float w, float bottom)
    {
        Ui.Text(x, y, editor.BattalionsTitle!, Theme.Text, bold: true);
        y += 26;
        for (int i = 0; i < editor.Battalions.Count && y + 26 < bottom - 80; i++)
        {
            DocumentView.Press(Ui, editor.Battalions[i], new Rect(x, y, w, 24));
            y += 28;
        }
        if (editor.Split != null) DocumentView.Press(Ui, editor.Split, new Rect(x, y, w, 30));
        y += 44;
    }

    /// <summary>The player's other units in the province: to join this one, go into it or take it in.</summary>
    private void MergeSection(UnitEditorWindow editor, float x, ref float y, float w, float bottom)
    {
        if (editor.Merges.Count == 0) return;
        Ui.Text(x, y, "Unir e incorporar", Theme.Text, bold: true);
        y += 26;
        foreach (var merge in editor.Merges)
        {
            if (y + 28 > bottom) break;
            DocumentView.Press(Ui, merge, new Rect(x, y, w, 26));
            y += 30;
        }
        y += 10;
    }

    /// <summary>A brigade's or division's parts, each with its button to leave as a unit of its own.</summary>
    private void PartsSection(UnitEditorWindow editor, float x, ref float y, float w, float bottom)
    {
        if (editor.Parts.Count == 0) return;
        Ui.Text(x, y, "Partes", Theme.Text, bold: true);
        y += 26;
        foreach (var part in editor.Parts)
        {
            if (y + 28 > bottom) break;
            DocumentView.Press(Ui, part, new Rect(x, y, w, 26));
            y += 30;
        }
        y += 10;
    }

    /// <summary>Who leads the unit, the reserve to choose a replacement from and the button to recruit another.</summary>
    private void OfficerSection(OfficerColumn officers, float x, ref float y, float w, float bottom)
    {
        Ui.Text(x, y, officers.Title, Theme.Text, bold: true);
        y += 26;
        if (officers.Current is { } current)
        {
            OfficerCard(current, x, ref y, w);
            if (officers.Relieve != null) DocumentView.Press(Ui, officers.Relieve, new Rect(x, y, w, 28));
            y += 36;
        }
        else
        {
            Ui.Text(x, y, officers.Nobody ?? "", Theme.TextDim, FontSize.Small);
            y += 28;
        }

        Ui.Text(x, y, officers.ReserveTitle, Theme.Text, bold: true);
        y += 26;
        if (officers.EmptyReserve != null)
        {
            Ui.Text(x, y, officers.EmptyReserve, Theme.TextDim, FontSize.Small);
            y += 24;
        }
        int shown = 0;
        foreach (var officer in officers.Reserve)
        {
            if (y + 34 > bottom - 44) break;
            shown++;
            PortraitPainter.Draw(Batch, new Rect(x, y, 30, 30), officer.Portrait);
            Ui.Text(x + 36, y, officer.Title, Theme.Text, FontSize.Small, bold: true);
            Ui.Text(x + 36, y + 15, officer.Summary, Theme.Of(officer.SummaryInk), FontSize.Small);
            if (Ui.Hover(new Rect(x, y, w - 150, 30))) Ui.Tooltip(officer.Tooltip);
            DocumentView.Press(Ui, officer.Assign, new Rect(x + w - 144, y + 2, 84, 26));
            DocumentView.Press(Ui, officer.Retire, new Rect(x + w - 54, y + 2, 54, 26));
            y += 36;
        }
        if (shown < officers.Reserve.Count)
        {
            Ui.Text(x, y, string.Format(officers.Hidden, officers.Reserve.Count - shown), Theme.TextDim, FontSize.Small);
            y += 22;
        }
        DocumentView.Press(Ui, officers.Recruit, new Rect(x, Math.Max(y, bottom - 36), w, 32));
    }

    /// <summary>An officer's portrait, and beside it their title, stars and each trait on its own line, virtues in green and flaws in red.</summary>
    private void OfficerCard(OfficerCard officer, float x, ref float y, float w)
    {
        const float portrait = 72;
        float top = y;
        PortraitPainter.Draw(Batch, new Rect(x, y, portrait, portrait), officer.Portrait);
        float tx = x + portrait + 10, tw = w - portrait - 10;
        Ui.Text(tx, y, officer.Title, Theme.Accent, bold: true);
        Ui.Text(tx + tw - Ui.Font.Measure(officer.Stars, FontSize.Normal, true), y, officer.Stars, Theme.Accent, bold: true);
        if (Ui.Hover(new Rect(x, y, w, portrait))) Ui.Tooltip(officer.Tooltip);
        y += 24;
        foreach (var (text, ink) in officer.Traits)
        {
            Ui.Text(tx + 4, y, Ui.Font.Wrap(text, tw - 4, FontSize.Small).First(), Theme.Of(ink), FontSize.Small);
            y += 18;
        }
        y = Math.Max(y, top + portrait) + 8;
    }
}
