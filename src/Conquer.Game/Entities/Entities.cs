using Conquer.Game.Economy;
using Conquer.Game.Rules;

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

    public Migration(int id, int ownerId, int from, int to, int people, long departHours, long arriveHours, bool forced)
    {
        Id = id;
        OwnerId = ownerId;
        FromProvinceId = from;
        ToProvinceId = to;
        People = people;
        DepartHours = departHours;
        ArriveHours = arriveHours;
        Forced = forced;
    }

    public double Progress(long nowHours) =>
        ArriveHours <= DepartHours ? 1 : Math.Clamp((nowHours - DepartHours) / (double)(ArriveHours - DepartHours), 0, 1);
}

public readonly record struct Notification(long Hours, int PlayerId, string Text);
