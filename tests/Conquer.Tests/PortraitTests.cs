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
        var teresa = new Officer(3, "Teresa Cortés", [OfficerTrait.Offensive], 3, rank: OfficerRank.LieutenantGeneral);
        Assert.True(teresa.IsFemale);
        var face = Portrait.Of(teresa, Era.Industrial, 0xFF0000AA, naval: true);
        Assert.True(face.IsFemale);
        Assert.Equal(FacialHair.None, face.Beard);
        Assert.Equal((int)OfficerRank.LieutenantGeneral, face.Rank);
        Assert.Equal(3, face.Skill);
        Assert.True(face.Naval);
        Assert.False(new Officer(4, "Sancho Haro", [OfficerTrait.Offensive], 1).IsFemale);
    }

    [Fact]
    public void HairGreysWithSkill()
    {
        var young = Portrait.Of(new Officer(1, "Pedro Castro", [OfficerTrait.Offensive], 1), Era.Modern, 0xFF000000);
        var veteran = young with { Skill = 5 };
        Assert.True(veteran.Greying > young.Greying);
    }
}
