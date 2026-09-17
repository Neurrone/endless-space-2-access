#!/bin/sh
# The mod's own settings window, reached the player's way through the pause menu's entry,
# dumped one tab at a time - every tab the window declares, counted at runtime. The window
# REMEMBERS its selected tab across opens, so the route leaves it on the first one.
set -u
. "$(dirname "$0")/../lib.sh" "$@"

ensure_modal screen.game-menu GameMenuModalWindow
reset
snap "$TMP/gm.txt"
MODSET=$(label_of "$TMP/gm.txt" 'gamemenu:mod-settings')
if [ -n "$MODSET" ] && findland "$MODSET"; then
  echo "   discovered: pause-menu entry [$MODSET]"
  inp ui.click
  # The mod's window IS the game's options modal with the mod's content in it, so the screen
  # it focuses is the options screen.
  if onscreen screen.options 15000; then
    frame; frame
    optiontabs tab "mod settings"
    inp ui.next; inp ui.next; inp ui.home; inp ui.click   # Cancel
    hidden OptionsModalWindow 10000 || echo "   NOTE: the settings window did not close on Cancel"
  else
    skip "the mod's settings window did not open - no settings tab dumped"
  fi
else
  skip "the pause menu declares no mod-settings entry - no settings tab dumped"
fi
done_
