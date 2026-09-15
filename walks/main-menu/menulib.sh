# walks/main-menu/menulib.sh -- helpers for the pages the game draws BEFORE a save is loaded.
# Sourced after ../lib.sh by every scenario in this folder. This folder is not in walk-all.sh:
# it is run on its own against a freshly launched game sitting at the main menu.
#
# The shared drain reads the player empire, which does not exist out of game, so this folder
# has two of its own. `mdrain` closes what is on TOP - the modal stack and the out-game window
# list - and is what a station inside the lobby uses, because the lobby has to survive it.
# `backhome` additionally leaves the lobby with the game's own Exit and puts the menu back: a
# page that REPLACES the menu (the DLC browser, the credits) hid it on the way in and only its
# own Exit would have shown it again. Twice, both of them: closing the window on top can
# UNCOVER one that was standing behind it, and the window under a message box is only shown
# again a frame after the box goes.
mdrain()   { evq "$CS/menudrain.cs"; frame; frame; evq "$CS/menudrain.cs"; waitfor "$P_NOMODAL" 10000; }
backhome() { evq "$CS/menuhome.cs"; frame; frame; evq "$CS/menuhome.cs"; onscreen screen.main-menu 40000 || echo "   NOTE: the main menu did not come back after $1"; }

# ensure_menu -- the main menu focused with nothing over it. Proceeds at once when it is.
ensure_menu() { isonscreen screen.main-menu && return 0; backhome start; }

# optiontabs <file prefix> <label> -- dump an options window one tab at a time and leave it on
# the first. Both windows this folder opens are the game's options modal - the mod's own
# settings are a subclass of it - so both are read the same way, by the tab keys the screen
# declares rather than by counted arrows: a capture resets the cursor, and the tab a counted
# step would land on depends on which stop the reset left it in.
optiontabs() {
  ot_pfx="$1"; ot_lbl="$2"
  snap "$TMP/ot.txt"
  # Counted the same way the rows are indexed, so the loop can never ask for a tab key_nth
  # cannot answer: nkeys counts LINES, and key_nth indexes MATCHES.
  ot_n=$(grep -oE '\[options:tab/[^]]*\]' "$TMP/ot.txt" | wc -l | tr -d ' ')
  echo "   discovered: $ot_n tabs on $ot_lbl"
  ot_i=1
  while [ "$ot_i" -le "$ot_n" ]; do
    ot_c=$(key_nth "$TMP/ot.txt" 'options:tab/[^]]*\]' "$ot_i" | sed 's|.*/||')
    if tkey "$TMP/ot.txt" 'options:tab/[^]]*\]' "$ot_i"; then
      inp ui.click; frame; frame
      capture "$ot_pfx-$ot_c" "$ot_lbl, $ot_c"
    else
      skip "$ot_lbl tab $ot_c could not be landed on"
    fi
    ot_i=$((ot_i+1))
  done
  # left on the first tab: both windows remember the selected one across opens
  if [ "$ot_n" -ge 1 ] && tkey "$TMP/ot.txt" 'options:tab/[^]]*\]' 1; then inp ui.click; frame; frame; fi
}
