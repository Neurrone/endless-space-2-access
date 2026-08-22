using System;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// A video the game plays over everything: the faction introduction a new game opens with, the scene
    /// a finished quest earns, the outro a victory earns, and the one it plays when a colony ship settles
    /// a world (<c>CutsceneModalWindow</c>, and <c>ColonizationCutsceneModalWindow</c> which is the same
    /// window with a planet card on it).
    ///
    /// Like the loading screen this is something that HAPPENS to the player, so there is nothing to
    /// navigate and every word comes from the per-frame update. The game's own keys are the whole of the
    /// interaction - any input action or click cuts the video short (<c>HandleInput</c> takes anything).
    ///
    /// So EVERY key the mod takes here means that skip (<see cref="AnyKey"/>, owner decision
    /// 2026-08-12). Being a screen at all is what makes this necessary rather than a nicety: the keys a
    /// mod screen claims are hidden from the game's own binding matcher, so while a cutscene played, Tab,
    /// Enter and the arrows did nothing at all - the mod had nothing to navigate and the game never saw
    /// the press. The cost is that review keys cannot be used to re-read a subtitle while the video
    /// plays; the player pressed a key at a video, and skipping is what they meant.
    ///
    /// What it says is the SUBTITLE, as the game brings each one up, and only while the player has
    /// subtitles turned on - which is the game's own <c>DisplaySubtitles</c> option and the only reason the
    /// text exists at all. Each is QUEUED, never interrupting: a scene is a sequence of lines and a player
    /// being read to should hear them in the order they were spoken rather than losing each to the next.
    /// The tradeoff is deliberate - a line that ran long is still being read when the next appears - and it
    /// is the same choice the loading screen makes for the same reason.
    ///
    /// Nothing here reads the video's own dialogue where subtitles are off. That would be inventing
    /// content the game does not have, and the option is where a player says whether they want it.
    ///
    /// What the video SHOWS is said too, from the mod's own audio descriptions
    /// (<see cref="CutsceneDescriptions"/>), which are written into the gaps between the spoken lines
    /// and timed off the same clock the game runs its subtitles on. Those are the mod's words rather
    /// than the game's, so they carry their own setting and are the one thing here that speaks with
    /// the game's subtitles turned off.
    ///
    /// The colonization scene draws a planet card as well, and it is NOT read here. The description
    /// track written for that scene takes its place (owner decision 2026-08-22): both would be
    /// talking over a twelve-second video, and the card is the one a player can go back to - the
    /// planet's own screen still has it once the ship has landed, while the footage is gone for good.
    /// The card's reading itself is untouched, and still serves the discovery flythrough that shares
    /// the panel (<see cref="DiscoveryCards"/>).
    ///
    /// One screen for both windows: they are separate window objects on the same layer and are never up
    /// together (a colonization scene and a quest scene are two different game events), so the screen
    /// reads whichever one the game is showing.
    /// </summary>
    public sealed class CutsceneScreen : Screen
    {
        /// <summary>The mod's own word for "a video is playing", for a window that draws no heading and
        /// whose whole content may be a picture. Optional: a build without the phrase says nothing rather
        /// than reading the key.</summary>
        private const string ScreenNameKey = "screen.cutscene";

        /// <summary>The subtitle last spoken, so the same line is not repeated while it stays up. Instance
        /// state, so a hot reload starts the watch over.</summary>
        private string _subtitle;

        public override string Key
        {
            get { return "screen.cutscene"; }
        }

        /// <summary>Above the notifications a quest scene is opened from and above the view levels, and
        /// below the game menu and the message box - a video is not a reason a question cannot be asked.
        /// </summary>
        public override int Layer
        {
            get { return 47; }
        }

        public override string ScreenName
        {
            get { return OptionalText.Phrase(ScreenNameKey); }
        }

        public override bool IsActive()
        {
            try
            {
                CutsceneModalWindow window = Showing();
                return window != null && window.Shown;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The game's: the window answers every key by cutting the video short, which is the
        /// only thing there is to do here.</summary>
        public override bool Back()
        {
            return false;
        }

        /// <summary>Nothing here to search - the screen declares no controls at all - and a letter the
        /// mod claimed for a search that cannot match anything is a letter the game never sees. So the
        /// alphabet stays the game's, and a letter it has a binding for cuts the scene short through the
        /// game's own matcher, exactly as it does with no mod loaded.</summary>
        public override bool AllowsTypeahead
        {
            get { return false; }
        }

        /// <summary>
        /// Any key but Escape, which is the game's own: cut the video short, exactly as the game does for
        /// a key it has no special answer to.
        ///
        /// <c>InputAction.None</c> rather than <c>Exit</c> on purpose - it is what any ORDINARY key
        /// amounts to at this window. Exit is one of the two the base window answers itself by hiding,
        /// and the difference shows on the scene the victory screen plays: shown with
        /// <c>autoHide: false</c>, its own key path unloads the video and leaves the window standing for
        /// the screen behind it to finish with (<c>CutsceneModalWindow.HandleInput</c>,
        /// <c>VictoryScreen.cs:262</c>), which hiding it would skip.
        /// </summary>
        public override bool AnyKey(string actionKey)
        {
            // Spelled out, like ModEntry's: the game has an InputAction of its own in the global
            // namespace, so this file cannot take a using on the mod's input namespace.
            if (actionKey == ES2Access.UI.Input.UiActions.Back)
            {
                return false;
            }

            try
            {
                CutsceneModalWindow window = Showing();
                return window != null && window.HandleInput(InputAction.None);
            }
            catch (Exception e)
            {
                Log.Warn("cutscene: skipping the scene threw: " + e);
                return false;
            }
        }

        public override void OnPush()
        {
            Rearm();
        }

        /// <summary>The scene is over, whether it finished or the player cut it short. Anything the
        /// description track had left goes with it - the footage it was describing is gone.
        ///
        /// Only on the way OUT: the window is already shown by the time this screen is pushed, so a
        /// track armed for the video that just started must survive OnPush.</summary>
        public override void OnPop()
        {
            Rearm();
            CutsceneDescriptions.Rearm();
        }

        public override void OnUpdate()
        {
            try
            {
                Announce();
            }
            catch (Exception e)
            {
                Log.Warn("cutscene: reading the scene threw: " + e);
            }
        }

        private void Rearm()
        {
            _subtitle = null;
        }

        private void Announce()
        {
            CutsceneModalWindow window = Showing();
            if (window == null)
            {
                return;
            }

            // Descriptions first: they are written to sit in the gaps between the spoken lines, so
            // a cue and the subtitle that follows it reach the queue in the order the scene has them.
            CutsceneDescriptions.Tick(window);
            Subtitle(window);
        }

        /// <summary>The line the scene is showing, when it changes. The label holds the whole line from
        /// the frame it is set (the panel only makes it visible then), and the game hides it again between
        /// lines - so an empty label is a gap in the dialogue and not a line worth saying.</summary>
        private void Subtitle(CutsceneModalWindow window)
        {
            AgePrimitiveLabel label = window.CutsceneSubtitle;
            AgeTransform widget = label == null ? null : label.AgeTransform;
            string line = AgeWidgets.Visible(widget) ? AgeText.Label(label) : null;
            if (string.IsNullOrEmpty(line))
            {
                // Cleared rather than kept, so the same line coming back up is news again - a scene
                // repeats a speaker's name across cuts.
                _subtitle = null;
                return;
            }

            if (line == _subtitle)
            {
                return;
            }

            _subtitle = line;
            Voice.Say(line, false);
        }

        /// <summary>Whichever of the two scene windows the game is showing, the colonization one first: it
        /// is the specific case, and asking for it first means a scene with a card on it is never read as
        /// a bare video.</summary>
        private static CutsceneModalWindow Showing()
        {
            CutsceneModalWindow colonization = Window<ColonizationCutsceneModalWindow>();
            if (colonization != null && colonization.Shown)
            {
                return colonization;
            }

            CutsceneModalWindow cutscene = Window<CutsceneModalWindow>();
            return cutscene != null && cutscene.Shown ? cutscene : null;
        }

        private static T Window<T>()
            where T : GuiWindow
        {
            try
            {
                return Gui.GuiServiceAvailable ? Gui.GuiService.GetWindow<T>(false) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
