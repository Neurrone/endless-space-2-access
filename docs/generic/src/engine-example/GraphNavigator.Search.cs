using System;
using System.Collections.Generic;
using System.Text;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI.Input;

namespace ES2Access.UI
{
    public sealed partial class GraphNavigator
    {
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

            if (_screen.SuspendsTypeahead)
            {
                // Heard and dropped. The letters stay CLAIMED - TakesTypedKey is deliberately not
                // asking this, so the game's own letter hotkeys still never see them - and they turn
                // into nothing here. Drained rather than left queued: characters typed at the mode
                // are not a search the player deferred, and firing them the moment the mode ended
                // would search with letters nobody meant for the page underneath.
                NextTyped();
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
                GraphBuilder builder = new GraphBuilder(_state.Expanded, NodeGate.For(_screen.Key));
                builder.ExpandAll = true;
                _screen.Render(builder);
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

            CancelPendingFocus();

            // The visual is committed before the result is read out, and for the reason EnsureFocus
            // commits it first: committing it is what asks the game to SHOW where the cursor now is,
            // and a result inside a system the camera has not reached yet reads as the far view's
            // version of its row - a world with a curiosity reads as a leaf, because the child the
            // curiosity becomes is declared off a card the map has not drawn. Where that leaves the
            // page between views the reading is left to EnsureFocus, which makes it once, from the
            // settled build (<see cref="Screen.BetweenViews"/>).
            SyncVisual(node);
            if (_screen != null && _screen.BetweenViews)
            {
                return node.Id;
            }

            Voice.Say(GraphAnnouncer.Compose(_lastSpokenNode, node), true);
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
