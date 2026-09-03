namespace ES2Access.Core.Speech
{
    /// <summary>
    /// THE EMPIRE PAGES: research, quests, the senate and its politics, elections, the
    /// empire table, the economy, the military and its tactics, the academy and ship design.
    ///
    /// Part of <see cref="ModStrings"/>; the English defaults for every key here live in the
    /// <c>Defaults</c> table with the rest.
    /// </summary>
    public static partial class ModStrings
    {
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

        // The census ring's boost icon: the luxury that would boost a people, drawn full when the
        // empire can pay and dimmed when it cannot (PopulationCensusArc.RefreshBoostSpecifics). A
        // running boost draws its turn count instead and needs no words of the mod's (owner
        // ruling 2026-09-03).
        public const string SenateCensusBoostSufficient = "senate.census-boost-sufficient";
        public const string SenateCensusBoostInsufficient = "senate.census-boost-insufficient";

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
        /// and they are ONE thing to the player, so they share one phrase. The status and population
        /// cells slide out the same cards panel in two drawn modes - with the planet action buttons,
        /// or with the population rings - and those are two different answers to a press, so each mode
        /// has a phrase of its own.</summary>
        public const string EmpirePlanetActionsPanelOpened = "empire.planet-actions-panel-opened";
        public const string EmpirePlanetActionsPanelClosed = "empire.planet-actions-panel-closed";
        public const string EmpirePopulationPanelOpened = "empire.population-panel-opened";
        public const string EmpirePopulationPanelClosed = "empire.population-panel-closed";
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
        public const string EconomyTaxesPanel = "economy.taxes-panel";
        public const string EconomyLogPanel = "economy.log-panel";
        public const string EconomyAdsPanel = "economy.ads-panel";
        public const string EconomyEventsPanel = "economy.events-panel";
        public const string EconomyInflationPanel = "economy.inflation-panel";
        public const string EconomyTradePanel = "economy.trade-panel";

        // The marketplace's own captions, for the six places the game draws a control or a block and
        // writes no word over it: the price graph (drawn as a picture with only its axes labelled),
        // the two quantity steppers (bare arrows), and the three facts the tax box states in a row of
        // unlabelled values.
        public const string EconomyPriceHistory = "economy.price-history";

        /// <summary>What one column of the price-history table is called: the turn its prices were
        /// taken on. A key of its own rather than the turn log's <see cref="HudTurnLogTurn"/>, which
        /// happens to read the same in English: the two are a column caption and a heading over a
        /// day's events, and a language that inflects one of them differently must be free to say
        /// so.</summary>
        public const string EconomyPriceHistoryTurn = "economy.price-history-turn";

        public const string EconomyIncrement = "economy.increment";
        public const string EconomyDecrement = "economy.decrement";
        public const string EconomyLocation = "economy.location";
        public const string EconomyOwner = "economy.owner";
        public const string EconomyTaxRate = "economy.tax-rate";

        // The three bands each trading panel draws: the filter radios across the top, the list of what
        // is on offer, and the strip the trade is set up in. The game captions none of them.
        public const string EconomyFilters = "economy.filters";
        public const string EconomyAvailable = "economy.available";
        public const string EconomySellingWhat = "economy.selling-what";
        public const string EconomyBuyingWhat = "economy.buying-what";
        public const string EconomySellTransaction = "economy.sell-transaction";
        public const string EconomyBuyTransaction = "economy.buy-transaction";

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
    }
}
