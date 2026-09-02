using System;
using System.Collections.Generic;
using System.Reflection;
using Amplitude;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;
using ES2Access.UI;

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
    public sealed partial class GlobalHud
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
        private const string ModeEndedKey = ModStrings.CursorModeEnded;

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
            return GameWindows.Of<UserInstructionsWindow>();
        }

        // ---- shared ----

        /// <summary>A stock and what the next turn does to it, in the game's own number formatting -
        /// grouped, rounded down, and signed for the part that is a change. Shared with the star
        /// system page, whose colony panel draws the same resource strip for one system.</summary>
        internal static string StockAndNet(float stock, float net, int decimals)
        {
            return StockAndNet(stock, net, decimals, decimals);
        }

        /// <summary>The same phrase where the two figures are written to different precisions, which
        /// is how the game writes a resource item's pair (<see cref="ES2Access.UI.ResourceRows"/>):
        /// each label's decimals are decided from its OWN value.</summary>
        internal static string StockAndNet(
            float stock,
            float net,
            int stockDecimals,
            int netDecimals
        )
        {
            return ModStrings.Format(
                ModStrings.GalaxyStockAndNet,
                Amount(stock, false, stockDecimals),
                Amount(net, true, netDecimals)
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
            return GameWindows.Of<EndTurnWindow>();
        }

        private static GameOverlayWindow OverlayWindow()
        {
            return GameWindows.Of<GameOverlayWindow>();
        }
    }
}
