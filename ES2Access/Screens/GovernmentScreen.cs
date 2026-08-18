using System;
using System.Collections.Generic;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// Choosing how the empire is governed: the window the senate's "Change Government" button opens.
    ///
    /// The shape is the shape it is drawn in: a heading across the top, the government in force on the
    /// left under its own caption, the ones that could replace it on the right under theirs, the
    /// empire's money, influence and approval along the bottom, and Cancel and Validate under those.
    ///
    /// Each of those bands is walked one member per step. The cards are peers of one kind and so are
    /// the three figures and the two buttons, so a sideways move buys nothing and where the layout box
    /// wrapped them is a rendering accident. The two captions are the bands' NAMES rather than nodes in
    /// them: neither carries anything on hover (measured), so there is nothing in them to review.
    ///
    /// The governments are RADIO buttons and Validate is a separate press, because that is the game's
    /// own model: a government item's toggle only makes it the selection
    /// (<c>GovernmentModalWindow.OnGovernmentSelectedCb</c> :367-377) and nothing changes until
    /// Validate posts the order (<c>OnValidateCb</c> :379-395). Validate stays declared while it
    /// refuses - the game writes why on it, from "pick one first" to whichever of the senate's own
    /// conditions is unmet - and hearing that is the point.
    ///
    /// A government card is a whole argument for choosing it: what approval it needs, what it costs,
    /// what it does to the empire, and which election actions it would allow. All of that is drawn on
    /// the card permanently rather than offered on a hover, so it is spoken with the card and walkable
    /// line by line in the review buffer.
    ///
    /// There is no screen name. The window's heading is a drawn element with its own explanation on its
    /// tooltip, so it is declared where it is drawn and focus lands on it - which says what has just
    /// opened, once, instead of saying it as a screen name and then again as a control.
    ///
    /// Escape is the game's: the window is an input handler of its own and answers it by closing, which
    /// is the route Cancel takes too.
    /// </summary>
    public sealed class GovernmentScreen : Screen
    {
        private static readonly object HeadingStop = "government:heading";
        private static readonly object CurrentStop = "government:current";
        private static readonly object ChoicesStop = "government:choices";
        private static readonly object ResourcesStop = "government:resources";
        private static readonly object ActionsStop = "government:actions";

        // Reused across builds rather than allocated per frame: Build runs every tick.
        private readonly List<Cell> _cells = new List<Cell>();

        public override string Key
        {
            get { return "screen.government"; }
        }

        /// <summary>Over the senate that opens it, and under the message box anything here could raise.
        /// </summary>
        public override int Layer
        {
            get { return 33; }
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
                GovernmentModalWindow window = Window();
                return window != null && window.Shown && window.IsReady;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Escape is the game's: the window closes itself, which is what Cancel does.</summary>
        public override bool Back()
        {
            return false;
        }

        public override void Build(GraphBuilder builder)
        {
            GovernmentModalWindow window = Window();
            if (window == null)
            {
                return;
            }

            try
            {
                BuildHeading(builder, window);
                BuildCurrent(builder, window);
                BuildChoices(builder, window);
                BuildResources(builder, window);
                BuildActions(builder, window);
            }
            catch (Exception e)
            {
                Log.Warn("government: reading the window threw: " + e);
            }
        }

        private void BuildHeading(GraphBuilder builder, GovernmentModalWindow window)
        {
            builder.BeginStop(HeadingStop);
            _cells.Clear();
            Cells.AddReadout(_cells, AgeWidgets.ChildNamed(window.AgeTransform, "TitleGroup", 2), "government:title");
            Cells.EmitLinear(builder, _cells);
        }

        /// <summary>The government in force, under the caption the window draws over it - which is one
        /// of two, because a government the game is hiding is announced as the NEXT one instead
        /// (<c>RefreshActiveGovernment</c> :272-290). Whichever is drawn is what is read.
        ///
        /// The caption is a bare word over the one card below it, with nothing on hover (measured: no
        /// tooltip on either label), so it names the band rather than standing in it: a level the
        /// announcer says on the way in, not a node to walk past.</summary>
        private void BuildCurrent(GraphBuilder builder, GovernmentModalWindow window)
        {
            builder.BeginStop(CurrentStop);
            bool named = Caption(
                builder,
                Shown(window.ActiveGovernmentTitle) ?? Shown(window.NextGovernmentTitle)
            );

            _cells.Clear();
            AddCard(_cells, window.ActiveGovernmentItem, "government:active");
            Cells.EmitLinear(builder, _cells);
            Unname(builder, named);
        }

        private void BuildChoices(GraphBuilder builder, GovernmentModalWindow window)
        {
            builder.BeginStop(ChoicesStop);
            bool named = Caption(
                builder,
                Shown(AgeWidgets.ChildNamed(window.GovernmentSelectionGroup, "SelectionTitleGroup", 2))
            );

            _cells.Clear();
            AgeTransform table = window.AvailableGovernmentsTable;
            IList<AgeTransform> items = table == null ? null : table.Children;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                AddCard(
                    _cells,
                    items[i] == null ? null : items[i].GetComponent<GovernmentItem>(),
                    "government:choice/" + i
                );
            }

            Cells.EmitLinear(builder, _cells);
            Unname(builder, named);
        }

        /// <summary>The caption the window draws over a band, as the band's own name. A caption the
        /// game left empty pushes nothing, so the band is never announced under a blank level.</summary>
        private static bool Caption(GraphBuilder builder, AgeTransform widget)
        {
            string text = widget == null ? null : AgeWidgets.TextOf(widget);
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            builder.PushContext(text);
            return true;
        }

        private static void Unname(GraphBuilder builder, bool named)
        {
            if (named)
            {
                builder.PopContext();
            }
        }

        private static AgeTransform Shown(AgeTransform widget)
        {
            return widget != null && AgeWidgets.Visible(widget) ? widget : null;
        }

        /// <summary>
        /// One government to choose from: its name, whether it is the selection, and everything the card
        /// argues with - the approval it asks for, the cost, its effects and its election actions.
        ///
        /// Activating it replays the card's own toggle, so the game makes the choice exclusive and
        /// re-reads the cost band the way it does for a mouse. The card in force is drawn the same way
        /// and disabled, which is what tells the player which one they already have.
        /// </summary>
        private void AddCard(List<Cell> cells, GovernmentItem item, string key)
        {
            AgeTransform widget = item == null ? null : item.AgeTransform;
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            GovernmentItem it = item;
            NodeVtable vtable = GraphNodes.Radio(
                () => AgeText.Label(it.TitleLabel),
                () => it.Toggle != null && it.Toggle.State,
                () => AgeWidgets.Toggle(it.Toggle),
                () => AgeWidgets.Operable(widget)
            );
            // Drawn on the card rather than offered on a hover, so it is announced outright the way a
            // control's own description is, and the review buffer holds it either way.
            vtable.Sections = GraphNodes.Sections(
                new NodeSection(() => CardLines(it), TooltipMode.Announce)
            );
            AgeWidgets.Point(vtable, it.Toggle);
            Cells.Add(cells, widget, ControlId.Referenced(widget, key), vtable);
        }

        /// <summary>
        /// Everything the card says under its name, a line at a time - the stats band, the effects and
        /// the election actions, in the order they are drawn.
        ///
        /// DRAWN words only. Reading the card's whole content the general way pulled in the sentences
        /// the game keeps on the little captions' tooltips as well, which turned every card into a
        /// paragraph of hover text nobody had hovered over - and hover text is the tooltip rule's, not
        /// the readout's.
        /// </summary>
        private static IList<string> CardLines(GovernmentItem item)
        {
            List<string> lines = new List<string>();
            try
            {
                AgeTransform title = item.TitleLabel == null ? null : item.TitleLabel.AgeTransform;
                AgeTransform content = item.ContentTable;
                IList<AgeTransform> bands = content == null ? null : content.Children;
                for (int i = 0; bands != null && i < bands.Count; i++)
                {
                    AgeTransform band = bands[i];
                    if (band != null && !Holds(band, title))
                    {
                        Lines(band, lines, 3);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("government: reading a government card threw: " + e);
            }

            return lines;
        }

        /// <summary>One line per drawn row, by the same rule the side panels are read with: a group
        /// whose children are all primitives is one thing drawn out of several pieces; a group holding
        /// other groups is a band of separate lines.</summary>
        private static void Lines(AgeTransform widget, List<string> into, int depth)
        {
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            IList<AgeTransform> children = widget.Children;
            bool band = false;
            for (int i = 0; depth > 0 && children != null && i < children.Count; i++)
            {
                AgeTransform child = children[i];
                IList<AgeTransform> grandchildren =
                    child == null || !AgeWidgets.Visible(child) ? null : child.Children;
                for (int j = 0; grandchildren != null && j < grandchildren.Count; j++)
                {
                    band = band || AgeWidgets.Visible(grandchildren[j]);
                }
            }

            if (band)
            {
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    Lines(children[i], into, depth - 1);
                }

                return;
            }

            // A group of pieces all on ONE drawn row is one thing drawn out of them ("Cost" beside
            // "100 Influence"); a group whose pieces are stacked is a list, and each row of it is a
            // line. Without that split a government's whole effects table arrived as one sentence.
            List<List<AgeTransform>> rows = Drawn(children);
            if (rows.Count > 1)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    Add(into, Joined(rows[i]));
                }

                return;
            }

            Add(into, AgeWidgets.TextOf(widget));
        }

        private static List<List<AgeTransform>> Drawn(IList<AgeTransform> children)
        {
            List<AgeTransform> visible = new List<AgeTransform>();
            for (int i = 0; children != null && i < children.Count; i++)
            {
                if (children[i] != null && AgeWidgets.Visible(children[i]))
                {
                    visible.Add(children[i]);
                }
            }

            return AgeLayout.Rows(visible, Self);
        }

        private static readonly Func<AgeTransform, AgeTransform> Self = widget => widget;

        private static string Joined(List<AgeTransform> row)
        {
            Core.Speech.MessageBuilder message = new Core.Speech.MessageBuilder();
            for (int i = 0; i < row.Count; i++)
            {
                message.Fragment(AgeWidgets.TextOf(row[i]));
            }

            return message.Build();
        }

        private static void Add(List<string> into, string line)
        {
            if (!string.IsNullOrEmpty(line))
            {
                into.Add(line);
            }
        }

        private static bool Holds(AgeTransform band, AgeTransform label)
        {
            if (label == null)
            {
                return false;
            }

            for (AgeTransform at = label; at != null; at = at.Parent)
            {
                if (ReferenceEquals(at, band))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>What the empire has to spend and how content it is, along the bottom - and, once a
        /// government is chosen, how long the anarchy between the two would last. The two money boxes
        /// answer a click only in the developers' god mode, so they are readouts.</summary>
        private void BuildResources(GraphBuilder builder, GovernmentModalWindow window)
        {
            builder.BeginStop(ResourcesStop);
            _cells.Clear();
            AddTotal(_cells, Parent(window.EmpireMoneyLabel), "government:money");
            AddTotal(_cells, Parent(window.EmpirePointLabel), "government:influence");
            AddTotal(_cells, window.EmpireHappinessGroup, "government:approval");
            Cells.AddReadout(_cells, window.AnarchyDurationGroup, "government:anarchy");
            Cells.EmitLinear(builder, _cells);
        }

        /// <summary>One of the empire's running totals along the bottom. The game writes the number and
        /// draws the symbol that says what it counts, so the name comes from the wrapper it hangs on the
        /// box's own tooltip - which is where the game keeps the word it did not write.</summary>
        private static void AddTotal(List<Cell> cells, AgeTransform widget, string key)
        {
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeTransform at = widget;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeWidgets.TooltipTitle(AgeWidgets.Raw(at))),
                    GraphNodes.ValuePart(() => AgeWidgets.TextOf(at)),
                },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            AgeWidgets.PointAt(vtable, widget);
            Cells.Add(cells, widget, ControlId.Referenced(widget, key), vtable);
        }

        private void BuildActions(GraphBuilder builder, GovernmentModalWindow window)
        {
            builder.BeginStop(ActionsStop);
            _cells.Clear();
            AgeTransform validate = window.ValidateButton;
            AgeTransform band = validate == null ? null : validate.Parent;
            IList<AgeTransform> children = band == null ? null : band.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                Cells.AddControl(_cells, children[i], "government:button/" + i);
            }

            Cells.EmitLinear(builder, _cells);
        }

        private static AgeTransform Parent(AgePrimitiveLabel label)
        {
            try
            {
                return label == null || label.AgeTransform == null
                    ? null
                    : label.AgeTransform.Parent;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static GovernmentModalWindow Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<GovernmentModalWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
