#!/bin/sh
# The mod's own settings window, reached the player's way through the pause menu's entry,
# dumped on both tabs. The window REMEMBERS its selected tab across opens, so the route
# selects the first tab explicitly before the first dump and leaves it there.
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
    inp ui.home; inp ui.click; frame; frame          # force the FIRST tab
    capture tab-1 "mod settings, first tab"
    inp ui.down; inp ui.click; frame; frame
    capture tab-2 "mod settings, second tab"
    inp ui.up; inp ui.click; frame; frame            # leave it on the first tab
    inp ui.next; inp ui.next; inp ui.home; inp ui.click   # Cancel
    hidden OptionsModalWindow 10000 || echo "   NOTE: the settings window did not close on Cancel"
  else
    skip "the mod's settings window did not open - both settings-tab dumps not captured"
  fi
else
  skip "the pause menu declares no mod-settings entry - both settings-tab dumps not captured"
fi
done_
