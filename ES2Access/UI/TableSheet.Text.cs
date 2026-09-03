using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.Util;
using UnityEngine;

namespace ES2Access.UI
{
    public sealed partial class TableSheet
    {
        /// <summary>The things one widget is showing, while they are being put in drawing order - the
        /// sheet's own scratch, because this runs for every cell of every row of every build and the
        /// reading is finished before the next one starts.</summary>
        private readonly List<DrawnPart> _drawn = new List<DrawnPart>(4);

        /// <summary>The caption of the column whose cell is being read, while it is: a picture whose
        /// word IS that caption - the population column's population icon - says nothing, because
        /// the heading crossing already said it (owner ruling 2026-09-03). Null outside a captioned
        /// read.</summary>
        private string _echo;

        // ---- reading a cell ----

        /// <summary>What a cell is showing, with the word for showing nothing - the systems table's
        /// Hero and Resources columns are drawn empty for most systems and still have to read.</summary>
        public string CellText(AgeTransform cell)
        {
            return DrawnText(cell) ?? ModStrings.Get(ModStrings.NavCellEmpty);
        }

        /// <summary>The one answer to "what does this cell say": the screen's own reading where it has
        /// one (<see cref="ReadValue"/>), else what the cell draws. Every surface asks it, so the cell,
        /// its buffer line and the row's summary cannot disagree.</summary>
        private string Value(GuiTableHeader header, AgeTransform cell)
        {
            string own = Own(header, cell);
            if (own != null)
            {
                return own;
            }

            _echo = Caption(header);
            try
            {
                return DrawnText(cell);
            }
            finally
            {
                _echo = null;
            }
        }

        /// <summary>The screen's own reading of this cell (<see cref="ReadValue"/>), or null where it
        /// has none - asked ONCE per cell per build and remembered in <see cref="_supplied"/>, because
        /// the same answer decides whether the cell's tooltip is a second thing to read
        /// (<see cref="Supplied"/>) and what the cell, its buffer head and the row's facts all say.
        /// The heading joins the entry so a cell re-read under a different column recomputes rather
        /// than answering for the old one.</summary>
        private string Own(GuiTableHeader header, AgeTransform cell)
        {
            if (ReadValue == null)
            {
                return null;
            }

            KeyValuePair<GuiTableHeader, string> memo;
            if (
                cell != null
                && _supplied.TryGetValue(cell, out memo)
                && ReferenceEquals(memo.Key, header)
            )
            {
                return memo.Value;
            }

            string said = null;
            try
            {
                said = ReadValue(header, cell);
            }
            catch (Exception e)
            {
                Log.Warn("table: reading a column's own value threw: " + e);
            }

            if (cell != null)
            {
                _supplied[cell] = new KeyValuePair<GuiTableHeader, string>(header, said);
            }

            return said;
        }

        private string Text(GuiTableHeader header, AgeTransform cell)
        {
            return Value(header, cell) ?? ModStrings.Get(ModStrings.NavCellEmpty);
        }

        /// <summary>Whether the screen answered for this cell, which is also what says its own tooltip is
        /// not a second thing to read.</summary>
        private bool Supplied(GuiTableHeader header, AgeTransform cell)
        {
            return Own(header, cell) != null;
        }

        /// <summary>Everything the player can see in a cell, or null when it is showing nothing: its
        /// words and its named pictures, in the order they are drawn
        /// (<see cref="Ordered"/>), and - for a column that draws nothing else - the first thing its
        /// tooltips say for themselves.</summary>
        public string DrawnText(AgeTransform cell)
        {
            List<string> tooltips = new List<string>();
            string drawn = null;
            try
            {
                drawn = Drawn(cell, tooltips, MaxCellDepth);
            }
            catch (Exception e)
            {
                Log.Warn("table: reading a column threw: " + e);
            }

            if (!string.IsNullOrEmpty(drawn))
            {
                return drawn;
            }

            if (tooltips.Count > 0)
            {
                return tooltips[0];
            }

            return SortKeyText(cell) ?? DeepText(cell);
        }

        /// <summary>
        /// What a cell whose figure sits DEEPER than <see cref="MaxCellDepth"/> is showing - asked only
        /// of a cell the shallow reading found nothing in at all, so a cell that already reads keeps the
        /// reading it had.
        ///
        /// The systems table's Resources column is such a cell: the game draws a whole
        /// <c>ResourcesPanel</c> inside it and the panel keeps its own pooled item table, so the figure
        /// the player sees is four levels down (cell / ResourcesBanner / ResourceItemsTable /
        /// ResourceIncomeItemList / Net) and the column said the empty word beside a drawn "2".
        ///
        /// The shallow cap cannot simply be raised: the third level of the automation column is that
        /// drop list's CLOSED popup, whose entries would then be read as though the cell were showing
        /// all of them at once. What keeps it out however deep this looks is that a reading stops at
        /// anything the renderer is not painting (<see cref="CollectDrawn"/>): the popup is parked at
        /// ALPHA ZERO with <c>Visible</c> still true, and so is a pooled item the panel retired.
        /// </summary>
        private string DeepText(AgeTransform cell)
        {
            try
            {
                return Drawn(cell, null, DeepCellDepth);
            }
            catch (Exception e)
            {
                Log.Warn("table: reading a column deeper threw: " + e);
                return null;
            }
        }

        /// <summary>
        /// What a cell the game draws as a PORTRAIT is showing, out of the value the game sorts the
        /// column by.
        ///
        /// The assigned-hero column - the systems table's, the fleets table's, the fleet-selection
        /// window's - is a picture and nothing else: no label, and a "Hero" tooltip the tooltip window
        /// assembles, so there is nothing on the widget to read and the cell said the empty word whether a
        /// hero was assigned or not. Which hero, or none, is the only thing the column exists to say.
        ///
        /// The game has already worked the answer out: it writes the assigned hero's own localized name
        /// into the cell's <c>Comparable</c> so the header can sort on it, and the empty string when the
        /// slot is free (<c>GuiTableCellAssignedHero.Refresh</c> :20-63). So that is what is read - the
        /// game's words, kept in step with the portrait by the same Refresh that paints it. An empty
        /// answer stays null, and the shared empty word covers it.
        /// </summary>
        private static string SortKeyText(AgeTransform cell)
        {
            try
            {
                GuiTableCellAssignedHero portrait =
                    cell == null ? null : cell.GetComponent<GuiTableCellAssignedHero>();
                string name =
                    portrait == null ? null : AgeText.Clean(portrait.Comparable as string);
                return string.IsNullOrEmpty(name) ? null : name;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// What a widget is showing, as one sentence: everything drawn inside it that says a word,
        /// read in the order it is drawn.
        ///
        /// The scratch list is the sheet's own and reused - this runs for every cell of every row of
        /// every build - so the pieces are turned into the sentence before anything else is asked.
        /// </summary>
        private string Drawn(AgeTransform widget, List<string> tooltips, int limit)
        {
            return Drawn(widget, tooltips, limit, null);
        }

        /// <summary>The same, leaving out the subtrees rooted at <paramref name="skip"/> - the widgets
        /// a cell's other pieces are read from (<see cref="Piece"/>).</summary>
        private string Drawn(
            AgeTransform widget,
            List<string> tooltips,
            int limit,
            HashSet<AgeTransform> skip
        )
        {
            _drawn.Clear();
            CollectDrawn(widget, tooltips, 0, limit, skip);
            Ordered(_drawn);
            MessageBuilder said = new MessageBuilder();
            for (int i = 0; i < _drawn.Count; i++)
            {
                said.ListItem(_drawn[i].Text);
            }

            _drawn.Clear();
            return said.Build();
        }

        private void CollectDrawn(
            AgeTransform widget,
            List<string> tooltips,
            int depth,
            int limit,
            HashSet<AgeTransform> skip = null
        )
        {
            // Flow control: the walk stops where the renderer stops, so an undrawn branch contributes
            // none of its words - and a FADED one is undrawn. A pooled strip inside a cell retires its
            // surplus items by fading them while leaving them visible, still holding the last binding's
            // picture: the load/save window's content column holds four expansion badges that way, and
            // reading them named four expansions a save does not use.
            if (
                widget == null
                || depth > limit
                || !widget.Visible
                || widget.Alpha <= 0f
                || (skip != null && depth > 0 && skip.Contains(widget))
            )
            {
                return;
            }

            AgePrimitiveLabel label = widget.GetComponent<AgePrimitiveLabel>();
            if (label != null)
            {
                Says(widget, AgeText.Label(label));
            }

            // A picture the icon table has a word for is one of the cell's words: the resources column
            // draws its resource as an icon and the amount as a number beside it, and the number alone
            // is what the column used to say. Everything the table does not name is decoration and
            // answers null, which is what keeps backgrounds and rules out of the reading.
            Says(widget, Unechoed(PictureName(widget)));

            if (depth > 0 && tooltips != null)
            {
                AddTooltip(widget.AgeTooltip, tooltips);
            }

            List<AgeTransform> children = widget.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                CollectDrawn(children[i], tooltips, depth + 1, limit, skip);
            }
        }

        /// <summary>One thing the player can see, remembered with where it is drawn. The rectangle is
        /// asked only of a widget that actually says something, so a cell full of art costs nothing.
        /// </summary>
        private void Says(AgeTransform widget, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            Rect at = widget.GetGlobalPosition();
            _drawn.Add(new DrawnPart(text, at.x, at.y, at.y + at.height));
        }

        /// <summary>What a widget drawing no words is called, where it is drawing something that
        /// stands for a word at all - the icon table is the test (<see cref="IconNames.NameForAsset"/>),
        /// and it names only the pictures that carry meaning.</summary>
        /// <summary>A picture's word, unless it is the caption of the column being read
        /// (<see cref="_echo"/>) - the column's own icon repeated in every cell, which the crossing
        /// into the column already said.</summary>
        private string Unechoed(string picture)
        {
            return picture != null
                && _echo != null
                && string.Equals(picture.Trim(), _echo.Trim(), StringComparison.OrdinalIgnoreCase)
                ? null
                : picture;
        }

        private static string PictureName(AgeTransform widget)
        {
            AgePrimitiveImage image = widget.GetComponent<AgePrimitiveImage>();
            Texture texture = image == null ? null : image.Texture;
            return texture == null ? null : IconNames.NameForAsset(texture.name);
        }

        /// <summary>
        /// The order the player sees: down the lines, and left to right along each of them.
        ///
        /// Tree order is not drawing order - the resources column lays its icon out to the LEFT of the
        /// number and its prefab lists the number first, so the cell said "2 Transvine" for something
        /// drawn as "Transvine 2". Two widgets are on the same line when they overlap vertically, which
        /// is the question a reader's eye asks; a tall icon beside a short number is one line, and a
        /// stacked list is one line each. Both passes are stable, so anything drawn at the same place -
        /// a label and the picture behind it - keeps the order it was collected in.
        /// </summary>
        private static void Ordered(List<DrawnPart> parts)
        {
            SortBy(parts, 0, parts.Count, true);
            int from = 0;
            while (from < parts.Count)
            {
                float bottom = parts[from].Bottom;
                int to = from + 1;
                while (to < parts.Count && parts[to].Middle < bottom)
                {
                    bottom = Math.Min(bottom, parts[to].Bottom);
                    to++;
                }

                SortBy(parts, from, to, false);
                from = to;
            }
        }

        /// <summary>A stable insertion sort over one stretch of the list, by top edge or by left edge.
        /// Insertion because these are handfuls - a cell holds two or three drawn things - and because
        /// the order of two things drawn at the same place has to be the order they were found in.
        /// </summary>
        private static void SortBy(List<DrawnPart> parts, int from, int to, bool vertical)
        {
            for (int i = from + 1; i < to; i++)
            {
                DrawnPart part = parts[i];
                float key = vertical ? part.Top : part.Left;
                int j = i - 1;
                while (j >= from && (vertical ? parts[j].Top : parts[j].Left) > key)
                {
                    parts[j + 1] = parts[j];
                    j--;
                }

                parts[j + 1] = part;
            }
        }

        /// <summary>One word a cell is showing and the rectangle it is drawn in.</summary>
        private struct DrawnPart
        {
            public readonly string Text;
            public readonly float Left;
            public readonly float Top;
            public readonly float Bottom;

            public DrawnPart(string text, float left, float top, float bottom)
            {
                Text = text;
                Left = left;
                Top = top;
                Bottom = bottom;
            }

            public float Middle
            {
                get { return (Top + Bottom) * 0.5f; }
            }
        }

        /// <summary>The words hanging off the things drawn INSIDE a cell, for the buffer. The cell's
        /// own tooltip is not among them: it is declared as the control's tooltip and reaches both
        /// surfaces from there. Only the tooltips whose words are ON the widget: the class-backed ones
        /// have no words until they are drawn, and reach the buffer as SECTIONS instead
        /// (<see cref="Inside"/>).</summary>
        private void CollectTooltips(AgeTransform widget, List<string> into, int depth)
        {
            if (widget == null || depth > MaxCellDepth || !widget.Visible)
            {
                return;
            }

            AddTooltip(widget.AgeTooltip, into);
            List<AgeTransform> children = widget.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                CollectTooltips(children[i], into, depth + 1);
            }
        }

        /// <summary>A tooltip's words, but only where the words are actually in it: the ones these
        /// tables hang on their number columns name a simulation property and are assembled by the
        /// tooltip window at draw time, so there is nothing in them to read off the widget.</summary>
        private static void AddTooltip(AgeTooltip tooltip, List<string> into)
        {
            if (AgeWidgets.Readable(tooltip) == null)
            {
                return;
            }

            IList<string> lines = AgeText.Lines(AgeText.Tooltip(tooltip));
            for (int i = 0; i < lines.Count; i++)
            {
                if (!into.Contains(lines[i]))
                {
                    into.Add(lines[i]);
                }
            }
        }

        /// <summary>
        /// The RENDERER-ASSEMBLED tooltips hanging on the things drawn inside a cell.
        ///
        /// A cell's own tooltip has always been declared, and the words hanging on its pieces have
        /// always been read (<see cref="CollectTooltips"/>) - but only where those words are on the
        /// widget. A class-backed one inside a cell has no words until it is drawn, so reading it as
        /// text answered "nothing" and the dossier the game hangs on a status circle or a growth arrow
        /// was dropped without trace. Declared as its own section instead, which is the surface that
        /// can wait for the drawing.
        ///
        /// The cell's own is excluded here (the caller declares it), and so is anything equal to it -
        /// a table that names its tooltip through <c>GuiTableCell.Tooltip</c> may be naming one that
        /// hangs on a piece INSIDE the cell.
        /// </summary>
        /// <summary>The last renderer-assembled dossier drawn inside a cell, or null - the one the
        /// pointer goes to where the cell has no tooltip of its own.</summary>
        private AgeTooltip LastInside(AgeTransform cell)
        {
            List<AgeTooltip> inner = Inside(cell, null);
            return inner == null ? null : inner[inner.Count - 1];
        }

        private List<AgeTooltip> Inside(AgeTransform cell, AgeTooltip own)
        {
            List<AgeTooltip> found = Hovers(cell);
            List<AgeTooltip> kept = null;
            for (int i = 0; i < found.Count; i++)
            {
                if (
                    AgeWidgets.Readable(found[i]) != null
                    || AgeWidgets.SameTooltip(found[i], own)
                )
                {
                    continue;
                }

                if (kept == null)
                {
                    kept = new List<AgeTooltip>(found.Count - i);
                }

                kept.Add(found[i]);
            }

            return kept;
        }

        /// <summary>Every hover surface hanging inside a cell, subtree walked ONCE per cell per build
        /// (<see cref="_hovers"/>). The walk is the expensive half of <see cref="Inside"/> and its
        /// answer does not depend on which tooltip the caller is excluding, so the filtering stays per
        /// call and only the walk is remembered.
        ///
        /// PAINTED, because these answers are the cell's columns and the surface its pointer goes to:
        /// a cell that holds a pooled list of its own - the resources column's item table - retires a
        /// surplus item by fading it to nothing while leaving it visible, still carrying the dossier of
        /// the resource it last held, so a walk by visibility alone gives a one-resource system a
        /// second column about a resource it does not have.
        ///
        /// DEEP, for the same reason <see cref="DeepText"/> is: a cell holding a whole panel of its own
        /// keeps the thing the player points at several levels down - the resources column's dossiers
        /// sit three levels under the cell, and the shallow cap that keeps a closed drop list's entries
        /// out of the cell's WORDS was leaving every one of them unreachable. The cap is not what keeps
        /// that popup out here; being unpainted is.</summary>
        private List<AgeTooltip> Hovers(AgeTransform cell)
        {
            List<AgeTooltip> found;
            if (cell != null && _hovers.TryGetValue(cell, out found))
            {
                return found;
            }

            found = new List<AgeTooltip>();
            AgeWidgets.EffectiveTooltips(
                cell,
                found,
                TooltipReach.Descendants | TooltipReach.Painted,
                DeepCellDepth
            );
            if (cell != null)
            {
                _hovers[cell] = found;
            }

            return found;
        }
    }
}
