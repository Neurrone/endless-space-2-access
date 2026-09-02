using System;
using System.Collections.Generic;
using Amplitude.Unity.Framework;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// Whose influence covers a SQUARE of the map, together with the empires behind the indexes so the
    /// caller can say it and compare it against the square it came from.
    ///
    /// The classification and the names travel together because the "out of" line names what was LEFT:
    /// the empire that owned the previous cell may own nothing near the new one, and by then there is
    /// nothing left to look its name up from.
    /// </summary>
    public sealed class CellInfluence
    {
        private static readonly List<Empire> Nobody = new List<Empire>();

        public static readonly CellInfluence Nothing = new CellInfluence(
            InfluenceReading.Nothing,
            Nobody,
            Nobody
        );

        public CellInfluence(
            InfluenceReading reading,
            List<Empire> owners,
            List<Empire> contesters
        )
        {
            Reading = reading;
            Owners = owners;
            Contesters = contesters;
        }

        public InfluenceReading Reading { get; private set; }

        /// <summary>The empires holding part of the cell, in the reading's own order.</summary>
        public List<Empire> Owners { get; private set; }

        /// <summary>The empires reaching into it without holding any of it.</summary>
        public List<Empire> Contesters { get; private set; }

        public bool Silent
        {
            get { return Reading.Silent; }
        }
    }

    /// <summary>
    /// The influence a system throws over the map, and whose influence a system is standing in.
    ///
    /// The game models influence as one circle per COLONY: a radius the colony grows for itself, a
    /// strength field falling off inside it, and every node resolved to the single strongest source
    /// standing over it (<c>ColonizedStarSystemRepository.TryGetInfluence</c>). The map draws all of
    /// that as a coloured disk and writes not one word anywhere on it - not the radius, not which way
    /// it is going next turn, not whose colour the disk is - so both readings here are the mod's
    /// phrases around the game's own numbers.
    ///
    /// Three questions, because they are three different things: how far THIS colony reaches
    /// (<see cref="RadiusLines"/>, a review line, since it is a number to plan with rather than news),
    /// whose influence has WON this place (<see cref="UnderInfluence"/>, spoken, since a system under
    /// somebody else's influence cannot be colonized and can eventually change hands), and who else is
    /// reaching for it without holding it (<see cref="Contested"/>, spoken, because that is the
    /// contest in progress and the map draws it as overlapping colour).
    ///
    /// EVERYTHING here is gated on the player perceiving the node (<see cref="MapVisibility.Perceived"/>).
    /// The influence values are the SIMULATION's, global and player-blind: reading them ungated hands
    /// the player the position and reach of colonies they have never seen. The game's own disk asks
    /// the same question before it draws (<c>GalaxyStarSystem.UpdateInfluenceRange</c> :1926 hides it
    /// on <c>Node.Visibility.IsInvisible</c>).
    ///
    /// Cost: <c>TryGetInfluenceRadius</c> is a dictionary lookup on the node's own position over the
    /// colonies standing THERE (<c>ColonizedStarSystemRepository</c> :132-161), not the galaxy-wide
    /// walk its <c>TryGetInfluence</c> sibling is, and the influencer is a plain property the influence
    /// pass has already resolved (<c>GameNode.SystemWhichInfluences</c> :212). Both are cheap enough
    /// for the focused node's per-frame readout - measured 3.8 us per focused frame for both spoken
    /// parts together; neither is asked for a system nobody is standing on.
    /// </summary>
    public static class SystemInfluence
    {
        /// <summary>
        /// How far this system's own influence reaches, and which way it is going next turn.
        ///
        /// Only for a system that projects at all: the service answers with the strongest colony
        /// standing at the node and its radius, and an OUTPOST projects nothing (its descriptor forces
        /// <c>SystemInfluenceRadius</c> to zero), so a place still growing into a colony has no line
        /// here rather than a line saying nought.
        ///
        /// The sentence itself, and the rule about which way the radius is going, are
        /// <see cref="InfluenceText.Radius"/>'s.
        /// </summary>
        public static IList<string> RadiusLines(GameNode node, Empire empire)
        {
            try
            {
                IInfluenceService influence = Services.GetService<IInfluenceService>();
                if (node == null || influence == null || !MapVisibility.Perceived(node, empire))
                {
                    return null;
                }

                ColonizedStarSystem source;
                float radius;
                float next;
                if (
                    !influence.TryGetInfluenceRadius(
                        node.NodePosition,
                        out source,
                        out radius,
                        out next
                    )
                    || radius <= 0f
                )
                {
                    return null;
                }

                return new List<string> { InfluenceText.Radius(radius, next) };
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a system's influence radius threw: " + e);
                return null;
            }
        }

        /// <summary>
        /// Whose influence is standing over this place, where that is not the empire holding it.
        ///
        /// Nothing at all for the ordinary case - a colony inside its own empire's circle - because
        /// that is what a colony being where it is MEANS. What is worth a word is the mismatch: a
        /// system of yours standing in somebody else's influence (which is how a system changes hands
        /// without a shot), and an uncolonized node inside anybody's influence (which is what blocks a
        /// colony ship from settling there at all).
        ///
        /// Whose influence it is, is read as the map PAINTS it: the disk takes its colour from
        /// <c>systemWhichInfluences.Empire</c> (<c>GalaxyStarSystem.UpdateInfluenceRange</c> :1932),
        /// so that same empire is the one named and the one compared against the owners, rather than
        /// <c>InfluenceOwner</c>, which looks through an integrated minor faction to the empire that
        /// absorbed it and would name somebody whose colour is nowhere on the map.
        ///
        /// The owners are every colony standing at the node that this empire can see - an outpost
        /// included, because holding a place is holding it whether or not it has grown up yet, and a
        /// system shared with a minor faction has two of them.
        /// </summary>
        public static string UnderInfluence(GameNode node, Empire empire)
        {
            try
            {
                if (node == null || !MapVisibility.Perceived(node, empire))
                {
                    return null;
                }

                ColonizedStarSystem source = node.SystemWhichInfluences;
                Empire influencer = source == null ? null : source.Empire;
                if (influencer == null || EmpireIndex.Holds(OwnersAt(node, empire), influencer))
                {
                    return null;
                }

                return ReferenceEquals(influencer, Gui.PlayerEmpire)
                    ? ModStrings.Get(ModStrings.GalaxySystemInfluencedByYou)
                    : ModStrings.Format(
                        ModStrings.GalaxySystemInfluencedBy,
                        EmpireNames.Named(influencer)
                    );
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading whose influence a system is under threw: " + e);
                return null;
            }
        }

        /// <summary>
        /// Who else's influence REACHES this place without having won it.
        ///
        /// The winner is one empire, but a node can stand inside several circles at once, and the
        /// difference between "safely mine" and "about to be somebody else's" is exactly that overlap -
        /// which the map draws as colour on colour and never as a word. The empires already implied by
        /// the row are left out: the one that holds the node (its own reach is what being there means)
        /// and the one that has won it (the line above has just named them).
        ///
        /// Reach is the game's own test, the one its influence pass makes for every node
        /// (<c>ColonizedStarSystemRepository.TryGetInfluence</c> :111-127): the distance from here to
        /// the colony, against that colony's radius, boundary counting as inside. A colony is asked
        /// only where it is the source the game resolves AT ITS OWN NODE - two empires sharing a system
        /// have one circle between them, the stronger one, because that is the only one the game's own
        /// resolution can ever hand to anybody.
        /// </summary>
        public static string Contested(GameNode node, Empire empire)
        {
            try
            {
                IInfluenceService influence = Services.GetService<IInfluenceService>();
                IColonizedStarSystemRepositoryService colonies =
                    Services.GetService<IColonizedStarSystemRepositoryService>();
                if (
                    node == null
                    || influence == null
                    || colonies == null
                    || !MapVisibility.Perceived(node, empire)
                )
                {
                    return null;
                }

                ColonizedStarSystem won = node.SystemWhichInfluences;
                Empire winner = won == null ? null : won.Empire;
                List<Empire> owners = OwnersAt(node, empire);
                List<Empire> reaching = null;
                foreach (ColonizedStarSystem colony in colonies.GetValues())
                {
                    Empire behind = colony.Destroyed ? null : colony.Empire;
                    if (
                        behind == null
                        || ReferenceEquals(behind, winner)
                        || EmpireIndex.Holds(owners, behind)
                        || (reaching != null && EmpireIndex.Holds(reaching, behind))
                    )
                    {
                        continue;
                    }

                    ColonizedStarSystem source;
                    float radius;
                    if (
                        !influence.TryGetInfluenceRadius(colony.NodePosition, out source, out radius)
                        || !ReferenceEquals(source, colony)
                        || !Reaches(node, colony, radius)
                    )
                    {
                        continue;
                    }

                    if (reaching == null)
                    {
                        reaching = new List<Empire>();
                    }

                    reaching.Add(behind);
                }

                return Contesters(reaching);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading who is contesting a system's influence threw: " + e);
                return null;
            }
        }

        // ---- whose influence covers a square of the map ----

        /// <summary>
        /// WHOSE INFLUENCE STANDS OVER A CELL of the map - the inspect cursor's question, which is
        /// about an AREA and so is not a question the game will answer.
        ///
        /// The game only ever resolves influence at a POINT (<c>TryGetInfluence</c>), so every identity
        /// here is one of its own answers, asked at the covering grid and at the exact position of every
        /// galaxy node inside the cell - exact, because the Unfallen root override only fires on an
        /// exact position match (<c>ColonizedStarSystemRepository</c> :98-110) and a grid that missed it
        /// would be certifying a point the game answers differently. Whether those answers hold
        /// everywhere BETWEEN the samples is the one thing the mod decides, and it decides it with a
        /// proof rather than a guess (<see cref="InfluenceCell"/>).
        ///
        /// The circles are read the way the game builds them: one per galaxy node standing on colonies,
        /// centred on that node, with the strongest colony there setting the radius - the same walk
        /// <c>TryGetInfluence</c> makes, so the field the certificate is computed from is the field the
        /// game is resolving.
        ///
        /// A source the player is not being SHOWN is stripped of its name and not of its field: the map
        /// hides a colony's disk when the node is invisible (<c>GalaxyStarSystem.UpdateInfluenceRange</c>
        /// :1926), so naming that empire would hand the player a colony they have never seen. Left in
        /// the field as an anonymous rival it can only cost the cell its certificate, which reads as
        /// "edge of" and names nobody new.
        ///
        /// Whether the cell may be read AT ALL is the caller's: the mode asks its own fog first, and a
        /// cell nobody has explored is told nothing about (<see cref="Screens.GalaxyInspect"/>).
        ///
        /// The classification is then told to somebody STANDING in the cell
        /// (<see cref="InfluenceReading.EdgeWhereNobodyHolds"/>): a rim thinner than the sample spacing
        /// leaves every point query answering nobody while the circle is still overhead, and there is
        /// no held ground there for anyone to contest - the cursor is simply on the edge of it.
        ///
        /// Cost: nothing at all where no circle reaches the cell, which is most of the map - the grid is
        /// only asked for once something is there to classify. Measured over 86 nodes: 0.01 ms for
        /// empty space, 1.18 ms inside a bubble (1 by 1) and 1.37 ms (11 by 11). Only ever on a
        /// KEYPRESS.
        /// </summary>
        public static CellInfluence OverCell(
            double lowX,
            double lowY,
            double highX,
            double highY,
            Empire empire
        )
        {
            try
            {
                IInfluenceService influence = Services.GetService<IInfluenceService>();
                if (influence == null || empire == null || !GameGalaxy.Present())
                {
                    return CellInfluence.Nothing;
                }

                GameNode[] nodes = GameGalaxy.GameNodes();
                if (nodes == null)
                {
                    return CellInfluence.Nothing;
                }

                List<InfluenceSource> sources = new List<InfluenceSource>();
                List<Empire> known = new List<Empire>();
                List<GameNode> inside = new List<GameNode>();
                bool anyReaches = false;
                for (int i = 0; i < nodes.Length; i++)
                {
                    GameNode node = nodes[i];
                    if (node == null)
                    {
                        continue;
                    }

                    double x = node.GalaxyPosition.X;
                    double y = node.GalaxyPosition.Y;
                    if (x >= lowX && x < highX && y >= lowY && y < highY)
                    {
                        inside.Add(node);
                    }

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
                    source.X = x;
                    source.Y = y;
                    source.Radius = radius;
                    source.Empire = Nameable(colony, empire, known);
                    sources.Add(source);
                    anyReaches =
                        anyReaches
                        || InfluenceCell.Reaches(source, lowX, lowY, highX, highY);
                }

                List<InfluenceAnswer> answers = new List<InfluenceAnswer>();
                for (int i = 0; i < inside.Count; i++)
                {
                    Ask(influence, inside[i].GalaxyPosition, empire, known, answers);
                }

                if (anyReaches)
                {
                    double[] xs = InfluenceCell.Axis(lowX, highX, InfluenceCell.SamplesPerSide);
                    double[] ys = InfluenceCell.Axis(lowY, highY, InfluenceCell.SamplesPerSide);
                    for (int a = 0; a < xs.Length; a++)
                    {
                        for (int b = 0; b < ys.Length; b++)
                        {
                            Ask(
                                influence,
                                new GalaxyPosition((float)xs[a], (float)ys[b]),
                                empire,
                                known,
                                answers
                            );
                        }
                    }
                }

                InfluenceReading reading = InfluenceCell.Classify(
                    lowX,
                    lowY,
                    highX,
                    highY,
                    sources,
                    influence.InfluenceStrenghtPower,
                    answers,
                    InfluenceCell.CoveringRadius(
                        highX - lowX,
                        highY - lowY,
                        InfluenceCell.SamplesPerSide
                    ),
                    anyReaches
                ).EdgeWhereNobodyHolds();
                return new CellInfluence(
                    reading,
                    Behind(reading.Empires, known),
                    Behind(reading.Contesters, known)
                );
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: classifying the influence over a cell threw: " + e);
                return CellInfluence.Nothing;
            }
        }

        /// <summary>Ask the game whose influence stands at one point, and record its answer at the very
        /// coordinates it was asked about - the certificate is evaluated at the sampled point, so the
        /// arithmetic and the query must be talking about the same place down to the float.</summary>
        private static void Ask(
            IInfluenceService influence,
            GalaxyPosition at,
            Empire empire,
            List<Empire> known,
            List<InfluenceAnswer> into
        )
        {
            ColonizedStarSystem colony;
            InfluenceAnswer answer = new InfluenceAnswer();
            answer.X = at.X;
            answer.Y = at.Y;
            answer.Empire = influence.TryGetInfluence(at, out colony, false)
                ? Nameable(colony, empire, known)
                : -1;
            into.Add(answer);
        }

        /// <summary>The empire index the player may be TOLD about, or -1 - a colony whose node the map
        /// is not showing is nobody as far as the reading is concerned, and its empire is remembered
        /// only where it can be named. Shared with <see cref="InfluenceGround"/> rather than copied:
        /// two readings of the same field that disagreed about who may be NAMED would be two different
        /// fog rules.</summary>
        internal static int Nameable(
            ColonizedStarSystem colony,
            Empire empire,
            List<Empire> known
        )
        {
            Empire behind = colony == null || colony.Destroyed ? null : colony.Empire;
            if (behind == null || !MapVisibility.Perceived(colony.Node, empire))
            {
                return -1;
            }

            if (!EmpireIndex.Holds(known, behind))
            {
                known.Add(behind);
            }

            return behind.Index;
        }

        /// <summary>The empires behind a set of indexes, in the set's own order.</summary>
        private static List<Empire> Behind(int[] indexes, List<Empire> known)
        {
            List<Empire> empires = new List<Empire>(indexes.Length);
            for (int i = 0; i < indexes.Length; i++)
            {
                Empire behind = EmpireIndex.Find(known, indexes[i]);
                if (behind != null)
                {
                    empires.Add(behind);
                }
            }

            return empires;
        }

        /// <summary>The cell's own influence sentence - "in", "on the edge of", and who. Nothing at all
        /// where nobody holds any of it, which for a cursor reading is empty sky: a reach into ground
        /// nobody holds has already become an EDGE by the time this is asked
        /// (<see cref="InfluenceReading.EdgeWhereNobodyHolds"/>).</summary>
        public static string Whose(CellInfluence cell)
        {
            if (cell == null || cell.Owners.Count == 0)
            {
                return null;
            }

            return InfluenceText.Cell(cell.Reading.Cover, Names(cell.Owners), Alone(cell.Owners));
        }

        /// <summary>Who is reaching into the cell without holding any of it - the same sentence a
        /// system's own contested line is said in, because it is the same fact about a different
        /// shape. It always rides on top of an owner: a contest is a fact about ground somebody holds,
        /// so a cell nobody holds names its reachers as edge-owners instead
        /// (<see cref="InfluenceReading.EdgeWhereNobodyHolds"/>) and this answers nothing.</summary>
        public static string ContestedIn(CellInfluence cell)
        {
            return cell == null ? null : Contesters(new List<Empire>(cell.Contesters));
        }

        /// <summary>Stepping out into space nobody's influence reaches: what was left behind, named the
        /// way the constellation crossing names the region being left.</summary>
        public static string LeftBehind(CellInfluence was)
        {
            if (was == null || was.Owners.Count == 0)
            {
                return null;
            }

            return InfluenceText.Left(Names(was.Owners), Alone(was.Owners));
        }

        private static bool Alone(List<Empire> empires)
        {
            return empires.Count == 1 && ReferenceEquals(empires[0], Gui.PlayerEmpire);
        }

        private static List<string> Names(List<Empire> empires)
        {
            List<string> names = new List<string>(empires.Count);
            for (int i = 0; i < empires.Count; i++)
            {
                names.Add(Named(empires[i]));
            }

            return names;
        }

        /// <summary>Whether this colony's circle covers that node - <see cref="InfluenceCell.Reaches"/>
        /// asked of a rectangle of no size, which is what a POINT is (the same degenerate rect the
        /// ground sweep passes it). One implementation of "is this place inside that circle", so a
        /// system's reading and the map's own ground can never disagree about the boundary.</summary>
        private static bool Reaches(GameNode node, ColonizedStarSystem colony, float radius)
        {
            GameNode from = colony.Node;
            if (from == null)
            {
                return false;
            }

            InfluenceSource source = new InfluenceSource
            {
                X = from.GalaxyPosition.X,
                Y = from.GalaxyPosition.Y,
                Radius = radius,
            };
            return InfluenceCell.Reaches(
                source,
                node.GalaxyPosition.X,
                node.GalaxyPosition.Y,
                node.GalaxyPosition.X,
                node.GalaxyPosition.Y
            );
        }

        /// <summary>
        /// The contesters as one line, in the empire order the game itself lists empires in, so the
        /// same overlap is read the same way twice running.
        ///
        /// The player alone gets a sentence of their own, because "your empire" is what the player is
        /// called everywhere else in the mod. In a list they are named like anybody else: a list is
        /// read as a list of empires, and one item wearing a different kind of name is the thing that
        /// makes a list hard to follow.
        /// </summary>
        private static string Contesters(List<Empire> reaching)
        {
            if (reaching == null || reaching.Count == 0)
            {
                return null;
            }

            reaching.Sort(ByEmpireOrder);
            if (reaching.Count == 1 && ReferenceEquals(reaching[0], Gui.PlayerEmpire))
            {
                return ModStrings.Get(ModStrings.GalaxySystemInfluenceContestedYou);
            }

            List<string> names = new List<string>(reaching.Count);
            for (int i = 0; i < reaching.Count; i++)
            {
                names.Add(Named(reaching[i]));
            }

            return InfluenceText.Contested(names);
        }

        private static readonly Comparison<Empire> ByEmpireOrder = CompareEmpireOrder;

        private static int CompareEmpireOrder(Empire left, Empire right)
        {
            return left.Index.CompareTo(right.Index);
        }

        private static string Named(Empire empire)
        {
            return EmpireNames.Named(empire);
        }

        /// <summary>The empires holding this place. Asked through the same visibility gate the rest of
        /// the map's ownership reading uses (<c>Visibility[empire] >= 1</c>), so a colony the player has
        /// never seen can neither silence a line nor be named by one - and an OUTPOST counts, because
        /// holding a place is holding it whether or not it has grown up yet.</summary>
        private static List<Empire> OwnersAt(GameNode node, Empire empire)
        {
            List<Empire> owners = new List<Empire>();
            IColonizedStarSystemRepositoryService colonies =
                Services.GetService<IColonizedStarSystemRepositoryService>();
            if (colonies == null)
            {
                return owners;
            }

            foreach (ColonizedStarSystem colony in colonies.GetValues(node.NodePosition))
            {
                if (
                    colony.Empire != null
                    && (int)colony.Visibility[empire] >= (int)EntityVisibility.Layer.Known
                    && colony.State != StarSystemState.Ghost
                    && !EmpireIndex.Holds(owners, colony.Empire)
                )
                {
                    owners.Add(colony.Empire);
                }
            }

            return owners;
        }
    }
}
