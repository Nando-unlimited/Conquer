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

public enum ProvinceTab
{
    General,
    Buildings,
    Army,
}

/// <summary>The army on screen: unit counters, battles, the unit panel and a city's Ejército tab.</summary>
public sealed partial class GameScreen
{
    private static readonly Rgba StrengthColor = new(0xFF6FBF5A);
    private static readonly Rgba OrganisationColor = new(0xFFE0B656);
    private static readonly Rgba BattleColor = new(0xFFE04A3A);

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
            var s = _camera.MapToScreen(pos);
            if (!OnScreen(s)) continue;
            int stack = stackIndex.GetValueOrDefault(unit.ProvinceId);
            stackIndex[unit.ProvinceId] = stack + 1;
            s += new Vector2(stack * 5, -stack * 7 - 14);

            bool selected = unit.Id == _selectedUnitId;
            if (selected || (unit.OwnerId == Human.Id && _camera.Zoom >= 1.5f)) DrawPath(unit, pos, selected);
            if (selected && _session.CommanderOf(unit) is { } hq)
                Batch.Line(s, _camera.MapToScreen(Center(hq.ProvinceId)), (_session.InCommandRange(unit) ? Theme.Good : Theme.Bad).WithAlpha(0.8f), 1.5f);
            if (unit.AttackingProvinceId is int target)
                PathArrow.Draw(Batch, [_camera.MapToScreen(pos), _camera.MapToScreen(Between(unit.ProvinceId, target, 1))], BattleColor, _realTime, 5);

            // Counters shrink when zoomed out so they don't bury the map.
            float scale = selected ? 1 : Math.Clamp(_camera.Zoom / 3, 0.45f, 1);
            float W = 28 * scale, H = 19 * scale;
            var r = new Rect(s.X - W / 2, s.Y - H / 2, W, H);
            var color = new Rgba(_session.Players[unit.OwnerId].Color);
            // A soft shadow lifts the counter off the map; the selected one's frame pulses.
            Batch.Shadow(r.X - 2, r.Y, r.W + 4, r.H + 4, 3, spread: 5, strength: 0.5f);
            if (selected)
            {
                float pulse = 0.5f + 0.5f * MathF.Sin((float)_realTime * 5);
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
                Bar(new Rect(r.X - 2, r.Bottom + 3, r.W + 4, 3), unit.StrengthShare, StrengthColor);
                Bar(new Rect(r.X - 2, r.Bottom + 7, r.W + 4, 3), unit.OrganisationShare, OrganisationColor);
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
        var s = _camera.MapToScreen(Center(provinceId));
        if (!OnScreen(s)) return;
        float size = 9 + 2 * (float)Math.Sin(_realTime * 6);
        Batch.Rect(s.X - size - 2, s.Y - size - 2, 2 * size + 4, 2 * size + 4, Rgba.Black.WithAlpha(0.6f));
        Batch.Line(new(s.X - size, s.Y - size), new(s.X + size, s.Y + size), BattleColor, 3);
        Batch.Line(new(s.X - size, s.Y + size), new(s.X + size, s.Y - size), BattleColor, 3);
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

    // ------------------------------------------------------------------ unit panel

    private void UnitPanel(Unit unit, float x, ref float y, float w)
    {
        var owner = _session.Players[unit.OwnerId];
        var here = Map.Provinces[unit.ProvinceId];
        Ui.Text(x, y, unit.Name, Theme.Accent, FontSize.Large, bold: true);
        y += 32;
        string kind = unit.Type switch
        {
            UnitType.Regiment => $"{Formations.CombatName(unit.Battalions.Count)} de {Formations.BattalionCount(unit.Battalions.Count)}",
            UnitType.Headquarters => $"Cuartel general de {Formations.LevelName(unit.HeadquartersLevel).ToLowerInvariant()}",
            UnitType.Fleet => $"Flota de {Formations.ShipCount(unit.Battalions.Count)}",
            _ => $"{unit.Citizens:N0} colonos",
        };
        Ui.Text(x, y, kind, Theme.TextDim, FontSize.Small);
        y += 24;
        Line(x, ref y, "Nación", owner.Name, new Rgba(owner.Color));
        Line(x, ref y, "Ubicación", _session.PlaceName(here));
        Line(x, ref y, "Estado", UnitState(unit, out var stateColor), stateColor);

        if (unit.IsMilitary || unit.IsFleet) RegimentDetails(unit, x, ref y, w);
        else if (unit.IsHeadquarters) HeadquartersDetails(unit, x, ref y, w);
        if (unit.OwnerId != Human.Id) return;

        y += 6;
        if (unit.IsAboard)
        {
            Paragraph(x, ref y, w, "Clic derecho en la costa junto a su flota (o en su puerto) para desembarcar. En tierra enemiga sin tropas, desembarcar la ocupa.", Theme.TextDim);
            return;
        }
        if (unit.IsMilitary || unit.IsFleet || unit.IsHeadquarters)
        {
            if (Ui.Button(new Rect(x, y, w, 32), "Editar unidad", tooltip: "Renombrar, separar y unir tropas, y elegir su oficial."))
                OpenUnitEditor(unit);
            y += 38;
        }
        EmbarkButtons(unit, x, ref y, w);
        float half = (w - 6) / 2;
        if (unit.CanFoundCity)
        {
            var can = _session.CanFoundCity(unit);
            if (Ui.Button(new Rect(x, y, w, 32), "Fundar ciudad", can.Ok, tooltip: can.Ok ? "Reclama esta provincia y funda una ciudad con estos colonos." : can.Message))
                OpenCityNaming(unit.Id, unit.ProvinceId);
            y += 38;
        }
        if (unit.IsMilitary)
        {
            var can = _session.CanClaim(unit);
            if (Ui.Button(new Rect(x, y, w, 32), "Reclamar provincia", can.Ok, tooltip: can.Ok ? "Esta provincia libre pasará a ser tuya." : can.Message))
                Show(_session.Claim(Human.Id, unit.Id));
            y += 38;
            if (unit.IsScouting)
            {
                string label = unit.AutoClaim ? "Dejar de explorar" : "Explorar y reclamar";
                string tip = unit.AutoClaim ? "Se detiene donde está y vuelve a esperar órdenes."
                    : "Va sola a la mejor provincia libre junto a tus fronteras, la reclama y sigue con la siguiente, sin entrar en tierras ajenas. " +
                      "Darle una orden de movimiento la detiene.";
                if (Ui.Button(new Rect(x, y, w, 32), label, active: unit.AutoClaim, tooltip: tip))
                {
                    if (unit.AutoClaim) _session.MoveUnit(Human.Id, unit.Id, unit.ProvinceId);
                    Show(_session.SetAutoClaim(Human.Id, unit.Id, !unit.AutoClaim));
                }
                y += 38;
            }
            EngineerButtons(unit, here, x, ref y, w);
        }
        bool canSettle = here.OwnerId == Human.Id && !here.IsOccupied;
        if (Ui.Button(new Rect(x, y, half, 32), unit.CanFoundCity ? "Asentarse" : "Licenciar", canSettle,
                tooltip: canSettle ? "Disuelve la unidad; sus ciudadanos se quedan a vivir en esta provincia." : "Solo en una provincia tuya.", size: FontSize.Small))
        {
            Show(_session.Disband(Human.Id, unit.Id));
            _selectedProvince = here.Id;
            return;
        }
        if (Ui.Button(new Rect(x + half + 6, y, half, 32), "Detener", unit.IsMoving || unit.AttackingProvinceId.HasValue || unit.AutoClaim, size: FontSize.Small))
        {
            if (unit.AutoClaim) _session.SetAutoClaim(Human.Id, unit.Id, false);
            _session.MoveUnit(Human.Id, unit.Id, unit.ProvinceId);
        }
        y += 40;
        Paragraph(x, ref y, w, unit.IsFleet
            ? "Clic derecho para navegar: por mares costeros con Navegación a vela y por el océano con Cartografía; atraca en tus ciudades con costa. Las flotas enemigas que se encuentran combaten."
            : unit.IsMilitary
            ? "Clic derecho para mover. Mover a una provincia enemiga con tropas la ataca; sin tropas, la ocupa. Solo se entra en tierras de naciones con las que estás en guerra. Para cruzar el mar, clic derecho sobre una flota tuya con transportes."
            : "Clic derecho para mover. No puede entrar en tierras de otras naciones. Para cruzar el mar, clic derecho sobre una flota tuya con transportes.", Theme.TextDim);
    }

    /// <summary>
    /// For a unit with engineers: a button for each kind of road its advances allow, which opens the window to choose
    /// where it goes (only from a city or HQ of the player's), and the works under way along whose route it stands.
    /// </summary>
    private void EngineerButtons(Unit unit, Province here, float x, ref float y, float w)
    {
        if (!unit.Battalions.Any(b => b.Type == BattalionType.Engineers)) return;
        bool hub = _session.IsRoadHub(Human.Id, here.Id);
        foreach (var kind in RoadKinds.All.Where(k => Human.Techs.Contains(k.Info().Requires)))
        {
            var info = kind.Info();
            bool can = hub && !unit.IsMoving;
            string tip = !hub ? "Solo desde una provincia con una ciudad o un cuartel general tuyos."
                : unit.IsMoving ? "La unidad está en marcha."
                : $"Elige la ciudad o el cuartel general que quieres unir con {(info.Feminine ? "una" : "un")} {info.Name.ToLowerInvariant()}.";
            if (Ui.Button(new Rect(x, y, w, 32), $"Construir {info.Name.ToLowerInvariant()}...", can, tooltip: tip, size: FontSize.Small))
                OpenRoadWindow(here.Id, kind);
            y += 38;
        }
        foreach (var work in _session.RoadProjects.Where(r => r.OwnerId == Human.Id && r.Route.Contains(here.Id)).ToList())
        {
            var info = work.Kind.Info();
            Ui.Text(x, y, $"{info.Name} a {HubName(work.To)}", Theme.Accent, FontSize.Small, bold: true);
            y += 18;
            int engineers = _session.EngineersOn(work);
            Ui.Text(x, y, $"Quedan {_session.LinksLeft(work)} tramos · {Formations.BattalionCount(engineers)} de ingenieros en la ruta", Theme.TextDim, FontSize.Small);
            y += 20;
            if (Ui.Button(new Rect(x, y, w, 26), "Cancelar obra", size: FontSize.Small, tooltip: "Se devuelve lo que costaban los tramos sin hacer."))
                Show(_session.CancelRoad(Human.Id, work.Id));
            y += 32;
        }
    }

    /// <summary>Buttons to board one of the player's fleets with room, in this province or the sea next to it.</summary>
    private void EmbarkButtons(Unit unit, float x, ref float y, float w)
    {
        if (unit.IsFleet) return;
        var here = Map.Provinces[unit.ProvinceId];
        var fleets = _session.Units.Where(f => f.IsFleet && f.OwnerId == unit.OwnerId && f.Capacity > 0
                                               && (f.ProvinceId == here.Id || here.Neighbors.Contains(f.ProvinceId))).Take(3);
        foreach (var fleet in fleets)
        {
            var can = _session.CanEmbark(unit, fleet);
            int room = fleet.Capacity - _session.CargoMen(fleet);
            if (Ui.Button(new Rect(x, y, w, 28), $"Embarcar en {fleet.Name} (sitio para {room:N0})", can.Ok, tooltip: can.Ok ? null : can.Message, size: FontSize.Small))
                Show(_session.Embark(Human.Id, unit.Id, fleet.Id));
            y += 32;
        }
    }

    private string UnitState(Unit unit, out Rgba color)
    {
        color = Theme.Text;
        if (unit.CarrierId is int carrier && _session.UnitById(carrier) is { } fleet) return $"A bordo de {fleet.Name}";
        if (unit.IsFleet && _session.EnemyFleetsIn(unit.ProvinceId, unit.OwnerId).Any())
        {
            color = BattleColor;
            return "Combatiendo en el mar";
        }
        if (unit.AttackingProvinceId is int target)
        {
            color = BattleColor;
            var p = Map.Provinces[target];
            return $"Atacando {_session.PlaceName(p)}";
        }
        if (_session.InBattle(unit))
        {
            color = BattleColor;
            return "Defendiendo";
        }
        if (unit.IsMoving && unit.Destination is int dest)
        {
            double hours = unit.HoursToNext;
            for (int i = 0; i + 1 < unit.Path.Count; i++) hours += _session.Pathfinder.StepHours(unit.Path[i], unit.Path[i + 1]) / unit.Speed;
            var p = Map.Provinces[dest];
            return (unit.AutoClaim ? "Explorando hacia " : "Hacia ") + $"{_session.PlaceName(p)} ({GameSession.FormatHours(hours)})";
        }
        return unit.AutoClaim ? "Explorando" : "Esperando órdenes";
    }

    /// <summary>A regiment's battalions, or a fleet's ships and cargo (split and merged in the unit editor).</summary>
    private void RegimentDetails(Unit unit, float x, ref float y, float w)
    {
        if (unit.IsFleet)
        {
            Line(x, ref y, "Velocidad en el mar", $"{unit.Speed * GameRules.SailingSpeed * GameRules.CitizenSpeedKmh:0.#} km/h");
            bool port = _session.IsPort(Map.Provinces[unit.ProvinceId], unit.OwnerId);
            Line(x, ref y, "Reparaciones", port ? "En puerto" : "Solo en puerto", port ? Theme.Good : Theme.TextDim);
            if (unit.Capacity > 0)
            {
                Line(x, ref y, "Carga", $"{_session.CargoMen(unit):N0} / {unit.Capacity:N0} hombres");
                foreach (var cargo in _session.CargoOf(unit))
                {
                    Ui.Text(x + 8, y, $"{cargo.Name} ({cargo.Citizens:N0})", Theme.Text, FontSize.Small);
                    y += 18;
                }
            }
        }
        else
        {
            bool supplied = _session.IsInSupply(unit) || unit.IsAboard;
            Line(x, ref y, "Suministro", unit.IsAboard ? "Víveres del barco" : supplied ? "Con suministro" : "Sin suministro", supplied ? Theme.Good : Theme.Bad);
            Line(x, ref y, "Velocidad", $"{unit.Speed * GameRules.CitizenSpeedKmh:0.#} km/h");
            CommandLine(unit, x, ref y);
            OfficerLine("Oficial", unit.Officer, "Manda esta unidad.", x, ref y, w);
            OfficerLine("General", _session.GeneralOf(unit), "Manda las unidades de su cuartel general que estén a su alcance.", x, ref y, w);
            if (unit.Battalions.Count > 0)
            {
                double xp = unit.Battalions.Sum(b => b.Experience * b.Strength) / Math.Max(1, unit.Battalions.Sum(b => b.Strength));
                Line(x, ref y, "Experiencia", $"{Battalion.ExperienceName(xp)} ({xp:P0})");
                if (Ui.Hover(new Rect(x, y - 24, w, 22)))
                    Ui.Tooltip($"Cada batallón gana experiencia combatiendo: hasta +{MilitaryRules.ExperienceBonus:P0} de fuego.\nLos reclutas nuevos la diluyen.");
            }
        }
        y += 4;

        foreach (var b in unit.Battalions)
        {
            MapIcons.Battalion(Batch, x, y + 1, b.Type);
            Ui.Text(x + 26, y, Formations.BattalionName(b.Info), Theme.Text, FontSize.Small, bold: true);
            string men = $"{b.Strength:0}/{b.Info.Men}";
            Ui.Text(x + w - Ui.Font.Measure(men, FontSize.Small), y, men, Theme.TextDim, FontSize.Small);
            var row = new Rect(x, y, w, 30);
            y += 19;
            Bar(new Rect(x, y, w, 4), b.StrengthShare, StrengthColor);
            Bar(new Rect(x, y + 5, w, 4), b.OrganisationShare, OrganisationColor);
            if (Ui.Hover(row))
                Ui.Tooltip($"{b.Info.Name}: ataque {b.Info.Attack:0.#}, defensa {b.Info.Defense:0.#}, organización {b.Organisation:0}/{b.Info.MaxOrganisation:0}" +
                           $"\n{b.Type.Role().Name()}, {Battalion.ExperienceName(b.Experience).ToLowerInvariant()} ({b.Experience:P0} de experiencia)" +
                           (b.Info.Mounted ? "\nMontada: rápida, pero ataca a la mitad en bosques, pantanos y montañas." : "") +
                           (b.Info.Capacity > 0 ? $"\nLleva {b.Info.Capacity:N0} hombres." : ""));
            y += 16;
        }
        if (unit.OwnerId != Human.Id || unit.IsAboard) return;
        if (!unit.IsFleet) AttachButtons(unit, x, ref y, w);
    }

    private void HeadquartersDetails(Unit hq, float x, ref float y, float w)
    {
        var info = CommandLevels.Info(hq.HeadquartersLevel);
        Line(x, ref y, "Alcance", $"{info.RangeKm:N0} km");
        CommandLine(hq, x, ref y);
        OfficerLine("General", hq.Officer, "Manda las unidades de su cuartel general que estén a su alcance.", x, ref y, w);
        var subs = _session.SubordinatesOf(hq).ToList();
        string below = Formations.SubordinatesPlural(hq.HeadquartersLevel);
        Ui.Text(x, y, $"Al mando ({subs.Count}/{info.MaxSubordinates} {below})", Theme.Text, bold: true);
        y += 24;
        foreach (var sub in subs)
        {
            bool inRange = _session.InCommandRange(sub);
            Ui.Text(x + 8, y, sub.Name, inRange ? Theme.Text : Theme.Bad, FontSize.Small);
            if (!inRange) Ui.Text(x + w - Ui.Font.Measure("fuera de alcance", FontSize.Small), y, "fuera de alcance", Theme.Bad, FontSize.Small);
            y += 20;
        }
        if (subs.Count == 0)
        {
            Ui.Text(x + 8, y, "Nadie todavía: asígnale unidades desde su panel.", Theme.TextDim, FontSize.Small);
            y += 20;
        }
        if (hq.OwnerId == Human.Id) AttachButtons(hq, x, ref y, w);
    }

    /// <summary>An officer leading the unit, or its HQ's general, with their traits and what they do on hover.</summary>
    private void OfficerLine(string label, Officer? officer, string role, float x, ref float y, float w)
    {
        if (officer == null)
        {
            Line(x, ref y, label, "Ninguno", Theme.TextDim);
            return;
        }
        // Just the name and stars fit; the traits are in the tooltip, and a flawed officer is not shown in green.
        Line(x, ref y, label, $"{officer.Name} {new string('*', officer.Skill)}", officer.Traits.Any(Officer.IsFlaw) ? Theme.Text : Theme.Good);
        if (Ui.Hover(new Rect(x, y - 24, w, 22))) Ui.Tooltip($"{OfficerTooltip(officer)}\n{role}");
    }

    /// <summary>Who the unit reports to, whether that HQ is in range, and the bonus it gives.</summary>
    private void CommandLine(Unit unit, float x, ref float y)
    {
        if (_session.CommanderOf(unit) is not { } hq)
        {
            Line(x, ref y, "Mando", unit.HeadquartersLevel >= CommandLevels.Highest ? "Mando supremo" : "Sin cuartel general", Theme.TextDim);
            return;
        }
        bool inRange = _session.InCommandRange(unit);
        double bonus = _session.CommandBonus(unit);
        Line(x, ref y, "Mando", inRange ? $"{hq.Name} (+{bonus:P0})" : $"{hq.Name} (lejos)", inRange ? Theme.Good : Theme.Bad);
    }

    /// <summary>Buttons to put the unit under one of the nearest HQs of the level above, or to leave its HQ.</summary>
    private void AttachButtons(Unit unit, float x, ref float y, float w)
    {
        if (unit.CommandLevel >= CommandLevels.Highest) return;
        var here = Map.Provinces[unit.ProvinceId];
        var hqs = _session.Units.Where(u => u.IsHeadquarters && u.OwnerId == unit.OwnerId && u.HeadquartersLevel == unit.CommandLevel + 1 && u.Id != unit.CommanderId)
            .OrderBy(u => Map.DistanceKm(here, Map.Provinces[u.ProvinceId])).Take(3).ToList();
        foreach (var hq in hqs)
        {
            var can = _session.CanAttach(unit, hq);
            double km = Map.DistanceKm(here, Map.Provinces[hq.ProvinceId]);
            if (Ui.Button(new Rect(x, y, w, 26), $"Bajo el mando de {hq.Name} ({km:N0} km)", can.Ok, tooltip: can.Ok ? null : can.Message, size: FontSize.Small))
                Show(_session.Attach(Human.Id, unit.Id, hq.Id));
            y += 30;
        }
        if (unit.CommanderId.HasValue && Ui.Button(new Rect(x, y, w, 26), "Quitar del mando", size: FontSize.Small))
            Show(_session.Detach(Human.Id, unit.Id));
        if (unit.CommanderId.HasValue) y += 30;
        if (hqs.Count == 0 && unit.CommanderId is null)
        {
            Ui.Text(x, y, $"Forma un cuartel de {Formations.LevelName(unit.CommandLevel + 1).ToLowerInvariant()} en una ciudad para darle mando.", Theme.TextDim, FontSize.Small);
            y += 22;
        }
    }

    // ------------------------------------------------------------------ province: Ejército tab

    /// <summary>Battalions the province (its city, its barracks or its workshop) can train (those of undiscovered advances are not listed), HQs, and what is in training.</summary>
    private void ArmyPanel(Province p, float x, ref float y, float w)
    {
        // The training buildings it lacks, of those the player can build: barracks always, the workshop once siege engines are known.
        var lacking = new[] { BuildingType.Barracks, BuildingType.Workshop }.Where(b => !p.Has(b) && IsBuildingKnown(b)).Select(b => b.For(Human)).ToList();
        if (lacking.Count > 0)
        {
            var reasons = lacking.Select(b => b == BuildingType.Barracks
                ? "Sin cuartel no entrena infantería ni caballería."
                : $"Sin {b.Info().Name.ToLowerInvariant()} no construye máquinas de guerra (catapultas, cañones, artillería, tanques, bombarderos).");
            string text = string.Join(" ", reasons) + (lacking.Count > 1 ? " Constrúyelos" : lacking[0] == BuildingType.Factory ? " Constrúyela" : " Constrúyelo") + " en la pestaña Edificios.";
            foreach (var line in Ui.Font.Wrap(text, w, FontSize.Small))
            {
                Ui.Text(x, y, line, Theme.Bad, FontSize.Small);
                y += Ui.Font.LineHeight(FontSize.Small);
            }
            y += 8;
        }
        Ui.Text(x, y, $"Entrenar {Formations.CombatPlural}", Theme.Text, bold: true);
        y += 26;
        foreach (var template in Human.Templates.Take(4))
        {
            var can = _session.CanTrainTemplate(p, template);
            int days = GameSession.TrainingDays(Human, template);
            string tip = $"{template.Name}: {template.Composition}.\n{template.Men} hombres de la provincia. Ataque {template.Attack:0.#}, defensa {template.Defense:0.#}." +
                         $"\nCoste: {template.Cost}. {TextFormat.TrainingDaysText(days, template.TrainingDays)}" + (can.Ok ? "" : "\n" + can.Message);
            if (Ui.Button(new Rect(x, y, w, 28), $"{template.Name}  ·  {Formations.BattalionCount(template.Battalions.Count)}  ·  {days} d",
                    can.Ok, tooltip: tip, size: FontSize.Small))
                Show(_session.TrainTemplate(Human.Id, p.Id, template.Id));
            y += 32;
        }
        Ui.Text(x, y, Human.Templates.Count > 4 ? "Más plantillas en la pestaña Plantillas de la nación (N)." : "Diseña plantillas en la pestaña Plantillas de la nación (N).",
            Theme.TextDim, FontSize.Small);
        y += 28;

        string plural = "batallones";
        Ui.Text(x, y, $"Entrenar {plural} sueltos", Theme.Text, bold: true);
        y += 26;
        foreach (var type in Battalions.All.Where(t => t.Info().Requires.All(Human.Techs.Contains)))
        {
            var info = type.Info();
            var can = _session.CanTrain(p, type);
            int days = GameSession.TrainingDays(Human, type);
            string tip = $"{Formations.BattalionName(info)}: {info.Men} hombres de la provincia. Ataque {info.Attack:0.#}, defensa {info.Defense:0.#}, " +
                         $"organización {info.MaxOrganisation:0}, {info.Speed * GameRules.CitizenSpeedKmh:0.#} km/h." +
                         (info.Mounted ? "\nMontada: ataca a la mitad en bosques, pantanos y montañas." : "") +
                         $"\nCoste: {info.Cost}. {TextFormat.TrainingDaysText(days, info.TrainingDays)} Mantenimiento: {TextFormat.UpkeepText([info.Cost])}." + (can.Ok ? "" : "\n" + can.Message);
            if (Ui.Button(new Rect(x, y, w, 28), $"{info.Name}  ·  {info.Cost}  ·  {days} d", can.Ok, tooltip: tip, size: FontSize.Small))
                Show(_session.Train(Human.Id, p.Id, type));
            MapIcons.Battalion(Batch, x + 7, y + 7, type);
            y += 32;
        }

        y += 8;
        Ui.Text(x, y, "Cuarteles generales", Theme.Text, bold: true);
        y += 26;
        foreach (var level in CommandLevels.All)
        {
            var can = _session.CanRaiseHeadquarters(p, level.Level);
            string tip = $"Manda hasta {level.MaxSubordinates} {Formations.SubordinatesPlural(level.Level)} a menos de {level.RangeKm:N0} km: " +
                         $"+{MilitaryRules.CommandBonus:P0} en combate y recuperación (+{MilitaryRules.HigherCommandBonus:P0} por cada nivel superior enlazado)." +
                         $"\n{level.Staff} hombres de la provincia. Coste: {level.Cost}. Tarda {level.TrainingDays} días." + (can.Ok ? "" : "\n" + can.Message);
            if (Ui.Button(new Rect(x, y, w, 28), $"{Formations.LevelName(level.Level)}  ·  {level.Cost}  ·  {level.TrainingDays} d", can.Ok, tooltip: tip, size: FontSize.Small))
                Show(_session.RaiseHeadquarters(Human.Id, p.Id, level.Level));
            y += 32;
        }

        if (p.Training.Count == 0) return;
        y += 8;
        Ui.Text(x, y, "En instrucción", Theme.Text, bold: true);
        y += 26;
        foreach (var order in p.Training)
        {
            Ui.Text(x, y, order.Name, Theme.Text, FontSize.Small);
            string days = $"{order.DaysLeft} d";
            Ui.Text(x + w - Ui.Font.Measure(days, FontSize.Small), y, days, Theme.TextDim, FontSize.Small);
            y += 18;
            Bar(new Rect(x, y, w, 5), 1 - order.DaysLeft / (double)order.TotalDays, Theme.Accent);
            y += 12;
        }
    }
}
