using Conquer.Client.Graphics;
using Conquer.Client.UI;
using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Client.Screens;

/// <summary>
/// The window that opens when a battle's crossed swords are clicked: the terrain, both sides with their men, losses,
/// organisation and fire, every unit with its officer and which of its battalions are in the line, and how the
/// organisation of each side has gone hour by hour. It updates live; time keeps running (pause with Space).
/// </summary>
public sealed partial class GameScreen
{
    private readonly List<(int ProvinceId, Battle? Battle, Rect Bounds)> _battleHitBoxes = [];
    /// <summary>The land battle on show; kept after it ends to show how it went.</summary>
    private Battle? _viewedBattle;
    /// <summary>The sea province whose naval battle is on show.</summary>
    private int? _viewedNavalBattle;

    private bool BattleWindowOpen => _viewedBattle != null || _viewedNavalBattle.HasValue;

    private void OpenBattle(int provinceId, Battle? battle)
    {
        _viewedBattle = battle;
        _viewedNavalBattle = battle == null ? provinceId : null;
    }

    private void CloseBattle()
    {
        _viewedBattle = null;
        _viewedNavalBattle = null;
    }

    /// <summary>For <c>--panel battle</c>: opens the first battle under way, if there is one.</summary>
    private void ShowFirstBattle()
    {
        if (_session.Battles.FirstOrDefault() is { } battle) OpenBattle(battle.ProvinceId, battle);
        else if (_session.NavalBattleProvinces().FirstOrDefault(-1) is var sea and >= 0) OpenBattle(sea, null);
    }

    private void DrawBattleWindow()
    {
        var s = _app.ScreenSize;
        Batch.Rect(0, 0, s.X, s.Y, Rgba.Black.WithAlpha(0.35f));
        Ui.Block(new Rect(0, 0, s.X, s.Y));
        float width = Math.Min(980, s.X - 32), height = Math.Min(700, s.Y - 100);
        var panel = new Rect(s.X / 2 - width / 2, s.Y / 2 - height / 2, width, height);
        Ui.Panel(panel);

        int provinceId = _viewedBattle?.ProvinceId ?? _viewedNavalBattle!.Value;
        if (_viewedBattle is { } battle) LandBattle(battle, panel);
        else NavalBattle(provinceId, panel);

        var bottom = panel.Bottom - 52;
        if (Ui.Button(new Rect(panel.X + 24, bottom, 200, 36), "Ir a la provincia", tooltip: "Centra el mapa en la batalla y cierra la ventana."))
        {
            _game.Camera.LookAt(Center(provinceId));
            CloseBattle();
        }
        if (Ui.Button(new Rect(panel.Right - 24 - 160, bottom, 160, 36), "Cerrar")) CloseBattle();
    }

    // ------------------------------------------------------------------ on land

    private void LandBattle(Battle battle, Rect panel)
    {
        var p = Map.Provinces[battle.ProvinceId];
        bool ongoing = _session.Battles.Contains(battle);
        float x = panel.X + 24, y = panel.Y + 20, w = panel.W - 48;

        Ui.Text(x, y, $"Batalla por {_session.PlaceName(p)}", Theme.Accent, FontSize.Large, bold: true);
        long hours = (battle.EndHours ?? _session.Date.Hours) - battle.StartHours;
        string state = (ongoing ? "En curso"
            : battle.AttackersWon == true ? "Los atacantes toman la provincia"
            : "Los defensores resisten") + $" · {GameSession.FormatHours(hours)}";
        var stateColor = ongoing ? Theme.Bad : Theme.Accent;
        Ui.Text(panel.Right - 24 - Ui.Font.Measure(state, FontSize.Normal, true), y + 8, state, stateColor, bold: true);
        y += 40;

        // While it lasts, the sides as they stand; afterwards, what the last hour left of them.
        var attackers = battle.Attackers.Select(_session.UnitById).OfType<Unit>().Where(u => u.AttackingProvinceId == battle.ProvinceId).ToList();
        var defenders = ongoing ? _session.EnemyRegimentsIn(battle.ProvinceId, battle.AttackerId).ToList() : [];
        bool engineers = GameSession.HasEngineers(attackers);
        var attacking = ongoing ? _session.Engage(attackers, p, attacking: true) : [];
        var defending = ongoing ? _session.Engage(defenders, p, attacking: false, engineers) : [];

        int front = MilitaryRules.FrontWidth(p.Biome);
        double defense = GameSession.DefenseMultiplier(p, engineers), unengineered = GameSession.DefenseMultiplier(p);
        Ui.Text(x, y, $"{p.Info.Name}{(p.HasRiver ? " con río" : "")} · defensa ×{defense:0.##}" +
                      (defense < unengineered ? $" (×{unengineered:0.##} sin los ingenieros del atacante)" : "") + " · " +
                      $"frente de {front} batallones, con hasta {front / 2} de artillería, aviación e ingenieros detrás", Theme.TextDim, FontSize.Small);
        y += 26;
        var last = battle.History.Count > 0 ? battle.History[^1] : default;
        var attackSide = ongoing
            ? new SideStats(attackers.Sum(u => u.Citizens), GameSession.AverageOrganisation(attackers), GameSession.ExpectedFire(attacking))
            : new SideStats(last.AttackerMen, last.AttackerOrganisation, last.AttackerFire);
        var defenseSide = ongoing
            ? new SideStats(defenders.Sum(u => u.Citizens), GameSession.AverageOrganisation(defenders), GameSession.ExpectedFire(defending))
            : new SideStats(last.DefenderMen, last.DefenderOrganisation, last.DefenderFire);

        FireBalance(new Rect(x, y, w, 18), ongoing ? "Fuego por hora" : "Fuego en la última hora", battle.AttackerId, attackSide.Fire, battle.DefenderId, defenseSide.Fire);
        y += 32;

        float chartHeight = battle.History.Count >= 2 ? 120 : 0;
        float columnsBottom = panel.Bottom - 64 - (chartHeight > 0 ? chartHeight + 34 : 0);
        float column = (w - 24) / 2;
        float left = y, right = y;
        BattleSide(x, ref left, column, columnsBottom, "Atacante", battle.AttackerId, attackSide, battle.AttackerLosses, attackers, attacking, true, ongoing);
        BattleSide(x + column + 24, ref right, column, columnsBottom, "Defensor", battle.DefenderId, defenseSide, battle.DefenderLosses, defenders, defending, false, ongoing);

        if (chartHeight > 0) OrganisationChart(battle, new Rect(x, columnsBottom + 30, w, chartHeight));
    }

    private readonly record struct SideStats(double Men, double Organisation, double Fire);

    /// <summary>A bar split by each side's fire this hour: who is winning the exchange.</summary>
    private void FireBalance(Rect r, string title, int attackerId, double attackFire, int defenderId, double defenseFire)
    {
        double total = attackFire + defenseFire;
        double share = total <= 0 ? 0.5 : attackFire / total;
        float split = r.W * (float)share;
        Batch.Rect(r.X - 1, r.Y - 1, r.W + 2, r.H + 2, Rgba.Black);
        Batch.Rect(r.X, r.Y, split, r.H, new Rgba(_session.Players[attackerId].Color));
        Batch.Rect(r.X + split, r.Y, r.W - split, r.H, new Rgba(_session.Players[defenderId].Color));
        Batch.Rect(r.X + r.W / 2 - 1, r.Y - 3, 2, r.H + 6, Theme.Text);
        string label = $"{title}: {attackFire:0.#} contra {defenseFire:0.#}";
        var box = new Rect(r.X + r.W / 2 - Ui.Font.Measure(label, FontSize.Small, true) / 2 - 8, r.Y, Ui.Font.Measure(label, FontSize.Small, true) + 16, r.H);
        Batch.Rect(box.X, box.Y, box.W, box.H, Rgba.Black.WithAlpha(0.6f));
        Ui.TextCentered(box, label, Theme.Text, FontSize.Small, bold: true);
        if (Ui.Hover(r))
            Ui.Tooltip("El fuego de los batallones que combaten, con sus armas combinadas y antes de la suerte (±" +
                       $"{MilitaryRules.CombatRandomness:P0}). Cada punto quita {MilitaryRules.OrganisationDamage:0.##} de organización " +
                       $"y {MilitaryRules.StrengthDamage:0.##} hombres al otro bando.");
    }

    private void BattleSide(float x, ref float y, float w, float bottom, string role, int playerId, SideStats side, double losses,
        List<Unit> units, List<GameSession.Engaged> engaged, bool attacking, bool ongoing)
    {
        var player = _session.Players[playerId];
        Batch.Rect(x, y + 3, 12, 12, new Rgba(player.Color));
        Ui.Text(x + 18, y, player.Name, Theme.Text, bold: true);
        Ui.Text(x + w - Ui.Font.Measure(role, FontSize.Small), y + 2, role, Theme.TextDim, FontSize.Small);
        y += 26;

        StatRow(x, ref y, w, "Hombres", $"{side.Men:N0}", Theme.Text);
        StatRow(x, ref y, w, "Bajas", $"{losses:N0}", losses > 0 ? Theme.Bad : Theme.TextDim);
        StatRow(x, ref y, w, "Organización", $"{side.Organisation:P0}", side.Organisation < 0.25 ? Theme.Bad : Theme.Text);
        Bar(new Rect(x, y, w, 6), side.Organisation, Theme.Organisation);
        // The point where a unit breaks: defenders retreat, attackers give up.
        Batch.Rect(x + w * (float)MilitaryRules.BreakingOrganisation, y - 2, 1, 10, Theme.Bad);
        y += 14;
        StatRow(x, ref y, w, "Fuego por hora", $"{side.Fire:0.#}  ({Casualties(side.Fire * MilitaryRules.StrengthDamage)} al enemigo)", Theme.Text);
        if (!ongoing)
        {
            y += 6;
            return;
        }

        int total = units.Sum(u => u.Battalions.Count);
        int support = engaged.Count(e => e.Exposure < 1);
        StatRow(x, ref y, w, "Combaten", $"{engaged.Count - support} en el frente, {support} detrás, {total - engaged.Count} en reserva", Theme.Text);
        StatRow(x, ref y, w, "Armas combinadas", $"+{GameSession.CombinedArms(engaged.Select(e => e.Role)):P0}", Theme.Text);
        y += 8;

        Ui.Text(x, y, units.Count == 1 ? "1 unidad" : $"{units.Count} unidades", Theme.Text, FontSize.Small, bold: true);
        y += 22;
        int shown = 0;
        foreach (var unit in units)
        {
            if (y + 44 > bottom) break;
            shown++;
            BattleUnitRow(unit, engaged, attacking, x, ref y, w);
        }
        if (shown < units.Count) Ui.Text(x, y, $"y {units.Count - shown} más", Theme.TextDim, FontSize.Small);
    }

    private static string Casualties(double men) => Math.Round(men) == 1 ? "1 baja" : $"{men:N0} bajas";

    private void StatRow(float x, ref float y, float w, string label, string value, Rgba color)
    {
        Ui.Text(x, y, label, Theme.TextDim, FontSize.Small);
        Ui.Text(x + w - Ui.Font.Measure(value, FontSize.Small), y, value, color, FontSize.Small);
        y += 19;
    }

    /// <summary>
    /// A unit in the fight: its name and officer, its men and how many of its battalions are in the line, with its
    /// strength and organisation bars. Hovering lists every battalion and where it stands.
    /// </summary>
    private void BattleUnitRow(Unit unit, List<GameSession.Engaged> engaged, bool attacking, float x, ref float y, float w)
    {
        var row = new Rect(x, y, w, 40);
        if (Ui.Hover(row)) Batch.Rect(row.X - 4, row.Y - 2, row.W + 8, row.H + 2, Theme.Highlight);
        var mine = engaged.Where(e => e.Unit == unit).ToList();
        Ui.Text(x, y, unit.Name, unit.OwnerId == Human.Id ? Theme.Accent : Theme.Text, FontSize.Small, bold: true);
        string leader = unit.Officer?.Title ?? "sin oficial";
        Ui.Text(x + w - Ui.Font.Measure(leader, FontSize.Small), y, leader, unit.Officer == null ? Theme.TextDim : Theme.Text, FontSize.Small);
        y += 18;
        bool supplied = _session.IsInSupply(unit);
        int count = unit.Battalions.Count;
        string fighting = mine.Count == count ? count == 1 ? "su batallón combate" : $"sus {count} batallones combaten"
            : mine.Count == 0 ? "todo en reserva" : $"combaten {mine.Count} de {count} batallones";
        string line = $"{unit.Citizens:N0} hombres · {fighting}" + (supplied ? "" : " · sin suministro");
        Ui.Text(x, y, line, supplied ? Theme.TextDim : Theme.Bad, FontSize.Small);
        y += 17;
        float half = (w - 6) / 2;
        Bar(new Rect(x, y, half, 4), unit.StrengthShare, Theme.Strength);
        Bar(new Rect(x + half + 6, y, half, 4), unit.OrganisationShare, Theme.Organisation);
        y += 10;
        if (Ui.Hover(row)) Ui.Tooltip(BattleUnitTooltip(unit, mine, attacking));
    }

    private string BattleUnitTooltip(Unit unit, List<GameSession.Engaged> mine, bool attacking)
    {
        var lines = new List<string> { unit.Name };
        double command = _session.CommandBonus(unit);
        if (command > 0) lines.Add($"Cadena de mando: +{command:P0}");
        if (_session.GeneralOf(unit) is { } general) lines.Add($"General: {general.Title} ({general.Summary})");
        if (unit.Officer is { } officer) lines.Add($"Oficial: {officer.Title} ({officer.Summary})");
        foreach (var b in unit.Battalions)
        {
            var e = mine.FirstOrDefault(m => m.Battalion == b);
            string where = e.Battalion == null ? "en reserva" : e.Exposure < 1 ? "detrás del frente" : "en el frente";
            string fire = e.Battalion == null ? "" : $" · fuego {e.Fire:0.#}";
            lines.Add($"{Formations.BattalionName(b.Info)}: {b.Strength:0}/{b.Info.Men} hombres · organización {b.OrganisationShare:P0} · " +
                      $"experiencia {b.Experience:P0} · {where}{fire}");
        }
        lines.Add(attacking ? "Ataca desde su provincia; entra cuando no quedan defensores." : "Defiende con la ventaja del terreno.");
        return string.Join("\n", lines);
    }

    /// <summary>Each side's organisation, hour by hour, with the line below which units break.</summary>
    private void OrganisationChart(Battle battle, Rect r)
    {
        Ui.Text(r.X, r.Y - 24, "Organización, hora a hora", Theme.Text, FontSize.Small, bold: true);
        Batch.Rect(r.X, r.Y, r.W, r.H, Rgba.Black.WithAlpha(0.45f));
        for (int i = 1; i < 4; i++) Batch.Rect(r.X, r.Y + r.H * i / 4, r.W, 1, Theme.Highlight);
        float breakY = r.Bottom - r.H * (float)MilitaryRules.BreakingOrganisation;
        Batch.Line(new(r.X, breakY), new(r.Right, breakY), Theme.Bad.WithAlpha(0.7f), 1);
        Ui.Text(r.Right - Ui.Font.Measure("se rompen", FontSize.Small) - 4, breakY - 17, "se rompen", Theme.Bad, FontSize.Small);

        var history = battle.History;
        int n = history.Count;
        System.Numerics.Vector2 Point(int i, double share) => new(r.X + r.W * i / (n - 1), r.Bottom - r.H * (float)Math.Clamp(share, 0, 1));
        var attackerColor = new Rgba(_session.Players[battle.AttackerId].Color);
        var defenderColor = new Rgba(_session.Players[battle.DefenderId].Color);
        // With long battles, only as many points as there are pixels.
        int step = Math.Max(1, n / (int)Math.Max(1, r.W / 2));
        for (int i = step; i < n; i += step)
        {
            int prev = i - step;
            Batch.Line(Point(prev, history[prev].AttackerOrganisation), Point(i, history[i].AttackerOrganisation), attackerColor, 2);
            Batch.Line(Point(prev, history[prev].DefenderOrganisation), Point(i, history[i].DefenderOrganisation), defenderColor, 2);
        }
        Ui.Text(r.X + 4, r.Bottom + 2, $"hace {GameSession.FormatHours(n - 1)}", Theme.TextDim, FontSize.Small);
        Ui.Text(r.Right - Ui.Font.Measure("ahora", FontSize.Small) - 4, r.Bottom + 2, "ahora", Theme.TextDim, FontSize.Small);

        if (!Ui.Hover(r)) return;
        int at = Math.Clamp((int)MathF.Round((Ui.Input.Mouse.X - r.X) / r.W * (n - 1)), 0, n - 1);
        Batch.Rect(Point(at, 0).X, r.Y, 1, r.H, Theme.Text.WithAlpha(0.5f));
        var h = history[at];
        Ui.Tooltip((at == n - 1 ? "Ahora" : $"Hace {GameSession.FormatHours(n - 1 - at)}") + "\n" +
                   $"{_session.Players[battle.AttackerId].Name}: {h.AttackerMen:N0} hombres, organización {h.AttackerOrganisation:P0}, fuego {h.AttackerFire:0.#}\n" +
                   $"{_session.Players[battle.DefenderId].Name}: {h.DefenderMen:N0} hombres, organización {h.DefenderOrganisation:P0}, fuego {h.DefenderFire:0.#}");
    }

    // ------------------------------------------------------------------ at sea

    private void NavalBattle(int provinceId, Rect panel)
    {
        float x = panel.X + 24, y = panel.Y + 20, w = panel.W - 48;
        var p = Map.Provinces[provinceId];
        Ui.Text(x, y, $"Batalla naval en {p.DisplayName}", Theme.Accent, FontSize.Large, bold: true);
        bool ongoing = _session.NavalBattleProvinces().Contains(provinceId);
        string state = ongoing ? "En curso" : "Terminada";
        Ui.Text(panel.Right - 24 - Ui.Font.Measure(state, FontSize.Normal, true), y + 8, state, ongoing ? Theme.Bad : Theme.Accent, bold: true);
        y += 40;
        Ui.Text(x, y, "Cada nación dispara sobre todos los barcos enemigos; las flotas rotas huyen a mar abierto o se hunden.", Theme.TextDim, FontSize.Small);
        y += 30;
        if (!ongoing)
        {
            Ui.Text(x, y, "Ya no quedan flotas enemigas frente a frente.", Theme.Text);
            return;
        }

        var sides = _session.Units.Where(u => u.IsFleet && u.ProvinceId == provinceId).GroupBy(u => u.OwnerId).ToList();
        float column = (w - 24 * (sides.Count - 1)) / Math.Max(1, sides.Count);
        float bottom = panel.Bottom - 64;
        for (int i = 0; i < sides.Count; i++)
        {
            float cx = x + i * (column + 24), cy = y;
            var fleets = sides[i].ToList();
            var player = _session.Players[sides[i].Key];
            Batch.Rect(cx, cy + 3, 12, 12, new Rgba(player.Color));
            Ui.Text(cx + 18, cy, player.Name, Theme.Text, bold: true);
            cy += 26;
            double organisation = GameSession.AverageOrganisation(fleets);
            StatRow(cx, ref cy, column, "Barcos", $"{fleets.Sum(f => f.Battalions.Count)}", Theme.Text);
            StatRow(cx, ref cy, column, "Tripulantes", $"{fleets.Sum(f => f.Citizens):N0}", Theme.Text);
            StatRow(cx, ref cy, column, "Organización", $"{organisation:P0}", organisation < 0.25 ? Theme.Bad : Theme.Text);
            Bar(new Rect(cx, cy, column, 6), organisation, Theme.Organisation);
            cy += 14;
            StatRow(cx, ref cy, column, "Fuego por hora", $"{fleets.Sum(GameSession.ExpectedNavalFire):0.#}", Theme.Text);
            cy += 8;
            foreach (var fleet in fleets)
            {
                if (cy + 44 > bottom) break;
                Ui.Text(cx, cy, fleet.Name, fleet.OwnerId == Human.Id ? Theme.Accent : Theme.Text, FontSize.Small, bold: true);
                cy += 18;
                int aboard = _session.CargoOf(fleet).Count();
                Ui.Text(cx, cy, $"{Formations.ShipCount(fleet.Battalions.Count)} · {fleet.Citizens:N0} tripulantes" + (aboard > 0 ? $" · {aboard} a bordo" : ""),
                    Theme.TextDim, FontSize.Small);
                cy += 17;
                float half = (column - 6) / 2;
                Bar(new Rect(cx, cy, half, 4), fleet.StrengthShare, Theme.Strength);
                Bar(new Rect(cx + half + 6, cy, half, 4), fleet.OrganisationShare, Theme.Organisation);
                cy += 10;
            }
        }
    }
}
