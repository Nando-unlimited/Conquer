using Conquer.Game.Buildings;
using Conquer.Game.Military;
using Conquer.Presentation;

namespace Conquer.Tests;

/// <summary>Every model the map asks for has its picture in the client's assets.</summary>
public class ModelTests
{
    private static readonly string Sprites = FindSprites();

    private static string FindSprites()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Conquer.sln"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "src", "Conquer.Client", "Assets", "Sprites");
    }

    private static bool Exists(string model) =>
        File.Exists(Path.Combine(Sprites, model + ".png")) || File.Exists(Path.Combine(Sprites, "Units", model + ".png"));

    [Fact]
    public void EveryBattalionAndShipHasAModel()
    {
        foreach (var type in Battalions.All) Assert.True(Exists(Models.Of(type)), $"{type}: {Models.Of(type)}");
        Assert.True(Exists("unit-settlers"));
    }

    [Fact]
    public void EveryBuildingModelAndCityExists()
    {
        foreach (var type in Enum.GetValues<BuildingType>())
            if (Models.Of(type) is { } model) Assert.True(Exists(model), $"{type}: {model}");
        Assert.True(Exists(Models.City(true)));
        Assert.True(Exists(Models.City(false)));
    }

    [Fact]
    public void UnitModelsHaveTheirTeamColours()
    {
        foreach (var type in Battalions.All)
            Assert.True(File.Exists(Path.Combine(Sprites, "Units", Models.Of(type) + "-team.png")), Models.Of(type));
    }
}
