using System;
using System.Collections.Generic;
using System.Text;
using ES2Access.Core.Util;
using UnityEngine;

namespace ES2Access.UI
{
    /// <summary>
    /// What the tooltip on screen is SAYING, read off the tooltip as drawn.
    ///
    /// Most of the game's tooltips carry their words: a string on the widget, there to be read
    /// whether or not anything is hovering it. The interesting ones do not. A tooltip that names a
    /// CLASS carries only a class name and a target object, and the words are assembled at draw time
    /// by the tooltip window - which loads a list of little prefabs ("panel features"), hands each of
    /// them the target, and lets each write its own line from live data. A resource's tooltip is a
    /// stat block built that way; there is no string anywhere that holds it.
    ///
    /// There is no service that will assemble one on request either: the only way the text exists is
    /// for the window to build it. So this reads the window - the labels of the features it is
    /// currently showing, in the order it laid them out. Which makes what the review buffer holds
    /// equal to what is drawn by construction, rather than by a second implementation of the
    /// game's own assembly rules that would drift from it.
    ///
    /// It follows that a class tooltip only reads while it is up, which is exactly why focus asks the
    /// game to draw the focused widget's tooltip (see <see cref="PointerFocus"/>). The window is
    /// asked which tooltip it is drawing before anything is read from it, so a stale tooltip - one
    /// still fading out from the widget focus just left - is never mistaken for this one's.
    ///
    /// Main-thread only.
    /// </summary>
    public static class DrawnTooltip
    {
        /// <summary>How deep inside a panel feature to look for its labels.</summary>
        private const int MaxDepth = 8;

        private static readonly List<string> Nothing = new List<string>();

        /// <summary>The lines the tooltip window is drawing for <paramref name="tooltip"/> right now,
        /// and nothing at all when it is drawing something else or nothing.</summary>
        public static IList<string> Lines(AgeTooltip tooltip)
        {
            try
            {
                if (tooltip == null)
                {
                    return Nothing;
                }

                GuiTooltipWindow window = Window();
                if (window == null || !ReferenceEquals(window.AgeTooltip, tooltip))
                {
                    return Nothing;
                }

                AgeTransform table = window.PanelFeaturesTable;
                if (table == null || !table.Visible)
                {
                    return Nothing;
                }

                List<Entry> entries = new List<Entry>();
                Read(table, entries, 0);
                return Join(entries);
            }
            catch (Exception e)
            {
                Log.Warn("tooltip: reading the drawn tooltip threw: " + e);
                return Nothing;
            }
        }

        private static GuiTooltipWindow Window()
        {
            GuiTooltipWindow window = Gui.GuiServiceAvailable
                ? Gui.GuiService.GetWindow<GuiTooltipWindow>(false)
                : null;
            return window != null && window.Shown ? window : null;
        }

        /// <summary>One labelled widget under the tooltip's panel, still carrying its own transform -
        /// grouping into drawn rows needs the rectangle, which is gone once the text has been read out
        /// of it.</summary>
        private struct Entry
        {
            public AgeTransform Widget;
            public string Text;

            /// <summary>Set when the text is the name of a PICTURE rather than words the panel drew.
            /// It is the same text either way, but it earns its place in the line differently - see
            /// <see cref="Join"/>.</summary>
            public bool Icon;
        }

        private static readonly Func<Entry, AgeTransform> EntryWidget = entry => entry.Widget;

        /// <summary>
        /// Every label under a widget, in the order the window arranged them.
        ///
        /// Hidden branches are skipped rather than read, and that is load-bearing rather than tidy:
        /// the window POOLS its panel features instead of destroying them, so a tooltip that once
        /// showed six lines and now shows two still has four labels hanging off it, holding the text
        /// of whatever was hovered before.
        /// </summary>
        private static void Read(AgeTransform widget, List<Entry> entries, int depth)
        {
            if (depth > MaxDepth)
            {
                return;
            }

            AgePrimitiveLabel label = widget.GetComponent<AgePrimitiveLabel>();
            string text = label != null ? AgeText.Label(label) : PictureName(widget);
            if (!string.IsNullOrEmpty(text))
            {
                entries.Add(
                    new Entry
                    {
                        Widget = widget,
                        Text = text,
                        Icon = label == null,
                    }
                );
            }

            // The engine's own test for "the player can see this child", asked the way the engine asks
            // it: transparent counts as hidden unless the parent has declared otherwise.
            List<AgeTransform> children = widget.Children;
            for (int i = 0; i < children.Count; i++)
            {
                AgeTransform child = children[i];
                if (child != null && child.Visible && (widget.StrictVisibility || child.Alpha > 0f))
                {
                    Read(child, entries, depth + 1);
                }
            }
        }

        /// <summary>
        /// What a widget that draws no words is called, when it is drawing something that stands for
        /// a word at all.
        ///
        /// A stat strip is laid out as icons and numbers in alternating widgets, and the icon is where
        /// the meaning is: "36 37 38 22 9" is what a reader that only looks at labels gets from a row
        /// the player sees as five named quantities. The picture is not decoration there, it is the
        /// column heading.
        ///
        /// Which is exactly what has to be told apart, because the same panel draws backgrounds, rules
        /// and portraits and none of those is a word anybody wants read. The test is the icon table:
        /// a texture it names stands for something, a texture it does not is decoration
        /// (<see cref="IconNames.NameForAsset"/>).
        /// </summary>
        private static string PictureName(AgeTransform widget)
        {
            AgePrimitiveImage image = widget.GetComponent<AgePrimitiveImage>();
            Texture texture = image == null ? null : image.Texture;
            return texture == null ? null : IconNames.NameForAsset(texture.name);
        }

        /// <summary>
        /// The entries turned into the lines a review buffer walks: one drawn ROW at a time, in
        /// reading order, rather than one label at a time.
        ///
        /// "Production per turn" and "+0" are two labels the panel draws side by side - a caption and
        /// its value - and a sighted player reads them as one fact, not two. Grouping by the rectangle
        /// (<see cref="AgeLayout.Rows{T}"/>) rather than by which panel feature produced which label is
        /// what makes that true regardless of which prefab happens to own which half of the row.
        ///
        /// The join is a plain space, on purpose: it is prose the game laid out across two widgets
        /// instead of one, not a list of separate facts, so <see cref="ES2Access.Core.Speech.ModStrings.ListSeparator"/>
        /// would read a false pause into the middle of a sentence. A label whose OWN text still holds
        /// an embedded newline - a paragraph the window wrapped at its own width - keeps that break:
        /// the space-join happens on the raw text first, and splitting the result on '\n' afterwards is
        /// what lets "one label, several physical lines" and "several labels, one shared line" share
        /// the same code instead of needing two.
        /// </summary>
        private static List<string> Join(List<Entry> entries)
        {
            List<string> lines = new List<string>();
            foreach (List<Entry> row in AgeLayout.Rows(entries, EntryWidget))
            {
                string words = Words(row);
                if (words.Length == 0)
                {
                    continue;
                }

                StringBuilder combined = new StringBuilder();
                for (int i = 0; i < row.Count; i++)
                {
                    Entry cell = row[i];
                    if (cell.Icon && Says(words, cell.Text))
                    {
                        continue;
                    }

                    if (combined.Length > 0)
                    {
                        combined.Append(' ');
                    }

                    combined.Append(cell.Text);
                }

                foreach (string line in AgeText.Lines(combined.ToString()))
                {
                    lines.Add(line);
                }
            }

            return lines;
        }

        /// <summary>
        /// The row's actual words, and the reason a row with none of them is dropped whole.
        ///
        /// A picture completes a sentence or it illustrates one. The five icons of a stat strip are
        /// read because the numbers beside them are unreadable without them; the star portrait at the
        /// top of a system's tooltip is on its own line with nothing to complete, and announcing "Blue
        /// Star" for it puts a line into the reading that the panel never wrote - just above the line
        /// where the panel does say, in words, "Star System (Blue Star)".
        ///
        /// Sharing the line with words is therefore the whole test, and it needs no threshold on how
        /// big a picture has to be before it stops being an icon.
        /// </summary>
        private static string Words(List<Entry> row)
        {
            StringBuilder said = new StringBuilder();
            for (int i = 0; i < row.Count; i++)
            {
                if (!row[i].Icon)
                {
                    said.Append(row[i].Text).Append(' ');
                }
            }

            return said.ToString();
        }

        /// <summary>Whether the row's own words already say what an icon on it is called - the header
        /// symbol beside the heading "Star System (Blue Star)" is named "System", and reading both
        /// gives "System Star System (Blue Star)".</summary>
        private static bool Says(string words, string name)
        {
            string key = TextUtil.LettersAndDigits(name);
            return key.Length > 0 && TextUtil.LettersAndDigits(words).IndexOf(key) >= 0;
        }
    }
}
