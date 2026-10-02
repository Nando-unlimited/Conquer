using System.Text.Json;
using Conquer.Game.Science;

namespace Conquer.Presentation;

/// <summary>The sound effects the game plays; the client maps each to a file in Assets/Audio/Sfx.</summary>
public enum SoundCue
{
    Click,
    Confirm,
    Alert,
    Battle,
    War,
    Peace,
    Build,
    Discovery,
    CityFounded,
    Coins,
    Bell,
}

/// <summary>Which music plays: tracks in Assets/Audio/Music, by age and by war or peace.</summary>
public static class Soundtrack
{
    private static readonly string[] EarlyPeace = ["exploration", "harvest_season", "market_day", "kings_feast", "lord_of_the_land"];
    private static readonly string[] EarlyWar = ["medieval_battle", "war_theme"];
    private static readonly string[] LatePeace = ["fantasy_orchestral", "cinematic_calm", "new_sunrise"];
    private static readonly string[] LateWar = ["determined_pursuit", "orchestral_battle"];

    /// <summary>
    /// The tracks for a nation in an age, at war or at peace: medieval music up to the Middle Ages, orchestral from the
    /// age of Discoveries on.
    /// </summary>
    public static IReadOnlyList<string> For(Era era, bool atWar) =>
        era < Era.Renaissance ? atWar ? EarlyWar : EarlyPeace : atWar ? LateWar : LatePeace;

    /// <summary>The music of the title and menu screens: «The Britons», by Kevin MacLeod.</summary>
    public static IReadOnlyList<string> Menu => ["the_britons"];
}

/// <summary>How loud the music and the sound effects play, from 0 to 1; kept in a file beside the saved games.</summary>
public sealed class AudioSettings
{
    public double Music { get; set; } = 0.5;
    public double Sounds { get; set; } = 0.75;

    private static string FilePath => Path.Combine(SaveFiles.Folder, "sonido.json");

    public static AudioSettings Current { get; } = Load();

    private static AudioSettings Load()
    {
        try
        {
            return File.Exists(FilePath) ? JsonSerializer.Deserialize<AudioSettings>(File.ReadAllText(FilePath)) ?? new() : new();
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
            // Not being able to remember the volume is no reason to stop the game.
        }
    }

    /// <summary>Steps a volume up by a quarter, back to silence after the loudest.</summary>
    private static double Next(double volume) => volume >= 0.99 ? 0 : Math.Round(volume * 4 + 1) / 4;

    public void NextMusic()
    {
        Music = Next(Music);
        Save();
    }

    public void NextSounds()
    {
        Sounds = Next(Sounds);
        Save();
    }

    public static string Label(string what, double volume) => volume <= 0 ? $"{what}: no" : $"{what}: {volume:P0}";

    /// <summary>The menu buttons that step the music and the sound volume.</summary>
    public IReadOnlyList<Button> Buttons() =>
    [
        new(Label("Música", Music), NextMusic, Tooltip: "Volumen de la música: pulsa para subirlo; después del máximo, se apaga."),
        new(Label("Sonido", Sounds), NextSounds, Tooltip: "Volumen de los efectos: pulsa para subirlo; después del máximo, se apagan."),
    ];
}
