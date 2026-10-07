using Conquer.Game.Buildings;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Simulation;

namespace Conquer.Presentation;

/// <summary>
/// The Fuerza aérea tab of the nation screen: its air units with their size, división and mission, its air HQs, its
/// airfields and the escuadrillas it can form.
/// </summary>
public sealed partial class NationScreen
{
    private TablesPage AirForce() => new([AirUnitsTable(), AirCommandTable(), Airfields(), FormFlights()]);

    /// <summary>Each air unit: its size, base, planes, organisation, división and mission, and buttons to show its base or land it.</summary>
    private TablePage AirUnitsTable()
    {
        Column[] columns = [new("Unidad", 300), new("Base", 170), new("Aviones", 130), new("Organiz.", 90), new("División", 160), new("Misión", 280), new("", 150)];
        var rows = new List<IReadOnlyList<Cell>>();
        foreach (var unit in Session.AirUnits.Where(u => u.OwnerId == Player.Id).OrderBy(u => u.Type).ThenByDescending(u => u.Flights.Count))
        {
            var home = Session.BaseOf(unit);
            bool flying = Session.IsFlying(unit);
            string mission = unit.TargetProvinceId is int t ? $"{GameSession.AirMissionName(unit.Mission)} sobre {Session.PlaceName(Map.Provinces[t])}" : GameSession.AirMissionName(unit.Mission);
            var division = Session.DivisionOf(unit);
            rows.Add(
            [
                new TextCell(unit.Name, Bold: true, Suffix: $" {unit.Flights.Count} escuadr.", Tooltip: $"{unit.Info.Name}, {unit.Crews:N0} tripulantes."),
                new TextCell(unit.CarrierId is int c && Session.UnitById(c) is { } fleet ? fleet.Name : Session.PlaceName(home), Tone.Dim),
                new TextCell($"{unit.PlaneCount:0}/{unit.FullPlanes}", unit.StrengthShare < 0.5 ? Tone.Bad : Tone.Normal,
                    Bar: new CellBar(unit.StrengthShare, Tone.Strength, 26, 3, 20)),
                new TextCell("", Bar: new CellBar(unit.OrganisationShare, Tone.Organisation, 13, 8, 20)),
                new TextCell(division?.Name ?? "-", division == null ? Tone.Dim : Session.InAirCommand(unit) ? Tone.Good : Tone.Bad, TextSize.Small, Top: 8),
                new TextCell(mission + (unit.Mission is not (AirMission.None or AirMission.Paradrop) && !flying ? " (en tierra)" : ""),
                    unit.Mission == AirMission.None ? Tone.Dim : flying ? Tone.Good : Tone.Accent, TextSize.Small, Top: 8,
                    Tooltip: GameSession.AirMissionDescription(unit.Mission) + " Cámbiala en el panel de su base."),
                new ButtonsCell(
                [
                    new Button("Ver", () => ViewProvince(home.Id), Tooltip: "Mostrar su base en el mapa (allí se cambian su misión y su división)", Size: TextSize.Small),
                    new Button("Aterrizar", () => Show(Session.SetAirMission(Player.Id, unit.Id, AirMission.None)), unit.Mission != AirMission.None,
                        Tooltip: "Deja su misión y se queda en su base.", Size: TextSize.Small),
                ]),
            ]);
        }
        return new TablePage(new Table(columns, rows, Empty: "No tienes aviones. Forma escuadrillas en un aeródromo (abajo)."),
            "Unidades aéreas · escuadrilla de 6 aviones, escuadrón de 3 escuadrillas, grupo de 6 escuadrones, ala de 3 grupos", Tone.Accent);
    }

    /// <summary>
    /// The Mando aéreo and each División aérea: base, general and units, with a button to disband each, and buttons to
    /// form a División aérea or the Mando aéreo at the airfield nearest the capital.
    /// </summary>
    private TablePage AirCommandTable()
    {
        Column[] columns = [new("Cuartel general", 220), new("Base", 190), new("General", 260), new("Manda", 300), new("", 110)];
        var rows = new List<IReadOnlyList<Cell>>();
        foreach (var hq in Session.AirHeadquarters.Where(h => h.OwnerId == Player.Id).OrderByDescending(h => h.Level).ThenBy(h => h.Number))
        {
            string commands = hq.IsCommand
                ? $"{Session.AirHeadquarters.Count(h => h.OwnerId == Player.Id && !h.IsCommand)} divisiones aéreas"
                : $"{Session.UnitsOf(hq).Count()}/{MilitaryRules.MaxUnitsPerAirDivision} unidades";
            rows.Add(
            [
                new TextCell(hq.Name, hq.IsCommand ? Tone.Accent : Tone.Normal, Bold: true),
                new TextCell(Session.PlaceName(Map.Provinces[hq.BaseProvinceId]), Tone.Dim),
                new TextCell(hq.Officer is { } o ? $"{o.Title}" : "Ninguno", hq.Officer == null ? Tone.Dim : Tone.Normal, TextSize.Small, Top: 8,
                    Tooltip: hq.Officer is { } g ? $"{g.Summary}: suma su habilidad al fuego de las unidades de su división." : null),
                new TextCell(commands, Tone.Dim, TextSize.Small, Top: 8,
                    Tooltip: hq.IsCommand
                        ? $"Las divisiones a menos de {MilitaryRules.AirCommandRangeKm:N0} km dan +{MilitaryRules.HigherAirCommandBonus:P0} más a sus unidades."
                        : $"Manda las unidades con base a menos de {MilitaryRules.AirDivisionRangeKm:N0} km: +{MilitaryRules.AirCommandBonus:P0} y la habilidad de su general."),
                new ButtonsCell([new Button("Disolver", () => Show(Session.DisbandAirHeadquarters(Player.Id, hq.Id)),
                    Tooltip: "Sus unidades quedan sin división; su plana mayor vuelve a la reserva y su general a los oficiales sin destino.", Size: TextSize.Small)]),
            ]);
        }
        var airfield = Player.Provinces.Select(id => Map.Provinces[id]).Where(p => p.Has(BuildingType.Airfield))
            .OrderBy(p => Player.CapitalCityId is int c && Session.CityById(c) is { } city ? Map.DistanceKm(p, Map.Provinces[city.ProvinceId]) : 0).FirstOrDefault();
        foreach (int level in new[] { 1, 2 })
        {
            var can = airfield == null ? CommandResult.Fail("Se forma en uno de tus aeródromos.") : Session.CanRaiseAirHeadquarters(airfield, level);
            rows.Add(
            [
                new TextCell(level == 2 ? "Mando aéreo" : "División aérea", Tone.Dim),
                new TextCell(airfield == null ? "-" : Session.PlaceName(airfield), Tone.Dim),
                new TextCell($"Coste: {GameSession.AirHeadquartersCost(level)}, {MilitaryRules.AirHeadquartersStaff} hombres", Tone.Dim, TextSize.Small, Top: 8),
                new TextCell(level == 2 ? "Uno por nación: manda las divisiones aéreas" : "Manda unidades aéreas a su alcance", Tone.Dim, TextSize.Small, Top: 8),
                new ButtonsCell([new Button("Formar", () => Show(Session.RaiseAirHeadquarters(Player.Id, airfield!.Id, level)), can.Ok,
                    Tooltip: can.Ok ? $"Se forma en {Session.PlaceName(airfield!)} con un general de aviación." : can.Message, Size: TextSize.Small)]),
            ]);
        }
        return new TablePage(new Table(columns, rows), "Mando · asigna cada unidad a su división desde el panel de su base", Tone.Accent);
    }

    /// <summary>Each airfield: its escuadrillas out of its room and those being formed.</summary>
    private TablePage Airfields()
    {
        Column[] columns = [new("Aeródromo", 240), new("Escuadrillas", 140), new("En formación", 600)];
        var rows = new List<IReadOnlyList<Cell>>();
        foreach (var p in Player.Provinces.Select(id => Map.Provinces[id]).Where(p => p.Has(BuildingType.Airfield)).OrderBy(p => Session.PlaceName(p)))
        {
            var forming = p.Training.Where(o => o.Battalion is BattalionType t && t.First().Flies).ToList();
            rows.Add(
            [
                new TextCell(Session.PlaceName(p), Bold: true),
                new TextCell($"{Session.AirUnitsAt(p).Sum(u => u.Flights.Count)}/{MilitaryRules.FlightsPerAirfield}", Session.AirfieldRoom(p, Player.Id) > 0 ? Tone.Accent : Tone.Normal),
                new TextCell(forming.Count == 0 ? "-" : string.Join(", ", forming.Select(o => $"{o.Name} ({o.DaysLeft} d)")), forming.Count == 0 ? Tone.Dim : Tone.Normal),
            ]);
        }
        return new TablePage(new Table(columns, rows, Empty: "No tienes aeródromos: constrúyelos (Aviación) en la pestaña Edificios de una provincia."),
            "Aeródromos", Tone.Accent);
    }

    /// <summary>
    /// A row per kind of aircraft: the model it would form, its figures per escuadrilla and cost, and a button to form one
    /// at the airfield with room nearest the capital.
    /// </summary>
    private TablePage FormFlights()
    {
        Column[] columns = [new("Tipo", 210), new("Modelo", 270), new("Aire", 60), new("Tierra", 70), new("Alcance", 100), new("Hombres", 90), new("Coste", 290), new("", 100)];
        var airfield = Player.Provinces.Select(id => Map.Provinces[id]).Where(p => Session.AirfieldRoom(p, Player.Id) > 0)
            .OrderBy(p => Player.CapitalCityId is int c && Session.CityById(c) is { } city ? Map.DistanceKm(p, Map.Provinces[city.ProvinceId]) : 0).FirstOrDefault();
        var rows = new List<IReadOnlyList<Cell>>();
        foreach (var type in Battalions.All.Where(t => t.Line().Group == BattalionGroup.Air))
        {
            var model = GameSession.TrainedModel(Player, type);
            bool known = type.BestModel(Player.Techs) >= 0;
            var can = airfield == null ? CommandResult.Fail("Necesitas un aeródromo con sitio.") : Session.CanTrain(airfield, type);
            rows.Add(
            [
                new TextCell(type.Line().Name, Bold: true),
                new TextCell(model.Name, known ? Tone.Normal : Tone.Dim, Suffix: known ? null : " · por descubrir"),
                new TextCell($"{model.AirAttack:0}", Tone.Dim),
                new TextCell($"{model.Attack:0}", Tone.Dim),
                new TextCell($"{model.RangeKm:N0} km", Tone.Dim),
                new TextCell($"{model.Men}", Tone.Dim, Tooltip: $"{model.Men / model.Pieces} por avión."),
                new TextCell($"{model.TrainingCost} y {model.PiecesText(model.Pieces)}", Tone.Dim, TextSize.Small, Top: 8),
                new ButtonsCell([new Button("Formar", () => Show(Session.Train(Player.Id, airfield!.Id, type)), can.Ok,
                    Tooltip: can.Ok ? $"Forma una escuadrilla en {Session.PlaceName(airfield!)} en {GameSession.TrainingDays(Player, type)} días: {model.Men} tripulantes de la provincia, su oro y {model.Pieces} aviones del almacén." : can.Message,
                    Size: TextSize.Small)]),
            ]);
        }
        return new TablePage(new Table(columns, rows), "Formar escuadrillas (cifras por escuadrilla) · los aviones los fabrican los talleres", Tone.Accent);
    }
}
