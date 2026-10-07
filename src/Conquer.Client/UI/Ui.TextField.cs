using System.Diagnostics;
using Conquer.Client.Graphics;
using Silk.NET.Input;

namespace Conquer.Client.UI;

/// <summary>
/// The text field: a caret that moves with the arrows, Home and End (a word at a time with Ctrl), a selection made with
/// Shift or the mouse, Backspace and Delete (a word with Ctrl), Ctrl+A, Ctrl+C, Ctrl+X and Ctrl+V. When it opens, its
/// text is all selected, so typing replaces it; a click puts the caret where it falls and a double click selects it all.
/// Held keys repeat.
/// </summary>
public sealed partial class Ui
{
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private const double RepeatDelay = 0.45, RepeatEvery = 0.035, DoubleClick = 0.35;
    private static readonly Key[] EditingKeys = [Key.Left, Key.Right, Key.Home, Key.End, Key.Backspace, Key.Delete];

    private long _frame;
    private readonly FieldState _field = new();

    /// <summary>The one field being edited: where it was last frame, its caret and selection, and the key being held.</summary>
    private sealed class FieldState
    {
        public Rect Rect;
        public long Frame = -2;
        public int Caret, Anchor;
        public float Scroll;
        public bool Dragging;
        public double LastClick = -1, LastEdit;
        public Key? Held;
        public double NextRepeat;
    }

    public string TextField(Rect r, string text, int maxLength)
    {
        var f = _field;
        double now = Clock.Elapsed.TotalSeconds;
        // A field that was not there last frame (or has moved) opens with all its text selected.
        if (f.Frame != _frame - 1 || f.Rect.X != r.X || f.Rect.Y != r.Y)
        {
            f.Caret = text.Length;
            f.Anchor = 0;
            f.Scroll = 0;
            f.Dragging = false;
            f.Held = null;
            f.LastEdit = now;
        }
        f.Rect = r;
        f.Frame = _frame;
        f.Caret = Math.Clamp(f.Caret, 0, text.Length);
        f.Anchor = Math.Clamp(f.Anchor, 0, text.Length);

        var inner = new Rect(r.X + 10, r.Y, r.W - 20, r.H);
        bool ctrl = Input.KeysDown.Contains(Key.ControlLeft) || Input.KeysDown.Contains(Key.ControlRight);
        bool shift = Input.KeysDown.Contains(Key.ShiftLeft) || Input.KeysDown.Contains(Key.ShiftRight);
        int before = f.Caret, anchorBefore = f.Anchor;
        string original = text;

        // The mouse: a click places the caret (Shift extends the selection), dragging selects, a double click selects all.
        if (Input.LeftPressed && Hover(r))
        {
            if (now - f.LastClick < DoubleClick)
            {
                f.Anchor = 0;
                f.Caret = text.Length;
                f.LastClick = -1;
            }
            else
            {
                f.Caret = IndexAt(text, Input.Mouse.X - inner.X + f.Scroll);
                if (!shift) f.Anchor = f.Caret;
                f.Dragging = true;
                f.LastClick = now;
            }
        }
        else if (f.Dragging && Input.LeftDown) f.Caret = IndexAt(text, Input.Mouse.X - inner.X + f.Scroll);
        if (!Input.LeftDown) f.Dragging = false;

        if (ctrl && Input.KeysPressed.Contains(Key.A))
        {
            f.Anchor = 0;
            f.Caret = text.Length;
        }
        if (ctrl && (Input.KeysPressed.Contains(Key.C) || Input.KeysPressed.Contains(Key.X)) && f.Caret != f.Anchor)
        {
            Input.WriteClipboard?.Invoke(Selected(text));
            if (Input.KeysPressed.Contains(Key.X)) text = DeleteSelection(text);
        }
        if (ctrl && Input.KeysPressed.Contains(Key.V) && Input.ReadClipboard?.Invoke() is { Length: > 0 } pasted)
            text = Insert(text, new string([.. pasted.Where(Typable)]), maxLength);

        foreach (var key in KeysTyped(now))
        {
            bool selection = f.Caret != f.Anchor;
            switch (key)
            {
                case Key.Left or Key.Right when selection && !shift:
                    // An arrow without Shift drops the selection at its side.
                    f.Caret = f.Anchor = key == Key.Left ? Math.Min(f.Caret, f.Anchor) : Math.Max(f.Caret, f.Anchor);
                    break;
                case Key.Left:
                    f.Caret = ctrl ? WordStart(text, f.Caret) : Math.Max(0, f.Caret - 1);
                    break;
                case Key.Right:
                    f.Caret = ctrl ? WordEnd(text, f.Caret) : Math.Min(text.Length, f.Caret + 1);
                    break;
                case Key.Home:
                    f.Caret = 0;
                    break;
                case Key.End:
                    f.Caret = text.Length;
                    break;
                case Key.Backspace when selection:
                case Key.Delete when selection:
                    text = DeleteSelection(text);
                    break;
                case Key.Backspace when f.Caret > 0:
                {
                    int from = ctrl ? WordStart(text, f.Caret) : f.Caret - 1;
                    text = text.Remove(from, f.Caret - from);
                    f.Caret = f.Anchor = from;
                    break;
                }
                case Key.Delete when f.Caret < text.Length:
                {
                    int to = ctrl ? WordEnd(text, f.Caret) : f.Caret + 1;
                    text = text.Remove(f.Caret, to - f.Caret);
                    f.Anchor = f.Caret;
                    break;
                }
            }
            if (key is Key.Left or Key.Right or Key.Home or Key.End && !shift) f.Anchor = f.Caret;
        }

        // Typed characters (AltGr counts as Ctrl, so these are not checked against it).
        string typed = new([.. Input.Chars.Where(Typable)]);
        if (typed.Length > 0) text = Insert(text, typed, maxLength);
        if (text != original || f.Caret != before || f.Anchor != anchorBefore) f.LastEdit = now;

        // Draw: the box, the selection behind the text, the text scrolled so the caret shows, and the blinking caret.
        _blockers.Add(r);
        Batch.RoundedRect(r.X, r.Y, r.W, r.H, Theme.ButtonRadius, new Rgba(0xFF0A0E13), new Rgba(0xFF121821));
        Batch.RoundedOutline(r.X, r.Y, r.W, r.H, Theme.ButtonRadius, Theme.Accent);
        float caretX = Font.Measure(text[..f.Caret], FontSize.Normal);
        if (caretX - f.Scroll > inner.W - 2) f.Scroll = caretX - inner.W + 2;
        if (caretX - f.Scroll < 0) f.Scroll = caretX;
        f.Scroll = Math.Clamp(f.Scroll, 0, Math.Max(0, Font.Measure(text, FontSize.Normal) - inner.W + 2));

        PushClip(inner with { X = inner.X - 2, W = inner.W + 4 });
        float h = Font.LineHeight(FontSize.Normal), x0 = inner.X - f.Scroll;
        if (f.Caret != f.Anchor)
        {
            int a = Math.Min(f.Caret, f.Anchor), b = Math.Max(f.Caret, f.Anchor);
            float sx = Font.Measure(text[..a], FontSize.Normal), ex = Font.Measure(text[..b], FontSize.Normal);
            Batch.Rect(x0 + sx, r.Y + 6, ex - sx, r.H - 12, Theme.Accent.WithAlpha(0.35f));
        }
        Text(x0, r.Y + (r.H - h) / 2, text);
        if ((now - f.LastEdit) % 1.0 < 0.6) Batch.Rect(x0 + caretX, r.Y + 7, 2, r.H - 14, Theme.Accent);
        PopClip();
        return text;

        string Selected(string t) => t[Math.Min(f.Caret, f.Anchor)..Math.Max(f.Caret, f.Anchor)];

        string DeleteSelection(string t)
        {
            int a = Math.Min(f.Caret, f.Anchor);
            t = t.Remove(a, Math.Abs(f.Caret - f.Anchor));
            f.Caret = f.Anchor = a;
            return t;
        }

        // Puts the characters at the caret, over the selection, as many as fit.
        string Insert(string t, string chars, int max)
        {
            t = DeleteSelection(t);
            chars = chars[..Math.Min(chars.Length, Math.Max(0, max - t.Length))];
            t = t.Insert(f.Caret, chars);
            f.Caret = f.Anchor = f.Caret + chars.Length;
            return t;
        }
    }

    private static bool Typable(char c) => c >= ' ' && c <= (char)255 && !char.IsControl(c);

    /// <summary>
    /// The editing keys pressed this frame, and the one held down repeating: by the system's own repeat when it sends
    /// one, or else after <see cref="RepeatDelay"/>, every <see cref="RepeatEvery"/>.
    /// </summary>
    private IEnumerable<Key> KeysTyped(double now)
    {
        var f = _field;
        var keys = Input.KeysPressed.Concat(Input.KeysRepeated).Where(k => EditingKeys.Contains(k)).ToList();
        Key? pressed = Input.KeysPressed.Where(k => EditingKeys.Contains(k)).Select(k => (Key?)k).LastOrDefault();
        if (pressed is Key down)
        {
            f.Held = down;
            f.NextRepeat = now + RepeatDelay;
        }
        else if (f.Held is Key held && Input.KeysDown.Contains(held))
        {
            if (Input.KeysRepeated.Count == 0 && !Input.SystemRepeats && now >= f.NextRepeat)
            {
                keys.Add(held);
                f.NextRepeat = now + RepeatEvery;
            }
        }
        else f.Held = null;
        return keys;
    }

    /// <summary>Where a click this far into the text falls: before the character whose middle is past it.</summary>
    private int IndexAt(string text, float x)
    {
        for (int i = 0; i < text.Length; i++)
        {
            float left = Font.Measure(text[..i], FontSize.Normal), right = Font.Measure(text[..(i + 1)], FontSize.Normal);
            if (x < (left + right) / 2) return i;
        }
        return text.Length;
    }

    /// <summary>The start of the word before the caret, past any spaces.</summary>
    private static int WordStart(string text, int i)
    {
        while (i > 0 && text[i - 1] == ' ') i--;
        while (i > 0 && text[i - 1] != ' ') i--;
        return i;
    }

    /// <summary>The end of the word after the caret, past any spaces.</summary>
    private static int WordEnd(string text, int i)
    {
        while (i < text.Length && text[i] == ' ') i++;
        while (i < text.Length && text[i] != ' ') i++;
        return i;
    }
}
