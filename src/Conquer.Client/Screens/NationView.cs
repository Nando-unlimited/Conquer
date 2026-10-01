using Conquer.Client.Graphics;
using Conquer.Client.UI;
using Conquer.Game.Military;
using Conquer.Presentation;

namespace Conquer.Client.Screens;

/// <summary>Draws the nation screen (<see cref="NationScreen"/>): its tabs, and each page as tables, figures, cards or the unit designer.</summary>
public sealed class NationView(NationScreen screen)
{
    private const float RowHeight = 34;

    private readonly float[] _scroll = new float[NationScreen.TabNames.Length];

    public void Frame(Ui ui, Rect area)
    {
        if (!screen.Visible) return;
        ui.Panel(area, opaque: true); // opaque: the map would clutter the tables
        ui.Text(area.X + 20, area.Y + 14, screen.Title, Theme.Accent, FontSize.Large, bold: true);
        float tx = area.X + 40 + ui.Font.Measure(screen.Title, FontSize.Large, true);
        for (int i = 0; i < NationScreen.TabNames.Length; i++)
            if (ui.Button(new Rect(tx + i * 108, area.Y + 12, 102, 32), NationScreen.TabNames[i], active: (int)screen.Tab == i)) screen.Tab = (NationTab)i;
        if (ui.Button(new Rect(area.Right - 120, area.Y + 12, 100, 32), "Cerrar", tooltip: "Cerrar (N o Esc)")) screen.Visible = false;

        var content = new Rect(area.X + 20, area.Y + 60, area.W - 40, area.H - 76);
        switch (screen.Page())
        {
            case SummaryPage summary: Summary(ui, content, summary); break;
            case TablePage table: TablePage(ui, content, table); break;
            case SciencePage science: Science(ui, content, science); break;
            case TemplatesPage templates: Templates(ui, content, templates); break;
        }
    }

    // ------------------------------------------------------------------ summary

    private static void Summary(Ui ui, Rect r, SummaryPage page)
    {
        float colW = (r.W - 30) / 2, y = r.Y;
        DocumentView.Draw(ui, page.Left, r.X, ref y, colW);
        y = r.Y;
        DocumentView.Draw(ui, page.Right, r.X + colW + 30, ref y, colW);
    }

    // ------------------------------------------------------------------ tables

    private void TablePage(Ui ui, Rect r, TablePage page)
    {
        if (page.Title != null)
        {
            ui.Text(r.X, r.Y, page.Title, Theme.Of(page.TitleInk), bold: true);
            r = new Rect(r.X, r.Y + 34, r.W, r.H - 34);
        }
        var table = page.Table;
        if (table.Rows.Count == 0 && table.Empty != null)
        {
            ui.Text(r.X, r.Y, table.Empty, Theme.TextDim);
            return;
        }
        float top = Header(ui, r, table);
        foreach (var (cells, rowY) in Rows(ui, r, top, table.Rows))
        {
            float x = r.X + 8;
            for (int i = 0; i < cells.Count; i++)
            {
                float width = table.Columns[i].Width;
                switch (cells[i])
                {
                    case TextCell text: TextCell(ui, text, x, rowY, width); break;
                    case ButtonsCell buttons:
                        int n = buttons.Buttons.Count;
                        float bw = (width - buttons.Inset - 4 * (n - 1)) / n;
                        for (int b = 0; b < n; b++) DocumentView.Press(ui, buttons.Buttons[b], new Rect(x + b * (bw + 4), rowY + 3, bw, RowHeight - 6));
                        break;
                }
                x += width;
            }
        }
    }

    private static void TextCell(Ui ui, TextCell c, float x, float rowY, float width)
    {
        float tx = x + c.Indent;
        if (c.Swatch is uint swatch)
        {
            ui.Batch.Rect(tx, rowY + 9, 16, 16, Rgba.Black);
            ui.Batch.Rect(tx + 2, rowY + 11, 12, 12, new Rgba(swatch));
            tx += 24;
        }
        var size = DocumentView.Size(c.Size);
        string text = c.Text;
        // The suffix always shows; the text gives way to it.
        if (c.Suffix != null && text.Length > 0) text = ui.Font.Wrap(text, width - 14 - ui.Font.Measure(c.Suffix, FontSize.Small), size).First();
        if (text.Length > 0) ui.Text(tx, rowY + c.Top, text, Theme.Of(c.Ink), size, c.Bold);
        if (c.Suffix != null) ui.Text(tx + ui.Font.Measure(text, size, c.Bold), rowY + c.Top + 2, c.Suffix, Theme.TextDim, FontSize.Small);
        if (c.Bar is { } bar)
        {
            float w = width - bar.Inset;
            ui.Batch.Rect(x, rowY + bar.Top, w, bar.Thickness, Theme.ButtonDisabled);
            ui.Batch.Rect(x, rowY + bar.Top, w * (float)Math.Clamp(bar.Fraction, 0, 1), bar.Thickness, Theme.Of(bar.Fill));
        }
        if (c.Tooltip != null && ui.Hover(new Rect(x, rowY, width, RowHeight))) ui.Tooltip(c.Tooltip);
    }

    /// <summary>Draws the column titles; the sortable ones are buttons that sort the table. Returns where rows start.</summary>
    private static float Header(Ui ui, Rect r, Table table)
    {
        float x = r.X;
        for (int i = 0; i < table.Columns.Count; i++)
        {
            var (title, width) = table.Columns[i];
            if (i < table.Sortable)
            {
                string arrow = table.SortColumn == i ? (table.SortAscending ? " ^" : " v") : "";
                if (ui.Button(new Rect(x, r.Y, width - 6, 28), title + arrow, active: table.SortColumn == i, tooltip: "Ordenar", size: FontSize.Small))
                    table.SortBy?.Invoke(i);
            }
            else ui.Text(x + 8, r.Y + 5, title, Theme.TextDim, FontSize.Small);
            x += width;
        }
        return r.Y + (table.Sortable > 0 ? 36 : 30);
    }

    /// <summary>Scrolls the rows with the mouse wheel and yields those that fit in view, with their y.</summary>
    private IEnumerable<(T Row, float Y)> Rows<T>(Ui ui, Rect r, float top, IReadOnlyList<T> rows)
    {
        int t = (int)screen.Tab;
        float visible = r.Bottom - top;
        if (ui.Hover(r)) _scroll[t] -= ui.Input.Scroll * RowHeight * 3;
        _scroll[t] = Math.Clamp(_scroll[t], 0, Math.Max(0, rows.Count * RowHeight - visible));

        int first = (int)(_scroll[t] / RowHeight);
        for (int i = first; i < rows.Count; i++)
        {
            float y = top + i * RowHeight - _scroll[t];
            if (y < top - 0.5f) continue;
            if (y + RowHeight > r.Bottom) break;
            if (i % 2 == 0) ui.Batch.Rect(r.X, y, r.W, RowHeight, Theme.Button.WithAlpha(0.35f));
            if (ui.Hover(new Rect(r.X, y, r.W, RowHeight))) ui.Batch.Outline(r.X, y, r.W, RowHeight, Theme.PanelBorder);
            yield return (rows[i], y);
        }
        if (rows.Count * RowHeight > visible)
        {
            float barH = visible * visible / (rows.Count * RowHeight);
            float barY = top + (visible - barH) * _scroll[t] / (rows.Count * RowHeight - visible);
            ui.Batch.Rect(r.Right - 4, barY, 4, barH, Theme.PanelBorder);
        }
    }

    // ------------------------------------------------------------------ science

    /// <summary>The points per day, the institutions of the age shown on the right, a button per age and the three branches side by side.</summary>
    private static void Science(Ui ui, Rect r, SciencePage page)
    {
        float y = r.Y;
        ui.Text(r.X, y, page.Points, Theme.Of(page.PointsInk), FontSize.Normal, bold: true);
        if (ui.Hover(new Rect(r.X, y, ui.Font.Measure(page.Points, FontSize.Normal, true), 24))) ui.Tooltip(page.PointsTooltip);
        float ix = r.Right;
        foreach (var badge in page.Institutions) ix = InstitutionBadge(ui, ix, y - 4, badge) - 24;
        y += 30;
        for (int i = 0; i < page.Eras.Count; i++) DocumentView.Press(ui, page.Eras[i], new Rect(r.X + i * 136, y, 130, 26));
        y += 38;

        const float Gap = 16;
        float colW = (r.W - Gap * 2) / 3;
        for (int i = 0; i < page.Branches.Count; i++) Branch(ui, new Rect(r.X + i * (colW + Gap), y, colW, r.Bottom - y), page.Branches[i]);
    }

    /// <summary>An institution right-aligned at <paramref name="right"/>, with its adopt button if it has one. Returns where its left edge ended up.</summary>
    private static float InstitutionBadge(Ui ui, float right, float y, InstitutionBadge badge)
    {
        if (badge.Adopt != null)
        {
            DocumentView.Press(ui, badge.Adopt, new Rect(right - 150, y, 150, 28));
            right -= 158;
        }
        float w = ui.Font.Measure(badge.Text, FontSize.Small);
        ui.Text(right - w, y + 7, badge.Text, Theme.Of(badge.Ink), FontSize.Small);
        if (ui.Hover(new Rect(right - w, y, w, 28))) ui.Tooltip(badge.Tooltip);
        return right - w;
    }

    private static void Branch(Ui ui, Rect r, BranchColumn branch)
    {
        float x = r.X, y = r.Y;
        ui.Text(x, y, branch.Name, Theme.Accent, FontSize.Large, bold: true);
        // Priority: - n + and the share of science it brings.
        float px = r.Right - 170;
        DocumentView.Press(ui, branch.Less, new Rect(px, y, 30, 28));
        ui.TextCentered(new Rect(px + 30, y, 34, 28), branch.Priority);
        DocumentView.Press(ui, branch.More, new Rect(px + 64, y, 30, 28));
        ui.Text(px + 102, y + 5, branch.Share, Theme.TextDim);
        y += 40;

        ui.Text(x, y, branch.Status, Theme.Of(branch.StatusInk), FontSize.Small);
        if (branch.Progress is double progress)
        {
            y += 20;
            ProgressBar(ui, new Rect(x, y, r.W, 8), progress, Theme.Accent);
            y += 18;
        }
        else y += 30;

        const float CardH = 88, CardGap = 6;
        foreach (var level in branch.Levels)
        {
            ui.Text(x, y, level.Title, Theme.Of(level.Ink), FontSize.Small);
            y += 16;
            foreach (var card in level.Cards)
            {
                TechCard(ui, new Rect(x, y, r.W, CardH), card);
                y += CardH + CardGap;
            }
        }
    }

    private static void TechCard(Ui ui, Rect c, TechCard card)
    {
        float alpha = card.Known ? 0.3f : 0.7f;
        ui.Batch.RoundedRect(c.X, c.Y, c.W, c.H, Theme.ButtonRadius, Theme.ButtonTop.WithAlpha(alpha), Theme.ButtonBottom.WithAlpha(alpha));
        ui.Batch.RoundedOutline(c.X, c.Y, c.W, c.H, Theme.ButtonRadius, Theme.Of(card.Border));

        float x = c.X + 10, y = c.Y + 6;
        ui.Text(x, y, card.Name, Theme.Of(card.NameInk), bold: true);
        float right = c.Right - 10;
        if (card.Research != null)
        {
            DocumentView.Press(ui, card.Research, new Rect(right - 84, y - 2, 84, 24));
            right -= 92;
        }
        ui.Text(right - ui.Font.Measure(card.State, FontSize.Small), y + 3, card.State, Theme.TextDim, FontSize.Small);
        y += 24;
        foreach (var line in ui.Font.Wrap(card.Description, c.W - 20, FontSize.Small).Take(2))
        {
            ui.Text(x, y, line, Theme.Of(card.DescriptionInk), FontSize.Small);
            y += ui.Font.LineHeight(FontSize.Small);
        }
        float ny = c.Bottom - 8 - card.Notes.Count * ui.Font.LineHeight(FontSize.Small);
        foreach (var (text, ink) in card.Notes)
        {
            ui.Text(x, ny, ui.Font.Wrap(text, c.W - 20, FontSize.Small).First(), Theme.Of(ink), FontSize.Small);
            ny += ui.Font.LineHeight(FontSize.Small);
        }
        if (card.Progress is double progress) ProgressBar(ui, new Rect(c.X + 1, c.Bottom - 4, c.W - 2, 3), progress, Theme.Of(card.ProgressInk));
    }

    private static void ProgressBar(Ui ui, Rect r, double share, Rgba color)
    {
        ui.Batch.Rect(r.X, r.Y, r.W, r.H, Theme.ButtonDisabled);
        ui.Batch.Rect(r.X, r.Y, (float)(r.W * Math.Clamp(share, 0, 1)), r.H, color);
    }

    // ------------------------------------------------------------------ templates

    /// <summary>The templates on the left with their actions; the chosen one's slots and the battalions to add in the middle; its figures on the right.</summary>
    private static void Templates(Ui ui, Rect r, TemplatesPage page)
    {
        const float ListW = 240;
        float y = r.Y;
        ui.Text(r.X, y, "Plantillas", Theme.Text, bold: true);
        y += 28;
        foreach (var button in page.Templates)
        {
            if (y > r.Bottom - 130) break;
            DocumentView.Press(ui, button, new Rect(r.X, y, ListW, 30));
            y += 34;
        }
        y += 8;
        foreach (var action in page.Actions)
        {
            DocumentView.Press(ui, action, new Rect(r.X, y, ListW, 30));
            y += 34;
        }

        float x = r.X + ListW + 30, w = r.Right - x;
        y = r.Y;
        ui.Text(x, y, page.Name, Theme.Accent, FontSize.Large, bold: true);
        ui.Text(x + ui.Font.Measure(page.Name, FontSize.Large, true) + 16, y + 8, page.Kind, Theme.TextDim, FontSize.Small);
        y += 40;
        foreach (var slot in page.Slots)
        {
            var area = new Rect(x, y, w * 0.6f, 32);
            ui.Batch.Rect(area.X, area.Y, area.W, area.H, Theme.Button.WithAlpha(0.35f));
            if (slot.Battalion is BattalionType type)
            {
                MapIcons.Battalion(ui.Batch, area.X + 8, area.Y + 9, type);
                ui.Text(area.X + 36, area.Y + 6, slot.Name, Theme.Text);
                ui.Text(area.Right - 90 - ui.Font.Measure(slot.Stats, FontSize.Small), area.Y + 9, slot.Stats, Theme.TextDim, FontSize.Small);
                if (slot.Remove != null) DocumentView.Press(ui, slot.Remove, new Rect(area.Right - 80, area.Y + 4, 74, 24));
            }
            else ui.Text(area.X + 10, area.Y + 8, slot.Name, Theme.TextDisabled, FontSize.Small);
            y += 36;
        }

        y += 10;
        ui.Text(x, y, "Añadir", Theme.Text, bold: true);
        y += 26;
        float bw = (w * 0.6f - 12) / 3;
        for (int i = 0; i < page.Add.Count; i++)
        {
            // The battalion's icon goes at the left, so the label moves over.
            var button = page.Add[i];
            DocumentView.Press(ui, button with { Text = "     " + button.Text }, new Rect(x + i % 3 * (bw + 6), y + i / 3 * 34, bw, 30));
        }

        float sx = x + w * 0.6f + 30, sy = r.Y + 40;
        DocumentView.Draw(ui, page.Details, sx, ref sy, r.Right - sx);
    }
}
