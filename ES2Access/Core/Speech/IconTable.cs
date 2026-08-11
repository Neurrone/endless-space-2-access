using System;
using System.Collections.Generic;

namespace ES2Access.Core.Speech
{
    /// <summary>
    /// Which <c>icon.*</c> string names each icon the game can draw - the whole vocabulary,
    /// written out, rather than guessed from the icon's id at speak time.
    ///
    /// Two kinds of icon reach a reader, and both end at the same key. An inline
    /// <c>[token]</c> is one of the entries the engine registers from its own
    /// <c>GuiSpecialCharacters</c> files - a closed set of 382, which is why a token this
    /// table does not know is a defect worth logging rather than a case to handle. A picture
    /// drawn on its own is a texture, and the set of those is open: every bitmap in the game
    /// can be drawn, so a texture missing here means "decoration", not "unnamed icon".
    ///
    /// The variants of one concept collapse onto one key on purpose. <c>[dust]</c> and
    /// <c>[dustColored]</c> are the same coin drawn in two colours, <c>FIDSIDUST</c> and
    /// <c>FIDSIDUSTLARGE</c> the same coin drawn at two sizes, and all four are the word
    /// "Dust" to somebody listening.
    ///
    /// An entry whose key is EMPTY is registered with the engine and deliberately has no name:
    /// the game's own file gives it <c>Codepoint="0"</c> and a value that is nothing but a
    /// <c>#RRGGBB#</c> colour run, so there is no picture there to name - <c>[blue-gray]</c>
    /// reached the player as the word "blue-gray" in the middle of "92% (blue-gray Infantry 44)".
    ///
    /// The picture table is derived from the game's own element data: a texture is named when
    /// some GuiElement that draws it also carries an inline token, which is the game itself
    /// saying the two render the same concept. Portraits, mood art, loading screens and
    /// notification illustrations are left out - they are pictures OF a thing rather than
    /// symbols standing in for a word.
    ///
    /// A symbol no element draws is added BY HAND and kept across a regeneration: the game also
    /// paints a few of them straight into a panel, where the picture is the only caption a number
    /// has - <c>TurnSymbol</c>, the hourglass the construction table puts its remaining turns
    /// beside.
    /// </summary>
    public static class IconTable
    {
        // token (without its brackets, upper case) = key
        private const string TokenRows =
            "A=icon.letter-a ACADEMY=icon.academy ACADEMYCENTER=icon.academy ACTIONPOINT=icon.action-point\n" +
            "ADMINISTRATOR=icon.overseer ADMIRAL=icon.guardian ADVENTURER=icon.seeker AFFINITY=icon.affinity\n" +
            "AMOEBAS=icon.amoeba AMOEBASCENTER=icon.amoeba ANOMALY=icon.anomaly ATTACKER=icon.attacker\n" +
            "B=icon.letter-b BACKDOOR=icon.backdoor BADANOMALY= BASRYXO=icon.basryxo\n" +
            "BASRYXOCENTER=icon.basryxo BATTLESHIP=icon.juggernaut BHAGABAS=icon.bhagaba\n" +
            "BHAGABASCENTER=icon.bhagaba BLUE-GRAY= BOARDINGPOD=icon.boarding-pod BOMBER=icon.bomber\n" +
            "BRANCH=icon.branch BUYOUT=icon.buyout C=icon.letter-c CARRIER=icon.carrier\n" +
            "CATALYST=icon.catalyst CITADEL=icon.citadel CITADELDEFENSE=icon.citadel-garrison\n" +
            "CITADELDEFENSECOLORED=icon.citadel-garrison COLONIZATION=icon.colonization\n" +
            "COLONIZER=icon.colonizer COMMANDPOINT=icon.command-point CORPORATE=icon.counselor\n" +
            "CRAVERS=icon.cravers CRAVERSCENTER=icon.cravers CROWN=icon.crown CURIOSITY=icon.curiosity\n" +
            "CURRENTTURN=icon.this-turn CUSTOM00=icon.custom-population-00\n" +
            "CUSTOM00CENTER=icon.custom-population-00 CUSTOM01=icon.custom-population-01\n" +
            "CUSTOM01CENTER=icon.custom-population-01 CUSTOM02=icon.custom-population-02\n" +
            "CUSTOM02CENTER=icon.custom-population-02 CUSTOM03=icon.custom-population-03\n" +
            "CUSTOM03CENTER=icon.custom-population-03 CUSTOM04=icon.custom-population-04\n" +
            "CUSTOM04CENTER=icon.custom-population-04 CUSTOM05=icon.custom-population-05\n" +
            "CUSTOM05CENTER=icon.custom-population-05 CUSTOM06=icon.custom-population-06\n" +
            "CUSTOM06CENTER=icon.custom-population-06 CUSTOM07=icon.custom-population-07\n" +
            "CUSTOM07CENTER=icon.custom-population-07 CUSTOM08=icon.custom-population-08\n" +
            "CUSTOM08CENTER=icon.custom-population-08 CUSTOM09=icon.custom-population-09\n" +
            "CUSTOM09CENTER=icon.custom-population-09 CUSTOM10=icon.custom-population-10\n" +
            "CUSTOM10CENTER=icon.custom-population-10 CUSTOM11=icon.custom-population-11\n" +
            "CUSTOM11CENTER=icon.custom-population-11 D=icon.letter-d DAMAGEAPPLIED=icon.damage-dealt\n" +
            "DAMAGERECEIVED=icon.damage-taken DEFEAT=icon.defeat DEFENDHOME=icon.homeland-defense\n" +
            "DEFENSE=icon.defender DEFENSIVEMILITARYPOWER=icon.defensive-power\n" +
            "DEFENSIVEPROGRAM=icon.defensive-program DELTA=icon.delta DELTAVALUE=\n" +
            "DERIVATE=icon.partial-derivative DEUYIVANS=icon.deuyivans DEUYIVANSCENTER=icon.deuyivans\n" +
            "DIPLOSTATEAGRESSIVE=icon.diplomacy-aggressive DIPLOSTATEALLIANCE=icon.diplomacy-alliance\n" +
            "DIPLOSTATEASSIMILATED=icon.diplomacy-assimilated DIPLOSTATECOLDWAR=icon.diplomacy-cold-war\n" +
            "DIPLOSTATEFRIENDLY=icon.diplomacy-friendly DIPLOSTATENEUTRAL=icon.diplomacy-neutral\n" +
            "DIPLOSTATEPARTNER=icon.diplomacy-partner DIPLOSTATEPEACE=icon.diplomacy-peace\n" +
            "DIPLOSTATETRUCE=icon.diplomacy-truce DIPLOSTATEUNKNOWN=icon.diplomacy-unknown\n" +
            "DIPLOSTATEWAR=icon.diplomacy-war DISCLAIMER=icon.disclaimer DUEL=icon.duel DUST=icon.dust\n" +
            "DUSTCOLORED=icon.dust E=icon.letter-e ECOLOGIST=icon.ecologists ECOLOGISTCOLORED=icon.ecologists\n" +
            "EFFICIENCY=icon.efficiency EPISTIS=icon.epistis EPISTISCENTER=icon.epistis\n" +
            "EUSOCIALS=icon.eusocials EUSOCIALSCENTER=icon.eusocials EVENT=icon.event\n" +
            "EXPLORER=icon.exploration EYDERS=icon.eyder EYDERSCENTER=icon.eyder F=icon.letter-f\n" +
            "FIDS=icon.fids FIDSI=icon.fidsi FIGHTER=icon.fighter FIXEDANOMALY= FOOD=icon.food\n" +
            "FOODCOLORED=icon.food G=icon.letter-g GALVRANS=icon.galvran GALVRANSCENTER=icon.galvran\n" +
            "GNASHASTS=icon.gnashast GNASHASTSCENTER=icon.gnashast GOLDENAGE=icon.golden-age GOODANOMALY=\n" +
            "GRAY= GREEN= GREENMAN=icon.amblyr GREENMANCENTER=icon.amblyr GROWTH=icon.growth\n" +
            "GROWTHCOLORED=icon.growth GUARDIANS=icon.guardians GUARDIANSCENTER=icon.guardians\n" +
            "H=icon.letter-h HACKINGBEACON=icon.transmigration-beacon HACKINGOPERATION=icon.hacking\n" +
            "HACKINGSPEED=icon.hacking-speed HAPPINESS=icon.approval HAPPINESSCOLORED=icon.approval\n" +
            "HARMONY=icon.harmony HARMONYCENTER=icon.harmony HAROSHEMS=icon.haroshems\n" +
            "HAROSHEMSCENTER=icon.haroshems HEALTH=icon.health HERO=icon.hero HEROCLASS=icon.hero-class\n" +
            "HISSHOS=icon.yuusho HISSHOSCENTER=icon.yuusho HONOR=icon.keii HONORCOLORED=icon.keii\n" +
            "HORATIO=icon.horatio HORATIOCENTER=icon.horatio HQ=icon.headquarters HUMIDITYGAS=icon.gas\n" +
            "I=icon.letter-i ILLO=icon.illo ILLOCENTER=icon.illo IMPROVEMENT=icon.improvement\n" +
            "INDUSTRIALIST=icon.industrialists INDUSTRIALISTCOLORED=icon.industrialists\n" +
            "INDUSTRY=icon.industry INDUSTRYCOLORED=icon.industry INFANTRY=icon.infantry\n" +
            "INFINITE=icon.unlimited INFLUENCEUPKEEP=icon.influence INFLUENCEUPKEEPCOLORED=icon.influence\n" +
            "INSPIRINGPRESENCE=icon.way-of-the-obsidian-eagle INTIMIDATION=icon.war-banners\n" +
            "INVISIBILITY=icon.invisibility J=icon.letter-j JUGGERNAUT=icon.behemoth K=icon.letter-k\n" +
            "KALGEROS=icon.kalgeros KALGEROSCENTER=icon.kalgeros KALMAT=icon.kalmat KALMATCENTER=icon.kalmat\n" +
            "KALTIKMAS=icon.kaltikma KALTIKMASCENTER=icon.kaltikma KAMIKAZE=icon.kamikaze L=icon.letter-l\n" +
            "LEFTCLICK=icon.left-click LEVEL=icon.level LIFEFORCE=icon.essence LIFEFORCECOLORED=icon.essence\n" +
            "LIFEFORCEUPKEEP=icon.essence-upkeep LONGRANGE=icon.long-range LUX00LUXURY=icon.luxury-resource\n" +
            "LUX01REDSANG=icon.redsang LUX02JADONYX=icon.jadonyx LUX03DUSTCIDUOUS=icon.dustciduous-trees\n" +
            "LUX04BLUECAP=icon.bluecap-mold LUX05EDENINCENSE=icon.eden-incense LUX06TRANSVINE=icon.transvine\n" +
            "LUX07DARKGLITTER=icon.dark-glitter LUX08UBERSPUDS=icon.superspuds LUX09HYDROMIEL=icon.hydromiel\n" +
            "LUX10VOIDSTONE=icon.void-stone LUX11PROTOORCHID=icon.proto-orchid\n" +
            "LUX12IONICCRISTAL=icon.ionic-crystal LUX13GIGALATTICE=icon.giga-lattice\n" +
            "LUX14LOSTCITIES=icon.lost-cities LUX15AMIANTHOID=icon.amianthoid LUX16GOSSAMER=icon.gossamer\n" +
            "LUX17MERCURITE=icon.mercurite LUX18ENDLESSFOUNDRIES=icon.endless-foundries\n" +
            "LUX19DUSTWATER=icon.dustwater LUX20PROTOSPORES=icon.proto-spores\n" +
            "LUX21METAENTACTOGEN=icon.meta-entactogen LUX22BENTHICGEMS=icon.benthic-gems\n" +
            "LUX23VIRTUALARTIFACTS=icon.virtual-artifacts LUX24DRIFTBUDS=icon.drift-buds M=icon.letter-m\n" +
            "MAJORHISSHOS=icon.hissho MAJORHISSHOSCENTER=icon.hissho MANPOWER=icon.manpower\n" +
            "MANPOWERCOLORED=icon.manpower MASTERYCOMMAND=icon.mastery-command\n" +
            "MASTERYCURIOSITY=icon.mastery-wanderlust MASTERYLABOUR=icon.mastery-labor\n" +
            "MASTERYWIT=icon.mastery-wit MAVROS=icon.mavros MAVROSCENTER=icon.mavros MEDAL=icon.medal\n" +
            "MEDIUMRANGE=icon.medium-range MERCENARIES=icon.mercenaries MEZARI=icon.mezari\n" +
            "MEZARICENTER=icon.mezari MIDANOMALY= MILITARIST=icon.militarists\n" +
            "MILITARISTCOLORED=icon.militarists MOINS=icon.minus MOTHERSHIP=icon.mothership\n" +
            "MOVE=icon.movement N=icon.letter-n NEGATIVEIMPACTWHITE=icon.negative NIRIS=icon.niris\n" +
            "NIRISCENTER=icon.niris O=icon.letter-o OBEDIENCE=icon.obedience OBEDIENCECOLORED=icon.obedience\n" +
            "OBLITERATOR=icon.obliterator OFFENSIVEMILITARYPOWER=icon.offensive-power\n" +
            "OFFENSIVEPROGRAM=icon.offensive-program OHM=icon.ohm ORACULAR=icon.xirmisala\n" +
            "ORACULARCENTER=icon.xirmisala OUTPOSTCOLORED= OVERCOLONIZATION=icon.overcolonization\n" +
            "OVERPOPULATION=icon.overpopulation P=icon.letter-p PACIFIST=icon.pacifists\n" +
            "PACIFISTCOLORED=icon.pacifists PI=icon.pi PILGRIMS=icon.pilgrims PILGRIMSCENTER=icon.pilgrims\n" +
            "PINK= PIRATES=icon.pirates PLANE=icon.air PLATING=icon.plating POLITICS=icon.politics\n" +
            "POOR=icon.sterile POPULATION=icon.population POPULATIONUNCOLORED=icon.population\n" +
            "POSITIVEIMPACT=icon.positive POSITIVEIMPACTWHITE=icon.positive PRESSURE=icon.pressure\n" +
            "PRESTIGE=icon.influence PRESTIGECOLORED=icon.influence PRIVATEERS=icon.privateers\n" +
            "PROCESSINGPOWER=icon.bandwidth PULSOS=icon.pulsos PULSOSCENTER=icon.pulsos Q=icon.letter-q\n" +
            "QUADRANTECONOMYANDTRADE=icon.quadrant-economy QUADRANTEMPIREDEVELOPMENT=icon.quadrant-empire\n" +
            "QUADRANTMILITARY=icon.quadrant-military QUADRANTSCIENCEANDEXPLORATION=icon.quadrant-science\n" +
            "QUEST=icon.quest R=icon.letter-r RANDOM=icon.random RANDOMCENTER=icon.random RED=\n" +
            "RELIC=icon.relic RELICCOLORED=icon.relic RELIGIOUS=icon.religious\n" +
            "RELIGIOUSCOLORED=icon.religious REMNANTS=icon.remnant REMNANTSCENTER=icon.remnant\n" +
            "REWARD=icon.reward RICH=icon.fertile RIGHTCLICK=icon.right-click ROLE=icon.role S=icon.letter-s\n" +
            "SCAVENGERS=icon.scavengers SCAVENGERSCENTER=icon.scavengers SCIENCE=icon.science\n" +
            "SCIENCECOLORED=icon.science SCIENTIST=icon.scientists SCIENTISTCOLORED=icon.scientists\n" +
            "SEFALOROS=icon.sefaloros SEFALOROSCENTER=icon.sefaloros SHEREDYN=icon.sheredyn\n" +
            "SHEREDYNCENTER=icon.sheredyn SHIELD=icon.shield SHIP=icon.ship SHIPCREW=icon.ship-manpower\n" +
            "SHIPCREWCOLORED=icon.ship-manpower SHORTRANGE=icon.short-range\n" +
            "SISTERSOFMERCY=icon.sisters-of-mercy SISTERSOFMERCYCENTER=icon.sisters-of-mercy SIZE=icon.size\n" +
            "SOPHONS=icon.sophons SOPHONSCENTER=icon.sophons SOWERS=icon.sowers SOWERSCENTER=icon.sowers\n" +
            "SQRT=icon.square-root STARSYSTEM=icon.star-system STRAT00STRATEGIC=icon.strategic-resource\n" +
            "STRAT01TITANIUM=icon.titanium STRAT01TITANIUMCOLORED=icon.titanium STRAT02HYPERIUM=icon.hyperium\n" +
            "STRAT02HYPERIUMCOLORED=icon.hyperium STRAT03ADAMANTIAN=icon.adamantian\n" +
            "STRAT03ADAMANTIANCOLORED=icon.adamantian STRAT04ANTI-MATTER=icon.antimatter\n" +
            "STRAT04ANTI-MATTERCOLORED=icon.antimatter STRAT05ORICHALCIX=icon.orichalcix\n" +
            "STRAT05ORICHALCIXCOLORED=icon.orichalcix STRAT06QUADRINIX=icon.quadrinix\n" +
            "STRAT06QUADRINIXCOLORED=icon.quadrinix SUM=icon.sum SUPERCOLONIZER=icon.cryogenic-colonizer\n" +
            "SUPERGUARDIANS=icon.guardian-guides SUPERGUARDIANSCENTER=icon.guardian-guides\n" +
            "SUPPORT=icon.support SYSTEMDEFENSE=icon.system-garrison\n" +
            "SYSTEMDEFENSECOLORED=icon.system-garrison T=icon.letter-t TANK=icon.armor\n" +
            "TECHNOLOGISTS=icon.technologists TECHNOLOGISTSCENTER=icon.technologists\n" +
            "TECHNOLOGY=icon.technology TEMPERATURECOLD=icon.cold TEMPERATUREHOT=icon.hot\n" +
            "TEMPERATURETEMPERATE=icon.temperate TEMPLAR=icon.nakalim TEMPLARCENTER=icon.nakalim\n" +
            "TERRAFORMATION=icon.terraformation TERRANS=icon.imperials TERRANSCENTER=icon.imperials\n" +
            "TIKANANS=icon.tikanan TIKANANSCENTER=icon.tikanan TIMELORDS=icon.riftborn\n" +
            "TIMELORDSCENTER=icon.riftborn TRADEEFFICIENCY=icon.trade-efficiency TRAITOR=icon.traitor\n" +
            "TURN=icon.turn TURNCOLORED=icon.turn U=icon.letter-u UMBRALCHOIR=icon.umbral-choir\n" +
            "UMBRALCHOIRCENTER=icon.umbral-choir UNFALLEN=icon.unfallen UNFALLENCENTER=icon.unfallen\n" +
            "UPKEEP=icon.upkeep UPKEEPCOLORED=icon.upkeep V=icon.letter-v VAMPIRILIS=icon.vodyani\n" +
            "VAMPIRILISCENTER=icon.vodyani VAULTERS=icon.vaulters VAULTERSCENTER=icon.vaulters\n" +
            "VAULTERSMILITARY=icon.bereaved VAULTERSMILITARYCENTER=icon.bereaved\n" +
            "VAULTERSSCIENCE=icon.foundlings VAULTERSSCIENCECENTER=icon.foundlings VENETIANS=icon.lumeris\n" +
            "VENETIANSCENTER=icon.lumeris VICTORY=icon.victory W=icon.letter-w\n" +
            "WARDOFTHEEMPEROR=icon.order-of-the-red-blade WAREXHAUST=icon.war-momentum\n" +
            "WARPOINT=icon.war-point WONDER=icon.wonder X=icon.letter-x XENOS=icon.xenos\n" +
            "XENOSCENTER=icon.xenos Y=icon.letter-y Z=icon.letter-z ZVALIS=icon.zvali ZVALISCENTER=icon.zvali";

        // texture name (upper case) = key
        private const string PictureRows =
            "ACADEMYFIDSIDUST=icon.dust ACADEMYFIDSIDUSTLARGE=icon.dust AFFINITYACADEMYMEDIUM=icon.academy\n" +
            "AFFINITYACADEMYMEDIUMINVERTED=icon.academy AFFINITYACADEMYSMALL=icon.academy\n" +
            "AFFINITYAMOEBALARGE=icon.amoeba AFFINITYAMOEBAMEDIUM=icon.amoeba\n" +
            "AFFINITYAMOEBAMEDIUMINVERTED=icon.amoeba AFFINITYAMOEBASMALL=icon.amoeba\n" +
            "AFFINITYBASRYXOLARGE=icon.basryxo AFFINITYBASRYXOMEDIUM=icon.basryxo\n" +
            "AFFINITYBASRYXOMEDIUMINVERTED=icon.basryxo AFFINITYBASRYXOSMALL=icon.basryxo\n" +
            "AFFINITYBENTHYSLARGE=icon.kalgeros AFFINITYBENTHYSMEDIUM=icon.kalgeros\n" +
            "AFFINITYBENTHYSMEDIUMINVERTED=icon.kalgeros AFFINITYBENTHYSSMALL=icon.kalgeros\n" +
            "AFFINITYBHAGABASLARGE=icon.bhagaba AFFINITYBHAGABASMEDIUM=icon.bhagaba\n" +
            "AFFINITYBHAGABASMEDIUMINVERTED=icon.bhagaba AFFINITYBHAGABASSMALL=icon.bhagaba\n" +
            "AFFINITYBOTSLARGE=icon.epistis AFFINITYBOTSMEDIUMINVERTED=icon.epistis\n" +
            "AFFINITYCRAVERSLARGE=icon.cravers AFFINITYCRAVERSMEDIUM=icon.cravers\n" +
            "AFFINITYCRAVERSMEDIUMINVERTED=icon.cravers AFFINITYCRAVERSSMALL=icon.cravers\n" +
            "AFFINITYDEUYIVANSLARGE=icon.deuyivans AFFINITYDEUYIVANSMEDIUM=icon.deuyivans\n" +
            "AFFINITYDEUYIVANSMEDIUMINVERTED=icon.deuyivans AFFINITYDEUYIVANSSMALL=icon.deuyivans\n" +
            "AFFINITYEYDERSLARGE=icon.eyder AFFINITYEYDERSMEDIUM=icon.eyder\n" +
            "AFFINITYEYDERSMEDIUMINVERTED=icon.eyder AFFINITYEYDERSSMALL=icon.eyder\n" +
            "AFFINITYGALVRANSLARGE=icon.galvran AFFINITYGALVRANSMEDIUM=icon.galvran\n" +
            "AFFINITYGALVRANSMEDIUMINVERTED=icon.galvran AFFINITYGALVRANSSMALL=icon.galvran\n" +
            "AFFINITYGNASHASTSLARGE=icon.gnashast AFFINITYGNASHASTSMEDIUM=icon.gnashast\n" +
            "AFFINITYGNASHASTSMEDIUMINVERTED=icon.gnashast AFFINITYGNASHASTSSMALL=icon.gnashast\n" +
            "AFFINITYGREENMANLARGE=icon.amblyr AFFINITYGREENMANMEDIUM=icon.amblyr\n" +
            "AFFINITYGREENMANMEDIUMINVERTED=icon.amblyr AFFINITYGREENMANSMALL=icon.amblyr\n" +
            "AFFINITYGUARDIANSLARGE=icon.guardians AFFINITYGUARDIANSMEDIUM=icon.guardians\n" +
            "AFFINITYGUARDIANSMEDIUMINVERTED=icon.guardians AFFINITYGUARDIANSSMALL=icon.guardians\n" +
            "AFFINITYHARMONYLARGE=icon.harmony AFFINITYHARMONYMEDIUM=icon.harmony\n" +
            "AFFINITYHARMONYMEDIUMINVERTED=icon.harmony AFFINITYHARMONYSMALL=icon.harmony\n" +
            "AFFINITYHAROSHEMSLARGE=icon.haroshems AFFINITYHAROSHEMSMEDIUM=icon.haroshems\n" +
            "AFFINITYHAROSHEMSMEDIUMINVERTED=icon.haroshems AFFINITYHAROSHEMSSMALL=icon.haroshems\n" +
            "AFFINITYHISSHOSLARGE=icon.yuusho AFFINITYHISSHOSMEDIUM=icon.yuusho\n" +
            "AFFINITYHISSHOSMEDIUMINVERTED=icon.yuusho AFFINITYHISSHOSSMALL=icon.yuusho\n" +
            "AFFINITYHORATIOLARGE=icon.horatio AFFINITYHORATIOMEDIUM=icon.horatio\n" +
            "AFFINITYHORATIOMEDIUMINVERTED=icon.horatio AFFINITYHORATIOSMALL=icon.horatio\n" +
            "AFFINITYILLOLARGE=icon.illo AFFINITYILLOMEDIUM=icon.illo AFFINITYILLOMEDIUMINVERTED=icon.illo\n" +
            "AFFINITYILLOSMALL=icon.illo AFFINITYKALTIKMASLARGE=icon.kaltikma\n" +
            "AFFINITYKALTIKMASMEDIUM=icon.kaltikma AFFINITYKALTIKMASMEDIUMINVERTED=icon.kaltikma\n" +
            "AFFINITYKALTIKMASSMALL=icon.kaltikma AFFINITYMAJORHISSHOSLARGE=icon.hissho\n" +
            "AFFINITYMAJORHISSHOSMEDIUM=icon.hissho AFFINITYMAJORHISSHOSMEDIUMINVERTED=icon.hissho\n" +
            "AFFINITYMAJORHISSHOSSMALL=icon.hissho AFFINITYMAVROSLARGE=icon.mavros\n" +
            "AFFINITYMAVROSMEDIUM=icon.mavros AFFINITYMAVROSMEDIUMINVERTED=icon.mavros\n" +
            "AFFINITYMAVROSSMALL=icon.mavros AFFINITYMEZARILARGE=icon.mezari AFFINITYMEZARIMEDIUM=icon.mezari\n" +
            "AFFINITYMEZARIMEDIUMINVERTED=icon.mezari AFFINITYMEZARISMALL=icon.mezari\n" +
            "AFFINITYMINORHACKINGLARGE=icon.kalmat AFFINITYMINORHACKINGMEDIUM=icon.kalmat\n" +
            "AFFINITYMINORHACKINGMEDIUMINVERTED=icon.kalmat AFFINITYMINORHACKINGSMALL=icon.kalmat\n" +
            "AFFINITYMINORORACULARSLARGE=icon.xirmisala AFFINITYMINORORACULARSMEDIUM=icon.xirmisala\n" +
            "AFFINITYMINORORACULARSMEDIUMINVERTED=icon.xirmisala AFFINITYMINORORACULARSSMALL=icon.xirmisala\n" +
            "AFFINITYNIRISLARGE=icon.niris AFFINITYNIRISMEDIUM=icon.niris\n" +
            "AFFINITYNIRISMEDIUMINVERTED=icon.niris AFFINITYNIRISSMALL=icon.niris\n" +
            "AFFINITYPILGRIMSLARGE=icon.pilgrims AFFINITYPILGRIMSMEDIUM=icon.pilgrims\n" +
            "AFFINITYPILGRIMSMEDIUMINVERTED=icon.pilgrims AFFINITYPILGRIMSSMALL=icon.pilgrims\n" +
            "AFFINITYPIRATEMEDIUM=icon.pirates AFFINITYPIRATEMEDIUMINVERTED=icon.pirates\n" +
            "AFFINITYPIRATESMALL=icon.pirates\n" +
            "AFFINITYPOPULATIONICONSOVERRIDE00MEDIUMINVERTED=icon.custom-population-00\n" +
            "AFFINITYPOPULATIONICONSOVERRIDE01MEDIUMINVERTED=icon.custom-population-01\n" +
            "AFFINITYPOPULATIONICONSOVERRIDE02MEDIUMINVERTED=icon.custom-population-02\n" +
            "AFFINITYPOPULATIONICONSOVERRIDE03MEDIUMINVERTED=icon.custom-population-03\n" +
            "AFFINITYPOPULATIONICONSOVERRIDE04MEDIUMINVERTED=icon.custom-population-04\n" +
            "AFFINITYPOPULATIONICONSOVERRIDE05MEDIUMINVERTED=icon.custom-population-05\n" +
            "AFFINITYPOPULATIONICONSOVERRIDE06MEDIUMINVERTED=icon.custom-population-06\n" +
            "AFFINITYPOPULATIONICONSOVERRIDE07MEDIUMINVERTED=icon.custom-population-07\n" +
            "AFFINITYPOPULATIONICONSOVERRIDE08MEDIUMINVERTED=icon.custom-population-08\n" +
            "AFFINITYPOPULATIONICONSOVERRIDE09MEDIUMINVERTED=icon.custom-population-09\n" +
            "AFFINITYPOPULATIONICONSOVERRIDE10MEDIUMINVERTED=icon.custom-population-10\n" +
            "AFFINITYPOPULATIONICONSOVERRIDE11MEDIUMINVERTED=icon.custom-population-11\n" +
            "AFFINITYPULSOSLARGE=icon.pulsos AFFINITYPULSOSMEDIUM=icon.pulsos\n" +
            "AFFINITYPULSOSMEDIUMINVERTED=icon.pulsos AFFINITYPULSOSSMALL=icon.pulsos\n" +
            "AFFINITYRANDOMLARGE=icon.random AFFINITYRANDOMMEDIUM=icon.random\n" +
            "AFFINITYRANDOMMEDIUMINVERTED=icon.random AFFINITYRANDOMSMALL=icon.random\n" +
            "AFFINITYREMNANTLARGE=icon.remnant AFFINITYREMNANTMEDIUM=icon.remnant\n" +
            "AFFINITYREMNANTMEDIUMINVERTED=icon.remnant AFFINITYREMNANTSMALL=icon.remnant\n" +
            "AFFINITYSCAVENGERSSMALL=icon.scavengers AFFINITYSEFALOROSLARGE=icon.sefaloros\n" +
            "AFFINITYSEFALOROSMEDIUM=icon.sefaloros AFFINITYSEFALOROSMEDIUMINVERTED=icon.sefaloros\n" +
            "AFFINITYSEFALOROSSMALL=icon.sefaloros AFFINITYSHEREDYNLARGE=icon.sheredyn\n" +
            "AFFINITYSHEREDYNMEDIUM=icon.sheredyn AFFINITYSHEREDYNMEDIUMINVERTED=icon.sheredyn\n" +
            "AFFINITYSHEREDYNSMALL=icon.sheredyn AFFINITYSISTERSOFMERCYLARGE=icon.sisters-of-mercy\n" +
            "AFFINITYSISTERSOFMERCYMEDIUM=icon.sisters-of-mercy\n" +
            "AFFINITYSISTERSOFMERCYMEDIUMINVERTED=icon.sisters-of-mercy\n" +
            "AFFINITYSISTERSOFMERCYSMALL=icon.sisters-of-mercy AFFINITYSOPHONSLARGE=icon.sophons\n" +
            "AFFINITYSOPHONSMEDIUM=icon.sophons AFFINITYSOPHONSMEDIUMINVERTED=icon.sophons\n" +
            "AFFINITYSOPHONSSMALL=icon.sophons AFFINITYSOWERSLARGE=icon.sowers\n" +
            "AFFINITYSOWERSMEDIUM=icon.sowers AFFINITYSOWERSMEDIUMINVERTED=icon.sowers\n" +
            "AFFINITYSOWERSSMALL=icon.sowers AFFINITYSUPERGUARDIANSLARGE=icon.guardian-guides\n" +
            "AFFINITYSUPERGUARDIANSMEDIUM=icon.guardian-guides\n" +
            "AFFINITYSUPERGUARDIANSMEDIUMINVERTED=icon.guardian-guides\n" +
            "AFFINITYSUPERGUARDIANSSMALL=icon.guardian-guides AFFINITYTEMPLARSLARGE=icon.nakalim\n" +
            "AFFINITYTEMPLARSMEDIUM=icon.nakalim AFFINITYTEMPLARSMEDIUMINVERTED=icon.nakalim\n" +
            "AFFINITYTEMPLARSSMALL=icon.nakalim AFFINITYTERRANSLARGE=icon.imperials\n" +
            "AFFINITYTERRANSMEDIUM=icon.imperials AFFINITYTERRANSMEDIUMINVERTED=icon.imperials\n" +
            "AFFINITYTERRANSSMALL=icon.imperials AFFINITYTIKANANSLARGE=icon.tikanan\n" +
            "AFFINITYTIKANANSMEDIUM=icon.tikanan AFFINITYTIKANANSMEDIUMINVERTED=icon.tikanan\n" +
            "AFFINITYTIKANANSSMALL=icon.tikanan AFFINITYTIMEBOTSMEDIUM=icon.epistis\n" +
            "AFFINITYTIMEBOTSSMALL=icon.epistis AFFINITYTIMELORDSLARGE=icon.riftborn\n" +
            "AFFINITYTIMELORDSMEDIUM=icon.riftborn AFFINITYTIMELORDSMEDIUMINVERTED=icon.riftborn\n" +
            "AFFINITYTIMELORDSSMALL=icon.riftborn AFFINITYUMBRALCHOIRLARGE=icon.umbral-choir\n" +
            "AFFINITYUMBRALCHOIRMEDIUM=icon.umbral-choir AFFINITYUMBRALCHOIRMEDIUMINVERTED=icon.umbral-choir\n" +
            "AFFINITYUMBRALCHOIRSMALL=icon.umbral-choir AFFINITYUNFALLENLARGE=icon.unfallen\n" +
            "AFFINITYUNFALLENMEDIUM=icon.unfallen AFFINITYUNFALLENMEDIUMINVERTED=icon.unfallen\n" +
            "AFFINITYUNFALLENSMALL=icon.unfallen AFFINITYVAMPIRILISLARGE=icon.vodyani\n" +
            "AFFINITYVAMPIRILISMEDIUM=icon.vodyani AFFINITYVAMPIRILISMEDIUMINVERTED=icon.vodyani\n" +
            "AFFINITYVAMPIRILISSMALL=icon.vodyani AFFINITYVAULTERSLARGE=icon.vaulters\n" +
            "AFFINITYVAULTERSMEDIUM=icon.vaulters AFFINITYVAULTERSMEDIUMINVERTED=icon.vaulters\n" +
            "AFFINITYVAULTERSMILITARYLARGE=icon.bereaved AFFINITYVAULTERSMILITARYMEDIUM=icon.bereaved\n" +
            "AFFINITYVAULTERSMILITARYMEDIUMINVERTED=icon.bereaved AFFINITYVAULTERSMILITARYSMALL=icon.bereaved\n" +
            "AFFINITYVAULTERSSCIENCELARGE=icon.foundlings AFFINITYVAULTERSSCIENCEMEDIUM=icon.foundlings\n" +
            "AFFINITYVAULTERSSCIENCEMEDIUMINVERTED=icon.foundlings\n" +
            "AFFINITYVAULTERSSCIENCESMALL=icon.foundlings AFFINITYVAULTERSSMALL=icon.vaulters\n" +
            "AFFINITYVENETIANSLARGE=icon.lumeris AFFINITYVENETIANSMEDIUM=icon.lumeris\n" +
            "AFFINITYVENETIANSMEDIUMINVERTED=icon.lumeris AFFINITYVENETIANSSMALL=icon.lumeris\n" +
            "AFFINITYZVALILARGE=icon.zvali AFFINITYZVALIMEDIUM=icon.zvali\n" +
            "AFFINITYZVALIMEDIUMINVERTED=icon.zvali AFFINITYZVALISMALL=icon.zvali APPROVALSMALL=icon.approval\n" +
            "CATEGORYMANPOWERCITADELSMALL=icon.citadel-garrison CATEGORYMANPOWERSHIPSMALL=icon.ship-manpower\n" +
            "CATEGORYMANPOWERSYSTEMSMALL=icon.manpower DIPLOMATICRELATIONSTATEHOTWARSMALL=icon.war-point\n" +
            "EMPIREMANPOWERLARGE=icon.manpower EMPIREMANPOWERSMALL=icon.manpower FIDSEMPIRE=icon.medal\n" +
            "FIDSI=icon.fids FIDSIDUST=icon.dust FIDSIDUSTLARGE=icon.dust FIDSIEMPIRE=icon.influence\n" +
            "FIDSIEMPIREPOINTLARGE=icon.influence FIDSIFOOD=icon.food FIDSIFOODLARGE=icon.food\n" +
            "FIDSIINDUSTRY=icon.industry FIDSIINDUSTRYLARGE=icon.industry FIDSISCIENCE=icon.science\n" +
            "FIDSISCIENCELARGE=icon.science GROUNDTROOPINFANTRYLARGE=icon.infantry\n" +
            "GROUNDTROOPPLANELARGE=icon.air GROUNDTROOPTANKLARGE=icon.armor GROWTHSMALL=icon.food\n" +
            "HACKINGSPEEDLARGE=icon.hacking-speed HACKINGSPEEDSMALL=icon.hacking-speed\n" +
            "HEADERMANPOWER=icon.manpower HEROCLASSADMINISTRATORSMALL=icon.overseer\n" +
            "HEROCLASSADMIRALSMALL=icon.guardian HEROCLASSADVENTURERSMALL=icon.seeker\n" +
            "HEROCLASSCORPORATESMALL=icon.counselor HONORLARGE=icon.keii HONORSMALL=icon.keii\n" +
            "INFANTRY=icon.infantry INFANTRYICONMEDIUM=icon.infantry LIFEFORCELARGE=icon.essence\n" +
            "LIFEFORCESMALL=icon.essence LUXURY01LARGE=icon.redsang LUXURY01SMALL=icon.redsang\n" +
            "LUXURY02LARGE=icon.jadonyx LUXURY02SMALL=icon.jadonyx LUXURY03LARGE=icon.dustciduous-trees\n" +
            "LUXURY03SMALL=icon.dustciduous-trees LUXURY04LARGE=icon.bluecap-mold\n" +
            "LUXURY04SMALL=icon.bluecap-mold LUXURY05LARGE=icon.eden-incense LUXURY05SMALL=icon.eden-incense\n" +
            "LUXURY06LARGE=icon.transvine LUXURY06SMALL=icon.transvine LUXURY07LARGE=icon.dark-glitter\n" +
            "LUXURY07SMALL=icon.dark-glitter LUXURY08LARGE=icon.superspuds LUXURY08SMALL=icon.superspuds\n" +
            "LUXURY09LARGE=icon.hydromiel LUXURY09SMALL=icon.hydromiel LUXURY10LARGE=icon.void-stone\n" +
            "LUXURY10SMALL=icon.void-stone LUXURY11LARGE=icon.proto-orchid LUXURY11SMALL=icon.proto-orchid\n" +
            "LUXURY12LARGE=icon.ionic-crystal LUXURY12SMALL=icon.ionic-crystal\n" +
            "LUXURY13LARGE=icon.giga-lattice LUXURY13SMALL=icon.giga-lattice LUXURY14LARGE=icon.lost-cities\n" +
            "LUXURY14SMALL=icon.lost-cities LUXURY15LARGE=icon.amianthoid LUXURY15SMALL=icon.amianthoid\n" +
            "LUXURY16LARGE=icon.gossamer LUXURY16SMALL=icon.gossamer LUXURY17LARGE=icon.mercurite\n" +
            "LUXURY17SMALL=icon.mercurite LUXURY18LARGE=icon.endless-foundries\n" +
            "LUXURY18SMALL=icon.endless-foundries LUXURY19LARGE=icon.dustwater LUXURY19SMALL=icon.dustwater\n" +
            "LUXURY20LARGE=icon.proto-spores LUXURY20SMALL=icon.proto-spores\n" +
            "LUXURY21LARGE=icon.meta-entactogen LUXURY21SMALL=icon.meta-entactogen\n" +
            "LUXURY22LARGE=icon.benthic-gems LUXURY22SMALL=icon.benthic-gems\n" +
            "LUXURY23LARGE=icon.virtual-artifacts LUXURY23SMALL=icon.virtual-artifacts\n" +
            "LUXURY24LARGE=icon.drift-buds LUXURY24SMALL=icon.drift-buds OBEDIENCESMALL=icon.obedience\n" +
            "PLANE=icon.air PLANEICONMEDIUM=icon.air POLITICSECOLOGISTLARGE=icon.ecologists\n" +
            "POLITICSECOLOGISTMEDIUM=icon.ecologists POLITICSECOLOGISTMEDIUMINVERTED=icon.ecologists\n" +
            "POLITICSECOLOGISTSMALL=icon.ecologists POLITICSINDUSTRIALISTLARGE=icon.industrialists\n" +
            "POLITICSINDUSTRIALISTMEDIUM=icon.industrialists\n" +
            "POLITICSINDUSTRIALISTMEDIUMINVERTED=icon.industrialists\n" +
            "POLITICSINDUSTRIALISTSMALL=icon.industrialists POLITICSMILITARISTLARGE=icon.militarists\n" +
            "POLITICSMILITARISTMEDIUM=icon.militarists POLITICSMILITARISTMEDIUMINVERTED=icon.militarists\n" +
            "POLITICSMILITARISTSMALL=icon.militarists POLITICSNEUTRALMEDIUM=icon.politics\n" +
            "POLITICSNEUTRALSMALL=icon.politics POLITICSPACIFISTLARGE=icon.pacifists\n" +
            "POLITICSPACIFISTMEDIUM=icon.pacifists POLITICSPACIFISTMEDIUMINVERTED=icon.pacifists\n" +
            "POLITICSPACIFISTSMALL=icon.pacifists POLITICSRELIGIOUSLARGE=icon.religious\n" +
            "POLITICSRELIGIOUSMEDIUM=icon.religious POLITICSRELIGIOUSMEDIUMINVERTED=icon.religious\n" +
            "POLITICSRELIGIOUSSMALL=icon.religious POLITICSSCIENTISTLARGE=icon.scientists\n" +
            "POLITICSSCIENTISTMEDIUM=icon.scientists POLITICSSCIENTISTMEDIUMINVERTED=icon.scientists\n" +
            "POLITICSSCIENTISTSMALL=icon.scientists POPULATIONICONSOVERRIDE00MEDIUM=icon.custom-population-00\n" +
            "POPULATIONICONSOVERRIDE00SMALL=icon.custom-population-00\n" +
            "POPULATIONICONSOVERRIDE01MEDIUM=icon.custom-population-01\n" +
            "POPULATIONICONSOVERRIDE01SMALL=icon.custom-population-01\n" +
            "POPULATIONICONSOVERRIDE02MEDIUM=icon.custom-population-02\n" +
            "POPULATIONICONSOVERRIDE02SMALL=icon.custom-population-02\n" +
            "POPULATIONICONSOVERRIDE03MEDIUM=icon.custom-population-03\n" +
            "POPULATIONICONSOVERRIDE03SMALL=icon.custom-population-03\n" +
            "POPULATIONICONSOVERRIDE04MEDIUM=icon.custom-population-04\n" +
            "POPULATIONICONSOVERRIDE04SMALL=icon.custom-population-04\n" +
            "POPULATIONICONSOVERRIDE05MEDIUM=icon.custom-population-05\n" +
            "POPULATIONICONSOVERRIDE05SMALL=icon.custom-population-05\n" +
            "POPULATIONICONSOVERRIDE06MEDIUM=icon.custom-population-06\n" +
            "POPULATIONICONSOVERRIDE06SMALL=icon.custom-population-06\n" +
            "POPULATIONICONSOVERRIDE07MEDIUM=icon.custom-population-07\n" +
            "POPULATIONICONSOVERRIDE07SMALL=icon.custom-population-07\n" +
            "POPULATIONICONSOVERRIDE08MEDIUM=icon.custom-population-08\n" +
            "POPULATIONICONSOVERRIDE08SMALL=icon.custom-population-08\n" +
            "POPULATIONICONSOVERRIDE09MEDIUM=icon.custom-population-09\n" +
            "POPULATIONICONSOVERRIDE09SMALL=icon.custom-population-09\n" +
            "POPULATIONICONSOVERRIDE10MEDIUM=icon.custom-population-10\n" +
            "POPULATIONICONSOVERRIDE10SMALL=icon.custom-population-10\n" +
            "POPULATIONICONSOVERRIDE11MEDIUM=icon.custom-population-11\n" +
            "POPULATIONICONSOVERRIDE11SMALL=icon.custom-population-11 POPULATIONINVERTEDSMALL=icon.population\n" +
            "PROCESSINGPOWERLARGE=icon.bandwidth PROCESSINGPOWERSMALL=icon.bandwidth RELICLARGE=icon.relic\n" +
            "RELICSMALL=icon.relic SHIPROLEATTACKERSMALL=icon.attacker\n" +
            "SHIPROLEBATTLESHIPSMALL=icon.juggernaut SHIPROLECARRIERSMALL=icon.carrier\n" +
            "SHIPROLECOLONIZERSMALL=icon.colonizer SHIPROLEDEFENSESMALL=icon.defender\n" +
            "SHIPROLEEXPLORATIONSMALL=icon.exploration SHIPROLEHEROSMALL=icon.hero\n" +
            "SHIPROLEJUGGERNAUTSMALL=icon.behemoth SHIPROLEMOTHERSHIPSMALL=icon.mothership\n" +
            "SHIPROLEOBLITERATORSMALL=icon.obliterator SHIPROLEROOTCREATORSMALL=icon.essence\n" +
            "SHIPROLESUPERCOLONIZERSMALL=icon.cryogenic-colonizer SHIPROLESUPPORTSMALL=icon.support\n" +
            "SHIPTYPEJUGGERNAUTMEDIUM=icon.juggernaut SHIPTYPEJUGGERNAUTSMALLINVERTED=icon.juggernaut\n" +
            "SHIPTYPELARGEMEDIUM=icon.carrier SHIPTYPELARGESMALLINVERTED=icon.carrier\n" +
            "SHIPTYPEMEDIUMATTACKMEDIUM=icon.attacker SHIPTYPEMEDIUMATTACKSMALLINVERTED=icon.attacker\n" +
            "SHIPTYPEMEDIUMSUPPORTMEDIUM=icon.support SHIPTYPEMEDIUMSUPPORTSMALLINVERTED=icon.support\n" +
            "SHIPTYPEMOTHERSHIPMEDIUM=icon.mothership SHIPTYPEMOTHERSHIPSMALLINVERTED=icon.mothership\n" +
            "SHIPTYPESMALLATTACKMEDIUM=icon.attacker SHIPTYPESMALLATTACKSMALLINVERTED=icon.attacker\n" +
            "SHIPTYPESMALLCIVILIANMEDIUM=icon.essence SHIPTYPESMALLCIVILIANSMALLINVERTED=icon.essence\n" +
            "SHIPTYPESMALLEXPLORERMEDIUM=icon.exploration SHIPTYPESMALLEXPLORERSMALLINVERTED=icon.exploration\n" +
            "SHIPTYPESMALLHEROMEDIUM=icon.hero SHIPTYPESMALLHEROSMALLINVERTED=icon.hero\n" +
            "SHIPTYPESMALLSUPERCOLONIZERMEDIUM=icon.cryogenic-colonizer\n" +
            "SHIPTYPESMALLSUPERCOLONIZERSMALLINVERTED=icon.cryogenic-colonizer\n" +
            "SHIPTYPESMALLSUPPORTMEDIUM=icon.support SHIPTYPESMALLSUPPORTSMALLINVERTED=icon.support\n" +
            "SQUADRONTYPEBOMBER=icon.bomber SQUADRONTYPEFIGHTER=icon.fighter STRATEGIC01=icon.titanium\n" +
            "STRATEGIC01LARGE=icon.titanium STRATEGIC01SMALL=icon.titanium STRATEGIC02=icon.hyperium\n" +
            "STRATEGIC02LARGE=icon.hyperium STRATEGIC02SMALL=icon.hyperium STRATEGIC03=icon.adamantian\n" +
            "STRATEGIC03LARGE=icon.adamantian STRATEGIC03SMALL=icon.adamantian STRATEGIC04=icon.antimatter\n" +
            "STRATEGIC04LARGE=icon.antimatter STRATEGIC04SMALL=icon.antimatter STRATEGIC05=icon.orichalcix\n" +
            "STRATEGIC05LARGE=icon.orichalcix STRATEGIC05SMALL=icon.orichalcix STRATEGIC06=icon.quadrinix\n" +
            "STRATEGIC06LARGE=icon.quadrinix STRATEGIC06SMALL=icon.quadrinix\n" +
            "SYSTEMDEFENSE=icon.system-garrison TANK=icon.armor TANKICONMEDIUM=icon.armor\n" +
            "TRACINGSPEEDLARGE=icon.hacking-speed TURNSYMBOL=icon.turn UPKEEP=icon.upkeep";

        private static Dictionary<string, string> _tokens;
        private static Dictionary<string, string> _pictures;

        /// <summary>The <c>icon.*</c> key for an inline <c>[token]</c> icon (brackets already
        /// stripped). False when the engine registers a token this table has never heard of -
        /// a new one from a patch or a mod, and the caller's cue to complain.</summary>
        public static bool TryKeyForToken(string token, out string key)
        {
            return Look(Tokens(), token, out key);
        }

        /// <summary>The <c>icon.*</c> key for a picture the game draws on its own, named by the
        /// texture being drawn. False for the pictures that are decoration.</summary>
        public static bool TryKeyForPicture(string asset, out string key)
        {
            return Look(Pictures(), asset, out key);
        }

        /// <summary>Every key the table can produce, for the tests that hold the shipped
        /// strings to it.</summary>
        public static IEnumerable<string> Keys
        {
            get
            {
                Dictionary<string, bool> seen = new Dictionary<string, bool>();
                List<string> keys = new List<string>();
                foreach (Dictionary<string, string> table in new[] { Tokens(), Pictures() })
                {
                    foreach (KeyValuePair<string, string> entry in table)
                    {
                        if (entry.Value.Length > 0 && !seen.ContainsKey(entry.Value))
                        {
                            seen[entry.Value] = true;
                            keys.Add(entry.Value);
                        }
                    }
                }

                return keys;
            }
        }

        private static bool Look(Dictionary<string, string> table, string id, out string key)
        {
            key = null;
            return !string.IsNullOrEmpty(id) && table.TryGetValue(id, out key);
        }

        private static Dictionary<string, string> Tokens()
        {
            if (_tokens == null)
            {
                _tokens = Parse(TokenRows);
            }

            return _tokens;
        }

        private static Dictionary<string, string> Pictures()
        {
            if (_pictures == null)
            {
                _pictures = Parse(PictureRows);
            }

            return _pictures;
        }

        private static Dictionary<string, string> Parse(string rows)
        {
            Dictionary<string, string> table = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase
            );
            foreach (string row in rows.Split('\n'))
            {
                foreach (string entry in row.Split(' '))
                {
                    int equals = entry.IndexOf('=');
                    if (equals > 0)
                    {
                        table[entry.Substring(0, equals)] = entry.Substring(equals + 1);
                    }
                }
            }

            return table;
        }
    }
}
