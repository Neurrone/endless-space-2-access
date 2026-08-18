using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using UnityEngine;

namespace ES2Access.Screens
{
    /// <summary>
    /// "Which of these do you want to fight?" - the box the game raises when a fleet's attack or invasion
    /// order has more than one thing it could be aimed at.
    ///
    /// There are two of these windows, one per kind of battle: <c>TargetSelectionModalWindow</c>, a card
    /// per EMPIRE whose fleets are in the orbit (<c>FleetActionButtonAttack.ShowTargets</c>), and
    /// <c>GroundBattleTargetSelectionModalWindow</c>, a card per SYSTEM the fleet could invade
    /// (<c>FleetActionButtonGroundBattle</c>). Field for field they are the same window - one radio group
    /// of cards, a four-card viewport with paging arrows, one Validate button - so they are read by one
    /// implementation over a <see cref="Target"/> adapter, and differ only in which window each screen
    /// asks for and what a card is called.
    ///
    /// The model is the game's own and is copied rather than shortened: a card is a RADIO whose Enter is
    /// the card's own toggle, and Validate is what commits. It follows from that copy that pressing Enter
    /// a SECOND time on the card that is already picked commits as well - the window's own
    /// <c>OnCardSelected</c> treats a click on the current selection as a validation - which is exactly
    /// what a second mouse click does and is left as click parity rather than being suppressed. Validate
    /// is switched off until something is picked (<c>RefreshButtonsState</c>), so it reads unavailable and
    /// does nothing, which is the box saying it still needs an answer.
    ///
    /// EVERY card is declared, including the ones outside the four-card viewport, and the arrows that page
    /// it are NOT. The arrows are the mouse's affordance for a window too narrow to show everything; the
    /// keyboard's is that Tab reaches all of them and says "3 of 7". Landing on a card the viewport is not
    /// showing pages it in through the game's OWN paging path - the arrows' handlers, one card at a time,
    /// bounded exactly the way the window bounds them - so what a sighted player sees keeps following the
    /// cursor and no visibility flag is written by the mod.
    ///
    /// Escape is the game's: <c>OnCancelCb</c> tells the fleet action the player changed their mind, which
    /// is a different answer from closing the window and one only the game can give.
    /// </summary>
    public abstract class TargetSelectionScreenBase : Screen
    {
        private readonly List<Cell> _cells = new List<Cell>();

        /// <summary>The window this screen reads, or null while the game does not have it. Asked every
        /// frame, so it stays a lookup and allocates nothing - which is why it is separate from
        /// <see cref="Read"/>.</summary>
        protected abstract GuiWindow Showing();

        /// <summary>The parts of that window the shared reading needs. Called from
        /// <see cref="Build"/> alone, where allocating is already the norm.</summary>
        protected abstract Target Read();

        /// <summary>The key prefix and the mod's own fallback name for this flavour of the box.</summary>
        protected abstract string Prefix { get; }

        protected abstract string ScreenNameKey { get; }

        public override string ScreenName
        {
            get
            {
                string title = WindowShape.Title(Showing());
                return string.IsNullOrEmpty(title)
                    ? OptionalText.Phrase(ScreenNameKey)
                    : title;
            }
        }

        public override bool IsActive()
        {
            try
            {
                GuiWindow window = Showing();
                return window != null && window.Shown && window.IsReady;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The game's: cancelling is an answer the fleet action is waiting for.</summary>
        public override bool Back()
        {
            return false;
        }

        public override void Build(GraphBuilder builder)
        {
            Target target = Read();
            if (target == null || target.Cards == null)
            {
                return;
            }

            _cells.Clear();
            Cards(target);
            // The paging arrows are left out on purpose (see the class comment); everything else the
            // window drew - Validate, and whatever way out the prefab draws - is read off what is there.
            WindowShape.Controls(_cells, target.Window, Prefix, target.Selector);
            Cells.EmitLinear(builder, _cells);
        }

        /// <summary>
        /// One radio per card, in the row the window laid them out in.
        ///
        /// Keyed on the card's POSITION rather than on the card object: the table pools its cards and
        /// re-binds them by index on every showing, so a cursor keyed on the widget would be standing on a
        /// different target the next time the box opens.
        /// </summary>
        private void Cards(Target target)
        {
            IList<AgeTransform> children = Children(target.Cards);
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform widget = children[i];
                AgeControlToggle toggle = Toggle(widget);
                if (toggle == null || !target.Bound(widget))
                {
                    continue;
                }

                int index = i;
                Target it = target;
                AgeControlToggle switched = toggle;
                AgeTransform card = widget;
                AgeTooltip tooltip = AgeWidgets.Raw(widget);
                NodeVtable vtable = GraphNodes.Radio(
                    () => Name(it, card),
                    () => switched.State,
                    () => AgeWidgets.Toggle(switched),
                    // The card's own visibility is NOT the question: a card outside the viewport is one
                    // the player can still choose, and choosing it is what pages it into view. What
                    // would refuse is the table itself being switched off, which is the window telling
                    // the player it has their answer already.
                    () => AgeWidgets.Operable(it.Cards),
                    null,
                    tooltip
                );
                // Paged into view BEFORE the pointer follows: the card has no place on screen until the
                // viewport is showing it, and a tooltip anchored to a rect nobody is drawing lands on
                // empty sky.
                AgeWidgets.Point(vtable, switched, tooltip, card);
                Action hover = vtable.OnFocusVisual;
                vtable.OnFocusVisual = () =>
                {
                    Reveal(it, index);
                    hover();
                };
                Cells.Add(
                    _cells,
                    widget,
                    ControlId.Structural(Prefix + "/card/" + index),
                    vtable
                );
            }
        }

        /// <summary>What a card is called: everything written on it. A target card is built out of groups
        /// - a portrait block, the empire's name, a line per fleet or the system's type - and the whole of
        /// that is what the player is choosing between, so the flavour's own name for the target is only
        /// used where the card drew no words at all.</summary>
        private string Name(Target target, AgeTransform card)
        {
            string drawn = AgeWidgets.TextOf(card, CardDepth);
            return string.IsNullOrEmpty(drawn) ? target.Name(card) : drawn;
        }

        /// <summary>How deep to read a card's words. A target card nests a table of fleet lines inside a
        /// scroll view inside a group, which is deeper than the shared default.</summary>
        private const int CardDepth = 8;

        /// <summary>
        /// Page the viewport until the card at <paramref name="index"/> is inside it, using the window's
        /// own arrow handlers.
        ///
        /// Bounded the way the window bounds itself: a window showing everything pages nothing, and the
        /// first-shown index never leaves 0..count-viewport. That matters because the arrow handlers do
        /// NOT clamp - <c>ShowOtherCards</c> shifts the visible run by the delta it is given, and one
        /// press too many would leave the window drawing no cards at all. The loop also stops the moment a
        /// press fails to move anything, so a game that has changed its mind about paging cannot spin it.
        /// </summary>
        private static void Reveal(Target target, int index)
        {
            try
            {
                IList<AgeTransform> children = Children(target.Cards);
                int count = children == null ? 0 : children.Count;
                int viewport = target.Viewport;
                if (count <= viewport || viewport <= 0)
                {
                    return;
                }

                int first = FirstShown(children);
                if (first < 0)
                {
                    return;
                }

                int want = first;
                if (index < first)
                {
                    want = index;
                }
                else if (index >= first + viewport)
                {
                    want = index - viewport + 1;
                }

                want = Mathf.Clamp(want, 0, count - viewport);
                for (int step = 0; step < count && want != first; step++)
                {
                    AgeWidgets.Press(want < first ? target.Previous : target.Next);
                    int now = FirstShown(children);
                    if (now == first)
                    {
                        return;
                    }

                    first = now;
                }
            }
            catch (Exception e)
            {
                Log.Warn("target selection: paging the cards threw: " + e);
            }
        }

        /// <summary>Which card the viewport starts at - the window's own <c>GetFirstCardShownIndex</c>,
        /// which is private on both windows and is the one thing the mod has to ask by looking.</summary>
        private static int FirstShown(IList<AgeTransform> children)
        {
            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] != null && children[i].Visible)
                {
                    return i;
                }
            }

            return -1;
        }

        private static AgeControlToggle Toggle(AgeTransform widget)
        {
            try
            {
                return widget == null
                    ? null
                    : widget.GetComponentInChildren<AgeControlToggle>(true);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static IList<AgeTransform> Children(AgeTransform widget)
        {
            try
            {
                return widget == null ? null : widget.Children;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The parts of whichever of the two windows is up, so the reading above is written
        /// once. <see cref="Name"/> is the flavour's own word for a target, for a card that drew none;
        /// <see cref="Bound"/> is how it tells a live card from a pooled spare.</summary>
        protected sealed class Target
        {
            public GuiWindow Window;
            public AgeTransform Cards;
            public AgeTransform Selector;
            public AgeControlButton Previous;
            public AgeControlButton Next;
            public int Viewport;
            public Func<AgeTransform, string> Name;
            public Func<AgeTransform, bool> Bound;
        }
    }

    /// <summary>Which empire's fleets to attack, in the orbit the fleet is sitting in.</summary>
    public sealed class TargetSelectionScreen : TargetSelectionScreenBase
    {
        public override string Key
        {
            get { return "screen.target-selection"; }
        }

        /// <summary>Over the galaxy page and the fleet panel the order was given from, and under the
        /// notifications the game can raise while it is up.</summary>
        public override int Layer
        {
            get { return 23; }
        }

        protected override string Prefix
        {
            get { return "target-select"; }
        }

        protected override string ScreenNameKey
        {
            get { return "screen.target-selection"; }
        }

        protected override GuiWindow Showing()
        {
            return Window();
        }

        protected override Target Read()
        {
            TargetSelectionModalWindow window = Window();
            if (window == null)
            {
                return null;
            }

            return new Target
            {
                Window = window,
                Cards = window.EmpireCardsTable,
                Selector = window.SelectorGroup,
                Previous = window.PreviousButton,
                Next = window.NextButton,
                Viewport = window.MaxCardNumber,
                Name = EmpireName,
                Bound = IsCard,
            };
        }

        /// <summary>The empire whose fleets this card holds, in the game's own name for it.</summary>
        private static string EmpireName(AgeTransform widget)
        {
            try
            {
                EmpireFleetCard card = Card(widget);
                GuiEmpire empire = card == null ? null : card.GuiEmpire;
                return empire == null ? null : AgeText.Clean(empire.Title);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool IsCard(AgeTransform widget)
        {
            EmpireFleetCard card = Card(widget);
            try
            {
                return card != null && card.GuiEmpire != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static EmpireFleetCard Card(AgeTransform widget)
        {
            try
            {
                return widget == null ? null : widget.GetComponent<EmpireFleetCard>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static TargetSelectionModalWindow Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<TargetSelectionModalWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    /// <summary>Which system to invade, of the ones this fleet's troops could land on.</summary>
    public sealed class GroundTargetSelectionScreen : TargetSelectionScreenBase
    {
        public override string Key
        {
            get { return "screen.ground-target-selection"; }
        }

        /// <summary>Its own number rather than the space box's: the two are separate windows and nothing
        /// in the game promises they cannot both be up.</summary>
        public override int Layer
        {
            get { return 24; }
        }

        protected override string Prefix
        {
            get { return "ground-target-select"; }
        }

        protected override string ScreenNameKey
        {
            get { return "screen.ground-target-selection"; }
        }

        protected override GuiWindow Showing()
        {
            return Window();
        }

        protected override Target Read()
        {
            GroundBattleTargetSelectionModalWindow window = Window();
            if (window == null)
            {
                return null;
            }

            return new Target
            {
                Window = window,
                Cards = window.TargetCardsTable,
                Selector = window.SelectorGroup,
                Previous = window.PreviousButton,
                Next = window.NextButton,
                Viewport = window.MaxCardNumber,
                Name = SystemName,
                Bound = IsCard,
            };
        }

        /// <summary>The system this card would invade, in the game's own name for it.</summary>
        private static string SystemName(AgeTransform widget)
        {
            try
            {
                EmpireSystemCard card = Card(widget);
                ColonizedStarSystem system = card == null ? null : card.System;
                return system == null ? null : AgeText.Clean(system.LocalizedName);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool IsCard(AgeTransform widget)
        {
            EmpireSystemCard card = Card(widget);
            try
            {
                return card != null && card.System != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static EmpireSystemCard Card(AgeTransform widget)
        {
            try
            {
                return widget == null ? null : widget.GetComponent<EmpireSystemCard>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static GroundBattleTargetSelectionModalWindow Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<GroundBattleTargetSelectionModalWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
