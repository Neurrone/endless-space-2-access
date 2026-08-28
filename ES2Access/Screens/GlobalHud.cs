using System;
using System.Collections.Generic;
using System.Reflection;
using Amplitude;
using Amplitude.Unity.Framework;
using Amplitude.Unity.View;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using ES2Access.UI.Input;
using UnityEngine;

namespace ES2Access.Screens
{
    /// <summary>
    /// The bits of the game that are on the screen whatever the player is looking at: what the empire
    /// is worth along the top, the name of the view and its scan lens in the top centre, the quest the
    /// game is tracking, the notification icons and a collapsed tutorial down the right-hand edge, and
    /// the turn controls in the bottom corner.
    ///
    /// None of them belongs to a page. The galaxy, the star system's management page and a planet's
    /// overview are three different view levels of the same running game, and the game draws these
    /// clusters over all three - so a player who walked into a system could still see the End
    /// Turn button, the dust total and the tutorial bar and had no way to reach any of them. They
    /// were declared by the galaxy screen because the galaxy is where they were first met, which is
    /// not a reason for them to live there.
    ///
    /// So they are declared here and every page that is drawn under them asks for them, in the order
    /// they are drawn relative to that page's own content. The stop keys are shared on purpose: a
    /// stop's remembered cursor position is kept per screen, so the same key on three screens is
    /// three positions and not one, and naming them after the cluster rather than after the galaxy is
    /// what stops a reader of the graph believing the star system page has grown a galaxy.
    ///
    /// A stop exists on a frame only if the game is showing something at it. There are no
    /// placeholders: where the game draws nothing for having no notifications, Tab finds nothing
    /// there either, because a stop that says "nothing" is a stop the player has to walk past to
    /// learn what a glance at the screen would have told them.
    ///
    /// The turn changing is watched here for the same reason: it is the one thing on any of these
    /// pages that happens TO the player rather than being done by them, and it goes on happening
    /// wherever they are standing. The watch is instance state, so it is reload-safe by construction
    /// and each page keeps its own. The pinned quest and the MODE THE CURSOR IS IN
    /// (<see cref="AnnounceCursorMode"/>) are watched beside it, both for the same reason: they change
    /// under the player, wherever the player is.
    ///
    /// Everything is read from the game's own model rather than from the labels on the banners. Every
    /// number up there is animated - the dust total counts up to its new value over a second or so -
    /// so the labels are a picture of a number on its way somewhere, and the model is the number.
    /// </summary>
    public sealed class GlobalHud
    {
        public static readonly object EmpireStop = "hud:empire";
        public static readonly object ViewTitleStop = "hud:view-title";
        public static readonly object QuestStop = "hud:quest";
        public static readonly object TutorialStop = "hud:tutorial";
        public static readonly object NotificationStop = "hud:notifications";
        public static readonly object TurnLogStop = "hud:turn-log";
        public static readonly object TurnStop = "hud:turn";

        private List<Fleet> _idleFleets = new List<Fleet>();

        private int _turn = -1;

        /// <summary>The multiplayer wait: whether the player's turn is over and the game is still on the
        /// others, and how many of them were still playing when that was last said. Instance state, like
        /// the turn watch, so each page keeps its own and a reload starts the watch over.</summary>
        private bool _waiting;

        private int _playing = -1;

        /// <summary>The two private fields of <c>EndTurnWindow</c> the turn timer is only readable from,
        /// looked up once per load rather than per frame.</summary>
        private static FieldInfo _timerEnd;

        private static FieldInfo _timerKind;

        /// <summary>The journal this page is listening to, kept so that the subscription can be given
        /// back. Instance state, so a hot reload takes it with the page.</summary>
        private QuestJournal _journal;

        /// <summary>Set by the journal's own event and drained by <see cref="Update"/>: the watcher
        /// only records that the pinned quest changed, and the per-frame pump is what speaks.</summary>
        private bool _questChanged;

        /// <summary>The instruction the game is currently showing for the cursor's mode, or null while
        /// the cursor is in no mode. Instance state, so each page keeps its own and a hot reload starts
        /// the watch over.</summary>
        private string _instruction;

        // ---- the passive watch ----

        /// <summary>Start the watch from the turn that is showing, so arriving on a page never
        /// announces a turn nobody just took. The pinned quest needs no such baseline - the game
        /// raises an event when it changes, so there is nothing to compare against. The cursor mode is
        /// baselined for the same reason as the turn: walking onto a page while a mode is already up
        /// must not announce it as though the player had just asked for it.</summary>
        public void Baseline()
        {
            _turn = Turn();
            _questChanged = false;
            _instruction = Instruction();
            _waiting = WaitingForOthers();
            _playing = PlayersPlaying(TurnWindow());
            WatchQuests();
        }

        /// <summary>Stop watching. The next arrival baselines afresh rather than comparing against
        /// however many turns passed while the player was somewhere else, and the journal gets its
        /// subscription back - the page is not there to announce anything.</summary>
        public void Forget()
        {
            _turn = -1;
            _questChanged = false;
            _instruction = null;
            _waiting = false;
            _playing = -1;
            ForgetQuests();
        }

        /// <summary>The turn ends and the next one begins on the game's schedule, not the player's -
        /// and while it does, the player is usually nowhere near the End Turn button. The same is
        /// true of the quest the game is tracking: finishing one pins the next, and of the mode the
        /// mouse cursor is in, which the game announces by writing an instruction across the screen.
        /// </summary>
        public void Update()
        {
            AnnounceTurn();
            AnnounceTurnWait();
            AnnounceQuest();
            AnnounceCursorMode();
        }

        private void AnnounceTurn()
        {
            try
            {
                int turn = Turn();
                if (turn < 0 || turn == _turn)
                {
                    return;
                }

                bool first = _turn < 0;
                _turn = turn;
                if (!first)
                {
                    Voice.Say(ModStrings.Format(ModStrings.GalaxyTurn, turn), false);
                }
            }
            catch (Exception e)
            {
                Log.Warn("hud: watching the turn threw: " + e);
            }
        }

        /// <summary>
        /// The other half of a multiplayer turn: the player has ended theirs and the game is waiting on
        /// everybody else.
        ///
        /// The game shows it by rewriting the End Turn caption to "Pending"
        /// (<c>EndTurnWindow.RefreshEndTurnLabel</c> :1123-1160) and by unlit slots on the ready ring,
        /// and nothing announces either: the turn NUMBER does not change while the wait lasts, so the
        /// turn watch above sees nothing until it is over. So the wait says itself when it starts, and
        /// each time one more player finishes - which is the only progress there is to report while the
        /// player can do nothing but listen.
        ///
        /// Gated on the ready ring, which the game draws outside single player only (:735): in a solo
        /// game the same client states are passed through on every turn and none of them is a wait.
        /// </summary>
        private void AnnounceTurnWait()
        {
            try
            {
                EndTurnWindow window = TurnWindow();
                int playing = PlayersPlaying(window);
                if (playing < 0)
                {
                    _waiting = false;
                    _playing = -1;
                    return;
                }

                bool waiting = WaitingForOthers();
                if (waiting && !_waiting)
                {
                    Voice.Say(ModStrings.Get(ModStrings.GalaxyTurnWaiting), false);
                }
                else if (waiting && playing > 0 && _playing > playing)
                {
                    Voice.Say(PlayersText(window), false);
                }

                _waiting = waiting;
                _playing = playing;
            }
            catch (Exception e)
            {
                Log.Warn("hud: watching the multiplayer wait threw: " + e);
            }
        }

        /// <summary>Whether the player's own turn is over and the game has not started the next one -
        /// the state the End Turn caption reads "Pending" in.</summary>
        private static bool WaitingForOthers()
        {
            try
            {
                return Gui.GuiGameWindowService != null
                    && Gui.GuiGameWindowService.CurrentGameClientStateType
                        == typeof(GameClientState_Turn_Finished);
            }
            catch (Exception)
            {
                return false;
            }
        }

        // ---- the mode the cursor is in ----

        /// <summary>
        /// The game's own instruction for the mode the mouse is in, when it changes, and a word when the
        /// mode ends.
        ///
        /// Some orders are given in two steps: pressing "launch a probe", "take this system", "fire the
        /// obliterator", "start a hacking operation" does not act - it puts the CURSOR into a mode and
        /// waits for the player to click a target. Nine cursors work this way, and the only thing on
        /// screen saying so is a line of text the game writes across the top
        /// (<c>UserInstructionsWindow</c>, shown by <c>GuiManager</c>:1552 exactly while
        /// <c>CurrentCursor.HasUserInstructions</c>). Without this, pressing such a button reads as doing
        /// nothing at all, and the player is left in a mode they cannot see.
        ///
        /// It is announced and nothing more: entering the mode says what the game says, and leaving it
        /// says that it is over. What the mode is OPERATED with is the galaxy page's business - Enter
        /// confirms at the focused node and backslash is the mode's own right click
        /// (<see cref="ES2Access.UI.CursorTargeting"/>) - and this is the one place that says the mode
        /// ended, however it ended.
        ///
        /// Watched through the WINDOW rather than through the cursor service: the window's caption is the
        /// finished, localized sentence the player would read, the game has already decided whether the
        /// mode is one worth showing (a mode with no instruction draws nothing), and it is two field
        /// reads per frame against a service subscription that would have to be given back on every page
        /// change.
        /// </summary>
        private void AnnounceCursorMode()
        {
            try
            {
                string instruction = Instruction();
                if (instruction == _instruction)
                {
                    return;
                }

                bool ended = string.IsNullOrEmpty(instruction);
                _instruction = instruction;
                bool dismissed = false;
                if (!ended)
                {
                    // A mode the game has just armed takes the map, and the galaxy's inspect cell is a
                    // mode OF the map: with both up the arrows would mean the square while the banner
                    // asked for a target, and Enter would land the cell rather than confirm. So the
                    // cell goes first and says so (owner ruling 2026-08-20). Every one of the nine
                    // cursors, not the fleet ones alone - this is the one place the mod sees an
                    // instruction appear.
                    dismissed = GalaxyInspect.Dismiss();
                }

                // ...AND THEN WHERE THE PLAYER IS STANDING, which the landing says for itself. The
                // tree cursor never moved while the cell was up, so the ordinary "say it only when the
                // cursor moved" rule would leave the player with a mode ended, an instruction to obey,
                // and no idea which control the keys have gone back to. Asking the navigator for its
                // next landing is the mode's own Escape route (<c>GalaxyInspect.Exit</c>) requested by
                // somebody else, rather than a line composed here.
                //
                // Only the ARMING path. The six zoom-in fleet actions dismiss the cell too and each
                // goes on to seat the cursor somewhere new, which announces itself; a re-read there
                // would say the seat twice.
                GraphNavigator navigator = ModEntry.Navigator;
                if (dismissed && navigator != null)
                {
                    navigator.AnnounceNextLanding();
                }

                // THE INSTRUCTION IS SAID ONCE (owner rulings 2026-08-20). While a mode is waiting the
                // map stop names ITSELF after the game's banner - <c>GalaxyHudScreen.MapContext</c>
                // reads this very sentence - so anything that reads that stop out says the instruction
                // too, and a standalone line in front of it is the same words twice in a row.
                //
                // Two things read it out, and the check is one question about the map stop rather than
                // anything about which cursor was armed. The re-read just asked for above, when the
                // player is standing in the stop already; or a landing ALREADY IN FLIGHT into it - the
                // launch-probe mode seats the cursor on its first bearing, which lives under the acting
                // fleet's system and so inside this stop, and that landing stays outstanding for the
                // frames its collapsed branches take to open.
                //
                // Anything else falls through and the line is spoken on its own, which is what an
                // arming the mod did not see coming (a dev-injected cursor) has to have: the
                // instruction may be redundant, but it must never be missing.
                // The mode ENDING is never carried by either: the stop goes back to being called "Map"
                // the moment the banner goes, so nothing else is going to say that it is over.
                bool reReadCarriesIt = dismissed && GalaxyHudScreen.CursorOnMap();
                bool landingCarriesIt =
                    !ended
                    && navigator != null
                    && GalaxyHudScreen.IsMapStop(navigator.PendingStopKey);
                if (reReadCarriesIt || landingCarriesIt)
                {
                    return;
                }

                Voice.Say(
                    ended ? OptionalText.Phrase(ModeEndedKey) : instruction,
                    false
                );
            }
            catch (Exception e)
            {
                Log.Warn("hud: watching the cursor mode threw: " + e);
            }
        }

        /// <summary>The mod's own word for a mode ending, which the game marks by simply taking its
        /// instruction off the screen. Optional: a build without the phrase says nothing rather than
        /// reading the key.</summary>
        private const string ModeEndedKey = "cursor.mode-ended";

        /// <summary>What the game is instructing the player to do with the cursor, or null while it is
        /// instructing nothing. The window is hidden whenever there is no mode, so its own visibility is
        /// the whole test.
        ///
        /// Shared rather than private because the galaxy page names its map stop with this sentence
        /// while a mode is up (<c>GalaxyHudScreen.MapContext</c>): one read of the game's own caption,
        /// so the words the mode was announced with and the words the stop is called by cannot
        /// differ.</summary>
        internal static string Instruction()
        {
            UserInstructionsWindow window = InstructionsWindow();
            try
            {
                return window == null || !window.Shown
                    ? null
                    : AgeText.Label(window.UserIntructionCaption);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static UserInstructionsWindow InstructionsWindow()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<UserInstructionsWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---- the empire ----

        /// <summary>
        /// What the empire is worth, in the rows the corner of the screen it comes from is drawn in:
        /// the strip of icons that open the game's screens, the running totals under it, the research
        /// line under those, and the stockpiles under that.
        ///
        /// The rows are worked out from the rectangles, which is why the whole cluster is gathered
        /// before any of it is declared. Four panels contribute to it and none of them knows about
        /// the others, so where their lines fall relative to each other is a question only this can
        /// answer - and it answers it by looking.
        ///
        /// EACH ROW IS ITS OWN REGION (owner ruling, 2026-08-19). This is the first stop on every
        /// page in the game, and it is four unrelated things stacked in one corner: the strip that
        /// opens the game's screens, the three totals the empire is spending, what is being
        /// researched, and the stockpiles. Walked as one flat stop, the eighth screen icon and the
        /// first total were neighbours with nothing between them saying the player had crossed from
        /// one thing to another. So the rows carry the levels, announced on the way in and not on
        /// every node, which is the shape the galaxy's own panels already have - and each row is a
        /// REGION as well as a level, so that Alt+Up/Down jumps panel to panel down the corner. The two
        /// halves ride on different mechanisms and shipped apart once: a level is announced by the path
        /// diff and a region jump reads the node's own region key, so the rows read as four things and
        /// the jump key still did nothing (owner-reported, 2026-08-19). They are set together now
        /// (<see cref="Name"/>) for exactly that reason.
        ///
        /// The names are the mod's own: the game draws these banners as icons and figures with no
        /// caption anywhere on them (measured - <c>ControlBanner</c>, <c>EmpireBanner</c> and
        /// <c>ResourcesPanel</c> hold tables and value areas and no label of their own), so there is
        /// no game word to prefer. The faction panels the game stacks underneath for the empires that
        /// have them are named too (<see cref="AddFactionPanels"/>), and five of those seven DO have a
        /// game word, because the thing each of them counts is a titled thing in the game's own data.
        /// </summary>
        public void Empire(GraphBuilder builder)
        {
            GameOverlayWindow window = OverlayWindow();
            Empire empire = PlayerEmpire();
            if (window == null || empire == null)
            {
                return;
            }

            List<Cell> cells = new List<Cell>();
            int from = cells.Count;
            AddScreenToggles(cells, window.ControlBanner);
            Name(cells, from, ModStrings.Get(ModStrings.HudControlsPanel), "controls");
            from = cells.Count;
            AddTotals(cells, window.EmpireBanner, empire);
            Name(cells, from, ModStrings.Get(ModStrings.HudKeyResourcesPanel), "key-resources");
            from = cells.Count;
            AddResearch(cells, window.EmpireBanner, empire);
            Name(cells, from, ModStrings.Get(ModStrings.GalaxyResearch), "research");
            from = cells.Count;
            AddStockpiles(cells, window.StrategicsBanner);
            Name(cells, from, ModStrings.Get(ModStrings.HudStrategicResourcesPanel), "strategics");
            AddFactionPanels(cells, window);

            builder.BeginStop(EmpireStop);
            int line = 0;
            foreach (List<Cell> row in AgeLayout.Rows(cells, CellWidget))
            {
                string named = RowName(row);
                // EVERY row carries a region, not only the named ones: the jump is asked of the focused
                // node's own region key, so one unregioned line in the middle of the stop is a key that
                // does nothing exactly there. A line two panels share has no name to take one from and
                // takes its place in the stop instead.
                string region = RowRegion(row);
                builder.SetRegion(EmpireStop + "/" + (region ?? "line/" + line));
                line++;
                if (named != null)
                {
                    builder.PushContext(named);
                }

                builder.StartRow();
                foreach (Cell cell in row)
                {
                    builder.AddItem(Nodes.Drawn(cell.Id, cell.Vtable, cell.Widget));
                }

                builder.EndRow();
                if (named != null)
                {
                    builder.PopContext();
                }
            }

            builder.SetRegion(null);
        }

        /// <summary>Name the cells one contributor has just added, so that the row they fall into can
        /// say what it is and be jumped to. Applied AFTER the contributor rather than passed into it:
        /// which panel a cell came from is this method's own knowledge, and the helpers that read the
        /// banners have no business carrying a word about the player's ear.
        ///
        /// The word and the region key are set together and never apart: a row the player hears as a
        /// thing of its own is a row the region jump has to be able to land on, and the two coming from
        /// one call is what stops a later contributor from adding the level and forgetting the key
        /// (which is exactly what the four banner rows shipped with).</summary>
        private static void Name(List<Cell> cells, int from, string named, string region)
        {
            if (string.IsNullOrEmpty(named))
            {
                return;
            }

            for (int i = from; i < cells.Count; i++)
            {
                cells[i].Row = named;
                cells[i].Region = region;
            }
        }

        /// <summary>The game's own word for something, or nothing where the corpus never wrote one - a
        /// key that came back as itself is parked text, not a name to say over a row.</summary>
        private static string GameWord(string key)
        {
            try
            {
                string said = AgeText.Clean(Gui.Localize(key));
                return string.IsNullOrEmpty(said) || said[0] == '%' ? null : said;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What a row is called - which is a question about the whole row and not about its
        /// first cell. The rows fall out of the RECTANGLES, so nothing here can promise that one
        /// contributor's cells are a row of their own; a line that has picked up cells from two of
        /// them is a line no single word describes, and it is declared with no level rather than under
        /// the name of whichever cell happened to be leftmost.</summary>
        private static string RowName(List<Cell> row)
        {
            string named = row.Count == 0 ? null : row[0].Row;
            for (int i = 1; i < row.Count; i++)
            {
                if (row[i].Row != named)
                {
                    return null;
                }
            }

            return named;
        }

        /// <summary>Which region a row is, on the same terms as <see cref="RowName"/>: the panel every
        /// cell of it came from, or nothing where the rectangles put two panels on one line.</summary>
        private static string RowRegion(List<Cell> row)
        {
            string region = row.Count == 0 ? null : row[0].Region;
            for (int i = 1; i < row.Count; i++)
            {
                if (row[i].Region != region)
                {
                    return null;
                }
            }

            return region;
        }

        /// <summary>
        /// The two clusters the game draws across the top of every page that is drawn over a view
        /// level, in the order it draws them: what the empire is worth in the left corner, then what
        /// the player is looking at in the centre.
        ///
        /// One call rather than two, because the top of the screen is the same on every such page and
        /// the next page to be modelled should not be able to inherit half of it. A page that has to
        /// put something of its own between them can still call the two halves separately.
        /// </summary>
        public void Top(GraphBuilder builder)
        {
            Empire(builder);
            ViewTitle(builder);
        }

        /// <summary>
        /// What the player is looking at, as the game writes it across the top centre: the lens that
        /// would X-ray the view and, where the page has one, the zoom ladder - one control per row.
        ///
        /// The words the game draws over the cluster - the view's name - are declared NOWHERE (owner
        /// ruling, 2026-08-18, superseding the level-label reading): the screen already says which page
        /// the player is on when it arrives, so a level repeating it prefixes the first thing in this
        /// stop with a word the player has just heard. The control those words sit on is not declared
        /// either - a Close button carrying the same caption on every page above the galaxy
        /// (<c>TopTitlePanel.Setup</c>) - because Escape already leaves the page and a button called
        /// "Technology Screen" that closes the technology screen reads as the way IN.
        ///
        /// What the cluster IS called on the map is "View Controls", the mod's own words (owner ruling
        /// 2026-08-19): the view's name says which page the player is on, which the screen has already
        /// said on arrival, while what this stop holds is the two controls over how that page is being
        /// looked at.
        ///
        /// The lens is named by the game, and what it is named changes as the camera climbs: the map's
        /// zoom step picks a layer descriptor and the descriptor picks the lens, so the same button
        /// reads "Diplomacy scan" from far out and "System scan" up close. The label is read live for
        /// exactly that reason, and the game hides the whole group on the pages that have no lens.
        ///
        /// The zoom comes FIRST where a page has one (owner ruling): it is what the player reaches for,
        /// and the lens is the rarer errand. A page passes its own ladder in rather than appending it
        /// afterwards, because the order is this cluster's to decide.
        ///
        /// A page with neither a lens nor a ladder declares no stop at all - an empty stop is a Tab
        /// press that lands nowhere - which is why this answers whether it declared one.
        /// </summary>
        public bool ViewTitle(GraphBuilder builder, ZoomLadder zoom = null)
        {
            GameOverlayWindow window = OverlayWindow();
            TopTitlePanel panel = window == null ? null : window.TopTitlePanel;
            // Flow control: a stop, a lens and a zoom ladder are opened under it, and the answer tells
            // the caller whether a stop was declared at all.
            if (panel == null || !panel.Shown || !AgeWidgets.Visible(panel.AgeTransform))
            {
                return false;
            }

            bool ladder = zoom != null && ZoomLadder.Rungs;
            if (!ladder && !ScanDrawn(panel))
            {
                return false;
            }

            builder.BeginStop(ViewTitleStop);
            if (!ladder)
            {
                AddScanToggle(builder, panel);
                return true;
            }

            // Named only where the ladder is - the galaxy map, the one page whose cluster holds more
            // than the lens button. A page showing the lens alone is one control, and a level over a
            // single control is a word said before every reading of it (owner ruling 2026-08-19).
            builder.PushContext(ModStrings.Get(ModStrings.HudViewControlsPanel));
            zoom.Build(builder, "hud:view-title/zoom");
            AddScanToggle(builder, panel);
            builder.PopContext();
            return true;
        }

        /// <summary>The lens toggle. The tooltip explaining it is hung on the GROUP around the label
        /// and the icon rather than on the button, which is what the game shows a tooltip for and so is
        /// what the pointer is aimed at.</summary>
        private static void AddScanToggle(GraphBuilder builder, TopTitlePanel panel)
        {
            if (!ScanDrawn(panel))
            {
                return;
            }

            AgeTransform group = panel.ScanGroup;
            AgeControlButton button = panel.ScanButton;
            AgeControlButton it = button;
            AgeTooltip tooltip = AgeWidgets.Raw(group);
            NodeVtable vtable = GraphNodes.Button(
                () => AgeText.Label(panel.ScanLabel),
                () => AgeWidgets.Press(it),
                () => AgeWidgets.Operable(group),
                tooltip
            );
            AgeWidgets.Point(vtable, it, tooltip, group);
            builder.AddItem(Nodes.Drawn(ControlId.For(button, "hud:view-title/scan"), vtable, button));
        }

        /// <summary>Whether the page has a lens at all - asked before the stop is begun as well as
        /// while filling it, because a stop with nothing in it is a Tab press that lands nowhere.
        /// </summary>
        private static bool ScanDrawn(TopTitlePanel panel)
        {
            return panel.ScanGroup != null
                && panel.ScanButton != null
                && AgeWidgets.Visible(panel.ScanGroup);
        }

        /// <summary>A control on its way into the graph, still carrying the widget it was read from:
        /// the rows are worked out from the whole cluster at once, which cannot be done while
        /// declaring it row by row.</summary>
        private sealed class Cell
        {
            public AgeTransform Widget;
            public ControlId Id;
            public NodeVtable Vtable;

            /// <summary>What the row this cell lands in is called, or null where nothing has named it
            /// (<see cref="RowName"/>).</summary>
            public string Row;

            /// <summary>Which region of the stop the row this cell lands in IS - the panel it was read
            /// off, in a word that is not the player's, because a region key has to be the same string
            /// on every rebuild and the name is a localized one. Null where nothing has named it
            /// (<see cref="RowRegion"/>).</summary>
            public string Region;
        }

        private static readonly Func<Cell, AgeTransform> CellWidget = cell => cell.Widget;

        /// <summary>The strip of icons along the top, each of which opens one of the game's screens.
        /// The game gives them no captions at all - the name of the screen and the key that opens it
        /// are in the tooltip, which is where both are read from.
        ///
        /// A toggle can also carry a BADGE with a sentence of its own - the senate icon's dot, "The
        /// leading political party in the Senate" - and that sentence exists nowhere else on the
        /// screen. Every tooltip inside the toggle is therefore declared, in drawn order, with the
        /// button's OWN speaking and the badges reviewable: which of a row's tooltips speaks is the
        /// screen's call where the row is a control plus a badge, and what the button OPENS is the
        /// thing a player standing on it asked for (measured 2026-08-23: the badge's sentence had no
        /// surface at all).</summary>
        private static void AddScreenToggles(List<Cell> cells, ControlBanner banner)
        {
            if (banner == null || banner.TogglesTable == null)
            {
                return;
            }

            try
            {
                foreach (
                    ControlBannerToggle toggle in banner.TogglesTable.GetChildren<ControlBannerToggle>(
                        false
                    )
                )
                {
                    AgeTransform widget = toggle.AgeTransform;
                    // Banding input: AddCell appends straight to the list, so the gate never sees these
                    // until the strip has already been worked into rows by their rectangles.
                    if (toggle.Screen == null || !AgeWidgets.Visible(widget))
                    {
                        continue;
                    }

                    ControlBanner strip = banner;
                    GuiScreen screen = toggle.Screen;
                    AgeTooltip tooltip = AgeWidgets.Raw(widget);
                    NodeVtable vtable = GraphNodes.Button(
                        () => ScreenTitle(screen),
                        () => strip.OnControlBannerToggle(screen),
                        () => AgeWidgets.Enabled(widget),
                        tooltip
                    );
                    List<AgeTooltip> inside = new List<AgeTooltip>(2);
                    AgeWidgets.Tooltips(widget, inside);
                    if (inside.Count > 1)
                    {
                        vtable.Sections = ToggleSections(inside);
                    }

                    AgeWidgets.PointAt(vtable, widget);
                    cells.Add(
                        new Cell
                        {
                            Widget = widget,
                            Id = ControlId.For(
                                toggle,
                                "hud:empire/screen/" + screen.GetType().Name
                            ),
                            Vtable = vtable,
                        }
                    );
                }
            }
            catch (Exception e)
            {
                Log.Warn("hud: reading the screen icons threw: " + e);
            }
        }

        /// <summary>One icon.s explanations as sections: the FIRST is the icon.s own and speaks; every
        /// later one is a badge inside it and is reviewable. The order is the drawn one the resolver
        /// answers in - the control.s own tooltip, then the badges inside it - so the button says what it
        /// opens and the badge.s sentence is a buffer line away rather than nowhere at all. Which of
        /// several tooltips is the icon.s OWN is a fact about the icon; how loudly that one reads is the
        /// tooltip.s own kind to answer.
        ///
        /// The one place in the mod where a badge stays a REVIEWED section rather than becoming an entry
        /// of its own, and it is a shape constraint rather than a judgement: the strip's icons are laid
        /// into a graph ROW, and a node inside an open row cannot be a group
        /// (<c>GraphBuilder.BeginGroup</c> refuses one). The badges are content-backed sentences, so
        /// dropping them would lose real words rather than an empty promise - which is what the ruling
        /// asks for everywhere it CAN be applied. Reported 2026-08-28; also unverified live, because no
        /// screen icon in this fixture carries a second tooltip at all (measured: 0 of 8).</summary>
        private static IList<NodeSection> ToggleSections(List<AgeTooltip> tooltips)
        {
            List<NodeSection> sections = new List<NodeSection>(tooltips.Count);
            for (int i = 0; i < tooltips.Count; i++)
            {
                if (i > 0)
                {
                    NodeSection badge = GraphNodes.ReviewedTooltipSection(tooltips[i]);
                    if (badge != null)
                    {
                        sections.Add(badge);
                    }

                    continue;
                }

                IList<NodeSection> tip = GraphNodes.HintSections(tooltips[i]);
                for (int j = 0; tip != null && j < tip.Count; j++)
                {
                    sections.Add(tip[j]);
                }
            }

            return sections.Count == 0 ? null : sections;
        }

        /// <summary>What the game calls the screen an icon opens - the same title it writes as the
        /// first line of the icon's own tooltip.</summary>
        private static string ScreenTitle(GuiScreen screen)
        {
            try
            {
                return AgeText.Clean(Gui.GetLocalizedTitle(screen.GetType().Name));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The running totals the banner across the top carries.</summary>
        private static void AddTotals(List<Cell> cells, EmpireBanner banner, Empire empire)
        {
            if (banner == null)
            {
                return;
            }

            Empire it = empire;
            AddTotal(
                cells,
                banner.MoneyLabel,
                "dust",
                SimulationProperties.Empire.NetEmpireMoney,
                () => Value(it, SimulationProperties.Empire.BankAccount),
                () => Value(it, SimulationProperties.Empire.NetEmpireMoney)
            );
            AddTotal(
                cells,
                banner.ManpowerLabel,
                "manpower",
                SimulationProperties.Empire.EmpireManpower,
                () => Value(it, SimulationProperties.Empire.EmpireManpowerStock),
                () =>
                    Value(it, SimulationProperties.Empire.EmpireManpower)
                    - Value(it, SimulationProperties.Empire.EmpireManpowerUpkeep)
            );
            AddTotal(
                cells,
                banner.EmpirePointLabel,
                "influence",
                SimulationProperties.Empire.NetEmpireEmpirePoint,
                () => Value(it, SimulationProperties.Empire.EmpireEmpirePointStock),
                () => Value(it, SimulationProperties.Empire.NetEmpireEmpirePoint)
            );
        }

        /// <summary>One of the banner's running totals: what it is called, what there is of it, and
        /// what the next turn will add or take away.</summary>
        private static void AddTotal(
            List<Cell> cells,
            AgePrimitiveLabel label,
            string key,
            StaticString property,
            Func<float> stock,
            Func<float> net
        )
        {
            // Banding input: AddCell appends without the gate's question, and the banner's readouts are
            // worked into one row by where they are drawn.
            if (label == null || !AgeWidgets.Visible(label.AgeTransform))
            {
                return;
            }

            AgeTransform area = Area(label);
            AgeTooltip tooltip = AgeWidgets.Raw(area);
            NodeVtable vtable = GraphNodes.Readout(
                () => Gui.GetLocalizedTitle(property),
                () => StockAndNet(stock(), net(), 0),
                null,
                tooltip
            );
            AgeWidgets.PointAt(vtable, area);
            cells.Add(
                new Cell
                {
                    Widget = area,
                    Id = ControlId.For(label, "hud:empire/" + key),
                    Vtable = vtable,
                }
            );
        }

        /// <summary>What is being researched and how long is left, or the game's own words for having
        /// queued nothing. Opening it is the banner's own click, which is what knows whether the
        /// technology screen can be reached at all.</summary>
        private static void AddResearch(List<Cell> cells, EmpireBanner banner, Empire empire)
        {
            AgeControlButton button = banner == null ? null : banner.ResearchButton;
            // The tutorial hides the whole research area until it has taught the rest, and the game
            // hides it outright for an empire that cannot research.
            if (
                button == null
                || !AgeWidgets.Visible(banner.ResearchGroup)
                || !AgeWidgets.Visible(AgeWidgets.Transform(button))
            )
            {
                return;
            }

            AgeControlButton it = button;
            Empire owner = empire;
            // The banner hangs the technology's tooltip on the line of text, not on the button - which
            // is stretched across the whole banner - so that is both what the game shows a tooltip for
            // and what it should be drawn under.
            AgeTransform line =
                banner.ResearchLabel == null
                    ? AgeWidgets.Transform(button)
                    : banner.ResearchLabel.AgeTransform;
            AgeTooltip tooltip = AgeWidgets.Raw(line);
            NodeVtable vtable = GraphNodes.Button(
                () => ModStrings.Get(ModStrings.GalaxyResearch),
                () => AgeWidgets.Press(it),
                () => AgeWidgets.Enabled(AgeWidgets.Transform(it)),
                tooltip
            );
            vtable.Announcements.Add(GraphNodes.ValuePart(() => ResearchText(owner)));
            AgeWidgets.Point(vtable, it, tooltip, line);
            cells.Add(
                new Cell
                {
                    Widget = AgeWidgets.Transform(it),
                    Id = ControlId.For(it, "hud:empire/research"),
                    Vtable = vtable,
                }
            );
            AddResearchBuyout(cells, banner);
        }

        /// <summary>
        /// Buying the technology being researched outright, from the button the banner draws at the end
        /// of the research line.
        ///
        /// Same rule the construction queue's buy-outs follow (ES2 facts): the game HIDES this button
        /// for an empire that cannot buy technology at all and otherwise leaves it drawn and switched
        /// off with the reason written into its own tooltip (<c>EmpireBanner.RefreshBuyout</c>
        /// :470-515), so DRAWN is what declares it and <c>Enable</c> is what offers it. Which currency
        /// this could be bought with, and why the answer is no today, is exactly what the player asks
        /// the banner.
        /// </summary>
        private static void AddResearchBuyout(List<Cell> cells, EmpireBanner banner)
        {
            BuyoutButton buyout = banner.BuyoutButton;
            if (buyout == null || !AgeWidgets.Visible(buyout.AgeTransform))
            {
                return;
            }

            BuyoutButton it = buyout;
            AgeTransform at = buyout.AgeTransform;
            AgeTooltip tooltip = AgeWidgets.Raw(at);
            NodeVtable vtable = GraphNodes.Button(
                () =>
                    ModStrings.Format(
                        ModStrings.SystemBuyOut,
                        AgeText.Clean(Gui.GetLocalizedTitle("Empire" + it.Resource))
                    ),
                () => AgeWidgets.Press(at),
                () => AgeWidgets.Offered(at),
                tooltip
            );
            // The price the button writes on itself, and only while the button is on offer: a refused
            // one carries a marker there rather than a number ("x", "-") and its tooltip already names
            // the amount that cannot be afforded.
            vtable.Announcements.Add(GraphNodes.ValuePart(() => BuyoutCost(it, at)));
            GraphNodes.AddRefusal(vtable, tooltip, () => AgeWidgets.Offered(at));
            AgeWidgets.PointAt(vtable, at);
            cells.Add(
                new Cell
                {
                    Widget = at,
                    Id = ControlId.For(buyout, "hud:empire/research-buyout"),
                    Vtable = vtable,
                }
            );
        }

        private static string BuyoutCost(BuyoutButton buyout, AgeTransform widget)
        {
            try
            {
                return AgeWidgets.Offered(widget) && buyout.CostLabel != null
                    ? AgeText.Label(buyout.CostLabel)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string ResearchText(Empire empire)
        {
            try
            {
                DepartmentOfScience science = empire.GetAgency<DepartmentOfScience>();
                Construction construction = science.ResearchQueue.Peek();
                if (construction == null)
                {
                    return AgeText.Clean("%NoResearchQueued");
                }

                TechnologyDefinition definition =
                    construction.ConstructibleElement as TechnologyDefinition;
                GuiTechnology2 technology = Gui.GuiWrapperProviderService.GetGuiTechnology2(
                    definition.Name
                );
                int turns = science.GetTechnologyRemainingTurn(definition);
                string title = technology == null ? null : AgeText.Clean(technology.Title);
                if (turns < 0 || turns == int.MaxValue)
                {
                    return title;
                }

                return new MessageBuilder()
                    .ListItem(title)
                    .ListItem(ModStrings.Format(ModStrings.GalaxyTurnsRemaining, turns))
                    .Build();
            }
            catch (Exception e)
            {
                Log.Warn("hud: reading the research line threw: " + e);
                return null;
            }
        }

        /// <summary>The strategic and luxury resources the empire holds, in the order the strip beside
        /// the banner shows them. A resource sitting at zero stays in the list - the strip dims it
        /// rather than dropping it, and "we have none of that" is the answer to the question.</summary>
        private static void AddStockpiles(List<Cell> cells, ResourcesPanel panel)
        {
            if (panel == null || panel.ResourceItemsTable == null)
            {
                return;
            }

            try
            {
                foreach (ResourceItem item in panel.ResourceItemsTable.GetChildren<ResourceItem>(false))
                {
                    GuiLocatedResource resource = item.GuiLocatedResource;
                    // Banding input: same door as the rest of the banner - AddCell takes the item
                    // without asking the gate, and its rectangle is what puts it on the strip's row.
                    if (resource == null || !AgeWidgets.Visible(item.AgeTransform))
                    {
                        continue;
                    }

                    GuiLocatedResource it = resource;
                    // Small holdings of a strategic or a luxury are counted in tenths, which is how
                    // the strip itself writes them.
                    NodeVtable vtable = GraphNodes.Readout(
                        () => AgeText.Clean(it.Title),
                        () =>
                            StockAndNet(
                                it.GetStockValueFromCache(),
                                it.GetNetValueFromCache(),
                                it.GetStockValueFromCache() < 10f ? 1 : 0
                            ),
                        null,
                        item.Tooltip
                    );
                    AgeWidgets.Point(vtable, item.Button, item.Tooltip, item.AgeTransform);
                    cells.Add(
                        new Cell
                        {
                            Widget = item.AgeTransform,
                            Id = ControlId.For(item, "hud:empire/resource/" + resource.Name),
                            Vtable = vtable,
                        }
                    );
                }
            }
            catch (Exception e)
            {
                Log.Warn("hud: reading the resource strip threw: " + e);
            }
        }

        // ---- the faction readouts under the banners ----
        //
        // The game stacks up to seven more panels straight under the three banners, in the same column
        // and at the same width (measured: the banners fill the top 106 pixels and the stack begins
        // exactly there), and shows each one only to an empire that has the thing it counts - a
        // Vodyani's essence and Arks, a gene hunter's assimilation, a Riftborn's time bubbles, a golden
        // age's countdown, a pirate mark anyone may buy, a Hissho's keii, a Templar's relics. They are
        // part of the same cluster as the banners, so they are cells of the same stop and their rows
        // fall out of the rectangles like every other row up there; nothing here decides which row
        // anything is on.
        //
        // Which of them is DRAWN is the game's own answer, asked per frame
        // (<c>GameOverlayWindow.Update*Visibility</c>), and nothing here re-derives the affinities and
        // unlocks behind it.
        //
        // Several of these carry clicks that do nothing outside the game's own debug mode - the essence
        // and keii totals post a resource transfer only while it is in god mode, and the time bubble
        // panel's own click is that and nothing else - so those are readouts, exactly as the dust and
        // manpower totals beside them are. Only a click the game would really act on is a button.

        // Each of the seven is a ROW of its own and says what it is on the way in, the same way the four
        // banner rows above them do (owner ruling 2026-08-19, which supersedes the "no level at all"
        // reading recorded on <see cref="Empire"/>). Five are named by the game's own title for the
        // thing the panel counts and two by the mod's, because the corpus has no bare title for them -
        // see the ModStrings comment on <see cref="ModStrings.HudSingularitiesPanel"/>.
        //
        // The word rides on the CELLS, not on the call order: these rows fall out of the rectangles like
        // every other row of this stop, so a panel the game happened to draw level with another gets no
        // level at all rather than the wrong one (<see cref="RowName"/>).
        private static void AddFactionPanels(List<Cell> cells, GameOverlayWindow window)
        {
            try
            {
                int from = cells.Count;
                AddLifeforce(cells, window.LifeforceStatusPanel);
                Name(cells, from, GameWord("%NetEmpireLifeforceTitle"), "lifeforce");
                from = cells.Count;
                AddGenes(cells, window.GeneManagementShortcutPanel);
                Name(cells, from, GameWord("%AssimilationShortcutTitle"), "genes");
                from = cells.Count;
                AddTimeBubbles(cells, window.TimeBubbleStockPanel);
                Name(cells, from, ModStrings.Get(ModStrings.HudSingularitiesPanel), "singularities");
                from = cells.Count;
                AddGoldenAge(cells, window.GoldenAgePanel);
                Name(cells, from, GameWord("%GoldenAgeTitle"), "golden-age");
                from = cells.Count;
                AddPirateMark(cells, window.PirateMarkPanel);
                Name(cells, from, ModStrings.Get(ModStrings.HudPirateMarkPanel), "pirate-mark");
                from = cells.Count;
                AddHonor(cells, window.HonorManagementPanel);
                Name(cells, from, GameWord("%HonorTitle"), "honor");
                from = cells.Count;
                AddRelics(cells, window.RelicManagementPanel);
                Name(cells, from, GameWord("%RelicsTitle"), "relics");
            }
            catch (Exception e)
            {
                Log.Warn("hud: reading the faction panels threw: " + e);
            }
        }

        /// <summary>What a Vodyani empire lives on: the essence it holds against what it can hold and
        /// what the turn will bring, and how many Arks are carrying it. Read off the panel's own labels
        /// rather than out of the model, because what it writes is a stock, a ceiling and a net in one
        /// line and the model would have to be re-assembled into it.</summary>
        private static void AddLifeforce(List<Cell> cells, LifeforceStatusPanel panel)
        {
            if (!Drawn(panel))
            {
                return;
            }

            AddValue(
                cells,
                "lifeforce",
                Tip(panel.LifeforceTooltip),
                SimulationProperties.Empire.NetEmpireLifeforce,
                panel.LifeforceValue
            );
            AddValue(cells, "motherships", Area(panel.MothershipValue), null, panel.MothershipValue);
        }

        /// <summary>How close a gene hunter is to absorbing another people - the line the panel writes
        /// while it is counting, or the icon it swaps in when it is ready - and the button beside it
        /// that opens the population screen. The game wires that button in its prefab and exposes no
        /// field for it, so it is found by being the panel's button (<see cref="OnlyButton"/>).</summary>
        private static void AddGenes(List<Cell> cells, GeneManagementShortcutPanel panel)
        {
            if (!Drawn(panel))
            {
                return;
            }

            AgePrimitiveLabel status = panel.AssimilationStatusLabel;
            AgeTransform line = status == null ? null : status.AgeTransform;
            // Content: which of the two the panel is drawing - the status sentence or the ready icon,
            // never both - and banding input either way, because AddCell does not ask the gate.
            if (AgeWidgets.Visible(line))
            {
                AgePrimitiveLabel it = status;
                AddCell(
                    cells,
                    line,
                    "hud:empire/assimilation",
                    GraphNodes.Readout(() => AgeText.Label(it), () => null, null, AgeWidgets.Raw(line))
                );
            }
            else
            {
                AgeTransform ready =
                    panel.ReadyIcon == null ? null : panel.ReadyIcon.AgeTransform;
                // The other half of that choice, and the same banding reason.
                if (AgeWidgets.Visible(ready))
                {
                    AgeTooltip tooltip = AgeWidgets.Raw(ready);
                    AddCell(
                        cells,
                        ready,
                        "hud:empire/assimilation",
                        GraphNodes.Readout(
                            CardActions.NameFromTooltip(tooltip),
                            () => null,
                            null,
                            tooltip
                        )
                    );
                }
            }

            AddDrawnButton(cells, OnlyButton(panel.AgeTransform), "population");
        }

        /// <summary>The bubbles a Riftborn empire is holding, one node each, in the order the strip
        /// lays them out - an empty slot included, because the strip draws one and "there is room for
        /// another" is the answer to what the strip is being asked. Pressing one puts the map into the
        /// mode that plants it, or takes the camera to the one already planted; the small button on it
        /// throws it away behind the game's own confirmation.</summary>
        private static void AddTimeBubbles(List<Cell> cells, TimeBubbleStockPanel panel)
        {
            if (!Drawn(panel))
            {
                return;
            }

            AgeTransform table = panel.TimeBubbleTable;
            IList<AgeTransform> items = table == null ? null : table.Children;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                AgeTransform item = items[i];
                // Banding input: AddCell takes each bubble without asking the gate, and the bubbles are
                // worked into a row by where they are drawn.
                if (item == null || !AgeWidgets.Visible(item))
                {
                    continue;
                }

                AgeTransform it = item;
                AddCell(
                    cells,
                    it,
                    ControlId.Structural("hud:empire/time-bubble/" + i),
                    GraphNodes.Button(
                        ThingName(it),
                        () => AgeWidgets.Press(it),
                        () => AgeWidgets.Operable(it),
                        AgeWidgets.Raw(it)
                    )
                );

                TimeBubbleItem bubble = item.GetComponent<TimeBubbleItem>();
                AgeTransform destroy =
                    bubble == null ? null : AgeWidgets.Transform(bubble.DestroyBubbleButton);
                AddDrawnButton(
                    cells,
                    destroy,
                    ControlId.Structural("hud:empire/time-bubble/" + i + "/destroy")
                );
            }
        }

        /// <summary>How long a golden age has left, or how long the ship that starts one is locked in a
        /// garrison, plus the button that takes the camera to that ship. Each line is read as the words
        /// its own group draws, caption and figure together, because the game spreads them over two
        /// labels and only one of them is a field.</summary>
        private static void AddGoldenAge(List<Cell> cells, GoldenAgePanel panel)
        {
            if (!Drawn(panel))
            {
                return;
            }

            AddDrawnLine(
                cells,
                panel.NextGoldenAgeDurationGroup,
                "golden-age",
                Tip(panel.GoldenAgeGaugeTooltip)
            );
            AddDrawnLine(cells, panel.LockDurationGroup, "golden-age-lock", null);
            AddDrawnButton(cells, panel.ColonizerLocationButton, "golden-age-locate");
        }

        /// <summary>The pirate mark: what it is aimed at and how long it has left where one is running,
        /// an offer to aim one where it is not. The item itself is the button that starts the aiming -
        /// the game switches the map into a targeting cursor - and it REFUSES while a mark is already
        /// out, with its own tooltip naming the system that is marked.</summary>
        private static void AddPirateMark(List<Cell> cells, PirateMarkInventoryPanel panel)
        {
            if (!Drawn(panel))
            {
                return;
            }

            AgeTransform item = panel.PirateMarkItem;
            // Banding input: AddCell appends the mark without the gate's question.
            if (AgeWidgets.Visible(item))
            {
                AgeTransform it = item;
                AddCell(
                    cells,
                    it,
                    "hud:empire/pirate-mark",
                    GraphNodes.Button(
                        () => AgeWidgets.TextOf(it),
                        () => AgeWidgets.Press(it),
                        () => AgeWidgets.Operable(it),
                        AgeWidgets.Raw(it)
                    )
                );
            }

            AddDrawnButton(cells, panel.ShowLocationButton, "pirate-mark-locate");
        }

        /// <summary>A Hissho empire's keii, and the actions its gauge unlocks - one node per threshold
        /// the panel draws a button on, named by the wrapper the game hangs on that button's own
        /// tooltip, with the turns a running one has left beside it. Pressing one starts it (the map
        /// takes a cursor for choosing where) or calls a running one off, which is the button's own
        /// click either way.</summary>
        private static void AddHonor(List<Cell> cells, HonorManagementPanel panel)
        {
            if (!Drawn(panel))
            {
                return;
            }

            AddValue(
                cells,
                "honor",
                Tip(panel.HonorTooltip),
                SimulationProperties.Empire.NetEmpireHonor,
                ValueLabel(panel.HonorValueField)
            );

            AgeTransform table = panel.HonorGaugeSegmentsTable;
            IList<AgeTransform> segments = table == null ? null : table.Children;
            for (int i = 0; segments != null && i < segments.Count; i++)
            {
                HonorGaugeSegment segment =
                    segments[i] == null ? null : segments[i].GetComponent<HonorGaugeSegment>();
                AgeControlButton button = segment == null ? null : segment.ActionButton;
                AgeTransform action = AgeWidgets.Transform(button);
                // Banding input: AddCell takes each segment's action without asking the gate, and the
                // gauge's segments are banded by where they are drawn along it.
                if (!AgeWidgets.Visible(action))
                {
                    continue;
                }

                AgeTooltip tooltip = segment.ActionTooltip;
                AgeControlButton it = button;
                AgePrimitiveLabel turns = segment.RemainingTurnsLabel;
                NodeVtable vtable = GraphNodes.Button(
                    WrapperName(tooltip),
                    () => AgeWidgets.Press(it),
                    () => AgeWidgets.Operable(action),
                    tooltip
                );
                vtable.Announcements.Add(GraphNodes.ValuePart(() => Turns(turns)));
                AgeWidgets.Point(vtable, it, tooltip, action);
                cells.Add(
                    new Cell
                    {
                        Widget = action,
                        Id = ControlId.Structural("hud:empire/honor-action/" + i),
                        Vtable = vtable,
                    }
                );

                // The segment's own gauge carries a SECOND dossier - the keii property the track is
                // measuring (<c>HonorGaugeSegment.Refresh</c> :67-69) - and only one tooltip can be
                // drawn at a time, so it is a node beside the action rather than a promise folded into
                // it.
                List<TooltipChildren.Dossier> gauge = new List<TooltipChildren.Dossier>(1);
                TooltipChildren.Add(gauge, segment.GaugeGroup);
                for (int g = 0; g < gauge.Count; g++)
                {
                    cells.Add(
                        new Cell
                        {
                            Widget = segment.GaugeGroup,
                            Id = ControlId.Structural("hud:empire/honor-gauge/" + i),
                            Vtable = TooltipChildren.Node(gauge[g]),
                        }
                    );
                }
            }
        }

        /// <summary>What a Templar empire has collected and where it has put it. The panel keeps a
        /// group at zero rather than dropping it - it dims it instead - so all five are read, and "we
        /// have none of those" is the answer to the question.</summary>
        private static void AddRelics(List<Cell> cells, RelicManagementPanel panel)
        {
            if (!Drawn(panel))
            {
                return;
            }

            AddValue(
                cells,
                "relics",
                panel.NetRelicsGroup,
                SimulationProperties.Empire.NetEmpireRelics,
                panel.NetRelicsLabel
            );
            AddValue(
                cells,
                "relics-research",
                panel.ResearchRelicsGroup,
                SimulationProperties.Empire.ResultingResearchRelics,
                panel.ResearchRelicsLabel
            );
            AddValue(
                cells,
                "relics-hero",
                panel.HeroRelicsGroup,
                SimulationProperties.Empire.HeroRelics,
                panel.HeroRelicsLabel
            );
            AddValue(
                cells,
                "relics-empire",
                panel.FIDIRelicsGroup,
                SimulationProperties.Empire.FIDIRelics,
                panel.FIDIRelicsLabel
            );
            AddValue(
                cells,
                "relics-temple",
                panel.TempleRelicsGroup,
                SimulationProperties.Empire.TempleRelics,
                panel.TempleRelicsLabel
            );
        }

        /// <summary>Whether the game is showing one of these panels at all - it keeps every one of them
        /// alive and hides the ones this empire has no use for.</summary>
        private static bool Drawn(GuiPanel panel)
        {
            try
            {
                // Flow control: each caller reads a whole panel's worth of cells under this answer.
                return panel != null && panel.Shown && AgeWidgets.Visible(panel.AgeTransform);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>One of these panels' readouts: what the game calls the thing, and the figure the
        /// panel is drawing for it.</summary>
        private static void AddValue(
            List<Cell> cells,
            string key,
            AgeTransform area,
            string property,
            AgePrimitiveLabel value
        )
        {
            // Banding input: AddCell appends without the gate's question, and the banner passes every
            // one of its value groups through here whether or not this empire has that currency.
            if (!AgeWidgets.Visible(area))
            {
                return;
            }

            AgeTooltip tooltip = AgeWidgets.Raw(area);
            AgePrimitiveLabel it = value;
            AddCell(
                cells,
                area,
                "hud:empire/" + key,
                GraphNodes.Readout(Naming(property, tooltip), () => AgeText.Label(it), null, tooltip)
            );
        }

        /// <summary>A line the game writes as a caption and a figure in separate labels inside one
        /// group, read as the one phrase it looks like.</summary>
        private static void AddDrawnLine(
            List<Cell> cells,
            AgeTransform group,
            string key,
            AgeTransform under
        )
        {
            // Banding input, as at AddValue: AddCell appends without the gate's question.
            if (!AgeWidgets.Visible(group))
            {
                return;
            }

            AgeTransform it = group;
            // Content: which widget's tooltip the line is read with - the panel hangs it under the group
            // on some pages and on the group itself on others.
            AgeTransform area = AgeWidgets.Visible(under) ? under : group;
            AgeTooltip tooltip = AgeWidgets.Raw(area);
            NodeVtable vtable = GraphNodes.Readout(
                () => AgeWidgets.TextOf(it),
                () => null,
                null,
                tooltip
            );
            AgeWidgets.PointAt(vtable, area);
            cells.Add(
                new Cell
                {
                    Widget = it,
                    Id = ControlId.For(it, "hud:empire/" + key),
                    Vtable = vtable,
                }
            );
        }

        /// <summary>A button the game draws as a bare icon and names only in the sentence its tooltip
        /// opens with - the two "show me where that is" buttons, the bubble's own destroy.</summary>
        private static void AddDrawnButton(List<Cell> cells, AgeTransform widget, string key)
        {
            AddDrawnButton(cells, widget, ControlId.For(widget, "hud:empire/" + key));
        }

        private static void AddDrawnButton(List<Cell> cells, AgeTransform widget, ControlId id)
        {
            // Banding input: AddCell appends without the gate's question, and these bare icons band
            // with the line they sit beside.
            if (!AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeTransform it = widget;
            AgeTooltip tooltip = AgeWidgets.Raw(it);
            AddCell(
                cells,
                it,
                id,
                GraphNodes.Button(
                    CardActions.NameFromTooltip(tooltip),
                    () => AgeWidgets.Press(it),
                    () => AgeWidgets.Operable(it),
                    tooltip
                )
            );
        }

        private static void AddCell(
            List<Cell> cells,
            AgeTransform widget,
            string key,
            NodeVtable vtable
        )
        {
            AddCell(cells, widget, ControlId.For(widget, key), vtable);
        }

        private static void AddCell(
            List<Cell> cells,
            AgeTransform widget,
            ControlId id,
            NodeVtable vtable
        )
        {
            AgeWidgets.PointAt(vtable, widget);
            cells.Add(new Cell { Widget = widget, Id = id, Vtable = vtable });
        }

        /// <summary>What to call a readout: the title the game keeps for the simulation property behind
        /// it, and where it keeps none, the sentence its own tooltip opens with. Half of these are drawn
        /// as an icon and a figure with the words nowhere but in the tooltip.</summary>
        private static Func<string> Naming(string property, AgeTooltip tooltip)
        {
            string it = property;
            AgeTooltip tip = tooltip;
            return () =>
            {
                string title = PropertyTitle(it);
                return string.IsNullOrEmpty(title) ? CardActions.FirstLine(tip) : title;
            };
        }

        /// <summary>
        /// What the game calls a simulation property, or nothing where it has no name to give.
        ///
        /// Asked about a property it has no GUI element for, the game answers with a pink "(missing
        /// GuiElement)" placeholder written for its own designers; asked about one whose title is not in
        /// the localization, it answers with the key. Neither is a name, and both are on properties
        /// these panels really use (measured: MothershipCount, TempleRelics, FIDIRelics).
        /// </summary>
        private static string PropertyTitle(string property)
        {
            try
            {
                if (string.IsNullOrEmpty(property) || Gui.GetGuiElement(property) == null)
                {
                    return null;
                }

                string title = AgeText.Clean(Gui.GetLocalizedTitle(property));
                return string.IsNullOrEmpty(title) || title[0] == '%' ? null : title;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What one of these wordless items is: the words it draws, the name off the wrapper
        /// the game hangs on its tooltip, and failing both the sentence that tooltip opens with.
        /// </summary>
        private static Func<string> ThingName(AgeTransform widget)
        {
            AgeTransform it = widget;
            Func<string> named = WrapperName(AgeWidgets.Raw(widget));
            return () =>
            {
                string drawn = AgeWidgets.TextOf(it);
                return string.IsNullOrEmpty(drawn) ? named() : drawn;
            };
        }

        /// <summary>The same for a control whose tooltip the game hangs somewhere other than on it - the
        /// keii gauge's action buttons, whose tooltip is a field of the segment. Only the tooltip is
        /// asked: the words drawn ON such a button are the turns its action has left, which is a value
        /// and not a name.</summary>
        private static Func<string> WrapperName(AgeTooltip tooltip)
        {
            AgeTooltip tip = tooltip;
            return () =>
            {
                string named = AgeWidgets.TooltipTitle(tip);
                return string.IsNullOrEmpty(named) ? CardActions.FirstLine(tip) : named;
            };
        }

        /// <summary>The one button a panel draws, found by BEING one: the game wires the click in its
        /// prefab and exposes no field for it, and matching on the widget's name would tie this to a
        /// string inside an asset.</summary>
        private static AgeTransform OnlyButton(AgeTransform panel)
        {
            try
            {
                IList<AgeTransform> children = panel == null ? null : panel.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    AgeTransform child = children[i];
                    // Content: WHICH child is the panel's one button. A panel keeps children it is not
                    // drawing, and the first of those with a button on it is not the one.
                    if (
                        child != null
                        && AgeWidgets.Visible(child)
                        && child.GetComponent<AgeControlButton>() != null
                    )
                    {
                        return child;
                    }
                }
            }
            catch (Exception) { }

            return null;
        }

        /// <summary>The label a value field is drawn on - the field is a behaviour that writes into an
        /// <c>AgePrimitiveLabel</c> on its own transform.</summary>
        private static AgePrimitiveLabel ValueLabel(GuiValueField field)
        {
            try
            {
                return field == null
                    ? null
                    : field.AgeTransform.GetComponent<AgePrimitiveLabel>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Turns(AgePrimitiveLabel label)
        {
            return AgeWidgets.DrawnLabel(label);
        }

        private static AgeTransform Tip(AgeTooltip tooltip)
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

        // ---- the pinned quest ----

        /// <summary>
        /// The quest the game is tracking, as the panel in the top right corner shows it: what it is
        /// called, how it is going, and what has to be done next.
        ///
        /// Three nodes at most, because the game draws three things to click: the panel itself, which
        /// opens the journal on this quest, and the two bare icons on it - the marker that takes the
        /// camera to wherever the quest is happening, and the pin that lets it go. They are drawn, so
        /// they are walked; neither is captioned, so each is named by the mod and explains itself with
        /// the game's own tooltip. An icon the game is not drawing is not a node: the marker is hidden
        /// outright for a quest with nowhere to point at, and a node that took the camera nowhere would
        /// teach the player that part of the panel is decoration.
        ///
        /// The stop is there only while the game draws the panel, which is two questions and not one:
        /// the game hides the whole window behind any full screen it opens, and it draws nothing at
        /// all while no quest is pinned. Neither state gets a placeholder - a stop saying "no quest"
        /// is a stop the player walks past to learn what a glance would have told them.
        /// </summary>
        public void Quest(GraphBuilder builder)
        {
            PinnedQuestPanel panel = QuestPanel();
            if (panel == null)
            {
                return;
            }

            PinnedQuestPanel it = panel;
            AgeTooltip hint = panel.QuestObjectiveTooltip;
            AgeControlButton open = AgeWidgets.Button(panel.AgeTransform);
            NodeVtable vtable = GraphNodes.Button(
                () => AgeText.FullLabel(it.QuestTitleLabel),
                () => AgeWidgets.Press(open),
                null,
                hint
            );
            vtable.Announcements.Add(GraphNodes.ValuePart(() => QuestProgress(it)));
            vtable.Announcements.Add(
                GraphNodes.ValuePart(() => AgeText.FullLabel(it.QuestObjectiveLabel))
            );
            // The panel's OWN sentence - what this corner of the screen is and what clicking it does -
            // is written on a tooltip the mod deliberately does not point at (below), so it exists
            // nowhere else; it reads first in the buffer, ahead of the objective's, and speaks nothing
            // (measured 2026-08-23: it was uncovered).
            vtable.Sections = GraphNodes.Sections(
                GraphNodes.TooltipDetails(AgeWidgets.Raw(panel.AgeTransform)),
                hint
            );
            // The panel is the thing that lights up, but the tooltip worth reading hangs off the
            // objective's own label inside it - pointing at the panel would leave the review buffer
            // waiting on a tooltip the game never drew.
            AgeWidgets.Point(
                vtable,
                open,
                hint,
                hint == null ? panel.AgeTransform : hint.AgeTransform
            );

            builder.BeginStop(QuestStop);
            // The panel carries no caption of its own (below), so the word is the mod's: without it
            // Tab lands on a quest title with nothing saying which corner of the screen it came from.
            builder.PushContext(ModStrings.Get(ModStrings.HudQuestPanel));
            // Synthetic: the row stands for the pinned QUEST, read off the panel's binding rather than
            // off any one widget it draws.
            builder.AddItem(Nodes.Synthetic(ControlId.For(panel.PinnedQuest, "hud:quest"), vtable));
            AddQuestButton(
                builder,
                panel.ShowLocationButton,
                ModStrings.HudQuestShowLocation,
                "hud:quest/location"
            );
            AddQuestButton(builder, panel.UnpinButton, ModStrings.HudQuestUnpin, "hud:quest/unpin");
            builder.PopContext();
        }

        /// <summary>One of the icons the panel draws on itself, where the game is drawing it. Drawn AND
        /// enabled: the game hides the marker for a quest with nowhere to point at without ever
        /// switching it off, so asking about enablement alone declares a control the player cannot see
        /// and the game will not act on.</summary>
        private static void AddQuestButton(
            GraphBuilder builder,
            AgeControlButton button,
            string nameKey,
            string key
        )
        {
            AgeTransform widget = AgeWidgets.Transform(button);
            // Synthetic guard: the node declared below carries no widget, so this test is the whole of
            // its existence check - nothing downstream will ask again.
            if (widget == null || !AgeWidgets.Visible(widget) || !AgeWidgets.Operable(widget))
            {
                return;
            }

            AgeControlButton it = button;
            NodeVtable vtable = GraphNodes.Button(
                () => ModStrings.Get(nameKey),
                () => AgeWidgets.Press(it),
                null,
                AgeWidgets.Raw(widget)
            );
            AgeWidgets.PointAt(vtable, widget);
            // Synthetic: mod-authored - the quest strip's own button, which the HUD draws nothing
            // separate for.
            builder.AddItem(Nodes.Synthetic(ControlId.Structural(key), vtable));
        }

        /// <summary>How the quest is going, in the game's own word for it - "Ongoing", or the count of
        /// what is done out of what is needed where the objective has one. The panel hides this label
        /// outright while a quest is waiting on the player to choose between objectives.</summary>
        private static string QuestProgress(PinnedQuestPanel panel)
        {
            try
            {
                return AgeWidgets.Visible(panel.QuestProgressLabel.AgeTransform)
                    ? AgeText.FullLabel(panel.QuestProgressLabel)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Which quest the game is tracking has changed - a quest finished and the journal
        /// pinned the next one, or the player let this one go. Said wherever they are standing,
        /// because the panel is drawn over every page and nothing else reports the change.</summary>
        private void AnnounceQuest()
        {
            if (!_questChanged)
            {
                return;
            }

            _questChanged = false;
            try
            {
                Voice.Say(QuestAnnouncement(), false);
            }
            catch (Exception e)
            {
                Log.Warn("hud: announcing the pinned quest threw: " + e);
            }
        }

        /// <summary>The panel's own words where the game is drawing them, and the quest's title on its
        /// own where it is not - the journal can pin a quest while a full screen is covering the
        /// panel. Nothing pinned is its own sentence rather than an empty one.</summary>
        private string QuestAnnouncement()
        {
            PinnedQuestPanel panel = QuestPanel();
            if (panel != null)
            {
                return ModStrings.Format(
                    ModStrings.HudQuestPinned,
                    new MessageBuilder()
                        .ListItem(AgeText.FullLabel(panel.QuestTitleLabel))
                        .ListItem(QuestProgress(panel))
                        .Build()
                );
            }

            Quest quest = ActiveQuest();
            return quest == null
                ? ModStrings.Get(ModStrings.HudQuestUnpinned)
                : ModStrings.Format(
                    ModStrings.HudQuestPinned,
                    AgeText.Clean(new GuiQuest(quest).Title)
                );
        }

        /// <summary>Listen to the player empire's journal for the tracked quest changing. Subscribed
        /// when the page arrives and given back when it leaves, so the mod holds no subscription
        /// nobody is listening to and a hot reload - which pops every page - leaves none behind.
        /// </summary>
        private void WatchQuests()
        {
            ForgetQuests();
            try
            {
                Empire empire = PlayerEmpire();
                DepartmentOfInternalAffairs affairs =
                    empire == null ? null : empire.GetAgency<DepartmentOfInternalAffairs>();
                QuestJournal journal = affairs == null ? null : affairs.QuestJournal;
                if (journal == null)
                {
                    return;
                }

                _journal = journal;
                journal.ActiveQuestChange += OnActiveQuestChange;
            }
            catch (Exception e)
            {
                Log.Warn("hud: watching the quest journal threw: " + e);
            }
        }

        private void ForgetQuests()
        {
            try
            {
                if (_journal != null)
                {
                    _journal.ActiveQuestChange -= OnActiveQuestChange;
                }
            }
            catch (Exception e)
            {
                Log.Warn("hud: releasing the quest journal threw: " + e);
            }

            _journal = null;
        }

        /// <summary>Only sets state: what the change should say is worked out - and said - from the
        /// per-frame pump, which is also where the panel has finished rewriting itself.</summary>
        private void OnActiveQuestChange(object sender, QuestJournalChangeEventArgs e)
        {
            _questChanged = true;
        }

        private Quest ActiveQuest()
        {
            try
            {
                return _journal == null ? null : _journal.ActiveQuest;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The panel while the game is really showing a quest on it. Three answers have to
        /// agree: the window is up at all (the game hides it behind every full screen it opens), the
        /// panel still holds a quest (it drops it the moment it starts fading out), and nothing above
        /// it in the tree has been hidden.</summary>
        private static PinnedQuestPanel QuestPanel()
        {
            try
            {
                PinnedQuestWindow window = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<PinnedQuestWindow>(false)
                    : null;
                if (window == null || !window.Shown)
                {
                    return null;
                }

                PinnedQuestPanel panel = window.PinnedQuestPanel;
                // Flow control: a null answer is how the caller hears that the quest strip is not on
                // the page, and skips its whole context.
                return panel != null
                    && panel.PinnedQuest != null
                    && AgeWidgets.Visible(panel.AgeTransform)
                    ? panel
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---- the collapsed tutorial ----

        /// <summary>The bar a collapsed tutorial leaves at the top of the right-hand edge - its title,
        /// its close button, the arrow that brings it back. The tutorial screen knows how to read it;
        /// what this decides is WHERE it sits on the eleven pages that share the HUD's right-hand edge:
        /// among that edge's own fixtures, above the notification icons, which is where it is drawn.
        ///
        /// Every other page gets it appended instead, by <see cref="Screen.BuildShared"/>, and on the
        /// same condition: exactly while the game is drawing the bar. This stop is what
        /// <c>GraphBuilder.DeclaredStop</c> answers for, so a page that placed the bar itself is never
        /// given a second one.</summary>
        public void Tutorial(GraphBuilder builder)
        {
            builder.BeginStop(TutorialStop);
            TutorialScreen.BuildCollapsedBar(builder);
        }

        // ---- notifications ----

        /// <summary>
        /// Everything the game is waiting to tell the player, as a list they can walk instead of a
        /// column of icons they would have to click. Enter opens one - the popup that appears is a
        /// screen of ours and takes over from here - and Backslash throws it away, because throwing
        /// one away is the game's OWN right click on the icon
        /// (<c>NotificationItemsWindow.HandleInput</c> :90-101). With nothing
        /// waiting the game shows an empty corner, so this stop is not there at all.
        ///
        /// What a stop here holds is what the strip holds: an icon and, on hovering it, its title.
        /// Not the notification's description - the game does not show that until the popup is opened,
        /// and opening it is what this stop's Enter is for. Putting the whole text in the buffer here
        /// made the strip a second place to read the message, one that answered before the player had
        /// asked and disagreed with the screen as drawn.
        ///
        /// The MOD's own notifications share the same list but are not drawn on the strip at all
        /// (<see cref="NotificationStrip"/>), so they are left out of here and read in
        /// <see cref="TurnLog"/> instead: this stop is what the game is showing, and that one is the
        /// log of what the game never showed.
        ///
        /// The LAST entry is "throw them all away" (<see cref="DismissAllNotifications"/>) - the
        /// gesture the game offers only as an Alt+right click on the bare triangle behind the icons,
        /// over the notifications THIS stop holds and no others: the Turn log has a button of its own
        /// and neither reaches into the other's list (owner ruling 2026-08-24). There is no key for
        /// it: it is a button, reached with the arrows and pressed with Enter (owner ruling
        /// 2026-08-23).
        /// </summary>
        public void Notifications(GraphBuilder builder)
        {
            builder.BeginStop(NotificationStop);
            // The strip is a column of bare icons with no caption over it, so the word is the mod's.
            // Popped in a finally because the walk below has an early return and a catch of its own,
            // and a level left open would take every stop declared after this one with it.
            builder.PushContext(ModStrings.Get(ModStrings.HudNotificationsPanel));
            int count = 0;
            try
            {
                IGuiNotificationService service = Gui.GuiNotificationService;
                if (service == null)
                {
                    return;
                }

                NotificationItem[] items = NotificationItems();
                foreach (GuiNotification notification in service.GetPlayerEmpireGuiNotifications())
                {
                    if (Mine(notification) != null)
                    {
                        continue;
                    }

                    GuiNotification it = notification;
                    NodeVtable vtable = GraphNodes.Button(
                        () => AgeText.Clean(it.GetTitle()),
                        () => Open(it),
                        null,
                        null
                    );
                    vtable.OnContextual = () => Dismiss(it);
                    GoToLocation(vtable, it);
                    // The strip is bare icons: nothing on it says the row can be thrown away, and the
                    // game's own right click is the only way to do it without opening the popup first.
                    NodeHints.Add(vtable, ModStrings.HintDismiss, UiActions.Contextual);
                    vtable.Sections = GraphNodes.Sections(GraphNodes.TooltipDetails(IconTooltip(it, items)), null);
                    // Synthesized from the game's own notification list, not read off a widget: the
                    // strip's icons are pooled and the walk holds the NOTIFICATION, so there is
                    // nothing here whose paint state could vouch for the row. The enumeration is
                    // where the honesty lives - the service lists the notifications that exist.
                    builder.AddItem(
                        Nodes.Synthetic(ControlId.For(it, "hud:notification/" + count), vtable)
                    );
                    count++;
                }

                if (count > 0)
                {
                    // Keyed on the game's OWN control for the gesture - the bare triangle behind the
                    // icons, which the prefab names and the window never binds - so the cursor rides
                    // it and the coverage audit finds the node standing on it rather than reporting a
                    // drawn control nothing declares.
                    AgeTransform triangle = CloseAllTriangle();
                    NodeVtable dismissAll = GraphNodes.Button(
                        () => ModStrings.Get(ModStrings.HudDismissAllNotifications),
                        DismissAllNotifications
                    );
                    // Synthetic where the strip draws no triangle to stand on: the row is then the mod's
                    // own, over the notification list the service keeps.
                    builder.AddItem(
                        triangle == null
                            ? (NodeDeclaration)Nodes.Synthetic(
                                ControlId.Structural("hud:notification/dismiss-all"),
                                dismissAll
                            )
                            : Nodes.Drawn(
                                ControlId.For(triangle, "hud:notification/dismiss-all"),
                                dismissAll,
                                triangle
                            )
                    );
                }
            }
            catch (Exception e)
            {
                Log.Warn("hud: reading the notifications threw: " + e);
            }
            finally
            {
                builder.PopContext();
            }
        }

        /// <summary>
        /// GO TO WHERE THIS HAPPENED, on a row whose popup is not open - a notification icon on the
        /// strip, a line of the turn log.
        ///
        /// Offered exactly where the popup would DRAW a show-location button
        /// (<see cref="NotificationScreen.DrawsShowLocation"/>): the game marks that button visible
        /// from the notification's own <c>HasLocation</c> without asking whether the prefab laid one
        /// out, and forty-one of the sixty-nine did not (ES2 facts). Offering the key where the mouse
        /// has no button would be an affordance the sighted player cannot see - and, worse, one whose
        /// handler moves nothing.
        ///
        /// What it DOES is the button's own handler minus its last line, which toggles the popup open
        /// (<see cref="NotificationScreen.GoToLocation"/>): from a closed row that toggle would open
        /// the popup instead of going anywhere.
        ///
        /// The hint comes FIRST, before "to dismiss": going somewhere is what the row is usually
        /// pressed for, and throwing it away is what is done afterwards.
        /// </summary>
        private static void GoToLocation(NodeVtable vtable, GuiNotification notification)
        {
            GuiNotification it = notification;
            if (!NotificationScreen.DrawsShowLocation(it))
            {
                return;
            }

            vtable.OnGoTo = () => NotificationScreen.GoToLocation(it);
            NodeHints.Add(
                vtable,
                ModStrings.HintGoToLocation,
                UiActions.GoToLocation,
                0,
                () => NotificationScreen.DrawsShowLocation(it)
            );
        }

        /// <summary>
        /// The mod's own notifications - the things that happened this turn and the last few that the
        /// game itself never mentions: a system revealed, a fleet arrived, somebody else's fleet
        /// sighted or lost. They live in the game's list beside the game's own and behave exactly like
        /// them (Enter opens the same popup, Backslash throws the same one away), but the game draws
        /// none of them on its strip, so they are read here rather than beside the icons the player
        /// can see.
        ///
        /// Grouped under the turn each one happened on, NEWEST TURN FIRST, because the news a player
        /// walks a log for is the news that has just landed; within a turn they keep the order they
        /// arrived in. The turn is the one stamped when the notification was made
        /// (<see cref="ModNotification.Turn"/>), so a log spanning the five turns one lives for says
        /// which day each line is from without any line having to say it itself. Each turn is a REGION
        /// as well as a spoken level, so Alt+Up/Down steps a turn at a time.
        ///
        /// No tooltip section, unlike the stop above: the strip binds a mod item's tooltip to its own
        /// title (and then deactivates the item), so a section here would be the row's own words a
        /// second time - measured 2026-08-20, the game notifications' buffers hold exactly their title
        /// for the same reason.
        ///
        /// With nothing logged the stop is not there at all, which is the rule every stop on this HUD
        /// follows. It is the one place that rule is arguable - a sighted player cannot glance at this
        /// list, because there is nothing drawn to glance at - so it is on the owner's list to settle.
        ///
        /// The LAST entry throws the whole log away (<see cref="DismissAllLogged"/>), in a region of
        /// its own after the turns; because the stop only exists while the log holds something, that
        /// button is never offered over an empty list (owner ruling 2026-08-23).
        /// </summary>
        public void TurnLog(GraphBuilder builder)
        {
            List<ModNotification> logged = Logged();
            if (logged.Count == 0)
            {
                return;
            }

            List<int> turns = new List<int>();
            for (int i = 0; i < logged.Count; i++)
            {
                if (!turns.Contains(logged[i].Turn))
                {
                    turns.Add(logged[i].Turn);
                }
            }

            turns.Sort();
            turns.Reverse();

            builder.BeginStop(TurnLogStop);
            builder.PushContext(ModStrings.Get(ModStrings.HudTurnLogPanel));
            try
            {
                for (int t = 0; t < turns.Count; t++)
                {
                    int turn = turns[t];
                    builder.SetRegion("hud:turn-log/turn/" + turn);
                    builder.PushContext(ModStrings.Format(ModStrings.HudTurnLogTurn, turn));
                    try
                    {
                        int within = 0;
                        for (int i = 0; i < logged.Count; i++)
                        {
                            ModNotification it = logged[i];
                            if (it.Turn != turn)
                            {
                                continue;
                            }

                            NodeVtable vtable = GraphNodes.Button(
                                () => AgeText.Clean(it.GetTitle()),
                                () => Open(it)
                            );
                            vtable.OnContextual = () => Dismiss(it);
                            GoToLocation(vtable, it);
                            NodeHints.Add(vtable, ModStrings.HintDismiss, UiActions.Contextual);
                            // Synthetic: the turn log is the mod's own record of notifications that have
                            // been and gone - the HUD draws nothing for a dismissed one.
                            builder.AddItem(Nodes.Synthetic(
                                ControlId.For(it, "hud:turn-log/" + turn + "/" + within),
                                vtable
                            ));
                            within++;
                        }
                    }
                    finally
                    {
                        builder.PopContext();
                    }
                }

                // Throw the whole log away, in a region of its own so no turn owns it and Alt+Down
                // from the last turn reaches it. Declared unconditionally here: the stop does not
                // exist at all while the log is empty (above), so there is never a button offering to
                // clear nothing.
                builder.SetRegion("hud:turn-log/dismiss-all");
                // Synthetic: mod-authored - a command over the mod's own log.
                builder.AddItem(Nodes.Synthetic(
                    ControlId.Structural("hud:turn-log/dismiss-all"),
                    GraphNodes.Button(
                        () => ModStrings.Get(ModStrings.HudDismissAllTurnLog),
                        DismissAllLogged
                    )
                ));
            }
            finally
            {
                builder.PopContext();
            }
        }

        /// <summary>The widget the game hangs its close-all on: <c>BaseTriangleBackground</c>, an
        /// <c>AgeControlButton</c> whose only wiring is <c>OnRightClickMethod=OnCloseAllCb</c>. The
        /// window exposes no field for it, so it is found by the name the prefab gives it - which is
        /// unique under that window (measured 2026-08-23). It carries no tooltip of any kind, which is
        /// why the button's name is the mod's.</summary>
        private static AgeTransform CloseAllTriangle()
        {
            try
            {
                NotificationItemsWindow window = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<NotificationItemsWindow>(false)
                    : null;
                return window == null
                    ? null
                    : AgeWidgets.ChildNamed(window.AgeTransform, "BaseTriangleBackground", 3);
            }
            catch (Exception e)
            {
                Log.Warn("hud: finding the close-all triangle threw: " + e);
                return null;
            }
        }

        /// <summary>
        /// Throw away everything the GAME is waiting to tell the player, and nothing of the mod's:
        /// every notification on the strip dismissed one by one, which is the same discard Backslash
        /// makes on the row it is standing on (<see cref="Dismiss"/>).
        ///
        /// NOT the game's own <c>DismissAllGuiNotifications</c> - the call its icon strip makes for an
        /// Alt+right click on the triangle behind the icons
        /// (<c>NotificationItemsWindow.OnCloseAllCb</c> :237-245). The game keeps ONE list and the
        /// mod's own notifications live in it, so that call takes the Turn log with it. Each of these
        /// two buttons clears its own list and leaves the other standing (owner ruling 2026-08-24),
        /// and which list a notification is in is the one question <see cref="Mine"/> answers - for
        /// the strip stop, for the Turn log and for both buttons - so no two of them can disagree and
        /// nothing falls between them.
        ///
        /// That handler's other branch, Shift, only HIDES the popups that happen to be open and
        /// dismisses nothing; the mod offers the dismissing one, because that is what a strip with no
        /// popup up can be asked for.
        ///
        /// Walked over the split's own copy, since dismissing removes each one from the list it reads.
        /// </summary>
        private static void DismissAllNotifications()
        {
            try
            {
                IGuiNotificationService service = Gui.GuiNotificationService;
                if (service == null)
                {
                    return;
                }

                List<GuiNotification> theirs = OwnedNotifications.Theirs(
                    service.GetPlayerEmpireGuiNotifications(),
                    Split
                );
                for (int i = 0; i < theirs.Count; i++)
                {
                    Dismiss(theirs[i]);
                }
            }
            catch (Exception e)
            {
                Log.Warn("hud: dismissing every notification threw: " + e);
            }
        }

        /// <summary>Throw away every line of the Turn log and nothing else - the same discard Backslash
        /// makes on one row (<see cref="Dismiss"/>), over the mod's own notifications only, so the
        /// game's icon strip is left exactly as it was. Walked over a copy, since dismissing removes
        /// each one from the list this reads.</summary>
        private static void DismissAllLogged()
        {
            List<ModNotification> logged = Logged();
            for (int i = 0; i < logged.Count; i++)
            {
                Dismiss(logged[i]);
            }
        }

        /// <summary>The mod's own notification, where this notification is one of the mod's - the ONE
        /// test behind the split between the two lists the player walks. The strip stop leaves these
        /// out, the Turn log holds exactly these, each dismiss-all clears exactly one side of it
        /// (<see cref="OwnedNotifications"/>), and a minimized popup hands back to the stop this
        /// answers for (<c>NotificationScreen.ListOf</c>). Five readings, one test.</summary>
        public static ModNotification Mine(GuiNotification notification)
        {
            return notification as ModNotification;
        }

        /// <summary>The same test as a converter, held once so that splitting a list allocates
        /// nothing beyond the list it answers with.</summary>
        private static readonly Converter<GuiNotification, ModNotification> Split = Mine;

        private static readonly List<ModNotification> NoneLogged = new List<ModNotification>();

        /// <summary>Every mod notification standing in the player's list, in the list's own order. The
        /// same list the stop above walks - one list is what makes the popup's Previous/Next cross
        /// between the game's news and the mod's.</summary>
        private static List<ModNotification> Logged()
        {
            try
            {
                IGuiNotificationService service = Gui.GuiNotificationService;
                return service == null
                    ? NoneLogged
                    : OwnedNotifications.Mine(service.GetPlayerEmpireGuiNotifications(), Split);
            }
            catch (Exception e)
            {
                Log.Warn("hud: reading the turn log threw: " + e);
                return NoneLogged;
            }
        }

        /// <summary>The tooltip the strip hangs on this notification's icon - read from the icon
        /// rather than composed from the notification, so it stays whatever the game decides to put
        /// there. Today the game binds it to the notification's title, and the buffer drops a first
        /// line that only repeats the control's name, so the usual result is a buffer holding exactly
        /// the one line the strip shows.</summary>
        private static AgeTooltip IconTooltip(GuiNotification notification, NotificationItem[] items)
        {
            try
            {
                for (int i = 0; i < items.Length; i++)
                {
                    if (ReferenceEquals(items[i].GuiNotification, notification))
                    {
                        return items[i].Tootlip;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("hud: matching a notification to its icon threw: " + e);
            }

            return null;
        }

        private static readonly NotificationItem[] NoItems = new NotificationItem[0];

        private static NotificationItem[] NotificationItems()
        {
            try
            {
                NotificationItemsWindow window = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<NotificationItemsWindow>(false)
                    : null;
                return window == null
                    ? NoItems
                    : window.GetComponentsInChildren<NotificationItem>(true);
            }
            catch (Exception e)
            {
                Log.Warn("hud: finding the notification icons threw: " + e);
                return NoItems;
            }
        }

        private static void Open(GuiNotification notification)
        {
            try
            {
                Gui.GuiNotificationService.ToggleGuiNotification(notification);
            }
            catch (Exception e)
            {
                Log.Warn("hud: opening a notification threw: " + e);
            }
        }

        /// <summary>Throw a notification away. One the game will not let go of stays, silently: the
        /// key simply did nothing, which is what a key that does not apply here should do.</summary>
        private static void Dismiss(GuiNotification notification)
        {
            try
            {
                if (notification.IsDismissible)
                {
                    Gui.GuiNotificationService.DismissGuiNotification(notification);
                }
            }
            catch (Exception e)
            {
                Log.Warn("hud: dismissing a notification threw: " + e);
            }
        }

        // ---- the turn ----

        /// <summary>What the turn itself offers: end it, move everything that was told to move, walk
        /// to the next fleet with nothing to do, and open the game menu.</summary>
        public void Turn(GraphBuilder builder)
        {
            EndTurnWindow window = TurnWindow();
            // Flow control: everything the turn offers is read under this, and a stop with nothing in
            // it is a Tab press that lands nowhere.
            if (window == null || !AgeWidgets.Visible(window.AgeTransform))
            {
                return;
            }

            List<Cell> found = new List<Cell>();
            EndTurnWindow it = window;
            AgeControlButton endTurn = window.EndTurnButton;
            // Banding input: AddCell appends without the gate's question, and the corner's controls are
            // worked into rows by where they are drawn.
            if (AgeWidgets.Visible(AgeWidgets.Transform(endTurn)))
            {
                NodeVtable vtable = GraphNodes.Button(
                    () => EndTurnLabel(it),
                    () => AgeWidgets.Press(endTurn),
                    () => CanEndTurn(it)
                );
                vtable.Announcements.Add(GraphNodes.ValuePart(() => TurnText(it)));
                vtable.Sections = GraphNodes.Sections(() => EndTurnReason(it), null);
                AgeWidgets.Point(vtable, endTurn);
                found.Add(
                    new Cell
                    {
                        Widget = AgeWidgets.Transform(endTurn),
                        Id = ControlId.For(endTurn, "hud:end-turn"),
                        Vtable = vtable,
                    }
                );
            }

            AddTurnButton(found, window.ApplyMovementsButton, "apply-movements", ModStrings.GalaxyApplyMovements, null);
            AddTurnButton(
                found,
                window.NextIdleFleetButton,
                "next-idle-fleet",
                ModStrings.GalaxyNextIdleFleet,
                IdleFleetsText,
                NextIdleFleet
            );
            AddTurnButton(found, window.GameMenuButton, "game-menu", ModStrings.GalaxyGameMenu, null);
            AddPendingNotifications(found, window.PendingNotificationButton);
            AddRequestToggle(found, window.RequestToggle);
            AddSync(found, window);
            AddPlayers(found, window);
            AddTimers(found, window);
            AddRealTimeClock(found, window);

            // One control per row: the cluster's members are peers of one kind - things to do with the
            // turn - and which of them the game drew beside which is a fact about the corner they are
            // packed into, not about what they are. Up and down walk the lot.
            builder.BeginStop(TurnStop);
            for (int i = 0; i < found.Count; i++)
            {
                builder.AddItem(Nodes.Drawn(found[i].Id, found[i].Vtable, found[i].Widget));
            }
        }

        /// <summary><paramref name="activate"/> is for the one button whose click the mod does better
        /// than a press of it would (<see cref="NextIdleFleet"/>); everything else on the turn cluster
        /// presses the game's own control, which is what keeps a button the mod knows nothing about
        /// working.</summary>
        private void AddTurnButton(
            List<Cell> found,
            AgeControlButton button,
            string key,
            string nameKey,
            Func<string> value,
            Action<AgeControlButton> activate = null
        )
        {
            // Banding input: same corner, same door - AddCell takes the button without asking the gate.
            if (!AgeWidgets.Visible(AgeWidgets.Transform(button)))
            {
                return;
            }

            AgeControlButton it = button;
            Action<AgeControlButton> act = activate;
            NodeVtable vtable = GraphNodes.Button(
                () => ModStrings.Get(nameKey),
                () =>
                {
                    if (act == null)
                    {
                        AgeWidgets.Press(it);
                        return;
                    }

                    act(it);
                },
                () => AgeWidgets.Enabled(AgeWidgets.Transform(it)),
                AgeWidgets.Readable(AgeWidgets.Raw(AgeWidgets.Transform(it)))
            );
            if (value != null)
            {
                vtable.Announcements.Add(GraphNodes.ValuePart(value));
            }

            AgeWidgets.Point(vtable, it);
            found.Add(
                new Cell
                {
                    Widget = AgeWidgets.Transform(it),
                    Id = ControlId.For(it, "hud:" + key),
                    Vtable = vtable,
                }
            );
        }

        /// <summary>
        /// The way back to the notifications the scan view is holding up.
        ///
        /// The scan view suppresses every notification pop-up while it is open
        /// (<c>GuiManager.CanShowNotifications</c> :1584), and this button is how the game offers the
        /// player the ones that queued up behind it: a click is <c>ToggleScanView</c>
        /// (<c>EndTurnWindow.OnPendingNotificationCb</c> :1368-1371), which leaves the scan view and lets
        /// the pop-ups arrive.
        ///
        /// It lives in the window's <c>ScanViewGroup</c>, so normal view never draws it whatever starts
        /// its fade - and the game does FADE it rather than hide it (modifiers started on a notification
        /// arriving :1708-1714 and on the turn being ended in scan view :1684-1690, run backwards when
        /// the turn validates :1692-1706, reset on every view switch :1678-1682). A faded-out control is
        /// still <c>Visible</c>, so the node exists only while the player can actually SEE it
        /// (<see cref="AgeWidgets.Painted"/>). Nothing announces its arrival: it is there to be found,
        /// not to interrupt.
        ///
        /// The game writes no caption on it - a bare icon whose tooltip is a sentence about what a click
        /// would do - so the name is the mod's.
        /// </summary>
        private void AddPendingNotifications(List<Cell> found, AgeTransform button)
        {
            // Kept although the cell carries this widget: the gate counts an ANIMATING alpha as drawn,
            // which is right for a window fading itself in and wrong here - this control is faded both
            // ways by the game as its own state, so its own disappearance would keep it declared for
            // the length of the fade. PAINTED is the stricter test it needs.
            if (!AgeWidgets.Painted(button))
            {
                return;
            }

            AgeTransform at = button;
            NodeVtable vtable = GraphNodes.Button(
                () => ModStrings.Get(ModStrings.GalaxyPendingNotifications),
                () => AgeWidgets.Press(at),
                () => AgeWidgets.Enabled(at),
                AgeWidgets.Readable(AgeWidgets.Raw(at))
            );
            AgeWidgets.PointAt(vtable, at);
            found.Add(
                new Cell
                {
                    Widget = at,
                    Id = ControlId.For(at, "hud:pending-notifications"),
                    Vtable = vtable,
                }
            );
        }

        /// <summary>
        /// The switch tucked in beside the turn controls that shows what an ALLIANCE is coordinating: the
        /// requests allies pin on the map, and the panel they are sent from - the game opens the list and
        /// flips ping visibility together on one click
        /// (<c>EndTurnWindow.OnToggleRequestCb</c> :1337-1354).
        ///
        /// It is drawn on every game and switched off for an empire in no alliance, with the game's own
        /// sentence for why on its tooltip (<c>RequestToggleTooltipContent</c> :555-570) - which is the
        /// whole reason to declare it while it refuses: a control nobody can find is a feature nobody
        /// knows exists. The game writes no caption for it anywhere (a bare icon, whose tooltip is a
        /// sentence about what a click would do rather than a name), so the name is the mod's.
        /// </summary>
        private void AddRequestToggle(List<Cell> found, AgeControlToggle toggle)
        {
            AgeTransform widget = AgeWidgets.Transform(toggle);
            // Banding input: AddCell appends without the gate's question, and the toggle bands with the
            // rest of the corner.
            if (!AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeControlToggle it = toggle;
            AgeTransform at = widget;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            Func<bool> enabled = () => AgeWidgets.Offered(at);
            NodeVtable vtable = GraphNodes.Checkbox(
                () => ModStrings.Get(ModStrings.GalaxyAllianceRequests),
                () => it.State,
                () => AgeWidgets.Toggle(it),
                enabled,
                tooltip
            );
            GraphNodes.AddRefusal(vtable, tooltip, enabled);
            AgeWidgets.Point(vtable, it, tooltip, widget);
            found.Add(
                new Cell
                {
                    Widget = widget,
                    Id = ControlId.For(toggle, "hud:alliance-requests"),
                    Vtable = vtable,
                }
            );
        }

        // ---- the multiplayer half of the turn cluster ----

        /// <summary>
        /// Whether the game is still in step with the other players, and the host's way out when it is
        /// not.
        ///
        /// The game draws the state as a tinted icon and puts the whole of its meaning on a tooltip
        /// (<c>EndTurnWindow.RefreshSyncState</c> :1254-1269 hangs the <c>SyncStatus&lt;state&gt;</c>
        /// element's description there), so the sentence is what this row SAYS rather than something
        /// hanging off it. The group is drawn only outside single player (:734), which is what keeps
        /// every line here absent from a solo game.
        ///
        /// The button beside it returns everybody to the lobby to reload the last auto-save
        /// (<c>OnDesyncStatusClickCb</c> :1318-1321) and is switched on only for the host, and only on a
        /// checksum mismatch - so it is declared while refusing, like every other button the mod
        /// declares: knowing the way out exists is the point.
        /// </summary>
        private void AddSync(List<Cell> found, EndTurnWindow window)
        {
            AgeTransform group = window.SyncGroup;
            // Banding input: AddCell appends without the gate's question, and the game draws this group
            // only for the host on a checksum mismatch.
            if (!AgeWidgets.Visible(group))
            {
                return;
            }

            EndTurnWindow it = window;
            // The sync state exists only as the words on the group's own tooltip, so the tooltip is
            // what the row declares and the readout says them by the ordinary rule - rather than the
            // row copying them into a value of its own and the buffer holding them twice.
            NodeVtable vtable = GraphNodes.Readout(
                () => ModStrings.Get(ModStrings.GalaxySyncState),
                () => null,
                null,
                it.SyncTooltip
            );
            AgeWidgets.PointAt(vtable, group);
            found.Add(
                new Cell
                {
                    Widget = group,
                    Id = ControlId.For(group, "hud:sync"),
                    Vtable = vtable,
                }
            );

            AddTurnButton(found, window.DesyncButton, "desync", ModStrings.GalaxyReturnToLobby, null);
        }

        /// <summary>
        /// Where the other players are in their turn: how many are still playing, and a line each for
        /// what the game says about them.
        ///
        /// Read off the ring of slots the game draws around the End Turn button - which is drawn in
        /// multiplayer only (:735) and, unlike the players list, is NOT gated on where the mouse is
        /// (<c>EndTurnWindow.SpecificUpdate</c> :906-921 shows that list only while the physical cursor
        /// is inside the button, and the mod moves no cursor). Each slot already carries the game's own
        /// sentence about its player - leader and faction, then the state word
        /// (<c>CompetitorOrbitalSlot.Refresh</c> :45-68) - so nothing here recomputes a player state.
        ///
        /// One row rather than one per player: the cluster is a handful of buttons in the corner of the
        /// screen, and eight more stops in it would be walked past on every pass. The per-player lines
        /// are the row's reviewable content.
        /// </summary>
        private void AddPlayers(List<Cell> found, EndTurnWindow window)
        {
            AgeTransform ring = window.CompetitorsCircularTable;
            // Banding input, and a different widget: the ring is what a single-player game does not
            // draw, while the one cell below stands on it and is read for every player's line.
            if (!AgeWidgets.Visible(ring))
            {
                return;
            }

            EndTurnWindow it = window;
            NodeVtable vtable = GraphNodes.Readout(
                () => ModStrings.Get(ModStrings.GalaxyPlayers),
                () => PlayersText(it),
                () => PlayerLines(it),
                null,
                // The count changes as players end their turn, and the watch below is what announces
                // that wherever the player is standing; a watched value would say it twice here.
                false
            );
            found.Add(
                new Cell
                {
                    Widget = ring,
                    Id = ControlId.For(ring, "hud:players"),
                    Vtable = vtable,
                }
            );
        }

        /// <summary>
        /// The clocks a multiplayer game can be running: the whole game's, which the game writes as a
        /// label, and the current turn's, which it draws as arcs around the End Turn button with no
        /// number written anywhere.
        ///
        /// Neither value is watched. Both change every second, and a value that re-announces itself
        /// under the cursor would talk over everything else the player is doing; asked for, they are
        /// current.
        /// </summary>
        private void AddTimers(List<Cell> found, EndTurnWindow window)
        {
            EndTurnWindow it = window;
            AgeTransform global = window.GlobalTimerLabel == null
                ? null
                : window.GlobalTimerLabel.AgeTransform;
            // Banding input: AddCell appends without the gate's question, and the game draws the timers
            // only in a game that runs one.
            if (AgeWidgets.Visible(global))
            {
                NodeVtable vtable = GraphNodes.Readout(
                    () => ModStrings.Get(ModStrings.GalaxyGlobalTimer),
                    () => OneLine(AgeText.Label(it.GlobalTimerLabel)),
                    null,
                    null,
                    false
                );
                found.Add(
                    new Cell
                    {
                        Widget = global,
                        Id = ControlId.For(global, "hud:global-timer"),
                        Vtable = vtable,
                    }
                );
            }

            AgeTransform arc = window.CommonTimerArc == null
                ? null
                : window.CommonTimerArc.AgeTransform;
            if (arc == null || TimerSeconds(window) < 0)
            {
                return;
            }

            NodeVtable turnTimer = GraphNodes.Readout(
                () => ModStrings.Get(TimerNameKey(it)),
                () => ModStrings.Format(ModStrings.GalaxyTimerSeconds, TimerSeconds(it)),
                null,
                null,
                false
            );
            found.Add(
                new Cell
                {
                    Widget = arc,
                    Id = ControlId.For(arc, "hud:turn-timer"),
                    Vtable = turnTimer,
                }
            );
        }

        /// <summary>
        /// The wall clock the game can draw above the End Turn button - the real time of day, not
        /// anything about the game.
        ///
        /// The player switches it on in the options ("Display In-Game Clock") and picks its format
        /// there, and the game writes the label once a minute from <c>DateTime.Now</c>
        /// (<c>EndTurnWindow.UpdateRealTimeClockCoroutine</c> :946-969). The row is exactly the label:
        /// no arithmetic, no format of the mod's, so a player who chose 24-hour time hears 24-hour time.
        ///
        /// Declared only while the game is drawing it - the option on, no global timer in the way, and
        /// no save in flight, which is one flag the window itself computes
        /// (<c>Refresh</c> :880) - and not watched, for the same reason as the timers above.
        /// </summary>
        private void AddRealTimeClock(List<Cell> found, EndTurnWindow window)
        {
            AgeTransform clock = window.RealTimeClockLabel == null
                ? null
                : window.RealTimeClockLabel.AgeTransform;
            // Banding input: same door as the timers - AddCell takes the clock without asking the gate.
            if (!AgeWidgets.Visible(clock))
            {
                return;
            }

            EndTurnWindow it = window;
            NodeVtable vtable = GraphNodes.Readout(
                () => ModStrings.Get(ModStrings.GalaxyRealTimeClock),
                () => OneLine(AgeText.Label(it.RealTimeClockLabel)),
                null,
                null,
                false
            );
            found.Add(
                new Cell
                {
                    Widget = clock,
                    Id = ControlId.For(clock, "hud:real-time-clock"),
                    Vtable = vtable,
                }
            );
        }

        /// <summary>How many players have not ended their turn, counted the way the game counts them:
        /// the slots of the ready ring whose unready icon is showing (<c>EndTurnWindow.Refresh</c>
        /// :857-880). -1 when there is no ring, which is every single-player game.</summary>
        private static int PlayersPlaying(EndTurnWindow window)
        {
            try
            {
                AgeTransform ring = window == null ? null : window.CompetitorsCircularTable;
                // Spoken count: this figure is said as "N still playing", and -1 is how the caller
                // hears that there is no ring to count - which is every single-player game.
                if (!AgeWidgets.Visible(ring))
                {
                    return -1;
                }

                IList<AgeTransform> slots = ring.Children;
                int playing = 0;
                for (int i = 0; slots != null && i < slots.Count; i++)
                {
                    CompetitorOrbitalSlot slot = Slot(slots[i]);
                    if (slot != null && slot.UnreadyIcon != null && slot.UnreadyIcon.Visible)
                    {
                        playing++;
                    }
                }

                return playing;
            }
            catch (Exception e)
            {
                Log.Warn("hud: counting the players still playing threw: " + e);
                return -1;
            }
        }

        private static string PlayersText(EndTurnWindow window)
        {
            int playing = PlayersPlaying(window);
            if (playing < 0)
            {
                return null;
            }

            return playing == 0
                ? ModStrings.Get(ModStrings.GalaxyPlayersAllReady)
                : ModStrings.Plural(
                    ModStrings.GalaxyPlayerPlaying,
                    ModStrings.GalaxyPlayersPlaying,
                    playing
                );
        }

        /// <summary>A line per player, in the game's own words: leader and faction, then where they are
        /// in their turn - and, for a human who is not the local player, the whisper instruction the
        /// game appends to the same tooltip, which is reviewable rather than spoken.</summary>
        private static IList<string> PlayerLines(EndTurnWindow window)
        {
            List<string> lines = new List<string>();
            try
            {
                AgeTransform ring = window == null ? null : window.CompetitorsCircularTable;
                // Content: which lines the players' row is reviewed with. Lines, not nodes - the ring
                // declares one cell and these are what it says.
                if (!AgeWidgets.Visible(ring))
                {
                    return lines;
                }

                IList<AgeTransform> slots = ring.Children;
                for (int i = 0; slots != null && i < slots.Count; i++)
                {
                    CompetitorOrbitalSlot slot = Slot(slots[i]);
                    if (slot == null)
                    {
                        continue;
                    }

                    foreach (string line in AgeText.Lines(AgeText.Tooltip(slot.Tooltip)))
                    {
                        lines.Add(line);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("hud: reading the player states threw: " + e);
            }

            return lines;
        }

        private static CompetitorOrbitalSlot Slot(AgeTransform widget)
        {
            try
            {
                return widget == null ? null : widget.GetComponent<CompetitorOrbitalSlot>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// How long the running turn timer has left, in whole seconds, or -1 while no timer is running.
        ///
        /// The window draws the three timers as ARCS with no number on them and keeps the end time and
        /// the kind of timer in private fields (:157-163, written from the timer service's own event
        /// :1520-1530), so there is nothing on screen to read and the fields are the only source. The
        /// same expression the window uses: end time minus the game's clock (:1071).
        /// </summary>
        private static int TimerSeconds(EndTurnWindow window)
        {
            try
            {
                if (window == null || TimerKind(window) == GameTimerType.None)
                {
                    return -1;
                }

                FieldInfo field = TimerField("currentTimerEndTime", ref _timerEnd);
                if (field == null)
                {
                    return -1;
                }

                double left = (double)field.GetValue(window) - global::Game.Time;
                return left <= 0.0 ? -1 : (int)left;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        /// <summary>Which of the three clocks is running, so the row can name itself: the turn's own
        /// timer, the overtime the previous turns banked, or the shortened one the last player left in
        /// the turn is given.</summary>
        private static GameTimerType TimerKind(EndTurnWindow window)
        {
            try
            {
                FieldInfo field = TimerField("currentTimerType", ref _timerKind);
                return field == null
                    ? GameTimerType.None
                    : (GameTimerType)field.GetValue(window);
            }
            catch (Exception)
            {
                return GameTimerType.None;
            }
        }

        private static string TimerNameKey(EndTurnWindow window)
        {
            switch (TimerKind(window))
            {
                case GameTimerType.Overtime:
                    return ModStrings.GalaxyOvertimeTimer;
                case GameTimerType.LastPlayer:
                    return ModStrings.GalaxyLastPlayerTimer;
                default:
                    return ModStrings.GalaxyTurnTimer;
            }
        }

        private static FieldInfo TimerField(string name, ref FieldInfo cache)
        {
            if (cache != null)
            {
                return cache;
            }

            try
            {
                cache = typeof(EndTurnWindow).GetField(
                    name,
                    BindingFlags.Instance | BindingFlags.NonPublic
                );
            }
            catch (Exception)
            {
                cache = null;
            }

            return cache;
        }

        /// <summary>The button's own caption, which the game writes over two lines and rewrites while
        /// a turn is being processed - so it says what the button is doing, not only what it is.
        ///
        /// It ends with the chord that ends the turn from anywhere (<see cref="UiActions.EndTurn"/>),
        /// because the one control every turn passes through is the one worth being able to reach
        /// without walking to it, and a key nothing names is a key nobody finds.</summary>
        private static string EndTurnLabel(EndTurnWindow window)
        {
            string caption = OneLine(AgeText.Label(window.EndTurnTitle));
            return ChordNames.Label(
                string.IsNullOrEmpty(caption)
                    ? ModStrings.Get(ModStrings.GalaxyEndTurn)
                    : caption,
                UiActions.EndTurn,
                0
            );
        }

        /// <summary>
        /// End the turn the way the game's own end-turn SHORTCUT does (<c>EndTurnWindow.HandleInput</c>
        /// :637-654): the same three gates, then the armed cursor put back to the plain galaxy one - an
        /// order still waiting for a target would otherwise eat the turn - and then the session's own
        /// TryToEndTurn. The button is not pressed: the shortcut path is what a key is, and it is the
        /// path the game itself takes for a key.
        ///
        /// A refusal speaks the END-TURN NODE's own reading - its name, which turn it is and
        /// "unavailable" - because the player pressing this key from the far side of the page cannot see
        /// the button greying out. It is read out of the graph rather than composed again here, so the
        /// key and the button can never say different things; the game's sentence about WHY stays where
        /// the button keeps it, in the review buffer (<see cref="EndTurnReason"/>). Success says nothing
        /// at all, exactly as pressing the button says nothing.
        ///
        /// False means the key was not this page's business (no turn controls drawn), which is what
        /// leaves the press alone.
        /// </summary>
        public static bool EndTurnByKey()
        {
            EndTurnWindow window = TurnWindow();
            // Flow control: false is how the caller hears that the key was not this page's business,
            // which is what leaves the press alone.
            if (window == null || !AgeWidgets.Visible(window.AgeTransform))
            {
                return false;
            }

            if (!CanEndTurn(window))
            {
                SpeakTurnRefusal("hud:end-turn");
                return true;
            }

            try
            {
                ICursorService cursors = Services.GetService<ICursorService>();
                if (
                    cursors != null
                    && cursors.CurrentCursor != null
                    && cursors.CurrentCursor.GetType() != typeof(GalaxyCursor)
                )
                {
                    cursors.ChangeCursor(typeof(GalaxyCursor));
                }

                window.EndTurnService.Target.TryToEndTurn();
            }
            catch (Exception e)
            {
                Log.Warn("hud: ending the turn from the keyboard threw: " + e);
            }

            return true;
        }

        /// <summary>
        /// Go to the next fleet with nothing to do, from anywhere the turn controls are drawn
        /// (`docs/interaction.md`, Control+Alt+F). The act is the one the mod already does better than a
        /// press of the button would (<see cref="NextIdleFleet"/>), so the key and the button's own Enter
        /// are the same route - including the galaxy page's single-camera-move version of it.
        ///
        /// A refusal speaks the BUTTON's own reading out of the graph - its name, how many fleets are
        /// idle and "unavailable" - for the reason the end-turn key does it: the player pressing this from
        /// the far side of the page cannot see the button greyed out, and a global key silent both when it
        /// works and when it refuses is unreadable. Success says nothing, exactly as pressing the button
        /// says nothing; the arrival announces itself.
        ///
        /// False means the key was not this page's business (no turn controls drawn), which is what
        /// leaves the press alone.
        /// </summary>
        public static bool NextIdleFleetByKey()
        {
            EndTurnWindow window = TurnWindow();
            // Flow control: false is how the caller hears that the key was not this page's business.
            if (window == null || !AgeWidgets.Visible(window.AgeTransform))
            {
                return false;
            }

            // Flow control, and availability: the key is only this page's business where the button is
            // drawn at all, and the refusal below reads that same button's own switched-off state.
            AgeTransform button = AgeWidgets.Transform(window.NextIdleFleetButton);
            if (button == null || !AgeWidgets.Visible(button))
            {
                return false;
            }

            // The game switches this button off exactly while nothing is idle
            // (`EndTurnWindow.UpdateIdleFleetsCollectionAndButton` :1038), which is the same question the
            // node beside it answers, so the key and the button can never disagree.
            if (!AgeWidgets.Enabled(button))
            {
                SpeakTurnRefusal("hud:next-idle-fleet");
                return true;
            }

            NextIdleFleet(window.NextIdleFleetButton);
            return true;
        }

        /// <summary>What one of the turn corner's controls says about itself right now, read out of the
        /// graph rather than composed again here - the refusal the player would have heard by walking to
        /// it.</summary>
        private static void SpeakTurnRefusal(string structuralKey)
        {
            GraphNavigator navigator = ModEntry.Navigator;
            GraphRender render = navigator == null ? null : navigator.Render;
            GraphNode node =
                render == null ? null : render.NodeAt(ControlId.Structural(structuralKey));
            if (node != null)
            {
                Voice.Say(GraphAnnouncer.LeafText(node), true);
            }
        }

        /// <summary>Which turn it is. Read from the turn service rather than from the label beside the
        /// button, which the game writes as an icon token followed by the number.</summary>
        private static string TurnText(EndTurnWindow window)
        {
            int turn = Turn(window);
            return turn < 0 ? null : ModStrings.Format(ModStrings.GalaxyTurn, turn);
        }

        /// <summary>
        /// The three gates the game's own end-turn shortcut passes, in its own order: nothing is in
        /// the way, the tutorial is not holding the turn back, and the session will accept it.
        /// </summary>
        private static bool CanEndTurn(EndTurnWindow window)
        {
            try
            {
                if (!Gui.GuiGameWindowService.CanEndTurnByShortcut)
                {
                    return false;
                }

                if (window.EndTurnDisabler != null && window.EndTurnDisabler.IsTargetDisabled())
                {
                    return false;
                }

                return window.EndTurnService != null
                    && window.EndTurnService.Target != null
                    && window.EndTurnService.Target.CanEndTurn();
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Why the button is refusing, when the game says. It hangs no tooltip on this one
        /// button, but the tutorial holding an element back is a thing the game has words for and puts
        /// on every other element it holds back, so those are the words used here.</summary>
        private static IList<string> EndTurnReason(EndTurnWindow window)
        {
            List<string> lines = new List<string>();
            try
            {
                AgeTooltip tooltip = AgeWidgets.Readable(
                    AgeWidgets.Raw(AgeWidgets.Transform(window.EndTurnButton))
                );
                foreach (string line in AgeText.Lines(AgeText.Tooltip(tooltip)))
                {
                    lines.Add(line);
                }

                if (window.EndTurnDisabler != null && window.EndTurnDisabler.IsTargetDisabled())
                {
                    string reason = AgeText.Clean("%TutorialDisabledElementDescription");
                    if (!string.IsNullOrEmpty(reason))
                    {
                        lines.Add(reason);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("hud: reading the end-turn reason threw: " + e);
            }

            return lines;
        }

        /// <summary>
        /// Go to the next fleet with nothing to do - the galaxy page's own route where the player is on
        /// the galaxy page, and the game's button everywhere else.
        ///
        /// The turn cluster is drawn on every page in the game, and only the galaxy page has a map to
        /// land a cursor on; on any other page the button's own behaviour (fly the camera to the fleet,
        /// select it, and let the galaxy page pick the arrival up) is still the right one and is left
        /// alone. On the galaxy page it costs a second camera move, which is what the page's own route
        /// takes out (<see cref="GalaxyHudScreen.GoToNextIdleFleet"/>).
        /// </summary>
        private static void NextIdleFleet(AgeControlButton button)
        {
            GraphNavigator navigator = ModEntry.Navigator;
            GalaxyHudScreen galaxy = navigator == null ? null : navigator.Screen as GalaxyHudScreen;
            if (galaxy == null || !galaxy.GoToNextIdleFleet())
            {
                AgeWidgets.Press(button);
            }
        }

        /// <summary>How many fleets are waiting to be given something to do, counted the way the
        /// button beside it counts them.</summary>
        private string IdleFleetsText()
        {
            try
            {
                Empire empire = Gui.PlayerEmpire;
                if (empire == null)
                {
                    return null;
                }

                global::FleetsScreen.GetIdleFleets(empire, ref _idleFleets);
                return ModStrings.Format(ModStrings.GalaxyIdleFleets, _idleFleets.Count);
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---- shared ----

        /// <summary>A stock and what the next turn does to it, in the game's own number formatting -
        /// grouped, rounded down, and signed for the part that is a change. Shared with the star
        /// system page, whose colony panel draws the same resource strip for one system.</summary>
        internal static string StockAndNet(float stock, float net, int decimals)
        {
            return ModStrings.Format(
                ModStrings.GalaxyStockAndNet,
                Amount(stock, false, decimals),
                Amount(net, true, decimals)
            );
        }

        /// <summary>A number the way the game writes it.</summary>
        internal static string Amount(float value, bool signed, int decimals)
        {
            try
            {
                return Gui.FormatAmount(value, true, Gui.Rounding.Floor, signed, decimals);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static float Value(Empire empire, StaticString property)
        {
            try
            {
                return empire.GetPropertyValue(property);
            }
            catch (Exception)
            {
                return 0f;
            }
        }

        /// <summary>The banner hangs each total's tooltip on the group around the label rather than on
        /// the label, because the icon beside it is part of the same hover target - and that group is
        /// also the shape the player sees, so it is what the row model measures and what the tooltip is
        /// drawn under.</summary>
        private static AgeTransform Area(AgePrimitiveLabel label)
        {
            try
            {
                AgeTransform widget = label.AgeTransform;
                AgeTransform group = widget.Parent;
                return group != null && AgeWidgets.Raw(group) != null ? group : widget;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The game writes the End Turn caption over two lines. Spoken, that is one phrase.
        /// </summary>
        private static string OneLine(string text)
        {
            MessageBuilder message = new MessageBuilder();
            foreach (string line in AgeText.Lines(text))
            {
                message.Fragment(line);
            }

            return message.Build();
        }

        internal static int Turn()
        {
            return Turn(TurnWindow());
        }

        private static int Turn(EndTurnWindow window)
        {
            try
            {
                if (
                    window == null
                    || window.EndTurnService == null
                    || window.EndTurnService.Target == null
                )
                {
                    return -1;
                }

                return window.EndTurnService.Target.Turn + 1;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        internal static Empire PlayerEmpire()
        {
            try
            {
                return Gui.PlayerEmpire;
            }
            catch (Exception)
            {
                return null;
            }
        }

        internal static EndTurnWindow TurnWindow()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<EndTurnWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static GameOverlayWindow OverlayWindow()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<GameOverlayWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
