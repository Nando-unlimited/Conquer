using System.Numerics;
using Conquer.Game.Buildings;
using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Simulation;

namespace Conquer.Presentation;

/// <summary>A city on screen: houses with roofs of its nation's colour (more of them the more it holds), a tower if it is the capital, and its name when there is room.</summary>
public sealed record CityMarker(Vector2 Screen, float Scale, uint Color, int Population, bool Capital, string? Name, int Id = 0, bool Industry = false);

/// <summary>A nation's name over the middle of its land, as large as the land looks and fading when zoomed in close.</summary>
public sealed record NationLabel(Vector2 Screen, string Name, TextSize Size, float Alpha, uint Color);

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
/// </summary>
public sealed record UnitCounter(int UnitId, Vector2 Screen, float Scale, uint Color, bool Selected, CounterKind Kind, UnitFunction Function,
    string Symbol, int Aboard, string Echelon, double Strength, double Organisation,
    IReadOnlyList<Vector2>? Path, (Vector2 To, bool InRange)? Command, (Vector2 From, Vector2 To)? Attack,
    bool Moving = false, bool Fighting = false, Vector2 Heading = default);

/// <summary>Crossed swords over a battle; <see cref="Battle"/> is null at sea. The tooltip is worked out only when hovered.</summary>
public sealed record BattleMarker(int ProvinceId, Battle? Battle, Vector2 Screen, Func<string> Tooltip);

/// <summary>Everything drawn on the map over the provinces, in screen positions, already left out when off screen.</summary>
public sealed record MapMarkers(IReadOnlyList<CityMarker> Cities, IReadOnlyList<NationLabel> Nations, IReadOnlyList<UnitCounter> Units, IReadOnlyList<BattleMarker> Battles);

/// <summary>The markers on the map, worked out from the camera.</summary>
public sealed partial class GameController
{
    private bool OnScreen(Vector2 p, float margin = 40) =>
        p.X > -margin && p.Y > -margin && p.X < Camera.Screen.X + margin && p.Y < Camera.Screen.Y + margin;

    public MapMarkers Markers() => new(Cities(), NationLabels(), Counters(), Battles());

    private List<CityMarker> Cities()
    {
        var cities = new List<CityMarker>();
        foreach (var city in Session.Cities)
        {
            var s = Camera.MapToScreen(Center(city.ProvinceId));
            if (!OnScreen(s)) continue;
            bool capital = Session.Players[city.OwnerId].CapitalCityId == city.Id;
            // Houses grow a little as the map is zoomed in.
            float scale = Math.Clamp(Camera.Zoom / 4.5f, 0.6f, 1.5f);
            bool named = Camera.Zoom >= 2.5f || (capital && Camera.Zoom >= 1);
            cities.Add(new CityMarker(s, scale, Session.Players[city.OwnerId].Color, (int)Map.Provinces[city.ProvinceId].Population, capital, named ? city.Name : null,
                city.Id, Map.Provinces[city.ProvinceId].Buildings.Any(b => b is BuildingType.Workshop or BuildingType.Factory)));
        }
        return cities;
    }

    /// <summary>
    /// Each nation's name over its land: in its middle (a circular mean of longitudes, so a nation across the date
    /// line is labelled over its land), sized to how big it looks, hidden while it is too small and faded out when
    /// zoomed in so close that it covers the screen.
    /// </summary>
    private List<NationLabel> NationLabels()
    {
        const float KmPerMapPixel = 40075f / 3600;
        var labels = new List<NationLabel>();
        foreach (var player in Session.Players)
        {
            if (player.Provinces.Count < 3) continue;
            double cos = 0, sin = 0, ySum = 0, area = 0;
            foreach (int id in player.Provinces)
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

    /// <summary>NATO-style counters, stacked a little up and to the right when several share a province. Units aboard show in their fleet's panel.</summary>
    private List<UnitCounter> Counters()
    {
        var counters = new List<UnitCounter>();
        var stackIndex = new Dictionary<int, int>();
        foreach (var unit in Session.Units)
        {
            // The fog of war hides units the player cannot see.
            if (unit.IsAboard || !Session.CanSee(Human.Id, unit)) continue;
            var pos = unit.IsMoving && !unit.AttackingProvinceId.HasValue ? Between(unit.ProvinceId, unit.Path[0], unit.StepProgress) : Center(unit.ProvinceId);
            var s = Camera.MapToScreen(pos);
            if (!OnScreen(s)) continue;
            int stack = stackIndex.GetValueOrDefault(unit.ProvinceId);
            stackIndex[unit.ProvinceId] = stack + 1;
            s += new Vector2(stack * 5, -stack * 7 - 14);

            bool selected = unit.Id == SelectedUnitId;
            var path = selected || (unit.OwnerId == Human.Id && Camera.Zoom >= 1.5f) ? Route(unit, pos) : null;
            (Vector2, bool)? command = selected && Session.CommanderOf(unit) is { } hq
                ? (Camera.MapToScreen(Center(hq.ProvinceId)), Session.InCommandRange(unit)) : null;
            (Vector2, Vector2)? attack = unit.AttackingProvinceId is int target
                ? (Camera.MapToScreen(pos), Camera.MapToScreen(Between(unit.ProvinceId, target, 1))) : null;
            var kind = unit.IsMilitary ? CounterKind.Military : unit.IsFleet ? CounterKind.Fleet : unit.IsHeadquarters ? CounterKind.Headquarters : CounterKind.Settlers;
            counters.Add(new UnitCounter(unit.Id, s, selected ? 1 : Math.Clamp(Camera.Zoom / 3, 0.45f, 1), Session.Players[unit.OwnerId].Color, selected,
                kind, unit.Function, unit.Symbol, unit.IsFleet ? Session.CargoOf(unit).Count() : 0, unit.Echelon, unit.StrengthShare, unit.OrganisationShare,
                path, command, attack, unit.IsMoving, Fighting(unit), Heading(unit)));
        }
        return counters;
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
