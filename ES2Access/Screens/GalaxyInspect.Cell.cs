using System;
using System.Collections.Generic;
using Amplitude.Unity.Framework;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.Util;
using ES2Access.ES2.UI;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>What is IN the square: everything the map draws inside it, gathered once, and the
    /// sentence, the buffer lines and the fog reading made out of that.</summary>
    internal sealed partial class GalaxyInspect
    {
        // ---- what is in the cell ----

        /// <summary>The things the map is drawing inside the cell, gathered once so the reading and
        /// Enter cannot disagree about what is there.</summary>
        private sealed class Contents
        {
            public readonly List<StarSystemNode> Places = new List<StarSystemNode>();
            public readonly List<StarSystemNode> Special = new List<StarSystemNode>();
            public readonly List<Fleet> Fleets = new List<Fleet>();
            public readonly List<GalaxyHudScreen.ScannedProbe> Probes =
                new List<GalaxyHudScreen.ScannedProbe>();
            public readonly List<ObliteratorProjectile> Projectiles =
                new List<ObliteratorProjectile>();
            public readonly List<CoordinationRequest> Pins = new List<CoordinationRequest>();

            /// <summary>The quest pins standing in the cell - the ones drawn at a star and the ones
            /// planted out on a fleet crossing a lane alike, since the cell is about what the map
            /// DRAWS here and a pin is drawn wherever the thing it is bound to stands.</summary>
            public readonly List<QuestMarkers.Marker> Markers = new List<QuestMarkers.Marker>();

            /// <summary>The slots of the point bookmarks standing in the cell - places the player
            /// named that the map draws nothing at, so the only way the cell can know about one is to
            /// ask the page (<c>GalaxyHudScreen.BookmarkPointAt</c>). A bookmarked SYSTEM is not one
            /// of these: it is the star itself, which the cell already holds, wearing the same word on
            /// the end of its own reading.</summary>
            public readonly List<char> Bookmarks = new List<char>();
            public readonly List<string> Lanes = new List<string>();

            /// <summary>The lanes themselves, in the order the sentence names them - what the travel
            /// keys need (a lane's ends) and what tells one cell's lanes from another's when the skip
            /// compares two cells. Kept beside the sentences rather than instead of them: the reading
            /// is the same one lane list said out loud.</summary>
            public readonly List<Link> Links = new List<Link>();
        }

        /// <summary>
        /// The whole cell as one line: what is in it, then where it is.
        ///
        /// The contents come FIRST because they are what the player is sweeping the map FOR - the
        /// pair of numbers is the same shape on every cell and, said in front, it is a preamble to
        /// listen past on every reading of a sweep. Said last it is still on every reading, which is
        /// what the mode needs: wherever the cursor stops, the player is told where they now are,
        /// whether or not anything is there (owner's ruling 2026-08-26). An empty cell says the pair
        /// and stops; there is no word for "empty", because hearing the pair alone IS the answer and
        /// a word on every empty cell of a sweep would be most of what the sweep said.
        /// </summary>
        private string CellText(Contents contents, string fog)
        {
            MessageBuilder message = new MessageBuilder();
            List<List<string>> things = CellParts(contents, fog);
            for (int i = 0; i < things.Count; i++)
            {
                for (int j = 0; j < things[i].Count; j++)
                {
                    Item(message, things[i][j]);
                }
            }

            return message.Build();
        }

        /// <summary>
        /// ONE WALK of the cell: everything in it, thing by thing, each as the fragments its reading is
        /// made of - and in the order the cell names them.
        ///
        /// The sentence and the review buffer are the same reading twice
        /// (<see cref="CellText"/> joins every fragment of every thing into one breath;
        /// <see cref="CellLines"/> makes one line per thing). They were two walks, and two walks of the
        /// same contents is two chances to disagree about what is in the cell, what order it comes in,
        /// or which fragments a thing has - a disagreement the player meets as a buffer that does not
        /// hold what the sentence just said.
        ///
        /// The contents come FIRST because they are what the player is sweeping the map FOR - the pair
        /// of numbers is the same shape on every cell and, said in front, it is a preamble to listen
        /// past on every reading of a sweep. Said last it is still on every reading, which is what the
        /// mode needs: wherever the cursor stops, the player is told where they now are, whether or not
        /// anything is there (owner's ruling 2026-08-26). An empty cell says the pair and stops; there
        /// is no word for "empty", because hearing the pair alone IS the answer and a word on every
        /// empty cell of a sweep would be most of what the sweep said.
        /// </summary>
        private List<List<string>> CellParts(Contents contents, string fog)
        {
            List<List<string>> things = new List<List<string>>();
            for (int i = 0; i < contents.Places.Count; i++)
            {
                things.Add(PlaceParts(contents.Places[i]));
            }

            for (int i = 0; i < contents.Special.Count; i++)
            {
                things.Add(PlaceParts(contents.Special[i]));
            }

            for (int i = 0; i < contents.Fleets.Count; i++)
            {
                Fleet fleet = contents.Fleets[i];
                things.Add(
                    Parts(
                        fleet.LocalizedName,
                        FleetPhrase.Describe(fleet),
                        PairOf(fleet.GalaxyPosition)
                    )
                );
            }

            for (int i = 0; i < contents.Probes.Count; i++)
            {
                GalaxyHudScreen.ScannedProbe found = contents.Probes[i];
                // Named the way the tree's own probe node and the scanner name it - the one
                // composition all three share (<c>GalaxyHudScreen.ProbeName</c>), since the game
                // gives a probe no name of its own and a name read off a drawn mote would be gone
                // whenever the map was not drawing one.
                things.Add(
                    Parts(
                        found.Name,
                        found.Probe == null ? null : PairOf(found.Probe.GalaxyPosition)
                    )
                );
            }

            // Projectiles then pins, which is the order the tree declares them in behind the probes
            // (GalaxyHudScreen.AddProbes/AddProjectiles/AddPins) - the same three things the map
            // draws out between the stars, heard in the same order however they are reached.
            for (int i = 0; i < contents.Projectiles.Count; i++)
            {
                ObliteratorProjectile shot = contents.Projectiles[i];
                things.Add(
                    Parts(
                        ModStrings.Get(ModStrings.GalaxyObliteratorProjectile),
                        PairOf(shot.GalaxyPosition)
                    )
                );
            }

            for (int i = 0; i < contents.Pins.Count; i++)
            {
                CoordinationRequest pin = contents.Pins[i];
                things.Add(
                    Parts(GalaxyHudScreen.PinKind(pin), PairOf(pin.GalaxyPosition))
                );
            }

            for (int i = 0; i < contents.Markers.Count; i++)
            {
                things.Add(
                    Parts(
                        QuestMarkers.Name(contents.Markers[i]),
                        PairOf(contents.Markers[i].At)
                    )
                );
            }

            // Last of the things standing in the square and ahead of the lanes crossing it, which is
            // where a thing with no picture belongs: everything above is something the map draws, and
            // a bookmark is the player's own note about this stretch of sky. It says which slot and no
            // more - a bookmark has no name of its own, and where it stands is the square the cell is
            // already about to name (<see cref="BookmarkWord"/>).
            for (int i = 0; i < contents.Bookmarks.Count; i++)
            {
                things.Add(Parts(BookmarkWord(contents.Bookmarks[i])));
            }

            for (int i = 0; i < contents.Lanes.Count; i++)
            {
                things.Add(Parts(contents.Lanes[i]));
            }

            things.Add(Parts(fog));
            things.Add(Parts(MapCoordinates.Text(_x, _y, 0.0, 0.0)));
            return things;
        }

        /// <summary>One thing's fragments, in the order they are said.</summary>
        private static List<string> Parts(params string[] parts)
        {
            return new List<string>(parts);
        }

        /// <summary>One item of the cell's sentence: comma-separated from whatever was said before it,
        /// and plain where it is the first thing said.
        ///
        /// <see cref="MessageBuilder.ListItemForcedComma"/> writes its separator unconditionally - which
        /// is what the cell wants BETWEEN items, and a stray leading comma at the head of one. Nothing
        /// in a cell is guaranteed to be there, so which item is the first is not known until it is
        /// reached: an empty cell reaches only the coordinates, which must read as the bare pair.
        /// </summary>
        private static void Item(MessageBuilder message, string text)
        {
            if (message.IsEmpty)
            {
                message.Fragment(text);
            }
            else
            {
                message.ListItemForcedComma(text);
            }
        }

        /// <summary>
        /// The same cell, as the lines the player REVIEWS it by - one per thing in it, in the order
        /// the sentence names them.
        ///
        /// The sentence is one breath and says everything; the buffer is for going back over it a
        /// thing at a time, which is the whole point of a cell that can hold a dozen. So the split is
        /// per THING and not per fragment: a fleet's name and where it stands are one line, because
        /// they are one answer.
        /// </summary>
        private List<string> CellLines(Contents contents, string fog)
        {
            List<string> lines = new List<string>();
            List<List<string>> things = CellParts(contents, fog);
            for (int i = 0; i < things.Count; i++)
            {
                MessageBuilder line = new MessageBuilder();
                List<string> parts = things[i];
                for (int j = 0; j < parts.Count; j++)
                {
                    Item(line, parts[j]);
                }

                Line(lines, line.Build());
            }

            return lines;
        }

        private static void Line(List<string> lines, string said)
        {
            if (!string.IsNullOrEmpty(said))
            {
                lines.Add(said);
            }
        }

        /// <summary>One place in the cell: its name, where it stands, and - where the map has drawn
        /// something other than a star system - what it is. The same things the tree's own system node
        /// says, in the same order.</summary>
        private List<string> PlaceParts(StarSystemNode node)
        {
            // The bookmark is LAST of what this place says, exactly as it is last of what the place's
            // own tree row says, and written by that row's own composition
            // (<see cref="BookmarkWord"/>).
            return Parts(
                node.LocalizedName,
                PairOf(node.GalaxyPosition),
                GalaxyHudScreen.SpecialKind(node),
                _screen.BookmarkWord(node)
            );
        }

        /// <summary>The word a bookmark is said with in a cell - the map tree's own word for a slot,
        /// composed where the tree composes it, so the two surfaces cannot drift apart.</summary>
        private static string BookmarkWord(char digit)
        {
            return GalaxyHudScreen.BookmarkWord(digit);
        }

        /// <summary>Where a thing in the cell stands, said only where it is not simply the cell's own
        /// centre. At the smallest cursor every reading would otherwise say the same pair twice.
        /// </summary>
        private string PairOf(GalaxyPosition position)
        {
            double east;
            double north;
            GalaxyCoordinates.Offsets(position, out east, out north);
            int x = MapCoordinates.Round(east);
            int y = MapCoordinates.Round(north);
            return x == _x && y == _y ? null : MapCoordinates.Text(east, north, 0.0, 0.0);
        }

        /// <summary>
        /// Everything the map draws inside the cell, at this distance.
        ///
        /// Nothing here decides for itself what may be seen: the places are the ones the map NAMES
        /// (<see cref="MapVisibility.Perceived"/>, the label window's own gate), the fleets are the
        /// ones the fleet labels draw (<see cref="FleetPresence.Drawing"/>), the probes, obliterator
        /// missiles and ally pins are the labels the page is already holding - the very lists the
        /// tree declares its own nodes from - and a lane is offered exactly where the map paints the
        /// line (<see cref="MapVisibility.Drawn"/>). A cell that reported something the map is hiding
        /// would be handing the player the galaxy's own model instead of its picture.
        ///
        /// AND AT THIS DISTANCE (owner ruling 2026-09-01): the same band table the tree filters its
        /// rows with decides which KINDS the cell may hold, out of the same vocabulary
        /// (<see cref="BandKind"/>) - so a square cannot name a fleet at a rung the tree has no fleet
        /// row at, and the map is one picture read two ways rather than two pictures. Filtered HERE,
        /// at the one gathering, so that the reading, Enter, the skip's comparison of two cells and
        /// the travel keys are all looking at the same square: a filter applied at the sentence would
        /// leave the keys travelling by lanes nobody was told about.
        ///
        /// Two kinds are EXEMPT from that filter. SYSTEMS are held at every rung, though the picture
        /// stops naming them at the two furthest out, because the survey was ruled to name the known
        /// systems a square contains - that is the deviation the survey IS. And the player's own point
        /// BOOKMARKS are held at every rung too (owner ruling 2026-09-02, reversing the line of
        /// 2026-09-01 that silenced them under the survey): a bookmark is an annotation and not a
        /// rendering, so no band can stop drawing it, and it has a row at every rung of the tree for
        /// exactly that reason. The two halves of "this square is bookmarked" had come apart - a
        /// bookmarked SYSTEM kept its word at every rung, because that word rides the place's own
        /// reading, while a bookmarked POINT went silent at 1-2 - and a square that says nothing about
        /// the mark the player put on it is the survey's own answer being wrong about the survey's own
        /// subject: at those two rungs a point bookmark is the only thing in a square of empty sky
        /// there is to say.
        /// </summary>
        private Contents Read()
        {
            Contents contents = new Contents();
            try
            {
                Empire empire = Gui.PlayerEmpire;
                if (empire == null || !GameGalaxy.Present())
                {
                    return contents;
                }

                // One read of the table per gathering, in the tree's own vocabulary: the lozenges
                // from level 5, everything the picture only draws beside a full nameplate - probes,
                // missiles, ally pins, quest pins - from level 7 and under NO scan lens at all
                // (<see cref="BandKind.OpenSpace"/>, the tree's own gate for the same rows), the lines
                // from level 3.
                bool showsFleets = ZoomBands.Shows(BandKind.Fleets);
                bool showsDetail = ZoomBands.Shows(BandKind.OpenSpace);
                bool showsLanes = ZoomBands.Shows(BandKind.Lanes);

                List<StarSystemNode> perceived = new List<StarSystemNode>();
                foreach (StarSystemNode node in GameGalaxy.StarSystemNodes())
                {
                    if (!MapVisibility.Perceived(node, empire))
                    {
                        continue;
                    }

                    perceived.Add(node);
                    if (!Holds(node.GalaxyPosition))
                    {
                        continue;
                    }

                    if (node is SpecialNode)
                    {
                        contents.Special.Add(node);
                    }
                    else
                    {
                        contents.Places.Add(node);
                    }
                }

                IList<Fleet> fleets = showsFleets ? FleetPresence.Drawing() : NoFleetsHere;
                for (int i = 0; i < fleets.Count; i++)
                {
                    if (Holds(fleets[i].GalaxyPosition))
                    {
                        contents.Fleets.Add(fleets[i]);
                    }
                }

                // The SIGHTED probes, and named by the very call the scanner names them with
                // (owner ruling 2026-08-26): the cell used to read the DRAWN LABELS and take the
                // words off the dossier hanging on one, so a probe whose mote the map was not
                // drawing was missing from its own square, and its name was a second composition
                // that could drift from the one the tree and the scanner say. One list, one name.
                if (showsDetail)
                {
                    IList<GalaxyHudScreen.ScannedProbe> probes = _screen.ScannedProbes();
                    for (int i = 0; i < probes.Count; i++)
                    {
                        Probe probe = probes[i].Probe;
                        if (probe != null && Holds(probe.GalaxyPosition))
                        {
                            contents.Probes.Add(probes[i]);
                        }
                    }

                    IList<GalaxyHudScreen.SightedShot> shots = _screen.SightedProjectiles;
                    for (int i = 0; i < shots.Count; i++)
                    {
                        ObliteratorProjectile shot = shots[i].Shot;
                        if (Holds(shot.GalaxyPosition))
                        {
                            contents.Projectiles.Add(shot);
                        }
                    }

                    IList<GalaxyHudScreen.SightedPin> pins = _screen.SightedPins;
                    for (int i = 0; i < pins.Count; i++)
                    {
                        CoordinationRequest pin = pins[i].Request;
                        if (Holds(pin.GalaxyPosition))
                        {
                            contents.Pins.Add(pin);
                        }
                    }

                    List<QuestMarkers.Marker> markers = QuestMarkers.Of(empire);
                    for (int i = 0; i < markers.Count; i++)
                    {
                        if (Holds(markers[i].At))
                        {
                            contents.Markers.Add(markers[i]);
                        }
                    }
                }

                // No band gate: a bookmark is the player's own annotation and is held at every rung.
                char digit;
                GalaxyPosition spot;
                for (int i = 0; _screen.BookmarkPointAt(i, out digit, out spot); i++)
                {
                    if (Holds(spot))
                    {
                        contents.Bookmarks.Add(digit);
                    }
                }

                if (showsLanes)
                {
                    Lanes(contents, perceived, empire);
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading what is in the inspect cursor threw: " + e);
            }

            return contents;
        }

        /// <summary>What the drawing fleets are at a band that draws none.</summary>
        private static readonly Fleet[] NoFleetsHere = new Fleet[0];

        /// <summary>Whether a point on the map is inside the cell, measured from home like everything
        /// else the player is told.</summary>
        private bool Holds(GalaxyPosition position)
        {
            double east;
            double north;
            GalaxyCoordinates.Offsets(position, out east, out north);
            return InspectGrid.Holds(_x, _y, _size, east, north);
        }

        /// <summary>
        /// The starlanes that CROSS the cell - which is a different question from the lanes leaving a
        /// system in it, since a cell of empty space between two stars holds the line and neither end
        /// of it.
        ///
        /// Each lane is named once however many of its ends are in the cell, and named the same way
        /// round every time (westmost end first - <see cref="InspectGrid.WestmostFirst"/>), so the same
        /// lane heard from two neighbouring cells is heard as one lane. A lane whose far end the map
        /// has not named says which way it runs instead, exactly as that lane's own node does under the
        /// system it leaves.
        ///
        /// Two gates, not one, and the second is what the cell needs beyond what the tree needs.
        /// <see cref="MapVisibility.Drawn"/> answers for the LINK - is this line lit at all - and the
        /// tree, which hangs a lane under the system it leaves, needs no more than that. A cell is a
        /// PLACE, and a lit line is not lit along the whole of its length
        /// (<see cref="Lit"/>): the geometry runs end to end whatever anyone has explored, and the fog
        /// cuts it short.
        /// </summary>
        private void Lanes(Contents contents, List<StarSystemNode> perceived, Empire empire)
        {
            HashSet<GameEntityGUID> seen = new HashSet<GameEntityGUID>();
            for (int i = 0; i < perceived.Count; i++)
            {
                StarSystemNode node = perceived[i];
                List<Link> links = node.Links;
                for (int j = 0; j < links.Count; j++)
                {
                    Link link = links[j];
                    bool wormhole = link is WormholeLink;
                    if (wormhole && !empire.HasWormholeTechnology)
                    {
                        continue;
                    }

                    if (!MapVisibility.Drawn(link, empire) || seen.Contains(link.GUID))
                    {
                        continue;
                    }

                    double eastOne;
                    double northOne;
                    double eastTwo;
                    double northTwo;
                    GalaxyCoordinates.Offsets(
                        link.ExtremityNode1.GalaxyPosition,
                        out eastOne,
                        out northOne
                    );
                    GalaxyCoordinates.Offsets(
                        link.ExtremityNode2.GalaxyPosition,
                        out eastTwo,
                        out northTwo
                    );
                    if (
                        !InspectGrid.Crosses(
                            _x,
                            _y,
                            _size,
                            eastOne,
                            northOne,
                            eastTwo,
                            northTwo
                        )
                    )
                    {
                        continue;
                    }

                    seen.Add(link.GUID);
                    if (!Lit(eastOne, northOne, eastTwo, northTwo))
                    {
                        continue;
                    }

                    string said = LaneText(link, empire, wormhole);
                    if (said != null)
                    {
                        contents.Lanes.Add(said);
                        contents.Links.Add(link);
                    }
                }
            }
        }

        /// <summary>
        /// Whether the map is really painting this lane INSIDE the cell.
        ///
        /// <see cref="MapVisibility.Drawn"/> is the link's half of the question and stops there: the
        /// line is built between the two extremity positions the moment the link is created
        /// (<c>GalaxyWarplink.Ignite</c>) and the intensity its exploration state buys is applied to
        /// the whole of it in one colour (<c>GalaxyLink.Refresh</c> passes the SAME state for both
        /// ends). What cuts a lit line short is the FOG, which is not a property of the line at all: it
        /// is a shader over the world - <c>FOWRendererService</c> publishes the empire's own distance
        /// field as a global texture the map's materials sample - so a lane running into unexplored
        /// space is drawn as a stub out of the end the player can see and fades to nothing where the
        /// fog begins. Measured on the fixture: the Xiu-to-Procyon lane draws nothing at all at
        /// (-2, -9) while the cell there named it (owner-reported, 2026-08-20).
        ///
        /// This is the one content class the cell reads whose gate does not settle WHERE the thing is
        /// drawn. Everything else the cell names is a LABEL, and a label window decides for itself
        /// whether to draw one at all; a lane is world geometry, and the fog is drawn over it.
        ///
        /// Asked at the same granularity the cell's own fog count is asked at
        /// (<see cref="Fogged"/>) - the whole-unit squares of the cell, sampled at their centres - so a
        /// cell that tells the player it is "Unexplored" can never also name a lane crossing it. At the
        /// default one-by-one cursor that is a single lookup.
        /// </summary>
        private bool Lit(double eastOne, double northOne, double eastTwo, double northTwo)
        {
            try
            {
                IVisibilityService visibility = Services.GetService<IVisibilityService>();
                Empire empire = Gui.PlayerEmpire;
                if (visibility == null || empire == null)
                {
                    // The same way out <see cref="Fogged"/> takes: with no fog field to ask, the map
                    // is not being described as foggier than it is.
                    return true;
                }

                GalaxyPosition origin = GalaxyCoordinates.Origin();
                int half = InspectGrid.HalfWidth(_size);
                for (int east = -half; east <= half; east++)
                {
                    for (int north = -half; north <= half; north++)
                    {
                        int x = _x + east;
                        int y = _y + north;
                        if (
                            !InspectGrid.Crosses(
                                x,
                                y,
                                1,
                                eastOne,
                                northOne,
                                eastTwo,
                                northTwo
                            )
                        )
                        {
                            continue;
                        }

                        if (
                            visibility.IsExplored(
                                empire,
                                new GalaxyPosition(origin.X + x, origin.Y + y)
                            )
                        )
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
            catch (Exception e)
            {
                Log.Warn(
                    "galaxy: asking whether the map paints a lane in the inspect cursor threw: " + e
                );
                return true;
            }
        }

        /// <summary>One lane as the sentence the cell says it in.</summary>
        private static string LaneText(Link link, Empire empire, bool wormhole)
        {
            GameNode one = link.ExtremityNode1;
            GameNode two = link.ExtremityNode2;
            bool namedOne = MapVisibility.Perceived(one, empire);
            bool namedTwo = MapVisibility.Perceived(two, empire);
            if (namedOne && namedTwo)
            {
                bool oneFirst = InspectGrid.WestmostFirst(
                    one.GalaxyPosition.X,
                    one.GalaxyPosition.Y,
                    two.GalaxyPosition.X,
                    two.GalaxyPosition.Y
                );
                GameNode west = oneFirst ? one : two;
                GameNode east = oneFirst ? two : one;
                return ModStrings.Format(
                    wormhole ? ModStrings.GalaxyInspectWormhole : ModStrings.GalaxyInspectStarlane,
                    west.LocalizedName,
                    east.LocalizedName
                );
            }

            GameNode known = namedOne ? one : namedTwo ? two : null;
            GameNode dark = namedOne ? two : one;
            if (known == null)
            {
                return null;
            }

            return ModStrings.Format(
                wormhole
                    ? ModStrings.GalaxyInspectWormholeUnexplored
                    : ModStrings.GalaxyInspectStarlaneUnexplored,
                known.LocalizedName,
                CompassDirections.Direction(
                    dark.GalaxyPosition.X - known.GalaxyPosition.X,
                    dark.GalaxyPosition.Y - known.GalaxyPosition.Y
                )
            );
        }

        // ---- the fog ----

        /// <summary>
        /// How much of the cell the map has not lifted the fog from, said as the last thing in the
        /// reading.
        ///
        /// The fog is the one thing on this map that a sighted player reads from an ABSENCE - a grey
        /// wash over a region with nothing in it - and a reader of a list of contents would hear the
        /// same silence for "nothing there" and for "nobody has ever been there", which are opposite
        /// answers to the question the mode is for. So a cell wholly under it says so, and a cell
        /// partly under it says how many of its whole-unit squares are, which is what tells the player
        /// to shrink the cursor and find the edge of what they can see.
        ///
        /// The game answers it per POINT rather than per place (<c>IVisibilityService.IsExplored</c>
        /// samples the empire's fog-of-war distance field, the very field the fog is drawn from), so
        /// the cell is sampled at the centre of each of its squares - at most 121 lookups into a byte
        /// array, and only on the frame the cursor moved.
        /// </summary>
        private string FogText()
        {
            int fogged = Fogged();
            if (fogged <= 0)
            {
                return null;
            }

            return fogged >= InspectGrid.Squares(_size)
                ? ModStrings.Get(ModStrings.GalaxyInspectFog)
                : ModStrings.Plural(
                    ModStrings.GalaxyInspectFogOne,
                    ModStrings.GalaxyInspectFogMany,
                    fogged
                );
        }

        private int Fogged()
        {
            try
            {
                IVisibilityService visibility = Services.GetService<IVisibilityService>();
                Empire empire = Gui.PlayerEmpire;
                if (visibility == null || empire == null)
                {
                    return 0;
                }

                GalaxyPosition origin = GalaxyCoordinates.Origin();
                int half = InspectGrid.HalfWidth(_size);
                int fogged = 0;
                for (int east = -half; east <= half; east++)
                {
                    for (int north = -half; north <= half; north++)
                    {
                        GalaxyPosition at = new GalaxyPosition(
                            origin.X + _x + east,
                            origin.Y + _y + north
                        );
                        if (!visibility.IsExplored(empire, at))
                        {
                            fogged++;
                        }
                    }
                }

                return fogged;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: sampling the fog inside the inspect cursor threw: " + e);
                return 0;
            }
        }
    }
}
