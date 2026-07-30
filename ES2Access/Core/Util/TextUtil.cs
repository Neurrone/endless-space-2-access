using System.Globalization;
using System.Text;

namespace ES2Access.Core.Util
{
    /// <summary>Text folding shared by matching code (type-ahead search).</summary>
    public static class TextUtil
    {
        /// <summary>Fold accents away for matching ("Séance" matches "seance"); ligatures œ/æ expand.
        /// Ported from OniAccess (VisionNotIncluded) with permission.</summary>
        public static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            string decomposed = text.Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder(decomposed.Length);
            for (int i = 0; i < decomposed.Length; i++)
            {
                char c = decomposed[i];
                switch (c)
                {
                    case 'œ':
                    case 'Œ':
                        sb.Append("oe");
                        break;
                    case 'æ':
                    case 'Æ':
                        sb.Append("ae");
                        break;
                    default:
                        if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                            sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Only the letters and digits, lowercased - what two spellings of the same words have in
        /// common.
        ///
        /// It exists so that "already said" can be asked about text the game wrote twice in two
        /// styles. An icon named "Over Colonization" sits against the words "Over-colonization
        /// penalty", and reading both stutters; the hyphen, the capital and the space are exactly the
        /// differences that must not hide the repetition, and they are exactly what this drops.
        /// </summary>
        public static string LettersAndDigits(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            StringBuilder kept = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsLetterOrDigit(c)) kept.Append(char.ToLowerInvariant(c));
            }
            return kept.ToString();
        }

        /// <summary>Null/empty/all-whitespace test. (<c>string.IsNullOrWhiteSpace</c> is .NET 4.0; the
        /// game's Mono runtime is on the 3.5 profile.)</summary>
        public static bool IsBlank(string s)
        {
            if (string.IsNullOrEmpty(s)) return true;
            for (int i = 0; i < s.Length; i++)
                if (!char.IsWhiteSpace(s[i])) return false;
            return true;
        }
    }
}
