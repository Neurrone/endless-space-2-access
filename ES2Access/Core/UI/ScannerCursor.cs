namespace ES2Access.Core.UI
{
    /// <summary>What a scanner press turned out to be, and therefore which sentence it is said in.
    /// </summary>
    public enum ScannerAnswer
    {
        /// <summary>A new scope was landed in: it is named, counted, and its nearest thing said.
        /// </summary>
        Scope,

        /// <summary>One thing in the scope the cursor is already in.</summary>
        Instance,

        /// <summary>The scope the cursor is parked in holds nothing. Only reachable by standing still
        /// while the last thing in it went away - cycling skips empty scopes rather than landing in
        /// one.</summary>
        Empty,
    }

    /// <summary>
    /// Where the SCANNER is pointing: which category, which of that category's subcategories, and
    /// which of the things in it - plus the three rules that make the keys predictable (skip a scope
    /// with nothing in it, wrap at the ends of a list, and say where you are before you move the
    /// first time).
    ///
    /// The lists themselves are never held here. They are rebuilt and re-sorted on every press
    /// (nearest first, from wherever the player is reading), so the only thing worth remembering
    /// between presses is a POSITION - and a position into a list that is rebuilt is an index, not an
    /// identity. That is why landing in a new scope resets to the nearest thing rather than trying to
    /// keep hold of the one that was selected: the list it was an index into no longer exists.
    ///
    /// The counts arrive as a table, one row per category and one column per subcategory, so a single
    /// snapshot of the world answers every question a cycle asks - including "is the category next
    /// door empty", which is what skipping needs and what nothing else would have gathered.
    ///
    /// Engine-free: the whole of the skipping, wrapping and resetting is testable without the game,
    /// which is the half that has no audible failure mode - a cycle that quietly lands on an empty
    /// scope sounds exactly like one that found nothing to say.
    /// </summary>
    public sealed class ScannerCursor
    {
        /// <summary>Which category the cursor is in - an index into the count table's rows.</summary>
        public int Category
        {
            get { return _category; }
        }

        /// <summary>Which of that category's subcategories - an index into that row.</summary>
        public int Subcategory
        {
            get { return _subcategory; }
        }

        /// <summary>Which of the things in that scope, nearest first.</summary>
        public int Index
        {
            get { return _index; }
        }

        /// <summary>
        /// Whether this is the FIRST scanner press since the mod loaded or the player changed games.
        ///
        /// The first press of any of these keys says where the cursor already is instead of moving it.
        /// The scanner is not a mode and nothing announces it on the way in, so without this the
        /// player's first press would step off a position they were never told - and the position
        /// they were never told is the one they most want to hear.
        /// </summary>
        public bool Arm()
        {
            if (_armed)
            {
                return false;
            }

            _armed = true;
            return true;
        }

        /// <summary>Where the cursor already is, for the arming press: <paramref name="said"/> is the
        /// sentence the tier that was pressed would have said, and the only thing that can displace it
        /// is the scope having emptied.</summary>
        public ScannerAnswer Hold(int[][] counts, ScannerAnswer said)
        {
            Settle(counts);
            return Count(counts) == 0 ? ScannerAnswer.Empty : said;
        }

        /// <summary>
        /// The next category with anything in it, in the direction asked for, wrapping at the ends.
        ///
        /// Landing opens the category at its first subcategory that holds something - which is "all"
        /// in every taxonomy that has one, and never a scope the player would have to press again to
        /// escape from.
        /// </summary>
        public ScannerAnswer CycleCategory(int delta, int[][] counts)
        {
            Settle(counts);
            int rows = counts.Length;
            for (int step = 1; step <= rows; step++)
            {
                int at = Wrap(_category + delta * step, rows);
                if (Holds(counts[at]))
                {
                    _category = at;
                    _subcategory = FirstHolding(counts[at]);
                    _index = 0;
                    return ScannerAnswer.Scope;
                }
            }

            return Stay(counts);
        }

        /// <summary>The next subcategory of this category with anything in it, wrapping at the ends.
        /// Where the category holds exactly one such scope the cursor comes round to it and says it
        /// again, which is the answer to "what else is there" when the answer is "nothing else".
        /// </summary>
        public ScannerAnswer CycleSubcategory(int delta, int[][] counts)
        {
            Settle(counts);
            int[] row = counts[_category];
            for (int step = 1; step <= row.Length; step++)
            {
                int at = Wrap(_subcategory + delta * step, row.Length);
                if (row[at] > 0)
                {
                    _subcategory = at;
                    _index = 0;
                    return ScannerAnswer.Scope;
                }
            }

            return Stay(counts);
        }

        /// <summary>One thing along the current scope's list, wrapping past either end - so the far
        /// end is one press from the nearest, and a player sweeping a list never has to remember how
        /// long it was.</summary>
        public ScannerAnswer Step(int delta, int[][] counts)
        {
            Settle(counts);
            int count = Count(counts);
            if (count == 0)
            {
                return ScannerAnswer.Empty;
            }

            _index = Wrap(_index + delta, count);
            return ScannerAnswer.Instance;
        }

        /// <summary>How many things the scope the cursor is in holds.</summary>
        public int Count(int[][] counts)
        {
            return InRange(counts) ? counts[_category][_subcategory] : 0;
        }

        /// <summary>Point the scanner back at the beginning and forget that it was ever pressed - the
        /// mod being torn down, or the player having gone to another game.</summary>
        public void Forget()
        {
            _category = 0;
            _subcategory = 0;
            _index = 0;
            _armed = false;
        }

        private int _category;
        private int _subcategory;
        private int _index;
        private bool _armed;

        /// <summary>Bring the cursor back inside a world that has changed under it: a scope that has
        /// shrunk past the parked index leaves the cursor on the nearest thing rather than on nothing,
        /// which is the same place a new scope starts at.</summary>
        private void Settle(int[][] counts)
        {
            if (counts == null || counts.Length == 0)
            {
                _category = 0;
                _subcategory = 0;
                _index = 0;
                return;
            }

            if (_category < 0 || _category >= counts.Length)
            {
                _category = 0;
            }

            int[] row = counts[_category];
            if (_subcategory < 0 || _subcategory >= row.Length)
            {
                _subcategory = 0;
            }

            if (_index < 0 || _index >= row[_subcategory])
            {
                _index = 0;
            }
        }

        /// <summary>The answer when a cycle found nowhere else to go: the cursor has not moved, and
        /// says where it is - or that there is nothing here, which is the one way the empty line is
        /// ever reached.</summary>
        private ScannerAnswer Stay(int[][] counts)
        {
            _index = 0;
            return Count(counts) == 0 ? ScannerAnswer.Empty : ScannerAnswer.Scope;
        }

        private bool InRange(int[][] counts)
        {
            return counts != null
                && _category >= 0
                && _category < counts.Length
                && _subcategory >= 0
                && _subcategory < counts[_category].Length;
        }

        private static bool Holds(int[] row)
        {
            for (int i = 0; i < row.Length; i++)
            {
                if (row[i] > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static int FirstHolding(int[] row)
        {
            for (int i = 0; i < row.Length; i++)
            {
                if (row[i] > 0)
                {
                    return i;
                }
            }

            return 0;
        }

        /// <summary>Positive modulo: the wrap has to work the same way off either end of a list, and
        /// C#'s own remainder does not.</summary>
        private static int Wrap(int value, int length)
        {
            return length <= 0 ? 0 : ((value % length) + length) % length;
        }
    }
}
