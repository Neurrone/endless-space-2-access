#!/bin/sh
# The fleet-selection modal. NEVER presses Confirm.
set -u
. "$(dirname "$0")/../lib.sh" "$@"

ensure_modal screen.fleet-selection FleetSelectionModalWindow
capture modal "fleet-selection modal"
exitwin FleetSelectionModalWindow
done_
