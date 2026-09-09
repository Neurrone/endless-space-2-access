using System;
using System.Collections.Generic;
using ES2Access.Core.Util;
using UnityEngine;

namespace ES2Access.UI
{
    /// <summary>
    /// One component sweep of a widget subtree, held for the length of ONE frame.
    ///
    /// The accessible tree is immediate-mode: every screen rebuilds itself from live state every frame,
    /// which is what makes a stale cursor impossible. The cost that buys is paid in scene walks -
    /// <c>GetComponentsInChildren</c> from a panel root is O(subtree) - and the same subtree is walked
    /// several times in one frame, because the question is asked once by the screen's own "is this page
    /// still mine", once by its build, and again by whatever an announcement part resolves to. Nobody
    /// is walking it twice for a different answer: within one frame the game has not moved.
    ///
    /// So the sweep is remembered by (root, frame) and nothing else. Not for the life of the window:
    /// most of these roots are POOLED tables that add and retire rows between frames, and a cache that
    /// outlived the frame would answer for rows that have gone. Keying on the frame number rather than
    /// invalidating on an event is what makes that safe with nothing to remember to clear - the first
    /// call of the next frame drops the whole table.
    ///
    /// The last frame's entries are held until then, which is the one reference this keeps to game
    /// objects. There is no teardown step for it and none is needed: the cache lives in the mod
    /// assembly beside the game types it describes, so it dies when the assembly is replaced
    /// (<c>docs/generic/performance.md</c>, "Allocation discipline in hot paths").
    ///
    /// One instance per (component kind, subject) - the subject is what a failed walk is logged as.
    /// </summary>
    public sealed class FrameSweep<T>
        where T : Component
    {
        private static readonly T[] None = new T[0];

        private readonly string _subject;

        private readonly bool _inactiveToo;

        private readonly Dictionary<Component, T[]> _found = new Dictionary<Component, T[]>();

        private int _frame = -1;

        /// <param name="subject">What a walk that threw is logged as.</param>
        public FrameSweep(string subject)
            : this(subject, true) { }

        /// <param name="subject">What a walk that threw is logged as.</param>
        /// <param name="inactiveToo">The <c>includeInactive</c> the walk is made with. It is part of
        /// the ANSWER, not a detail: a caller that matched only the components the game has switched on
        /// would start matching switched-off ones if this were changed under it, so each sweep says
        /// which question it is asking and one sweep never serves both.</param>
        public FrameSweep(string subject, bool inactiveToo)
        {
            _subject = subject;
            _inactiveToo = inactiveToo;
        }

        /// <summary>Every <typeparamref name="T"/> under <paramref name="root"/> - the same answer
        /// <c>GetComponentsInChildren</c> gives for this sweep's <c>includeInactive</c>, walked at most
        /// once per root per frame. Never null: a missing root and a walk that threw both answer empty,
        /// so a caller reads the same "nothing there" it read before.</summary>
        public T[] Under(Component root)
        {
            try
            {
                int frame = Time.frameCount;
                if (_frame != frame)
                {
                    _found.Clear();
                    _frame = frame;
                }

                if (root == null)
                {
                    return None;
                }

                T[] hit;
                if (_found.TryGetValue(root, out hit))
                {
                    return hit;
                }

                hit = root.GetComponentsInChildren<T>(_inactiveToo);
                _found[root] = hit;
                return hit;
            }
            catch (Exception e)
            {
                Log.Warn(_subject + ": sweeping for " + typeof(T).Name + " threw: " + e);
                return None;
            }
        }
    }

    /// <summary>
    /// Every label of one kind a map-label WINDOW is holding, swept once per frame.
    ///
    /// The galaxy draws each family of map labels - the star names, the fleet lozenges, the docks, the
    /// hangars, the constellations, each lens's own - into a window of its own, and the mod matches an
    /// entity to its label by walking that window. Seven such walks existed, three of them holding the
    /// answer for a frame in three verbatim copies of the same eight lines and four of them repeating
    /// the walk for every system on the map. This is the one copy.
    ///
    /// The window is resolved by the caller (<paramref name="window"/> in the constructor), because
    /// which window and when it counts as up is the caller's knowledge: a lens whose window exists but
    /// is not shown holds labels bound to whatever it last drew, and that resolver answers null.
    /// A null window is an empty answer, never a null one.
    /// </summary>
    public sealed class LabelSweep<T>
        where T : Component
    {
        private readonly FrameSweep<T> _sweep;

        private readonly Func<Component> _window;

        public LabelSweep(string subject, Func<Component> window)
        {
            _sweep = new FrameSweep<T>(subject);
            _window = window;
        }

        public T[] Labels()
        {
            try
            {
                return _sweep.Under(_window());
            }
            catch (Exception)
            {
                return _sweep.Under(null);
            }
        }
    }
}
