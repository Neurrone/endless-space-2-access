#!/bin/sh
# The marketplace: its section radios (MarketTabRadio) and the item list under them, read
# with the section holding MORE rows selected, then the A-B-A rebind check against the
# section with fewer rows -- selecting a section re-Binds every radio in the pool and
# re-fills the item table.
#
# Which economy tab holds the marketplace, and which of its sections are selectable, is
# read off the page. A section the empire may not trade in is drawn switched off and
# refuses the selection.
set -u
. "$(dirname "$0")/../lib.sh" "$@"

BUYRADIOS='t = Gui.GuiService.GetWindow<EconomyScreen>(false).MarketplacePanel.BuyableItemsPanel.MarketTabRadiosTable;'
SELLRADIOS='t = Gui.GuiService.GetWindow<EconomyScreen>(false).MarketplacePanel.SalableItemsPanel.MarketTabRadiosTable;'

# The first radio of the band is its region's first row, so its spoken label opens with the
# region's drawn caption and type-ahead can never find its own text. The route therefore
# lands on the SECOND radio by text and reaches the first with ui.left -- the radios sit
# side by side, so Left and Right are the steps between them, not Up and Down.
# selsect <label> <extra step or empty> -- select a section, echo how many item rows it fills
# No reset here: the readings are walker captures, which expand and read by key, and a
# cursor left standing on the radios lands the next selection without walking the stops.
selsect() { findland "$1" || return 1
            if [ -n "${2:-}" ]; then inp "$2"; fi
            inp ui.click; frame; frame
            snap "$TMP/sec.txt"; nkeys "$TMP/sec.txt" 'economy:buy/row[^]]*\]'; }

ensure_screen screen.economy EconomyScreen
reset
snap "$TMP/eco.txt"
NTAB=$(nkeys "$TMP/eco.txt" 'economy:tab/[0-9]*\]')
echo "   discovered: $NTAB economy tab(s)"
# The tab already selected may be the marketplace (the screen remembers it across opens);
# only when it is not does the route walk the tabs.
MKT=0; i=1
[ "$(nkeys "$TMP/eco.txt" 'economy:buy/filter/[0-9]*\]')" -ge 2 ] && MKT=1
while [ "$MKT" -ne 1 ] && [ "$i" -le "${NTAB:-0}" ]; do
  tab=$(label_nth "$TMP/eco.txt" 'economy:tab/[0-9]*\]' "$i")
  # The tab stop is four stops BEHIND the reset cursor and nine ahead of it.
  if [ -n "$tab" ] && findland "$tab" 6 ui.prev; then
    inp ui.click; frame; frame; reset
    snap "$TMP/eco.txt"
    if [ "$(nkeys "$TMP/eco.txt" 'economy:buy/filter/[0-9]*\]')" -ge 2 ]; then MKT=1; break; fi
  fi
  i=$((i+1))
done

SEC1=$(grep -E '\[economy:buy/filter/0\]' "$TMP/eco.txt")
SEC2=$(grep -E '\[economy:buy/filter/1\]' "$TMP/eco.txt")
SECLBL=$(label_nth "$TMP/eco.txt" 'economy:buy/filter/[0-9]*\]' 2)
if [ "$MKT" -ne 1 ]; then
  skip "no economy tab offers two or more marketplace sections - the marketplace is unreachable"
elif [ -z "$SECLBL" ] || printf '%s\n%s\n' "$SEC1" "$SEC2" | grep -q ', unavailable,'; then
  skip "the first two marketplace sections are not both selectable - the radio rebind is unreachable"
else
  C1=$(selsect "$SECLBL" ui.left); C2=$(selsect "$SECLBL" "")
  echo "   discovered: the first two sections fill $C1 and $C2 item rows"
  if [ "${C1:-0}" -ge "${C2:-0}" ]; then ASTEP=ui.left; BSTEP=""; else ASTEP=""; BSTEP=ui.left; fi
  selsect "$SECLBL" "$ASTEP" > /dev/null
  at section-a
  dump section-a
  ghosts "economy marketplace, the section with the most rows (A)"
  gwalk buy  a 'economy:buy/'
  gwalk sell a 'economy:sell/'
  gpool poolbuy  a "$BUYRADIOS"
  gpool poolsell a "$SELLRADIOS"

  selsect "$SECLBL" "$BSTEP" > /dev/null
  at section-b
  gpool poolbuyb b "$BUYRADIOS"

  selsect "$SECLBL" "$ASTEP" > /dev/null
  at section-a-prime
  gwalk buy  a2 'economy:buy/'
  gwalk sell a2 'economy:sell/'
  gpool poolbuy  a2 "$BUYRADIOS"
  gpool poolsell a2 "$SELLRADIOS"
  aba buy sell poolbuy poolsell
fi
done_
