using System;
using System.Collections.Generic;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// The laws window: what the senate's "Pass Laws" button opens, and what an empty law slot opens
    /// when it is pressed.
    ///
    /// Four bands, in the order they are drawn: the heading, with how many slots are left and what the
    /// empire has to spend on them; the filter strip; the grid of law cards the filter matches; and the
    /// pane that writes the selected law out in full, with the button that would enact or repeal it.
    ///
    /// The filters switch instantly - a mouse click on one rebuilds the grid there and then - so they
    /// are radios that do their job on Enter, not a selection waiting for a confirmation. The CARDS are
    /// the other way round, and that is the game's model rather than a choice of the mod's: a card's
    /// toggle only makes it the selection (<c>LawsManagementModalWindow.BindLawCard</c> :424-434 and
    /// <c>RefreshSelectedLawDetails</c> :286-338), and it is Pass or Abolish underneath that acts. Both
    /// stay declared while they refuse, carrying the game's own reasons - not enough influence, not
    /// enough political experience, no slot left.
    ///
    /// The detail pane is permanent drawn text, not a hover: the law's long title, the short title the
    /// card carries, the paragraph explaining it, its effects, its upkeep, the political experience it
    /// needs and what it costs. All of it is read as the pane's own lines, and the paragraph is walkable
    /// line by line in the review buffer.
    ///
    /// There is no screen name: the window's heading is declared where it is drawn and focus lands on
    /// it, which says what has just opened, once.
    /// </summary>
    public sealed class LawsScreen : Screen
    {
        private static readonly object HeadingStop = "laws:heading";
        private static readonly object FiltersStop = "laws:filters";
        private static readonly object CardsStop = "laws:cards";
        private static readonly object DetailStop = "laws:detail";
        private static readonly object ActionsStop = "laws:actions";

        /// <summary>Shared by the card rows, so up and down across the grid keep the column.</summary>
        private static readonly object CardRowKey = "laws:card-row";

        // Reused across builds rather than allocated per frame: Build runs every tick.
        private readonly List<Cell> _cells = new List<Cell>();

        public override string Key
        {
            get { return "screen.laws"; }
        }

        /// <summary>Over the senate that opens it, above the government window it is never up with, and
        /// under the message box anything here could raise.</summary>
        public override int Layer
        {
            get { return 34; }
        }

        /// <summary>The heading, because it is drawn first and Tab does not wrap.</summary>
        public override object InitialFocusStop
        {
            get { return HeadingStop; }
        }

        public override bool IsActive()
        {
            try
            {
                LawsManagementModalWindow window = Window();
                return window != null && window.Shown && window.IsReady;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Escape is the game's: the window closes itself, which is what Close does.</summary>
        public override bool Back()
        {
            return false;
        }

        public override void Build(GraphBuilder builder)
        {
            LawsManagementModalWindow window = Window();
            if (window == null)
            {
                return;
            }

            try
            {
                BuildHeading(builder, window);
                BuildFilters(builder, window);
                BuildCards(builder, window);
                BuildDetail(builder, window);
                BuildActions(builder, window);
            }
            catch (Exception e)
            {
                Log.Warn("laws: reading the window threw: " + e);
            }
        }

        /// <summary>The window's own heading, and the two numbers the game draws on the same line as
        /// the filters - how many law slots are still free, and the empire's influence.</summary>
        private void BuildHeading(GraphBuilder builder, LawsManagementModalWindow window)
        {
            builder.BeginStop(HeadingStop);
            _cells.Clear();
            Cells.AddReadout(
                _cells,
                AgeWidgets.ChildNamed(window.AgeTransform, "Title", 3),
                "laws:title"
            );
            Cells.AddReadout(_cells, Widget(window.VotedLawSlotsLabel), "laws:slots-left");
            Cells.AddReadout(_cells, Widget(window.CurrentPrestigeLabel), "laws:influence");
            Cells.Emit(builder, _cells);
        }

        /// <summary>Which laws the grid shows: the ones that could be passed now, one filter per party
        /// in the senate, and all of them. Switching rebuilds the grid at once, so Enter does it.
        /// </summary>
        private void BuildFilters(GraphBuilder builder, LawsManagementModalWindow window)
        {
            builder.BeginStop(FiltersStop);
            _cells.Clear();
            AgeTransform table = window.LawFiltersTable;
            IList<AgeTransform> children = table == null ? null : table.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AddFilter(_cells, children[i], i);
            }

            Cells.Emit(builder, _cells);
        }

        private static void AddFilter(List<Cell> cells, AgeTransform widget, int index)
        {
            LawFilter filter = widget == null ? null : widget.GetComponent<LawFilter>();
            if (filter == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            LawFilter it = filter;
            NodeVtable vtable = GraphNodes.Tab(
                () => AgeText.Label(it.TitleLabel),
                () => it.Toggle != null && it.Toggle.State,
                () => AgeWidgets.Operable(widget),
                filter.Tooltip
            );
            vtable.OnActivate = () => AgeWidgets.Toggle(it.Toggle);
            AgeWidgets.Point(vtable, filter.Toggle);
            Cells.Add(cells, widget, ControlId.Referenced(widget, "laws:filter/" + index), vtable);
        }

        private void BuildCards(GraphBuilder builder, LawsManagementModalWindow window)
        {
            builder.BeginStop(CardsStop);
            _cells.Clear();
            LawCards.Cards(_cells, window.LawCardsTable, "laws:card/");
            Emit(builder, _cells, CardRowKey);
        }

        /// <summary>
        /// Everything the window writes about the law under the cursor's selection, in the order it is
        /// drawn: the long title, the short one, the paragraph, the effects, the political experience it
        /// asks for and the upkeep it would add, then what it costs and the button that would enact it.
        ///
        /// The pane is not drawn at all until something is selected (<c>LawDetails.Visible</c>), and a
        /// stop with nothing in it does not exist that frame.
        /// </summary>
        private void BuildDetail(GraphBuilder builder, LawsManagementModalWindow window)
        {
            AgeTransform pane = window.LawDetails;
            if (pane == null || !AgeWidgets.Visible(pane))
            {
                return;
            }

            builder.BeginStop(DetailStop);
            _cells.Clear();
            Cells.AddReadout(_cells, Widget(window.LawTitle), "laws:law-title");
            Cells.AddReadout(_cells, Widget(window.LawShortTitle), "laws:law-short-title");
            AddDescription(_cells, window);
            Cells.Emit(builder, _cells);

            _cells.Clear();
            AddEffects(_cells, window.PanelFeatureEffects);
            Cells.Emit(builder, _cells);

            // The upkeep total and the cost are drawn INSIDE the two blocks above them - the total in
            // the upkeep block, the cost in the button it is the price of - so they are read as part of
            // those and never declared a second time.
            _cells.Clear();
            Cells.AddReadout(_cells, Widget(window.PanelFeatureExperience), "laws:experience");
            Cells.AddReadout(_cells, Widget(window.PanelFeatureLawUpkeep), "laws:upkeep");
            AddAction(_cells, window.VoteButton, "laws:vote");
            AddAction(_cells, window.AbrogateButton, "laws:abolish");
            Cells.Emit(builder, _cells);
        }

        /// <summary>
        /// Pass or Abolish. The game draws the price INSIDE the button, above the word on it, so the
        /// button is read as its own word and then its price rather than as both run together
        /// ("Cost: 15 Influence Pass").
        ///
        /// Neither is pressed by anything but the player: each posts an order that changes the empire's
        /// laws. Both stay declared while they refuse, with the game's own reasons - and those reasons
        /// are the whole value of a button that will not go.
        /// </summary>
        private static void AddAction(List<Cell> cells, AgeControlButton button, string key)
        {
            AgeTransform widget = AgeWidgets.Transform(button);
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeTransform word = AgeWidgets.ChildNamed(widget, "ButtonContainer", 1) ?? widget;
            AgeTransform price = AgeWidgets.ChildNamed(widget, "CostContainer", 1);
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            AgeControlButton it = button;
            NodeVtable vtable = GraphNodes.Button(
                () => AgeWidgets.TextOf(word),
                () => AgeWidgets.Press(it),
                () => AgeWidgets.Operable(widget),
                tooltip
            );
            if (price != null)
            {
                vtable.Announcements.Add(GraphNodes.ValuePart(() => AgeWidgets.TextOf(price)));
            }

            AgeWidgets.Point(vtable, button);
            Cells.Add(cells, widget, ControlId.Referenced(widget, key), vtable);
        }

        /// <summary>The law's own paragraph. It is permanently drawn, so it is spoken in full, and its
        /// own lines are in the review buffer to walk.</summary>
        private static void AddDescription(List<Cell> cells, LawsManagementModalWindow window)
        {
            AgePrimitiveLabel label = window.LawDescription;
            AgeTransform widget = Widget(label);
            if (widget == null)
            {
                return;
            }

            AgePrimitiveLabel it = label;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeText.FullLabel(it)),
                },
                Sections = GraphNodes.Sections(
                    new NodeSection(() => AgeText.Lines(AgeText.FullLabel(it)), TooltipMode.None)
                ),
            };
            AgeWidgets.PointAt(vtable, widget);
            Cells.Add(cells, widget, ControlId.Referenced(widget, "laws:description"), vtable);
        }

        /// <summary>The block of effect lines under its caption - one line each, because each is a
        /// separate sentence the game wrote about a separate effect.</summary>
        private static void AddEffects(List<Cell> cells, PanelFeatureEffects effects)
        {
            AgeTransform group = effects == null ? null : effects.AgeTransform;
            if (group == null || !AgeWidgets.Visible(group))
            {
                return;
            }

            AgeTransform caption =
                effects.TitleLabel == null ? null : effects.TitleLabel.AgeTransform;
            Cells.AddReadout(cells, caption, "laws:effects-caption");

            IList<AgeTransform> bands = group.Children;
            for (int i = 0; bands != null && i < bands.Count; i++)
            {
                AgeTransform band = bands[i];
                if (band == null || ReferenceEquals(band, caption) || !AgeWidgets.Visible(band))
                {
                    continue;
                }

                IList<AgeTransform> lines = band.Children;
                for (int j = 0; lines != null && j < lines.Count; j++)
                {
                    Cells.AddReadout(cells, lines[j], "laws:effect/" + i + "/" + j);
                }
            }
        }

        /// <summary>The window's own exit, which the game draws in the corner well away from
        /// everything else.</summary>
        private void BuildActions(GraphBuilder builder, LawsManagementModalWindow window)
        {
            _cells.Clear();
            Cells.AddControl(
                _cells,
                AgeWidgets.ChildNamed(window.AgeTransform, "CloseButton", 2),
                "laws:close"
            );
            if (_cells.Count > 0)
            {
                builder.BeginStop(ActionsStop);
                Cells.Emit(builder, _cells);
            }
        }

        private static AgeTransform Widget(AgePrimitiveLabel label)
        {
            try
            {
                return label == null ? null : label.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static AgeTransform Widget(GuiPanelFeature feature)
        {
            try
            {
                return feature == null ? null : feature.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static AgeTransform Widget(AgeControlButton button)
        {
            return AgeWidgets.Transform(button);
        }

        private static void Emit(GraphBuilder builder, List<Cell> cells, object rowKey)
        {
            foreach (List<Cell> row in AgeLayout.Rows(cells, CellWidget))
            {
                builder.StartRow(rowKey);
                foreach (Cell cell in row)
                {
                    builder.AddItem(cell.Id, cell.Vtable);
                }

                builder.EndRow();
            }
        }

        private static readonly Func<Cell, AgeTransform> CellWidget = cell => cell.Widget;

        private static LawsManagementModalWindow Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<LawsManagementModalWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
