using Conquer.Game.Economy;
using Conquer.Game.Military;
using Conquer.Game.Rules;

namespace Conquer.Presentation;

/// <summary>What a piece of text or a bar means, which the client turns into a colour of its theme.</summary>
public enum Tone
{
    Normal,
    Dim,
    Disabled,
    Accent,
    Good,
    Bad,
    River,
    Battle,
    Strength,
    Organisation,
    /// <summary>The dark groove a bar fills.</summary>
    Track,
    /// <summary>The groove of a progress bar inside a panel.</summary>
    Groove,
    /// <summary>The border of a card or a row.</summary>
    Border,
    /// <summary>The two middle mood levels, between unrest (<see cref="Bad"/>) and content (<see cref="Good"/>).</summary>
    Uneasy,
    Calm,
}

public enum TextSize
{
    Small,
    Normal,
    Large,
    /// <summary>The title face: the game's name and the nations on the map.</summary>
    Title,
}

/// <summary>A <see cref="Tone"/>, or a nation's own colour (0xAARRGGBB) when <see cref="Color"/> is set.</summary>
public readonly record struct Ink(Tone Tone, uint Color = 0)
{
    public static implicit operator Ink(Tone tone) => new(tone);

    public static Ink Nation(uint color) => new(Tone.Normal, color);

    /// <summary>Red in unrest, green when content, <paramref name="normal"/> in between.</summary>
    public static Ink Mood(double mood, Tone normal) =>
        mood < GameRules.UnrestMood ? Tone.Bad : GameRules.MoodLevel(mood) == 3 ? Tone.Good : normal;
}

/// <summary>A small picture before a label: a resource or a kind of battalion.</summary>
public abstract record Icon;
public sealed record ResourceIcon(ResourceType Resource) : Icon;
public sealed record BattalionIcon(BattalionType Battalion) : Icon;

/// <summary>
/// One piece of a <see cref="Document"/>, stacked top to bottom. Heights and gaps are in interface pixels, so every
/// client lays a document out the same way.
/// </summary>
public abstract record Element;

/// <summary>Bold text on a line of its own, followed by <paramref name="Height"/> pixels.</summary>
public sealed record Heading(string Text, Ink Ink, TextSize Size = TextSize.Normal, float Height = 26) : Element;

/// <summary>One line of text that is not wrapped.</summary>
public sealed record Label(string Text, Ink Ink, TextSize Size = TextSize.Small, bool Bold = false, float Height = 22, float Indent = 0, string? Tooltip = null) : Element;

/// <summary>Text wrapped to the width (less <paramref name="Indent"/>), followed by <paramref name="After"/> pixels.</summary>
public sealed record Paragraph(string Text, Ink Ink, TextSize Size = TextSize.Small, float After = 0, float Indent = 0) : Element;

/// <summary>A label and its value in two columns, optionally headed by an icon; hovering shows the tooltip.</summary>
public sealed record Info(string Label, string Value, Ink Ink = default, string? Tooltip = null, Icon? Icon = null) : Element;

/// <summary>Text on the left and on the right of one line, the left one optionally after an icon.</summary>
public sealed record Row(string Left, string Right, Ink LeftInk, Ink RightInk, bool Bold = false, float Height = 20, float Indent = 0, Icon? Icon = null, string? Tooltip = null) : Element;

/// <summary>A button across the width, followed by <paramref name="Gap"/> pixels; <paramref name="OnClick"/> runs when it is pressed.</summary>
public sealed record Button(string Text, Action? OnClick, bool Enabled = true, bool Active = false, string? Tooltip = null,
    TextSize Size = TextSize.Normal, float Height = 32, float Gap = 6, Icon? Icon = null) : Element
{
    /// <summary>Runs the button's action, unless it is disabled.</summary>
    public void Press()
    {
        if (Enabled) OnClick?.Invoke();
    }
}

/// <summary>Buttons sharing a line in equal parts (tabs, for example); their own heights and gaps are ignored.</summary>
public sealed record ButtonRow(IReadOnlyList<Button> Buttons, float Height = 32, float Gap = 8, float Spacing = 6) : Element;

/// <summary>A number between buttons that lower and raise it.</summary>
public sealed record Stepper(string Value, IReadOnlyList<Button> Less, IReadOnlyList<Button> More, float Height = 28, float Gap = 6) : Element;

/// <summary>A line of small text with a small button at its right end.</summary>
public sealed record LabelAndButton(string Text, Ink Ink, Button Button, float ButtonWidth = 60) : Element;

/// <summary>A bar filled to <paramref name="Fraction"/> (0 to 1), followed by <paramref name="Gap"/> pixels; <paramref name="Mark"/> (0 to 1) draws a red notch, such as the point where units break.</summary>
public sealed record Bar(double Fraction, Ink Fill, Ink Track, float Thickness, float Gap, double? Mark = null) : Element;

public sealed record Space(float Height) : Element;

/// <summary>A label on the left and its value against the right edge.</summary>
public sealed record Pair(string Label, string Value, Ink Ink = default, string? Tooltip = null, float Indent = 0, TextSize Size = TextSize.Normal, float Height = 24) : Element;

/// <summary>One column of a <see cref="Columns"/> line, with its own tooltip.</summary>
public sealed record ColumnText(string Text, Ink Ink = default, string? Tooltip = null);

/// <summary>A label and texts in columns that start at fixed distances from the right edge (a small table).</summary>
public sealed record Columns(string Label, IReadOnlyList<ColumnText> Values, IReadOnlyList<float> FromRight, TextSize Size = TextSize.Normal, float Height = 24) : Element;

/// <summary>One part of a <see cref="Distribution"/>: its share of the bar, its colour, and its legend line.</summary>
public sealed record Share(double Value, Ink Ink, string Label, string Amount);

/// <summary>A bar split between the parts, then a legend line for each from the last part to the first.</summary>
public sealed record Distribution(IReadOnlyList<Share> Parts) : Element;

/// <summary>A nation's name in bold after a square of its colour (0xAARRGGBB), with a small note on the right.</summary>
public sealed record Banner(string Text, uint Color, string Right = "") : Element;

/// <summary>
/// A unit in a list: its name and a note on the right, a line about it, and its strength and organisation bars side
/// by side. With a tooltip, the row lights up under the mouse. Rows that do not fit above the bottom become «y N más».
/// </summary>
public sealed record UnitEntry(string Name, Ink NameInk, string Right, Ink RightInk, string Line, Ink LineInk, double Strength, double Organisation,
    string? Tooltip = null) : Element;

/// <summary>A panel's contents, top to bottom; <see cref="OnClose"/> (if any) is its close button.</summary>
public sealed class Document
{
    public List<Element> Elements { get; } = [];
    public Action? OnClose { get; init; }
    /// <summary>What replaces the <see cref="UnitEntry"/> rows that do not fit ("{0}" is how many).</summary>
    public string Hidden { get; init; } = "y {0} más";

    public void Add(Element element) => Elements.Add(element);
}

/// <summary>Officers' portraits side by side, each with a caption under it (and a tooltip), <see cref="Size"/> pixels square.</summary>
public sealed record Portraits(IReadOnlyList<(Portrait Portrait, string Caption, string Tooltip)> Officers, float Size = 64) : Element;
