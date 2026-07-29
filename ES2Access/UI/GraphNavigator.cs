using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.Screens;
using ES2Access.UI.Input;

namespace ES2Access.UI
{
    /// <summary>
    /// Drives the key graph from the player's keys and says what happened. The engine in
    /// <see cref="KeyGraph"/> only moves a cursor and reports the outcome; every spoken word in the
    /// UI layer originates here.
    ///
    /// The design rule that makes it predictable: <b>one place announces a focus change, whatever
    /// caused it</b>. <see cref="EnsureFocus"/> runs at the end of every frame, compares where focus
    /// is against what was last spoken, and reads the difference. An arrow key, a screen choosing its
    /// landing spot, a rebuild that had to recover onto a surviving control, the game moving the world
    /// under us - all of them arrive at the same comparison, so nothing is announced twice and nothing
    /// is silently skipped. Handlers that want to speak immediately (so a held arrow reads the item
    /// you land on rather than queueing behind the previous one) do so and then write the differ's
    /// memory themselves, which is what "already said that" looks like here.
    ///
    /// Interruption follows intent: a navigation move interrupts, because the player asked for
    /// something newer than whatever is still being read. Arriving on a screen, and a control's state
    /// changing on its own, queue instead.
    ///
    /// One <see cref="GraphState"/> is kept per live screen, so returning to a page returns to where
    /// you were on it.
    ///
    /// The same comparison feeds the UI review buffer: whatever the focus readout says, the buffer is
    /// refilled with the long form of it, so the player can walk the description of the control they
    /// just landed on. It is refilled only when the readout actually changed - the screen rebuilds
    /// every frame, and a review cursor that reset on every rebuild would be unusable.
    /// </summary>
    public sealed class GraphNavigator
    {
        private readonly Dictionary<Screen, GraphState> _states = new Dictionary<Screen, GraphState>();
        private readonly BufferController _buffers;

        private Screen _screen;
        private GraphState _state;
        private KeyGraph _graph;

        // What the differ last read out, by identity and by node (the node carries the parent chain
        // the next readout is diffed against).
        private ControlId _lastSpokenKey;
        private GraphNode _lastSpokenNode;

        // A requested landing, applied on the next EnsureFocus.
        private ControlId _pendingFocus;
        private bool _pendingAnnounce;
        private object _pendingStop;

        // The live-part watch: the focus it is baselined against, and the last resolved text of each
        // effective announcement part (index-parallel, with nulls where a part is not live).
        private ControlId _liveKey;
        private readonly List<string> _liveValues = new List<string>();

        // What the UI review buffer currently holds: the control it was filled from, and the readout
        // it was filled at. A rebuild that produces the same readout for the same control leaves the
        // player's place in the buffer alone.
        private ControlId _bufferKey;
        private string _bufferReadout;

        // Which control the game is currently being made to look hovered on, and the node whose
        // hooks will undo it. Kept by id, not by object: the graph is rebuilt every frame, so the
        // node standing for a control is a different instance each time.
        private ControlId _visualKey;
        private GraphNode _visualNode;

        public GraphNavigator(BufferController buffers = null)
        {
            _buffers = buffers;
        }

        public Screen Screen
        {
            get { return _screen; }
        }

        public GraphNode CurrentNode
        {
            get { return _graph == null ? null : _graph.CurrentNode; }
        }

        /// <summary>Point the navigator at a screen (null when none is focused). The screen's cursor
        /// is restored if it has one, and the differ starts fresh so the arrival reads in full.</summary>
        public void Attach(Screen screen)
        {
            if (ReferenceEquals(screen, _screen))
            {
                return;
            }

            _screen = screen;
            _lastSpokenKey = null;
            _lastSpokenNode = null;
            _liveKey = null;
            _liveValues.Clear();
            _bufferKey = null;
            _bufferReadout = null;
            _pendingFocus = null;
            _pendingStop = null;
            ClearVisual();

            if (screen == null)
            {
                _state = null;
                _graph = null;
                return;
            }

            if (!_states.TryGetValue(screen, out _state))
            {
                _state = new GraphState();
                _states.Add(screen, _state);
            }

            Screen built = screen;
            GraphState state = _state;
            _graph = new KeyGraph(() => BuildRender(built, state), state);
        }

        /// <summary>Forget a closed screen's cursor, so re-opening it starts at the top.</summary>
        public void ScreenClosed(Screen screen)
        {
            if (screen != null)
            {
                _states.Remove(screen);
            }
        }

        /// <summary>Give up the cursor entirely; the next EnsureFocus seats it again.</summary>
        public void Blur()
        {
            if (_state != null)
            {
                _state.CurKey = null;
            }

            _lastSpokenKey = null;
            _lastSpokenNode = null;
            _liveKey = null;
            _liveValues.Clear();
            _bufferKey = null;
            _bufferReadout = null;
            ClearVisual();
        }

        /// <summary>Ask for focus to land on a control (a screen choosing where to put the player).
        /// Applied on the next tick, when the control is in the render.</summary>
        public void FocusNode(ControlId id, bool announce = true)
        {
            _pendingFocus = id;
            _pendingAnnounce = announce;
        }

        /// <summary>Re-read the focused control in full, ancestors included.</summary>
        public void AnnounceCurrent()
        {
            if (_graph == null || !_graph.Rerender())
            {
                return;
            }

            GraphNode node = _graph.CurrentNode;
            if (node == null)
            {
                return;
            }

            Voice.Say(GraphAnnouncer.ComposeFull(node), true);
            _lastSpokenKey = node.Id;
            _lastSpokenNode = node;
        }

        /// <summary>Run an action by name. The input layer calls this; so can the dev server, which is
        /// how navigation is tested without a keyboard.</summary>
        public bool Dispatch(string actionKey)
        {
            if (_screen == null || _graph == null)
            {
                return false;
            }

            switch (actionKey)
            {
                case UiActions.Up:
                    return Arrow(GraphDir.Up);
                case UiActions.Down:
                    return Arrow(GraphDir.Down);
                case UiActions.Left:
                    return Arrow(GraphDir.Left);
                case UiActions.Right:
                    return Arrow(GraphDir.Right);
                case UiActions.Next:
                    return Stop(1);
                case UiActions.Prev:
                    return Stop(-1);
                case UiActions.Home:
                    return JumpEdge(true);
                case UiActions.End:
                    return JumpEdge(false);
                case UiActions.RegionPrev:
                    return InRegion() && Region(-1);
                case UiActions.RegionNext:
                    return InRegion() && Region(1);
                case UiActions.Activate:
                    return Activate();
                case UiActions.Secondary:
                    return Secondary();
                case UiActions.Back:
                    return _screen.Back();
                default:
                    return false;
            }
        }

        /// <summary>
        /// End of frame: seat the cursor if it needs seating, announce it if it moved, and watch the
        /// focused control's live parts. The single announcement site.
        /// </summary>
        public void EnsureFocus()
        {
            if (_screen == null || _graph == null)
            {
                return;
            }

            if (_state.CurKey == null && _pendingFocus == null)
            {
                // No content yet - a window still animating in. Reconcile will seat the start node
                // as soon as there is something to seat it on.
                if (!_graph.Rerender())
                {
                    return;
                }

                object stop = _screen.InitialFocusStop;
                if (stop != null)
                {
                    GraphNode landing = KeyGraph.StopLanding(_graph.Current, _graph.State, stop);
                    if (landing != null)
                    {
                        _graph.Focus(landing.Id);
                    }
                }
            }
            else
            {
                if (!_graph.Rerender())
                {
                    return;
                }

                if (_pendingFocus != null)
                {
                    // One frame of grace: a control requested mid-build may only appear now. Still
                    // missing means it was removed, so drop the request rather than chase it forever.
                    if (_graph.Current.Nodes.ContainsKey(_pendingFocus))
                    {
                        _graph.Focus(_pendingFocus);
                        if (!_pendingAnnounce)
                        {
                            _lastSpokenKey = _pendingFocus;
                            _lastSpokenNode = _graph.CurrentNode;
                        }
                    }

                    _pendingFocus = null;
                }

                if (_pendingStop != null)
                {
                    GraphNode landing = KeyGraph.StopLanding(
                        _graph.Current,
                        _graph.State,
                        _pendingStop
                    );
                    if (landing != null)
                    {
                        _graph.Focus(landing.Id);
                    }

                    _pendingStop = null;
                }
            }

            GraphNode node = _graph.CurrentNode;
            if (node == null)
            {
                return;
            }

            if (_lastSpokenKey == null || !_lastSpokenKey.Equals(node.Id))
            {
                // Queued: an arrival follows the screen name rather than cutting it off.
                Voice.Say(GraphAnnouncer.Compose(_lastSpokenNode, node), false);
                _lastSpokenKey = node.Id;
                _lastSpokenNode = node;
            }

            SyncVisual(node);
            FillBuffer(node);
            WatchLive(node);
        }

        /// <summary>
        /// Point the game's own pointer feedback at the focused control - the hover highlight, a menu
        /// opening under it, its tooltip - so that someone watching the screen can follow where the
        /// keyboard is. Nothing here speaks; the hooks are the screen's, and a screen that does not
        /// set them simply looks untouched.
        ///
        /// Alongside the announcement, in the same place and on the same comparison: whatever moved
        /// focus, the game's appearance follows it exactly once.
        /// </summary>
        private void SyncVisual(GraphNode node)
        {
            if (_visualKey != null && _visualKey.Equals(node.Id))
            {
                return;
            }

            ClearVisual();
            _visualKey = node.Id;
            _visualNode = node;
            Safe(node.Vtable.OnFocusVisual, "OnFocusVisual");
        }

        /// <summary>Leave the game looking as though nothing were hovered - focus has gone somewhere
        /// we do not describe, or the mod is going away.</summary>
        public void ClearVisual()
        {
            if (_visualNode != null)
            {
                Safe(_visualNode.Vtable.OnBlurVisual, "OnBlurVisual");
            }

            _visualKey = null;
            _visualNode = null;
        }

        private static void Safe(Action action, string what)
        {
            if (action == null)
            {
                return;
            }

            try
            {
                action();
            }
            catch (Exception e)
            {
                Log.Warn("nav: " + what + " threw: " + e);
            }
        }

        /// <summary>
        /// Refill the UI review buffer from the focused control - its name, the state words its
        /// readout would append, then its detail lines.
        ///
        /// Only when something actually changed. The screen is rebuilt every frame from live game
        /// state, so "the focus moved" cannot be answered by object identity; it is answered by the
        /// control's id and the readout it composes to. Sitting still on a control therefore keeps the
        /// player's place in the buffer, and a control that changes under them (a button that becomes
        /// available, its reason gone) refills with the truth.
        /// </summary>
        private void FillBuffer(GraphNode node)
        {
            if (_buffers == null)
            {
                return;
            }

            string readout = GraphAnnouncer.LeafText(node);
            if (
                _bufferKey != null
                && _bufferKey.Equals(node.Id)
                && string.Equals(_bufferReadout, readout)
            )
            {
                return;
            }

            _bufferKey = node.Id;
            _bufferReadout = readout;
            _buffers.ReplaceUiLines(BufferLines(node));
        }

        private static List<string> BufferLines(GraphNode node)
        {
            List<string> lines = new List<string>();
            string label = GraphAnnouncer.FirstPartText(node);
            Add(lines, label);

            // The state words the focus readout appends - "unavailable", "expanded". The role word
            // and the auto-stamped position are left out: they describe the control, and the buffer
            // is for what the control has to say. So is the tooltip part - whether it announces the
            // text or only says there is one, the tooltip's own lines follow below.
            List<NodeAnnouncement> parts = GraphAnnouncer.EffectiveAnnouncements(node);
            for (int i = 0; i < parts.Count; i++)
            {
                NodeAnnouncement part = parts[i];
                if (
                    part == null
                    || part.Kind == AnnouncementKinds.Label
                    || part.Kind == AnnouncementKinds.Role
                    || part.Kind == AnnouncementKinds.Tooltip
                )
                {
                    continue;
                }

                Add(lines, Resolve(part.Text));
            }

            if (
                node.Expandable
                && !node.Vtable.SpeaksOwnExpansion
                && GraphAnnouncer.ExpandedStateText != null
            )
            {
                Add(lines, GraphAnnouncer.ExpandedStateText(node.Expanded));
            }

            IList<string> details = ResolveDetails(node);
            for (int i = 0; i < details.Count; i++)
            {
                // A tooltip whose first line is just the control's name again: the buffer already
                // opened with it. Only an exact repeat is dropped, so a heading that adds anything
                // still reads.
                if (i == 0 && IsSameText(label, details[i]))
                {
                    continue;
                }

                Add(lines, details[i]);
            }

            return lines;
        }

        private static readonly List<string> NoDetails = new List<string>();

        private static IList<string> ResolveDetails(GraphNode node)
        {
            Func<IList<string>> details = node.Vtable.DetailLines;
            if (details == null)
            {
                return NoDetails;
            }

            try
            {
                return details() ?? NoDetails;
            }
            catch (Exception)
            {
                return NoDetails;
            }
        }

        private static string Resolve(Func<string> text)
        {
            try
            {
                return text == null ? null : text();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool IsSameText(string left, string right)
        {
            return !string.IsNullOrEmpty(left)
                && !string.IsNullOrEmpty(right)
                && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static void Add(List<string> lines, string line)
        {
            if (!string.IsNullOrEmpty(line))
            {
                lines.Add(line);
            }
        }

        private GraphRender BuildRender(Screen screen, GraphState state)
        {
            try
            {
                GraphBuilder builder = new GraphBuilder(state.Expanded);
                screen.Build(builder);
                return builder.Build();
            }
            catch (Exception e)
            {
                Log.Warn("nav: " + screen.Key + ".Build threw: " + e);
                return null;
            }
        }

        // Left/right first offer to adjust a value, then to move along a wired edge, and only then
        // fall back on tree semantics - so a slider adjusts, a row steps sideways, and a group
        // expands, each without knowing about the others.
        private bool Arrow(GraphDir dir)
        {
            GraphNode focused = _graph.CurrentNode;
            if (focused == null)
            {
                return false;
            }

            bool horizontal = dir == GraphDir.Left || dir == GraphDir.Right;
            if (horizontal && Adjust(dir == GraphDir.Right ? 1 : -1))
            {
                return true;
            }

            MoveResult move = _graph.Move(dir);
            if (move.Moved)
            {
                AnnounceMove(move);
                return true;
            }

            if (horizontal)
            {
                KeyGraph.TreeResult tree =
                    dir == GraphDir.Right ? _graph.TreeRight() : _graph.TreeLeft();
                switch (tree.Kind)
                {
                    case KeyGraph.TreeMove.Expanded:
                    case KeyGraph.TreeMove.Collapsed:
                        SpeakFocusedState();
                        return true;
                    case KeyGraph.TreeMove.EmptyGroup:
                        Voice.Say(ModStrings.Get(ModStrings.NavNoDetails), true);
                        return true;
                    case KeyGraph.TreeMove.Descended:
                    case KeyGraph.TreeMove.Ascended:
                        AnnounceMove(tree.Move);
                        return true;
                    case KeyGraph.TreeMove.Leaf:
                        return true;
                }
            }

            // Nothing that way. Inside a tree the key is still ours (there is nowhere to bubble to
            // that would make sense); on a plain list it falls through.
            return KeyGraph.InTree(focused);
        }

        private bool Stop(int step)
        {
            MoveResult move = _graph.MoveStop(step, false);
            if (move.Moved)
            {
                AnnounceMove(move);
            }

            return true;
        }

        private bool JumpEdge(bool first)
        {
            GraphNode node = _graph.CurrentNode;
            if (node == null)
            {
                return false;
            }

            MoveResult move = KeyGraph.InTree(node)
                ? _graph.MoveToSiblingEdge(first)
                : _graph.MoveToEdge(first ? GraphDir.Up : GraphDir.Down);
            if (move.Moved)
            {
                AnnounceMove(move);
            }

            return true;
        }

        private bool InRegion()
        {
            GraphNode node = _graph.CurrentNode;
            return node != null && node.RegionKey != null;
        }

        private bool Region(int step)
        {
            MoveResult move = _graph.MoveRegion(step);
            if (move.Moved)
            {
                AnnounceMove(move);
            }

            return true;
        }

        private bool Activate()
        {
            GraphNode node = _graph.CurrentNode;
            if (node == null)
            {
                return false;
            }

            if (node.Vtable.OnActivate != null)
            {
                _graph.Activate();
                SpeakStateAfterChange();
            }

            return true;
        }

        private bool Secondary()
        {
            GraphNode node = _graph.CurrentNode;
            if (node == null)
            {
                return false;
            }

            if (node.Vtable.OnSecondary != null)
            {
                _graph.Secondary();
                SpeakStateAfterChange();
            }

            return true;
        }

        private bool Adjust(int sign)
        {
            GraphNode node = _graph.CurrentNode;
            if (node == null || node.Vtable.OnAdjust == null)
            {
                return false;
            }

            _graph.TryAdjust(sign, false);
            SpeakStateAfterChange();
            return true;
        }

        // The synchronous half of state feedback: an action the player just took reports its result
        // at once, interrupting, so holding a key down reads every step instead of falling behind.
        // The control is re-read after the action, since acting on it re-rendered the graph.
        private void SpeakStateAfterChange()
        {
            GraphNode node = _graph.CurrentNode;
            Func<string> state = node == null ? null : node.Vtable.StateText;
            if (state == null)
            {
                return;
            }

            Voice.Say(state(), true);

            // The change has just been spoken; re-baseline so the live watch does not say it again.
            _liveKey = null;
        }

        private void SpeakFocusedState()
        {
            GraphNode node = _graph.CurrentNode;
            if (node == null)
            {
                return;
            }

            Voice.Say(GraphAnnouncer.LeafText(node), true);
            _lastSpokenKey = node.Id;
            _lastSpokenNode = node;
            _liveKey = null;
        }

        private void AnnounceMove(MoveResult result)
        {
            GraphNode node = result.To;
            if (node == null)
            {
                return;
            }

            Voice.Say(GraphAnnouncer.Compose(result.From, node, result.TransitionLabel), true);
            _lastSpokenKey = node.Id;
            _lastSpokenNode = node;
        }

        /// <summary>
        /// Watches the focused control's live parts and speaks the ones that change - a button that
        /// becomes unavailable, a value the game flips on its own. Nothing is spoken on the frame the
        /// baseline is taken: the focus readout has just said all of it.
        /// </summary>
        private void WatchLive(GraphNode node)
        {
            List<NodeAnnouncement> parts = GraphAnnouncer.EffectiveAnnouncements(node);
            if (parts.Count == 0)
            {
                return;
            }

            bool baseline =
                _liveKey == null
                || !_liveKey.Equals(node.Id)
                || _liveValues.Count != parts.Count;
            if (baseline)
            {
                _liveKey = node.Id;
                _liveValues.Clear();
            }

            for (int i = 0; i < parts.Count; i++)
            {
                NodeAnnouncement part = parts[i];
                if (part == null || !part.Live)
                {
                    // A placeholder keeps the value list index-parallel with the parts.
                    if (baseline)
                    {
                        _liveValues.Add(null);
                    }

                    continue;
                }

                string text = null;
                try
                {
                    if (part.Text != null)
                    {
                        text = part.Text();
                    }
                }
                catch (Exception) { }

                if (baseline)
                {
                    _liveValues.Add(text);
                    continue;
                }

                if (!string.Equals(_liveValues[i], text))
                {
                    _liveValues[i] = text;
                    Voice.Say(text, false);
                }
            }
        }
    }
}
