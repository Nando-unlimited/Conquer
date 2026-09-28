using Conquer.Game.Economy;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Science;

namespace Conquer.Game.Entities;

public sealed class Player
{
    public int Id { get; }
    public string Name { get; }
    /// <summary>0xAARRGGBB.</summary>
    public uint Color { get; }
    public bool IsHuman { get; }
    public Stockpile Stockpile { get; } = new();
    public HashSet<int> Provinces { get; } = [];
    public int? CapitalCityId { get; set; }
    /// <summary>Net change of each resource over the last day, for display.</summary>
    public double[] LastDayNet { get; } = new double[Resources.All.Length];
    public bool IsStarving { get; set; }
    /// <summary>Days the food stockpile would last at the last day's consumption.</summary>
    public double FoodReserveDays { get; set; }

    /// <summary>Advances discovered so far.</summary>
    public HashSet<Tech> Techs { get; } = [];
    /// <summary>The effects of every discovered advance, added up.</summary>
    public Modifiers Bonuses { get; private set; } = Modifiers.None;
    /// <summary>What the nation is researching; null while idle.</summary>
    public Tech? Researching { get; set; }
    /// <summary>Science points put into each advance; kept when research switches to another one.</summary>
    public double[] ResearchProgress { get; } = new double[Science.Techs.All.Length];
    /// <summary>Resources the nation can see and mine: those known from the start plus those its advances reveal.</summary>
    public HashSet<ResourceType> KnownResources { get; } = [.. Resources.KnownFromStart];

    public bool Knows(ResourceType resource) => KnownResources.Contains(resource);

    /// <summary>Science earned while nothing was being researched; it goes into the next advance chosen.</summary>
    public double SpareScience { get; set; }
    public double LastDayScience { get; set; }

    public void Learn(Tech tech)
    {
        if (!Techs.Add(tech)) return;
        Bonuses += tech.Info().Effects;
        KnownResources.UnionWith(tech.Info().Reveals);
    }

    public Player(int id, string name, uint color, bool isHuman)
    {
        Id = id;
        Name = name;
        Color = color;
        IsHuman = isHuman;
    }
}

public sealed class City
{
    public int Id { get; }
    public string Name { get; }
    public int OwnerId { get; set; }
    public int ProvinceId { get; }
    public long FoundedHours { get; }
    /// <summary>The city's festival lasts until this hour; in the past when there is none.</summary>
    public long FestivalUntilHours { get; set; }

    public bool HasFestival(long nowHours) => FestivalUntilHours > nowHours;

    /// <summary>Brigades and HQs being trained, in order; each counts down on its own.</summary>
    public List<TrainingOrder> Training { get; } = [];

    public City(int id, string name, int ownerId, int provinceId, long foundedHours)
    {
        Id = id;
        Name = name;
        OwnerId = ownerId;
        ProvinceId = provinceId;
        FoundedHours = foundedHours;
    }
}

/// <summary>
/// Something on the map that walks: a band of settlers, a division of 1 to 4 brigades, or the
/// headquarters of a corps, army, army group or theatre.
/// </summary>
public sealed class Unit
{
    private readonly int _citizens;

    public int Id { get; }
    public int OwnerId { get; }
    public UnitType Type { get; }
    public int ProvinceId { get; set; }
    /// <summary>"1.ª División", "II Cuerpo"… (settlers are just "Colonos").</summary>
    public string Name { get; }
    /// <summary>A division's brigades; empty for other units.</summary>
    public List<Brigade> Brigades { get; } = [];
    /// <summary>For an HQ, 1 (corps) to 4 (theatre); 0 for divisions.</summary>
    public int HeadquartersLevel { get; }
    /// <summary>The HQ this unit reports to, one level up; null while unattached.</summary>
    public int? CommanderId { get; set; }
    /// <summary>The province a division is attacking; it stays where it is until it wins.</summary>
    public int? AttackingProvinceId { get; set; }

    /// <summary>Provinces still to enter, in order; empty when the unit is idle.</summary>
    public List<int> Path { get; } = [];
    /// <summary>Hours left to reach Path[0], and the full duration of that step.</summary>
    public double HoursToNext { get; set; }
    public double StepHours { get; set; }

    public Unit(int id, int ownerId, UnitType type, int provinceId, int citizens, string name, int headquartersLevel = 0)
    {
        Id = id;
        OwnerId = ownerId;
        Type = type;
        ProvinceId = provinceId;
        _citizens = citizens;
        Name = name;
        HeadquartersLevel = headquartersLevel;
    }

    /// <summary>Citizens in the unit: its settlers or staff, or the men left in a division's brigades.</summary>
    public int Citizens => Type == UnitType.Division ? (int)Math.Round(Brigades.Sum(b => b.Strength)) : _citizens;
    public bool IsMilitary => Type == UnitType.Division;
    public bool IsHeadquarters => Type == UnitType.Headquarters;
    public bool CanFoundCity => Type == UnitType.Settlers;
    /// <summary>0 for divisions, 1-4 for HQs, -1 for units outside the chain of command.</summary>
    public int CommandLevel => Type switch
    {
        UnitType.Division => CommandLevels.Division,
        UnitType.Headquarters => HeadquartersLevel,
        _ => -1,
    };
    /// <summary>Marching speed as a multiple of a walking citizen's: the slowest brigade sets a division's pace.</summary>
    public double Speed => Type switch
    {
        UnitType.Division => Brigades.Count == 0 ? 1 : Brigades.Min(b => b.Info.Speed),
        UnitType.Headquarters => MilitaryRules.HeadquartersSpeed,
        _ => 1,
    };
    /// <summary>A division's organisation as a share of its maximum (0..1).</summary>
    public double OrganisationShare =>
        Brigades.Count == 0 ? 0 : Brigades.Sum(b => b.Organisation) / Brigades.Sum(b => b.Info.MaxOrganisation);
    /// <summary>A division's strength as a share of its full complement (0..1).</summary>
    public double StrengthShare =>
        Brigades.Count == 0 ? 0 : Brigades.Sum(b => b.Strength) / Brigades.Sum(b => b.Info.Men);
    public string Symbol => Type switch
    {
        UnitType.Settlers => "C",
        UnitType.Headquarters => CommandLevels.Info(HeadquartersLevel).Symbol,
        _ => Brigades.Count == 0 ? "?" : Brigades.GroupBy(b => b.Type).OrderByDescending(g => g.Count()).First().Key.Info().Symbol,
    };

    public bool IsMoving => Path.Count > 0;
    public int? Destination => Path.Count > 0 ? Path[^1] : null;
    /// <summary>0..1 progress along the current step.</summary>
    public double StepProgress => StepHours <= 0 ? 0 : 1 - HoursToNext / StepHours;
}

/// <summary>A group of citizens walking from one province to another.</summary>
public sealed class Migration
{
    public int Id { get; }
    public int OwnerId { get; }
    public int FromProvinceId { get; }
    public int ToProvinceId { get; }
    public int People { get; }
    public long DepartHours { get; }
    public long ArriveHours { get; }
    /// <summary>Paid for by the player rather than moving on its own.</summary>
    public bool Forced { get; }
    /// <summary>Mood the migrants bring; it blends with the residents' mood when they arrive.</summary>
    public double Mood { get; }

    public Migration(int id, int ownerId, int from, int to, int people, long departHours, long arriveHours, bool forced, double mood)
    {
        Id = id;
        OwnerId = ownerId;
        FromProvinceId = from;
        ToProvinceId = to;
        People = people;
        DepartHours = departHours;
        ArriveHours = arriveHours;
        Forced = forced;
        Mood = mood;
    }

    public double Progress(long nowHours) =>
        ArriveHours <= DepartHours ? 1 : Math.Clamp((nowHours - DepartHours) / (double)(ArriveHours - DepartHours), 0, 1);
}

public readonly record struct Notification(long Hours, int PlayerId, string Text);
