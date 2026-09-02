using ES2Access.Loader.Dev;

namespace ES2Access.Dev
{
    /// <summary>
    /// Everything the mod has spoken, so a developer or agent who cannot hear the screen reader can
    /// read it back over HTTP.
    ///
    /// It is the loader's own ring (<see cref="SeqLog"/>) with the mod's capacity on it, and nothing
    /// else: the sequencing, the cursor, the blocking wait for the next line and the settle poll are
    /// all one implementation. There used to be a second, line-for-line identical one here, and the
    /// two of them drifted in the only way they could - the loader's grew the settle poll, this one
    /// grew the blocking wait, and neither could be used through the other.
    ///
    /// Written from the Unity main thread (the speech pump) and read from HTTP handler threads.
    /// </summary>
    internal sealed class SpeechLog : SeqLog
    {
        /// <summary>How many lines are kept. Longer than the loader's own log, because the question
        /// asked of this one is usually "what did that whole walk say".</summary>
        private const int Capacity = 1000;

        public SpeechLog()
            : base(Capacity) { }
    }
}
