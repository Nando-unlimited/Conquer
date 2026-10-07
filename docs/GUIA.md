# Guía del código de Conquer

Qué hace cada fichero y cada función del proyecto. Mantenla al día: cuando un cambio añada, quite o
cambie de sentido un fichero o una función, actualiza su sección aquí en el mismo commit.

Estado: versión 1.0.2.

---

## 1. Visión general

La solución (`Conquer.sln`) tiene cinco proyectos:

| Proyecto | Tipo | Qué contiene |
| --- | --- | --- |
| `src/Conquer.Game` | Librería | Todas las reglas y la simulación: mundo, provincias, recursos, unidades, ciudades, migración, IA. **No sabe nada de gráficos**, así que se puede probar sin abrir ventana. |
| `src/Conquer.Presentation` | Librería | Lo que ve y hace el jugador sin depender de ningún motor gráfico: cámara, modos de mapa, reloj de juego, mensajes, textos (ayuda, historial, cifras) y partidas guardadas. Usa `Conquer.Game`. |
| `src/Conquer.Client` | Ejecutable (`Conquer.exe`) | Ventana, dibujo del mapa por shader, interfaz propia, menús. Usa `Conquer.Game` y `Conquer.Presentation`; es lo único que cambiaría al pasar a otro motor (Godot). |
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
- El tiempo se cuenta en horas desde el 1 de enero del año 1 de la partida. Los años son los de tu civilización, no los de la historia: las eras son etapas de desarrollo, no fechas.
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
| `GrowthRate`, `CityGrowthMultiplier` | Crecimiento diario de la población (0,004 %: un 1,5 % al año con fertilidad normal); las ciudades crecen el doble. |
| `BaseBirthsPerProvince`, `FoodSpoilage` | Nacimientos diarios fijos de cada provincia habitada (0,02), además de `GrowthRate` y por su fertilidad, para que un puñado de colonos llegue a aldea; no dependen de la tierra. Parte de la comida almacenada que se estropea cada día (1 %). |
| `LandCarryingScale`, `EraCapacity(era)` | La tierra alimenta al principio la décima parte de lo que dice su bioma (decenas de millones en todo el mundo); cada era lo multiplica: ×1,5 Clásica, ×2 Medieval, ×3 Descubrimientos, ×5 Industrial y ×8 Moderna. |
| `StarvationRate` | Población que muere al día si no hay comida. Los avances (Medicina) y el granero de la provincia salvan cada uno su parte: con los dos, muere la cuarta parte. |
| `CityCapacityMultiplier` | Una ciudad alimenta 2,5 veces más gente que la tierra sola. |
| `TaxGoldPerCitizen` | Oro por habitante y día. |
| `DepositFullWorkers` | Habitantes necesarios para que un yacimiento rinda al máximo. |
| `DepositSizeMultiplier` | Multiplica el tamaño de todas las bolsas de recurso al empezar la partida (1; la dificultad ajusta el tamaño de cada bolsa al generar el mapa, con `DifficultyInfo.DepositSize`). |
| `ScienceBasePerCity`, `SciencePerCityCitizen`, `ResearchCostMultiplier` | Ciencia diaria de cada ciudad: 0,25 fijos más 0,001 por habitante (antes de moral y avances). Cada avance cuesta el triple de sus puntos de lista, para que las eras duren generaciones. |
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
| `StartingMood`, `BaseMood`, `CityMood`, `CapitalMood` | Moral inicial (60) y factores fijos de la moral: base 50, +10 con ciudad, +15 en la capital. |
| `KmPerMoodPoint`, `MaxDistanceMoodPenalty` | −1 de moral por cada 50 km a la capital, hasta −20. |
| `MaxOvercrowdingMoodPenalty`, `StarvingMood` | Hasta −20 por hacinamiento (al doble de la capacidad) y −40 por hambre. |
| `ForcedMigrantMoodPenalty` | Los migrantes forzados llegan 20 puntos más descontentos que su provincia de origen. |
| `FoodReserveMood`, `FoodReserveFullDays` | Hasta +10 de moral por reservas de comida, completo con comida para 30 días. |
| `FestivalMood`, `FestivalDays`, `FestivalGoldPerHundred`, `MinFestivalCost` | Fiestas: +20 de moral durante 30 días por 2 de oro cada 100 habitantes (mínimo 10). |
| `FestivalCost(población)` | Oro que cuestan unas fiestas en una ciudad de ese tamaño. |
| `MoodChangePerDay`, `FertilityChangePerDay` | Parte de la distancia a su objetivo que recorren cada día la moral (10 %) y la fertilidad (3 %). |
| `UnrestMood` | Por debajo de 25 la provincia está descontenta y no paga impuestos. |
| `StarvingFertility` | Parte de la fertilidad que queda con hambre (20 %). |
| `MoodProductivity(moral)` | Multiplicador de la producción: ×0,75 con moral 0, ×1 con 50, ×1,25 con 100. |
| `TargetFertility(moral, hambre)` | Fertilidad hacia la que tiende una provincia: 0,5 + moral/100, por 0,2 si hay hambre. |
| `MoodNames`, `MoodLevel(moral)`, `MoodName(moral)` | Los cuatro niveles de moral (Descontento, Inquieto, Tranquilo, Contento), el nivel (0-3) de un valor y su nombre. |
| `SettlerCitizens`, `SettlersCost` | Una banda de colonos enviada desde una ciudad lleva 300 ciudadanos, y además cuesta esto. |
| `UnitType` | Tipos de unidad: `Settlers` (colonos), `Regiment` (regimiento), `Headquarters` (cuartel general) y `Fleet` (flota de barcos). |

### `Rules/MilitaryRules.cs`
Cifras del ejército. El combate se mide por hora; el resto, por día.

| Elemento | Qué es |
| --- | --- |
| `MaxBattalionsPerRegiment`, `MaxRegimentsPerBrigade`, `MaxDivisionParts`, `MaxDivisionMen`, `MaxShipsPerFleet`, `DryDockRepair` | Un regimiento tiene como mucho 5 batallones (y una plantilla, uno); una brigada, 4 regimientos; una división, 5 brigadas y regimientos y 10.000 hombres; una agrupación naval, 27 barcos (una fuerza); un dique seco repara las flotas el doble de rápido. |
| `HeadquartersSpeed` | Los cuarteles generales marchan a 1,5 veces el paso de un ciudadano. |
| `TechTrainingSpeed` | Cada avance que estudia un batallón (`TechInfo.FasterTraining`) hace que el cuartel lo entrene un 25 % antes (los días se dividen entre 1 + 0,25 por avance). |
| `OrganisationDamage`, `StrengthDamage` | Organización y hombres que pierde un bando por cada punto de fuego enemigo, repartidos entre sus batallones que combaten. |
| `BreakingOrganisation` | Una unidad se rompe por debajo del 10 % de su organización: el defensor se retira y el atacante abandona. |
| `CombatRandomness` | El fuego de cada bando varía un ±20 % cada hora. |
| `FrontWidth(bioma)` | Batallones que caben en primera línea: 12 en llano, 9 en colinas, 8 en bosques y pantanos, 6 en montañas y 4 en alta montaña. La artillería y la aviación, hasta la mitad, disparan desde detrás. |
| `SupportExposure` | La artillería y la aviación reciben una cuarta parte del daño que les tocaría. |
| `CombinedArmsBonus`, `MaxCombinedArmsBonus` | +10 % de fuego por cada tipo de tropa distinto más allá del primero entre los que combaten, hasta +30 %. |
| `FactoryOutput`, `StartingWarriorKits`, `StartingScoutKits` | Una fábrica hace el doble de equipo que un taller; cada nación empieza con equipo para 4 batallones de guerreros y 2 de exploradores. |
| `MountainTroopsRoughTerrain`, `MedicsSaving`, `AntiAirAgainstAircraft`, `AntiAirShield`, `MaxAntiAirShield` | Las tropas de montaña, ×1,5 en terreno difícil; los médicos salvan un 30 % de las bajas; la antiaérea dispara ×2 contra aviones y les quita un 25 % de fuego por batallón, hasta un 75 %. |
| `ExperienceBonus`, `ExperiencePerBattleHour` | Un batallón de élite dispara un 50 % más; cada hora de combate gana un 0,3 % de lo que le falta. |
| `UpkeepGoldShare`, `UpkeepResourceShare` | Mantenimiento diario: un 2 % del oro y un 1 % de los demás recursos (salvo la madera) de lo que costó cada batallón, barco y cuartel general. |
| `UnpaidOrganisationLoss`, `UnpaidDesertion` | Sin pagar, cada día se pierde un 10 % de organización y deserta un 2 % de los hombres, sin recuperación ni refuerzos. |
| `MountedRoughTerrainAttack`, `TerrainDefense(bioma)`, `IsRough(bioma)` | La caballería ataca a la mitad en terreno difícil; el defensor dispara ×1,25 en colinas, ×1,5 en montañas y ×1,2 en bosques y pantanos. |
| `RiverDefense`, `DefenseMultiplier(provincia, ingenieros)` | Con un gran río el defensor dispara ×1,25 más (el atacante tiene que cruzarlo); se multiplica por el del terreno. Si el atacante lleva ingenieros, el río no cuenta y el bonus del terreno se queda en la mitad (`EngineeredTerrainDefense`). |
| `EngineerWorkDays` | Días de trabajo que cada batallón de ingenieros hace al día en una carretera o un ferrocarril. |
| `CommandBonus`, `HigherCommandBonus` | +10 % en combate y recuperación con el cuartel propio a su alcance, y +5 % por cada nivel superior enlazado. |
| `OfficerCost`, `MaxUnitNameLength` | Reclutar un oficial para la reserva cuesta 40 de oro; el nombre que el jugador da a una unidad tiene como mucho 30 letras. |
| `SupplyRangeHours` | El suministro llega hasta 10 días de marcha por tierra propia o libre desde una ciudad o desde las carreteras y ferrocarriles unidos a ella. |
| `OutOfSupplyEfficiency`, `OutOfSupplyOrganisationLoss`, `OutOfSupplyAttrition` | Sin suministro se lucha al 75 % y se pierde cada día un 5 % de organización y un 1 % de hombres. |
| `SiegeDaysPerDefense`, `SiegeAttackPerDay` | Días de asedio por punto de defensa de las fortificaciones (60: 30 con murallas, 60 con castillo) y ataque de artillería que suma un día de asedio al día (12: un batallón de catapultas). |
| `OrganisationRecovery`, `ReinforcementRate` | Con suministro y fuera de combate se recupera un 20 % de organización al día; la capital envía cada día a cada batallón reclutas para un 5 % de sus hombres (ver `GameSession.Logistics`). |
| `UnattachedShipmentSlowdown` | Los envíos a unidades sin cuartel general a su alcance tardan el doble. |
| `ConvoyCapacity`, `ConvoysPerOrder`, `ConvoysInOldSaves` | Un convoy lleva 100 hombres, piezas o suministros; los astilleros los hacen en lotes de 5; las partidas anteriores dan 10 a cada nación con puerto. |
| `EscortWeight`, `ConvoyEvasion`, `MaxDailyConvoyLoss`, `SubmarineRaidBonus` | Cada día un envío en el mar pierde ataque / (ataque + 2 × escolta + 30) de sus convoyes y su carga (como mucho el 60 %); los submarinos atacan convoyes el doble. |
| `AmmoPerHundredMenHour`, `AmmoHours`, `OutOfAmmoEfficiency` | Cada batallón que combate gasta 0,25 suministros por cada 100 hombres y hora; una unidad lleva munición para 24 horas de combate; sin munición se lucha al 50 %. |
| `OccupiedMood` | −30 de moral en una provincia ocupada por el enemigo. |

### `Military/Battalions.cs`
Los batallones que se entrenan en las ciudades, la tropa especializada de la que se forman los regimientos. Cada tipo es una **línea** con un **modelo** por época: un batallón conserva su línea y lucha con el modelo más moderno que conoce su nación. **Aquí se añaden y equilibran las tropas.**

| Elemento | Qué es |
| --- | --- |
| `BattalionType` | Las líneas. Infantería: ligera (guerreros, vélites con Tácticas militares, arcabuceros con Pólvora, infantería ligera con Estriado), pesada (espadachines con Trabajo del bronce, falanges con Tácticas militares, legionarios con Instrucción militar, infantería pesada con Armerías, piqueros con Pólvora y, con el Estriado, infantería ligera), a distancia (arqueros con Tiro con arco, ballesteros con Maquinaria, mosqueteros con Ciencia militar, fusileros con Estriado, ametralladores con Ametralladoras), de montaña (montañeses con Tácticas militares, almogávares con Armerías, cazadores de montaña con Pólvora, cazadores alpinos con Estriado, tropas de montaña con Motor de combustión) y paracaidistas (Aviación). Caballería: jinetes (Doma del caballo), catafractos (Caballería pesada), caballeros (Estribo) y caballería mecanizada (Motor de combustión); carros de guerra (La rueda) y tanques (Blindados). Artillería: catapultas (Maquinaria de asedio), trabuquetes (Talleres de asedio), cañones (Metalurgia), artillería de campaña (Acero) y pesada; y antiaérea (Aviación). Apoyo: exploradores (50 hombres, desde el principio; solo las unidades con exploradores reclaman tierra), ingenieros (Ingeniería: rebajan la defensa del terreno y el río cuando atacan y construyen calzadas y ferrocarriles) y médicos (50 hombres, Medicina). Bombarderos (Aviación), que vuelan. Barcos, en líneas con un modelo por época: transportes (barco de transporte con Navegación a vela, 600 hombres; carraca con Cartografía, 900; vapor de transporte con Máquina de vapor, 1.500; buque de transporte con Motor de combustión, 2.500), buques de línea (trirreme, galeón —lleva 200—, navío de línea con Metalurgia, acorazado con Acero y acorazado moderno con Ingeniería naval), escoltas (liburna, carabela, corbeta con Cartografía y Pólvora, fragata, crucero con Acero y destructor con Ingeniería naval), submarinos (Motor de combustión) y portaaviones (Ingeniería naval y Aviación). Los de Ingeniería naval solo se construyen en ciudades con dique seco (`Shipyard`). Las partidas anteriores a 1.111.0 pasan sus barcos a su línea y modelo al leerse (`SaveGame.RenameOldShips`). |
| `BattalionGroup`, `LineInfo`, `Line(tipo)` | El grupo de cada línea (infantería, caballería, artillería, apoyo, marina o aviación), su nombre y sus modelos, del más antiguo al más moderno. |
| `BattalionInfo` | Un modelo: clave para su maqueta (`Key`), nombre, símbolo, hombres, coste, días de instrucción, avances que requiere, ataque, defensa, organización máxima, velocidad, si es montado (caballos, carros y tanques), si es una máquina de guerra (`Machine`: se hace en el taller), si vuela (`Flies`), si es un barco (`Naval`: sus hombres son la tripulación y su ataque, sus cañones) y cuántos hombres lleva (`Capacity`). Su equipo: `Pieces` (las piezas que necesita a plantilla completa: un arma por hombre, un caballo por jinete, 30 carros, 5 catapultas con 50 hombres, 10 tanques con 50; ninguna los barcos) y `PieceName` («armas», «caballos», «catapultas»...); la infantería de cada época comparte sus armas y exploradores, ingenieros y médicos los suministros (`Supply`, un `SupplyInfo`), que se guardan juntos bajo `SupplyKey` (los demás, bajo su `Key`); `SupplyName` es el título de lo que fabrica un taller («Armas clásicas», «Suministros», «Catapultas», «Caballos (jinetes)»); `TrainingCost` (lo que cuesta entrenarlo: su oro) y `EquipmentCost` (lo que cuesta su equipo: el resto), `PiecesText(n)` («100 armas antiguas», «5 catapultas», «50 suministros»). |
| `SupplyInfo`, `Supplies` | Un suministro compartido (clave, nombre y coste de cada pieza) y los que hay: `General` (suministros) y las armas de cada época (`AncientArms`, `ClassicalArms`, `MedievalArms`, `Firearms`, `Rifles`, `ModernArms`). En la tabla de modelos, `Takes(suministro, modelo)` suma al oro del modelo el coste de sus piezas; `UsersOf` y `BySupply` dan los modelos que lo usan. |
| `Battalions.All`, `Models(tipo)`, `First(tipo)`, `ByKey(clave)`, `ModelByKey(clave)`, `BestModel(tipo, avances)`, `ModelFor(tipo, avances)` | Todas las líneas; sus modelos; el primero (lo que hace falta para entrenar la línea); el índice del más moderno cuyos avances se conocen (−1 si ninguno) y ese modelo (el primero si ninguno). |
| `BattalionRole`, `Role(tipo)`, `Name(papel)`, `Name(grupo)` | El papel de cada línea en combate: infantería, caballería, artillería (también la antiaérea), blindados, aviación, naval, ingenieros o apoyo (los médicos: no combaten). |
| `TrainingBuilding(modelo, tipo)` | El edificio que pide la provincia para entrenarlo: taller (o la fábrica en que se convierte) para las máquinas de guerra (toda la artillería, la antiaérea, los tanques y los bombarderos; los carros de guerra no), cuartel para las demás tropas de combate, y ninguno para exploradores, ingenieros, médicos y barcos (que piden puerto). |
| `Battalion` | Un batallón de una unidad: su línea, su modelo (`Model`; `Info` es su ficha), sus hombres (`Strength`), su organización, ambos como parte de su máximo, y su experiencia (`Experience`, 0 a 1; `ExperienceName`: Novato, Regular, Veterano o Élite). `Modernise(modelo)` le hace adoptar uno más moderno, conservando sus hombres y su parte de organización. |

### `Military/Formations.cs`
Los nombres de las formaciones: batallón → regimiento (hasta 5) → brigada (hasta 4 regimientos) → división (hasta 5 brigadas y regimientos y 10.000 hombres), cada una de ellas una sola unidad en el mapa (`Echelon`), → cuerpo → ejército → grupo de ejércitos (cuarteles generales).

| Elemento | Qué es |
| --- | --- |
| `CombatName(tamaño)`, `CombatPluralOf(tamaño)`, `Count(n, tamaño)`, `CombatPlural`, `CombatEchelon(tamaño)` | «Regimiento», «Brigada» o «División»; sus plurales; «3 regimientos»; las marcas OTAN III, X o XX. |
| `LevelName`, `LevelPlural`, `SubordinatesPlural` | Nombre de cada nivel de cuartel general: Cuerpo, Ejército y Grupo de ejércitos. |
| `BattalionCount`, `BattalionName` | «3 batallones»; «Batallón de arqueros» por su modelo o «Batallón de infantería a distancia» por su línea, o el tipo de barco («Trirreme»). |
| `Function(batallones)`, `FunctionOf(tipo[, modelo])`, `UnitFunction` | El símbolo OTAN de una unidad: el del papel más común entre sus batallones (infantería, montaña, paracaidistas, caballería, blindados, mecanizada si hay tanques con infantería o caballería, artillería, antiaérea, ingenieros, médicos, aviación, naval). Los carros de guerra tirados por caballos cuentan como caballería. |
| `CombatUnitName(número, tamaño)`, `HeadquartersName(nivel, número)`, `Roman(n)` | «3.er Regimiento», «3.ª Brigada», «1.ª División» (cada tamaño se numera aparte); «IV Cuerpo», «1.er Ejército», «2.º Grupo de ejércitos». |
| `ShipCount(n)` | «3 barcos». (El nombre de una agrupación naval va por su tamaño: «1.ª Flotilla», «2.ª Escuadra», «1.ª Fuerza».) |

### `Military/Echelons.cs`
| Elemento | Qué es |
| --- | --- |
| `Echelon` | El tamaño de una unidad de combate: regimiento, brigada o división. |
| `Regiment`, `Brigade` | Un regimiento (sus batallones) o una brigada (sus regimientos) dentro de una unidad, con su número y su nombre propios (`Name`), que recupera si sale a moverse por su cuenta; `Men`, sus hombres a plantilla completa. Un regimiento suelto lleva en su unidad el número y el nombre de ella. |

### `Military/Officer.cs`
Los oficiales al frente de las unidades de combate y, como generales, de los cuarteles generales.

| Elemento | Qué es |
| --- | --- |
| `OfficerRank` | Coronel (regimiento), Brigadier (brigada), General de división (división), Teniente general (cuerpo), General (ejército) y Mariscal (grupo de ejércitos). En la armada y la aviación, sus equivalentes (`RankName(rango, arma)`). |
| `OfficerBranch` | El arma de un oficial, desde que se recluta: ejército (regimientos y cuarteles), armada (flotas: capitán de navío, comodoro, contraalmirante, vicealmirante, almirante, gran almirante) o aviación (regimientos de aviones: coronel de aviación, general de brigada aérea, general de división aérea, teniente general del aire, general del aire, mariscal del aire). `Officer.Branch`, `BranchName`, `BranchOfficer`; se guarda en `OfficerSave.Branch` (ejército en las partidas anteriores a 1.91.0). |
| `OfficerTrait`, `IsFlaw` | Seis virtudes, que mejoran por estrella: Ofensivo (+10 % de ataque), Defensivo (+10 % de defensa), Organizador (+25 % de recuperación), Táctico (−10 % de organización perdida en combate, hasta −50 %), Marchador (+5 % de velocidad) e Intendente (−5 % de mantenimiento). Y sus seis defectos, en el mismo orden y fijos: Timorato (−15 % de ataque), Temerario (−15 % de defensa), Desorganizado (−25 % de recuperación), Indeciso (+20 % de organización perdida), Lento (−15 % de velocidad) y Corrupto (+20 % de mantenimiento). `(int)rasgo % 6` es lo que afecta. |
| `Officer` | Id, nombre, rasgos, estrellas iniciales (1 a 3), victorias y rango (que solo sube); `Skill` suma una estrella cada 3 victorias, hasta 5. `FireBonus`, `RecoveryBonus`, `OrganisationLoss`, `SpeedBonus` y `UpkeepChange` dan su efecto (negativo si estorba); `Title` («Coronel Hernán Ulloa»), `Summary` («Ofensivo, Lento **»), `TraitsDescription`, `TraitName`, `TraitDescription` y `RankName` lo describen. |
| `Recruit(id, azar, rango)` | Un oficial nuevo: nombre al azar, 1 a 3 estrellas, una virtud, a veces una segunda y a veces un defecto que no anula una de sus virtudes. |

### `Military/Templates.cs`
`RegimentTemplate`: diseño de unidad como en Hearts of Iron: qué líneas de batallón lleva (1 a 5: un regimiento). Las ciudades entrenan unidades enteras a partir de él. `Name` («Plantilla II») y `Requires` (los avances que abren cada línea); lo demás depende de los avances de la nación (`known`), que deciden el modelo de cada batallón (`Models`): `Men`, `Cost` (lo que cuesta entrenarlos: su oro), `Equipment` (el equipo que necesitan: «200 armas de guerreros»), `TrainingDays` (el del batallón más lento, porque se instruyen a la vez, sin contar los avances que aceleran; con ellos, `GameSession.TrainingDays`), `TrainingBuildings` (cuartel, taller, los dos o ninguno), `Attack`, `Defense`, `MaxOrganisation`, `Speed`, `AnyMounted` y `Composition` («2 × Guerreros, 1 × Arqueros»).

### `Military/Command.cs`
La cadena de mando, la instrucción y las batallas.

| Elemento | Qué es |
| --- | --- |
| `CommandLevelInfo`, `CommandLevels` | Los tres niveles de cuartel general: cuerpo, ejército y grupo de ejércitos, con su alcance (600 a 2.500 km), 5 subordinados, personal, coste y días. El nivel 0 (`Combat`) es la unidad de combate. Los nombres salen de `Formations`. |
| `TrainingOrder` | Lo que entrena una provincia (su ciudad o su cuartel): un batallón, una unidad entera de una plantilla (`TemplateName`, `TemplateBattalions`) o un cuartel general, con el modelo de cada batallón (`Models`: el del equipo que se le dio) y los días que le quedan y los que tarda en total (los de su nación al pagarlo); `Name` lo nombra («Batallón de arqueros», «Brigada (Plantilla II)»). |
| `Battle` | Una batalla por una provincia: quién ataca, quién defiende, las unidades atacantes (que esperan en sus provincias), cuándo empezó, las bajas de cada bando y, al terminar, quién ganó y cuándo (`AttackersWon`, `EndHours`). `History` guarda cada hora (sin guardarse en la partida). |
| `BattleHour` | Una hora de batalla: hombres y organización media que le quedan a cada bando y el fuego de cada uno. |

### `Rules/Difficulty.cs`
Los niveles de dificultad. **Aquí se equilibran.**

| Elemento | Qué es |
| --- | --- |
| `Difficulty` | Muy fácil, Fácil, Normal, Difícil y Muy difícil. Se elige con el mapa y va en sus ajustes (`WorldSettings.Difficulty`), así que se guarda con la partida. |
| `DifficultyInfo` | Nombre, descripción y lo que cambia cada nivel: la probabilidad de la segunda tirada de yacimientos (`ExtraDepositChance`: de 3 a 0), el tamaño de las bolsas (`DepositSize`: de 1,5 a 0,5), los recursos iniciales del jugador humano (`StartingResources`: de 2 a 0,5) y lo que producen e investigan los rivales del ordenador (`ComputerOutput`: de 0,75 a 1,5). Normal deja todo en 1. |
| `Difficulties.All`, `Info(nivel)` | Todos los niveles y la ficha de cada uno. |

### `Rules/Modifiers.cs`
`Modifiers`: mejoras sobre las reglas normales. Los avances las aplican a toda la nación y los edificios a su provincia; todas se suman con `+`. Campos: parte extra de comida, madera, yacimientos, impuestos, ciencia, capacidad de la tierra y fertilidad; puntos de moral; parte de las muertes por hambre que se evita; daño extra de quien defiende la provincia (`Defense`), rapidez de las obras (`BuildSpeed`: 0,25 las acaba en 1/1,25 de los días) y parte de la moral perdida por la distancia a la capital que se evita (`DistanceMood`). `Modifiers.None` es «sin mejoras».

### `Buildings/Building.cs`
Los edificios que se construyen en las provincias. **Aquí se añaden y equilibran los edificios.**

| Elemento | Qué es |
| --- | --- |
| `BuildingType` | Granja, Granero, Aserradero, Mina, Cuartel (sin avance, en cualquier provincia: sin él no se entrenan tropas de combate, y en una provincia sin ciudad permite entrenar, ver `Battalions.TrainingBuilding`), Templo, Biblioteca, Mercado, Acueducto, Herbolario, Anfiteatro (+10 de moral; pide Construcción), Muralla (+50 % de daño al defender; pide Fortificaciones), Universidad (+50 % de ciencia; Educación), Banco (+50 % de impuestos; Banca) Castillo (defensores al doble de daño; Castillos), Fábrica (fabrica el doble de equipo que el taller y construye las máquinas de guerra, +50 % de madera y yacimientos; Industrialización), Hospital (+20 % de fertilidad, +10 % de capacidad; Salubridad), Central eléctrica (+25 % de ciencia e impuestos; Electricidad), Puerto (en ciudades con costa: construye y repara barcos, +15 % de impuestos; Navegación a vela) Dique seco (pide puerto: construye los barcos más avanzados y repara el doble de rápido; Ingeniería naval) y Taller (Maquinaria de asedio: sin él no se construyen máquinas de guerra; con la Industrialización pasa a ser fábrica, `BuildingInfo.BecomesWith`, y desde entonces ya no se construye: `Buildings.For(tipo, jugador)` da el edificio que toca). Granja, granero, aserradero, mina, cuartel, taller y fábrica se construyen en cualquier provincia; el resto solo donde hay ciudad. `Road` y `Railway` solo quedan para leer partidas anteriores a la 1.36.0 (no están en `Buildings.All`): las carreteras son ahora tramos entre provincias (`RoadNetwork`). Salvo la granja y el aserradero, cada uno pide un avance (el granero, Alfarería). |
| `BuildingInfo` | Nombre, descripción, coste, días de obra, avance que requiere, si solo va en ciudades, si necesita un yacimiento sin agotar, sus efectos (`Modifiers`) en la provincia, si necesita costa (`NeedsCoast`), otro edificio antes (`RequiresBuilding`: el dique seco, el puerto). |
| `Buildings.All`, `Buildings.Info(tipo)` | Todos los edificios y la ficha de cada uno. |

### `Science/Technology.cs`
Los avances que se pueden investigar, en tres ramas con niveles; en cada nivel se elige qué avance investigar, como en Civilization. **Aquí se añaden y equilibran los avances.**

| Elemento | Qué es |
| --- | --- |
| `Era`, `Name(era)`, `The(era)` | Las eras: Antigüedad, Clásica, Medieval, Descubrimientos, Industrial y Moderna (`The` las da con artículo para las frases: «la era de los Descubrimientos»). Cada una después de la primera se abre con una institución. |
| `TechBranch`, `Name(rama)` | Las tres ramas: Economía, Sociedad y Militar. |
| `Tech` | Los avances. Los nuevos van al final del enum, porque las partidas guardadas guardan el progreso por posición. Por rama y nivel: Economía (1: Agricultura, Carpintería; 2: Minería, Irrigación; 3: Moneda), Sociedad (1: Escritura, Mitología; 2: Alfarería, Medicina; 3: Código de leyes) y Militar (1: Tiro con arco, Doma del caballo; 2: Trabajo del bronce, La rueda; 3: Trabajo del hierro). Los niveles 1 a 3 son de la Antigüedad; los 4 y 5, de la era Clásica: Economía (4: Comercio, Construcción; 5: Ingeniería, Navegación a vela), Sociedad (4: Filosofía, Matemáticas; 5: Drama y poesía) y Militar (4: Tácticas militares, Maquinaria de asedio; 5: Caballería pesada, Fortificaciones); Construcción acelera las obras un 25 % y Administración reduce a la mitad la moral perdida por distancia (Sociedad 5). Los 6 y 7, de la Medieval: Economía (6: Rotación de cultivos, Gremios; 7: Banca), Sociedad (6: Teología, Educación; 7: Astronomía) y Militar (6: Estribo, Maquinaria; 7: Castillos). Los 8 y 9, de la era de los Descubrimientos: Economía (8: Economía, Minería profunda; 9: Cartografía), Sociedad (8: Imprenta, Anatomía; 9: Método científico) y Militar (8: Pólvora, Metalurgia; 9: Ciencia militar). Los 10 y 11, de la Industrial: Economía (10: Máquina de vapor, Industrialización; 11: Ferrocarril), Sociedad (10: Química, que revela el caucho, Salubridad; 11: Educación pública) y Militar (10: Estriado, Acero; 11: Ametralladoras). Los 12 y 13, de la Moderna: Economía (12: Refinado del petróleo, que revela el petróleo, Fertilizantes; 13: Producción en cadena, Ingeniería naval), Sociedad (12: Electricidad, que revela el aluminio, Antibióticos; 13: Electrónica, que revela el silicio) y Militar (12: Motor de combustión, Artillería pesada; 13: Blindados, Aviación). Además, avances militares que mejoran el cuartel (`FasterTraining`): Arcos mejorados (3, arqueros, carros de arqueros y ballesteros) y Cría caballar (3, caballería y carros), Instrucción militar (5, guerreros, lanceros, infantería de hierro y legionarios), Talleres de asedio (6, catapultas y cañones), Armerías (7, infantería de hierro, legionarios, catafractos y caballeros), Piezas intercambiables (9, armas de fuego de infantería), Servicio militar obligatorio (11, mosqueteros, fusileros, ametralladoras e infantería motorizada), Fundiciones de artillería (11, cañones y artillería) y Producción bélica (13, infantería motorizada, tanques y bombarderos). |
| `TechInfo` | Nombre, rama, nivel (se abre al conocer la mitad del nivel anterior de su rama), descripción, efectos en toda la nación (`Effects`, como `Modifiers`), avances que también pide, de su rama o de otra (`Requires`: Irrigación pide Agricultura; La rueda, Doma del caballo; Medicina, Escritura; Moneda, Escritura y Minería; Trabajo del bronce, Minería; Trabajo del hierro, Trabajo del bronce) , recursos que revela (`Reveals`: Minería el carbón, Trabajo del hierro el hierro) y su era (`Era`; todos los actuales son de la Antigüedad), y los batallones que el cuartel entrena más rápido desde entonces (`FasterTraining`, ver `MilitaryRules.TechTrainingSpeed`). `Cost` sale de su nivel. |
| `LevelCost(nivel)`, `LevelCosts` | Lo que cuesta cada nivel, igual en las tres ramas: 70, 160, 300, 500, 750, 1.100, 1.600, 2.300, 3.200, 4.500, 6.200, 8.500 y 12.000 puntos (sin contar el recargo por institución). **Aquí se equilibra la velocidad de la investigación.** |
| `Techs.All`, `Branches`, `Info(avance)`, `InBranch(rama)`, `InLevel(rama, nivel)`, `Levels(rama)`, `NeededToOpenNext(rama, nivel)` | Todos los avances, las ramas, la ficha de cada avance, los de una rama por nivel, los de un nivel, cuántos niveles tiene una rama y cuántos avances de un nivel abren el siguiente. |

### `Science/Institution.cs`
Las instituciones, como en Europa Universalis: ideas que abren cada era. **Aquí se añaden y equilibran.**

| Elemento | Qué es |
| --- | --- |
| `Institution` | Las instituciones: el Urbanismo abre la era Clásica el Feudalismo (+10 % de impuestos y +3 de moral) la Medieval el Humanismo (+10 % de ciencia y +3 de moral) la era de los Descubrimientos la Industrialización (+15 % de madera y de yacimientos) la Industrial y la Electrificación (+15 % de ciencia y +10 % de impuestos) la Moderna. |
| `InstitutionInfo` | Nombre, era que abre (`Opens`), dónde nace (`Birth`, para la interfaz), descripción, bonus al adoptarla (`Bonus`, como `Modifiers`: el Urbanismo da +10 % de ciencia y +10 % de fertilidad), color en el mapa y género (`Feminine`); `The` es su nombre con artículo («el Urbanismo»). |
| `Institutions.All`, `Info(institución)` | Todas las instituciones y la ficha de cada una. |

### `Economy/ResourceType.cs`
| Elemento | Qué es |
| --- | --- |
| `ResourceType` | Los 15 recursos: comida, madera, carbón, hierro, cobre, silicio, petróleo, aluminio, caucho, oro, plata, piedra, azufre, salitre y caballos (los nuevos van al final: las partidas guardan por posición). `IsRenewable`: los caballos salen de pastos que no se agotan. |
| `Resources.All` / `Resources.Deposits` | Todos los recursos / los que salen de yacimientos (todos menos comida y madera). |
| `Resources.KnownFromStart` | Recursos que todos conocen desde el principio: comida, madera, piedra, caballos, cobre, oro y plata. Los demás están ocultos, y no se pueden explotar, hasta que un avance los revela. |
| `Resources.Name(type)` | Nombre en español para la interfaz. |
| `Stockpile` | Almacén nacional de recursos de un jugador (`stockpile[recurso]`). |
| `Stockpile.Has(cost)` | ¿Hay suficiente para pagar un coste? |
| `Stockpile.TrySpend(cost)` | Paga el coste si puede; devuelve si lo ha pagado. |
| `ResourceCost` | Lista de (recurso, cantidad); `ToString()` lo escribe como "150 Comida, 50 Madera"; `Times(n)` lo multiplica. |

### `Entities/Entities.cs`
Los objetos de una partida.

| Elemento | Qué es |
| --- | --- |
| `Player` | Jugador: id, nombre, color, si es humano, almacén, provincias que posee, capital, balance del último día (`LastDayNet`) y lo que entró y salió de cada recurso según de dónde vino o adónde fue (`LastDayFlows`, por `ResourceFlow`: producción, comercio, consumo, ejército y talleres; `Record` lo anota), si pasa hambre y cuántos días duraría su comida (`FoodReserveDays`).; avances conocidos (`Techs`) y la suma de sus efectos (`Bonuses`), prioridad de cada rama (`ResearchPriorities`, de 0 a 10) y la parte de la ciencia que le toca (`ScienceShare(rama)`), el avance que investiga cada rama (`Researching[rama]`, nulo mientras no elige), puntos puestos en cada avance (`ResearchProgress`), ciencia que ninguna rama pudo aceptar porque no investigaban nada (`SpareScience`) y ciencia del último día. recursos que conoce (`KnownResources`, `Knows(recurso)`); `Learn(avance)` añade un avance y sus efectos, revela sus recursos y sube su era (`Era`: la más reciente de sus avances); sus plantillas (`Templates`); su almacén de equipo (`Equipment`, piezas por modelo o los suministros compartidos: `EquipmentOf`, `AddEquipment`); sus oficiales sin destino (`OfficerReserve`); si el último día no pudo pagar el mantenimiento del ejército (`ArmyUnpaid`); su reserva de reclutas (`Manpower`); su fe (`ReligionId`, al azar al empezar); instituciones adoptadas (`Institutions`), y `Adopt(institución)` suma su bonus a `Bonuses`; si ha desaparecido de la partida (`Eliminated`). |
| `City` | Ciudad: id, nombre, dueño, provincia, fecha de fundación y hasta cuándo dura su fiesta (`FestivalUntilHours`, `HasFestival(ahora)`). Lo que entrena está en su provincia (`Province.Training`). |
| `Unit` | Unidad en el mapa: colonos, unidad de combate (regimiento, brigada o división, `Size`, que se mueve y combate entera), cuartel general o flota. Tipo, dueño (`Owner`), provincia, número, nombre (`Name`: el que le dio el jugador, `CustomName`, o si no `AutomaticName`, según el nivel, el número y, en las de combate, el tamaño), sus partes (`Regiments`: el de un regimiento, los de una brigada o los sueltos de una división; `Brigades`: las de una división; `Parts` las cuenta), sus batallones (`Battalions`: todos los de sus partes, o los barcos de una flota, `Ships`) y sus hombres a plantilla completa (`FullMen`), nivel (cuartel), su oficial (`Officer`: el de una unidad de combate o el general de un cuartel; `HasOfficer` dice si lleva, y `RequiredRank` el rango que pide su tamaño: coronel, brigadier o general de división), cuartel del que depende (`CommanderId`), provincia que ataca (`AttackingProvinceId`) y ruta pendiente (`Path`). `Citizens` son los colonos, el personal o los hombres de sus batallones; `Speed`, la de su batallón más lento, cambiada por su oficial; `Flies`, si solo tiene aviones (cruza el mar); `IsFleet`, si es una flota, y `Capacity`, los hombres que lleva; `CarrierId`/`IsAboard`, la flota en la que viaja; `OrganisationShare`/`StrengthShare`, su estado; `CommandLevel` y `Symbol`, para la cadena de mando y la ficha; `Echelon` y `Function`, las marcas de tamaño y el símbolo OTAN de su arma; `HasScouts`, si lleva algún batallón de exploradores (solo esas reclaman), `IsScouting`, si solo tiene exploradores, y `AutoClaim`, si explora y reclama tierra por su cuenta. `HoursToNext`/`StepHours` miden el tramo actual; `StepProgress` da el avance (0..1) para dibujarla entre provincias. |
| `Migration` | Grupo de migrantes en camino: origen, destino, personas, salida, llegada, si es forzada y la moral que llevan (`Mood`). |
| `Notification` | Mensaje para un jugador (fecha, jugador, texto). |

`GameDate` es un instante del juego (horas desde el inicio, el 1 de enero del año 1). Calendario de 365 días sin bisiestos; los años cuentan la vida de tu civilización, no la historia real.
`GameDate` es un instante del juego (horas desde el inicio). Calendario de 365 días sin bisiestos.

| Elemento | Qué es |
| `Days`, `Hour`, `Year`, `DayOfYear`, `MonthAndDay` | Partes de la fecha; `Year` empieza en 1. |
| `Days`, `Hour`, `Year`, `DayOfYear`, `MonthAndDay` | Partes de la fecha; `Year` es negativo antes de Cristo y no existe el año 0. |
| `AddHours(h)` | Fecha tras sumar horas. |
| `ToString()` | "1 ene, año 1, 00:00". |

### `Simulation/GameSession.cs`
El corazón del juego: una partida en marcha. Es una clase parcial: el ejército está en `GameSession.Military.cs` y la diplomacia en `GameSession.Diplomacy.cs`.

| Función | Qué hace |
| --- | --- |
| `CommandResult` | Resultado de una orden: `Ok` y un mensaje para el jugador. |
| `Create(map, jugadores, semilla, rivales)` | Nueva partida (los tests pueden quitar los rivales): limpia las provincias (dueño, controlador, población, ciudad, moral, fertilidad, edificios y bolsas de recurso llenas según `DepositSizeMultiplier`), crea jugadores con sus recursos iniciales (los del humano, multiplicados por la dificultad), una plantilla de dos cohortes de guerreros y una unidad de colonos cada uno en sitios fértiles y alejados, y crea las IA. |
| `UnitById`, `CityById`, `CityIn(provincia)` | Búsquedas. |
| `CapacityOf(provincia)` | Habitantes que puede alimentar una provincia, contando la bonificación de ciudad, la era de su dueño (`EraCapacity`), sus avances (Irrigación) y sus edificios (Acueducto). |
| `BonusesOf(provincia)` | Mejoras que se aplican a una provincia: las de los avances de su dueño más las de sus edificios. |
| `MoodFactors(provincia)` | Lista de (causa, puntos) que forman la moral objetivo: base, ciudad, capital o distancia a ella (menos con Administración), fiestas, reservas de comida, hacinamiento, hambre, ocupación enemiga, cultura extranjera (menos cuanto más asimilada), avances que dan moral (Mitología) y edificios que dan moral (Templo). |
| `TargetMood(provincia)` | Suma de esos factores, entre 0 y 100. |
| `Stats(jugador)` | Totales de la nación (`NationStats`): población asentada, en unidades y migrando; provincias, ciudades y unidades; moral y fertilidad medios ponderados por habitantes, habitantes en cada nivel de moral, lo que queda en los yacimientos de sus provincias (`Reserves`) y los nacimientos diarios (`DailyBirths`). |
| `Step()` | Avanza una hora: mueve unidades, resuelve las batallas en tierra y en el mar, hace llegar migrantes, los exploradores que van solos reclaman y eligen destino; a medianoche economía, migración, ciencia, instituciones, obras y ejército; cada 6 h piensan las IA. |
| `ArriveMigrations()` | Suma los migrantes que llegan a su destino (si el destino se perdió o está ocupado, van a la capital), mezclando su moral. |
| `DailyEconomy(jugador)` | Las provincias ocupadas no producen ni comen para su dueño. Producción del día (comida, madera, oro, yacimientos) multiplicada por la moral, los avances y los edificios de cada provincia; los yacimientos sacan de su bolsa hasta agotarla (`Extract`), solo los de recursos que el jugador conoce; sin impuestos en provincias descontentas; consumo de comida, hambre, días de reserva de comida, moral y fertilidad, y crecimiento de la población (proporcional a la fertilidad). Resta el mantenimiento del ejército (`Upkeep`); si no llega, anota `ArmyUnpaid` y avisa al jugador. |
| `Extract(provincia, recurso, cantidad)` | Saca de la bolsa de un yacimiento lo que se pide o lo que queda, y avisa al jugador cuando se agota. |
| `UpdateMoodAndFertility(provincia, dueño, hambre)` | Acerca la moral a su objetivo y la fertilidad a la que marca la moral; avisa cuando una ciudad del jugador entra o sale del descontento. |
| `TargetFertility(provincia, dueño, hambre)` | Fertilidad hacia la que tiende una provincia, con los avances de su dueño (Medicina) y sus edificios (Herbolario). |
| `SciencePerDay(jugador)` | Puntos de ciencia al día de sus ciudades, por su moral, sus avances (Escritura) y sus edificios (Biblioteca); los de los rivales, además, por la dificultad. |
| `Difficulty`, `OutputMultiplier(jugador)` | La dificultad de la partida (la de los ajustes del mapa) / lo que multiplica la producción y la ciencia de un jugador: `ComputerOutput` para los rivales del ordenador y 1 para el humano. En `DailyEconomy` se aplica después de sacar de los yacimientos, para que la producción extra no los agote antes. |
| `DailyScience(jugador)` | Reparte la ciencia del día (más la guardada) entre las ramas que investigan algo, según su prioridad. Cada rama toma como mucho lo que le falta a su avance y lo demás pasa a las otras; si ninguna investiga nada, se guarda. Al completar un avance lo aprende, deja la rama sin investigación y avisa para elegir el siguiente. |
| `IsLevelOpen(jugador, rama, nivel)` | El nivel 1 siempre; los demás, cuando se conoce la mitad del anterior. |
| `CanResearch(jugador, avance)` / `Research(...)` | Comprueba (no conocido, nivel abierto y con sus requisitos) / pone la ciencia de su rama en ese avance, en lugar del que investigaba (los puntos de los dos se quedan); la ciencia guardada entra en él de inmediato. |
| `MissingRequirements(jugador, avance)`, `RequirementList(...)` | Avances que le faltan / su lista en texto. |
| `NeighbourNations(jugador)`, `ResearchCost(jugador, avance)` | Naciones con provincias junto a las suyas / lo que le cuesta un avance (sus puntos por `ResearchCostMultiplier`): un 10 % menos por cada vecina que ya lo conoce, hasta 3 (`NeighbourResearchDiscount`, `MaxNeighbourDiscounts`). |
| `SetResearchPriority(jugador, rama, prioridad)` | Cambia la prioridad de una rama (0 a `MaxResearchPriority`). |
| `IsBuildingAvailable(provincia, tipo, constructor)` | ¿Podría construirse aquí algún día? Tiene dueño, se conoce su avance (el del constructor, si se da) y hay ciudad, yacimiento conocido, costa u otro edificio si los necesita, y no lo sustituye ya otro mejor (el taller, la fábrica) (sin mirar coste ni obras). |
| `CanBuild(jugador, provincia, tipo)` / `Build(...)` | Comprueba (es tuya, no está construido, está disponible, no hay otra obra, tiene al menos 10 habitantes y puedes pagarlo) / paga y empieza la obra. `BuildDays(jugador, días)`: lo que dura una obra, menos con los avances que aceleran las obras. |
| `DailyConstruction(jugador)`, `UpgradeBuildings(jugador)`, `FinishedWorks`, `FinishedWork` | Cada obra avanza un día; al terminar, el edificio empieza a funcionar (o se funda la ciudad), avisa al jugador y queda en `FinishedWorks` durante `RecentWorkDays` (3) días, sin guardarse, para la alerta de obras terminadas. Antes, los edificios que el jugador ya sabe mejorar pasan a serlo (los talleres, a fábricas con la Industrialización; también los de provincias conquistadas) y se le avisa; un taller en obras se termina como fábrica. |
| `EngineersIn(jugador, provincia)` | Batallones de ingenieros del jugador que están en la provincia sin moverse, atacar ni ir embarcados: los que construyen carreteras y ferrocarriles. |
| `IsCitySite(jugador, provincia)` / `CanBuildCity(...)` / `BuildCity(jugador, provincia, nombre)` | ¿Pueden sus habitantes levantar una ciudad (es tuya, sin ciudad ni otra obra, ninguna ciudad hecha o en obras al lado, al menos 500 habitantes)? / lo mismo y además puedes pagarla / paga y empieza la obra, que ocupa la provincia como un edificio (`PlannedCityName`). |
| `CheckCityName(nombre)`, `SuggestCityName()` | El nombre no puede estar vacío, pasar de 24 letras ni repetir el de otra ciudad (hecha o en obras) / propone uno libre para el jugador. |
| `PlaceName(provincia)` | Cómo llamar a un lugar en los mensajes: su ciudad si la tiene, si no la provincia. |
| `DailyMigration(jugador)` | Cada ciudad no ocupada envía parte de su gente a las provincias propias poco pobladas; primero las vacías y las cercanas. Guarda las fracciones de persona para el día siguiente. |
| `CanFoundCity(unidad)` / `FoundCity(jugador, unidad, nombre)` | Comprueba / funda una ciudad con colonos: reclama la provincia, crea la ciudad con ese nombre (o uno al azar) y los colonos pasan a ser su población. `AddCity` la crea, la hace capital si es la primera y avisa. |
| `CanClaim(unidad)` / `Claim(...)` | Comprueba / reclama con una unidad militar que lleve exploradores la provincia libre en la que está (y avisa del nombre que recibe). |
| `SetOwner(...)`, `NameProvince(provincia)` | Cambia el dueño de una provincia; la primera nación que la reclama le pone nombre (`ProvinceNames.Next`, sin repetir), que se queda aunque cambie de manos. |
| `CanRecruitSettlers(ciudad)` / `RecruitSettlers(...)` | Comprueba / envía colonos desde una ciudad, pagando recursos y habitantes. |
| `Disband(...)` | La unidad se disuelve: sus ciudadanos pasan a vivir en la provincia (propia) donde está; una flota en puerto desembarca antes lo que lleva. |
| `CanHoldFestival(ciudad)` / `HoldFestival(...)` | Comprueba / paga unas fiestas que suben la moral de la ciudad durante 30 días (una a la vez). |
| `CanForceMigration(...)` / `ForceMigration(...)` | Comprueba / envía un número elegido de ciudadanos entre dos provincias propias pagando oro; viajan con la moral de su origen menos 20. |
| `Settle(provincia, personas, moral)` | Añade gente a una provincia mezclando su moral con la de los residentes según cuántos son (migrantes, colonos al fundar, unidades que se asientan). |
| `SetOwner(provincia, jugador)` | Cambia el dueño (y el controlador) de una provincia y avisa al cliente (`OwnershipChanged`). Si cambia de dueño, la asimilación y la rebelión vuelven a cero; la tierra vacía toma la cultura del nuevo dueño y la gente conserva la suya. |
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
| `MoveUnit(...)`, `UnitStepHours(...)` | Orden de mover (la ruta evita tierras vedadas; el tiempo depende de la velocidad de la unidad y, en tierra, de la nieve y el barro: `SeasonSlowdown`). Una unidad embarcada desembarca; una en tierra que apunta a una flota suya en el mar de al lado embarca. |
| `MoveUnits()` | Cada hora cada unidad avanza; un regimiento que entra donde hay tropas enemigas ataca, y si no las hay la ocupa (`EnterProvince`), o la sitia si tiene murallas o castillo (`LaySiege`); una flota lleva su carga consigo y se detiene a combatir si encuentra barcos enemigos. |
| `Occupy(provincia, jugador)` | La provincia pasa a manos del jugador (o vuelve a su dueño) y los civiles, cuarteles y flotas enemigos huyen (los embarcados, con su flota). |
| `CanTrainIn`, `CanTrain`/`Train`, `CanTrainTemplate`/`TrainTemplate`, `CanRaiseHeadquarters`/`RaiseHeadquarters`, `CanRaiseTroops(...)`, `MinimumPopulation` | Pagan y ponen en instrucción, en una provincia con ciudad, cuartel o taller (`CanTrainIn`; se pasan ids de provincia), un batallón, un barco (solo en ciudades con puerto, y los más avanzados con dique seco; forma una flota nueva) o un cuartel general; los hombres salen de la provincia, que conserva el mínimo de una ciudad o, sin ciudad, el de una provincia poblada. `CanRaiseTroops` comprueba ciudad, cuartel o taller, avances, los edificios que piden los batallones (cuartel para las tropas de combate, taller o fábrica para las máquinas de guerra), ocupación, habitantes y coste (solo el oro). Un batallón necesita además el equipo de su modelo en el almacén: se entrena con el más moderno del que haya (`StockedModel`, `TrainedModel`; en una plantilla, `TemplateModels`, para todos los de cada línea a la vez), se lleva sus piezas y queda apuntado en la orden; sin ninguno, «Falta equipo». |
| `ModelFor(jugador, tipo)`, `NewBattalion(...)`, `AddTrained(...)`, `Modernise(jugador)` | El modelo más moderno de una línea que conoce la nación; los batallones nuevos de los tests nacen con él, y los entrenados con el de su equipo. Cada día (`DailyMilitary`, tras el suministro) los batallones con suministro y sin combatir adoptan el más moderno de su línea si hay en el almacén las piezas para sus hombres, y devuelven las viejas. Los refuerzos también gastan piezas de su modelo: sin ellas no se refuerza. |
| `TrainingSpeed(jugador, tipo)`, `TrainingDays(jugador, tipo o plantilla)` | Cuánto más rápido entrena el jugador una línea (un 25 % por cada avance que la estudia) y los días que le cuesta su modelo; una plantilla tarda lo que su batallón más lento. |
| `TemplateById`, `AddTemplate(...)` | Busca una plantilla del jugador y crea una nueva con el siguiente número (también la usan la IA y los tests). |
| `CreateTemplate`, `DuplicateTemplate`, `DeleteTemplate` | Plantilla nueva (con un batallón de infantería ligera), copia de otra, o borrarla (siempre queda al menos una). |
| `RenameTemplate(jugador, plantilla, nombre)` | Le da un nombre propio (`Template.CustomName`, se guarda en la partida; como mucho `MaxUnitNameLength` letras y sin repetir el de otra plantilla suya); vacío, vuelve a «Plantilla» y su número. |
| `CanAddToTemplate`/`AddToTemplate`, `RemoveFromTemplate` | Añaden un batallón conocido (hasta 6; nunca barcos) o quitan uno (queda al menos uno). |
| `CanTrainTemplate`/`TrainTemplate(...)` | Pagan todos los batallones de una plantilla a la vez; se instruyen juntos y forman un solo regimiento. |
| `DailyTraining(jugador)` | Las órdenes de instrucción de sus provincias avanzan (no mientras están ocupadas); al terminar aparece el regimiento (con ese batallón o con los de la plantilla), la flota o el cuartel general en la provincia. |
| `Organise(unidad, batallones)`, `NextNumber(...)` | Reparte batallones en regimientos de hasta 5: uno es un regimiento; hasta 4, una brigada; más, una división de brigadas. Cada formación nueva toma el siguiente número de su tamaño (regimientos, brigadas y divisiones se numeran aparte). `AddRegiment` lo usa, y también las partidas de 1.101.0, que guardaban los batallones en una sola lista. |
| `CanMerge`/`Merge`, `Split` | Unen los batallones de dos regimientos de la misma provincia (hasta 5) o los barcos de dos flotas (hasta 10, con su carga), o separan uno o varios batallones (hasta 5, como un regimiento nuevo) o barcos juntos en una unidad nueva, sin oficial (un barco no se separa si la carga no cabría en el resto); los regimientos y brigadas que se quedan sin nada desaparecen. Al unir, la unidad conserva su oficial; el de la otra toma el mando si no tenía, o vuelve a la reserva (`Absorb`). |
| `CanIncorporate`/`Incorporate` | Mete una unidad de combate en otra como parte suya, conservando su número y su nombre dentro: dos regimientos forman una brigada; un regimiento entra en una brigada, o forma una división con ella si ya tiene cuatro; dos brigadas forman una división; una división acoge regimientos y brigadas hasta 5 partes y 10.000 hombres. Queda la de mayor tamaño (o esta, si son iguales), que al crecer toma el siguiente número de su nuevo tamaño y pierde el nombre que le dio el jugador (que se queda en la parte de dentro). Su oficial asciende al rango que corresponde. Una división no cabe en otra. |
| `PartsOf(unidad)`, `CanDetach`/`Detach(...)` | Las partes de una brigada o división (las brigadas de una división y luego sus regimientos; los regimientos de una brigada) y sacar una como unidad propia en la misma provincia, con su número y su nombre y sin oficial. Debe quedarle al menos una. |
| `RenameUnit(...)` | Da a una unidad el nombre que elige el jugador (30 letras como mucho); uno vacío le devuelve el automático. |
| `CanRecruitOfficer`/`RecruitOfficer`, `NewOfficer(...)` | Reclutar un oficial de un arma (40 de oro) con rasgos al azar para la reserva, sin repetir el nombre de otro oficial de la nación. Los de marina piden una flota o un puerto; los de aviación, la Aviación. |
| `AssignOfficer`, `CanLead`, `RelieveOfficer`, `RetireOfficer`, `Promote(unidad)` | Poner al mando de una unidad de combate, un cuartel o una flota a un oficial de la reserva de su arma (`Unit.OfficerBranch`) (el anterior vuelve a ella), relevarlo o retirarlo para siempre. Un oficial asciende solo al rango que pide el tamaño de su unidad, y nunca baja. |
| `CanAttach`/`Attach`, `Detach` | Ponen una unidad bajo el mando de un cuartel del nivel superior (5 como mucho) o la quitan. |
| `InCommandRange(unidad)`, `CommandBonus(unidad)` | Si su cuartel la alcanza, y la bonificación de toda la cadena enlazada. |
| `ComputeSupply(jugador)`, `IsInSupply(unidad)`, `IsSupplied(jugador, provincia)` | Los sitiadores también reciben suministro si la provincia sitiada linda con una abastecida. Provincias abastecidas: todo lo que sus carreteras y ferrocarriles unen a sus ciudades (`SupplyNetwork`), y desde ahí hasta 10 días de marcha por tierra propia o libre (nunca por mar), y una más allá (el frente). Se recalcula cada día y al terminar un tramo. |
| `InBattle(unidad)` | Si ataca o defiende. |
| `DailyMilitary(jugador)` | Cada día: instrucción, suministro, desgaste por frío, desierto o altura (`DailyAttrition`: la unidad que se queda sin hombres muere de frío o de sed), recuperación de organización, envíos de la capital (`DailyShipments`; las flotas completan su tripulación en puerto directamente desde la capital), desgaste sin suministro (la unidad que se queda sin hombres se dispersa) y, si no se pagó el mantenimiento, pérdida de organización y deserciones sin recuperación; los oficiales y generales organizadores aceleran la recuperación y los desorganizados la frenan. Las tropas embarcadas ni se desgastan ni se recuperan; las flotas solo se reparan y completan su tripulación en puerto, el doble de rápido con dique seco. |
| `StartAttack(...)`, `CancelAttack(...)` | El regimiento se detiene en la frontera y ataca (se une a la batalla o la abre), o la abandona. |
| `ResolveBattles()` | Una hora de cada batalla: fuego de ambos bandos, daño, retiradas y abandonos; si no quedan defensores, los atacantes entran. |
| `Engage(unidades, provincia, ataca, ingenierosEnemigos)`, `Engaged` | Los batallones de un bando que combaten esta hora: los mejores que caben en el frente (`FrontWidth`) y, detrás, hasta la mitad de artillería, aviación e ingenieros (exposición 0,25); el resto espera en reserva, y los médicos no combaten. Fuego de cada uno: ataque o defensa, hombres, organización, experiencia, mando, oficial propio y general, suministro, terreno (menos si el atacante lleva ingenieros; las tropas de montaña, ×1,5 en terreno difícil) y murallas. |
| `FaceEachOther(atacantes, defensores)`, `HasMedics(unidad)` | La antiaérea de cada bando contra los aviones del otro: con aviones enemigos dispara ×2, y cada batallón suyo les quita un 25 % de fuego (hasta un 75 %). Si una unidad lleva médicos (con hombres), pierde un 30 % menos de hombres en combate. |
| `SideFire(...)`, `ExpectedFire(...)`, `CombinedArms(papeles)` | Fuego de un bando: la suma de sus batallones, más las armas combinadas (+10 % por papel distinto sin contar a los médicos, hasta +30 %) y azar (`ExpectedFire`, sin azar, para la ventana de la batalla). |
| `Damage(...)`, `Broken(...)`, `AverageOrganisation(...)`, `GeneralOf(unidad)` | Reparto del daño entre los batallones que combaten según su exposición (devuelve los hombres perdidos, que se suman a las bajas de la batalla) (los oficiales tácticos ahorran organización y los indecisos la gastan; en el mar, entre todos los barcos); cuándo se rompe una unidad; el general del cuartel propio si está a su alcance. Cada hora de combate da experiencia, y cada victoria suma una a los oficiales y generales de los vencedores. |
| `DefenseMultiplier(provincia, ingenieros)`, `HasEngineers(unidades)` | Cuánto más daño hace quien defiende: el terreno (rebajado si el atacante lleva ingenieros) por su muralla o castillo; si alguna de las unidades lleva ingenieros, combatan o no. |
| `EndBattle(...)`, `Retreat(...)`, `Destroy(...)` | Final de la batalla y avisos; retirada a una provincia vecina sin enemigos (o destrucción si está rodeada); una unidad destruida en combate pierde a su oficial, y una que se dispersa sin pago o sin suministro lo devuelve a la reserva. |

### `Simulation/GameSession.Manpower.cs`
La reserva de reclutas.

| Elemento | Qué hace |
| --- | --- |
| `ManpowerCapacity(jugador)`, `ManpowerPerDay(jugador)` | Lo que cabe en la reserva: 500 más el 6 % de la gente de sus provincias no ocupadas, más con los avances (`Modifiers.Manpower`: Instrucción militar +25 %, Servicio militar obligatorio +50 %); se rellena de vacía a llena en 2 años. |
| `DailyManpower(jugador)` | Cada día (al empezar `DailyMilitary`) la rellena, sin pasar de lo que cabe. |
| `LacksManpower(jugador, hombres)`, `ReturnToReserve(jugador, hombres)` | Por qué no hay hombres para entrenar o formar un cuartel general (`CanRaiseTroops`, `CanRaiseHeadquarters`); licenciar una unidad (`Disband`) devuelve sus hombres. Los refuerzos de `DailyMilitary` también la gastan. |

### `Simulation/GameSession.Production.cs`
El equipo, como en Hearts of Iron.

| Elemento | Qué hace |
| --- | --- |
| `HasWorkshop(provincia)`, `ProducibleModels(jugador)` | Si tiene taller o fábrica; los modelos de los que la nación puede fabricar equipo: el más moderno de cada línea (los barcos no), y los suministros una vez. |
| `ProductionRate(provincia, modelo)`, `ProductionOf(provincia)` | Piezas al día: las de un batallón en sus días de instrucción, el doble en una fábrica (`FactoryOutput`); lo que fabrica ahora. |
| `CanProduce`, `SetProduction(...)` | Elegir (o parar) lo que fabrica un taller: hace falta taller o fábrica, no estar ocupada y conocer el modelo. |
| `DailyProduction(jugador)` | Cada día, antes de `DailyMilitary`, cada taller hace sus piezas y gasta lo que cuestan (su parte del coste del modelo menos el oro, también en `LastDayNet` y en el flujo de talleres); si el almacén no alcanza, hace solo la parte que puede pagar. |
| `StockedModel(jugador, tipo, n)` | El modelo más moderno de la línea del que hay equipo para n batallones; nulo si no hay. |

### `Military/Aircraft.cs` y `Simulation/GameSession.Air.cs`
La aviación: la unidad básica es la escuadrilla de 6 aviones (un `Battalion` de una línea del grupo Aviación: sus piezas son los aviones y sus hombres las tripulaciones), y las escuadrillas de un tipo se unen en unidades aéreas (`AirUnit`) que no son fichas del mapa.

| Elemento | Qué hace |
| --- | --- |
| Líneas de aviones | Cazas (`Fighters`, 1 hombre por avión), bombarderos en picado (`CloseSupport`, 2), bombarderos tácticos (`TacticalBombers`, 4), estratégicos (`Bombers`, 10), aviación naval (`NavalBombers`, 2) y transportes (`AirTransports`, 4), con un modelo con Aviación, otro con Radar y otro con Motor a reacción. Cifras por escuadrilla: `Attack` en tierra y mar, `AirAttack` contra aviones, `RangeKm`, `Capacity` (paracaidistas). Los aviones los hacen los talleres; se forman en un aeródromo (`TrainingBuilding`). |
| `AirEchelon`, `AirEchelons` | Escuadrilla (1), escuadrón (hasta 3 escuadrillas), grupo (hasta 18: 6 escuadrones) y ala (hasta 54: 3 grupos); `Numbered` da «1.ª Escuadrilla», «1.er Escuadrón». |
| `AirUnit` | Sus escuadrillas (`Flights`, de un solo tipo), número, tamaño (`Size`), base (aeródromo o portaaviones), misión y División aérea (`CommanderId`); aviones, tripulaciones, fuerza y organización medias. |
| `AirHeadquarters` | División aérea (nivel 1) o Mando aéreo (nivel 2, uno por nación), en un aeródromo y con un general de aviación. |
| `AirUnits`, `AirUnitById`, `AirUnitsAt`, `AirUnitsOn`, `BaseOf` | Las unidades aéreas y dónde están. |
| `HasNavy(jugador)`, `HasAirForce(jugador)` | Si la nación tiene marina (conoce el avance del puerto, Navegación a vela, o tiene barcos) o aviación (conoce Aviación o tiene unidades aéreas): muestran sus pestañas. Al descubrir el avance que las abre, el jugador recibe un aviso que lo explica. |
| `AirfieldRoom`, `CarrierRoom`, `AddAirUnit` | Escuadrillas que caben aún en un aeródromo (18, contando las que se forman) y en los portaaviones de una flota (6 cada uno); `Train` de un avión forma una escuadrilla (`DailyTraining`). |
| `CanJoin`, `JoinAirUnits`, `SplitAirUnit`, `Renumber` | Unir unidades del mismo tipo y base hasta un ala, o sacar escuadrillas como unidad propia; al cambiar de tamaño toman número nuevo. |
| `Rebase`, `InRange`, `DisbandAirUnit` | Cambiar de base a menos del doble de su alcance (a un portaaviones solo cazas, en picado y naval); disolverla devuelve tripulaciones y aviones. |
| `CanRaiseAirHeadquarters`, `RaiseAirHeadquarters`, `AttachAirUnit`, `DisbandAirHeadquarters` | Formar una División aérea o el Mando aéreo en un aeródromo (oro y 30 hombres), poner una unidad bajo una división (hasta 6) y disolverlos. |
| `InAirCommand`, `AirCommandBonus` | Una división manda las unidades con base a menos de 1.500 km: +10 % y la habilidad de su general (`NavalFireBonus`); el Mando aéreo, a menos de 4.000 km de la división, +5 % más. |
| `DailyAirUnits(jugador)`, `Relocate`, `LoseCarrierAirUnits` | Cada día: una unidad sin base vuela al aeródromo más cercano con sitio o se pierde (y un cuartel sin aeródromo se traslada o se disuelve); en su base con suministro cada escuadrilla recupera organización y repone aviones y tripulaciones. Las de un portaaviones hundido caen con él. Cuestan mantenimiento. |
| `TurnFlyingBattalionsIntoAirUnits()` | Al cargar, cada batallón de aviones de un regimiento pasa a ser una escuadrilla en el aeródromo más cercano (la capital recibe uno si no hay). Las alas de 10 aviones de 1.114–1.116 (`WingSave`) se cargan como escuadrones de 2 escuadrillas. |
| `Province.Damage`, `DamageOf`, `SetDamage`, `WorkshopDamage` | El daño de cada edificio (0–1): da esa parte menos de sus efectos (`Modifiers.Times`) y el taller fabrica menos. |

### `Simulation/GameSession.AirWar.cs`
La guerra en el aire: cada ala vuela su misión sobre las provincias a menos de 300 km de su objetivo (`MissionRadiusKm`).

| Elemento | Qué hace |
| --- | --- |
| `MissionsFor(tipo)`, `SetAirMission`, `AirMissionName`, `AirMissionDescription` | Cada tipo vuela sus misiones (cazas superioridad, en picado apoyo cercano, tácticos apoyo cercano o bombardeo, estratégicos bombardeo, aviación naval ataque naval, transportes paracaidistas) sobre una provincia a su alcance, o ninguna. |
| `IsFlying`, `Covers`, `Effectiveness` | Vuela si tiene misión a su alcance, aviones y al menos el 20 % de organización; lo que hace va con sus aviones, su organización y su experiencia. |
| `FighterCover`, `AirSuperiority`, `EnemyRulesTheAir`, `AirSuperiorityMultiplier` | Los cazas propios y enemigos sobre una provincia; la parte propia (nula sin cazas); el enemigo domina con el 60 %. En `Engage` las tropas luchan hasta un 15 % mejor o peor; en `UnitStepHours` marchan 1,5 veces más despacio bajo un cielo enemigo, y en `DailyShipments` sus envíos tardan 1,5 veces más y un 25 % vuelve. |
| `CloseAirSupport(jugador, provincia)` | El fuego por hora del apoyo cercano sobre la provincia, que `ResolveBattles` suma al de su bando (la mitad bajo un cielo enemigo). |
| `AirFire(unidad, cifra)`, `DailyAir()`, `RepairBuildings` | El fuego de una unidad es el de sus escuadrillas (aviones, organización, experiencia) por su bonus de mando. Cada día, tras las incursiones: las unidades enemigas cuyas zonas se tocan se disparan (los cazas con todo, las demás a la mitad) y la antiaérea enemiga junto al objetivo dispara a las que atacan; cada escuadrilla pierde aviones y organización (`AirEvasion`) y gana experiencia; luego los bombarderos bombardean (`Bomb`: moral y daño a un edificio al azar, que se derrumba al llegar a 1) y la aviación naval ataca (`StrikeAtSea`: flotas y convoyes con `SinkConvoys`); los edificios no bombardeados se reparan un 2 %. Anota los aviones perdidos y derribados (`PlanesLostLastDay`, `PlanesDownedLastDay`). |
| `CanParadrop`, `Paradrop` | Una unidad solo de paracaidistas en un aeródromo con transportes que la lleven y alcancen el objetivo salta sobre una provincia sin tropas enemigas: llega con la mitad de su organización (y pierde hombres bajo un cielo enemigo) y ocupa la provincia si es enemiga; los transportes vuelven con la mitad de la suya. |

### `Simulation/GameSession.Convoys.cs`
La guerra en el mar más allá de las batallas: los convoyes y las misiones de las flotas (`Unit.Mission`, de `FleetMission`).

| Elemento | Qué hace |
| --- | --- |
| `Player.Convoys`, `ConvoysInUse`, `FreeConvoys`, `CanOrderConvoys`, `OrderConvoys` | Los convoyes de la nación, los que van en envíos y los libres; encargar un lote (`ShipOrder.Convoys`, `Battalions.ConvoyBatch`) a los astilleros, que al terminarlo suma 5 convoyes y no forma flota. |
| `SetFleetMission`, `MissionName`, `MissionDescription`, `MissionSeas` | La misión de una flota, que cumple donde está y en los mares vecinos: patrullar, escoltar convoyes, atacar convoyes o bloquear puertos. |
| `DailyConvoyRaids()` | Cada día, tras `DailyMilitary`: los envíos en el mar cuyos mares de ruta alcanzan flotas enemigas que atacan convoyes pierden parte de sus convoyes y carga (`RaidFire`: los submarinos el doble), menos con flotas propias escoltando esos mares; anota `ConvoysLostLastDay` y `ConvoysSunkLastDay` y avisa al jugador. |
| `Patrol()` | Cada hora, antes de mover las unidades: las flotas que patrullan, quietas y sin combate, salen a por la flota enemiga más débil de un mar vecino. |
| `BlockadersOf(puerto)`, `IsBlockaded(puerto)`, `Blockading(flota)` | Un puerto enemigo junto a una flota en guerra que bloquea está bloqueado (se calcula una vez por hora): no construye barcos (`CanBuildShipIn`), no embarca envíos y `BonusesOf` le quita los impuestos del puerto. |

### `Military/Fleets.cs` y `Simulation/GameSession.Fleets.cs`
La jerarquía naval y el combustible.

| Elemento | Qué hace |
| --- | --- |
| `NavalEchelon`, `NavalEchelons` | Flotilla (hasta 3 buques), escuadra (hasta 9: 3 flotillas) y fuerza (hasta 27: 3 escuadras): el tamaño de una agrupación (una ficha), que da su nombre («1.ª Escuadra») y el rango de su oficial; `RenumberFleet` le da número nuevo al cambiar de tamaño al unir o separar. |
| `NavalHeadquarters`, `RaiseNavalHeadquarters`, `AttachFleet`, `DisbandNavalHeadquarters` | Flota (nivel 1) y Armada (nivel 2, una por nación), en un puerto con un almirante: oro y 50 hombres. Una Flota manda hasta 5 agrupaciones (`Unit.FleetCommanderId`). |
| `InFleetCommand`, `FleetCommandBonus` | La Flota manda las agrupaciones a menos de 2.000 km de su puerto: +10 % de fuego (en `NavalFire`) y la habilidad de su almirante; la Armada, con la Flota a menos de 6.000 km, +5 % más. |
| `DailyFleets(jugador)` | Cada día cada agrupación en el mar quema el carbón (vapor) o el petróleo (modernos) de sus barcos (`BattalionInfo.Fuel`, `FuelPerDay`); si no hay, queda sin combustible (`Unit.OutOfFuel`): navega 4 veces más despacio (`UnitStepHours`) y lucha a la mitad. En puerto repostan. Un cuartel naval sin puerto se traslada o se disuelve. |
| `FuelAircraft()` | Al empezar `DailyAir`, cada unidad aérea con misión quema 1 de petróleo por escuadrilla; sin él se queda en tierra ese día (`AirUnit.Grounded`). Las tripulaciones aéreas comen con la nación (`DailyEconomy`). |
| `SaveGame.Corvettes` | Las partidas anteriores a 1.118.0 no tienen la corbeta: al cargarlas, fragatas, cruceros y destructores suben un puesto en su línea. |

### `Simulation/GameSession.Shipyards.cs`
Los astilleros, como en Hearts of Iron: la nación encarga barcos a una cola (`Player.ShipOrders`, de `ShipOrder`: línea, modelo, puerto preferido, puerto que lo construye, días de trabajo hechos y si espera tripulación).

| Elemento | Qué hace |
| --- | --- |
| `Slips(puerto, jugador)`, `Shipyards(jugador)` | Gradas de un puerto: una, y otra más con dique seco; los puertos de la nación con las suyas. |
| `CanBuildShipIn`, `CanOrderShip`, `OrderShip(jugador, línea, puerto)` | Encargar el modelo más nuevo de una línea al final de la cola, sin pagar nada; hace falta conocerlo y un puerto con su astillero. `Train` de un barco lo encarga con ese puerto como preferido. |
| `MoveShipOrder`, `CancelShipOrder` | Subir o bajar un encargo en la cola, o cancelarlo (lo gastado se pierde). |
| `ShipWorkPerDay`, `DaysLeft` | Días de trabajo que hace una grada al día (uno, más con los avances que estudian la línea) y los que le quedan a un encargo. |
| `DailyShipyards(jugador)`, `Launch` | Cada día (en `DailyMilitary`): los encargos cuyo puerto ya no puede construirlos sueltan su grada (conservan el trabajo); las gradas libres toman los primeros de la cola que pueden construir (antes el puerto preferido); cada grada trabaja un día pagando su parte del coste (o lo que alcance, en el flujo de talleres); el barco terminado toma su tripulación del puerto y de la reserva y sale como flota nueva, o espera avisando una vez. |

### `Simulation/GameSession.Logistics.cs`
La logística, como en Hearts of Iron: el almacén está en la capital y lo que necesitan las tropas les llega en envíos.

| Elemento | Qué hace |
| --- | --- |
| `Shipment`, `Shipments`, `ShipmentsTo(unidad)` | Un envío de la capital a una unidad de combate: reclutas (`Men`), piezas de equipo por `SupplyKey` (`Pieces`), munición (`Ammo`) y la hora a la que llega (`ArriveHours`). |
| `DailyShipments(jugador)` | Cada día, en `DailyMilitary` tras el suministro, la capital envía a cada unidad lo que le falta, menos lo que ya va en camino: reclutas para un día de refuerzos con su equipo (gastando reserva y gente de la capital), las piezas del modelo nuevo para modernizarla y la munición gastada. Sirve primero a las unidades de los cuarteles de prioridad alta, luego normal y baja, y al final a las que no tienen cuartel a su alcance; dentro de cada grupo, a las más cercanas. Tarda lo que se tarda desde la capital por tierra propia o libre (más rápido por carreteras) y por el mar que la nación navega, embarcando solo en sus puertos no bloqueados (`SupplyRoutes`, `SeaLegs`); el doble sin cuartel. Por mar necesita convoyes libres para su carga (`Cargo`): lo que no cabe vuelve (`Unload`) y cuenta en `Player.CargoLeftForWantOfConvoys`. |
| `ShipmentTerms(unidad)`, `NoShipmentsReason(unidad)` | Su puesto en la cola y cuánto más tardan sus envíos; nulo (y el porqué) si no le llega nada: embarcada, sin suministro o con su cuartel aislado (sin suministro). Sin capital no sale nada. |
| `ArriveShipments()`, `Deliver`, `Return` | Cada hora, los envíos que llegan: si la unidad sigue con suministro, recibe la munición, luego moderniza los batallones con las piezas que trae (las viejas vuelven al almacén) y luego los reclutas con su equipo; lo que sobra, o todo si la unidad ya no existe o está aislada, vuelve al almacén, a la reserva y a la capital. |
| `ModerniseInPlace(jugador)` | Los batallones cuyo modelo nuevo usa las mismas armas lo adoptan al momento, sin envío. |
| `AmmoCapacity`, `Ammo`, `AmmoPerHour`, `AmmoEfficiency`, `SpendAmmo` | La munición de una unidad (`Unit.AmmoSpent` es lo gastado): un día de combate de sus batallones salvo los médicos; lo que gasta un batallón por hora; la parte del fuego que conserva (del 100 % al 50 % sin munición, en `Engage`); `ResolveBattles` la gasta cada hora por cada batallón que combate. |
| `SetSupplyPriority(jugador, cuartel, prioridad)`, `PriorityName` | La prioridad de suministro de un cuartel general (`Unit.SupplyPriority`: alta, normal o baja). |

### `Simulation/GameSession.Vision.cs`
La niebla de guerra.

| Elemento | Qué hace |
| --- | --- |
| `SharesVision(jugador, otro)` | Las naciones que comparten lo que ven: la misma, sus aliados, su señor y sus vasallos. |
| `VisibleProvinces(jugador)` | Lo que ve: las provincias suyas o que ocupa (y las de quienes comparten su vista) con una provincia alrededor, y alrededor de sus unidades una provincia (dos con exploradores) y la siguiente de su ruta. Se calcula de nuevo cada hora o cuando aparecen o desaparecen unidades, y lo que ve se añade a lo explorado (`Player.Explored`, que se guarda con la partida). `Step` lo calcula cada hora para el jugador, así que explora aunque el tiempo corra deprisa. |
| `HasExplored(jugador, provincia)` | Si la ha visto alguna vez; lo que no, el jugador no lo conoce. |
| `CanSee(jugador, unidad)` | Si ve una unidad: las propias y de quienes comparten su vista siempre; las demás solo en una provincia que ve (las embarcadas, si ve su flota). |

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
| `ResolveNavalBattles()`, `NavalFire(flota)`, `ExpectedNavalFire(flota)` | Cada hora, en cada mar con enemigos, cada nación dispara con sus barcos (ataque por tripulación y organización, con algo de azar) contra todos los barcos enemigos, calculando todo antes de aplicarlo. El oficial de la flota suma su `NavalFireBonus` (la media de su bonificación al atacar y al defender) y gana una victoria cuando su bando rompe una flota enemiga. |
| `CanRefit(flota, barco, modelo)`, `RefitCost(modelo)`, `RefitFleets(jugador)` | Cada día (en `DailyMilitary`), las flotas en uno de sus puertos y sin combatir pasan sus barcos al modelo más nuevo de su línea, pagando el 50 % de su coste (`RefitCostShare`, en el flujo de mantenimiento); hace falta el dique seco si el modelo lo pide y sitio para lo que llevan. Avisa al jugador de qué barcos se modernizaron. |
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
`SaveGame`: la partida guardada como datos, escrita en JSON comprimido con gzip. El mapa no se guarda: se vuelve a generar a partir de `World`. `Write(flujo)` la escribe; `Read(flujo)` la lee y lanza `InvalidDataException` si está dañada o su formato (`Format`) es de otra versión. `InstitutionBirths` guarda dónde y cuándo nació cada institución (vacío en partidas anteriores). `Roads` (`RoadLinkSave`), `RoadProjects` (`RoadProjectSave`) y `NextRoadProjectId` guardan la red y sus obras; si falta `Roads` (antes de la 1.36.0), al cargar se unen las provincias vecinas que tenían los dos el edificio calzada o ferrocarril (`LoadRoads`). `RealisticPopulation` marca las partidas guardadas desde la 1.63.0: al cargar una anterior, cada provincia se queda con la gente que su tierra alimenta. `ExtraDeposits` marca las partidas guardadas desde la 1.13.0: al cargar una anterior, los yacimientos que su generador no ponía empiezan llenos. `Barracks` marca las guardadas desde la 1.44.0: al cargar una anterior, cada ciudad recibe un cuartel, porque antes todas entrenaban tropas. `Workshops` marca las guardadas desde la 1.45.0: al cargar una anterior, cada provincia con cuartel cuyo dueño conoce la Maquinaria de asedio recibe un taller (una fábrica si ya conoce la Industrialización), porque antes el cuartel construía las máquinas de guerra. Desde la 1.44.1 lo que se entrena va en `ProvinceSave.Training`; el de `CitySave.Training` de partidas anteriores pasa a la provincia de la ciudad. Los registros `PlayerSave`, `ProvinceSave`, `CitySave`, `UnitSave` (con la flota que lleva a cada unidad, `CarrierId`, su oficial, `OfficerSave`, y el nombre que le dio el jugador), etc. son sus partes. Los oficiales en reserva van en `PlayerSave` y el siguiente número de oficial en `NextOfficerId`; `GeneralSave` es el general de las partidas anteriores a los oficiales (un solo rasgo), que al cargar pasa a ser un oficial del rango de su cuartel.

### `Simulation/GameSession.Diplomacy.cs`
Guerra, puntuación de guerra y tratados de paz.

| Elemento | Qué hace |
| --- | --- |
| `PeaceTerms` | Lo que firma quien propone la paz: `White` (paz blanca), `TakeOccupied` (se queda con las provincias enemigas que ocupa), `CedeOccupied` (entrega las suyas que ocupa el enemigo), `Reparations` (el enemigo le paga reparaciones) o `Vassalize` (el enemigo pasa a ser su vasallo). |
| `War` | Una guerra: cuándo empezó y cuántas batallas ha ganado cada bando (`Victories`). Se guarda en `WarSave`. |
| `AtWar(a, b)`, `EnemiesOf(jugador)`, `WarDays(a, b)`, `WarVictories(jugador, enemigo)` | Si dos naciones están en guerra, sus enemigos, cuánto dura la guerra y las batallas ganadas (en tierra y en el mar; las cuenta `RecordVictory`). |
| `CanDeclareWar`/`DeclareWar`, `StartWar`, `TruceDaysLeft(a, b)` | Declara la guerra a otra nación (no a un aliado, ni con un pacto, ni durante la tregua, ni siendo vasallo); `StartWar` la empieza y acaba con los pactos y el paso entre las dos; la víctima lo recuerda (`Remember`) y sus aliados entran en la guerra (`CallAllies`). La tregua: tras cada paz, 730 días (`GameRules.TruceDays`) sin guerra entre las dos. Las treguas se guardan en `TruceSave`. |
| `ProvinceValue(provincia)` | Lo que vale en la mesa de paz: 1, más 1 por cada 2.000 habitantes (hasta 5), más 2 si tiene ciudad o 6 si es la capital (`GameRules`). |
| `WarScore(jugador, enemigo)` | De -100 a 100: la parte del valor del enemigo que ocupa, menos la parte del suyo que le ocupan, más 2 por batalla ganada y menos 2 por perdida (hasta ±25). |
| `OccupiedBy(ocupante, dueño)`, `PeaceCost(jugador, enemigo, términos)` | Las provincias de una nación que ocupa otra, y lo que cuesta cada tratado: quedárselas, la parte del valor del enemigo que suponen; las reparaciones, 30; el vasallaje, 60. |
| `CanProposePeace`/`ProposePeace(jugador, otro, términos)` | Propone la paz. Quedarse con lo ocupado, las reparaciones y el vasallaje piden puntuación suficiente; un vasallo no puede tener vasallos. La IA decide con `AiPlayer.WouldAcceptPeace`; a un humano no se le impone nunca. |
| `MakePeace(a, b, términos)` | Firma la paz: terminan las batallas, las provincias del tratado cambian de dueño (`Cede`), las demás ocupadas vuelven a sus dueños y los ejércitos regresan a su provincia más cercana. Las reparaciones y el vasallaje empiezan aquí, y las guerras de los vasallos terminan con las de su señor. Una nación que se queda sin tierras queda anexionada y sale de la partida (`Eliminate`). |
| `Cede(provincia, receptor)` | Pasa una provincia con su ciudad y edificios (y termina su asedio): se pierde lo que se entrenaba o construía allí, su moral baja 20 y, si era la capital, el antiguo dueño pasa la capital a su ciudad más poblada. |

### `Simulation/GameSession.Vassals.cs`
Reparaciones, vasallos y capitulación.

| Elemento | Qué hace |
| --- | --- |
| `Reparations` | Reparaciones de guerra: quién paga, a quién y hasta cuándo. Se guardan tal cual en `SaveGame.Reparations`. |
| `OverlordOf`, `IsVassalOf`, `VassalsOf`, `InVassalage(a, b)`, `VassalYears` | El vasallaje: el señor de cada vasallo y desde cuándo (`VassalSave`). Señor y vasallo cruzan las tierras del otro (`CanUnitEnter`). |
| `MakeVassal(vasallo, señor)` | Lo hace vasallo: sus vasallos quedan libres, deja sus alianzas y firma la paz con los aliados y vasallos de su señor. |
| `CallVassals(atacante, defensor)` | Al empezar una guerra entran en ella los vasallos del atacante, y el señor y los vasallos del defensor. |
| `DailyTributes()`, `DailyTribute(jugador)` | Cada día los vasallos pagan el 10 % de sus ingresos de oro y quienes deben reparaciones el 25 % (se descuenta de `LastDayNet`); las reparaciones vencidas terminan. `DailyTribute` da lo que una nación paga o cobra al día. |
| `CanAnnexVassal`/`AnnexVassal`, `ReleaseVassal` | A los 10 años de vasallaje, y sin guerras, el señor se queda con todo; liberarlo deja buen recuerdo (+40). |
| `DailyCapitulations()`, `Capitulate(nación)` | Una nación en guerra con todas sus ciudades ocupadas capitula: cada enemigo se queda lo que ocupa, quien tiene su capital (o el que más ocupa) se lleva el resto, y desaparece. |
| `Eliminate(nación)` | La saca de la partida (`Player.Eliminated`): terminan sus guerras, batallas, asedios, alianzas, vasallajes y reparaciones, y desaparecen sus unidades y migrantes. También al quedarse sin tierras en un tratado o al ser anexionada. |

### `Simulation/GameSession.Pacts.cs`
Pactos de no agresión y paso militar.

| Elemento | Qué hace |
| --- | --- |
| `HavePact(a, b)`, `CanProposePact`/`ProposePact`, `BreakPact` | Pacto de no agresión: ninguno de los dos puede declarar la guerra al otro (`CanDeclareWar`) y un aliado con un pacto con el atacante no entra en la guerra. La IA decide con `AiPlayer.WouldSignPact`. Romperlo deja una tregua de 365 días y un recuerdo de −30. Se guardan en `SaveGame.Pacts`. |
| `GivesAccess(quien da, a quien)`, `CanAskAccess`/`AskAccess`, `CanGrantAccess`/`GrantAccess`, `RevokeAccess` | Paso militar, en un sentido: los ejércitos de uno cruzan las tierras del otro. Pedirlo lo decide la IA con `AiPlayer.WouldGrantAccess`; darlo sube 10 la opinión del que lo recibe. Al retirarlo, sus tropas vuelven a casa (`Evict`). Se guarda en `AccessSave`. |
| `MayCross(jugador, dueño)` | Si los ejércitos de una nación pueden cruzar en paz las tierras de otra: aliados, señor y vasallo, o con paso (lo usa `CanUnitEnter`). |
| `CanAgree`, `Evict(invitado, anfitrión)`, `EndAgreements(a, b)` | Lo que piden todos estos acuerdos (no estar en guerra, ni ser vasallo); las tropas que ya no pueden estar en tierra ajena vuelven a casa (también al romper una alianza); la guerra (`StartWar`) acaba con los pactos y el paso entre los dos. |

### `Simulation/GameSession.Trade.cs`
Comercio de recursos.

| Elemento | Qué hace |
| --- | --- |
| `TradeDeal` | Un acuerdo: quién vende qué y cuánto al día, quién paga con qué y cuánto, y hasta cuándo. Se guardan tal cual en `SaveGame.Trades` (con `NextTradeId`). |
| `Surplus(nación, recurso)` | Lo que puede vender al día: la mitad de lo que ganó de ese recurso el último día (`GameRules.TradeSurplusShare`). |
| `TradeOffer(comprador, vendedor, bienes, pago)` | Lo que ofrecería el vendedor: su excedente, a su valor en oro (`GameRules.ResourceValue`) con el margen del lado de la IA (`GameRules.TradeMargin`, menor cuanto mejor ve al otro), recortado a lo que el comprador puede pagar. Nulo si vale menos de 1 de oro al día. |
| `CanTrade`/`Trade`, `CancelTrade` | Firma el acuerdo por 365 días (sin guerra, conociendo los dos recursos, uno por recurso y pareja, hasta 6 por nación, y si la IA ve al otro con al menos −25). Cancelarlo deja un recuerdo de −10. |
| `DailyTrade()`, `DailyTradeBalance(nación)` | Cada día se entregan los bienes y el pago (también en `LastDayNet`); los acuerdos vencidos, entre enemigos o que alguien no puede cumplir terminan. `DailyTradeBalance` da lo que una nación recibe y entrega al día. |

### `Simulation/GameSession.Relations.cs`
Opinión entre naciones, regalos y alianzas.

| Elemento | Qué hace |
| --- | --- |
| `OpinionMemory` | Algo que una nación recuerda de otra: la razón y cuánto pesa aún (se olvida `OpinionFadePerDay` al día). Se guardan en `MemorySave`. |
| `OpinionFactors(de, sobre)`, `Opinion(de, sobre)` | Lo que una nación piensa de otra (de -100 a 100): aliados (+30), en guerra (−50), frontera común (−10), enemigo común en guerra (+25), rival común (+20: las dos lindan con una nación más fuerte que la que opina), gobierna a su gente (−5 por provincia, hasta −30) y sus recuerdos: le declaró la guerra (−60), le quitó provincias (−5 cada una), rompió la alianza (−60), regalos (+15 cada uno). |
| `BorderNations(nación)` | Las naciones que lindan con ella, calculadas una vez al día. |
| `Remember(de, sobre, razón, valor)`, `DailyMemories()` | Añade un recuerdo (la misma razón se suma) y los va olvidando cada día. |
| `GiftCost(jugador)`, `SendGift(jugador, otro)` | Un regalo cuesta un mes de ingresos de oro (al menos 50) y sube 15 la opinión del que lo recibe. |
| `AreAllied`, `AlliesOf`, `CanProposeAlliance`/`ProposeAlliance`, `BreakAlliance` | Alianzas (hasta 3 por nación). La IA acepta si su opinión llega a 40 (`AiPlayer.WouldAlly`). Romperla la recuerda el antiguo aliado. Los aliados no pueden declararse la guerra y sus ejércitos cruzan las tierras del otro (`CanUnitEnter`). Se guardan en `AllianceSave`. |
| `CallAllies(atacante, defensor)` | Al declarar la guerra, los aliados del defensor entran en ella; uno aliado de los dos se queda fuera y deja de ser aliado del atacante. |

### `Simulation/GameSession.Culture.cs`
Culturas y rebeliones.

| Función | Qué hace |
| --- | --- |
| `CultureOf(provincia)`, `HasForeignCulture(provincia)` | La nación cuya cultura comparte su gente, y si no es la de su dueño. |
| `ForeignCultureMood(provincia)`, `DailyAssimilation(provincia)` | Moral que resta la cultura extranjera (25, menos cuanto más asimilada) y lo que avanza la asimilación cada día (unos 15 años a moral 50, más deprisa con moral alta). |
| `IsGarrisoned(provincia)`, `RevoltRisk(provincia)`, `WouldSecede(provincia)` | Si hay un regimiento de su dueño dentro, lo cerca que está de la rebelión (0 a 1) y si al sublevarse se uniría a la nación de su cultura (gente extranjera, esa nación aún tiene tierras y no es la capital del dueño). |
| `DailyUnrest(jugador)` | Cada día: la tierra vacía toma la cultura del dueño, la gente extranjera se asimila (al terminar adopta la del dueño), y las provincias por debajo de 25 de moral sin guarnición se acercan a la rebelión (1 a 2 días por día, según la moral); con moral alta se calman (2 por día). Avisa al llegar a la mitad. |
| `Revolt(provincia)` | La provincia se subleva: se une a la nación de su cultura (`Cede`, con moral al menos 60, y las tropas del antiguo dueño vuelven a casa si no están en guerra) o, si no puede, hay una revuelta: muere el 10 % de la gente, arde un edificio al azar y la moral sube al menos a 35. |
| `SendHome(unidad)` | Una unidad en tierra que su nación ya no controla vuelve a su provincia más cercana, o se disuelve si no tiene ninguna. También la usa `MakePeace`. |

### `Simulation/GameSession.Religion.cs`
Religiones.

| Elemento | Qué hace |
| --- | --- |
| `ReligionName(fe)`, `FaithOpinion(de, sobre)` | El nombre de una fe (`Rules/Religions.cs`: cinco, con su color); misma fe +10 de opinión, otra fe −10 (`OpinionFactors`). |
| `HasOtherFaith(provincia)`, `DailyConversion(provincia)`, `YearsToConvert(provincia)` | Si su gente sigue otra fe que su dueño (−10 de moral en `MoodFactors`), lo que avanza al día hacia la de su dueño (unos 25 años; más con moral alta, el doble con templo y +50 % con la Teología) y los años que le faltan. Los migrantes de la nación también la convierten (`ArriveMigrations`). |
| `DailyFaith(jugador)` | Cada día: la tierra vacía toma la fe de su dueño y la poblada avanza hacia ella; al llegar, la adopta. |

### `Simulation/GameSession.Plague.cs`
Epidemias. Usa su propio generador aleatorio (`seed ^ 0x9A6E`), así que no altera el resto de la partida.

| Elemento | Qué hace |
| --- | --- |
| `IsSick(provincia)`, `StartPlague(provincia)` | Si sufre una epidemia (`Province.PlagueDaysLeft` > 0) y hacer que enferme durante 90 días. Mientras dura, −15 de moral en `MoodFactors`. |
| `PlagueResistance(provincia)`, `PlagueDeaths(provincia)` | La parte de muertes y contagios que se evita (`Modifiers.PlagueResistance`: Medicina y Salubridad 25 %, Antibióticos 40 %, herbolario 20 %, hospital 40 %; como mucho 90 %) y la parte de su gente que muere cada día (0,1 % sin resistencia). |
| `DailyPlague()` | Cada día: las provincias enfermas pierden gente y contagian a sus vecinas (0,5 %, 3 % por un camino) y a los puertos a menos de 2000 km (0,5 %); al terminar quedan inmunes 10 años (`PlagueImmuneUntil`). Además cada ciudad puede enfermar sola, más cuanto mayor es. |

### `Simulation/GameSession.Decisions.cs`
Eventos con decisiones. Usa su propio generador aleatorio (`seed ^ 0xDEC1`).

| Elemento | Qué hace |
| --- | --- |
| `DecisionKind`, `Decision`, `DecisionOption`, `MoodEvent` | Los seis eventos (sequía, filón de oro, refugiados, bandidos, gran cosecha, inventor); uno pendiente con su nación, provincia, escala y plazo; una respuesta con su texto y su coste; y la moral que deja una decisión en una provincia durante un año. Todo se guarda (`Decisions`, `MoodEvents`, `NextDecisionId`). |
| `PendingDecisions(jugador)`, `DecisionById(id)`, `DecisionScale(jugador)` | Las decisiones que esperan respuesta y lo que está en juego: la raíz cuadrada de la gente de la nación entre 20 000, como mínimo 1. |
| `DecisionTitle`, `DecisionText`, `DecisionOptions(decisión)` | El título, el texto y las dos respuestas; la primera es la que se toma por defecto. |
| `CanChoose`/`Choose(jugador, decisión, respuesta)` | Responder: paga el coste y aplica el efecto (`Apply`). La moral va a `AddMoodEvent` y sale en `MoodFactors` con el título del evento. |
| `DailyDecisions()` | Cada día: caducan los efectos de moral, las decisiones sin respuesta tras 30 días toman la primera (y avisan), y cada nación con capital tiene 1,5 eventos al año de media. Los rivales responden al momento (`AiAnswer`: pagan si pueden, contentan a las provincias descontentas). |
| `StartDecision(jugador, tipo, provincia)` | Hace que ocurra un evento (lo usan las pruebas y `--panel decision`). |

La ventana está en `GameController.Decisions.cs` (`DecisionWindow`, `DecisionOpen`, que para el tiempo) y se dibuja en `GameScreen.Decisions.cs`.

### `Simulation/GameSession.History.cs`
El registro de estadísticas.

| Elemento | Qué hace |
| --- | --- |
| `HistorySample`, `History`, `HistoryOf(jugador)` | Las cifras de una nación un día (gente, soldados, oro neto al día, provincias, ciencia al día); se guardan todas (`SaveGame.History`). |
| `Sample(jugador)` | Las cifras de una nación ahora mismo. |
| `DailyHistory()` | Cada 30 días anota las de cada nación que sigue en pie. |

### `Simulation/GameSession.Objectives.cs`
Objetivos guiados para empezar (solo para el humano).

| Elemento | Qué hace |
| --- | --- |
| `Objective`, `AllObjectives` | Los nueve, en orden: capital, elegir investigación, poner en marcha una obra, entrenar un regimiento, reclamar una provincia, descubrir un avance, segunda ciudad, 10 000 habitantes y un acuerdo (alianza, pacto, paso o comercio). |
| `ObjectiveDone`, `ObjectivesDone`, `CurrentObjective` | Si está cumplido, cuántos lo están y el primero que falta (null cuando están todos). Se guardan en `SaveGame.ObjectivesDone`. |
| `ObjectiveTitle`, `ObjectiveHint` | El texto de cada uno y cómo cumplirlo. |
| `CheckObjectives()` | Cada hora: los que se cumplen ahora (`Met`) quedan cumplidos para siempre, pagan 50 de oro y avisan del siguiente. Se pueden cumplir en cualquier orden. |
| `LoadObjectives(partida)` | En las partidas anteriores a 1.81.0, cuenta como cumplidos los que ya lo están, sin pagarlos. |

### `Simulation/GameSession.Victory.cs`
Victoria y derrota.

| Elemento | Qué hace |
| --- | --- |
| `VictoryKind`, `GameOutcome`, `Outcome` | Las tres victorias (dominación, ciencia, puntuación) y quién ganó, cómo y cuándo. Se guarda en `SaveGame.Outcome`. |
| `Score(nación)`, `Ranking` | Puntuación: uno por cada 1000 habitantes, 5 por provincia, 20 por ciudad y 10 por avance (0 si está eliminada); y las naciones en pie de más a menos puntos. |
| `RivalsLeft(nación)` | Cuántas naciones le quedan por hacer caer o convertir en vasallas. |
| `DailyVictory()`, `Win(nación, tipo)` | Cada día, mientras nadie haya ganado: gana la primera nación (el humano antes que nadie) que sabe todos los avances, que no tiene rivales libres (con capital y en partidas de más de uno) o, al empezar el año 300, la de más puntos. Avisa de la victoria o la derrota. La partida sigue. |
| `HumanDefeated`, `VictoryName`, `VictoryText` | Si el humano ha perdido (ganó otro o su nación cayó), el nombre de cada victoria y la frase que la cuenta. |

### `Simulation/GameSession.Sieges.cs`
Asedios.

| Elemento | Qué hace |
| --- | --- |
| `Siege`, `Sieges`, `SiegeAt(provincia)` | Un asedio en curso: provincia, quién la sitia y cuántos días de trabajo lleva. Se guardan en `SiegeSave`. |
| `IsFortified(provincia)`, `SiegeDays(provincia)` | Si sus murallas o su castillo resisten (defensa de edificios mayor que 0) y cuántos días de asedio pide sin artillería. |
| `DailySiegeWork(asedio)`, `IsBesieging(unidad)` | Días de trabajo de un día: 1 más lo que suma la artillería por su ataque (`SiegeAttackPerDay`), según sus hombres. Si una unidad está sitiando donde está. |
| `LaySiege(provincia, atacante)` | Un regimiento entra en una provincia enemiga fortificada: empieza el asedio (o se suma al suyo) y avisa. |
| `DailySieges()` | Cada día avanza cada asedio; sin sitiadores o sin guerra se levanta, y al completarse la provincia queda ocupada (`Occupy`). |
| `EndSieges(condición)` | Termina los asedios que cumplen la condición: los de dos naciones que firman la paz y el de una provincia cedida. |

### `Simulation/GameSession.Events.cs`
`GameEvent` y `Happened`: lo que pasa (guerra declarada, paz, batalla, ciudad fundada, obra terminada, avance descubierto, asedio, revuelta, regalo), con la nación y la otra implicada. No cambia ninguna regla: es para quien escuche, sobre todo el sonido.

### `Simulation/GameSession.Seasons.cs`
Estaciones y desgaste.

| Elemento | Qué hace |
| --- | --- |
| `Season`, `SeasonNames`, `SeasonOf(provincia)` | Primavera, verano, otoño e invierno; de diciembre a febrero es invierno en el norte y verano en el sur. |
| `WinterSeverity(provincia)`, `MudSeverity(provincia)` | Dureza del invierno (0 a 1): nada por debajo de 30° de latitud, máxima desde 60°. El barro de primavera y otoño es la mitad de esa dureza. |
| `SeasonSlowdown(provincia)` | Cuánto más se tarda en entrar marchando: hasta el doble con la nieve más profunda (`WinterSlowdown`), hasta 1,5 veces con barro. Las flotas no se frenan. |
| `DailyAttrition(unidad)`, `HeightAttrition(provincia)` | Parte de sus hombres que pierde un regimiento al día: hasta un 0,5 % por el frío fuera de una ciudad de su nación (`WinterAttrition`), un 0,2 % en el desierto (`DesertAttrition`) y, en cualquier estación, un 0,4 % en las cumbres y el hielo polar (`PeakAttrition`) y un 0,1 % en la alta montaña (`HighMountainAttrition`). |
| `SeasonEffect(provincia)` | Lo que la estación y la tierra hacen allí a las tropas, en texto; nulo si nada. |

### `Simulation/RoadNetwork.cs`
Carreteras y ferrocarriles.

| Elemento | Qué es |
| --- | --- |
| `RoadKind`, `RoadInfo`, `RoadKinds` | Carretera (Ingeniería: 20 de madera y 8 de oro y 10 días de trabajo por tramo, ×1,5 de velocidad) y ferrocarril (Ferrocarril: madera, oro, hierro y carbón, 15 días por tramo, ×2). |
| `RoadNetwork` | Los tramos entre provincias vecinas, de quien sea: `Between(a, b)`, `Has(a, b, tipo)` (un ferrocarril vale como carretera), `Lay` (pone o mejora, nunca empeora), `Links` y `Version` (sube con cada tramo). |
| `RoadProject` | Una obra: su dueño, tipo, ruta (de principio a fin), días por tramo, el tramo en curso (`Next`) y el trabajo que le queda. |
| `RoadPlan` | Lo que costaría una carretera: ruta, tramos nuevos, coste, días de trabajo, ciudades por el camino y horas de marcha. |

### `Simulation/GameSession.Roads.cs`
Obras de carreteras y ferrocarriles, y el suministro por ellas.

| Función | Qué hace |
| --- | --- |
| `Roads`, `RoadProjects` | La red y las obras en curso. |
| `IsRoadHub(jugador, provincia)`, `RoadHubs(jugador)` | Donde empiezan y acaban las carreteras: sus ciudades (si no están ocupadas) y sus cuarteles generales. |
| `CanLayRoad(...)`, `Connected(a, b, tipo)` | Tierra por la que puede construir (la suya, la que ocupa y la libre); si dos provincias están unidas por la red. |
| `PlanRoad(jugador, desde, hasta, tipo)` | La ruta de marcha entre dos provincias por esa tierra (aprovecha las carreteras que hay) y lo que costaría; no cuenta los tramos hechos ni los que ya van a hacer sus obras. |
| `CanBuildRoad(...)` / `BuildRoad(...)` | Comprueba (avance, ciudad o cuartel en los dos extremos, ingenieros en el origen, hay camino, falta algún tramo y puedes pagarlo) / paga los tramos que faltan y empieza la obra. |
| `LinksLeft(obra)`, `EngineersOn(obra)`, `CancelRoad(jugador, obra)` | Tramos por hacer, batallones de ingenieros del dueño en cualquier provincia de la ruta, y cancelar devolviendo lo de los tramos sin hacer. |
| `DailyRoadWork()` | Cada día, cada obra avanza un día de trabajo por batallón de ingenieros en su ruta, tramo a tramo desde el principio (espera si el dueño pierde esa tierra); al acabar, avisa. |
| `SupplyNetwork(jugador, ciudades)` | Las provincias que las carreteras y ferrocarriles unen a sus ciudades por tierra que controla o libre: de ahí sale el suministro. |

### `Simulation/GameSession.Exploration.cs`
Exploradores que van solos: cada unidad de exploradores con la opción activada camina hasta la mejor provincia libre junto a las fronteras de su nación, la reclama y busca la siguiente.

| Función | Qué hace |
| --- | --- |
| `CanAutoClaim(unidad)` / `SetAutoClaim(jugador, unidad, sí/no)` | Comprueba (solo unidades de exploradores) / activa o quita la exploración. Al activarla sale ya hacia su primer destino; si no hay tierra libre a su alcance, falla. Quitarla no la detiene: conserva el camino que lleve. |
| `AutoClaimUnits()`, `AutoClaim(unidad, callada)` | Cada hora, cada unidad que explora y está quieta reclama la provincia en la que está (si puede) y sale hacia la siguiente; si se queda sin tierra, deja de explorar y avisa. Si deja de ser solo de exploradores, lo deja. |
| `NextAutoClaimTarget(unidad)`, `LandScore(provincia)` | El camino a la provincia libre y habitable junto a sus fronteras (o junto a la unidad, si aún no tiene ninguna) con mejor tierra para lo lejos que está, a menos de 30 días, sin pasar por tierras ajenas y sin repetir el destino de otros exploradores suyos. |

### `Simulation/Pathfinder.cs`
Rutas por el grafo de provincias. Solo por tierra: mares y lagos están cerrados (serán para unidades
navales); el hielo polar se puede cruzar.

| Función | Qué hace |
| --- | --- |
| `CanEnter(provincia)`, `Allowed(...)` | ¿Puede entrar un viajero sin regla propia (los migrantes)? (no, si es agua). Con una regla (`puedeEntrar`), la regla decide, agua incluida. |
| `StepHours(a, b)`, `Speed(provincia)`, `FastestSpeed` | Horas para ir de una provincia a su vecina: distancia entre centros / (10 km/h × facilidad del terreno × la carretera (×1,5) o el ferrocarril (×2) que las une, si hay). La heurística de A* usa la provincia más rápida posible para no pasarse. |
| `FindPath(desde, hasta, puedeEntrar)` | Ruta más rápida (A*) y su duración, o `null` si no hay camino; `puedeEntrar` dice por dónde puede ir (para una unidad, `CanUnitEnter`: ni tierras ajenas en paz ni mar sin navegación). |
| `FromSources(orígenes, maxHoras, destinos, puedeEntrar)` | Horas desde el origen más cercano a cada provincia (Dijkstra) y cuál es ese origen. Se usa para la migración y para que la IA busque sitio. Puede parar al alcanzar todos los destinos. |
| `Heuristic` | Estimación para A*: distancia en línea recta a 10 km/h. |

### `Simulation/Names.cs`
| Elemento | Qué es |
| --- | --- |
| `CityNames.Next(usados, random)`, `Suggest(...)` | Genera un nombre de ciudad por sílabas sin repetir (y lo reserva, o solo lo propone); solo cuando se acaban los reales. |
| `ProvinceNames.Next(usados, azar)` | Un nombre de provincia nuevo con otras sílabas (unas 37.000 combinaciones), sin repetir y sin vocales dobles ni tres seguidas; si se agotaran, añade un número romano. Solo cuando se acaban los reales. |

### `Simulation/Countries.cs` y `Simulation/GameSession.Names.cs`
| Elemento | Qué es |
| --- | --- |
| `Country`, `Countries.All`, `ByName`, `Pick(n, azar)`, `MaxNations` | Los 49 países reales que pueden ser naciones, con sus ciudades (la capital primero) y regiones reales en español y en letras Latin-1, de 24 letras como mucho. |
| `Countries.Color(n)` | El color de la nación n: doce fijos y, después, tonos repartidos por el ángulo áureo con luminosidad alterna. |
| `SuggestCityName(jugador, después)`, `NextCityName(jugador)` | El primer nombre de ciudad libre (o el siguiente a uno dado, para «Otro nombre»): las ciudades de su país, luego las de los países que no juegan y luego las de los demás (cada nación en su propio orden); al final, uno inventado. |
| `NameProvince(provincia, jugador)` | Al reclamarla, una región de su país, luego de los demás, luego una ciudad libre de otro país y luego «Norte de», «Sur de», «Este de» u «Oeste de» una región; al final, uno inventado. Los nombres de ciudades y provincias no se repiten entre sí. |

### `AI/AiPlayer.cs`
Rival controlado por el ordenador. Clase parcial: el ejército está en `AiPlayer.Military.cs`. Determinista (usa su propia semilla).

| Función | Qué hace |
| --- | --- |
| `Think(decisionesDiarias)` | Turno de la IA (sin contar flotas ni tropas embarcadas): clasifica regimientos nuevos, licencia soldados si hay hambre en paz, guía a colonos, cuarteles, ingenieros, reclamadores y soldados (en guerra), y trae a casa las divisiones sin suministro; `PlayerId` identifica la nación; una vez al día, celebra fiestas, ajusta las prioridades de la investigación, elige qué investigar, recluta y construye. |
| `HoldFestivals()` | Paga fiestas en las ciudades con moral por debajo de 45 si, tras pagarlas, le quedan 30 de oro para reclutar. |
| `SetResearchPriorities()` | Prioridades de la investigación: Economía 2, Sociedad 1 y Militar 1 en paz; Economía 3 si pasa hambre; Militar 3 en guerra. |
| `ChooseResearch()`, `ResearchOrder` | Cada rama sin investigación elige el primer avance que puede de su orden de preferencia (Agricultura, Escritura, Tiro con arco, Carpintería... y en cada era nueva, primero lo que da ciencia, comida o mejores tropas). |
| `AdoptInstitutions()` | Compra las instituciones que ya han llegado a su nación si, tras pagarlas, le quedan 30 de oro para reclutar. |
| `BuildCityIfWorthIt()` | Mientras tenga menos de 8 ciudades, convierte en ciudad su provincia sin ciudad más poblada (con al menos 1.000 habitantes) cuando puede pagarla guardando para reclutar. |
| `Construct()`, `BuildOrder`, `WorthBuilding(...)` | Elige su edificio más deseado (ciudades primero, luego por población y orden de preferencia, que empieza por el cuartel) donde compense: al menos 200 habitantes, aserraderos en tierra con madera, templos y anfiteatros donde hay inquietud, acueductos al 60 % de la capacidad, cuartel en la capital, taller donde tiene cuartel, y cuarteles, murallas y castillos en las de al menos 2.000 habitantes; universidades, bancos, fábricas, hospitales y centrales eléctricas en sus ciudades. Lo empieza cuando puede pagarlo guardando madera y oro para reclutar; si no, ahorra. |
| `GuideSettlers(unidad)` | Busca el mejor sitio cercano para una ciudad, va allí y la funda. |
| `GuideWarriors(unidad)` | Reclamadores (exploradores) en paz: Reclama la provincia si está libre; si no, va a la mejor provincia libre de su frontera. No reclama más rápido de lo que llegan los migrantes. |
| `Recruit()` | Envía colonos cuando una ciudad ha crecido lo bastante, entrena exploradores para reclamar tierra si le sobra comida y, con `TrainEngineers`, un batallón de ingenieros si no tiene y quiere una carretera o un ferrocarril. |
| `IsEngineerUnit(unidad)`, `WantedRoad()`, `GuideEngineers(unidad)` | Sus unidades de solo ingenieros no van al ejército: une a su capital la ciudad más cercana que aún no lo está, primero por carretera y luego por ferrocarril, desde la ciudad unida más próxima a ella. Los ingenieros van allí y empiezan la obra cuando puede pagarla, y se quedan en su ruta hasta acabarla. |
| `FreeBorderProvinces()` | Provincias libres y reclamables junto a su territorio. |
| `CanSettle(provincia)` | ¿Se puede fundar ciudad aquí? |
| `SiteScore(provincia)` | Lo buena que es una provincia: comida, yacimientos conocidos sin agotar y costa. |
| `TotalPopulation()`, `NearestCityDistanceKm()` | Ayudas. |

### `AI/AiPlayer.Military.cs`
El ejército de un rival.

| Función | Qué hace |
| --- | --- |
| `ClassifyNewRegiments()`, `Auxiliary(tipo)` | Los regimientos nuevos de un solo batallón de exploradores cubren primero los puestos de «reclamadores» (uno por ciudad, más uno); el resto forma el ejército, también los reclamadores sin exploradores de partidas antiguas. |
| `BuildArmy()`, `Spare(coste)` | Desde el día 180, hasta tener 2 batallones por ciudad o uno por cada 30 provincias, lo que sea más (el doble en guerra), entrena unidades enteras de su plantilla, del tamaño que su ciudad puede dar (hasta 6 batallones: infantería con dos de choque), o si no el mejor batallón suelto, en su ciudad más poblada con cuartel (la plantilla solo lleva máquinas de guerra si esa ciudad tiene taller); sin gastar la reserva ni entrenar si pierde oro cada día. |
| `BuildNavy()` | Una flota de guerra por cada tres puertos: el barco de combate con más ataque que puede construir, en su puerto más poblado. Las flotas se quedan en puerto. |
| `ArmyTemplate(tamaño)`, `CanSupply(tipo)` | Su plantilla (nunca con exploradores ni ingenieros, igual que `BuildArmy`): dos de su infantería más resistente, su tropa más ofensiva y otra de infantería, recortada al tamaño; solo con tropas cuyos materiales (cobre, hierro…) tiene o produce. |
| `ChooseProduction()` | Cada día pone cada taller y fábrica a fabricar el equipo que más le falta de su plantilla, sus exploradores y sus ingenieros: la línea cuyo almacén, con lo que fabrican los demás talleres en un mes, arma menos batallones. Con equipo para 6 batallones de cada una (`KitsInStore`), los para. |
| `OrganiseArmy()`, `RaiseAndAttach(...)`, `HighestHeadquarters` | Junta regimientos pequeños (hasta 5 batallones) y mete las unidades de una provincia en brigadas y divisiones (`Incorporate`), forma cuarteles de cuerpo y ejército cuando hacen falta y asigna a todos. |
| `StaffArmy()` | Una vez al día pone oficial a su mayor unidad o flota sin él, uno de su arma: el mejor de la reserva (más virtudes y estrellas, menos defectos) o uno nuevo si la reserva está vacía y le sobra el oro. |
| `FollowTroops(cuartel)` | El cuartel va adonde están sus unidades si alguna queda fuera de alcance. |
| `GuideSoldier(unidad)` | En guerra: si sitia una provincia, se queda hasta tomarla; si no, acude a sus ciudades atacadas, ataca la provincia enemiga vecina más débil (si supera 1,3 veces su defensa) o marcha hacia tierra enemiga que su suministro alcance; descansa si está desorganizada. |
| `GoHomeIfCutOff(unidad)`, `IsEnemyLand(provincia)` | Un regimiento sin suministro vuelve a la capital. |
| `TakeWinterQuarters(unidad)` | En paz, un regimiento que pierde hombres por el frío o el desierto se refugia en su ciudad más cercana, salvo si guarda una provincia descontenta. |
| `KeepOrder()`, `GarrisonedRevoltRisk` | En paz, manda el regimiento libre más cercano de su ejército a cada provincia sin guarnición que pasa del 25 % de rebelión; los que ya guardan una provincia descontenta se quedan. |
| `Diplomacy()`, `Neighbours()`, `WarAppetite(vecino)`, `HasFreeLandNearby()` | Tras dos años, cada día tiene una entre 60 de pensar en una guerra: ataca al vecino más débil (sin tregua ni alianza) cuyo poder con el de sus aliados (`DefendingPower`) no pase de su apetito por el suyo; si puede invadir por mar (`CanInvade`), también a las naciones de ultramar (`OverseasNeighbours`), con 0,2 menos de apetito. El apetito es su carácter (`_aggression`, de 0,8 a 1,1, fijo por semilla), +0,3 si ya no tiene tierra libre junto a la suya, +0,3 si el vecino ya está en guerra y +0,2 si gobierna gente de su cultura, +0,1 si sigue otra fe. En guerra con otro rival, tras 60 días le exige lo que ocupa si la puntuación lo paga; si no, lo hace vasallo cuando tiene menos de la mitad de sus provincias, o le impone reparaciones tras 120 días; y le propone la paz blanca cuando la guerra se alarga y va mal. Un vasallo deja la paz de las guerras de su señor a su señor. Anexiona a sus vasallos en cuanto puede. |
| `WouldAcceptPeace(otro, términos)`, `Winning(otro)` | Acepta siempre que le entreguen tierras; cede lo que le ocupan, paga reparaciones o se hace vasallo si no va ganando o si la puntuación del enemigo pasa de 50; la paz blanca, tras 60 días si no va ganando (o tras un año). Va ganando si su ejército es mucho más fuerte y ocupa más de lo que ha perdido. |

### `AI/AiPlayer.Relations.cs`
Los amigos del rival.

| Función | Qué hace |
| --- | --- |
| `WouldAlly(otro)` | Firma una alianza si su opinión del otro llega a 40, tiene sitio para otro aliado y el otro no es aliado de sus enemigos. |
| `SeekAlliances()` | Cada 60 días de media, ofrece la alianza al rival que mejor ve (al menos 15); si este aún no le ve lo bastante bien pero no le es hostil, le hace un regalo si le sobra oro. |
| `DefendingPower(víctima)` | El ejército al que se enfrentaría: el de la víctima más el de sus aliados, y menos cuanto peor la ve (una nación odiada parece más débil). |
| `SeekTrade()` | Cada 30 días de media, compra por oro un recurso que conoce y del que no gana nada al rival que más tiene de sobra. Nunca impone acuerdos al humano. |
| `WouldSignPact(otro)`, `WouldGrantAccess(otro)`, `SeekPacts()` | Firma un pacto si su opinión del otro llega a 0, o a −25 si su ejército es más fuerte; deja pasar a quien ve con al menos 20. Cada 60 días de media busca un pacto con el vecino más fuerte que le da miedo (un ejército un 20 % mayor). |

### `AI/AiPlayer.Naval.cs`
Invasiones por mar. No guarda nada entre días: cada paso sale de dónde están las tropas y los barcos.

| Función | Qué hace |
| --- | --- |
| `StagingPort()`, `TransportType(puerto)`, `CanInvade()` | Su mayor ciudad con puerto (donde se reúne la invasión), el transporte con más sitio que puede construir allí, y si puede invadir (tiene puerto y sabe construir transportes). |
| `OverseasNeighbours()`, `IsCoast(provincia)` | Naciones sin frontera con la suya cuya costa está al alcance de sus puertos: 1.500 km con Navegación a vela, 8.000 km con Cartografía. |
| `Invade()` | En guerra con un enemigo sin frontera: reúne en el puerto hasta 4 regimientos (la mitad de su ejército), construye transportes hasta que quepan, embarca, zarpa cuando están todos y desembarca; los barcos vacíos vuelven al puerto. Si no hay ruta a ninguna costa enemiga, las tropas vuelven a tierra. |
| `Land(flota, carga, enemigos)` | Desembarca todo en la costa enemiga junto a la flota sin tropas enemigas, la más valiosa (`ProvinceValue`). |
| `SailToLanding(flota, enemigos)` | Navega hasta el mar junto a la costa enemiga peor defendida (y, a igualdad, la más cercana); falso si no encuentra ruta. |
| `ConvoysAndMissions(enGuerra)`, `OrganiseNavy()` | Encarga convoyes si le faltan y, en guerra, pone sus submarinos a atacar convoyes y sus buques de guerra a patrullar. Une sus agrupaciones de guerra en cada puerto (hasta una fuerza; los transportes aparte), forma una Flota por cada 3 agrupaciones de guerra y la Armada con dos Flotas, y pone cada agrupación bajo la Flota más cercana con sitio. |

### `World/Biome.cs`
| Elemento | Qué es |
| --- | --- |
| `Biome` | Los 18 biomas (océano profundo, océano, mar costero, lago, hielo polar, tundra, taiga, bosque templado, pradera, estepa, desierto, sabana, selva tropical, humedal, colinas, montañas, alta montaña y cumbres, este al final para no cambiar el número de los demás). Las cumbres no son habitables (no se reclaman) pero se cruzan despacio, como el hielo. |
| `BiomeInfo` | Ficha de cada bioma: nombre, si es agua, si es habitable, densidad de provincias (valores bajos, provincias grandes), rendimiento de comida y madera, habitantes por km², facilidad de paso y color. |
| `Biomes.Info(bioma)` | Devuelve la ficha. **Aquí se equilibra cada tipo de terreno.** |

### `World/Province.cs`
`Province`: id, nombre propio (`Name`: se lo pone la primera nación que la reclama; hasta entonces, y siempre en océanos y polos, está vacío y `DisplayName` usa el del bioma), bioma dominante, centro (píxel y lat/lon), área en km², altitud media, vecinas,
yacimientos (`Deposits`: producción diaria; `DepositSizes`: tamaño de la bolsa; `Reserves`: lo que queda en la partida), dueño, población, ciudad, moral (`Mood`, 0-100) y fertilidad
(`Fertility`, multiplicador de nacimientos, 1 = normal), instituciones que han llegado (`Institutions`), cultura (`CultureId`: la nación que la pobló; -1 si está vacía), religión (`ReligionId`, -1 si está vacía) y lo convertida que está a la de su dueño (`Conversion`, 0-1), asimilación (`Assimilation`, 0-1) y días camino de la rebelión (`RevoltProgress`). `ControllerId` es quién la tiene en la guerra (su dueño, o el enemigo que la ocupa) e `IsOccupied` si la ocupa otro. `IsWater`, `IsClaimable`, `IsOwned`,
`RiverFlow` (agua del mayor río que la cruza, 0 sin río), `HasRiver` (la cruza un gran río), `FoodYield` (rendimiento de comida de su bioma, más en un gran río),
`Capacity` (habitantes que alimenta su tierra al principio de la historia, más en un gran río) y `HasDeposit(recurso)` (tiene ese yacimiento sin agotar) son atajos.
Edificios: los terminados (`Buildings`) y la suma de sus efectos (`BuildingBonuses`), el que está en obras (`Constructing`) o la ciudad en obras (`PlannedCityName`) y los días que le quedan (`ConstructionDaysLeft`). Lo que entrena su ciudad, su cuartel o su taller (`Training`) y lo que fabrica su taller o fábrica (`Production`: la clave de un modelo, o nada; se pierde al cambiar de dueño). `AddBuilding(tipo)` añade uno terminado y `RemoveBuilding(tipo)` lo derriba; `Has(tipo)` dice si lo tiene o tiene aquello en que se convirtió (una fábrica cuenta como taller); `ClearBuildings()` los quita todos y vacía la instrucción (nueva partida).

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
| `WorldSettings` | Tipo de mapa, semilla, número objetivo de provincias (25.000), dificultad (Normal si no se dice, también en las partidas guardadas antes de que existiera) y versión del generador (`Generator`): las partidas nuevas usan `WorldGenerator.LatestGenerator` (3: con cumbres desde el 2 y como mucho `GameRules.MaxDepositsPerProvince` (4) yacimientos por provincia desde el 3) y las guardadas antes de que existiera, el 1, así que su mapa se regenera igual. **Un cambio en la generación que altere el mapa debe ir en una versión nueva del generador**, o las partidas guardadas dejarán de cargar. `Size` es el tamaño del mundo aleatorio (Grande si no se dice, también en partidas antiguas); `WorldSettings.New(tipo, semilla, dificultad, tamaño)` prepara los de una partida nueva (la Tierra siempre es grande). |
| `MapSize`, `MapSizeInfo`, `MapSizes` | Tamaños del mapa aleatorio: Diminuto (12 % de tierra, 2.550 provincias en todo el mapa: unas 1.700 de tierra; hasta 12 naciones), Pequeño (18 % de tierra, 4.650: unas 3.550; hasta 16), Mediano (24 %, 8.750: unas 7.050; hasta 24) y Grande (30 %, 12.500: unas 10.750; hasta 32, también la Tierra). Los pequeños tienen menos tierra y provincias más grandes. Hasta 1.108.0 había el doble de provincias; las partidas guardadas guardan su número y se regeneran igual. |
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
| `Place(provincias, semilla, dificultad, máximo)` | Reparte yacimientos en las provincias habitables, agrupados por regiones; una provincia puede tener varios, hasta `máximo` (`GameRules.MaxDepositsPerProvince`, 4, en los mapas del generador 3; sin límite en los anteriores). Cada recurso se sortea una vez y, si falla, una segunda con la probabilidad multiplicada por `ExtraDepositChance` de la dificultad y con otros generadores, para que la primera tirada ponga los mismos yacimientos en todas las dificultades. La piedra y el azufre, y luego el salitre y los caballos, que llegaron después, tiran con generadores propios, así que los demás yacimientos siguen donde estaban. Provincias con yacimiento (sin contar piedra, azufre, salitre ni caballos): 52 % en Muy fácil, 43 % en Fácil, 30 % en Normal, 24 % en Difícil y 17 % en Muy difícil (con dos o más: 15 %, 9 %, 5 %, 3 % y 1 %). |
| `AddDeposit(...)` | Pone un yacimiento: una bolsa finita con producción diaria (`Deposits`) y tamaño total (`DepositSizes`) de 10 a 50 años de producción máxima por `DepositSize` de la dificultad, sorteado con su propio generador. |
| `Chance(recurso, bioma, latitud)` | Probabilidad de cada recurso según el terreno (caucho en selvas tropicales, petróleo en desiertos, etc.). |
| `Cap(provincia, máximo)` | Si una provincia tiene más yacimientos que `máximo`, se queda con los más raros en su terreno (menor `Chance`) y quita los demás. Todas las tiradas se hacen igual, así que las otras provincias no cambian. |
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

## 3. `src/Conquer.Presentation` — presentación sin motor

Lo que ve y hace el jugador, sin nada de Silk.NET, OpenGL ni ningún otro motor (un test lo comprueba): el estado de la partida en pantalla
(`GameController`), el contenido de cada panel, barra, ventana, pantalla y menú como datos (`Document`, tablas, tarjetas...), los
marcadores del mapa ya en posiciones de pantalla, y las órdenes que dan sus botones. El cliente solo dibuja todo eso y convierte el ratón
y el teclado en llamadas aquí, así que cambiar de motor (por ejemplo, a Godot) no toca ni esta librería ni `Conquer.Game`.

### `Camera.cs`
| Función | Qué hace |
| --- | --- |
| `ScreenToMap`, `MapToScreen` | Convierten entre píxeles de pantalla y del mapa (teniendo en cuenta la vuelta al mundo). |
| `Pan`, `ZoomAt`, `LookAt` | Mover, hacer zoom alrededor del ratón, centrar. |
| `Clamp()` | No deja salirse por arriba o por abajo del mapa. |

### `MapMode.cs`
`MapMode`: terreno, político, población, moral, fertilidad, recursos, instituciones, cultura y religión.

### `GameClock.cs`
`GameClock`: el reloj de la partida. `HoursPerSecond` son las horas de juego por segundo real de cada velocidad (pausa, 1 h/s … 7 días/s); `SetSpeed`, `TogglePause` (reanuda a la velocidad de antes) y `Advance(dt)`, que dice cuántas horas simular en este fotograma sin acumular retraso si el ordenador no da abasto.

### `MessageLog.cs`
`MessageLog`: los mensajes en pantalla (resultados de las órdenes y avisos de la partida), cada uno `Lifetime` segundos. `Current(ahora)` quita los caducados y devuelve los demás del más nuevo al más antiguo; `Message.Opacity(ahora)` los desvanece el último segundo y medio.

### `TextFormat.cs`
| Función | Qué hace |
| --- | --- |
| `Compact(valor, decimales)` | Cifra abreviada: 950, 12,3k, 2,9M (con `decimales`, 8,7). |
| `Plural(n, uno, varios)` | «1 unidad», «3 unidades». |
| `UpkeepText(costes)` | Mantenimiento diario en palabras: «1,2 oro, 0,4 hierro/día». |
| `TrainingDaysText(días, base)` | Días de instrucción, con los de antes de tus avances si estos los acortan. |
| `SpanishSortKey(nombre)` | Clave para ordenar nombres en orden alfabético español sin ICU: sin mayúsculas ni tildes, y la ñ después de la n. |

### `Markup.cs`
`Markup.Parse(línea)`: el poco Markdown de la ayuda y el historial: «## » título, «### » sección, «- » viñeta y el resto párrafo (`MarkupLine`, `MarkupKind`).

### `HelpTopics.cs`
**Aquí se escribe la ayuda.** Temas: controles, primeros pasos, población y moral, economía y recursos, ciudades, ciencia, ejército, flotas y mar, diplomacia, mapa y partida. Las cifras salen de `GameRules`, así que siguen los cambios de equilibrio; solo caben caracteres Latin-1 (nada de «…» ni «−»). `Lines(tema)` da el tema ya interpretado; `AllText`, todo el texto para los tests.

### `Changelog.cs`
`Changelog.Lines`: `CHANGELOG.md` (incluido en esta librería) ya interpretado, sin el título, los corchetes de las versiones ni las negritas.

### `SaveFiles.cs`
Partidas guardadas en disco, en la carpeta `Partidas` junto al ejecutable (`Folder`). Si no se puede escribir en ella, en la carpeta de datos del usuario, donde iban antes de la 1.45.1: `~/.local/share/Conquer/Partidas` en Linux, `%LOCALAPPDATA%\Conquer\Partidas` en Windows y `~/Library/Application Support/Conquer/Partidas` en macOS. Al arrancar, las partidas que quedan allí se mueven a la del juego (`ChooseFolder`). Cada una es un fichero `.conquer` que se llama como la nación y la fecha de juego ("Kartesia - 5 feb 3999 a.C. 00h"). `List()` las devuelve de la más reciente a la más antigua; `Save(partida, versión)` escribe primero un fichero temporal y luego lo renombra, para no dejar nunca una partida a medio escribir; `Read(ruta)` y `Delete(partida)`.

### `GameController.cs`
La partida en pantalla, sin pantalla: lo que se ve, lo seleccionado, los diálogos abiertos, el reloj, los mensajes y las órdenes del jugador. `GameScreen` lo dibuja y convierte clics y teclas en llamadas a él. Clase parcial: los paneles y barras que construye están en `GameController.Province.cs`, `GameController.Unit.cs` y `GameController.Bars.cs`.

| Elemento | Qué es |
| --- | --- |
| `ProvinceTab`, `CityNaming` | Pestañas del panel de provincia (General, Edificios y Ejército, esta solo donde se entrenan tropas) y la ciudad que se está nombrando (por unos colonos o por los habitantes de una provincia). |
| `GameController(partida, cargada)` | Crea la cámara y centra la vista en tus colonos (seleccionados) o, en una partida cargada, en tu capital, sin repetir los avisos antiguos. |
| `Camera`, `Clock`, `Messages`, `Now`, `Mode`, `ResourceFilter` | La vista, el reloj de juego, los mensajes, los segundos reales desde que se abrió la partida (mensajes y animaciones), el modo de mapa y, en el de recursos, el único que se muestra. |
| `Tick(dt, congelado)` | Un fotograma: pasa el tiempo real, simula las horas que da el reloj salvo si está congelado (una ventana que para el tiempo) y convierte los avisos nuevos de la partida en mensajes. |
| `Show(resultado)` | Muestra el resultado de una orden. |
| `Nation` | La pantalla de la nación (`NationScreen`). |
| `Center`, `Between`, `CenterOnHome`, `CycleMode` | Centro de una provincia, punto entre dos cruzando el borde del mapa por el lado corto, centrar la vista en la capital y pasar al siguiente modo de mapa. |
| `SelectedUnit`, `SelectedUnitId`, `SelectedProvince`, `HoverProvince`, `HasSelection` | Selección (una unidad o una provincia; la unidad que ya no existe o que se pierde en la niebla se deselecciona sola) y provincia bajo el ratón. |
| `VisibleProvinces`, `ExploredProvinces`, `IsExplored`, `FogSignature` | Lo que ve el jugador (`GameSession.VisibleProvinces`), lo que ha explorado alguna vez (`Player.Explored`; leerlo pone al día lo que ve) y un número que cambia cuando cambia cualquiera de los dos, para que el cliente vuelva a dibujar la niebla. |
| `SelectUnit`, `SelectProvince`, `ClearSelection`, `ViewUnit`, `ViewProvince` | Cambiar la selección (una provincia sin explorar no se puede seleccionar); las dos últimas además centran la vista (desde la pantalla de la nación). |
| `ChoosingMigrationTarget`, `MigrationAmount` | Migración forzada: si el próximo clic elige el destino, y cuánta gente. |
| `ClickProvince()` | Clic en el mapa sin marcador: envía la migración forzada si se estaba eligiendo destino, o selecciona la provincia. |
| `OrderMove()` | Clic derecho: mueve la unidad seleccionada (o ataca); una orden propia detiene la exploración automática. |
| `Save(versión)` | Guarda la partida y avisa de cómo fue. |
| `Naming`, `CityName`, `OpenCityNaming`, `SuggestCityName`, `CancelCityNaming`, `ConfirmCityName` | Diálogo para nombrar una ciudad: propone un nombre, se puede cambiar o pedir otro, y al confirmar la funda o empieza la obra; si el nombre no vale, sigue abierto. |

### `GameController.Province.cs`
| Función | Qué hace |
| --- | --- |
| `SidePanel()` | El panel derecho como `Document`: el de la unidad seleccionada o el de la provincia, con `ClearSelection` como botón de cerrar; null si no hay nada seleccionado. |
| `ProvincePanel`, `GeneralTab` | Nombre y pestañas General, Edificios y Ejército (esta solo donde se entrenan tropas o queda algo en instrucción). En General: terreno, superficie, altitud, asedio en curso (quién, cuánto lleva y cuándo caerá) o días de asedio si está fortificada, estación (con lo que la nieve, el barro o el desierto hacen a las tropas), río (con lo que da al pasar el ratón), dueño, población (con los migrantes en camino, `IncomingMigrants`, que cuentan al llegar), moral (`MoodTooltip`: sus causas y su efecto en la producción), fertilidad, nacimientos, migrantes, recursos y yacimientos con lo que les queda; en tus ciudades, fiestas y colonos. |
| `AddCultureAndRevolt(documento, provincia)`, `AddFaith` | En General, la cultura de su gente, su religión (con lo convertida que está y los años que le faltan si no es la de su dueño) (con lo asimilada que está y los años que le faltan) y, si está descontenta o camino de la rebelión, el porcentaje, si tiene guarnición y qué pasará al sublevarse. |
| `ForcedMigration` | Migración forzada: cuánta gente (−100, −10, +10, +100 y «Máx.», sin pasar de la que puede salir), su coste en oro y el botón para elegir el destino en el mapa. |
| `ProductionSection` | En la pestaña Edificios de una provincia tuya con taller o fábrica: lo que fabrica y un botón por cada suministro que puede hacer, titulado por lo que se fabrica (`SupplyName`), con sus piezas al día y, en su tooltip, para quién es, lo que gasta y lo que hay en el almacén; y «Parar». |
| `BuildingsTab`, `TrainingImprovements`, `IsBuildingKnown` | Pestaña Edificios: la obra en curso (edificio o ciudad) con su barra, los edificios terminados (bajo el cuartel y el taller o la fábrica, las tropas que tus avances instruyen más rápido allí) y, en tus provincias, «Ciudad» y un botón por cada edificio que puedes levantar (los terminados y los botones llevan el icono del edificio, si lo tiene); los que solo necesitan ciudad o yacimiento dicen qué les falta, y los de avances sin descubrir no aparecen. El taller deja de ofrecerse cuando ya se construyen fábricas. |
| `ArmyTab` | Pestaña Ejército: un aviso si falta el cuartel o el taller, las unidades de tus plantillas (las cuatro primeras), los batallones sueltos (los de avances sin descubrir no aparecen), los cuarteles generales y lo que está en instrucción, con su barra. |

### `GameController.Unit.cs`
| Función | Qué hace |
| --- | --- |
| `UnitPanel` | Panel de la unidad: tipo (`Composition`: «Brigada de 3 regimientos (12 batallones)»), hombres (los que quedan y su plantilla completa), nación, ubicación, estado (`UnitState`: «A bordo de…», «Combatiendo en el mar», «Hacia… (tiempo)», «Explorando»…) y, si es tuya, sus botones: «Editar unidad» (en unidades de combate, cuarteles y flotas; abre el editor), embarcar en una flota cercana con sitio (`EmbarkButtons`), fundar, reclamar, «Explorar y reclamar» en las de exploradores (una orden de movimiento o «Detener» lo quitan), licenciar o asentarse y detener. Una unidad embarcada explica cómo desembarcar. |
| `EngineerButtons` | Con ingenieros: construir una carretera o un ferrocarril desde una ciudad o cuartel tuyo (abre la ventana de carreteras) y las obras que pasan por aquí, con «Cancelar obra». |
| `RegimentDetails` | Suministro, velocidad, mando, oficial propio, general de su cuartel y experiencia media (en una flota: velocidad en el mar, si está en puerto y su carga), y cada batallón con sus barras de hombres y organización y, al pasar el ratón, su ataque, defensa, papel y experiencia. |
| `HeadquartersDetails`, `CommandLine`, `OfficerLine`, `AttachButtons` | Alcance, general y subordinados de un cuartel (en rojo los que están fuera de alcance), de quién depende la unidad y botones para asignarla a uno de los tres cuarteles más cercanos del nivel de arriba o quitarla. |
| `OfficerTooltip(oficial)`, `HubName(provincia)` | Los rasgos y estrellas de un oficial; el nombre de la ciudad o de los cuarteles de una provincia, para las carreteras. |

### `GameController.Bars.cs`
| Elemento | Qué es |
| --- | --- |
| `TopBar()`, `ResourceStock`, `SeasonAtHome()` | La barra superior: nación, población y moral media, fecha con la estación en la capital, botones de velocidad, cada recurso conocido con su cantidad abreviada y su cambio del día, y la etiqueta del botón Nación (con «!» si alguna rama de la ciencia no investiga nada teniendo avances disponibles). |
| `ModeNames`, `ModeButtons()`, `ResourceFilterButtons()` | Un botón por modo de mapa y, en el de recursos, «Todos» y uno por recurso conocido, que hacen de leyenda. |
| `Hints()` | Las pistas de controles de abajo (otras mientras se elige el destino de una migración). |
| `MapTooltip()` | El tooltip de la provincia bajo el ratón («Tierra inexplorada» si no la ha visto nunca): nombre, terreno, dueño, río, población (y los migrantes en camino), moral y fertilidad, y sus yacimientos en el modo recursos sus instituciones en el modo instituciones, o su cultura, asimilación y rebelión en el modo cultura. |
| `CityNamingDialog()` | El diálogo para nombrar una ciudad: título, dónde o cuánto cuesta, por qué no vale el nombre y la etiqueta de confirmar. |

### `Document.cs`
El contenido de un panel como datos, de arriba abajo, para que cualquier cliente lo dibuje igual. Las alturas y separaciones van en píxeles de interfaz.

| Elemento | Qué es |
| --- | --- |
| `Tone`, `Ink`, `TextSize` | Qué significa un color (normal, tenue, desactivado, dorado, bueno, malo, río, batalla, hombres, organización , los fondos de las barras, el borde de una tarjeta y los niveles intermedios de moral) o el color propio de una nación (`Ink.Nation`); `Ink.Mood` colorea una moral. Tres tamaños de letra. |
| `Icon` (`ResourceIcon`, `BattalionIcon`, `BuildingIcon`, `ScienceIcon`) | Icono de un recurso, de un tipo de batallón, de un edificio o de la ciencia (`ciencia.png`, junto a los de recursos: en el aviso «Ciencia sin elegir», que lo lleva en `Alert.Icon`, en la pestaña Ciencia y junto a sus puntos; sin imagen, nada). `Icon.Slug` escribe un nombre como los de sus ficheros. El de un recurso es su imagen de `Assets/ResourceIcons` si la hay (`ResourceIcon.Slug`), o el dibujado. El de un edificio es una imagen de `Assets/BuildingIcons` con su nombre (`BuildingIcon.Slug`: en minúsculas, sin tildes y con guiones, «Central eléctrica» es `central-electrica`); si no la hay, no se dibuja nada. Los botones y las etiquetas (`Label`) pueden llevar uno. |
| `Heading`, `Label`, `Paragraph` | Texto en negrita, una línea sin ajustar (con tooltip opcional) y un párrafo ajustado al ancho. |
| `Info`, `Row` | Etiqueta y valor en dos columnas (con icono y tooltip opcionales); texto a la izquierda y a la derecha de una línea. |
| `Button`, `ButtonRow`, `Stepper`, `LabelAndButton` | Un botón a todo el ancho con su acción (`Press()` la ejecuta solo si está activo), botones que se reparten una fila (pestañas), un número entre botones que lo bajan y suben, y una línea con un botón pequeño a la derecha. |
| `Bar`, `Space` | Una barra de progreso (con una muesca roja opcional, como el punto donde se rompen las unidades) y un hueco. |
| `Banner`, `UnitEntry` | El nombre de una nación tras un cuadro de su color, con una nota a la derecha; una unidad en una lista (nombre, nota, una línea y sus barras de hombres y organización), y las que no caben se cuentan con `Document.Hidden` («y N más»). |
| `Pair`, `Columns`, `Distribution` | Etiqueta con su valor pegado al borde derecho (en dos tamaños); una etiqueta y textos en columnas a distancias fijas del borde derecho (cada uno con su tooltip); una barra repartida entre partes con su leyenda debajo (la moral por niveles). |
| `Document` | La lista de elementos y, si tiene, la acción de su botón de cerrar. |

### `NationScreen.cs`
La pantalla de la nación (botón «Nación» o tecla N) como datos: `Visible`, las pestañas que se ven (`Tabs`: Marina solo con `GameSession.HasNavy` y Fuerza aérea solo con `HasAirForce`), la pestaña (`Tab`, el resumen si la elegida está oculta), el orden de cada tabla, la era que muestra la ciencia y la plantilla elegida. El tiempo sigue corriendo mientras está abierta. `Page()` da lo que muestra la pestaña actual (`NationPage`); «Ver» la cierra y centra el mapa en la provincia o la unidad.

| Elemento | Qué es |
| --- | --- |
| `NationTab` | Pestañas: `Summary` (Resumen), `Cities` (Ciudades), `Provinces` (Provincias), `Science` (Ciencia), `Army` (Ejército), `Templates` (Plantillas), `Units` (Unidades), `Store` (Almacén), `Diplomacy` (Diplomacia), `Trade` (Comercio) y `Statistics` (Estadísticas). |
| `Summary(...)` | Resumen: población (total, asentada, en unidades, migrando), fertilidad media, moral media con su reparto por niveles (`MoodDistribution`), territorio, ciencia por día y, por rama, su parte y el avance en curso, cada institución (adoptada, cuánto de tu población la tiene o sin nacer), comida con sus días de reserva y el resto de recursos conocidos con su almacén, balance diario y lo que queda en sus bolsas; al final, la victoria (`VictoryProgress`): tu puntuación y puesto, los avances que sabes, las naciones libres que quedan y, si ya hay, el ganador. |
| `Cities(...)` | Tabla de ciudades: población / capacidad, moral, fertilidad, fiestas, enviar colonos o entrenar guerreros y «Ver». |
| `Provinces(...)`, `Work(...)` | Tabla de todas las provincias: población, moral, fertilidad, terreno, migrantes en camino, lo que tiene en curso (la obra y lo que entrena su ciudad: la primera con sus días y su barra, cuántas más hay y todas en el tooltip) y «Ver». |
| `ProvinceCells(...)` | Celdas de población, moral (con tooltip de causas) y fertilidad, comunes a las dos tablas. |
| `SortableTable(...)`, `Sort(...)` | Tablas cuyas cuatro primeras columnas (nombre, población, moral, fertilidad) ordenan al pulsarlas; otra pulsación invierte el orden; los nombres van de la A a la Z y los números de mayor a menor. Los nombres se ordenan con `TextFormat.SpanishSortKey`. |

### `NationScreen.Science.cs`
| Función | Qué hace |
| --- | --- |
| `Science()`, `InstitutionBadge(...)`, `Branch(...)`, `TechCard(...)` | Pestaña Ciencia: puntos al día (con su desglose y los guardados), una segunda fila con un botón por era para ver sus niveles (`_scienceEra`; por defecto, la primera con avances por descubrir; «(+)» y su tooltip avisan del recargo si falta su institución), la institución que abre la era mostrada a la derecha, su estado con `InstitutionStatus` (adoptada, cuánto se ha extendido y el botón para adoptarla con oro, o sin nacer), y una columna por rama con su prioridad (− y +) y su parte, lo que investiga con barra y tiempo estimado (o un aviso para elegir), y sus niveles: cada uno con lo que hace falta para abrirlo y una tarjeta por avance con su estado, coste (con el descuento por vecinos o el recargo por institución), efecto, requisitos, los edificios y batallones que permite y el botón Investigar. |

### `NationScreen.Military.cs`
| Función | Qué hace |
| --- | --- |
| `Army()`, `UnitActivity(...)` | Orden de batalla, con el mantenimiento diario del ejército (en rojo si no se paga): cada cuartel con sus unidades en árbol, después las unidades sin cuartel y las flotas, con ubicación, hombres, organización, suministro, estado y «Ver». |
| `Store()`, `Stock()` | Pestaña Almacén (en `NationScreen.Store.cs`): dos tablas, los recursos y el equipo. `Stock` da una fila por recurso conocido con lo que hay, lo que entró y salió el último día por producción, comercio, consumo, ejército y talleres, el balance y en cuántos días se agota si baja. |
| `Units()` | Pestaña Unidades (en `NationScreen.Units.cs`): todos los modelos de batallón y de barco por grupo y línea, con hombres, ataque, defensa, organización, velocidad, días de instrucción, el equipo de un batallón y el coste; el que se entrena ahora en negrita, los antiguos y los por descubrir (con su avance) apagados; su tooltip (`Description`) dice qué lo distingue, dónde se instruye y qué requiere. |
| `Equipment()` | La tabla de equipo de la pestaña Almacén: una fila por suministro que la nación puede fabricar o del que guarda piezas, con las que hay en el almacén (y los batallones que arman), lo que fabrican al día sus talleres, lo que esperan sus batallones para modernizarse y lo que cuesta el equipo de un batallón; arriba, cuántos talleres tiene y cuántos están parados. |
| `Templates()` | Diseñador de unidades: tus plantillas a la izquierda (nueva, duplicar, borrar); a la derecha los batallones de la elegida (hasta 12, con «Quitar»), botones para añadir los que conoces y lo que cuesta, su mantenimiento y cómo lucha una unidad de ese diseño (con `TextFormat.UpkeepText` y `TextFormat.TrainingDaysText`). |
| `Diplomacy()`, `RelationCell`, `OpinionCell`, `PeaceTimeButtons`, `WarScoreCell`, `PeaceButtons`, `TermsButton` | Cada nación que sigue en la partida: relación (en guerra y desde cuándo, vasallo o señor, aliados, tregua o paz; en el tooltip sus aliados, su señor, sus vasallos y las reparaciones), lo que piensa de nosotros (con sus razones), su poder militar frente al tuyo, provincias, lo tomado y perdido, la puntuación de guerra (con su desglose) y los botones: en paz, guerra, alianza (o romperla), pacto (o romperlo), pedir paso, dar paso (o cerrarlo) y regalo (`PactButton`, `Agreement`); con un vasallo, anexionarlo o liberarlo; en guerra, paz blanca, exigir lo ocupado, tributo, vasallo y ceder lo ocupado. |

### `NationScreen.Navy.cs`
La pestaña Marina (`Navy()`, un `TablesPage`): las agrupaciones (tamaño, ubicación, barcos con su combustible, tripulación, organización, Flota, misión y si están en puerto, en el mar, combatiendo o sin combustible), el mando naval (Armada y Flotas con su almirante y lo que mandan, «Disolver» y «Formar» en el puerto mayor), los astilleros (gradas ocupadas y lo que construye cada uno), la cola de construcción (astillero, progreso, días que quedan, coste al día y botones para subir, bajar y cancelar) y una fila por línea de barcos con el modelo que se encargaría, sus cifras y «Encargar».

### `NationScreen.AirForce.cs`
La pestaña Fuerza aérea (`AirForce()`, un `TablesPage`): las unidades aéreas (tamaño, base, aviones, organización, división y misión, con «Ver» su base y «Aterrizar»), los cuarteles generales (Mando aéreo y divisiones con su general y lo que mandan, «Disolver», y «Formar» una División aérea o el Mando aéreo en el aeródromo más cercano a la capital), los aeródromos (escuadrillas y las que se forman) y una fila por tipo de avión con el modelo, sus cifras por escuadrilla y «Formar» una escuadrilla.

### `AI/AiPlayer.Air.cs`
| Elemento | Qué hace |
| --- | --- |
| `BuildAirForce()`, `OrganiseAirForce()` | Con aeródromo (en `BuildOrder` tras el dique seco: uno, y otro por cada 8 ciudades), forma una escuadrilla al día hasta tener 3 más una por ciudad (36 como mucho): la mitad cazas, una cuarta parte en picado, una sexta tácticos y el resto estratégicos; `ChooseProduction` fabrica sus aviones (`AirLines`). Une sus unidades de un tipo en cada base, forma una División aérea por cada 6 escuadrillas y el Mando aéreo con dos divisiones, y pone cada unidad suelta bajo una división a su alcance. |
| `AirMissions(enGuerra)`, `Mission(unidad, enemigos)` | En guerra, cazas y bombarderos en picado sobre su batalla más cercana a su alcance (los tácticos también, o si no sobre la ciudad enemiga más cercana) (los cazas si no, sobre los objetivos de sus bombarderos o su provincia más cercana al enemigo), bombarderos sobre la ciudad enemiga más cercana y aviación naval sobre la flota enemiga más cercana; en paz, todas en tierra. |

### `NationScreen.Trade.cs`
| Función | Qué hace |
| --- | --- |
| `Trade()`, `Offers(nación)`, `OfferRow`, `Amount` | Pestaña Comercio: tus acuerdos en curso (con los días que quedan y «Cancelar») y las ofertas de cada nación en paz: lo que le sobra, por oro o por lo que más le falta de lo que te sobra a ti, y lo tuyo que te compra por oro, con «Firmar». El tooltip de cada cantidad da su valor en oro. |

### `NationScreen.Statistics.cs`
| Función | Qué hace |
| --- | --- |
| `StatisticsMetric`, `Metric`, `Statistics()` | Pestaña Estadísticas: botones para elegir la cifra (población, ejército, oro al día, provincias o ciencia al día) y una gráfica con una línea por nación desde el principio, más la cifra de hoy. Puntos y marcas van de 0 a 1 en cada eje; la nación del jugador va primero en la leyenda y encima del resto. |

### `NationPages.cs`
Lo que muestra cada pestaña de la pantalla de la nación, para que el cliente lo dibuje.

| Elemento | Qué es |
| --- | --- |
| `Table`, `Column`, `TextCell`, `ButtonsCell`, `CellBar` | Una tabla: columnas con su ancho, filas de celdas (texto con sufijo pequeño, tooltip, muestra del color de una nación o barra; o botones que se reparten la celda), las columnas que ordenan, el orden actual con la acción de cambiarlo y el texto que se ve si no hay filas. |
| `TablePage`, `TablesPage`, `SummaryPage` | Una tabla, bajo una línea en negrita si la hay (Ciudades, Provincias, Ejército, Diplomacia, Unidades); varias una debajo de otra (Almacén); dos columnas de cifras (Resumen). |
| `SciencePage`, `InstitutionBadge`, `BranchColumn`, `TechLevel`, `TechCard` | La ciencia: puntos al día, instituciones de la era mostrada, botones de era y, por rama, prioridad, estado, niveles y tarjetas de avance. |
| `TemplatesPage`, `TemplateSlot` | El diseñador de unidades: plantillas y sus acciones, huecos de la elegida, batallones para añadir y sus cifras (un `Document`). `Rename` es el botón «Cambiar nombre»; mientras se renombra, `Renaming` trae «Aceptar» y «Cancelar» y el cliente edita `NationScreen.TemplateNameDraft` en lugar del nombre (Intro acepta, Esc cancela, y las teclas no llegan al mapa). |
| `StatisticsPage`, `ChartSeries`, `ChartTick` | Las estadísticas: botones de cifra, título, una línea por nación (puntos de 0 a 1, color, última cifra, si es la del jugador) y las marcas de los ejes. |




### `Portraits.cs`
`Portrait`: cómo es un oficial para su retrato. `Of(oficial, era, color)` inventa la cara a partir de su nombre y su id (siempre la misma, sin guardar nada): mujer u hombre según el nombre (`Officer.IsFemale`), tono de piel, color y peinado del pelo, barba, anchura de cara y mandíbula, edad, gesto, nariz, si va sin tocado y el tono del fondo; y lleva su habilidad, su rango, la era y el color de su nación. `Greying`: cuánto ha encanecido, con la edad y la habilidad. `PhotoGroups`: los grupos de retratos pintados que le valen, del mejor al peor: con nombre rango-era-sexo-arma: de su rango (`RankSlugs`: coronel a mariscal), su era (`EraSlug`), su sexo (`SexSlug`) y su arma (`BranchSlug`: ejercito, marina, aviacion; `CavalrySlug`, caballeria, si manda un regimiento sobre todo de caballería: `Of(..., caballería)`), luego de los rangos más cercanos y, para caballería, marinos y aviadores, después los del ejército; `Pick` elige uno dentro del grupo. Cómo crear esos retratos: `docs/RETRATOS.md`.
### `Models.cs`
`Models`: qué maqueta representa cada cosa en el mapa, por su nombre. `Of(modelo)`, por su `Key`: soldados vestidos según su época (de guerreros a infantería pesada y ballesteros con casco de bronce, arcabuceros, piqueros y mosqueteros con tricornio, infantería ligera, fusileros, ametralladores y médicos con casco de acero), tropas de montaña, paracaidistas, jinetes (también catafractos, caballeros y carros), caballería mecanizada, catapultas y trabuquetes, cañones, obuses, antiaérea, ingenieros, tanques, bombarderos y cada barco. `Of(unidad)`: una flota, su barco más fuerte; un regimiento, la línea más numerosa en el modelo de su primer batallón; los colonos y los cuarteles, ninguna (siguen con su ficha: el triángulo de los colonos, las letras del cuartel). `Of(edificio)`: granja y granero un molino, taller y fábrica una forja, biblioteca y universidad una torre, mercado y banco un mercado, murallas y castillo un fuerte, puerto y dique un puerto. `City(capital)`: castillo o pueblo. `CityIcon(población, capital, moderna)`: el icono de una ciudad en `Assets/BuildingIcons` (`CityIcons`): cabaña con menos de 2.000 habitantes, pueblo con menos de 10.000, y desde ahí (y siempre la capital) `capital`; en la época moderna, la capital y las de 10.000 o más, `ciudad-moderna`.
### `Display.cs`
`DisplaySettings`: opciones de pantalla guardadas en `pantalla.json`, en la carpeta de partidas: si los iconos del mapa se mueven (`Animations`) y si unidades y edificios se dibujan como maquetas 3D o como etiquetas: fichas OTAN e iconos (`UnitModels`, falso salvo que se elija). `SetAnimations` y `SetUnitModels` los cambian y los guardan; se eligen en las opciones (`SettingsMenu`).
### `Audio.cs`
| Elemento | Qué hace |
| --- | --- |
| `SoundCue` | Los efectos: clic, confirmación, alerta, batalla, guerra, paz, obra, descubrimiento, ciudad fundada, monedas y campana. |
| `Soundtrack` | Las pistas: medievales hasta la Edad Media y orquestales desde la era de los Descubrimientos, unas para la paz y otras para la guerra (`For(era, enGuerra)`); la del menú (`Menu`: «The Britons», de Kevin MacLeod) y la de la pantalla de carga (`Loading`: «Lord of the Land», de Kevin MacLeod, que también suena en paz en las primeras eras, así que sigue sonando al empezar la partida). |
| `AudioSettings` | Volumen de la música y de los efectos (0 a 1), guardado en `sonido.json` en la carpeta de partidas. `ChangeMusic` y `ChangeSounds` lo suben o bajan de cuarto en cuarto (entre silencio y el máximo) y lo guardan; `Label` lo escribe («50 %», «No»). Se eligen en las opciones (`SettingsMenu`). |

### `GameController.Audio.cs`
| Función | Qué hace |
| --- | --- |
| `Playlist` | La música del momento: la era del jugador y si está en guerra. |
| `Play(efecto)`, `TakeSounds()` | Los sonidos pendientes (como mucho 32), que el cliente recoge cada fotograma. |
| `Hear(evento)` | El sonido de lo que ocurre al jugador (`GameSession.Happened`): guerra (tambores), paz (fanfarria), batalla, asedio o revuelta (campana), regalo (monedas), y sus ciudades, obras y avances. `Show` añade la campanilla de confirmación a cada orden que sale bien. |
| `ListenForAlerts()` | Cada hora de juego, si aparece una alerta grave nueva, suena. |


### `GameController.Objectives.cs`
| Elemento | Qué hace |
| --- | --- |
| `ObjectiveCard`, `ObjectiveCard()` | La tarjeta del objetivo actual bajo las alertas: «Objetivo n de 9», el objetivo, cómo cumplirlo, «Ir» y el botón para plegarla (`ObjectivesFolded`). Desaparece cuando se cumplen todos. |
| `GoTo(objetivo)` | Adónde lleva «Ir»: los colonos, la pestaña Ciencia, la capital (en Edificios, Ejército o General), los exploradores, el Resumen o la Diplomacia. |

### `GameController.Victory.cs`
| Elemento | Qué hace |
| --- | --- |
| `GameOverWindow`, `GameOverOpen`, `GameOver(navegador)` | La ventana del final, que se abre una vez cuando alguien gana o cae la nación del jugador y para el tiempo: «¡Victoria!» o «Derrota», cómo ha sido, la clasificación final y los botones «Seguir jugando» (o «Seguir mirando») y «Menú principal». Se dibuja en `GameScreen.Victory.cs`. |
### `GameController.Alerts.cs`
Las alertas bajo la barra superior.

| Elemento | Qué hace |
| --- | --- |
| `Alert` | Una alerta: texto corto, gravedad (`Tone`), explicación con la lista de lo afectado (hasta 5) y qué hace al pulsarla. |
| `Alerts()` | Las que se cumplen ahora, de más a menos urgente: hambre (o comida para menos de 30 días si se come más de lo que se cosecha), ejército sin paga, provincias atacadas, provincias sitiadas, provincias descontentas o camino de la rebelión, unidades sin suministro, unidades que pierden hombres por el frío, el desierto o la altura, obras terminadas en los últimos 3 días (en verde; cada clic lleva a la siguiente) y ramas de la ciencia sin elegir. Las de comida, paga y ciencia abren la pestaña de la nación que toca. |
| `ProvinceAlert(...)`, `UnitAlert(...)`, `NextClick(...)` | Alertas sobre provincias o unidades: cada clic centra el mapa en la siguiente (`ViewProvince`, `ViewUnit`). |

### `GameController.Dialogs.cs`
La ventana para editar una unidad tuya (botón «Editar unidad» de su panel) y la de construir una carretera o un ferrocarril (botón de los ingenieros en una ciudad o cuartel): su estado, lo que muestran y sus órdenes. El tiempo se para mientras alguna está abierta.

| Elemento | Qué es |
| --- | --- |
| `EditingUnitId`, `OpenUnitEditor(unidad)`, `CloseUnitEditor()`, `UnitEditor()`, `UnitEditorWindow` | Abre y cierra la ventana y da su contenido: a la izquierda el nombre, las partes de una brigada o división con «Separar» (`Parts`), «Unir» (batallones de dos regimientos, o barcos) e «Incorporar» o «Incorporarse a» (otra unidad como parte, o esta en la otra), y las tropas; a la derecha, en unidades de combate y cuarteles, el oficial. Se cierra sola si la unidad desaparece. |
| `UnitName`, `RenameEditedUnit()` | El nombre que se escribe, «Renombrar» (o Intro) y «Volver al nombre automático». |
| Batallones | Cada batallón o barco es un botón que se marca; «Separar los marcados» los saca juntos en una unidad nueva. |
| Uniones | Botones para unir las demás unidades tuyas de la provincia, diciendo qué pasa con su oficial. |
| `OfficerColumn`, `OfficerCard`, `ReserveOfficer` | El oficial al mando con sus rasgos (virtudes en verde, defectos en rojo) y «Relevar del mando»; la reserva del arma de la unidad, con «Asignar» y «Retirar» para cada oficial; y el botón para reclutar uno de esa arma. |
| `RoadWindowOpen`, `OpenRoadWindow(desde, tipo)`, `CloseRoadWindow()`, `RoadWindow()` | Tus otras ciudades y cuarteles, del más cercano al más lejano (el más cercano que lo necesita ya elegido); los ya unidos, apagados; tramos nuevos, coste, trabajo y ciudades que une por el camino; «Construir» y «Cancelar» (o Esc). |
| `PlannedRoute` | La ruta elegida, que `RoadLayer` dibuja en el mapa. |

### `GameController.Battle.cs`
La ventana de una batalla (clic en sus espadas). Se actualiza en directo y el tiempo sigue corriendo (Espacio lo para).

| Elemento | Qué es |
| --- | --- |
| `BattleWindowOpen`, `OpenBattle(...)`, `CloseBattle()`, `BattleWindow()`, `BattleWindow` | Abre, cierra (Esc o «Cerrar») y da el contenido de la ventana; «Ir a la provincia» centra el mapa en la batalla. |
| `LandBattle(...)`, `FireBalance` | Batalla en tierra: estado y duración (o quién ganó), terreno, defensa y frente, y una barra con el fuego por hora de cada bando. Terminada, muestra cómo acabó la última hora y las bajas. |
| `BattleSide(...)`, `Stat(...)`, `Casualties(...)` | Cada bando, como `Document`: hombres, bajas, organización (con la marca donde se rompen), fuego, batallones en el frente, detrás y en reserva, armas combinadas y sus unidades. |
| `BattleUnit(...)` | Cada unidad: nombre, oficial, hombres, cuántos batallones combaten, suministro y barras; al pasar el ratón, su mando, general, oficial y cada batallón con su puesto y su fuego. |
| `Chart(...)`, `OrganisationChart` | La organización de cada bando hora a hora, con la línea donde se rompen; al pasar el ratón, los datos de esa hora. |
| `NavalBattle(...)` | Batalla en el mar: cada nación con sus barcos, tripulantes, organización, fuego y flotas. |
| `OpenFirstBattle()` | Para `--panel battle`: abre la primera batalla en curso. |

### `GameController.Map.cs`
Lo que se dibuja sobre las provincias, ya en posiciones de pantalla (con la cámara) y sin lo que queda fuera de ella: `Markers()` devuelve `MapMarkers`.

| Elemento | Qué es |
| --- | --- |
| `Cities()`, `CityMarker` | Cada ciudad en tierra explorada, como un punto (el cliente le da tamaño según su población), sus edificios con zoom alto, el color de su nación, su población (más casas cuanto más poblada), si es la capital, un poco más grande con zoom, y su nombre si hay sitio (con zoom, o la capital desde más lejos). |
| `ProvinceLabels()`, `ProvinceLabel`, `ProvinceNamesZoom`, `ProvinceNamesFullZoom` | Con zoom desde `ProvinceNamesZoom` (3,5), el nombre de cada provincia explorada en pantalla, en su centro (encima de la ciudad si la tiene), apareciendo hasta verse entero en `ProvinceNamesFullZoom` (4,5). |
| `NationLabels()`, `NationLabel` | El nombre de cada nación sobre la tierra suya que el jugador ha explorado: en su centro (media circular de las longitudes), del tamaño que ocupa en pantalla, oculto si se ve muy pequeña y desvanecido al acercarse mucho. |
| `Counters()`, `CounterKind`, `UnitCounter` | Cada unidad que no va embarcada y que el jugador ve (`GameSession.CanSee`; la niebla oculta las demás): dónde está (a mitad de camino si marcha; las que están en una provincia, de lejos unas encima de otras en su centro, sobre la ciudad, con la seleccionada encima, y desde `UnitSpreadZoom` (6) desplegadas en un círculo alrededor, más ancho cuantas más hay), su color, si está seleccionada, su tipo (combate, flota, cuartel o colonos), su arma (`Unit.Function`), su letra, cuántas van a bordo, sus marcas de tamaño (`Unit.Echelon`), sus barras y, con ella, su ruta (`Route`: entera para la seleccionada y para tus demás unidades en marcha si hay zoom, cruzando el borde del mapa por el lado corto), la línea a su cuartel (si está seleccionada) y la flecha de su ataque. Se encogen con el zoom lejano. |
| `Battles()`, `BattleMarker`, `BattleSummary(...)`, `NavalBattleSummary(...)` | Espadas cruzadas sobre cada batalla que el jugador ve, en tierra o en el mar; su tooltip (los dos bandos, su organización y el terreno) solo se calcula al pasar el ratón. |
| `Deposits()`, `DepositMarker`, `DepositIconsShown`, `DepositIconZoom` | En el modo recursos y con zoom desde `DepositIconZoom` (3): cada provincia explorada en pantalla con yacimientos que el jugador conoce (solo el del filtro, si lo hay), con sus recursos del más rico al más pobre y el tamaño de sus iconos (crece con el zoom); encima de la ciudad si la hay. `GameScreen.DrawDeposits` los dibuja en fila sobre una franja oscura. |

### `Menus.cs`
Los menús fuera de la partida como datos. Para cambiar de pantalla piden a un `IMenuNavigator` (lo pone el cliente) que muestre el menú principal, el de nueva partida o el de cargar, que empiece o cargue una partida o que salga.

| Elemento | Qué es |
| --- | --- |
| `MainMenu` | Pantalla inicial: "Continuar" (carga la última partida guardada), "Nueva partida", "Cargar partida", opciones (`Settings`, un `SettingsMenu`), historial de versiones, créditos (`CreditsOpen`) y salir; mientras hay una ventana abierta, los demás botones no hacen nada, y `CloseWindows` (Esc) las cierra. |
| `Credits` | Los créditos: quién hizo el juego y de quién son la música, los sonidos, los gráficos, las tipografías y los datos del mapa, con sus licencias (`Lines`, en el mismo formato que el historial), y la línea de copyright del pie de la pantalla inicial (`Copyright`). |
| `SettingsMenu` | Las opciones, iguales desde la pantalla inicial y desde el menú de pausa (`GameController.Settings`, que para el tiempo y se cierra con Esc): volumen de la música y del sonido (un valor entre - y +), animaciones (sí o no) y mapa (figuras 3D o fichas), como `OptionRow`, y «Cerrar». |
| `NewGameMenu`, `OptionRow` | Nueva partida, fila a fila: tipo de mapa, tamaño (solo en el aleatorio), semilla, número de jugadores, dificultad (con su descripción y los recursos con los que empiezas), "Comenzar" y "Volver". |
| `LoadGameMenu`, `SaveRow` | Lista de partidas guardadas, de la más reciente a la más antigua, para cargar o borrar (pide confirmación). |
| `LoadingJob` | Genera el mundo en segundo plano para una partida nueva o una guardada, diciendo qué hace en cada momento; lo que el cliente necesita para dibujar el mapa lo prepara en el mismo hilo (una función que recibe). Da la partida al terminar, o el error. |

### `GameController.Menus.cs`
| Elemento | Qué es |
| --- | --- |
| `MenuOpen`, `HelpOpen`, `ChangelogOpen`, `TimeStopped` | El menú de pausa, la ayuda y el historial, y si hay abierta alguna ventana que para el tiempo (esas tres, el diálogo de la ciudad, el editor de unidades o la ventana de carreteras; las batallas no lo paran). |
| `Escape()` | Esc cierra la ayuda, la batalla, la ventana de carreteras, el historial o la pantalla de la nación, por ese orden; si no, cancela la elección del destino de una migración o quita la selección, y si no hay nada, abre o cierra el menú. |
| `PauseMenu(navegador, versión)` | Los botones del menú de pausa: continuar, guardar la partida, ayuda, historial de versiones, menú principal y salir. |

---

## 4. `src/Conquer.Client` — ventana, gráficos e interfaz

### `Program.cs`
Punto de entrada. Comprueba OpenGL 3.3 (`GlSupport.Ensure`), pone el formato de números en español (a mano, sin depender de ICU) y lee los argumentos.

| Elemento | Qué es |
| --- | --- |
| `StartOptions.Parse(args)` | Opciones de línea de comandos para pruebas: `--new random|earth`, `--seed`, `--players`, `--difficulty veryeasy|easy|normal|hard|veryhard`, `--size tiny|small|medium|large`, `--days` (funda la capital y avanza N días), `--zoom`, `--at longitud,latitud` (centra la vista ahí), `--mode terrain|political|population|mood|fertility|resources|institutions`, `--nation summary|cities|provinces|science|army|templates|diplomacy` (abre la pantalla de la nación), `--panel buildings|army|regiment|march|edit|found` (una pestaña de la provincia seleccionada, un regimiento de muestra, en marcha o con su ventana de edición abierta, o el diálogo para nombrar la primera ciudad), `--load fichero.conquer` (carga una partida guardada), `--menu new|load` (abre esa pantalla del menú) y `--screenshot fichero.png` (guarda una captura del juego y se cierra). |
| `QuickStart` | Partida que empieza directamente, sin menús. |

### `ConquerApp.cs`
| Función | Qué hace |
| --- | --- |
| `IScreen` | Interfaz de una pantalla: `Frame(dt)` actualiza y dibuja un fotograma. |
| `IAudibleScreen` | Una pantalla con música y sonidos propios (la partida): su lista de pistas (`Playlist`) y los sonidos pendientes (`TakeSounds`). Las demás suenan con la música del menú. |
| `ConquerApp(options)` | Crea la ventana con OpenGL 3.3: 1600×900, o menor si no cabe en el monitor (`InitialSize`), con antialiasing (MSAA 4×) si la tarjeta lo admite. |
| `UiScale`, `ScreenSize`, `PixelScale` | Las pantallas se colocan en píxeles lógicos (`ScreenSize`). Si la ventana es menor de 1280×820, `UiScale` (< 1) encoge toda la interfaz en proporción para que nada se salga; el ratón se divide por la misma escala. `PixelScale` son los píxeles reales por píxel lógico (pantallas HiDPI por la escala). |
| `Run()`, `Quit()`, `Show(pantalla)` | Arranca, cierra, cambia de pantalla al acabar el fotograma. |
| `OnLoad()` | Inicia OpenGL, la fuente, la interfaz, el sonido (`AudioPlayer`) y los eventos de ratón y teclado; muestra el menú o la partida rápida. |
| `OnRender(dt)` | Cada fotograma: limpia, dibuja la pantalla actual, la interfaz y el tooltip, y hace sonar lo que toca (`PlayAudio`: el clic de cualquier botón pulsado, los sonidos y la música de la pantalla). |
| `CaptureIfRequested()` | Con `--screenshot`, guarda la imagen tras unos fotogramas y cierra; en los menús espera a que esté su fondo. |
| `ReadVersion()` | Lee la versión del ejecutable (la del `.csproj`). |

### `Audio/AudioPlayer.cs`
`AudioPlayer`: el sonido, con OpenAL (OpenAL Soft, incluido para Windows, Linux y macOS) y NVorbis para leer OGG. La música y los efectos van dentro del ejecutable (`Assets/Audio`, recursos `Audio/Music/…` y `Audio/Sfx/…`). Sin dispositivo de sonido, el juego sigue en silencio (`Available`).

| Función | Qué hace |
| --- | --- |
| `AudioPlayer()` | Abre el dispositivo, decodifica los efectos (`Decode`) y crea 8 voces para ellos y una fuente con 4 búferes para la música. |
| `Update(dt, pistas, sonidos, ajustes)` | Cada fotograma: suena cada efecto pedido (`Ring`: el mismo no se repite antes de 0,25 s) y la música sigue (`Stream`). |
| `Stream(...)`, `StartTrack()`, `Fill(búfer)`, `StopTrack()` | La música se lee del OGG en trozos de medio segundo. Al acabar una pista empieza otra al azar de la lista (no la misma); si la lista cambia y ya no tiene la que suena, esta se funde en 2 s y empieza otra de la nueva. |

### `Screens/MenuScreens.cs`
| Elemento | Qué es |
| --- | --- |
| `MenuNavigator` | El `IMenuNavigator` del cliente: cambia de pantalla o cierra el juego. |
| `MainMenuScreen`, `NewGameScreen`, `LoadGameScreen` | Dibujan `MainMenu`, `NewGameMenu` y `LoadGameMenu` sobre el fondo del mapa (`MenuBackground`); la lista de partidas guardadas se desplaza con la rueda. La pantalla inicial lleva el copyright al pie. Esc cierra el historial, los créditos o las opciones, o vuelve al menú principal. |
| `SettingsView` | Dibuja las opciones (`SettingsMenu`) en el centro, sobre un fondo oscurecido: una fila por opción y «Cerrar» abajo. La usan la pantalla inicial y la partida. |
| `LoadingScreen` | Muestra cómo va un `LoadingJob` (con `MapRenderer.Prepare` para los píxeles del mapa), con su música (`Soundtrack.Loading`), y al terminar abre `GameScreen`; si falla, dice por qué. |

### `Screens/GameScreen.cs`
La pantalla de juego. Clase parcial: el ejército en pantalla está en `GameScreen.Army.cs`, la ventana de edición de unidades en `GameScreen.UnitEditor.cs` la de cada batalla en `GameScreen.Battle.cs` y la de las carreteras nuevas en `GameScreen.Roads.cs`.

| Función | Qué hace |
| --- | --- |
| `GameScreen(...)` | Crea el `GameController` de la partida (que centra la vista en tus colonos o, en una partida cargada, en tu capital) y el renderizador. |
| `ApplyTestOptions(...)`, `ShowSampleArmy(...)` | Aplican `--days`, `--zoom`, `--at`, `--mode`, `--nation` y `--panel` (con `regiment`, entrena y selecciona una unidad de muestra bajo un cuerpo; con `march`, además la pone en marcha para ver su ruta; con `edit`, recluta oficiales y abre su ventana de edición, `ShowSampleOfficers`; con `found`, abre el diálogo para nombrar la ciudad de los colonos). |
| `Frame(dt)` | Fotograma: teclas, tiempo, refresco del mapa, dibujo del mapa, marcadores, paneles e interacción. |
| `Frame(dt)` (tiempo) | Llama a `GameController.Tick`, congelado con el menú, la ayuda, el historial o una ventana abierta, y pasa al renderizador el modo de mapa y el filtro de recursos cuando cambian. Recolorea el mapa cada día en los modos de datos y cada mes en el de terreno, por la nieve. |
| `HandleKeys` | Teclado (Espacio, 1-5, Tab, Inicio, N, F1, +/-, WASD, Esc, que llama a `GameController.Escape`). Mientras la ayuda (`HelpView`) está abierta el tiempo se para y el mapa no responde; con la ventana de edición de una unidad abierta, las teclas van a su nombre (Intro renombra, Esc cierra). |
| `HandleMapMouse()` | Rueda para el zoom, arrastrar para mover el mapa, clics. |
| `LeftClick()` | Abre la batalla o selecciona la unidad bajo el ratón; si no hay ninguna (o se elige el destino de una migración), `GameController.ClickProvince`. |
| `RightClick()` | `GameController.OrderMove`: mueve la unidad seleccionada (o ataca, si el destino es enemigo). |
| `NationRect` | Rectángulo de la pantalla de la nación. |
| `Center` | Atajo a `GameController.Center` (para `ShowSampleArmy`). |
| `DrawRivers()` | Dibuja los ríos con `RiverLayer` (con la vega verde solo en el modo terreno). Después, `RoadLayer` dibuja carreteras y ferrocarriles. Ninguno se ve en tierra sin explorar. |
| `DrawTopBar()` | Dibuja la barra superior (`GameController.TopBar`): color y nombre de la nación, población y moral, fecha y velocidades, recursos (icono, cantidad y cambio del día) y los botones «?» (ayuda), Nación y Menú, que abren pantallas del cliente. |
| `DrawAlerts()` | Dibuja las alertas (`GameController.Alerts`) en una columna bajo la barra superior, a la izquierda: un botón por alerta con una marca del color de su gravedad. Debajo, la tarjeta del objetivo actual (`DrawObjective`), plegada o desplegada. Se ocultan con la pantalla de la nación abierta. |
| `DrawSidePanel()` | Dibuja el panel derecho (`GameController.SidePanel`) con `DocumentView`, con su botón de cerrar; lo que no cabe se desplaza con la rueda o su barra, y vuelve arriba cuando cambia lo que muestra (`Document.Key`). |
| `DrawCityNaming()` | Dibuja el diálogo de `GameController.CityNamingDialog` para nombrar una ciudad al fundarla con colonos o al construirla: propone un nombre, se puede escribir otro o pedir otro al azar, avisa si no vale y la funda o empieza la obra (Intro confirma, Esc cancela). Mientras está abierto el tiempo se para y las teclas van a la caja de texto. |
| `Paragraph()` | Escribe un párrafo ajustado al ancho (lo usa la ventana de carreteras). |
| `DrawBottomBar()`, `ModeBarWidth` | Modos de mapa (`GameController.ModeButtons`, un botón de 120 píxeles por modo) y pistas de controles (`GameController.Hints`; si no caben, quita las del medio y deja siempre «F1: ayuda» al final; se ocultan con la pantalla de la nación abierta). |
| `DrawResourceFilter(barra)` | En el modo recursos, la fila de `GameController.ResourceFilterButtons` sobre los modos de mapa. |
| `DrawMessages()`, `HoverTooltip()` | Mensajes (`MessageLog`) en tiras redondeadas con una marca dorada (roja si algo falló) (más arriba si está abierto el filtro de recursos; con la pantalla de la nación abierta, solo el último, en la franja junto a los modos de mapa, con `DrawLatestMessageInStrip`) y tooltip de la provincia bajo el ratón (`GameController.MapTooltip`). |
| `DrawPauseMenu()` | Dibuja el menú de pausa (`GameController.PauseMenu`) con la versión del juego. |



### `Graphics/SpriteAtlas.cs`
`SpriteAtlas`: carga las imágenes incrustadas `Sprites/*.png` (las maquetas isométricas de `Assets/Sprites`) en una sola textura con mipmaps, colocadas por filas, y extiende el color de los bordes a los píxeles transparentes de alrededor (`Bleed`) para que no se oscurezcan al reducirse. `Draw(lote, nombre, pie, caja, color)` dibuja una maqueta tan grande como quepa en la caja, de pie en ese punto, y encima su imagen `-team` teñida del color de la nación; puede dibujarla en espejo para que mire a la izquierda.

### `Graphics/PortraitPainter.cs`
`PortraitPainter.Draw(lote, rect, retrato)`: si hay retratos pintados para el oficial en `Assets/Portraits` (cargados una vez con `LoadPhotos`, agrupados por `GroupOf`: «coronel-renacimiento-hombre-ejercito-03» es de «coronel-renacimiento-hombre-ejercito»), dibuja uno tal cual (`DrawPhoto`: el retrato ya muestra el rango), recortado para llenar el cuadro y con un marco fino del color de su nación; si no, pinta con formas el retrato de un oficial (`Portrait`): cabeza y hombros con su cara (piel, pelo que encanece con la habilidad, barba, cejas, nariz, arrugas si es mayor), el uniforme de su era en el color de su nación o de marino si manda una flota, el tocado de la época (casco de bronce con cresta, yelmo nasal, tricornio o bicornio, quepis, gorra de plato), galones de coronel o estrellas de general en los hombros y un pasador por cada estrella de habilidad más allá de la primera. Traído de la otra versión del juego.
### `Graphics/Motion.cs`
`Motion`: movimientos pequeños en bucle para los iconos del mapa (`Wave`, una oscilación; `Hop`, un salto; `Cycle`, de 0 a 1 una y otra vez). Cada cosa va desfasada de las demás según su id (ángulo áureo). Solo se dibujan; se apagan con «Animaciones» en el menú de pausa (`DisplaySettings`).
### `Screens/GameScreen.Markers.cs`
Dibuja los marcadores de `GameController.Markers`.

| Función | Qué hace |
| --- | --- |
| `DrawMarkers()` | Nombres de provincias, ciudades, nombres de naciones, fichas y batallas, en ese orden; guarda dónde quedan las fichas y las espadas para los clics. |
| `DrawCity(...)`, `CityDotRadius(...)`, `DrawProvinceName(...)`, `OutlinedText(...)`, `DrawNationName(...)` | La ciudad como un punto del color de su dueño, con borde oscuro y aro dorado si es la capital, de radio `CityDotRadius` (de 4 en un pueblo a 14, con la raíz de su población), su nombre debajo con contorno oscuro y, de cerca, los iconos de sus edificios en fila (con figuras 3D, las maquetas de los que no tienen icono); el nombre de una provincia, pequeño y con contorno; el nombre de una nación con sombra, en un tono claro de su color. |
| `DrawCounter(...)`, `DrawEchelon(...)`, `Bar(...)` | Fichas OTAN: la ruta como flecha verde (`PathArrow`), la línea al cuartel (verde si está a su alcance), la flecha roja del ataque y la ficha: dentro del marco, el símbolo de su arma (`MapIcons.NatoSymbol`: aspa para infantería, aspa con raya vertical para la motorizada, aspa con óvalo para la mecanizada, barra diagonal para caballería y exploradores, óvalo para blindados, punto para artillería, puente para ingenieros y alas para aviación); encima, las marcas de tamaño (III regimiento, X brigada, XX división, XXX cuerpo, XXXX ejército, XXXXX grupo de ejércitos); los cuarteles, con «HQ»; los colonos, un carromato (`MapIcons.Settlers`); las flotas, un casco bajo la letra de su barco y un punto por cada unidad a bordo. Barras de hombres (verde) y organización (ámbar). Cada ficha es un bloque en relieve, como las piezas de un wargame de tablero: sombra hacia abajo a la derecha, su grosor debajo, bisel en la cara y la cara sombreada de clara a oscura; el marco de la seleccionada late. Con las animaciones (`CounterMotion`), las fichas que marchan saltan, la seleccionada se balancea, las que combaten tiemblan y las flotas se mecen y dejan una estela (`Wake`). |
| `Smoke(...)`, `Bursts(...)` | Con las animaciones, humo que sube de las ciudades de más de 2000 habitantes (más oscuro y espeso si tienen taller o fábrica) y explosiones que se hinchan y se apagan alrededor de cada batalla. |
| `DrawFigure(...)` | Con figuras 3D, una unidad como maqueta (`Models`) sobre una peana del color de su nación, mirando hacia donde va, con sus marcas de tamaño encima, sus barras debajo y un anillo que late si está seleccionada. Las unidades de una provincia se ponen en fila, a la derecha de la ciudad si la hay. |
| `DrawBattleMark(...)` | Las espadas cruzadas, que laten; al pasar el ratón, su resumen; al hacer clic, la ventana de la batalla. |

### `Screens/GameScreen.UnitEditor.cs`
Dibuja la ventana de `GameController.UnitEditor`: a la izquierda el nombre (`NameSection`, con la caja de texto), las partes de una brigada o división con su botón para separarlas (`PartsSection`), lo que se puede unir o incorporar (`MergeSection`) y los batallones o barcos (`BattalionSection`), que en una división pueden no caber; a la derecha, en unidades con oficial, el oficial y la reserva (`OfficerSection`, `OfficerCard`). Las listas se cortan donde acaba la ventana. `ShowSampleOfficers(unidad)`, para `--panel edit`, recluta cuatro oficiales, pone el primero al mando y abre la ventana.

### `Screens/GameScreen.Battle.cs`
`DrawBattleWindow()`: dibuja la ventana de `GameController.BattleWindow`: título y estado, la línea del terreno, la barra del fuego (`FireBalance`), una columna por bando (cada una un `Document`, cuyas unidades se cortan donde acaba la columna) y el gráfico de organización (`OrganisationChart`, con los datos de la hora bajo el ratón). `_battleHitBoxes` guarda dónde están las espadas de cada batalla en el mapa, para abrirlas con un clic.

### `Screens/GameScreen.Roads.cs`
`DrawRoadWindow()`: dibuja la ventana de `GameController.RoadWindow` a la izquierda, para que se vea la ruta en el mapa: los destinos que caben (y cuántos más hay), y debajo lo que cuesta y une el elegido, con «Construir» y «Cancelar».

### `Screens/NationView.cs`
`NationView`: dibuja la pantalla de la nación (`NationScreen`): el panel opaco con el nombre, las pestañas que se ven (`NationScreen.Tabs`) y «Cerrar», y la página de la pestaña. Las tablas (`TablePage`, `TextCell`, `Header`, `Rows`, `Row`, `RowBackground`) llevan cabeceras que ordenan, filas alternas resaltadas bajo el ratón y desplazamiento con la rueda o la barra (guardado por pestaña); `Fit` estrecha todas las columnas en proporción si no caben en el ancho y `Clip` corta con «...» el texto que no cabe en su celda (entero en el tooltip); `TablesPage` pone varias tablas una bajo otra y se desplazan juntas; el resumen son dos `Document` que se desplazan juntos; las ramas de la ciencia y las cifras del diseñador también se desplazan cuando no caben; la ciencia (`Science`, `InstitutionBadge`, `Branch`, `TechCard`) coloca las tres ramas en columnas con sus tarjetas, de 88 píxeles o más altas si su descripción y sus notas ocupan más líneas (`TechCardHeight`); el diseñador (`Templates`) pone la lista de plantillas a la izquierda, su nombre con «Cambiar nombre» (o el campo de texto al renombrarla), los huecos y los batallones para añadir en el centro y sus cifras a la derecha. Las estadísticas (`Statistics`) dibujan la gráfica con sus líneas de guía y la leyenda a la derecha.


### `Graphics/CoastDistance.cs`
`CoastDistance.Build(mapa)`: para cada píxel del mapa, la distancia a la tierra más cercana en píxeles del mapa (0 en tierra, hasta 32), en un byte por píxel. Es una transformada de distancia euclídea exacta (Felzenszwalb y Huttenlocher), primero por filas y luego por columnas, que cruza la costura donde el mapa da la vuelta. El shader del mapa pinta con ella la plataforma, las olas y la espuma del mar.
### `Graphics/MapRenderer.cs`
Dibuja el mapa entero con un único shader.

| Elemento | Qué es |
| --- | --- |
| `ResourceFilter` | En el modo recursos, el único recurso que se muestra (o `null` para todos). |
| `IsResourceKnown` | Qué recursos conoce quien mira; los demás no se dibujan. |
| Shader de fragmentos | Para cada píxel de pantalla calcula el punto del mapa, busca la provincia en la textura de ids y la colorea según su dueño o su población. Con zoom alto mezcla las 3×3 celdas vecinas con pesos B-spline cuadráticos (`smoothRegions`, `strongest`) para trazar fronteras y costa como curvas suaves, sin escalones; el color del terreno sale solo de las celdas del mismo lado de la costa (`isWater`, en el canal verde de la textura de dueños) y la franja clara del agua sigue la distancia a la costa. Con zoom lejano compara con el píxel vecino. Ilumina el relieve desde el noroeste con la altura interpolada de la textura de detalle (`landDetail`, `relief`; baches finos sobre todo en la roca) y añade textura procedural según el suelo (`octave`, `noise`, `crowns`: moteado, copas de árboles, dunas), cada capa solo cuando es lo bastante grande en pantalla (`detailFade`); el agua toma su color de la profundidad, de turquesa en los bajíos a azul marino en las fosas, con una plataforma turquesa junto a la costa (de la textura de distancia a la costa, `uCoast`), un oleaje lento de dos capas, destellos de sol con zoom alto, olas que llegan a la orilla y espuma que rompe en ella (`waterDetail`, `uTime`). En el mapa político todo se ve como sobre pergamino viejo (`parchment`): colores hacia el crema, manchas suaves fijas al mapa y bordes tostados. Las fronteras nacionales son una línea oscura con, por dentro, una franja del color de la nación que se desvanece (`nationBorder`; su anchura sale de la textura de distancias a la frontera, y con zoom lejano la línea busca hasta 6 píxeles alrededor, `ownerDistanceNear`; en los modos de datos, una sombra); las provincias ocupadas llevan rayas del color del ocupante. Resalta la provincia seleccionada y la que está bajo el ratón, y aplica una viñeta suave hacia los bordes de la pantalla. |
| `Prepare(mapa)` | Prepara (fuera del hilo principal) los píxeles de ids, colores del terreno, detalle y distancia a la costa (`CoastDistance`). |
| `ProvinceAt(mapa, punto, zoom)` | Provincia que se ve en un punto, con la misma regla que el shader (para los clics). |
| `SmoothZoom` | Zoom a partir del cual las fronteras se suavizan. |
| `Refresh(partida, visibles, exploradas)` | Recalcula la niebla de guerra (va en el canal azul de la textura de dueños: las provincias exploradas fuera de `visibles` se dibujan en gris y más oscuras; las que no están en `exploradas`, casi negras con un leve moteado, sin dueño para que ninguna frontera las delate y sin resaltarse bajo el ratón) y el color de cada provincia según el modo (en el de cultura, el color de la nación de su gente; en el de religión, el de su fe; en el de terreno, la nieve del invierno), su dueño, el color de su dueño y, si está ocupada, el del ocupante (texturas pequeñas de 256×128). Si ha cambiado algún dueño, vuelve a calcular las distancias a la frontera (`BuildBorderDistances`: crece píxel a píxel desde las fronteras entre tierras de distinto dueño, hasta 5; las costas no cuentan). |
| `PopulationColor(densidad)` | Escala de color del modo población. |
| `ScaleColor(valor)` | Rojo-amarillo-verde de 0 a 1, para los modos moral y fertilidad. |
| `DepositColor(provincia)` | Color del modo recursos, de lejos: el del yacimiento principal que queda de los conocidos, o con filtro ese recurso más intenso cuanto más queda. Gris si no hay nada. De cerca (`DepositIcons`, que pone `GameScreen` según `GameController.DepositIconsShown`) no se colorea: los yacimientos van en iconos. |
| `ResourceColor(recurso)` | Color de cada recurso en el mapa y en la leyenda. |
| `InstitutionColor(provincia)` | Color del modo instituciones: el de la institución más reciente que ha llegado (más fuerte en las ciudades); gris si ninguna. |
| `Draw(cámara, ..., tiempo)` | Pasa los parámetros al shader (el tiempo mueve las ondas del agua) y dibuja; en el modo terreno las líneas entre provincias son más tenues, para que destaque el terreno. |

### `Graphics/TerrainColors.cs`
`Build(mapa)`: color de cada píxel según el bioma; en tierra, alturas algo más pálidas (el relieve lo ilumina el shader). El agua la colorea el shader según su profundidad. `BuildDetail(mapa)`: textura de detalle con la altura de la tierra (canal rojo, en escala de raíz cuadrada hasta `MaxHeight`; en el agua, su profundidad hasta `MaxDepth`) y cuánto suelo es bosque, arena y roca (`Ground`); al filtrarse, los biomas vecinos se funden.

### `Graphics/Batch2D.cs`
| Elemento | Qué es |
| --- | --- |
| `Rgba` | Color con `WithAlpha` (transparencia) y `Scale` (aclarar u oscurecer). |
| `Batch2D` | Acumula rectángulos, líneas y letras y los envía juntos a la GPU. |
| `Begin`, `Flush` | Empezar un fotograma / dibujar lo acumulado. |
| `Quad`, `Rect`, `Outline`, `Line` | Figuras básicas en píxeles de pantalla. |
| `PushClip`, `PopClip` | Solo se dibuja dentro de un rectángulo (con el scissor de OpenGL) hasta deshacerlo. |
| `Triangle`, `Gradient`, `Circle`, `RoundedRect`, `RoundedOutline`, `Shadow`, `Mix` | Triángulos con color por esquina, rectángulos en degradado vertical, círculos y anillos, rectángulos con esquinas redondeadas (con degradado opcional) y su borde, sombras suaves por capas y mezcla de dos colores. |

### `Graphics/RoadLayer.cs`
`RoadLayer`: carreteras y ferrocarriles como líneas entre los centros de las provincias, como en Hearts of Iron. La carretera es una franja clara con borde oscuro; el ferrocarril, una línea oscura con traviesas. Las obras del jugador se ven como trazos dorados y la ruta que se está eligiendo, como una línea discontinua brillante. Se ocultan con el mapa muy alejado (`MinZoom`) y en tierra sin explorar.

### `Graphics/BuildingIcons.cs`
`BuildingIcons`: los iconos propios de los edificios, de `Assets/BuildingIcons` y los de las ciudades (`Models.CityIcons`); los nombres, en su `LEEME.txt`. `Load` los carga una vez al abrir la partida, a 128 píxeles como mucho; `Has` dice si un edificio (o un nombre) tiene el suyo y `Draw` lo dibuja llenando un cuadrado. En el mapa, `DrawCity` pone con zoom alto los iconos de los edificios de la ciudad en fila bajo su punto (las maquetas, con figuras 3D, de los que no tienen). `DocumentView` los pone a la izquierda de los botones y las etiquetas que los piden, y aparta el texto solo cuando hay imagen.

### `Graphics/RiverLayer.cs`
`RiverLayer`: los ríos como cauces continuos. Al crearse une los segmentos de cada tramo en un solo camino y apunta la provincia de cada punto; `Draw(lote, cámara, modoTerreno, explorada)` dibuja cada tramo, cortado donde empieza la tierra sin explorar, con bordes suavizados, una orilla más oscura, el agua, un reflejo claro en los anchos y, en el modo terreno, una vega verde a los lados de los grandes ríos. La anchura crece con el caudal y con el zoom; con el zoom alejado solo se ven los grandes ríos.

### `Graphics/Strokes.cs`
`Strokes`: bandas suavizadas a lo largo de un camino de puntos de pantalla, para los ríos y las flechas. `Along(...)` dibuja una banda del ancho que se pida en cada punto, sólida con un borde que se difumina o difuminada desde el centro; `Band(...)` es un tramo de ella.

### `Graphics/PathArrow.cs`
`PathArrow.Draw(lote, puntos, color, tiempo, ancho, opacidad)`: flecha de ruta como en Hearts of Iron: una curva gruesa (Catmull-Rom) por los puntos, con contorno oscuro, punta en el destino y marcas claras que avanzan hacia él.

### `Graphics/Icons.cs`
`Icons.Resource(lote, recurso, centro, tamaño)`: el icono de un recurso. Si `Assets/ResourceIcons` tiene su imagen (`ResourceIcon.Slug`: `petroleo.png`; las carga `Icons.Load` al abrir la partida), esa; si no, dibujado con figuras: una espiga (comida), un tronco (madera), trozos de carbón, lingotes (hierro, cobre, aluminio), monedas (oro, plata), un cristal (silicio), una gota (petróleo) y un neumático (caucho), con el color de cada recurso en el mapa.

### `Graphics/MapIcons.cs`
`MapIcons.City(lote, pie, tejado, casas, capital, escala)`: una ciudad dibujada con figuras: de una a cuatro casas (`Houses(población)`: 2.000, 10.000 y 50.000 habitantes) con pared clara, puerta y tejado a dos aguas del color de su nación, contorno oscuro y sombra; la capital añade una torre almenada con un banderín dorado. Devuelve dónde puede ir su nombre. `NatoSymbol(lote, x, y, ancho, alto, arma)`: el símbolo OTAN del arma de una unidad dentro de su marco. `Settlers(lote, x, y, ancho, alto)`: el símbolo de los colonos, un triángulo. `Battalion(lote, x, y, tipo)`: una ficha OTAN pequeña de un batallón (un casco para los barcos), que acompaña a las tropas en la pestaña Ejército de la provincia, en el panel de la unidad y en las plantillas.

### `Graphics/MenuBackground.cs`
`MenuBackground`: el fondo de los menús: la Tierra real a media resolución, coloreada por altitud (azules por profundidad, llanuras verdes, colinas pardas, cumbres pálidas y hielo) y con relieve, que se desplaza despacio hacia el oeste bajo un velo oscuro. Se construye una vez en segundo plano y lo comparten todas las pantallas de menú; `IsReady` dice si ya se ve.

### `Graphics/Font.cs`
| Función | Qué hace |
| --- | --- |
| `Font(gl)` | Genera una textura con las letras en 4 tamaños: Lato Light, fina y limpia, para la interfaz (con Lato Regular como negrita), y Cinzel (Regular), capitales romanas, para la negrita del tamaño grande (títulos de ventana y encabezados) y el tamaño de título (el nombre del juego y las naciones en el mapa); incluye acentos y ñ (Latin-1). |
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
`Ensure()`: antes de abrir la ventana, prueba a crear una oculta con OpenGL 3.3 (y después otra con antialiasing 4×: `Samples` queda en 4 si se puede, o en 0) (sin él, crear la ventana del juego lo cierra de golpe). Si falla en Linux, activa `LIBGL_ALWAYS_SOFTWARE=1` (el renderizado por software de Mesa) y vuelve a probar; devuelve si se puede jugar.

### `UI/Ui.cs`
Interfaz propia de "modo inmediato": los botones se declaran en cada fotograma y devuelven si se han pulsado.

| Elemento | Qué es |
| --- | --- |
| `Rect` | Rectángulo con `Contains`, `Inset` e `Intersect`. |
| `ScrollState` | Lo desplazada que está un área (`Offset`), lo alto que era su contenido (`Content`, `Overflows`) y por dónde se agarró su barra. |
| `InputState` | Estado del ratón y del teclado en el fotograma. |
| `Theme` | Colores de la interfaz, con los degradados de paneles y botones (`PanelTop`/`PanelBottom`, `ButtonTop`/`ButtonBottom`, `HoverTop`…, `ActiveTop`…), el brillo superior (`Highlight`) y los radios de las esquinas (`PanelRadius`, `ButtonRadius`). `Theme.Mood(moral, normal)` colorea una moral: rojo si hay descontento, verde si está contento. `River`, `Battle`, `Strength` y `Organisation` son el azul de los ríos, el rojo de las batallas y el verde y el ámbar de las barras de hombres y organización; `Theme.Of(tinta)` da el color de un `Ink` de Conquer.Presentation: el de una nación o el del tema para su tono. |
| `Ui.PushClip`, `PopClip`, `BeginScroll`, `EndScroll`, `Wheel`, `ScrollBar` | Recorte y desplazamiento: entre `BeginScroll` y `EndScroll` lo que se dibuja se recorta al área (y el ratón solo cuenta dentro), la rueda la mueve y, si el contenido no cabe, una barra a la derecha (`ScrollBarWidth`) se arrastra o se pulsa para moverla. |
| `Ui.Panel`, `Text`, `TextCentered`, `Button`, `Hover`, `Tooltip` | Piezas de la interfaz. Un panel lleva sombra, cuerpo en degradado, brillo arriba y borde redondeado (`radius`: 0 para la barra superior; `opaque` para las pantallas de la nación y la ayuda). Un botón tiene relieve, se ilumina bajo el ratón con borde dorado, es dorado si está activo, se hunde al pulsarlo y su texto lleva sombra. Los tooltips tienen sombra y una línea dorada arriba. |
| `TextField(área, texto, máximo)` | Caja de texto de una línea: añade lo tecleado en el fotograma (`InputState.Chars`) y borra con Retroceso. Solo admite caracteres que la fuente sabe dibujar. |
| `Ui.ButtonClicked` | Si se ha pulsado algún botón en el fotograma (suena el clic). |
| `Ui.MouseOverUi`, `Block` | Si el ratón está sobre la interfaz (para no hacer clic en el mapa a través de un panel). |
| `BeginFrame`, `EndFrame` | Empezar el fotograma / dibujar el tooltip al final. |

### `UI/DocumentView.cs`
`DocumentView.Draw(ui, documento, x, y, ancho)`: dibuja un `Document` (también `Pair`, `Columns` y `Distribution`) de Conquer.Presentation de arriba abajo con las piezas de `Ui` y ejecuta la acción de los botones pulsados. `Press(ui, botón, área)` dibuja un botón suelto del modelo (velocidades, modos de mapa, filtro de recursos) con su icono; `Size` traduce los tamaños de letra.

### `UI/ChangelogView.cs`
`ChangelogView`: muestra el historial (`Changelog`), o con otro título otro texto con el mismo formato (los `Credits`), en un panel con desplazamiento y barra; `Frame` dice si se pulsó «Cerrar» (quien lo abre guarda si está abierto). `Layout` ajusta cada línea al ancho y le da tamaño y color según sea título, sección, viñeta o párrafo.

### `Screens/HelpView.cs`
`HelpView`: la ayuda durante la partida (F1, el botón «?» de la barra superior o el menú de pausa; abierta según `GameController.HelpOpen`, y `Frame` dice si se pulsó «Cerrar»). Temas a la izquierda y el texto del elegido a la derecha, con desplazamiento y barra. El texto está en `HelpTopics` (Conquer.Presentation); `Layout` ajusta el tema al ancho.

### `Assets/`
Fuentes Lato (Light y Regular) y Cinzel, de Google Fonts, con sus licencias `OFL-Lato.txt` y `OFL-Cinzel.txt`.

---

## 5. `tools/Conquer.EarthData/Program.cs`
Genera `src/Conquer.Game/Assets/earth.gz` a partir del relieve y la batimetría de la NASA (GEBCO) y de
las costas, lagos y glaciares de Natural Earth.

| Función | Qué hace |
| --- | --- |
| Programa principal | Combina las fuentes y escribe el fichero. |
| `Load`, `Downsample` | Leen las imágenes de 21600×10800 y las reducen a 3600×1800. |
| `Rasterize`, `FillPolygon` | Convierten los polígonos GeoJSON en máscaras de píxeles. |

Uso: ver el README.

---

## 6. `tests/Conquer.Tests`

| Fichero | Qué comprueba |
| --- | --- |
| `WorldGenerationTests.cs` | Ambos mapas salen con unas 25.000 provincias y todos los píxeles asignados; el mapa pequeño tiene menos tierra, en un tercio de las provincias y más grandes, y el diminuto, en una sexta parte. |
| `PeakTests.cs` | En la Tierra del generador 2, cada cordillera por encima de 5.000 m (el Tíbet y el Himalaya, los Andes…) es una sola provincia de cumbres y la Antártida sigue siendo hielo; las cumbres no se reclaman pero las tropas las cruzan; la partida guarda la versión del generador; los mapas del generador 1 no tienen cumbres. |
| `ExplorationTests.cs` | Los exploradores que van solos reclaman tierra libre junto a las fronteras por su cuenta; solo las unidades de exploradores pueden hacerlo y la opción se guarda con la partida. |
| `GameplayTests.cs` | Inicio sin territorio y con los recursos correctos; fundar la capital; océanos y polos no reclamables; las unidades terrestres no entran al mar pero sí cruzan hielo; nada cruza el mar; provincias mayores en desiertos, polos y océanos; solo las unidades con exploradores reclaman (también las mixtas); velocidad de 10 km/h; migración diaria; migración forzada con su coste; consumo de comida; la capital gana moral y fertilidad; el hambre los hunde; los migrantes forzados llegan descontentos; las provincias descontentas no pagan impuestos; la fertilidad acelera el crecimiento; las reservas de comida alegran; las fiestas cuestan oro y duran un mes; las estadísticas de la nación suman bien; cada yacimiento es una bolsa finita que empieza llena; muchas provincias tienen yacimientos y algunas varios; las bolsas se agotan y dejan de producir; las ciudades producen ciencia que descubre avances; los niveles se abren con un avance del anterior; elegir otro avance de la rama sustituye al que investigaba; la ciencia se reparte según la prioridad entre las ramas que investigan algo; sin nada elegido la ciencia se guarda y entra en el siguiente avance elegido; los vecinos que conocen un avance lo abaratan; los avances mejoran la economía; los edificios cuestan y tardan, tienen sus requisitos y mejoran su provincia; al principio solo se conocen los recursos antiguos y los avances revelan los demás; los recursos desconocidos no se explotan; la IA se expande e investiga. `WorldFixture` genera un único mundo para todos. |
| `EquipmentTests.cs` | Cada nación empieza con equipo para sus primeros guerreros y exploradores y el taller no pide avance; un taller fabrica lo que se le pide gastando sus recursos (sin ellos no), una fábrica el doble; sin equipo no se entrena y se entrena con el modelo más moderno del que haya; un batallón adopta el modelo nuevo cuando llega su equipo y devuelve el viejo; los refuerzos necesitan equipo; se guardan; la artillería y los tanques son pocas piezas con su tripulación. |
| `LogisticsTests.cs` | Los refuerzos tardan el camino desde la capital y salen del almacén al enviarse; los cuarteles de prioridad alta se sirven antes que los de baja, y las unidades sin cuartel, las últimas y el doble de lentas; un cuartel aislado deja a sus unidades sin envíos; un envío cuya unidad queda aislada vuelve al almacén y a la reserva; combatir gasta munición y sin ella se lucha a la mitad, y la capital la repone con suministros; las batallas la gastan; envíos, munición y prioridades se guardan. |
| `NameTests.cs` | Los nombres reales no se repiten, caben en 24 letras y en la fuente; cada nación tiene su color; las naciones son países reales y nombran su capital y su primera provincia con lo suyo; al acabarse sus regiones toma las de otros países. |
| `FormationTests.cs` | Los batallones se reparten en regimientos, brigadas y divisiones; dos regimientos juntan sus batallones hasta 5; dos regimientos forman una brigada, la quinta parte la convierte en división, el oficial asciende, una división no cabe en otra; una división acoge 5 partes y 10.000 hombres; las partes salen con su número y su nombre; separar batallones forma un regimiento y deshace lo que queda vacío; se guardan. |
| `MilitaryTests.cs` | Instrucción de batallones (hombres, recursos y días); batallones que piden su avance (o sus dos avances); legionarios, catapultas y catafractos piden sus avances de la era Clásica; solo las provincias con taller construyen máquinas de guerra, y con la Industrialización los talleres pasan a ser fábricas; unir (hasta 12), separar y velocidad del batallón más lento; no se entra en tierras ajenas sin guerra; ocupar tierra enemiga sin defensa; un ataque fuerte gana y uno débil se rompe; defensores rodeados destruidos; la paz devuelve lo ocupado; la IA solo acepta la paz pasado un tiempo; desgaste sin suministro; recuperación y refuerzos desde la capital; bonificación de mando en cadena y alcance; las unidades se llaman por su tamaño y conservan su número; un cuerpo manda 5 unidades como mucho; el mantenimiento diario y el ejército sin pagar; cada cuartel tiene un general de su rango que manda a las unidades a su alcance; los oficiales ganan estrellas con victorias y ayudan por sus virtudes; las batallas dan experiencia y victorias al general; las batallas cuentan las bajas y guardan cada hora y su final, y las bajas se guardan; los exploradores son baratos, rápidos y débiles y reclaman tierra; los ingenieros piden Ingeniería, van detrás del frente y rebajan el terreno y el río del defensor; los ingenieros construyen carreteras solo desde una ciudad o cuartel hasta otro, por la ruta de marcha, pagando solo los tramos que faltan, más deprisa cuantos más hay en la ruta; sin ellos la obra se para y al cancelarla se devuelve lo que falta; el suministro llega por las carreteras más allá de su alcance; las partidas antiguas convierten las calzadas en tramos; los reclutas diluyen la experiencia; solo combate lo que cabe en el frente, con la artillería detrás; las armas combinadas; generales y experiencia se guardan, los generales de partidas antiguas pasan a ser oficiales y los cuarteles sin general reciben uno al cargar; cada nación empieza con una plantilla de dos guerreros; las plantillas se editan dentro de sus límites; una plantilla entrena un regimiento entero a la vez. |
| `OfficerTests.cs` | Reclutar oficiales cuesta oro y llena la reserva; ningún defecto anula una virtud; asignar, relevar y retirar pasan por la reserva; los oficiales ascienden al crecer su unidad y no bajan; al unir, la unidad conserva su oficial o toma el de la otra; varios batallones se separan juntos y sin oficial; renombrar y volver al nombre automático; los defectos estorban y las virtudes ayudan (ataque, velocidad, mantenimiento); licenciar devuelve el oficial a la reserva; oficiales, reserva y nombres se guardan. |
| `CityTests.cs` | Solo granja, granero, aserradero, mina, cuartel, taller y fábrica van sin ciudad; los habitantes construyen una ciudad con el nombre que eligen y la provincia conserva el suyo; hacen falta 500 habitantes, sitio y un nombre libre; el granero salva a la mitad de los que morirían de hambre; una ciudad en obras se guarda y se termina tras cargar; las provincias empiezan sin nombre, reciben uno distinto al reclamarlas y lo guardan con la partida. |
| `NavalTests.cs` | El puerto pide costa y Navegación a vela; sin puerto no se construyen barcos ni atracan flotas; el dique seco pide puerto, permite los barcos avanzados y repara el doble de rápido. Las tropas necesitan barco para cruzar el mar (los aviones no); los barcos se construyen en puertos y forman flotas; las flotas navegan hasta donde sabe su nación; los transportes llevan tropas y las desembarcan en otra costa; clic derecho sobre una flota embarca; los barcos de guerra hunden una flota enemiga con su carga; unir flotas conserva la carga; las flotas y la carga se guardan; los barcos van más rápido que a pie; cada línea de barcos tiene un modelo por época y los transportes llevan más con cada una; las flotas en puerto se modernizan al modelo más nuevo pagando la mitad (en el mar no, y el acorazado moderno pide dique seco). |
| `EraTests.cs` | Cada era cuesta más hasta adoptar su institución y se abre al conocer la anterior; el Feudalismo nace en la capital de la primera nación con 8 ciudades el Humanismo en la mayor ciudad con universidad y la Industrialización donde una fábrica trabaja carbón; la Electrificación en la capital de la primera nación con Electricidad; Química revela el caucho y el ferrocarril dobla la velocidad; los avances modernos revelan los últimos recursos y todos los recursos se pueden descubrir; los edificios y batallones de cada era piden su avance; el castillo dobla el daño de los defensores. |
| `ClassicalMechanicsTests.cs` | La muralla hace que los defensores peguen más; las carreteras acortan la marcha (y la ruta más rápida las usa); Construcción acelera las obras; Administración reduce a la mitad la moral perdida por la distancia. |
| `InstitutionTests.cs` | Los avances de la era Clásica cuestan más sin Urbanismo y sus niveles siguen a los de la Antigüedad; Construcción permite el anfiteatro. El Urbanismo nace en la primera ciudad grande; se extiende solo a provincias asentadas; una nación lo adopta cuando lo tiene la mitad de su población y gana su bonus; se puede adoptar antes pagando oro; los avances de una era cuestan más hasta adoptar su institución; las instituciones se guardan con la partida. |
| `DifficultyTests.cs` | En Muy difícil hay menos yacimientos, los mismos de la primera tirada y con la mitad de bolsa; el humano empieza con los recursos de su dificultad y los rivales con los normales; los rivales producen más ciencia en dificultades altas; la dificultad se guarda con la partida. `VeryHardWorldFixture` genera el mismo mundo en Muy difícil. |
| `SaveGameTests.cs` | Cargar una partida y volver a guardarla da exactamente el mismo fichero; la partida cargada sigue jugándose; los mismos ajustes generan el mismo mapa; las partidas anteriores a la 1.13.0 reciben llenos los yacimientos nuevos; el mapa de los tests conserva su huella y cargan las partidas guardadas con la 1.30.1 y con la 1.31.0/1.32.0; las partidas anteriores a los talleres dan uno a cada cuartel cuyo dueño conoce la Maquinaria de asedio; se rechazan las partidas de otro mapa y las dañadas; los barcos de las partidas anteriores a 1.111.0 (trirreme, galeón, acorazado, vapor, destructor) se cargan en su línea y modelo. |
| `InterfaceTests.cs` | Los nombres se ordenan alfabéticamente en español (sin tildes, ñ tras n); el texto de la ayuda solo usa caracteres que la fuente sabe dibujar (Latin-1); `Conquer.Game` y `Conquer.Presentation` no dependen de ningún motor (ni Silk.NET, ni Stb, ni Godot, ni el cliente); el reloj reanuda a la velocidad que tenía; los mensajes salen del más nuevo al más antiguo y caducan; el historial empieza por la última versión, sin marcas de Markdown. Los tests no usan el cliente. |
| `ControllerTests.cs` | La partida en pantalla sin pantalla: empieza con los colonos seleccionados y a la vista; seleccionar una provincia quita la unidad; el tiempo no corre congelado ni en pausa; nombrar una ciudad la funda con ese nombre y la selecciona, y un nombre no válido deja el diálogo abierto; el modo de mapa vuelve al terreno tras dar la vuelta; el panel de los colonos ofrece fundar la ciudad; las pestañas de la capital cambian y su botón de cerrar quita la selección; la migración forzada no pide más gente de la que puede salir, y sus migrantes salen «en camino» en la población del destino; los botones de velocidad de la barra superior ponen el reloj; el editor de unidades separa los batallones marcados en una unidad nueva, renombra y recupera el nombre automático; la ventana de carreteras sin destinos lo dice y se cancela; la ventana de una batalla muestra los dos bandos, el fuego y el gráfico, y «Ir a la provincia» centra el mapa en ella; el mapa muestra la ficha de los colonos seleccionados y, una vez fundada, su ciudad con su nombre; el panel de una unidad muestra su munición (en rojo sin ella, con la alerta «Sin munición») y sus envíos en camino, y el de un cuartel, los botones de prioridad de suministro. |
| `NationScreenTests.cs` | La pantalla de la nación como datos: la tabla de ciudades lista la capital y su «Ver» la muestra en el mapa; pulsar un título de columna ordena por ella y pulsarlo otra vez lo invierte; el diseñador añade un batallón a una plantilla nueva; declarar la guerra cambia el botón a proponer la paz; la ciencia tiene tres ramas e «Investigar» elige el avance; el resumen muestra la capital. |
| `ShipyardTests.cs` | Los barcos se encargan sin pagar nada, una grada los toma y avanza un día de trabajo al día pagando su parte del coste (o lo que puede pagar), y al terminar salen como flota en el puerto; un puerto tiene una grada y el dique seco añade otra; la cola se reordena y se cancela; un barco terminado espera a su tripulación; hace falta puerto y su astillero, y entrenar un barco en un puerto lo encarga; la cola se guarda; la pestaña Marina encarga barcos y muestra la cola. |
| `ConvoyTests.cs` | Los envíos por mar necesitan convoyes y sin ellos se quedan; los atacantes hunden convoyes y las escoltas los protegen; un puerto bloqueado no construye, no envía por mar y pierde su comercio; las patrullas van a por las flotas enemigas vecinas; los astilleros hacen convoyes; convoyes, misiones y rutas se guardan, y las partidas anteriores dan convoyes a quien tiene puerto. |
| `FleetHierarchyTests.cs` | Los barcos llevan las tripulaciones acordadas y la corbeta es una escolta; las agrupaciones crecen de flotilla a escuadra y fuerza (con su rango) y no pasan de 27 barcos; las Flotas mandan las agrupaciones cercanas a su puerto y la Armada las Flotas; los barcos de vapor queman carbón y sin él quedan sin combustible hasta volver a puerto; los aviones sin petróleo se quedan en tierra y sus tripulaciones comen; cuarteles, combustible y la corbeta se guardan. |
| `AirTests.cs` | Cada tipo vuela en escuadrillas de 6 aviones con sus hombres por avión y un modelo por época; las escuadrillas se forman en aeródromos hasta llenarlos y no van en plantillas; se unen en escuadrón, grupo y ala y se separan; las divisiones aéreas mandan las unidades cercanas y el Mando aéreo las divisiones, con su bonus y su límite; cambian de base a menos del doble de su alcance y solo algunas a portaaviones; una unidad sin base vuela a otro aeródromo o se pierde; se reparan en su base y al disolverlas devuelven sus aviones; un portaaviones hundido se lleva sus aviones; unidades, cuarteles y daños se guardan; los regimientos de bombarderos y las alas de partidas anteriores se convierten; los edificios dañados dan menos; la pestaña Fuerza aérea forma cuarteles y escuadrillas. |
| `AirWarTests.cs` | Cada ala solo vuela sus misiones y a su alcance; los cazas dominan el cielo y las tropas luchan mejor bajo él, y un día de combate aéreo cuesta aviones a los dos bandos; el apoyo cercano suma fuego y hace la mitad bajo un cielo enemigo; la antiaérea derriba aviones; los bombarderos quitan moral y frenan los talleres; la aviación naval daña flotas; los paracaidistas saltan desde su aeródromo con transportes; bajo un cielo enemigo se marcha más despacio; las misiones se guardan; en guerra la IA manda sus bombarderos sobre la ciudad enemiga más cercana y sus cazas a dominar el aire. |
| `MenuTests.cs` | Los menús como datos, con un navegador falso: la nueva partida empieza con lo elegido (la Tierra no deja elegir semilla ni tamaño, y no hay menos de un jugador ni más de los que admite cada tamaño de mapa: 12, 16, 24 y 32); el menú principal no hace nada más con el historial abierto; los créditos se abren, bloquean el menú y citan «Lord of the Land»; una partida guardada se carga y se borra tras confirmar; Esc cierra lo de arriba y después abre el menú de pausa, cuyos botones abren la ayuda y salen. |
| `ReleaseTests.cs` | La versión del `.csproj` coincide con la primera entrada del `CHANGELOG.md`. |

---

## 7. Otros ficheros

| Fichero | Qué es |
| --- | --- |
| `CHANGELOG.md` | Historial de versiones para el jugador (también se ve en el juego). |
| `README.md` | Cómo ejecutar, controles, estructura y licencias de los datos. |
| `docs/GUIA.md` | Esta guía. |
| `nuget.config` | Usa solo nuget.org como fuente de paquetes. |
| `.github/workflows/build.yml` | En cada push a `dev` o `main` y en cada pull request, pasa las pruebas en Linux, Windows y macOS y publica el juego autocontenido (un solo ejecutable) para `win-x64`, `linux-x64`, `osx-x64` y `osx-arm64`; se descarga desde la pestaña Actions de GitHub. |
| `.gitignore`, `.gitattributes` | Ficheros que git ignora y ficheros binarios. |
