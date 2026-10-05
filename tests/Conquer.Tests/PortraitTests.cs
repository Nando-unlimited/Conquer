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
            ["teniente-antigua-mujer-ejercito", "division-antigua-mujer-ejercito", "general-antigua-mujer-ejercito",
             "brigadier-antigua-mujer-ejercito", "mariscal-antigua-mujer-ejercito", "coronel-antigua-mujer-ejercito"],
            Portrait.Of(teniente, Era.Ancient, 0).PhotoGroups);
    }

    [Fact]
    public void ASailorLooksForASailorsPortraitFirstThenTheArmys()
    {
        var colonel = new Officer(6, "Sancho Haro", [OfficerTrait.Offensive], 1, branch: OfficerBranch.Navy);
        var groups = Portrait.Of(colonel, Era.Renaissance, 0).PhotoGroups.ToList();
        Assert.Equal("coronel-renacimiento-hombre-marina", groups[0]);
        Assert.Equal("mariscal-renacimiento-hombre-marina", groups[5]);
        Assert.Equal("coronel-renacimiento-hombre-ejercito", groups[6]);
        Assert.Equal(12, groups.Count);
    }

    [Fact]
    public void AnAirmanLooksForAnAirmansPortraitFirst()
    {
        var airman = new Officer(9, "Diego Lara", [OfficerTrait.Offensive], 1, branch: OfficerBranch.Air);
        var face = Portrait.Of(airman, Era.Modern, 0);
        Assert.True(face.Air);
        Assert.Equal("coronel-moderna-hombre-aviacion", face.PhotoGroups.First());
    }

    [Fact]
    public void ACavalryOfficerLooksForACavalrymansPortraitFirstThenTheArmys()
    {
        var colonel = new Officer(10, "Sancho Haro", [OfficerTrait.Offensive], 1);
        var groups = Portrait.Of(colonel, Era.Industrial, 0, cavalry: true).PhotoGroups.ToList();
        Assert.Equal("coronel-industrial-hombre-caballeria", groups[0]);
        Assert.Equal("coronel-industrial-hombre-ejercito", groups[6]);
        // Only the army has cavalry.
        var sailor = new Officer(11, "Diego Lara", [OfficerTrait.Offensive], 1, branch: OfficerBranch.Navy);
        Assert.DoesNotContain(Portrait.Of(sailor, Era.Industrial, 0, cavalry: true).PhotoGroups, g => g.EndsWith(Portrait.CavalrySlug));
    }

    /// <summary>The painted portraits in the assets are named rank-era-sex-arm-number, so every one can be found.</summary>
    [Fact]
    public void ThePaintedPortraitsAreWellNamed()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Conquer.sln"))) dir = dir.Parent;
        var folder = Path.Combine(dir!.FullName, "src", "Conquer.Client", "Assets", "Portraits");
        var groups = (from rank in Portrait.RankSlugs
                      from era in Enum.GetValues<Era>()
                      from female in new[] { false, true }
                      from arm in Enum.GetValues<OfficerBranch>().Select(Portrait.BranchSlug).Append(Portrait.CavalrySlug)
                      select $"{rank}-{Portrait.EraSlug(era)}-{Portrait.SexSlug(female)}-{arm}").ToHashSet();
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
