#!/bin/sh
# The improvements modal, bound to the empire's first colonized system.
set -u
. "$(dirname "$0")/../lib.sh" "$@"

ensure_modal screen.improvements ImprovementsManagementModalWindow \
  '((System.Func<string>)(() => { System.Collections.IList css = (System.Collections.IList)Gui.PlayerEmpire.GetAgency<DepartmentOfTheInterior>().ColonizedStarSystems; if (css.Count == 0) return "NO COLONIZED SYSTEM"; var w = Gui.GuiService.GetWindow<ImprovementsManagementModalWindow>(); w.ColonizedStarSystem = (ColonizedStarSystem)css[0]; Gui.GuiService.ShowWindow(w); return "improvements shown"; }))()'
capture modal "improvements modal"
exitwin ImprovementsManagementModalWindow
done_
