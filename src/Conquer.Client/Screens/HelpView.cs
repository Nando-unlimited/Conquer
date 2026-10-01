using Conquer.Client.Graphics;
using Conquer.Client.UI;
using Conquer.Presentation;

namespace Conquer.Client.Screens;

/// <summary>The in-game help (<see cref="HelpTopics"/>): topics on the left, the chosen one's text on the right, scrollable.</summary>
public sealed class HelpView
{
    private readonly List<(string Text, bool Heading, float Indent)> _lines = [];
    private int _topic;
    private float _scroll;
    private float _wrappedFor = -1;

    /// <summary>Draws the help; true when its Close button was pressed.</summary>
    public bool Frame(Ui ui, Rect area)
    {
        ui.Panel(area, opaque: true);
        ui.Text(area.X + 20, area.Y + 14, "Ayuda", Theme.Accent, FontSize.Large, bold: true);
        bool closed = ui.Button(new Rect(area.Right - 120, area.Y + 12, 100, 32), "Cerrar", tooltip: "Cerrar (F1 o Esc)");

        // Topics down the left.
        float ty = area.Y + 60;
        for (int i = 0; i < HelpTopics.All.Length; i++)
        {
            if (ui.Button(new Rect(area.X + 20, ty, 190, 30), HelpTopics.All[i].Title, active: i == _topic, size: FontSize.Small) && i != _topic)
            {
                _topic = i;
                _scroll = 0;
                _wrappedFor = -1;
            }
            ty += 34;
        }

        var content = new Rect(area.X + 230, area.Y + 60, area.W - 250, area.H - 76);
        if (_wrappedFor != content.W) Layout(ui.Font, content.W);
        if (ui.Hover(content)) _scroll -= ui.Input.Scroll * 60;
        float Height((string Text, bool Heading, float Indent) l) => ui.Font.LineHeight(FontSize.Normal, l.Heading) + (l.Heading ? 10 : 2);
        _scroll = Math.Clamp(_scroll, 0, Math.Max(0, _lines.Sum(Height) - content.H));

        float y = content.Y - _scroll;
        foreach (var line in _lines)
        {
            float h = Height(line);
            if (y >= content.Y - 1 && y + h <= content.Bottom + 1)
                ui.Text(content.X + line.Indent, y + (line.Heading ? 8 : 0), line.Text, line.Heading ? Theme.Accent : Theme.Text, FontSize.Normal, line.Heading);
            y += h;
        }
        return closed;
    }

    /// <summary>Wraps the chosen topic to the panel's width.</summary>
    private void Layout(Font font, float width)
    {
        _wrappedFor = width;
        _lines.Clear();
        foreach (var line in HelpTopics.Lines(_topic))
        {
            if (line.Kind == MarkupKind.Heading) _lines.Add((line.Text, true, 0));
            else if (line.Kind == MarkupKind.Bullet)
            {
                var wrapped = font.Wrap(line.Text, width - 24, FontSize.Normal);
                for (int i = 0; i < wrapped.Count; i++) _lines.Add(((i == 0 ? "· " : "") + wrapped[i], false, i == 0 ? 6 : 18));
            }
            else
            {
                foreach (var l in font.Wrap(line.Text, width, FontSize.Normal)) _lines.Add((l, false, 0));
                _lines.Add(("", false, 0));
            }
        }
    }
}
