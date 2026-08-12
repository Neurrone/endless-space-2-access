using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;

namespace ES2Access.Screens
{
    /// <summary>
    /// One page of the game made navigable. A screen answers two questions: is the game showing me
    /// right now (<see cref="IsActive"/>), and what is on me (<see cref="Build"/>).
    ///
    /// Build is IMMEDIATE MODE - it is called afresh for every navigation operation and declares the
    /// controls as they are at that instant. There is no retained tree to keep in step with the game,
    /// which is what lets a screen ignore the game's own refresh and rebind events entirely. The
    /// cursor survives rebuilds because control identities do (see ControlId), not because the tree
    /// does.
    ///
    /// Declaring nothing is legal and means "nothing here yet"; the navigator simply retries next
    /// frame. That is the right answer while a window is still animating in.
    ///
    /// A screen can also open a CHILD screen (see <see cref="PushChild"/>) - a menu of the actions a
    /// control offers, a list to pick from. Children are pushed by their parent rather than polled,
    /// because nothing in the game says they are open: they are the mod's own idea.
    /// </summary>
    public abstract class Screen
    {
        /// <summary>Stable identity, for logging and for telling two screens apart.</summary>
        public abstract string Key { get; }

        /// <summary>Which screens cover which. The highest layer among the active screens is the one
        /// the player is on.</summary>
        public virtual int Layer
        {
            get { return 0; }
        }

        /// <summary>Polled every frame. True while the game is showing this page and it is ready to
        /// be operated.</summary>
        public abstract bool IsActive();

        /// <summary>
        /// Whether the page can still be WORKED, as opposed to standing there while the engine switches
        /// it off - fading out after its own Close, or greyed under a confirmation it raised itself.
        ///
        /// This is the announcement side of a gap <see cref="IsActive"/> deliberately spans. A screen
        /// stays active across that gap on purpose (the improvements modal outlives its window so the page
        /// behind it is not handed back mid-fade), and in those frames the engine disables the whole
        /// renderer stack above the window, so every control on the page flips to unavailable at once. The
        /// live watch would then make the control the player has just pressed say "unavailable" as its
        /// last word - measured on the improvements window's Close and on the ship designer's Close, whose
        /// lose-your-changes box greys the window a frame before the box itself is up.
        ///
        /// Nothing about that is a fact about the control, so while this is false the live watch stays
        /// silent (it still re-baselines, so a change made across the gap is not announced late). Nothing
        /// else changes: the readouts, the refusals and the buffers are untouched, and a screen with no
        /// such gap never overrides this.
        /// </summary>
        public virtual bool IsWorkable
        {
            get { return true; }
        }

        /// <summary>Declare the screen's controls. Called on every navigation operation.</summary>
        public virtual void Build(GraphBuilder builder) { }

        /// <summary>Spoken when the player arrives on the screen, before the focused control reads.
        /// Null for a screen whose content already says where you are.</summary>
        public virtual string ScreenName
        {
            get { return null; }
        }

        /// <summary>Where focus lands on first arrival, as a Tab-stop key; null starts at the graph's
        /// own start node.</summary>
        public virtual object InitialFocusStop
        {
            get { return null; }
        }

        /// <summary>Keep the cursor position after the screen closes, for a page the player leaves
        /// and comes straight back to.</summary>
        public virtual bool KeepStateOnPop
        {
            get { return false; }
        }

        /// <summary>The back key was pressed. Return true when the screen handled it; false lets the
        /// game's own handling stand.</summary>
        public virtual bool Back()
        {
            return false;
        }

        /// <summary>
        /// An action fired on a screen where every key means the SAME one thing - the game's own "press
        /// anything to skip", which a cutscene answers with. Offered before the review chords and before
        /// navigation, because the point is that nothing else gets the press; return true when the screen
        /// acted on it.
        ///
        /// It exists because the mod cannot decline a key after the fact: the keys it claims are hidden
        /// from the game's binding matcher (`GameKeyStandDown`), so on a screen whose whole interaction is
        /// the game's press-anything handler, every claimed key EATS the skip. A screen with something to
        /// navigate never wants this, which is why it is opt-in per screen rather than a mode.
        ///
        /// Escape is not offered - it stays the game's like everywhere else (<see cref="ConsumesBack"/>),
        /// so the screen underneath keeps whatever the engine's own cancel does.
        /// </summary>
        public virtual bool AnyKey(string actionKey)
        {
            return false;
        }

        /// <summary>
        /// Whether <see cref="Back"/> is going to claim the key, asked BEFORE it is pressed.
        ///
        /// The mod and the game read the keyboard in parallel, so a key the mod acts on also does
        /// whatever the game has bound to it unless the game is told to stand down - and the game is
        /// told by a predicate it asks during its own scan, which may run either side of the mod's.
        /// Answering after the fact is therefore too late: by then the menu Escape closed is gone and
        /// the same Escape has already reached the pause menu underneath.
        ///
        /// This is a DIFFERENT question from <see cref="Back"/>, not a copy of it. Back asks what the
        /// mod does with the key; this asks whether the game must be denied it. The drop-list screen
        /// answers true to the first and false to this one on purpose - it handles Escape and still
        /// needs the engine to see it, because the engine's own cancel handling is what closes the
        /// popup.
        ///
        /// Screens overwhelmingly answer false: Escape belongs to the game, layer by layer, and only a
        /// surface the mod itself put on the screen - one the game knows nothing about and so cannot
        /// close - has any business taking the key away from it.
        /// </summary>
        public virtual bool ConsumesBack
        {
            get { return false; }
        }

        // ---- type-ahead search ----
        //
        // Typing letters on a screen searches its controls and moves focus to the match. Every
        // screen does it without asking; the two properties below are for the screens where the
        // letters mean something else.

        /// <summary>Whether typing searches this screen. False for a screen whose whole point is a
        /// box the player types into - the letters are the box's, whether or not the game has taken
        /// the keyboard for it yet.</summary>
        public virtual bool AllowsTypeahead
        {
            get { return true; }
        }

        /// <summary>
        /// Whether the screen is in the middle of handing the keyboard to the game - a key capture
        /// or a text editor asked for and not yet given.
        ///
        /// The mod's input layer stands down on its own once the game HAS the keyboard; this covers
        /// the frames before that, where the keys are still ours and the next thing typed is meant
        /// for the field.
        /// </summary>
        public virtual bool CapturesRawInput
        {
            get { return false; }
        }

        /// <summary>
        /// What a search on this screen looks through, given where the cursor is - null (the usual
        /// answer) for the declared controls of the focused Tab-stop.
        ///
        /// A screen answers only when what the player is searching for is not declared: a tree
        /// whose collapsed branches hold most of its items can offer them all here and open the
        /// branch when one is landed on. The navigator still does the focusing and the speaking.
        ///
        /// <paramref name="render"/> is the graph as it stands, so a screen that only wants to ADD the
        /// undeclared items can build the ordinary scope (<c>SearchScope.OverStop</c>) and extend it
        /// rather than re-deriving everything the stop already declares.
        /// </summary>
        public virtual SearchScope TypeAheadScope(GraphNode focused, GraphRender render)
        {
            return null;
        }

        public virtual void OnPush() { }

        public virtual void OnPop() { }

        // ---- child screens ----
        //
        // One linear chain: a screen has at most one child, which may have one of its own. The player
        // is on the deepest of them, and the manager works that out rather than being told, so a push
        // or a removal takes effect on the next tick with nothing else to keep in step.
        //
        // What this buys, and why it is worth a mechanism: a covered parent keeps its own GraphState,
        // so closing a menu puts the cursor back on the control that opened it for free. Nothing has
        // to remember where the player was, because nobody ever moved them.

        /// <summary>The screen this one was opened from, or null for a screen the manager polls.
        /// </summary>
        public Screen ParentScreen { get; private set; }

        /// <summary>The child this screen has open, or null.</summary>
        public Screen ActiveChild { get; private set; }

        /// <summary>Who to tell when a child closes, so its cursor can be dropped. Inherited by
        /// children from the screen they are pushed onto.</summary>
        internal ScreenManager Manager { get; set; }

        /// <summary>The screen the player is actually on: this one, or the deepest thing open over it.
        /// </summary>
        public Screen Deepest()
        {
            Screen at = this;
            // Bounded rather than "while": a chain is a handful deep by construction, and a cycle
            // introduced by a bug should not hang the frame.
            for (int depth = 0; depth < 16 && at.ActiveChild != null; depth++)
            {
                at = at.ActiveChild;
            }

            return at;
        }

        /// <summary>Open <paramref name="child"/> over this screen. Any child already open is closed
        /// first - one chain, no branching.</summary>
        public void PushChild(Screen child)
        {
            if (child == null || ReferenceEquals(child, ActiveChild))
            {
                return;
            }

            if (ActiveChild != null)
            {
                RemoveChild(ActiveChild);
            }

            child.ParentScreen = this;
            child.Manager = Manager;
            ActiveChild = child;
            child.OnPush();
        }

        /// <summary>Close <paramref name="child"/>, and anything it had open, deepest first. Focus
        /// falls back to this screen on the manager's next tick.</summary>
        public void RemoveChild(Screen child)
        {
            if (child == null || !ReferenceEquals(ActiveChild, child))
            {
                return;
            }

            if (child.ActiveChild != null)
            {
                child.RemoveChild(child.ActiveChild);
            }

            ActiveChild = null;
            child.OnPop();
            ScreenManager manager = child.Manager;
            child.ParentScreen = null;
            child.Manager = null;
            if (manager != null)
            {
                manager.ChildClosed(child);
            }
        }

        /// <summary>Close this screen from the inside - what a child screen's own Escape does.</summary>
        public void CloseSelf()
        {
            Screen parent = ParentScreen;
            if (parent != null)
            {
                parent.RemoveChild(this);
            }
        }

        public virtual void OnFocus() { }

        public virtual void OnUnfocus() { }

        /// <summary>Per-frame work for the focused screen only.</summary>
        public virtual void OnUpdate() { }
    }
}
