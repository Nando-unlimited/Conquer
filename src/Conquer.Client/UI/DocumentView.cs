using System.Numerics;
using Conquer.Client.Graphics;
using Conquer.Presentation;

namespace Conquer.Client.UI;

/// <summary>Draws a <see cref="Document"/> from Conquer.Presentation top to bottom, and runs the actions of the buttons pressed.</summary>
public static class DocumentView
{
    public static FontSize Size(TextSize size) => size switch
    {
        TextSize.Small => FontSize.Small,
        TextSize.Large => FontSize.Large,
        TextSize.Title => FontSize.Title,
        _ => FontSize.Normal,
    };

    /// <summary>
    /// Lays the document out from (<paramref name="x"/>, <paramref name="y"/>) in a column <paramref name="w"/> wide. The
    /// <see cref="UnitEntry"/> rows that would go below <paramref name="bottom"/> are left out and counted instead.
    /// </summary>
    public static void Draw(Ui ui, Document doc, float x, ref float y, float w, float bottom = float.MaxValue)
    {
        for (int index = 0; index < doc.Elements.Count; index++)
        {
            switch (doc.Elements[index])
            {
                case Heading h:
                    ui.Text(x, y, h.Text, Theme.Of(h.Ink), Size(h.Size), bold: true);
                    y += h.Height;
                    break;
                case Label l:
                    float indent = l.Indent;
                    if (l.Icon is BuildingIcon icon && BuildingIcons.Has(icon.Building))
                    {
                        // The building's picture before its name.
                        BuildingIcons.Draw(ui.Batch, icon.Building, x + indent, y - 1, 20);
                        indent += 26;
                    }
                    ui.Text(x + indent, y, l.Text, Theme.Of(l.Ink), Size(l.Size), l.Bold);
                    if (l.Tooltip != null && ui.Hover(new Rect(x, y, w, l.Height))) ui.Tooltip(l.Tooltip);
                    y += l.Height;
                    break;
                case Paragraph p:
                    foreach (var line in ui.Font.Wrap(p.Text, w - p.Indent, Size(p.Size)))
                    {
                        ui.Text(x + p.Indent, y, line, Theme.Of(p.Ink), Size(p.Size));
                        y += ui.Font.LineHeight(Size(p.Size));
                    }
                    y += p.After;
                    break;
                case Info i:
                    InfoLine(ui, i, x, y, w);
                    y += 24;
                    break;
                case Row r:
                    RowLine(ui, r, x, y, w);
                    y += r.Height;
                    break;
                case Button b:
                    Press(ui, b, new Rect(x, y, w, b.Height));
                    y += b.Height + b.Gap;
                    break;
                case ButtonRow row:
                    float bw = (w - row.Spacing * (row.Buttons.Count - 1)) / row.Buttons.Count;
                    for (int n = 0; n < row.Buttons.Count; n++) Press(ui, row.Buttons[n], new Rect(x + n * (bw + row.Spacing), y, bw, row.Height));
                    y += row.Height + row.Gap;
                    break;
                case Stepper s:
                    StepperLine(ui, s, x, y, w);
                    y += s.Height + s.Gap;
                    break;
                case LabelAndButton lb:
                    ui.Text(x, y, lb.Text, Theme.Of(lb.Ink), FontSize.Small);
                    Press(ui, lb.Button, new Rect(x + w - lb.ButtonWidth, y - 2, lb.ButtonWidth, 22));
                    y += 24;
                    break;
                case Bar bar:
                    ui.Batch.Rect(x, y, w, bar.Thickness, Theme.Of(bar.Track));
                    ui.Batch.Rect(x, y, w * (float)Math.Clamp(bar.Fraction, 0, 1), bar.Thickness, Theme.Of(bar.Fill));
                    if (bar.Mark is double mark) ui.Batch.Rect(x + w * (float)mark, y - 2, 1, 10, Theme.Bad);
                    y += bar.Thickness + bar.Gap;
                    break;
                case Pair pair:
                    var size = Size(pair.Size);
                    ui.Text(x + pair.Indent, y, pair.Label, Theme.TextDim, size);
                    ui.Text(x + w - ui.Font.Measure(pair.Value, size), y, pair.Value, Theme.Of(pair.Ink), size);
                    if (pair.Tooltip != null && ui.Hover(new Rect(x, y, w, pair.Height))) ui.Tooltip(pair.Tooltip);
                    y += pair.Height;
                    break;
                case Banner banner:
                    ui.Batch.Rect(x, y + 3, 12, 12, new Rgba(banner.Color));
                    ui.Text(x + 18, y, banner.Text, Theme.Text, bold: true);
                    if (banner.Right.Length > 0) ui.Text(x + w - ui.Font.Measure(banner.Right, FontSize.Small), y + 2, banner.Right, Theme.TextDim, FontSize.Small);
                    y += 26;
                    break;
                case UnitEntry entry:
                    if (y + 44 > bottom)
                    {
                        int left = doc.Elements.Skip(index).OfType<UnitEntry>().Count();
                        ui.Text(x, y, string.Format(doc.Hidden, left), Theme.TextDim, FontSize.Small);
                        return;
                    }
                    Entry(ui, entry, x, ref y, w);
                    break;
                case Columns columns:
                    ColumnsLine(ui, columns, x, y, w);
                    y += columns.Height;
                    break;
                case Distribution distribution:
                    DistributionBar(ui, distribution, x, ref y, w);
                    break;
                case Space space:
                    y += space.Height;
                    break;
                case IconGrid grid:
                    Grid(ui, grid, x, ref y, w);
                    break;
                case Portraits portraits:
                    float px = x;
                    foreach (var (portrait, caption, tooltip) in portraits.Officers)
                    {
                        var frame = new Rect(px, y, portraits.Size, portraits.Size);
                        PortraitPainter.Draw(ui.Batch, frame, portrait);
                        ui.TextCentered(new Rect(px - 10, y + portraits.Size + 2, portraits.Size + 20, 18), caption, Theme.TextDim, FontSize.Small);
                        if (ui.Hover(frame)) ui.Tooltip(tooltip);
                        px += portraits.Size + 14;
                    }
                    y += portraits.Size + 24;
                    break;
            }
        }
    }

    /// <summary>
    /// Icons in rows: each on a tile, outlined under the mouse with its tooltip; a damaged one is outlined in red with a
    /// bar of its damage. A tile without a picture shows its name.
    /// </summary>
    private static void Grid(Ui ui, IconGrid grid, float x, ref float y, float w)
    {
        const float Gap = 6;
        float size = grid.Size;
        int perRow = Math.Max(1, (int)((w + Gap) / (size + Gap)));
        for (int i = 0; i < grid.Tiles.Count; i++)
        {
            var tile = grid.Tiles[i];
            var r = new Rect(x + i % perRow * (size + Gap), y + i / perRow * (size + Gap), size, size);
            bool hover = ui.Hover(r);
            ui.Batch.RoundedRect(r.X, r.Y, r.W, r.H, Theme.ButtonRadius, Theme.Button.WithAlpha(hover ? 0.9f : 0.55f), Theme.Button.WithAlpha(hover ? 0.7f : 0.35f));
            if (tile.Icon is BuildingIcon icon && BuildingIcons.Has(icon.Building))
                BuildingIcons.Draw(ui.Batch, icon.Building, r.X + 3, r.Y + 3, size - 6);
            else
            {
                var lines = ui.Font.Wrap(tile.Name, size - 6, FontSize.Small);
                float lh = ui.Font.LineHeight(FontSize.Small), ty = r.Y + (size - lines.Count * lh) / 2;
                foreach (string line in lines)
                {
                    ui.TextCentered(new Rect(r.X, ty, r.W, lh), line, Theme.Text, FontSize.Small);
                    ty += lh;
                }
            }
            if (tile.Damage > 0)
            {
                ui.Batch.Rect(r.X + 3, r.Bottom - 6, (r.W - 6) * (float)Math.Clamp(tile.Damage, 0, 1), 3, Theme.Battle);
                ui.Batch.RoundedOutline(r.X, r.Y, r.W, r.H, Theme.ButtonRadius, Theme.Battle);
            }
            else if (hover) ui.Batch.RoundedOutline(r.X, r.Y, r.W, r.H, Theme.ButtonRadius, Theme.Accent);
            if (hover) ui.Tooltip(tile.Tooltip);
        }
        y += (grid.Tiles.Count + perRow - 1) / perRow * (size + Gap) + 4;
    }

    /// <summary>A unit's name and note, a line about it, and its strength and organisation bars side by side; lit under the mouse when it has a tooltip.</summary>
    public static void Entry(Ui ui, UnitEntry e, float x, ref float y, float w)
    {
        var row = new Rect(x, y, w, 40);
        bool hover = e.Tooltip != null && ui.Hover(row);
        if (hover) ui.Batch.Rect(row.X - 4, row.Y - 2, row.W + 8, row.H + 2, Theme.Highlight);
        ui.Text(x, y, e.Name, Theme.Of(e.NameInk), FontSize.Small, bold: true);
        if (e.Right.Length > 0) ui.Text(x + w - ui.Font.Measure(e.Right, FontSize.Small), y, e.Right, Theme.Of(e.RightInk), FontSize.Small);
        y += 18;
        ui.Text(x, y, e.Line, Theme.Of(e.LineInk), FontSize.Small);
        y += 17;
        float half = (w - 6) / 2;
        Meter(ui, new Rect(x, y, half, 4), e.Strength, Theme.Strength);
        Meter(ui, new Rect(x + half + 6, y, half, 4), e.Organisation, Theme.Organisation);
        y += 10;
        if (hover) ui.Tooltip(e.Tooltip!);
    }

    private static void Meter(Ui ui, Rect r, double share, Rgba color)
    {
        ui.Batch.Rect(r.X, r.Y, r.W, r.H, Rgba.Black.WithAlpha(0.7f));
        ui.Batch.Rect(r.X, r.Y, r.W * (float)Math.Clamp(share, 0, 1), r.H, color);
    }

    /// <summary>Draws the button and runs its action if it was pressed this frame.</summary>
    public static void Press(Ui ui, Button b, Rect r)
    {
        // A resource's icon sits at the left of the label, which moves over to make room; so does a building's, when it has
        // one; a battalion's goes in the corner.
        bool building = b.Icon is BuildingIcon icon && BuildingIcons.Has(icon.Building);
        string text = b.Icon is ResourceIcon || building ? "     " + b.Text : b.Text;
        bool pressed = ui.Button(r, text, b.Enabled, b.Active, b.Tooltip, Size(b.Size));
        switch (b.Icon)
        {
            case ResourceIcon resource: Icons.Resource(ui.Batch, resource.Resource, new Vector2(r.X + 14, r.Y + 16), 16); break;
            case BattalionIcon battalion: MapIcons.Battalion(ui.Batch, r.X + 7, r.Y + 7, battalion.Battalion); break;
            case BuildingIcon built:
                float size = r.H - 6;
                BuildingIcons.Draw(ui.Batch, built.Building, r.X + 4, r.Y + 3, size);
                break;
        }
        if (pressed) b.Press();
    }

    private static void InfoLine(Ui ui, Info i, float x, float y, float w)
    {
        string label = i.Label;
        if (i.Icon is ResourceIcon resource)
        {
            Icons.Resource(ui.Batch, resource.Resource, new Vector2(x + 8, y + 10), 15);
            label = "      " + label;
        }
        ui.Text(x, y, label, Theme.TextDim);
        ui.Text(x + 130, y, i.Value, Theme.Of(i.Ink));
        if (i.Tooltip != null && ui.Hover(new Rect(x, y, w, 24))) ui.Tooltip(i.Tooltip);
    }

    private static void RowLine(Ui ui, Row r, float x, float y, float w)
    {
        float left = x + r.Indent;
        if (r.Icon is BattalionIcon battalion)
        {
            MapIcons.Battalion(ui.Batch, left, y + 1, battalion.Battalion);
            left += 26;
        }
        ui.Text(left, y, r.Left, Theme.Of(r.LeftInk), FontSize.Small, r.Bold);
        if (r.Right.Length > 0) ui.Text(x + w - ui.Font.Measure(r.Right, FontSize.Small), y, r.Right, Theme.Of(r.RightInk), FontSize.Small);
        if (r.Tooltip != null && ui.Hover(new Rect(x, y, w, r.Height))) ui.Tooltip(r.Tooltip);
    }

    /// <summary>The label, then each value where its column starts; a value's tooltip covers its column.</summary>
    private static void ColumnsLine(Ui ui, Columns c, float x, float y, float w)
    {
        if (c.Label.Length > 0) ui.Text(x, y, c.Label, Theme.TextDim, Size(c.Size));
        for (int i = 0; i < c.Values.Count; i++)
        {
            var value = c.Values[i];
            float left = x + w - c.FromRight[i], width = c.FromRight[i] - (i + 1 < c.FromRight.Count ? c.FromRight[i + 1] : 0);
            ui.Text(left, y, value.Text, Theme.Of(value.Ink), Size(c.Size));
            if (value.Tooltip != null && ui.Hover(new Rect(left, y, width, 20))) ui.Tooltip(value.Tooltip);
        }
    }

    /// <summary>A bar split between the parts (a groove if all are empty), then a legend line for each, last part first.</summary>
    private static void DistributionBar(Ui ui, Distribution d, float x, ref float y, float w)
    {
        double total = d.Parts.Sum(p => p.Value);
        float bx = x;
        foreach (var part in d.Parts.Where(_ => total > 0))
        {
            float bw = (float)(w * part.Value / total);
            ui.Batch.Rect(bx, y, bw, 16, Theme.Of(part.Ink));
            bx += bw;
        }
        if (total <= 0) ui.Batch.Rect(x, y, w, 16, Theme.ButtonDisabled);
        y += 24;
        foreach (var part in d.Parts.Reverse())
        {
            ui.Batch.Rect(x, y + 5, 12, 12, Theme.Of(part.Ink));
            ui.Text(x + 20, y, part.Label, Theme.TextDim);
            ui.Text(x + w - ui.Font.Measure(part.Amount, FontSize.Normal), y, part.Amount);
            y += 24;
        }
    }

    /// <summary>The buttons that lower the value, the value in an 80-pixel box, and those that raise it.</summary>
    private static void StepperLine(Ui ui, Stepper s, float x, float y, float w)
    {
        const float ValueWidth = 80;
        float bw = (w - ValueWidth) / (s.Less.Count + s.More.Count);
        for (int i = 0; i < s.Less.Count; i++) Press(ui, s.Less[i], new Rect(x + i * bw, y, bw - 4, s.Height));
        float valueX = x + s.Less.Count * bw;
        ui.TextCentered(new Rect(valueX, y, ValueWidth, s.Height), s.Value, Theme.Text, FontSize.Normal, bold: true);
        for (int i = 0; i < s.More.Count; i++) Press(ui, s.More[i], new Rect(valueX + ValueWidth + i * bw, y, bw - 4, s.Height));
    }
}
