#!/bin/sh
# The notification popup a pending notification raises: its dump and the notification parity
# probe, then the A-B-A rebind check of its pooled body by browsing to the next notification
# and back with the popup's own arrows.
#
# A pending notification is a fixture accident the walk may neither create nor destroy:
# raising one costs a turn and dismissing one changes the save. So the strip is READ, the
# popup is closed through the manager (a hide, not a dismiss), and the AlreadyRead flags that
# opening sets are put back. If the strip is empty the popup is skipped and recorded.
set -u
. "$(dirname "$0")/../lib.sh" "$@"

NOTIFBODY='{ var ws = UnityEngine.Object.FindObjectsOfType<NotificationWindow>(); for (int q=0;q<ws.Length;q++) if (ws[q].Shown) t = ws[q].gameObject.GetComponent<AgeTransform>(); }'
ensure_galaxy
inp ui.focusNotifications
snap "$TMP/hud.txt"
NNOTIF=$(nkeys "$TMP/hud.txt" 'hud:notification/[0-9]*\]')
echo "   discovered: $NNOTIF pending notification(s)"
READSTATE=$(fact '((System.Func<string>)(() => { var sb=new System.Text.StringBuilder(); System.Collections.IList ns=(System.Collections.IList)Gui.GuiNotificationService.GetPlayerEmpireGuiNotifications(); for(int i=0;i<ns.Count;i++){ if(sb.Length>0) sb.Append("|"); sb.Append(((GuiNotification)ns[i]).AlreadyRead); } return sb.ToString(); }))()')

if [ "${NNOTIF:-0}" -lt 1 ]; then
  skip "the HUD strip holds no pending notification - notification popup not captured"
else
  inp ui.home; inp ui.click
  onscreen screen.notification 10000 || echo "   NOTE: the popup did not focus"
  at popup-open
  dump popup
  ghosts "notification popup"
  printf '\n--- NotificationParity ---\n' >> "$OUT/ghosts.txt"
  curl -s -X POST --data-raw 'ES2Access.Dev.DevProbe.NotificationParity()' "$HOST/eval?speech=0" >> "$OUT/ghosts.txt"
  if [ "${NNOTIF:-0}" -lt 2 ]; then
    skip "the HUD strip holds fewer than two pending notifications - the popup-body rebind is unreachable"
  else
    gwalk graph a 'notification:'
    gpool pool  a "$NOTIFBODY"
    inp ui.pageNext; frame; frame                    # the popup's own Next arrow
    at popup-next
    gpool poolb b "$NOTIFBODY"
    inp ui.pagePrev; frame; frame                    # and its Previous arrow, back to A
    at popup-back
    gwalk graph a2 'notification:'
    gpool pool  a2 "$NOTIFBODY"
    aba graph pool
  fi
  # Closed through the manager, not by hiding the window: the manager's hide is the same HideWindow
  # plus CurrentGuiNotification = null, and a slot left set suppresses every later auto-open and the
  # keyboard zoom (docs/notifications.md). Hiding dismisses nothing.
  evs '((System.Func<string>)(() => { var mgr = Gui.GuiNotificationService; mgr.HideAllGuiNotifications(); return "hid all; current=" + (mgr.CurrentGuiNotification == null ? "null" : "SET"); }))()'
  onscreen screen.galaxy 10000 || echo "   NOTE: the galaxy HUD did not come back"
fi
# Put back the read flags that opening the popup set, so the strip is left as it was found.
if [ -n "$READSTATE" ]; then
  evs "((System.Func<string>)(() => { string[] want = \"$READSTATE\".Split('|'); System.Collections.IList ns=(System.Collections.IList)Gui.GuiNotificationService.GetPlayerEmpireGuiNotifications(); int n=0; for(int i=0;i<ns.Count && i<want.Length;i++){ var g=(GuiNotification)ns[i]; bool w = want[i]==\"True\"; if(g.AlreadyRead!=w){ g.AlreadyRead=w; n++; } } return \"restored \"+n+\" read flags\"; }))()"
fi
done_
