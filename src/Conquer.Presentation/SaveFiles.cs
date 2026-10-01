using Conquer.Game.Simulation;

namespace Conquer.Presentation;

/// <summary>A saved game on disk: its name (the file name) and when it was saved.</summary>
public sealed record SaveFile(string Path, string Name, DateTime SavedAt);

/// <summary>
/// Saved games live in a Partidas folder next to the game. If the game's folder cannot be written to, they go to the
/// user's data folder instead, where they lived before 1.45.1: ~/.local/share/Conquer/Partidas on Linux,
/// %LOCALAPPDATA%\Conquer\Partidas on Windows and ~/Library/Application Support/Conquer/Partidas on macOS.
/// </summary>
public static class SaveFiles
{
    public const string Extension = ".conquer";

    /// <summary>Where saved games went before 1.45.1, and where they still go if the game's folder is read-only.</summary>
    private static readonly string UserFolder = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create),
        "Conquer", "Partidas");

    public static string Folder { get; } = ChooseFolder();

    /// <summary>The Partidas folder next to the game if it can write there, bringing over the games saved in the user's folder.</summary>
    private static string ChooseFolder()
    {
        string folder = System.IO.Path.Combine(AppContext.BaseDirectory, "Partidas");
        try
        {
            Directory.CreateDirectory(folder);
            string probe = System.IO.Path.Combine(folder, ".probe");
            File.WriteAllText(probe, "");
            File.Delete(probe);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return UserFolder;
        }
        if (Directory.Exists(UserFolder))
            foreach (var old in Directory.GetFiles(UserFolder, "*" + Extension))
            {
                string moved = System.IO.Path.Combine(folder, System.IO.Path.GetFileName(old));
                if (!File.Exists(moved)) File.Move(old, moved);
            }
        return folder;
    }

    /// <summary>Saved games, newest first.</summary>
    public static List<SaveFile> List()
    {
        if (!Directory.Exists(Folder)) return [];
        return new DirectoryInfo(Folder).GetFiles("*" + Extension)
            .Select(f => new SaveFile(f.FullName, System.IO.Path.GetFileNameWithoutExtension(f.Name), f.LastWriteTime))
            .OrderByDescending(f => f.SavedAt)
            .ToList();
    }

    /// <summary>Writes the game as "Nation - date", stamped with the game <paramref name="version"/>, and returns that name; saving again at the same moment overwrites it.</summary>
    public static string Save(GameSession session, string version)
    {
        var date = session.Date;
        string name = Clean($"{session.Human.Name} - {date.ToString().Replace(", ", " ").Replace(":00", "h")}");
        Directory.CreateDirectory(Folder);
        string path = System.IO.Path.Combine(Folder, name + Extension);
        // Write to a temporary file first so a crash never leaves a half-written save behind.
        string temporary = path + ".tmp";
        using (var file = File.Create(temporary)) session.ToSave(version).Write(file);
        File.Move(temporary, path, overwrite: true);
        return name;
    }

    public static SaveGame Read(string path)
    {
        using var file = File.OpenRead(path);
        return SaveGame.Read(file);
    }

    public static void Delete(SaveFile save) => File.Delete(save.Path);

    /// <summary>Drops characters that Windows, macOS or Linux do not allow in file names.</summary>
    private static string Clean(string name)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars().Concat(['<', '>', ':', '"', '/', '\\', '|', '?', '*']).ToHashSet();
        return new string(name.Where(c => !invalid.Contains(c)).ToArray()).Trim().TrimEnd('.');
    }
}
