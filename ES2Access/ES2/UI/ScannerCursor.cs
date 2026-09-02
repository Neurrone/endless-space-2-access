using System.Collections.Generic;
using ES2Access.Core.Util;

namespace ES2Access.ES2.UI
{
    /// <summary>What a scanner press turned out to be, and therefore which sentence it is said in.
    /// </summary>
    public enum ScannerAnswer
    {
        /// <summary>A scope was landed in or is being reported from a standstill: it is named, and
        /// then the thing the cursor stands on in it is said.</summary>
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
    /// (nearest first, from wherever the player is reading), so what is remembered between presses is
    /// a POSITION plus the two IDENTITIES that let a position be found again in a list that was
    /// rebuilt: the NAME of the subcategory the category was left in, and the KEY of the thing the
    /// cursor was standing on. Both matter for the same reason - an index into a rebuilt list is not
    /// an identity - and neither is guessed here: the caller hands over the names with the counts
    /// (<see cref="ScannerTable"/>) and the keys with the list.
    ///
    /// Landing in a NEW scope still resets to the nearest thing. Only staying where you were keeps
    /// hold of the thing: re-seating on it is what lets the player walk the map with the arrow keys
    /// - which re-sorts every list around wherever they now are - and then step on from the very
    /// thing they were last told about rather than from whatever has since become the nearest.
    ///
    /// The counts arrive as a table, one row per category and one column per subcategory, so a single
    /// snapshot of the world answers every question a cycle asks - including "is the category next
    /// door empty", which is what skipping needs and what nothing else would have gathered.
    ///
    /// Engine-free: the whole of the skipping, wrapping, remembering and resetting is testable without
    /// the game, which is the half that has no audible failure mode - a cycle that quietly lands on an
    /// empty scope sounds exactly like one that found nothing to say.
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

        /// <summary>The key of the thing the cursor was last told to be standing on, which is what a
        /// re-seat looks for. Null until a press has landed on something.</summary>
        public string ResultKey
        {
            get { return _resultKey; }
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

        /// <summary>Where the cursor already is, for the arming press. It says the scope it is parked
        /// in whichever tier's key armed it - the player is being told a PLACE, and the tier they
        /// happened to press says nothing about where they were standing - and the only thing that can
        /// displace that is the scope having emptied.</summary>
        public ScannerAnswer Hold(ScannerTable counts)
        {
            Settle(counts);
            return Count(counts) == 0 ? ScannerAnswer.Empty : ScannerAnswer.Scope;
        }

        /// <summary>
        /// The next category with anything in it, in the direction asked for, wrapping at the ends.
        ///
        /// Landing opens the category at the subcategory the player last left IT in - each category
        /// remembers its own, and one never visited is at its first scope that holds something, which
        /// is "all" in every taxonomy that has one. A player who narrows systems to their own and then
        /// goes to look at fleets comes back to their own systems rather than to all of them: the
        /// narrowing is a question about systems, not a mode the whole scanner is in, and having to
        /// re-narrow after every look sideways is what made the reset wrong.
        ///
        /// A remembered scope that has since emptied falls back to the first that holds something -
        /// the same rule a first visit gets, and never a scope the player would have to press again to
        /// escape from.
        /// </summary>
        public ScannerAnswer CycleCategory(int delta, ScannerTable counts)
        {
            Settle(counts);
            int rows = counts == null ? 0 : counts.Categories;
            for (int step = 1; step <= rows; step++)
            {
                int at = Cycle.Wrap(_category + delta * step, rows);
                if (counts.Holds(at))
                {
                    _category = at;
                    _subcategory = Remembered(counts, at);
                    _index = 0;
                    Remember(counts);
                    return ScannerAnswer.Scope;
                }
            }

            return Stay(counts);
        }

        /// <summary>The next subcategory of this category with anything in it, wrapping at the ends.
        /// Where the category holds exactly one such scope the cursor comes round to it and says it
        /// again, which is the answer to "what else is there" when the answer is "nothing else".
        /// </summary>
        public ScannerAnswer CycleSubcategory(int delta, ScannerTable counts)
        {
            Settle(counts);
            int width = counts == null ? 0 : counts.Width(_category);
            for (int step = 1; step <= width; step++)
            {
                int at = Cycle.Wrap(_subcategory + delta * step, width);
                if (counts.Count(_category, at) > 0)
                {
                    _subcategory = at;
                    _index = 0;
                    Remember(counts);
                    return ScannerAnswer.Scope;
                }
            }

            return Stay(counts);
        }

        /// <summary>One thing along the current scope's list, wrapping past either end - so the far
        /// end is one press from the nearest, and a player sweeping a list never has to remember how
        /// long it was.</summary>
        public ScannerAnswer Step(int delta, ScannerTable counts)
        {
            Settle(counts);
            int count = Count(counts);
            if (count == 0)
            {
                return ScannerAnswer.Empty;
            }

            _index = Cycle.Wrap(_index + delta, count);
            return ScannerAnswer.Instance;
        }

        /// <summary>How many things the scope the cursor is in holds.</summary>
        public int Count(ScannerTable counts)
        {
            return counts == null ? 0 : counts.Count(_category, _subcategory);
        }

        /// <summary>
        /// Put the cursor back on the THING it was last standing on, in the list as it now stands -
        /// called with the freshly built scope before the press is acted on.
        ///
        /// The list is rebuilt and re-sorted around wherever the player is reading from, so between
        /// two presses the same thing can be at a different index and the same index can be a
        /// different thing. Stepping from the index would then step from something the player was
        /// never told about, which is exactly what walking the map between two presses does.
        ///
        /// Where the thing is gone the settled index stands, which is the nearest thing in the scope
        /// or the beginning of it - the same place a scope that shrank leaves the cursor.
        /// </summary>
        public void Reseat(ScannerTable counts, IList<string> keys)
        {
            Settle(counts);
            if (_resultKey == null || keys == null)
            {
                return;
            }

            for (int i = 0; i < keys.Count; i++)
            {
                if (_resultKey == keys[i])
                {
                    _index = i;
                    return;
                }
            }
        }

        /// <summary>
        /// Put the cursor somewhere outright - which category, which of its columns, which thing.
        ///
        /// The three tier keys never need this: each of them asks for a MOVE from where the cursor
        /// already is. A quick key does, because it is a whole gesture of its own - it names the
        /// category it walks and works out the position itself - and the scanner has one cursor, so
        /// the paging keys must carry on from wherever a quick key left it.
        /// </summary>
        public void Point(int category, int subcategory, int index, ScannerTable counts)
        {
            Settle(counts);
            _category = category;
            _subcategory = subcategory;
            _index = index;
            Remember(counts);
        }

        /// <summary>What the press ended up standing on, so the next one can find it again. Called
        /// with the scope the announcement was read out of.</summary>
        public void Landed(IList<string> keys)
        {
            _resultKey = keys != null && _index >= 0 && _index < keys.Count ? keys[_index] : null;
        }

        /// <summary>Point the scanner back at the beginning and forget that it was ever pressed - the
        /// mod being torn down, or the player having gone to another game.</summary>
        public void Forget()
        {
            _category = 0;
            _subcategory = 0;
            _index = 0;
            _armed = false;
            _memory = Empty;
            _named = NoNames;
            _resultKey = null;
        }

        private int _category;
        private int _subcategory;
        private int _index;
        private bool _armed;
        private string _resultKey;

        /// <summary>Which subcategory each category was last left in, as an INDEX. Zero - "all" in
        /// every taxonomy that has one - is what a category never visited remembers, so a first visit
        /// needs no flag of its own: the memory and the fallback agree. This is the whole memory for a
        /// category whose columns have no names, and the fallback for one whose have.</summary>
        private int[] _memory = Empty;

        /// <summary>...and as a NAME, for the categories that name their columns. The list of scopes a
        /// category has can be derived from what is out there and can therefore change shape between
        /// presses, and an index into a list that changed shape is not the scope it was.</summary>
        private string[] _named = NoNames;

        private static readonly int[] Empty = new int[0];
        private static readonly string[] NoNames = new string[0];

        /// <summary>Bring the cursor back inside a world that has changed under it: a scope that has
        /// shrunk past the parked index leaves the cursor on the nearest thing rather than on nothing,
        /// which is the same place a new scope starts at - and a scope that is no longer THERE leaves
        /// it in the first one that holds something, because an index into a column that has gone
        /// points at somebody else's things.
        ///
        /// Public because the caller has to ask for it BEFORE it builds the list it will re-seat in:
        /// which list that is depends on which scope the cursor turns out to be in, and that is what
        /// this settles.</summary>
        public void Settle(ScannerTable counts)
        {
            if (counts == null || counts.Categories == 0)
            {
                _category = 0;
                _subcategory = 0;
                _index = 0;
                return;
            }

            if (_memory.Length != counts.Categories)
            {
                int[] grown = new int[counts.Categories];
                string[] renamed = new string[counts.Categories];
                for (int i = 0; i < _memory.Length && i < grown.Length; i++)
                {
                    grown[i] = _memory[i];
                    renamed[i] = i < _named.Length ? _named[i] : null;
                }

                _memory = grown;
                _named = renamed;
            }

            if (_category < 0 || _category >= counts.Categories)
            {
                _category = 0;
            }

            int width = counts.Width(_category);
            string want = _named[_category];
            int named = counts.Find(_category, want);
            if (named >= 0)
            {
                _subcategory = named;
            }
            else if (want != null)
            {
                _subcategory = counts.FirstHolding(_category);
            }
            else if (_subcategory < 0 || _subcategory >= width)
            {
                _subcategory = 0;
            }

            if (_index < 0 || _index >= counts.Count(_category, _subcategory))
            {
                _index = 0;
            }

            Remember(counts);
        }

        /// <summary>Write down where the cursor now stands, both ways round.</summary>
        private void Remember(ScannerTable counts)
        {
            if (_category < 0 || _category >= _memory.Length)
            {
                return;
            }

            _memory[_category] = _subcategory;
            _named[_category] = counts.Label(_category, _subcategory);
        }

        /// <summary>The subcategory a category is opened at: the one it was last left in while that
        /// still holds something, and otherwise its first that does. Asked by name where the category
        /// names its columns, because the index that name stood at is not the one it stands at now.
        /// </summary>
        private int Remembered(ScannerTable counts, int category)
        {
            string want = category < _named.Length ? _named[category] : null;
            int named = counts.Find(category, want);
            if (named >= 0)
            {
                return counts.Count(category, named) > 0 ? named : counts.FirstHolding(category);
            }

            if (want != null)
            {
                return counts.FirstHolding(category);
            }

            int last = category < _memory.Length ? _memory[category] : 0;
            return last >= 0 && last < counts.Width(category) && counts.Count(category, last) > 0
                ? last
                : counts.FirstHolding(category);
        }

        /// <summary>The answer when a cycle found nowhere else to go: the cursor has not moved, and
        /// says where it is - or that there is nothing here, which is the one way the empty line is
        /// ever reached.</summary>
        private ScannerAnswer Stay(ScannerTable counts)
        {
            _index = 0;
            return Count(counts) == 0 ? ScannerAnswer.Empty : ScannerAnswer.Scope;
        }
    }
}
