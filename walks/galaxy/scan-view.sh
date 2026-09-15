#!/bin/sh
# Scan view: toggle it on, dump, toggle it off.
#
# Scan view is not a screen of its own to the mod: the galaxy screen keeps the focus and
# gains the scan stop, so the arrival is the game's own IsInScanView plus the stop appearing
# in the mod's dump, never a screen key. The toggle is a toggle, so the route leaves scan
# view before it enters it.
set -u
. "$(dirname "$0")/../lib.sh" "$@"

ensure_galaxy
evs '((System.Func<string>)(() => { var gm = (GuiManager)Gui.GuiService; if (gm.IsInScanView) Gui.GuiGameWindowService.ToggleScanView(); return "scan view off"; }))()'
evs 'Gui.GuiGameWindowService.ToggleScanView()'
waitfor '((GuiManager)Gui.GuiService).IsInScanView' 10000 || echo "   NOTE: the game did not enter scan view"
waitdump 'scan:' || echo "   NOTE: no scan stop was declared in scan view"
capture view "scan view"
evs 'Gui.GuiGameWindowService.ToggleScanView()'
waitfor '!((GuiManager)Gui.GuiService).IsInScanView' 10000 || echo "   NOTE: the game did not leave scan view"
onscreen screen.galaxy 10000 || echo "   NOTE: the galaxy HUD did not come back"
done_
