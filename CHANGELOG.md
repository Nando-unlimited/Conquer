# Historial de versiones

Todos los cambios del juego, del más reciente al más antiguo. Cada versión es un commit cuyo título es su número.

## [1.122.0] - 2026-10-07

Iconos nuevos, y los edificios de la provincia como iconos.

### Cambiado
- **Iconos nuevos** de los edificios, las ciudades y los recursos, y un dibujo nuevo para los colonos. Piedra, azufre y salitre estrenan icono.
- **Opciones › Mapa**: «Etiquetas» deja el mapa como hasta ahora (fichas OTAN y ciudades como puntos); «Figuras 3D» muestra las maquetas de las unidades, los colonos como un carro y cada ciudad con su icono según crece. Los edificios no se dibujan en el mapa en ningún caso.
- **Pestaña Edificios de la provincia**: los construidos se ven como iconos en cuadrícula; al pasar el ratón, su nombre, qué hace, su daño y las tropas que instruye más deprisa. Los dañados van con un borde rojo y una barra de su daño.

## [1.121.0] - 2026-10-07

Emplazar tropas, plantillas por filas, una ventana de nación más limpia y un mejor editor de texto.

### Añadido
- **Emplazar**: en el panel de una unidad de combate de tierra. Se atrinchera donde está durante 5 días y defiende hasta un 25 % mejor; mientras, recupera la organización un 50 % más deprisa y sus envíos de la capital tardan la mitad. Moverse o atacar lo levanta y pierde lo atrincherado. En el mapa se ven sacos terreros bajo su ficha.
- **Contacto entre naciones**: Diplomacia, Comercio y Estadísticas solo muestran las naciones que conoces, porque has visto sus tierras o sus tropas (o ellas las tuyas), o por guerra, alianza, vasallaje o comercio. Una vez conocidas, no se olvidan.

### Cambiado
- **Plantillas como en HOI4**: los huecos se reparten en tres columnas, Frente (infantería, caballería, blindados), A distancia (arqueros y tiradores, artillería, antiaérea) y Apoyo (exploradores, ingenieros, médicos), cada una con sus batallones como tarjetas y los que puede añadir. El regimiento sigue llevando 5 batallones en total, de cualquier fila.
- **Editor de texto**: al abrirse, el nombre queda seleccionado y basta escribir para cambiarlo. El cursor se mueve con las flechas, Inicio y Fin (por palabras con Ctrl), se selecciona con Mayús o el ratón (doble clic, todo), Supr borra hacia delante, Ctrl+Retroceso borra una palabra, y funcionan Ctrl+A, Ctrl+C, Ctrl+X y Ctrl+V. Las teclas mantenidas se repiten.
- **Migración más rápida y en grupos de 50**: las ciudades envían cada día un 0,1 % de su gente (antes un 0,01 %), que sale en grupos de 50.
- El **Resumen** de la nación ya no repite los recursos (están en Almacén, que gana la columna «En bolsas»), y la pestaña **Provincias** muestra solo el nombre de la provincia.
- El mapa ya no dibuja los iconos de los edificios bajo las ciudades.

### Corregido
- Las olas de la costa iban hacia el mar: ahora llegan a la orilla.

## [1.120.0] - 2026-10-07

Las pestañas de Marina y Fuerza aérea aparecen cuando se pueden usar, y dos arreglos de dibujo.

### Cambiado
- **Marina y Fuerza aérea** ya no se ven en la pantalla de la nación hasta que puedes construir barcos (Navegación a vela) o aviones (Aviación). Al descubrir el avance, un aviso te dice que puedes construir puertos y barcos (o aeródromos y aviones) y que ha aparecido su pestaña.

### Corregido
- Las tarjetas de Ciencia crecen cuando su descripción ocupa dos líneas o más, y ya no la tapan los requisitos (Armerías, por ejemplo).
- Las tablas de la nación caben en la ventana: si sus columnas no caben, se estrechan todas, y el texto que no cabe en su celda se corta con «...» y se ve entero al pasar el ratón (los costes de la pestaña Unidades se salían por la derecha).

## [1.119.0] - 2026-10-07

Un mapa más limpio, letras más finas y plantillas con nombre.

### Cambiado
- **Ciudades y pueblos como puntos** del color de su nación, más grandes cuanto más gente tienen (de un pueblo a una gran ciudad); la capital lleva un aro dorado. Los nombres llevan un contorno oscuro para leerse sobre cualquier terreno.
- **Etiquetas por defecto**: el mapa muestra las fichas OTAN y los iconos de los edificios; las maquetas siguen en Opciones («Figuras 3D»). Si ya habías elegido las maquetas, se mantienen.
- **Letras mucho más finas**: la interfaz usa Lato Light (y Lato Regular para la negrita) en lugar de Lilita One, y los títulos, Cinzel normal en lugar de negrita.
- **Como mucho 4 yacimientos por provincia** en los mapas nuevos: si le tocan más, se queda con los más raros en su terreno. Las partidas guardadas conservan su mapa.

### Añadido
- **Nombres de las provincias** al acercarse al mapa.
- **Cambiar el nombre de las plantillas**: «Cambiar nombre», junto al nombre de la plantilla en la pestaña Plantillas (Intro acepta, Esc cancela; vacío, vuelve a su número). Se guarda con la partida.

## [1.118.0] - 2026-10-07

La marina, como se acordó: su jerarquía, la corbeta, las tripulaciones y el combustible.

### Cambiado
- **Jerarquía de la marina**: los barcos se agrupan en una sola ficha que crece con ellos: flotilla (hasta 3 buques), escuadra (hasta 3 flotillas, 9 buques) y fuerza (hasta 3 escuadras, 27 buques). Se unen y separan en «Editar unidad», y cada tamaño tiene su numeración («1.ª Flotilla», «2.ª Escuadra») y el rango de su oficial.
- **Tripulaciones** como en los barcos reales: trirreme 200, carabela 25, galeón 250, fragata 250, navío de línea 700, acorazado 1.000, crucero 700, destructor 300, submarino 60 y portaaviones 2.000. Los barcos ya construidos completan su tripulación en puerto.
- La pestaña Marina lista las agrupaciones con su tamaño, su Flota y su combustible.

### Añadido
- **Corbeta**: una escolta de los Descubrimientos (Cartografía y Pólvora), entre la carabela y la fragata, con 120 hombres.
- **Flota y Armada**: cuarteles generales con un almirante, en un puerto. La Flota manda hasta 5 agrupaciones a menos de 2.000 km de su puerto (+10 % de fuego y la habilidad de su almirante); la Armada, una por nación, manda las flotas (+5 % más). Se forman en la pestaña Marina, y cada agrupación cambia de flota desde su panel.
- **Combustible**: los barcos de vela no gastan; los de vapor queman carbón y los modernos petróleo cada día en el mar (en puerto, nada). Sin él, la agrupación navega 4 veces más despacio y lucha a la mitad hasta repostar. Los aviones queman petróleo los días que vuelan su misión y, sin él, se quedan en tierra.
- Las tripulaciones de los aviones comen como el resto de la nación.
- La IA une sus barcos de guerra en agrupaciones mayores y forma flotas y su Armada.

### Corregido
- Las partidas guardadas suben un puesto a fragatas, cruceros y destructores para hacer sitio a la corbeta.

## [1.117.0] - 2026-10-07

La aviación, como se acordó: escuadrillas y su jerarquía.

### Cambiado
- **La unidad básica es la escuadrilla de 6 aviones**, no el ala de 10. Cada tipo lleva sus hombres por avión: cazas 1, bombarderos en picado 2, bombarderos tácticos 4, estratégicos 10, aviación naval 2 y transportes 4.
- **Jerarquía**: las escuadrillas de un tipo en una misma base se unen («Unir» en el panel de la base) en escuadrón (hasta 3 escuadrillas), grupo (hasta 6 escuadrones) y ala (hasta 3 grupos), y se separan («Separar»). Cada tamaño tiene su numeración: «1.ª Escuadrilla de cazas», «2.º Grupo de bombarderos estratégicos».
- Un aeródromo alberga 18 escuadrillas (un grupo) y cada portaaviones, 6.
- **Los bombardeos dañan los edificios**: un edificio dañado da menos de lo que da según su daño (un taller dañado fabrica menos) y se repara un 2 % al día si no lo vuelven a bombardear; dañado del todo, se derrumba. La pestaña Edificios muestra el daño.
- La pestaña Fuerza aérea lista las unidades con su tamaño y su división.

### Añadido
- **Bombarderos tácticos**: bombarderos ligeros (Aviación), medios (Radar) y tácticos a reacción, que hacen apoyo cercano y bombardeo.
- **Cuarteles generales aéreos**: la División aérea manda hasta 6 unidades con base a menos de 1.500 km (+10 % y la habilidad de su general de aviación); el Mando aéreo, uno por nación, manda las divisiones (+5 % más). Se forman en un aeródromo desde la pestaña Fuerza aérea, y cada unidad se asigna a su división desde el panel de su base.
- La IA une sus escuadrillas en unidades mayores, forma divisiones aéreas y su Mando aéreo, y usa los bombarderos tácticos.

### Corregido
- La flecha del aviso de modernización de los barcos no se veía en la fuente del juego.
- Las partidas guardadas convierten cada ala de 10 aviones en un escuadrón de 2 escuadrillas.

## [1.116.0] - 2026-10-07

La aviación (3 de 3), y con ella termina la nueva estructura militar.

### Añadido
- **Pestaña Fuerza aérea** en la pantalla de la nación: todas tus alas con su base, aviones, organización y misión (y si vuelan o se quedan en tierra), tus aeródromos con las alas que tienen y las que se forman, y un botón «Formar» por cada tipo de avión en el aeródromo con sitio más cercano a la capital.
- **La IA usa la aviación**: construye aeródromos, fabrica aviones y forma alas (la mitad cazas, una cuarta parte apoyo cercano y el resto bombarderos). En guerra pone cazas y apoyo cercano sobre sus batallas, bombarderos sobre las ciudades enemigas y aviación naval sobre las flotas enemigas.

### Cambiado
- Los regimientos de bombarderos de las partidas guardadas pasan a ser alas en el aeródromo más cercano; si la nación no tiene ninguno, su capital recibe uno.

## [1.115.0] - 2026-10-06

La aviación (2 de 3): misiones y combate aéreo.

### Añadido
- **Misiones de las alas**: en el panel del aeródromo (o del portaaviones) cada ala recibe su misión y eliges en el mapa la provincia sobre la que vuela, a su alcance. Cubre las provincias a menos de 300 km. Con poca organización se queda en tierra.
  - **Superioridad aérea** (cazas): quien tiene el 60 % de los cazas sobre una provincia domina su cielo. Sus tropas luchan hasta un 15 % mejor y las enemigas peor; bajo un cielo enemigo las tropas marchan 1,5 veces más despacio, sus envíos tardan más y una cuarta parte no llega. El panel de la provincia dice quién domina.
  - **Apoyo cercano**: suma su fuego a tus batallas de la zona.
  - **Bombardeo estratégico**: quita moral a la provincia enemiga, frena sus talleres al día siguiente y puede derribar edificios.
  - **Ataque naval**: daña las flotas enemigas y hunde convoyes en los mares de la zona.
  - **Paracaidistas**: una unidad de paracaidistas en un aeródromo con aviones de transporte se lanza («Lanzar en paracaídas...» en su panel) sobre una provincia sin tropas enemigas a su alcance, y la ocupa si es enemiga.
- **Combate aéreo**: cada día las alas enemigas que se cruzan se derriban aviones y la antiaérea dispara a las que atacan su zona. Bajo un cielo enemigo, bombarderos, apoyo cercano y aviación naval hacen la mitad. Nueva alerta «Aviones derribados».
- La ventana de batalla muestra el apoyo aéreo de cada bando y el efecto del cielo.

## [1.114.0] - 2026-10-06

Última parte de la nueva estructura militar: la aviación (1 de 3).

### Añadido
- **Alas de aviones**: los aviones ya no son fichas que caminan, sino alas de 10 aviones con base en aeródromos y portaaviones. Hay cinco tipos: cazas, apoyo cercano, bombarderos estratégicos, aviación naval y transportes aéreos.
- **Dos avances nuevos**, Radar y Motor a reacción. Cada tipo tiene un modelo biplano (Aviación), otro monoplano (Radar) y otro a reacción (Motor a reacción), cada uno con más alcance y fuego.
- **Aeródromo**: edificio nuevo (Aviación) en cualquier provincia tuya, para 4 alas. Las alas se forman con los botones de aviones de su pestaña Ejército; los aviones los fabrican los talleres y las fábricas. El panel de la provincia muestra sus alas.
- Las alas se reparan en su base con suministro (reponen aviones del almacén y tripulaciones de la reserva). Pueden cambiar de base a otro aeródromo o a un portaaviones (2 alas por portaaviones: cazas, apoyo cercano o aviación naval). Si pierden su base, vuelan al aeródromo más cercano con sitio; si no hay, se pierden. Las de un portaaviones hundido caen con él.
- Nueva sección Aviación en la ayuda.

### Cambiado
- Los bombarderos se forman en aeródromos y no en talleres. Producción bélica acelera también las demás alas.
- Las misiones de las alas, el combate aéreo y la pestaña Fuerza aérea llegarán en las dos próximas versiones. Los regimientos de bombarderos de las partidas guardadas siguen como estaban hasta entonces.

## [1.113.0] - 2026-10-06

La marina (3 de 3): convoyes y misiones.

### Añadido
- **Convoyes**: lo que la capital envía a tus tropas al otro lado del mar viaja en convoyes, cada uno con 100 hombres, piezas o suministros. Los envíos van por la ruta real: por tierra hasta uno de tus puertos, por el mar que sabes navegar y por tierra hasta la unidad. Los convoyes de un envío no sirven para otro hasta que llega; sin bastantes, el envío lleva menos y avisa la alerta «Faltan convoyes». Se encargan en lotes de 5 a los astilleros (pestaña Marina), que muestra cuántos tienes, en el mar y en puerto.
- **Misiones de las flotas**, en su panel y en la pestaña Marina:
  - **Patrullar**: va a por las flotas enemigas que entren en los mares vecinos.
  - **Escoltar convoyes**: protege los tuyos en su mar y los vecinos.
  - **Atacar convoyes**: cada día hunde parte de los convoyes enemigos que crucen su mar o los vecinos, y su carga; los submarinos, el doble. La alerta «Convoyes hundidos» avisa de los tuyos.
  - **Bloquear puertos**: un puerto enemigo junto a ella no construye barcos, no envía nada por mar y pierde los impuestos de su comercio marítimo.
- La IA encarga convoyes cuando le faltan y, en guerra, pone sus submarinos a atacar convoyes y sus buques de guerra a patrullar.

### Cambiado
- Los envíos por mar ya no tardan 10 días fijos: tardan lo que se navega por su ruta.
- Las partidas guardadas dan 10 convoyes a cada nación que ya tiene un puerto.

## [1.112.0] - 2026-10-06

La marina (2 de 3): los astilleros.

### Añadido
- **Astilleros con cola**, como en Hearts of Iron: los barcos se encargan a una cola de toda la nación. Cada puerto tiene una grada, y otra más con dique seco; cada grada libre toma el primer barco de la cola que puede construir (un encargo hecho desde un puerto prefiere ese puerto).
- Cada día la grada avanza su barco y paga la parte de su coste que toca a ese día; si no hay bastante, avanza solo lo que puede pagar. Al terminar, el barco toma su tripulación del puerto y de la reserva y sale como una flota nueva; si faltan hombres, espera y avisa.
- **Pestaña Marina** en la pantalla de la nación: tus flotas, tus astilleros con sus gradas, la cola de construcción (adelantar, retrasar o cancelar cada encargo, con su progreso, los días que quedan y lo que cuesta al día) y un botón «Encargar» por cada línea de barcos.

### Cambiado
- Los barcos ya no se pagan de golpe: el botón de un puerto los encarga a la cola. La IA también usa la cola.

## [1.111.0] - 2026-10-06

Quinta parte de la nueva estructura militar: la marina (1 de 3).

### Cambiado
- **Líneas de barcos por época**, como las del ejército. Transportes: barco de transporte (600 hombres), carraca (900, Cartografía), vapor de transporte (1.500) y buque de transporte (2.500, Motor de combustión). Buques de línea: trirreme, galeón, navío de línea (Metalurgia), acorazado y acorazado moderno (Ingeniería naval, con dique seco). Escoltas: liburna, carabela, fragata, crucero (Acero) y destructor. Submarinos (Motor de combustión) y portaaviones.
- Las partidas guardadas pasan cada barco a su línea y modelo: el trirreme, el galeón y el acorazado son buques de línea; el vapor, un transporte, y el destructor, una escolta.

### Añadido
- **Modernización en puerto**: una flota en uno de tus puertos pasa cada día sus barcos al modelo más nuevo de su línea que conozcas, por la mitad de lo que cuesta. Conserva su tripulación y el puerto completa el resto. Los modelos con dique seco lo piden también para modernizarse. El panel de la flota dice qué barcos pueden modernizarse y, si no, por qué.

## [1.110.0] - 2026-10-06

Cuarta parte de la nueva estructura militar: la logística.

### Añadido
- **Envíos desde la capital**: el almacén de la nación está en su capital, y lo que necesitan tus tropas les llega en envíos que tardan lo que se tarda en llegar por la red de suministro (más deprisa por carreteras y ferrocarriles; por mar, desde tu ciudad más cercana y 10 días más). Cada día la capital manda a cada unidad con suministro los reclutas para reforzarla con su equipo, el equipo del modelo nuevo para modernizarla y la munición que ha gastado. Si al llegar la unidad está aislada o ya no existe, el envío vuelve al almacén.
- **Prioridad de suministro**: cada cuartel general tiene prioridad alta, normal o baja (botones en su panel). Cuando no hay para todos, se sirve primero a sus unidades por ese orden; las unidades sin cuartel a su alcance van las últimas y sus envíos tardan el doble. Un cuartel aislado deja a sus unidades sin envíos.
- **Munición**: cada unidad lleva suministros para 24 horas de combate, y cada batallón que combate gasta 0,25 por cada 100 hombres y hora. Sin munición lucha a la mitad. La capital la repone con los suministros del almacén, que fabrican los talleres.
- El panel de una unidad dice su munición y los envíos que van en camino (qué llevan y cuándo llegan, o por qué no le llega nada); la ventana de batalla, la munición de cada unidad; nueva alerta «Sin munición»; la pestaña Almacén suma la columna «En camino»; nueva sección Logística en la ayuda.

### Cambiado
- Los refuerzos y la modernización ya no llegan al momento: viajan desde la capital. Un batallón cuyo modelo nuevo usa las mismas armas (de falanges a legionarios) lo sigue adoptando al momento. Las flotas siguen completando su tripulación en puerto.
- La IA fabrica también los suministros que su ejército necesitaría para reponer toda su munición.
- Las partidas guardadas empiezan con la munición llena y sin envíos en camino.

## [1.109.0] - 2026-10-06

### Añadido
- **Países reales**: las naciones son 49 países de verdad (España, Francia, China, México, Perú, Etiopía...), cada una con sus ciudades y regiones reales. Tu primera ciudad es la capital de tu país (Madrid, París, Pekín...) y «Otro nombre» propone la siguiente; las provincias toman el nombre de sus regiones (Castilla, Normandía, Lanna...). Cuando se acaban, una nación toma nombres de los países que no juegan, luego de los demás y luego variantes como «Norte de Castilla»; solo al final inventa uno.
- **Más naciones**: hasta 12 en el mapa diminuto, 16 en el pequeño, 24 en el mediano y 32 en el grande y la Tierra (antes, 8). A partir de la decimotercera, sus colores se generan para que todas se distingan.

### Cambiado
- **La mitad de provincias**, el doble de grandes, en todos los mapas: el grande tiene 12.500 (unas 10.750 de tierra) en vez de 25.000. Cada una alberga el doble de gente; los yacimientos siguen siendo uno por provincia.
- Las partidas guardadas conservan su mapa y sus nombres.

## [1.108.0] - 2026-10-06

### Añadido
- **Salitre**: lo descubre la Pólvora, junto al azufre. Abunda en los desiertos, algo en estepas y sabanas, y escasea en el resto.
- **Caballos**: los conoces desde el principio. Salen de pastos en praderas y estepas (menos en sabanas, bosques templados, colinas y desiertos) que, a diferencia de los yacimientos, no se agotan.

### Cambiado
- La pólvora es salitre y azufre: las armas de pólvora piden 10 de salitre y 5 de azufre por batallón; los cañones, 15 y 10; la artillería de campaña y la pesada, 10 y 5; la antiaérea, 5 y 5. Los fusiles y las armas modernas cambian su azufre por salitre (5).
- La caballería necesita caballos de verdad: jinetes, catafractos y caballeros, 100 por batallón (y 10 de madera en vez de 20); los carros de guerra, 60 (y 40 de madera en vez de 60). Su mantenimiento gasta algunos cada día.
- Las partidas guardadas reciben llenos los yacimientos y pastos nuevos; los demás siguen donde estaban.

## [1.107.0] - 2026-10-06

### Añadido
- **Piedra**: un recurso nuevo que conoces desde el principio. Sale de canteras, que hay en casi todas las colinas y montañas y en algunas llanuras secas. Empiezas con 50.
- **Azufre**: lo descubre la Pólvora. Sale de yacimientos en las montañas y los desiertos y escasea en el resto, así que se vuelve un bien de comercio.

### Cambiado
- Los edificios de piedra la piden en vez de parte de su madera: templo (30), acueducto (60), anfiteatro (80), muralla (120), castillo (200), universidad (60) y banco (50).
- La pólvora pide azufre: las armas de pólvora (10 por batallón en lugar del carbón), los fusiles y las armas modernas (5), los cañones (20 en lugar del carbón), la artillería de campaña y la pesada (15) y la antiaérea (10).
- Las partidas guardadas reciben la piedra inicial, y sus canteras y yacimientos de azufre empiezan llenos. Los demás yacimientos siguen donde estaban.

## [1.106.0] - 2026-10-06

### Cambiado
- **La infantería comparte las armas de su época**, como en Hearts of Iron: armas antiguas (guerreros, espadachines, arqueros), clásicas (vélites, falanges, legionarios, montañeses), medievales (infantería pesada, ballesteros, almogávares), de pólvora (arcabuceros, piqueros, mosqueteros, cazadores de montaña), fusiles (infantería ligera, fusileros, ametralladores, cazadores alpinos) y armas modernas (tropas de montaña, paracaidistas). Un taller fabrica «Armas clásicas» para todos los que las usan.
- Lo que cuestan las armas es el mismo para todos los modelos de una época; los modelos solo se distinguen por el oro de su instrucción. Las armas clásicas piden cobre; las medievales, hierro; las de pólvora y los fusiles, hierro y carbón; las modernas, hierro, carbón y caucho.
- Un batallón que pasa a un modelo con las mismas armas (de falanges a legionarios) lo hace al momento, sin gastar equipo.
- Las partidas guardadas juntan las armas de cada época.

## [1.105.0] - 2026-10-06

### Añadido
- **Pestaña Unidades** en la pantalla de la nación: todos los tipos de batallón y de barco, por grupo y línea, con sus hombres, ataque, defensa, organización, velocidad, días de instrucción, el equipo que necesita un batallón y su coste. El modelo que entrenas ahora va en negrita; los antiguos y los que faltan por descubrir (con su avance), apagados. Su tooltip cuenta qué lo distingue y dónde se instruye.
- **Barras de desplazamiento** cuando la información no cabe: el panel de la provincia o la unidad, el resumen de la nación, las ramas de la ciencia, las cifras de las plantillas, las tablas, la ayuda y el historial. Se mueven con la rueda, arrastrando la barra o pulsando en ella.

### Cambiado
- **La pestaña Equipo es ahora Almacén**, y además del equipo muestra todos los recursos: lo que hay, lo que entró y salió el último día por producción, comercio, consumo, ejército y talleres, el balance y en cuántos días se agota si baja.
- **La producción de los talleres se titula por lo que se fabrica**, no por quién lo usa: «Armas (legionarios)», «Caballos (jinetes)», «Catapultas», «Cañones», «Tanques», «Suministros»... Para quién es, en su tooltip.
- **Suministros**: exploradores, ingenieros y médicos comparten un mismo suministro, uno por hombre, en lugar de equipos, herramientas y botiquines. Las partidas guardadas juntan lo que tenían. El equipo de los ingenieros cuesta 20 de madera en vez de 40, como el de los demás.
- Lo que gastan los talleres cuenta ya en el cambio diario de los recursos de la barra superior.
- Las pestañas de la nación pasan a letra pequeña cuando su nombre no cabe.

## [1.104.0] - 2026-10-06

### Añadido
- **Exploradores que solo exploran**: el nuevo botón «Explorar» manda a una unidad de exploradores a la tierra desconocida más cercana, y luego a la siguiente, sin reclamar nada ni entrar en tierras enemigas, hasta que no quede nada por descubrir a su alcance. Las unidades que exploran a la vez se reparten. «Explorar y reclamar» sigue como antes.

## [1.103.0] - 2026-10-06

Tercera parte de la nueva estructura militar: el equipo.

### Añadido
- **Equipo**: cada batallón necesita el equipo de su modelo, como en Hearts of Iron. Es un arma por hombre, un caballo por jinete, 30 carros o, siguiendo la nueva estructura, 5 catapultas con 50 hombres o 10 tanques con 50.
- **Los talleres y las fábricas lo fabrican**: en la pestaña Edificios de su provincia eliges qué hace cada uno. Un taller hace el equipo de un batallón en sus días de instrucción; una fábrica, el doble. Gasta los recursos del batallón salvo el oro; si faltan, fabrica menos o nada.
- **Almacén de la nación**: nueva pestaña Equipo en la pantalla de la nación, con lo que hay de cada modelo, lo que se fabrica al día, lo que esperan tus batallones para modernizarse y lo que cuesta.
- Entrenar toma el equipo del almacén y solo cuesta el oro. Se entrena con el modelo más moderno del que haya equipo; sin equipo, no se entrena.
- Reforzar las bajas también gasta equipo.
- Cada nación empieza con equipo para 4 batallones de guerreros y 2 de exploradores.
- Los rivales eligen qué fabrican sus talleres según lo que necesitan.

### Cambiado
- **Modernizar ya no es inmediato**: un batallón con suministro adopta el modelo nuevo de su línea cuando hay equipo para él en el almacén, y devuelve el viejo.
- **El taller se puede construir desde el principio**; las máquinas de guerra siguen pidiendo su avance.
- La artillería y los tanques llevan menos hombres: catapultas y trabuquetes 50, cañones y antiaérea 80, tanques 50.
- Las pestañas de la pantalla de la nación se estrechan para caber todas.

## [1.102.0] - 2026-10-06

Segunda parte de la nueva estructura militar: las formaciones.

### Cambiado
- **Regimientos, brigadas y divisiones**: un regimiento tiene hasta 5 batallones; una brigada, hasta 4 regimientos; una división, hasta 5 brigadas y regimientos y nunca más de 10.000 hombres. Cada una es una sola ficha en el mapa (III, X o XX), que se mueve y combate entera.
- **Organizar las unidades** desde el editor de la unidad, con las de la misma provincia:
  - «Unir» junta los batallones de dos regimientos en uno (hasta 5).
  - «Incorporar» mete una unidad en otra: dos regimientos forman una brigada; un regimiento entra en una brigada, o forma una división con ella si ya tiene cuatro; dos brigadas forman una división; una división acoge regimientos y brigadas.
  - «Separar» saca un regimiento de una brigada, o una brigada o un regimiento de una división, como unidad propia, con su número y su nombre.
  - Los batallones marcados se separan como un regimiento nuevo (hasta 5).
- Regimientos, brigadas y divisiones se numeran aparte. Al crecer, una unidad toma el siguiente número de su nuevo tamaño; el nombre que le diste se queda en la parte que era.
- El oficial asciende con su unidad: coronel para un regimiento, brigadier para una brigada y general de división para una división.
- El panel de la unidad la describe por sus partes («División de 1 brigada y 1 regimiento») y agrupa sus batallones por brigada y regimiento, con sus hombres respecto a su plantilla completa.
- Las plantillas diseñan un regimiento: hasta 5 batallones.
- Los rivales llenan sus regimientos y los agrupan en brigadas y divisiones.

## [1.101.0] - 2026-10-06

Primera parte de la nueva estructura militar: los tipos de batallón.

### Cambiado
- **Líneas de batallón que evolucionan con las épocas**: cada tipo de batallón es ahora una línea con un modelo por época, que llega con su avance. Al descubrirlo, tus batallones de esa línea lo adoptan en el día y desde entonces se entrena el nuevo.
  - **Infantería ligera**: guerreros → vélites → arcabuceros → infantería ligera.
  - **Infantería pesada**: espadachines → falanges → legionarios → infantería pesada → piqueros; con el Estriado pasa a infantería ligera.
  - **Infantería a distancia**: arqueros → ballesteros → mosqueteros → fusileros → ametralladores.
  - **Caballería**: jinetes → catafractos → caballeros → caballería mecanizada. **Carros**: carros de guerra → tanques.
  - **Artillería**: catapultas → trabuquetes → cañones → artillería de campaña → artillería pesada.
- El ejército se organiza en cuatro grupos: infantería, caballería, artillería y apoyo.
- Las plantillas guardan líneas, no modelos: con cada avance, sus batallones se entrenan con el modelo nuevo.
- Los avances que aceleran la instrucción aceleran líneas enteras (Arcos mejorados, toda la infantería a distancia).

### Añadido
- **Tropas de montaña** (desde la era Clásica, mejores en cada era): un 50 % más de fuego en montañas, colinas, bosques y pantanos.
- **Paracaidistas** (Aviación); podrán saltar cuando haya aviones de transporte.
- **Artillería antiaérea** (Aviación): dispara el doble contra los aviones y cada batallón les quita un 25 % de fuego, hasta un 75 %.
- **Médicos** (Medicina, 50 hombres): no combaten, pero salvan un 30 % de las bajas de su unidad.
- Nuevos símbolos OTAN para montaña, paracaidistas, antiaérea y médicos; los carros de guerra se dibujan como caballería.

### Quitado
- Lanceros de bronce, infantería de hierro, carros de arqueros e infantería motorizada, que ya cubren las nuevas líneas.

### Partidas guardadas
- Las partidas de versiones anteriores ya no se pueden cargar.

## [1.100.0] - 2026-10-05

### Cambiado
- **Unidades apiladas de lejos, desplegadas de cerca**: con el mapa alejado, las unidades de una provincia están unas encima de otras en su centro, sobre la ciudad si la hay, con la seleccionada encima. Al acercar el zoom se despliegan en círculo alrededor del centro de la provincia, sin taparse entre ellas ni el nombre de la ciudad. Las que marchan siguen en su camino.

## [1.99.0] - 2026-10-05

### Añadido
- **Icono de la ciencia**: en el aviso «Ciencia sin elegir», en la pestaña Ciencia de la pantalla de la nación y junto a los puntos de ciencia al día.

## [1.98.0] - 2026-10-05

### Añadido
- **Iconos propios de edificios y recursos**: 21 edificios y 11 recursos con su icono, en los paneles, la barra superior, el filtro de recursos y el mapa.
- **Ciudades con icono que crece**: en el mapa, una ciudad es una cabaña con menos de 2.000 habitantes, un pueblo con menos de 10.000 y una ciudad desde ahí; la capital siempre es una ciudad, con un aro dorado en su base. En la época moderna, la capital y las grandes son ciudades modernas.
- De cerca, bajo el nombre de cada ciudad, los iconos de sus edificios (antes solo con figuras 3D).

### Cambiado
- Los iconos van reducidos a 128 píxeles (0,6 MB en lugar de 19); los originales se guardan aparte, en `originales/`, que no entra en el juego ni en git.

## [1.97.0] - 2026-10-05

### Añadido
- **Iconos de los yacimientos en el mapa**: en el modo Recursos, de cerca, cada provincia muestra en fila los iconos de todos sus yacimientos conocidos (del más rico al más pobre), en lugar de colorearse solo por el principal. Con el filtro, solo el del recurso elegido. De lejos, donde no caben, sigue el color.
- **Preparado para iconos de recursos**: si en `Assets/ResourceIcons` hay una imagen con el nombre de un recurso (`oro.png`, `petroleo.png`...), sustituye al icono dibujado en todas partes: la barra superior, los paneles, el filtro y el mapa. La lista de nombres está en el `LEEME.txt` de esa carpeta.

## [1.96.0] - 2026-10-05

### Añadido
- **Preparado para iconos de edificios**: si en `Assets/BuildingIcons` hay una imagen con el nombre de un edificio (`granja.png`, `cuartel.png`, `central-electrica.png`...), sale junto a su nombre en la pestaña Edificios de la provincia, en los construidos y en los botones para construir. Los edificios sin imagen se ven como antes. La lista de nombres está en el `LEEME.txt` de esa carpeta.

## [1.95.0] - 2026-10-05

### Añadido
- **Primeros retratos pintados**: 113 retratos de oficiales para las épocas clásica, medieval, industrial y moderna, de los seis rangos, hombres y mujeres, del ejército, la marina, la caballería (industrial) y la aviación (moderna). Las épocas antigua y del Renacimiento siguen con el retrato dibujado.

### Cambiado
- Los retratos van en la carpeta del juego reducidos a 256 píxeles de alto y en JPG (2 MB en total en lugar de 254); los originales a tamaño completo se guardan aparte, en `Assets/Portraits/originales`, que no entra en el juego ni en git.
- Los retratos pueden ser verticales: el juego recorta un cuadrado, más cerca de arriba.

## [1.94.0] - 2026-10-05

### Añadido
- **Retratos de caballería**: el grupo `caballeria` (por ejemplo `coronel-industrial-hombre-caballeria-01.png`) es para los oficiales al mando de un regimiento sobre todo de caballería. Si su época no tiene retratos de caballería, toman uno del ejército.

### Corregido
- `docs/RETRATOS.md` decía que los aviadores existen en la época industrial: la Aviación es de la moderna. Ahora explica qué grupos tiene sentido pintar en cada época (en la clásica y la medieval basta con `ejercito` y `marina`).

## [1.93.0] - 2026-10-05

### Cambiado
- **Nuevo nombre para los retratos pintados**: `rango-época-sexo-grupo-número`, donde el grupo es el arma del oficial (`ejercito`, `marina` o `aviacion`). Por ejemplo `coronel-renacimiento-hombre-ejercito-03.png` o `division-moderna-mujer-marina-01.png`. Ya no valen los retratos sin rango.
- Los oficiales de marina y de aviación también pueden ser mujeres en los retratos pintados; si no hay retratos de su arma, toman uno del ejército.
- `docs/RETRATOS.md` y `Assets/Portraits/LEEME.txt` explican los nuevos nombres.

## [1.92.0] - 2026-10-05

### Añadido
- **Créditos**: un botón «Créditos» en la pantalla inicial muestra quién hizo el juego y de quién son la música, los sonidos, los gráficos, las tipografías y los datos del mapa, con sus licencias. Al pie de la pantalla, el copyright.
- **Música en la pantalla de carga**: mientras se crea o se carga la partida suena «Lord of the Land», de Kevin MacLeod, que sigue sonando si la partida empieza en paz en las primeras eras.

### Cambiado
- **Niebla de guerra total**: solo se ve el mundo que has explorado, sea tuyo o no. El resto está en negro: ni tierra, ni ríos, ni caminos, ni ciudades, ni fronteras, ni nombres de naciones; no se puede seleccionar y el tooltip solo dice «Tierra inexplorada». Lo explorado que ya no ves sigue en gris. Lo explorado se guarda con la partida; en las partidas guardadas anteriores empiezas conociendo solo lo que ves.
- **Los colonos se marcan con un triángulo**, en lugar de la carreta, tanto con fichas como con figuras 3D.

### Corregido
- Los migrantes enviados con la migración forzada no aparecían en la población de su destino mientras viajaban: ahora la población de la provincia y su tooltip dicen cuántos vienen en camino («+50 en camino») hasta que llegan y se suman.

## [1.91.0] - 2026-10-02

### Añadido
- **Tres armas para los oficiales**: ejército, armada y aviación. Cada oficial pertenece a una desde que se recluta, y cada arma tiene sus nombres para los mismos seis rangos:
  - **Ejército**: coronel, brigadier, general de división, teniente general, general y mariscal.
  - **Armada**: capitán de navío, comodoro, contraalmirante, vicealmirante, almirante y gran almirante.
  - **Aviación**: coronel de aviación, general de brigada aérea, general de división aérea, teniente general del aire, general del aire y mariscal del aire.
- **Las flotas tienen oficial**, de marina. Sus rasgos y su habilidad afectan al fuego de los barcos en las batallas navales, y gana estrellas con las victorias en el mar. Asciende con el tamaño de la flota, como en tierra.
- Los regimientos de aviones los mandan oficiales de aviación; el resto de regimientos y los cuarteles generales, los del ejército. Solo un oficial del arma de la unidad puede mandarla.
- En el editor de la unidad, la reserva muestra los oficiales de su arma y el botón recluta uno de esa arma. Los de marina piden una flota o un puerto; los de aviación, la Aviación.
- Los rivales también ponen oficiales de marina al frente de sus flotas.
- Los retratos de los aviadores llevan uniforme azul grisáceo y gorra de plato. Los retratos pintados aceptan el grupo `aviador`.
- En las partidas guardadas anteriores, todos los oficiales son del ejército.

## [1.90.0] - 2026-10-02

### Cambiado
- **Retratos pintados por rango**: el rango va en el nombre de la imagen (`renacimiento-hombre-coronel-03.png`) y el propio retrato lo muestra, así que el juego ya no le pinta insignias encima, solo un marco fino del color de la nación. Si falta el rango de un oficial, toma un retrato del rango más cercano.
- Al ascender, el oficial pasa a un retrato de su nuevo rango.
- `docs/RETRATOS.md` explica los rangos y sus equivalentes en la marina, cuántos retratos conviene hacer de cada rango y cómo se nota cada rango en cada época.

## [1.89.0] - 2026-10-02

### Añadido
- **Opciones en la pantalla inicial**: un botón «Opciones» abre una ventana con el volumen de la música y del sonido (con - y +, de cuarto en cuarto), las animaciones (sí o no) y el aspecto del mapa (figuras 3D o fichas). Los cambios se aplican al momento y se recuerdan entre partidas.

### Cambiado
- El menú de pausa abre esa misma ventana con su botón «Opciones», en lugar de tener un botón para cada ajuste. Mientras está abierta, el tiempo se para; Esc la cierra.

## [1.88.0] - 2026-10-02

### Añadido
- **Preparado para retratos pintados**: si en `Assets/Portraits` hay imágenes de una época (por ejemplo `renacimiento-hombre-03.png`), los oficiales de esa época toman una de su sexo, o de marino si mandan una flota, y siempre la misma. El juego les pone un marco del color de su nación y su rango en una banda abajo. Las épocas sin imágenes siguen con el retrato dibujado.
- `docs/RETRATOS.md` explica cómo crearlas con una IA de imágenes: formato, nombres, cuántas hacen falta, un estilo común y la descripción de cada época para oficiales, oficiales mujeres y marinos.

## [1.87.0] - 2026-10-02

### Añadido
- **Retratos de los oficiales**, traídos de la otra versión del juego. Cada oficial y general tiene una cara propia, sacada de su nombre, así que nunca cambia y las partidas guardadas no necesitan nada nuevo.
  - Lleva el uniforme y el tocado de su época en el color de su nación: casco de bronce con cresta en la Antigüedad, yelmo nasal en la Edad Media, tricornio, quepis y gorra de plato; de marino si manda una flota.
  - Los galones (coronel) o las estrellas (generales) de los hombros dicen su rango, cada estrella de habilidad más allá de la primera añade un pasador en el pecho y el pelo encanece a medida que gana experiencia.
  - Se ven en el editor de la unidad (el oficial al mando y los de la reserva) y en el panel de la unidad, bajo su oficial y su general.

## [1.86.0] - 2026-10-02

### Cambiado
- **Nueva tipografía**, la de la otra versión del juego: **Lilita One**, redonda y robusta, para toda la interfaz, y **Cinzel Bold**, en capitales romanas, para los títulos de las ventanas, los encabezados, el nombre del juego y los de las naciones en el mapa.
- **Fichas en relieve**: con «Mapa: fichas», cada ficha OTAN es un bloque como las piezas de un wargame de tablero, iluminado desde arriba a la izquierda, con su grosor, un bisel, una sombra y la cara sombreada de clara a oscura.

## [1.85.0] - 2026-10-02

### Añadido
- **Maquetas isométricas en el mapa**, traídas de la otra versión del juego:
  - **Unidades**: cada regimiento es una figurita sobre una peana del color de su nación, que mira hacia donde marcha. Los soldados visten según su época: casco de bronce y lanza, tricornio y mosquete, casco de acero y fusil. También hay jinetes, catapultas, cañones, obuses, ingenieros, camiones y tanques, y los colonos van en carreta. Uniformes y banderas se pintan del color de la nación.
  - **Flotas**: su barco más fuerte, del trirreme al portaaviones.
  - **Ciudades**: un castillo en la capital y un pueblo en las demás, sobre una mancha del color de su dueño.
  - **Edificios**: de cerca, bajo el nombre de la ciudad, una fila de maquetas con lo que hay construido (granja, taller, biblioteca, mercado, fuerte y puerto).
  - Las unidades de una provincia se ponen en fila, a la derecha de la ciudad si la hay. Los cuarteles generales siguen con su ficha.
- En el menú de pausa, **«Mapa: figuras 3D / fichas»** vuelve a las fichas OTAN y las casitas de siempre.
- Las maquetas de ciudades y edificios salen del Hexagon Kit de Kenney (dominio público); las de unidades se hicieron para la otra versión al estilo de esos kits. Se citan en el README.

## [1.84.0] - 2026-10-02

### Añadido
- **Animaciones en el mapa**, traídas de la otra versión del juego:
  - Las unidades **saltan** al marchar, la seleccionada **se balancea** y las que combaten **tiemblan**.
  - Alrededor de cada batalla **estallan proyectiles**.
  - Las flotas **se mecen** con el oleaje y dejan una **estela** de espuma al navegar.
  - Sale **humo** de las ciudades, más oscuro y espeso de las que tienen taller o fábrica.
  - Cada cosa se mueve a su ritmo, desfasada de las demás.
- En el menú de pausa, un botón **«Animaciones»** las apaga o las enciende. Se recuerda entre partidas.

### Corregido
- Los botones de volumen de la música y del sonido vuelven a estar en el menú de pausa, como decía la ayuda.

## [1.83.0] - 2026-10-02

### Cambiado
- **Un mar más vivo**, traído de la otra versión del juego:
  - Junto a las costas, el agua se vuelve **turquesa** sobre la plataforma y se oscurece mar adentro, sin saltos entre provincias.
  - Un **oleaje lento** recorre el mar y, de cerca, el sol brilla en el agua.
  - Las **olas llegan a la orilla** y la espuma rompe en ella, también con el mapa más alejado.

## [1.82.0] - 2026-10-02

### Añadido
- **Victoria**: hay tres formas de ganar la partida, y los rivales también pueden ganarla.
  - **Dominación**: haz caer a todas las demás naciones o conviértelas en tus vasallas.
  - **Ciencia**: descubre todos los avances.
  - **Puntuación**: si nadie ha ganado antes, al empezar el año 300 gana la nación con más puntos. Se gana un punto por cada 1.000 habitantes, 5 por provincia, 20 por ciudad y 10 por avance.
- **Derrota**: pierdes si gana otra nación o si cae la tuya.
- Al decidirse la partida se abre una ventana con el resultado y la clasificación final. Puedes **seguir jugando** (o mirando, si tu nación ha caído) o volver al menú principal.
- El Resumen de la nación tiene una nueva sección **Victoria**: tu puntuación y tu puesto, los avances que te faltan, las naciones libres que quedan y, si lo hay, el ganador.

## [1.81.0] - 2026-10-02

### Añadido
- **Objetivos para empezar**: una tarjeta bajo las alertas te guía por los primeros pasos y te dice cómo dar cada uno.
  - Son nueve: fundar la capital, elegir qué investigar, poner en marcha una obra, entrenar un regimiento, reclamar una provincia, descubrir un avance, fundar una segunda ciudad, llegar a 10.000 habitantes y firmar un acuerdo con otra nación.
  - Cada uno da **50 de oro**. Se pueden cumplir en cualquier orden, y la tarjeta muestra siempre el primero que falta.
  - El botón **«Ir»** lleva adonde se cumple: los colonos, la ciencia, la capital, los exploradores o la diplomacia. Con «-» la tarjeta se pliega en una línea.
  - En las partidas anteriores, los objetivos ya cumplidos cuentan como tales, sin pagarse.

## [1.80.0] - 2026-10-02

### Añadido
- **Estadísticas**: nueva pestaña de la nación con una gráfica de cómo ha cambiado cada nación desde el principio de la partida.
  - Elige la cifra: **población, ejército, oro al día, provincias o ciencia al día**.
  - Cada nación es una línea de su color, la tuya más gruesa. La leyenda da la cifra de hoy de cada una, y las eliminadas siguen ahí hasta el día en que cayeron.
  - Las cifras se anotan cada 30 días y se guardan con la partida. En las partidas anteriores, el registro empieza al cargarlas.

## [1.79.0] - 2026-10-02

### Añadido
- **Eventos con decisiones**: alguna vez al año pasa algo en una de tus provincias y una ventana te pide que elijas. El tiempo se para hasta que respondes.
  - **Sequía**: guardar el grano (la provincia pierde moral y gente) o repartirlo (cuesta comida y la alegra).
  - **Un filón de oro**: para la corona (mucho oro y descontento) o para los mineros (menos oro y contento).
  - **Refugiados**: cerrar las puertas o acogerlos (más gente, algo menos de moral).
  - **Bandidos**: dejarlos (se pierde gente y moral) o enviar soldados (cuesta oro).
  - **Una gran cosecha**: vender el excedente (oro) o celebrarla (un mes de fiestas).
  - **Un inventor**: despedirlo o financiarlo (cuesta oro y da un mes de ciencia).
  - Lo que está en juego crece con tu nación. La moral que cambia dura un año y sale en la moral de la provincia con el nombre del evento.
  - Si no respondes en 30 días, se elige la primera opción. Los rivales también viven estos eventos y deciden al momento.
  - El botón "Ver en el mapa" lleva a la provincia.

## [1.78.0] - 2026-10-02

### Añadido
- **Epidemias**: de vez en cuando una ciudad enferma, más a menudo cuanto más grande es.
  - Durante 90 días muere cada día el 0,1 % de su gente y la provincia pierde 15 de moral.
  - **Contagio**: pasa a las provincias vecinas, seis veces más deprisa por un camino, y de un puerto a otros a menos de 2000 km.
  - **Resistencia**: la Medicina y la Salubridad la frenan un 25 %, los Antibióticos un 40 %, el herbolario un 20 % y el hospital un 40 %, hasta un 90 % en total. Frena tanto las muertes como el contagio.
  - Al terminar, la provincia queda **inmune 10 años**.
  - Nueva alerta "Epidemia" con tus provincias enfermas, los días que les quedan y los muertos al día. El panel de la provincia y su tooltip también lo muestran.

## [1.77.0] - 2026-10-02

### Añadido
- **Religiones**: cada nación sigue una de cinco fes, el Culto del Sol, la Fe del Río, los Antiguos Dioses, el Camino de los Astros y la Madre Tierra. La gente de cada provincia conserva la fe de quien la pobló.
  - **Otra fe**: bajo un dueño de otra religión, la provincia pierde 10 de moral hasta convertirse. Tarda unos 25 años: más deprisa con moral alta, el doble con un templo y un 50 % más con la Teología. Los migrantes de tus otras provincias también ayudan.
  - **Opinión**: las naciones de la misma fe se ven mejor (+10) y las de otra, peor (−10). Los rivales atacan con algo más de ganas a los de otra fe.
  - **Nuevo modo de mapa, Religión**: cada provincia, del color de su fe.
  - Tu religión sale en el Resumen de la nación, la de cada nación en el tooltip de su relación, y la de cada provincia en su panel, con lo convertida que está y los años que le faltan.
- En las partidas guardadas anteriores, cada nación recibe una fe según su número, y su gente la comparte.

### Cambiado
- En la pestaña Diplomacia, las columnas Provincias y Ocupación ya no se pisan.

## [1.76.0] - 2026-10-02

### Añadido
- **Niebla de guerra**: solo ves las tropas y las batallas ajenas donde llega tu vista:
  - En tus tierras y en las que ocupas, en las de tus aliados, tu señor y tus vasallos, y en una provincia alrededor de todas ellas.
  - Alrededor de cada una de tus unidades, una provincia. **Los exploradores ven dos**, así que sirven para algo más que reclamar tierra.
  - Lo que no ves se dibuja **en gris y más oscuro**. Las unidades que entran en la niebla desaparecen del mapa y, si estaban seleccionadas, se deseleccionan.
  - Los aliados, el señor y los vasallos comparten lo que ven.

## [1.75.0] - 2026-10-02

### Añadido
- **Reserva de reclutas**: los soldados siguen saliendo de la población, pero solo se alistan los que hay en la reserva.
  - Caben **500 más el 6 % de tu población**. Instrucción militar la agranda un 25 % y Servicio militar obligatorio un 50 %.
  - Entrenar batallones y barcos, formar cuarteles generales y reforzar unidades **gasta reclutas**. Licenciar una unidad devuelve sus hombres.
  - **Se rellena despacio**: de vacía a llena en 2 años. Perder un ejército duele de verdad.
  - Lo que queda se ve en el Resumen de la nación, en el título de la pestaña Ejército y en el tooltip de la población de la barra superior. Sin reclutas, el botón de entrenar lo explica.
- Las partidas guardadas anteriores empiezan con la reserva llena.

## [1.74.0] - 2026-10-02

### Añadido
- **Desgaste en las alturas**: en cualquier estación, los regimientos pierden un 0,4 % de sus hombres al día en las cumbres y el hielo polar, y un 0,1 % en la alta montaña. El panel de la provincia lo explica, y la alerta «Desgaste» incluye a esas unidades.
- **Alerta «Obras terminadas»**, en verde: las obras que has terminado en los últimos 3 días. Cada clic lleva a la siguiente, para empezar allí otra.

## [1.73.0] - 2026-10-02

### Añadido
- **Comercio de recursos**, en una pestaña nueva de la pantalla de la nación, **Comercio**:
  - Cada nación en paz ofrece **la mitad de lo que gana al día** de cada recurso que le sobra. Lo vende por oro o a cambio del recurso tuyo que más le falta, y te compra por oro lo que te sobra a ti.
  - Cada recurso tiene un **valor en oro**: la comida 0,1, la madera 0,5, el carbón y el cobre 1,5, el hierro y la plata 2, el caucho 3, y el silicio, el petróleo y el aluminio 4. Los rivales se quedan un **margen** de hasta el 50 %, menor cuanto mejor te ven.
  - Pulsa **Firmar** para un acuerdo de un año: cada día se entregan los bienes y el pago, que se suman al balance diario. Hay como mucho 6 acuerdos por nación y uno por recurso con cada nación.
  - Cada acuerdo sube 5 la opinión de cada nación sobre la otra (hasta 15). **Cancelar** uno antes de tiempo deja un mal recuerdo (−10).
  - Un acuerdo se rompe si una parte no puede entregar o pagar, o si estalla una guerra entre las dos.
- **Los rivales comercian entre ellos**: compran por oro los recursos que conocen y no producen al rival que más tiene de sobra.

## [1.72.0] - 2026-10-02

### Añadido
- **Pactos de no agresión**, con el botón **Pacto** de la pestaña Diplomacia:
  - Mientras dure, ninguno de los dos puede declarar la guerra al otro. La relación muestra «Pacto» y la opinión sube 10.
  - **Sin pacto** lo rompe: hay un año de tregua antes de poder atacar, y el otro lo recuerda (−30).
  - Un aliado que tiene un pacto con el atacante no entra en la guerra.
  - Los rivales lo firman si su opinión de ti es al menos 0, o −25 si tu ejército es más fuerte. Entre ellos, buscan un pacto con el vecino más fuerte que les da miedo.
- **Paso militar**:
  - **Pedir paso** deja a tus ejércitos cruzar sus tierras. Aceptan si su opinión de ti llega a 20.
  - **Dar paso** deja a los suyos cruzar las tuyas, y su opinión de ti sube 10. **Cerrar paso** lo retira y sus tropas vuelven a casa.
  - El tooltip de la relación dice quién deja pasar a quién. Una guerra entre los dos acaba con el paso y con el pacto.

### Cambiado
- Al romper una alianza, las tropas que estaban en tierras del otro vuelven a casa.

## [1.71.0] - 2026-10-02

### Añadido
- **Dos tratados de paz nuevos** en la pestaña Diplomacia, en guerra:
  - **Tributo** (30 de puntuación de guerra): el enemigo te paga el 25 % de sus ingresos de oro durante 5 años. Lo recordará (−30 de opinión).
  - **Vasallo** (60 de puntuación): el enemigo pasa a ser tu vasallo. Te paga el 10 % de sus ingresos de oro y entra en tus guerras; si lo atacan, entras tú en las suyas. No puede declarar guerras ni aliarse con nadie, y sus tierras están abiertas a tus ejércitos.
  - En los dos casos, las provincias ocupadas vuelven a sus dueños. La IA los acepta en las mismas condiciones que Exigir.
- **Vasallos**: en la Diplomacia aparecen como «Vasallo» con los años que llevan. A los 10 años puedes **anexionarlo** (sus provincias, ciudades y gente pasan a ser tuyas) o, cuando quieras, **liberarlo** (+40 de opinión). La relación de cada nación dice en el tooltip quién es su señor, quiénes sus vasallos y qué reparaciones se pagan.
- **Capitulación**: una nación en guerra con todas sus ciudades ocupadas se rinde. Cada enemigo se queda con lo que ocupa y quien tiene su capital (o, si no, quien más ocupa) se lleva el resto.
- **Las naciones que pierden todas sus tierras desaparecen**, por capitulación, por tratado o anexionadas por su señor. Sus ejércitos, guerras, alianzas y tratados terminan, y salen de la pestaña Diplomacia.
- **Los rivales también lo usan entre ellos**: hacen vasallo a un enemigo con menos de la mitad de sus provincias, imponen tributos en las guerras largas que ganan y anexionan a sus vasallos en cuanto pueden.

### Cambiado
- La pantalla de la nación es más ancha (hasta 1.260 píxeles) para que quepan los botones de la diplomacia.

## [1.70.2] - 2026-10-02

### Añadido
- **Nueva pista para la paz en las primeras eras**: «Lord of the Land», de Kevin MacLeod (incompetech.com, licencia CC BY 4.0; se cita en el README y en `Assets/Audio/CREDITS.txt`).

## [1.70.1] - 2026-10-02

### Cambiado
- **Nueva música en la pantalla inicial**: «The Britons», de Kevin MacLeod (FreePD.com, dominio público).

## [1.70.0] - 2026-10-02

### Añadido
- **Música**: 11 pistas que cambian según la situación.
  - **Hasta la Edad Media**, música medieval: cuatro pistas para la paz y dos para la guerra.
  - **Desde la era de los Descubrimientos**, música orquestal: tres pistas para la paz y dos para la guerra.
  - Al declararse o acabar una guerra, o al cambiar de era, la pista se funde y empieza otra que encaja.
  - En la pantalla inicial suena un tema orquestal.
- **Efectos de sonido**:
  - **Guerra**: tambores al declarar la guerra o al entrar en una.
  - **Paz**: una fanfarria al firmarla.
  - Campana en los asedios y las revueltas, choque metálico al empezar una batalla y monedas en los regalos.
  - Melodía al fundar una ciudad, martillo al terminar una obra y página al descubrir un avance.
  - Una campanilla al cumplirse una orden, un clic en los botones y un aviso cuando aparece una alerta grave.
- **Volumen**: en el menú de pausa y en la pantalla inicial hay un botón para la música y otro para el sonido. Cada pulsación sube el volumen un cuarto y, después del máximo, lo apaga. Se recuerda entre partidas.
- Todo el audio es de dominio público (CC0): Kenney.nl y autores de OpenGameArt.org. Los créditos están en el README y en `Assets/Audio/CREDITS.txt`.

## [1.69.0] - 2026-10-02

### Añadido
- **Opinión entre naciones** (de −100 a 100), en una columna nueva de la pestaña Diplomacia. Dice lo que cada nación piensa de ti, y el tooltip explica por qué:
  - **Lo que ve ahora**: alianza (+30), guerra (−50), frontera común (−10), un enemigo común en guerra (+25), un **rival común** (+20: los dos lindan con una nación más fuerte) y gente de su cultura bajo tu gobierno (−5 por provincia, hasta −30).
  - **Lo que recuerda**: que le declaraste la guerra (−60), que le quitaste provincias en un tratado (−5 cada una), que rompiste una alianza (−60) y tus regalos (+15 cada uno). Los recuerdos se olvidan poco a poco (0,1 al día).
- **Regalos**: envía oro a otra nación (un mes de tus ingresos de oro, al menos 50) y su opinión de ti sube 15.
- **Alianzas** (hasta 3 por nación):
  - **Si uno de los aliados es atacado, los demás entran en la guerra.** Te avisa quién se une y contra quién.
  - **Los ejércitos aliados pueden cruzar las tierras del otro.**
  - Los aliados no pueden declararse la guerra entre sí; hay que romper antes la alianza, y el otro lo recordará.
  - Los rivales aceptan si su opinión de ti llega a 40.
- **Los rivales también hacen diplomacia**: buscan alianza con quien comparte su rival o su enemigo, hacen regalos a quien aún no se fía de ellos, y antes de atacar cuentan el ejército de los aliados de la víctima. Una nación a la que odian les parece más débil de lo que es.
- La relación muestra «Aliados», y su tooltip dice qué aliados tiene cada nación. El botón de guerra avisa de qué aliados entrarán en ella.

### Cambiado
- En la pestaña Diplomacia, los botones en paz son ahora **Guerra**, **Aliarse** (o **Romper**) y **Regalo**.

## [1.68.0] - 2026-10-02

### Añadido
- **Asedios**: una provincia con murallas o castillo ya no se ocupa con solo entrar sin defensores. Las tropas que entran la **sitian**, y cae a los **30 días si tiene murallas** o a los **60 si tiene castillo**.
  - **La artillería lo acelera**: cada batallón completo de catapultas suma un día de asedio al día, y los cañones y la artillería, más según su ataque.
  - **Si los sitiadores se van, el asedio se levanta.** También termina con la paz o si la provincia se cede.
  - **Los sitiadores reciben suministro** si la provincia sitiada linda con una tuya abastecida.
  - El panel de la provincia muestra quién la sitia, cuánto lleva y cuándo caerá. En las fortificadas que no están sitiadas, cuántos días de asedio aguantan. El tooltip del mapa también lo dice.
  - **Alerta «Sitiadas»** cuando el enemigo sitia provincias tuyas.
  - Los rivales se quedan a terminar sus asedios en lugar de seguir de largo.
- Las murallas y el castillo explican en su descripción cuántos días de asedio piden.

## [1.67.0] - 2026-10-02

### Añadido
- **Alertas** bajo la barra superior, a la izquierda, cuando algo necesita atención. Al pasar el ratón explican qué ocurre y dónde (hasta 5 lugares o unidades), y al hacer clic te llevan allí:
  - **Hambre**, o **comida para pocos días** cuando se come más de lo que se cosecha.
  - **Sin paga**: el ejército no cobra.
  - **Atacados**: provincias tuyas donde el enemigo ataca.
  - **Descontento**: provincias con la moral por debajo de 25 o camino de la rebelión, con su porcentaje y si tienen guarnición.
  - **Sin suministro** y **Desgaste**: unidades que pierden hombres fuera del suministro, o por el frío o el desierto.
  - **Ciencia sin elegir**: ramas sin nada que investigar.
- En las alertas de provincias y unidades, cada clic centra el mapa en la siguiente. Las de comida, paga y ciencia abren la pestaña de la nación que corresponde.
- La marca de cada alerta es roja si es grave y dorada si es un aviso.

## [1.66.0] - 2026-10-02

### Cambiado
- **Calendario propio**: la partida empieza el 1 de enero del **año 1**, y la fecha se muestra como «12 mar, año 37». Los años cuentan la vida de tu civilización, no la historia real: las eras son etapas de desarrollo, no fechas. Antes empezaba en el 4000 a. C. y, con el ritmo del juego, se llegaba a la era Moderna antes del 3900 a. C.
- **El Renacimiento pasa a llamarse era de los Descubrimientos**, que describe mejor una etapa en la que llegan la Cartografía y los barcos que cruzan el océano.
- Las partidas guardadas se cargan igual; solo cambia cómo se muestra la fecha.

## [1.65.0] - 2026-10-01

### Añadido
- **Los rivales invaden por mar.** Hasta ahora no sabían embarcar, así que en los mapas donde cada nación tiene su continente nunca había guerras entre ellos. Ahora, con un puerto y transportes:
  - También declaran la guerra a naciones de ultramar: con Navegación a vela, a costas a menos de 1.500 km de sus puertos; con Cartografía, hasta 8.000 km. Piden algo más de ventaja que contra un vecino por tierra.
  - En esas guerras reúnen en su mayor puerto hasta 4 regimientos (la mitad de su ejército) y construyen transportes hasta que quepan todos.
  - Embarcan, navegan hasta la costa enemiga peor defendida, desembarcan y combaten allí. Los barcos vuelven a puerto.
  - Si no encuentran ruta a ninguna costa enemiga, las tropas vuelven a tierra.
  - En una partida de prueba en un mapa aleatorio, desde el año 22 hubo 6 guerras de ultramar con desembarcos y 4 tratados con cesiones de provincias.

## [1.64.0] - 2026-10-01

### Añadido
- **Estaciones**: de diciembre a febrero es invierno en el norte y verano en el sur. La fecha de la barra superior dice la estación en tu capital, y el panel de cada provincia la de allí.
  - **Nieve**: lejos del trópico (desde 30° de latitud, al máximo desde 60°), en invierno las tropas tardan hasta el doble en marchar.
  - **Frío**: en invierno, los regimientos pierden hasta un 0,5 % de sus hombres al día, salvo si se refugian en una ciudad de su nación. Una unidad que se queda sin hombres muere de frío.
  - **Barro**: en primavera y otoño, donde el invierno es duro, las tropas marchan hasta un 50 % más despacio.
  - **El mapa de terreno se cubre de nieve** en invierno, más espesa cuanto más duro es.
- **Desierto**: los regimientos pierden un 0,2 % de sus hombres al día por el calor y la sed, en cualquier estación.
- Los rivales, en paz, llevan a sus tropas a sus ciudades cuando el frío o el desierto las desgasta.

## [1.63.0] - 2026-10-01

### Cambiado
- **Población realista.** Hasta ahora la población crecía un 55 % al año y la tierra admitía casi 900 millones de personas desde el 4000 a. C. Una nación pasaba de 3.000 a 125 millones de habitantes en 10 años, el oro y la comida se contaban por miles de millones y todos los avances se descubrían en 3 años. Ahora:
  - **La gente crece despacio**: un 1,5 % al año con fertilidad normal, el doble en las ciudades y más con buena moral y avances. En la práctica, unos pocos por ciento al año.
  - **La tierra alimenta al principio la décima parte que antes**, unas decenas de millones en todo el mundo, y **cada era alimenta a más gente**: ×1,5 en la Clásica, ×2 en la Medieval, ×3 en el Renacimiento, ×5 en la Industrial y ×8 en la Moderna.
  - **La tierra vacía ya no se llena sola**: los nacimientos de base son pocos y fijos por provincia, no proporcionales a lo que la tierra podría alimentar.
  - **Las ciudades envían menos emigrantes** (un 0,01 % al día en lugar del 0,15 %), para no vaciarse.
  - **La comida almacenada se estropea**: cada día se pierde el 1 % de lo guardado.
- **Las eras duran generaciones**: cada avance cuesta el triple y la ciencia fija de cada ciudad baja de 0,5 a 0,25 puntos al día. En una partida de prueba, un rival llega a la era Clásica hacia el año 5, al Renacimiento hacia el 20, a la Industrial hacia el 30 y a la Moderna hacia el 50.
- Con todo ello, **los soldados salen caros en gente y el oro vuelve a importar**: hacia el año 30 una nación tiene unas 50.000 personas y gana unos 200 de oro al día.
- Los rivales levantan el cuartel antes que la granja y el granero, para tener ejército desde el principio.
- **Partidas anteriores**: al cargarlas, cada provincia se queda con la gente que su tierra alimenta.

## [1.62.0] - 2026-10-01

### Añadido
- **Treguas**: tras cada paz, las dos naciones no pueden volver a declararse la guerra durante 2 años. La pestaña Diplomacia muestra los días que faltan y desactiva el botón de declarar la guerra.

### Cambiado
- **Rivales más belicosos**. Hasta ahora solo atacaban a un vecino con menos del 60 % de su poder, y casi nunca había guerras. Ahora:
  - Cada rival tiene su carácter: los hay que atacan a vecinos con el 80 % de su poder y los hay que se atreven con uno del 110 %.
  - Se atreven con vecinos más fuertes (un 30 % más) si ya no les queda tierra libre que reclamar, si el vecino ya está en otra guerra, y un 20 % más si el vecino gobierna gente de su cultura.
  - Se lo piensan más a menudo: una vez cada 60 días de media, antes 90.
- **Los ejércitos de los rivales crecen con su nación**: tienen al menos un batallón por cada 30 provincias, no solo 2 por ciudad. Una nación grande ya no se queda con 25 batallones.

## [1.61.1] - 2026-10-01

### Cambiado
- **El humor pasa a llamarse moral** en todo el juego: el panel de la provincia, la barra superior, el modo de mapa, las tablas de la nación, los edificios, los avances y la ayuda. Funciona igual.

## [1.61.0] - 2026-10-01

### Añadido
- **Culturas**: la gente de cada provincia es de la cultura de la nación que la pobló, y la conserva aunque la provincia cambie de manos.
  - Bajo un dueño extranjero pierde hasta 25 de humor, menos a medida que se asimila. Tarda unos 15 años con humor 50: más deprisa con buen humor (hasta el doble) y más despacio con mal humor (hasta 4 veces). Los migrantes de tus otras provincias que se instalan allí la aceleran.
  - Al asimilarse del todo, adopta tu cultura. La tierra vacía toma siempre la de su dueño.
  - **Nuevo modo de mapa, Cultura**: cada provincia, del color de la nación de su gente. Si no coincide con la franja de su frontera, su gente es extranjera.
  - El panel de la provincia muestra su cultura, lo asimilada que está y cuántos años le faltan.
- **Rebeliones**: una provincia descontenta (humor por debajo de 25) sin tropas de su dueño dentro se acerca a la rebelión. Tarda de 30 a 60 días, menos cuanto peor es el humor.
  - Si su gente es de otra nación que aún tiene tierras, **se subleva y se une a ella**. Esto no pasa con la capital.
  - Si no, estalla una **revuelta**: muere el 10 % de la gente y arde uno de sus edificios.
  - **Una guarnición** (un regimiento tuyo en la provincia) la detiene. Con buen humor se calma poco a poco.
  - El panel de la provincia muestra cómo va la rebelión, si tiene guarnición y qué pasará al sublevarse. Llega un aviso cuando está a medio camino.
  - Los rivales mandan tropas a sus provincias que van camino de la rebelión.

### Cambiado
- Al cargar una partida anterior, la gente de cada provincia tiene la cultura de su dueño.

## [1.60.0] - 2026-10-01

### Añadido
- **Tratados de paz: las guerras ya cambian el mapa.** En la pestaña Diplomacia, en guerra, hay tres botones:
  - **Paz blanca**: como hasta ahora, las provincias ocupadas vuelven a sus dueños.
  - **Exigir**: te quedas con las provincias suyas que ocupas, con sus ciudades y edificios. Hace falta bastante puntuación de guerra para pagarlas.
  - **Ceder**: les entregas las provincias tuyas que ocupan, para salir de una guerra perdida. Lo aceptan siempre.
- **Puntuación de guerra** (de -100 a +100), en su propia columna: la parte de la nación enemiga que ocupas, menos la de la tuya que te ocupan, más 2 puntos por cada batalla ganada en tierra o en el mar y menos 2 por cada perdida (hasta 25). Las ciudades, la gente y sobre todo la capital valen más. El tooltip da el desglose.
  - Quedarte con lo ocupado cuesta la parte de su nación que supone. Si ocupas todo, cuesta 100 y la nación queda **anexionada**.
  - La IA acepta tus exigencias si cree que va perdiendo o si tu puntuación pasa de 50.
- **Las provincias cedidas** pierden 20 de humor, y lo que se entrenaba o construía en ellas se pierde. Si se cede la capital, la capital pasa a la ciudad más poblada que le quede a la nación.
- **Los rivales también conquistan entre ellos**: tras 60 días de guerra, un rival exige a otro las provincias que le ocupa si la puntuación lo paga. Un aviso te cuenta quién cede qué.

## [1.59.0] - 2026-10-01

### Cambiado
- **Código separado del motor gráfico** (último paso): los menús (pantalla inicial, nueva partida, cargar partida, pantalla de carga y menú de pausa), la tecla Esc y la ayuda y el historial dentro de la partida se gestionan en Conquer.Presentation. Toda la lógica de la interfaz vive ya ahí; el cliente solo dibuja y pasa el ratón y el teclado, así que cambiar de motor no tocará las reglas ni la presentación. El juego se ve y funciona igual.

## [1.58.0] - 2026-10-01

### Cambiado
- **Código separado del motor gráfico** (séptimo paso): lo que se dibuja sobre el mapa (ciudades, nombres de las naciones, fichas de las unidades con sus rutas y batallas) se calcula en Conquer.Presentation, ya en posiciones de pantalla; el cliente solo lo dibuja. El juego se ve y funciona igual.

## [1.57.0] - 2026-10-01

### Cambiado
- **Código separado del motor gráfico** (sexto paso): la ventana de las batallas, en tierra y en el mar, se construye en Conquer.Presentation (bandos, fuego, unidades y gráfico de organización); el cliente solo la dibuja. El juego se ve y funciona igual.

## [1.56.0] - 2026-10-01

### Cambiado
- **Código separado del motor gráfico** (quinto paso): la ventana para editar unidades y la de construir carreteras y ferrocarriles guardan su estado y construyen su contenido en Conquer.Presentation; el cliente solo las dibuja. El juego se ve y funciona igual.

## [1.55.0] - 2026-10-01

### Cambiado
- **Código separado del motor gráfico** (cuarto paso): la pantalla de la nación (resumen, ciudades, provincias, ciencia, ejército, plantillas y diplomacia) se construye en Conquer.Presentation como tablas, tarjetas y listas de cifras; el cliente solo las dibuja. El juego se ve y funciona igual.

## [1.54.0] - 2026-10-01

### Cambiado
- **Código separado del motor gráfico** (tercer paso): lo que muestran el panel de la provincia (con sus pestañas General, Edificios y Ejército), el de la unidad, la barra superior, los modos de mapa, las pistas de controles, el tooltip del mapa y el diálogo para nombrar ciudades se construye en Conquer.Presentation como documentos (títulos, datos, párrafos, botones, barras); el cliente solo los dibuja. El juego se ve y funciona igual.

## [1.53.0] - 2026-10-01

### Cambiado
- **Código separado del motor gráfico** (segundo paso): el estado de la partida en pantalla (selección, provincia bajo el ratón, pestañas, migración forzada, diálogo para nombrar ciudades, modo de mapa, cámara, reloj y mensajes) y las órdenes del jugador (seleccionar, mover, centrar la vista, guardar) pasan a Conquer.Presentation (GameController). El juego se ve y funciona igual.

## [1.52.0] - 2026-10-01

### Cambiado
- **Código separado del motor gráfico** (primer paso): el nuevo proyecto Conquer.Presentation reúne lo que no depende de Silk.NET ni de OpenGL (cámara, modos de mapa, reloj y velocidades, mensajes, textos de ayuda, historial de versiones, partidas guardadas y formato de cifras). El juego se ve y funciona igual.

## [1.51.0] - 2026-10-01

### Cambiado
- **Símbolo para los colonos**: su ficha muestra un carromato en lugar de la letra «C», y se ve a cualquier zoom.
- Se quitan los picos y los árboles dibujados sobre el mapa de terreno.

## [1.50.0] - 2026-10-01

### Cambiado
- **Bordes suaves**: líneas, fichas, iconos y botones se dibujan con antialiasing (si la tarjeta gráfica lo admite), sin dientes de sierra.
- **Mapa político en pergamino**: los colores tiran al crema del papel viejo, con manchas suaves y los bordes de la pantalla tostados.
- **Fichas OTAN junto a las tropas**: los botones para entrenar batallones, la lista de batallones de una unidad y las plantillas muestran el símbolo de cada tropa.

## [1.49.0] - 2026-10-01

### Cambiado
- **Montañas y bosques dibujados en el mapa**: en el mapa de terreno, al acercarse, aparecen picos (nevados en las cumbres) sobre la roca y árboles en los bosques, como en un mapa antiguo.
- **Ciudades con casas**: cada ciudad es un grupo de casas con el tejado del color de su nación, más casas cuanto más poblada; la capital tiene una torre con bandera.
- Las fichas de las unidades llevan sombra, y el marco de la seleccionada late.

## [1.48.0] - 2026-10-01

### Cambiado
- **Agua según su profundidad**: el mar pasa suavemente del turquesa de los bajíos al azul marino de las fosas, sin los escalones de antes entre mar costero, océano y océano profundo.
- **Espuma en la costa**: con el mapa cerca, las olas rompen en una franja blanca que se mueve a lo largo de la orilla.

## [1.47.0] - 2026-10-01

### Cambiado
- **Fronteras con el color de cada nación**: por dentro de cada frontera hay una franja de su color que se desvanece hacia el interior, como en los mapas de gran estrategia. En el mapa político la franja es más oscura que el relleno.
- En el mapa de terreno el tinte de las naciones es más suave, para que se vea el paisaje; las franjas las distinguen.
- **Provincias ocupadas a rayas**: llevan rayas del color del ocupante sobre el del dueño, en lugar de cambiar de color.
- Con el mapa alejado, las fronteras nacionales también se ven suaves y continuas.

## [1.46.0] - 2026-10-01

### Cambiado
- **Terreno con más detalle**: el relieve se ilumina en la tarjeta gráfica a partir de la altura, nítido a cualquier zoom; las montañas muestran crestas y rugosidad al acercarse.
- Los biomas se funden entre sí en lugar de cortarse en escalera.
- Al acercarse aparece textura según el suelo: un moteado suave en las praderas, copas de árboles en los bosques, dunas en la arena y ondas lentas en el agua. Solo surge cuando es lo bastante grande para no parpadear.

## [1.45.1] - 2026-10-01

### Cambiado
- **Las partidas se guardan en la carpeta del juego**, en `Partidas` junto al ejecutable. Al arrancar, las que había en la carpeta de datos del usuario (`%LOCALAPPDATA%\Conquer\Partidas` en Windows) se mueven allí. Si la carpeta del juego no se puede escribir, se siguen guardando donde antes.

## [1.45.0] - 2026-10-01

### Añadido
- **Taller**: un edificio nuevo para construir las máquinas de guerra (80 de madera y 40 de oro, 40 días; llega con la Maquinaria de asedio). Como el cuartel, se puede levantar en cualquier provincia tuya, tenga ciudad o no.
  - **Solo las provincias con taller construyen catapultas, cañones, artillería de campaña, artillería pesada, tanques y bombarderos.** El cuartel ya no los entrena: se queda con la infantería y la caballería.
  - Una plantilla que mezcla soldados y máquinas pide cuartel y taller en la misma provincia.
  - La pestaña Ejército avisa de lo que le falta a la provincia, y cada tropa dice «Requiere un taller en la provincia».
  - Los avances que aceleran las máquinas (Talleres de asedio, Fundiciones de artillería, Producción bélica) las hacen salir antes del taller; su panel lo muestra.
- **Los talleres pasan a ser fábricas con la Industrialización**: al descubrirla, todos tus talleres (y los que conquistes después) se convierten en fábricas, que siguen construyendo las máquinas de guerra y además dan +50 % de madera y de yacimientos. Desde entonces se construyen fábricas en lugar de talleres, y un taller en obras se termina como fábrica.

### Cambiado
- La fábrica ya no necesita ciudad: se puede construir en cualquier provincia tuya.
- La IA levanta un taller donde tiene cuartel y solo pone máquinas de guerra en su plantilla si puede construirlas.
- Al cargar una partida guardada de una versión anterior, cada provincia con cuartel cuyo dueño conoce la Maquinaria de asedio recibe un taller (o una fábrica, si ya conoce la Industrialización), porque hasta ahora el cuartel construía las máquinas.

## [1.44.1] - 2026-10-01

### Cambiado
- **El cuartel ya no necesita ciudad**: se puede construir en cualquier provincia tuya. Una provincia sin ciudad pero con cuartel entrena tropas igual que una ciudad: tiene su pestaña **Ejército**, y las tropas aparecen allí. Siempre se queda con unos pocos habitantes para no despoblarse.
- La instrucción pertenece ahora a la provincia, no a la ciudad: la columna «En curso» de la pestaña Provincias la muestra también en provincias sin ciudad. Las partidas guardadas conservan lo que estaba en instrucción.

## [1.44.0] - 2026-10-01

### Añadido
- **Cuartel**: un edificio nuevo para las ciudades (60 de madera y 30 de oro, 30 días, sin avance). **Solo las ciudades con cuartel entrenan tropas de combate**, sueltas o con plantillas.
  - Exploradores, ingenieros, colonos y cuarteles generales se siguen formando en cualquier ciudad, y los barcos en los puertos.
  - La pestaña Ejército de una ciudad sin cuartel lo avisa, y cada tropa dice «Requiere un cuartel en la ciudad».
- **Avances militares que mejoran el cuartel**: cada uno hace que el cuartel entrene un 25 % antes solo las tropas que estudia, y se suman.
  - Arcos mejorados: arqueros, carros de arqueros y ballesteros.
  - Cría caballar: jinetes, carros, catafractos y caballeros.
  - Instrucción militar: guerreros, lanceros, infantería de hierro y legionarios.
  - Talleres de asedio: catapultas y cañones.
  - Armerías: infantería de hierro, legionarios, catafractos y caballeros.
  - Piezas intercambiables: arcabuceros, mosqueteros, fusileros y ametralladoras.
  - Servicio militar obligatorio: mosqueteros, fusileros, ametralladoras e infantería motorizada.
  - Fundiciones de artillería: cañones, artillería de campaña y artillería pesada.
  - Producción bélica: infantería motorizada, tanques y bombarderos.
  - Los botones de entrenar muestran los días que tardas tú, y al pasar el ratón cuántos serían sin tus avances. El panel del cuartel lista las tropas que entrena más rápido.

### Cambiado
- La IA construye un cuartel en su capital (y en sus ciudades grandes) y entrena allí su ejército. También investiga los nuevos avances.
- Al cargar una partida guardada de una versión anterior, todas sus ciudades reciben un cuartel, porque hasta ahora todas entrenaban tropas.

## [1.43.0] - 2026-10-01

### Añadido
- **Exploradores que van solos**: las unidades formadas solo por exploradores tienen el botón **«Explorar y reclamar»**. Con él van por su cuenta a la mejor provincia libre junto a tus fronteras (la de mejor tierra para lo lejos que está), la reclaman y siguen con la siguiente.
  - Nunca pasan por tierras de otras naciones, y si hay varias explorando no van a la misma provincia.
  - Avisan de cada provincia que reclaman, y cuando no les queda tierra libre a su alcance dejan de explorar y lo dicen.
  - Su estado dice «Explorando». «Dejar de explorar», «Detener» o una orden de movimiento les devuelven el mando. La opción se guarda con la partida.

### Cambiado
- **Solo los exploradores reclaman territorio**: una unidad solo puede reclamar una provincia libre si lleva al menos un batallón de exploradores. Las unidades mixtas (por ejemplo, guerreros con exploradores) también pueden.
- La IA solo usa exploradores para reclamar tierra. En las partidas guardadas, los guerreros que tenía con esa tarea pasan a su ejército.

## [1.42.0] - 2026-10-01

### Cambiado
- La pestaña **Provincias** de la pantalla de la nación tiene una columna **«En curso»** con lo que hace cada provincia: el edificio o la ciudad en obras y lo que entrena su ciudad. Muestra lo primero con los días que faltan y su barra de progreso, y cuántas tareas más hay; al pasar el ratón aparecen todas.
- El mapa ya no dibuja un punto por cada grupo de migrantes en camino: con muchos, ralentizaban el juego. Los migrantes se siguen viendo en el panel de la provincia y en la columna «En camino».

## [1.41.1] - 2026-09-30

### Cambiado
- Los cuarteles generales vuelven a llevar «HQ» dentro del marco en lugar del asta de la OTAN; encima siguen sus marcas de nivel (XXX, XXXX, XXXXX).

## [1.41.0] - 2026-09-30

### Cambiado
- **Fichas OTAN de las unidades**: muestran el símbolo de su tamaño y de su tipo de tropa.
  - Encima del marco, las marcas de tamaño: III regimiento, X brigada, XX división; XXX cuerpo, XXXX ejército y XXXXX grupo de ejércitos. Antes las unidades de combate no llevaban ninguna.
  - Dentro, el símbolo del arma que forma la mayoría de sus batallones: aspa para la infantería, aspa con raya vertical para la motorizada, aspa con óvalo para la mecanizada (tanques con infantería motorizada), barra diagonal para la caballería, los carros y los exploradores, óvalo para los blindados, punto para la artillería, puente para los ingenieros y alas para la aviación. Antes solo se distinguía la infantería de los montados, y los tanques salían como caballería.
  - Los cuarteles generales llevan el asta de la OTAN bajo el marco en lugar del texto «HQ».

## [1.40.0] - 2026-09-30

### Cambiado
- El panel de la provincia muestra siempre sus **inmigrantes** (los que vienen hacia ella) y sus **emigrantes** (los que se van), en camino ahora mismo, con una explicación de cómo funciona la migración al pasar el ratón. Antes solo aparecían los que llegaban, y solo si había alguno.

## [1.39.0] - 2026-09-30

### Cambiado
- **Nacimientos por provincia**: cada provincia habitada tiene sus propios nacimientos según su fertilidad, no solo las que ya tienen mucha gente.
  - Además del crecimiento de siempre (una parte de sus habitantes), nacen cada día 2 personas por cada 100.000 que pueda alimentar su tierra, multiplicadas por su fertilidad y frenadas a medida que se llena. Así el campo crece por sí mismo y no solo con los migrantes de las ciudades.
  - Con hambre no nace nadie, como antes.
- El panel de la provincia muestra sus **nacimientos al día**; en las pestañas Ciudades y Provincias aparecen al pasar el ratón por la fertilidad, y el Resumen da el total de la nación.

## [1.38.0] - 2026-09-30

### Añadido
- **Mapa diminuto**: un cuarto tamaño para el mapa aleatorio, con un 12 % de tierra en provincias unas 2,5 veces más grandes que las del mapa grande: unas 3.500 provincias de tierra, la sexta parte del grande.

## [1.37.0] - 2026-09-30

### Añadido
- **Tamaño del mapa aleatorio**: al crear una partida con mapa aleatorio se elige Pequeño, Mediano o Grande.
  - Grande es el mapa de siempre: un 30 % de tierra y unas 21.000 provincias de tierra.
  - Mediano tiene menos tierra (24 %) y provincias algo más grandes: unas 14.000, dos tercios del grande.
  - Pequeño tiene poca tierra (18 %) y provincias casi el doble de grandes: unas 7.000, un tercio del grande.
  - La Tierra real tiene un solo tamaño. Las partidas guardadas conservan el suyo, y las anteriores son grandes.

## [1.36.0] - 2026-09-30

### Cambiado
- **Carreteras y ferrocarriles**: ya no son edificios de una provincia, sino líneas que unen ciudades y cuarteles generales.
  - Los construyen los ingenieros, solo desde una provincia con una ciudad o un cuartel general tuyos. Con el botón «Construir carretera…» (o «Construir ferrocarril…») de su panel eliges qué otra ciudad o cuartel unir; el más cercano sale elegido.
  - La ruta es la misma que seguiría un ejército, por tu tierra, la que ocupas o la libre, y aprovecha las carreteras que ya hay. Las ciudades por las que pasa quedan unidas también. La ventana muestra la ruta en el mapa, los tramos nuevos, el coste, el trabajo y las ciudades que une.
  - Se paga por tramo entre dos provincias: la carretera, 20 de madera y 8 de oro; el ferrocarril, 25 de madera, 20 de oro, 15 de hierro y 5 de carbón. Cada tramo lleva 10 días de trabajo (15 el ferrocarril). Cada batallón de ingenieros en la ruta hace un día de trabajo al día, tramo a tramo desde el origen; sin ingenieros la obra se para. Se puede cancelar desde el panel de los ingenieros y se devuelve lo de los tramos sin hacer.
  - Se ven en el mapa como en Hearts of Iron: la carretera como una franja clara, el ferrocarril como una línea oscura con traviesas y las obras como trazos dorados.
  - Por ellas se marcha más deprisa: ×1,5 por carretera y ×2 por ferrocarril.
- **Suministro**: llega a todo lo que tus carreteras y ferrocarriles unen a tus ciudades, y desde ahí hasta 10 días de marcha por tierra propia o libre (antes, 15 días desde las ciudades).
- Los rivales unen sus ciudades con su capital, primero por carretera y después por ferrocarril.
- Las partidas guardadas convierten sus calzadas y ferrocarriles en tramos entre provincias vecinas que tuvieran los dos el mismo; las obras de calzada o ferrocarril a medias se pierden.

### Corregido
- Las duraciones ya no salen como «6 d 24 h».

## [1.35.0] - 2026-09-30

### Añadido
- **Exploradores**: un batallón que se entrena desde el principio. Son 50 hombres, muy baratos (10 de madera y 5 de oro), se instruyen en 7 días y marchan un 50 % más rápido, pero casi no atacan ni defienden. Reclaman tierra libre como cualquier unidad. Los rivales los usan ahora para expandirse.
- **Ingenieros**: un batallón que llega con el avance Ingeniería.
  - En combate van detrás del frente, como la artillería. Si el atacante lleva ingenieros, el río no frena el ataque y la ventaja del terreno del defensor (colinas, bosques, pantanos, montañas) se queda en la mitad. La ventana y el resumen de la batalla muestran la defensa con y sin ellos.
  - Son los únicos que construyen **calzadas** y **ferrocarriles**, con los botones «Construir calzada» y «Construir ferrocarril» de su panel, en tu tierra o en tierra enemiga que ocupas, sin mínimo de habitantes. Cada batallón de ingenieros en la provincia hace un día de trabajo al día; si se van, la obra se para.
  - Los rivales entrenan ingenieros y los llevan a sus ciudades para construir calzadas y ferrocarriles.

### Cambiado
- Las calzadas y los ferrocarriles necesitan ingenieros en la provincia. La pestaña Edificios muestra su obra con cuántos ingenieros trabajan, o si está parada.

## [1.34.0] - 2026-09-30

### Añadido
- **Ventana de la batalla**: haz clic en las espadas rojas de una batalla para verla en detalle. Se actualiza en directo mientras corre el tiempo:
  - El terreno, la defensa que da y cuántos batallones caben en el frente.
  - Una barra con el fuego por hora de cada bando, para ver quién gana el intercambio.
  - Cada bando con sus hombres, sus bajas desde que empezó, su organización (con la marca donde se rompen), su fuego, cuántos batallones combaten en el frente, detrás y en reserva, y sus armas combinadas.
  - Cada unidad con su oficial, sus hombres, cuántos de sus batallones combaten y si está sin suministro; al pasar el ratón, su mando, su general y cada batallón con su puesto, su experiencia y su fuego.
  - Una gráfica de la organización de cada bando hora a hora.
  - Al terminar, dice quién ganó, cuánto duró y las bajas de cada bando. «Ir a la provincia» centra el mapa en la batalla.
  - Las batallas en el mar muestran los barcos, tripulantes, organización y fuego de cada nación.
- Las partidas guardadas conservan las bajas de las batallas en curso; su gráfica empieza de nuevo al cargar.

## [1.33.0] - 2026-09-30

### Añadido
- **Cumbres**: en las partidas nuevas, la tierra por encima de 5.000 m es un bioma propio, «Cumbres», y cada cordillera es una sola provincia: el Tíbet y el Himalaya, los Andes centrales, el Kunlun, el Tian Shan… Como los polos, no se pueden reclamar ni poblar, pero las tropas las cruzan, despacio. La Antártida sigue siendo hielo polar. En los mapas aleatorios casi no hay tierra tan alta.
- Las partidas guardadas conservan su mapa: la partida guarda la versión del generador con que se creó, y las anteriores siguen sin cumbres.

## [1.32.1] - 2026-09-30

### Corregido
- Las partidas guardadas con la 1.30.1 o anteriores vuelven a cargarse. Desde la 1.31.0, el arreglo de las desembocaduras y las confluencias cambiaba un poco el río de algunas provincias, y el juego creía que el mapa era otro. Ahora el río de cada provincia se calcula como antes, y los arreglos solo afectan al dibujo. Las partidas guardadas con la 1.31.0 y la 1.32.0 también cargan.

## [1.32.0] - 2026-09-30

### Añadido
- **Editar unidades**: el botón «Editar unidad» de su panel abre una ventana para renombrarla (o volver a su nombre automático), separar varios batallones o barcos a la vez en una unidad nueva, unirla con otras de la provincia y elegir su oficial. El tiempo se para mientras está abierta.
- **Oficiales**:
  - Tu nación tiene una reserva de oficiales. Se reclutan por 40 de oro y desde ahí se ponen al mando de unidades de combate y cuarteles generales.
  - Su rango va con el tamaño de lo que mandan: Coronel (regimiento), Brigadier (brigada), General de división (división), Teniente general (cuerpo), General (ejército) y Mariscal (grupo de ejércitos). Ascienden solos cuando su unidad crece.
  - Cada uno tiene una o dos virtudes que mejoran con sus estrellas (ofensivo, defensivo, organizador, táctico, marchador, intendente) y a veces un defecto (timorato, temerario, desorganizado, indeciso, lento, corrupto), que afectan a ataque, defensa, recuperación, organización perdida en combate, velocidad o mantenimiento. Un mal oficial es peor que ninguno.
  - El oficial de una unidad le aplica sus rasgos; el general de un cuartel, a las unidades bajo su mando que estén a su alcance. Ganan estrellas con las victorias.
  - Si su unidad es destruida en combate, caen con ella; si la relevas, la unes a otra o la licencias, vuelven a la reserva.
  - Los rivales también reclutan oficiales. En las partidas guardadas, los generales pasan a ser oficiales de su rango.

### Cambiado
- Separar y unir unidades se hace ahora en la ventana de edición, en vez de en el panel de la unidad.
- **Rutas de las unidades como flechas**: la ruta es una flecha verde que se curva por cada provincia del camino, con contorno oscuro, punta en el destino y marcas que avanzan hacia él. La unidad seleccionada la muestra entera; tus demás unidades en marcha, más tenue. Los ataques se marcan con una flecha roja.

## [1.31.0] - 2026-09-30

### Cambiado
- **Mapa del terreno**: las fronteras entre provincias se ven más suaves, para que lo que destaque sea el terreno. Los demás mapas no cambian.
- **Ríos**: se dibujan como un cauce continuo con bordes suaves, orilla más oscura y un reflejo claro en los anchos, y se ensanchan desde el nacimiento hasta la desembocadura y al acercar el zoom. En el mapa del terreno, los grandes ríos tienen a los lados una vega verde, la tierra fértil que riegan.
- Los ríos terminan justo donde empieza el mar o el lago, en vez de adentrarse en el agua.
- Los afluentes se unen al río en el mismo punto donde este sigue, sin dejar un hueco en la confluencia.

## [1.30.1] - 2026-09-30

### Corregido
- Abrir la pestaña Ejército de una ciudad, o seleccionar un cuartel de cuerpo, cerraba el juego con un error de índice fuera de los límites.

## [1.30.0] - 2026-09-29

### Cambiado
- **Estructura militar revisada**:
  - **Adiós a los nombres romanos**: batallones y unidades usan siempre los nombres modernos.
  - **Unidades de combate hasta la división**: son las que se mueven y combaten, y se llaman por su tamaño: Regimiento (1 a 3 batallones), Brigada (4 a 6) y División (7 a 12). Al crecer conservan su número («3.er Regimiento» pasa a «3.ª Brigada»).
  - **Solo tres niveles de cuartel general**: Cuerpo, Ejército y Grupo de ejércitos, con 5 subordinados cada uno. En las partidas guardadas los cuarteles antiguos bajan de nivel, y se deshacen los enlaces de mando que ya no encajan.
- **Mantenimiento**: cada día, batallones, barcos y cuarteles cuestan un 2 % del oro y un 1 % de los demás recursos (salvo la madera) de lo que costaron. Si no hay con qué pagar, pierden organización, desertan y no se recuperan. La nación muestra el mantenimiento del ejército, y los batallones y plantillas el suyo.
- **Generales**: cada cuartel general tiene uno, con un rasgo (ofensivo, defensivo, organizador o táctico) y de 1 a 5 estrellas. Ganan una estrella cada 3 victorias y ayudan a las unidades de su cuartel que estén a su alcance.
- **Experiencia**: los batallones la ganan combatiendo (Novato, Regular, Veterano, Élite), hasta +50 % de fuego; los reclutas nuevos la diluyen.
- **Combate con frente y armas combinadas**:
  - Solo combaten los mejores batallones que caben en el frente: 12 en llano, menos en bosques, colinas y montañas (4 en alta montaña). El resto espera en reserva.
  - La artillería y la aviación disparan desde detrás y reciben menos daño.
  - Cada tipo de tropa distinto entre los que combaten suma +10 % de fuego, hasta +30 %.
  - El resumen de la batalla lo muestra todo.
- **Se empieza con 3.000 colonos y 3.000 de comida**, para poder formar un ejército antes. Los colonos enviados desde una ciudad siguen siendo 300.
- La IA forma unidades de hasta 6 batallones y no entrena tropas mientras pierde oro cada día.

## [1.29.0] - 2026-09-29

### Cambiado
- **Las provincias nacen sin nombre**: hasta que alguien las reclama se muestran por su terreno («Pradera», «Taiga»…). La primera nación que reclama una provincia, o funda una ciudad en ella, le pone nombre, y ese nombre se queda aunque luego cambie de manos. Al reclamar una, el aviso te dice cómo la has llamado.
- En las partidas guardadas con versiones anteriores, las provincias con dueño reciben un nombre nuevo al cargarlas; las que no tienen dueño lo pierden.

## [1.28.0] - 2026-09-29

### Cambiado
- **Tipografía nueva**, en lugar de Noto Sans:
  - **Cinzel**, capitales romanas, para el título del juego y los nombres de las naciones en el mapa.
  - **Fira Sans** para el resto de la interfaz: clara, con cifras regulares que se alinean bien en tablas y barras.
- **Bordes suaves al acercar el mapa**: las fronteras de provincias y naciones y la costa se dibujan como curvas, sin escalones de píxeles. La franja de agua clara de la costa sigue esa curva.
- La línea de controles de la barra inferior se ajusta al ancho de la pantalla: si no cabe, quita atajos y deja siempre «F1: ayuda».

## [1.27.0] - 2026-09-29

### Cambiado
- **Interfaz renovada**:
  - **Paneles**: sombra, degradado, brillo arriba y esquinas redondeadas.
  - **Botones**: relieve, brillo al pasar el ratón, dorado cuando están activos, se hunden al pulsarlos y su texto lleva sombra.
  - **Tooltips, mensajes y tarjetas de ciencia**: redondeados, a juego con lo demás.
- **Iconos de recursos** dibujados en la barra superior, el panel de provincia y el filtro de recursos del mapa: espiga, tronco, lingotes, monedas, cristal, gota y neumático.
- **Mapa**:
  - **Relieve**: más marcado, y las montañas más claras.
  - **Costas**: una franja de agua clara las resalta.
  - **Fronteras nacionales**: más gruesas y con un sombreado hacia dentro.
  - **Viñeta**: bordes de la pantalla suavemente oscurecidos.
- **Nombres de las naciones** sobre su territorio, del tamaño que ocupan en pantalla.
- **Menú principal**: se ve sobre un mapa de la Tierra con relieve que se desplaza despacio, con el título con sombra y los botones en un panel. El mismo fondo aparece en las pantallas de nueva partida, cargar partida y carga.

## [1.26.0] - 2026-09-29

### Añadido
- **Ayuda durante la partida**: se abre con **F1**, con el botón **«?»** de la barra superior o desde el menú de pausa. Tiene 11 temas: controles, primeros pasos, población y humor, economía y recursos, ciudades, ciencia, ejército, flotas y mar, diplomacia, mapa y partida. Mientras está abierta, el tiempo se para.

### Corregido
- En las tarjetas de ciencia, el descuento por vecinos se veía como «?10 %» en lugar de «-10 %».

## [1.25.0] - 2026-09-29

### Añadido
- **Puerto**: edificio para ciudades con costa, que se desbloquea con Navegación a vela. Sin él, la ciudad no construye barcos y las flotas no atracan ni se reparan allí. Además da +15 % de impuestos por el comercio marítimo.
- **Ingeniería naval**: nuevo avance de la era Moderna (Economía, nivel 13). Desbloquea el **dique seco** y dos barcos avanzados:
  - **Destructor**: rápido y fuerte.
  - **Portaaviones**: el barco de guerra más fuerte; también pide Aviación.
- **Dique seco**: se construye en una ciudad que ya tenga puerto. Solo allí se construyen el destructor y el portaaviones, y las flotas se reparan el doble de rápido.

### Cambiado
- **Puertos**: ahora son las ciudades con el edificio Puerto, no cualquier ciudad con costa. En las partidas guardadas, las flotas que estén en una ciudad sin puerto pueden salir al mar, pero no volver hasta que se construya uno.
- Los rivales construyen puertos y diques secos e investigan Ingeniería naval.

## [1.24.0] - 2026-09-29

### Añadido
- **Flotas**: los barcos se construyen sueltos en ciudades con costa, y cada uno forma una flota nueva. Las flotas navegan por el mar costero con Navegación a vela y por el océano con Cartografía, y atracan en tus puertos. Se unen y se separan como los regimientos, hasta 10 barcos.
- **Barcos**:
  - **Trirreme**: de combate, con Navegación a vela.
  - **Barco de transporte**: lleva 600 hombres, con Navegación a vela.
  - **Galeón**: de combate, y lleva 200 hombres, con Cartografía.
  - **Vapor de transporte**: lleva 1.500 hombres, con Máquina de vapor.
  - **Acorazado**: de combate, con Acero.
- **Transporte de tropas**:
  - **Embarcar**: regimientos, cuarteles y colonos suben a una flota con transportes que esté en su provincia o en el mar de al lado. Se hace con el botón «Embarcar» o con clic derecho sobre la flota.
  - **En el barco**: viajan con ella sin desgaste.
  - **Desembarcar**: clic derecho en la costa junto a la flota. En tierra enemiga sin tropas, desembarcar la ocupa; una costa con tropas enemigas no se puede tomar desde el mar.
- **Batallas navales**: las flotas de naciones en guerra que coinciden en un mar combaten hora a hora. La que se rompe huye a otro mar, o se hunde con todo lo que lleva. Aparecen en el mapa como las batallas en tierra.
- **Reparaciones**: las flotas recuperan organización y tripulación en sus puertos.
- **Los rivales** mantienen una flota de guerra por cada tres puertos.
- La pestaña Ejército de la nación también lista las flotas.

### Cambiado
- **Las tropas ya no cruzan el mar solas**: hace falta embarcarlas. Solo los regimientos de aviones siguen volando sobre el mar.

## [1.23.0] - 2026-09-29

### Añadido
- **Navegación**: con **Navegación a vela** (nuevo avance de Economía, nivel 5) tus unidades cruzan mares costeros y lagos en barco. Con **Cartografía**, también el océano. En el mar van el doble de rápido que a pie, pero sin suministro.
- **Aviación** (nuevo avance Militar, nivel 13): **bombarderos**, muy rápidos y con mucho ataque, que gastan aluminio y petróleo. Un regimiento que solo tenga aviones puede cruzar cualquier mar sin barcos.

### Cambiado
- Los migrantes y el suministro siguen yendo solo por tierra.

## [1.22.0] - 2026-09-29

### Añadido
- **La era Moderna**: niveles 12 y 13 de cada rama, que cuestan 8.500 y 12.000 puntos.
  - **Economía**: Refinado del petróleo (descubre el **petróleo**) y Fertilizantes (+30 % de comida, +20 % de capacidad); después, Producción en cadena (+25 % de madera y de yacimientos).
  - **Sociedad**: Electricidad (descubre el **aluminio**, +20 % de ciencia) y Antibióticos (+20 % de fertilidad; el hambre mata a la mitad); después, Electrónica (descubre el **silicio**, +25 % de ciencia).
  - **Militar**: Motor de combustión (infantería motorizada) y Artillería pesada; después, Blindados (tanques).
- **Ya se pueden descubrir todos los recursos del mapa.**
- **La Electrificación**, institución de la era Moderna.
  - **Dónde nace**: en la capital de la primera nación que descubre la Electricidad.
  - **Qué da al adoptarla**: +15 % de ciencia y +10 % de impuestos.
- **Nuevo edificio, la Central eléctrica**: +25 % de ciencia y de impuestos en una ciudad.
- **Nuevos batallones**, que gastan petróleo y caucho:
  - **Infantería motorizada**: más del doble de rápida.
  - **Artillería pesada**.
  - **Tanques**.

## [1.21.0] - 2026-09-29

### Añadido
- **La era Industrial**: niveles 10 y 11 de cada rama, que cuestan 4.500 y 6.200 puntos.
  - **Economía**: Máquina de vapor (+25 % de yacimientos) e Industrialización (fábricas); después, Ferrocarril.
  - **Sociedad**: Química (descubre el **caucho**) y Salubridad (hospitales); después, Educación pública (+20 % de ciencia).
  - **Militar**: Estriado (fusileros) y Acero (artillería de campaña); después, Ametralladoras.
- **La Industrialización**, institución de la era Industrial.
  - **Dónde nace**: en la primera provincia con fábrica y yacimiento de carbón.
  - **Qué da al adoptarla**: +15 % de madera y de yacimientos.
- **Nuevos edificios**:
  - **Fábrica**: +50 % de madera y de yacimientos en la provincia.
  - **Hospital**: +20 % de fertilidad y +10 % de capacidad.
  - **Ferrocarril**: la provincia se cruza el doble de deprisa; se construye en cualquier provincia habitada y se suma a la calzada.
- **Nuevos batallones**, que gastan hierro y carbón:
  - **Fusileros**.
  - **Artillería de campaña**: mucho ataque y lenta.
  - **Ametralladoras**: mucha defensa.

## [1.20.0] - 2026-09-29

### Añadido
- **El Renacimiento**: niveles 8 y 9 de cada rama, que cuestan 2.300 y 3.200 puntos.
  - **Economía**: Economía (+20 % de impuestos) y Minería profunda (+30 % de yacimientos); después, Cartografía (+10 % de impuestos).
  - **Sociedad**: Imprenta (+25 % de ciencia) y Anatomía (+15 % de fertilidad); después, Método científico (+20 % de ciencia).
  - **Militar**: Pólvora (arcabuceros) y Metalurgia (cañones); después, Ciencia militar (mosqueteros).
- **El Humanismo**, institución del Renacimiento.
  - **Dónde nace**: en la ciudad más poblada que tenga universidad.
  - **Qué da al adoptarlo**: +10 % de ciencia y +3 de humor.
- **Ciencia militar moderniza el ejército**: las legiones pasan a llamarse regimientos, y las vexilaciones, brigadas.
- **Nuevos batallones**, que gastan hierro y carbón:
  - **Arcabuceros**.
  - **Cañones**: mucho ataque, poca defensa y lentos.
  - **Mosqueteros**.

## [1.19.0] - 2026-09-29

### Añadido
- **La era Medieval**: niveles 6 y 7 de cada rama, que cuestan 1.100 y 1.600 puntos.
  - **Economía**: Rotación de cultivos (+20 % de comida y +10 % de capacidad) y Gremios (+20 % de madera y de yacimientos); después, Banca (bancos).
  - **Sociedad**: Teología (+5 de humor) y Educación (universidades); después, Astronomía (+20 % de ciencia).
  - **Militar**: Estribo (caballeros) y Maquinaria (ballesteros); después, Castillos.
- **El Feudalismo**, institución de la era Medieval.
  - **Dónde nace**: en la capital de la primera nación que llega a 8 ciudades.
  - **Qué da al adoptarlo**: +10 % de impuestos y +3 de humor.
  - **Qué pasa si no lo adoptas**: los avances medievales te cuestan un 50 % más.
- **Nuevos edificios de ciudad**:
  - **Universidad**: +50 % de ciencia.
  - **Banco**: +50 % de impuestos.
  - **Castillo**: los defensores hacen el doble de daño; se suma a la muralla.
- **Nuevos batallones**: **Caballeros**, caballería pesada, y **Ballesteros**.

### Cambiado
- La pestaña Ciencia pone los botones de era en una segunda fila, marca con «(+)» las eras que te cuestan más y muestra al lado de los puntos solo la institución de la era que estás viendo.

## [1.18.0] - 2026-09-29

### Añadido
- **Murallas**: quien defiende una provincia amurallada hace un 50 % más de daño. Se construyen en ciudades y piden el nuevo avance **Fortificaciones** (Militar, nivel 5).
- **Calzadas**: una provincia con calzada se cruza un 50 % más deprisa, y las rutas las aprovechan. Se construyen en cualquier provincia habitada y piden Ingeniería.
- **Administración** (Sociedad, nivel 5): la lejanía de la capital resta la mitad de humor.

### Cambiado
- **Construcción** acelera las obras un 25 %, incluidas las ciudades. Sigue permitiendo el anfiteatro.
- Los rivales construyen calzadas en sus ciudades y murallas en las de más de 2.000 habitantes.

## [1.17.0] - 2026-09-29

### Añadido
- **La era Clásica**: dos niveles más en cada rama de la ciencia, que cuestan 500 y 750 puntos. Mientras no adoptes el Urbanismo, te cuestan un 50 % más.
  - **Economía**: Comercio (+15 % de impuestos) y Construcción (anfiteatros); después, Ingeniería (la tierra alimenta un 15 % más de gente).
  - **Sociedad**: Filosofía (+20 % de ciencia y +3 de humor) y Matemáticas (+15 % de ciencia); después, Drama y poesía (+5 de humor).
  - **Militar**: Tácticas militares (legionarios) y Maquinaria de asedio (catapultas); después, Caballería pesada (catafractos).
- **Nuevo edificio, el Anfiteatro**: +10 de humor en una ciudad.
- **Nuevos batallones**:
  - **Legionarios**: infantería pesada, necesita hierro.
  - **Catapultas**: mucho ataque, casi nada de defensa y lentas.
  - **Catafractos**: caballería con armadura, necesita hierro.
- La pestaña Ciencia muestra una era cada vez, con un botón por era. Por defecto aparece la primera en la que te quedan avances por descubrir.

### Cambiado
- Los rivales investigan y usan los avances, edificios y batallones nuevos.

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
