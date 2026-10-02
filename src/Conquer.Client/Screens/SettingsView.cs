using System.Numerics;
using Conquer.Client.Graphics;
using Conquer.Client.UI;
using Conquer.Presentation;

namespace Conquer.Client.Screens;

/// <summary>
/// Draws the options window (<see cref="SettingsMenu"/>) in the middle of the screen, over a darkened backdrop: a row
/// per option, volumes as a value between - and +, the rest as buttons side by side, and Cerrar at the bottom.
/// </summary>
public static class SettingsView
{
    private const float LabelWidth = 130, RowHeight = 46;

    public static void Frame(Ui ui, Vector2 screen, SettingsMenu menu)
    {
        if (!menu.Open) return;
        ui.Batch.Rect(0, 0, screen.X, screen.Y, Rgba.Black.WithAlpha(0.45f));
        ui.Block(new Rect(0, 0, screen.X, screen.Y));
        var rows = menu.Rows();
        float width = 460, height = 80 + rows.Count * RowHeight + 64;
        var panel = new Rect(screen.X / 2 - width / 2, screen.Y / 2 - height / 2, width, height);
        ui.Panel(panel);
        float x = panel.X + 24, y = panel.Y + 20, w = panel.W - 48;
        ui.Text(x, y, "Opciones", Theme.Accent, FontSize.Large, bold: true);
        y += 50;
        foreach (var row in rows)
        {
            ui.Text(x, y + 6, row.Label, row.Enabled ? Theme.TextDim : Theme.TextDisabled);
            float bx = x + LabelWidth, bw = w - LabelWidth;
            if (row.Value != null)
            {
                DocumentView.Press(ui, row.Buttons[0], new Rect(bx, y, 40, 32));
                ui.TextCentered(new Rect(bx + 44, y, bw - 88, 32), row.Value, Theme.Text);
                DocumentView.Press(ui, row.After![0], new Rect(bx + bw - 40, y, 40, 32));
            }
            else
            {
                float each = (bw - 6 * (row.Buttons.Count - 1)) / row.Buttons.Count;
                for (int i = 0; i < row.Buttons.Count; i++) DocumentView.Press(ui, row.Buttons[i], new Rect(bx + i * (each + 6), y, each, 32));
            }
            y += RowHeight;
        }
        DocumentView.Press(ui, menu.Close, new Rect(x, panel.Bottom - 56, w, 40));
    }
}
