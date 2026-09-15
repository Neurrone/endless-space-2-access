#!/bin/sh
# The rename box, shared by every screen that renames something (system, fleet, hero),
# seeded with a harness-authored constant, never a fixture name. NEVER confirms.
set -u
. "$(dirname "$0")/../lib.sh" "$@"

ensure_modal screen.rename RenameModalWindow \
  '((System.Func<string>)(() => { var w = Gui.GuiService.GetWindow<RenameModalWindow>(); w.OriginalName = "walk probe"; Gui.GuiService.ShowWindow(w); return "rename shown="+w.Shown; }))()'
capture box "rename box"
exitwin RenameModalWindow
# leave no game-owned text field holding the keyboard
evs 'AgeManager.Instance.FocusedControl = null'
done_
