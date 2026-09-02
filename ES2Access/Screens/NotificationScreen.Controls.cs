using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using ES2Access.UI.Input;
using UnityEngine;

namespace ES2Access.Screens
{
    /// <summary>The popup's controls: which of its three bands each one is drawn in, what the
    /// collection of them is, and the strip each band is walked as.</summary>
    public sealed partial class NotificationScreen
    {
        /// <summary>
        /// Which of the popup's three bands each control is drawn in.
        ///
        /// Every notification is built out of the same skeleton, and the skeleton is what answers:
        /// the arrows and the pop-up-again box sit beside the title along the top, dismissing and
        /// putting aside sit along the bottom, and whatever the popup added of its own is its
        /// content - walked among the rows where the player sees it rather than swept into a strip.
        ///
        /// The popup answers back in the bottom bar, though, and often: accepting an offer, opening
        /// the academy, replaying a battle are all drawn in the row Dismiss is in. So the question is
        /// asked of the CONTAINER the game drew the control in rather than of its rectangle - a
        /// control inside the title bar or inside the button bar belongs to that strip, everything
        /// else to the body. Measured over all sixty-nine popup windows: fifty of their own buttons
        /// sit inside the button bar, none anywhere inside the title bar, and the forty-eight other
        /// controls they add all sit in content containers of their own.
        ///
        /// Rectangles cannot answer it, whether measured against the words the popup says or against
        /// the rails themselves. A survey draws its party lines level with nothing in particular,
        /// beside a pie chart, above a description the game puts at the BOTTOM of the content - and
        /// measuring swept all four of them into the top strip, interleaved with the browsing arrows.
        /// </summary>
        private static void Sort(
            NotificationWindow window,
            List<Control> controls,
            List<Control> above,
            List<Control> inside,
            List<Control> below
        )
        {
            List<AgeTransform> title = TitleBar(window, controls);
            List<AgeTransform> buttons = ButtonBar(controls);
            foreach (Control control in controls)
            {
                if (Array.IndexOf(TopKeys, control.Key) >= 0 || Within(control.Widget, title))
                {
                    above.Add(control);
                }
                else if (
                    Array.IndexOf(BottomKeys, control.Key) >= 0
                    || Within(control.Widget, buttons)
                )
                {
                    below.Add(control);
                }
                else
                {
                    inside.Add(control);
                }
            }
        }

        /// <summary>The rails themselves, by name: a control the base window owns is its strip's
        /// whatever the popup did with it - one that parks Dismiss in a box of its own still dismisses
        /// from the bottom. Internal because the family's self-check tells a rail from a popup's own
        /// control by these names, and a second spelling of them would make every popup read clean.
        /// </summary>
        internal static readonly string[] TopKeys = { "next", "previous", "auto-popup" };

        internal static readonly string[] BottomKeys = { "dismiss", MinimizeKey, "show-location" };

        /// <summary>The bar the title is drawn across, which is the one the browsing arrows and the
        /// pop-up-again box are drawn in: the game lays both of them out as groups inside it.</summary>
        private static List<AgeTransform> TitleBar(
            NotificationWindow window,
            List<Control> controls
        )
        {
            List<AgeTransform> bars = new List<AgeTransform>();
            AgeTransform group = Value(window, TitleGroup) as AgeTransform;
            if (group != null)
            {
                bars.Add(group);
            }

            Holders(bars, controls, TopKeys);
            return bars;
        }

        /// <summary>The row of buttons along the bottom, as the container the game drew them in.
        /// </summary>
        private static List<AgeTransform> ButtonBar(List<Control> controls)
        {
            List<AgeTransform> bars = new List<AgeTransform>();
            Holders(bars, controls, BottomKeys);
            return bars;
        }

        /// <summary>What the game drew these rails inside - which is what marks the strip out on
        /// screen for a control that is not one of them.</summary>
        private static void Holders(
            List<AgeTransform> bars,
            List<Control> controls,
            string[] keys
        )
        {
            foreach (Control control in controls)
            {
                if (Array.IndexOf(keys, control.Key) < 0)
                {
                    continue;
                }

                AgeTransform holder = control.Widget.Parent;
                if (holder != null && !bars.Contains(holder))
                {
                    bars.Add(holder);
                }
            }
        }

        private static bool Within(AgeTransform widget, List<AgeTransform> bars)
        {
            foreach (AgeTransform bar in bars)
            {
                if (AgeWidgets.Under(widget, bar))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Whether a widget is part of the popup's own CONTENT rather than of one of the two strips
        /// the base window draws around it.
        ///
        /// The containers answer it, exactly as <see cref="Sort"/> answers the same question for
        /// controls: the title bar and the button bar are real widgets the game lays its own rails out
        /// inside, and everything a popup adds of its own it adds somewhere else. There used to be a
        /// second, rectangle-based answer here - a widget level with or above the title rails was the
        /// top strip, level with or below the buttons the bottom - and it decided this for the drawn
        /// body, the two table readers and the table containers while <see cref="Sort"/> decided it
        /// for controls. Two models for one question is two chances to disagree, and it is the
        /// rectangles that <see cref="Sort"/>'s own measurement had already ruled out: a survey draws
        /// its party lines level with nothing in particular, and measuring swept all four of them into
        /// the top strip.
        /// </summary>
        private static bool InBody(
            AgeTransform widget,
            List<AgeTransform> title,
            List<AgeTransform> buttons
        )
        {
            return !Within(widget, title) && !Within(widget, buttons);
        }

        /// <summary>One strip of controls, one node per row: the browsing arrows and the pop-up-again
        /// box, or dismissing and putting aside, are peers of one kind and the line the game drew them
        /// on is a rendering accident, so up and down walk the whole strip and nothing is reached
        /// sideways.</summary>
        private static void Strip(GraphBuilder builder, List<Control> controls)
        {
            foreach (Control control in controls)
            {
                Add(builder, control);
            }
        }

        /// <summary>What the words block and anything hanging under it are keyed by.</summary>
        /// <summary>The node the popup's own words are read as. Internal for the same reason the bands
        /// are: the self-check singles this node out by key.</summary>
        internal const string WordsKey = "notification:words";

        private static ControlId WordsId(AgePrimitiveLabel label)
        {
            return ControlId.For(label, WordsKey);
        }

        /// <summary>
        /// What the popup offers on hovering its words.
        ///
        /// A notification whose sentence names a thing offers that thing's dossier on hover - "your
        /// empire now has access to Bluecap Mold" comes with the resource's stat block - and the game
        /// hangs it not on the label but on the BLOCK it drew the label in
        /// (<c>LuxuryDiscoveredNotificationWindow.ResourceTooltip</c> sits on the description group).
        /// So the walk starts at the words and goes up for as long as the container is nothing but the
        /// words' own block (<see cref="Wraps"/>), which is what reaches that group and stops before
        /// the one above it that also holds the picture beside the text.
        ///
        /// A tooltip that only repeats the words is not a second thing to say, the same as anywhere
        /// else on this screen.
        /// </summary>
        private static List<AgeTooltip> WordsTooltips(AgePrimitiveLabel label, string text)
        {
            List<AgeTooltip> kept = new List<AgeTooltip>();
            try
            {
                AgeTransform at = label == null ? null : label.AgeTransform;
                for (int depth = 0; at != null && depth < MaxAncestors; depth++)
                {
                    AgeTooltip tooltip = at.AgeTooltip;
                    if (
                        tooltip != null
                        && !kept.Contains(tooltip)
                        && Explains(tooltip, text) != null
                    )
                    {
                        kept.Add(tooltip);
                    }

                    AgeTransform parent = at.Parent;
                    if (parent == null || !Wraps(parent, at))
                    {
                        break;
                    }

                    at = parent;
                }
            }
            catch (Exception e)
            {
                Log.Warn("notification: looking for the words' explanation threw: " + e);
            }

            return kept;
        }

        /// <summary>Whether the container is drawn as nothing but this widget's own block - its
        /// rectangle the widget's, grown by the margins the widget was laid out with. That is exactly
        /// how the popup family sizes the group it draws its description in, and it is what tells that
        /// group apart from the container above it, which is drawn around the picture as well.
        /// </summary>
        private static bool Wraps(AgeTransform container, AgeTransform widget)
        {
            Rect inner = widget.GetGlobalPosition();
            Rect outer = container.GetGlobalPosition();
            return outer.xMin >= inner.xMin - widget.PixelMarginLeft - Slack
                && outer.yMin >= inner.yMin - widget.PixelMarginTop - Slack
                && outer.xMax <= inner.xMax + widget.PixelMarginRight + Slack
                && outer.yMax <= inner.yMax + widget.PixelMarginBottom + Slack;
        }

        /// <summary>How far a block may miss the widget it was sized to and still be that widget's
        /// block: a pixel of rounding, not a row of anything.</summary>
        private const float Slack = 2f;

        private static void Add(GraphBuilder builder, Control control)
        {
            Control it = control;
            AgeTooltip explains = Tip(it);
            NodeVtable vtable;
            if (it.Toggle == null || it.Acts)
            {
                vtable = GraphNodes.Button(
                    () => Caption(it),
                    () => Press(it),
                    () => AgeWidgets.Operable(it.Widget),
                    explains,
                    it.Drawn
                );
            }
            else if (it.Radio || InRadioGroup(it.Toggle))
            {
                vtable = GraphNodes.Radio(
                    () => Caption(it),
                    () => State(it.Toggle),
                    () => Press(it),
                    () => AgeWidgets.Operable(it.Widget),
                    it.Drawn,
                    explains
                );

                // Picking is not doing: the popup wants the choice CONFIRMED, and where it draws no
                // button for that the game's own second click is what confirms - so the choice takes
                // the double-click chord for it. None of the four handlers it can reach
                // (<c>NarrativeEventBegunNotificationWindow.OnChoiceDoubleClick</c> :322-329,
                // <c>QuestBegunNotificationWindow.OnObjectiveValidated</c> :410-413 and the two
                // contextual-exchange windows' <c>OnChoiceDoubleClick</c>) reads the modifiers the
                // player is still holding while it runs.
                AgeControlToggle again = it.Toggle;
                if (
                    again.UseDoubleClick
                    && again.OnDoubleClickObject != null
                    && !string.IsNullOrEmpty(again.OnDoubleClickMethod)
                )
                {
                    vtable.OnDoubleClick = () => AgeWidgets.DoubleClick(again);
                }
            }
            else
            {
                vtable = GraphNodes.Checkbox(
                    () => Caption(it),
                    () => State(it.Toggle),
                    () => Press(it),
                    () => AgeWidgets.Operable(it.Widget),
                    explains,
                    it.Drawn
                );
            }

            if (it.Details != null)
            {
                vtable.Sections = it.Details;
            }

            if (it.Toggle != null)
            {
                // A control the game drew as a TOGGLE has no button to light up: its own toggle carries
                // the hover - and hovering it is also what makes the game show what a choice card
                // unlocks, exactly as it does for a mouse. Asked of the toggle rather than of what the
                // control DOES with it, because the pop-up-again box and every expander are toggles the
                // mod does not call a card, and they used to hover through the null-button overload:
                // nothing lit, and nothing the game only draws under the pointer appeared.
                AgeWidgets.Point(vtable, it.Toggle, explains, it.Widget);
            }
            else
            {
                vtable.OnFocusVisual = () =>
                    PointerFocus.MoveTo(it.Button, explains, it.Widget);
                vtable.OnBlurVisual = AgeWidgets.ReleasePointer;
                vtable.PointsAt = () => explains;
            }

            HandBackOnMinimize(it, vtable);
            ControlId id = IdOf(it);
            if (it.Dossiers == null || it.Dossiers.Count == 0)
            {
                builder.AddItem(Nodes.Drawn(id, vtable, it.Widget));
                return;
            }

            // A control the popup drew as a card the mouse can hover INSIDE keeps everything it already
            // was - its name, its role, its state, its click and the chord that confirms it - and the
            // pages it draws no words for become nodes under it. The popup declares no actions of its
            // own on a card, so the region handed back is simply the one in force.
            builder.BeginGroup(Nodes.Drawn(id, vtable, it.Widget));
            if (builder.IsExpanded(id))
            {
                TooltipChildren.Emit(builder, "notification:" + it.Key, it.Dossiers, builder.Region);
            }

            builder.EndGroup();
        }

        /// <summary>
        /// Putting the popup aside hands the player back to the icon it came from, not to wherever they
        /// were standing when it arrived.
        ///
        /// A notification pops up on its own - the game raises it, most often on the turn the player has
        /// just ended - so the cursor underneath is on whatever they last touched, and closing the popup
        /// restores it. Measured: minimising put focus back on End Turn, one Enter from ending another
        /// turn. Minimise is the one control here that means "not now": the popup goes to the strip of
        /// icons and stays there, so the strip is where the player is now, and its own stop is what
        /// remembers which icon. Every other exit - Done, Inspect, the buttons that open a page - is going
        /// somewhere, and those keep the landing the page they opened chose.
        ///
        /// WHICH list it goes to is the notification's own answer: the game's own go back to the icon
        /// strip, and the MOD's - which the strip does not draw at all - go back to the turn log they
        /// are read in (<see cref="GlobalHud.TurnLog"/>). Asked of the popup being put aside rather
        /// than remembered from the way in, because Previous/Next walks between the two families
        /// inside one popup and it is the notification the player is looking at now that has a place
        /// to go back to.
        /// </summary>
        private static void HandBackOnMinimize(Control control, NodeVtable vtable)
        {
            if (control.Key != MinimizeKey || vtable.OnActivate == null)
            {
                return;
            }

            Action press = vtable.OnActivate;
            AgeTransform widget = control.Widget;
            vtable.OnActivate = () =>
            {
                // Read before the press: minimising unbinds the popup.
                object stop = ListOf(widget);
                press();
                GraphNavigator navigator = ModEntry.Navigator;
                if (navigator != null)
                {
                    navigator.LandOnStopAfterClose(stop);
                }
            };
        }

        /// <summary>The stop the notification this popup is showing is read on. The popup's own window
        /// is what holds it, and the window is above every control it drew.</summary>
        private static object ListOf(AgeTransform widget)
        {
            try
            {
                NotificationWindow window =
                    widget == null ? null : widget.GetComponentInParent<NotificationWindow>();
                GuiNotification notification = window == null ? null : window.GuiNotification;
                return GlobalHud.Mine(notification) != null
                    ? GlobalHud.TurnLogStop
                    : GlobalHud.NotificationStop;
            }
            catch (Exception e)
            {
                Log.Warn("notification: finding the list a minimized notification goes to threw: " + e);
                return GlobalHud.NotificationStop;
            }
        }

        private const string MinimizeKey = "minimize";

        private static ControlId IdOf(Control control)
        {
            return ControlId.For(control.Widget, "notification:" + control.Key);
        }

        /// <summary>Whether a toggle is one of a set the game lets the player pick exactly one of -
        /// the choice cards a quest offers - rather than a box of its own, like the one that pins the
        /// quest. The game answers this itself: <c>GuiRadioGroup.Load</c> re-points every toggle it
        /// owns at its own object, so a toggle whose switch target carries a <c>GuiRadioGroup</c> is a
        /// member of that group, and one wired to anything else is not.</summary>
        private static bool InRadioGroup(AgeControlToggle toggle)
        {
            try
            {
                return toggle.OnSwitchObject != null
                    && toggle.OnSwitchObject.GetComponent<GuiRadioGroup>() != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static readonly Comparison<Control> ReadingOrder = delegate(Control a, Control b)
        {
            return AgeLayout.ReadingOrder(a.Widget, b.Widget);
        };

        /// <summary>One control of the popup: the widget, how to name it when the game did not, and
        /// - when it is a toggle - the state it carries.</summary>
        internal struct Control
        {
            public string Key;
            public AgeTransform Widget;
            public AgeControlButton Button;
            public AgeControlToggle Toggle;
            public string NameKey;

            /// <summary>The name the GAME has for this control where it wrote none on it - the confirm
            /// button a choice popup draws as a tick.</summary>
            public string Name;

            /// <summary>The action whose chord does this control's job from anywhere on the popup, said
            /// after the name ("Next notification (Alt+Right)"). Only the browsing pair carries one:
            /// they are the controls a player uses over and over, and the key is the whole reason for
            /// declaring the pair a second time.</summary>
            public string ChordAction;

            /// <summary>One of a set the player picks exactly one of, where the popup wired that
            /// exclusivity by hand instead of with a <c>GuiRadioGroup</c>.</summary>
            public bool Radio;

            /// <summary>The card this control is the switch of, where the popup drew the choice as one:
            /// a title over what choosing it would do. Set means "name this by its title and review the
            /// rest" (<see cref="ChoiceName"/>) rather than by everything written on it, and it is the
            /// card - not the switch inside it - that the words are read off where the switch has none.
            /// </summary>
            public AgeTransform Card;

            /// <summary>The tooltip that explains this control, where the popup's own code is what
            /// knows which one that is - a choice card's reason for refusing sits on the CARD and the
            /// switch that refuses is a piece inside it, a suggestion card's dossier is the one it also
            /// carries in the buffer. The control's own tooltip still wins where the game hung one
            /// there (<see cref="Tip(Control)"/>), so naming the same object costs nothing and says
            /// which tooltip the card meant.</summary>
            public AgeTooltip Tip;

            /// <summary>A toggle the game uses as a one-shot ACTION rather than as a setting: clicking a
            /// suggested technology queues it there and then, and the tick it leaves behind is animation.
            /// Announced and worked as a BUTTON, because a state the player is told about is a state they
            /// can expect to change back.</summary>
            public bool Acts;

            /// <summary>What this control carries in the review buffer, where the popup's own code is
            /// what knows. The shared reading takes a control's ONE tooltip; a card assembled out of
            /// several - what branch this is, what the thing does, what it unlocks - is the only place
            /// any of them is reachable from, and which of them is worth saying outright is a question
            /// about the card rather than about any one tooltip. Null leaves the shared reading alone.
            /// </summary>
            public IList<NodeSection> Details;

            /// <summary>The lines this control DRAWS, where the shared naming no longer says them: a
            /// choice card's title names it and the consequences under the title are these. Handed to
            /// the node factory as its <c>details</c> rather than replacing the sections
            /// (<see cref="Details"/>), so the control's tooltip - a card's reason for refusing - still
            /// reads after them.</summary>
            public Func<IList<string>> Drawn;

            /// <summary>The dossiers this control owns BEYOND the one it speaks, which turn it into an
            /// expandable group with a "Tooltips" region under it
            /// (<see cref="TooltipChildren"/>). A choice drawn as a hero card is the case: the card
            /// explains the hero's affinity, class, politics, every mastery and the ship they come with
            /// on hovering each of them, and one node can point at only one. Null everywhere else, and
            /// the control is a leaf.</summary>
            public List<TooltipChildren.Dossier> Dossiers;
        }

        /// <summary>The tooltip a control speaks and carries: its own where it has one, else the one the
        /// game hung on what the control is a piece of.</summary>
        private static AgeTooltip Tip(Control control)
        {
            return control.Widget.AgeTooltip ?? control.Tip;
        }

        /// <summary>
        /// The controls the popup is currently offering: the ones every notification has - dismissing
        /// it, putting it aside, showing where it happened, walking to its neighbours, deciding
        /// whether this kind should interrupt again - and whatever this particular one asks the player
        /// to decide. Found in no particular order, because where they are drawn is what decides
        /// where they are walked.
        ///
        /// <paramref name="own"/> is false for a popup that writes its own body: the skeleton is still the
        /// screen's, and everything the popup added of its own is the body's - declaring both would give
        /// the same button two nodes under two ids.
        /// </summary>
        private static List<Control> Controls(NotificationWindow window, bool own = true)
        {
            List<Control> controls = new List<Control>();
            try
            {
                AgeTransform root = Root(window);
                AgeControlButton dismiss = Button(window, DismissButton);
                AgeControlButton showLocation = Button(window, ShowLocationButton);
                AgeControlButton minimize = Button(window, MinimizeButton);
                AgeControlButton previous = Button(window, PreviousNotificationButton);
                AgeControlButton next = Button(window, NextNotificationButton);
                AgeControlToggle autoPopup = Toggle(window, AutoPopupToggle);

                Add(controls, "dismiss", dismiss, ModStrings.NotifyDismiss);
                List<AgeTransform> choices = own
                    ? ChoiceWidgets(window)
                    : new List<AgeTransform>();
                foreach (
                    AgeControl extra in own ? (IList<AgeControl>)Extras(window) : NoExtras
                )
                {
                    Add(
                        controls,
                        extra.name,
                        extra as AgeControlButton,
                        extra as AgeControlToggle,
                        null,
                        null,
                        In(extra.AgeTransform, choices) != null
                    );
                }

                AddChoices(controls, choices);

                // The cards the popup drew as pictures with their words laid out around them, already
                // named and explained by the code that knows which word is which.
                foreach (Control card in own ? CardControls(window) : NoCards)
                {
                    if (!Has(controls, card.Widget))
                    {
                        controls.Add(card);
                    }
                }

                // The buttons that leave the popup for a page of their own, for a popup that drew one with
                // no words on it. Skipped where the shared rule already found it.
                foreach (Gateway gateway in own ? Gateways(window) : NoGateways)
                {
                    AgeControlButton button = Clickable(gateway.Widget);
                    if (button == null || Has(controls, button.AgeTransform))
                    {
                        continue;
                    }

                    string leads = WordlessName(button.AgeTransform, gateway.NameKey);
                    Add(
                        controls,
                        "gateway/" + gateway.Widget.name,
                        button,
                        null,
                        null,
                        leads,
                        false
                    );
                }

                // The tick that folds a detail panel out and away, for a popup that drew it as a bare
                // "+". Named by what the popup wrote about it, which is only ever its tooltip.
                // The "+" fades ITSELF in the first time a report is shown
                // (<c>DamageReportNotificationWindow.OnEndShow</c> :30-34 makes it visible and starts
                // its modifiers), so it is offered when it is drawn rather than when it is flagged
                // visible - otherwise the popup announces a control the screen is not showing yet.
                foreach (AgeControlToggle expander in own ? Expanders(window) : NoExpanders)
                {
                    if (
                        !Painted(expander.AgeTransform, root)
                        || Has(controls, expander.AgeTransform)
                    )
                    {
                        continue;
                    }

                    string unfolds = WordlessName(expander.AgeTransform, null);
                    Add(
                        controls,
                        "expander/" + expander.name,
                        null,
                        expander,
                        null,
                        unfolds,
                        false
                    );
                }

                // The button that puts the choice into effect, for a popup that drew it as a tick with
                // no words on it: the game has a name for it even where it wrote none there.
                AgeControl confirm = own ? Confirm(window) : null;
                if (confirm != null && !Has(controls, confirm.AgeTransform))
                {
                    Add(
                        controls,
                        "confirm",
                        confirm as AgeControlButton,
                        confirm as AgeControlToggle,
                        null,
                        ConfirmName()
                    );
                }

                // Named with the key that does the same thing from anywhere on the popup, like the
                // browsing pair below it: the chord is the whole reason a player reading the body
                // never has to walk down here (docs/interaction.md).
                AddPaged(
                    controls,
                    "show-location",
                    showLocation,
                    ModStrings.NotifyShowLocation,
                    UiActions.GoToLocation
                );
                Add(controls, MinimizeKey, minimize, ModStrings.NotifyMinimize);
                // The browsing pair, named with the page keys that do the same thing from anywhere on
                // the popup (Screen.PagePrev/PageNext). Ours follow the BUTTONS' own meaning: the
                // game's own Up/Down keys on this window are the other way round
                // (NotificationWindow.HandleInput), and the buttons are what a player can see.
                AddPaged(controls, "previous", previous, ModStrings.NotifyPrevious, UiActions.PagePrev);
                AddPaged(controls, "next", next, ModStrings.NotifyNext, UiActions.PageNext);
                Add(controls, "auto-popup", null, autoPopup, ModStrings.NotifyAutoPopup);

                // Every control, rails included, has to be one the popup is DRAWING. The rails are
                // bound by name from the base window and a prefab that lays none out still answers with
                // one (<see cref="Painted"/>), so this is where a stop that leads nowhere is dropped -
                // asked of the whole list rather than at each Add, because the answer is the same
                // question for all of them.
                for (int i = controls.Count - 1; i >= 0; i--)
                {
                    if (!Painted(controls[i].Widget, root))
                    {
                        controls.RemoveAt(i);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("notification: reading the controls threw: " + e);
            }

            return controls;
        }

        private static void Add(
            List<Control> controls,
            string key,
            AgeControlButton button,
            string nameKey
        )
        {
            Add(controls, key, button, null, nameKey);
        }

        /// <summary>A control that also has a key of its own, said after its name.</summary>
        private static void AddPaged(
            List<Control> controls,
            string key,
            AgeControlButton button,
            string nameKey,
            string actionKey
        )
        {
            int before = controls.Count;
            Add(controls, key, button, null, nameKey);
            if (controls.Count > before)
            {
                Control added = controls[controls.Count - 1];
                added.ChordAction = actionKey;
                controls[controls.Count - 1] = added;
            }
        }

        private static void Add(
            List<Control> controls,
            string key,
            AgeControlButton button,
            AgeControlToggle toggle,
            string nameKey,
            string name = null,
            bool radio = false,
            AgeTooltip tip = null
        )
        {
            AgeControl control = toggle == null ? (AgeControl)button : toggle;
            if (control == null || !AgeWidgets.Visible(control.AgeTransform))
            {
                return;
            }

            controls.Add(
                new Control
                {
                    Key = key,
                    Widget = control.AgeTransform,
                    Button = button,
                    Toggle = toggle,
                    NameKey = nameKey,
                    Name = name,
                    Radio = radio,
                    Tip = tip,
                }
            );
        }

        private static bool Has(List<Control> controls, AgeTransform widget)
        {
            foreach (Control control in controls)
            {
                if (ReferenceEquals(control.Widget, widget))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// What this particular notification added to the shared skeleton. A popup's own answers are
        /// the controls it wired a handler to and wrote a caption on: the caption is what tells them
        /// apart from the invisible click-catchers every popup is built out of - the sheet behind it
        /// that minimises it, the bar it is dragged by, the text area that finishes the typing
        /// animation - none of which is a thing the player chooses.
        /// </summary>
        private static List<AgeControl> Extras(NotificationWindow window)
        {
            List<AgeControl> extras = new List<AgeControl>();
            AgeControl[] declared = Declared(window);
            AgeTransform root = Root(window);
            foreach (AgeControl control in WindowControls.Under(window))
            {
                AgeControlButton button = control as AgeControlButton;
                AgeControlToggle toggle = control as AgeControlToggle;
                bool wired =
                    (button != null && !string.IsNullOrEmpty(button.OnActivateMethod))
                    || (toggle != null && !string.IsNullOrEmpty(toggle.OnSwitchMethod));
                if (
                    !wired
                    || !Painted(control.AgeTransform, root)
                    || string.IsNullOrEmpty(Captioned(control.AgeTransform))
                    || Array.IndexOf(declared, control) >= 0
                )
                {
                    continue;
                }

                extras.Add(control);
            }

            return extras;
        }

        /// <summary>What a popup that owns its own body adds to the skeleton, as far as the shared
        /// reading is concerned: nothing.</summary>
        private static readonly AgeControl[] NoExtras = new AgeControl[0];

        private static AgeControl[] Declared(NotificationWindow window)
        {
            return new AgeControl[]
            {
                Button(window, ModalButton),
                Button(window, DismissButton),
                Button(window, ShowLocationButton),
                Button(window, MinimizeButton),
                Button(window, PreviousNotificationButton),
                Button(window, NextNotificationButton),
                Toggle(window, AutoPopupToggle),
            };
        }

    }
}
