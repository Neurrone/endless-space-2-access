namespace ES2Access.Core.Speech
{
    /// <summary>
    /// THE PAGES OUTSIDE A GAME, and the small screens that only ever need a name: the
    /// main menu's destinations, the new-game and join-game lobbies, the credits, the DLC and modding
    /// pages, and the end-game screens.
    ///
    /// Part of <see cref="ModStrings"/>; the English defaults for every key here live in the
    /// <c>Defaults</c> table with the rest.
    /// </summary>
    public static partial class ModStrings
    {
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
        /// own, a store button for what you do not - and nowhere in words.</summary>
        public const string DlcOwned = "dlc.owned";
        public const string DlcNotOwned = "dlc.not-owned";

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

        /// <summary>Where a notification popup's gateway button goes, used only where the popup wrote
        /// no caption and no tooltip on it. Asked for optionally, so a build without the phrase leaves
        /// the button to whatever the game did write rather than reading a key aloud.</summary>
        public const string NotifyOpenNegotiation = "notify.open-negotiation";
        public const string NotifyOpenMinorFaction = "notify.open-minor-faction";
        public const string NotifyOpenScoreScreen = "notify.open-score-screen";
        public const string NotifyOpenAcademy = "notify.open-academy";

        /// <summary>What the elimination popup means when the empire knocked out is the player's own: the
        /// game writes the same sentence for their defeat as for an AI's, and the only difference on screen
        /// is which buttons it draws.</summary>
        public const string NotifyOwnElimination = "notify.own-elimination";
    }
}
