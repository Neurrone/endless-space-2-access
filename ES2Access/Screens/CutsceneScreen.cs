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
    /// interaction - any input action or click cuts the video short (<c>HandleInput</c> takes anything) -
    /// and no key of the mod's is bound: a skip of our own would duplicate one the game already answers.
    ///
    /// One consequence of being a screen at all, inherited from <see cref="SystemDiscoveryScreen"/> and
    /// spelled out here because it is a cost: while a mod screen is focused the mod claims Enter, so ENTER
    /// no longer cuts the scene short. Escape and the mouse still do, and Escape is the key the game's own
    /// prompt is about, so the affordance survives - but it is narrower than it was.
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
    /// The colonization scene additionally draws a planet card, which is read ONCE when the card is bound -
    /// the same panel the discovery cutscene draws, so the same reading (<see cref="DiscoveryCards"/>).
    /// The card is what the scene is ABOUT, and it exists nowhere else while the video is up.
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

        /// <summary>The card already read, so the colonization scene describes its world once. Held as the
        /// PLANET rather than as a flag: the window is reused by the next colonization.</summary>
        private Planet _described;

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

        public override void OnPush()
        {
            Rearm();
        }

        public override void OnPop()
        {
            Rearm();
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
            _described = null;
        }

        private void Announce()
        {
            CutsceneModalWindow window = Showing();
            if (window == null)
            {
                return;
            }

            Card(window as ColonizationCutsceneModalWindow);
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

        /// <summary>The world being settled, once, as soon as the scene has bound its card. The window
        /// binds the card at the END of its show animation (<c>OnEndShow</c>), so the wait is real but
        /// short; until then there is nothing to describe and nothing is recorded as described.</summary>
        private void Card(ColonizationCutsceneModalWindow window)
        {
            if (window == null)
            {
                return;
            }

            PlanetLabel_SystemDiscovery card = window.PlanetLabel;
            Planet planet = Bound(card);
            if (planet == null || ReferenceEquals(planet, _described))
            {
                return;
            }

            string text = DiscoveryCards.Read(card);
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            _described = planet;
            Voice.Say(text, false);
        }

        private static Planet Bound(PlanetLabel_SystemDiscovery card)
        {
            try
            {
                return card == null || !AgeWidgets.Visible(card.AgeTransform) ? null : card.Planet;
            }
            catch (Exception)
            {
                return null;
            }
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
