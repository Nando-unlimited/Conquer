# Reglas de Conquer

Cómo funciona el juego por dentro: fórmulas y cifras. Este archivo se genera a partir de `RulesManual` (Conquer.Presentation), lo mismo que la pantalla Reglas del juego; no lo edites a mano.

## El tiempo

El juego avanza hora a hora. Cada hora se mueven las unidades, los emigrantes y los envíos, y se libra una hora de cada batalla. A medianoche se hace la cuenta del día, en este orden: economía (producción, comida, mantenimiento, moral y nacimientos), rebeliones, religión, migración, ciencia, instituciones, obras, talleres, ejército (reclutas, instrucción, suministro y recuperación), aviación, asedios, diplomacia, comercio, epidemias, decisiones y victoria.

Salvo que se diga otra cosa, las cifras de este manual son por día.

### Distancias y velocidad

- La gente camina 10 km/h por terreno llano; las unidades y los emigrantes también. El terreno difícil, los ríos, la nieve y el barro la frenan; las carreteras y los ferrocarriles la aceleran.
- Los barcos van 2 veces más deprisa por el mar.
- Los cuarteles generales van a caballo: 1,5 veces más deprisa que la infantería.

## Población

### Capacidad

Cada provincia alimenta a un número de habitantes según su tierra (bioma, ríos y superficie): su capacidad. Se multiplica por:

- Ciudad: ×2,5 hasta el Renacimiento; la industria hace ciudades mucho mayores: ×4 en la era Industrial y ×6 en la Moderna.
- Era de su dueño: ×1 en la Antigua, ×1,5 en la Clásica, ×2 en la Medieval, ×3 en el Renacimiento, ×5 en la Industrial y ×8 en la Moderna.
- Avances y edificios que la aumentan.

### Nacimientos

Cada día nacen en una provincia habitada:

- (habitantes × 0,012 % + 0,02) × fertilidad × (1 - habitantes / capacidad).
- En las ciudades la primera parte es ×2. Sin hambre y con fertilidad normal, una provincia casi vacía crece en torno a un 4,4 % al año y una a medio llenar, un 2,2 %; una ciudad, el doble. Cuanto más se acerca a su capacidad, más despacio.
- Transición demográfica: hasta el Renacimiento nacen y mueren muchos, y el ritmo es el de arriba; en la era Industrial la medicina y la higiene lo multiplican por 2, y en la Moderna las familias tienen menos hijos: ×0,5.
- Por encima de su capacidad no nace nadie, y con hambre nacen menos (ver Hambre).
- La pequeña base fija hace que unos pocos colonos lleguen a formar un pueblo, pero no llena la tierra vacía por sí sola: eso lo hace la migración.

### Fertilidad

- Multiplica los nacimientos (1 es lo normal). Tiende a 0,5 + moral / 100 (de 0,5 con moral 0 a 1,5 con moral 100), más lo que den los avances y edificios; sin nada que comer, al 20 % de eso, y en proporción con el hambre.
- Cada día recorre un 3 % de la distancia a ese objetivo: va más despacio que la moral.

### Comida

- Cada trabajador produce 0,13 de comida × el rendimiento de su tierra (más en las llanuras de los grandes ríos: ×1,25). Trabajan los habitantes hasta la capacidad; los que sobran producen solo el 30 %.
- Cada persona come 0,1 al día: los habitantes, los soldados, los emigrantes en camino y las tripulaciones de los aviones.
- Lo almacenado se estropea: cada día se pierde el 1 %. Por eso las reservas se estabilizan en unos 100 días de excedente.
- La producción se multiplica por la productividad de la moral (ver Moral) y por las bonificaciones de comida.

### Hambre

- Si el almacén de comida no llega para lo que se come en el día, la nación pasa hambre en la proporción de lo que falta: si falta el 10 % de la comida, el hambre es del 10 %.
- Cada día muere el 1 % de la gente de cada provincia × el hambre (menos con los avances y los graneros que la protegen); los nacimientos bajan en la misma proporción, la moral recibe -40 × el hambre y la fertilidad tiende a bajar hacia el 20 %. Así la escasez avisa antes de volverse una catástrofe.
- Las provincias ocupadas por el enemigo ni producen ni comen para su dueño.

## Moral

Cada provincia tiene una moral de 0 a 100. Cada día recorre un 10 % de la distancia hasta su moral objetivo: la suma de estos factores, entre 0 y 100.

### Factores

- Base: +50.
- Ciudad: +10. Capital: +15.
- Distancia a la capital: -1 por cada 50 km, hasta -20 (la Administración y otros avances lo reducen).
- Reservas de comida: hasta +10 cuando lo almacenado da para 30 días (proporcional por debajo).
- Fiestas: +20 durante 30 días; cuestan 2 de oro por cada 100 habitantes (mínimo 10).
- Hacinamiento: por encima de la capacidad, hasta -20 cuando hay el doble de gente de la que alimenta la tierra.
- Hambre: -40 × la parte de la comida que falta. Ocupación enemiga: -30. Epidemia: -15.
- Cultura extranjera: hasta -25, menos a medida que se asimila. Otra fe: -10 hasta convertirse.
- Avances, edificios (templos, anfiteatros...) y decisiones: lo que diga cada uno.

### Efectos

- Productividad: los habitantes trabajan × (0,75 + moral / 200): ×0,75 con moral 0, ×1 con 50 y ×1,25 con 100. Afecta a la comida, la madera, los impuestos, los yacimientos y la ciencia.
- Por debajo de 25 la provincia está descontenta: no paga impuestos y, sin guarnición, se acerca a la rebelión (ver Cultura, religión y rebelión).
- La moral alta acelera la fertilidad, la asimilación y la conversión.
- Niveles: Descontento (menos de 25), Inquieto (menos de 45), Tranquilo (menos de 65) y Contento.

## Migración

Toda migración ocurre dentro de una nación: nadie emigra a otra.

### Migración espontánea

- Quién emigra: solo las ciudades no ocupadas con más de 200 habitantes. Las provincias sin ciudad nunca envían gente.
- Cuántos: depende de lo llena que esté la ciudad (habitantes / capacidad). Hasta el 20 % no se va nadie; desde ahí, cada vez más, hasta el 0,2 % de su gente al día cuando está llena. La gente se va por falta de sitio: una ciudad baja hasta donde sus nacimientos compensan a los que se van, en torno a un tercio de su capacidad, y ahí se queda en vez de vaciarse.
- Salen en grupos de 25. Lo que no llega a un grupo se acumula para el día siguiente. Una ciudad nunca baja de 200 por emigrar.
- Adónde: a las provincias de la nación sin ciudad, sin ocupar y a menos de 60 días de camino. Cada una toma su gente de la ciudad más cercana que esté enviando gente: una ciudad nueva, que aún no emigra, no deja sin emigrantes a las provincias de su alrededor.
- Cada provincia quiere llenarse hasta el 50 % de su capacidad, o hasta lo llena que esté la ciudad que le manda gente si está más llena. Su déficit es eso menos sus habitantes y los que ya van hacia ella; sin déficit, no atrae a nadie.
- Cada grupo va a la provincia con más atracción: déficit / lo que quiere llenar / (1 + días de viaje / 10). La distancia pesa poco (a 10 días se atrae la mitad), así que los emigrantes se reparten por toda la tierra en vez de llenar primero la más cercana.
- Las que tienen menos de 10 habitantes (contando los que van en camino) atraen 10 veces más: la tierra nueva recibe primero a sus primeros pobladores.
- Cuando un grupo sale, la atracción de su destino baja en proporción a lo que ha cubierto.
- Nunca hay migración espontánea de una ciudad a otra: eso solo lo consigue la migración forzada.

### Cómo evoluciona

- Al principio la capital, la única ciudad llena, da a cada provincia reclamada sus primeros pobladores y luego reparte su crecimiento entre todas.
- Las ciudades nuevas se suman cuando llegan al 20 % de su capacidad.
- Cuando el campo pasa del 50 %, la migración se para mientras las ciudades estén menos llenas que él, y vuelve cuando lo adelantan. Solo se detiene del todo cuando ciudades y campo están igual de llenos, o al llegar la era Industrial, en la que se invierte (ver Urbanización).

### Urbanización

- Desde la era Industrial la migración cambia de sentido: el campo se vacía hacia las ciudades, que la industria ha hecho mucho mayores. Las ciudades ya no mandan gente al campo.
- Cada provincia sin ciudad con más de 200 habitantes envía a su ciudad más cercana hasta el 0,05 % de su gente al día cuando está llena; nada por debajo del 20 %, y en proporción entre medias.
- Una ciudad los acoge mientras no esté 30 puntos más llena que la provincia que los manda, ni pase del 95 %. Así el campo se vacía poco a poco y las ciudades acaban con la mayor parte de la gente: en el banco de pruebas, en torno al 70 % al llegar la era Moderna.

### Migración forzada

- Mueve la gente que elijas entre dos provincias tuyas no ocupadas, con o sin ciudad, si hay camino por tierra. Cuesta 1 de oro por cada 10 personas.
- Una ciudad de origen conserva al menos 100 habitantes.
- Los emigrantes llegan con 20 puntos menos de moral que la provincia de la que salieron.

### Viaje y llegada

- Caminan a 10 km/h por el camino más rápido y comen por el camino.
- Al llegar, su moral se mezcla con la de los residentes, ponderada por el número de cada uno.
- Llegan con la cultura y la fe de su nación: en una provincia de otra cultura o fe cuentan como gente asimilada y convertida, así que migrar acelera la asimilación.
- Si el destino se ha perdido por el camino, se instalan en la capital.
- El modo de mapa Población muestra con flechas a los emigrantes en camino.

## Producción

Todo lo que producen las provincias va al almacén de la nación, en la capital.

### Lo que da cada provincia

- Comida: ver Población.
- Madera: según el bioma, por cada habitante que trabaja la tierra (hasta su capacidad).
- Oro: 0,002 de impuestos por habitante al día. Las provincias descontentas no pagan.
- Yacimientos: cada uno da su rendimiento × habitantes / 1.000 (al máximo con 1.000 habitantes). Solo se explotan los recursos que la nación conoce.
- Todo se multiplica por la productividad de la moral y por las bonificaciones de los avances y edificios de la provincia (granjas, aserraderos, minas, mercados, puertos...).

### Yacimientos

- Son bolsas finitas: se agotan. Los caballos salen de pastos, que se renuevan. Una provincia tiene como mucho 4 yacimientos.
- Al agotarse uno, la provincia deja de dar ese recurso.

### Gastos

- Comida: lo que come la gente y lo que se estropea.
- Mantenimiento militar: cada batallón, barco o cuartel general cuesta al día el 2 % de su oro y el 1 % de sus demás materiales (la madera no). Ver Ejército.
- Los talleres y las fábricas gastan los materiales del equipo que fabrican (salvo el oro).

### Dificultad

Los rivales del ordenador multiplican su producción y su ciencia según la dificultad: ×0,75 en Muy fácil, ×0,9 en Fácil, ×1 en Normal, ×1,2 en Difícil y ×1,5 en Muy difícil. La dificultad también cambia cuántos yacimientos hay, su tamaño y los recursos con que empiezas.

## Ciudades y obras

### Fundar ciudades

- Con colonos, en una provincia libre o tuya. Una ciudad envía colonos con 300 de sus habitantes y 150 Comida, 50 Madera, 20 Oro.
- Sin colonos: en una provincia tuya con al menos 500 habitantes, por 150 Madera, 50 Oro y 60 días de obras.
- Nunca en una provincia vecina de otra ciudad (construida o en obras).
- Empiezas con 5.000 colonos, 5.000 de comida, 100 de madera, 50 de oro y 50 de piedra (según la dificultad).

### Qué cambia una ciudad

- Capacidad ×2,5 (×4 en la era Industrial y ×6 en la Moderna) y crecimiento ×2.
- Moral +10 (+15 más la capital).
- Produce ciencia, envía emigrantes y colonos, y permite los mejores edificios.
- Deja de recibir emigrantes: desde entonces crece por sus nacimientos, hasta la era Industrial, en la que es el campo el que se muda a ella.

### Edificios

- Cada edificio pide un avance y cuesta materiales y días de obra (menos con los avances que aceleran la construcción). Hace falta un mínimo de 10 habitantes y no puede haber otra obra en marcha en la provincia.
- Granjas, graneros, aserraderos, minas, cuarteles, talleres y fábricas van en cualquier provincia habitada; el resto, solo en ciudades.
- Las obras se paran mientras la provincia está ocupada.

## Ciencia

### Producción

- Cada ciudad no ocupada produce (0,25 + habitantes × 0,001) puntos al día × la productividad de su moral × (1 + bonificaciones de ciencia de la nación y de sus edificios).
- Las provincias sin ciudad no producen ciencia.

### Reparto

- La ciencia del día se reparte entre las ramas que investigan algo, en proporción a su prioridad (de 0 a 10).
- Una rama no toma más de lo que le falta a su avance: el resto pasa a las demás. Lo que nadie puede tomar se guarda y entra en el siguiente avance que elijas.

### Coste de un avance

- Sus puntos × 3.
- -10 % por cada nación vecina (con frontera) que ya lo conoce, hasta 3 (-30 %).
- +50 % mientras no hayas adoptado la institución de su era.

### Niveles y eras

- Cada rama tiene niveles. El siguiente se abre al conocer la mitad (redondeando hacia arriba, al menos uno) de los avances del nivel anterior. Algunos avances piden además otros concretos.
- La era de tu nación es la de su rama más atrasada: la más moderna en la que conoces avances de las tres ramas. Adelantar mucho una sola rama no te lleva a una era nueva. La era cambia la capacidad de la tierra, los nombres de tus tropas y oficiales, los retratos y la música.

## Instituciones

Cada era desde la Clásica abre con una institución que nace en algún lugar del mundo y se extiende de provincia en provincia.

### Dónde nacen

- Urbanismo: en la primera ciudad que llega a 5.000 habitantes (la mayor).
- Feudalismo: en la capital de la primera nación con 8 ciudades.
- Humanismo: en la mayor ciudad con universidad.
- Industrialización: en la mayor provincia con fábrica y yacimiento de carbón.
- Electrificación: en la capital de la primera nación que descubre la Electricidad.

### Cómo se extienden

- Cada día, cada provincia con al menos 10 habitantes que no la tiene puede tomarla de cada vecina que la tiene, con una probabilidad del 1 % por vecina (×3 si la provincia tiene ciudad). Avanza como mucho una provincia al día.

### Adopción

- Tu nación la adopta sola cuando la tiene el 50 % de su gente.
- Antes, pagando 0,1 de oro por cada habitante que aún no la tiene (mínimo 50).
- Hasta adoptarla, los avances de su era cuestan un 50 % más. Adoptada, da su bonificación.

## Cultura, religión y rebelión

### Cultura

- La gente de una provincia es de la cultura de la nación que la pobló. Una provincia vacía toma la de su dueño.
- Bajo un dueño de otra cultura pierde -25 × (1 - asimilación) de moral.
- La asimilación avanza cada día clamp(moral / 50, 0,25, 2) / (15 años): 15 años con moral 50, 7,5 con moral 100 y hasta 60 con moral muy baja. Al completarse, la provincia adopta tu cultura. Los emigrantes de tu cultura la aceleran.
- Las provincias ocupadas no se asimilan.

### Religión

- Igual que la cultura: bajo un dueño de otra fe pierde -10 de moral hasta convertirse.
- La conversión avanza cada día clamp(moral / 50, 0,25, 2) × (1 + 1 con templo + 0,5 con Teología) / (25 años).
- Las naciones de la misma fe se ven mejor (+10 de opinión) y las de otra, peor (-10).

### Rebelión

- Una provincia descontenta (moral menor que 25) sin ningún regimiento suyo dentro suma cada día 1 + (25 - moral) / 25 de rebelión. Al llegar a 60 se rebela: entre 30 y 60 días, antes cuanto peor la moral.
- Con moral de 25 o más se calma 2 puntos al día. Una guarnición la para del todo.
- Si su gente es de otra nación que aún tiene tierras (y no es tu capital), se une a ella con moral de al menos 60.
- Si no, hay una revuelta: muere el 10 % de su gente, arde un edificio al azar y la moral queda en al menos 35.

## Epidemias

- Cada día, cada ciudad puede enfermar con una probabilidad de 1 entre 14.600 × (1 + habitantes / 20.000) × (1 - resistencia): una ciudad pequeña, una vez cada 40 años de media; una de 20.000, cada 20.
- Dura 90 días. Cada día mata al 0,1 % de la gente × (1 - resistencia) y quita 15 de moral.
- Contagio diario: a cada provincia vecina con un 0,5 %, por carretera o ferrocarril con un 3 %, y de un puerto a cada puerto a menos de 2.000 km con un 0,5 % (todo × (1 - resistencia)).
- La resistencia viene de la Medicina, la Salubridad, los Antibióticos, los herbolarios y los hospitales, hasta un 90 %.
- Al pasar, la provincia queda inmune 10 años.

## Ejército

### Reclutas

- La reserva de reclutas llega hasta 500 + el 6 % de la población asentada (más con Instrucción militar y Servicio militar obligatorio).
- Se rellena a razón de su máximo / 2 años: de vacía a llena en 2 años.
- Entrenar, formar cuarteles generales y reforzar la gastan; licenciar una unidad devuelve sus hombres.

### Instrucción

- Cada batallón tiene sus días de instrucción, su coste en oro y su equipo. Sin el equipo en el almacén no se entrena.
- Cada avance que estudia una tropa la entrena un 25 % más deprisa, y se suman.
- Un taller fabrica en los días de instrucción de un batallón el equipo de uno; una fábrica, ×2.

### Organización

- Regimiento: hasta 5 batallones. Brigada: hasta 4 regimientos. División: hasta 5 brigadas y regimientos, y 10.000 hombres.
- Cuerpo: 600 km de alcance. Ejército: 1.200 km. Grupo de ejércitos: 2.500 km. Cada uno manda hasta 5 unidades del nivel de abajo.

### Mantenimiento

- Cada día cada batallón, barco o cuartel cuesta el 2 % de su oro y el 1 % de sus demás materiales (la madera no). Los oficiales intendentes y corruptos lo cambian.
- Si no hay con qué pagar, cada día cada batallón pierde el 10 % de su organización y deserta el 2 % de sus hombres, y nadie se recupera. Lo que se debía se perdona.

### Oficiales

- Cuestan 40 de oro. Tienen de 1 a 5 estrellas y ganan una cada 3 victorias.
- Virtudes, por estrella: ofensivo +10 % de ataque, defensivo +10 % de defensa, organizador +25 % de recuperación, táctico -10 % de organización perdida (hasta -50 %), marchador +5 % de velocidad, intendente -5 % de mantenimiento.
- Defectos, fijos: timorato -15 % de ataque, temerario -15 % de defensa, desorganizado -25 % de recuperación, indeciso +20 % de organización perdida, lento -15 % de velocidad, corrupto +20 % de mantenimiento.
- Se aplican los rasgos del oficial de la unidad y los del general de su cuartel si está a su alcance.

## Combate

Una batalla se libra hora a hora hasta que un bando se queda sin unidades en ella.

### Quién combate

- Cada bando pone en el frente sus mejores batallones de primera línea, tantos como caben: 12 en llano, 9 en colinas, 8 en bosques, selvas y pantanos, 6 en montañas y 4 en alta montaña.
- Detrás, hasta la mitad de esa cifra de artillería, aviación e ingenieros. El resto espera en reserva. Los médicos no combaten.

### Fuego de cada batallón

Ataque (si ataca) o defensa (si defiende) del modelo

- × hombres / plantilla × (0,5 + 0,5 × organización / máxima)
- × (1 + 50 % × experiencia)
- × (1 + bonificación de mando + rasgos del oficial y del general)
- × 0,75 sin suministro, × 0,5 sin munición, × el efecto del cielo (hasta ±15 %)
- y, solo los defensores, × el terreno × las murallas o el castillo × el atrincheramiento.
- La caballería y los carros atacan al 50 % en terreno difícil; las tropas de montaña disparan ×1,5 en él.

### Fuego de cada bando

- La suma de sus batallones × (1 + armas combinadas) × un azar de ±20 %, más el apoyo aéreo cercano.
- Armas combinadas: +10 % por cada tipo de tropa distinto combatiendo después del primero (infantería, caballería, artillería, blindados, aviación, ingenieros), hasta +30 %.
- Antiaérea: con aviones enemigos en la batalla dispara ×2, y cada batallón suyo quita un 25 % al fuego de esos aviones (hasta 75 %).

### Terreno

- Los defensores disparan ×1,25 en colinas, ×1,5 en montañas y ×1,2 en bosques, selvas y pantanos; ×1,25 más con río.
- Si el atacante lleva ingenieros, el río no cuenta y la ventaja del terreno se queda en el 50 %.
- Atrincherarse da hasta +25 % de defensa en 5 días.

### Daño

- El fuego enemigo se reparte entre los batallones que combaten; los de detrás reciben el 25 % de lo que recibe uno del frente.
- Cada punto de fuego quita 0,3 de organización y 0,1 hombres. Los médicos salvan el 30 % de las bajas de su unidad; los oficiales tácticos o indecisos cambian la organización perdida.
- Cada hora de combate da 0,3 % de experiencia, menos cuanto más tiene.

### Final

- Una unidad por debajo del 10 % de organización, o sin hombres, se rompe. El defensor roto se retira a una provincia vecina suya o libre sin enemigos; si no hay ninguna, queda rodeado y destruido. El atacante roto abandona el ataque.
- Cuando no queda ningún defensor, los atacantes entran en la provincia. Los oficiales de los vencedores suman una victoria.

### Cadena de mando

- Una unidad con su cuartel a su alcance recibe +10 % de fuego y de recuperación, y +5 % por cada cuartel superior de la cadena que también esté a su alcance.

## Suministro y desgaste

### Suministro

- Sale de tus ciudades por tus carreteras y ferrocarriles, y desde ahí hasta 10 días de marcha por tierra tuya, ocupada o libre, más una provincia (el frente).
- Los sitiadores lo reciben también de cualquier provincia abastecida vecina de la que sitian.
- Sin suministro, una unidad dispara al 75 % y pierde cada día el 5 % de su organización y el 1 % de sus hombres.

### Recuperación

- Con suministro y fuera de combate recupera cada día el 20 % de su organización × (1 + mando + rasgos); la mitad si está en marcha, ×1,5 atrincherada.
- Los refuerzos llegan desde la capital en envíos: hasta el 5 % de la plantilla de cada batallón al día, con su equipo, mientras haya reclutas. Los reclutas nuevos diluyen la experiencia.

### Envíos y munición

- Los envíos tardan lo que se tarda desde la capital; ×2 y los últimos para las unidades sin cuartel a su alcance, y la mitad para las atrincheradas.
- Cada unidad lleva munición para 24 horas. Cada batallón gasta 0,25 suministros por cada 100 hombres y hora de combate.
- Por mar, en convoyes de 100. Cada día un envío en el mar pierde ataque / (ataque + 2 × escolta + 30) de su carga, como mucho el 60 %. Los submarinos atacan ×2.

### Desgaste

- Invierno (por encima de 30° de latitud, lo peor desde 60°): hasta el 0,5 % de los hombres al día fuera de tus ciudades, y marchar cuesta hasta 2 veces más. En primavera y otoño el barro frena un 50 % de lo que frena la nieve.
- Desierto: 0,2 % al día. Cumbres y hielo polar: 0,4 %. Alta montaña: 0,1 %. Todo el año, con o sin suministro.

## Asedios

- Una provincia con murallas o castillo no se ocupa al entrar: hay que sitiarla durante 60 días × su bonificación de defensa (30 con murallas, 60 con castillo).
- Cada día se avanza 1 + la suma, por cada batallón de artillería sitiador, de su ataque / 12 × sus hombres / plantilla.
- Si no queda ningún sitiador, o termina la guerra, el asedio se levanta y se pierde lo avanzado.
- Las murallas y el castillo también multiplican el fuego de los defensores en batalla.

## Mar y aire

### Flotas

- Una fuerza tiene hasta 27 barcos. La Flota manda hasta 5 agrupaciones a menos de 2.000 km (+10 %); la Armada, sus flotas a menos de 6.000 km (+5 %).
- Sin combustible, una agrupación navega 4 veces más despacio y dispara al 50 %.
- En sus puertos se reparan y completan su tripulación (×2 con dique seco) y se modernizan por el 50 % del coste del modelo nuevo.

### Aviación

- Escuadrilla: 6 aviones. Aeródromo: 18 escuadrillas; portaaviones: 6.
- División aérea: hasta 6 unidades a menos de 1.500 km (+10 %); Mando aéreo: divisiones a menos de 4.000 km (+5 %).
- Cada misión cubre las provincias a menos de 300 km de su objetivo y gasta 1 de petróleo por escuadrilla y día. Con menos del 20 % de organización no despega.
- Domina el cielo quien tiene al menos el 60 % de los cazas sobre él: sus tropas disparan hasta un 15 % más y las enemigas otro tanto menos; bajo cielo enemigo se marcha ×1,5 más despacio, se pierde el 25 % de lo enviado y los bombarderos hacen el 50 %.
- Cada día una unidad aérea pierde fuego enemigo / (fuego enemigo + 5 × defensa + 20) de sus aviones, como mucho el 40 %.
- Bombardeo: cada punto de daño quita 0,33 % a un edificio, que produce menos según su daño y se derrumba al llegar al 100 %. Cada día sin bombardeos repara un 2 % de su daño.

## Diplomacia y guerra

### Puntuación de guerra

- Cada provincia vale 1 + habitantes / 2.000 (hasta +5), +2 con ciudad o +6 si es la capital.
- Puntuación = % del valor del enemigo que ocupas - % del tuyo que te ocupa + 2 por batalla ganada - 2 por perdida (las batallas, hasta ±25). Entre -100 y 100.

### Paz

- Paz blanca: todo vuelve a sus dueños.
- Quedarte con lo ocupado cuesta el % del valor del enemigo que ocupas.
- Tributo: 30 puntos; te paga el 25 % de sus ingresos de oro durante 5 años.
- Vasallo: 60 puntos; te paga el 10 % de sus ingresos, entra en tus guerras y puedes anexionarlo a los 10 años.
- Tras la paz, 730 días de tregua. Las provincias cedidas pierden 20 de moral.
- Una nación con todas sus ciudades ocupadas capitula: cada enemigo se queda con lo que ocupa y quien tiene su capital, con el resto.

### Opinión

Cada nación piensa algo de las demás, de -100 a 100: lo que ve ahora más lo que recuerda, que se olvida a 0,1 puntos al día.

- Lo que ve: aliados +30, en guerra -50, frontera común -10, enemigo común +25, rival común más fuerte +20, pacto +10, paso militar dado +10, misma fe +10, otra fe -10, gente suya bajo tu dominio -5 por provincia (hasta -30), vasallo -10, señor +10.
- Lo que recuerda: declararle la guerra -60, cada provincia quitada -5, romper una alianza -60, romper un pacto -30, tributo -30, regalo +15, liberarlo +40.
- Un regalo cuesta 30 días de tus ingresos de oro (mínimo 50).

### Qué aceptan los rivales

- Alianza: con opinión de al menos 40, y 3 aliados como mucho.
- Pacto de no agresión: con opinión de al menos 0, o -25 si tu ejército es más fuerte. Romperlo deja 365 días de tregua.
- Paso militar: con opinión de al menos 20.
- Paz: la que paga la puntuación de guerra, o la blanca si la guerra les va mal o se alarga.

## Comercio

- Cada nación en paz ofrece el 50 % de lo que gana al día de cada recurso.
- Valor en oro de cada unidad: comida 0,1, madera y piedra 0,5, carbón, cobre y caballos 1,5, hierro, plata, azufre y salitre 2, caucho 3, silicio, petróleo y aluminio 4.
- Los rivales se quedan un margen de 25 % - opinión / 400 (entre 0 y 50 %). Comercian con opinión de al menos -25.
- Cada acuerdo dura 365 días y como mucho hay 6 por nación. No hay acuerdo si lo vendido vale menos de 1 de oro al día.
- Cada acuerdo sube la opinión +5 (hasta +15); cancelarlo, -10. Se rompe si alguien no puede pagar o entregar, o con la guerra.

## Decisiones y victoria

### Decisiones

- Cada nación con ciudad recibe de media 1,5 decisiones al año, con dos respuestas. Sin responder en 30 días, se toma la primera.
- Lo que está en juego crece con la raíz cuadrada de la población / 20.000. La moral que cambian dura 365 días.

### Objetivos

- Cada objetivo cumplido da 50 de oro. El de población pide 10.000 habitantes.

### Victoria

- Dominación: todas las demás naciones han caído o son tus vasallas.
- Ciencia: descubrir todos los avances.
- Puntuación: al empezar el año 300, gana quien tenga más puntos: 1 por cada 1.000 habitantes, 5 por provincia, 20 por ciudad y 10 por avance.
