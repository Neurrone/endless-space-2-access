#!/bin/sh
# Family: the diplomacy page and the minor-civilization window (bound to whichever minor
# empire the galaxy happens to hold).
# NEVER presses a diplomacy action. The negotiation modal is deliberately NOT opened --
# closing an unsigned negotiation posts an order. The pirate window throws with no pirate
# systems. Both are covered by-key in 09.
set -u
FAMILY=diplomacy; . "$(dirname "$0")/lib.sh" "$@"

prologue

openwin DiplomacyScreen 2500; evq "$CS/tut.cs"; pause 800
capture 01-diplomacy "diplomacy page"
delay0; inp ui.next; at "tip-diplomacy"; tip diplomacy-card; delayrestore
hidewin DiplomacyScreen; pause 800

curl -s -X POST --data-binary "@$CS/minor.cs" "$HOST/eval?settle=1500" > "$TMP/minor.out"
grep -oE '"result":"[^"]*"|"error":"[^"]*"' "$TMP/minor.out"
pause 1500
if grep -qE '"result":"minor shown' "$TMP/minor.out"; then
  evq "$CS/tut.cs"; pause 600
  capture 02-minor-civilization "minor civilization window"
  delay0; inp ui.next; at "tip-minor"; tip minor-relation; delayrestore
  exitwin MinorFactionDiplomacyModalWindow; hidewin MinorFactionDiplomacyModalWindow
else
  skip "no minor empire with a system in this galaxy - minor-civilization window not captured"
fi

epilogue
