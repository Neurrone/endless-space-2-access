#!/bin/sh
# The military page: its dump, and a focused tooltip probe on row 2 of the fleets table.
set -u
. "$(dirname "$0")/../lib.sh" "$@"

ensure_screen screen.military MilitaryScreen
capture page "military page"
snap "$TMP/mil.txt"
ROW=$(label_nth "$TMP/mil.txt" 'military:row[^]]*c0\]' 2)
delay0
if [ -n "$ROW" ] && findland "$ROW"; then
  echo "   discovered: fleet row [$ROW]"
  at tip-fleet-row; tip fleet-row
else
  skip "fewer than two fleet rows - fleet-row tooltip not captured"
fi
delayrestore
done_
