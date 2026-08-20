using System.Collections.Generic;
using System.Text;

namespace ES2Access.Core.Map
{
    /// <summary>
    /// "Which constellation is this place in?" — answered from the places the constellations are
    /// made of, because the game names the members and draws the shape but never says where one
    /// region ends.
    ///
    /// Each region is the convex outline around its own member places. A query lands in the one
    /// outline that holds it; in none, and the answer is "no region" rather than a guess. Outlines
    /// interlock — a long constellation's hull can swallow a compact neighbour's — so a place inside
    /// several is awarded to whichever of them has the nearest MEMBER, which is the question a
    /// player is really asking: not "whose box is this in" but "whose stars are these". Equally near
    /// members go to the region added first, so the caller's own ordering settles a genuine tie and
    /// the same galaxy always answers the same way.
    ///
    /// Regions are identified by the index <see cref="Add(MapPoint[])"/> hands back, so nothing here
    /// knows what a constellation is; the runtime layer keeps that mapping. Build once per galaxy —
    /// that is where all the allocation is. <see cref="Classify(MapPoint)"/> and
    /// <see cref="DistanceTo"/> allocate nothing and may be asked per keystroke or per frame.
    /// </summary>
    public sealed class ConstellationRegions
    {
        private readonly double _epsilon;
        private readonly List<ConvexHull> _hulls = new List<ConvexHull>();
        private readonly List<MapPoint[]> _members = new List<MapPoint[]>();

        public ConstellationRegions()
            : this(ConvexHull.OnEdge)
        {
        }

        /// <param name="epsilon">How far outside an outline still counts as inside it, in the
        /// caller's own units.</param>
        public ConstellationRegions(double epsilon)
        {
            _epsilon = epsilon;
        }

        public int Count
        {
            get { return _hulls.Count; }
        }

        /// <summary>Adds a region made of these places and answers the index that now names it.
        /// The array is copied.</summary>
        public int Add(MapPoint[] places)
        {
            return Add(places, places.Length);
        }

        public int Add(MapPoint[] places, int count)
        {
            MapPoint[] members = new MapPoint[count];
            System.Array.Copy(places, members, count);
            _members.Add(members);
            _hulls.Add(ConvexHull.Build(members, count));
            return _hulls.Count - 1;
        }

        /// <summary>The same, from two coordinate arrays — for a caller (a dev probe, a game layer
        /// reading positions out of a list) that has numbers rather than points.</summary>
        public int Add(double[] xs, double[] ys, int count)
        {
            MapPoint[] members = new MapPoint[count];
            for (int i = 0; i < count; i++)
            {
                members[i] = new MapPoint(xs[i], ys[i]);
            }

            _members.Add(members);
            _hulls.Add(ConvexHull.Build(members, count));
            return _hulls.Count - 1;
        }

        public ConvexHull Hull(int region)
        {
            return _hulls[region];
        }

        public int MemberCount(int region)
        {
            return _members[region].Length;
        }

        public MapPoint Member(int region, int index)
        {
            return _members[region][index];
        }

        /// <summary>The region this place belongs to, or -1 if no outline holds it.</summary>
        public int Classify(MapPoint place)
        {
            int chosen = -1;
            double chosenMember = 0;
            bool ranked = false;

            for (int region = 0; region < _hulls.Count; region++)
            {
                if (!_hulls[region].Contains(place, _epsilon))
                {
                    continue;
                }

                if (chosen < 0)
                {
                    chosen = region;
                    continue;
                }

                // A second outline holds it, so the member places decide — and only now is it worth
                // walking them.
                if (!ranked)
                {
                    chosenMember = NearestMemberSquared(chosen, place);
                    ranked = true;
                }

                double contender = NearestMemberSquared(region, place);
                if (contender < chosenMember)
                {
                    chosen = region;
                    chosenMember = contender;
                }
            }

            return chosen;
        }

        public int Classify(double x, double y)
        {
            return Classify(new MapPoint(x, y));
        }

        /// <summary>How far this place is from one region: zero anywhere inside its outline,
        /// otherwise the distance to the outline's edge. This is what ranks the regions a place is
        /// NOT in — "just outside this one, half a galaxy from that one".</summary>
        public double DistanceTo(int region, MapPoint place)
        {
            return _hulls[region].DistanceTo(place, _epsilon);
        }

        private double NearestMemberSquared(int region, MapPoint place)
        {
            MapPoint[] members = _members[region];
            double best = double.PositiveInfinity;
            for (int i = 0; i < members.Length; i++)
            {
                double away = members[i].SquaredDistanceTo(place);
                if (away < best)
                {
                    best = away;
                }
            }

            return best;
        }

        /// <summary>
        /// Checks the model against the galaxy it was built from, for a probe to run on real data
        /// before any of this is believed.
        ///
        /// One count is an invariant and any other value is a bug: a member place must be inside its
        /// OWN outline, since the outline was built to enclose it. The rest are measurements of how
        /// interlocked this galaxy really is — how many member places another region's outline also
        /// swallows, and how many the classifier hands to somebody else, which happens only where
        /// two regions genuinely claim the same spot.
        /// </summary>
        public RegionAudit Audit()
        {
            RegionAudit audit = new RegionAudit();
            audit.Regions = _hulls.Count;

            for (int region = 0; region < _hulls.Count; region++)
            {
                MapPoint[] members = _members[region];
                for (int i = 0; i < members.Length; i++)
                {
                    MapPoint place = members[i];
                    audit.Members++;

                    if (!_hulls[region].Contains(place, _epsilon))
                    {
                        audit.OutsideOwnHull++;
                        if (audit.FirstStrandedRegion < 0)
                        {
                            audit.FirstStrandedRegion = region;
                            audit.FirstStrandedMember = i;
                        }
                    }

                    for (int other = 0; other < _hulls.Count; other++)
                    {
                        if (other != region && _hulls[other].Contains(place, _epsilon))
                        {
                            audit.InsideAnotherHull++;
                            break;
                        }
                    }

                    int answer = Classify(place);
                    if (answer != region)
                    {
                        audit.ClassifiedElsewhere++;
                        if (audit.FirstDisagreeingRegion < 0)
                        {
                            audit.FirstDisagreeingRegion = region;
                            audit.FirstDisagreeingMember = i;
                            audit.FirstDisagreeingAnswer = answer;
                        }
                    }
                }
            }

            return audit;
        }
    }

    /// <summary>What <see cref="ConstellationRegions.Audit"/> found. Its <see cref="ToString"/> is
    /// one line, so a probe can print it and read the whole verdict.</summary>
    public sealed class RegionAudit
    {
        public int Regions;
        public int Members;

        /// <summary>Member places their own outline does not hold. Must be zero; anything else is a
        /// defect in the geometry, not a property of the galaxy.</summary>
        public int OutsideOwnHull;

        /// <summary>Member places some other region's outline also holds — how interlocked this
        /// galaxy is. Informative, never a failure.</summary>
        public int InsideAnotherHull;

        /// <summary>Member places the classifier awards to a different region than the one that owns
        /// them — the overlap that the nearest-member rule could not settle, which needs two regions
        /// claiming the same spot.</summary>
        public int ClassifiedElsewhere;

        public int FirstStrandedRegion = -1;
        public int FirstStrandedMember = -1;
        public int FirstDisagreeingRegion = -1;
        public int FirstDisagreeingMember = -1;
        public int FirstDisagreeingAnswer = -1;

        public override string ToString()
        {
            StringBuilder text = new StringBuilder();
            text.Append("regions ").Append(Regions)
                .Append(", members ").Append(Members)
                .Append(", outside own hull ").Append(OutsideOwnHull)
                .Append(", inside another hull ").Append(InsideAnotherHull)
                .Append(", classified elsewhere ").Append(ClassifiedElsewhere);

            if (OutsideOwnHull > 0)
            {
                text.Append("; first stranded: region ").Append(FirstStrandedRegion)
                    .Append(" member ").Append(FirstStrandedMember);
            }

            if (ClassifiedElsewhere > 0)
            {
                text.Append("; first disagreement: region ").Append(FirstDisagreeingRegion)
                    .Append(" member ").Append(FirstDisagreeingMember)
                    .Append(" answered ").Append(FirstDisagreeingAnswer);
            }

            return text.ToString();
        }
    }
}
