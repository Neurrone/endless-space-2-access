using ES2Access.Core.UI.Graph;

namespace ES2Access.UI
{
    /// <summary>
    /// The word a surface DRAWS over a block of content, declared the one way it should be.
    ///
    /// A caption that titles a block is the block's NAME - a level the announcer says on the way in
    /// and never again while the player walks inside it - and not a row of its own: a node whose
    /// whole content is a word the next node already sits under is a step the player has to take
    /// past nothing (owner ruling 2026-08-22, generalised from the minor-diplomacy design).
    ///
    /// The one exception is a caption the game hung an EXPLANATION on. The block's name is a spoken
    /// phrase with no review buffer behind it, so a caption that carries a tooltip has nowhere else
    /// to put its words: it stays a row as well, as the panel headings on the economy, senate and
    /// recipe windows already did. Saying the word twice costs nothing - the announcer drops a level
    /// whose label the node below it repeats (<c>GraphAnnouncer.DuplicatesNext</c>).
    ///
    /// Which of the two a caption is, is asked of the WIDGET every build rather than decided per
    /// screen: the same prefab caption carries a sentence on one window and nothing on the next, and
    /// a screen that hard-codes the answer is wrong on half of them.
    ///
    /// Hand this the caption's GROUP, not the label inside it. ES2's prefabs routinely draw the word
    /// on a label and hang the sentence on the wrapper around it (ES2 facts, "A block caption's WORD
    /// and its EXPLANATION sit on different widgets"), and <see cref="AgeWidgets.TextOf"/> descends -
    /// so the group answers both questions and the label answers only one, losing the sentence
    /// silently. Find the group from the label's own unique prefab name and take its parent; the
    /// wrappers themselves share names across a window.
    ///
    /// Where the wrapper is the whole BLOCK rather than a title box - the population window's
    /// effects panels hang the caption's sentence on the panel that holds the effect lines too -
    /// descending would swallow the block into its own name, so the caller passes the label as the
    /// word and the block as <c>group</c>. The row then says the label and points at the block,
    /// which is where the game draws that sentence.
    ///
    /// And before guessing at a wrapper at all, ask the panel CLASS for a tooltip field: several
    /// prefabs expose the caption's sentence as a named serialized field hung nowhere the tree
    /// search looks (<c>NewGameCategoryPanel.CategoryTooltip</c> is the precedent).
    /// </summary>
    public static class Captions
    {
        /// <summary>
        /// Name the block being declared after the caption <paramref name="widget"/> draws, and
        /// declare that caption as the block's first row where it has an explanation to offer.
        ///
        /// Answers whether a level was pushed; hand that back to <see cref="Pop"/>. A caption the
        /// game drew nothing in pushes nothing, so no block is announced under a blank word.
        ///
        /// <paramref name="key"/> is the row's key, and null is the caller saying it will never want
        /// the row (a caption already declared elsewhere).
        ///
        /// <paramref name="group"/> is the block the caption titles, for the prefabs that hang the
        /// sentence there rather than on the label. Only the sentence comes from it: the row still
        /// says the caption's own word.
        /// </summary>
        public static bool Push(
            GraphBuilder builder,
            AgeTransform widget,
            object key = null,
            string text = null,
            AgeTransform group = null
        )
        {
            if (builder == null)
            {
                return false;
            }

            string word = text ?? Text(widget);
            if (string.IsNullOrEmpty(word))
            {
                return false;
            }

            builder.PushContext(word);
            Row(builder, widget, key, group);
            return true;
        }

        /// <summary>
        /// The caption as a row, with no block named after it - for a surface that has already named
        /// the block some other way: a table whose region its own sheet names, a panel the page gives
        /// a word of its own, a caption that is a node in its own right.
        ///
        /// A caption the game hung no explanation on gets no row, which is the whole rule: its word is
        /// the block's name and a node whose content is that same word is a step past nothing.
        /// Answers whether a row was declared.
        /// </summary>
        public static bool Row(
            GraphBuilder builder,
            AgeTransform widget,
            object key,
            AgeTransform group = null
        )
        {
            AgeTooltip tooltip = Explanation(widget) ?? Explanation(group);
            // The caption's own widget is what the row SAYS, whichever of the two the sentence turned
            // out to be on: a row with a sentence and no word is a stop the player cannot name.
            if (builder == null || key == null || tooltip == null || widget == null)
            {
                return false;
            }

            Cell cell = Cells.Readout(widget, tooltip, key.ToString());
            builder.AddItem(Nodes.Drawn(cell.Id, cell.Vtable, cell.Widget));
            return true;
        }

        /// <summary>The sentence a widget carries, where the game would draw one for it - and is
        /// drawing the widget at all, so a caption the game has put away is no row.</summary>
        private static AgeTooltip Explanation(AgeTransform widget)
        {
            // Asked of the caption AND of the block around it, so this decides WHICH of two widgets
            // supplies the sentence - and the block is not the widget the row stands on, which is
            // where the gate's ancestry walk would have covered it.
            AgeTooltip tooltip = AgeWidgets.Visible(widget) ? AgeWidgets.Raw(widget) : null;
            return tooltip != null && AgeWidgets.Draws(tooltip) ? tooltip : null;
        }

        /// <summary>Close the block's name off again, so what is declared next is not declared inside
        /// it.</summary>
        public static void Pop(GraphBuilder builder, bool pushed)
        {
            if (pushed && builder != null)
            {
                builder.PopContext();
            }
        }

        /// <summary>What the caption says, or null where the game is not drawing it.</summary>
        public static string Text(AgeTransform widget)
        {
            return AgeWidgets.DrawnText(widget);
        }
    }
}
