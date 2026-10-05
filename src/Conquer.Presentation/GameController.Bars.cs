using Conquer.Game.Economy;
using Conquer.Game.Rules;
using Conquer.Game.Science;
using Conquer.Game.Simulation;

namespace Conquer.Presentation;

/// <summary>A resource in the top bar: the amount in store and, under it, the last day's change (null if about nothing).</summary>
public sealed record ResourceStock(ResourceType Resource, string Amount, Ink AmountInk, string? Change, Ink ChangeInk, string Tooltip);

/// <summary>
/// The strip across the top: the nation and its people, the date with the speed buttons, the known resources and the
/// label of the button to the nation screen (with "!" when some branch of science is idle).
/// </summary>
public sealed record TopBar(string Nation, uint Color, string People, Ink PeopleInk, string PeopleTooltip, string Date,
    IReadOnlyList<Button> Speeds, IReadOnlyList<ResourceStock> Resources, string NationButton, string NationTooltip);

/// <summary>The keys and clicks reminded at the bottom; the last one (F1) stays when there is no room for all.</summary>
public sealed record Hints(IReadOnlyList<string> Items, Ink Ink);

/// <summary>The dialog to name a city: its title, where or at what cost, why the name is not valid (null if it is) and the confirm label.</summary>
public sealed record CityNamingDialog(string Title, string Detail, string? Error, string Confirm);

/// <summary>The bars around the map, the map's tooltip and the city naming dialog.</summary>
public sealed partial class GameController
{
    public static readonly string[] ModeNames = ["Terreno", "Político", "Población", "Moral", "Fertilidad", "Recursos", "Instituciones", "Cultura", "Religión"];

    public TopBar TopBar()
    {
        var stats = Session.Stats(Human);
        double population = stats.Settled, mood = stats.AverageMood;
        string[] labels = ["||", "1", "2", "3", "4", "5"];
        var speeds = labels.Select((label, i) => new Button(label, () => Clock.SetSpeed(i), Active: Clock.Speed == i,
            Tooltip: i == 0 ? "Pausa (Espacio)" : $"Velocidad {i}: {GameSession.FormatHours(GameClock.HoursPerSecond[i])} por segundo (tecla {i})",
            Size: TextSize.Small)).ToList();
        var resources = Resources.All.Where(Human.Knows).Select(r =>
        {
            double amount = Human.Stockpile[r];
            double net = Human.LastDayNet[(int)r];
            return new ResourceStock(r, TextFormat.Compact(amount), r == ResourceType.Food && Human.IsStarving ? Tone.Bad : Tone.Normal,
                Math.Abs(net) >= 0.05 ? (net > 0 ? "+" : "") + TextFormat.Compact(net, decimals: true) : null, net > 0 ? Tone.Good : Tone.Bad,
                $"{r.Name()}: {amount:N1}\nCambio en el último día: {net:+0.##;-0.##;0}");
        }).ToList();
        // A branch with nothing chosen hands its science to the others, or leaves it waiting when none is researching.
        bool idleScience = Human.CapitalCityId.HasValue && Techs.Branches.Any(b =>
            Human.Researching[(int)b] is null && Techs.InBranch(b).Any(t => GameSession.CanResearch(Human, t).Ok));
        return new TopBar(Human.Name, Human.Color, $"{population:N0} hab. · moral {mood:0}", Ink.Mood(mood, Tone.Dim),
            $"{population:N0} habitantes\nMoral media: {mood:0} ({GameRules.MoodName(mood)})\n"
            + $"Reclutas: {Human.Manpower:N0} de {Session.ManpowerCapacity(Human):N0} (+{Session.ManpowerPerDay(Human):0.#} al día)", Session.Date + SeasonAtHome(), speeds, resources,
            idleScience ? "Nación !" : "Nación",
            idleScience ? "Gestionar el país (N)\nHay ramas de la ciencia sin investigación." : "Gestionar el país (N)");
    }

    /// <summary>The season at the capital (or where the first settlers wait), after the date; the hemisphere decides it.</summary>
    private string SeasonAtHome()
    {
        int? home = Human.CapitalCityId is int id && Session.CityById(id) is { } capital ? capital.ProvinceId
            : Session.Units.FirstOrDefault(u => u.OwnerId == Human.Id)?.ProvinceId;
        return home is int h ? " · " + GameSession.SeasonNames[(int)Session.SeasonOf(Map.Provinces[h])].ToLowerInvariant() : "";
    }

    /// <summary>One button per map mode.</summary>
    public IReadOnlyList<Button> ModeButtons() =>
        ModeNames.Select((name, i) => new Button(name, () => Mode = (MapMode)i, Active: (int)Mode == i, Tooltip: "Modo de mapa (Tab)")).ToList();

    /// <summary>In the resources mode, buttons to show every deposit or only one known resource; they double as the legend.</summary>
    public IReadOnlyList<Button> ResourceFilterButtons()
    {
        if (Mode != MapMode.Resources) return [];
        var buttons = new List<Button>
        {
            new("Todos", () => ResourceFilter = null, Active: ResourceFilter is null, Tooltip: "Todos los yacimientos de cada provincia, con sus iconos (de lejos, el color del principal)", Size: TextSize.Small),
        };
        foreach (var r in Resources.Deposits.Where(Human.Knows))
            buttons.Add(new Button(r.Name(), () => ResourceFilter = ResourceFilter == r ? null : r, Active: ResourceFilter == r,
                Tooltip: $"Solo {r.Name().ToLowerInvariant()}: su icono donde lo hay (de lejos, un color más intenso cuanto más queda en la bolsa)", Size: TextSize.Small, Icon: new ResourceIcon(r)));
        return buttons;
    }

    public Hints Hints() => ChoosingMigrationTarget
        ? new(["Clic izquierdo: elegir provincia de destino", "Esc: cancelar"], Tone.Accent)
        : new(["Clic: seleccionar", "Arrastrar: mover mapa", "Clic dcho: mover unidad", "Rueda: zoom", "Espacio: pausa", "1-5: velocidad", "Inicio: tu capital", "F1: ayuda"], Tone.Dim);

    /// <summary>What the map's tooltip says about the province under the mouse; null when there is none.</summary>
    public string? MapTooltip()
    {
        if (HoverProvince < 0) return null;
        if (!ExploredProvinces.Contains(HoverProvince)) return "Tierra inexplorada";
        var p = Map.Provinces[HoverProvince];
        string owner = !p.IsClaimable ? "No reclamable" : p.IsOwned ? Session.Players[p.OwnerId].Name : "Sin dueño";
        var city = Session.CityIn(p);
        string text = (city != null ? $"{city.Name}  ·  {p.DisplayName}" : p.DisplayName)
            + (p.Name.Length > 0 ? $"  ·  {p.Info.Name.ToLowerInvariant()}" : "") + $"  ·  {owner}";
        if (p.HasRiver) text += "  ·  gran río";
        if (p.IsClaimable && Session.SeasonEffect(p) is { } season) text += "\n" + season;
        if (Session.SiegeAt(p.Id) is { } siege)
            text += $"\nSitiada por {Session.Players[siege.AttackerId].Name}: {Math.Min(1, siege.Progress / GameSession.SiegeDays(p)):P0}";
        if (p.IsOwned) text += $"\n{p.Population:N0} habitantes" + (IncomingMigrants(p.Id) is var incoming and > 0 ? $" (+{incoming:N0} en camino)" : "");
        if (p.IsOwned && p.Population >= 1) text += $"\nMoral {p.Mood:0} ({GameRules.MoodName(p.Mood)})  ·  Fertilidad {p.Fertility:P0}";
        if (Mode == MapMode.Resources)
            foreach (var r in Resources.Deposits.Where(r => p.Deposits[(int)r] > 0 && Human.Knows(r)))
                text += p.HasDeposit(r) ? $"\n{r.Name()}: {p.Deposits[(int)r]:0.0}/día, quedan {TextFormat.Compact(p.Reserves[(int)r])}" : $"\n{r.Name()}: agotado";
        if (Mode == MapMode.Institutions)
            text += p.Institutions.Count > 0 ? "\n" + string.Join(", ", Institutions.All.Where(p.Institutions.Contains).Select(i => i.Info().Name))
                : Institutions.All.Any(Session.IsBorn) ? "\nSin instituciones" : "\nTodavía no ha nacido ninguna institución";
        if (Mode == MapMode.Culture && p.IsOwned && Session.CultureOf(p) is { } culture && p.Population >= 1)
        {
            text += GameSession.HasForeignCulture(p) ? $"\nCultura de {culture.Name} ({p.Assimilation:P0} asimilada)" : $"\nCultura de {culture.Name}";
            if (p.RevoltProgress > 0) text += $"\nRebelión {GameSession.RevoltRisk(p):P0}";
        }
        if (Mode == MapMode.Religion && p.IsOwned && p.ReligionId >= 0 && p.Population >= 1)
            text += Session.HasOtherFaith(p) ? $"\n{GameSession.ReligionName(p.ReligionId)} ({p.Conversion:P0} convertida)" : $"\n{GameSession.ReligionName(p.ReligionId)}";
        if (GameSession.IsSick(p) && Session.VisibleProvinces(Human.Id).Contains(p.Id)) text += $"\nEpidemia: {p.PlagueDaysLeft} días";
        if (ChoosingMigrationTarget) text += "\nClic para enviar aquí a los migrantes";
        return text;
    }

    /// <summary>The city naming dialog while it is open.</summary>
    public CityNamingDialog? CityNamingDialog()
    {
        if (Naming is not var (unitId, provinceId)) return null;
        var check = Session.CheckCityName(CityName);
        return new CityNamingDialog(
            unitId.HasValue ? "Fundar ciudad" : "Construir ciudad",
            unitId.HasValue
                ? $"Los colonos fundarán la ciudad en {Map.Provinces[provinceId].DisplayName}."
                : $"Coste: {GameRules.CityCost}. Estará lista en {GameRules.CityBuildingDays} días.",
            check.Ok ? null : check.Message,
            unitId.HasValue ? "Fundar" : "Construir");
    }
}
