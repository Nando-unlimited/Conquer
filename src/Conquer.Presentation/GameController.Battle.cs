using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Simulation;

namespace Conquer.Presentation;

/// <summary>A bar split by each side's fire this hour, in their nations' colours: who is winning the exchange.</summary>
public sealed record FireBalance(string Label, double AttackerShare, uint AttackerColor, uint DefenderColor, string Tooltip);

/// <summary>
/// Each side's organisation hour by hour (0 to 1), in their colours, with the line below which units break;
/// <see cref="Describe"/> says what happened at an hour (an index into the points).
/// </summary>
public sealed record OrganisationChart(IReadOnlyList<(double Attacker, double Defender)> Points, uint AttackerColor, uint DefenderColor,
    double Breaking, string Since, Func<int, string> Describe);

/// <summary>
/// The window of a battle on land or at sea: its title and state, a line about the ground, the balance of fire, a
/// column per side, a message when it is over, the organisation chart, and the buttons to go there or close.
/// </summary>
public sealed record BattleWindow(string Title, string State, Ink StateInk, string Note, float NoteHeight, FireBalance? Fire,
    IReadOnlyList<Document> Sides, string? Ended, OrganisationChart? Chart, Button GoTo, Button Close);

/// <summary>The window of a battle: which one is on show, and its contents.</summary>
public sealed partial class GameController
{
    /// <summary>The land battle on show; kept after it ends to show how it went.</summary>
    private Battle? _viewedBattle;
    /// <summary>The sea province whose naval battle is on show.</summary>
    private int? _viewedNavalBattle;

    public bool BattleWindowOpen => _viewedBattle != null || _viewedNavalBattle.HasValue;

    /// <summary>Shows a land battle, or the naval battle in <paramref name="provinceId"/> when <paramref name="battle"/> is null.</summary>
    public void OpenBattle(int provinceId, Battle? battle)
    {
        _viewedBattle = battle;
        _viewedNavalBattle = battle == null ? provinceId : null;
    }

    public void CloseBattle()
    {
        _viewedBattle = null;
        _viewedNavalBattle = null;
    }

    /// <summary>Opens the first battle under way, if there is one.</summary>
    public void OpenFirstBattle()
    {
        if (Session.Battles.FirstOrDefault() is { } battle) OpenBattle(battle.ProvinceId, battle);
        else if (Session.NavalBattleProvinces().FirstOrDefault(-1) is var sea and >= 0) OpenBattle(sea, null);
    }

    public BattleWindow? BattleWindow()
    {
        if (!BattleWindowOpen) return null;
        int provinceId = _viewedBattle?.ProvinceId ?? _viewedNavalBattle!.Value;
        var goTo = new Button("Ir a la provincia", () =>
        {
            Camera.LookAt(Center(provinceId));
            CloseBattle();
        }, Tooltip: "Centra el mapa en la batalla y cierra la ventana.");
        var close = new Button("Cerrar", CloseBattle);
        return _viewedBattle is { } battle ? LandBattle(battle, goTo, close) : NavalBattle(provinceId, goTo, close);
    }

    private static Pair Stat(string label, string value, Ink ink = default) => new(label, value, ink, Size: TextSize.Small, Height: 19);

    private static string Casualties(double men) => Math.Round(men) == 1 ? "1 baja" : $"{men:N0} bajas";

    // ------------------------------------------------------------------ on land

    private readonly record struct SideStats(double Men, double Organisation, double Fire);

    private BattleWindow LandBattle(Battle battle, Button goTo, Button close)
    {
        var p = Map.Provinces[battle.ProvinceId];
        bool ongoing = Session.Battles.Contains(battle);
        long hours = (battle.EndHours ?? Session.Date.Hours) - battle.StartHours;
        string state = (ongoing ? "En curso"
            : battle.AttackersWon == true ? "Los atacantes toman la provincia"
            : "Los defensores resisten") + $" · {GameSession.FormatHours(hours)}";

        // While it lasts, the sides as they stand; afterwards, what the last hour left of them.
        var attackers = battle.Attackers.Select(Session.UnitById).OfType<Unit>().Where(u => u.AttackingProvinceId == battle.ProvinceId).ToList();
        var defenders = ongoing ? Session.EnemyRegimentsIn(battle.ProvinceId, battle.AttackerId).ToList() : [];
        bool engineers = GameSession.HasEngineers(attackers);
        var attacking = ongoing ? Session.Engage(attackers, p, attacking: true) : [];
        var defending = ongoing ? Session.Engage(defenders, p, attacking: false, engineers) : [];
        (attacking, defending) = GameSession.FaceEachOther(attacking, defending);

        int front = MilitaryRules.FrontWidth(p.Biome);
        double defense = GameSession.DefenseMultiplier(p, engineers), unengineered = GameSession.DefenseMultiplier(p);
        string note = $"{p.Info.Name}{(p.HasRiver ? " con río" : "")} · defensa ×{defense:0.##}" +
                      (defense < unengineered ? $" (×{unengineered:0.##} sin los ingenieros del atacante)" : "") + " · " +
                      $"frente de {front} batallones, con hasta {front / 2} de artillería, aviación e ingenieros detrás";
        var last = battle.History.Count > 0 ? battle.History[^1] : default;
        var attackSide = ongoing
            ? new SideStats(attackers.Sum(u => u.Citizens), GameSession.AverageOrganisation(attackers), GameSession.ExpectedFire(attacking))
            : new SideStats(last.AttackerMen, last.AttackerOrganisation, last.AttackerFire);
        var defenseSide = ongoing
            ? new SideStats(defenders.Sum(u => u.Citizens), GameSession.AverageOrganisation(defenders), GameSession.ExpectedFire(defending))
            : new SideStats(last.DefenderMen, last.DefenderOrganisation, last.DefenderFire);

        double total = attackSide.Fire + defenseSide.Fire;
        var fire = new FireBalance($"{(ongoing ? "Fuego por hora" : "Fuego en la última hora")}: {attackSide.Fire:0.#} contra {defenseSide.Fire:0.#}",
            total <= 0 ? 0.5 : attackSide.Fire / total, Session.Players[battle.AttackerId].Color, Session.Players[battle.DefenderId].Color,
            "El fuego de los batallones que combaten, con sus armas combinadas y antes de la suerte (±" +
            $"{MilitaryRules.CombatRandomness:P0}). Cada punto quita {MilitaryRules.OrganisationDamage:0.##} de organización " +
            $"y {MilitaryRules.StrengthDamage:0.##} hombres al otro bando.");

        return new BattleWindow($"Batalla por {Session.PlaceName(p)}", state, ongoing ? Tone.Bad : Tone.Accent, note, 26, fire,
        [
            BattleSide("Atacante", battle.AttackerId, attackSide, battle.AttackerLosses, attackers, attacking, true, ongoing),
            BattleSide("Defensor", battle.DefenderId, defenseSide, battle.DefenderLosses, defenders, defending, false, ongoing),
        ], null, battle.History.Count >= 2 ? Chart(battle) : null, goTo, close);
    }

    private Document BattleSide(string role, int playerId, SideStats side, double losses, List<Unit> units, List<GameSession.Engaged> engaged,
        bool attacking, bool ongoing)
    {
        var player = Session.Players[playerId];
        var doc = new Document();
        doc.Add(new Banner(player.Name, player.Color, role));
        doc.Add(Stat("Hombres", $"{side.Men:N0}"));
        doc.Add(Stat("Bajas", $"{losses:N0}", losses > 0 ? Tone.Bad : Tone.Dim));
        doc.Add(Stat("Organización", $"{side.Organisation:P0}", side.Organisation < 0.25 ? Tone.Bad : Tone.Normal));
        // The notch is where a unit breaks: defenders retreat, attackers give up.
        doc.Add(new Bar(side.Organisation, Tone.Organisation, Tone.Track, 6, 8, MilitaryRules.BreakingOrganisation));
        doc.Add(Stat("Fuego por hora", $"{side.Fire:0.#}  ({Casualties(side.Fire * MilitaryRules.StrengthDamage)} al enemigo)"));
        if (!ongoing)
        {
            doc.Add(new Space(6));
            return doc;
        }

        int total = units.Sum(u => u.Battalions.Count);
        int support = engaged.Count(e => e.Exposure < 1);
        doc.Add(Stat("Combaten", $"{engaged.Count - support} en el frente, {support} detrás, {total - engaged.Count} en reserva"));
        doc.Add(Stat("Armas combinadas", $"+{GameSession.CombinedArms(engaged.Select(e => e.Role)):P0}"));
        doc.Add(new Space(8));
        doc.Add(new Label(units.Count == 1 ? "1 unidad" : $"{units.Count} unidades", Tone.Normal, Bold: true));
        foreach (var unit in units) doc.Add(BattleUnit(unit, engaged.Where(e => e.Unit == unit).ToList(), attacking));
        return doc;
    }

    /// <summary>
    /// A unit in the fight: its name and officer, its men and how many of its battalions are in the line, with its
    /// strength and organisation bars. Hovering lists every battalion and where it stands.
    /// </summary>
    private UnitEntry BattleUnit(Unit unit, List<GameSession.Engaged> mine, bool attacking)
    {
        bool supplied = Session.IsInSupply(unit);
        int count = unit.Battalions.Count;
        string fighting = mine.Count == count ? count == 1 ? "su batallón combate" : $"sus {count} batallones combaten"
            : mine.Count == 0 ? "todo en reserva" : $"combaten {mine.Count} de {count} batallones";

        var lines = new List<string> { unit.Name };
        double command = Session.CommandBonus(unit);
        if (command > 0) lines.Add($"Cadena de mando: +{command:P0}");
        if (Session.GeneralOf(unit) is { } general) lines.Add($"General: {general.Title} ({general.Summary})");
        if (unit.Officer is { } officer) lines.Add($"Oficial: {officer.Title} ({officer.Summary})");
        foreach (var b in unit.Battalions)
        {
            var e = mine.FirstOrDefault(m => m.Battalion == b);
            string where = e.Battalion == null ? "en reserva" : e.Exposure < 1 ? "detrás del frente" : "en el frente";
            string fire = e.Battalion == null ? "" : $" · fuego {e.Fire:0.#}";
            lines.Add($"{Formations.BattalionName(b.Info)}: {b.Strength:0}/{b.Info.Men} hombres · organización {b.OrganisationShare:P0} · " +
                      $"experiencia {b.Experience:P0} · {where}{fire}");
        }
        lines.Add(attacking ? "Ataca desde su provincia; entra cuando no quedan defensores." : "Defiende con la ventaja del terreno.");

        return new UnitEntry(unit.Name, unit.OwnerId == Human.Id ? Tone.Accent : Tone.Normal, unit.Officer?.Title ?? "sin oficial",
            unit.Officer == null ? Tone.Dim : Tone.Normal, $"{unit.Citizens:N0} hombres · {fighting}" + (supplied ? "" : " · sin suministro"),
            supplied ? Tone.Dim : Tone.Bad, unit.StrengthShare, unit.OrganisationShare, string.Join("\n", lines));
    }

    /// <summary>Each side's organisation, hour by hour.</summary>
    private OrganisationChart Chart(Battle battle)
    {
        var history = battle.History;
        int n = history.Count;
        string attacker = Session.Players[battle.AttackerId].Name, defender = Session.Players[battle.DefenderId].Name;
        return new OrganisationChart(history.Select(h => (h.AttackerOrganisation, h.DefenderOrganisation)).ToList(),
            Session.Players[battle.AttackerId].Color, Session.Players[battle.DefenderId].Color, MilitaryRules.BreakingOrganisation,
            $"hace {GameSession.FormatHours(n - 1)}", at =>
            {
                var h = history[at];
                return (at == n - 1 ? "Ahora" : $"Hace {GameSession.FormatHours(n - 1 - at)}") + "\n" +
                       $"{attacker}: {h.AttackerMen:N0} hombres, organización {h.AttackerOrganisation:P0}, fuego {h.AttackerFire:0.#}\n" +
                       $"{defender}: {h.DefenderMen:N0} hombres, organización {h.DefenderOrganisation:P0}, fuego {h.DefenderFire:0.#}";
            });
    }

    // ------------------------------------------------------------------ at sea

    private BattleWindow NavalBattle(int provinceId, Button goTo, Button close)
    {
        var p = Map.Provinces[provinceId];
        bool ongoing = Session.NavalBattleProvinces().Contains(provinceId);
        var sides = new List<Document>();
        if (ongoing)
            foreach (var group in Session.Units.Where(u => u.IsFleet && u.ProvinceId == provinceId).GroupBy(u => u.OwnerId))
            {
                var fleets = group.ToList();
                var player = Session.Players[group.Key];
                double organisation = GameSession.AverageOrganisation(fleets);
                var doc = new Document();
                doc.Add(new Banner(player.Name, player.Color));
                doc.Add(Stat("Barcos", $"{fleets.Sum(f => f.Battalions.Count)}"));
                doc.Add(Stat("Tripulantes", $"{fleets.Sum(f => f.Citizens):N0}"));
                doc.Add(Stat("Organización", $"{organisation:P0}", organisation < 0.25 ? Tone.Bad : Tone.Normal));
                doc.Add(new Bar(organisation, Tone.Organisation, Tone.Track, 6, 8));
                doc.Add(Stat("Fuego por hora", $"{fleets.Sum(GameSession.ExpectedNavalFire):0.#}"));
                doc.Add(new Space(8));
                foreach (var fleet in fleets)
                {
                    int aboard = Session.CargoOf(fleet).Count();
                    doc.Add(new UnitEntry(fleet.Name, fleet.OwnerId == Human.Id ? Tone.Accent : Tone.Normal, "", Tone.Dim,
                        $"{Formations.ShipCount(fleet.Battalions.Count)} · {fleet.Citizens:N0} tripulantes" + (aboard > 0 ? $" · {aboard} a bordo" : ""),
                        Tone.Dim, fleet.StrengthShare, fleet.OrganisationShare));
                }
                sides.Add(doc);
            }
        return new BattleWindow($"Batalla naval en {p.DisplayName}", ongoing ? "En curso" : "Terminada", ongoing ? Tone.Bad : Tone.Accent,
            "Cada nación dispara sobre todos los barcos enemigos; las flotas rotas huyen a mar abierto o se hunden.", 30, null, sides,
            ongoing ? null : "Ya no quedan flotas enemigas frente a frente.", null, goTo, close);
    }
}
