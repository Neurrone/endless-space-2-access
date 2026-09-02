using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.ES2.UI;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>Moving the square about the map: a step, a skip to the next cell that holds
    /// something, and the lane the cell sits on travelled to either of its ends.</summary>
    internal sealed partial class GalaxyInspect
    {
        private bool Move(int east, int north)
        {
            int x = InspectGrid.Step(_x, _size, east);
            int y = InspectGrid.Step(_y, _size, north);
            if (!InspectGrid.InBounds(x, y, _size, _lowX, _highX, _lowY, _highY))
            {
                Voice.Say(ModStrings.Get(ModStrings.GalaxyInspectEdge), true);
                return true;
            }

            _x = x;
            _y = y;
            Settle(true);
            return true;
        }

        /// <summary>
        /// GO TO THE NEXT INTERESTING CELL in one press - the modified arrow.
        ///
        /// A sweep of this map is mostly empty: between two stars there can be twenty cells with
        /// nothing in them, each of which the plain arrow reads out as its own pair. So the modified
        /// arrow walks the same cells the plain one would, in the same steps, and stops at the first
        /// one that is not what the player is standing on (<see cref="CellSkip"/>) - which makes it
        /// "take me to the next thing over there", the question a sighted player answers by looking
        /// along the row.
        ///
        /// The number of cells passed over is said first, and only where there were any: hearing "12"
        /// is how the player knows the next thing is a long way off rather than next door, and a
        /// "skipped 0" on every ordinary step would be a word on most presses. The landing itself is
        /// an arrow key's - camera, square, and the cell read out - queued behind that line so both
        /// are heard whole.
        ///
        /// The walk re-reads every cell it passes, which is the cell scan run once per candidate. That
        /// is a keypress's cost and never a frame's, and it is what keeps the skip and the arrows
        /// telling the same story about what is in a cell.
        /// </summary>
        private bool Skip(int east, int north)
        {
            int x;
            int y;
            int skipped;
            if (
                !CellSkip.Find(
                    _x,
                    _y,
                    _size,
                    east,
                    north,
                    CellInBounds,
                    SignatureAt,
                    out x,
                    out y,
                    out skipped
                )
            )
            {
                // Not one step possible - the same answer the plain arrow gives in this position.
                Voice.Say(ModStrings.Get(ModStrings.GalaxyInspectEdge), true);
                return true;
            }

            _x = x;
            _y = y;
            if (skipped <= 0)
            {
                Settle(true);
                return true;
            }

            Voice.Say(
                ModStrings.Plural(
                    ModStrings.GalaxyInspectSkippedOne,
                    ModStrings.GalaxyInspectSkippedMany,
                    skipped
                ),
                true
            );
            Settle(false);
            return true;
        }

        private bool CellInBounds(int x, int y)
        {
            return InspectGrid.InBounds(x, y, _size, _lowX, _highX, _lowY, _highY);
        }

        /// <summary>
        /// What a cell IS, for the purpose of deciding whether the cursor should stop in it: the
        /// identity of everything its reading would name, and how much of it is fogged.
        ///
        /// Read through the mode's own cell reading rather than from a second walk of the galaxy, so
        /// that the skip can never disagree with the sentence the player hears when it lands: the
        /// cursor is moved to the candidate, the cell is read, and the cursor is put back.
        /// </summary>
        private CellSignature SignatureAt(int x, int y)
        {
            int wasX = _x;
            int wasY = _y;
            _x = x;
            _y = y;
            try
            {
                Contents contents = Read();
                List<string> things = new List<string>();
                Identify(things, "place", contents.Places);
                Identify(things, "place", contents.Special);
                Identify(things, "fleet", contents.Fleets);
                for (int i = 0; i < contents.Probes.Count; i++)
                {
                    Probe probe = contents.Probes[i].Probe;
                    if (probe != null)
                    {
                        things.Add("probe:" + probe.GUID);
                    }
                }

                Identify(things, "shot", contents.Projectiles);
                Identify(things, "pin", contents.Pins);
                for (int i = 0; i < contents.Markers.Count; i++)
                {
                    things.Add("marker:" + contents.Markers[i].Pin.GUID);
                }

                for (int i = 0; i < contents.Links.Count; i++)
                {
                    things.Add("lane:" + contents.Links[i].GUID);
                }

                // A place the player NAMED is part of what the square is, exactly as a star standing
                // in it is: the skip is "take me to the next thing over there", and a bookmark is a
                // thing the player put over there on purpose. Both kinds count - the point with
                // nothing at it, and the word a bookmarked star's reading ends with - because cell
                // identity is the identity of everything the reading names.
                for (int i = 0; i < contents.Bookmarks.Count; i++)
                {
                    things.Add("bookmark:" + contents.Bookmarks[i]);
                }

                Bookmarked(things, contents.Places);
                Bookmarked(things, contents.Special);

                // Whose influence covers the cell is part of what the cell IS, exactly as the star
                // standing in it is: a border is a thing a sighted player steers by, and a skip that
                // ran straight through one would carry the cursor from deep inside an empire to deep
                // inside the next without a word.
                CellNow().Reading.Tokens(things);
                return new CellSignature(things, Fog());
            }
            finally
            {
                _x = wasX;
                _y = wasY;
            }
        }

        /// <summary>The bookmark word each of these places wears, as identity - nothing at all for the
        /// places nobody has named.</summary>
        private void Bookmarked(List<string> things, List<StarSystemNode> places)
        {
            for (int i = 0; i < places.Count; i++)
            {
                string word = _screen.BookmarkWord(places[i]);
                if (word != null)
                {
                    things.Add("bookmark:" + word);
                }
            }
        }

        private static void Identify<T>(List<string> things, string kind, List<T> found)
            where T : IGameEntity
        {
            for (int i = 0; i < found.Count; i++)
            {
                things.Add(kind + ":" + found[i].GUID);
            }
        }

        /// <summary>Which of the three the cell is - the same sampling the reading says out loud,
        /// bucketed. A COUNT here would stop the cursor on every cell along the edge of the explored
        /// map, since the number of fogged squares changes by one all the way down it.</summary>
        private CellFog Fog()
        {
            int fogged = Fogged();
            if (fogged <= 0)
            {
                return CellFog.Clear;
            }

            return fogged >= InspectGrid.Squares(_size) ? CellFog.Wholly : CellFog.Partly;
        }

        // ---- travelling by what the cell holds ----

        /// <summary>
        /// TRAVEL WEST ALONG THE ONE LANE HERE - the modified left arrow.
        ///
        /// A lane is the map's own long-distance geometry, and a cell sitting on one between two stars
        /// is exactly where a player wants to ask "where does this go". So the key takes the cursor to
        /// the lane's westmost end, which is the end the cell's own sentence names FIRST - the player
        /// has just heard "Star lane from Dusay to Heka", and this is the key that goes to Dusay.
        ///
        /// It acts whenever there is no ambiguity and refuses silently otherwise: exactly one lane in
        /// the cell and it travels, anything else and the key is taken and nothing happens. Fleets in
        /// the cell make no difference to it, because a fleet has no exposed origin at all - the map
        /// draws where a fleet is going and never where it came from - so there is nothing here for
        /// the westward key to compete with.
        /// </summary>
        private bool FollowWest()
        {
            Contents contents = Read();
            if (contents.Links.Count == 1)
            {
                GoTo(LaneEnd(contents.Links[0], true));
            }

            return true;
        }

        /// <summary>
        /// TRAVEL TO WHERE THINGS HERE ARE GOING - the modified right arrow.
        ///
        /// A fleet under way wins over the lane it is riding, because a fleet is the thing the player
        /// is following and the lane is only the road: the cursor goes to the node the fleet's leg is
        /// flying to, which is the very thing the tree files that fleet under
        /// (<see cref="GalaxyHudScreen.DestinationOf"/>) and so is drawn map knowledge rather than the
        /// simulation's own plan. NOTHING is read out of a fleet's route beyond it - a foreign fleet's
        /// orders are not the player's to see, and a fleet whose destination the map does not name
        /// contributes nothing and blocks nothing.
        ///
        /// Several fleets agreeing on one place is still no ambiguity, so they travel too; fleets
        /// heading for different places are, so the key is taken and nothing happens. With no fleet
        /// destination to be had the key falls back to the lane, the mirror of the westward one: the
        /// eastmost end, the one the cell's sentence names second.
        /// </summary>
        private bool FollowEast()
        {
            Contents contents = Read();
            GameNode goal = null;
            for (int i = 0; i < contents.Fleets.Count; i++)
            {
                GameNode heading = GalaxyHudScreen.DestinationOf(contents.Fleets[i]);
                if (heading == null || !MapVisibility.Perceived(heading, Gui.PlayerEmpire))
                {
                    continue;
                }

                if (goal == null)
                {
                    goal = heading;
                }
                else if (!ReferenceEquals(goal, heading))
                {
                    // Two fleets bound for two places: there is no one answer, so there is no move.
                    return true;
                }
            }

            if (goal != null)
            {
                GoTo(goal);
            }
            else if (contents.Links.Count == 1)
            {
                GoTo(LaneEnd(contents.Links[0], false));
            }

            return true;
        }

        /// <summary>
        /// Which end of a lane the two travel keys mean, in the order the cell's own sentence names
        /// them (<see cref="LaneText"/>): the westmost end first, and where two ends stand at the same
        /// longitude the southern one.
        ///
        /// A lane with only one end named says only that end - "Star lane from Dusay going north
        /// east" - so it has a first end and no second one: the westward key travels to the named end
        /// and the eastward key has nowhere to go. Somewhere the player has never seen is not a place
        /// the cursor may be sent to.
        /// </summary>
        private static GameNode LaneEnd(Link link, bool west)
        {
            Empire empire = Gui.PlayerEmpire;
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
                return west == oneFirst ? one : two;
            }

            return west ? (namedOne ? one : namedTwo ? two : null) : null;
        }

        /// <summary>Send the cursor to a place named by the cell's own contents - the scanner's own
        /// landing, on the ROUNDED pair the player is told, which is the pair that puts the thing
        /// inside even the one-unit cursor. Nowhere to go is the silent refusal every one of these
        /// keys makes, and the mode stays up through all of them.</summary>
        private void GoTo(GameNode node)
        {
            if (node == null)
            {
                return;
            }

            // A travel key is a LEAP and not a step - it crosses whatever is between here and the far
            // end of a lane in one press - so the square being left is remembered, and Backspace comes
            // back to it (<see cref="_leaps"/>). After the refusal test above: a key that went nowhere
            // has nothing to come back from.
            PushCell();

            double east;
            double north;
            GalaxyCoordinates.Offsets(node.GalaxyPosition, out east, out north);
            JumpTo(MapCoordinates.Round(east), MapCoordinates.Round(north));
        }

        /// <summary>Grow or shrink the cell. A size that is not a CHANGE says nothing at all - the two
        /// ends of the ladder are where this happens, and a reading that repeated the size the player
        /// is already standing on would report a move that never took place. Silence is the refusal,
        /// the same way a slider already at its end refuses (owner ruling, 2026-08-19); the key is
        /// still taken, so the page underneath never sees it.</summary>
        private bool Resize(int size)
        {
            if (size == _size)
            {
                return true;
            }

            _size = size;
            Voice.Say(SizeText(), true);
            Settle(false);
            return true;
        }
    }
}
