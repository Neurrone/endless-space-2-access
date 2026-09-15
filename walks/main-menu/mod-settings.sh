#!/bin/sh
# The mod's own settings, through the entry it added to the main menu, one tab at a time.
set -u
. "$(dirname "$0")/../lib.sh" "$@"; . "$(dirname "$0")/menulib.sh"

ensure_menu
reset
snap "$TMP/mm.txt"
MODSET=$(label_of "$TMP/mm.txt" 'mainmenu:mod-settings')
if [ -n "$MODSET" ] && findland "$MODSET"; then
  echo "   discovered: menu entry [$MODSET]"
  inp ui.click
  # The mod's window IS the game's options modal with the mod's content in it, so the screen it
  # focuses is the options screen; only the name it speaks says which of the two is up.
  if onscreen screen.options 20000; then
    frame; frame
    optiontabs tab "mod settings"
  else
    skip "the mod's settings window did not open - no settings tab dumped"
  fi
else
  skip "the main menu declares no mod-settings entry - no settings tab dumped"
fi
backhome "the mod settings"
done_
