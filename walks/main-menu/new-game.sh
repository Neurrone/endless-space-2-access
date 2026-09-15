#!/bin/sh
# The new-game lobby and the three pages only it opens: the faction choice, the custom-faction
# editor and the advanced game settings. One scenario because the three are reachable through
# the lobby alone. The lobby IS entered -- it is a screen of its own -- and left again with
# the game's own Exit, which is what its Back button calls. NEVER presses Start or Quick Start.
set -u
. "$(dirname "$0")/../lib.sh" "$@"; . "$(dirname "$0")/menulib.sh"

ensure_menu
reset
snap "$TMP/mm.txt"
NEWGAME=$(label_of "$TMP/mm.txt" 'mainmenu:MainMenuNewGame')
if [ -n "$NEWGAME" ] && findland "$NEWGAME"; then
  echo "   discovered: menu entry [$NEWGAME]"
  inp ui.click
  if onscreen screen.new-game 60000; then
    frame; frame
    capture lobby "new-game lobby"
    snap "$TMP/ng.txt"

    # faction choice -- the empire slot's own portrait button
    if tkey "$TMP/ng.txt" 'newgame:empire/change\]' 1 && { inp ui.click; onscreen screen.faction-choice 20000; }; then
      frame; frame
      capture faction-choice "faction choice"
      snap "$TMP/fc.txt"
      # the custom-faction editor, only where a key route reaches it. The band's own first row
      # is Delete, so this asks for the create button by name rather than for "a row of the band".
      ADDFAC=$(label_of "$TMP/fc.txt" 'faction-choice:custom/AddFactionButton')
      if [ -n "$ADDFAC" ] && findland "$ADDFAC" && { inp ui.click; onscreen screen.custom-faction 20000; }; then
        frame; frame
        capture custom-faction "custom-faction editor"
      else
        skip "no key route from faction choice into the custom-faction editor"
      fi
      mdrain; onscreen screen.new-game 20000 || echo "   NOTE: the lobby did not come back after faction choice"
    else
      skip "the lobby declares no faction-choice button - faction choice and the custom-faction editor not captured"
      mdrain
    fi

    # advanced settings -- one button per game-setup category; the first the lobby offers
    snap "$TMP/ng.txt"
    if tkey "$TMP/ng.txt" 'newgame:[^]]*/advanced\]' 1 && { inp ui.click; onscreen screen.advanced-settings 20000; }; then
      frame; frame
      capture advanced-settings "advanced game settings"
      mdrain; onscreen screen.new-game 20000 || echo "   NOTE: the lobby did not come back after advanced settings"
    else
      skip "the lobby declares no advanced-settings button"
      mdrain
    fi
  else
    skip "the new-game lobby did not open - it and the pages it opens not captured"
  fi
else
  skip "the main menu declares no new-game entry - the lobby and everything it opens not captured"
fi
backhome "the lobby"
done_
