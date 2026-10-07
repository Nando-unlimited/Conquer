using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Presentation;

/// <summary>The air force in the panels: air units with their missions, the sky over a province, and dropping paratroopers.</summary>
public sealed partial class GameController
{
    /// <summary>
    /// An air unit: its name, escuadrillas and planes with their bars, its División aérea and mission and, to its owner,
    /// a button per mission it can fly (each asks for the target on the map), and buttons to join the others of its kind
    /// at its base, split off an escuadrilla, change its división, land it and disband it.
    /// </summary>
    private void AirUnitRows(Document doc, AirUnit unit)
    {
        var info = unit.Info;
        int range = (int)unit.Flights.Min(f => f.Info.RangeKm);
        doc.Add(new Row(unit.Name, $"{unit.PlaneCount:0}/{unit.FullPlanes} aviones", Tone.Normal, Tone.Dim, Bold: true, Height: 19, Icon: new BattalionIcon(unit.Type),
            Tooltip: $"{unit.Size.Name()} de {unit.Flights.Count} escuadrilla{(unit.Flights.Count == 1 ? "" : "s")} de {info.Name.ToLowerInvariant()}: " +
                     $"fuego en tierra {info.Attack:0}, en el aire {info.AirAttack:0}, defensa {info.Defense:0} por escuadrilla; alcance {range:N0} km." +
                     (info.Capacity > 0 ? $" Lleva {unit.Flights.Sum(f => f.Info.Capacity):N0} paracaidistas." : "") +
                     $"\nTripulaciones {unit.Crews:N0}. Organización {unit.OrganisationShare:P0}; con menos del {MilitaryRules.MinFlyingOrganisation:P0} no despega."));
        doc.Add(new Bar(unit.StrengthShare, Tone.Strength, Tone.Track, 4, 1));
        doc.Add(new Bar(unit.OrganisationShare, Tone.Organisation, Tone.Track, 4, 4));
        var division = Session.DivisionOf(unit);
        if (division != null)
            doc.Add(new Label($"{division.Name}" + (Session.InAirCommand(unit) ? $" (+{Session.AirCommandBonus(unit):P0})" : " (lejos)"),
                Session.InAirCommand(unit) ? Tone.Good : Tone.Bad, Height: 20, Indent: 8));
        string mission = unit.TargetProvinceId is int t ? $"{GameSession.AirMissionName(unit.Mission)} sobre {Session.PlaceName(Map.Provinces[t])}" : GameSession.AirMissionName(unit.Mission);
        bool flying = Session.IsFlying(unit);
        doc.Add(new Label(mission + (unit.Mission is not (AirMission.None or AirMission.Paradrop) && !flying ? " (en tierra)" : ""),
            unit.Mission == AirMission.None ? Tone.Dim : flying ? Tone.Good : Tone.Accent, Height: 20, Indent: 8, Tooltip: GameSession.AirMissionDescription(unit.Mission)));
        if (unit.OwnerId != Human.Id) return;

        var missions = GameSession.MissionsFor(unit.Type).Where(m => m != AirMission.Paradrop).Select(m => new Button(GameSession.AirMissionName(m) + "...",
            () => ChooseTarget($"Clic: {GameSession.AirMissionName(m).ToLowerInvariant()} de {unit.Name} (alcance {range:N0} km)",
                id => Session.SetAirMission(Human.Id, unit.Id, m, id)),
            Active: unit.Mission == m, Tooltip: GameSession.AirMissionDescription(m) + " Elige la provincia en el mapa.", Size: TextSize.Small)).ToList();
        missions.Add(new Button("Aterrizar", () => Show(Session.SetAirMission(Human.Id, unit.Id, AirMission.None)), unit.Mission != AirMission.None,
            Tooltip: "Deja su misión y se queda en su base.", Size: TextSize.Small));
        doc.Add(new ButtonRow(missions, Height: 24, Gap: 4, Spacing: 4));

        var others = Session.AirUnits.Where(o => o != unit && Session.CanJoin(unit, o).Ok).ToList();
        var divisions = Session.AirHeadquarters.Where(h => h.OwnerId == Human.Id && !h.IsCommand).ToList();
        int? next = divisions.Count == 0 ? null
            : division == null ? divisions[0].Id
            : divisions.IndexOf(division) + 1 < divisions.Count ? divisions[divisions.IndexOf(division) + 1].Id : null;
        doc.Add(new ButtonRow(
        [
            new Button("Unir", () => { foreach (var o in others) Show(Session.JoinAirUnits(Human.Id, unit.Id, o.Id)); }, others.Count > 0,
                Tooltip: $"Une a esta las demás unidades de {unit.Type.Line().Name.ToLowerInvariant()} de la base: escuadrón (3 escuadrillas), grupo (18) y ala (54).",
                Size: TextSize.Small),
            new Button("Separar", () => Show(Session.SplitAirUnit(Human.Id, unit.Id, 1)), unit.Flights.Count > 1,
                Tooltip: "Saca una escuadrilla como unidad propia.", Size: TextSize.Small),
            new Button(divisions.Count == 0 ? "Sin divisiones" : "División", () => Show(Session.AttachAirUnit(Human.Id, unit.Id, next)), divisions.Count > 0,
                Tooltip: "Pasa a la siguiente división aérea (o a ninguna). Una división manda las unidades a su alcance y les da un bonus; fórmalas en la pestaña Fuerza aérea.",
                Size: TextSize.Small),
            new Button("Disolver", () => Show(Session.DisbandAirUnit(Human.Id, unit.Id)), Tooltip: "Sus tripulaciones vuelven a la reserva y sus aviones al almacén.",
                Size: TextSize.Small),
        ], Height: 24, Gap: 8, Spacing: 4));
    }

    /// <summary>Who rules the sky over a province, for its panel; nothing while no fighters fly over it.</summary>
    private void SkyLine(Document doc, Province p)
    {
        if (Session.AirSuperiority(Human.Id, p) is not double share) return;
        string state = share >= MilitaryRules.AirRuleShare ? "Dominamos el aire" : 1 - share >= MilitaryRules.AirRuleShare ? "El enemigo domina el aire" : "Disputado";
        doc.Add(new Info("Cielo", $"{state} ({share:P0})", share >= MilitaryRules.AirRuleShare ? Tone.Good : 1 - share >= MilitaryRules.AirRuleShare ? Tone.Bad : Tone.Accent,
            $"Tu parte de los cazas que vuelan sobre la provincia. Tus tropas luchan hasta un {MilitaryRules.AirSuperiorityBonus:P0} mejor o peor según quién domina el aire; " +
            $"bajo un cielo enemigo marchan más despacio, reciben menos suministro y tus bombarderos hacen la mitad."));
    }

    /// <summary>For a unit of paratroopers at an airfield with transports: a button to pick where to drop it.</summary>
    private void ParadropButton(Document doc, Unit unit)
    {
        if (unit.OwnerId != Human.Id || !unit.IsMilitary || unit.Battalions.Any(b => b.Type != BattalionType.Paratroopers)) return;
        bool transports = Session.AirUnits.Any(u => u.OwnerId == Human.Id && u.Type == BattalionType.AirTransports && u.BaseProvinceId == unit.ProvinceId);
        doc.Add(new Button("Lanzar en paracaídas...", () => ChooseTarget($"Clic: dónde saltan los paracaidistas de {unit.Name}", id => Session.Paradrop(Human.Id, unit.Id, id)),
            transports, Tooltip: transports
                ? "Elige en el mapa una provincia sin tropas enemigas al alcance de los transportes de este aeródromo."
                : "Hacen falta aviones de transporte en el aeródromo donde está la unidad.", Size: TextSize.Small, Height: 28, Gap: 4));
    }
}
