using System;
using System.Collections.Generic;

namespace ES2Access.UI
{
    public static partial class AgeWidgets
    {
        /// <summary>
        /// Everything a panel the mod has NOT modelled widget by widget is showing, one line per thing
        /// it says: the text of every label it draws, and the words of every tooltip whose words are on
        /// the widget rather than composed by a renderer.
        ///
        /// For a read-only panel of a shape the mod has no model for - a lens's own overlay, an
        /// out-of-fixture variant - this is the whole reading, and it costs nothing per screen. It is
        /// deliberately NOT a substitute for modelling a panel the player has to work: it produces
        /// lines, not controls, and it says nothing about which line belongs to which control.
        ///
        /// A line is dropped when it only repeats the line before it, which is what the game's habit of
        /// drawing the same words on a group and on the label inside it would otherwise produce.
        /// </summary>
        public static IList<string> DrawnLines(AgeTransform widget, int maxDepth = 8)
        {
            List<string> lines = new List<string>();
            CollectLines(widget, lines, maxDepth, false);
            return lines;
        }

        /// <summary>The same reading for a panel whose lines the game FADES rather than hides - the scan
        /// view's map labels, where a whole line of a label is switched off for the layer the camera is on
        /// by animating its alpha to nothing and leaving it marked visible. Reading such a panel by
        /// visibility alone announces a line the player cannot see; see <see cref="Painted"/> for the same
        /// rule applied to pooled tables and <see cref="PaintedText"/> for the one-phrase form.</summary>
        public static IList<string> PaintedLines(AgeTransform widget, int maxDepth = 8)
        {
            List<string> lines = new List<string>();
            CollectLines(widget, lines, maxDepth, true);
            return lines;
        }

        private static void CollectLines(
            AgeTransform widget,
            List<string> lines,
            int depth,
            bool paintedOnly
        )
        {
            if (widget == null || depth < 0)
            {
                return;
            }

            try
            {
                if (!widget.Visible || (paintedOnly && widget.Alpha <= 0f))
                {
                    return;
                }

                AgePrimitiveLabel label = widget.GetComponent<AgePrimitiveLabel>();
                Add(lines, label == null ? null : AgeText.Label(label));
                // Only the words written onto the tooltip itself (the same gate and split
                // <see cref="TooltipLines"/> uses for its first half), never the drawn tooltip
                // window: this walk reads an unmodelled panel, and the window draws whatever the
                // pointer is parked on, which is not this panel's text.
                IList<string> words = AgeText.ContentLines(Readable(Raw(widget)));
                for (int i = 0; words != null && i < words.Count; i++)
                {
                    Add(lines, words[i]);
                }

                IList<AgeTransform> children = widget.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    CollectLines(children[i], lines, depth - 1, paintedOnly);
                }
            }
            catch (Exception) { }
        }

        private static void Add(List<string> lines, string line)
        {
            if (!string.IsNullOrEmpty(line) && (lines.Count == 0 || lines[lines.Count - 1] != line))
            {
                lines.Add(line);
            }
        }

        /// <summary>Every text a widget draws, in one phrase - the caption of a group whose words the
        /// game spreads over an icon, a number and a label. It is read off the LABELS and nothing
        /// else: an icon token inside label text is named, a standalone icon widget beside them is
        /// not read at all.
        /// </summary>
        public static string TextOf(AgeTransform widget, int maxDepth = 6)
        {
            List<string> parts = new List<string>();
            Collect(widget, parts, maxDepth);
            return Phrase(parts);
        }

        /// <summary>The same reading, for a widget whose words come out of a POOLED table: the rows the
        /// game retired by fading them to nothing are left out, so the phrase is what is on the screen.
        /// See <see cref="Painted"/> for why <see cref="TextOf"/> cannot answer this on its own, and why
        /// it is asked here rather than everywhere.</summary>
        public static string PaintedText(AgeTransform widget, int maxDepth = 6)
        {
            List<string> parts = new List<string>();
            Collect(widget, parts, maxDepth, true);
            return Phrase(parts);
        }

        /// <summary>
        /// The same reading for a widget a walk has ALREADY vouched for, asking the painted question
        /// only of the pieces BELOW it.
        ///
        /// <see cref="PaintedText"/> also asks the widget's OWN alpha, which a walk that came down
        /// through <see cref="DrawnChild"/> has already settled - and which would read a container
        /// fading ITSELF in as wordless, the failure <see cref="Paints"/> exists to avoid. What is left
        /// is the case a leaf reading hits: a group at full alpha whose only words are on a POOLED row
        /// the game retired by fading it - the ship design costs box, where the group kept for a
        /// strategic-resource row still holds "1 Adamantian" for a design that costs no strategic
        /// resource. Such a widget reads as nothing, and the caller's empty-text early-out drops it.
        /// </summary>
        public static string PaintedPartsText(AgeTransform widget, int maxDepth = 6)
        {
            List<string> parts = new List<string>();
            try
            {
                if (widget != null && widget.Visible)
                {
                    AddLabel(widget, parts);
                    IList<AgeTransform> children = widget.Children;
                    for (int i = 0; children != null && i < children.Count; i++)
                    {
                        Collect(children[i], parts, maxDepth - 1, true);
                    }
                }
            }
            catch (Exception) { }

            return Phrase(parts);
        }

        private static string Phrase(List<string> parts)
        {
            Core.Speech.MessageBuilder message = new Core.Speech.MessageBuilder();
            for (int i = 0; i < parts.Count; i++)
            {
                message.Fragment(parts[i]);
            }

            return message.Build();
        }

        /// <summary>
        /// What a label SAYS while the game is drawing it, and null where it is not.
        ///
        /// The drawn test belongs here rather than at the call site because this game hides a label
        /// without clearing it: an unguarded read answers with the words the label was last bound
        /// with, so a panel that has moved on speaks the PREVIOUS binding's figure as though it were
        /// still on the screen.
        ///
        /// null IS the not-drawn answer; a drawn blank label answers empty. A caller that only wants
        /// to know whether there is anything to say may treat the two alike, but one that wraps the
        /// value in a sentence of its own must test null - the game draws labels it has nothing to
        /// write into, and formatting an empty one says the sentence with a hole in it.
        /// </summary>
        public static string DrawnLabel(AgePrimitiveLabel label)
        {
            try
            {
                return label == null || !Visible(label.AgeTransform)
                    ? null
                    : AgeText.Label(label) ?? string.Empty;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The same answer for a label the panel shows and hides by the GROUP around it
        /// rather than by the label itself: the drawn question is asked of <paramref name="gate"/>,
        /// which is the thing the game switches, while the words come off the label inside it.
        /// </summary>
        public static string DrawnLabel(AgeTransform gate, AgePrimitiveLabel label)
        {
            try
            {
                return label == null || !Visible(gate)
                    ? null
                    : AgeText.Label(label) ?? string.Empty;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Every text a widget draws in one phrase - <see cref="TextOf"/> - but only while the game
        /// is drawing the widget, and null where it is not.
        ///
        /// <see cref="TextOf"/> asks each level's own visible flag as it descends, so what the guard
        /// adds is the ANCESTRY above the widget: a group the window has collapsed leaves the block
        /// inside it marked visible and still holding its words, and reading one ungated captions a
        /// region with the previous binding's stale heading. null is the not-drawn answer; a drawn
        /// widget with nothing written on it answers the empty phrase <see cref="TextOf"/> gives.
        /// </summary>
        public static string DrawnText(AgeTransform widget, int maxDepth = 6)
        {
            try
            {
                return widget == null || !Visible(widget) ? null : TextOf(widget, maxDepth);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The widget a label is drawn on while the game is drawing it, else null - the same
        /// question <see cref="DrawnLabel(AgePrimitiveLabel)"/> answers, for a caller whose answer is
        /// the WIDGET rather than the words on it and whose null means "the window drew no such
        /// thing".</summary>
        public static AgeTransform Drawn(AgePrimitiveLabel label)
        {
            try
            {
                AgeTransform at = label == null ? null : label.AgeTransform;
                return Visible(at) ? at : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void Collect(
            AgeTransform widget,
            List<string> parts,
            int depth,
            bool paintedOnly = false
        )
        {
            if (widget == null || depth < 0)
            {
                return;
            }

            try
            {
                if (!widget.Visible || (paintedOnly && widget.Alpha <= 0f))
                {
                    return;
                }

                AddLabel(widget, parts);
                IList<AgeTransform> children = widget.Children;
                if (children == null)
                {
                    return;
                }

                for (int i = 0; i < children.Count; i++)
                {
                    Collect(children[i], parts, depth - 1, paintedOnly);
                }
            }
            catch (Exception) { }
        }

        private static void AddLabel(AgeTransform widget, List<string> parts)
        {
            AgePrimitiveLabel label = widget.GetComponent<AgePrimitiveLabel>();
            if (label == null)
            {
                return;
            }

            string text = AgeText.Label(label);
            if (!string.IsNullOrEmpty(text) && !parts.Contains(text))
            {
                parts.Add(text);
            }
        }

        /// <summary>
        /// What one item of a table SAYS: the words it draws, or - for an item the game draws as a bare
        /// icon and names nowhere on itself - the title of the wrapper it hangs on its own tooltip.
        ///
        /// Tables of findings (anomalies, curiosities, resource deposits) are rows of wordless pictures,
        /// and reading them as text is silence: three panels contributed NOTHING at all until this asked
        /// the wrapper. The wrapper is where the game keeps the name it would have written, so this is
        /// the same answer <see cref="TooltipTitle"/> gives a control, extended down a couple of levels
        /// because a table item routinely hangs its tooltip on the image inside it rather than on the
        /// item.
        /// </summary>
        public static string ItemText(AgeTransform widget)
        {
            // A pooled table with StrictVisibility off retires a surplus child by parking it at
            // Alpha 0 with Visible still true (AgeTransform.RefreshChildrenIList), and the parked
            // item keeps its old wrapper on its tooltip - so an item that draws nothing must say
            // nothing, or it answers with the previous binding's name.
            if (!Paints(widget))
            {
                return null;
            }

            string drawn = TextOf(widget);
            if (!string.IsNullOrEmpty(drawn))
            {
                return drawn;
            }

            return WrapperTitle(widget, 3);
        }

        private static string WrapperTitle(AgeTransform widget, int depth)
        {
            if (widget == null || depth < 0)
            {
                return null;
            }

            try
            {
                if (!Paints(widget))
                {
                    return null;
                }

                string title = TooltipTitle(Raw(widget));
                if (!string.IsNullOrEmpty(title))
                {
                    return title;
                }

                IList<AgeTransform> children = widget.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    title = WrapperTitle(children[i], depth - 1);
                    if (!string.IsNullOrEmpty(title))
                    {
                        return title;
                    }
                }
            }
            catch (Exception) { }

            return null;
        }
    }
}
