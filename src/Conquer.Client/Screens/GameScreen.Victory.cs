using Conquer.Client.Graphics;
using Conquer.Client.UI;
using Conquer.Presentation;

namespace Conquer.Client.Screens;

/// <summary>
/// Draws the end of the game (<see cref="GameController.GameOver"/>) over everything: victory or defeat, how it came
/// about, the final ranking and the buttons to go on playing or go back to the main menu.
/// </summary>
public sealed partial class GameScreen
{
    private void DrawGameOver()
    {
        if (_game.GameOver(new MenuNavigator(_app)) is not { } window) return;
        var s = _app.ScreenSize;
        Batch.Rect(0, 0, s.X, s.Y, Rgba.Black.WithAlpha(0.55f));
        Ui.Block(new Rect(0, 0, s.X, s.Y));
        int rows = Math.Min(window.Ranking.Count, 10);
        float width = Math.Min(520, s.X - 32), height = 196 + rows * 26;
        var panel = new Rect(s.X / 2 - width / 2, s.Y / 2 - height / 2, width, height);
        Ui.Panel(panel);
        float x = panel.X + 24, y = panel.Y + 20, w = panel.W - 48;
        Ui.TextCentered(new Rect(panel.X, y, panel.W, 40), window.Title, window.Won ? Theme.Good : Theme.Bad, FontSize.Large, bold: true);
        y += 50;
        Paragraph(x, ref y, w, window.Text, Theme.Text, FontSize.Normal);
        y += 12;
        foreach (var (name, score, color) in window.Ranking.Take(rows))
        {
            Batch.Rect(x, y + 3, 16, 16, Rgba.Black);
            Batch.Rect(x + 2, y + 5, 12, 12, new Rgba(color));
            Ui.Text(x + 24, y, name, Theme.Text, FontSize.Small);
            Ui.Text(x + w - 80, y, score, Theme.TextDim, FontSize.Small);
            y += 26;
        }
        float bw = (w - 12) / 2;
        for (int i = 0; i < window.Buttons.Count; i++)
            DocumentView.Press(Ui, window.Buttons[i], new Rect(x + i * (bw + 12), panel.Bottom - 56, bw, 40));
    }
}
