using System;
using System.Collections.Generic;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// The one place a node's EXISTENCE is decided against what the game is drawing.
    ///
    /// The AGE engine pools table rows: a table filled with <c>ReserveChildren</c> +
    /// <c>RefreshChildrenIList</c> never shrinks, and retires a surplus row by fading it to alpha 0
    /// while leaving it <c>Visible</c> and still holding the PREVIOUS binding's words. Every walk that
    /// decided existence for itself had to know that, and four of them did not - the ghosts announced
    /// a stale value with no name, and each was found by a bug report rather than by a check. So the
    /// decision is taken away from the walks: <see cref="GraphBuilder"/> asks this of every node it is
    /// about to make, and a walk that declares a retired row simply has that row dropped.
    ///
    /// The test is <see cref="AgeWidgets.Paints"/> on the carrier ALONE - one step, never
    /// <see cref="AgeWidgets.Painted"/>. Renders rebuild EVERY frame while a screen is focused, many
    /// screens gate their build on the window merely being shown, and several deliberately build
    /// during an arrival fade - and the game fades a window ROOT while every child stays at alpha 1.
    /// An ancestry-walking test would therefore blank a whole screen for the length of every arrival
    /// animation. One step asks only what the walk itself is responsible for: the row.
    ///
    /// A node with no carrier passes ungated (<see cref="NodeCarrier"/>).
    ///
    /// <para><b>The flag.</b> <see cref="Enabled"/> is a runtime switch, default OFF, flipped from the
    /// dev REPL without a rebuild. Like every static in this assembly it is re-initialised by a hot
    /// reload (the mod is loaded from bytes into a fresh assembly each time), so a flip does not
    /// survive <c>POST /reload</c> - and neither does <see cref="_reported"/>, which is why the drop
    /// log needs no teardown of its own.</para>
    ///
    /// <para><b>The log.</b> Every drop is reported once per screen+node, under the stable prefix
    /// <c>NodeGate drop:</c>, so a future leak surfaces in <c>GET /log?grep=NodeGate</c> instead of in
    /// a bug report. Deduped because the render rebuilds per frame; a permanent drop would otherwise
    /// be thousands of identical lines.</para>
    /// </summary>
    public static class NodeGate
    {
        /// <summary>Whether the gate drops anything at all. OFF until the live measurement battery has
        /// run; flip it from the REPL to measure.</summary>
        public static bool Enabled;

        /// <summary>Screen+node pairs already reported, so a per-frame rebuild logs a drop once. Capped
        /// rather than pruned: it exists to keep the log readable, and forgetting a whole session's
        /// worth costs one repeated line each.</summary>
        private const int MaxRemembered = 4000;

        private static readonly HashSet<string> _reported = new HashSet<string>();

        // One delegate per screen so a build does not allocate a closure per frame.
        private static readonly Dictionary<string, Func<ControlId, NodeVtable, bool>> _predicates =
            new Dictionary<string, Func<ControlId, NodeVtable, bool>>();

        /// <summary>The drop predicate to build a screen's render with. Held per screen key because the
        /// key is what makes a drop report readable.</summary>
        public static Func<ControlId, NodeVtable, bool> For(string screenKey)
        {
            string key = screenKey ?? "";
            Func<ControlId, NodeVtable, bool> predicate;
            if (!_predicates.TryGetValue(key, out predicate))
            {
                predicate = (id, vtable) => Drops(key, id, vtable);
                _predicates.Add(key, predicate);
            }

            return predicate;
        }

        /// <summary>Forget which drops have been reported - for a REPL session that wants the next
        /// build's drops logged again after flipping <see cref="Enabled"/>.</summary>
        public static void Forget()
        {
            _reported.Clear();
        }

        private static bool Drops(string screenKey, ControlId id, NodeVtable vtable)
        {
            if (!Enabled)
            {
                return false;
            }

            AgeTransform carrier = NodeCarrier.Of(id, vtable);
            if (carrier == null || AgeWidgets.Paints(carrier))
            {
                return false;
            }

            Report(screenKey, id, carrier);
            return true;
        }

        private static void Report(string screenKey, ControlId id, AgeTransform carrier)
        {
            string node = id == null ? "?" : Convert.ToString(id.StructuralKey);
            if (!_reported.Add(screenKey + " # " + node))
            {
                return;
            }

            if (_reported.Count > MaxRemembered)
            {
                _reported.Clear();
            }

            Log.Info(
                "NodeGate drop: screen="
                    + screenKey
                    + " node="
                    + node
                    + " at="
                    + NodeCarrier.Path(carrier)
                    + " why="
                    + Why(carrier)
            );
        }

        /// <summary>Which half of the one-step test said no. The two answers want different fixes: NOT
        /// VISIBLE is a branch the window switched off and a walk that kept its rows; FADED is the
        /// pooled table retiring a surplus row, which keeps its old words as well as its place.</summary>
        private static string Why(AgeTransform carrier)
        {
            try
            {
                return carrier.Visible ? "faded to nothing" : "not visible";
            }
            catch (Exception e)
            {
                return "reading it threw: " + e.GetType().Name;
            }
        }
    }
}
