using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
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
            _table.Decorate = HeroRow;
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
            AddTitle(builder, window);
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
                Cells.EmitLinear(builder, _cells);
            }
        }

        /// <summary>
        /// The heading, as a row above the table's first line.
        ///
        /// It is already the table's NAME - the region the sheet announces on the way in - so it takes
        /// a row only for what a name cannot carry: the sentence the game hung on the label it writes
        /// the heading on, which nothing else on this window reaches (<see cref="Captions"/>).
        /// </summary>
        private static void AddTitle(GraphBuilder builder, HeroCompleteListModalWindow window)
        {
            AgeTransform label = AgeWidgets.ChildNamed(
                window == null ? null : window.AgeTransform,
                "TitleLabel",
                4
            );
            Captions.Row(builder, label, "hero-list:title");
        }

        /// <summary>
        /// The dossier about the hero a row stands for, which the game hangs on the row's FIRST column
        /// - the one drawing the hero - and not on the row.
        ///
        /// Every other column's dossier is read where that column is, but the first column is the row
        /// itself, and the row carries only its own tooltip: this window's whole page about a hero -
        /// their class, what they are good at - reached no keyboard at all. It rides with the row and
        /// the row points at it, because a dossier the renderer assembles has no words until the game
        /// draws it.
        /// </summary>
        private static void HeroRow(GuiTableLine line, NodeVtable vtable)
        {
            AgeTransform cell = FirstColumn(line);
            List<AgeTooltip> found = new List<AgeTooltip>(2);
            AgeWidgets.EffectiveTooltips(
                cell,
                found,
                TooltipReach.Own | TooltipReach.Descendants,
                3
            );
            AgeTooltip dossier = found.Count == 0 ? null : found[found.Count - 1];
            NodeSection section = GraphNodes.TooltipSection(dossier);
            if (section == null || AgeWidgets.SameTooltip(line.Tooltip, dossier))
            {
                return;
            }

            // The dossier REPLACES whatever tooltip the generic cell reading found, rather than joining
            // it: the pointer moves to the dossier on the next line, and a tooltip a node no longer
            // points at is a buffer promise the game will never draw. What the cell DRAWS is kept.
            vtable.Sections = GraphNodes.OnlyTooltip(vtable.Sections, dossier);
            AgeWidgets.PointAt(vtable, cell, dossier);
        }

        /// <summary>The column the row's name is drawn in, which is the row itself.</summary>
        private static AgeTransform FirstColumn(GuiTableLine line)
        {
            try
            {
                IList<AgeTransform> cells =
                    line == null || line.CellsTable == null ? null : line.CellsTable.Children;
                return cells == null || cells.Count == 0 ? null : cells[0];
            }
            catch (Exception)
            {
                return null;
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
