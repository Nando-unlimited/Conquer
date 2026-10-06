using Conquer.Game.Buildings;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Simulation;

namespace Conquer.Presentation;

/// <summary>The Fuerza aérea tab of the nation screen: its wings with their missions, its airfields and the wings it can form.</summary>
public sealed partial class NationScreen
{
    private TablesPage AirForce() => new([WingsTable(), Airfields(), FormWings()]);

    /// <summary>Each wing: its base, planes, organisation and mission, whether it flies, and buttons to show its base or land it.</summary>
    private TablePage WingsTable()
    {
        Column[] columns = [new("Ala", 260), new("Base", 190), new("Aviones", 140), new("Organiz.", 100), new("Misión", 330), new("", 150)];
        var rows = new List<IReadOnlyList<Cell>>();
        foreach (var wing in Session.Wings.Where(w => w.OwnerId == Player.Id).OrderBy(w => w.Type).ThenBy(w => w.Number))
        {
            var home = Session.BaseOf(wing);
            bool flying = Session.IsFlying(wing);
            string mission = wing.TargetProvinceId is int t ? $"{GameSession.AirMissionName(wing.Mission)} sobre {Session.PlaceName(Map.Provinces[t])}" : GameSession.AirMissionName(wing.Mission);
            rows.Add(
            [
                new TextCell(wing.Name, Bold: true, Suffix: $" {wing.Info.Name.ToLowerInvariant()}"),
                new TextCell(wing.CarrierId is int c && Session.UnitById(c) is { } fleet ? fleet.Name : Session.PlaceName(home), Tone.Dim),
                new TextCell($"{wing.PlaneCount:0}/{wing.Info.Pieces}", wing.Planes.StrengthShare < 0.5 ? Tone.Bad : Tone.Normal,
                    Bar: new CellBar(wing.Planes.StrengthShare, Tone.Strength, 26, 3, 20)),
                new TextCell("", Bar: new CellBar(wing.Planes.OrganisationShare, Tone.Organisation, 13, 8, 20)),
                new TextCell(mission + (wing.Mission is not (AirMission.None or AirMission.Paradrop) && !flying ? " (en tierra)" : ""),
                    wing.Mission == AirMission.None ? Tone.Dim : flying ? Tone.Good : Tone.Accent, TextSize.Small, Top: 8,
                    Tooltip: GameSession.AirMissionDescription(wing.Mission) + " Cámbiala en el panel de su base."),
                new ButtonsCell(
                [
                    new Button("Ver", () => ViewProvince(home.Id), Tooltip: "Mostrar su base en el mapa (allí se cambia su misión)", Size: TextSize.Small),
                    new Button("Aterrizar", () => Show(Session.SetAirMission(Player.Id, wing.Id, AirMission.None)), wing.Mission != AirMission.None,
                        Tooltip: "Deja su misión y se queda en su base.", Size: TextSize.Small),
                ]),
            ]);
        }
        return new TablePage(new Table(columns, rows, Empty: "No tienes alas. Fórmalas en un aeródromo (abajo)."), "Alas", Tone.Accent);
    }

    /// <summary>Each airfield: its wings out of its room and those being formed.</summary>
    private TablePage Airfields()
    {
        Column[] columns = [new("Aeródromo", 240), new("Alas", 120), new("En formación", 600)];
        var rows = new List<IReadOnlyList<Cell>>();
        foreach (var p in Player.Provinces.Select(id => Map.Provinces[id]).Where(p => p.Has(BuildingType.Airfield)).OrderBy(p => Session.PlaceName(p)))
        {
            var forming = p.Training.Where(o => o.Battalion is BattalionType t && t.First().Flies).ToList();
            rows.Add(
            [
                new TextCell(Session.PlaceName(p), Bold: true),
                new TextCell($"{Session.WingsAt(p).Count()}/{MilitaryRules.WingsPerAirfield}", Session.AirfieldRoom(p, Player.Id) > 0 ? Tone.Accent : Tone.Normal),
                new TextCell(forming.Count == 0 ? "-" : string.Join(", ", forming.Select(o => $"{o.Name} ({o.DaysLeft} d)")), forming.Count == 0 ? Tone.Dim : Tone.Normal),
            ]);
        }
        return new TablePage(new Table(columns, rows, Empty: "No tienes aeródromos: constrúyelos (Aviación) en la pestaña Edificios de una provincia."),
            "Aeródromos", Tone.Accent);
    }

    /// <summary>
    /// A row per kind of aircraft: the model it would form, its figures and cost, and a button to form a wing at the
    /// airfield with room nearest the capital.
    /// </summary>
    private TablePage FormWings()
    {
        Column[] columns = [new("Tipo", 200), new("Modelo", 270), new("Aire", 70), new("Tierra", 70), new("Alcance", 100), new("Días", 70), new("Coste", 320), new("", 100)];
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
                new TextCell($"{GameSession.TrainingDays(Player, type)}", Tone.Dim),
                new TextCell($"{model.TrainingCost} y {model.PiecesText(model.Pieces)}", Tone.Dim, TextSize.Small, Top: 8),
                new ButtonsCell([new Button("Formar", () => Show(Session.Train(Player.Id, airfield!.Id, type)), can.Ok,
                    Tooltip: can.Ok ? $"Forma un ala en {Session.PlaceName(airfield!)}: {model.Men} tripulantes de la provincia, su oro y {model.Pieces} aviones del almacén." : can.Message,
                    Size: TextSize.Small)]),
            ]);
        }
        return new TablePage(new Table(columns, rows), "Formar alas · los aviones los fabrican los talleres (pestaña Almacén)", Tone.Accent);
    }
}
