using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;
using ES2Access.Screens;

namespace ES2Access.UI
{
    /// <summary>
    /// THE quest markers the map is showing this player - one walk, four readers.
    ///
    /// There is no all-markers service to ask, so the walk goes the way the player's own journal goes:
    /// every quest in progress, then the markers of the step it is on (<c>Quest.GetMarkers</c>
    /// :510-522, which is <c>IQuestManagementService.GetMarkers(instance, empire)</c> filtered to that
    /// step). Walking from the QUEST rather than from the pins the map draws is what makes a marker
    /// nameable at all: a pin carries an instance id and nothing else, and the quest's title is on the
    /// quest.
    ///
    /// The gate is the pin's own - <c>GalaxyQuestMarker.UpdateVisibility</c> :157-165 deactivates a
    /// marker that does not list the active player's empire - and it is the whole gate: a marker is an
    /// object placed in the world, not one of the culled label windows, so nothing here moves with the
    /// camera. The TRACKED form is the map's own distinction: it brightens the pin of the pinned
    /// quest, which is the journal's <c>ActiveQuest</c> (<c>QuestMarker.IsMarkerOfPinnedQuestForEmpire</c>
    /// compares exactly that).
    ///
    /// One enumeration, because four surfaces used to walk it and they could disagree: the line a
    /// system's review buffer says, the marker's own node under the thing it marks, the top-level row
    /// an open-space marker gets, the scanner's Quest markers category, and the inspect cell's
    /// reading. A marker is placed on a THING, not on a place, and the thing is kept
    /// (<see cref="Resolve"/>) rather than folded down to the star it stands over: where its row hangs
    /// and what its name says are both questions about the world, the curiosity, the fleet or the star
    /// it was planted on. A marker on a fleet in mid-lane stands at no star at all, which is what
    /// <see cref="Marker.System"/> being null means.
    /// </summary>
    internal static class QuestMarkers
    {
        /// <summary>One quest marker the map is showing.</summary>
        internal struct Marker
        {
            /// <summary>The quest it belongs to.</summary>
            public Quest Quest;

            /// <summary>The step of that quest it was planted for.</summary>
            public QuestStep Step;

            /// <summary>The game's own title for the quest - the only name a marker has.</summary>
            public string Title;

            /// <summary>Whether this is the quest the player is tracking, which the map draws
            /// differently.</summary>
            public bool Pinned;

            /// <summary>Where the pin stands on the map.</summary>
            public GalaxyPosition At;

            /// <summary>The star it stands at, or null for one planted out in the open (a fleet
            /// crossing a lane).</summary>
            public StarSystemNode System;

            /// <summary>Whether the map is NAMING that star. False for a place the picture draws
            /// without naming, and for one it draws nothing at all - both of which are said in the
            /// mod's own unexplored words rather than by a name the player has never been shown.
            /// </summary>
            public bool Named;

            /// <summary>The world it stands on - the planet it is planted on, or the world a
            /// curiosity sits on - or null.</summary>
            public Planet Planet;

            /// <summary>Whether what it is planted on is a curiosity, which is a thing on a world
            /// rather than the world itself.</summary>
            public bool OnCuriosity;

            /// <summary>The fleet it is planted on, or null.</summary>
            public Fleet Fleet;

            /// <summary>The empire the walk was made for, which is whose map every name here is
            /// read off.</summary>
            public Empire Empire;

            /// <summary>The pin itself, for a caller that needs to tell two markers of one quest
            /// apart.</summary>
            public QuestMarker Pin;
        }

        /// <summary>
        /// Every marker the map is showing this empire, in journal order. Empty rather than null for
        /// every failure, so no caller has to guard.
        ///
        /// The list is the walk's own and is handed out again, unchanged, to every caller that asks
        /// for the same empire within one frame - which on the map is three asks per build plus the
        /// inspect cell's and the scanner's. Keyed on (empire, <c>Time.frameCount</c>) the way
        /// <see cref="FrameSweep{T}"/> keys its walks, and for the same reason: the journal moves
        /// between frames and never within one, and a key that is the frame number needs nothing
        /// remembered to clear. Read it, do not keep it: the next frame refills this very list.
        /// </summary>
        public static List<Marker> Of(Empire empire)
        {
            int frame = UnityEngine.Time.frameCount;
            if (frame == _frame && ReferenceEquals(empire, _asked))
            {
                return _found;
            }

            _frame = frame;
            _asked = empire;
            List<Marker> found = _found;
            found.Clear();
            try
            {
                DepartmentOfInternalAffairs affairs =
                    empire == null ? null : empire.GetAgency<DepartmentOfInternalAffairs>();
                QuestJournal journal = affairs == null ? null : affairs.QuestJournal;
                if (journal == null)
                {
                    return found;
                }

                Quest pinned = journal.ActiveQuest;
                ReadOnlyCollection<Quest> quests = journal.Read(QuestState.InProgress);
                for (int i = 0; quests != null && i < quests.Count; i++)
                {
                    Quest quest = quests[i];
                    QuestStep step = quest == null ? null : quest.GetCurrentStep();
                    if (step == null)
                    {
                        continue;
                    }

                    // The markers FIRST, the wrapper only once one of them is shown: `new GuiQuest`
                    // looks the quest's GUI element up and, for a quest that has none (the
                    // victory-condition quests, "QuestDefeatBySystems"), the game logs a warning with
                    // a stack trace - and this walk runs on every render, which filled the game's
                    // diagnostics log at gigabytes an hour (2026-08-23). Most quests have no marker
                    // on their current step, so most never reach the wrapper at all.
                    List<QuestMarker> pins = quest.GetMarkers(step);
                    string title = null;
                    for (int m = 0; pins != null && m < pins.Count; m++)
                    {
                        QuestMarker pin = pins[m];
                        if (!Shown(pin, empire))
                        {
                            continue;
                        }

                        if (title == null)
                        {
                            title = AgeText.Clean(new GuiQuest(quest).Title);
                            if (string.IsNullOrEmpty(title))
                            {
                                break;
                            }
                        }

                        Marker made = new Marker
                        {
                            Quest = quest,
                            Step = step,
                            Title = title,
                            Pinned = ReferenceEquals(quest, pinned),
                            At = pin.GalaxyPosition,
                            Pin = pin,
                            Empire = empire,
                        };
                        Resolve(pin, empire, ref made);
                        found.Add(made);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("quests: listing the markers on the map threw: " + e);
            }

            return found;
        }

        /// <summary>The walk's own list, refilled in place. It holds the frame's quests and pins,
        /// which is the one reference this keeps to game objects; there is no teardown for it and
        /// none is needed, since it dies with the assembly the moment that is replaced.</summary>
        private static readonly List<Marker> _found = new List<Marker>();

        private static Empire _asked;

        private static int _frame = -1;

        /// <summary>
        /// What a marker is called: the quest's title in the tracked or the ordinary form - the map's
        /// own distinction - and then WHERE the pin is planted, in the same words the tree row and the
        /// scanner result both say (owner ruling 2026-09-10), so the two cannot describe one pin
        /// differently.
        ///
        /// The place is the thing the pin is ON, not the sky it hangs over: a world, a curiosity's
        /// world, a fleet and the star it is parked at, or the star itself. A star the map draws
        /// without naming - and one it draws nothing at all for, which a pin still gives the position
        /// of - is the mod's unexplored words: the simulation knows the name and the picture is
        /// withholding it. A pin on nothing the tree can place says the quest and nothing else.
        /// </summary>
        public static string Name(Marker marker)
        {
            if (marker.Fleet != null)
            {
                string fleet = AgeText.Clean(marker.Fleet.LocalizedName);
                return marker.Named
                    ? ModStrings.Format(
                        marker.Pinned
                            ? ModStrings.GalaxyQuestMarkerOnFleetAtPinned
                            : ModStrings.GalaxyQuestMarkerOnFleetAt,
                        marker.Title,
                        fleet,
                        AgeText.Clean(marker.System.LocalizedName)
                    )
                    : ModStrings.Format(
                        marker.Pinned
                            ? ModStrings.GalaxyQuestMarkerOnFleetPinned
                            : ModStrings.GalaxyQuestMarkerOnFleet,
                        marker.Title,
                        fleet
                    );
            }

            // The world's own words, whenever the map has a NAME for the world - which is the
            // survey and not the band (owner ruling 2026-09-10). Where the row hangs is a question
            // about what this build is drawing and it changes with the camera; what the row SAYS is a
            // question about what the player has been shown, and it does not. Below the survey the
            // planet has no name to say - the card calls it unknown - so the pin falls through to the
            // star it stands at, which the map is naming.
            if (
                marker.Planet != null
                && marker.Named
                && GalaxyHudScreen.Surveyed(marker.System, marker.Empire)
            )
            {
                string place = PlanetPlace.Of(marker.System, marker.Planet, marker.Empire);
                if (!string.IsNullOrEmpty(place))
                {
                    return ModStrings.Format(
                        marker.OnCuriosity
                            ? (
                                marker.Pinned
                                    ? ModStrings.GalaxyQuestMarkerCuriosityPinned
                                    : ModStrings.GalaxyQuestMarkerCuriosity
                            )
                            : (
                                marker.Pinned
                                    ? ModStrings.GalaxyQuestMarkerOnPlanetPinned
                                    : ModStrings.GalaxyQuestMarkerOnPlanet
                            ),
                        marker.Title,
                        place
                    );
                }
            }

            if (marker.System != null)
            {
                return marker.Named
                    ? ModStrings.Format(
                        marker.Pinned
                            ? ModStrings.GalaxyQuestMarkerAtSystemPinned
                            : ModStrings.GalaxyQuestMarkerAtSystem,
                        marker.Title,
                        AgeText.Clean(marker.System.LocalizedName)
                    )
                    : ModStrings.Format(
                        marker.Pinned
                            ? ModStrings.GalaxyQuestMarkerUnexploredPinned
                            : ModStrings.GalaxyQuestMarkerUnexplored,
                        marker.Title
                    );
            }

            return ModStrings.Format(
                marker.Pinned
                    ? ModStrings.GalaxySystemQuestMarkerPinned
                    : ModStrings.GalaxySystemQuestMarker,
                marker.Title
            );
        }

        /// <summary>What the step asks for, in the game's own words - the objective description the
        /// journal and the pinned-quest panel draw. Null where the quest has no gui element for the
        /// step, which the game itself logs and draws nothing for.</summary>
        public static IList<string> Objective(Marker marker)
        {
            try
            {
                if (marker.Quest == null || marker.Step == null)
                {
                    return null;
                }

                QuestObjectiveSet set = marker.Step.GetOwnObjectiveSet();
                if (set == null)
                {
                    return null;
                }

                string lore = AgeText.Clean(new GuiQuest(marker.Quest).GetObjectiveLore(set));
                return string.IsNullOrEmpty(lore) ? null : AgeText.Lines(lore);
            }
            catch (Exception e)
            {
                Log.Warn("quests: reading a marker's objective threw: " + e);
                return null;
            }
        }

        /// <summary>Whether this empire is one of the ones the marker is planted for - the pin's own
        /// visibility gate.</summary>
        private static bool Shown(QuestMarker marker, Empire empire)
        {
            Empire[] shown = marker == null ? null : marker.Empires;
            for (int i = 0; shown != null && i < shown.Length; i++)
            {
                if (ReferenceEquals(shown[i], empire))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// What a marker is planted ON, which is where its row hangs and what its name says.
        ///
        /// The game resolves a marker's position through the thing it is bound to
        /// (<c>QuestMarker.GalaxyPosition</c>), and the same five kinds of thing answer here - a node,
        /// a planet, a curiosity, a colony and a fleet. Each of them is a different PLACE in the tree
        /// though they all stand at one star (owner ruling 2026-09-10), so the thing itself is kept
        /// rather than folded down to the star: a pin on a world hangs under that world's row, one on
        /// a curiosity under the world the curiosity sits on, one on a fleet under that fleet's row.
        /// A fleet crossing a lane stands at no star at all, which is what <see cref="Marker.System"/>
        /// being null means.
        /// </summary>
        private static void Resolve(QuestMarker marker, Empire empire, ref Marker made)
        {
            IGameEntity target = marker == null ? null : marker.Target;
            StarSystemNode system = target as StarSystemNode;

            Planet planet = target as Planet;
            if (planet != null)
            {
                made.Planet = planet;
                system = planet.StarSystemNode;
            }

            Curiosity curiosity = target as Curiosity;
            if (curiosity != null)
            {
                made.OnCuriosity = true;
                made.Planet = curiosity.CuriosityController as Planet;
                system = curiosity.GetNode();
            }

            ColonizedStarSystem colony = target as ColonizedStarSystem;
            if (colony != null)
            {
                system = colony.Node as StarSystemNode;
            }

            Fleet fleet = target as Fleet;
            if (fleet != null)
            {
                made.Fleet = fleet;
                system = FleetOrders.Orbit(fleet) as StarSystemNode;
            }

            made.System = system;
            made.Named = system != null && MapVisibility.Perceived(system, empire);
        }
        }
}
