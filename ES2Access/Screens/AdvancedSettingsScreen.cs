using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// The advanced settings a category's Advanced button opens, made navigable.
    ///
    /// One window serves every category. It builds a table per category once
    /// (<c>AdvancedSettingsModalWindow.Load</c> :53-70) and shows only the one the button that opened
    /// it named (<c>Refresh</c> :103-115, against <c>CurrentCategory</c>), so this screen reads
    /// whichever table is drawn rather than being told which category it is on - and the same screen
    /// covers Gameplay, Galaxy and any category a patch gives advanced groups to.
    ///
    /// The drawn shape is columns: one per <c>AdvancedSettingsGroup</c> in the game's own XML, each
    /// with a heading and its own scroll view of settings. So each column is a Tab stop announced by
    /// its heading, and inside it every setting is a row of its own - the same one-control-per-row rule
    /// the lobby underneath follows, and the same rows: <see cref="SettingRows"/> reads and works them,
    /// which is why a text field in the Galaxy group needs nothing here. A column taller than its
    /// viewport scrolls itself, because <see cref="ScrollIntoView"/> hangs off the focus-commit site
    /// and finds the scroll view above whatever control focus landed on.
    ///
    /// Escape is the game's, and here it is nothing but a dismiss: the window overrides no input
    /// handling, so <c>GuiModalWindow.HandleInput</c> (:36-44) hides it and the lobby - which kept its
    /// cursor while it was covered - comes back on the control that opened this.
    /// </summary>
    public sealed class AdvancedSettingsScreen : Screen
    {
        private const string ActionsStop = "advanced:actions";

        /// <summary>How far into the bottom band to look for its buttons.</summary>
        private const int ButtonDepth = 2;

        // Reused across builds rather than allocated per frame: Build runs every tick.
        private readonly List<AgeTransform> _columns = new List<AgeTransform>();
        private readonly List<AgeTransform> _rows = new List<AgeTransform>();
        private readonly List<AgeTransform> _buttons = new List<AgeTransform>();

        /// <summary>The deferred keyboard hand-over for this window's text boxes - the galaxy
        /// generation seed is one.</summary>
        private readonly TextFieldEditor _editor = new TextFieldEditor();

        private static readonly Func<AgeTransform, AgeTransform> Itself = widget => widget;

        public override string Key
        {
            get { return "screen.advanced-settings"; }
        }

        /// <summary>Just above the new game page it is opened from - its only opener - and well below
        /// the drop list at 70, which its own settings can open over it.</summary>
        public override int Layer
        {
            get { return 5; }
        }

        /// <summary>"Advanced Settings : Gameplay" - the game writes the category into its own heading
        /// (<c>OnBeginShow</c> :83), which is exactly what a player arriving needs to hear.</summary>
        public override string ScreenName
        {
            get
            {
                AdvancedSettingsModalWindow window = Window();
                string title = window == null ? null : AgeText.Label(window.WindowTitle);
                return string.IsNullOrEmpty(title)
                    ? ModStrings.Get(ModStrings.ScreenAdvancedSettings)
                    : title;
            }
        }

        /// <summary>Ours while the window is up and has finished animating in. Deliberately not
        /// conditioned on no modal being visible: this window IS the modal.</summary>
        public override bool IsActive()
        {
            AdvancedSettingsModalWindow window = Window();
            try
            {
                return window != null && window.Shown && window.IsReady;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Escape belongs to the game: the window inherits the modal's own Exit handling,
        /// which is a plain dismiss.</summary>
        public override bool Back()
        {
            return false;
        }

        public override void OnUpdate()
        {
            _editor.Update();
        }

        /// <summary>A text editor has been asked for and the keyboard has not changed hands yet:
        /// what the player types next is meant for the field, not for a search.</summary>
        public override bool CapturesRawInput
        {
            get { return _editor.Pending; }
        }

        public override void OnUnfocus()
        {
            _editor.Cancel();
        }

        public override void Build(GraphBuilder builder)
        {
            AdvancedSettingsModalWindow window = Window();
            if (window == null || window.TablesContainer == null)
            {
                return;
            }

            AgeTransform table = ShownTable(window);
            if (table == null)
            {
                return;
            }

            BuildColumns(builder, table);

            builder.BeginStop(ActionsStop);
            BuildActions(builder, window);
        }

        /// <summary>The category's table - the one the window has drawn. The others are still there
        /// with every setting in them, which is why this asks what is on screen rather than which
        /// category the window was opened for: they are the same answer, and only one of them can go
        /// stale.</summary>
        private static AgeTransform ShownTable(AdvancedSettingsModalWindow window)
        {
            IList<AgeTransform> children = AgeWidgets.DrawnChildren(window.TablesContainer);
            for (int i = 0; children != null && i < children.Count; i++)
            {
                if (SettingRows.Drawn(children[i]))
                {
                    return children[i];
                }
            }

            return null;
        }

        // ---- one stop per group column ----

        private void BuildColumns(GraphBuilder builder, AgeTransform table)
        {
            _columns.Clear();
            IList<AgeTransform> children = AgeWidgets.DrawnChildren(table);
            for (int i = 0; children != null && i < children.Count; i++)
            {
                if (SettingRows.Drawn(children[i]))
                {
                    _columns.Add(children[i]);
                }
            }

            foreach (List<AgeTransform> band in AgeLayout.Rows(_columns, Itself))
            {
                for (int i = 0; i < band.Count; i++)
                {
                    BuildColumn(builder, band[i]);
                }
            }
        }

        /// <summary>One column: its own Tab stop, the heading the game wrote on it pushed as the level
        /// its settings sit under, and a row per setting in the order they are drawn.</summary>
        private void BuildColumn(GraphBuilder builder, AgeTransform widget)
        {
            AdvancedSettingsGroupItem group = Get<AdvancedSettingsGroupItem>(widget);
            if (group == null)
            {
                return;
            }

            builder.BeginStop("advanced:group/" + AgeWidgets.NameOf(widget));

            string title = AgeText.Label(group.Title);
            bool named = !string.IsNullOrEmpty(title);
            if (named)
            {
                builder.PushContext(title);
            }

            try
            {
                _rows.Clear();
                IList<AgeTransform> children = AgeWidgets.DrawnChildren(group.SettingsTable);
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    if (SettingRows.Drawn(children[i]))
                    {
                        _rows.Add(children[i]);
                    }
                }

                foreach (List<AgeTransform> row in AgeLayout.Rows(_rows, Itself))
                {
                    for (int i = 0; i < row.Count; i++)
                    {
                        AddSetting(builder, row[i], AgeWidgets.NameOf(widget));
                    }
                }
            }
            finally
            {
                if (named)
                {
                    builder.PopContext();
                }
            }
        }

        private void AddSetting(GraphBuilder builder, AgeTransform widget, string column)
        {
            SettingItem item = Get<SettingItem>(widget);
            if (item == null)
            {
                return;
            }

            SettingRows.Add(
                builder,
                item,
                "advanced:" + column + "/" + SettingRows.SettingKey(item),
                _editor
            );
        }

        // ---- the bottom row ----

        /// <summary>Whatever the band along the bottom is showing - one Back button today. Read from
        /// the band rather than from a field, because the window names none of its buttons; the
        /// full-screen click-shield behind everything is a button too and is not in this band, so it
        /// never turns up here.</summary>
        private void BuildActions(GraphBuilder builder, AdvancedSettingsModalWindow window)
        {
            _buttons.Clear();
            AgeTransform tables = window.TablesContainer;
            AgeTransform container = tables == null ? null : tables.Parent;
            IList<AgeTransform> children = AgeWidgets.DrawnChildren(container);
            for (int i = 0; children != null && i < children.Count; i++)
            {
                if (!ReferenceEquals(children[i], window.TablesContainer))
                {
                    Collect(children[i], _buttons, ButtonDepth);
                }
            }

            foreach (List<AgeTransform> row in AgeLayout.Rows(_buttons, Itself))
            {
                for (int i = 0; i < row.Count; i++)
                {
                    SettingRows.AddButton(
                        builder,
                        AgeWidgets.Button(row[i]),
                        "advanced:button/" + AgeWidgets.NameOf(row[i])
                    );
                }
            }
        }

        private static void Collect(AgeTransform widget, List<AgeTransform> into, int depth)
        {
            if (widget == null || depth < 0 || !SettingRows.Drawn(widget))
            {
                return;
            }

            if (AgeWidgets.Button(widget) != null)
            {
                into.Add(widget);
                return;
            }

            IList<AgeTransform> children = AgeWidgets.DrawnChildren(widget);
            for (int i = 0; children != null && i < children.Count; i++)
            {
                Collect(children[i], into, depth - 1);
            }
        }

        // ---- reading the window ----

        private static AdvancedSettingsModalWindow Window()
        {
            return GameWindows.Of<AdvancedSettingsModalWindow>();
        }

        private static T Get<T>(AgeTransform widget)
            where T : UnityEngine.Component
        {
            try
            {
                return widget == null ? null : widget.GetComponent<T>();
            }
            catch (Exception)
            {
                return null;
            }
        }

    }
}
