#!/bin/sh
# The senate page: its dump, and a focused tooltip probe on a cell of its second stop (row 1
# of a region carries the region's drawn caption in its spoken label, so row 2 is the one
# type-ahead and a probe can read cleanly).
set -u
. "$(dirname "$0")/../lib.sh" "$@"

ensure_screen screen.senate SenateScreen
capture page "senate page"
delay0; inp ui.next; inp ui.home; at tip-cell; tip cell; delayrestore
done_
