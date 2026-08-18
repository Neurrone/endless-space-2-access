using System;
using System.Collections.Generic;
using System.Reflection;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using UnityEngine;

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
    public sealed class OptionsScreen : Screen
    {
        private static readonly object TabStop = "options:tabs";
        private static readonly object RowStop = "options:rows";
        private static readonly object ButtonStop = "options:buttons";

        /// <summary>How many fine steps one coarse slider step is worth.</summary>
        private const int CoarseSteps = 10;

        /// <summary>What <c>AgeControlSlider</c> renders when it was given no format of its own.
        /// </summary>
        private const string DefaultValueFormat = "######0";

        /// <summary>How far up a parent chain to look before deciding it is not a chain.</summary>
        private const int MaxAncestors = 64;

        public override string Key
        {
            get { return "screen.options"; }
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

        public override string ScreenName
        {
            get { return ModStrings.Get(ModStrings.ScreenOptions); }
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
        }

        /// <summary>A binding row is listening, or about to be: every key belongs to the field being
        /// bound, and a letter that started a search instead would be a key the player could never
        /// bind.</summary>
        public override bool CapturesRawInput
        {
            get { return _pending != null || _capturing != null; }
        }

        /// <summary>Something else has the player's attention - a confirmation, the drop list, or the
        /// window has gone. A capture that was asked for and not yet handed over is abandoned rather
        /// than left armed to fire under whatever comes next.</summary>
        public override void OnUnfocus()
        {
            CancelPending();
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
            string category = SelectedCategory(window);
            bool named = !string.IsNullOrEmpty(category);
            if (named)
            {
                builder.PushContext(category);
            }

            BuildRows(builder);
            if (named)
            {
                builder.PopContext();
            }

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
                AgeTooltip tooltip = TooltipOf(entry.Toggle.AgeTransform);
                NodeVtable vtable = GraphNodes.Tab(
                    () => AgeText.Label(entry.TitleLabel),
                    () => Selected(entry),
                    () => Enabled(entry.Toggle.AgeTransform),
                    tooltip
                );
                // Focusing the tab IS switching to it. The hook runs once per focus change, after the
                // readout has been composed, so the page changes without the tab announcing itself
                // twice; the guard inside makes a re-focus of the showing tab a no-op.
                vtable.OnFocusVisual = () =>
                {
                    Switch(entry);
                    PointerFocus.MoveTo(null, tooltip, AnchorOf(entry.TitleLabel));
                };
                vtable.OnBlurVisual = ReleasePointer;
                vtable.OnActivate = () => Switch(entry);

                builder.AddItem(
                    ControlId.Referenced(entry, "options:tab/" + CategoryOf(entry)),
                    vtable
                );
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
                    if (tab != null && tab.Toggle != null && Visible(tab.Toggle.AgeTransform))
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

        // ---- the settings of the category showing ----

        private static void BuildRows(GraphBuilder builder)
        {
            OptionsTabPanel panel = ShownPanel();
            if (panel == null || panel.OptionsTable == null)
            {
                return;
            }

            string category = PanelKey(panel);
            foreach (OptionItem item in Rows(panel))
            {
                OptionItem row = item;
                NodeVtable vtable = RowVtable(row);
                if (vtable == null)
                {
                    continue;
                }

                AgeTooltip tooltip = row.Tooltip;
                vtable.OnFocusVisual = () =>
                    PointerFocus.MoveTo(null, tooltip, AnchorOf(row.TitleLabel));
                vtable.OnBlurVisual = ReleasePointer;

                builder.AddItem(
                    ControlId.Referenced(row, "options:" + category + "/" + OptionKey(row)),
                    vtable
                );
            }
        }

        /// <summary>How one setting is read and worked, chosen by what kind of setting it is. Every
        /// kind announces its title, its tooltip and whether it is refusing; what differs is the value
        /// it holds and how the player changes it.</summary>
        private static NodeVtable RowVtable(OptionItem item)
        {
            Func<string> label = () => AgeText.Label(item.TitleLabel);
            Func<bool> enabled = () => Enabled(AgeTransformOf(item));
            AgeTooltip tooltip = item.Tooltip;

            OptionCheckboxItem checkbox = item as OptionCheckboxItem;
            if (checkbox != null && checkbox.Toggle != null)
            {
                return GraphNodes.Checkbox(
                    label,
                    () => Checked(checkbox),
                    () => Flip(checkbox),
                    enabled,
                    tooltip
                );
            }

            OptionSliderItem slider = item as OptionSliderItem;
            if (slider != null && slider.Slider != null)
            {
                return GraphNodes.Slider(
                    label,
                    () => SliderText(slider.Slider),
                    (sign, large) => Slide(slider, sign, large),
                    enabled,
                    tooltip
                );
            }

            OptionDropListItem dropList = item as OptionDropListItem;
            if (dropList != null && dropList.DropList != null)
            {
                NodeVtable combo = GraphNodes.ComboBox(
                    label,
                    () => DropListText(dropList.DropList),
                    () =>
                        DropListScreen.Open(
                            dropList.DropList,
                            AgeText.Label(dropList.TitleLabel),
                            index =>
                            {
                                dropList.DropList.SelectedItem = index;
                                Call(EntrySelected, dropList, NoSender);
                            }
                        ),
                    enabled,
                    tooltip
                );
                // Activating this one opens a list rather than changing the setting, so there is no
                // new state to report: the list that opens says where it starts.
                combo.StateText = null;
                return combo;
            }

            // What the action is called, then the keys it is on: Enter rebinds the first, Backspace
            // the second.
            OptionKeyMappingItem binding = item as OptionKeyMappingItem;
            if (binding != null)
            {
                NodeVtable keys = GraphNodes.Button(
                    label,
                    () => StartCapture(binding, false),
                    enabled,
                    tooltip
                );
                keys.Announcements.Add(GraphNodes.ValuePart(() => BindingText(binding)));
                keys.OnSecondary = () => StartCapture(binding, true);
                return keys;
            }

            // No option in the game is a text field, and the row this page used to declare for one was
            // a Button that handed the keyboard over with no words, no way back and no cancel - a
            // second, worse copy of an editor that now exists once (<see cref="TextFieldEditor"/>).
            // Deleted rather than migrated: nothing draws it, so nothing could test it. An option that
            // ever becomes one falls through to the read-only row below, which is honest.
            return ReadOnly(label, enabled, tooltip);
        }

        /// <summary>A row of a kind we have no interaction for: still named, still readable, and
        /// silent about being a control the player can do anything with.</summary>
        private static NodeVtable ReadOnly(
            Func<string> label,
            Func<bool> enabled,
            AgeTooltip tooltip
        )
        {
            List<NodeAnnouncement> parts = new List<NodeAnnouncement>
            {
                GraphNodes.LabelPart(label),
                GraphNodes.DisabledPart(enabled),
            };

            return new NodeVtable
            {
                Announcements = parts,
                Sections = GraphNodes.Sections(null, tooltip),
            };
        }

        // ---- working the settings ----

        private static bool Checked(OptionCheckboxItem item)
        {
            try
            {
                return item.Toggle.State;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Tick or untick the box exactly as a click does: the toggle's own state first - it
        /// is what the game reads back - then the item's click handler, which stores the value and
        /// tells the window a setting changed.</summary>
        private static void Flip(OptionCheckboxItem item)
        {
            try
            {
                item.Toggle.State = !item.Toggle.State;
                Call(CheckboxSwitched, item, NoSender);
            }
            catch (Exception e)
            {
                Log.Warn("options: toggling a setting threw: " + e);
            }
        }

        /// <summary>The number the slider is showing. Composed the way the slider composes its own
        /// label - snapped to the increment, then through the format string the option gave it, which
        /// is what turns 0.75 into "75%".</summary>
        private static string SliderText(AgeControlSlider slider)
        {
            try
            {
                string format = slider.ValueFormat;
                return Snap(slider, slider.CurrentValue).ToString(
                    string.IsNullOrEmpty(format) ? DefaultValueFormat : format
                );
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Move the slider one step, or ten. The value lands on the increment grid and is
        /// then handed over through the item's release handler - the same two things a drag and a
        /// mouse-up do, in the same order.</summary>
        private static void Slide(OptionSliderItem item, int sign, bool large)
        {
            try
            {
                AgeControlSlider slider = item.Slider;
                float step =
                    slider.Increment > 0f
                        ? slider.Increment
                        : (slider.MaxValue - slider.MinValue) / 100f;
                if (step <= 0f)
                {
                    return;
                }

                float target = Mathf.Clamp(
                    Snap(slider, slider.CurrentValue + sign * step * (large ? CoarseSteps : 1)),
                    slider.MinValue,
                    slider.MaxValue
                );
                if (target == slider.CurrentValue)
                {
                    return;
                }

                slider.CurrentValue = target;
                Call(SliderReleased, item, NoSender);
            }
            catch (Exception e)
            {
                Log.Warn("options: moving a slider threw: " + e);
            }
        }

        private static float Snap(AgeControlSlider slider, float value)
        {
            return slider.Increment > 0f
                ? slider.MinValue
                    + slider.Increment
                        * Mathf.Round((value - slider.MinValue) / slider.Increment)
                : value;
        }

        /// <summary>What the closed list is set to. Read from the label the list is rendering, which
        /// the game has already localized; the raw entry table holds localization keys, so it is only
        /// the fallback and is localized on the way out.</summary>
        private static string DropListText(AgeControlDropList list)
        {
            try
            {
                string rendered = AgeText.Label(LabelIn(list.CurrentItem));
                if (!string.IsNullOrEmpty(rendered))
                {
                    return rendered;
                }

                int index = list.SelectedItem;
                return index >= 0 && index < list.LabelTable.Count
                    ? AgeText.Clean(list.LabelTable[index])
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The keys an action is on. Watched live, which is what carries the whole rebinding flow:
        /// the field blanks itself the moment it takes the keyboard, rewrites its own text as each
        /// key goes down, and the game writes the settled binding back when the capture ends - so the
        /// player hears the combination building under their fingers and then hears what stuck,
        /// without this screen having to watch for any of it.
        ///
        /// While a capture is running only the field being captured speaks. The other one has not
        /// changed and saying "not bound" about a field that has merely been emptied to listen would
        /// be a lie.
        /// </summary>
        private static string BindingText(OptionKeyMappingItem item)
        {
            try
            {
                AgeControlKeyBindingField listening = CapturingField(item);
                if (listening != null)
                {
                    return AgeText.Label(listening.Label);
                }

                string secondary = KeyText(item.SecondaryKeyBindingField);
                MessageBuilder message = new MessageBuilder();
                message.ListItem(KeyText(item.PrimaryKeyBindingField));
                if (!string.IsNullOrEmpty(secondary))
                {
                    message.ListItem(
                        ModStrings.Format(ModStrings.NavKeyBindingSecondary, secondary)
                    );
                }

                return message.Build() ?? ModStrings.Get(ModStrings.NavNotBound);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string KeyText(AgeControlKeyBindingField field)
        {
            return field == null ? null : AgeText.Label(field.Label);
        }

        // ---- rebinding a key ----

        /// <summary>The row whose binding is being captured, remembered only so that the mod going
        /// away mid-capture can hand the keyboard back. Whether a capture is running at all is asked
        /// of the game, never of this field, so it can never be out of step with what is on screen.
        /// </summary>
        private static OptionKeyMappingItem _capturing;

        /// <summary>The row that has asked for a capture and is waiting for the player's hand to come
        /// off the keyboard, and which of its two bindings was asked for.</summary>
        private static OptionKeyMappingItem _pending;
        private static bool _pendingSecondary;

        /// <summary>Consecutive frames with nothing held down since the capture was asked for.
        /// </summary>
        private static int _pendingClearFrames;

        /// <summary>
        /// How many of those it takes before the keyboard changes hands.
        ///
        /// Two, not one. The frame a key comes up is a frame on which nothing is held AND
        /// <c>GetKeyUp</c> reports it - and a key coming up is precisely what the binding field treats
        /// as the end of a capture. Handing over on the first clear frame would therefore hand the
        /// field the release of the very key that asked for the capture, which is the bug this whole
        /// wait exists to avoid. One more frame and that release has been and gone.
        /// </summary>
        private const int ClearFramesBeforeCapture = 2;

        /// <summary>
        /// Ask to listen for a new combination. The prompt is spoken now; the keyboard changes hands
        /// once the player has let go of it - see <see cref="HandOverWhenReleased"/>.
        ///
        /// Everything after the hand-over belongs to the game. The field blanks itself, scans every
        /// key each frame while it holds the focus, builds the combination from up to two keys, and
        /// ends the capture on the first key RELEASE - which hands the focus back, which is what makes
        /// the field apply what was pressed, raise the "that key is already used for X" question if it
        /// has to, and write the result back into both fields.
        ///
        /// The mod's input layer stands down on its own for the duration, because the field declares
        /// itself keyboard-exclusive - and it must, or the arrow keys and Escape could never be bound
        /// to anything. This is exactly why the drop list's exemption is written as "is this the one
        /// control we handed focus to" rather than "is the focused control ours".
        /// </summary>
        private static void StartCapture(OptionKeyMappingItem item, bool secondary)
        {
            try
            {
                // One at a time. A second Enter while the first is still waiting to be handed over is
                // the same request again, and re-arming it would only re-say the prompt.
                if (_pending != null || !Enabled(AgeTransformOf(item)))
                {
                    return;
                }

                AgeControlKeyBindingField field = secondary
                    ? item.SecondaryKeyBindingField
                    : item.PrimaryKeyBindingField;
                if (field == null || AgeManager.Instance == null)
                {
                    return;
                }

                _pending = item;
                _pendingSecondary = secondary;
                _pendingClearFrames = 0;

                // Said at once, and interrupting: the row has just been read and what matters now is
                // that the keyboard is about to change hands.
                Voice.Say(
                    ModStrings.Get(
                        secondary
                            ? ModStrings.NavPressSecondaryKey
                            : ModStrings.NavPressPrimaryKey
                    ),
                    true
                );
            }
            catch (Exception e)
            {
                Log.Warn("options: starting a key capture threw: " + e);
            }
        }

        /// <summary>
        /// Give the field the keyboard, but not until the player has let go of everything.
        ///
        /// This is the whole reason the hand-over is deferred at all. The field ends its capture on
        /// the FIRST key release it sees, and the key that asked for the capture - Enter, or Backspace
        /// - is still down at that moment and will come up a few frames later. Handing over
        /// immediately therefore ended the capture with whatever the player had not yet had time to
        /// press, which read as the capture dying the instant it started. Waiting for a clear keyboard
        /// costs nobody anything: the player has to let go before pressing the new combination anyway.
        ///
        /// Nothing is stood down during the wait - the field does not have the focus yet - so the
        /// player can simply arrow away instead, and the request goes with them.
        /// </summary>
        private void HandOverWhenReleased()
        {
            OptionKeyMappingItem item = _pending;
            if (item == null)
            {
                return;
            }

            if (!OnRow(item))
            {
                CancelPending();
                return;
            }

            // Spelled out: the game has its own Input in the global namespace.
            if (UnityEngine.Input.anyKey)
            {
                _pendingClearFrames = 0;
                return;
            }

            if (++_pendingClearFrames < ClearFramesBeforeCapture)
            {
                return;
            }

            try
            {
                AgeControlKeyBindingField field = _pendingSecondary
                    ? item.SecondaryKeyBindingField
                    : item.PrimaryKeyBindingField;
                AgeManager age = AgeManager.Instance;
                _pending = null;
                if (field == null || age == null)
                {
                    return;
                }

                _capturing = item;
                age.FocusedControl = field;
            }
            catch (Exception e)
            {
                _pending = null;
                Log.Warn("options: handing the keyboard to a binding field threw: " + e);
            }
        }

        /// <summary>Whether the cursor is still on the row that asked to capture. Moving off it is the
        /// player changing their mind, and the request has to go with them or the next thing they
        /// press would be bound to a row they have left.</summary>
        private static bool OnRow(OptionKeyMappingItem item)
        {
            try
            {
                GraphNavigator navigator = ModEntry.Navigator;
                GraphNode node = navigator == null ? null : navigator.CurrentNode;
                return node != null && node.Id.ReferenceMatches(item);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void CancelPending()
        {
            _pending = null;
            _pendingClearFrames = 0;
        }

        /// <summary>Which of a row's two fields is listening for keys right now, or null. Read from
        /// the game's own focus rather than from anything the mod remembers, so a capture the game
        /// ended - a key released, Escape, a click elsewhere - is over here the same instant.</summary>
        private static AgeControlKeyBindingField CapturingField(OptionKeyMappingItem item)
        {
            try
            {
                AgeManager age = AgeManager.Instance;
                AgeControl focused = age == null ? null : age.FocusedControl;
                if (focused == null)
                {
                    return null;
                }

                if (ReferenceEquals(focused, item.PrimaryKeyBindingField))
                {
                    return item.PrimaryKeyBindingField;
                }

                return ReferenceEquals(focused, item.SecondaryKeyBindingField)
                    ? item.SecondaryKeyBindingField
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Stop listening, and bind nothing - the mod is going away and the player never finished.
        ///
        /// The order matters: the fields are put back to what the option actually holds FIRST, so
        /// that handing the keyboard back cannot apply a half-pressed combination. Letting go is what
        /// makes the game apply, and it only applies what it finds different from the current
        /// binding.
        /// </summary>
        internal static void ReleaseCapture()
        {
            CancelPending();
            OptionKeyMappingItem item = _capturing;
            _capturing = null;
            if (item == null)
            {
                return;
            }

            try
            {
                AgeManager age = AgeManager.Instance;
                if (age == null || CapturingField(item) == null)
                {
                    return;
                }

                item.Refresh();
                age.FocusedControl = null;
            }
            catch (Exception e)
            {
                Log.Warn("options: abandoning a key capture threw: " + e);
            }
        }

        // ---- the buttons along the bottom ----

        /// <summary>One of the window's bottom buttons: the button itself and where along the bar it
        /// is drawn.</summary>
        private struct Command
        {
            public AgeControlButton Button;
            public float X;
        }

        /// <summary>
        /// The bar along the bottom.
        ///
        /// The window carries the bar TWICE - once for the main menu, once for a game in progress -
        /// and the buttons it names in its own fields are the in-game set, whichever skin is being
        /// worn. So the bar is not read from those fields at all: it is whichever wired buttons are
        /// actually drawn, in the order they are drawn. That is one mechanism for both skins, it is
        /// how the duplicate "Reset to Defaults" stopped being possible, and it needs no list of
        /// which buttons the window is expected to have.
        /// </summary>
        private void BuildButtons(GraphBuilder builder, OptionsModalWindow window)
        {
            List<Command> commands = Commands(window);
            if (commands.Count == 0)
            {
                return;
            }

            HashSet<string> taken = new HashSet<string>();
            foreach (Command entry in commands)
            {
                Command command = entry;
                AgePrimitiveLabel caption = LabelIn(command.Button.AgeTransform);
                AgeTooltip tooltip = TooltipOf(command.Button.AgeTransform);
                AgeTransform available = EnableFlagOf(window, command.Button);
                NodeVtable vtable = GraphNodes.Button(
                    () => ButtonText(caption, tooltip),
                    () => Press(command),
                    () => Enabled(available),
                    tooltip
                );
                vtable.OnFocusVisual = () =>
                    PointerFocus.MoveTo(command.Button, tooltip, AnchorOf(caption));
                vtable.OnBlurVisual = ReleasePointer;

                builder.AddItem(
                    ControlId.Referenced(
                        command.Button,
                        "options:button/" + Distinct(taken, KeyOf(command.Button))
                    ),
                    vtable
                );
            }
        }

        /// <summary>The buttons the bar is currently showing, left to right as they are drawn - the
        /// order the fields are declared in is not the order they sit in.</summary>
        private List<Command> Commands(OptionsModalWindow window)
        {
            List<Command> commands = new List<Command>();
            List<AgeControlButton> buttons = Buttons(window);
            for (int i = 0; i < buttons.Count; i++)
            {
                AgeControlButton button = buttons[i];
                if (button == null || !OnScreen(button.AgeTransform))
                {
                    continue;
                }

                Command command = new Command
                {
                    Button = button,
                    X = LeftEdge(button.AgeTransform),
                };

                // Placed by where it is drawn, so the bar reads the way it looks. Inserted rather
                // than sorted afterwards: two buttons at the same place keep the order they were
                // found in, which List.Sort would not promise.
                int at = commands.Count;
                while (at > 0 && commands[at - 1].X > command.X)
                {
                    at--;
                }

                commands.Insert(at, command);
            }

            return commands;
        }

        // The window's wired buttons, and which window they were found on. Held per screen instance
        // rather than statically, so a hot reload starts with nothing remembered; the window builds
        // its bar once when it loads and never rebuilds it, so finding them again on every navigation
        // operation would be a walk of the whole window for an answer that cannot have changed. What
        // DOES change - which of them are drawn, where, and whether they are available - is read live
        // every time.
        private OptionsModalWindow _buttonsFrom;
        private List<AgeControlButton> _buttons;

        private List<AgeControlButton> Buttons(OptionsModalWindow window)
        {
            if (ReferenceEquals(_buttonsFrom, window) && _buttons != null && AllAlive(_buttons))
            {
                return _buttons;
            }

            _buttonsFrom = window;
            _buttons = Collect(window);
            return _buttons;
        }

        /// <summary>Every button on the window that is wired to something. The window's backdrops are
        /// buttons too - they are there to swallow clicks that miss - and they are told to call
        /// nothing, which is exactly what tells them apart from the bar.</summary>
        private static List<AgeControlButton> Collect(OptionsModalWindow window)
        {
            List<AgeControlButton> buttons = new List<AgeControlButton>();
            try
            {
                foreach (
                    AgeControlButton button in window.GetComponentsInChildren<AgeControlButton>(true)
                )
                {
                    if (button != null && !string.IsNullOrEmpty(button.OnActivateMethod))
                    {
                        buttons.Add(button);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("options: finding the window's buttons threw: " + e);
            }

            return buttons;
        }

        private static bool AllAlive(List<AgeControlButton> buttons)
        {
            for (int i = 0; i < buttons.Count; i++)
            {
                if (buttons[i] == null)
                {
                    return false;
                }
            }

            return true;
        }

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

        private static string KeyOf(AgeControlButton button)
        {
            try
            {
                return button.name + "/" + button.OnActivateMethod;
            }
            catch (Exception)
            {
                return "?";
            }
        }

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
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            IList<string> lines = AgeText.Lines(AgeText.Tooltip(tooltip));
            return lines.Count > 0 ? lines[0] : null;
        }

        /// <summary>Press a button the way the engine presses it: every AGE button carries the object
        /// and method its own mouse handler sends to, so replaying that pair runs the window's own
        /// handler - the same one for either skin's copy of the button.</summary>
        private static void Press(Command command)
        {
            AgeControlButton button = command.Button;
            try
            {
                if (button.OnActivateObject != null)
                {
                    button.OnActivateObject.SendMessage(
                        button.OnActivateMethod,
                        button.gameObject,
                        SendMessageOptions.DontRequireReceiver
                    );
                }
            }
            catch (Exception e)
            {
                Log.Warn("options: pressing " + KeyOf(button) + " threw: " + e);
            }
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
                    if (item != null && Visible(AgeTransformOf(item)))
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

        private static string OptionKey(OptionItem item)
        {
            try
            {
                return item.Option != null ? item.Option.PropertyName : item.name;
            }
            catch (Exception)
            {
                return "?";
            }
        }

        internal static OptionsModalWindow Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<OptionsModalWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---- shared plumbing ----

        // The click handlers the game runs when a setting is changed with the mouse. Resolved once:
        // a category of 140 checkboxes is rebuilt on every navigation operation, and a reflection
        // lookup per row per operation would be paid for nothing.
        private static readonly MethodInfo CheckboxSwitched = Handler(
            typeof(OptionCheckboxItem),
            "OnSwitchCb"
        );

        private static readonly MethodInfo SliderReleased = Handler(
            typeof(OptionSliderItem),
            "OnSliderReleasedCb"
        );

        // The handler a click on one of the setting's list entries reaches: it stores the value and
        // tells the window a setting changed.
        private static readonly MethodInfo EntrySelected = Handler(
            typeof(OptionDropListItem),
            "OnEntrySelectedCb"
        );

        private static readonly Action ReleasePointer = PointerFocus.Release;

        /// <summary>The argument list for a click handler that wants the game object the click landed
        /// on. There was no click, so it gets nothing - which is the same thing the game passes when
        /// it calls these handlers itself. Spelled out as an array because a bare null would be read
        /// as "no arguments at all" and the handlers take one.</summary>
        internal static readonly object[] NoSender = { null };

        internal static MethodInfo Handler(Type type, string name)
        {
            try
            {
                return type.GetMethod(
                    name,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
                );
            }
            catch (Exception e)
            {
                Log.Warn("options: looking up " + type.Name + "." + name + " threw: " + e);
                return null;
            }
        }

        internal static void Call(MethodInfo method, object target, params object[] arguments)
        {
            if (method == null || target == null)
            {
                return;
            }

            method.Invoke(target, arguments);
        }

        /// <summary>Where a focused control's tooltip is drawn from: the transform hugging the visible
        /// text, never the hit area, which the layout stretches well past the words.</summary>
        private static AgeTransform AnchorOf(AgePrimitiveLabel label)
        {
            try
            {
                return label == null ? null : label.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

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

        private static AgeTooltip TooltipOf(AgeTransform transform)
        {
            try
            {
                return transform == null ? null : transform.AgeTooltip;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static AgeTransform AgeTransformOf(OptionItem item)
        {
            try
            {
                return item == null ? null : item.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool Visible(AgeTransform transform)
        {
            try
            {
                return transform != null && transform.Visible;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Whether a widget is really on screen: its own visibility and every ancestor's.
        ///
        /// The window carries two skins - one for the main menu, one for a game in progress - and
        /// switches between them by hiding whole CONTAINERS. A button in the skin that is not in use
        /// therefore reports itself perfectly visible while nothing of it is drawn, which is how the
        /// Controls page came to offer "Reset to Defaults" twice.
        /// </summary>
        private static bool OnScreen(AgeTransform transform)
        {
            try
            {
                int depth = 0;
                for (
                    AgeTransform node = transform;
                    node != null && depth++ < MaxAncestors;
                    node = node.Parent
                )
                {
                    if (!node.Visible)
                    {
                        return false;
                    }
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static float LeftEdge(AgeTransform transform)
        {
            try
            {
                return transform.GetGlobalPosition().x;
            }
            catch (Exception)
            {
                return 0f;
            }
        }

        private static bool Enabled(AgeTransform transform)
        {
            try
            {
                return transform != null && transform.Enable;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
