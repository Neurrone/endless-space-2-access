using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.UI;

// The game has its own NewGameScreen; this file adapts it, so the two names have to coexist.
using GameNewGame = NewGameScreen;

namespace ES2Access.Screens
{
    /// <summary>
    /// The lobby a single-player game is set up in, made navigable.
    ///
    /// The page is six panels the game lays out in a grid - Empire, Competitors, Chat, Session,
    /// Gameplay, Galaxy - and a row of buttons along the bottom. Each panel is a Tab stop in the order
    /// it is drawn, announced by the heading the game wrote on it, and every control inside it is a row
    /// of its own: up and down walk the rows, and left and right are left to the control standing on
    /// (a slider's step). Nowhere on this screen does an arrow mean "the next column" - the Competitors
    /// grid draws a faction and a colour side by side, and they are two rows here, because a player who
    /// has to remember which axis a control is on has to look at the screen.
    ///
    /// The panels are read from the game's own description of the page rather than from a list of the
    /// settings it happens to have today: <c>NewGameScreenGuiElement</c> gives the categories and their
    /// entries, <c>NewGameCategoryPanel.Load</c> instantiates one prefab per entry from the setting
    /// database, and this screen walks whatever came out of that - so a setting added by a patch, or one
    /// the game shows only in multiplayer, arrives navigable with nothing declared here. How one
    /// setting reads and is worked is <see cref="SettingRows"/>, shared with the advanced-settings
    /// modal the Advanced buttons open.
    ///
    /// Escape is the game's. <c>NewGameScreen.HandleInput</c> (:198-220) answers Exit by leaving the
    /// lobby, which is the same route the Back button takes (<c>OnClickBackCb</c> :543-546), and the
    /// screen claims no key of its own. Start is declared and wired to the button's own handler
    /// (<c>OnClickStartCb</c> :538-541, which in single player sets <c>Session.LocalPlayerReady</c> and
    /// launches the game); nothing here presses it.
    ///
    /// The screen stands down while any modal is up; both of the ones this page opens - the advanced
    /// settings and the faction chooser - have screens of their own, which take over while they are
    /// there and hand the cursor back to the button that opened them.
    /// </summary>
    public sealed class NewGameScreen : Screen
    {
        private const string ActionsStop = "newgame:actions";

        private const string ConnectingStop = "newgame:connecting";

        /// <summary>How long the launch lock has to hold before it is worth saying. The lock arrives from
        /// a static session event and a state the game passes THROUGH would otherwise announce itself
        /// twice for nothing.</summary>
        private const int LockSettleFrames = 10;


        /// <summary>The game's own names for the two controls the lobby draws without a caption: the
        /// faction list beside a portrait and the wordless colour swatch next to it. Both are named in
        /// the localization corpus under the thing they change, which is where a control with no words
        /// of its own is always named.</summary>
        private const string FactionTitleKey = "%FactionNameTitle";

        private const string ColorTitleKey = "%EmpireColorTitle";

        // Reused across builds rather than allocated per frame: Build runs every tick.
        private readonly List<AgeTransform> _entries = new List<AgeTransform>();
        private readonly List<AgeTransform> _slots = new List<AgeTransform>();

        /// <summary>The deferred keyboard hand-over for this page's text boxes.</summary>
        private readonly TextFieldEditor _editor = new TextFieldEditor();

        /// <summary>Whether the "connecting" cover has been announced, and the two halves of the launch
        /// lock: what the game reports now, what was last said about it, and how long the current answer
        /// has held.</summary>
        private bool _connectingTold;

        private bool _lockSeen;
        private bool _lockTold;
        private int _lockSettling;

        private static readonly Func<AgeTransform, AgeTransform> Itself = widget => widget;

        public override string Key
        {
            get { return "screen.new-game"; }
        }

        /// <summary>The same layer as the main menu it replaces: it is the other full-screen out-game
        /// page, and the two are never up together (showing this one hides the menu). The advanced
        /// settings sit at 5 over it and the tutorial picker at 90.</summary>
        public override int Layer
        {
            get { return 0; }
        }

        /// <summary>The heading the game drew across the top of the page, which is the page's name in
        /// the game's own words.</summary>
        public override string ScreenName
        {
            get
            {
                string title = AgeText.Label(OptionsScreen.LabelIn(WindowTransform()));
                return string.IsNullOrEmpty(title)
                    ? ModStrings.Get(ModStrings.ScreenNewGame)
                    : title;
            }
        }

        /// <summary>Ours while the lobby is shown, has finished animating in, and nothing is on top of
        /// it.</summary>
        public override bool IsActive()
        {
            GameNewGame window = Window();
            try
            {
                if (window == null || !window.Shown || !window.IsReady)
                {
                    return false;
                }

                GuiManager gui = Gui.GuiServiceAvailable ? Gui.GuiService as GuiManager : null;

                // Operable as well as shown: the engine disables the whole page while a modal is over
                // it and re-enables it a frame or so AFTER the modal reports itself gone, so a screen
                // that only asked about the modal arrived on a page where every button still read
                // "unavailable" - and, the parts being live, nothing ever said otherwise.
                return gui != null
                    && !gui.IsAnyModalVisible
                    && AgeWidgets.Operable(window.AgeTransform);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The page is left for a modal - the faction chooser, the advanced settings - and
        /// comes straight back, so the cursor waits on the control that opened it rather than starting
        /// again at the top of the first panel.</summary>
        public override bool KeepStateOnPop
        {
            get { return true; }
        }

        /// <summary>Escape belongs to the game: the window handles Exit itself and leaves the lobby,
        /// which is exactly what the Back button does.</summary>
        public override bool Back()
        {
            return false;
        }

        public override void OnUpdate()
        {
            _editor.Update();
            AnnounceLobbyStates();
        }

        /// <summary>
        /// The two things a multiplayer lobby does to the whole page rather than to a control.
        ///
        /// A joiner's lobby is covered by <c>AfterJoinLoadingPanel</c> until the host's slots replicate
        /// (<c>OnBeginShow</c> :363-366, hidden by <c>ILobbySlotProvider_OnCollectionChange</c>
        /// :609-614). It is a plain panel, so nothing about it makes the page inactive and both slot
        /// panels quietly draw nothing underneath it while there are fewer than two slots - a page that
        /// says nothing at all unless the cover is announced in the game's own words.
        ///
        /// <c>GuiLocked</c> arrives from the static <c>SessionState.OnLockLobbyUI</c> event five seconds
        /// before the game launches (:555-559) and switches off the empire panel and every slot's Join,
        /// Lock and Invite at once. Without a word for it, thirty controls turn into thirty
        /// "unavailable"s with no reason given. Measured: a session REOPENED by a setting change - the
        /// Session Mode drop list - does not raise it, so the state does not flicker under an ordinary
        /// edit; the settle is there for the states the game passes through on the way out of a lobby.
        /// </summary>
        private void AnnounceLobbyStates()
        {
            GameNewGame window = Window();
            if (window == null)
            {
                return;
            }

            AgeTransform connecting = Transform(window.AfterJoinLoadingPanel);
            if (connecting == null || !SettingRows.Drawn(connecting))
            {
                _connectingTold = false;
            }
            else if (!_connectingTold)
            {
                _connectingTold = true;
                Voice.Say(AgeWidgets.TextOf(connecting), false);
            }

            bool locked = Locked(window);
            if (locked != _lockSeen)
            {
                _lockSeen = locked;
                _lockSettling = LockSettleFrames;
                return;
            }

            if (_lockSettling > 0 && --_lockSettling == 0 && locked != _lockTold)
            {
                _lockTold = locked;
                Voice.Say(
                    ModStrings.Get(
                        locked ? ModStrings.NewGameLobbyLocked : ModStrings.NewGameLobbyUnlocked
                    ),
                    false
                );
            }
        }

        private static bool Locked(GameNewGame window)
        {
            try
            {
                return window.GuiLocked;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>A text editor has been asked for and the keyboard has not changed hands yet:
        /// what the player types next is meant for the field, not for a search.</summary>
        public override bool CapturesRawInput
        {
            get { return _editor.Pending; }
        }

        /// <summary>Something else has the player's attention - a modal, a drop list, or the page has
        /// gone. An editor that was asked for and not yet opened goes with them.</summary>
        public override void OnUnfocus()
        {
            _editor.Cancel();
        }

        public override void Build(GraphBuilder builder)
        {
            GameNewGame window = Window();
            if (window == null)
            {
                return;
            }

            BuildConnecting(builder, window);

            IList<AgeTransform> categories = Children(window.CategoryPanelsContainer);
            for (int i = 0; categories != null && i < categories.Count; i++)
            {
                BuildCategory(builder, categories[i]);
            }

            builder.BeginStop(ActionsStop);
            BuildActions(builder, window);
        }

        /// <summary>The cover a joiner's lobby sits behind until the host's slots arrive, as a line of its
        /// own at the top of the page - the game writes "Connecting to lobby…" on it. The panels under it
        /// are still declared: each row of them is drawn-gated, so a lobby that has nothing yet
        /// contributes nothing and the page grows into itself as the slots land.</summary>
        private static void BuildConnecting(GraphBuilder builder, GameNewGame window)
        {
            AgeTransform panel = Transform(window.AfterJoinLoadingPanel);
            if (panel == null || !SettingRows.Drawn(panel))
            {
                return;
            }

            builder.BeginStop(ConnectingStop);
            SettingRows.AddReadout(builder, panel, "newgame:connecting");
        }

        // ---- one panel per category ----

        /// <summary>One drawn panel: its own Tab stop, its heading pushed as the level the controls sit
        /// under, and a row per control in the order the panel drew them.</summary>
        private void BuildCategory(GraphBuilder builder, AgeTransform widget)
        {
            NewGameCategoryPanel panel = Get<NewGameCategoryPanel>(widget);
            if (panel == null || !SettingRows.Drawn(widget))
            {
                return;
            }

            string key = CategoryKey(panel);
            builder.BeginStop("newgame:cat/" + key);

            // The heading is the level its settings sit under - and a row of its own as well, because
            // the game wrote a description for each category and hung it on the panel's own tooltip
            // (NewGameCategoryPanel.Bind :47 fills CategoryTooltip from the category's description).
            // A level is a spoken phrase with no review buffer behind it, so that paragraph would
            // otherwise have nowhere to live; the widget it actually hangs on is named here rather than
            // guessed at, since it is a field of the panel and not the label.
            bool named = Captions.Push(
                builder,
                Transform(panel.CategoryNameLabel),
                "newgame:cat/" + key + "/title",
                null,
                Transform(panel.CategoryTooltip)
            );

            try
            {
                BuildEntries(builder, panel, key);
                SettingRows.AddButton(
                    builder,
                    panel.AdvancedButton,
                    "newgame:" + key + "/advanced"
                );
            }
            finally
            {
                Captions.Pop(builder, named);
            }
        }

        /// <summary>The panel's entries in the order they are drawn - the settings grid lays four of
        /// them out two by two, so the rectangles are what says which is read first, not the order the
        /// table happens to hold them in.</summary>
        private void BuildEntries(GraphBuilder builder, NewGameCategoryPanel panel, string key)
        {
            _entries.Clear();
            IList<AgeTransform> children = Children(panel.NewGameEntriesTable);
            for (int i = 0; children != null && i < children.Count; i++)
            {
                if (SettingRows.Drawn(children[i]))
                {
                    _entries.Add(children[i]);
                }
            }

            foreach (List<AgeTransform> row in AgeLayout.Rows(_entries, Itself))
            {
                for (int i = 0; i < row.Count; i++)
                {
                    BuildEntry(builder, row[i], key);
                }
            }
        }

        /// <summary>What one entry of a panel turns out to be. Most are settings built from the setting
        /// database; the four the game builds from prefabs of its own are recognized by the component
        /// they carry, and anything else is read for whatever it draws.</summary>
        private void BuildEntry(GraphBuilder builder, AgeTransform widget, string key)
        {
            SettingItem setting = Get<SettingItem>(widget);
            if (setting != null)
            {
                SettingRows.Add(
                    builder,
                    setting,
                    "newgame:" + key + "/" + SettingRows.SettingKey(setting),
                    _editor
                );
                return;
            }

            NewGameEmpireSlotPanel empire = Get<NewGameEmpireSlotPanel>(widget);
            if (empire != null)
            {
                BuildEmpireSlot(builder, empire);
                return;
            }

            NewGameCompetitorSlotsPanel competitors = Get<NewGameCompetitorSlotsPanel>(widget);
            if (competitors != null)
            {
                BuildCompetitors(builder, competitors);
                return;
            }

            NewGameChatPanel chat = Get<NewGameChatPanel>(widget);
            if (chat != null)
            {
                AddChatHistory(builder, chat);
                SettingRows.AddTextField(builder, chat.ChatTextField, "newgame:chat/field", _editor);
                return;
            }

            BuildUnmodelled(builder, widget, key);
        }

        /// <summary>
        /// What has been said in the lobby, as a row of its own above the field it is typed into - the
        /// lines are the row's reviewable content, so the player walks the history at their own pace
        /// instead of hearing fifty of them at once.
        ///
        /// Gated on the line list the game only shows in multiplayer: in single player the panel is
        /// soft-hidden, with the lines invisible and the field disabled
        /// (<c>NewGameChatPanel.SessionService_SessionChange</c> :43-65), so a single-player lobby grows
        /// no row here at all. The lines themselves come from <see cref="SessionChat"/>, which is also
        /// what speaks them as they arrive - one wording, two surfaces.
        /// </summary>
        private static void AddChatHistory(GraphBuilder builder, NewGameChatPanel chat)
        {
            AgeTransform lines = Transform(chat.ChatLinesScrollView);
            if (lines == null || !SettingRows.Drawn(lines))
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Readout(
                () => ModStrings.Get(ModStrings.NewGameChatLog),
                SessionChat.Latest,
                SessionChat.History,
                null,
                // The log grows under the cursor and every arriving line is already spoken by the
                // watcher; re-reading the newest one under focus as well would say it twice.
                false
            );
            builder.AddItem(Nodes.Drawn(ControlId.For(lines, "newgame:chat/lines"), vtable, lines));
        }

        // ---- the player's own empire ----

        /// <summary>The Empire panel: the portrait button that opens the faction chooser, the star
        /// rating the game draws on it, the player's name, the faction and colour lists, the two lines
        /// the faction is described by, and the custom-skin boxes the deluxe editions add.</summary>
        private void BuildEmpireSlot(GraphBuilder builder, NewGameEmpireSlotPanel panel)
        {
            PlayerCompetitorSlot slot = panel.PlayerCompetitorSlot;
            if (slot == null || !SettingRows.Drawn(Transform(slot)))
            {
                return;
            }

            // The chooser it opens has no screen of its own yet, and its own Escape COMMITS the
            // highlighted faction rather than cancelling - so the button is declared as the game wires
            // it (PlayerCompetitorSlot.OnSelectFactionCb :234-241) and nothing here presses it.
            SettingRows.AddButton(builder, slot.PortraitButton, "newgame:empire/change");
            SettingRows.AddReadout(builder, slot.DifficultyHintGroup, "newgame:empire/difficulty");
            SettingRows.AddTextField(
                builder,
                slot.PlayerNameTextField,
                "newgame:empire/name",
                _editor
            );
            AddDropList(builder, slot.FactionDropList, FactionTitleKey, "newgame:empire/faction");
            AddDropList(builder, slot.EmpireColorDropList, ColorTitleKey, "newgame:empire/color");
            SettingRows.AddReadout(builder, Owner(slot.GameplayAffinityLabel), "newgame:empire/affinity");
            SettingRows.AddReadout(builder, Owner(slot.MajorPopulationLabel), "newgame:empire/population");
            AddSkinToggle(
                builder,
                slot.HeroesCustomSkinGroup,
                slot.HeroesCustomSkinToggle,
                "newgame:empire/heroes-skin"
            );
            AddSkinToggle(
                builder,
                slot.FactionCustomSkinGroup,
                slot.FactionCustomSkinToggle,
                "newgame:empire/faction-skin"
            );
        }

        // ---- the other empires ----

        /// <summary>The Competitors grid. Each slot becomes a band the region keys jump between, so
        /// Alt+down goes to the next empire rather than through four rows to reach it, and inside a
        /// band the four things the game drew are four rows.
        ///
        /// A band is also a named level, because the game names none of them: every slot is captioned
        /// "AI" and the only thing telling two of them apart on screen is which row of the grid they
        /// were drawn in. The number is that place in the panel counted from the top, so the empire
        /// panel above - the player's own, which is not a competitor - is not counted; the level is
        /// pushed rather than added as a row so that arriving in a band says whose it is and walking
        /// inside one does not repeat it.</summary>
        private void BuildCompetitors(GraphBuilder builder, NewGameCompetitorSlotsPanel panel)
        {
            _slots.Clear();
            IList<AgeTransform> children = Children(panel.OtherCompetitorSlotsTable);
            for (int i = 0; children != null && i < children.Count; i++)
            {
                if (SettingRows.Drawn(children[i]) && Get<CompetitorSlot>(children[i]) != null)
                {
                    _slots.Add(children[i]);
                }
            }

            int index = 0;
            foreach (List<AgeTransform> row in AgeLayout.Rows(_slots, Itself))
            {
                for (int i = 0; i < row.Count; i++)
                {
                    builder.SetRegion("newgame:competitor/" + index);
                    builder.PushContext(ModStrings.Format(ModStrings.NewGamePlayer, index + 1));
                    try
                    {
                        BuildCompetitorSlot(builder, Get<CompetitorSlot>(row[i]), index);
                    }
                    finally
                    {
                        builder.PopContext();
                    }

                    index++;
                }
            }

            builder.SetRegion(null);
            SettingRows.AddButton(builder, panel.InviteButton, "newgame:competitors/invite");
        }

        private void BuildCompetitorSlot(GraphBuilder builder, CompetitorSlot slot, int index)
        {
            if (slot == null)
            {
                return;
            }

            string key = "newgame:competitor/" + index;
            SettingRows.AddTextField(builder, slot.PlayerNameTextField, key + "/name", _editor);

            // The crown is drawn to the LEFT of the name and so before it, but it is a mark ON the name
            // rather than a thing of its own: a player entering a slot needs to hear whose slot it is
            // before hearing what is true of them.
            AddStateIcon(
                builder,
                Transform(slot.HostIcon),
                key + "/host",
                () => ModStrings.Get(ModStrings.NewGameHost)
            );
            // Faction before the state strip (owner order, 2026-08-21): the game draws the
            // difficulty marks between the name and the faction, but what a slot IS comes before
            // how hard it plays - so a band reads name, faction, difficulty and the multiplayer
            // marks, colour.
            AddDropList(builder, slot.FactionDropList, FactionTitleKey, key + "/faction");
            BuildSlotStates(builder, slot, key);
            AddDropList(builder, slot.EmpireColorDropList, ColorTitleKey, key + "/color");
        }

        /// <summary>
        /// The strip of marks and buttons the game draws beside a slot's name, in the order it draws them
        /// - which is the order the widgets sit in the strip, because that is what the game's own
        /// <c>ArrangeChildren</c> lays them out by. Each is recognized by which of the slot's widgets it
        /// IS, so the reading of one never lands on another, and each is drawn-gated: in single player
        /// only the difficulty rating is ever there, and the multiplayer five appear exactly when
        /// <c>CompetitorSlot.RefreshStates</c> :226-254 shows them.
        ///
        /// What each of them is:
        /// - Join, on a free slot - and every AI slot IS free (<c>LobbySlot.IsFree</c> is
        ///   <c>IsAI</c>) - takes the local player to that empire.
        /// - Kick, host-only and only on another human's slot.
        /// - Lock, host-only, keeps anybody else off a free slot. A tick rather than a button: the game
        ///   draws it as a toggle carrying the slot's locked state.
        /// - Ready and eliminated are readouts, and the game's own tooltips are complete sentences about
        ///   the player ("This player is ready"), so they are what the marks SAY rather than something
        ///   hanging off them.
        /// </summary>
        private void BuildSlotStates(GraphBuilder builder, CompetitorSlot slot, string key)
        {
            IList<AgeTransform> states = Children(slot.StatesTable);
            for (int i = 0; states != null && i < states.Count; i++)
            {
                AgeTransform widget = states[i];
                if (widget == null || !SettingRows.Drawn(widget))
                {
                    continue;
                }

                if (widget == slot.DifficultyAgainstGroup)
                {
                    SettingRows.AddReadout(builder, widget, key + "/difficulty");
                }
                else if (widget == Transform(slot.JoinButton))
                {
                    SettingRows.AddButton(builder, slot.JoinButton, key + "/join");
                }
                else if (widget == Transform(slot.KickButton))
                {
                    // Named by the mod for the same reason the lock is: the game draws a symbol whose
                    // only words are the mouse instruction on its tooltip, and "Click to kick this
                    // player" is what the button DOES, not what it is called.
                    AddNamedButton(
                        builder,
                        slot.KickButton,
                        () => ModStrings.Get(ModStrings.NewGameKick),
                        key + "/kick"
                    );
                }
                else if (widget == Transform(slot.LockToggle))
                {
                    AddLockToggle(builder, slot.LockToggle, key + "/lock");
                }
                else if (widget == slot.ReadyIconGroup || widget == slot.EliminatedGroup)
                {
                    AddStateIcon(
                        builder,
                        widget,
                        key + (widget == slot.ReadyIconGroup ? "/ready" : "/eliminated")
                    );
                }
            }
        }

        /// <summary>A mark the game draws as a picture and explains in words somewhere else - on the
        /// mark's own tooltip, which is the only words there are for it. Declared as the readout's
        /// tooltip rather than copied into a value of its own, so the one place those words are written
        /// down is what both the line and the review buffer read.</summary>
        private static void AddStateIcon(
            GraphBuilder builder,
            AgeTransform widget,
            string key,
            Func<string> text = null
        )
        {
            if (widget == null || !SettingRows.Drawn(widget))
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Readout(
                () => null,
                text,
                null,
                text == null ? AgeWidgets.Raw(widget) : null
            );
            AgeWidgets.PointAt(vtable, widget);
            builder.AddItem(Nodes.Drawn(ControlId.For(widget, key), vtable, widget));
        }

        /// <summary>A button the game drew as a symbol that its own tooltip does not name, so the mod
        /// supplies the name and the tooltip still says what pressing it does.</summary>
        private static void AddNamedButton(
            GraphBuilder builder,
            AgeControlButton button,
            Func<string> label,
            string key
        )
        {
            AgeTransform widget = Transform(button);
            if (button == null || !SettingRows.Drawn(widget))
            {
                return;
            }

            AgeControlButton it = button;
            NodeVtable vtable = GraphNodes.Button(
                label,
                () => AgeWidgets.Press(it),
                () => AgeWidgets.Operable(widget),
                AgeWidgets.Raw(widget)
            );
            AgeWidgets.Point(vtable, it);
            builder.AddItem(Nodes.Drawn(ControlId.For(button, key), vtable, button));
        }

        /// <summary>The host's lock on a free slot. Named by the mod: the toggle draws no words and its
        /// tooltip is a mouse instruction ("Click to prevent any player from switching to this empire"),
        /// which explains what ticking it does but does not name the thing being ticked.</summary>
        private static void AddLockToggle(
            GraphBuilder builder,
            AgeControlToggle toggle,
            string key
        )
        {
            AgeTransform widget = Transform(toggle);
            if (toggle == null || !SettingRows.Drawn(widget))
            {
                return;
            }

            AgeControlToggle it = toggle;
            NodeVtable vtable = GraphNodes.Checkbox(
                () => ModStrings.Get(ModStrings.NewGameLockEmpire),
                () => it.State,
                () => AgeWidgets.Toggle(it),
                () => AgeWidgets.Operable(widget),
                AgeWidgets.Raw(widget)
            );
            AgeWidgets.Point(vtable, it);
            builder.AddItem(Nodes.Drawn(ControlId.For(toggle, key), vtable, toggle));
        }

        // ---- the bottom row ----

        /// <summary>
        /// Back and Start, in the order they are drawn - and on ONE row, walked left and right.
        ///
        /// The one-control-per-row rule this page follows is about its SETTINGS: a grid of controls
        /// where an arrow across the row would mean something different depending on where you were.
        /// A bar of two buttons along the bottom is the shape every other screen in the mod already
        /// gives its cancel-and-confirm row, and a player who has met one has met them all.
        ///
        /// Taken from the band they share rather than from the window's fields: the window names four
        /// buttons for that one place - Start, Stop, Ready and Unready, only ever one of them drawn -
        /// and does not name Back at all.
        /// </summary>
        private void BuildActions(GraphBuilder builder, GameNewGame window)
        {
            _entries.Clear();
            AgeTransform band = Parent(Transform(window.StartButton));
            IList<AgeTransform> children = Children(band);
            for (int i = 0; children != null && i < children.Count; i++)
            {
                if (SettingRows.Drawn(children[i]) && AgeWidgets.Button(children[i]) != null)
                {
                    _entries.Add(children[i]);
                }
            }

            SettingRows.AddButtons(builder, _entries, "newgame:button/");
        }

        // ---- the shapes only this page has ----
        /// <summary>A list the control opens. The game gives these no caption of their own on this
        /// page, so they are named the way the corpus names them - under the thing they change.
        /// </summary>
        private static void AddDropList(
            GraphBuilder builder,
            AgeControlDropList list,
            string titleKey,
            string key
        )
        {
            AgeTransform widget = Transform(list);
            if (list == null || !SettingRows.Drawn(widget))
            {
                return;
            }

            string title = titleKey;
            SettingRows.AddCombo(builder, list, () => Localized(title), null, key);
        }

        /// <summary>One of the deluxe-edition skin boxes: the group carries the caption and the
        /// sentence, the toggle carries the state.</summary>
        private static void AddSkinToggle(
            GraphBuilder builder,
            AgeTransform group,
            AgeControlToggle toggle,
            string key
        )
        {
            if (group == null || toggle == null || !SettingRows.Drawn(group))
            {
                return;
            }

            AgeTransform band = group;
            AgeControlToggle it = toggle;
            AgeTooltip tooltip = AgeWidgets.Raw(group);
            NodeVtable vtable = GraphNodes.Checkbox(
                () => AgeText.Label(OptionsScreen.LabelIn(band)),
                () => it.State,
                () => AgeWidgets.Toggle(it),
                () => AgeWidgets.Operable(band),
                tooltip
            );
            AgeWidgets.PointAt(vtable, group);
            builder.AddItem(Nodes.Drawn(ControlId.For(toggle, key), vtable, toggle));
        }

        /// <summary>Whatever a panel nobody has modelled draws, a line per group of words. The
        /// downloadable-content strip is one of these and draws nothing at all in single player with no
        /// exclusive content installed, so it contributes no rows - which is the right answer: a stop
        /// with nothing in it does not exist.</summary>
        private void BuildUnmodelled(GraphBuilder builder, AgeTransform widget, string key)
        {
            IList<AgeTransform> children = Children(widget);
            bool any = false;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = children[i];
                if (!SettingRows.Drawn(child) || string.IsNullOrEmpty(AgeWidgets.TextOf(child)))
                {
                    continue;
                }

                any = true;
                SettingRows.AddReadout(builder, child, "newgame:" + key + "/" + Name(widget) + "/" + i);
            }

            if (!any && !string.IsNullOrEmpty(AgeWidgets.TextOf(widget)))
            {
                SettingRows.AddReadout(builder, widget, "newgame:" + key + "/" + Name(widget));
            }
        }

        // ---- reading the window ----

        internal static GameNewGame Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<GameNewGame>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Localized(string key)
        {
            try
            {
                return AgeText.Clean(key);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string CategoryKey(NewGameCategoryPanel panel)
        {
            try
            {
                return panel.Category != null ? panel.Category.Name : panel.name;
            }
            catch (Exception)
            {
                return "?";
            }
        }

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

        private static IList<AgeTransform> Children(AgeTransform widget)
        {
            try
            {
                return widget == null ? null : widget.Children;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static AgeTransform Parent(AgeTransform widget)
        {
            try
            {
                return widget == null ? null : widget.Parent;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static AgeTransform Transform(AgeControl control)
        {
            return AgeWidgets.Transform(control);
        }

        private static AgeTransform Transform(AgeTooltip tooltip)
        {
            return SettingRows.TransformOf(tooltip);
        }

        private static AgeTransform Transform(AgePrimitiveLabel label)
        {
            return SettingRows.TransformOf(label);
        }

        private static AgeTransform Transform(GuiPanel panel)
        {
            try
            {
                return panel == null ? null : panel.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static AgeTransform Transform(AgePrimitiveImage image)
        {
            try
            {
                return image == null ? null : image.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static AgeTransform Transform(CompetitorSlot slot)
        {
            try
            {
                return slot == null ? null : slot.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The page itself, for reading the heading it drew.</summary>
        private static AgeTransform WindowTransform()
        {
            try
            {
                GameNewGame window = Window();
                return window == null ? null : window.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static AgeTransform Transform(AgeTransform widget)
        {
            return widget;
        }

        /// <summary>The band a label was drawn in - the group holding the icon that captions it and the
        /// words themselves.</summary>
        private static AgeTransform Owner(AgePrimitiveLabel label)
        {
            return Parent(Transform(label));
        }
    }
}
