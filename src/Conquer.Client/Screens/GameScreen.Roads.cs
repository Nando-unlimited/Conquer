using Conquer.Client.Graphics;
using Conquer.Client.UI;
using Conquer.Presentation;

namespace Conquer.Client.Screens;

/// <summary>
/// Draws the window to lay a road or railway (<see cref="GameController.RoadWindow"/>) on the left, so the chosen
/// route stays in sight on the map: the destinations that fit, then what the chosen one costs and joins.
/// </summary>
public sealed partial class GameScreen
{
    private void DrawRoadWindow()
    {
        if (_game.RoadWindow() is not { } window) return;
        var s = _app.ScreenSize;
        Ui.Block(new Rect(0, 0, s.X, s.Y));
        float width = Math.Min(480, s.X - 32), height = Math.Min(600, s.Y - TopBarHeight - 90);
        var panel = new Rect(16, TopBarHeight + 16, width, height);
        Ui.Panel(panel);
        float x = panel.X + 20, y = panel.Y + 18, w = panel.W - 40;
        Ui.Text(x, y, window.Title, Theme.Accent, FontSize.Large, bold: true);
        y += 38;
        Paragraph(x, ref y, w, window.Intro, Theme.TextDim);
        y += 4;

        float listBottom = panel.Bottom - 210;
        if (window.None != null)
        {
            Ui.Text(x, y, window.None, Theme.TextDim, FontSize.Small);
            y += 24;
        }
        int shown = 0;
        foreach (var destination in window.Destinations)
        {
            if (y + 28 > listBottom) break;
            shown++;
            DocumentView.Press(Ui, destination, new Rect(x, y, w, 26));
            y += 30;
        }
        if (shown < window.Destinations.Count) Ui.Text(x, y, string.Format(window.Hidden, window.Destinations.Count - shown), Theme.TextDim, FontSize.Small);

        y = listBottom + 10;
        foreach (var (label, value, ink) in window.Details)
        {
            Ui.Text(x, y, label, Theme.TextDim, FontSize.Small);
            float top = y;
            Paragraph(x + 110, ref y, w - 110, value, Theme.Of(ink));
            y = Math.Max(y, top + 20) + 4;
        }

        float half = (w - 8) / 2;
        DocumentView.Press(Ui, window.Build, new Rect(x, panel.Bottom - 52, half, 36));
        DocumentView.Press(Ui, window.Cancel, new Rect(x + half + 8, panel.Bottom - 52, half, 36));
    }
}
