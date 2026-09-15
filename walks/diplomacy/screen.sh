#!/bin/sh
# The diplomacy page: its dump, a focused tooltip probe on a card, then the A-B-A rebind
# check of the pooled ring sectors (EmpireSector). A wedge is never CLICKED -- activating
# one opens the negotiation modal, and closing an unsigned negotiation posts an order -- so
# the rebind is the screen's own hover selection, which fades each empire's detail block in
# and out, driven by moving the cursor between two wedges. NEVER presses a diplomacy action.
set -u
. "$(dirname "$0")/../lib.sh" "$@"

SECTORS='t = Gui.GuiService.GetWindow<DiplomacyScreen>(false).EmpireSectorsContainer;'
ensure_screen screen.diplomacy DiplomacyScreen
capture page "diplomacy page"
delay0; inp ui.next; at tip-card; tip card; delayrestore

reset
snap "$TMP/dip.txt"
EMPRE='diplomacy:empire/[0-9]*\]'
# Row 1 of the ring carries the region's drawn caption in its spoken label, so the rows
# type-ahead can find start at 2.
DA=$(label_nth "$TMP/dip.txt" "$EMPRE" 2); DB=$(label_nth "$TMP/dip.txt" "$EMPRE" 3)
echo "   discovered: $(nkeys "$TMP/dip.txt" "$EMPRE") empires on the ring"
if [ -z "$DA" ] || [ -z "$DB" ]; then
  skip "fewer than three empires on the diplomacy ring - the sector rebind is unreachable"
elif ! findland "$DA"; then
  skip "type-ahead could not land on a diplomacy wedge - the sector rebind is unreachable"
else
  frame; frame
  at wedge-a
  gwalk graph a 'diplomacy:empire/'
  gpool pool  a "$SECTORS"
  if findland "$DB"; then
    frame; frame
    at wedge-b
    gpool poolb b "$SECTORS"
    findland "$DA"; frame; frame
    at wedge-a-prime
    gwalk graph a2 'diplomacy:empire/'
    gpool pool  a2 "$SECTORS"
    aba graph pool
  else
    skip "type-ahead could not land on a second diplomacy wedge - the sector rebind is unreachable"
  fi
fi
done_
