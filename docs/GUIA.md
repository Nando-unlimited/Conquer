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
5. Cada `Step()` mueve unidades y migrantes; a medianoche calcula la economía y la migración del día;
   cada 6 horas piensan los rivales (`AiPlayer.Think`).
6. Las órdenes del jugador (mover, fundar, reclamar, reclutar, migrar) son métodos de `GameSession`
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
| `MoodChangePerDay`, `FertilityChangePerDay` | Parte de la distancia a su objetivo que recorren cada día el humor (10 %) y la fertilidad (3 %). |
| `UnrestMood` | Por debajo de 25 la provincia está descontenta y no paga impuestos. |
| `StarvingFertility` | Parte de la fertilidad que queda con hambre (20 %). |
| `MoodProductivity(humor)` | Multiplicador de la producción: ×0,75 con humor 0, ×1 con 50, ×1,25 con 100. |
| `TargetFertility(humor, hambre)` | Fertilidad hacia la que tiende una provincia: 0,5 + humor/100, por 0,2 si hay hambre. |
| `MoodName(humor)` | Descontento, Inquieto, Tranquilo o Contento. |
| `UnitType` | Tipos de unidad: `Settlers` (colonos) y `Warriors` (guerreros). |
| `UnitTypeInfo` | Nombre, símbolo, ciudadanos, si es militar, si funda ciudades y coste. |
| `UnitTypes.Info(type)` | Devuelve la ficha de un tipo de unidad. |

### `Economy/ResourceType.cs`
| Elemento | Qué es |
| --- | --- |
| `ResourceType` | Los 11 recursos: comida, madera, carbón, hierro, cobre, silicio, petróleo, aluminio, caucho, oro, plata. |
| `Resources.All` / `Resources.Deposits` | Todos los recursos / los que salen de yacimientos (todos menos comida y madera). |
| `Resources.Name(type)` | Nombre en español para la interfaz. |
| `Stockpile` | Almacén nacional de recursos de un jugador (`stockpile[recurso]`). |
| `Stockpile.Has(cost)` | ¿Hay suficiente para pagar un coste? |
| `Stockpile.TrySpend(cost)` | Paga el coste si puede; devuelve si lo ha pagado. |
| `ResourceCost` | Lista de (recurso, cantidad); `ToString()` lo escribe como "150 Comida, 50 Madera". |

### `Entities/Entities.cs`
Los objetos de una partida.

| Elemento | Qué es |
| --- | --- |
| `Player` | Jugador: id, nombre, color, si es humano, almacén, provincias que posee, capital, balance del último día (`LastDayNet`) y si pasa hambre. |
| `City` | Ciudad: id, nombre, dueño, provincia y fecha de fundación. |
| `Unit` | Unidad en el mapa: tipo, dueño, provincia, ciudadanos y ruta pendiente (`Path`). `HoursToNext`/`StepHours` miden el tramo actual; `StepProgress` da el avance (0..1) para dibujarla entre provincias. |
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
El corazón del juego: una partida en marcha.

| Función | Qué hace |
| --- | --- |
| `CommandResult` | Resultado de una orden: `Ok` y un mensaje para el jugador. |
| `Create(map, jugadores, semilla)` | Nueva partida: limpia las provincias, crea jugadores con sus recursos iniciales y una unidad de colonos cada uno en sitios fértiles y alejados, y crea las IA. |
| `UnitById`, `CityById`, `CityIn(provincia)` | Búsquedas. |
| `CapacityOf(provincia)` | Habitantes que puede alimentar una provincia, contando la bonificación de ciudad. |
| `MoodFactors(provincia)` | Lista de (causa, puntos) que forman el humor objetivo: base, ciudad, capital o distancia a ella, hacinamiento y hambre. |
| `TargetMood(provincia)` | Suma de esos factores, entre 0 y 100. |
| `AverageMood(jugador)` | Humor medio de su población, ponderado por habitantes. |
| `Step()` | Avanza una hora: mueve unidades, hace llegar migrantes; a medianoche economía y migración; cada 6 h piensan las IA. |
| `MoveUnits()` | Avanza cada unidad por su ruta y la cambia de provincia al terminar cada tramo. |
| `ArriveMigrations()` | Suma los migrantes que llegan a su destino (si el destino se perdió, van a la capital), mezclando su humor. |
| `DailyEconomy(jugador)` | Producción del día (comida, madera, oro, yacimientos) multiplicada por el humor, sin impuestos en provincias descontentas; consumo de comida, hambre, humor y fertilidad, y crecimiento de la población (proporcional a la fertilidad). |
| `UpdateMoodAndFertility(provincia, hambre)` | Acerca el humor a su objetivo y la fertilidad a la que marca el humor; avisa cuando una ciudad del jugador entra o sale del descontento. |
| `DailyMigration(jugador)` | Cada ciudad envía parte de su gente a las provincias propias poco pobladas; primero las vacías y las cercanas. Guarda las fracciones de persona para el día siguiente. |
| `MoveUnit(...)` | Orden de mover una unidad a una provincia (calcula la ruta por tierra). |
| `CanFoundCity(unidad)` / `FoundCity(...)` | Comprueba / funda una ciudad con colonos: reclama la provincia, crea la ciudad (capital si es la primera) y los colonos pasan a ser su población. |
| `CanClaim(unidad)` / `Claim(...)` | Comprueba / reclama con una unidad militar la provincia libre en la que está. |
| `CanRecruit(ciudad, tipo)` / `Recruit(...)` | Comprueba / recluta una unidad en una ciudad, pagando recursos y habitantes. |
| `Disband(...)` | La unidad se asienta: sus ciudadanos pasan a vivir en la provincia (propia) donde está. |
| `CanForceMigration(...)` / `ForceMigration(...)` | Comprueba / envía un número elegido de ciudadanos entre dos provincias propias pagando oro; viajan con el humor de su origen menos 20. |
| `Settle(provincia, personas, humor)` | Añade gente a una provincia mezclando su humor con el de los residentes según cuántos son (migrantes, colonos al fundar, unidades que se asientan). |
| `SetOwner(provincia, jugador)` | Cambia el dueño de una provincia y avisa al cliente (`OwnershipChanged`). |
| `AddUnit`, `RemoveUnit` | Crean y quitan unidades (`AddUnit` también se usa en los tests). |
| `Notify(jugador, texto)` | Añade una notificación. |
| `FormatHours(h)` | "5 h", "2 d 3 h". |
| `PickStartProvinces(n)` | Elige posiciones iniciales fértiles, lo más separadas posible y en masas de tierra de al menos 200 provincias. |
| `LandmassSizes()` | Tamaño (en provincias) de la masa de tierra conectada de cada provincia. |

### `Simulation/Pathfinder.cs`
Rutas por el grafo de provincias. Solo por tierra: mares y lagos están cerrados (serán para unidades
navales); el hielo polar se puede cruzar.

| Función | Qué hace |
| --- | --- |
| `CanEnter(provincia)` | ¿Puede entrar un viajero terrestre? (no, si es agua). |
| `StepHours(a, b)` | Horas para ir de una provincia a su vecina: distancia entre centros / (10 km/h × facilidad del terreno). |
| `FindPath(desde, hasta)` | Ruta más rápida (A*) y su duración, o `null` si no hay camino por tierra. |
| `FromSources(orígenes, maxHoras, destinos)` | Horas desde el origen más cercano a cada provincia (Dijkstra) y cuál es ese origen. Se usa para la migración y para que la IA busque sitio. Puede parar al alcanzar todos los destinos. |
| `Heuristic` | Estimación para A*: distancia en línea recta a 10 km/h. |

### `Simulation/Names.cs`
| Elemento | Qué es |
| --- | --- |
| `PlayerNames.Pick(n, random)` | Nombres de naciones al azar; `Colors` son sus colores. |
| `CityNames.Next(usados, random)` | Genera un nombre de ciudad por sílabas sin repetir. |

### `AI/AiPlayer.cs`
Rival controlado por el ordenador. Determinista (usa su propia semilla).

| Función | Qué hace |
| --- | --- |
| `Think(decisionesDiarias)` | Turno de la IA: licencia guerreros si hay hambre, guía a colonos y guerreros y, una vez al día, celebra fiestas y recluta. |
| `GuideSettlers(unidad)` | Busca el mejor sitio cercano para una ciudad, va allí y la funda. |
| `GuideWarriors(unidad)` | Reclama la provincia si está libre; si no, va a la mejor provincia libre de su frontera. No reclama más rápido de lo que llegan los migrantes. |
| `Recruit()` | Recluta colonos cuando una ciudad ha crecido lo bastante, y guerreros si le sobra comida. |
| `FreeBorderProvinces()` | Provincias libres y reclamables junto a su territorio. |
| `CanSettle(provincia)` | ¿Se puede fundar ciudad aquí? |
| `SiteScore(provincia)` | Lo buena que es una provincia: comida, yacimientos y costa. |
| `TotalPopulation()`, `NearestCityDistanceKm()` | Ayudas. |

### `World/Biome.cs`
| Elemento | Qué es |
| --- | --- |
| `Biome` | Los 17 biomas (océano profundo, océano, mar costero, lago, hielo polar, tundra, taiga, bosque templado, pradera, estepa, desierto, sabana, selva tropical, humedal, colinas, montañas, alta montaña). |
| `BiomeInfo` | Ficha de cada bioma: nombre, si es agua, si es habitable, densidad de provincias (valores bajos, provincias grandes), rendimiento de comida y madera, habitantes por km², facilidad de paso y color. |
| `Biomes.Info(bioma)` | Devuelve la ficha. **Aquí se equilibra cada tipo de terreno.** |

### `World/Province.cs`
`Province`: id, bioma dominante, centro (píxel y lat/lon), área en km², altitud media, vecinas,
yacimientos (`Deposits`), dueño, población, ciudad, humor (`Mood`, 0-100) y fertilidad
(`Fertility`, multiplicador de nacimientos, 1 = normal). `IsWater`, `IsClaimable`, `IsOwned` y
`Capacity` (habitantes que alimenta su tierra) son atajos.

### `World/WorldMap.cs`
| Elemento | Qué es |
| --- | --- |
| `MapKind` | `Random` o `Earth`. |
| `WorldMap` | El mapa: tamaño, altitud, bioma e id de provincia por píxel, y la lista de provincias. |
| `Latitude(y)`, `Longitude(x)`, `WrapX(x)` | Conversión de píxeles a grados y vuelta al mundo en X. |
| `ProvinceAt(x, y)` | Provincia en un píxel. |
| `PixelAreaKm2(fila)` | Área real de un píxel (menor cerca de los polos). |
| `DistanceKm(...)` | Distancia sobre la esfera entre dos puntos o provincias. |

### `World/WorldGenerator.cs`
| Elemento | Qué es |
| --- | --- |
| `WorldSettings` | Tipo de mapa, semilla y número objetivo de provincias (25.000). |
| `Generate(ajustes, progreso)` | Crea el mundo en cuatro pasos: relieve → clima y biomas → provincias → recursos. Informa del paso en curso para la pantalla de carga. |

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
| `Place(provincias, semilla)` | Reparte yacimientos en las provincias habitables, agrupados por regiones. |
| `Chance(recurso, bioma, latitud)` | Probabilidad de cada recurso según el terreno (caucho en selvas tropicales, petróleo en desiertos, etc.). |
| `Richness(recurso)` | Producción típica diaria de un yacimiento. |

### `Assets/earth.gz`
Mapa de la Tierra real, generado por `tools/Conquer.EarthData`. Se incluye dentro de la DLL.

---

## 3. `src/Conquer.Client` — ventana, gráficos e interfaz

### `Program.cs`
Punto de entrada. Pone el formato de números en español y lee los argumentos.

| Elemento | Qué es |
| --- | --- |
| `StartOptions.Parse(args)` | Opciones de línea de comandos para pruebas: `--new random|earth`, `--seed`, `--players`, `--days` (funda la capital y avanza N días), `--zoom`, `--mode terrain|political|population` y `--screenshot fichero.png` (guarda una captura del juego y se cierra). |
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
La pantalla de juego.

| Función | Qué hace |
| --- | --- |
| `GameScreen(...)` | Crea el renderizador y la cámara y centra la vista en tus colonos. |
| `ApplyTestOptions(...)` | Aplica `--days`, `--zoom` y `--mode`. |
| `Frame(dt)` | Fotograma: teclas, tiempo, refresco del mapa, dibujo del mapa, marcadores, paneles e interacción. |
| `AdvanceTime(dt)` | Convierte tiempo real en horas de juego según la velocidad (pausa, 1 h/s … 7 días/s). |
| `SetSpeed`, `HandleKeys` | Velocidad y teclado (Espacio, 1-5, Tab, Inicio, +/-, WASD, Esc). |
| `HandleMapMouse()` | Rueda para el zoom, arrastrar para mover el mapa, clics. |
| `LeftClick()` | Selecciona unidad o provincia, o elige el destino de una migración forzada. |
| `RightClick()` | Da orden de mover la unidad seleccionada. |
| `CenterOnHome()` | Centra la vista en la capital. |
| `Show(resultado)`, `CollectNotifications()` | Mensajes temporales en pantalla. |
| `Center`, `Between`, `OnScreen` | Posición de una provincia, punto intermedio entre dos (cruzando el borde del mapa por el lado corto) y si algo está en pantalla. |
| `DrawCities()` | Marcadores de ciudad y sus nombres. |
| `DrawMigrations()` | Puntos que representan a los migrantes en camino. |
| `DrawUnits()`, `DrawPath()` | Fichas de unidades (estilo OTAN: aspa para infantería, "C" para colonos) y la ruta de la seleccionada. |
| `DrawTopBar()`, `Compact()` | Barra superior: nación, población y humor medio, fecha, velocidades y recursos (con números abreviados: 12,3k, 2,9M). |
| `DrawSidePanel()`, `UnitPanel()`, `ProvincePanel()` | Panel derecho: datos y botones de la unidad o provincia seleccionada (humor y fertilidad, fundar, reclamar, asentarse, reclutar, migración forzada). |
| `MoodColor(humor, normal)`, `MoodTooltip(provincia)` | Color del humor (rojo si hay descontento, verde si está contento) y tooltip con sus causas y su efecto en la producción. |
| `Line()`, `Paragraph()` | Ayudas para escribir filas y párrafos en el panel. |
| `DrawBottomBar()` | Modos de mapa y ayuda de controles. |
| `DrawMessages()`, `HoverTooltip()` | Mensajes y tooltip de la provincia bajo el ratón. |
| `DrawPauseMenu()` | Menú de pausa (Esc). |

### `Graphics/MapRenderer.cs`
Dibuja el mapa entero con un único shader.

| Elemento | Qué es |
| --- | --- |
| `MapMode` | Terreno, político, población, humor, fertilidad. |
| Shader de fragmentos | Para cada píxel de pantalla calcula el punto del mapa, busca la provincia en la textura de ids y la colorea según su dueño o su población. Con zoom alto mezcla las 4 celdas vecinas (`smoothRegions`, `strongest`) para trazar fronteras suaves; con zoom lejano compara con el píxel vecino. Resalta la provincia seleccionada y la que está bajo el ratón. |
| `Prepare(mapa)` | Prepara (fuera del hilo principal) los píxeles de ids y colores del terreno. |
| `ProvinceAt(mapa, punto, zoom)` | Provincia que se ve en un punto, con la misma regla que el shader (para los clics). |
| `SmoothZoom` | Zoom a partir del cual las fronteras se suavizan. |
| `Refresh(partida)` | Recalcula el color de cada provincia y su dueño (texturas pequeñas de 256×128). |
| `PopulationColor(densidad)` | Escala de color del modo población. |
| `ScaleColor(valor)` | Rojo-amarillo-verde de 0 a 1, para los modos humor y fertilidad. |
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

### `UI/Ui.cs`
Interfaz propia de "modo inmediato": los botones se declaran en cada fotograma y devuelven si se han pulsado.

| Elemento | Qué es |
| --- | --- |
| `Rect` | Rectángulo con `Contains` e `Inset`. |
| `InputState` | Estado del ratón y del teclado en el fotograma. |
| `Theme` | Colores de la interfaz. |
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
| `GameplayTests.cs` | Inicio sin territorio y con los recursos correctos; fundar la capital; océanos y polos no reclamables; las unidades terrestres no entran al mar pero sí cruzan hielo; nada cruza el mar; provincias mayores en desiertos, polos y océanos; solo las unidades militares reclaman; velocidad de 10 km/h; migración diaria; migración forzada con su coste; consumo de comida; la capital gana humor y fertilidad; el hambre los hunde; los migrantes forzados llegan descontentos; las provincias descontentas no pagan impuestos; la fertilidad acelera el crecimiento; la IA se expande. `WorldFixture` genera un único mundo para todos. |
| `ReleaseTests.cs` | La versión del `.csproj` coincide con la primera entrada del `CHANGELOG.md`. |

---

## 6. Otros ficheros

| Fichero | Qué es |
| --- | --- |
| `CHANGELOG.md` | Historial de versiones para el jugador (también se ve en el juego). |
| `README.md` | Cómo ejecutar, controles, estructura y licencias de los datos. |
| `docs/GUIA.md` | Esta guía. |
| `nuget.config` | Usa solo nuget.org como fuente de paquetes. |
| `.gitignore`, `.gitattributes` | Ficheros que git ignora y ficheros binarios. |
