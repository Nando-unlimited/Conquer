using System.Numerics;
using Conquer.Client.Graphics;
using Conquer.Game.Rules;
using Conquer.Presentation;

namespace Conquer.Client.UI;

public readonly record struct Rect(float X, float Y, float W, float H)
{
    public float Right => X + W;
    public float Bottom => Y + H;
    public bool Contains(Vector2 p) => p.X >= X && p.X < X + W && p.Y >= Y && p.Y < Y + H;
    public Rect Inset(float d) => new(X + d, Y + d, W - 2 * d, H - 2 * d);

    /// <summary>The part of this rectangle inside the other; empty (zero wide or tall) when they do not meet.</summary>
    public Rect Intersect(Rect o)
    {
        float x = Math.Max(X, o.X), y = Math.Max(Y, o.Y);
        return new(x, y, Math.Max(0, Math.Min(Right, o.Right) - x), Math.Max(0, Math.Min(Bottom, o.Bottom) - y));
    }
}

/// <summary>
/// How far a scrolling area is scrolled and how tall its content was the last time it was drawn
/// (<see cref="Ui.BeginScroll"/>), and where its bar was grabbed while it is dragged.
/// </summary>
public sealed class ScrollState
{
    public float Offset;
    public float Content;
    internal float? Grab;

    /// <summary>Whether the content does not fit in an area this tall, so it scrolls and shows its bar.</summary>
    public bool Overflows(float height) => Content > height + 0.5f;

    public void Reset()
    {
        Offset = 0;
        Grab = null;
    }
}

/// <summary>Mouse and keyboard state for one frame, gathered from input events.</summary>
public sealed class InputState
{
    public Vector2 Mouse;
    public bool LeftDown, LeftPressed, LeftReleased;
    public bool RightPressed;
    public float Scroll;
    public Vector2 LeftPressPosition;
    public readonly List<Silk.NET.Input.Key> KeysPressed = [];
    public readonly HashSet<Silk.NET.Input.Key> KeysDown = [];
    /// <summary>Characters typed this frame, for text fields.</summary>
    public readonly List<char> Chars = [];

    public void EndFrame()
    {
        LeftPressed = LeftReleased = RightPressed = false;
        Scroll = 0;
        KeysPressed.Clear();
        Chars.Clear();
    }
}

public static class Theme
{
    public static readonly Rgba Panel = new(0xE6141A22);
    public static readonly Rgba PanelBorder = new(0xFF3A4656);
    public static readonly Rgba Button = new(0xFF26303D);
    public static readonly Rgba ButtonHover = new(0xFF34425A);
    public static readonly Rgba ButtonActive = new(0xFF8A6A2A);
    public static readonly Rgba ButtonDisabled = new(0xFF1C222A);
    public static readonly Rgba Text = new(0xFFE8E4DA);
    public static readonly Rgba TextDim = new(0xFF9AA3AE);
    public static readonly Rgba TextDisabled = new(0xFF5E6670);
    public static readonly Rgba Accent = new(0xFFE0B656);
    public static readonly Rgba Good = new(0xFF7FCB6A);
    public static readonly Rgba Bad = new(0xFFE06A5A);

    // Shading for panels and buttons: lighter at the top, as if lit from above.
    public static readonly Rgba PanelTop = new(0xF41C2531);
    public static readonly Rgba PanelBottom = new(0xF40D1218);
    public static readonly Rgba Highlight = new(0x1CFFFFFF);
    public static readonly Rgba ButtonTop = new(0xFF303C4D);
    public static readonly Rgba ButtonBottom = new(0xFF1F2833);
    public static readonly Rgba HoverTop = new(0xFF41516A);
    public static readonly Rgba HoverBottom = new(0xFF2A3647);
    public static readonly Rgba ActiveTop = new(0xFFB99442);
    public static readonly Rgba ActiveBottom = new(0xFF765822);
    public const float PanelRadius = 8;
    public const float ButtonRadius = 4;

    public static readonly Rgba River = new(0xFF3F7FC8);
    public static readonly Rgba Battle = new(0xFFE04A3A);
    public static readonly Rgba Strength = new(0xFF6FBF5A);
    public static readonly Rgba Organisation = new(0xFFE0B656);

    /// <summary>Red in unrest, green when content, <paramref name="normal"/> in between.</summary>
    public static Rgba Mood(double mood, Rgba normal) =>
        mood < GameRules.UnrestMood ? Bad : GameRules.MoodLevel(mood) == 3 ? Good : normal;

    /// <summary>The colour of a presentation <see cref="Ink"/>: a nation's own, or the theme's for its tone.</summary>
    public static Rgba Of(Ink ink) => ink.Color != 0 ? new Rgba(ink.Color) : ink.Tone switch
    {
        Tone.Dim => TextDim,
        Tone.Disabled => TextDisabled,
        Tone.Accent => Accent,
        Tone.Good => Good,
        Tone.Bad => Bad,
        Tone.River => River,
        Tone.Battle => Battle,
        Tone.Strength => Strength,
        Tone.Organisation => Organisation,
        Tone.Track => Rgba.Black.WithAlpha(0.7f),
        Tone.Groove => ButtonDisabled,
        Tone.Border => PanelBorder,
        Tone.Uneasy => new Rgba(0xFFE0A050),
        Tone.Calm => new Rgba(0xFFB9C08A),
        _ => Text,
    };
}

/// <summary>
/// A small immediate-mode UI: widgets are declared every frame and report clicks directly.
/// Every panel drawn registers its area so the map knows the mouse is over the interface.
/// </summary>
public sealed class Ui
{
    private readonly List<Rect> _blockers = [];
    private string? _tooltip;

    public Batch2D Batch { get; }
    public Font Font { get; }
    public InputState Input { get; }

    public Ui(Batch2D batch, Font font, InputState input)
    {
        Batch = batch;
        Font = font;
        Input = input;
    }

    public void BeginFrame()
    {
        _blockers.Clear();
        _clips.Clear();
        _tooltip = null;
        ButtonClicked = false;
    }

    private readonly Stack<Rect> _clips = new();

    /// <summary>Draws and takes the mouse only inside this area (and the one already clipped to) until <see cref="PopClip"/>.</summary>
    public void PushClip(Rect r)
    {
        if (_clips.Count > 0) r = r.Intersect(_clips.Peek());
        _clips.Push(r);
        Batch.PushClip(r.X, r.Y, r.W, r.H);
    }

    public void PopClip()
    {
        if (_clips.Count > 0) _clips.Pop();
        Batch.PopClip();
    }

    /// <summary>Width of the bar along the right of a scrolling area; content that scrolls leaves it room.</summary>
    public const float ScrollBarWidth = 8;

    /// <summary>
    /// Starts a scrolling area: the mouse wheel over it scrolls it, and what is drawn until <see cref="EndScroll"/> is
    /// clipped to it. Returns the y where its content starts, moved up by how far it is scrolled.
    /// </summary>
    public float BeginScroll(Rect view, ScrollState scroll, float step = 60)
    {
        Wheel(view, scroll, step);
        PushClip(view);
        return view.Y - scroll.Offset;
    }

    /// <summary>Ends a scrolling area whose content took <paramref name="contentHeight"/>, and draws its bar if it does not fit.</summary>
    public void EndScroll(Rect view, ScrollState scroll, float contentHeight)
    {
        PopClip();
        scroll.Content = contentHeight;
        scroll.Offset = Math.Clamp(scroll.Offset, 0, Math.Max(0, contentHeight - view.H));
        ScrollBar(view, scroll);
    }

    /// <summary>Scrolls the area by the mouse wheel when the mouse is over it, and keeps it within its content.</summary>
    public void Wheel(Rect view, ScrollState scroll, float step)
    {
        if (Input.Scroll != 0 && Hover(view))
        {
            scroll.Offset -= Input.Scroll * step;
            Input.Scroll = 0; // one area scrolls, not the ones around it as well
        }
        scroll.Offset = Math.Clamp(scroll.Offset, 0, Math.Max(0, scroll.Content - view.H));
    }

    /// <summary>
    /// The bar along the right edge of a scrolling area whose content does not fit: its thumb shows the part in view,
    /// and dragging it, or clicking the track, scrolls.
    /// </summary>
    public void ScrollBar(Rect view, ScrollState scroll)
    {
        if (!scroll.Overflows(view.H))
        {
            scroll.Grab = null;
            return;
        }
        var track = new Rect(view.Right - ScrollBarWidth, view.Y, ScrollBarWidth, view.H);
        _blockers.Add(track);
        float range = scroll.Content - view.H;
        float thumbH = Math.Max(24, view.H * view.H / scroll.Content);
        float ThumbY() => view.Y + (view.H - thumbH) * scroll.Offset / range;
        if (Input.LeftPressed && Hover(track))
        {
            float thumbY = ThumbY();
            scroll.Grab = Input.Mouse.Y >= thumbY && Input.Mouse.Y < thumbY + thumbH ? Input.Mouse.Y - thumbY : thumbH / 2;
        }
        if (scroll.Grab is float grab)
        {
            if (Input.LeftDown) scroll.Offset = Math.Clamp((Input.Mouse.Y - grab - view.Y) / Math.Max(1, view.H - thumbH) * range, 0, range);
            else scroll.Grab = null;
        }
        bool lit = scroll.Grab != null || Hover(track);
        Batch.RoundedRect(track.X, track.Y, track.W, track.H, ScrollBarWidth / 2, Rgba.Black.WithAlpha(0.35f));
        Batch.RoundedRect(track.X + 1, ThumbY() + 1, track.W - 2, thumbH - 2, ScrollBarWidth / 2 - 1, lit ? Theme.Accent : Theme.ButtonHover);
    }

    /// <summary>Whether a button was clicked this frame (it clicks).</summary>
    public bool ButtonClicked { get; private set; }

    public bool MouseOverUi => _blockers.Any(r => r.Contains(Input.Mouse));

    /// <summary>Marks an area as interface without drawing anything.</summary>
    public void Block(Rect r) => _blockers.Add(r);

    /// <summary>
    /// A panel: a soft shadow, a dark body lit from above, a thin highlight along the top and a rounded
    /// border. <paramref name="opaque"/> hides the map completely (for full-screen tables).
    /// </summary>
    public void Panel(Rect r, float radius = Theme.PanelRadius, bool opaque = false)
    {
        _blockers.Add(r);
        Batch.Shadow(r.X, r.Y, r.W, r.H, radius);
        var top = opaque ? Theme.PanelTop.WithAlpha(1) : Theme.PanelTop;
        var bottom = opaque ? Theme.PanelBottom.WithAlpha(1) : Theme.PanelBottom;
        Batch.RoundedRect(r.X, r.Y, r.W, r.H, radius, top, bottom);
        Batch.Rect(r.X + radius, r.Y + 1, r.W - 2 * radius, 1, Theme.Highlight);
        Batch.RoundedOutline(r.X, r.Y, r.W, r.H, radius, Theme.PanelBorder);
    }

    public float Text(float x, float y, string text, Rgba? color = null, FontSize size = FontSize.Normal, bool bold = false) =>
        Font.Draw(Batch, text, x, y, color ?? Theme.Text, size, bold);

    public void TextCentered(Rect r, string text, Rgba? color = null, FontSize size = FontSize.Normal, bool bold = false)
    {
        float w = Font.Measure(text, size, bold);
        float h = Font.LineHeight(size, bold);
        Text(r.X + (r.W - w) / 2, r.Y + (r.H - h) / 2, text, color, size, bold);
    }

    /// <summary>Whether the mouse is over the rectangle, and over the part of it not clipped away.</summary>
    public bool Hover(Rect r) => r.Contains(Input.Mouse) && (_clips.Count == 0 || _clips.Peek().Contains(Input.Mouse));

    /// <summary>True on the frame the button is clicked (pressed and released inside it).</summary>
    public bool Button(Rect r, string label, bool enabled = true, bool active = false, string? tooltip = null, FontSize size = FontSize.Normal)
    {
        _blockers.Add(_clips.Count > 0 ? r.Intersect(_clips.Peek()) : r);
        bool hover = Hover(r);
        bool pressed = enabled && hover && Input.LeftDown && r.Contains(Input.LeftPressPosition);
        // Raised when idle, brighter under the mouse, gold when active, sunk while held down.
        var (top, bottom) = !enabled ? (Theme.ButtonDisabled, Theme.ButtonDisabled)
            : active ? (Theme.ActiveTop, Theme.ActiveBottom)
            : hover ? (Theme.HoverTop, Theme.HoverBottom)
            : (Theme.ButtonTop, Theme.ButtonBottom);
        if (pressed) (top, bottom) = (bottom, top);
        Batch.RoundedRect(r.X, r.Y, r.W, r.H, Theme.ButtonRadius, top, bottom);
        if (enabled && !pressed) Batch.Rect(r.X + Theme.ButtonRadius, r.Y + 1, r.W - 2 * Theme.ButtonRadius, 1, Theme.Highlight);
        Batch.RoundedOutline(r.X, r.Y, r.W, r.H, Theme.ButtonRadius, hover && enabled ? Theme.Accent : active ? Theme.ActiveBottom : Theme.PanelBorder);
        var textArea = pressed ? r with { Y = r.Y + 1 } : r;
        if (enabled) TextCentered(textArea with { X = textArea.X + 1, Y = textArea.Y + 1 }, label, Rgba.Black.WithAlpha(0.45f), size);
        TextCentered(textArea, label, enabled ? active ? Rgba.White : Theme.Text : Theme.TextDisabled, size);
        if (hover && tooltip != null) _tooltip = tooltip;
        bool clicked = enabled && hover && Input.LeftReleased && r.Contains(Input.LeftPressPosition);
        ButtonClicked |= clicked;
        return clicked;
    }

    /// <summary>
    /// A one-line text box that takes this frame's typing (Backspace deletes) and returns the new text.
    /// Only characters the font can draw are accepted. There is one field on screen at a time, so it always has the focus.
    /// </summary>
    public string TextField(Rect r, string text, int maxLength)
    {
        foreach (char c in Input.Chars)
            if (c >= ' ' && c <= (char)255 && !char.IsControl(c) && text.Length < maxLength) text += c;
        if (Input.KeysPressed.Contains(Silk.NET.Input.Key.Backspace) && text.Length > 0) text = text[..^1];

        _blockers.Add(r);
        Batch.RoundedRect(r.X, r.Y, r.W, r.H, Theme.ButtonRadius, new Rgba(0xFF0A0E13), new Rgba(0xFF121821));
        Batch.RoundedOutline(r.X, r.Y, r.W, r.H, Theme.ButtonRadius, Theme.Accent);
        float h = Font.LineHeight(FontSize.Normal);
        float end = Text(r.X + 10, r.Y + (r.H - h) / 2, text);
        Batch.Rect(r.X + 11 + end, r.Y + 7, 2, r.H - 14, Theme.Accent);
        return text;
    }

    public void Tooltip(string text) => _tooltip = text;

    /// <summary>Draws the pending tooltip next to the mouse; call last.</summary>
    public void EndFrame(Vector2 screen)
    {
        if (string.IsNullOrEmpty(_tooltip)) return;
        var lines = _tooltip.Split('\n').SelectMany(l => Font.Wrap(l, 340, FontSize.Small)).ToList();
        float lh = Font.LineHeight(FontSize.Small);
        float w = lines.Max(l => Font.Measure(l, FontSize.Small)) + 16;
        float h = lines.Count * lh + 10;
        float x = Math.Min(Input.Mouse.X + 16, screen.X - w - 4);
        float y = Math.Min(Input.Mouse.Y + 20, screen.Y - h - 4);
        Batch.Shadow(x, y, w, h, Theme.ButtonRadius, 6, 0.4f);
        Batch.RoundedRect(x, y, w, h, Theme.ButtonRadius, new Rgba(0xF6161D27), new Rgba(0xF60B0F14));
        Batch.Rect(x + 2, y, w - 4, 2, Theme.Accent);
        Batch.RoundedOutline(x, y, w, h, Theme.ButtonRadius, Theme.PanelBorder);
        for (int i = 0; i < lines.Count; i++) Text(x + 8, y + 5 + i * lh, lines[i], Theme.Text, FontSize.Small);
    }
}
