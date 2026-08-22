using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.UI;

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

        /// <summary>
        /// A page that exists to be ANSWERED and holds nothing else: the error box, the two message
        /// boxes, the drop list, the loading screen - and the chat page, which contains the player in
        /// the panel on purpose. Whatever the game may still be drawing around them is not theirs - the
        /// player answers, and the page underneath comes back with everything that belongs to it.
        /// Overridden by those pages so that <see cref="BuildShared"/> adds nothing to them; every
        /// other page leaves it alone.
        /// </summary>
        public virtual bool AnswersOnly
        {
            get { return false; }
        }

        /// <summary>
        /// What a page declares beyond its own content, added after <see cref="Build"/>.
        ///
        /// Two things, and both for the same reason: the game draws them OVER whatever the player is
        /// looking at, so they belong to whatever page that is rather than to the page they were first met
        /// on. The second is the chat panel's new-message button
        /// (<see cref="ChatScreen.BuildNewMessages"/>) - the only part of the panel the game draws while
        /// chat is closed, and the page-level way into a chat page that is otherwise entered with the
        /// game's own chat key.
        ///
        /// The first is the bar a COLLAPSED tutorial leaves on screen. Collapsing the popup hands the
        /// keyboard back to the page underneath, so the bar belongs to whatever page that is - and it is
        /// declared exactly where the game is DRAWING it, which is the gate
        /// <see cref="TutorialScreen.BuildCollapsedBar"/> asks: a tutorial page declares for itself what
        /// it may be drawn above, so an <c>Above*</c> page keeps its bar over screens, modals and
        /// notifications alike (and clickable there), while an <c>UnderScreens</c> page's panel is
        /// HIDDEN by the game the moment anything opens - and then there is nothing to declare and
        /// nothing is. Following the drawing is what stops a player who minimised a tutorial over a
        /// modal from having no way back to it, without inventing a bar on a page where the game drew
        /// none.
        ///
        /// A page that reads the bar among its OWN stops - the galaxy and the ten pages that share the
        /// HUD's right-hand edge, where it is drawn above the notification icons - keeps the place it
        /// chose; every other page gets it last. A page that only takes an ANSWER
        /// (<see cref="AnswersOnly"/>) gets it not at all.
        ///
        /// And a page that has declared NOTHING gets it not at all either, which is the whole of why
        /// these are contributions rather than a screen. "Nothing here yet" is what a page arriving in
        /// pieces says while it waits for the half the cursor must be seated on, and it works because
        /// an empty render is skipped and the cursor is left alone. A strip added to that emptiness
        /// makes the render non-empty and hands the seat to the strip - measured on the star system
        /// page, which becomes active exactly one frame before its planet cards are drawn: on the way
        /// back from the technology wheel the cursor landed on "Close tutorial" and stayed there, since
        /// the bar is declared on every later frame too and reconciliation then has no reason to move.
        /// </summary>
        public void BuildShared(GraphBuilder builder)
        {
            if (AnswersOnly || !builder.DeclaredAnything)
            {
                return;
            }

            if (!builder.DeclaredStop(GlobalHud.TutorialStop))
            {
                builder.BeginStop(GlobalHud.TutorialStop);
                TutorialScreen.BuildCollapsedBar(builder);
            }

            if (!builder.DeclaredStop(ChatScreen.AlertStop))
            {
                ChatScreen.BuildNewMessages(builder);
            }
        }

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
        /// The contextual key - the game's own right click - offered to the SCREEN before the focused
        /// control's own command. Return true when the screen took it.
        ///
        /// For a mode the GAME has put the page into, which has taken the right click for itself and
        /// means it wherever the pointer is standing: the map waiting for an order's target answers here,
        /// so the same key stops sending fleets for exactly as long as the game would have stopped
        /// answering a right click with a move. A screen with no such mode never overrides this, and the
        /// control's own right click is untouched.
        /// </summary>
        public virtual bool Contextual()
        {
            return false;
        }

        /// <summary>
        /// The second-command key (Backspace) offered to the SCREEN before the focused control's own
        /// <see cref="NodeVtable.OnSecondary"/>. Return true when the screen took it.
        ///
        /// For a command that belongs to a PANEL rather than to a control: the galaxy's way back down
        /// the starlanes it has been travelled is about where the player has been, not about the lane or
        /// the planet the cursor is standing on, and wiring it per node would mean wiring it onto every
        /// node the panel will ever declare. The focused node is passed because such a command is
        /// usually scoped to one stop - a screen answers only where it means something and leaves every
        /// other panel's Backspace alone.
        ///
        /// A screen that never overrides this changes nothing: the control's own second command is
        /// reached exactly as before.
        /// </summary>
        public virtual bool Secondary(GraphNode focused)
        {
            return false;
        }

        /// <summary>
        /// Turn the page back or on - the previous/next system, planet, notification, hero - offered to
        /// the SCREEN wherever the cursor is standing on it, because what these turn is the whole
        /// surface rather than the control under the cursor. Return true when the screen took the key.
        ///
        /// A screen overrides them exactly where the GAME draws such a pair, and answers by pressing the
        /// game's own button through <see cref="Page"/>. A screen that draws none never overrides them
        /// and the key does nothing at all.
        /// </summary>
        public virtual bool PagePrev()
        {
            return false;
        }

        public virtual bool PageNext()
        {
            return false;
        }

        /// <summary>How a screen answers the page keys: press the game's own arrow while the game has it
        /// switched on, and say nothing at either end of the run.
        ///
        /// The key is taken (true) wherever the pair is DRAWN, switched off included - a page that has
        /// run out of systems to step to has answered the press, and repeating the name of the page the
        /// player is already on would be the checkbox that re-reads itself at a limit. A pair the game
        /// is not drawing is not this screen's key at all.</summary>
        protected static bool Page(AgeTransform button)
        {
            if (button == null || !AgeWidgets.Visible(button))
            {
                return false;
            }

            if (AgeWidgets.Operable(button))
            {
                AgeWidgets.Press(button);
            }

            return true;
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
        /// Escape MAY arrive here - the galaxy's inspect cursor takes it to leave the mode - but a
        /// screen only sees it where <see cref="ConsumesBack"/> has denied the game the key, and a live
        /// type-ahead search's Escape outranks every answer given here: the dispatch withholds the key
        /// from this hook while a search is up, so the innermost surface ends first (owner ruling
        /// 2026-08-19, <c>ModEntry.Dispatch</c>). The cutscene declines the key outright.
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

        /// <summary>
        /// Whether a landing this screen asked for should be held rather than worked on this frame -
        /// true while the page is in a state where nothing it declares can be judged.
        ///
        /// The navigator gives up a landing the render leads nowhere near, and spends a frame of its
        /// budget on every other frame it waits (<see cref="FocusRequest"/>). Both are the wrong answer
        /// while the game is mid-flight between views: what the page declares then is a half-built
        /// render of somewhere the camera has not arrived at, and reading "nothing leads there" off it
        /// throws away a landing that would have worked a second later. A screen the game never moves
        /// under says nothing here, which is the default.
        /// </summary>
        public virtual bool LandingSuspended
        {
            get { return false; }
        }

        /// <summary>
        /// The player has just moved the cursor on this screen themselves: give up any landing this
        /// screen is still waiting to make.
        ///
        /// The navigator cancels its own outstanding request on the same three keystrokes, and calls
        /// this beside it so a screen holding a landing of its OWN - one still waiting for the game to
        /// draw the control it is aimed at, which the navigator has not been told about yet - dies with
        /// it. Without it a seat armed by a button press outlives the player walking away from where it
        /// was going to put them, and lands minutes later on something they have forgotten asking for.
        ///
        /// Only ever called for the screen the player is ON, which is what scopes it: another page's
        /// arrival, and the player reading or dismissing a cutscene, leave this screen's landings alone.
        /// </summary>
        public virtual void CancelLandings() { }

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
