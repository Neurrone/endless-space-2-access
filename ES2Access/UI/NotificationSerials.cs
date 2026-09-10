using System.Collections.Generic;

namespace ES2Access.UI
{
    /// <summary>
    /// A serial per notification, its own for as long as the game holds it, so the rows the strip and
    /// the turn log declare can be keyed on the notification rather than on where it currently sits.
    ///
    /// Why it exists: both lists are rebuilt every frame from the empire's notification list, and a
    /// row keyed on its POSITION there is a key that belongs to whoever stands in that place next.
    /// Throw the first of three away and the second rebuilds under the key the first had, which the
    /// navigator reads as "the cursor has not moved" and says nothing - the player throws a
    /// notification away and hears silence. A serial leaves with the row it was given to, so the row
    /// the cursor falls to is a new key and announces itself.
    ///
    /// Identity is the notification INSTANCE: <c>GuiNotification</c> overrides neither Equals nor
    /// GetHashCode, so the dictionary keys by reference, which is exactly "the same notification" -
    /// the game builds one object per notification and keeps it until it is dismissed.
    ///
    /// A row costs one dictionary lookup. <see cref="Prune"/> is what keeps the dictionary the size
    /// of the list the player can see, and it walks only on the frames where the two disagree - which
    /// is the frame after a notification arrives or is thrown away, never the frames in between.
    /// </summary>
    public static class NotificationSerials
    {
        private static readonly Dictionary<GuiNotification, int> Assigned =
            new Dictionary<GuiNotification, int>();

        private static readonly HashSet<GuiNotification> Standing = new HashSet<GuiNotification>();

        private static readonly List<GuiNotification> Gone = new List<GuiNotification>();

        private static int _next;

        /// <summary>This notification's serial, assigning one if this is the first time it is seen.
        /// </summary>
        public static int Of(GuiNotification notification)
        {
            int serial;
            if (Assigned.TryGetValue(notification, out serial))
            {
                return serial;
            }

            serial = ++_next;
            Assigned.Add(notification, serial);
            return serial;
        }

        /// <summary>Forget every notification the empire no longer holds. Called once per walk of the
        /// list, with the list itself.</summary>
        public static void Prune(List<GuiNotification> standing)
        {
            if (standing == null || standing.Count == Assigned.Count)
            {
                return;
            }

            for (int i = 0; i < standing.Count; i++)
            {
                Standing.Add(standing[i]);
            }

            foreach (KeyValuePair<GuiNotification, int> entry in Assigned)
            {
                if (!Standing.Contains(entry.Key))
                {
                    Gone.Add(entry.Key);
                }
            }

            for (int i = 0; i < Gone.Count; i++)
            {
                Assigned.Remove(Gone[i]);
            }

            Standing.Clear();
            Gone.Clear();
        }

        /// <summary>Give up every serial: the notifications belong to a game this assembly is about to
        /// stop knowing about.</summary>
        public static void Clear()
        {
            Assigned.Clear();
            Standing.Clear();
            Gone.Clear();
            _next = 0;
        }
    }
}
