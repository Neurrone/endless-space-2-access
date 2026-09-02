using System;
using System.Collections.Generic;
using System.Text;

namespace ES2Access.UI
{
    /// <summary>
    /// Turns a raw AGE string into something worth reading aloud, in the one place both the screen
    /// adapters and the dev dump go through.
    ///
    /// Five steps, in order: prefer a label's already-resolved <c>TranslatedText</c>; resolve a
    /// leftover <c>%key</c> through the game's localizer (tooltips and deferred content routinely
    /// still hold keys); substitute every inline <c>[token]</c> icon with its name (see
    /// <see cref="IconNames"/>) before anything else can destroy the bracket text that names it; run
    /// the engine's own <see cref="AgeUtils.CleanLine"/>, which expands any icon tokens still left
    /// (ones this mod found no name for) into picture glyphs and strips the <c>#RRGGBBAA#</c> colour
    /// markup the labels carry; then drop whatever glyphs remain, which live in the font's private use
    /// area and mean nothing to a screen reader.
    ///
    /// Newlines survive — the game separates a disabled button's reason from its description with a
    /// blank line, and a screen reader reads that as the pause it is. Every step is guarded: a
    /// getter that throws costs the text, not the frame.
    ///
    /// Main-thread only (it reads live scene objects and shares one scratch buffer).
    /// </summary>
    public static class AgeText
    {
        // The font's private use area, where the [Icon] tokens expand to picture glyphs.
        private const char PrivateUseFirst = '';
        private const char PrivateUseLast = '';

        private static readonly StringBuilder CleanBuffer = new StringBuilder(512);

        /// <summary>The spoken form of a raw AGE string, or null when nothing is left to say.</summary>
        public static string Clean(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return null;
            }

            string text = raw;
            try
            {
                if (Gui.IsLocalizationKey(text))
                {
                    text = Gui.Localize(text);
                }
            }
            catch (Exception) { }

            text = SubstituteIcons(text);

            try
            {
                StringBuilder buffer = CleanBuffer;
                AgeUtils.CleanLine(text, ref buffer);
                text = buffer.ToString();
            }
            catch (Exception) { }

            return StripUnspeakable(text);
        }

        /// <summary>
        /// The game's own word for a key it keeps in its corpus - a column heading, a card's caption,
        /// the name of a fact it drew only as a picture - or null where the corpus has no such string.
        ///
        /// The null is the whole point, and is why this is one place rather than twenty-five. The
        /// localizer hands an unknown key straight BACK, so a caller that speaks the answer unguarded
        /// reads "%HeroCardLevelTitle" aloud; eight sites did. A key the game never finished writing is
        /// silence: the caller's own fallback - a different key, a drawn label, nothing at all - is a
        /// better line than the key.
        ///
        /// Empty is the same answer as unresolved, for the same reason. The localize step is
        /// <see cref="Clean"/>'s own, which is also what strips the colour markup and names the inline
        /// icons, so a title reads exactly as any other AGE string does.
        /// </summary>
        public static string Title(string key)
        {
            string title = Clean(key);
            return string.IsNullOrEmpty(title) || Gui.IsLocalizationKey(title) ? null : title;
        }

        /// <summary>
        /// A label's spoken text. <c>TranslatedText</c> is the post-localization, post-markup string
        /// the label actually renders; <c>Text</c> is what was assigned, which for a data-driven
        /// caption is still a <c>%key</c>.
        ///
        /// An inline icon token is the exception. The engine builds <c>TranslatedText</c> by running
        /// its OWN markup pass over <c>Text</c> the moment the label is set - the same pass
        /// <see cref="AgeUtils.CleanLine"/> performs, done once and cached rather than done again on
        /// every read - and for a <c>[token]</c> icon that pass throws the bracket away entirely
        /// rather than leaving a glyph character behind, so <c>TranslatedText</c> already carries
        /// nothing this class could substitute a name into ("Enables exploitation of
        /// #3c98ff##REVERT#" - the color wrapper survives, the icon does not). <c>Text</c> is read
        /// first whenever it still holds a bracket, precisely so <see cref="Clean"/> gets a chance to
        /// substitute the icon's name before anything destroys it - the same reasoning
        /// <see cref="Tooltip(AgeTooltip)"/> already reads <c>Content</c> untouched for.
        ///
        /// A <c>%key</c> hides that same loss one level down. The bracket is not in <c>Text</c> at
        /// all - it is in what the key TRANSLATES to - so the test above passes the label through to
        /// <c>TranslatedText</c>, which the localizer has already run its icon pass over: the game's
        /// own description of a system's approval says the words "can influence the [food] and
        /// [influence] outputs" and the label renders "can influence the and outputs". Resolving the
        /// key here, exactly as <see cref="LabelWithoutLeadingIcon"/> and <see cref="FullLabel"/>
        /// already do, keeps the brackets long enough to name them. Only for a translation that
        /// really does carry one: everywhere else <c>TranslatedText</c> stays the reading, because it
        /// is the string the label actually drew, ellipsis and all.
        /// </summary>
        public static string Label(AgePrimitiveLabel label)
        {
            if (label == null)
            {
                return null;
            }

            string raw = null;
            try
            {
                raw = label.Text;
            }
            catch (Exception) { }

            if (!string.IsNullOrEmpty(raw))
            {
                if (raw.IndexOf('[') >= 0)
                {
                    return Clean(raw);
                }

                string localized = LocalizedWithIcons(raw);
                if (localized != null)
                {
                    return Clean(localized);
                }
            }

            string text = null;
            try
            {
                text = label.TranslatedText;
                if (string.IsNullOrEmpty(text))
                {
                    text = raw;
                }
            }
            catch (Exception) { }

            return Ellipsized(raw, text) ? Clean(Localized(raw)) : Clean(text);
        }

        /// <summary>
        /// Whether the drawn string is the engine's own ellipsis of the assigned one, in which case the
        /// words the label MEANS are still in <c>Text</c> and <see cref="Label"/> reads those instead.
        ///
        /// A label carrying <c>AutoTruncate</c> has its <c>TranslatedText</c> chopped two characters at
        /// a time and closed with a period until it fits the box
        /// (<c>AgePrimitiveLabel.ComputeText_AutoTruncateIfNecessary</c> :720-727 calling
        /// <c>AgeUtils.TruncateString</c> :414-430), so "Xeno-Industrial Infrastructure" is drawn
        /// "Xeno-Industrial." and speaking that speaks the column width. 1787 of this game's labels
        /// carry the flag and it fires on whichever of them the layout squeezes, so the test is a
        /// MEASUREMENT of the string, not a list of screens: the drawn text must end in the truncation
        /// character and be a strict prefix of the whole text. Where nothing was cut the drawn string
        /// stays the reading, markup pass and all - this changes only what was already an artifact.
        ///
        /// It cannot see a truncation the game performed BEFORE assigning the label (a ship design's
        /// title is composed against the label's width, <c>GuiShipDesign.GetFullTitle</c> :766-781);
        /// that one has no untruncated text to find here and is answered at the reader that knows the
        /// model, as <c>SystemPanels.QueueLineName</c> does.
        /// </summary>
        private static bool Ellipsized(string assigned, string drawn)
        {
            if (
                string.IsNullOrEmpty(assigned)
                || string.IsNullOrEmpty(drawn)
                || drawn[drawn.Length - 1] != TruncationChar
            )
            {
                return false;
            }

            string full = Localized(assigned);
            return !string.IsNullOrEmpty(full)
                && full.Length > drawn.Length
                && full.StartsWith(drawn.Substring(0, drawn.Length - 1));
        }

        /// <summary>The character <c>AgeUtils.TruncateString</c> closes a clipped line with.</summary>
        private const char TruncationChar = '.';

        private static string Localized(string raw)
        {
            try
            {
                return Gui.IsLocalizationKey(raw) ? Gui.Localize(raw) : raw;
            }
            catch (Exception)
            {
                return raw;
            }
        }

        /// <summary>What a <c>%key</c> translates to, but only when the translation carries an icon
        /// token - the one case where the key is worth resolving again rather than reading the string
        /// the label drew. Null for everything else, including a key the localizer does not know
        /// (which comes back as itself), so the caller falls through to the drawn text.</summary>
        private static string LocalizedWithIcons(string raw)
        {
            try
            {
                if (!Gui.IsLocalizationKey(raw))
                {
                    return null;
                }

                string localized = Gui.Localize(raw);
                return string.IsNullOrEmpty(localized)
                    || localized == raw
                    || localized.IndexOf('[') < 0
                    ? null
                    : localized;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// A label's spoken text with the icon it OPENS with left unsaid.
        ///
        /// The game writes a caption's own picture at the head of the line - "[crown] Owner:",
        /// "[explorer] Fully discovered by:" - as a bullet for the sentence that follows it, and the
        /// sentence names the same thing in words. Spoken, the name of that picture is a word in front
        /// of a caption that did not need one ("Crown Owner: No owner"), so a reading that has already
        /// decided its lines ARE captions asks for this instead of <see cref="Label"/>. Only the
        /// leading one: an icon written in the middle of a sentence is standing in for a noun the
        /// sentence does not otherwise have, and dropping it would cost the sentence its subject.
        ///
        /// The engine's own symbol registry is the referee here exactly as it is in
        /// <see cref="SubstituteIcons"/> - a leading bracket that is not a registered icon is literal
        /// text and stays - and the token is removed BEFORE anything substitutes a name into it, so
        /// nothing is ever matched against words this class invented.
        /// </summary>
        public static string LabelWithoutLeadingIcon(AgePrimitiveLabel label)
        {
            if (label == null)
            {
                return null;
            }

            string raw = null;
            try
            {
                raw = label.Text;
            }
            catch (Exception) { }

            if (!string.IsNullOrEmpty(raw))
            {
                try
                {
                    if (Gui.IsLocalizationKey(raw))
                    {
                        raw = Gui.Localize(raw);
                    }
                }
                catch (Exception) { }
            }

            return string.IsNullOrEmpty(raw) || raw.IndexOf('[') < 0
                ? Label(label)
                : Clean(DropLeadingIcon(raw));
        }

        /// <summary>The string without the icon token it opens with, or unchanged where it opens with
        /// something else. Leading whitespace and the colour-markup runs the labels are wrapped in are
        /// looked through: an icon written behind either of them is still the first thing the line
        /// says.</summary>
        private static string DropLeadingIcon(string text)
        {
            int i = 0;
            while (i < text.Length)
            {
                if (char.IsWhiteSpace(text[i]))
                {
                    i++;
                    continue;
                }

                int past = PastMarkup(text, i);
                if (past != i)
                {
                    i = past;
                    continue;
                }

                break;
            }

            if (i >= text.Length || text[i] != '[')
            {
                return text;
            }

            int end = text.IndexOf(']', i + 1);
            if (end < 0 || !EngineExpands(text.Substring(i + 1, end - i - 1)))
            {
                return text;
            }

            return text.Substring(0, i) + text.Substring(end + 1);
        }

        /// <summary>
        /// The whole of what a label says, for one the game has ellipsized to fit its box.
        ///
        /// <see cref="Label"/> reads <c>TranslatedText</c>, which is what FITS - a tile 96 pixels wide
        /// draws "Colony Base" as "Colony Ba.", and speaking that is speaking a rendering artifact. The
        /// untruncated words are the localization key the label still holds in <c>Text</c>, resolved
        /// the same way the engine resolves it; the game itself shows them in full on the tile's
        /// tooltip, so this is the game's own text either way.
        ///
        /// Only for labels MEASURED to truncate: everywhere else <see cref="Label"/> is the drawn
        /// string and already correct.
        /// </summary>
        public static string FullLabel(AgePrimitiveLabel label)
        {
            if (label == null)
            {
                return null;
            }

            string raw = null;
            try
            {
                raw = label.Text;
            }
            catch (Exception) { }

            if (string.IsNullOrEmpty(raw))
            {
                return Label(label);
            }

            // Already-formatted text, icon tokens and all: there is no key left to resolve, and
            // Clean is what turns the tokens into names.
            if (raw.IndexOf('[') >= 0)
            {
                return Clean(raw);
            }

            try
            {
                return Clean(Gui.Localize(raw));
            }
            catch (Exception)
            {
                return Label(label);
            }
        }

        /// <summary>A widget's tooltip text. Populated at bind time, so it reads without ever
        /// showing the tooltip window; for a disabled control the game has already appended the
        /// reason it is disabled.</summary>
        public static string Tooltip(AgeTooltip tooltip)
        {
            if (tooltip == null)
            {
                return null;
            }

            try
            {
                return Clean(tooltip.Content);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>A cleaned block of game text as separate spoken lines - blank ones dropped - for a
        /// review buffer to walk. The game writes a tooltip as a name, a description and, when it is
        /// refusing, its reason, separated by exactly these newlines.</summary>
        public static IList<string> Lines(string text)
        {
            List<string> lines = new List<string>();
            if (string.IsNullOrEmpty(text))
            {
                return lines;
            }

            foreach (string line in text.Split('\n'))
            {
                string trimmed = line.Trim();
                if (trimmed.Length > 0)
                {
                    lines.Add(trimmed);
                }
            }

            return lines;
        }

        /// <summary>
        /// A tooltip's OWN written words as spoken lines - what the game bound onto the tooltip itself,
        /// and nothing that a renderer would assemble on hover.
        ///
        /// The named form of <c>Lines(Tooltip(t))</c>, and the name is the point: the mod has two
        /// readings of a tooltip and they answer differently for a class-backed one.
        /// <c>AgeWidgets.TooltipLines</c> is the reading for a caller who wants what the PLAYER would
        /// read, falling back to the drawn tooltip window where the words are assembled. This one is
        /// for a caller that deliberately wants only the written content - a name read off a tooltip
        /// at declare time, a panel walked without hovering anything - where the drawn-window fallback
        /// would answer either nothing or, worse, whatever the pointer happens to be over. Written out
        /// at a call site, the two are one call apart and indistinguishable; named, the choice is on
        /// the page.
        /// </summary>
        public static IList<string> ContentLines(AgeTooltip tooltip)
        {
            return Lines(Tooltip(tooltip));
        }

        /// <summary>The tooltip attached to <paramref name="transform"/>, if it has one.</summary>
        public static string Tooltip(AgeTransform transform)
        {
            if (transform == null)
            {
                return null;
            }

            try
            {
                return Tooltip(transform.AgeTooltip);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Replace every inline <c>[token]</c> icon with its name, before <c>CleanLine</c> gets a
        /// chance to turn the bracket into an opaque picture glyph with no bracket text left to read
        /// it back from.
        ///
        /// The scan is deliberately simple - AGE's own icon tokens do not nest brackets or span a
        /// newline - so a plain index of the next <c>[</c> and its matching <c>]</c> is the whole
        /// grammar. An unmatched <c>[</c> (there is no closing bracket left in the string) is copied
        /// through literally rather than treated as the start of a token: better an odd character
        /// than a mangled truncation of the rest of the line.
        ///
        /// A space is inserted on whichever side of the substitution needs one. The game routinely
        /// writes a number hard against the icon it belongs to with no gap at all - "3[population]" -
        /// because a picture glyph needs none; a NAME does, or "3" and "Population" read as the one
        /// word "3Population". Only added where it is missing, so a token that already sat in its own
        /// whitespace - the ordinary case - gains nothing extra.
        ///
        /// Brackets that are not icons at all - a save titled "[Beginner] access test" - must come
        /// through untouched. The engine itself is the referee: a token registered in its own
        /// symbol table is an icon (<see cref="EngineExpands"/>); one that is not is literal text
        /// and is copied through brackets and all.
        ///
        /// An icon reaches the sentence only if it has something to add to it. It is dropped when
        /// <see cref="IconNames"/> gives it no name - either because the table says it has none (an
        /// entry that is a colour directive rather than a picture) or, once per load and with a
        /// warning, because the table has never heard of it - and dropped again when the name would
        /// only repeat the words already beside it (<see cref="Duplicates"/>).
        /// </summary>
        private static string SubstituteIcons(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('[') < 0)
            {
                return text;
            }

            StringBuilder result = new StringBuilder(text.Length);
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (c == '[')
                {
                    int end = text.IndexOf(']', i + 1);
                    if (end > i)
                    {
                        string token = text.Substring(i + 1, end - i - 1);
                        if (!EngineExpands(token))
                        {
                            result.Append(text, i, end - i + 1);
                            i = end + 1;
                            continue;
                        }

                        int next = end + 1;
                        string name = IconNames.NameFor(token);
                        if (name == null || Duplicates(name, result, text, next))
                        {
                            i = next;
                            continue;
                        }

                        if (NeedsSpace(EffectiveBefore(result), name[0]))
                        {
                            result.Append(' ');
                        }

                        result.Append(name);

                        if (NeedsSpace(name[name.Length - 1], EffectiveAfter(text, next)))
                        {
                            result.Append(' ');
                        }

                        i = next;
                        continue;
                    }
                }

                result.Append(c);
                i++;
            }

            return result.ToString();
        }

        /// <summary>
        /// Whether the word right beside the icon already says what the icon is called.
        ///
        /// The game writes an icon immediately in front of the noun it stands for far more often than
        /// it writes one instead of that noun, and a name substituted against its own word stutters:
        /// the over-colonization penalty is drawn as its icon followed by "Over-colonization penalty
        /// on Empire" and was read as "Over Colonization Over-colonization penalty on Empire".
        /// Skipping the substitution there loses nothing, because the word the icon would have
        /// contributed is the very next word in the line.
        ///
        /// ADJACENT is the whole rule (owner ruling 2026-08-25). An earlier revision dropped the name
        /// whenever most of its words appeared ANYWHERE in the icon's line, and that quietly deleted
        /// words the sentence needed: the game's description of a system's approval - "can influence
        /// the [food] and [influence] outputs" - lost "Influence" to the verb "influence" eight words
        /// upstream, which is a different word doing a different job. A repeat is only a stutter when
        /// the reader would hear the two together.
        ///
        /// So the name's words are compared, in order, against the words that BUTT UP against the
        /// icon - the run beginning immediately after it, and the run ending immediately before it -
        /// with the whole name having to match. Folded to letters and digits (the two spellings are
        /// rarely the same string: "Over Colonization" against "Over-colonization"), and looking
        /// through the colour markup the game writes between an icon and its word, which is not a
        /// word.
        ///
        /// Only the icon's own line is considered. A word on the next line of a stat block is a
        /// different sentence rather than a stutter.
        /// </summary>
        private static bool Duplicates(string name, StringBuilder before, string text, int after)
        {
            List<string> words = Words(name);
            return words.Count > 0
                && (
                    Repeats(words, Words(LineAfter(text, after)), true)
                    || Repeats(words, Words(LineBefore(before)), false)
                );
        }

        /// <summary>Whether the run of words at the near END of <paramref name="beside"/> - its head
        /// for the text that follows the icon, its tail for the text in front of it - is the name
        /// again.</summary>
        private static bool Repeats(List<string> name, List<string> beside, bool leading)
        {
            if (beside.Count < name.Count)
            {
                return false;
            }

            int offset = leading ? 0 : beside.Count - name.Count;
            for (int i = 0; i < name.Count; i++)
            {
                if (!SameWord(beside[offset + i], name[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Whether two folded words are the same word. A plural counts: the game writes
        /// "[fighter] Stance Ratio" and "[ship]Ships", and a name read out against its own plural
        /// stutters exactly as it does against itself. Only a trailing "s" or "es", so a word that
        /// merely BEGINS with another stays a different word - the Dust coin drawn in front of
        /// "Dustciduous Trees" is still worth saying.</summary>
        private static bool SameWord(string one, string other)
        {
            if (one == other)
            {
                return true;
            }

            string longer = one.Length > other.Length ? one : other;
            string shorter = one.Length > other.Length ? other : one;
            return longer.Length - shorter.Length <= 2
                && longer[longer.Length - 1] == 's'
                && longer.StartsWith(shorter, StringComparison.Ordinal);
        }

        /// <summary>A line's words, folded for comparison: letters and digits only, lower-cased, with
        /// a hyphen breaking a word the way a space does and a <c>#RRGGBBAA#</c> colour run treated as
        /// the nothing it is rather than as a word made of its hex digits.</summary>
        private static List<string> Words(string text)
        {
            List<string> words = new List<string>();
            if (string.IsNullOrEmpty(text))
            {
                return words;
            }

            StringBuilder word = new StringBuilder();
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                int past = PastMarkup(text, i);
                if (past != i)
                {
                    Flush(words, word);
                    i = past;
                    continue;
                }

                if (char.IsLetterOrDigit(c))
                {
                    word.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    Flush(words, word);
                }

                i++;
            }

            Flush(words, word);
            return words;
        }

        private static void Flush(List<string> words, StringBuilder word)
        {
            if (word.Length > 0)
            {
                words.Add(word.ToString());
                word.Length = 0;
            }
        }

        private static string LineAfter(string text, int index)
        {
            int end = text.IndexOf('\n', index);
            return text.Substring(index, (end < 0 ? text.Length : end) - index);
        }

        private static string LineBefore(StringBuilder result)
        {
            int start = 0;
            for (int i = result.Length - 1; i >= 0; i--)
            {
                if (result[i] == '\n')
                {
                    start = i + 1;
                    break;
                }
            }

            return result.ToString(start, result.Length - start);
        }

        /// <summary>
        /// Whether the engine treats this bracketed token as an icon at all, asked of the engine's
        /// own registry - the very dictionary <see cref="AgeUtils.CleanLine"/> looks the token up
        /// in, keyed the way it keys it (brackets included, upper-cased). A token that is not in
        /// there is not an icon: CleanLine copies it through and the player SEES the brackets, so a
        /// save titled "[Beginner] access test" must be read with them.
        ///
        /// Asking the registry rather than watching what CleanLine does to a probe string matters,
        /// because CleanLine does two things: it expands icons AND it strips <c>#RRGGBB#</c> colour
        /// runs. A bracket that contains only colour markup - the game ships
        /// "[#52008a#Debug#REVERT#]" - therefore comes back changed from a probe while not having
        /// been expanded at all, and was taken for an icon whose name nobody could find.
        /// </summary>
        private static bool EngineExpands(string token)
        {
            try
            {
                return AgePrimitiveLabel.SpecialCharacters.ContainsKey(("[" + token + "]").ToUpper());
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Whether a run-together substitution's seam needs a space: true only when both sides are
        // word characters, so "3[population]" gains one ("3 Population") while "[dust] from" -
        // already sitting next to a space - gains nothing it did not already have.
        private static bool NeedsSpace(char before, char after)
        {
            return char.IsLetterOrDigit(before) && char.IsLetterOrDigit(after);
        }

        // The seam's real neighbours. Colour markup ("#E6C361#", "#REVERT#") sits between the icon
        // and the word it belongs to and is stripped later, so the character that will actually end
        // up adjacent is the one on the far side of any run of #...# pairs. "\0" when nothing but
        // markup remains - IsLetterOrDigit says no, so no space is added.
        //
        // The only markup skip that runs BACKWARDS, over what has been written so far rather than
        // over the source string - so it hands the eight characters ending here to
        // <see cref="PastMarkup"/> rather than applying that method's rule a second time.
        private static char EffectiveBefore(StringBuilder result)
        {
            int i = result.Length - 1;
            while (
                i >= MarkupRun - 1
                && PastMarkup(result.ToString(i - MarkupRun + 1, MarkupRun), 0) != 0
            )
            {
                i -= MarkupRun;
            }

            return i >= 0 ? result[i] : '\0';
        }

        private static char EffectiveAfter(string text, int index)
        {
            int i = index;
            int past;
            while ((past = PastMarkup(text, i)) != i)
            {
                i = past;
            }

            return i < text.Length ? text[i] : '\0';
        }

        /// <summary>How long a colour-markup run is: <c>#RRGGBB#</c> and <c>#REVERT#</c> alike are
        /// eight characters, opening hash to closing hash.</summary>
        private const int MarkupRun = 8;

        /// <summary>
        /// Where a colour-markup run opening at <paramref name="index"/> ends, or
        /// <paramref name="index"/> unchanged where nothing markup-shaped opens there - the ONE place
        /// this class decides what is markup and what is text the player is meant to read.
        ///
        /// The rule is the engine's, and it is positional and exact
        /// (<c>AgeUtils.CleanLine</c>, <c>decompiled/Assembly-CSharp-firstpass/AgeUtils.cs:311-319</c>:
        /// a <c>#</c> whose eighth character is also a <c>#</c> is removed whole, and nothing else is
        /// touched). Matching it matters in both directions: a <c>#</c> with no partner eight along is
        /// LITERAL and stays on the screen, so "#1 in the galaxy" and "Ref #4432#8801" must be read
        /// with their hashes and with everything between them, which a scan to "the next #, however
        /// far off" silently swallowed.
        /// </summary>
        private static int PastMarkup(string text, int index)
        {
            return index >= 0
                && index + MarkupRun <= text.Length
                && text[index] == '#'
                && text[index + MarkupRun - 1] == '#'
                ? index + MarkupRun
                : index;
        }

        /// <summary>
        /// Control characters (newlines excepted) and the private-use glyphs the icon tokens expand
        /// to: rendered, they are pictures; spoken, they are noise.
        ///
        /// The gap an icon leaves behind goes with it. An icon is written with a space on each side
        /// and occupies the one between them, so removing it - whether as an unreadable glyph here or
        /// as a name not worth saying earlier - leaves two spaces where the picture used to be. That
        /// is not spacing the game authored, it is the hole this class made, and some voices read it
        /// as a pause in the middle of a sentence. Runs of blanks therefore collapse to one; newlines
        /// do not, because the game separates a tooltip's description from its refusal with a blank
        /// line and a screen reader reads that as the pause it is meant to be.
        /// </summary>
        private static string StripUnspeakable(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            StringBuilder kept = new StringBuilder(text.Length);
            bool blank = false;
            foreach (char character in text)
            {
                bool speakable =
                    character == '\n'
                    || (
                        character >= ' '
                        && (character < PrivateUseFirst || character > PrivateUseLast)
                    );
                if (!speakable)
                {
                    continue;
                }

                // Any blank, not just the plain space: the game holds a quantity together with its
                // unit using non-breaking spaces ("+25% [manpower] Manpower Capacity"), and
                // a gap left by a removed icon is just as much a gap for being unbreakable.
                if (character != '\n' && char.IsWhiteSpace(character))
                {
                    blank = true;
                    continue;
                }

                if (blank && kept.Length > 0 && kept[kept.Length - 1] != '\n' && character != '\n')
                {
                    kept.Append(' ');
                }

                blank = false;
                kept.Append(character);
            }

            string result = kept.ToString().Trim();
            return result.Length == 0 ? null : result;
        }
    }
}
