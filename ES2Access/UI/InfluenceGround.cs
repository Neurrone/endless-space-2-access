using System;
using System.Collections.Generic;
using Amplitude.Unity.Framework;
using ES2Access.Core.UI;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// One square of map the player's own influence reaches, and what stands over it.
    ///
    /// The square is an inspect-grid cell of the smallest size - one map unit, centred on the whole
    /// pair the mod speaks positions in (<see cref="InspectGrid"/>) - so "ground" means the same thing
    /// here as it does under the inspect cursor, and a tile the scanner sends the player to is exactly
    /// the cell they then hear read out.
    /// </summary>
    public struct GroundTile
    {
        /// <summary>The tile's centre, in the pair the map is spoken in (offsets from home).</summary>
        public int X;
        public int Y;

        /// <summary>The player's OWN colony the game answered with somewhere inside the tile, or null
        /// where the game gave the player no point of it at all. The game's own answer, never a
        /// recomputation: whose the ground is, is not a question the mod may decide.</summary>
        public ColonizedStarSystem Held;

        /// <summary>The player's own colony whose field is strongest at the tile's centre among the
        /// ones REACHING it - which own system's reach this square lies in, for a square the player
        /// no longer wins any point of. The game's own comparison, over the game's own radii,
        /// restricted to the sources this answer is about.</summary>
        public ColonizedStarSystem Reaching;

        /// <summary>The empire, not the player's, that the game answered with at some sampled point of
        /// this tile - null where nobody else won any of it. An empire the player is not being SHOWN
        /// is nobody here, exactly as it is nobody to the inspect cursor's reading.</summary>
        public Empire Taker;

        /// <summary>Whether the whole tile is PROVABLY the player's - <see cref="InfluenceCell"/>'s
        /// certificate over the game's own answers, and never a guess.</summary>
        public bool Whole;
    }

    /// <summary>
    /// THE GROUND THE PLAYER'S OWN INFLUENCE STANDS ON, square by square.
    ///
    /// The node readings (<see cref="SystemInfluence"/>) answer "whose influence is over this place",
    /// one place at a time, and the inspect cursor answers it for one cell the player is standing in.
    /// Neither answers the question behind both features here: WHERE, across the whole of the
    /// player's own reach, is somebody else's field winning - which is what a border being pushed
    /// back looks like, and which the map draws as one colour creeping over another and writes no word
    /// about anywhere.
    ///
    /// The sweep is one pass and two readers. The turn-end watch
    /// (<see cref="InfluenceGroundWatch"/>) compares two passes to hear ground being LOST; the
    /// scanner's Contested Influence category reads one pass to list where the contest stands right
    /// now. Both need exactly the same tiles classified exactly the same way, so there is one place
    /// that does it.
    ///
    /// EVERY IDENTITY IS THE GAME'S. Influence resolves at a POINT
    /// (<c>ColonizedStarSystemRepository.TryGetInfluence</c>) and a tile is an area, so the split is
    /// the one <see cref="InfluenceCell"/> exists for: the game is asked at points, and the only thing
    /// computed here is the certificate that its answer holds everywhere between them. Nothing here
    /// can invent an owner, and a mistake in the arithmetic can only drop a tile out of "provably
    /// mine".
    ///
    /// THE FOG IS ASKED TWICE, and both halves matter. A tile whose centre the player has never
    /// explored is not swept at all (the inspect cursor's own question,
    /// <c>IVisibilityService.IsExplored</c>); and a colony whose node the map is not showing keeps its
    /// FIELD and loses its NAME (<see cref="SystemInfluence.Nameable"/>), so an unseen rival can cost
    /// a tile its certificate but can never be named as having taken one.
    ///
    /// COST is why the sample count is not fixed. A tile that one of the player's own circles covers
    /// entirely and no other circle reaches at all can only answer one way, and the certificate says
    /// so from the geometry alone - so it costs ONE point query, and the eleven-by-eleven grid is only
    /// paid for on the border band where the answer is actually in doubt. That is what makes the sweep
    /// affordable both once per turn and once per scanner keypress.
    /// </summary>
    public static class InfluenceGround
    {
        /// <summary>One map unit across - the inspect cursor's smallest cell, and the resolution
        /// "ground" is counted in.</summary>
        public const int TileSize = InspectGrid.SmallestSize;

        /// <summary>A tile's identity as one number, for the turn-to-turn diff's table.</summary>
        public static long Key(int x, int y)
        {
            return ((long)x << 32) | (uint)y;
        }

        /// <summary>Which tile a point belongs to, on the inspect grid's own half-open rule: a point
        /// exactly on a low edge is in that tile, one exactly on a high edge belongs to the next. Not
        /// <c>MapCoordinates.Round</c>, whose midpoints go AWAY from zero - the two disagree at a
        /// negative half unit, and a tile the sweep and the cursor disagreed about would be a square
        /// the scanner sent the player to and the cursor then read as somewhere else.</summary>
        public static int Tile(double offset)
        {
            return (int)Math.Floor(offset + 0.5);
        }

        public static List<GroundTile> Sweep(Empire empire)
        {
            int queries;
            return Sweep(empire, out queries);
        }

        /// <summary>
        /// Every tile the player's own influence circles reach, classified.
        ///
        /// <paramref name="queries"/> is how many point questions the game was asked - the cost
        /// figure, since everything else in the pass is arithmetic.
        /// </summary>
        public static List<GroundTile> Sweep(Empire empire, out int queries)
        {
            queries = 0;
            List<GroundTile> ground = new List<GroundTile>();
            try
            {
                IInfluenceService influence = Services.GetService<IInfluenceService>();
                IVisibilityService visibility = Services.GetService<IVisibilityService>();
                if (
                    influence == null
                    || visibility == null
                    || empire == null
                    || !GameGalaxy.Present()
                )
                {
                    return ground;
                }

                GameNode[] nodes = GameGalaxy.GameNodes();
                if (nodes == null)
                {
                    return ground;
                }

                GalaxyPosition origin = GalaxyCoordinates.Origin();
                double exponent = influence.InfluenceStrenghtPower;
                int mine = empire.Index;

                // The circles, read the way the game builds them - one per galaxy node standing on
                // colonies, the strongest colony there setting the radius - and the nodes bucketed by
                // tile, because the game's root override only fires on an EXACT position match and a
                // grid that missed a node would certify a point the game answers differently.
                List<InfluenceSource> sources = new List<InfluenceSource>();
                List<ColonizedStarSystem> behind = new List<ColonizedStarSystem>();
                List<Empire> known = new List<Empire>();
                Dictionary<long, List<GameNode>> standing = new Dictionary<long, List<GameNode>>();
                for (int i = 0; i < nodes.Length; i++)
                {
                    GameNode node = nodes[i];
                    if (node == null)
                    {
                        continue;
                    }

                    long at = Key(
                        Tile(node.GalaxyPosition.X - origin.X),
                        Tile(node.GalaxyPosition.Y - origin.Y)
                    );
                    List<GameNode> here;
                    if (!standing.TryGetValue(at, out here))
                    {
                        here = new List<GameNode>(1);
                        standing[at] = here;
                    }

                    here.Add(node);

                    ColonizedStarSystem colony;
                    float radius;
                    if (
                        !influence.TryGetInfluenceRadius(node.NodePosition, out colony, out radius)
                        || radius <= 0f
                    )
                    {
                        continue;
                    }

                    InfluenceSource source = new InfluenceSource();
                    source.X = node.GalaxyPosition.X;
                    source.Y = node.GalaxyPosition.Y;
                    source.Radius = radius;
                    source.Empire = SystemInfluence.Nameable(colony, empire, known);
                    sources.Add(source);
                    behind.Add(colony);
                }

                // The box every tile in question sits in: the union of the player's own circles.
                bool any = false;
                double westE = 0.0;
                double eastE = 0.0;
                double southN = 0.0;
                double northN = 0.0;
                for (int i = 0; i < sources.Count; i++)
                {
                    if (sources[i].Empire != mine)
                    {
                        continue;
                    }

                    double cx = sources[i].X - origin.X;
                    double cy = sources[i].Y - origin.Y;
                    double r = sources[i].Radius;
                    if (!any)
                    {
                        any = true;
                        westE = cx - r;
                        eastE = cx + r;
                        southN = cy - r;
                        northN = cy + r;
                        continue;
                    }

                    westE = Math.Min(westE, cx - r);
                    eastE = Math.Max(eastE, cx + r);
                    southN = Math.Min(southN, cy - r);
                    northN = Math.Max(northN, cy + r);
                }

                if (!any)
                {
                    return ground;
                }

                List<InfluenceAnswer> answers = new List<InfluenceAnswer>();
                for (int x = Tile(westE); x <= Tile(eastE); x++)
                {
                    for (int y = Tile(southN); y <= Tile(northN); y++)
                    {
                        Look(
                            influence,
                            visibility,
                            empire,
                            origin,
                            exponent,
                            sources,
                            behind,
                            known,
                            standing,
                            answers,
                            x,
                            y,
                            ground,
                            ref queries
                        );
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: sweeping the ground the player's influence covers threw: " + e);
            }

            return ground;
        }

        /// <summary>One tile: whether it is in reach at all, whether the player may be told about it,
        /// and then the game's own answers over it.</summary>
        private static void Look(
            IInfluenceService influence,
            IVisibilityService visibility,
            Empire empire,
            GalaxyPosition origin,
            double exponent,
            List<InfluenceSource> sources,
            List<ColonizedStarSystem> behind,
            List<Empire> known,
            Dictionary<long, List<GameNode>> standing,
            List<InfluenceAnswer> answers,
            int x,
            int y,
            List<GroundTile> ground,
            ref int queries
        )
        {
            int mine = empire.Index;
            double lowX = origin.X + InspectGrid.Low(x, TileSize);
            double highX = origin.X + InspectGrid.High(x, TileSize);
            double lowY = origin.Y + InspectGrid.Low(y, TileSize);
            double highY = origin.Y + InspectGrid.High(y, TileSize);
            double centreX = origin.X + x;
            double centreY = origin.Y + y;

            // Is any of this the player's reach at all, whose own circle covers all of it, and does
            // anybody else's reach in? The three geometry questions that decide both whether the tile
            // belongs to the sweep and how much the sweep has to pay for it.
            bool ours = false;
            bool covered = false;
            bool rivals = false;
            ColonizedStarSystem reaching = null;
            double strongest = 0.0;
            for (int i = 0; i < sources.Count; i++)
            {
                if (!InfluenceCell.Reaches(sources[i], lowX, lowY, highX, highY))
                {
                    continue;
                }

                if (sources[i].Empire != mine)
                {
                    rivals = true;
                    continue;
                }

                ours = true;
                covered = covered || InfluenceCell.Contains(sources[i], lowX, lowY, highX, highY);
                double strength = InfluenceCell.Strength(sources[i], centreX, centreY, exponent);
                if (reaching == null || strength > strongest)
                {
                    reaching = behind[i];
                    strongest = strength;
                }
            }

            if (!ours)
            {
                return;
            }

            // The inspect cursor's own fog question, asked of the one square this tile is
            // (GalaxyInspect.Fogged): a place the player has never explored is told nothing about, in
            // either direction.
            if (!visibility.IsExplored(empire, new GalaxyPosition((float)centreX, (float)centreY)))
            {
                return;
            }

            // A tile one of the player's own circles covers entirely, that nothing else reaches, can
            // only answer one way - the certificate's exact-containment proof needs no grid, and one
            // confirming point is the whole cost. Everywhere else the doubt is real and the full grid
            // is paid for.
            int perSide = covered && !rivals ? 1 : InfluenceCell.SamplesPerSide;

            // WHERE NOTHING ELSE REACHES, a sample point outside every one of the player's own circles
            // settles the tile without asking the game anything: every field there is nought, so the
            // game answers nobody, which is neither a taker nor a point the certificate could survive.
            // A tile like that is neither news nor ground - it is the ragged outside of the player's
            // own rim - and the whole eleven-by-eleven grid would be spent proving it.
            //
            // The arithmetic is only ever allowed to SILENCE, which is what keeps this honest: it can
            // drop a tile out of "provably mine" and it can decide nobody took anything, and there is
            // no path by which it names an owner or invents a loss. (The one thing it cannot see is the
            // game's exact-position override on a galaxy node; that too can only cost a certificate.)
            if (!rivals && !Covered(sources, mine, lowX, lowY, highX, highY, perSide))
            {
                GroundTile bare = new GroundTile();
                bare.X = x;
                bare.Y = y;
                bare.Reaching = reaching;
                ground.Add(bare);
                return;
            }

            answers.Clear();
            ColonizedStarSystem held = null;
            List<GameNode> here;
            if (standing.TryGetValue(Key(x, y), out here))
            {
                for (int i = 0; i < here.Count; i++)
                {
                    Ask(
                        influence,
                        here[i].GalaxyPosition,
                        empire,
                        known,
                        answers,
                        ref held,
                        ref queries
                    );
                }
            }

            double[] xs = InfluenceCell.Axis(lowX, highX, perSide);
            double[] ys = InfluenceCell.Axis(lowY, highY, perSide);
            for (int a = 0; a < xs.Length; a++)
            {
                for (int b = 0; b < ys.Length; b++)
                {
                    Ask(
                        influence,
                        new GalaxyPosition((float)xs[a], (float)ys[b]),
                        empire,
                        known,
                        answers,
                        ref held,
                        ref queries
                    );
                }
            }

            InfluenceReading reading = InfluenceCell.Classify(
                lowX,
                lowY,
                highX,
                highY,
                sources,
                exponent,
                answers,
                InfluenceCell.CoveringRadius(highX - lowX, highY - lowY, perSide),
                true
            );

            GroundTile tile = new GroundTile();
            tile.X = x;
            tile.Y = y;
            tile.Held = held;
            tile.Reaching = reaching;
            tile.Whole =
                reading.Cover == InfluenceCover.Whole
                && reading.Empires.Length == 1
                && reading.Empires[0] == mine;
            for (int i = 0; i < reading.Empires.Length; i++)
            {
                if (reading.Empires[i] == mine)
                {
                    continue;
                }

                tile.Taker = Behind(reading.Empires[i], known);
                break;
            }

            ground.Add(tile);
        }

        /// <summary>Whether every sample point of the tile lies inside at least one of the player's own
        /// circles - the same distance test the game makes before it gives a colony any strength at
        /// all, boundary inside, and no square root or power anywhere in it.</summary>
        private static bool Covered(
            List<InfluenceSource> sources,
            int mine,
            double lowX,
            double lowY,
            double highX,
            double highY,
            int perSide
        )
        {
            double[] xs = InfluenceCell.Axis(lowX, highX, perSide);
            double[] ys = InfluenceCell.Axis(lowY, highY, perSide);
            for (int a = 0; a < xs.Length; a++)
            {
                for (int b = 0; b < ys.Length; b++)
                {
                    bool inside = false;
                    for (int i = 0; i < sources.Count && !inside; i++)
                    {
                        inside =
                            sources[i].Empire == mine
                            && InfluenceCell.Reaches(sources[i], xs[a], ys[b], xs[a], ys[b]);
                    }

                    if (!inside)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>Ask the game whose influence stands at one point, recorded at the very coordinates
        /// it was asked about - the certificate is evaluated at the sampled point, so the arithmetic
        /// and the query have to be talking about the same place down to the float. The player's OWN
        /// colony is remembered as it comes back, because which of the player's systems held a square
        /// is the game's answer and not a second computation.</summary>
        private static void Ask(
            IInfluenceService influence,
            GalaxyPosition at,
            Empire empire,
            List<Empire> known,
            List<InfluenceAnswer> into,
            ref ColonizedStarSystem held,
            ref int queries
        )
        {
            ColonizedStarSystem colony;
            queries++;
            InfluenceAnswer answer = new InfluenceAnswer();
            answer.X = at.X;
            answer.Y = at.Y;
            answer.Empire = influence.TryGetInfluence(at, out colony, false)
                ? SystemInfluence.Nameable(colony, empire, known)
                : -1;
            if (held == null && answer.Empire == empire.Index)
            {
                held = colony;
            }

            into.Add(answer);
        }

        private static Empire Behind(int index, List<Empire> known)
        {
            for (int i = 0; i < known.Count; i++)
            {
                if (known[i].Index == index)
                {
                    return known[i];
                }
            }

            return null;
        }
    }
}
