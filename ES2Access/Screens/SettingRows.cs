using System;
using System.Collections.Generic;
using System.Reflection;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using UnityEngine;

// The observer every setting on either page is bound to is the game's own new game screen; the mod's
// adapter for it shares the name, so the two have to coexist here.
using GameNewGame = NewGameScreen;

namespace ES2Access.Screens
{
    /// <summary>
    /// One game setting, as a row.
    ///
    /// The game builds every setting the same way wherever it draws one: a <c>SettingItem</c> prefab
    /// per <c>Gui.ControlType</c>, chosen from the setting database, with a title, a value the player
    /// works, a description on the title and the game's own sentence about the value it is currently
    /// on. The new game lobby draws them in six panels and the advanced-settings modal draws more of
    /// them in group columns, so how one reads and how one is worked lives here rather than on either
    /// screen: the second screen inherited every kind of setting, including the ones its own fixture
    /// does not draw, for nothing.
    ///
    /// What is NOT here is anything about where the rows go. Stops, contexts, regions and the order
    /// rows are declared in are the screen's business, because they are what the player sees.
    /// </summary>
    public static class SettingRows
    {
        /// <summary>How many single steps one coarse slider step is worth.</summary>
        private const int CoarseSteps = 10;

        /// <summary>
        /// Whether the player can actually see a widget.
        ///
        /// Its visibility flag alone is not enough. A pooled table keeps the children it no longer
        /// needs - a competitor slot a lowered empire count dropped - flagged visible and faded to
        /// nothing, so an eighth empire went on being declared, and read, after the count had come back
        /// down to seven. Only the widget's own alpha is asked about: the game fades a read-only
        /// setting to half, and half is still drawn.
        /// </summary>
        public static bool Drawn(AgeTransform widget)
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

        /// <summary>One setting, read and worked by what kind of setting it is. Every kind announces
        /// its title and the game's own sentence about the value it currently holds; the setting's own
        /// description - the tooltip on its title - joins it in the review buffer, where a player can
        /// ask for it without hearing it on every pass.</summary>
        public static void Add(
            GraphBuilder builder,
            SettingItem item,
            string key,
            TextFieldEditor editor
        )
        {
            AgeTransform widget = item == null ? null : item.AgeTransform;
            if (widget == null || !Drawn(widget))
            {
                return;
            }

            SettingItem it = item;
            Func<string> label = () => AgeText.Label(it.SettingTitle);
            Func<bool> enabled = () => AgeWidgets.Operable(it.AgeTransform);
            AgeTooltip caption = AgeWidgets.Raw(TransformOf(item.SettingTitle));
            ControlId id = ControlId.Referenced(item, key);

            SettingSliderItem slider = item as SettingSliderItem;
            if (slider != null)
            {
                SettingSliderItem s = slider;
                AgeTooltip value = slider.CurrentSettingTooltip;
                NodeVtable vtable = GraphNodes.Slider(
                    label,
                    () => AgeText.Label(s.SelectedSettingItemLabel),
                    (sign, large) => Slide(s, sign, large),
                    enabled
                );
                vtable.Sections = RowSections(caption, value);
                PointAtTooltip(vtable, value);
                builder.AddItem(id, vtable);
                return;
            }

            SettingDropListItem list = item as SettingDropListItem;
            if (list != null && list.DropList != null)
            {
                AddCombo(builder, list.DropList, label, caption, key);
                return;
            }

            SettingCheckBoxItem box = item as SettingCheckBoxItem;
            if (box != null && box.Toggle != null)
            {
                SettingCheckBoxItem b = box;
                AgeTooltip value = box.CurrentSettingTooltip;
                NodeVtable vtable = GraphNodes.Checkbox(
                    label,
                    () => b.Toggle.State,
                    () => AgeWidgets.Toggle(b.Toggle),
                    enabled
                );
                vtable.Sections = RowSections(caption, value);
                AgeWidgets.Point(vtable, box.Toggle);
                builder.AddItem(id, vtable);
                return;
            }

            SettingTextFieldItem field = item as SettingTextFieldItem;
            if (field != null && field.TextField != null)
            {
                AddTextField(
                    builder,
                    field.TextField,
                    label,
                    caption,
                    field,
                    SettingFieldGainFocus,
                    id,
                    editor
                );
                return;
            }

            // A kind of setting nothing here knows how to work: still named, still readable, and
            // silent about being something the player can do anything with.
            NodeVtable readOnly = GraphNodes.Readout(label, () => null, null, caption);
            PointAtTooltip(readOnly, caption);
            builder.AddItem(id, readOnly);
        }

        /// <summary>The setting's own name, for a row key.</summary>
        public static string SettingKey(SettingItem item)
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

        // ---- working a slider ----

        /// <summary>
        /// Move a setting slider one step, or ten.
        ///
        /// The slider's own mouse handler cannot be replayed - it works out the new value from where
        /// the cursor is - so this is the rest of what that handler does, with the step counted instead
        /// of measured: the value the other settings would allow (a galaxy too small for twelve empires
        /// pulls the count back), the registry entry the setting asks to be saved in, and the lobby
        /// screen's own <c>OnSettingModified</c>, which writes the lobby data and refreshes the row.
        /// Which step the slider is on is asked of the lobby data rather than of the widget, because
        /// that is where the game itself reads it back from (<c>SettingSliderItem.Refresh</c> :77-121).
        ///
        /// The observer is the new game screen wherever a setting is drawn: the advanced-settings modal
        /// binds its own items to it too (<c>AdvancedSettingsModalWindow.BindAdvancedSettingsGroup</c>
        /// :171-175), so one path serves both.
        /// </summary>
        private static void Slide(SettingSliderItem slider, int sign, bool large)
        {
            try
            {
                GameSettingDefinition definition = slider.SettingDefinition;
                if (definition == null || definition.ItemDefinitions == null)
                {
                    return;
                }

                int count = definition.ItemDefinitions.Length;
                int index = CurrentIndex(definition);
                if (count == 0 || index < 0)
                {
                    return;
                }

                int target = Mathf.Clamp(index + sign * (large ? CoarseSteps : 1), 0, count - 1);
                if (target == index)
                {
                    return;
                }

                Apply(definition, definition.ItemDefinitions[target]);
            }
            catch (Exception e)
            {
                Log.Warn("settings: moving a setting slider threw: " + e);
            }
        }

        /// <summary>Which of a setting's values is in force, as an index into its own list.</summary>
        private static int CurrentIndex(GameSettingDefinition definition)
        {
            GameNewGame window = NewGameScreen.Window();
            Amplitude.Unity.Session.Session session = window == null ? null : window.Session;
            if (session == null)
            {
                return -1;
            }

            string value = session.GetLobbyData<string>(definition.LobbyDataName);
            if (string.IsNullOrEmpty(value))
            {
                return -1;
            }

            GameSettingDefinition.ItemDefinition current = definition.GetItemDefinitionByName(value);
            return current == null ? -1 : Array.IndexOf(definition.ItemDefinitions, current);
        }

        private static void Apply(
            GameSettingDefinition definition,
            GameSettingDefinition.ItemDefinition value
        )
        {
            GameNewGame window = NewGameScreen.Window();
            IGameSettingService settings =
                Amplitude.Unity.Framework.Services.GetService<IGameSettingService>();
            if (window == null || settings == null)
            {
                return;
            }

            GameSettingDefinition.ItemDefinition corrected =
                settings.GetCorrectedSettingItemIfConstrainedByOthers(definition, value);
            if (corrected == null || !window.CanModifyGameSettings(definition))
            {
                return;
            }

            if (definition.SaveInRegistry)
            {
                Amplitude.Unity.Framework.Application.Registry.SetValue(
                    definition.RegistryPath,
                    corrected.Name
                );
            }

            window.OnSettingModified(definition, corrected);
        }

        // ---- the other shapes a control takes ----

        /// <summary>A button: what it is called, or - for one the game drew as a symbol - what its
        /// tooltip calls it, in which case the tooltip is not said twice.</summary>
        public static void AddButton(GraphBuilder builder, AgeControlButton button, string key)
        {
            AgeTransform widget = AgeWidgets.Transform(button);
            if (button == null || !Drawn(widget))
            {
                return;
            }

            AgeControlButton it = button;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            bool wordless = string.IsNullOrEmpty(AgeWidgets.TextOf(widget));
            NodeVtable vtable = GraphNodes.Button(
                () => ButtonText(it, tooltip),
                () => AgeWidgets.Press(it),
                () => AgeWidgets.Operable(AgeWidgets.Transform(it)),
                tooltip,
                // A button the game drew as a symbol is NAMED by its tooltip, and the label has just
                // said it: reviewable, never said twice.
                wordless ? TooltipMode.None : (TooltipMode?)null
            );
            AgeWidgets.Point(vtable, it);
            builder.AddItem(ControlId.Referenced(button, key), vtable);
        }

        /// <summary>A bar of buttons as ONE row, left to right the way they are drawn - the shape every
        /// screen's cancel-and-confirm row has. <paramref name="widgets"/> is whatever the caller found
        /// in the band; it is ordered here, so a caller only has to say which widgets are in it.
        /// </summary>
        public static void AddButtonRow(
            GraphBuilder builder,
            List<AgeTransform> widgets,
            string keyPrefix
        )
        {
            List<AgeTransform> drawn = new List<AgeTransform>();
            for (int i = 0; widgets != null && i < widgets.Count; i++)
            {
                if (Drawn(widgets[i]) && AgeWidgets.Button(widgets[i]) != null)
                {
                    drawn.Add(widgets[i]);
                }
            }

            if (drawn.Count == 0)
            {
                return;
            }

            // Opened only once something is going to go in it: an empty row is a build-time throw,
            // and a throw out of Build empties the whole screen.
            builder.StartRow();
            foreach (List<AgeTransform> row in AgeLayout.Rows(drawn, Itself))
            {
                for (int i = 0; i < row.Count; i++)
                {
                    AddButton(builder, AgeWidgets.Button(row[i]), keyPrefix + Name(row[i]));
                }
            }

            builder.EndRow();
        }

        private static readonly Func<AgeTransform, AgeTransform> Itself = widget => widget;

        private static string Name(AgeTransform widget)
        {
            try
            {
                return widget == null ? "?" : widget.name;
            }
            catch (Exception)
            {
                return "?";
            }
        }

        private static string ButtonText(AgeControlButton button, AgeTooltip tooltip)
        {
            string text = AgeWidgets.TextOf(AgeWidgets.Transform(button));
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            IList<string> lines = AgeText.Lines(AgeText.Tooltip(tooltip));
            return lines.Count > 0 ? lines[0] : null;
        }

        /// <summary>A text box with no caption of its own - the chat box, an empire's name.</summary>
        public static void AddTextField(
            GraphBuilder builder,
            AgeControlTextField field,
            string key,
            TextFieldEditor editor
        )
        {
            AddTextField(
                builder,
                field,
                null,
                AgeWidgets.Raw(AgeWidgets.Transform(field)),
                null,
                null,
                field == null ? null : ControlId.Referenced(field, key),
                editor
            );
        }

        /// <summary>
        /// A box of text the game lets the player type in - or, where the game has turned it off, the
        /// same box saying so.
        ///
        /// It is declared as a field either way rather than as a line of text, because that is what the
        /// player is looking at: the chat box in single player says "Chat is disabled in single player"
        /// and an AI empire's name box says "AI", and both are boxes the player would otherwise go
        /// hunting for a way into. A box that is refusing announces so and swallows the key.
        ///
        /// While the game holds the keyboard the value says nothing at all: the screen reader is
        /// already echoing what is being typed, and re-reading the whole field after every letter would
        /// bury it.
        /// </summary>
        public static void AddTextField(
            GraphBuilder builder,
            AgeControlTextField field,
            Func<string> label,
            AgeTooltip tooltip,
            object owner,
            MethodInfo gainFocus,
            ControlId id,
            TextFieldEditor editor
        )
        {
            Cell cell = TextFieldCell(field, label, tooltip, owner, gainFocus, id, editor);
            if (cell != null)
            {
                builder.AddItem(cell.Id, cell.Vtable);
            }
        }

        /// <summary>The same field as a <see cref="Cell"/>, for a screen that gathers its controls and
        /// emits them in the rows the game drew them in rather than one at a time. Null for a field the
        /// game is not drawing.</summary>
        public static Cell TextFieldCell(
            AgeControlTextField field,
            Func<string> label,
            AgeTooltip tooltip,
            object owner,
            MethodInfo gainFocus,
            ControlId id,
            TextFieldEditor editor
        )
        {
            AgeTransform widget = AgeWidgets.Transform(field);
            if (field == null || id == null || editor == null || !Drawn(widget))
            {
                return null;
            }

            AgeControlTextField it = field;
            object host = owner;
            MethodInfo handler = gainFocus;
            ControlId row = id;
            TextFieldEditor editing = editor;
            Func<bool> enabled = () => AgeWidgets.Operable(AgeWidgets.Transform(it));

            // The caption's tooltip and the field's own, the same way every other row here treats a
            // pair: declared in drawn order, and the projection says which of them speaks.
            AgeTooltip own = AgeWidgets.Raw(widget);
            NodeVtable vtable = GraphNodes.EditField(
                label,
                () => TextFieldEditor.Typing(it) ? null : FieldText(it),
                () => editing.Request(it, host, handler, row),
                enabled
            );
            vtable.Sections = RowSections(tooltip, own, () => FieldText(it));
            AgeWidgets.PointAt(vtable, widget);
            return new Cell { Widget = widget, Id = row, Vtable = vtable };
        }

        public static string FieldText(AgeControlTextField field)
        {
            try
            {
                return field == null || field.Label == null
                    ? null
                    : AgeText.Clean(field.Label.Text);
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---- readouts ----

        /// <summary>How far down a readout to look for the tooltips that belong to it.</summary>
        private const int TooltipDepth = 3;

        // Reused rather than allocated per call: readouts are declared on every build.
        private static readonly List<AgeTooltip> Scratch = new List<AgeTooltip>();

        /// <summary>A line the player reads but does not work - the star rating on a portrait, a
        /// faction's affinity, one of its traits. What it says is whatever the game drew in it; what it
        /// means is in the tooltips hanging under it, the last of which - the one belonging to the
        /// value rather than to the icon that captions it - is the one announced, with all of them in
        /// the review buffer.</summary>
        public static void AddReadout(GraphBuilder builder, AgeTransform widget, string key)
        {
            if (widget == null || !Drawn(widget))
            {
                return;
            }

            AgeTransform it = widget;
            AgeTooltip tooltip = LastTooltip(widget);
            NodeVtable vtable = GraphNodes.Readout(() => null, () => AgeWidgets.TextOf(it), null, null);
            vtable.Sections = RowSections(it, tooltip);
            AgeWidgets.PointAt(vtable, tooltip != null ? TransformOf(tooltip) : it);
            builder.AddItem(ControlId.Referenced(widget, key), vtable);
        }

        /// <summary>The last tooltip drawn under a readout - the one belonging to the value rather than
        /// to the icon that captions it.</summary>
        public static AgeTooltip LastTooltip(AgeTransform widget)
        {
            Scratch.Clear();
            CollectTooltips(widget, Scratch, TooltipDepth);
            return Scratch.Count == 0 ? null : Scratch[Scratch.Count - 1];
        }

        /// <summary>Every tooltip in a row as declared sections, in the order the row was built. A row
        /// can carry more than one - the icon's explanation of what the line is and the value's
        /// description of what it says - and the projection decides which of them speaks; all of them
        /// are reviewable. Collected at declare time because a section's MODE is structural, and the
        /// row is declared afresh every frame anyway.</summary>
        /// <summary>
        /// Every tooltip drawn anywhere in a row, where exactly ONE of them is the one worth hearing
        /// and the rest are what the buffer keeps: a faction card's refusal reason beside the
        /// difficulty rating printed on it, a readout's value beside the icon that captions it.
        ///
        /// The row says which, because only the row knows. The projection's "the last short one
        /// speaks" rule is right for a caption-then-value PAIR, where the value is the last thing
        /// drawn; it is wrong for a card, where what comes after the important tooltip is a badge.
        /// <paramref name="said"/> null - a row whose own widget carries no tooltip - means none of
        /// them speaks and all of them are reviewable, which is the honest reading of a row that has
        /// nothing at its own level to explain itself with.
        /// </summary>
        public static IList<NodeSection> RowSections(
            AgeTransform widget,
            AgeTooltip said,
            TooltipMode? mode = null
        )
        {
            List<AgeTooltip> found = new List<AgeTooltip>();
            CollectTooltips(widget, found, TooltipDepth);
            List<NodeSection> sections = null;
            for (int i = 0; i < found.Count; i++)
            {
                NodeSection section = GraphNodes.TooltipSection(
                    found[i],
                    said != null && found[i] == said ? mode : (TooltipMode?)TooltipMode.None
                );
                if (section == null)
                {
                    continue;
                }

                if (sections == null)
                {
                    sections = new List<NodeSection>(found.Count);
                }

                sections.Add(section);
            }

            return sections;
        }

        private static void CollectTooltips(AgeTransform widget, List<AgeTooltip> into, int depth)
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

                AgeTooltip tooltip = widget.AgeTooltip;
                if (tooltip != null)
                {
                    into.Add(tooltip);
                }

                IList<AgeTransform> children = widget.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    CollectTooltips(children[i], into, depth - 1);
                }
            }
            catch (Exception) { }
        }

        // ---- drop lists ----

        /// <summary>
        /// A list the player opens, as a row - the one place any screen builds one.
        ///
        /// Two tooltips again: the caption the game drew beside the list explains what the setting IS,
        /// and the list's own current entry explains what it is SET TO. The value's is what gets
        /// announced and both fill the buffer. Every list on every page goes through here so that
        /// none of them can quietly come out without either.
        /// </summary>
        public static void AddCombo(
            GraphBuilder builder,
            AgeControlDropList list,
            Func<string> label,
            AgeTooltip caption,
            string key
        )
        {
            AgeTransform widget = AgeWidgets.Transform(list);
            if (list == null || !Drawn(widget))
            {
                return;
            }

            AgeControlDropList it = list;
            AgeTooltip value = AgeWidgets.Raw(TransformOf(OptionsScreen.LabelIn(widget)));
            AgeTooltip said = value ?? caption;
            NodeVtable vtable = GraphNodes.ComboBox(
                label ?? Nothing,
                () => DropListText(it),
                () => OpenList(it, label == null ? null : label()),
                () => AgeWidgets.Operable(AgeWidgets.Transform(it))
            );
            // Activating this one opens a list rather than changing the setting: the list that opens
            // says where it starts.
            vtable.StateText = null;
            vtable.Sections = RowSections(caption, value);
            AgeWidgets.PointAt(vtable, said != null ? TransformOf(said) : widget);
            builder.AddItem(ControlId.Referenced(list, key), vtable);
        }

        private static readonly Func<string> Nothing = () => null;

        public static void OpenList(AgeControlDropList list, string title)
        {
            AgeControlDropList it = list;
            DropListScreen.Open(list, title, index => AgeWidgets.Choose(it, index));
        }

        /// <summary>What the closed list is set to: the label it is rendering, which the game has
        /// already localized. The raw entry table holds localization keys, so it is only the fallback
        /// and is localized on the way out.</summary>
        public static string DropListText(AgeControlDropList list)
        {
            try
            {
                string rendered = AgeText.Label(OptionsScreen.LabelIn(list.CurrentItem));
                if (!string.IsNullOrEmpty(rendered))
                {
                    return rendered;
                }

                int index = list.SelectedItem;
                string label = index >= 0 && index < list.LabelTable.Count
                    ? AgeText.Clean(list.LabelTable[index])
                    : null;

                // A list drawn as bare colour swatches renders no label at all: what it is set to is
                // the colour itself, which the game's palette names (see EmpireColors).
                return string.IsNullOrEmpty(label)
                    ? DropListScreen.ColorName(list, index)
                    : label;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---- shared plumbing ----

        /// <summary>
        /// A row's content, declared once: what the row DRAWS (a text field's own text), then the
        /// setting's own description, then the game's sentence about the value it is currently on -
        /// drawn order, which is the order the buffer reads them in.
        ///
        /// Which of the two tooltips the focus readout says is not decided here and never was a
        /// screen's business: the projection announces the last one that is short and mentions any
        /// that is long (<see cref="TooltipParts"/>).
        /// </summary>
        public static IList<NodeSection> RowSections(
            AgeTooltip caption,
            AgeTooltip value,
            Func<string> drawn = null
        )
        {
            Func<string> text = drawn;
            return GraphNodes.Sections(
                text == null ? null : NodeSection.Buffer(() => AgeText.Lines(text())),
                GraphNodes.TooltipSection(caption),
                value == caption ? null : GraphNodes.TooltipSection(value)
            );
        }

        public static void Append(List<string> lines, Func<IList<string>> source)
        {
            if (source != null)
            {
                Append(lines, source());
            }
        }

        public static void Append(List<string> lines, IList<string> source)
        {
            for (int i = 0; source != null && i < source.Count; i++)
            {
                if (!string.IsNullOrEmpty(source[i]) && !lines.Contains(source[i]))
                {
                    lines.Add(source[i]);
                }
            }
        }

        /// <summary>Point at the widget the tooltip hangs on rather than at the row: a row's tooltip
        /// routinely belongs to one label inside it, and pointing at the container draws nothing.
        /// </summary>
        public static void PointAtTooltip(NodeVtable vtable, AgeTooltip tooltip)
        {
            AgeTooltip it = tooltip;
            vtable.OnFocusVisual = () =>
                PointerFocus.MoveTo(TransformOf(it), it, TransformOf(it));
            vtable.OnBlurVisual = AgeWidgets.ReleasePointer;
        }

        public static AgeTransform TransformOf(AgeTooltip tooltip)
        {
            try
            {
                return tooltip == null ? null : tooltip.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static AgeTransform TransformOf(AgePrimitiveLabel label)
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

        // The handler a click on a setting's text field reaches when it takes the keyboard: it is what
        // clears the placeholder. Resolved once - a lookup per row per navigation operation would be
        // paid for nothing.
        private static readonly MethodInfo SettingFieldGainFocus = OptionsScreen.Handler(
            typeof(SettingTextFieldItem),
            "OnTextFieldGainFocusCb"
        );
    }

    /// <summary>
    /// The deferred hand-over of the keyboard to one of the game's text editors, held per screen so
    /// that a reload takes it with the screen and two screens never fight over one request.
    ///
    /// The wait is the whole point. The engine delivers key events to the focused control in its own
    /// LateUpdate, after the mod's frame, and a text field's answer to Return is to hand the focus
    /// straight back - which for these fields is also what commits what is in them. Handing over
    /// during the frame Enter was pressed therefore gives the field that very Enter: the editor opens
    /// and closes inside one frame with nothing typed. Waiting for a frame on which nothing new went
    /// down costs the player nothing, and is the same wait the save-name box and the key-capture rows
    /// make.
    /// </summary>
    public sealed class TextFieldEditor
    {
        private AgeControlTextField _field;
        private ControlId _row;
        private object _owner;
        private MethodInfo _gainFocus;

        /// <summary>Ask for the game's editor, and say so - entering an editor is not a thing a player
        /// can be left to infer from silence.</summary>
        public void Request(
            AgeControlTextField field,
            object owner,
            MethodInfo gainFocus,
            ControlId row
        )
        {
            if (_field != null)
            {
                return;
            }

            _field = field;
            _owner = owner;
            _gainFocus = gainFocus;
            _row = row;
            Voice.Say(ModStrings.Get(ModStrings.RenameTypePrompt), true);
        }

        /// <summary>Whether an editor has been asked for and the keyboard has not changed hands yet.
        /// The screen that owns this editor answers <c>CapturesRawInput</c> with it: during the wait
        /// the mod's keys are still live, and what the player types next is meant for the field.
        /// </summary>
        public bool Pending
        {
            get { return _field != null; }
        }

        /// <summary>Called from the owning screen's per-frame update.</summary>
        public void Update()
        {
            AgeControlTextField field = _field;
            if (field == null)
            {
                return;
            }

            // Moving off the row during the wait is the player changing their mind, and the request has
            // to go with them - otherwise the keyboard would be handed to a field they have left.
            if (!OnRow(_row))
            {
                Cancel();
                return;
            }

            // Spelled out: the game has its own Input in the global namespace.
            if (UnityEngine.Input.anyKeyDown)
            {
                return;
            }

            object owner = _owner;
            MethodInfo gainFocus = _gainFocus;
            Cancel();
            try
            {
                AgeManager age = AgeManager.Instance;
                if (age == null || !AgeWidgets.Operable(AgeWidgets.Transform(field)))
                {
                    return;
                }

                age.FocusedControl = field;
                OptionsScreen.Call(gainFocus, owner, OptionsScreen.NoSender);
            }
            catch (Exception e)
            {
                Log.Warn("settings: opening a text editor threw: " + e);
            }
        }

        public void Cancel()
        {
            _field = null;
            _owner = null;
            _gainFocus = null;
            _row = null;
        }

        /// <summary>Whether the game currently has the keyboard on this field - asked of the engine's
        /// own focus, so an edit the game ended is over here the same instant.</summary>
        public static bool Typing(AgeControlTextField field)
        {
            try
            {
                AgeManager age = AgeManager.Instance;
                return age != null && ReferenceEquals(age.FocusedControl, field);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool OnRow(ControlId id)
        {
            try
            {
                GraphNavigator navigator = ModEntry.Navigator;
                GraphNode node = navigator == null ? null : navigator.CurrentNode;
                return id != null && node != null && id.Equals(node.Id);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
