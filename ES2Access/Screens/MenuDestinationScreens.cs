using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// The five pages the main menu can open that the mod has no model of yet: the DLC browser, the
    /// credits, the mod manager, the resource exporter and the multiplayer lobby list.
    ///
    /// This is a MINIMUM PASS, and it exists for one reason: a declared main-menu entry that opens a page
    /// the mod says nothing about is a silent dead end. The player presses Enter on "Credits", the menu
    /// disappears, nothing is spoken, and every key they try goes to a page they cannot hear. That is worse
    /// than the entry not being there. So each of these gets the floor: arriving says where you are, the
    /// controls the page drew with words on them are reachable, and Escape leaves - which is verified from
    /// the game's own code in each subclass rather than assumed.
    ///
    /// What is NOT here is a model of any of these pages: the DLC list and its three tabs, the mod list and
    /// its ordering, the exporter's own picker, the lobby table. Each is a screen's worth of work and each
    /// is named in its subclass as deferred, so nobody reads this as finished.
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
    /// DEFERRED: the lobby table itself, which is a <c>GuiTable</c> and so is a short job once somebody can
    /// reach a session to test it against.
    ///
    /// Escape: <c>HandleInput</c> shows the main menu again.
    /// </summary>
    public sealed class JoinGameListScreen : MenuDestinationScreen
    {
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
    }
}
