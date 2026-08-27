#!/bin/sh
# Family: the star-system management page (the empire's first owned system, then its
# second, then the first again -- the pooled-row shrink leg), the planet-overview page,
# the improvements modal and the system-politics modal.
#
# The system is whichever one `DepartmentOfTheInterior.ColonizedStarSystems[0]` is; the
# planets are read off the page itself. An empire owning one system loses the page-turn
# legs and says so in skipped.txt.
set -u
FAMILY=system; . "$(dirname "$0")/lib.sh" "$@"

prologue

NSYS=$(fact '((System.Func<string>)(() => { return ((System.Collections.IList)Gui.PlayerEmpire.GetAgency<DepartmentOfTheInterior>().ColonizedStarSystems).Count.ToString(); }))()')
echo "   discovered: $NSYS colonized systems"
if [ "${NSYS:-0}" -lt 1 ] 2>/dev/null; then
  skip "the empire owns no colonized system - the whole star-system family is unreachable"
  epilogue
  exit 0
fi

# ---- the first owned system ---------------------------------------------------------
ev "$CS/sysopen.cs"; pause 3000; evq "$CS/tut.cs"; pause 600
evq "$CS/reset.cs"; pause 600; inp ui.home
at "system-a-page"
dump 01-system-a
ghosts "star system page, first owned system"

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
  if [ -n "$p" ] && tland "$p"; then
    at "tip-planet-card-$n"; tip "sys-planet-card-$n"
  else
    skip "the page has no planet row $n of $NPL - planet-card tooltip $n not captured"
  fi
  n=$((n+1))
done
delayrestore
PLANET2=$(label_nth "$TMP/page.txt" "$PLRE" 2)

# ---- the second owned system, then back (pool shrink) --------------------------------
if [ "${NSYS:-0}" -ge 2 ]; then
  inp ui.pageNext; pause 2500; evq "$CS/tut.cs"; pause 600
  evq "$CS/reset.cs"; pause 600; inp ui.home
  at "system-b-page"
  dump 02-system-b
  ghosts "star system page, second owned system"

  inp ui.pagePrev; pause 2500; evq "$CS/tut.cs"; pause 600
  evq "$CS/reset.cs"; pause 600; inp ui.home
  at "system-a-revisit"
  dump 03-system-a-revisit
  ghosts "star system page, first system revisited (pool shrink)"
else
  skip "the empire owns one system - second-system and pool-shrink page dumps not captured"
fi

# ---- the planet-overview page ---------------------------------------------------------
if [ -n "$PLANET2" ] && tland "$PLANET2"; then
  at "on-planet"
  inp ui.activate; pause 3000; evq "$CS/tut.cs"; pause 600
  evq "$CS/reset.cs"; pause 600; inp ui.home
  at "planet-page"
  dump 04-planet-overview
  ghosts "planet overview page"
  ev "$CS/sysopen.cs"; pause 3000; evq "$CS/tut.cs"; pause 600
else
  skip "no planet row to open - planet-overview page not captured"
fi

# ---- improvements modal ---------------------------------------------------------------
evs '((System.Func<string>)(() => { System.Collections.IList css = (System.Collections.IList)Gui.PlayerEmpire.GetAgency<DepartmentOfTheInterior>().ColonizedStarSystems; var w = Gui.GuiService.GetWindow<ImprovementsManagementModalWindow>(); w.ColonizedStarSystem = (ColonizedStarSystem)css[0]; Gui.GuiService.ShowWindow(w); return "improvements shown"; }))()'
pause 2000; evq "$CS/reset.cs"; pause 600; inp ui.home
at "improvements"
dump 05-improvements-modal
ghosts "improvements modal"
exitwin ImprovementsManagementModalWindow; hidewin ImprovementsManagementModalWindow

# ---- system-politics modal ------------------------------------------------------------
evs '((System.Func<string>)(() => { System.Collections.IList css = (System.Collections.IList)Gui.PlayerEmpire.GetAgency<DepartmentOfTheInterior>().ColonizedStarSystems; var w = Gui.GuiService.GetWindow<StarSystemPopulationModalWindow>(); w.Bind((ColonizedStarSystem)css[0]); Gui.GuiService.ShowWindow(w); return "system politics shown"; }))()'
pause 2200; evq "$CS/tut.cs"; pause 600; evq "$CS/reset.cs"; pause 600; inp ui.home
at "system-politics"
dump 06-system-politics-modal
ghosts "system politics modal"
exitwin StarSystemPopulationModalWindow; hidewin StarSystemPopulationModalWindow

epilogue
