using Conquer.Game.Buildings;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Science;
using Conquer.Game.Simulation;

namespace Conquer.Presentation;

/// <summary>The Ciencia tab: the three branches side by side, one age at a time.</summary>
public sealed partial class NationScreen
{
    /// <summary>The age the science tab shows; null for the first one with advances left to discover.</summary>
    private Era? _scienceEra;

    private SciencePage Science()
    {
        double perDay = Session.SciencePerDay(Player);
        string points = $"Ciencia: {perDay:0.##} puntos al día" + (Player.SpareScience >= 1 ? $" · {Player.SpareScience:0} guardados" : "");
        string pointsTip = $"Cada ciudad aporta {GameRules.ScienceBasePerCity:0.#} puntos más {GameRules.SciencePerCityCitizen * 1000:0.#} por cada mil habitantes, " +
                           $"multiplicado por su humor." + (Player.Bonuses.Science > 0 ? $"\nAvances: +{Player.Bonuses.Science:P0}." : "") +
                           "\nSe reparte entre las ramas según su prioridad. Si una rama no investiga nada, su parte va a las demás." +
                           $"\nCada vecino que ya conoce un avance te lo abarata un {GameRules.NeighbourResearchDiscount:P0} (hasta {GameRules.MaxNeighbourDiscounts})." +
                           (Player.SpareScience >= 1 ? "\nLos puntos guardados entran en el próximo avance que elijas." : "");

        // One age at a time: a button for each, and the institution that opens the one shown.
        var eras = Techs.All.Select(t => t.Info().Era).Distinct().Order().ToList();
        var shown = _scienceEra ?? eras.FirstOrDefault(e => Techs.All.Any(t => t.Info().Era == e && !Player.Techs.Contains(t)), eras[^1]);
        var eraButtons = eras.Select(era =>
        {
            double multiplier = GameSession.EraCostMultiplier(Player, era);
            return new Button(era.Name() + (multiplier > 1 ? " (+)" : ""), () => _scienceEra = era, Active: era == shown,
                Tooltip: multiplier > 1 ? $"Sus avances te cuestan un {multiplier - 1:P0} más hasta que adoptes su institución." : null, Size: TextSize.Small);
        }).ToList();

        var neighbours = Session.NeighbourNations(Player);
        return new SciencePage(points, Player.SpareScience >= 1 ? Tone.Accent : Tone.Normal, pointsTip,
            Institutions.All.Where(i => i.Info().Opens == shown).Select(InstitutionBadge).ToList(), eraButtons,
            Techs.Branches.Select(b => Branch(b, shown, perDay, neighbours)).ToList());
    }

    /// <summary>An institution: adopted, how far it has spread and the button to adopt it, or not yet born.</summary>
    private InstitutionBadge InstitutionBadge(Institution institution)
    {
        var info = institution.Info();
        bool adopted = Player.Institutions.Contains(institution), born = Session.IsBorn(institution);
        Button? adopt = null;
        if (born && !adopted)
        {
            double cost = Session.AdoptionCost(Player, institution);
            var can = Session.CanAdopt(Player, institution);
            adopt = new Button($"Adoptar ({cost:N0} oro)", () => Show(Session.Adopt(Player.Id, institution)), can.Ok,
                Tooltip: can.Ok ? info.Description : can.Message, Size: TextSize.Small);
        }
        string text = adopted ? $"{info.Name}: adoptado" : born ? $"{info.Name}: {Session.InstitutionShare(Player, institution):P0} de tu población"
            : $"{info.Name}: aún no ha nacido";
        return new InstitutionBadge(text, adopted ? Tone.Good : born ? Tone.Accent : Tone.Dim,
            $"{info.Birth}\n{info.Description}\nMientras no lo adoptes, los avances de la era {info.Opens.Name()} cuestan un " +
            $"{GameRules.InstitutionPenalty:P0} más. Se adopta al llegar a la {GameRules.InstitutionAdoptionShare:P0} de tu población, o antes pagando oro.", adopt);
    }

    private BranchColumn Branch(TechBranch branch, Era era, double perDay, IReadOnlySet<int> neighbours)
    {
        int priority = Player.ResearchPriorities[(int)branch];
        var less = new Button("-", () => Show(Session.SetResearchPriority(Player.Id, branch, priority - 1)), priority > 0, Tooltip: "Menos prioridad");
        var more = new Button("+", () => Show(Session.SetResearchPriority(Player.Id, branch, priority + 1)), priority < GameRules.MaxResearchPriority, Tooltip: "Más prioridad");

        string status;
        Ink statusInk;
        double? progress = null;
        if (Player.Researching[(int)branch] is Tech current)
        {
            double cost = Session.ResearchCost(Player, current, neighbours), done = Player.ResearchProgress[(int)current];
            double rate = perDay * Player.ScienceShare(branch);
            string eta = rate > 0 ? $"unos {GameSession.FormatHours(Math.Ceiling((cost - done) / rate) * 24)}" : "sin ciencia";
            status = $"Investigando {current.Info().Name}: {done:0} / {cost:0} · {eta}";
            statusInk = Tone.Normal;
            progress = done / cost;
        }
        else
        {
            bool complete = Techs.InBranch(branch).All(Player.Techs.Contains);
            bool any = Techs.InBranch(branch).Any(t => GameSession.CanResearch(Player, t).Ok);
            status = complete ? "Rama completa: su ciencia va a las demás." : any ? "Elige qué investigar: mientras, su ciencia va a las demás."
                : "Nada disponible: faltan avances de otras ramas.";
            statusInk = complete ? Tone.Good : any ? Tone.Accent : Tone.Dim;
        }

        var levels = new List<TechLevel>();
        foreach (int level in Techs.InBranch(branch).Where(t => t.Info().Era == era).Select(t => t.Info().Level).Distinct())
        {
            bool open = GameSession.IsLevelOpen(Player, branch, level);
            string title = $"Nivel {level}";
            if (!open) title += $" · se abre con {Techs.NeededToOpenNext(branch, level - 1)} avance del nivel {level - 1}";
            else if (level < Techs.Levels(branch) && Techs.InLevel(branch, level).Count() > 1)
                title += $" · {Techs.NeededToOpenNext(branch, level)} de {Techs.InLevel(branch, level).Count()} abren el siguiente";
            levels.Add(new TechLevel(title, open ? Tone.Dim : Tone.Disabled, Techs.InLevel(branch, level).Select(t => TechCard(t, neighbours)).ToList()));
        }
        return new BranchColumn(branch.Name(), priority.ToString(), less, more, $"{Player.ScienceShare(branch):P0}", status, statusInk, progress, levels);
    }

    /// <summary>One advance: its state, cost, effect, the advances it needs, what it unlocks and the button to research it.</summary>
    private TechCard TechCard(Tech tech, IReadOnlySet<int> neighbours)
    {
        var info = tech.Info();
        bool known = Player.Techs.Contains(tech), current = Player.Researching[(int)info.Branch] == tech;
        var can = GameSession.CanResearch(Player, tech);
        double cost = Session.ResearchCost(Player, tech, neighbours), done = Player.ResearchProgress[(int)tech];
        string state = known ? "Descubierto" : (done > 0 ? $"{done:0} / {cost:0}" : $"{cost:0} puntos") +
                       (cost < info.Cost ? $" (-{1 - cost / info.Cost:P0})" : cost > info.Cost ? $" (+{cost / info.Cost - 1:P0})" : "");
        // Choosing it replaces what the branch was researching; the points already in either one stay.
        var research = known || current ? null
            : new Button("Investigar", () => Show(Session.Research(Player.Id, tech)), can.Ok, Tooltip: can.Ok ? null : can.Message, Size: TextSize.Small);
        // Buildings and battalions stay hidden until their advance is known, so the card says what it brings.
        var unlocks = Buildings.All.Where(b => b.Info().RequiresTech == tech).Select(b => b.Info().Name)
            .Concat(Battalions.All.Where(b => b.Info().Requires.Contains(tech)).Select(b => b.Info().Name))
            .Concat(RoadKinds.All.Where(r => r.Info().Requires == tech).Select(r => r.Info().Plural)).ToList();
        var notes = new List<(string Text, Ink Ink)>();
        if (info.Requires.Length > 0)
            notes.Add(("Requiere: " + string.Join(", ", info.Requires.Select(t => t.Info().Name)),
                info.Requires.All(Player.Techs.Contains) ? Tone.Dim : Tone.Bad));
        if (unlocks.Count > 0) notes.Add(("Permite: " + string.Join(", ", unlocks), known ? Tone.Dim : Tone.Accent));
        return new TechCard(info.Name, known ? Tone.Good : current ? Tone.Accent : can.Ok ? Tone.Normal : Tone.Disabled, state, research,
            info.Description, known || can.Ok ? Tone.Normal : Tone.Dim, notes, !known && done > 0 ? done / cost : null, current ? Tone.Accent : Tone.Dim,
            known, known ? Tone.Good : current ? Tone.Accent : can.Ok ? Tone.Border : Tone.Groove);
    }
}
