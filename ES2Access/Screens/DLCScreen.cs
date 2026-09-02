using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// The downloadable-content browser (<c>DLCModalWindow</c>): three tabs over a list of content, and
    /// Apply.
    ///
    /// The tabs are the game's own one-of-N (<c>TabsToggleGroup</c>) and each switches which KIND of
    /// content the list holds - expansions, add-ons, updates - so they are tabs and the list follows the
    /// one that is selected, which is what the eye does with it. Switching is the game's own click and is
    /// remembered for the session (<c>CurrentTabType</c> outlives a close), so a test that switches one puts
    /// it back.
    ///
    /// A row is read as the content's name and whether it is YOURS, and it says its own description as it
    /// is read - the paragraph the eye reads and the words a player is deciding on, short enough to hear
    /// in full and still steppable in the review buffer afterwards. What KIND of content it is goes unsaid:
    /// the tab the list is following has already named it, and every row on a tab is that same kind.
    ///
    /// Where the content is owned, the row IS the tick that activates it - one checkbox saying the name,
    /// the word owned, and its own on or off - rather than a readout followed by a second cell called
    /// "Activated", which made the player step twice over one line. Rows the game draws no tick on -
    /// content you do not own, and the whole Updates tab - stay plain readouts, and the store button the
    /// game draws below one of them is its own stop as before.
    ///
    /// Ownership is read off WHICH CONTROL the game drew rather than out of the accessibility flags behind
    /// it (<c>DLCItem.Refresh</c>): content you have shows a tick that activates it, content you do not
    /// shows a button to the store instead, and content the question does not apply to shows neither -
    /// which is what the whole Updates tab is. So the row says "owned" exactly where a tick is drawn and
    /// "not owned" exactly where a store button is, and says nothing where the game itself says nothing.
    /// Both words are the mod's, because the game expresses the state as the SHAPE of the row and nowhere
    /// in words; nothing else on the row is invented.
    ///
    /// The store button and the activation tick are declared but nothing here presses either: the button
    /// opens Steam's overlay or a browser at the store page (<c>OnBuyCb</c>), and the tick is what Apply
    /// then writes into the registry. Apply itself is the game's, refusing with its own sentence until
    /// something has changed.
    ///
    /// Escape: <c>HandleInput</c> hides the window, or raises the game's own confirmation box first if the
    /// player has changed something ("%DLCModalWindowExitConfirmation").
    /// </summary>
    public sealed class DLCScreen : MenuDestinationScreen
    {
        private static readonly object TabsStop = "dlc:tabs";
        private static readonly object ItemsStop = "dlc:items";
        private static readonly object ActionsStop = "dlc:actions";

        private readonly List<Cell> _cells = new List<Cell>();

        // Reused rather than allocated per frame: Build runs every tick.
        private readonly List<AgeTransform> _buttons = new List<AgeTransform>();

        public override string Key
        {
            get { return ModStrings.ScreenDlc; }
        }

        protected override string Prefix
        {
            get { return "dlc"; }
        }

        protected override string ScreenNameKey
        {
            get { return ModStrings.ScreenDlc; }
        }

        /// <summary>The tabs, because they decide what the rest of the page is.</summary>
        public override object InitialFocusStop
        {
            get { return TabsStop; }
        }

        protected override GuiWindow Window()
        {
            return Get<DLCModalWindow>();
        }

        public override void Build(GraphBuilder builder)
        {
            DLCModalWindow window = Window() as DLCModalWindow;
            if (window == null)
            {
                return;
            }

            builder.BeginStop(TabsStop);
            AddTitle(builder);
            Tabs(builder, window);

            builder.BeginStop(ItemsStop);

            // Which of the three kinds is being listed, in the game's own tab word: the list itself
            // says nothing about it, so a player who tabbed straight past the bar would be reading an
            // unnamed list. Spoken once, on the way in.
            bool named = Captions.Push(builder, null, null, SelectedTab(window));

            Items(builder, window);
            Captions.Pop(builder, named);

            builder.BeginStop(ActionsStop);
            Buttons(builder, window);
        }

        /// <summary>Cancel and Apply, on the one row they are drawn in. Taken from the band they share
        /// rather than by scanning the window for controls: the shape floor's scan reaches the pooled item
        /// rows too, and while the game is instantiating a row for a tab that needs more of them, the new
        /// widgets have no parent yet - so the scan cannot tell they are inside the list, keys two of them
        /// the same and throws a duplicate id, which empties the whole page for that frame (measured:
        /// "dlc/BuyButton/3" twice while switching tabs).</summary>
        private void Buttons(GraphBuilder builder, DLCModalWindow window)
        {
            _buttons.Clear();
            AgeTransform band = Band(window);
            IList<AgeTransform> children = band == null ? null : band.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                _buttons.Add(children[i]);
            }

            SettingRows.AddButtons(builder, _buttons, "dlc:button/");
        }

        private static AgeTransform Band(DLCModalWindow window)
        {
            try
            {
                AgeTransform apply = AgeWidgets.Transform(window.ApplyButton);
                return apply == null ? null : apply.Parent;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The tab the window is showing, in the words it is drawn with.</summary>
        private static string SelectedTab(DLCModalWindow window)
        {
            try
            {
                AgeTransform table = Toggles(window);
                IList<AgeTransform> children = table == null ? null : table.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    AgeControlToggle toggle = children[i].GetComponent<AgeControlToggle>();
                    // Content: which of the tab words names the window's current selection.
                    if (toggle != null && toggle.State && AgeWidgets.Visible(children[i]))
                    {
                        return AgeWidgets.TextOf(children[i]);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("dlc: reading the selected tab threw: " + e);
            }

            return null;
        }

        /// <summary>The three kinds of content, as the one-of-N the game made them.</summary>
        private void Tabs(GraphBuilder builder, DLCModalWindow window)
        {
            _cells.Clear();
            try
            {
                AgeTransform table = Toggles(window);
                IList<AgeTransform> children = table == null ? null : table.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    Tab(children[i], i);
                }
            }
            catch (Exception e)
            {
                Log.Warn("dlc: reading the tabs threw: " + e);
            }

            Cells.EmitLinear(builder, _cells);
        }

        private void Tab(AgeTransform widget, int index)
        {
            AgeControlToggle toggle = widget == null ? null : widget.GetComponent<AgeControlToggle>();
            // Banding input: Cells.Add does not ask the gate (it answers with the cell it appended),
            // so a tab the window is not drawing would still bring its rectangle to the banding.
            if (toggle == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeControlToggle it = toggle;
            AgeTransform at = widget;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            NodeVtable vtable = GraphNodes.Tab(
                () => AgeWidgets.TextOf(at),
                () => it.State,
                () => AgeWidgets.Offered(at),
                tooltip
            );
            vtable.OnActivate = () => AgeWidgets.Toggle(it);
            AgeWidgets.Point(vtable, it, tooltip, at);
            Cells.Add(_cells, widget, ControlId.Structural("dlc:tab/" + index), vtable);
        }

        /// <summary>The content the selected tab holds, one row each, or the game's own words for a tab
        /// with nothing in it.</summary>
        private void Items(GraphBuilder builder, DLCModalWindow window)
        {
            _cells.Clear();
            try
            {
                // A line rather than an empty stop: the player has to be able to land on the answer,
                // and the game has already written it. Whether the game is drawing that answer is the
                // gate's question, which AddReadout asks of this same widget.
                Cells.AddReadout(_cells, window.NoItemsLabel, "dlc:empty");

                AgeTransform table = window.DLCItemsTable;
                IList<AgeTransform> children = table == null ? null : table.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    Item(children[i], i);
                }
            }
            catch (Exception e)
            {
                Log.Warn("dlc: reading the content list threw: " + e);
            }

            Cells.EmitLinear(builder, _cells);
        }

        /// <summary>One piece of content: the row itself where the game's tick makes it a checkbox, a
        /// readout where it does not, and the store button the game may draw beside it.</summary>
        private void Item(AgeTransform widget, int index)
        {
            DLCItem item = widget == null ? null : widget.GetComponent<DLCItem>();
            // Painted, not merely visible: this is a pooled table and it never shrinks - switching to a
            // tab with fewer items leaves the surplus rows behind at alpha zero, still holding the
            // previous tab's content (measured: six Updates followed by six retired add-ons).
            if (item == null || !AgeWidgets.Painted(widget))
            {
                return;
            }

            string key = "dlc:item/" + Named(item, index);
            DLCItem it = item;
            AgePrimitiveLabel description = item.DescriptionLabel;
            Func<IList<string>> details = () => AgeText.Lines(AgeText.Label(description));

            AgeTransform activate = AgeWidgets.Transform(item.ActivateToggle);
            if (AgeWidgets.Visible(activate))
            {
                AgeControlToggle box = item.ActivateToggle;
                AgeTransform at = activate;
                AgeTooltip tooltip = AgeWidgets.Raw(activate);
                NodeVtable tick = GraphNodes.Checkbox(
                    () => AgeText.Label(it.TitleLabel),
                    () => box.State,
                    () => AgeWidgets.Toggle(box),
                    () => AgeWidgets.Offered(at),
                    tooltip,
                    null,
                    () => ModStrings.Get(ModStrings.DlcOwned)
                );
                // Said as the row is read rather than left to the buffer: the description is one short
                // paragraph and it is the whole of what the player is deciding on.
                tick.Sections = GraphNodes.SpokenSections(details, tooltip);
                GraphNodes.AddRefusal(tick, tooltip, () => AgeWidgets.Offered(at));
                AgeWidgets.Point(tick, box, tooltip, at);
                Cells.Add(_cells, widget, ControlId.Structural(key), tick);
            }
            else
            {
                NodeVtable vtable = new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.LabelPart(() => AgeText.Label(it.TitleLabel)),
                        GraphNodes.ValuePart(() => Ownership(it)),
                    },
                    Sections = GraphNodes.SpokenSections(details, null),
                };
                AgeWidgets.PointAt(vtable, widget);
                Cells.Add(_cells, widget, ControlId.Structural(key), vtable);
            }

            // The store button carries no caption - it is named by the sentence the game explains it
            // with, which is the shared reading of a wordless control.
            Cells.AddControl(_cells, AgeWidgets.Transform(item.BuyButton), key + "/buy");
        }

        /// <summary>Whether this content is the player's, in the mod's words - said only where the game
        /// drew the control that answers it.</summary>
        private static string Ownership(DLCItem item)
        {
            try
            {
                // Content: which of the two words the item is called owned by, or neither. The controls
                // asked about are the tick and the store button, not the item row the node stands on.
                if (AgeWidgets.Visible(AgeWidgets.Transform(item.ActivateToggle)))
                {
                    return ModStrings.Get(ModStrings.DlcOwned);
                }

                return AgeWidgets.Visible(AgeWidgets.Transform(item.BuyButton))
                    ? ModStrings.Get(ModStrings.DlcNotOwned)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What keys a row: the content's own identifier, which survives the table pooling its
        /// rows and rebinding them to another tab's content. Its position is the fallback.</summary>
        private static string Named(DLCItem item, int index)
        {
            try
            {
                GuiDLC dlc = item.GuiDLC;
                string name = dlc == null || dlc.Name == null ? null : dlc.Name.ToString();
                return string.IsNullOrEmpty(name) ? index.ToString() : name;
            }
            catch (Exception)
            {
                return index.ToString();
            }
        }

        private static AgeTransform Toggles(DLCModalWindow window)
        {
            try
            {
                GuiRadioGroup group = window.TabsToggleGroup;
                return group == null ? null : group.TogglesTable;
            }
            catch (Exception)
            {
                return null;
            }
        }

    }
}
