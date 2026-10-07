using System.Numerics;
using Conquer.Game.Buildings;
using Conquer.Game.Economy;
using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Simulation;

namespace Conquer.Presentation;

/// <summary>
/// A city on screen: its icon (<see cref="Models.CityIcon"/>), or else houses with roofs of its nation's colour (more of
/// them the more it holds) and a tower if it is the capital; and its name when there is room.
/// </summary>
public sealed record CityMarker(Vector2 Screen, float Scale, uint Color, int Population, bool Capital, string? Name, int Id = 0, bool Industry = false,
    string Icon = "cabana");

/// <summary>A nation's name over the middle of its land, as large as the land looks and fading when zoomed in close.</summary>
public sealed record NationLabel(Vector2 Screen, string Name, TextSize Size, float Alpha, uint Color);

/// <summary>A province's name over its middle (above its city, if it has one), shown close in and fading in as the map zooms.</summary>
public sealed record ProvinceLabel(Vector2 Screen, string Name, float Alpha);

public enum CounterKind
{
    Military,
    Fleet,
    Headquarters,
    Settlers,
}

/// <summary>
/// A unit's counter on screen with what goes with it: its route (screen points, fuller when selected), the line to
/// its HQ (green in range, red out of it) and the arrow of an attack. <see cref="Scale"/> shrinks counters when zoomed out.
/// <see cref="Entrenchment"/> is how far an emplaced unit has dug in (null when it is not emplaced). The units of a nation
/// standing together in a province make one stack, as in Hearts of Iron III: the counter is its top unit's, and
/// <see cref="Stack"/> holds them all, the top one first (or in their order when the selected one is among them).
/// <see cref="ShortName"/> is the unit's name abbreviated (<see cref="UnitLabels.Short"/>), <see cref="Flag"/> its nation's.
/// </summary>
public sealed record UnitCounter(int UnitId, Vector2 Screen, float Scale, uint Color, bool Selected, CounterKind Kind, UnitFunction Function,
    string Symbol, int Aboard, string Echelon, double Strength, double Organisation,
    IReadOnlyList<Vector2>? Path, (Vector2 To, bool InRange)? Command, (Vector2 From, Vector2 To)? Attack,
    bool Moving = false, bool Fighting = false, Vector2 Heading = default, string? Model = null, double? Entrenchment = null,
    NationFlag? Flag = null, string ShortName = "", IReadOnlyList<int>? Stack = null)
{
    /// <summary>How many units the counter stands for: the number on it.</summary>
    public int Count => Stack?.Count ?? 1;
}

/// <summary>Crossed swords over a battle; <see cref="Battle"/> is null at sea. The tooltip is worked out only when hovered.</summary>
public sealed record BattleMarker(int ProvinceId, Battle? Battle, Vector2 Screen, Func<string> Tooltip);

/// <summary>Everything drawn on the map over the provinces, in screen positions, already left out when off screen.</summary>
public sealed record MapMarkers(IReadOnlyList<CityMarker> Cities, IReadOnlyList<NationLabel> Nations, IReadOnlyList<UnitCounter> Units, IReadOnlyList<BattleMarker> Battles,
    IReadOnlyList<DepositMarker> Deposits, IReadOnlyList<ProvinceLabel> Provinces);

/// <summary>
/// In the resources mode, close enough in: the icons of a province's deposits in a row, <see cref="Size"/> pixels each,
/// centred on <see cref="Screen"/>.
/// </summary>
public sealed record DepositMarker(int ProvinceId, Vector2 Screen, float Size, IReadOnlyList<ResourceType> Resources);

/// <summary>The markers on the map, worked out from the camera.</summary>
public sealed partial class GameController
{
    /// <summary>From this zoom on, the resources mode shows each province's deposits as icons instead of colours.</summary>
    public const float DepositIconZoom = 3f;

    /// <summary>The resources mode shows icons now (close in) rather than colouring each province by its main deposit.</summary>
    public bool DepositIconsShown => Mode == MapMode.Resources && Camera.Zoom >= DepositIconZoom;

    private bool OnScreen(Vector2 p, float margin = 40) =>
        p.X > -margin && p.Y > -margin && p.X < Camera.Screen.X + margin && p.Y < Camera.Screen.Y + margin;

    public MapMarkers Markers()
    {
        _ = ExploredProvinces; // brings what the player has explored up to date before hiding the rest
        return new(Cities(), NationLabels(), Counters(), Battles(), Deposits(), ProvinceLabels());
    }

    /// <summary>
    /// Every deposit the player knows in each explored province on screen (only the chosen resource, with the filter),
    /// richest first; above the city, where there is one. None while <see cref="DepositIconsShown"/> is off.
    /// </summary>
    private List<DepositMarker> Deposits()
    {
        var marks = new List<DepositMarker>();
        if (!DepositIconsShown) return marks;
        float size = Math.Clamp(Camera.Zoom * 3.5f, 14, 26);
        foreach (var p in Map.Provinces)
        {
            if (!p.IsClaimable || !IsExplored(p.Id)) continue;
            var s = Camera.MapToScreen(Center(p.Id));
            if (!OnScreen(s)) continue;
            var found = Resources.Deposits
                .Where(r => p.HasDeposit(r) && Human.Knows(r) && (ResourceFilter is null || ResourceFilter == r))
                .OrderByDescending(r => p.Reserves[(int)r]).ToList();
            if (found.Count == 0) continue;
            if (p.CityId.HasValue) s.Y -= 22 + size / 2;
            marks.Add(new DepositMarker(p.Id, s, size, found));
        }
        return marks;
    }

    /// <summary>From this zoom on, the provinces show their names, fading in until <see cref="ProvinceNamesFullZoom"/>.</summary>
    public const float ProvinceNamesZoom = 3.5f, ProvinceNamesFullZoom = 4.5f;

    /// <summary>The names of the named provinces the player has explored on screen, close in; above the city where there is one.</summary>
    private List<ProvinceLabel> ProvinceLabels()
    {
        var labels = new List<ProvinceLabel>();
        if (Camera.Zoom < ProvinceNamesZoom) return labels;
        float alpha = Math.Clamp((Camera.Zoom - ProvinceNamesZoom) / (ProvinceNamesFullZoom - ProvinceNamesZoom), 0.25f, 1);
        foreach (var p in Map.Provinces)
        {
            if (p.Name.Length == 0 || !IsExplored(p.Id)) continue;
            var s = Camera.MapToScreen(Center(p.Id));
            if (!OnScreen(s)) continue;
            if (p.CityId.HasValue) s.Y -= 30;
            labels.Add(new ProvinceLabel(s, p.Name, alpha));
        }
        return labels;
    }

    private List<CityMarker> Cities()
    {
        var cities = new List<CityMarker>();
        foreach (var city in Session.Cities)
        {
            // Cities in lands the player has never seen are unknown to them.
            if (!IsExplored(city.ProvinceId)) continue;
            var s = Camera.MapToScreen(Center(city.ProvinceId));
            if (!OnScreen(s)) continue;
            bool capital = Session.Players[city.OwnerId].CapitalCityId == city.Id;
            // Houses grow a little as the map is zoomed in.
            float scale = Math.Clamp(Camera.Zoom / 4.5f, 0.6f, 1.5f);
            bool named = Camera.Zoom >= 2.5f || (capital && Camera.Zoom >= 1);
            var owner = Session.Players[city.OwnerId];
            var p = Map.Provinces[city.ProvinceId];
            cities.Add(new CityMarker(s, scale, owner.Color, (int)p.Population, capital, named ? city.Name : null,
                city.Id, p.Buildings.Any(b => b is BuildingType.Workshop or BuildingType.Factory),
                Models.CityIcon(p.Population, capital, owner.Era >= Conquer.Game.Science.Era.Modern)));
        }
        return cities;
    }

    /// <summary>
    /// Each nation's name over its land: in its middle (a circular mean of longitudes, so a nation across the date
    /// line is labelled over its land), sized to how big it looks, hidden while it is too small and faded out when
    /// zoomed in so close that it covers the screen. Only the land the player has explored counts.
    /// </summary>
    private List<NationLabel> NationLabels()
    {
        const float KmPerMapPixel = 40075f / 3600;
        var labels = new List<NationLabel>();
        foreach (var player in Session.Players)
        {
            var known = player.Provinces.Where(IsExplored).ToList();
            if (known.Count < 3) continue;
            double cos = 0, sin = 0, ySum = 0, area = 0;
            foreach (int id in known)
            {
                var p = Map.Provinces[id];
                double angle = (p.CenterX + 0.5) / Map.Width * Math.Tau;
                cos += Math.Cos(angle) * p.AreaKm2;
                sin += Math.Sin(angle) * p.AreaKm2;
                ySum += (p.CenterY + 0.5) * p.AreaKm2;
                area += p.AreaKm2;
            }
            double mean = Math.Atan2(sin, cos);
            var centre = new Vector2((float)((mean < 0 ? mean + Math.Tau : mean) / Math.Tau * Map.Width), (float)(ySum / area));
            var s = Camera.MapToScreen(centre);
            if (!OnScreen(s)) continue;

            float extent = (float)Math.Sqrt(area) / KmPerMapPixel * Camera.Zoom;
            if (extent < 45) continue;
            var size = extent > 280 ? TextSize.Title : extent > 150 ? TextSize.Large : extent > 80 ? TextSize.Normal : TextSize.Small;
            float alpha = Math.Clamp((1600 - extent) / 600, 0, 1) * 0.85f;
            if (alpha <= 0) continue;
            labels.Add(new NationLabel(s, player.Name.ToUpperInvariant(), size, alpha, player.Color));
        }
        return labels;
    }

    /// <summary>From this zoom on, the stacks in a province with a city stand above it, clear of the city and its name.</summary>
    public const float UnitSpreadZoom = 6f;

    /// <summary>
    /// The units on the map, stacked as in Hearts of Iron III: those of one nation standing in a province (attacking the
    /// same province, or not attacking) make one counter with their number on it, the selected one on top, or else the
    /// first of them by kind (combat units, fleets, HQs, settlers) and number. Stacks sharing a province stand side by
    /// side on its centre (over its city), and from <see cref="UnitSpreadZoom"/> above the city. Units on the march go on
    /// their own, where their route has got them. Units aboard show in their fleet's panel.
    /// </summary>
    private List<UnitCounter> Counters()
    {
        var counters = new List<UnitCounter>();
        // The fog of war hides units the player cannot see.
        var shown = Session.Units.Where(u => !u.IsAboard && Session.CanSee(Human.Id, u)).ToList();
        bool Standing(Unit u) => !u.IsMoving || u.AttackingProvinceId.HasValue;
        var stacks = shown.Where(Standing)
            .GroupBy(u => (u.ProvinceId, u.OwnerId, u.AttackingProvinceId))
            .Select(g => g.OrderBy(u => u.IsMilitary ? 0 : u.IsFleet ? 1 : u.IsHeadquarters ? 2 : 3).ThenBy(u => u.Id).ToList())
            .ToList();
        var marching = shown.Where(u => !Standing(u)).Select(u => new List<Unit> { u });
        // Each province's stacks side by side, in the order of their nations.
        var inProvince = stacks.GroupBy(st => st[0].ProvinceId)
            .ToDictionary(g => g.Key, g => g.OrderBy(st => st[0].OwnerId).ThenBy(st => st[0].AttackingProvinceId ?? -1).ToList());
        // The selected unit's stack goes last, so it is drawn (and clicked) on top.
        foreach (var stack in stacks.Concat(marching).OrderBy(st => st.Any(u => u.Id == SelectedUnitId)))
        {
            var top = stack.FirstOrDefault(u => u.Id == SelectedUnitId) ?? stack[0];
            bool standing = Standing(top);
            var pos = standing ? Center(top.ProvinceId) : Between(top.ProvinceId, top.Path[0], top.StepProgress);
            var s = Camera.MapToScreen(pos);
            float scale = top.Id == SelectedUnitId ? 1 : Math.Clamp(Camera.Zoom / 3, 0.45f, 1);
            if (standing)
            {
                var here = inProvince[top.ProvinceId];
                int index = here.IndexOf(stack);
                s.X += (index - (here.Count - 1) / 2f) * 170 * scale;
                if (Camera.Zoom >= UnitSpreadZoom && Map.Provinces[top.ProvinceId].CityId.HasValue) s.Y -= 34 * Math.Clamp(Camera.Zoom / 6, 1, 1.6f);
            }
            if (!OnScreen(s)) continue;

            bool selected = top.Id == SelectedUnitId;
            var path = selected || (top.OwnerId == Human.Id && Camera.Zoom >= 1.5f) ? Route(top, pos) : null;
            (Vector2, bool)? command = selected && Session.CommanderOf(top) is { } hq
                ? (Camera.MapToScreen(Center(hq.ProvinceId)), Session.InCommandRange(top)) : null;
            (Vector2, Vector2)? attack = top.AttackingProvinceId is int target
                ? (Camera.MapToScreen(pos), Camera.MapToScreen(Between(top.ProvinceId, target, 1))) : null;
            var kind = top.IsMilitary ? CounterKind.Military : top.IsFleet ? CounterKind.Fleet : top.IsHeadquarters ? CounterKind.Headquarters : CounterKind.Settlers;
            var owner = Session.Players[top.OwnerId];
            // The top unit first, then the rest in their order, so clicking again goes through them.
            var ids = stack.Select(u => u.Id).ToList();
            if (!selected) { ids.Remove(top.Id); ids.Insert(0, top.Id); }
            counters.Add(new UnitCounter(top.Id, s, scale, owner.Color, selected,
                kind, top.Function, top.Symbol, top.IsFleet ? Session.CargoOf(top).Count() : 0, top.Echelon, top.StrengthShare, top.OrganisationShare,
                path, command, attack, top.IsMoving, Fighting(top), Heading(top), Models.Of(top),
                GameSession.IsEmplaced(top) ? top.Entrenchment : null,
                Flags.Of(owner.Name, owner.Color), UnitLabels.Short(top.Name), ids));
        }
        return counters;
    }

    /// <summary>
    /// Clicking a stack: selects its top unit, or, when one of its units is already selected, the next one in it (after
    /// the last, the first again), as clicking a stack over and over goes through its units.
    /// </summary>
    public void SelectInStack(IReadOnlyList<int> stack)
    {
        if (stack.Count == 0) return;
        int at = SelectedUnitId is int id ? stack.ToList().IndexOf(id) : -1;
        SelectUnit(stack[at < 0 ? 0 : (at + 1) % stack.Count]);
    }

    /// <summary>Whether the unit is attacking, or defending a province under attack, so its counter shakes.</summary>
    private bool Fighting(Unit unit) =>
        unit.AttackingProvinceId.HasValue
        || (unit.IsMilitary && Session.Battles.Any(b => b.ProvinceId == unit.ProvinceId && b.DefenderId == unit.OwnerId));

    /// <summary>The way the unit is going on screen, of length 1, or zero when it is not moving.</summary>
    private Vector2 Heading(Unit unit)
    {
        if (!unit.IsMoving) return Vector2.Zero;
        var d = Camera.MapToScreen(Between(unit.ProvinceId, unit.Path[0], 1)) - Camera.MapToScreen(Center(unit.ProvinceId));
        return d.LengthSquared() > 0.01f ? Vector2.Normalize(d) : Vector2.Zero;
    }

    /// <summary>
    /// The unit's route on screen, from where it is through the centre of each province it will cross; each step
    /// crosses the date line the short way. Null when it is not going anywhere.
    /// </summary>
    private List<Vector2>? Route(Unit unit, Vector2 from)
    {
        if (unit.Path.Count == 0) return null;
        var origin = Camera.MapToScreen(from);
        var points = new List<Vector2> { origin };
        var previous = from;
        foreach (int step in unit.Path)
        {
            var c = Center(step);
            float dx = c.X - previous.X;
            dx -= Map.Width * MathF.Round(dx / Map.Width);
            previous = new Vector2(previous.X + dx, c.Y);
            points.Add(origin + (previous - from) * Camera.Zoom);
        }
        return points;
    }

    /// <summary>Crossed swords over every province being fought for, on land or at sea.</summary>
    private List<BattleMarker> Battles()
    {
        var marks = new List<BattleMarker>();
        void Add(int provinceId, Battle? battle, Func<string> summary)
        {
            var s = Camera.MapToScreen(Center(provinceId));
            if (OnScreen(s)) marks.Add(new BattleMarker(provinceId, battle, s, () => summary() + "\nClic para ver la batalla en detalle."));
        }
        var visible = Session.VisibleProvinces(Human.Id);
        foreach (var battle in Session.Battles.Where(b => visible.Contains(b.ProvinceId))) Add(battle.ProvinceId, battle, () => BattleSummary(battle));
        foreach (int sea in Session.NavalBattleProvinces().Where(visible.Contains)) Add(sea, null, () => NavalBattleSummary(sea));
        return marks;
    }

    private string NavalBattleSummary(int provinceId)
    {
        var sides = Session.Units.Where(u => u.IsFleet && u.ProvinceId == provinceId).GroupBy(u => u.OwnerId)
            .Select(g => $"{Session.Players[g.Key].Name}: {Formations.ShipCount(g.Sum(f => f.Battalions.Count))} · " +
                         $"organización {g.Average(f => f.OrganisationShare):P0}");
        return $"Batalla naval en {Map.Provinces[provinceId].DisplayName}\n" + string.Join("\n", sides);
    }

    private string BattleSummary(Battle battle)
    {
        var attackers = battle.Attackers.Select(Session.UnitById).OfType<Unit>().ToList();
        var defenders = Session.EnemyRegimentsIn(battle.ProvinceId, battle.AttackerId).ToList();
        var p = Map.Provinces[battle.ProvinceId];
        bool engineers = GameSession.HasEngineers(attackers);
        string Side(string role, int player, List<Unit> units, bool attacking)
        {
            var engaged = Session.Engage(units, p, attacking, engineers);
            int total = units.Sum(u => u.Battalions.Count);
            return $"{role}: {Session.Players[player].Name} · {units.Count} u. · {units.Sum(u => u.Citizens):N0} hombres · " +
                   $"organización {(units.Count == 0 ? 0 : units.Average(u => u.OrganisationShare)):P0}\n" +
                   $"   combaten {engaged.Count} de {total} batallones · armas combinadas +{GameSession.CombinedArms(engaged.Select(e => e.Role)):P0}";
        }
        return $"Batalla por {Session.PlaceName(p)} ({GameSession.FormatHours(Session.Date.Hours - battle.StartHours)})\n" +
               Side("Atacante", battle.AttackerId, attackers, true) + "\n" + Side("Defensor", battle.DefenderId, defenders, false) +
               $"\nFrente: {MilitaryRules.FrontWidth(p.Biome)} batallones en primera línea, más artillería, aviación e ingenieros detrás" +
               $"\nDefensa por el terreno{(p.HasRiver ? " y el río" : "")}: ×{MilitaryRules.DefenseMultiplier(p, engineers):0.##}" +
               (engineers && MilitaryRules.DefenseMultiplier(p, true) < MilitaryRules.DefenseMultiplier(p) ? $" (×{MilitaryRules.DefenseMultiplier(p):0.##} sin los ingenieros del atacante)" : "");
    }
}
