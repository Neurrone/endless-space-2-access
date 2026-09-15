#!/bin/sh
# Load/save in the mode the menu opens it in. Nothing is loaded, written or deleted.
set -u
. "$(dirname "$0")/../lib.sh" "$@"; . "$(dirname "$0")/menulib.sh"

ensure_menu
evs '((System.Func<string>)(() => { var w = Gui.GuiService.GetWindow("OutGameLoadModalWindow") as LoadSaveModalWindow; if (w == null) return "NO WINDOW"; w.LoadSaveMode = LoadSaveModalWindow.LoadSaveType.Load; Gui.GuiService.ShowWindow(w); return "loadsave shown=" + w.Shown; }))()'
if onscreen screen.load-save 20000; then
  frame; frame
  capture modal "load/save (Load, from the menu)"
else
  skip "the out-game load window did not open"
fi
backhome "load/save"
done_
