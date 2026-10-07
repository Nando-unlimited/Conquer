using Conquer.Game.Economy;
using Conquer.Game.Rules;
using Conquer.Game.World;

namespace Conquer.Tests;

public class DepositCapTests
{
    private static int DepositCount(Province p) => Resources.Deposits.Count(r => p.Deposits[(int)r] > 0);

    [Fact]
    public void NewMapsHoldAtMostFourDepositsPerProvinceAndOldOnesKeepTheirs()
    {
        // The easiest difficulty has the most deposits, so the cap has the most to do.
        var settings = WorldSettings.New(MapKind.Random, 42, Difficulty.VeryEasy, MapSize.Small);
        Assert.Equal(3, settings.Generator);
        var capped = WorldGenerator.Generate(settings);
        var old = WorldGenerator.Generate(settings with { Generator = 2 });

        Assert.Equal(4, GameRules.MaxDepositsPerProvince);
        Assert.All(capped.Provinces, p => Assert.InRange(DepositCount(p), 0, GameRules.MaxDepositsPerProvince));
        Assert.Contains(old.Provinces, p => DepositCount(p) > GameRules.MaxDepositsPerProvince);

        // The cap only takes deposits away: what stays is what the old generator placed, and provinces under it are untouched.
        foreach (var p in capped.Provinces)
        {
            var before = old.Provinces[p.Id];
            foreach (var r in Resources.Deposits.Where(r => p.Deposits[(int)r] > 0))
            {
                Assert.Equal(before.Deposits[(int)r], p.Deposits[(int)r]);
                Assert.Equal(before.DepositSizes[(int)r], p.DepositSizes[(int)r]);
            }
            if (DepositCount(before) <= GameRules.MaxDepositsPerProvince) Assert.Equal(DepositCount(before), DepositCount(p));
            else Assert.Equal(GameRules.MaxDepositsPerProvince, DepositCount(p));
        }
    }
}
