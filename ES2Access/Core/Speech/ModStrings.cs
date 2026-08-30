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

        /// <summary>The last join of a spoken enumeration - the one place a list stops being commas
        /// and takes a conjunction ("a, b, and c"), which every language does its own way and some do
        /// not do at all. Two forms because English joins a PAIR without the comma, and a translator
        /// whose language makes no such distinction writes the same shape twice. Both take the
        /// already-joined head and the last item, so the comma itself lives in the template rather
        /// than being glued on outside it (<see cref="SpokenList"/>).</summary>
        public const string ListPair = "speech.list-pair";
        public const string ListFinal = "speech.list-final";
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

        /// <summary>A box that only takes a number and carries its own stepper: the arrows change the
        /// value where an ordinary edit field's arrows would move a caret, so the role word has to say
        /// so before the player tries.</summary>
        public const string ControlNumericEditField = "control.numeric-edit-field";
        public const string ControlMenuItem = "control.menu-item";

        // The four things an edit of a text box says about itself. The game says none of them: it
        // draws a caret and nothing else, so entering, leaving, and every character between are
        // silent without these. "space" and "blank" are the two places in a line of text that have no
        // sound of their own - a space, and the empty place past the last character.
        public const string EditStarted = "edit.started";
        public const string EditCommitted = "edit.committed";
        public const string EditCancelled = "edit.cancelled";
        public const string EditCaretSpace = "edit.space";
        public const string EditCaretBlank = "edit.blank";

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
        public const string NavNoDetails = "nav.no-details";
        public const string NavTable = "nav.table";

        /// <summary>The name of the region holding a node's dossiers - the explanations the game
        /// draws for the thing rather than the things the player can do to it. A word of the mod's
        /// own: the game writes no caption over them, because for a mouse they are hovers rather
        /// than a block on the screen.</summary>
        public const string NodeTooltipsRegion = "node.tooltips-region";

        /// <summary>A caption and the figure under it read as one phrase, joined by a colon
        /// ("Flotilla 1: Short Range", "Planet Food production: 2 of 5"). For a row whose caption is
        /// not drawn beside the figure but supplied from elsewhere - a typed tooltip reader working
        /// from two of the game's own templates, a planet card's rating pips the prefab never
        /// captions: with a bare space the two run together as if the caption were part of the
        /// value. A connective, so the translator owns the punctuation.</summary>
        public const string CaptionedColon = "speech.captioned-colon";

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

        // The three columns a key-binding row is read as, and what the mod says when one of its
        // keys and one of the game's are the same chord.
        // The game draws no captions over the two key fields, so these three are the mod's own words.
        public const string NavKeyBindingAction = "nav.key-binding-action";
        public const string NavKeyBindingPrimaryColumn = "nav.key-binding-primary-column";
        public const string NavKeyBindingSecondaryColumn = "nav.key-binding-secondary-column";
        public const string NavKeyBindingShadowed = "nav.key-binding-shadowed";

        // A block's truthful answer when it holds nothing - generic on purpose (owner, 2026-08-28).
        public const string None = "none";

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

        /// <summary>The MOD's own options window - the same surface the game's Options is, drawn by
        /// the game and filled by the mod, so it needs a name of its own or arriving at it would
        /// announce the game's settings.</summary>
        public const string ScreenModSettings = "screen.mod-settings";

        /// <summary>The entry that opens it, on the main menu and the pause menu. The mod declares
        /// it; nothing is drawn for it, so these are the only words it has.</summary>
        public const string ModSettingsEntry = "mod-settings.entry";

        /// <summary>What the Scanner tab says about itself. The key-binding tab has no words of the
        /// mod's own at all: it wears the game's "%OptionToggleControlsTitle" and
        /// "%OptionToggleControlsDescription", so it matches the game's own Controls tab in every
        /// language (<see cref="ES2Access.UI.ModOptions.ModOptions.Categories"/>).</summary>
        public const string ModSettingsScanner = "mod-settings.scanner";
        public const string ModSettingsScannerDescription = "mod-settings.scanner-description";

        // --- the editor for the player's own three scanner categories (2026-08-23) ---

        /// <summary>The Scanner tab's button for one slot: which slot it is, and what is in it - the
        /// player's own name, or the word for a slot nobody has filled.</summary>
        public const string ScannerEditSlotButton = "mod-settings.scanner.slot-button";
        public const string ScannerEditEmpty = "mod-settings.scanner.empty";

        /// <summary>The name box at the top of a slot's own tab. Typing a name into an empty slot is
        /// what fills it.</summary>
        public const string ScannerEditName = "mod-settings.scanner.name";

        /// <summary>One keyword's box, numbered by its place in the list - which is the order its
        /// columns come out in.</summary>
        public const string ScannerEditKeyword = "mod-settings.scanner.keyword";

        /// <summary>A built-in category inside a slot: what the scanner calls it, and how many of its
        /// columns this custom category draws from.</summary>
        public const string ScannerEditSelected = "mod-settings.scanner.selected";

        /// <summary>A column the player's category still points at that this galaxy has nothing of -
        /// offered ticked so it can be taken off, since the scanner silently skips it every press.
        /// </summary>
        public const string ScannerEditMissing = "mod-settings.scanner.missing";

        public const string ScannerEditAddKeyword = "mod-settings.scanner.add-keyword";

        /// <summary>What the mod says after an edit that takes the control the player is standing on
        /// away with it - a keyword removed, a slot cleared. Queued rather than interrupting, so the
        /// place the cursor lands next is heard after it rather than instead of it.</summary>
        public const string ScannerEditRemoved = "mod-settings.scanner.removed";
        public const string ScannerEditCleared = "mod-settings.scanner.cleared";

        /// <summary>The three refusals. All keep what was there: a name already in the cycle would be
        /// two categories the player cannot tell apart, a word already asked for would be two columns
        /// holding the same things, and a category with no name is one the cycle reads as silence.
        /// </summary>
        public const string ScannerEditNameTaken = "mod-settings.scanner.name-taken";
        public const string ScannerEditKeywordTaken = "mod-settings.scanner.keyword-taken";
        public const string ScannerEditNameBlank = "mod-settings.scanner.name-blank";

        public const string ScannerEditClear = "mod-settings.scanner.clear";

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
        public const string TutorialBar = "tutorial.bar";
        public const string TutorialMinimize = "tutorial.minimize";
        public const string TutorialShowLocation = "tutorial.show-location";
        public const string TutorialClose = "tutorial.close";

        // The galaxy: the controls the game draws as icons and never names, and the shapes its
        // numbers are spoken in.
        public const string GalaxyTurn = "galaxy.turn";
        public const string GalaxyEndTurn = "galaxy.end-turn";
        public const string GalaxyApplyMovements = "galaxy.apply-movements";
        public const string GalaxyNextIdleFleet = "galaxy.next-idle-fleet";
        public const string GalaxyIdleFleets = "galaxy.idle-fleets";
        public const string GalaxyGameMenu = "galaxy.game-menu";

        /// <summary>The button the scan view offers when notifications are queued up behind it. The game
        /// draws it as a bare icon whose tooltip is a sentence about what a click would do, so there is
        /// no name of the game's to use.</summary>
        public const string GalaxyPendingNotifications = "galaxy.pending-notifications";

        /// <summary>The clock of real time the game can draw above the End Turn button. The game's only
        /// words for it are the OPTION that switches it on ("Display In-Game Clock"), which names a
        /// setting rather than the reading, so the caption here is the mod's.</summary>
        public const string GalaxyRealTimeClock = "galaxy.real-time-clock";

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
        public const string GalaxyManageSystem = "galaxy.manage-system";

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
        // them: there is no line to number, so the leg is named by where the fleet is GOING. Only where
        // it is going, because that is the half the map itself shows - a selected fleet's committed
        // path is drawn ahead of it and nothing anywhere draws where it came from. A whole phrase,
        // because "to" is the sentence rather than a word slotted into it, and one more for a
        // destination the map has not named, which is the same silence a lane running into the dark
        // keeps.
        public const string GalaxyFleetFreeMovingTo = "galaxy.fleet-free-moving-to";
        public const string GalaxyFleetFreeMovingToUnexplored =
            "galaxy.fleet-free-moving-to-unexplored";

        /// <summary>The same for a fleet flying a STARLANE whose far end the map has not named - the
        /// other way a fleet under way can have no system in this tree to hang under. A separate
        /// phrase because the two are different pictures: one is a line on the map running into the
        /// dark, the other is a fleet striking out where there is no line at all.</summary>
        public const string GalaxyFleetOnLaneToUnexplored = "galaxy.fleet-on-lane-to-unexplored";

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

        // Who holds a colony at a system several empires share. The label draws this as a row of
        // little bars tinted with each empire's colour and writes nothing at all on them - no
        // number, no name, not even a tooltip - so the caption is the mod's and the names inside it
        // are the game's own.
        public const string GalaxySystemEmpireBars = "galaxy.system-empire-bars";

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

        // The one stretch of sky the map has no name for. Every system on the map hangs under the
        // constellation it stands in, and a constellation nobody has explored is one the map draws no
        // name across - so the systems in all of them share a single group, and what to call THAT is
        // the mod's, because the game says nothing there at all.
        public const string GalaxyConstellationUnexplored = "galaxy.constellation-unexplored";

        // A place the map is drawing a star at and refusing to name: no label, no dossier, no star
        // type, nothing but a generic body and the ring around it. The game writes no text there at
        // all, so what the row calls itself is the mod's - and it is deliberately the same words for
        // every one of them, because telling two of them apart is what their coordinates are for.
        public const string GalaxySystemUnexplored = "galaxy.system-unexplored";

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

        // How far a colony's influence reaches, and which way it is going next turn. The map draws the
        // reach as a disk and the change as a second, fainter edge outside it, and writes no number on
        // either - so the sentence around the game's own two figures is the mod's. Three forms rather
        // than a direction word glued onto a number, because which way it is going is the whole point
        // of the second figure; the steady one still says the number, since a radius that is not moving
        // is exactly what a player planning a colony wants to hear.
        public const string GalaxySystemInfluenceGrowing = "galaxy.system-influence-growing";
        public const string GalaxySystemInfluenceShrinking = "galaxy.system-influence-shrinking";
        public const string GalaxySystemInfluenceSteady = "galaxy.system-influence-steady";

        // Whose influence has won a place, where that is not the empire holding it - the one thing on
        // the map that takes a system from its owner without a shot, and the reason a colony ship is
        // refused at an empty one. The map says it by painting the place another empire's colour, so
        // every word of it is the mod's, and the player's own empire is called what the mod calls it
        // everywhere else rather than by its name.
        public const string GalaxySystemInfluencedBy = "galaxy.system-influenced-by";
        public const string GalaxySystemInfluencedByYou = "galaxy.system-influenced-by-you";

        // ...and who else is reaching for the same place without having won it, which is the contest
        // still in progress. One line however many they are: the single form, the player alone, and the
        // list form, whose first slot is the empires before the last one - a complete sentence per
        // shape rather than a conjunction glued between names, so a translator can put the joining
        // word where their language wants it.
        public const string GalaxySystemInfluenceContested = "galaxy.system-influence-contested";
        public const string GalaxySystemInfluenceContestedList =
            "galaxy.system-influence-contested-list";
        public const string GalaxySystemInfluenceContestedYou =
            "galaxy.system-influence-contested-you";

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

        /// <summary>The eight headings BETWEEN the eight above, for a surface that aims rather than
        /// describes: a probe is launched down a bearing the player chooses, and eight choices leave
        /// a quarter of the map unreachable while sixteen cover it. Nothing that merely NAMES a
        /// direction uses them - a lane said as "north-northeast" is harder to hear and no more
        /// useful than one said as "north" (<see cref="CompassDirections.KeyForBearing16"/>).</summary>
        public const string DirectionNorthNorthEast = "direction.north-northeast";
        public const string DirectionEastNorthEast = "direction.east-northeast";
        public const string DirectionEastSouthEast = "direction.east-southeast";
        public const string DirectionSouthSouthEast = "direction.south-southeast";
        public const string DirectionSouthSouthWest = "direction.south-southwest";
        public const string DirectionWestSouthWest = "direction.west-southwest";
        public const string DirectionWestNorthWest = "direction.west-northwest";
        public const string DirectionNorthNorthWest = "direction.north-northwest";

        /// <summary>The same question answered as the two COMPONENTS of the offset instead of as one
        /// compass word - "23 south", "1 west, 23 south" (<see cref="CompassDirections.Offsets"/>).
        /// One whole phrase per axis rather than a number glued to a direction word, because the
        /// number and the word agree in most languages; the value is always positive, the sign having
        /// already chosen which of the four templates is used.</summary>
        public const string OffsetNorth = "offset.north";
        public const string OffsetSouth = "offset.south";
        public const string OffsetEast = "offset.east";
        public const string OffsetWest = "offset.west";

        /// <summary>Where a place on the map is, as a pair of whole numbers measured from the empire's
        /// home system (<see cref="MapCoordinates"/>). The game draws the galaxy and writes no
        /// coordinate anywhere, so the pair is the mod's - and it is ONE template rather than a number
        /// said twice, because which axis comes first, and what separates them, is a language's
        /// business.</summary>
        public const string GalaxyCoordinatePair = "galaxy.coordinates";

        /// <summary>The galaxy in one sentence: the shape and size it was generated with, how far
        /// across it is, and where its middle lies from the empire's home system - the picture a
        /// sighted player takes in at a glance when the map first opens and can never be told
        /// otherwise (<see cref="GalaxyMapText"/>). One template rather than a chain of them because
        /// every join in it is punctuation a language chooses for itself, and the shape and size words
        /// are the GAME's own, dropped in whole.</summary>
        public const string GalaxyMapSummary = "galaxy.map-summary";

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

        // How far the modified arrow carried the cursor, said before the cell it landed on and only
        // where it passed over anything: the number is how the player hears that the next thing on
        // this bearing was a long way off rather than next door.
        public const string GalaxyInspectSkippedOne = "galaxy.inspect.skipped-one";
        public const string GalaxyInspectSkippedMany = "galaxy.inspect.skipped-many";

        // Which stretch of sky the cell is in, said ONLY when it changes - on the way in where the
        // cursor opens inside one, and afterwards on the press that crosses a boundary. There are
        // exactly two things a crossing can be: arriving somewhere named, and leaving the last named
        // place for the space between them, which is why the second names the constellation being LEFT
        // and not the nothing being entered. Whole phrases, because a language may want the word for
        // "constellation" in front of the name or inflected by it.
        public const string GalaxyInspectConstellation = "galaxy.inspect.constellation";
        public const string GalaxyInspectConstellationLeft = "galaxy.inspect.constellation-left";

        // Whose influence the cell is standing in, said on the crossing exactly as the constellation
        // above is. Three states and not two, because a cell is an AREA: provably all one empire's,
        // partly somebody's - which is what a border sounds like - and out of influenced space
        // altogether, which names what was left for the same reason the constellation's does. The list
        // forms carry the joining word inside the sentence; there is no list form of "in", because a
        // cell proved to be wholly one empire's has exactly one owner.
        public const string GalaxyInspectInfluenceIn = "galaxy.inspect.influence-in";
        public const string GalaxyInspectInfluenceInYou = "galaxy.inspect.influence-in-you";
        public const string GalaxyInspectInfluenceEdge = "galaxy.inspect.influence-edge";
        public const string GalaxyInspectInfluenceEdgeYou = "galaxy.inspect.influence-edge-you";
        public const string GalaxyInspectInfluenceEdgeList = "galaxy.inspect.influence-edge-list";
        public const string GalaxyInspectInfluenceOut = "galaxy.inspect.influence-out";
        public const string GalaxyInspectInfluenceOutYou = "galaxy.inspect.influence-out-you";
        public const string GalaxyInspectInfluenceOutList = "galaxy.inspect.influence-out-list";

        // The map's SCANNER - "what is near me, of this kind". The scope is said as a CATEGORY and a
        // SUBCATEGORY, in that order, because that is the shape of the two keys that move it: one
        // steps the category, the other the subcategory, and a player who hears the two halves named
        // apart knows which key changed what. The composite is its own template so a language decides
        // what stands between the halves; both halves are whole localized labels, never fragments,
        // and the subcategory labels are kept PER CATEGORY so that a language whose adjectives agree
        // with the noun can inflect "friendly" for systems, fleets and probes separately.
        public const string GalaxyScannerScope = "galaxy.scanner.scope";
        public const string GalaxyScannerSystems = "galaxy.scanner.systems";
        public const string GalaxyScannerFleets = "galaxy.scanner.fleets";
        public const string GalaxyScannerProbes = "galaxy.scanner.probes";
        public const string GalaxyScannerSystemsAll = "galaxy.scanner.systems-all";
        public const string GalaxyScannerSystemsFriendly = "galaxy.scanner.systems-friendly";
        public const string GalaxyScannerSystemsNeutral = "galaxy.scanner.systems-neutral";
        public const string GalaxyScannerSystemsEnemy = "galaxy.scanner.systems-enemy";
        public const string GalaxyScannerFleetsAll = "galaxy.scanner.fleets-all";
        public const string GalaxyScannerFleetsFriendly = "galaxy.scanner.fleets-friendly";
        public const string GalaxyScannerFleetsNeutral = "galaxy.scanner.fleets-neutral";
        public const string GalaxyScannerFleetsEnemy = "galaxy.scanner.fleets-enemy";
        public const string GalaxyScannerProbesAll = "galaxy.scanner.probes-all";
        public const string GalaxyScannerProbesFriendly = "galaxy.scanner.probes-friendly";
        public const string GalaxyScannerProbesNeutral = "galaxy.scanner.probes-neutral";
        public const string GalaxyScannerProbesEnemy = "galaxy.scanner.probes-enemy";

        /// <summary>The three subcategories the star systems have that the affiliation trio cannot
        /// express: an empire's capital, which is a fact about a system as well as about who holds it;
        /// the systems a minor faction lives on, which are neutral AND findable as theirs; and the
        /// phenomena the map draws among the stars, which nobody holds at all.</summary>
        public const string GalaxyScannerSystemsHomeworld = "galaxy.scanner.systems-homeworld";
        public const string GalaxyScannerSystemsMinorFactions =
            "galaxy.scanner.systems-minor-factions";
        public const string GalaxyScannerSystemsSpecial = "galaxy.scanner.systems-special";

        /// <summary>The three categories that are only ever asked "what is there": each has a single
        /// subcategory, and it is still named rather than left blank, so a subcategory press answers
        /// with the same kind of word there as everywhere else.</summary>
        public const string GalaxyScannerQuestMarkers = "galaxy.scanner.quest-markers";
        public const string GalaxyScannerQuestMarkersAll = "galaxy.scanner.quest-markers-all";
        public const string GalaxyScannerPins = "galaxy.scanner.pins";
        public const string GalaxyScannerPinsAll = "galaxy.scanner.pins-all";
        public const string GalaxyScannerProjectiles = "galaxy.scanner.projectiles";
        public const string GalaxyScannerProjectilesAll = "galaxy.scanner.projectiles-all";

        /// <summary>Squares inside the player's own influence that somebody else's field is winning -
        /// where a border is being pushed back, which the map draws as one colour over another and
        /// writes no word about. Only ever asked "what is there", so its one subcategory is "all".
        /// </summary>
        public const string GalaxyScannerContestedInfluence = "galaxy.scanner.contested-influence";
        public const string GalaxyScannerContestedInfluenceAll =
            "galaxy.scanner.contested-influence-all";

        /// <summary>What a found square is CALLED. A square of sky has no name of its own, so it is
        /// named by the system whose ground it is - the whole sentence, because "near" is a word a
        /// language puts where it wants it.</summary>
        public const string GalaxyScannerNear = "galaxy.scanner.near";

        /// <summary>A scope the player is parked in that has run out under them. The scope's own name
        /// goes INSIDE the sentence rather than in front of it, so a language that says "there are no
        /// friendly fleets" has somewhere to put the words.</summary>
        public const string GalaxyScannerEmpty = "galaxy.scanner.empty";

        /// <summary>What is said instead of the offset components when the thing the scanner found is
        /// where the player is already reading from.</summary>
        public const string GalaxyScannerHere = "galaxy.scanner.here";

        // --- scanner categories (2026-08-22) ---

        /// <summary>The worlds a player could settle, and the two senses of it: one standing free,
        /// one somebody else is already on that this empire could take.</summary>
        public const string GalaxyScannerColonizable = "galaxy.scanner.colonizable";
        public const string GalaxyScannerColonizableUnoccupied =
            "galaxy.scanner.colonizable-unoccupied";
        public const string GalaxyScannerColonizableOccupied = "galaxy.scanner.colonizable-occupied";

        /// <summary>Every way out of the known map - a lane or a wormhole whose far end the player has
        /// not seen.</summary>
        public const string GalaxyScannerUnexplored = "galaxy.scanner.unexplored";
        public const string GalaxyScannerUnexploredAll = "galaxy.scanner.unexplored-all";

        /// <summary>One of those ways out, named from the end the player CAN see - the place it goes
        /// has no name yet, which is the whole of what makes it unexplored. The number is the system's
        /// own lane numbering, clockwise from north, the same one the tree says.</summary>
        public const string GalaxyScannerUnexploredLane = "galaxy.scanner.unexplored-lane";
        public const string GalaxyScannerUnexploredWormhole = "galaxy.scanner.unexplored-wormhole";

        // What has been found on the worlds. Each of these four categories names its own "all"; every
        // other subcategory it has is a KIND the game itself names, so there is no key for one.
        public const string GalaxyScannerAnomalies = "galaxy.scanner.anomalies";
        public const string GalaxyScannerAnomaliesAll = "galaxy.scanner.anomalies-all";
        public const string GalaxyScannerCuriosities = "galaxy.scanner.curiosities";
        public const string GalaxyScannerCuriositiesAll = "galaxy.scanner.curiosities-all";

        /// <summary>The two questions asked of a curiosity beyond what KIND it is (owner ruling
        /// 2026-08-23): whether an expedition could be sent to it at all, and whether the one thing
        /// stopping that is the empire's expedition power - the refusal the card draws a padlock for
        /// (<c>PlanetCuriosityItem.ShowLockIfNeeded</c>). The game keeps no TITLE for either: the
        /// failure it writes is a whole sentence (<c>%FailureEmpireExpeditionPowerTooLowDescription</c>)
        /// and there is no matching Title key, so the column names are the mod's own.</summary>
        public const string GalaxyScannerCuriositiesExplorable =
            "galaxy.scanner.curiosities-explorable";
        public const string GalaxyScannerCuriositiesLowPower =
            "galaxy.scanner.curiosities-low-power";
        public const string GalaxyScannerLuxury = "galaxy.scanner.luxury";
        public const string GalaxyScannerLuxuryAll = "galaxy.scanner.luxury-all";
        public const string GalaxyScannerStrategic = "galaxy.scanner.strategic";
        public const string GalaxyScannerStrategicAll = "galaxy.scanner.strategic-all";

        /// <summary>How a thing found on a world is named in the column that holds every kind of them
        /// at once: the kind, and the world it is on. In a column that IS one kind the row is the
        /// world alone - the column has already said the rest.</summary>
        public const string GalaxyScannerOnPlanet = "galaxy.scanner.on-planet";

        // --- the player's own scanner categories (2026-08-23) ---

        /// <summary>What an empty slot is called once the player fills it, numbered the way its two
        /// keys are.</summary>
        public const string GalaxyScannerCustomName = "galaxy.scanner.custom-name";

        /// <summary>A custom category's first column: everything it caught, however it was caught.
        /// Its own word rather than a built-in category's, because it is the one column of the one
        /// taxonomy the player wrote.</summary>
        public const string GalaxyScannerCustomAll = "galaxy.scanner.custom-all";

        /// <summary>What one of the six quick keys says when the slot it answers for is empty. It
        /// names the KEY, which the mod reads off the live binding - so a player who moved the key
        /// hears the key they pressed.</summary>
        public const string GalaxyScannerNoCustom = "galaxy.scanner.no-custom";

        /// <summary>How many people a world would ever hold. The game draws this beside an icon and
        /// writes the words nowhere, so they are the mod's.</summary>
        public const string GalaxyScannerMaxPopulation = "galaxy.scanner.max-population";

        /// <summary>One of a world's five outputs and how much of it - the name is the game's own
        /// title for the property, the number the game's own formatting, and this is only the order
        /// they go in.</summary>
        public const string GalaxyScannerOutput = "galaxy.scanner.output";

        // --- end scanner categories ---
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

        /// <summary>The group the probe mode adds to the system a probe is being launched from - the
        /// sixteen compass bearings, offered beside that system's starlanes because the order takes any
        /// direction at all and the lanes are only the ones that lead somewhere - named by what the
        /// order can REACH: a probe flies its own speed for its own lifetime and then stops, so the
        /// number of units it will cover is the one fact that decides whether any of the bearings under
        /// it is worth taking.</summary>
        public const string GalaxyProbeLaunchReach = "galaxy.probe-launch-reach";

        /// <summary>What lies down one bearing, said as the stretches of it nobody has explored yet
        /// and how far the map itself goes that way (<see cref="ProbeContextText"/>). The game draws
        /// the fog and the map's rim and says neither in words, so all of it is the mod's.
        ///
        /// <see cref="GalaxyProbeContext"/> itself is only what the bearing ANNOUNCES - the heading and
        /// the one number a listener chooses on - because sixteen of these are walked one after another
        /// and the ranges that explain the number are a paragraph each. Every other template here is a
        /// LINE of the review buffer, said whole and on its own, which is why each of them reads as a
        /// complete clause rather than as something to glue.
        ///
        /// The distances are whole units along the bearing, measured from the system the probe leaves.
        /// A range is two of them; the last stretch is the one that runs off the map, and it takes a
        /// template of its own because "46 to the map edge at 58" is one clause where the others are
        /// pairs. The word for "unexplored" is said ONCE and the ranges follow it, which is what keeps
        /// a six-range answer hearable - so it lives in the template that INTRODUCES the list rather
        /// than in each range.</summary>
        public const string GalaxyProbeContext = "galaxy.probe-context";
        public const string GalaxyProbeContextUnexplored = "galaxy.probe-context-unexplored";
        public const string GalaxyProbeContextRange = "galaxy.probe-context-range";
        public const string GalaxyProbeContextToEdge = "galaxy.probe-context-to-edge";
        public const string GalaxyProbeContextEdge = "galaxy.probe-context-edge";
        public const string GalaxyProbeContextExplored = "galaxy.probe-context-explored";

        /// <summary>The share of everything a launch this way would reveal that the empire already has
        /// ({0}, the whole number of percent - <see cref="ProbeFootprint"/>). The one figure a listener
        /// comparing sixteen headings chooses on, so it is said twice: once in the bearing's own
        /// announcement (<see cref="GalaxyProbeContext"/>) and once as the first line of its buffer,
        /// where the ranges that explain it follow.</summary>
        public const string GalaxyProbeContextPercentExplored =
            "galaxy.probe-context-percent-explored";

        /// <summary>What is unexplored BESIDE the flight line rather than on it - the map a probe
        /// would uncover in passing without ever flying into it. A buffer line per side, following the
        /// flight line's own, and each names the side it is on, because "unexplored 2-40" and
        /// "unexplored alongside to the southwest 2-40" send a probe to two different places. Both
        /// sides are one line when they hold the same stretches. The ranges take
        /// <see cref="GalaxyProbeContextRange"/> like every other range and never fold into the map's
        /// edge: the clause before this one has already said where the edge is.</summary>
        public const string GalaxyProbeContextAlongside = "galaxy.probe-context-alongside";
        public const string GalaxyProbeContextAlongsideBoth =
            "galaxy.probe-context-alongside-both";

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

        /// <summary>Whose a fleet is and which way the player stands to them, as one phrase per
        /// standing rather than a standing word glued in front of a name - a language that inflects
        /// "enemy" for the owner's gender or case has to see the whole thing (<c>FleetPhrase</c>).
        /// A fleet of the player's own says none of these.
        /// </summary>
        public const string FleetOwnedEnemy = "fleet.owned-enemy";
        public const string FleetOwnedNeutral = "fleet.owned-neutral";
        public const string FleetOwnedFriendly = "fleet.owned-friendly";

        /// <summary>The hero riding with a fleet, named. Any fleet's, own or foreign: the game's own
        /// dossier draws a foreign hero's name with no ownership gate on it.</summary>
        public const string FleetHero = "fleet.hero";

        /// <summary>TWO OR MORE ships of one kind in a fleet - a count and the design's own name. A
        /// group of ONE has no template and no number: it is the design's name on its own (owner
        /// ruling 2026-08-26). The hero's own ship is one of these like any other and is not marked
        /// out; that a hero is aboard is <see cref="FleetHero"/>'s to say.</summary>
        public const string FleetShipGroup = "fleet.ship-group";
        public const string FleetRouteCancelledTo = "fleet.route-cancelled-to";
        public const string FleetRouteCancelled = "fleet.route-cancelled";

        // The notifications the MOD raises into the game's own pipeline, for the eight things the
        // game puts on the event bus and then tells nobody about. Each family has a TITLE - what is
        // spoken as it arrives, and what the popup writes across the top - and, where the game has a
        // second thing to say, a BODY sentence the popup shows under it. The "-plain" forms are the
        // same news with a place the map has not named left out, rather than a sentence with a hole
        // in it. Every popup body is prefixed with the turn it happened on, because a popup reached
        // by Previous/Next from a game notification otherwise carries no clue.
        public const string NotificationTurnPrefix = "notification.turn-prefix";

        public const string NotificationSystemRevealed = "notification.system-revealed";
        public const string NotificationSystemRevealedPlain = "notification.system-revealed-plain";

        /// <summary>The discovery with nothing known about it - what is left when the notification has
        /// outlived the node it was about (a reload replacing the assembly under a standing one is how
        /// this was first seen). The news is still true; only the name is gone.</summary>
        public const string NotificationSystemRevealedUnknown =
            "notification.system-revealed-unknown";
        public const string NotificationSystemRevealedBody = "notification.system-revealed-body";
        public const string NotificationSystemRevealedInConstellation =
            "notification.system-revealed-in-constellation";

        public const string NotificationFleetSighted = "notification.fleet-sighted";
        public const string NotificationFleetSightedNowhere = "notification.fleet-sighted-nowhere";
        public const string NotificationFleetSightedBody = "notification.fleet-sighted-body";
        public const string NotificationFleetSightedBodyNowhere =
            "notification.fleet-sighted-body-nowhere";

        public const string NotificationColonySighted = "notification.colony-sighted";
        public const string NotificationColonySightedBody = "notification.colony-sighted-body";

        public const string NotificationFleetDispatched = "notification.fleet-dispatched";
        public const string NotificationFleetDispatchedTo = "notification.fleet-dispatched-to";
        public const string NotificationFleetDispatchedPlain = "notification.fleet-dispatched-plain";

        public const string NotificationSystemBesieged = "notification.system-besieged";
        public const string NotificationSystemBesiegedPlain = "notification.system-besieged-plain";
        public const string NotificationSystemBesiegedBody = "notification.system-besieged-body";

        public const string NotificationSystemBlockaded = "notification.system-blockaded";
        public const string NotificationSystemBlockadedPlain = "notification.system-blockaded-plain";
        public const string NotificationSystemBlockadedBody = "notification.system-blockaded-body";

        public const string NotificationTradeBlockadeEnded = "notification.trade-blockade-ended";
        public const string NotificationTradeBlockadeEndedPlain =
            "notification.trade-blockade-ended-plain";

        public const string NotificationObliteratorFired = "notification.obliterator-fired";
        public const string NotificationObliteratorFiredPlain =
            "notification.obliterator-fired-plain";
        public const string NotificationObliteratorObserved = "notification.obliterator-observed";
        public const string NotificationObliteratorObservedPlain =
            "notification.obliterator-observed-plain";

        /// <summary>A fleet of the player's reaching the place it was sent to. The journey ENDING is
        /// also how a fleet stopped short reads, so the two are separate families with separate
        /// sentences - being stopped keeps the wording it already had
        /// (<see cref="FleetInterceptedAt"/>).</summary>
        public const string NotificationFleetArrived = "notification.fleet-arrived";
        public const string NotificationFleetArrivedPlain = "notification.fleet-arrived-plain";

        /// <summary>Somebody else's fleet going out of sight. Four whole sentences rather than one
        /// with holes in it: the fleet's name and the place it was last seen at are each things the
        /// map may not have known, and a language that inflects around either cannot be handed a
        /// fragment.</summary>
        public const string NotificationFleetLostSight = "notification.fleet-lost-sight";
        public const string NotificationFleetLostSightUnnamed =
            "notification.fleet-lost-sight-unnamed";
        public const string NotificationFleetLostSightNowhere =
            "notification.fleet-lost-sight-nowhere";
        public const string NotificationFleetLostSightUnnamedNowhere =
            "notification.fleet-lost-sight-unnamed-nowhere";

        /// <summary>Somebody else's fleet that was already in sight standing somewhere else when the
        /// turn came round. A fleet under way is at no place the map names, which is what the second
        /// and third forms are for.</summary>
        public const string NotificationForeignFleetMoved = "notification.foreign-fleet-moved";
        public const string NotificationForeignFleetMovedTo = "notification.foreign-fleet-moved-to";
        public const string NotificationForeignFleetMovedAway =
            "notification.foreign-fleet-moved-away";

        /// <summary>One of the player's own systems whose influence lost ground this turn - squares
        /// that were provably inside its reach and that a rival's field now wins. One sentence per
        /// system and taker however many squares changed: a border moving is one piece of news.
        /// </summary>
        public const string NotificationInfluenceGroundLost = "notification.influence-ground-lost";

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

        // What the four panels of the galaxy view are called when the player Tabs into one. The game
        // captions none of them - the map is the whole screen, the quest panel, the notification strip
        // and the zoom-and-lens cluster are drawn as bare icons and figures - so all four names are the
        // mod's own (owner wordings, 2026-08-19).
        public const string GalaxyMapPanel = "galaxy.map-panel";
        public const string HudQuestPanel = "hud.quest-panel";
        public const string HudNotificationsPanel = "hud.notifications-panel";
        public const string HudViewControlsPanel = "hud.view-controls-panel";

        /// <summary>The stop beside the notification strip holding the news the game raises no icon
        /// for - the mod's own notifications, grouped by the turn they happened on. Both words are the
        /// mod's: the game draws neither the list nor the grouping.</summary>
        public const string HudTurnLogPanel = "hud.turn-log-panel";
        public const string HudTurnLogTurn = "hud.turn-log-turn";

        /// <summary>The two "throw the whole list away" buttons, last in the notification strip's stop
        /// and last in the Turn log's (owner ruling 2026-08-23). Both names are the mod's: the game
        /// hangs the same action on a bare triangle behind its icons with no tooltip and no caption of
        /// any kind (measured 2026-08-23 - <c>BaseTriangleBackground</c> carries no
        /// <c>AgeTooltip</c> at all), and the Turn log is not drawn anywhere. They are two phrases and
        /// not one because they are two acts: the first is the game's own dismiss-everything, which
        /// takes the Turn log with it, and the second clears only the mod's own lines.</summary>
        public const string HudDismissAllNotifications = "hud.dismiss-all-notifications";
        public const string HudDismissAllTurnLog = "hud.dismiss-all-turn-log";


        // The rows of the empire cluster in the top-left corner, which is the first Tab stop on every
        // page in the game. Four unrelated things are stacked there and the game captions none of
        // them, so each row's name is the mod's own (owner wordings, 2026-08-19). The research row is
        // named by the word it already used, GalaxyResearch.
        public const string HudControlsPanel = "hud.controls-panel";
        public const string HudKeyResourcesPanel = "hud.key-resources-panel";
        public const string HudStrategicResourcesPanel = "hud.strategic-resources-panel";

        // The two faction rows of that cluster the game has no standalone name for. The other five are
        // named by the game's own titles (%NetEmpireLifeforceTitle, %AssimilationShortcutTitle,
        // %GoldenAgeTitle, %HonorTitle, %RelicsTitle - all five verified to resolve). These two do not
        // exist as a bare title anywhere in the corpus: "Singularities" is only ever inside a sentence
        // or as another screen's source label, and "Pirate Mark" only inside one
        // (%PirateMarkPanelTargetSystemTitle is "Mark Pirate Target", an instruction). So they are the
        // mod's own words, chosen to be exactly the game's (owner ruling 2026-08-19).
        public const string HudSingularitiesPanel = "hud.singularities-panel";
        public const string HudPirateMarkPanel = "hud.pirate-mark-panel";

        // The pause menu's icon-only toggle, and the word for a settings panel the game will only
        // show, not let you change.
        public const string GameMenuGameSettings = "gamemenu.game-settings";
        public const string GameMenuReadOnlySettings = "gamemenu.read-only-settings";

        // The save page: the name field, the cloud toggle, and what an empty cell of the save table
        // says.
        public const string LoadSaveSaveName = "loadsave.save-name";
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
        public const string DragStarted = "drag.started";

        /// <summary>The same announcement where no chord can be spelled at all (a test, boot, a host
        /// with no keyboard): what is held, and nothing promised about keys.</summary>
        public const string DragStartedPlain = "drag.started-plain";

        public const string DragDropped = "drag.dropped";
        public const string DragDropRefused = "drag.drop-refused";
        public const string DragCancelled = "drag.cancelled";

        /// <summary>What a queue line says when the thing that was carried lands on it: which item
        /// moved, and the position number the player will hear the line read back with.</summary>
        public const string DragMovedToPosition = "drag.moved-to-position";

        /// <summary>What a control says while it would take the thing the player is holding.</summary>
        public const string DragDropTarget = "drag.drop-target";

        /// <summary>What a control the player could pick something up from says while nothing is being
        /// carried - one of the few things the readout says a control HAS rather than what it is. Not
        /// said while something IS held: the useful fact about a control then is whether the thing can
        /// go there.</summary>
        public const string DragDraggable = "drag.draggable";

        /// <summary>The two DERIVED usage hints every draggable surface gets (<c>CarryState.HintLines</c>):
        /// what this control would hand over, named in the source's own words with its quantity, and
        /// where what is held can be put down. <c>{0}</c> is the chord, spelled by the same renderer the
        /// declared hints use, and <c>{1}</c> the thing.</summary>
        public const string DragHint = "drag.drag-hint";

        public const string DragDropHint = "drag.drop-hint";

        // Putting something in a queue and taking it out again - the system's construction queue and
        // the empire's research queue, which are the same gesture on two screens and so the same
        // words. The game writes no word for either outcome: a construction answers with a sound and
        // a flying icon, and a technology's own dot does swap to the game's "Queued" but only for the
        // player standing on that dot, and never for the queue LINE that has just gone. Three whole
        // phrases rather than a shared "Queued" with a fragment after it, so a language that frames
        // "first in the queue" differently has somewhere to do it.
        public const string QueueQueued = "queue.queued";
        public const string QueueQueuedFirst = "queue.queued-first";
        public const string QueueCancelled = "queue.cancelled";

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
        /// page a system or a planet opens onto. The rung and how many there are, said on its own so
        /// that the band name below can follow it or not. The game writes nothing comparable of its
        /// own.</summary>
        public const string ZoomLevel = "zoom.level";

        // What the map is DRAWING at a rung, which is the thing a rung number does not say: the game
        // groups its thirteen zoom steps into layers and swaps whole map at each boundary. The
        // grouping is the game's (`GalaxyViewCameraController.LayerDescriptorNamesByZoomIndex`); the
        // words are the mod's, because that table holds asset names - "InformativeGalaxyLayer" - and
        // the game shows the player none of them. Owner wordings, 2026-08-20. The furthest-out step
        // is the painted backdrop rather than a map and is deliberately given no word at all.
        public const string ZoomBandGalaxyMap = "zoom.band.galaxy-map";
        public const string ZoomBandInformativeGalaxy = "zoom.band.informative-galaxy";
        public const string ZoomBandConstellation = "zoom.band.constellation";
        public const string ZoomBandSystems = "zoom.band.systems";
        public const string ZoomBandSystem = "zoom.band.system";
        public const string ZoomBandSystemOverview = "zoom.band.system-overview";

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

        /// <summary>The star-system page names itself after the system it is showing: the drawn system
        /// name and the game's own word for the page ("Heka, System management"), so that turning the
        /// page says which system the player is now on. A connective, not a phrase - both halves are
        /// somebody else's words.</summary>
        public const string ScreenStarSystemNamed = "screen.star-system-named";
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

        /// <summary>The box the star-system page draws its own heading over - the SYSTEM's name, which
        /// is what the box is about. The game keeps no title key for it at all
        /// (<c>%StarSystemSideInfoDescription</c> is the sentence on its header icon), so the word is
        /// the mod's, and it is "System" rather than "Colony" because the box is drawn for an outpost
        /// and a ghost system too (owner ruling 2026-08-22).</summary>
        /// <summary>The one stop the left-edge information panels share. It has to be a word of its own
        /// rather than any panel's: naming it after the colony panel would announce "System, System,
        /// System level 2" on every Tab into it, because that panel is also the first REGION inside it.
        /// The game titles the group nothing at all - it draws four unlabelled boxes down an edge - so
        /// the phrase is the mod's.</summary>
        public const string SystemSidePanels = "system.side-panels";

        public const string SystemColonyPanel = "system.colony-panel";
        public const string SystemPopulationPanel = "system.population-panel";

        /// <summary>What the spaceport side panel is called - the game draws the word as the panel's
        /// own title but writes no name the stop could be read off, so it is a mod string carrying the
        /// game's word (owner-approved 2026-08-29).</summary>
        public const string SystemSpaceportPanel = "system.spaceport-panel";
        public const string SystemRepresentativesPanel = "system.representatives-panel";
        public const string SystemOutpostPanel = "system.outpost-panel";
        public const string SystemGovernorPanel = "system.governor-panel";
        public const string SystemSanctuaryPopulationPanel = "system.sanctuary-population-panel";
        public const string SystemSanctuaryPanel = "system.sanctuary-panel";

        /// <summary>The box that says where a ghost empire's ships and people appear. The game heads
        /// both of its rows with its own words for the thing ("Sanctuary Link:") and writes no title
        /// for the box, so the name carries that word, as the two panels above it do.</summary>
        public const string SystemSanctuaryLinksPanel = "system.sanctuary-links-panel";
        public const string SystemConstructiblesPanel = "system.constructibles-panel";
        public const string SystemQueuePanel = "system.queue-panel";
        public const string SystemHangarPanel = "system.hangar-panel";

        // The buttons the page's cards and queue lines draw as bare icons, which the game names
        // nowhere, and what the mod says about an action it carried out.
        public const string SystemColonize = "system.colonize";
        public const string SystemRenamePlanet = "system.rename-planet";
        public const string SystemRenameSystem = "system.rename-system";
        public const string SystemPopulationMoved = "system.population-moved";

        /// <summary>One slot of a colony's population ring: where it is round the ring, and whether
        /// the game is drawing anybody in it. The ring itself is a picture with no words at all, and
        /// which BAND a slot is in - ordinary, overpopulated, locked - is said by the region it is
        /// read in, in the game's own words, so these two carry the position alone. Complete phrases
        /// rather than a position with an adjective glued on: "empty" agrees with its noun in half the
        /// languages this mod ships.</summary>
        public const string SystemPopulationSlot = "system.population-slot";
        public const string SystemPopulationSlotEmpty = "system.population-slot-empty";

        /// <summary>The same slot on a world NOBODY has settled, where every slot of the ring is empty
        /// and they are all one band - so the row's position in that band IS its rank, and the reading
        /// says the position once instead of twice.</summary>
        public const string SystemPopulationSlotVacant = "system.population-slot-vacant";

        public const string SystemImprovements = "system.improvements";
        public const string SystemLevel = "system.level";

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

        /// <summary>
        /// What the six zoom-in fleet actions really do, said on the button that carries them.
        ///
        /// None of these six ORDERS anything: the game's button selects the fleet's own system and
        /// brings the camera in, because the order is a control drawn INSIDE the zoomed system - a
        /// planet's colonize or destroy button, a curiosity in orbit, a probe site, the wreck. A
        /// sighted player watches the map fly in and finds the target under the pointer; a keyboard
        /// player heard a button that appeared to do nothing. So each button says where pressing it
        /// puts the cursor, and the page puts it there.
        /// </summary>
        public const string FleetsActionSeatsColonize = "fleets.action-seats-colonize";
        public const string FleetsActionSeatsDestroy = "fleets.action-seats-destroy";
        public const string FleetsActionSeatsExpedition = "fleets.action-seats-expedition";
        public const string FleetsActionSeatsProbeSite = "fleets.action-seats-probe-site";
        public const string FleetsActionSeatsWreck = "fleets.action-seats-wreck";
        public const string FleetsActionSeatsTerraform = "fleets.action-seats-terraform";
        public const string FleetsActionSeatsRestore = "fleets.action-seats-restore";
        public const string FleetsActionSeatsReduceAnomaly = "fleets.action-seats-reduce-anomaly";

        // The first-visit cutscene. It draws no heading of its own, so the mod says which system is
        // being shown; everything about each planet comes from the card in the game's own words.
        public const string ScreenSystemDiscovery = "screen.system-discovery";
        public const string DiscoverySystem = "discovery.system";

        /// <summary>The rename box the game opens over a page.</summary>
        public const string ScreenRename = "screen.rename";

        // The research screen - the wheel of technologies the game draws over the galaxy. The three
        // panel names are the mod's, because the game labels none of them; the quadrants, the stages
        // and every technology on them are named by the game.
        public const string ScreenResearch = "screen.research";
        public const string ResearchStatusPanel = "research.status-panel";
        public const string ResearchKeyPanel = "research.key-panel";
        public const string ResearchTreePanel = "research.tree-panel";

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

        // A law out of the backing party's reach draws where that party stands as a notch on a bar
        // with no numbers on it, under the game's own caption for what the law REQUIRES ("Required
        // Political experience:"). The caption for the drawn notch is the mod's, phrased on that one;
        // its two slots are the tier the notch sits in, in the game's own word for it, and how far
        // along the bar it is drawn, already written as a share ("20%").
        public const string PoliticsCurrentExperience = "politics.current-experience";

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

        /// <summary>The panel a systems-table cell slides out under the table, arriving and leaving.
        /// Nothing else would say so: the page does not change, and a whole tab stop's worth of content
        /// appears and disappears under the player with no sound but Tab taking longer to come round -
        /// the same reason the fleet panel announces itself. Each phrase names the panel with the word
        /// the player will then hear as the region they are walking, and the system whose row was
        /// clicked. The construction cell opens the game's constructibles and queue panels side by side
        /// and they are ONE thing to the player, so they share one phrase.</summary>
        public const string EmpirePlanetsPanelOpened = "empire.planets-panel-opened";
        public const string EmpirePlanetsPanelClosed = "empire.planets-panel-closed";
        public const string EmpireConstructionPanelOpened = "empire.construction-panel-opened";
        public const string EmpireConstructionPanelClosed = "empire.construction-panel-closed";
        public const string EmpireHangarPanelOpened = "empire.hangar-panel-opened";
        public const string EmpireHangarPanelClosed = "empire.hangar-panel-closed";

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

        /// <summary>The names of the deck editor's two panels, said on the Tab into each. The list of
        /// unlocked tactics has no drawn heading at all - only the count sentence over it, which is
        /// not a name - so its word is the mod's. The set below DOES draw one, and the drawn word is
        /// what is used; this is the fallback for a build that hides it.</summary>
        public const string TacticsAvailablePanel = "tactics.available-panel";
        public const string TacticsDeckPanel = "tactics.deck-panel";

        // The hero page and the galaxy-wide hero list its Academy box opens. Both screen names are
        // fallbacks for the frames before the window has written its own heading. The third is for the
        // band of hero actions the page draws along its bottom with no caption of its own.
        public const string ScreenAcademy = "screen.academy";
        public const string AcademyHeroActions = "academy.hero-actions";
        public const string ScreenHeroCompleteList = "screen.hero-complete-list";

        // The ship designer. Its heading is the game's own and is declared where it is drawn, so the
        // only mod words here are for the bands the game draws with no caption at all - the module slots
        // it draws over the rendered ship, and the two halves of the module band (the switches that
        // decide what is listed, and the list itself) - for a module having been fitted into one or
        // taken out again, which is the mod's own drag ending and has no game sentence, and for the
        // state of a slot itself. What a slot ACCEPTS has no key here: the categories are read out as
        // the GAME's own titles for them, in the game's own list separators.
        public const string ShipDesignSlots = "ship-design.slots";

        /// <summary>The two halves of a bar-of-switches-over-a-grid band, which several panels draw and
        /// none of them caption: these two are the words for ALL of them (the module band and the star
        /// system's constructibles), because a player who has learned the pair on one panel should not
        /// meet a synonym on the next. The key names are the band they were first written for. The
        /// hangar's bar is NOT one of these: its buttons are commands, so its word is "Actions"
        /// (DiplomacyActionsBand, owner-ruled 2026-08-18) while its ships keep "Available".</summary>
        public const string ShipDesignFilters = "ship-design.filters";
        public const string ShipDesignAvailable = "ship-design.available";
        public const string ShipDesignModuleFitted = "ship-design.module-fitted";
        public const string ShipDesignModuleRemoved = "ship-design.module-removed";
        public const string ShipDesignSlotEmpty = "ship-design.slot-empty";

        /// <summary>How much of a fitted module's effect a slot applies - the one slot marker whose
        /// words are the mod's rather than the game's. The game draws the fact as two, three or four
        /// DOTS and titles it "{0} Multiplier" (<c>%PanelFeatureSlotMultiplierTitle</c>), which read
        /// aloud after the slot's other markers is a bare number in front of a noun; the phrase names
        /// the multiplication instead (owner ruling, 2026-08-19). A complete phrase, so a translator
        /// may put the number wherever the language needs it.</summary>
        public const string ShipDesignSlotMultiplier = "ship-design.slot-multiplier";
        public const string ShipDesignRemoveTarget = "ship-design.remove-target";

        // The battle popups and the two cinematics. Everything a battle SAYS on screen is the game's
        // own text; what is left here is the counting, the two sides' names and the things the game
        // draws as arcs and bars with no number anywhere on them.
        public const string ScreenBattle = "screen.battle";
        public const string ScreenGroundBattle = "screen.ground-battle";
        public const string ScreenAdvancedBattleReport = "screen.battle-report-advanced";
        /// <summary>The band of battle plans on the advanced setup, which Tab stops in on its own
        /// (the plans are the decision that window exists for). The game draws the hand as cards and
        /// gives the band no heading at all, so the word naming it is the mod's.</summary>
        public const string BattleTactics = "battle.tactics";

        public const string BattleYourFleets = "battle.your-fleets";
        public const string BattleEnemyFleets = "battle.enemy-fleets";

        /// <summary>The band of figures on the advanced setup - the four pages behind the wordless
        /// switches, which Tab now stops in on its own. Named for the same reason the two sides are:
        /// a stop the player lands in says what it is.</summary>
        public const string BattleStats = "battle.stats";
        public const string BattleYourTroops = "battle.your-troops";
        public const string BattleEnemyTroops = "battle.enemy-troops";
        public const string BattleYourDamage = "battle.your-damage";
        public const string BattleEnemyDamage = "battle.enemy-damage";
        public const string BattleShowYourFleets = "battle.show-your-fleets";
        public const string BattleShowEnemyFleets = "battle.show-enemy-fleets";
        /// <summary>
        /// Which side is stronger and by how much, phrased FROM the stronger side: the fleet that
        /// leads, the percentage more military power it has, and the fleet it is being compared
        /// against. The game draws this as two arcs with no figure anywhere on them, and the arcs say
        /// "one of these is bigger" rather than "350 against 172" - so the sentence says the same
        /// thing the picture does, which two bare numbers left the listener to work out.
        ///
        /// A complete phrase with all three parts, so a language that puts the comparison first or
        /// inflects the fleet names can. The percentage is what the stronger side has ON TOP of the
        /// weaker one - two equal fleets are 0% and never a refusal to speak.
        /// </summary>
        public const string BattleBalance = "battle.balance";

        /// <summary>The same sentence for the one comparison a percentage cannot make: a side with
        /// no military power left at all, which a report of a battle that wiped a fleet out is full
        /// of. Dividing by it would answer with infinity, so the wipe-out is its own phrase.</summary>
        public const string BattleBalanceAll = "battle.balance-all";

        /// <summary>The same question on the ground, where the two arcs are sized from manpower
        /// rather than from military power - a different quantity, so a different sentence.</summary>
        public const string BattleGroundBalance = "battle.ground-balance";
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

        // The exchange of fire, which the game draws as tracer and explosion and writes down
        // nowhere. Each is a whole sentence rather than a stem plus fragments, because the count
        // and the damage kind sit inside the clause a translation has to inflect - and there is
        // one per shape the tallies can take, so the composer chooses a form and never builds one.
        public const string BattleFireEnergy = "battle.fire-energy";
        public const string BattleFireEnergyMany = "battle.fire-energy-many";
        public const string BattleFireProjectile = "battle.fire-projectile";
        public const string BattleFireProjectileMany = "battle.fire-projectile-many";
        public const string BattleFireMixed = "battle.fire-mixed";
        public const string BattleFireMixedMany = "battle.fire-mixed-many";
        public const string BattleFirePlain = "battle.fire-plain";
        public const string BattleFirePlainMany = "battle.fire-plain-many";
        public const string BattleFireAbsorbed = "battle.fire-absorbed";
        public const string BattleFireAbsorbedMany = "battle.fire-absorbed-many";
        public const string BattleFireMissed = "battle.fire-missed";
        public const string BattleFireMissedMany = "battle.fire-missed-many";
        public const string BattleFireMissedClause = "battle.fire-missed-clause";
        public const string BattleFireMissedClauseMany = "battle.fire-missed-clause-many";
        public const string BattleFireShieldClause = "battle.fire-shield-clause";

        // What arrives, mends and is awarded mid-fight - each read off the one instruction in the
        // replay stream that reports it.
        public const string BattleReinforcements = "battle.reinforcements";
        public const string BattleEnemyReinforcements = "battle.enemy-reinforcements";
        public const string BattleRepaired = "battle.repaired";
        public const string BattleEffectApplied = "battle.effect-applied";
        public const string BattleMedalEarned = "battle.medal-earned";
        public const string GroundBattleRound = "ground-battle.round";
        public const string GroundBattleBombardment = "ground-battle.bombardment";
        public const string GroundBattleAssault = "ground-battle.assault";
        public const string GroundBattleYourTroopLost = "ground-battle.your-troop-lost";
        public const string GroundBattleYourTroopsLost = "ground-battle.your-troops-lost";
        public const string GroundBattleEnemyTroopLost = "ground-battle.enemy-troop-lost";
        public const string GroundBattleEnemyTroopsLost = "ground-battle.enemy-troops-lost";

        /// <summary>The four pages of figures the advanced battle setup keeps behind four wordless
        /// switches. The game has a description per switch and no title for any of them, so the page
        /// names are the mod's - and the pages are the list the player walks, so these are what the
        /// rows are called.</summary>
        public const string BattleStatsTrajectories = "battle.stats-trajectories";
        public const string BattleStatsMilitary = "battle.stats-military";
        public const string BattleStatsDamage = "battle.stats-damage";
        public const string BattleStatsRange = "battle.stats-range";

        /// <summary>Where one flotilla will fight, for the trajectory page: the flotilla's number and
        /// the range, the latter already in the game's own words for it. A complete phrase, so a
        /// language that puts the range first can.</summary>
        public const string BattleFlotillaRange = "battle.flotilla-range";

        /// <summary>
        /// How one of the two damage rings is split between the fleets. The window sizes these from
        /// what each side's weapons of that type get THROUGH the other side's defences of that type,
        /// which the phrase says out loud - the ring means something narrower than "damage", and a
        /// listener told a bare percentage would read it as the wrong quantity.
        ///
        /// Both halves are always said, including a zero: an unopposed ring means nothing of the other
        /// side's is getting through, which is a fact worth hearing rather than a half-sentence.
        /// </summary>
        public const string BattleEnergyDamageShare = "battle.energy-damage-share";
        public const string BattleProjectileDamageShare = "battle.projectile-damage-share";

        /// <summary>Which of the two damage rings is drawn thicker - the window's own comparison of how
        /// much damage of each type there is, which neither ring's split says. Nothing is said for two
        /// rings drawn alike.</summary>
        public const string BattleEnergyBiggerThreat = "battle.energy-bigger-threat";
        public const string BattleProjectileBiggerThreat = "battle.projectile-bigger-threat";

        /// <summary>How one of the three range rings is split between the fleets - how well each side's
        /// ships shoot at that range, averaged over the ships they are bringing.</summary>
        public const string BattleShortRangeShare = "battle.short-range-share";
        public const string BattleMediumRangeShare = "battle.medium-range-share";
        public const string BattleLongRangeShare = "battle.long-range-share";

        /// <summary>Which side had the morale bonus, for the advanced report's heading. The game draws
        /// this as a happiness icon tinted with the holder's empire colour and titles it "Morale bonus";
        /// the colour is the whole of WHOSE it is, and a title read out is a caption a listener cannot
        /// answer "mine or theirs?" from (owner-reported 2026-08-30). So the mod states the fact and
        /// names the side, and the game's own definition of the bonus stays in the review buffer behind
        /// it. Past tense: the report is written after the fight.</summary>
        public const string BattleYourMoraleBonus = "battle.your-morale-bonus";
        public const string BattleEnemyMoraleBonus = "battle.enemy-morale-bonus";

        /// <summary>How much of a side's shooting missed, for the advanced report's damage gauges. The
        /// game writes the COUNT beside the band and draws the SHARE as the band's height, with no
        /// figure on it and no total written anywhere - so the phrase says the share and no total, which
        /// is exactly what the picture says.</summary>
        public const string BattleShotsMissed = "battle.shots-missed";

        /// <summary>Which of the three range rings is drawn thickest - the range BOTH fleets are best
        /// suited to, which is where the fight is likely to be decided.</summary>
        public const string BattleShortRangeMatters = "battle.short-range-matters";
        public const string BattleMediumRangeMatters = "battle.medium-range-matters";
        public const string BattleLongRangeMatters = "battle.long-range-matters";

        /// <summary>
        /// Whether a ship is pinned to the flotilla it is in, which the advanced setup draws as a
        /// white glow on a 24-pixel chip and writes down nowhere at all.
        ///
        /// A locked ship is exempt from the sorting presets: the three buttons that redistribute the
        /// fleet leave it where the player put it. So the two words are a STATE the player sets and
        /// re-reads, not a decoration - which is why they are a control's two state words rather than
        /// one word that appears and vanishes. "Not locked" says the flotilla nothing, because a ship
        /// that is not pinned has no flotilla it belongs to: the next preset may move it anywhere.
        /// </summary>
        public const string BattleShipLockedInFlotilla = "battle.ship-locked-in-flotilla";
        public const string BattleShipNotLocked = "battle.ship-not-locked";

        /// <summary>A ship carried from one flotilla to another and put down there, in the shape every
        /// landed drop in the mod reports itself with (<c>fleets.ship-moved</c>,
        /// <c>system.population-moved</c>) - what moved, and where it went.</summary>
        public const string BattleShipMoved = "battle.ship-moved";

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

        /// <summary>Which of the other empires a competitor slot is. The game captions every one of them
        /// with the same word ("AI"), so the panel's own drawing names none of them apart: the number is
        /// the slot's place in the panel, counted from the top, and it is the only handle a player has for
        /// saying which empire they are editing.</summary>
        public const string NewGamePlayer = "new-game.player";

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
        // screen name is only a fallback for the frames before the window has written one - and on the
        // skill page the heading is what names the wheel, which is why the wheel needs no word here. A
        // skill dot needs two: the game paints the level a skill stands at, and the level a pending pick
        // would take it to, as a ring of coloured arcs round the dot and writes no number anywhere.
        public const string ScreenHeroInspection = "screen.hero-inspection";
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

        // --- screens batch (2026-08-22) ---

        /// <summary>One mark on the population overview's collection track: how many of a people it
        /// takes, and whether the empire has that many. The game draws the number and shows the answer
        /// by fading the circle, and writes neither in words anywhere - so both phrases are the mod's,
        /// each a whole sentence rather than a number with a word stuck on the end.</summary>
        public const string PopulationThresholdReached = "population.threshold-reached";
        public const string PopulationThresholdNotReached = "population.threshold-not-reached";

        // --- end screens batch ---

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

        /// <summary>Whose relations the ring has just been re-centred on. Swap mode rewrites every card
        /// on the page at once and the only thing that says whose ring it now is, is the name over the
        /// hologram in the middle - so the change is announced. {0} is the empire, as the game writes
        /// it there.</summary>
        public const string DiplomacyViewingFrom = "diplomacy.viewing-from";

        public const string ScreenNegotiation = "screen.negotiation";

        /// <summary>The negotiation table's bands. The game captions most of them itself - the window
        /// heading, the pressure gauge, both term shelves, the contract and its two contributions - and
        /// those words are read off the widget with the keys below as the fallback. The bands the game
        /// leaves unnamed name themselves from here.</summary>

        /// <summary>The band between the two empire banners: what the diplomatic status is, and the
        /// three things that status permits. The game draws the status sentence and the ability icons
        /// and captions neither.</summary>
        public const string NegotiationRelationship = "negotiation.relationship";
        public const string NegotiationDossier = "negotiation.dossier";
        public const string NegotiationPressure = "negotiation.pressure";
        public const string NegotiationMyTerms = "negotiation.my-terms";
        public const string NegotiationTheirTerms = "negotiation.their-terms";
        public const string NegotiationTerms = "negotiation.terms";
        public const string NegotiationContract = "negotiation.contract";
        public const string NegotiationContractBoth = "negotiation.contract-both";
        public const string NegotiationContractMine = "negotiation.contract-mine";
        public const string NegotiationContractTheirs = "negotiation.contract-theirs";
        public const string NegotiationApproval = "negotiation.approval";

        /// <summary>Where the deal-approval bar STANDS, which is what that band draws and all it draws:
        /// the computer sends written reasons with its evaluation and the window throws them away, so a
        /// number is the honest reading.</summary>

        /// <summary>One empire's share of the influence-pressure bar. The gauge is one track split
        /// between the two of them and the game writes no figure anywhere on it, so each side is named
        /// by the banner it is drawn under and given the length it holds.</summary>
        public const string NegotiationPressureShare = "negotiation.pressure-share";

        /// <summary>Where a threshold notch sits along the pressure bar. The game draws the marker at a
        /// place on the track and says only whether it has been reached, so without the place the two
        /// markers are two sentences with nothing to tell them apart or measure the bar against
        /// (owner-reported 2026-08-27: "the thresholds for pressure make no sense currently").</summary>
        public const string NegotiationThresholdAt = "negotiation.threshold-at";
        public const string NegotiationActions = "negotiation.actions";

        /// <summary>The stepper a resource term draws beside its name. The game writes the number in the
        /// box and nothing else, so the box needs naming.</summary>
        public const string NegotiationQuantity = "negotiation.quantity";

        /// <summary>The fallback name of the minor-civilization window, for a build where the game's own
        /// title (<c>%MinorFactionDiplomacyModalWindowTitle</c>) reads back empty. Every band of that
        /// window is named by the game's own caption, so nothing else here belongs to it.</summary>
        public const string ScreenMinorDiplomacy = "screen.minor-diplomacy";

        public const string ScreenPirateDiplomacy = "screen.pirate-diplomacy";
        public const string PiratePower = "pirate.power";
        public const string PirateStanding = "pirate.standing";
        public const string PirateNextFleet = "pirate.next-fleet";

        public const string ScreenAcademyDiplomacy = "screen.academy-diplomacy";
        public const string ScreenAcademyModal = "screen.academy-modal";
        public const string AcademyRelation = "academy-diplomacy.relation";
        public const string AcademyNamedShips = "academy.named-ships";

        /// <summary>The band of actions, and the pair of stock figures along the bottom edge, that the
        /// minor-faction, pirate and Academy windows all draw. All THREE windows caption the actions
        /// band themselves ("Actions", from three separate keys - corrected 2026-08-22, this comment
        /// used to say only the pirate one did), so the word here is the fallback and the shared name
        /// for the OTHER bands that borrow it: the hangar's command bar and the senate's button
        /// strip.</summary>
        public const string DiplomacyActionsBand = "diplomacy.actions-band";
        public const string DiplomacyTreasury = "diplomacy.treasury";

        /// <summary>One band of a minor faction's relation gauge, named by the relation state it would
        /// buy and the number of relation points where that state starts ("CORDIAL (25)"). A
        /// connective: the state word is the game's (<c>%DiplomaticRelationStateMinor…Title</c>,
        /// read off the band's own description key) and the number is measured off the bar.</summary>
        public const string MinorBand = "minor.band";

        /// <summary>What the relation points and their per-turn trend are ("40 (+7/Turn)"). The game
        /// gives the line no title anywhere - only a sentence explaining what a relation IS - so the
        /// caption is the mod's (owner ruling 2026-08-22).</summary>
        public const string MinorRelationship = "minor.relationship";

        /// <summary>The pane one of the senate windows writes the selected thing out in; the game gives
        /// it no heading of its own. Its button band shares <see cref="DiplomacyActionsBand"/>.
        /// </summary>
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
            { StartupReady, "Endless Space 2 Access {0} ready" },
            { FragmentSeparator, " " },
            { ListSeparator, ", " },
            { ListPair, "{0} and {1}" },
            { ListFinal, "{0}, and {1}" },
            { Fraction, "{0} of {1}" },
            { FractionUnit, "{0} of {1} {2}" },
            { Quantity, "x {0}" },
            { ControlButton, "button" },
            { ControlGroup, "group" },
            { ControlTab, "tab" },
            { ControlCheckbox, "checkbox" },
            { ControlSlider, "slider" },
            { ControlComboBox, "combo box" },
            { ControlEditField, "editable" },
            { ControlNumericEditField, "numeric editable" },
            { EditStarted, "editing" },
            { EditCommitted, "edited" },
            { EditCancelled, "Cancelled" },
            { EditCaretSpace, "space" },
            { EditCaretBlank, "blank" },
            { ControlMenuItem, "menu item" },
            { ControlRadioButton, "radio button" },
            { NavExpanded, "expanded" },
            { NavCollapsed, "collapsed" },
            { NavChecked, "checked" },
            { NavUnchecked, "not checked" },
            { NavSelected, "selected" },
            { NavNotSelected, "not selected" },
            { NavDisabled, "unavailable" },
            { NavNoDetails, "Nothing in here" },
            { NavTable, "table" },
            { NodeTooltipsRegion, "Tooltips" },
            { CaptionedColon, "{0}: {1}" },
            { SearchNoMatch, "No match for {0}" },
            { SearchCleared, "Search cleared" },
            { NavKeyBindingSecondary, "secondary {0}" },
            { NavNotBound, "not bound" },
            { NavPressPrimaryKey, "Press the new key combination." },
            { NavPressSecondaryKey, "Press the new secondary key combination." },
            { NavKeyBindingAction, "Action" },
            { NavKeyBindingPrimaryColumn, "Primary key" },
            { NavKeyBindingSecondaryColumn, "Secondary key" },
            {
                NavKeyBindingShadowed,
                "While the mod's {0} is active, the game's {1} will not fire."
            },
            { None, "None" },
            { BufferUi, "UI" },
            { BufferEmpty, "Buffer empty" },
            { BufferLine, "{0}. {1}" },
            { BufferChat, "Chat" },
            { ChatSaid, "{0}: {1}" },
            { ChatWhispered, "{0} whispers: {1}" },
            { ChatToAlliance, "{0}, to the alliance: {1}" },
            { ChatPanel, "Chat" },
            { ChatUnread, "unread messages" },

            { ChatMessageBox, "Message" },
            { ScreenMainMenu, "Main menu" },
            { ScreenMessageBox, "Dialog" },
            { ScreenOptions, "Options" },
            { ScreenModSettings, "Mod settings" },
            { ModSettingsEntry, "Mod settings" },
            { ModSettingsScanner, "Scanner" },
            {
                ModSettingsScannerDescription,
                "The three scanner categories you write for yourself."
            },
            { ScannerEditSlotButton, "Custom category {0}: {1}" },
            { ScannerEditEmpty, "empty" },
            { ScannerEditName, "Name" },
            { ScannerEditKeyword, "Keyword {0}" },
            { ScannerEditSelected, "{0}, {1} selected" },
            { ScannerEditMissing, "{0}, not found this game" },
            { ScannerEditAddKeyword, "Add keyword" },
            { ScannerEditRemoved, "{0} removed" },
            { ScannerEditCleared, "Custom category {0} cleared" },
            { ScannerEditNameTaken, "{0} is already the name of a category" },
            { ScannerEditNameBlank, "A custom category needs a name" },
            {
                ScannerEditKeywordTaken,
                "That keyword is already in this custom category"
            },
            { ScannerEditClear, "Clear this custom category" },
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
            { TutorialBar, "Tutorial" },
            { TutorialMinimize, "Minimize" },
            { TutorialShowLocation, "Show location" },
            { TutorialClose, "Close tutorial" },
            { GalaxyTurn, "Turn {0}" },
            { GalaxyEndTurn, "End turn" },
            { GalaxyApplyMovements, "Apply movements" },
            { GalaxyNextIdleFleet, "Next idle fleet" },
            { GalaxyIdleFleets, "{0} idle fleets" },
            { GalaxyGameMenu, "Game menu" },
            { GalaxyPendingNotifications, "Show pending notifications" },
            { GalaxyRealTimeClock, "Clock" },
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
            { GalaxyManageSystem, "Manage system" },
            { GalaxyShownOnMap, "Shown on the map" },
            { GalaxyQuestShownOnMap, "{0}, objective shown on the map" },
            { GalaxyStarlane, "Starlane {0} to {1}, {2}" },
            { GalaxyStarlaneUnexplored, "Starlane {0} to an unexplored system, {1}" },
            { GalaxyWormhole, "Wormhole {0} to {1}, {2}" },
            { GalaxyWormholeUnexplored, "Wormhole {0} to an unexplored system, {1}" },
            { GalaxyFleetOnStarlane, "on starlane {0}, {1}" },
            { GalaxyFleetOnWormhole, "on wormhole {0}, {1}" },
            { GalaxyFleetFreeMovingTo, "free moving to {0}" },
            { GalaxyFleetFreeMovingToUnexplored, "free moving to an unexplored system" },
            { GalaxyFleetOnLaneToUnexplored, "on a star lane to an unexplored system" },
            { GalaxyFleetUnderWayNearbyOne, "{0} fleet under way nearby" },
            { GalaxyFleetsUnderWayNearby, "{0} fleets under way nearby" },
            { GalaxySystemPopulation, "{0} population" },
            { GalaxySystemSleepers, "{0} sleepers" },
            { GalaxySystemBuilding, "Building {0}, {1} turns" },
            { GalaxySystemNothingBuilding, "Nothing under construction" },
            { GalaxySystemRebellion, "Rebellion at {0} percent, {1} to go" },
            { GalaxySystemPiratePower, "Pirate power level {0}" },
            { GalaxySystemMinorRelation, "Relations {0} percent" },
            { GalaxySystemEmpireBars, "Colonies here: {0}" },
            { GalaxySystemDepositExploited, "{0}, exploited" },
            { GalaxySystemDepositIdle, "{0}, not exploited" },
            { GalaxySystemFriendlyShip, "{0} friendly ship" },
            { GalaxySystemFriendlyShips, "{0} friendly ships" },
            { GalaxySystemHostileShip, "{0} hostile ship" },
            { GalaxySystemHostileShips, "{0} hostile ships" },
            { GalaxySystemAcademyLevel, "Academy level {0}, {1} percent to the next level" },
            { GalaxyConstellationUnexplored, "Unexplored space" },
            { GalaxySystemUnexplored, "Unexplored system" },
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
            {
                GalaxySystemInfluenceGrowing,
                "Influence radius: {0}, growing to {1} next turn"
            },
            {
                GalaxySystemInfluenceShrinking,
                "Influence radius: {0}, shrinking to {1} next turn"
            },
            { GalaxySystemInfluenceSteady, "Influence radius: {0}, no change next turn" },
            { GalaxySystemInfluencedBy, "Under {0}'s influence" },
            { GalaxySystemInfluencedByYou, "Under your influence" },
            { GalaxySystemInfluenceContested, "Influence contested by {0}" },
            { GalaxySystemInfluenceContestedList, "Influence contested by {0} and {1}" },
            { GalaxySystemInfluenceContestedYou, "Influence contested by your empire" },
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
            { DirectionNorthNorthEast, "north-northeast" },
            { DirectionEastNorthEast, "east-northeast" },
            { DirectionEastSouthEast, "east-southeast" },
            { DirectionSouthSouthEast, "south-southeast" },
            { DirectionSouthSouthWest, "south-southwest" },
            { DirectionWestSouthWest, "west-southwest" },
            { DirectionWestNorthWest, "west-northwest" },
            { DirectionNorthNorthWest, "north-northwest" },
            { OffsetNorth, "{0} north" },
            { OffsetSouth, "{0} south" },
            { OffsetEast, "{0} east" },
            { OffsetWest, "{0} west" },
            { GalaxyCoordinatePair, "{0}, {1}" },
            {
                GalaxyMapSummary,
                "{0} galaxy, {1} size, {2} by {3} units; galactic center {4} of {5}."
            },
            { GalaxyInspectEntered, "Inspect mode" },
            { GalaxyInspectExited, "Exited inspect mode" },
            { GalaxyInspectCursorSize, "Cursor {0} by {1}" },
            { GalaxyInspectEdge, "Map edge" },
            { GalaxyInspectStarlane, "Star lane from {0} to {1}" },
            { GalaxyInspectStarlaneUnexplored, "Star lane from {0} going {1}" },
            { GalaxyInspectWormhole, "Wormhole from {0} to {1}" },
            { GalaxyInspectWormholeUnexplored, "Wormhole from {0} going {1}" },
            { GalaxyInspectFog, "Unexplored" },
            { GalaxyInspectFogOne, "1 square unexplored" },
            { GalaxyInspectFogMany, "{0} squares unexplored" },
            { GalaxyInspectSkippedOne, "Skipped 1 square" },
            { GalaxyInspectSkippedMany, "Skipped {0} squares" },
            { GalaxyInspectConstellation, "{0} constellation" },
            { GalaxyInspectConstellationLeft, "Out of {0} constellation" },
            { GalaxyInspectInfluenceIn, "In {0}'s influence" },
            { GalaxyInspectInfluenceInYou, "In your influence" },
            { GalaxyInspectInfluenceEdge, "Edge of {0}'s influence" },
            { GalaxyInspectInfluenceEdgeYou, "Edge of your influence" },
            { GalaxyInspectInfluenceEdgeList, "Edge of {0}'s and {1}'s influence" },
            { GalaxyInspectInfluenceOut, "Out of {0}'s influence" },
            { GalaxyInspectInfluenceOutYou, "Out of your influence" },
            { GalaxyInspectInfluenceOutList, "Out of {0}'s and {1}'s influence" },
            { GalaxyScannerScope, "{0}: {1}" },
            { GalaxyScannerSystems, "Systems" },
            { GalaxyScannerFleets, "Fleets" },
            { GalaxyScannerProbes, "Probes" },
            { GalaxyScannerSystemsAll, "all" },
            { GalaxyScannerSystemsFriendly, "friendly" },
            { GalaxyScannerSystemsNeutral, "neutral" },
            { GalaxyScannerSystemsEnemy, "enemy" },
            { GalaxyScannerFleetsAll, "all" },
            { GalaxyScannerFleetsFriendly, "friendly" },
            { GalaxyScannerFleetsNeutral, "neutral" },
            { GalaxyScannerFleetsEnemy, "enemy" },
            { GalaxyScannerProbesAll, "all" },
            { GalaxyScannerProbesFriendly, "friendly" },
            { GalaxyScannerProbesNeutral, "neutral" },
            { GalaxyScannerProbesEnemy, "enemy" },
            { GalaxyScannerSystemsHomeworld, "homeworld" },
            { GalaxyScannerSystemsMinorFactions, "minor factions" },
            { GalaxyScannerSystemsSpecial, "special" },
            { GalaxyScannerQuestMarkers, "Quest markers" },
            { GalaxyScannerQuestMarkersAll, "all" },
            { GalaxyScannerPins, "Ally pins" },
            { GalaxyScannerPinsAll, "all" },
            { GalaxyScannerProjectiles, "Obliterator missiles" },
            { GalaxyScannerProjectilesAll, "all" },
            { GalaxyScannerContestedInfluence, "Contested Influence" },
            { GalaxyScannerContestedInfluenceAll, "all" },
            { GalaxyScannerNear, "Near {0}" },
            { GalaxyScannerEmpty, "{0}, none found" },
            { GalaxyScannerHere, "here" },
            // --- scanner categories (2026-08-22) ---
            { GalaxyScannerColonizable, "Colonizable Planets" },
            { GalaxyScannerColonizableUnoccupied, "unoccupied" },
            { GalaxyScannerColonizableOccupied, "occupied" },
            { GalaxyScannerUnexplored, "Unexplored" },
            { GalaxyScannerUnexploredAll, "all" },
            { GalaxyScannerUnexploredLane, "Star lane {0} from {1} heading {2}" },
            { GalaxyScannerUnexploredWormhole, "Wormhole {0} from {1} heading {2}" },
            { GalaxyScannerAnomalies, "Anomalies" },
            { GalaxyScannerAnomaliesAll, "all" },
            { GalaxyScannerCuriosities, "Curiosities" },
            { GalaxyScannerCuriositiesAll, "all" },
            { GalaxyScannerCuriositiesExplorable, "Explorable" },
            { GalaxyScannerCuriositiesLowPower, "Insufficient Expedition Power" },
            { GalaxyScannerLuxury, "Luxury Resources" },
            { GalaxyScannerLuxuryAll, "all" },
            { GalaxyScannerStrategic, "Strategic Resources" },
            { GalaxyScannerStrategicAll, "all" },
            { GalaxyScannerOnPlanet, "{0} on {1}" },
            { GalaxyScannerCustomName, "Custom {0}" },
            { GalaxyScannerCustomAll, "all" },
            { GalaxyScannerNoCustom, "No custom category on {0}" },
            { GalaxyScannerMaxPopulation, "max population {0}" },
            { GalaxyScannerOutput, "{0} {1}" },
            // --- end scanner categories ---
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
            { GalaxyProbeLaunchReach, "Launch probe, reach {0}" },
            { GalaxyProbeContext, "{0}: {1}." },
            { GalaxyProbeContextUnexplored, "Unexplored {0}" },
            { GalaxyProbeContextRange, "{0}-{1}" },
            { GalaxyProbeContextToEdge, "{0} to the map edge at {1}" },
            { GalaxyProbeContextEdge, "map edge at {0}" },
            { GalaxyProbeContextExplored, "Fully explored to the map edge at {0}" },
            { GalaxyProbeContextPercentExplored, "{0} percent explored" },
            { GalaxyProbeContextAlongside, "Unexplored alongside to the {0}: {1}" },
            { GalaxyProbeContextAlongsideBoth, "Unexplored alongside to both sides: {0}" },
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
            { FleetOwnedEnemy, "enemy {0}" },
            { FleetOwnedNeutral, "neutral {0}" },
            { FleetOwnedFriendly, "friendly {0}" },
            { FleetHero, "hero {0}" },
            { FleetShipGroup, "{0} {1}" },
            { FleetRouteCancelledTo, "The route of {0} to {1} was cancelled" },
            { FleetRouteCancelled, "The route of {0} was cancelled" },
            { NotificationTurnPrefix, "Turn {0}: {1}" },
            { NotificationSystemRevealed, "New system discovered: {0} at {1}" },
            { NotificationSystemRevealedPlain, "New system discovered: {0}" },
            { NotificationSystemRevealedUnknown, "New system discovered" },
            { NotificationSystemRevealedBody, "{0} has been discovered." },
            {
                NotificationSystemRevealedInConstellation,
                "{0} has been discovered, in the {1} constellation."
            },
            { NotificationFleetSighted, "{0} fleet sighted at {1}" },
            { NotificationFleetSightedNowhere, "{0} fleet sighted" },
            { NotificationFleetSightedBody, "The {0} fleet {1} was sighted at {2}." },
            { NotificationFleetSightedBodyNowhere, "The {0} fleet {1} was sighted." },
            { NotificationColonySighted, "{0} colony sighted at {1}" },
            { NotificationColonySightedBody, "A {0} colony was sighted at {1}." },
            { NotificationFleetDispatched, "{0} dispatched from {1} to {2}" },
            { NotificationFleetDispatchedTo, "{0} dispatched to {1}" },
            { NotificationFleetDispatchedPlain, "{0} dispatched" },
            { NotificationSystemBesieged, "{0} under siege by {1}" },
            { NotificationSystemBesiegedPlain, "{0} is under siege" },
            { NotificationSystemBesiegedBody, "{1} is besieging {0}." },
            { NotificationSystemBlockaded, "{0} blockaded by {1}" },
            { NotificationSystemBlockadedPlain, "{0} is blockaded" },
            { NotificationSystemBlockadedBody, "{1} is blockading {0}." },
            { NotificationTradeBlockadeEnded, "Trade blockade ended at {0}" },
            { NotificationTradeBlockadeEndedPlain, "Trade blockade ended" },
            { NotificationObliteratorFired, "{0} fired an Obliterator from {1}" },
            { NotificationObliteratorFiredPlain, "{0} fired an Obliterator" },
            { NotificationObliteratorObserved, "{0} fired an Obliterator from {1}" },
            { NotificationObliteratorObservedPlain, "{0} fired an Obliterator" },
            { NotificationFleetArrived, "{0} arrived at {1}" },
            { NotificationFleetArrivedPlain, "{0} arrived" },
            { NotificationFleetLostSight, "Lost sight of {0} fleet {1}, last seen at {2}" },
            { NotificationFleetLostSightUnnamed, "Lost sight of {0} fleet, last seen at {1}" },
            { NotificationFleetLostSightNowhere, "Lost sight of {0} fleet {1}" },
            { NotificationFleetLostSightUnnamedNowhere, "Lost sight of {0} fleet" },
            { NotificationForeignFleetMoved, "{0} fleet moved from {1} to {2}" },
            { NotificationForeignFleetMovedTo, "{0} fleet moved to {1}" },
            { NotificationForeignFleetMovedAway, "{0} fleet moved away from {1}" },
            { NotificationInfluenceGroundLost, "{0}'s influence lost ground to {1}" },
            { HudQuestShowLocation, "Show location" },
            { HudQuestUnpin, "Unpin quest" },
            { HudQuestPinned, "Pinned quest: {0}" },
            { HudQuestUnpinned, "No quest is pinned" },
            { GalaxyMapPanel, "Galactic Map" },
            { HudQuestPanel, "Quest" },
            { HudNotificationsPanel, "Notifications" },
            { HudTurnLogPanel, "Turn log" },
            { HudTurnLogTurn, "Turn {0}" },
            { HudDismissAllNotifications, "Dismiss all notifications" },
            { HudDismissAllTurnLog, "Dismiss all Turn log entries" },
            { HudViewControlsPanel, "View Controls" },
            { HudControlsPanel, "Controls" },
            { HudKeyResourcesPanel, "Key Resources" },
            { HudStrategicResourcesPanel, "Strategic Resources" },
            { HudSingularitiesPanel, "Singularities" },
            { HudPirateMarkPanel, "Pirate Mark" },
            { GameMenuGameSettings, "Game settings" },
            { GameMenuReadOnlySettings, "read only" },
            { LoadSaveSaveName, "Save name" },
            { LoadSaveCloud, "Cloud saves" },
            { NavCellEmpty, "empty" },
            { SaveStarted, "Saving the game" },
            { SaveFinished, "Save finished" },
            { DragStarted, "Dragging {0}. {1} to drop, {2} to cancel." },
            { DragStartedPlain, "Dragging {0}" },
            { DragDropped, "Dropped {0}" },
            { DragDropRefused, "{0} cannot go there" },
            { DragCancelled, "Cancelled drag" },
            { DragMovedToPosition, "Moved {0} to position {1}" },
            { DragDropTarget, "drop target" },
            { DragDraggable, "draggable" },
            { DragHint, "{0} to drag {1}." },
            { DragDropHint, "{0} to drop {1}." },
            { QueueQueued, "Queued {0}" },
            { QueueQueuedFirst, "Queued {0} as first item" },
            { QueueCancelled, "Cancelled {0}" },
            { ScreenScanView, "Scan view" },
            { ScanSystemInfo, "System information" },
            { ScanLegend, "Legend" },
            { ScanOutput, "{0} {1}" },
            { ScanHeroEfficiency, "{0}%" },
            { Zoom, "Zoom" },
            { ZoomLevel, "Zoom level {0} of {1}" },
            { ZoomBandGalaxyMap, "Galaxy map" },
            { ZoomBandInformativeGalaxy, "Informative galaxy" },
            { ZoomBandConstellation, "Constellation" },
            { ZoomBandSystems, "Systems" },
            { ZoomBandSystem, "System" },
            { ZoomBandSystemOverview, "System Overview" },
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
            { ScreenStarSystemNamed, "{0}, {1}" },
            { ScreenPlanet, "Planet" },
            { PlanetPrevious, "Previous planet" },
            { PlanetNext, "Next planet" },
            { SystemHangarEmpty, "No ships in the hangar" },
            { SystemPlanetsPanel, "Planets" },
            { SystemSidePanels, "System information" },
            { SystemColonyPanel, "System" },
            { SystemPopulationPanel, "Population" },
            { SystemSpaceportPanel, "Spaceport" },
            { SystemRepresentativesPanel, "Representatives" },
            { SystemOutpostPanel, "Outpost" },
            { SystemGovernorPanel, "Governor" },
            { SystemSanctuaryPopulationPanel, "Sanctuary population" },
            { SystemSanctuaryPanel, "Sanctuary" },
            { SystemSanctuaryLinksPanel, "Sanctuary links" },
            { SystemConstructiblesPanel, "Constructibles" },
            { SystemQueuePanel, "Construction queue" },
            { SystemHangarPanel, "Hangar" },
            { SystemColonize, "Colonize" },
            { SystemRenamePlanet, "Rename planet" },
            { SystemRenameSystem, "Rename system" },
            { SystemPopulationMoved, "Moved {0} to {1}" },
            { SystemPopulationSlot, "Slot {0} of {1}" },
            { SystemPopulationSlotEmpty, "Empty slot {0} of {1}" },
            { SystemPopulationSlotVacant, "Empty slot" },
            { SystemImprovements, "System improvements" },
            { SystemLevel, "System level {0}" },
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
            { FleetsActionSeatsColonize, "moves focus to the first planet that can be colonized" },
            { FleetsActionSeatsDestroy, "moves focus to the first planet that can be destroyed" },
            { FleetsActionSeatsExpedition, "moves focus to the first curiosity in the system" },
            { FleetsActionSeatsProbeSite, "moves focus to the first probe site in the system" },
            { FleetsActionSeatsWreck, "moves focus to the wreck" },
            { FleetsActionSeatsTerraform, "moves focus to the first planet that can be terraformed" },
            { FleetsActionSeatsRestore, "moves focus to the first planet that can be restored" },
            {
                FleetsActionSeatsReduceAnomaly,
                "moves focus to the first planet whose anomaly can be reduced"
            },
            { ScreenSystemDiscovery, "System discovery" },
            { DiscoverySystem, "Discovering {0}" },
            { ScreenRename, "Rename" },
            { ScreenResearch, "Research" },
            { ResearchStatusPanel, "Research status" },
            { ResearchKeyPanel, "Research key" },
            { ResearchTreePanel, "Technology tree" },
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
            { EmpirePlanetsPanelOpened, "Planets panel open for {0}" },
            { EmpirePlanetsPanelClosed, "Planets panel closed" },
            { EmpireConstructionPanelOpened, "Construction panel open for {0}" },
            { EmpireConstructionPanelClosed, "Construction panel closed" },
            { EmpireHangarPanelOpened, "Hangar panel open for {0}" },
            { EmpireHangarPanelClosed, "Hangar panel closed" },
            { ScreenSenate, "Senate" },
            { SenateGovernmentPanel, "Government" },
            { SenateElectionPanel, "Next election" },
            { SenateSupportPanel, "Support history" },
            { SenateEmptyLawSlot, "Empty law slot" },
            { SenateLockedLawSlot, "Locked law slot" },
            { SenateEmptySenatorSlot, "Empty senator slot" },
            { SenateLockedSenatorSlot, "Locked senator slot" },
            { PoliticsCurrentExperience, "Current Political experience: {0} ({1})" },
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
            { ShipDesignFilters, "Filters" },
            { ShipDesignAvailable, "Available" },
            { ShipDesignModuleFitted, "Fitted {0}" },
            { ShipDesignModuleRemoved, "Removed {0}" },
            { ShipDesignSlotEmpty, "empty" },
            { ShipDesignSlotMultiplier, "Times {0} Multiplier" },
            { ShipDesignRemoveTarget, "Drop here to remove" },
            { ScreenHeroInspection, "Hero inspection" },
            { HeroSkillLevel, "level {0} of {1}" },
            { HeroSkillPending, "level {0} pending" },
            { HeroSkillRing, "Ring {0} of {1}" },
            { HeroSkillRingPoint, "opens once 1 skill point has been spent" },
            { HeroSkillRingPoints, "opens once {0} skill points have been spent" },
            { ScreenFleetSelection, "Fleet selection" },
            { TacticsSlotFilled, "Put {0} in your Tactics Set" },
            { TacticsSlotEmptied, "{0} removed" },
            { TacticsRemoveTarget, "Drop tactic here to remove" },
            { TacticsAvailablePanel, "Available" },
            { TacticsDeckPanel, "Tactics set" },
            { ScreenBattle, "Space battle" },
            { ScreenGroundBattle, "Ground battle" },
            { ScreenAdvancedBattleReport, "Advanced battle report" },
            { BattleTactics, "Tactics" },
            { BattleYourFleets, "Your fleets" },
            { BattleEnemyFleets, "Enemy fleets" },
            { BattleStats, "Stats" },
            { BattleYourTroops, "Your troops" },
            { BattleEnemyTroops, "Enemy troops" },
            { BattleYourDamage, "Damage you caused" },
            { BattleEnemyDamage, "Damage they caused" },
            { BattleShowYourFleets, "Show your fleets" },
            { BattleShowEnemyFleets, "Show enemy fleets" },
            { BattleBalance, "Balance of power: {0} has {1}% more military power than {2}" },
            { BattleBalanceAll, "Balance of power: {0} has all the military power, {1} has none" },
            { BattleGroundBalance, "Manpower {0} against {1}" },
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
            { BattleFireEnergy, "{0} hit {1}: {2} energy damage" },
            { BattleFireEnergyMany, "{0} hit {1} {2} times: {3} energy damage" },
            { BattleFireProjectile, "{0} hit {1}: {2} projectile damage" },
            { BattleFireProjectileMany, "{0} hit {1} {2} times: {3} projectile damage" },
            { BattleFireMixed, "{0} hit {1}: {2} energy damage and {3} projectile damage" },
            {
                BattleFireMixedMany,
                "{0} hit {1} {2} times: {3} energy damage and {4} projectile damage"
            },
            { BattleFirePlain, "{0} hit {1}: {2} damage" },
            { BattleFirePlainMany, "{0} hit {1} {2} times: {3} damage" },
            { BattleFireAbsorbed, "{0} hit {1}: fully absorbed by shields" },
            { BattleFireAbsorbedMany, "{0} hit {1} {2} times: fully absorbed by shields" },
            { BattleFireMissed, "{0} missed {1}" },
            { BattleFireMissedMany, "{0} missed {1} {2} times" },
            { BattleFireMissedClause, "missed" },
            { BattleFireMissedClauseMany, "missed {0} times" },
            { BattleFireShieldClause, "{0} absorbed by shields" },
            { BattleReinforcements, "Reinforcements arrive: {0}" },
            { BattleEnemyReinforcements, "Enemy reinforcements arrive: {0}" },
            { BattleRepaired, "{0} repaired {1}" },
            { BattleEffectApplied, "{0} applies {1} to {2}" },
            { BattleMedalEarned, "{0} earned the {1} medal" },
            { GroundBattleRound, "Round {0}" },
            { GroundBattleBombardment, "Orbital bombardment" },
            { GroundBattleAssault, "Ground assault" },
            { GroundBattleYourTroopLost, "One of your {0} is lost" },
            { GroundBattleYourTroopsLost, "{0} of your troops lost" },
            { GroundBattleEnemyTroopLost, "One enemy {0} is lost" },
            { GroundBattleEnemyTroopsLost, "{0} enemy troops lost" },
            { BattleStatsTrajectories, "Trajectories" },
            { BattleStatsMilitary, "Military power" },
            { BattleStatsDamage, "Damage" },
            { BattleStatsRange, "Range" },
            { BattleFlotillaRange, "Flotilla {0} engages at {1}" },
            {
                BattleEnergyDamageShare,
                "Of the energy damage that gets through defenses: {0}% is yours, {1}% is the enemy's"
            },
            {
                BattleProjectileDamageShare,
                "Of the projectile damage that gets through defenses: {0}% is yours, {1}% is the "
                    + "enemy's"
            },
            { BattleEnergyBiggerThreat, "Energy is the bigger threat" },
            { BattleProjectileBiggerThreat, "Projectile is the bigger threat" },
            { BattleShortRangeShare, "Effectiveness at short range: {0}% yours, {1}% enemy's" },
            { BattleMediumRangeShare, "Effectiveness at medium range: {0}% yours, {1}% enemy's" },
            { BattleLongRangeShare, "Effectiveness at long range: {0}% yours, {1}% enemy's" },
            { BattleYourMoraleBonus, "Your fleet had the morale bonus" },
            { BattleEnemyMoraleBonus, "Their fleet had the morale bonus" },
            { BattleShotsMissed, "{0}% of shots missed" },
            { BattleShortRangeMatters, "Short range matters most" },
            { BattleMediumRangeMatters, "Medium range matters most" },
            { BattleLongRangeMatters, "Long range matters most" },
            { BattleShipLockedInFlotilla, "locked in flotilla" },
            { BattleShipNotLocked, "not locked" },
            { BattleShipMoved, "Moved {0} to {1}" },
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
            { NewGamePlayer, "Player {0}" },
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

            // --- screens batch (2026-08-22) ---
            { PopulationThresholdReached, "{0} population, reached" },
            { PopulationThresholdNotReached, "{0} population, not reached" },

            // --- end screens batch ---
            { ScreenDiplomacy, "Diplomatic status" },
            { DiplomacyEmpires, "Empires" },
            { DiplomacyControls, "Screen controls" },
            { DiplomacyNewOptions, "new diplomatic options" },
            { DiplomacyNotYourRing, "You can only negotiate from your own empire's ring" },
            { DiplomacyViewingFrom, "Viewing relations from the perspective of {0}" },
            { ScreenNegotiation, "Negotiation" },
            { NegotiationRelationship, "Relationship" },
            { NegotiationDossier, "Empire information" },
            { NegotiationPressure, "Pressure" },
            { NegotiationMyTerms, "Terms you can offer" },
            { NegotiationTheirTerms, "Terms you can ask for" },
            { NegotiationTerms, "Terms" },
            { NegotiationContract, "The deal" },
            { NegotiationContractBoth, "Both empires" },
            { NegotiationContractMine, "You give" },
            { NegotiationContractTheirs, "They give" },
            { NegotiationApproval, "Deal approval" },
            { NegotiationPressureShare, "{0} {1} percent" },
            { NegotiationThresholdAt, "{0} percent" },
            { NegotiationActions, "Offer" },
            { NegotiationQuantity, "Quantity" },
            { ScreenMinorDiplomacy, "Minor faction diplomacy" },
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
            { MinorBand, "{0} ({1})" },
            { MinorRelationship, "Relationship" },
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
        /// (<see cref="IconDefaults"/>) and the key names and usage hints
        /// (<see cref="HintDefaults"/>) and the names of the mod's own keyboard actions
        /// (<see cref="ActionDefaults"/>) are as much a shipped string as any other; they are held
        /// in tables of their own only because each is a family read together.
        /// </summary>
        public static bool TryGetDefault(string key, out string template)
        {
            return Defaults.TryGetValue(key, out template)
                || IconDefaults.TryGetValue(key, out template)
                || HintDefaults.TryGetValue(key, out template)
                || ActionDefaults.TryGetValue(key, out template);
        }

        /// <summary>Whether the mod ships a phrase for <paramref name="key"/> at all - asked where a
        /// key is COMPOSED and may legitimately not exist (a keyboard key the hint table does not
        /// name), so that <see cref="Get"/>'s warn-once is not spent on a miss that is expected.
        /// </summary>
        public static bool Has(string key)
        {
            string ignored;
            return (_overrides != null && _overrides.TryGetValue(key, out ignored))
                || TryGetDefault(key, out ignored);
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
