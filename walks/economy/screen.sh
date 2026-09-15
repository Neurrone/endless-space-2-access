#!/bin/sh
# The economy page: its dump, and a focused tooltip probe on a cell of its second stop.
set -u
. "$(dirname "$0")/../lib.sh" "$@"

ensure_screen screen.economy EconomyScreen
capture page "economy page"
delay0; inp ui.next; inp ui.home; at tip-cell; tip cell; delayrestore
done_
