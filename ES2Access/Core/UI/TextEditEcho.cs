using ES2Access.Core.Speech;

namespace ES2Access.Core.UI
{
    /// <summary>What one frame of a live text edit did, in the words to say for it.</summary>
    public enum EditEchoKind
    {
        /// <summary>Nothing worth saying: the text and the caret are where they were, or the game
        /// rewrote the whole box for reasons of its own.</summary>
        None,

        /// <summary>Characters arrived at the caret.</summary>
        Typed,

        /// <summary>Characters were removed at the caret - a Backspace, a forward Delete.</summary>
        Deleted,

        /// <summary>The text is unchanged and the caret moved.</summary>
        Caret,
    }

    /// <summary>One frame's worth of edit, and the line to speak for it.</summary>
    public struct EditEcho
    {
        public EditEchoKind Kind;

        /// <summary>Already in the form the player hears it: a printable character as itself, a space
        /// and an end-of-text caret as the words for them. Null when <see cref="Kind"/> is
        /// <see cref="EditEchoKind.None"/>.</summary>
        public string Text;
    }

    /// <summary>
    /// What a single-line text box just did, worked out by comparing where it was with where it is.
    ///
    /// The mod echoes typing itself rather than leaving it to the screen reader: nothing else is
    /// reading these boxes, because the game draws its own caret into a widget no accessibility API
    /// knows about. So every frame of an edit is a before/after pair - the text and the caret offset -
    /// and this is the whole decision of what that pair means. Kept here, off the engine, because it
    /// is pure string arithmetic and because getting it wrong is silent.
    ///
    /// The engine this was written against has no selection: a caret, insertions at it, and removals
    /// either side of it are the only edits that exist (<c>AgeControlTextArea</c>). That is what makes
    /// the arithmetic decidable - any change that is NOT one contiguous insert or remove at the caret
    /// is the GAME rewriting the box (a quantity clamped on the way out, a chat line sent and the box
    /// emptied, a history entry recalled), and those must not be read out as if the player had typed
    /// them.
    ///
    /// One judgement inside that rule: emptying a box of more than one character in a single frame is
    /// treated as a rewrite rather than as a deletion, because no keystroke can do it - Backspace
    /// takes one character and the word-delete takes one word, and a whole-box clear is always the
    /// game's. A word-delete that happens to empty the box is the price, and it is silent.
    /// </summary>
    public static class TextEditEcho
    {
        /// <summary>
        /// What changed between two readings of the same box. <paramref name="oldCaret"/> and
        /// <paramref name="newCaret"/> are offsets into their own text and are clamped, so a caret the
        /// engine has not computed yet (it parks it past the end) reads as end-of-text.
        /// </summary>
        public static EditEcho Since(
            string oldText,
            int oldCaret,
            string newText,
            int newCaret
        )
        {
            string before = oldText ?? string.Empty;
            string after = newText ?? string.Empty;
            int from = Caret(before, oldCaret);
            int to = Caret(after, newCaret);

            if (before == after)
            {
                return from == to ? Nothing : Echo(EditEchoKind.Caret, CharacterAt(after, to));
            }

            if (after.Length > before.Length)
            {
                int added = after.Length - before.Length;
                int at = to - added;
                if (at >= 0 && at <= before.Length && after.Remove(at, added) == before)
                {
                    return Echo(EditEchoKind.Typed, Words(after.Substring(at, added)));
                }

                return Nothing;
            }

            int removed = before.Length - after.Length;
            if (to > before.Length - removed || before.Remove(to, removed) != after)
            {
                return Nothing;
            }

            // A box emptied in one frame is the game's doing, not a keystroke's - see the class note.
            if (removed > 1 && after.Length == 0)
            {
                return Nothing;
            }

            return Echo(EditEchoKind.Deleted, Words(before.Substring(to, removed)));
        }

        /// <summary>
        /// The character the caret is sitting on, as the player should hear it: the character itself,
        /// the word for a space, or the word for the empty place past the last character.
        /// </summary>
        public static string CharacterAt(string text, int caret)
        {
            string it = text ?? string.Empty;
            int at = Caret(it, caret);
            return at >= it.Length
                ? ModStrings.Get(ModStrings.EditCaretBlank)
                : Words(it.Substring(at, 1));
        }

        /// <summary>A run of typed or deleted text as it is spoken. A lone space has no sound of its
        /// own, so it is named; everything else is said as it is written.</summary>
        private static string Words(string text)
        {
            return text == " " ? ModStrings.Get(ModStrings.EditCaretSpace) : text;
        }

        private static int Caret(string text, int caret)
        {
            if (caret < 0)
            {
                return 0;
            }

            return caret > text.Length ? text.Length : caret;
        }

        private static EditEcho Echo(EditEchoKind kind, string text)
        {
            EditEcho echo = new EditEcho();
            echo.Kind = kind;
            echo.Text = text;
            return echo;
        }

        private static readonly EditEcho Nothing = new EditEcho();
    }
}
