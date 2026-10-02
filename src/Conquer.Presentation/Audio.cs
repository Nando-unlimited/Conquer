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

    /// <summary>A volume a quarter up or down, between silence and the loudest.</summary>
    private static double Step(double volume, int quarters) => Math.Clamp(Math.Round(volume * 4) + quarters, 0, 4) / 4;

    public void ChangeMusic(int quarters)
    {
        Music = Step(Music, quarters);
        Save();
    }

    public void ChangeSounds(int quarters)
    {
        Sounds = Step(Sounds, quarters);
        Save();
    }

    /// <summary>A volume as the options show it: "No" when silent.</summary>
    public static string Label(double volume) => volume <= 0 ? "No" : $"{volume:P0}";
}
