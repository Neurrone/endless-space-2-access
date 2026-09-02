using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    public sealed partial class TableSheet
    {
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
            return Own(header, cell) ?? DrawnText(cell);
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
        /// words, and - for a column drawn as a picture, like the automation icon - the first thing its
        /// pictures say for themselves.</summary>
        public string DrawnText(AgeTransform cell)
        {
            MessageBuilder labels = new MessageBuilder();
            List<string> tooltips = new List<string>();
            try
            {
                CollectDrawn(cell, labels, tooltips, 0, MaxCellDepth, false);
            }
            catch (Exception e)
            {
                Log.Warn("table: reading a column threw: " + e);
            }

            string drawn = labels.Build();
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
        /// all of them at once. The popup is parked at ALPHA ZERO with <c>Visible</c> still true, so
        /// this pass is painted-only, which leaves it out however deep it looks - and leaves out a
        /// pooled item the panel retired the same way.
        /// </summary>
        private string DeepText(AgeTransform cell)
        {
            MessageBuilder labels = new MessageBuilder();
            try
            {
                CollectDrawn(cell, labels, null, 0, DeepCellDepth, true);
            }
            catch (Exception e)
            {
                Log.Warn("table: reading a column deeper threw: " + e);
            }

            string drawn = labels.Build();
            return string.IsNullOrEmpty(drawn) ? null : drawn;
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

        private void CollectDrawn(
            AgeTransform widget,
            MessageBuilder labels,
            List<string> tooltips,
            int depth,
            int limit,
            bool paintedOnly
        )
        {
            if (widget == null || depth > limit || !widget.Visible)
            {
                return;
            }

            if (paintedOnly && widget.Alpha <= 0f)
            {
                return;
            }

            AgePrimitiveLabel label = widget.GetComponent<AgePrimitiveLabel>();
            if (label != null)
            {
                labels.ListItem(AgeText.Label(label));
            }

            if (depth > 0 && tooltips != null)
            {
                AddTooltip(widget.AgeTooltip, tooltips);
            }

            List<AgeTransform> children = widget.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                CollectDrawn(children[i], labels, tooltips, depth + 1, limit, paintedOnly);
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
        /// call and only the walk is remembered.</summary>
        private List<AgeTooltip> Hovers(AgeTransform cell)
        {
            List<AgeTooltip> found;
            if (cell != null && _hovers.TryGetValue(cell, out found))
            {
                return found;
            }

            found = new List<AgeTooltip>();
            AgeWidgets.EffectiveTooltips(cell, found, TooltipReach.Descendants, MaxCellDepth);
            if (cell != null)
            {
                _hovers[cell] = found;
            }

            return found;
        }
    }
}
