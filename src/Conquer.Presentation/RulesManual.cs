using System.Text;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Science;
using Conquer.Game.World;

namespace Conquer.Presentation;

/// <summary>
/// The rules manual: how the game works underneath, with its formulas and figures, topic by topic. Where the help
/// (<see cref="HelpTopics"/>) says what to do, this says how it is worked out. Figures come from the rules, so it
/// follows any rebalancing; <c>docs/REGLAS.md</c> is generated from it (<see cref="ToMarkdown"/>).
/// In the text, "## " starts a heading, "### " a subheading and "- " a bullet (see <see cref="Markup"/>).
/// </summary>
public static class RulesManual
{
    private static (string Title, string[] Text)[]? _all;

    /// <summary>The topics, written with Spanish numbers whatever the culture of the thread that first asks.</summary>
    public static (string Title, string[] Text)[] All => _all ??= InSpanish(Build);

    private static T InSpanish<T>(Func<T> write)
    {
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = TextFormat.Spanish;
        try { return write(); }
        finally { System.Globalization.CultureInfo.CurrentCulture = culture; }
    }

    private static (string Title, string[] Text)[] Build() =>
    [
        ("El tiempo",
        [
            "El juego avanza hora a hora. Cada hora se mueven las unidades, los emigrantes y los envíos, y se libra una hora de cada batalla. A medianoche se hace la cuenta del día, en este orden: economía (producción, comida, mantenimiento, moral y nacimientos), rebeliones, religión, migración, ciencia, instituciones, obras, talleres, ejército (reclutas, instrucción, suministro y recuperación), aviación, asedios, diplomacia, comercio, epidemias, decisiones y victoria.",
            "Salvo que se diga otra cosa, las cifras de este manual son por día.",
            "## Distancias y velocidad",
            $"- La gente camina {GameRules.CitizenSpeedKmh:0} km/h por terreno llano; las unidades y los emigrantes también. El terreno difícil, los ríos, la nieve y el barro la frenan; las carreteras y los ferrocarriles la aceleran.",
            $"- Los barcos van {GameRules.SailingSpeed:0} veces más deprisa por el mar.",
            $"- Los cuarteles generales van a caballo: {MilitaryRules.HeadquartersSpeed:0.#} veces más deprisa que la infantería.",
        ]),
        ("Población",
        [
            "## Capacidad",
            "Cada provincia alimenta a un número de habitantes según su tierra (bioma, ríos y superficie): su capacidad. Se multiplica por:",
            $"- Ciudad: ×{GameRules.CityCapacity(Era.Ancient):0.#} hasta el Renacimiento; la industria hace ciudades mucho mayores: ×{GameRules.CityCapacity(Era.Industrial):0.#} en la era Industrial y ×{GameRules.CityCapacity(Era.Modern):0.#} en la Moderna.",
            $"- Era de su dueño: ×{GameRules.EraCapacity(Era.Ancient):0.#} en la Antigua, ×{GameRules.EraCapacity(Era.Classical):0.#} en la Clásica, ×{GameRules.EraCapacity(Era.Medieval):0.#} en la Medieval, ×{GameRules.EraCapacity(Era.Renaissance):0.#} en el Renacimiento, ×{GameRules.EraCapacity(Era.Industrial):0.#} en la Industrial y ×{GameRules.EraCapacity(Era.Modern):0.#} en la Moderna.",
            "- Avances y edificios que la aumentan.",
            "## Nacimientos",
            "Cada día nacen en una provincia habitada:",
            $"- (habitantes × {GameRules.GrowthRate * 100:0.####} % + {GameRules.BaseBirthsPerProvince:0.##}) × fertilidad × (1 - habitantes / capacidad).",
            $"- En las ciudades la primera parte es ×{GameRules.CityGrowthMultiplier:0}. Sin hambre y con fertilidad normal, una provincia casi vacía crece en torno a un {GameRules.GrowthRate * 365:P1} al año y una a medio llenar, un {GameRules.GrowthRate * 365 / 2:P1}; una ciudad, el doble. Cuanto más se acerca a su capacidad, más despacio.",
            $"- Transición demográfica: hasta el Renacimiento nacen y mueren muchos, y el ritmo es el de arriba; en la era Industrial la medicina y la higiene lo multiplican por {GameRules.EraGrowth(Era.Industrial):0.#}, y en la Moderna las familias tienen menos hijos: ×{GameRules.EraGrowth(Era.Modern):0.#}.",
            "- Por encima de su capacidad no nace nadie, y con hambre nacen menos (ver Hambre).",
            "- La pequeña base fija hace que unos pocos colonos lleguen a formar un pueblo, pero no llena la tierra vacía por sí sola: eso lo hace la migración.",
            "## Fertilidad",
            $"- Multiplica los nacimientos (1 es lo normal). Tiende a 0,5 + moral / 100 (de 0,5 con moral 0 a 1,5 con moral 100), más lo que den los avances y edificios; sin nada que comer, al {GameRules.StarvingFertility:P0} de eso, y en proporción con el hambre.",
            $"- Cada día recorre un {GameRules.FertilityChangePerDay:P0} de la distancia a ese objetivo: va más despacio que la moral.",
            "## Comida",
            $"- Cada trabajador produce {GameRules.FoodPerWorker:0.##} de comida × el rendimiento de su tierra (más en las llanuras de los grandes ríos: ×{GameRules.RiverFertility:0.##}). Trabajan los habitantes hasta la capacidad; los que sobran producen solo el {GameRules.OvercrowdedFoodShare:P0}.",
            $"- Cada persona come {GameRules.FoodPerCitizen:0.##} al día: los habitantes, los soldados, los emigrantes en camino y las tripulaciones de los aviones.",
            $"- Lo almacenado se estropea: cada día se pierde el {GameRules.FoodSpoilage:P0}. Por eso las reservas se estabilizan en unos 100 días de excedente.",
            "- La producción se multiplica por la productividad de la moral (ver Moral) y por las bonificaciones de comida.",
            "## Hambre",
            $"- Si el almacén de comida no llega para lo que se come en el día, la nación pasa hambre en la proporción de lo que falta: si falta el 10 % de la comida, el hambre es del 10 %.",
            $"- Cada día muere el {GameRules.StarvationRate:P0} de la gente de cada provincia × el hambre (menos con los avances y los graneros que la protegen); los nacimientos bajan en la misma proporción, la moral recibe {GameRules.StarvingMood:0} × el hambre y la fertilidad tiende a bajar hacia el {GameRules.StarvingFertility:P0}. Así la escasez avisa antes de volverse una catástrofe.",
            "- Las provincias ocupadas por el enemigo ni producen ni comen para su dueño.",
        ]),
        ("Moral",
        [
            "Cada provincia tiene una moral de 0 a 100. Cada día recorre un "
                + $"{GameRules.MoodChangePerDay:P0} de la distancia hasta su moral objetivo: la suma de estos factores, entre 0 y 100.",
            "## Factores",
            $"- Base: +{GameRules.BaseMood:0}.",
            $"- Ciudad: +{GameRules.CityMood:0}. Capital: +{GameRules.CapitalMood:0}.",
            $"- Distancia a la capital: -1 por cada {GameRules.KmPerMoodPoint:0} km, hasta -{GameRules.MaxDistanceMoodPenalty:0} (la Administración y otros avances lo reducen).",
            $"- Reservas de comida: hasta +{GameRules.FoodReserveMood:0} cuando lo almacenado da para {GameRules.FoodReserveFullDays:0} días (proporcional por debajo).",
            $"- Fiestas: +{GameRules.FestivalMood:0} durante {GameRules.FestivalDays} días; cuestan {GameRules.FestivalGoldPerHundred:0} de oro por cada 100 habitantes (mínimo {GameRules.MinFestivalCost:0}).",
            $"- Hacinamiento: por encima de la capacidad, hasta -{GameRules.MaxOvercrowdingMoodPenalty:0} cuando hay el doble de gente de la que alimenta la tierra.",
            $"- Hambre: {GameRules.StarvingMood:0} × la parte de la comida que falta. Ocupación enemiga: {MilitaryRules.OccupiedMood:0}. Epidemia: -{GameRules.PlagueMood:0}.",
            $"- Cultura extranjera: hasta -{GameRules.ForeignCultureMood:0}, menos a medida que se asimila. Otra fe: -{GameRules.OtherFaithMood:0} hasta convertirse.",
            "- Avances, edificios (templos, anfiteatros...) y decisiones: lo que diga cada uno.",
            "## Efectos",
            "- Productividad: los habitantes trabajan × (0,75 + moral / 200): ×0,75 con moral 0, ×1 con 50 y ×1,25 con 100. Afecta a la comida, la madera, los impuestos, los yacimientos y la ciencia.",
            $"- Por debajo de {GameRules.UnrestMood:0} la provincia está descontenta: no paga impuestos y, sin guarnición, se acerca a la rebelión (ver Cultura, religión y rebelión).",
            "- La moral alta acelera la fertilidad, la asimilación y la conversión.",
            $"- Niveles: {GameRules.MoodNames[0]} (menos de {GameRules.UnrestMood:0}), {GameRules.MoodNames[1]} (menos de 45), {GameRules.MoodNames[2]} (menos de 65) y {GameRules.MoodNames[3]}.",
        ]),
        ("Migración",
        [
            "Toda migración ocurre dentro de una nación: nadie emigra a otra.",
            "## Migración espontánea",
            $"- Quién emigra: solo las ciudades no ocupadas con más de {GameRules.MinEmigrationCityPopulation} habitantes. Las provincias sin ciudad nunca envían gente.",
            $"- Cuántos: depende de lo llena que esté la ciudad (habitantes / capacidad). Hasta el {GameRules.EmigrationStartFill:P0} no se va nadie; desde ahí, cada vez más, hasta el {GameRules.DailyEmigrationShare:P1} de su gente al día cuando está llena. La gente se va por falta de sitio: una ciudad baja hasta donde sus nacimientos compensan a los que se van, en torno a un tercio de su capacidad, y ahí se queda en vez de vaciarse.",
            $"- Salen en grupos de {GameRules.MigrationGroup}. Lo que no llega a un grupo se acumula para el día siguiente. Una ciudad nunca baja de {GameRules.MinEmigrationCityPopulation} por emigrar.",
            "- Adónde: a las provincias de la nación sin ciudad, sin ocupar y a menos de 60 días de camino. Cada una toma su gente de la ciudad más cercana que esté enviando gente: una ciudad nueva, que aún no emigra, no deja sin emigrantes a las provincias de su alrededor.",
            $"- Cada provincia quiere llenarse hasta el {GameRules.MigrationTargetShare:P0} de su capacidad, o hasta lo llena que esté la ciudad que le manda gente si está más llena. Su déficit es eso menos sus habitantes y los que ya van hacia ella; sin déficit, no atrae a nadie.",
            $"- Cada grupo va a la provincia con más atracción: déficit / lo que quiere llenar / (1 + días de viaje / {GameRules.MigrationDistanceDays:0}). La distancia pesa poco (a {GameRules.MigrationDistanceDays:0} días se atrae la mitad), así que los emigrantes se reparten por toda la tierra en vez de llenar primero la más cercana.",
            $"- Las que tienen menos de {GameRules.SettledPopulation} habitantes (contando los que van en camino) atraen {GameRules.UnsettledMigrationPull:0} veces más: la tierra nueva recibe primero a sus primeros pobladores.",
            "- Cuando un grupo sale, la atracción de su destino baja en proporción a lo que ha cubierto.",
            "- Nunca hay migración espontánea de una ciudad a otra: eso solo lo consigue la migración forzada.",
            "## Cómo evoluciona",
            "- Al principio la capital, la única ciudad llena, da a cada provincia reclamada sus primeros pobladores y luego reparte su crecimiento entre todas.",
            $"- Las ciudades nuevas se suman cuando llegan al {GameRules.EmigrationStartFill:P0} de su capacidad.",
            $"- Cuando el campo pasa del {GameRules.MigrationTargetShare:P0}, la migración se para mientras las ciudades estén menos llenas que él, y vuelve cuando lo adelantan. Solo se detiene del todo cuando ciudades y campo están igual de llenos, o al llegar la era Industrial, en la que se invierte (ver Urbanización).",
            "## Urbanización",
            $"- Desde la era Industrial la migración cambia de sentido: el campo se vacía hacia las ciudades, que la industria ha hecho mucho mayores. Las ciudades ya no mandan gente al campo.",
            $"- Cada provincia sin ciudad con más de {GameRules.MinEmigrationCityPopulation} habitantes envía a su ciudad más cercana hasta el {GameRules.UrbanizationShare:P2} de su gente al día cuando está llena; nada por debajo del {GameRules.EmigrationStartFill:P0}, y en proporción entre medias.",
            $"- Una ciudad los acoge mientras no esté {GameRules.UrbanizationGap * 100:0} puntos más llena que la provincia que los manda, ni pase del {GameRules.UrbanizationCityFill:P0}. Así el campo se vacía poco a poco y las ciudades acaban con la mayor parte de la gente: en el banco de pruebas, en torno al 70 % al llegar la era Moderna.",
            "## Migración forzada",
            $"- Mueve la gente que elijas entre dos provincias tuyas no ocupadas, con o sin ciudad, si hay camino por tierra. Cuesta {GameRules.ForcedMigrationGoldPerTen:0} de oro por cada 10 personas.",
            $"- Una ciudad de origen conserva al menos {GameRules.MinCityPopulation} habitantes.",
            $"- Los emigrantes llegan con {GameRules.ForcedMigrantMoodPenalty:0} puntos menos de moral que la provincia de la que salieron.",
            "## Viaje y llegada",
            $"- Caminan a {GameRules.CitizenSpeedKmh:0} km/h por el camino más rápido y comen por el camino.",
            "- Al llegar, su moral se mezcla con la de los residentes, ponderada por el número de cada uno.",
            "- Llegan con la cultura y la fe de su nación: en una provincia de otra cultura o fe cuentan como gente asimilada y convertida, así que migrar acelera la asimilación.",
            "- Si el destino se ha perdido por el camino, se instalan en la capital.",
            "- El modo de mapa Población muestra con flechas a los emigrantes en camino.",
        ]),
        ("Producción",
        [
            "Todo lo que producen las provincias va al almacén de la nación, en la capital.",
            "## Lo que da cada provincia",
            "- Comida: ver Población.",
            "- Madera: según el bioma, por cada habitante que trabaja la tierra (hasta su capacidad).",
            $"- Oro: {GameRules.TaxGoldPerCitizen:0.###} de impuestos por habitante al día. Las provincias descontentas no pagan.",
            $"- Yacimientos: cada uno da su rendimiento × habitantes / {GameRules.DepositFullWorkers:N0} (al máximo con {GameRules.DepositFullWorkers:N0} habitantes). Solo se explotan los recursos que la nación conoce.",
            "- Todo se multiplica por la productividad de la moral y por las bonificaciones de los avances y edificios de la provincia (granjas, aserraderos, minas, mercados, puertos...).",
            "## Yacimientos",
            $"- Son bolsas finitas: se agotan. Los caballos salen de pastos, que se renuevan. Una provincia tiene como mucho {GameRules.MaxDepositsPerProvince} yacimientos.",
            "- Al agotarse uno, la provincia deja de dar ese recurso.",
            "## Gastos",
            "- Comida: lo que come la gente y lo que se estropea.",
            $"- Mantenimiento militar: cada batallón, barco o cuartel general cuesta al día el {MilitaryRules.UpkeepGoldShare:P0} de su oro y el {MilitaryRules.UpkeepResourceShare:P0} de sus demás materiales (la madera no). Ver Ejército.",
            "- Los talleres y las fábricas gastan los materiales del equipo que fabrican (salvo el oro).",
            "## Dificultad",
            "Los rivales del ordenador multiplican su producción y su ciencia según la dificultad: ×0,75 en Muy fácil, ×0,9 en Fácil, ×1 en Normal, ×1,2 en Difícil y ×1,5 en Muy difícil. La dificultad también cambia cuántos yacimientos hay, su tamaño y los recursos con que empiezas.",
        ]),
        ("Ciudades y obras",
        [
            "## Fundar ciudades",
            $"- Con colonos, en una provincia libre o tuya. Una ciudad envía colonos con {GameRules.SettlerCitizens} de sus habitantes y {GameRules.SettlersCost}.",
            $"- Sin colonos: en una provincia tuya con al menos {GameRules.CityBuildingPopulation} habitantes, por {GameRules.CityCost} y {GameRules.CityBuildingDays} días de obras.",
            "- Nunca en una provincia vecina de otra ciudad (construida o en obras).",
            $"- Empiezas con {GameRules.StartingCitizens:N0} colonos, {GameRules.StartingFood:N0} de comida, {GameRules.StartingWood:0} de madera, {GameRules.StartingGold:0} de oro y {GameRules.StartingStone:0} de piedra (según la dificultad).",
            "## Qué cambia una ciudad",
            $"- Capacidad ×{GameRules.CityCapacity(Era.Ancient):0.#} (×{GameRules.CityCapacity(Era.Industrial):0.#} en la era Industrial y ×{GameRules.CityCapacity(Era.Modern):0.#} en la Moderna) y crecimiento ×{GameRules.CityGrowthMultiplier:0}.",
            $"- Moral +{GameRules.CityMood:0} (+{GameRules.CapitalMood:0} más la capital).",
            "- Produce ciencia, envía emigrantes y colonos, y permite los mejores edificios.",
            "- Deja de recibir emigrantes: desde entonces crece por sus nacimientos, hasta la era Industrial, en la que es el campo el que se muda a ella.",
            "## Edificios",
            $"- Cada edificio pide un avance y cuesta materiales y días de obra (menos con los avances que aceleran la construcción). Hace falta un mínimo de {GameRules.SettledPopulation} habitantes y no puede haber otra obra en marcha en la provincia.",
            "- Granjas, graneros, aserraderos, minas, cuarteles, talleres y fábricas van en cualquier provincia habitada; el resto, solo en ciudades.",
            "- Las obras se paran mientras la provincia está ocupada.",
        ]),
        ("Ciencia",
        [
            "## Producción",
            $"- Cada ciudad no ocupada produce ({GameRules.ScienceBasePerCity:0.##} + habitantes × {GameRules.SciencePerCityCitizen:0.###}) puntos al día × la productividad de su moral × (1 + bonificaciones de ciencia de la nación y de sus edificios).",
            "- Las provincias sin ciudad no producen ciencia.",
            "## Reparto",
            $"- La ciencia del día se reparte entre las ramas que investigan algo, en proporción a su prioridad (de 0 a {GameRules.MaxResearchPriority}).",
            "- Una rama no toma más de lo que le falta a su avance: el resto pasa a las demás. Lo que nadie puede tomar se guarda y entra en el siguiente avance que elijas.",
            "## Coste de un avance",
            $"- Sus puntos × {GameRules.ResearchCostMultiplier:0}.",
            $"- -{GameRules.NeighbourResearchDiscount:P0} por cada nación vecina (con frontera) que ya lo conoce, hasta {GameRules.MaxNeighbourDiscounts} (-{GameRules.NeighbourResearchDiscount * GameRules.MaxNeighbourDiscounts:P0}).",
            $"- +{GameRules.InstitutionPenalty:P0} mientras no hayas adoptado la institución de su era.",
            "## Niveles y eras",
            $"- Cada rama tiene niveles. El siguiente se abre al conocer la mitad (redondeando hacia arriba, al menos uno) de los avances del nivel anterior. Algunos avances piden además otros concretos.",
            "- La era de tu nación es la de su rama más atrasada: la más moderna en la que conoces avances de las tres ramas. Adelantar mucho una sola rama no te lleva a una era nueva. La era cambia la capacidad de la tierra, los nombres de tus tropas y oficiales, los retratos y la música.",
        ]),
        ("Instituciones",
        [
            "Cada era desde la Clásica abre con una institución que nace en algún lugar del mundo y se extiende de provincia en provincia.",
            "## Dónde nacen",
            $"- Urbanismo: en la primera ciudad que llega a {GameRules.UrbanismBirthPopulation:N0} habitantes (la mayor).",
            $"- Feudalismo: en la capital de la primera nación con {GameRules.FeudalismBirthCities} ciudades.",
            "- Humanismo: en la mayor ciudad con universidad.",
            "- Industrialización: en la mayor provincia con fábrica y yacimiento de carbón.",
            "- Electrificación: en la capital de la primera nación que descubre la Electricidad.",
            "## Cómo se extienden",
            $"- Cada día, cada provincia con al menos {GameRules.SettledPopulation} habitantes que no la tiene puede tomarla de cada vecina que la tiene, con una probabilidad del {GameRules.InstitutionSpreadChance:P0} por vecina (×{GameRules.CityInstitutionSpread:0} si la provincia tiene ciudad). Avanza como mucho una provincia al día.",
            "## Adopción",
            $"- Tu nación la adopta sola cuando la tiene el {GameRules.InstitutionAdoptionShare:P0} de su gente.",
            $"- Antes, pagando {GameRules.InstitutionGoldPerCitizen:0.#} de oro por cada habitante que aún no la tiene (mínimo {GameRules.MinInstitutionGold:0}).",
            $"- Hasta adoptarla, los avances de su era cuestan un {GameRules.InstitutionPenalty:P0} más. Adoptada, da su bonificación.",
        ]),
        ("Cultura, religión y rebelión",
        [
            "## Cultura",
            "- La gente de una provincia es de la cultura de la nación que la pobló. Una provincia vacía toma la de su dueño.",
            $"- Bajo un dueño de otra cultura pierde -{GameRules.ForeignCultureMood:0} × (1 - asimilación) de moral.",
            $"- La asimilación avanza cada día clamp(moral / 50, {GameRules.MinAssimilationPace:0.##}, {GameRules.MaxAssimilationPace:0}) / ({GameRules.AssimilationYears:0} años): {GameRules.AssimilationYears:0} años con moral 50, {GameRules.AssimilationYears / GameRules.MaxAssimilationPace:0.#} con moral 100 y hasta {GameRules.AssimilationYears / GameRules.MinAssimilationPace:0} con moral muy baja. Al completarse, la provincia adopta tu cultura. Los emigrantes de tu cultura la aceleran.",
            "- Las provincias ocupadas no se asimilan.",
            "## Religión",
            $"- Igual que la cultura: bajo un dueño de otra fe pierde -{GameRules.OtherFaithMood:0} de moral hasta convertirse.",
            $"- La conversión avanza cada día clamp(moral / 50, {GameRules.MinAssimilationPace:0.##}, {GameRules.MaxAssimilationPace:0}) × (1 + {GameRules.TempleConversion:0} con templo + {GameRules.TheologyConversion:0.#} con Teología) / ({GameRules.ConversionYears:0} años).",
            $"- Las naciones de la misma fe se ven mejor (+{GameRules.SameFaithOpinion:0} de opinión) y las de otra, peor ({GameRules.OtherFaithOpinion:0}).",
            "## Rebelión",
            $"- Una provincia descontenta (moral menor que {GameRules.UnrestMood:0}) sin ningún regimiento suyo dentro suma cada día 1 + ({GameRules.UnrestMood:0} - moral) / {GameRules.UnrestMood:0} de rebelión. Al llegar a {GameRules.RevoltDays:0} se rebela: entre {GameRules.RevoltDays / 2:0} y {GameRules.RevoltDays:0} días, antes cuanto peor la moral.",
            $"- Con moral de {GameRules.UnrestMood:0} o más se calma {GameRules.RevoltCalmingPerDay:0} puntos al día. Una guarnición la para del todo.",
            $"- Si su gente es de otra nación que aún tiene tierras (y no es tu capital), se une a ella con moral de al menos {GameRules.LiberatedMood:0}.",
            $"- Si no, hay una revuelta: muere el {GameRules.RevoltDeaths:P0} de su gente, arde un edificio al azar y la moral queda en al menos {GameRules.AfterRiotMood:0}.",
        ]),
        ("Epidemias",
        [
            $"- Cada día, cada ciudad puede enfermar con una probabilidad de 1 entre {1 / GameRules.PlagueOutbreakChance:N0} × (1 + habitantes / {GameRules.PlagueOutbreakPeople:N0}) × (1 - resistencia): una ciudad pequeña, una vez cada {1 / (GameRules.PlagueOutbreakChance * 365):0} años de media; una de {GameRules.PlagueOutbreakPeople:N0}, cada {1 / (GameRules.PlagueOutbreakChance * 365 * 2):0}.",
            $"- Dura {GameRules.PlagueDays} días. Cada día mata al {GameRules.PlagueDeathRate:P1} de la gente × (1 - resistencia) y quita {GameRules.PlagueMood:0} de moral.",
            $"- Contagio diario: a cada provincia vecina con un {GameRules.PlagueSpread:P1}, por carretera o ferrocarril con un {GameRules.PlagueRoadSpread:P0}, y de un puerto a cada puerto a menos de {GameRules.PlaguePortKm:N0} km con un {GameRules.PlaguePortSpread:P1} (todo × (1 - resistencia)).",
            $"- La resistencia viene de la Medicina, la Salubridad, los Antibióticos, los herbolarios y los hospitales, hasta un {GameRules.MaxPlagueResistance:P0}.",
            $"- Al pasar, la provincia queda inmune {GameRules.PlagueImmunityYears:0} años.",
        ]),
        ("Ejército",
        [
            "## Reclutas",
            $"- La reserva de reclutas llega hasta {GameRules.BaseManpower:N0} + el {GameRules.ManpowerShare:P0} de la población asentada (más con Instrucción militar y Servicio militar obligatorio).",
            $"- Se rellena a razón de su máximo / {GameRules.ManpowerRecoveryYears:0} años: de vacía a llena en {GameRules.ManpowerRecoveryYears:0} años.",
            "- Entrenar, formar cuarteles generales y reforzar la gastan; licenciar una unidad devuelve sus hombres.",
            "## Instrucción",
            "- Cada batallón tiene sus días de instrucción, su coste en oro y su equipo. Sin el equipo en el almacén no se entrena.",
            $"- Cada avance que estudia una tropa la entrena un {MilitaryRules.TechTrainingSpeed:P0} más deprisa, y se suman.",
            $"- Un taller fabrica en los días de instrucción de un batallón el equipo de uno; una fábrica, ×{MilitaryRules.FactoryOutput:0}.",
            "## Organización",
            $"- Regimiento: hasta {MilitaryRules.MaxBattalionsPerRegiment} batallones. Brigada: hasta {MilitaryRules.MaxRegimentsPerBrigade} regimientos. División: hasta {MilitaryRules.MaxDivisionParts} brigadas y regimientos, y {MilitaryRules.MaxDivisionMen:N0} hombres.",
            $"- Cuerpo: {CommandLevels.Info(1).RangeKm:N0} km de alcance. Ejército: {CommandLevels.Info(2).RangeKm:N0} km. Grupo de ejércitos: {CommandLevels.Info(3).RangeKm:N0} km. Cada uno manda hasta {CommandLevels.Info(1).MaxSubordinates} unidades del nivel de abajo.",
            "## Mantenimiento",
            $"- Cada día cada batallón, barco o cuartel cuesta el {MilitaryRules.UpkeepGoldShare:P0} de su oro y el {MilitaryRules.UpkeepResourceShare:P0} de sus demás materiales (la madera no). Los oficiales intendentes y corruptos lo cambian.",
            $"- Si no hay con qué pagar, cada día cada batallón pierde el {MilitaryRules.UnpaidOrganisationLoss:P0} de su organización y deserta el {MilitaryRules.UnpaidDesertion:P0} de sus hombres, y nadie se recupera. Lo que se debía se perdona.",
            "## Oficiales",
            $"- Cuestan {MilitaryRules.OfficerCost:0} de oro. Tienen de 1 a {Officer.MaxSkill} estrellas y ganan una cada {Officer.VictoriesPerStar} victorias.",
            $"- Virtudes, por estrella: ofensivo +{Officer.AttackPerStar:P0} de ataque, defensivo +{Officer.DefensePerStar:P0} de defensa, organizador +{Officer.RecoveryPerStar:P0} de recuperación, táctico -{Officer.ShieldPerStar:P0} de organización perdida (hasta -{Officer.MaxShield:P0}), marchador +{Officer.SpeedPerStar:P0} de velocidad, intendente -{Officer.UpkeepPerStar:P0} de mantenimiento.",
            $"- Defectos, fijos: timorato -{Officer.TimidAttack:P0} de ataque, temerario -{Officer.RecklessDefense:P0} de defensa, desorganizado -{Officer.DisorganisedRecovery:P0} de recuperación, indeciso +{Officer.IndecisiveLoss:P0} de organización perdida, lento -{Officer.SlowSpeed:P0} de velocidad, corrupto +{Officer.CorruptUpkeep:P0} de mantenimiento.",
            "- Se aplican los rasgos del oficial de la unidad y los del general de su cuartel si está a su alcance.",
        ]),
        ("Combate",
        [
            "Una batalla se libra hora a hora hasta que un bando se queda sin unidades en ella.",
            "## Quién combate",
            $"- Cada bando pone en el frente sus mejores batallones de primera línea, tantos como caben: {MilitaryRules.FrontWidth(Biome.Grassland)} en llano, {MilitaryRules.FrontWidth(Biome.Hills)} en colinas, {MilitaryRules.FrontWidth(Biome.TemperateForest)} en bosques, selvas y pantanos, {MilitaryRules.FrontWidth(Biome.Mountains)} en montañas y {MilitaryRules.FrontWidth(Biome.HighMountains)} en alta montaña.",
            "- Detrás, hasta la mitad de esa cifra de artillería, aviación e ingenieros. El resto espera en reserva. Los médicos no combaten.",
            "## Fuego de cada batallón",
            "Ataque (si ataca) o defensa (si defiende) del modelo",
            "- × hombres / plantilla × (0,5 + 0,5 × organización / máxima)",
            $"- × (1 + {MilitaryRules.ExperienceBonus:P0} × experiencia)",
            "- × (1 + bonificación de mando + rasgos del oficial y del general)",
            $"- × {MilitaryRules.OutOfSupplyEfficiency:0.##} sin suministro, × {MilitaryRules.OutOfAmmoEfficiency:0.#} sin munición, × el efecto del cielo (hasta ±{MilitaryRules.AirSuperiorityBonus:P0})",
            "- y, solo los defensores, × el terreno × las murallas o el castillo × el atrincheramiento.",
            $"- La caballería y los carros atacan al {MilitaryRules.MountedRoughTerrainAttack:P0} en terreno difícil; las tropas de montaña disparan ×{MilitaryRules.MountainTroopsRoughTerrain:0.#} en él.",
            "## Fuego de cada bando",
            $"- La suma de sus batallones × (1 + armas combinadas) × un azar de ±{MilitaryRules.CombatRandomness:P0}, más el apoyo aéreo cercano.",
            $"- Armas combinadas: +{MilitaryRules.CombinedArmsBonus:P0} por cada tipo de tropa distinto combatiendo después del primero (infantería, caballería, artillería, blindados, aviación, ingenieros), hasta +{MilitaryRules.MaxCombinedArmsBonus:P0}.",
            $"- Antiaérea: con aviones enemigos en la batalla dispara ×{MilitaryRules.AntiAirAgainstAircraft:0}, y cada batallón suyo quita un {MilitaryRules.AntiAirShield:P0} al fuego de esos aviones (hasta {MilitaryRules.MaxAntiAirShield:P0}).",
            "## Terreno",
            $"- Los defensores disparan ×{MilitaryRules.TerrainDefense(Biome.Hills):0.##} en colinas, ×{MilitaryRules.TerrainDefense(Biome.Mountains):0.#} en montañas y ×{MilitaryRules.TerrainDefense(Biome.TemperateForest):0.#} en bosques, selvas y pantanos; ×{MilitaryRules.RiverDefense:0.##} más con río.",
            $"- Si el atacante lleva ingenieros, el río no cuenta y la ventaja del terreno se queda en el {MilitaryRules.EngineeredTerrainDefense:P0}.",
            $"- Atrincherarse da hasta +{MilitaryRules.EmplacementDefense:P0} de defensa en {MilitaryRules.EmplacementDays:0} días.",
            "## Daño",
            $"- El fuego enemigo se reparte entre los batallones que combaten; los de detrás reciben el {MilitaryRules.SupportExposure:P0} de lo que recibe uno del frente.",
            $"- Cada punto de fuego quita {MilitaryRules.OrganisationDamage:0.#} de organización y {MilitaryRules.StrengthDamage:0.#} hombres. Los médicos salvan el {MilitaryRules.MedicsSaving:P0} de las bajas de su unidad; los oficiales tácticos o indecisos cambian la organización perdida.",
            $"- Cada hora de combate da {MilitaryRules.ExperiencePerBattleHour:P1} de experiencia, menos cuanto más tiene.",
            "## Final",
            $"- Una unidad por debajo del {MilitaryRules.BreakingOrganisation:P0} de organización, o sin hombres, se rompe. El defensor roto se retira a una provincia vecina suya o libre sin enemigos; si no hay ninguna, queda rodeado y destruido. El atacante roto abandona el ataque.",
            "- Cuando no queda ningún defensor, los atacantes entran en la provincia. Los oficiales de los vencedores suman una victoria.",
            "## Cadena de mando",
            $"- Una unidad con su cuartel a su alcance recibe +{MilitaryRules.CommandBonus:P0} de fuego y de recuperación, y +{MilitaryRules.HigherCommandBonus:P0} por cada cuartel superior de la cadena que también esté a su alcance.",
        ]),
        ("Suministro y desgaste",
        [
            "## Suministro",
            $"- Sale de tus ciudades por tus carreteras y ferrocarriles, y desde ahí hasta {MilitaryRules.SupplyRangeHours / 24:0} días de marcha por tierra tuya, ocupada o libre, más una provincia (el frente).",
            "- Los sitiadores lo reciben también de cualquier provincia abastecida vecina de la que sitian.",
            $"- Sin suministro, una unidad dispara al {MilitaryRules.OutOfSupplyEfficiency:P0} y pierde cada día el {MilitaryRules.OutOfSupplyOrganisationLoss:P0} de su organización y el {MilitaryRules.OutOfSupplyAttrition:P0} de sus hombres.",
            "## Recuperación",
            $"- Con suministro y fuera de combate recupera cada día el {MilitaryRules.OrganisationRecovery:P0} de su organización × (1 + mando + rasgos); la mitad si está en marcha, ×{MilitaryRules.EmplacedRecovery:0.#} atrincherada.",
            $"- Los refuerzos llegan desde la capital en envíos: hasta el {MilitaryRules.ReinforcementRate:P0} de la plantilla de cada batallón al día, con su equipo, mientras haya reclutas. Los reclutas nuevos diluyen la experiencia.",
            "## Envíos y munición",
            $"- Los envíos tardan lo que se tarda desde la capital; ×{MilitaryRules.UnattachedShipmentSlowdown:0} y los últimos para las unidades sin cuartel a su alcance, y la mitad para las atrincheradas.",
            $"- Cada unidad lleva munición para {MilitaryRules.AmmoHours:0} horas. Cada batallón gasta {MilitaryRules.AmmoPerHundredMenHour:0.##} suministros por cada 100 hombres y hora de combate.",
            $"- Por mar, en convoyes de {MilitaryRules.ConvoyCapacity:0}. Cada día un envío en el mar pierde ataque / (ataque + {MilitaryRules.EscortWeight:0} × escolta + {MilitaryRules.ConvoyEvasion:0}) de su carga, como mucho el {MilitaryRules.MaxDailyConvoyLoss:P0}. Los submarinos atacan ×{MilitaryRules.SubmarineRaidBonus:0}.",
            "## Desgaste",
            $"- Invierno (por encima de {GameRules.MildWinterLatitude:0}° de latitud, lo peor desde {GameRules.HarshWinterLatitude:0}°): hasta el {GameRules.WinterAttrition:P1} de los hombres al día fuera de tus ciudades, y marchar cuesta hasta {1 + GameRules.WinterSlowdown:0} veces más. En primavera y otoño el barro frena un {GameRules.MudShare:P0} de lo que frena la nieve.",
            $"- Desierto: {GameRules.DesertAttrition:P1} al día. Cumbres y hielo polar: {GameRules.PeakAttrition:P1}. Alta montaña: {GameRules.HighMountainAttrition:P1}. Todo el año, con o sin suministro.",
        ]),
        ("Asedios",
        [
            $"- Una provincia con murallas o castillo no se ocupa al entrar: hay que sitiarla durante {MilitaryRules.SiegeDaysPerDefense:0} días × su bonificación de defensa ({MilitaryRules.SiegeDaysPerDefense / 2:0} con murallas, {MilitaryRules.SiegeDaysPerDefense:0} con castillo).",
            $"- Cada día se avanza 1 + la suma, por cada batallón de artillería sitiador, de su ataque / {MilitaryRules.SiegeAttackPerDay:0} × sus hombres / plantilla.",
            "- Si no queda ningún sitiador, o termina la guerra, el asedio se levanta y se pierde lo avanzado.",
            "- Las murallas y el castillo también multiplican el fuego de los defensores en batalla.",
        ]),
        ("Mar y aire",
        [
            "## Flotas",
            $"- Una fuerza tiene hasta {MilitaryRules.MaxShipsPerFleet} barcos. La Flota manda hasta {MilitaryRules.MaxFleetsPerFlota} agrupaciones a menos de {MilitaryRules.FleetCommandRangeKm:N0} km (+{MilitaryRules.FleetCommandBonus:P0}); la Armada, sus flotas a menos de {MilitaryRules.NavyCommandRangeKm:N0} km (+{MilitaryRules.HigherFleetCommandBonus:P0}).",
            $"- Sin combustible, una agrupación navega {MilitaryRules.OutOfFuelSlowdown:0} veces más despacio y dispara al {MilitaryRules.OutOfFuelEfficiency:P0}.",
            $"- En sus puertos se reparan y completan su tripulación (×{MilitaryRules.DryDockRepair:0} con dique seco) y se modernizan por el {MilitaryRules.RefitCostShare:P0} del coste del modelo nuevo.",
            "## Aviación",
            $"- Escuadrilla: {MilitaryRules.PlanesPerFlight} aviones. Aeródromo: {MilitaryRules.FlightsPerAirfield} escuadrillas; portaaviones: {MilitaryRules.FlightsPerCarrier}.",
            $"- División aérea: hasta {MilitaryRules.MaxUnitsPerAirDivision} unidades a menos de {MilitaryRules.AirDivisionRangeKm:N0} km (+{MilitaryRules.AirCommandBonus:P0}); Mando aéreo: divisiones a menos de {MilitaryRules.AirCommandRangeKm:N0} km (+{MilitaryRules.HigherAirCommandBonus:P0}).",
            $"- Cada misión cubre las provincias a menos de {MilitaryRules.MissionRadiusKm:N0} km de su objetivo y gasta {MilitaryRules.OilPerFlightDay:0} de petróleo por escuadrilla y día. Con menos del {MilitaryRules.MinFlyingOrganisation:P0} de organización no despega.",
            $"- Domina el cielo quien tiene al menos el {MilitaryRules.AirRuleShare:P0} de los cazas sobre él: sus tropas disparan hasta un {MilitaryRules.AirSuperiorityBonus:P0} más y las enemigas otro tanto menos; bajo cielo enemigo se marcha ×{MilitaryRules.UnderEnemyAirSlowdown:0.#} más despacio, se pierde el {MilitaryRules.UnderEnemyAirSupplyLoss:P0} de lo enviado y los bombarderos hacen el {MilitaryRules.UnescortedBomberEffect:P0}.",
            $"- Cada día una unidad aérea pierde fuego enemigo / (fuego enemigo + {MilitaryRules.AirDefenseWeight:0} × defensa + {MilitaryRules.AirEvasion:0}) de sus aviones, como mucho el {MilitaryRules.MaxDailyAirLoss:P0}.",
            $"- Bombardeo: cada punto de daño quita {MilitaryRules.BuildingDamagePerHarm:P2} a un edificio, que produce menos según su daño y se derrumba al llegar al 100 %. Cada día sin bombardeos repara un {MilitaryRules.BuildingRepairPerDay:P0} de su daño.",
        ]),
        ("Diplomacia y guerra",
        [
            "## Puntuación de guerra",
            $"- Cada provincia vale 1 + habitantes / {GameRules.PeoplePerValuePoint:N0} (hasta +{GameRules.MaxPopulationValue:0}), +{GameRules.CityValue:0} con ciudad o +{GameRules.CapitalValue:0} si es la capital.",
            $"- Puntuación = % del valor del enemigo que ocupas - % del tuyo que te ocupa + {GameRules.WarScorePerVictory:0} por batalla ganada - {GameRules.WarScorePerVictory:0} por perdida (las batallas, hasta ±{GameRules.MaxBattleWarScore:0}). Entre -100 y 100.",
            "## Paz",
            "- Paz blanca: todo vuelve a sus dueños.",
            "- Quedarte con lo ocupado cuesta el % del valor del enemigo que ocupas.",
            $"- Tributo: {GameRules.ReparationsWarScore:0} puntos; te paga el {GameRules.ReparationsShare:P0} de sus ingresos de oro durante {GameRules.ReparationsDays / 365} años.",
            $"- Vasallo: {GameRules.VassalWarScore:0} puntos; te paga el {GameRules.VassalTributeShare:P0} de sus ingresos, entra en tus guerras y puedes anexionarlo a los {GameRules.VassalAnnexYears:0} años.",
            $"- Tras la paz, {GameRules.TruceDays} días de tregua. Las provincias cedidas pierden {GameRules.CededMoodPenalty:0} de moral.",
            "- Una nación con todas sus ciudades ocupadas capitula: cada enemigo se queda con lo que ocupa y quien tiene su capital, con el resto.",
            "## Opinión",
            $"Cada nación piensa algo de las demás, de -100 a 100: lo que ve ahora más lo que recuerda, que se olvida a {GameRules.OpinionFadePerDay:0.#} puntos al día.",
            $"- Lo que ve: aliados +{GameRules.AllianceOpinion:0}, en guerra {GameRules.AtWarOpinion:0}, frontera común {GameRules.BorderOpinion:0}, enemigo común +{GameRules.CommonEnemyOpinion:0}, rival común más fuerte +{GameRules.CommonRivalOpinion:0}, pacto +{GameRules.PactOpinion:0}, paso militar dado +{GameRules.AccessOpinion:0}, misma fe +{GameRules.SameFaithOpinion:0}, otra fe {GameRules.OtherFaithOpinion:0}, gente suya bajo tu dominio {-GameRules.RuledProvinceOpinion:0} por provincia (hasta {-GameRules.MaxRuledPeopleOpinion:0}), vasallo {GameRules.VassalOpinion:0}, señor +{GameRules.OverlordOpinion:0}.",
            $"- Lo que recuerda: declararle la guerra {GameRules.DeclaredWarOpinion:0}, cada provincia quitada {GameRules.ProvinceTakenOpinion:0}, romper una alianza {GameRules.BrokenAllianceOpinion:0}, romper un pacto {GameRules.BrokenPactOpinion:0}, tributo {GameRules.ReparationsOpinion:0}, regalo +{GameRules.GiftOpinion:0}, liberarlo +{GameRules.ReleasedOpinion:0}.",
            $"- Un regalo cuesta {GameRules.GiftIncomeDays:0} días de tus ingresos de oro (mínimo {GameRules.MinGiftGold:0}).",
            "## Qué aceptan los rivales",
            $"- Alianza: con opinión de al menos {GameRules.AllianceAcceptOpinion:0}, y {GameRules.MaxAllies} aliados como mucho.",
            $"- Pacto de no agresión: con opinión de al menos {GameRules.PactAcceptOpinion:0}, o {GameRules.FearedPactOpinion:0} si tu ejército es más fuerte. Romperlo deja {GameRules.BrokenPactTruceDays} días de tregua.",
            $"- Paso militar: con opinión de al menos {GameRules.AccessAcceptOpinion:0}.",
            "- Paz: la que paga la puntuación de guerra, o la blanca si la guerra les va mal o se alarga.",
        ]),
        ("Comercio",
        [
            $"- Cada nación en paz ofrece el {GameRules.TradeSurplusShare:P0} de lo que gana al día de cada recurso.",
            "- Valor en oro de cada unidad: comida " + $"{GameRules.ResourceValue(Conquer.Game.Economy.ResourceType.Food):0.#}, madera y piedra {GameRules.ResourceValue(Conquer.Game.Economy.ResourceType.Wood):0.#}, carbón, cobre y caballos {GameRules.ResourceValue(Conquer.Game.Economy.ResourceType.Coal):0.#}, hierro, plata, azufre y salitre {GameRules.ResourceValue(Conquer.Game.Economy.ResourceType.Iron):0}, caucho {GameRules.ResourceValue(Conquer.Game.Economy.ResourceType.Rubber):0}, silicio, petróleo y aluminio {GameRules.ResourceValue(Conquer.Game.Economy.ResourceType.Oil):0}.",
            $"- Los rivales se quedan un margen de {GameRules.MaxTradeMargin / 2:P0} - opinión / 400 (entre 0 y {GameRules.MaxTradeMargin:P0}). Comercian con opinión de al menos {GameRules.TradeAcceptOpinion:0}.",
            $"- Cada acuerdo dura {GameRules.TradeDealDays} días y como mucho hay {GameRules.MaxTradeDeals} por nación. No hay acuerdo si lo vendido vale menos de {GameRules.MinTradeGoldPerDay:0} de oro al día.",
            $"- Cada acuerdo sube la opinión +{GameRules.TradeOpinion:0} (hasta +{GameRules.MaxTradeOpinion:0}); cancelarlo, {GameRules.CancelledTradeOpinion:0}. Se rompe si alguien no puede pagar o entregar, o con la guerra.",
        ]),
        ("Decisiones y victoria",
        [
            "## Decisiones",
            $"- Cada nación con ciudad recibe de media {GameRules.DecisionsPerYear:0.#} decisiones al año, con dos respuestas. Sin responder en {GameRules.DecisionDays} días, se toma la primera.",
            $"- Lo que está en juego crece con la raíz cuadrada de la población / {GameRules.DecisionPeople:N0}. La moral que cambian dura {GameRules.DecisionMoodDays} días.",
            "## Objetivos",
            $"- Cada objetivo cumplido da {GameRules.ObjectiveGold:0} de oro. El de población pide {GameRules.ObjectivePeople:N0} habitantes.",
            "## Victoria",
            "- Dominación: todas las demás naciones han caído o son tus vasallas.",
            "- Ciencia: descubrir todos los avances.",
            $"- Puntuación: al empezar el año {GameRules.ScoreVictoryYear}, gana quien tenga más puntos: 1 por cada {GameRules.ScorePeople:N0} habitantes, {GameRules.ScorePerProvince:0} por provincia, {GameRules.ScorePerCity:0} por ciudad y {GameRules.ScorePerTech:0} por avance.",
        ]),
    ];

    /// <summary>Every topic title and line of text, for checking them.</summary>
    public static IEnumerable<string> AllText => All.SelectMany(t => t.Text.Prepend(t.Title));

    /// <summary>A topic's text as headings, bullets and paragraphs.</summary>
    public static IReadOnlyList<MarkupLine> Lines(int topic) => All[topic].Text.Select(Markup.Parse).ToList();

    /// <summary>The whole manual as a Markdown document: each topic a section, its headings one level down.</summary>
    public static string ToMarkdown()
    {
        var text = new StringBuilder();
        text.Append("# Reglas de Conquer\n\n");
        text.Append("Cómo funciona el juego por dentro: fórmulas y cifras. Este archivo se genera a partir de `RulesManual` ");
        text.Append("(Conquer.Presentation), lo mismo que la pantalla Reglas del juego; no lo edites a mano.\n");
        foreach (var (title, lines) in All)
        {
            text.Append($"\n## {title}\n\n");
            MarkupKind? previous = null;
            foreach (var line in lines.Select(Markup.Parse))
            {
                // A blank line between blocks; the bullets of a list go line after line.
                if (previous != null && !(line.Kind == MarkupKind.Bullet && previous == MarkupKind.Bullet)) text.Append('\n');
                text.Append(line.Kind switch
                {
                    MarkupKind.Heading => $"### {line.Text}\n",
                    MarkupKind.Subheading => $"#### {line.Text}\n",
                    MarkupKind.Bullet => $"- {line.Text}\n",
                    _ => $"{line.Text}\n",
                });
                previous = line.Kind;
            }
        }
        return text.ToString();
    }
}
