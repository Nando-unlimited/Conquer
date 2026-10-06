using Conquer.Game.Economy;
using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Simulation;

namespace Conquer.Presentation;

/// <summary>The Ejército (order of battle), Plantillas and Diplomacia tabs of the nation screen.</summary>
public sealed partial class NationScreen
{
    private int _selectedTemplateId = -1;

    // ------------------------------------------------------------------ army

    /// <summary>
    /// The order of battle: every HQ with the units under it, as a tree, then the regiments without an
    /// HQ and the fleets. Each row shows where the unit is, its men, organisation, supply and what it is doing.
    /// </summary>
    private TablePage Army()
    {
        var mine = Session.Units.Where(u => u.OwnerId == Player.Id && (u.CommandLevel >= 0 || u.IsFleet)).ToList();
        var regiments = mine.Where(u => u.IsMilitary).ToList();
        string title = $"{TextFormat.Plural(regiments.Count, "unidad de combate", Formations.CombatPlural)} · {Formations.BattalionCount(regiments.Sum(u => u.Battalions.Count))} · {regiments.Sum(u => u.Citizens):N0} hombres · " +
                       $"reclutas {Player.Manpower:N0}/{Session.ManpowerCapacity(Player):N0} · poder militar {Session.MilitaryPower(Player.Id):0} · mantenimiento {Session.Upkeep(Player)[(int)ResourceType.Gold]:0.#} de oro/día" +
                       (Player.ArmyUnpaid ? " (sin pagar)" : "");

        var units = new List<(Unit Unit, int Depth)>();
        void AddTree(Unit unit, int depth)
        {
            units.Add((unit, depth));
            foreach (var sub in Session.SubordinatesOf(unit).OrderByDescending(u => u.CommandLevel).ThenBy(u => u.Name)) AddTree(sub, depth + 1);
        }
        foreach (var top in mine.Where(u => u.IsHeadquarters && Session.CommanderOf(u) is null).OrderByDescending(u => u.HeadquartersLevel).ThenBy(u => u.Name))
            AddTree(top, 0);
        foreach (var loose in regiments.Where(u => Session.CommanderOf(u) is null).OrderBy(u => u.Name)) units.Add((loose, 0));
        foreach (var fleet in mine.Where(u => u.IsFleet).OrderBy(u => u.Name)) units.Add((fleet, 0));

        Column[] columns = [new("Unidad", 290), new("Ubicación", 170), new("Hombres", 100), new("Organización", 130), new("Suministro", 110), new("Estado", 170), new("", 60)];
        var rows = new List<IReadOnlyList<Cell>>();
        foreach (var (unit, depth) in units)
        {
            Ink nameInk = unit.CommanderId.HasValue && !Session.InCommandRange(unit) ? Tone.Bad : unit.IsHeadquarters ? Tone.Accent : Tone.Normal;
            var p = Map.Provinces[unit.ProvinceId];
            var cells = new List<Cell>
            {
                new TextCell((depth > 0 ? "· " : "") + unit.Name, nameInk, Bold: unit.IsHeadquarters, Indent: depth * 18),
                new TextCell(Session.PlaceName(p), Tone.Dim),
            };
            if (unit.IsMilitary || unit.IsFleet)
            {
                cells.Add(new TextCell($"{unit.Citizens:N0}", unit.StrengthShare < 0.5 ? Tone.Bad : Tone.Normal, Bar: new CellBar(unit.StrengthShare, Tone.Strength, 26, 3, 20)));
                cells.Add(new TextCell("", Bar: new CellBar(unit.OrganisationShare, Tone.Organisation, 13, 8, 20)));
                bool supplied = unit.IsAboard || Session.IsInSupply(unit);
                cells.Add(unit.IsFleet
                    ? new TextCell(Session.IsPort(p, unit.OwnerId) ? "Puerto" : "En el mar", Tone.Dim)
                    : new TextCell(supplied ? "Sí" : "No", supplied ? Tone.Good : Tone.Bad));
            }
            else
            {
                int subs = Session.SubordinatesOf(unit).Count();
                cells.Add(new TextCell($"{subs}/{CommandLevels.Info(unit.HeadquartersLevel).MaxSubordinates} al mando", Tone.Dim, TextSize.Small));
                cells.Add(new TextCell(""));
                cells.Add(new TextCell(""));
            }
            cells.Add(new TextCell(UnitActivity(unit), Session.InBattle(unit) ? Tone.Bad : Tone.Dim, TextSize.Small));
            cells.Add(new ButtonsCell([new Button("Ver", () => ViewUnit(unit.Id), Tooltip: "Seleccionar en el mapa", Size: TextSize.Small)]));
            rows.Add(cells);
        }
        return new TablePage(new Table(columns, rows, Empty: "No tienes ejército. Entrena batallones en la pestaña Ejército de tus ciudades."),
            title, Player.ArmyUnpaid ? Tone.Bad : Tone.Normal);
    }

    private string UnitActivity(Unit unit)
    {
        if (unit.CarrierId is int carrier && Session.UnitById(carrier) is { } fleet) return $"A bordo de {fleet.Name}";
        if (unit.AttackingProvinceId is int target) return $"Atacando {Session.PlaceName(Map.Provinces[target])}";
        if (Session.InBattle(unit)) return "Defendiendo";
        if (unit.IsMoving && unit.Destination is int dest) return $"Hacia {Session.PlaceName(Map.Provinces[dest])}";
        return "En reserva";
    }

    // ------------------------------------------------------------------ templates

    /// <summary>
    /// The unit designer: the nation's templates (new, duplicate, delete); the chosen one's battalions, buttons to add
    /// the battalions it knows, and what a unit of that design costs and how it fights.
    /// </summary>
    private TemplatesPage Templates()
    {
        if (Session.TemplateById(Player, _selectedTemplateId) is not { } template)
        {
            template = Player.Templates[0];
            _selectedTemplateId = template.Id;
        }

        var list = Player.Templates.Select(t => new Button($"{t.Name}  ({Formations.BattalionCount(t.Battalions.Count)})", () => _selectedTemplateId = t.Id,
            Active: t.Id == template.Id, Size: TextSize.Small)).ToList();
        Button[] actions =
        [
            new("Nueva plantilla", () =>
            {
                Show(Session.CreateTemplate(Player.Id));
                _selectedTemplateId = Player.Templates[^1].Id;
            }, Size: TextSize.Small),
            new("Duplicar", () =>
            {
                Show(Session.DuplicateTemplate(Player.Id, template.Id));
                _selectedTemplateId = Player.Templates[^1].Id;
            }, Size: TextSize.Small),
            new("Borrar", () => Show(Session.DeleteTemplate(Player.Id, template.Id)), Player.Templates.Count > 1, Tooltip: "Hace falta al menos una plantilla.", Size: TextSize.Small),
        ];

        var slots = new List<TemplateSlot>();
        for (int i = 0; i < MilitaryRules.MaxBattalionsPerRegiment; i++)
        {
            if (i >= template.Battalions.Count)
            {
                slots.Add(new TemplateSlot(null, "hueco libre", "", null));
                continue;
            }
            var info = GameSession.ModelFor(Player, template.Battalions[i]);
            int index = i;
            slots.Add(new TemplateSlot(template.Battalions[i], Formations.BattalionName(info), $"A {info.Attack:0.#} · D {info.Defense:0.#} · {info.Men} h",
                new Button("Quitar", () => Show(Session.RemoveFromTemplate(Player.Id, template.Id, index)), template.Battalions.Count > 1, Size: TextSize.Small)));
        }

        // Ships are built one by one in ports, never from templates.
        var add = Battalions.All.Where(t => !t.First().Naval && t.BestModel(Player.Techs) >= 0 && !t.Redundant(Player.Techs)).Select(type =>
        {
            var can = Session.CanAddToTemplate(Player, template, type);
            var model = GameSession.ModelFor(Player, type);
            string tip = $"{Formations.BattalionName(model)} ({type.Line().Name.ToLowerInvariant()}, {type.Line().Group.Name().ToLowerInvariant()})"
                         + (GameController.LineNote(type) is { } note ? "\n" + note : "");
            return new Button("+ " + model.Name, () => Show(Session.AddToTemplate(Player.Id, template.Id, type)), can.Ok,
                Tooltip: can.Ok ? tip : can.Message, Size: TextSize.Small, Icon: new BattalionIcon(type));
        }).ToList();

        // What a unit of this design is like.
        var details = new Document();
        details.Add(new Heading(Formations.CombatName(Echelon.Regiment), Tone.Accent, Height: 28));
        var known = Player.Techs;
        details.Add(new Pair("Hombres", $"{template.Men(known):N0}"));
        details.Add(new Pair("Instrucción", $"{GameSession.TrainingDays(Player, template)} días"));
        details.Add(new Pair("Ataque", $"{template.Attack(known):0.#}"));
        details.Add(new Pair("Defensa", $"{template.Defense(known):0.#}"));
        details.Add(new Pair("Organización", $"{template.MaxOrganisation(known):0}"));
        details.Add(new Pair("Velocidad", $"{template.Speed(known) * GameRules.CitizenSpeedKmh:0.#} km/h"));
        details.Add(new Pair("Mantenimiento", TextFormat.UpkeepText(template.Models(known).Select(m => m.Cost))));
        details.Add(new Space(6));
        details.Add(new Label("Coste", Tone.Dim, TextSize.Normal));
        foreach (var (type, amount) in template.Cost(known).Items) details.Add(new Pair(type.Name(), $"{amount:0}", Indent: 12));
        details.Add(new Space(10));
        details.Add(new Paragraph("Se entrena entero en la pestaña Ejército de tus ciudades; sus batallones se instruyen a la vez.", Tone.Dim));
        if (template.AnyMounted(known)) details.Add(new Paragraph("Los montados atacan a la mitad en bosques, pantanos y montañas.", Tone.Dim));

        return new TemplatesPage(list, actions, template.Name,
            $"{Formations.CombatName(Echelon.Regiment)} de {Formations.BattalionCount(template.Battalions.Count)}", slots, add, details);
    }

    // ------------------------------------------------------------------ diplomacy
    /// <summary>
    /// Every other nation: at peace, allied, in a truce or at war; what it thinks of us; its army against ours; what each
    /// holds of the other and the war score; and war, alliance and gifts, or the three kinds of peace.
    /// </summary>
    private TablePage Diplomacy()
    {
        double ours = Session.MilitaryPower(Player.Id);
        Column[] columns = [new("Nación", 150), new("Relación", 120), new("Opinión", 70), new("Poder militar", 120), new("Provincias", 75),
            new("Ocupación", 125), new("Puntuación", 75), new("", 465)];
        var rows = new List<IReadOnlyList<Cell>>();
        foreach (var other in Session.Players.Where(p => p.Id != Player.Id && !p.Eliminated))
        {
            bool war = Session.AtWar(Player.Id, other.Id);
            double theirs = Session.MilitaryPower(other.Id);
            string ratio = ours <= 0 && theirs <= 0 ? "igual" : theirs <= 0 ? "sin ejército" : ours / theirs >= 1.2 ? "más débil que tú" : ours / theirs <= 0.8 ? "más fuerte que tú" : "parecido al tuyo";
            int taken = Session.OccupiedBy(Player.Id, other.Id).Count;
            int lost = Session.OccupiedBy(other.Id, Player.Id).Count;
            rows.Add(
            [
                new TextCell(other.Name, Bold: true, Swatch: other.Color),
                RelationCell(other),
                OpinionCell(other),
                new TextCell($"{theirs:0} ({ratio})", theirs > ours * 1.2 ? Tone.Bad : Tone.Normal, TextSize.Small),
                new TextCell($"{other.Provinces.Count:N0}", Tone.Dim),
                new TextCell(war || taken + lost > 0 ? $"tomadas {taken} · perdidas {lost}" : "-", lost > taken ? Tone.Bad : Tone.Dim, TextSize.Small),
                war ? WarScoreCell(other) : new TextCell("-", Tone.Dim),
                new ButtonsCell(war ? PeaceButtons(other, taken, lost) : PeaceTimeButtons(other)),
            ]);
        }
        return new TablePage(new Table(columns, rows));
    }

    /// <summary>At war (and for how long), allied, in a truce or at peace; its allies in the tooltip.</summary>
    private TextCell RelationCell(Player other)
    {
        double truce = Session.TruceDaysLeft(Player.Id, other.Id);
        var allies = Session.AlliesOf(other.Id).Select(a => a.Name).ToList();
        string tip = (allies.Count > 0 ? $"Aliados de {other.Name}: {string.Join(", ", allies)}. Si la atacas, entrarán en la guerra." : $"{other.Name} no tiene aliados.")
                     + (truce > 0 ? $"\nTras la última paz, ninguno de los dos puede declarar la guerra al otro durante {GameRules.TruceDays} días." : "");
        if (Session.OverlordOf(other.Id) is int overlord && overlord != Player.Id)
            tip += $"\nEs vasallo de {Session.Players[overlord].Name}: si la atacas, su señor entrará en la guerra.";
        var vassals = Session.VassalsOf(other.Id).Select(v => v.Name).ToList();
        if (vassals.Count > 0) tip += $"\nSus vasallos: {string.Join(", ", vassals)}. Entran en todas sus guerras.";
        double reparations = Session.ReparationsDaysLeft(other.Id, Player.Id), owed = Session.ReparationsDaysLeft(Player.Id, other.Id);
        if (reparations > 0) tip += $"\nNos paga reparaciones ({GameRules.ReparationsShare:P0} de sus ingresos de oro) durante {reparations:0} días más.";
        if (owed > 0) tip += $"\nLe pagamos reparaciones ({GameRules.ReparationsShare:P0} de nuestros ingresos de oro) durante {owed:0} días más.";
        tip += $"\nReligión: {GameSession.ReligionName(other.ReligionId)}" + (other.ReligionId == Player.ReligionId ? " (la nuestra)." : ".");
        if (Session.GivesAccess(other.Id, Player.Id)) tip += $"\n{other.Name} deja pasar a nuestros ejércitos por sus tierras.";
        if (Session.GivesAccess(Player.Id, other.Id)) tip += $"\nDejamos pasar a los ejércitos de {other.Name} por nuestras tierras.";
        return Session.AtWar(Player.Id, other.Id) ? new TextCell($"En guerra ({Session.WarDays(Player.Id, other.Id):0} d)", Tone.Bad, Tooltip: tip)
            : Session.IsVassalOf(other.Id, Player.Id) ? new TextCell($"Vasallo ({Session.VassalYears(other.Id):0.#} años)", Tone.Good, Bold: true,
                Tooltip: tip + $"\nNos paga el {GameRules.VassalTributeShare:P0} de sus ingresos de oro y entra en nuestras guerras.")
            : Session.IsVassalOf(Player.Id, other.Id) ? new TextCell("Nuestro señor", Tone.Bad, Bold: true,
                Tooltip: tip + $"\nLe pagamos el {GameRules.VassalTributeShare:P0} de nuestros ingresos de oro y entramos en sus guerras.")
            : Session.AreAllied(Player.Id, other.Id) ? new TextCell("Aliados", Tone.Good, Bold: true, Tooltip: tip)
            : truce > 0 ? new TextCell($"Tregua ({Math.Ceiling(truce):0} d)", Tone.Good, Tooltip: tip)
            : Session.HavePact(Player.Id, other.Id) ? new TextCell("Pacto", Tone.Good, Bold: true,
                Tooltip: tip + "\nTenéis un pacto de no agresión: ninguno de los dos puede declarar la guerra al otro mientras dure.")
            : new TextCell("En paz", Tone.Good, Tooltip: tip);
    }

    /// <summary>What the other nation thinks of us, with its reasons in the tooltip.</summary>
    private TextCell OpinionCell(Player other)
    {
        double opinion = Session.Opinion(other.Id, Player.Id);
        var factors = Session.OpinionFactors(other.Id, Player.Id).Select(f => $"{f.Points:+0;-0;0}  {f.Reason}").ToList();
        string tip = $"Lo que {other.Name} piensa de nosotros (de -100 a 100). Se alía con quien piensa al menos {GameRules.AllianceAcceptOpinion:0}.\n"
                     + (factors.Count > 0 ? string.Join("\n", factors) : "Ni bien ni mal.")
                     + "\nLos recuerdos (guerras, provincias quitadas, regalos) se van olvidando con el tiempo.";
        return new TextCell($"{opinion:+0;-0;0}", opinion >= GameRules.AllianceAcceptOpinion ? Tone.Good : opinion < 0 ? Tone.Bad : Tone.Normal, Bold: true, Tooltip: tip);
    }

    /// <summary>War, alliance (or breaking it) and a gift.</summary>
    private List<Button> PeaceTimeButtons(Player other)
    {
        if (Session.IsVassalOf(other.Id, Player.Id))
        {
            var annex = Session.CanAnnexVassal(Player.Id, other.Id);
            return
            [
                new Button("Anexionar", () => Show(Session.AnnexVassal(Player.Id, other.Id)), annex.Ok,
                    Tooltip: $"Sus provincias, ciudades y gente pasan a ser tuyas. Hace falta que lleve {GameRules.VassalAnnexYears:0} años de vasallo."
                             + (annex.Ok ? "" : $"\n{annex.Message}"), Size: TextSize.Small),
                new Button("Liberar", () => Show(Session.ReleaseVassal(Player.Id, other.Id)),
                    Tooltip: $"Deja de ser vasallo tuyo y lo agradecerá (+{GameRules.ReleasedOpinion:0} de opinión).", Size: TextSize.Small),
            ];
        }
        var war = Session.CanDeclareWar(Player.Id, other.Id);
        var allies = Session.AlliesOf(other.Id).Where(a => a.Id != Player.Id && !Session.AreAllied(a.Id, Player.Id)).Select(a => a.Name).ToList();
        string warTip = war.Ok
            ? "Tus ejércitos podrán entrar en sus tierras, atacar sus tropas y ocupar sus provincias."
              + (allies.Count > 0 ? $"\nSus aliados entrarán en la guerra: {string.Join(", ", allies)}." : "")
            : war.Message;
        bool allied = Session.AreAllied(Player.Id, other.Id);
        var ally = Session.CanProposeAlliance(Player.Id, other.Id);
        double gift = GameSession.GiftCost(Player);
        return
        [
            new Button("Guerra", () => Show(Session.DeclareWar(Player.Id, other.Id)), war.Ok, Tooltip: warTip, Size: TextSize.Small),
            allied
                ? new Button("Romper", () => Show(Session.BreakAlliance(Player.Id, other.Id)),
                    Tooltip: $"Rompes la alianza. {other.Name} no lo olvidará pronto ({GameRules.BrokenAllianceOpinion:0} de opinión).", Size: TextSize.Small)
                : new Button("Aliarse", () => Show(Session.ProposeAlliance(Player.Id, other.Id)), ally.Ok,
                    Tooltip: ally.Ok
                        ? $"Si uno de los dos es atacado, el otro entra en la guerra, y vuestros ejércitos pueden cruzar las tierras del otro. "
                          + $"Acepta si su opinión de nosotros llega a {GameRules.AllianceAcceptOpinion:0}."
                        : ally.Message, Size: TextSize.Small),
            PactButton(other),
            Session.GivesAccess(other.Id, Player.Id)
                ? new Button("Con paso", null, false, Tooltip: $"{other.Name} ya deja pasar a nuestros ejércitos por sus tierras.", Size: TextSize.Small)
                : Agreement("Pedir paso", Session.CanAskAccess(Player.Id, other.Id), () => Session.AskAccess(Player.Id, other.Id),
                    $"Pides que tus ejércitos puedan cruzar sus tierras. Acepta si su opinión de nosotros llega a {GameRules.AccessAcceptOpinion:0}."),
            Session.GivesAccess(Player.Id, other.Id)
                ? new Button("Cerrar paso", () => Show(Session.RevokeAccess(Player.Id, other.Id)),
                    Tooltip: "Retiras el permiso: sus tropas en tus tierras vuelven a las suyas.", Size: TextSize.Small)
                : Agreement("Dar paso", Session.CanGrantAccess(Player.Id, other.Id), () => Session.GrantAccess(Player.Id, other.Id),
                    $"Sus ejércitos podrán cruzar tus tierras. Su opinión de nosotros sube {GameRules.AccessOpinion:0} mientras dure."),
            new Button($"Regalo ({gift:0})", () => Show(Session.SendGift(Player.Id, other.Id)), Player.Stockpile[Game.Economy.ResourceType.Gold] >= gift,
                Tooltip: $"Le envías {gift:0} de oro (un mes de tus ingresos): su opinión de nosotros sube {GameRules.GiftOpinion:0}.", Size: TextSize.Small),
        ];
    }

    /// <summary>A non-aggression pact, or breaking the one we have.</summary>
    private Button PactButton(Player other) => Session.HavePact(Player.Id, other.Id)
        ? new Button("Sin pacto", () => Show(Session.BreakPact(Player.Id, other.Id)),
            Tooltip: $"Rompes el pacto de no agresión: podréis declararos la guerra dentro de {GameRules.BrokenPactTruceDays} días. "
                     + $"{other.Name} lo recordará ({GameRules.BrokenPactOpinion:0} de opinión).", Size: TextSize.Small)
        : Agreement("Pacto", Session.CanProposePact(Player.Id, other.Id), () => Session.ProposePact(Player.Id, other.Id),
            $"Pacto de no agresión: ninguno de los dos puede declarar la guerra al otro hasta que uno lo rompa. Acepta si su opinión de nosotros "
            + $"llega a {GameRules.PactAcceptOpinion:0}, o a {GameRules.FearedPactOpinion:0} si nuestro ejército es más fuerte.");

    /// <summary>A button for an agreement: enabled when it can be offered, with the reason in the tooltip when not.</summary>
    private Button Agreement(string label, CommandResult can, Func<CommandResult> act, string what) =>
        new(label, () => Show(act()), can.Ok, Tooltip: can.Ok ? what : $"{what}\n{can.Message}", Size: TextSize.Small);

    /// <summary>How the war goes for us, from -100 to 100, with what makes it up in the tooltip.</summary>
    private TextCell WarScoreCell(Player other)
    {
        double score = Session.WarScore(Player.Id, other.Id);
        int won = Session.WarVictories(Player.Id, other.Id), lost = Session.WarVictories(other.Id, Player.Id);
        string tip = $"Puntuación de guerra: {score:+0;-0;0}. Suma la parte de su nación que ocupas (cuentan más las ciudades, la gente y "
            + $"sobre todo la capital), resta la de la tuya que ocupan, y añade {GameRules.WarScorePerVictory:0} por cada batalla ganada y "
            + $"quita otro tanto por cada perdida (hasta {GameRules.MaxBattleWarScore:0}). Batallas: {won} ganadas, {lost} perdidas.";
        return new TextCell($"{score:+0;-0;0}", score > 0 ? Tone.Good : score < 0 ? Tone.Bad : Tone.Normal, Bold: true, Tooltip: tip);
    }

    /// <summary>White peace, keeping the land we occupy, or handing over the land they occupy.</summary>
    private List<Button> PeaceButtons(Player other, int taken, int lost)
    {
        var take = Session.CanProposePeace(Player.Id, other.Id, PeaceTerms.TakeOccupied);
        double cost = Session.PeaceCost(Player.Id, other.Id, PeaceTerms.TakeOccupied);
        string takeTip = taken == 0
            ? $"No ocupas ninguna provincia de {other.Name}."
            : $"Te quedas con las {taken} provincias suyas que ocupas, con sus ciudades y edificios. Cuesta {cost:0} de puntuación de guerra "
              + $"y tienes {Session.WarScore(Player.Id, other.Id):0}. La IA acepta si cree que va perdiendo o si la puntuación pasa de 50."
              + (take.Ok ? "" : $"\n{take.Message}");
        string cedeTip = lost == 0
            ? $"{other.Name} no ocupa ninguna provincia tuya."
            : $"Le entregas las {lost} provincias tuyas que ocupa, con sus ciudades y edificios. Lo acepta siempre.";
        return
        [
            new Button("Paz blanca", () => Show(Session.ProposePeace(Player.Id, other.Id)),
                Tooltip: "Las provincias ocupadas vuelven a sus dueños y los ejércitos regresan a casa. La IA solo acepta si la guerra le va mal o se alarga.",
                Size: TextSize.Small),
            new Button($"Exigir ({taken})", () => Show(Session.ProposePeace(Player.Id, other.Id, PeaceTerms.TakeOccupied)), take.Ok,
                Tooltip: takeTip, Size: TextSize.Small),
            TermsButton(other, "Tributo", PeaceTerms.Reparations,
                $"Te paga el {GameRules.ReparationsShare:P0} de sus ingresos de oro durante {GameRules.ReparationsDays / 365} años. Las provincias ocupadas vuelven a sus dueños."),
            TermsButton(other, "Vasallo", PeaceTerms.Vassalize,
                $"Pasa a ser vasallo tuyo: te paga el {GameRules.VassalTributeShare:P0} de sus ingresos de oro, entra en tus guerras y no puede declarar las suyas. "
                + $"A los {GameRules.VassalAnnexYears:0} años puedes anexionarlo. Las provincias ocupadas vuelven a sus dueños."),
            new Button($"Ceder ({lost})", () => Show(Session.ProposePeace(Player.Id, other.Id, PeaceTerms.CedeOccupied)), lost > 0,
                Tooltip: cedeTip, Size: TextSize.Small),
        ];
    }

    /// <summary>A treaty paid for with a fixed war score, explained with what it costs and what we have.</summary>
    private Button TermsButton(Player other, string label, PeaceTerms terms, string what)
    {
        var can = Session.CanProposePeace(Player.Id, other.Id, terms);
        string tip = $"{what}\nCuesta {Session.PeaceCost(Player.Id, other.Id, terms):0} de puntuación de guerra y tienes {Session.WarScore(Player.Id, other.Id):0}. "
                     + "La IA acepta si cree que va perdiendo o si la puntuación pasa de 50." + (can.Ok ? "" : $"\n{can.Message}");
        return new Button(label, () => Show(Session.ProposePeace(Player.Id, other.Id, terms)), can.Ok, Tooltip: tip, Size: TextSize.Small);
    }
}
