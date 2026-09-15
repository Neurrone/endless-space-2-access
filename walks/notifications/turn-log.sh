#!/bin/sh
# The HUD after the turn-log key: records whether this fixture declares that stop at all.
set -u
. "$(dirname "$0")/../lib.sh" "$@"

ensure_galaxy
inp ui.focusTurnLog
at turn-log-key
dump hud
ghosts "galaxy HUD after the turn-log key"
snap "$TMP/tl.txt"
if [ "$(nkeys "$TMP/tl.txt" 'hud:turn-log')" -eq 0 ]; then
  skip "hud:turn-log is not declared in this fixture - the key leaves the cursor where it was"
fi
done_
