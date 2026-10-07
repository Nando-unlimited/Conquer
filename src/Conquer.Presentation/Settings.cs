namespace Conquer.Presentation;

/// <summary>
/// The options window, the same from the title screen and from the pause menu: music and sound volumes (a value
/// between - and +), whether the map's icons move, and whether units, cities and buildings are 3D models or counters.
/// Every change is kept at once (<see cref="AudioSettings"/>, <see cref="DisplaySettings"/>).
/// </summary>
public sealed class SettingsMenu
{
    public bool Open { get; set; }

    private static AudioSettings Audio => AudioSettings.Current;
    private static DisplaySettings Display => DisplaySettings.Current;

    public IReadOnlyList<OptionRow> Rows() =>
    [
        Volume("Música", Audio.Music, Audio.ChangeMusic, "la música"),
        Volume("Sonido", Audio.Sounds, Audio.ChangeSounds, "los efectos"),
        new("Animaciones", true,
        [
            new Button("Sí", () => Display.SetAnimations(true), Active: Display.Animations,
                Tooltip: "Las unidades saltan al marchar y tiemblan en combate, los barcos se mecen y sale humo de las ciudades."),
            new Button("No", () => Display.SetAnimations(false), Active: !Display.Animations, Tooltip: "Los iconos del mapa se quedan quietos."),
        ]),
        new("Mapa", true,
        [
            new Button("Figuras 3D", () => Display.SetUnitModels(true), Active: Display.UnitModels,
                Tooltip: "Unidades y barcos como maquetas, y las ciudades con su icono."),
            new Button("Etiquetas", () => Display.SetUnitModels(false), Active: !Display.UnitModels,
                Tooltip: "Fichas como las de Hearts of Iron III para las unidades (con su bandera, apiladas) y puntos para las ciudades, sin iconos (lo normal)."),
        ]),
    ];

    private static OptionRow Volume(string label, double volume, Action<int> change, string what) =>
        new(label, true, [new Button("-", () => change(-1), volume > 0, Tooltip: $"Bajar el volumen de {what}.")], AudioSettings.Label(volume),
            [new Button("+", () => change(1), volume < 1, Tooltip: $"Subir el volumen de {what}.")]);

    public Button Close => new("Cerrar", () => Open = false);
}
