using Conquer.Client.Graphics;
using Conquer.Client.UI;
using Conquer.Game.Military;
using Conquer.Game.Simulation;

namespace Conquer.Client.Screens;

/// <summary>
/// The window to lay a road or railway from a city or HQ of the player's where their engineers stand: it lists the
/// player's other cities and HQs, nearest first (the nearest one that still needs it chosen), and shows the chosen
/// route on the map with what it costs, the work it takes and the cities it joins on the way. Time stops while it is open.
/// </summary>
public sealed partial class GameScreen
{
    private RoadKind? _roadKind;
    private int _roadFrom;
    private int _roadTarget = -1;
    private List<(int Province, RoadPlan Plan)> _roadOptions = [];

    private bool RoadWindowOpen => _roadKind.HasValue;

    private void OpenRoadWindow(int from, RoadKind kind)
    {
        _roadKind = kind;
        _roadFrom = from;
        _roadOptions = [.. _session.RoadHubs(Human.Id).Where(id => id != from)
            .Select(id => (Province: id, Plan: _session.PlanRoad(Human.Id, from, id, kind)))
            .Where(o => o.Plan != null).Select(o => (o.Province, o.Plan!))
            .OrderBy(o => o.Item2.Hours)];
        _roadTarget = _roadOptions.Where(o => o.Plan.NewLinks > 0).Select(o => o.Province).DefaultIfEmpty(-1).First();
    }

    private void CloseRoadWindow() => _roadKind = null;

    /// <summary>The route being chosen, to draw on the map.</summary>
    private IReadOnlyList<int>? PlannedRoute => RoadWindowOpen ? _roadOptions.FirstOrDefault(o => o.Province == _roadTarget).Plan?.Route : null;


    private void DrawRoadWindow()
    {
        var kind = _roadKind!.Value;
        var info = kind.Info();
        var s = _app.ScreenSize;
        Ui.Block(new Rect(0, 0, s.X, s.Y));
        // On the left, so the route stays in sight on the map.
        float width = Math.Min(480, s.X - 32), height = Math.Min(600, s.Y - TopBarHeight - 90);
        var panel = new Rect(16, TopBarHeight + 16, width, height);
        Ui.Panel(panel);
        float x = panel.X + 20, y = panel.Y + 18, w = panel.W - 40;
        Ui.Text(x, y, $"{(info.Feminine ? "Nueva" : "Nuevo")} {info.Name.ToLowerInvariant()} desde {_game.HubName(_roadFrom)}", Theme.Accent, FontSize.Large, bold: true);
        y += 38;
        Paragraph(x, ref y, w, "¿Qué ciudad o cuartel general quieres unir? La ruta es la que seguiría un ejército, aprovechando las carreteras que ya hay; " +
                                "las ciudades por las que pasa quedan unidas también.", Theme.TextDim);
        y += 4;

        float listBottom = panel.Bottom - 210;
        if (_roadOptions.Count == 0)
        {
            Ui.Text(x, y, "No tienes otra ciudad ni cuartel general a los que llegar.", Theme.TextDim, FontSize.Small);
            y += 24;
        }
        int shown = 0;
        foreach (var (province, plan) in _roadOptions)
        {
            if (y + 28 > listBottom) break;
            shown++;
            string label = plan.NewLinks == 0
                ? $"{_game.HubName(province)}  ·  ya {(info.Feminine ? "unida" : "unido")}"
                : $"{_game.HubName(province)}  ·  {GameSession.FormatHours(plan.Hours)} de marcha  ·  {plan.NewLinks} tramos";
            if (Ui.Button(new Rect(x, y, w, 26), label, plan.NewLinks > 0, active: province == _roadTarget, size: FontSize.Small))
                _roadTarget = province;
            y += 30;
        }
        if (shown < _roadOptions.Count) Ui.Text(x, y, $"y {_roadOptions.Count - shown} más lejos", Theme.TextDim, FontSize.Small);

        y = listBottom + 10;
        var chosen = _roadOptions.FirstOrDefault(o => o.Province == _roadTarget).Plan;
        if (chosen != null)
        {
            void Row(string label, string value, Rgba color)
            {
                Ui.Text(x, y, label, Theme.TextDim, FontSize.Small);
                float top = y;
                Paragraph(x + 110, ref y, w - 110, value, color);
                y = Math.Max(y, top + 20) + 4;
            }
            int engineers = _session.EngineersIn(Human.Id, _roadFrom);
            Row("Tramos nuevos", $"{chosen.NewLinks} de {chosen.Route.Count - 1}", Theme.Text);
            Row("Coste", chosen.Cost.ToString(), Human.Stockpile.Has(chosen.Cost) ? Theme.Text : Theme.Bad);
            Row("Trabajo", engineers > 1
                ? $"{chosen.WorkDays} días de un batallón: unos {Math.Ceiling(chosen.WorkDays / (double)engineers):0} con los {engineers} que hay aquí"
                : $"{chosen.WorkDays} días con un batallón de ingenieros", Theme.Text);
            Row("Une también", chosen.CitiesOnTheWay.Count == 0 ? "ninguna otra ciudad" : string.Join(", ", chosen.CitiesOnTheWay.Select(_game.HubName)), Theme.Text);
        }

        var can = _roadTarget < 0 ? CommandResult.Fail("Elige adónde va.") : _session.CanBuildRoad(Human.Id, _roadFrom, _roadTarget, kind);
        float half = (w - 8) / 2;
        if (Ui.Button(new Rect(x, panel.Bottom - 52, half, 36), "Construir", can.Ok,
                tooltip: can.Ok ? "Se paga ahora. Los ingenieros que estén en la ruta la construyen tramo a tramo desde aquí; si se van, la obra se para." : can.Message))
        {
            _game.Show(_session.BuildRoad(Human.Id, _roadFrom, _roadTarget, kind));
            CloseRoadWindow();
        }
        if (Ui.Button(new Rect(x + half + 8, panel.Bottom - 52, half, 36), "Cancelar")) CloseRoadWindow();
    }
}
