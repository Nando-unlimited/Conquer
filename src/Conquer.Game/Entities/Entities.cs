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
    /// <summary>The last day's upkeep could not be paid in full; worked out again every day.</summary>
    public bool ArmyUnpaid { get; set; }
    /// <summary>Days the food stockpile would last at the last day's consumption.</summary>
    public double FoodReserveDays { get; set; }

    /// <summary>Advances discovered so far.</summary>
    public HashSet<Tech> Techs { get; } = [];
    /// <summary>The effects of every discovered advance and adopted institution, added up.</summary>
    public Modifiers Bonuses { get; private set; } = Modifiers.None;
    /// <summary>Science points put into each advance.</summary>
    public double[] ResearchProgress { get; } = new double[Science.Techs.All.Length];
    /// <summary>How much of its science each branch gets (0 to <see cref="GameRules.MaxResearchPriority"/>), by <see cref="TechBranch"/>.</summary>
    public int[] ResearchPriorities { get; } = [.. Science.Techs.Branches.Select(_ => GameRules.DefaultResearchPriority)];

    /// <summary>The share of the nation's science a branch gets; all alike when every priority is zero.</summary>
    public double ScienceShare(TechBranch branch)
    {
        int total = ResearchPriorities.Sum();
        return total > 0 ? (double)ResearchPriorities[(int)branch] / total : 1.0 / ResearchPriorities.Length;
    }

    /// <summary>The advance each branch is researching, by <see cref="TechBranch"/>; null while none is chosen.</summary>
    public Tech?[] Researching { get; } = new Tech?[Science.Techs.Branches.Length];
    /// <summary>Resources the nation can see and mine: those known from the start plus those its advances reveal.</summary>
    public HashSet<ResourceType> KnownResources { get; } = [.. Resources.KnownFromStart];

    public bool Knows(ResourceType resource) => KnownResources.Contains(resource);

    /// <summary>Science no branch could take (nothing chosen to research); it goes into research again the next day.</summary>
    public double SpareScience { get; set; }
    public double LastDayScience { get; set; }

    /// <summary>Its regiment designs; every nation starts with one of two warrior battalions.</summary>
    public List<RegimentTemplate> Templates { get; } = [];

    /// <summary>Officers recruited but not leading anything, ready to be put at the head of a unit.</summary>
    public List<Officer> OfficerReserve { get; } = [];

    /// <summary>Institutions the nation has adopted.</summary>
    public HashSet<Institution> Institutions { get; } = [];

    public void Learn(Tech tech)
    {
        if (!Techs.Add(tech)) return;
        Bonuses += tech.Info().Effects;
        KnownResources.UnionWith(tech.Info().Reveals);
    }

    public void Adopt(Institution institution)
    {
        if (Institutions.Add(institution)) Bonuses += institution.Info().Bonus;
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

    /// <summary>Battalions and HQs being trained, in order; each counts down on its own.</summary>
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
/// Something on the map that walks or sails: a band of settlers, a regiment of 1 to 6 battalions (the
/// smallest unit that fights on land), the headquarters of a brigade, division, corps, army or army group,
/// or a fleet of ships.
/// </summary>
public sealed class Unit
{
    private readonly int _citizens;

    public int Id { get; }
    public Player Owner { get; }
    public int OwnerId => Owner.Id;
    public UnitType Type { get; }
    public int ProvinceId { get; set; }
    /// <summary>Its number among its nation's units of the same level (0 for settlers).</summary>
    public int Number { get; }
    /// <summary>A regiment's battalions; empty for other units.</summary>
    public List<Battalion> Battalions { get; } = [];
    /// <summary>For an HQ, 1 (brigade) to 5 (army group); 0 for regiments.</summary>
    public int HeadquartersLevel { get; }
    /// <summary>The HQ this unit reports to, one level up; null while unattached.</summary>
    public int? CommanderId { get; set; }
    /// <summary>The province a regiment is attacking; it stays where it is until it wins.</summary>
    public int? AttackingProvinceId { get; set; }
    /// <summary>The fleet carrying this unit over the sea; null on land. It goes wherever the fleet goes.</summary>
    public int? CarrierId { get; set; }
    /// <summary>The officer at the head of a combat unit, or an HQ's general; null while it has none.</summary>
    public Officer? Officer { get; set; }
    /// <summary>A name the player gave it; null keeps the one that goes with its number and size.</summary>
    public string? CustomName { get; set; }

    /// <summary>Provinces still to enter, in order; empty when the unit is idle.</summary>
    public List<int> Path { get; } = [];
    /// <summary>Hours left to reach Path[0], and the full duration of that step.</summary>
    public double HoursToNext { get; set; }
    public double StepHours { get; set; }

    public Unit(int id, Player owner, UnitType type, int provinceId, int citizens, int number = 0, int headquartersLevel = 0)
    {
        Id = id;
        Owner = owner;
        Type = type;
        ProvinceId = provinceId;
        _citizens = citizens;
        Number = number;
        HeadquartersLevel = headquartersLevel;
    }

    /// <summary>
    /// The name the player gave it, or else "3.er Regimiento", "3.ª Brigada" or "3.ª División" by its size, "II Cuerpo"
    /// for an HQ, "2.ª Flota"; settlers are just "Colonos".
    /// </summary>
    public string Name => CustomName ?? AutomaticName;

    /// <summary>The name that goes with its number and size.</summary>
    public string AutomaticName => Type switch
    {
        UnitType.Settlers => "Colonos",
        UnitType.Fleet => Formations.FleetName(Number),
        UnitType.Headquarters => Formations.HeadquartersName(HeadquartersLevel, Number),
        _ => Formations.CombatUnitName(Number, Battalions.Count),
    };

    /// <summary>Citizens in the unit: its settlers or staff, or the men left in a regiment's battalions or a fleet's crews.</summary>
    public int Citizens => Type is UnitType.Regiment or UnitType.Fleet ? (int)Math.Round(Battalions.Sum(b => b.Strength)) : _citizens;
    public bool IsMilitary => Type == UnitType.Regiment;
    public bool IsHeadquarters => Type == UnitType.Headquarters;
    public bool IsFleet => Type == UnitType.Fleet;
    /// <summary>Carried by a fleet rather than standing on land.</summary>
    public bool IsAboard => CarrierId.HasValue;
    /// <summary>Men a fleet can carry: the sum of its ships' holds.</summary>
    public int Capacity => IsFleet ? Battalions.Sum(b => b.Info.Capacity) : 0;
    public bool CanFoundCity => Type == UnitType.Settlers;
    /// <summary>Combat units and HQs are led by an officer; settlers and fleets are not.</summary>
    public bool HasOfficer => Type is UnitType.Regiment or UnitType.Headquarters;
    /// <summary>The rank that goes with its size: colonel to major general for combat units, lieutenant general up for HQs.</summary>
    public OfficerRank RequiredRank => Type == UnitType.Headquarters
        ? OfficerRank.MajorGeneral + HeadquartersLevel
        : Battalions.Count <= 3 ? OfficerRank.Colonel : Battalions.Count <= 6 ? OfficerRank.Brigadier : OfficerRank.MajorGeneral;
    /// <summary>0 for regiments, 1-5 for HQs, -1 for units outside the chain of command.</summary>
    public int CommandLevel => Type switch
    {
        UnitType.Regiment => CommandLevels.Combat,
        UnitType.Headquarters => HeadquartersLevel,
        _ => -1,
    };
    /// <summary>
    /// Speed as a multiple of a walking citizen's (a ship's, of the sailing speed): the slowest battalion or ship sets
    /// the pace, and the officer at its head may quicken or slow it.
    /// </summary>
    public double Speed => (1 + (Officer?.SpeedBonus ?? 0)) * Type switch
    {
        UnitType.Regiment or UnitType.Fleet => Battalions.Count == 0 ? 1 : Battalions.Min(b => b.Info.Speed),
        UnitType.Headquarters => MilitaryRules.HeadquartersSpeed,
        _ => 1,
    };
    /// <summary>A regiment made only of aircraft, which may fly over the sea.</summary>
    public bool Flies => Type == UnitType.Regiment && Battalions.Count > 0 && Battalions.All(b => b.Info.Flies);
    /// <summary>A regiment's organisation as a share of its maximum (0..1).</summary>
    public double OrganisationShare =>
        Battalions.Count == 0 ? 0 : Battalions.Sum(b => b.Organisation) / Battalions.Sum(b => b.Info.MaxOrganisation);
    /// <summary>A regiment's strength as a share of its full complement (0..1).</summary>
    public double StrengthShare =>
        Battalions.Count == 0 ? 0 : Battalions.Sum(b => b.Strength) / Battalions.Sum(b => b.Info.Men);
    public string Symbol => Type switch
    {
        UnitType.Settlers => "C",
        UnitType.Headquarters => CommandLevels.Info(HeadquartersLevel).Symbol,
        _ => Battalions.Count == 0 ? "?" : Battalions.GroupBy(b => b.Type).OrderByDescending(g => g.Count()).First().Key.Info().Symbol,
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
