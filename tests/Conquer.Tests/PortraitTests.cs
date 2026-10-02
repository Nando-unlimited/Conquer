using Conquer.Game.Military;
using Conquer.Game.Science;
using Conquer.Presentation;

namespace Conquer.Tests;

/// <summary>Officers' portraits: the same face every time, made up from the name, with the rank, skill and era.</summary>
public class PortraitTests
{
    [Fact]
    public void AnOfficerAlwaysHasTheSameFace()
    {
        var officer = new Officer(7, "Hernán Ulloa", [OfficerTrait.Offensive], 2);
        var a = Portrait.Of(officer, Era.Ancient, 0xFFAA0000);
        var b = Portrait.Of(new Officer(7, "Hernán Ulloa", [OfficerTrait.Offensive], 2), Era.Ancient, 0xFFAA0000);
        Assert.Equal(a, b);
        Assert.NotEqual(a, Portrait.Of(new Officer(8, "Diego Lara", [OfficerTrait.Offensive], 2), Era.Ancient, 0xFFAA0000));
    }

    [Fact]
    public void WomenHaveNoBeardAndTheRankAndSkillShow()
    {
        var teresa = new Officer(3, "Teresa Cortés", [OfficerTrait.Offensive], 3, rank: OfficerRank.LieutenantGeneral, branch: OfficerBranch.Navy);
        Assert.True(teresa.IsFemale);
        var face = Portrait.Of(teresa, Era.Industrial, 0xFF0000AA);
        Assert.True(face.IsFemale);
        Assert.Equal(FacialHair.None, face.Beard);
        Assert.Equal((int)OfficerRank.LieutenantGeneral, face.Rank);
        Assert.Equal(3, face.Skill);
        Assert.True(face.Naval);
        Assert.False(new Officer(4, "Sancho Haro", [OfficerTrait.Offensive], 1).IsFemale);
    }

    [Fact]
    public void APortraitOfTheOfficersRankComesFirstThenTheNearestRanks()
    {
        var teniente = new Officer(5, "Leonor Bazán", [OfficerTrait.Offensive], 1, rank: OfficerRank.LieutenantGeneral);
        Assert.Equal(
            ["antigua-mujer-teniente", "antigua-mujer-division", "antigua-mujer-general", "antigua-mujer-brigadier", "antigua-mujer-mariscal",
             "antigua-mujer-coronel", "antigua-mujer"],
            Portrait.Of(teniente, Era.Ancient, 0).PhotoGroups);
    }

    [Fact]
    public void ASailorLooksForASailorsPortraitFirst()
    {
        var colonel = new Officer(6, "Sancho Haro", [OfficerTrait.Offensive], 1, branch: OfficerBranch.Navy);
        var groups = Portrait.Of(colonel, Era.Renaissance, 0).PhotoGroups.ToList();
        Assert.Equal("renacimiento-marino-coronel", groups[0]);
        Assert.Equal("renacimiento-marino", groups[6]);
        Assert.Equal("renacimiento-hombre-coronel", groups[7]);
        Assert.Equal("renacimiento-hombre", groups[^1]);
    }

    [Fact]
    public void AnAirmanLooksForAnAirmansPortraitFirst()
    {
        var airman = new Officer(9, "Diego Lara", [OfficerTrait.Offensive], 1, branch: OfficerBranch.Air);
        var face = Portrait.Of(airman, Era.Modern, 0);
        Assert.True(face.Air);
        Assert.Equal("moderna-aviador-coronel", face.PhotoGroups.First());
    }

    /// <summary>The painted portraits in the assets are named era-group[-rank]-number, so every one can be found.</summary>
    [Fact]
    public void ThePaintedPortraitsAreWellNamed()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Conquer.sln"))) dir = dir.Parent;
        var folder = Path.Combine(dir!.FullName, "src", "Conquer.Client", "Assets", "Portraits");
        var bases = Enum.GetValues<Era>().SelectMany(e => new[] { "hombre", "mujer", "marino", "aviador" }.Select(g => $"{Portrait.EraSlug(e)}-{g}")).ToList();
        var groups = bases.Concat(bases.SelectMany(b => Portrait.RankSlugs.Select(r => $"{b}-{r}"))).ToHashSet();
        foreach (var file in Directory.GetFiles(folder).Where(f => f.EndsWith(".png") || f.EndsWith(".jpg")))
        {
            string name = Path.GetFileNameWithoutExtension(file);
            int dash = name.LastIndexOf('-');
            Assert.True(dash > 0 && int.TryParse(name[(dash + 1)..], out _) && groups.Contains(name[..dash]), $"Nombre de retrato no válido: {name}");
        }
    }

    [Fact]
    public void HairGreysWithSkill()
    {
        var young = Portrait.Of(new Officer(1, "Pedro Castro", [OfficerTrait.Offensive], 1), Era.Modern, 0xFF000000);
        var veteran = young with { Skill = 5 };
        Assert.True(veteran.Greying > young.Greying);
    }
}
