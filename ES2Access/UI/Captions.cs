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
        /// </summary>
        public static bool Push(
            GraphBuilder builder,
            AgeTransform widget,
            object key = null,
            string text = null
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
            AgeTooltip tooltip = widget == null ? null : AgeWidgets.Raw(widget);
            if (key != null && tooltip != null && AgeWidgets.Draws(tooltip))
            {
                Cell cell = Cells.Readout(widget, tooltip, key.ToString());
                builder.AddItem(cell.Id, cell.Vtable);
            }

            return true;
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
            return widget == null || !AgeWidgets.Visible(widget)
                ? null
                : AgeWidgets.TextOf(widget);
        }
    }
}
