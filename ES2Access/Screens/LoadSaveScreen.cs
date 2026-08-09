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
    /// The save and load page - one game window wearing three hats (save from a game, load from a
    /// game, load from the main menu; the main menu even keeps a second instance of it), made
    /// navigable once for all of them.
    ///
    /// Two places to be, and Tab moves between them: the window's contents - the cloud toggle drawn
    /// above the table, the table of saves, and the name field the save skin adds below it - and the
    /// row of commands along the bottom. Which side of the table a control sits on is measured rather
    /// than declared, so either skin reads in the order it is drawn.
    ///
    /// The table is walked as a table. Left and right step across the columns the game is drawing and
    /// each cell reads its own column's heading and what is written in it - the heading the game's
    /// header row shows, the value the cell itself shows, so what is spoken is what is on screen
    /// rather than a second opinion assembled from the save file. Up and down move between saves and
    /// keep the column, and every cell's review buffer holds the whole row, so a player who wants the
    /// save's facts in one go never has to walk sideways for them.
    ///
    /// Enter on any cell selects that save - the game's own selection, the one that enables Load and
    /// Delete and copies the name into the save-name field - and says so; the commands then do what
    /// they say. That split (Enter selects, the command acts) is the game's own double-click-versus-
    /// click distinction, kept because it means no save is ever loaded, overwritten or deleted by a
    /// stray Enter.
    ///
    /// The save-name field is the game's own text editor, and handing it the keyboard has to WAIT A
    /// FRAME; see <see cref="RequestEdit"/>.
    ///
    /// Escape belongs to the game: its own route closes this window and, when it was opened from the
    /// pause menu, re-raises that menu. Cancel, the button that does the same thing with the mouse, is
    /// declared alongside the other commands.
    /// </summary>
    public sealed class LoadSaveScreen : Screen
    {
        private static readonly object ContentStop = "loadsave:content";
        private static readonly object CommandStop = "loadsave:commands";

        /// <summary>Shared by every save's row, which is what makes up and down keep the column.
        /// </summary>
        private static readonly object SaveRowKey = "loadsave:save-row";

        /// <summary>How deep inside a cell to look for the text it is drawing.</summary>
        private const int MaxCellDepth = 6;

        public override string Key
        {
            get { return "screen.load-save"; }
        }

        /// <summary>Above the pause menu that opens it, below the loading screen it leads to and
        /// the confirmation boxes it raises.</summary>
        public override int Layer
        {
            get { return 55; }
        }

        /// <summary>The window's own title - "Save game" and "Load game" are its words, not ours.
        /// </summary>
        public override string ScreenName
        {
            get { return WindowTitle(Window()); }
        }

        /// <summary>The window's own title, with the mod's fallback for a window that is drawing none
        /// - used both for the screen name and for naming the table the saves are walked as.</summary>
        private static string WindowTitle(LoadSaveModalWindow window)
        {
            string title = null;
            try
            {
                title = window != null ? AgeText.Label(window.WindowTitle) : null;
            }
            catch (Exception)
            {
                title = null;
            }

            return string.IsNullOrEmpty(title) ? ModStrings.Get(ModStrings.ScreenLoadSave) : title;
        }

        public override bool IsActive()
        {
            LoadSaveModalWindow window = Window();
            try
            {
                return window != null && window.Shown && window.IsReady;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The game's own Escape route closes the window - and re-raises the pause menu
        /// when that is where the player came from.</summary>
        public override bool Back()
        {
            return false;
        }

        public override void OnUpdate()
        {
            HandOverWhenKeyboardIsQuiet();
        }

        /// <summary>Something else has the player's attention. An edit that was asked for and not yet
        /// handed over is abandoned rather than left armed to fire under whatever comes next.</summary>
        public override void OnUnfocus()
        {
            _editing = null;
        }

        public override void Build(GraphBuilder builder)
        {
            LoadSaveModalWindow window = Window();
            if (window == null)
            {
                return;
            }

            builder.BeginStop(ContentStop);
            BuildContents(builder, window);

            builder.BeginStop(CommandStop);
            BuildCommands(builder, window);
        }

        // ---- the window's contents ----

        /// <summary>
        /// The table, with whatever the window draws above and below it in its place.
        ///
        /// Which is measured, not listed: the cloud toggle sits in the top right corner above the
        /// headers and the name field below the last row, and both belong where a sighted player finds
        /// them. Asking the layout means the same code puts a control the window gains tomorrow in the
        /// right place too.
        /// </summary>
        private void BuildContents(GraphBuilder builder, LoadSaveModalWindow window)
        {
            AgeTransform table = TableTransform(window);
            bool cloudAbove = Above(window.CloudToggleGroup, table);
            bool fieldAbove = Above(FieldTransform(window.SaveNameTextField), table);
            ControlId field = null;

            if (cloudAbove)
            {
                AddCloudToggle(builder, window);
            }

            if (fieldAbove)
            {
                field = AddNameField(builder, window);
            }

            // Announced once on the way in - "Load Game, table" - the way wotr-access names a table,
            // rather than on every cell: a role heard on every row would drown the row itself out, and
            // what a table IS is a fact about entering it, not about any one thing in it.
            builder.PushContext(WindowTitle(window), ModStrings.Get(ModStrings.NavTable), false);
            ControlId firstCell = AddSaves(builder, window);
            builder.PopContext();

            if (!fieldAbove)
            {
                field = AddNameField(builder, window);
            }

            if (!cloudAbove)
            {
                AddCloudToggle(builder, window);
            }

            ControlId start = field;

            // The save skin opens on the field the player came to type in; the load skin opens on the
            // saves. Either way it is the first Tab stop, so Tab never has to wrap to reach the rest.
            if (Saving(window) && start != null)
            {
                builder.SetStart(start);
            }
            else if (firstCell != null)
            {
                builder.SetStart(firstCell);
            }
            else if (start != null)
            {
                builder.SetStart(start);
            }
        }

        /// <summary>Every save the table is showing, a row each, a cell at a time.</summary>
        private static ControlId AddSaves(GraphBuilder builder, LoadSaveModalWindow window)
        {
            List<GuiTableHeader> headers = Headers(window);
            HashSet<string> taken = new HashSet<string>();
            ControlId first = null;
            foreach (GuiTableLine line in Lines(window))
            {
                GuiTableLine row = line;
                List<AgeTransform> cells = Cells(row);
                if (cells.Count == 0)
                {
                    continue;
                }

                LoadSaveModalWindow owner = window;
                string save = Distinct(taken, SaveKey(Descriptor(row)));
                builder.StartRow(SaveRowKey);
                for (int i = 0; i < cells.Count; i++)
                {
                    AgeTransform cell = cells[i];
                    GuiTableHeader header = HeaderFor(headers, cell, i);
                    ControlId id = ControlId.Referenced(
                        cell,
                        "loadsave:cell/" + save + "/" + ColumnOf(cell, i)
                    );
                    first = first ?? id;
                    builder.AddItem(id, CellVtable(owner, row, headers, cell, header));
                }

                builder.EndRow();
            }

            return first;
        }

        /// <summary>
        /// One cell: its column's heading and what the cell is showing, with the whole row behind it in
        /// the review buffer and the game's own selection on Enter.
        ///
        /// An empty cell still reads. The table's columns are the same eight for every save, and a
        /// cell that dropped out of the row when it had nothing to show would shift the columns under
        /// the player - and take up-and-down's column with them.
        /// </summary>
        private static NodeVtable CellVtable(
            LoadSaveModalWindow window,
            GuiTableLine line,
            List<GuiTableHeader> headers,
            AgeTransform cell,
            GuiTableHeader header
        )
        {
            Func<bool> selected = () => Selected(window, line);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => HeaderText(header)),
                    GraphNodes.ValuePart(() => CellText(cell)),
                    GraphNodes.SelectedPart(selected),
                    GraphNodes.DisabledPart(() => Enabled(line.AgeTransform)),
                },
                Sections = GraphNodes.Sections(() => CellFacts(header, cell), null),
                StateText = () =>
                    selected() ? ModStrings.Get(ModStrings.NavSelected) : null,
                OnActivate = () => Select(window, line),
            };
            vtable.OnFocusVisual = () => PointerFocus.MoveTo(cell, TooltipOfCell(cell), cell);
            vtable.OnBlurVisual = ReleasePointer;
            return vtable;
        }

        /// <summary>This cell for the review buffer: its own heading and value, then whatever the
        /// cell has to say for itself - for the mods column, the configuration the save wants. The
        /// rest of the row is a walk away; the buffer holds the cell the player is on.</summary>
        private static IList<string> CellFacts(GuiTableHeader header, AgeTransform cell)
        {
            List<string> lines = new List<string>();
            try
            {
                string fact = new MessageBuilder()
                    .ListItem(HeaderText(header))
                    .ListItem(CellText(cell))
                    .Build();
                if (!string.IsNullOrEmpty(fact))
                {
                    lines.Add(fact);
                }

                foreach (string detail in AgeText.Lines(AgeText.Tooltip(TooltipOfCell(cell))))
                {
                    lines.Add(detail);
                }
            }
            catch (Exception e)
            {
                Log.Warn("load save: reviewing a save threw: " + e);
            }

            return lines;
        }

        private static string HeaderText(GuiTableHeader header)
        {
            try
            {
                return header == null ? null : AgeText.Label(header.Label);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What a cell is showing, and the word for showing nothing. The columns the game
        /// draws as a picture - a cloud badge, the icons of the content a save needs - carry their
        /// meaning in their tooltips instead, which is what those read.</summary>
        private static string CellText(AgeTransform cell)
        {
            MessageBuilder labels = new MessageBuilder();
            MessageBuilder tooltips = new MessageBuilder();
            try
            {
                ReadCell(cell, labels, tooltips, 0);
            }
            catch (Exception e)
            {
                Log.Warn("load save: reading a cell threw: " + e);
            }

            string drawn = labels.Build() ?? tooltips.Build();
            return drawn ?? ModStrings.Get(ModStrings.LoadSaveCellEmpty);
        }

        // Everything the player can see inside a cell, in the order it is laid out: its words, and
        // separately the first line of every tooltip hanging off it. Hidden branches are skipped -
        // the cloud column keeps its badge and hides it rather than removing it.
        private static void ReadCell(
            AgeTransform widget,
            MessageBuilder labels,
            MessageBuilder tooltips,
            int depth
        )
        {
            if (depth > MaxCellDepth)
            {
                return;
            }

            AgePrimitiveLabel label = widget.GetComponent<AgePrimitiveLabel>();
            if (label != null)
            {
                labels.ListItem(AgeText.Label(label));
            }

            IList<string> lines = AgeText.Lines(AgeText.Tooltip(widget.AgeTooltip));
            if (lines.Count > 0)
            {
                tooltips.ListItem(lines[0]);
            }

            List<AgeTransform> children = widget.Children;
            for (int i = 0; i < children.Count; i++)
            {
                AgeTransform child = children[i];
                if (child != null && child.Visible)
                {
                    ReadCell(child, labels, tooltips, depth + 1);
                }
            }
        }

        // ---- the save-name field ----

        /// <summary>The window and field whose editor has been asked for and not yet opened.</summary>
        private LoadSaveModalWindow _editing;

        /// <summary>The save-name field, declared while the save skin shows it. Its value is whatever
        /// the field holds - the game's "enter a name here" prompt included, because that prompt is
        /// what a sighted player is looking at - and nothing at all while the player is typing into
        /// it: the screen reader is already echoing the keys, and re-reading the whole field after
        /// every letter would bury them.</summary>
        private ControlId AddNameField(GraphBuilder builder, LoadSaveModalWindow window)
        {
            AgeControlTextField field = window.SaveNameTextField;
            if (!Visible(field))
            {
                return null;
            }

            LoadSaveModalWindow owner = window;
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.EditField,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => ModStrings.Get(ModStrings.LoadSaveSaveName)),
                    GraphNodes.ValuePart(() => Typing(owner) ? null : FieldText(owner)),
                },
                Sections = GraphNodes.Sections(() => new List<string> { FieldText(owner) }, null),
                OnActivate = () => RequestEdit(owner),
                // The other single-item row at this level: paired with the cloud toggle it would
                // count as "2 of 2" of nothing either control is a member of.
                SpeaksOwnPosition = true,
            };
            ControlId id = ControlId.Referenced(field, "loadsave:name");
            builder.AddItem(id, vtable);
            return id;
        }

        /// <summary>
        /// Ask for the game's editor, and say so - entering an editor is not a thing a player can be
        /// left to infer from silence.
        ///
        /// The keyboard changes hands a frame later, and that wait is the whole point. The engine
        /// hands the focused control every key that goes down IN ITS OWN LateUpdate, which is after
        /// this; and the text field's answer to Return is to hand the focus straight back (and, when
        /// the name is one the game would accept, to save under it there and then). So handing over
        /// during the frame the player pressed Enter gave the field the press that asked for it: the
        /// editor opened and closed inside one frame, and nothing could be typed into it. Waiting for
        /// a frame on which nothing new went down costs the player nothing and is the same shape as
        /// the options page's key capture, which waits for the same reason.
        /// </summary>
        private void RequestEdit(LoadSaveModalWindow window)
        {
            if (_editing != null || window.SaveNameTextField == null)
            {
                return;
            }

            _editing = window;
            Voice.Say(ModStrings.Get(ModStrings.LoadSaveEditName), true);
        }

        /// <summary>Hand the field the keyboard, exactly as clicking it would: the field takes the
        /// engine's focus (the mod's input layer stands down for a key-exclusive control, which is
        /// correct - the letters belong in the name), and the game's gain-focus handler runs so the
        /// placeholder clears the way it does for the mouse. Both of the game's ways out - Enter
        /// saves, Escape abandons - clear the focus again, and the layer wakes up on its own.
        /// </summary>
        private void HandOverWhenKeyboardIsQuiet()
        {
            LoadSaveModalWindow window = _editing;
            if (window == null)
            {
                return;
            }

            // Moving off the field during the wait is the player changing their mind, and the request
            // has to go with them - otherwise the keyboard would be handed to a field they have left.
            if (!OnField(window))
            {
                _editing = null;
                return;
            }

            // Spelled out: the game has its own Input in the global namespace.
            if (UnityEngine.Input.anyKeyDown)
            {
                return;
            }

            _editing = null;
            try
            {
                AgeManager age = AgeManager.Instance;
                if (age == null || window.SaveNameTextField == null || !window.Shown)
                {
                    return;
                }

                age.FocusedControl = window.SaveNameTextField;
                OptionsScreen.Call(NameFieldGainFocus, window, OptionsScreen.NoSender);
            }
            catch (Exception e)
            {
                Log.Warn("load save: opening the name editor threw: " + e);
            }
        }

        /// <summary>Whether the cursor is still on the field that asked for its editor.</summary>
        private static bool OnField(LoadSaveModalWindow window)
        {
            try
            {
                GraphNavigator navigator = ModEntry.Navigator;
                GraphNode node = navigator == null ? null : navigator.CurrentNode;
                return node != null && node.Id.ReferenceMatches(window.SaveNameTextField);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Whether the game currently has the keyboard on the name field - asked of the
        /// engine's own focus, so an edit the game ended is over here the same instant.</summary>
        private static bool Typing(LoadSaveModalWindow window)
        {
            try
            {
                AgeManager age = AgeManager.Instance;
                return age != null
                    && window.SaveNameTextField != null
                    && ReferenceEquals(age.FocusedControl, window.SaveNameTextField);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string FieldText(LoadSaveModalWindow window)
        {
            try
            {
                return window.SaveNameTextField != null
                    ? AgeText.Clean(window.SaveNameTextField.Label.Text)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---- selecting a save ----

        /// <summary>Select a save the way clicking its row does: the table's selection first, then
        /// the window's own selection handler - the one that copies the name into the field and
        /// re-weighs the buttons.</summary>
        private static void Select(LoadSaveModalWindow window, GuiTableLine line)
        {
            try
            {
                window.GuiTable.SelectedLine = line;
                OptionsScreen.Call(LineSelected, window, new object[] { line });
            }
            catch (Exception e)
            {
                Log.Warn("load save: selecting a save threw: " + e);
            }
        }

        private static bool Selected(LoadSaveModalWindow window, GuiTableLine line)
        {
            try
            {
                return ReferenceEquals(window.GuiTable.SelectedLine, line);
            }
            catch (Exception)
            {
                return false;
            }
        }

        // ---- the commands along the bottom ----

        /// <summary>
        /// The row of commands under the table, left to right as it is drawn.
        ///
        /// Read off the window rather than from its named fields, the way the options window's bar is:
        /// the fields name Load, Save and Delete, and the bar also holds Cancel - the Escape route
        /// with a button on it - and the Fix Mods button the window raises for a save whose mods can be
        /// put right. Taking whichever wired buttons are drawn below the table gets all of them, in
        /// the order they sit in, and needs no list of what the window is expected to have.
        /// </summary>
        private void BuildCommands(GraphBuilder builder, LoadSaveModalWindow window)
        {
            List<AgeControlButton> commands = Commands(window);
            if (commands.Count == 0)
            {
                return;
            }

            List<ControlId> ids = new List<ControlId>(commands.Count);
            builder.StartRow();
            foreach (AgeControlButton entry in commands)
            {
                AgeControlButton command = entry;
                AgeTooltip tooltip = TooltipOf(command);
                NodeVtable vtable = GraphNodes.Button(
                    () => Caption(command),
                    () => Press(command),
                    () => Enabled(TransformOf(command)),
                    tooltip
                );
                vtable.OnFocusVisual = () => PointerFocus.MoveTo(command, tooltip);
                vtable.OnBlurVisual = ReleasePointer;

                ControlId id = ControlId.Referenced(command, "loadsave:command/" + KeyOf(command));
                ids.Add(id);
                builder.AddItem(id, vtable);
            }

            builder.EndRow();
            LinkVertically(builder, ids);
        }

        /// <summary>The wired buttons the window is drawing below its table, left to right. The
        /// header row's sort buttons are level with the table and the backdrop is wired to nothing, so
        /// neither is a command.</summary>
        private List<AgeControlButton> Commands(LoadSaveModalWindow window)
        {
            List<AgeControlButton> commands = new List<AgeControlButton>();
            AgeTransform table = TableTransform(window);
            if (table == null)
            {
                return commands;
            }

            foreach (AgeControlButton button in Buttons(window))
            {
                AgeTransform transform = TransformOf(button);
                if (
                    button == null
                    || transform == null
                    || !OnScreen(transform)
                    || AgeLayout.Band(transform, table) != 1
                )
                {
                    continue;
                }

                // Placed by where it is drawn rather than sorted afterwards, so two buttons in the
                // same place keep the order they were found in.
                float x = LeftEdge(transform);
                int at = commands.Count;
                while (at > 0 && LeftEdge(TransformOf(commands[at - 1])) > x)
                {
                    at--;
                }

                commands.Insert(at, button);
            }

            return commands;
        }

        // The window's wired buttons, and which window they were found on. Held per screen instance,
        // so a hot reload starts with nothing remembered; the window builds its bar once when it loads
        // and never rebuilds it, so walking the whole window on every navigation operation would be
        // paid for an answer that cannot have changed. What DOES change - which of them are drawn,
        // where, and whether they are available - is read live every time.
        private LoadSaveModalWindow _buttonsFrom;
        private List<AgeControlButton> _buttons;

        private List<AgeControlButton> Buttons(LoadSaveModalWindow window)
        {
            if (ReferenceEquals(_buttonsFrom, window) && _buttons != null && AllAlive(_buttons))
            {
                return _buttons;
            }

            _buttonsFrom = window;
            _buttons = Collect(window);
            return _buttons;
        }

        private static List<AgeControlButton> Collect(LoadSaveModalWindow window)
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
                Log.Warn("load save: finding the window's buttons threw: " + e);
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

        /// <summary>A command's name: the caption it is showing, or - for one drawn as a symbol - the
        /// first line of what its tooltip calls it, so no command is announced as nothing.</summary>
        private static string Caption(AgeControlButton button)
        {
            string text = AgeText.Label(OptionsScreen.LabelIn(TransformOf(button)));
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            IList<string> lines = AgeText.Lines(AgeText.Tooltip(TooltipOf(button)));
            return lines.Count > 0 ? lines[0] : null;
        }

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

        private static void Press(AgeControlButton button)
        {
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
                Log.Warn("load save: pressing " + KeyOf(button) + " threw: " + e);
            }
        }

        // ---- the cloud toggle ----

        private static void AddCloudToggle(GraphBuilder builder, LoadSaveModalWindow window)
        {
            AgeControlToggle toggle = window.CloudToggle;
            try
            {
                if (
                    toggle == null
                    || window.CloudToggleGroup == null
                    || !window.CloudToggleGroup.Visible
                )
                {
                    return;
                }
            }
            catch (Exception)
            {
                return;
            }

            AgeControlToggle control = toggle;
            AgeTransform group = window.CloudToggleGroup;
            NodeVtable vtable = GraphNodes.Checkbox(
                () => CloudCaption(group),
                () => State(control),
                () => Flip(control),
                () => Enabled(TransformOf(control)),
                window.CloudToggleTooltip,
                // Reviewable, not spoken. The short/long rule reads this as short because the game
                // wrote it into Content, but what it wrote is two paragraphs about how Steam Cloud
                // works - not the one sentence the rule assumes - and a tick box that recites them
                // every time focus passes over it is unusable. Stated here rather than left to the
                // rule, because it is the row that can see how long the sentence turned out.
                TooltipMode.None
            );
            // The only checkbox at this level of the window: "1 of 2" with the name field below it
            // would be counting two unrelated controls that happen to share a Tab stop, not members
            // of a list either belongs to.
            vtable.SpeaksOwnPosition = true;
            builder.AddItem(ControlId.Referenced(control, "loadsave:cloud"), vtable);
        }

        /// <summary>The words next to the cloud tick, which the game writes beside it rather than on
        /// it; the mod's own name for it only if the window is drawing none.</summary>
        private static string CloudCaption(AgeTransform group)
        {
            string text = AgeText.Label(OptionsScreen.LabelIn(group));
            return string.IsNullOrEmpty(text) ? ModStrings.Get(ModStrings.LoadSaveCloud) : text;
        }

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
                Log.Warn("load save: flipping the cloud toggle threw: " + e);
            }
        }

        // ---- reading the window ----

        /// <summary>
        /// The saves the table is showing, top to bottom.
        ///
        /// "Showing" needs both tests. The table POOLS its rows rather than destroying them, and hides
        /// the surplus the way the engine hides anything that has not declared strict visibility: by
        /// making it TRANSPARENT, which leaves it reporting itself perfectly visible. A row left over
        /// from a longer list therefore has to be recognised by having no save bound to it - which is
        /// also what a row that is between bindings looks like.
        /// </summary>
        private static List<GuiTableLine> Lines(LoadSaveModalWindow window)
        {
            List<GuiTableLine> lines = new List<GuiTableLine>();
            try
            {
                AgeTransform table =
                    window.GuiTable == null ? null : window.GuiTable.LinesTable;
                if (table == null)
                {
                    return lines;
                }

                foreach (GuiTableLine line in table.GetChildren<GuiTableLine>(false))
                {
                    if (
                        line != null
                        && line.AgeTransform != null
                        && line.AgeTransform.Visible
                        && (table.StrictVisibility || line.AgeTransform.Alpha > 0f)
                        && Descriptor(line) != null
                    )
                    {
                        lines.Add(line);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("load save: reading the save list threw: " + e);
            }

            return lines;
        }

        private static List<AgeTransform> Cells(GuiTableLine line)
        {
            List<AgeTransform> cells = new List<AgeTransform>();
            try
            {
                if (line.CellsTable == null)
                {
                    return cells;
                }

                List<AgeTransform> children = line.CellsTable.Children;
                for (int i = 0; i < children.Count; i++)
                {
                    if (children[i] != null)
                    {
                        cells.Add(children[i]);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("load save: reading a save's columns threw: " + e);
            }

            return cells;
        }

        private static List<GuiTableHeader> Headers(LoadSaveModalWindow window)
        {
            List<GuiTableHeader> headers = new List<GuiTableHeader>();
            try
            {
                if (window.GuiTable == null || window.GuiTable.HeadersTable == null)
                {
                    return headers;
                }

                foreach (
                    GuiTableHeader header in
                        window.GuiTable.HeadersTable.GetChildren<GuiTableHeader>(false)
                )
                {
                    if (header != null)
                    {
                        headers.Add(header);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("load save: reading the column headings threw: " + e);
            }

            return headers;
        }

        /// <summary>The heading over a cell: the header the game bound to the same column, found by
        /// the column's own name so the pairing survives a table that reorders its columns, and by
        /// position when a cell does not say which column it is.</summary>
        private static GuiTableHeader HeaderFor(
            List<GuiTableHeader> headers,
            AgeTransform cell,
            int index
        )
        {
            string column = ColumnName(cell);
            if (column != null)
            {
                for (int i = 0; i < headers.Count; i++)
                {
                    try
                    {
                        if (headers[i].PropertyName == column)
                        {
                            return headers[i];
                        }
                    }
                    catch (Exception) { }
                }
            }

            return index < headers.Count ? headers[index] : null;
        }

        private static string ColumnName(AgeTransform cell)
        {
            try
            {
                GuiTableCell component = cell.GetComponent<GuiTableCell>();
                return component != null && component.ColumnInfo != null
                    ? component.ColumnInfo.Name
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string ColumnOf(AgeTransform cell, int index)
        {
            return ColumnName(cell) ?? index.ToString();
        }

        private static AgeTooltip TooltipOfCell(AgeTransform cell)
        {
            try
            {
                GuiTableCell component = cell == null ? null : cell.GetComponent<GuiTableCell>();
                if (component != null && component.Tooltip != null)
                {
                    return component.Tooltip;
                }

                return cell == null ? null : cell.AgeTooltip;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static GameSaveDescriptor Descriptor(GuiTableLine line)
        {
            try
            {
                return line != null ? line.Data as GameSaveDescriptor : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>A stable identity for a save across the every-frame rebuild: the file it lives
        /// in, which survives renames of everything else about the row.</summary>
        private static string SaveKey(GameSaveDescriptor save)
        {
            try
            {
                if (save == null)
                {
                    return "?";
                }

                return string.IsNullOrEmpty(save.SourceFileName) ? save.Title : save.SourceFileName;
            }
            catch (Exception)
            {
                return "?";
            }
        }

        // Two saves cannot share a file, so this is insurance rather than a case the game produces:
        // should the table ever draw two rows the mod cannot tell apart, the second gets a suffix
        // instead of colliding and taking the whole screen down with it.
        private static string Distinct(HashSet<string> taken, string key)
        {
            string candidate = key;
            for (int n = 2; !taken.Add(candidate); n++)
            {
                candidate = key + "#" + n;
            }

            return candidate;
        }

        private static bool Saving(LoadSaveModalWindow window)
        {
            try
            {
                return window.LoadSaveMode == LoadSaveModalWindow.LoadSaveType.Save;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static AgeTransform TableTransform(LoadSaveModalWindow window)
        {
            try
            {
                return window.GuiTable == null ? null : window.GuiTable.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static AgeTransform FieldTransform(AgeControlTextField field)
        {
            try
            {
                return field == null ? null : field.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Whether a widget is drawn clear above the table. False for anything that is not
        /// drawn at all, so a hidden control does not decide where the reachable ones go.</summary>
        private static bool Above(AgeTransform widget, AgeTransform table)
        {
            return widget != null && table != null && AgeLayout.Band(widget, table) < 0;
        }

        private static AgeTransform TransformOf(AgeControl control)
        {
            try
            {
                return control == null ? null : control.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static AgeTooltip TooltipOf(AgeControl control)
        {
            AgeTransform transform = TransformOf(control);
            try
            {
                return transform == null ? null : transform.AgeTooltip;
            }
            catch (Exception)
            {
                return null;
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

        private static bool Visible(AgeControl control)
        {
            AgeTransform transform = TransformOf(control);
            try
            {
                return transform != null && transform.Visible;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Whether a widget is really drawn: its own visibility and every ancestor's. The
        /// window swaps skins by hiding whole containers, so a control in the skin that is not in use
        /// reports itself perfectly visible while nothing of it is on screen.</summary>
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

        /// <summary>How far up a parent chain to look before deciding it is not a chain.</summary>
        private const int MaxAncestors = 64;

        private static float LeftEdge(AgeTransform transform)
        {
            try
            {
                return transform == null ? 0f : transform.GetGlobalPosition().x;
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

        // A row of commands walks left and right on its own; wiring up and down as well means nobody
        // has to guess which axis a bar of three buttons is on.
        private static void LinkVertically(GraphBuilder builder, List<ControlId> ids)
        {
            for (int i = 1; i < ids.Count; i++)
            {
                builder.Connect(ids[i - 1], GraphDir.Down, ids[i]);
                builder.Connect(ids[i], GraphDir.Up, ids[i - 1]);
            }
        }

        private static readonly Action ReleasePointer = PointerFocus.Release;

        /// <summary>Whichever instance of the window is up: the game keeps one for in-game saving
        /// and loading and a second one for the main menu's Load Game.</summary>
        private static LoadSaveModalWindow Window()
        {
            LoadSaveModalWindow window = Instance("LoadSaveModalWindow");
            if (window != null && Showing(window))
            {
                return window;
            }

            window = Instance("OutGameLoadModalWindow");
            return window != null && Showing(window) ? window : null;
        }

        private static bool Showing(LoadSaveModalWindow window)
        {
            try
            {
                return window.Shown;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static LoadSaveModalWindow Instance(string name)
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow(name) as LoadSaveModalWindow
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // The window's own handlers, reached the way the drop list screen reaches the options
        // window's: resolved once, replayed with the argument its click path passes.
        private static readonly MethodInfo LineSelected = OptionsScreen.Handler(
            typeof(LoadSaveModalWindow),
            "OnLineSelection"
        );

        private static readonly MethodInfo NameFieldGainFocus = OptionsScreen.Handler(
            typeof(LoadSaveModalWindow),
            "OnSaveNameTextFieldGainFocusCb"
        );
    }
}
