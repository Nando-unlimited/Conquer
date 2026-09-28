using System.Numerics;
using Conquer.Client.Graphics;

namespace Conquer.Client.UI;

public readonly record struct Rect(float X, float Y, float W, float H)
{
    public float Right => X + W;
    public float Bottom => Y + H;
    public bool Contains(Vector2 p) => p.X >= X && p.X < X + W && p.Y >= Y && p.Y < Y + H;
    public Rect Inset(float d) => new(X + d, Y + d, W - 2 * d, H - 2 * d);
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

    public void EndFrame()
    {
        LeftPressed = LeftReleased = RightPressed = false;
        Scroll = 0;
        KeysPressed.Clear();
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
        _tooltip = null;
    }

    public bool MouseOverUi => _blockers.Any(r => r.Contains(Input.Mouse));

    /// <summary>Marks an area as interface without drawing anything.</summary>
    public void Block(Rect r) => _blockers.Add(r);

    public void Panel(Rect r)
    {
        _blockers.Add(r);
        Batch.Rect(r.X, r.Y, r.W, r.H, Theme.Panel);
        Batch.Outline(r.X, r.Y, r.W, r.H, Theme.PanelBorder);
    }

    public float Text(float x, float y, string text, Rgba? color = null, FontSize size = FontSize.Normal, bool bold = false) =>
        Font.Draw(Batch, text, x, y, color ?? Theme.Text, size, bold);

    public void TextCentered(Rect r, string text, Rgba? color = null, FontSize size = FontSize.Normal, bool bold = false)
    {
        float w = Font.Measure(text, size, bold);
        float h = Font.LineHeight(size, bold);
        Text(r.X + (r.W - w) / 2, r.Y + (r.H - h) / 2, text, color, size, bold);
    }

    public bool Hover(Rect r) => r.Contains(Input.Mouse);

    /// <summary>True on the frame the button is clicked (pressed and released inside it).</summary>
    public bool Button(Rect r, string label, bool enabled = true, bool active = false, string? tooltip = null, FontSize size = FontSize.Normal)
    {
        _blockers.Add(r);
        bool hover = Hover(r);
        var fill = !enabled ? Theme.ButtonDisabled : active ? Theme.ButtonActive : hover ? Theme.ButtonHover : Theme.Button;
        Batch.Rect(r.X, r.Y, r.W, r.H, fill);
        Batch.Outline(r.X, r.Y, r.W, r.H, hover && enabled ? Theme.Accent : Theme.PanelBorder);
        TextCentered(r, label, enabled ? Theme.Text : Theme.TextDisabled, size);
        if (hover && tooltip != null) _tooltip = tooltip;
        return enabled && hover && Input.LeftReleased && r.Contains(Input.LeftPressPosition);
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
        Batch.Rect(x, y, w, h, new Rgba(0xF20C1016));
        Batch.Outline(x, y, w, h, Theme.Accent);
        for (int i = 0; i < lines.Count; i++) Text(x + 8, y + 5 + i * lh, lines[i], Theme.Text, FontSize.Small);
    }
}
