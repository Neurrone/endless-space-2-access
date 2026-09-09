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
    /// thing, and it says it in the game's own words rather than in a phrase of ours. The words are
    /// the four count phrases the game's own label tooltips are headed with, which tell two fleets of
    /// your own from two of an enemy's and two of an ally's ("2 Fleets", "2 Enemy Fleets", "2 Allied
    /// Fleets"). WHICH of the four is picked here rather than by <c>GuiFleetGroup.Title</c>, because
    /// the game's own choice mistakes every minor civilization for an enemy - see <c>Standing</c>.
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
    /// Every entry point still ANSWERS from a repository walk, so they belong behind an announcement
    /// part that is READ on focus and a section that is read into the buffer, never behind a live part
    /// watched at 60 Hz. What has changed is that the walk is made once a frame for the whole map
    /// rather than once per asking place: the galaxy build asks "what is parked here" of every located
    /// star and "what is flying this" of every lane out of an open system, and each of those questions
    /// used to walk a whole repository. The three memos below are keyed on
    /// <c>Time.frameCount</c> and nothing else, the same shape the map's own <c>MovingNear</c> uses:
    /// within one frame the game has not moved, so a remembered answer is the answer, and the first
    /// ask of the next frame throws the lot away - which is what makes it safe to hold repository
    /// objects here with nothing to remember to clear.
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
        /// It takes BOTH repositories because the game splits the map's fleets between them: a fleet
        /// stops being registered in the fleet label window's repository the moment it finishes docking
        /// (<c>GalaxyFleet.RefreshVisibility</c> keeps it only while it is not visually docked), and
        /// from then on the only place it appears is its system's docking slot - which is where the
        /// dock label finds the fleets it draws in a lozenge. So the under-way half comes from the one
        /// repository and the parked half from the other, and each half is the label window's own
        /// visible set: the slot repository is already vision-filtered, so only the fleet repository
        /// needs the extra label gate applied. A fleet still playing its docking transition is in both,
        /// which is what the GUIDs are for.
        ///
        /// A parked fleet's <c>GalaxyPosition</c> is its star's, which is what puts it in the same
        /// square as the star - the small offset the map draws its berth at is a picture detail, not a
        /// place. A system's own hangar is not here for the same reason it is not in
        /// <c>FleetsAt</c>: it is drawn in the lozenge but it is not a fleet.
        /// </summary>
        public static IList<Fleet> Drawing()
        {
            Frame();
            if (_drawnBuilt)
            {
                return DrawnFleets;
            }

            _drawnBuilt = true;
            try
            {
                List<Fleet> fleets = DrawnFleets;
                HashSet<ulong> seen = SeenFleets;

                IVisibleGalaxyFleetRepositoryService flying =
                    Services.GetService<IVisibleGalaxyFleetRepositoryService>();
                if (flying != null)
                {
                    ReadOnlyCollection<GalaxyFleet> drawn = flying.GalaxyFleets;
                    for (int i = 0; i < drawn.Count; i++)
                    {
                        Fleet fleet = drawn[i] == null ? null : drawn[i].Fleet;
                        if (fleet != null && !fleet.IsDestroyed && Drawn(fleet))
                        {
                            fleets.Add(fleet);
                            seen.Add(fleet.GUID.ToUInt64(null));
                        }
                    }
                }

                IVisibleDockingSlotRepositoryService parked =
                    Services.GetService<IVisibleDockingSlotRepositoryService>();
                if (parked != null)
                {
                    ReadOnlyCollection<DockingSlotCursorTarget> slots = parked.DockingSlots;
                    for (int i = 0; i < slots.Count; i++)
                    {
                        DockingSlotCursorTarget slot = slots[i];
                        if (slot == null)
                        {
                            continue;
                        }

                        ReadOnlyCollection<GalaxyFleet> docked = slot.GalaxyFleets;
                        for (int j = 0; j < docked.Count; j++)
                        {
                            Fleet fleet = docked[j] == null ? null : docked[j].Fleet;
                            if (
                                fleet != null
                                && !fleet.IsDestroyed
                                && seen.Add(fleet.GUID.ToUInt64(null))
                            )
                            {
                                fleets.Add(fleet);
                            }
                        }
                    }
                }

                return fleets;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading every fleet the map draws threw: " + e);
                DrawnFleets.Clear();
                return DrawnFleets;
            }
        }

        private static readonly Fleet[] None = new Fleet[0];

        // ---- the frame memos ----
        //
        // One walk of each repository per frame, handed to every place that asks. The lists and the
        // dictionaries are REUSED rather than rebuilt, so a quiet frame allocates nothing at all; they
        // are emptied by the first ask of a new frame, which is also what keeps the repository objects
        // in them from outliving the frame that drew them.

        private static int _frame = -1;

        private static readonly List<Fleet> DrawnFleets = new List<Fleet>();

        private static readonly HashSet<ulong> SeenFleets = new HashSet<ulong>();

        private static bool _drawnBuilt;

        private static readonly Dictionary<ulong, List<List<Garrison>>> ParkedSlots =
            new Dictionary<ulong, List<List<Garrison>>>();

        private static bool _parkedBuilt;

        private static readonly Dictionary<int, Flying> FlyingLegs = new Dictionary<int, Flying>();

        private static bool _flyingBuilt;

        /// <summary>The fleets on one leg, gathered per empire as <see cref="GroupsOn"/> hands them
        /// out - the owners beside the groups, because which group an owner already has is what the
        /// gathering asks on every fleet.</summary>
        private sealed class Flying
        {
            public readonly List<Empire> Owners = new List<Empire>(2);

            public readonly List<List<Garrison>> Groups = new List<List<Garrison>>(2);
        }

        private static void Frame()
        {
            int frame = UnityEngine.Time.frameCount;
            if (_frame == frame)
            {
                return;
            }

            _frame = frame;
            DrawnFleets.Clear();
            SeenFleets.Clear();
            _drawnBuilt = false;
            ParkedSlots.Clear();
            _parkedBuilt = false;
            FlyingLegs.Clear();
            _flyingBuilt = false;
        }

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
            if (node == null)
            {
                return null;
            }

            List<List<Garrison>> groups;
            return ParkedByNode().TryGetValue(node.GUID.ToUInt64(null), out groups) ? groups : null;
        }

        /// <summary>Every slot the map is drawing a lozenge for, gathered under the node it stands at:
        /// one walk of the repository serves every system the frame declares. A node holding more than
        /// one slot keeps them in the repository's own order, which is the order the walk met them and
        /// therefore the order a single system's own walk used to return.</summary>
        private static Dictionary<ulong, List<List<Garrison>>> ParkedByNode()
        {
            Frame();
            if (_parkedBuilt)
            {
                return ParkedSlots;
            }

            _parkedBuilt = true;
            try
            {
                IVisibleDockingSlotRepositoryService repository =
                    Services.GetService<IVisibleDockingSlotRepositoryService>();
                if (repository == null)
                {
                    return ParkedSlots;
                }

                ReadOnlyCollection<DockingSlotCursorTarget> slots = repository.DockingSlots;
                for (int i = 0; i < slots.Count; i++)
                {
                    DockingSlotCursorTarget slot = slots[i];
                    if (slot == null || slot.GameNode == null)
                    {
                        continue;
                    }

                    List<Garrison> docked = Docked(slot);
                    if (docked.Count == 0)
                    {
                        continue;
                    }

                    ulong at = slot.GameNode.GUID.ToUInt64(null);
                    List<List<Garrison>> groups;
                    if (!ParkedSlots.TryGetValue(at, out groups))
                    {
                        groups = new List<List<Garrison>>(2);
                        ParkedSlots[at] = groups;
                    }

                    groups.Add(docked);
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading the fleets at a system threw: " + e);
            }

            return ParkedSlots;
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
                if (link == null)
                {
                    return null;
                }

                Flying flying;
                return FlyingByLeg()
                    .TryGetValue(
                        Leg(link.ExtremityNode1.NodePosition, link.ExtremityNode2.NodePosition),
                        out flying
                    )
                    ? flying.Groups
                    : null;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading the fleets on a starlane threw: " + e);
                return null;
            }
        }

        /// <summary>Every fleet the map draws under way, gathered under the LEG it is flying: one walk
        /// of the repository serves every lane out of every open system. A fleet in orbit is drawn at
        /// its system rather than on a lane and one with no valid leg is drawn wherever it was left, so
        /// neither is filed here; the rest are filed by the pair of node positions the game stores,
        /// taken either way round (<see cref="Leg"/>), which is the test each lane used to make of every
        /// fleet in the galaxy. The fleets inside a group and the groups inside a leg keep the
        /// repository's own order, so a lane reads what it read before.</summary>
        private static Dictionary<int, Flying> FlyingByLeg()
        {
            Frame();
            if (_flyingBuilt)
            {
                return FlyingLegs;
            }

            _flyingBuilt = true;
            try
            {
                IVisibleGalaxyFleetRepositoryService repository =
                    Services.GetService<IVisibleGalaxyFleetRepositoryService>();
                if (repository == null)
                {
                    return FlyingLegs;
                }

                ReadOnlyCollection<GalaxyFleet> drawn = repository.GalaxyFleets;
                for (int i = 0; i < drawn.Count; i++)
                {
                    Fleet fleet = drawn[i] == null ? null : drawn[i].Fleet;
                    if (fleet == null || fleet.IsDestroyed || !Drawn(fleet))
                    {
                        continue;
                    }

                    FleetPosition position = fleet.Position;
                    if (position.IsInOrbit || !position.IsInMovement)
                    {
                        continue;
                    }

                    int leg = Leg(position.Movement.Start, position.Movement.Goal);
                    Flying on;
                    if (!FlyingLegs.TryGetValue(leg, out on))
                    {
                        on = new Flying();
                        FlyingLegs[leg] = on;
                    }

                    int at = on.Owners.IndexOf(fleet.Empire);
                    if (at < 0)
                    {
                        on.Owners.Add(fleet.Empire);
                        on.Groups.Add(new List<Garrison>(2));
                        at = on.Groups.Count - 1;
                    }

                    on.Groups[at].Add(fleet);
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading the fleets on a starlane threw: " + e);
            }

            return FlyingLegs;
        }

        /// <summary>One key for a leg whichever way round it is flown - the two node indices, smaller
        /// first. A node index is a short, so the pair fits an int with no collision and no boxing.
        /// </summary>
        private static int Leg(NodePosition one, NodePosition two)
        {
            int first = one.NodeIndex;
            int second = two.NodeIndex;
            return first <= second
                ? ((ushort)first << 16) | (ushort)second
                : ((ushort)second << 16) | (ushort)first;
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
                    || (int)fleet.Visibility[empire] >= (int)EntityVisibility.Layer.Visible;
            }
            catch (Exception)
            {
                return true;
            }
        }

        /// <summary>The extra test the fleet label window makes before drawing a label at all
        /// (<c>FleetLabelsWindow.ShowAllLabels</c>): the repository holds the fleets that exist on the
        /// map, this is the one that says whether this empire may look at them.
        ///
        /// Guarded the way <see cref="ShowsShipCount"/> is, and for the same reasons: a fleet with no
        /// visibility table at all, and a player who is not a major empire (a spectator, a loading
        /// frame), are both states the indexer throws in rather than answers. Unanswerable is treated
        /// as VISIBLE here, matching the sibling: the window's test is what withholds a label, and a
        /// mod that cannot ask it must not withhold one on its own account.</summary>
        private static bool Drawn(Fleet fleet)
        {
            MajorEmpire empire = Gui.PlayerEmpire as MajorEmpire;
            return fleet.Visibility == null
                || empire == null
                || (int)fleet.Visibility[empire] >= (int)EntityVisibility.Layer.Marked;
        }

        private const string CountPlayer = "%PanelFeatureFleetCountPlayer";
        private const string CountEnemy = "%PanelFeatureFleetCountEnemy";
        private const string CountNeutral = "%PanelFeatureFleetCountNeutral";
        private const string CountAllied = "%PanelFeatureFleetCountAllied";

        /// <summary>The game's own heading for a lozenge holding these garrisons - the count phrase its
        /// tooltip is titled with, in the game's own words and its own singular/plural pair, with only
        /// the CHOICE between the four phrases made here (<c>Standing</c>).</summary>
        private static string Title(List<Garrison> garrisons)
        {
            return Gui.Localize(
                Standing(garrisons) + (garrisons.Count > 1 ? "Plural" : "Single"),
                garrisons.Count.ToString()
            );
        }

        /// <summary>Which of the game's four count phrases a lozenge's owner earns.
        ///
        /// Whose the group is stays the game's answer (<c>GuiFleetGroup.Empire</c>): the first
        /// garrison's empire when it is the player's, otherwise the empire it is FLYING THE COLOURS of,
        /// so a disguised fleet reads as whoever it is pretending to be. Which way the player stands to
        /// that empire is <see cref="SideOf(Empire)"/>.</summary>
        private static string Standing(List<Garrison> garrisons)
        {
            try
            {
                switch (SideOf(new GuiFleetGroup(garrisons).Empire))
                {
                    case Side.Player:
                        return CountPlayer;
                    case Side.Enemy:
                        return CountEnemy;
                    case Side.Friendly:
                        return CountAllied;
                    default:
                        return CountNeutral;
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading which way the player stands to a fleet group threw: " + e);
                return CountNeutral;
            }
        }

        /// <summary>Which way the player stands to somebody - the one answer the whole mod classifies
        /// standing by. Its four buckets are the four the game's own count phrases come in, and the
        /// surfaces that need fewer read them together: the scanner files Player with Friendly, and the
        /// spoken fleet phrase leaves Player out altogether.</summary>
        public enum Side
        {
            /// <summary>The reading empire's own.</summary>
            Player,

            /// <summary>Somebody the game's own ladder puts at or below cold war, at war, or an
            /// unbought pirate.</summary>
            Enemy,

            /// <summary>Everybody in between - a peace, a truce, a minor civilization on cordial
            /// terms, an empire not yet met, and anything with no owner at all.</summary>
            Neutral,

            /// <summary>Above peace on the game's ladder, or a minor civilization under the player's
            /// wing, or the Academy allied.</summary>
            Friendly,
        }

        /// <summary>
        /// Which way the player stands to whoever owns something, resolving the reading empire and its
        /// foreign office from the GUI.
        /// </summary>
        public static Side SideOf(Empire owner)
        {
            Empire player = Gui.PlayerEmpire;
            return SideOf(
                owner,
                player,
                player == null ? null : player.GetAgency<DepartmentOfForeignAffairs>()
            );
        }

        /// <summary>
        /// The one standing ladder in the mod - what the lozenge count phrases, the spoken fleet
        /// phrase and the scanner's affiliation filters all classify by (owner ruling 2026-08-26:
        /// every surface uses the game's own word).
        ///
        /// For a major empire this walks the game's own ladder exactly, cold war included - the map
        /// calls a cold-war neighbour's fleets enemy fleets and so does this, so the tooltip heading and
        /// the spoken heading agree. Below that ladder is where the game's version goes wrong:
        /// <c>GetDiplomaticRelationStateValue</c> knows only the eight MAJOR state names and answers -1
        /// for everything else, which drops every minor faction, the Academy and the pirates into the
        /// "at most cold war" branch and calls a friendly minor civilization's fleets enemy fleets. On
        /// the screen they are drawn in their owner's own colour, so a sighted player is never told
        /// that; only the heading is wrong, and it is wrong for exactly the states the value function
        /// does not name. So those states are bucketed here: at war (which includes the Academy's own
        /// war state) or an unbought pirate is an enemy, the three states that put a minor civilization
        /// under the player's wing plus an Academy alliance are friendly, and everything else - cordial,
        /// amicable, friendly, not yet met - is neutral.
        ///
        /// <paramref name="player"/> and <paramref name="foreign"/> are passed in for a caller that
        /// already holds them - a scanner sweep classifies hundreds of things against the one empire.
        /// </summary>
        public static Side SideOf(Empire owner, Empire player, DepartmentOfForeignAffairs foreign)
        {
            try
            {
                if (owner == null)
                {
                    return Side.Neutral;
                }

                if (ReferenceEquals(owner, player))
                {
                    return Side.Player;
                }

                DiplomaticRelation relation =
                    foreign == null ? null : foreign.GetDiplomaticRelation(owner);
                DiplomaticRelationState state = relation == null ? null : relation.State;
                if (state == null)
                {
                    return Side.Neutral;
                }

                int value = DiplomaticRelationState.GetDiplomaticRelationStateValue(state.Name);
                if (value >= 0)
                {
                    if (
                        value
                        <= DiplomaticRelationState.GetDiplomaticRelationStateValue(
                            DiplomaticRelationState.Names.Major.ColdWar
                        )
                    )
                    {
                        return Side.Enemy;
                    }

                    if (
                        value
                        <= DiplomaticRelationState.GetDiplomaticRelationStateValue(
                            DiplomaticRelationState.Names.Major.Peace
                        )
                    )
                    {
                        return Side.Neutral;
                    }

                    return Side.Friendly;
                }

                if (owner is PirateEmpire)
                {
                    return state.Name == DiplomaticRelationState.Names.Pirate.Peace
                        ? Side.Neutral
                        : Side.Enemy;
                }

                if (state.IsWarState)
                {
                    return Side.Enemy;
                }

                if (
                    state.Name == DiplomaticRelationState.Names.Minor.Brainwashed
                    || state.Name == DiplomaticRelationState.Names.Minor.Aligned
                    || state.Name == DiplomaticRelationState.Names.Minor.Integrated
                    || state.Name == DiplomaticRelationState.Names.Academy.Ally
                )
                {
                    return Side.Friendly;
                }

                return Side.Neutral;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading which way the player stands to an empire threw: " + e);
                return Side.Neutral;
            }
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
                    message.ListItem(AgeText.Clean(garrisons[j].LocalizedName));
                }

                lines.Add(message.Build());
            }

            return lines;
        }
    }
}
