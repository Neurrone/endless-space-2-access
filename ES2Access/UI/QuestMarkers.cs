using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;

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
    /// system's review buffer says, the marker's own node under that system, the top-level row an
    /// open-space marker gets, the scanner's Quest markers category, and the inspect cell's reading.
    /// A marker is placed on a THING, not on a place, so where it STANDS is that thing's own node -
    /// a planet's system, a curiosity's system, the node a fleet is standing at - and a marker on a
    /// fleet in mid-lane stands at no node at all, which is what <see cref="Marker.Node"/> being
    /// invalid means.
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

            /// <summary>The node it stands at, or <c>NodePosition.Invalid</c> for one planted out in
            /// the open (a fleet crossing a lane).</summary>
            public NodePosition Node;

            /// <summary>The pin itself, for a caller that needs to tell two markers of one quest
            /// apart.</summary>
            public QuestMarker Pin;
        }

        /// <summary>Every marker the map is showing this empire, in journal order. Empty rather than
        /// null for every failure, so no caller has to guard.</summary>
        public static List<Marker> Of(Empire empire)
        {
            List<Marker> found = new List<Marker>();
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

                    string title = AgeText.Clean(new GuiQuest(quest).Title);
                    if (string.IsNullOrEmpty(title))
                    {
                        continue;
                    }

                    List<QuestMarker> pins = quest.GetMarkers(step);
                    for (int m = 0; pins != null && m < pins.Count; m++)
                    {
                        QuestMarker pin = pins[m];
                        if (!Shown(pin, empire))
                        {
                            continue;
                        }

                        found.Add(
                            new Marker
                            {
                                Quest = quest,
                                Step = step,
                                Title = title,
                                Pinned = ReferenceEquals(quest, pinned),
                                At = pin.GalaxyPosition,
                                Node = NodeOf(pin),
                                Pin = pin,
                            }
                        );
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("quests: listing the markers on the map threw: " + e);
            }

            return found;
        }

        /// <summary>What a marker is called: the quest's title in the tracked or the ordinary form -
        /// the map's own distinction, and the phrase a system's review buffer has always used.
        /// </summary>
        public static string Name(Marker marker)
        {
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

        /// <summary>Where a marker stands, as the node it is at. The game resolves a marker's position
        /// through the thing it is bound to (<c>QuestMarker.GalaxyPosition</c>), and the same five
        /// kinds of thing answer here - a node, a planet's system, a curiosity's system, a colony's
        /// system, the node a fleet is standing at - because a position on the map is not a place in
        /// the tree. Invalid for anything else, which includes a fleet in mid-lane.</summary>
        private static NodePosition NodeOf(QuestMarker marker)
        {
            IGameEntity target = marker == null ? null : marker.Target;
            GameNode node = target as GameNode;
            if (node != null)
            {
                return node.NodePosition;
            }

            Planet planet = target as Planet;
            if (planet != null && planet.StarSystemNode != null)
            {
                return planet.StarSystemNode.NodePosition;
            }

            Curiosity curiosity = target as Curiosity;
            if (curiosity != null && curiosity.CuriosityController != null)
            {
                StarSystemNode at = curiosity.CuriosityController.GetNode();
                return at == null ? NodePosition.Invalid : at.NodePosition;
            }

            ColonizedStarSystem colony = target as ColonizedStarSystem;
            if (colony != null && colony.Node != null)
            {
                return colony.Node.NodePosition;
            }

            Fleet fleet = target as Fleet;
            return fleet == null ? NodePosition.Invalid : fleet.NodePosition;
        }
    }
}
