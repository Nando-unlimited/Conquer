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
2. `ConquerApp` abre la ventana y muestra `MainMenuScreen`.
3. Al pulsar "Comenzar", `LoadingScreen` genera en segundo plano el mundo (`WorldGenerator.Generate`),
   prepara los píxeles del mapa (`MapRenderer.Prepare`) y crea la partida (`GameSession.Create`).
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
| `StartingCitizens/Food/Gold/Wood` | Recursos iniciales: 300 colonos, 600 de comida, 50 de oro, 100 de madera. |
| `CitizenSpeedKmh` | 10 km/h: velocidad de ciudadanos, migrantes y unidades en terreno llano. |
| `FoodPerCitizen` | Comida que come cada ciudadano al día (0,1). |
| `FoodPerWorker` | Comida que produce cada trabajador en tierra de rendimiento 1 (0,13). |
| `GrowthRate`, `CityGrowthMultiplier` | Crecimiento diario de la población; las ciudades crecen el doble. |
| `StarvationRate` | Población que muere al día si no hay comida. |
| `CityCapacityMultiplier` | Una ciudad alimenta 2,5 veces más gente que la tierra sola. |
| `TaxGoldPerCitizen` | Oro por habitante y día. |
| `DepositFullWorkers` | Habitantes necesarios para que un yacimiento rinda al máximo. |
| `DepositSizeMultiplier` | Multiplica el tamaño de todas las bolsas de recurso al empezar la partida (1 por ahora; lo cambiarán los niveles de dificultad). |
| `ScienceBasePerCity`, `SciencePerCityCitizen` | Ciencia diaria de cada ciudad: 0,5 fijos más 0,001 por habitante (antes de humor y avances). |
| `MinRiverFlow`, `GreatRiverFlow` | Agua que ha de reunir un tramo para ser río (60) y para ser gran río (300); solo los grandes ríos cambian el juego. |
| `RiverFertility` | La tierra de un gran río da un 25 % más de comida y de capacidad. |
| `OvercrowdedFoodShare` | Lo que rinden los trabajadores que superan la capacidad de la tierra. |
| `DailyEmigrationShare`, `MinEmigrationCityPopulation` | Parte de una ciudad que emigra cada día, y población por debajo de la cual deja de enviar gente. |
| `SettledPopulation` | Habitantes con los que una provincia se considera asentada. |
| `MigrationTargetShare` | Las provincias atraen migrantes hasta llenar esta parte de su capacidad. |
| `MinCityPopulation` | Población mínima que debe quedar en una ciudad al reclutar o migrar. |
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
| `SettlersCost` | Lo que cuesta enviar colonos (además de sus 300 ciudadanos). |
| `UnitType` | Tipos de unidad: `Settlers` (colonos), `Regiment` (regimiento) y `Headquarters` (cuartel general). |

### `Rules/MilitaryRules.cs`
Cifras del ejército. El combate se mide por hora; el resto, por día.

| Elemento | Qué es |
| --- | --- |
| `MaxBattalionsPerRegiment` | Un regimiento tiene como mucho 6 batallones. |
| `HeadquartersSpeed` | Los cuarteles generales marchan a 1,5 veces el paso de un ciudadano. |
| `OrganisationDamage`, `StrengthDamage` | Organización y hombres que pierde un bando por cada punto de fuego enemigo, repartidos entre sus batallones. |
| `BreakingOrganisation` | Un regimiento se rompe por debajo del 10 % de su organización: el defensor se retira y el atacante abandona. |
| `CombatRandomness` | El fuego de cada bando varía un ±20 % cada hora. |
| `MountedRoughTerrainAttack`, `TerrainDefense(bioma)`, `IsRough(bioma)` | La caballería ataca a la mitad en terreno difícil; el defensor dispara ×1,25 en colinas, ×1,5 en montañas y ×1,2 en bosques y pantanos. |
| `RiverDefense`, `DefenseMultiplier(provincia)` | Con un gran río el defensor dispara ×1,25 más (el atacante tiene que cruzarlo); se multiplica por el del terreno. |
| `CommandBonus`, `HigherCommandBonus` | +10 % en combate y recuperación con el cuartel propio a su alcance, y +5 % por cada nivel superior enlazado. |
| `SupplyRangeHours` | El suministro llega hasta 15 días de marcha desde una ciudad, por tierra propia o libre. |
| `OutOfSupplyEfficiency`, `OutOfSupplyOrganisationLoss`, `OutOfSupplyAttrition` | Sin suministro se lucha al 75 % y se pierde cada día un 5 % de organización y un 1 % de hombres. |
| `OrganisationRecovery`, `ReinforcementRate` | Con suministro y fuera de combate se recupera un 20 % de organización al día y un 5 % de hombres, que salen de la capital. |
| `OccupiedMood` | −30 de humor en una provincia ocupada por el enemigo. |

### `Military/Battalions.cs`
Los batallones que se entrenan en las ciudades, la tropa especializada de la que se forman los regimientos. **Aquí se añaden y equilibran las tropas.**

| Elemento | Qué es |
| --- | --- |
| `BattalionType` | Guerreros, Arqueros, Lanceros de bronce, Jinetes, Carros de guerra, Carros de arqueros e Infantería de hierro. |
| `BattalionInfo` | Nombre, símbolo, hombres, coste, días de instrucción, avances que requiere (todos: los carros de arqueros piden La rueda y Tiro con arco), ataque, defensa, organización máxima, velocidad y si es montado. |
| `Battalions.All`, `Battalions.Info(tipo)` | Todos los batallones y la ficha de cada uno. |
| `Battalion` | Un batallón de un regimiento: sus hombres (`Strength`) y su organización, y ambos como parte de su máximo. |

### `Military/Formations.cs`
Las formaciones y sus nombres en cada época: batallón → regimiento (la unidad mínima que lucha) → brigada → división → cuerpo → ejército → grupo de ejércitos. Con nombres romanos hasta que un avance moderniza el ejército.

| Elemento | Qué es |
| --- | --- |
| `ArmyEra` | Época de los nombres: `Classical` (romanos) o `Modern`. |
| `ModernisedBy`, `EraOf(avances)` | El avance que trae los nombres modernos (todavía ninguno) y la época de una nación según sus avances. |
| `LevelName`, `LevelPlural` | Nombre de cada nivel: Legión, Vexilación, Ejército consular, Ejército provincial, Ejército de campaña y Prefectura; o Regimiento, Brigada, División, Cuerpo, Ejército y Grupo de ejércitos. |
| `BattalionWord`, `BattalionPlural`, `BattalionCount`, `BattalionName` | El batallón: cohorte (ala si es montado) o batallón; «Cohorte de arqueros», «3 cohortes». |
| `UnitName(nivel, número, época)`, `Roman(n)` | «Legión III», «Vexilación I»; en época moderna «3.er Regimiento», «1.ª Brigada», «II Cuerpo». |

### `Military/Templates.cs`
`RegimentTemplate`: diseño de regimiento como en Hearts of Iron: qué batallones lleva (1 a 6). Las ciudades entrenan regimientos enteros a partir de él. `Name` («Plantilla II»), `Men`, `Cost` (la suma de sus batallones), `TrainingDays` (el del batallón más lento, porque se instruyen a la vez), `Requires`, `Attack`, `Defense`, `MaxOrganisation`, `Speed`, `AnyMounted` y `Composition` («2 × Guerreros, 1 × Arqueros») lo resumen.

### `Military/Command.cs`
La cadena de mando, la instrucción y las batallas.

| Elemento | Qué es |
| --- | --- |
| `CommandLevelInfo`, `CommandLevels` | Los cinco niveles de cuartel general como en HOI3: brigada, división, cuerpo, ejército y grupo de ejércitos, con su alcance (150 a 2.500 km), subordinados (4 o 5), personal, coste y días. El nivel 0 es el regimiento. Los nombres salen de `Formations`. |
| `TrainingOrder` | Lo que entrena una ciudad: un batallón, un regimiento entero de una plantilla (`TemplateName`, `TemplateBattalions`) o un cuartel general, con los días que le quedan; `Name(época)` lo nombra («Cohorte de arqueros»). |
| `Battle` | Una batalla por una provincia: quién ataca, quién defiende, los regimientos atacantes (que esperan en sus provincias) y cuándo empezó. |

### `Rules/Modifiers.cs`
`Modifiers`: mejoras sobre las reglas normales. Los avances las aplican a toda la nación y los edificios a su provincia; todas se suman con `+`. Campos: parte extra de comida, madera, yacimientos, impuestos, ciencia, capacidad de la tierra y fertilidad; puntos de humor; parte de las muertes por hambre que se evita. `Modifiers.None` es «sin mejoras».

### `Buildings/Building.cs`
Los edificios que se construyen en las provincias. **Aquí se añaden y equilibran los edificios.**

| Elemento | Qué es |
| --- | --- |
| `BuildingType` | Granja, Aserradero, Mina, Templo, Biblioteca, Mercado, Acueducto y Herbolario. |
| `BuildingInfo` | Nombre, descripción, coste (madera y oro), días de obra, avance que requiere, si solo va en ciudades, si necesita un yacimiento sin agotar y sus efectos (`Modifiers`) en la provincia. |
| `Buildings.All`, `Buildings.Info(tipo)` | Todos los edificios y la ficha de cada uno. |

### `Science/Technology.cs`
Los avances que se pueden investigar. **Aquí se añaden y equilibran los avances.**

| Elemento | Qué es |
| --- | --- |
| `Tech` | Los avances: Agricultura, Carpintería, Minería, Trabajo del bronce, Trabajo del hierro, Doma del caballo, La rueda, Tiro con arco, Escritura, Mitología, Irrigación, Medicina y Moneda. |
| `TechInfo.Effects` | Lo que mejora el avance en toda la nación, como `Modifiers`. |
| `TechInfo` | Nombre, coste en puntos de ciencia, requisitos, descripción, efectos y recursos que revela (`Reveals`: Minería el carbón, Trabajo del hierro el hierro). |
| `Techs.All`, `Techs.Info(avance)` | Todos los avances y la ficha de cada uno. |

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
| `Player` | Jugador: id, nombre, color, si es humano, almacén, provincias que posee, capital, balance del último día (`LastDayNet`), si pasa hambre y cuántos días duraría su comida (`FoodReserveDays`).; avances conocidos (`Techs`) y la suma de sus efectos (`Bonuses`), avance en investigación (`Researching`), época de su ejército (`ArmyEra`), puntos puestos en cada avance (`ResearchProgress`), ciencia guardada sin investigación (`SpareScience`) y ciencia del último día. recursos que conoce (`KnownResources`, `Knows(recurso)`); `Learn(avance)` añade un avance y sus efectos y revela sus recursos; sus plantillas de regimiento (`Templates`) |
| `City` | Ciudad: id, nombre, dueño, provincia, fecha de fundación y hasta cuándo dura su fiesta (`FestivalUntilHours`, `HasFestival(ahora)`).; lo que está entrenando (`Training`). |
| `Unit` | Unidad en el mapa: colonos, regimiento o cuartel general. Tipo, dueño (`Owner`), provincia, número, nombre (según el nivel, el número y la época de su nación), batallones (regimiento), nivel (cuartel), cuartel del que depende (`CommanderId`), provincia que ataca (`AttackingProvinceId`) y ruta pendiente (`Path`). `Citizens` son los colonos, el personal o los hombres de sus brigadas; `Speed`, la de su brigada más lenta; `OrganisationShare`/`StrengthShare`, su estado; `CommandLevel` y `Symbol`, para la cadena de mando y la ficha. `HoursToNext`/`StepHours` miden el tramo actual; `StepProgress` da el avance (0..1) para dibujarla entre provincias. |
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
| `Create(map, jugadores, semilla, rivales)` | Nueva partida (los tests pueden quitar los rivales): limpia las provincias (dueño, controlador, población, ciudad, humor, fertilidad, edificios y bolsas de recurso llenas según `DepositSizeMultiplier`), crea jugadores con sus recursos iniciales, una plantilla de dos cohortes de guerreros y una unidad de colonos cada uno en sitios fértiles y alejados, y crea las IA. |
| `UnitById`, `CityById`, `CityIn(provincia)` | Búsquedas. |
| `CapacityOf(provincia)` | Habitantes que puede alimentar una provincia, contando la bonificación de ciudad y los avances de su dueño (Irrigación) y sus edificios (Acueducto). |
| `BonusesOf(provincia)` | Mejoras que se aplican a una provincia: las de los avances de su dueño más las de sus edificios. |
| `MoodFactors(provincia)` | Lista de (causa, puntos) que forman el humor objetivo: base, ciudad, capital o distancia a ella, fiestas, reservas de comida, hacinamiento, hambre, ocupación enemiga, avances que dan humor (Mitología) y edificios que dan humor (Templo). |
| `TargetMood(provincia)` | Suma de esos factores, entre 0 y 100. |
| `Stats(jugador)` | Totales de la nación (`NationStats`): población asentada, en unidades y migrando; provincias, ciudades y unidades; humor y fertilidad medios ponderados por habitantes, habitantes en cada nivel de humor y lo que queda en los yacimientos de sus provincias (`Reserves`). |
| `Step()` | Avanza una hora: mueve unidades, resuelve las batallas, hace llegar migrantes; a medianoche economía, migración, ciencia, obras y ejército; cada 6 h piensan las IA. |
| `ArriveMigrations()` | Suma los migrantes que llegan a su destino (si el destino se perdió o está ocupado, van a la capital), mezclando su humor. |
| `DailyEconomy(jugador)` | Las provincias ocupadas no producen ni comen para su dueño. Producción del día (comida, madera, oro, yacimientos) multiplicada por el humor, los avances y los edificios de cada provincia; los yacimientos sacan de su bolsa hasta agotarla (`Extract`), solo los de recursos que el jugador conoce; sin impuestos en provincias descontentas; consumo de comida, hambre, días de reserva de comida, humor y fertilidad, y crecimiento de la población (proporcional a la fertilidad). |
| `Extract(provincia, recurso, cantidad)` | Saca de la bolsa de un yacimiento lo que se pide o lo que queda, y avisa al jugador cuando se agota. |
| `UpdateMoodAndFertility(provincia, dueño, hambre)` | Acerca el humor a su objetivo y la fertilidad a la que marca el humor; avisa cuando una ciudad del jugador entra o sale del descontento. |
| `TargetFertility(provincia, dueño, hambre)` | Fertilidad hacia la que tiende una provincia, con los avances de su dueño (Medicina) y sus edificios (Herbolario). |
| `SciencePerDay(jugador)` | Puntos de ciencia al día de sus ciudades, por su humor, sus avances (Escritura) y sus edificios (Biblioteca). |
| `DailyScience(jugador)` | Suma la ciencia del día al avance en investigación (o la guarda si no hay ninguno); al completarlo lo aprende, guarda lo sobrante y avisa. |
| `CanResearch(jugador, avance)` / `Research(...)` | Comprueba (no conocido y con sus requisitos) / pone la ciencia del país en un avance; la ciencia guardada entra en él de inmediato. |
| `IsBuildingAvailable(provincia, tipo)` | ¿Podría construirse aquí algún día? Tiene dueño, se conoce su avance y hay ciudad o yacimiento conocido si los necesita (sin mirar coste ni obras). |
| `CanBuild(jugador, provincia, tipo)` / `Build(...)` | Comprueba (es tuya, no está construido, está disponible, no hay otra obra, tiene al menos 10 habitantes y puedes pagarlo) / paga y empieza la obra. |
| `DailyConstruction(jugador)` | Cada obra avanza un día; al terminar, el edificio empieza a funcionar y avisa al jugador. |
| `DailyMigration(jugador)` | Cada ciudad no ocupada envía parte de su gente a las provincias propias poco pobladas; primero las vacías y las cercanas. Guarda las fracciones de persona para el día siguiente. |
| `CanFoundCity(unidad)` / `FoundCity(...)` | Comprueba / funda una ciudad con colonos: reclama la provincia, crea la ciudad (capital si es la primera) y los colonos pasan a ser su población. |
| `CanClaim(unidad)` / `Claim(...)` | Comprueba / reclama con una unidad militar la provincia libre en la que está. |
| `CanRecruitSettlers(ciudad)` / `RecruitSettlers(...)` | Comprueba / envía colonos desde una ciudad, pagando recursos y habitantes. |
| `Disband(...)` | La unidad se disuelve: sus ciudadanos pasan a vivir en la provincia (propia) donde está. |
| `CanHoldFestival(ciudad)` / `HoldFestival(...)` | Comprueba / paga unas fiestas que suben el humor de la ciudad durante 30 días (una a la vez). |
| `CanForceMigration(...)` / `ForceMigration(...)` | Comprueba / envía un número elegido de ciudadanos entre dos provincias propias pagando oro; viajan con el humor de su origen menos 20. |
| `Settle(provincia, personas, humor)` | Añade gente a una provincia mezclando su humor con el de los residentes según cuántos son (migrantes, colonos al fundar, unidades que se asientan). |
| `SetOwner(provincia, jugador)` | Cambia el dueño (y el controlador) de una provincia y avisa al cliente (`OwnershipChanged`). |
| `AddUnit`, `RemoveUnit` | Crean y quitan unidades (`AddUnit` también se usa en los tests); al quitar una, sus subordinados pierden el cuartel y sale de las batallas. |
| `Notify(jugador, texto)` | Añade una notificación. |
| `FormatHours(h)` | "5 h", "2 d 3 h". |
| `PickStartProvinces(n)` | Elige posiciones iniciales fértiles, lo más separadas posible y en masas de tierra de al menos 200 provincias. |
| `LandmassSizes()` | Tamaño (en provincias) de la masa de tierra conectada de cada provincia. |

### `Simulation/GameSession.Military.cs`
El ejército dentro de la partida.

| Función | Qué hace |
| --- | --- |
| `Battles`, `BattleIn(provincia)` | Las batallas en curso y la de una provincia. |
| `AddRegiment(...)`, `AddHeadquarters(...)`, `NextUnitNumber(...)` | Crean regimientos y cuarteles con el siguiente número de su nivel (el nombre sale de él: «Legión III»); también se usan en los tests. |
| `CommanderOf(unidad)`, `SubordinatesOf(cuartel)` | Cadena de mando hacia arriba y hacia abajo. |
| `RegimentPower(unidad)`, `MilitaryPower(jugador)` | Valor aproximado de combate de un regimiento y de todo un ejército. |
| `EnemyRegimentsIn(provincia, jugador)` | Regimientos de naciones en guerra con el jugador en una provincia. |
| `CanUnitEnter(unidad, provincia)` | Tierra libre y propia siempre; la de otra nación solo para regimientos en guerra con ella. |
| `MoveUnit(...)`, `UnitStepHours(...)` | Orden de mover (la ruta evita tierras vedadas; el tiempo depende de la velocidad de la unidad). |
| `MoveUnits()` | Cada hora cada unidad avanza; un regimiento que entra donde hay tropas enemigas ataca, y si no las hay la ocupa (`EnterProvince`). |
| `Occupy(provincia, jugador)` | La provincia pasa a manos del jugador (o vuelve a su dueño) y los civiles y cuarteles enemigos huyen. |
| `CanTrain`/`Train`, `CanRaiseHeadquarters`/`RaiseHeadquarters`, `CanRaiseTroops(...)` | Pagan y ponen en instrucción un batallón o un cuartel; los hombres salen de la ciudad. `CanRaiseTroops` comprueba avances, ocupación, habitantes y coste. |
| `TemplateById`, `AddTemplate(...)` | Busca una plantilla del jugador y crea una nueva con el siguiente número (también la usan la IA y los tests). |
| `CreateTemplate`, `DuplicateTemplate`, `DeleteTemplate` | Plantilla nueva (con una cohorte de guerreros), copia de otra, o borrarla (siempre queda al menos una). |
| `CanAddToTemplate`/`AddToTemplate`, `RemoveFromTemplate` | Añaden un batallón conocido (hasta 6) o quitan uno (queda al menos uno). |
| `CanTrainTemplate`/`TrainTemplate(...)` | Pagan todos los batallones de una plantilla a la vez; se instruyen juntos y forman un solo regimiento. |
| `DailyTraining(jugador)` | Las órdenes de instrucción avanzan; al terminar aparece el regimiento (con ese batallón o con los de la plantilla) o el cuartel en la ciudad. |
| `CanMerge`/`Merge`, `Split` | Unen dos regimientos de la misma provincia (hasta 6 batallones) o separan un batallón en un regimiento nuevo. |
| `CanAttach`/`Attach`, `Detach` | Ponen una unidad bajo el mando de un cuartel del nivel superior (5 como mucho) o la quitan. |
| `InCommandRange(unidad)`, `CommandBonus(unidad)` | Si su cuartel la alcanza, y la bonificación de toda la cadena enlazada. |
| `ComputeSupply(jugador)`, `IsInSupply(unidad)`, `IsSupplied(jugador, provincia)` | Provincias abastecidas: hasta 15 días desde sus ciudades por tierra propia o libre, y una más allá (el frente). |
| `InBattle(unidad)` | Si ataca o defiende. |
| `DailyMilitary(jugador)` | Cada día: instrucción, suministro, recuperación de organización, refuerzos desde la capital y desgaste sin suministro (el regimiento que se queda sin hombres se dispersa). |
| `StartAttack(...)`, `CancelAttack(...)` | El regimiento se detiene en la frontera y ataca (se une a la batalla o la abre), o la abandona. |
| `ResolveBattles()` | Una hora de cada batalla: fuego de ambos bandos, daño, retiradas y abandonos; si no quedan defensores, los atacantes entran. |
| `Fire(...)`, `Damage(...)`, `Broken(...)` | Fuego de un regimiento (ataque o defensa, hombres, organización, mando, suministro, terreno y azar) y su reparto como daño. |
| `EndBattle(...)`, `Retreat(...)`, `Destroy(...)` | Final de la batalla y avisos; retirada a una provincia vecina sin enemigos (o destrucción si está rodeada). |

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
| `CanEnter(provincia)` | ¿Puede entrar un viajero terrestre? (no, si es agua). |
| `StepHours(a, b)` | Horas para ir de una provincia a su vecina: distancia entre centros / (10 km/h × facilidad del terreno). |
| `FindPath(desde, hasta, puedeEntrar)` | Ruta más rápida (A*) y su duración, o `null` si no hay camino por tierra; `puedeEntrar` limita las provincias (tierras de otras naciones para un ejército). |
| `FromSources(orígenes, maxHoras, destinos, puedeEntrar)` | Horas desde el origen más cercano a cada provincia (Dijkstra) y cuál es ese origen. Se usa para la migración y para que la IA busque sitio. Puede parar al alcanzar todos los destinos. |
| `Heuristic` | Estimación para A*: distancia en línea recta a 10 km/h. |

### `Simulation/Names.cs`
| Elemento | Qué es |
| --- | --- |
| `PlayerNames.Pick(n, random)` | Nombres de naciones al azar; `Colors` son sus colores. |
| `CityNames.Next(usados, random)` | Genera un nombre de ciudad por sílabas sin repetir. |

### `AI/AiPlayer.cs`
Rival controlado por el ordenador. Clase parcial: el ejército está en `AiPlayer.Military.cs`. Determinista (usa su propia semilla).

| Función | Qué hace |
| --- | --- |
| `Think(decisionesDiarias)` | Turno de la IA: clasifica regimientos nuevos, licencia soldados si hay hambre en paz, guía a colonos, reclamadores, soldados (en guerra) y cuarteles, y trae a casa las divisiones sin suministro; `PlayerId` identifica la nación; una vez al día, celebra fiestas, elige investigación, recluta y construye. |
| `HoldFestivals()` | Paga fiestas en las ciudades con humor por debajo de 45 si, tras pagarlas, le quedan 30 de oro para reclutar. |
| `ChooseResearch()`, `ResearchOrder` | Cuando no investiga nada, elige el primer avance disponible de su orden de preferencia (Agricultura, Escritura, Minería, Trabajo del hierro, Irrigación...). |
| `Construct()`, `BuildOrder`, `WorthBuilding(...)` | Elige su edificio más deseado (ciudades primero, luego por población y orden de preferencia) donde compense: al menos 200 habitantes, aserraderos en tierra con madera, templos donde hay inquietud, acueductos al 60 % de la capacidad. Lo empieza cuando puede pagarlo guardando madera y oro para reclutar; si no, ahorra. |
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
| `BuildArmy()`, `Spare(coste)` | Desde el día 180, hasta tener 2 batallones por ciudad (4 en guerra), entrena regimientos enteros de su plantilla, del tamaño que su ciudad puede dar (2 a 4 batallones), o si no el mejor batallón suelto; sin gastar la reserva. |
| `ArmyTemplate(tamaño)`, `CanSupply(tipo)` | Su plantilla: dos de su infantería más resistente, su tropa más ofensiva y otra de infantería, recortada al tamaño; solo con tropas cuyos materiales (cobre, hierro…) tiene o produce. |
| `OrganiseArmy()`, `RaiseAndAttach(...)`, `HighestHeadquarters` | Une regimientos pequeños (hasta 4 batallones), forma cuarteles de brigada, división y cuerpo cuando hacen falta y asigna a todos. |
| `FollowTroops(cuartel)` | El cuartel va adonde están sus unidades si alguna queda fuera de alcance. |
| `GuideSoldier(unidad)` | En guerra: acude a sus ciudades atacadas, ataca la provincia enemiga vecina más débil (si supera 1,3 veces su defensa) o marcha hacia tierra enemiga que su suministro alcance; descansa si está desorganizada. |
| `GoHomeIfCutOff(unidad)`, `IsEnemyLand(provincia)` | Un regimiento sin suministro vuelve a la capital. |
| `Diplomacy()`, `Neighbours()` | Tras dos años, a veces declara la guerra a un vecino con menos del 60 % de su poder; propone la paz a otros rivales cuando una guerra se alarga y va mal. |
| `WouldAcceptPeace(otro)`, `Winning(otro)` | Acepta la paz tras 60 días si no va ganando (o tras un año); va ganando si su ejército es mucho más fuerte y ocupa más de lo que ha perdido. |

### `World/Biome.cs`
| Elemento | Qué es |
| --- | --- |
| `Biome` | Los 17 biomas (océano profundo, océano, mar costero, lago, hielo polar, tundra, taiga, bosque templado, pradera, estepa, desierto, sabana, selva tropical, humedal, colinas, montañas, alta montaña). |
| `BiomeInfo` | Ficha de cada bioma: nombre, si es agua, si es habitable, densidad de provincias (valores bajos, provincias grandes), rendimiento de comida y madera, habitantes por km², facilidad de paso y color. |
| `Biomes.Info(bioma)` | Devuelve la ficha. **Aquí se equilibra cada tipo de terreno.** |

### `World/Province.cs`
`Province`: id, bioma dominante, centro (píxel y lat/lon), área en km², altitud media, vecinas,
yacimientos (`Deposits`: producción diaria; `DepositSizes`: tamaño de la bolsa; `Reserves`: lo que queda en la partida), dueño, población, ciudad, humor (`Mood`, 0-100) y fertilidad
(`Fertility`, multiplicador de nacimientos, 1 = normal). `ControllerId` es quién la tiene en la guerra (su dueño, o el enemigo que la ocupa) e `IsOccupied` si la ocupa otro. `IsWater`, `IsClaimable`, `IsOwned`,
`RiverFlow` (agua del mayor río que la cruza, 0 sin río), `HasRiver` (la cruza un gran río), `FoodYield` (rendimiento de comida de su bioma, más en un gran río),
`Capacity` (habitantes que alimenta su tierra, más en un gran río) y `HasDeposit(recurso)` (tiene ese yacimiento sin agotar) son atajos.
Edificios: los terminados (`Buildings`) y la suma de sus efectos (`BuildingBonuses`), el que está en obras (`Constructing`) y los días que le quedan (`ConstructionDaysLeft`). `AddBuilding(tipo)` añade uno terminado; `ClearBuildings()` los quita todos (nueva partida).

### `World/WorldMap.cs`
| Elemento | Qué es |
| --- | --- |
| `MapKind` | `Random` o `Earth`. |
| `RiverSegment` | Un tramo corto de río entre dos puntos del mapa, con el agua que lleva. |
| `WorldMap` | El mapa: tamaño, altitud, bioma e id de provincia por píxel, la lista de provincias y los ríos (`Rivers`). |
| `Latitude(y)`, `Longitude(x)`, `WrapX(x)` | Conversión de píxeles a grados y vuelta al mundo en X. |
| `ProvinceAt(x, y)` | Provincia en un píxel. |
| `PixelAreaKm2(fila)` | Área real de un píxel (menor cerca de los polos). |
| `DistanceKm(...)` | Distancia sobre la esfera entre dos puntos o provincias. |

### `World/WorldGenerator.cs`
| Elemento | Qué es |
| --- | --- |
| `WorldSettings` | Tipo de mapa, semilla y número objetivo de provincias (25.000). |
| `Generate(ajustes, progreso)` | Crea el mundo en cinco pasos: relieve → clima y biomas → provincias → recursos → ríos (cada provincia guarda el mayor que la cruza). Informa del paso en curso para la pantalla de carga. |

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
| `Assign(...)` | Bioma de cada píxel a partir de temperatura (latitud y altitud) y humedad (bandas de latitud y distancia al mar). |
| `LatitudeMoisture(lat)` | Humedad según la latitud: ecuador húmedo, subtrópicos secos, latitudes medias húmedas, polos secos. |
| `Slope(...)` | Desnivel con los vecinos (para detectar colinas). |
| `DistanceToOceanKm(...)` | Distancia de cada píxel al mar (los interiores de los continentes son más secos). |

### `World/Generation/ProvinceGenerator.cs`
Divide el mapa en provincias.

| Función | Qué hace |
| --- | --- |
| `Generate(...)` | Proceso completo: semillas → crecimiento → relajación → segundo crecimiento → huecos → provincias. |
| `Category(bioma)` | Mar, lago, hielo o tierra: una provincia nunca mezcla categorías. |
| `StepCosts(...)` | Coste con algo de ruido por píxel, para que las fronteras no sean rectas. |
| `PlaceSeeds(...)` | Reparte las semillas según la densidad del bioma y el área real (menos semillas en desiertos, polos y océanos, así que sus provincias son mayores). |
| `Grow(...)` | Cada semilla se extiende por su categoría (camino más corto con cola por cubos). |
| `Relax(...)` | Mueve cada semilla al centro de su región (paso de Lloyd) para formas más regulares. |
| `CircularMeanX(...)` | Media de X teniendo en cuenta que el mapa da la vuelta. |
| `FillLeftovers(...)` | Zonas sin semilla (islas, lagos aislados): las diminutas se unen a una vecina y el resto pasan a ser provincias propias. |
| `Neighbours4(...)`, `Edge(...)` | Ayudas. |
| `BuildProvinces(...)` | Crea los objetos `Province`: bioma dominante, área, altitud, centro y vecinas. |

### `World/Generation/ResourceGenerator.cs`
| Función | Qué hace |
| --- | --- |
| `Place(provincias, semilla)` | Reparte yacimientos en las provincias habitables, agrupados por regiones. Cada uno es una bolsa finita: producción diaria (`Deposits`) y tamaño total (`DepositSizes`) de 10 a 50 años de producción máxima, sorteado con su propio generador para que una semilla siga poniendo los mismos yacimientos. |
| `Chance(recurso, bioma, latitud)` | Probabilidad de cada recurso según el terreno (caucho en selvas tropicales, petróleo en desiertos, etc.). |
| `Richness(recurso)` | Producción típica diaria de un yacimiento. |

### `World/Generation/RiverGenerator.cs`
Traza los ríos a partir del relieve, en una rejilla de 2×2 píxeles.

| Función | Qué hace |
| --- | --- |
| `Trace(...)` | Inundación por prioridad desde mares y lagos: cada celda de tierra desagua en la vecina que llegó primero (las hondonadas se cruzan como un lago lleno). La lluvia baja por ese árbol y cada celda con agua suficiente es río. Un poco de ruido en la altitud hace que serpenteen por el llano. |
| `Smooth(...)` | Une las celdas en tramos (de una fuente o confluencia a la siguiente o al mar), los suaviza y los corta en `RiverSegment`. |
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
| `StartOptions.Parse(args)` | Opciones de línea de comandos para pruebas: `--new random|earth`, `--seed`, `--players`, `--days` (funda la capital y avanza N días), `--zoom`, `--mode terrain|political|population|mood|fertility|resources`, `--nation summary|cities|provinces|science|army|templates|diplomacy` (abre la pantalla de la nación), `--panel buildings|army|regiment` (una pestaña de la provincia seleccionada, o un regimiento de muestra) y `--screenshot fichero.png` (guarda una captura del juego y se cierra). |
| `QuickStart` | Partida que empieza directamente, sin menús. |

### `ConquerApp.cs`
| Función | Qué hace |
| --- | --- |
| `IScreen` | Interfaz de una pantalla: `Frame(dt)` actualiza y dibuja un fotograma. |
| `ConquerApp(options)` | Crea la ventana de 1600×900 con OpenGL 3.3. |
| `Run()`, `Quit()`, `Show(pantalla)` | Arranca, cierra, cambia de pantalla al acabar el fotograma. |
| `OnLoad()` | Inicia OpenGL, la fuente, la interfaz y los eventos de ratón y teclado; muestra el menú o la partida rápida. |
| `OnRender(dt)` | Cada fotograma: limpia, dibuja la pantalla actual, la interfaz y el tooltip. |
| `CaptureIfRequested()` | Con `--screenshot`, guarda la imagen tras unos fotogramas y cierra. |
| `ReadVersion()` | Lee la versión del ejecutable (la del `.csproj`). |

### `Screens/MenuScreens.cs`
| Elemento | Qué es |
| --- | --- |
| `MainMenuScreen` | Menú principal: tipo de mapa, semilla, número de jugadores, "Comenzar", historial de versiones y salir. |
| `LoadingScreen` | Genera el mundo en segundo plano y muestra el progreso; al terminar abre `GameScreen`. |

### `Screens/GameScreen.cs`
La pantalla de juego. Clase parcial: el ejército en pantalla está en `GameScreen.Army.cs`.

| Función | Qué hace |
| --- | --- |
| `GameScreen(...)` | Crea el renderizador y la cámara y centra la vista en tus colonos. |
| `ApplyTestOptions(...)`, `ShowSampleArmy(...)` | Aplican `--days`, `--zoom`, `--mode`, `--nation` y `--panel` (con `regiment`, entrena y selecciona un regimiento de muestra bajo una vexilación). |
| `Frame(dt)` | Fotograma: teclas, tiempo, refresco del mapa, dibujo del mapa, marcadores, paneles e interacción. |
| `AdvanceTime(dt)` | Convierte tiempo real en horas de juego según la velocidad (pausa, 1 h/s … 7 días/s). |
| `SetSpeed`, `HandleKeys` | Velocidad y teclado (Espacio, 1-5, Tab, Inicio, N, +/-, WASD, Esc). |
| `HandleMapMouse()` | Rueda para el zoom, arrastrar para mover el mapa, clics. |
| `LeftClick()` | Selecciona unidad o provincia, o elige el destino de una migración forzada. |
| `RightClick()` | Da orden de mover la unidad seleccionada (o de atacar, si el destino es enemigo). |
| `CenterOnHome()` | Centra la vista en la capital. |
| `ViewUnit(unidad)` | Selecciona una unidad y centra la vista en ella (desde la pantalla de la nación). |
| `NationRect`, `ViewProvince(provincia)` | Rectángulo de la pantalla de la nación, y seleccionar y centrar una provincia cuando se pide desde ella. |
| `Show(resultado)`, `CollectNotifications()` | Mensajes temporales en pantalla. |
| `Center`, `Between`, `OnScreen` | Posición de una provincia, punto intermedio entre dos (cruzando el borde del mapa por el lado corto) y si algo está en pantalla. |
| `DrawRivers()` | Ríos como líneas azules, más anchas cuanta más agua llevan; con el zoom alejado solo se ven los grandes. |
| `DrawCities()` | Marcadores de ciudad y sus nombres. |
| `DrawMigrations()` | Puntos que representan a los migrantes en camino. |
| `DrawPath()` | Ruta de la unidad seleccionada. |
| `DrawTopBar()`, `Compact()` | Barra superior: nación, población y humor medio, fecha, velocidades, recursos conocidos y botones Nación (con «!» si no se investiga nada) y Menú (con números abreviados: 12,3k, 2,9M). |
| `DrawSidePanel()`, `UnitPanel()`, `ProvincePanel()` | Panel derecho: datos y botones de la unidad o provincia seleccionada; el de provincia tiene pestañas General, Edificios y Ejército (humor y fertilidad, yacimientos con lo que les queda, fundar, fiestas, reclamar, asentarse, reclutar, migración forzada). |
| `MoodTooltip(provincia)` | Tooltip del humor con sus causas y su efecto en la producción. |
| `BuildingsPanel(...)` | Pestaña Edificios: la obra en curso con su barra, los edificios terminados y, en tus provincias, un botón por cada edificio que puedes levantar; los que solo necesitan ciudad o yacimiento dicen qué les falta, y los de avances sin descubrir no aparecen (`IsBuildingKnown`). |
| `Line()`, `Paragraph()` | Ayudas para escribir filas y párrafos en el panel. |
| `DrawBottomBar()` | Modos de mapa y ayuda de controles. |
| `DrawResourceFilter(barra)` | En el modo recursos, fila de botones para ver todos los yacimientos o solo uno de los recursos que conoces; hace de leyenda con el color de cada recurso. |
| `DrawMessages()`, `HoverTooltip()` | Mensajes (más arriba si está abierto el filtro de recursos) y tooltip de la provincia bajo el ratón (con sus yacimientos en el modo recursos). |
| `DrawPauseMenu()` | Menú de pausa (Esc). |

### `Screens/GameScreen.Army.cs`
El ejército en pantalla.

| Función | Qué hace |
| --- | --- |
| `ProvinceTab` | Pestañas del panel de provincia: General, Edificios y Ejército (esta solo en tus ciudades). |
| `DrawUnits()`, `Bar(...)` | Fichas estilo OTAN: aspa para infantería, barra para montados, marcas de nivel en los cuarteles y «C» para colonos, con barras de hombres (verde) y organización (ámbar). Dibuja la ruta, la línea a su cuartel (verde si está a su alcance) y una flecha roja al atacar. |
| `DrawBattles()`, `BattleSummary(...)` | Espadas cruzadas sobre cada batalla; al pasar el ratón, los dos bandos, su organización y el terreno. |
| `UnitPanel(...)`, `UnitState(...)` | Panel de la unidad: tipo, nación, ubicación, estado y botones (fundar, reclamar, licenciar, detener). |
| `RegimentDetails(...)` | Suministro, velocidad, mando, cada batallón con sus barras (y «Separar») y botones para unir otros regimientos de la provincia. |
| `HeadquartersDetails(...)`, `CommandLine(...)`, `AttachButtons(...)` | Alcance y subordinados de un cuartel, de quién depende la unidad y botones para asignarla a un cuartel cercano o quitarla. |
| `ArmyPanel(ciudad)` | Pestaña Ejército de una ciudad: regimientos que puedes entrenar de tus plantillas (las cuatro primeras), batallones que puedes entrenar (los de avances sin descubrir no aparecen), cuarteles generales y lo que está en instrucción, con los nombres de la época. |

### `Screens/NationView.Military.cs`
Pestañas Ejército, Plantillas y Diplomacia de la pantalla de la nación.

| Función | Qué hace |
| --- | --- |
| `Army(...)`, `UnitActivity(...)`, `Plural(...)` | Orden de batalla: cada cuartel con sus unidades en árbol y después los regimientos sin cuartel, con ubicación, hombres, organización, suministro, estado y «Ver». |
| `Templates(...)` | Diseñador de regimientos: tus plantillas a la izquierda (nueva, duplicar, borrar); a la derecha los batallones de la elegida (hasta 6, con «Quitar»), botones para añadir los que conoces y lo que cuesta y cómo lucha un regimiento de ese diseño. |
| `Diplomacy(...)` | Cada nación: paz o guerra (y desde cuándo), su poder militar frente al tuyo, provincias, lo tomado y perdido, y los botones de declarar la guerra o proponer la paz. |

### `Screens/NationView.cs`
Pantalla de la nación (botón «Nación» o tecla N). El tiempo sigue corriendo mientras está abierta.

| Elemento | Qué es |
| --- | --- |
| `NationTab` | Pestañas: `Summary` (Resumen), `Cities` (Ciudades), `Provinces` (Provincias), `Science` (Ciencia), `Army` (Ejército), `Templates` (Plantillas) y `Diplomacy` (Diplomacia). |
| `NationView(partida, jugador, verProvincia, verUnidad, mostrar)` | Recibe qué hacer al pulsar «Ver» (centrar el mapa en una provincia o una unidad) y cómo mostrar el resultado de las órdenes. |
| `Frame(ui, área)` | Dibuja el panel opaco con las pestañas y el botón Cerrar. |
| `Summary(...)` | Resumen: población (total, asentada, en unidades, migrando), fertilidad media, humor medio con su barra por niveles (`MoodBar`), territorio, ciencia por día e investigación actual, comida con sus días de reserva y el resto de recursos conocidos con su almacén, balance diario y lo que queda en sus bolsas. |
| `Cities(...)` | Tabla de ciudades: población / capacidad, humor, fertilidad, fiestas, enviar colonos o entrenar guerreros y «Ver». |
| `Provinces(...)` | Tabla de todas las provincias: población, humor, fertilidad, terreno, migrantes en camino y «Ver». |
| `ProvinceCells(...)` | Celdas de población, humor (con tooltip de causas) y fertilidad, comunes a las dos tablas. |
| `Header(...)`, `Sort(...)` | Cabeceras que ordenan al pulsarlas (nombre, población, humor, fertilidad; otra pulsación invierte el orden). |
| `Rows(...)` | Filas visibles con desplazamiento por la rueda del ratón y barra de desplazamiento. |
| `Science(...)`, `TechCard(...)`, `ProgressBar(...)` | Pestaña Ciencia: puntos al día (con su desglose), investigación actual con barra de progreso y tiempo estimado, y una tarjeta por avance con su estado, coste, efecto, requisitos, los edificios y batallones que permite y el botón Investigar (cinco tarjetas por fila). |
| `Heading`, `Row`, `ViewButton`, `ProvinceName`, `Compact` | Ayudas de dibujo y formato. |

### `Graphics/MapRenderer.cs`
Dibuja el mapa entero con un único shader.

| Elemento | Qué es |
| --- | --- |
| `MapMode` | Terreno, político, población, humor, fertilidad, recursos. |
| `ResourceFilter` | En el modo recursos, el único recurso que se muestra (o `null` para todos). |
| `IsResourceKnown` | Qué recursos conoce quien mira; los demás no se dibujan. |
| Shader de fragmentos | Para cada píxel de pantalla calcula el punto del mapa, busca la provincia en la textura de ids y la colorea según su dueño o su población. Con zoom alto mezcla las 4 celdas vecinas (`smoothRegions`, `strongest`) para trazar fronteras suaves; con zoom lejano compara con el píxel vecino. Resalta la provincia seleccionada y la que está bajo el ratón. |
| `Prepare(mapa)` | Prepara (fuera del hilo principal) los píxeles de ids y colores del terreno. |
| `ProvinceAt(mapa, punto, zoom)` | Provincia que se ve en un punto, con la misma regla que el shader (para los clics). |
| `SmoothZoom` | Zoom a partir del cual las fronteras se suavizan. |
| `Refresh(partida)` | Recalcula el color de cada provincia (según quién la controla: lo ocupado toma el color del ocupante dentro de las fronteras del dueño) y su dueño (texturas pequeñas de 256×128). |
| `PopulationColor(densidad)` | Escala de color del modo población. |
| `ScaleColor(valor)` | Rojo-amarillo-verde de 0 a 1, para los modos humor y fertilidad. |
| `DepositColor(provincia)` | Color del modo recursos: el del yacimiento principal que queda de los conocidos, o con filtro ese recurso más intenso cuanto más queda. Gris si no hay nada. |
| `ResourceColor(recurso)` | Color de cada recurso en el mapa y en la leyenda. |
| `Draw(cámara, ...)` | Pasa los parámetros al shader y dibuja. |

### `Graphics/TerrainColors.cs`
`Build(mapa)`: color de cada píxel según el bioma, sombreado del relieve en tierra y profundidad en el mar.

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

### `Graphics/Font.cs`
| Función | Qué hace |
| --- | --- |
| `Font(gl)` | Genera una textura con las letras de Noto Sans (normal y negrita) en 4 tamaños; incluye acentos y ñ. |
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
| `Theme` | Colores de la interfaz. `Theme.Mood(humor, normal)` colorea un humor: rojo si hay descontento, verde si está contento. |
| `Ui.Panel`, `Text`, `TextCentered`, `Button`, `Hover`, `Tooltip` | Piezas de la interfaz. |
| `Ui.MouseOverUi`, `Block` | Si el ratón está sobre la interfaz (para no hacer clic en el mapa a través de un panel). |
| `BeginFrame`, `EndFrame` | Empezar el fotograma / dibujar el tooltip al final. |

### `UI/ChangelogView.cs`
`ChangelogView`: muestra `CHANGELOG.md` (incluido en el ejecutable) en un panel con desplazamiento. `Layout` convierte el Markdown en líneas con formato.

### `Assets/`
Fuentes Noto Sans (normal y negrita) y su licencia `OFL.txt`.

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
| `GameplayTests.cs` | Inicio sin territorio y con los recursos correctos; fundar la capital; océanos y polos no reclamables; las unidades terrestres no entran al mar pero sí cruzan hielo; nada cruza el mar; provincias mayores en desiertos, polos y océanos; solo las unidades militares reclaman; velocidad de 10 km/h; migración diaria; migración forzada con su coste; consumo de comida; la capital gana humor y fertilidad; el hambre los hunde; los migrantes forzados llegan descontentos; las provincias descontentas no pagan impuestos; la fertilidad acelera el crecimiento; las reservas de comida alegran; las fiestas cuestan oro y duran un mes; las estadísticas de la nación suman bien; cada yacimiento es una bolsa finita que empieza llena; las bolsas se agotan y dejan de producir; las ciudades producen ciencia que descubre avances; los avances piden sus requisitos; la ciencia sin investigación se guarda; los avances mejoran la economía; los edificios cuestan y tardan, tienen sus requisitos y mejoran su provincia; al principio solo se conocen los recursos antiguos y los avances revelan los demás; los recursos desconocidos no se explotan; la IA se expande e investiga. `WorldFixture` genera un único mundo para todos. |
| `MilitaryTests.cs` | Instrucción de batallones (hombres, recursos y días); batallones que piden su avance (o sus dos avances); unir (hasta 6), separar y velocidad del batallón más lento; no se entra en tierras ajenas sin guerra; ocupar tierra enemiga sin defensa; un ataque fuerte gana y uno débil se rompe; defensores rodeados destruidos; la paz devuelve lo ocupado; la IA solo acepta la paz pasado un tiempo; desgaste sin suministro; recuperación y refuerzos desde la capital; bonificación de mando en cadena y alcance; nombres romanos y modernos de las formaciones; una vexilación manda 4 regimientos como mucho; cada nación empieza con una plantilla de dos guerreros; las plantillas se editan dentro de sus límites; una plantilla entrena un regimiento entero a la vez. |
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
