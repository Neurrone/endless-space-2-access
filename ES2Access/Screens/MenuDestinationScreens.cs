using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// The five pages the main menu can open that the mod has no model of yet: the DLC browser, the
    /// credits, the mod manager, the resource exporter - and the multiplayer lobby list, which has
    /// outgrown this floor and only shares the frame (see <see cref="JoinGameListScreen"/>).
    ///
    /// This is a MINIMUM PASS, and it exists for one reason: a declared main-menu entry that opens a page
    /// the mod says nothing about is a silent dead end. The player presses Enter on "Credits", the menu
    /// disappears, nothing is spoken, and every key they try goes to a page they cannot hear. That is worse
    /// than the entry not being there. So each of these gets the floor: arriving says where you are, the
    /// controls the page drew with words on them are reachable, and Escape leaves - which is verified from
    /// the game's own code in each subclass rather than assumed.
    ///
    /// What is NOT here is a model of any of these pages: the DLC list and its three tabs, the mod list and
    /// its ordering, the exporter's own picker. Each is a screen's worth of work and each is named in its
    /// subclass as deferred, so nobody reads this as finished.
    ///
    /// All five share layer 0 with the main menu and the new-game lobby, and for the same reason: they
    /// REPLACE the menu rather than floating over it. Four of them are shown after the menu has been
    /// hidden and put it back on the way out (each one's <c>HandleInput</c> shows <c>MainMenuScreen</c>
    /// again), and the DLC browser is a modal, which the mod's own main-menu screen already steps aside for
    /// (it reports itself inactive while any modal is visible). So no two of them are ever live together,
    /// which is what one number means.
    /// </summary>
    public abstract class MenuDestinationScreen : Screen
    {
        private readonly List<Cell> _cells = new List<Cell>();

        /// <summary>The window this page is, or null while the game is not showing it.</summary>
        protected abstract GuiWindow Window();

        /// <summary>The key prefix for this page's controls, and the mod's own name for it - used only
        /// where the page draws no heading of its own.</summary>
        protected abstract string Prefix { get; }

        protected abstract string ScreenNameKey { get; }

        public override int Layer
        {
            get { return 0; }
        }

        public override string ScreenName
        {
            get
            {
                string title = WindowShape.Title(Window());
                return string.IsNullOrEmpty(title) ? OptionalText.Phrase(ScreenNameKey) : title;
            }
        }

        public override bool IsActive()
        {
            try
            {
                GuiWindow window = Window();
                return window != null && window.Shown && window.IsReady;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The game's, on all five - each answers Exit itself, and what it does with it differs
        /// per page (back to the menu, or a confirmation for unapplied changes). Answering it here would
        /// replace a decision the game has already made.</summary>
        public override bool Back()
        {
            return false;
        }

        public override void Build(GraphBuilder builder)
        {
            GuiWindow window = Window();
            if (window == null)
            {
                return;
            }

            _cells.Clear();
            WindowShape.Controls(_cells, window, Prefix);
            Cells.Emit(builder, _cells);
        }

        protected static T Get<T>()
            where T : GuiWindow
        {
            try
            {
                return Gui.GuiServiceAvailable ? Gui.GuiService.GetWindow<T>(false) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// The downloadable-content browser (<c>DLCModalWindow</c>).
    ///
    /// DEFERRED: the three tabs and the list of content items with their own activation boxes. What is
    /// declared is whatever the window drew with words on it, which includes the tabs and Apply.
    ///
    /// Escape: <c>HandleInput</c> hides the window, or raises the game's own confirmation box first if the
    /// player has changed something ("%DLCModalWindowExitConfirmation").
    /// </summary>
    public sealed class DLCScreen : MenuDestinationScreen
    {
        public override string Key
        {
            get { return "screen.dlc"; }
        }

        protected override string Prefix
        {
            get { return "dlc"; }
        }

        protected override string ScreenNameKey
        {
            get { return "screen.dlc"; }
        }

        protected override GuiWindow Window()
        {
            return Get<DLCModalWindow>();
        }
    }

    /// <summary>
    /// The credits (<c>CreditScreen</c>).
    ///
    /// DEFERRED, and named here because it is the whole page: the credit text itself. The screen builds
    /// hundreds of lines out of a text asset and scrolls them past on a timer, so making it readable means
    /// one review buffer holding the whole roll - not hundreds of nodes rebuilt on every keypress. Until
    /// then this page announces itself and leaves.
    ///
    /// Escape: <c>HandleInput</c> shows the main menu again.
    /// </summary>
    public sealed class CreditsScreen : MenuDestinationScreen
    {
        public override string Key
        {
            get { return "screen.credits"; }
        }

        protected override string Prefix
        {
            get { return "credits"; }
        }

        protected override string ScreenNameKey
        {
            get { return "screen.credits"; }
        }

        protected override GuiWindow Window()
        {
            return Get<CreditScreen>();
        }
    }

    /// <summary>
    /// The mod manager (<c>ModdingScreen</c>).
    ///
    /// DEFERRED: the two lists of mods and the moving of one between them, which is what the page is for.
    /// Also reachable from the load-save box and the lobby list, both of which hide themselves first
    /// (<c>OnFixModsCb</c>), which is why this can share the menu's layer.
    ///
    /// Escape: <c>HandleInput</c> shows the main menu again, behind the game's own confirmation box where
    /// the player has changed the configuration ("%ModdingScreenExitConfirmation").
    /// </summary>
    public sealed class ModdingConfigScreen : MenuDestinationScreen
    {
        public override string Key
        {
            get { return "screen.modding"; }
        }

        protected override string Prefix
        {
            get { return "modding"; }
        }

        protected override string ScreenNameKey
        {
            get { return "screen.modding"; }
        }

        protected override GuiWindow Window()
        {
            return Get<ModdingScreen>();
        }
    }

    /// <summary>
    /// The resource exporter - a modding tool that writes the game's own data out to files
    /// (<c>ResourcesExportScreen</c>).
    ///
    /// DEFERRED: the resource list and the export itself.
    ///
    /// Escape: <c>HandleInput</c> shows the main menu again - except while an export is running, when the
    /// window swallows every action until it finishes. That is the game's decision and is left alone.
    /// </summary>
    public sealed class ResourcesExportModScreen : MenuDestinationScreen
    {
        public override string Key
        {
            get { return "screen.resources-export"; }
        }

        protected override string Prefix
        {
            get { return "resources-export"; }
        }

        protected override string ScreenNameKey
        {
            get { return "screen.resources-export"; }
        }

        protected override GuiWindow Window()
        {
            return Get<ResourcesExportScreen>();
        }
    }

    /// <summary>
    /// The list of multiplayer games to join (<c>JoinGameScreen</c>).
    ///
    /// The page is one <c>GuiTable</c> of Steam lobbies and three buttons under it, so it is a
    /// <see cref="TableSheet"/> over <c>JoinGameScreen.Table</c> and nothing else: the ten columns of the
    /// <c>JoinGameSessionListTable</c> column set, their captions and their crossed edges are the shared
    /// reading every table in the mod gets, a row is the game's own select-then-act (Enter picks a lobby,
    /// which is what enables Join), and a lobby the game will not let you into arrives already switched
    /// off, which is <see cref="TableSheet"/>'s refused-row case (<c>JoinGameScreen.Refresh</c> :239 hands
    /// the table its <c>invalidLobbyInfo</c> as the disabled set). Joining is left to the Join button:
    /// the game's own second route is a DOUBLE click on a row, and a single Enter that both picked a lobby
    /// and connected to it would make every pass over the list a decision.
    ///
    /// Two columns draw no words of their own and are read by <see cref="ReadValue"/>: the player count is
    /// a gauge with an "n/m" label (<c>GuiTableCellRatio</c>), said as the fraction phrase every other
    /// count in the mod uses and taken from the lobby's own numbers rather than from the drawn digits; and
    /// the content column is a strip of downloadable-content ICONS (<c>GuiTableCellDownloadableContent</c>)
    /// whose names are on tooltips the tooltip window assembles from a CLASS, so nothing on the widgets
    /// can be read and the names are taken from what the game wrote into each item. The third column the
    /// audit expected to need a hook - the mods column - needs none: <c>GuiTableCellMods</c> writes a real
    /// label ("Valid"/"Fixable"/"Invalid") and a plain tooltip, so it reads like any text column.
    ///
    /// The list ARRIVES LATE, and that is the one thing about this page that is not a widget. Opening it
    /// asks Steam for the lobby list (<c>OnBeginShow</c> :220) and the answer comes back seconds later on
    /// a callback (<c>SteamMatchMaking_CallbackRequestLobbyList</c> :382-425); a sighted player watches
    /// the spinner and then the rows, and a blind player would hear an empty page and never learn it had
    /// filled. So the search's end is announced with what it found. The page's own state is private, so
    /// the search is read off what it DRAWS: the Refresh button is switched off for exactly as long as it
    /// runs (<c>RefreshButtons</c> :299,:307 are the only writers of that flag).
    ///
    /// Escape: <c>HandleInput</c> shows the main menu again.
    /// </summary>
    public sealed class JoinGameListScreen : MenuDestinationScreen
    {
        private static readonly object HeadersStop = "join-game:headers";
        private static readonly object LinesStop = "join-game:lines";
        private static readonly object ActionsStop = "join-game:actions";

        /// <summary>How long the arrival announcement waits for the rows: the callback puts the page back
        /// to idle and marks the window dirty, and the lines are not rebuilt until the window's own
        /// refresh runs, so counting them on the frame the search ended counts the OLD list.</summary>
        private const int SettleFrames = 5;

        private readonly TableSheet _table;

        // Reused across builds rather than allocated per frame: Build runs every tick.
        private readonly List<AgeTransform> _buttons = new List<AgeTransform>();

        /// <summary>Whether there is a search whose answer has not been announced yet, and how many
        /// frames are left before it is.</summary>
        private bool _pending;

        private int _settling;

        public JoinGameListScreen()
        {
            _table = new TableSheet("join-game:", LobbyOf);
            _table.RowName = LobbyName;
            _table.ReadValue = ReadCellValue;
            _table.RowDetails = RowExtras;
        }

        public override string Key
        {
            get { return "screen.join-game"; }
        }

        protected override string Prefix
        {
            get { return "join-game"; }
        }

        protected override string ScreenNameKey
        {
            get { return "screen.join-game"; }
        }

        protected override GuiWindow Window()
        {
            return Get<JoinGameScreen>();
        }

        /// <summary>Arriving is itself an unannounced search: the page asks Steam the moment it is shown
        /// (<c>OnBeginShow</c> :220), and how long that takes is Steam's business - it can be over before
        /// this screen has run a frame, or seconds later. So a visit starts owing the player a count.
        /// </summary>
        public override void OnPush()
        {
            _pending = true;
            _settling = SettleFrames;
        }

        /// <summary>What a finished search found, once it has finished and the rows it found have been
        /// drawn. Nothing is said while one is running, so a manual refresh answers once rather than
        /// twice, and the answer waits for the page to be idle however long that takes.</summary>
        public override void OnUpdate()
        {
            JoinGameScreen window = Page();
            if (window == null)
            {
                return;
            }

            if (Searching(window))
            {
                _pending = true;
                _settling = SettleFrames;
                return;
            }

            if (!_pending || _settling-- > 0)
            {
                return;
            }

            _pending = false;
            int found = _table.Lines(window.Table).Count;
            Voice.Say(
                found == 0
                    ? ModStrings.Get(ModStrings.JoinGameNoGames)
                    : ModStrings.Plural(
                        ModStrings.JoinGameGameFound,
                        ModStrings.JoinGameGamesFound,
                        found
                    ),
                false
            );
        }

        public override void Build(GraphBuilder builder)
        {
            JoinGameScreen window = Page();
            GuiTable table = window == null ? null : window.Table;
            if (table == null)
            {
                return;
            }

            builder.BeginStop(HeadersStop);
            _table.Headers(builder, table);

            builder.BeginStop(LinesStop);
            if (_table.Lines(table).Count == 0)
            {
                // A page whose whole content is a list the search came back empty for. Declared as a
                // line rather than left as an empty stop: the player has to be able to land on the
                // answer, and "nothing here" is the answer.
                builder.AddItem(
                    ControlId.Structural("join-game:empty"),
                    GraphNodes.Readout(
                        () => null,
                        () => ModStrings.Get(ModStrings.JoinGameNoGames),
                        null,
                        null
                    )
                );
            }
            else
            {
                _table.Rows(builder, table, ScreenName);
            }

            builder.BeginStop(ActionsStop);
            BuildButtons(builder, window);
        }

        /// <summary>Back, Refresh, Join and - when the selected lobby's mods can be fixed - Fix Mods, on
        /// one row in the order they are drawn. Taken from the band they share rather than from the
        /// window's own fields, which is what makes Fix Mods arrive on its own when the game shows it.
        /// </summary>
        private void BuildButtons(GraphBuilder builder, JoinGameScreen window)
        {
            _buttons.Clear();
            AgeTransform band = Parent(window.JoinButton);
            IList<AgeTransform> children = band == null ? null : band.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                _buttons.Add(children[i]);
            }

            SettingRows.AddButtonRow(builder, _buttons, "join-game:button/");
        }

        /// <summary>Whether the Steam search is running, read off the page's own drawing: the state
        /// itself is private and the Refresh button is switched off for exactly that time.</summary>
        private static bool Searching(JoinGameScreen window)
        {
            try
            {
                return window.RefreshButton != null && !window.RefreshButton.Enable;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The lobby a row stands for, as the number Steam calls it. The wrapper and the
        /// <c>LobbyInfo</c> under it are both rebuilt on every search (<c>CallbackRequestLobbyList</c>
        /// :392), and <c>Steamworks.SteamID</c> is a class that overrides no equality, so neither of them
        /// can key a row across a refresh; the id itself can.</summary>
        private static object LobbyOf(GuiTableLine line)
        {
            LobbyInfo lobby = Lobby(line);
            try
            {
                return lobby == null || lobby.SteamIDLobby == null
                    ? null
                    : (object)lobby.SteamIDLobby.UInt64AccountID;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What the row is called when the name column draws nothing - the session's own
        /// name.</summary>
        private static string LobbyName(GuiTableLine line)
        {
            LobbyInfo lobby = Lobby(line);
            try
            {
                return lobby == null ? null : AgeText.Clean(lobby.Name);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What a lobby says beyond its columns: which victory conditions it is playing to. The
        /// game hangs it on the NAME column as that column's description (the column set names
        /// <c>VictoryConditions</c> as the Name column's <c>Description</c>), and the name column is the
        /// row itself - whose own tooltip is the LINE's - so it would otherwise be the one thing on the
        /// row nothing reads. Taken from the lobby, which is where the game reads it from too.</summary>
        private static IList<string> RowExtras(GuiTableLine line)
        {
            try
            {
                GuiLobbyInfo lobby = line == null ? null : line.Data as GuiLobbyInfo;
                string said = lobby == null ? null : AgeText.Clean(lobby.VictoryConditions);
                return string.IsNullOrEmpty(said) ? null : new List<string> { said };
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The two columns the game draws without words. Null everywhere else, which is the
        /// shared reading of a cell.</summary>
        private static string ReadCellValue(GuiTableHeader header, AgeTransform cell)
        {
            if (cell == null)
            {
                return null;
            }

            GuiTableCellRatio ratio = cell.GetComponent<GuiTableCellRatio>();
            if (ratio != null)
            {
                return PlayerCount(cell);
            }

            GuiTableCellDownloadableContent content =
                cell.GetComponent<GuiTableCellDownloadableContent>();
            return content == null ? null : ContentNames(content);
        }

        /// <summary>How many of a lobby's seats are taken, as the fraction phrase rather than as the
        /// "3/8" the cell paints. Read from the lobby's own numbers - the same <c>IRatioProvider</c> the
        /// cell fills its gauge from (<c>GuiTableCellRatio.Refresh</c> :30-46) - because the digits in the
        /// label are that reading already formatted for the eye.</summary>
        private static string PlayerCount(AgeTransform cell)
        {
            try
            {
                LobbyInfo lobby = Lobby(LineOf(cell));
                return lobby == null
                    ? null
                    : new MessageBuilder()
                        .PushFraction(lobby.CurrentPlayerCount, lobby.SlotCount)
                        .Build();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Which exclusive downloadable content a lobby is using, out of the strip of icons the
        /// column draws. Each item's name is the words the game wrote onto its tooltip
        /// (<c>DLCItemMinimal.Refresh</c>), which is the only place they exist: the tooltip carries a
        /// CLASS, so the tooltip window assembles what is shown and there is nothing on the widget for
        /// the shared cell reading to find.</summary>
        private static string ContentNames(GuiTableCellDownloadableContent content)
        {
            try
            {
                MessageBuilder names = new MessageBuilder();
                IList<AgeTransform> items =
                    content.DLCItemsTable == null ? null : content.DLCItemsTable.Children;
                for (int i = 0; items != null && i < items.Count; i++)
                {
                    AgeTransform item = items[i];
                    DLCItemMinimal drawn =
                        item == null || !item.Visible ? null : item.GetComponent<DLCItemMinimal>();
                    if (drawn == null || drawn.Tooltip == null)
                    {
                        continue;
                    }

                    // The content's own localized name, off the wrapper the item hung on its tooltip.
                    // What the item wrote into the tooltip's CONTENT is the identifier the tooltip
                    // window then looks a title up from ("DLCVaulters"), so it is only the fallback.
                    string name = AgeWidgets.TooltipTitle(drawn.Tooltip);
                    names.ListItem(
                        string.IsNullOrEmpty(name) ? AgeText.Clean(drawn.Tooltip.Content) : name
                    );
                }

                string said = names.Build();
                return string.IsNullOrEmpty(said) ? null : said;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static LobbyInfo Lobby(GuiTableLine line)
        {
            try
            {
                GuiLobbyInfo wrapper = line == null ? null : line.Data as GuiLobbyInfo;
                return wrapper == null ? null : wrapper.LobbyInfo;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The row a cell belongs to. A cell is handed to <c>ReadValue</c> on its own, and what
        /// it is showing belongs to the lobby its row stands for.</summary>
        private static GuiTableLine LineOf(AgeTransform cell)
        {
            try
            {
                AgeTransform widget = cell;
                for (int i = 0; widget != null && i < 4; i++)
                {
                    GuiTableLine line = widget.GetComponent<GuiTableLine>();
                    if (line != null)
                    {
                        return line;
                    }

                    widget = widget.Parent;
                }
            }
            catch (Exception) { }

            return null;
        }

        private JoinGameScreen Page()
        {
            return Window() as JoinGameScreen;
        }

        private static AgeTransform Parent(AgeTransform widget)
        {
            try
            {
                return widget == null ? null : widget.Parent;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
