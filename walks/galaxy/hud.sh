#!/bin/sh
# The galaxy HUD at overview: its dump, and focused tooltip probes on row 2 of two HUD
# regions (a screen button, a strategic resource) -- row 2 per the caption rule.
set -u
. "$(dirname "$0")/../lib.sh" "$@"

ensure_galaxy
capture overview "galaxy overview"

inp ui.focusEmpire
snap "$TMP/hud.txt"
delay0
for pair in 'hud:empire/screen/[A-Za-z]*\]|screen-button' 'hud:empire/resource/[A-Za-z0-9]*\]|strategic-resource'; do
  re=$(echo "$pair" | cut -d'|' -f1); lbl=$(echo "$pair" | cut -d'|' -f2)
  txt=$(label_nth "$TMP/hud.txt" "$re" 2)
  if [ -n "$txt" ] && findland "$txt"; then
    at "tip-$lbl"; tip "$lbl"
  else
    skip "no second HUD row matching $re - $lbl tooltip not captured"
  fi
done
delayrestore
done_
