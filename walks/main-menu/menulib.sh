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

