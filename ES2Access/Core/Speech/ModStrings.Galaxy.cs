namespace ES2Access.Core.Speech
{
    /// <summary>
    /// The GALAXY MAP's own words: what the map draws around a star, which way a
    /// place lies and how far, what a fleet is doing out there, and the phrases the scanner and the
    /// probe context speak.
    ///
    /// Part of <see cref="ModStrings"/>; the English defaults for every key here live in the
    /// <c>Defaults</c> table with the rest.
    /// </summary>
    public static partial class ModStrings
    {
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

        // Who is holding an OUTPOST at a system somebody else owns, or at one the map says has no
        // owner at all - the empires named as a spoken list, so one template says it for one empire
        // and the other for several. Not the player's own outpost, which the row has its own word
        // for (<see cref="GalaxySystemOutpost"/>).
        public const string GalaxyOutpostHeld = "galaxy.outpost-held";
        public const string GalaxyOutpostsHeld = "galaxy.outposts-held";
        public const string GalaxyManageSystem = "galaxy.manage-system";

        // The game has taken the camera to a point the map draws nothing at - a quest marker out
        // between the stars, a battle at a place the player cannot see. The camera HAS moved, so
        // silence would leave the player reading somewhere the game is no longer showing.
        public const string GalaxyShownOnMap = "galaxy.shown-on-map";

        // The same reveal, made by the quest banner's pin, which knows which quest it is about. Said
        // whether or not the marker turned out to be somewhere the tree can name.
        public const string GalaxyQuestShownOnMap = "galaxy.quest-shown-on-map";

        // The lanes out of a system. The game draws these as lines and writes nothing on them, and
        // where a line runs off into space it has no destination to name either. A lane says the way
        // it leaves and where it comes out - "northeast to Leo" - and no number: the rows sit under
        // the system's own "Star lanes" heading, which is where the words "star lane" are already
        // said, and one direction plus one far end tells the lanes of a system apart on its own. The
        // clockwise-from-north numbering is still what orders them; it is just not spoken any more.
        public const string GalaxyStarlane = "galaxy.starlane";
        public const string GalaxyStarlaneUnexplored = "galaxy.starlane-unexplored";
        public const string GalaxyWormhole = "galaxy.wormhole";
        public const string GalaxyWormholeUnexplored = "galaxy.wormhole-unexplored";

        // Where a fleet under way is arriving, where it came FROM, and WHEN it gets there - one
        // sentence said under the system it is flying towards, which is the one system this tree
        // hangs it under, so "why is this fleet here, and when does it matter" is the question the
        // phrase answers. The hosting system is named in it rather than left to the row's place in
        // the tree, because the row is heard on its own from the scanner and the search as well.
        // The lane it is flying is named by its far end, the end it set out from, because that is
        // what tells one of a system's incoming lanes from another; the kind of line is said too,
        // since a wormhole is a different picture from a lane.
        //
        // Where the map has not named that end, the phrase still says which WAY the lane runs - the
        // compass word for the bearing out of the hosting system towards the far end, which is the
        // very word that system's own lane row says for the same line. Without it an unexplored
        // origin is the one case that tells two incoming lanes apart with nothing at all, and the
        // picture does tell them apart: the lines leave in different directions (owner ruling
        // 2026-09-02).
        //
        // Three of each, because the turn count is not always there to say: the game only draws a
        // foreign fleet's path where the empire has earned the right to see it, and where the picture
        // is refused the words are too (owner ruling 2026-09-02, `FleetRoute.RouteShown`). The
        // countless phrase is what is left. Whole phrases throughout rather than a turn clause glued
        // on the end, because a language that puts "in two turns" first has to be able to.
        public const string GalaxyFleetArrivingLane = "galaxy.fleet-arriving-lane";
        public const string GalaxyFleetArrivingLaneThisTurn =
            "galaxy.fleet-arriving-lane-this-turn";
        public const string GalaxyFleetArrivingLaneTurns = "galaxy.fleet-arriving-lane-turns";
        public const string GalaxyFleetArrivingWormhole = "galaxy.fleet-arriving-wormhole";
        public const string GalaxyFleetArrivingWormholeThisTurn =
            "galaxy.fleet-arriving-wormhole-this-turn";
        public const string GalaxyFleetArrivingWormholeTurns =
            "galaxy.fleet-arriving-wormhole-turns";
        public const string GalaxyFleetArrivingLaneDark = "galaxy.fleet-arriving-lane-dark";
        public const string GalaxyFleetArrivingLaneDarkThisTurn =
            "galaxy.fleet-arriving-lane-dark-this-turn";
        public const string GalaxyFleetArrivingLaneDarkTurns =
            "galaxy.fleet-arriving-lane-dark-turns";
        public const string GalaxyFleetArrivingWormholeDark = "galaxy.fleet-arriving-wormhole-dark";
        public const string GalaxyFleetArrivingWormholeDarkThisTurn =
            "galaxy.fleet-arriving-wormhole-dark-this-turn";
        public const string GalaxyFleetArrivingWormholeDarkTurns =
            "galaxy.fleet-arriving-wormhole-dark-turns";

        // The same answer for a fleet crossing OPEN SPACE between two systems with no lane between
        // them: there is no line to name, so the leg is said as the direction the fleet is coming in
        // from - a fleet standing west of the system is arriving from the west. It is the same
        // compass word the picture gives everyone else, read off the fleet's own place against the
        // system it is flying to, and it moves with the fleet as the turns pass.
        public const string GalaxyFleetArrivingOpen = "galaxy.fleet-arriving-open";
        public const string GalaxyFleetArrivingOpenThisTurn =
            "galaxy.fleet-arriving-open-this-turn";
        public const string GalaxyFleetArrivingOpenTurns = "galaxy.fleet-arriving-open-turns";

        // What the fleet does AFTER this system, said only where this system is not the end of its
        // journey: the row has already said when it gets HERE, and where the journey carries on the
        // player is owed the rest of it. Nothing at all where the destination is the hosting system,
        // which would be the same fact twice. Gated with the count above and for the same reason - a
        // route the game will not draw is a route the mod does not read out.
        public const string GalaxyFleetEnRouteThisTurn = "galaxy.fleet-en-route-this-turn";
        public const string GalaxyFleetEnRouteTurns = "galaxy.fleet-en-route-turns";
        public const string GalaxyFleetEnRouteUnexploredThisTurn =
            "galaxy.fleet-en-route-unexplored-this-turn";
        public const string GalaxyFleetEnRouteUnexploredTurns =
            "galaxy.fleet-en-route-unexplored-turns";

        // A fleet crossing open space towards somewhere the map has NOT named has no system in this
        // tree to hang under, so it gets a row of its own at the top and says the one thing that can
        // be said: the player cannot see where it is going.
        public const string GalaxyFleetFreeMovingToUnexplored =
            "galaxy.fleet-free-moving-to-unexplored";

        /// <summary>The same for a fleet flying a STAR LANE whose far end the map has not named - the
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

        // What the label's bottom row calls the lair the pirates keep at this system - a picture the
        // game names nowhere. It is the door into their diplomacy (the icon is wired to the handler
        // that opens whichever window the empire living here calls for), so the name is the PLACE and
        // the sentence it carries says what is happening there.
        public const string GalaxySystemPirateLair = "galaxy.system-pirate-lair";

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

        // The named blocks an opened system on the map is read in, in the order they are walked. The
        // map draws them all as one strip of icons round a name, so every one of these words is the
        // mod's - what the player hears once on the way into the block, and what Alt+Up/Down jumps
        // by. They name the QUESTION each block answers, never the widgets it was assembled from.
        public const string GalaxySystemStatusRegion = "galaxy.system-status-region";
        public const string GalaxySystemActionsRegion = "galaxy.system-actions-region";
        public const string GalaxySystemPlanetsRegion = "galaxy.system-planets-region";
        public const string GalaxySystemLanesRegion = "galaxy.system-lanes-region";
        public const string GalaxySystemFleetsRegion = "galaxy.system-fleets-region";
        public const string GalaxySystemResourcesRegion = "galaxy.system-resources-region";
        public const string GalaxySystemDetailsRegion = "galaxy.system-details-region";

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

        /// <summary>The same four components said SHORT - "23s", "1w, 23s" - for a player who is
        /// stepping through scanner results and hears the pair on every one of them. A whole
        /// template per axis again, so a language whose abbreviation goes in front of the number, or
        /// wants a space, can say so.</summary>
        public const string OffsetNorthShort = "offset.north-short";
        public const string OffsetSouthShort = "offset.south-short";
        public const string OffsetEastShort = "offset.east-short";
        public const string OffsetWestShort = "offset.west-short";

        /// <summary>Where a place on the map is, as a pair of whole numbers measured from the empire's
        /// home system (<see cref="MapCoordinates"/>). The game draws the galaxy and writes no
        /// coordinate anywhere, so the pair is the mod's - and it is ONE template rather than a number
        /// said twice, because which axis comes first, and what separates them, is a language's
        /// business.</summary>
        public const string GalaxyCoordinatePair = "galaxy.coordinates";

        /// <summary>The galaxy in one sentence: the shape and size it was generated with, how far
        /// across it is, and where its middle lies from the empire's home system - the picture a
        /// sighted player takes in at a glance when the map first opens and can never be told
        /// otherwise (<see cref="ES2Access.ES2.Speech.GalaxyMapText"/>). One template rather than a
        /// chain of them because every join in it is punctuation a language chooses for itself, and
        /// the shape and size words are the GAME's own, dropped in whole.</summary>
        public const string GalaxyMapSummary = "galaxy.map-summary";

        // The map's inspect cursor - a mode of the mod's own, so every word in it is the mod's. The
        // cursor's size is said as a whole phrase rather than a number glued to a symbol, because "3
        // by 3" is a shape and "3x3" is a sum. The lane phrases name the lane's ends the same way
        // round every time (westmost first) and say which way it runs between them, so one lane heard
        // from two neighbouring cells is heard as one lane - and heard with the same compass word the
        // lane's own row under a system says, off the same eight-word set.
        // MAP BOOKMARKS - ten places the player names by a digit. Every one of these is the mod's own
        // word: the game has no bookmarks and no text for one. The digit is a placeholder rather than
        // part of the sentence, so a language that puts the number elsewhere can; and the "set" line
        // names the place in the same breath, because a confirmation that does not say WHAT was
        // remembered is a confirmation the player has to go and check.
        public const string GalaxyBookmarkSet = "galaxy.bookmark.set";

        // The same confirmation, for the set that took a place another slot already held: one place
        // belongs to one slot, so the old one is emptied, and the player is told rather than left to
        // find a slot missing later. One whole sentence and not the plain line with a clause bolted
        // on, so a language that puts the news first can.
        public const string GalaxyBookmarkSetReplacing = "galaxy.bookmark.set-replacing";
        public const string GalaxyBookmarkEmpty = "galaxy.bookmark.empty";
        public const string GalaxyBookmarkSuffix = "galaxy.bookmark.suffix";
        public const string GalaxyBookmarkPoint = "galaxy.bookmark.point";

        // The heading the scan tree gathers the player's own POINT bookmarks under (owner-approved
        // 2026-09-01). A heading and not a sentence: it is the one branch of that tree the lens is
        // not drawing, so it is named rather than left as loose rows among the empires and their
        // holdings. A bookmarked SYSTEM has no row here - its annotation rides the system's own row.
        public const string GalaxyBookmarksGroup = "galaxy.bookmark.group";

        // The one refusal the set key has: a cursor big enough to hold two systems cannot say which
        // of them the player meant. It names the way OUT rather than the problem - the size keys are
        // right there - and carries no digit, because which slot was asked for is not what went
        // wrong.
        public const string GalaxyBookmarkShrink = "galaxy.bookmark.shrink";

        // The home key's own refusal: an empire that has not got a home system yet. The Vaulters begin
        // that way - their whole faction is a ship, and there is no capital until they set one down -
        // so the key is pressed and there is nowhere to go. Said rather than swallowed, because silence
        // from a key the player just pressed is indistinguishable from a key that does not work.
        public const string GalaxyBookmarkNoHome = "galaxy.bookmark.no-home";

        // The one line of the bookmarks file a PERSON reads: which game's bookmarks these are, written
        // at the top of it and refreshed on every write. It is a caption and not speech - nothing says
        // it out loud - but it is the player's own language for all that, and it is a whole sentence
        // here so a translator can order the three parts however their language orders them. The
        // comment mark in front of it is the file format's and never theirs.
        public const string GalaxyBookmarkFileHeader = "galaxy.bookmark.file-header";

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

        /// <summary>One of those ways out, named from the end the player CAN see and the way it leaves
        /// that end - the place it goes has no name yet, which is the whole of what makes it
        /// unexplored. The kind of line comes first because this list mixes lanes with wormholes, and
        /// the compass word is the one the lane's own row under that system says.</summary>
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
        /// they go in. Not folded into <see cref="ScanOutput"/> despite the identical English: this
        /// one is given (name, amount) and that one (amount, name), so one template could not be
        /// reordered for both.</summary>
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
        /// and how far the map itself goes that way
        /// (<see cref="ES2Access.ES2.Speech.ProbeContextText"/>). The game draws the fog and the
        /// map's rim and says neither in words, so all of it is the mod's.
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
        /// ({0}, the whole number of percent - <see cref="ES2Access.ES2.Map.ProbeFootprint"/>). The
        /// one figure a listener comparing sixteen headings chooses on, so it is said twice: once in
        /// the bearing's own announcement (<see cref="GalaxyProbeContext"/>) and once as the first
        /// line of its buffer, where the ranges that explain it follow.</summary>
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
    }
}
