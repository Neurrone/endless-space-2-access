#!/bin/sh
# The ship designer in CREATION mode with no ship bound (safe: never Create, Apply or
# Retrofit), then its hull drop list, addressed by node key with the label read back.
set -u
. "$(dirname "$0")/../lib.sh" "$@"

ensure_modal screen.ship-design ShipDesignModalWindow \
  '((System.Func<string>)(() => { var w = Gui.GuiService.GetWindow<ShipDesignModalWindow>(false); w.Bind(null); Gui.GuiService.ShowWindow(w); return "designer shown="+w.Shown; }))()'
capture designer "ship designer (creation mode)"

snap "$TMP/design.txt"
inp ui.next
HULL=$(label_of "$TMP/design.txt" 'shipdesign/info/hull')
if [ -n "$HULL" ] && findland "$HULL"; then
  echo "   discovered: hull combo [$HULL]"
  inp ui.click
  if onscreen screen.drop-list 10000; then
    capture hull-drop-list "hull drop list"
    inp ui.back
    onscreen screen.ship-design 10000 || echo "   NOTE: the designer did not take focus back"
  else
    skip "the hull combo did not open its drop list"
  fi
else
  skip "the ship designer declares no combo box - hull drop list not captured"
fi
exitwin ShipDesignModalWindow
done_
