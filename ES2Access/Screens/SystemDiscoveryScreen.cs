using System;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// The first time a fleet reaches a system, the game stops playing and shows it off: the camera
    /// flies through, and one planet at a time a card is typed onto the screen naming the world and
    /// everything the survey found on it. This is that cutscene, made audible.
    ///
    /// Like the loading screen it is something that HAPPENS to the player rather than something they
    /// do - the game's own keys are the whole of the interaction, a click to hurry it along and
    /// Escape or a right-click to cut it short - so nothing is declared to navigate and every word
    /// comes from the per-frame update. Nothing of ours is bound here: a skip key of our own would
    /// duplicate a key the game already answers.
    ///
    /// Arriving says which system is being discovered, because the cutscene draws no heading of its
    /// own - the galaxy is hidden for the duration and the only window up is the one planet card, so
    /// the system's name is on screen nowhere and a player who alt-tabbed back in would otherwise
    /// have no idea what they were being shown.
    ///
    /// Each planet is then announced as the game brings it up, QUEUED: the reveal is a sequence of
    /// cards and a player being read to should hear them in the order they were shown rather than
    /// losing each to the next.
    ///
    /// What the card says is <see cref="DiscoveryCards"/>' - the shared reading of the one panel two
    /// cutscenes draw (the colonization scene draws the same card for the world being settled).
    ///
    /// The card is only read once the panel is bound to the planet the sequence has moved to. The
    /// game binds and refreshes it in one go, inside its own handler for the focus change, so the
    /// check is cheap and the wait never lasts - but until it passes there is nothing to describe,
    /// and a planet the screen has not described is not recorded as announced.
    ///
    /// Everything the player is permitted to see here was settled before the cutscene could start:
    /// the view level refuses to run at all unless the system is explored and this empire's
    /// planets-visibility flag for it is set (<c>GalaxyViewLevel_SystemDiscovery.CanBeActivated</c>),
    /// which is the same gate the galaxy map's planet circles are behind.
    /// </summary>
    public sealed class SystemDiscoveryScreen : Screen
    {
        /// <summary>The planet the last announcement was about. Instance state, so a hot reload
        /// starts the cutscene's watch over rather than inheriting a stale answer.</summary>
        private Planet _announced;

        public override string Key
        {
            get { return ModStrings.ScreenSystemDiscovery; }
        }

        /// <summary>The same layer as the galaxy, the system page and the planet page: these are the
        /// game's view levels over the one map, and no two of them are ever up together.</summary>
        public override int Layer
        {
            get { return 10; }
        }

        /// <summary>Which system is being discovered, in the game's own name for it.</summary>
        public override string ScreenName
        {
            get
            {
                string system = SystemName();
                return string.IsNullOrEmpty(system)
                    ? ModStrings.Get(ModStrings.ScreenSystemDiscovery)
                    : ModStrings.Format(ModStrings.DiscoverySystem, system);
            }
        }

        public override bool IsActive()
        {
            return GalaxyViewLevels.At<GalaxyViewLevel_SystemDiscovery>();
        }

        /// <summary>Escape is the game's: the view level is an input handler of its own and answers
        /// the key by cutting the cutscene short and going back to the galaxy.</summary>
        public override bool Back()
        {
            return false;
        }

        /// <summary>
        /// Every showing starts with nothing announced.
        ///
        /// Deliberately not baselined against the planet the sequence is on: unlike a progress record
        /// that outlives the load that wrote it, the focused planet is dropped when the view level is
        /// deactivated, so anything found here belongs to THIS run of the cutscene and is news. The
        /// arrival announcement cannot double up with it either - arriving says the system, the watch
        /// says the planets - and the sequence can be under way before the GUI admits which view level
        /// is up, so baselining would silently swallow the first planet.
        /// </summary>
        public override void OnPush()
        {
            _announced = null;
        }

        public override void OnPop()
        {
            _announced = null;
        }

        public override void OnUpdate()
        {
            try
            {
                Announce();
            }
            catch (Exception e)
            {
                Log.Warn("discovery: reading the revealed planet threw: " + e);
            }
        }

        private void Announce()
        {
            GalaxyViewLevel_SystemDiscovery level = Level();
            AbstractGalaxyPlanet drawn = level == null ? null : level.FocusedPlanet;
            Planet planet = drawn == null ? null : drawn.Planet;
            if (planet == null || ReferenceEquals(planet, _announced))
            {
                return;
            }

            PlanetLabel_SystemDiscovery card = Card();
            if (card == null || !ReferenceEquals(card.Planet, planet))
            {
                return;
            }

            string text = DiscoveryCards.Read(card);
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            // Recorded only now, on the one path that actually said something: a card that was not
            // filled in yet is asked again next frame rather than being counted as read.
            _announced = planet;
            Voice.Say(text, false);
        }

        private string SystemName()
        {
            try
            {
                GalaxyViewLevel_SystemDiscovery level = Level();
                GalaxyStarSystem system = level == null ? null : level.StarSystem;
                StarSystemNode node = system == null ? null : system.StarSystemNode;
                return node == null ? null : AgeText.Clean(node.LocalizedName);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static GalaxyViewLevel_SystemDiscovery Level()
        {
            return GalaxyViewLevels.Level as GalaxyViewLevel_SystemDiscovery;
        }

        /// <summary>The one panel the cutscene draws. It belongs to a window of the game's, which
        /// rebuilds it, so it is found by asking rather than held.</summary>
        private static PlanetLabel_SystemDiscovery Card()
        {
            try
            {
                PlanetLabelsWindow_SystemDiscovery window = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<PlanetLabelsWindow_SystemDiscovery>(false)
                    : null;
                return window == null ? null : window.PlanetLabel;
            }
            catch (Exception)
            {
                return null;
            }
        }

    }
}
