# Historial de versiones

Todos los cambios del juego, del más reciente al más antiguo. Cada versión es un commit cuyo título es su número.

## [1.7.1] - 2026-09-28

### Cambiado
- La lista de edificios de la provincia solo muestra los que ya puedes conocer: los que dependen de un avance no aparecen hasta descubrirlo. Los que solo necesitan una ciudad o un yacimiento siguen apareciendo con lo que les falta.
- Las tarjetas de la pestaña Ciencia muestran qué edificios permite cada avance.

## [1.7.0] - 2026-09-28

### Cambiado
- **Recursos ocultos**: al principio solo se conocen la comida, la madera, el cobre, el oro y la plata. Los demás recursos no aparecen en la barra superior, el panel de la provincia, el mapa de recursos, los tooltips ni la pantalla de la nación, y sus yacimientos no se explotan, hasta que un avance los descubre.
- **Minería** descubre el carbón.
- El petróleo, el caucho, el aluminio y el silicio seguirán ocultos hasta que existan los avances de épocas posteriores que los descubran.
- La mina solo se puede construir sobre un yacimiento que conozcas.
- Los rivales del ordenador tampoco ven ni explotan lo que no conocen.

### Añadido
- Nuevo avance **Trabajo del hierro** (200, requiere Minería): descubre el hierro.

## [1.6.0] - 2026-09-28

### Añadido
- **Edificios** en las provincias: se pagan con madera y oro y tardan días en construirse. Cada provincia puede tener uno de cada tipo y levantar uno a la vez, y hacen falta al menos 10 habitantes para construir. Avisa al terminar cada obra.
- Ocho edificios, que mejoran su propia provincia:
  - Granja (40 madera, 10 oro, 20 días): +25 % de comida.
  - Aserradero (30 madera, 10 oro, 15 días): +50 % de madera.
  - Mina (60 madera, 20 oro, 30 días; requiere Minería y un yacimiento): +50 % de producción de los yacimientos.
  - Templo (50 madera, 30 oro, 30 días; requiere Mitología): +10 de humor.
  - Biblioteca (60 madera, 40 oro, 40 días; requiere Escritura y ciudad): +50 % de ciencia de la ciudad.
  - Mercado (80 madera, 50 oro, 45 días; requiere Moneda y ciudad): +50 % de oro de los impuestos.
  - Acueducto (100 madera, 40 oro, 60 días; requiere Irrigación): la tierra alimenta un 25 % más de gente.
  - Herbolario (40 madera, 30 oro, 30 días; requiere Medicina): +20 % de fertilidad.
- El panel de la provincia tiene dos pestañas: **General** y **Edificios** (obra en curso, edificios construidos y botones para construir; los que aún no se pueden levantar dicen qué les falta).
- Los rivales del ordenador construyen en sus ciudades y provincias más pobladas, ahorrando para el edificio que más les conviene.

## [1.5.0] - 2026-09-28

### Añadido
- **Ciencia**: las ciudades producen puntos de ciencia cada día (0,5 por ciudad más 1 por cada mil habitantes), multiplicados por su humor.
- **Investigación**: elige un avance en la nueva pestaña **Ciencia** de la pantalla de la nación; la ciencia se acumula en él hasta descubrirlo. Si cambias de avance no pierdes lo investigado, y la ciencia que se gana sin nada en investigación se guarda para el siguiente.
- Ocho avances de la Antigüedad, algunos con requisitos:
  - Agricultura (60): +20 % de comida.
  - Carpintería (60): +50 % de madera.
  - Minería (100): +50 % de producción de los yacimientos (se agotan antes).
  - Escritura (80): +30 % de ciencia.
  - Mitología (100): +5 de humor en todas tus provincias.
  - Irrigación (250, requiere Agricultura): la tierra alimenta un 25 % más de gente.
  - Medicina (250, requiere Escritura): +10 % de fertilidad y el hambre mata la mitad.
  - Moneda (300, requiere Escritura y Minería): +30 % de oro de los impuestos.
- Aviso al descubrir un avance, ciencia por día e investigación actual en el resumen de la nación, y el botón «Nación !» cuando no estás investigando nada.
- Los rivales del ordenador también investigan.

## [1.4.0] - 2026-09-28

### Añadido
- **Bolsas de recurso**: cada yacimiento guarda una cantidad limitada, de 10 a 50 años de producción a pleno rendimiento. Al explotarlo se vacía y, cuando se agota, deja de producir y te avisa.
- El panel de la provincia muestra lo que queda en cada yacimiento; al pasar el ratón, el total de la bolsa y cuántos años dura explotada al máximo.
- En la pantalla de la nación, la tabla de recursos muestra cuánto queda en las bolsas de tus provincias.
- Modo de mapa **Recursos**: cada provincia con el color de su yacimiento principal, con una leyenda que sirve de filtro para ver un solo recurso (más intenso cuanto más queda en la bolsa). El tooltip del mapa lista los yacimientos de la provincia.
- El tamaño de las bolsas se podrá ajustar con la dificultad, cuando exista.

### Cambiado
- Los rivales del ordenador ya no valoran los yacimientos agotados al buscar dónde fundar.

## [1.3.0] - 2026-09-28

### Añadido
- **Pantalla de la nación** para gestionar el país: se abre con el botón «Nación» de la barra superior o con la tecla N. El tiempo sigue corriendo mientras está abierta (Espacio para pausar).
- Pestaña **Resumen**: población total, en sus provincias, en unidades y migrando; fertilidad media; humor medio y cuántos habitantes están contentos, tranquilos, inquietos o descontentos; capital, provincias, ciudades y unidades; comida, su balance diario y los días de reserva; y el resto de recursos con su cambio diario.
- Pestaña **Ciudades**: todas tus ciudades con su población, humor y fertilidad, y botones para celebrar fiestas, reclutar colonos o guerreros y verlas en el mapa.
- Pestaña **Provincias**: todas tus provincias con población, humor, fertilidad, terreno y migrantes en camino, y un botón para verlas en el mapa.
- Las tablas se ordenan pulsando las cabeceras (nombre, población, humor o fertilidad) y se desplazan con la rueda del ratón.

## [1.2.0] - 2026-09-28

### Añadido
- **Reservas de comida**: la población está más contenta cuanto más dure la comida almacenada, hasta +10 de humor con reservas para 30 días.
- **Fiestas**: una ciudad puede celebrar fiestas pagando oro (2 por cada 100 habitantes, mínimo 10). Suben su humor +20 durante 30 días; no se pueden repetir hasta que terminen. Botón en el panel de la ciudad.
- Los rivales del ordenador celebran fiestas en sus ciudades inquietas (humor por debajo de 45) cuando les sobra oro.

## [1.1.0] - 2026-09-28

### Añadido
- **Humor** de la población (0 a 100) en cada provincia. Cada día se acerca poco a poco al que piden sus condiciones: base 50, +10 si hay ciudad, +15 en la capital, −1 por cada 50 km de distancia a la capital (hasta −20), hasta −20 por hacinamiento y −40 si la nación pasa hambre.
- La gente contenta produce más: el humor multiplica la producción de comida, madera, oro y yacimientos entre ×0,75 (humor 0) y ×1,25 (humor 100).
- Por debajo de 25 la provincia está descontenta y no paga impuestos. Avisa cuando una de tus ciudades se descontenta o vuelve a la calma.
- Los migrantes llevan su humor consigo y lo mezclan con el de los vecinos al llegar. Los de una migración forzada llegan 20 puntos más descontentos.
- **Fertilidad** en cada provincia: multiplica los nacimientos (100 % es lo normal). Sigue al humor, del 50 % al 150 %, y cae al 20 % con hambre; cambia más despacio que el humor.
- Humor y fertilidad en el panel de la provincia (con el desglose de causas al pasar el ratón), en el tooltip del mapa y el humor medio en la barra superior.
- Modos de mapa **Humor** y **Fertilidad**.

## [1.0.2] - 2026-09-28

### Cambiado
- Solo las unidades navales pueden ir por el mar. Colonos y guerreros viajan únicamente por tierra (el hielo polar se puede cruzar, pero no los mares ni los lagos).
- Los migrantes tampoco cruzan el mar: las provincias al otro lado no reciben migración automática y no se les puede enviar migración forzada.
- Todos los jugadores empiezan en una masa de tierra lo bastante grande para expandirse.

## [1.0.1] - 2026-09-28

### Cambiado
- Las fronteras de provincias y países se ven suaves y sin escalones al acercar el zoom, en lugar de seguir los píxeles del mapa.
- Al hacer clic cerca de una frontera se selecciona la provincia que se ve en pantalla.

## [1.0.0] - 2026-09-28

Primera versión jugable.

### Mapa
- Dos tipos de mapa: **aleatorio** (continentes generados a partir de una semilla) o **Tierra real** (relieve de la NASA/GEBCO y costas, lagos y glaciares de Natural Earth).
- Biomas según altitud, distancia al ecuador y humedad: océano profundo, océano, mar costero, lagos, hielo polar, tundra, taiga, bosque templado, pradera, estepa, desierto, sabana, selva tropical, humedales, colinas, montañas y alta montaña.
- Unas 25.000 provincias, como en Hearts of Iron. En desiertos, polos y océanos las provincias son mucho más grandes.
- Los océanos y el hielo polar no se pueden reclamar, pero las unidades sí pueden cruzarlos.
- Modos de mapa: terreno, político y población.

### Recursos
- Comida, madera, carbón, hierro, cobre, silicio, petróleo, aluminio, caucho, oro y plata.
- La comida y la madera salen de la tierra según el bioma; el resto, de yacimientos repartidos por las provincias, que rinden al máximo con 1.000 habitantes.
- Cada ciudadano come 0,1 de comida al día. Sin comida, la población muere de hambre.

### Inicio de partida
- Nadie posee territorio. Cada jugador empieza con una unidad de colonos (300 ciudadanos), 600 de comida, 50 de oro y 100 de madera.
- Los colonos reclaman la provincia en la que están, si no tiene dueño, y fundan la primera ciudad, que es la capital.
- Las unidades militares (guerreros) reclaman provincias libres.
- Las ciudades reclutan colonos y guerreros con sus propios habitantes.

### Población y migración
- La población bien alimentada crece cada día hasta llenar la capacidad de su tierra; las ciudades crecen el doble de rápido y alimentan a más gente.
- Cada día, las ciudades envían parte de su población a las provincias propias poco pobladas, empezando por las que aún no tienen a nadie y las más cercanas. Los migrantes viajan a 10 km/h (más despacio por montañas, selvas o hielo) y llegan tras el tiempo real de viaje.
- Migración forzada: elige cuántos ciudadanos mover de una provincia tuya a otra; cuesta 1 de oro por cada 10.

### Tiempo y rivales
- Tiempo continuo desde el 1 de enero de 4000 a.C., en pasos de una hora, con pausa y cinco velocidades.
- Rivales controlados por el ordenador que fundan ciudades, reclutan y se expanden.

### Interfaz
- Menú principal con opciones de nueva partida, menú de pausa e historial de versiones dentro del juego.
