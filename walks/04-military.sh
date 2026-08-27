#!/bin/sh
# Family: military page, fleet-selection modal, ship designer (+ its hull drop list),
# ground-troop management, the battle-tactics deck.
# NEVER presses Retrofit / Create / Apply / Confirm.
set -u
FAMILY=military; . "$(dirname "$0")/lib.sh" "$@"

prologue

openwin MilitaryScreen 2500; evq "$CS/tut.cs"; pause 800
capture 01-military "military page"
snap "$TMP/mil.txt"
ROW=$(label_nth "$TMP/mil.txt" 'military:row[^]]*c0\]' 2)
delay0
if [ -n "$ROW" ] && tland "$ROW"; then
  echo "   discovered: fleet row [$ROW]"
  at "tip-fleet-row"; tip military-fleet-row
else
  skip "fewer than two fleet rows - fleet-row tooltip not captured"
fi
delayrestore

openwin FleetSelectionModalWindow 2000; evq "$CS/tut.cs"; pause 600
capture 02-fleet-selection "fleet-selection modal"
exitwin FleetSelectionModalWindow; hidewin FleetSelectionModalWindow

# ship designer, CREATION mode with no ship bound (safe: never Create/Apply)
evs '((System.Func<string>)(() => { var w = Gui.GuiService.GetWindow<ShipDesignModalWindow>(false); w.Bind(null); Gui.GuiService.ShowWindow(w); return "designer shown="+w.Shown; }))()'
pause 2500; evq "$CS/tut.cs"; pause 800
capture 03-ship-designer "ship designer (creation mode)"

# the hull drop list -- addressed by node key, its label read back off the dump
snap "$TMP/design.txt"
inp ui.next
HULL=$(label_of "$TMP/design.txt" 'shipdesign/info/hull')
if [ -n "$HULL" ] && tland "$HULL"; then
  echo "   discovered: hull combo [$HULL]"
  inp ui.activate; pause 1500
  capture 04-hull-drop-list "hull drop list"
  inp ui.back; pause 1000
else
  skip "the ship designer declares no combo box - hull drop list not captured"
fi
evq "$CS/drain.cs"; pause 1500

openwin GroundTroopManagementModalWindow 2000; evq "$CS/tut.cs"; pause 600
capture 05-troop-management "ground troop management"
exitwin GroundTroopManagementModalWindow; hidewin GroundTroopManagementModalWindow

openwin PlayCardDeckModalWindow 2000; evq "$CS/tut.cs"; pause 600
capture 06-battle-tactics "battle tactics deck"
exitwin PlayCardDeckModalWindow; hidewin PlayCardDeckModalWindow

hidewin MilitaryScreen
epilogue
