# Guía del código de Conquer

Qué hace cada fichero y cada función del proyecto. Mantenla al día: cuando un cambio añada, quite o
cambie de sentido un fichero o una función, actualiza su sección aquí en el mismo commit.

Estado: versión 1.0.2.

---

## 1. Visión general

La solución (`Conquer.sln`) tiene cuatro proyectos:

| Proyecto | Tipo | Qué contiene |
| --- | --- | --- |
| `src/Conquer.Game` | Librería | Todas las reglas y la simulación: mundo, provincias, recursos, unidades, ciudades, migración, IA. **No sabe nada de gráficos**, así que se puede probar sin abrir ventana. |
| `src/Conquer.Client` | Ejecutable (`Conquer.exe`) | Ventana, dibujo del mapa por shader, interfaz propia, menús. Usa `Conquer.Game`. |
| `tools/Conquer.EarthData` | Herramienta de consola | Genera el mapa de la Tierra real (`earth.gz`) a partir de datos públicos. Solo se usa para regenerar ese fichero. |
| `tests/Conquer.Tests` | Tests (xUnit) | Pruebas de la generación del mundo, de las reglas y de la versión. |

Motor: propio. Silk.NET da la ventana, la entrada y OpenGL 3.3; StbTrueTypeSharp convierte la fuente en
texturas. Todo lo demás (mapa, interfaz, simulación) es código nuestro.

### Flujo de una partida

1. `Program.cs` lee los argumentos y crea `ConquerApp`.
2. `ConquerApp` abre la ventana y muestra la pantalla inicial (`MainMenuScreen`).
3. Al pulsar "Comenzar" en `NewGameScreen`, `LoadingScreen` genera en segundo plano el mundo (`WorldGenerator.Generate`),
   prepara los píxeles del mapa (`MapRenderer.Prepare`) y crea la partida (`GameSession.Create`).
   Al cargar una partida guardada, genera el mismo mundo a partir de sus ajustes y la recoloca en él (`GameSession.Load`).
4. `GameScreen` dibuja el mapa y la interfaz en cada fotograma y hace avanzar el tiempo llamando a
   `GameSession.Step()` (una hora de juego por llamada).
5. Cada `Step()` mueve unidades y migrantes y resuelve una hora de cada batalla; a medianoche calcula la economía, la migración, la ciencia, las obras y el ejército del día;
   cada 6 horas piensan los rivales (`AiPlayer.Think`).
6. Las órdenes del jugador (mover, atacar, fundar, reclamar, reclutar, entrenar, declarar la guerra, migrar) son métodos de `GameSession`
   que devuelven un `CommandResult` (éxito o motivo del fallo).

### Convenciones

- Coordenadas del mapa: raster equirectangular de 3600×1800 píxeles (0,1° por píxel). X da la vuelta
  al mundo (longitud); la fila 0 es el polo norte.
- Colores: `uint` en formato `0xAARRGGBB`.
- El tiempo se cuenta en horas desde el 1 de enero de 4000 a.C.
- Todas las cifras de equilibrio están en `GameRules.cs` y `Biome.cs`.
- Cada cambio sube `<Version>` en `Conquer.Client.csproj` y añade una entrada en `CHANGELOG.md`; el
  título del commit es el número de versión.

---

## 2. `src/Conquer.Game` — reglas y simulación

### `Rules/GameRules.cs`
Todas las constantes de equilibrio (por día de juego salvo que se diga otra cosa).

| Elemento | Qué es |
| --- | --- |
| `StartingCitizens/Food/Gold/Wood` | Recursos iniciales: 3.000 colonos (para tener pronto con quién formar un ejército), 3.000 de comida, 50 de oro, 100 de madera. |
| `CitizenSpeedKmh`, `SailingSpeed` | 10 km/h: velocidad de ciudadanos, migrantes y unidades en terreno llano; los barcos van el doble de rápido por el mar. |
| `FoodPerCitizen` | Comida que come cada ciudadano al día (0,1). |
| `FoodPerWorker` | Comida que produce cada trabajador en tierra de rendimiento 1 (0,13). |
| `GrowthRate`, `CityGrowthMultiplier` | Crecimiento diario de la población; las ciudades crecen el doble. |
| `StarvationRate` | Población que muere al día si no hay comida. Los avances (Medicina) y el granero de la provincia salvan cada uno su parte: con los dos, muere la cuarta parte. |
| `CityCapacityMultiplier` | Una ciudad alimenta 2,5 veces más gente que la tierra sola. |
| `TaxGoldPerCitizen` | Oro por habitante y día. |
| `DepositFullWorkers` | Habitantes necesarios para que un yacimiento rinda al máximo. |
| `DepositSizeMultiplier` | Multiplica el tamaño de todas las bolsas de recurso al empezar la partida (1; la dificultad ajusta el tamaño de cada bolsa al generar el mapa, con `DifficultyInfo.DepositSize`). |
| `ScienceBasePerCity`, `SciencePerCityCitizen` | Ciencia diaria de cada ciudad: 0,5 fijos más 0,001 por habitante (antes de humor y avances). |
| `MaxResearchPriority`, `DefaultResearchPriority` | Prioridad de cada rama de la investigación: de 0 a 10, 1 al empezar. |
| `LevelUnlockShare` | Parte de los avances de un nivel que hay que conocer para abrir el siguiente (la mitad: 1 de 2). |
| `UrbanismBirthPopulation`, `FeudalismBirthCities` | El Urbanismo nace en la primera ciudad de 5.000 habitantes; el Feudalismo, en la capital de la primera nación con 8 ciudades. |
| `InstitutionSpreadChance`, `CityInstitutionSpread` | Cada día, una provincia asentada recibe una institución con un 1 % de probabilidad por cada vecina que la tiene; el triple si tiene ciudad. |
| `InstitutionAdoptionShare`, `InstitutionGoldPerCitizen`, `MinInstitutionGold` | Una nación adopta sola una institución cuando la tiene la mitad de su población; antes cuesta 1 de oro por cada 10 habitantes que aún no la tienen (mínimo 50). |
| `InstitutionPenalty` | Mientras no se adopta, los avances de la era que abre cuestan un 50 % más. |
| `NeighbourResearchDiscount`, `MaxNeighbourDiscounts` | Un avance cuesta un 10 % menos por cada nación vecina que ya lo conoce, contando 3 como mucho. |
| `MinRiverFlow`, `GreatRiverFlow` | Agua que ha de reunir un tramo para ser río (60) y para ser gran río (300); solo los grandes ríos cambian el juego. |
| `PeakElevation`, `MinPeakPixels` | La tierra por encima de 5.000 m es cumbres (en los mapas del generador 2); las manchas de menos de 30 píxeles conservan su bioma. |
| `RiverFertility` | La tierra de un gran río da un 25 % más de comida y de capacidad. |
| `OvercrowdedFoodShare` | Lo que rinden los trabajadores que superan la capacidad de la tierra. |
| `DailyEmigrationShare`, `MinEmigrationCityPopulation` | Parte de una ciudad que emigra cada día, y población por debajo de la cual deja de enviar gente. |
| `SettledPopulation` | Habitantes con los que una provincia se considera asentada. |
| `MigrationTargetShare` | Las provincias atraen migrantes hasta llenar esta parte de su capacidad. |
| `MinCityPopulation` | Población mínima que debe quedar en una ciudad al reclutar o migrar. |
| `CityBuildingPopulation`, `CityCost`, `CityBuildingDays`, `MaxCityNameLength` | Para construir una ciudad sin colonos: 500 habitantes en la provincia, 150 de madera y 50 de oro, 60 días; el nombre tiene como mucho 24 letras. |
| `ForcedMigrationCost(citizens)` | Oro que cuesta una migración forzada (1 por cada 10 ciudadanos). |
| `StartingMood`, `BaseMood`, `CityMood`, `CapitalMood` | Humor inicial (60) y factores fijos del humor: base 50, +10 con ciudad, +15 en la capital. |
| `KmPerMoodPoint`, `MaxDistanceMoodPenalty` | −1 de humor por cada 50 km a la capital, hasta −20. |
| `MaxOvercrowdingMoodPenalty`, `StarvingMood` | Hasta −20 por hacinamiento (al doble de la capacidad) y −40 por hambre. |
| `ForcedMigrantMoodPenalty` | Los migrantes forzados llegan 20 puntos más descontentos que su provincia de origen. |
| `FoodReserveMood`, `FoodReserveFullDays` | Hasta +10 de humor por reservas de comida, completo con comida para 30 días. |
| `FestivalMood`, `FestivalDays`, `FestivalGoldPerHundred`, `MinFestivalCost` | Fiestas: +20 de humor durante 30 días por 2 de oro cada 100 habitantes (mínimo 10). |
| `FestivalCost(población)` | Oro que cuestan unas fiestas en una ciudad de ese tamaño. |
| `MoodChangePerDay`, `FertilityChangePerDay` | Parte de la distancia a su objetivo que recorren cada día el humor (10 %) y la fertilidad (3 %). |
| `UnrestMood` | Por debajo de 25 la provincia está descontenta y no paga impuestos. |
| `StarvingFertility` | Parte de la fertilidad que queda con hambre (20 %). |
| `MoodProductivity(humor)` | Multiplicador de la producción: ×0,75 con humor 0, ×1 con 50, ×1,25 con 100. |
| `TargetFertility(humor, hambre)` | Fertilidad hacia la que tiende una provincia: 0,5 + humor/100, por 0,2 si hay hambre. |
| `MoodNames`, `MoodLevel(humor)`, `MoodName(humor)` | Los cuatro niveles de humor (Descontento, Inquieto, Tranquilo, Contento), el nivel (0-3) de un valor y su nombre. |
| `SettlerCitizens`, `SettlersCost` | Una banda de colonos enviada desde una ciudad lleva 300 ciudadanos, y además cuesta esto. |
| `UnitType` | Tipos de unidad: `Settlers` (colonos), `Regiment` (regimiento), `Headquarters` (cuartel general) y `Fleet` (flota de barcos). |

### `Rules/MilitaryRules.cs`
Cifras del ejército. El combate se mide por hora; el resto, por día.

| Elemento | Qué es |
| --- | --- |
| `MaxBattalionsPerUnit`, `MaxShipsPerFleet`, `DryDockRepair` | Una unidad de combate tiene como mucho 12 batallones (una división); una flota, 10 barcos; un dique seco repara las flotas el doble de rápido. |
| `HeadquartersSpeed` | Los cuarteles generales marchan a 1,5 veces el paso de un ciudadano. |
| `OrganisationDamage`, `StrengthDamage` | Organización y hombres que pierde un bando por cada punto de fuego enemigo, repartidos entre sus batallones que combaten. |
| `BreakingOrganisation` | Una unidad se rompe por debajo del 10 % de su organización: el defensor se retira y el atacante abandona. |
| `CombatRandomness` | El fuego de cada bando varía un ±20 % cada hora. |
| `FrontWidth(bioma)` | Batallones que caben en primera línea: 12 en llano, 9 en colinas, 8 en bosques y pantanos, 6 en montañas y 4 en alta montaña. La artillería y la aviación, hasta la mitad, disparan desde detrás. |
| `SupportExposure` | La artillería y la aviación reciben una cuarta parte del daño que les tocaría. |
| `CombinedArmsBonus`, `MaxCombinedArmsBonus` | +10 % de fuego por cada tipo de tropa distinto más allá del primero entre los que combaten, hasta +30 %. |
| `ExperienceBonus`, `ExperiencePerBattleHour` | Un batallón de élite dispara un 50 % más; cada hora de combate gana un 0,3 % de lo que le falta. |
| `UpkeepGoldShare`, `UpkeepResourceShare` | Mantenimiento diario: un 2 % del oro y un 1 % de los demás recursos (salvo la madera) de lo que costó cada batallón, barco y cuartel general. |
| `UnpaidOrganisationLoss`, `UnpaidDesertion` | Sin pagar, cada día se pierde un 10 % de organización y deserta un 2 % de los hombres, sin recuperación ni refuerzos. |
| `MountedRoughTerrainAttack`, `TerrainDefense(bioma)`, `IsRough(bioma)` | La caballería ataca a la mitad en terreno difícil; el defensor dispara ×1,25 en colinas, ×1,5 en montañas y ×1,2 en bosques y pantanos. |
| `RiverDefense`, `DefenseMultiplier(provincia)` | Con un gran río el defensor dispara ×1,25 más (el atacante tiene que cruzarlo); se multiplica por el del terreno. |
| `CommandBonus`, `HigherCommandBonus` | +10 % en combate y recuperación con el cuartel propio a su alcance, y +5 % por cada nivel superior enlazado. |
| `OfficerCost`, `MaxUnitNameLength` | Reclutar un oficial para la reserva cuesta 40 de oro; el nombre que el jugador da a una unidad tiene como mucho 30 letras. |
| `SupplyRangeHours` | El suministro llega hasta 15 días de marcha desde una ciudad, por tierra propia o libre. |
| `OutOfSupplyEfficiency`, `OutOfSupplyOrganisationLoss`, `OutOfSupplyAttrition` | Sin suministro se lucha al 75 % y se pierde cada día un 5 % de organización y un 1 % de hombres. |
| `OrganisationRecovery`, `ReinforcementRate` | Con suministro y fuera de combate se recupera un 20 % de organización al día y un 5 % de hombres, que salen de la capital. |
| `OccupiedMood` | −30 de humor en una provincia ocupada por el enemigo. |

### `Military/Battalions.cs`
Los batallones que se entrenan en las ciudades, la tropa especializada de la que se forman los regimientos. **Aquí se añaden y equilibran las tropas.**

| Elemento | Qué es |
| --- | --- |
| `BattalionType` | Guerreros, Arqueros, Lanceros de bronce, Jinetes, Carros de guerra, Carros de arqueros e Infantería de hierro; y de la era Clásica, Legionarios (Tácticas militares), Catapultas (Maquinaria de asedio: mucho ataque, casi nada de defensa, lentas) Catafractos (Caballería pesada); de la Medieval, Caballeros (Estribo) y Ballesteros (Maquinaria); y del Renacimiento, Arcabuceros (Pólvora), Cañones (Metalurgia) y Mosqueteros (Ciencia militar), que gastan hierro y carbón; y de la Industrial, Fusileros (Estriado), Artillería de campaña (Acero) y Ametralladoras (mucha defensa); y de la Moderna, Infantería motorizada (Motor de combustión), Artillería pesada y Tanques (Blindados), que gastan petróleo y caucho, y Bombarderos (Aviación), que vuelan y gastan aluminio. Barcos: Trirreme y Barco de transporte (600 hombres) con Navegación a vela, Galeón (lleva 200) con Cartografía, Vapor de transporte (1.500) con Máquina de vapor, Acorazado con Acero, y Destructor y Portaaviones (este también pide Aviación) con Ingeniería naval, que solo se construyen en ciudades con dique seco (`Shipyard`). |
| `BattalionInfo` | Nombre, símbolo, hombres, coste, días de instrucción, avances que requiere (todos: los carros de arqueros piden La rueda y Tiro con arco), ataque, defensa, organización máxima, velocidad, si es montado, si vuela (`Flies`: los bombarderos), si es un barco (`Naval`: sus hombres son la tripulación y su ataque, sus cañones) y cuántos hombres lleva (`Capacity`). |
| `Battalions.All`, `Battalions.Info(tipo)` | Todos los batallones y la ficha de cada uno. |
| `BattalionRole`, `Role(tipo)`, `Name(papel)` | El papel de cada batallón en combate: infantería, caballería, artillería (catapultas y cañones), blindados, aviación o naval. |
| `Battalion` | Un batallón de una unidad: sus hombres (`Strength`), su organización, ambos como parte de su máximo, y su experiencia (`Experience`, 0 a 1; `ExperienceName`: Novato, Regular, Veterano o Élite). |

### `Military/Formations.cs`
Los nombres de las formaciones: batallón → unidad de combate (la que se mueve y lucha: regimiento, brigada o división según su tamaño) → cuerpo → ejército → grupo de ejércitos.

| Elemento | Qué es |
| --- | --- |
| `CombatName(batallones)`, `CombatPlural` | Una unidad de combate se llama Regimiento (1 a 3 batallones), Brigada (4 a 6) o División (7 a 12). |
| `LevelName`, `LevelPlural`, `SubordinatesPlural` | Nombre de cada nivel de cuartel general: Cuerpo, Ejército y Grupo de ejércitos. |
| `BattalionCount`, `BattalionName` | «3 batallones»; «Batallón de arqueros», o el tipo de barco («Trirreme»). |
| `CombatUnitName(número, batallones)`, `HeadquartersName(nivel, número)`, `Roman(n)` | «3.er Regimiento», «3.ª Brigada», «1.ª División» (el número se conserva al crecer); «IV Cuerpo», «1.er Ejército», «2.º Grupo de ejércitos». |
| `FleetName(número)`, `ShipCount(n)` | «2.ª Flota»; «3 barcos». |

### `Military/Officer.cs`
Los oficiales al frente de las unidades de combate y, como generales, de los cuarteles generales.

| Elemento | Qué es |
| --- | --- |
| `OfficerRank` | Coronel (regimiento), Brigadier (brigada), General de división (división), Teniente general (cuerpo), General (ejército) y Mariscal (grupo de ejércitos). |
| `OfficerTrait`, `IsFlaw` | Seis virtudes, que mejoran por estrella: Ofensivo (+10 % de ataque), Defensivo (+10 % de defensa), Organizador (+25 % de recuperación), Táctico (−10 % de organización perdida en combate, hasta −50 %), Marchador (+5 % de velocidad) e Intendente (−5 % de mantenimiento). Y sus seis defectos, en el mismo orden y fijos: Timorato (−15 % de ataque), Temerario (−15 % de defensa), Desorganizado (−25 % de recuperación), Indeciso (+20 % de organización perdida), Lento (−15 % de velocidad) y Corrupto (+20 % de mantenimiento). `(int)rasgo % 6` es lo que afecta. |
| `Officer` | Id, nombre, rasgos, estrellas iniciales (1 a 3), victorias y rango (que solo sube); `Skill` suma una estrella cada 3 victorias, hasta 5. `FireBonus`, `RecoveryBonus`, `OrganisationLoss`, `SpeedBonus` y `UpkeepChange` dan su efecto (negativo si estorba); `Title` («Coronel Hernán Ulloa»), `Summary` («Ofensivo, Lento **»), `TraitsDescription`, `TraitName`, `TraitDescription` y `RankName` lo describen. |
| `Recruit(id, azar, rango)` | Un oficial nuevo: nombre al azar, 1 a 3 estrellas, una virtud, a veces una segunda y a veces un defecto que no anula una de sus virtudes. |

### `Military/Templates.cs`
`RegimentTemplate`: diseño de unidad como en Hearts of Iron: qué batallones lleva (1 a 12). Las ciudades entrenan unidades enteras a partir de él. `Name` («Plantilla II»), `Men`, `Cost` (la suma de sus batallones), `TrainingDays` (el del batallón más lento, porque se instruyen a la vez), `Requires`, `Attack`, `Defense`, `MaxOrganisation`, `Speed`, `AnyMounted` y `Composition` («2 × Guerreros, 1 × Arqueros») lo resumen.

### `Military/Command.cs`
La cadena de mando, la instrucción y las batallas.

| Elemento | Qué es |
| --- | --- |
| `CommandLevelInfo`, `CommandLevels` | Los tres niveles de cuartel general: cuerpo, ejército y grupo de ejércitos, con su alcance (600 a 2.500 km), 5 subordinados, personal, coste y días. El nivel 0 (`Combat`) es la unidad de combate. Los nombres salen de `Formations`. |
| `TrainingOrder` | Lo que entrena una ciudad: un batallón, una unidad entera de una plantilla (`TemplateName`, `TemplateBattalions`) o un cuartel general, con los días que le quedan; `Name` lo nombra («Batallón de arqueros», «Brigada (Plantilla II)»). |
| `Battle` | Una batalla por una provincia: quién ataca, quién defiende, las unidades atacantes (que esperan en sus provincias) y cuándo empezó. |

### `Rules/Difficulty.cs`
Los niveles de dificultad. **Aquí se equilibran.**

| Elemento | Qué es |
| --- | --- |
| `Difficulty` | Muy fácil, Fácil, Normal, Difícil y Muy difícil. Se elige con el mapa y va en sus ajustes (`WorldSettings.Difficulty`), así que se guarda con la partida. |
| `DifficultyInfo` | Nombre, descripción y lo que cambia cada nivel: la probabilidad de la segunda tirada de yacimientos (`ExtraDepositChance`: de 3 a 0), el tamaño de las bolsas (`DepositSize`: de 1,5 a 0,5), los recursos iniciales del jugador humano (`StartingResources`: de 2 a 0,5) y lo que producen e investigan los rivales del ordenador (`ComputerOutput`: de 0,75 a 1,5). Normal deja todo en 1. |
| `Difficulties.All`, `Info(nivel)` | Todos los niveles y la ficha de cada uno. |

### `Rules/Modifiers.cs`
`Modifiers`: mejoras sobre las reglas normales. Los avances las aplican a toda la nación y los edificios a su provincia; todas se suman con `+`. Campos: parte extra de comida, madera, yacimientos, impuestos, ciencia, capacidad de la tierra y fertilidad; puntos de humor; parte de las muertes por hambre que se evita; daño extra de quien defiende la provincia (`Defense`), velocidad al cruzarla (`MoveSpeed`), rapidez de las obras (`BuildSpeed`: 0,25 las acaba en 1/1,25 de los días) y parte del humor perdido por la distancia a la capital que se evita (`DistanceMood`). `Modifiers.None` es «sin mejoras».

### `Buildings/Building.cs`
Los edificios que se construyen en las provincias. **Aquí se añaden y equilibran los edificios.**

| Elemento | Qué es |
| --- | --- |
| `BuildingType` | Granja, Granero, Aserradero, Mina, Templo, Biblioteca, Mercado, Acueducto, Herbolario, Anfiteatro (+10 de humor; pide Construcción), Muralla (+50 % de daño al defender; pide Fortificaciones) Calzada (la provincia se cruza un 50 % más deprisa; pide Ingeniería), Universidad (+50 % de ciencia; Educación), Banco (+50 % de impuestos; Banca) Castillo (defensores al doble de daño; Castillos), Fábrica (+50 % de madera y yacimientos; Industrialización), Hospital (+20 % de fertilidad, +10 % de capacidad; Salubridad) Ferrocarril (la provincia se cruza el doble de deprisa; Ferrocarril) Central eléctrica (+25 % de ciencia e impuestos; Electricidad), Puerto (en ciudades con costa: construye y repara barcos, +15 % de impuestos; Navegación a vela) y Dique seco (pide puerto: construye los barcos más avanzados y repara el doble de rápido; Ingeniería naval). Granja, granero, aserradero, mina, calzada y ferrocarril se construyen en cualquier provincia; el resto solo donde hay ciudad. Salvo la granja y el aserradero, cada uno pide un avance (el granero, Alfarería). |
| `BuildingInfo` | Nombre, descripción, coste, días de obra, avance que requiere, si solo va en ciudades, si necesita un yacimiento sin agotar, sus efectos (`Modifiers`) en la provincia, si necesita costa (`NeedsCoast`) y otro edificio antes (`RequiresBuilding`: el dique seco, el puerto). |
| `Buildings.All`, `Buildings.Info(tipo)` | Todos los edificios y la ficha de cada uno. |

### `Science/Technology.cs`
Los avances que se pueden investigar, en tres ramas con niveles; en cada nivel se elige qué avance investigar, como en Civilization. **Aquí se añaden y equilibran los avances.**

| Elemento | Qué es |
| --- | --- |
| `Era`, `Name(era)` | Las eras: Antigüedad, Clásica, Medieval, Renacimiento, Industrial y Moderna. Cada una después de la primera se abre con una institución. |
| `TechBranch`, `Name(rama)` | Las tres ramas: Economía, Sociedad y Militar. |
| `Tech` | Los avances. Los nuevos van al final del enum, porque las partidas guardadas guardan el progreso por posición. Por rama y nivel: Economía (1: Agricultura, Carpintería; 2: Minería, Irrigación; 3: Moneda), Sociedad (1: Escritura, Mitología; 2: Alfarería, Medicina; 3: Código de leyes) y Militar (1: Tiro con arco, Doma del caballo; 2: Trabajo del bronce, La rueda; 3: Trabajo del hierro). Los niveles 1 a 3 son de la Antigüedad; los 4 y 5, de la era Clásica: Economía (4: Comercio, Construcción; 5: Ingeniería, Navegación a vela), Sociedad (4: Filosofía, Matemáticas; 5: Drama y poesía) y Militar (4: Tácticas militares, Maquinaria de asedio; 5: Caballería pesada, Fortificaciones); Construcción acelera las obras un 25 % y Administración reduce a la mitad el humor perdido por distancia (Sociedad 5). Los 6 y 7, de la Medieval: Economía (6: Rotación de cultivos, Gremios; 7: Banca), Sociedad (6: Teología, Educación; 7: Astronomía) y Militar (6: Estribo, Maquinaria; 7: Castillos). Los 8 y 9, del Renacimiento: Economía (8: Economía, Minería profunda; 9: Cartografía), Sociedad (8: Imprenta, Anatomía; 9: Método científico) y Militar (8: Pólvora, Metalurgia; 9: Ciencia militar). Los 10 y 11, de la Industrial: Economía (10: Máquina de vapor, Industrialización; 11: Ferrocarril), Sociedad (10: Química, que revela el caucho, Salubridad; 11: Educación pública) y Militar (10: Estriado, Acero; 11: Ametralladoras). Los 12 y 13, de la Moderna: Economía (12: Refinado del petróleo, que revela el petróleo, Fertilizantes; 13: Producción en cadena, Ingeniería naval), Sociedad (12: Electricidad, que revela el aluminio, Antibióticos; 13: Electrónica, que revela el silicio) y Militar (12: Motor de combustión, Artillería pesada; 13: Blindados, Aviación). |
| `TechInfo` | Nombre, rama, nivel (se abre al conocer la mitad del nivel anterior de su rama), descripción, efectos en toda la nación (`Effects`, como `Modifiers`), avances que también pide, de su rama o de otra (`Requires`: Irrigación pide Agricultura; La rueda, Doma del caballo; Medicina, Escritura; Moneda, Escritura y Minería; Trabajo del bronce, Minería; Trabajo del hierro, Trabajo del bronce) , recursos que revela (`Reveals`: Minería el carbón, Trabajo del hierro el hierro) y su era (`Era`; todos los actuales son de la Antigüedad). `Cost` sale de su nivel. |
| `LevelCost(nivel)`, `LevelCosts` | Lo que cuesta cada nivel, igual en las tres ramas: 70, 160, 300, 500, 750, 1.100, 1.600, 2.300, 3.200, 4.500, 6.200, 8.500 y 12.000 puntos (sin contar el recargo por institución). **Aquí se equilibra la velocidad de la investigación.** |
| `Techs.All`, `Branches`, `Info(avance)`, `InBranch(rama)`, `InLevel(rama, nivel)`, `Levels(rama)`, `NeededToOpenNext(rama, nivel)` | Todos los avances, las ramas, la ficha de cada avance, los de una rama por nivel, los de un nivel, cuántos niveles tiene una rama y cuántos avances de un nivel abren el siguiente. |

### `Science/Institution.cs`
Las instituciones, como en Europa Universalis: ideas que abren cada era. **Aquí se añaden y equilibran.**

| Elemento | Qué es |
| --- | --- |
| `Institution` | Las instituciones: el Urbanismo abre la era Clásica el Feudalismo (+10 % de impuestos y +3 de humor) la Medieval el Humanismo (+10 % de ciencia y +3 de humor) el Renacimiento la Industrialización (+15 % de madera y de yacimientos) la Industrial y la Electrificación (+15 % de ciencia y +10 % de impuestos) la Moderna. |
| `InstitutionInfo` | Nombre, era que abre (`Opens`), dónde nace (`Birth`, para la interfaz), descripción, bonus al adoptarla (`Bonus`, como `Modifiers`: el Urbanismo da +10 % de ciencia y +10 % de fertilidad), color en el mapa y género (`Feminine`); `The` es su nombre con artículo («el Urbanismo»). |
| `Institutions.All`, `Info(institución)` | Todas las instituciones y la ficha de cada una. |

### `Economy/ResourceType.cs`
| Elemento | Qué es |
| --- | --- |
| `ResourceType` | Los 11 recursos: comida, madera, carbón, hierro, cobre, silicio, petróleo, aluminio, caucho, oro, plata. |
| `Resources.All` / `Resources.Deposits` | Todos los recursos / los que salen de yacimientos (todos menos comida y madera). |
| `Resources.KnownFromStart` | Recursos que todos conocen desde el principio: comida, madera, cobre, oro y plata. Los demás están ocultos, y no se pueden explotar, hasta que un avance los revela. |
| `Resources.Name(type)` | Nombre en español para la interfaz. |
| `Stockpile` | Almacén nacional de recursos de un jugador (`stockpile[recurso]`). |
| `Stockpile.Has(cost)` | ¿Hay suficiente para pagar un coste? |
| `Stockpile.TrySpend(cost)` | Paga el coste si puede; devuelve si lo ha pagado. |
| `ResourceCost` | Lista de (recurso, cantidad); `ToString()` lo escribe como "150 Comida, 50 Madera". |

### `Entities/Entities.cs`
Los objetos de una partida.

| Elemento | Qué es |
| --- | --- |
| `Player` | Jugador: id, nombre, color, si es humano, almacén, provincias que posee, capital, balance del último día (`LastDayNet`), si pasa hambre y cuántos días duraría su comida (`FoodReserveDays`).; avances conocidos (`Techs`) y la suma de sus efectos (`Bonuses`), prioridad de cada rama (`ResearchPriorities`, de 0 a 10) y la parte de la ciencia que le toca (`ScienceShare(rama)`), el avance que investiga cada rama (`Researching[rama]`, nulo mientras no elige), puntos puestos en cada avance (`ResearchProgress`), ciencia que ninguna rama pudo aceptar porque no investigaban nada (`SpareScience`) y ciencia del último día. recursos que conoce (`KnownResources`, `Knows(recurso)`); `Learn(avance)` añade un avance y sus efectos y revela sus recursos; sus plantillas (`Templates`); sus oficiales sin destino (`OfficerReserve`); si el último día no pudo pagar el mantenimiento del ejército (`ArmyUnpaid`); instituciones adoptadas (`Institutions`), y `Adopt(institución)` suma su bonus a `Bonuses`. |
| `City` | Ciudad: id, nombre, dueño, provincia, fecha de fundación y hasta cuándo dura su fiesta (`FestivalUntilHours`, `HasFestival(ahora)`).; lo que está entrenando (`Training`). |
| `Unit` | Unidad en el mapa: colonos, unidad de combate (regimiento, brigada o división según sus batallones), cuartel general o flota. Tipo, dueño (`Owner`), provincia, número, nombre (`Name`: el que le dio el jugador, `CustomName`, o si no `AutomaticName`, según el nivel, el número y, en las de combate, el tamaño), batallones, nivel (cuartel), su oficial (`Officer`: el de una unidad de combate o el general de un cuartel; `HasOfficer` dice si lleva, y `RequiredRank` el rango que pide su tamaño), cuartel del que depende (`CommanderId`), provincia que ataca (`AttackingProvinceId`) y ruta pendiente (`Path`). `Citizens` son los colonos, el personal o los hombres de sus batallones; `Speed`, la de su batallón más lento, cambiada por su oficial; `Flies`, si solo tiene aviones (cruza el mar); `IsFleet`, si es una flota, y `Capacity`, los hombres que lleva; `CarrierId`/`IsAboard`, la flota en la que viaja; `OrganisationShare`/`StrengthShare`, su estado; `CommandLevel` y `Symbol`, para la cadena de mando y la ficha. `HoursToNext`/`StepHours` miden el tramo actual; `StepProgress` da el avance (0..1) para dibujarla entre provincias. |
| `Migration` | Grupo de migrantes en camino: origen, destino, personas, salida, llegada, si es forzada y el humor que llevan (`Mood`). `Progress(ahora)` da el avance del viaje. |
| `Notification` | Mensaje para un jugador (fecha, jugador, texto). |

### `Simulation/GameDate.cs`
`GameDate` es un instante del juego (horas desde el inicio). Calendario de 365 días sin bisiestos.

| Elemento | Qué es |
| --- | --- |
| `Days`, `Hour`, `Year`, `DayOfYear`, `MonthAndDay` | Partes de la fecha; `Year` es negativo antes de Cristo y no existe el año 0. |
| `AddHours(h)` | Fecha tras sumar horas. |
| `ToString()` | "1 ene 4000 a.C., 00:00". |

### `Simulation/GameSession.cs`
El corazón del juego: una partida en marcha. Es una clase parcial: el ejército está en `GameSession.Military.cs` y la diplomacia en `GameSession.Diplomacy.cs`.

| Función | Qué hace |
| --- | --- |
| `CommandResult` | Resultado de una orden: `Ok` y un mensaje para el jugador. |
| `Create(map, jugadores, semilla, rivales)` | Nueva partida (los tests pueden quitar los rivales): limpia las provincias (dueño, controlador, población, ciudad, humor, fertilidad, edificios y bolsas de recurso llenas según `DepositSizeMultiplier`), crea jugadores con sus recursos iniciales (los del humano, multiplicados por la dificultad), una plantilla de dos cohortes de guerreros y una unidad de colonos cada uno en sitios fértiles y alejados, y crea las IA. |
| `UnitById`, `CityById`, `CityIn(provincia)` | Búsquedas. |
| `CapacityOf(provincia)` | Habitantes que puede alimentar una provincia, contando la bonificación de ciudad y los avances de su dueño (Irrigación) y sus edificios (Acueducto). |
| `BonusesOf(provincia)` | Mejoras que se aplican a una provincia: las de los avances de su dueño más las de sus edificios. |
| `MoodFactors(provincia)` | Lista de (causa, puntos) que forman el humor objetivo: base, ciudad, capital o distancia a ella (menos con Administración), fiestas, reservas de comida, hacinamiento, hambre, ocupación enemiga, avances que dan humor (Mitología) y edificios que dan humor (Templo). |
| `TargetMood(provincia)` | Suma de esos factores, entre 0 y 100. |
| `Stats(jugador)` | Totales de la nación (`NationStats`): población asentada, en unidades y migrando; provincias, ciudades y unidades; humor y fertilidad medios ponderados por habitantes, habitantes en cada nivel de humor y lo que queda en los yacimientos de sus provincias (`Reserves`). |
| `Step()` | Avanza una hora: mueve unidades, resuelve las batallas en tierra y en el mar, hace llegar migrantes; a medianoche economía, migración, ciencia, instituciones, obras y ejército; cada 6 h piensan las IA. |
| `ArriveMigrations()` | Suma los migrantes que llegan a su destino (si el destino se perdió o está ocupado, van a la capital), mezclando su humor. |
| `DailyEconomy(jugador)` | Las provincias ocupadas no producen ni comen para su dueño. Producción del día (comida, madera, oro, yacimientos) multiplicada por el humor, los avances y los edificios de cada provincia; los yacimientos sacan de su bolsa hasta agotarla (`Extract`), solo los de recursos que el jugador conoce; sin impuestos en provincias descontentas; consumo de comida, hambre, días de reserva de comida, humor y fertilidad, y crecimiento de la población (proporcional a la fertilidad). Resta el mantenimiento del ejército (`Upkeep`); si no llega, anota `ArmyUnpaid` y avisa al jugador. |
| `Extract(provincia, recurso, cantidad)` | Saca de la bolsa de un yacimiento lo que se pide o lo que queda, y avisa al jugador cuando se agota. |
| `UpdateMoodAndFertility(provincia, dueño, hambre)` | Acerca el humor a su objetivo y la fertilidad a la que marca el humor; avisa cuando una ciudad del jugador entra o sale del descontento. |
| `TargetFertility(provincia, dueño, hambre)` | Fertilidad hacia la que tiende una provincia, con los avances de su dueño (Medicina) y sus edificios (Herbolario). |
| `SciencePerDay(jugador)` | Puntos de ciencia al día de sus ciudades, por su humor, sus avances (Escritura) y sus edificios (Biblioteca); los de los rivales, además, por la dificultad. |
| `Difficulty`, `OutputMultiplier(jugador)` | La dificultad de la partida (la de los ajustes del mapa) / lo que multiplica la producción y la ciencia de un jugador: `ComputerOutput` para los rivales del ordenador y 1 para el humano. En `DailyEconomy` se aplica después de sacar de los yacimientos, para que la producción extra no los agote antes. |
| `DailyScience(jugador)` | Reparte la ciencia del día (más la guardada) entre las ramas que investigan algo, según su prioridad. Cada rama toma como mucho lo que le falta a su avance y lo demás pasa a las otras; si ninguna investiga nada, se guarda. Al completar un avance lo aprende, deja la rama sin investigación y avisa para elegir el siguiente. |
| `IsLevelOpen(jugador, rama, nivel)` | El nivel 1 siempre; los demás, cuando se conoce la mitad del anterior. |
| `CanResearch(jugador, avance)` / `Research(...)` | Comprueba (no conocido, nivel abierto y con sus requisitos) / pone la ciencia de su rama en ese avance, en lugar del que investigaba (los puntos de los dos se quedan); la ciencia guardada entra en él de inmediato. |
| `MissingRequirements(jugador, avance)`, `RequirementList(...)` | Avances que le faltan / su lista en texto. |
| `NeighbourNations(jugador)`, `ResearchCost(jugador, avance)` | Naciones con provincias junto a las suyas / lo que le cuesta un avance: un 10 % menos por cada vecina que ya lo conoce, hasta 3 (`NeighbourResearchDiscount`, `MaxNeighbourDiscounts`). |
| `SetResearchPriority(jugador, rama, prioridad)` | Cambia la prioridad de una rama (0 a `MaxResearchPriority`). |
| `IsBuildingAvailable(provincia, tipo)` | ¿Podría construirse aquí algún día? Tiene dueño, se conoce su avance y hay ciudad, yacimiento conocido, costa u otro edificio si los necesita (sin mirar coste ni obras). |
| `CanBuild(jugador, provincia, tipo)` / `Build(...)` | Comprueba (es tuya, no está construido, está disponible, no hay otra obra, tiene al menos 10 habitantes y puedes pagarlo) / paga y empieza la obra. `BuildDays(jugador, días)`: lo que dura una obra, menos con los avances que aceleran las obras. |
| `DailyConstruction(jugador)` | Cada obra avanza un día; al terminar, el edificio empieza a funcionar (o se funda la ciudad) y avisa al jugador. |
| `IsCitySite(jugador, provincia)` / `CanBuildCity(...)` / `BuildCity(jugador, provincia, nombre)` | ¿Pueden sus habitantes levantar una ciudad (es tuya, sin ciudad ni otra obra, ninguna ciudad hecha o en obras al lado, al menos 500 habitantes)? / lo mismo y además puedes pagarla / paga y empieza la obra, que ocupa la provincia como un edificio (`PlannedCityName`). |
| `CheckCityName(nombre)`, `SuggestCityName()` | El nombre no puede estar vacío, pasar de 24 letras ni repetir el de otra ciudad (hecha o en obras) / propone uno libre para el jugador. |
| `PlaceName(provincia)` | Cómo llamar a un lugar en los mensajes: su ciudad si la tiene, si no la provincia. |
| `DailyMigration(jugador)` | Cada ciudad no ocupada envía parte de su gente a las provincias propias poco pobladas; primero las vacías y las cercanas. Guarda las fracciones de persona para el día siguiente. |
| `CanFoundCity(unidad)` / `FoundCity(jugador, unidad, nombre)` | Comprueba / funda una ciudad con colonos: reclama la provincia, crea la ciudad con ese nombre (o uno al azar) y los colonos pasan a ser su población. `AddCity` la crea, la hace capital si es la primera y avisa. |
| `CanClaim(unidad)` / `Claim(...)` | Comprueba / reclama con una unidad militar la provincia libre en la que está (y avisa del nombre que recibe). |
| `SetOwner(...)`, `NameProvince(provincia)` | Cambia el dueño de una provincia; la primera nación que la reclama le pone nombre (`ProvinceNames.Next`, sin repetir), que se queda aunque cambie de manos. |
| `CanRecruitSettlers(ciudad)` / `RecruitSettlers(...)` | Comprueba / envía colonos desde una ciudad, pagando recursos y habitantes. |
| `Disband(...)` | La unidad se disuelve: sus ciudadanos pasan a vivir en la provincia (propia) donde está; una flota en puerto desembarca antes lo que lleva. |
| `CanHoldFestival(ciudad)` / `HoldFestival(...)` | Comprueba / paga unas fiestas que suben el humor de la ciudad durante 30 días (una a la vez). |
| `CanForceMigration(...)` / `ForceMigration(...)` | Comprueba / envía un número elegido de ciudadanos entre dos provincias propias pagando oro; viajan con el humor de su origen menos 20. |
| `Settle(provincia, personas, humor)` | Añade gente a una provincia mezclando su humor con el de los residentes según cuántos son (migrantes, colonos al fundar, unidades que se asientan). |
| `SetOwner(provincia, jugador)` | Cambia el dueño (y el controlador) de una provincia y avisa al cliente (`OwnershipChanged`). |
| `AddUnit`, `RemoveUnit` | Crean y quitan unidades (`AddUnit` también se usa en los tests); al quitar una, sus subordinados pierden el cuartel, sale de las batallas y su oficial vuelve a la reserva (salvo si la destruyeron). |
| `Notify(jugador, texto)` | Añade una notificación. |
| `FormatHours(h)` | "5 h", "2 d 3 h". |
| `PickStartProvinces(n)` | Elige posiciones iniciales fértiles, lo más separadas posible y en masas de tierra de al menos 200 provincias. |
| `LandmassSizes()` | Tamaño (en provincias) de la masa de tierra conectada de cada provincia. |

### `Simulation/GameSession.Military.cs`
El ejército dentro de la partida.

| Función | Qué hace |
| --- | --- |
| `Battles`, `BattleIn(provincia)` | Las batallas en curso y la de una provincia. |
| `AddRegiment(...)`, `AddHeadquarters(...)`, `NextUnitNumber(...)` | Crean unidades de combate y cuarteles (con un general recién reclutado de su rango) con el siguiente número de su nivel (el nombre sale de él: «3.er Regimiento», «II Cuerpo»); también se usan en los tests. |
| `CommanderOf(unidad)`, `SubordinatesOf(cuartel)` | Cadena de mando hacia arriba y hacia abajo. |
| `RegimentPower(unidad)`, `MilitaryPower(jugador)` | Valor aproximado de combate de un regimiento y de todo un ejército. |
| `EnemyRegimentsIn(provincia, jugador)` | Regimientos de naciones en guerra con el jugador en una provincia (los embarcados no cuentan). |
| `CanUnitEnter(unidad, provincia)`, `CanSail(jugador, mar)` | Tierra libre y propia siempre; la de otra nación solo para regimientos en guerra con ella; el agua solo para los aviones (las tropas van en barco). Una flota navega por el mar costero y los lagos con Navegación a vela, por el océano con Cartografía, y entra en sus puertos. |
| `MoveUnit(...)`, `UnitStepHours(...)` | Orden de mover (la ruta evita tierras vedadas; el tiempo depende de la velocidad de la unidad). Una unidad embarcada desembarca; una en tierra que apunta a una flota suya en el mar de al lado embarca. |
| `MoveUnits()` | Cada hora cada unidad avanza; un regimiento que entra donde hay tropas enemigas ataca, y si no las hay la ocupa (`EnterProvince`); una flota lleva su carga consigo y se detiene a combatir si encuentra barcos enemigos. |
| `Occupy(provincia, jugador)` | La provincia pasa a manos del jugador (o vuelve a su dueño) y los civiles, cuarteles y flotas enemigos huyen (los embarcados, con su flota). |
| `CanTrain`/`Train`, `CanRaiseHeadquarters`/`RaiseHeadquarters`, `CanRaiseTroops(...)` | Pagan y ponen en instrucción un batallón, un barco (solo en ciudades con puerto, y los más avanzados con dique seco; forma una flota nueva) o un cuartel; los hombres salen de la ciudad. `CanRaiseTroops` comprueba avances, ocupación, habitantes y coste. |
| `TemplateById`, `AddTemplate(...)` | Busca una plantilla del jugador y crea una nueva con el siguiente número (también la usan la IA y los tests). |
| `CreateTemplate`, `DuplicateTemplate`, `DeleteTemplate` | Plantilla nueva (con una cohorte de guerreros), copia de otra, o borrarla (siempre queda al menos una). |
| `CanAddToTemplate`/`AddToTemplate`, `RemoveFromTemplate` | Añaden un batallón conocido (hasta 6; nunca barcos) o quitan uno (queda al menos uno). |
| `CanTrainTemplate`/`TrainTemplate(...)` | Pagan todos los batallones de una plantilla a la vez; se instruyen juntos y forman un solo regimiento. |
| `DailyTraining(jugador)` | Las órdenes de instrucción avanzan; al terminar aparece el regimiento (con ese batallón o con los de la plantilla) o el cuartel en la ciudad. |
| `CanMerge`/`Merge`, `Split` | Unen dos unidades de combate de la misma provincia (hasta 12 batallones) o dos flotas (hasta 10 barcos, con su carga), o separan uno o varios batallones o barcos juntos en una unidad nueva, sin oficial (un barco no se separa si la carga no cabría en el resto). Al unir, la unidad conserva su oficial; el de la otra toma el mando si no tenía, o vuelve a la reserva. |
| `RenameUnit(...)` | Da a una unidad el nombre que elige el jugador (30 letras como mucho); uno vacío le devuelve el automático. |
| `CanRecruitOfficer`/`RecruitOfficer`, `NewOfficer(...)` | Reclutar un oficial (40 de oro) con rasgos al azar para la reserva, sin repetir el nombre de otro oficial de la nación. |
| `AssignOfficer`, `RelieveOfficer`, `RetireOfficer`, `Promote(unidad)` | Poner al mando de una unidad de combate o un cuartel a un oficial de la reserva (el anterior vuelve a ella), relevarlo o retirarlo para siempre. Un oficial asciende solo al rango que pide el tamaño de su unidad, y nunca baja. |
| `CanAttach`/`Attach`, `Detach` | Ponen una unidad bajo el mando de un cuartel del nivel superior (5 como mucho) o la quitan. |
| `InCommandRange(unidad)`, `CommandBonus(unidad)` | Si su cuartel la alcanza, y la bonificación de toda la cadena enlazada. |
| `ComputeSupply(jugador)`, `IsInSupply(unidad)`, `IsSupplied(jugador, provincia)` | Provincias abastecidas: hasta 15 días desde sus ciudades por tierra propia o libre (nunca por mar), y una más allá (el frente). |
| `InBattle(unidad)` | Si ataca o defiende. |
| `DailyMilitary(jugador)` | Cada día: instrucción, suministro, recuperación de organización, refuerzos desde la capital (los reclutas diluyen la experiencia), desgaste sin suministro (la unidad que se queda sin hombres se dispersa) y, si no se pagó el mantenimiento, pérdida de organización y deserciones sin recuperación; los oficiales y generales organizadores aceleran la recuperación y los desorganizados la frenan. Las tropas embarcadas ni se desgastan ni se recuperan; las flotas solo se reparan y completan su tripulación en puerto, el doble de rápido con dique seco. |
| `StartAttack(...)`, `CancelAttack(...)` | El regimiento se detiene en la frontera y ataca (se une a la batalla o la abre), o la abandona. |
| `ResolveBattles()` | Una hora de cada batalla: fuego de ambos bandos, daño, retiradas y abandonos; si no quedan defensores, los atacantes entran. |
| `Engage(unidades, provincia, ataca)`, `Engaged` | Los batallones de un bando que combaten esta hora: los mejores que caben en el frente (`FrontWidth`) y, detrás, hasta la mitad de artillería y aviación (exposición 0,25); el resto espera en reserva. Fuego de cada uno: ataque o defensa, hombres, organización, experiencia, mando, oficial propio y general, suministro, terreno y murallas. |
| `SideFire(...)`, `CombinedArms(papeles)` | Fuego de un bando: la suma de sus batallones, más las armas combinadas (+10 % por papel distinto, hasta +30 %) y azar. |
| `Damage(...)`, `Broken(...)`, `GeneralOf(unidad)` | Reparto del daño entre los batallones que combaten según su exposición (los oficiales tácticos ahorran organización y los indecisos la gastan; en el mar, entre todos los barcos); cuándo se rompe una unidad; el general del cuartel propio si está a su alcance. Cada hora de combate da experiencia, y cada victoria suma una a los oficiales y generales de los vencedores. |
| `DefenseMultiplier(provincia)` | Cuánto más daño hace quien defiende: el terreno por su muralla o castillo. |
| `EndBattle(...)`, `Retreat(...)`, `Destroy(...)` | Final de la batalla y avisos; retirada a una provincia vecina sin enemigos (o destrucción si está rodeada); una unidad destruida en combate pierde a su oficial, y una que se dispersa sin pago o sin suministro lo devuelve a la reserva. |

### `Simulation/GameSession.Institutions.cs`
Las instituciones dentro de la partida.

| Función | Qué hace |
| --- | --- |
| `DailyInstitutions()` | Cada día: hace nacer las instituciones cuyas condiciones se cumplen, las extiende y hace que las naciones adopten las que tiene la mitad de su población. |
| `Birthplace(institución)`, `Born(...)` | Dónde nace hoy (el Urbanismo, en la ciudad más poblada de al menos 5.000 habitantes; el Feudalismo, en la capital de la primera nación con 8 ciudades; el Humanismo, en la ciudad más poblada con universidad; la Industrialización, en la provincia más poblada con fábrica y yacimiento de carbón; la Electrificación, en la capital de la primera nación que descubre la Electricidad) / la hace nacer allí y avisa. |
| `IsBorn(institución)`, `BirthplaceOf(...)` | Si ya ha nacido / la provincia donde nació. |
| `SpreadInstitutions()` | Cada provincia asentada sin la institución puede recibirla de cada vecina que la tiene (más fácil si tiene ciudad); se decide para todas antes de aplicar, así que avanza una provincia al día como mucho. |
| `Reach(provincia, institución)` | La institución llega a una provincia; avisa al jugador la primera vez que entra en su nación. |
| `InstitutionShare(jugador, institución)` | Parte de la población de la nación que vive donde ha llegado. |
| `AdoptionCost(...)`, `CanAdopt(...)` / `Adopt(jugador, institución)` | Oro para adoptarla antes de tiempo / comprueba (ha nacido, ha llegado a alguna de sus provincias y hay oro) / la paga y la adopta. |
| `EraCostMultiplier(jugador, era)` | Lo que se encarecen los avances de una era: un 50 % más por cada institución suya sin adoptar. |

### `Simulation/GameSession.Naval.cs`
Las flotas: barcos construidos en puertos que navegan, llevan tropas y combaten en el mar.

| Función | Qué hace |
| --- | --- |
| `AddFleet(dueño, provincia, barcos)` | Pone una flota nueva con la tripulación completa (instrucción y tests); se numeran aparte (`FleetNumbering`). |
| `IsPort(provincia, jugador)` | Ciudad propia con el edificio Puerto que no ocupa el enemigo: donde se construyen, atracan y reparan los barcos. |
| `CanFleetEnter(...)`, `SyncCargo(flota)` | Por dónde puede ir una flota (el mar que sabe navegar su nación y sus puertos) / pone su carga donde está ella. |
| `CargoOf(flota)`, `CargoMen(flota)` | Las unidades que lleva y cuántos hombres suman, de su `Capacity`. |
| `CanEmbark`/`Embark(jugador, unidad, flota)` | Una unidad de tierra sube a una flota suya con transportes que está quieta en su provincia o en el mar de al lado, si cabe. |
| `Disembark(jugador, unidad, provincia)` | La unidad baja en el puerto de la flota o en una costa junto a ella; la tierra enemiga sin tropas se ocupa; una costa con tropas enemigas no se puede tomar desde el mar. |
| `EnemyFleetsIn(...)`, `NavalBattleProvinces()` | Flotas enemigas en una provincia / mares donde se encuentran flotas de naciones en guerra. |
| `ResolveNavalBattles()`, `NavalFire(flota)` | Cada hora, en cada mar con enemigos, cada nación dispara con sus barcos (ataque por tripulación y organización, con algo de azar) contra todos los barcos enemigos, calculando todo antes de aplicarlo. |
| `FleeOrSink(flota)`, `Sink(flota)` | La flota rota huye a un mar vecino (o a su puerto) sin enemigos; si no puede, o no le queda tripulación, se hunde con todo lo que lleva. |
| `NotifyNavalEncounter(...)` | Avisa al jugador cuando su flota topa con una enemiga, o una enemiga con la suya. |

### `Simulation/GameSession.Save.cs`
Guardar y cargar partidas.

| Función | Qué hace |
| --- | --- |
| `ToSave(versión)` | Convierte la partida en un `SaveGame`: jugadores, provincias que han cambiado, ciudades, unidades, migraciones, batallas, guerras, avisos, los planes de la IA y los contadores internos. |
| `Load(mapa, partida)` | Continúa una partida guardada sobre un mapa recién generado con sus ajustes. Si el mapa no es el mismo, lanza `InvalidDataException`. El azar de después se siembra con la semilla y la fecha. |
| `Fingerprint(mapa, ríos)`, `DrawnRiverFlows(mapa)` | Huella del mapa (bioma, tamaño, posición, vecinos y ríos de cada provincia) para comprobar que el generador sigue haciendo el mismo mundo. Al cargar también vale la huella con el río de cada provincia sacado de los ríos dibujados, como la guardaron la 1.31.0 y la 1.32.0. Un test fija la huella del mundo de los tests: si cambia, las partidas guardadas dejan de cargar. |

### `Simulation/SaveGame.cs`
`SaveGame`: la partida guardada como datos, escrita en JSON comprimido con gzip. El mapa no se guarda: se vuelve a generar a partir de `World`. `Write(flujo)` la escribe; `Read(flujo)` la lee y lanza `InvalidDataException` si está dañada o su formato (`Format`) es de otra versión. `InstitutionBirths` guarda dónde y cuándo nació cada institución (vacío en partidas anteriores). `ExtraDeposits` marca las partidas guardadas desde la 1.13.0: al cargar una anterior, los yacimientos que su generador no ponía empiezan llenos. Los registros `PlayerSave`, `ProvinceSave`, `CitySave`, `UnitSave` (con la flota que lleva a cada unidad, `CarrierId`, su oficial, `OfficerSave`, y el nombre que le dio el jugador), etc. son sus partes. Los oficiales en reserva van en `PlayerSave` y el siguiente número de oficial en `NextOfficerId`; `GeneralSave` es el general de las partidas anteriores a los oficiales (un solo rasgo), que al cargar pasa a ser un oficial del rango de su cuartel.

### `Simulation/GameSession.Diplomacy.cs`
Guerra y paz.

| Función | Qué hace |
| --- | --- |
| `AtWar(a, b)`, `EnemiesOf(jugador)`, `WarDays(a, b)` | Si dos naciones están en guerra, sus enemigos y cuánto dura la guerra. |
| `CanDeclareWar`/`DeclareWar` | Declara la guerra a otra nación. |
| `ProposePeace(jugador, otro)` | Propone la paz; la IA acepta si la guerra le va mal o se alarga (`AiPlayer.WouldAcceptPeace`). |
| `MakePeace(a, b)` | Firma la paz: terminan las batallas, las provincias ocupadas vuelven a sus dueños y los ejércitos regresan a su provincia más cercana. |

### `Simulation/Pathfinder.cs`
Rutas por el grafo de provincias. Solo por tierra: mares y lagos están cerrados (serán para unidades
navales); el hielo polar se puede cruzar.

| Función | Qué hace |
| --- | --- |
| `CanEnter(provincia)`, `Allowed(...)` | ¿Puede entrar un viajero sin regla propia (los migrantes)? (no, si es agua). Con una regla (`puedeEntrar`), la regla decide, agua incluida. |
| `StepHours(a, b)`, `Speed(provincia)`, `FastestSpeed` | Horas para ir de una provincia a su vecina: distancia entre centros / (10 km/h × facilidad del terreno × sus calzadas). La heurística de A* usa la provincia más rápida posible para no pasarse. |
| `FindPath(desde, hasta, puedeEntrar)` | Ruta más rápida (A*) y su duración, o `null` si no hay camino; `puedeEntrar` dice por dónde puede ir (para una unidad, `CanUnitEnter`: ni tierras ajenas en paz ni mar sin navegación). |
| `FromSources(orígenes, maxHoras, destinos, puedeEntrar)` | Horas desde el origen más cercano a cada provincia (Dijkstra) y cuál es ese origen. Se usa para la migración y para que la IA busque sitio. Puede parar al alcanzar todos los destinos. |
| `Heuristic` | Estimación para A*: distancia en línea recta a 10 km/h. |

### `Simulation/Names.cs`
| Elemento | Qué es |
| --- | --- |
| `PlayerNames.Pick(n, random)` | Nombres de naciones al azar; `Colors` son sus colores. |
| `CityNames.Next(usados, random)`, `Suggest(...)` | Genera un nombre de ciudad por sílabas sin repetir (y lo reserva, o solo lo propone). |
| `ProvinceNames.Next(usados, azar)` | Un nombre de provincia nuevo con otras sílabas (unas 37.000 combinaciones), sin repetir y sin vocales dobles ni tres seguidas; si se agotaran, añade un número romano. Se usa al reclamar una provincia. |

### `AI/AiPlayer.cs`
Rival controlado por el ordenador. Clase parcial: el ejército está en `AiPlayer.Military.cs`. Determinista (usa su propia semilla).

| Función | Qué hace |
| --- | --- |
| `Think(decisionesDiarias)` | Turno de la IA (sin contar flotas ni tropas embarcadas): clasifica regimientos nuevos, licencia soldados si hay hambre en paz, guía a colonos, reclamadores, soldados (en guerra) y cuarteles, y trae a casa las divisiones sin suministro; `PlayerId` identifica la nación; una vez al día, celebra fiestas, ajusta las prioridades de la investigación, elige qué investigar, recluta y construye. |
| `HoldFestivals()` | Paga fiestas en las ciudades con humor por debajo de 45 si, tras pagarlas, le quedan 30 de oro para reclutar. |
| `SetResearchPriorities()` | Prioridades de la investigación: Economía 2, Sociedad 1 y Militar 1 en paz; Economía 3 si pasa hambre; Militar 3 en guerra. |
| `ChooseResearch()`, `ResearchOrder` | Cada rama sin investigación elige el primer avance que puede de su orden de preferencia (Agricultura, Escritura, Tiro con arco, Carpintería... y en cada era nueva, primero lo que da ciencia, comida o mejores tropas). |
| `AdoptInstitutions()` | Compra las instituciones que ya han llegado a su nación si, tras pagarlas, le quedan 30 de oro para reclutar. |
| `BuildCityIfWorthIt()` | Mientras tenga menos de 8 ciudades, convierte en ciudad su provincia sin ciudad más poblada (con al menos 1.000 habitantes) cuando puede pagarla guardando para reclutar. |
| `Construct()`, `BuildOrder`, `WorthBuilding(...)` | Elige su edificio más deseado (ciudades primero, luego por población y orden de preferencia) donde compense: al menos 200 habitantes, aserraderos en tierra con madera, templos y anfiteatros donde hay inquietud, acueductos al 60 % de la capacidad, calzadas en las ciudades, y murallas y castillos en las de al menos 2.000 habitantes; universidades, bancos, fábricas, hospitales, ferrocarriles y centrales eléctricas en sus ciudades. Lo empieza cuando puede pagarlo guardando madera y oro para reclutar; si no, ahorra. |
| `GuideSettlers(unidad)` | Busca el mejor sitio cercano para una ciudad, va allí y la funda. |
| `GuideWarriors(unidad)` | Reclamadores en paz: Reclama la provincia si está libre; si no, va a la mejor provincia libre de su frontera. No reclama más rápido de lo que llegan los migrantes. |
| `Recruit()` | Envía colonos cuando una ciudad ha crecido lo bastante y entrena guerreros para reclamar tierra si le sobra comida. |
| `FreeBorderProvinces()` | Provincias libres y reclamables junto a su territorio. |
| `CanSettle(provincia)` | ¿Se puede fundar ciudad aquí? |
| `SiteScore(provincia)` | Lo buena que es una provincia: comida, yacimientos conocidos sin agotar y costa. |
| `TotalPopulation()`, `NearestCityDistanceKm()` | Ayudas. |

### `AI/AiPlayer.Military.cs`
El ejército de un rival.

| Función | Qué hace |
| --- | --- |
| `ClassifyNewRegiments()` | Los regimientos nuevos de una sola cohorte de guerreros cubren primero los puestos de «reclamadores» (uno por ciudad, más uno); el resto forma el ejército. |
| `BuildArmy()`, `Spare(coste)` | Desde el día 180, hasta tener 2 batallones por ciudad (4 en guerra), entrena unidades enteras de su plantilla, del tamaño que su ciudad puede dar (hasta 6 batallones: infantería con dos de choque), o si no el mejor batallón suelto; sin gastar la reserva ni entrenar si pierde oro cada día. |
| `BuildNavy()` | Una flota de guerra por cada tres puertos: el barco de combate con más ataque que puede construir, en su puerto más poblado. Las flotas se quedan en puerto. |
| `ArmyTemplate(tamaño)`, `CanSupply(tipo)` | Su plantilla: dos de su infantería más resistente, su tropa más ofensiva y otra de infantería, recortada al tamaño; solo con tropas cuyos materiales (cobre, hierro…) tiene o produce. |
| `OrganiseArmy()`, `RaiseAndAttach(...)`, `HighestHeadquarters` | Une unidades pequeñas (hasta 6 batallones), forma cuarteles de cuerpo y ejército cuando hacen falta y asigna a todos. |
| `StaffArmy()` | Una vez al día pone oficial a su mayor unidad sin él: el mejor de la reserva (más virtudes y estrellas, menos defectos) o uno nuevo si la reserva está vacía y le sobra el oro. |
| `FollowTroops(cuartel)` | El cuartel va adonde están sus unidades si alguna queda fuera de alcance. |
| `GuideSoldier(unidad)` | En guerra: acude a sus ciudades atacadas, ataca la provincia enemiga vecina más débil (si supera 1,3 veces su defensa) o marcha hacia tierra enemiga que su suministro alcance; descansa si está desorganizada. |
| `GoHomeIfCutOff(unidad)`, `IsEnemyLand(provincia)` | Un regimiento sin suministro vuelve a la capital. |
| `Diplomacy()`, `Neighbours()` | Tras dos años, a veces declara la guerra a un vecino con menos del 60 % de su poder; propone la paz a otros rivales cuando una guerra se alarga y va mal. |
| `WouldAcceptPeace(otro)`, `Winning(otro)` | Acepta la paz tras 60 días si no va ganando (o tras un año); va ganando si su ejército es mucho más fuerte y ocupa más de lo que ha perdido. |

### `World/Biome.cs`
| Elemento | Qué es |
| --- | --- |
| `Biome` | Los 18 biomas (océano profundo, océano, mar costero, lago, hielo polar, tundra, taiga, bosque templado, pradera, estepa, desierto, sabana, selva tropical, humedal, colinas, montañas, alta montaña y cumbres, este al final para no cambiar el número de los demás). Las cumbres no son habitables (no se reclaman) pero se cruzan despacio, como el hielo. |
| `BiomeInfo` | Ficha de cada bioma: nombre, si es agua, si es habitable, densidad de provincias (valores bajos, provincias grandes), rendimiento de comida y madera, habitantes por km², facilidad de paso y color. |
| `Biomes.Info(bioma)` | Devuelve la ficha. **Aquí se equilibra cada tipo de terreno.** |

### `World/Province.cs`
`Province`: id, nombre propio (`Name`: se lo pone la primera nación que la reclama; hasta entonces, y siempre en océanos y polos, está vacío y `DisplayName` usa el del bioma), bioma dominante, centro (píxel y lat/lon), área en km², altitud media, vecinas,
yacimientos (`Deposits`: producción diaria; `DepositSizes`: tamaño de la bolsa; `Reserves`: lo que queda en la partida), dueño, población, ciudad, humor (`Mood`, 0-100) y fertilidad
(`Fertility`, multiplicador de nacimientos, 1 = normal), instituciones que han llegado (`Institutions`). `ControllerId` es quién la tiene en la guerra (su dueño, o el enemigo que la ocupa) e `IsOccupied` si la ocupa otro. `IsWater`, `IsClaimable`, `IsOwned`,
`RiverFlow` (agua del mayor río que la cruza, 0 sin río), `HasRiver` (la cruza un gran río), `FoodYield` (rendimiento de comida de su bioma, más en un gran río),
`Capacity` (habitantes que alimenta su tierra, más en un gran río) y `HasDeposit(recurso)` (tiene ese yacimiento sin agotar) son atajos.
Edificios: los terminados (`Buildings`) y la suma de sus efectos (`BuildingBonuses`), el que está en obras (`Constructing`) o la ciudad en obras (`PlannedCityName`) y los días que le quedan (`ConstructionDaysLeft`). `AddBuilding(tipo)` añade uno terminado; `ClearBuildings()` los quita todos (nueva partida).

### `World/WorldMap.cs`
| Elemento | Qué es |
| --- | --- |
| `MapKind` | `Random` o `Earth`. |
| `RiverSegment` | Un tramo corto de río entre dos puntos del mapa, con el agua que lleva. |
| `WorldMap` | El mapa: tamaño, altitud, bioma e id de provincia por píxel, la lista de provincias, los ríos (`Rivers`) y los ajustes con que se generó (`Settings`). |
| `Latitude(y)`, `Longitude(x)`, `WrapX(x)` | Conversión de píxeles a grados y vuelta al mundo en X. |
| `ProvinceAt(x, y)` | Provincia en un píxel. |
| `PixelAreaKm2(fila)` | Área real de un píxel (menor cerca de los polos). |
| `DistanceKm(...)` | Distancia sobre la esfera entre dos puntos o provincias. |

### `World/WorldGenerator.cs`
| Elemento | Qué es |
| --- | --- |
| `WorldSettings` | Tipo de mapa, semilla, número objetivo de provincias (25.000), dificultad (Normal si no se dice, también en las partidas guardadas antes de que existiera) y versión del generador (`Generator`): las partidas nuevas usan `WorldGenerator.LatestGenerator` (2, con cumbres) y las guardadas antes de que existiera, el 1, así que su mapa se regenera igual. **Un cambio en la generación que altere el mapa debe ir en una versión nueva del generador**, o las partidas guardadas dejarán de cargar. |
| `Generate(ajustes, progreso)` | Crea el mundo en cinco pasos: relieve → clima y biomas → provincias → recursos → ríos (cada provincia guarda el mayor que la cruza). Las provincias nacen sin nombre. Informa del paso en curso para la pantalla de carga. |

### `World/EarthData.cs`
Formato del fichero `Assets/earth.gz` (Tierra real, 3600×1800): altitud en metros y marcas de tierra,
lago y glaciar por píxel.

| Función | Qué hace |
| --- | --- |
| `LoadEmbedded()` | Lee el fichero incluido en la DLL. |
| `Read(stream)` / `Write(ruta, ...)` | Leer y escribir el formato (gzip). |

### `World/Generation/Noise.cs`
| Elemento | Qué es |
| --- | --- |
| `Noise` | Ruido de Perlin 3D con semilla. Se muestrea sobre la esfera, así que el mapa no tiene costuras. |
| `Sample(x, y, z)` | Un valor de ruido (−1..1). |
| `Fractal(...)` | Suma de varias octavas: formas grandes con detalle. |
| `Ridged(...)` | Ruido con crestas afiladas, para cordilleras. |
| `Sphere.Point(x, y, ...)` | Punto de la esfera que corresponde a un píxel. |

### `World/Generation/TerrainGenerator.cs`
| Función | Qué hace |
| --- | --- |
| `Earth()` | Relieve, lagos y glaciares de la Tierra real. |
| `Random(...)` | Continentes aleatorios (ruido deformado), cordilleras y plataforma continental; unos 30 % de tierra. |
| `AreaPercentile(...)` | Nivel del mar que deja la fracción de tierra pedida (teniendo en cuenta el área real de cada píxel). |

### `World/Generation/ClimateGenerator.cs`
| Función | Qué hace |
| --- | --- |
| `Assign(...)` | Bioma de cada píxel a partir de temperatura (latitud y altitud) y humedad (bandas de latitud y distancia al mar). Con cumbres (generador 2), `MarkPeaks` convierte en cumbres cada mancha de tierra por encima de 5.000 m (sin contar lagos ni las latitudes de más de 60°, donde la altura es la del casquete polar). |
| `LatitudeMoisture(lat)` | Humedad según la latitud: ecuador húmedo, subtrópicos secos, latitudes medias húmedas, polos secos. |
| `Slope(...)` | Desnivel con los vecinos (para detectar colinas). |
| `DistanceToOceanKm(...)` | Distancia de cada píxel al mar (los interiores de los continentes son más secos). |

### `World/Generation/ProvinceGenerator.cs`
Divide el mapa en provincias.

| Función | Qué hace |
| --- | --- |
| `Generate(...)` | Proceso completo: semillas → crecimiento → relajación → segundo crecimiento → huecos → provincias. |
| `Category(bioma)` | Mar, lago, hielo, cumbres o tierra: una provincia nunca mezcla categorías. |
| `StepCosts(...)` | Coste con algo de ruido por píxel, para que las fronteras no sean rectas. |
| `PlaceSeeds(...)` | Reparte las semillas según la densidad del bioma y el área real (menos semillas en desiertos, polos y océanos, así que sus provincias son mayores). |
| `Grow(...)` | Cada semilla se extiende por su categoría (camino más corto con cola por cubos). |
| `Relax(...)` | Mueve cada semilla al centro de su región (paso de Lloyd) para formas más regulares. |
| `CircularMeanX(...)` | Media de X teniendo en cuenta que el mapa da la vuelta. |
| `FillLeftovers(...)` | Zonas sin semilla (islas, lagos aislados): las diminutas se unen a una vecina y el resto pasan a ser provincias propias. |
| `MergePeaks(...)` | Cada cordillera de cumbres (píxeles que se tocan, también en diagonal) pasa a ser una sola provincia. |
| `Neighbours4(...)`, `Edge(...)` | Ayudas. |
| `BuildProvinces(...)` | Crea los objetos `Province`: bioma dominante, área, altitud, centro y vecinas. |

### `World/Generation/ResourceGenerator.cs`
| Función | Qué hace |
| --- | --- |
| `Place(provincias, semilla, dificultad)` | Reparte yacimientos en las provincias habitables, agrupados por regiones; una provincia puede tener varios. Cada recurso se sortea una vez y, si falla, una segunda con la probabilidad multiplicada por `ExtraDepositChance` de la dificultad y con otros generadores, para que la primera tirada ponga los mismos yacimientos en todas las dificultades. Provincias con yacimiento: 52 % en Muy fácil, 43 % en Fácil, 30 % en Normal, 24 % en Difícil y 17 % en Muy difícil (con dos o más: 15 %, 9 %, 5 %, 3 % y 1 %). |
| `AddDeposit(...)` | Pone un yacimiento: una bolsa finita con producción diaria (`Deposits`) y tamaño total (`DepositSizes`) de 10 a 50 años de producción máxima por `DepositSize` de la dificultad, sorteado con su propio generador. |
| `Chance(recurso, bioma, latitud)` | Probabilidad de cada recurso según el terreno (caucho en selvas tropicales, petróleo en desiertos, etc.). |
| `Richness(recurso)` | Producción típica diaria de un yacimiento. |

### `World/Generation/RiverGenerator.cs`
Traza los ríos a partir del relieve, en una rejilla de 2×2 píxeles.

| Función | Qué hace |
| --- | --- |
| `Trace(...)` | Inundación por prioridad desde mares y lagos: cada celda de tierra desagua en la vecina que llegó primero (las hondonadas se cruzan como un lago lleno). La lluvia baja por ese árbol y cada celda con agua suficiente es río. Un poco de ruido en la altitud hace que serpenteen por el llano. |
| `Smooth(...)` | Une las celdas en tramos (de una fuente o confluencia a la siguiente o al mar), los suaviza y los corta en `RiverSegment`. El tramo que llega a una confluencia acaba en el mismo punto desplazado donde empieza el siguiente, para que se unan sin hueco. Devuelve también los tramos como se trazaban hasta la 1.30.1 (sin esos arreglos), solo para calcular el río de cada provincia (`RiverFlow`) igual que entonces, porque forma parte de la huella del mapa. |
| `TrimAtWater(...)` | Corta el final de un tramo que desemboca justo donde empieza el agua tal como la dibuja el mapa (la costa suavizada con pesos B-spline), en vez de en el centro de la primera celda de agua. |
| `Jitter`, `Unwrap`, `Wrap`, `Chaikin` | Ayudas: desplazamiento fijo para no seguir la rejilla, continuidad al cruzar el borde del mapa y suavizado de esquinas. |
| `Runoff(bioma)` | Agua que aporta cada bioma: mucha en selvas, bosques y montañas; poca en desiertos y hielo. |

### `Assets/earth.gz`
Mapa de la Tierra real, generado por `tools/Conquer.EarthData`. Se incluye dentro de la DLL.

---

## 3. `src/Conquer.Client` — ventana, gráficos e interfaz

### `Program.cs`
Punto de entrada. Comprueba OpenGL 3.3 (`GlSupport.Ensure`), pone el formato de números en español (a mano, sin depender de ICU) y lee los argumentos.

| Elemento | Qué es |
| --- | --- |
| `StartOptions.Parse(args)` | Opciones de línea de comandos para pruebas: `--new random|earth`, `--seed`, `--players`, `--difficulty veryeasy|easy|normal|hard|veryhard`, `--days` (funda la capital y avanza N días), `--zoom`, `--at longitud,latitud` (centra la vista ahí), `--mode terrain|political|population|mood|fertility|resources|institutions`, `--nation summary|cities|provinces|science|army|templates|diplomacy` (abre la pantalla de la nación), `--panel buildings|army|regiment|march|edit|found` (una pestaña de la provincia seleccionada, un regimiento de muestra, en marcha o con su ventana de edición abierta, o el diálogo para nombrar la primera ciudad), `--load fichero.conquer` (carga una partida guardada), `--menu new|load` (abre esa pantalla del menú) y `--screenshot fichero.png` (guarda una captura del juego y se cierra). |
| `QuickStart` | Partida que empieza directamente, sin menús. |

### `ConquerApp.cs`
| Función | Qué hace |
| --- | --- |
| `IScreen` | Interfaz de una pantalla: `Frame(dt)` actualiza y dibuja un fotograma. |
| `ConquerApp(options)` | Crea la ventana con OpenGL 3.3: 1600×900, o menor si no cabe en el monitor (`InitialSize`). |
| `UiScale`, `ScreenSize`, `PixelScale` | Las pantallas se colocan en píxeles lógicos (`ScreenSize`). Si la ventana es menor de 1280×820, `UiScale` (< 1) encoge toda la interfaz en proporción para que nada se salga; el ratón se divide por la misma escala. `PixelScale` son los píxeles reales por píxel lógico (pantallas HiDPI por la escala). |
| `Run()`, `Quit()`, `Show(pantalla)` | Arranca, cierra, cambia de pantalla al acabar el fotograma. |
| `OnLoad()` | Inicia OpenGL, la fuente, la interfaz y los eventos de ratón y teclado; muestra el menú o la partida rápida. |
| `OnRender(dt)` | Cada fotograma: limpia, dibuja la pantalla actual, la interfaz y el tooltip. |
| `CaptureIfRequested()` | Con `--screenshot`, guarda la imagen tras unos fotogramas y cierra; en los menús espera a que esté su fondo. |
| `ReadVersion()` | Lee la versión del ejecutable (la del `.csproj`). |

### `Screens/MenuScreens.cs`
| Elemento | Qué es |
| --- | --- |
| `MainMenuScreen` | Pantalla inicial sobre el fondo del mapa (`MenuBackground`), con el título con sombra y los botones en un panel: "Continuar" (carga la última partida guardada), "Nueva partida", "Cargar partida", historial de versiones y salir. |
| `NewGameScreen` | Nueva partida: tipo de mapa, semilla, número de jugadores, dificultad (con su descripción y los recursos con los que empiezas), "Comenzar" y "Volver". |
| `LoadGameScreen` | Lista de partidas guardadas, de la más reciente a la más antigua, para cargar o borrar (pide confirmación). |
| `LoadingScreen` | Genera el mundo en segundo plano y muestra el progreso, para una partida nueva o una guardada; al terminar abre `GameScreen`. |

### `SaveFiles.cs`
Partidas guardadas en disco, en la carpeta de datos del usuario: `~/.local/share/Conquer/Partidas` en Linux, `%LOCALAPPDATA%\Conquer\Partidas` en Windows y `~/Library/Application Support/Conquer/Partidas` en macOS. Cada una es un fichero `.conquer` que se llama como la nación y la fecha de juego ("Kartesia - 5 feb 3999 a.C. 00h"). `List()` las devuelve de la más reciente a la más antigua; `Save(partida)` escribe primero un fichero temporal y luego lo renombra, para no dejar nunca una partida a medio escribir; `Read(ruta)` y `Delete(partida)`.

### `Screens/GameScreen.cs`
La pantalla de juego. Clase parcial: el ejército en pantalla está en `GameScreen.Army.cs`, y la ventana de edición de unidades en `GameScreen.UnitEditor.cs`.

| Función | Qué hace |
| --- | --- |
| `GameScreen(...)` | Crea el renderizador y la cámara y centra la vista en tus colonos o, en una partida cargada, en tu capital (sin repetir los avisos antiguos). |
| `ApplyTestOptions(...)`, `ShowSampleArmy(...)` | Aplican `--days`, `--zoom`, `--at`, `--mode`, `--nation` y `--panel` (con `regiment`, entrena y selecciona una unidad de muestra bajo un cuerpo; con `march`, además la pone en marcha para ver su ruta; con `edit`, recluta oficiales y abre su ventana de edición, `ShowSampleOfficers`; con `found`, abre el diálogo para nombrar la ciudad de los colonos). |
| `Frame(dt)` | Fotograma: teclas, tiempo, refresco del mapa, dibujo del mapa, marcadores, paneles e interacción. |
| `AdvanceTime(dt)` | Convierte tiempo real en horas de juego según la velocidad (pausa, 1 h/s … 7 días/s). |
| `SetSpeed`, `HandleKeys` | Velocidad y teclado (Espacio, 1-5, Tab, Inicio, N, F1, +/-, WASD, Esc). Mientras la ayuda (`HelpView`) está abierta el tiempo se para y el mapa no responde; con la ventana de edición de una unidad abierta, las teclas van a su nombre (Intro renombra, Esc cierra). |
| `HandleMapMouse()` | Rueda para el zoom, arrastrar para mover el mapa, clics. |
| `LeftClick()` | Selecciona unidad o provincia, o elige el destino de una migración forzada. |
| `RightClick()` | Da orden de mover la unidad seleccionada (o de atacar, si el destino es enemigo). |
| `CenterOnHome()` | Centra la vista en la capital. |
| `ViewUnit(unidad)` | Selecciona una unidad y centra la vista en ella (desde la pantalla de la nación). |
| `NationRect`, `ViewProvince(provincia)` | Rectángulo de la pantalla de la nación, y seleccionar y centrar una provincia cuando se pide desde ella. |
| `Show(resultado)`, `CollectNotifications()` | Mensajes temporales en pantalla. |
| `Center`, `Between`, `OnScreen` | Posición de una provincia, punto intermedio entre dos (cruzando el borde del mapa por el lado corto) y si algo está en pantalla. |
| `DrawRivers()` | Dibuja los ríos con `RiverLayer` (con la vega verde solo en el modo terreno). |
| `DrawCities()` | Marcadores de ciudad y sus nombres. |
| `DrawMigrations()` | Puntos que representan a los migrantes en camino. |
| `DrawPath(unidad, desde, seleccionada)` | Ruta como flecha verde (`PathArrow`) por el centro de cada provincia del camino, cruzando el borde del mapa por el lado corto: entera para la unidad seleccionada y más tenue para tus demás unidades en marcha. |
| `DrawTopBar()`, `Compact()` | Barra superior: nación, población y humor medio, fecha, velocidades, recursos conocidos (icono, cantidad y cambio del día; el nombre, en el tooltip) y botones «?» (ayuda), Nación (con «!» si alguna rama no investiga nada teniendo avances disponibles) y Menú (con números abreviados: 12,3k, 2,9M). |
| `DrawSidePanel()`, `UnitPanel()`, `ProvincePanel()` | Panel derecho: datos y botones de la unidad o provincia seleccionada; el de provincia tiene pestañas General, Edificios y Ejército (humor y fertilidad, yacimientos con lo que les queda, fundar, fiestas, reclamar, asentarse, reclutar, migración forzada). |
| `MoodTooltip(provincia)` | Tooltip del humor con sus causas y su efecto en la producción. |
| `OpenCityNaming(...)`, `DrawCityNaming()`, `ConfirmCityName()` | Diálogo para nombrar una ciudad al fundarla con colonos o al construirla: propone un nombre, se puede escribir otro o pedir otro al azar, avisa si no vale y la funda o empieza la obra (Intro confirma, Esc cancela). Mientras está abierto el tiempo se para y las teclas van a la caja de texto. |
| `BuildingsPanel(...)` | Pestaña Edificios: la obra en curso (edificio o ciudad) con su barra, el botón «Ciudad» en provincias sin ciudad, los edificios terminados y, en tus provincias, un botón por cada edificio que puedes levantar; los que solo necesitan ciudad o yacimiento dicen qué les falta, y los de avances sin descubrir no aparecen (`IsBuildingKnown`). |
| `Line()`, `ResourceLine()`, `Paragraph()` | Ayudas para escribir filas (también con el icono de un recurso) y párrafos en el panel. |
| `DrawBottomBar()`, `ModeBarWidth` | Modos de mapa (un botón de 120 píxeles por modo) y ayuda de controles (si no cabe, quita atajos del medio y deja siempre «F1: ayuda» al final) (la ayuda se oculta con la pantalla de la nación abierta). |
| `DrawResourceFilter(barra)` | En el modo recursos, fila de botones para ver todos los yacimientos o solo uno de los recursos que conoces; hace de leyenda con el color de cada recurso. |
| `DrawMessages()`, `HoverTooltip()` | Mensajes en tiras redondeadas con una marca dorada (roja si algo falló) (más arriba si está abierto el filtro de recursos; con la pantalla de la nación abierta, solo el último, en la franja junto a los modos de mapa, con `DrawLatestMessageInStrip`) y tooltip de la provincia bajo el ratón (con sus yacimientos en el modo recursos y sus instituciones en el modo instituciones). |
| `DrawPauseMenu()`, `SaveCurrentGame()` | Menú de pausa (Esc): continuar, guardar la partida (`SaveFiles.Save`), ayuda, historial de versiones, volver a la pantalla inicial o salir. |

### `Screens/GameScreen.Army.cs`
El ejército en pantalla.

| Función | Qué hace |
| --- | --- |
| `ProvinceTab` | Pestañas del panel de provincia: General, Edificios y Ejército (esta solo en tus ciudades). |
| `DrawNationNames()` | El nombre de cada nación sobre su tierra: en su centro (media circular de las longitudes), del tamaño que ocupa en pantalla, oculto si se ve muy pequeña y desvanecido al acercarse mucho. |
| `DrawUnits()`, `Bar(...)` | Fichas estilo OTAN: aspa para infantería, barra para montados, marcas de nivel en los cuarteles y «C» para colonos; las flotas llevan un casco bajo la letra de su barco y un punto por cada unidad a bordo, y las unidades embarcadas no se dibujan. Barras de hombres (verde) y organización (ámbar). Dibuja la ruta (`DrawPath`), la línea a su cuartel (verde si está a su alcance) y una flecha roja (`PathArrow`) al atacar. |
| `DrawBattles()`, `DrawBattleMark(...)`, `BattleSummary(...)`, `NavalBattleSummary(...)` | Espadas cruzadas sobre cada batalla, en tierra o en el mar; al pasar el ratón, los dos bandos, su organización y el terreno. |
| `UnitPanel(...)`, `UnitState(...)`, `EmbarkButtons(...)` | Panel de la unidad: tipo, nación, ubicación, estado («A bordo de…», «Combatiendo en el mar») y botones («Editar unidad» en unidades de combate, cuarteles y flotas; fundar, reclamar, embarcar en una flota cercana con sitio, licenciar, detener). Una unidad embarcada explica cómo desembarcar. |
| `RegimentDetails(...)` | Suministro, velocidad, mando, oficial propio, general de su cuartel y experiencia media (en una flota: velocidad en el mar, si está en puerto y su carga), y cada batallón con sus barras, su papel y experiencia al pasar el ratón. Separar y unir se hacen en la ventana de edición. |
| `HeadquartersDetails(...)`, `CommandLine(...)`, `OfficerLine(...)`, `AttachButtons(...)` | Alcance, general (nombre y estrellas, con sus rasgos al pasar el ratón) y subordinados de un cuartel, de quién depende la unidad y botones para asignarla a un cuartel cercano o quitarla. |
| `ArmyPanel(ciudad)` | Pestaña Ejército de una ciudad: regimientos que puedes entrenar de tus plantillas (las cuatro primeras), batallones que puedes entrenar (los de avances sin descubrir no aparecen), cuarteles generales y lo que está en instrucción, con los nombres de la época. |

### `Screens/GameScreen.UnitEditor.cs`
La ventana para editar una unidad tuya (botón «Editar unidad» de su panel). El tiempo se para mientras está abierta.

| Función | Qué hace |
| --- | --- |
| `OpenUnitEditor(unidad)`, `CloseUnitEditor()`, `DrawUnitEditor()` | Abre, cierra y dibuja la ventana: a la izquierda el nombre, las tropas y las uniones; a la derecha, en unidades de combate y cuarteles, el oficial. Se cierra sola si la unidad desaparece. |
| `NameSection(...)`, `RenameEditedUnit()` | Caja de texto con el nombre, «Renombrar» (o Intro) y «Volver al nombre automático». |
| `BattalionSection(...)` | Cada batallón o barco es un botón que se marca; «Separar los marcados» los saca juntos en una unidad nueva. |
| `MergeSection(...)` | Botones para unir las demás unidades tuyas de la provincia, diciendo qué pasa con su oficial. |
| `OfficerSection(...)`, `OfficerCard(...)`, `OfficerTooltip(...)` | El oficial al mando con sus rasgos (virtudes en verde, defectos en rojo) y «Relevar del mando»; la reserva, con «Asignar» y «Retirar» para cada oficial; y «Reclutar oficial». |
| `ShowSampleOfficers(unidad)` | Para `--panel edit`: recluta cuatro oficiales, pone el primero al mando y abre la ventana. |

### `Screens/NationView.Military.cs`
Pestañas Ejército, Plantillas y Diplomacia de la pantalla de la nación.

| Función | Qué hace |
| --- | --- |
| `Army(...)`, `UnitActivity(...)`, `Plural(...)` | Orden de batalla, con el mantenimiento diario del ejército (en rojo si no se paga): cada cuartel con sus unidades en árbol, después las unidades sin cuartel y las flotas, con ubicación, hombres, organización, suministro, estado y «Ver». |
| `Templates(...)` | Diseñador de unidades: tus plantillas a la izquierda (nueva, duplicar, borrar); a la derecha los batallones de la elegida (hasta 12, con «Quitar»), botones para añadir los que conoces y lo que cuesta, su mantenimiento y cómo lucha una unidad de ese diseño; `UpkeepText` escribe un mantenimiento. |
| `Diplomacy(...)` | Cada nación: paz o guerra (y desde cuándo), su poder militar frente al tuyo, provincias, lo tomado y perdido, y los botones de declarar la guerra o proponer la paz. |

### `Screens/NationView.cs`
Pantalla de la nación (botón «Nación» o tecla N). El tiempo sigue corriendo mientras está abierta.

| Elemento | Qué es |
| --- | --- |
| `NationTab` | Pestañas: `Summary` (Resumen), `Cities` (Ciudades), `Provinces` (Provincias), `Science` (Ciencia), `Army` (Ejército), `Templates` (Plantillas) y `Diplomacy` (Diplomacia). |
| `NationView(partida, jugador, verProvincia, verUnidad, mostrar)` | Recibe qué hacer al pulsar «Ver» (centrar el mapa en una provincia o una unidad) y cómo mostrar el resultado de las órdenes. |
| `Frame(ui, área)` | Dibuja el panel opaco con las pestañas y el botón Cerrar. |
| `Summary(...)` | Resumen: población (total, asentada, en unidades, migrando), fertilidad media, humor medio con su barra por niveles (`MoodBar`), territorio, ciencia por día y, por rama, su parte y el avance en curso, cada institución (adoptada, cuánto de tu población la tiene o sin nacer), comida con sus días de reserva y el resto de recursos conocidos con su almacén, balance diario y lo que queda en sus bolsas. |
| `Cities(...)` | Tabla de ciudades: población / capacidad, humor, fertilidad, fiestas, enviar colonos o entrenar guerreros y «Ver». |
| `Provinces(...)` | Tabla de todas las provincias: población, humor, fertilidad, terreno, migrantes en camino y «Ver». |
| `ProvinceCells(...)` | Celdas de población, humor (con tooltip de causas) y fertilidad, comunes a las dos tablas. |
| `Header(...)`, `Sort(...)` | Cabeceras que ordenan al pulsarlas (nombre, población, humor, fertilidad; otra pulsación invierte el orden). |
| `SpanishSortKey(nombre)` | Clave para ordenar nombres en orden alfabético español sin ICU: sin mayúsculas ni tildes, y la ñ después de la n. |
| `Rows(...)` | Filas visibles con desplazamiento por la rueda del ratón y barra de desplazamiento. |
| `Science(...)`, `Branch(...)`, `TechCard(...)`, `ProgressBar(...)` | Pestaña Ciencia: puntos al día (con su desglose y los guardados), una segunda fila con un botón por era para ver sus niveles (`_scienceEra`; por defecto, la primera con avances por descubrir; «(+)» y su tooltip avisan del recargo si falta su institución), la institución que abre la era mostrada a la derecha, su estado con `InstitutionStatus` (adoptada, cuánto se ha extendido y el botón para adoptarla con oro, o sin nacer), y una columna por rama con su prioridad (− y +) y su parte, lo que investiga con barra y tiempo estimado (o un aviso para elegir), y sus niveles: cada uno con lo que hace falta para abrirlo y una tarjeta por avance con su estado, coste (con el descuento por vecinos o el recargo por institución), efecto, requisitos, los edificios y batallones que permite y el botón Investigar. |
| `Heading`, `Row`, `ViewButton`, `ProvinceName`, `Compact` | Ayudas de dibujo y formato. |

### `Graphics/MapRenderer.cs`
Dibuja el mapa entero con un único shader.

| Elemento | Qué es |
| --- | --- |
| `MapMode` | Terreno, político, población, humor, fertilidad, recursos e instituciones. |
| `ResourceFilter` | En el modo recursos, el único recurso que se muestra (o `null` para todos). |
| `IsResourceKnown` | Qué recursos conoce quien mira; los demás no se dibujan. |
| Shader de fragmentos | Para cada píxel de pantalla calcula el punto del mapa, busca la provincia en la textura de ids y la colorea según su dueño o su población. Con zoom alto mezcla las 3×3 celdas vecinas con pesos B-spline cuadráticos (`smoothRegions`, `strongest`) para trazar fronteras y costa como curvas suaves, sin escalones; el color del terreno sale solo de las celdas del mismo lado de la costa (`isWater`, en el canal verde de la textura de dueños) y la franja clara del agua sigue la distancia a la costa. Con zoom lejano compara con el píxel vecino. Oscurece la tierra junto a las fronteras nacionales (más gruesas), resalta la provincia seleccionada y la que está bajo el ratón, y aplica una viñeta suave hacia los bordes de la pantalla. |
| `Prepare(mapa)` | Prepara (fuera del hilo principal) los píxeles de ids y colores del terreno. |
| `ProvinceAt(mapa, punto, zoom)` | Provincia que se ve en un punto, con la misma regla que el shader (para los clics). |
| `SmoothZoom` | Zoom a partir del cual las fronteras se suavizan. |
| `Refresh(partida)` | Recalcula el color de cada provincia (según quién la controla: lo ocupado toma el color del ocupante dentro de las fronteras del dueño) y su dueño (texturas pequeñas de 256×128). |
| `PopulationColor(densidad)` | Escala de color del modo población. |
| `ScaleColor(valor)` | Rojo-amarillo-verde de 0 a 1, para los modos humor y fertilidad. |
| `DepositColor(provincia)` | Color del modo recursos: el del yacimiento principal que queda de los conocidos, o con filtro ese recurso más intenso cuanto más queda. Gris si no hay nada. |
| `ResourceColor(recurso)` | Color de cada recurso en el mapa y en la leyenda. |
| `InstitutionColor(provincia)` | Color del modo instituciones: el de la institución más reciente que ha llegado (más fuerte en las ciudades); gris si ninguna. |
| `Draw(cámara, ...)` | Pasa los parámetros al shader y dibuja; en el modo terreno las líneas entre provincias son más tenues, para que destaque el terreno. |

### `Graphics/TerrainColors.cs`
`Build(mapa)`: color de cada píxel según el bioma; en tierra, relieve iluminado desde el noroeste (medido sobre dos píxeles) y alturas algo más pálidas; en el mar, más oscuro cuanto más hondo. La franja clara de la costa la dibuja el shader del mapa.

### `Graphics/Camera.cs`
| Función | Qué hace |
| --- | --- |
| `ScreenToMap`, `MapToScreen` | Convierten entre píxeles de pantalla y del mapa (teniendo en cuenta la vuelta al mundo). |
| `Pan`, `ZoomAt`, `LookAt` | Mover, hacer zoom alrededor del ratón, centrar. |
| `Clamp()` | No deja salirse por arriba o por abajo del mapa. |

### `Graphics/Batch2D.cs`
| Elemento | Qué es |
| --- | --- |
| `Rgba` | Color con `WithAlpha` (transparencia) y `Scale` (aclarar u oscurecer). |
| `Batch2D` | Acumula rectángulos, líneas y letras y los envía juntos a la GPU. |
| `Begin`, `Flush` | Empezar un fotograma / dibujar lo acumulado. |
| `Quad`, `Rect`, `Outline`, `Line` | Figuras básicas en píxeles de pantalla. |
| `Triangle`, `Gradient`, `Circle`, `RoundedRect`, `RoundedOutline`, `Shadow`, `Mix` | Triángulos con color por esquina, rectángulos en degradado vertical, círculos y anillos, rectángulos con esquinas redondeadas (con degradado opcional) y su borde, sombras suaves por capas y mezcla de dos colores. |

### `Graphics/RiverLayer.cs`
`RiverLayer`: los ríos como cauces continuos. Al crearse une los segmentos de cada tramo en un solo camino; `Draw(lote, cámara, modoTerreno)` dibuja cada tramo con bordes suavizados, una orilla más oscura, el agua, un reflejo claro en los anchos y, en el modo terreno, una vega verde a los lados de los grandes ríos. La anchura crece con el caudal y con el zoom; con el zoom alejado solo se ven los grandes ríos.

### `Graphics/Strokes.cs`
`Strokes`: bandas suavizadas a lo largo de un camino de puntos de pantalla, para los ríos y las flechas. `Along(...)` dibuja una banda del ancho que se pida en cada punto, sólida con un borde que se difumina o difuminada desde el centro; `Band(...)` es un tramo de ella.

### `Graphics/PathArrow.cs`
`PathArrow.Draw(lote, puntos, color, tiempo, ancho, opacidad)`: flecha de ruta como en Hearts of Iron: una curva gruesa (Catmull-Rom) por los puntos, con contorno oscuro, punta en el destino y marcas claras que avanzan hacia él.

### `Graphics/Icons.cs`
`Icons.Resource(lote, recurso, centro, tamaño)`: iconos de los recursos dibujados con figuras, sin imágenes: una espiga (comida), un tronco (madera), trozos de carbón, lingotes (hierro, cobre, aluminio), monedas (oro, plata), un cristal (silicio), una gota (petróleo) y un neumático (caucho), con el color de cada recurso en el mapa.

### `Graphics/MenuBackground.cs`
`MenuBackground`: el fondo de los menús: la Tierra real a media resolución, coloreada por altitud (azules por profundidad, llanuras verdes, colinas pardas, cumbres pálidas y hielo) y con relieve, que se desplaza despacio hacia el oeste bajo un velo oscuro. Se construye una vez en segundo plano y lo comparten todas las pantallas de menú; `IsReady` dice si ya se ve.

### `Graphics/Font.cs`
| Función | Qué hace |
| --- | --- |
| `Font(gl)` | Genera una textura con las letras en 4 tamaños: Fira Sans (normal y negrita), clara y con cifras regulares, para la interfaz y Cinzel, capitales romanas, para el tamaño de título (el nombre del juego y las naciones en el mapa); incluye acentos y ñ (Latin-1). |
| `Draw(...)` | Escribe texto. |
| `Measure(...)`, `LineHeight(...)` | Ancho de un texto y alto de línea. |
| `Wrap(...)` | Parte un texto en líneas que caben en un ancho. |

### `Graphics/GlObjects.cs`
| Elemento | Qué es |
| --- | --- |
| `Shader` | Compila y enlaza un programa de GPU; `Set(...)` le pasa valores. |
| `Texture` | Textura RGBA: creación, `Update` y `Bind`. |

### `Graphics/Screenshot.cs`
`Save(gl, ancho, alto, ruta)`: guarda lo que hay en pantalla como PNG (lo usa `--screenshot`).

### `Graphics/GlSupport.cs`
`Ensure()`: antes de abrir la ventana, prueba a crear una oculta con OpenGL 3.3 (sin él, crear la ventana del juego lo cierra de golpe). Si falla en Linux, activa `LIBGL_ALWAYS_SOFTWARE=1` (el renderizado por software de Mesa) y vuelve a probar; devuelve si se puede jugar.

### `UI/Ui.cs`
Interfaz propia de "modo inmediato": los botones se declaran en cada fotograma y devuelven si se han pulsado.

| Elemento | Qué es |
| --- | --- |
| `Rect` | Rectángulo con `Contains` e `Inset`. |
| `InputState` | Estado del ratón y del teclado en el fotograma. |
| `Theme` | Colores de la interfaz, con los degradados de paneles y botones (`PanelTop`/`PanelBottom`, `ButtonTop`/`ButtonBottom`, `HoverTop`…, `ActiveTop`…), el brillo superior (`Highlight`) y los radios de las esquinas (`PanelRadius`, `ButtonRadius`). `Theme.Mood(humor, normal)` colorea un humor: rojo si hay descontento, verde si está contento. |
| `Ui.Panel`, `Text`, `TextCentered`, `Button`, `Hover`, `Tooltip` | Piezas de la interfaz. Un panel lleva sombra, cuerpo en degradado, brillo arriba y borde redondeado (`radius`: 0 para la barra superior; `opaque` para las pantallas de la nación y la ayuda). Un botón tiene relieve, se ilumina bajo el ratón con borde dorado, es dorado si está activo, se hunde al pulsarlo y su texto lleva sombra. Los tooltips tienen sombra y una línea dorada arriba. |
| `TextField(área, texto, máximo)` | Caja de texto de una línea: añade lo tecleado en el fotograma (`InputState.Chars`) y borra con Retroceso. Solo admite caracteres que la fuente sabe dibujar. |
| `Ui.MouseOverUi`, `Block` | Si el ratón está sobre la interfaz (para no hacer clic en el mapa a través de un panel). |
| `BeginFrame`, `EndFrame` | Empezar el fotograma / dibujar el tooltip al final. |

### `UI/ChangelogView.cs`
`ChangelogView`: muestra `CHANGELOG.md` (incluido en el ejecutable) en un panel con desplazamiento. `Layout` convierte el Markdown en líneas con formato.

### `Screens/HelpView.cs`
`HelpView`: la ayuda durante la partida (F1, el botón «?» de la barra superior o el menú de pausa). Temas a la izquierda y el texto del elegido a la derecha, con desplazamiento: controles, primeros pasos, población y humor, economía y recursos, ciudades, ciencia, ejército, flotas y mar, diplomacia, mapa y partida. **Aquí se escribe la ayuda.** Las cifras salen de `GameRules`, así que siguen los cambios de equilibrio; en el texto, «## » empieza un título y «- » una viñeta, y solo caben caracteres Latin-1 (nada de «…» ni «−»). `Layout` ajusta el tema al ancho; `AllText` da todo el texto para los tests.

### `Assets/`
Fuentes Fira Sans (normal y negrita) y Cinzel (variable; se usa su peso por defecto), de Google Fonts, con sus licencias `OFL-FiraSans.txt` y `OFL-Cinzel.txt`.

---

## 4. `tools/Conquer.EarthData/Program.cs`
Genera `src/Conquer.Game/Assets/earth.gz` a partir del relieve y la batimetría de la NASA (GEBCO) y de
las costas, lagos y glaciares de Natural Earth.

| Función | Qué hace |
| --- | --- |
| Programa principal | Combina las fuentes y escribe el fichero. |
| `Load`, `Downsample` | Leen las imágenes de 21600×10800 y las reducen a 3600×1800. |
| `Rasterize`, `FillPolygon` | Convierten los polígonos GeoJSON en máscaras de píxeles. |

Uso: ver el README.

---

## 5. `tests/Conquer.Tests`

| Fichero | Qué comprueba |
| --- | --- |
| `WorldGenerationTests.cs` | Ambos mapas salen con unas 25.000 provincias y todos los píxeles asignados. |
| `PeakTests.cs` | En la Tierra del generador 2, cada cordillera por encima de 5.000 m (el Tíbet y el Himalaya, los Andes…) es una sola provincia de cumbres y la Antártida sigue siendo hielo; las cumbres no se reclaman pero las tropas las cruzan; la partida guarda la versión del generador; los mapas del generador 1 no tienen cumbres. |
| `GameplayTests.cs` | Inicio sin territorio y con los recursos correctos; fundar la capital; océanos y polos no reclamables; las unidades terrestres no entran al mar pero sí cruzan hielo; nada cruza el mar; provincias mayores en desiertos, polos y océanos; solo las unidades militares reclaman; velocidad de 10 km/h; migración diaria; migración forzada con su coste; consumo de comida; la capital gana humor y fertilidad; el hambre los hunde; los migrantes forzados llegan descontentos; las provincias descontentas no pagan impuestos; la fertilidad acelera el crecimiento; las reservas de comida alegran; las fiestas cuestan oro y duran un mes; las estadísticas de la nación suman bien; cada yacimiento es una bolsa finita que empieza llena; muchas provincias tienen yacimientos y algunas varios; las bolsas se agotan y dejan de producir; las ciudades producen ciencia que descubre avances; los niveles se abren con un avance del anterior; elegir otro avance de la rama sustituye al que investigaba; la ciencia se reparte según la prioridad entre las ramas que investigan algo; sin nada elegido la ciencia se guarda y entra en el siguiente avance elegido; los vecinos que conocen un avance lo abaratan; los avances mejoran la economía; los edificios cuestan y tardan, tienen sus requisitos y mejoran su provincia; al principio solo se conocen los recursos antiguos y los avances revelan los demás; los recursos desconocidos no se explotan; la IA se expande e investiga. `WorldFixture` genera un único mundo para todos. |
| `MilitaryTests.cs` | Instrucción de batallones (hombres, recursos y días); batallones que piden su avance (o sus dos avances); legionarios, catapultas y catafractos piden sus avances de la era Clásica; unir (hasta 12), separar y velocidad del batallón más lento; no se entra en tierras ajenas sin guerra; ocupar tierra enemiga sin defensa; un ataque fuerte gana y uno débil se rompe; defensores rodeados destruidos; la paz devuelve lo ocupado; la IA solo acepta la paz pasado un tiempo; desgaste sin suministro; recuperación y refuerzos desde la capital; bonificación de mando en cadena y alcance; las unidades se llaman por su tamaño y conservan su número; un cuerpo manda 5 unidades como mucho; el mantenimiento diario y el ejército sin pagar; cada cuartel tiene un general de su rango que manda a las unidades a su alcance; los oficiales ganan estrellas con victorias y ayudan por sus virtudes; las batallas dan experiencia y victorias al general; los reclutas diluyen la experiencia; solo combate lo que cabe en el frente, con la artillería detrás; las armas combinadas; generales y experiencia se guardan, los generales de partidas antiguas pasan a ser oficiales y los cuarteles sin general reciben uno al cargar; cada nación empieza con una plantilla de dos guerreros; las plantillas se editan dentro de sus límites; una plantilla entrena un regimiento entero a la vez. |
| `OfficerTests.cs` | Reclutar oficiales cuesta oro y llena la reserva; ningún defecto anula una virtud; asignar, relevar y retirar pasan por la reserva; los oficiales ascienden al crecer su unidad y no bajan; al unir, la unidad conserva su oficial o toma el de la otra; varios batallones se separan juntos y sin oficial; renombrar y volver al nombre automático; los defectos estorban y las virtudes ayudan (ataque, velocidad, mantenimiento); licenciar devuelve el oficial a la reserva; oficiales, reserva y nombres se guardan. |
| `CityTests.cs` | Solo granja, granero, aserradero, mina, calzada y ferrocarril van sin ciudad; los habitantes construyen una ciudad con el nombre que eligen y la provincia conserva el suyo; hacen falta 500 habitantes, sitio y un nombre libre; el granero salva a la mitad de los que morirían de hambre; una ciudad en obras se guarda y se termina tras cargar; las provincias empiezan sin nombre, reciben uno distinto al reclamarlas y lo guardan con la partida. |
| `NavalTests.cs` | El puerto pide costa y Navegación a vela; sin puerto no se construyen barcos ni atracan flotas; el dique seco pide puerto, permite los barcos avanzados y repara el doble de rápido. Las tropas necesitan barco para cruzar el mar (los aviones no); los barcos se construyen en puertos y forman flotas; las flotas navegan hasta donde sabe su nación; los transportes llevan tropas y las desembarcan en otra costa; clic derecho sobre una flota embarca; los barcos de guerra hunden una flota enemiga con su carga; unir flotas conserva la carga; las flotas y la carga se guardan; los barcos van más rápido que a pie. |
| `EraTests.cs` | Cada era cuesta más hasta adoptar su institución y se abre al conocer la anterior; el Feudalismo nace en la capital de la primera nación con 8 ciudades el Humanismo en la mayor ciudad con universidad y la Industrialización donde una fábrica trabaja carbón; la Electrificación en la capital de la primera nación con Electricidad; Química revela el caucho y el ferrocarril dobla la velocidad; los avances modernos revelan los últimos recursos y todos los recursos se pueden descubrir; los edificios y batallones de cada era piden su avance; el castillo dobla el daño de los defensores. |
| `ClassicalMechanicsTests.cs` | La muralla hace que los defensores peguen más; las calzadas acortan la marcha (y la ruta más rápida las usa); Construcción acelera las obras; Administración reduce a la mitad el humor perdido por la distancia. |
| `InstitutionTests.cs` | Los avances de la era Clásica cuestan más sin Urbanismo y sus niveles siguen a los de la Antigüedad; Construcción permite el anfiteatro. El Urbanismo nace en la primera ciudad grande; se extiende solo a provincias asentadas; una nación lo adopta cuando lo tiene la mitad de su población y gana su bonus; se puede adoptar antes pagando oro; los avances de una era cuestan más hasta adoptar su institución; las instituciones se guardan con la partida. |
| `DifficultyTests.cs` | En Muy difícil hay menos yacimientos, los mismos de la primera tirada y con la mitad de bolsa; el humano empieza con los recursos de su dificultad y los rivales con los normales; los rivales producen más ciencia en dificultades altas; la dificultad se guarda con la partida. `VeryHardWorldFixture` genera el mismo mundo en Muy difícil. |
| `SaveGameTests.cs` | Cargar una partida y volver a guardarla da exactamente el mismo fichero; la partida cargada sigue jugándose; los mismos ajustes generan el mismo mapa; las partidas anteriores a la 1.13.0 reciben llenos los yacimientos nuevos; el mapa de los tests conserva su huella y cargan las partidas guardadas con la 1.30.1 y con la 1.31.0/1.32.0; se rechazan las partidas de otro mapa y las dañadas. |
| `InterfaceTests.cs` | Los nombres se ordenan alfabéticamente en español (sin tildes, ñ tras n); el texto de la ayuda solo usa caracteres que la fuente sabe dibujar (Latin-1). |
| `ReleaseTests.cs` | La versión del `.csproj` coincide con la primera entrada del `CHANGELOG.md`. |

---

## 6. Otros ficheros

| Fichero | Qué es |
| --- | --- |
| `CHANGELOG.md` | Historial de versiones para el jugador (también se ve en el juego). |
| `README.md` | Cómo ejecutar, controles, estructura y licencias de los datos. |
| `docs/GUIA.md` | Esta guía. |
| `nuget.config` | Usa solo nuget.org como fuente de paquetes. |
| `.github/workflows/build.yml` | En cada push a `dev` o `main` y en cada pull request, pasa las pruebas en Linux, Windows y macOS y publica el juego autocontenido (un solo ejecutable) para `win-x64`, `linux-x64`, `osx-x64` y `osx-arm64`; se descarga desde la pestaña Actions de GitHub. |
| `.gitignore`, `.gitattributes` | Ficheros que git ignora y ficheros binarios. |
