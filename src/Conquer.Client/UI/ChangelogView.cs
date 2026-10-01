using Conquer.Client.Graphics;
using Conquer.Presentation;

namespace Conquer.Client.UI;

/// <summary>Shows the <see cref="Changelog"/> as a scrollable panel.</summary>
public sealed class ChangelogView
{
    private readonly List<(string Text, FontSize Size, bool Bold, Rgba Color, float Indent)> _lines = [];
    private float _scroll;
    private float _wrappedFor = -1;

    public bool Visible { get; set; }

    /// <summary>Draws the panel while it is visible; its Close button hides it.</summary>
    public void Frame(Ui ui, Rect area)
    {
        if (!Visible) return;
        ui.Panel(area);
        ui.Text(area.X + 20, area.Y + 14, "Historial de versiones", Theme.Accent, FontSize.Large, bold: true);
        if (ui.Button(new Rect(area.Right - 120, area.Y + 12, 100, 32), "Cerrar")) Visible = false;

        var content = new Rect(area.X + 20, area.Y + 60, area.W - 40, area.H - 76);
        if (_wrappedFor != content.W) Layout(ui.Font, content.W);

        if (ui.Hover(area)) _scroll -= ui.Input.Scroll * 60;
        float total = _lines.Sum(l => ui.Font.LineHeight(l.Size, l.Bold) + (l.Size == FontSize.Large ? 8 : 0));
        _scroll = Math.Clamp(_scroll, 0, Math.Max(0, total - content.H));

        float y = content.Y - _scroll;
        foreach (var (text, size, bold, color, indent) in _lines)
        {
            float h = ui.Font.LineHeight(size, bold) + (size == FontSize.Large ? 8 : 0);
            if (y + h > content.Y && y < content.Bottom - ui.Font.LineHeight(size, bold) + 2)
                ui.Text(content.X + indent, y + (size == FontSize.Large ? 8 : 0), text, color, size, bold);
            y += h;
        }
    }

    private void Layout(Font font, float width)
    {
        _wrappedFor = width;
        _lines.Clear();
        foreach (var line in Changelog.Lines)
        {
            switch (line.Kind)
            {
                case MarkupKind.Heading: _lines.Add((line.Text, FontSize.Large, true, Theme.Accent, 0)); break;
                case MarkupKind.Subheading: _lines.Add((line.Text, FontSize.Normal, true, Theme.Text, 0)); break;
                case MarkupKind.Bullet:
                    var wrapped = font.Wrap(line.Text, width - 24, FontSize.Normal);
                    for (int i = 0; i < wrapped.Count; i++)
                        _lines.Add(((i == 0 ? "· " : "") + wrapped[i], FontSize.Normal, false, Theme.Text, i == 0 ? 6 : 18));
                    break;
                default:
                    foreach (var l in font.Wrap(line.Text, width, FontSize.Normal)) _lines.Add((l, FontSize.Normal, false, Theme.TextDim, 0));
                    break;
            }
        }
    }
}
