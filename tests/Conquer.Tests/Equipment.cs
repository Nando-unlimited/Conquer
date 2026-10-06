using Conquer.Game.Entities;
using Conquer.Game.Military;

namespace Conquer.Tests;

public static class Equipment
{
    /// <summary>Fills the nation's stockpile with plenty of every model's equipment, for tests about something else.</summary>
    public static void Arm(this Player player)
    {
        foreach (var model in Battalions.All.SelectMany(t => t.Models()).Where(m => m.NeedsEquipment))
            player.Equipment[model.SupplyKey] = 100_000;
    }
}
