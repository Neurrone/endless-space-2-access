using System.Collections.Generic;

namespace ES2Access.UI
{
    /// <summary>
    /// The cells one entry point declared on earlier builds, keyed on the widget each was read off,
    /// so a build re-creates only what it has not declared before. The global HUD and the collapsed
    /// tutorial bar are declared on every page on every frame, and the game's collector turns that
    /// allocation into pauses.
    ///
    /// A kept cell stays correct across builds only because every closure in it captures objects
    /// the game keeps for the session - the window, its widgets, the empire, a ModStrings key - and
    /// reads live state when it is called. What a declaration reads EAGERLY is the exception, so
    /// an entry is used only while those reads would still answer the same: what the widget was
    /// bound to (a pooled item's resource, a toggle's screen, the pinned quest), the tooltip it was
    /// declared with and that tooltip's class (which settle the section's mode and cost panel in
    /// <c>GraphNodes.TooltipSection</c>), and the long-tooltip option (which settles whether the
    /// section has a late reader). The whole set is dropped when the entry point reads from a
    /// different window, panel or empire than it was filled from.
    /// </summary>
    internal sealed class KeptCells<T>
        where T : class
    {
        /// <summary>More widgets than any one entry point draws: a table that made fresh children
        /// rather than pooling them would otherwise grow the set for the session.</summary>
        private const int Limit = 128;

        private struct Entry
        {
            public T Cell;
            public object Bound;
            public AgeTooltip Tip;
            public string TipClass;
        }

        private readonly Dictionary<object, Entry> _entries = new Dictionary<object, Entry>(
            ByReference.Instance
        );

        private object _owner;

        private object _ownerToo;

        private bool _longTooltips;

        /// <summary>Asked at the top of every build: drops everything kept when what the entry point
        /// reads from, or the long-tooltip option, is not what the set was filled under.</summary>
        public void Own(object owner, object ownerToo)
        {
            bool longTooltips = ES2Access.UI.Settings.LongTooltipSettings.Announced;
            if (
                ReferenceEquals(owner, _owner)
                && ReferenceEquals(ownerToo, _ownerToo)
                && longTooltips == _longTooltips
            )
            {
                return;
            }

            _entries.Clear();
            _owner = owner;
            _ownerToo = ownerToo;
            _longTooltips = longTooltips;
        }

        /// <summary>The cell kept for <paramref name="widget"/>, or null when there is none or it was
        /// declared against another binding or tooltip.</summary>
        public T Find(object widget, object bound, AgeTooltip tip)
        {
            Entry entry;
            if (widget == null || !_entries.TryGetValue(widget, out entry))
            {
                return null;
            }

            return ReferenceEquals(entry.Bound, bound)
                && ReferenceEquals(entry.Tip, tip)
                && string.Equals(entry.TipClass, ClassOf(tip))
                ? entry.Cell
                : null;
        }

        public T Keep(object widget, object bound, AgeTooltip tip, T cell)
        {
            if (widget == null)
            {
                return cell;
            }

            if (_entries.Count >= Limit && !_entries.ContainsKey(widget))
            {
                _entries.Clear();
            }

            _entries[widget] = new Entry
            {
                Cell = cell,
                Bound = bound,
                Tip = tip,
                TipClass = ClassOf(tip),
            };
            return cell;
        }

        public void Clear()
        {
            _entries.Clear();
            _owner = null;
            _ownerToo = null;
        }

        private static string ClassOf(AgeTooltip tip)
        {
            return tip == null ? null : tip.Class;
        }

        /// <summary>Widgets compared as objects: Unity's own equality asks the engine whether each
        /// side is still alive, on every lookup, and calls two destroyed widgets equal.</summary>
        private sealed class ByReference : IEqualityComparer<object>
        {
            public static readonly ByReference Instance = new ByReference();

            bool IEqualityComparer<object>.Equals(object a, object b)
            {
                return ReferenceEquals(a, b);
            }

            public int GetHashCode(object value)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
            }
        }
    }
}
