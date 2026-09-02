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
    /// Three places to be in the save skin and two in the load skin, and Tab moves between them: the
    /// window's contents - the cloud toggle drawn above the table and the table of saves itself - then
    /// the save-name field, which only the save skin draws, and then the row of commands along the
    /// bottom. Which side of the table a control sits on is measured rather than declared, so either
    /// skin reads in the order it is drawn: the cloud toggle bands above the table and the field below
    /// it (measured 2026-09-02: cloud band -1, field band 1), which is what puts the field's stop
    /// between the table's and the commands'.
    ///
    /// The field is a stop of its own because it is a different KIND of thing from the saves it sits
    /// under - a place to type rather than a place to choose - and a player tabbing past the table
    /// should not have to walk through it to reach Save (owner ruling 2026-09-02).
    ///
    /// The table is the game's own <c>GuiTable</c> and is read by the shared <see cref="TableSheet"/>,
    /// which is what gives it the sort headers as a row above the saves, "3 of 12" as the player moves
    /// between saves, a column's heading spoken as the edge crossed into it, and one cell's own heading,
    /// value and tooltip in its review buffer.
    ///
    /// Enter on any cell selects that save - the game's own selection, the one that enables Load and
    /// Delete and copies the name into the save-name field - and says so; the commands then do what
    /// they say. That split (Enter selects, the command acts) is the game's own double-click-versus-
    /// click distinction, kept because it means no save is ever loaded, overwritten or deleted by a
    /// stray Enter. The second click (Ctrl+Alt+Enter) is carried too - it loads the save, or in save
    /// mode overwrites it, each behind the game's own confirmation box, exactly as the mouse's double
    /// click does (owner ruling 2026-08-14: parity over caution; the confirmation is the game's guard).
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
        private static readonly object FieldStop = "loadsave:name-field";
        private const string TitleKey = "loadsave:title";
        private static readonly object CommandStop = "loadsave:commands";

        /// <summary>The saves, read as the game's table. Held across builds like every other table's.
        /// </summary>
        private readonly TableSheet _table = new TableSheet("loadsave:", SaveOf);

        public override string Key
        {
            get { return ModStrings.ScreenLoadSave; }
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
            _editor.Update();
        }

        /// <summary>False while the field has been asked for and the keyboard has not changed hands
        /// yet: what the player types next belongs in the field, not in a search.</summary>
        public override bool CapturesRawInput
        {
            get { return _editor.Pending; }
        }

        /// <summary>Something else has the player's attention. An edit that was asked for and not yet
        /// handed over is abandoned rather than left armed to fire under whatever comes next.</summary>
        public override void OnUnfocus()
        {
            _editor.Cancel();
        }

        public override void Build(GraphBuilder builder)
        {
            LoadSaveModalWindow window = Window();
            if (window == null)
            {
                return;
            }

            // The row's second click ACTS here rather than showing (owner ruling 2026-08-14), and the
            // two modes act differently: in Load it loads the row, in Save it writes over it. Only the
            // load half is named - a hint has one approved sentence, and telling a player in Save mode
            // that the gesture loads would be wrong.
            _table.DoubleClickHint = Saving(window) ? null : ModStrings.HintLoad;

            AgeTransform table = TableTransform(window);
            // Where the field's own stop goes is the same question the cloud toggle answers - which
            // side of the table the window drew it on - so the two are asked the same way and neither
            // is listed. In the save skin the field is below, which is what makes it the middle stop.
            bool fieldAbove = Above(AgeWidgets.Transform(window.SaveNameTextField), table);

            if (fieldAbove)
            {
                AddNameField(builder, window);
            }

            builder.BeginStop(ContentStop);
            ControlId start = BuildContents(builder, window, table);

            if (!fieldAbove)
            {
                AddNameField(builder, window);
            }

            builder.BeginStop(CommandStop);
            BuildCommands(builder, window);

            // The saves are what the player came for in BOTH skins - a save is written over the row
            // that is chosen, and a name is typed only after that (owner ruling 2026-09-02). Where the
            // table has no rows to land on, the start is whatever the table's own stop drew first and
            // never the field, which is a stop away.
            if (start != null)
            {
                builder.SetStart(start);
            }
        }

        // ---- the window's contents ----

        /// <summary>
        /// The table, with whatever the window draws above and below it in its place.
        ///
        /// Which is measured, not listed: the cloud toggle sits in the top right corner above the
        /// headers, and it belongs where a sighted player finds it. Asking the layout means the same
        /// code puts a control the window gains tomorrow in the right place too.
        ///
        /// Answers where the page opens: the first save, or - for a table the game filled with nothing
        /// - the first thing this stop drew, so an empty list is a page the player still arrives on
        /// and hears rather than a page with no cursor at all.
        /// </summary>
        private ControlId BuildContents(
            GraphBuilder builder,
            LoadSaveModalWindow window,
            AgeTransform table
        )
        {
            // The title across the top carries the sentence saying what the window is for, and the
            // game writes a different one in each mode (<c>LoadSaveModalWindow</c> :150-159) - so it
            // is read off the widget and never branched on here. The title is already the screen's
            // name and the table's; the row is for the sentence, which a name cannot carry
            // (<see cref="Captions"/>).
            AgeTransform heading =
                window.WindowTitle == null ? null : window.WindowTitle.AgeTransform;
            // The caption row's own id, named by the same key it was declared under - a graph node is
            // found by its structural key, so this is the row itself and not a copy of it.
            ControlId title = Captions.Row(builder, heading, TitleKey)
                ? ControlId.For(heading, TitleKey)
                : null;

            bool cloudAbove = Above(window.CloudToggleGroup, table);
            ControlId cloud = null;

            if (cloudAbove)
            {
                cloud = AddCloudToggle(builder, window);
            }

            ControlId firstRow = AddSaves(builder, window);

            if (!cloudAbove)
            {
                cloud = AddCloudToggle(builder, window);
            }

            return firstRow ?? title ?? cloud;
        }

        /// <summary>The saves, as the shared table reading: the sort headers as a row above them, then
        /// a row per save, under the window's own title ("Load Game, table", said once on the way in).
        /// Answers with the first row, which is where a load opens.</summary>
        private ControlId AddSaves(GraphBuilder builder, LoadSaveModalWindow window)
        {
            GuiTable table = window.GuiTable;
            if (table == null)
            {
                return null;
            }

            _table.Headers(builder, table);
            return _table.Rows(builder, table, WindowTitle(window)).FirstRow;
        }

        // ---- the save-name field ----

        /// <summary>The deferred hand-over of the keyboard to the game's field, and everything the edit
        /// itself says - shared with every other text box in the game
        /// (<see cref="TextFieldEditor"/>).</summary>
        private readonly TextFieldEditor _editor = new TextFieldEditor();

        /// <summary>
        /// The save-name field, declared while the save skin shows it - and in a Tab stop of its own,
        /// which is what makes it three stops in the save skin and two in the load skin.
        ///
        /// The stop is opened only once the node is really there, so a skin that draws no field leaves
        /// no empty place for Tab to stop at.
        ///
        /// Its value is whatever the field holds - the game's "enter a name here" prompt included,
        /// because that prompt is what a sighted player is looking at - and nothing at all while the
        /// player is typing into it: the editor is reading the keys out one at a time, and re-reading
        /// the whole field after every letter would bury them.
        /// </summary>
        private ControlId AddNameField(GraphBuilder builder, LoadSaveModalWindow window)
        {
            AgeControlTextField field = window.SaveNameTextField;
            if (!AgeWidgets.Visible(AgeWidgets.Transform(field)))
            {
                return null;
            }

            ControlId id = ControlId.For(field, "loadsave:name");
            Cell cell = SettingRows.TextFieldCell(
                field,
                () => ModStrings.Get(ModStrings.LoadSaveSaveName),
                null,
                window,
                NameFieldGainFocus,
                id,
                _editor
            );
            if (cell == null)
            {
                return null;
            }

            // Alone in its stop, so there is no list for it to be a member of.
            cell.Vtable.SpeaksOwnPosition = true;
            builder.BeginStop(FieldStop);
            builder.AddItem(Nodes.Drawn(id, cell.Vtable, field));
            return id;
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

            foreach (AgeControlButton entry in commands)
            {
                AgeControlButton command = entry;
                AgeTooltip tooltip = AgeWidgets.Raw(AgeWidgets.Transform(command));
                NodeVtable vtable = GraphNodes.Button(
                    () => Caption(command),
                    () => AgeWidgets.Press(command),
                    () => AgeWidgets.Operable(AgeWidgets.Transform(command)),
                    tooltip
                );
                vtable.OnFocusVisual = () => PointerFocus.MoveTo(command, tooltip);
                vtable.OnBlurVisual = ReleasePointer;

                builder.AddItem(Nodes.Drawn(
                    ControlId.For(
                        command,
                        "loadsave:command/" + SettingRows.ButtonBar.KeyOf(command)
                    ),
                    vtable,
                    command
                ));
            }
        }

        /// <summary>The wired buttons the window is drawing below its table, left to right - the
        /// shared bar reading (<see cref="SettingRows.ButtonBar"/>) with the one thing that is this
        /// window's own: the header row's sort buttons are level with the table and so are not
        /// commands. The backdrop is wired to nothing, which the bar itself drops.</summary>
        private List<AgeControlButton> Commands(LoadSaveModalWindow window)
        {
            AgeTransform table = TableTransform(window);
            return table == null
                ? new List<AgeControlButton>()
                : _bar.Drawn(
                    window,
                    button => AgeLayout.Band(AgeWidgets.Transform(button), table) == 1
                );
        }

        private readonly SettingRows.ButtonBar _bar = new SettingRows.ButtonBar("load save");

        /// <summary>A command's name: the caption it is showing, or - for one drawn as a symbol - the
        /// first line of what its tooltip calls it, so no command is announced as nothing.</summary>
        private static string Caption(AgeControlButton button)
        {
            string text = AgeText.Label(
                OptionsScreen.LabelIn(AgeWidgets.Transform(button))
            );
            return string.IsNullOrEmpty(text)
                ? CardActions.FirstLine(AgeWidgets.Raw(AgeWidgets.Transform(button)))
                : text;
        }

        // ---- the cloud toggle ----

        /// <summary>Answers the node it declared, which is one of the places the page can open on when
        /// the game gave the table nothing to show.</summary>
        private static ControlId AddCloudToggle(GraphBuilder builder, LoadSaveModalWindow window)
        {
            AgeControlToggle toggle = window.CloudToggle;
            try
            {
                // Flow control: the window keeps the cloud group and draws it only where cloud saves are offered.
                if (
                    toggle == null
                    || window.CloudToggleGroup == null
                    || !window.CloudToggleGroup.Visible
                )
                {
                    return null;
                }
            }
            catch (Exception)
            {
                return null;
            }

            AgeControlToggle control = toggle;
            AgeTransform group = window.CloudToggleGroup;
            NodeVtable vtable = GraphNodes.Checkbox(
                () => CloudCaption(group),
                () => State(control),
                () => AgeWidgets.Toggle(control),
                () => AgeWidgets.Operable(AgeWidgets.Transform(control)),
                window.CloudToggleTooltip
            );
            // The aim is the door's now (GraphNodes.Aim, from the tooltip handed to the factory above):
            // the box's sentence read perfectly in the buffer and the game drew nothing at all, because
            // nothing moved the pointer onto the tick (owner-reported 2026-08-28; TooltipPipe read
            // "over=ColumnLocalizedTitle want=- win=hidden" with the cursor on this node), and that gap
            // is no longer reachable from any factory. What is kept here is what the door cannot say:
            // the TICK is a toggle, and only this call makes it look hovered.
            AgeWidgets.Point(vtable, control, window.CloudToggleTooltip, group);
            // The only checkbox in the window's contents: a position here would be counting unrelated
            // controls that happen to share a Tab stop, not members of a list it belongs to.
            vtable.SpeaksOwnPosition = true;
            ControlId id = ControlId.For(control, "loadsave:cloud");
            builder.AddItem(Nodes.Drawn(id, vtable, control));
            return id;
        }

        /// <summary>The words next to the cloud tick, which the game writes beside it rather than on
        /// it; the mod's own name for it only if the window is drawing none.</summary>
        private static string CloudCaption(AgeTransform group)
        {
            string text = AgeText.Label(OptionsScreen.LabelIn(group));
            return string.IsNullOrEmpty(text) ? ModStrings.Get(ModStrings.LoadSaveCloud) : text;
        }

        // ---- reading the window ----

        /// <summary>What a row STANDS FOR - the save it is showing, which is what identifies it across
        /// the every-frame rebuild and across a re-sort, and is also the test for whether a pooled line
        /// is a real row at all.</summary>
        private static object SaveOf(GuiTableLine line)
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

        /// <summary>Whether a widget is drawn clear above the table. False for anything that is not
        /// drawn at all, so a hidden control does not decide where the reachable ones go.</summary>
        private static bool Above(AgeTransform widget, AgeTransform table)
        {
            return widget != null && table != null && AgeLayout.Band(widget, table) < 0;
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

        /// <summary>Through <see cref="GameWindows"/> rather than the engine's own by-name lookup:
        /// that one logs an Error for every miss and the game forwards Errors to its telemetry, so
        /// asking once a tick for a window the registry has not filled in yet wrote a few hundred
        /// error reports per session into the player's diagnostics file (measured 2026-08-23).
        /// </summary>
        private static LoadSaveModalWindow Instance(string name)
        {
            try
            {
                return GameWindows.Named(name) as LoadSaveModalWindow;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // The window's own handler, reached the way the drop list screen reaches the options window's:
        // resolved once, replayed with the argument its click path passes.
        private static readonly MethodInfo NameFieldGainFocus = GameHandlers.Method(
            typeof(LoadSaveModalWindow),
            "OnSaveNameTextFieldGainFocusCb"
        );
    }
}
