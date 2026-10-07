using Conquer.Game.Buildings;
using Conquer.Game.Economy;
using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Science;
using Conquer.Game.Simulation;
using Conquer.Game.World;
using Conquer.Presentation;

namespace Conquer.Tests;

/// <summary>Shipyards: the nation's queue of ships, built day by day in the slips of its ports and paid as they go.</summary>
[Collection("World")]
public class ShipyardTests(WorldFixture world)
{
    private readonly WorldMap _map = world.Map;

    /// <summary>A human capital on the coast with a port, navigation and plenty of everything.</summary>
    private (GameSession Session, Province Port) WithPort()
    {
        var port = _map.Provinces.First(p => p.IsClaimable && p.Neighbors.Length > 2 && p.Neighbors.Any(n => _map.Provinces[n].Biome == Biome.ShallowSea));
        var s = GameSession.Create(_map, 1, seed: 7, computerRivals: false);
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, port.Id, 300).Id);
        port.Population = 3000;
        s.Human.Manpower = 3000;
        s.Human.Learn(Tech.Navigation);
        foreach (var r in Resources.All) s.Human.Stockpile[r] = 5000;
        port.AddBuilding(BuildingType.Port);
        return (s, port);
    }

    private static void ToMidnight(GameSession s)
    {
        do s.Step(); while (s.Date.Hour != 0);
    }

    [Fact]
    public void ShipsAreBuiltDayByDayAndPaidAsTheyGo()
    {
        var (s, port) = WithPort();
        var trireme = BattalionType.LineShip.First();
        Assert.True(s.OrderShip(0, BattalionType.LineShip).Ok);
        var order = Assert.Single(s.Human.ShipOrders);
        Assert.Null(order.PortId);
        Assert.Equal(5000, s.Human.Stockpile[ResourceType.Iron]); // nothing paid yet

        double gold = s.Human.Stockpile[ResourceType.Gold];
        ToMidnight(s);
        Assert.Equal(port.Id, order.PortId);
        Assert.Equal(1, order.DaysDone, 6);
        // One day's share of the trireme's wood went into it.
        double wood = trireme.Cost.Items.First(i => i.Type == ResourceType.Wood).Amount / trireme.TrainingDays;
        Assert.Equal(-wood, s.Human.LastDayFlows[(int)ResourceFlow.Workshops][(int)ResourceType.Wood], 6);

        for (int d = 1; d < trireme.TrainingDays; d++) ToMidnight(s);
        Assert.Empty(s.Human.ShipOrders);
        var fleet = s.Units.Single(u => u.IsFleet);
        Assert.Equal(port.Id, fleet.ProvinceId);
        Assert.Equal("Trirreme", fleet.Ships.Single().Info.Name);
    }

    [Fact]
    public void APortHasOneSlipAndADryDockAddsAnother()
    {
        var (s, port) = WithPort();
        for (int i = 0; i < 3; i++) s.OrderShip(0, BattalionType.Transport);
        Assert.Equal(1, s.Slips(port, 0));
        ToMidnight(s);
        Assert.Equal([true, false, false], s.Human.ShipOrders.Select(o => o.PortId != null));

        port.AddBuilding(BuildingType.DryDock);
        Assert.Equal(2, s.Slips(port, 0));
        ToMidnight(s);
        Assert.Equal([true, true, false], s.Human.ShipOrders.Select(o => o.PortId != null));
    }

    [Fact]
    public void WithoutTheMeansTheSlipWorksOnlyWhatItCanPay()
    {
        var (s, port) = WithPort();
        port.Population = 0; // nobody cuts wood today
        s.OrderShip(0, BattalionType.LineShip);
        var trireme = BattalionType.LineShip.First();
        // Half a day's wood.
        s.Human.Stockpile[ResourceType.Wood] = trireme.Cost.Items.First(i => i.Type == ResourceType.Wood).Amount / trireme.TrainingDays / 2;
        s.Human.Stockpile[ResourceType.Gold] = 5000;
        ToMidnight(s);
        Assert.InRange(s.Human.ShipOrders[0].DaysDone, 0.4, 0.6);
    }

    [Fact]
    public void TheQueueCanBeReorderedAndCancelled()
    {
        var (s, _) = WithPort();
        s.OrderShip(0, BattalionType.LineShip);
        s.OrderShip(0, BattalionType.Transport);
        var ids = s.Human.ShipOrders.Select(o => o.Id).ToList();
        Assert.True(s.MoveShipOrder(0, ids[1], -1).Ok);
        Assert.Equal([ids[1], ids[0]], s.Human.ShipOrders.Select(o => o.Id));
        Assert.True(s.CancelShipOrder(0, ids[1]).Ok);
        Assert.Equal([ids[0]], s.Human.ShipOrders.Select(o => o.Id));
        Assert.False(s.CancelShipOrder(0, ids[1]).Ok);
    }

    [Fact]
    public void AFinishedShipWaitsForItsCrew()
    {
        var (s, _) = WithPort();
        s.OrderShip(0, BattalionType.Transport);
        s.Human.ShipOrders[0].DaysDone = BattalionType.Transport.First().TrainingDays;
        s.Human.Manpower = 0;
        ToMidnight(s);
        Assert.True(s.Human.ShipOrders.Single().WaitingForCrew);
        Assert.DoesNotContain(s.Units, u => u.IsFleet);
        s.Human.Manpower = 3000;
        ToMidnight(s);
        Assert.Empty(s.Human.ShipOrders);
        Assert.Contains(s.Units, u => u.IsFleet);
    }

    [Fact]
    public void ShipsNeedAPortAndTheirShipyard()
    {
        var (s, port) = WithPort();
        port.Buildings.Remove(BuildingType.Port);
        Assert.StartsWith("Necesitas un puerto", s.CanOrderShip(s.Human, BattalionType.Transport).Message);
        port.AddBuilding(BuildingType.Port);
        foreach (var t in new[] { Tech.NavalEngineering }) s.Human.Learn(t);
        Assert.StartsWith("Requiere dique seco", s.CanOrderShip(s.Human, BattalionType.Escort).Message);
        // Ordering from a port's panel puts it in the queue too.
        Assert.True(s.Train(0, port.Id, BattalionType.Transport).Ok);
        Assert.Equal(port.Id, s.Human.ShipOrders.Single().PreferredPortId);
    }

    [Fact]
    public void TheQueueIsSaved()
    {
        var (s, _) = WithPort();
        s.OrderShip(0, BattalionType.LineShip);
        s.OrderShip(0, BattalionType.Transport);
        ToMidnight(s);
        var loaded = GameSession.Load(_map, s.ToSave("test"));
        Assert.Equal(s.Human.ShipOrders.Select(o => (o.Id, o.Type, o.Model, o.PortId, o.DaysDone)),
            loaded.Human.ShipOrders.Select(o => (o.Id, o.Type, o.Model, o.PortId, o.DaysDone)));
        Assert.True(loaded.OrderShip(0, BattalionType.Transport).Ok);
        Assert.Equal(3, loaded.Human.ShipOrders.Select(o => o.Id).Distinct().Count());
    }

    [Fact]
    public void TheNavyTabOrdersShipsAndShowsTheQueue()
    {
        var game = new GameController(WithPort().Session);
        game.Nation.Visible = true;
        game.Nation.Tab = NationTab.Navy;
        var page = Assert.IsType<TablesPage>(game.Nation.Page());
        Assert.StartsWith("Agrupaciones", page.Tables[0].Title);
        Assert.StartsWith("Mando naval", page.Tables[1].Title);
        Assert.Equal("Astilleros", page.Tables[2].Title);
        var order = page.Tables[4].Table.Rows.SelectMany(r => r).OfType<ButtonsCell>().SelectMany(c => c.Buttons).First(b => b.Text == "Encargar" && b.Enabled);
        order.Press();
        var queue = Assert.IsType<TablesPage>(game.Nation.Page()).Tables[3].Table;
        Assert.Single(queue.Rows);
    }
}
