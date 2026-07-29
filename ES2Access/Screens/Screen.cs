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
    /// Not implemented yet: child-screen chaining (a screen pushing a sub-screen for a dropdown's
    /// option list or a confirmation box). The manager keeps a flat stack until a screen needs more.
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

        public virtual void OnPush() { }

        public virtual void OnPop() { }

        public virtual void OnFocus() { }

        public virtual void OnUnfocus() { }

        /// <summary>Per-frame work for the focused screen only.</summary>
        public virtual void OnUpdate() { }
    }
}
