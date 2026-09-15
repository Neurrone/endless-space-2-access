#!/bin/sh
# The system-politics modal, bound to the empire's first colonized system.
set -u
. "$(dirname "$0")/../lib.sh" "$@"

ensure_modal screen.system-politics StarSystemPopulationModalWindow \
  '((System.Func<string>)(() => { System.Collections.IList css = (System.Collections.IList)Gui.PlayerEmpire.GetAgency<DepartmentOfTheInterior>().ColonizedStarSystems; if (css.Count == 0) return "NO COLONIZED SYSTEM"; var w = Gui.GuiService.GetWindow<StarSystemPopulationModalWindow>(); w.Bind((ColonizedStarSystem)css[0]); Gui.GuiService.ShowWindow(w); return "system politics shown"; }))()'
capture modal "system politics modal"
exitwin StarSystemPopulationModalWindow
done_
