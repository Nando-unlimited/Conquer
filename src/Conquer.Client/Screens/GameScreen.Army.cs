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
    /// and a "C" for settlers. Divisions carry a strength bar (green) and an organisation bar (amber).
    /// </summary>
    private void DrawUnits()
    {
        _unitHitBoxes.Clear();
        var stackIndex = new Dictionary<int, int>();
        foreach (var unit in _session.Units)
        {
            var pos = unit.IsMoving && !unit.AttackingProvinceId.HasValue ? Between(unit.ProvinceId, unit.Path[0], unit.StepProgress) : Center(unit.ProvinceId);
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
                bool mounted = unit.Brigades.Count(b => b.Info.Mounted) * 2 > unit.Brigades.Count;
                Batch.Line(new(r.X + 2, r.Bottom - 2), new(r.Right - 2, r.Y + 2), Rgba.Black, 1.5f);
                if (!mounted) Batch.Line(new(r.X + 2, r.Y + 2), new(r.Right - 2, r.Bottom - 2), Rgba.Black, 1.5f);
                Bar(new Rect(r.X - 2, r.Bottom + 3, r.W + 4, 3), unit.StrengthShare, StrengthColor);
                Bar(new Rect(r.X - 2, r.Bottom + 7, r.W + 4, 3), unit.OrganisationShare, OrganisationColor);
            }
            else if (scale > 0.7f) Ui.TextCentered(r, unit.IsHeadquarters ? "HQ" : unit.Symbol, Rgba.Black, FontSize.Small, bold: true);
            if (unit.IsHeadquarters && scale > 0.7f)
                Ui.TextCentered(new Rect(r.X - 10, r.Y - 16, r.W + 20, 14), unit.Symbol, Rgba.White, FontSize.Small, bold: true);
            _unitHitBoxes.Add((unit.Id, r));
        }
    }

    /// <summary>Crossed swords over every province being fought for; hovering shows both sides.</summary>
    private void DrawBattles()
    {
        foreach (var battle in _session.Battles)
        {
            var s = _camera.MapToScreen(Center(battle.ProvinceId));
            if (!OnScreen(s)) continue;
            float size = 9 + 2 * (float)Math.Sin(_realTime * 6);
            Batch.Rect(s.X - size - 2, s.Y - size - 2, 2 * size + 4, 2 * size + 4, Rgba.Black.WithAlpha(0.6f));
            Batch.Line(new(s.X - size, s.Y - size), new(s.X + size, s.Y + size), BattleColor, 3);
            Batch.Line(new(s.X - size, s.Y + size), new(s.X + size, s.Y - size), BattleColor, 3);
            if (Ui.Hover(new Rect(s.X - size, s.Y - size, 2 * size, 2 * size))) Ui.Tooltip(BattleSummary(battle));
        }
    }

    private string BattleSummary(Battle battle)
    {
        var attackers = battle.Attackers.Select(_session.UnitById).OfType<Unit>().ToList();
        var defenders = _session.EnemyDivisionsIn(battle.ProvinceId, battle.AttackerId).ToList();
        string Side(string role, int player, List<Unit> units) =>
            $"{role}: {_session.Players[player].Name} · {units.Count} div. · {units.Sum(u => u.Citizens):N0} hombres · " +
            $"organización {(units.Count == 0 ? 0 : units.Average(u => u.OrganisationShare)):P0}";
        var p = Map.Provinces[battle.ProvinceId];
        return $"Batalla por {_session.CityIn(p)?.Name ?? p.Info.Name} ({GameSession.FormatHours(_session.Date.Hours - battle.StartHours)})\n" +
               Side("Atacante", battle.AttackerId, attackers) + "\n" + Side("Defensor", battle.DefenderId, defenders) +
               $"\nTerreno: defensa ×{MilitaryRules.TerrainDefense(p.Biome):0.##}";
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
            UnitType.Division => $"División de {unit.Brigades.Count} brigada{(unit.Brigades.Count == 1 ? "" : "s")}",
            UnitType.Headquarters => $"Cuartel general de {CommandLevels.Info(unit.HeadquartersLevel).Name.ToLowerInvariant()}",
            _ => $"{unit.Citizens:N0} colonos",
        };
        Ui.Text(x, y, kind, Theme.TextDim, FontSize.Small);
        y += 24;
        Line(x, ref y, "Nación", owner.Name, new Rgba(owner.Color));
        Line(x, ref y, "Ubicación", _session.CityIn(here)?.Name ?? here.Info.Name);
        Line(x, ref y, "Estado", UnitState(unit, out var stateColor), stateColor);

        if (unit.IsMilitary) DivisionDetails(unit, x, ref y, w);
        else if (unit.IsHeadquarters) HeadquartersDetails(unit, x, ref y, w);
        if (unit.OwnerId != Human.Id) return;

        y += 6;
        float half = (w - 6) / 2;
        if (unit.CanFoundCity)
        {
            var can = _session.CanFoundCity(unit);
            if (Ui.Button(new Rect(x, y, w, 32), "Fundar ciudad", can.Ok, tooltip: can.Ok ? "Reclama esta provincia y funda una ciudad con estos colonos." : can.Message))
                Show(_session.FoundCity(Human.Id, unit.Id));
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
        if (Ui.Button(new Rect(x, y, half, 32), unit.IsMilitary || unit.IsHeadquarters ? "Licenciar" : "Asentarse", canSettle,
                tooltip: canSettle ? "Disuelve la unidad; sus ciudadanos se quedan a vivir en esta provincia." : "Solo en una provincia tuya.", size: FontSize.Small))
        {
            Show(_session.Disband(Human.Id, unit.Id));
            _selectedProvince = here.Id;
            return;
        }
        if (Ui.Button(new Rect(x + half + 6, y, half, 32), "Detener", unit.IsMoving || unit.AttackingProvinceId.HasValue, size: FontSize.Small))
            _session.MoveUnit(Human.Id, unit.Id, unit.ProvinceId);
        y += 40;
        Paragraph(x, ref y, w, unit.IsMilitary
            ? "Clic derecho para mover. Mover a una provincia enemiga con tropas la ataca; sin tropas, la ocupa. Solo se entra en tierras de naciones con las que estás en guerra."
            : "Clic derecho para mover. No puede entrar en tierras de otras naciones.", Theme.TextDim);
    }

    private string UnitState(Unit unit, out Rgba color)
    {
        color = Theme.Text;
        if (unit.AttackingProvinceId is int target)
        {
            color = BattleColor;
            var p = Map.Provinces[target];
            return $"Atacando {_session.CityIn(p)?.Name ?? p.Info.Name}";
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
            return $"Hacia {_session.CityIn(p)?.Name ?? p.Info.Name} ({GameSession.FormatHours(hours)})";
        }
        return "Esperando órdenes";
    }

    private void DivisionDetails(Unit unit, float x, ref float y, float w)
    {
        bool supplied = _session.IsInSupply(unit);
        Line(x, ref y, "Suministro", supplied ? "Abastecida" : "Sin suministro", supplied ? Theme.Good : Theme.Bad);
        Line(x, ref y, "Velocidad", $"{unit.Speed * GameRules.CitizenSpeedKmh:0.#} km/h");
        CommandLine(unit, x, ref y);
        y += 4;

        bool mine = unit.OwnerId == Human.Id;
        for (int i = 0; i < unit.Brigades.Count; i++)
        {
            var b = unit.Brigades[i];
            Ui.Text(x, y, b.Info.Name, Theme.Text, FontSize.Small, bold: true);
            string men = $"{b.Strength:0}/{b.Info.Men}";
            Ui.Text(x + w - (mine && unit.Brigades.Count > 1 ? 74 : 0) - Ui.Font.Measure(men, FontSize.Small), y, men, Theme.TextDim, FontSize.Small);
            if (mine && unit.Brigades.Count > 1 && Ui.Button(new Rect(x + w - 66, y - 2, 66, 20), "Separar", !unit.AttackingProvinceId.HasValue,
                    tooltip: "Esta brigada forma una división nueva.", size: FontSize.Small))
                Show(_session.Split(Human.Id, unit.Id, i));
            var row = new Rect(x, y, w, 30);
            y += 19;
            Bar(new Rect(x, y, w, 4), b.StrengthShare, StrengthColor);
            Bar(new Rect(x, y + 5, w, 4), b.OrganisationShare, OrganisationColor);
            if (Ui.Hover(row))
                Ui.Tooltip($"{b.Info.Name}: ataque {b.Info.Attack:0.#}, defensa {b.Info.Defense:0.#}, organización {b.Organisation:0}/{b.Info.MaxOrganisation:0}" +
                           (b.Info.Mounted ? "\nMontada: rápida, pero ataca a la mitad en bosques, pantanos y montañas." : ""));
            y += 16;
        }
        if (!mine) return;

        var others = _session.Units.Where(u => u.IsMilitary && u.OwnerId == Human.Id && u.ProvinceId == unit.ProvinceId && u.Id != unit.Id).Take(3).ToList();
        foreach (var other in others)
        {
            var can = _session.CanMerge(unit, other);
            if (Ui.Button(new Rect(x, y, w, 26), $"Unir la {other.Name} ({other.Brigades.Count} br.)", can.Ok, tooltip: can.Ok ? null : can.Message, size: FontSize.Small))
                Show(_session.Merge(Human.Id, unit.Id, other.Id));
            y += 30;
        }
        AttachButtons(unit, x, ref y, w);
    }

    private void HeadquartersDetails(Unit hq, float x, ref float y, float w)
    {
        var info = CommandLevels.Info(hq.HeadquartersLevel);
        Line(x, ref y, "Alcance", $"{info.RangeKm:N0} km");
        CommandLine(hq, x, ref y);
        var subs = _session.SubordinatesOf(hq).ToList();
        string below = CommandLevels.NameOf(hq.HeadquartersLevel - 1).ToLowerInvariant();
        Ui.Text(x, y, $"Al mando ({subs.Count}/{info.MaxSubordinates} de {below})", Theme.Text, bold: true);
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
            Ui.Text(x, y, $"Forma un cuartel de {CommandLevels.Info(unit.CommandLevel + 1).Name.ToLowerInvariant()} en una ciudad para darle mando.", Theme.TextDim, FontSize.Small);
            y += 22;
        }
    }

    // ------------------------------------------------------------------ city: Ejército tab

    /// <summary>Brigades the city can train (those of undiscovered advances are not listed), HQs, and what is in training.</summary>
    private void ArmyPanel(City city, float x, ref float y, float w)
    {
        Ui.Text(x, y, "Entrenar brigadas", Theme.Text, bold: true);
        y += 26;
        foreach (var type in Brigades.All.Where(t => t.Info().RequiresTech is not Tech tech || Human.Techs.Contains(tech)))
        {
            var info = type.Info();
            var can = _session.CanTrain(city, type);
            string tip = $"{info.Name}: {info.Men} hombres de la ciudad. Ataque {info.Attack:0.#}, defensa {info.Defense:0.#}, " +
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
            string tip = $"Manda hasta {level.MaxSubordinates} unidades de {CommandLevels.NameOf(level.Level - 1).ToLowerInvariant()} a menos de {level.RangeKm:N0} km: " +
                         $"+{MilitaryRules.CommandBonus:P0} en combate y recuperación (+{MilitaryRules.HigherCommandBonus:P0} por cada nivel superior enlazado)." +
                         $"\n{level.Staff} hombres de la ciudad. Coste: {level.Cost}. Tarda {level.TrainingDays} días." + (can.Ok ? "" : "\n" + can.Message);
            if (Ui.Button(new Rect(x, y, w, 28), $"{level.Name}  ·  {level.Cost}  ·  {level.TrainingDays} d", can.Ok, tooltip: tip, size: FontSize.Small))
                Show(_session.RaiseHeadquarters(Human.Id, city.Id, level.Level));
            y += 32;
        }

        if (city.Training.Count == 0) return;
        y += 8;
        Ui.Text(x, y, "En instrucción", Theme.Text, bold: true);
        y += 26;
        foreach (var order in city.Training)
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
