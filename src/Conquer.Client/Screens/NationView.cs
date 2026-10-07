using Conquer.Client.Graphics;
using Conquer.Client.UI;
using Conquer.Game.Military;
using System.Numerics;
using Conquer.Presentation;

namespace Conquer.Client.Screens;

/// <summary>Draws the nation screen (<see cref="NationScreen"/>): its tabs, and each page as tables, figures, cards or the unit designer.</summary>
public sealed class NationView(NationScreen screen)
{
    private const float RowHeight = 34;

    /// <summary>How far each tab is scrolled, so it stays put when the player comes back to it.</summary>
    private readonly ScrollState[] _scroll = [.. NationScreen.TabNames.Select(_ => new ScrollState())];

    private ScrollState Scroll => _scroll[(int)screen.Tab];

    public void Frame(Ui ui, Rect area)
    {
        if (!screen.Visible) return;
        ui.Panel(area, opaque: true); // opaque: the map would clutter the tables
        ui.Text(area.X + 20, area.Y + 14, screen.Title, Theme.Accent, FontSize.Large, bold: true);
        float tx = area.X + 40 + ui.Font.Measure(screen.Title, FontSize.Large, true);
        // The tabs share the room up to the close button, 108 pixels each at most.
        var tabs = screen.Tabs;
        float step = Math.Min(108, (area.Right - 130 - tx) / tabs.Count);
        for (int n = 0; n < tabs.Count; n++)
        {
            int i = (int)tabs[n];
            // The science tab carries its picture, if there is one, and its name moves over to make room.
            var tab = new Rect(tx + n * step, area.Y + 12, step - 6, 32);
            bool picture = tabs[n] == NationTab.Science && Icons.HasPicture(ScienceIcon.Name);
            string label = picture ? "     " + NationScreen.TabNames[i] : NationScreen.TabNames[i];
            // A name too long for its tab drops to the small letters.
            var size = ui.Font.Measure(label, FontSize.Normal) > tab.W - 10 ? FontSize.Small : FontSize.Normal;
            if (ui.Button(tab, label, active: screen.Tab == tabs[n], size: size)) screen.Tab = tabs[n];
            if (picture) Icons.Picture(ui.Batch, ScienceIcon.Name, new Vector2(tab.X + 18, tab.Y + tab.H / 2), 22);
        }
        if (ui.Button(new Rect(area.Right - 120, area.Y + 12, 100, 32), "Cerrar", tooltip: "Cerrar (N o Esc)")) screen.Visible = false;

        var content = new Rect(area.X + 20, area.Y + 60, area.W - 40, area.H - 76);
        switch (screen.Page())
        {
            case SummaryPage summary: Summary(ui, content, summary, Scroll); break;
            case TablePage table: TablePage(ui, content, table); break;
            case TablesPage tables: TablesPage(ui, content, tables); break;
            case SciencePage science: Science(ui, content, science, Scroll); break;
            case TemplatesPage templates: Templates(ui, content, templates, Scroll, screen); break;
            case StatisticsPage statistics: Statistics(ui, content, statistics); break;
        }
    }

    // ------------------------------------------------------------------ statistics

    /// <summary>The figure buttons, then the graph on the left with its marks and the legend on the right.</summary>
    private static void Statistics(Ui ui, Rect r, StatisticsPage page)
    {
        for (int i = 0; i < page.Metrics.Count; i++) DocumentView.Press(ui, page.Metrics[i], new Rect(r.X + i * 138, r.Y, 132, 30));
        ui.Text(r.X, r.Y + 44, page.Title, Theme.Text, bold: true);
        if (page.Empty != null)
        {
            ui.Text(r.X, r.Y + 80, page.Empty, Theme.TextDim);
            return;
        }

        const float legendW = 250, axisW = 64, axisH = 28;
        var chart = new Rect(r.X + axisW, r.Y + 84, r.W - legendW - axisW - 20, r.H - 84 - axisH);
        ui.Batch.Rect(chart.X, chart.Y, chart.W, chart.H, Rgba.Black.WithAlpha(0.25f));
        foreach (var tick in page.YTicks)
        {
            float y = chart.Bottom - tick.At * chart.H;
            ui.Batch.Rect(chart.X, y, chart.W, 1, Theme.PanelBorder);
            ui.Text(r.X, y - 9, tick.Text, Theme.TextDim, FontSize.Small);
        }
        foreach (var tick in page.XTicks)
        {
            float x = chart.X + tick.At * chart.W;
            ui.Batch.Rect(x, chart.Y, 1, chart.H, Theme.PanelBorder);
            ui.Text(x + 4, chart.Bottom + 6, tick.Text, Theme.TextDim, FontSize.Small);
        }
        // The player's line last, so it is drawn on top.
        foreach (var s in page.Series.OrderBy(s => s.Player))
        {
            var color = new Rgba(s.Color);
            Vector2 At((float X, float Y) p) => new(chart.X + p.X * chart.W, chart.Bottom - p.Y * chart.H);
            if (s.Points.Count == 1) ui.Batch.Rect(At(s.Points[0]).X - 2, At(s.Points[0]).Y - 2, 4, 4, color);
            for (int i = 1; i < s.Points.Count; i++) ui.Batch.Line(At(s.Points[i - 1]), At(s.Points[i]), color, s.Player ? 3 : 2);
        }

        float lx = chart.Right + 20, ly = chart.Y;
        foreach (var s in page.Series)
        {
            if (ly + 24 > r.Bottom) break;
            ui.Batch.Rect(lx, ly + 3, 16, 16, Rgba.Black);
            ui.Batch.Rect(lx + 2, ly + 5, 12, 12, new Rgba(s.Color));
            ui.Text(lx + 24, ly, ui.Font.Wrap(s.Name, legendW - 100, FontSize.Small).First(), s.Player ? Theme.Accent : Theme.Text, FontSize.Small, s.Player);
            ui.Text(lx + legendW - 70, ly, s.Last, Theme.TextDim, FontSize.Small);
            ly += 26;
        }
    }

    // ------------------------------------------------------------------ summary

    /// <summary>The two columns side by side, scrolling together when the taller does not fit.</summary>
    private static void Summary(Ui ui, Rect r, SummaryPage page, ScrollState scroll)
    {
        float colW = (r.W - 30 - (scroll.Overflows(r.H) ? Ui.ScrollBarWidth + 8 : 0)) / 2;
        float top = ui.BeginScroll(r, scroll), left = top, right = top;
        DocumentView.Draw(ui, page.Left, r.X, ref left, colW);
        DocumentView.Draw(ui, page.Right, r.X + colW + 30, ref right, colW);
        ui.EndScroll(r, scroll, Math.Max(left, right) - top + 10);
    }

    // ------------------------------------------------------------------ tables

    /// <summary>One table under its title; its rows scroll under the column titles, which stay.</summary>
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
        var widths = Fit(table, r.W - Ui.ScrollBarWidth - 6);
        float top = Header(ui, r, table, widths);
        foreach (var (cells, rowY) in Rows(ui, r, top, table.Rows)) Row(ui, table, cells, r.X, rowY, widths);
    }

    /// <summary>Several tables one under the other, each under its title, scrolling all together.</summary>
    private void TablesPage(Ui ui, Rect r, TablesPage page)
    {
        var scroll = Scroll;
        float width = r.W - (scroll.Overflows(r.H) ? Ui.ScrollBarWidth + 6 : 0);
        float top = ui.BeginScroll(r, scroll, RowHeight * 3), y = top;
        foreach (var part in page.Tables)
        {
            if (part.Title != null)
            {
                ui.Text(r.X, y, part.Title, Theme.Of(part.TitleInk), bold: true);
                y += 34;
            }
            var table = part.Table;
            if (table.Rows.Count == 0 && table.Empty != null)
            {
                ui.Text(r.X, y, table.Empty, Theme.TextDim);
                y += 40;
                continue;
            }
            var widths = Fit(table, width);
            y = Header(ui, new Rect(r.X, y, width, RowHeight), table, widths);
            for (int i = 0; i < table.Rows.Count; i++, y += RowHeight)
            {
                // Rows out of view are skipped; the clipping cuts the ones half in view.
                if (y + RowHeight < r.Y || y > r.Bottom) continue;
                RowBackground(ui, new Rect(r.X, y, width, RowHeight), i);
                Row(ui, table, table.Rows[i], r.X, y, widths);
            }
            y += 24;
        }
        ui.EndScroll(r, scroll, y - top);
    }

    /// <summary>A row's cells across its columns.</summary>
    private static void Row(Ui ui, Table table, IReadOnlyList<Cell> cells, float left, float rowY, float[] widths)
    {
        float x = left + 8;
        for (int i = 0; i < cells.Count; i++)
        {
            float width = widths[i];
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

    /// <summary>
    /// The columns' widths, narrowed all in proportion when together they are wider than <paramref name="available"/>,
    /// so the last ones never run past the table's edge.
    /// </summary>
    private static float[] Fit(Table table, float available)
    {
        var widths = table.Columns.Select(c => c.Width).ToArray();
        float total = widths.Sum() + 8;
        if (total > available && available > 0)
            for (int i = 0; i < widths.Length; i++) widths[i] *= available / total;
        return widths;
    }

    /// <summary>The text cut short with "..." to fit in <paramref name="width"/>, or whole if it fits.</summary>
    private static string Clip(Ui ui, string text, float width, FontSize size, bool bold = false)
    {
        if (ui.Font.Measure(text, size, bold) <= width) return text;
        int n = text.Length;
        while (n > 0 && ui.Font.Measure(text[..n].TrimEnd() + "...", size, bold) > width) n--;
        return text[..n].TrimEnd() + "...";
    }

    /// <summary>Every other row shaded, and the one under the mouse outlined.</summary>
    private static void RowBackground(Ui ui, Rect row, int index)
    {
        if (index % 2 == 0) ui.Batch.Rect(row.X, row.Y, row.W, row.H, Theme.Button.WithAlpha(0.35f));
        if (ui.Hover(row)) ui.Batch.Outline(row.X, row.Y, row.W, row.H, Theme.PanelBorder);
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
        if (c.Suffix != null && text.Length > 0) text = ui.Font.Wrap(text, width - 14 - (tx - x) - ui.Font.Measure(c.Suffix, FontSize.Small), size).First();
        else if (text.Length > 0) text = Clip(ui, text, width - 10 - (tx - x), size, c.Bold);
        if (text.Length > 0) ui.Text(tx, rowY + c.Top, text, Theme.Of(c.Ink), size, c.Bold);
        if (c.Suffix != null) ui.Text(tx + ui.Font.Measure(text, size, c.Bold), rowY + c.Top + 2, c.Suffix, Theme.TextDim, FontSize.Small);
        if (c.Bar is { } bar)
        {
            float w = width - bar.Inset;
            ui.Batch.Rect(x, rowY + bar.Top, w, bar.Thickness, Theme.ButtonDisabled);
            ui.Batch.Rect(x, rowY + bar.Top, w * (float)Math.Clamp(bar.Fraction, 0, 1), bar.Thickness, Theme.Of(bar.Fill));
        }
        // A cut text shows whole in the tooltip when the cell has none of its own.
        string? tooltip = c.Tooltip ?? (text != c.Text && c.Suffix == null ? c.Text : null);
        if (tooltip != null && ui.Hover(new Rect(x, rowY, width, RowHeight))) ui.Tooltip(tooltip);
    }

    /// <summary>Draws the column titles; the sortable ones are buttons that sort the table. Returns where rows start.</summary>
    private static float Header(Ui ui, Rect r, Table table, float[] widths)
    {
        float x = r.X;
        for (int i = 0; i < table.Columns.Count; i++)
        {
            string title = table.Columns[i].Title;
            float width = widths[i];
            if (i < table.Sortable)
            {
                string arrow = table.SortColumn == i ? (table.SortAscending ? " ^" : " v") : "";
                if (ui.Button(new Rect(x, r.Y, width - 6, 28), title + arrow, active: table.SortColumn == i, tooltip: "Ordenar", size: FontSize.Small))
                    table.SortBy?.Invoke(i);
            }
            else ui.Text(x + 8, r.Y + 5, Clip(ui, title, width - 12, FontSize.Small), Theme.TextDim, FontSize.Small);
            x += width;
        }
        return r.Y + (table.Sortable > 0 ? 36 : 30);
    }

    /// <summary>
    /// Scrolls the rows with the mouse wheel or the bar along their right, and yields those that fit in view, with
    /// their y.
    /// </summary>
    private IEnumerable<(T Row, float Y)> Rows<T>(Ui ui, Rect r, float top, IReadOnlyList<T> rows)
    {
        var scroll = Scroll;
        var view = new Rect(r.X, top, r.W, r.Bottom - top);
        scroll.Content = rows.Count * RowHeight;
        ui.Wheel(r, scroll, RowHeight * 3);
        float width = r.W - (scroll.Overflows(view.H) ? Ui.ScrollBarWidth + 4 : 0);

        int first = (int)(scroll.Offset / RowHeight);
        for (int i = first; i < rows.Count; i++)
        {
            float y = top + i * RowHeight - scroll.Offset;
            if (y < top - 0.5f) continue;
            if (y + RowHeight > r.Bottom) break;
            RowBackground(ui, new Rect(r.X, y, width, RowHeight), i);
            yield return (rows[i], y);
        }
        ui.ScrollBar(view, scroll);
    }

    // ------------------------------------------------------------------ science

    /// <summary>The points per day, the institutions of the age shown on the right, a button per age and the three branches side by side.</summary>
    private static void Science(Ui ui, Rect r, SciencePage page, ScrollState scroll)
    {
        float y = r.Y, px = r.X;
        if (Icons.HasPicture(ScienceIcon.Name))
        {
            Icons.Picture(ui.Batch, ScienceIcon.Name, new Vector2(r.X + 12, y + 10), 24);
            px += 30;
        }
        ui.Text(px, y, page.Points, Theme.Of(page.PointsInk), FontSize.Normal, bold: true);
        if (ui.Hover(new Rect(r.X, y, px - r.X + ui.Font.Measure(page.Points, FontSize.Normal, true), 24))) ui.Tooltip(page.PointsTooltip);
        float ix = r.Right;
        foreach (var badge in page.Institutions) ix = InstitutionBadge(ui, ix, y - 4, badge) - 24;
        y += 30;
        for (int i = 0; i < page.Eras.Count; i++) DocumentView.Press(ui, page.Eras[i], new Rect(r.X + i * 136, y, 130, 26));
        y += 38;

        // The branches scroll together when the longest does not fit.
        const float Gap = 16;
        var view = new Rect(r.X, y, r.W, r.Bottom - y);
        float colW = (r.W - Gap * 2 - (scroll.Overflows(view.H) ? Ui.ScrollBarWidth + 6 : 0)) / 3;
        float top = ui.BeginScroll(view, scroll), bottom = top;
        for (int i = 0; i < page.Branches.Count; i++)
            bottom = Math.Max(bottom, Branch(ui, new Rect(r.X + i * (colW + Gap), top, colW, view.H), page.Branches[i]));
        ui.EndScroll(view, scroll, bottom - top);
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

    /// <summary>A branch of science, top to bottom; returns where it ends.</summary>
    private static float Branch(Ui ui, Rect r, BranchColumn branch)
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

        const float CardGap = 6;
        foreach (var level in branch.Levels)
        {
            ui.Text(x, y, level.Title, Theme.Of(level.Ink), FontSize.Small);
            y += 16;
            foreach (var card in level.Cards)
            {
                float h = TechCardHeight(ui, r.W, card);
                TechCard(ui, new Rect(x, y, r.W, h), card);
                y += h + CardGap;
            }
        }
        return y;
    }

    /// <summary>A card's height: its name, then its description and its notes, each wrapped to as many lines as they need.</summary>
    private static float TechCardHeight(Ui ui, float w, TechCard card)
    {
        int lines = ui.Font.Wrap(card.Description, w - 20, FontSize.Small).Count + card.Notes.Sum(n => ui.Font.Wrap(n.Text, w - 20, FontSize.Small).Count);
        // The old fixed height left a free line between the description and the notes; keep it as the minimum.
        return Math.Max(88, 30 + 8 + lines * ui.Font.LineHeight(FontSize.Small) + 12);
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
        foreach (var line in ui.Font.Wrap(card.Description, c.W - 20, FontSize.Small))
        {
            ui.Text(x, y, line, Theme.Of(card.DescriptionInk), FontSize.Small);
            y += ui.Font.LineHeight(FontSize.Small);
        }
        // The notes sit at the bottom, below the description however many lines it took.
        var notes = card.Notes.SelectMany(n => ui.Font.Wrap(n.Text, c.W - 20, FontSize.Small).Select(line => (line, n.Ink))).ToList();
        float ny = Math.Max(y + 4, c.Bottom - 8 - notes.Count * ui.Font.LineHeight(FontSize.Small));
        foreach (var (line, ink) in notes)
        {
            ui.Text(x, ny, line, Theme.Of(ink), FontSize.Small);
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
    private static void Templates(Ui ui, Rect r, TemplatesPage page, ScrollState scroll, NationScreen screen)
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
        if (page.Renaming is { } renaming && screen.TemplateNameDraft is { } draft)
        {
            // The name becomes a text field with its buttons beside it; Enter accepts and Escape cancels.
            screen.TemplateNameDraft = ui.TextField(new Rect(x, y, 300, 32), draft, Conquer.Game.Rules.MilitaryRules.MaxUnitNameLength);
            float bx = x + 310;
            foreach (var button in renaming)
            {
                DocumentView.Press(ui, button, new Rect(bx, y + 2, 90, 28));
                bx += 96;
            }
            var keys = ui.Input.KeysPressed;
            if (keys.Contains(Silk.NET.Input.Key.Enter) || keys.Contains(Silk.NET.Input.Key.KeypadEnter)) renaming[0].OnClick?.Invoke();
            else if (keys.Contains(Silk.NET.Input.Key.Escape)) renaming[^1].OnClick?.Invoke();
        }
        else
        {
            ui.Text(x, y, page.Name, Theme.Accent, FontSize.Large, bold: true);
            float kx = x + ui.Font.Measure(page.Name, FontSize.Large, true) + 16;
            ui.Text(kx, y + 8, page.Kind, Theme.TextDim, FontSize.Small);
            if (page.Rename is { } rename)
                DocumentView.Press(ui, rename, new Rect(kx + ui.Font.Measure(page.Kind, FontSize.Small) + 16, y + 4, 130, 26));
        }
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

        // The figures scroll when they do not fit.
        float sx = x + w * 0.6f + 30;
        var view = new Rect(sx, r.Y + 40, r.Right - sx, r.H - 40);
        float top = ui.BeginScroll(view, scroll), sy = top;
        DocumentView.Draw(ui, page.Details, sx, ref sy, view.W - (scroll.Overflows(view.H) ? Ui.ScrollBarWidth + 6 : 0));
        ui.EndScroll(view, scroll, sy - top);
    }
}
