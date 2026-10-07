using Conquer.Game.Military;
using Conquer.Game.Simulation;
using Conquer.Game.World;
using Conquer.Presentation;

namespace Conquer.Tests;

public static class Contacts
{
    /// <summary>Has every nation meet every other, for tests about something else.</summary>
    public static void MeetEveryone(this GameSession s)
    {
        foreach (var p in s.Players) p.Contacts.UnionWith(s.Players.Where(o => o.Id != p.Id).Select(o => o.Id));
    }
}

/// <summary>Whom a nation has met: the nation screen lists only those in Diplomacia, Comercio and Estadísticas.</summary>
[Collection("World")]
public class ContactTests(WorldFixture world)
{
    private readonly WorldMap _map = world.Map;

    [Fact]
    public void NationsMeetWhenOneSeesTheOthersLandAndStayMet()
    {
        var s = GameSession.Create(_map, 2, seed: 7, computerRivals: false);
        s.UpdateContacts();
        Assert.False(s.HasContact(0, 1));
        Assert.True(s.HasContact(0, 0));

        // The rival founds its city, and a scout of the player's walks into it.
        var settlers = s.Units.First(u => u.OwnerId == 1 && u.CanFoundCity);
        var theirs = _map.Provinces[settlers.ProvinceId];
        Assert.True(s.FoundCity(1, settlers.Id).Ok);
        var scout = s.AddRegiment(0, theirs.Id, BattalionType.Scouts);
        s.UpdateContacts();
        Assert.True(s.HasContact(0, 1));
        Assert.True(s.HasContact(1, 0));

        // Once met, they stay met.
        s.Disband(0, scout.Id);
        s.UpdateContacts();
        Assert.True(s.HasContact(0, 1));
    }

    [Fact]
    public void TheNationScreenListsOnlyTheNationsMet()
    {
        var game = new GameController(GameSession.Create(_map, 2, seed: 7, computerRivals: false));
        game.Session.UpdateContacts();
        game.Nation.Visible = true;
        game.Nation.Tab = NationTab.Diplomacy;
        var table = ((TablePage)game.Nation.Page()).Table;
        Assert.Empty(table.Rows);
        Assert.NotNull(table.Empty);

        game.Session.MeetEveryone();
        Assert.Single(((TablePage)game.Nation.Page()).Table.Rows);
    }

    [Fact]
    public void ContactsAreSaved()
    {
        var s = GameSession.Create(_map, 2, seed: 7, computerRivals: false);
        s.MeetEveryone();
        var loaded = GameSession.Load(_map, s.ToSave("test"));
        Assert.True(loaded.HasContact(0, 1));
    }
}
