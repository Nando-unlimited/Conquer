using System.Numerics;
using Conquer.Client.Graphics;
using Conquer.Client.UI;
using Conquer.Game.Military;
using Conquer.Presentation;

namespace Conquer.Client.Screens;

/// <summary>
/// Draws the window of a battle (<see cref="GameController.BattleWindow"/>), opened by clicking its crossed swords: the
/// balance of fire, a column per side with its units, and the organisation chart.
/// </summary>
public sealed partial class GameScreen
{
    private readonly List<(int ProvinceId, Battle? Battle, Rect Bounds)> _battleHitBoxes = [];

    private void DrawBattleWindow()
    {
        if (_game.BattleWindow() is not { } window) return;
        var s = _app.ScreenSize;
        Batch.Rect(0, 0, s.X, s.Y, Rgba.Black.WithAlpha(0.35f));
        Ui.Block(new Rect(0, 0, s.X, s.Y));
        float width = Math.Min(980, s.X - 32), height = Math.Min(700, s.Y - 100);
        var panel = new Rect(s.X / 2 - width / 2, s.Y / 2 - height / 2, width, height);
        Ui.Panel(panel);

        float x = panel.X + 24, y = panel.Y + 20, w = panel.W - 48;
        Ui.Text(x, y, window.Title, Theme.Accent, FontSize.Large, bold: true);
        Ui.Text(panel.Right - 24 - Ui.Font.Measure(window.State, FontSize.Normal, true), y + 8, window.State, Theme.Of(window.StateInk), bold: true);
        y += 40;
        Ui.Text(x, y, window.Note, Theme.TextDim, FontSize.Small);
        y += window.NoteHeight;
        if (window.Fire is { } fire)
        {
            FireBalance(new Rect(x, y, w, 18), fire);
            y += 32;
        }
        if (window.Ended != null) Ui.Text(x, y, window.Ended, Theme.Text);

        float chartHeight = window.Chart != null ? 120 : 0;
        float bottom = panel.Bottom - 64 - (chartHeight > 0 ? chartHeight + 34 : 0);
        int sides = window.Sides.Count;
        float column = (w - 24 * (sides - 1)) / Math.Max(1, sides);
        for (int i = 0; i < sides; i++)
        {
            float sy = y;
            DocumentView.Draw(Ui, window.Sides[i], x + i * (column + 24), ref sy, column, bottom);
        }
        if (window.Chart is { } chart) OrganisationChart(chart, new Rect(x, bottom + 30, w, chartHeight));

        DocumentView.Press(Ui, window.GoTo, new Rect(panel.X + 24, panel.Bottom - 52, 200, 36));
        DocumentView.Press(Ui, window.Close, new Rect(panel.Right - 24 - 160, panel.Bottom - 52, 160, 36));
    }

    /// <summary>A bar split by each side's fire this hour: who is winning the exchange.</summary>
    private void FireBalance(Rect r, FireBalance fire)
    {
        float split = r.W * (float)fire.AttackerShare;
        Batch.Rect(r.X - 1, r.Y - 1, r.W + 2, r.H + 2, Rgba.Black);
        Batch.Rect(r.X, r.Y, split, r.H, new Rgba(fire.AttackerColor));
        Batch.Rect(r.X + split, r.Y, r.W - split, r.H, new Rgba(fire.DefenderColor));
        Batch.Rect(r.X + r.W / 2 - 1, r.Y - 3, 2, r.H + 6, Theme.Text);
        float labelW = Ui.Font.Measure(fire.Label, FontSize.Small, true);
        var box = new Rect(r.X + r.W / 2 - labelW / 2 - 8, r.Y, labelW + 16, r.H);
        Batch.Rect(box.X, box.Y, box.W, box.H, Rgba.Black.WithAlpha(0.6f));
        Ui.TextCentered(box, fire.Label, Theme.Text, FontSize.Small, bold: true);
        if (Ui.Hover(r)) Ui.Tooltip(fire.Tooltip);
    }

    /// <summary>Each side's organisation, hour by hour, with the line below which units break.</summary>
    private void OrganisationChart(OrganisationChart chart, Rect r)
    {
        Ui.Text(r.X, r.Y - 24, "Organización, hora a hora", Theme.Text, FontSize.Small, bold: true);
        Batch.Rect(r.X, r.Y, r.W, r.H, Rgba.Black.WithAlpha(0.45f));
        for (int i = 1; i < 4; i++) Batch.Rect(r.X, r.Y + r.H * i / 4, r.W, 1, Theme.Highlight);
        float breakY = r.Bottom - r.H * (float)chart.Breaking;
        Batch.Line(new(r.X, breakY), new(r.Right, breakY), Theme.Bad.WithAlpha(0.7f), 1);
        Ui.Text(r.Right - Ui.Font.Measure("se rompen", FontSize.Small) - 4, breakY - 17, "se rompen", Theme.Bad, FontSize.Small);

        var points = chart.Points;
        int n = points.Count;
        Vector2 Point(int i, double share) => new(r.X + r.W * i / (n - 1), r.Bottom - r.H * (float)Math.Clamp(share, 0, 1));
        var attackerColor = new Rgba(chart.AttackerColor);
        var defenderColor = new Rgba(chart.DefenderColor);
        // With long battles, only as many points as there are pixels.
        int step = Math.Max(1, n / (int)Math.Max(1, r.W / 2));
        for (int i = step; i < n; i += step)
        {
            int prev = i - step;
            Batch.Line(Point(prev, points[prev].Attacker), Point(i, points[i].Attacker), attackerColor, 2);
            Batch.Line(Point(prev, points[prev].Defender), Point(i, points[i].Defender), defenderColor, 2);
        }
        Ui.Text(r.X + 4, r.Bottom + 2, chart.Since, Theme.TextDim, FontSize.Small);
        Ui.Text(r.Right - Ui.Font.Measure("ahora", FontSize.Small) - 4, r.Bottom + 2, "ahora", Theme.TextDim, FontSize.Small);

        if (!Ui.Hover(r)) return;
        int at = Math.Clamp((int)MathF.Round((Ui.Input.Mouse.X - r.X) / r.W * (n - 1)), 0, n - 1);
        Batch.Rect(Point(at, 0).X, r.Y, 1, r.H, Theme.Text.WithAlpha(0.5f));
        Ui.Tooltip(chart.Describe(at));
    }
}
