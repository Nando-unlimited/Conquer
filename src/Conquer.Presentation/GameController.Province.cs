using Conquer.Game.Buildings;
using Conquer.Game.Economy;
using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Science;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Presentation;

/// <summary>The province panel: its General, Edificios and Ejército tabs.</summary>
public sealed partial class GameController
{
    /// <summary>The side panel for the selected unit or province; null when nothing is selected.</summary>
    public Document? SidePanel()
    {
        var unit = SelectedUnit;
        if (unit == null && SelectedProvince < 0) return null;
        var doc = new Document { OnClose = ClearSelection, Key = unit != null ? $"unidad {unit.Id}" : $"provincia {SelectedProvince} {ProvinceTab}" };
        if (unit != null) UnitPanel(doc, unit);
        else ProvincePanel(doc, Map.Provinces[SelectedProvince]);
        return doc;
    }

    /// <summary>People on their way to settle in a province, forced or not; they count in its population once they arrive.</summary>
    public int IncomingMigrants(int provinceId) => Session.Migrations.Where(m => m.ToProvinceId == provinceId).Sum(m => m.People);

    public bool IsBuildingKnown(BuildingType type) => type.Info().RequiresTech is not Tech tech || Human.Techs.Contains(tech);

    /// <summary>Current mood, where it is heading and why, and what it does to the province.</summary>
    public string MoodTooltip(Province p)
    {
        var factors = Session.MoodFactors(p).Select(f => $"{f.Points:+0;-0;0}  {f.Reason}");
        string text = $"Moral {p.Mood:0}; tiende a {Session.TargetMood(p):0}.\n" + string.Join("\n", factors) +
                      $"\nProducción ×{GameRules.MoodProductivity(p.Mood):0.00}";
        if (p.Mood < GameRules.UnrestMood) text += "\nDescontento: no paga impuestos.";
        return text;
    }

    private static Heading Section(string text, float height = 26) => new(text, Tone.Normal, Height: height);

    /// <summary>The people's faith, and how far they have come toward their ruler's if it is another.</summary>
    private void AddFaith(Document doc, Province p)
    {
        if (p.ReligionId < 0 || p.Population < 1) return;
        var ink = Ink.Nation(Religions.Colors[p.ReligionId]);
        string name = GameSession.ReligionName(p.ReligionId);
        if (!Session.HasOtherFaith(p))
        {
            doc.Add(new Info("Religión", name, ink, "Su gente sigue la fe de su nación."));
            return;
        }
        string ruler = GameSession.ReligionName(Session.Players[p.OwnerId].ReligionId);
        doc.Add(new Info("Religión", $"{name} · {p.Conversion:P0} convertida", ink,
            $"Su gente no sigue la fe de su nación ({ruler}): pierde {GameRules.OtherFaithMood:0} de moral hasta convertirse.\n" +
            $"Al ritmo actual lo hará en unos {Session.YearsToConvert(p):0} años; va más deprisa con moral alta, con un templo (el doble) y con la Teología.\n" +
            "Los migrantes de tus otras provincias que se instalan aquí también ayudan."));
    }

    /// <summary>An epidemic raging in the province, or the immunity left by the last one.</summary>
    private void AddPlague(Document doc, Province p)
    {
        if (GameSession.IsSick(p))
        {
            double deaths = p.Population * Session.PlagueDeaths(p);
            doc.Add(new Info("Epidemia", $"{p.PlagueDaysLeft} días · {TextFormat.Compact(deaths)} muertos/día", Tone.Bad,
                $"Una epidemia mata cada día al {Session.PlagueDeaths(p):P2} de su gente y le quita {GameRules.PlagueMood:0} de moral.\n" +
                "Se contagia a las provincias vecinas, mucho más por los caminos, y de puerto a puerto.\n" +
                $"Resistencia aquí: {Session.PlagueResistance(p):P0} (Medicina, Saneamiento, Antibióticos, herbolarios y hospitales).\n" +
                $"Al terminar, la provincia queda inmune unos {GameRules.PlagueImmunityYears:0} años."));
        }
        else if (p.PlagueImmuneUntil > Session.Date.Hours && p.Population >= 1)
            doc.Add(new Info("Epidemia", $"Inmune {(p.PlagueImmuneUntil - Session.Date.Hours) / 24 / 365.0:0.#} años", Tone.Good,
                "Ya pasó una epidemia: su gente no volverá a enfermar en un tiempo."));
    }

    /// <summary>Whose culture the people share and how far they have assimilated; how close the province is to revolt, if at all.</summary>
    private void AddCultureAndRevolt(Document doc, Province p)
    {
        if (Session.CultureOf(p) is not { } culture) return;
        if (GameSession.HasForeignCulture(p))
        {
            double years = (1 - p.Assimilation) / GameSession.DailyAssimilation(p) / 365;
            doc.Add(new Info("Cultura", $"{culture.Name} · {p.Assimilation:P0} asimilada", Ink.Nation(culture.Color),
                $"Su gente es de cultura de {culture.Name}: pierde {-GameSession.ForeignCultureMood(p):0} de moral, menos cuanto más se asimila.\n" +
                $"Al ritmo actual adoptará la de {Session.Players[p.OwnerId].Name} en unos {years:0} años; va más deprisa con moral alta.\n" +
                "Los migrantes de tus otras provincias que se instalan aquí la aceleran."));
        }
        else
            doc.Add(new Info("Cultura", culture.Name, Ink.Nation(culture.Color), "Su gente comparte la cultura de su nación."));
        AddFaith(doc, p);
        AddPlague(doc, p);

        if (p.RevoltProgress <= 0 && p.Mood >= GameRules.UnrestMood) return;
        bool garrison = Session.IsGarrisoned(p);
        string outcome = Session.WouldSecede(p)
            ? $"se sublevará y se unirá a {culture.Name}"
            : $"estallará una revuelta: morirá el {GameRules.RevoltDeaths:P0} de su gente y arderá uno de sus edificios";
        string tip = $"Con la moral por debajo de {GameRules.UnrestMood:0} y sin tropas de su dueño dentro, la provincia se acerca a la rebelión, " +
                     $"más deprisa cuanto peor es la moral. Al llegar al 100 % {outcome}.\n" +
                     "Una guarnición (un regimiento en la provincia) la detiene; con moral alta se calma poco a poco." +
                     (garrison ? "\nAhora hay guarnición: no avanza." : "");
        doc.Add(new Info("Rebelión", $"{GameSession.RevoltRisk(p):P0}" + (garrison ? " · guarnición" : ""), garrison ? Tone.Normal : Tone.Bad, tip));
    }

    private void ProvincePanel(Document doc, Province p)
    {
        var city = Session.CityIn(p);
        doc.Add(new Heading(city?.Name ?? p.DisplayName, Tone.Accent, TextSize.Large, Height: 36));
        if (p.IsOwned)
        {
            string buildings = p.Constructing.HasValue || p.PlannedCityName != null ? $"Edificios ({p.Buildings.Count}+1)" : $"Edificios ({p.Buildings.Count})";
            // Cities, barracks and workshops train troops; elsewhere the tab still shows what was left training.
            bool army = p.OwnerId == Human.Id && (city != null || p.Has(BuildingType.Barracks) || p.Has(BuildingType.Workshop) || p.Training.Count > 0);
            if (!army && ProvinceTab == ProvinceTab.Army) ProvinceTab = ProvinceTab.General;
            string[] tabs = army ? ["General", buildings, p.Training.Count > 0 ? $"Ejército ({p.Training.Count})" : "Ejército"] : ["General", buildings];
            doc.Add(new ButtonRow(
                tabs.Select((label, i) => new Button(label, () => ProvinceTab = (ProvinceTab)i, Active: (int)ProvinceTab == i, Size: TextSize.Small)).ToList(),
                Height: 28, Gap: 10));
            if (ProvinceTab == ProvinceTab.Buildings)
            {
                BuildingsTab(doc, p);
                return;
            }
            if (ProvinceTab == ProvinceTab.Army)
            {
                ArmyTab(doc, p);
                return;
            }
        }
        GeneralTab(doc, p, city);
    }

    private void GeneralTab(Document doc, Province p, City? city)
    {
        if (city != null) doc.Add(new Info("Provincia", p.DisplayName));
        if (p.PlannedCityName != null) doc.Add(new Info("Ciudad en obras", p.PlannedCityName, Tone.Accent));
        if (p.Name.Length > 0 || city != null) doc.Add(new Info("Terreno", p.Info.Name));
        else if (p.IsClaimable) doc.Add(new Info("Nombre", "Sin reclamar", Tone.Dim));
        doc.Add(new Info("Superficie", $"{p.AreaKm2:N0} km²"));
        doc.Add(new Info("Altitud media", $"{p.MeanElevation:N0} m"));
        if (p.RiverFlow > 0)
            doc.Add(new Info("Río", p.HasRiver ? "Gran río" : "Arroyo", p.HasRiver ? Tone.River : Tone.Dim, p.HasRiver
                ? $"Tierra fértil: +{GameRules.RiverFertility - 1:P0} de comida y de capacidad.\nQuien ataque debe cruzarlo: el defensor dispara un {MilitaryRules.RiverDefense - 1:P0} más."
                : "Un arroyo: no cambia nada. Solo los grandes ríos fertilizan la tierra y protegen de los ataques."));
        if (Session.SiegeAt(p.Id) is { } siege)
        {
            double days = GameSession.SiegeDays(p), work = Session.DailySiegeWork(siege);
            doc.Add(new Info("Asedio", $"{Session.Players[siege.AttackerId].Name} · {Math.Min(1, siege.Progress / days):P0}", Tone.Bad,
                $"{Session.Players[siege.AttackerId].Name} sitia la provincia. Lleva {siege.Progress:0} de {days:0} días de trabajo y avanza {work:0.#} al día " +
                $"(la artillería lo acelera): caerá en unos {Math.Ceiling(Math.Max(0, days - siege.Progress) / work):0} días si los sitiadores siguen allí."));
        }
        else if (GameSession.IsFortified(p) && p.IsOwned)
            doc.Add(new Info("Fortificada", $"{GameSession.SiegeDays(p):0} días de asedio", Tone.Normal,
                "Sus murallas o su castillo resisten: el enemigo que entra no la ocupa, tiene que sitiarla. La artillería acorta el asedio."));
        if (!p.IsWater)
        {
            string? effect = Session.SeasonEffect(p);
            doc.Add(new Info("Estación", GameSession.SeasonNames[(int)Session.SeasonOf(p)], effect != null ? Tone.Bad : Tone.Normal,
                effect ?? "Ni la estación ni la tierra frenan ni desgastan aquí a las tropas."));
        }

        if (!p.IsClaimable)
        {
            doc.Add(new Space(6));
            doc.Add(new Paragraph(p.IsWater
                ? "Aguas abiertas: no se pueden reclamar y solo las unidades navales pueden navegarlas."
                : "Hielo polar: inhabitable. No se puede reclamar, pero sí atravesar.", Tone.Dim));
            return;
        }

        if (p.IsOwned)
        {
            var owner = Session.Players[p.OwnerId];
            doc.Add(new Info("Dueño", owner.Name, Ink.Nation(owner.Color)));
            SkyLine(doc, p);
            int incoming = IncomingMigrants(p.Id);
            int outgoing = Session.Migrations.Where(m => m.FromProvinceId == p.Id).Sum(m => m.People);
            doc.Add(new Info("Población", $"{p.Population:N0} / {Session.CapacityOf(p):N0}" + (incoming > 0 ? $" (+{incoming:N0} en camino)" : ""), Tone.Normal,
                incoming > 0 ? $"Llegarán {incoming:N0} migrantes más; cuentan en la población cuando se instalan." : null));
            if (p.Population >= 1)
            {
                doc.Add(new Info("Moral", $"{p.Mood:0} · {GameRules.MoodName(p.Mood)}", Ink.Mood(p.Mood, Tone.Normal), MoodTooltip(p)));
                doc.Add(new Info("Fertilidad", $"{p.Fertility:P0}", p.Fertility < 0.75 ? Tone.Bad : p.Fertility >= 1.15 ? Tone.Good : Tone.Normal,
                    "Nacimientos respecto a lo normal. Sube con la moral alta, cae con el hambre y cambia despacio.\n" +
                    $"Tiende a {Session.TargetFertility(p, owner, owner.IsStarving):P0}."));
                doc.Add(new Info("Nacimientos", $"+{Session.DailyBirths(p, owner.IsStarving):0.##} al día", owner.IsStarving ? Tone.Bad : Tone.Normal));
                AddCultureAndRevolt(doc, p);
            }
            string migrants = "Gente en camino hacia esta provincia y desde ella.\n" +
                              $"Las ciudades de más de {GameRules.MinEmigrationCityPopulation} habitantes envían cada día un {GameRules.DailyEmigrationShare:P2} de su gente " +
                              $"a tus provincias sin ciudad que no llegan al {GameRules.MigrationTargetShare:P0} de su capacidad.";
            doc.Add(new Info("Inmigrantes", $"{incoming:N0}", incoming > 0 ? Tone.Normal : Tone.Dim, migrants));
            doc.Add(new Info("Emigrantes", $"{outgoing:N0}", outgoing > 0 ? Tone.Normal : Tone.Dim, migrants));
        }
        else
        {
            doc.Add(new Info("Dueño", "Nadie", Tone.Dim));
            doc.Add(new Info("Capacidad", $"{p.Capacity:N0} habitantes"));
        }

        doc.Add(new Space(6));
        doc.Add(Section("Recursos", 24));
        double fed = p.FoodYield * GameRules.FoodPerWorker;
        doc.Add(new Info(ResourceType.Food.Name(), $"{fed * 1000:0} por mil hab./día", Icon: new ResourceIcon(ResourceType.Food)));
        if (p.Info.WoodYield > 0) doc.Add(new Info(ResourceType.Wood.Name(), $"{p.Info.WoodYield:0.#} por mil hab./día", Icon: new ResourceIcon(ResourceType.Wood)));
        foreach (var r in Resources.Deposits.Where(r => p.Deposits[(int)r] > 0 && Human.Knows(r)))
        {
            if (r.IsRenewable())
            {
                doc.Add(new Info(r.Name(), $"{p.Deposits[(int)r]:0.0}/día · pastos", Tone.Normal,
                    $"Pastos de {r.Name().ToLowerInvariant()}: no se agotan. Dan lo máximo con {GameRules.DepositFullWorkers:N0} habitantes.", new ResourceIcon(r)));
                continue;
            }
            double left = p.Reserves[(int)r];
            string tip = left <= 0 ? "Esta bolsa se ha agotado y ya no produce."
                : $"Bolsa de {r.Name().ToLowerInvariant()}: quedan {left:N0} de {p.DepositSizes[(int)r] * GameRules.DepositSizeMultiplier:N0}.\n" +
                  $"Explotada al máximo ({GameRules.DepositFullWorkers:N0} habitantes) dura unos {left / p.Deposits[(int)r] / 365:0} años.";
            doc.Add(left <= 0
                ? new Info(r.Name(), "Agotado", Tone.Dim, tip, new ResourceIcon(r))
                : new Info(r.Name(), $"{p.Deposits[(int)r]:0.0}/día · quedan {TextFormat.Compact(left)}", Tone.Normal, tip, new ResourceIcon(r)));
        }

        if (p.OwnerId != Human.Id) return;

        if (city != null)
        {
            doc.Add(new Space(10));
            doc.Add(Section("Fiestas"));
            var festival = Session.CanHoldFestival(city);
            string festivalLabel = city.HasFestival(Session.Date.Hours)
                ? $"De fiesta: quedan {GameSession.FormatHours(city.FestivalUntilHours - Session.Date.Hours)}"
                : $"Celebrar fiestas ({GameRules.FestivalCost(p.Population):N0} oro)";
            string festivalTip = $"+{GameRules.FestivalMood:0} a la moral de la ciudad durante {GameRules.FestivalDays} días." +
                                 (festival.Ok ? "" : "\n" + festival.Message);
            doc.Add(new Button(festivalLabel, () => Show(Session.HoldFestival(Human.Id, city.Id)), festival.Ok, Tooltip: festivalTip, Height: 34));

            doc.Add(new Space(10));
            doc.Add(Section("Colonos"));
            var settlers = Session.CanRecruitSettlers(city);
            string settlersTip = $"{GameRules.SettlerCitizens} ciudadanos salen de la ciudad para fundar otra. Coste: {GameRules.SettlersCost}." +
                                 (settlers.Ok ? "" : "\n" + settlers.Message);
            doc.Add(new Button($"Enviar colonos ({GameRules.SettlerCitizens} hab.)", () => Show(Session.RecruitSettlers(Human.Id, city.Id)), settlers.Ok,
                Tooltip: settlersTip, Height: 34));
        }

        if (p.Population >= 1) ForcedMigration(doc, p);
    }

    /// <summary>How many to send (in steps of 10 and 100, or as many as can go), what it costs, and the button to pick where.</summary>
    private void ForcedMigration(Document doc, Province p)
    {
        doc.Add(new Space(10));
        doc.Add(Section("Migración forzada"));
        int keep = p.CityId.HasValue ? GameRules.MinCityPopulation : 0;
        int max = Math.Max(1, (int)p.Population - keep);
        MigrationAmount = Math.Clamp(MigrationAmount, 1, max);
        Button Step(int by) => new(by < 0 ? $"{by}" : $"+{by}", () => MigrationAmount = Math.Clamp(MigrationAmount + by, 1, max), Size: TextSize.Small);
        doc.Add(new Stepper(MigrationAmount.ToString("N0"), [Step(-100), Step(-10)], [Step(10), Step(100)]));
        double cost = GameRules.ForcedMigrationCost(MigrationAmount);
        bool affordable = Human.Stockpile[ResourceType.Gold] >= cost;
        doc.Add(new LabelAndButton($"Coste: {cost:N0} de oro", affordable ? Tone.Dim : Tone.Bad, new Button("Máx.", () => MigrationAmount = max, Size: TextSize.Small)));
        doc.Add(new Button(ChoosingMigrationTarget ? "Elige el destino en el mapa..." : "Enviar a otra provincia",
            () => ChoosingMigrationTarget = !ChoosingMigrationTarget, affordable && p.Population - keep >= 1, ChoosingMigrationTarget,
            "Haz clic en una de tus provincias. Los ciudadanos viajan a 10 km/h.", Height: 34));
    }

    /// <summary>
    /// What the province's workshop or factory makes: a button for each model the nation can make (the one it makes
    /// lit), with how many pieces a day and what they cost, and one to stop it.
    /// </summary>
    private void ProductionSection(Document doc, Province p)
    {
        doc.Add(new Space(10));
        doc.Add(Section("Producción"));
        var current = GameSession.ProductionOf(p);
        doc.Add(new Paragraph(current is { } making
            ? $"Fabrica {making.SupplyName.ToLowerInvariant()}: {GameSession.ProductionRate(p, making):0.#} al día. En almacén: {Human.EquipmentOf(making):N0}."
            : "Parado: elige qué fabrica. Lo fabricado va al almacén de la nación (pestaña Almacén, N).", current == null ? Tone.Accent : Tone.Dim, After: 4));
        foreach (var (type, model) in GameSession.ProducibleModels(Human))
        {
            double rate = GameSession.ProductionRate(p, model);
            var cost = model.EquipmentCost;
            string costPerDay = cost.Items.Length == 0 ? "gratis"
                : string.Join(", ", cost.Items.Select(i => $"{i.Amount * rate / model.Pieces:0.#} {i.Type.Name().ToLowerInvariant()}"));
            var can = Session.CanProduce(p, model);
            doc.Add(new Button($"{model.SupplyName}  ·  {rate:0.#}/día", () => Show(Session.SetProduction(Human.Id, p.Id, model.Key)), can.Ok,
                current?.Key == model.Key, $"Para {model.Name.ToLowerInvariant()} ({type.Line().Name.ToLowerInvariant()}): {model.PiecesText(model.Pieces)} por batallón. " +
                $"Gasta {costPerDay} al día. En almacén: {Human.EquipmentOf(model):N0}." + (can.Ok ? "" : "\n" + can.Message),
                TextSize.Small, Height: 28, Gap: 4, Icon: new BattalionIcon(type)));
        }
        if (current != null) doc.Add(new Button("Parar", () => Show(Session.SetProduction(Human.Id, p.Id, null)), Size: TextSize.Small, Height: 28, Gap: 4));
    }

    /// <summary>The wings based at the province's airfield: planes, organisation and a button to disband each; wings are formed with the buttons above.</summary>
    private void AirfieldSection(Document doc, Province p)
    {
        doc.Add(new Space(8));
        var wings = Session.WingsAt(p).ToList();
        doc.Add(Section($"Aeródromo ({wings.Count}/{MilitaryRules.WingsPerAirfield} alas)"));
        if (wings.Count == 0) doc.Add(new Label("Sin alas: fórmalas con los botones de aviones de arriba.", Tone.Dim, Height: 22));
        foreach (var wing in wings) WingRows(doc, wing);
    }

    /// <summary>What sets a line apart in battle, beyond its numbers; null for the plain ones.</summary>
    public static string? LineNote(BattalionType type) => type switch
    {
        BattalionType.MountainInfantry => $"En montañas, colinas, bosques y pantanos lucha un {MilitaryRules.MountainTroopsRoughTerrain - 1:P0} mejor.",
        BattalionType.Medics => $"No combate: salva un {MilitaryRules.MedicsSaving:P0} de las bajas de su unidad.",
        BattalionType.AntiAir => $"Contra aviones dispara ×{MilitaryRules.AntiAirAgainstAircraft:0} y quita un {MilitaryRules.AntiAirShield:P0} a su fuego (hasta un {MilitaryRules.MaxAntiAirShield:P0}).",
        BattalionType.Paratroopers => "Podrán lanzarse en paracaídas cuando haya aviones de transporte.",
        BattalionType.Engineers => "Atacando, restan al defensor la ventaja del terreno y del río; construyen carreteras y ferrocarriles.",
        BattalionType.Scouts => "Exploran y reclaman tierra libre.",
        _ => null,
    };

    /// <summary>The battalions the owner's military advances have a training building (barracks or workshop) train faster, and how much.</summary>
    private static void TrainingImprovements(Document doc, BuildingType building, Player owner)
    {
        var faster = Battalions.All.Where(t => t.BestModel(owner.Techs) >= 0 && GameSession.ModelFor(owner, t).TrainingBuilding(t) == building
                                              && GameSession.TrainingSpeed(owner, t) > 0)
            .Select(t => $"{GameSession.ModelFor(owner, t).Name} -{1 - 1 / (1 + GameSession.TrainingSpeed(owner, t)):P0}").ToList();
        if (faster.Count == 0) return;
        doc.Add(new Paragraph("Instrucción más corta: " + string.Join(", ", faster) + ".", Tone.Good, After: 6, Indent: 10));
    }

    /// <summary>
    /// The province's buildings: the one under construction, the finished ones and, in your own
    /// provinces, a button for each building you can put up; the rest say what they are missing.
    /// Buildings whose advance you have not discovered are not listed.
    /// </summary>
    private void BuildingsTab(Document doc, Province p)
    {
        if (p.Constructing.HasValue || p.PlannedCityName != null)
        {
            var (name, days) = p.Constructing is BuildingType building
                ? (building.Info().Name, building.Info().Days)
                : ($"ciudad de {p.PlannedCityName}", GameRules.CityBuildingDays);
            doc.Add(new Heading($"En obras: {name}", Tone.Accent));
            doc.Add(new Bar(1 - p.ConstructionDaysLeft / (double)days, Tone.Accent, Tone.Groove, 8, 6));
            doc.Add(new Label($"Quedan {p.ConstructionDaysLeft} días", Tone.Dim, Height: 30));
        }

        doc.Add(Section("Construidos"));
        if (p.Buildings.Count == 0) doc.Add(new Label("Ninguno todavía.", Tone.Dim));
        foreach (var built in Buildings.All.Where(p.Buildings.Contains))
        {
            doc.Add(new Label(built.Info().Name, Tone.Good, TextSize.Normal, Icon: new BuildingIcon(built)));
            doc.Add(new Label(built.Info().Description, Tone.Dim, Height: 24, Indent: 10));
            // Barracks, and the workshop or the factory it became, list the troops they train faster.
            var trains = built == BuildingType.Factory ? BuildingType.Workshop : built;
            if (trains is BuildingType.Barracks or BuildingType.Workshop && p.OwnerId >= 0) TrainingImprovements(doc, trains, Session.Players[p.OwnerId]);
        }

        if (p.OwnerId != Human.Id) return;
        if (GameSession.HasWorkshop(p)) ProductionSection(doc, p);
        doc.Add(new Space(10));
        doc.Add(Section("Construir"));
        var missing = new List<(string Name, string Reason)>();
        if (!p.CityId.HasValue && p.PlannedCityName == null)
        {
            var site = Session.IsCitySite(Human.Id, p);
            if (!site.Ok) missing.Add(("Ciudad", site.Message));
            else
            {
                var can = Session.CanBuildCity(Human.Id, p);
                string tip = $"Los habitantes de la provincia levantan una ciudad con el nombre que elijas.\nCoste: {GameRules.CityCost}. Tarda {GameRules.CityBuildingDays} días."
                    + (can.Ok ? "" : "\n" + can.Message);
                doc.Add(new Button($"Ciudad  ·  {GameRules.CityCost}  ·  {GameRules.CityBuildingDays} d", () => OpenCityNaming(null, p.Id), can.Ok,
                    Tooltip: tip, Size: TextSize.Small, Height: 30, Gap: 4));
            }
        }
        // Buildings of advances not yet discovered stay out of the list altogether, and so do those replaced by something
        // better (the workshop, once factories are known).
        foreach (var type in Buildings.All.Where(t => !p.Buildings.Contains(t) && p.Constructing != t && IsBuildingKnown(t) && t.For(Human) == t))
        {
            var available = Session.IsBuildingAvailable(p, type);
            if (!available.Ok)
            {
                missing.Add((type.Info().Name, available.Message));
                continue;
            }
            var info = type.Info();
            var can = Session.CanBuild(Human.Id, p, type);
            string tip = $"{info.Description}\nCoste: {info.Cost}. Tarda {info.Days} días." + (can.Ok ? "" : "\n" + can.Message);
            doc.Add(new Button($"{info.Name}  ·  {info.Cost}  ·  {info.Days} d", () => Show(Session.Build(Human.Id, p.Id, type)), can.Ok,
                Tooltip: tip, Size: TextSize.Small, Height: 30, Gap: 4, Icon: new BuildingIcon(type)));
        }
        if (missing.Count == 0) return;
        doc.Add(new Space(6));
        foreach (var (name, reason) in missing)
            doc.Add(new Label($"{name}: {reason.TrimEnd('.').ToLowerInvariant()}", Tone.Disabled, Height: 20));
    }

    /// <summary>Battalions the province (its city, its barracks or its workshop) can train (those of undiscovered advances are not listed), HQs, and what is in training.</summary>
    private void ArmyTab(Document doc, Province p)
    {
        // The training buildings it lacks, of those the player can build: barracks always, the workshop once siege engines are known.
        var lacking = new[] { BuildingType.Barracks, BuildingType.Workshop }.Where(b => !p.Has(b) && IsBuildingKnown(b)).Select(b => b.For(Human)).ToList();
        if (lacking.Count > 0)
        {
            var reasons = lacking.Select(b => b == BuildingType.Barracks
                ? "Sin cuartel no entrena infantería ni caballería."
                : $"Sin {b.Info().Name.ToLowerInvariant()} no construye máquinas de guerra (catapultas, cañones, artillería, tanques, bombarderos).");
            string text = string.Join(" ", reasons) + (lacking.Count > 1 ? " Constrúyelos" : lacking[0] == BuildingType.Factory ? " Constrúyela" : " Constrúyelo") + " en la pestaña Edificios.";
            doc.Add(new Paragraph(text, Tone.Bad, After: 8));
        }
        doc.Add(Section($"Entrenar {Formations.CombatPlural}"));
        foreach (var template in Human.Templates.Take(4))
        {
            var can = Session.CanTrainTemplate(p, template);
            int days = GameSession.TrainingDays(Human, template);
            var known = Human.Techs;
            string tip = $"{template.Name}: {template.Composition(known)}.\n{template.Men(known)} hombres de la provincia. Ataque {template.Attack(known):0.#}, defensa {template.Defense(known):0.#}." +
                         $"\nCoste: {template.Cost(known)}. Equipo: {template.Equipment(known)}. {TextFormat.TrainingDaysText(days, template.TrainingDays(known))}" + (can.Ok ? "" : "\n" + can.Message);
            doc.Add(new Button($"{template.Name}  ·  {Formations.BattalionCount(template.Battalions.Count)}  ·  {days} d",
                () => Show(Session.TrainTemplate(Human.Id, p.Id, template.Id)), can.Ok, Tooltip: tip, Size: TextSize.Small, Height: 28, Gap: 4));
        }
        doc.Add(new Label(Human.Templates.Count > 4 ? "Más plantillas en la pestaña Plantillas de la nación (N)." : "Diseña plantillas en la pestaña Plantillas de la nación (N).",
            Tone.Dim, Height: 28));

        doc.Add(Section("Entrenar batallones sueltos"));
        foreach (var type in Battalions.All.Where(t => t.BestModel(Human.Techs) >= 0 && !t.Redundant(Human.Techs)))
        {
            var info = GameSession.TrainedModel(Human, type);
            var can = Session.CanTrain(p, type);
            int days = GameSession.TrainingDays(Human, type);
            string tip = $"{Formations.BattalionName(info)} ({type.Line().Name.ToLowerInvariant()}, {type.Line().Group.Name().ToLowerInvariant()}): " +
                         $"{info.Men} hombres de la provincia. Ataque {info.Attack:0.#}, defensa {info.Defense:0.#}, " +
                         $"organización {info.MaxOrganisation:0}, {info.Speed * GameRules.CitizenSpeedKmh:0.#} km/h." +
                         (info.Mounted ? "\nMontada: ataca a la mitad en bosques, pantanos y montañas." : "") +
                         (LineNote(type) is { } note ? "\n" + note : "") +
                         (info.Naval
                             ? $"\nVa a la cola de los astilleros (pestaña Marina de la nación), preferiblemente en este puerto. Cuesta {info.Cost}, que se paga mientras se construye: {days} días de grada. Al terminar toma {info.Men} hombres del puerto."
                             : $"\nCoste: {info.Cost}. {TextFormat.TrainingDaysText(days, info.TrainingDays)}") +
                         $" Mantenimiento: {TextFormat.UpkeepText([info.Cost])}." + (can.Ok ? "" : "\n" + can.Message);
            doc.Add(new Button(info.Naval ? $"Encargar {info.Name.ToLowerInvariant()}  ·  {days} d" : $"{info.Name}  ·  {info.TrainingCost}  ·  {days} d",
                () => Show(Session.Train(Human.Id, p.Id, type)), can.Ok,
                Tooltip: tip, Size: TextSize.Small, Height: 28, Gap: 4, Icon: new BattalionIcon(type)));
        }

        if (p.Has(BuildingType.Airfield)) AirfieldSection(doc, p);

        doc.Add(new Space(8));
        doc.Add(Section("Cuarteles generales"));
        foreach (var level in CommandLevels.All)
        {
            var can = Session.CanRaiseHeadquarters(p, level.Level);
            string tip = $"Manda hasta {level.MaxSubordinates} {Formations.SubordinatesPlural(level.Level)} a menos de {level.RangeKm:N0} km: " +
                         $"+{MilitaryRules.CommandBonus:P0} en combate y recuperación (+{MilitaryRules.HigherCommandBonus:P0} por cada nivel superior enlazado)." +
                         $"\n{level.Staff} hombres de la provincia. Coste: {level.Cost}. Tarda {level.TrainingDays} días." + (can.Ok ? "" : "\n" + can.Message);
            doc.Add(new Button($"{Formations.LevelName(level.Level)}  ·  {level.Cost}  ·  {level.TrainingDays} d",
                () => Show(Session.RaiseHeadquarters(Human.Id, p.Id, level.Level)), can.Ok, Tooltip: tip, Size: TextSize.Small, Height: 28, Gap: 4));
        }

        if (p.Training.Count == 0) return;
        doc.Add(new Space(8));
        doc.Add(Section("En instrucción"));
        foreach (var order in p.Training)
        {
            doc.Add(new Row(order.Name, $"{order.DaysLeft} d", Tone.Normal, Tone.Dim, Height: 18));
            doc.Add(new Bar(1 - order.DaysLeft / (double)order.TotalDays, Tone.Accent, Tone.Track, 5, 7));
        }
    }
}
