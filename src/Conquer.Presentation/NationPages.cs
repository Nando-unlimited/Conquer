using Conquer.Game.Military;

namespace Conquer.Presentation;

public sealed record Column(string Title, float Width);

/// <summary>A bar inside a table cell: <see cref="Top"/> pixels below the row's top, as wide as the column less <see cref="Inset"/>.</summary>
public sealed record CellBar(double Fraction, Ink Fill, float Top, float Thickness, float Inset);

public abstract record Cell;

/// <summary>
/// Text in a table cell, <see cref="Top"/> pixels below the row's top. A <see cref="Suffix"/> follows it in small dim
/// letters (the text is cut short to leave it room); a <see cref="Swatch"/> (0xAARRGGBB) is a nation's colour before it.
/// </summary>
public sealed record TextCell(string Text, Ink Ink = default, TextSize Size = TextSize.Normal, bool Bold = false, float Indent = 0, float Top = 6,
    string? Suffix = null, string? Tooltip = null, uint? Swatch = null, CellBar? Bar = null) : Cell;

/// <summary>Buttons sharing a cell, as wide as the column less <see cref="Inset"/>.</summary>
public sealed record ButtonsCell(IReadOnlyList<Button> Buttons, float Inset = 6) : Cell;

/// <summary>
/// A table whose first <see cref="Sortable"/> column titles sort it when pressed (<see cref="SortBy"/>); with no rows
/// it shows <see cref="Empty"/> instead, if there is one.
/// </summary>
public sealed record Table(IReadOnlyList<Column> Columns, IReadOnlyList<IReadOnlyList<Cell>> Rows, int Sortable = 0, int SortColumn = 0,
    bool SortAscending = false, Action<int>? SortBy = null, string? Empty = null);

/// <summary>What a tab of the nation screen shows.</summary>
public abstract record NationPage;

/// <summary>A table, under a bold line when there is a <see cref="Title"/>.</summary>
public sealed record TablePage(Table Table, string? Title = null, Ink TitleInk = default) : NationPage;

/// <summary>Several tables one under the other, scrolling together.</summary>
public sealed record TablesPage(IReadOnlyList<TablePage> Tables) : NationPage;

/// <summary>Two columns of figures.</summary>
public sealed record SummaryPage(Document Left, Document Right) : NationPage;

public sealed record InstitutionBadge(string Text, Ink Ink, string Tooltip, Button? Adopt);

/// <summary>
/// One advance: its name, state (cost or progress) and the button to research it, two lines of description, notes
/// at the bottom (what it needs, what it allows) and, once started, a bar with its progress.
/// </summary>
public sealed record TechCard(string Name, Ink NameInk, string State, Button? Research, string Description, Ink DescriptionInk,
    IReadOnlyList<(string Text, Ink Ink)> Notes, double? Progress, Ink ProgressInk, bool Known, Ink Border);

public sealed record TechLevel(string Title, Ink Ink, IReadOnlyList<TechCard> Cards);

/// <summary>
/// A branch of science: its priority with the buttons to change it, its share of the science, what it is researching
/// (<see cref="Progress"/> is null when nothing) and its advances of the age shown, level by level.
/// </summary>
public sealed record BranchColumn(string Name, string Priority, Button Less, Button More, string Share, string Status, Ink StatusInk,
    double? Progress, IReadOnlyList<TechLevel> Levels);

public sealed record SciencePage(string Points, Ink PointsInk, string PointsTooltip, IReadOnlyList<InstitutionBadge> Institutions,
    IReadOnlyList<Button> Eras, IReadOnlyList<BranchColumn> Branches) : NationPage;

/// <summary>A place in a template: a battalion (its model and line) with its figures and the button to remove it, or an empty place.</summary>
public sealed record TemplateSlot(BattalionType? Battalion, string Name, string Line, string Stats, Button? Remove);

/// <summary>The designer's rows, HOI4 style; only a way of showing the regiment, whose places they all share.</summary>
public enum TemplateRow
{
    Front,
    Ranged,
    Support,
}

/// <summary>One row of the designer: its battalions, an empty place while the regiment has room, and the battalions it can take.</summary>
/// <see cref="None"/> says why there is nothing to add, when nothing is known for the row yet.
public sealed record TemplateSection(string Name, string Tooltip, IReadOnlyList<TemplateSlot> Slots, IReadOnlyList<Button> Add, string? None = null);

/// <summary>
/// The template designer. While its name is being changed, <see cref="Renaming"/> holds the buttons to accept and cancel, and
/// the client edits <see cref="NationScreen.TemplateNameDraft"/> in place of the name.
/// </summary>
public sealed record TemplatesPage(IReadOnlyList<Button> Templates, IReadOnlyList<Button> Actions, string Name, string Kind, string Free,
    IReadOnlyList<TemplateSection> Sections, Document Details, Button? Rename = null, IReadOnlyList<Button>? Renaming = null) : NationPage;

/// <summary>One nation's line on a graph: its points from 0 to 1 on both axes, its latest figure, and whether it is the player's.</summary>
public sealed record ChartSeries(string Name, uint Color, IReadOnlyList<(float X, float Y)> Points, string Last, bool Player);

/// <summary>A mark on an axis of a graph, from 0 to 1 along it.</summary>
public sealed record ChartTick(float At, string Text);

/// <summary>
/// The statistics: buttons to choose the figure, then a graph with one line per nation over the game, its axes'
/// marks and a legend with each nation's latest figure. <see cref="Empty"/> when there is nothing to draw yet.
/// </summary>
public sealed record StatisticsPage(IReadOnlyList<Button> Metrics, string Title, string? Empty, IReadOnlyList<ChartSeries> Series,
    IReadOnlyList<ChartTick> YTicks, IReadOnlyList<ChartTick> XTicks) : NationPage;
