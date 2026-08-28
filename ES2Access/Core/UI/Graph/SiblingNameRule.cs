using System;
using System.Collections.Generic;
using ES2Access.Core.Util;

namespace ES2Access.Core.UI.Graph
{
    /// <summary>
    /// Which rung of its own naming ladder a nested entry answers to, when a SIBLING answers to the
    /// same word.
    ///
    /// A nested tooltip entry is named after the widget a mouse would hover to raise it, in that
    /// widget's own drawn words, with a ladder of fallbacks behind it for the widget that draws none
    /// (the wrapper's title, the sentence's first line). The words are the GAME's, so nothing stops
    /// one card from drawing the same word twice: the senate's winner card writes the party's name
    /// over the portrait group AND on the party-name label beside it, while the dossier the portrait
    /// raises is the seated hero's own page. Named off the drawn words alone the player hears
    /// "Industrialists" twice, and the page that is hidden behind the repeat is the hero's.
    ///
    /// So a COLLISION is what makes the rest of an entry's ladder speak: an entry whose name another
    /// entry of the same set also answers to takes the first rung of its OWN ladder that says
    /// something else - here the portrait wrapper's title, "Dmitri Lenko". An entry with no such rung
    /// keeps the shared name: the ladder may only report words the game wrote, never invent a better
    /// one, and the player still has the entries' order to tell them apart (both senate cards whose
    /// two widgets really do explain the same party are that case).
    ///
    /// Collisions are judged against every sibling's OWN first answer, never against a name another
    /// entry has already moved to. Each entry is therefore decided independently and reading them in
    /// any order gives the same set of names - and one entry stepping down its ladder never renames
    /// the entry it collided with.
    ///
    /// A ladder is asked rung by rung, on demand, because the answers are read off live widgets and
    /// the rungs past the first cost a text walk apiece: a set with no collision in it asks each
    /// sibling exactly as much as naming it alone would.
    /// </summary>
    public static class SiblingNameRule
    {
        /// <summary>
        /// What entry <paramref name="index"/> is called, given every entry's ladder.
        ///
        /// <paramref name="ladders"/> is the sibling set in declared order, each a function from rung
        /// number to that rung's answer (null or empty where the rung has none);
        /// <paramref name="rungs"/> is how many rungs the ladder has.
        /// </summary>
        public static string Name(IList<Func<int, string>> ladders, int index, int rungs)
        {
            if (ladders == null || index < 0 || index >= ladders.Count)
            {
                return null;
            }

            string mine = First(ladders[index], rungs);
            string folded = TextUtil.LettersAndDigits(mine);
            if (folded.Length == 0 || !Collides(ladders, index, rungs, folded))
            {
                return mine;
            }

            for (int rung = 0; rung < rungs; rung++)
            {
                string answer = Answer(ladders[index], rung);
                if (answer != null && TextUtil.LettersAndDigits(answer) != folded)
                {
                    return answer;
                }
            }

            return mine;
        }

        /// <summary>The first rung of one ladder that answers anything - what an entry with no sibling
        /// to be confused with is called, and the answer collisions are judged on.</summary>
        public static string First(Func<int, string> ladder, int rungs)
        {
            for (int rung = 0; rung < rungs; rung++)
            {
                string answer = Answer(ladder, rung);
                if (answer != null)
                {
                    return answer;
                }
            }

            return null;
        }

        private static bool Collides(
            IList<Func<int, string>> ladders,
            int index,
            int rungs,
            string folded
        )
        {
            for (int i = 0; i < ladders.Count; i++)
            {
                if (i != index && TextUtil.LettersAndDigits(First(ladders[i], rungs)) == folded)
                {
                    return true;
                }
            }

            return false;
        }

        // A rung that answers whitespace answers nothing: the caller's rungs read text off widgets, and
        // a widget drawing a blank string is not a widget that named anything.
        private static string Answer(Func<int, string> ladder, int rung)
        {
            string answer = ladder == null ? null : ladder(rung);
            return TextUtil.IsBlank(answer) ? null : answer;
        }
    }
}
