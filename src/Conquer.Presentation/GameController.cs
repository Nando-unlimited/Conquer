using System.Numerics;
using Conquer.Game.Economy;
using Conquer.Game.Entities;
using Conquer.Game.Rules;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Presentation;

/// <summary>Which tab the province panel shows.</summary>
public enum ProvinceTab
{
    General,
    Buildings,
    Army,
}

/// <summary>A city being named: founded by settlers (<see cref="UnitId"/>) or built by a province's citizens.</summary>
public readonly record struct CityNaming(int? UnitId, int ProvinceId);

/// <summary>
/// A game on screen, without the screen: the view (camera, map mode), what is selected, the open dialogs, the clock
/// and the messages, and the player's orders. The client draws it and turns clicks and keys into calls here.
/// </summary>
public sealed partial class GameController
{
    private int _seenNotifications;

    public GameSession Session { get; }
    public Player Human => Session.Human;
    public WorldMap Map => Session.Map;

    public Camera Camera { get; }
    public GameClock Clock { get; } = new();
    public MessageLog Messages { get; } = new();
    /// <summary>Real seconds since the game was opened; messages and animations go by it.</summary>
    public double Now { get; private set; }

    public MapMode Mode { get; set; }
    /// <summary>In the resources mode, the only resource shown (null for all of them).</summary>
    public ResourceType? ResourceFilter { get; set; }

    public int? SelectedUnitId { get; private set; }
    /// <summary>The selected province, or -1.</summary>
    public int SelectedProvince { get; private set; } = -1;
    /// <summary>The province under the mouse, or -1; the client sets it every frame.</summary>
    public int HoverProvince { get; set; } = -1;
    public ProvinceTab ProvinceTab { get; set; }

    /// <summary>The next click on the map picks where the forced migrants go.</summary>
    public bool ChoosingMigrationTarget { get; set; }
    public int MigrationAmount { get; set; } = 50;

    public CityNaming? Naming { get; private set; }

    /// <summary>The nation screen (button «Nación» or key N).</summary>
    public NationScreen Nation { get; }
    public string CityName { get; set; } = "";

    /// <param name="loaded">A saved game being carried on: it opens on the player's capital and does not repeat old messages.</param>
    public GameController(GameSession session, bool loaded = false)
    {
        Session = session;
        Camera = new Camera(session.Map.Width, session.Map.Height);
        Nation = new NationScreen(this);
        session.Happened += Hear;
        if (loaded)
        {
            _seenNotifications = session.Notifications.Count;
            Camera.LookAt(Camera.Center, zoom: 8);
            CenterOnHome();
            Messages.Add("Partida cargada.", 0);
            return;
        }
        var settlers = session.Units.First(u => u.OwnerId == GameSession.HumanPlayerId);
        SelectedUnitId = settlers.Id;
        Camera.LookAt(Center(settlers.ProvinceId), zoom: 8);
    }

    /// <summary>
    /// One frame: <paramref name="dt"/> real seconds go by, the game advances as fast as the clock says unless
    /// <paramref name="frozen"/> (a window that stops time is open), and new notifications become messages.
    /// </summary>
    public void Tick(double dt, bool frozen)
    {
        Now += dt;
        if (!frozen)
            for (int i = Clock.Advance(dt); i > 0; i--) Session.Step();
        for (; _seenNotifications < Session.Notifications.Count; _seenNotifications++)
        {
            var n = Session.Notifications[_seenNotifications];
            if (n.PlayerId == Human.Id || n.PlayerId < 0) Messages.Add(n.Text, Now);
        }
        ListenForAlerts();
    }

    /// <summary>Shows the result of an order: what happened, or why it could not be done.</summary>
    public void Show(CommandResult result)
    {
        Messages.Add(result.Message, Now, result.Ok);
        if (result.Ok) Play(SoundCue.Confirm);
    }

    // ------------------------------------------------------------------ view

    /// <summary>The middle of a province, in map pixels.</summary>
    public Vector2 Center(int provinceId)
    {
        var p = Map.Provinces[provinceId];
        return new Vector2(p.CenterX + 0.5f, p.CenterY + 0.5f);
    }

    /// <summary>Map position between two provinces, crossing the date line the short way.</summary>
    public Vector2 Between(int from, int to, double t)
    {
        var a = Center(from);
        var b = Center(to);
        float dx = b.X - a.X;
        dx -= Map.Width * MathF.Round(dx / Map.Width);
        return new Vector2(a.X + dx * (float)t, a.Y + (b.Y - a.Y) * (float)t);
    }

    public void CenterOnHome()
    {
        if (Human.CapitalCityId is int capital && Session.CityById(capital) is { } city) Camera.LookAt(Center(city.ProvinceId));
        else if (Session.Units.FirstOrDefault(u => u.OwnerId == Human.Id) is { } unit) Camera.LookAt(Center(unit.ProvinceId));
    }

    public void CycleMode() => Mode = (MapMode)(((int)Mode + 1) % Enum.GetValues<MapMode>().Length);

    // ------------------------------------------------------------------ selection

    /// <summary>The selected unit, if it still exists and can be seen; one that is gone or lost in the fog is deselected.</summary>
    public Unit? SelectedUnit
    {
        get
        {
            var unit = SelectedUnitId is int id ? Session.UnitById(id) : null;
            if (unit != null && !Session.CanSee(Human.Id, unit)) unit = null;
            if (unit == null) SelectedUnitId = null;
            return unit;
        }
    }

    /// <summary>The provinces the player can see; the rest lie under the fog of war.</summary>
    public IReadOnlySet<int> VisibleProvinces => Session.VisibleProvinces(Human.Id);

    /// <summary>The provinces the player has ever seen; the rest of the world is unknown and drawn black.</summary>
    public IReadOnlySet<int> ExploredProvinces
    {
        get
        {
            Session.VisibleProvinces(Human.Id); // what the player sees now counts as explored
            return Human.Explored;
        }
    }

    /// <summary>Whether the player has explored a province, as of the last look at <see cref="ExploredProvinces"/>.</summary>
    public bool IsExplored(int provinceId) => Human.Explored.Contains(provinceId);

    /// <summary>A number that changes when what the player sees or has explored changes, so the client knows when to redraw the fog.</summary>
    public int FogSignature
    {
        get
        {
            var visible = Session.VisibleProvinces(Human.Id);
            if (!ReferenceEquals(visible, _fogFor))
            {
                _fogFor = visible;
                _fogSignature = visible.Aggregate(visible.Count, (hash, id) => hash ^ (id * 397) ^ (id << 11));
            }
            return _fogSignature ^ (Human.Explored.Count * 7919);
        }
    }

    private IReadOnlySet<int>? _fogFor;
    private int _fogSignature;

    public bool HasSelection => SelectedUnitId.HasValue || SelectedProvince >= 0;

    public void SelectUnit(int unitId)
    {
        SelectedUnitId = unitId;
        SelectedProvince = -1;
    }

    /// <summary>Selects a province; one still unexplored cannot be, so clicking it clears the selection.</summary>
    public void SelectProvince(int provinceId)
    {
        SelectedUnitId = null;
        SelectedProvince = provinceId >= 0 && ExploredProvinces.Contains(provinceId) ? provinceId : -1;
    }

    public void ClearSelection()
    {
        SelectedUnitId = null;
        SelectedProvince = -1;
    }

    /// <summary>Selects a unit and centres the map on it (from the nation screen).</summary>
    public void ViewUnit(int unitId)
    {
        if (Session.UnitById(unitId) is not { } unit) return;
        SelectUnit(unit.Id);
        ChoosingMigrationTarget = false;
        Camera.LookAt(Center(unit.ProvinceId));
    }

    /// <summary>Selects a province and centres the map on it (from the nation screen).</summary>
    public void ViewProvince(int provinceId)
    {
        SelectProvince(provinceId);
        ChoosingMigrationTarget = false;
        Camera.LookAt(Center(provinceId));
    }

    // ------------------------------------------------------------------ orders

    /// <summary>
    /// A left click on the map that hit no marker: sends the forced migrants there when choosing their destination,
    /// otherwise selects the province under the mouse.
    /// </summary>
    public void ClickProvince()
    {
        if (ChoosingMigrationTarget)
        {
            ChoosingMigrationTarget = false;
            if (HoverProvince >= 0 && SelectedProvince >= 0)
                Show(Session.ForceMigration(Human.Id, SelectedProvince, HoverProvince, MigrationAmount));
            return;
        }
        SelectProvince(HoverProvince);
    }

    /// <summary>A right click on the map: moves the selected unit of the player's there (attacking, if the place is enemy).</summary>
    public void OrderMove()
    {
        if (SelectedUnit is not { } unit || unit.OwnerId != Human.Id || HoverProvince < 0) return;
        var result = Session.MoveUnit(Human.Id, unit.Id, HoverProvince);
        // An order of the player's own takes over from exploring.
        if (result.Ok && unit.ExploresAlone) Session.SetScoutOrders(Human.Id, unit.Id, ScoutOrders.None);
        Show(result);
    }

    /// <summary>Saves the game; true if it worked. Either way a message says so.</summary>
    public bool Save(string version)
    {
        try
        {
            string name = SaveFiles.Save(Session, version);
            Show(CommandResult.Success($"Partida guardada: {name}."));
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Show(CommandResult.Fail($"No se pudo guardar la partida: {e.Message}"));
            return false;
        }
    }

    // ------------------------------------------------------------------ naming a city

    /// <summary>Opens the dialog to name a city founded by these settlers, or built by this province's citizens.</summary>
    public void OpenCityNaming(int? unitId, int provinceId)
    {
        Naming = new CityNaming(unitId, provinceId);
        CityName = Session.SuggestCityName(Human.Id);
    }

    public void SuggestCityName() => CityName = Session.SuggestCityName(Human.Id, CityName);

    public void CancelCityNaming() => Naming = null;

    /// <summary>Founds or starts building the city with the name typed; the dialog stays open if the name is not valid.</summary>
    public void ConfirmCityName()
    {
        if (Naming is not var (unitId, provinceId)) return;
        var result = unitId is int unit ? Session.FoundCity(Human.Id, unit, CityName) : Session.BuildCity(Human.Id, provinceId, CityName);
        Show(result);
        if (!result.Ok) return;
        Naming = null;
        if (unitId.HasValue) SelectProvince(provinceId);
    }
}
