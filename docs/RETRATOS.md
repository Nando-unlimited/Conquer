# Retratos pintados de los oficiales

El juego puede usar retratos pintados (creados con una IA de imágenes como Midjourney o DALL·E) en lugar de los
dibujados con formas. Basta con dejar las imágenes en `src/Conquer.Client/Assets/Portraits` y compilar: cada oficial
toma uno de su época y su sexo (o de marino si manda una flota), siempre el mismo. Las épocas sin imágenes siguen con el
retrato dibujado, así que se pueden ir añadiendo poco a poco.

El juego pone encima un **marco del color de la nación** y, en una **banda abajo**, las insignias de rango (tres
galones para un coronel, una estrella por cada grado de general). Por eso los uniformes deben ir en colores neutros y
la franja inferior no debe tener nada importante.

## Formato y nombres

- PNG o JPG, **cuadrados**, de 256 píxeles o más (el juego los reduce a 256). Cabeza y hombros, la cara en el tercio
  superior y centrada.
- Nombre: `época-grupo-número.png`, por ejemplo `renacimiento-hombre-03.png`.

| Época | En el nombre | Tiempos |
| --- | --- | --- |
| Antigua | `antigua` | Edad del Bronce, hasta ~800 a. C. |
| Clásica | `clasica` | Grecia y Roma |
| Medieval | `medieval` | Edad Media |
| Renacimiento | `renacimiento` | siglos XVI a XVIII |
| Industrial | `industrial` | siglo XIX |
| Moderna | `moderna` | siglo XX |

| Grupo | En el nombre | Cuántos por época |
| --- | --- | --- |
| Oficiales hombres | `hombre` | 10 |
| Oficiales mujeres | `mujer` | 5 |
| Marinos (mandan flotas) | `marino` | 4 |

Unas 19 imágenes por época, 114 en total. Con menos también funciona; con más, se repiten menos caras.

## Estilo común

Para que todos parezcan de la misma galería, usa siempre el mismo estilo y cambia solo la descripción del personaje.
En Midjourney conviene fijar el estilo con `--sref` usando el primer retrato que te guste, y variar edad, rasgos, color
de piel, barba y expresión.

Estilo base (en inglés, que estas herramientas entienden mejor):

```
oil painting portrait, head and shoulders, three-quarter view, looking slightly off camera,
plain dark muted background, soft Rembrandt lighting, visible painterly brushstrokes,
muted neutral uniform colors, no text, no frame, centered, square --ar 1:1 --style raw
```

Plantilla: `[estilo base], [personaje], [edad y rasgos]`. Por ejemplo:
`..., a classical antiquity general in a polished bronze muscle cuirass and plain off-white cloak, weathered middle-aged man with a short grey beard and a scar on the cheek`.

## Personajes por época

Para las mujeres, usa la misma descripción con *female commander*. Para variar, añade cosas como *young, middle-aged,
old, stern, confident, tired, battle-scarred*, y orígenes distintos (*Mediterranean, North African, Persian, East Asian,
Northern European...*).

| Época | Oficiales (`hombre` / `mujer`) | Marinos (`marino`) |
| --- | --- | --- |
| Antigua | bronze age warlord, bronze helmet with a horsehair crest, leather and bronze scale armor, undyed wool cloak | bronze age ship captain, bareheaded, linen tunic, salt-weathered skin, rope over the shoulder |
| Clásica | classical antiquity general, polished bronze muscle cuirass, crested helmet or bareheaded, plain off-white cloak | trireme captain of antiquity, bareheaded, white linen tunic and simple bronze brooch |
| Medieval | medieval knight commander, chainmail coif, nasal helm, plain grey surcoat | medieval sea captain, wool hood, leather jerkin, weathered face |
| Renacimiento | 17th–18th century army commander, black tricorne hat, buff leather coat, steel gorget, lace cravat | Age of Sail admiral, black bicorne hat, dark navy coat with gold trim |
| Industrial | 19th century army officer, kepi, dark frock coat with brass buttons and epaulettes | Victorian naval officer, navy blue uniform with gold braid, peaked cap |
| Moderna | 20th century army general, peaked cap, olive drab uniform, medal ribbons | mid-20th century admiral, white peaked cap, navy blue uniform, gold sleeve stripes |

## Licencias

Revisa las condiciones de la herramienta que uses antes de publicar el juego: en Midjourney, el uso comercial depende
del plan de pago; con DALL·E (OpenAI), las imágenes son tuyas según sus condiciones. Apunta en
`src/Conquer.Client/Assets/Portraits/LEEME.txt` con qué herramienta y plan se hicieron.
