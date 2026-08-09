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

            IList<AgeTransform> categories = Children(window.CategoryPanelsContainer);
            for (int i = 0; categories != null && i < categories.Count; i++)
            {
                BuildCategory(builder, categories[i]);
            }

            builder.BeginStop(ActionsStop);
            BuildActions(builder, window);
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

            string title = AgeText.Label(panel.CategoryNameLabel);
            bool named = !string.IsNullOrEmpty(title);
            if (named)
            {
                builder.PushContext(title);
            }

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
                if (named)
                {
                    builder.PopContext();
                }
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
                SettingRows.AddTextField(builder, chat.ChatTextField, "newgame:chat/field", _editor);
                return;
            }

            BuildUnmodelled(builder, widget, key);
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
        /// band the four things the game drew are four rows.</summary>
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
                    BuildCompetitorSlot(builder, Get<CompetitorSlot>(row[i]), index);
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
            SettingRows.AddReadout(builder, slot.DifficultyAgainstGroup, key + "/difficulty");
            AddDropList(builder, slot.FactionDropList, FactionTitleKey, key + "/faction");
            AddDropList(builder, slot.EmpireColorDropList, ColorTitleKey, key + "/color");
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

            SettingRows.AddButtonRow(builder, _entries, "newgame:button/");
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
                tooltip,
                GraphNodes.ModeFor(tooltip)
            );
            AgeWidgets.PointAt(vtable, group);
            builder.AddItem(ControlId.Referenced(toggle, key), vtable);
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
