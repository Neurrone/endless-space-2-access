namespace ES2Access.Core.UI
{
    /// <summary>
    /// A rung of a ladder the player has to be told they are on - how close a camera is looking, how
    /// far a slider has been pushed - when the thing that moved it was not a control they were
    /// standing on.
    ///
    /// A <see cref="StepWatch"/> with the three answers a ladder needs on top of it, all of which are
    /// "no, and the player knows anyway":
    ///
    /// The FIRST sighting is never news. This watches a value that exists wherever the player is, so
    /// there is no arrival to baseline against - the first rung it ever sees is simply where the game
    /// already was, and announcing it would say the zoom changed for someone who has only just started
    /// looking.
    ///
    /// A rung read out by a CONTROL is not news either. Where the player is standing on the slider that
    /// moves this very value, its own readout has already said the new number, and a second line would
    /// say it twice. It is still absorbed: the player has heard it, so moving away and coming back must
    /// not announce it again.
    ///
    /// A value still SETTLING is not an answer at all. Anything a camera flies to passes through
    /// intermediate states on the way, and reporting those would announce a rung the player never
    /// stopped at. Nothing is absorbed then - the watermark stays where it was, so whatever the value
    /// settles on is still news.
    ///
    /// Engine-free, so all of it is tested off-game.
    /// </summary>
    public sealed class RungWatch
    {
        private readonly StepWatch _watch = new StepWatch();

        private bool _seen;

        /// <summary>Nothing has been said, and nothing has been seen - the ladder itself has gone (a
        /// game left, a camera put away), so the next rung is a first sighting again.</summary>
        public void Forget()
        {
            _watch.Forget();
            _seen = false;
        }

        /// <summary>
        /// Whether <paramref name="rung"/> is one the player has to be TOLD about. A negative rung
        /// means the ladder does not apply here and is never announced.
        /// </summary>
        /// <param name="settling">The value is mid-flight and this rung is not where it will stop.</param>
        /// <param name="saidElsewhere">Something the player is already listening to has just read this
        /// rung out - the focused slider that moves it.</param>
        public bool NeedsTelling(int rung, bool settling, bool saidElsewhere)
        {
            if (rung < 0 || settling)
            {
                return false;
            }

            if (!_seen)
            {
                _seen = true;
                _watch.Baseline(rung);
                return false;
            }

            if (!_watch.IsNew(rung))
            {
                return false;
            }

            if (saidElsewhere)
            {
                _watch.Baseline(rung);
                return false;
            }

            return true;
        }

        /// <summary>Record that the player has now heard this rung. Called only from the path that
        /// actually spoke, so a frame that could not compose the words says them later.</summary>
        public void Told(int rung)
        {
            _watch.Told(rung);
        }
    }
}
