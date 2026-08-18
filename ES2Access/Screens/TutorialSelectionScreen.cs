using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// The tutorial picker the game raises over the new game screen the first time it is opened
    /// (<c>NewGameScreen.OnBeginShow</c> :375-378, gated on <c>TutorialManager.IsPlayingForTheFirstTime</c>),
    /// and the first thing a new player meets. Silent, it is a dead end: three cards, no keyboard, and
    /// the game will not go on until one of them is picked or the box is refused.
    ///
    /// The game's own model is SELECT then CONFIRM, and it is kept. The three cards are
    /// <c>AgeControlToggle</c>s whose switch handlers make one of them the choice and clear the other
    /// two (<c>TutorialSelectionModalWindow.OnToggleBeginnerCb</c> :101-112 and its two siblings), and
    /// the Confirm button stays disabled until one is chosen (<c>Refresh</c> :60-67). So Enter on a card
    /// only picks it - nothing starts a tutorial until Confirm is pressed, which is
    /// <c>OnValidateCb</c> :167-170 -> <c>Validate</c> :69-87, the one path that loads the beginner save
    /// or writes the lobby setting and hides the window. That is exactly the shape a mod must not
    /// improve on: a key that started a tutorial from the card would be a key a player regrets.
    ///
    /// Escape is the game's. The window is an input handler of its own and answers Exit by cancelling
    /// (:32-40 -> <c>OnCancelCb</c> :161-165), which sets the tutorial mode to none and hides the box -
    /// leaving the player on the new game screen underneath, not back at the main menu. "No, thanks"
    /// takes the same route: the prefab wires it to <c>OnCancelCb</c> (measured).
    ///
    /// The descriptions the cards carry are the whole basis for choosing, and the game draws them on the
    /// cards rather than hiding them behind a hover, so they are spoken with the card and also walkable
    /// line by line in the review buffer.
    /// </summary>
    public sealed class TutorialSelectionScreen : Screen
    {
        private static readonly object ChoicesStop = "tutorial-selection:choices";
        private static readonly object ActionsStop = "tutorial-selection:actions";

        /// <summary>Topmost first - what the player reads first is what the card is called.</summary>
        private static readonly Comparison<AgePrimitiveLabel> Drawn = CompareByTop;

        // Reused across builds rather than allocated per frame: Build runs every tick.
        private readonly List<Cell> _cells = new List<Cell>();
        private readonly List<AgePrimitiveLabel> _labels = new List<AgePrimitiveLabel>();

        public override string Key
        {
            get { return "screen.tutorial-selection"; }
        }

        /// <summary>Over the new game screen it is drawn on. Nothing of ours is under it yet and only
        /// the message box can come over it, which is where the number leaves room.</summary>
        public override int Layer
        {
            get { return 90; }
        }

        /// <summary>The cards, because they are drawn first and Tab does not wrap.</summary>
        public override object InitialFocusStop
        {
            get { return ChoicesStop; }
        }

        /// <summary>The question the window asks, in the game's own words - "Please select a tutorial".
        /// It is BOTH the screen's spoken name, said on arrival, and a node of its own at the top of
        /// the page, so it can be gone back to; focus starts on the cards, which is what keeps it from
        /// being said twice.</summary>
        public override string ScreenName
        {
            get
            {
                string heading = HeadingText(Window());
                return string.IsNullOrEmpty(heading)
                    ? ModStrings.Get(ModStrings.ScreenTutorialSelection)
                    : heading;
            }
        }

        public override bool IsActive()
        {
            TutorialSelectionModalWindow window = Window();
            try
            {
                return window != null && window.Shown && window.IsReady;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Escape is the game's: the window handles it itself and cancels, which is the same
        /// route "No, thanks" takes.</summary>
        public override bool Back()
        {
            return false;
        }

        public override void Build(GraphBuilder builder)
        {
            TutorialSelectionModalWindow window = Window();
            if (window == null)
            {
                return;
            }

            builder.BeginStop(ChoicesStop);
            AddHeading(builder, window);
            BuildChoices(builder, window);

            builder.BeginStop(ActionsStop);
            BuildActions(builder, window);
        }

        /// <summary>The question drawn across the top of the box, as the page's first node. The window
        /// binds neither its title group nor the label inside it, so it is found by where it is drawn
        /// rather than by name.</summary>
        private void AddHeading(GraphBuilder builder, TutorialSelectionModalWindow window)
        {
            _labels.Clear();
            CollectLabels(WindowTransform(window), _labels, 3);
            if (_labels.Count == 0)
            {
                return;
            }

            AgePrimitiveLabel heading = _labels[0];
            builder.AddItem(
                ControlId.Referenced(heading, "tutorial-selection:heading"),
                GraphNodes.Readout(() => AgeText.Label(heading), () => null, null, null)
            );
        }

        /// <summary>The words of that heading, for the screen's spoken name.</summary>
        private string HeadingText(TutorialSelectionModalWindow window)
        {
            try
            {
                _labels.Clear();
                CollectLabels(WindowTransform(window), _labels, 3);
                return _labels.Count == 0 ? null : AgeText.Label(_labels[0]);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static AgeTransform WindowTransform(TutorialSelectionModalWindow window)
        {
            try
            {
                return window == null ? null : window.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---- the three cards ----

        private void BuildChoices(GraphBuilder builder, TutorialSelectionModalWindow window)
        {
            _cells.Clear();
            AddCard(_cells, window.BeginnerToggle, 0);
            AddCard(_cells, window.AdvancedToggle, 1);
            AddCard(_cells, window.ExpertToggle, 2);
            Emit(builder, _cells);

            // Focus starts on the cards, not on the heading above them: the heading is also what
            // arriving announces, and hearing it twice in a row is the cost of having it both ways.
            if (_cells.Count > 0)
            {
                builder.SetStart(_cells[0].Id);
            }
        }

        /// <summary>
        /// One card. Its name is the heading the game drew on it and its description is the paragraph
        /// underneath - both read off the card, because the window names neither.
        ///
        /// Activating it replays the card's own click path (<see cref="AgeWidgets.Toggle"/>: the state,
        /// then the switch handler that reads it), so the game makes the choice exclusive and plays the
        /// click it plays for a mouse. Pressing the card that is already chosen re-chooses it, which is
        /// what a mouse click on it does too - there is no untick here.
        /// </summary>
        private void AddCard(List<Cell> cells, AgeControlToggle card, int index)
        {
            AgeTransform widget = AgeWidgets.Transform(card);
            if (card == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeControlToggle it = card;
            NodeVtable vtable = GraphNodes.Radio(
                () => Title(it),
                () => it.State,
                () => AgeWidgets.Toggle(it),
                () => AgeWidgets.Operable(AgeWidgets.Transform(it))
            );

            // The card's own explanation - the one section this control has. It is drawn on the card
            // permanently rather than offered on a hover, so it is announced outright the way a
            // control's own sentence of description is, and the review buffer holds it either way.
            // Declared with its mode rather than read off a tooltip, because there is no tooltip here:
            // this is a control the mod made out of what the game painted on a card.
            vtable.Sections = GraphNodes.Sections(
                new NodeSection(() => Description(it), TooltipMode.Announce)
            );

            AgeWidgets.Point(vtable, card);
            Add(cells, widget, ControlId.Referenced(card, "tutorial-selection:choice/" + index), vtable);
        }

        /// <summary>The heading drawn across the top of a card - the topmost of the words on it.
        /// </summary>
        private string Title(AgeControlToggle card)
        {
            _labels.Clear();
            CollectLabels(AgeWidgets.Transform(card), _labels, 4);
            return _labels.Count == 0 ? null : AgeText.Label(_labels[0]);
        }

        /// <summary>Everything the card says under its heading, a line at a time. The game writes the
        /// paragraph as one label with blank lines in it, which is exactly how it should be walked.
        /// </summary>
        private IList<string> Description(AgeControlToggle card)
        {
            _labels.Clear();
            CollectLabels(AgeWidgets.Transform(card), _labels, 4);
            List<string> lines = new List<string>();
            for (int i = 1; i < _labels.Count; i++)
            {
                IList<string> part = AgeText.Lines(AgeText.Label(_labels[i]));
                for (int j = 0; part != null && j < part.Count; j++)
                {
                    lines.Add(part[j]);
                }
            }

            return lines;
        }

        // ---- the bottom row ----

        /// <summary>Refuse and Confirm, discovered from the band they share rather than named: the
        /// window exposes Confirm as a field and "No, thanks" only as its sibling, and taking both from
        /// the band keeps them in the order they are drawn. Confirm stays declared while it is refusing
        /// - hearing that it is there and why it will not go yet is the point.</summary>
        private void BuildActions(GraphBuilder builder, TutorialSelectionModalWindow window)
        {
            _cells.Clear();
            try
            {
                AgeTransform confirm = AgeWidgets.Transform(window.ConfirmButton);
                AgeTransform band = confirm == null ? null : confirm.Parent;
                IList<AgeTransform> children = band == null ? null : band.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    AddButton(_cells, children[i]);
                }
            }
            catch (Exception) { }

            Emit(builder, _cells);
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
            NodeVtable vtable = GraphNodes.Button(
                () => AgeWidgets.TextOf(it),
                () => AgeWidgets.Press(it),
                () => AgeWidgets.Operable(it)
            );
            AgeWidgets.Point(vtable, button);
            Add(cells, widget, ControlId.Referenced(widget, "tutorial-selection:button/" + Name(widget)), vtable);
        }

        // ---- shared ----
        /// <summary>Every label the player can see under <paramref name="widget"/>, topmost first.
        /// </summary>
        private static void CollectLabels(
            AgeTransform widget,
            List<AgePrimitiveLabel> into,
            int depth
        )
        {
            Descend(widget, into, depth);
            into.Sort(Drawn);
        }

        private static void Descend(AgeTransform widget, List<AgePrimitiveLabel> into, int depth)
        {
            if (widget == null || depth < 0)
            {
                return;
            }

            try
            {
                if (!widget.Visible)
                {
                    return;
                }

                AgePrimitiveLabel label = widget.GetComponent<AgePrimitiveLabel>();
                if (label != null)
                {
                    into.Add(label);
                }

                IList<AgeTransform> children = widget.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    Descend(children[i], into, depth - 1);
                }
            }
            catch (Exception) { }
        }

        private static int CompareByTop(AgePrimitiveLabel left, AgePrimitiveLabel right)
        {
            try
            {
                return left.AgeTransform.GetGlobalPosition()
                    .y.CompareTo(right.AgeTransform.GetGlobalPosition().y);
            }
            catch (Exception)
            {
                return 0;
            }
        }

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

        /// <summary>A control on its way into the graph, still carrying the widget it was read from: the
        /// rows are worked out from a whole band at once, which cannot be done while declaring it row by
        /// row.</summary>
        private sealed class Cell
        {
            public AgeTransform Widget;
            public ControlId Id;
            public NodeVtable Vtable;
        }

        private static readonly Func<Cell, AgeTransform> CellWidget = cell => cell.Widget;

        /// <summary>One node per row, in the order the game drew them. The three cards are peers of one
        /// kind and so are the two buttons under them: the line the window drew each set on is a fact
        /// about its layout box, so the player walks the whole page with one key.</summary>
        private static void Emit(GraphBuilder builder, List<Cell> cells)
        {
            foreach (List<Cell> row in AgeLayout.Rows(cells, CellWidget))
            {
                foreach (Cell cell in row)
                {
                    builder.AddItem(cell.Id, cell.Vtable);
                }
            }
        }

        private static void Add(
            List<Cell> cells,
            AgeTransform widget,
            ControlId id,
            NodeVtable vtable
        )
        {
            cells.Add(new Cell { Widget = widget, Id = id, Vtable = vtable });
        }

        private static TutorialSelectionModalWindow Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<TutorialSelectionModalWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
