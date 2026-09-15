#!/bin/sh
# The minor-civilization window, bound to whichever minor empire the galaxy happens to hold
# (cs/minor.cs finds one). NEVER presses a diplomacy action. The pirate window throws with no
# pirate systems and the negotiation modal is never opened; both are covered by by-key.sh.
set -u
. "$(dirname "$0")/../lib.sh" "$@"

waitfor "$P_NOMODAL" 50 || drain
curl -s -X POST --data-binary "@$CS/minor.cs" "$HOST/eval?speech=0" > "$TMP/minor.out"
grep -oE '"result":"[^"]*"|"error":"[^"]*"' "$TMP/minor.out"
if grep -qE '"result":"minor shown' "$TMP/minor.out" && onscreen screen.minor-diplomacy 10000; then
  tut
  capture modal "minor civilization window"
  delay0; inp ui.next; at tip-relation; tip relation; delayrestore
  exitwin MinorFactionDiplomacyModalWindow
else
  skip "no minor empire with a system in this galaxy - minor-civilization window not captured"
fi
done_
