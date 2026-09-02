using System;
using System.Collections.Generic;
using Amplitude;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>The map as a scan lens draws it: the same stars, grouped by WHOSE they are.</summary>
    public sealed partial class GalaxyHudScreen
    {
        /// <summary>Whether the mode was on the last time the page looked, so that beginning and ending
        /// it can each be said once (<see cref="WatchTheLens"/>).</summary>
        private bool _scanWas;

        /// <summary>
        /// Whether the galaxy's own scan lens is up over this page.
        ///
        /// The game's compound (<c>IsInGalaxyScanView</c>) rather than the raw <c>IsInScanView</c>
        /// flag, which is also the battle's per-ship overlay and three cinematics' - and minus the
        /// frames a battle screen is still fading off, where the lens is technically up over a map
        /// nobody is looking at (<see cref="ScanLensPanels.BattleEnding"/>).
        ///
        /// Static because the KEY a system's row hangs under depends on it
        /// (<see cref="SystemKey(StarSystemNode, Empire)"/>) and that is composed from static context.
        /// It reads the game's own fields and remembers nothing, so a hot reload takes nothing with it.
        /// </summary>
        internal static bool Scanning
        {
            get
            {
                try
                {
                    GuiManager gui = GuiService();
                    return gui != null
                        && gui.IsInGalaxyScanView
                        && !ScanLensPanels.BattleEnding();
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Say what the mode itself is doing, which nothing else on the page says any more.
        ///
        /// Entering the lens used to push a screen of its own and the screen's name - the lens - was
        /// the announcement; now the page keeps the keyboard across the change, so there is no screen
        /// arrival to carry it. So the page says the lens on the way in and its own name on the way
        /// out, which is exactly the pair the player used to hear, and the lens WATCHER
        /// (<see cref="ScanLensPanels.Announce"/>) is asked first so that a mode entered on a zoom
        /// that also crossed a descriptor is not said twice.
        ///
        /// Both are gated on the lens having drawn itself: the game turns normal view off several
        /// frames before it shows the overlay, and naming a lens before its window is up names
        /// whichever one the camera's layer happens to imply.
        /// </summary>
        private void WatchTheLens()
        {
            bool lens = _lens.Arrived(Scanning);
            if (lens)
            {
                if (!_lens.Announce() && !_scanWas)
                {
                    Voice.Say(_lens.Name(), false);
                }

                // And the System lens's own panel, which the player opens and closes with a tick and
                // which nothing else would say (<see cref="ScanLensPanels.WatchSystemInfo"/>).
                _lens.WatchSystemInfo();
            }
            else if (_scanWas)
            {
                Voice.Say(ScreenName, false);
            }

            _scanWas = lens;
        }

        // ---- the owner-grouped tree ----

        /// <summary>Which heading a star stands under while a lens is up. The four buckets after the
        /// empires are the ruling's own (2026-09-01): an UNEXPLORED system's owner is not none, it is
        /// unknown - the lens draws "???" over it - so filing it under "No owner" would assert
        /// knowledge the picture withholds.</summary>
        private enum ScanBucket
        {
            Empire = 0,
            Minors = 1,
            Pirates = 2,
            NoOwner = 3,
            Unexplored = 4,
        }

        /// <summary>One heading of the in-mode tree and which of this build's member lists holds the
        /// stars under it - the same pooling the constellation groups use, for the same reason.
        /// </summary>
        private struct OwnerGroup
        {
            public ScanBucket Bucket;

            /// <summary>The empire the heading names, for <see cref="ScanBucket.Empire"/>.</summary>
            public Empire Empire;

            /// <summary>Where the watching empire's intelligence says that empire is - what the
            /// ordering is by. Only where <see cref="Placed"/>.</summary>
            public GalaxyPosition Centre;

            public bool Placed;

            public int Members;
        }

        private readonly List<OwnerGroup> _owners = new List<OwnerGroup>();

        private readonly List<List<StarSystemNode>> _ownerMembers =
            new List<List<StarSystemNode>>();

        private static readonly Comparison<OwnerGroup> OwnerOrder = CompareOwners;

        /// <summary>
        /// The order the headings read in (RULED 2026-09-01): the empires first, by where the player
        /// knows their centre to be, in the same reading order everything else on this map is in - so
        /// the walk crosses the sky the way the picture does. An empire whose centre the player has no
        /// record of has nothing drawn to see and sorts after the ones that have, by the first star of
        /// its own; and the four position-less buckets come last in the ruled order, since "nobody's"
        /// and "unknown" are not places.
        /// </summary>
        private static int CompareOwners(OwnerGroup left, OwnerGroup right)
        {
            if (left.Bucket != right.Bucket)
            {
                return left.Bucket < right.Bucket ? -1 : 1;
            }

            if (left.Bucket != ScanBucket.Empire)
            {
                return 0;
            }

            if (left.Placed != right.Placed)
            {
                return left.Placed ? -1 : 1;
            }

            return ComparePositions(left.Centre, right.Centre);
        }

        /// <summary>
        /// Sort this build's stars under the empires that hold them.
        ///
        /// The lens paints every system's owner - a ring in the owner's colour, a pie where several
        /// claim it, the six-way ownership bar on the label - so whose a star is IS the picture at
        /// these bands, and the tree groups by it (RULED 2026-09-01, superseding flat reading order
        /// in-mode). The constellations are not here at all: the game hides that whole window under
        /// every lens, so a group the picture never draws would be a level of the tree the player
        /// cannot see.
        ///
        /// The two lists arrive in reading order, so appending each star to its own heading leaves
        /// every heading's members in reading order too, and only the headings need sorting.
        /// </summary>
        private void PartitionByOwner(Empire empire)
        {
            _owners.Clear();
            int claimed = 0;
            int named = 0;
            int drawn = 0;
            while (named < _systems.Count || drawn < _located.Count)
            {
                bool takeNamed =
                    drawn >= _located.Count
                    || (
                        named < _systems.Count
                        && ComparePositions(
                            _systems[named].GalaxyPosition,
                            _located[drawn].GalaxyPosition
                        ) <= 0
                    );
                StarSystemNode node = takeNamed ? _systems[named++] : _located[drawn++];
                Empire owner;
                ScanBucket bucket = OwnerOf(node, empire, out owner);
                int slot = -1;
                for (int j = 0; j < _owners.Count; j++)
                {
                    if (
                        _owners[j].Bucket == bucket
                        && (bucket != ScanBucket.Empire
                            || ReferenceEquals(_owners[j].Empire, owner))
                    )
                    {
                        slot = _owners[j].Members;
                        break;
                    }
                }

                if (slot < 0)
                {
                    if (claimed >= _ownerMembers.Count)
                    {
                        _ownerMembers.Add(new List<StarSystemNode>());
                    }

                    slot = claimed++;
                    _ownerMembers[slot].Clear();
                    OwnerGroup group = new OwnerGroup
                    {
                        Bucket = bucket,
                        Empire = owner,
                        Members = slot,
                        // Nothing of an empire whose centre is not in the watching empire's records is
                        // drawn to see, so the star its own group opens with is the only order left.
                        Centre = node.GalaxyPosition,
                    };
                    if (bucket == ScanBucket.Empire)
                    {
                        GalaxyPosition centre;
                        group.Placed = KnownCentre(owner, empire, out centre);
                        if (group.Placed)
                        {
                            group.Centre = centre;
                        }
                    }

                    _owners.Add(group);
                }

                _ownerMembers[slot].Add(node);
            }

            _owners.Sort(OwnerOrder);
        }

        /// <summary>Whose a star is, as a heading. An unexplored one is UNKNOWN rather than unowned,
        /// which is the whole reason it gets a bucket of its own; everything else follows the colony
        /// the player can see, exactly as the row's own owner word does
        /// (<see cref="SystemOwner"/>).</summary>
        private static ScanBucket OwnerOf(StarSystemNode node, Empire empire, out Empire owner)
        {
            owner = null;
            try
            {
                if (!Perceived(node, empire))
                {
                    return ScanBucket.Unexplored;
                }

                ColonizedStarSystem colony = VisibleColony(node, empire);
                Empire held = colony == null ? null : colony.Empire;
                if (held == null)
                {
                    return ScanBucket.NoOwner;
                }

                if (held is MinorEmpire)
                {
                    return ScanBucket.Minors;
                }

                if (held is PirateEmpire)
                {
                    return ScanBucket.Pirates;
                }

                owner = held;
                return ScanBucket.Empire;
            }
            catch (Exception e)
            {
                Log.Warn("scan: asking whose a system is threw: " + e);
                return ScanBucket.NoOwner;
            }
        }

        /// <summary>
        /// Where the watching empire's intelligence puts another empire - the same discovery-gated
        /// record the lens draws its centre circle and its links from
        /// (<c>DepartmentOfIntelligence.GetEmpirePosition</c>, whose <c>Known</c> flag is set only once
        /// one of that empire's colonies has been surveyed or sighted).
        ///
        /// Read rather than derived, so the ordering the player hears is the one the picture is drawn
        /// in; an empire the record does not place has no centre drawn either.
        /// </summary>
        private static bool KnownCentre(Empire owner, Empire empire, out GalaxyPosition centre)
        {
            centre = default(GalaxyPosition);
            try
            {
                DepartmentOfIntelligence intelligence =
                    empire == null ? null : empire.GetAgency<DepartmentOfIntelligence>();
                EmpirePosition position =
                    intelligence == null || owner == null
                        ? null
                        : intelligence.GetEmpirePosition(owner);
                if (position == null || !position.Known)
                {
                    return false;
                }

                centre = position.GalaxyPosition;
                return true;
            }
            catch (Exception e)
            {
                Log.Warn("scan: reading an empire's known centre threw: " + e);
                return false;
            }
        }

        /// <summary>The head of every key under one heading. A path, like every other key on this stop,
        /// so a landing inside a shut heading opens it on the way in
        /// (<c>KeyGraph.AncestorKeys</c>).</summary>
        private static string OwnerKey(ScanBucket bucket, Empire owner)
        {
            switch (bucket)
            {
                case ScanBucket.Empire:
                    return OwnerKeyHead + (owner == null ? "unknown" : owner.Index.ToString());
                case ScanBucket.Minors:
                    return OwnerKeyHead + "minors";
                case ScanBucket.Pirates:
                    return OwnerKeyHead + "pirates";
                case ScanBucket.Unexplored:
                    return OwnerKeyHead + "unexplored";
                default:
                    return OwnerKeyHead + "none";
            }
        }

        /// <summary>
        /// The headings are a level of their own and are NOT in the stars' keys.
        ///
        /// Everything under a system keeps the key it has in the ordinary view - the system's own row,
        /// its planet dots, its lanes - which is what makes the mode change cost the cursor nothing in
        /// either direction: the row the player is standing on is the same row, so it is not
        /// reconciled at all, and a row the mode really does take away (a fleet) seats on the SYSTEM
        /// that contained it through the key path, which is the ruled recovery. Re-heading every key
        /// would have made every descendant of every system a new node and left the cursor with
        /// nothing to fall back to.
        ///
        /// The cost, named: a landing sent into a heading the player has CLOSED cannot open it on the
        /// way in, because the heading is not in the target's ancestry. The headings open themselves
        /// the first time they are declared (<see cref="Seed"/>), so this is only reachable by closing
        /// one by hand.
        /// </summary>
        private const string OwnerKeyHead = "galaxy:owner/";

        /// <summary>What the heading is CALLED. Every word here is one the mod or the game already
        /// uses somewhere: an empire is named the way a system's own owner word names it
        /// (<c>GuiEmpire.GetLeaderName</c>, the header of the game's own system dossier), and the four
        /// buckets borrow the scanner's word for minor factions, the icon table's for pirates, the
        /// game's own "No owner", and the scanner's "Unexplored" - which is the wording the ruling
        /// asked for.</summary>
        private static string OwnerHeading(OwnerGroup group, Empire empire)
        {
            try
            {
                switch (group.Bucket)
                {
                    case ScanBucket.Minors:
                        return ModStrings.Get(ModStrings.GalaxyScannerSystemsMinorFactions);
                    case ScanBucket.Pirates:
                        return ModStrings.Get(ModStrings.IconPirates);
                    case ScanBucket.Unexplored:
                        return ModStrings.Get(ModStrings.GalaxyScannerUnexplored);
                    case ScanBucket.NoOwner:
                        return AgeText.Clean(Gui.Localize(NoOwnerKey));
                }

                GuiEmpire wrapper =
                    group.Empire == null
                        ? null
                        : Gui.GuiWrapperProviderService.GetGuiEmpire(group.Empire);
                return wrapper == null
                    ? null
                    : AgeText.Clean(wrapper.GetLeaderName(empire));
            }
            catch (Exception e)
            {
                Log.Warn("scan: naming an owner heading threw: " + e);
                return null;
            }
        }

        /// <summary>
        /// The map stop while a lens is up: the stars the lens is painting, grouped by whose they are.
        ///
        /// No constellations, no fleets, no probes, no missiles, no pins, no quest markers, no deposits
        /// and no docks - the game hides every one of those windows in the mode, through one gate
        /// (<c>GuiManager</c> :1555-1567), and the table says the same
        /// (<see cref="Core.UI.Bands"/>). What is left is what the lens really draws: a ring or a pie
        /// per system, its label, the planet circles inside it, and the lane network under all of it.
        ///
        /// From the two furthest-out rungs the lens draws no system at all - it draws the empires,
        /// their centres and their spokes - so the tree there holds only the player's own annotations
        /// until that band's content lands.
        /// </summary>
        private void BuildScanTree(GraphBuilder builder, Empire empire, ScanNodeLabel[] labels)
        {
            // No stretch of sky is declared in the mode, and the bookmark merge asks which ones are
            // (<see cref="InGroups"/>) - so the partition is emptied rather than left holding the last
            // ordinary build's, which would file a point bookmark inside a group nothing declares.
            _groups.Clear();
            _unexplored.Clear();
            // The headings this build files the stars under, gathered from scratch: a landing opens
            // them through it (<see cref="NoteGrouping"/>) and a stale entry would open a heading the
            // tree has stopped declaring.
            ForgetGrouping();
            GatherBookmarks(empire);
            if (!_showsSystems)
            {
                // The lens names no star at these two rungs - it draws the EMPIRES, their centres and
                // the curves tethering the watched one's colonies (<see cref="BuildEmpireList"/>).
                if (ZoomBands.Shows(BandKind.Empires))
                {
                    BuildEmpireList(builder, empire);
                }

                EmitScanBookmarks(builder);
                return;
            }

            PartitionByOwner(empire);
            for (int i = 0; i < _owners.Count; i++)
            {
                AddOwnerGroup(builder, _owners[i], empire, labels);
            }

            // Last, and under a heading of its own: a bookmark is the player's own note rather than
            // something the lens is drawing, so it belongs to no empire's holdings.
            EmitScanBookmarks(builder);
        }

        /// <summary>
        /// THE PLAYER'S OWN POINT BOOKMARKS, UNDER THEIR OWN HEADING (owner ruling 2026-09-01, the
        /// word approved 2026-09-01).
        ///
        /// The in-mode tree is otherwise a picture of what the lens is painting - empires and their
        /// holdings - and a handful of annotations loose among them would read as places the lens had
        /// drawn. So they are gathered, in position order as everywhere else, behind one word.
        ///
        /// Only POINTS. A bookmarked SYSTEM's annotation rides that system's own row, which the tree
        /// already holds wherever the lens names the stars.
        ///
        /// The heading is not in the rows' keys, for the reason the owner headings are not in the
        /// stars' (<see cref="OwnerKeyHead"/>): a bookmark keeps the key it has in the ordinary view,
        /// so the cursor rides across the mode change. It seeds open, so the group has to be shut by
        /// hand before a landing could fail to reach inside it.
        /// </summary>
        private void EmitScanBookmarks(GraphBuilder builder)
        {
            int points = 0;
            for (int i = 0; i < _bookmarkPoints.Count; i++)
            {
                if (!_bookmarkPoints[i].Emitted && _bookmarkPoints[i].Under == null)
                {
                    points++;
                }
            }

            if (points == 0)
            {
                return;
            }

            ControlId id = ControlId.Structural(ScanBookmarksKey);
            Seed(builder, id);
            bool open = builder.ExpandAll || builder.IsExpanded(id);
            NodeVtable vtable = GraphNodes.Group(
                () => ModStrings.Get(ModStrings.GalaxyBookmarksGroup)
            );
            // Synthetic: a heading over the mod's own rows - there is no widget anywhere for a
            // bookmark, let alone for a list of them.
            builder.BeginGroup(Nodes.Synthetic(id, vtable), expanded: open);
            if (open)
            {
                EmitBookmarksAfter(builder, null);
            }

            builder.EndGroup();
        }

        private const string ScanBookmarksKey = "galaxy:bookmarks";

        private void AddOwnerGroup(
            GraphBuilder builder,
            OwnerGroup group,
            Empire empire,
            ScanNodeLabel[] labels
        )
        {
            OwnerGroup it = group;
            Empire looking = empire;
            NodeVtable vtable = GraphNodes.Group(() => OwnerHeading(it, looking));
            if (group.Bucket == ScanBucket.Empire)
            {
                // The heading carries the diplomacy band's own reading (RULED 2026-09-01), so zooming
                // out of the stars into the empire list is one continuous shape rather than two
                // different things that happen to be called the same: how the player stands with them,
                // and where their centre is. Asked of the PLAYER and never of whoever the diplomacy
                // band was last pointed at - these lenses draw no diplomacy, so there is no watched
                // empire in the picture to compose against (<see cref="Watching"/>).
                Empire held = group.Empire;
                vtable.Announcements.Add(
                    GraphNodes.ValuePart(() => RelationWord(held, looking), false)
                );
                if (group.Placed)
                {
                    vtable.Announcements.Add(GalaxyCoordinates.Part(group.Centre));
                }
            }

            string key = OwnerKey(group.Bucket, group.Empire);
            ControlId id = ControlId.Structural(key);
            Seed(builder, id);
            bool open = builder.ExpandAll || builder.IsExpanded(id);
            List<StarSystemNode> members = _ownerMembers[group.Members];
            // Whether or not the heading is open: a landing has to be able to open it, and a shut one
            // is exactly the case that needs the ancestry (<see cref="NoteGrouping"/>).
            for (int i = 0; i < members.Count; i++)
            {
                _scanGrouping[SystemKey(members[i], empire)] = key;
            }

            // Synthetic: a heading the mod assembled from whose the stars are - the lens paints the
            // ownership and draws no list of it.
            builder.BeginGroup(Nodes.Synthetic(id, vtable), expanded: open);
            if (open)
            {
                for (int i = 0; i < members.Count; i++)
                {
                    AddScanSystem(builder, members[i], empire, labels);
                }
            }

            builder.EndGroup();
        }

        // ---- the heading a landing has to open on its way in ----

        /// <summary>Which heading each star is filed under, keyed by the star's own key. Rebuilt on
        /// every in-mode build and emptied on every other one, so it can only ever name headings the
        /// tree is really declaring. Static because the engine's ancestry question has no page to ask
        /// and there is exactly one map, the same reason the inspect mode's own state is.</summary>
        private static readonly Dictionary<string, string> _scanGrouping =
            new Dictionary<string, string>();

        /// <summary>
        /// THE OWNER HEADING IS A REAL ANCESTOR TO EVERY LANDING (owner ruling 2026-09-01, after
        /// playtest), although it is not in its members' keys.
        ///
        /// Measured: following a star lane to a system inside a shut "No owner" heading did nothing at
        /// all - the row the landing was aimed at is not declared, the ancestry read out of its key
        /// names a constellation the mode never declares, and so nothing was opened and nothing was
        /// said. The headings seed open, which is why only a player who had shut one by hand met it.
        ///
        /// The heading stays out of the keys - that is what lets the cursor ride the mode change
        /// (<see cref="OwnerKeyHead"/>) - so the page NAMES it instead, through the engine's one
        /// ancestry question (<c>KeyGraph.GroupingAncestor</c>). Every landing there is goes through
        /// that question, so this is one mechanism rather than a patch per caller: the lane hop, the
        /// scanner's go-to, a bookmark jump, a type-ahead result, a leap being restored and the
        /// reconciliation of a dead row all open the heading on the way in, one level per build,
        /// exactly as they open a shut constellation in the ordinary view.
        /// </summary>
        internal static object NoteGrouping(object structuralKey)
        {
            string key = structuralKey as string;
            if (key == null || !Scanning || _scanGrouping.Count == 0)
            {
                return null;
            }

            string heading;
            return _scanGrouping.TryGetValue(key, out heading) ? heading : null;
        }

        /// <summary>Given back with the page, and emptied by any build that is not the lens's own.
        /// </summary>
        internal static void ForgetGrouping()
        {
            _scanGrouping.Clear();
        }

        /// <summary>
        /// One star as a lens draws it.
        ///
        /// FOCUS IS HOVER (RULED 2026-09-01): the lens paints some of a label's content only for the
        /// systems it thinks are important (<c>ScanNodeLabel</c> :553-557), and the mouse raises any
        /// label's content by hovering it (:1035) - so the row says what hovering would give, on every
        /// lens, and is read from the MODEL rather than from whichever lines the layer happened to
        /// fade up. Without that most of the Trade band's rows would be anonymous rings.
        ///
        /// Its name is the map's own answer to whether the place has been explored: a star the picture
        /// draws "???" over is one the mod already refuses to name anywhere else, and it says the same
        /// mod word here (<c>ModStrings.GalaxySystemUnexplored</c>) rather than leaking a name the
        /// picture is withholding. Whose it is follows, in the game's own words - which is also the
        /// heading this row is standing under, said again because a row has to be readable on its own.
        ///
        /// What hangs under it is what the lens draws INSIDE the label: the planet circles at dot
        /// fidelity, and the lanes leaving the star, which the lens goes on drawing at every rung -
        /// read in the same named regions the ordinary map's system row is read in
        /// (<see cref="AddInside"/>), three of the seven, because a lens draws no fleets, no deposits
        /// and no doors. So Alt+Up/Down means the same thing on either side of the mode change.
        /// </summary>
        private void AddScanSystem(
            GraphBuilder builder,
            StarSystemNode node,
            Empire empire,
            ScanNodeLabel[] labels
        )
        {
            StarSystemNode it = node;
            Empire looking = empire;
            bool named = Perceived(node, empire);
            ScanNodeLabel label = ScanLabelFor(node, labels);
            ScanNodeLabel drawn = label;
            NodeVtable vtable = GraphNodes.Group(
                () =>
                    named
                        ? it.LocalizedName
                        : ModStrings.Get(ModStrings.GalaxySystemUnexplored)
            );
            vtable.Announcements.Add(GalaxyCoordinates.Part(node.GalaxyPosition));
            vtable.Announcements.Add(GraphNodes.ValuePart(() => SystemOwner(it, looking), false));
            vtable.Announcements.Add(GraphNodes.ValuePart(() => HomeSystemWord(it, looking), false));
            // What the lens's own lines make of the place: one line per trade route that ends here or
            // runs through (<see cref="WeaveInto"/>). Nothing at all where none does.
            WeaveInto(vtable, node, empire);
            // Last of everything, as it is on the ordinary map: the player's own note about the place.
            vtable.Announcements.Add(GraphNodes.ValuePart(() => BookmarkWord(it), false));

            // The trade dial's dossier, which is where the figures the lens draws as an ANGLE live -
            // the trading structure, the neighbours, the level and population, the improvement
            // modifier and the governor. The game hangs it on the dial and shows the dial only for a
            // colony of the player's own, so a system without one carries no section at all.
            AgeTooltip dial = TradeDial(label);
            if (dial != null)
            {
                vtable.Sections = GraphNodes.Sections(GraphNodes.TooltipSection(dial));
                AgeWidgets.PointAt(vtable, label.AgeTransform, dial);
            }
            else if (label != null)
            {
                AgeWidgets.PointAt(vtable, label.AgeTransform);
            }

            // The two clicks the map itself puts on a system are unchanged in the mode: the lens is a
            // light over the same map, and the game answers a click on a star the same way under it.
            vtable.OnActivate = () => ZoomIn(it);
            vtable.OnContextual = () => SystemCommand(it);

            string place = SystemKey(node, empire);
            ControlId id = ControlId.For(it, place);
            HashSet<ControlId> expansion = builder.Expansion;
            ControlId group = id;
            vtable.OnCollapse = () => Collapse(expansion, group, it);
            // Synthetic: a place on the map, assembled from the galaxy model rather than drawn as one
            // thing - the lens draws a ring, a label and a row of circles.
            builder.BeginGroup(Nodes.Synthetic(id, vtable));
            if (builder.IsExpanded(id))
            {
                // The same named regions the ordinary map's system row is read in, in the same fixed
                // order, so that Alt+Up/Down means the same thing either side of the mode change -
                // three of them here, because a lens draws no fleets, no deposits and no doors.
                object outer = builder.Region;
                try
                {
                    object at = Region(
                        builder,
                        place,
                        "planets",
                        ModStrings.GalaxySystemPlanetsRegion
                    );
                    try
                    {
                        AddScanPlanets(builder, place, node, empire, drawn);
                    }
                    finally
                    {
                        TooltipChildren.EndRegion(builder, at);
                    }

                    at = Region(builder, place, "lanes", ModStrings.GalaxySystemLanesRegion);
                    try
                    {
                        AddStarlanes(builder, place, node, empire, LanesOf(node, empire));
                    }
                    finally
                    {
                        TooltipChildren.EndRegion(builder, at);
                    }

                    at = Region(builder, place, "status", ModStrings.GalaxySystemStatusRegion);
                    try
                    {
                        List<TooltipChildren.Dossier> icons =
                            new List<TooltipChildren.Dossier>(2);
                        SystemLabelReadout.ScanIcons(icons, drawn);
                        TooltipChildren.EmitInto(
                            builder,
                            place,
                            icons,
                            SystemLabelReadout.Region.Status
                        );
                    }
                    finally
                    {
                        TooltipChildren.EndRegion(builder, at);
                    }
                }
                finally
                {
                    builder.SetRegion(outer);
                }
            }

            builder.EndGroup();
        }

        /// <summary>The planet circles the lens rings a star with, each read as the DOT it is (owner
        /// ruling 2026-09-01): the name, the colonisation status and the marks the circle carries, with
        /// the circle's own tooltip as the buffer - the same shape the ordinary map's planet dots have,
        /// and literally the same call (<see cref="AddPlanetDot"/>), so a world reads the same either
        /// side of the mode.</summary>
        private static void AddScanPlanets(
            GraphBuilder builder,
            string place,
            StarSystemNode node,
            Empire empire,
            ScanNodeLabel label
        )
        {
            if (
                ZoomBands.Fidelity(BandKind.Planets) == BandFidelity.None
                || !PlanetsDeclared(node, empire)
            )
            {
                return;
            }

            try
            {
                for (int i = 0; i < node.Planets.Count; i++)
                {
                    AddPlanetDot(
                        builder,
                        ControlId.For(node.Planets[i], place + "/planet/" + i),
                        node,
                        node.Planets[i],
                        empire,
                        ScanCircle(label, i)
                    );
                }
            }
            catch (Exception e)
            {
                Log.Warn("scan: reading a system's planet circles threw: " + e);
            }
        }

        /// <summary>The Nth circle in a scan label's ring of planets, or nothing where the label is not
        /// on the screen or the pool's Nth widget is a leftover the game faded out. The table is a pool
        /// that keeps the widgets a bigger system needed, so the widgets are read in the order they
        /// were bound - which is orbit order.</summary>
        private static AgeTransform ScanCircle(ScanNodeLabel label, int orbit)
        {
            try
            {
                AgeTransform table = label == null ? null : label.PlanetCirclesTable;
                // Different widget: the row stands for the PLANET and this is the circle the lens
                // draws it as, which is only there while the camera has this label in frame.
                if (table == null || !AgeWidgets.Painted(table))
                {
                    return null;
                }

                IList<AgeTransform> children = table.Children;
                AgeTransform widget = children == null || orbit >= children.Count
                    ? null
                    : children[orbit];
                PlanetCircleItem item =
                    widget == null ? null : widget.GetComponent<PlanetCircleItem>();
                // Content: which of two readings the dot gets. A pooled leftover carries the previous
                // system's tooltip, and aiming at one would review another star's world.
                return item != null && item.IsBound && AgeWidgets.Painted(widget) ? widget : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The dial's own dossier, and only while the dial is drawn: the game shows it for a
        /// system that is the player's colony and hides it for everybody else's
        /// (<c>ScanNodeLabel.RefreshTradingScore</c>).</summary>
        private static AgeTooltip TradeDial(ScanNodeLabel label)
        {
            try
            {
                StarSystemManagementScanViewItemRating rating =
                    label == null ? null : label.TradeRatingGroup;
                // Different widget: the dossier is hung on the DIAL, and a class-backed tooltip only
                // has words once the game has drawn the widget it belongs to.
                return rating == null || !AgeWidgets.Painted(rating.AgeTransform)
                    ? null
                    : label.TradeEfficiencyTooltip;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static readonly ScanNodeLabel[] NoScanLabels = new ScanNodeLabel[0];

        /// <summary>Every label the lens is drawing over the map, held for the length of ONE frame for
        /// the same reason the ordinary system labels are: the window pools them and the walk is a
        /// component search, with several callers in a frame.</summary>
        private static ScanNodeLabel[] ScanLabels()
        {
            try
            {
                int frame = UnityEngine.Time.frameCount;
                if (_scanLabelsFrame == frame && _scanLabels != null)
                {
                    return _scanLabels;
                }

                ScanNodeLabelsWindow window = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<ScanNodeLabelsWindow>(false)
                    : null;
                _scanLabels =
                    window == null
                        ? NoScanLabels
                        : window.GetComponentsInChildren<ScanNodeLabel>(true);
                _scanLabelsFrame = frame;
                return _scanLabels;
            }
            catch (Exception e)
            {
                Log.Warn("scan: finding the lens labels threw: " + e);
                return NoScanLabels;
            }
        }

        private static ScanNodeLabel[] _scanLabels;

        private static int _scanLabelsFrame = -1;

        /// <summary>The lens's own label for a star, matched by the node it was bound to - and only
        /// while the camera has it in frame, because the window culls its labels by camera position
        /// and a culled one carries whichever star it last stood over. The row exists either way: what
        /// the lens draws of a KIND is a fact about the band and not about where the camera is
        /// pointing (owner ruling 2026-09-01), so an off-screen star keeps its row and simply has no
        /// widget to hang a dossier on.</summary>
        private static ScanNodeLabel ScanLabelFor(StarSystemNode node, ScanNodeLabel[] labels)
        {
            try
            {
                for (int i = 0; i < labels.Length; i++)
                {
                    ScanNodeLabel label = labels[i];
                    // Different widget, and a dedupe of the window's pool: a label the camera has
                    // culled is still bound to whatever it last drew.
                    if (
                        label != null
                        && ReferenceEquals(label.GameNode, node)
                        && AgeWidgets.Visible(label.AgeTransform)
                    )
                    {
                        return label;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("scan: matching a system to its lens label threw: " + e);
            }

            return null;
        }
    }
}
