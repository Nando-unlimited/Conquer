using Conquer.Game.Entities;
using Conquer.Game.Rules;
using Conquer.Game.Science;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Presentation;

/// <summary>
/// Something that needs the player's attention, shown under the top bar: a short label, how serious it is, what it is
/// about, where a click takes the player and, for some, an icon.
/// </summary>
public sealed record Alert(string Text, Tone Tone, string Tooltip, Action OnClick, Icon? Icon = null);

/// <summary>The alerts under the top bar: hunger, unrest, troops in trouble, battles at home, finished works and idle research.</summary>
public sealed partial class GameController
{
    /// <summary>How many times each alert has been clicked, so that each click shows the next of its provinces or units.</summary>
    private readonly Dictionary<string, int> _alertClicks = [];

    /// <summary>The alerts that apply now, the most urgent first.</summary>
    public IReadOnlyList<Alert> Alerts()
    {
        var alerts = new List<Alert>();
        if (Human.IsStarving)
            alerts.Add(new Alert("Hambre", Tone.Bad,
                "Se ha acabado la comida: la gente muere de hambre y la moral se hunde. Construye granjas, deja de reclutar o licencia tropas.",
                () => OpenNation(NationTab.Summary)));
        else if (Human.LastDayNet[(int)Game.Economy.ResourceType.Food] < 0 && Human.FoodReserveDays < GameRules.FoodReserveFullDays)
            alerts.Add(new Alert($"Comida: {Human.FoodReserveDays:0} días", Tone.Accent,
                $"Se come más de lo que se cosecha y la comida almacenada dura {Human.FoodReserveDays:0} días.",
                () => OpenNation(NationTab.Summary)));

        if (Human.ArmyUnpaid)
            alerts.Add(new Alert("Sin paga", Tone.Bad,
                "No hay con qué pagar al ejército: las tropas pierden organización y desertan. Licencia unidades o consigue más recursos.",
                () => OpenNation(NationTab.Army)));

        var attacked = Session.Battles.Where(b => b.DefenderId == Human.Id).Select(b => b.ProvinceId).Distinct().ToList();
        if (attacked.Count > 0)
            alerts.Add(ProvinceAlert("Atacados", Tone.Bad, attacked,
                "El enemigo ataca estas provincias. Haz clic para ir a cada una.", p => PlaceOf(p)));

        var besieged = Session.Sieges.Where(x => Map.Provinces[x.ProvinceId].ControllerId == Human.Id).OrderByDescending(x => x.Progress)
            .Select(x => x.ProvinceId).ToList();
        if (besieged.Count > 0)
            alerts.Add(ProvinceAlert("Sitiadas", Tone.Bad, besieged,
                "El enemigo sitia estas provincias: caerán si no lo echas a tiempo.",
                p => $"{PlaceOf(p)}: {Math.Min(1, Session.SiegeAt(p.Id)!.Progress / GameSession.SiegeDays(p)):P0}"));

        var restless = Human.Provinces.Select(id => Map.Provinces[id])
            .Where(p => p.Population >= 1 && (p.Mood < GameRules.UnrestMood || p.RevoltProgress > 0))
            .OrderByDescending(p => p.RevoltProgress).ThenBy(p => p.Mood).Select(p => p.Id).ToList();
        if (restless.Count > 0)
            alerts.Add(ProvinceAlert("Descontento", restless.Any(id => GameSession.RevoltRisk(Map.Provinces[id]) >= 0.5) ? Tone.Bad : Tone.Accent, restless,
                $"Provincias con la moral por debajo de {GameRules.UnrestMood:0}: no pagan impuestos y, sin tropas dentro, se acercan a la rebelión.",
                p => $"{PlaceOf(p)}: moral {p.Mood:0}" + (p.RevoltProgress > 0 ? $", rebelión {GameSession.RevoltRisk(p):P0}" : "")
                     + (Session.IsGarrisoned(p) ? " (guarnición)" : "")));

        var sick = Human.Provinces.Select(id => Map.Provinces[id]).Where(GameSession.IsSick)
            .OrderByDescending(p => p.Population).Select(p => p.Id).ToList();
        if (sick.Count > 0)
            alerts.Add(ProvinceAlert("Epidemia", Tone.Bad, sick,
                "Una epidemia mata a parte de la gente de estas provincias y se contagia por los caminos y los puertos. La Medicina, el Saneamiento y los hospitales la frenan.",
                p => $"{PlaceOf(p)}: {p.PlagueDaysLeft} días, {TextFormat.Compact(p.Population * Session.PlagueDeaths(p))} muertos/día"));

        var regiments = Session.Units.Where(u => u.OwnerId == Human.Id && u.IsMilitary && !u.IsAboard).ToList();
        var unsupplied = regiments.Where(u => !Session.IsInSupply(u)).Select(u => u.Id).ToList();
        if (unsupplied.Count > 0)
            alerts.Add(UnitAlert("Sin suministro", Tone.Bad, unsupplied,
                "Estas unidades están fuera del alcance de tus ciudades y carreteras: pierden hombres y organización cada día."));
        var noAmmo = regiments.Where(u => GameSession.AmmoCapacity(u) > 0 && GameSession.Ammo(u) < GameSession.AmmoCapacity(u) * 0.25).Select(u => u.Id).ToList();
        if (noAmmo.Count > 0)
            alerts.Add(UnitAlert("Sin munición", Tone.Bad, noAmmo,
                $"A estas unidades les queda menos de una cuarta parte de su munición: sin ella luchan al {MilitaryRules.OutOfAmmoEfficiency:P0}. " +
                "La capital se la repone con los suministros del almacén: fabrica más en los talleres o sube la prioridad de su cuartel general."));
        if (Human.CargoLeftForWantOfConvoys >= 0.5)
            alerts.Add(new Alert("Faltan convoyes", Tone.Bad,
                $"Los envíos a tus tropas al otro lado del mar dejaron atrás {Human.CargoLeftForWantOfConvoys:N0} hombres, piezas o suministros por falta de convoyes. Encarga más en la pestaña Marina.",
                () => OpenNation(NationTab.Navy)));
        if (Human.ConvoysLostLastDay >= 0.05)
            alerts.Add(new Alert("Convoyes hundidos", Tone.Bad,
                $"Ayer el enemigo hundió {Human.ConvoysLostLastDay:0.#} de tus convoyes con su carga. Pon flotas a escoltar los mares de sus rutas o cambia de ruta.",
                () => OpenNation(NationTab.Navy)));
        var worn = regiments.Where(u => Session.IsInSupply(u) && Session.DailyAttrition(u) > 0).Select(u => u.Id).ToList();
        if (worn.Count > 0)
            alerts.Add(UnitAlert("Desgaste", Tone.Accent, worn,
                "Estas unidades pierden hombres por el frío, el desierto o la altura. Refúgialas en una de tus ciudades o bájalas de las cumbres."));

        var finished = Session.FinishedWorks.Where(w => Map.Provinces[w.ProvinceId].OwnerId == Human.Id).Reverse().ToList();
        if (finished.Count > 0)
            alerts.Add(new Alert($"Obras terminadas ({finished.Count})", Tone.Good,
                $"Obras acabadas en los últimos {GameSession.RecentWorkDays} días: puedes empezar otra allí. Haz clic para ir a cada una.\n"
                + Listed(finished.Select(w => $"{w.Name} en {PlaceOf(Map.Provinces[w.ProvinceId])}")),
                () => ViewProvince(finished[NextClick("Obras terminadas", finished.Count)].ProvinceId)));

        bool idleScience =Human.CapitalCityId.HasValue && Techs.Branches.Any(b =>
            Human.Researching[(int)b] is null && Techs.InBranch(b).Any(t => GameSession.CanResearch(Human, t).Ok));
        if (idleScience)
            alerts.Add(new Alert("Ciencia sin elegir", Tone.Accent, "Hay ramas de la ciencia sin nada que investigar: su parte de la ciencia se pierde para ellas.",
                () => OpenNation(NationTab.Science), new ScienceIcon()));
        return alerts;
    }

    private string PlaceOf(Province p) => Session.PlaceName(p);

    /// <summary>An alert about some provinces: each click shows the next one.</summary>
    private Alert ProvinceAlert(string label, Tone tone, List<int> provinces, string intro, Func<Province, string> line) =>
        new($"{label} ({provinces.Count})", tone, intro + "\n" + Listed(provinces.Select(id => line(Map.Provinces[id]))),
            () => ViewProvince(provinces[NextClick(label, provinces.Count)]));

    /// <summary>An alert about some units: each click selects the next one.</summary>
    private Alert UnitAlert(string label, Tone tone, List<int> units, string intro) =>
        new($"{label} ({units.Count})", tone, intro + "\n" + Listed(units.Select(Session.UnitById).OfType<Unit>()
                .Select(u => $"{u.Name} en {PlaceOf(Map.Provinces[u.ProvinceId])}")),
            () => ViewUnit(units[NextClick(label, units.Count)]));

    /// <summary>Up to five lines, and how many more there are.</summary>
    private static string Listed(IEnumerable<string> lines)
    {
        var all = lines.ToList();
        return string.Join("\n", all.Take(5).Select(l => "· " + l)) + (all.Count > 5 ? $"\n... y {all.Count - 5} más" : "");
    }

    private int NextClick(string label, int count)
    {
        int n = _alertClicks.GetValueOrDefault(label);
        _alertClicks[label] = n + 1;
        return n % count;
    }

    private void OpenNation(NationTab tab)
    {
        Nation.Tab = tab;
        Nation.Visible = true;
    }
}
