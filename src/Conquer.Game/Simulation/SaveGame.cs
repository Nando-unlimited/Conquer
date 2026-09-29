using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Conquer.Game.Buildings;
using Conquer.Game.Economy;
using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Science;
using Conquer.Game.World;

namespace Conquer.Game.Simulation;

/// <summary>
/// Everything a game needs to carry on later, as plain data written to gzipped JSON. The map itself is
/// not stored: it is generated again from <see cref="World"/>, and <see cref="MapFingerprint"/> checks
/// that the generator still makes the same one. Enums are written by name so reordering them is safe.
/// </summary>
public sealed record SaveGame
{
    /// <summary>Bumped when the format changes in a way older saves cannot be read into.</summary>
    public const int CurrentFormat = 1;

    public int Format { get; init; } = CurrentFormat;
    /// <summary>Version of the game that wrote it, for display.</summary>
    public string GameVersion { get; init; } = "";
    public DateTime SavedAtUtc { get; init; }

    public required WorldSettings World { get; init; }
    public required long MapFingerprint { get; init; }
    /// <summary>
    /// Written after the generator began placing extra deposits (1.13.0). Older saves hold no reserves for
    /// those, so loading one fills them up.
    /// </summary>
    public bool ExtraDeposits { get; init; }
    public required long Hours { get; init; }
    public required bool ComputerRivals { get; init; }

    public required List<PlayerSave> Players { get; init; }
    public required List<ProvinceSave> Provinces { get; init; }
    public required List<CitySave> Cities { get; init; }
    public required List<UnitSave> Units { get; init; }
    public required List<MigrationSave> Migrations { get; init; }
    public required List<BattleSave> Battles { get; init; }
    public required List<WarSave> Wars { get; init; }
    public required List<Notification> Notifications { get; init; }
    public required List<AiSave> Ais { get; init; }
    public required Dictionary<int, double> EmigrationCarry { get; init; }
    public required List<UnitNumberSave> UnitNumbers { get; init; }
    public required int NextUnitId { get; init; }
    public required int NextCityId { get; init; }
    public required int NextMigrationId { get; init; }
    public required int NextTemplateId { get; init; }

    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() },
        IncludeFields = true,
    };

    public void Write(Stream stream)
    {
        using var gzip = new GZipStream(stream, CompressionLevel.Optimal, leaveOpen: true);
        JsonSerializer.Serialize(gzip, this, Options);
    }

    /// <summary>Reads a save, or throws <see cref="InvalidDataException"/> if it is damaged or from an incompatible version.</summary>
    public static SaveGame Read(Stream stream)
    {
        SaveGame? save;
        try
        {
            using var gzip = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true);
            save = JsonSerializer.Deserialize<SaveGame>(gzip, Options);
        }
        catch (Exception e) when (e is JsonException or InvalidDataException or NotSupportedException)
        {
            throw new InvalidDataException("La partida guardada está dañada.", e);
        }
        if (save == null) throw new InvalidDataException("La partida guardada está vacía.");
        if (save.Format != CurrentFormat)
            throw new InvalidDataException($"La partida es de otra versión del juego ({save.GameVersion}) y no se puede cargar.");
        return save;
    }
}

public sealed record PlayerSave(
    int Id, string Name, uint Color, bool IsHuman, double[] Stockpile, int? CapitalCityId,
    double[] LastDayNet, bool IsStarving, double FoodReserveDays, List<Tech> Techs,
    double[] ResearchProgress, double SpareScience, double LastDayScience, List<TemplateSave> Templates,
    int[]? ResearchPriorities = null, List<Tech>? CurrentResearch = null);

public sealed record TemplateSave(int Id, int Number, List<BattalionType> Battalions);

/// <summary>Only what changes during a game; the rest of each province comes from the generated map.</summary>
public sealed record ProvinceSave(
    int Id, int OwnerId, int ControllerId, double Population, int? CityId, double Mood, double Fertility,
    double[] Reserves, List<BuildingType> Buildings, BuildingType? Constructing, int ConstructionDaysLeft, string? PlannedCityName = null);

public sealed record CitySave(int Id, string Name, int OwnerId, int ProvinceId, long FoundedHours, long FestivalUntilHours, List<TrainingSave> Training);

public sealed record TrainingSave(
    BattalionType? Battalion, string? TemplateName, List<BattalionType> TemplateBattalions, int HeadquartersLevel, int DaysLeft, int TotalDays);

public sealed record UnitSave(
    int Id, int OwnerId, UnitType Type, int ProvinceId, int Citizens, int Number, int HeadquartersLevel,
    List<BattalionSave> Battalions, int? CommanderId, int? AttackingProvinceId, List<int> Path, double HoursToNext, double StepHours);

public sealed record BattalionSave(BattalionType Type, double Strength, double Organisation);

public sealed record MigrationSave(int Id, int OwnerId, int From, int To, int People, long DepartHours, long ArriveHours, bool Forced, double Mood);

public sealed record BattleSave(int ProvinceId, int AttackerId, int DefenderId, long StartHours, List<int> Attackers);

public sealed record WarSave(int A, int B, long StartHours);

public sealed record UnitNumberSave(int PlayerId, int Level, int Number);

/// <summary>A computer rival's plans: where each unit is heading, which regiments claim land, and its army template.</summary>
public sealed record AiSave(int PlayerId, Dictionary<int, int> Targets, List<int> Claimers, List<int> KnownRegiments, int? ArmyTemplateId);
