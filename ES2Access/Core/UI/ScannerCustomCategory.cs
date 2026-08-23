using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;

namespace ES2Access.Core.UI
{
    /// <summary>One column of a built-in category, as a custom category refers to it: two stable
    /// keys, never an index and never a label (<see cref="ScannerKeys"/>). A selector the galaxy has
    /// no column for is not an error - it is a luxury nobody has found yet - so it is kept and
    /// skipped, never dropped.</summary>
    public sealed class ScannerSelector
    {
        public ScannerSelector(string category, string subcategory)
        {
            Category = category ?? string.Empty;
            Subcategory = subcategory ?? string.Empty;
        }

        public readonly string Category;
        public readonly string Subcategory;

        /// <summary>Whether this names the same column as another. Ordinal and case-SENSITIVE: these
        /// are the game's own internal names and the mod's own, never anything a player typed.
        /// </summary>
        public bool Same(ScannerSelector other)
        {
            return other != null && other.Category == Category && other.Subcategory == Subcategory;
        }

        public override string ToString()
        {
            return Category + ":" + Subcategory;
        }
    }

    /// <summary>
    /// A CATEGORY THE PLAYER MADE: a name, the built-in columns it draws from, and the words it looks
    /// for. It is what turns "where are the enemy fleets and the enemy systems and anything called
    /// Sophon" into one press of one key.
    ///
    /// Everything here is the model alone. Which results it catches is worked out on every scanner
    /// press from what the galaxy holds (<c>GalaxyScanner</c>), and nothing about this survives into
    /// that answer except the three lists below - so a category can name a column this galaxy does
    /// not have, and a keyword nothing matches, without anything going wrong.
    ///
    /// Blank is refused everywhere, in the name and in a keyword alike: a blank would persist into
    /// the settings file as something a screen reader speaks as silence, and a player would have no
    /// way to tell an empty row from a missing one.
    ///
    /// Engine-free, so the rules that decide what a saved category IS can be tested without the game.
    /// </summary>
    public sealed class ScannerCustomCategory
    {
        /// <summary>A category the player has named. A blank name is refused here rather than kept
        /// and worked around later - see <see cref="Named"/> for the answering-with-null form.
        /// </summary>
        public ScannerCustomCategory(string name)
        {
            _name = Clean(name);
            if (_name == null)
            {
                throw new ArgumentException("a custom category cannot be nameless", "name");
            }
        }

        /// <summary>The same, answering null where there is no name - what a decode and a runtime
        /// setter want, neither of which has anywhere to put an exception.</summary>
        public static ScannerCustomCategory Named(string name)
        {
            return Clean(name) == null ? null : new ScannerCustomCategory(name);
        }

        /// <summary>What the player calls it - the name the category cycle says. Never blank.
        /// </summary>
        public string Name
        {
            get { return _name; }
        }

        /// <summary>The built-in columns it draws from, in the order the player added them - which is
        /// the order its own columns come out in.</summary>
        public IList<ScannerSelector> Selectors
        {
            get { return _selectors; }
        }

        /// <summary>The words it looks for, in the order the player added them.</summary>
        public IList<string> Keywords
        {
            get { return _keywords; }
        }

        /// <summary>Whether it would catch nothing whatever the galaxy held - no column named and no
        /// word looked for. Such a category is still configured, and still a slot the player can
        /// see; it just never has a result.</summary>
        public bool Asks
        {
            get { return _selectors.Count > 0 || _keywords.Count > 0; }
        }

        /// <summary>Call it something else. Blank is refused, and so is the name it already has -
        /// both of which the editor reports rather than performing.</summary>
        public bool Rename(string name)
        {
            string wanted = Clean(name);
            if (wanted == null || wanted == _name)
            {
                return false;
            }

            _name = wanted;
            return true;
        }

        /// <summary>Draw from one more column. A column already named is refused: the same column
        /// twice would be two identical subcategories, which the cursor could not tell apart.
        /// </summary>
        public bool AddSelector(ScannerSelector selector)
        {
            if (selector == null || selector.Category.Length == 0 || selector.Subcategory.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < _selectors.Count; i++)
            {
                if (_selectors[i].Same(selector))
                {
                    return false;
                }
            }

            _selectors.Add(selector);
            return true;
        }

        public bool RemoveSelector(ScannerSelector selector)
        {
            for (int i = 0; selector != null && i < _selectors.Count; i++)
            {
                if (_selectors[i].Same(selector))
                {
                    _selectors.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        /// <summary>Look for one more word. Trimmed, blank refused, and the same word in another
        /// case refused - the match itself ignores case, so two spellings would be two columns
        /// holding exactly the same things.</summary>
        public bool AddKeyword(string keyword)
        {
            string wanted = Clean(keyword);
            if (wanted == null || Has(wanted))
            {
                return false;
            }

            _keywords.Add(wanted);
            return true;
        }

        public bool RemoveKeyword(string keyword)
        {
            string wanted = Clean(keyword);
            for (int i = 0; wanted != null && i < _keywords.Count; i++)
            {
                if (string.Equals(_keywords[i], wanted, StringComparison.OrdinalIgnoreCase))
                {
                    _keywords.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether this word is already being looked for, however it was spelt.</summary>
        public bool Has(string keyword)
        {
            string wanted = Clean(keyword);
            for (int i = 0; wanted != null && i < _keywords.Count; i++)
            {
                if (string.Equals(_keywords[i], wanted, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>An independent copy - what an editor holding edits back until Apply works on, so
        /// that Cancel can throw them away by dropping the copy.</summary>
        public ScannerCustomCategory Copy()
        {
            ScannerCustomCategory copy = new ScannerCustomCategory(_name);
            for (int i = 0; i < _selectors.Count; i++)
            {
                copy._selectors.Add(_selectors[i]);
            }

            for (int i = 0; i < _keywords.Count; i++)
            {
                copy._keywords.Add(_keywords[i]);
            }

            return copy;
        }

        /// <summary>What an empty slot is called when the player fills it - "Custom 1", numbered the
        /// way the six keys are.</summary>
        public static string DefaultName(int slot)
        {
            return ModStrings.Format(ModStrings.GalaxyScannerCustomName, slot + 1);
        }

        /// <summary>Text as it is kept: trimmed, and null where nothing is left of it.</summary>
        internal static string Clean(string text)
        {
            if (text == null)
            {
                return null;
            }

            string trimmed = text.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }

        private string _name;
        private readonly List<ScannerSelector> _selectors = new List<ScannerSelector>();
        private readonly List<string> _keywords = new List<string>();
    }
}
