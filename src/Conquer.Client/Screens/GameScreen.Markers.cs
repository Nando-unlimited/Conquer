using System.Numerics;
using Conquer.Client.Graphics;
using Conquer.Client.UI;
using Conquer.Presentation;

namespace Conquer.Client.Screens;

/// <summary>Draws the markers on the map (<see cref="GameController.Markers"/>): cities, nation names, unit counters and battles.</summary>
public sealed partial class GameScreen
{
    private static readonly Rgba MoveColor = new(0xFF62B83E);
    private static readonly Rgba TownSmoke = new(0xFFC0C0C0), SootSmoke = new(0xFF595959);
    private static readonly Rgba BurstFire = new(0xFFFFBF4D), BurstFlash = new(0xFFFFFFD9);

    private void DrawMarkers()
    {
        Motion.Time = (float)(_game.Now % 3600);
        var markers = _game.Markers();
        foreach (var city in markers.Cities) DrawCity(city);
        foreach (var label in markers.Nations) DrawNationName(label);
        _unitHitBoxes.Clear();
        foreach (var counter in markers.Units) DrawCounter(counter);
        _battleHitBoxes.Clear();
        foreach (var battle in markers.Battles) DrawBattleMark(battle);
    }

    /// <summary>
    /// The city and its name under it: a little castle or village standing on a patch of its owner's colour, with the
    /// models of its buildings in a row under the name when close in; or, with counters, houses.
    /// </summary>
    private void DrawCity(CityMarker city)
    {
        var s = city.Screen;
        float sc = city.Scale, below;
        string model = Models.City(city.Capital);
        bool models = DisplaySettings.Current.UnitModels && _sprites.Has(model);
        if (models)
        {
            var foot = s + new Vector2(0, 8 * sc);
            Batch.Ellipse(foot + new Vector2(0, 2), 21 * sc, 8 * sc, Rgba.Black.WithAlpha(0.35f));
            Batch.Ellipse(foot, 20 * sc, 7.5f * sc, new Rgba(city.Color).WithAlpha(0.9f));
            var size = _sprites.Draw(Batch, model, foot + new Vector2(0, 5 * sc), new Vector2(42, 42) * sc, new Rgba(city.Color));
            if (Motion.Enabled && (city.Industry || city.Population >= 2000))
                Smoke(foot + new Vector2(8 * sc, -size.Y + 4 * sc), city.Id, sc, city.Industry);
            below = 10 * sc;
        }
        else
        {
            if (Motion.Enabled && (city.Industry || city.Population >= 2000)) Smoke(s + new Vector2(6 * sc, -10 * sc), city.Id, sc, city.Industry);
            below = MapIcons.City(Batch, s + new Vector2(0, 5 * sc), new Rgba(city.Color), MapIcons.Houses(city.Population), city.Capital, sc);
        }
        float ty = s.Y + 5 * sc + below;
        if (city.Name != null)
        {
            float w = Ui.Font.Measure(city.Name, FontSize.Small, true);
            Ui.Text(s.X - w / 2 + 1, ty + 1, city.Name, Rgba.Black, FontSize.Small, bold: true);
            Ui.Text(s.X - w / 2, ty, city.Name, Rgba.White, FontSize.Small, bold: true);
            ty += 20;
        }
        if (!models || city.Buildings is not { Count: > 0 } buildings) return;
        // The buildings in a row, each a model as tall as fits.
        float each = 22 * sc, x = s.X - (buildings.Count - 1) * each / 2;
        foreach (var building in buildings)
        {
            _sprites.Draw(Batch, building, new Vector2(x, ty + each), new Vector2(each, each), new Rgba(city.Color));
            x += each;
        }
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
        s += CounterMotion(c);
        if (c.Kind == CounterKind.Fleet && c.Moving) Wake(s, c.Heading, c.Scale, c.UnitId);
        if (DisplaySettings.Current.UnitModels && c.Model is { } model && _sprites.Has(model))
        {
            DrawFigure(c, s, model);
            return;
        }

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

    /// <summary>
    /// A unit as a little 3D model on a patch of its nation's colour, facing the way it goes, with its size marks
    /// above and its strength and organisation bars under the patch.
    /// </summary>
    private void DrawFigure(UnitCounter c, Vector2 s, string model)
    {
        float sc = c.Scale;
        bool ship = c.Kind == CounterKind.Fleet;
        var color = new Rgba(c.Color);
        var foot = s + new Vector2(0, 9 * sc);
        float rx = (ship ? 19 : 14) * sc, ry = (ship ? 7 : 5.5f) * sc;
        Batch.Ellipse(foot + new Vector2(1.5f, 2), rx + 1.5f, ry + 1, Rgba.Black.WithAlpha(0.4f));
        Batch.Ellipse(foot, rx, ry, color.Scale(0.55f).WithAlpha(1));
        Batch.Ellipse(foot, rx - 1.5f, ry - 1.2f, color);
        if (c.Selected)
        {
            float pulse = 0.5f + 0.5f * MathF.Sin((float)_game.Now * 5);
            Batch.Ellipse(foot, rx + 5, ry + 4, Theme.Accent.WithAlpha(0.5f + 0.4f * pulse), thickness: 2.5f);
        }
        var box = ship ? new Vector2(48, 32) * sc : new Vector2(30, 38) * sc;
        var size = _sprites.Draw(Batch, model, foot + new Vector2(0, ry * 0.5f), box, color, mirrored: c.Heading.X < -0.1f);
        var r = new Rect(s.X - Math.Max(size.X, 2 * rx) / 2, foot.Y + ry * 0.5f - size.Y, Math.Max(size.X, 2 * rx), size.Y + ry);
        if (c.Kind is CounterKind.Military or CounterKind.Fleet)
        {
            Bar(new Rect(foot.X - rx, foot.Y + ry + 3, 2 * rx, 3), c.Strength, Theme.Strength);
            Bar(new Rect(foot.X - rx, foot.Y + ry + 7, 2 * rx, 3), c.Organisation, Theme.Organisation);
        }
        if (ship) for (int i = 0; i < c.Aboard; i++) Batch.Rect(foot.X + rx + 3, foot.Y - ry - i * 5, 3, 3, Rgba.White);
        if (c.Echelon.Length > 0) DrawEchelon(r, c.Echelon, sc);
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
        if (Motion.Enabled) Bursts(s, mark.ProvinceId);
        var bounds = new Rect(s.X - 11, s.Y - 11, 22, 22);
        _battleHitBoxes.Add((mark.ProvinceId, mark.Battle, bounds));
        if (Ui.Hover(bounds)) Ui.Tooltip(mark.Tooltip());
    }

    /// <summary>
    /// How far the counter is moved this frame: marching units hop, the selected one bobs, units in battle shake and
    /// fleets ride the swell.
    /// </summary>
    private static Vector2 CounterMotion(UnitCounter c)
    {
        var offset = Vector2.Zero;
        if (c.Kind == CounterKind.Fleet) offset.Y += Motion.Wave(1.6f, c.UnitId, 1.6f * c.Scale);
        else if (c.Moving && !c.Fighting) offset.Y -= Motion.Hop(7f, c.UnitId, 3.5f * c.Scale);
        if (c.Selected) offset.Y -= Motion.Wave(3f, c.UnitId, 1.5f);
        if (c.Fighting) offset += new Vector2(Motion.Wave(37f, c.UnitId, 1.4f), Motion.Wave(29f, c.UnitId + 7, 0.8f));
        return offset;
    }

    /// <summary>A fading V of foam behind a sailing fleet.</summary>
    private void Wake(Vector2 s, Vector2 heading, float scale, int id)
    {
        if (heading == Vector2.Zero || !Motion.Enabled) return;
        var back = -heading;
        var side = new Vector2(-heading.Y, heading.X);
        var stern = s + back * 14 * scale + new Vector2(0, 6 * scale);
        float flicker = 0.75f + 0.25f * Motion.Wave(4f, id, 1f);
        for (int i = 0; i < 4; i++)
        {
            float t = (i + Motion.Cycle(1.2f, id)) / 4f;
            var foam = Rgba.White.WithAlpha((1 - t) * 0.55f * flicker);
            var along = stern + back * t * 26 * scale;
            Batch.Line(along + side * t * 9 * scale, along + side * (t * 9 + 3) * scale, foam, 1.5f);
            Batch.Line(along - side * t * 9 * scale, along - side * (t * 9 + 3) * scale, foam, 1.5f);
        }
    }

    /// <summary>Puffs of smoke rising from a town, darker and thicker from its workshops.</summary>
    private void Smoke(Vector2 chimney, int id, float scale, bool industry)
    {
        int puffs = industry ? 4 : 3;
        for (int i = 0; i < puffs; i++)
        {
            float t = Motion.Cycle(0.3f, id, i / (float)puffs);
            var at = chimney + new Vector2(MathF.Sin(t * 5 + id) * 3 * scale + t * 6 * scale, -t * 26 * scale);
            Batch.Circle(at, (2 + t * 5) * scale, (industry ? SootSmoke : TownSmoke).WithAlpha((1 - t) * (industry ? 0.55f : 0.35f)), segments: 12);
        }
    }

    /// <summary>Shells bursting about a battle: flashes that swell and fade, each in its own spot.</summary>
    private void Bursts(Vector2 centre, int id)
    {
        for (int i = 0; i < 3; i++)
        {
            float t = Motion.Cycle(0.9f, id * 3 + i, i / 3f);
            float angle = (id * 7 + i * 2.1f + MathF.Floor(Motion.Time * 0.9f + i / 3f)) * 2.39996f;
            var at = centre + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 18;
            Batch.Circle(at, 2 + t * 7, BurstFire.WithAlpha((1 - t) * 0.8f), segments: 12);
            Batch.Circle(at, 1 + t * 4, BurstFlash.WithAlpha((1 - t) * (1 - t)), segments: 10);
        }
    }

    private void Bar(Rect r, double share, Rgba color)
    {
        Batch.Rect(r.X, r.Y, r.W, r.H, Rgba.Black.WithAlpha(0.7f));
        Batch.Rect(r.X, r.Y, r.W * (float)Math.Clamp(share, 0, 1), r.H, color);
    }
}
