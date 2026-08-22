using System.Diagnostics;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// What the last scanner press cost, kept so the question can be ASKED instead of guessed at.
    ///
    /// The scanner rebuilds every category on every press, and two of the questions it now asks are
    /// the kind that look cheap and are not: whether an empire could colonize a world runs a
    /// prerequisite check, and a galaxy holds hundreds of worlds. Nothing about that shows in a
    /// transcript or a dump - a slow press sounds exactly like a fast one - so the numbers are kept
    /// here, readable live (<c>ES2Access.UI.ScannerCost.Line()</c>) and logged by themselves when a
    /// press crosses the threshold at which a player would start to feel it.
    ///
    /// Public on purpose: the dev REPL compiles into its own assembly and cannot see anything
    /// internal, and a measurement nobody can take is not a measurement.
    /// </summary>
    public static class ScannerCost
    {
        /// <summary>How long a snapshot may take before it is worth a line in the log, in
        /// milliseconds. A keystroke the player is holding down repeats about every eighth of a
        /// second, so a rebuild costing a quarter of that is where "the key feels slow" begins.
        /// </summary>
        private const long Loud = 30;

        public static void Begin()
        {
            _checks = 0;
            _watch.Reset();
            _watch.Start();
        }

        public static void End()
        {
            _watch.Stop();
            _milliseconds = _watch.ElapsedMilliseconds;
            _colonizability = _checks;
            _presses++;
            if (_milliseconds >= Loud)
            {
                Log.Info("galaxy: " + Line());
            }
        }

        /// <summary>One question asked of the game that the scanner cannot ask cheaply - counted so a
        /// memo that stops working shows up as a number rather than as a feeling.</summary>
        public static void Colonizability()
        {
            _checks++;
        }

        /// <summary>What the last press cost, in one line.</summary>
        public static string Line()
        {
            return "scanner snapshot "
                + _milliseconds
                + " ms, "
                + _colonizability
                + " colonizability checks, press "
                + _presses;
        }

        public static long Milliseconds
        {
            get { return _milliseconds; }
        }

        public static int ColonizabilityChecks
        {
            get { return _colonizability; }
        }

        public static int Presses
        {
            get { return _presses; }
        }

        /// <summary>Mod teardown: the numbers describe a session, and the next one starts at nothing.
        /// </summary>
        public static void Forget()
        {
            _milliseconds = 0;
            _colonizability = 0;
            _checks = 0;
            _presses = 0;
        }

        private static int _checks;

        private static readonly Stopwatch _watch = new Stopwatch();
        private static long _milliseconds;
        private static int _colonizability;
        private static int _presses;
    }
}
