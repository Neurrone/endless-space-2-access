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

        // The multiplayer session's own stream: everything said in the lobby and in the game, plus
        // every session event the game posts as a system message - joins, kicks, the launch
        // countdown, the network clock. Spoken as it arrives and kept here to be re-read.
        public const string BufferChat = "buffer.chat";
        public const string ChatSaid = "chat.said";
        public const string ChatWhispered = "chat.whispered";
        public const string ChatToAlliance = "chat.to-alliance";

        // Said when the game's own chat box takes the keyboard, which it does silently. The keys are
        // named because they are the game's, not the mod's: Enter posts the line and Escape hands the
        // keyboard back.
        public const string ChatTyping = "chat.typing";

        // The chat panel's own controls. The tab bar is the game's, and what the game draws on it is a
        // name per tab and a bare dot for a tab holding something unseen - so the dot needs words and the
        // cluster needs a name to be entered under.
        public const string ChatPanel = "chat.panel";
        public const string ChatUnread = "chat.unread";

        // The log of what has been said, and the box the next line is typed into. Both are the mod's
        // words: the game draws the log as bare lines under no heading at all, and it never draws the
        // box's own prompt (the panel keeps "%PressEnterToStartChattingTitle" as a placeholder it
        // compares against and never shows).

        public const string ChatMessageBox = "chat.message-box";

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

        // The scan view's anchored prompt: what to call it where its data gave it no heading, and
        // the cross it is closed with, which the game draws as a bare icon.
        public const string PromptScreen = "prompt.screen";
        public const string PromptClose = "prompt.close";

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

        /// <summary>The switch beside the turn controls that shows an alliance's coordination requests.
        /// The game draws it as a bare icon and its tooltip is a sentence about what a click would do,
        /// so there is no name of the game's to use.</summary>
        public const string GalaxyAllianceRequests = "galaxy.alliance-requests";

        // The multiplayer half of the turn controls, none of which the game names in words: the sync
        // mark is an icon whose whole meaning is on its tooltip, the desync button is an icon that
        // sends everybody back to the lobby, the ready ring is eight pictures, and the timers are
        // arcs drawn around the End Turn button with no caption anywhere. All of them are absent in
        // single player, where the game does not draw them at all.
        public const string GalaxySyncState = "galaxy.sync-state";
        public const string GalaxyReturnToLobby = "galaxy.return-to-lobby";
        public const string GalaxyPlayers = "galaxy.players";
        public const string GalaxyPlayersAllReady = "galaxy.players-all-ready";
        public const string GalaxyPlayerPlaying = "galaxy.player-playing";
        public const string GalaxyPlayersPlaying = "galaxy.players-playing";
        public const string GalaxyGlobalTimer = "galaxy.global-timer";
        public const string GalaxyTurnTimer = "galaxy.turn-timer";
        public const string GalaxyOvertimeTimer = "galaxy.overtime-timer";
        public const string GalaxyLastPlayerTimer = "galaxy.last-player-timer";
        public const string GalaxyTimerSeconds = "galaxy.timer-seconds";

        /// <summary>The turn is over for the player and the game is waiting on everybody else - the
        /// state the game itself shows by rewriting the End Turn caption to "Pending", which nothing
        /// announces because the turn number does not change while it lasts.</summary>
        public const string GalaxyTurnWaiting = "galaxy.turn-waiting";

        public const string GalaxyResearch = "galaxy.research";
        public const string GalaxyTurnsRemaining = "galaxy.turns-remaining";
        public const string GalaxyStockAndNet = "galaxy.stock-and-net";
        public const string GalaxySystemColonized = "galaxy.system-colonized";

        // A system of the player's that is still an OUTPOST rather than a colony - a distinct label on
        // the map, so a distinct word here.
        public const string GalaxySystemOutpost = "galaxy.system-outpost";
        public const string GalaxyOpenSystem = "galaxy.open-system";

        // The game has taken the camera to a point the map draws nothing at - a quest marker out
        // between the stars, a battle at a place the player cannot see. The camera HAS moved, so
        // silence would leave the player reading somewhere the game is no longer showing.
        public const string GalaxyShownOnMap = "galaxy.shown-on-map";

        // The same reveal, made by the quest banner's pin, which knows which quest it is about. Said
        // whether or not the marker turned out to be somewhere the tree can name.
        public const string GalaxyQuestShownOnMap = "galaxy.quest-shown-on-map";

        // The lanes out of a system. The game draws these as lines and writes nothing on them, and
        // where a line runs off into space it has no destination to name either. The number is the
        // lane's place going clockwise from north, which is also the order they are walked in; the
        // last slot is the compass word for the way the line leaves.
        public const string GalaxyStarlane = "galaxy.starlane";
        public const string GalaxyStarlaneUnexplored = "galaxy.starlane-unexplored";
        public const string GalaxyWormhole = "galaxy.wormhole";
        public const string GalaxyWormholeUnexplored = "galaxy.wormhole-unexplored";

        // Which lane a fleet under way is flying, said where that fleet hangs under one of the systems
        // the lane runs between. The number and the compass word are the ones the lane node itself
        // announces, off the same list, so the two cannot name different lines. Whole phrases, and one
        // per kind of line, because "starlane" and "wormhole" are different words in the sentence rather
        // than a noun slotted into it.
        public const string GalaxyFleetOnStarlane = "galaxy.fleet-on-starlane";
        public const string GalaxyFleetOnWormhole = "galaxy.fleet-on-wormhole";

        // The same answer for a fleet crossing OPEN SPACE between two systems with no lane between
        // them: there is no line to number, so the leg is named by the system at its other end and by
        // which end that is. Whole phrases, one per direction, because "to" and "from" are the sentence
        // rather than a word slotted into it - and one more of each for a far end the map has not
        // named, which is the same silence a lane running into the dark keeps.
        public const string GalaxyFleetFreeMovingTo = "galaxy.fleet-free-moving-to";
        public const string GalaxyFleetFreeMovingFrom = "galaxy.fleet-free-moving-from";
        public const string GalaxyFleetFreeMovingToUnexplored =
            "galaxy.fleet-free-moving-to-unexplored";
        public const string GalaxyFleetFreeMovingFromUnexplored =
            "galaxy.fleet-free-moving-from-unexplored";

        // How many fleets are under way on the lanes leaving a system, said by the system itself after
        // the count of what is parked there - the two together are what its branch opens onto.
        public const string GalaxyFleetUnderWayNearbyOne = "galaxy.fleet-under-way-nearby-one";
        public const string GalaxyFleetsUnderWayNearby = "galaxy.fleets-under-way-nearby";

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

        // What has been found in the ground, and whether the system is working it. The label says the
        // second half by drawing the deposit's picture lit or faded and writes no word for either
        // state, so both phrases are the mod's and each carries the deposit's own name.
        public const string GalaxySystemDepositExploited = "galaxy.system-deposit-exploited";
        public const string GalaxySystemDepositIdle = "galaxy.system-deposit-idle";

        // How many ships are sitting at a system, split the way the label splits them: the two
        // lozenges it draws under the name, one for the player's side and one for everyone else's.
        // The label writes a bare number in each and names neither side.
        public const string GalaxySystemFriendlyShip = "galaxy.system-friendly-ship";
        public const string GalaxySystemFriendlyShips = "galaxy.system-friendly-ships";
        public const string GalaxySystemHostileShip = "galaxy.system-hostile-ship";
        public const string GalaxySystemHostileShips = "galaxy.system-hostile-ships";

        // The Academy's standing, drawn on a system it holds: a bare number for the level and a ring
        // for how far the next one is. The ring carries no figure at all, so it is read as the
        // proportion it is drawn at, and the sentence around both is the mod's.
        public const string GalaxySystemAcademyLevel = "galaxy.system-academy-level";

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

        // A ground battle at a system. The map says it with one small picture beside the name and
        // explains it in a sentence that names nobody, so who is attacking is the mod's phrase - and it
        // is the DISPLAYED attacker, because the game hides a privateer's real owner behind another
        // empire's flag and reading past that would tell the player something the picture does not.
        // The bare form is for the battle the map is drawing while the repository will not say whose.
        public const string GalaxySystemInvadedBy = "galaxy.system-invaded-by";
        public const string GalaxySystemInvaded = "galaxy.system-invaded";

        // A time bubble parked on a system - the disk the map draws over it. The game names the bubble
        // and says who made it, but writes the turns it has left nowhere on the map at all, so the
        // sentence around all three is the mod's.
        public const string GalaxySystemTimeBubble = "galaxy.system-time-bubble";

        // A quest's marker standing at a system: the pin the map draws, and the brighter one it draws
        // for the quest the player is tracking. The game writes no word for either - the pin IS the
        // word - so the phrase around the quest's own title is the mod's, and the two forms are what
        // the map's own brightening says.
        public const string GalaxySystemQuestMarker = "galaxy.system-quest-marker";
        public const string GalaxySystemQuestMarkerPinned = "galaxy.system-quest-marker-pinned";

        // The ring the map draws round a node somebody is holding: a citadel's, or a fleet standing
        // guard. The ring is a colour and nothing else, so both phrases are the mod's, and each names
        // the empire the ring is PAINTED for - the displayed one, privateers included.
        public const string GalaxySystemCitadel = "galaxy.system-citadel";
        public const string GalaxySystemGuarded = "galaxy.system-guarded";

        /// <summary>How many curiosities a planet's orbital card is drawing in the ring around it. The
        /// card says it with one wordless icon per curiosity and writes no number anywhere, so the
        /// phrase is the mod's - and it is a COUNTED phrase, hence a form per number (see
        /// <see cref="Plural"/>) rather than a number glued to a noun.</summary>
        public const string GalaxyPlanetCuriosityOne = "galaxy.planet-curiosity-one";
        public const string GalaxyPlanetCuriosities = "galaxy.planet-curiosities";

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

        /// <summary>Where a place on the map is, as a pair of whole numbers measured from the empire's
        /// home system (<see cref="MapCoordinates"/>). The game draws the galaxy and writes no
        /// coordinate anywhere, so the pair is the mod's - and it is ONE template rather than a number
        /// said twice, because which axis comes first, and what separates them, is a language's
        /// business.</summary>
        public const string GalaxyCoordinatePair = "galaxy.coordinates";

        // The map's inspect cursor - a mode of the mod's own, so every word in it is the mod's. The
        // cursor's size is said as a whole phrase rather than a number glued to a symbol, because "3
        // by 3" is a shape and "3x3" is a sum. The two starlane phrases name the lane's ends the same
        // way round every time (westmost first), so one lane heard from two neighbouring cells is
        // heard as one lane; a lane whose far end the map has not named says which way it runs
        // instead, exactly as the lane's own node does.
        public const string GalaxyInspectEntered = "galaxy.inspect.entered";
        public const string GalaxyInspectExited = "galaxy.inspect.exited";
        public const string GalaxyInspectCursorSize = "galaxy.inspect.cursor-size";
        public const string GalaxyInspectEdge = "galaxy.inspect.edge";
        public const string GalaxyInspectStarlane = "galaxy.inspect.starlane";
        public const string GalaxyInspectStarlaneUnexplored = "galaxy.inspect.starlane-unexplored";
        public const string GalaxyInspectWormhole = "galaxy.inspect.wormhole";
        public const string GalaxyInspectWormholeUnexplored = "galaxy.inspect.wormhole-unexplored";

        // What the map draws OVER a place nobody has been to yet. The game paints it and names it
        // nowhere in its own interface - the only words it has for the thing are in one quest
        // objective's tooltip, "the fog of war" - so the phrase is the mod's, and it names what the
        // sampling actually asks (IsExplored, per square) rather than the picture: "unexplored". A
        // cell wholly under it says so; a cell partly under it says how many of its squares are,
        // which is what tells the player to shrink the cursor and find the edge of what they can see.
        public const string GalaxyInspectFog = "galaxy.inspect.fog";
        public const string GalaxyInspectFogOne = "galaxy.inspect.fog-one";
        public const string GalaxyInspectFogMany = "galaxy.inspect.fog-many";

        // The map's SCANNER - "what is near me, of this kind". Every scope is one whole phrase rather
        // than an adjective glued to a noun, because in most languages the adjective agrees with the
        // noun and no code that pastes two fragments together can inflect either. The eight are the
        // taxonomy in full: two categories, four ways of looking at each.
        public const string GalaxyScannerSystemsAll = "galaxy.scanner.systems-all";
        public const string GalaxyScannerSystemsFriendly = "galaxy.scanner.systems-friendly";
        public const string GalaxyScannerSystemsNeutral = "galaxy.scanner.systems-neutral";
        public const string GalaxyScannerSystemsEnemy = "galaxy.scanner.systems-enemy";
        public const string GalaxyScannerFleetsAll = "galaxy.scanner.fleets-all";
        public const string GalaxyScannerFleetsFriendly = "galaxy.scanner.fleets-friendly";
        public const string GalaxyScannerFleetsNeutral = "galaxy.scanner.fleets-neutral";
        public const string GalaxyScannerFleetsEnemy = "galaxy.scanner.fleets-enemy";

        /// <summary>How many things a scope holds, said as the scanner lands in it - a counted phrase,
        /// hence a form per number (see <see cref="Plural"/>).</summary>
        public const string GalaxyScannerFoundOne = "galaxy.scanner.found-one";
        public const string GalaxyScannerFoundMany = "galaxy.scanner.found-many";

        /// <summary>A scope the player is parked in that has run out under them. The scope's own name
        /// goes INSIDE the sentence rather than in front of it, so a language that says "there are no
        /// friendly fleets" has somewhere to put the words.</summary>
        public const string GalaxyScannerEmpty = "galaxy.scanner.empty";

        /// <summary>How far away something the scanner found is and which way it lies, in the galaxy's
        /// own units - the same unit the coordinate pair is in, so the two numbers can be held against
        /// each other. Counted, because a distance of one is a unit and not units.</summary>
        public const string GalaxyScannerDistanceOne = "galaxy.scanner.distance-one";
        public const string GalaxyScannerDistanceMany = "galaxy.scanner.distance-many";

        /// <summary>What is said instead of a distance and a direction when the thing the scanner
        /// found is where the player is already reading from.</summary>
        public const string GalaxyScannerHere = "galaxy.scanner.here";
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

        /// <summary>What a probe launch answers with: where it was aimed and how many probes the fleet
        /// has left. The game answers a launch with a sound and nothing in words, so both halves are the
        /// mod's; the count is a COUNTED phrase and therefore a form per number (see
        /// <see cref="Plural"/>) rather than a number glued to a noun. The bare form is for the fleet
        /// whose probe stock could not be read at all, where a made-up number would be worse than
        /// none.</summary>
        public const string GalaxyProbeLaunched = "galaxy.probe-launched";
        public const string GalaxyProbeLaunchedOne = "galaxy.probe-launched-one";
        public const string GalaxyProbeLaunchedMany = "galaxy.probe-launched-many";

        /// <summary>The same answer for a probe aimed at a compass DIRECTION rather than at a place.
        /// A parallel family rather than the place one with a direction word dropped into it: "towards
        /// Primus" and "heading north" are the same sentence only in English, and a translator handed
        /// one template for both would have to pick a preposition that fits a proper noun and a bearing
        /// at once.</summary>
        public const string GalaxyProbeHeading = "galaxy.probe-heading";
        public const string GalaxyProbeHeadingOne = "galaxy.probe-heading-one";
        public const string GalaxyProbeHeadingMany = "galaxy.probe-heading-many";

        /// <summary>The group the probe mode adds to the system a probe is being launched from: the
        /// eight compass bearings, offered beside that system's starlanes because the order takes any
        /// direction at all and the lanes are only the ones that lead somewhere.</summary>
        public const string GalaxyProbeDirections = "galaxy.probe-directions";

        /// <summary>Where a probe drifting through the map is, said from the system it is nearest to -
        /// which is how a sighted player reads the mote: against the nearest star. The distance is in
        /// turns of the probe's OWN flight, the unit the map's own countdown beside it is already in;
        /// "at" is for one that has not left yet, and the middle form for one less than a turn out,
        /// where a rounded number would say nothing. The distance is a COUNTED phrase and so has a form
        /// per number (see <see cref="Plural"/>), which this family spells out rather than calling
        /// because the number is not the only thing in the sentence.</summary>
        public const string GalaxyProbeAt = "galaxy.probe-at";
        public const string GalaxyProbeNear = "galaxy.probe-near";
        public const string GalaxyProbeOutOne = "galaxy.probe-out-one";
        public const string GalaxyProbeOutMany = "galaxy.probe-out-many";

        /// <summary>What pinning an ally coordination request answers with. The pin is drawn on the map
        /// and named nowhere, so the sentence is the mod's and the place in it is the game's.</summary>
        public const string GalaxyCoordinationRequested = "galaxy.coordination-requested";

        // How long a journey takes and where it stops on the way. The game says none of this in words
        // at all - it draws a dotted line with a numbered marker wherever a turn runs out - so every
        // phrase here is the mod's, and each is a whole sentence rather than a number glued to a noun.
        // "Movement" without "points" is deliberate: the figure is said beside a turn count, where the
        // longer word is the only thing that makes the pair hard to hear.
        public const string FleetRoutePreviewThisTurn = "fleet.route-preview-this-turn";
        public const string FleetRoutePreviewTurns = "fleet.route-preview-turns";
        public const string FleetRouteUsesPortal = "fleet.route-uses-portal";
        public const string FleetRouteUsesWormhole = "fleet.route-uses-wormhole";

        // The itinerary, one line per turn in which the fleet reaches somewhere, under the turn number
        // the game itself is showing rather than a count from now. A turn can reach several places, and
        // they share the line - so what fills the slot is a LIST of names, and a place the map has not
        // named goes into that list under the noun phrase below rather than as a sentence of its own.
        // The header says whose journey the turns belong to: under a DESTINATION the itinerary is a
        // proposal about the selected fleets, and without a word saying so it reads as something the
        // place itself is doing. A fleet's own itinerary needs no header - the fleet line above it has
        // already said where it is going.
        public const string FleetRoutePreviewHeader = "fleet.route-preview-header";
        public const string FleetItineraryTurn = "fleet.itinerary-turn";
        public const string FleetItineraryArrival = "fleet.itinerary-arrival";
        public const string FleetUnexploredSystem = "fleet.unexplored-system";

        // What a fleet already under way says about where it is going. Two shapes, because some of the
        // surfaces that say it have already named the destination and some have not.
        public const string FleetRouteToThisTurn = "fleet.route-to-this-turn";
        public const string FleetRouteToTurns = "fleet.route-to-turns";
        public const string FleetRouteUnexploredThisTurn = "fleet.route-unexplored-this-turn";
        public const string FleetRouteUnexploredTurns = "fleet.route-unexplored-turns";
        public const string FleetRouteArrivesThisTurn = "fleet.route-arrives-this-turn";
        public const string FleetRouteArrivesTurns = "fleet.route-arrives-turns";

        // A journey the game called off without asking. Being intercepted zeroes a fleet's movement and
        // terminates its move order, and a route whose next hop has become impossible is cancelled
        // outright - both silently, and the only sign of either is a fleet that has stopped.
        public const string FleetInterceptedAt = "fleet.intercepted-at";
        public const string FleetIntercepted = "fleet.intercepted";
        public const string FleetRouteCancelledTo = "fleet.route-cancelled-to";
        public const string FleetRouteCancelled = "fleet.route-cancelled";

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

        // The save the game is writing right now - a manual one, a quick save, or the autosave at the
        // end of a turn. The game marks the whole of it with a spinning icon and no words at all, so
        // both halves are the mod's own sentences, and whole sentences: the fact a player needs is that
        // the game is busy writing and then that it is safe to leave.
        public const string SaveStarted = "save.started";
        public const string SaveFinished = "save.finished";

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

        /// <summary>What a control the player could pick something up from says while nothing is being
        /// carried - the drag's half of "has tooltip". Not said while something IS held: the useful fact
        /// about a control then is whether the thing can go there.</summary>
        public const string CarryDraggable = "carry.draggable";

        // The scan view. The lens names itself in the game's own words, so the only strings here are
        // for the two boxes the game draws bare and the pairing of a number with the output it counts.
        public const string ScreenScanView = "screen.scan-view";
        public const string ScanSystemInfo = "scan.system-info";
        public const string ScanLegend = "scan.legend";
        public const string ScanOutput = "scan.output";

        /// <summary>How much of a governor's dial the system management lens has filled in. The game
        /// draws the value as a pie and writes no number anywhere, and its own caption over the dial
        /// already says what is being measured, so this is the bare proportion.</summary>
        public const string ScanHeroEfficiency = "scan.hero-efficiency";

        /// <summary>How close the game is looking, wherever the mod offers it as a control - the scan
        /// view and the map. The game draws no such control - its zoom is a wheel gesture and two keys
        /// held down - so the name is the mod's.</summary>
        public const string Zoom = "zoom";

        /// <summary>How close the game is looking, said whenever it changes wherever the player is
        /// standing - the wheel, the game's own held zoom keys, a click that flies into a system, the
        /// page a system or a planet opens onto. The rung and how many there are, because the numbers
        /// are all there is: the game gives its zoom steps no names, and the two view levels above them
        /// are pages rather than distances. The game writes nothing comparable of its own.</summary>
        public const string ZoomLevel = "zoom.level";

        /// <summary>One of the icon pairs a planet's card lines up under its ring: which of the empire's
        /// populations does well on that planet, and what they get out of it. The game draws two pictures
        /// side by side and writes no words at all.</summary>
        public const string ScanSynergy = "scan.synergy";

        /// <summary>A fight in orbit, which the diplomacy lens draws as the two sides' emblems and no
        /// words - so the empires are named from the fleets and the fact of the battle is the mod's word.
        /// The second form is for a line the game is drawing while the fleets have already gone.</summary>
        public const string ScanBattle = "scan.battle";
        public const string ScanBattleHere = "scan.battle-here";

        /// <summary>The trade routes the scan view draws as lines across the map, said a LANE at a time
        /// because that is what the game draws: one line per pair of neighbouring places, however many
        /// of the empire's routes run over it, painted open, blockaded or mixed
        /// (<see cref="ES2Access.Core.UI.TradeLanes"/>). The game writes no words on any of it - the
        /// lines carry a colour and nothing else - so the heading, the lane's name and all four counted
        /// phrases are the mod's. Mixed keeps its own sentence: the lane is a third colour on the
        /// screen, and "blockaded" alone would hide the traffic still getting through.</summary>
        public const string ScanTradeRoutesGroup = "scan.trade-routes-group";
        public const string ScanTradeLane = "scan.trade-lane";
        public const string ScanTradeRouteOne = "scan.trade-route-one";
        public const string ScanTradeRoutes = "scan.trade-routes";
        public const string ScanTradeRouteOneBlockaded = "scan.trade-route-one-blockaded";
        public const string ScanTradeRoutesBlockaded = "scan.trade-routes-blockaded";
        public const string ScanTradeRoutesMixed = "scan.trade-routes-mixed";

        // The star system management page. The panel names are the mod's, because the game draws the
        // panels as unlabelled boxes with an icon in the corner; everything a panel CONTAINS is read
        // in the game's own words.
        public const string ScreenStarSystem = "screen.star-system";
        public const string ScreenPlanet = "screen.planet";

        /// <summary>The two arrows the planet page draws beside the planet's name, which walk the empire's
        /// planets one at a time. The game gives them a DESCRIPTION and no title
        /// (<c>%PlanetInfoSidePreviousDescription</c> is the whole of what it writes), so the names are the
        /// mod's.</summary>
        public const string PlanetPrevious = "planet.previous";
        public const string PlanetNext = "planet.next";

        /// <summary>What a star system's hangar says when it holds no ships. The game draws the toolbar
        /// and an empty area, with no placeholder of its own anywhere
        /// (<c>ShipsManagementPanel</c> has no such label), so the phrase is the mod's.</summary>
        public const string SystemHangarEmpty = "system.hangar-empty";

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

        // The election's vote breakdown draws how far the count has got as a bare horizontal bar -
        // three coloured segments, no words anywhere on or around it. The mod's sentence is the only
        // way the two numbers behind it are said.
        public const string ElectionRepresentativesCounted = "election.representatives-counted";
        // The empire summary. The screen's own name is the game's and is what is normally spoken; the
        // name here is the fallback for the frames before the window has written its heading. The mod
        // word is for a box the game draws with no heading at all: the panel holding the empire's
        // portrait and description.
        public const string ScreenEmpire = "screen.empire";
        public const string EmpireDescriptionPanel = "empire.description-panel";

        /// <summary>Which of the two opposite things pressing an empire relic slot will do (Nakalim only).
        /// The slot flips between them on its own as the empire's relic stock changes
        /// (<c>RelicSlotItem.Update</c>), and the ONLY sign on screen is which of two pictures is drawn -
        /// so a blind player would otherwise press a button that silently means the opposite thing.
        /// </summary>
        public const string EmpireRelicSlotAssign = "empire.relic-slot-assign";
        public const string EmpireRelicSlotRemove = "empire.relic-slot-remove";

        /// <summary>What happened when a population unit was dropped on ANOTHER system's row: the game
        /// does not move it there, it points the home system's spaceport at that system and sends a ship,
        /// so this is a different sentence from a move between two planets of one system.</summary>
        public const string EmpirePopulationSent = "empire.population-sent";

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

        /// <summary>A tactic having landed in a slot of the battle deck, one having been taken out
        /// again, and the name of the place a tactic is dropped to take it out - a node the mod draws
        /// because the mouse's own way out is releasing the card over nothing, which a keyboard cannot
        /// aim at. The window's own headings, its cards and its refusals are all the game's words; a
        /// drag ending is the MOD's and the game answers it in silence, so these are the phrases the
        /// deck editor needs.</summary>
        public const string TacticsSlotFilled = "tactics.slot-filled";
        public const string TacticsSlotEmptied = "tactics.slot-emptied";
        public const string TacticsRemoveTarget = "tactics.remove-target";

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

        // The sweep screens: the error and non-blocking dialogs, the target pickers, the
        // cutscenes, the cursor-mode watcher, the end-game pages and the main-menu
        // destinations that had no name at all. The screens ask for these through
        // OptionalText, so a translation that lacks one silences that line rather than
        // speaking the key.
        public const string ScreenError = "screen.error";
        public const string ScreenTargetSelection = "screen.target-selection";
        public const string ScreenGroundTargetSelection = "screen.ground-target-selection";
        public const string ScreenCutscene = "screen.cutscene";
        public const string CursorModeEnded = "cursor.mode-ended";
        public const string ScreenVictory = "screen.victory";
        public const string ScreenJournal = "screen.journal";
        public const string ScreenDlc = "screen.dlc";
        public const string ScreenCredits = "screen.credits";
        public const string ScreenModding = "screen.modding";
        public const string ScreenResourcesExport = "screen.resources-export";
        public const string ScreenJoinGame = "screen.join-game";
        public const string ScreenDisclaimer = "screen.disclaimer";

        /// <summary>The credit roll: a name for the one thing on the page, and how much of it there is.
        /// The page itself writes neither - it is six hundred lines of prose and nothing else.</summary>
        public const string CreditsRoll = "credits.roll";
        public const string CreditsLine = "credits.line";
        public const string CreditsLines = "credits.lines";

        /// <summary>What the content browser expresses as the SHAPE of a row - a tick to activate what you
        /// own, a store button for what you do not - and what the tick itself is called, since the game
        /// draws it as a bare box.</summary>
        public const string DlcOwned = "dlc.owned";
        public const string DlcNotOwned = "dlc.not-owned";
        public const string DlcActivated = "dlc.activated";

        /// <summary>The mod manager's activation box, which the game draws as a bare tick beside the mod's
        /// name.</summary>
        public const string ModdingActivated = "modding.activated";

        /// <summary>What the list of multiplayer games says when the Steam search comes back. The search
        /// is asynchronous - the page opens empty and fills seconds later - so its arrival is the only
        /// thing that says the list is the list, and the empty answer is a line on the page as well as
        /// the sentence a finished search speaks.</summary>
        public const string JoinGameNoGames = "join-game.no-games";
        public const string JoinGameGameFound = "join-game.game-found";
        public const string JoinGameGamesFound = "join-game.games-found";

        /// <summary>How much the asset exporter's list is showing. The page opens on a loading curtain
        /// and fills a moment later, and each of its three filter ticks then adds or removes hundreds
        /// of rows at once, so the size of the list is the one thing about it nothing on screen says.
        /// </summary>
        public const string ResourcesExportAssetListed = "resources-export.asset-listed";
        public const string ResourcesExportAssetsListed = "resources-export.assets-listed";

        // The lobby's multiplayer marks. The game draws the crown, the kick button and the lock as
        // pictures with no words: the crown carries no tooltip at all, and the other two explain what
        // clicking them DOES ("Click to kick this player") without naming the thing being clicked. The
        // ready and eliminated marks do explain themselves in the game's words and take nothing from
        // here. The launch lock is a state with no widget of its own - it switches thirty controls off
        // at once, five seconds before the game starts.
        public const string NewGameHost = "new-game.host";
        public const string NewGameKick = "new-game.kick";
        public const string NewGameLockEmpire = "new-game.lock-empire";
        public const string NewGameLobbyLocked = "new-game.lobby-locked";
        public const string NewGameLobbyUnlocked = "new-game.lobby-unlocked";

        /// <summary>The lobby's chat history, which the game draws as a scrolling list of lines with no
        /// heading of its own.</summary>
        public const string NewGameChatLog = "new-game.chat-log";

        public const string NotifyOpenNegotiation = "notify.open-negotiation";
        public const string NotifyOpenMinorFaction = "notify.open-minor-faction";
        public const string NotifyOpenScoreScreen = "notify.open-score-screen";
        public const string NotifyOpenAcademy = "notify.open-academy";

        /// <summary>What the elimination popup means when the empire knocked out is the player's own: the
        /// game writes the same sentence for their defeat as for an AI's, and the only difference on screen
        /// is which buttons it draws.</summary>
        public const string NotifyOwnElimination = "notify.own-elimination";

        // A hero's own window. Its heading is the game's own and is declared where it is drawn, so the
        // screen name is only a fallback for the frames before the window has written one. The wheel of
        // skills the page draws with no caption over it needs a word, and a skill dot needs two: the game
        // paints the level a skill stands at, and the level a pending pick would take it to, as a ring of
        // coloured arcs round the dot and writes no number anywhere.
        public const string ScreenHeroInspection = "screen.hero-inspection";
        public const string HeroSkillTrees = "hero.skill-trees";
        public const string HeroSkillLevel = "hero.skill-level";
        public const string HeroSkillPending = "hero.skill-pending";

        /// <summary>A ring of the wheel, which the game names nowhere: it draws the rings as concentric
        /// arcs and writes only the threshold each one opens at, as a legend with a leader line pointing
        /// into the wheel. So the mod says which ring it is, and says the threshold as the sentence the
        /// leader line makes for anyone who can follow it.</summary>
        public const string HeroSkillRing = "hero.skill-ring";
        public const string HeroSkillRingPoint = "hero.skill-ring-point";
        public const string HeroSkillRingPoints = "hero.skill-ring-points";

        public const string ScreenGovernment = "screen.government";
        public const string ScreenLaws = "screen.laws";
        public const string ScreenPopulation = "screen.population";

        /// <summary>The two markers the population screen draws on a people's row for a gene hunter
        /// (Horatio) and nowhere else: one says this people has already been spliced into the empire's
        /// own, the other that there are now enough of them to splice. The game draws both as bare
        /// pictures with no text and no tooltip on them (<c>PopulationAffinityFilter</c>
        /// :87-99 only flips <c>Visible</c>), so these are the only words there are for them.</summary>
        public const string PopulationAssimilated = "population.assimilated";
        public const string PopulationReadyForAssimilation = "population.ready-for-assimilation";

        // ---- the diplomacy family ----
        // Every window in this family writes its own heading, so each screen name here is only ever
        // spoken in the frames before the game has filled its title in. What the family really needs mod
        // words for is the bands the game draws with no caption at all.

        public const string ScreenDiplomacy = "screen.diplomacy";

        /// <summary>The ring of empires, and the tick box and two window buttons drawn under it. The game
        /// captions neither group.</summary>
        public const string DiplomacyEmpires = "diplomacy.empires";
        public const string DiplomacyControls = "diplomacy.controls";

        /// <summary>The badge the diplomacy ring paints on an empire with NOTHING written on it: either a
        /// truce can be forced with them now, or terms the player has not seen yet have become available.
        /// A wordless mark needs words, and the game has none for it anywhere.</summary>
        public const string DiplomacyNewOptions = "diplomacy.new-options";

        /// <summary>Why an empire refuses to be negotiated with while the ring is centred on somebody
        /// else's relations. The game has a sentence for an unmet empire and for an eliminated one and
        /// says nothing at all about this case, which is reachable only through its own swap mode.
        /// </summary>
        public const string DiplomacyNotYourRing = "diplomacy.not-your-ring";

        public const string ScreenNegotiation = "screen.negotiation";

        /// <summary>The negotiation table's eight bands. The game titles the pressure gauge and writes
        /// real column headers over the term tables, but captions none of the bands themselves.</summary>
        public const string NegotiationHeader = "negotiation.header";
        public const string NegotiationDossier = "negotiation.dossier";
        public const string NegotiationPressure = "negotiation.pressure";
        public const string NegotiationMyTerms = "negotiation.my-terms";
        public const string NegotiationTheirTerms = "negotiation.their-terms";
        public const string NegotiationFilters = "negotiation.filters";
        public const string NegotiationTerms = "negotiation.terms";
        public const string NegotiationContract = "negotiation.contract";
        public const string NegotiationContractBoth = "negotiation.contract-both";
        public const string NegotiationContractMine = "negotiation.contract-mine";
        public const string NegotiationContractTheirs = "negotiation.contract-theirs";
        public const string NegotiationApproval = "negotiation.approval";

        /// <summary>Where the deal-approval bar STANDS, which is what that band draws and all it draws:
        /// the computer sends written reasons with its evaluation and the window throws them away, so a
        /// number is the honest reading.</summary>
        public const string NegotiationApprovalValue = "negotiation.approval-value";
        public const string NegotiationActions = "negotiation.actions";

        /// <summary>The stepper a resource term draws beside its name. The game writes the number in the
        /// box and nothing else, so the box needs naming.</summary>
        public const string NegotiationQuantity = "negotiation.quantity";

        public const string ScreenMinorDiplomacy = "screen.minor-diplomacy";
        public const string MinorIdentity = "minor.identity";
        public const string MinorRelation = "minor.relation";
        public const string MinorGains = "minor.gains";

        public const string ScreenPirateDiplomacy = "screen.pirate-diplomacy";
        public const string PiratePower = "pirate.power";
        public const string PirateStanding = "pirate.standing";
        public const string PirateNextFleet = "pirate.next-fleet";

        public const string ScreenAcademyDiplomacy = "screen.academy-diplomacy";
        public const string ScreenAcademyModal = "screen.academy-modal";
        public const string AcademyRelation = "academy-diplomacy.relation";
        public const string AcademyNamedShips = "academy.named-ships";

        /// <summary>The band of actions, and the pair of stock figures along the bottom edge, that the
        /// minor-faction, pirate and Academy windows all draw. The pirate window captions its actions
        /// ("Actions") and the other two do not, so the mod's caption is used for all three rather than
        /// one band being named differently from its twins.</summary>
        public const string DiplomacyActionsBand = "diplomacy.actions-band";
        public const string DiplomacyTreasury = "diplomacy.treasury";

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
            { BufferChat, "Chat" },
            { ChatSaid, "{0}: {1}" },
            { ChatWhispered, "{0} whispers: {1}" },
            { ChatToAlliance, "{0}, to the alliance: {1}" },
            { ChatTyping, "Chat. Type a message, Enter sends it, Escape leaves." },
            { ChatPanel, "Chat" },
            { ChatUnread, "unread messages" },

            { ChatMessageBox, "Message" },
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
            { PromptScreen, "Prompt" },
            { PromptClose, "Close" },
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
            { GalaxyAllianceRequests, "Alliance requests" },
            { GalaxySyncState, "Multiplayer synchronization" },
            { GalaxyReturnToLobby, "Return all players to the lobby" },
            { GalaxyPlayers, "Players" },
            { GalaxyPlayersAllReady, "every player has ended their turn" },
            { GalaxyPlayerPlaying, "{0} player is still playing" },
            { GalaxyPlayersPlaying, "{0} players are still playing" },
            { GalaxyGlobalTimer, "Game time remaining" },
            { GalaxyTurnTimer, "Turn time remaining" },
            { GalaxyOvertimeTimer, "Overtime remaining" },
            { GalaxyLastPlayerTimer, "Last player time remaining" },
            { GalaxyTimerSeconds, "{0} seconds" },
            { GalaxyTurnWaiting, "Turn ended, waiting for the other players" },
            { GalaxyResearch, "Research" },
            { GalaxyTurnsRemaining, "{0} turns remaining" },
            { GalaxyStockAndNet, "{0}, {1} per turn" },
            { GalaxySystemColonized, "colonized" },
            { GalaxySystemOutpost, "outpost" },
            { GalaxyOpenSystem, "Open system" },
            { GalaxyShownOnMap, "Shown on the map" },
            { GalaxyQuestShownOnMap, "{0}, objective shown on the map" },
            { GalaxyStarlane, "Starlane {0} to {1}, {2}" },
            { GalaxyStarlaneUnexplored, "Starlane {0} to an unexplored system, {1}" },
            { GalaxyWormhole, "Wormhole {0} to {1}, {2}" },
            { GalaxyWormholeUnexplored, "Wormhole {0} to an unexplored system, {1}" },
            { GalaxyFleetOnStarlane, "on starlane {0}, {1}" },
            { GalaxyFleetOnWormhole, "on wormhole {0}, {1}" },
            { GalaxyFleetFreeMovingTo, "free moving to {0}" },
            { GalaxyFleetFreeMovingFrom, "free moving from {0}" },
            { GalaxyFleetFreeMovingToUnexplored, "free moving to an unexplored system" },
            { GalaxyFleetFreeMovingFromUnexplored, "free moving from an unexplored system" },
            { GalaxyFleetUnderWayNearbyOne, "{0} fleet under way nearby" },
            { GalaxyFleetsUnderWayNearby, "{0} fleets under way nearby" },
            { GalaxySystemPopulation, "{0} population" },
            { GalaxySystemSleepers, "{0} sleepers" },
            { GalaxySystemBuilding, "Building {0}, {1} turns" },
            { GalaxySystemNothingBuilding, "Nothing under construction" },
            { GalaxySystemRebellion, "Rebellion at {0} percent, {1} to go" },
            { GalaxySystemPiratePower, "Pirate power level {0}" },
            { GalaxySystemMinorRelation, "Relations {0} percent" },
            { GalaxySystemDepositExploited, "{0}, exploited" },
            { GalaxySystemDepositIdle, "{0}, not exploited" },
            { GalaxySystemFriendlyShip, "{0} friendly ship" },
            { GalaxySystemFriendlyShips, "{0} friendly ships" },
            { GalaxySystemHostileShip, "{0} hostile ship" },
            { GalaxySystemHostileShips, "{0} hostile ships" },
            { GalaxySystemAcademyLevel, "Academy level {0}, {1} percent to the next level" },
            { GalaxySystemConstellation, "In the {0} constellation" },
            { GalaxyObliteratorProjectile, "Obliterator missile" },
            { GalaxySystemDiplomacy, "Diplomacy" },
            { GalaxySystemPirateMarkBuyout, "Buy out pirate mark" },
            { GalaxySystemConversionBuyout, "Buy out conversion" },
            { GalaxySystemAcademyBuyout, "Buy out academy conversion" },
            { GalaxySystemHackingBeacon, "Displace hacking beacon" },
            { GalaxySystemInvadedBy, "Ground battle, attacker {0}" },
            { GalaxySystemInvaded, "Ground battle" },
            { GalaxySystemTimeBubble, "{0}, {1}, {2} turns left" },
            { GalaxySystemQuestMarker, "Quest here: {0}" },
            { GalaxySystemQuestMarkerPinned, "Tracked quest here: {0}" },
            { GalaxySystemCitadel, "Citadel, {0}" },
            { GalaxySystemGuarded, "Guarded by {0}" },
            { GalaxyPlanetCuriosityOne, "{0} curiosity" },
            { GalaxyPlanetCuriosities, "{0} curiosities" },
            { DirectionNorth, "north" },
            { DirectionNorthEast, "northeast" },
            { DirectionEast, "east" },
            { DirectionSouthEast, "southeast" },
            { DirectionSouth, "south" },
            { DirectionSouthWest, "southwest" },
            { DirectionWest, "west" },
            { DirectionNorthWest, "northwest" },
            { GalaxyCoordinatePair, "{0}, {1}" },
            { GalaxyInspectEntered, "Inspect mode" },
            { GalaxyInspectExited, "Exited inspect mode" },
            { GalaxyInspectCursorSize, "Cursor {0} by {1}" },
            { GalaxyInspectEdge, "Edge of the galaxy" },
            { GalaxyInspectStarlane, "Star lane from {0} to {1}" },
            { GalaxyInspectStarlaneUnexplored, "Star lane from {0} going {1}" },
            { GalaxyInspectWormhole, "Wormhole from {0} to {1}" },
            { GalaxyInspectWormholeUnexplored, "Wormhole from {0} going {1}" },
            { GalaxyInspectFog, "Unexplored" },
            { GalaxyInspectFogOne, "1 square unexplored" },
            { GalaxyInspectFogMany, "{0} squares unexplored" },
            { GalaxyScannerSystemsAll, "All systems" },
            { GalaxyScannerSystemsFriendly, "Friendly systems" },
            { GalaxyScannerSystemsNeutral, "Neutral systems" },
            { GalaxyScannerSystemsEnemy, "Enemy systems" },
            { GalaxyScannerFleetsAll, "All fleets" },
            { GalaxyScannerFleetsFriendly, "Friendly fleets" },
            { GalaxyScannerFleetsNeutral, "Neutral fleets" },
            { GalaxyScannerFleetsEnemy, "Enemy fleets" },
            { GalaxyScannerFoundOne, "1 found" },
            { GalaxyScannerFoundMany, "{0} found" },
            { GalaxyScannerEmpty, "{0}, none found" },
            { GalaxyScannerDistanceOne, "{0} unit {1}" },
            { GalaxyScannerDistanceMany, "{0} units {1}" },
            { GalaxyScannerHere, "here" },
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
            { GalaxyProbeLaunched, "Probe launched towards {0}" },
            { GalaxyProbeLaunchedOne, "Probe launched towards {0}, {1} probe remaining" },
            { GalaxyProbeLaunchedMany, "Probe launched towards {0}, {1} probes remaining" },
            { GalaxyProbeHeading, "Probe launched heading {0}" },
            { GalaxyProbeHeadingOne, "Probe launched heading {0}, {1} probe remaining" },
            { GalaxyProbeHeadingMany, "Probe launched heading {0}, {1} probes remaining" },
            { GalaxyProbeDirections, "Launch towards" },
            { GalaxyProbeAt, "at {0}" },
            { GalaxyProbeNear, "{0} of {1}" },
            { GalaxyProbeOutOne, "{0} of {1}, {2} turn out" },
            { GalaxyProbeOutMany, "{0} of {1}, {2} turns out" },
            { GalaxyCoordinationRequested, "Coordination request placed at {0}" },
            { FleetRoutePreviewThisTurn, "Arrives this turn, {0} movement" },
            { FleetRoutePreviewTurns, "{0} turns, {1} movement" },
            { FleetRouteUsesPortal, "Uses portal" },
            { FleetRouteUsesWormhole, "Uses wormhole" },
            { FleetRoutePreviewHeader, "Route preview" },
            { FleetItineraryTurn, "Turn {0}: {1}" },
            { FleetItineraryArrival, "Turn {0}: {1} (destination)" },
            { FleetUnexploredSystem, "an unexplored system" },
            { FleetRouteToThisTurn, "En route to {0}, arrives this turn" },
            { FleetRouteToTurns, "En route to {0}, arrives in {1} turns" },
            {
                FleetRouteUnexploredThisTurn,
                "En route to an unexplored system, arrives this turn"
            },
            {
                FleetRouteUnexploredTurns,
                "En route to an unexplored system, arrives in {0} turns"
            },
            { FleetRouteArrivesThisTurn, "Arrives this turn" },
            { FleetRouteArrivesTurns, "Arrives in {0} turns" },
            { FleetInterceptedAt, "{0} was intercepted at {1}" },
            { FleetIntercepted, "{0} was intercepted" },
            { FleetRouteCancelledTo, "The route of {0} to {1} was cancelled" },
            { FleetRouteCancelled, "The route of {0} was cancelled" },
            { HudQuestShowLocation, "Show location" },
            { HudQuestUnpin, "Unpin quest" },
            { HudQuestPinned, "Pinned quest: {0}" },
            { HudQuestUnpinned, "No quest is pinned" },
            { GameMenuGameSettings, "Game settings" },
            { GameMenuReadOnlySettings, "read only" },
            { LoadSaveSaveName, "Save name" },
            { LoadSaveEditName, "Type the save name, then press Enter." },
            { LoadSaveCloud, "Cloud saves" },
            { NavCellEmpty, "empty" },
            { SaveStarted, "Saving the game" },
            { SaveFinished, "Save finished" },
            { CarryCarrying, "Dragging {0}" },
            { CarryDropped, "Dropped {0}" },
            { CarryDropRefused, "{0} cannot go there" },
            { CarryCancelled, "Cancelled drag" },
            { CarryMovedToPosition, "Moved {0} to position {1}" },
            { CarryDropTarget, "drop target" },
            { CarryDraggable, "draggable" },
            { ScreenScanView, "Scan view" },
            { ScanSystemInfo, "System information" },
            { ScanLegend, "Legend" },
            { ScanOutput, "{0} {1}" },
            { ScanHeroEfficiency, "{0}%" },
            { Zoom, "Zoom" },
            { ZoomLevel, "Zoom level {0} of {1}" },
            { ScanSynergy, "{0} for {1}" },
            { ScanBattle, "Battle between {0}" },
            { ScanBattleHere, "Battle" },
            { ScanTradeRoutesGroup, "Trade routes" },
            { ScanTradeLane, "{0} to {1}" },
            { ScanTradeRouteOne, "One trade route" },
            { ScanTradeRoutes, "{0} trade routes" },
            { ScanTradeRouteOneBlockaded, "One trade route, blockaded" },
            { ScanTradeRoutesBlockaded, "{0} trade routes, blockaded" },
            { ScanTradeRoutesMixed, "{0} trade routes, {1} blockaded" },
            { ScreenStarSystem, "Star system" },
            { ScreenPlanet, "Planet" },
            { PlanetPrevious, "Previous planet" },
            { PlanetNext, "Next planet" },
            { SystemHangarEmpty, "No ships in the hangar" },
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
            {
                RenameTypePrompt,
                "Type the new name, then press Enter to confirm or Escape to stop editing."
            },
            { ScreenResearch, "Research" },
            { ResearchStatusPanel, "Research status" },
            { ResearchKeyPanel, "Research key" },
            { ResearchTreePanel, "Technology tree" },
            { ResearchCounts, "{0} ({1} / {2})" },
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
            { EmpireRelicSlotAssign, "Assign relics" },
            { EmpireRelicSlotRemove, "Remove relics" },
            { EmpirePopulationSent, "Sent {0} to {1} by spaceport" },
            { ScreenSenate, "Senate" },
            { SenateGovernmentPanel, "Government" },
            { SenateElectionPanel, "Next election" },
            { SenateSupportPanel, "Support history" },
            { SenateEmptyLawSlot, "Empty law slot" },
            { SenateLockedLawSlot, "Locked law slot" },
            { SenateEmptySenatorSlot, "Empty senator slot" },
            { SenateLockedSenatorSlot, "Locked senator slot" },
            { ElectionRepresentativesCounted, "{0} of {1} representatives counted" },
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
            { HeroSkillRing, "Ring {0} of {1}" },
            { HeroSkillRingPoint, "opens once 1 skill point has been spent" },
            { HeroSkillRingPoints, "opens once {0} skill points have been spent" },
            { ScreenFleetSelection, "Fleet selection" },
            { TacticsSlotFilled, "Put {0} in your Tactics Set" },
            { TacticsSlotEmptied, "{0} removed" },
            { TacticsRemoveTarget, "Drop tactic here to remove" },
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
            { ScreenError, "Error" },
            { ScreenTargetSelection, "Choose a target" },
            { ScreenGroundTargetSelection, "Choose a system to invade" },
            { ScreenCutscene, "Cutscene" },
            { CursorModeEnded, "Target selection ended" },
            { ScreenVictory, "Score screen" },
            { ScreenJournal, "Journal" },
            { ScreenDlc, "Downloadable content" },
            { ScreenCredits, "Credits" },
            { ScreenModding, "Mods" },
            { ScreenResourcesExport, "Export resources" },
            { ScreenJoinGame, "Join game" },
            { ScreenDisclaimer, "Disclaimer" },
            { CreditsRoll, "Credit roll" },
            { CreditsLine, "{0} line" },
            { CreditsLines, "{0} lines" },
            { DlcOwned, "owned" },
            { DlcNotOwned, "not owned" },
            { DlcActivated, "Activated" },
            { ModdingActivated, "Activated" },
            { JoinGameNoGames, "No multiplayer games found" },
            { JoinGameGameFound, "{0} multiplayer game found" },
            { JoinGameGamesFound, "{0} multiplayer games found" },
            { ResourcesExportAssetListed, "{0} asset listed" },
            { ResourcesExportAssetsListed, "{0} assets listed" },
            { NewGameHost, "Host" },
            { NewGameKick, "Kick" },
            { NewGameLockEmpire, "Lock empire" },
            { NewGameLobbyLocked, "The lobby is locked while the game launches" },
            { NewGameLobbyUnlocked, "The lobby is unlocked" },
            { NewGameChatLog, "Chat log" },
            { NotifyOpenNegotiation, "Open negotiation" },
            { NotifyOpenMinorFaction, "Open minor faction diplomacy" },
            { NotifyOpenScoreScreen, "Open score screen" },
            { NotifyOpenAcademy, "Open the Academy" },
            {
                NotifyOwnElimination,
                "your empire is out of the game: the score screen is the only way on"
            },
            { ScreenAcademy, "Hero management" },
            { AcademyHeroActions, "Hero actions" },
            { ScreenHeroCompleteList, "Heroes in the galaxy" },
            { ScreenGovernment, "Government selection" },
            { ScreenLaws, "Laws" },
            { ScreenPopulation, "Population overview" },
            { PopulationAssimilated, "Assimilated" },
            { PopulationReadyForAssimilation, "Ready for assimilation" },
            { ScreenDiplomacy, "Diplomatic status" },
            { DiplomacyEmpires, "Empires" },
            { DiplomacyControls, "Screen controls" },
            { DiplomacyNewOptions, "new diplomatic options" },
            { DiplomacyNotYourRing, "You can only negotiate from your own empire's ring" },
            { ScreenNegotiation, "Negotiation" },
            { NegotiationHeader, "The two empires" },
            { NegotiationDossier, "Empire information" },
            { NegotiationPressure, "Pressure" },
            { NegotiationMyTerms, "Terms you can offer" },
            { NegotiationTheirTerms, "Terms you can ask for" },
            { NegotiationFilters, "Categories" },
            { NegotiationTerms, "Terms" },
            { NegotiationContract, "The deal" },
            { NegotiationContractBoth, "Both empires" },
            { NegotiationContractMine, "You give" },
            { NegotiationContractTheirs, "They give" },
            { NegotiationApproval, "Deal approval" },
            { NegotiationApprovalValue, "{0} percent" },
            { NegotiationActions, "Offer" },
            { NegotiationQuantity, "Quantity" },
            { ScreenMinorDiplomacy, "Minor faction diplomacy" },
            { MinorIdentity, "The faction" },
            { MinorRelation, "Relation" },
            { MinorGains, "What you gain" },
            { ScreenPirateDiplomacy, "Pirate diplomacy" },
            { PiratePower, "Pirate power" },
            { PirateStanding, "Standing" },
            { PirateNextFleet, "Next pirate fleet" },
            { ScreenAcademyDiplomacy, "Academy diplomacy" },
            { ScreenAcademyModal, "The Academy" },
            { AcademyRelation, "Relation" },
            { AcademyNamedShips, "Named ships" },
            { DiplomacyActionsBand, "Actions" },
            { DiplomacyTreasury, "Treasury" },
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
