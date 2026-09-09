using System;
using System.Collections.Generic;
using ES2Access.Core.Util;
using UnityEngine;

namespace ES2Access.UI
{
    /// <summary>
    /// Where a widget is drawn, for screens that want their navigation to match what the player can
    /// see. A popup that draws a strip of controls above its text and another below it should be
    /// walked that way - left and right along a strip, up and down between them - and the game's own
    /// answer to "which strip is this in" is the rectangle it drew the control at.
    ///
    /// Reading the layout rather than declaring it means one rule covers every window built from the
    /// same skeleton, including the ones that add a control of their own in a place no list of
    /// special cases would have predicted.
    ///
    /// Order and rows (<see cref="Rows"/>, <see cref="TopThenLeft"/>, <see cref="SameRow"/>) are asked
    /// of <see cref="AgeWidgets.LaidOutAt"/> - the drawn rectangle with scrolling undone - so that a
    /// panel's fixed parts and its scrolling parts keep the arrangement the panel was built with
    /// however far the player has scrolled. <see cref="Band"/> and <see cref="ReadingOrder"/> ask
    /// about what is on SCREEN and so keep drawn rectangles.
    /// </summary>
    public static class AgeLayout
    {
        /// <summary>Two positions closer than this are the same position: a strip's controls are
        /// laid out in whole pixels, so anything below half a pixel is rounding.</summary>
        private const float SamePlace = 0.5f;

        /// <summary>Whether <paramref name="widget"/> is drawn clear above the block of text
        /// (<c>-1</c>), clear below it (<c>1</c>), or level with it (<c>0</c>).</summary>
        public static int Band(AgeTransform widget, AgeTransform text)
        {
            try
            {
                Rect it = widget.GetGlobalPosition();
                Rect words = text.GetGlobalPosition();
                if (it.yMax <= words.yMin)
                {
                    return -1;
                }

                return it.yMin >= words.yMax ? 1 : 0;
            }
            catch (Exception e)
            {
                Log.Warn("layout: measuring a widget threw: " + e);
                return 0;
            }
        }

        /// <summary>
        /// Whether two widgets are drawn on the same line: each one's middle is level with the other.
        ///
        /// Sharing an edge is deliberately not enough. A banner's header line and the line under it
        /// routinely overlap by a pixel or three - the game lays out panels to touch - and they are
        /// plainly two lines to anyone looking at them.
        /// </summary>
        public static bool SameRow(AgeTransform first, AgeTransform second)
        {
            try
            {
                Rect a = AgeWidgets.LaidOutAt(first);
                Rect b = AgeWidgets.LaidOutAt(second);
                return Level(Middle(a), b) && Level(Middle(b), a);
            }
            catch (Exception e)
            {
                Log.Warn("layout: comparing two rows threw: " + e);
                return false;
            }
        }

        /// <summary>
        /// The widgets grouped into the lines they are drawn on - rows top to bottom, and left to right
        /// within a row. A screen walks the result with up and down between the rows and left and right
        /// along one, which is the shape the cluster already has for anyone who can see it.
        ///
        /// Grouping by measurement rather than by which panel each widget came from is what makes a
        /// strip that WRAPS work: a resource strip too long for its banner is laid out by the engine on
        /// a second line, and nothing in the panel says so - the rectangles do.
        /// </summary>
        /// <remarks>Each cell is measured ONCE, before anything is sorted: the comparisons that follow
        /// read the rectangle they were handed rather than walking the cell's ancestry again, which is
        /// what a sort of n cells did 2n log n times on every banded panel every frame.</remarks>
        public static List<List<T>> Rows<T>(IList<T> cells, Func<T, AgeTransform> widget)
        {
            List<Placed<T>> sorted = new List<Placed<T>>(cells.Count);
            for (int i = 0; i < cells.Count; i++)
            {
                sorted.Add(Measure(cells[i], widget(cells[i])));
            }

            sorted.Sort(Order<T>.Down);

            List<List<T>> rows = new List<List<T>>();
            List<Placed<T>> line = Order<T>.Line;
            line.Clear();
            Placed<T> anchor = default(Placed<T>);
            for (int i = 0; i < sorted.Count; i++)
            {
                Placed<T> cell = sorted[i];
                if (line.Count > 0 && !SameRow(anchor, cell))
                {
                    rows.Add(Read(line));
                }

                if (line.Count == 0)
                {
                    anchor = cell;
                }

                line.Add(cell);
            }

            if (line.Count > 0)
            {
                rows.Add(Read(line));
            }

            return rows;
        }

        /// <summary>
        /// One row, read across and handed over as the cells themselves.
        ///
        /// Down the screen was enough to find the rows; it is not enough to read one. The sort that
        /// found them orders by top edge first, so two cells of one row whose tops differ by a few
        /// pixels come out in that order rather than in the order they are drawn across - which is how
        /// a strip of icons and the numbers beside them, offset by three pixels, read as every number
        /// followed by every icon. Ordering each row again, across, is what makes "left to right
        /// within a row" true rather than merely intended.
        /// </summary>
        private static List<T> Read<T>(List<Placed<T>> line)
        {
            line.Sort(Order<T>.Across);

            List<T> row = new List<T>(line.Count);
            for (int i = 0; i < line.Count; i++)
            {
                row.Add(line[i].Cell);
            }

            line.Clear();
            return row;
        }

        /// <summary>Where a cell is laid out and which way its text pulls, taken once. A cell nothing
        /// could be measured of is ordered by neither rule and shares a row with nothing, which is the
        /// answer the comparisons themselves gave when the measurement threw under them.</summary>
        private static Placed<T> Measure<T>(T cell, AgeTransform widget)
        {
            try
            {
                int pull = TextPull(widget);
                return new Placed<T>(cell, AgeWidgets.LaidOutAt(widget), pull, true);
            }
            catch (Exception e)
            {
                Log.Warn("layout: measuring a cell threw: " + e);
                return new Placed<T>(cell, new Rect(), 0, false);
            }
        }

        /// <summary>Whether two measured cells are drawn on the same line - <see cref="SameRow"/>
        /// asked of rectangles already taken.</summary>
        private static bool SameRow<T>(Placed<T> first, Placed<T> second)
        {
            if (!first.Known || !second.Known)
            {
                return false;
            }

            return Level(Middle(first.Rect), second.Rect) && Level(Middle(second.Rect), first.Rect);
        }

        /// <summary>
        /// The order two cells of one row are read in.
        ///
        /// Their left edges, except when those tie - which is not the corner case it sounds like. A
        /// caption and its value are routinely TWO LABELS OCCUPYING THE SAME RECTANGLE, each spanning
        /// the panel's full width, one drawing its text against the left edge and the other against
        /// the right. The rectangles are identical, so nothing about position can separate them and
        /// the order falls back to whichever the widget tree happened to list first - which is how
        /// "Current Stock: 0/300" came to be read as "0/300 Current Stock:".
        ///
        /// Where the box says nothing, what the label does INSIDE the box says everything: text
        /// pushed left is drawn left of text pushed right, whatever their rectangles claim.
        /// </summary>
        private static int AcrossTheRow<T>(Placed<T> first, Placed<T> second)
        {
            if (!first.Known || !second.Known)
            {
                return 0;
            }

            Rect a = first.Rect;
            Rect b = second.Rect;
            if (Mathf.Abs(a.xMin - b.xMin) > SamePlace)
            {
                return a.xMin < b.xMin ? -1 : 1;
            }

            int pull = first.Pull.CompareTo(second.Pull);
            if (pull != 0)
            {
                return pull;
            }

            return Mathf.Abs(a.yMin - b.yMin) > SamePlace ? (a.yMin < b.yMin ? -1 : 1) : 0;
        }

        /// <summary><see cref="TopThenLeft"/> asked of rectangles already taken.</summary>
        private static int DownThePanel<T>(Placed<T> first, Placed<T> second)
        {
            if (!first.Known || !second.Known)
            {
                return 0;
            }

            Rect a = first.Rect;
            Rect b = second.Rect;
            if (Mathf.Abs(a.yMin - b.yMin) > SamePlace)
            {
                return a.yMin < b.yMin ? -1 : 1;
            }

            return Mathf.Abs(a.xMin - b.xMin) > SamePlace ? (a.xMin < b.xMin ? -1 : 1) : 0;
        }

        /// <summary>A cell with its measurement, which is all the two orderings and the row test read.
        /// </summary>
        private struct Placed<T>
        {
            public readonly T Cell;

            public readonly Rect Rect;

            public readonly int Pull;

            /// <summary>Whether the measurement was taken at all.</summary>
            public readonly bool Known;

            public Placed(T cell, Rect rect, int pull, bool known)
            {
                Cell = cell;
                Rect = rect;
                Pull = pull;
                Known = known;
            }
        }

        /// <summary>The two comparisons and the row buffer, made once per cell type rather than per
        /// call: the comparisons capture nothing, so there is one delegate for each instead of two
        /// closures per <see cref="Rows"/>. The buffer is safe to share because nothing runs between
        /// its filling and its emptying but those comparisons - the caller's own widget lookup is done
        /// with before the first row is started.</summary>
        private static class Order<T>
        {
            public static readonly Comparison<Placed<T>> Down = DownThePanel<T>;

            public static readonly Comparison<Placed<T>> Across = AcrossTheRow<T>;

            public static readonly List<Placed<T>> Line = new List<Placed<T>>();
        }

        /// <summary>Which side of its own box a widget's text is drawn against: 0 left, 1 centred, 2
        /// right. <see cref="AgeTextAnchor"/> lists its twelve anchors as four vertical bands of
        /// left/centre/right, so the horizontal third of the value is the whole question. A widget
        /// with no text at all answers "left", which costs nothing: it fills its own box, so its box
        /// has already placed it.</summary>
        private static int TextPull(AgeTransform widget)
        {
            try
            {
                AgePrimitiveLabel label = widget.GetComponent<AgePrimitiveLabel>();
                return label == null ? 0 : (int)label.Alignement % 3;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>The order the two widgets are read in: left to right, and top to bottom where one
        /// sits above the other in the same column.</summary>
        public static int ReadingOrder(AgeTransform first, AgeTransform second)
        {
            try
            {
                Rect a = first.GetGlobalPosition();
                Rect b = second.GetGlobalPosition();
                if (Mathf.Abs(a.xMin - b.xMin) > SamePlace)
                {
                    return a.xMin < b.xMin ? -1 : 1;
                }

                if (Mathf.Abs(a.yMin - b.yMin) > SamePlace)
                {
                    return a.yMin < b.yMin ? -1 : 1;
                }

                return 0;
            }
            catch (Exception e)
            {
                Log.Warn("layout: comparing two widgets threw: " + e);
                return 0;
            }
        }

        /// <summary>
        /// A total order over a whole cluster, down the screen first and then across, so the sort is
        /// stable whatever the rows turn out to be. Row grouping is a separate question, asked of
        /// neighbours once they are in order.
        ///
        /// This is the order a VERTICAL list of things is read in - a popup's content area, where each
        /// item is a line of its own - and the counterpart of <see cref="ReadingOrder"/>, which walks a
        /// strip across before it goes down.
        /// </summary>
        public static int TopThenLeft(AgeTransform first, AgeTransform second)
        {
            try
            {
                Rect a = AgeWidgets.LaidOutAt(first);
                Rect b = AgeWidgets.LaidOutAt(second);
                if (Mathf.Abs(a.yMin - b.yMin) > SamePlace)
                {
                    return a.yMin < b.yMin ? -1 : 1;
                }

                return Mathf.Abs(a.xMin - b.xMin) > SamePlace ? (a.xMin < b.xMin ? -1 : 1) : 0;
            }
            catch (Exception e)
            {
                Log.Warn("layout: ordering two widgets threw: " + e);
                return 0;
            }
        }

        private static float Middle(Rect rect)
        {
            return rect.yMin + rect.height * 0.5f;
        }

        private static bool Level(float middle, Rect rect)
        {
            return middle >= rect.yMin && middle <= rect.yMax;
        }
    }
}
