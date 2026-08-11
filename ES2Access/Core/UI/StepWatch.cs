namespace ES2Access.Core.UI
{
    /// <summary>
    /// A step the player has to be TOLD about - a wizard page the game turns under them, a phase it
    /// advances on its own while nothing is focused.
    ///
    /// It is a watermark and nothing else, but the two rules it carries are both easy to lose and
    /// neither shows up in a dump.
    ///
    /// The first: a screen whose ARRIVAL already announces the step it arrived on must baseline to
    /// that step, or the player hears the same words twice in a row - once as the screen's name and
    /// once as the change. Only a source the game keeps across the screen's own comings and goings is
    /// baselined this way; a source the game clears on leave is left at <see cref="Forget"/>, because
    /// baselining that one can swallow a genuine first event depending on frame timing.
    ///
    /// The second: the watermark moves only when the announcement was actually MADE. The game can
    /// change the step a frame before the words for the new one exist - a panel whose title is
    /// written as it starts animating in - and a watcher that consumed the change on that frame
    /// loses the announcement permanently, silently, and only for the fast machines.
    ///
    /// Engine-free, so both rules are tested off-game.
    /// </summary>
    public sealed class StepWatch
    {
        /// <summary>No step: what a surface reports before it has one, and what
        /// <see cref="Forget"/> goes back to.</summary>
        private const int Nothing = -1;

        private int _told = Nothing;

        /// <summary>Nothing has been said. The surface this was watching has gone, so nothing it saw
        /// still applies.</summary>
        public void Forget()
        {
            _told = Nothing;
        }

        /// <summary>Treat <paramref name="step"/> as already announced - what a screen does on
        /// arrival, when arriving is itself what says which step this is.</summary>
        public void Baseline(int step)
        {
            _told = step;
        }

        /// <summary>Whether <paramref name="step"/> is one the player has not been told about. A
        /// negative step means the surface has no step yet and is never announced.</summary>
        public bool IsNew(int step)
        {
            return step >= 0 && step != _told;
        }

        /// <summary>Record that the player has now heard about <paramref name="step"/>. Called only
        /// from the path that actually spoke.</summary>
        public void Told(int step)
        {
            _told = step;
        }
    }
}
