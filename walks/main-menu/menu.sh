#!/bin/sh
# The main menu itself.
set -u
. "$(dirname "$0")/../lib.sh" "$@"; . "$(dirname "$0")/menulib.sh"

ensure_menu
capture menu "main menu"
snap "$TMP/mm.txt"
echo "   discovered: $(nkeys "$TMP/mm.txt" 'mainmenu:[^]]*\]') main-menu entries"
done_
