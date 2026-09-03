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

        /// <summary>A bare percentage said as a whole phrase - the gauges and bars whose only reading
        /// IS a share, with no sentence of the game's own to put it in. A template rather than a "%"
        /// glued onto a number, because the sign is not a word: screen readers differ on whether they
        /// voice it at all, and a language that writes the unit in front, or spells it, or spaces it
        /// differently has nowhere to say so once it is glued. Where the phrase around the figure says
        /// anything more than "percent" it gets a template of its own instead.</summary>
        public const string Percent = "format.percent";

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

        /// <summary>The entry that opens it, on the main menu and the pause menu. The mod adds a
        /// real entry to each menu (owner ruling 2026-09-02), so these words are DRAWN on it as
        /// well as spoken - which is why they are short enough to fit the main menu's own entry on
        /// ONE line (owner ruling 2026-09-02), with the sentence beside them saying which mod.
        /// </summary>
        public const string ModSettingsEntry = "mod-settings.entry";
        public const string ModSettingsEntryDescription = "mod-settings.entry-description";

        /// <summary>What the General tab - the window's first - is called. It is where a setting
        /// that belongs to no other tab lives, and it says nothing further about itself: the tab
        /// carries NO tooltip (owner ruling 2026-09-02).</summary>
        public const string ModSettingsGeneral = "mod-settings.general";

        /// <summary>The General tab's one row: whether the game's cut scenes are described while
        /// they play. The setting itself lives in the BepInEx config file, which this row is the
        /// player's way into.</summary>
        public const string ModSettingsCutsceneDescriptions =
            "mod-settings.general.cutscene-descriptions";

        /// <summary>The General tab's other row: whether the tooltips the game assembles on hover -
        /// the long ones - are read out once they have drawn, or left to the review buffer. On by
        /// default; the description names the kinds of thing it is about, because "long" is not
        /// something a player can see from the setting.</summary>
        public const string ModSettingsAnnounceLongTooltips =
            "mod-settings.general.announce-long-tooltips";
        public const string ModSettingsAnnounceLongTooltipsDescription =
            "mod-settings.general.announce-long-tooltips-description";

        /// <summary>What the Scanner tab says about itself. The key-binding tab has no words of the
        /// mod's own at all: it wears the game's "%OptionToggleControlsTitle" and
        /// "%OptionToggleControlsDescription", so it matches the game's own Controls tab in every
        /// language (<see cref="ES2Access.UI.ModOptions.ModOptions.Categories"/>).</summary>
        public const string ModSettingsScanner = "mod-settings.scanner";
        public const string ModSettingsScannerDescription = "mod-settings.scanner-description";

        /// <summary>The Scanner tab's first row: whether the scanner says how far away a result is
        /// in short form - "23s" rather than "23 south".</summary>
        public const string ModSettingsScannerShortDirections =
            "mod-settings.scanner.short-directions";

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

        // --- the Controls tab's six tables (owner ruling 2026-09-02) ---

        /// <summary>What each block of key-binding rows is called. The Controls tab is not one
        /// table of eighty-one rows but six, each under its own heading, so "3 of 23" counts the
        /// table the player is standing in and the section jump walks the page by its six names.
        /// The layout itself - which action is in which table, and in what order - is
        /// <see cref="ES2Access.UI.Input.KeybindLayout"/>.</summary>
        public const string ModSettingsKeysCursor = "mod-settings.keys.cursor";
        public const string ModSettingsKeysBuffers = "mod-settings.keys.buffers";
        public const string ModSettingsKeysHotkeys = "mod-settings.keys.hotkeys";
        public const string ModSettingsKeysInspect = "mod-settings.keys.inspect";
        public const string ModSettingsKeysScanner = "mod-settings.keys.scanner";
        public const string ModSettingsKeysBookmarks = "mod-settings.keys.bookmarks";

        // --- the Bookmarks tab, the window's last (owner ruling 2026-09-02) ---

        /// <summary>What the tab is called, and what it says about itself. The tab holds no setting:
        /// it says where this campaign's map bookmarks are kept and offers the two ways of reaching
        /// them, which is what a player needs when a save changes hands.</summary>
        public const string ModSettingsBookmarks = "mod-settings.bookmarks";
        public const string ModSettingsBookmarksDescription = "mod-settings.bookmarks-description";

        /// <summary>The three sentences about where this campaign stands. A campaign nobody has
        /// saved has no identity to name a file after, so its bookmarks are held in memory until the
        /// first save; a saved campaign with no bookmarks has no file yet; and one with a file says
        /// the whole path, because the path is the thing the player came here for.</summary>
        public const string ModSettingsBookmarksUnsaved = "mod-settings.bookmarks.unsaved";
        public const string ModSettingsBookmarksNone = "mod-settings.bookmarks.none";
        public const string ModSettingsBookmarksSavedTo = "mod-settings.bookmarks.saved-to";

        /// <summary>The two buttons, and what the copy leaves in front of the file's own text - the
        /// name to save it under, which carries the campaign's GUID and is the one thing whoever
        /// receives it cannot work out.</summary>
        public const string ModSettingsBookmarksCopy = "mod-settings.bookmarks.copy";
        public const string ModSettingsBookmarksOpenFolder = "mod-settings.bookmarks.open-folder";
        public const string ModSettingsBookmarksCopyHeader = "mod-settings.bookmarks.copy-header";

        /// <summary>What the mod says after the copy. A clipboard changes nothing the player can
        /// see or hear, so the press has to say it landed.</summary>
        public const string ModSettingsBookmarksCopied = "mod-settings.bookmarks.copied";
        /// <summary>The copy could not read the file - gone or unreadable since the page was built.
        /// </summary>
        public const string ModSettingsBookmarksCopyFailed = "mod-settings.bookmarks.copy-failed";

        public const string ScreenLoading = "screen.loading";
        public const string ScreenNotification = "screen.notification";
        public const string ScreenTutorial = "screen.tutorial";
        public const string ScreenGalaxy = "screen.galaxy";
        public const string ScreenGameMenu = "screen.game-menu";
        public const string ScreenLoadSave = "screen.load-save";

        // How far a load has got, said at the quarter marks.
        public const string LoadingProgress = "loading.progress";

        // The name of the progress bar the loading window draws. The game draws it as a bare
        // rectangle and localizes no word for it, so the row it becomes is named here.
        public const string LoadingProgressBar = "loading.progress-bar";

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
            { Percent, "{0} percent" },
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
            { ModSettingsEntry, "Mod Settings" },
            { ModSettingsEntryDescription, "Endless Space 2 accessibility mod settings" },
            { ModSettingsGeneral, "General" },
            { ModSettingsCutsceneDescriptions, "Video descriptions in cut scenes" },
            { ModSettingsAnnounceLongTooltips, "Announce long tooltips" },
            {
                ModSettingsAnnounceLongTooltipsDescription,
                "Whether longer tooltips like improvements, heroes, technologies and ship designs "
                    + "are automatically read"
            },
            { ModSettingsScanner, "Scanner" },
            {
                ModSettingsScannerDescription,
                "Configure custom scanner categories and shortened directions"
            },
            { ModSettingsScannerShortDirections, "Shortened directions" },
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
            { ModSettingsKeysCursor, "Cursor and navigation" },
            { ModSettingsKeysBuffers, "Buffers" },
            { ModSettingsKeysHotkeys, "UI hotkeys" },
            { ModSettingsKeysInspect, "Inspect mode" },
            { ModSettingsKeysScanner, "Scanner" },
            { ModSettingsKeysBookmarks, "Bookmarks" },
            { ModSettingsBookmarks, "Bookmarks" },
            { ModSettingsBookmarksDescription, "Share this game's map bookmarks" },
            {
                ModSettingsBookmarksUnsaved,
                "Bookmarks are written to disk when this game is first saved."
            },
            { ModSettingsBookmarksNone, "No bookmarks set for this game" },
            { ModSettingsBookmarksSavedTo, "Bookmarks are saved to {0}" },
            { ModSettingsBookmarksCopy, "Copy bookmarks to clipboard" },
            { ModSettingsBookmarksOpenFolder, "Open bookmarks folder" },
            {
                ModSettingsBookmarksCopyHeader,
                "Save this text as {0} in the mod's bookmarks folder"
            },
            { ModSettingsBookmarksCopied, "Bookmarks copied to the clipboard" },
            { ModSettingsBookmarksCopyFailed, "The bookmarks file could not be read" },
            { ScreenLoading, "Loading" },
            { ScreenNotification, "Notification" },
            { ScreenTutorial, "Tutorial" },
            { ScreenGalaxy, "Galaxy" },
            { ScreenGameMenu, "Game menu" },
            { ScreenLoadSave, "Load or save" },
            { LoadingProgress, "{0} percent" },
            { LoadingProgressBar, "Loading progress" },
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
            { GalaxyOutpostHeld, "{0} outpost" },
            { GalaxyOutpostsHeld, "{0} outposts" },
            { GalaxyManageSystem, "Manage system" },
            { GalaxyShownOnMap, "Shown on the map" },
            { GalaxyQuestShownOnMap, "{0}, objective shown on the map" },
            { GalaxyStarlane, "{0} to {1}" },
            { GalaxyStarlaneUnexplored, "{0} to an unexplored system" },
            { GalaxyWormhole, "{0} to {1} by wormhole" },
            { GalaxyWormholeUnexplored, "{0} to an unexplored system by wormhole" },
            { GalaxyFleetArrivingLane, "arriving at {0} from {1} by star lane" },
            { GalaxyFleetArrivingLaneThisTurn, "arriving at {0} from {1} by star lane this turn" },
            { GalaxyFleetArrivingLaneTurns, "arriving at {0} from {1} by star lane in {2} turns" },
            { GalaxyFleetArrivingWormhole, "arriving at {0} from {1} by wormhole" },
            { GalaxyFleetArrivingWormholeThisTurn, "arriving at {0} from {1} by wormhole this turn" },
            { GalaxyFleetArrivingWormholeTurns, "arriving at {0} from {1} by wormhole in {2} turns" },
            {
                GalaxyFleetArrivingLaneDark,
                "arriving at {0} from an unexplored system to the {1} by star lane"
            },
            {
                GalaxyFleetArrivingLaneDarkThisTurn,
                "arriving at {0} from an unexplored system to the {1} by star lane this turn"
            },
            {
                GalaxyFleetArrivingLaneDarkTurns,
                "arriving at {0} from an unexplored system to the {1} by star lane in {2} turns"
            },
            {
                GalaxyFleetArrivingWormholeDark,
                "arriving at {0} from an unexplored system to the {1} by wormhole"
            },
            {
                GalaxyFleetArrivingWormholeDarkThisTurn,
                "arriving at {0} from an unexplored system to the {1} by wormhole this turn"
            },
            {
                GalaxyFleetArrivingWormholeDarkTurns,
                "arriving at {0} from an unexplored system to the {1} by wormhole in {2} turns"
            },
            { GalaxyFleetArrivingOpen, "arriving at {0} from the {1}" },
            { GalaxyFleetArrivingOpenThisTurn, "arriving at {0} from the {1} this turn" },
            { GalaxyFleetArrivingOpenTurns, "arriving at {0} from the {1} in {2} turns" },
            { GalaxyFleetEnRouteThisTurn, "en route to {0} this turn" },
            { GalaxyFleetEnRouteTurns, "en route to {0} in {1} turns" },
            {
                GalaxyFleetEnRouteUnexploredThisTurn,
                "en route to an unexplored system this turn"
            },
            {
                GalaxyFleetEnRouteUnexploredTurns,
                "en route to an unexplored system in {0} turns"
            },
            { GalaxyFleetFreeMovingToUnexplored, "free moving to an unexplored system" },
            { GalaxyFleetOnLaneToUnexplored, "on a star lane to an unexplored system" },
            { GalaxyFleetUnderWayNearbyOne, "{0} fleet under way nearby" },
            { GalaxyFleetsUnderWayNearby, "{0} fleets under way nearby" },
            { GalaxySystemPopulation, "{0} population" },
            { GalaxySystemSleepers, "{0} sleepers" },
            { GalaxySystemBuilding, "Building {0}, {1} turns" },
            { GalaxySystemNothingBuilding, "Nothing under construction" },
            { GalaxySystemRebellion, "Rebellion at {0} percent, {1} to go" },
            { GalaxySystemPiratePower, "Pirate power level {0}, {1} percent to the next level" },
            { GalaxySystemMinorRelation, "Relations {0} percent" },
            { GalaxySystemPirateLair, "Pirate lair" },
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
            { GalaxySystemStatusRegion, "Status" },
            { GalaxySystemActionsRegion, "Actions" },
            { GalaxySystemPlanetsRegion, "Planets" },
            { GalaxySystemLanesRegion, "Star lanes" },
            { GalaxySystemFleetsRegion, "Fleets" },
            { GalaxySystemResourcesRegion, "Resources" },
            { GalaxySystemDetailsRegion, "Details" },
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
            { OffsetNorthShort, "{0}n" },
            { OffsetSouthShort, "{0}s" },
            { OffsetEastShort, "{0}e" },
            { OffsetWestShort, "{0}w" },
            { GalaxyCoordinatePair, "{0}, {1}" },
            {
                GalaxyMapSummary,
                "{0} galaxy, {1} size, {2} by {3} units; galactic center {4} of {5}."
            },
            { GalaxyBookmarkSet, "Bookmark {0} set on {1}" },
            { GalaxyBookmarkSetReplacing, "Bookmark {0} set on {1}, replacing bookmark {2}" },
            { GalaxyBookmarkEmpty, "No bookmark {0}" },
            { GalaxyBookmarkSuffix, "bookmark {0}" },
            { GalaxyBookmarkPoint, "Bookmark {0} at {1}" },
            { GalaxyBookmarksGroup, "Bookmarks" },
            { GalaxyBookmarkShrink, "Shrink cursor so it contains only one system" },
            { GalaxyBookmarkNoHome, "No home system" },
            { GalaxyBookmarkFileHeader, "{0}, {1}, turn {2}" },
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
            { GalaxyScannerUnexploredLane, "Star lane from {0} heading {1}" },
            { GalaxyScannerUnexploredWormhole, "Wormhole from {0} heading {1}" },
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
            { GalaxyFleetShip, "{0} ship" },
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
            { HudPanel, "Hud" },
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
            { ScanLegendStop, "Captions" },
            { ScanOutput, "{0} {1}" },
            { ScanHeroEfficiency, "{0}%" },
            { Zoom, "Zoom" },
            { ZoomLevel, "Zoom level {0} of {1}" },
            { ZoomBandConstellations, "Constellations" },
            { ZoomBandSystemsAndLanes, "Systems and star lanes" },
            { ZoomBandSystemsLanesFleets, "Systems, star lanes and fleets" },
            { ZoomBandSystemDetails, "System details" },
            { ZoomBandOrbital, "Orbital" },
            { ScanSynergy, "{0} for {1}" },
            { ScanBattle, "Battle between {0}" },
            { ScanBattleHere, "Battle" },
            { ScanEmpireHome, "Home System {0}" },
            { ScanSystemOutputs, "Outputs" },
            { ScanSystemRankRegion, "System rank" },
            { ScanSystemRank, "rank {0} of {1}" },
            { ScanSystemRankHistory, "Rank history" },
            { ScanSystemInfoShown, "System information shown" },
            { ScanSystemInfoHidden, "System information hidden" },
            { ScanTradeRouteTo, "Trade route to {0}" },
            { ScanTradeRouteToBlockaded, "Trade route to {0}, blockaded" },
            { ScanTradeRouteThrough, "along trade route from {0} to {1}" },
            { ScanTradeLaneOpen, "carries trade route {0} to {1}, open" },
            { ScanTradeLaneBlockaded, "carries trade route {0} to {1}, blockaded" },
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
            { SystemNetPerTurn, "{0} per turn" },
            { FleetsPanelOpened, "Fleet panel open for {0}" },
            { FleetsPanelClosed, "Fleet panel closed" },
            { FleetsActionsPanel, "Fleet actions" },
            { FleetsFleetsPanel, "Fleets" },
            { FleetsHeroPanel, "Hero" },
            { FleetsShipsPanel, "Ships" },
            { FleetsShipRange, "{0} ship selected, {1} to {2}" },
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
            { EmpirePlanetActionsPanelOpened, "Planet actions panel open for {0}" },
            { EmpirePlanetActionsPanelClosed, "Planet actions panel closed" },
            { EmpirePopulationPanelOpened, "Population panel open for {0}" },
            { EmpirePopulationPanelClosed, "Population panel closed" },
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
            { EconomyTaxesPanel, "Marketplace taxes" },
            { EconomyLogPanel, "Recent transactions" },
            { EconomyAdsPanel, "Advertisements" },
            { EconomyEventsPanel, "Market events" },
            { EconomyInflationPanel, "Inflation" },
            { EconomyTradePanel, "Trade overview" },
            { EconomyPriceHistory, "Price history" },
            { EconomyPriceHistoryTurn, "Turn {0}" },
            { EconomyIncrement, "Increment" },
            { EconomyDecrement, "Decrement" },
            { EconomyLocation, "Location" },
            { EconomyOwner, "Owner" },
            { EconomyTaxRate, "Tax rate" },
            { EconomyFilters, "Filters" },
            { EconomyAvailable, "Available" },
            { EconomySellingWhat, "Selling {0}" },
            { EconomyBuyingWhat, "Buying {0}" },
            { EconomySellTransaction, "Sell transaction" },
            { EconomyBuyTransaction, "Buy transaction" },
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
            { PirateThresholdReached, "Threshold {0}, reached" },
            { PirateThresholdProgress, "Threshold {0}, {1} percent" },
            { ScreenAcademyDiplomacy, "Academy diplomacy" },
            { ScreenAcademyModal, "The Academy" },
            { AcademyRelation, "Relation" },
            { AcademyNamedShips, "Named ships" },
            { DiplomacyActionsBand, "Actions" },
            { DiplomacyTreasury, "Treasury" },
            { MinorBand, "{0} ({1})" },
            { MinorRelationship, "Relationship" },
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
        private static string _language;

        /// <summary>
        /// Make <paramref name="overrides"/> the active translation overlay for a language whose
        /// plural rule is English's. Null or empty clears the overlay back to the English defaults.
        /// </summary>
        public static void Install(IDictionary<string, string> overrides)
        {
            Install(overrides, null);
        }

        /// <summary>
        /// Make <paramref name="overrides"/> the active translation overlay, spoken as
        /// <paramref name="language"/> - the game's own language name, which is also the name of
        /// the translation file. Null or empty overrides clear back to the English defaults, which
        /// is also the right result for a language with no translation file; the language is still
        /// recorded, because <see cref="Plural"/>'s rule belongs to the language rather than to the
        /// file.
        /// </summary>
        public static void Install(IDictionary<string, string> overrides, string language)
        {
            Warned.Clear();
            _language = language;
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

        /// <summary>Drop the overlay, returning to English defaults and English's plural rule.</summary>
        public static void Reset()
        {
            _overrides = null;
            _language = null;
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
        /// with a single form writes the same sentence twice.
        ///
        /// The forms BEYOND those two are real, and they live in the locale file rather than in a
        /// call site - English needs none of them and ships none, so no caller passes a third key
        /// and adding a language costs no code. <see cref="PluralKey"/> is where they are chosen,
        /// and is also what a caller uses whose count is not the phrase's only slot.
        /// </summary>
        public static string Plural(string oneKey, string manyKey, int count)
        {
            return Format(PluralKey(oneKey, manyKey, count), count);
        }

        /// <summary>
        /// Which of a counted pair's KEYS a number calls for, by the same rules as
        /// <see cref="Plural"/>, for the callers that cannot let it do the formatting: a phrase
        /// whose count is not its only slot ("Probe launched towards {0}, {1} probes remaining"),
        /// and one whose two forms take different arguments altogether ("Arrives this turn, {0}
        /// movement" against "{0} turns, {1} movement"). Such a caller compares the answer against
        /// the ONE key to know which argument list goes with it.
        ///
        /// Three forms, two of which live in the locale file rather than at the call site. The
        /// paucal is <c>&lt;manyKey&gt;.few</c> (<see cref="PluralRules.FewSuffix"/>). The other is
        /// <c>&lt;manyKey&gt;.one</c> (<see cref="PluralRules.OneSuffix"/>), for the singular with a
        /// count that is not one - Russian's 21, 31 and so on, where a pair whose ONE sentence has
        /// no number in it would otherwise say something untrue. A count of one always takes
        /// <paramref name="oneKey"/>, and a missing form falls back to the key it hangs off, so a
        /// language that carries neither is spoken exactly as it was before either existed.
        /// </summary>
        public static string PluralKey(string oneKey, string manyKey, int count)
        {
            switch (PluralRules.For(_language, count))
            {
                case PluralForm.One:
                    if (count != 1)
                    {
                        string singularKey = manyKey + PluralRules.OneSuffix;
                        if (Has(singularKey))
                        {
                            return singularKey;
                        }
                    }

                    return oneKey;

                case PluralForm.Few:
                    string fewKey = manyKey + PluralRules.FewSuffix;
                    return Has(fewKey) ? fewKey : manyKey;

                default:
                    return manyKey;
            }
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

        /// <summary>
        /// Every key the mod ships an English phrase for, across all four default tables.
        ///
        /// <see cref="TryGetDefault"/> answers "is this key ours"; this answers "what are all of
        /// them", which is the question the translation template has to match in BOTH directions -
        /// a key missing from english.json is a phrase no translator is ever offered, and there is
        /// no way to see that from the individual lookups.
        /// </summary>
        public static IEnumerable<string> DefaultKeys()
        {
            foreach (string key in Defaults.Keys)
            {
                yield return key;
            }

            foreach (string key in IconDefaults.Keys)
            {
                yield return key;
            }

            foreach (string key in HintDefaults.Keys)
            {
                yield return key;
            }

            foreach (string key in ActionDefaults.Keys)
            {
                yield return key;
            }
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
