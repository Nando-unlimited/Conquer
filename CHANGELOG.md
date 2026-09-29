# Historial de versiones

Todos los cambios del juego, del más reciente al más antiguo. Cada versión es un commit cuyo título es su número.

## [1.16.0] - 2026-09-29

### Añadido
- **Instituciones, como en Europa Universalis.** La primera es el **Urbanismo**:
  - **Dónde nace**: en la primera ciudad del mundo que llega a 5.000 habitantes, sea tuya o de un rival.
  - **Cómo se extiende**: de provincia en provincia, más deprisa hacia las ciudades. Solo llega a provincias habitadas.
  - **Cuándo se adopta**: tu nación lo adopta sola cuando lo tiene la mitad de tu población, o antes pagando oro (1 por cada 10 habitantes que aún no lo tienen) si ya ha llegado a alguna de tus provincias.
  - **Qué da al adoptarlo**: +10 % de ciencia y +10 % de fertilidad.
  - **Qué pasa si no lo adoptas**: cuando llegue la era Clásica, sus avances te costarán un 50 % más.
- Nuevo modo de mapa **Instituciones** para ver por dónde se ha extendido.
- La pestaña Ciencia y el resumen de la nación muestran cada institución: si la has adoptado, cuánta de tu población la tiene y el botón para adoptarla.
- Los rivales compran las instituciones cuando les sobra el oro.
- Las partidas guardadas con versiones anteriores se pueden cargar; en ellas, el Urbanismo aún no ha nacido.

## [1.15.0] - 2026-09-29

### Cambiado
- **Tú eliges qué investigar en cada rama, como en Civilization.** Cada rama tiene tres niveles: en los dos primeros hay dos avances para escoger y en el tercero, uno. Un nivel se abre en cuanto conoces uno de los avances del anterior.
- Las tres ramas investigan a la vez y la prioridad sigue repartiendo la ciencia entre ellas. Una rama sin nada elegido cede su parte a las demás; si ninguna investiga nada, la ciencia se guarda para el próximo avance que elijas.
- Algunos avances piden otros de su misma rama: Irrigación pide Agricultura, La rueda pide Doma del caballo y Trabajo del hierro pide Trabajo del bronce.
- Costes por nivel: 70, 160 y 300 puntos.
- El botón **Nación** muestra «!» cuando alguna rama no investiga nada y podría hacerlo.
- Las partidas guardadas con versiones anteriores se pueden cargar; al cargarlas, cada rama espera a que elijas qué investigar.

## [1.14.0] - 2026-09-29

### Añadido
- **La ciencia avanza en tres ramas a la vez, como en Europa Universalis: Economía, Sociedad y Militar.** Cada rama sube nivel a nivel y ya no hay que elegir qué investigar. En la pestaña Ciencia das a cada rama una **prioridad** de 0 a 10, y la ciencia se reparte en proporción.
- Si el avance de una rama está completo pero espera a otra (Trabajo del bronce necesita Minería; Moneda, Escritura), o la rama está terminada, su ciencia pasa a las demás.
- **Los vecinos enseñan**: cada nación con la que tienes frontera y que ya conoce un avance te lo abarata un 10 %, hasta un 30 %.
- Dos avances nuevos en Sociedad: **Alfarería**, que permite el granero, y **Código de leyes**, con +10 % de impuestos.

### Cambiado
- Los avances cuestan según su nivel, igual en las tres ramas: 60, 100, 150, 220 y 300 puntos.
- El granero necesita Alfarería.
- Los rivales dan más prioridad a la economía en paz y a lo militar en guerra.
- Las partidas guardadas con versiones anteriores se pueden cargar: conservan sus avances y lo investigado, con las tres ramas a la misma prioridad.

## [1.13.0] - 2026-09-29

### Añadido
- **Niveles de dificultad**: Muy fácil, Fácil, Normal, Difícil y Muy difícil, que se eligen en la pantalla de nueva partida. Cambian:
  - **Cuántos yacimientos hay**: de unas 5 de cada 10 provincias en Muy fácil a menos de 2 de cada 10 en Muy difícil.
  - **Cuánto dura cada yacimiento**: de 1,5 veces lo normal en Muy fácil a la mitad en Muy difícil.
  - **Los recursos con los que empiezas**: del doble a la mitad.
  - **Lo que producen e investigan los rivales**: de un 25 % menos a un 50 % más.

### Cambiado
- **Una provincia puede tener varios yacimientos a la vez**, y es más fácil encontrarlas: en Normal, 3 de cada 10 provincias tienen alguno (antes, menos de 2 de cada 10). Los yacimientos de siempre siguen en el mismo sitio; los nuevos se suman a ellos.
- Las partidas guardadas con versiones anteriores se pueden cargar, en dificultad Normal; los yacimientos nuevos empiezan llenos.

## [1.12.0] - 2026-09-29

### Añadido
- **Construir ciudades en tus provincias** sin necesidad de colonos: en la pestaña Edificios de una provincia con al menos 500 habitantes aparece **Ciudad** (150 de madera, 50 de oro, 60 días). No puede haber otra ciudad al lado, ni hecha ni en obras.
- **Tú eliges el nombre de cada ciudad**, tanto al fundarla con colonos como al construirla. El juego propone uno, puedes escribir otro o pedir **Otro nombre**. No se pueden repetir nombres.
- **Las provincias tienen nombre propio** («Pararira», «Casanela»…), distinto del de la ciudad que se funde en ellas. Se ve en el panel, en el tooltip del mapa, en las tablas y en los avisos.
- **Granero**: en su provincia, el hambre mata a la mitad de gente. Se suma a Medicina: con los dos, muere la cuarta parte.
- Los rivales también construyen ciudades en sus provincias más pobladas.

### Cambiado
- Solo **granjas, graneros, aserraderos y minas** se construyen sin ciudad. Templos, acueductos y herbolarios necesitan ahora una ciudad, como ya la necesitaban bibliotecas y mercados. Los que ya estuvieran construidos en provincias sin ciudad siguen funcionando.
- Las partidas guardadas con versiones anteriores se pueden cargar; las provincias reciben su nombre al cargarlas.

## [1.11.0] - 2026-09-29

### Añadido
- **Guardar y cargar partidas.** En el menú (Esc) está **Guardar partida**; cada partida se guarda con el nombre de tu nación y la fecha de juego («Kartesia - 5 feb 3999 a.C. 00h»).
- **Pantalla inicial** nueva: **Continuar** (la última partida guardada), **Nueva partida**, **Cargar partida**, historial de versiones y salir.
- En **Cargar partida** ves tus partidas de la más reciente a la más antigua, y puedes cargarlas o borrarlas (pide confirmación).
- Las partidas ocupan pocos KB: el mapa no se guarda, se vuelve a generar igual al cargar. Si una versión futura del juego genera el mapa de otra forma, avisa de que esa partida no se puede cargar.

### Arreglado
- Algunos mensajes del ejército («Plantilla no válida», «en instrucción: 15 días»…) salían con un carácter roto en lugar de la letra con tilde.

## [1.10.3] - 2026-09-29

### Arreglado
- En pantallas pequeñas (por ejemplo, portátiles de 1366×768) los menús ya no se salen de la ventana: si es menor de 1280×820, toda la interfaz se encoge en proporción para que se vean todas las opciones.
- La ventana se abre a un tamaño que cabe en la pantalla.
- Los avisos ya no tapan la pantalla de la nación: mientras está abierta, el último aviso sale abajo a la derecha, junto a los modos de mapa.
- Las tablas de ciudades y provincias vuelven a ordenarse alfabéticamente en español (sin importar tildes y con la ñ tras la n); desde la 1.10.1 los nombres con tilde iban al final.

## [1.10.2] - 2026-09-29

### Arreglado
- El juego ya no se cierra de golpe en ordenadores con tarjetas gráficas antiguas (sin OpenGL 3.3). En Linux pasa solo al renderizado por software, más lento pero jugable; en Windows y macOS explica qué falta.

## [1.10.1] - 2026-09-29

### Cambiado
- El juego ya no necesita la librería ICU del sistema, así que arranca también en Linux mínimos. Los números se siguen viendo en español (1.234,5 y 25 %).
- Cada cambio se compila y se prueba automáticamente en Windows, Linux y macOS, y se publican versiones listas para jugar en cada sistema.

## [1.10.0] - 2026-09-28

### Añadido
- **Plantillas de regimiento**, como en Hearts of Iron. En la nueva pestaña **Plantillas** de la pantalla de la nación diseñas regimientos de 1 a 6 batallones: añades los que conoces y quitas los que sobran.
  - Puedes crear, duplicar y borrar plantillas.
  - Cada una muestra sus hombres, días de instrucción, ataque, defensa, organización, velocidad y coste.
- En la pestaña Ejército de tus ciudades se entrena un **regimiento entero** a partir de una plantilla: pagas todo a la vez, los hombres salen de la ciudad y sus batallones se instruyen juntos (tarda lo que el más lento). Los batallones sueltos se siguen pudiendo entrenar.
- Cada nación empieza con una plantilla de dos cohortes de guerreros.
- La IA diseña su propia plantilla con su mejor infantería y su tropa más ofensiva, solo con tropas cuyos materiales tiene, y la recorta al tamaño que su ciudad puede dar.
- **Ríos**, trazados a partir del relieve y la lluvia en los mapas aleatorio y de la Tierra. Se ven como líneas azules, más anchas cuanta más agua llevan; con el zoom alejado solo se ven los grandes.
  - Los **grandes ríos** hacen la tierra más fértil: +25 % de comida y de capacidad.
  - Quien ataque una provincia con un gran río tiene que cruzarlo: el defensor dispara un 25 % más.
  - Los arroyos se dibujan pero no cambian nada. El panel y el tooltip de la provincia dicen si tiene río.

## [1.9.0] - 2026-09-28

### Cambiado
- **Formaciones más detalladas**, preparadas para regimientos con batallones especializados: batallón → regimiento → brigada → división → cuerpo → ejército → grupo de ejércitos.
  - Lo que se entrena en las ciudades es el **batallón**, la tropa especializada: guerreros, arqueros, jinetes, etc.
  - Los batallones forman **regimientos**, la unidad mínima que se mueve y lucha, de hasta 6 batallones (antes, divisiones de hasta 4 brigadas).
- La cadena de mando tiene ahora cinco niveles de cuartel general: **brigada** (hasta 4 regimientos, 150 km), **división** (hasta 4 brigadas, 300 km), **cuerpo** (600 km), **ejército** (1.200 km) y **grupo de ejércitos** (2.500 km). El teatro desaparece.
- **Nombres romanos**: mientras tu ejército no se modernice, las formaciones llevan nombres romanos:
  - El batallón es una cohorte (o un ala si es montado).
  - El regimiento es una legión, la brigada una vexilación, la división un ejército consular, el cuerpo un ejército provincial, el ejército un ejército de campaña y el grupo de ejércitos una prefectura.
  - Las unidades se numeran a la romana: «Legión III», «Vexilación I».
- Los nombres modernos («3.er Regimiento», «1.ª Brigada», «II Cuerpo») ya están listos y los traerá un avance de una época posterior, cuando exista.
- La IA forma cuarteles de brigada, división y cuerpo, y regimientos de hasta 4 batallones.

## [1.8.1] - 2026-09-28

### Añadido
- Nuevo avance **Tiro con arco** (80): hace falta para entrenar arqueros.
- Nueva brigada **Carros de arqueros** (50 madera, 40 oro, 10 cobre, 35 días; ataque 6, defensa 2, montada): requiere La rueda y Tiro con arco.
- Las tarjetas de la pestaña Ciencia muestran también qué brigadas permite cada avance, y caben cinco por fila.

### Cambiado
- Los arqueros ya no están disponibles desde el principio: requieren Tiro con arco.

## [1.8.0] - 2026-09-28

Ejército al estilo de Hearts of Iron III, en la Antigüedad.

### Añadido
- **Brigadas y divisiones**: las ciudades entrenan brigadas (tardan días y cogen sus hombres de la población) y cada una forma una división. Las divisiones de una misma provincia se pueden **unir** (hasta 4 brigadas) y **separar**; marchan al paso de su brigada más lenta.
- Seis brigadas de la Antigüedad, cada una con ataque, defensa, organización y velocidad:
  - Guerreros y Arqueros, desde el principio.
  - Lanceros de bronce (Trabajo del bronce).
  - Jinetes (Doma del caballo).
  - Carros de guerra (La rueda).
  - Infantería de hierro (Trabajo del hierro).
- Los montados son más rápidos pero atacan a la mitad en bosques, pantanos y montañas.
- Nuevos avances: **Trabajo del bronce**, **Doma del caballo** y **La rueda**. Trabajo del hierro requiere ahora Trabajo del bronce.
- **Guerra y paz**: puedes declarar la guerra y proponer la paz en la nueva pestaña **Diplomacia** de la pantalla de la nación. Nadie puede entrar en tierras de otra nación si no está en guerra con ella; los colonos y los cuarteles, nunca.
- **Combate hora a hora**: mover una división a una provincia enemiga con tropas la ataca desde la frontera. Cada bando dispara con su ataque o su defensa según sus hombres, su organización, el mando, el suministro y el terreno (el defensor gana hasta un 50 % en montañas). La división que se queda sin organización se retira; la que no tiene adónde ir es destruida.
- **Ocupación**: al ganar una batalla o entrar en tierra enemiga sin defensa, la provincia pasa a estar ocupada. Una provincia ocupada no produce ni come para su dueño, no recibe migrantes, sus obras se detienen y su humor cae 30 puntos. En el mapa político toma el color del ocupante dentro de las fronteras de su dueño. La paz devuelve lo ocupado y los ejércitos vuelven a casa.
- **Suministro**: llega desde tus ciudades por tierra que controlas (o sin dueño) hasta 15 días de marcha, y una provincia más allá.
  - Con suministro y fuera de combate, las divisiones recuperan organización y reciben refuerzos que salen de la capital.
  - Sin él, luchan peor, se desgastan y acaban dispersándose.
- **Cadena de mando**: cuarteles generales de cuerpo, ejército, grupo de ejércitos y teatro, cada uno con su alcance (300 a 2.500 km) y hasta 5 subordinados del nivel inferior. Una división con su cuartel a su alcance lucha y se recupera un 10 % mejor, y un 5 % más por cada nivel superior enlazado.
- Pestaña **Ejército** en tus ciudades para entrenar brigadas y cuarteles y ver lo que está en instrucción.
- Panel de la división con sus brigadas, barras de hombres y organización, suministro, mando y botones para unir, separar y asignar a un cuartel.
- Pestaña **Ejército** en la pantalla de la nación con el orden de batalla completo.
- En el mapa, fichas con barras de hombres y organización, flechas de ataque y espadas cruzadas sobre cada batalla (con los dos bandos al pasar el ratón).
- **La IA**:
  - Forma un ejército, lo organiza en divisiones y cuarteles y declara la guerra a vecinos mucho más débiles pasados dos años.
  - Ataca las provincias enemigas peor defendidas que alcanza su suministro y acude a defender sus ciudades.
  - Hace la paz cuando la guerra le va mal. Solo acepta tu propuesta de paz si la guerra dura ya dos meses y no la va ganando.

### Cambiado
- Los guerreros son ahora una brigada: se entrenan en la pestaña Ejército de la ciudad (15 días) en lugar de aparecer al momento. Los colonos se envían desde la pestaña General.

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
