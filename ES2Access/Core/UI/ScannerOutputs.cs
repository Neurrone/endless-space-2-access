using System;
using ES2Access.Core.Speech;

namespace ES2Access.Core.UI
{
    /// <summary>
    /// What a world the scanner is offering would PRODUCE, as the row says it.
    ///
    /// Two rules, both of them about a line the player hears in a list of a dozen results and cannot
    /// re-read at leisure:
    ///
    /// <para>The resource is named by its SHORT name - "Food", not the simulation property's own
    /// title ("Planet Food production"), which is a phrase written for a tooltip and reads as five
    /// words of scaffolding around one. The short names are the ones the mod already keeps for the
    /// icons the game draws in their place (<c>ModStrings</c> Icons), so the vocabulary is one.</para>
    ///
    /// <para>A figure the page would DRAW as zero is dropped. Every uncolonized world has five of
    /// these and most worlds produce two or three of them, so the zeros are two thirds of the line
    /// and say nothing: a resource that is not listed is a resource the world does not make. The test
    /// is the DISPLAYED figure, floored the way the game's own formatter floors it, so a world making
    /// 0.4 Food - which the row would read as "Food 0" - is silent about food rather than saying a
    /// number the player cannot act on.</para>
    ///
    /// Engine-free so both rules are testable off the game.
    /// </summary>
    public static class ScannerOutputs
    {
        /// <summary>Whether an output figure reaches the row at all: false where the page would draw
        /// it as zero. Floor, because that is the rounding the game's own amount formatter uses
        /// (<c>Gui.Rounding.Floor</c>) and the two must agree or the row drops a figure it then
        /// shows.</summary>
        public static bool Says(float value)
        {
            return (int)Math.Floor(value) != 0;
        }

        /// <summary>One output as the row says it - the resource's short name and the figure. Null
        /// where either half is missing, which the caller drops.</summary>
        public static string Line(string resource, string amount)
        {
            if (string.IsNullOrEmpty(resource) || string.IsNullOrEmpty(amount))
            {
                return null;
            }

            return ModStrings.Format(ModStrings.GalaxyScannerOutput, resource, amount);
        }
    }
}
