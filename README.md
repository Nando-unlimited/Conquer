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
dotnet run --project src/Conquer.Client -c Release -- --new earth
```

## Controles

| Acción | Control |
| --- | --- |
| Seleccionar provincia o unidad | Clic izquierdo |
| Mover el mapa | Arrastrar, WASD o flechas |
| Zoom | Rueda del ratón, + / - |
| Mover la unidad seleccionada | Clic derecho |
| Pausa / velocidades | Espacio / 1-5 |
| Cambiar modo de mapa | Tab |
| Ir a tu capital | Inicio |
| Menú | Esc |

## Estructura

| Carpeta | Contenido |
| --- | --- |
| `src/Conquer.Game` | Reglas y simulación, sin dependencias gráficas: mapa, biomas, provincias, recursos, unidades, migración, IA. |
| `src/Conquer.Client` | Ventana, renderizado del mapa por shader, interfaz propia. |
| `tools/Conquer.EarthData` | Genera `src/Conquer.Game/Assets/earth.gz` (mapa de la Tierra real) a partir de datos públicos. |
| `tests/Conquer.Tests` | Pruebas de la generación del mundo y de las reglas. |

## Versiones

Cada cambio sube la versión en `src/Conquer.Client/Conquer.Client.csproj` y añade una entrada al principio de
[CHANGELOG.md](CHANGELOG.md), que también se ve dentro del juego (Menú → Historial de versiones). El título de cada
commit es el número de versión. Un test comprueba que el csproj y el changelog coinciden.

## Datos y licencias

- Relieve y batimetría: NASA Visible Earth, GEBCO (dominio público).
- Costas, lagos y glaciares: Natural Earth 1:50m (dominio público).
- Fuente Noto Sans: SIL Open Font License 1.1 (`src/Conquer.Client/Assets/OFL.txt`).

Para regenerar el mapa de la Tierra, descarga `gebco_08_rev_elev_21600x10800.png`, `gebco_08_rev_bath_21600x10800.png`
(NASA Visible Earth) y `ne_50m_land`, `ne_50m_lakes`, `ne_50m_glaciated_areas` en GeoJSON (Natural Earth) a una
carpeta y ejecuta:

```
dotnet run --project tools/Conquer.EarthData -c Release -- <carpeta> src/Conquer.Game/Assets/earth.gz
```
