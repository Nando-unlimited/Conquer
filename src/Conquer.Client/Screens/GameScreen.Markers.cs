using System.Numerics;
using Conquer.Client.Graphics;
using Conquer.Client.UI;
using Conquer.Presentation;

namespace Conquer.Client.Screens;

/// <summary>Draws the markers on the map (<see cref="GameController.Markers"/>): cities, nation names, unit counters and battles.</summary>
public sealed partial class GameScreen
{
    private static readonly Rgba MoveColor = new(0xFF62B83E);

    private void DrawMarkers()
    {
        var markers = _game.Markers();
        foreach (var city in markers.Cities) DrawCity(city);
        foreach (var label in markers.Nations) DrawNationName(label);
        _unitHitBoxes.Clear();
        foreach (var counter in markers.Units) DrawCounter(counter);
        _battleHitBoxes.Clear();
        foreach (var battle in markers.Battles) DrawBattleMark(battle);
    }

    /// <summary>Houses whose bases stand on the province's centre, and the name under them.</summary>
    private void DrawCity(CityMarker city)
    {
        var s = city.Screen;
        float below = MapIcons.City(Batch, s + new Vector2(0, 5 * city.Scale), new Rgba(city.Color), MapIcons.Houses(city.Population), city.Capital, city.Scale);
        if (city.Name == null) return;
        float w = Ui.Font.Measure(city.Name, FontSize.Small, true);
        float ty = s.Y + 5 * city.Scale + below;
        Ui.Text(s.X - w / 2 + 1, ty + 1, city.Name, Rgba.Black, FontSize.Small, bold: true);
        Ui.Text(s.X - w / 2, ty, city.Name, Rgba.White, FontSize.Small, bold: true);
    }

    private void DrawNationName(NationLabel label)
    {
        var size = DocumentView.Size(label.Size);
        var r = new Rect(label.Screen.X - 300, label.Screen.Y - 30, 600, 60);
        Ui.TextCentered(r with { X = r.X + 2, Y = r.Y + 2 }, label.Name, Rgba.Black.WithAlpha(label.Alpha * 0.7f), size, bold: true);
        Ui.TextCentered(r, label.Name, Batch2D.Mix(new Rgba(label.Color), Rgba.White, 0.55f).WithAlpha(label.Alpha), size, bold: true);
    }

    /// <summary>
    /// A NATO-style counter: the symbol of the unit's arm inside the frame, its size marks above it, "HQ" inside an
    /// HQ's frame and a wagon for settlers; combat units and fleets carry a strength bar (green) and an organisation
    /// bar (amber). Before it, its route, the line to its HQ and the arrow of its attack.
    /// </summary>
    private void DrawCounter(UnitCounter c)
    {
        if (c.Path != null) PathArrow.Draw(Batch, c.Path, MoveColor, _game.Now, c.Selected ? 6 : 4, c.Selected ? 1 : 0.55f);
        var s = c.Screen;
        if (c.Command is var (hq, inRange)) Batch.Line(s, hq, (inRange ? Theme.Good : Theme.Bad).WithAlpha(0.8f), 1.5f);
        if (c.Attack is var (from, to)) PathArrow.Draw(Batch, [from, to], Theme.Battle, _game.Now, 5);

        float W = 28 * c.Scale, H = 19 * c.Scale;
        var r = new Rect(s.X - W / 2, s.Y - H / 2, W, H);
        var color = new Rgba(c.Color);
        // A soft shadow lifts the counter off the map; the selected one's frame pulses.
        Batch.Shadow(r.X - 2, r.Y, r.W + 4, r.H + 4, 3, spread: 5, strength: 0.5f);
        if (c.Selected)
        {
            float pulse = 0.5f + 0.5f * MathF.Sin((float)_game.Now * 5);
            Batch.Rect(r.X - 4, r.Y - 4, r.W + 8, r.H + 8, Theme.Accent.WithAlpha(0.25f + 0.35f * pulse));
        }
        Batch.Rect(r.X - 2, r.Y - 2, r.W + 4, r.H + 4, c.Selected ? Theme.Accent : Rgba.Black);
        Batch.Rect(r.X, r.Y, r.W, r.H, color.Scale(0.55f).WithAlpha(1));
        Batch.Rect(r.X + 2, r.Y + 2, r.W - 4, r.H - 4, color);
        switch (c.Kind)
        {
            case CounterKind.Military:
                MapIcons.NatoSymbol(Batch, r.X + 2, r.Y + 2, r.W - 4, r.H - 4, c.Function);
                break;
            case CounterKind.Fleet:
                // A hull under the ship letter; a dot for every unit aboard.
                Batch.Line(new(r.X + 3, r.Bottom - 4), new(r.Right - 3, r.Bottom - 4), Rgba.Black, 2);
                Batch.Line(new(r.X + 3, r.Bottom - 4), new(r.X + 7, r.Bottom - 1), Rgba.Black, 1.5f);
                Batch.Line(new(r.Right - 3, r.Bottom - 4), new(r.Right - 7, r.Bottom - 1), Rgba.Black, 1.5f);
                if (c.Scale > 0.7f) Ui.TextCentered(new Rect(r.X, r.Y - 2, r.W, r.H - 4), c.Symbol, Rgba.Black, FontSize.Small, bold: true);
                for (int i = 0; i < c.Aboard; i++) Batch.Rect(r.Right + 3, r.Y + i * 5, 3, 3, Rgba.White);
                break;
            case CounterKind.Headquarters:
                if (c.Scale > 0.7f) Ui.TextCentered(r, "HQ", Rgba.Black, FontSize.Small, bold: true);
                break;
            default:
                MapIcons.Settlers(Batch, r.X + 2, r.Y + 2, r.W - 4, r.H - 4);
                break;
        }
        if (c.Kind is CounterKind.Military or CounterKind.Fleet)
        {
            Bar(new Rect(r.X - 2, r.Bottom + 3, r.W + 4, 3), c.Strength, Theme.Strength);
            Bar(new Rect(r.X - 2, r.Bottom + 7, r.W + 4, 3), c.Organisation, Theme.Organisation);
        }
        if (c.Echelon.Length > 0) DrawEchelon(r, c.Echelon, c.Scale);
        _unitHitBoxes.Add((c.UnitId, r));
    }

    /// <summary>NATO size marks centred over the frame: a bar for each "I", a small cross for each "X".</summary>
    private void DrawEchelon(Rect r, string marks, float scale)
    {
        float h = 7 * scale, w = 5 * scale, gap = 3 * scale;
        float total = marks.Sum(c => c == 'I' ? 0 : w) + (marks.Length - 1) * gap;
        float x = r.X + (r.W - total) / 2, bottom = r.Y - 4, top = bottom - h;
        Batch.Rect(x - 2, top - 2, total + 4, h + 4, Rgba.Black.WithAlpha(0.45f));
        foreach (char c in marks)
        {
            if (c == 'I') Batch.Line(new(x, top), new(x, bottom), Rgba.White, 1.5f);
            else
            {
                Batch.Line(new(x, top), new(x + w, bottom), Rgba.White, 1.5f);
                Batch.Line(new(x, bottom), new(x + w, top), Rgba.White, 1.5f);
                x += w;
            }
            x += gap;
        }
    }

    /// <summary>The crossed swords: hovering shows a summary, clicking opens the battle's window.</summary>
    private void DrawBattleMark(BattleMarker mark)
    {
        var s = mark.Screen;
        float size = 9 + 2 * (float)Math.Sin(_game.Now * 6);
        Batch.Rect(s.X - size - 2, s.Y - size - 2, 2 * size + 4, 2 * size + 4, Rgba.Black.WithAlpha(0.6f));
        Batch.Line(new(s.X - size, s.Y - size), new(s.X + size, s.Y + size), Theme.Battle, 3);
        Batch.Line(new(s.X - size, s.Y + size), new(s.X + size, s.Y - size), Theme.Battle, 3);
        var bounds = new Rect(s.X - 11, s.Y - 11, 22, 22);
        _battleHitBoxes.Add((mark.ProvinceId, mark.Battle, bounds));
        if (Ui.Hover(bounds)) Ui.Tooltip(mark.Tooltip());
    }

    private void Bar(Rect r, double share, Rgba color)
    {
        Batch.Rect(r.X, r.Y, r.W, r.H, Rgba.Black.WithAlpha(0.7f));
        Batch.Rect(r.X, r.Y, r.W * (float)Math.Clamp(share, 0, 1), r.H, color);
    }
}
