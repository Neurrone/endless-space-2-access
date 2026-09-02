using System;
using System.Collections.Generic;
using System.Reflection;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
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
        ///
        /// No longer the EXISTENCE gate for the rows below - every one of them stands on the widget it
        /// was read off, and <see cref="NodeGate"/> asks this question of that widget and of its whole
        /// ancestry. What is left here is banding input and the content readings other screens make of
        /// it.
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
            if (widget == null)
            {
                return;
            }

            SettingItem it = item;
            Func<string> label = () => AgeText.Label(it.SettingTitle);
            Func<bool> enabled = () => AgeWidgets.Operable(it.AgeTransform);
            AgeTooltip caption = AgeWidgets.Raw(AgeWidgets.Transform(item.SettingTitle));
            ControlId id = ControlId.For(item, key);
            // The entries the sink hands back for whichever branch below builds this row: a setting has
            // two hover targets and the row points at one of them.
            List<TooltipChildren.Dossier> dossiers = new List<TooltipChildren.Dossier>(1);

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
                vtable.Sections = RowSections(caption, value, null, dossiers);
                SayValueTooltip(vtable, value);
                PointAtTooltip(vtable, value);
                TooltipChildren.Declare(builder, Nodes.Drawn(id, vtable, item), key, dossiers);
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
                vtable.Sections = RowSections(caption, value, null, dossiers);
                SayValueTooltip(vtable, value);
                AgeWidgets.Point(vtable, box.Toggle);
                TooltipChildren.Declare(builder, Nodes.Drawn(id, vtable, item), key, dossiers);
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
            builder.AddItem(Nodes.Drawn(id, readOnly, item));
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
        /// tooltip calls it, in which case the readout drops that line from the tooltip it
        /// announces.</summary>
        public static void AddButton(GraphBuilder builder, AgeControlButton button, string key)
        {
            AgeTransform widget = AgeWidgets.Transform(button);
            if (button == null)
            {
                return;
            }

            AgeControlButton it = button;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            NodeVtable vtable = GraphNodes.Button(
                () => ButtonText(it, tooltip),
                () => AgeWidgets.Press(it),
                () => AgeWidgets.Operable(AgeWidgets.Transform(it)),
                tooltip
            );
            AgeWidgets.Point(vtable, it);
            builder.AddItem(Nodes.Drawn(ControlId.For(button, key), vtable, button));
        }

        /// <summary>A bar of buttons, one node per row, in the order they are drawn - the shape every
        /// out-game page's cancel-and-confirm band has. The buttons are peers of one kind and which of
        /// them the layout put beside which is a fact about the box, not about the choices, so the
        /// player walks the whole band with one key. <paramref name="widgets"/> is whatever the caller
        /// found in the band; it is ordered here, so a caller only has to say which widgets are in it.
        /// </summary>
        public static void AddButtons(
            GraphBuilder builder,
            List<AgeTransform> widgets,
            string keyPrefix
        )
        {
            List<AgeTransform> drawn = new List<AgeTransform>();
            for (int i = 0; widgets != null && i < widgets.Count; i++)
            {
                // Banding input: the band's rows come out of these RECTANGLES, and a retired button's
                // stale rectangle splits one drawn row into two. The gate, which only sees finished
                // nodes, is too late for that - so the ghost never reaches the layout.
                if (Drawn(widgets[i]) && AgeWidgets.Button(widgets[i]) != null)
                {
                    drawn.Add(widgets[i]);
                }
            }

            foreach (List<AgeTransform> row in AgeLayout.Rows(drawn, Itself))
            {
                for (int i = 0; i < row.Count; i++)
                {
                    AddButton(builder, AgeWidgets.Button(row[i]), keyPrefix + row[i].name);
                }
            }
        }

        private static readonly Func<AgeTransform, AgeTransform> Itself = widget => widget;

        private static string ButtonText(AgeControlButton button, AgeTooltip tooltip)
        {
            string text = AgeWidgets.TextOf(AgeWidgets.Transform(button));
            return string.IsNullOrEmpty(text) ? CardActions.FirstLine(tooltip) : text;
        }

        /// <summary>A text box with no caption of its own - the chat box, an empire's name.</summary>
        public static void AddTextField(
            GraphBuilder builder,
            AgeControlTextArea field,
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
                field == null ? null : ControlId.For(field, key),
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
            AgeControlTextArea field,
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
                TooltipChildren.Declare(
                    builder,
                    Nodes.Drawn(cell.Id, cell.Vtable, cell.Widget),
                    cell.Key,
                    cell.Dossiers
                );
            }
        }

        /// <summary>The same field as a <see cref="Cell"/>, for a screen that gathers its controls and
        /// emits them in the rows the game drew them in rather than one at a time. Null where there is
        /// no field, no identity or no editor to work it with.</summary>
        public static Cell TextFieldCell(
            AgeControlTextArea field,
            Func<string> label,
            AgeTooltip tooltip,
            object owner,
            MethodInfo gainFocus,
            ControlId id,
            TextFieldEditor editor,
            TextEditOptions options = null
        )
        {
            AgeTransform widget = AgeWidgets.Transform(field);
            if (field == null || id == null || editor == null)
            {
                return null;
            }

            TextEditOptions how = options;

            AgeControlTextArea it = field;
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
                () => editing.Request(it, host, handler, row, how),
                enabled
            );
            List<TooltipChildren.Dossier> dossiers = new List<TooltipChildren.Dossier>(1);
            vtable.Sections = RowSections(tooltip, own, () => FieldText(it), dossiers);
            AgeWidgets.PointAt(vtable, widget);
            return new Cell
            {
                Widget = widget,
                Id = row,
                Vtable = vtable,
                Dossiers = dossiers,
                Key = row.StructuralKey as string,
            };
        }

        public static string FieldText(AgeControlTextArea field)
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

        /// <summary>Empty the scratch - mod teardown. What the last read left in it is the game's
        /// own tooltips.</summary>
        public static void Forget()
        {
            Scratch.Clear();
        }

        /// <summary>A line the player reads but does not work - the star rating on a portrait, a
        /// faction's affinity, one of its traits. What it says is whatever the game drew in it; what it
        /// means is in the tooltips hanging under it, the last of which - the one belonging to the
        /// value rather than to the icon that captions it - is the one announced, with all of them in
        /// the review buffer.</summary>
        public static void AddReadout(GraphBuilder builder, AgeTransform widget, string key)
        {
            if (widget == null)
            {
                return;
            }

            AgeTransform it = widget;
            AgeTooltip tooltip = LastTooltip(widget);
            NodeVtable vtable = GraphNodes.Readout(() => null, () => AgeWidgets.TextOf(it), null, null);
            List<TooltipChildren.Dossier> dossiers = new List<TooltipChildren.Dossier>(2);
            vtable.Sections = RowSections(it, tooltip, dossiers);
            AgeWidgets.PointAt(vtable, tooltip != null ? AgeWidgets.TooltipOwner(tooltip) : it);
            TooltipChildren.Declare(
                builder,
                Nodes.Drawn(ControlId.For(widget, key), vtable, widget),
                key,
                dossiers
            );
        }

        /// <summary>The last tooltip drawn under a readout - the one belonging to the value rather than
        /// to the icon that captions it.</summary>
        public static AgeTooltip LastTooltip(AgeTransform widget)
        {
            Scratch.Clear();
            CollectTooltips(widget, Scratch, TooltipDepth);
            return Scratch.Count == 0 ? null : Scratch[Scratch.Count - 1];
        }

        /// <summary>
        /// Every tooltip drawn anywhere in a row, where exactly ONE of them is the one worth hearing
        /// and the rest are what the buffer keeps: a faction card's refusal reason beside the
        /// difficulty rating printed on it, a readout's value beside the icon that captions it.
        /// Collected at declare time because a section's MODE is structural, and the row is declared
        /// afresh every frame anyway.
        ///
        /// The row says WHICH, because only the row knows; how loudly that one reads is still the
        /// tooltip's own kind to answer. The door's "the last one is the node's own" rule is right for a
        /// caption-then-value PAIR, where the value is the last thing drawn; it is wrong for a card,
        /// where what comes after the important tooltip is a badge.
        /// <paramref name="said"/> null - a row whose own widget carries no tooltip - means none of
        /// them speaks and all of them are reviewable, which is the honest reading of a row that has
        /// nothing at its own level to explain itself with.
        /// </summary>
        public static IList<NodeSection> RowSections(
            AgeTransform widget,
            AgeTooltip said,
            List<TooltipChildren.Dossier> into
        )
        {
            List<AgeTooltip> found = new List<AgeTooltip>();
            CollectTooltips(widget, found, TooltipDepth);
            // Drawn order, with the one the row POINTS AT last, which is what the sink calls the row's
            // own. Everything else was a reviewed section - a promise the row could never fill for a
            // renderer-assembled one and a paragraph the player cannot step through for a written one -
            // and is now an entry of its own, aimed at the widget a mouse would have pointed at.
            List<AgeTooltip> ordered = new List<AgeTooltip>(found.Count + 1);
            AgeTooltip own = null;
            for (int i = 0; i < found.Count; i++)
            {
                if (said != null && found[i] == said)
                {
                    own = found[i];
                    continue;
                }

                ordered.Add(found[i]);
            }

            ordered.Add(own);
            TooltipChildren.Carried carried = TooltipChildren.Split(ordered);
            Keep(into, carried.Children);
            return GraphNodes.Sections(GraphNodes.TooltipSection(carried.Own));
        }

        /// <summary>The sink's entries into the caller's list, where it kept one.</summary>
        public static void Keep(
            List<TooltipChildren.Dossier> into,
            List<TooltipChildren.Dossier> children
        )
        {
            if (into != null && children != null)
            {
                into.AddRange(children);
            }
        }

        private static void CollectTooltips(AgeTransform widget, List<AgeTooltip> into, int depth)
        {
            AgeWidgets.EffectiveTooltips(
                widget,
                into,
                TooltipReach.Own | TooltipReach.Descendants,
                depth
            );
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
            if (list == null)
            {
                return;
            }

            AgeControlDropList it = list;

            // What the list is SET TO explains itself on the label the list is drawing - and, where a
            // prefab hangs nothing there, on the list itself. The engine means to keep the closed
            // control's tooltip in step with the selection (AgeControlDropList.SelectedItem :141-158
            // hands the selected item's table entry to SetTooltip), and in this game it never
            // arrives: the target overload SENDS "OnSetTooltipTarget" (:547-557) and no component in
            // either assembly receives that message, while the string overload ignores the receiver
            // it was given (:559-569). So this second source is dead weight in ES2 today and is asked
            // anyway - it costs one field read, it is what the engine says the answer is, and a
            // prefab that carries a written tooltip on the list gets read instead of ignored. The
            // game's own behaviour is not "fixed" here: nothing writes that tooltip, so nothing is
            // invented for it.
            AgeTooltip value =
                AgeWidgets.Raw(AgeWidgets.Transform(OptionsScreen.LabelIn(widget))) ?? AgeWidgets.Raw(widget);
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
            List<TooltipChildren.Dossier> dossiers = new List<TooltipChildren.Dossier>(1);
            vtable.Sections = RowSections(caption, value, null, dossiers);
            AgeWidgets.PointAt(vtable, said != null ? AgeWidgets.TooltipOwner(said) : widget);
            TooltipChildren.Declare(
                builder,
                Nodes.Drawn(ControlId.For(list, key), vtable, list),
                key,
                dossiers
            );
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
        /// A row's content, declared once: what the row DRAWS (a text field's own text), then the ONE
        /// tooltip a hover on the row's value raises.
        ///
        /// A setting row has TWO hover targets and always did - the caption beside it explains what the
        /// setting IS, and the value's own explains what it is SET TO - and the game draws whichever is
        /// innermost under the pointer. The row points at the value, so the caption's sentence was words
        /// on the row that no gesture the row offers would ever draw. It becomes an entry of its own
        /// through the sink, named by the widget a mouse would have pointed at.
        ///
        /// Measured 2026-08-28: on this fixture exactly one row in the whole game carries a pair that
        /// differs - the ship designer's hull list, whose group holds %ShipStatHullDescription while the
        /// list itself holds the ShipHull dossier. Every settings row on the options page and the pause
        /// menu has one tooltip or two that are the same object, so they are unchanged.
        /// </summary>
        /// <param name="own">Which of the pair the row POINTS AT, where that is not the value - the
        /// pause menu's setting rows aim at the caption on the title, and a row's own tooltip is
        /// whichever one the pointer will make the game draw, never whichever one reads best.</param>
        public static IList<NodeSection> RowSections(
            AgeTooltip caption,
            AgeTooltip value,
            Func<string> drawn,
            List<TooltipChildren.Dossier> into,
            AgeTooltip own = null
        )
        {
            AgeTooltip mine = own ?? value ?? caption;
            AgeTooltip other = AgeWidgets.SameTooltip(mine, caption) ? value : caption;
            List<AgeTooltip> gathered = new List<AgeTooltip>(2);
            if (other != null && !AgeWidgets.SameTooltip(other, mine))
            {
                gathered.Add(other);
            }

            gathered.Add(mine);
            TooltipChildren.Carried carried = TooltipChildren.Split(gathered);
            Keep(into, carried.Children);

            Func<string> text = drawn;
            return GraphNodes.Sections(
                text == null ? null : NodeSection.Buffer(() => AgeText.Lines(text())),
                GraphNodes.TooltipSection(carried.Own)
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

        /// <summary>
        /// Say the game's own sentence about the value again the moment the player changes it.
        ///
        /// Every setting the game draws keeps TWO tooltips: what the setting IS, on its title, and one
        /// the game rewrites for every value the setting lands on (<c>SettingSliderItem.CurrentValue</c>
        /// :52-55, <c>SettingCheckBoxItem.Refresh</c> :50-53). The second is the ANSWER to the change the
        /// player has just made - what a slow game costs in turns - so a step that reported only the
        /// value's name sent the player to the review buffer for the half they had asked for by pressing
        /// the key.
        ///
        /// Wrapped around the control's own acted state, which keeps the refusal swallow: a setting the
        /// game will not let the player move still says nothing at all. Asked through the tooltip's own
        /// mode, so a description the game assembles at draw time stays in the review buffer where the
        /// long ones live, exactly as the focus readout leaves it - and asked at speak time, because the
        /// mode is a per-frame answer.
        ///
        /// A setting whose tooltip does NOT move with its value gets nothing here: the options screen's
        /// sliders and tick boxes carry one tooltip written once at load (<c>OptionItem.Load</c> :23), and
        /// repeating it on every keypress would say what has not changed.
        /// </summary>
        public static void SayValueTooltip(NodeVtable vtable, AgeTooltip value)
        {
            Func<string> state = vtable == null ? null : vtable.StateText;
            if (state == null || value == null)
            {
                return;
            }

            AgeTooltip it = value;
            vtable.StateText = () =>
            {
                string said = state();
                if (string.IsNullOrEmpty(said))
                {
                    return said;
                }

                NodeAnnouncement part = TooltipParts.Part(GraphNodes.Sections(null, it));
                return new MessageBuilder()
                    .ListItem(said)
                    .ListItem(part == null || part.Text == null ? null : part.Text())
                    .Build();
            };
        }

        /// <summary>Point at the widget the tooltip hangs on rather than at the row: a row's tooltip
        /// routinely belongs to one label inside it, and pointing at the container draws nothing.
        /// </summary>
        public static void PointAtTooltip(NodeVtable vtable, AgeTooltip tooltip)
        {
            AgeTooltip it = tooltip;
            vtable.OnFocusVisual = () =>
                PointerFocus.MoveTo(AgeWidgets.TooltipOwner(it), it, AgeWidgets.TooltipOwner(it));
            vtable.OnBlurVisual = AgeWidgets.ReleasePointer;
        }

        /// <summary>
        /// A wrapped label as the one sentence it is.
        ///
        /// The game wraps a long message over as many lines as the panel is wide, so its line breaks
        /// are where the words ran out and not punctuation. They are joined with a space, which is the
        /// sentence the game wrote; a comma between them would put a pause in the middle of one and
        /// read a full stop as "lost., Continue".
        /// </summary>
        internal static string OneLine(string text)
        {
            MessageBuilder message = new MessageBuilder();
            foreach (string line in AgeText.Lines(text))
            {
                message.Fragment(line);
            }

            return message.Build();
        }

        // The handler a click on a setting's text field reaches when it takes the keyboard: it is what
        // clears the placeholder. Resolved once - a lookup per row per navigation operation would be
        // paid for nothing.
        private static readonly MethodInfo SettingFieldGainFocus = GameHandlers.Method(
            typeof(SettingTextFieldItem),
            "OnTextFieldGainFocusCb"
        );

        /// <summary>
        /// A modal window's own command bar: the wired buttons the prefab hung under it, as the ones
        /// the game is DRAWING, left to right.
        ///
        /// Both windows that have one carry the bar TWICE - once for the main menu, once for a game in
        /// progress - and the buttons they name in their own fields are the in-game set whichever skin
        /// is worn, so neither bar can be read from those fields. What is read instead is whichever
        /// wired buttons are drawn: the window's backdrops are buttons too (they are there to swallow
        /// clicks that miss) and they are wired to nothing, which is exactly what tells them apart from
        /// the bar. That is one mechanism for both skins, it is how the duplicate "Reset to Defaults"
        /// stopped being possible, and it needs no list of which buttons a window is expected to have.
        ///
        /// The wired set is remembered per window, because a window builds its bar once when it loads
        /// and never rebuilds it, so walking the whole thing on every navigation operation would be
        /// paid for an answer that cannot have changed. Which of them are DRAWN, where, and whether
        /// they are available is read live every time. Held per instance, so a hot reload starts with
        /// nothing remembered.
        /// </summary>
        internal sealed class ButtonBar
        {
            private readonly string _subject;
            private Component _from;
            private List<AgeControlButton> _wired;

            internal ButtonBar(string subject)
            {
                _subject = subject;
            }

            /// <summary>The bar as it is drawn: the wired buttons <paramref name="keep"/> accepts, in
            /// the order they sit in. Banding input, not existence - the list is counted aloud, and a
            /// button of the skin that is not in use keeps its old rectangle, so a gate that dropped
            /// its node afterwards would still have let it reorder and miscount the buttons the player
            /// does hear.</summary>
            internal List<AgeControlButton> Drawn(
                Component window,
                Predicate<AgeControlButton> keep
            )
            {
                List<AgeControlButton> bar = new List<AgeControlButton>();
                List<AgeControlButton> wired = Wired(window);
                for (int i = 0; i < wired.Count; i++)
                {
                    AgeControlButton button = wired[i];
                    AgeTransform transform = AgeWidgets.Transform(button);
                    if (
                        transform == null
                        || !AgeWidgets.Visible(transform)
                        || (keep != null && !keep(button))
                    )
                    {
                        continue;
                    }

                    // Placed by where it is drawn rather than sorted afterwards, so two buttons in the
                    // same place keep the order they were found in.
                    float x = LeftEdge(transform);
                    int at = bar.Count;
                    while (at > 0 && LeftEdge(AgeWidgets.Transform(bar[at - 1])) > x)
                    {
                        at--;
                    }

                    bar.Insert(at, button);
                }

                return bar;
            }

            /// <summary>A button's key: what the window called it and what it is wired to, which is
            /// what tells the two skins' copies of one command apart.</summary>
            internal static string KeyOf(AgeControlButton button)
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

            private List<AgeControlButton> Wired(Component window)
            {
                if (ReferenceEquals(_from, window) && _wired != null && AllAlive(_wired))
                {
                    return _wired;
                }

                _from = window;
                _wired = Collect(window);
                return _wired;
            }

            private List<AgeControlButton> Collect(Component window)
            {
                List<AgeControlButton> buttons = new List<AgeControlButton>();
                try
                {
                    foreach (
                        AgeControlButton button in window.GetComponentsInChildren<AgeControlButton>(
                            true
                        )
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
                    Log.Warn(_subject + ": finding the window's buttons threw: " + e);
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
        }
    }
}
