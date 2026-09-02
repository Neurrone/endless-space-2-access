namespace ES2Access.Core.Speech
{
    /// <summary>
    /// The HEADS-UP DISPLAY and the chrome every screen shares: the turn bar and the
    /// empire banner, the pause and load pages, saving, and the drag-and-drop and queueing gestures.
    ///
    /// Part of <see cref="ModStrings"/>; the English defaults for every key here live in the
    /// <c>Defaults</c> table with the rest.
    /// </summary>
    public static partial class ModStrings
    {
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

        /// <summary>The cluster itself, over those rows: the one Tab stop that had no word of its own,
        /// so a player landing on it heard the row and never the place. The game captions the corner
        /// with nothing at all, so the word is the mod's.</summary>
        public const string HudPanel = "hud.panel";

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
    }
}
