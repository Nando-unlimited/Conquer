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
        _ => FontSize.Normal,
    };

    /// <summary>Lays the document out from (<paramref name="x"/>, <paramref name="y"/>) in a column <paramref name="w"/> wide.</summary>
    public static void Draw(Ui ui, Document doc, float x, ref float y, float w)
    {
        foreach (var element in doc.Elements)
        {
            switch (element)
            {
                case Heading h:
                    ui.Text(x, y, h.Text, Theme.Of(h.Ink), Size(h.Size), bold: true);
                    y += h.Height;
                    break;
                case Label l:
                    ui.Text(x + l.Indent, y, l.Text, Theme.Of(l.Ink), Size(l.Size), l.Bold);
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
                    y += bar.Thickness + bar.Gap;
                    break;
                case Space space:
                    y += space.Height;
                    break;
            }
        }
    }

    /// <summary>Draws the button and runs its action if it was pressed this frame.</summary>
    public static void Press(Ui ui, Button b, Rect r)
    {
        // A resource's icon sits at the left of the label, which moves over to make room; a battalion's goes in the corner.
        string text = b.Icon is ResourceIcon ? "     " + b.Text : b.Text;
        bool pressed = ui.Button(r, text, b.Enabled, b.Active, b.Tooltip, Size(b.Size));
        switch (b.Icon)
        {
            case ResourceIcon resource: Icons.Resource(ui.Batch, resource.Resource, new Vector2(r.X + 14, r.Y + 16), 16); break;
            case BattalionIcon battalion: MapIcons.Battalion(ui.Batch, r.X + 7, r.Y + 7, battalion.Battalion); break;
        }
        if (pressed) b.OnClick?.Invoke();
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
