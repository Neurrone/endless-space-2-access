using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
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
            get { return ModStrings.ScreenGovernment; }
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

        /// <summary>The heading, with the sentence the window explains itself by - which the prefab
        /// hangs on the label inside the title group rather than on the group, so the reading has to
        /// descend into it or the sentence reaches nobody.</summary>
        private void BuildHeading(GraphBuilder builder, GovernmentModalWindow window)
        {
            builder.BeginStop(HeadingStop);
            _cells.Clear();
            AgeTransform title = AgeWidgets.ChildNamed(window.AgeTransform, "TitleGroup", 2);
            // Read off the pieces the renderer is DRAWING, and gated on the same reading: the group is
            // reached by name rather than through a field, so nothing here vouches for what is inside it,
            // and a heading whose only words sat on a faded-out piece would be a sentence about a window
            // that has moved on.
            if (title != null && !string.IsNullOrEmpty(AgeWidgets.PaintedPartsText(title)))
            {
                _cells.Add(Cells.PaintedReadout(title, "government:title"));
            }

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
            bool named = Captions.Push(
                builder,
                Shown(window.ActiveGovernmentTitle) ?? Shown(window.NextGovernmentTitle),
                "government:current/title"
            );

            _cells.Clear();
            AddCard(_cells, window.ActiveGovernmentItem, "government:active");
            Cells.EmitLinear(builder, _cells);
            Captions.Pop(builder, named);
        }

        private void BuildChoices(GraphBuilder builder, GovernmentModalWindow window)
        {
            builder.BeginStop(ChoicesStop);
            bool named = Captions.Push(
                builder,
                Shown(
                    AgeWidgets.ChildNamed(window.GovernmentSelectionGroup, "SelectionTitleGroup", 2)
                ),
                "government:choices/title"
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
            Captions.Pop(builder, named);
        }

        /// <summary>Which of the two title widgets the window is DRAWING - the caption a context is
        /// named by, not a node.</summary>
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
            if (widget == null)
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
                NodeSection.Composed(() => CardLines(it))
            );
            AgeWidgets.Point(vtable, it.Toggle);
            Cells.Add(cells, widget, ControlId.For(widget, key), vtable);

            // The card draws its figures under little captions - the approval it asks for, what it
            // costs, what election actions it would allow - and each of those captions carries a
            // sentence saying what that figure MEANS. Folding them into the card's own reading turned
            // every card into a paragraph of hover text (see CardLines), and a row of several
            // explanations is exactly what one merged node cannot serve, so they are nodes of their
            // own. Read off the card's content rather than by prefab name: the same three captions and
            // the "no election actions" line are drawn by the same table on every card.
            List<TooltipChildren.Dossier> captions = new List<TooltipChildren.Dossier>(4);
            TooltipChildren.AddPlainInside(captions, item.ContentTable);
            if (captions.Count > 0)
            {
                Cell owner = cells[cells.Count - 1];
                owner.Dossiers = captions;
                owner.Key = key;
            }
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
                    if (band != null && !AgeWidgets.Under(title, band))
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
            // Content: this walk produces LINES of a card's reading, never nodes - and it descends, so
            // a hidden band whose children each read visible would be read out of it.
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            IList<AgeTransform> children = widget.Children;
            bool band = false;
            for (int i = 0; depth > 0 && children != null && i < children.Count; i++)
            {
                AgeTransform child = children[i];
                // Flow control: whether anything under here is drawn at all, which is what decides the band is a band.
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
                // Which children the layout bands into rows - geometry feeding a content reading.
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

        /// <summary>What the empire has to spend and how content it is, along the bottom - and, once a
        /// government is chosen, how long the anarchy between the two would last. The two money boxes
        /// answer a click only in the developers' god mode, so they are readouts.</summary>
        private void BuildResources(GraphBuilder builder, GovernmentModalWindow window)
        {
            builder.BeginStop(ResourcesStop);
            _cells.Clear();
            AgeTransform money = AgeWidgets.Transform(window.EmpireMoneyLabel);
            AgeTransform influence = AgeWidgets.Transform(window.EmpirePointLabel);
            AddTotal(_cells, money == null ? null : money.Parent, "government:money");
            AddTotal(_cells, influence == null ? null : influence.Parent, "government:influence");
            AddTotal(_cells, window.EmpireHappinessGroup, "government:approval");
            Cells.AddReadout(_cells, window.AnarchyDurationGroup, "government:anarchy");
            Cells.EmitLinear(builder, _cells);
        }

        /// <summary>One of the empire's running totals along the bottom. The game writes the number and
        /// draws the symbol that says what it counts, so the name comes from the wrapper it hangs on the
        /// box's own tooltip - which is where the game keeps the word it did not write.</summary>
        private static void AddTotal(List<Cell> cells, AgeTransform widget, string key)
        {
            if (widget == null)
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
            Cells.Add(cells, widget, ControlId.For(widget, key), vtable);
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

        private static GovernmentModalWindow Window()
        {
            return GameWindows.Of<GovernmentModalWindow>();
        }
    }
}
