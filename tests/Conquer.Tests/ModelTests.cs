using Conquer.Game.Buildings;
using Conquer.Game.Economy;
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
        foreach (var model in Battalions.All.SelectMany(t => t.Models())) Assert.True(Exists(Models.Of(model)), $"{model.Name}: {Models.Of(model)}");
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
        foreach (var model in Battalions.All.SelectMany(t => t.Models()))
            Assert.True(File.Exists(Path.Combine(Sprites, "Units", Models.Of(model) + "-team.png")), Models.Of(model));
    }

    [Fact]
    public void BuildingIconsAreNamedAfterTheirBuilding()
    {
        Assert.Equal("granja", BuildingIcon.Slug(BuildingType.Farm));
        Assert.Equal("central-electrica", BuildingIcon.Slug(BuildingType.PowerPlant));
        Assert.Equal("fabrica", BuildingIcon.Slug(BuildingType.Factory));
        var slugs = Buildings.All.Select(BuildingIcon.Slug).ToList();
        Assert.Equal(slugs.Count, slugs.Distinct().Count());

        // Every picture in the folder belongs to a building or is a city's, so none is left unused by a typo.
        var folder = Path.Combine(Path.GetDirectoryName(Sprites)!, "BuildingIcons");
        foreach (var file in Directory.GetFiles(folder).Where(f => f.EndsWith(".png") || f.EndsWith(".jpg")))
            Assert.Contains(Path.GetFileNameWithoutExtension(file), slugs.Concat(Models.CityIcons));
    }

    [Fact]
    public void ACitysIconGrowsWithItAndModernisesInTheModernEra()
    {
        Assert.Equal("cabana", Models.CityIcon(500, capital: false, modern: false));
        Assert.Equal("pueblo", Models.CityIcon(5000, capital: false, modern: false));
        Assert.Equal("capital", Models.CityIcon(20_000, capital: false, modern: false));
        Assert.Equal("capital", Models.CityIcon(500, capital: true, modern: false));
        Assert.Equal("pueblo", Models.CityIcon(5000, capital: false, modern: true));
        Assert.Equal("ciudad-moderna", Models.CityIcon(20_000, capital: false, modern: true));
        Assert.Equal("ciudad-moderna", Models.CityIcon(500, capital: true, modern: true));
        Assert.All([500.0, 5000, 20_000], population => Assert.Contains(Models.CityIcon(population, false, false), Models.CityIcons));
    }

    [Fact]
    public void ResourceIconsAreNamedAfterTheirResource()
    {
        Assert.Equal("petroleo", ResourceIcon.Slug(ResourceType.Oil));
        Assert.Equal("carbon", ResourceIcon.Slug(ResourceType.Coal));
        var slugs = Resources.All.Select(ResourceIcon.Slug).ToList();
        Assert.Equal(slugs.Count, slugs.Distinct().Count());

        var folder = Path.Combine(Path.GetDirectoryName(Sprites)!, "ResourceIcons");
        foreach (var file in Directory.GetFiles(folder).Where(f => f.EndsWith(".png") || f.EndsWith(".jpg")))
            Assert.Contains(Path.GetFileNameWithoutExtension(file), slugs.Append(ScienceIcon.Name));
    }
}
