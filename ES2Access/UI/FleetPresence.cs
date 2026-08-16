using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Amplitude.Unity.Framework;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// Which fleets the map is drawing at a place - a star system, or a starlane between two of them.
    ///
    /// A sighted player learns this from the map itself: a lozenge sitting on a system, a lozenge
    /// sliding along a lane, each with a number in it. So a place on this map has to say the same
    /// thing, and it says it in the game's own words rather than in a phrase of ours. The number
    /// comes from <c>GuiFleetGroup.Title</c>, which is what the game's own label tooltips are headed
    /// with - and which already knows the difference between two fleets of your own, two of an
    /// enemy's and two of an ally's ("2 Fleets", "2 Enemy Fleets", "2 Allied Fleets"), because the
    /// count phrase it picks depends on the diplomatic relation.
    ///
    /// WHICH fleets are drawn is never re-derived here. Both answers come from the repositories the
    /// two label windows themselves iterate - <c>DockLabelsWindow.ShowAllLabels</c> walks
    /// <c>IVisibleDockingSlotRepositoryService.DockingSlots</c>, <c>FleetLabelsWindow.ShowAllLabels</c>
    /// walks <c>IVisibleGalaxyFleetRepositoryService.GalaxyFleets</c> - so a fleet nobody can see is
    /// absent for the same reason it is absent from the picture, and no vision rule is reimplemented.
    ///
    /// A system's group is the DOCKING SLOT's, assembled by the same rule the dock label assembles it
    /// (<c>DockLabel.FillDockedGarrisons</c>): the system's own hangar when it is holding ships, then
    /// every fleet parked in the slot. One slot exists per empire per system, so a system contested by
    /// two empires reads as two groups, exactly as the map draws two lozenges there.
    ///
    /// A lane's fleets are the ones whose current movement runs between that lane's two ends, in
    /// either direction - the game stores a fleet's leg as a start and a goal node
    /// (<c>FleetPosition.Movement</c>), and a leg is a lane. Cancelling a move does NOT clear that leg
    /// (the game leaves start and goal intact), so a fleet stranded mid-lane still belongs to its lane -
    /// which matters now that the tree hangs every fleet under the place the map draws it and there is
    /// no fleet stop left to catch one that belongs nowhere.
    ///
    /// Nothing here is cached and nothing runs per frame: every entry point walks a repository, so
    /// they belong behind an announcement part that is READ on focus and a section that is read into
    /// the buffer, never behind a live part watched at 60 Hz.
    /// </summary>
    public static class FleetPresence
    {
        /// <summary>The count phrase for everything the map draws at a system, one part per group the
        /// map draws a lozenge for. Null when it draws none.</summary>
        public static string At(GameNode node)
        {
            return Compose(GroupsAt(node));
        }

        /// <summary>The same for the fleets the map draws out on a lane.</summary>
        public static string On(Link link)
        {
            return Compose(GroupsOn(link));
        }

        /// <summary>Each group as one reviewable line - its count phrase followed by the names of the
        /// fleets in it, which is what the label's own dossier tooltip shows a mouse resting there.
        /// </summary>
        public static IList<string> LinesAt(GameNode node)
        {
            return Detail(GroupsAt(node));
        }

        public static IList<string> LinesOn(Link link)
        {
            return Detail(GroupsOn(link));
        }

        /// <summary>
        /// The fleets themselves, in the order the map's own lozenge lists them - what the tree hangs
        /// under a system so that each of them can be read and worked one at a time.
        ///
        /// The groups are what the map draws, and a group can hold something that is not a fleet: a
        /// system's own hangar is drawn in the lozenge and counted in its title, and it is not a thing
        /// that can be selected or sent anywhere. So the count phrase keeps it and this does not.
        /// </summary>
        public static IList<Fleet> FleetsAt(GameNode node)
        {
            return Fleets(GroupsAt(node));
        }

        /// <summary>The same for the fleets the map draws out on a lane - the ones whose current leg is
        /// this lane, which is where a fleet under way is drawn and therefore where it belongs.
        /// </summary>
        public static IList<Fleet> FleetsOn(Link link)
        {
            return Fleets(GroupsOn(link));
        }

        /// <summary>
        /// Every fleet the map is drawing ANYWHERE, parked or under way - for a caller asking about a
        /// REGION of the map rather than about a place in it (the inspect cursor's square of galaxy),
        /// which has no node and no link to ask through.
        ///
        /// The fleet label window's own repository and its own gate, so the answer is the set of
        /// lozenges on the screen and no vision rule is re-derived. A parked fleet's
        /// <c>GalaxyPosition</c> is its star's, which is what puts it in the same square as the star -
        /// the small offset the map draws its berth at is a picture detail, not a place.
        /// </summary>
        public static IList<Fleet> Drawing()
        {
            try
            {
                IVisibleGalaxyFleetRepositoryService repository =
                    Services.GetService<IVisibleGalaxyFleetRepositoryService>();
                if (repository == null)
                {
                    return None;
                }

                ReadOnlyCollection<GalaxyFleet> drawn = repository.GalaxyFleets;
                List<Fleet> fleets = new List<Fleet>(drawn.Count);
                for (int i = 0; i < drawn.Count; i++)
                {
                    Fleet fleet = drawn[i] == null ? null : drawn[i].Fleet;
                    if (fleet != null && !fleet.IsDestroyed && Drawn(fleet))
                    {
                        fleets.Add(fleet);
                    }
                }

                return fleets;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading every fleet the map draws threw: " + e);
                return None;
            }
        }

        private static readonly Fleet[] None = new Fleet[0];

        private static IList<Fleet> Fleets(List<List<Garrison>> groups)
        {
            if (groups == null || groups.Count == 0)
            {
                return None;
            }

            List<Fleet> fleets = new List<Fleet>(groups.Count);
            for (int i = 0; i < groups.Count; i++)
            {
                List<Garrison> garrisons = groups[i];
                for (int j = 0; j < garrisons.Count; j++)
                {
                    Fleet fleet = garrisons[j] as Fleet;
                    if (fleet != null)
                    {
                        fleets.Add(fleet);
                    }
                }
            }

            return fleets;
        }

        /// <summary>The garrisons the map draws in one lozenge, in the order it draws them.</summary>
        private static List<List<Garrison>> GroupsAt(GameNode node)
        {
            try
            {
                IVisibleDockingSlotRepositoryService repository =
                    Services.GetService<IVisibleDockingSlotRepositoryService>();
                if (node == null || repository == null)
                {
                    return null;
                }

                List<List<Garrison>> groups = null;
                ReadOnlyCollection<DockingSlotCursorTarget> slots = repository.DockingSlots;
                for (int i = 0; i < slots.Count; i++)
                {
                    DockingSlotCursorTarget slot = slots[i];
                    if (slot == null || slot.GameNode == null || slot.GameNode.GUID != node.GUID)
                    {
                        continue;
                    }

                    List<Garrison> docked = Docked(slot);
                    if (docked.Count == 0)
                    {
                        continue;
                    }

                    if (groups == null)
                    {
                        groups = new List<List<Garrison>>(2);
                    }

                    groups.Add(docked);
                }

                return groups;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading the fleets at a system threw: " + e);
                return null;
            }
        }

        /// <summary>What a dock label puts in its lozenge: the system's own hangar while it is holding
        /// ships of the player's, then every fleet in the slot that is still alive
        /// (<c>DockLabel.FillDockedGarrisons</c>).</summary>
        private static List<Garrison> Docked(DockingSlotCursorTarget slot)
        {
            List<Garrison> garrisons = new List<Garrison>(2);
            GalaxyGarrison hangar = slot.GalaxyHangar;
            if (
                hangar != null
                && hangar.Garrison != null
                && hangar.Garrison.ShipsCount > 0
                && hangar.Garrison.Empire == Gui.PlayerEmpire
                && !slot.ContainsAttachedMothership
            )
            {
                garrisons.Add(hangar.Garrison);
            }

            ReadOnlyCollection<GalaxyFleet> fleets = slot.GalaxyFleets;
            for (int i = 0; i < fleets.Count; i++)
            {
                GalaxyFleet fleet = fleets[i];
                if (fleet != null && fleet.Fleet != null && !fleet.Fleet.IsDestroyed)
                {
                    garrisons.Add(fleet.Fleet);
                }
            }

            return garrisons;
        }

        /// <summary>
        /// The fleets flying this lane, gathered per empire.
        ///
        /// The map merges the lozenges of fleets that end up drawn close together, whoever owns them
        /// (<c>MergedFleetLabels</c>), which is a pixel question with no answer off the screen. Empire
        /// is the grouping the COUNT PHRASE needs instead: the phrase is chosen from the diplomatic
        /// relation to the group's owner, so a group of mixed owners would describe everyone in it as
        /// whatever the first one is.
        /// </summary>
        private static List<List<Garrison>> GroupsOn(Link link)
        {
            try
            {
                IVisibleGalaxyFleetRepositoryService repository =
                    Services.GetService<IVisibleGalaxyFleetRepositoryService>();
                if (link == null || repository == null)
                {
                    return null;
                }

                NodePosition one = link.ExtremityNode1.NodePosition;
                NodePosition two = link.ExtremityNode2.NodePosition;
                List<Empire> owners = null;
                List<List<Garrison>> groups = null;
                ReadOnlyCollection<GalaxyFleet> flying = repository.GalaxyFleets;
                for (int i = 0; i < flying.Count; i++)
                {
                    Fleet fleet = flying[i] == null ? null : flying[i].Fleet;
                    if (fleet == null || fleet.IsDestroyed || !Drawn(fleet) || !Between(fleet, one, two))
                    {
                        continue;
                    }

                    if (groups == null)
                    {
                        owners = new List<Empire>(2);
                        groups = new List<List<Garrison>>(2);
                    }

                    int at = owners.IndexOf(fleet.Empire);
                    if (at < 0)
                    {
                        owners.Add(fleet.Empire);
                        groups.Add(new List<Garrison>(2));
                        at = groups.Count - 1;
                    }

                    groups[at].Add(fleet);
                }

                return groups;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading the fleets on a starlane threw: " + e);
                return null;
            }
        }

        /// <summary>
        /// Whether the map lets the player SELECT this fleet at all.
        ///
        /// One refusal and it is not about who owns the fleet: `GalaxyFleetCursorTarget` turns both
        /// selection and highlight off for an AUTOMATED fleet (:17-24 and :26-33), so an automated
        /// delivery fleet does not even light up under the pointer, let alone open the fleet panel.
        /// Everything else the map draws a lozenge for is selectable whoever owns it - there is no
        /// empire test anywhere in that target.
        ///
        /// What this is for is the ROLE WORD as much as the action: a row that announced itself a
        /// button and then did nothing on Enter would be promising something the map never offered.
        /// </summary>
        public static bool Selectable(Fleet fleet)
        {
            try
            {
                return !fleet.IsAutomated;
            }
            catch (Exception)
            {
                return true;
            }
        }

        /// <summary>
        /// Whether the map puts a SHIP COUNT on this fleet's lozenge.
        ///
        /// Seeing a fleet and seeing how big it is are two different permissions, and the map draws the
        /// second one strictly (<c>GarrisonsLabelButton.RefreshShipCount</c> :203-217): a fleet is
        /// counted into the number on the lozenge only while it is AUTOMATED - a wandering monster,
        /// whose strength the game tells everybody - or the empire's own visibility of it has reached
        /// Visible. Below that the map shows the fleet and says nothing about its size, so neither does
        /// this mod: an omitted part, not a placeholder, because the game draws no placeholder either.
        ///
        /// An empire's own fleets are always at full visibility, so this is only ever false for
        /// somebody else's.
        /// </summary>
        public static bool ShowsShipCount(Fleet fleet)
        {
            try
            {
                MajorEmpire empire = Gui.PlayerEmpire as MajorEmpire;
                return fleet.IsAutomated
                    || fleet.Visibility == null
                    || empire == null
                    || (int)fleet.Visibility[empire] >= 3;
            }
            catch (Exception)
            {
                return true;
            }
        }

        /// <summary>The extra test the fleet label window makes before drawing a label at all
        /// (<c>FleetLabelsWindow.ShowAllLabels</c>): the repository holds the fleets that exist on the
        /// map, this is the one that says whether this empire may look at them.</summary>
        private static bool Drawn(Fleet fleet)
        {
            return (int)fleet.Visibility[Gui.PlayerEmpire as MajorEmpire] >= 2;
        }

        /// <summary>Whether the leg a fleet is currently flying is this lane, taken either way round.
        /// A fleet in orbit is drawn at its system rather than on a lane, and one with no valid leg is
        /// drawn wherever it was left.</summary>
        private static bool Between(Fleet fleet, NodePosition one, NodePosition two)
        {
            FleetPosition position = fleet.Position;
            if (position.IsInOrbit || !position.IsInMovement)
            {
                return false;
            }

            NodePosition start = position.Movement.Start;
            NodePosition goal = position.Movement.Goal;
            return (start == one && goal == two) || (start == two && goal == one);
        }

        /// <summary>The game's own heading for a lozenge holding these garrisons - the count phrase its
        /// tooltip is titled with, which knows whose fleets they are.</summary>
        private static string Title(List<Garrison> garrisons)
        {
            return new GuiFleetGroup(garrisons).Title;
        }

        private static string Compose(List<List<Garrison>> groups)
        {
            if (groups == null || groups.Count == 0)
            {
                return null;
            }

            MessageBuilder message = new MessageBuilder();
            for (int i = 0; i < groups.Count; i++)
            {
                message.ListItem(Title(groups[i]));
            }

            return message.Build();
        }

        private static IList<string> Detail(List<List<Garrison>> groups)
        {
            if (groups == null || groups.Count == 0)
            {
                return null;
            }

            List<string> lines = new List<string>(groups.Count);
            for (int i = 0; i < groups.Count; i++)
            {
                List<Garrison> garrisons = groups[i];
                MessageBuilder message = new MessageBuilder();
                message.ListItem(Title(garrisons));
                for (int j = 0; j < garrisons.Count; j++)
                {
                    message.ListItem(garrisons[j].LocalizedName);
                }

                lines.Add(message.Build());
            }

            return lines;
        }
    }
}
