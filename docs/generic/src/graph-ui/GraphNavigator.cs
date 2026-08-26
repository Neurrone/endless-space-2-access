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

        // A requested landing, applied on the next EnsureFocus - and kept across the frames a branch
        // takes to open where the control asked for is inside a collapsed one (see FocusRequest).
        private FocusRequest _pendingFocus;
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

        // A MODE's reading standing in for the focused control's, while the mode owns the screen -
        // see OverrideBuffer.
        private List<string> _bufferOverride;

        // Which control the game is currently being made to look hovered on, and the node whose
        // hooks will undo it. Kept by id, not by object: the graph is rebuilt every frame, so the
        // node standing for a control is a different instance each time.
        private ControlId _visualKey;
        private GraphNode _visualNode;

        // What that commit aimed at (NodeVtable.PointsAt's answer at the time). The graph is rebuilt
        // every frame and a node's aim is resolved when ASKED, so this is what a later frame's answer
        // is compared against - see SyncVisual.
        private object _visualAim;

        // Where the cursor stood at the last visual commit, and whether the commit being made now is
        // the cursor having MOVED. Unlike _visualKey this survives ClearVisual, which is a re-commit
        // on the control the cursor is already on.
        private ControlId _visualFrom;
        private bool _cursorMovedHere;

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

        /// <summary>The render the cursor is standing in - the last one built, which every dispatch and
        /// every frame's <see cref="EnsureFocus"/> refreshes. For a caller that wants to read what a
        /// control OTHER than the focused one is saying (a global key speaking a button's refusal from
        /// the far side of the page); null before the first build.</summary>
        public GraphRender Render
        {
            get { return _graph == null ? null : _graph.Current; }
        }

        /// <summary>How many nodes the focused screen declared on the last rebuild, or -1 when there
        /// is no render. For a trace of a transition: a page that has stopped declaring its own
        /// content while something else still declares its shared strip is the whole of what a
        /// cursor jump in the gap looks like.</summary>
        public int RenderedNodeCount
        {
            get
            {
                return _graph == null || _graph.Current == null ? -1 : _graph.Current.Nodes.Count;
            }
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
            screen.BuildShared(builder);
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
            _bufferOverride = null;
            // The outstanding landing is deliberately NOT dropped here. It belongs to the screen that
            // asked for it, and that screen losing the keyboard - to a cutscene, a modal, the game's own
            // view change - is not the player deciding they no longer want it. It is held, worked on
            // again the moment its own screen is back (OwnPendingFocus), and cancelled only by that
            // screen's own navigation.
            _pendingStop = null;
            // Nowhere to have moved FROM: the first commit on a page is a cursor being seated, not a
            // player going somewhere (see CursorMovedHere).
            _visualFrom = null;
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

        /// <summary>Forget a closed screen's cursor, so re-opening it starts at the top - and with it any
        /// landing that screen was still waiting to make, which is aimed at ids in the very graph state
        /// being thrown away.</summary>
        public void ScreenClosed(Screen screen)
        {
            if (screen == null)
            {
                return;
            }

            _states.Remove(screen);
            if (_pendingFocus != null && ReferenceEquals(_pendingFocus.Owner, screen))
            {
                _pendingFocus = null;
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
            _bufferOverride = null;
            // Giving up the cursor is not moving it: whatever it is seated on next is a landing.
            _visualFrom = null;
            ClearVisual();
        }

        /// <summary>
        /// Treat whatever the cursor is standing on next as NEWS, even where it is what it was
        /// standing on before.
        ///
        /// For a MODE handing the player back to the tree. Focus never moved while the mode was up, so
        /// a landing on the very stop the mode was opened from is silent by the ordinary rule ("say it
        /// only when the cursor moved") - and the player, who has been somewhere else entirely, hears
        /// nothing at all and cannot tell the mode has ended (owner-reported, the galaxy's inspect
        /// cell).
        /// </summary>
        public void AnnounceNextLanding()
        {
            _lastSpokenKey = null;
            _lastSpokenNode = null;
        }

        /// <summary>
        /// GO TO WHERE THE FOCUSED THING HAPPENED - the game's own show-location, replayed from the
        /// keyboard.
        ///
        /// The SCREEN is offered it first (<see cref="ES2Access.Screens.Screen.GoToLocation"/>), for a
        /// surface where the affordance belongs to the page - an open notification popup draws the
        /// button in its own bottom bar and the key means it from anywhere on the popup - and the
        /// focused control answers otherwise (a notification strip row, a turn-log row). Where neither
        /// offers one the press did nothing and says nothing.
        /// </summary>
        public bool GoToLocation()
        {
            if (_screen == null || _graph == null)
            {
                return false;
            }

            return _screen.GoToLocation() || _graph.GoToLocation();
        }

        /// <summary>The same fact asked before the press, for the key's claim: does anything on this
        /// page answer the key at all. Read off the standing render, like every other claim.</summary>
        public bool TakesGoToLocation()
        {
            if (_screen == null || _graph == null)
            {
                return false;
            }

            return _screen.OffersGoToLocation || _graph.OffersGoTo;
        }

        /// <summary>EMPTY THE FOCUSED CONTROL - only the controls that wire one answer, and the press
        /// says nothing anywhere else. The control itself speaks the result: what it holds now is a
        /// live part of its own readout.</summary>
        public bool ClearControl()
        {
            return _graph != null && _graph.Clear();
        }

        /// <summary>The same fact asked before the press, for the key's claim. Off the standing render,
        /// like every other claim - the key is Delete, and everywhere the cursor is not on a control
        /// that empties it stays the game's.</summary>
        public bool TakesClearKey()
        {
            return _graph != null && _graph.OffersClear;
        }

        /// <summary>Whether the focused screen's LAST render declared this Tab-stop - the availability
        /// half of a jump-to-stop key, and the same question its key CLAIM asks
        /// (<c>docs/interaction.md</c>). Read off the standing render rather than built fresh: the claim
        /// is asked from inside the game's key scans several times a frame, and the render is rebuilt by
        /// every dispatch and every <see cref="EnsureFocus"/> anyway, so it is never more than a frame
        /// old.</summary>
        public bool DeclaresStop(object stopKey)
        {
            return _screen != null && KeyGraph.DeclaresStop(Render, stopKey);
        }

        /// <summary>Put the cursor on a Tab-stop NOW and announce the landing - a global key that means
        /// "take me to that panel". Where the stop is absent the key did nothing and says so by saying
        /// nothing: the answer is false and the caller leaves the press alone.
        ///
        /// It lands where TAB would land (<see cref="KeyGraph.StopLanding(GraphRender,GraphState,object)"/>
        /// - the remembered position, else the selected member, else the first control), so the key and
        /// Tab agree about where a panel begins.</summary>
        public bool FocusStop(object stopKey)
        {
            if (_screen == null || _graph == null || !_graph.Rerender())
            {
                return false;
            }

            GraphNode landing = KeyGraph.StopLanding(_graph.Current, _graph.State, stopKey);
            GraphNode from = _graph.CurrentNode;
            if (landing == null || !_graph.Focus(landing.Id))
            {
                return false;
            }

            AnnounceMove(
                new MoveResult { From = from, To = _graph.CurrentNode, Moved = true }
            );
            return true;
        }

        /// <summary>
        /// Ask for focus to land on a control (a screen choosing where to put the player). Applied on
        /// the next tick.
        ///
        /// The control does not have to be in the render: a landing aimed inside a COLLAPSED branch
        /// opens that branch on the way, one level per build, for as deep as the branches go - and
        /// waits out the frames the game takes to draw what the branch reads from. A control the
        /// render leads nowhere near is dropped at once, and a branch that never produces the control
        /// gives up on its own (<see cref="FocusRequest"/>), so nothing is left armed over the
        /// player's own navigation.
        /// </summary>
        public void FocusNode(ControlId id, bool announce = true)
        {
            _pendingFocus = id == null ? null : new FocusRequest(id, announce, _screen);
        }

        /// <summary>The outstanding landing, but only on a frame the screen that asked for it is the one
        /// focused - null everywhere else. A request is aimed at an id in ITS OWN screen's graph, so
        /// every question about it (is it reachable, has the budget run out, has the player moved off it)
        /// can only be asked of that screen's render; asked of anybody else's the answers are noise.
        /// </summary>
        private FocusRequest OwnPendingFocus
        {
            get
            {
                return _pendingFocus != null && ReferenceEquals(_pendingFocus.Owner, _screen)
                    ? _pendingFocus
                    : null;
            }
        }

        /// <summary>Drop the outstanding landing whoever it belongs to - for the mod going away, which
        /// is the one caller that is not a screen deciding about its own request.</summary>
        public void ForgetPendingLanding()
        {
            _pendingFocus = null;
        }

        /// <summary>
        /// The STOP an outstanding landing is aimed at, or null while none is outstanding.
        ///
        /// For a caller deciding whether something it is about to say will be said again by a landing
        /// already in flight - a panel that names itself after live game text has two speakers, and a
        /// landing that enters it reads that name (the galaxy's map stop under a targeting mode,
        /// <c>GalaxyHudScreen.MapContext</c>). Asked of the STOP rather than of the id, so no caller
        /// has to know which ids a panel declares.
        ///
        /// A landing is routinely aimed INSIDE a collapsed branch and stays outstanding for the frames
        /// that branch takes to open (<see cref="FocusRequest"/>), so the node itself usually does not
        /// exist in the render yet. The stop is then read off the deepest ancestor that does exist -
        /// the same walk the landing's own <c>Reach</c> makes, by the id's own <c>/</c>-separated path
        /// (<see cref="KeyGraph.AncestorKeys"/>) - and is null where not even an ancestor is declared,
        /// which is the honest answer: that landing may never happen.
        /// </summary>
        public object PendingStopKey
        {
            get
            {
                FocusRequest request = OwnPendingFocus;
                ControlId id = request == null ? null : request.Id;
                GraphRender render = _graph == null ? null : _graph.Current;
                if (id == null || render == null)
                {
                    return null;
                }

                GraphNode node = render.NodeAt(id);
                if (node != null)
                {
                    return node.StopKey;
                }

                IList<object> keys = KeyGraph.AncestorKeys(id.StructuralKey);
                for (int i = 0; i < keys.Count; i++)
                {
                    GraphNode ancestor = render.NodeAt(ControlId.Structural(keys[i]));
                    if (ancestor != null)
                    {
                        return ancestor.StopKey;
                    }
                }

                return null;
            }
        }

        /// <summary>Re-read the focused control in full, ancestors included.
        /// <paramref name="interrupt"/> false QUEUES it instead, for a caller that has just said
        /// something of its own about the same control and would otherwise cut its own line off (the
        /// shared text editor's "edited", followed by the field's new value).</summary>
        public void AnnounceCurrent(bool interrupt = true)
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

            Voice.Say(GraphAnnouncer.ComposeFull(node), interrupt);
            _lastSpokenKey = node.Id;
            _lastSpokenNode = node;

            // Everything live has just been read out; re-baseline so the live watch does not say any
            // of it a second time. A caller asks for this while the control is CHANGING under it -
            // the rename box's field, whose value is withheld while the game holds the keyboard and
            // reappears the moment it lets go - which is exactly when the watch would.
            _liveKey = null;
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
                // The page keys are the SCREEN's, wherever the cursor is standing on it: what they turn
                // is the whole surface (the next system, the next planet, the next notification), which
                // is a fact about the page and not about the control under the cursor. A screen that
                // draws no such pair never overrides them and the press does nothing at all.
                case UiActions.PagePrev:
                    return _screen.PagePrev();
                case UiActions.PageNext:
                    return _screen.PageNext();
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
                case UiActions.DoubleClick:
                    return DoubleClick();
                case UiActions.GoToLocation:
                    return GoToLocation();
                case UiActions.Clear:
                    return ClearControl();
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

            // A landing another screen is holding is none of this screen's business: the page still
            // needs seating, and asking the question of OwnPendingFocus is what keeps a suspended
            // request from standing in for one.
            FocusRequest pending = OwnPendingFocus;
            if (_state.CurKey == null && pending == null)
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

                if (pending != null)
                {
                    FocusOutcome outcome = PendingOutcome(pending);
                    if (outcome == FocusOutcome.Land)
                    {
                        _graph.Focus(pending.Id);
                        if (!pending.Announce)
                        {
                            _lastSpokenKey = pending.Id;
                            _lastSpokenNode = _graph.CurrentNode;
                        }
                    }

                    if (outcome != FocusOutcome.Wait)
                    {
                        _pendingFocus = null;
                    }
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
        /// What to do with the outstanding landing this frame: land on it, keep waiting for the branch
        /// it is in to open, or give up (<see cref="FocusRequest"/>).
        ///
        /// With NO cursor on the page at all the request gets its old single attempt instead. An
        /// unseated cursor is a page still waiting to be seated, and waiting for a branch would leave
        /// the player on a screen with no focus and nothing said until the budget ran out - seating
        /// beats a landing that can be re-asked for. Not while the page says it is SUSPENDED, though:
        /// an unseated cursor is what every page looks like mid-transition, and the single attempt
        /// there is a coin toss the landing loses.
        /// </summary>
        private FocusOutcome PendingOutcome(FocusRequest pending)
        {
            bool suspended = _screen.LandingSuspended;
            if (_state.CurKey == null)
            {
                if (_graph.Current.Nodes.ContainsKey(pending.Id))
                {
                    return FocusOutcome.Land;
                }

                return suspended ? FocusOutcome.Wait : FocusOutcome.Drop;
            }

            return pending.Step(_graph.Reach(pending.Id), suspended);
        }

        /// <summary>
        /// Give up an outstanding landing because the player has moved the cursor themselves. A landing
        /// waits out the frames a branch takes to open, and over that window the player is still
        /// navigating: a request that survived one would yank them off wherever they had got to, for a
        /// reason they could not connect to anything they did.
        ///
        /// Scoped to the screen the player is ON, both ways. The navigator's own request is dropped only
        /// where the focused screen is the one that asked for it, so a cutscene the player reads or
        /// dismisses over another page cannot kill that page's landing; and the focused screen is told,
        /// so a landing it is still holding privately - a seat waiting for the game to draw its target -
        /// dies on the same keystroke.
        /// </summary>
        private void CancelPendingFocus()
        {
            if (OwnPendingFocus != null)
            {
                _pendingFocus = null;
            }

            if (_screen != null)
            {
                _screen.CancelLandings();
            }
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
        ///
        /// And re-committed, on the SAME control, whenever what the control aims at has changed
        /// (<see cref="NodeVtable.PointsAt"/>). A commit happens once per focus change, but the thing
        /// a node points at is a question the game keeps answering differently under a standing
        /// cursor: pooled widgets get handed to another row, one tooltip on a window gets re-bound to
        /// whatever the camera is looking at. The pointer stayed where it was first put, so the game
        /// went on drawing somebody else's dossier for the control the player was standing on - and
        /// nothing was ever going to correct it. Comparing the answer against the one that was
        /// committed is what turns that into a re-commit, per site, with nothing for a screen to
        /// remember; a node whose answer is stable takes exactly the path it always did.
        /// </summary>
        private void SyncVisual(GraphNode node)
        {
            if (_visualKey != null && _visualKey.Equals(node.Id))
            {
                if (ReferenceEquals(Aim(node), _visualAim))
                {
                    return;
                }
            }

            ClearVisual();
            _visualKey = node.Id;
            _visualNode = node;
            _visualAim = Aim(node);
            _cursorMovedHere = _visualFrom != null && !_visualFrom.Equals(node.Id);
            _visualFrom = node.Id;
            ScrollIntoView.Reveal(node.Vtable.ScrollAnchor, node.Id.Reference);
            // The screen's own half first, so a rule that moves the WORLD (the galaxy page's camera)
            // has run before the node aims the pointer at whatever the new distance draws.
            if (_screen != null)
            {
                try
                {
                    _screen.OnFocusVisual(node);
                }
                catch (Exception e)
                {
                    Log.Warn("navigator: a screen's OnFocusVisual threw: " + e);
                }
            }

            Safe(node.Vtable.OnFocusVisual, "OnFocusVisual");
            _cursorMovedHere = false;
        }

        /// <summary>
        /// Whether the commit now running is the cursor having MOVED here - asked from inside an
        /// <c>OnFocusVisual</c> hook, and false anywhere else.
        ///
        /// A focus visual is committed for three different reasons and only one of them is the player
        /// going somewhere: the cursor moved, the screen was re-attached and the cursor it remembered
        /// re-seated, or the visual was dropped and re-taken on the SAME control because what the game
        /// draws for it changed (<c>GalaxyHudScreen.FollowCamera</c>). A hook that only points the
        /// game's pointer wants all three. A hook that MOVES THE WORLD - a camera pan to whatever the
        /// cursor is on - wants only the first, or re-entering a page flies the camera back to the
        /// system the player was reading before, over wherever the game has since taken it.
        /// </summary>
        public bool CursorMovedHere
        {
            get { return _cursorMovedHere; }
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
            _visualAim = null;
        }

        /// <summary>What a node aims at right now, or null where it aims at nothing and where asking
        /// threw - an aim that cannot be resolved is not a reason to keep re-committing.</summary>
        private static object Aim(GraphNode node)
        {
            try
            {
                Func<object> points = node.Vtable.PointsAt;
                return points == null ? null : points();
            }
            catch (Exception)
            {
                return null;
            }
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
        /// <summary>
        /// Put a MODE's own reading in the review buffer instead of the focused control's, and keep it
        /// there until the mode gives it back.
        ///
        /// A mode of one widget (the galaxy's inspect cell) moves the player about something the tree
        /// cursor is not standing on: focus never leaves the map, so the buffer would go on offering
        /// the map's own stop for as long as the mode was up, and the one thing the player most wants
        /// to re-read - what is in the cell they have just moved to - would be the one thing they
        /// could not (owner-reported).
        ///
        /// Called on every reading rather than once, because the reading is what changed; the lines are
        /// compared before they are pushed, so standing still keeps the player's place in the buffer
        /// exactly as an unchanged control does.
        /// </summary>
        public void OverrideBuffer(List<string> lines)
        {
            if (_buffers == null || lines == null)
            {
                return;
            }

            _bufferOverride = lines;
            // The focused control's fill is invalidated as well as suspended: whatever it left in the
            // buffer is now stale, and the frame the mode ENDS on has to refill from scratch rather
            // than decide nothing changed.
            _bufferKey = null;
            _bufferReadout = null;
            if (Same(_bufferLines, lines))
            {
                return;
            }

            _bufferLines = lines;
            _buffers.ReplaceUiLines(lines);
        }

        /// <summary>Give the review buffer back to the focused control - the mode has ended, and the
        /// next frame refills from wherever the cursor has been left.</summary>
        public void ReleaseBuffer()
        {
            if (_bufferOverride == null)
            {
                return;
            }

            _bufferOverride = null;
            _bufferKey = null;
            _bufferReadout = null;
            _bufferLines = null;
        }

        private void FillBuffer(GraphNode node)
        {
            if (_buffers == null || _bufferOverride != null)
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
                screen.BuildShared(builder);
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
                    case KeyGraph.TreeMove.Collapsed:
                        SpeakFocusedState();
                        return true;
                    case KeyGraph.TreeMove.EmptyGroup:
                        // The branch is OPEN and holds nothing. The cursor stays on the header, which is
                        // the one place left to press Left from to shut it again.
                        Voice.Say(ModStrings.Get(ModStrings.NavNoDetails), true);
                        return true;
                    case KeyGraph.TreeMove.Descended:
                    case KeyGraph.TreeMove.Ascended:
                        AnnounceMove(tree.Move);
                        return true;
                    case KeyGraph.TreeMove.Followed:
                        // The leaf named somewhere else and sent the cursor there itself. Nothing is
                        // said here: the landing is announced once, by the pending-focus path every
                        // other jump goes through, and a word from the key as well would be that
                        // landing described twice.
                        return true;
                    case KeyGraph.TreeMove.Leaf:
                        return true;
                }
            }

            // Nothing that way. Inside a tree the key is still ours (there is nowhere to bubble to
            // that would make sense); on a plain list it falls through.
            return KeyGraph.InTree(focused);
        }

        /// <summary>
        /// Tab and Shift+Tab, which WRAP: the last stop's Tab lands on the first and the first stop's
        /// Shift+Tab on the last (owner decision 2026-08-12). A player who cannot see the panels has no
        /// way to know a page has run out of them, so stopping dead at an end reads as a broken key -
        /// and coming round is how every other stop-cycling reader behaves.
        ///
        /// A page with exactly ONE stop is where wrapping would be a lie: coming round to the panel the
        /// player is already on is not a move, so the key is consumed and says nothing rather than
        /// re-reading the same control (<see cref="KeyGraph.MoveStop"/> answers not-moved there).
        /// </summary>
        private bool Stop(int step)
        {
            MoveResult move = _graph.MoveStop(step, true);
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

            // The SCREEN is offered the key first, the same way the contextual key is offered
            // (<see cref="Screen.Secondary"/>): where a page has folded a second command onto this key
            // for a whole panel of its own - the galaxy's way back down the lanes it has been travelled -
            // that command belongs to the panel rather than to whichever node the cursor happens to be
            // on, most of which wire no OnSecondary at all. A screen that declines leaves the focused
            // control's own second command exactly as it was.
            if (_screen.Secondary(node))
            {
                return true;
            }

            if (node.Vtable.OnSecondary != null)
            {
                _graph.Secondary();
                SpeakStateAfterChange();
            }

            return true;
        }

        // The game's own Alt+click: the control's OTHER activation where it wires one, and otherwise its
        // plain click replayed while the player is still holding Alt, which is what lets the GAME's
        // handler decide whether the modifier means anything here (<see cref="KeyGraph.Alternate"/>).
        private bool Alternate()
        {
            if (_graph.CurrentNode == null)
            {
                return false;
            }

            if (_graph.Alternate())
            {
                SpeakStateAfterChange();
            }

            // Claimed either way: a control that has neither must not let the chord through to the
            // game, where the same keys mean something else entirely.
            return true;
        }

        // The command the game puts on a right click here. Claimed either way - the key means
        // something else entirely to the game - and SILENT where the control has no such command: the
        // gesture keys are pressed speculatively all over a page, and a cue on every one of them is
        // noise rather than reassurance.
        private bool Contextual()
        {
            // A mode the game has put the page into gets the key first, because it has taken the right
            // click from every control underneath (<see cref="Screen.Contextual"/>). Nothing is re-read
            // after it: the control did not change, and what the mode's end sounds like is the one place
            // that watches it.
            if (_screen.Contextual())
            {
                return true;
            }

            if (_graph.Contextual())
            {
                SpeakStateAfterChange();
            }

            return true;
        }

        // The game's own second click, which several of this game's controls answer with a command of
        // their own. Claimed either way and SILENT where the control has no such command, for the
        // same reason the right click is: the gesture keys are pressed speculatively along a row. It
        // never falls back to the single click - the whole point of the control having a double click
        // is that the two do different things.
        private bool DoubleClick()
        {
            if (_graph.DoubleClick())
            {
                SpeakStateAfterChange();
            }

            return true;
        }

        // The two selection chords, which are the game's own modified clicks: one item in or out of the
        // selection, and everything from the last one to this one. A control that is not part of a
        // selection gets its plain click replayed with the modifier still held, so the modified clicks
        // the GAME understands and the mod never wired work anyway (KeyGraph.SelectToggle); a control
        // with no click either answers with silence rather than borrowing another control's command.
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
            CancelPendingFocus();
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
            CancelPendingFocus();
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

        // What the live search is looking through, built on its first keystroke and kept until it
        // ends: the fully-open build behind it is the most expensive thing a search does.
        private SearchScope _searchScope;

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

            if (OwnPendingFocus == null && _typeAhead.Strayed(FocusedKey))
            {
                // Something else moved focus; these letters start a fresh search from where the
                // player actually is. A landing of OUR OWN still in flight is not that: focus has not
                // reached the result yet, and the search is still the offer the player is stepping.
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
            // Dropped first and unconditionally: a screen that offered nothing to search built a scope
            // all the same, and a kept empty one would never be asked for again.
            _searchScope = null;
            // Forgotten, not closed: where the search LEFT the player is where they are, and shutting
            // the branch under them on the way out would take the cursor's own surroundings away.
            _searchOpened.Clear();

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
            if (OwnPendingFocus == null && _typeAhead.Strayed(FocusedKey))
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
                case UiActions.Secondary:
                    // The two keys that put the keyboard back, and they go no further: the game must
                    // not also close the screen the player was searching, and Backspace must not also
                    // do the page's own second command on whatever the last match landed on. Backspace
                    // is here rather than editing the typed letters because a search is re-typed in a
                    // keystroke and the key is worth more as the way OUT of one - one gesture, the
                    // same as Escape's (owner decision 2026-08-14).
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
        // is in, and either of those PLUS everything the page would declare with its branches open
        // (SearchScope.Extend). Built once and kept for the life of the search: the deep build is a
        // whole second render of the page and nothing it holds can change while the player types.
        private SearchScope ScopeFor(GraphNode focused)
        {
            if (_searchScope != null)
            {
                return _searchScope;
            }

            SearchScope declared = null;
            try
            {
                declared = _screen.TypeAheadScope(focused, _graph.Current);
            }
            catch (Exception e)
            {
                Log.Warn("nav: " + _screen.Key + ".TypeAheadScope threw: " + e);
            }

            SearchScope basis =
                declared ?? SearchScope.OverStop(_graph.Current, focused.StopKey);
            _searchScope = SearchScope.Extend(
                basis,
                _graph.Current,
                DeepRender(),
                focused.StopKey,
                RevealDeep
            );
            return _searchScope;
        }

        /// <summary>The page as it would be with every group open - what a search looks through beyond
        /// what is declared. The screen's own build, so the enumerations are the ones the expansion
        /// itself uses and nothing here has to know what a branch holds.</summary>
        private GraphRender DeepRender()
        {
            try
            {
                int start = Environment.TickCount;
                GraphBuilder builder = new GraphBuilder(_state.Expanded);
                builder.ExpandAll = true;
                _screen.Build(builder);
                _screen.BuildShared(builder);
                GraphRender render = builder.Build();
                SearchBuildMs = Environment.TickCount - start;
                SearchBuildNodes = render == null ? 0 : render.Order.Count;
                return render;
            }
            catch (Exception e)
            {
                Log.Warn("nav: " + _screen.Key + ".Build with everything open threw: " + e);
                return null;
            }
        }

        /// <summary>How long the last search's fully-open build took, and how many controls it found.
        /// Read by the dev probe: this is the one expensive thing a search does and it is paid once per
        /// search, never per keystroke and never per frame.</summary>
        public static int SearchBuildMs;

        public static int SearchBuildNodes;

        /// <summary>Land on a control only the fully-open build declared: open every branch it is
        /// inside, outermost first, and answer with the control itself. A group whose expansion is an
        /// adapter's own business is opened through its handler, exactly as the tree keys open it;
        /// everything else goes into the persistent set. A branch the standing render already has open
        /// is left alone, so nothing is toggled.</summary>
        private ControlId RevealDeep(GraphNode node)
        {
            if (node == null)
            {
                return null;
            }

            List<GraphNode> branches = new List<GraphNode>();
            for (GraphNode at = node.Parent; at != null; at = at.Parent)
            {
                if (at.Expandable && at.Id != null)
                {
                    branches.Add(at);
                }
            }

            // Everything the LAST landing opened and this one is not inside goes shut again (owner
            // ruling 2026-08-23). Walking a search past a near-miss used to leave that branch hanging
            // open behind the cursor, so a search of half a dozen results left half a dozen systems
            // opened up that the player never asked for. Only what the SEARCH opened is closed - a
            // branch the player had open before typing is not the search's to touch - and the branch
            // the search finishes in stays open, because that is where the player has been left.
            CloseOpenedExcept(branches);

            GraphRender standing = _graph == null ? null : _graph.Current;
            for (int i = branches.Count - 1; i >= 0; i--)
            {
                GraphNode branch = branches[i];
                GraphNode open = standing == null ? null : standing.NodeAt(branch.Id);
                if (open != null && open.Expanded)
                {
                    continue;
                }

                if (branch.Vtable.OnExpand != null)
                {
                    branch.Vtable.OnExpand();
                }
                else
                {
                    _state.Expanded.Add(branch.Id);
                }

                _searchOpened.Add(branch);
            }

            return node.Id;
        }

        // The branches THIS search opened, outermost first, in the order they were opened. Emptied
        // when the search ends without closing anything: the last landing's branch is where the
        // player is standing.
        private readonly List<GraphNode> _searchOpened = new List<GraphNode>();

        /// <summary>Shut every branch this search opened that the new landing is not inside, innermost
        /// first - the way a player closing them by hand would. A branch whose expansion is the
        /// screen's own business is closed through its handler, exactly as the tree keys close it.
        /// </summary>
        private void CloseOpenedExcept(List<GraphNode> keep)
        {
            for (int i = _searchOpened.Count - 1; i >= 0; i--)
            {
                GraphNode opened = _searchOpened[i];
                if (Holds(keep, opened.Id))
                {
                    continue;
                }

                _searchOpened.RemoveAt(i);
                try
                {
                    if (opened.Vtable.OnCollapse != null)
                    {
                        opened.Vtable.OnCollapse();
                    }
                    else
                    {
                        _state.Expanded.Remove(opened.Id);
                    }
                }
                catch (Exception e)
                {
                    Log.Warn("nav: closing a branch the search opened threw: " + e);
                }
            }
        }

        private static bool Holds(List<GraphNode> branches, ControlId id)
        {
            for (int i = 0; i < branches.Count; i++)
            {
                if (branches[i].Id != null && branches[i].Id.Equals(id))
                {
                    return true;
                }
            }

            return false;
        }

        // A result landing: focus it, keep the column the search started in, and read it out at
        // once (interrupting, like any other move the player asked for). Answers with where focus
        // ended up, which is what the search watches to know it is still current.
        private ControlId LandOnSearchResult(ControlId id)
        {
            if (id == null)
            {
                return null;
            }

            if (!_graph.Focus(id))
            {
                // Not declared yet - the result was one only the fully-open build knew about, and the
                // branches it is inside have just been asked to open. Opening them takes a build, or
                // several, so the landing goes to the pending-focus pass, which walks the ancestry
                // down and announces the arrival itself.
                FocusNode(id);
                return id;
            }

            FollowSearchColumn();
            GraphNode node = _graph.CurrentNode;
            if (node == null)
            {
                return null;
            }

            Voice.Say(GraphAnnouncer.Compose(_lastSpokenNode, node), true);
            CancelPendingFocus();
            _lastSpokenKey = node.Id;
            _lastSpokenNode = node;
            return node.Id;
        }

        // A search over a table matches rows and lands on their primary cell; the player was
        // reading a column, so step sideways back into it. Bounded rather than "while": a table
        // whose edges are wired in a circle must not take the frame with it.
        //
        // A cell that matched BY ITS OWN words is the thing the player asked for, so the column is not
        // followed off it: a table whose rows have no name (NodeVtable.SearchesAsItself, stamped on
        // every cell of such a row, column 0 included) offers every cell as a result, and stepping
        // right from one would read a different cell than the one that matched. The same is true of a
        // sort-header band, whose headings are no row's cells.
        private void FollowSearchColumn()
        {
            for (int step = 0; step < 64 && _searchColumn > 0; step++)
            {
                GraphNode node = _graph.CurrentNode;
                if (node == null || node.Vtable.SearchesAsItself || node.Vtable.Column >= _searchColumn)
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
