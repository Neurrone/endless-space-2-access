#!/bin/sh
# The game's own options, tab by tab. Unlike the in-game scenario, this one DOES select tabs:
# it selects every one it found and ends on the first, so the tab the modal remembers is the
# same on both halves of a pair.
set -u
. "$(dirname "$0")/../lib.sh" "$@"; . "$(dirname "$0")/menulib.sh"

ensure_menu
evs '((System.Func<string>)(() => { var w = Gui.GuiService.GetWindow<OptionsModalWindow>(); if (w == null) return "NO WINDOW"; w.OutGameSkin = true; Gui.GuiService.ShowWindow(w); return "options shown=" + w.Shown; }))()'
if onscreen screen.options 20000; then
  frame; frame
  capture modal "game options, as opened"
  optiontabs tab "game options"
else
  skip "the game options modal did not open"
fi
backhome "the options"
done_
