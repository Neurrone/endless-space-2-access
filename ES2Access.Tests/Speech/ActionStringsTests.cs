using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.UI.Input;
using Xunit;

namespace ES2Access.Tests.Speech
{
    /// <summary>
    /// The names of the mod's own keyboard actions are addressed by a COMPOSED key
    /// (<c>action.ui.down.title</c>), so the sweep over shipped constants in
    /// <see cref="LocaleFileTests"/> cannot see them. This is that sweep for this family: a phrase
    /// the mod speaks and the English template does not carry is a phrase no translator is offered.
    /// </summary>
    public class ActionStringsTests
    {
        [Fact]
        public void EveryActionNameIsInTheEnglishTemplate()
        {
            SortedSet<string> shipped = TestPaths.ShippedKeys(TestPaths.EnglishTemplate());

            foreach (string key in ModStrings.ActionStringKeys())
            {
                Assert.True(shipped.Contains(key), "english.json: missing key '" + key + "'");
            }
        }

        /// <summary>
        /// A description is optional and a title is not (owner ruling 2026-09-02): most rows say
        /// everything they have in their name, and a tooltip repeating the name would be read twice
        /// on every row. So the check runs one way - a sentence with nothing to explain is a key
        /// nothing can reach.
        /// </summary>
        [Fact]
        public void EveryDescriptionExplainsAnActionThatHasATitle()
        {
            SortedSet<string> keys = new SortedSet<string>(ModStrings.ActionStringKeys());
            Assert.NotEmpty(keys);
            foreach (string key in keys)
            {
                if (!key.EndsWith(".description"))
                {
                    continue;
                }

                string title = key.Substring(0, key.Length - ".description".Length) + ".title";
                Assert.True(keys.Contains(title), "no title for '" + key + "'");
            }
        }

        /// <summary>Every row of the Controls tab is named. A binding whose title key is missing
        /// draws and speaks its raw action name.</summary>
        [Fact]
        public void EveryActionOnTheControlsTabHasATitle()
        {
            SortedSet<string> keys = new SortedSet<string>(ModStrings.ActionStringKeys());
            foreach (KeybindLayout.Block block in KeybindLayout.Blocks)
            {
                foreach (string action in block.Actions)
                {
                    Assert.True(
                        keys.Contains(ModStrings.ActionTitleKey(action)),
                        "no title for '" + action + "'"
                    );
                }
            }
        }

        [Fact]
        public void TheKeysAreComposedFromTheActionName()
        {
            Assert.Equal("action.ui.down.title", ModStrings.ActionTitleKey("ui.down"));
            Assert.Equal(
                "action.galaxy.scanNext.description",
                ModStrings.ActionDescriptionKey("galaxy.scanNext")
            );
        }
    }
}
