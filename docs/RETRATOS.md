# Retratos pintados de los oficiales

El juego puede usar retratos pintados (creados con una IA de imágenes como Recraft, Midjourney o DALL·E) en lugar de
los dibujados con formas. Basta con dejar las imágenes en `src/Conquer.Client/Assets/Portraits` y compilar.

Los retratos **muestran el rango ellos mismos**: el juego los dibuja tal cual, solo con un marco fino del color de la
nación. Cada oficial toma uno de su rango, su época, su sexo y su arma (ejército, marina o aviación), siempre el mismo
mientras no cambie de rango; al ascender pasa a uno del nuevo rango, así que cambia de cara.

No hace falta tenerlo todo: si falta su rango, el oficial toma uno del rango más cercano (el inferior si hay empate); un
marino o un aviador sin retratos de su arma toma uno del ejército; y si su época no tiene ninguno, el retrato
dibujado. Se pueden ir añadiendo poco a poco, por ejemplo empezando solo con
`coronel` y `general`.

## Formato y nombres

- PNG o JPG (no SVG), **cuadrados**, de 256 píxeles o más (el juego los reduce a 256). Cabeza y hombros, la cara
  centrada en la parte de arriba.
- Nombre: `rango-época-sexo-grupo-número.png`, en minúsculas y sin tildes. Por ejemplo
  `coronel-renacimiento-hombre-ejercito-03.png`, `division-moderna-mujer-marina-01.png` o
  `brigadier-industrial-hombre-aviacion-02.png`. El número puede ser cualquiera. Las cinco partes son obligatorias.

| Época | En el nombre | Tiempos |
| --- | --- | --- |
| Antigua | `antigua` | Edad del Bronce, hasta ~800 a. C. |
| Clásica | `clasica` | Grecia y Roma |
| Medieval | `medieval` | Edad Media |
| Renacimiento | `renacimiento` | siglos XVI a XVIII |
| Industrial | `industrial` | siglo XIX |
| Moderna | `moderna` | siglo XX |

| Sexo | En el nombre |
| --- | --- |
| Hombre | `hombre` |
| Mujer | `mujer` |

| Grupo (arma) | En el nombre |
| --- | --- |
| Ejército (regimientos y cuarteles generales) | `ejercito` |
| Marina (mandan flotas) | `marina` |
| Aviación (mandan aviones) | `aviacion` |

| Rango | En el nombre | En la armada | En la aviación | Qué manda |
| --- | --- | --- | --- | --- |
| Coronel | `coronel` | Capitán de navío | Coronel de aviación | Un regimiento pequeño |
| Brigadier | `brigadier` | Comodoro | General de brigada aérea | Un regimiento (o flota) de 4 a 6 batallones (o barcos) |
| General de división | `division` | Contraalmirante | General de división aérea | Un regimiento (o flota) de 7 o más |
| Teniente general | `teniente` | Vicealmirante | Teniente general del aire | Un cuerpo de ejército |
| General | `general` | Almirante | General del aire | Un ejército |
| Mariscal | `mariscal` | Gran almirante | Mariscal del aire | Un grupo de ejércitos |

Casi todos los oficiales son coroneles (los reclutados lo son, y la mayoría de regimientos son pequeños); los cuarteles
generales tienen tenientes generales, generales y mariscales, y son siempre del ejército: los marinos y los aviadores
solo llegan hasta `division`. Por eso conviene tener más coroneles:

| Rango | Por época: `hombre-ejercito` | `mujer-ejercito` | `marina` y `aviacion` (cada sexo) |
| --- | --- | --- | --- |
| `coronel` | 6 | 3 | 2 |
| `brigadier`, `division` | 2 cada uno | 1 cada uno | 1 cada uno |
| `teniente`, `general`, `mariscal` | 2 cada uno | 1 cada uno | — |

Unas 30 imágenes por época. Con menos también funciona.

## Estilo común

Para que todos parezcan de la misma galería, usa siempre el mismo estilo (en Recraft, guárdalo como estilo propio) y
cambia solo la descripción del personaje y su rango. Varía edad, rasgos, color de piel, barba y expresión.

Estilo base (en inglés, que estas herramientas entienden mejor):

```
oil painting portrait, head and shoulders, three-quarter view, looking slightly off camera,
plain dark muted background, soft Rembrandt lighting, visible painterly brushstrokes,
muted neutral uniform colors, no text, no frame, centered, square
```

Plantilla: `[estilo base], [personaje de la época], [rango], [edad y rasgos]`. Los uniformes en colores neutros, porque
el marco ya pone el color de la nación.

## El rango en la imagen

Los rangos altos, además de llevar más insignias, deben parecer **mayores y más condecorados**: un coronel puede ser
joven; un mariscal, un veterano canoso cargado de honores.

| Rango | Épocas antigua, clásica y medieval | Renacimiento | Industrial y moderna |
| --- | --- | --- | --- |
| `coronel` | simple armor and plain cloak, no ornaments | plain coat, simple sash | three bars or a colonel's insignia on the shoulder boards, few ribbons |
| `brigadier` | a small gold ornament on the armor | coat with modest gold lace | one gold star on the shoulder boards |
| `division` | gilded helmet trim, finer cloak | richer gold lace, a sash | two gold stars on the shoulder boards |
| `teniente` | ornate armor with gold details, rich cloak | heavy gold embroidery on the coat | three gold stars, several medal ribbons |
| `general` | gilded armor, a laurel or a gold torque | general's coat heavy with gold embroidery, an order's star on the chest | four gold stars, rows of medal ribbons |
| `mariscal` | the most splendid gilded armor, purple cloak, a commander's baton | marshal's baton, grand sash, orders and stars | five stars or a marshal's insignia, a baton, many decorations |

Para los marinos, lo mismo con sus equivalentes: *ship captain, commodore, rear admiral, vice admiral, admiral, grand
admiral*, con los galones en las mangas en las épocas industrial y moderna. Para los aviadores (solo existen desde la
Aviación, en las épocas industrial y moderna): *air force colonel, air commodore, air vice-marshal*, uniforme azul
grisáceo, gorra de plato con el emblema de alas y las alas de piloto en el pecho.

## Personajes por época

Para las mujeres, la misma descripción con *female commander*.

| Época | Ejército (`ejercito`) | Marina (`marina`) |
| --- | --- | --- |
| Antigua | bronze age warlord, bronze helmet with a horsehair crest, leather and bronze scale armor, undyed wool cloak | bronze age ship captain, bareheaded, linen tunic, salt-weathered skin |
| Clásica | classical antiquity general, polished bronze muscle cuirass, crested helmet or bareheaded, plain off-white cloak | trireme captain of antiquity, bareheaded, white linen tunic and simple bronze brooch |
| Medieval | medieval knight commander, chainmail coif, nasal helm, plain grey surcoat | medieval sea captain, wool hood, leather jerkin, weathered face |
| Renacimiento | 17th–18th century army commander, black tricorne hat, buff leather coat, steel gorget, lace cravat | Age of Sail naval officer, black bicorne hat, dark navy coat with gold trim |
| Industrial | 19th century army officer, kepi, dark frock coat with brass buttons and epaulettes | Victorian naval officer, navy blue uniform with gold braid, peaked cap |
| Moderna | 20th century army officer, peaked cap, olive drab uniform | mid-20th century naval officer, white peaked cap, navy blue uniform |

## Licencias

Revisa las condiciones de la herramienta que uses antes de publicar el juego (en Recraft y Midjourney, el uso comercial
depende del plan). Apunta en `src/Conquer.Client/Assets/Portraits/LEEME.txt` con qué herramienta y plan se hicieron.
