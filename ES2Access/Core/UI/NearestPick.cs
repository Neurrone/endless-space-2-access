namespace ES2Access.Core.UI
{
    /// <summary>
    /// "Which of these things is the one that was meant?", answered by distance — for a game that says
    /// where to look with a POINT rather than with the object standing there.
    ///
    /// Three rules, and they are the whole of it. Nothing further away than the radius is an answer at
    /// all: a point in empty space is better left unanswered than pinned on the nearest star half a
    /// galaxy off. Nearer wins. And a tie goes to whoever was offered FIRST, which is what lets the
    /// caller express a preference between two things that genuinely sit at the same spot by the order
    /// it offers them in - a star system and the fleets parked at it share a position exactly, and the
    /// containing place is the better answer.
    ///
    /// Squared distances throughout, so no caller needs a square root to use it.
    /// </summary>
    public sealed class NearestPick
    {
        private readonly double _limit;
        private double _best;
        private int _index = -1;

        /// <param name="radius">How far from the point a candidate may be and still be the answer, in
        /// whatever units the caller measures in.</param>
        public NearestPick(double radius)
        {
            _limit = radius * radius;
        }

        /// <summary>Whether anything offered so far is close enough to be an answer.</summary>
        public bool Found
        {
            get { return _index >= 0; }
        }

        /// <summary>The caller's index for the nearest candidate, or -1 while nothing qualifies.
        /// </summary>
        public int Index
        {
            get { return _index; }
        }

        /// <summary>Offer one candidate. Answers whether it is now the nearest.</summary>
        public bool Offer(int index, double squaredDistance)
        {
            if (squaredDistance > _limit || (_index >= 0 && squaredDistance >= _best))
            {
                return false;
            }

            _best = squaredDistance;
            _index = index;
            return true;
        }
    }
}
