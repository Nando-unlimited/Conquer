namespace Conquer.Game.Rules;

/// <summary>The faiths of the world. Each nation follows one; the people of each province keep theirs until they convert.</summary>
public static class Religions
{
    public static readonly string[] Names = ["Culto del Sol", "Fe del Río", "Antiguos Dioses", "Camino de los Astros", "Madre Tierra"];

    /// <summary>Each faith's colour on the religion map (0xAARRGGBB).</summary>
    public static readonly uint[] Colors = [0xFFE8B730, 0xFF3C8DD9, 0xFFB0413E, 0xFF8C5BD6, 0xFF4CA35A];

    public static int Count => Names.Length;
}
