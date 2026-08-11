using System;
using System.Collections.Generic;
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
    /// What is read is the card's own fields, in the order it draws them, and only the ones it is
    /// showing - a world with no anomalies has no anomaly line. The words come off the card's labels
    /// rather than being rebuilt from the model, because half of what the card says exists nowhere
    /// else: "Unique planet" and the pirate-lair warning are text the panel was authored with, and
    /// the anomaly, curiosity and deposit lines are assembled by the panel from templates of its own.
    /// The one field taken from the model instead is the planet's NAME, which the card ellipsizes to
    /// fit its box the way the improvements tiles do.
    ///
    /// Reading the labels is safe despite the typewriter animation. The typewriter does not write the
    /// text a character at a time - it sets the label's whole text at once and then moves a cursor the
    /// RENDERER draws up to (<c>AgeModifierTypewriter</c> only advances the label's CurrentLine and
    /// CurrentCharInLine), so the label holds every word from the first frame and is read complete
    /// while a sighted player is still watching it appear.
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
        /// <summary>Reused across frames rather than allocated per update: the announcer runs every
        /// tick the cutscene is up.</summary>
        private readonly List<string> _fields = new List<string>();

        /// <summary>The planet the last announcement was about. Instance state, so a hot reload
        /// starts the cutscene's watch over rather than inheriting a stale answer.</summary>
        private Planet _announced;

        public override string Key
        {
            get { return "screen.system-discovery"; }
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

            string text = FieldReadout.Compose(Fields(card, planet));
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            // Recorded only now, on the one path that actually said something: a card that was not
            // filled in yet is asked again next frame rather than being counted as read.
            _announced = planet;
            Voice.Say(text, false);
        }

        /// <summary>The card's fields in the order it draws them: what the world is called, whether it
        /// is one of a kind, whether something hostile is living on it, how big it is and what kind of
        /// place it is, whether anyone has claimed it, then whatever the survey turned up - anomalies,
        /// curiosities, deposits - and last how good it is at each of the four outputs.</summary>
        private IList<string> Fields(PlanetLabel_SystemDiscovery card, Planet planet)
        {
            _fields.Clear();
            _fields.Add(Name(planet));
            AddLabel(card.UniqueSubtitle);
            AddLabel(card.HostilePresenceTitle);
            AddLabel(card.PlanetSizeAndType);
            AddLabel(card.PlanetStatus);
            AddItems(card.PlanetAnomaliesTable);
            AddItems(card.PlanetCuriositiesTable);
            AddItems(card.ResourceDepositsGroup);
            AddItems(card.FidsScoreTable);
            return _fields;
        }

        /// <summary>What the planet is called, in full. The card writes the same name and then
        /// truncates it to the width of its box - "Cravin." for Cravings - so the model's answer is
        /// the one worth speaking; it is the same string the card started from.</summary>
        private static string Name(Planet planet)
        {
            try
            {
                return AgeText.Clean(planet.LocalizedName);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void AddLabel(AgePrimitiveLabel label)
        {
            AgeTransform widget = Transform(label);
            if (AgeWidgets.Visible(widget))
            {
                _fields.Add(AgeText.Label(label));
            }
        }

        /// <summary>A row per thing the table holds, rather than the table's text in one lump: two
        /// worlds' worth of findings routinely repeat a word - two lines both rated "Poor" - and a
        /// single readout of the whole table would drop the second as a duplicate.
        ///
        /// What each item SAYS (<see cref="AgeWidgets.ItemText"/>), not the text drawn on it: the
        /// anomalies, the curiosities and the deposits are rows of bare pictures, so reading them as
        /// text left the survey's whole findings unspoken - the one thing the cutscene exists to
        /// report.</summary>
        private void AddItems(AgeTransform table)
        {
            if (!AgeWidgets.Visible(table))
            {
                return;
            }

            IList<AgeTransform> items = Children(table);
            for (int i = 0; items != null && i < items.Count; i++)
            {
                if (items[i] != null && AgeWidgets.Visible(items[i]))
                {
                    _fields.Add(AgeWidgets.ItemText(items[i]));
                }
            }
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

        private static AgeTransform Transform(AgePrimitiveLabel label)
        {
            try
            {
                return label == null ? null : label.AgeTransform;
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
    }
}
