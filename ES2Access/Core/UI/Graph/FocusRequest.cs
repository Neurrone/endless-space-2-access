using System.Collections.Generic;

namespace ES2Access.Core.UI.Graph
{
    /// <summary>How far a control asked for is from being in the render - the answer
    /// <see cref="KeyGraph.Reach"/> gives about one id.</summary>
    public enum ReachStep
    {
        /// <summary>The control is declared: focus can land on it now.</summary>
        Present,

        /// <summary>A collapsed group on the way to it was just opened - together with every level
        /// between it and the control that the render does not declare yet; the control appears on the
        /// build that follows.</summary>
        Opened,

        /// <summary>Something on the way to it is declared and already open, but the control is not
        /// there yet - the game has not drawn what the branch reads from.</summary>
        Waiting,

        /// <summary>Nothing in the render leads to it: there is no branch to open, so no amount of
        /// waiting can produce it.</summary>
        Unreachable,
    }

    /// <summary>What a caller holding a <see cref="FocusRequest"/> should do this frame.</summary>
    public enum FocusOutcome
    {
        /// <summary>Put the cursor on it and forget the request.</summary>
        Land,

        /// <summary>Keep the request for another frame.</summary>
        Wait,

        /// <summary>Give up on it.</summary>
        Drop,
    }

    /// <summary>
    /// A landing that has been asked for and not yet made - a screen sending the cursor somewhere.
    ///
    /// The control is often not in the render when it is asked for: the branch it hangs in is
    /// collapsed, and a collapsed group declares no children at all. So the request survives while
    /// progress towards it is being made - the WHOLE path to it opened by the first build that can
    /// see any of it (<see cref="KeyGraph.Reach"/>), and then however long the game takes to draw what
    /// that branch reads from - and dies two ways. A request nothing in the render leads to dies at
    /// once, which is what a landing aimed at a control that has simply gone away has always done. One
    /// whose branch is open and never produces it dies when the budget runs out, so an impossible id
    /// cannot keep a landing armed over the player's own navigation for the rest of the session.
    ///
    /// The budget is WAITING TIME, not builds: how long a branch takes to produce its child is the
    /// game's business (a card the map binds once its camera has flown in), and how many builds fit in
    /// that time is the mod's own idle rate, which is allowed to change. Opening is not waiting and
    /// spends none of it; it has its own cap, because a level that answers "opened" for ever - a group
    /// whose own expand hook refuses - must not loop.
    ///
    /// Both deaths only happen on a frame the request is being WORKED ON. A landing belongs to the
    /// screen that asked for it, and frames where that screen is not the one the player is on cannot
    /// carry it any further: the game may be playing a cutscene over it, or flying the camera between
    /// two views, and the render such a frame would be judged against is somebody else's or half built.
    /// So those frames are SUSPENDED - the request is kept, nothing is spent, and neither "nothing
    /// leads there" nor the budget can kill it - and it resumes with the budget it had when the player
    /// comes back (owner-reported: a fleet action's seat died during the system-discovery cutscene and
    /// the cursor never moved).
    ///
    /// Off the engine so the budget is testable: the clock that drives it is the navigator's.
    /// </summary>
    public sealed class FocusRequest
    {
        /// <summary>Five seconds of waiting. Long because the levels of a tree are not the slow part:
        /// a branch that reads from something the game DRAWS (a card the map binds once its camera has
        /// flown in) needs the flight, not the build. Short enough that an id the game never draws
        /// stops being waited for.</summary>
        public const double DefaultSeconds = 5.0;

        // Openings allowed beyond the levels the id's own key names: the grouping level a page may add
        // above them (<see cref="KeyGraph.GroupingAncestor"/>), plus one so every request gets at
        // least one opening.
        private const int SpareOpens = 2;

        private readonly ControlId _id;
        private readonly bool _announce;
        private readonly object _owner;
        private List<ControlId> _opened;
        private double _seconds;
        private int _opens;

        public FocusRequest(ControlId id, bool announce, double seconds = DefaultSeconds)
            : this(id, announce, null, seconds) { }

        public FocusRequest(ControlId id, bool announce, object owner, double seconds = DefaultSeconds)
        {
            _id = id;
            _announce = announce;
            _owner = owner;
            _seconds = seconds;
            _opens = PathLevels(id) + SpareOpens;
        }

        // How many levels the id's own key names - counted off the key rather than through
        // KeyGraph.AncestorKeys, because a request is sometimes made inside a build (a zoom hand-off
        // re-asks every build): this allocates nothing and asks no injected hook.
        private static int PathLevels(ControlId id)
        {
            string path = id == null ? null : id.StructuralKey as string;
            if (path == null)
            {
                return 0;
            }

            int levels = 0;
            for (int i = 0; i < path.Length; i++)
            {
                if (path[i] == '/') levels++;
            }

            return levels;
        }

        /// <summary>Who asked for the landing - the screen whose graph the id belongs to, or null where
        /// the caller does not scope its requests. The one question that decides whether a frame counts:
        /// only the screen that asked can carry its own landing forward, and only that screen's own
        /// navigation cancels it. Compared by reference; nothing here reads it.</summary>
        public object Owner
        {
            get { return _owner; }
        }

        /// <summary>The control the cursor was asked to land on.</summary>
        public ControlId Id
        {
            get { return _id; }
        }

        /// <summary>Whether the landing should be read out (false: the caller has said its own piece
        /// about it).</summary>
        public bool Announce
        {
            get { return _announce; }
        }

        /// <summary>Seconds of waiting still allowed - for a test, and for a caller that wants to know
        /// how much of the budget a cascade cost.</summary>
        public double SecondsLeft
        {
            get { return _seconds; }
        }

        /// <summary>Openings still allowed before the request gives up on a branch that keeps
        /// answering "opened" - see the class summary.</summary>
        public int OpensLeft
        {
            get { return _opens; }
        }

        /// <summary>
        /// The levels <see cref="KeyGraph.Reach"/> expanded FOR THIS REQUEST that the render declared
        /// no control for - null until there are any.
        ///
        /// Opening the whole path means expanding keys that are not controls at all (the <c>a/b</c> in
        /// a tree that declares only <c>a</c> and <c>a/b/c</c>). Nothing would ever collapse those
        /// again, so the request owns them: when it ends, whichever of them the render still declares
        /// nothing for are taken back out (<see cref="KeyGraph.ReleaseOpenedPath"/>), leaving the
        /// expansion set as opening one level at a time would have left it. Levels already expanded
        /// when the request found them are not recorded: they are somebody else's.
        /// </summary>
        public IList<ControlId> OpenedPath
        {
            get { return _opened; }
        }

        /// <summary>Note a level this request expanded itself. <see cref="KeyGraph.Reach"/> is the
        /// only caller.</summary>
        public void RecordOpened(ControlId id)
        {
            if (_opened == null)
            {
                _opened = new List<ControlId>();
            }

            _opened.Add(id);
        }

        /// <summary>Let go of the record - the clean-up has dealt with it.</summary>
        public void ForgetOpenedPath()
        {
            _opened = null;
        }

        /// <summary>Spend a pass on the request, given how close the render says the control is and how
        /// long (real seconds) since the last pass judged it.</summary>
        public FocusOutcome Step(ReachStep reach, double seconds)
        {
            return Step(reach, false, seconds);
        }

        /// <summary>
        /// The same, on a pass that may be SUSPENDED - one where nothing about this request can be
        /// judged, because the screen that asked for it is not the one being drawn or the view is still
        /// moving.
        ///
        /// A suspended pass costs nothing and proves nothing: the budget is not spent, an
        /// "unreachable" answer is not believed, and a control that IS present does not land either -
        /// because the render being asked is not the render the landing was aimed at, and neither is
        /// what that control would SAY. A landing announces itself once, and a node read while the
        /// camera is still flying reads the far view's version of itself (the galaxy's planet rows:
        /// "Osulo I, Colonized" before the flight, "Osulo I, group, Medium Mediterrane., Colonized,
        /// collapsed" after it). So the whole judgement waits, landing the frame the screen says it is
        /// settled. An earlier revision landed a present control mid-transition and shipped exactly
        /// that defect.
        /// </summary>
        public FocusOutcome Step(ReachStep reach, bool suspended, double seconds)
        {
            if (suspended)
            {
                return FocusOutcome.Wait;
            }

            if (reach == ReachStep.Present)
            {
                return FocusOutcome.Land;
            }

            if (reach == ReachStep.Unreachable)
            {
                return FocusOutcome.Drop;
            }

            // Opening cost the request a pass, not waiting time: the whole path opens on the pass that
            // first sees any of it, and a level that has to be opened separately after that is one
            // whose own hook ignores the expansion set. Its own cap is what stops such a level looping.
            if (reach == ReachStep.Opened)
            {
                return --_opens > 0 ? FocusOutcome.Wait : FocusOutcome.Drop;
            }

            _seconds -= seconds;
            return _seconds > 0 ? FocusOutcome.Wait : FocusOutcome.Drop;
        }
    }
}
