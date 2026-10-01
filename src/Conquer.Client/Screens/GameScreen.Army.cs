using System.Numerics;
using Conquer.Client.Graphics;
using Conquer.Client.UI;
using Conquer.Game.Buildings;
using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Science;
using Conquer.Game.Simulation;
using Conquer.Game.World;
using Conquer.Presentation;

namespace Conquer.Client.Screens;

/// <summary>The army on the map: unit counters and battles.</summary>
public sealed partial class GameScreen
{
    /// <summary>
    /// NATO-style counters: the symbol of the unit's arm inside the frame (see <see cref="UnitFunction"/>), its size
    /// marks above it (III regiment, X brigade, XX division, XXX to XXXXX for HQs), "HQ" inside an HQ's frame
    /// and a "C" for settlers. Combat units carry a strength bar (green) and an organisation bar (amber).
    /// </summary>
    private void DrawUnits()
    {
        _unitHitBoxes.Clear();
        var stackIndex = new Dictionary<int, int>();
        foreach (var unit in _session.Units)
        {
            if (unit.IsAboard) continue; // shown in its fleet's panel
            var pos =unit.IsMoving && !unit.AttackingProvinceId.HasValue ? Between(unit.ProvinceId, unit.Path[0], unit.StepProgress) : Center(unit.ProvinceId);
            var s = _game.Camera.MapToScreen(pos);
            if (!OnScreen(s)) continue;
            int stack = stackIndex.GetValueOrDefault(unit.ProvinceId);
            stackIndex[unit.ProvinceId] = stack + 1;
            s += new Vector2(stack * 5, -stack * 7 - 14);

            bool selected = unit.Id == _game.SelectedUnitId;
            if (selected || (unit.OwnerId == Human.Id && _game.Camera.Zoom >= 1.5f)) DrawPath(unit, pos, selected);
            if (selected && _session.CommanderOf(unit) is { } hq)
                Batch.Line(s, _game.Camera.MapToScreen(Center(hq.ProvinceId)), (_session.InCommandRange(unit) ? Theme.Good : Theme.Bad).WithAlpha(0.8f), 1.5f);
            if (unit.AttackingProvinceId is int target)
                PathArrow.Draw(Batch, [_game.Camera.MapToScreen(pos), _game.Camera.MapToScreen(Between(unit.ProvinceId, target, 1))], Theme.Battle, _game.Now, 5);

            // Counters shrink when zoomed out so they don't bury the map.
            float scale = selected ? 1 : Math.Clamp(_game.Camera.Zoom / 3, 0.45f, 1);
            float W = 28 * scale, H = 19 * scale;
            var r = new Rect(s.X - W / 2, s.Y - H / 2, W, H);
            var color = new Rgba(_session.Players[unit.OwnerId].Color);
            // A soft shadow lifts the counter off the map; the selected one's frame pulses.
            Batch.Shadow(r.X - 2, r.Y, r.W + 4, r.H + 4, 3, spread: 5, strength: 0.5f);
            if (selected)
            {
                float pulse = 0.5f + 0.5f * MathF.Sin((float)_game.Now * 5);
                Batch.Rect(r.X - 4, r.Y - 4, r.W + 8, r.H + 8, Theme.Accent.WithAlpha(0.25f + 0.35f * pulse));
            }
            Batch.Rect(r.X - 2, r.Y - 2, r.W + 4, r.H + 4, selected ? Theme.Accent : Rgba.Black);
            Batch.Rect(r.X, r.Y, r.W, r.H, color.Scale(0.55f).WithAlpha(1));
            Batch.Rect(r.X + 2, r.Y + 2, r.W - 4, r.H - 4, color);
            if (unit.IsMilitary) MapIcons.NatoSymbol(Batch, r.X + 2, r.Y + 2, r.W - 4, r.H - 4, unit.Function);
            else if (unit.IsFleet)
            {
                // A hull under the ship letter; a dot for every unit aboard.
                Batch.Line(new(r.X + 3, r.Bottom - 4), new(r.Right - 3, r.Bottom - 4), Rgba.Black, 2);
                Batch.Line(new(r.X + 3, r.Bottom - 4), new(r.X + 7, r.Bottom - 1), Rgba.Black, 1.5f);
                Batch.Line(new(r.Right - 3, r.Bottom - 4), new(r.Right - 7, r.Bottom - 1), Rgba.Black, 1.5f);
                if (scale > 0.7f) Ui.TextCentered(new Rect(r.X, r.Y - 2, r.W, r.H - 4), unit.Symbol, Rgba.Black, FontSize.Small, bold: true);
                int aboard = _session.CargoOf(unit).Count();
                for (int i = 0; i < aboard; i++) Batch.Rect(r.Right + 3, r.Y + i * 5, 3, 3, Rgba.White);
            }
            if (unit.IsMilitary || unit.IsFleet)
            {
                Bar(new Rect(r.X - 2, r.Bottom + 3, r.W + 4, 3), unit.StrengthShare, Theme.Strength);
                Bar(new Rect(r.X - 2, r.Bottom + 7, r.W + 4, 3), unit.OrganisationShare, Theme.Organisation);
            }
            else if (unit.IsHeadquarters) { if (scale > 0.7f) Ui.TextCentered(r, "HQ", Rgba.Black, FontSize.Small, bold: true); }
            else MapIcons.Settlers(Batch, r.X + 2, r.Y + 2, r.W - 4, r.H - 4);
            if (unit.Echelon.Length > 0) DrawEchelon(r, unit.Echelon, scale);
            _unitHitBoxes.Add((unit.Id, r));
        }
    }

    /// <summary>NATO size marks centred over the frame: a bar for each "I", a small cross for each "X".</summary>
    private void DrawEchelon(Rect r, string marks, float scale)
    {
        float h = 7 * scale, w = 5 * scale, gap = 3 * scale;
        float total = marks.Sum(c => c == 'I' ? 0 : w) + (marks.Length - 1) * gap;
        float x = r.X + (r.W - total) / 2, bottom = r.Y - 4, top = bottom - h;
        Batch.Rect(x - 2, top - 2, total + 4, h + 4, Rgba.Black.WithAlpha(0.45f));
        foreach (char c in marks)
        {
            if (c == 'I') Batch.Line(new(x, top), new(x, bottom), Rgba.White, 1.5f);
            else
            {
                Batch.Line(new(x, top), new(x + w, bottom), Rgba.White, 1.5f);
                Batch.Line(new(x, bottom), new(x + w, top), Rgba.White, 1.5f);
                x += w;
            }
            x += gap;
        }
    }

    /// <summary>Crossed swords over every province being fought for, on land or at sea; hovering shows both sides.</summary>
    private void DrawBattles()
    {
        _battleHitBoxes.Clear();
        foreach (var battle in _session.Battles) DrawBattleMark(battle.ProvinceId, battle, () => BattleSummary(battle));
        foreach (int sea in _session.NavalBattleProvinces()) DrawBattleMark(sea, null, () => NavalBattleSummary(sea));
    }

    /// <summary>The crossed swords: hovering shows a summary, clicking opens the battle's window.</summary>
    private void DrawBattleMark(int provinceId, Battle? battle, Func<string> summary)
    {
        var s = _game.Camera.MapToScreen(Center(provinceId));
        if (!OnScreen(s)) return;
        float size = 9 + 2 * (float)Math.Sin(_game.Now * 6);
        Batch.Rect(s.X - size - 2, s.Y - size - 2, 2 * size + 4, 2 * size + 4, Rgba.Black.WithAlpha(0.6f));
        Batch.Line(new(s.X - size, s.Y - size), new(s.X + size, s.Y + size), Theme.Battle, 3);
        Batch.Line(new(s.X - size, s.Y + size), new(s.X + size, s.Y - size), Theme.Battle, 3);
        var bounds = new Rect(s.X - 11, s.Y - 11, 22, 22);
        _battleHitBoxes.Add((provinceId, battle, bounds));
        if (Ui.Hover(bounds)) Ui.Tooltip(summary() + "\nClic para ver la batalla en detalle.");
    }

    private string NavalBattleSummary(int provinceId)
    {
        var sides = _session.Units.Where(u => u.IsFleet && u.ProvinceId == provinceId).GroupBy(u => u.OwnerId)
            .Select(g => $"{_session.Players[g.Key].Name}: {Formations.ShipCount(g.Sum(f => f.Battalions.Count))} · " +
                         $"organización {g.Average(f => f.OrganisationShare):P0}");
        return $"Batalla naval en {Map.Provinces[provinceId].DisplayName}\n" + string.Join("\n", sides);
    }

    private string BattleSummary(Battle battle)
    {
        var attackers = battle.Attackers.Select(_session.UnitById).OfType<Unit>().ToList();
        var defenders = _session.EnemyRegimentsIn(battle.ProvinceId, battle.AttackerId).ToList();
        var p = Map.Provinces[battle.ProvinceId];
        bool engineers = GameSession.HasEngineers(attackers);
        string Side(string role, int player, List<Unit> units, bool attacking)
        {
            var engaged = _session.Engage(units, p, attacking, engineers);
            int total = units.Sum(u => u.Battalions.Count);
            return $"{role}: {_session.Players[player].Name} · {units.Count} u. · {units.Sum(u => u.Citizens):N0} hombres · " +
                   $"organización {(units.Count == 0 ? 0 : units.Average(u => u.OrganisationShare)):P0}\n" +
                   $"   combaten {engaged.Count} de {total} batallones · armas combinadas +{GameSession.CombinedArms(engaged.Select(e => e.Role)):P0}";
        }
        return $"Batalla por {_session.PlaceName(p)} ({GameSession.FormatHours(_session.Date.Hours - battle.StartHours)})\n" +
               Side("Atacante", battle.AttackerId, attackers, true) + "\n" + Side("Defensor", battle.DefenderId, defenders, false) +
               $"\nFrente: {MilitaryRules.FrontWidth(p.Biome)} batallones en primera línea, más artillería, aviación e ingenieros detrás" +
               $"\nDefensa por el terreno{(p.HasRiver ? " y el río" : "")}: ×{MilitaryRules.DefenseMultiplier(p, engineers):0.##}" +
               (engineers && MilitaryRules.DefenseMultiplier(p, true) < MilitaryRules.DefenseMultiplier(p) ? $" (×{MilitaryRules.DefenseMultiplier(p):0.##} sin los ingenieros del atacante)" : "");
    }

    private void Bar(Rect r, double share, Rgba color)
    {
        Batch.Rect(r.X, r.Y, r.W, r.H, Rgba.Black.WithAlpha(0.7f));
        Batch.Rect(r.X, r.Y, r.W * (float)Math.Clamp(share, 0, 1), r.H, color);
    }
}
