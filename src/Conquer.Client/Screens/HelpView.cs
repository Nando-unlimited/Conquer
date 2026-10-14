using Conquer.Client.Graphics;
using Conquer.Client.UI;
using Conquer.Presentation;

namespace Conquer.Client.Screens;

/// <summary>
/// The in-game help, in two books: the help (<see cref="HelpTopics"/>), what to do, and the rules
/// (<see cref="RulesManual"/>), how it is worked out. Tabs at the top choose the book, its topics go down the left and
/// the chosen one's text on the right, scrollable.
/// </summary>
public sealed class HelpView
{
    private readonly List<(string Text, MarkupKind Kind, float Indent)> _lines = [];
    private bool _rules;
    private int _topic;
    private readonly ScrollState _scroll = new();
    private float _wrappedFor = -1;

    private (string Title, string[] Text)[] Topics => _rules ? RulesManual.All : HelpTopics.All;

    /// <summary>Draws the help; true when its Close button was pressed.</summary>
    public bool Frame(Ui ui, Rect area)
    {
        ui.Panel(area, opaque: true);
        if (ui.Button(new Rect(area.X + 20, area.Y + 12, 110, 32), "Ayuda", active: !_rules, tooltip: "Qué hacer y cómo") && _rules) Open(rules: false);
        if (ui.Button(new Rect(area.X + 136, area.Y + 12, 110, 32), "Reglas", active: _rules, tooltip: "Cómo funciona el juego por dentro: fórmulas y cifras") && !_rules) Open(rules: true);
        bool closed = ui.Button(new Rect(area.Right - 120, area.Y + 12, 100, 32), "Cerrar", tooltip: "Cerrar (F1 o Esc)");

        // Topics down the left.
        float ty = area.Y + 60;
        for (int i = 0; i < Topics.Length; i++)
        {
            if (ui.Button(new Rect(area.X + 20, ty, 190, 30), Topics[i].Title, active: i == _topic, size: FontSize.Small) && i != _topic)
            {
                _topic = i;
                _scroll.Reset();
                _wrappedFor = -1;
            }
            ty += 34;
        }

        var content = new Rect(area.X + 230, area.Y + 60, area.W - 250, area.H - 76);
        // The text leaves room for the scroll bar along its right.
        if (_wrappedFor != content.W) Layout(ui.Font, content.W);
        _scroll.Content = _lines.Sum(l => Height(ui.Font, l.Kind));
        ui.Wheel(content, _scroll, 60);

        float y = content.Y - _scroll.Offset;
        foreach (var line in _lines)
        {
            float h = Height(ui.Font, line.Kind);
            bool heading = line.Kind == MarkupKind.Heading, bold = heading || line.Kind == MarkupKind.Subheading;
            if (y >= content.Y - 1 && y + h <= content.Bottom + 1)
                ui.Text(content.X + line.Indent, y + (heading ? 8 : bold ? 4 : 0), line.Text, heading ? Theme.Accent : Theme.Text, FontSize.Normal, bold);
            y += h;
        }
        ui.ScrollBar(content, _scroll);
        return closed;
    }

    private void Open(bool rules)
    {
        _rules = rules;
        _topic = 0;
        _scroll.Reset();
        _wrappedFor = -1;
    }

    private static float Height(Font font, MarkupKind kind) => kind switch
    {
        MarkupKind.Heading => font.LineHeight(FontSize.Normal, true) + 10,
        MarkupKind.Subheading => font.LineHeight(FontSize.Normal, true) + 6,
        _ => font.LineHeight(FontSize.Normal, false) + 2,
    };

    /// <summary>Wraps the chosen topic to the panel's width.</summary>
    private void Layout(Font font, float width)
    {
        _wrappedFor = width;
        width -= Ui.ScrollBarWidth + 12;
        _lines.Clear();
        foreach (var line in _rules ? RulesManual.Lines(_topic) : HelpTopics.Lines(_topic))
        {
            if (line.Kind is MarkupKind.Heading or MarkupKind.Subheading) _lines.Add((line.Text, line.Kind, 0));
            else if (line.Kind == MarkupKind.Bullet)
            {
                var wrapped = font.Wrap(line.Text, width - 24, FontSize.Normal);
                for (int i = 0; i < wrapped.Count; i++) _lines.Add(((i == 0 ? "· " : "") + wrapped[i], MarkupKind.Bullet, i == 0 ? 6 : 18));
            }
            else
            {
                foreach (var l in font.Wrap(line.Text, width, FontSize.Normal)) _lines.Add((l, MarkupKind.Paragraph, 0));
                _lines.Add(("", MarkupKind.Paragraph, 0));
            }
        }
    }
}
