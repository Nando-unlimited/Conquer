namespace Conquer.Presentation;

/// <summary>Who made the game and whose work it uses, with the licences that ask to be cited; the title screen shows it.</summary>
public static class Credits
{
    /// <summary>The line at the foot of the title screen.</summary>
    public const string Copyright = "© 2026 Fernando Fawcett. Todos los derechos reservados.";

    public static IReadOnlyList<MarkupLine> Lines { get; } = new[]
    {
        "## Conquer",
        "Un juego de Fernando Fawcett: diseño y programación.",
        "© 2026 Fernando Fawcett. Todos los derechos reservados. La música, los sonidos, las maquetas, las tipografías y los datos del mapa son de sus autores, con las licencias que se citan abajo.",
        "## Música",
        "- «Lord of the Land», Kevin MacLeod (incompetech.com). Licensed under Creative Commons: By Attribution 4.0 License (https://creativecommons.org/licenses/by/4.0/).",
        "- «The Britons», Kevin MacLeod (FreePD.com), dominio público.",
        "- «Medieval Exploration», «Harvest Season», «Market Day», «King's Feast» y «Battle»: RandomMind (OpenGameArt.org), CC0.",
        "- «War Theme»: Spring Spring; «Fantasy Orchestral Theme»: Joth; «At Home - Orchestral»: Wolfgang_; «New Sunrise»: nene; «Determined Pursuit» y «Orchestral Battle Theme»: Emma_MA (OpenGameArt.org), CC0.",
        "## Efectos de sonido",
        "- Kenney (www.kenney.nl): Interface Sounds, Impact Sounds, RPG Audio y Music Jingles, CC0.",
        "- «Classic Fanfare Lick»: fvcalderan; «Horde War Drums Loop»: William Hector; «Point Bell»: HaelDB (OpenGameArt.org), CC0.",
        "## Gráficos",
        "- Ciudades y edificios: renderizados del Hexagon Kit de Kenney (www.kenney.nl), CC0.",
        "- Unidades, barcos y aviones: modelados para este juego al estilo de los kits de Kenney.",
        "## Tipografías",
        "- Cinzel: The Cinzel Project Authors (Natanael Gama), SIL Open Font License 1.1.",
        "- Lato: Lukasz Dziedzic, SIL Open Font License 1.1.",
        "## Mapa de la Tierra",
        "- Relieve y batimetría: NASA Visible Earth, GEBCO, dominio público.",
        "- Costas, lagos y glaciares: Natural Earth, dominio público.",
    }.Select(Markup.Parse).ToList();
}
