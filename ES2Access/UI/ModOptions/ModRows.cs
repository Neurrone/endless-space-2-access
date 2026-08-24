using System;
using System.Collections.Generic;
using System.Reflection;
using Amplitude.Unity.Options;
using ES2Access.Core.Util;
using UnityEngine;

namespace ES2Access.UI.ModOptions
{
    /// <summary>One row's value, seen as a piece of text.</summary>
    public interface IModTextRow
    {
        [OptionTypeTextfield("ModText", MaxLength = ModRows.MaxChars)]
        string Text { get; set; }
    }

    /// <summary>One row's value, seen as a tick.</summary>
    public interface IModToggleRow
    {
        [OptionTypeToggle("ModToggle")]
        bool Ticked { get; set; }
    }

    /// <summary>
    /// THE MOD'S OWN ROWS, built out of the game's own option prefabs.
    ///
    /// The game builds a panel's rows by reflecting over ONE provider object's properties, which can
    /// express a fixed list of settings and nothing else - no repeatable row, no row that appears
    /// because the player added something. So a mod panel is loaded with no rows of its own and
    /// filled here, through the two doors the game leaves open: the panel's own row prefabs, and the
    /// public <c>OptionItem.Load(option, window, panel)</c>. The one closed door is the panel's
    /// <c>Options</c> array, whose setter is private and which the game's own scans read, so it is
    /// written by reflection at the end (<see cref="Publish"/>). This is the same trick
    /// <see cref="KeybindRows"/> uses, generalized so three kinds of row can use it.
    ///
    /// EVERY CHILD OF <c>OptionsTable</c> MUST BE AN <c>OptionItem</c> WITH A NON-NULL OPTION.
    /// Five of the game's own passes - the table's priority comparer, <c>BackupSettings</c>,
    /// <c>CheckWhetherSomeApplicationSettingHasChanged</c>, <c>CommitSettings</c> and
    /// <c>RestoreSettings</c> - walk <c>OptionsTable.Children</c> and dereference
    /// <c>GetComponent&lt;OptionItem&gt;().Option</c> without a null check (measured 2026-08-24: a
    /// plain button parented into the table throws inside <c>AgeTransform.Init</c>, in the comparer,
    /// before the row is even visible). So a DRAWN BUTTON gets an <c>OptionItem</c> component and an
    /// option of its own that nothing reads, and the comparer is dropped before anything is added -
    /// every row here carries the same priority, so the comparer could only shuffle them.
    /// </summary>
    public static class ModRows
    {
        /// <summary>How long a name or a keyword may be. The field enforces it as the player types,
        /// which is the only place the limit has to exist.</summary>
        public const int MaxChars = 40;

        /// <summary>Prepare a panel to be filled by hand. Called before the first row goes in.
        /// </summary>
        public static void Begin(OptionsTabPanel panel)
        {
            if (panel == null || panel.OptionsTable == null)
            {
                return;
            }

            // Before anything is added, not after: the comparer runs from AgeTransform.Init inside
            // InstantiateChild, so a row it cannot sort throws on the way in.
            panel.OptionsTable.ChildrenComparer = null;
        }

        /// <summary>Throw away every row the panel is holding - what a rebuild starts with when the
        /// SHAPE of the page has changed (a keyword added, a slot named).</summary>
        public static void Clear(OptionsTabPanel panel)
        {
            if (panel == null || panel.OptionsTable == null)
            {
                return;
            }

            try
            {
                panel.OptionsTable.DestroyAllChildren();
            }
            catch (Exception e)
            {
                Log.Warn("mod options: emptying a panel threw: " + e);
            }
        }

        /// <summary>Hand the game the options its own scans walk. Everything the panel holds is in
        /// here, in drawn order.</summary>
        public static void Publish(OptionsTabPanel panel, List<Option> options)
        {
            if (panel == null)
            {
                return;
            }

            try
            {
                PropertyInfo property = typeof(OptionsTabPanel).GetProperty(
                    "Options",
                    BindingFlags.Instance | BindingFlags.Public
                );
                MethodInfo setter = property == null ? null : property.GetSetMethod(true);
                if (setter == null)
                {
                    Log.Warn("mod options: OptionsTabPanel.Options has no setter");
                    return;
                }

                setter.Invoke(panel, new object[] { options.ToArray() });
                panel.OptionsTable.Sort();
            }
            catch (Exception e)
            {
                Log.Warn("mod options: setting the panel's options threw: " + e);
            }
        }

        // ---- the three kinds of row ----

        /// <summary>A text box: the row's title, and a value the player types.</summary>
        public static Option Text(
            OptionsTabPanel panel,
            string name,
            string title,
            Func<string> read,
            Action<string> write
        )
        {
            ModTextRow provider = new ModTextRow(read, write);
            Option option = Mint(provider, typeof(IModTextRow));
            OptionTextFieldItem item = Add<OptionTextFieldItem>(
                panel,
                panel.OptionTextFieldPrefab,
                name,
                option,
                title
            );
            if (item == null)
            {
                return null;
            }

            Ours.Add(option);
            item.Refresh();
            return option;
        }

        /// <summary>A tick.</summary>
        public static Option Toggle(
            OptionsTabPanel panel,
            string name,
            string title,
            Func<bool> read,
            Action<bool> write
        )
        {
            ModToggleRow provider = new ModToggleRow(read, write);
            Option option = Mint(provider, typeof(IModToggleRow));
            OptionCheckboxItem item = Add<OptionCheckboxItem>(
                panel,
                panel.OptionCheckboxPrefab,
                name,
                option,
                title
            );
            if (item == null)
            {
                return null;
            }

            item.Refresh();
            return option;
        }

        /// <summary>
        /// A DRAWN BUTTON in the rows table - the game's own button, cloned off the window's Cancel.
        ///
        /// The game has no button row: five row kinds and not one of them is "do a thing". So the
        /// button is the window's own, re-parented into the table, carrying an <c>OptionItem</c> for
        /// the passes described on this class and an option nothing ever reads. What the button DOES
        /// is the mod's, kept beside the row rather than on it (<see cref="ActionOf"/>), because the
        /// component is the game's type and cannot hold a delegate of ours.
        /// </summary>
        public static Option Button(
            OptionsTabPanel panel,
            OptionsModalWindow window,
            string name,
            string title,
            Action activate
        )
        {
            OptionItem item;
            Option option = Cloned(panel, window, name, title, out item);
            if (option != null)
            {
                Actions[item] = activate;
            }

            return option;
        }

        /// <summary>
        /// A GROUP HEADER in the rows table - the same drawn button, standing for the block of rows
        /// under it rather than for an action.
        ///
        /// The mod's options screen turns it into an expandable group node
        /// (<see cref="ES2Access.Screens.OptionsScreen"/>), so the tree keys open and shut it and its
        /// readout carries "expanded"/"collapsed". What opening MEANS - which rows are shown and the
        /// table laid out again - is the caller's, because only the caller knows which rows belong to
        /// it.
        /// </summary>
        public static Option Group(
            OptionsTabPanel panel,
            OptionsModalWindow window,
            string name,
            string title,
            Func<bool> expanded,
            Action<bool> expand
        )
        {
            OptionItem item;
            Option option = Cloned(panel, window, name, title, out item);
            if (option != null)
            {
                Groups[item] = new ModGroupRow(expanded, expand);
            }

            return option;
        }

        private static Option Cloned(
            OptionsTabPanel panel,
            OptionsModalWindow window,
            string name,
            string title,
            out OptionItem made
        )
        {
            made = null;
            if (panel == null || window == null || window.CancelButton == null)
            {
                return null;
            }

            try
            {
                Option option = Mint(new ModToggleRow(Never, Ignore), typeof(IModToggleRow));
                AgeTransform row = panel.OptionsTable.InstantiateChild(
                    window.CancelButton.transform,
                    name
                );
                row.Init();
                AgeControlButton button = row.GetComponent<AgeControlButton>();
                if (button == null)
                {
                    Log.Warn("mod options: the cloned button has no AgeControlButton");
                    return null;
                }

                // The clone came off the window's Cancel and would still call it.
                button.OnActivateObject = null;
                button.OnActivateMethod = string.Empty;

                // The clone came out of the BUTTON BAR, where it is pinned to the bar's own corners.
                // In a rows table it has to behave like a row: full width, stacked by the table.
                row.AttachLeft = true;
                row.AttachRight = true;
                row.AttachTop = false;
                row.AttachBottom = false;
                row.PixelMarginLeft = 0;
                row.PixelMarginRight = 0;
                row.Width = panel.OptionsTable.Width;

                // The cross the Cancel button wears means "cancel" wherever it is drawn, and these
                // buttons do nothing of the kind. The circle stays - it is what says "button" -
                // and the symbol inside it goes.
                for (int i = 0; i < row.Children.Count; i++)
                {
                    if (row.Children[i].name == "Icon")
                    {
                        row.Children[i].Visible = false;
                    }
                }

                OptionItem item = row.gameObject.AddComponent<OptionItem>();
                item.TitleLabel = LabelIn(row);
                item.Tooltip = row.AgeTooltip;
                if (item.Tooltip != null)
                {
                    // The clone brought the Cancel button's own words with it.
                    item.Tooltip.Content = string.Empty;
                }

                SetOption(item, option);
                if (item.TitleLabel != null)
                {
                    item.TitleLabel.Text = title;
                }

                made = item;
                return option;
            }
            catch (Exception e)
            {
                Log.Warn("mod options: building the button row " + name + " threw: " + e);
                return null;
            }
        }

        /// <summary>
        /// A CAPTION over the rows that follow it - drawn as a row of the table, spoken as the name
        /// of the section rather than as a control of its own.
        ///
        /// It is a checkbox row with its tick hidden, because that is the row shape the panel's own
        /// prefab draws and a caption in a different shape would not look like part of the page.
        /// </summary>
        public static Option Caption(OptionsTabPanel panel, string name, string title)
        {
            ModToggleRow provider = new ModToggleRow(Never, Ignore);
            Option option = Mint(provider, typeof(IModToggleRow));
            OptionCheckboxItem item = Add<OptionCheckboxItem>(
                panel,
                panel.OptionCheckboxPrefab,
                name,
                option,
                title
            );
            if (item == null)
            {
                return null;
            }

            if (item.Toggle != null && item.Toggle.AgeTransform != null)
            {
                item.Toggle.AgeTransform.Visible = false;
            }

            Captions[item] = title;
            return option;
        }

        /// <summary>Say a caption again, after something under it changed the words it carries.
        /// </summary>
        public static void Recaption(OptionItem item, string title)
        {
            if (item == null || !Captions.ContainsKey(item))
            {
                return;
            }

            Captions[item] = title;
            if (item.TitleLabel != null)
            {
                item.TitleLabel.Text = title;
            }
        }

        /// <summary>What a drawn button row does, or null where the row is not one of ours.</summary>
        public static Action ActionOf(OptionItem item)
        {
            Action action;
            return item != null && Actions.TryGetValue(item, out action) ? action : null;
        }

        /// <summary>The section this row is the caption of, or null where it captions nothing.
        /// </summary>
        public static string CaptionOf(OptionItem item)
        {
            string title;
            return item != null && Captions.TryGetValue(item, out title) ? title : null;
        }

        /// <summary>The block this row is the header of, or null where the row heads nothing.
        /// </summary>
        public static ModGroupRow GroupOf(OptionItem item)
        {
            ModGroupRow group;
            return item != null && Groups.TryGetValue(item, out group) ? group : null;
        }

        /// <summary>Whether a text option is one of ours - what the patch on the game's broken
        /// text-field commit asks before it steps in.</summary>
        public static bool IsOurText(Option option)
        {
            return option != null && Ours.Contains(option);
        }

        /// <summary>Mod teardown: hold no row and no delegate across a reload.</summary>
        public static void Forget()
        {
            Actions.Clear();
            Captions.Clear();
            Groups.Clear();
            Ours.Clear();
        }

        // ---- the machinery ----

        private static T Add<T>(
            OptionsTabPanel panel,
            Transform prefab,
            string name,
            Option option,
            string title
        )
            where T : OptionItem
        {
            if (panel == null || panel.OptionsTable == null || prefab == null || option == null)
            {
                Log.Warn("mod options: no prefab to build the row " + name + " from");
                return null;
            }

            try
            {
                AgeTransform row = panel.OptionsTable.InstantiateChild(prefab, name);
                row.Init();
                T item = row.GetComponent<T>();
                if (item == null)
                {
                    Log.Warn("mod options: the prefab for " + name + " has no " + typeof(T).Name);
                    return null;
                }

                item.Load(option, panel.Parent, panel);
                // After Load, never before: Load writes the game's own %Option<Name>Title into both,
                // and a localization key nothing has a row for comes back from the localizer
                // unchanged - so leaving them would draw and speak the raw key.
                if (item.TitleLabel != null)
                {
                    item.TitleLabel.Text = title;
                }

                if (item.Tooltip != null)
                {
                    item.Tooltip.Content = string.Empty;
                }

                return item;
            }
            catch (Exception e)
            {
                Log.Warn("mod options: building the row " + name + " threw: " + e);
                return null;
            }
        }

        private static Option Mint(object provider, Type face)
        {
            Option[] minted = Option.GetOptions(provider, face, true, true, true);
            if (minted.Length == 0)
            {
                Log.Warn("mod options: no option minted over " + face.Name);
                return null;
            }

            return minted[0];
        }

        /// <summary>Give a row an option without going through <c>Load</c> - for the cloned button,
        /// which has no title label of the prefab's own for Load to write into.</summary>
        private static void SetOption(OptionItem item, Option option)
        {
            try
            {
                for (Type type = item.GetType(); type != null; type = type.BaseType)
                {
                    PropertyInfo property = type.GetProperty(
                        "Option",
                        BindingFlags.Instance
                            | BindingFlags.Public
                            | BindingFlags.NonPublic
                            | BindingFlags.DeclaredOnly
                    );
                    MethodInfo setter = property == null ? null : property.GetSetMethod(true);
                    if (setter != null)
                    {
                        setter.Invoke(item, new object[] { option });
                        return;
                    }
                }

                Log.Warn("mod options: nothing can set OptionItem.Option");
            }
            catch (Exception e)
            {
                Log.Warn("mod options: setting a row's option threw: " + e);
            }
        }

        private static AgePrimitiveLabel LabelIn(AgeTransform row)
        {
            for (int i = 0; row != null && i < row.Children.Count; i++)
            {
                AgePrimitiveLabel label = row.Children[i].GetComponent<AgePrimitiveLabel>();
                if (label != null)
                {
                    return label;
                }
            }

            return null;
        }

        private static bool Never()
        {
            return false;
        }

        private static void Ignore(bool value) { }

        private static readonly Dictionary<OptionItem, Action> Actions =
            new Dictionary<OptionItem, Action>();

        private static readonly Dictionary<OptionItem, string> Captions =
            new Dictionary<OptionItem, string>();

        private static readonly Dictionary<OptionItem, ModGroupRow> Groups =
            new Dictionary<OptionItem, ModGroupRow>();

        private static readonly List<Option> Ours = new List<Option>();
    }

    /// <summary>
    /// One row's text, as the option machinery reads and writes it.
    ///
    /// The getter never answers null. <c>Option.Changed</c> is
    /// <c>!(backup != null &amp;&amp; backup.Equals(Value))</c>, so an option whose value is null
    /// reads as permanently changed - Apply lit on a window nobody has touched, and Escape always
    /// asking about unapplied changes.
    /// </summary>
    public sealed class ModTextRow : IModTextRow
    {
        public ModTextRow(Func<string> read, Action<string> write)
        {
            _read = read;
            _write = write;
        }

        public string Text
        {
            get
            {
                string text = _read == null ? null : _read();
                return text ?? string.Empty;
            }
            set
            {
                if (_write != null)
                {
                    _write(value ?? string.Empty);
                }
            }
        }

        private readonly Func<string> _read;
        private readonly Action<string> _write;
    }

    /// <summary>What a header row stands for: whether its block is open, and how to open or shut it.
    /// Kept beside the row rather than on it, because the row's component is the game's type.
    /// </summary>
    public sealed class ModGroupRow
    {
        public ModGroupRow(Func<bool> expanded, Action<bool> expand)
        {
            _expanded = expanded;
            _expand = expand;
        }

        public bool Expanded
        {
            get { return _expanded != null && _expanded(); }
        }

        public void Expand(bool open)
        {
            if (_expand != null)
            {
                _expand(open);
            }
        }

        private readonly Func<bool> _expanded;
        private readonly Action<bool> _expand;
    }

    /// <summary>One row's tick, the same way round.</summary>
    public sealed class ModToggleRow : IModToggleRow
    {
        public ModToggleRow(Func<bool> read, Action<bool> write)
        {
            _read = read;
            _write = write;
        }

        public bool Ticked
        {
            get { return _read != null && _read(); }
            set
            {
                if (_write != null)
                {
                    _write(value);
                }
            }
        }

        private readonly Func<bool> _read;
        private readonly Action<bool> _write;
    }
}
