using System.Collections.Generic;

namespace ES2Access.Core.Speech
{
    /// <summary>
    /// What each icon the game draws is called. One key per concept - see
    /// <see cref="IconTable"/> for which tokens and textures land on which key - and one
    /// complete word or phrase per key, because these are substituted into the middle of a
    /// sentence the game wrote: "+10 Food per Fertile".
    ///
    /// They are ordinary translatable strings and live apart from the rest only because there
    /// are so many of them.
    /// </summary>
    public static partial class ModStrings
    {
        public const string IconAcademy = "icon.academy";
        public const string IconActionPoint = "icon.action-point";
        public const string IconAdamantian = "icon.adamantian";
        public const string IconAffinity = "icon.affinity";
        public const string IconAir = "icon.air";
        public const string IconAmblyr = "icon.amblyr";
        public const string IconAmianthoid = "icon.amianthoid";
        public const string IconAmoeba = "icon.amoeba";
        public const string IconAnomaly = "icon.anomaly";
        public const string IconAntimatter = "icon.antimatter";
        public const string IconApproval = "icon.approval";
        public const string IconArmor = "icon.armor";
        public const string IconAttacker = "icon.attacker";
        public const string IconBackdoor = "icon.backdoor";
        public const string IconBandwidth = "icon.bandwidth";
        public const string IconBasryxo = "icon.basryxo";
        public const string IconBehemoth = "icon.behemoth";
        public const string IconBenthicGems = "icon.benthic-gems";
        public const string IconBereaved = "icon.bereaved";
        public const string IconBhagaba = "icon.bhagaba";
        public const string IconBluecapMold = "icon.bluecap-mold";
        public const string IconBoardingPod = "icon.boarding-pod";
        public const string IconBomber = "icon.bomber";
        public const string IconBranch = "icon.branch";
        public const string IconBuyout = "icon.buyout";
        public const string IconCarrier = "icon.carrier";
        public const string IconCatalyst = "icon.catalyst";
        public const string IconCitadel = "icon.citadel";
        public const string IconCitadelGarrison = "icon.citadel-garrison";
        public const string IconCold = "icon.cold";
        public const string IconColonization = "icon.colonization";
        public const string IconColonizer = "icon.colonizer";
        public const string IconCommandPoint = "icon.command-point";
        public const string IconCounselor = "icon.counselor";
        public const string IconCravers = "icon.cravers";
        public const string IconCrown = "icon.crown";
        public const string IconCryogenicColonizer = "icon.cryogenic-colonizer";
        public const string IconCuriosity = "icon.curiosity";
        public const string IconCustomPopulation00 = "icon.custom-population-00";
        public const string IconCustomPopulation01 = "icon.custom-population-01";
        public const string IconCustomPopulation02 = "icon.custom-population-02";
        public const string IconCustomPopulation03 = "icon.custom-population-03";
        public const string IconCustomPopulation04 = "icon.custom-population-04";
        public const string IconCustomPopulation05 = "icon.custom-population-05";
        public const string IconCustomPopulation06 = "icon.custom-population-06";
        public const string IconCustomPopulation07 = "icon.custom-population-07";
        public const string IconCustomPopulation08 = "icon.custom-population-08";
        public const string IconCustomPopulation09 = "icon.custom-population-09";
        public const string IconCustomPopulation10 = "icon.custom-population-10";
        public const string IconCustomPopulation11 = "icon.custom-population-11";
        public const string IconDamageDealt = "icon.damage-dealt";
        public const string IconDamageTaken = "icon.damage-taken";
        public const string IconDarkGlitter = "icon.dark-glitter";
        public const string IconDefeat = "icon.defeat";
        public const string IconDefender = "icon.defender";
        public const string IconDefensivePower = "icon.defensive-power";
        public const string IconDefensiveProgram = "icon.defensive-program";
        public const string IconDelta = "icon.delta";
        public const string IconDeuyivans = "icon.deuyivans";
        public const string IconDiplomacyAggressive = "icon.diplomacy-aggressive";
        public const string IconDiplomacyAlliance = "icon.diplomacy-alliance";
        public const string IconDiplomacyAssimilated = "icon.diplomacy-assimilated";
        public const string IconDiplomacyColdWar = "icon.diplomacy-cold-war";
        public const string IconDiplomacyFriendly = "icon.diplomacy-friendly";
        public const string IconDiplomacyNeutral = "icon.diplomacy-neutral";
        public const string IconDiplomacyPartner = "icon.diplomacy-partner";
        public const string IconDiplomacyPeace = "icon.diplomacy-peace";
        public const string IconDiplomacyTruce = "icon.diplomacy-truce";
        public const string IconDiplomacyUnknown = "icon.diplomacy-unknown";
        public const string IconDiplomacyWar = "icon.diplomacy-war";
        public const string IconDisclaimer = "icon.disclaimer";
        public const string IconDriftBuds = "icon.drift-buds";
        public const string IconDuel = "icon.duel";
        public const string IconDust = "icon.dust";
        public const string IconDustciduousTrees = "icon.dustciduous-trees";
        public const string IconDustwater = "icon.dustwater";
        public const string IconEcologists = "icon.ecologists";
        public const string IconEdenIncense = "icon.eden-incense";
        public const string IconEfficiency = "icon.efficiency";
        public const string IconEndlessFoundries = "icon.endless-foundries";
        public const string IconEpistis = "icon.epistis";
        public const string IconEssence = "icon.essence";
        public const string IconEssenceUpkeep = "icon.essence-upkeep";
        public const string IconEusocials = "icon.eusocials";
        public const string IconEvent = "icon.event";
        public const string IconExploration = "icon.exploration";
        public const string IconEyder = "icon.eyder";
        public const string IconFertile = "icon.fertile";
        public const string IconFids = "icon.fids";
        public const string IconFidsi = "icon.fidsi";
        public const string IconFighter = "icon.fighter";
        public const string IconFood = "icon.food";
        public const string IconFoundlings = "icon.foundlings";
        public const string IconGalvran = "icon.galvran";
        public const string IconGas = "icon.gas";
        public const string IconGigaLattice = "icon.giga-lattice";
        public const string IconGnashast = "icon.gnashast";
        public const string IconGoldenAge = "icon.golden-age";
        public const string IconGossamer = "icon.gossamer";
        public const string IconGrowth = "icon.growth";
        public const string IconGuardian = "icon.guardian";
        public const string IconGuardianGuides = "icon.guardian-guides";
        public const string IconGuardians = "icon.guardians";
        public const string IconHacking = "icon.hacking";
        public const string IconHackingSpeed = "icon.hacking-speed";
        public const string IconHarmony = "icon.harmony";
        public const string IconHaroshems = "icon.haroshems";
        public const string IconHeadquarters = "icon.headquarters";
        public const string IconHealth = "icon.health";
        public const string IconHero = "icon.hero";
        public const string IconHeroClass = "icon.hero-class";
        public const string IconHissho = "icon.hissho";
        public const string IconHomelandDefense = "icon.homeland-defense";
        public const string IconHoratio = "icon.horatio";
        public const string IconHot = "icon.hot";
        public const string IconHydromiel = "icon.hydromiel";
        public const string IconHyperium = "icon.hyperium";
        public const string IconIllo = "icon.illo";
        public const string IconImperials = "icon.imperials";
        public const string IconImprovement = "icon.improvement";
        public const string IconIndustrialists = "icon.industrialists";
        public const string IconIndustry = "icon.industry";
        public const string IconInfantry = "icon.infantry";
        public const string IconInfluence = "icon.influence";
        public const string IconInvisibility = "icon.invisibility";
        public const string IconIonicCrystal = "icon.ionic-crystal";
        public const string IconJadonyx = "icon.jadonyx";
        public const string IconJuggernaut = "icon.juggernaut";
        public const string IconKalgeros = "icon.kalgeros";
        public const string IconKalmat = "icon.kalmat";
        public const string IconKaltikma = "icon.kaltikma";
        public const string IconKamikaze = "icon.kamikaze";
        public const string IconKeii = "icon.keii";
        public const string IconLeftClick = "icon.left-click";
        public const string IconLetterA = "icon.letter-a";
        public const string IconLetterB = "icon.letter-b";
        public const string IconLetterC = "icon.letter-c";
        public const string IconLetterD = "icon.letter-d";
        public const string IconLetterE = "icon.letter-e";
        public const string IconLetterF = "icon.letter-f";
        public const string IconLetterG = "icon.letter-g";
        public const string IconLetterH = "icon.letter-h";
        public const string IconLetterI = "icon.letter-i";
        public const string IconLetterJ = "icon.letter-j";
        public const string IconLetterK = "icon.letter-k";
        public const string IconLetterL = "icon.letter-l";
        public const string IconLetterM = "icon.letter-m";
        public const string IconLetterN = "icon.letter-n";
        public const string IconLetterO = "icon.letter-o";
        public const string IconLetterP = "icon.letter-p";
        public const string IconLetterQ = "icon.letter-q";
        public const string IconLetterR = "icon.letter-r";
        public const string IconLetterS = "icon.letter-s";
        public const string IconLetterT = "icon.letter-t";
        public const string IconLetterU = "icon.letter-u";
        public const string IconLetterV = "icon.letter-v";
        public const string IconLetterW = "icon.letter-w";
        public const string IconLetterX = "icon.letter-x";
        public const string IconLetterY = "icon.letter-y";
        public const string IconLetterZ = "icon.letter-z";
        public const string IconLevel = "icon.level";
        public const string IconLongRange = "icon.long-range";
        public const string IconLostCities = "icon.lost-cities";
        public const string IconLumeris = "icon.lumeris";
        public const string IconLuxuryResource = "icon.luxury-resource";
        public const string IconManpower = "icon.manpower";
        public const string IconMasteryCommand = "icon.mastery-command";
        public const string IconMasteryLabor = "icon.mastery-labor";
        public const string IconMasteryWanderlust = "icon.mastery-wanderlust";
        public const string IconMasteryWit = "icon.mastery-wit";
        public const string IconMavros = "icon.mavros";
        public const string IconMedal = "icon.medal";
        public const string IconMediumRange = "icon.medium-range";
        public const string IconMercenaries = "icon.mercenaries";
        public const string IconMercurite = "icon.mercurite";
        public const string IconMetaEntactogen = "icon.meta-entactogen";
        public const string IconMezari = "icon.mezari";
        public const string IconMilitarists = "icon.militarists";
        public const string IconMinus = "icon.minus";
        public const string IconMothership = "icon.mothership";
        public const string IconMovement = "icon.movement";
        public const string IconNakalim = "icon.nakalim";
        public const string IconNegative = "icon.negative";
        public const string IconNiris = "icon.niris";
        public const string IconObedience = "icon.obedience";
        public const string IconObliterator = "icon.obliterator";
        public const string IconOffensivePower = "icon.offensive-power";
        public const string IconOffensiveProgram = "icon.offensive-program";
        public const string IconOhm = "icon.ohm";
        public const string IconOrderOfTheRedBlade = "icon.order-of-the-red-blade";
        public const string IconOrichalcix = "icon.orichalcix";
        public const string IconOvercolonization = "icon.overcolonization";
        public const string IconOverpopulation = "icon.overpopulation";
        public const string IconOverseer = "icon.overseer";
        public const string IconPacifists = "icon.pacifists";
        public const string IconPartialDerivative = "icon.partial-derivative";
        public const string IconPi = "icon.pi";
        public const string IconPilgrims = "icon.pilgrims";
        public const string IconPirates = "icon.pirates";
        public const string IconPlating = "icon.plating";
        public const string IconPolitics = "icon.politics";
        public const string IconPopulation = "icon.population";
        public const string IconPositive = "icon.positive";
        public const string IconPressure = "icon.pressure";
        public const string IconPrivateers = "icon.privateers";
        public const string IconProtoOrchid = "icon.proto-orchid";
        public const string IconProtoSpores = "icon.proto-spores";
        public const string IconPulsos = "icon.pulsos";
        public const string IconQuadrantEconomy = "icon.quadrant-economy";
        public const string IconQuadrantEmpire = "icon.quadrant-empire";
        public const string IconQuadrantMilitary = "icon.quadrant-military";
        public const string IconQuadrantScience = "icon.quadrant-science";
        public const string IconQuadrinix = "icon.quadrinix";
        public const string IconQuest = "icon.quest";
        public const string IconRandom = "icon.random";
        public const string IconRedsang = "icon.redsang";
        public const string IconRelic = "icon.relic";
        public const string IconReligious = "icon.religious";
        public const string IconRemnant = "icon.remnant";
        public const string IconReward = "icon.reward";
        public const string IconRiftborn = "icon.riftborn";
        public const string IconRightClick = "icon.right-click";
        public const string IconRole = "icon.role";
        public const string IconScavengers = "icon.scavengers";
        public const string IconScience = "icon.science";
        public const string IconScientists = "icon.scientists";
        public const string IconSeeker = "icon.seeker";
        public const string IconSefaloros = "icon.sefaloros";
        public const string IconSheredyn = "icon.sheredyn";
        public const string IconShield = "icon.shield";
        public const string IconShip = "icon.ship";
        public const string IconShipManpower = "icon.ship-manpower";
        public const string IconShortRange = "icon.short-range";
        public const string IconSistersOfMercy = "icon.sisters-of-mercy";
        public const string IconSize = "icon.size";
        public const string IconSophons = "icon.sophons";
        public const string IconSowers = "icon.sowers";
        public const string IconSquareRoot = "icon.square-root";
        public const string IconStarSystem = "icon.star-system";
        public const string IconSterile = "icon.sterile";
        public const string IconStrategicResource = "icon.strategic-resource";
        public const string IconSum = "icon.sum";
        public const string IconSuperspuds = "icon.superspuds";
        public const string IconSupport = "icon.support";
        public const string IconSystemGarrison = "icon.system-garrison";
        public const string IconTechnologists = "icon.technologists";
        public const string IconTechnology = "icon.technology";
        public const string IconTemperate = "icon.temperate";
        public const string IconTerraformation = "icon.terraformation";
        public const string IconThisTurn = "icon.this-turn";
        public const string IconTikanan = "icon.tikanan";
        public const string IconTitanium = "icon.titanium";
        public const string IconTradeEfficiency = "icon.trade-efficiency";
        public const string IconTraitor = "icon.traitor";
        public const string IconTransmigrationBeacon = "icon.transmigration-beacon";
        public const string IconTransvine = "icon.transvine";
        public const string IconTurn = "icon.turn";
        public const string IconUmbralChoir = "icon.umbral-choir";
        public const string IconUnfallen = "icon.unfallen";
        public const string IconUnlimited = "icon.unlimited";
        public const string IconUpkeep = "icon.upkeep";
        public const string IconVaulters = "icon.vaulters";
        public const string IconVictory = "icon.victory";
        public const string IconVirtualArtifacts = "icon.virtual-artifacts";
        public const string IconVodyani = "icon.vodyani";
        public const string IconVoidStone = "icon.void-stone";
        public const string IconWarBanners = "icon.war-banners";
        public const string IconWarMomentum = "icon.war-momentum";
        public const string IconWarPoint = "icon.war-point";
        public const string IconWayOfTheObsidianEagle = "icon.way-of-the-obsidian-eagle";
        public const string IconWonder = "icon.wonder";
        public const string IconXenos = "icon.xenos";
        public const string IconXirmisala = "icon.xirmisala";
        public const string IconYuusho = "icon.yuusho";
        public const string IconZvali = "icon.zvali";

        private static readonly Dictionary<string, string> IconDefaults = new Dictionary<
            string,
            string
        >
        {
            { IconAcademy, "Academy" },
            { IconActionPoint, "Action Point" },
            { IconAdamantian, "Adamantian" },
            { IconAffinity, "Affinity" },
            { IconAir, "Air" },
            { IconAmblyr, "Amblyr" },
            { IconAmianthoid, "Amianthoid" },
            { IconAmoeba, "Amoeba" },
            { IconAnomaly, "Anomaly" },
            { IconAntimatter, "Antimatter" },
            { IconApproval, "Approval" },
            { IconArmor, "Armor" },
            { IconAttacker, "Attacker" },
            { IconBackdoor, "Backdoor" },
            { IconBandwidth, "Bandwidth" },
            { IconBasryxo, "Basryxo" },
            { IconBehemoth, "Behemoth" },
            { IconBenthicGems, "Benthic Gems" },
            { IconBereaved, "Bereaved" },
            { IconBhagaba, "Bhagaba" },
            { IconBluecapMold, "Bluecap Mold" },
            { IconBoardingPod, "Boarding Pod" },
            { IconBomber, "Bomber" },
            { IconBranch, "Branch" },
            { IconBuyout, "Buyout" },
            { IconCarrier, "Carrier" },
            { IconCatalyst, "Catalyst" },
            { IconCitadel, "Citadel" },
            { IconCitadelGarrison, "Citadel Garrison" },
            { IconCold, "Cold" },
            { IconColonization, "Colonization" },
            { IconColonizer, "Colonizer" },
            { IconCommandPoint, "Command Point" },
            { IconCounselor, "Counselor" },
            { IconCravers, "Cravers" },
            { IconCrown, "Crown" },
            { IconCryogenicColonizer, "Cryogenic Colonizer" },
            { IconCuriosity, "Curiosity" },
            { IconCustomPopulation00, "Custom population 1" },
            { IconCustomPopulation01, "Custom population 2" },
            { IconCustomPopulation02, "Custom population 3" },
            { IconCustomPopulation03, "Custom population 4" },
            { IconCustomPopulation04, "Custom population 5" },
            { IconCustomPopulation05, "Custom population 6" },
            { IconCustomPopulation06, "Custom population 7" },
            { IconCustomPopulation07, "Custom population 8" },
            { IconCustomPopulation08, "Custom population 9" },
            { IconCustomPopulation09, "Custom population 10" },
            { IconCustomPopulation10, "Custom population 11" },
            { IconCustomPopulation11, "Custom population 12" },
            { IconDamageDealt, "Damage Dealt" },
            { IconDamageTaken, "Damage Taken" },
            { IconDarkGlitter, "Dark Glitter" },
            { IconDefeat, "Defeat" },
            { IconDefender, "Defender" },
            { IconDefensivePower, "Defensive Power" },
            { IconDefensiveProgram, "Defensive Program" },
            { IconDelta, "delta" },
            { IconDeuyivans, "Deuyivans" },
            { IconDiplomacyAggressive, "Aggressive" },
            { IconDiplomacyAlliance, "Alliance" },
            { IconDiplomacyAssimilated, "Assimilated" },
            { IconDiplomacyColdWar, "Cold War" },
            { IconDiplomacyFriendly, "Friendly" },
            { IconDiplomacyNeutral, "Neutral" },
            { IconDiplomacyPartner, "Partner" },
            { IconDiplomacyPeace, "Peace" },
            { IconDiplomacyTruce, "Truce" },
            { IconDiplomacyUnknown, "Unknown" },
            { IconDiplomacyWar, "War" },
            { IconDisclaimer, "Disclaimer" },
            { IconDriftBuds, "Drift Buds" },
            { IconDuel, "Duel" },
            { IconDust, "Dust" },
            { IconDustciduousTrees, "Dustciduous Trees" },
            { IconDustwater, "DustWater" },
            { IconEcologists, "Ecologists" },
            { IconEdenIncense, "Eden Incense" },
            { IconEfficiency, "Efficiency" },
            { IconEndlessFoundries, "Endless Foundries" },
            { IconEpistis, "Epistis" },
            { IconEssence, "Essence" },
            { IconEssenceUpkeep, "Essence upkeep" },
            { IconEusocials, "Eusocials" },
            { IconEvent, "Event" },
            { IconExploration, "Exploration" },
            { IconEyder, "Eyder" },
            { IconFertile, "Fertile" },
            { IconFids, "FIDS" },
            { IconFidsi, "FIDSI" },
            { IconFighter, "Fighter" },
            { IconFood, "Food" },
            { IconFoundlings, "Foundlings" },
            { IconGalvran, "Galvran" },
            { IconGas, "Gas" },
            { IconGigaLattice, "Giga Lattice" },
            { IconGnashast, "Gnashast" },
            { IconGoldenAge, "Golden Age" },
            { IconGossamer, "Gossamer" },
            { IconGrowth, "Growth" },
            { IconGuardian, "Guardian" },
            { IconGuardianGuides, "Guardian Guides" },
            { IconGuardians, "Guardians" },
            { IconHacking, "Hacking" },
            { IconHackingSpeed, "Hacking Speed" },
            { IconHarmony, "Harmony" },
            { IconHaroshems, "Haroshems" },
            { IconHeadquarters, "Headquarters" },
            { IconHealth, "Health" },
            { IconHero, "Hero" },
            { IconHeroClass, "Class" },
            { IconHissho, "Hissho" },
            { IconHomelandDefense, "Homeland Defense" },
            { IconHoratio, "Horatio" },
            { IconHot, "Hot" },
            { IconHydromiel, "Hydromiel" },
            { IconHyperium, "Hyperium" },
            { IconIllo, "Illo" },
            { IconImperials, "Imperials" },
            { IconImprovement, "Improvement" },
            { IconIndustrialists, "Industrialists" },
            { IconIndustry, "Industry" },
            { IconInfantry, "Infantry" },
            { IconInfluence, "Influence" },
            { IconInvisibility, "Invisibility" },
            { IconIonicCrystal, "Ionic Crystal" },
            { IconJadonyx, "Jadonyx" },
            { IconJuggernaut, "Juggernaut" },
            { IconKalgeros, "Kalgeros" },
            { IconKalmat, "Kalmat" },
            { IconKaltikma, "Kal'Tik'Ma" },
            { IconKamikaze, "Kamikaze" },
            { IconKeii, "Keii" },
            { IconLeftClick, "left click" },
            { IconLetterA, "A" },
            { IconLetterB, "B" },
            { IconLetterC, "C" },
            { IconLetterD, "D" },
            { IconLetterE, "E" },
            { IconLetterF, "F" },
            { IconLetterG, "G" },
            { IconLetterH, "H" },
            { IconLetterI, "I" },
            { IconLetterJ, "J" },
            { IconLetterK, "K" },
            { IconLetterL, "L" },
            { IconLetterM, "M" },
            { IconLetterN, "N" },
            { IconLetterO, "O" },
            { IconLetterP, "P" },
            { IconLetterQ, "Q" },
            { IconLetterR, "R" },
            { IconLetterS, "S" },
            { IconLetterT, "T" },
            { IconLetterU, "U" },
            { IconLetterV, "V" },
            { IconLetterW, "W" },
            { IconLetterX, "X" },
            { IconLetterY, "Y" },
            { IconLetterZ, "Z" },
            { IconLevel, "Level" },
            { IconLongRange, "long range" },
            { IconLostCities, "Lost Cities" },
            { IconLumeris, "Lumeris" },
            { IconLuxuryResource, "Luxury Resource" },
            { IconManpower, "Manpower" },
            { IconMasteryCommand, "Command" },
            { IconMasteryLabor, "Labor" },
            { IconMasteryWanderlust, "Wanderlust" },
            { IconMasteryWit, "Wit" },
            { IconMavros, "Mavros" },
            { IconMedal, "Medal" },
            { IconMediumRange, "medium range" },
            { IconMercenaries, "Mercenaries" },
            { IconMercurite, "Mercurite" },
            { IconMetaEntactogen, "Meta-Entactogen" },
            { IconMezari, "Mezari" },
            { IconMilitarists, "Militarists" },
            { IconMinus, "minus" },
            { IconMothership, "Mothership" },
            { IconMovement, "Movement" },
            { IconNakalim, "Nakalim" },
            { IconNegative, "negative" },
            { IconNiris, "Niris" },
            { IconObedience, "Obedience" },
            { IconObliterator, "Obliterator" },
            { IconOffensivePower, "Offensive Power" },
            { IconOffensiveProgram, "Offensive Program" },
            { IconOhm, "ohms" },
            { IconOrderOfTheRedBlade, "Order of the Red Blade" },
            { IconOrichalcix, "Orichalcix" },
            { IconOvercolonization, "Over Colonization" },
            { IconOverpopulation, "Overpopulation" },
            { IconOverseer, "Overseer" },
            { IconPacifists, "Pacifists" },
            { IconPartialDerivative, "partial derivative" },
            { IconPi, "pi" },
            { IconPilgrims, "Pilgrims" },
            { IconPirates, "Pirates" },
            { IconPlating, "Plating" },
            { IconPolitics, "Politics" },
            { IconPopulation, "Population" },
            { IconPositive, "positive" },
            { IconPressure, "Pressure" },
            { IconPrivateers, "Privateers" },
            { IconProtoOrchid, "Proto-Orchid" },
            { IconProtoSpores, "Proto-spores" },
            { IconPulsos, "Pulsos" },
            { IconQuadrantEconomy, "Economy and Trade" },
            { IconQuadrantEmpire, "Empire Development" },
            { IconQuadrantMilitary, "Military" },
            { IconQuadrantScience, "Science and Exploration" },
            { IconQuadrinix, "Quadrinix" },
            { IconQuest, "Quest" },
            { IconRandom, "Random" },
            { IconRedsang, "RedSang" },
            { IconRelic, "Relic" },
            { IconReligious, "Religious" },
            { IconRemnant, "Remnant" },
            { IconReward, "Reward" },
            { IconRiftborn, "Riftborn" },
            { IconRightClick, "right click" },
            { IconRole, "Role" },
            { IconScavengers, "Scavengers" },
            { IconScience, "Science" },
            { IconScientists, "Scientists" },
            { IconSeeker, "Seeker" },
            { IconSefaloros, "Sefaloros" },
            { IconSheredyn, "Sheredyn" },
            { IconShield, "Shield" },
            { IconShip, "Ship" },
            { IconShipManpower, "Ship Manpower" },
            { IconShortRange, "short range" },
            { IconSistersOfMercy, "Sisters of Mercy" },
            { IconSize, "Size" },
            { IconSophons, "Sophons" },
            { IconSowers, "Sowers" },
            { IconSquareRoot, "square root" },
            { IconStarSystem, "Star System" },
            { IconSterile, "Sterile" },
            { IconStrategicResource, "Strategic Resource" },
            { IconSum, "sum" },
            { IconSuperspuds, "Superspuds" },
            { IconSupport, "Support" },
            { IconSystemGarrison, "System Garrison" },
            { IconTechnologists, "Technologists" },
            { IconTechnology, "Technology" },
            { IconTemperate, "Temperate" },
            { IconTerraformation, "Terraformation" },
            { IconThisTurn, "This Turn" },
            { IconTikanan, "Tikanan" },
            { IconTitanium, "Titanium" },
            { IconTradeEfficiency, "Trade Efficiency" },
            { IconTraitor, "Sleeper" },
            { IconTransmigrationBeacon, "Transmigration Beacon" },
            { IconTransvine, "Transvine" },
            { IconTurn, "Turn" },
            { IconUmbralChoir, "Umbral Choir" },
            { IconUnfallen, "Unfallen" },
            { IconUnlimited, "Unlimited" },
            { IconUpkeep, "Upkeep" },
            { IconVaulters, "Vaulters" },
            { IconVictory, "Victory" },
            { IconVirtualArtifacts, "Virtual Artifacts" },
            { IconVodyani, "Vodyani" },
            { IconVoidStone, "Void Stone" },
            { IconWarBanners, "War Banners" },
            { IconWarMomentum, "War Momentum" },
            { IconWarPoint, "War Point" },
            { IconWayOfTheObsidianEagle, "Way of the Obsidian Eagle" },
            { IconWonder, "Wonder" },
            { IconXenos, "Xenos" },
            { IconXirmisala, "Xirmisala" },
            { IconYuusho, "Yuusho" },
            { IconZvali, "Z'vali" },
        };
    }
}
