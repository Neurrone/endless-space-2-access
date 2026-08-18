using System;
using System.Collections.Generic;
using ES2Access.Core.UI.Graph;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// The disclaimer box the game can put in front of the main menu (<c>DisclaimerModalWindow</c>).
    ///
    /// Two shapes, one window. As a DISCLAIMER it is a stack of statements the player has to agree to one
    /// after another - a computer under the minimum specification, an alpha or beta build - and it offers "I
    /// Agree", which steps to the next one and remembers the answer, and "Decline", which QUITS THE GAME
    /// (<c>OnDeclineCB</c> calls <c>Application.Quit</c>). As an INFORMATIVE box (<c>ShowInformative</c>) it
    /// says one thing and offers "More Info", which opens the community site in a browser, and "Next", which
    /// closes it. Which pair of buttons is drawn is the game's answer and this reads whichever it drew, so
    /// one model covers both.
    ///
    /// THE WORDS ARE THE WHOLE POINT and they are what the shape floor cannot see: the buttons carry
    /// captions and would have been reachable anyway, while the statement itself is a label in a scroll
    /// view - so it is declared as a line of prose, said whole and walkable line by line in the review
    /// buffer. A player who cannot read it cannot answer it.
    ///
    /// ESCAPE DOES NOTHING HERE, and that is the game's decision, not an omission: <c>HandleInput</c>
    /// returns true for every action without acting on any of them, so the box swallows Exit and the only
    /// ways out are the buttons it drew. The mod does not answer the key either
    /// (<see cref="MenuDestinationScreen.Back"/>), because inventing an escape would dismiss a statement the
    /// game means to have an answer to.
    ///
    /// Reachable in retail on a machine whose graphics hardware is under the minimum
    /// (<c>MainMenuScreen.ShowDisclaimers</c>) and on an alpha or beta build; the informative shape has one
    /// caller-less route left in the code (<c>Gui.ShowLongGameDisclaimerIfNeeded</c>). Neither is a state a
    /// save can produce, so both were measured by calling the window's own <c>Show</c> with the game's own
    /// strings.
    /// </summary>
    public sealed class DisclaimerScreen : MenuDestinationScreen
    {
        private static readonly object TextStop = "disclaimer:text";
        private static readonly object ActionsStop = "disclaimer:actions";

        private readonly List<Cell> _cells = new List<Cell>();

        public override string Key
        {
            get { return "screen.disclaimer"; }
        }

        protected override string Prefix
        {
            get { return "disclaimer"; }
        }

        protected override string ScreenNameKey
        {
            get { return "screen.disclaimer"; }
        }

        /// <summary>What there is to answer, which has to be read before the answer is given.</summary>
        public override object InitialFocusStop
        {
            get { return TextStop; }
        }

        protected override GuiWindow Window()
        {
            return Get<DisclaimerModalWindow>();
        }

        public override void Build(GraphBuilder builder)
        {
            DisclaimerModalWindow window = Window() as DisclaimerModalWindow;
            if (window == null)
            {
                return;
            }

            builder.BeginStop(TextStop);
            Statement(builder, window);

            builder.BeginStop(ActionsStop);
            _cells.Clear();
            WindowShape.Controls(_cells, window, Prefix);
            Cells.EmitLinear(builder, _cells);
        }

        /// <summary>The statement itself. The title is not declared beside it: it is this screen's NAME,
        /// and the game hangs an unrelated sentence on that label which belongs to another window's
        /// prefab.</summary>
        private static void Statement(GraphBuilder builder, DisclaimerModalWindow window)
        {
            AgePrimitiveLabel label = Content(window);
            AgeTransform widget = label == null ? null : label.AgeTransform;
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgePrimitiveLabel it = label;
            NodeVtable vtable = GraphNodes.Readout(
                () => AgeText.Label(it),
                () => null,
                () => AgeText.Lines(AgeText.Label(it)),
                null
            );
            AgeWidgets.PointAt(vtable, widget);
            builder.AddItem(ControlId.Referenced(label, "disclaimer:statement"), vtable);
        }

        private static AgePrimitiveLabel Content(DisclaimerModalWindow window)
        {
            try
            {
                return window.Content;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
