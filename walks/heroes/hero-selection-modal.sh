#!/bin/sh
# The hero-selection modal. The mod's screen goes by the DELEGATE the opener installs, not by
# the window being shown, so the route opens it the way an assign affordance does
# (ColonyHeroSidePanel.OnAssignCb :268-273: a delegate, an assignation, then show) with a
# do-nothing delegate and the empire's first colonized system as the assignation. NEVER
# presses Confirm, so the delegate is never called. The strip is drawn from the empire's
# active heroes, so an empire with none is skipped and recorded; by-key.sh covers it either
# way.
set -u
. "$(dirname "$0")/../lib.sh" "$@"

NHERO=$(fact '((System.Func<string>)(() => { var d = Gui.PlayerEmpire.GetAgency<DepartmentOfEducation>(); if (d == null) return "0"; return ((System.Collections.IList)d.ActiveHeroes).Count.ToString(); }))()')
echo "   discovered: ${NHERO:-?} heroes"
if [ "${NHERO:-0}" -ge 1 ] 2>/dev/null; then
  ensure_modal screen.hero-selection HeroSelectionModalWindow \
    '((System.Func<string>)(() => { var w = Gui.GuiService.GetWindow<HeroSelectionModalWindow>(); System.Collections.IList css = (System.Collections.IList)Gui.PlayerEmpire.GetAgency<DepartmentOfTheInterior>().ColonizedStarSystems; if (css.Count == 0) return "NO COLONIZED SYSTEM"; w.Assignation = (ColonizedStarSystem)css[0]; w.Delegate = delegate(Hero h) { }; Gui.GuiService.ShowWindow(w); return "hero selection shown="+w.Shown; }))()'
  capture modal "hero selection modal"
  exitwin HeroSelectionModalWindow
else
  skip "the empire owns no hero - hero-selection window not captured"
fi
done_
