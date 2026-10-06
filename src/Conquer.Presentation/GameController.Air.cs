using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Presentation;

/// <summary>The air force in the panels: wings with their missions, the sky over a province, and dropping paratroopers.</summary>
public sealed partial class GameController
{
    /// <summary>
    /// A wing: its name and planes with their bars, its mission and, to its owner, a button per mission it can fly
    /// (each asks for the target on the map), one to stand it down and one to disband it.
    /// </summary>
    private void WingRows(Document doc, AirWing wing)
    {
        var info = wing.Info;
        doc.Add(new Row(wing.Name, $"{wing.PlaneCount:0}/{info.Pieces} aviones", Tone.Normal, Tone.Dim, Bold: true, Height: 19, Icon: new BattalionIcon(wing.Type),
            Tooltip: $"{info.Name}: fuego en tierra {info.Attack:0}, en el aire {info.AirAttack:0}, defensa {info.Defense:0}, alcance {info.RangeKm:N0} km." +
                     (info.Capacity > 0 ? $" Lleva {info.Capacity} paracaidistas." : "") +
                     $"\nOrganización {wing.Planes.OrganisationShare:P0}; con menos del {MilitaryRules.MinFlyingOrganisation:P0} no despega."));
        doc.Add(new Bar(wing.Planes.StrengthShare, Tone.Strength, Tone.Track, 4, 1));
        doc.Add(new Bar(wing.Planes.OrganisationShare, Tone.Organisation, Tone.Track, 4, 4));
        string mission = wing.TargetProvinceId is int t ? $"{GameSession.AirMissionName(wing.Mission)} sobre {Session.PlaceName(Map.Provinces[t])}" : GameSession.AirMissionName(wing.Mission);
        bool flying = Session.IsFlying(wing);
        doc.Add(new Label(mission + (wing.Mission != AirMission.None && wing.Mission != AirMission.Paradrop && !flying ? " (en tierra)" : ""),
            wing.Mission == AirMission.None ? Tone.Dim : flying ? Tone.Good : Tone.Accent, Height: 20, Indent: 8, Tooltip: GameSession.AirMissionDescription(wing.Mission)));
        if (wing.OwnerId != Human.Id) return;

        var buttons = GameSession.MissionsFor(wing.Type).Where(m => m != AirMission.Paradrop).Select(m => new Button(GameSession.AirMissionName(m) + "...",
            () => ChooseTarget($"Clic: {GameSession.AirMissionName(m).ToLowerInvariant()} de {wing.Name} (alcance {info.RangeKm:N0} km)",
                id => Session.SetAirMission(Human.Id, wing.Id, m, id)),
            Active: wing.Mission == m, Tooltip: GameSession.AirMissionDescription(m) + " Elige la provincia en el mapa.", Size: TextSize.Small)).ToList();
        buttons.Add(new Button("Aterrizar", () => Show(Session.SetAirMission(Human.Id, wing.Id, AirMission.None)), wing.Mission != AirMission.None,
            Tooltip: "Deja su misión y se queda en su base.", Size: TextSize.Small));
        buttons.Add(new Button("Disolver", () => Show(Session.DisbandWing(Human.Id, wing.Id)), Tooltip: "Sus tripulaciones vuelven a la reserva y sus aviones al almacén.",
            Size: TextSize.Small));
        doc.Add(new ButtonRow(buttons, Height: 24, Gap: 8, Spacing: 4));
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
        bool transports = Session.Wings.Any(w => w.OwnerId == Human.Id && w.Type == BattalionType.AirTransports && w.BaseProvinceId == unit.ProvinceId);
        doc.Add(new Button("Lanzar en paracaídas...", () => ChooseTarget($"Clic: dónde saltan los paracaidistas de {unit.Name}", id => Session.Paradrop(Human.Id, unit.Id, id)),
            transports, Tooltip: transports
                ? "Elige en el mapa una provincia sin tropas enemigas al alcance de los transportes de este aeródromo."
                : "Hacen falta aviones de transporte en el aeródromo donde está la unidad.", Size: TextSize.Small, Height: 28, Gap: 4));
    }
}
