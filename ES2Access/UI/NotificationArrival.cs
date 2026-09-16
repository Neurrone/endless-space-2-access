using System;
using System.Collections.Generic;
using HarmonyLib;

namespace ES2Access.UI
{
    /// <summary>
    /// A notification popup is drawn in full the moment it opens, instead of fading its contents in
    /// over the half second after.
    ///
    /// The first time a popup is shown - which is every ordinary arrival, because the game writes
    /// <c>FirstShow = !AlreadyRead</c> when it shows one (<c>GuiNotificationManager</c> :527) - it
    /// hides everything the prefab named in <c>AnimateOnEndShowTransforms</c> while the window itself
    /// slides in (<c>NotificationWindow.OnBeginShow</c> :132-165) and only starts fading those pieces
    /// up once the window reports itself ready (<c>OnEndShow</c> :167-177). For most of a second the
    /// popup is therefore a frame with a title in it and nothing else, which cost the player the
    /// thing the whole popup is for: the cursor lands on a screen once and is never moved again, so
    /// the mod either seated them on the browse arrow in the top strip because no words existed yet,
    /// or waited out the fade and said nothing at all meanwhile - on a popup whose text types itself
    /// out a letter at a time, for seven seconds.
    ///
    /// So the fade is finished as soon as it is started. The call is the game's own: showing a popup
    /// for the SECOND time takes the other branch of <c>OnBeginShow</c>, which makes each named piece
    /// visible and puts it straight at its end state with
    /// <c>ResetAllModifiers(toStart: false, recursive: true, applyValue: true)</c>. Doing exactly
    /// that on the first show reproduces the re-show's picture and nothing else: no state of ours to
    /// restore, nothing to undo when the mod goes away, and no cost on any frame the game is not
    /// already opening a popup on.
    ///
    /// The hook is on <c>NotificationWindow.OnEndShow</c> itself rather than on the windows that
    /// override it, so it runs where the base class starts the fade and BEFORE whatever the subclass
    /// does next - the academy portrait's lip sync among it (<c>DiplomaticInteractionNotification
    /// Window.OnEndShow</c> :382-398), which is left to play as it always did.
    ///
    /// One limit, measured rather than guessed: a prefab that animates something it did NOT name -
    /// the alliance update window starts its rename and member groups by hand after the base call -
    /// still fades that part in. It is in the notification notes.
    ///
    /// The one animation that is NOT finished is the one that types its text out a letter at a time,
    /// which is set going again after the reset. A typewriter does not fade a label in, it moves the
    /// character counter the renderer draws up to, and <c>AgeModifier.ResetToEnd</c> stops a modifier
    /// without applying its value - so a typewriter the game had just started is left stopped at zero
    /// characters and its label draws nothing at all, for as long as the popup is up (measured on the
    /// new-unlocked-content popup's lore panel). Six popups put one inside a named piece. Typing is
    /// also the whole point of those six, so they are left typing exactly as the game starts them,
    /// with everything around the text drawn in full from the first frame.
    /// </summary>
    internal static class NotificationArrival
    {
        private static readonly ModPatch Patches = new ModPatch(
            "notificationarrival",
            "the notification popup's arrival"
        );

        public static void Install()
        {
            Patches.Install(
                patch =>
                    patch.Postfix(
                        AccessTools.Method(typeof(NotificationWindow), "OnEndShow", Type.EmptyTypes),
                        typeof(NotificationArrival),
                        "FinishFade"
                    )
            );
        }

        public static void Remove()
        {
            Patches.Remove();
        }

        private static void FinishFade(NotificationWindow __instance)
        {
            try
            {
                // Only the first show starts a fade; on any other the game has already put these
                // pieces at their end state and there is nothing to finish.
                if (__instance == null || !__instance.FirstShow)
                {
                    return;
                }

                AgeTransform[] animated = __instance.AnimateOnEndShowTransforms;
                for (int i = 0; animated != null && i < animated.Length; i++)
                {
                    AgeTransform piece = animated[i];
                    if (piece != null)
                    {
                        piece.ResetAllModifiers(toStart: false, recursive: true, applyValue: true);
                        KeepTyping(piece);
                    }
                }
            }
            catch (Exception e)
            {
                // Runs inside the window's own show: say so once rather than once a popup, and leave
                // the arrival fading as the game drew it.
                Patches.Report("notifications: finishing the popup's arrival threw", e);
            }
        }

        /// <summary>
        /// Starts the typewriters under one named piece over again, undoing the reset for them alone.
        /// The game has just started every modifier under this piece
        /// (<c>NotificationWindow.OnEndShow</c> :167-177), so a typewriter found here is one the
        /// reset stopped a moment ago, and starting it puts it back exactly where that left it.
        ///
        /// Walks the piece's AGE children rather than the GameObject's, because that is the same
        /// subtree - and the same order - the reset itself covered
        /// (<c>AgeTransform.ResetAllModifiers</c>), and a modifier sits on the transform's own object
        /// (<c>AgeTransform.Awake</c> reads them with <c>GetComponents</c>). Cost: once per popup that
        /// is opening for the first time, over the handful of transforms its prefab named - never on a
        /// frame that is not opening a popup. A prefab that names both a label and the group holding
        /// it starts that one typewriter twice, in the same frame and before anything is drawn, which
        /// leaves it exactly where starting it once would have (the new-unlocked-content popup does).
        /// </summary>
        private static void KeepTyping(AgeTransform piece)
        {
            AgeModifierTypewriter typewriter = piece.GetComponent<AgeModifierTypewriter>();
            if (typewriter != null)
            {
                typewriter.StartAnimation();
            }

            List<AgeTransform> children = piece.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                if (children[i] != null)
                {
                    KeepTyping(children[i]);
                }
            }
        }
    }
}
