using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using Amplitude;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.ES2.Bookmarks;
using ES2Access.ES2.UI;
using ES2Access.UI;
using ES2Access.UI.Bookmarks;
using ES2Access.UI.Input;
using UnityEngine;

namespace ES2Access.Screens
{
    /// <summary>The spine of the map stop: which places exist this build, how they are partitioned
    /// into stretches of sky, the order everything is read in, and the bookmark rows interleaved
    /// into it.</summary>
    public sealed partial class GalaxyHudScreen
    {
        /// <summary>
        /// What the map is drawing at the distance the camera is at, taken once for the whole build.
        ///
        /// One reading per build rather than one per row: the answer is the same for every row of a
        /// build by construction - the band is a property of the camera, not of the thing being
        /// declared - and asking it per row would put two of the engine's service lookups behind every
        /// system, lane and fleet on the map.
        /// </summary>
        private bool _showsSystems;

        private bool _showsFleets;

        private bool _showsDetail;

        /// <summary>Whether the picture is drawing the things that stand out BETWEEN the stars - a
        /// probe, a missile, an ally pin, a quest marker in open space. Its own column of the band
        /// table rather than the nameplate detail gate they arrive with: under a scan lens the game
        /// hides their windows at every lens while the planet dots go on being drawn, so the two
        /// questions have different answers there (<see cref="BandKind.OpenSpace"/>).</summary>
        private bool _showsOpenSpace;

        /// <summary>The level the last build was made at, so this one can tell a step WITHIN a band
        /// from a step across one (<see cref="Core.UI.Bands.SameShape"/>). -1 before the first
        /// build.</summary>
        private int _builtAtLevel = -1;

        private bool _builtScanning;

        /// <summary>Every star the map is naming, in the order it reads - one list, colonies and
        /// everything else together (<see cref="SystemsRegion"/>).</summary>
        private readonly List<StarSystemNode> _systems = new List<StarSystemNode>();

        /// <summary>Every star the map is DRAWING and refusing to name, in the same reading order -
        /// the places the player has been told the position of and nothing else
        /// (<see cref="AddLocated"/>). Kept out of <see cref="_systems"/> rather than marked inside
        /// it: everything that list feeds says a system's real name - the type-ahead index over the
        /// closed constellations reads <c>LocalizedName</c> straight off the node - and a place the
        /// picture is not naming must not be findable by a name the picture has never shown.</summary>
        private readonly List<StarSystemNode> _located = new List<StarSystemNode>();

        /// <summary>Which of them are the empire's own, for the one word the row says about it. Kept
        /// apart from the order rather than sorted into it: whose a star is changes what its row says,
        /// never where the row sits.</summary>
        private readonly List<StarSystemNode> _colonies = new List<StarSystemNode>();

        /// <summary>The same three memberships as a SET each, filled beside the lists and never read
        /// for order. Every one of them is asked "is this star in you" inside a loop over the whole
        /// galaxy - twice while the lists are being built, once per place while the tree is, once per
        /// system while the type-ahead index is - and a list answers that by walking itself, so the
        /// build was quadratic in the number of stars on the map. The LISTS stay because reading order
        /// is what they are for; only the membership question moved.</summary>
        private readonly HashSet<StarSystemNode> _namedSet = new HashSet<StarSystemNode>();

        private readonly HashSet<StarSystemNode> _locatedSet = new HashSet<StarSystemNode>();

        private readonly HashSet<StarSystemNode> _colonySet = new HashSet<StarSystemNode>();

        /// <summary>
        /// What the galaxy answered THIS frame, before anything is put in reading order - one list per
        /// membership, filled by the gather and read by nothing but <see cref="Order"/>.
        ///
        /// The gather itself has to run every frame: what the map is naming turns on each node's own
        /// exploration and visibility, and the game bumps no generation covering both -
        /// <c>IVisibilityService.VisibilityRevision</c> counts fog-of-war reveals and the end of the
        /// visibility refresh pass (decompiled/Assembly-CSharp/VisibilityController.cs:179, :201,
        /// :234, :1041) and says nothing about a node reaching Identified or about a colony being
        /// founded. What does NOT have to run every frame is the SORT: where a star reads is decided
        /// by its position and by where home is, and neither of those moves.
        /// </summary>
        private readonly List<StarSystemNode> _gatheredColonies = new List<StarSystemNode>();

        private readonly HashSet<StarSystemNode> _gatheredColonySet = new HashSet<StarSystemNode>();

        private readonly List<StarSystemNode> _gatheredSystems = new List<StarSystemNode>();

        private readonly List<StarSystemNode> _gatheredLocated = new List<StarSystemNode>();

        /// <summary>Where home was when the lists were last put in order - part of the key because
        /// reading order is measured FROM home (<see cref="ComparePositions"/>) and rounded into rows
        /// afterwards, so a different origin is a different order even though nothing has moved.
        /// </summary>
        private float _orderedFromX;

        private float _orderedFromY;

        /// <summary>Every star this build is listing, by its GUID - what a bookmark is resolved
        /// through (<see cref="Listed"/>). Filled in the pass that puts the lists in order, because
        /// it answers the same question they do and changes exactly when they change.</summary>
        private readonly Dictionary<ulong, StarSystemNode> _listedByGuid =
            new Dictionary<ulong, StarSystemNode>();

        /// <summary>The fleets crossing open space towards somewhere the map has not named - the ones
        /// with no system to hang under (<see cref="AddAdrift"/>).</summary>
        private readonly List<Fleet> _adrift = new List<Fleet>();

        /// <summary>
        /// One bookmarked point of galaxy, as the tree has to hold it: which slot it is, where it is,
        /// and which stretch of sky it falls in.
        ///
        /// A CLASS and kept per slot rather than rebuilt per build, because the camera rule records
        /// where it sent the camera BY REFERENCE (<see cref="Showing"/>) - a fresh object every frame
        /// would be a place the record never recognises, and the camera would re-centre on every frame
        /// the cursor stood on the row.
        /// </summary>
        private sealed class BookmarkPoint
        {
            public char Digit;
            public GalaxyPosition At;

            /// <summary>The stretch of sky the point falls in, which is what its key is built from -
            /// a fact about where it is, so the row keeps one identity at every band.</summary>
            public Constellation Sky;

            /// <summary>The group this build hangs the row in, or null for the top level. The same
            /// thing as <see cref="Sky"/> wherever the map is drawing the systems inside it, and null
            /// at the two bands where it is not: a bookmark is the player's own annotation and is
            /// there at every level (owner ruling), so its row cannot be filed inside a group that
            /// stands shut with nothing in it.</summary>
            public Constellation Under;

            public ControlId Id;

            /// <summary>The stretch of sky <see cref="Id"/> was composed for, and the empire it was
            /// composed for. The key is built from those two and the slot, and the slot does not
            /// move, so an unchanged pair is an unchanged key. A sky that is named at all has been
            /// explored - <see cref="InGroups"/> only answers yes for a group
            /// <see cref="Partition"/> declared, and it declares none for an unexplored
            /// constellation - so the branch inside <c>GroupKey</c> is settled by the sky as well.
            /// </summary>
            public Constellation KeyedUnder;

            public Empire KeyedFor;

            /// <summary>Whether this build is declaring the row at all.</summary>
            public bool Listed;

            /// <summary>Whether this build has already declared it - the merge walks the same list
            /// once per group and once at the top level.</summary>
            public bool Emitted;
        }

        /// <summary>Every slot that is a point of space, kept by slot for the reason
        /// <see cref="BookmarkPoint"/> gives.</summary>
        private readonly Dictionary<char, BookmarkPoint> _bookmarkSpots =
            new Dictionary<char, BookmarkPoint>();

        /// <summary>This build's point bookmarks in reading order.</summary>
        private readonly List<BookmarkPoint> _bookmarkPoints = new List<BookmarkPoint>();

        /// <summary>The slot each bookmarked system's row says it is, for the word the row ends with.
        /// </summary>
        private readonly Dictionary<ulong, char> _bookmarkedDigit = new Dictionary<ulong, char>();

        /// <summary>The system each slot bookmarks, where this build is listing it.</summary>
        private readonly Dictionary<char, StarSystemNode> _bookmarkSystems =
            new Dictionary<char, StarSystemNode>();

        private static readonly Comparison<BookmarkPoint> BookmarkReadingOrder = CompareBookmarks;

        private static int CompareBookmarks(BookmarkPoint left, BookmarkPoint right)
        {
            return ComparePositions(left.At, right.At);
        }

        /// <summary>
        /// Work out what each filled slot is for THIS build: a word on a system's row, or a row of
        /// its own.
        ///
        /// A slot naming a system the build is listing is the first - the system's own row carries the
        /// bookmark, so an obliterated system keeps it (the game strips a system of everything and
        /// never takes its node away). A slot whose system the map is not listing at all falls back to
        /// the bare point it was set at, which is the only honest thing left to offer.
        ///
        /// Asked after the lists of places are settled and before anything is declared from them, so
        /// "is this system listed" is answered against the very build that would carry the word.
        /// </summary>
        private void GatherBookmarks(Empire empire)
        {
            _bookmarkPoints.Clear();
            _bookmarkedDigit.Clear();
            _bookmarkSystems.Clear();
            foreach (KeyValuePair<char, BookmarkPoint> parked in _bookmarkSpots)
            {
                parked.Value.Listed = false;
                parked.Value.Emitted = false;
            }

            MapBookmarks bookmarks = MapBookmarkStore.Bookmarks;
            if (bookmarks.Count == 0)
            {
                return;
            }

            foreach (char digit in MapBookmarks.Digits)
            {
                MapBookmark bookmark;
                if (!bookmarks.TryGet(digit, out bookmark))
                {
                    continue;
                }

                StarSystemNode listed = bookmark.IsSystem ? Listed(bookmark.SystemGuid) : null;
                if (listed != null)
                {
                    _bookmarkedDigit[bookmark.SystemGuid] = digit;
                    _bookmarkSystems[digit] = listed;
                    continue;
                }

                _bookmarkPoints.Add(
                    BookmarkSpot(digit, new GalaxyPosition(bookmark.X, bookmark.Y), empire)
                );
            }

            _bookmarkPoints.Sort(BookmarkReadingOrder);
        }

        /// <summary>The system a bookmark names, where this build's own lists of places hold it -
        /// which is what decides whether a bookmarked system has a row to carry the word. Read off
        /// the index those lists are put in order with, rather than scanned for down all three of
        /// them: a colony is in the named list too, and a star is in exactly one of the two lists,
        /// so one entry per GUID answers what walking colonies, then named, then located answered.
        /// </summary>
        private StarSystemNode Listed(ulong guid)
        {
            StarSystemNode node;
            return _listedByGuid.TryGetValue(guid, out node) ? node : null;
        }

        /// <summary>This slot's kept point, brought up to date for this build: where it is, which
        /// stretch of sky holds it, and therefore where its row hangs.</summary>
        private BookmarkPoint BookmarkSpot(char digit, GalaxyPosition at, Empire empire)
        {
            BookmarkPoint point;
            if (!_bookmarkSpots.TryGetValue(digit, out point))
            {
                point = new BookmarkPoint { Digit = digit };
                _bookmarkSpots[digit] = point;
            }

            double east;
            double north;
            GalaxyCoordinates.Offsets(at, out east, out north);
            Constellation sky = ConstellationMap.Classify(east, north);
            point.At = at;
            point.Sky = InGroups(sky) ? sky : null;
            // From the bands where the map draws no system, every constellation group stands shut and
            // holds nothing, so a point filed inside one would be unreachable by browsing - and a jump
            // to it would land on a row this build never declared, which is a jump that says nothing
            // (measured 2026-09-01). The row goes to the top level instead, walked into the same
            // reading order the groups themselves are in, and keeps the key it has everywhere else so
            // the cursor rides across the boundary.
            point.Under = _showsSystems ? point.Sky : null;
            point.Listed = true;
            point.Emitted = false;
            if (
                point.Id == null
                || !ReferenceEquals(point.KeyedUnder, point.Sky)
                || !ReferenceEquals(point.KeyedFor, empire)
            )
            {
                point.KeyedUnder = point.Sky;
                point.KeyedFor = empire;
                point.Id = ControlId.Structural(
                    (
                        point.Sky == null
                            ? StrayBookmarkKey
                            : GroupKey(point.Sky, empire) + "/bookmark/"
                    ) + digit
                );
            }

            return point;
        }

        /// <summary>Whether a stretch of sky is one this build is declaring a group for. A
        /// constellation whose members are all unlisted has no group, so a point that falls inside it
        /// has nowhere to hang and takes the row of its own that everything homeless on this map
        /// takes (<see cref="AddAdrift"/>).</summary>
        private bool InGroups(Constellation sky)
        {
            if (sky == null)
            {
                return false;
            }

            for (int i = 0; i < _groups.Count; i++)
            {
                if (ReferenceEquals(_groups[i].Constellation, sky))
                {
                    return true;
                }
            }

            return false;
        }

        private const string StrayBookmarkKey = "galaxy:bookmark/";

        /// <summary>The word a bookmarked system's row ends with. Last of everything the row says: it
        /// is the player's own note about the place and not a fact about it, so it comes after every
        /// answer the map itself is giving.
        ///
        /// The one composition of that word: the inspect cell says it about a bookmarked system
        /// standing inside the square too (<see cref="GalaxyInspect"/>), and a second
        /// <see cref="ModStrings.GalaxyBookmarkSuffix"/> anywhere would be a wording free to drift
        /// from this one.</summary>
        internal string BookmarkWord(StarSystemNode node)
        {
            char digit;
            return node != null && _bookmarkedDigit.TryGetValue((ulong)node.GUID, out digit)
                ? BookmarkWord(digit)
                : null;
        }

        /// <summary>The same word for a slot named on its own - what the inspect cell says about a
        /// point bookmark standing in the square, which has no row of the map's to carry it.</summary>
        internal static string BookmarkWord(char digit)
        {
            return ModStrings.Format(ModStrings.GalaxyBookmarkSuffix, digit.ToString());
        }

        /// <summary>Declare every point bookmark in this stretch of sky that reads before
        /// <paramref name="before"/> - the merge that walks them into the group's own order, the same
        /// order its systems and fleets are in.</summary>
        private void EmitBookmarksBefore(
            GraphBuilder builder,
            Constellation sky,
            GalaxyPosition before
        )
        {
            for (int i = 0; i < _bookmarkPoints.Count; i++)
            {
                BookmarkPoint point = _bookmarkPoints[i];
                if (
                    point.Emitted
                    || !ReferenceEquals(point.Under, sky)
                    || ComparePositions(point.At, before) >= 0
                )
                {
                    continue;
                }

                AddBookmarkPoint(builder, point);
            }
        }

        /// <summary>The rest of one stretch of sky's point bookmarks, after its last other entry.
        /// </summary>
        private void EmitBookmarksAfter(GraphBuilder builder, Constellation sky)
        {
            for (int i = 0; i < _bookmarkPoints.Count; i++)
            {
                BookmarkPoint point = _bookmarkPoints[i];
                if (!point.Emitted && ReferenceEquals(point.Under, sky))
                {
                    AddBookmarkPoint(builder, point);
                }
            }
        }

        /// <summary>
        /// A place the player named that the map draws nothing at.
        ///
        /// It is the mod's own row in the fullest sense - nothing on the screen answers for it, and
        /// nothing in the game's model does either - so it says the two things that are true of it:
        /// which slot it is, and where it is. The camera follows it like anything else standing out on
        /// the map (<see cref="Place"/>), and there is nothing to activate: a point of space has no
        /// click.
        /// </summary>
        private void AddBookmarkPoint(GraphBuilder builder, BookmarkPoint point)
        {
            point.Emitted = true;
            BookmarkPoint it = point;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(
                        () =>
                            ModStrings.Format(
                                ModStrings.GalaxyBookmarkPoint,
                                it.Digit.ToString(),
                                GalaxyCoordinates.Text(it.At)
                            )
                    ),
                },
            };
            // Synthetic: the mod's own row for a place the mod is the only one that knows about.
            builder.AddItem(Nodes.Synthetic(point.Id, vtable));
        }

        /// <summary>The point bookmark a row on this stop stands for, or null - the same identity
        /// lookup <see cref="OpenSpaceThing"/> makes for the things the map really does draw out
        /// there, and for the same reason: the row is keyed structurally, so nothing on the id says
        /// where it is.</summary>
        private BookmarkPoint BookmarkAt(ControlId id)
        {
            if (id == null)
            {
                return null;
            }

            for (int i = 0; i < _bookmarkPoints.Count; i++)
            {
                if (id.Equals(_bookmarkPoints[i].Id))
                {
                    return _bookmarkPoints[i];
                }
            }

            return null;
        }

        // ---- systems ----

        /// <summary>
        /// The star systems the player can see, filed under the stretches of sky the map draws names
        /// across.
        ///
        /// Which systems those are is the same question the map asks when it decides whether to draw a
        /// system's name: explored at least once, and either remembered or currently in sight. Asking
        /// it the same way is what keeps this list and the map showing the same galaxy.
        ///
        /// The map has exactly one level above a system, and the game already draws it: the
        /// CONSTELLATION, whose name floats over the region its members are scattered through. Every
        /// node in the galaxy belongs to one, so grouping by it costs nothing to derive and gives a
        /// galaxy of forty stars a walk of five entries instead of forty
        /// (<see cref="AddConstellation"/>). A constellation the player has not explored has no name
        /// drawn across it and must not be named here either, so everything in all of those shares one
        /// group with a mod-authored caption (<see cref="AddUnexplored"/>) which goes last, having no
        /// position of its own to be sorted by.
        ///
        /// One list and not two, INSIDE all that. An empire's own colonies used to be held at the
        /// front, which made the list run north to south twice and put two systems that are neighbours
        /// on the map at opposite ends of the walk; the map itself draws no such division, and whose a
        /// star is is already the first thing its own row says (owner ruling 2026-08-16). So the only
        /// division left on this stop is between what stands AT a place and what is drawn out between
        /// the stars, which is a division the picture really has.
        ///
        /// The systems are put in the order they would be READ off the map -
        /// <see cref="ReadingOrder"/> - and so are the constellation groups, by the centroid the game
        /// itself stores for each and writes its name at. So the list runs the same way twice and the
        /// same way the pairs it speaks do. Home is not held at the front either: it sits wherever its
        /// own pair puts it. A fleet crossing open space towards somewhere the map has not named is
        /// walked into that same order by its own position (<see cref="AddAdrift"/>), because it stands
        /// at no place and there is nowhere else for it to be.
        /// </summary>
        private void BuildSystems(GraphBuilder builder)
        {
            try
            {
                Empire empire = PlayerEmpire();
                if (empire == null || !GameGalaxy.Present())
                {
                    return;
                }

                ReadBand(builder);
                _gatheredColonies.Clear();
                _gatheredColonySet.Clear();
                DepartmentOfTheInterior interior = empire.GetAgency<DepartmentOfTheInterior>();
                if (interior != null)
                {
                    // Indexed rather than walked: the game hands the colonies back as a
                    // ReadOnlyCollection, whose interface enumerator is an object allocated on every
                    // frame the map is up.
                    ReadOnlyCollection<ColonizedStarSystem> owned = interior.ColonizedStarSystems;
                    for (int i = 0; i < owned.Count; i++)
                    {
                        // An empire can hold more than one thing in the same system - a colony and a
                        // ghost of it - and the system is still one place on the map.
                        StarSystemNode node = owned[i].Node;
                        if (node != null && _gatheredColonySet.Add(node))
                        {
                            _gatheredColonies.Add(node);
                        }
                    }
                }

                _gatheredSystems.Clear();
                _gatheredLocated.Clear();
                // The galaxy's own node array, read in place. `Galaxy.StarSystemNodes` is a yield
                // iterator over exactly this array making exactly this type test
                // (decompiled/Assembly-CSharp/Galaxy.cs:26-38), and the mod wrapped it in a second
                // iterator - two compiler-generated objects and two enumerators per frame, for a
                // filtered read of an array the game already holds.
                GameNode[] all = GameGalaxy.GameNodes();
                for (int i = 0; all != null && i < all.Length; i++)
                {
                    StarSystemNode node = all[i] as StarSystemNode;
                    if (node == null || _gatheredColonySet.Contains(node))
                    {
                        continue;
                    }

                    if (Perceived(node, empire))
                    {
                        _gatheredSystems.Add(node);
                    }
                    else if (MapVisibility.Located(node, empire))
                    {
                        // The map is drawing a star here and naming nothing (<see cref="AddLocated"/>).
                        _gatheredLocated.Add(node);
                    }
                }

                MarkedPlaces(empire);
                Order(GalaxyCoordinates.Origin());
                FreeMovingAdrift(_systems, _adrift);
                _adrift.Sort(FleetReadingOrder);

                if (!_showsFleets)
                {
                    _adrift.Clear();
                }

                // After the lists and after the fleets: where a pin hangs is a question about which
                // of them holds the thing it is planted on (<see cref="MarkerHomes"/>).
                MarkerHomes(empire);

                // Gathered whatever the band is: the lists are what the inspect cell reads a square's
                // contents out of as well, and the cell operates below the band the open-space ROWS
                // begin at.
                Drifting();
                // Gathered here for the same reason and with the same reach: the lines the lens draws
                // over the map are read onto the systems and lanes below
                // (<see cref="GatherTradeRoutes"/>), which both modes build, and the gather is what
                // empties them again when the mode ends.
                GatherTradeRoutes(empire);
                // Under a lens the map is the same map read by whose the stars are, so the whole of
                // the rest of this - the constellations, the open-space region, the things the lens
                // hides - is a different shape (<see cref="BuildScanTree"/>). The gathering above is
                // shared because the lists are also what the inspect cell reads a square out of.
                if (Scanning)
                {
                    BuildScanTree(builder, empire, ScanLabels());
                    return;
                }

                // The lens's own headings go with the lens: nothing in the ordinary tree hangs under
                // one, and an entry left behind would name a heading nothing is declaring
                // (<see cref="NoteGrouping"/>).
                ForgetGrouping();

                // Every probe the map is drawing: they all sit at the top of the open-space region
                // now (<see cref="AddProbes"/>), so every one of them is a reason to declare it.
                //
                // Counted off the SIGHTED sets, which are the lists the rows themselves are declared
                // from (<see cref="AddProbes"/>, <see cref="AddProjectiles"/>, <see cref="AddPins"/>)
                // - so the region exists exactly when it will hold rows, and never over an empty
                // one. The drawn-label lists are a different question: a missile the camera has
                // culled has no label and still has a row, which would have opened a region with
                // nothing in it or left rows with no region to sit in (owner ruling 2026-08-26,
                // the same unification the two lookups above got).
                // ...and declared only from the band that draws them: the map stops drawing a probe,
                // a missile and a quest pin at the same step it stops drawing the full nameplate they
                // hang beside (<see cref="_showsDetail"/>).
                int drifting = !_showsOpenSpace
                    ? 0
                    : _drifting.Count + _shots.Count + _sighted.Count + OpenSpaceMarkers(empire);
                // Declared whichever halves the map has: a lone region's jump is swallowed silently,
                // which is what the key doing nothing here should sound like, and a section that
                // appears and disappears with the fleet count is a stop that changes shape under the
                // player (owner ruling, 2026-08-18).
                builder.SetRegion(SystemsRegion);

                // Fetched once for the whole stop rather than once per system: the labels the map
                // draws are pooled by the window, not rebuilt per frame, so one walk of the label
                // window serves every system this build declares.
                StarSystemLabel[] labels = SystemLabels();
                ConstellationLabel[] regions = ConstellationLabels();
                Partition(empire);
                // After the partition, because which stretch of sky a bookmarked point hangs in
                // depends on which stretches this build is declaring at all.
                GatherBookmarks(empire);

                // Two lists already in the same order, merged as they are declared: a homeless fleet
                // takes its place among the constellations rather than being parked at either end of
                // them. A bookmarked point that falls in no stretch of sky the map is naming is
                // walked in with them, by the same rule and for the same reason.
                int sky = 0;
                int fleet = 0;
                while (sky < _groups.Count || fleet < _adrift.Count)
                {
                    bool takeSky =
                        fleet >= _adrift.Count
                        || (
                            sky < _groups.Count
                            && ComparePositions(
                                _groups[sky].Constellation.GalaxyPosition,
                                _adrift[fleet].GalaxyPosition
                            ) <= 0
                        );
                    GalaxyPosition next = takeSky
                        ? _groups[sky].Constellation.GalaxyPosition
                        : _adrift[fleet].GalaxyPosition;
                    EmitBookmarksBefore(builder, null, next);
                    if (takeSky)
                    {
                        AddConstellation(builder, _groups[sky], empire, labels, regions);
                        sky++;
                    }
                    else
                    {
                        AddAdrift(builder, _adrift[fleet]);
                        fleet++;
                    }
                }

                EmitBookmarksAfter(builder, null);
                AddUnexplored(builder, empire, labels);

                if (drifting > 0)
                {
                    builder.SetRegion(OpenSpaceRegion);
                }

                if (_showsOpenSpace)
                {
                    AddProbes(builder);
                    AddProjectiles(builder);
                    AddPins(builder);
                    // A quest pin planted on a fleet in mid-lane stands at no place at all, so it
                    // belongs here with the other things drifting between the stars rather than under
                    // whichever star happens to be nearest.
                    AddOpenSpaceMarkers(builder, empire);
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading the systems threw: " + e);
            }
        }

        /// <summary>
        /// Put this frame's gather in reading order - or leave the last frame's order standing, where
        /// it is the same set of stars read from the same home.
        ///
        /// The comparison is by MEMBERSHIP rather than by the order the gather arrived in: a count
        /// against each settled set and a lookup per gathered star. That settles set equality outright,
        /// because neither side holds a duplicate - a colony is deduped as it is taken and a node
        /// appears once in the galaxy's array - and set equality is the whole of what the sorted lists
        /// depend on, since a star does not move and the rule that orders them
        /// (<see cref="ComparePositions"/>) reads nothing else but home, which is the other half of
        /// the key. Two sorts of the whole galaxy, each comparison measuring two positions from home
        /// and rounding both into rows, is what that buys back on every frame nothing was discovered,
        /// colonized or lost - which is nearly all of them.
        ///
        /// Keeping the order is also the STABLER answer where two stars round into the same row and
        /// the same column: <c>List.Sort</c> is unstable, so re-running it over an unchanged galaxy is
        /// free to swap such a pair under the cursor.
        /// </summary>
        private void Order(GalaxyPosition home)
        {
            if (
                home.X == _orderedFromX
                && home.Y == _orderedFromY
                && _gatheredColonies.Count == _colonySet.Count
                && _gatheredColonies.Count + _gatheredSystems.Count == _namedSet.Count
                && _gatheredLocated.Count == _locatedSet.Count
                && AllIn(_gatheredColonies, _colonySet)
                && AllIn(_gatheredSystems, _namedSet)
                && AllIn(_gatheredLocated, _locatedSet)
            )
            {
                return;
            }

            _systems.Clear();
            _located.Clear();
            _colonies.Clear();
            _namedSet.Clear();
            _locatedSet.Clear();
            _colonySet.Clear();
            for (int i = 0; i < _gatheredColonies.Count; i++)
            {
                StarSystemNode node = _gatheredColonies[i];
                _colonies.Add(node);
                _colonySet.Add(node);
                _systems.Add(node);
                _namedSet.Add(node);
            }

            for (int i = 0; i < _gatheredSystems.Count; i++)
            {
                _systems.Add(_gatheredSystems[i]);
                _namedSet.Add(_gatheredSystems[i]);
            }

            for (int i = 0; i < _gatheredLocated.Count; i++)
            {
                _located.Add(_gatheredLocated[i]);
                _locatedSet.Add(_gatheredLocated[i]);
            }

            _systems.Sort(ReadingOrder);
            _located.Sort(ReadingOrder);
            _listedByGuid.Clear();
            for (int i = 0; i < _systems.Count; i++)
            {
                _listedByGuid[(ulong)_systems[i].GUID] = _systems[i];
            }

            for (int i = 0; i < _located.Count; i++)
            {
                _listedByGuid[(ulong)_located[i].GUID] = _located[i];
            }

            _orderedFromX = home.X;
            _orderedFromY = home.Y;
        }

        /// <summary>Whether the settled membership holds every star gathered. With the counts equal
        /// on both sides and neither holding a duplicate, that is set equality.</summary>
        private static bool AllIn(List<StarSystemNode> gathered, HashSet<StarSystemNode> settled)
        {
            for (int i = 0; i < gathered.Count; i++)
            {
                if (!settled.Contains(gathered[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Take this build's reading of what the map is drawing, and tell the build whether the
        /// picture has just CHANGED SHAPE.
        ///
        /// The second half is what keeps a cursor inside the tree across a zoom. A step within a band
        /// takes nothing away and a row that vanishes there is one thing going out of existence -
        /// recovered, as everywhere else in the mod, by landing on the nearest thing beside it. A step
        /// ACROSS a band takes whole families away at once, and beside a vanished fleet there is
        /// another vanished fleet: the only thing the player was reading that is still on the map is
        /// the place it was standing at, which is the row that CONTAINED it
        /// (<see cref="GraphBuilder.SeatOnContainer"/>).
        ///
        /// Never off a search build: that build is a second, invisible rendering of the same map
        /// (<see cref="GraphBuilder.ExpandAll"/>) and letting it consume the change would spend it on
        /// a render the player's cursor is not in.
        /// </summary>
        private void ReadBand(GraphBuilder builder)
        {
            _showsSystems = ZoomBands.Shows(BandKind.Systems);
            _showsFleets = ZoomBands.Shows(BandKind.Fleets);
            _showsDetail = ZoomBands.MapDetail;
            _showsOpenSpace = ZoomBands.Shows(BandKind.OpenSpace);
            if (builder.ExpandAll)
            {
                return;
            }

            int level = ZoomBands.Level;
            bool scanning = ZoomBands.Scanning;
            builder.SeatOnContainer =
                _builtAtLevel >= 0
                && level >= 0
                && (scanning != _builtScanning
                    || !Bands.SameShape(_builtAtLevel, level, scanning));
            _builtAtLevel = level;
            _builtScanning = scanning;
        }

        /// <summary>One stretch of sky the map names, and which of this build's member lists holds the
        /// systems it is showing. The members are held by INDEX into a pool of lists rather than in the
        /// struct, so the whole partition is rebuilt every frame without allocating one list per
        /// constellation per build (<see cref="Partition"/>).</summary>
        private struct SkyGroup
        {
            public Constellation Constellation;
            public int Members;
        }

        /// <summary>The constellations this build is naming, in the order they read.</summary>
        private readonly List<SkyGroup> _groups = new List<SkyGroup>();

        /// <summary>The pool <see cref="SkyGroup.Members"/> indexes: reused build after build, cleared
        /// as each is claimed, and never longer than the galaxy has constellations.</summary>
        private readonly List<List<StarSystemNode>> _members =
            new List<List<StarSystemNode>>();

        /// <summary>Everything standing in a constellation the map draws no name across, in reading
        /// order.</summary>
        private readonly List<StarSystemNode> _unexplored = new List<StarSystemNode>();

        /// <summary>
        /// Sort this build's systems into the stretches of sky they stand in.
        ///
        /// <see cref="_systems"/> and <see cref="_located"/> are each already in reading order, so
        /// walking the two of them merged and appending each place to its own group leaves every group
        /// in reading order too, and only the groups themselves need sorting.
        ///
        /// Rebuilt every frame like the rest of the stop, and allocation-free after the first galaxy:
        /// the member lists are pooled and the group list keeps its capacity across
        /// <see cref="List{T}.Clear"/>. Which constellation a node is in never changes; only whether
        /// the player has been shown its name does, and that is one array read each.
        /// </summary>
        private void Partition(Empire empire)
        {
            _groups.Clear();
            _unexplored.Clear();
            int claimed = 0;
            int named = 0;
            int drawn = 0;
            // Two lists already in the same order, merged as they are filed - the same walk the stop
            // makes over the constellations and the homeless fleets. A place the map draws without
            // naming stands in the reading order where its POSITION puts it, among the named ones,
            // because that is where a player steering by coordinates would come to it.
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
                Constellation constellation = node.Constellation;
                if (!Explored(constellation, empire))
                {
                    _unexplored.Add(node);
                    continue;
                }

                int slot = -1;
                for (int j = 0; j < _groups.Count; j++)
                {
                    if (ReferenceEquals(_groups[j].Constellation, constellation))
                    {
                        slot = _groups[j].Members;
                        break;
                    }
                }

                if (slot < 0)
                {
                    if (claimed >= _members.Count)
                    {
                        _members.Add(new List<StarSystemNode>());
                    }

                    slot = claimed++;
                    _members[slot].Clear();
                    _groups.Add(
                        new SkyGroup { Constellation = constellation, Members = slot }
                    );
                }

                _members[slot].Add(node);
            }

            _groups.Sort(ConstellationOrder);
        }

        /// <summary>
        /// The order the things on this stop are listed in: north to south by rows a unit high, and
        /// west to east along a row.
        ///
        /// Left alone the list arrives in the galaxy's own node array order, which is whatever order
        /// generation built the nodes in - it means nothing to a player, and a list they count
        /// positions along ("7 of 23") has to have a reason for the position.
        ///
        /// The rule itself is <see cref="MapCoordinates.ReadingOrder"/>, which is engine-free and
        /// unit-tested; everything here is the adapter that turns a thing on the map into the pair of
        /// offsets it speaks.
        ///
        /// Once per build of the stop rather than per frame, through delegates held here rather than
        /// method groups at the call sites, which would allocate one per sort.
        /// </summary>
        private static readonly Comparison<StarSystemNode> ReadingOrder = CompareReadingOrder;

        private static readonly Comparison<Fleet> FleetReadingOrder = CompareFleetReadingOrder;

        /// <summary>The same rule for the stretches of sky, off the centroid the game stores for each
        /// and writes its name at - so the groups read down the map the way their members do.</summary>
        private static readonly Comparison<SkyGroup> ConstellationOrder = CompareConstellationOrder;

        private static int CompareConstellationOrder(SkyGroup left, SkyGroup right)
        {
            return ComparePositions(
                left.Constellation.GalaxyPosition,
                right.Constellation.GalaxyPosition
            );
        }

        private static int CompareReadingOrder(StarSystemNode left, StarSystemNode right)
        {
            return ComparePositions(left.GalaxyPosition, right.GalaxyPosition);
        }

        private static int CompareFleetReadingOrder(Fleet left, Fleet right)
        {
            return ComparePositions(left.GalaxyPosition, right.GalaxyPosition);
        }

        private static int ComparePositions(GalaxyPosition left, GalaxyPosition right)
        {
            double leftEast,
                leftNorth,
                rightEast,
                rightNorth;
            GalaxyCoordinates.Offsets(left, out leftEast, out leftNorth);
            GalaxyCoordinates.Offsets(right, out rightEast, out rightNorth);
            return MapCoordinates.ReadingOrder(leftEast, leftNorth, rightEast, rightNorth);
        }

        /// <summary>
        /// THE LABEL WALK: which of a label window's pooled labels is the one standing for this thing.
        ///
        /// Every family on the map asks it - systems, constellations, probes, missiles, pins, hangars,
        /// the two lens layers - and each of them wrote the same loop, which is how they came to
        /// disagree about the one thing that varies. <paramref name="matches"/> is what the label was
        /// BOUND to, which differs per family and is the caller's own business.
        /// <paramref name="drawn"/> is the DRAWN-NESS POLICY, and it is stated per call rather than
        /// assumed here because the windows behave differently: some cull their labels by camera
        /// position and leave the culled ones bound to whatever star they last stood over, so a match
        /// on a culled label names the wrong thing and the caller must say so; others keep no such
        /// pool, and testing them would drop a label the map is drawing. Null means "no policy" - any
        /// bound label answers.
        ///
        /// The FIRST match wins, so a caller wanting a fallback rule (match on the reference, else on
        /// the entity's identity) calls twice with two predicates rather than nesting two loops.
        ///
        /// Never throws: a label pool read mid-rebuild is a real state, and the answer for it is that
        /// the map is drawing nothing for this thing yet.
        /// </summary>
        private static TLabel LabelFor<TLabel>(
            IList<TLabel> labels,
            Func<TLabel, bool> matches,
            Func<TLabel, bool> drawn
        )
            where TLabel : class
        {
            try
            {
                for (int i = 0; labels != null && i < labels.Count; i++)
                {
                    TLabel label = labels[i];
                    if (label != null && (drawn == null || drawn(label)) && matches(label))
                    {
                        return label;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: matching a thing to its map label threw: " + e);
            }

            return null;
        }

        /// <summary>
        /// THE LABEL WALK, made once for a whole build instead of once per row.
        ///
        /// <see cref="LabelFor{TLabel}"/> answers one thing's label by walking the window's whole
        /// pool, which made a build quadratic - every row on the map against every label the window
        /// pools - and allocated a capturing delegate per call, two of them where the first pass
        /// missed. The pool itself was already swept once per frame (<see cref="LabelSweep{T}"/>);
        /// this is the MATCH memoised on the same terms, keyed on the swept ARRAY: the sweep builds a
        /// fresh one every frame and hands the same one to every caller within a frame, so an
        /// unchanged reference is proof that nothing has been re-swept since the index was filled.
        ///
        /// The two-pass rule is kept exactly. Pass one is the label bound to this very instance; pass
        /// two, where the family has one, is a label bound to another instance carrying the same
        /// identity - what a window that rebuilt its labels from a copy leaves behind. Each map keeps
        /// the FIRST label in pool order that answers, which is what a walk stopping at its first hit
        /// returned, and the whole first pass is exhausted before the second is consulted.
        ///
        /// The DRAWN-NESS POLICY is applied as the index is filled, so a culled label never enters it
        /// - the same labels the walk skipped, at one test per label per frame rather than one per
        /// label per row. A fill that throws leaves the index empty and every lookup answers null,
        /// which is what the walk answered when it threw.
        ///
        /// Like <see cref="FrameSweep{T}"/> this holds game objects for the length of a frame and
        /// needs no teardown: it dies with the assembly it lives in.
        /// </summary>
        private sealed class LabelIndex<TLabel, TEntity>
            where TLabel : class
            where TEntity : class
        {
            private readonly string _subject;

            private readonly Func<TLabel, TEntity> _boundTo;

            /// <summary>The identity the second pass matches on, or null for a family with no second
            /// pass.</summary>
            private readonly Func<TEntity, ulong> _identity;

            /// <summary>The caller's drawn-ness policy, or null for "any bound label answers".
            /// </summary>
            private readonly Func<TLabel, bool> _drawn;

            private readonly Dictionary<TEntity, TLabel> _bound = new Dictionary<TEntity, TLabel>();

            private readonly Dictionary<ulong, TLabel> _named = new Dictionary<ulong, TLabel>();

            private TLabel[] _pool;

            public LabelIndex(
                string subject,
                Func<TLabel, TEntity> boundTo,
                Func<TEntity, ulong> identity,
                Func<TLabel, bool> drawn
            )
            {
                _subject = subject;
                _boundTo = boundTo;
                _identity = identity;
                _drawn = drawn;
            }

            /// <summary>The label standing for this thing, or null - including for a null thing, which
            /// no caller on the map asks about and which a pool holding an unbound label would
            /// otherwise answer with somebody else's widget.</summary>
            public TLabel For(TEntity entity, TLabel[] labels)
            {
                Fill(labels);
                TLabel hit;
                if (entity == null)
                {
                    return null;
                }

                if (_bound.TryGetValue(entity, out hit))
                {
                    return hit;
                }

                return _identity != null && _named.TryGetValue(_identity(entity), out hit)
                    ? hit
                    : null;
            }

            private void Fill(TLabel[] labels)
            {
                if (ReferenceEquals(_pool, labels))
                {
                    return;
                }

                _pool = labels;
                _bound.Clear();
                _named.Clear();
                try
                {
                    for (int i = 0; labels != null && i < labels.Length; i++)
                    {
                        TLabel label = labels[i];
                        if (label == null || (_drawn != null && !_drawn(label)))
                        {
                            continue;
                        }

                        TEntity entity = _boundTo(label);
                        if (entity == null || _bound.ContainsKey(entity))
                        {
                            continue;
                        }

                        _bound[entity] = label;
                        if (_identity == null)
                        {
                            continue;
                        }

                        ulong named = _identity(entity);
                        if (!_named.ContainsKey(named))
                        {
                            _named[named] = label;
                        }
                    }
                }
                catch (Exception e)
                {
                    _bound.Clear();
                    _named.Clear();
                    Log.Warn(_subject + ": matching a thing to its map label threw: " + e);
                }
            }
        }

        /// <summary>The on-map label carrying this system's tooltip - matched by the node reference
        /// the label was bound to, with the entity's own identity as a fallback for the rare case the
        /// window rebuilt its labels from a copy rather than the same instance this stop is holding.
        /// No drawn policy: this window keeps a label per system rather than a pool it re-binds, so a
        /// bound label is this system's whether or not the camera has it in frame.</summary>
        private static StarSystemLabel LabelFor(StarSystemNode node, StarSystemLabel[] labels)
        {
            return SystemLabelIndex.For(node, labels);
        }

        private static readonly LabelIndex<StarSystemLabel, StarSystemNode> SystemLabelIndex =
            new LabelIndex<StarSystemLabel, StarSystemNode>(
                "galaxy",
                LabelledSystem,
                SystemIdentity,
                null
            );

        private static StarSystemNode LabelledSystem(StarSystemLabel label)
        {
            return label.StarSystemNode;
        }

        private static ulong SystemIdentity(StarSystemNode node)
        {
            return node.GUID;
        }

        /// <summary>Every label the map is currently drawing for a system - never held ACROSS frames,
        /// because the window grows this list as the player explores more of the galaxy and a cache
        /// keyed on nothing that changes would go stale exactly when a newly-discovered system needed
        /// its tooltip. Held for the length of ONE frame, because the walk has several callers in a
        /// frame - the build, the focused row's aim, the focused dossier's name.</summary>
        private static readonly LabelSweep<StarSystemLabel> SystemLozenges =
            new LabelSweep<StarSystemLabel>("galaxy", SystemLabelsWindow);

        private static StarSystemLabel[] SystemLabels()
        {
            return SystemLozenges.Labels();
        }

        private static Component SystemLabelsWindow()
        {
            return Gui.GuiServiceAvailable
                ? Gui.GuiService.GetWindow<StarSystemLabelsWindow>(false)
                : null;
        }
    }
}
