using Conquer.Game.Economy;
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
    public TechEffects Bonuses { get; private set; } = new();
    /// <summary>What the nation is researching; null while idle.</summary>
    public Tech? Researching { get; set; }
    /// <summary>Science points put into each advance; kept when research switches to another one.</summary>
    public double[] ResearchProgress { get; } = new double[Science.Techs.All.Length];
    /// <summary>Science earned while nothing was being researched; it goes into the next advance chosen.</summary>
    public double SpareScience { get; set; }
    public double LastDayScience { get; set; }

    public void Learn(Tech tech)
    {
        if (Techs.Add(tech)) Bonuses += tech.Info().Effects;
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

    public City(int id, string name, int ownerId, int provinceId, long foundedHours)
    {
        Id = id;
        Name = name;
        OwnerId = ownerId;
        ProvinceId = provinceId;
        FoundedHours = foundedHours;
    }
}

public sealed class Unit
{
    public int Id { get; }
    public int OwnerId { get; }
    public UnitType Type { get; }
    public int ProvinceId { get; set; }
    public int Citizens { get; set; }

    /// <summary>Provinces still to enter, in order; empty when the unit is idle.</summary>
    public List<int> Path { get; } = [];
    /// <summary>Hours left to reach Path[0], and the full duration of that step.</summary>
    public double HoursToNext { get; set; }
    public double StepHours { get; set; }

    public Unit(int id, int ownerId, UnitType type, int provinceId, int citizens)
    {
        Id = id;
        OwnerId = ownerId;
        Type = type;
        ProvinceId = provinceId;
        Citizens = citizens;
    }

    public UnitTypeInfo Info => Type.Info();
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
