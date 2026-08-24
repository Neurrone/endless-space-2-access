using System;
using System.Collections.Generic;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// What a window's SHAPE says about it, for a page the mod has no model of yet.
    ///
    /// A real model of a screen says what its parts mean - which rows are a table, which set is a
    /// one-of-N, what a picture is worth. That work is per screen and cannot be shared. What CAN be
    /// shared is the floor under it: the heading the window drew, and the controls it drew with words
    /// on them. That is enough to make a menu destination stop being a silent dead end - the player
    /// arrives, hears where they are, and can reach the buttons - and it is deliberately not enough to
    /// call the screen finished.
    ///
    /// The rule for what counts as a control is the notification screen's, which was measured against
    /// sixty popups: a control the window WIRED a handler to and WROTE a caption on. The caption is what
    /// tells a real button from the invisible click-catchers every AGE window is built out of - the
    /// sheet behind it that dismisses it, the bar it is dragged by, the area that finishes a typing
    /// animation - none of which is a thing the player chooses. A control the game named only in its
    /// tooltip is still declared, named by that sentence (<see cref="Cells.Control"/>).
    ///
    /// Everything is emitted through <see cref="Cells"/>, so the buttons are walked in the rows the
    /// window drew them in rather than in the order the component scan happened to find them.
    /// </summary>
    public static class WindowShape
    {
        /// <summary>How far down from the window root to look for a heading. The prefabs put it in a
        /// title group a level or two under the main container.</summary>
        private const int TitleDepth = 4;

        /// <summary>The names the game's own prefabs give the band a window writes its heading in, in
        /// the order they are tried: the label itself where there is one, else the group holding it and
        /// whatever else is drawn beside it.</summary>
        private static readonly string[] TitleNames = { "WindowTitle", "TitleLabel", "TitleGroup" };

        /// <summary>The heading this window has written across its top, or null where it drew none.
        /// Found where it is DRAWN rather than in a field, because a window class overwhelmingly
        /// exposes its contents and not its own title.
        ///
        /// <paramref name="alsoNamed"/> is for a FAMILY of windows whose prefabs agree on a name of their
        /// own - the out-game pages write their heading in a "WindowTitleLabel" - tried after the shared
        /// names so that adding one can never rename a window that already answers.</summary>
        public static string Title(GuiWindow window, string[] alsoNamed = null)
        {
            return AgeWidgets.TextOf(TitleWidget(window, alsoNamed));
        }

        /// <summary>The widget that heading is written in - the same search, answered as the thing
        /// rather than as its words, for a page that has to declare the heading as a node: a window
        /// title the game hung an explanation on has nowhere else to put those words, since a screen's
        /// spoken name is a phrase with no review buffer behind it (owner ruling, see
        /// <see cref="Captions"/>).</summary>
        public static AgeTransform TitleWidget(GuiWindow window, string[] alsoNamed = null)
        {
            try
            {
                AgeTransform root = window == null ? null : window.AgeTransform;
                if (root == null)
                {
                    return null;
                }

                for (int i = 0; i < TitleNames.Length; i++)
                {
                    AgeTransform found = AgeWidgets.ChildNamed(root, TitleNames[i], TitleDepth);
                    if (!string.IsNullOrEmpty(AgeWidgets.TextOf(found)))
                    {
                        return found;
                    }
                }

                for (int i = 0; alsoNamed != null && i < alsoNamed.Length; i++)
                {
                    AgeTransform found = AgeWidgets.ChildNamed(root, alsoNamed[i], TitleDepth);
                    if (!string.IsNullOrEmpty(AgeWidgets.TextOf(found)))
                    {
                        return found;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("window shape: reading the heading threw: " + e);
            }

            return null;
        }

        /// <summary>Every control the window is drawing that the player could choose, gathered into
        /// <paramref name="cells"/> for the caller to emit where its own reading puts them.
        ///
        /// <paramref name="except"/> is for a control the CALLER has modelled - or has decided the
        /// keyboard does not need, like a viewport's paging arrows - so that the shared reading does not
        /// declare it a second time under a name of its own.</summary>
        public static void Controls(
            List<Cell> cells,
            GuiWindow window,
            string prefix,
            params AgeTransform[] except
        )
        {
            try
            {
                if (window == null)
                {
                    return;
                }

                foreach (
                    AgeControl control in window.gameObject.GetComponentsInChildren<AgeControl>(true)
                )
                {
                    if (!Excluded(control, except))
                    {
                        Add(cells, control, prefix);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("window shape: reading the controls threw: " + e);
            }
        }

        /// <summary>The handler every one of these prefabs wires its own dismissal to.</summary>
        private const string CloseHandler = "OnCloseCb";

        /// <summary>
        /// The button a window draws to close itself, as a node of its own.
        ///
        /// A modal that a mod screen closes with Escape still DRAWS a cross, and a player walking the
        /// page has every reason to expect to reach it - a way out is a thing a page offers, not a
        /// keystroke to be remembered. None of these windows exposes the button as a field, so it is
        /// found the way the Academy's switch button is: by the handler the prefab wires it to, which
        /// is also the only thing that identifies a widget drawn as a bare cross. Enter presses that
        /// handler, so the way out is the game's own, and Escape is left exactly as it was.
        ///
        /// Named by whatever the game wrote on it - the caption where the prefab drew one, else the
        /// first line of the sentence it hung on the cross ("Closes this panel"), whose remaining lines
        /// stay in the review buffer rather than being announced twice.
        /// </summary>
        public static void Close(GraphBuilder builder, GuiWindow window, string keyPrefix)
        {
            try
            {
                AgeTransform at = AgeWidgets.Transform(Wired(window, CloseHandler));
                if (at == null || !AgeWidgets.Visible(at))
                {
                    return;
                }

                AgeTooltip tooltip = AgeWidgets.Raw(at);
                string caption = AgeWidgets.TextOf(at);
                AgeTransform it = at;
                NodeVtable vtable = GraphNodes.Button(
                    string.IsNullOrEmpty(caption)
                        ? CardActions.NameFromTooltip(tooltip)
                        : () => AgeWidgets.TextOf(it),
                    () => AgeWidgets.Press(it),
                    () => AgeWidgets.Offered(it),
                    tooltip,
                    string.IsNullOrEmpty(caption) ? TooltipMode.None : (TooltipMode?)null
                );
                AgeWidgets.PointAt(vtable, at);
                builder.AddItem(ControlId.Referenced(at, keyPrefix + "close"), vtable);
            }
            catch (Exception e)
            {
                Log.Warn("window shape: reading the close button threw: " + e);
            }
        }

        /// <summary>The button under a window that the prefab wired to <paramref name="method"/>.
        /// </summary>
        private static AgeControlButton Wired(GuiWindow window, string method)
        {
            AgeTransform root = window == null ? null : window.AgeTransform;
            AgeControlButton[] buttons =
                root == null ? null : root.GetComponentsInChildren<AgeControlButton>(true);
            for (int i = 0; buttons != null && i < buttons.Length; i++)
            {
                if (buttons[i] != null && buttons[i].OnActivateMethod == method)
                {
                    return buttons[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Every line the window has WRITTEN under <paramref name="root"/>, as something to read.
        ///
        /// The other half of a shape reading: a page whose content the mod has no model of is still a page
        /// the game wrote words on, and those words are what the player came for. Each visible label is one
        /// readout, so two drawn side by side are two facts - walked one after the other, because a page
        /// nobody has modelled has no columns to preserve - and a paragraph the game wrapped is one node
        /// holding its own wrapping. This fills a caller-owned list and belongs to no host's layout:
        /// which emitter the cells go out through is the host's own call, and the default there is
        /// <see cref="Cells.EmitLinear"/>.
        ///
        /// A hidden branch is skipped rather than read: these windows keep a block per case and hide the
        /// ones that do not apply. <paramref name="maxDepth"/> bounds the walk, which is also what bounds
        /// the cost - a page that draws hundreds of lines declares hundreds of nodes, and a caller with
        /// such a page should be reading a model of it instead.
        ///
        /// Each line is keyed by WHERE it was drawn - its position under each parent between it and the
        /// root - and not by its name. Measured on the score screen, which is the page with the most of
        /// them: a panel of scores is rows of clones and every label in it is called "Label", so a name
        /// key made two lines the same control and the duplicate emptied the WHOLE page (the builder
        /// throws, the screen declares nothing). A page laid out as clones is the normal case for this
        /// reader, so the key has to be unique by construction.
        /// </summary>
        public static void Readouts(
            List<Cell> cells,
            AgeTransform root,
            string prefix,
            int maxDepth = 8
        )
        {
            try
            {
                Lines(cells, root, prefix, maxDepth, 0, string.Empty);
            }
            catch (Exception e)
            {
                Log.Warn("window shape: reading the lines threw: " + e);
            }
        }

        private static void Lines(
            List<Cell> cells,
            AgeTransform widget,
            string prefix,
            int maxDepth,
            int depth,
            string path
        )
        {
            if (widget == null || depth > maxDepth || !widget.Visible)
            {
                return;
            }

            AgePrimitiveLabel label = widget.GetComponent<AgePrimitiveLabel>();
            if (
                label != null
                && !string.IsNullOrEmpty(AgeText.Label(label))
                && AgeWidgets.Control(widget) == null
                && AgeWidgets.ParentControl(widget) == null
            )
            {
                // Only a line that is not part of a CONTROL: the caption on a button is that button's
                // name, and reading it here as well would say it twice - once as the button and once as a
                // line of prose beside it. This is what makes the two halves of a shape reading safe to
                // use together on the same window.
                // Keyed on the LABEL's own transform (through Referenced) and on where it is drawn, with
                // its name only as a readable suffix: a position in the collected list would move under
                // the cursor the moment a line above it appeared or went, and the name alone is not
                // unique on a page built out of clones.
                cells.Add(
                    Cells.Readout(
                        widget,
                        AgeWidgets.Raw(widget),
                        prefix + "/line" + path + "/" + widget.name
                    )
                );
            }

            List<AgeTransform> children = widget.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                Lines(cells, children[i], prefix, maxDepth, depth + 1, path + "/" + i);
            }
        }

        /// <summary>Whether this control is one the caller has already dealt with - itself, or anything
        /// inside it, since a caller names the button and the game hangs the control on a child of
        /// it.</summary>
        private static bool Excluded(AgeControl control, AgeTransform[] except)
        {
            if (except == null || except.Length == 0)
            {
                return false;
            }

            AgeTransform widget = control.AgeTransform;
            for (int i = 0; i < except.Length; i++)
            {
                if (except[i] == null)
                {
                    continue;
                }

                if (ReferenceEquals(except[i], widget) || IsUnder(widget, except[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsUnder(AgeTransform widget, AgeTransform ancestor)
        {
            int depth = 0;
            for (
                AgeTransform at = widget == null ? null : widget.Parent;
                at != null && depth++ < MaxAncestors;
                at = at.Parent
            )
            {
                if (ReferenceEquals(at, ancestor))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>How far up a parent chain to look before deciding it is not a chain.</summary>
        private const int MaxAncestors = 32;

        private static void Add(List<Cell> cells, AgeControl control, string prefix)
        {
            AgeControlButton button = control as AgeControlButton;
            AgeControlToggle toggle = control as AgeControlToggle;
            bool wired =
                (button != null && !string.IsNullOrEmpty(button.OnActivateMethod))
                || (toggle != null && !string.IsNullOrEmpty(toggle.OnSwitchMethod));
            AgeTransform widget = control.AgeTransform;
            if (!wired || !AgeWidgets.Visible(widget))
            {
                return;
            }

            string caption = AgeWidgets.TextOf(widget);
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            if (string.IsNullOrEmpty(caption) && string.IsNullOrEmpty(CardActions.FirstLine(tooltip)))
            {
                // Neither words on it nor a sentence about it: a click-catcher, not a control.
                return;
            }

            // Index-in-parent joins the name because a scraped window can draw the same
            // pooled prefab several times over ("BuyButton" four times on the DLC page),
            // and a name-only key throws Duplicate control id, which empties the WHOLE
            // page - the repeated-node rule, applied here because no screen author ever
            // sees the keys a scraper makes.
            string key = prefix + "/" + widget.name + "/" + AgeWidgets.IndexInParent(widget);
            if (toggle == null)
            {
                cells.Add(Cells.Control(widget, button, tooltip, caption, key));
                return;
            }

            AgeControlToggle it = toggle;
            Func<string> label = string.IsNullOrEmpty(caption)
                ? CardActions.NameFromTooltip(tooltip)
                : () => AgeWidgets.TextOf(widget);
            NodeVtable vtable = InRadioGroup(toggle)
                ? GraphNodes.Radio(
                    label,
                    () => it.State,
                    () => AgeWidgets.Toggle(it),
                    () => AgeWidgets.Offered(widget),
                    null,
                    tooltip
                )
                : GraphNodes.Checkbox(
                    label,
                    () => it.State,
                    () => AgeWidgets.Toggle(it),
                    () => AgeWidgets.Offered(widget),
                    tooltip
                );
            AgeWidgets.Point(vtable, it, tooltip, widget);
            Cells.Add(cells, widget, ControlId.Referenced(toggle, key), vtable);
        }

        /// <summary>Whether this toggle is one of a set the game lets the player pick exactly one of
        /// rather than a box of its own. The game answers it itself: <c>GuiRadioGroup.Load</c>
        /// re-points every toggle it owns at its own object, so a toggle whose switch target carries a
        /// group is a member of it.</summary>
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

    }
}
