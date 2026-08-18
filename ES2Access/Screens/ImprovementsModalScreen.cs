using System;
using System.Collections.Generic;
using ES2Access.Core.UI.Graph;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// What has been BUILT on a system, and the one thing the player can do about it: the modal the
    /// colony panel's "System improvements" button opens over the star system page.
    ///
    /// It is the game's own window, not a page of ours, so it is polled rather than pushed: it comes up
    /// while the star system screen stands down for it (any modal being visible is already that
    /// screen's answer to "am I covered"), and it goes away when the game hides it.
    ///
    /// The shape is the shape it is drawn in. A heading band across the top - the window's title and
    /// the system's total upkeep - then a grid of tiles, one per improvement, then the button row along
    /// the bottom. The tiles are the game's own CHECKBOXES: this window does not scrap an improvement
    /// where you stand, it collects a selection and scraps the lot when Scrap is pressed, and the game
    /// asks for confirmation before it does. So Enter on a tile ticks it and nothing else - there is no
    /// action menu here, because the game's own model is select-then-act and copying it is what keeps
    /// Enter a key nobody regrets. Scrap is a button like any other, and the confirmation it raises is
    /// the mod's message-box screen, which already speaks.
    ///
    /// A tile's name is read in FULL. The game draws it into a 96-pixel box and ellipsizes it - "Colony
    /// Ba." - and shows the whole name on the tile's tooltip instead, which is where a sighted player
    /// reads it; see <see cref="AgeText.FullLabel"/>.
    ///
    /// There is no screen name. The window's heading is a drawn element with its own explanation on
    /// its tooltip, so it is declared where it is drawn and focus lands on it - which says what has
    /// just opened, once, instead of saying it as a screen name and then again as a control.
    /// </summary>
    public sealed class ImprovementsModalScreen : Screen
    {
        private static readonly object SummaryStop = "improvements:summary";
        private static readonly object ListStop = "improvements:list";
        private static readonly object ActionsStop = "improvements:actions";

        /// <summary>Reused across builds rather than allocated per frame: Build runs every tick.
        /// </summary>
        private readonly List<Cell> _cells = new List<Cell>();
        private readonly List<AgeTransform> _tiles = new List<AgeTransform>();

        public override string Key
        {
            get { return "screen.improvements"; }
        }

        /// <summary>Over the star system page it is opened from and everything that page can have up,
        /// and under the message box - which is this window's own confirmation.</summary>
        public override int Layer
        {
            get { return 85; }
        }

        /// <summary>The heading band, because it is drawn first and Tab does not wrap.</summary>
        public override object InitialFocusStop
        {
            get { return SummaryStop; }
        }

        /// <summary>Set once the window has finished arriving, and cleared when the game unbinds it.
        /// Instance state, so a hot reload starts it over rather than inheriting a stale answer.
        /// </summary>
        private bool _arrived;

        /// <summary>
        /// Arriving and leaving are different questions here. We arrive when the window is shown and
        /// ready; we leave when the game UNBINDS it - it drops the system it was opened for as the last
        /// thing it does, and not before.
        ///
        /// The gap between the two matters: the window stops reporting itself shown when it starts
        /// fading out, but the page behind it is only re-enabled when the fade finishes. A screen that
        /// left at the first of those hands the player back to a page whose every control is still
        /// disabled, and the control the cursor returns to announces itself unavailable for a moment
        /// (measured: "System improvements, button, unavailable" on closing this window).
        /// </summary>
        public override bool IsActive()
        {
            ImprovementsManagementModalWindow window = Window();
            try
            {
                if (window == null || window.ColonizedStarSystem == null)
                {
                    _arrived = false;
                    return false;
                }

                if (!_arrived)
                {
                    _arrived = window.Shown && window.IsReady;
                }

                return _arrived;
            }
            catch (Exception)
            {
                _arrived = false;
                return false;
            }
        }

        /// <summary>The other side of the gap <see cref="IsActive"/> deliberately spans: while the window
        /// is fading out - or greyed under the confirmation Scrap raises - the engine has switched the
        /// whole stack above it off, so every control here reads unavailable at once. Nothing on the page
        /// changed; the page did.</summary>
        public override bool IsWorkable
        {
            get
            {
                ImprovementsManagementModalWindow window = Window();
                return window != null
                    && window.Shown
                    && AgeWidgets.Operable(window.AgeTransform);
            }
        }

        /// <summary>Escape is the game's: the window is an input handler of its own and answers the key
        /// by closing itself, which is the same route its Close button takes.</summary>
        public override bool Back()
        {
            return false;
        }

        public override void Build(GraphBuilder builder)
        {
            ImprovementsManagementModalWindow window = Window();
            if (window == null)
            {
                return;
            }

            builder.BeginStop(SummaryStop);
            BuildSummary(builder, window);

            builder.BeginStop(ListStop);
            BuildImprovements(builder, window);

            builder.BeginStop(ActionsStop);
            BuildActions(builder, window);
        }

        // ---- the heading band ----

        private void BuildSummary(GraphBuilder builder, ImprovementsManagementModalWindow window)
        {
            AgeTransform upkeep =
                window.UpkeepLabel == null ? null : window.UpkeepLabel.AgeTransform;
            _cells.Clear();
            AddReadout(_cells, HeadingBeside(upkeep), "improvements:title");
            AddReadout(_cells, upkeep, "improvements:upkeep");
            Cells.EmitLinear(builder, _cells);
        }

        /// <summary>The window's title, found as the other label in the band the upkeep line is drawn
        /// in - the window exposes the upkeep label and not the title, and the two are siblings.
        /// </summary>
        private static AgeTransform HeadingBeside(AgeTransform upkeep)
        {
            try
            {
                AgeTransform band = upkeep == null ? null : upkeep.Parent;
                IList<AgeTransform> children = band == null ? null : band.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    AgeTransform child = children[i];
                    if (
                        child != null
                        && !ReferenceEquals(child, upkeep)
                        && child.GetComponent<AgePrimitiveLabel>() != null
                    )
                    {
                        return child;
                    }
                }
            }
            catch (Exception) { }

            return null;
        }

        // ---- the improvements ----

        /// <summary>
        /// One tile per improvement, one per row in the order the grid draws them: the tiles are peers
        /// of one kind and the line an engine wrapped them onto is a fact about the box, not about the
        /// improvements. Nothing is declared for a system with none: the game draws an empty area there,
        /// with no caption and no placeholder, and a stop with nothing in it does not exist.
        ///
        /// An improvement the game will not let go of - a colony's own base, an empire's capital - has
        /// its tile disabled, so it reads as unavailable and Enter does nothing. It stays on the grid
        /// because it is on the grid, and because what it costs in upkeep is half of what the window
        /// is for.
        /// </summary>
        private void BuildImprovements(
            GraphBuilder builder,
            ImprovementsManagementModalWindow window
        )
        {
            AgeTransform table = window.ImprovementsTable;
            _tiles.Clear();
            try
            {
                IList<AgeTransform> children = table == null ? null : table.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    AgeTransform tile = children[i];
                    if (tile != null && AgeWidgets.Visible(tile) && Item(tile) != null)
                    {
                        _tiles.Add(tile);
                    }
                }
            }
            catch (Exception) { }

            _cells.Clear();
            for (int i = 0; i < _tiles.Count; i++)
            {
                AddTile(_cells, _tiles[i], i);
            }

            Cells.EmitLinear(builder, _cells);
        }

        /// <summary>The tile's place in the grid is its structural key, not its widget name: the game
        /// hands two tiles the same name while it is rebuilding the grid, and two controls with one
        /// identity is a graph the builder refuses to declare (measured: 1940 consecutive
        /// "Duplicate control id" throws out of Build). The reference beside it is what carries the
        /// cursor when an improvement moves to another square.</summary>
        private static void AddTile(List<Cell> cells, AgeTransform tile, int index)
        {
            ImprovementItem item = Item(tile);
            AgeControlToggle toggle = item.Toggle;
            AgePrimitiveLabel title = item.Title;
            AgePrimitiveLabel upkeep = item.Upkeep;
            AgeTransform widget = tile;
            AgeTooltip tooltip = AgeWidgets.Raw(tile);

            NodeVtable vtable = GraphNodes.Checkbox(
                () => AgeText.FullLabel(title),
                () => toggle != null && toggle.State,
                () => AgeWidgets.Toggle(toggle),
                () => AgeWidgets.Operable(widget),
                tooltip
            );
            // What the tile draws over its picture, and the number the whole window is about.
            vtable.Announcements.Add(GraphNodes.ValuePart(() => AgeText.Label(upkeep)));
            AgeWidgets.PointAt(vtable, tile);
            Add(cells, tile, ControlId.Referenced(tile, "improvements:tile/" + index), vtable);
        }

        private static ImprovementItem Item(AgeTransform tile)
        {
            try
            {
                return tile.GetComponent<ImprovementItem>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---- the bottom band ----

        /// <summary>The buttons as the window drew them, discovered from the group they share rather
        /// than named: the window exposes Scrap as a field and Close only as its sibling, and taking
        /// both from the group keeps them in the order they are drawn in. The hero the system has
        /// assigned, whose upkeep is counted in the total above, is drawn in this same band.</summary>
        private void BuildActions(GraphBuilder builder, ImprovementsManagementModalWindow window)
        {
            _cells.Clear();

            AgeTransform hero = window.HeroGroup;
            if (hero != null && AgeWidgets.Visible(hero))
            {
                AgeTooltip tooltip = window.HeroTooltip;
                AddReadout(_cells, hero, "improvements:hero", tooltip);
            }

            try
            {
                AgeTransform scrap =
                    window.ScrapButton == null ? null : window.ScrapButton.AgeTransform;
                AgeTransform group = scrap == null ? null : scrap.Parent;
                IList<AgeTransform> children = group == null ? null : group.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    AddButton(_cells, children[i]);
                }
            }
            catch (Exception) { }

            Cells.EmitLinear(builder, _cells);
        }

        private static void AddButton(List<Cell> cells, AgeTransform widget)
        {
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeControlButton button = AgeWidgets.Button(widget);
            if (button == null)
            {
                return;
            }

            AgeTransform it = widget;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            NodeVtable vtable = GraphNodes.Button(
                () => AgeWidgets.TextOf(it),
                () => AgeWidgets.Press(it),
                () => AgeWidgets.Operable(it),
                tooltip
            );
            AgeWidgets.Point(vtable, button);
            Add(
                cells,
                widget,
                ControlId.Referenced(widget, "improvements:button/" + Name(widget)),
                vtable
            );
        }

        /// <summary>The widget's own name, which is what makes one button's structural key differ from
        /// the next one's - the bottom band is a fixed set of named buttons, unlike the grid above it.
        /// </summary>
        private static string Name(AgeTransform widget)
        {
            try
            {
                return widget.name;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        // ---- shared ----

        private static void Add(
            List<Cell> cells,
            AgeTransform widget,
            ControlId id,
            NodeVtable vtable
        )
        {
            cells.Add(new Cell { Widget = widget, Id = id, Vtable = vtable });
        }

        private static void AddReadout(
            List<Cell> cells,
            AgeTransform widget,
            string key,
            AgeTooltip tooltip = null
        )
        {
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeTransform it = widget;
            AgeTooltip its = tooltip == null ? AgeWidgets.Raw(widget) : tooltip;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeWidgets.TextOf(it)),
                },
                Sections = GraphNodes.Sections(null, its),
            };

            AgeWidgets.PointAt(vtable, widget);
            Add(cells, widget, ControlId.Referenced(widget, key), vtable);
        }

        private static ImprovementsManagementModalWindow Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<ImprovementsManagementModalWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
