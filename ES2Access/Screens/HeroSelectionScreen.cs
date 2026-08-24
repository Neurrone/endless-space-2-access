using System;
using System.Collections.Generic;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// Which hero to put somewhere: the window every "assign a hero" affordance in the game opens - a
    /// colony's governor slot (<c>ColonyHeroSidePanel.OnAssignCb</c> :268-274), a fleet's hero slot, the
    /// systems management panel's - each installing its own <c>Delegate</c> and its own
    /// <c>Assignation</c>, and none of that written down here. What is modelled is what the window DRAWS:
    /// a strip of hero cards and a band of three buttons.
    ///
    /// The game's model is select-then-confirm and it is copied rather than shortened. A card is a RADIO:
    /// Enter is the card's own toggle, which only records the choice (<c>HeroDetailedCard.OnSwitchCb</c>
    /// :410-421 -&gt; <c>HeroSelectionModalWindow.OnHeroSelection</c> :161-167, which sets
    /// <c>SelectedHero</c> and re-reads the buttons), and the assignment happens when Confirm is pressed.
    /// The game also commits on a DOUBLE click of the card's inner Content button
    /// (<c>OnDoubleClickCb</c> -&gt; <c>OnHeroDoubleClick</c> :169-179, which assigns and closes with no
    /// confirmation), and that gesture is deliberately not wired: a single Enter that both picked and
    /// assigned would make every pass over the strip a decision.
    ///
    /// What a card SAYS is <see cref="HeroCards"/>' - the shared reader for the one prefab family five
    /// surfaces draw - so this screen inherits the bands its own window never shows. The card's own
    /// buttons (experience, health, the assignment locator) are reviewable this round and not offered as
    /// controls; what they show is already in the card's buffer.
    ///
    /// A hero the assignment will not take is left DRAWN and switched off with the game's own sentence on
    /// the card's tooltip (<c>RefreshButtonsAndSelection</c> :122-142), so it reads unavailable and says
    /// why, and Enter does nothing.
    ///
    /// Two hazards, both the game's:
    /// - <c>Refresh</c> :74-77 reads <c>if (heroes.Contains(SelectedHero)) SelectedHero = null;</c> - an
    ///   inverted test that WIPES the selection on any refresh of the window. Nothing here caches which
    ///   card is picked: the state is asked live off the card the game ticks, so a selection vanishing
    ///   under the player is heard rather than believed.
    /// - Inspect opens <c>HeroInspectionModalWindow</c>, modelled by <see cref="HeroInspectionScreen"/>;
    ///   the game's own Escape closes it back onto this one.
    ///
    /// Escape is the game's: the window is a <c>GuiModalWindow</c> and hiding it assigns nothing.
    /// </summary>
    public sealed class HeroSelectionScreen : Screen
    {
        private static readonly object CardsStop = "hero-select:cards";
        private static readonly object ActionsStop = "hero-select:actions";

        private readonly List<Cell> _cells = new List<Cell>();

        public override string Key
        {
            get { return "screen.hero-selection"; }
        }

        /// <summary>Above the pages that open it and below the hero inspection window its own Inspect
        /// button raises.</summary>
        public override int Layer
        {
            get { return 28; }
        }

        /// <summary>What the window has written across its top - "Select a Hero". The cards are what
        /// focus lands on, so the title is said as the screen's name rather than declared as a control
        /// nobody would walk to.</summary>
        public override string ScreenName
        {
            get { return Title(Window()); }
        }

        /// <summary>The cards: they are drawn first, Tab does not wrap, and the card already picked is
        /// where focus lands inside the stop (the radio's own selected part).</summary>
        public override object InitialFocusStop
        {
            get { return CardsStop; }
        }

        /// <summary>Set once the window has finished arriving, cleared when the game drops the delegate
        /// its opener installed - which is the last thing <c>OnEndHide</c> does (:59-67) and so the
        /// unbind gate, rather than visibility, which stops reporting while the page beneath is still
        /// disabled. Instance state, so a hot reload starts it over.</summary>
        private bool _arrived;

        public override bool IsActive()
        {
            HeroSelectionModalWindow window = Window();
            try
            {
                if (window == null || window.Delegate == null)
                {
                    _arrived = false;
                    return false;
                }

                if (!_arrived)
                {
                    _arrived = window.Shown && window.IsReady;
                }

                return _arrived && !Buried(window);
            }
            catch (Exception)
            {
                _arrived = false;
                return false;
            }
        }

        /// <summary>
        /// Whether the game has put another modal on top of this one - which for a modal is not being
        /// COVERED but being HIDDEN: the stack is exclusive, so opening the inspection window this
        /// window's own Inspect button opens takes this one off the screen entirely (measured:
        /// <c>heroSel.Shown=False, AgeTransform.Visible=False</c> while the inspection window is up).
        /// Keeping the keyboard here would leave the player pressing Enter on a Confirm they cannot see.
        ///
        /// Asked of the game's own record of which modal is on top (<c>GuiManager.ModalOnTop</c>, written
        /// by <c>ModalWindow_VisibilityChanged</c> :1750-1765) rather than of this window's visibility,
        /// because the two answers differ on the frame that matters: while this window is closing,
        /// visibility is already false and there is no modal on top at all - and leaving THEN is the
        /// departure that must wait for the unbind, or the page underneath reads every control unavailable
        /// (the <see cref="ImprovementsModalScreen"/> measurement).
        /// </summary>
        private static bool Buried(HeroSelectionModalWindow window)
        {
            try
            {
                GuiManager manager = Gui.GuiGameWindowService as GuiManager;
                GuiModalWindow top = manager == null ? null : manager.ModalOnTop;
                return top != null && !ReferenceEquals(top, window);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public override void Build(GraphBuilder builder)
        {
            HeroSelectionModalWindow window = Window();
            if (window == null || !window.Shown)
            {
                // On the way out. The screen stays ACTIVE until the game unbinds it - leaving at
                // begin-hide would hand the keyboard to a page that is not interactive yet - but it
                // declares nothing while the window fades, because the game switches these controls off
                // as it goes and a live part on the focused one would announce the fade as a refusal
                // (heard once as "unavailable" on pressing Cancel). An empty render keeps the cursor.
                return;
            }

            builder.BeginStop(CardsStop);
            BuildCards(builder, window);

            builder.BeginStop(ActionsStop);
            BuildActions(builder, window);
        }

        // ---- the cards ----

        /// <summary>
        /// One card per hero the empire has, one per row in the order the game laid them out - left to
        /// right, then down - because the cards are peers of one kind and where the strip wrapped is a
        /// fact about the table rather than about the heroes.
        ///
        /// Keyed on the HERO, not the card: the table pools its cards and re-binds them by index on every
        /// refresh (<c>BindHeroCard</c> :86-107), so a cursor keyed on the widget would act on a
        /// different hero a frame later.
        /// </summary>
        private void BuildCards(GraphBuilder builder, HeroSelectionModalWindow window)
        {
            _cells.Clear();
            ControlId start = null;
            try
            {
                AgeTransform table = window.HeroCardsTable;
                IList<AgeTransform> children = table == null ? null : table.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    ControlId id = AddCard(_cells, children[i], i);
                    if (id != null && Picked(window, children[i]))
                    {
                        start = id;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("hero selection: reading the cards threw: " + e);
            }

            Cells.EmitLinear(builder, _cells);
            if (start != null)
            {
                builder.SetStart(start);
            }
        }

        private static ControlId AddCard(List<Cell> cells, AgeTransform widget, int index)
        {
            HeroDetailedCard card = Card(widget);
            Hero hero = HeroCards.Hero(card);
            if (hero == null || !Drawn(widget))
            {
                return null;
            }

            HeroDetailedCard it = card;
            AgeTransform host = widget;
            AgeTooltip refusal = AgeWidgets.Raw(widget);
            NodeVtable vtable = GraphNodes.Radio(
                HeroCards.Name(card),
                () => it.Toggle != null && it.Toggle.State,
                () => AgeWidgets.Toggle(it.Toggle),
                () => AgeWidgets.Operable(host)
            );
            // The card's whole drawn face, then the two tooltips it carries: the dossier the game hangs
            // on the card as a whole, and - last, so it is the one that speaks - the sentence written
            // there when this hero cannot take the assignment.
            vtable.Sections = GraphNodes.Sections(
                NodeSection.Buffer(() => HeroCards.Lines(it)),
                GraphNodes.TooltipSection(it == null ? null : it.HeroTooltip),
                GraphNodes.TooltipSection(refusal)
            );
            AgeWidgets.Point(vtable, it.Toggle, refusal, host);
            ControlId id = ControlId.Referenced(hero, "hero-select:card/" + index);
            Cells.Add(cells, widget, id, vtable);
            return id;
        }

        /// <summary>Whether this is the card the game has ticked. Asked of the window rather than of the
        /// card's own toggle only here, where the answer decides where focus STARTS - the tick is
        /// rewritten on every refresh from exactly this field.</summary>
        private static bool Picked(HeroSelectionModalWindow window, AgeTransform widget)
        {
            try
            {
                return window.SelectedHero != null
                    && ReferenceEquals(HeroCards.Hero(Card(widget)), window.SelectedHero);
            }
            catch (Exception)
            {
                return false;
            }
        }

        // ---- the bottom band ----

        /// <summary>Cancel, Inspect and Confirm, taken from the band they share rather than named: the
        /// window exposes two of the three and reading the band keeps them in the order they are drawn
        /// in. Confirm and Inspect are switched off until a hero is picked, which is what makes them read
        /// unavailable with the game's own sentence for what they would do - Confirm's tooltip has the
        /// reason appended to it (<c>RefreshButtonsAndSelection</c> :147-158).</summary>
        private void BuildActions(GraphBuilder builder, HeroSelectionModalWindow window)
        {
            _cells.Clear();
            try
            {
                AgeTransform validate =
                    window.ValidateButton == null ? null : window.ValidateButton.AgeTransform;
                AgeTransform band = validate == null ? null : validate.Parent;
                IList<AgeTransform> children = band == null ? null : band.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    AddButton(_cells, children[i]);
                }
            }
            catch (Exception e)
            {
                Log.Warn("hero selection: reading the button band threw: " + e);
            }

            Cells.EmitLinear(builder, _cells);
        }

        private static void AddButton(List<Cell> cells, AgeTransform widget)
        {
            AgeControlButton button = widget == null ? null : AgeWidgets.Button(widget);
            if (button == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeTransform it = widget;
            NodeVtable vtable = GraphNodes.Button(
                () => AgeWidgets.TextOf(it),
                () => AgeWidgets.Press(it),
                () => AgeWidgets.Operable(it),
                AgeWidgets.Raw(it)
            );
            AgeWidgets.Point(vtable, button);
            Cells.Add(
                cells,
                widget,
                ControlId.Referenced(widget, "hero-select:button/" + Name(widget)),
                vtable
            );
        }

        // ---- reading the window ----

        /// <summary>The window's own title, found where it is drawn: the class exposes its cards and two
        /// of its buttons and nothing else.</summary>
        private static string Title(HeroSelectionModalWindow window)
        {
            try
            {
                if (window == null)
                {
                    return null;
                }

                AgePrimitiveLabel[] labels =
                    window.GetComponentsInChildren<AgePrimitiveLabel>(true);
                for (int i = 0; i < labels.Length; i++)
                {
                    if (labels[i] != null && labels[i].name == "WindowTitle")
                    {
                        return AgeText.Label(labels[i]);
                    }
                }
            }
            catch (Exception) { }

            return null;
        }

        /// <summary>A card the game has stopped using is left in the pool transparent rather than hidden,
        /// so alpha is half the question.</summary>
        private static bool Drawn(AgeTransform widget)
        {
            try
            {
                return AgeWidgets.Visible(widget) && widget.Alpha > 0f;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static HeroDetailedCard Card(AgeTransform widget)
        {
            try
            {
                return widget == null ? null : widget.GetComponent<HeroDetailedCard>();
            }
            catch (Exception)
            {
                return null;
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

        private static HeroSelectionModalWindow Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<HeroSelectionModalWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
