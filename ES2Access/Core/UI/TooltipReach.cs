using System;
using System.Collections.Generic;

namespace ES2Access.Core.UI
{
    /// <summary>
    /// How far from a widget a tooltip still counts as that widget's, as a set of directions a
    /// caller asks for explicitly.
    ///
    /// It is a set rather than a depth because the game hangs its explanations in four different
    /// PLACES and a reading that asks for the wrong one loses them silently: on the widget, on the
    /// pieces drawn inside it, on the block it was drawn in, and on the wordless icon beside it.
    /// Every direction a screen does not ask for is one it cannot accidentally pick up either -
    /// which is what lets a shared resolver replace four hand-written collectors without any of
    /// them changing what it reads.
    /// </summary>
    [Flags]
    public enum TooltipReach
    {
        /// <summary>Nothing at all - for a caller that only wants the dedupe.</summary>
        None = 0,

        /// <summary>The widget's own tooltip.</summary>
        Own = 1,

        /// <summary>The tooltips on the things drawn INSIDE it, in the order the prefab lays them
        /// out.</summary>
        Descendants = 2,

        /// <summary>The tooltip on the block the widget was drawn in - the game writes these with
        /// <c>GetComponentInParent</c>, which takes the NEAREST ancestor carrying one and stops, so
        /// this walk does the same rather than collecting every container up to the window.</summary>
        Parents = 4,

        /// <summary>The tooltip on a wordless icon drawn BESIDE the widget - the caption for a value
        /// label, which the game hangs on the picture rather than on the number.</summary>
        Siblings = 8,

        /// <summary>A drop-list popup ITEM: the tooltip the engine wrote onto the item when the list
        /// was filled, which is where both of its per-item tables end up.</summary>
        ListEntry = 16,
    }

    /// <summary>
    /// What makes two tooltips the same tooltip: the class that renders it, the words it carries and
    /// the thing it is about.
    ///
    /// Identity is NOT the component reference. <c>AgeTooltip.Copy</c> clones a tooltip onto another
    /// widget - an event item copies its own onto the picture inside it - so a walk that dedupes by
    /// reference reads one explanation twice, once per copy, and the review buffer says everything
    /// the game says twice over. Comparing what the game would DRAW from instead collapses the copies
    /// and keeps two genuinely different tooltips apart even when a prefab hangs them on one widget.
    ///
    /// Engine-free on purpose: the target is held as <see cref="object"/> and compared by reference,
    /// which is the same question "is this the same thing being described" whatever the game's own
    /// type for it is.
    /// </summary>
    public struct TooltipKey : IEquatable<TooltipKey>
    {
        public readonly string Class;
        public readonly string Content;
        public readonly object Target;

        public TooltipKey(string cls, string content, object target)
        {
            // A prefab leaves an unset string as null and code that clears one writes "", and the
            // engine reads both as "nothing here" - so the two must not look like two tooltips.
            Class = cls ?? string.Empty;
            Content = content ?? string.Empty;
            Target = target;
        }

        public bool Equals(TooltipKey other)
        {
            return Class == other.Class
                && Content == other.Content
                && ReferenceEquals(Target, other.Target);
        }

        public override bool Equals(object obj)
        {
            return obj is TooltipKey && Equals((TooltipKey)obj);
        }

        public override int GetHashCode()
        {
            return Class.GetHashCode() ^ Content.GetHashCode();
        }
    }

    /// <summary>The tooltips already collected, as the keys that identify them - the dedupe half of
    /// the resolver, kept here so it is unit-testable off the engine.</summary>
    public sealed class TooltipSet
    {
        private readonly List<TooltipKey> _seen = new List<TooltipKey>(4);

        public int Count
        {
            get { return _seen.Count; }
        }

        public void Clear()
        {
            _seen.Clear();
        }

        /// <summary>Whether this tooltip is new - and remembers it when it is. The FIRST place a
        /// tooltip is found wins, so a resolver's declared order (containers, captions, the widget,
        /// then its pieces) is the order the player reads them in.</summary>
        public bool Add(TooltipKey key)
        {
            for (int i = 0; i < _seen.Count; i++)
            {
                if (_seen[i].Equals(key))
                {
                    return false;
                }
            }

            _seen.Add(key);
            return true;
        }

        public bool Has(TooltipKey key)
        {
            for (int i = 0; i < _seen.Count; i++)
            {
                if (_seen[i].Equals(key))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
