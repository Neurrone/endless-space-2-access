namespace ES2Access.Core.Speech
{
    /// <summary>
    /// STAR SYSTEMS, PLANETS AND FLEETS as the management pages read them: the system
    /// page's panels and cells, a planet's own lines, and the fleets table.
    ///
    /// Part of <see cref="ModStrings"/>; the English defaults for every key here live in the
    /// <c>Defaults</c> table with the rest.
    /// </summary>
    public static partial class ModStrings
    {
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

        /// <summary>The one stop the left-edge information panels share. It has to be a word of its own
        /// rather than any panel's: naming it after the colony panel would announce "System, System,
        /// System level 2" on every Tab into it, because that panel is also the first REGION inside it.
        /// The game titles the group nothing at all - it draws four unlabelled boxes down an edge - so
        /// the phrase is the mod's.</summary>
        public const string SystemSidePanels = "system.side-panels";

        /// <summary>The box the star-system page draws its own heading over - the SYSTEM's name, which
        /// is what the box is about. The game keeps no title key for it at all
        /// (<c>%StarSystemSideInfoDescription</c> is the sentence on its header icon), so the word is
        /// the mod's, and it is "System" rather than "Colony" because the box is drawn for an outpost
        /// and a ghost system too (owner ruling 2026-08-22).</summary>
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

        /// <summary>A rate with no holding beside it: the colony panel's resource strip draws each
        /// resource's per-turn figure and hides the stock label entirely, so the row says what the
        /// next turn does and nothing about what is held.</summary>
        public const string SystemNetPerTurn = "system.net-per-turn";

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
    }
}
