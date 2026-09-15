#!/bin/sh
# The planet-cards panel a systems-table row slides out, in BOTH of its modes (Actions, which
# a status cell opens, and Population, which a population cell opens), dumped for the
# empire's LARGEST owned system; then the A-B-A rebind check of its pooled cards
# (PlanetCard) against the SMALLEST owned system, so the pool retires some before it grows
# back.
#
# The panel is opened through the game's own opener rather than by clicking a cell: which
# cell opens which detail is decided by the cell's TYPE, and one of them opens a hero modal
# (StarSystemsManagementPanel :311-318 chooses; :342 / :351 are the two openers).
set -u
. "$(dirname "$0")/../lib.sh" "$@"

EMPCARDS='t = Gui.GuiService.GetWindow<EmpireScreen>(false).StarSystemsManagementPanel.StarSystemPlanetCardsPanel.PlanetCardsTable;'
P_PANEL='Gui.GuiService.GetWindow<EmpireScreen>(false).StarSystemsManagementPanel.StarSystemPlanetCardsPanel.Shown && Gui.GuiService.GetWindow<EmpireScreen>(false).StarSystemsManagementPanel.StarSystemPlanetCardsPanel.Mode == PlanetCard.DisplayMode.'

# selrow <system name> <Actions|Population> -- select that row of the systems table and open
# the cards panel in that mode. Non-zero when type-ahead cannot reach the row.
selrow() {
  reset
  findland "$1" || return 1
  inp ui.click
  evs "((System.Func<string>)(() => { var w = Gui.GuiService.GetWindow<EmpireScreen>(false); var p = w.StarSystemsManagementPanel; if (p == null || p.GuiTable == null || p.GuiTable.SelectedLine == null) return \"NO SELECTED ROW\"; var m = typeof(StarSystemsManagementPanel).GetMethod(\"ShowStarSystemPlanetCardsPanelWith$2\", System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance); if (m == null) return \"NO OPENER\"; m.Invoke(p, null); return \"planet cards shown=\" + p.StarSystemPlanetCardsPanel.Shown; }))()"
  waitfor "$P_PANEL$2" 10000 || { echo "   NOTE: the cards panel did not open in $2 mode"; return 1; }
  frame; frame
}

REC=$(colonized)
if [ -z "$REC" ]; then skip "the empire owns no colonized system - no planet cards"; done_; exit 0; fi
BIG=$(biggest "$REC"); SML=$(smallest "$REC")
TWO=0; [ "$(rkey "$BIG")" != "$(rkey "$SML")" ] && TWO=1

ensure_screen screen.empire EmpireScreen
if ! selrow "$(rlabel "$BIG")" Actions; then
  skip "the systems table has no row for the largest owned system - planet cards not captured"; done_; exit 0
fi
at cards-actions
dump actions
ghosts "empire page, planet cards in Actions mode, largest owned system (A)"
gwalk graph a 'empire:planet/'
gpool pool  a "$EMPCARDS"

selrow "$(rlabel "$BIG")" Population
at cards-population
dump population
ghosts "empire page, planet cards in Population mode, largest owned system"

if [ "$TWO" -ne 1 ]; then
  skip "the empire owns fewer than two colonized systems - the empire planet-card rebind is unreachable"
elif selrow "$(rlabel "$SML")" Actions; then
  at cards-b
  gpool poolb b "$EMPCARDS"
  selrow "$(rlabel "$BIG")" Actions
  at cards-a-prime
  gwalk graph a2 'empire:planet/'
  gpool pool  a2 "$EMPCARDS"
  aba graph pool
else
  skip "the systems table has no row for the smallest owned system - the empire planet-card rebind is unreachable"
fi
done_
