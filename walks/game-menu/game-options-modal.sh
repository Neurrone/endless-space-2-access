#!/bin/sh
# The game's own options modal. It remembers its selected tab across opens and this route
# never touches its tabs, so whichever tab a human last left it on is the one both halves
# of a pair dump. Leave it on Video.
set -u
. "$(dirname "$0")/../lib.sh" "$@"

ensure_modal screen.options OptionsModalWindow
capture modal "game options modal"
exitwin OptionsModalWindow
done_
