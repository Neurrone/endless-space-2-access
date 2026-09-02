using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.Util;
using ES2Access.ES2.UI;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>What the square has just crossed INTO, said only on the press that changes it: the
    /// stretch of sky it is in, and whose influence stands over it.</summary>
    internal sealed partial class GalaxyInspect
    {
        /// <summary>
        /// WHICH STRETCH OF SKY THE CELL IS IN, said only when it has changed.
        ///
        /// The map writes a constellation's name across the middle of a region and draws no boundary
        /// anywhere; the boundary the mod derives from the region's own members
        /// (<see cref="ConstellationMap"/>) is what makes "which one am I in" a question a walked cell
        /// can answer at all. It is a fact about the WHOLE stretch, so repeating it on every cell of a
        /// crossing would be most of what a sweep said: it is news on the press that changes it and
        /// silent on every other, which is the same rule the size key already follows.
        ///
        /// There are exactly two changes. Arriving in a named region says its name. Leaving the last
        /// named region for the space between them names the region being LEFT, because the space
        /// itself has no name and "out of Herkules" is the only thing that can be said about it that
        /// the player did not already know.
        ///
        /// Only the constellations this empire has EXPLORED are in the model, so a cell in a stretch of
        /// sky the map has drawn no name across is in no region at all and this says nothing about it -
        /// the fog's own reading is the whole answer there.
        ///
        /// Entering the mode on a cell that is in no region says nothing: there is nothing to name and
        /// nothing has been left.
        /// </summary>
        private string Crossing()
        {
            try
            {
                // The classification only depends on the cell, and Settle is called for a resize and a
                // re-centre as well as for a move - so the cell it was last asked about is remembered
                // and a Settle that did not move the cursor asks nothing.
                if (_skyKnown && _skyX == _x && _skyY == _y)
                {
                    return null;
                }

                Constellation now = ConstellationMap.Classify(_x, _y);
                Constellation was = _sky;
                bool known = _skyKnown;
                _sky = now;
                _skyKnown = true;
                _skyX = _x;
                _skyY = _y;
                if (known && ReferenceEquals(now, was))
                {
                    return null;
                }

                if (now != null)
                {
                    return ModStrings.Format(
                        ModStrings.GalaxyInspectConstellation,
                        now.LocalizedName
                    );
                }

                return known && was != null
                    ? ModStrings.Format(
                        ModStrings.GalaxyInspectConstellationLeft,
                        was.LocalizedName
                    )
                    : null;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: naming the cell's constellation threw: " + e);
                return null;
            }
        }

        /// <summary>The stretch of sky the cursor was last read as standing in, and the cell that
        /// answer was taken for. Reset on entry, never on a suspend: coming back to the map re-reads
        /// the cell the player left the cursor on, and a cell that has not moved has crossed nothing.
        /// </summary>
        private Constellation _sky;
        private bool _skyKnown;
        private int _skyX;
        private int _skyY;

        /// <summary>
        /// WHOSE INFLUENCE THE CELL IS STANDING IN, said only when it has changed.
        ///
        /// The map paints influence as a coloured disk with not one word on it, and the thing a player
        /// steering by it actually needs is not the disk but its BOUNDARY: a colony ship cannot settle
        /// inside somebody else's, a system inside one can change hands without a shot, and the edge is
        /// where both of those start being true. So the crossing is the news, exactly as the
        /// constellation's is (<see cref="Crossing"/>), and it is silent on every press that does not
        /// change it.
        ///
        /// Three things can be said. A cell PROVED to be one empire's throughout is "in" theirs; one
        /// the boundary runs through - the rim of a circle, the line between two empires, or a cell the
        /// proof could not settle - is the "edge of" theirs, and several empires holding parts of one
        /// cell collapse into one line rather than one apiece. Stepping out into space nobody reaches
        /// names what was LEFT, for the same reason the constellation crossing does.
        ///
        /// The contested line rides along on top of HELD GROUND: an empire whose circle reaches into a
        /// cell somebody else holds is the overlap the map draws as colour on colour, and it is the
        /// same sentence a system's own row says it in. Where nobody holds any of the cell there is
        /// nothing to contest, and a lone circle reaching into it is read as its edge instead
        /// (<see cref="Core.UI.InfluenceReading.EdgeWhereNobodyHolds"/>) - so a rim too thin for any
        /// sample to land in still says "edge of", and says it once for the whole rim.
        ///
        /// The comparison is on the whole SET - who holds it, how much, and who is reaching - so a
        /// crossing that only changes the contest still speaks, and a sweep along a border does not
        /// repeat the border.
        ///
        /// A cell wholly under the fog says nothing about influence at all, and contributes nothing to
        /// the cell's identity for the skip: the fog's own reading is the whole answer there, exactly
        /// as it is for the constellation.
        ///
        /// THE SURVEY IS THE ONE EXCEPTION (owner ruling 2026-09-01). At the two furthest rungs the
        /// map names nothing and paints only the territory, so whose the square is stops being news
        /// about a crossing and becomes the answer the cell is being asked for: there it is said on
        /// EVERY square (<see cref="Surveying"/>). Space nobody holds is still the bare pair - a word
        /// for "nobody's" would be most of what a sweep of the empty half of a galaxy said, and there
        /// is none to say (owner question, open).
        /// </summary>
        private IList<string> Influence()
        {
            try
            {
                // The SIZE is part of the key where the constellation's is not: growing the cursor
                // over a rim really does take the cell from inside a circle to across its edge, and
                // that is a crossing the player made with the size key.
                if (
                    _bubblesKnown
                    && _bubblesX == _x
                    && _bubblesY == _y
                    && _bubblesSize == _size
                )
                {
                    return null;
                }

                CellInfluence now = CellNow();
                CellInfluence was = _bubbles;
                bool known = _bubblesKnown;
                bool survey = Surveying();
                _bubbles = now;
                _bubblesKnown = true;
                _bubblesX = _x;
                _bubblesY = _y;
                _bubblesSize = _size;
                if (!survey && known && now.Reading.Equals(was.Reading))
                {
                    return null;
                }

                List<string> lines = new List<string>();
                if (now.Silent)
                {
                    // Nothing here and nothing reaching for it: the only thing to say is what the
                    // cursor has just walked out of, and nothing at all where it walked out of nowhere.
                    Line(lines, known ? SystemInfluence.LeftBehind(was) : null);
                    return lines;
                }

                Line(lines, SystemInfluence.Whose(now));
                Line(lines, SystemInfluence.ContestedIn(now));
                return lines;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: naming the influence over the cell threw: " + e);
                return null;
            }
        }

        /// <summary>The influence over the cell the cursor is standing on now - the mode's own fog gate
        /// in front of it, so a square of map nobody has explored is told nothing about.</summary>
        private CellInfluence CellNow()
        {
            if (Fogged() >= InspectGrid.Squares(_size))
            {
                return CellInfluence.Nothing;
            }

            GalaxyPosition origin = GalaxyCoordinates.Origin();
            return SystemInfluence.OverCell(
                origin.X + InspectGrid.Low(_x, _size),
                origin.Y + InspectGrid.Low(_y, _size),
                origin.X + InspectGrid.High(_x, _size),
                origin.Y + InspectGrid.High(_y, _size),
                Gui.PlayerEmpire
            );
        }

        /// <summary>Whose influence the cursor was last read as standing in, and the cell that answer
        /// was taken for - the same memo the constellation crossing keeps, and for the same reason: a
        /// resize or a re-centre calls Settle without moving the cursor, and a crossing that did not
        /// happen must not speak.</summary>
        private CellInfluence _bubbles = CellInfluence.Nothing;
        private bool _bubblesKnown;
        private int _bubblesX;
        private int _bubblesY;
        private int _bubblesSize;
    }
}
