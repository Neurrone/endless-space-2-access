using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// The FRAME the pages the main menu opens share: the DLC browser, the credits, the mod manager, the
    /// multiplayer lobby list, the resource exporter - and the boot disclaimer, which arrives over the menu
    /// rather than from it.
    ///
    /// What is shared is not a model but the four answers every one of these pages gives identically: it is
    /// live exactly while the game is showing its window, it is named by the heading it drew (falling back
    /// to a phrase of the mod's where it drew none), it sits on layer 0, and it does NOT answer the back
    /// key. The last two are the ones worth writing down:
    ///
    /// Layer 0 is shared with the main menu and the new-game lobby because these pages REPLACE the menu
    /// rather than floating over it. Most are shown after the menu has been hidden and put it back on the
    /// way out (each one's <c>HandleInput</c> shows <c>MainMenuScreen</c> again), and the modals among them
    /// - the DLC browser, the disclaimer - are covered by the mod's own main-menu screen reporting itself
    /// inactive while any modal is visible. So no two of them are ever live together, which is what one
    /// number means.
    ///
    /// Escape is left to the game on all of them, because what it does differs per page and the game has
    /// already decided: back to the menu, a confirmation for unapplied changes, or - on the disclaimer -
    /// nothing at all.
    ///
    /// <see cref="Build"/> defaults to the shape floor (<see cref="WindowShape"/>): the heading, plus every
    /// control the page drew words on. That is what a page with no model of its own would get, and it is
    /// deliberately not enough to call such a page finished - every one of these pages now overrides
    /// <see cref="Build"/> with a model of its own content, so the floor is only what a page ADDED here
    /// starts from.
    /// </summary>
    public abstract class MenuDestinationScreen : Screen
    {
        /// <summary>Where these pages write their heading when it is not in one of the names every window
        /// shares. Measured on the DLC browser and the mod manager, which both call it
        /// "WindowTitleLabel"; tried only after the shared names, so it cannot rename a page that already
        /// answers.</summary>
        private static readonly string[] OutGameTitleNames = { "WindowTitleLabel" };

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
                string title = WindowShape.Title(Window(), OutGameTitleNames);
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

        /// <summary>The game's, on every one of them - each answers Exit itself, and what it does with it
        /// differs per page. Answering it here would replace a decision the game has already made.
        /// </summary>
        public override bool Back()
        {
            return false;
        }

        /// <summary>The shape floor: the controls this page drew words on, in the rows it drew them.
        /// Overridden by every page that has a model of its own.</summary>
        public override void Build(GraphBuilder builder)
        {
            GuiWindow window = Window();
            if (window == null)
            {
                return;
            }

            _cells.Clear();
            WindowShape.Controls(_cells, window, Prefix);
            Cells.EmitLinear(builder, _cells);
        }

        /// <summary>
        /// The heading, as the page's first row - but only where the game hung an EXPLANATION on it.
        ///
        /// The words themselves are already the screen's spoken name, so a plain heading is a step past
        /// nothing and stays a name only. A heading with a sentence behind it is the other case the
        /// caption rule names: the spoken name has no review buffer, so those words have nowhere else
        /// to live and the heading is a row as well (<see cref="Captions.Row"/>). Three of these pages
        /// draw one - the DLC browser, the mod manager and the lobby list - and the row does not exist
        /// on the pages that do not.
        /// </summary>
        protected void AddTitle(GraphBuilder builder)
        {
            AgeTransform title = WindowShape.TitleWidget(Window(), OutGameTitleNames);
            Captions.Row(builder, title, Prefix + ":title", Parent(title));
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

            builder.BeginStop(LinesStop);
            // GAME DEFECT, read as drawn: this page's prefab title carries the ADVANCED SETTINGS
            // sentence, which belongs to another window entirely. Nothing here suppresses or rewords
            // it - what a sighted player reads on hover is what this row says (owner ruling: parity).
            AddTitle(builder);
            _table.Headers(builder, table);
            if (_table.Lines(table).Count == 0)
            {
                // A page whose whole content is a list the search came back empty for. Declared as a
                // line rather than left as an empty stop: the player has to be able to land on the
                // answer, and "nothing here" is the answer.
                builder.AddItem(Nodes.Synthetic(
                    ControlId.Structural("join-game:empty"),
                    GraphNodes.Readout(
                        () => null,
                        () => ModStrings.Get(ModStrings.JoinGameNoGames),
                        null,
                        null
                    )
                ));
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

            SettingRows.AddButtons(builder, _buttons, "join-game:button/");
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
