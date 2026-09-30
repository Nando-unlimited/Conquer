using Conquer.Client.Graphics;
using Conquer.Client.UI;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.World;

namespace Conquer.Client.Screens;

/// <summary>
/// The in-game help: topics on the left, the chosen one's text on the right, scrollable. Figures come from
/// the rules, so the help follows any rebalancing. In the text, "## " starts a heading and "- " a bullet.
/// </summary>
public sealed class HelpView
{
    private static readonly (string Title, string[] Text)[] Topics =
    [
        ("Controles",
        [
            "## Ratón",
            "- Clic izquierdo: selecciona una unidad o una provincia; sobre unas espadas rojas, abre la batalla.",
            "- Clic derecho: mueve la unidad seleccionada. Sobre una provincia enemiga con tropas, la ataca; sobre una flota tuya en el mar de al lado, embarca.",
            "- Arrastrar: mueve el mapa. Rueda: zoom.",
            "## Teclado",
            "- Espacio: pausa o reanuda. 1 a 5: velocidad del tiempo.",
            "- W A S D o las flechas: mueven el mapa. + y -: zoom.",
            "- Tab: cambia el modo de mapa. Inicio: centra el mapa en tu capital.",
            "- N: pantalla de la nación. F1: esta ayuda. Esc: cierra ventanas, quita la selección o abre el menú.",
        ]),
        ("Primeros pasos",
        [
            $"Empiezas con {GameRules.StartingCitizens:N0} colonos y nada de tierra. Busca una provincia fértil (mejor junto a un gran río) y pulsa «Fundar ciudad»: será tu capital.",
            "## Qué hacer después",
            "- En la pantalla de la nación (N), pestaña Ciencia, elige qué investigar en cada rama.",
            "- Entrena exploradores en la ciudad (baratos y rápidos, aunque cualquier unidad sirve) y reclama con ellos las provincias libres de alrededor: los colonos de tus ciudades irán a vivir allí solos.",
            $"- El mar, el hielo polar y las cumbres (por encima de {GameRules.PeakElevation:N0} m, una sola provincia por cordillera) no se pueden reclamar. Las tropas cruzan el hielo y las cumbres, despacio.",
            "- Construye granjas y aserraderos, y una ciudad nueva donde haya 500 habitantes.",
            "- Envía colonos desde una ciudad para fundar otras más lejos.",
            "## Consejo",
            "Vigila la comida en la barra superior: si se acaba, la gente muere de hambre y el humor se hunde.",
        ]),
        ("Población y humor",
        [
            "Cada provincia tiene habitantes, humor (0 a 100) y fertilidad. Cada una tiene sus propios nacimientos según su fertilidad, sus habitantes y la tierra que tiene, mientras haya comida y sitio; las ciudades crecen el doble de rápido.",
            "## Humor",
            $"- Sube con la ciudad (+{GameRules.CityMood:0}), la capital (+{GameRules.CapitalMood:0}), las reservas de comida, las fiestas (+{GameRules.FestivalMood:0} durante {GameRules.FestivalDays} días), templos, anfiteatros y avances.",
            $"- Baja con la distancia a la capital (hasta -{GameRules.MaxDistanceMoodPenalty:0}), el hacinamiento, el hambre ({GameRules.StarvingMood:0}) y la ocupación enemiga.",
            $"- Por debajo de {GameRules.UnrestMood:0} la provincia está descontenta y no paga impuestos. Un humor alto hace trabajar más.",
            "## Migración",
            "Las ciudades envían gente a tus provincias poco pobladas. Puedes forzar una migración pagando oro, pero los migrantes llegan descontentos.",
        ]),
        ("Economía y recursos",
        [
            "Tus provincias producen comida y madera, y pagan oro en impuestos. Todo va al almacén de la nación (barra superior).",
            "## Yacimientos",
            "- Carbón, hierro, cobre, oro, plata, silicio, petróleo, aluminio y caucho salen de yacimientos: bolsas finitas que se agotan.",
            "- Al empezar solo conoces el cobre, el oro y la plata; los demás los revelan los avances (Minería el carbón, Trabajo del hierro el hierro, Química el caucho...).",
            $"- Un yacimiento rinde al máximo con {GameRules.DepositFullWorkers:N0} habitantes en su provincia. El modo de mapa Recursos los muestra.",
            "## Edificios",
            "En la pestaña Edificios de cada provincia. Granjas, graneros, aserraderos y minas van en cualquier provincia habitada; el resto, solo en ciudades. Cada edificio pide un avance.",
            "## Carreteras y ferrocarriles",
            "- Los construyen tus ingenieros: desde una ciudad o un cuartel general tuyos, pulsa «Construir carretera» en su panel y elige qué otra ciudad o cuartel unir (el más cercano sale elegido).",
            "- La ruta es la que seguiría un ejército, por tu tierra, la que ocupas o la libre; las ciudades por las que pasa quedan unidas también. Solo se pagan los tramos que faltan.",
            "- Cada batallón de ingenieros en la ruta hace un día de trabajo al día; si se van, la obra se para. Se ven en el mapa: la carretera clara, el ferrocarril oscuro con traviesas.",
            $"- Por ellas se marcha más deprisa y llega el suministro: a todo lo que unen a tus ciudades y, desde ahí, hasta {MilitaryRules.SupplyRangeHours / 24:0} días de marcha.",
        ]),
        ("Ciudades",
        [
            "Las ciudades producen ciencia, crecen más deprisa, entrenan tropas y construyen los mejores edificios.",
            $"- Con colonos: «Fundar ciudad» en una provincia libre o tuya.",
            $"- Sin colonos: en la pestaña Edificios de una provincia tuya con al menos {GameRules.CityBuildingPopulation} habitantes aparece «Ciudad» ({GameRules.CityCost}, {GameRules.CityBuildingDays} días).",
            "- No puede haber dos ciudades juntas. Tú eliges el nombre.",
        ]),
        ("Ciencia",
        [
            "La ciencia de tus ciudades avanza en tres ramas a la vez: Economía, Sociedad y Militar. En la pestaña Ciencia (N) eliges qué investigar en cada una.",
            "## Prioridades",
            $"- Cada rama tiene una prioridad de 0 a {GameRules.MaxResearchPriority}: la ciencia se reparte en proporción.",
            "- Una rama sin nada elegido cede su parte a las demás; si ninguna investiga, la ciencia se guarda para el siguiente avance que elijas.",
            "## Niveles y eras",
            "- Cada nivel se abre al conocer uno de los avances del nivel anterior de su rama. Algunos avances piden otros concretos.",
            "- Los niveles se agrupan en eras: Antigüedad, Clásica, Medieval, Renacimiento, Industrial y Moderna. Arriba de la pestaña hay un botón por era.",
            $"- Cada nación vecina que ya conoce un avance te lo abarata un {GameRules.NeighbourResearchDiscount:P0}.",
            "## Instituciones",
            $"- Cada era nueva tiene una institución (Urbanismo, Feudalismo, Humanismo, Industrialización, Electrificación) que nace en algún lugar del mundo y se extiende de provincia en provincia.",
            $"- Tu nación la adopta cuando la tiene la mitad de tu población, o antes pagando oro. Da un bonus; mientras no la adoptes, los avances de su era cuestan un {GameRules.InstitutionPenalty:P0} más.",
            "- El modo de mapa Instituciones muestra por dónde van.",
        ]),
        ("Ejército",
        [
            "Las unidades de combate se forman con batallones entrenados en las ciudades (pestaña Ejército de la provincia). Los hombres salen de la ciudad.",
            "## Organización",
            $"- Cada unidad se llama por su tamaño: Regimiento (1 a 3 batallones), Brigada (4 a 6) y División (7 a {MilitaryRules.MaxBattalionsPerUnit}). Son las que se mueven y combaten.",
            "- Diseña plantillas en la pestaña Plantillas de la nación para entrenar unidades enteras de golpe.",
            "- El botón «Editar unidad» de su panel abre una ventana para renombrarla, separar varios batallones a la vez, unirla con otras de la provincia y elegir su oficial.",
            "- Por encima están los cuarteles generales: Cuerpo, Ejército y Grupo de ejércitos. Dan un bonus en combate a las unidades bajo su mando que estén a su alcance.",
            "## Mantenimiento",
            $"- Cada día, batallones, barcos y cuarteles cuestan un {MilitaryRules.UpkeepGoldShare:P0} de su oro y un {MilitaryRules.UpkeepResourceShare:P0} de sus demás recursos (la madera no).",
            "- Si no hay con qué pagar, las tropas pierden organización, desertan y no se recuperan.",
            "## Oficiales y experiencia",
            $"- Recluta oficiales con oro ({MilitaryRules.OfficerCost:0}) en la ventana de edición de una unidad: van a la reserva de tu nación y desde ahí los pones al mando.",
            "- Su rango va con el tamaño de lo que mandan: Coronel (regimiento), Brigadier (brigada), General de división (división), Teniente general (cuerpo), General (ejército) y Mariscal (grupo de ejércitos). Ascienden solos cuando su unidad crece.",
            "- Cada oficial tiene una o dos virtudes (ofensivo, defensivo, organizador, táctico, marchador, intendente), que mejoran con sus estrellas, y a veces un defecto (timorato, temerario, desorganizado, indeciso, lento, corrupto).",
            "- El oficial de una unidad de combate le aplica sus rasgos; el general de un cuartel, a las unidades bajo su mando que estén a su alcance.",
            $"- Tienen de 1 a {Officer.MaxSkill} estrellas y ganan una cada {Officer.VictoriesPerStar} victorias. Si su unidad es destruida en combate, caen con ella; si la relevas o la unes a otra, vuelven a la reserva.",
            $"- Los batallones ganan experiencia combatiendo (Novato, Regular, Veterano, Élite): hasta +{MilitaryRules.ExperienceBonus:P0} de fuego. Los reclutas nuevos la diluyen.",
            "## Suministro y combate",
            "- Las tropas se abastecen desde tus ciudades y por tus carreteras y ferrocarriles, a través de tierra propia o libre; sin suministro pierden hombres y organización.",
            "- Mover una unidad a una provincia enemiga con tropas la ataca; sin tropas, la ocupa. Cada hora ambos bandos se dañan; el que pierde la organización se retira.",
            $"- Frente: solo combaten a la vez los mejores batallones que caben en él ({MilitaryRules.FrontWidth(Biome.Grassland)} en llano, {MilitaryRules.FrontWidth(Biome.HighMountains)} en alta montaña); el resto espera en reserva.",
            "- La artillería, la aviación y los ingenieros van detrás del frente (la mitad de ancho) y reciben menos daño.",
            $"- Si el atacante lleva ingenieros, el río no le frena y la ventaja del terreno del defensor se queda en la {MilitaryRules.EngineeredTerrainDefense:P0}.",
            $"- Armas combinadas: cada tipo de tropa distinto (infantería, caballería, artillería, blindados, aviación, ingenieros) suma +{MilitaryRules.CombinedArmsBonus:P0} de fuego, hasta +{MilitaryRules.MaxCombinedArmsBonus:P0}.",
            "- Defender es más fácil en montañas, bosques, ríos y detrás de murallas o castillos. La caballería rinde menos en terreno difícil.",
        ]),
        ("Flotas y mar",
        [
            "Las tropas no cruzan el mar solas: necesitan barcos. Solo los aviones vuelan sobre él.",
            "## Puertos y barcos",
            "- Construye un Puerto (Navegación a vela) en una ciudad con costa. Allí se construyen los barcos, y cada uno forma una flota.",
            "- Trirremes, galeones, acorazados, destructores y portaaviones combaten; los barcos de transporte llevan tropas. Los más avanzados piden un Dique seco.",
            "- Las flotas navegan por el mar costero con Navegación a vela y por el océano con Cartografía. Se reparan en puerto.",
            "## Transportar tropas",
            "- Embarca una unidad con el botón «Embarcar en...» de su panel o con clic derecho sobre una flota tuya con transportes en el mar de al lado.",
            "- Mueve la flota y, con la unidad embarcada seleccionada, haz clic derecho en la costa para desembarcar. En tierra enemiga sin tropas, la ocupa.",
            "- Las flotas enemigas que se encuentran combaten; la que se rompe huye o se hunde con lo que lleva.",
        ]),
        ("Diplomacia",
        [
            "En la pestaña Diplomacia de la nación declaras la guerra y propones la paz.",
            "- Tus ejércitos solo entran en tierras de naciones con las que estás en guerra.",
            "- Los rivales aceptan la paz si la guerra les va mal o se alarga. Con la paz, las provincias ocupadas vuelven a sus dueños.",
        ]),
        ("Mapa",
        [
            "Los botones de abajo (o Tab) cambian el modo de mapa:",
            "- Terreno y Político: el mundo y quién es dueño de cada provincia.",
            "- Población, Humor y Fertilidad: el estado de cada provincia habitada.",
            "- Recursos: los yacimientos que conoces, con un filtro por recurso.",
            "- Instituciones: por dónde se han extendido.",
            "Al pasar el ratón por una provincia verás sus datos. Las espadas rojas marcan batallas, en tierra o en el mar: haz clic en ellas para ver la batalla en detalle.",
        ]),
        ("Partida",
        [
            "- El menú (Esc o botón Menú) pausa el juego, guarda la partida y muestra el historial de versiones.",
            "- La dificultad se elige al empezar: cambia cuántos yacimientos hay y su tamaño, los recursos iniciales y lo que producen los rivales.",
            "- Los rivales del ordenador se expanden, investigan, construyen y hacen la guerra por su cuenta.",
        ]),
    ];

    /// <summary>Every topic title and line of text, for checking them.</summary>
    public static IEnumerable<string> AllText => Topics.SelectMany(t => t.Text.Prepend(t.Title));

    private readonly List<(string Text, bool Heading, float Indent)> _lines = [];
    private int _topic;
    private float _scroll;
    private float _wrappedFor = -1;

    public bool Visible { get; set; }

    public void Frame(Ui ui, Rect area)
    {
        if (!Visible) return;
        ui.Panel(area, opaque: true);
        ui.Text(area.X + 20, area.Y + 14, "Ayuda", Theme.Accent, FontSize.Large, bold: true);
        if (ui.Button(new Rect(area.Right - 120, area.Y + 12, 100, 32), "Cerrar", tooltip: "Cerrar (F1 o Esc)")) Visible = false;

        // Topics down the left.
        float ty = area.Y + 60;
        for (int i = 0; i < Topics.Length; i++)
        {
            if (ui.Button(new Rect(area.X + 20, ty, 190, 30), Topics[i].Title, active: i == _topic, size: FontSize.Small) && i != _topic)
            {
                _topic = i;
                _scroll = 0;
                _wrappedFor = -1;
            }
            ty += 34;
        }

        var content = new Rect(area.X + 230, area.Y + 60, area.W - 250, area.H - 76);
        if (_wrappedFor != content.W) Layout(ui.Font, content.W);
        if (ui.Hover(content)) _scroll -= ui.Input.Scroll * 60;
        float Height((string Text, bool Heading, float Indent) l) => ui.Font.LineHeight(FontSize.Normal, l.Heading) + (l.Heading ? 10 : 2);
        _scroll = Math.Clamp(_scroll, 0, Math.Max(0, _lines.Sum(Height) - content.H));

        float y = content.Y - _scroll;
        foreach (var line in _lines)
        {
            float h = Height(line);
            if (y >= content.Y - 1 && y + h <= content.Bottom + 1)
                ui.Text(content.X + line.Indent, y + (line.Heading ? 8 : 0), line.Text, line.Heading ? Theme.Accent : Theme.Text, FontSize.Normal, line.Heading);
            y += h;
        }
    }

    /// <summary>Wraps the chosen topic to the panel's width.</summary>
    private void Layout(Font font, float width)
    {
        _wrappedFor = width;
        _lines.Clear();
        foreach (var paragraph in Topics[_topic].Text)
        {
            if (paragraph.StartsWith("## ")) _lines.Add((paragraph[3..], true, 0));
            else if (paragraph.StartsWith("- "))
            {
                var wrapped = font.Wrap(paragraph[2..], width - 24, FontSize.Normal);
                for (int i = 0; i < wrapped.Count; i++) _lines.Add(((i == 0 ? "· " : "") + wrapped[i], false, i == 0 ? 6 : 18));
            }
            else
            {
                foreach (var l in font.Wrap(paragraph, width, FontSize.Normal)) _lines.Add((l, false, 0));
                _lines.Add(("", false, 0));
            }
        }
    }
}
