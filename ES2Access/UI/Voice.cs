using ES2Access.Core.Speech;

namespace ES2Access.UI
{
    /// <summary>
    /// The engine-side shortcut to the mod's one speech chokepoint, for the UI layer's own lines
    /// (focus readouts, state feedback). It exists so the navigator and the node factories do not
    /// each rebuild the same <see cref="MessageBuilder"/> wrapper around <see cref="ModEntry.Speech"/>.
    ///
    /// Everything that calls this runs inside the per-frame pump - the navigator is driven from
    /// <see cref="ModEntry.Update"/> - so speaking directly is correct here, unlike from a watcher or
    /// a Harmony hook, which must only set state.
    /// </summary>
    internal static class Voice
    {
        /// <summary>Speak one already-composed line. Empty text and a torn-down mod are no-ops, so
        /// callers can pass an optional line blindly.</summary>
        public static void Say(string text, bool interrupt)
        {
            PrismSpeech speech = ModEntry.Speech;
            if (speech == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            speech.Speak(new MessageBuilder().Fragment(text), interrupt);
        }
    }
}
