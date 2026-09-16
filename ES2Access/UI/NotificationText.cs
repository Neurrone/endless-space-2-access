using System;
using System.Collections.Generic;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// What a notification says, asked of the notification itself - and what to read when the game
    /// cannot say it.
    ///
    /// A notification names an entity, and its own title and description are written by formatting
    /// that entity's name into a template (<c>NotificationCuriosityFailed.GetTitle</c> reads
    /// <c>CuriosityController.LocalizedName</c>). When the entity it named is gone - destroyed since
    /// the notification was raised, or never resolved at all - the game's own code throws while
    /// writing the sentence. Unguarded that throw unwinds into whatever asked: a row's label lambda
    /// evaluated by the announcer takes the mod's per-frame pump with it, and the loader switches the
    /// whole mod off until a reload.
    ///
    /// So every place that asks a notification for its words asks HERE, and a part the game cannot
    /// produce is the EMPTY STRING (owner ruling 2026-09-16): empty says plainly that something went
    /// wrong, where a guess or a neighbour's sentence would say something false. The two parts are
    /// caught separately - a notification whose description throws still has a title worth reading -
    /// and the guard lives here, in the provider of the text, rather than in the graph layer, where it
    /// would swallow every future throwing part of every screen.
    ///
    /// A part that has thrown is remembered per notification INSTANCE, which is the game's own
    /// identity for "the same notification" (<see cref="NotificationSerials"/> keys on it for the same
    /// reason). Without that, a label read every frame throws and is caught every frame, and the log
    /// fills with one warning per frame; with it the game is asked once, the warning is written once,
    /// naming the notification's type and the exception, and every later frame answers from the memo
    /// without entering the game's code at all. The memo holds nothing but the notifications that
    /// failed, and it goes away with the assembly it lives in.
    /// </summary>
    public static class NotificationText
    {
        /// <summary>The notifications whose own text the game cannot write, and which half of it. Only
        /// failures are here: a notification that answers is asked again next time, because a
        /// stackable one is rebound to a newer event and its words change under it.</summary>
        private static readonly Dictionary<GuiNotification, int> Failures =
            new Dictionary<GuiNotification, int>();

        private const int TitleFailed = 1;

        private const int DescriptionFailed = 2;

        /// <summary>What the notification calls itself, cleaned of the markup a drawn string carries -
        /// or the empty string if the game cannot write it.</summary>
        public static string Title(GuiNotification notification)
        {
            bool failed;
            return Read(notification, true, out failed);
        }

        /// <summary>The sentence the notification carries - or the empty string if the game cannot
        /// write it.</summary>
        public static string Description(GuiNotification notification)
        {
            bool failed;
            return Read(notification, false, out failed);
        }

        /// <summary>
        /// One half of what the notification says, and whether the game could say it at all.
        ///
        /// <paramref name="failed"/> is the answer to "is this sentence the game's, or ours because
        /// the game threw" - the question a caller with another source of the same words has to ask
        /// before preferring that source. Null comes back for a part the notification simply left
        /// empty, which is not a failure and never was.
        /// </summary>
        public static string Read(GuiNotification notification, bool title, out bool failed)
        {
            failed = false;
            if (notification == null)
            {
                return null;
            }

            int known;
            Failures.TryGetValue(notification, out known);
            int part = title ? TitleFailed : DescriptionFailed;
            if ((known & part) != 0)
            {
                failed = true;
                return string.Empty;
            }

            try
            {
                return AgeText.Clean(title ? notification.GetTitle() : notification.GetDescription());
            }
            catch (Exception e)
            {
                Failures[notification] = known | part;
                failed = true;
                Log.Warn(
                    "notification: "
                        + notification.GetType().Name
                        + " cannot write its "
                        + (title ? "title" : "description")
                        + ", so it reads as nothing: "
                        + e
                );
                return string.Empty;
            }
        }

        /// <summary>Forget every notification that could not write its own words: they belong to a game
        /// this assembly is about to stop knowing about.</summary>
        public static void Clear()
        {
            Failures.Clear();
        }
    }
}
