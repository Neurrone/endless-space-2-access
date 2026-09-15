#!/bin/sh
# The planet overview page, reached the player's way: Enter on a planet card of the
# star-system page (row 2 of the planets stop -- row 1 carries the region's caption).
set -u
. "$(dirname "$0")/../lib.sh" "$@"

ensure_system || { done_; exit 0; }
snap "$TMP/page.txt"
P=$(label_nth "$TMP/page.txt" 'system:planet/[0-9]*\]' 2)
if [ -n "$P" ] && findland "$P"; then
  at on-planet
  inp ui.click
  if onscreen screen.planet 20000; then
    tut
    capture page "planet overview page"
  else
    skip "Enter on the planet row did not open the planet page"
  fi
else
  skip "no second planet row to open - planet-overview page not captured"
fi
done_
