namespace ES2Access.Core.UI.Graph
{
    /// <summary>How far a control asked for is from being in the render - the answer
    /// <see cref="KeyGraph.Reach"/> gives about one id.</summary>
    public enum ReachStep
    {
        /// <summary>The control is declared: focus can land on it now.</summary>
        Present,

        /// <summary>A collapsed group on the way to it was just opened; the control appears on a
        /// later build.</summary>
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
    /// progress towards it is being made - one level of ancestry opened per build
    /// (<see cref="KeyGraph.Reach"/>), and then however many frames the game takes to draw what that
    /// branch reads from - and dies two ways. A request nothing in the render leads to dies at once,
    /// which is what a landing aimed at a control that has simply gone away has always done. One
    /// whose branch is open and never produces it dies when the budget runs out, so an impossible id
    /// cannot keep a landing armed over the player's own navigation for the rest of the session.
    ///
    /// Off the engine so the budget is testable: the frame that drives it is the navigator's.
    /// </summary>
    public sealed class FocusRequest
    {
        /// <summary>About five seconds of frames. Long because the levels of a tree are not the slow
        /// part: a branch that reads from something the game DRAWS (a card the map binds once its
        /// camera has flown in) needs the flight, not the build. Short enough that an id the game
        /// never draws stops being waited for.</summary>
        public const int DefaultFrames = 300;

        private readonly ControlId _id;
        private readonly bool _announce;
        private int _frames;

        public FocusRequest(ControlId id, bool announce, int frames = DefaultFrames)
        {
            _id = id;
            _announce = announce;
            _frames = frames;
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

        /// <summary>Frames of waiting still allowed - for a test, and for a caller that wants to know
        /// how much of the budget a cascade cost.</summary>
        public int FramesLeft
        {
            get { return _frames; }
        }

        /// <summary>Spend this frame on the request, given how close the render says the control is.
        /// </summary>
        public FocusOutcome Step(ReachStep reach)
        {
            if (reach == ReachStep.Present)
            {
                return FocusOutcome.Land;
            }

            if (reach == ReachStep.Unreachable)
            {
                return FocusOutcome.Drop;
            }

            return --_frames > 0 ? FocusOutcome.Wait : FocusOutcome.Drop;
        }
    }
}
