using Conquer.Game.Entities;
using Conquer.Game.Rules;
using Conquer.Game.World;

namespace Conquer.Game.Simulation;

/// <summary>The four seasons, which come at opposite times in each hemisphere.</summary>
public enum Season
{
    Spring,
    Summer,
    Autumn,
    Winter,
}

/// <summary>
/// Seasons and attrition. Away from the tropics, winter snow slows armies and wears them down unless they shelter in
/// one of their nation's cities; the thaw and the autumn rains turn the roads to mud. Deserts wear armies down all year.
/// </summary>
public sealed partial class GameSession
{
    public static readonly string[] SeasonNames = ["Primavera", "Verano", "Otoño", "Invierno"];

    /// <summary>The season in a province: December to February is winter in the north and summer in the south.</summary>
    public Season SeasonOf(Province p)
    {
        int month = Date.MonthAndDay.Month;
        if (p.Latitude < 0) month = (month + 5) % 12 + 1;
        return month switch
        {
            12 or 1 or 2 => Season.Winter,
            >= 3 and <= 5 => Season.Spring,
            >= 6 and <= 8 => Season.Summer,
            _ => Season.Autumn,
        };
    }

    /// <summary>
    /// How hard winter is there, from 0 (no winter, or not winter now) to 1 (deep snow): nothing in the tropics,
    /// growing from <see cref="GameRules.MildWinterLatitude"/> to <see cref="GameRules.HarshWinterLatitude"/>.
    /// </summary>
    public double WinterSeverity(Province p) =>
        SeasonOf(p) == Season.Winter ? ColdOf(p) : 0;

    /// <summary>How deep the mud is in spring and autumn, from 0 to 1, where winters are hard enough to thaw.</summary>
    public double MudSeverity(Province p) =>
        SeasonOf(p) is Season.Spring or Season.Autumn ? ColdOf(p) * GameRules.MudShare : 0;

    private static double ColdOf(Province p) =>
        p.IsWater ? 0 : Math.Clamp((Math.Abs(p.Latitude) - GameRules.MildWinterLatitude) / (GameRules.HarshWinterLatitude - GameRules.MildWinterLatitude), 0, 1);

    /// <summary>How many times longer it takes to march into a province now: snow and mud slow armies down.</summary>
    public double SeasonSlowdown(Province p) =>
        1 + GameRules.WinterSlowdown * WinterSeverity(p) + GameRules.WinterSlowdown * MudSeverity(p);

    /// <summary>
    /// Share of its men a regiment loses each day where it stands: to cold in winter, unless it shelters in a city its
    /// nation holds, and to heat and thirst in the desert.
    /// </summary>
    public double DailyAttrition(Unit unit)
    {
        if (!unit.IsMilitary || unit.IsAboard) return 0;
        var p = Map.Provinces[unit.ProvinceId];
        bool sheltered = p.CityId.HasValue && p.ControllerId == unit.OwnerId;
        double cold = sheltered ? 0 : GameRules.WinterAttrition * WinterSeverity(p);
        double heat = p.Biome == Biome.Desert ? GameRules.DesertAttrition : 0;
        return cold + heat + HeightAttrition(p);
    }

    /// <summary>What the thin air, the ice and the cold of the heights take each day, in any season.</summary>
    public static double HeightAttrition(Province p) => p.Biome switch
    {
        Biome.Peaks or Biome.PolarIce => GameRules.PeakAttrition,
        Biome.HighMountains => GameRules.HighMountainAttrition,
        _ => 0,
    };

    /// <summary>What the season and the land do to armies in a province, for the player: null when nothing.</summary>
    public string? SeasonEffect(Province p)
    {
        var lines = new List<string>();
        double winter = WinterSeverity(p), mud = MudSeverity(p);
        if (winter > 0)
            lines.Add($"Invierno {(winter >= 0.66 ? "crudo" : winter >= 0.33 ? "frío" : "suave")}: las tropas marchan un {GameRules.WinterSlowdown * winter:P0} más despacio " +
                      $"y pierden un {GameRules.WinterAttrition * winter:P1} de sus hombres al día, salvo en una ciudad suya.");
        else if (mud > 0)
            lines.Add($"Barro: las tropas marchan un {GameRules.WinterSlowdown * mud:P0} más despacio.");
        if (p.Biome == Biome.Desert)
            lines.Add($"Desierto: las tropas pierden un {GameRules.DesertAttrition:P1} de sus hombres al día por el calor y la sed.");
        if (HeightAttrition(p) > 0)
            lines.Add($"{p.Info.Name}: las tropas pierden un {HeightAttrition(p):P1} de sus hombres al día por el frío y el aire enrarecido, en cualquier estación.");
        return lines.Count == 0 ? null : string.Join("\n", lines);
    }
}
