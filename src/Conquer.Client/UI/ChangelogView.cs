using Conquer.Client.Graphics;
using Conquer.Presentation;

namespace Conquer.Client.UI;

/// <summary>Shows the <see cref="Changelog"/>, or other text in the same markup (the <see cref="Credits"/>), as a scrollable panel.</summary>
public sealed class ChangelogView(string title, IReadOnlyList<MarkupLine> text)
{
    private readonly List<(string Text, FontSize Size, bool Bold, Rgba Color, float Indent)> _lines = [];
    private readonly ScrollState _scroll = new();
    private float _wrappedFor = -1;

    public ChangelogView() : this("Historial de versiones", Changelog.Lines) { }

    /// <summary>Draws the panel; true when its Close button was pressed.</summary>
    public bool Frame(Ui ui, Rect area)
    {
        ui.Panel(area);
        ui.Text(area.X + 20, area.Y + 14, title, Theme.Accent, FontSize.Large, bold: true);
        bool closed = ui.Button(new Rect(area.Right - 120, area.Y + 12, 100, 32), "Cerrar");

        var content = new Rect(area.X + 20, area.Y + 60, area.W - 40, area.H - 76);
        if (_wrappedFor != content.W) Layout(ui.Font, content.W);

        _scroll.Content = _lines.Sum(l => ui.Font.LineHeight(l.Size, l.Bold) + (l.Size == FontSize.Large ? 8 : 0));
        ui.Wheel(area, _scroll, 60);

        float y = content.Y - _scroll.Offset;
        foreach (var (text, size, bold, color, indent) in _lines)
        {
            float h = ui.Font.LineHeight(size, bold) + (size == FontSize.Large ? 8 : 0);
            if (y + h > content.Y && y < content.Bottom - ui.Font.LineHeight(size, bold) + 2)
                ui.Text(content.X + indent, y + (size == FontSize.Large ? 8 : 0), text, color, size, bold);
            y += h;
        }
        ui.ScrollBar(content, _scroll);
        return closed;
    }

    private void Layout(Font font, float width)
    {
        _wrappedFor = width;
        width -= Ui.ScrollBarWidth + 12; // room for the scroll bar
        _lines.Clear();
        foreach (var line in text)
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
