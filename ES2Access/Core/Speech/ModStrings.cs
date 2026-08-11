using System;
using System.Collections.Generic;
using ES2Access.Core.Util;

namespace ES2Access.Core.Speech
{
    /// <summary>
    /// The mod's own spoken strings — everything the mod says that does not come from the game's
    /// text. Two layers: compiled-in English defaults that always exist, and an optional overlay
    /// installed from a translation file. A missing or broken translation therefore degrades to
    /// English instead of to silence or a crash.
    ///
    /// Deliberately BCL-only: it holds no file, engine or JSON knowledge, so <see cref="Core"/>
    /// stays testable offline. The engine side (ModLocale) reads the game's language and calls
    /// <see cref="Install"/>.
    ///
    /// Main-thread only; there is no locking. Speech is composed on the Unity main thread.
    /// </summary>
    public static partial class ModStrings
    {
        public const string StartupReady = "startup.ready";
        public const string FragmentSeparator = "speech.fragment-separator";
        public const string ListSeparator = "speech.list-separator";
        public const string Fraction = "speech.fraction";
        public const string FractionUnit = "speech.fraction-unit";
        public const string Quantity = "speech.quantity";

        // The role words that say what kind of control the player is on.
        public const string ControlButton = "control.button";
        public const string ControlGroup = "control.group";
        public const string ControlTab = "control.tab";
        public const string ControlCheckbox = "control.checkbox";
        public const string ControlSlider = "control.slider";
        public const string ControlComboBox = "control.combo-box";
        public const string ControlEditField = "control.edit-field";
        public const string ControlMenuItem = "control.menu-item";

        // One of a set where exactly one is in force. Not a checkbox: activating it can only ever
        // make it the chosen one, and the box the player would expect to untick does not exist.
        public const string ControlRadioButton = "control.radio-button";

        // What navigation says about a control beyond its own text. Each is a whole phrase: a
        // language that negates with more than a leading word has somewhere to put it.
        public const string NavExpanded = "nav.expanded";
        public const string NavCollapsed = "nav.collapsed";
        public const string NavChecked = "nav.checked";
        public const string NavUnchecked = "nav.unchecked";
        public const string NavSelected = "nav.selected";

        /// <summary>The other half of <see cref="NavSelected"/>, and only for a list the player picks
        /// SEVERAL things out of: there, membership is the thing being read and its absence has to be
        /// audible. A group where exactly one is in force (a tab bar, a radio group) still says
        /// nothing about the ones that are not.</summary>
        public const string NavNotSelected = "nav.not-selected";

        public const string NavDisabled = "nav.disabled";
        public const string NavHasTooltip = "nav.has-tooltip";
        public const string NavNoDetails = "nav.no-details";
        public const string NavTable = "nav.table";

        // Typing letters on a screen searches what is on it. Both are whole phrases: the text the
        // player typed is quoted inside the sentence, so a language that frames a quotation
        // differently has somewhere to do it.
        public const string SearchNoMatch = "search.no-match";
        public const string SearchCleared = "search.cleared";

        // A key-binding row: the keys it is on, and the prompt while it is waiting for new ones.
        public const string NavKeyBindingSecondary = "nav.key-binding-secondary";
        public const string NavNotBound = "nav.not-bound";
        public const string NavPressPrimaryKey = "nav.press-primary-key";
        public const string NavPressSecondaryKey = "nav.press-secondary-key";

        // A tooltip drew a bar split between two things and wrote no number on it. The bar's own
        // caption names the two sides in this order, so the proportions follow it unnamed.
        public const string TooltipBalance = "tooltip.balance";

        // The review buffers - the text the player walks line by line.
        public const string BufferUi = "buffer.ui";
        public const string BufferEmpty = "buffer.empty";
        public const string BufferLine = "buffer.line";

        // Screen names, spoken on arrival.
        public const string ScreenMainMenu = "screen.main-menu";
        public const string ScreenMessageBox = "screen.message-box";
        public const string ScreenOptions = "screen.options";
        public const string ScreenLoading = "screen.loading";
        public const string ScreenNotification = "screen.notification";
        public const string ScreenTutorial = "screen.tutorial";
        public const string ScreenGalaxy = "screen.galaxy";
        public const string ScreenGameMenu = "screen.game-menu";
        public const string ScreenLoadSave = "screen.load-save";

        // How far a load has got, said at the quarter marks.
        public const string LoadingProgress = "loading.progress";

        // The controls every notification popup carries. The game draws them as icons and names
        // only some of them, so the unnamed ones are named here.
        public const string NotifyDismiss = "notify.dismiss";
        public const string NotifyMinimize = "notify.minimize";
        public const string NotifyShowLocation = "notify.show-location";
        public const string NotifyNext = "notify.next";
        public const string NotifyPrevious = "notify.previous";
        public const string NotifyAutoPopup = "notify.auto-popup";

        // The tutorial popup's own controls, and the name of one of the dots it marks its pages
        // with - the game draws them as dots and writes nothing on them.
        public const string TutorialPreviousPage = "tutorial.previous-page";
        public const string TutorialNextPage = "tutorial.next-page";
        public const string TutorialMinimize = "tutorial.minimize";
        public const string TutorialShowLocation = "tutorial.show-location";
        public const string TutorialClose = "tutorial.close";
        public const string TutorialPageMark = "tutorial.page-mark";

        // The galaxy: the controls the game draws as icons and never names, and the shapes its
        // numbers are spoken in.
        public const string GalaxyTurn = "galaxy.turn";
        public const string GalaxyEndTurn = "galaxy.end-turn";
        public const string GalaxyApplyMovements = "galaxy.apply-movements";
        public const string GalaxyNextIdleFleet = "galaxy.next-idle-fleet";
        public const string GalaxyIdleFleets = "galaxy.idle-fleets";
        public const string GalaxyGameMenu = "galaxy.game-menu";
        public const string GalaxyResearch = "galaxy.research";
        public const string GalaxyTurnsRemaining = "galaxy.turns-remaining";
        public const string GalaxyStockAndNet = "galaxy.stock-and-net";
        public const string GalaxySystemColonized = "galaxy.system-colonized";

        // A system of the player's that is still an OUTPOST rather than a colony - a distinct label on
        // the map, so a distinct word here.
        public const string GalaxySystemOutpost = "galaxy.system-outpost";
        public const string GalaxyOpenSystem = "galaxy.open-system";

        // What the camera just did, said back because the player cannot see it move. The game has no
        // words of its own for the pair: what it does write about zooming is the titles of its two
        // camera KEY BINDINGS ("Zoom in (Galaxy)", "Zoom out (Galaxy)"), which name a key rather than
        // report a change, so these are the mod's.
        public const string GalaxyZoomedIn = "galaxy.zoomed-in";
        public const string GalaxyZoomedOut = "galaxy.zoomed-out";

        // The lanes out of a system. The game draws these as lines and writes nothing on them, and
        // where a line runs off into space it has no destination to name either. The number is the
        // lane's place going clockwise from north, which is also the order they are walked in; the
        // last slot is the compass word for the way the line leaves.
        public const string GalaxyStarlane = "galaxy.starlane";
        public const string GalaxyStarlaneUnexplored = "galaxy.starlane-unexplored";
        public const string GalaxyWormhole = "galaxy.wormhole";
        public const string GalaxyWormholeUnexplored = "galaxy.wormhole-unexplored";

        // The place a lane leads to, offered inside the lane itself. The game draws no such control - a
        // line on the map is the whole of what it draws - so the phrase is the mod's, and it is a whole
        // phrase because the name of a system is all that goes in it.
        public const string GalaxyLaneDestination = "galaxy.lane-destination";

        // What the map writes on a system's own label. The numbers are the game's; the words around
        // them are the mod's, because the label draws every one of them as a bare figure beside a
        // picture and names none of them anywhere on itself. "Sleepers" is the game's own word for the
        // agents it counts, taken from the sentence it explains the count with.
        public const string GalaxySystemPopulation = "galaxy.system-population";
        public const string GalaxySystemSleepers = "galaxy.system-sleepers";
        public const string GalaxySystemBuilding = "galaxy.system-building";
        public const string GalaxySystemNothingBuilding = "galaxy.system-nothing-building";
        public const string GalaxySystemRebellion = "galaxy.system-rebellion";
        public const string GalaxySystemPiratePower = "galaxy.system-pirate-power";
        public const string GalaxySystemMinorRelation = "galaxy.system-minor-relation";

        // The stretch of sky a system stands in. The map writes the constellation's name across the
        // region rather than on anything in it, and the tree has no level for a region, so the name is
        // said on the system and the sentence around it is the mod's.
        public const string GalaxySystemConstellation = "galaxy.system-constellation";

        // The one thing the map draws in open space that the game names nowhere: the missile an
        // obliterator fires at a star system. Everything else about it - where it is going and when it
        // arrives - is the game's own sentence, which arrives with the node as its tooltip.
        public const string GalaxyObliteratorProjectile = "galaxy.obliterator-projectile";

        // The buttons the label draws on a system. Each is a wordless icon the game explains in a
        // sentence rather than names - the sentence arrives with the node as its tooltip - so what to
        // CALL each one is the mod's, kept as short as the thing it does.
        public const string GalaxySystemDiplomacy = "galaxy.system-diplomacy";
        public const string GalaxySystemPirateMarkBuyout = "galaxy.system-pirate-mark-buyout";
        public const string GalaxySystemConversionBuyout = "galaxy.system-conversion-buyout";
        public const string GalaxySystemAcademyBuyout = "galaxy.system-academy-buyout";
        public const string GalaxySystemHackingBeacon = "galaxy.system-hacking-beacon";

        // Which way something on the map lies from somewhere else. The game writes no compass word
        // anywhere - it draws the map and expects it to be looked at - so all eight are the mod's.
        public const string DirectionNorth = "direction.north";
        public const string DirectionNorthEast = "direction.northeast";
        public const string DirectionEast = "direction.east";
        public const string DirectionSouthEast = "direction.southeast";
        public const string DirectionSouth = "direction.south";
        public const string DirectionSouthWest = "direction.southwest";
        public const string DirectionWest = "direction.west";
        public const string DirectionNorthWest = "direction.northwest";
        public const string GalaxyFleetShips = "galaxy.fleet-ships";
        public const string GalaxyFleetMoving = "galaxy.fleet-moving";
        public const string GalaxyFleetMovement = "galaxy.fleet-movement";

        // Where a fleet is and what it is doing. The game draws this as an icon beside the name of a
        // system - orbiting one, or on its way to one - and writes no word for either state, so the
        // sentence is the mod's and the system in it is the game's. A destination the map has not
        // named is left unnamed here too, the same way a starlane into the dark is.
        public const string GalaxyFleetDockedAt = "galaxy.fleet-docked-at";
        public const string GalaxyFleetDocked = "galaxy.fleet-docked";
        public const string GalaxyFleetMovingTo = "galaxy.fleet-moving-to";
        public const string GalaxyFleetMovingUnexplored = "galaxy.fleet-moving-unexplored";
        public const string GalaxyFleetGuarding = "galaxy.fleet-guarding";

        // What a fleet on the map can be told to do, and what a place on the map offers the fleets that
        // are selected. Moving is a drag in the game and has no button anywhere, so none of these has a
        // name in the game's own words.
        public const string GalaxySendFleet = "galaxy.send-fleet";
        public const string GalaxySendFleets = "galaxy.send-fleets";

        // The pinned quest the game draws in the top right corner. Its title, its status and its
        // objective are the game's own words; what a player can DO with the panel is not written
        // anywhere on it - the game draws two of the three as bare icons and the third as a click on
        // the panel itself - so those three are named here. The two announcements are the whole
        // sentence rather than a word glued to one: which quest is being tracked changes without the
        // player standing anywhere near the panel.
        public const string HudQuestShowLocation = "hud.quest-show-location";
        public const string HudQuestUnpin = "hud.quest-unpin";
        public const string HudQuestPinned = "hud.quest-pinned";
        public const string HudQuestUnpinned = "hud.quest-unpinned";

        // The pause menu's icon-only toggle, and the word for a settings panel the game will only
        // show, not let you change.
        public const string GameMenuGameSettings = "gamemenu.game-settings";
        public const string GameMenuReadOnlySettings = "gamemenu.read-only-settings";

        // The save page: the name field, the prompt for typing into it, the cloud toggle, and what an
        // empty cell of the save table says.
        public const string LoadSaveSaveName = "loadsave.save-name";
        public const string LoadSaveEditName = "loadsave.edit-name";
        public const string LoadSaveCloud = "loadsave.cloud";
        public const string NavCellEmpty = "nav.cell-empty";

        // Picking something up and putting it down somewhere else (a ship into another fleet). The
        // words are the DRAG's, because that is the gesture these keys stand in for and the one the
        // game's own tooltips name. The carried thing is named in the mod's sentence but in the game's
        // own words, and each of these is a whole phrase so a language that frames "dragging X"
        // differently has somewhere to do it. Ending a drag without moving anything - the back key,
        // or putting the thing back where it came from - is one phrase and names nothing, because
        // nothing happened to name. A refusal normally speaks the GAME's reason instead; the one here
        // is the fallback for a check that refuses wordlessly.
        public const string CarryCarrying = "carry.carrying";
        public const string CarryDropped = "carry.dropped";
        public const string CarryDropRefused = "carry.drop-refused";
        public const string CarryCancelled = "carry.cancelled";

        /// <summary>What a queue line says when the thing that was carried lands on it: which item
        /// moved, and the position number the player will hear the line read back with.</summary>
        public const string CarryMovedToPosition = "carry.moved-to-position";

        /// <summary>What a control says while it would take the thing the player is holding.</summary>
        public const string CarryDropTarget = "carry.drop-target";

        // The scan view. The lens names itself in the game's own words, so the only strings here are
        // for the two boxes the game draws bare and the pairing of a number with the output it counts.
        public const string ScreenScanView = "screen.scan-view";
        public const string ScanSystemInfo = "scan.system-info";
        public const string ScanLegend = "scan.legend";
        public const string ScanOutput = "scan.output";

        // The star system management page. The panel names are the mod's, because the game draws the
        // panels as unlabelled boxes with an icon in the corner; everything a panel CONTAINS is read
        // in the game's own words.
        public const string ScreenStarSystem = "screen.star-system";
        public const string ScreenPlanet = "screen.planet";
        public const string SystemPlanetsPanel = "system.planets-panel";
        public const string SystemColonyPanel = "system.colony-panel";
        public const string SystemPopulationPanel = "system.population-panel";
        public const string SystemRepresentativesPanel = "system.representatives-panel";
        public const string SystemOutpostPanel = "system.outpost-panel";
        public const string SystemGovernorPanel = "system.governor-panel";
        public const string SystemConstructiblesPanel = "system.constructibles-panel";
        public const string SystemQueuePanel = "system.queue-panel";
        public const string SystemHangarPanel = "system.hangar-panel";

        // The buttons the page's cards and queue lines draw as bare icons, which the game names
        // nowhere, and what the mod says about an action it carried out.
        public const string SystemColonize = "system.colonize";
        public const string SystemRenamePlanet = "system.rename-planet";
        public const string SystemRenameSystem = "system.rename-system";
        public const string SystemPopulationMoved = "system.population-moved";
        public const string SystemImprovements = "system.improvements";
        public const string SystemLevel = "system.level";
        public const string SystemSecurity = "system.security";

        /// <summary>How many outposts this colony is feeding part of its growth to. The population
        /// panel draws the number alone beside a symbol and says what it means nowhere but on the row's
        /// tooltip, so the row's own name is the mod's - and it is a COUNTED phrase, hence a form per
        /// number (see <see cref="Plural"/>) rather than a count glued to a noun.</summary>
        public const string SystemSupplyingOutpost = "system.supplying-outpost";
        public const string SystemSupplyingOutposts = "system.supplying-outposts";

        // The construction queue: what a buy-out button on a line is called, and what a line says
        // about where it is and how far along it is.
        public const string SystemBuyOut = "system.buy-out";
        public const string SystemQueuePosition = "system.queue-position";
        public const string SystemProgress = "system.progress";
        public const string SystemIndustryCost = "system.industry-cost";
        public const string SystemShipSelected = "system.ship-selected";

        // The panel the game puts up while a fleet is selected. Its three panels draw no headings of
        // their own, so these name them; everything inside them is in the game's own words - including
        // the ship toolbar, which the game names under the action each button carries out. The panel is
        // no page of its own, so its coming and going is announced rather than heard as a screen
        // change: three tab stops appear and disappear under the player.
        public const string FleetsPanelOpened = "fleets.panel-opened";
        public const string FleetsPanelClosed = "fleets.panel-closed";
        public const string FleetsActionsPanel = "fleets.actions-panel";
        public const string FleetsFleetsPanel = "fleets.fleets-panel";
        public const string FleetsHeroPanel = "fleets.hero-panel";
        public const string FleetsShipsPanel = "fleets.ships-panel";

        /// <summary>What a range selection did, as one sentence: how many ships are picked out now and
        /// which two ends of the run they are. A range is the one selection gesture whose result cannot
        /// be heard from the row it was made on - every OTHER row changed too - so the outcome is said
        /// rather than the row's own new state.</summary>
        public const string FleetsShipsRange = "fleets.ships-range";

        /// <summary>A ship carried out of one fleet and put down in another. The mod's own sentence:
        /// the game says nothing when a drag completes, it simply redraws.</summary>
        public const string FleetsShipMoved = "fleets.ship-moved";

        // The first-visit cutscene. It draws no heading of its own, so the mod says which system is
        // being shown; everything about each planet comes from the card in the game's own words.
        public const string ScreenSystemDiscovery = "screen.system-discovery";
        public const string DiscoverySystem = "discovery.system";

        /// <summary>The rename box the game opens over a page: what it is, and that typing has begun.
        /// </summary>
        public const string ScreenRename = "screen.rename";
        public const string RenameTypePrompt = "rename.type-prompt";
        public const string RenameConfirm = "rename.confirm";

        /// <summary>What the box says when the field lets go of the keyboard without the box closing -
        /// the first of the two Escapes it takes to get out of here. Nothing on screen changes, so
        /// without this the player is left pressing keys at a box that has gone quiet.</summary>
        public const string RenameKeyboardReturned = "rename.keyboard-returned";

        // The research screen - the wheel of technologies the game draws over the galaxy. The three
        // panel names are the mod's, because the game labels none of them; the quadrants, the stages
        // and every technology on them are named by the game.
        public const string ScreenResearch = "screen.research";
        public const string ResearchStatusPanel = "research.status-panel";
        public const string ResearchKeyPanel = "research.key-panel";
        public const string ResearchTreePanel = "research.tree-panel";

        /// <summary>How much of a quadrant or a stage is done, over the technologies the game draws.
        /// </summary>
        public const string ResearchCounts = "research.counts";

        /// <summary>Where a technology sits in the research queue.</summary>
        public const string ResearchQueuePosition = "research.queue-position";

        // The arcs the wheel draws between two technologies. Each is a whole sentence because each
        // says something different depending on which end of the arc the player is standing on.
        public const string ResearchLinkReduces = "research.link-reduces";
        public const string ResearchLinkReducedBy = "research.link-reduced-by";
        public const string ResearchLinkExclusive = "research.link-exclusive";
        public const string ResearchLinkUnlocks = "research.link-unlocks";
        public const string ResearchLinkUnlockedBy = "research.link-unlocked-by";

        /// <summary>Who got to a deed first, once the game has ruled the player out of it. The deed
        /// itself, its four states and the word "Deed" are all the game's; only the sentence that
        /// puts an empire's name to a failure is the mod's, because the game draws it as a logo.
        /// </summary>
        public const string ResearchDeedWinner = "research.deed-winner";

        // The quest journal - the strip of quest cards the game draws over the galaxy. Its own heading
        // is the game's and is what is normally spoken; the name here is the fallback for the frames
        // before the window has written it. "Pinned" is the mod's word for a state the game draws only
        // as a lit pin, and it is the same word the HUD's pinned-quest announcements already use.
        public const string ScreenQuests = "screen.quests";
        public const string QuestsPinned = "quests.pinned";

        // The senate and the three windows it opens. The screens' own names are the game's and are
        // what is normally spoken; the names here are the fallback for the frames before a window has
        // written its heading. The three side panels and the two empty law-slot states are the mod's
        // words, because the game draws those boxes and slots with no words on them at all - a corner
        // icon and a sentence on hover for the panels, a bare picture for the slots.
        public const string ScreenSenate = "screen.senate";
        public const string SenateGovernmentPanel = "senate.government-panel";
        public const string SenateElectionPanel = "senate.election-panel";
        public const string SenateSupportPanel = "senate.support-panel";
        public const string SenateEmptyLawSlot = "senate.empty-law-slot";
        public const string SenateLockedLawSlot = "senate.locked-law-slot";
        public const string SenateEmptySenatorSlot = "senate.empty-senator-slot";
        public const string SenateLockedSenatorSlot = "senate.locked-senator-slot";
        // The empire summary. The screen's own name is the game's and is what is normally spoken; the
        // name here is the fallback for the frames before the window has written its heading. The two
        // mod words are for boxes the game draws with no heading at all: the panel holding the empire's
        // portrait and description, and the band of column headings over the systems table.
        public const string ScreenEmpire = "screen.empire";
        public const string EmpireDescriptionPanel = "empire.description-panel";
        public const string EmpireHeadingsBand = "empire.headings-band";

        // The economy screen. The screen's own name is the game's; the name here is the fallback for
        // the frames before the window has written its heading. The rest are for boxes the game draws
        // with no heading at all - the marketplace's four panels and its two tickers - and for the one
        // number the marketplace changes without ever captioning it, the quantity being traded.
        public const string ScreenEconomy = "screen.economy";
        public const string EconomyQuantity = "economy.quantity";
        public const string EconomyBuyPanel = "economy.buy-panel";
        public const string EconomySellPanel = "economy.sell-panel";
        public const string EconomyTaxesPanel = "economy.taxes-panel";
        public const string EconomyLogPanel = "economy.log-panel";
        public const string EconomyAdsPanel = "economy.ads-panel";
        public const string EconomyEventsPanel = "economy.events-panel";
        public const string EconomyInflationPanel = "economy.inflation-panel";
        public const string EconomyTradePanel = "economy.trade-panel";

        // The military screen and the fleet picker the Academy raises over it. Both screen names are
        // fallbacks for the frames before the window has written its heading. The rest are for bands
        // the game draws with no heading of its own: the manpower box down the left edge, the three
        // things that can be done to the selected fleet, the ships of that fleet, and the four things
        // that can be done to the selected ship design.
        public const string ScreenMilitary = "screen.military";
        public const string MilitaryManpowerPanel = "military.manpower-panel";
        public const string MilitaryFleetActions = "military.fleet-actions";
        public const string MilitaryShipsPanel = "military.ships-panel";
        public const string MilitaryDesignActions = "military.design-actions";
        public const string ScreenFleetSelection = "screen.fleet-selection";

        /// <summary>A tactic having landed in a slot of the battle deck. The window's own headings, its
        /// cards and its refusals are all the game's words; a drop is the MOD's drag ending and the game
        /// answers it in silence, so this is the one phrase the deck editor needs.</summary>
        public const string TacticsSlotFilled = "tactics.slot-filled";

        // The hero page and the galaxy-wide hero list its Academy box opens. Both screen names are
        // fallbacks for the frames before the window has written its own heading. The third is for the
        // band of hero actions the page draws along its bottom with no caption of its own.
        public const string ScreenAcademy = "screen.academy";
        public const string AcademyHeroActions = "academy.hero-actions";
        public const string ScreenHeroCompleteList = "screen.hero-complete-list";

        // The ship designer. Its heading is the game's own and is declared where it is drawn, so the
        // only mod words here are for the band the game draws with no caption at all - the module slots
        // it draws over the rendered ship - and for a module having been fitted into one, which is the
        // mod's own drag ending and has no game sentence.
        public const string ShipDesignSlots = "ship-design.slots";
        public const string ShipDesignModuleFitted = "ship-design.module-fitted";

        // The battle popups and the two cinematics. Everything a battle SAYS on screen is the game's
        // own text; what is left here is the counting, the two sides' names and the things the game
        // draws as arcs and bars with no number anywhere on them.
        public const string ScreenBattle = "screen.battle";
        public const string ScreenGroundBattle = "screen.ground-battle";
        public const string ScreenAdvancedBattleReport = "screen.battle-report-advanced";
        public const string BattleYourFleets = "battle.your-fleets";
        public const string BattleEnemyFleets = "battle.enemy-fleets";
        public const string BattleYourTroops = "battle.your-troops";
        public const string BattleEnemyTroops = "battle.enemy-troops";
        public const string BattleYourDamage = "battle.your-damage";
        public const string BattleEnemyDamage = "battle.enemy-damage";
        public const string BattleShowYourFleets = "battle.show-your-fleets";
        public const string BattleShowEnemyFleets = "battle.show-enemy-fleets";
        public const string BattleBalance = "battle.balance";
        public const string BattleTimeLeft = "battle.time-left";
        public const string BattleProgress = "battle.progress";
        public const string BattleYourShipLost = "battle.your-ship-lost";
        public const string BattleYourShipsLost = "battle.your-ships-lost";
        public const string BattleEnemyShipLost = "battle.enemy-ship-lost";
        public const string BattleEnemyShipsLost = "battle.enemy-ships-lost";
        public const string BattleYourFlotillaLost = "battle.your-flotilla-lost";
        public const string BattleYourFlotillasLost = "battle.your-flotillas-lost";
        public const string BattleEnemyFlotillaLost = "battle.enemy-flotilla-lost";
        public const string BattleEnemyFlotillasLost = "battle.enemy-flotillas-lost";
        public const string GroundBattleRound = "ground-battle.round";
        public const string GroundBattleBombardment = "ground-battle.bombardment";
        public const string GroundBattleAssault = "ground-battle.assault";
        public const string GroundBattleYourTroopLost = "ground-battle.your-troop-lost";
        public const string GroundBattleYourTroopsLost = "ground-battle.your-troops-lost";
        public const string GroundBattleEnemyTroopLost = "ground-battle.enemy-troop-lost";
        public const string GroundBattleEnemyTroopsLost = "ground-battle.enemy-troops-lost";

        // A hero's own window. Its heading is the game's own and is declared where it is drawn, so the
        // screen name is only a fallback for the frames before the window has written one. The wheel of
        // skills the page draws with no caption over it needs a word, and a skill dot needs two: the game
        // paints the level a skill stands at, and the level a pending pick would take it to, as a ring of
        // coloured arcs round the dot and writes no number anywhere.
        public const string ScreenHeroInspection = "screen.hero-inspection";
        public const string HeroSkillTrees = "hero.skill-trees";
        public const string HeroSkillLevel = "hero.skill-level";
        public const string HeroSkillPending = "hero.skill-pending";

        public const string ScreenGovernment = "screen.government";
        public const string ScreenLaws = "screen.laws";
        public const string ScreenPopulation = "screen.population";

        /// <summary>The band of buttons a window draws along its bottom edge, and the pane one of these
        /// windows writes the selected thing out in. The game gives neither a heading of its own.
        /// </summary>
        public const string SenateActionsBand = "senate.actions-band";
        public const string SenateDetailPane = "senate.detail-pane";

        /// <summary>The tutorial picker the game raises over a new game. Only ever spoken if the
        /// window has not written its own heading yet; the heading it draws is what is normally
        /// said.</summary>
        public const string ScreenTutorialSelection = "screen.tutorial-selection";

        /// <summary>The single-player lobby. Only ever spoken if the window has not written its own
        /// heading yet; the heading it draws is what is normally said.</summary>
        public const string ScreenNewGame = "screen.new-game";

        /// <summary>The advanced settings a category's Advanced button opens. Only ever spoken if the
        /// window has not written its own heading yet; it normally names the category itself.</summary>
        public const string ScreenAdvancedSettings = "screen.advanced-settings";

        /// <summary>The faction chooser. Only ever spoken if the window has not written its own
        /// heading yet.</summary>
        public const string ScreenFactionChoice = "screen.faction-choice";

        /// <summary>
        /// What an empire colour is called. The game draws its colour lists as bare swatches and names
        /// them only in its palette data, which is not translated anywhere in the corpus - so the
        /// palette's own identifier picks the key and the English default is that identifier written
        /// out. Anything the palette holds that is not listed here is read as the game's word split at
        /// its capitals, so a modded palette still speaks.
        /// </summary>
        internal const string ColorPrefix = "color.";

        private static readonly Dictionary<string, string> Defaults = new Dictionary<string, string>
        {
            { StartupReady, "Endless Space 2 Access ready" },
            { FragmentSeparator, " " },
            { ListSeparator, ", " },
            { Fraction, "{0} of {1}" },
            { FractionUnit, "{0} of {1} {2}" },
            { Quantity, "x {0}" },
            { ControlButton, "button" },
            { ControlGroup, "group" },
            { ControlTab, "tab" },
            { ControlCheckbox, "checkbox" },
            { ControlSlider, "slider" },
            { ControlComboBox, "combo box" },
            { ControlEditField, "edit field" },
            { ControlMenuItem, "menu item" },
            { ControlRadioButton, "radio button" },
            { NavExpanded, "expanded" },
            { NavCollapsed, "collapsed" },
            { NavChecked, "checked" },
            { NavUnchecked, "not checked" },
            { NavSelected, "selected" },
            { NavNotSelected, "not selected" },
            { NavDisabled, "unavailable" },
            { NavHasTooltip, "has tooltip" },
            { NavNoDetails, "Nothing in here" },
            { NavTable, "table" },
            { SearchNoMatch, "No match for {0}" },
            { SearchCleared, "Search cleared" },
            { NavKeyBindingSecondary, "secondary {0}" },
            { NavNotBound, "not bound" },
            { NavPressPrimaryKey, "Press the new key combination." },
            { NavPressSecondaryKey, "Press the new secondary key combination." },
            { TooltipBalance, "{0} to {1}" },
            { BufferUi, "UI" },
            { BufferEmpty, "Buffer empty" },
            { BufferLine, "{0}. {1}" },
            { ScreenMainMenu, "Main menu" },
            { ScreenMessageBox, "Dialog" },
            { ScreenOptions, "Options" },
            { ScreenLoading, "Loading" },
            { ScreenNotification, "Notification" },
            { ScreenTutorial, "Tutorial" },
            { ScreenGalaxy, "Galaxy" },
            { ScreenGameMenu, "Game menu" },
            { ScreenLoadSave, "Load or save" },
            { LoadingProgress, "{0} percent" },
            { NotifyDismiss, "Dismiss" },
            { NotifyMinimize, "Minimize" },
            { NotifyShowLocation, "Show location" },
            { NotifyNext, "Next notification" },
            { NotifyPrevious, "Previous notification" },
            { NotifyAutoPopup, "Pop up automatically" },
            { TutorialPreviousPage, "Previous page" },
            { TutorialNextPage, "Next page" },
            { TutorialMinimize, "Minimize" },
            { TutorialShowLocation, "Show location" },
            { TutorialClose, "Close tutorial" },
            { TutorialPageMark, "Page {0}" },
            { GalaxyTurn, "Turn {0}" },
            { GalaxyEndTurn, "End turn" },
            { GalaxyApplyMovements, "Apply movements" },
            { GalaxyNextIdleFleet, "Next idle fleet" },
            { GalaxyIdleFleets, "{0} idle fleets" },
            { GalaxyGameMenu, "Game menu" },
            { GalaxyResearch, "Research" },
            { GalaxyTurnsRemaining, "{0} turns remaining" },
            { GalaxyStockAndNet, "{0}, {1} per turn" },
            { GalaxySystemColonized, "colonized" },
            { GalaxySystemOutpost, "outpost" },
            { GalaxyOpenSystem, "Open system" },
            { GalaxyZoomedIn, "Zoomed in" },
            { GalaxyZoomedOut, "Zoomed out" },
            { GalaxyStarlane, "Starlane {0} to {1}, {2}" },
            { GalaxyStarlaneUnexplored, "Starlane {0} to an unexplored system, {1}" },
            { GalaxyWormhole, "Wormhole {0} to {1}, {2}" },
            { GalaxyWormholeUnexplored, "Wormhole {0} to an unexplored system, {1}" },
            { GalaxyLaneDestination, "Go to {0}" },
            { GalaxySystemPopulation, "{0} population" },
            { GalaxySystemSleepers, "{0} sleepers" },
            { GalaxySystemBuilding, "Building {0}, {1} turns" },
            { GalaxySystemNothingBuilding, "Nothing under construction" },
            { GalaxySystemRebellion, "Rebellion at {0} percent, {1} to go" },
            { GalaxySystemPiratePower, "Pirate power level {0}" },
            { GalaxySystemMinorRelation, "Relations {0} percent" },
            { GalaxySystemConstellation, "In the {0} constellation" },
            { GalaxyObliteratorProjectile, "Obliterator missile" },
            { GalaxySystemDiplomacy, "Diplomacy" },
            { GalaxySystemPirateMarkBuyout, "Buy out pirate mark" },
            { GalaxySystemConversionBuyout, "Buy out conversion" },
            { GalaxySystemAcademyBuyout, "Buy out academy conversion" },
            { GalaxySystemHackingBeacon, "Displace hacking beacon" },
            { DirectionNorth, "north" },
            { DirectionNorthEast, "northeast" },
            { DirectionEast, "east" },
            { DirectionSouthEast, "southeast" },
            { DirectionSouth, "south" },
            { DirectionSouthWest, "southwest" },
            { DirectionWest, "west" },
            { DirectionNorthWest, "northwest" },
            { GalaxyFleetShips, "{0} ships" },
            { GalaxyFleetMoving, "moving" },
            { GalaxyFleetMovement, "{0} movement points" },
            { GalaxyFleetDockedAt, "Docked at {0}" },
            { GalaxyFleetDocked, "Docked" },
            { GalaxyFleetMovingTo, "Moving to {0}" },
            { GalaxyFleetMovingUnexplored, "Moving to an unexplored system" },
            { GalaxyFleetGuarding, "Guarding" },
            { GalaxySendFleet, "Send fleet {0} here" },
            { GalaxySendFleets, "Send the {0} selected fleets here" },
            { HudQuestShowLocation, "Show location" },
            { HudQuestUnpin, "Unpin quest" },
            { HudQuestPinned, "Pinned quest: {0}" },
            { HudQuestUnpinned, "No quest is pinned" },
            { GameMenuGameSettings, "Game settings" },
            { GameMenuReadOnlySettings, "read only" },
            { LoadSaveSaveName, "Save name" },
            { LoadSaveEditName, "Type the save name, then press Enter." },
            { LoadSaveCloud, "Steam cloud saves" },
            { NavCellEmpty, "empty" },
            { CarryCarrying, "Dragging {0}" },
            { CarryDropped, "Dropped {0}" },
            { CarryDropRefused, "{0} cannot go there" },
            { CarryCancelled, "Cancelled drag" },
            { CarryMovedToPosition, "Moved {0} to position {1}" },
            { CarryDropTarget, "drop target" },
            { ScreenScanView, "Scan view" },
            { ScanSystemInfo, "System information" },
            { ScanLegend, "Legend" },
            { ScanOutput, "{0} {1}" },
            { ScreenStarSystem, "Star system" },
            { ScreenPlanet, "Planet" },
            { SystemPlanetsPanel, "Planets" },
            { SystemColonyPanel, "Colony" },
            { SystemPopulationPanel, "Population" },
            { SystemRepresentativesPanel, "Representatives" },
            { SystemOutpostPanel, "Outpost" },
            { SystemGovernorPanel, "Governor" },
            { SystemConstructiblesPanel, "Available constructions" },
            { SystemQueuePanel, "Construction queue" },
            { SystemHangarPanel, "Hangar" },
            { SystemColonize, "Colonize" },
            { SystemRenamePlanet, "Rename planet" },
            { SystemRenameSystem, "Rename system" },
            { SystemPopulationMoved, "Moved {0} to {1}" },
            { SystemImprovements, "System improvements" },
            { SystemLevel, "System level {0}" },
            { SystemSecurity, "Security" },
            { SystemSupplyingOutpost, "Supplying {0} outpost" },
            { SystemSupplyingOutposts, "Supplying {0} outposts" },

            { SystemBuyOut, "Buy out with {0}" },
            { SystemQueuePosition, "position {0}" },
            { SystemProgress, "{0} percent built" },
            { SystemIndustryCost, "{0} industry" },
            { SystemShipSelected, "selected" },
            { FleetsPanelOpened, "Fleet panel open for {0}" },
            { FleetsPanelClosed, "Fleet panel closed" },
            { FleetsActionsPanel, "Fleet actions" },
            { FleetsFleetsPanel, "Fleets" },
            { FleetsHeroPanel, "Hero" },
            { FleetsShipsPanel, "Ships" },
            { FleetsShipsRange, "{0} ships selected, {1} to {2}" },
            { FleetsShipMoved, "Moved {0} to {1}" },
            { ScreenSystemDiscovery, "System discovery" },
            { DiscoverySystem, "Discovering {0}" },
            { ScreenRename, "Rename" },
            { RenameTypePrompt, "Type the new name, then press Enter." },
            { RenameConfirm, "Confirm" },
            {
                RenameKeyboardReturned,
                "The name box has let go of the keyboard; press Escape again to close it."
            },
            { ScreenResearch, "Research" },
            { ResearchStatusPanel, "Research status" },
            { ResearchKeyPanel, "Research key" },
            { ResearchTreePanel, "Technology tree" },
            { ResearchCounts, "{0} available, {1} researched of {2}" },
            { ResearchQueuePosition, "position {0}" },

            { ResearchLinkReduces, "Reduces the cost of {0}" },
            { ResearchLinkReducedBy, "Cost reduced by {0}" },
            { ResearchLinkExclusive, "Mutually exclusive with {0}" },
            { ResearchLinkUnlocks, "Unlocks {0}" },
            { ResearchLinkUnlockedBy, "Unlocked by {0}" },
            { ResearchDeedWinner, "won by {0}" },
            { ScreenQuests, "Quests Log" },
            { QuestsPinned, "pinned" },
            { ScreenEmpire, "Empire summary" },
            { EmpireDescriptionPanel, "Empire" },
            { EmpireHeadingsBand, "Column headings" },
            { ScreenSenate, "Senate" },
            { SenateGovernmentPanel, "Government" },
            { SenateElectionPanel, "Next election" },
            { SenateSupportPanel, "Support history" },
            { SenateEmptyLawSlot, "Empty law slot" },
            { SenateLockedLawSlot, "Locked law slot" },
            { SenateEmptySenatorSlot, "Empty senator slot" },
            { SenateLockedSenatorSlot, "Locked senator slot" },
            { ScreenEconomy, "Economy" },
            { EconomyQuantity, "Quantity {0}" },
            { EconomyBuyPanel, "Buy" },
            { EconomySellPanel, "Sell" },
            { EconomyTaxesPanel, "Marketplace taxes" },
            { EconomyLogPanel, "Recent transactions" },
            { EconomyAdsPanel, "Advertisements" },
            { EconomyEventsPanel, "Market events" },
            { EconomyInflationPanel, "Inflation" },
            { EconomyTradePanel, "Trade overview" },
            { ScreenMilitary, "Military status" },
            { MilitaryManpowerPanel, "Manpower" },
            { MilitaryFleetActions, "Fleet actions" },
            { MilitaryShipsPanel, "Ships" },
            { MilitaryDesignActions, "Ship design actions" },
            { ShipDesignSlots, "Module slots" },
            { ShipDesignModuleFitted, "Fitted {0}" },
            { ScreenHeroInspection, "Hero inspection" },
            { HeroSkillTrees, "Skill branches" },
            { HeroSkillLevel, "level {0} of {1}" },
            { HeroSkillPending, "level {0} pending" },
            { ScreenFleetSelection, "Fleet selection" },
            { TacticsSlotFilled, "Put {0} in your Tactics Set" },
            { ScreenBattle, "Space battle" },
            { ScreenGroundBattle, "Ground battle" },
            { ScreenAdvancedBattleReport, "Advanced battle report" },
            { BattleYourFleets, "Your fleets" },
            { BattleEnemyFleets, "Enemy fleets" },
            { BattleYourTroops, "Your troops" },
            { BattleEnemyTroops, "Enemy troops" },
            { BattleYourDamage, "Damage you caused" },
            { BattleEnemyDamage, "Damage they caused" },
            { BattleShowYourFleets, "Show your fleets" },
            { BattleShowEnemyFleets, "Show enemy fleets" },
            { BattleBalance, "Military power {0} against {1}" },
            { BattleTimeLeft, "{0} percent of the time left" },
            { BattleProgress, "Battle {0} percent fought" },
            { BattleYourShipLost, "Your {0} is lost" },
            { BattleYourShipsLost, "{0} of your ships lost" },
            { BattleEnemyShipLost, "Enemy {0} is lost" },
            { BattleEnemyShipsLost, "{0} enemy ships lost" },
            { BattleYourFlotillaLost, "Your {0} is destroyed" },
            { BattleYourFlotillasLost, "{0} of your flotillas destroyed" },
            { BattleEnemyFlotillaLost, "Enemy {0} is destroyed" },
            { BattleEnemyFlotillasLost, "{0} enemy flotillas destroyed" },
            { GroundBattleRound, "Round {0}" },
            { GroundBattleBombardment, "Orbital bombardment" },
            { GroundBattleAssault, "Ground assault" },
            { GroundBattleYourTroopLost, "One of your {0} is lost" },
            { GroundBattleYourTroopsLost, "{0} of your troops lost" },
            { GroundBattleEnemyTroopLost, "One enemy {0} is lost" },
            { GroundBattleEnemyTroopsLost, "{0} enemy troops lost" },
            { ScreenAcademy, "Hero management" },
            { AcademyHeroActions, "Hero actions" },
            { ScreenHeroCompleteList, "Heroes in the galaxy" },
            { ScreenGovernment, "Government selection" },
            { ScreenLaws, "Laws" },
            { ScreenPopulation, "Population overview" },
            { SenateActionsBand, "Actions" },
            { SenateDetailPane, "Details" },
            { ScreenTutorialSelection, "Tutorial selection" },
            { ScreenNewGame, "New game" },
            { ScreenAdvancedSettings, "Advanced settings" },
            { ScreenFactionChoice, "Faction selection" },
            { ColorPrefix + "marine-blue", "Marine blue" },
            { ColorPrefix + "red", "Red" },
            { ColorPrefix + "orange", "Orange" },
            { ColorPrefix + "apple", "Apple green" },
            { ColorPrefix + "green", "Green" },
            { ColorPrefix + "skyblue", "Sky blue" },
            { ColorPrefix + "purple", "Purple" },
            { ColorPrefix + "pink", "Pink" },
            { ColorPrefix + "yellow", "Yellow" },
            { ColorPrefix + "blue-green", "Blue green" },
            { ColorPrefix + "lavender", "Lavender" },
            { ColorPrefix + "wine", "Wine" },
            { ColorPrefix + "blue", "Blue" },
            { ColorPrefix + "cyan", "Cyan" },
            { ColorPrefix + "magenta", "Magenta" },
            { ColorPrefix + "kaki", "Khaki" },
        };

        // Keys already complained about, so a per-frame readout warns once, not every frame.
        private static readonly Dictionary<string, bool> Warned = new Dictionary<string, bool>();

        private static Dictionary<string, string> _overrides;

        /// <summary>
        /// Make <paramref name="overrides"/> the active translation overlay. Null or empty clears
        /// back to the English defaults, which is also the right result for a language with no
        /// translation file.
        /// </summary>
        public static void Install(IDictionary<string, string> overrides)
        {
            Warned.Clear();
            if (overrides == null || overrides.Count == 0)
            {
                _overrides = null;
                return;
            }

            Dictionary<string, string> copy = new Dictionary<string, string>(overrides.Count);
            foreach (KeyValuePair<string, string> entry in overrides)
            {
                copy[entry.Key] = entry.Value;
            }

            _overrides = copy;
        }

        /// <summary>Drop the overlay, returning to English defaults.</summary>
        public static void Reset()
        {
            _overrides = null;
            Warned.Clear();
        }

        /// <summary>
        /// The translated string for <paramref name="key"/>, else the English default, else the
        /// key itself so an unknown key is visible in speech rather than silently empty.
        /// </summary>
        public static string Get(string key)
        {
            string value;
            if (_overrides != null && _overrides.TryGetValue(key, out value))
            {
                return value;
            }

            if (TryGetDefault(key, out value))
            {
                return value;
            }

            WarnOnce("get:" + key, "strings: no such key '" + key + "'");
            return key;
        }

        /// <summary>
        /// <see cref="Get"/> plus <c>string.Format</c>. A translation whose placeholders do not
        /// match the English template throws inside Format; that is a broken translation, not a
        /// broken game, so it is logged once and the English template is used instead. Never
        /// throws.
        /// </summary>
        public static string Format(string key, params object[] args)
        {
            string template = Get(key);
            try
            {
                return string.Format(template, args);
            }
            catch (FormatException)
            {
                WarnOnce(
                    "format:" + key,
                    "strings: bad format for key '" + key + "': " + template
                );
            }

            string fallback;
            if (TryGetDefault(key, out fallback) && fallback != template)
            {
                try
                {
                    return string.Format(fallback, args);
                }
                catch (FormatException) { }
            }

            return template;
        }

        /// <summary>
        /// A counted phrase in the form its number calls for, the number filling the chosen
        /// template's <c>{0}</c>.
        ///
        /// Each form is a WHOLE sentence of its own rather than a number glued to a noun, because the
        /// noun agrees with the number in most languages and no template can inflect a fragment handed
        /// to it. Two forms is what English needs and what a translator can always fill in - a language
        /// with a single form writes the same sentence twice. A language that wants THREE or more
        /// (Russian, Polish, Arabic) is the trigger for real plural rules carried by the locale file;
        /// nothing here anticipates them, which is deliberate.
        /// </summary>
        public static string Plural(string oneKey, string manyKey, int count)
        {
            return Format(count == 1 ? oneKey : manyKey, count);
        }

        /// <summary>
        /// The compiled-in English template for <paramref name="key"/>. Exposed so translation
        /// files can be validated against the shipped keys and placeholders. The icon names
        /// (<see cref="IconDefaults"/>) are as much a shipped string as any other; they are held
        /// in their own table only because there are hundreds of them.
        /// </summary>
        public static bool TryGetDefault(string key, out string template)
        {
            return Defaults.TryGetValue(key, out template)
                || IconDefaults.TryGetValue(key, out template);
        }

        private static void WarnOnce(string warnKey, string message)
        {
            if (Warned.ContainsKey(warnKey))
            {
                return;
            }

            Warned[warnKey] = true;
            Log.Warn(message);
        }
    }
}
