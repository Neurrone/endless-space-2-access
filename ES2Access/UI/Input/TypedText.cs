namespace ES2Access.UI.Input
{
    /// <summary>
    /// What the player TYPED this frame, as opposed to which of the mod's actions they triggered.
    ///
    /// Deliberately not part of <see cref="ModInput"/>'s bindings: a binding is one key standing for
    /// one action, and this is text - the engine's own accumulation of characters, in the player's
    /// keyboard layout, at the OS repeat rate, with the shift and dead-key handling the mod has no
    /// business reimplementing. The type-ahead search is its only consumer.
    ///
    /// A chord is not typing: while Ctrl or Alt is held the characters belong to whatever the chord
    /// means (the review buffer's Ctrl+arrows, the game's own shortcuts), so nothing is reported.
    /// </summary>
    internal static class TypedText
    {
        /// <summary>The characters typed since the last frame, or null when there were none.</summary>
        public static string Frame()
        {
            // Reading inputString allocates, and it can only hold anything while a key is down, so
            // the cheap question comes first: on all but a handful of frames this is the whole cost
            // of having a search at all.
            if (!UnityEngine.Input.anyKey || Modified())
            {
                return null;
            }

            return UnityEngine.Input.inputString;
        }

        private static bool Modified()
        {
            return UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftControl)
                || UnityEngine.Input.GetKey(UnityEngine.KeyCode.RightControl)
                || UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftAlt)
                || UnityEngine.Input.GetKey(UnityEngine.KeyCode.RightAlt);
        }
    }
}
