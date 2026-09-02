using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using ES2Access.UI.Input;
using ES2Access.UI.ModOptions;
using UnityEngine;
using GameBinding = Amplitude.Unity.Input.InputBinding;
using KeyCombination = Amplitude.Unity.Input.KeyCombination;

namespace ES2Access.Screens
{
    /// <summary>The settings of the category showing: the row each option gets, and what working a
    /// checkbox, a slider, a drop list or a binding does to it.</summary>
    public sealed partial class OptionsScreen
    {
        // ---- the settings of the category showing ----

        /// <summary>
        /// The settings of the category on screen, in the order the page arranged them.
        ///
        /// A run of KEY-BINDING rows is read as a three-column TABLE rather than as a list of
        /// buttons: the action's name, the primary key and the secondary key are the two fields the
        /// game itself draws beside the name (<c>OptionKeyMappingItem</c>'s
        /// <c>PrimaryKeyBindingField</c>/<c>SecondaryKeyBindingField</c>), so the columns are a fact
        /// of the game's own data. Up and down walk the name column and read the whole row; left and
        /// right cross to the keys, each crossing naming the column it lands in; an empty key says so
        /// under its own caption, which is what the old one-node row could not do - it said nothing
        /// at all about a missing secondary. Owner ruling, 2026-08-23.
        ///
        /// Every other kind of setting stays one row, and a page that mixes the two keeps them in
        /// drawn order: the builder stitches the seam where its menu rows meet the sheet's raw ones.
        /// </summary>
        private void BuildRows(GraphBuilder builder, string category)
        {
            OptionsTabPanel panel = ShownPanel();
            if (panel == null || panel.OptionsTable == null)
            {
                return;
            }

            List<OptionItem> rows = Rows(panel);
            bool named = !string.IsNullOrEmpty(category);
            if (named)
            {
                // The page's own name carries the table's role word, so one context level says both.
                // A labelled region of the sheet's own would name the category a second time.
                builder.PushContext(
                    category,
                    HoldsBindings(rows) ? ModStrings.Get(ModStrings.NavTable) : null
                );
            }

            string key = PanelKey(panel);
            GraphSheet sheet = null;
            // A heading met while a sheet is open: the block under it is the sheet's next region, and
            // the region is opened by the row that starts it rather than by the heading, because a
            // region with nothing in it is not a place Alt+arrow can go.
            bool blockPending = false;
            bool sectioned = false;
            bool grouped = false;
            // A page WITH captions gets a region for the rows above the first one, so the block the
            // player starts in is a place Ctrl+arrow can leave. Without that the leading rows belong
            // to no region and the jump does nothing at all - measured on the custom-category page,
            // whose name and keyword boxes sit above thirteen captioned sections.
            if (Captioned(rows))
            {
                builder.SetRegion("options:" + key + "/head");
            }

            for (int i = 0; i < rows.Count; i++)
            {
                OptionKeyMappingItem binding = rows[i] as OptionKeyMappingItem;
                if (binding != null)
                {
                    if (sheet == null)
                    {
                        sheet = new GraphSheet(builder, "options:" + key + "/keys/");
                        sheet.Region(null, BindingColumns());
                    }
                    else if (blockPending)
                    {
                        // A new REGION of the SAME sheet, never a sheet of its own: the sheet numbers
                        // its own regions, so a second sheet would name the region the first already
                        // named and Alt+arrow would read the whole page as one block; and the sheet
                        // chains its rows across a region boundary, so Down still walks the page from
                        // its first row to its last. Both measured 2026-09-02 on the Controls tab.
                        sheet.Region(null, BindingColumns());
                    }

                    blockPending = false;
                    BuildBindingRow(sheet, binding);
                    continue;
                }

                // A CAPTION the mod drew over the rows under it is the name of a SECTION, not a
                // control: it is what the block is called, so it names the region the block is in
                // and is never a stop of its own. That is what makes Ctrl+arrow walk a page of a
                // hundred checkboxes by the thirteen headings it is written under. Asked before the
                // sheet is finished, because a heading BETWEEN two blocks of key bindings divides one
                // sheet rather than ending it.
                string caption = ModRows.CaptionOf(rows[i]);
                if (caption != null)
                {
                    if (sectioned)
                    {
                        builder.PopContext();
                    }

                    builder.PushContext(caption);
                    if (sheet != null)
                    {
                        blockPending = true;
                    }
                    else
                    {
                        builder.SetRegion("options:" + key + "/" + OptionKey(rows[i]));
                    }

                    sectioned = true;
                    continue;
                }

                if (sheet != null)
                {
                    sheet.Finish();
                    sheet = null;
                    blockPending = false;
                }

                // A HEADER the mod drew over a whole block is an expandable GROUP: the rows under it
                // are its children, and Left/Right and Enter open and shut it. What opening means -
                // which rows the page draws - is the mod's own (ModRows.Group), and it happens in the
                // same call, so the tree's open-and-step-in finds the children this very frame.
                ModGroupRow group = ModRows.GroupOf(rows[i]);
                if (group != null)
                {
                    if (sectioned)
                    {
                        builder.PopContext();
                        builder.SetRegion(null);
                        sectioned = false;
                    }

                    if (grouped)
                    {
                        builder.EndGroup();
                    }

                    // Synthetic: mod-authored - the mod's own grouping over the game's settings rows.
                    builder.BeginGroup(
                        Nodes.Synthetic(GroupId(key, rows[i]), GroupVtable(rows[i], group)),
                        group.Expanded
                    );
                    grouped = true;
                    continue;
                }

                BuildRow(builder, key, rows[i]);
            }

            if (sheet != null)
            {
                sheet.Finish();
            }

            if (sectioned)
            {
                builder.PopContext();
                builder.SetRegion(null);
            }

            if (grouped)
            {
                builder.EndGroup();
            }

            if (named)
            {
                builder.PopContext();
            }
        }

        /// <summary>A block header's identity. STRUCTURAL, not referenced: the row is destroyed and
        /// built again whenever the page's shape changes, and the cursor has to stay on the block the
        /// player was standing on.</summary>
        private static ControlId GroupId(string category, OptionItem item)
        {
            return ControlId.Structural("options:" + category + "/" + OptionKey(item));
        }

        /// <summary>
        /// One expandable block: the header's own words, and what opening and shutting it does.
        ///
        /// Three ways in, all the same act. Right opens it and steps into it; Left shuts it; Enter
        /// flips it where the player stands, which is what the drawn button does under the mouse -
        /// literally the same call (<see cref="ModRows.Activate"/>), so the two cannot come apart -
        /// and its new state is spoken through <see cref="NodeVtable.StateText"/>, because the cursor
        /// has not moved and nothing else would say it.
        /// </summary>
        private static NodeVtable GroupVtable(OptionItem item, ModGroupRow block)
        {
            OptionItem row = item;
            ModGroupRow group = block;
            AgeTooltip tooltip = row.Tooltip;
            NodeVtable vtable = GraphNodes.Group(
                () => AgeText.Label(row.TitleLabel),
                () => AgeWidgets.Operable(AgeWidgets.Transform(row)),
                tooltip
            );
            vtable.OnExpand = () => group.Expand(true);
            vtable.OnCollapse = () => group.Expand(false);
            vtable.OnActivate = () => ModRows.Activate(row);
            vtable.StateText = () =>
                GraphAnnouncer.ExpandedStateText == null
                    ? null
                    : GraphAnnouncer.ExpandedStateText(group.Expanded);
            vtable.OnFocusVisual = () =>
                PointerFocus.MoveTo(null, tooltip, AgeWidgets.Transform(row.TitleLabel));
            vtable.OnBlurVisual = ReleasePointer;
            return vtable;
        }

        /// <summary>Whether the page is divided into captioned sections - which is what makes the
        /// rows above the first caption a section of their own.</summary>
        private static bool Captioned(List<OptionItem> rows)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (ModRows.CaptionOf(rows[i]) != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HoldsBindings(List<OptionItem> rows)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] is OptionKeyMappingItem)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>One ordinary setting - anything that is not a key binding.</summary>
        private void BuildRow(GraphBuilder builder, string category, OptionItem item)
        {
            OptionItem row = item;
            // Built before the vtable, not after: a text row hands the keyboard over only while the
            // cursor is still on the row that asked, so the editor has to be told which row that is.
            ControlId id = ControlId.For(row, "options:" + category + "/" + OptionKey(row));
            NodeVtable vtable = RowVtable(row, id);
            if (vtable == null)
            {
                return;
            }

            AgeTooltip tooltip = row.Tooltip;
            vtable.OnFocusVisual = () =>
                PointerFocus.MoveTo(null, tooltip, AgeWidgets.Transform(row.TitleLabel));
            vtable.OnBlurVisual = ReleasePointer;

            builder.AddItem(Nodes.Drawn(id, vtable, row));
        }

        /// <summary>The three columns of a key-binding table, the name's caption first. The game
        /// draws no captions over these - there is no header band above the first row - so all three
        /// are the mod's own words.</summary>
        private static string[] BindingColumns()
        {
            return new[]
            {
                ModStrings.Get(ModStrings.NavKeyBindingAction),
                ModStrings.Get(ModStrings.NavKeyBindingPrimaryColumn),
                ModStrings.Get(ModStrings.NavKeyBindingSecondaryColumn),
            };
        }

        /// <summary>
        /// One key-binding row: the action, then its two keys.
        ///
        /// The name cell is role-less and inert - it NAMES the row, and the rebinding lives in the
        /// two key cells, which is what makes "secondary" a column rather than a second gesture on
        /// the row (the Backspace secondary-capture design it replaces is gone). It carries the whole
        /// row's keys as a value part all the same, so walking DOWN the table still reads what each
        /// action is on without stepping sideways, and the row's description tooltip stays here
        /// rather than being repeated on each key.
        /// </summary>
        private static void BuildBindingRow(GraphSheet sheet, OptionKeyMappingItem row)
        {
            OptionKeyMappingItem item = row;
            Func<bool> enabled = () => AgeWidgets.Operable(AgeWidgets.Transform(item));
            AgeTooltip tooltip = item.Tooltip;
            NodeVtable name = new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeText.Label(item.TitleLabel)),
                    GraphNodes.ValuePart(() => BindingText(item)),
                    GraphNodes.DisabledPart(enabled),
                },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            name.OnFocusVisual = () =>
                PointerFocus.MoveTo(null, tooltip, AgeWidgets.Transform(item.TitleLabel));
            name.OnBlurVisual = ReleasePointer;

            sheet.RowAt(
                name,
                item,
                new[]
                {
                    new KeyValuePair<int, NodeVtable>(1, KeyCell(item, false, enabled)),
                    new KeyValuePair<int, NodeVtable>(2, KeyCell(item, true, enabled)),
                },
                // The row the game drew: what the whole row is scrolled to, and what its three cells
                // exist by - the list is long and filtered, and a row the tab switched off is one the
                // sheet would otherwise go on offering.
                AgeWidgets.Transform(item)
            );
        }

        /// <summary>
        /// One of a row's two key cells: what that field holds, Enter to rebind it, Delete to empty
        /// it.
        ///
        /// It keeps the control's role word and its click, the way any table cell the game draws a
        /// real control into does. Its own words are the field's alone - the caption is the edge the
        /// player crossed to get here, and the buffer carries the pair itself because that crossing
        /// is not repeated on demand.
        /// </summary>
        private static NodeVtable KeyCell(
            OptionKeyMappingItem row,
            bool secondary,
            Func<bool> enabled
        )
        {
            OptionKeyMappingItem item = row;
            AgeControlKeyBindingField field = secondary
                ? item.SecondaryKeyBindingField
                : item.PrimaryKeyBindingField;
            string captionKey = secondary
                ? ModStrings.NavKeyBindingSecondaryColumn
                : ModStrings.NavKeyBindingPrimaryColumn;
            Func<string> value = () => CellText(item, field);
            NodeVtable cell = new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = new List<NodeAnnouncement>
                {
                    // The keys ARE this cell's name - it has no caption of its own, the column's is
                    // the edge crossed to reach it - so they are declared as the label and read
                    // before the role word rather than after it. Watched, which is what carries the
                    // capture: the field rewrites its own text as each key goes down.
                    new NodeAnnouncement(value, live: true, kind: AnnouncementKinds.Label),
                    GraphNodes.DisabledPart(enabled),
                },
                OnActivate = () => StartCapture(item, secondary),
                OnClear = () => ClearKey(item, secondary),
                BufferHead = () =>
                    new MessageBuilder()
                        .ListItem(ModStrings.Get(captionKey))
                        .ListItem(value())
                        .Build(),
            };
            NodeHints.Add(
                cell,
                ModStrings.HintClearKey,
                UiActions.Clear,
                0,
                () => enabled() && !string.IsNullOrEmpty(KeyText(field))
            );
            cell.OnFocusVisual = () =>
                PointerFocus.MoveTo(
                    field == null ? null : field.AgeTransform,
                    null,
                    field == null ? null : AgeWidgets.Transform(field.Label)
                );
            cell.OnBlurVisual = ReleasePointer;
            return cell;
        }

        /// <summary>How one setting is read and worked, chosen by what kind of setting it is. Every
        /// kind announces its title, its tooltip and whether it is refusing; what differs is the value
        /// it holds and how the player changes it.</summary>
        private NodeVtable RowVtable(OptionItem item, ControlId id)
        {
            Func<string> label = () => AgeText.Label(item.TitleLabel);
            Func<bool> enabled = () => AgeWidgets.Operable(AgeWidgets.Transform(item));
            AgeTooltip tooltip = item.Tooltip;

            // A row the MOD drew and wired: the game has no button row, so one of its own buttons is
            // cloned into the table and what it does is kept beside it (ModRows). Pressing it here
            // is the same call the mouse makes on it, so neither way in can drift from the other.
            if (ModRows.ActionOf(item) != null)
            {
                return GraphNodes.Button(label, () => ModRows.Activate(item), enabled, tooltip);
            }

            OptionCheckboxItem checkbox = item as OptionCheckboxItem;
            if (checkbox != null && checkbox.Toggle != null)
            {
                return GraphNodes.Checkbox(
                    label,
                    () => Checked(checkbox),
                    () => AgeWidgets.Toggle(checkbox.Toggle),
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
                                GameHandlers.Call(EntrySelected, dropList, GameHandlers.NoSender);
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

            // A key binding is not one of these: it is a row of the category's own table and is built
            // by BuildBindingRow, which never reaches here.

            // NO OPTION THE GAME SHIPS IS A TEXT FIELD - its own row for one commits the label OBJECT
            // into the option's value and the cast is swallowed as a logged error, so nothing in the
            // game could ever have used it. The mod's rows do (a category's name, its keywords), the
            // broken commit is patched (OptionTextFieldCommit), and the editing itself is the one
            // every text box in the game gets: Enter ends the edit and nothing else, Escape puts back
            // what was there.
            OptionTextFieldItem field = item as OptionTextFieldItem;
            if (field != null && field.TextField != null)
            {
                AgeControlTextField box = field.TextField;
                TextFieldEditor editor = _editor;
                NodeVtable edit = GraphNodes.EditField(
                    label,
                    () => TextFieldEditor.Typing(box) ? null : SettingRows.FieldText(box),
                    () => editor.Request(box, null, null, id),
                    enabled,
                    tooltip
                );
                return edit;
            }

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

            NodeVtable vtable = new NodeVtable { Announcements = parts };
            // Through the door, so the row DECLARES which tooltip it shows even though the caller
            // re-points it: every other kind of row here comes from a factory that says so, and a row
            // that named none read to the parity audit as a row with no tooltip at all.
            vtable.Sections = GraphNodes.SectionsFor(vtable, tooltip);
            return vtable;
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
                GameHandlers.Call(SliderReleased, item, GameHandlers.NoSender);
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

        /// <summary>
        /// What ONE key cell says: the keys in that field, or the word for an empty cell.
        ///
        /// With one exception, and it is the whole reason this is not just <see cref="KeyText"/>: a
        /// field that has taken the keyboard blanks itself to listen, and calling that "empty" would
        /// announce a binding the player has not lost. While this field is the one listening it says
        /// nothing until a key goes down, and then it says the combination building under their
        /// fingers.
        /// </summary>
        private static string CellText(OptionKeyMappingItem item, AgeControlKeyBindingField field)
        {
            string text = KeyText(field);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            return ReferenceEquals(CapturingField(item), field)
                ? null
                : ModStrings.Get(ModStrings.NavCellEmpty);
        }

        /// <summary>
        /// EMPTY ONE OF A ROW'S TWO KEYS.
        ///
        /// The game has no clear button at all: a mouse user empties a field by focusing it, which
        /// blanks it, and then clicking somewhere else, which writes the blank back
        /// (<c>OptionKeyMappingItem.OnGainFocusCb</c> :62-69 and <c>OnLoseFocusCb</c> :83-98). This is
        /// the same write without the focus round trip, so it lights Apply and is undone by Cancel
        /// exactly like any other change - for a game row, whose setter is the input manager, and for
        /// one of the mod's own, whose setter is the binding store.
        ///
        /// Refused while a capture is running or waiting to start: the keyboard is about to belong to
        /// a field, and emptying the row underneath it would be a change nobody asked for.
        /// </summary>
        private static void ClearKey(OptionKeyMappingItem item, bool secondary)
        {
            try
            {
                if (!AgeWidgets.Operable(AgeWidgets.Transform(item)) || _pending != null || _capturing != null)
                {
                    return;
                }

                GameBinding current = item.Option.Value as GameBinding;
                if (current == null)
                {
                    return;
                }

                KeyCombination going = secondary
                    ? current.SecondaryKeyCombination
                    : current.PrimaryKeyCombination;
                if (going == null || going.Equals(KeyCombination.None))
                {
                    return;
                }

                Write(
                    item,
                    new GameBinding(
                        current.InputAction,
                        secondary ? current.PrimaryKeyCombination : KeyCombination.None,
                        secondary ? KeyCombination.None : current.SecondaryKeyCombination
                    )
                );
            }
            catch (Exception e)
            {
                Log.Warn("options: clearing a key threw: " + e);
            }
        }

        /// <summary>Write a row's binding the way the game's own commit does
        /// (<c>OptionKeyMappingItem.OnChangeOptionValueConfirmation</c> :147-166): the option's value,
        /// then the window's own "a setting changed" - which is what lights Apply and what its backup
        /// is compared against - and then the row redraws both its fields.</summary>
        internal static void Write(OptionKeyMappingItem item, GameBinding binding)
        {
            item.Option.Value = binding;
            OptionsModalWindow window = Window();
            if (window != null)
            {
                window.OnOptionChanged(item.Option);
            }

            item.Refresh();
        }
    }
}
