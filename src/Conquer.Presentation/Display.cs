using System.Text.Json;

namespace Conquer.Presentation;

/// <summary>How the map looks: whether its icons move. Kept in pantalla.json in the saves folder.</summary>
public sealed class DisplaySettings
{
    public bool Animations { get; set; } = true;

    private static string FilePath => Path.Combine(SaveFiles.Folder, "pantalla.json");

    public static DisplaySettings Current { get; } = Load();

    private static DisplaySettings Load()
    {
        try
        {
            return File.Exists(FilePath) ? JsonSerializer.Deserialize<DisplaySettings>(File.ReadAllText(FilePath)) ?? new() : new();
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return new();
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(SaveFiles.Folder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Not being able to remember a preference is no reason to stop the game.
        }
    }

    public void ToggleAnimations()
    {
        Animations = !Animations;
        Save();
    }

    /// <summary>The menu buttons that switch the display options.</summary>
    public IReadOnlyList<Button> Buttons() =>
    [
        new($"Animaciones: {(Animations ? "sí" : "no")}", ToggleAnimations,
            Tooltip: "Las unidades saltan al marchar y tiemblan en combate, los barcos se mecen y sale humo de las ciudades."),
    ];
}
