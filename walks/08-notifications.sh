#!/bin/sh
# Family: the notification popup a pending notification raises, plus the HUD strip and the
# turn-log place key.
#
# A pending notification is a fixture accident, not something the walk may create: raising
# one costs a turn and dismissing one changes the save. So the strip is READ -- if it holds
# a notification the popup is opened and dumped, and the popup is closed by HIDING its
# window, never through the dismiss key. If the strip is empty the popup dump is skipped
# and recorded.
set -u
FAMILY=notifications; . "$(dirname "$0")/lib.sh" "$@"

prologue

inp ui.focusNotifications
snap "$TMP/hud.txt"
NNOTIF=$(nkeys "$TMP/hud.txt" 'hud:notification/[0-9]*\]')
echo "   discovered: $NNOTIF pending notification(s)"

if [ "${NNOTIF:-0}" -ge 1 ]; then
  inp ui.home
  at "on-notification-row"
  inp ui.activate; pause 2000
  at "popup-open"
  dump 01-notification-popup
  ghosts "notification popup"
  printf '\n--- NotificationParity ---\n' >> "$OUT/ghosts.txt"
  curl -s -X POST --data-raw 'ES2Access.Dev.DevProbe.NotificationParity()' "$HOST/eval?speech=0" >> "$OUT/ghosts.txt"
  evs '((System.Func<string>)(() => { var ws = UnityEngine.Object.FindObjectsOfType<NotificationWindow>(); var sb = new System.Text.StringBuilder(); for (int i=0;i<ws.Length;i++){ if (ws[i].Shown) { sb.Append(ws[i].Name).Append(","); Gui.GuiService.HideWindow(ws[i]); } } return sb.Length==0?"no popup shown":("hid "+sb.ToString()); }))()'
  pause 1500
else
  skip "the HUD strip holds no pending notification - notification popup not captured"
fi

# the turn-log place key: record whether this fixture declares that stop at all
inp ui.focusTurnLog
at "turn-log-key"
dump 02-hud-after-turn-log-key
ghosts "galaxy HUD after the turn-log key"
snap "$TMP/tl.txt"
if [ "$(nkeys "$TMP/tl.txt" 'hud:turn-log')" -eq 0 ]; then
  skip "hud:turn-log is not declared in this fixture - the key leaves the cursor where it was"
fi

epilogue
