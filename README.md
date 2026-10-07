# Conquer

Juego de estrategia en 2D, mezcla de Civilization y Hearts of Iron, escrito en C# (.NET 8) con un motor propio
sobre Silk.NET/OpenGL.

## Ejecutar

```
dotnet run --project src/Conquer.Client -c Release
```

Para saltarse los menús y empezar directamente (útil para probar):

```
dotnet run --project src/Conquer.Client -c Release -- --new random --seed 1234 --players 4
dotnet run --project src/Conquer.Client -c Release -- --new random --size small   # tiny, small, medium o large
dotnet run --project src/Conquer.Client -c Release -- --new earth
```

Versiones listas para jugar en Windows, Linux y macOS (no necesitan .NET instalado): pestaña **Actions** de GitHub →
última ejecución de *Build* → *Artifacts*. Para generarlas a mano:

```
dotnet publish src/Conquer.Client -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

(`linux-x64`, `osx-x64` u `osx-arm64` para los otros sistemas.)

## Partidas guardadas

Desde el menú (Esc) → **Guardar partida**. La pantalla inicial permite continuar la última o cargar cualquier otra.
Se guardan en la carpeta `Partidas`, junto al ejecutable del juego. Si esa carpeta no se puede escribir, van a
`~/.local/share/Conquer/Partidas` (Linux), `%LOCALAPPDATA%\Conquer\Partidas` (Windows) o
`~/Library/Application Support/Conquer/Partidas` (macOS), donde se guardaban antes de la 1.45.1.

## Controles

| Acción | Control |
| --- | --- |
| Seleccionar provincia o unidad | Clic izquierdo |
| Pasar a la siguiente unidad de una pila | Clic otra vez en la pila |
| Ver una batalla en detalle | Clic en sus espadas rojas |
| Mover el mapa | Arrastrar, WASD o flechas |
| Zoom | Rueda del ratón, + / - |
| Mover la unidad seleccionada | Clic derecho |
| Pausa / velocidades | Espacio / 1-5 |
| Cambiar modo de mapa | Tab |
| Ir a tu capital | Inicio |
| Menú (guardar la partida, salir) | Esc |

## Estructura

| Carpeta | Contenido |
| --- | --- |
| `src/Conquer.Game` | Reglas y simulación, sin dependencias gráficas: mapa, biomas, provincias, recursos, unidades, migración, IA. |
| `src/Conquer.Presentation` | Lo que ve y hace el jugador, sin motor gráfico: el estado de la partida en pantalla, el contenido de paneles, ventanas, pantallas y menús como datos, los marcadores del mapa y las órdenes de sus botones. |
| `src/Conquer.Client` | Ventana, renderizado del mapa por shader, interfaz propia (Silk.NET + OpenGL). Solo dibuja; cambiar de motor no toca los otros dos. |
| `tools/Conquer.EarthData` | Genera `src/Conquer.Game/Assets/earth.gz` (mapa de la Tierra real) a partir de datos públicos. |
| `tests/Conquer.Tests` | Pruebas de la generación del mundo y de las reglas. |

Qué hace cada fichero y cada función: [docs/GUIA.md](docs/GUIA.md).

## Versiones

Cada cambio sube la versión en `src/Conquer.Client/Conquer.Client.csproj` y añade una entrada al principio de
[CHANGELOG.md](CHANGELOG.md), que también se ve dentro del juego (Menú → Historial de versiones). El título de cada
commit es el número de versión. Un test comprueba que el csproj y el changelog coinciden.

## Datos y licencias

- Relieve y batimetría: NASA Visible Earth, GEBCO (dominio público).
- Costas, lagos y glaciares: Natural Earth 1:50m (dominio público).
- Fuentes Lato (Lukasz Dziedzic) y Cinzel (Natanael Gama): SIL Open Font License 1.1 (`src/Conquer.Client/Assets/OFL-Lato.txt` y `OFL-Cinzel.txt`).
- Música y efectos de sonido: dominio público, de Kenney.nl, de autores de OpenGameArt.org (RandomMind, Spring Spring, Joth, Wolfgang_, nene, Emma_MA, fvcalderan, William Hector, HaelDB) y de Kevin MacLeod («The Britons», FreePD.com). Detalle en `src/Conquer.Client/Assets/Audio/CREDITS.txt`.
- Maquetas isométricas de ciudades y edificios: renderizadas del Hexagon Kit de Kenney.nl (CC0). Las de unidades, barcos y aviones se modelaron para la otra versión del juego (conquerTEST, `tools/IsoSprites`) al estilo de los kits de Kenney. Detalle en `src/Conquer.Client/Assets/Sprites/LICENSE.txt`.
- «Lord of the Land» Kevin MacLeod (incompetech.com), Licensed under Creative Commons: By Attribution 4.0 License (https://creativecommons.org/licenses/by/4.0/).

Para regenerar el mapa de la Tierra, descarga `gebco_08_rev_elev_21600x10800.png`, `gebco_08_rev_bath_21600x10800.png`
(NASA Visible Earth) y `ne_50m_land`, `ne_50m_lakes`, `ne_50m_glaciated_areas` en GeoJSON (Natural Earth) a una
carpeta y ejecuta:

```
dotnet run --project tools/Conquer.EarthData -c Release -- <carpeta> src/Conquer.Game/Assets/earth.gz
```
