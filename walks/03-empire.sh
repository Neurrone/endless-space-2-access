#!/bin/sh
# Family: the empire-wide icon-strip screens and the senate family's modals.
# The technology and the systems-table cell are read off their own pages -- row 2 of each
# region, because row 1 carries the region's drawn caption in its spoken label.
set -u
FAMILY=empire; . "$(dirname "$0")/lib.sh" "$@"

prologue

openwin TechnologyScreen 2500; evq "$CS/tut.cs"; pause 800
capture 01-research "technology wheel"
snap "$TMP/res.txt"
TECH=$(label_nth "$TMP/res.txt" 'research:suggested/' 2)
delay0
if [ -n "$TECH" ] && tland "$TECH"; then
  echo "   discovered: technology [$TECH]"
  at "tip-technology"; tip research-technology
else
  skip "no suggested-technology row - technology tooltip not captured"
fi
delayrestore
hidewin TechnologyScreen; evq "$CS/tut.cs"

openwin NarrativeScreen 2500; evq "$CS/tut.cs"; pause 800
capture 02-quest-journal "quest journal"
hidewin NarrativeScreen

openwin EmpireScreen 2500; evq "$CS/tut.cs"; pause 800
capture 03-empire "empire page"
snap "$TMP/emp.txt"
CELL=$(label_nth "$TMP/emp.txt" 'empire:row[^]]*c0\]' 2)
delay0
if [ -n "$CELL" ] && tland "$CELL"; then
  echo "   discovered: empire systems-table row [$CELL]"
  at "tip-empire-cell"; tip empire-systems-cell
else
  skip "fewer than two systems-table rows - empire-cell tooltip not captured"
fi
delayrestore
hidewin EmpireScreen

openwin EconomyScreen 2500; evq "$CS/tut.cs"; pause 800
capture 04-economy "economy page"
delay0; inp ui.next; inp ui.home; at "tip-economy-cell"; tip economy-cell; delayrestore
hidewin EconomyScreen

openwin SenateScreen 2500; evq "$CS/tut.cs"; pause 800
capture 05-senate "senate page"
delay0; inp ui.next; inp ui.home; at "tip-senate"; tip senate-cell; delayrestore

openwin GovernmentModalWindow 2000; evq "$CS/tut.cs"; pause 600
capture 06-government "government modal"
exitwin GovernmentModalWindow; hidewin GovernmentModalWindow

openwin LawsManagementModalWindow 2000; evq "$CS/tut.cs"; pause 600
capture 07-laws "laws modal"
exitwin LawsManagementModalWindow; hidewin LawsManagementModalWindow

openwin PopulationModalWindow 2000; evq "$CS/tut.cs"; pause 600
capture 08-population "population overview modal"
exitwin PopulationModalWindow; hidewin PopulationModalWindow

hidewin SenateScreen
epilogue
