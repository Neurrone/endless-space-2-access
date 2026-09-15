#!/bin/sh
# The star-system management page: the page for the empire's LARGEST owned system, focused
# tooltip probes on two of its planet cards, then the page turned to the SMALLEST owned
# system and back, which is both the pool-shrink dump and the A-B-A rebind check of the
# pooled planet cards (PlanetLabel_SystemManagement): a card rebound from a bigger system
# keeps whatever the previous binding left on its components, and only reading A, then B
# with fewer planets, then A again shows it.
#
# Which systems those are is discovered at runtime from the empire's colonized list; an
# empire owning one system loses the page-turn legs and says so in skipped.txt.
set -u
. "$(dirname "$0")/../lib.sh" "$@"

SYSCARDS='t = Gui.GuiService.GetWindow<PlanetLabelsWindow_SystemManagement>(false).PlanetLabelsContainer;'
REC=$(colonized)
if [ -z "$REC" ]; then
  skip "the empire owns no colonized system - the whole star-system family is unreachable"
  done_; exit 0
fi
BIG=$(biggest "$REC"); SML=$(smallest "$REC")
TWO=0; [ "$(rkey "$BIG")" != "$(rkey "$SML")" ] && TWO=1
echo "   discovered: $(echo "$REC" | tr '|' '\n' | grep -c .) colonized system(s); A has $(rcount "$BIG") planets, B has $(rcount "$SML")"

ensure_system "$(rkey "$BIG")"
capture page "star system page, largest owned system (A)"

# ---- focused tooltip pass over the planet cards --------------------------------------
# Row 1 of the region carries the drawn caption ("Planets, <name>, ..."), so the rows that
# read cleanly start at 2.
snap "$TMP/page.txt"
PLRE='system:planet/[0-9]*\]'
NPL=$(nkeys "$TMP/page.txt" "$PLRE")
delay0
n=2
while [ "$n" -le 3 ]; do
  p=$(label_nth "$TMP/page.txt" "$PLRE" "$n")
  if [ -n "$p" ] && findland "$p"; then
    at "tip-planet-card-$n"; tip "planet-card-$n"
  else
    skip "the page has no planet row $n of $NPL - planet-card tooltip $n not captured"
  fi
  n=$((n+1))
done
delayrestore

# ---- A-B-A over the pooled cards ------------------------------------------------------
gwalk graph a 'system:planet'
gpool pool  a "$SYSCARDS"
if [ "$TWO" -ne 1 ]; then
  skip "the empire owns fewer than two colonized systems - page turn and planet-card rebind unreachable"
else
  ensure_system "$(rkey "$SML")"                 # B: fewer planets, so the card pool retires some
  capture page-b "star system page, smallest owned system (B)"
  gpool poolb b "$SYSCARDS"

  ensure_system "$(rkey "$BIG")"
  capture page-revisit "star system page, largest owned system revisited (A-prime)"
  gwalk graph a2 'system:planet'
  gpool pool  a2 "$SYSCARDS"
  aba graph pool
fi
done_
