namespace ES2Access.ES2.UI
{
    /// <summary>
    /// Where the technology wheel's viewport should be looking, given what the player has just opened
    /// or closed.
    ///
    /// The wheel is a tree drawn as a picture, and the two are the same thing: opening a branch is the
    /// player saying "show me this", so the camera goes there - to the quadrant when a quadrant is
    /// opened, to the ring when a stage is, and back out to the whole wheel when the last branch is
    /// closed and the player is standing at the top of the tree again. Closing a stage leaves them
    /// inside a quadrant, so that is what they get to look at.
    ///
    /// Engine-free because the rule is the interesting part: which level of the tree yields which
    /// view is a decision, and the arithmetic that turns a quadrant into a pair of coordinates is not.
    /// </summary>
    public static class ResearchCamera
    {
        /// <summary>How deep in the wheel the branch being opened or closed sits.</summary>
        public enum Level
        {
            Quadrant,
            Stage,
        }

        /// <summary>What the viewport should be showing. <see cref="None"/> is "leave it where it
        /// is", which is what a control that is not about the shape of the wheel asks for.</summary>
        public enum Aim
        {
            None,
            Overview,
            Quadrant,
            Stage,
            Technology,
        }

        /// <summary>The view the player has just moved into: the branch they opened, or - having
        /// closed one - the branch they are now standing in.</summary>
        public static Aim ForExpansion(Level level, bool expanded)
        {
            if (level == Level.Quadrant)
            {
                return expanded ? Aim.Quadrant : Aim.Overview;
            }

            return expanded ? Aim.Stage : Aim.Quadrant;
        }
    }
}
