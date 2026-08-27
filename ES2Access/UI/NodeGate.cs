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
    /// <para><b>What is asked, and of what.</b> The node's NATURE decides. A
    /// <see cref="DrawnNode"/> was declared with the widget that vouches for it, and
    /// <see cref="Withdrawn"/> is asked of that widget AND of everything it hangs from. A
    /// <see cref="SyntheticNode"/> is untestable by construction and passes: there is no widget in
    /// its declaration to ask, and honesty about its existence lives at the walk that enumerated
    /// it.</para>
    ///
    /// <para><b>Why the whole chain.</b> The engine's flags do not cascade: a window hides a branch by
    /// flipping the PARENT's <c>Visible</c> or fading the parent to nothing, and every child under it
    /// keeps <c>Visible == true</c> and <c>Alpha == 1</c>. So a one-step test says yes to a node the
    /// player cannot see, and ~96 walk-level entry gates had to say no for it, one site at a time. The
    /// chain is what the RENDERER itself asks: <c>AgeTransform.PrimitiveUpdateGUI</c>
    /// (<c>firstpass/AgeTransform.cs:1955-1958</c>) early-outs at <c>!visible || Alpha == 0f</c> before
    /// it draws anything or recurses, and the recursion into children is the only way a child is ever
    /// reached (<c>:2020-2026</c>, and <c>AgePrimitive.UpdateGUI</c> <c>:317-326</c> for a parent that
    /// carries a primitive of its own). Because that walk is TOP-DOWN, every ancestor is a gate the
    /// renderer passed before it reached the widget - which is why this walk does not stop early at
    /// one that is merely animating.</para>
    ///
    /// <para><b>What the walk does NOT do.</b> It never turns an arrival fade into a blank screen,
    /// because the settled test is applied per step: a window fading itself in is transparent AND
    /// animating, and counts as drawn. Measured on the four opening styles that fade (2026-08-27): the
    /// improvements and laws modals each hold <c>MainContainer</c> at alpha 0 with modifiers running
    /// for several frames while the window root climbs 0 -> 1, the research screen and the senate the
    /// same on the root alone, and the pause menu is the one that builds its items mid-fade
    /// (<c>SaveGameMenuItem</c> at alpha 0.92, modifiers running, every ancestor at alpha 1). Across
    /// every frame of all six traced arrivals, no declared node's chain ever read hidden-or-settled
    /// that the one-step test did not already read - zero would-be false drops. Cost: the whole
    /// ancestry pass over the largest measured screen (the star-system page with its cards expanded,
    /// 90 located widgets, mean chain 6.8 deep) is 0.015 ms against a 1.56 ms render rebuild.</para>
    ///
    /// <para><b>Before the node exists.</b> One walk has to ask the question earlier than this:
    /// <see cref="Cells"/> groups its widgets into rows by their RECTANGLES before it declares
    /// anything, and a ghost's stale rectangle merges or splits the bands the player hears counted.
    /// That walk asks <see cref="StillDrawn"/>, which is this same test under the same flag - not a
    /// second copy of it.</para>
    ///
    /// <para><b>The flag.</b> <see cref="Enabled"/> is a runtime switch, default ON, flipped from the
    /// dev REPL without a rebuild. It is kept past the measurement battery it was built for, because
    /// turning it off and on around one dump is how a screen's drops are MEASURED: the difference
    /// between the two renders is exactly what the gate is taking away. Like every static in this assembly it is re-initialised by a hot
    /// reload (the mod is loaded from bytes into a fresh assembly each time), so a flip does not
    /// survive <c>POST /reload</c> - and neither does <see cref="_reported"/>, which is why the drop
    /// log needs no teardown of its own.</para>
    ///
    /// <para><b>The log.</b> Every drop is reported once per screen+node, under the stable prefix
    /// <c>NodeGate drop:</c>, so a future leak surfaces in <c>GET /log?grep=NodeGate</c> instead of in
    /// a bug report. Deduped because the render rebuilds per frame; a permanent drop would otherwise
    /// be thousands of identical lines. A misdeclaration (<see cref="Nodes.Synthetic"/>) reports the
    /// same way, under <c>NodeGate misdeclared:</c>.</para>
    /// </summary>
    public static class NodeGate
    {
        /// <summary>Whether the gate drops anything at all. ON: the measurement battery has run. Flip it
        /// off from the REPL and dump, flip it back and dump again, and the diff is this screen's
        /// drops. That is the flag's WHOLE job - a dev verification lever, never a feature switch:
        /// production code must neither read nor write it (the gate-lever lint enforces the
        /// allowlist; dev probes that flip it restore it in a finally).</summary>
        public static bool Enabled = true;

        /// <summary>Screen+node pairs already reported, so a per-frame rebuild logs a drop once. Capped
        /// rather than pruned: it exists to keep the log readable, and forgetting a whole session's
        /// worth costs one repeated line each.</summary>
        private const int MaxRemembered = 4000;

        private static readonly HashSet<string> _reported = new HashSet<string>();

        /// <summary>Whose render is being built, so a drop taken BEFORE the builder sees the node
        /// (<see cref="StillDrawn"/>) - or a misdeclaration caught at the declaration door - is
        /// reported under the same screen key the builder's own drops are. Written by
        /// <see cref="BuildingIs"/> at the top of every build.</summary>
        private static string _building = "";

        // One delegate per screen so a build does not allocate a closure per frame.
        private static readonly Dictionary<string, Func<NodeDeclaration, bool>> _predicates =
            new Dictionary<string, Func<NodeDeclaration, bool>>();

        /// <summary>The drop predicate to build a screen's render with. Held per screen key because the
        /// key is what makes a drop report readable.</summary>
        public static Func<NodeDeclaration, bool> For(string screenKey)
        {
            string key = BuildingIs(screenKey);
            Func<NodeDeclaration, bool> predicate;
            if (!_predicates.TryGetValue(key, out predicate))
            {
                predicate = node => Drops(key, node);
                _predicates.Add(key, predicate);
            }

            return predicate;
        }

        /// <summary>Whose render is being built - the screen key a report outside the builder's own
        /// call is filed under.</summary>
        public static string Building
        {
            get { return _building; }
        }

        /// <summary>
        /// Say whose render is about to be built, and answer the key as it will be filed.
        ///
        /// Every build says this, not only a gated one. The drops taken BEFORE the builder sees a node
        /// (<see cref="StillDrawn"/>, from <see cref="Cells"/> and <see cref="CardActions"/>) and the
        /// misdeclarations caught at the declaration door have no node to read a screen off, so they
        /// read this - and a build that fetches no predicate used to leave it holding whatever screen
        /// last built WITH one. The by-key dump and the audits build exactly that way, which filed
        /// their drops under the focused screen: 466 of 891 logged lines named the wrong screen.
        /// </summary>
        public static string BuildingIs(string screenKey)
        {
            _building = screenKey ?? "";
            return _building;
        }

        /// <summary>Forget which drops have been reported - for a REPL session that wants the next
        /// build's drops logged again after flipping <see cref="Enabled"/>.</summary>
        public static void Forget()
        {
            _reported.Clear();
        }

        /// <summary>
        /// The same question the gate asks, for the ONE walk that has to ask it before a node exists.
        ///
        /// A cell list is grouped into rows GEOMETRICALLY (<see cref="AgeLayout.Rows"/>) before
        /// anything is declared, so a retired ghost's stale rectangle merges or splits the bands the
        /// player then hears counted - the gate, which only ever sees finished nodes, cannot undo
        /// that. So <see cref="Cells"/> asks here instead, with the gate's own <see cref="Withdrawn"/>
        /// rather than a second opinion: two tests that could disagree would band by one rule and
        /// declare by another.
        ///
        /// Honours <see cref="Enabled"/> like every other drop, so flipping the flag around a dump
        /// measures the banding path exactly as it measures the gate, and reports through the same
        /// log line. A null widget is nothing to ask, and passes.
        /// </summary>
        public static bool StillDrawn(AgeTransform widget, ControlId id = null)
        {
            if (!Enabled || widget == null)
            {
                return true;
            }

            AgeTransform hider = Hider(widget);
            if (hider == null)
            {
                return true;
            }

            Report(_building, id, widget, hider);
            return false;
        }

        private static bool Drops(string screenKey, NodeDeclaration node)
        {
            if (!Enabled)
            {
                return false;
            }

            AgeTransform widget = DrawnBy.Of(node);
            if (widget == null)
            {
                return false;
            }

            AgeTransform hider = Hider(widget);
            if (hider == null)
            {
                return false;
            }

            Report(screenKey, node.Id, widget, hider);
            return true;
        }

        /// <summary>Whether the widget is off the screen AND not on its way onto it - the boolean form
        /// of <see cref="Hider"/>, for a caller with no report to write.</summary>
        private static bool Withdrawn(AgeTransform widget)
        {
            return Hider(widget) != null;
        }

        /// <summary>
        /// Which widget in the chain took this node off the screen - the node's own, or the ancestor the
        /// renderer's recursion stops at - and null when the renderer would draw it.
        ///
        /// Each step asks the same two things the renderer asks. Switched off is settled by definition.
        /// Transparent is not: the same alpha 0 is both a pooled row parked for reuse and the FIRST FRAME
        /// of a window fading itself in, and the engine tells them apart by whether that widget's
        /// modifiers are still running (<c>firstpass/AgeTransform.cs:466</c>). The pause menu builds its
        /// items during its arrival fade, so a plain <see cref="AgeWidgets.Paints"/> test dropped
        /// <c>ResumeMenuItem</c> for one frame - and one frame is enough to displace the cursor for the
        /// rest of the screen's life. A parked pool ghost has no modifier running and still drops, which
        /// is the whole point of the gate.
        ///
        /// An animating ancestor counts as drawn and the walk CONTINUES upward rather than stopping: the
        /// renderer descends, so the ancestors above an animating one are gates it had already passed
        /// before it reached the animation, and exempting them would be exempting tests the renderer
        /// applies. This is <see cref="AgeWidgets.Painted"/>'s walk plus that one exemption -
        /// deliberately not shared with it, because <see cref="Dev.GhostAudit"/> wants the answer for the
        /// frame it is asked on and this wants the answer for a node's whole life.
        ///
        /// Reading it throwing is not evidence of a ghost: the node passes.
        /// </summary>
        private static AgeTransform Hider(AgeTransform widget)
        {
            try
            {
                AgeTransform at = widget;
                for (int depth = 0; at != null && depth < MaxAncestors; depth++)
                {
                    if (!at.Visible || (at.Alpha <= 0f && !at.ModifiersRunning))
                    {
                        return at;
                    }

                    at = at.Parent;
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>How far up a parent chain to look before deciding it is not a chain. The deepest
        /// declared node measured in this game sits 10 widgets from its renderer root.</summary>
        private const int MaxAncestors = 64;

        private static void Report(
            string screenKey,
            ControlId id,
            AgeTransform widget,
            AgeTransform hider
        )
        {
            string node = id == null ? "?" : Convert.ToString(id.StructuralKey);
            if (!Remember(screenKey, node))
            {
                return;
            }

            Log.Info(
                "NodeGate drop: screen="
                    + screenKey
                    + " node="
                    + node
                    + " at="
                    + DrawnBy.Path(widget)
                    + " why="
                    + Why(widget, hider)
            );
        }

        /// <summary>Whether this screen+node pair is being reported for the first time.</summary>
        internal static bool Remember(string screenKey, string node)
        {
            if (!_reported.Add(screenKey + " # " + node))
            {
                return false;
            }

            if (_reported.Count > MaxRemembered)
            {
                _reported.Clear();
            }

            return true;
        }

        /// <summary>Which half of the test said no, and WHERE. The answers want different fixes: NOT
        /// VISIBLE is a branch the window switched off and a walk that kept its rows; FADED AND SETTLED
        /// is the pooled table retiring a surplus row, which keeps its old words as well as its place;
        /// and either of them on an ANCESTOR is a walk that entered through a root it never gated -
        /// named, because the ancestor is the widget whose flags have to be looked at.</summary>
        private static string Why(AgeTransform widget, AgeTransform hider)
        {
            try
            {
                string half = hider.Visible ? "faded to nothing and settled" : "not visible";
                return ReferenceEquals(widget, hider)
                    ? half
                    : "ancestor " + half + " (" + hider.name + ")";
            }
            catch (Exception e)
            {
                return "reading it threw: " + e.GetType().Name;
            }
        }
    }
}
