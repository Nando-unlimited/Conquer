using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Presentation;

/// <summary>The unit panel: what the selected unit is and does, its battalions or ships, its command and its orders.</summary>
public sealed partial class GameController
{
    public static string OfficerTooltip(Officer officer) =>
        $"{officer.Title}\n{officer.TraitsDescription}\n" +
        $"{officer.Skill} de {Officer.MaxSkill} estrellas, {officer.Victories} victorias (una estrella más cada {Officer.VictoriesPerStar}); las virtudes mejoran con cada estrella.";

    /// <summary>What to call a city or HQ at the end of a road: the player's city there, else their HQs there.</summary>
    public string HubName(int provinceId)
    {
        var p = Map.Provinces[provinceId];
        if (Session.CityIn(p) is { } city) return city.Name;
        var hqs = Session.Units.Where(u => u.OwnerId == Human.Id && u.IsHeadquarters && u.ProvinceId == provinceId).Select(u => u.Name).ToList();
        return hqs.Count > 0 ? string.Join(", ", hqs) : p.DisplayName;
    }

    private void UnitPanel(Document doc, Unit unit)
    {
        var owner = Session.Players[unit.OwnerId];
        var here = Map.Provinces[unit.ProvinceId];
        doc.Add(new Heading(unit.Name, Tone.Accent, TextSize.Large, Height: 32));
        string kind = unit.Type switch
        {
            UnitType.Regiment => $"{Formations.CombatName(unit.Battalions.Count)} de {Formations.BattalionCount(unit.Battalions.Count)}",
            UnitType.Headquarters => $"Cuartel general de {Formations.LevelName(unit.HeadquartersLevel).ToLowerInvariant()}",
            UnitType.Fleet => $"Flota de {Formations.ShipCount(unit.Battalions.Count)}",
            _ => $"{unit.Citizens:N0} colonos",
        };
        doc.Add(new Label(kind, Tone.Dim, Height: 24));
        doc.Add(new Info("Nación", owner.Name, Ink.Nation(owner.Color)));
        doc.Add(new Info("Ubicación", Session.PlaceName(here)));
        doc.Add(new Info("Estado", UnitState(unit, out var stateTone), stateTone));

        if (unit.IsMilitary || unit.IsFleet) RegimentDetails(doc, unit);
        else if (unit.IsHeadquarters) HeadquartersDetails(doc, unit);
        if (unit.OwnerId != Human.Id) return;

        doc.Add(new Space(6));
        if (unit.IsAboard)
        {
            doc.Add(new Paragraph("Clic derecho en la costa junto a su flota (o en su puerto) para desembarcar. En tierra enemiga sin tropas, desembarcar la ocupa.", Tone.Dim));
            return;
        }
        if (unit.IsMilitary || unit.IsFleet || unit.IsHeadquarters)
            doc.Add(new Button("Editar unidad", () => OpenUnitEditor(unit), Tooltip: "Renombrar, separar y unir tropas, y elegir su oficial."));
        EmbarkButtons(doc, unit);
        if (unit.CanFoundCity)
        {
            var can = Session.CanFoundCity(unit);
            doc.Add(new Button("Fundar ciudad", () => OpenCityNaming(unit.Id, unit.ProvinceId), can.Ok,
                Tooltip: can.Ok ? "Reclama esta provincia y funda una ciudad con estos colonos." : can.Message));
        }
        if (unit.IsMilitary)
        {
            var can = Session.CanClaim(unit);
            doc.Add(new Button("Reclamar provincia", () => Show(Session.Claim(Human.Id, unit.Id)), can.Ok,
                Tooltip: can.Ok ? "Esta provincia libre pasará a ser tuya." : can.Message));
            if (unit.IsScouting)
            {
                string label = unit.AutoClaim ? "Dejar de explorar" : "Explorar y reclamar";
                string tip = unit.AutoClaim ? "Se detiene donde está y vuelve a esperar órdenes."
                    : "Va sola a la mejor provincia libre junto a tus fronteras, la reclama y sigue con la siguiente, sin entrar en tierras ajenas. " +
                      "Darle una orden de movimiento la detiene.";
                doc.Add(new Button(label, () =>
                {
                    if (unit.AutoClaim) Session.MoveUnit(Human.Id, unit.Id, unit.ProvinceId);
                    Show(Session.SetAutoClaim(Human.Id, unit.Id, !unit.AutoClaim));
                }, Active: unit.AutoClaim, Tooltip: tip));
            }
            EngineerButtons(doc, unit, here);
        }
        bool canSettle = here.OwnerId == Human.Id && !here.IsOccupied;
        doc.Add(new ButtonRow(
        [
            new Button(unit.CanFoundCity ? "Asentarse" : "Licenciar", () =>
                {
                    Show(Session.Disband(Human.Id, unit.Id));
                    SelectProvince(here.Id);
                }, canSettle,
                Tooltip: canSettle ? "Disuelve la unidad; sus ciudadanos se quedan a vivir en esta provincia." : "Solo en una provincia tuya.", Size: TextSize.Small),
            new Button("Detener", () =>
                {
                    if (unit.AutoClaim) Session.SetAutoClaim(Human.Id, unit.Id, false);
                    Session.MoveUnit(Human.Id, unit.Id, unit.ProvinceId);
                }, unit.IsMoving || unit.AttackingProvinceId.HasValue || unit.AutoClaim, Size: TextSize.Small),
        ]));
        doc.Add(new Paragraph(unit.IsFleet
            ? "Clic derecho para navegar: por mares costeros con Navegación a vela y por el océano con Cartografía; atraca en tus ciudades con costa. Las flotas enemigas que se encuentran combaten."
            : unit.IsMilitary
            ? "Clic derecho para mover. Mover a una provincia enemiga con tropas la ataca; sin tropas, la ocupa. Solo se entra en tierras de naciones con las que estás en guerra. Para cruzar el mar, clic derecho sobre una flota tuya con transportes."
            : "Clic derecho para mover. No puede entrar en tierras de otras naciones. Para cruzar el mar, clic derecho sobre una flota tuya con transportes.", Tone.Dim));
    }

    /// <summary>
    /// For a unit with engineers: a button for each kind of road its advances allow, which opens the window to choose
    /// where it goes (only from a city or HQ of the player's), and the works under way along whose route it stands.
    /// </summary>
    private void EngineerButtons(Document doc, Unit unit, Province here)
    {
        if (!unit.Battalions.Any(b => b.Type == BattalionType.Engineers)) return;
        bool hub = Session.IsRoadHub(Human.Id, here.Id);
        foreach (var kind in RoadKinds.All.Where(k => Human.Techs.Contains(k.Info().Requires)))
        {
            var info = kind.Info();
            bool can = hub && !unit.IsMoving;
            string tip = !hub ? "Solo desde una provincia con una ciudad o un cuartel general tuyos."
                : unit.IsMoving ? "La unidad está en marcha."
                : $"Elige la ciudad o el cuartel general que quieres unir con {(info.Feminine ? "una" : "un")} {info.Name.ToLowerInvariant()}.";
            doc.Add(new Button($"Construir {info.Name.ToLowerInvariant()}...", () => OpenRoadWindow(here.Id, kind), can, Tooltip: tip, Size: TextSize.Small));
        }
        foreach (var work in Session.RoadProjects.Where(r => r.OwnerId == Human.Id && r.Route.Contains(here.Id)).ToList())
        {
            var info = work.Kind.Info();
            doc.Add(new Label($"{info.Name} a {HubName(work.To)}", Tone.Accent, Bold: true, Height: 18));
            int engineers = Session.EngineersOn(work);
            doc.Add(new Label($"Quedan {Session.LinksLeft(work)} tramos · {Formations.BattalionCount(engineers)} de ingenieros en la ruta", Tone.Dim, Height: 20));
            doc.Add(new Button("Cancelar obra", () => Show(Session.CancelRoad(Human.Id, work.Id)),
                Tooltip: "Se devuelve lo que costaban los tramos sin hacer.", Size: TextSize.Small, Height: 26));
        }
    }

    /// <summary>Buttons to board one of the player's fleets with room, in this province or the sea next to it.</summary>
    private void EmbarkButtons(Document doc, Unit unit)
    {
        if (unit.IsFleet) return;
        var here = Map.Provinces[unit.ProvinceId];
        var fleets = Session.Units.Where(f => f.IsFleet && f.OwnerId == unit.OwnerId && f.Capacity > 0
                                              && (f.ProvinceId == here.Id || here.Neighbors.Contains(f.ProvinceId))).Take(3);
        foreach (var fleet in fleets)
        {
            var can = Session.CanEmbark(unit, fleet);
            int room = fleet.Capacity - Session.CargoMen(fleet);
            doc.Add(new Button($"Embarcar en {fleet.Name} (sitio para {room:N0})", () => Show(Session.Embark(Human.Id, unit.Id, fleet.Id)), can.Ok,
                Tooltip: can.Ok ? null : can.Message, Size: TextSize.Small, Height: 28, Gap: 4));
        }
    }

    /// <summary>What the unit is doing, in words: aboard, fighting, marching (and how long until it arrives) or waiting.</summary>
    public string UnitState(Unit unit, out Tone tone)
    {
        tone = Tone.Normal;
        if (unit.CarrierId is int carrier && Session.UnitById(carrier) is { } fleet) return $"A bordo de {fleet.Name}";
        if (unit.IsFleet && Session.EnemyFleetsIn(unit.ProvinceId, unit.OwnerId).Any())
        {
            tone = Tone.Battle;
            return "Combatiendo en el mar";
        }
        if (unit.AttackingProvinceId is int target)
        {
            tone = Tone.Battle;
            return $"Atacando {Session.PlaceName(Map.Provinces[target])}";
        }
        if (Session.InBattle(unit))
        {
            tone = Tone.Battle;
            return "Defendiendo";
        }
        if (unit.IsMoving && unit.Destination is int dest)
        {
            double hours = unit.HoursToNext;
            for (int i = 0; i + 1 < unit.Path.Count; i++) hours += Session.Pathfinder.StepHours(unit.Path[i], unit.Path[i + 1]) / unit.Speed;
            return (unit.AutoClaim ? "Explorando hacia " : "Hacia ") + $"{Session.PlaceName(Map.Provinces[dest])} ({GameSession.FormatHours(hours)})";
        }
        return unit.AutoClaim ? "Explorando" : "Esperando órdenes";
    }

    /// <summary>A regiment's battalions, or a fleet's ships and cargo (split and merged in the unit editor).</summary>
    private void RegimentDetails(Document doc, Unit unit)
    {
        if (unit.IsFleet)
        {
            doc.Add(new Info("Velocidad en el mar", $"{unit.Speed * GameRules.SailingSpeed * GameRules.CitizenSpeedKmh:0.#} km/h"));
            bool port = Session.IsPort(Map.Provinces[unit.ProvinceId], unit.OwnerId);
            doc.Add(new Info("Reparaciones", port ? "En puerto" : "Solo en puerto", port ? Tone.Good : Tone.Dim));
            doc.Add(OfficerLine("Oficial", unit.Officer, "Manda esta flota: sus rasgos y su habilidad afectan al fuego de sus barcos."));
            doc.Add(PortraitsOf(unit, ("Oficial", unit.Officer)));
            if (unit.Capacity > 0)
            {
                doc.Add(new Info("Carga", $"{Session.CargoMen(unit):N0} / {unit.Capacity:N0} hombres"));
                foreach (var cargo in Session.CargoOf(unit))
                    doc.Add(new Label($"{cargo.Name} ({cargo.Citizens:N0})", Tone.Normal, Height: 18, Indent: 8));
            }
        }
        else
        {
            bool supplied = Session.IsInSupply(unit) || unit.IsAboard;
            doc.Add(new Info("Suministro", unit.IsAboard ? "Víveres del barco" : supplied ? "Con suministro" : "Sin suministro", supplied ? Tone.Good : Tone.Bad));
            doc.Add(new Info("Velocidad", $"{unit.Speed * GameRules.CitizenSpeedKmh:0.#} km/h"));
            doc.Add(CommandLine(unit));
            doc.Add(OfficerLine("Oficial", unit.Officer, "Manda esta unidad."));
            doc.Add(OfficerLine("General", Session.GeneralOf(unit), "Manda las unidades de su cuartel general que estén a su alcance."));
            doc.Add(PortraitsOf(unit, ("Oficial", unit.Officer), ("General", Session.GeneralOf(unit))));
            if (unit.Battalions.Count > 0)
            {
                double xp = unit.Battalions.Sum(b => b.Experience * b.Strength) / Math.Max(1, unit.Battalions.Sum(b => b.Strength));
                doc.Add(new Info("Experiencia", $"{Battalion.ExperienceName(xp)} ({xp:P0})", Tone.Normal,
                    $"Cada batallón gana experiencia combatiendo: hasta +{MilitaryRules.ExperienceBonus:P0} de fuego.\nLos reclutas nuevos la diluyen."));
            }
        }
        doc.Add(new Space(4));

        foreach (var b in unit.Battalions)
        {
            string tip = $"{b.Info.Name}: ataque {b.Info.Attack:0.#}, defensa {b.Info.Defense:0.#}, organización {b.Organisation:0}/{b.Info.MaxOrganisation:0}" +
                         $"\n{b.Type.Role().Name()}, {Battalion.ExperienceName(b.Experience).ToLowerInvariant()} ({b.Experience:P0} de experiencia)" +
                         (b.Info.Mounted ? "\nMontada: rápida, pero ataca a la mitad en bosques, pantanos y montañas." : "") +
                         (b.Info.Capacity > 0 ? $"\nLleva {b.Info.Capacity:N0} hombres." : "");
            doc.Add(new Row(Formations.BattalionName(b.Info), $"{b.Strength:0}/{b.Info.Men}", Tone.Normal, Tone.Dim, Bold: true, Height: 19,
                Icon: new BattalionIcon(b.Type), Tooltip: tip));
            doc.Add(new Bar(b.StrengthShare, Tone.Strength, Tone.Track, 4, 1));
            doc.Add(new Bar(b.OrganisationShare, Tone.Organisation, Tone.Track, 4, 7));
        }
        if (unit.OwnerId != Human.Id || unit.IsAboard) return;
        if (!unit.IsFleet) AttachButtons(doc, unit);
    }

    private void HeadquartersDetails(Document doc, Unit hq)
    {
        var info = CommandLevels.Info(hq.HeadquartersLevel);
        doc.Add(new Info("Alcance", $"{info.RangeKm:N0} km"));
        doc.Add(CommandLine(hq));
        doc.Add(OfficerLine("General", hq.Officer, "Manda las unidades de su cuartel general que estén a su alcance."));
        doc.Add(PortraitsOf(hq, ("General", hq.Officer)));
        var subs = Session.SubordinatesOf(hq).ToList();
        string below = Formations.SubordinatesPlural(hq.HeadquartersLevel);
        doc.Add(Section($"Al mando ({subs.Count}/{info.MaxSubordinates} {below})", 24));
        foreach (var sub in subs)
        {
            bool inRange = Session.InCommandRange(sub);
            doc.Add(new Row(sub.Name, inRange ? "" : "fuera de alcance", inRange ? Tone.Normal : Tone.Bad, Tone.Bad, Indent: 8));
        }
        if (subs.Count == 0) doc.Add(new Label("Nadie todavía: asígnale unidades desde su panel.", Tone.Dim, Height: 20, Indent: 8));
        if (hq.OwnerId == Human.Id) AttachButtons(doc, hq);
    }

    /// <summary>An officer leading the unit, or its HQ's general, with their traits and what they do on hover.</summary>

    /// <summary>The portraits of the unit's officer and general, in its nation's colour and era; empty if it has neither.</summary>
    private Element PortraitsOf(Unit unit, params (string Role, Officer? Officer)[] officers)
    {
        var owner = Session.Players[unit.OwnerId];
        var shown = officers.Where(o => o.Officer != null)
            .Select(o => (Portrait.Of(o.Officer!, owner.Era, owner.Color), o.Role, OfficerTooltip(o.Officer!))).ToList();
        return shown.Count == 0 ? new Space(0) : new Portraits(shown);
    }
    private static Info OfficerLine(string label, Officer? officer, string role) =>
        officer == null
            ? new Info(label, "Ninguno", Tone.Dim)
            // Just the name and stars fit; the traits are in the tooltip, and a flawed officer is not shown in green.
            : new Info(label, $"{officer.Name} {new string('*', officer.Skill)}", officer.Traits.Any(Officer.IsFlaw) ? Tone.Normal : Tone.Good,
                $"{OfficerTooltip(officer)}\n{role}");

    /// <summary>Who the unit reports to, whether that HQ is in range, and the bonus it gives.</summary>
    private Info CommandLine(Unit unit)
    {
        if (Session.CommanderOf(unit) is not { } hq)
            return new Info("Mando", unit.HeadquartersLevel >= CommandLevels.Highest ? "Mando supremo" : "Sin cuartel general", Tone.Dim);
        bool inRange = Session.InCommandRange(unit);
        double bonus = Session.CommandBonus(unit);
        return new Info("Mando", inRange ? $"{hq.Name} (+{bonus:P0})" : $"{hq.Name} (lejos)", inRange ? Tone.Good : Tone.Bad);
    }

    /// <summary>Buttons to put the unit under one of the nearest HQs of the level above, or to leave its HQ.</summary>
    private void AttachButtons(Document doc, Unit unit)
    {
        if (unit.CommandLevel >= CommandLevels.Highest) return;
        var here = Map.Provinces[unit.ProvinceId];
        var hqs = Session.Units.Where(u => u.IsHeadquarters && u.OwnerId == unit.OwnerId && u.HeadquartersLevel == unit.CommandLevel + 1 && u.Id != unit.CommanderId)
            .OrderBy(u => Map.DistanceKm(here, Map.Provinces[u.ProvinceId])).Take(3).ToList();
        foreach (var hq in hqs)
        {
            var can = Session.CanAttach(unit, hq);
            double km = Map.DistanceKm(here, Map.Provinces[hq.ProvinceId]);
            doc.Add(new Button($"Bajo el mando de {hq.Name} ({km:N0} km)", () => Show(Session.Attach(Human.Id, unit.Id, hq.Id)), can.Ok,
                Tooltip: can.Ok ? null : can.Message, Size: TextSize.Small, Height: 26, Gap: 4));
        }
        if (unit.CommanderId.HasValue)
            doc.Add(new Button("Quitar del mando", () => Show(Session.Detach(Human.Id, unit.Id)), Size: TextSize.Small, Height: 26, Gap: 4));
        if (hqs.Count == 0 && unit.CommanderId is null)
            doc.Add(new Label($"Forma un cuartel de {Formations.LevelName(unit.CommandLevel + 1).ToLowerInvariant()} en una ciudad para darle mando.", Tone.Dim));
    }
}
