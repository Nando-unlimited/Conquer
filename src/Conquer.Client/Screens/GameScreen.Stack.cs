using Conquer.Client.Graphics;
using Conquer.Client.UI;
using Conquer.Presentation;

namespace Conquer.Client.Screens;

/// <summary>Draws the list of a stack's units (<see cref="GameController.StackList"/>) beside it on the map.</summary>
public sealed partial class GameScreen
{
    private void DrawStackList()
    {
        if (_game.StackList() is not { } window) return;
        const float Width = 340, Pad = 14, RowH = 52, ButtonW = 96;
        var s = _app.ScreenSize;
        // Rows that do not fit on screen become «y N más».
        int fit = Math.Max(1, (int)((s.Y - TopBarHeight - 120 - 48) / RowH));
        int shown = Math.Min(window.Units.Count, fit);
        float height = 48 + shown * RowH + (shown < window.Units.Count ? 22 : 0) + 4;
        // To the right of the stack, or to its left when there is no room; never off screen.
        float x = window.Anchor.X + 52 + Width < s.X - 8 ? window.Anchor.X + 52 : window.Anchor.X - 52 - Width;
        float y = Math.Clamp(window.Anchor.Y - 40, TopBarHeight + 8, s.Y - 70 - height);
        var panel = new Rect(Math.Clamp(x, 8, s.X - Width - 8), y, Width, height);
        Ui.Panel(panel);

        Ui.Text(panel.X + Pad, panel.Y + 12, window.Title, Theme.Accent, FontSize.Normal, bold: true);
        DocumentView.Press(Ui, window.Close, new Rect(panel.Right - Pad - 24, panel.Y + 10, 24, 22));

        float ry = panel.Y + 44, w = panel.W - 2 * Pad;
        for (int i = 0; i < shown; i++)
        {
            var row = window.Units[i];
            var area = new Rect(panel.X + Pad - 6, ry - 4, w + 12, RowH - 2);
            if (row.Selected) Batch.Rect(area.X, area.Y, area.W, area.H, Theme.Accent.WithAlpha(0.12f));
            else if (i % 2 == 0) Batch.Rect(area.X, area.Y, area.W, area.H, Theme.Button.WithAlpha(0.35f));
            float ey = ry;
            DocumentView.Entry(Ui, row.Entry, panel.X + Pad, ref ey, w - ButtonW - 10);
            float bx = panel.Right - Pad - ButtonW;
            DocumentView.Press(Ui, row.Select, new Rect(bx, ry, ButtonW, 20));
            if (row.Edit is { } edit) DocumentView.Press(Ui, edit, new Rect(bx, ry + 23, ButtonW, 20));
            ry += RowH;
        }
        if (shown < window.Units.Count)
            Ui.Text(panel.X + Pad, ry, $"y {window.Units.Count - shown} más", Theme.TextDim, FontSize.Small);
    }
}
