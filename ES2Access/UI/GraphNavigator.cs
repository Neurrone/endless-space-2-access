using System;
using System.Collections.Generic;
using System.Text;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
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

        // A stop the NEXT screen attached should land on - see LandOnStopAfterClose.
        private object _landingStop;

        // The live-part watch: the focus it is baselined against, and the last resolved text of each
        // effective announcement part (index-parallel, with nulls where a part is not live).
        private ControlId _liveKey;
        private readonly List<string> _liveValues = new List<string>();

        // What the UI review buffer currently holds: the control it was filled from, the readout it was
        // filled at, and the lines themselves. A rebuild that produces the same readout for the same
        // control leaves the player's place in the buffer alone, and so does one whose lines come out
        // the same - a buffer replaced with its own contents would still send the player back to the
        // first line of what they were reading.
        private ControlId _bufferKey;
        private string _bufferReadout;
        private List<string> _bufferLines;

        // Which control the game is currently being made to look hovered on, and the node whose
        // hooks will undo it. Kept by id, not by object: the graph is rebuilt every frame, so the
        // node standing for a control is a different instance each time.
        private ControlId _visualKey;
        private GraphNode _visualNode;

        // What the player is holding, if anything. Owned here because the carry key is dispatched
        // here and because a carry is scoped to the screen it started on, which is what this class
        // already tracks; ModEntry.Carry is the same object, for the screens that declare what can be
        // picked up and what will take a drop.
        private readonly CarryState _carry = new CarryState();

        public GraphNavigator(BufferController buffers = null)
        {
            _buffers = buffers;
            _typeAhead.OnLand = LandOnSearchResult;
            _typeAhead.OnNoMatch = SayNoMatch;
        }

        /// <summary>What the player is carrying - see <see cref="CarryState"/>. Never null; an empty
        /// carry is the normal state.</summary>
        public CarryState Carry
        {
            get { return _carry; }
        }

        public Screen Screen
        {
            get { return _screen; }
        }

        public GraphNode CurrentNode
        {
            get { return _graph == null ? null : _graph.CurrentNode; }
        }

        /// <summary>Where the cursor is, without needing the render it points into - for a caller that
        /// wants to mark the focused control without moving it.</summary>
        public ControlId FocusedKey
        {
            get { return _state == null ? null : _state.CurKey; }
        }

        /// <summary>
        /// A render of the focused screen built purely to be READ - the dev server's accessible-tree
        /// dump. Exactly the build path navigation uses, so what the dump shows is what navigation
        /// sees; and nothing else, so reading the screen cannot change it: the cursor is untouched, no
        /// focus/blur visual runs, and the render goes away with the caller.
        /// </summary>
        public GraphRender InspectRender()
        {
            return _screen == null ? null : BuildRender(_screen, _state);
        }

        /// <summary>
        /// The same read-only render for a screen that is NOT the focused one - what the dev server
        /// answers when asked what some other registered screen would offer. The screen's own
        /// expansion state is used when it has one, so asking about the focused screen this way
        /// gives exactly what <see cref="InspectRender()"/> gives; a screen the navigator has never
        /// been attached to is built against a throwaway state, which nothing else can see.
        ///
        /// Unlike <see cref="InspectRender()"/> this lets a failure through instead of logging it:
        /// a screen whose page the game has not bound throws here, and WHY it threw is the answer
        /// the caller wanted.
        /// </summary>
        public GraphRender InspectRender(Screen screen)
        {
            if (screen == null)
            {
                return null;
            }

            GraphState state;
            if (!_states.TryGetValue(screen, out state))
            {
                state = new GraphState();
            }

            GraphBuilder builder = new GraphBuilder(state.Expanded);
            screen.Build(builder);
            return builder.Build();
        }

        /// <summary>Point the navigator at a screen (null when none is focused). The screen's cursor
        /// is restored if it has one, and the differ starts fresh so the arrival reads in full.</summary>
        public void Attach(Screen screen)
        {
            if (ReferenceEquals(screen, _screen))
            {
                return;
            }

            // A carry belongs to the page it started on - that is where its drop targets are - but a
            // menu opened over that page is still that page, so a player can pick something up, open
            // an action menu and come back still holding it.
            _carry.ScreenChanged(SameFamily(screen, _carry.Owner as Screen));

            _screen = screen;
            ClearSearch();
            _lastSpokenKey = null;
            _lastSpokenNode = null;
            _liveKey = null;
            _liveValues.Clear();
            _bufferKey = null;
            _bufferReadout = null;
            _bufferLines = null;
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

            if (_landingStop != null)
            {
                _pendingStop = _landingStop;
                _landingStop = null;
            }
        }

        /// <summary>
        /// Ask for the cursor to land on a stop of whatever screen is focused NEXT - how a surface that
        /// puts ITSELF away hands the player back to the control that opened it.
        ///
        /// A closing surface knows where the player came in from but not what page will be underneath when
        /// it goes, and it cannot reach that page's cursor: every screen keeps its own. So it leaves the
        /// request here and the next <see cref="Attach"/> spends it. The request is spent whether or not
        /// the stop exists there, so it can never surface on a page nobody asked about; a page without the
        /// stop simply keeps the cursor it had.
        ///
        /// Only for a surface the player DISMISSED. A control that closes the surface by going somewhere
        /// else - a notification's Inspect, its link to a screen - wants the page it opened, not the list
        /// it came from.
        /// </summary>
        public void LandOnStopAfterClose(object stopKey)
        {
            _landingStop = stopKey;
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

            ClearSearch();
            _lastSpokenKey = null;
            _lastSpokenNode = null;
            _liveKey = null;
            _liveValues.Clear();
            _bufferKey = null;
            _bufferReadout = null;
            _bufferLines = null;
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

            if (actionKey == UiActions.Carry && _typeAhead.HasBuffer)
            {
                // A space typed into a search is TEXT, and the search takes it in TypeAheadTick.
                // Claimed all the same, so the game does not also act on it.
                return true;
            }

            if (_typeAhead.IsActive && SearchAction(actionKey))
            {
                return true;
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
                case UiActions.CoarseIncrease:
                    return Adjust(1, true);
                case UiActions.CoarseDecrease:
                    return Adjust(-1, true);
                case UiActions.Activate:
                    return Activate();
                case UiActions.Secondary:
                    return Secondary();
                case UiActions.Alternate:
                    return Alternate();
                case UiActions.Contextual:
                    return Contextual();
                case UiActions.Carry:
                    return CarryKey();
                case UiActions.SelectToggle:
                    return SelectChord(false);
                case UiActions.SelectRange:
                    return SelectChord(true);
                case UiActions.Back:
                    // Putting down what is being held comes before anything the screen does with the
                    // key: the carry is the mode the player is in, and the screen underneath is not.
                    return CancelCarry() || _screen.Back();
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
        ///
        /// Scrolling the focused control into view is done here rather than by the screens, and needs
        /// nothing declared: a control that named the game object it came from can be found on screen,
        /// and whether anything above it scrolls is a question about the game's own hierarchy. So it
        /// costs a screen nothing and is never forgotten.
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
            ScrollIntoView.Reveal(node.Id.Reference);
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
            List<string> lines = BufferLines(node);
            if (Same(_bufferLines, lines))
            {
                return;
            }

            _bufferLines = lines;
            _buffers.ReplaceUiLines(lines);
        }

        /// <summary>Fill the focused control's buffer again as soon as anything asks. The control's
        /// description is not always there the moment focus lands on it - a tooltip the game has to
        /// draw before its words exist - so whoever notices it arrive says so here.</summary>
        public void InvalidateBuffer()
        {
            _bufferKey = null;
            _bufferReadout = null;
        }

        private static bool Same(List<string> left, List<string> right)
        {
            if (left == null || right == null || left.Count != right.Count)
            {
                return false;
            }

            for (int i = 0; i < left.Count; i++)
            {
                if (!string.Equals(left[i], right[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>The lines a control fills the UI review buffer with - the buffer half of the
        /// projection, in <see cref="NodeBuffer"/> so that both halves of it are testable off the
        /// engine. Public here because the dev server's graph dump shows what a control has to say
        /// without focusing it.</summary>
        public static List<string> BufferLines(GraphNode node)
        {
            return NodeBuffer.Lines(node);
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
            if (horizontal && Adjust(dir == GraphDir.Right ? 1 : -1, false))
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
                : _graph.MoveToEdge(EdgeDir(node, first));
            if (move.Moved)
            {
                AnnounceMove(move);
            }

            return true;
        }

        /// <summary>
        /// Which way "the start" and "the end" lie: along whichever axis this stop's nodes are actually
        /// wired.
        ///
        /// Down the column where there is one, which is what a list and a table both want - Home in a
        /// table goes to the top of the column the player is comparing. A stop laid out as a single ROW
        /// has no vertical edges at all, and asking for one there simply did nothing: Home and End were
        /// silent on every band of buttons in the mod (measured on the ship designer's Close / Auto Design
        /// / Create row). So a node with nothing above or below it is asked sideways instead.
        /// </summary>
        private static GraphDir EdgeDir(GraphNode node, bool first)
        {
            bool vertical = Wired(node, GraphDir.Up) || Wired(node, GraphDir.Down);
            if (vertical)
            {
                return first ? GraphDir.Up : GraphDir.Down;
            }

            return first ? GraphDir.Left : GraphDir.Right;
        }

        private static bool Wired(GraphNode node, GraphDir dir)
        {
            Transition transition;
            return node.Transitions != null
                && node.Transitions.TryGetValue(dir, out transition)
                && transition != null;
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

        // Enter. While something is being carried this is also the key that PUTS IT DOWN: on a
        // control that will take the cargo it drops there and nothing else happens, and on every
        // other control it is the plain click it always was, with the carry still live underneath.
        private bool Activate()
        {
            GraphNode node = _graph.CurrentNode;
            if (node == null)
            {
                return false;
            }

            if (_carry != null && _carry.IsCarrying)
            {
                if (!_graph.Rerender())
                {
                    return false;
                }

                node = _graph.CurrentNode;
                CarryOutcome drop = CarryActions.Activate(
                    node == null ? null : node.Vtable,
                    _carry
                );
                if (drop.Handled)
                {
                    Voice.Say(drop.Speech, true);
                    return true;
                }

                if (node == null)
                {
                    return false;
                }
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

        private bool Alternate()
        {
            GraphNode node = _graph.CurrentNode;
            if (node == null)
            {
                return false;
            }

            if (node.Vtable.OnAlternate != null)
            {
                _graph.Alternate();
                SpeakStateAfterChange();
            }

            // Claimed either way: a control that has no other activation must not let the chord
            // through to the game, where the same keys mean something else entirely.
            return true;
        }

        // The command the game puts on a right click here. Claimed either way - the key means
        // something else entirely to the game - and SILENT where the control has no such command: the
        // gesture keys are pressed speculatively all over a page, and a cue on every one of them is
        // noise rather than reassurance.
        private bool Contextual()
        {
            if (_graph.Contextual())
            {
                SpeakStateAfterChange();
            }

            return true;
        }

        // The two selection chords, which are the game's own modified clicks: one item in or out of
        // the selection, and everything from the last one to this one. A control that is not part of
        // a selection answers with silence rather than falling back to plain activation, which would
        // do something the player did not ask for.
        private bool SelectChord(bool range)
        {
            if (range ? _graph.SelectRange() : _graph.SelectToggle())
            {
                SpeakStateAfterChange();
            }

            return true;
        }

        /// <summary>
        /// Pick something up, put it down, or swap what is being held - the whole decision is
        /// <see cref="CarryActions.Press"/>'s, so that it can be read (and tested) in one place. False
        /// means the key was never ours here and the game should have it, which is the same answer
        /// <see cref="TakesCarryKey"/> gave the game's own scan before the press.
        /// </summary>
        private bool CarryKey()
        {
            GraphNode node = _graph.CurrentNode;
            if (!CarryActions.Claims(node == null ? null : node.Vtable, _carry))
            {
                // Not ours here, and answered off the standing render rather than by building one:
                // Space is pressed on screens that have nothing to do with carrying.
                return false;
            }

            if (!_graph.Rerender())
            {
                return false;
            }

            node = _graph.CurrentNode;
            CarryOutcome outcome = CarryActions.Press(
                node == null ? null : node.Vtable,
                _carry,
                _screen
            );
            if (!outcome.Handled)
            {
                return false;
            }

            Voice.Say(outcome.Speech, true);
            return true;
        }

        // The back key while something is held: put it down, and go no further - the screen the
        // player was carrying across is not the thing they were trying to leave.
        private bool CancelCarry()
        {
            CarryOutcome outcome = CarryActions.Cancel(_carry);
            if (!outcome.Handled)
            {
                return false;
            }

            Voice.Say(outcome.Speech, true);
            return true;
        }

        /// <summary>
        /// Whether the carry key belongs to the mod where the cursor is standing - asked by the
        /// game's own key scans BEFORE the press, like the back key and the typed letters, because
        /// both sides poll and the game's scan can run either side of ours.
        ///
        /// Space is the game's own key everywhere else, so this is deliberately narrow: a search
        /// already collecting text (the space is a character), a control with something to pick up,
        /// or something already being carried. Reads the last render rather than building one - it is
        /// asked several times a frame.
        /// </summary>
        public bool TakesCarryKey()
        {
            if (_screen == null || _graph == null)
            {
                return false;
            }

            if (_typeAhead.HasBuffer)
            {
                return true;
            }

            GraphNode node = _graph.CurrentNode;
            return CarryActions.Claims(node == null ? null : node.Vtable, _carry);
        }

        // Whether two screens are the same page: the same screen, or one opened over the other (an
        // action menu is a child screen of the page it was opened from).
        private static bool SameFamily(Screen a, Screen b)
        {
            if (a == null || b == null)
            {
                return false;
            }

            return ReferenceEquals(a, b) || Descends(a, b) || Descends(b, a);
        }

        private static bool Descends(Screen child, Screen ancestor)
        {
            Screen at = child.ParentScreen;
            // Bounded rather than "while", like Screen.Deepest: a chain is a handful deep, and a
            // cycle introduced by a bug should not hang the frame.
            for (int depth = 0; depth < 16 && at != null; depth++)
            {
                if (ReferenceEquals(at, ancestor))
                {
                    return true;
                }

                at = at.ParentScreen;
            }

            return false;
        }

        // The one adjust path, fine or coarse: left and right take the small step, the same arrows
        // with Shift the large one, and both report the new value the same way. A control with no value to
        // adjust does not answer for either, so the coarse keys fall through to the game and the
        // arrows go back to being navigation.
        private bool Adjust(int sign, bool large)
        {
            GraphNode node = _graph.CurrentNode;
            if (node == null || node.Vtable.OnAdjust == null)
            {
                return false;
            }

            _graph.TryAdjust(sign, large);
            SpeakStateAfterChange();
            return true;
        }

        // The synchronous half of state feedback: an action the player just took reports its result
        // at once, interrupting, so holding a key down reads every step instead of falling behind.
        // The control is re-read after the action, since acting on it re-rendered the graph. A
        // control that answers with nothing - one that refused the action - is left alone entirely,
        // live watch included: nothing changed, so there is nothing to re-baseline.
        private void SpeakStateAfterChange()
        {
            GraphNode node = _graph.CurrentNode;
            Func<string> state = node == null ? null : node.Vtable.StateText;
            if (state == null)
            {
                return;
            }

            string text = state();
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            Voice.Say(text, true);

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
        ///
        /// Nor while the screen says it cannot be worked (<see cref="Screen.IsWorkable"/>): a page being
        /// switched off wholesale turns every control on it unavailable at once, and the control the
        /// player just pressed saying "unavailable" is a fact about the page, not about the control. The
        /// baseline is still taken, so nothing is announced late once the page comes back.
        /// </summary>
        private void WatchLive(GraphNode node)
        {
            List<NodeAnnouncement> parts = GraphAnnouncer.EffectiveAnnouncements(node);
            if (parts.Count == 0)
            {
                return;
            }

            bool mute = !Workable();

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
                    if (!mute)
                    {
                        Voice.Say(text, false);
                    }
                }
            }
        }

        private bool Workable()
        {
            try
            {
                return _screen == null || _screen.IsWorkable;
            }
            catch (Exception e)
            {
                Log.Warn("nav: IsWorkable threw: " + e);
                return true;
            }
        }

        // ---- type-ahead search ----
        //
        // Typing a letter on any screen of ours searches what is on it and moves focus to the best
        // match; more letters narrow it, Up/Down step the matches, Home/End go to the ends, and
        // Escape puts the keyboard back. There is no key that starts a search, because a key nobody
        // is told about is a key nobody uses - and because on a screen of forty controls, hunting
        // with the arrows is the thing that makes a game unplayable rather than merely slow.
        //
        // The characters do not come through the mod's bindings: a binding is one key meaning one
        // action, and this is text. They come from TypedCharacters, which is the keyboard in
        // production and the dev server in a test - the same path either way, gates included.

        private readonly TypeAhead _typeAhead = new TypeAhead();

        // Characters asked for over the dev server, taken by the next tick ahead of the keyboard.
        private readonly StringBuilder _typedQueue = new StringBuilder();

        // The tabular column focus was on when the search began: a result lands on the matched ROW
        // at that column, so searching never pulls the player out of the column they were reading.
        private int _searchColumn;

        /// <summary>
        /// Where typed characters come from this frame - the keyboard, in production. Null means
        /// nothing was typed.
        ///
        /// A hook rather than a call to UnityEngine.Input, so that a test can drive a search: HTTP
        /// cannot press a key, and the injection queue the dev server has is for ACTIONS, which
        /// typing is not.
        /// </summary>
        public Func<string> TypedCharacters;

        /// <summary>Whether the mod's keys mean anything at all this frame - the input layer's own
        /// verdict (a game text field holding the keyboard is what says no). Wired by ModEntry;
        /// null means "assume they do", which is what the unit tests want.</summary>
        public Func<bool> KeyboardIsOurs;

        /// <summary>Whether a search is collecting the keyboard right now. Escape belongs to it
        /// while it is - the game must stand down from a key that means "put the keyboard back".
        /// </summary>
        public bool SearchIsActive
        {
            get { return _typeAhead.IsActive; }
        }

        /// <summary>What has been typed into the current search - for the dev server, which cannot
        /// see the keyboard.</summary>
        public string SearchText
        {
            get { return _typeAhead.Buffer; }
        }

        /// <summary>How many controls the current search matched.</summary>
        public int SearchResultCount
        {
            get { return _typeAhead.ResultCount; }
        }

        /// <summary>
        /// Whether <paramref name="key"/> is one the focused screen is taking as TYPED TEXT rather
        /// than leaving to the game, which has letter hotkeys of its own.
        ///
        /// Asked by the game's key scans, before the press, for the same reason the back key is
        /// (see <see cref="Screen.ConsumesBack"/>): both sides poll, and the game's scan can run
        /// either side of our frame. Unlike the back key this needs no release latch - what it
        /// answers depends on the screen being focused and taking text, and neither of those is
        /// something typing a letter can change.
        /// </summary>
        public bool TakesTypedKey(UnityEngine.KeyCode key)
        {
            if (!TypeAheadArmed())
            {
                return false;
            }

            if (key >= UnityEngine.KeyCode.A && key <= UnityEngine.KeyCode.Z)
            {
                return true;
            }

            // Space only continues a search; on its own it is the game's, and a screen reader user
            // pressing it expects whatever the game does with it.
            return key == UnityEngine.KeyCode.Space && _typeAhead.HasBuffer;
        }

        /// <summary>Ask for <paramref name="text"/> to be typed - what the dev server's /type route
        /// does. Taken by the next <see cref="TypeAheadTick"/>, through the same gates a keypress
        /// passes.</summary>
        public void TypeText(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                _typedQueue.Append(text);
            }
        }

        /// <summary>
        /// The typing half of the frame: take what was typed and search with it. Called from the
        /// pump right after the key actions, so a letter and the control it lands on are the same
        /// frame's work.
        ///
        /// True when a character actually went into a search - what the dev route reports, and the
        /// difference between "the screen does not search" and "nothing matched".
        /// </summary>
        public bool TypeAheadTick()
        {
            if (!TypeAheadArmed())
            {
                // Not ours to hear: a screen that opted out, or the game holding the keyboard for
                // something the player is typing into. A search left open across that would step
                // them around a screen they had stopped looking at.
                ClearSearch();
                return false;
            }

            string typed = NextTyped();
            if (string.IsNullOrEmpty(typed))
            {
                return false;
            }

            if (_typeAhead.Strayed(FocusedKey))
            {
                // Something else moved focus; these letters start a fresh search from where the
                // player actually is.
                ClearSearch();
            }

            if (!_graph.Rerender())
            {
                return false;
            }

            bool taken = false;
            for (int i = 0; i < typed.Length; i++)
            {
                char c = typed[i];
                if (!char.IsLetter(c) && !(c == ' ' && _typeAhead.HasBuffer))
                {
                    continue;
                }

                GraphNode focused = _graph.CurrentNode;
                if (focused == null)
                {
                    break;
                }

                if (!_typeAhead.HasBuffer)
                {
                    _searchColumn = focused.Vtable.Column;
                }

                taken |= _typeAhead.Type(c, ScopeFor(focused));
            }

            return taken;
        }

        /// <summary>Give up the current search. Announced only when the player asked for it - the
        /// silent case is a search that stopped applying to where they are.</summary>
        public void ClearSearch(bool announce = false)
        {
            // Asked every frame by the tick, so the usual answer - there was no search - costs a
            // pair of flag reads.
            if (!_typeAhead.IsActive && !_typeAhead.HasBuffer)
            {
                return;
            }

            _typeAhead.Clear();
            _searchColumn = 0;
            if (announce)
            {
                Voice.Say(ModStrings.Get(ModStrings.SearchCleared), true);
            }
        }

        // While a search is up, the keys that walk its results belong to it. Everything else ends
        // the search and then does what it always does - so the player never has to think about
        // being "in" a mode they cannot see. True = the action was the search's.
        private bool SearchAction(string actionKey)
        {
            if (_typeAhead.Strayed(FocusedKey))
            {
                ClearSearch();
                return false;
            }

            switch (actionKey)
            {
                case UiActions.Up:
                    _typeAhead.Step(-1);
                    return true;
                case UiActions.Down:
                    _typeAhead.Step(1);
                    return true;
                case UiActions.Home:
                    _typeAhead.First();
                    return true;
                case UiActions.End:
                    _typeAhead.Last();
                    return true;
                case UiActions.Back:
                    // The key that puts the keyboard back, and it goes no further: the game must
                    // not also close the screen the player was searching.
                    ClearSearch(true);
                    return true;
                default:
                    ClearSearch();
                    return false;
            }
        }

        private bool TypeAheadArmed()
        {
            if (_screen == null || _graph == null)
            {
                return false;
            }

            if (!_screen.AllowsTypeahead || _screen.CapturesRawInput)
            {
                return false;
            }

            Func<bool> ours = KeyboardIsOurs;
            return ours == null || ours();
        }

        // The dev server's characters first - it queued them for exactly this - then the keyboard.
        private string NextTyped()
        {
            if (_typedQueue.Length > 0)
            {
                string queued = _typedQueue.ToString();
                _typedQueue.Length = 0;
                return queued;
            }

            Func<string> source = TypedCharacters;
            return source == null ? null : source();
        }

        // What this search looks through: whatever the screen offers, else the Tab-stop the cursor
        // is in. A screen answers only when the thing being searched for is not declared - a tree
        // whose collapsed branches hold most of it.
        private SearchScope ScopeFor(GraphNode focused)
        {
            SearchScope declared = null;
            try
            {
                declared = _screen.TypeAheadScope(focused, _graph.Current);
            }
            catch (Exception e)
            {
                Log.Warn("nav: " + _screen.Key + ".TypeAheadScope threw: " + e);
            }

            return declared ?? SearchScope.OverStop(_graph.Current, focused.StopKey);
        }

        // A result landing: focus it, keep the column the search started in, and read it out at
        // once (interrupting, like any other move the player asked for). Answers with where focus
        // ended up, which is what the search watches to know it is still current.
        private ControlId LandOnSearchResult(ControlId id)
        {
            if (id == null || !_graph.Focus(id))
            {
                return null;
            }

            FollowSearchColumn();
            GraphNode node = _graph.CurrentNode;
            if (node == null)
            {
                return null;
            }

            Voice.Say(GraphAnnouncer.Compose(_lastSpokenNode, node), true);
            _lastSpokenKey = node.Id;
            _lastSpokenNode = node;
            return node.Id;
        }

        // A search over a table matches rows and lands on their primary cell; the player was
        // reading a column, so step sideways back into it. Bounded rather than "while": a table
        // whose edges are wired in a circle must not take the frame with it.
        private void FollowSearchColumn()
        {
            for (int step = 0; step < 64 && _searchColumn > 0; step++)
            {
                GraphNode node = _graph.CurrentNode;
                if (node == null || node.Vtable.Column >= _searchColumn)
                {
                    return;
                }

                if (!_graph.Move(GraphDir.Right).Moved)
                {
                    return;
                }
            }
        }

        private static void SayNoMatch(string text)
        {
            Voice.Say(ModStrings.Format(ModStrings.SearchNoMatch, text), true);
        }
    }
}
