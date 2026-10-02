using Conquer.Client.Graphics;
using Conquer.Client.UI;
using Conquer.Presentation;

namespace Conquer.Client.Screens;

/// <summary>
/// Draws the event waiting for the player's answer (<see cref="GameController.DecisionWindow"/>) in the middle of the
/// screen: what happened, each answer with what it does under it, and how long is left.
/// </summary>
public sealed partial class GameScreen
{
    private void DrawDecision()
    {
        if (_game.DecisionWindow() is not { } window) return;
        var s = _app.ScreenSize;
        Batch.Rect(0, 0, s.X, s.Y, Rgba.Black.WithAlpha(0.35f));
        Ui.Block(new Rect(0, 0, s.X, s.Y));
        float width = Math.Min(520, s.X - 32), height = 176 + window.Options.Count * 64;
        var panel = new Rect(s.X / 2 - width / 2, s.Y / 2 - height / 2, width, height);
        Ui.Panel(panel);
        float x = panel.X + 24, y = panel.Y + 20, w = panel.W - 48;
        Ui.Text(x, y, window.Title, Theme.Accent, FontSize.Large, bold: true);
        DocumentView.Press(Ui, window.View, new Rect(panel.Right - 24 - 130, y, 130, 26));
        y += 34;
        Ui.Text(x, y, window.Place, Theme.TextDim, FontSize.Small);
        y += 26;
        Paragraph(x, ref y, w, window.Text, Theme.Text, FontSize.Normal);
        y += 12;
        for (int i = 0; i < window.Options.Count; i++)
        {
            DocumentView.Press(Ui, window.Options[i], new Rect(x, y, w, 36));
            y += 40;
            Paragraph(x + 8, ref y, w - 16, window.Effects[i], Theme.TextDim);
            y += 10;
        }
        Paragraph(x, ref y, w, window.Footer, Theme.TextDim);
    }
}
