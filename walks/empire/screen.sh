#!/bin/sh
# The empire page: its dump, and a focused tooltip probe on row 2 of the systems table
# (row 1 carries the region's caption).
set -u
. "$(dirname "$0")/../lib.sh" "$@"

ensure_screen screen.empire EmpireScreen
capture page "empire page"
snap "$TMP/emp.txt"
CELL=$(label_nth "$TMP/emp.txt" 'empire:row[^]]*c0\]' 2)
delay0
if [ -n "$CELL" ] && findland "$CELL"; then
  echo "   discovered: empire systems-table row [$CELL]"
  at tip-cell; tip systems-cell
else
  skip "fewer than two systems-table rows - empire-cell tooltip not captured"
fi
delayrestore
done_
