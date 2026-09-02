using System;
using System.Collections.Generic;
using HarmonyLib;

namespace ES2Access.UI
{
    /// <summary>
    /// Keeps the game's notification strip looking exactly as it always did, with the mod's own
    /// notifications in the list behind it.
    ///
    /// The strip is a scrolling table rebuilt from the same per-empire list the popup and the mod's
    /// stops read (<c>NotificationItemsWindow.Refresh</c> :158-185). Mod notifications sort after
    /// every game one, so a scouting turn that reveals eight systems would push the game's own icons
    /// - and the hover tooltips that are the only words on them - out of the visible band. So they
    /// are hidden from the strip and reached through the mod's own stop instead (owner ruling
    /// 2026-08-20).
    ///
    /// Hiding is done the way the engine's own table refresh does it, and that matters: the table
    /// decides between <c>Visible</c> and <c>Alpha</c> by its <c>StrictVisibility</c> flag
    /// (<c>AgeTransform.RefreshChildrenIList</c> :2382-2420) and the arrangement walk skips a child
    /// hidden either way (<c>ApplyDefaultChildrenArrangement</c> :2583-2586). Writing the OTHER
    /// field would leave a pooled row hidden for good, because the next refresh only ever restores
    /// the one the flag names.
    ///
    /// The frame behind the strip is faded with it: the window turns it on whenever the list is not
    /// empty, and a list holding nothing but mod notifications would otherwise draw an empty box.
    /// </summary>
    internal static class NotificationStrip
    {
        private static readonly ModPatch Patches = new ModPatch(
            "notificationstrip",
            "the notification strip"
        );

        public static void Install()
        {
            Patches.Install(
                patch =>
                    patch.Postfix(
                        AccessTools.Method(
                            typeof(NotificationItemsWindow),
                            "Refresh",
                            Type.EmptyTypes
                        ),
                        typeof(NotificationStrip),
                        "HideModItems"
                    )
            );
        }

        public static void Remove()
        {
            Patches.Remove();
        }

        /// <summary>Whether the strip patch is in place - what the teardown check reads.</summary>
        public static bool Installed
        {
            get { return Patches.Installed; }
        }

        /// <summary>Who is patching the strip's refresh right now (see <see cref="ModPatches"/>).
        /// </summary>
        internal static string[] Owners()
        {
            return ModPatches.Owners(
                AccessTools.Method(typeof(NotificationItemsWindow), "Refresh", Type.EmptyTypes),
                false
            );
        }

        private static void HideModItems(NotificationItemsWindow __instance)
        {
            try
            {
                AgeTransform table = __instance == null ? null : __instance.NotificationItemsTable;
                if (table == null)
                {
                    return;
                }

                bool strict = table.StrictVisibility;
                List<AgeTransform> children = table.Children;
                bool hidAny = false;
                int left = 0;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    AgeTransform child = children[i];
                    if (child == null)
                    {
                        continue;
                    }

                    bool shown = strict ? child.Visible : child.Alpha > 0f;
                    if (!shown)
                    {
                        continue;
                    }

                    NotificationItem item = child.GetComponent<NotificationItem>();
                    if (item == null || !(item.GuiNotification is ModNotification))
                    {
                        left++;
                        continue;
                    }

                    if (strict)
                    {
                        child.Visible = false;
                    }
                    else
                    {
                        child.Alpha = 0f;
                    }

                    hidAny = true;
                }

                if (!hidAny)
                {
                    return;
                }

                table.ArrangeChildren();
                float alpha = left <= 0 ? 0f : 1f;
                if (__instance.Frame != null)
                {
                    __instance.Frame.Alpha = alpha;
                }

                if (__instance.Background != null)
                {
                    __instance.Background.Alpha = alpha;
                }

                if (__instance.BlurMask != null)
                {
                    __instance.BlurMask.Alpha = alpha;
                }
            }
            catch (Exception e)
            {
                // Runs inside the window's own refresh: say so once rather than once a frame, and
                // leave the strip as the game drew it.
                Patches.Report("notifications: hiding the mod items from the strip threw", e);
            }
        }
    }
}
