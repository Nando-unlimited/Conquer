using Conquer.Game.Buildings;
using Conquer.Game.Entities;
using Conquer.Game.Rules;
using Conquer.Game.Science;
using Conquer.Game.World;

namespace Conquer.Game.Simulation;

/// <summary>
/// Faiths. Each nation follows one of a handful; the people of a province keep the faith of the nation that settled it.
/// Under a ruler of another faith they are unhappy until they convert, faster when content, with a temple and with
/// Theology. Nations of one faith think better of each other.
/// </summary>
public sealed partial class GameSession
{
    public static string ReligionName(int religionId) => religionId >= 0 ? Religions.Names[religionId] : "Ninguna";

    /// <summary>What sharing a faith, or not, adds to what one nation thinks of another.</summary>
    public double FaithOpinion(int from, int about) =>
        Players[from].ReligionId == Players[about].ReligionId ? GameRules.SameFaithOpinion : GameRules.OtherFaithOpinion;

    /// <summary>Whether the people of a province follow another faith than their ruler.</summary>
    public bool HasOtherFaith(Province p) => p.IsOwned && p.ReligionId >= 0 && p.Population >= 1 && p.ReligionId != Players[p.OwnerId].ReligionId;

    /// <summary>Share of the way to the ruler's faith gained in a day: faster when happy, with a temple and with Theology.</summary>
    public double DailyConversion(Province p)
    {
        double pace = Math.Clamp(p.Mood / 50, GameRules.MinAssimilationPace, GameRules.MaxAssimilationPace);
        double help = 1 + (p.Has(BuildingType.Temple) ? GameRules.TempleConversion : 0)
                      + (p.IsOwned && Players[p.OwnerId].Techs.Contains(Tech.Theology) ? GameRules.TheologyConversion : 0);
        return pace * help / (GameRules.ConversionYears * 365);
    }

    /// <summary>Years a province still needs to convert at today's pace (0 if it shares its ruler's faith).</summary>
    public double YearsToConvert(Province p) => HasOtherFaith(p) ? (1 - p.Conversion) / DailyConversion(p) / 365 : 0;

    /// <summary>Each day the people of another faith draw closer to their ruler's, and take it once all the way there.</summary>
    private void DailyFaith(Player player)
    {
        foreach (int id in player.Provinces)
        {
            var p = Map.Provinces[id];
            if (p.Population < 1)
            {
                p.ReligionId = player.ReligionId;
                p.Conversion = 0;
                continue;
            }
            if (p.IsOccupied || !HasOtherFaith(p) || (p.Conversion += DailyConversion(p)) < 1) continue;
            string old = ReligionName(p.ReligionId);
            p.ReligionId = player.ReligionId;
            p.Conversion = 0;
            if (player.Id == HumanPlayerId) Notify(player.Id, $"{PlaceName(p)} abandona la fe de {old} y abraza la nuestra.");
        }
    }
}
