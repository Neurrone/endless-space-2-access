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

        // What navigation says about a control beyond its own text. Each is a whole phrase: a
        // language that negates with more than a leading word has somewhere to put it.
        public const string NavExpanded = "nav.expanded";
        public const string NavCollapsed = "nav.collapsed";
        public const string NavChecked = "nav.checked";
        public const string NavUnchecked = "nav.unchecked";
        public const string NavSelected = "nav.selected";
        public const string NavDisabled = "nav.disabled";
        public const string NavHasTooltip = "nav.has-tooltip";
        public const string NavNoDetails = "nav.no-details";
        public const string NavTable = "nav.table";

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
        public const string GalaxyOpenSystem = "galaxy.open-system";

        // Where the camera is looking, as two things the player can ask for. The game has no words of
        // its own for either: it moves the camera on a double-click and a right-click and never names
        // the pair.
        public const string GalaxyShowSystemView = "galaxy.show-system-view";
        public const string GalaxyReturnToGalaxyView = "galaxy.return-to-galaxy-view";

        // The lanes out of a system. The game draws these as lines and writes nothing on them, and
        // where a line runs off into space it has no destination to name either.
        public const string GalaxyStarlane = "galaxy.starlane";
        public const string GalaxyStarlaneUnexplored = "galaxy.starlane-unexplored";
        public const string GalaxyWormhole = "galaxy.wormhole";
        public const string GalaxyWormholeUnexplored = "galaxy.wormhole-unexplored";
        public const string GalaxyFleetShips = "galaxy.fleet-ships";
        public const string GalaxyFleetMoving = "galaxy.fleet-moving";
        public const string GalaxyFleetMovement = "galaxy.fleet-movement";

        // The pause menu's icon-only toggle, and the word for a settings panel the game will only
        // show, not let you change.
        public const string GameMenuGameSettings = "gamemenu.game-settings";
        public const string GameMenuReadOnlySettings = "gamemenu.read-only-settings";

        // The save page: the name field, the prompt for typing into it, the cloud toggle, and what an
        // empty cell of the save table says.
        public const string LoadSaveSaveName = "loadsave.save-name";
        public const string LoadSaveEditName = "loadsave.edit-name";
        public const string LoadSaveCloud = "loadsave.cloud";
        public const string LoadSaveCellEmpty = "loadsave.cell-empty";

        /// <summary>What a control with nothing to offer says instead of opening an empty menu.
        /// </summary>
        public const string MenuNoActions = "menu.no-actions";

        // The star system management page. The panel names are the mod's, because the game draws the
        // panels as unlabelled boxes with an icon in the corner; everything a panel CONTAINS is read
        // in the game's own words.
        public const string ScreenStarSystem = "screen.star-system";
        public const string ScreenPlanet = "screen.planet";
        public const string SystemPlanetsPanel = "system.planets-panel";
        public const string SystemColonyPanel = "system.colony-panel";
        public const string SystemPopulationPanel = "system.population-panel";
        public const string SystemRepresentativesPanel = "system.representatives-panel";
        public const string SystemConstructiblesPanel = "system.constructibles-panel";
        public const string SystemQueuePanel = "system.queue-panel";
        public const string SystemHangarPanel = "system.hangar-panel";

        // The actions a control offers when Enter opens its menu, and the controls the game draws as
        // bare icons.
        public const string SystemColonize = "system.colonize";
        public const string SystemViewPlanet = "system.view-planet";
        public const string SystemRenamePlanet = "system.rename-planet";
        public const string SystemRenameSystem = "system.rename-system";
        public const string SystemMovePopulation = "system.move-population";
        public const string SystemImprovements = "system.improvements";
        public const string SystemLevel = "system.level";
        public const string SystemSecurity = "system.security";
        public const string SystemCancelConstruction = "system.cancel-construction";
        public const string SystemBuyOut = "system.buy-out";
        public const string SystemQueuePosition = "system.queue-position";
        public const string SystemProgress = "system.progress";
        public const string SystemIndustryCost = "system.industry-cost";
        public const string SystemSelectAllShips = "system.select-all-ships";
        public const string SystemCreateFleet = "system.create-fleet";
        public const string SystemRepairShips = "system.repair-ships";
        public const string SystemRetrofitShips = "system.retrofit-ships";
        public const string SystemScrapShips = "system.scrap-ships";
        public const string SystemSellShips = "system.sell-ships";
        public const string SystemShipSelected = "system.ship-selected";

        // The first-visit cutscene. It draws no heading of its own, so the mod says which system is
        // being shown; everything about each planet comes from the card in the game's own words.
        public const string ScreenSystemDiscovery = "screen.system-discovery";
        public const string DiscoverySystem = "discovery.system";

        /// <summary>The rename box the game opens over a page: what it is, and that typing has begun.
        /// </summary>
        public const string ScreenRename = "screen.rename";
        public const string RenameTypePrompt = "rename.type-prompt";
        public const string RenameConfirm = "rename.confirm";

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
            { NavExpanded, "expanded" },
            { NavCollapsed, "collapsed" },
            { NavChecked, "checked" },
            { NavUnchecked, "not checked" },
            { NavSelected, "selected" },
            { NavDisabled, "unavailable" },
            { NavHasTooltip, "has tooltip" },
            { NavNoDetails, "Nothing in here" },
            { NavTable, "table" },
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
            { GalaxyOpenSystem, "Open system" },
            { GalaxyShowSystemView, "Show system view" },
            { GalaxyReturnToGalaxyView, "Return to galaxy view" },
            { GalaxyStarlane, "Starlane to {0}" },
            { GalaxyStarlaneUnexplored, "Starlane to an unexplored system" },
            { GalaxyWormhole, "Wormhole to {0}" },
            { GalaxyWormholeUnexplored, "Wormhole to an unexplored system" },
            { GalaxyFleetShips, "{0} ships" },
            { GalaxyFleetMoving, "moving" },
            { GalaxyFleetMovement, "{0} movement points" },
            { GameMenuGameSettings, "Game settings" },
            { GameMenuReadOnlySettings, "read only" },
            { LoadSaveSaveName, "Save name" },
            { LoadSaveEditName, "Type the save name, then press Enter." },
            { LoadSaveCloud, "Steam cloud saves" },
            { LoadSaveCellEmpty, "empty" },
            { MenuNoActions, "Nothing to do here" },
            { ScreenStarSystem, "Star system" },
            { ScreenPlanet, "Planet" },
            { SystemPlanetsPanel, "Planets" },
            { SystemColonyPanel, "Colony" },
            { SystemPopulationPanel, "Population" },
            { SystemRepresentativesPanel, "Representatives" },
            { SystemConstructiblesPanel, "Available constructions" },
            { SystemQueuePanel, "Construction queue" },
            { SystemHangarPanel, "Hangar" },
            { SystemColonize, "Colonize" },
            { SystemViewPlanet, "View planet" },
            { SystemRenamePlanet, "Rename planet" },
            { SystemRenameSystem, "Rename system" },
            { SystemMovePopulation, "Move {0} to {1}" },
            { SystemImprovements, "System improvements" },
            { SystemLevel, "System level {0}" },
            { SystemSecurity, "Security" },
            { SystemCancelConstruction, "Cancel construction" },
            { SystemBuyOut, "Buy out with {0}" },
            { SystemQueuePosition, "position {0}" },
            { SystemProgress, "{0} percent built" },
            { SystemIndustryCost, "{0} industry" },
            { SystemSelectAllShips, "Select all ships" },
            { SystemCreateFleet, "Create fleet" },
            { SystemRepairShips, "Repair ships" },
            { SystemRetrofitShips, "Retrofit ships" },
            { SystemScrapShips, "Scrap ships" },
            { SystemSellShips, "Sell ships" },
            { SystemShipSelected, "selected" },
            { ScreenSystemDiscovery, "System discovery" },
            { DiscoverySystem, "Discovering {0}" },
            { ScreenRename, "Rename" },
            { RenameTypePrompt, "Type the new name, then press Enter." },
            { RenameConfirm, "Confirm" },
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
