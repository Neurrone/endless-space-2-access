#!/bin/sh
# The ground-troop management modal. NEVER presses Apply.
set -u
. "$(dirname "$0")/../lib.sh" "$@"

ensure_modal screen.troop-management GroundTroopManagementModalWindow
capture modal "ground troop management"
exitwin GroundTroopManagementModalWindow
done_
