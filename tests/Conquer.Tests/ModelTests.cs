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

    [Fact]
    public void BuildingIconsAreNamedAfterTheirBuilding()
    {
        Assert.Equal("granja", BuildingIcon.Slug(BuildingType.Farm));
        Assert.Equal("central-electrica", BuildingIcon.Slug(BuildingType.PowerPlant));
        Assert.Equal("fabrica", BuildingIcon.Slug(BuildingType.Factory));
        var slugs = Buildings.All.Select(BuildingIcon.Slug).ToList();
        Assert.Equal(slugs.Count, slugs.Distinct().Count());

        // Every picture in the folder belongs to a building, so none is left unused by a typo.
        var folder = Path.Combine(Path.GetDirectoryName(Sprites)!, "BuildingIcons");
        foreach (var file in Directory.GetFiles(folder).Where(f => f.EndsWith(".png") || f.EndsWith(".jpg")))
            Assert.Contains(Path.GetFileNameWithoutExtension(file), slugs);
    }
}
