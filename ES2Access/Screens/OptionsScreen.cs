using System;
using System.Collections.Generic;
using System.Reflection;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using ES2Access.UI.ModOptions;

namespace ES2Access.Screens
{
    /// <summary>
    /// The game's options window, made navigable. One window serves both the main menu and a game in
    /// progress, so making it navigable once covers every route to it.
    ///
    /// Three places to be, and Tab moves between them: the row of category tabs along the top, the
    /// settings of the category currently showing, and the buttons along the bottom. Nothing else on
    /// the window is interactive.
    ///
    /// The tab bar switches ON FOCUS rather than on Enter. A tab whose page you cannot see is not a
    /// thing a player wants to stand on, and the game's own tab switch is instant and free of
    /// consequence - it neither applies nor discards anything - so arriving at a tab and arriving at
    /// its page are the same event. Enter does it too, for the player who expects to have to ask.
    ///
    /// Every row is read from the live panel on each build. The settings themselves rebuild whenever
    /// the category changes, and the game keeps each row's availability current on its own (the
    /// resolution list refuses while a borderless change is pending), so a row that is refusing stays
    /// in the list and says so rather than disappearing out from under the cursor.
    ///
    /// Each control is worked through the same handler the mouse reaches: the control's state is set
    /// and then the item's own click callback is run, which is what tells the window something
    /// changed - and therefore what makes Apply available. The Controls page goes one step further
    /// and hands the game the keyboard outright while a new key combination is being pressed; see
    /// StartCapture.
    ///
    /// Escape is left to the game. Leaving with unapplied changes is a question the game asks in its
    /// own confirmation box, which is a screen of ours already - as is the fifteen seconds it gives
    /// you to keep a display mode. Answering no to that one puts the settings back AND closes the
    /// whole window, which is the game's own doing: this screen simply stops being active, and the
    /// player lands back on the page they opened it from.
    /// </summary>
    public sealed partial class OptionsScreen : Screen
    {
        private static readonly object TabStop = "options:tabs";
        internal static readonly object RowStop = "options:rows";
        private static readonly object ButtonStop = "options:buttons";

        /// <summary>How many fine steps one coarse slider step is worth.</summary>
        private const int CoarseSteps = 10;

        /// <summary>What <c>AgeControlSlider</c> renders when it was given no format of its own.
        /// </summary>
        private const string DefaultValueFormat = "######0";

        public override string Key
        {
            get { return ModStrings.ScreenOptions; }
        }

        /// <summary>
        /// Above every page that opens it, below the drop list it opens and the confirmation box it
        /// raises.
        ///
        /// One number, above the highest of the pages that can open this window - the pause menu, at
        /// 50 - rather than a number that depends on which of them did. The window is drawn over the
        /// pause menu rather than instead of it (<c>GameMenuModalWindow</c> stays <c>Shown</c>
        /// underneath), so it was tempting to read the game's own layering and report one above
        /// whatever was found. That is a layer that changes while the screen is up, and a layer is
        /// what the drop list and the message box are placed relative to: a screen that can move
        /// underneath them is a screen they cannot be reliably placed above.
        /// </summary>
        public override int Layer
        {
            get { return 52; }
        }

        /// <summary>The game's options, or the mod's own - the two are the same window class and a
        /// player arriving at the mod's settings must not be told they are in the game's.</summary>
        public override string ScreenName
        {
            get
            {
                return ModStrings.Get(
                    ES2Access.UI.ModOptions.ModOptions.IsOurs(Window())
                        ? ModStrings.ScreenModSettings
                        : ModStrings.ScreenOptions
                );
            }
        }

        /// <summary>Ours while the window is up and has finished animating in. Deliberately not
        /// conditioned on no modal being visible: this window IS the modal. A confirmation raised over
        /// it covers it by layer instead, and a covered screen keeps its cursor, so answering the
        /// question returns the player to the control they were on.</summary>
        public override bool IsActive()
        {
            OptionsModalWindow window = Window();
            try
            {
                return window != null && window.Shown && window.IsReady;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Escape belongs to the game: its own exit route is what raises the "you have not
        /// applied your changes" question, and that question is a screen of ours.</summary>
        public override bool Back()
        {
            return false;
        }

        public override void OnUpdate()
        {
            HandOverWhenReleased();
            WatchForTheEndOfACapture();
            _editor.Update();
        }

        /// <summary>The one text editor this page can have running - a category's name, one of its
        /// keywords. Per screen, because the engine has one focused control at a time.</summary>
        private readonly TextFieldEditor _editor = new TextFieldEditor();

        /// <summary>A binding row is listening, or about to be: every key belongs to the field being
        /// bound, and a letter that started a search instead would be a key the player could never
        /// bind.</summary>
        public override bool CapturesRawInput
        {
            get { return _pending != null || _capturing != null || _editor.Pending; }
        }

        /// <summary>Something else has the player's attention - a confirmation, the drop list, or the
        /// window has gone. A capture that was asked for and not yet handed over is abandoned rather
        /// than left armed to fire under whatever comes next.</summary>
        public override void OnUnfocus()
        {
            CancelPending();
            _editor.Cancel();
        }

        public override void Build(GraphBuilder builder)
        {
            OptionsModalWindow window = Window();
            if (window == null)
            {
                return;
            }

            builder.BeginStop(TabStop);
            BuildTabs(builder, window);

            builder.BeginStop(RowStop);
            BuildRows(builder, SelectedCategory(window));

            builder.BeginStop(ButtonStop);
            BuildButtons(builder, window);
        }

        // ---- the category tabs ----

        private static void BuildTabs(GraphBuilder builder, OptionsModalWindow window)
        {
            GuiRadioGroup group = window.RadioGroup;
            if (group == null || group.TogglesTable == null)
            {
                return;
            }

            List<OptionsTabToggle> tabs = Tabs(group);
            if (tabs.Count == 0)
            {
                return;
            }

            foreach (OptionsTabToggle tab in tabs)
            {
                OptionsTabToggle entry = tab;
                AgeTooltip tooltip = AgeWidgets.Raw(entry.Toggle.AgeTransform);
                NodeVtable vtable = GraphNodes.Tab(
                    () => AgeText.Label(entry.TitleLabel),
                    () => Selected(entry),
                    () => AgeWidgets.Operable(entry.Toggle.AgeTransform),
                    tooltip
                );
                // Focusing the tab IS switching to it. The hook runs once per focus change, after the
                // readout has been composed, so the page changes without the tab announcing itself
                // twice; the guard inside makes a re-focus of the showing tab a no-op.
                vtable.OnFocusVisual = () =>
                {
                    Switch(entry);
                    PointerFocus.MoveTo(null, tooltip, AgeWidgets.Transform(entry.TitleLabel));
                };
                vtable.OnBlurVisual = ReleasePointer;
                vtable.OnActivate = () => Switch(entry);

                builder.AddItem(Nodes.Drawn(
                    ControlId.For(entry, "options:tab/" + CategoryOf(entry)),
                    vtable,
                    entry
                ));
            }
        }

        /// <summary>The category the window is showing, in the words its tab is drawn with - the same
        /// label the tab itself is named from. It is what the rows below belong to, and the player who
        /// tabbed straight past the bar has otherwise no way to hear which page they are on.</summary>
        private static string SelectedCategory(OptionsModalWindow window)
        {
            GuiRadioGroup group = window == null ? null : window.RadioGroup;
            if (group == null || group.TogglesTable == null)
            {
                return null;
            }

            foreach (OptionsTabToggle tab in Tabs(group))
            {
                if (Selected(tab))
                {
                    return AgeText.Label(tab.TitleLabel);
                }
            }

            return null;
        }

        /// <summary>Switch to a category the way clicking its tab does. The radio group's own callback
        /// is what a click reaches: it moves the selection - the tick, the underline - and tells the
        /// window to open the page. Already-showing tabs are left alone, so re-entering the bar does
        /// not restart the page's animation.</summary>
        private static void Switch(OptionsTabToggle tab)
        {
            try
            {
                OptionsModalWindow window = Window();
                if (window == null || window.RadioGroup == null || Selected(tab))
                {
                    return;
                }

                window.RadioGroup.OnToggleSwitchCb(tab.Toggle.gameObject);
            }
            catch (Exception e)
            {
                Log.Warn("options: switching category threw: " + e);
            }
        }

        private static List<OptionsTabToggle> Tabs(GuiRadioGroup group)
        {
            List<OptionsTabToggle> tabs = new List<OptionsTabToggle>();
            try
            {
                foreach (
                    OptionsTabToggle tab in group.TogglesTable.GetChildren<OptionsTabToggle>(false)
                )
                {
                    // Flow control: the kept tabs are what the screen walks, and the page under the
                    // ticked one is read from this list rather than from the table.
                    if (tab != null && tab.Toggle != null && AgeWidgets.Visible(tab.Toggle.AgeTransform))
                    {
                        tabs.Add(tab);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("options: reading the category tabs threw: " + e);
            }

            return tabs;
        }

        private static bool Selected(OptionsTabToggle tab)
        {
            try
            {
                return tab != null && tab.Toggle != null && tab.Toggle.State;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string CategoryOf(OptionsTabToggle tab)
        {
            try
            {
                return string.IsNullOrEmpty(tab.CategoryName) ? tab.name : tab.CategoryName;
            }
            catch (Exception)
            {
                return "?";
            }
        }

        // ---- the buttons along the bottom ----

        /// <summary>
        /// The bar along the bottom - <see cref="SettingRows.ButtonBar"/>'s reading of it, with the
        /// one thing that is this window's own: a button the MOD put in a rows table is a ROW, not
        /// part of the bar, and is read where it is drawn among the settings it belongs to.
        /// </summary>
        private void BuildButtons(GraphBuilder builder, OptionsModalWindow window)
        {
            List<AgeControlButton> commands = _bar.Drawn(window, NotARow);
            if (commands.Count == 0)
            {
                return;
            }

            HashSet<string> taken = new HashSet<string>();
            foreach (AgeControlButton entry in commands)
            {
                AgeControlButton command = entry;
                AgePrimitiveLabel caption = LabelIn(command.AgeTransform);
                AgeTooltip tooltip = AgeWidgets.Raw(command.AgeTransform);
                AgeTransform available = EnableFlagOf(window, command);
                NodeVtable vtable = GraphNodes.Button(
                    () => ButtonText(caption, tooltip),
                    () => AgeWidgets.Press(command),
                    () => AgeWidgets.Operable(available),
                    tooltip
                );
                vtable.OnFocusVisual = () =>
                    PointerFocus.MoveTo(command, tooltip, AgeWidgets.Transform(caption));
                vtable.OnBlurVisual = ReleasePointer;

                builder.AddItem(Nodes.Drawn(
                    ControlId.For(
                        command,
                        "options:button/"
                            + Distinct(taken, SettingRows.ButtonBar.KeyOf(command))
                    ),
                    vtable,
                    command
                ));
            }
        }

        private readonly SettingRows.ButtonBar _bar = new SettingRows.ButtonBar("options");

        private static readonly Predicate<AgeControlButton> NotARow =
            button => button.GetComponentInParent<OptionsTabPanel>() == null;

        /// <summary>
        /// The transform whose Enable flag says whether this button is available.
        ///
        /// Normally the button's own. Apply is the exception: the window turns applying on and off by
        /// writing the flag on the button it holds in its ApplyButton field, and that field is the
        /// in-game copy whichever skin is worn - so the copy actually drawn out-game would answer with
        /// whatever its prefab shipped. There is one Apply state and this is where the window keeps
        /// it.
        /// </summary>
        private static AgeTransform EnableFlagOf(OptionsModalWindow window, AgeControlButton button)
        {
            try
            {
                if (button.OnActivateMethod == ApplyMethod && window.ApplyButton != null)
                {
                    return window.ApplyButton.AgeTransform;
                }

                return button.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The name the window's own apply handler goes by. Only used to find which button
        /// is Apply; pressing it still goes through the button's own wiring.</summary>
        private const string ApplyMethod = "OnApplyCb";

        // Both skins name their buttons the same, so should two of them ever be drawn at once the
        // second gets a suffix rather than colliding and taking the whole screen down.
        private static string Distinct(HashSet<string> taken, string key)
        {
            string candidate = key;
            for (int n = 2; !taken.Add(candidate); n++)
            {
                candidate = key + "#" + n;
            }

            return candidate;
        }

        /// <summary>A button's name: the caption it is showing, or - for one drawn as a symbol - the
        /// first line of what its tooltip calls it, so no button is ever announced as nothing.
        /// </summary>
        private static string ButtonText(AgePrimitiveLabel caption, AgeTooltip tooltip)
        {
            string text = AgeText.Label(caption);
            return string.IsNullOrEmpty(text) ? CardActions.FirstLine(tooltip) : text;
        }

        // ---- reading the window ----

        /// <summary>The page whose settings are on screen. The window hides the outgoing page
        /// instantly when the category changes, so exactly one is ever shown.</summary>
        private static OptionsTabPanel ShownPanel()
        {
            OptionsModalWindow window = Window();
            try
            {
                if (window == null || window.TabPanelsContainer == null)
                {
                    return null;
                }

                foreach (
                    OptionsTabPanel panel in
                        window.TabPanelsContainer.GetChildren<OptionsTabPanel>(false)
                )
                {
                    if (panel != null && panel.Shown)
                    {
                        return panel;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("options: finding the showing category threw: " + e);
            }

            return null;
        }

        /// <summary>The settings on a page, in the order the page arranged them. Read regardless of
        /// alpha - a page fading in is still the page - and filtered on the visibility the game sets
        /// for a setting this configuration has no use for.</summary>
        private static List<OptionItem> Rows(OptionsTabPanel panel)
        {
            List<OptionItem> rows = new List<OptionItem>();
            try
            {
                foreach (OptionItem item in panel.OptionsTable.GetChildren<OptionItem>(false))
                {
                    // Flow control: every row kept here is read control by control below, and the count
                    // is what tells the page whether it has anything in it at all.
                    if (item != null && AgeWidgets.Visible(AgeWidgets.Transform(item)))
                    {
                        rows.Add(item);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("options: reading a category's settings threw: " + e);
            }

            return rows;
        }

        private static string PanelKey(OptionsTabPanel panel)
        {
            try
            {
                return panel.name;
            }
            catch (Exception)
            {
                return "?";
            }
        }

        /// <summary>
        /// What identifies a row within its page.
        ///
        /// The ROW's own name, not the option's property name: the game names each row
        /// <c>&lt;index&gt;&lt;property&gt;&lt;kind&gt;</c> when it builds the page, so the name is
        /// unique wherever the property name is and unique where it is NOT - which the mod's own
        /// Keybinds page is, since all fifty of its rows are the same property on fifty providers.
        /// A shared property name used to collide into one duplicate id and take the whole page's
        /// build down with it.
        /// </summary>
        private static string OptionKey(OptionItem item)
        {
            try
            {
                return item.name;
            }
            catch (Exception)
            {
                return "?";
            }
        }

        /// <summary>
        /// WHICHEVER options window is showing - the game's, or the mod's own clone of it
        /// (<see cref="ES2Access.UI.ModOptions.ModOptions"/>).
        ///
        /// The two are the same class with different categories in them, and everything else on this
        /// screen is already instance-relative, so reading the mod's window costs exactly this
        /// method. The game's window is looked up by TYPE, which can never answer with the clone:
        /// the gui manager keys that lookup on the exact type and the clone's is a mod type. With
        /// neither showing the game's is answered, and <see cref="IsActive"/> then reports the
        /// screen is not up.
        /// </summary>
        internal static OptionsModalWindow Window()
        {
            try
            {
                ModOptionsWindow mods = ES2Access.UI.ModOptions.ModOptions.Window();
                if (mods != null && mods.Shown)
                {
                    return mods;
                }

                return GameWindows.Of<OptionsModalWindow>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---- shared plumbing ----

        // The click handlers the game runs when a setting is changed with the mouse. Resolved once:
        // a category of 140 rows is rebuilt on every navigation operation, and a reflection lookup per
        // row per operation would be paid for nothing.
        private static readonly MethodInfo SliderReleased = GameHandlers.Method(
            typeof(OptionSliderItem),
            "OnSliderReleasedCb"
        );

        // The handler a click on one of the setting's list entries reaches: it stores the value and
        // tells the window a setting changed.
        private static readonly MethodInfo EntrySelected = GameHandlers.Method(
            typeof(OptionDropListItem),
            "OnEntrySelectedCb"
        );

        private static readonly Action ReleasePointer = PointerFocus.Release;

        /// <summary>Where a focused control's tooltip is drawn from: the transform hugging the visible
        /// text, never the hit area, which the layout stretches well past the words.</summary>
        /// <summary>The label showing a widget's text: the widget itself when it is one, else the
        /// first one under it.</summary>
        internal static AgePrimitiveLabel LabelIn(AgeTransform transform)
        {
            try
            {
                if (transform == null)
                {
                    return null;
                }

                AgePrimitiveLabel own = transform.GetComponent<AgePrimitiveLabel>();
                if (own != null)
                {
                    return own;
                }

                foreach (AgePrimitiveLabel child in transform.GetChildren<AgePrimitiveLabel>(false))
                {
                    if (child != null)
                    {
                        return child;
                    }
                }

                return transform.GetComponentInChildren<AgePrimitiveLabel>();
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
