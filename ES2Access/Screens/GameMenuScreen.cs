using System;
using System.Collections.Generic;
using System.Reflection;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using ES2Access.UI.ModOptions;
using UnityEngine;

namespace ES2Access.Screens
{
    /// <summary>
    /// The pause menu - the ring of choices Escape raises over a running game - made navigable.
    ///
    /// The window draws its entries in a circle, which is a mouse shape; spoken, they are simply a
    /// list, walked with the arrows in the order the game itself declares them. Each entry's caption
    /// is whichever of its two labels the game is currently showing (the circle puts text on the
    /// side away from the centre), and its tooltip is spoken with it: for Save and Load the game
    /// writes the reason a refusal is refusing into exactly that tooltip, and the reason is the
    /// thing a player facing a grey button needs to hear.
    ///
    /// The two panel toggles (game settings, turn timers) open side panels in the window's top
    /// corners. The toggles stay where they are, in the ring; each panel becomes A TAB STOP OF ITS OWN
    /// while it is open, and stops existing when it is closed. That is what the panels are to the
    /// player: somewhere else to be, reachable only while it is on screen, and the toggle that opened
    /// it is still the thing that closes it - so the cursor is never left standing in a panel that has
    /// gone.
    ///
    /// What a panel's rows can do is the game's decision, not ours. The game settings panel is opened
    /// READ ONLY in a running game - the galaxy shape cannot be changed once it has been generated -
    /// and its rows are read as the values they are, with the panel saying once that it is read only
    /// rather than every row saying it cannot be changed. The turn timer panel is opened editable, and
    /// the rows the game will accept a change to are worked through its own handlers, the same way the
    /// options page works its settings.
    ///
    /// Escape belongs to the game here: the window is a modal and its own Exit route is what closes
    /// it and resumes play.
    /// </summary>
    public sealed class GameMenuScreen : Screen
    {
        private static readonly object MenuStop = "gamemenu:menu";
        private static readonly object GameSettingsStop = "gamemenu:game-settings-panel";
        private static readonly object TimerSettingsStop = "gamemenu:timer-settings-panel";

        /// <summary>How many single steps one coarse step of a setting slider is worth.</summary>
        private const int CoarseSteps = 10;

        public override string Key
        {
            get { return "screen.game-menu"; }
        }

        /// <summary>Above the popups a game corner can hold - a notification does not survive being
        /// asked to share the stage with the pause menu - and below the loading screen and the
        /// confirmation box, both of which can stand on top of it.</summary>
        public override int Layer
        {
            get { return 50; }
        }

        /// <summary>The window's own drawn heading, which is the only place it is ever said: the game
        /// writes the title across the top of the ring and nothing else declares it, so arriving reads
        /// the words the player sees. Drawn over two lines ("Game" over "Menu"), which is a fact about
        /// the box it was drawn in - the lines join with a space, the way any multi-line game text
        /// does. The mod's own word stands in where the window has not written its title yet.</summary>
        public override string ScreenName
        {
            get { return Title() ?? ModStrings.Get(ModStrings.ScreenGameMenu); }
        }

        private static string Title()
        {
            try
            {
                GameMenuModalWindow window = Window();
                AgeTransform title = window == null ? null : window.Title;
                string drawn = title == null
                    ? null
                    : AgeText.Label(title.GetComponent<AgePrimitiveLabel>());
                MessageBuilder message = new MessageBuilder();
                foreach (string line in AgeText.Lines(drawn))
                {
                    message.Fragment(line);
                }

                string words = message.IsEmpty ? null : message.Build();

                // A key the localizer handed back unchanged is text the game has not written: parked,
                // not shown, and never spoken.
                return string.IsNullOrEmpty(words) || words[0] == '%' ? null : words;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public override bool IsActive()
        {
            GameMenuModalWindow window = Window();
            try
            {
                return window != null && window.Shown && window.IsReady;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The game's own Escape route closes the menu and resumes play.</summary>
        public override bool Back()
        {
            return false;
        }

        public override void Build(GraphBuilder builder)
        {
            GameMenuModalWindow window = Window();
            if (window == null)
            {
                return;
            }

            builder.BeginStop(MenuStop);
            BuildMenu(builder, window);

            if (State(window.ShowGameSettingsPanelToggle))
            {
                builder.BeginStop(GameSettingsStop);
                BuildPanel(builder, window.GameSettingsPanel, "gamemenu:game-setting/");
            }

            if (State(window.ShowTimerSettingsPanelToggle))
            {
                builder.BeginStop(TimerSettingsStop);
                BuildPanel(builder, window.TimerSettingsPanel, "gamemenu:timer-setting/");
            }
        }

        // ---- the ring of choices, and the two panel toggles ----

        private static void BuildMenu(GraphBuilder builder, GameMenuModalWindow window)
        {
            foreach (GameMenuItem item in Items(window))
            {
                GameMenuItem entry = item;
                AgePrimitiveLabel caption = Caption(entry);
                if (caption == null)
                {
                    continue;
                }

                AgeControlButton button = Button(entry);
                AgeTooltip tooltip = Tooltip(entry);
                NodeVtable vtable = GraphNodes.Button(
                    () => AgeText.Label(Caption(entry)),
                    () => Press(Button(entry)),
                    () => Enabled(entry),
                    tooltip
                );
                if (button != null)
                {
                    vtable.OnFocusVisual = () => PointerFocus.MoveTo(button, tooltip);
                    vtable.OnBlurVisual = ReleasePointer;
                }

                builder.AddItem(Nodes.Drawn(
                    ControlId.For(entry, "gamemenu:" + entry.name),
                    vtable,
                    entry
                ));

                // The mod's own settings, right where the game's are. Found by what the entry DOES
                // rather than by what it is called, so a renamed prefab cannot move it.
                if (OpensOptions(button))
                {
                    ModSettingsNode.Add(builder, "gamemenu:mod-settings");
                }
            }

            AddToggle(
                builder,
                window.ShowGameSettingsPanelToggle,
                ToggleCaption(window.ShowGameSettingsPanelToggle, ModStrings.GameMenuGameSettings),
                "gamemenu:game-settings"
            );
            AddToggle(
                builder,
                window.ShowTimerSettingsPanelToggle,
                () => Timers(window),
                "gamemenu:timer-settings"
            );
        }

        private static void AddToggle(
            GraphBuilder builder,
            AgeControlToggle toggle,
            Func<string> caption,
            string key
        )
        {
            if (!Visible(toggle))
            {
                return;
            }

            AgeControlToggle control = toggle;
            // The sentence the game writes for each of these two boxes ("Click to show the main galaxy
            // and gameplay settings used for this game.") hangs on the toggle itself and was reaching
            // nobody: the node existed, was pressable, and carried none of its own words.
            AgeTooltip tooltip = AgeWidgets.Raw(AgeWidgets.Transform(control));
            NodeVtable vtable = GraphNodes.Checkbox(
                caption,
                () => State(control),
                () => Flip(control),
                () => Enabled(control),
                tooltip
            );
            AgeWidgets.PointAt(vtable, AgeWidgets.Transform(control), tooltip);
            builder.AddItem(Nodes.Drawn(ControlId.For(control, key), vtable, control));
        }

        // ---- a settings panel ----

        /// <summary>
        /// One side panel's settings, grouped under the headings the panel draws them under.
        ///
        /// The headings are structure rather than controls - there is nothing to do to "Time
        /// Management" - so they are pushed as context: they are announced when the cursor enters the
        /// group and stay silent while it walks along inside. A panel the game will not let the player
        /// change says so once, there, rather than once per row.
        /// </summary>
        private static void BuildPanel(GraphBuilder builder, InGameSettingsPanel panel, string key)
        {
            if (panel == null)
            {
                return;
            }

            foreach (InGameSettingCategoryItem group in Groups(panel))
            {
                List<SettingItem> rows = Rows(group);
                if (rows.Count == 0)
                {
                    continue;
                }

                builder.PushContext(AgeText.Label(group.Title), ReadOnlyWord(rows));
                foreach (SettingItem item in rows)
                {
                    SettingItem row = item;
                    NodeVtable vtable = SettingVtable(panel, row);
                    AgeTransform anchor = TitleTransform(row);
                    AgeTooltip tooltip = TooltipOf(anchor);
                    vtable.OnFocusVisual = () => PointerFocus.MoveTo(anchor, tooltip, anchor);
                    vtable.OnBlurVisual = ReleasePointer;
                    builder.AddItem(Nodes.Drawn(
                        ControlId.For(row.AgeTransform, key + SettingName(row)),
                        vtable,
                        row.AgeTransform
                    ));
                }

                builder.PopContext();
            }
        }

        /// <summary>"Read only", when the game is refusing every row in the group. Said about the
        /// group rather than about its rows: the whole game settings panel is a report in a running
        /// game, and hearing that a dozen times over is not a dozen times as informative.</summary>
        private static string ReadOnlyWord(List<SettingItem> rows)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (CanModify(rows[i]))
                {
                    return null;
                }
            }

            return ModStrings.Get(ModStrings.GameMenuReadOnlySettings);
        }

        /// <summary>
        /// How one setting is read and worked, chosen by what kind of setting it is. Every kind
        /// announces the game's own title for it, its description, and the value the panel is showing -
        /// which for each kind is the label the panel writes the chosen value into, so what is spoken
        /// is what is drawn.
        ///
        /// Only the kinds the game actually makes editable in a running game are given a way to change
        /// them: a tick box and the stepped slider the timer settings are made of. A drop list or a
        /// text field is read as the value it holds, which is all it ever is here - both only appear in
        /// the read-only panel.
        /// </summary>
        private static NodeVtable SettingVtable(InGameSettingsPanel panel, SettingItem item)
        {
            Func<string> label = () => AgeText.Label(item.SettingTitle);
            Func<bool> editable = () => CanModify(item);
            AgeTooltip tooltip = TooltipOf(TitleTransform(item));

            SettingCheckBoxItem checkbox = item as SettingCheckBoxItem;
            if (checkbox != null && checkbox.Toggle != null)
            {
                if (!CanModify(item))
                {
                    return Value(label, () => Ticked(checkbox), tooltip, item);
                }

                return Detailed(
                    GraphNodes.Checkbox(
                        label,
                        () => State(checkbox.Toggle),
                        () => FlipSetting(checkbox),
                        editable,
                        tooltip
                    ),
                    tooltip,
                    item
                );
            }

            SettingSliderItem slider = item as SettingSliderItem;
            if (slider != null)
            {
                Func<string> chosen = () => AgeText.Label(slider.SelectedSettingItemLabel);
                if (!CanModify(item))
                {
                    return Value(label, chosen, tooltip, item);
                }

                return Detailed(
                    GraphNodes.Slider(
                        label,
                        chosen,
                        (sign, large) => StepSetting(panel, slider, sign, large),
                        editable,
                        tooltip
                    ),
                    tooltip,
                    item
                );
            }

            SettingDropListItem list = item as SettingDropListItem;
            if (list != null && list.DropList != null)
            {
                return Value(label, () => ListText(list.DropList), tooltip, item);
            }

            SettingTextFieldItem field = item as SettingTextFieldItem;
            if (field != null && field.TextField != null)
            {
                return Value(label, () => AgeText.Label(field.TextField.Label), tooltip, item);
            }

            return Value(label, null, tooltip, item);
        }

        /// <summary>A setting that is only being reported: its name and what it is set to, and no word
        /// about being a control, because here it is not one.</summary>
        private static NodeVtable Value(
            Func<string> label,
            Func<string> value,
            AgeTooltip tooltip,
            SettingItem item
        )
        {
            List<NodeAnnouncement> parts = new List<NodeAnnouncement>
            {
                GraphNodes.LabelPart(label),
            };
            if (value != null)
            {
                parts.Add(GraphNodes.ValuePart(value));
            }

            return Detailed(new NodeVtable { Announcements = parts }, tooltip, item);
        }

        /// <summary>A setting's content, the same pair every other page declares (SettingRows): what
        /// the game says the setting is FOR, then what it says about the value it is currently ON -
        /// which is the half a player standing on "Normal" actually wants, and which is therefore the
        /// one the readout speaks, and the one said again the moment the player moves the setting
        /// (<see cref="SettingRows.SayValueTooltip"/>).</summary>
        private static NodeVtable Detailed(NodeVtable vtable, AgeTooltip tooltip, SettingItem item)
        {
            AgeTooltip value = CurrentValueTooltip(item);
            vtable.Sections = SettingRows.RowSections(tooltip, value);
            SettingRows.SayValueTooltip(vtable, value);
            return vtable;
        }

        /// <summary>What a tick box that cannot be changed is showing, in the words navigation uses
        /// for a tick everywhere else.</summary>
        private static string Ticked(SettingCheckBoxItem item)
        {
            return ModStrings.Get(
                State(item.Toggle) ? ModStrings.NavChecked : ModStrings.NavUnchecked
            );
        }

        /// <summary>Tick or untick a setting exactly as a click does: the toggle's own state first -
        /// it is what the item reads back - then the item's click handler, which asks the game whether
        /// anything else constrains the value and hands the answer to the panel.</summary>
        private static void FlipSetting(SettingCheckBoxItem item)
        {
            try
            {
                item.Toggle.State = !item.Toggle.State;
                OptionsScreen.Call(SettingToggled, item, OptionsScreen.NoSender);
            }
            catch (Exception e)
            {
                Log.Warn("game menu: toggling a setting threw: " + e);
            }
        }

        /// <summary>
        /// Move a setting slider one of its values along, or ten.
        ///
        /// The game's slider is a strip the mouse is dragged along: its own release handler recomputes
        /// which value the CURSOR is over, so replaying it would throw away the step just taken and
        /// read the pointer instead - wherever the pointer happens to be. So the keyboard does the half
        /// the mouse was for (choosing the value) and the game does the rest, through the same three
        /// steps its release handler runs afterwards: ask the game whether anything else constrains the
        /// value, remember it if the setting is one the game remembers, and tell the panel. The panel
        /// writes the value and refreshes the row, so the slider ends up showing what the game accepted
        /// rather than what was asked for.
        /// </summary>
        private static void StepSetting(
            InGameSettingsPanel panel,
            SettingSliderItem item,
            int sign,
            bool large
        )
        {
            try
            {
                GameSettingDefinition setting = item.SettingDefinition;
                if (setting == null || setting.ItemDefinitions == null)
                {
                    return;
                }

                int count = setting.ItemDefinitions.Length;
                int current = SliderValue(item);
                if (count == 0 || current < 0)
                {
                    return;
                }

                int target = Mathf.Clamp(
                    current + sign * (large ? CoarseSteps : 1),
                    0,
                    count - 1
                );
                if (target == current)
                {
                    return;
                }

                Apply(panel, setting, setting.ItemDefinitions[target]);
            }
            catch (Exception e)
            {
                Log.Warn("game menu: moving a setting slider threw: " + e);
            }
        }

        /// <summary>Hand the game a new value for a setting the way its own widgets do.</summary>
        private static void Apply(
            InGameSettingsPanel panel,
            GameSettingDefinition setting,
            GameSettingDefinition.ItemDefinition wanted
        )
        {
            IGameSettingService service = Amplitude.Unity.Framework.Services.GetService<IGameSettingService>();
            if (service == null)
            {
                return;
            }

            GameSettingDefinition.ItemDefinition allowed =
                service.GetCorrectedSettingItemIfConstrainedByOthers(setting, wanted);
            if (allowed == null)
            {
                return;
            }

            if (setting.SaveInRegistry)
            {
                Amplitude.Unity.Framework.Application.Registry.SetValue(
                    setting.RegistryPath,
                    allowed.Name
                );
            }

            panel.OnSettingModified(setting, allowed);
        }

        /// <summary>Which of its values a setting slider is on. The item keeps it to itself - it is
        /// the position of the cursor along the strip - so it is asked for by name.</summary>
        private static int SliderValue(SettingSliderItem item)
        {
            try
            {
                if (SliderCurrentValue == null)
                {
                    return -1;
                }

                return (int)SliderCurrentValue.GetValue(item, null);
            }
            catch (Exception)
            {
                return -1;
            }
        }

        /// <summary>What a closed drop list is set to: the label it is rendering, which the game has
        /// already localized.</summary>
        private static string ListText(AgeControlDropList list)
        {
            try
            {
                return AgeText.Label(OptionsScreen.LabelIn(list.CurrentItem));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Whether the game will accept a change to this setting. The panel writes the answer
        /// onto the row as it refreshes it, which is also what greys the row out on screen.</summary>
        private static bool CanModify(SettingItem item)
        {
            try
            {
                return item.ReadOnlyModifier != null
                    ? !item.ReadOnlyModifier.ReadOnly
                    : Enabled(item.AgeTransform);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static List<InGameSettingCategoryItem> Groups(InGameSettingsPanel panel)
        {
            List<InGameSettingCategoryItem> groups = new List<InGameSettingCategoryItem>();
            try
            {
                if (panel.InGameSettingCategoriesTable == null)
                {
                    return groups;
                }

                foreach (
                    InGameSettingCategoryItem group in
                        panel.InGameSettingCategoriesTable.GetChildren<InGameSettingCategoryItem>(
                            false
                        )
                )
                {
                    if (group != null && group.AgeTransform != null && group.AgeTransform.Visible)
                    {
                        groups.Add(group);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("game menu: reading a settings panel threw: " + e);
            }

            return groups;
        }

        /// <summary>A heading's settings, in the order the panel arranged them. The panel keeps a row
        /// for every setting the game knows and hides the ones this game has no use for, so the
        /// hidden ones are not there to be walked past.</summary>
        private static List<SettingItem> Rows(InGameSettingCategoryItem group)
        {
            List<SettingItem> rows = new List<SettingItem>();
            try
            {
                if (group.SettingItemsTable == null)
                {
                    return rows;
                }

                foreach (SettingItem item in group.SettingItemsTable.GetChildren<SettingItem>(false))
                {
                    if (
                        item != null
                        && item.AgeTransform != null
                        && item.AgeTransform.Visible
                        && item.SettingDefinition != null
                    )
                    {
                        rows.Add(item);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("game menu: reading a settings heading threw: " + e);
            }

            return rows;
        }

        private static string SettingName(SettingItem item)
        {
            try
            {
                return item.SettingDefinition != null
                    ? item.SettingDefinition.Name.ToString()
                    : item.name;
            }
            catch (Exception)
            {
                return "?";
            }
        }

        /// <summary>The transform hugging a setting's title, which is where the game hung its
        /// description and therefore where a tooltip shown for the keyboard belongs.</summary>
        private static AgeTransform TitleTransform(SettingItem item)
        {
            try
            {
                return item.SettingTitle == null ? null : item.SettingTitle.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The tooltip describing the value a setting is currently on; every kind of row
        /// keeps one, under its own name.</summary>
        private static AgeTooltip CurrentValueTooltip(SettingItem item)
        {
            try
            {
                SettingCheckBoxItem checkbox = item as SettingCheckBoxItem;
                if (checkbox != null)
                {
                    return checkbox.CurrentSettingTooltip;
                }

                SettingSliderItem slider = item as SettingSliderItem;
                if (slider != null)
                {
                    return slider.CurrentSettingTooltip;
                }

                SettingDropListItem list = item as SettingDropListItem;
                return list == null ? null : list.CurrentSettingTooltip;
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

        // The item handlers the game runs when a setting is changed with the mouse, and the value a
        // slider keeps to itself. Resolved once: a panel of a dozen rows is rebuilt on every
        // navigation operation.
        private static readonly MethodInfo SettingToggled = OptionsScreen.Handler(
            typeof(SettingCheckBoxItem),
            "OnToggleSettingCb"
        );

        private static readonly PropertyInfo SliderCurrentValue = Property(
            typeof(SettingSliderItem),
            "CurrentValue"
        );

        private static PropertyInfo Property(Type type, string name)
        {
            try
            {
                return type.GetProperty(
                    name,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
                );
            }
            catch (Exception e)
            {
                Log.Warn("game menu: looking up " + type.Name + "." + name + " threw: " + e);
                return null;
            }
        }

        /// <summary>The timer toggle's caption is the label the game itself swaps between "show"
        /// and "change" wordings depending on whether the settings are editable.</summary>
        private static string Timers(GameMenuModalWindow window)
        {
            try
            {
                return AgeText.Label(window.ShowTimerSettingsLabel);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>A caption for a toggle the game only draws as an icon: the first label among
        /// its children, else the mod's own name for it.</summary>
        private static Func<string> ToggleCaption(AgeControlToggle toggle, string fallbackKey)
        {
            AgePrimitiveLabel label = null;
            try
            {
                if (toggle != null && toggle.AgeTransform != null)
                {
                    foreach (
                        AgePrimitiveLabel child in toggle.AgeTransform.GetChildren<AgePrimitiveLabel>(
                            false
                        )
                    )
                    {
                        if (child != null)
                        {
                            label = child;
                            break;
                        }
                    }
                }
            }
            catch (Exception)
            {
                label = null;
            }

            AgePrimitiveLabel found = label;
            return () =>
            {
                string text = AgeText.Label(found);
                return string.IsNullOrEmpty(text) ? ModStrings.Get(fallbackKey) : text;
            };
        }

        // The circle's entries, in the order the game declares them. Ones the window is not
        // currently offering (an item can be hidden per session mode) stay out of the list.
        private static List<GameMenuItem> Items(GameMenuModalWindow window)
        {
            List<GameMenuItem> items = new List<GameMenuItem>();
            try
            {
                if (window.GameMenuItems == null)
                {
                    return items;
                }

                foreach (GameMenuItem item in window.GameMenuItems)
                {
                    if (item != null && item.AgeTransform != null && item.AgeTransform.Visible)
                    {
                        items.Add(item);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("game menu: reading the entries threw: " + e);
            }

            return items;
        }

        /// <summary>Whichever of the two labels the game is showing; the circle decides per side.
        /// </summary>
        private static AgePrimitiveLabel Caption(GameMenuItem item)
        {
            try
            {
                if (item.LabelRight != null && item.LabelRight.AgeTransform.Visible)
                {
                    return item.LabelRight;
                }

                if (item.LabelLeft != null && item.LabelLeft.AgeTransform.Visible)
                {
                    return item.LabelLeft;
                }
            }
            catch (Exception)
            {
                return null;
            }

            return null;
        }

        /// <summary>Whether this is the entry that opens the game's options window - the handler the
        /// window wires to its own Options item.</summary>
        private static bool OpensOptions(AgeControlButton button)
        {
            try
            {
                return button != null && button.OnActivateMethod == "OnOptionsCb";
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static AgeControlButton Button(GameMenuItem item)
        {
            try
            {
                return item.ButtonAgeTransform != null
                    ? item.ButtonAgeTransform.GetComponent<AgeControlButton>()
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static AgeTooltip Tooltip(GameMenuItem item)
        {
            try
            {
                return item.ButtonAgeTransform != null ? item.ButtonAgeTransform.AgeTooltip : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Press an entry the way the engine presses it: replay the object and method its
        /// own mouse handler sends to.</summary>
        private static void Press(AgeControlButton button)
        {
            try
            {
                if (
                    button != null
                    && button.OnActivateObject != null
                    && !string.IsNullOrEmpty(button.OnActivateMethod)
                )
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
                Log.Warn("game menu: pressing an entry threw: " + e);
            }
        }

        /// <summary>Flip a toggle the way clicking it does: the state first, then the handler the
        /// game wired, which reads the state it now finds.</summary>
        private static void Flip(AgeControlToggle toggle)
        {
            try
            {
                toggle.State = !toggle.State;
                if (toggle.OnSwitchObject != null && !string.IsNullOrEmpty(toggle.OnSwitchMethod))
                {
                    toggle.OnSwitchObject.SendMessage(
                        toggle.OnSwitchMethod,
                        toggle.gameObject,
                        SendMessageOptions.DontRequireReceiver
                    );
                }
            }
            catch (Exception e)
            {
                Log.Warn("game menu: flipping a toggle threw: " + e);
            }
        }

        private static bool State(AgeControlToggle toggle)
        {
            try
            {
                return toggle != null && toggle.State;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Whether a control is really on screen: its own visibility and every ancestor's -
        /// the menu hides whole containers to swap between its in-game and main-menu skins.</summary>
        private static bool Visible(AgeControlToggle toggle)
        {
            return AgeWidgets.Visible(AgeWidgets.Transform(toggle));
        }

        private static bool Enabled(GameMenuItem item)
        {
            try
            {
                return item.ButtonAgeTransform != null && item.ButtonAgeTransform.Enable;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool Enabled(AgeControlToggle toggle)
        {
            try
            {
                return toggle != null
                    && toggle.AgeTransform != null
                    && toggle.AgeTransform.Enable;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static readonly Action ReleasePointer = PointerFocus.Release;

        private static GameMenuModalWindow Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<GameMenuModalWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Where this screen is drawn, for the tooltip audit (see
        /// <see cref="ES2Access.Screens.Screen.RootTransform"/>).</summary>
        public override AgeTransform RootTransform
        {
            get { return RootOf(Window()); }
        }
    }
}
