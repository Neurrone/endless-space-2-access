using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// Every hero in the galaxy, whoever is employing them - the list the Academy's own box opens when the
    /// empire is entitled to see it (<c>AcademyInfoSidePanel.OnCompleteHeroListCb</c> :207-210, behind a
    /// button the game refuses with its own sentence for an empire that owns neither the Academy nor the
    /// Librarian role).
    ///
    /// A leaf sheet, and nothing more: the window binds its <c>GuiTable</c> with
    /// <c>canSelect: false</c> (:23) and leaves both <c>OnLineSelection</c> and <c>OnLineDoubleClick</c>
    /// empty (:79-85), so there is nothing to pick and nothing a row does. <see cref="TableSheet"/> reads
    /// that off the table itself - the flag lives on <c>LinesTable</c>'s own Enable - and declares the rows
    /// as the plain lines they are rather than as radios that would offer a choice the window does not
    /// have. The columns, their captions and the crossed edges are the shared reading of the
    /// <c>HeroCompleteListTable</c> column set.
    ///
    /// Escape is the game's: a <c>GuiModalWindow</c> with no <c>HandleInput</c>, so Exit hides it - and
    /// the Close button the window draws is declared anyway, because it is drawn and it is the way a mouse
    /// leaves.
    /// </summary>
    public sealed class HeroCompleteListScreen : Screen
    {
        private static readonly object LinesStop = "hero-list:lines";
        private static readonly object ActionsStop = "hero-list:actions";

        private readonly List<Cell> _cells = new List<Cell>();

        private readonly TableSheet _table;

        public HeroCompleteListScreen()
        {
            _table = new TableSheet("hero-list:", HeroOf);
            _table.RowName = HeroName;
        }

        public override string Key
        {
            get { return "screen.hero-complete-list"; }
        }

        /// <summary>Over the Academy page that opens it, and below everything this window could raise over
        /// itself.</summary>
        public override int Layer
        {
            get { return 27; }
        }

        /// <summary>What the window has written across its top. It does not expose the label, so it is
        /// found where it is drawn; the mod's own word covers the frames before it is written.</summary>
        public override string ScreenName
        {
            get
            {
                string title = Title(Window());
                return string.IsNullOrEmpty(title)
                    ? ModStrings.Get(ModStrings.ScreenHeroCompleteList)
                    : title;
            }
        }

        /// <summary>The table, because it is drawn first and Tab does not wrap - its headings are the
        /// row above its first line, not a stop of their own.</summary>
        public override object InitialFocusStop
        {
            get { return LinesStop; }
        }

        public override bool IsActive()
        {
            try
            {
                HeroCompleteListModalWindow window = Window();
                return window != null && window.Shown && window.IsReady;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The game's own: Exit hides the window.</summary>
        public override bool Back()
        {
            return false;
        }

        public override void Build(GraphBuilder builder)
        {
            HeroCompleteListModalWindow window = Window();
            GuiTable table = Table(window);
            if (table == null)
            {
                return;
            }

            builder.BeginStop(LinesStop);
            _table.Headers(builder, table);
            _table.Rows(builder, table, Title(window));

            _cells.Clear();
            Cells.AddControl(
                _cells,
                AgeWidgets.ChildNamed(window.AgeTransform, "CloseButton", 3),
                "hero-list:close"
            );
            if (_cells.Count > 0)
            {
                builder.BeginStop(ActionsStop);
                Cells.Emit(builder, _cells);
            }
        }

        // ---- reading the window ----

        /// <summary>The heading the window writes across its top ("Active Heroes in the galaxy"), found
        /// where it is drawn: the class exposes its table and nothing else.</summary>
        private static string Title(HeroCompleteListModalWindow window)
        {
            try
            {
                return window == null
                    ? null
                    : AgeWidgets.TextOf(
                        AgeWidgets.ChildNamed(window.AgeTransform, "TitleGroup", 3)
                    );
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The hero a row stands for. The wrapper the table binds is rebuilt on every refresh
        /// (<c>Refresh</c> :66-77), so it is the hero underneath it that identifies the row.</summary>
        private static Hero HeroOf(GuiTableLine line)
        {
            try
            {
                GuiHero wrapper = line == null ? null : line.Data as GuiHero;
                return wrapper == null ? null : wrapper.Hero;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What the row is called when the name column draws nothing - the hero's own name.
        /// </summary>
        private static string HeroName(GuiTableLine line)
        {
            try
            {
                GuiHero wrapper = line == null ? null : line.Data as GuiHero;
                return wrapper == null ? null : AgeText.Clean(wrapper.Title);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static GuiTable Table(HeroCompleteListModalWindow window)
        {
            try
            {
                return window == null ? null : window.GuiTable;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static HeroCompleteListModalWindow Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<HeroCompleteListModalWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
