using System.Numerics;
using Conquer.Client.Graphics;
using Conquer.Client.UI;
using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Science;
using Conquer.Game.Simulation;

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
    /// NATO-style counters: a cross for infantry, a slash for mounted troops, the level marks for HQs
    /// and a "C" for settlers. Regiments carry a strength bar (green) and an organisation bar (amber).
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
            if (selected)
            {
                DrawPath(unit, s);
                if (_session.CommanderOf(unit) is { } hq)
                    Batch.Line(s, _camera.MapToScreen(Center(hq.ProvinceId)), (_session.InCommandRange(unit) ? Theme.Good : Theme.Bad).WithAlpha(0.8f), 1.5f);
            }
            if (unit.AttackingProvinceId is int target)
                Batch.Line(s, _camera.MapToScreen(Center(target)), BattleColor.WithAlpha(0.9f), 2.5f);

            // Counters shrink when zoomed out so they don't bury the map.
            float scale = selected ? 1 : Math.Clamp(_camera.Zoom / 3, 0.45f, 1);
            float W = 28 * scale, H = 19 * scale;
            var r = new Rect(s.X - W / 2, s.Y - H / 2, W, H);
            var color = new Rgba(_session.Players[unit.OwnerId].Color);
            Batch.Rect(r.X - 2, r.Y - 2, r.W + 4, r.H + 4, selected ? Theme.Accent : Rgba.Black);
            Batch.Rect(r.X, r.Y, r.W, r.H, color.Scale(0.55f).WithAlpha(1));
            Batch.Rect(r.X + 2, r.Y + 2, r.W - 4, r.H - 4, color);
            if (unit.IsMilitary)
            {
                bool mounted = unit.Battalions.Count(b => b.Info.Mounted) * 2 > unit.Battalions.Count;
                Batch.Line(new(r.X + 2, r.Bottom - 2), new(r.Right - 2, r.Y + 2), Rgba.Black, 1.5f);
                if (!mounted) Batch.Line(new(r.X + 2, r.Y + 2), new(r.Right - 2, r.Bottom - 2), Rgba.Black, 1.5f);
            }
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
            else if (scale > 0.7f) Ui.TextCentered(r, unit.IsHeadquarters ? "HQ" : unit.Symbol, Rgba.Black, FontSize.Small, bold: true);
            if (unit.IsHeadquarters && scale > 0.7f)
                Ui.TextCentered(new Rect(r.X - 10, r.Y - 16, r.W + 20, 14), unit.Symbol, Rgba.White, FontSize.Small, bold: true);
            _unitHitBoxes.Add((unit.Id, r));
        }
    }

    /// <summary>Crossed swords over every province being fought for, on land or at sea; hovering shows both sides.</summary>
    private void DrawBattles()
    {
        foreach (var battle in _session.Battles) DrawBattleMark(battle.ProvinceId, BattleSummary(battle));
        foreach (int sea in _session.NavalBattleProvinces()) DrawBattleMark(sea, NavalBattleSummary(sea));
    }

    private void DrawBattleMark(int provinceId, string summary)
    {
        var s = _camera.MapToScreen(Center(provinceId));
        if (!OnScreen(s)) return;
        float size = 9 + 2 * (float)Math.Sin(_realTime * 6);
        Batch.Rect(s.X - size - 2, s.Y - size - 2, 2 * size + 4, 2 * size + 4, Rgba.Black.WithAlpha(0.6f));
        Batch.Line(new(s.X - size, s.Y - size), new(s.X + size, s.Y + size), BattleColor, 3);
        Batch.Line(new(s.X - size, s.Y + size), new(s.X + size, s.Y - size), BattleColor, 3);
        if (Ui.Hover(new Rect(s.X - size, s.Y - size, 2 * size, 2 * size))) Ui.Tooltip(summary);
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
        string Side(string role, int player, List<Unit> units) =>
            $"{role}: {_session.Players[player].Name} · {units.Count} div. · {units.Sum(u => u.Citizens):N0} hombres · " +
            $"organización {(units.Count == 0 ? 0 : units.Average(u => u.OrganisationShare)):P0}";
        var p = Map.Provinces[battle.ProvinceId];
        return $"Batalla por {_session.PlaceName(p)} ({GameSession.FormatHours(_session.Date.Hours - battle.StartHours)})\n" +
               Side("Atacante", battle.AttackerId, attackers) + "\n" + Side("Defensor", battle.DefenderId, defenders) +
               $"\nDefensa por el terreno{(p.HasRiver ? " y el río" : "")}: ×{MilitaryRules.DefenseMultiplier(p):0.##}";
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
        var era = owner.ArmyEra;
        string kind = unit.Type switch
        {
            UnitType.Regiment => $"{Formations.LevelName(CommandLevels.Regiment, era)} de {Formations.BattalionCount(unit.Battalions.Count, era)}",
            UnitType.Headquarters => $"Cuartel general de {Formations.LevelName(unit.HeadquartersLevel, era).ToLowerInvariant()}",
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
        }
        bool canSettle = here.OwnerId == Human.Id && !here.IsOccupied;
        if (Ui.Button(new Rect(x, y, half, 32), unit.CanFoundCity ? "Asentarse" : "Licenciar", canSettle,
                tooltip: canSettle ? "Disuelve la unidad; sus ciudadanos se quedan a vivir en esta provincia." : "Solo en una provincia tuya.", size: FontSize.Small))
        {
            Show(_session.Disband(Human.Id, unit.Id));
            _selectedProvince = here.Id;
            return;
        }
        if (Ui.Button(new Rect(x + half + 6, y, half, 32), "Detener", unit.IsMoving || unit.AttackingProvinceId.HasValue, size: FontSize.Small))
            _session.MoveUnit(Human.Id, unit.Id, unit.ProvinceId);
        y += 40;
        Paragraph(x, ref y, w, unit.IsFleet
            ? "Clic derecho para navegar: por mares costeros con Navegación a vela y por el océano con Cartografía; atraca en tus ciudades con costa. Las flotas enemigas que se encuentran combaten."
            : unit.IsMilitary
            ? "Clic derecho para mover. Mover a una provincia enemiga con tropas la ataca; sin tropas, la ocupa. Solo se entra en tierras de naciones con las que estás en guerra. Para cruzar el mar, clic derecho sobre una flota tuya con transportes."
            : "Clic derecho para mover. No puede entrar en tierras de otras naciones. Para cruzar el mar, clic derecho sobre una flota tuya con transportes.", Theme.TextDim);
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
            return $"Hacia {_session.PlaceName(p)} ({GameSession.FormatHours(hours)})";
        }
        return "Esperando órdenes";
    }

    /// <summary>A regiment's battalions, or a fleet's ships and cargo, with the buttons to split and merge them.</summary>
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
        }
        y += 4;

        bool mine = unit.OwnerId == Human.Id;
        for (int i = 0; i < unit.Battalions.Count; i++)
        {
            var b = unit.Battalions[i];
            Ui.Text(x, y, Formations.BattalionName(b.Info, unit.Owner.ArmyEra), Theme.Text, FontSize.Small, bold: true);
            string men = $"{b.Strength:0}/{b.Info.Men}";
            Ui.Text(x + w - (mine && unit.Battalions.Count > 1 ? 74 : 0) - Ui.Font.Measure(men, FontSize.Small), y, men, Theme.TextDim, FontSize.Small);
            if (mine && unit.Battalions.Count > 1 && Ui.Button(new Rect(x + w - 66, y - 2, 66, 20), "Separar", !unit.AttackingProvinceId.HasValue,
                    tooltip: "Sale de esta unidad y forma una nueva.", size: FontSize.Small))
                Show(_session.Split(Human.Id, unit.Id, i));
            var row = new Rect(x, y, w, 30);
            y += 19;
            Bar(new Rect(x, y, w, 4), b.StrengthShare, StrengthColor);
            Bar(new Rect(x, y + 5, w, 4), b.OrganisationShare, OrganisationColor);
            if (Ui.Hover(row))
                Ui.Tooltip($"{b.Info.Name}: ataque {b.Info.Attack:0.#}, defensa {b.Info.Defense:0.#}, organización {b.Organisation:0}/{b.Info.MaxOrganisation:0}" +
                           (b.Info.Mounted ? "\nMontada: rápida, pero ataca a la mitad en bosques, pantanos y montañas." : "") +
                           (b.Info.Capacity > 0 ? $"\nLleva {b.Info.Capacity:N0} hombres." : ""));
            y += 16;
        }
        if (!mine || unit.IsAboard) return;

        var others = _session.Units.Where(u => u.IsFleet == unit.IsFleet && (u.IsMilitary || u.IsFleet) && !u.IsAboard
                                               && u.OwnerId == Human.Id && u.ProvinceId == unit.ProvinceId && u.Id != unit.Id).Take(3).ToList();
        foreach (var other in others)
        {
            var can = _session.CanMerge(unit, other);
            string size = other.IsFleet ? Formations.ShipCount(other.Battalions.Count) : $"{other.Battalions.Count} br.";
            if (Ui.Button(new Rect(x, y, w, 26), $"Unir {other.Name} ({size})", can.Ok, tooltip: can.Ok ? null : can.Message, size: FontSize.Small))
                Show(_session.Merge(Human.Id, unit.Id, other.Id));
            y += 30;
        }
        if (!unit.IsFleet) AttachButtons(unit, x, ref y, w);
    }

    private void HeadquartersDetails(Unit hq, float x, ref float y, float w)
    {
        var info = CommandLevels.Info(hq.HeadquartersLevel);
        Line(x, ref y, "Alcance", $"{info.RangeKm:N0} km");
        CommandLine(hq, x, ref y);
        var subs = _session.SubordinatesOf(hq).ToList();
        string below = Formations.LevelPlural(hq.HeadquartersLevel - 1, hq.Owner.ArmyEra);
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
            Ui.Text(x, y, $"Forma un cuartel de {Formations.LevelName(unit.CommandLevel + 1, unit.Owner.ArmyEra).ToLowerInvariant()} en una ciudad para darle mando.", Theme.TextDim, FontSize.Small);
            y += 22;
        }
    }

    // ------------------------------------------------------------------ city: Ejército tab

    /// <summary>Battalions the city can train (those of undiscovered advances are not listed), HQs, and what is in training.</summary>
    private void ArmyPanel(City city, float x, ref float y, float w)
    {
        var era = Human.ArmyEra;
        Ui.Text(x, y, $"Entrenar {Formations.LevelPlural(CommandLevels.Regiment, era)}", Theme.Text, bold: true);
        y += 26;
        foreach (var template in Human.Templates.Take(4))
        {
            var can = _session.CanTrainTemplate(city, template);
            string tip = $"{template.Name}: {template.Composition}.\n{template.Men} hombres de la ciudad. Ataque {template.Attack:0.#}, defensa {template.Defense:0.#}." +
                         $"\nCoste: {template.Cost}. Tarda {template.TrainingDays} días." + (can.Ok ? "" : "\n" + can.Message);
            if (Ui.Button(new Rect(x, y, w, 28), $"{template.Name}  ·  {Formations.BattalionCount(template.Battalions.Count, era)}  ·  {template.TrainingDays} d",
                    can.Ok, tooltip: tip, size: FontSize.Small))
                Show(_session.TrainTemplate(Human.Id, city.Id, template.Id));
            y += 32;
        }
        Ui.Text(x, y, Human.Templates.Count > 4 ? "Más plantillas en la pestaña Plantillas de la nación (N)." : "Diseña plantillas en la pestaña Plantillas de la nación (N).",
            Theme.TextDim, FontSize.Small);
        y += 28;

        string plural = Formations.BattalionPlural(era);
        Ui.Text(x, y, $"Entrenar {plural} {(era == ArmyEra.Modern ? "sueltos" : "sueltas")}", Theme.Text, bold: true);
        y += 26;
        foreach (var type in Battalions.All.Where(t => t.Info().Requires.All(Human.Techs.Contains)))
        {
            var info = type.Info();
            var can = _session.CanTrain(city, type);
            string tip = $"{Formations.BattalionName(info, era)}: {info.Men} hombres de la ciudad. Ataque {info.Attack:0.#}, defensa {info.Defense:0.#}, " +
                         $"organización {info.MaxOrganisation:0}, {info.Speed * GameRules.CitizenSpeedKmh:0.#} km/h." +
                         (info.Mounted ? "\nMontada: ataca a la mitad en bosques, pantanos y montañas." : "") +
                         $"\nCoste: {info.Cost}. Tarda {info.TrainingDays} días." + (can.Ok ? "" : "\n" + can.Message);
            if (Ui.Button(new Rect(x, y, w, 28), $"{info.Name}  ·  {info.Cost}  ·  {info.TrainingDays} d", can.Ok, tooltip: tip, size: FontSize.Small))
                Show(_session.Train(Human.Id, city.Id, type));
            y += 32;
        }

        y += 8;
        Ui.Text(x, y, "Cuarteles generales", Theme.Text, bold: true);
        y += 26;
        foreach (var level in CommandLevels.All)
        {
            var can = _session.CanRaiseHeadquarters(city, level.Level);
            string tip = $"Manda hasta {level.MaxSubordinates} {Formations.LevelPlural(level.Level - 1, era)} a menos de {level.RangeKm:N0} km: " +
                         $"+{MilitaryRules.CommandBonus:P0} en combate y recuperación (+{MilitaryRules.HigherCommandBonus:P0} por cada nivel superior enlazado)." +
                         $"\n{level.Staff} hombres de la ciudad. Coste: {level.Cost}. Tarda {level.TrainingDays} días." + (can.Ok ? "" : "\n" + can.Message);
            if (Ui.Button(new Rect(x, y, w, 28), $"{Formations.LevelName(level.Level, era)}  ·  {level.Cost}  ·  {level.TrainingDays} d", can.Ok, tooltip: tip, size: FontSize.Small))
                Show(_session.RaiseHeadquarters(Human.Id, city.Id, level.Level));
            y += 32;
        }

        if (city.Training.Count == 0) return;
        y += 8;
        Ui.Text(x, y, "En instrucción", Theme.Text, bold: true);
        y += 26;
        foreach (var order in city.Training)
        {
            Ui.Text(x, y, order.Name(era), Theme.Text, FontSize.Small);
            string days = $"{order.DaysLeft} d";
            Ui.Text(x + w - Ui.Font.Measure(days, FontSize.Small), y, days, Theme.TextDim, FontSize.Small);
            y += 18;
            Bar(new Rect(x, y, w, 5), 1 - order.DaysLeft / (double)order.TotalDays, Theme.Accent);
            y += 12;
        }
    }
}
