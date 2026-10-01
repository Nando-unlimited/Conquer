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
}

public enum TextSize
{
    Small,
    Normal,
    Large,
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
    TextSize Size = TextSize.Normal, float Height = 32, float Gap = 6, Icon? Icon = null) : Element;

/// <summary>Buttons sharing a line in equal parts (tabs, for example); their own heights and gaps are ignored.</summary>
public sealed record ButtonRow(IReadOnlyList<Button> Buttons, float Height = 32, float Gap = 8, float Spacing = 6) : Element;

/// <summary>A number between buttons that lower and raise it.</summary>
public sealed record Stepper(string Value, IReadOnlyList<Button> Less, IReadOnlyList<Button> More, float Height = 28, float Gap = 6) : Element;

/// <summary>A line of small text with a small button at its right end.</summary>
public sealed record LabelAndButton(string Text, Ink Ink, Button Button, float ButtonWidth = 60) : Element;

/// <summary>A bar filled to <paramref name="Fraction"/> (0 to 1), followed by <paramref name="Gap"/> pixels.</summary>
public sealed record Bar(double Fraction, Ink Fill, Ink Track, float Thickness, float Gap) : Element;

public sealed record Space(float Height) : Element;

/// <summary>A panel's contents, top to bottom; <see cref="OnClose"/> (if any) is its close button.</summary>
public sealed class Document
{
    public List<Element> Elements { get; } = [];
    public Action? OnClose { get; init; }

    public void Add(Element element) => Elements.Add(element);
}
