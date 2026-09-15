#!/bin/sh
# Load/save in its "load from game" mode. Nothing is ever loaded, written or deleted.
set -u
. "$(dirname "$0")/../lib.sh" "$@"

ensure_modal screen.load-save LoadSaveModalWindow \
  '((System.Func<string>)(() => { var w = Gui.GuiService.GetWindow("LoadSaveModalWindow") as LoadSaveModalWindow; w.LoadSaveMode = LoadSaveModalWindow.LoadSaveType.LoadFromGame; Gui.GuiService.ShowWindow(w); return "loadsave shown="+w.Shown; }))()'
capture modal "load/save modal (Load from game)"
hidewin LoadSaveModalWindow
done_
