using System;
using System.Collections.Generic;
using System.Reflection;
using ES2Access.UI.Input;
using Xunit;

namespace ES2Access.Tests.Input
{
    /// <summary>
    /// The Controls tab is drawn from <see cref="KeybindLayout"/>, not from the order the mod
    /// registers its keys in. That is what lets the page be rearranged without moving a binding -
    /// and it is also how a key could be added to the mod and never drawn at all. These tests are
    /// the guard: every action the mod names appears on the page exactly once, and nothing appears
    /// there that the mod does not name.
    /// </summary>
    public class KeybindLayoutTests
    {
        [Fact]
        public void EveryActionIsOnThePageExactlyOnce()
        {
            SortedSet<string> declared = new SortedSet<string>(AllActions());
            Assert.NotEmpty(declared);

            SortedSet<string> seen = new SortedSet<string>();
            foreach (KeybindLayout.Block block in KeybindLayout.Blocks)
            {
                foreach (string action in block.Actions)
                {
                    Assert.True(seen.Add(action), "'" + action + "' is on the page twice");
                    Assert.True(declared.Contains(action), "'" + action + "' is not an action");
                }
            }

            foreach (string action in declared)
            {
                Assert.True(seen.Contains(action), "'" + action + "' has no row on the page");
            }
        }

        [Fact]
        public void EveryBlockIsNamedAndHoldsRows()
        {
            SortedSet<string> titles = new SortedSet<string>();
            foreach (KeybindLayout.Block block in KeybindLayout.Blocks)
            {
                Assert.False(string.IsNullOrEmpty(block.TitleKey));
                Assert.True(titles.Add(block.TitleKey), "two blocks share a heading");
                Assert.NotEmpty(block.Actions);
            }
        }

        /// <summary>Every action name the mod declares, read off the three constant tables the
        /// layout itself is written from - so a key added to one of them and forgotten on the page
        /// fails this build.</summary>
        private static IEnumerable<string> AllActions()
        {
            foreach (
                Type table in new[]
                {
                    typeof(UiActions),
                    typeof(MapActions),
                    typeof(BufferActions),
                }
            )
            {
                foreach (
                    FieldInfo field in table.GetFields(BindingFlags.Public | BindingFlags.Static)
                )
                {
                    if (field.IsLiteral && field.FieldType == typeof(string))
                    {
                        yield return (string)field.GetRawConstantValue();
                    }
                }
            }
        }
    }
}
